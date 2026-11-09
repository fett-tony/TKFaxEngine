/*
 * TKFaxEngine - managed C# port
 *
 * V18Encoder.cs
 *
 * V.18 data encoder.
 */

#nullable enable

using System.Text;
using TKFaxEngine.Daten.T140;

namespace TKFaxEngine.Modem.V18;

public sealed partial class V18State {
    /// <summary>Queues Unicode text without splitting a UTF-8 scalar.</summary>
    public int Put(string message, int length = -1) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(message);

        int nul = message.IndexOf('\0');
        ReadOnlySpan<char> effective = nul >= 0 ? message.AsSpan(0, nul) : message.AsSpan();
        int maximumUtf8Bytes = length < 0 ? int.MaxValue : length;
        if (maximumUtf8Bytes < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        int queuedBytes = 0;
        if (!_t140TransmitEncoder.FlushUtf8(
                rune => QueueRuneWithCount(rune, ref queuedBytes))) {
            ActivateCarrierForQueuedData(queuedBytes);
            return queuedBytes;
        }

        int examinedUtf8Bytes = 0;
        foreach (Rune rune in effective.EnumerateRunes()) {
            int utf8Length = rune.Utf8SequenceLength;
            if (examinedUtf8Bytes + utf8Length > maximumUtf8Bytes)
                break;

            int written = QueueEncodedRune(rune);
            if (written < 0)
                break;

            examinedUtf8Bytes += utf8Length;
            queuedBytes += written;
        }

        ActivateCarrierForQueuedData(queuedBytes);
        return queuedBytes;
    }

    /// <summary>Validates and streams UTF-8 input directly into the data encoder.</summary>
    public int Put(ReadOnlySpan<byte> message) {
        ThrowIfDisposed();
        if (message.IsEmpty)
            return 0;

        int queuedBytes = 0;
        int acceptedBytes = _t140TransmitEncoder.PutUtf8(
            message,
            rune => QueueRuneWithCount(rune, ref queuedBytes));

        ActivateCarrierForQueuedData(queuedBytes);
        return acceptedBytes;
    }

    private bool QueueRuneWithCount(Rune rune, ref int queuedBytes) {
        int written = QueueEncodedRune(rune);
        if (written < 0)
            return false;
        queuedBytes += written;
        return true;
    }

    private int QueueEncodedRune(Rune rune) {
        if (CurrentMode == V18Mode.V18Textphone) {
            int required = _t140TransmitEncoder.GetSessionByteCount(rune);
            if (_queue.FreeSpace() < required)
                return -1;

            Span<byte> t140 = stackalloc byte[7];
            if (!_t140TransmitEncoder.TryEncodeSessionRune(
                    rune,
                    t140,
                    out int t140Length)) {
                throw new InvalidOperationException(
                    "The fixed T.140 session buffer is too small.");
            }

            int written = _queue.Write(t140[..t140Length], t140Length);
            if (written != t140Length) {
                throw new InvalidOperationException(
                    "The V.18 transmit queue changed unexpectedly.");
            }
            return written;
        }

        byte legacy = EncodeLegacyRune(rune);
        return _queue.WriteByte(legacy) < 0 ? -1 : 1;
    }

    private static byte EncodeLegacyRune(Rune rune) {
        return rune.Value switch {
            <= 0x7F => (byte)rune.Value,
            0x00C6 => 0xC6,
            0x00D8 => 0xD8,
            0x00C5 => 0xC5,
            0x00E6 => 0xE6,
            0x00F8 => 0xF8,
            0x00E5 => 0xE5,
            T140Controls.LineSeparator => (byte)'\n',
            T140Controls.ParagraphSeparator => (byte)'\n',
            _ => (byte)'?'
        };
    }

    private void ActivateCarrierForQueuedData(int queuedBytes) {
        if (queuedBytes <= 0 || _transmitSignalOn != 0)
            return;
        Logging.Log((int)SpanLogSeverity.Flow, "Turning on the carrier\n");
        _transmitSignalOn = 1;
    }

