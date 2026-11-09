/*
 * TKFaxEngine - managed C# port
 *
 * T140Encoder.cs
 *
 * ITU-T T.140 streaming encoder.
 */

#nullable enable

using System.Buffers;
using System.Globalization;
using System.Text;

namespace TKFaxEngine.Daten.T140;

/// <summary>
/// Stateful T.140 session encoder plus UTF-8 and control-element helpers.
/// The retained state is limited to the mandatory initial U+FEFF signature
/// and at most one incomplete four-octet input scalar.
/// </summary>
public sealed class T140Encoder {
    private readonly byte[] _pendingInputUtf8 = new byte[4];
    private int _pendingInputUtf8Length;
    private bool _inputAtSessionStart = true;
    private bool _sessionSignatureSent;

    /// <summary>Gets whether the session code signature has been emitted.</summary>
    public bool SessionSignatureSent => _sessionSignatureSent;

    /// <summary>Starts a new T.140 presentation session and input stream.</summary>
    public void Reset() {
        _pendingInputUtf8Length = 0;
        _inputAtSessionStart = true;
        _sessionSignatureSent = false;
    }

    /// <summary>
    /// Gets the number of bytes required to emit a scalar in the current
    /// session, including the initial UTF-8 code signature when necessary.
    /// </summary>
    public int GetSessionByteCount(Rune rune) {
        return rune.Utf8SequenceLength + (_sessionSignatureSent ? 0 : 3);
    }

    /// <summary>
    /// Encodes one scalar and, for the first scalar in a session, prefixes
    /// U+FEFF. The state changes only after the complete element fits.
    /// </summary>
    public bool TryEncodeSessionRune(
        Rune rune,
        Span<byte> destination,
        out int bytesWritten) {
        int required = GetSessionByteCount(rune);
        if (destination.Length < required) {
            bytesWritten = 0;
            return false;
        }

        int offset = 0;
        if (!_sessionSignatureSent) {
            destination[0] = 0xEF;
            destination[1] = 0xBB;
            destination[2] = 0xBF;
            offset = 3;
        }

        offset += rune.EncodeToUtf8(destination[offset..]);
        _sessionSignatureSent = true;
        bytesWritten = offset;
        return true;
    }

    /// <summary>Writes the mandatory U+FEFF session signature once.</summary>
    public void WriteSessionSignature(T140ByteSink sink) {
        ArgumentNullException.ThrowIfNull(sink);
        if (_sessionSignatureSent)
            return;

        Span<byte> signature = stackalloc byte[3] { 0xEF, 0xBB, 0xBF };
        sink(signature);
        _sessionSignatureSent = true;
    }

    /// <summary>
    /// Writes text as complete UTF-8 scalar values and inserts the session
    /// signature before the first scalar.
    /// </summary>
    public void WriteText(string text, T140ByteSink sink) {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(sink);

        Span<byte> encoded = stackalloc byte[7];
        foreach (Rune rune in text.EnumerateRunes()) {
            if (!TryEncodeSessionRune(rune, encoded, out int length))
                throw new InvalidOperationException("The fixed T.140 scalar buffer is too small.");
            sink(encoded[..length]);
        }
    }

    /// <summary>Writes complete UTF-8 scalar values without a session signature.</summary>
    public static void Encode(string text, T140ByteSink sink) {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(sink);

        foreach (Rune rune in text.EnumerateRunes())
            WriteRune(rune, sink);
    }

    /// <summary>Writes BEL to request an in-session alert.</summary>
    public static void WriteAlert(T140ByteSink sink) =>
        WriteRune(new Rune(T140Controls.Bell), sink);

    /// <summary>Writes BS to erase the last displayed character.</summary>
    public static void WriteBackSpace(T140ByteSink sink) =>
        WriteRune(new Rune(T140Controls.BackSpace), sink);

    /// <summary>Writes the preferred T.140 LINE SEPARATOR new-line element.</summary>
    public static void WriteNewLine(T140ByteSink sink) =>
        WriteRune(new Rune(T140Controls.LineSeparator), sink);

    /// <summary>Writes the accepted, but non-preferred, CR LF new-line form.</summary>
    public static void WriteCarriageReturnLineFeed(T140ByteSink sink) {
        ArgumentNullException.ThrowIfNull(sink);
        Span<byte> sequence = stackalloc byte[2] {
            (byte)T140Controls.CarriageReturn,
            (byte)T140Controls.LineFeed
        };
        sink(sequence);
    }

    /// <summary>Writes INT as ESC followed by U+0061.</summary>
    public static void WriteInterrupt(T140ByteSink sink) {
        ArgumentNullException.ThrowIfNull(sink);
        Span<byte> sequence = stackalloc byte[2] {
            (byte)T140Controls.Escape,
            (byte)T140Controls.InterruptFinal
        };
        sink(sequence);
    }

