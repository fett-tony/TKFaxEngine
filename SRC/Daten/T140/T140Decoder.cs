/*
 * TKFaxEngine - managed C# port
 *
 * T140Decoder.cs
 *
 * ITU-T T.140 streaming decoder.
 */

#nullable enable

using System.Buffers;
using System.Text;

namespace TKFaxEngine.Daten.T140;

/// <summary>
/// Streaming UTF-8 and T.140 presentation-protocol decoder. Only an
/// incomplete UTF-8 scalar or an incomplete control element is retained
/// between calls.
/// </summary>
public sealed class T140Decoder {
    private const int MaximumControlBytes =
        (T140Controls.MaximumApplicationParameterCharacters + 3) * 4;

    private readonly byte[] _pendingUtf8 = new byte[4];
    private readonly byte[] _controlBytes = new byte[MaximumControlBytes];
    private int _pendingUtf8Length;
    private int _controlLength;
    private int _controlCharacters;
    private T140ParserState _parserState;
    private bool _atSessionStart = true;
    private bool _pendingCarriageReturn;
    private bool _applicationFunctionSeen;
    private bool _ucsSubsetDesignation;

    /// <summary>Resets UTF-8, protocol-element and session state.</summary>
    public void Reset() {
        _pendingUtf8Length = 0;
        _controlLength = 0;
        _controlCharacters = 0;
        _parserState = T140ParserState.Normal;
        _atSessionStart = true;
        _pendingCarriageReturn = false;
        _applicationFunctionSeen = false;
        _ucsSubsetDesignation = false;
    }

    /// <summary>
    /// Consumes one UTF-8 octet and emits complete validated T.140 elements.
    /// The initial U+FEFF signature is consumed and not forwarded as text.
    /// </summary>
    public void PutByte(byte value, T140ByteSink sink) {
        ArgumentNullException.ThrowIfNull(sink);
        PutByteCore(value, sink, null);
    }

    /// <summary>Consumes one UTF-8 octet and emits classified T.140 elements.</summary>
    public void PutByte(byte value, T140ElementSink sink) {
        ArgumentNullException.ThrowIfNull(sink);
        PutByteCore(value, null, sink);
    }

    /// <summary>Consumes a streaming block of UTF-8 octets.</summary>
    public void Put(ReadOnlySpan<byte> data, T140ByteSink sink) {
        ArgumentNullException.ThrowIfNull(sink);
        foreach (byte value in data)
            PutByteCore(value, sink, null);
    }

    /// <summary>Consumes a streaming block and emits classified elements.</summary>
    public void Put(ReadOnlySpan<byte> data, T140ElementSink sink) {
        ArgumentNullException.ThrowIfNull(sink);
        foreach (byte value in data)
            PutByteCore(value, null, sink);
    }

    /// <summary>
    /// Terminates a carrier. An incomplete UTF-8 scalar becomes one U+FFFD
    /// missing-text marker; incomplete control strings are discarded.
    /// </summary>
    public void CarrierDown(T140ByteSink sink) {
        ArgumentNullException.ThrowIfNull(sink);
        CarrierDownCore(sink, null);
    }

    /// <summary>Terminates a carrier and emits classified final elements.</summary>
    public void CarrierDown(T140ElementSink sink) {
        ArgumentNullException.ThrowIfNull(sink);
        CarrierDownCore(null, sink);
    }

    private void PutByteCore(
        byte value,
        T140ByteSink? byteSink,
        T140ElementSink? elementSink) {
        if (_pendingUtf8Length == _pendingUtf8.Length) {
            _atSessionStart = false;
            EmitMissingText(byteSink, elementSink);
            ShiftPendingUtf8(1);
        }

        _pendingUtf8[_pendingUtf8Length++] = value;
        ProcessPendingUtf8(byteSink, elementSink);
    }

