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

    public int StartingPhaseIncrement { get; internal set; }
    public int PhaseIncrementStep { get; internal set; }
    public int Scale { get; internal set; }
    public int Duration { get; internal set; }
    public bool Repeating { get; internal set; }
    public int Position { get; internal set; }
    public int CurrentPhaseIncrement { get; internal set; }
    public uint Phase { get; internal set; }

    public float CurrentFrequency =>
        SweptToneDds.Frequency(CurrentPhaseIncrement);

    public bool Completed =>
        !Repeating && Position >= Duration;

    public int Generate(Span<short> samples) {
        ThrowIfDisposed();
        return SweptTone.Generate(this, samples);
    }

    public int Generate(short[] samples, int offset, int length) {
        ArgumentNullException.ThrowIfNull(samples);
        return Generate(samples.AsSpan(offset, length));
    }

    public void Restart() {
        ThrowIfDisposed();
        Position = 0;
        CurrentPhaseIncrement = StartingPhaseIncrement;
    }

    public int Release() {
        ThrowIfDisposed();
        return 0;
    }

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
    public const int SampleRate = 8000;

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

    public static int Generate(
        SweptToneState state,
        short[] samples,
        int offset,
        int length) {
        ArgumentNullException.ThrowIfNull(samples);
        return Generate(state, samples.AsSpan(offset, length));
    }

    public static float CurrentFrequency(SweptToneState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        return state.CurrentFrequency;
    }

    public static int Release(SweptToneState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    public static int Free(SweptToneState? state) {
        state?.Dispose();
        return 0;
    }
}

/// <summary>
/// C-compatible facade retaining the original exported function names.
/// </summary>
public static class SweptToneApi {
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

    public static int swept_tone(
        SweptToneState state,
        Span<short> amplitude) {
        return SweptTone.Generate(state, amplitude);
    }

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

    public static float swept_tone_current_frequency(
        SweptToneState state) {
        return SweptTone.CurrentFrequency(state);
    }

    public static int swept_tone_release(
        SweptToneState state) {
        return SweptTone.Release(state);
    }

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
