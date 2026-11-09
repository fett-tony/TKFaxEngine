/* Managed port of gsm0610_decode.c. LGPL-2.1. */

#nullable enable

namespace TKFaxEngine.Audio;

public static partial class Gsm0610Api {
    public static int gsm0610_unpack_none(Gsm0610Frame frame, byte[] code) =>
        Gsm0610Codec.UnpackNone(frame, code, 0);

    public static int gsm0610_unpack_wav49(Gsm0610Frame[] frames, byte[] code) =>
        Gsm0610Codec.UnpackWav49(frames, code, 0);

    public static int gsm0610_unpack_voip(Gsm0610Frame frame, byte[] code) =>
        Gsm0610Codec.UnpackVoip(frame, code, 0);

    public static int gsm0610_decode(
        Gsm0610State state,
        short[] amplitude,
        byte[] code,
        int length) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(amplitude);
        ArgumentNullException.ThrowIfNull(code);
        state.ThrowIfDisposed();
        if ((uint)length > (uint)code.Length)
            throw new ArgumentOutOfRangeException(nameof(length));
        return Gsm0610Codec.Decode(state, amplitude, code, length);
    }
}

internal static partial class Gsm0610Codec {
    internal static void DecodeFrame(
        Gsm0610State state,
        short[] amplitude,
        int amplitudeOffset,
        Gsm0610Frame frame) {
        short[] erp = new short[40];
        short[] wt = new short[FrameLength];
        int drpOffset = 120;

        for (int subframe = 0; subframe < 4; subframe++) {
            RpeDecoding(
                state,
                frame.Xmaxc[subframe],
                frame.Mc[subframe],
                frame.XMc[subframe],
                erp);
            LongTermSynthesisFiltering(
                state,
                frame.Nc[subframe],
                frame.Bc[subframe],
                erp,
                state.Dp0,
                drpOffset);
            Array.Copy(state.Dp0, drpOffset, wt, subframe * 40, 40);
        }

        ShortTermSynthesisFilter(state, frame.LARc, wt, amplitude, amplitudeOffset);
        Postprocessing(state, amplitude, amplitudeOffset);
    }

    private static void Postprocessing(Gsm0610State state, short[] amplitude, int offset) {
        short msr = state.Msr;
        for (int k = 0; k < FrameLength; k++) {
            short tmp = MultR(msr, 28180);
            msr = Add(amplitude[offset + k], tmp);
            amplitude[offset + k] = (short)(Add(msr, msr) & 0xFFF8);
        }
        state.Msr = msr;
    }

    internal static int Decode(
        Gsm0610State state,
        short[] amplitude,
        byte[] code,
        int length) {
        int inputOffset = 0;
        int sampleOffset = 0;

        while (inputOffset < length) {
            switch (state.Packing) {
                case Gsm0610Packing.Wav49: {
                        if (length - inputOffset < 65) return sampleOffset;
                        if (amplitude.Length - sampleOffset < 320)
                            throw new ArgumentException("PCM output buffer too small.");
                        Gsm0610Frame[] frames = [new Gsm0610Frame(), new Gsm0610Frame()];
                        inputOffset += UnpackWav49(frames, code, inputOffset);
                        DecodeFrame(state, amplitude, sampleOffset, frames[0]);
                        sampleOffset += FrameLength;
                        DecodeFrame(state, amplitude, sampleOffset, frames[1]);
                        sampleOffset += FrameLength;
                        break;
                    }
                case Gsm0610Packing.Voip: {
                        if (length - inputOffset < 33) return sampleOffset;
                        if (amplitude.Length - sampleOffset < FrameLength)
                            throw new ArgumentException("PCM output buffer too small.");
                        Gsm0610Frame frame = new();
                        inputOffset += UnpackVoip(frame, code, inputOffset);
                        DecodeFrame(state, amplitude, sampleOffset, frame);
                        sampleOffset += FrameLength;
                        break;
                    }
                default: {
                        if (length - inputOffset < 76) return sampleOffset;
                        if (amplitude.Length - sampleOffset < FrameLength)
                            throw new ArgumentException("PCM output buffer too small.");
                        Gsm0610Frame frame = new();
                        inputOffset += UnpackNone(frame, code, inputOffset);
                        DecodeFrame(state, amplitude, sampleOffset, frame);
                        sampleOffset += FrameLength;
                        break;
                    }
            }
        }

        return sampleOffset;
    }

    internal static int UnpackNone(Gsm0610Frame frame, byte[] code, int offset) {
        if (code.Length - offset < 76) throw new ArgumentException("Input buffer too small.");
        int p = offset;
        for (int i = 0; i < 8; i++) frame.LARc[i] = code[p++];
        for (int sf = 0; sf < 4; sf++) {
            frame.Nc[sf] = code[p++];
            frame.Bc[sf] = code[p++];
            frame.Mc[sf] = code[p++];
            frame.Xmaxc[sf] = code[p++];
            for (int i = 0; i < 13; i++) frame.XMc[sf][i] = code[p++];
        }
        return 76;
    }

    internal static int UnpackWav49(Gsm0610Frame[] frames, byte[] code, int offset) {
        if (frames.Length < 2) throw new ArgumentException("Two frames are required.", nameof(frames));
        if (code.Length - offset < 65) throw new ArgumentException("Input buffer too small.");
        BitReader reader = new(code, offset, false);
        ReadFrameFields(reader, frames[0]);
        ReadFrameFields(reader, frames[1]);
        return 65;
    }

    internal static int UnpackVoip(Gsm0610Frame frame, byte[] code, int offset) {
        if (code.Length - offset < 33) throw new ArgumentException("Input buffer too small.");
        BitReader reader = new(code, offset, true);
        int magic = reader.Read(4);
        if (magic != Magic) throw new ArgumentException("Invalid GSM 06.10 VoIP magic nibble.");
        ReadFrameFields(reader, frame);
        return 33;
    }

    private static void ReadFrameFields(BitReader reader, Gsm0610Frame frame) {
        int[] larBits = [6, 6, 5, 5, 4, 4, 3, 3];
        for (int i = 0; i < 8; i++) frame.LARc[i] = (short)reader.Read(larBits[i]);
        for (int sf = 0; sf < 4; sf++) {
            frame.Nc[sf] = (short)reader.Read(7);
            frame.Bc[sf] = (short)reader.Read(2);
            frame.Mc[sf] = (short)reader.Read(2);
            frame.Xmaxc[sf] = (short)reader.Read(6);
            for (int i = 0; i < 13; i++) frame.XMc[sf][i] = (short)reader.Read(3);
        }
    }

    internal sealed class BitReader {
        private readonly byte[] _buffer;
        private readonly int _offset;
        private readonly bool _msbFirst;
        private int _bitPosition;

        internal BitReader(byte[] buffer, int offset, bool msbFirst) {
            _buffer = buffer;
            _offset = offset;
            _msbFirst = msbFirst;
        }

        internal int Read(int bits) {
            int value = 0;
            if (_msbFirst) {
                for (int i = 0; i < bits; i++) value = (value << 1) | ReadBit();
            } else {
                for (int i = 0; i < bits; i++) value |= ReadBit() << i;
            }
            return value;
        }

        private int ReadBit() {
            int byteIndex = _offset + (_bitPosition >> 3);
            int bitIndex = _msbFirst ? 7 - (_bitPosition & 7) : (_bitPosition & 7);
            _bitPosition++;
            return (_buffer[byteIndex] >> bitIndex) & 1;
        }
    }
}
