/*
 * TKFaxEngine - managed C# port
 *
 * OkiAdpcm.cs
 *
 * Combined port of oki_adpcm.h, private/oki_adpcm.h and oki_adpcm.c.
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2001, 2004 Steve Underwood.
 *
 * This port preserves the GNU Lesser General Public License version 2.1
 * licensing terms of the original source files.
 */

#nullable enable

namespace TKFaxEngine.Audio;

/// <summary>
/// Managed equivalent of <c>oki_adpcm_state_t</c>.
/// </summary>
public sealed class OkiAdpcmState : IDisposable {
    private bool _disposed;

    public OkiAdpcmState(int bitRate) {
        Initialize(bitRate);
    }

    public int BitRate { get; internal set; }

    public short Last { get; internal set; }

    public short StepIndex { get; internal set; }

    public byte OkiByte { get; internal set; }

    internal short[] History { get; } = new short[32];

    public int HistoryPointer { get; internal set; }

    public int Mark { get; internal set; }

    public int Phase { get; internal set; }

    public bool IsDisposed => _disposed;

    public void Initialize(int bitRate) {
        OkiAdpcm.ValidateBitRate(bitRate);

        BitRate = bitRate;
        Last = 0;
        StepIndex = 0;
        OkiByte = 0;
        Array.Clear(History, 0, History.Length);
        HistoryPointer = 0;
        Mark = 0;
        Phase = 0;
        _disposed = false;
    }

    public int Decode(
        Span<short> destination,
        ReadOnlySpan<byte> source,
        int sourceBytes) {
        ThrowIfDisposed();
        return OkiAdpcm.DecodeCore(this, destination, source, sourceBytes);
    }

    public int Decode(Span<short> destination, ReadOnlySpan<byte> source) {
        return Decode(destination, source, source.Length);
    }

    public int Encode(
        Span<byte> destination,
        ReadOnlySpan<short> source,
        int sampleCount) {
        ThrowIfDisposed();
        return OkiAdpcm.EncodeCore(this, destination, source, sampleCount);
    }

    public int Encode(Span<byte> destination, ReadOnlySpan<short> source) {
        return Encode(destination, source, source.Length);
    }

    /// <summary>
    /// Managed equivalent of <c>oki_adpcm_release()</c>. No external resources
    /// are owned, so release is intentionally a no-op.
    /// </summary>
    public int Release() {
        ThrowIfDisposed();
        return 0;
    }

    public int Free() {
        Dispose();
        return 0;
    }

    public void Dispose() {
        if (_disposed)
            return;

        BitRate = 0;
        Last = 0;
        StepIndex = 0;
        OkiByte = 0;
        Array.Clear(History, 0, History.Length);
        HistoryPointer = 0;
        Mark = 0;
        Phase = 0;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed() {
        if (_disposed)
            throw new ObjectDisposedException(nameof(OkiAdpcmState));
    }
}

/// <summary>
/// OKI/Dialogic ADPCM encoder and decoder supporting 24 kbit/s and 32 kbit/s.
/// </summary>
public static class OkiAdpcm {
    public const int BitRate24000 = 24000;
    public const int BitRate32000 = 32000;

    private static readonly short[] StepSize =
    {
        16, 17, 19, 21, 23, 25, 28, 31,
        34, 37, 41, 45, 50, 55, 60, 66,
        73, 80, 88, 97, 107, 118, 130, 143,
        157, 173, 190, 209, 230, 253, 279, 307,
        337, 371, 408, 449, 494, 544, 598, 658,
        724, 796, 876, 963, 1060, 1166, 1282, 1411,
        1552
    };

    private static readonly short[] StepAdjustment =
    {
        -1, -1, -1, -1, 2, 4, 6, 8
    };

    private static readonly float[] CutoffCoefficients =
    {
        -3.648392e-4f,
         5.062391e-4f,
         1.206247e-3f,
         1.804452e-3f,
         1.691750e-3f,
         4.083405e-4f,
        -1.931085e-3f,
        -4.452107e-3f,
        -5.794821e-3f,
        -4.778489e-3f,
        -1.161266e-3f,
         3.928504e-3f,
         8.259786e-3f,
         9.500425e-3f,
         6.512800e-3f,
         2.227856e-4f,
        -6.531275e-3f,
        -1.026843e-2f,
        -8.718062e-3f,
        -2.280487e-3f,
         5.817733e-3f,
         1.096777e-2f,
         9.634404e-3f,
         1.569301e-3f,
        -9.522632e-3f,
        -1.748273e-2f,
        -1.684408e-2f,
        -6.100054e-3f,
         1.071206e-2f,
         2.525209e-2f,
         2.871779e-2f,
         1.664411e-2f,
        -7.706268e-3f,
        -3.331083e-2f,
        -4.521249e-2f,
        -3.085962e-2f,
         1.373653e-2f,
         8.089593e-2f,
         1.529060e-1f,
         2.080487e-1f,
         2.286834e-1f,
         2.080487e-1f,
         1.529060e-1f,
         8.089593e-2f,
         1.373653e-2f,
        -3.085962e-2f,
        -4.521249e-2f,
        -3.331083e-2f,
        -7.706268e-3f,
         1.664411e-2f,
         2.871779e-2f,
         2.525209e-2f,
         1.071206e-2f,
        -6.100054e-3f,
        -1.684408e-2f,
        -1.748273e-2f,
        -9.522632e-3f,
         1.569301e-3f,
         9.634404e-3f,
         1.096777e-2f,
         5.817733e-3f,
        -2.280487e-3f,
        -8.718062e-3f,
        -1.026843e-2f,
        -6.531275e-3f,
         2.227856e-4f,
         6.512800e-3f,
         9.500425e-3f,
         8.259786e-3f,
         3.928504e-3f,
        -1.161266e-3f,
        -4.778489e-3f,
        -5.794821e-3f,
        -4.452107e-3f,
        -1.931085e-3f,
         4.083405e-4f,
         1.691750e-3f,
         1.804452e-3f,
         1.206247e-3f,
         5.062391e-4f,
        -3.648392e-4f
    };

