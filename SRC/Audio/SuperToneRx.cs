/*
 * TKFaxEngine - managed C# port
 *
 * SuperToneRx.cs
 *
 * Combined and ported from super_tone_rx.c and super_tone_rx.h.
 * Implements flexible telephony supervisory-tone detection.
 *
 * Original implementation by Steve Underwood.
 * Licensed under the GNU Lesser General Public License version 2.1.
 */

#nullable enable

namespace TKFaxEngine.Audio;

/// <summary>
/// Callback used to report the start or end of a recognised supervisory tone.
/// A non-negative code identifies a tone descriptor. A code of -1 reports that
/// the previously detected tone is no longer valid.
/// </summary>
public delegate void SuperToneReportHandler(object? userData, int code, int level, int delay);

/// <summary>
/// Callback used to report each completed detected cadence segment.
/// Frequencies are reported as detector indexes, matching the native implementation.
/// </summary>
public delegate void SuperToneSegmentHandler(
    object? userData,
    int frequency1,
    int frequency2,
    int durationMilliseconds);

/// <summary>
/// Describes the tones and cadence patterns monitored by <see cref="SuperToneRx"/>.
/// </summary>
public sealed class SuperToneRxDescriptor {
    internal const int SuperToneBins = 128;
    private const int MaximumPitchMappings = SuperToneBins / 2;

    private readonly List<PitchMapping> _pitches = new();
    private readonly List<GoertzelDescriptor> _detectors = new();
    private readonly List<List<PatternSegment>> _tones = new();

    /// <summary>Number of configured tone patterns.</summary>
    public int ToneCount => _tones.Count;

    /// <summary>Number of independent Goertzel filters required by the descriptor.</summary>
    public int MonitoredFrequencyCount => _detectors.Count;

    /// <summary>
    /// Adds an empty tone pattern and returns its zero-based tone identifier.
    /// </summary>
    public int AddTone() {
        _tones.Add(new List<PatternSegment>());
        return _tones.Count - 1;
    }

    /// <summary>
    /// Adds one cadence element to a tone pattern.
    /// Frequencies at or below zero describe silence or an unused second frequency.
    /// A maximum duration of zero means no upper duration limit.
    /// </summary>
    /// <returns>The zero-based element index within the tone pattern.</returns>
    public int AddElement(
        int tone,
        int frequency1,
        int frequency2,
        int minimumDurationMilliseconds,
        int maximumDurationMilliseconds) {
        if ((uint)tone >= (uint)_tones.Count)
            throw new ArgumentOutOfRangeException(nameof(tone));
        if (minimumDurationMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(minimumDurationMilliseconds));
        if (maximumDurationMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumDurationMilliseconds));
        if (maximumDurationMilliseconds != 0
            && maximumDurationMilliseconds < minimumDurationMilliseconds) {
            throw new ArgumentOutOfRangeException(
                nameof(maximumDurationMilliseconds),
                "The maximum duration must be zero or greater than or equal to the minimum duration.");
        }

        List<PatternSegment> pattern = _tones[tone];
        int step = pattern.Count;

        pattern.Add(new PatternSegment {
            Frequency1 = AddFrequency(frequency1),
            Frequency2 = AddFrequency(frequency2),
            MinimumDurationSamples = checked(minimumDurationMilliseconds * 8),
            MaximumDurationSamples = maximumDurationMilliseconds == 0
                ? int.MaxValue
                : checked(maximumDurationMilliseconds * 8)
        });

