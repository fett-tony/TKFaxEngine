/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * ToneDetect.cs - Managed C# port of tone_detect.c and tone_detect.h
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2001-2003, 2005 Steve Underwood
 *
 * This file is distributed under the terms of the GNU Lesser General Public
 * License version 2.1, matching the original source files.
 */

#nullable enable

namespace TKFaxEngine.Audio;

/// <summary>Single-precision complex value used by the periodogram helpers.</summary>
public readonly record struct ToneComplexF(float Real, float Imaginary) {
    public float Re => Real;

    public float Im => Imaginary;

    public static ToneComplexF operator +(ToneComplexF left, ToneComplexF right) =>
        new(left.Real + right.Real, left.Imaginary + right.Imaginary);

    public static ToneComplexF operator -(ToneComplexF left, ToneComplexF right) =>
        new(left.Real - right.Real, left.Imaginary - right.Imaginary);

    public static ToneComplexF operator *(ToneComplexF left, ToneComplexF right) =>
        new(
            left.Real * right.Real - left.Imaginary * right.Imaginary,
            left.Real * right.Imaginary + left.Imaginary * right.Real);
}

/// <summary>Immutable setup values for one Goertzel analysis bin.</summary>
public sealed class GoertzelDescriptor {
#if TKFAXENGINE_USE_FIXED_POINT
    public short Factor { get; internal set; }
#else
    public float Factor { get; internal set; }
#endif

    public int Samples { get; internal set; }
}

/// <summary>Incremental state for one Goertzel analysis bin.</summary>
public sealed class GoertzelState : IDisposable {
    private bool _disposed;

#if TKFAXENGINE_USE_FIXED_POINT
    public short V2 { get; private set; }

    public short V3 { get; private set; }

    public short Factor { get; private set; }
#else
    public float V2 { get; private set; }

    public float V3 { get; private set; }

    public float Factor { get; private set; }
#endif

    public int Samples { get; private set; }

    public int CurrentSample { get; private set; }

    public GoertzelState(GoertzelDescriptor descriptor) {
        Initialize(descriptor);
    }

    public void Initialize(GoertzelDescriptor descriptor) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(descriptor);
        if (descriptor.Samples <= 0) {
            throw new ArgumentOutOfRangeException(nameof(descriptor));
        }

        Factor = descriptor.Factor;
        Samples = descriptor.Samples;
        Reset();
    }

    public void Reset() {
        ThrowIfDisposed();
#if TKFAXENGINE_USE_FIXED_POINT
        V2 = 0;
        V3 = 0;
#else
        V2 = 0.0f;
        V3 = 0.0f;
#endif
        CurrentSample = 0;
    }

    /// <summary>Processes at most the remaining samples in the configured block.</summary>
    public int Update(ReadOnlySpan<short> amplitudes) {
        ThrowIfDisposed();

        int count = Math.Max(0, Math.Min(amplitudes.Length, Samples - CurrentSample));
        for (int i = 0; i < count; i++) {
            Sample(amplitudes[i]);
        }

        return count;
    }

    public void Sample(short amplitude) {
        ThrowIfDisposed();

#if TKFAXENGINE_USE_FIXED_POINT
        short previousV2 = V2;
        V2 = V3;
        short product = unchecked((short)(((int)Factor * V2) >> 14));
        V3 = unchecked((short)(product - previousV2 + (amplitude >> 7)));
#else
        float previousV2 = V2;
        V2 = V3;
        V3 = Factor * V2 - previousV2 + amplitude;
#endif
        CurrentSample++;
    }

#if TKFAXENGINE_USE_FIXED_POINT
    /// <summary>Minimal update using a pre-shifted fixed-point amplitude.</summary>
    public void SampleAdjusted(short amplitude)
    {
        ThrowIfDisposed();
        short previousV2 = V2;
        V2 = V3;
        short product = unchecked((short)(((int)Factor * V2) >> 14));
        V3 = unchecked((short)(product - previousV2 + amplitude));
    }

    public int Result()
    {
        ThrowIfDisposed();

        short previousV2 = V2;
        V2 = V3;
        short product = unchecked((short)(((int)Factor * V2) >> 14));
        V3 = unchecked((short)(product - previousV2));

        int result = unchecked(V3 * V3);
        result = unchecked(result + V2 * V2);
        int cross = unchecked((((int)V3 * Factor) >> 14) * V2);
        result = unchecked(result - cross);
        result = unchecked(result << 1);
        Reset();
        return result;
    }
