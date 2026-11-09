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

/// <summary>
/// Defines the available <c>AgcFloatLogLevel</c> values.
/// </summary>
public enum AgcFloatLogLevel {
    /// <summary>
    /// Represents the <c>Flow</c> value.
    /// </summary>
    Flow,
    /// <summary>
    /// Represents the <c>Warning</c> value.
    /// </summary>
    Warning
}

/// <summary>
/// Represents the <c>AgcFloatLogger</c> type.
/// </summary>
public sealed class AgcFloatLogger {
    /// <summary>
    /// Gets or sets the <c>Protocol</c> value.
    /// </summary>
    public string Protocol { get; set; } = "AGC";

    /// <summary>
    /// Gets or sets the <c>Sink</c> value.
    /// </summary>
    public Action<AgcFloatLogLevel, string>? Sink { get; set; }

    /// <summary>
    /// Performs the <c>Flow</c> operation.
    /// </summary>
    /// <param name="message">The message value.</param>
    public void Flow(string message) =>
        Sink?.Invoke(AgcFloatLogLevel.Flow, message);

    /// <summary>
    /// Performs the <c>Warning</c> operation.
    /// </summary>
    /// <param name="message">The message value.</param>
    public void Warning(string message) =>
        Sink?.Invoke(AgcFloatLogLevel.Warning, message);
}

/// <summary>
/// Immutable AGC configuration. Power values are stored as energy accumulated
/// over <see cref="AgcFloat.SamplesPerChunk"/> samples, matching agcf_descriptor_t.
/// </summary>
public sealed class AgcFloatDescriptor : IDisposable {
    private bool _disposed;

    /// <summary>
    /// Gets or sets the <c>SignalOnPowerThreshold</c> value.
    /// </summary>
    public float SignalOnPowerThreshold { get; internal set; }

    /// <summary>
    /// Gets or sets the <c>SignalOffPowerThreshold</c> value.
    /// </summary>
    public float SignalOffPowerThreshold { get; internal set; }

    /// <summary>
    /// Gets or sets the <c>SignalTargetPower</c> value.
    /// </summary>
    public float SignalTargetPower { get; internal set; }

    /// <summary>
    /// Gets or sets the <c>SignalOnPersistenceCheck</c> value.
    /// </summary>
    public short SignalOnPersistenceCheck { get; internal set; }

    /// <summary>
    /// Gets or sets the <c>SignalOffPersistenceCheck</c> value.
    /// </summary>
    public short SignalOffPersistenceCheck { get; internal set; }

    /// <summary>
    /// Gets or sets the <c>SignalDownPersistenceCheck</c> value.
    /// </summary>
    public short SignalDownPersistenceCheck { get; set; }

    /// <summary>
    /// Performs the <c>Dispose</c> operation.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the <c>Gain</c> value.
    /// </summary>
    public float Gain { get; internal set; } = 1.0f;

    internal float CurrentEnergy { get; set; }

    internal int CurrentSamples { get; set; }

    /// <summary>
    /// Gets or sets the <c>LastPower</c> value.
    /// </summary>
    public float LastPower { get; internal set; }

    internal int SignalOnPersistence { get; set; }

    internal int SignalOffPersistence { get; set; }

    /// <summary>
    /// Gets or sets the <c>Adapt</c> value.
    /// </summary>
    public bool Adapt { get; set; } = true;

    /// <summary>
    /// Gets or sets the <c>Detect</c> value.
    /// </summary>
    public bool Detect { get; set; } = true;

    /// <summary>
    /// Gets or sets the <c>ScaleSignal</c> value.
    /// </summary>
    public bool ScaleSignal { get; set; } = true;

    /// <summary>
    /// Gets or sets the <c>SignalPresent</c> value.
    /// </summary>
    public bool SignalPresent { get; internal set; }

    /// <summary>
    /// Gets the <c>Logging</c> value.
    /// </summary>
    public AgcFloatLogger Logging { get; } = new();

    /// <summary>
    /// Performs the <c>Process</c> operation.
    /// </summary>
    /// <param name="input">The input value.</param>
    /// <param name="output">The output value.</param>
    /// <returns>The result of the operation.</returns>
    public bool Process(ReadOnlySpan<float> input, Span<float> output) {
        ThrowIfDisposed();
        return AgcFloat.Process(this, input, output);
    }

    /// <summary>
    /// Performs the <c>Process</c> operation.
    /// </summary>
    /// <param name="input">The input value.</param>
    /// <param name="output">The output value.</param>
    /// <returns>The result of the operation.</returns>
    public bool Process(ReadOnlySpan<short> input, Span<float> output) {
        ThrowIfDisposed();
        return AgcFloat.ProcessFromInt16(this, input, output);
    }

    /// <summary>
    /// Performs the <c>Release</c> operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public int Release() {
        ThrowIfDisposed();
        return 0;
    }

    /// <summary>
    /// Performs the <c>Dispose</c> operation.
    /// </summary>
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

/// <summary>
/// Represents the <c>AgcFloat</c> type.
/// </summary>
public static class AgcFloat {
    /// <summary>
    /// Defines the <c>SamplesPerChunk</c> constant.
    /// </summary>
    public const int SamplesPerChunk = 40;

    /// <summary>
    /// Defines the <c>DcBlockCoefficient</c> constant.
    /// </summary>
    public const float DcBlockCoefficient = 0.9921875f;

