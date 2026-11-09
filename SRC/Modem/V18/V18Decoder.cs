/*
 * TKFaxEngine - managed C# port
 *
 * V18Decoder.cs
 *
 * V.18 data decoder.
 */

#nullable enable

using System.Text;
using TKFaxEngine.Audio;
using TKFaxEngine.Daten.T140;

namespace TKFaxEngine.Modem.V18;

public sealed partial class V18State {


    private SpanPutByteDelegate? _receiveByteConsumer;
    private int _receiveDataBits;
    private AsyncParity _receiveParity;
    private int _receiveFramePosition = -1;
    private int _receiveFrameValue;
    private int _receiveFrameOnes;
    private bool _receiveFrameParityValid;
    private bool _ignoreReceiveParity;
    private bool _legacyCarriageReturnPending;

    private void ConfigureReceiveBitDecoder(
        int dataBits,
        AsyncParity parity,
        SpanPutByteDelegate byteConsumer,
        bool ignoreParity) {
        if (dataBits is <= 0 or > 8)
            throw new ArgumentOutOfRangeException(nameof(dataBits));
        ArgumentNullException.ThrowIfNull(byteConsumer);

        _receiveDataBits = dataBits;
        _receiveParity = parity;
        _ignoreReceiveParity = ignoreParity && parity != AsyncParity.None;
        _receiveByteConsumer = byteConsumer;
        ResetReceiveBitDecoder();
    }


    private void ResetReceiveBitDecoder() {
        _receiveFramePosition = -1;
        _receiveFrameValue = 0;
        _receiveFrameOnes = 0;
        _receiveFrameParityValid = true;
    }


    private void ClearReceiveBitDecoder() {
        ResetReceiveBitDecoder();
        _receiveByteConsumer = null;
        _receiveDataBits = 0;
        _receiveParity = AsyncParity.None;
        _ignoreReceiveParity = false;
    }

    private void ResetT140ReceiveDataPath() {
        _t140ReceiveDecoder.Reset();
        _legacyCarriageReturnPending = false;
    }



    private void DecodeReceivedFskBit(int bitOrStatus) {
        SpanPutByteDelegate? consumer = _receiveByteConsumer;
        if (consumer is null)
            return;

        if (bitOrStatus < 0) {
            ResetReceiveBitDecoder();
            consumer(this, bitOrStatus);
            return;
        }

        int bit = bitOrStatus & 1;
        if (_receiveFramePosition < 0) {
            if (bit == 0) {
                _receiveFramePosition = 0;
                _receiveFrameValue = 0;
                _receiveFrameOnes = 0;
                _receiveFrameParityValid = true;
            }
            return;
        }

        if (_receiveFramePosition < _receiveDataBits) {
            if (bit != 0) {
                _receiveFrameValue |= 1 << _receiveFramePosition;
                _receiveFrameOnes++;
            }
            _receiveFramePosition++;
            return;
        }

        if (_receiveParity != AsyncParity.None &&
            _receiveFramePosition == _receiveDataBits) {
            int expected = _receiveParity switch {
                AsyncParity.Even => _receiveFrameOnes & 1,
                AsyncParity.Odd => (_receiveFrameOnes & 1) ^ 1,
                AsyncParity.Mark => 1,
                AsyncParity.Space => 0,
                _ => throw new InvalidOperationException(
                    $"Unsupported asynchronous parity mode: {_receiveParity}.")
            };
            _receiveFrameParityValid = _ignoreReceiveParity || bit == expected;
            _receiveFramePosition++;
            return;
        }

        bool framingValid = bit == 1;
        int value = _receiveFrameValue;
        bool deliver = framingValid && _receiveFrameParityValid;
        ResetReceiveBitDecoder();

        if (deliver)
            consumer(this, value);
    }

    private void TddPutAsyncByte(object? userData, int value) {
        _ = userData;

        if (value < 0) {
            Logging.Log(
                (int)SpanLogSeverity.Flow,
                "TDD signal status is %s (%d)\n",
                SignalStatusToString(value),
                value);

            switch ((SignalStatus)value) {
                case SignalStatus.CarrierUp:
                    MessageInProgressTimer = 0;
                    break;

                case SignalStatus.CarrierDown:
                    FlushLegacyCarriageReturn();
                    break;

                default:
                    Logging.Log(
                        (int)SpanLogSeverity.Warning,
                        "Unexpected special put byte value - %d!\n",
                        value);
                    break;
            }

            return;
        }

        if (ReceiveSuppressionTimer > 0) {
            Logging.Log(
                (int)SpanLogSeverity.Flow,
                "Rx suppressed byte 0x%02x (%d)\n",
                value,
                ReceiveSuppressionTimer);
            return;
        }

        byte decoded = DecodeBaudot((byte)value);
        if (decoded != 0xFF) {
            Logging.Log(
                (int)SpanLogSeverity.Flow,
                "Rx byte 0x%02x '%c'\n",
                decoded,
                decoded);
            DeliverLegacyByte(decoded);
        }
    }