#else
    /// <summary>Minimal update using a floating-point amplitude.</summary>
    public void SampleAdjusted(float amplitude) {
        ThrowIfDisposed();
        float previousV2 = V2;
        V2 = V3;
        V3 = Factor * V2 - previousV2 + amplitude;
    }

    public float Result() {
        ThrowIfDisposed();

        float previousV2 = V2;
        V2 = V3;
        V3 = Factor * V2 - previousV2;
        float result = 2.0f * (V3 * V3 + V2 * V2 - V2 * V3 * Factor);
        Reset();
        return result;
    }
#endif

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

/// <summary>Goertzel and complex periodogram utilities for telephony tones.</summary>
public static class ToneDetect {
    public const int SampleRate = 8000;
    public const float Dbm0MaximumSinePower = 3.14f;
    public const float DbovMaximumSinePower = -3.01f;

    public static GoertzelDescriptor MakeGoertzelDescriptor(float frequency, int samples) {
        if (!float.IsFinite(frequency)) {
            throw new ArgumentOutOfRangeException(nameof(frequency));
        }

        if (samples <= 0) {
            throw new ArgumentOutOfRangeException(nameof(samples));
        }

        float factor = 2.0f * MathF.Cos(2.0f * MathF.PI * frequency / SampleRate);
        GoertzelDescriptor descriptor = new();
#if TKFAXENGINE_USE_FIXED_POINT
        descriptor.Factor = unchecked((short)(16383.0f * factor));
#else
        descriptor.Factor = factor;
#endif
        descriptor.Samples = samples;
        return descriptor;
    }

#if TKFAXENGINE_USE_FIXED_POINT
    public static int GoertzelThresholdDbm0(int length, float threshold)
    {
        double scale = length * (double)length * 256.0 * 256.0 / 2.0;
        return SaturateToInt32(scale * Math.Pow(10.0, (threshold - Dbm0MaximumSinePower) / 10.0));
    }

    public static int GoertzelThresholdDbov(int length, float threshold)
    {
        double scale = length * (double)length * 256.0 * 256.0 / 2.0;
        return SaturateToInt32(scale * Math.Pow(10.0, (threshold - DbovMaximumSinePower) / 10.0));
    }

    public static short PreadjustAmplitude(short amplitude) => (short)(amplitude >> 7);
#else
    public static float GoertzelThresholdDbm0(int length, float threshold) {
        double scale = length * (double)length * 32768.0 * 32768.0 / 2.0;
        return (float)(scale * Math.Pow(10.0, (threshold - Dbm0MaximumSinePower) / 10.0));
    }

    public static float GoertzelThresholdDbov(int length, float threshold) {
        double scale = length * (double)length * 32768.0 * 32768.0 / 2.0;
        return (float)(scale * Math.Pow(10.0, (threshold - DbovMaximumSinePower) / 10.0));
    }

    public static float PreadjustAmplitude(short amplitude) => amplitude;
#endif

    public static ToneComplexF Periodogram(
        ReadOnlySpan<ToneComplexF> coefficients,
        ReadOnlySpan<ToneComplexF> amplitudes,
        int length) {
        ValidatePeriodogramLength(length, amplitudes.Length);
        if (coefficients.Length < length / 2) {
            throw new ArgumentException("Too few periodogram coefficients.", nameof(coefficients));
        }

        float real = 0.0f;
        float imaginary = 0.0f;
        for (int i = 0; i < length / 2; i++) {
            ToneComplexF sum = amplitudes[i] + amplitudes[length - 1 - i];
            ToneComplexF difference = amplitudes[i] - amplitudes[length - 1 - i];
            ToneComplexF coefficient = coefficients[i];
            real += coefficient.Real * sum.Real - coefficient.Imaginary * difference.Imaginary;
            imaginary += coefficient.Real * sum.Imaginary + coefficient.Imaginary * difference.Real;
        }

        return new ToneComplexF(real, imaginary);
    }