    private void ProcessPendingUtf8(
        T140ByteSink? byteSink,
        T140ElementSink? elementSink) {
        Span<byte> encoded = stackalloc byte[4];
        while (_pendingUtf8Length > 0) {
            OperationStatus status = Rune.DecodeFromUtf8(
                _pendingUtf8.AsSpan(0, _pendingUtf8Length),
                out Rune rune,
                out int consumed);

            if (status == OperationStatus.NeedMoreData)
                return;

            if (status == OperationStatus.Done) {
                ShiftPendingUtf8(consumed);
                int length = rune.EncodeToUtf8(encoded);
                ProcessRune(rune, encoded[..length], byteSink, elementSink);
                continue;
            }

            int invalidLength = Math.Max(consumed, 1);
            ShiftPendingUtf8(invalidLength);
            _atSessionStart = false;
            EmitMissingText(byteSink, elementSink);
        }
    }

    private void ProcessRune(
        Rune rune,
        ReadOnlySpan<byte> encoded,
        T140ByteSink? byteSink,
        T140ElementSink? elementSink) {
        if (_atSessionStart) {
            _atSessionStart = false;
            if (rune.Value == T140Controls.SessionSignature) {
                elementSink?.Invoke(T140ElementType.SessionSignature, encoded);
                return;
            }
        }

        if (_pendingCarriageReturn) {
            _pendingCarriageReturn = false;
            if (rune.Value == T140Controls.LineFeed) {
                Span<byte> newLine = stackalloc byte[2] {
                    (byte)T140Controls.CarriageReturn,
                    (byte)T140Controls.LineFeed
                };
                Emit(T140ElementType.NewLine, newLine, byteSink, elementSink);
                return;
            }

            Span<byte> carriageReturn = stackalloc byte[1] {
                (byte)T140Controls.CarriageReturn
            };
            Emit(T140ElementType.Text, carriageReturn, byteSink, elementSink);
        }

        switch (_parserState) {
            case T140ParserState.Escape:
                ProcessEscape(rune, encoded, byteSink, elementSink);
                return;

            case T140ParserState.SelectGraphicRendition:
                ProcessSgr(rune, encoded, byteSink, elementSink);
                return;

            case T140ParserState.ApplicationProtocol:
                ProcessApplicationProtocol(rune, encoded, byteSink, elementSink);
                return;
        }

        ProcessNormalRune(rune, encoded, byteSink, elementSink);
    }

    private void ProcessNormalRune(
        Rune rune,
        ReadOnlySpan<byte> encoded,
        T140ByteSink? byteSink,
        T140ElementSink? elementSink) {
        switch (rune.Value) {
            case T140Controls.Bell:
                Emit(T140ElementType.Alert, encoded, byteSink, elementSink);
                break;

            case T140Controls.BackSpace:
                Emit(T140ElementType.BackSpace, encoded, byteSink, elementSink);
                break;

            case T140Controls.CarriageReturn:
                _pendingCarriageReturn = true;
                break;

            case T140Controls.LineSeparator:
                Emit(T140ElementType.NewLine, encoded, byteSink, elementSink);
                break;

            case T140Controls.Escape:
                BeginControl(T140ParserState.Escape, encoded);
                break;

            case T140Controls.ControlSequenceIntroducer:
                BeginControl(T140ParserState.SelectGraphicRendition, encoded);
                break;

            case T140Controls.StartOfString:
                BeginControl(T140ParserState.ApplicationProtocol, encoded);
                _applicationFunctionSeen = false;
                break;

            case T140Controls.ReplacementCharacter:
                Emit(T140ElementType.MissingText, encoded, byteSink, elementSink);
                break;

            default:
                Emit(T140ElementType.Text, encoded, byteSink, elementSink);
                break;
        }
    }

    private void ProcessEscape(
        Rune rune,
        ReadOnlySpan<byte> encoded,
        T140ByteSink? byteSink,
        T140ElementSink? elementSink) {
        if (rune.Value == T140Controls.InterruptFinal && AppendControl(encoded)) {
            Emit(
                T140ElementType.Interrupt,
                _controlBytes.AsSpan(0, _controlLength),
                byteSink,
                elementSink);
            EndControl();
            return;
        }

        EndControl();
        ProcessNormalRune(rune, encoded, byteSink, elementSink);
    }