    /// <summary>Writes the U+FFFD missing-text marker.</summary>
    public static void WriteMissingText(T140ByteSink sink) =>
        WriteRune(new Rune(T140Controls.ReplacementCharacter), sink);

    /// <summary>
    /// Writes the ISO/IEC 10646 IUCS sequence CSI Ps... SPACE m. Each
    /// parameter is a registered UCS subset collection number.
    /// </summary>
    public static void WriteUcsSubsetDesignation(
        ReadOnlySpan<int> subsetCollectionNumbers,
        T140ByteSink sink) {
        ArgumentNullException.ThrowIfNull(sink);
        if (subsetCollectionNumbers.IsEmpty)
            throw new ArgumentException(
                "At least one UCS subset collection number is required.",
                nameof(subsetCollectionNumbers));

        ValidateDecimalParameters(subsetCollectionNumbers);
        WriteRune(new Rune(T140Controls.ControlSequenceIntroducer), sink);
        WriteDecimalParameters(subsetCollectionNumbers, sink);
        WriteRune(new Rune(' '), sink);
        WriteRune(new Rune(T140Controls.SelectGraphicRenditionFinal), sink);
    }

    /// <summary>Writes CSI Ps m using decimal ISO/IEC 6429 SGR parameters.</summary>
    public static void WriteSelectGraphicRendition(
        ReadOnlySpan<int> parameters,
        T140ByteSink sink) {
        ArgumentNullException.ThrowIfNull(sink);
        ValidateDecimalParameters(parameters);
        WriteRune(new Rune(T140Controls.ControlSequenceIntroducer), sink);
        if (!parameters.IsEmpty)
            WriteDecimalParameters(parameters, sink);
        WriteRune(new Rune(T140Controls.SelectGraphicRenditionFinal), sink);
    }

    /// <summary>
    /// Writes SOS, one function code, up to 255 parameter characters and ST.
    /// </summary>
    public static void WriteApplicationProtocolFunction(
        Rune functionCode,
        string parameterString,
        T140ByteSink sink) {
        ArgumentNullException.ThrowIfNull(parameterString);
        ArgumentNullException.ThrowIfNull(sink);
        if (functionCode.Value == T140Controls.StringTerminator) {
            throw new ArgumentOutOfRangeException(
                nameof(functionCode),
                "ST cannot be used as an application function code.");
        }

        int parameterCharacters = 0;
        foreach (Rune rune in parameterString.EnumerateRunes()) {
            if (rune.Value == T140Controls.StringTerminator)
                throw new ArgumentException(
                    "The T.140 application parameter string may not contain ST.",
                    nameof(parameterString));
            parameterCharacters++;
            if (parameterCharacters > T140Controls.MaximumApplicationParameterCharacters)
                throw new ArgumentOutOfRangeException(nameof(parameterString));
        }

        WriteRune(new Rune(T140Controls.StartOfString), sink);
        WriteRune(functionCode, sink);
        Encode(parameterString, sink);
        WriteRune(new Rune(T140Controls.StringTerminator), sink);
    }

    /// <summary>
    /// Accepts an arbitrarily split UTF-8 input stream and forwards complete,
    /// validated Unicode scalars. At most the current four-octet scalar is
    /// retained between calls. The returned value is the number of source
    /// octets accepted into the stream state.
    /// </summary>
    public int PutUtf8(
        ReadOnlySpan<byte> utf8,
        T140RuneSink sink) {
        ArgumentNullException.ThrowIfNull(sink);

        if (!ProcessPendingInputUtf8(sink))
            return 0;

        int accepted = 0;
        while (accepted < utf8.Length) {
            if (_pendingInputUtf8Length == _pendingInputUtf8.Length) {
                if (!sink(Rune.ReplacementChar))
                    break;
                _inputAtSessionStart = false;
                ShiftPendingInputUtf8(1);
                if (!ProcessPendingInputUtf8(sink))
                    break;
            }

            _pendingInputUtf8[_pendingInputUtf8Length++] = utf8[accepted++];
            if (!ProcessPendingInputUtf8(sink))
                break;
        }

        return accepted;
    }

    /// <summary>
    /// Ends the current UTF-8 input block. An incomplete final scalar is
    /// forwarded as U+FFFD. False means the sink applied backpressure and the
    /// retained scalar must be flushed again later.
    /// </summary>
    public bool FlushUtf8(T140RuneSink sink) {
        ArgumentNullException.ThrowIfNull(sink);
        if (!ProcessPendingInputUtf8(sink))
            return false;
        if (_pendingInputUtf8Length == 0)
            return true;
        if (!sink(Rune.ReplacementChar))
            return false;
        _inputAtSessionStart = false;
        _pendingInputUtf8Length = 0;
        return true;
    }