    public static int PreparePeriodogram(
        Span<ToneComplexF> sums,
        Span<ToneComplexF> differences,
        ReadOnlySpan<ToneComplexF> amplitudes,
        int length) {
        ValidatePeriodogramLength(length, amplitudes.Length);
        int halfLength = length / 2;
        if (sums.Length < halfLength || differences.Length < halfLength) {
            throw new ArgumentException("The result spans are too short.");
        }

        for (int i = 0; i < halfLength; i++) {
            sums[i] = amplitudes[i] + amplitudes[length - 1 - i];
            differences[i] = amplitudes[i] - amplitudes[length - 1 - i];
        }

        return halfLength;
    }

    public static ToneComplexF ApplyPeriodogram(
        ReadOnlySpan<ToneComplexF> coefficients,
        ReadOnlySpan<ToneComplexF> sums,
        ReadOnlySpan<ToneComplexF> differences,
        int length) {
        if (length <= 0 || (length & 1) != 0) {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be a positive even number.");
        }

        int halfLength = length / 2;
        if (coefficients.Length < halfLength || sums.Length < halfLength || differences.Length < halfLength) {
            throw new ArgumentException("The periodogram vectors are too short.");
        }

        float real = 0.0f;
        float imaginary = 0.0f;
        for (int i = 0; i < halfLength; i++) {
            ToneComplexF coefficient = coefficients[i];
            real += coefficient.Real * sums[i].Real - coefficient.Imaginary * differences[i].Imaginary;
            imaginary += coefficient.Real * sums[i].Imaginary + coefficient.Imaginary * differences[i].Real;
        }

        return new ToneComplexF(real, imaginary);
    }

    public static int GeneratePeriodogramCoefficients(
        Span<ToneComplexF> coefficients,
        float frequency,
        int sampleRate,
        int windowLength) {
        if (sampleRate <= 0) {
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        }

        if (windowLength <= 0 || (windowLength & 1) != 0) {
            throw new ArgumentOutOfRangeException(nameof(windowLength),
                "Window length must be a positive even number.");
        }

        int halfLength = windowLength / 2;
        if (coefficients.Length < halfLength) {
            throw new ArgumentException("The coefficient span is too short.", nameof(coefficients));
        }

        float windowSum = 0.0f;
        for (int i = 0; i < halfLength; i++) {
            float window = 0.53836f
                - 0.46164f * MathF.Cos(2.0f * MathF.PI * i / (windowLength - 1.0f));
            float phase = (i - windowLength / 2.0f + 0.5f)
                * frequency * 2.0f * MathF.PI / sampleRate;
            coefficients[i] = new ToneComplexF(
                MathF.Cos(phase) * window,
                -MathF.Sin(phase) * window);
            windowSum += window;
        }

        float gain = 1.0f / (2.0f * windowSum);
        for (int i = 0; i < halfLength; i++) {
            ToneComplexF value = coefficients[i];
            coefficients[i] = new ToneComplexF(value.Real * gain, value.Imaginary * gain);
        }

        return halfLength;
    }

    public static float GeneratePeriodogramPhaseOffset(
        out ToneComplexF offset,
        float frequency,
        int sampleRate,
        int interval) {
        if (sampleRate <= 0 || interval <= 0) {
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        }

        float radiansPerHertz = 2.0f * MathF.PI * interval / sampleRate;
        offset = new ToneComplexF(
            MathF.Cos(frequency * radiansPerHertz),
            MathF.Sin(frequency * radiansPerHertz));
        return 1.0f / radiansPerHertz;
    }

    public static float PeriodogramFrequencyError(
        ToneComplexF phaseOffset,
        float scale,
        ToneComplexF lastResult,
        ToneComplexF result) {
        ToneComplexF prediction = lastResult * phaseOffset;
        float denominator = result.Real * result.Real + result.Imaginary * result.Imaginary;
        if (denominator <= float.Epsilon) {
            return 0.0f;
        }

        return scale
            * (result.Imaginary * prediction.Real - result.Real * prediction.Imaginary)
            / denominator;
    }

    private static void ValidatePeriodogramLength(int length, int available) {
        if (length <= 0 || (length & 1) != 0 || length > available) {
            throw new ArgumentOutOfRangeException(nameof(length),
                "Length must be a positive even number within the input span.");
        }
    }

#if TKFAXENGINE_USE_FIXED_POINT
    private static int SaturateToInt32(double value)
    {
        if (value >= int.MaxValue)
        {
            return int.MaxValue;
        }

        if (value <= int.MinValue)
        {
            return int.MinValue;
        }

        return (int)value;
    }
#endif
}