        return step;
    }

    internal IReadOnlyList<GoertzelDescriptor> Detectors => _detectors;
    internal IReadOnlyList<List<PatternSegment>> Tones => _tones;

    private int AddFrequency(int frequency) {
        // The public C documentation uses -1 for silence, while the native
        // implementation also treats zero as silence. Supporting both avoids
        // creating a meaningless negative-frequency detector.
        if (frequency <= 0)
            return -1;

        for (int i = 0; i < _pitches.Count; i++) {
            PitchMapping pitch = _pitches[i];
            if (pitch.Frequency == frequency)
                return pitch.DetectorIndex;
        }

        for (int i = 0; i < _pitches.Count; i++) {
            PitchMapping pitch = _pitches[i];
            if (frequency >= pitch.Frequency - 10 && frequency <= pitch.Frequency + 10) {
                EnsurePitchCapacity();

                _pitches.Add(new PitchMapping(frequency, pitch.DetectorIndex));
                _detectors[pitch.DetectorIndex] = new GoertzelDescriptor(
                    (frequency + pitch.Frequency) / 2.0,
                    SuperToneBins);

                return pitch.DetectorIndex;
            }
        }

        EnsurePitchCapacity();

        int detectorIndex = _detectors.Count;
        _pitches.Add(new PitchMapping(frequency, detectorIndex));
        _detectors.Add(new GoertzelDescriptor(frequency, SuperToneBins));
        return detectorIndex;
    }

    private void EnsurePitchCapacity() {
        if (_pitches.Count >= MaximumPitchMappings) {
            throw new InvalidOperationException(
                $"A supervisory-tone descriptor supports at most {MaximumPitchMappings} pitch mappings.");
        }
    }

    private readonly record struct PitchMapping(int Frequency, int DetectorIndex);

    internal sealed class PatternSegment {
        internal int Frequency1;
        internal int Frequency2;
        internal int MinimumDurationSamples;
        internal int MaximumDurationSamples;
    }

    internal readonly struct GoertzelDescriptor {
        internal GoertzelDescriptor(double frequency, int samples) {
            Factor = 2.0 * Math.Cos(2.0 * Math.PI * frequency / SuperToneRx.SampleRate);
            Samples = samples;
        }

        internal double Factor { get; }
        internal int Samples { get; }
    }
}

/// <summary>
/// Flexible detector for telephone supervisory tones such as dial, busy,
/// ringback, reorder and intercept tones.
/// </summary>
public sealed class SuperToneRx {
    public const int SampleRate = 8000;
    public const int BlockSize = SuperToneRxDescriptor.SuperToneBins;

    private const double DetectionThreshold = 2104205.6; // -42 dBm0
    private const double ToneTwist = 3.981;              // 6 dB
    private const double ToneToTotalEnergy = 1.995;      // Native floating-point constant
    private const int ReportedLevel = -10;

    private readonly SuperToneRxDescriptor _descriptor;
    private readonly GoertzelState[] _goertzelStates;
    private readonly RuntimeSegment[] _segments = new RuntimeSegment[11];

    private double _energy;
    private int _blockPosition;
    private int _detectedTone = -1;
    private int _rotation;

    private SuperToneReportHandler _toneCallback;
    private SuperToneSegmentHandler? _segmentCallback;
    private object? _callbackData;

    /// <summary>
    /// Creates a supervisory-tone detector.
    /// </summary>
    public SuperToneRx(
        SuperToneRxDescriptor descriptor,
        SuperToneReportHandler callback,
        object? userData = null) {
        _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        _toneCallback = callback ?? throw new ArgumentNullException(nameof(callback));
        _callbackData = userData;

        IReadOnlyList<SuperToneRxDescriptor.GoertzelDescriptor> detectorDescriptors =
            descriptor.Detectors;

        _goertzelStates = new GoertzelState[detectorDescriptors.Count];
        for (int i = 0; i < _goertzelStates.Length; i++)
            _goertzelStates[i] = new GoertzelState(detectorDescriptors[i]);

        for (int i = 0; i < _segments.Length; i++) {
            _segments[i] = new RuntimeSegment {
                Frequency1 = -1,
                Frequency2 = -1
            };
        }
    }

    /// <summary>Currently recognised tone identifier, or -1 when no tone is recognised.</summary>
    public int DetectedTone => _detectedTone;

    /// <summary>Number of samples accumulated in the current 128-sample analysis block.</summary>
    public int CurrentBlockSamples => _blockPosition;

    /// <summary>Changes the tone start/termination callback.</summary>
    public void SetToneCallback(SuperToneReportHandler callback, object? userData = null) {
        _toneCallback = callback ?? throw new ArgumentNullException(nameof(callback));
        _callbackData = userData;
    }

    /// <summary>Sets or clears the optional completed-segment callback.</summary>
    public void SetSegmentCallback(SuperToneSegmentHandler? callback) {
        _segmentCallback = callback;
    }

    /// <summary>
    /// Processes signed 16-bit, 8000-sample/second audio.
    /// </summary>
    /// <returns>The number of processed samples.</returns>
    public int Process(ReadOnlySpan<short> samples) {
        int offset = 0;

        while (offset < samples.Length) {
            int count = Math.Min(BlockSize - _blockPosition, samples.Length - offset);
            ReadOnlySpan<short> block = samples.Slice(offset, count);

            for (int i = 0; i < _goertzelStates.Length; i++)
                _goertzelStates[i].Update(block);

            for (int i = 0; i < block.Length; i++) {
                double sample = block[i];
                _energy += sample * sample;
            }

            offset += count;
            _blockPosition += count;

            if (_blockPosition >= BlockSize) {
                ProcessCompletedBlock();
                _blockPosition = 0;
            }
        }

        return samples.Length;
    }

