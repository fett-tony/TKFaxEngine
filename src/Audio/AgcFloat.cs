/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * AgcFloat.cs - managed port of agc_float.c and agc_float.h
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2024 Steve Underwood.
 *
 * Distributed under the GNU Lesser General Public License version 2.1.
 */

#nullable enable

namespace TKFaxEngine.Audio;

public enum AgcFloatLogLevel {
    Flow,
    Warning
}

public sealed class AgcFloatLogger {
    public string Protocol { get; set; } = "AGC";

    public Action<AgcFloatLogLevel, string>? Sink { get; set; }

    public void Flow(string message) =>
        Sink?.Invoke(AgcFloatLogLevel.Flow, message);

    public void Warning(string message) =>
        Sink?.Invoke(AgcFloatLogLevel.Warning, message);
}

/// <summary>
/// Immutable AGC configuration. Power values are stored as energy accumulated
/// over <see cref="AgcFloat.SamplesPerChunk"/> samples, matching agcf_descriptor_t.
/// </summary>
public sealed class AgcFloatDescriptor : IDisposable {
    private bool _disposed;

    public float SignalOnPowerThreshold { get; internal set; }

    public float SignalOffPowerThreshold { get; internal set; }

    public float SignalTargetPower { get; internal set; }

    public short SignalOnPersistenceCheck { get; internal set; }

    public short SignalOffPersistenceCheck { get; internal set; }

    public short SignalDownPersistenceCheck { get; set; }

    public void Dispose() {
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    internal void ThrowIfDisposed() {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AgcFloatDescriptor));
    }
}

/// <summary>
/// Floating-point automatic gain controller with DC-blocked power detection.
/// </summary>
public sealed class AgcFloatState : IDisposable {
    private bool _disposed;

    internal AgcFloatDescriptor Descriptor { get; set; } = new();

    internal float DcBlockX { get; set; }

    internal float DcBlockY { get; set; }

    public float Gain { get; internal set; } = 1.0f;

    internal float CurrentEnergy { get; set; }

    internal int CurrentSamples { get; set; }

    public float LastPower { get; internal set; }

    internal int SignalOnPersistence { get; set; }

    internal int SignalOffPersistence { get; set; }

    public bool Adapt { get; set; } = true;

    public bool Detect { get; set; } = true;

    public bool ScaleSignal { get; set; } = true;

    public bool SignalPresent { get; internal set; }

    public AgcFloatLogger Logging { get; } = new();

    public bool Process(ReadOnlySpan<float> input, Span<float> output) {
        ThrowIfDisposed();
        return AgcFloat.Process(this, input, output);
    }

    public bool Process(ReadOnlySpan<short> input, Span<float> output) {
        ThrowIfDisposed();
        return AgcFloat.ProcessFromInt16(this, input, output);
    }

    public int Release() {
        ThrowIfDisposed();
        return 0;
    }

    public void Dispose() {
        if (_disposed)
            return;

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    internal void ThrowIfDisposed() {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AgcFloatState));
    }
}

public static class AgcFloat {
    public const int SamplesPerChunk = 40;

    public const float DcBlockCoefficient = 0.9921875f;

    public const float Dbm0MaximumSinePower = 3.14f;

    public const float Dbm0MaximumPower = 6.16f;

    private const float PcmScale = 32768.0f;

    public static AgcFloatDescriptor? MakeDescriptor(
        AgcFloatDescriptor? descriptor,
        float signalTargetPowerDbm0,
        float signalOnPowerThresholdDbm0,
        float signalOffPowerThresholdDbm0,
        int signalOnPersistenceCheck,
        int signalOffPersistenceCheck) {
        if (signalOnPowerThresholdDbm0 < signalOffPowerThresholdDbm0)
            return null;

        if (!float.IsFinite(signalTargetPowerDbm0) ||
            !float.IsFinite(signalOnPowerThresholdDbm0) ||
            !float.IsFinite(signalOffPowerThresholdDbm0)) {
            throw new ArgumentOutOfRangeException(
                nameof(signalTargetPowerDbm0));
        }

        if (signalOnPersistenceCheck < 0)
            throw new ArgumentOutOfRangeException(nameof(signalOnPersistenceCheck));
        if (signalOffPersistenceCheck < 0)
            throw new ArgumentOutOfRangeException(nameof(signalOffPersistenceCheck));

        descriptor ??= new AgcFloatDescriptor();
        descriptor.ThrowIfDisposed();

        descriptor.SignalTargetPower = EnergyThresholdDbm0(
            SamplesPerChunk,
            signalTargetPowerDbm0);
        descriptor.SignalOnPowerThreshold = EnergyThresholdDbm0(
            SamplesPerChunk,
            signalOnPowerThresholdDbm0);
        descriptor.SignalOffPowerThreshold = EnergyThresholdDbm0(
            SamplesPerChunk,
            signalOffPowerThresholdDbm0);
        descriptor.SignalOnPersistenceCheck = checked((short)(
            signalOnPersistenceCheck + 1));
        descriptor.SignalOffPersistenceCheck = checked((short)(
            signalOffPersistenceCheck + 1));
        descriptor.SignalDownPersistenceCheck = 0;
        return descriptor;
    }