    public static OkiAdpcmState? oki_adpcm_init(
        OkiAdpcmState? state,
        int bitRate) {
        if (bitRate != BitRate24000 && bitRate != BitRate32000)
            return null;

        if (state is null)
            return new OkiAdpcmState(bitRate);

        state.Initialize(bitRate);
        return state;
    }

    public static int oki_adpcm_release(OkiAdpcmState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    public static int oki_adpcm_free(OkiAdpcmState? state) {
        state?.Dispose();
        return 0;
    }

    public static int oki_adpcm_decode(
        OkiAdpcmState state,
        Span<short> amplitude,
        ReadOnlySpan<byte> okiData,
        int okiBytes) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Decode(amplitude, okiData, okiBytes);
    }

    public static int oki_adpcm_decode(
        OkiAdpcmState state,
        short[] amplitude,
        byte[] okiData,
        int okiBytes) {
        ArgumentNullException.ThrowIfNull(amplitude);
        ArgumentNullException.ThrowIfNull(okiData);
        return oki_adpcm_decode(
            state,
            amplitude.AsSpan(),
            okiData.AsSpan(),
            okiBytes);
    }

    public static int oki_adpcm_encode(
        OkiAdpcmState state,
        Span<byte> okiData,
        ReadOnlySpan<short> amplitude,
        int length) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Encode(okiData, amplitude, length);
    }

    public static int oki_adpcm_encode(
        OkiAdpcmState state,
        byte[] okiData,
        short[] amplitude,
        int length) {
        ArgumentNullException.ThrowIfNull(okiData);
        ArgumentNullException.ThrowIfNull(amplitude);
        return oki_adpcm_encode(
            state,
            okiData.AsSpan(),
            amplitude.AsSpan(),
            length);
    }

    internal static int DecodeCore(
        OkiAdpcmState state,
        Span<short> destination,
        ReadOnlySpan<byte> source,
        int sourceBytes) {
        ValidateCount(sourceBytes, source.Length, nameof(sourceBytes));

        int requiredSamples = state.BitRate == BitRate32000
            ? checked(sourceBytes * 2)
            : GetDecodedSampleCount(sourceBytes, state.Phase);

        if (destination.Length < requiredSamples) {
            throw new ArgumentException(
                $"The destination buffer must hold at least {requiredSamples} samples.",
                nameof(destination));
        }

        int samples = 0;

        if (state.BitRate == BitRate32000) {
            for (int i = 0; i < sourceBytes; i++) {
                destination[samples++] = unchecked((short)(
                    DecodeNibble(state, (byte)((source[i] >> 4) & 0x0F)) << 4));
                destination[samples++] = unchecked((short)(
                    DecodeNibble(state, (byte)(source[i] & 0x0F)) << 4));
            }

            return samples;
        }

        int nibbleNumber = 0;
        for (int i = 0; i < sourceBytes;) {
            if (state.Phase != 0) {
                byte nibble;
                if ((nibbleNumber++ & 1) != 0)
                    nibble = (byte)(source[i++] & 0x0F);
                else
                    nibble = (byte)((source[i] >> 4) & 0x0F);

                state.History[state.HistoryPointer++] = unchecked((short)(
                    DecodeNibble(state, nibble) << 4));
                state.HistoryPointer &= 31;
            }

            float filtered = 0.0f;
            for (
                int coefficient = 77 + state.Phase, history = state.HistoryPointer - 1;
                coefficient >= 0;
                coefficient -= 4, history--) {
                filtered += CutoffCoefficients[coefficient]
                    * state.History[history & 31];
            }

            destination[samples++] = FloatToInt16(filtered * 4.0f);

            state.Phase++;
            if (state.Phase > 3)
                state.Phase = 0;
        }

        return samples;
    }

