/*
 * TKFaxEngine - managed C# port
 *
 * ImaAdpcm.cs
 *
 * Combined port of ima_adpcm.h, private/ima_adpcm.h and ima_adpcm.c.
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2001, 2004 Steve Underwood.
 *
 * This port preserves the GNU Lesser General Public License version 2.1
 * licensing terms of the original source files.
 */

#nullable enable

namespace TKFaxEngine.Audio;

/// <summary>
/// Managed equivalent of <c>ima_adpcm_state_t</c>. The state is used for
/// either linear PCM to IMA ADPCM conversion or IMA ADPCM to linear PCM
/// conversion.
/// </summary>
public sealed class ImaAdpcmState : IDisposable {
    private bool _disposed;

    public ImaAdpcmState(int variant, int chunkSize) {
        Initialize(variant, chunkSize);
    }

    /// <summary>
    /// IMA ADPCM variant. One of <see cref="ImaAdpcm.IMA_ADPCM_IMA4"/>,
    /// <see cref="ImaAdpcm.IMA_ADPCM_DVI4"/> or
    /// <see cref="ImaAdpcm.IMA_ADPCM_VDVI"/>.
    /// </summary>
    public int Variant { get; internal set; }

    /// <summary>
    /// Size of a chunk in samples. Zero means every encode or decode call is
    /// treated as a complete chunk and carries its own four-byte header.
    /// </summary>
    public int ChunkSize { get; internal set; }

    /// <summary>Last predicted linear sample.</summary>
    public int Last { get; internal set; }

    /// <summary>Current index into the IMA step-size table.</summary>
    public int StepIndex { get; internal set; }

    /// <summary>Current encoded byte or VDVI bit accumulator.</summary>
    public ushort ImaByte { get; internal set; }

    /// <summary>Number of accumulated or pending code bits.</summary>
    public int Bits { get; internal set; }

    public bool IsDisposed => _disposed;

    public void Initialize(int variant, int chunkSize) {
        Variant = variant;
        ChunkSize = chunkSize;
        Last = 0;
        StepIndex = 0;
        ImaByte = 0;
        Bits = 0;
        _disposed = false;
    }

    public int Decode(
        Span<short> destination,
        ReadOnlySpan<byte> source,
        int sourceBytes) {
        ThrowIfDisposed();
        return ImaAdpcm.DecodeCore(this, destination, source, sourceBytes);
    }

    public int Decode(Span<short> destination, ReadOnlySpan<byte> source) {
        return Decode(destination, source, source.Length);
    }

    public int Encode(
        Span<byte> destination,
        ReadOnlySpan<short> source,
        int sampleCount) {
        ThrowIfDisposed();
        return ImaAdpcm.EncodeCore(this, destination, source, sampleCount);
    }

    public int Encode(Span<byte> destination, ReadOnlySpan<short> source) {
        return Encode(destination, source, source.Length);
    }

    /// <summary>
    /// Managed equivalent of <c>ima_adpcm_release()</c>. The original state
    /// owns no external resources, so release is intentionally a no-op.
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

        Variant = 0;
        ChunkSize = 0;
        Last = 0;
        StepIndex = 0;
        ImaByte = 0;
        Bits = 0;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    internal void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

/// <summary>
/// IMA/DVI/Intel ADPCM encoder and decoder. This is a direct managed port of
/// the spanDSP IMA4, RTP DVI4 and RTP VDVI implementations.
/// </summary>
public static class ImaAdpcm {
    /// <summary>Original IMA ADPCM variant.</summary>
    public const int IMA_ADPCM_IMA4 = 0;

    /// <summary>RTP DVI4 variant defined by RFC 3551.</summary>
    public const int IMA_ADPCM_DVI4 = 1;

    /// <summary>Variable-bit-rate VDVI variant defined by RFC 3551.</summary>
    public const int IMA_ADPCM_VDVI = 2;

    private const int StepMax = 88;

    private static readonly int[] StepSize =
    {
            7,     8,     9,    10,    11,    12,    13,    14,
           16,    17,    19,    21,    23,    25,    28,    31,
           34,    37,    41,    45,    50,    55,    60,    66,
           73,    80,    88,    97,   107,   118,   130,   143,
          157,   173,   190,   209,   230,   253,   279,   307,
          337,   371,   408,   449,   494,   544,   598,   658,
          724,   796,   876,   963,  1060,  1166,  1282,  1411,
         1552,  1707,  1878,  2066,  2272,  2499,  2749,  3024,
         3327,  3660,  4026,  4428,  4871,  5358,  5894,  6484,
         7132,  7845,  8630,  9493, 10442, 11487, 12635, 13899,
        15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794,
        32767
    };