    /// <summary>
    /// Decodes one complete UTF-8 block directly into validated Unicode
    /// scalars. Malformed or truncated input produces U+FFFD. The returned
    /// count is the number of source octets accepted before backpressure.
    /// </summary>
    public static int DecodeUtf8(
        ReadOnlySpan<byte> utf8,
        T140RuneSink sink) {
        ArgumentNullException.ThrowIfNull(sink);

        int accepted = 0;
        while (!utf8.IsEmpty) {
            OperationStatus status = Rune.DecodeFromUtf8(
                utf8,
                out Rune rune,
                out int consumed);

            int sourceLength;
            if (status == OperationStatus.Done) {
                sourceLength = consumed;
            } else {
                rune = Rune.ReplacementChar;
                sourceLength = status == OperationStatus.NeedMoreData
                    ? utf8.Length
                    : Math.Max(consumed, 1);
            }

            if (!sink(rune))
                break;

            accepted += sourceLength;
            utf8 = utf8[sourceLength..];
        }

        return accepted;
    }

    /// <summary>Validates UTF-8 and replaces malformed input with U+FFFD.</summary>
    public static string DecodeValidated(ReadOnlySpan<byte> utf8) {
        var builder = new StringBuilder();
        while (!utf8.IsEmpty) {
            OperationStatus status = Rune.DecodeFromUtf8(
                utf8,
                out Rune rune,
                out int consumed);

            if (status == OperationStatus.Done) {
                builder.Append(rune.ToString());
                utf8 = utf8[consumed..];
                continue;
            }

            builder.Append('\uFFFD');
            int discard = status == OperationStatus.NeedMoreData
                ? utf8.Length
                : Math.Max(consumed, 1);
            utf8 = utf8[discard..];
        }
        return builder.ToString();
    }

    /// <summary>Truncates UTF-8 to a complete scalar boundary.</summary>
    public static byte[] EncodeBounded(string text, int maximumBytes) {
        ArgumentNullException.ThrowIfNull(text);
        if (maximumBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));

        byte[] result = new byte[maximumBytes];
        int offset = 0;
        foreach (Rune rune in text.EnumerateRunes()) {
            int length = rune.Utf8SequenceLength;
            if (offset + length > maximumBytes)
                break;
            offset += rune.EncodeToUtf8(result.AsSpan(offset));
        }
        return result[..offset];
    }

    private bool ProcessPendingInputUtf8(T140RuneSink sink) {
        while (_pendingInputUtf8Length > 0) {
            OperationStatus status = Rune.DecodeFromUtf8(
                _pendingInputUtf8.AsSpan(0, _pendingInputUtf8Length),
                out Rune rune,
                out int consumed);

            if (status == OperationStatus.NeedMoreData)
                return true;

            if (status == OperationStatus.Done) {
                if (_inputAtSessionStart &&
                    rune.Value == T140Controls.SessionSignature) {
                    _inputAtSessionStart = false;
                    ShiftPendingInputUtf8(consumed);
                    continue;
                }

                if (!sink(rune))
                    return false;
                _inputAtSessionStart = false;
                ShiftPendingInputUtf8(consumed);
                continue;
            }

            if (!sink(Rune.ReplacementChar))
                return false;
            _inputAtSessionStart = false;
            ShiftPendingInputUtf8(Math.Max(consumed, 1));
        }

        return true;
    }

    private void ShiftPendingInputUtf8(int count) {
        if (count <= 0 || count > _pendingInputUtf8Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        int remaining = _pendingInputUtf8Length - count;
        if (remaining > 0) {
            _pendingInputUtf8.AsSpan(count, remaining)
                .CopyTo(_pendingInputUtf8);
        }
        _pendingInputUtf8Length = remaining;
    }

    private static void ValidateDecimalParameters(ReadOnlySpan<int> parameters) {
        int parameterCharacters = Math.Max(parameters.Length - 1, 0);
        for (int i = 0; i < parameters.Length; i++) {
            int parameter = parameters[i];
            if (parameter < 0)
                throw new ArgumentOutOfRangeException(nameof(parameters));

            parameterCharacters += CountDecimalDigits(parameter);
            if (parameterCharacters > T140Controls.MaximumSgrParameterCharacters) {
                throw new ArgumentOutOfRangeException(
                    nameof(parameters),
                    "The T.140 control parameter sequence exceeds 255 characters.");
            }
        }
    }

    private static void WriteDecimalParameters(
        ReadOnlySpan<int> parameters,
        T140ByteSink sink) {
        for (int i = 0; i < parameters.Length; i++) {
            if (i != 0)
                WriteRune(new Rune(';'), sink);

            string digits = parameters[i].ToString(CultureInfo.InvariantCulture);
            Encode(digits, sink);
        }
    }

    private static int CountDecimalDigits(int value) {
        int digits = 1;
        while (value >= 10) {
            value /= 10;
            digits++;
        }
        return digits;
    }

    private static void WriteRune(Rune rune, T140ByteSink sink) {
        ArgumentNullException.ThrowIfNull(sink);
        Span<byte> encoded = stackalloc byte[4];
        int length = rune.EncodeToUtf8(encoded);
        sink(encoded[..length]);
    }
}