    /// <summary>
    /// Defines the <c>Dbm0MaximumSinePower</c> constant.
    /// </summary>
    public const float Dbm0MaximumSinePower = 3.14f;

    /// <summary>
    /// Defines the <c>Dbm0MaximumPower</c> constant.
    /// </summary>
    public const float Dbm0MaximumPower = 6.16f;

    private const float PcmScale = 32768.0f;

    /// <summary>
    /// Performs the <c>MakeDescriptor</c> operation.
    /// </summary>
    /// <param name="descriptor">The descriptor value.</param>
    /// <param name="signalTargetPowerDbm0">The signal target power dbm0 value.</param>
    /// <param name="signalOnPowerThresholdDbm0">The signal on power threshold dbm0 value.</param>
    /// <param name="signalOffPowerThresholdDbm0">The signal off power threshold dbm0 value.</param>
    /// <param name="signalOnPersistenceCheck">The signal on persistence check value.</param>
    /// <param name="signalOffPersistenceCheck">The signal off persistence check value.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Performs the <c>Initialize</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="descriptor">The descriptor value.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Performs the <c>Process</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="input">The input value.</param>
    /// <param name="output">The output value.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Performs the <c>ProcessFromInt16</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="input">The input value.</param>
    /// <param name="output">The output value.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Performs the <c>CurrentPowerDbm0</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static float CurrentPowerDbm0(AgcFloatState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();

        if (state.LastPower <= 0.0f)
            return float.NegativeInfinity;

        return 10.0f * MathF.Log10(
            state.LastPower / (PcmScale * PcmScale)) +
            Dbm0MaximumPower;
    }

    /// <summary>
    /// Performs the <c>EnergyThresholdDbm0</c> operation.
    /// </summary>
    /// <param name="length">The length value.</param>
    /// <param name="thresholdDbm0">The threshold dbm0 value.</param>
    /// <returns>The result of the operation.</returns>
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
    /// <summary>
    /// Defines the <c>AGC_SAMPLES_PER_CHUNK</c> constant.
    /// </summary>
    public const int AGC_SAMPLES_PER_CHUNK = AgcFloat.SamplesPerChunk;

    /// <summary>
    /// Performs the <c>agcf_make_descriptor</c> operation.
    /// </summary>
    /// <param name="descriptor">The descriptor value.</param>
    /// <param name="signalTargetPower">The signal target power value.</param>
    /// <param name="signalOnPowerThreshold">The signal on power threshold value.</param>
    /// <param name="signalOffPowerThreshold">The signal off power threshold value.</param>
    /// <param name="signalOnPersistenceCheck">The signal on persistence check value.</param>
    /// <param name="signalOffPersistenceCheck">The signal off persistence check value.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Performs the <c>agcf_free_descriptor</c> operation.
    /// </summary>
    /// <param name="descriptor">The descriptor value.</param>
    /// <returns>The result of the operation.</returns>
    public static int agcf_free_descriptor(AgcFloatDescriptor? descriptor) {
        descriptor?.Dispose();
        return 0;
    }

    /// <summary>
    /// Performs the <c>agcf_rx</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="output">The output value.</param>
    /// <param name="input">The input value.</param>
    /// <param name="length">The length value.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Performs the <c>agcf_rx</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="output">The output value.</param>
    /// <param name="input">The input value.</param>
    /// <param name="length">The length value.</param>
    /// <returns>The result of the operation.</returns>
    public static bool agcf_rx(
        AgcFloatState state,
        float[] output,
        float[] input,
        int length) {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        return agcf_rx(state, output.AsSpan(), input.AsSpan(), length);
    }

    /// <summary>
    /// Performs the <c>agcf_from_int16_rx</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="output">The output value.</param>
    /// <param name="input">The input value.</param>
    /// <param name="length">The length value.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Performs the <c>agcf_from_int16_rx</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="output">The output value.</param>
    /// <param name="input">The input value.</param>
    /// <param name="length">The length value.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Performs the <c>agcf_current_power_dbm0</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static float agcf_current_power_dbm0(AgcFloatState state) =>
        AgcFloat.CurrentPowerDbm0(state);

    /// <summary>
    /// Performs the <c>agcf_get_scaling</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static float agcf_get_scaling(AgcFloatState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        return state.Gain;
    }

    /// <summary>
    /// Performs the <c>agcf_set_scaling</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="scaling">The scaling value.</param>
    public static void agcf_set_scaling(AgcFloatState state, float scaling) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        if (!float.IsFinite(scaling))
            throw new ArgumentOutOfRangeException(nameof(scaling));
        state.Gain = scaling;
    }

    /// <summary>
    /// Performs the <c>agcf_set_adaption</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="adapt">The adapt value.</param>
    public static void agcf_set_adaption(AgcFloatState state, bool adapt) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        state.Adapt = adapt;
    }

    /// <summary>
    /// Performs the <c>agcf_get_logging_state</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static AgcFloatLogger agcf_get_logging_state(AgcFloatState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        return state.Logging;
    }

    /// <summary>
    /// Performs the <c>agcf_init</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="descriptor">The descriptor value.</param>
    /// <returns>The result of the operation.</returns>
    public static AgcFloatState agcf_init(
        AgcFloatState? state,
        AgcFloatDescriptor descriptor) =>
        AgcFloat.Initialize(state, descriptor);

    /// <summary>
    /// Performs the <c>agcf_release</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int agcf_release(AgcFloatState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    /// <summary>
    /// Performs the <c>agcf_free</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
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