    public static AgcFloatState Initialize(
        AgcFloatState? state,
        AgcFloatDescriptor descriptor) {
        ArgumentNullException.ThrowIfNull(descriptor);
        descriptor.ThrowIfDisposed();

        state ??= new AgcFloatState();
        state.ThrowIfDisposed();

        state.Descriptor = descriptor;
        state.DcBlockX = 0.0f;
        state.DcBlockY = 0.0f;
        state.Gain = 1.0f;
        state.CurrentEnergy = 0.0f;
        state.CurrentSamples = 0;
        state.LastPower = 0.0f;
        state.SignalOnPersistence = 0;
        state.SignalOffPersistence = 0;
        state.Adapt = true;
        state.Detect = true;
        state.ScaleSignal = true;
        state.SignalPresent = false;
        state.Logging.Protocol = "AGC";
        return state;
    }

    public static bool Process(
        AgcFloatState state,
        ReadOnlySpan<float> input,
        Span<float> output) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();

        if (output.Length < input.Length)
            throw new ArgumentException("Output buffer is too small.", nameof(output));

        DetectAndAdapt(state, input);

        if (state.ScaleSignal) {
            for (int index = 0; index < input.Length; index++)
                output[index] = input[index] * state.Gain;
        }

        return state.SignalPresent;
    }

    public static bool ProcessFromInt16(
        AgcFloatState state,
        ReadOnlySpan<short> input,
        Span<float> output) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();

        if (output.Length < input.Length)
            throw new ArgumentException("Output buffer is too small.", nameof(output));

        if (state.Adapt || state.Detect) {
            for (int index = 0; index < input.Length; index++)
                ConsumeSample(state, input[index]);
        }

        if (state.ScaleSignal) {
            for (int index = 0; index < input.Length; index++)
                output[index] = input[index] * state.Gain;
        }

        return state.SignalPresent;
    }

    public static float CurrentPowerDbm0(AgcFloatState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();

        if (state.LastPower <= 0.0f)
            return float.NegativeInfinity;

        return 10.0f * MathF.Log10(
            state.LastPower / (PcmScale * PcmScale)) +
            Dbm0MaximumPower;
    }

    public static float EnergyThresholdDbm0(int length, float thresholdDbm0) {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        return (length * PcmScale * PcmScale / 2.0f) *
            MathF.Pow(
                10.0f,
                (thresholdDbm0 - Dbm0MaximumSinePower) / 10.0f);
    }

    private static void DetectAndAdapt(
        AgcFloatState state,
        ReadOnlySpan<float> input) {
        if (!(state.Adapt || state.Detect))
            return;

        for (int index = 0; index < input.Length; index++)
            ConsumeSample(state, input[index]);
    }

    private static void ConsumeSample(AgcFloatState state, float sample) {
        float sampleNoDc =
            sample - state.DcBlockX +
            DcBlockCoefficient * state.DcBlockY;

        state.DcBlockX = sample;
        state.DcBlockY = sampleNoDc;
        state.CurrentEnergy += sampleNoDc * sampleNoDc;

        if (++state.CurrentSamples < SamplesPerChunk)
            return;

        state.LastPower = state.CurrentEnergy;
        AgcFloatDescriptor descriptor = state.Descriptor;

        if (state.LastPower >= descriptor.SignalOnPowerThreshold) {
            if (state.SignalOnPersistence < descriptor.SignalOnPersistenceCheck) {
                state.SignalOnPersistence++;
                if (state.SignalOnPersistence == descriptor.SignalOnPersistenceCheck)
                    state.SignalPresent = true;
            }
        } else {
            state.SignalOnPersistence = 0;

            if (state.LastPower <= descriptor.SignalOffPowerThreshold) {
                if (state.SignalOffPersistence < descriptor.SignalOffPersistenceCheck) {
                    state.SignalOffPersistence++;
                    if (state.SignalOffPersistence == descriptor.SignalOffPersistenceCheck)
                        state.SignalPresent = false;
                }
            } else {
                state.SignalOffPersistence = 0;
            }
        }

        if (state.SignalPresent && state.Adapt) {
            state.Gain = state.LastPower > 0.0f
                ? MathF.Sqrt(descriptor.SignalTargetPower / state.LastPower)
                : 1.0f;
        }

        state.CurrentEnergy = 0.0f;
        state.CurrentSamples = 0;
    }
}

