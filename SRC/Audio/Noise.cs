/*
 * TKFaxEngine - managed C# port
 *
 * Noise.cs
 *
 * Combined port of noise.h, private/noise.h and noise.c.
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2005 Steve Underwood.
 *
 * This port preserves the GNU Lesser General Public License version 2.1
 * licensing terms of the original source files.
 */

#nullable enable

namespace TKFaxEngine.Audio;

/// <summary>
/// Noise classes supported by the low-complexity audio noise generator.
/// </summary>
public enum NoiseClass {
    Awgn = 1,
    Hoth = 2
}

/// <summary>
/// Managed equivalent of <c>noise_state_t</c>.
/// </summary>
public sealed class NoiseState : IDisposable {
    private bool _disposed;

    public NoiseState() {
    }

    public NoiseState(
        int seed,
        float level,
        NoiseClass noiseClass = NoiseClass.Awgn,
        int quality = 8,
        bool levelIsDbm0 = false) {
        if (levelIsDbm0)
            InitializeDbm0(seed, level, noiseClass, quality);
        else
            InitializeDbov(seed, level, noiseClass, quality);
    }

    public NoiseClass ClassOfNoise { get; private set; }

    public int Quality { get; private set; }

    public int Rms { get; private set; }

    public uint RandomNumber { get; private set; }

    public int FilterState { get; private set; }

    public bool IsDisposed => _disposed;

    /// <summary>
    /// Initializes the generator using a level expressed in dBov.
    /// </summary>
    public void InitializeDbov(
        int seed,
        float level,
        NoiseClass noiseClass,
        int quality) {
        if (noiseClass != NoiseClass.Awgn && noiseClass != NoiseClass.Hoth)
            throw new ArgumentOutOfRangeException(nameof(noiseClass));

        Quality = Math.Clamp(quality, Noise.MinimumQuality, Noise.MaximumQuality);
        ClassOfNoise = noiseClass;
        RandomNumber = unchecked((uint)seed);
        FilterState = 0;

        double rms = Math.Pow(10.0, level / 20.0) * 32768.0;
        if (noiseClass == NoiseClass.Hoth)
            rms *= 1.043;

        Rms = checked((int)(rms * Math.Sqrt(12.0 / Quality)));
        _disposed = false;
    }

    /// <summary>
    /// Initializes the generator using a level expressed in dBm0.
    /// </summary>
    public void InitializeDbm0(
        int seed,
        float level,
        NoiseClass noiseClass,
        int quality) {
        InitializeDbov(
            seed,
            level - Noise.Dbm0MaximumPower,
            noiseClass,
            quality);
    }

    /// <summary>
    /// Generates one signed 16-bit PCM noise sample.
    /// </summary>
    public short GenerateSample() {
        ThrowIfDisposed();

        int value = 0;
        uint randomNumber = RandomNumber;

        unchecked {
            for (int i = 0; i < Quality; i++) {
                randomNumber = 1664525U * randomNumber + 1013904223U;
                value += ((int)randomNumber) >> 22;
            }
        }

        RandomNumber = randomNumber;

        if (ClassOfNoise == NoiseClass.Hoth) {
            unchecked {
                FilterState = (3 * value + 5 * FilterState) >> 3;
                value = FilterState << 1;
            }
        }

        long scaled = ((long)value * Rms) >> 10;
        return Saturate16(scaled);
    }

    /// <summary>
    /// Fills a destination buffer with generated noise samples.
    /// </summary>
    public void Generate(Span<short> destination) {
        ThrowIfDisposed();

        for (int i = 0; i < destination.Length; i++)
            destination[i] = GenerateSample();
    }

    /// <summary>
    /// Managed equivalent of <c>noise_release()</c>. No external resources are
    /// owned, so release is intentionally a no-op.
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

        ClassOfNoise = default;
        Quality = 0;
        Rms = 0;
        RandomNumber = 0;
        FilterState = 0;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed() {
        if (_disposed)
            throw new ObjectDisposedException(nameof(NoiseState));
    }

    private static short Saturate16(long value) {
        if (value > short.MaxValue)
            return short.MaxValue;
        if (value < short.MinValue)
            return short.MinValue;
        return (short)value;
    }
}

/// <summary>
/// C-compatible facade for the original noise generator API.
/// </summary>
public static class Noise {
    public const int NOISE_CLASS_AWGN = (int)NoiseClass.Awgn;
    public const int NOISE_CLASS_HOTH = (int)NoiseClass.Hoth;

    public const int MinimumQuality = 4;
    public const int MaximumQuality = 20;

    /// <summary>Native <c>DBM0_MAX_POWER</c> value: 3.14 + 3.02 dB.</summary>
    public const float Dbm0MaximumPower = 6.16f;

    public static NoiseState noise_init_dbov(
        NoiseState? state,
        int seed,
        float level,
        int classOfNoise,
        int quality) {
        NoiseState result = state ?? new NoiseState();
        result.InitializeDbov(
            seed,
            level,
            ValidateNoiseClass(classOfNoise),
            quality);
        return result;
    }

    public static NoiseState noise_init_dbm0(
        NoiseState? state,
        int seed,
        float level,
        int classOfNoise,
        int quality) {
        NoiseState result = state ?? new NoiseState();
        result.InitializeDbm0(
            seed,
            level,
            ValidateNoiseClass(classOfNoise),
            quality);
        return result;
    }

    public static int noise_release(NoiseState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    public static int noise_free(NoiseState? state) {
        state?.Dispose();
        return 0;
    }

    public static short noise(NoiseState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GenerateSample();
    }

    private static NoiseClass ValidateNoiseClass(int classOfNoise) {
        return classOfNoise switch {
            NOISE_CLASS_AWGN => NoiseClass.Awgn,
            NOISE_CLASS_HOTH => NoiseClass.Hoth,
            _ => throw new ArgumentOutOfRangeException(
                nameof(classOfNoise),
                classOfNoise,
                "Only AWGN and Hoth noise are supported.")
        };
    }
}
