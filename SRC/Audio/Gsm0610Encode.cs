/*
 * TKFaxEngine - managed GSM 06.10 full-rate speech codec
 * Ported from gsm0610_encode.c, gsm0610.h and gsm0610_local.h.
 * Original implementation by Steve Underwood.
 * LGPL-2.1, matching the source files.
 */

#nullable enable

using System.Numerics;

namespace TKFaxEngine.Audio;

public enum Gsm0610Packing {
    None = 0,
    Wav49 = 1,
    Voip = 2
}

public sealed class Gsm0610Frame {
    public short[] LARc { get; } = new short[8];
    public short[] Nc { get; } = new short[4];
    public short[] Bc { get; } = new short[4];
    public short[] Mc { get; } = new short[4];
    public short[] Xmaxc { get; } = new short[4];
    public short[][] XMc { get; } =
    [
        new short[13], new short[13], new short[13], new short[13]
    ];
}

public sealed class Gsm0610State : IDisposable {
    internal bool Disposed;

    public Gsm0610Packing Packing { get; internal set; }
    internal short[] Dp0 { get; } = new short[280];
    internal short Z1;
    internal int LZ2;
    internal short Mp;
    internal short[] U { get; } = new short[8];
    internal short[][] LarPp { get; } = [new short[8], new short[8]];
    internal short J;
    internal short Nrp = 40;
    internal short[] V { get; } = new short[9];
    internal short Msr;
    internal short[] E { get; } = new short[50];

    public void Dispose() {
        Disposed = true;
        GC.SuppressFinalize(this);
    }

    internal void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Disposed, this);
}

public static partial class Gsm0610Api {
    public const int GSM0610_PACKING_NONE = 0;
    public const int GSM0610_PACKING_WAV49 = 1;
    public const int GSM0610_PACKING_VOIP = 2;
    public const int GSM0610_FRAME_LEN = 160;
    public const int GSM0610_MAGIC = 0xD;

    public static Gsm0610State gsm0610_init(Gsm0610State? state, int packing) {
        state ??= new Gsm0610State();
        state.ThrowIfDisposed();
        Array.Clear(state.Dp0);
        Array.Clear(state.U);
        Array.Clear(state.LarPp[0]);
        Array.Clear(state.LarPp[1]);
        Array.Clear(state.V);
        Array.Clear(state.E);
        state.Z1 = 0;
        state.LZ2 = 0;
        state.Mp = 0;
        state.J = 0;
        state.Nrp = 40;
        state.Msr = 0;
        state.Packing = (Gsm0610Packing)packing;
        return state;
    }

    public static int gsm0610_release(Gsm0610State state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        return 0;
    }

    public static int gsm0610_free(Gsm0610State? state) {
        state?.Dispose();
        return 0;
    }

    public static int gsm0610_set_packing(Gsm0610State state, int packing) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        state.Packing = (Gsm0610Packing)packing;
        return 0;
    }

    public static int gsm0610_pack_none(byte[] output, Gsm0610Frame frame) =>
        Gsm0610Codec.PackNone(output, 0, frame);

    public static int gsm0610_pack_wav49(byte[] output, Gsm0610Frame[] frames) =>
        Gsm0610Codec.PackWav49(output, 0, frames);

    public static int gsm0610_pack_voip(byte[] output, Gsm0610Frame frame) =>
        Gsm0610Codec.PackVoip(output, 0, frame);

    public static int gsm0610_encode(
        Gsm0610State state,
        byte[] code,
        short[] amplitude,
        int length) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(amplitude);
        state.ThrowIfDisposed();
        if ((uint)length > (uint)amplitude.Length)
            throw new ArgumentOutOfRangeException(nameof(length));
        return Gsm0610Codec.Encode(state, code, amplitude, length);
    }
}

internal static partial class Gsm0610Codec {
    internal const int FrameLength = 160;
    internal const int Magic = 0xD;