    private static readonly int[] StepAdjustment =
    {
        -1, -1, -1, -1, 2, 4, 6, 8
    };

    private static readonly VdviEncodeEntry[] VdviEncode =
    {
        new(0x00, 2),
        new(0x02, 3),
        new(0x0C, 4),
        new(0x1C, 5),
        new(0x3C, 6),
        new(0x7C, 7),
        new(0xFC, 8),
        new(0xFE, 8),
        new(0x02, 2),
        new(0x03, 3),
        new(0x0D, 4),
        new(0x1D, 5),
        new(0x3D, 6),
        new(0x7D, 7),
        new(0xFD, 8),
        new(0xFF, 8)
    };

    private static readonly VdviDecodeEntry[] VdviDecode =
    {
        new(0x0000, 0xC000, 2),
        new(0x4000, 0xE000, 3),
        new(0xC000, 0xF000, 4),
        new(0xE000, 0xF800, 5),
        new(0xF000, 0xFC00, 6),
        new(0xF800, 0xFE00, 7),
        new(0xFC00, 0xFF00, 8),
        new(0xFE00, 0xFF00, 8),
        new(0x8000, 0xC000, 2),
        new(0x6000, 0xE000, 3),
        new(0xD000, 0xF000, 4),
        new(0xE800, 0xF800, 5),
        new(0xF400, 0xFC00, 6),
        new(0xFA00, 0xFE00, 7),
        new(0xFD00, 0xFF00, 8),
        new(0xFF00, 0xFF00, 8)
    };

    public static ImaAdpcmState? ima_adpcm_init(
        ImaAdpcmState? state,
        int variant,
        int chunkSize) {
        if (state is null)
            return new ImaAdpcmState(variant, chunkSize);

        state.Initialize(variant, chunkSize);
        return state;
    }

    public static int ima_adpcm_release(ImaAdpcmState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    public static int ima_adpcm_free(ImaAdpcmState? state) {
        state?.Dispose();
        return 0;
    }

    public static int ima_adpcm_decode(
        ImaAdpcmState state,
        Span<short> amplitude,
        ReadOnlySpan<byte> imaData,
        int imaBytes) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Decode(amplitude, imaData, imaBytes);
    }

    public static int ima_adpcm_decode(
        ImaAdpcmState state,
        short[] amplitude,
        byte[] imaData,
        int imaBytes) {
        ArgumentNullException.ThrowIfNull(amplitude);
        ArgumentNullException.ThrowIfNull(imaData);
        return ima_adpcm_decode(
            state,
            amplitude.AsSpan(),
            imaData.AsSpan(),
            imaBytes);
    }

    public static int ima_adpcm_encode(
        ImaAdpcmState state,
        Span<byte> imaData,
        ReadOnlySpan<short> amplitude,
        int length) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Encode(imaData, amplitude, length);
    }

    public static int ima_adpcm_encode(
        ImaAdpcmState state,
        byte[] imaData,
        short[] amplitude,
        int length) {
        ArgumentNullException.ThrowIfNull(imaData);
        ArgumentNullException.ThrowIfNull(amplitude);
        return ima_adpcm_encode(
            state,
            imaData.AsSpan(),
            amplitude.AsSpan(),
            length);
    }

