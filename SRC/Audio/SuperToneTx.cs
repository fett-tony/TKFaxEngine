/*
 * TKFaxEngine - managed C# port
 *
 * SuperToneTx.cs
 *
 * Combined and ported from super_tone_tx.c and super_tone_tx.h.
 * Implements flexible tree-structured supervisory-tone generation.
 *
 * Original implementation by Steve Underwood.
 * Licensed under the GNU Lesser General Public License version 2.1.
 */

#nullable enable

namespace TKFaxEngine.Audio;

/// <summary>
/// One oscillator descriptor used by a supervisory-tone generation step.
/// </summary>
public readonly record struct SuperToneTxToneDescriptor(int PhaseRate, float Gain) {
    /// <summary>Creates a normal sine oscillator from frequency and dBm0 level.</summary>
    public static SuperToneTxToneDescriptor Create(float frequency, float levelDbm0) {
        if (frequency < 1.0f)
            return default;

        return new SuperToneTxToneDescriptor(
            SuperToneTx.FrequencyToPhaseRate(frequency),
            SuperToneTx.LevelDbm0ToAmplitude(levelDbm0));
    }
}

/// <summary>
/// One node in a tree-structured supervisory-tone cadence description.
/// </summary>
public sealed class SuperToneTxStep {
    private readonly SuperToneTxToneDescriptor[] _tones =
        new SuperToneTxToneDescriptor[SuperToneTx.MaximumTones];

    /// <summary>
    /// Creates a tone or silence step.
    /// </summary>
    /// <param name="frequency1">First frequency in hertz; zero creates a silence step.</param>
    /// <param name="level1Dbm0">First tone level in dBm0.</param>
    /// <param name="frequency2">Optional second frequency in hertz.</param>
    /// <param name="level2Dbm0">Second tone level in dBm0.</param>
    /// <param name="lengthMilliseconds">Step duration; zero gives an infinite tone.</param>
    /// <param name="cycles">Repeat count; zero repeats indefinitely.</param>
    public SuperToneTxStep(
        float frequency1,
        float level1Dbm0,
        float frequency2,
        float level2Dbm0,
        int lengthMilliseconds,
        int cycles) {
        if (lengthMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(lengthMilliseconds));
        if (cycles < 0)
            throw new ArgumentOutOfRangeException(nameof(cycles));

        if (frequency1 >= 1.0f)
            _tones[0] = SuperToneTxToneDescriptor.Create(frequency1, level1Dbm0);

        if (frequency2 >= 1.0f)
            _tones[1] = SuperToneTxToneDescriptor.Create(frequency2, level2Dbm0);

        ToneOn = frequency1 > 0.0f;
        LengthSamples = checked(lengthMilliseconds * SuperToneTx.SampleRate / 1000);
        Cycles = cycles;
    }

    /// <summary>True for an audible step and false for a silence step.</summary>
    public bool ToneOn { get; set; }

    /// <summary>Length of the step in samples. Zero means an infinite tone.</summary>
    public int LengthSamples { get; set; }

    /// <summary>Number of repetitions. Zero means endless repetition.</summary>
    public int Cycles { get; set; }

    /// <summary>Next step at the same tree level.</summary>
    public SuperToneTxStep? Next { get; set; }

    /// <summary>Nested sequence executed before repetition or advancing to <see cref="Next"/>.</summary>
    public SuperToneTxStep? Nest { get; set; }

    /// <summary>
    /// Sets one of the four additive sine oscillators.
    /// </summary>
    public void SetTone(int index, float frequency, float levelDbm0) {
        if ((uint)index >= SuperToneTx.MaximumTones)
            throw new ArgumentOutOfRangeException(nameof(index));

        _tones[index] = SuperToneTxToneDescriptor.Create(frequency, levelDbm0);
        if (index == 0)
            ToneOn = frequency > 0.0f;
    }

    /// <summary>Clears one oscillator.</summary>
    public void ClearTone(int index) {
        if ((uint)index >= SuperToneTx.MaximumTones)
            throw new ArgumentOutOfRangeException(nameof(index));

        _tones[index] = default;
        if (index == 0)
            ToneOn = false;
    }