    internal static short Sat16(long value) =>
        value > short.MaxValue ? short.MaxValue :
        value < short.MinValue ? short.MinValue : (short)value;

    internal static int Sat32(long value) =>
        value > int.MaxValue ? int.MaxValue :
        value < int.MinValue ? int.MinValue : (int)value;

    internal static short Add(short a, short b) => Sat16((int)a + b);
    internal static short Sub(short a, short b) => Sat16((int)a - b);

    internal static short Mult(short a, short b) {
        if (a == short.MinValue && b == short.MinValue)
            return short.MaxValue;
        return (short)(((int)a * b) >> 15);
    }

    internal static int LMult(short a, short b) {
        if (a == short.MinValue && b == short.MinValue)
            return int.MaxValue;
        return Sat32(((long)a * b) << 1);
    }

    internal static short MultR(short a, short b) {
        if (a == short.MinValue && b == short.MinValue)
            return short.MaxValue;
        return (short)((((int)a * b) + 16384) >> 15);
    }

    internal static short Abs(short value) =>
        value == short.MinValue ? short.MaxValue : (short)Math.Abs(value);

    internal static short Asr(short value, int count) {
        if (count >= 16) return (short)(value < 0 ? -1 : 0);
        if (count <= -16) return 0;
        return count < 0 ? (short)(value << -count) : (short)(value >> count);
    }

    internal static short Asl(short value, int count) {
        if (count >= 16) return 0;
        if (count <= -16) return (short)(value < 0 ? -1 : 0);
        return count < 0 ? Asr(value, -count) : (short)(value << count);
    }

    internal static int Add32(int a, int b) => Sat32((long)a + b);

    internal static int TopBit(int value) {
        if (value <= 0) return -1;
        return 31 - BitOperations.LeadingZeroCount((uint)value);
    }

    internal static short Norm(int value) {
        if (value == 0) return 0;
        if (value < 0) {
            if (value <= -1073741824) return 0;
            value = ~value;
        }
        return (short)(30 - TopBit(value));
    }

    internal static void EncodeFrame(
        Gsm0610State state,
        Gsm0610Frame frame,
        short[] amplitude,
        int amplitudeOffset) {
        short[] so = new short[FrameLength];
        Preprocess(state, amplitude, amplitudeOffset, so);
        LpcAnalysis(state, so, frame.LARc);
        ShortTermAnalysisFilter(state, frame.LARc, so);

        int dpOffset = 120;
        for (int subframe = 0; subframe < 4; subframe++) {
            int signalOffset = subframe * 40;
            LongTermPredictor(
                state,
                so,
                signalOffset,
                state.Dp0,
                dpOffset,
                state.E,
                5,
                state.Dp0,
                dpOffset,
                out frame.Nc[subframe],
                out frame.Bc[subframe]);

            RpeEncoding(
                state,
                state.E,
                5,
                out frame.Xmaxc[subframe],
                out frame.Mc[subframe],
                frame.XMc[subframe]);

            for (int i = 0; i < 40; i++)
                state.Dp0[dpOffset + i] = Add(state.E[5 + i], state.Dp0[dpOffset + i]);

            dpOffset += 40;
        }

        Array.Copy(state.Dp0, FrameLength, state.Dp0, 0, 120);
    }