    /// <summary>
    /// Processes a region of an audio array.
    /// </summary>
    public int Process(short[] samples, int offset, int count) {
        ArgumentNullException.ThrowIfNull(samples);
        if (offset < 0 || count < 0 || offset > samples.Length - count)
            throw new ArgumentOutOfRangeException();

        return Process(samples.AsSpan(offset, count));
    }

    /// <summary>
    /// Mirrors the native fill-in entry point. The original implementation
    /// intentionally performs no state transition for missing samples.
    /// </summary>
    public int FillIn(int samples) {
        if (samples < 0)
            throw new ArgumentOutOfRangeException(nameof(samples));

        return 0;
    }

    /// <summary>Resets accumulated detection state while retaining the descriptor and callbacks.</summary>
    public void Reset() {
        for (int i = 0; i < _goertzelStates.Length; i++)
            _goertzelStates[i].Reset();

        for (int i = 0; i < _segments.Length; i++) {
            _segments[i] = new RuntimeSegment {
                Frequency1 = -1,
                Frequency2 = -1
            };
        }

        _energy = 0.0;
        _blockPosition = 0;
        _detectedTone = -1;
        _rotation = 0;
    }

    private void ProcessCompletedBlock() {
        int firstFrequency;
        int secondFrequency;

        if (_energy < DetectionThreshold || _goertzelStates.Length == 0) {
            firstFrequency = -1;
            secondFrequency = -1;

            for (int i = 0; i < _goertzelStates.Length; i++)
                _goertzelStates[i].Reset();
        } else if (_goertzelStates.Length == 1) {
            // The native routine assumes at least two bins and would fail to
            // reset a one-bin detector. Handle the intended single-tone case.
            double result = _goertzelStates[0].Result();
            if (result < ToneToTotalEnergy * _energy) {
                firstFrequency = -1;
                secondFrequency = -1;
            } else {
                firstFrequency = 0;
                secondFrequency = -1;
            }
        } else {
            double[] results = new double[_goertzelStates.Length];
            for (int i = 0; i < _goertzelStates.Length; i++)
                results[i] = _goertzelStates[i].Result();

            if (results[0] > results[1]) {
                firstFrequency = 0;
                secondFrequency = 1;
            } else {
                firstFrequency = 1;
                secondFrequency = 0;
            }

            for (int i = 2; i < results.Length; i++) {
                if (results[i] >= results[firstFrequency]) {
                    secondFrequency = firstFrequency;
                    firstFrequency = i;
                } else if (results[i] >= results[secondFrequency]) {
                    secondFrequency = i;
                }
            }

            if (results[firstFrequency] + results[secondFrequency]
                < ToneToTotalEnergy * _energy) {
                firstFrequency = -1;
                secondFrequency = -1;
            } else if (results[firstFrequency] > ToneTwist * results[secondFrequency]) {
                secondFrequency = -1;
            } else if (secondFrequency < firstFrequency) {
                (firstFrequency, secondFrequency) = (secondFrequency, firstFrequency);
            }
        }

        UpdateCadence(firstFrequency, secondFrequency);
        _energy = 0.0;
    }

    private void UpdateCadence(int firstFrequency, int secondFrequency) {
        if (firstFrequency != _segments[10].Frequency1
            || secondFrequency != _segments[10].Frequency2) {
            // Require the new frequency pair in two consecutive blocks before
            // committing the state change.
            _segments[10].Frequency1 = firstFrequency;
            _segments[10].Frequency2 = secondFrequency;
            _segments[9].MinimumDurationBlocks++;
        } else {
            if (firstFrequency != _segments[9].Frequency1
                || secondFrequency != _segments[9].Frequency2) {
                if (_detectedTone >= 0) {
                    IReadOnlyList<SuperToneRxDescriptor.PatternSegment> pattern =
                        _descriptor.Tones[_detectedTone];

                    if (!TestCadence(pattern, -pattern.Count, _segments, _rotation++))
                        ReportToneEnded();
                }

                _segmentCallback?.Invoke(
                    _callbackData,
                    _segments[9].Frequency1,
                    _segments[9].Frequency2,
                    _segments[9].MinimumDurationBlocks * BlockSize / 8);

                Array.Copy(_segments, 1, _segments, 0, 9);
                _segments[9] = new RuntimeSegment {
                    Frequency1 = firstFrequency,
                    Frequency2 = secondFrequency,
                    MinimumDurationBlocks = 1
                };
            } else {
                if (_detectedTone >= 0) {
                    IReadOnlyList<SuperToneRxDescriptor.PatternSegment> pattern =
                        _descriptor.Tones[_detectedTone];

                    if (!TestCadence(pattern, pattern.Count, _segments, _rotation))
                        ReportToneEnded();
                }

                _segments[9].MinimumDurationBlocks++;
            }
        }

        if (_detectedTone < 0) {
            for (int tone = 0; tone < _descriptor.Tones.Count; tone++) {
                IReadOnlyList<SuperToneRxDescriptor.PatternSegment> pattern =
                    _descriptor.Tones[tone];

                if (TestCadence(pattern, pattern.Count, _segments, -1)) {
                    _detectedTone = tone;
                    _rotation = 0;
                    _toneCallback(_callbackData, tone, ReportedLevel, 0);
                    break;
                }
            }
        }
    }