    /// <summary>
    /// Configures the special two-oscillator amplitude-modulated mode supported
    /// by the native generator.
    /// </summary>
    public void SetAmplitudeModulatedTone(
        float carrierFrequency,
        float carrierLevelDbm0,
        float modulationFrequency,
        float modulationDepthPercent) {
        if (carrierFrequency < 1.0f)
            throw new ArgumentOutOfRangeException(nameof(carrierFrequency));
        if (modulationFrequency < 1.0f)
            throw new ArgumentOutOfRangeException(nameof(modulationFrequency));
        if (modulationDepthPercent < 0.0f)
            throw new ArgumentOutOfRangeException(nameof(modulationDepthPercent));

        int carrierRate = SuperToneTx.FrequencyToPhaseRate(carrierFrequency);
        _tones[0] = new SuperToneTxToneDescriptor(
            unchecked(-carrierRate),
            SuperToneTx.LevelDbm0ToAmplitude(carrierLevelDbm0));

        _tones[1] = new SuperToneTxToneDescriptor(
            SuperToneTx.FrequencyToPhaseRate(modulationFrequency),
            modulationDepthPercent / 100.0f);

        _tones[2] = default;
        _tones[3] = default;
        ToneOn = true;
    }

    internal ReadOnlySpan<SuperToneTxToneDescriptor> Tones => _tones;
}

/// <summary>
/// Generates telephone supervisory tones from a tree of cadence steps.
/// </summary>
public sealed class SuperToneTx {
    public const int SampleRate = 8000;
    public const int MaximumLevels = 4;
    public const int MaximumTones = 4;

    private const double FullPhase = 4294967296.0;
    private const double TwoPi = 2.0 * Math.PI;
    private const float Dbm0MaximumSinePower = 3.14f;

    private readonly SuperToneTxToneDescriptor[] _tones =
        new SuperToneTxToneDescriptor[MaximumTones];

    private readonly uint[] _phases = new uint[MaximumTones];
    private readonly SuperToneTxStep?[] _levels = new SuperToneTxStep?[MaximumLevels];
    private readonly int[] _cycles = new int[MaximumLevels];

    private int _currentPosition;
    private int _level;

    /// <summary>Creates a generator positioned at the first tree step.</summary>
    public SuperToneTx(SuperToneTxStep tree) {
        ArgumentNullException.ThrowIfNull(tree);

        _level = 0;
        _levels[0] = tree;
        _cycles[0] = tree.Cycles;
    }

    /// <summary>True after the complete finite tone tree has been generated.</summary>
    public bool IsComplete => _level < 0
        || _level >= MaximumLevels
        || _levels[0] is null;

    /// <summary>Current nesting level.</summary>
    public int Level => _level;

    /// <summary>Current sample position within the active step.</summary>
    public int CurrentPosition => _currentPosition;