    private int TddGetAsyncByte(object? userData) {
        _ = userData;

        if (_nextByte != 0xFF) {
            ReceiveSuppressionTimer = MillisecondsToSamples(TddCharacterSuppressionMilliseconds);
            int next = _nextByte;
            _nextByte = 0xFF;
            return next;
        }

        while (true) {
            int character = _queue.ReadByte();
            if (character < 0) {
                if (_transmitDraining) {
                    _transmitDraining = false;
                    return (int)SignalStatus.EndOfData;
                }

                Logging.Log((int)SpanLogSeverity.Flow, "Tx shutdown with delay\n");
                _asyncTransmitter!.SetPresendBits(14);
                _transmitDraining = true;
                ReceiveSuppressionTimer = MillisecondsToSamples(TddDrainSuppressionMilliseconds);
                return (int)SignalStatus.LinkIdle;
            }

            ushort encoded = EncodeBaudot((byte)character);
            if (encoded == 0)
                continue;

            ReceiveSuppressionTimer = MillisecondsToSamples(TddCharacterSuppressionMilliseconds);

            if (_transmitSignalOn == 1) {
                FskPreset preset = (CurrentMode & ~V18Mode.RepetitiveShiftsOption) switch {
                    V18Mode.Weitbrecht5Bit4545 => FskPreset.Weitbrecht4545,
                    V18Mode.Weitbrecht5Bit50 => FskPreset.Weitbrecht50,
                    _ => FskPreset.Weitbrecht476
                };
                _asyncTransmitter!.SetPresendBits(
                    CalculatePresendBits(
                        preset,
                        BaudotCarrierPreambleMilliseconds));
                _transmitSignalOn = 2;
            }

            if ((encoded & 0x03E0) != 0) {
                _nextByte = (byte)(encoded & 0x1F);
                return (encoded >> 5) & 0x1F;
            }

            _nextByte = 0xFF;
            return encoded & 0x1F;
        }
    }


    private int EdtGetAsyncByte(object? userData) {
        _ = userData;
        int character = _queue.ReadByte();
        if (character >= 0) {
            ReceiveSuppressionTimer = MillisecondsToSamples(300);
            return character;
        }

        if (_transmitSignalOn != 0) {
            Logging.Log((int)SpanLogSeverity.Flow, "Turning off the carrier\n");
            _transmitSignalOn = 0;
        }

        return (int)SignalStatus.LinkIdle;
    }


    private int Bell103GetAsyncByte(object? userData) {
        _ = userData;
        int character = _queue.ReadByte();
        return character >= 0 ? character : (int)SignalStatus.LinkIdle;
    }


    private int VideotexGetAsyncByte(object? userData) => Bell103GetAsyncByte(userData);


    private int TextphoneGetAsyncByte(object? userData) => Bell103GetAsyncByte(userData);


    private ushort EncodeBaudot(byte character) {
        byte code = BaudotEncodeTable[character & 0x7F];
        if (code == 0xFF)
            code = BaudotEncodeTable[(byte)'?'];

        if ((code & 0x40) != 0)
            return (ushort)(0x8000 | (code & 0x1F));

        bool figures = (code & 0x80) != 0;
        int requiredShift = figures ? 1 : 0;
        int shiftCode = figures ? BaudotFigureShift : BaudotLetterShift;
        bool periodicShift = RepeatShifts && _baudotCharactersSinceShift >= 72;
        bool modeChange = _baudotTransmitShift != requiredShift;
        bool initialShift = _baudotTransmitShift == 2;

        _baudotCharactersSinceShift++;
        if (!modeChange && !initialShift && !periodicShift)
            return (ushort)(code & 0x1F);

        _baudotTransmitShift = requiredShift;
        _baudotCharactersSinceShift = 0;
        return (ushort)(0x8000 | (shiftCode << 5) | (code & 0x1F));
    }

}