    private void ProcessSgr(
        Rune rune,
        ReadOnlySpan<byte> encoded,
        T140ByteSink? byteSink,
        T140ElementSink? elementSink) {
        if (rune.Value == T140Controls.SelectGraphicRenditionFinal) {
            if (AppendControl(encoded)) {
                Emit(
                    _ucsSubsetDesignation
                        ? T140ElementType.UcsSubset
                        : T140ElementType.SelectGraphicRendition,
                    _controlBytes.AsSpan(0, _controlLength),
                    byteSink,
                    elementSink);
            }
            EndControl();
            return;
        }

        if (rune.Value == ' ') {
            if (_ucsSubsetDesignation || _controlCharacters == 0 || !AppendControl(encoded)) {
                EndControl();
                return;
            }
            _ucsSubsetDesignation = true;
            return;
        }

        bool validParameterCharacter =
            !_ucsSubsetDesignation &&
            (rune.Value is >= '0' and <= '9' || rune.Value == ';');
        if (!validParameterCharacter ||
            _controlCharacters >= T140Controls.MaximumSgrParameterCharacters ||
            !AppendControl(encoded)) {
            EndControl();
            ProcessNormalRune(rune, encoded, byteSink, elementSink);
            return;
        }

        _controlCharacters++;
    }

    private void ProcessApplicationProtocol(
        Rune rune,
        ReadOnlySpan<byte> encoded,
        T140ByteSink? byteSink,
        T140ElementSink? elementSink) {
        if (!_applicationFunctionSeen) {
            if (rune.Value == T140Controls.StringTerminator || !AppendControl(encoded)) {
                EndControl();
                return;
            }

            _applicationFunctionSeen = true;
            return;
        }

        if (rune.Value == T140Controls.StringTerminator) {
            if (AppendControl(encoded)) {
                Emit(
                    T140ElementType.ApplicationProtocol,
                    _controlBytes.AsSpan(0, _controlLength),
                    byteSink,
                    elementSink);
            }
            EndControl();
            return;
        }

        if (_controlCharacters >= T140Controls.MaximumApplicationParameterCharacters ||
            !AppendControl(encoded)) {
            EndControl();
            ProcessNormalRune(rune, encoded, byteSink, elementSink);
            return;
        }

        _controlCharacters++;
    }

    private void CarrierDownCore(
        T140ByteSink? byteSink,
        T140ElementSink? elementSink) {
        if (_pendingUtf8Length > 0)
            EmitMissingText(byteSink, elementSink);

        if (_pendingCarriageReturn) {
            Span<byte> carriageReturn = stackalloc byte[1] {
                (byte)T140Controls.CarriageReturn
            };
            Emit(T140ElementType.Text, carriageReturn, byteSink, elementSink);
        }

        Reset();
    }

    private void BeginControl(
        T140ParserState parserState,
        ReadOnlySpan<byte> introducer) {
        _parserState = parserState;
        _controlLength = 0;
        _controlCharacters = 0;
        AppendControl(introducer);
    }

    private bool AppendControl(ReadOnlySpan<byte> encoded) {
        if (_controlLength + encoded.Length > _controlBytes.Length)
            return false;
        encoded.CopyTo(_controlBytes.AsSpan(_controlLength));
        _controlLength += encoded.Length;
        return true;
    }

    private void EndControl() {
        _parserState = T140ParserState.Normal;
        _controlLength = 0;
        _controlCharacters = 0;
        _applicationFunctionSeen = false;
        _ucsSubsetDesignation = false;
    }

    private void ShiftPendingUtf8(int count) {
        if (count <= 0 || count > _pendingUtf8Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        int remaining = _pendingUtf8Length - count;
        if (remaining > 0) {
            _pendingUtf8.AsSpan(count, remaining)
                .CopyTo(_pendingUtf8);
        }
        _pendingUtf8Length = remaining;
    }

    private static void EmitMissingText(
        T140ByteSink? byteSink,
        T140ElementSink? elementSink) {
        Span<byte> replacement = stackalloc byte[3] { 0xEF, 0xBF, 0xBD };
        Emit(T140ElementType.MissingText, replacement, byteSink, elementSink);
    }

    private static void Emit(
        T140ElementType elementType,
        ReadOnlySpan<byte> data,
        T140ByteSink? byteSink,
        T140ElementSink? elementSink) {
        byteSink?.Invoke(data);
        elementSink?.Invoke(elementType, data);
    }

    private enum T140ParserState {
        Normal,
        Escape,
        SelectGraphicRendition,
        ApplicationProtocol
    }
}