    internal static int Encode(
        Gsm0610State state,
        byte[] code,
        short[] amplitude,
        int length) {
        if (length % FrameLength != 0)
            throw new ArgumentException("PCM length must be a multiple of 160 samples.", nameof(length));

        int outputOffset = 0;
        int inputOffset = 0;
        while (inputOffset < length) {
            if (state.Packing == Gsm0610Packing.Wav49) {
                if (inputOffset + 2 * FrameLength > length)
                    throw new ArgumentException("WAV49 packing requires pairs of GSM frames.", nameof(length));
                Gsm0610Frame[] frames = [new Gsm0610Frame(), new Gsm0610Frame()];
                EncodeFrame(state, frames[0], amplitude, inputOffset);
                EncodeFrame(state, frames[1], amplitude, inputOffset + FrameLength);
                outputOffset += PackWav49(code, outputOffset, frames);
                inputOffset += 2 * FrameLength;
            } else {
                Gsm0610Frame frame = new();
                EncodeFrame(state, frame, amplitude, inputOffset);
                outputOffset += state.Packing == Gsm0610Packing.Voip
                    ? PackVoip(code, outputOffset, frame)
                    : PackNone(code, outputOffset, frame);
                inputOffset += FrameLength;
            }
        }
        return outputOffset;
    }

    internal static int PackNone(byte[] output, int offset, Gsm0610Frame frame) {
        if (output.Length - offset < 76) throw new ArgumentException("Output buffer too small.");
        int p = offset;
        for (int i = 0; i < 8; i++) output[p++] = (byte)frame.LARc[i];
        for (int sf = 0; sf < 4; sf++) {
            output[p++] = (byte)frame.Nc[sf];
            output[p++] = (byte)frame.Bc[sf];
            output[p++] = (byte)frame.Mc[sf];
            output[p++] = (byte)frame.Xmaxc[sf];
            for (int i = 0; i < 13; i++) output[p++] = (byte)frame.XMc[sf][i];
        }
        return 76;
    }

    internal static int PackWav49(byte[] output, int offset, Gsm0610Frame[] frames) {
        if (frames.Length < 2) throw new ArgumentException("Two frames are required.", nameof(frames));
        if (output.Length - offset < 65) throw new ArgumentException("Output buffer too small.");
        Array.Clear(output, offset, 65);
        BitWriter writer = new(output, offset, false);
        WriteFrameFields(writer, frames[0]);
        WriteFrameFields(writer, frames[1]);
        return 65;
    }

    internal static int PackVoip(byte[] output, int offset, Gsm0610Frame frame) {
        if (output.Length - offset < 33) throw new ArgumentException("Output buffer too small.");
        Array.Clear(output, offset, 33);
        BitWriter writer = new(output, offset, true);
        writer.Write(Magic, 4);
        WriteFrameFields(writer, frame);
        return 33;
    }

    private static void WriteFrameFields(BitWriter writer, Gsm0610Frame frame) {
        int[] larBits = [6, 6, 5, 5, 4, 4, 3, 3];
        for (int i = 0; i < 8; i++) writer.Write(frame.LARc[i], larBits[i]);
        for (int sf = 0; sf < 4; sf++) {
            writer.Write(frame.Nc[sf], 7);
            writer.Write(frame.Bc[sf], 2);
            writer.Write(frame.Mc[sf], 2);
            writer.Write(frame.Xmaxc[sf], 6);
            for (int i = 0; i < 13; i++) writer.Write(frame.XMc[sf][i], 3);
        }
    }

    internal sealed class BitWriter {
        private readonly byte[] _buffer;
        private readonly int _offset;
        private readonly bool _msbFirst;
        private int _bitPosition;

        internal BitWriter(byte[] buffer, int offset, bool msbFirst) {
            _buffer = buffer;
            _offset = offset;
            _msbFirst = msbFirst;
        }

        internal void Write(int value, int bits) {
            if (_msbFirst) {
                for (int i = bits - 1; i >= 0; i--) WriteBit((value >> i) & 1);
            } else {
                for (int i = 0; i < bits; i++) WriteBit((value >> i) & 1);
            }
        }

        private void WriteBit(int bit) {
            int byteIndex = _offset + (_bitPosition >> 3);
            int bitIndex = _msbFirst ? 7 - (_bitPosition & 7) : (_bitPosition & 7);
            if (bit != 0) _buffer[byteIndex] |= (byte)(1 << bitIndex);
            _bitPosition++;
        }
    }
}