/// <summary>Native-name-compatible entry points for tone detection.</summary>
public static class ToneDetectApi {
    public static void make_goertzel_descriptor(GoertzelDescriptor descriptor, float frequency, int samples) {
        ArgumentNullException.ThrowIfNull(descriptor);
        GoertzelDescriptor value = ToneDetect.MakeGoertzelDescriptor(frequency, samples);
        descriptor.Factor = value.Factor;
        descriptor.Samples = value.Samples;
    }

    public static GoertzelState? goertzel_init(GoertzelState? state, GoertzelDescriptor descriptor) {
        try {
            if (state is null) {
                return new GoertzelState(descriptor);
            }

            state.Initialize(descriptor);
            return state;
        } catch (ArgumentException) {
            return null;
        }
    }

    public static int goertzel_release(GoertzelState state) {
        ArgumentNullException.ThrowIfNull(state);
        return 0;
    }

    public static int goertzel_free(GoertzelState? state) {
        state?.Dispose();
        return 0;
    }

    public static void goertzel_reset(GoertzelState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.Reset();
    }

    public static int goertzel_update(GoertzelState state, short[] amplitudes, int samples) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(amplitudes);
        if (samples < 0 || samples > amplitudes.Length) {
            return -1;
        }

        return state.Update(amplitudes.AsSpan(0, samples));
    }

#if TKFAXENGINE_USE_FIXED_POINT
    public static int goertzel_result(GoertzelState state) => state.Result();

    public static short goertzel_preadjust_amp(short amplitude) => ToneDetect.PreadjustAmplitude(amplitude);

    public static void goertzel_samplex(GoertzelState state, short amplitude) => state.SampleAdjusted(amplitude);
#else
    public static float goertzel_result(GoertzelState state) => state.Result();

    public static float goertzel_preadjust_amp(short amplitude) => ToneDetect.PreadjustAmplitude(amplitude);

    public static void goertzel_samplex(GoertzelState state, float amplitude) => state.SampleAdjusted(amplitude);
#endif

    public static void goertzel_sample(GoertzelState state, short amplitude) => state.Sample(amplitude);

#if TKFAXENGINE_USE_FIXED_POINT
    public static int goertzel_threshold_dbm0(int length, float threshold) =>
        ToneDetect.GoertzelThresholdDbm0(length, threshold);

    public static int goertzel_threshold_dbmov(int length, float threshold) =>
        ToneDetect.GoertzelThresholdDbov(length, threshold);
#else
    public static float goertzel_threshold_dbm0(int length, float threshold) =>
        ToneDetect.GoertzelThresholdDbm0(length, threshold);

    public static float goertzel_threshold_dbmov(int length, float threshold) =>
        ToneDetect.GoertzelThresholdDbov(length, threshold);
#endif

    public static ToneComplexF periodogram(
        ToneComplexF[] coefficients,
        ToneComplexF[] amplitudes,
        int length) => ToneDetect.Periodogram(coefficients, amplitudes, length);

    public static int periodogram_prepare(
        ToneComplexF[] sums,
        ToneComplexF[] differences,
        ToneComplexF[] amplitudes,
        int length) => ToneDetect.PreparePeriodogram(sums, differences, amplitudes, length);

    public static ToneComplexF periodogram_apply(
        ToneComplexF[] coefficients,
        ToneComplexF[] sums,
        ToneComplexF[] differences,
        int length) => ToneDetect.ApplyPeriodogram(coefficients, sums, differences, length);

    public static int periodogram_generate_coeffs(
        ToneComplexF[] coefficients,
        float frequency,
        int sampleRate,
        int windowLength) =>
        ToneDetect.GeneratePeriodogramCoefficients(coefficients, frequency, sampleRate, windowLength);

    public static float periodogram_generate_phase_offset(
        out ToneComplexF offset,
        float frequency,
        int sampleRate,
        int interval) =>
        ToneDetect.GeneratePeriodogramPhaseOffset(out offset, frequency, sampleRate, interval);

    public static float periodogram_freq_error(
        ToneComplexF phaseOffset,
        float scale,
        ToneComplexF lastResult,
        ToneComplexF result) =>
        ToneDetect.PeriodogramFrequencyError(phaseOffset, scale, lastResult, result);
}