    private void ReportToneEnded() {
        _detectedTone = -1;
        _toneCallback(_callbackData, -1, ReportedLevel, 0);
    }

    private static bool TestCadence(
        IReadOnlyList<SuperToneRxDescriptor.PatternSegment> pattern,
        int steps,
        RuntimeSegment[] test,
        int rotation) {
        if (pattern.Count == 0)
            return false;

        if (rotation >= 0) {
            int patternIndex = 0;

            if (steps < 0) {
                steps = -steps;
                if (steps == 0)
                    return false;

                patternIndex = PositiveModulo(rotation + steps - 2, steps);
                SuperToneRxDescriptor.PatternSegment previousPattern = pattern[patternIndex];
                RuntimeSegment previousTest = test[8];

                if (previousPattern.Frequency1 != previousTest.Frequency1
                    || previousPattern.Frequency2 != previousTest.Frequency2) {
                    return false;
                }

                int durationSamples = previousTest.MinimumDurationBlocks * BlockSize;
                if (previousPattern.MinimumDurationSamples > durationSamples
                    || previousPattern.MaximumDurationSamples < durationSamples) {
                    return false;
                }
            }

            if (steps != 0)
                patternIndex = PositiveModulo(rotation + steps - 1, steps);

            SuperToneRxDescriptor.PatternSegment currentPattern = pattern[patternIndex];
            RuntimeSegment currentTest = test[9];

            if (currentPattern.Frequency1 != currentTest.Frequency1
                || currentPattern.Frequency2 != currentTest.Frequency2) {
                return false;
            }

            if (currentPattern.MaximumDurationSamples
                < currentTest.MinimumDurationBlocks * BlockSize) {
                return false;
            }
        } else {
            if (steps <= 0 || steps > pattern.Count || steps > 10)
                return false;

            for (int i = 0; i < steps; i++) {
                int testIndex = i + 10 - steps;
                SuperToneRxDescriptor.PatternSegment expected = pattern[i];
                RuntimeSegment actual = test[testIndex];

                if (expected.Frequency1 != actual.Frequency1
                    || expected.Frequency2 != actual.Frequency2) {
                    return false;
                }

                int durationSamples = actual.MinimumDurationBlocks * BlockSize;
                if (expected.MinimumDurationSamples > durationSamples
                    || expected.MaximumDurationSamples < durationSamples) {
                    return false;
                }
            }
        }

        return true;
    }

    private static int PositiveModulo(int value, int modulus) {
        int result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    private sealed class GoertzelState {
        private readonly double _factor;
        private readonly int _samples;

        private double _v2;
        private double _v3;
        private int _currentSample;

        internal GoertzelState(SuperToneRxDescriptor.GoertzelDescriptor descriptor) {
            _factor = descriptor.Factor;
            _samples = descriptor.Samples;
        }

        internal void Update(ReadOnlySpan<short> samples) {
            int usable = Math.Min(samples.Length, _samples - _currentSample);

            for (int i = 0; i < usable; i++) {
                double v1 = _v2;
                _v2 = _v3;
                _v3 = _factor * _v2 - v1 + samples[i];
            }

            _currentSample += usable;
        }

        internal double Result() {
            double v1 = _v2;
            _v2 = _v3;
            _v3 = _factor * _v2 - v1;

            double result = _v3 * _v3 + _v2 * _v2 - _v2 * _v3 * _factor;
            result *= 2.0;

            Reset();
            return result;
        }

        internal void Reset() {
            _v2 = 0.0;
            _v3 = 0.0;
            _currentSample = 0;
        }
    }

    private sealed class RuntimeSegment {
        internal int Frequency1;
        internal int Frequency2;
        internal int MinimumDurationBlocks;
    }
}