    internal static int DecodeCore(
        ImaAdpcmState state,
        Span<short> destination,
        ReadOnlySpan<byte> source,
        int sourceBytes) {
        ValidateCount(sourceBytes, source.Length, nameof(sourceBytes));
        ValidateDecodeBuffers(state, destination.Length, source, sourceBytes);

        int sourceIndex;
        int samples = 0;
        ushort code;

        switch (state.Variant) {
            case IMA_ADPCM_IMA4:
                sourceIndex = 0;
                if (state.ChunkSize == 0) {
                    destination[samples++] = unchecked((short)(
                        (source[1] << 8) | source[0]));
                    state.StepIndex = source[2];
                    state.Last = destination[0];
                    sourceIndex = 4;
                }

                for (; sourceIndex < sourceBytes; sourceIndex++) {
                    destination[samples++] = DecodeCode(
                        state,
                        (byte)(source[sourceIndex] & 0x0F));
                    destination[samples++] = DecodeCode(
                        state,
                        (byte)((source[sourceIndex] >> 4) & 0x0F));
                }
                break;

            case IMA_ADPCM_DVI4:
                sourceIndex = 0;
                if (state.ChunkSize == 0) {
                    state.Last = unchecked((short)(
                        (source[0] << 8) | source[1]));
                    state.StepIndex = source[2];
                    sourceIndex = 4;
                }

                for (; sourceIndex < sourceBytes; sourceIndex++) {
                    destination[samples++] = DecodeCode(
                        state,
                        (byte)((source[sourceIndex] >> 4) & 0x0F));
                    destination[samples++] = DecodeCode(
                        state,
                        (byte)(source[sourceIndex] & 0x0F));
                }
                break;

            case IMA_ADPCM_VDVI:
                sourceIndex = 0;
                if (state.ChunkSize == 0) {
                    state.Last = unchecked((short)(
                        (source[0] << 8) | source[1]));
                    state.StepIndex = source[2];
                    sourceIndex = 4;
                }

                code = 0;
                state.Bits = 0;
                for (; ; ) {
                    if (state.Bits <= 8) {
                        if (sourceIndex >= sourceBytes)
                            break;

                        code = unchecked((ushort)(code |
                            ((ushort)source[sourceIndex++] << (8 - state.Bits))));
                        state.Bits += 8;
                    }

                    int decodedCode = FindVdviCode(code);
                    destination[samples++] = DecodeCode(
                        state,
                        (byte)decodedCode);
                    code = unchecked((ushort)(
                        code << VdviDecode[decodedCode].Bits));
                    state.Bits -= VdviDecode[decodedCode].Bits;
                }

                // Use up the remnants of the last octet.
                while (state.Bits > 0) {
                    int decodedCode = FindVdviCode(code);
                    if (VdviDecode[decodedCode].Bits > state.Bits)
                        break;

                    destination[samples++] = DecodeCode(
                        state,
                        (byte)decodedCode);
                    code = unchecked((ushort)(
                        code << VdviDecode[decodedCode].Bits));
                    state.Bits -= VdviDecode[decodedCode].Bits;
                }
                break;
        }

        return samples;
    }

    internal static int EncodeCore(
        ImaAdpcmState state,
        Span<byte> destination,
        ReadOnlySpan<short> source,
        int sampleCount) {
        ValidateCount(sampleCount, source.Length, nameof(sampleCount));
        ValidateEncodeBuffers(state, destination.Length, source, sampleCount);

        int sourceIndex;
        int bytes = 0;

        switch (state.Variant) {
            case IMA_ADPCM_IMA4:
                sourceIndex = 0;
                if (state.ChunkSize == 0) {
                    destination[bytes++] = unchecked((byte)source[0]);
                    destination[bytes++] = unchecked((byte)(source[0] >> 8));
                    destination[bytes++] = unchecked((byte)state.StepIndex);
                    destination[bytes++] = 0;
                    state.Last = source[0];
                    state.Bits = 0;
                    sourceIndex = 1;
                }

                for (; sourceIndex < sampleCount; sourceIndex++) {
                    state.ImaByte = unchecked((byte)(
                        (state.ImaByte >> 4)
                        | (EncodeSample(state, source[sourceIndex]) << 4)));
                    if ((state.Bits++ & 1) != 0)
                        destination[bytes++] = unchecked((byte)state.ImaByte);
                }
                break;

            case IMA_ADPCM_DVI4:
                if (state.ChunkSize == 0) {
                    destination[bytes++] = unchecked((byte)(state.Last >> 8));
                    destination[bytes++] = unchecked((byte)state.Last);
                    destination[bytes++] = unchecked((byte)state.StepIndex);
                    destination[bytes++] = 0;
                }

                for (sourceIndex = 0; sourceIndex < sampleCount; sourceIndex++) {
                    state.ImaByte = unchecked((byte)(
                        (state.ImaByte << 4)
                        | EncodeSample(state, source[sourceIndex])));
                    if ((state.Bits++ & 1) != 0)
                        destination[bytes++] = unchecked((byte)state.ImaByte);
                }
                break;

            case IMA_ADPCM_VDVI:
                if (state.ChunkSize == 0) {
                    destination[bytes++] = unchecked((byte)(state.Last >> 8));
                    destination[bytes++] = unchecked((byte)state.Last);
                    destination[bytes++] = unchecked((byte)state.StepIndex);
                    destination[bytes++] = 0;
                }

                state.Bits = 0;
                for (sourceIndex = 0; sourceIndex < sampleCount; sourceIndex++) {
                    byte adpcmCode = EncodeSample(state, source[sourceIndex]);
                    VdviEncodeEntry entry = VdviEncode[adpcmCode];

                    state.ImaByte = unchecked((ushort)(
                        (state.ImaByte << entry.Bits) | entry.Code));
                    state.Bits += entry.Bits;
                    if (state.Bits >= 8) {
                        state.Bits -= 8;
                        destination[bytes++] = unchecked((byte)(
                            state.ImaByte >> state.Bits));
                    }
                }

                if (state.Bits != 0) {
                    destination[bytes++] = unchecked((byte)(
                        ((state.ImaByte << 8) | 0xFF) >> state.Bits));
                }
                break;
        }

        return bytes;
    }

