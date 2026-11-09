/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * SweptTone.cs - managed C# port of swept_tone.c and swept_tone.h
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2009 Steve Underwood.
 *
 * Distributed under the GNU Lesser General Public License version 2.1,
 * matching the original source files.
 */

#nullable enable

namespace TKFaxEngine.Audio;

/// <summary>
/// State of an 8 kHz signed 16-bit PCM swept-tone generator.
/// </summary>
public sealed class SweptToneState : IDisposable {
    private bool _disposed;

    /// <summary>
    /// Gets or sets the <c>StartingPhaseIncrement</c> value.
    /// </summary>
    public int StartingPhaseIncrement { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>PhaseIncrementStep</c> value.
    /// </summary>
    public int PhaseIncrementStep { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>Scale</c> value.
    /// </summary>
    public int Scale { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>Duration</c> value.
    /// </summary>
    public int Duration { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>Repeating</c> value.
    /// </summary>
    public bool Repeating { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>Position</c> value.
    /// </summary>
    public int Position { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>CurrentPhaseIncrement</c> value.
    /// </summary>
    public int CurrentPhaseIncrement { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>Phase</c> value.
    /// </summary>
    public uint Phase { get; internal set; }

    /// <summary>
    /// Gets the <c>CurrentFrequency</c> value.
    /// </summary>
    public float CurrentFrequency =>
        SweptToneDds.Frequency(CurrentPhaseIncrement);

    /// <summary>
    /// Gets the <c>Completed</c> value.
    /// </summary>
    public bool Completed =>
        !Repeating && Position >= Duration;

    /// <summary>
    /// Performs the <c>Generate</c> operation.
    /// </summary>
    /// <param name="samples">The samples value.</param>
    /// <returns>The result of the operation.</returns>
    public int Generate(Span<short> samples) {
        ThrowIfDisposed();
        return SweptTone.Generate(this, samples);
    }

    /// <summary>
    /// Performs the <c>Generate</c> operation.
    /// </summary>
    /// <param name="samples">The samples value.</param>
    /// <param name="offset">The offset value.</param>
    /// <param name="length">The length value.</param>
    /// <returns>The result of the operation.</returns>
    public int Generate(short[] samples, int offset, int length) {
        ArgumentNullException.ThrowIfNull(samples);
        return Generate(samples.AsSpan(offset, length));
    }

    /// <summary>
    /// Performs the <c>Restart</c> operation.
    /// </summary>
    public void Restart() {
        ThrowIfDisposed();
        Position = 0;
        CurrentPhaseIncrement = StartingPhaseIncrement;
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
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    internal void Initialize(
        float startFrequency,
        float endFrequency,
        float levelDbm0,
        int duration,
        bool repeating) {
        ThrowIfDisposed();

        if (!float.IsFinite(startFrequency))
            throw new ArgumentOutOfRangeException(nameof(startFrequency));
        if (!float.IsFinite(endFrequency))
            throw new ArgumentOutOfRangeException(nameof(endFrequency));
        if (!float.IsFinite(levelDbm0))
            throw new ArgumentOutOfRangeException(nameof(levelDbm0));
        if (duration <= 0)
            throw new ArgumentOutOfRangeException(nameof(duration));

        StartingPhaseIncrement =
            SweptToneDds.PhaseRate(startFrequency);
        CurrentPhaseIncrement = StartingPhaseIncrement;
        PhaseIncrementStep = SweptToneDds.PhaseRate(
            (endFrequency - startFrequency) / duration);
        Scale = SweptToneDds.ScalingDbm0(levelDbm0);
        Duration = duration;
        Repeating = repeating;
        Position = 0;
        Phase = 0;
    }

    internal void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

/// <summary>
/// Linear frequency-sweep generator compatible with swept_tone.c.
/// </summary>
public static class SweptTone {
    /// <summary>
    /// Defines the <c>SampleRate</c> constant.
    /// </summary>
    public const int SampleRate = 8000;

    /// <summary>
    /// Performs the <c>Initialize</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="startFrequency">The start frequency value.</param>
    /// <param name="endFrequency">The end frequency value.</param>
    /// <param name="levelDbm0">The level dbm0 value.</param>
    /// <param name="duration">The duration value.</param>
    /// <param name="repeating">The repeating value.</param>
    /// <returns>The result of the operation.</returns>
    public static SweptToneState Initialize(
        SweptToneState? state,
        float startFrequency,
        float endFrequency,
        float levelDbm0,
        int duration,
        bool repeating) {
        state ??= new SweptToneState();
        state.Initialize(
            startFrequency,
            endFrequency,
            levelDbm0,
            duration,
            repeating);
        return state;
    }

    /// <summary>
    /// Performs the <c>Generate</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="samples">The samples value.</param>
    /// <returns>The result of the operation.</returns>
    public static int Generate(
        SweptToneState state,
        Span<short> samples) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();

        int length = 0;

        while (length < samples.Length) {
            int chunkLength = samples.Length - length;
            int remaining = state.Duration - state.Position;

            if (chunkLength > remaining)
                chunkLength = remaining;

            for (int index = length;
                 index < length + chunkLength;
                 index++) {
                uint phase = state.Phase;
                int sample = SweptToneDds.Next(
                    ref phase,
                    state.CurrentPhaseIncrement);
                state.Phase = phase;

                samples[index] = unchecked((short)(
                    (sample * state.Scale) >> 15));

                state.CurrentPhaseIncrement = unchecked(
                    state.CurrentPhaseIncrement +
                    state.PhaseIncrementStep);
            }

            length += chunkLength;
            state.Position += chunkLength;

            if (state.Position >= state.Duration) {
                if (!state.Repeating)
                    break;

                state.Position = 0;
                state.CurrentPhaseIncrement =
                    state.StartingPhaseIncrement;
            }
        }

        return length;
    }

    /// <summary>
    /// Performs the <c>Generate</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="samples">The samples value.</param>
    /// <param name="offset">The offset value.</param>
    /// <param name="length">The length value.</param>
    /// <returns>The result of the operation.</returns>
    public static int Generate(
        SweptToneState state,
        short[] samples,
        int offset,
        int length) {
        ArgumentNullException.ThrowIfNull(samples);
        return Generate(state, samples.AsSpan(offset, length));
    }

    /// <summary>
    /// Performs the <c>CurrentFrequency</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static float CurrentFrequency(SweptToneState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        return state.CurrentFrequency;
    }

    /// <summary>
    /// Performs the <c>Release</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int Release(SweptToneState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    /// <summary>
    /// Performs the <c>Free</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int Free(SweptToneState? state) {
        state?.Dispose();
        return 0;
    }
}

/// <summary>
/// C-compatible facade retaining the original exported function names.
/// </summary>
public static class SweptToneApi {
    /// <summary>
    /// Performs the <c>swept_tone_init</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="start">The start value.</param>
    /// <param name="end">The end value.</param>
    /// <param name="level">The level value.</param>
    /// <param name="duration">The duration value.</param>
    /// <param name="repeating">The repeating value.</param>
    /// <returns>The result of the operation.</returns>
    public static SweptToneState swept_tone_init(
        SweptToneState? state,
        float start,
        float end,
        float level,
        int duration,
        int repeating) {
        return SweptTone.Initialize(
            state,
            start,
            end,
            level,
            duration,
            repeating != 0);
    }

    /// <summary>
    /// Performs the <c>swept_tone</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="amplitude">The amplitude value.</param>
    /// <returns>The result of the operation.</returns>
    public static int swept_tone(
        SweptToneState state,
        Span<short> amplitude) {
        return SweptTone.Generate(state, amplitude);
    }

    /// <summary>
    /// Performs the <c>swept_tone</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="amplitude">The amplitude value.</param>
    /// <param name="length">The length value.</param>
    /// <returns>The result of the operation.</returns>
    public static int swept_tone(
        SweptToneState state,
        short[] amplitude,
        int length) {
        ArgumentNullException.ThrowIfNull(amplitude);

        if ((uint)length > (uint)amplitude.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        return SweptTone.Generate(
            state,
            amplitude.AsSpan(0, length));
    }

    /// <summary>
    /// Performs the <c>swept_tone_current_frequency</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static float swept_tone_current_frequency(
        SweptToneState state) {
        return SweptTone.CurrentFrequency(state);
    }

    /// <summary>
    /// Performs the <c>swept_tone_release</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int swept_tone_release(
        SweptToneState state) {
        return SweptTone.Release(state);
    }

    /// <summary>
    /// Performs the <c>swept_tone_free</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int swept_tone_free(
        SweptToneState? state) {
        return SweptTone.Free(state);
    }
}

/// <summary>
/// Integer DDS matching the phase and level conventions used by dds_int.c.
/// </summary>
internal static class SweptToneDds {
    private const int SampleRate = SweptTone.SampleRate;
    private const int DdsSteps = 256;
    private const int DdsShift = 22;
    private const float Dbm0MaxSinePower = 3.14f;

    private static readonly short[] SineTable =
        BuildQuarterWaveTable();

    internal static int PhaseRate(float frequency) {
        return unchecked((int)(
            frequency * 65536.0f * 65536.0f / SampleRate));
    }

    internal static float Frequency(int phaseRate) {
        return (float)(
            (double)phaseRate * SampleRate / 4294967296.0);
    }

    internal static int ScalingDbm0(float level) {
        float ratio = MathF.Pow(
            10.0f,
            (level - Dbm0MaxSinePower) / 20.0f);

        return unchecked((short)(ratio * 32767.0f));
    }

    internal static short Lookup(uint phase) {
        phase >>= DdsShift;
        uint step = phase & (DdsSteps - 1u);

        if ((phase & DdsSteps) != 0)
            step = DdsSteps - step;

        short amplitude = SineTable[(int)step];

        if ((phase & (2u * DdsSteps)) != 0)
            amplitude = unchecked((short)-amplitude);

        return amplitude;
    }

    internal static short Next(
        ref uint phaseAccumulator,
        int phaseRate) {
        short amplitude = Lookup(phaseAccumulator);
        phaseAccumulator = unchecked(
            phaseAccumulator + (uint)phaseRate);
        return amplitude;
    }

    private static short[] BuildQuarterWaveTable() {
        short[] table = new short[DdsSteps + 1];

        for (int index = 0; index <= DdsSteps; index++) {
            double radians =
                (Math.PI / 2.0) * index / DdsSteps;

            table[index] = checked((short)Math.Floor(
                32767.0 * Math.Sin(radians) + 0.5));
        }

        return table;
    }
}