    private void EdtPutAsyncByte(object? userData, int value) {
        _ = userData;
        if (value < 0) {
            if (value == (int)SignalStatus.CarrierDown)
                FlushLegacyCarriageReturn();
            return;
        }
        if (ReceiveSuppressionTimer > 0)
            return;
        DeliverLegacyByte(value == 0x15 ? (byte)T140Controls.BackSpace : (byte)value);
    }


    private void Bell103PutAsyncByte(object? userData, int value) => EdtPutAsyncByte(userData, value);


    private void VideotexPutAsyncByte(object? userData, int value) => EdtPutAsyncByte(userData, value);


    private void TextphonePutAsyncByte(object? userData, int value) {
        _ = userData;
        if (value == (int)SignalStatus.CarrierDown) {
            if (CurrentMode == V18Mode.V18Textphone)
                _t140ReceiveDecoder.CarrierDown(DeliverUtf8);
            else
                FlushLegacyCarriageReturn();
            return;
        }
        if (ReceiveSuppressionTimer > 0 || value < 0)
            return;

        if (CurrentMode == V18Mode.V18Textphone)
            _t140ReceiveDecoder.PutByte((byte)value, DeliverUtf8);
        else
            DeliverLegacyByte((byte)value);
    }


    private byte DecodeBaudot(byte code) {
        switch (code) {
            case BaudotFigureShift:
                _baudotReceiveShift = 1;
                return 0xFF;

            case BaudotLetterShift:
                _baudotReceiveShift = 0;
                return 0xFF;

            default:
                return BaudotDecodeTable[_baudotReceiveShift][code & 0x1F];
        }
    }


    private void DtmfReceived(string digits) {
        TransmitSuppressionTimer = MillisecondsToSamples(400);
        if (ReceiveSuppressionTimer > 0)
            return;

        foreach (char digit in digits) {
            _receivedDtmf.Append(digit);
            if (digit is < '0' or > '9')
                continue;

            if (TryDecodeDtmf(_receivedDtmf.ToString(), out byte character, out int consumed)) {
                DeliverLegacyByte(character);
                _receivedDtmf.Remove(0, consumed);
            } else {
                // Drop an invalid complete sequence so the decoder cannot stall.
                int firstTerminator = FindFirstDtmfTerminator(_receivedDtmf);
                if (firstTerminator >= 0)
                    _receivedDtmf.Remove(0, firstTerminator + 1);
            }
        }

        MessageInProgressTimer = SecondsToSamples(5);
    }


    private static bool TryDecodeDtmf(
        string sequence,
        out byte character,
        out int consumed) {
        for (int length = Math.Min(4, sequence.Length); length >= 1; length--) {
            string candidate = sequence[..length];
            if (DtmfToAscii.TryGetValue(candidate, out character)) {
                consumed = length;
                return true;
            }
        }

        character = 0;
        consumed = 0;
        return false;
    }


    private static int FindFirstDtmfTerminator(StringBuilder sequence) {
        for (int i = 0; i < sequence.Length; i++) {
            if (sequence[i] is >= '0' and <= '9')
                return i;
        }

        return -1;
    }


    private void DeliverLegacyByte(byte value) {
        if (value == T140Controls.CarriageReturn) {
            if (_legacyCarriageReturnPending)
                DeliverLineSeparator();
            _legacyCarriageReturnPending = true;
            return;
        }

        if (_legacyCarriageReturnPending) {
            _legacyCarriageReturnPending = false;
            DeliverLineSeparator();
            if (value == T140Controls.LineFeed)
                return;
        }

        if (value == T140Controls.LineFeed) {
            DeliverLineSeparator();
            return;
        }

        Rune rune = value switch {
            0xC6 => new Rune(0x00C6),
            0xD8 => new Rune(0x00D8),
            0xC5 => new Rune(0x00C5),
            0xE6 => new Rune(0x00E6),
            0xF8 => new Rune(0x00F8),
            0xE5 => new Rune(0x00E5),
            _ => new Rune(value)
        };
        DeliverRune(rune);
    }

    private void FlushLegacyCarriageReturn() {
        if (!_legacyCarriageReturnPending)
            return;
        _legacyCarriageReturnPending = false;
        DeliverLineSeparator();
    }

    private void DeliverLineSeparator() =>
        DeliverRune(new Rune(T140Controls.LineSeparator));

    private void DeliverRune(Rune rune) {
        Span<byte> utf8 = stackalloc byte[4];
        int length = rune.EncodeToUtf8(utf8);
        DeliverUtf8(utf8[..length]);
    }

    private void DeliverUtf8(ReadOnlySpan<byte> value) {
        _putMessage?.Invoke(_putMessageUserData, value);
    }
}