    private static short DecodeCode(ImaAdpcmState state, byte adpcm) {
        int step = StepSize[state.StepIndex];
        int error = step >> 3;

        if ((adpcm & 0x01) != 0)
            error += step >> 2;
        if ((adpcm & 0x02) != 0)
            error += step >> 1;
        if ((adpcm & 0x04) != 0)
            error += step;
        if ((adpcm & 0x08) != 0)
            error = -error;

        short linear = Saturated.saturate16(state.Last + error);
        state.Last = linear;
        state.StepIndex += StepAdjustment[adpcm & 0x07];

        if (state.StepIndex < 0)
            state.StepIndex = 0;
        else if (state.StepIndex > StepMax)
            state.StepIndex = StepMax;

        return linear;
    }

    private static byte EncodeSample(ImaAdpcmState state, short linear) {
        int step = StepSize[state.StepIndex];
        int initialError = linear - state.Last;
        int error = initialError;
        int difference = step >> 3;
        byte adpcm = 0;

        if (error < 0) {
            adpcm = 0x08;
            error = -error;
        }

        if (error >= step) {
            adpcm |= 0x04;
            error -= step;
        }

        step >>= 1;
        if (error >= step) {
            adpcm |= 0x02;
            error -= step;
        }

        step >>= 1;
        if (error >= step) {
            adpcm |= 0x01;
            error -= step;
        }

        if (initialError < 0)
            difference = -(difference - initialError - error);
        else
            difference = difference + initialError - error;

        state.Last = Saturated.saturate16(difference + state.Last);
        state.StepIndex += StepAdjustment[adpcm & 0x07];

        if (state.StepIndex < 0)
            state.StepIndex = 0;
        else if (state.StepIndex > StepMax)
            state.StepIndex = StepMax;

        return adpcm;
    }

    private static int FindVdviCode(ushort code) {
        for (int index = 0; index < 8; index++) {
            VdviDecodeEntry positive = VdviDecode[index];
            if ((positive.Mask & code) == positive.Code)
                return index;

            VdviDecodeEntry negative = VdviDecode[index + 8];
            if ((negative.Mask & code) == negative.Code)
                return index + 8;
        }

        throw new InvalidOperationException("Invalid VDVI prefix code.");
    }

    private static void ValidateDecodeBuffers(
        ImaAdpcmState state,
        int destinationLength,
        ReadOnlySpan<byte> source,
        int sourceBytes) {
        if (state.Variant != IMA_ADPCM_IMA4
            && state.Variant != IMA_ADPCM_DVI4
            && state.Variant != IMA_ADPCM_VDVI) {
            return;
        }

        int headerBytes = state.ChunkSize == 0 ? 4 : 0;
        if (sourceBytes < headerBytes) {
            throw new ArgumentException(
                "A complete IMA ADPCM chunk header requires four bytes.",
                nameof(sourceBytes));
        }

        int payloadBytes = sourceBytes - headerBytes;
        int requiredSamples = state.Variant switch {
            IMA_ADPCM_IMA4 => checked(payloadBytes * 2
                + (state.ChunkSize == 0 ? 1 : 0)),
            IMA_ADPCM_DVI4 => checked(payloadBytes * 2),
            IMA_ADPCM_VDVI => CountVdviSamples(
                source,
                headerBytes,
                sourceBytes),
            _ => 0
        };

        if (destinationLength < requiredSamples) {
            throw new ArgumentException(
                $"The destination buffer must hold at least {requiredSamples} samples.",
                nameof(destinationLength));
        }
    }