/// <summary>C-compatible facade retaining all agcf_* names.</summary>
public static class AgcFloatApi {
    public const int AGC_SAMPLES_PER_CHUNK = AgcFloat.SamplesPerChunk;

    public static AgcFloatDescriptor? agcf_make_descriptor(
        AgcFloatDescriptor? descriptor,
        float signalTargetPower,
        float signalOnPowerThreshold,
        float signalOffPowerThreshold,
        int signalOnPersistenceCheck,
        int signalOffPersistenceCheck) =>
        AgcFloat.MakeDescriptor(
            descriptor,
            signalTargetPower,
            signalOnPowerThreshold,
            signalOffPowerThreshold,
            signalOnPersistenceCheck,
            signalOffPersistenceCheck);

    public static int agcf_free_descriptor(AgcFloatDescriptor? descriptor) {
        descriptor?.Dispose();
        return 0;
    }

    public static bool agcf_rx(
        AgcFloatState state,
        Span<float> output,
        ReadOnlySpan<float> input,
        int length) {
        ValidateLength(input.Length, output.Length, length);
        return AgcFloat.Process(
            state,
            input[..length],
            output[..length]);
    }

    public static bool agcf_rx(
        AgcFloatState state,
        float[] output,
        float[] input,
        int length) {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        return agcf_rx(state, output.AsSpan(), input.AsSpan(), length);
    }

    public static bool agcf_from_int16_rx(
        AgcFloatState state,
        Span<float> output,
        ReadOnlySpan<short> input,
        int length) {
        ValidateLength(input.Length, output.Length, length);
        return AgcFloat.ProcessFromInt16(
            state,
            input[..length],
            output[..length]);
    }

    public static bool agcf_from_int16_rx(
        AgcFloatState state,
        float[] output,
        short[] input,
        int length) {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        return agcf_from_int16_rx(
            state,
            output.AsSpan(),
            input.AsSpan(),
            length);
    }

    public static float agcf_current_power_dbm0(AgcFloatState state) =>
        AgcFloat.CurrentPowerDbm0(state);

    public static float agcf_get_scaling(AgcFloatState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        return state.Gain;
    }

    public static void agcf_set_scaling(AgcFloatState state, float scaling) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        if (!float.IsFinite(scaling))
            throw new ArgumentOutOfRangeException(nameof(scaling));
        state.Gain = scaling;
    }

    public static void agcf_set_adaption(AgcFloatState state, bool adapt) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        state.Adapt = adapt;
    }

    public static AgcFloatLogger agcf_get_logging_state(AgcFloatState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        return state.Logging;
    }

    public static AgcFloatState agcf_init(
        AgcFloatState? state,
        AgcFloatDescriptor descriptor) =>
        AgcFloat.Initialize(state, descriptor);

    public static int agcf_release(AgcFloatState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    public static int agcf_free(AgcFloatState? state) {
        state?.Dispose();
        return 0;
    }

    private static void ValidateLength(
        int inputLength,
        int outputLength,
        int requestedLength) {
        if (requestedLength < 0 ||
            requestedLength > inputLength ||
            requestedLength > outputLength) {
            throw new ArgumentOutOfRangeException(nameof(requestedLength));
        }
    }
}