    /// <summary>
    /// Generates up to <paramref name="destination"/>.Length signed 16-bit samples.
    /// </summary>
    /// <returns>The number of generated samples.</returns>
    public int Generate(Span<short> destination) {
        if (_level < 0 || _level >= MaximumLevels)
            return 0;

        int samples = 0;
        int noProgressCount = 0;
        SuperToneTxStep? tree = _levels[_level];

        while (tree is not null && samples < destination.Length) {
            int samplesBeforeStep = samples;
            SuperToneTxStep? treeBeforeStep = tree;
            int levelBeforeStep = _level;

            if (tree.ToneOn) {
                if (_currentPosition == 0)
                    tree.Tones.CopyTo(_tones);

                int length;
                if (tree.LengthSamples == 0) {
                    length = destination.Length - samples;
                    _currentPosition = 1;
                } else {
                    length = tree.LengthSamples - _currentPosition;
                    if (length < 0) {
                        throw new InvalidOperationException(
                            "The current tone position is beyond the configured step length.");
                    }

                    if (length > destination.Length - samples) {
                        length = destination.Length - samples;
                        _currentPosition += length;
                    } else {
                        _currentPosition = 0;
                    }
                }

                if (_tones[0].PhaseRate < 0) {
                    for (int limit = samples + length; samples < limit; samples++) {
                        float carrier = DdsMod(
                            ref _phases[0],
                            unchecked(-_tones[0].PhaseRate),
                            _tones[0].Gain);

                        float modulator = DdsMod(
                            ref _phases[1],
                            _tones[1].PhaseRate,
                            _tones[1].Gain);

                        destination[samples] = RoundAndClamp(carrier * (1.0f + modulator));
                    }
                } else {
                    for (int limit = samples + length; samples < limit; samples++) {
                        float amplitude = 0.0f;

                        for (int tone = 0; tone < MaximumTones; tone++) {
                            SuperToneTxToneDescriptor descriptor = _tones[tone];
                            if (descriptor.PhaseRate == 0)
                                break;

                            amplitude += DdsMod(
                                ref _phases[tone],
                                descriptor.PhaseRate,
                                descriptor.Gain);
                        }

                        destination[samples] = RoundAndClamp(amplitude);
                    }
                }

                if (_currentPosition != 0)
                    return samples;
            } else if (tree.LengthSamples > 0) {
                int length = tree.LengthSamples - _currentPosition;
                if (length < 0) {
                    throw new InvalidOperationException(
                        "The current silence position is beyond the configured step length.");
                }

                if (length > destination.Length - samples) {
                    length = destination.Length - samples;
                    _currentPosition += length;
                } else {
                    _currentPosition = 0;
                }

                destination.Slice(samples, length).Clear();
                samples += length;

                if (_currentPosition != 0)
                    return samples;
            }

            if (tree.Nest is not null) {
                if (_level >= MaximumLevels - 1) {
                    throw new InvalidOperationException(
                        $"The supervisory-tone tree exceeds {MaximumLevels} nesting levels.");
                }

                tree = tree.Nest;
                _levels[++_level] = tree;
                _cycles[_level] = tree.Cycles;
            } else {
                while (tree.Cycles != 0 && --_cycles[_level] <= 0) {
                    tree = tree.Next;
                    if (tree is not null) {
                        _levels[_level] = tree;
                        _cycles[_level] = tree.Cycles;
                        break;
                    }

                    if (_level <= 0) {
                        _levels[0] = null;
                        break;
                    }

                    tree = _levels[--_level];
                }
            }

            if (samples == samplesBeforeStep
                && ReferenceEquals(tree, treeBeforeStep)
                && _level == levelBeforeStep) {
                noProgressCount++;
                if (noProgressCount > MaximumLevels * 2) {
                    throw new InvalidOperationException(
                        "The tone tree contains an endless zero-length silence step.");
                }
            } else {
                noProgressCount = 0;
            }
        }

        return samples;
    }

    /// <summary>Generates audio into a region of an array.</summary>
    public int Generate(short[] destination, int offset, int maximumSamples) {
        ArgumentNullException.ThrowIfNull(destination);
        if (offset < 0 || maximumSamples < 0 || offset > destination.Length - maximumSamples)
            throw new ArgumentOutOfRangeException();

        return Generate(destination.AsSpan(offset, maximumSamples));
    }

    /// <summary>Resets the generator to a new tree and clears oscillator phases.</summary>
    public void Restart(SuperToneTxStep tree) {
        ArgumentNullException.ThrowIfNull(tree);

        Array.Clear(_tones);
        Array.Clear(_phases);
        Array.Clear(_levels);
        Array.Clear(_cycles);

        _currentPosition = 0;
        _level = 0;
        _levels[0] = tree;
        _cycles[0] = tree.Cycles;
    }

    internal static int FrequencyToPhaseRate(float frequency) {
        double phaseRate = frequency * FullPhase / SampleRate;
        long truncated = (long)phaseRate;
        return unchecked((int)truncated);
    }

    internal static float LevelDbm0ToAmplitude(float levelDbm0) {
        double ratio = Math.Pow(10.0, (levelDbm0 - Dbm0MaximumSinePower) / 20.0);
        return (float)(ratio * short.MaxValue);
    }

    private static float DdsMod(ref uint phaseAccumulator, int phaseRate, float scale) {
        double angle = phaseAccumulator * TwoPi / FullPhase;
        float amplitude = (float)(Math.Sin(angle) * scale);
        phaseAccumulator = unchecked(phaseAccumulator + (uint)phaseRate);
        return amplitude;
    }

    private static short RoundAndClamp(float value) {
        int rounded = (int)Math.Round(value, MidpointRounding.ToEven);
        if (rounded > short.MaxValue)
            return short.MaxValue;
        if (rounded < short.MinValue)
            return short.MinValue;

        return (short)rounded;
    }
}