    private static void ValidateEncodeBuffers(
        ImaAdpcmState state,
        int destinationLength,
        ReadOnlySpan<short> source,
        int sampleCount) {
        if (state.Variant != IMA_ADPCM_IMA4
            && state.Variant != IMA_ADPCM_DVI4
            && state.Variant != IMA_ADPCM_VDVI) {
            return;
        }

        int headerBytes = state.ChunkSize == 0 ? 4 : 0;
        if (state.ChunkSize == 0
            && state.Variant == IMA_ADPCM_IMA4
            && sampleCount == 0) {
            throw new ArgumentException(
                "An IMA4 chunk requires at least one linear PCM sample.",
                nameof(sampleCount));
        }

        int requiredBytes;
        switch (state.Variant) {
            case IMA_ADPCM_IMA4:
                int ima4Codes = sampleCount - (state.ChunkSize == 0 ? 1 : 0);
                if (ima4Codes < 0)
                    ima4Codes = 0;
                int ima4InitialParity = state.ChunkSize == 0 ? 0 : state.Bits & 1;
                requiredBytes = checked(headerBytes
                    + ((ima4Codes + ima4InitialParity) / 2));
                break;

            case IMA_ADPCM_DVI4:
                requiredBytes = checked(headerBytes
                    + ((sampleCount + (state.Bits & 1)) / 2));
                break;

            case IMA_ADPCM_VDVI:
                requiredBytes = checked(headerBytes
                    + GetVdviEncodedByteCount(state, source, sampleCount));
                break;

            default:
                requiredBytes = 0;
                break;
        }

        if (destinationLength < requiredBytes) {
            throw new ArgumentException(
                $"The destination buffer must hold at least {requiredBytes} bytes.",
                nameof(destinationLength));
        }
    }

    private static int CountVdviSamples(
        ReadOnlySpan<byte> source,
        int sourceIndex,
        int sourceBytes) {
        ushort code = 0;
        int bits = 0;
        int samples = 0;

        for (; ; ) {
            if (bits <= 8) {
                if (sourceIndex >= sourceBytes)
                    break;

                code = unchecked((ushort)(code
                    | ((ushort)source[sourceIndex++] << (8 - bits))));
                bits += 8;
            }

            int decodedCode = FindVdviCode(code);
            samples++;
            code = unchecked((ushort)(
                code << VdviDecode[decodedCode].Bits));
            bits -= VdviDecode[decodedCode].Bits;
        }

        while (bits > 0) {
            int decodedCode = FindVdviCode(code);
            if (VdviDecode[decodedCode].Bits > bits)
                break;

            samples++;
            code = unchecked((ushort)(
                code << VdviDecode[decodedCode].Bits));
            bits -= VdviDecode[decodedCode].Bits;
        }

        return samples;
    }

    private static int GetVdviEncodedByteCount(
        ImaAdpcmState state,
        ReadOnlySpan<short> source,
        int sampleCount) {
        var scratch = new ImaAdpcmState(state.Variant, state.ChunkSize) {
            Last = state.Last,
            StepIndex = state.StepIndex,
            ImaByte = state.ImaByte,
            Bits = state.Bits
        };

        int bits = 0;
        for (int index = 0; index < sampleCount; index++) {
            byte adpcmCode = EncodeSample(scratch, source[index]);
            bits = checked(bits + VdviEncode[adpcmCode].Bits);
        }

        return checked((bits + 7) / 8);
    }

    private static void ValidateCount(
        int count,
        int bufferLength,
        string parameterName) {
        if ((uint)count > (uint)bufferLength) {
            throw new ArgumentOutOfRangeException(
                parameterName,
                count,
                "The requested count must fit in the supplied buffer.");
        }
    }

    private readonly record struct VdviEncodeEntry(byte Code, byte Bits);

    private readonly record struct VdviDecodeEntry(
        ushort Code,
        ushort Mask,
        byte Bits);
}