    internal static int EncodeCore(
        OkiAdpcmState state,
        Span<byte> destination,
        ReadOnlySpan<short> source,
        int sampleCount) {
        ValidateCount(sampleCount, source.Length, nameof(sampleCount));

        if (sampleCount == 0)
            return 0;

        int encodedNibbles = state.BitRate == BitRate32000
            ? sampleCount
            : GetEncodedNibbleCount(sampleCount, state.Phase);
        int requiredBytes = GetEncodedByteCount(encodedNibbles, state.Mark);

        if (destination.Length < requiredBytes) {
            throw new ArgumentException(
                $"The destination buffer must hold at least {requiredBytes} bytes.",
                nameof(destination));
        }

        int bytes = 0;

        if (state.BitRate == BitRate32000) {
            for (int sample = 0; sample < sampleCount; sample++) {
                state.OkiByte = unchecked((byte)(
                    (state.OkiByte << 4) | EncodeSample(state, source[sample])));

                if ((state.Mark++ & 1) != 0)
                    destination[bytes++] = state.OkiByte;
            }

            return bytes;
        }

        int input = 0;
        for (; ; )
        {
            if (state.Phase > 2) {
                state.History[state.HistoryPointer++] = source[input];
                state.HistoryPointer &= 31;
                state.Phase = 0;

                input++;
                if (input >= sampleCount)
                    break;
            }

            state.History[state.HistoryPointer++] = source[input];
            state.HistoryPointer &= 31;

            float filtered = 0.0f;
            for (
                int coefficient = 80 - state.Phase, history = state.HistoryPointer - 1;
                coefficient >= 0;
                coefficient -= 3, history--) {
                filtered += CutoffCoefficients[coefficient]
                    * state.History[history & 31];
            }

            state.OkiByte = unchecked((byte)(
                (state.OkiByte << 4)
                | EncodeSample(state, FloatToInt16(filtered * 3.0f))));

            if ((state.Mark++ & 1) != 0)
                destination[bytes++] = state.OkiByte;

            state.Phase++;
            input++;
            if (input >= sampleCount)
                break;
        }

        return bytes;
    }

    internal static void ValidateBitRate(int bitRate) {
        if (bitRate != BitRate24000 && bitRate != BitRate32000) {
            throw new ArgumentOutOfRangeException(
                nameof(bitRate),
                bitRate,
                "The OKI ADPCM bit rate must be 24000 or 32000.");
        }
    }

    private static short DecodeNibble(OkiAdpcmState state, byte adpcm) {
        int step = StepSize[state.StepIndex];
        int difference = step >> 3;

        if ((adpcm & 0x01) != 0)
            difference += step >> 2;
        if ((adpcm & 0x02) != 0)
            difference += step >> 1;
        if ((adpcm & 0x04) != 0)
            difference += step;
        if ((adpcm & 0x08) != 0)
            difference = -difference;

        int linear = state.Last + difference;
        linear = Math.Clamp(linear, -2048, 2047);

        state.Last = (short)linear;

        int stepIndex = state.StepIndex + StepAdjustment[adpcm & 0x07];
        state.StepIndex = (short)Math.Clamp(stepIndex, 0, 48);
        return state.Last;
    }

    private static byte EncodeSample(OkiAdpcmState state, short linear) {
        int step = StepSize[state.StepIndex];
        int difference = (linear >> 4) - state.Last;
        byte adpcm = 0;

        if (difference < 0) {
            adpcm = 0x08;
            difference = -difference;
        }

        if (difference >= step) {
            adpcm |= 0x04;
            difference -= step;
        }

        if (difference >= (step >> 1)) {
            adpcm |= 0x02;
            difference -= step >> 1;
        }

        if (difference >= (step >> 2))
            adpcm |= 0x01;

        _ = DecodeNibble(state, adpcm);
        return adpcm;
    }

    private static int GetDecodedSampleCount(int sourceBytes, int initialPhase) {
        int sourceIndex = 0;
        int nibbleNumber = 0;
        int phase = initialPhase;
        int samples = 0;

        while (sourceIndex < sourceBytes) {
            if (phase != 0 && (nibbleNumber++ & 1) != 0)
                sourceIndex++;

            samples++;
            phase++;
            if (phase > 3)
                phase = 0;
        }

        return samples;
    }

    private static int GetEncodedNibbleCount(int sampleCount, int initialPhase) {
        if (sampleCount <= 0)
            return 0;

        int input = 0;
        int phase = initialPhase;
        int nibbles = 0;

        for (; ; )
        {
            if (phase > 2) {
                phase = 0;
                input++;
                if (input >= sampleCount)
                    break;
            }

            nibbles++;
            phase++;
            input++;
            if (input >= sampleCount)
                break;
        }

        return nibbles;
    }

    private static int GetEncodedByteCount(int nibbleCount, int initialMark) {
        int bytes = 0;
        int mark = initialMark;

        for (int i = 0; i < nibbleCount; i++) {
            if ((mark++ & 1) != 0)
                bytes++;
        }

        return bytes;
    }

    private static void ValidateCount(int count, int bufferLength, string parameterName) {
        if ((uint)count > (uint)bufferLength) {
            throw new ArgumentOutOfRangeException(
                parameterName,
                count,
                "The requested count must fit in the supplied buffer.");
        }
    }

    private static short FloatToInt16(float value) {
        if (value >= short.MaxValue)
            return short.MaxValue;
        if (value <= short.MinValue)
            return short.MinValue;
        return (short)value;
    }
}
