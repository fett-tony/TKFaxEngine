/*
 * TKFaxEngine - managed C# port
 *
 * BellR2Mf.cs
 *
 * Combined and ported from bell_r2_mf.c and bell_r2_mf.h.
 * Implements Bell MF and MFC/R2 tone generation and detection.
 *
 * Original implementation by Steve Underwood.
 * Licensed under the GNU Lesser General Public License version 2.1.
 */

#nullable enable

namespace TKFaxEngine.Audio;

/// <summary>Callback used to deliver one or more detected Bell MF digits.</summary>
public delegate void BellMfDigitsHandler(object? userData, string digits);

/// <summary>Callback used to report an R2 MF tone state change.</summary>
public delegate void R2MfToneReportHandler(object? userData, int code, int level, int delay);

internal readonly record struct BellR2ToneSpec(
    int Frequency1,
    int Frequency2,
    double Level1Dbm0,
    double Level2Dbm0,
    int OnTimeMilliseconds,
    int OffTimeMilliseconds);

internal static class BellR2MfTables {
    internal const int SampleRate = 8000;
    internal const int MaximumBellDigits = 128;
    internal const double MaximumSinePowerDbm0 = 3.14;
    internal const double TwoPi = 2.0 * Math.PI;

    internal const string BellCodes = "1234567890CA*B#";
    internal const string R2Codes = "1234567890BCDEF";

    internal const string BellPositions = "1247C-358A--69*---0B----#";
    internal const string R2Positions = "1247B-358C--69D---0E----F";

    internal static readonly BellR2ToneSpec[] BellTones =
    {
        new( 700,  900, -7, -7,  68, 68),
        new( 700, 1100, -7, -7,  68, 68),
        new( 900, 1100, -7, -7,  68, 68),
        new( 700, 1300, -7, -7,  68, 68),
        new( 900, 1300, -7, -7,  68, 68),
        new(1100, 1300, -7, -7,  68, 68),
        new( 700, 1500, -7, -7,  68, 68),
        new( 900, 1500, -7, -7,  68, 68),
        new(1100, 1500, -7, -7,  68, 68),
        new(1300, 1500, -7, -7,  68, 68),
        new( 700, 1700, -7, -7,  68, 68),
        new( 900, 1700, -7, -7,  68, 68),
        new(1100, 1700, -7, -7, 100, 68),
        new(1300, 1700, -7, -7,  68, 68),
        new(1500, 1700, -7, -7,  68, 68)
    };

    internal static readonly BellR2ToneSpec[] R2ForwardTones =
    {
        new(1380, 1500, -11, -11, 1, 0),
        new(1380, 1620, -11, -11, 1, 0),
        new(1500, 1620, -11, -11, 1, 0),
        new(1380, 1740, -11, -11, 1, 0),
        new(1500, 1740, -11, -11, 1, 0),
        new(1620, 1740, -11, -11, 1, 0),
        new(1380, 1860, -11, -11, 1, 0),
        new(1500, 1860, -11, -11, 1, 0),
        new(1620, 1860, -11, -11, 1, 0),
        new(1740, 1860, -11, -11, 1, 0),
        new(1380, 1980, -11, -11, 1, 0),
        new(1500, 1980, -11, -11, 1, 0),
        new(1620, 1980, -11, -11, 1, 0),
        new(1740, 1980, -11, -11, 1, 0),
        new(1860, 1980, -11, -11, 1, 0)
    };

    internal static readonly BellR2ToneSpec[] R2BackwardTones =
    {
        new(1140, 1020, -11, -11, 1, 0),
        new(1140,  900, -11, -11, 1, 0),
        new(1020,  900, -11, -11, 1, 0),
        new(1140,  780, -11, -11, 1, 0),
        new(1020,  780, -11, -11, 1, 0),
        new( 900,  780, -11, -11, 1, 0),
        new(1140,  660, -11, -11, 1, 0),
        new(1020,  660, -11, -11, 1, 0),
        new( 900,  660, -11, -11, 1, 0),
        new( 780,  660, -11, -11, 1, 0),
        new(1140,  540, -11, -11, 1, 0),
        new(1020,  540, -11, -11, 1, 0),
        new( 900,  540, -11, -11, 1, 0),
        new( 780,  540, -11, -11, 1, 0),
        new( 660,  540, -11, -11, 1, 0)
    };

    internal static readonly int[] BellFrequencies = { 700, 900, 1100, 1300, 1500, 1700 };
    internal static readonly int[] R2ForwardFrequencies = { 1380, 1500, 1620, 1740, 1860, 1980 };
    internal static readonly int[] R2BackwardFrequencies = { 1140, 1020, 900, 780, 660, 540 };

    internal static bool TryGetBellTone(char digit, out BellR2ToneSpec tone) {
        int index = BellCodes.IndexOf(digit);
        if (index >= 0) {
            tone = BellTones[index];
            return true;
        }

        tone = default;
        return false;
    }

    internal static bool TryGetR2Tone(bool forward, char digit, out BellR2ToneSpec tone) {
        int index = R2Codes.IndexOf(digit);
        if (index >= 0) {
            tone = forward ? R2ForwardTones[index] : R2BackwardTones[index];
            return true;
        }

        tone = default;
        return false;
    }

    internal static double LevelToAmplitude(double levelDbm0) {
        return Math.Pow(10.0, (levelDbm0 - MaximumSinePowerDbm0) / 20.0) * short.MaxValue;
    }

    internal static short MixAndClamp(double value) {
        if (value >= short.MaxValue)
            return short.MaxValue;
        if (value <= short.MinValue)
            return short.MinValue;

        return (short)Math.Round(value, MidpointRounding.ToEven);
    }
}

internal sealed class BellR2Goertzel {
    private readonly double _coefficient;
    private double _q1;
    private double _q2;

    internal BellR2Goertzel(double frequency, int blockLength) {
        _coefficient = 2.0 * Math.Cos(BellR2MfTables.TwoPi * frequency / BellR2MfTables.SampleRate);
        BlockLength = blockLength;
    }

    internal int BlockLength { get; }

    internal void Add(short sample) {
        double q0 = sample + _coefficient * _q1 - _q2;
        _q2 = _q1;
        _q1 = q0;
    }

    internal double GetEnergyAndReset() {
        // Push a zero through the recursive side, exactly as spanDSP's
        // goertzel_result() does before calculating the final energy.
        double previousQ2 = _q2;
        _q2 = _q1;
        _q1 = _coefficient * _q2 - previousQ2;

        double energy = (
            _q1 * _q1 +
            _q2 * _q2 -
            _q2 * _q1 * _coefficient) * 2.0;

        _q1 = 0.0;
        _q2 = 0.0;
        return energy < 0.0 ? 0.0 : energy;
    }

    internal void Reset() {
        _q1 = 0.0;
        _q2 = 0.0;
    }
}

/// <summary>
/// Bell MF tone generator with the native 128-character all-or-nothing input queue.
/// </summary>
public sealed class BellMfTx {
    private readonly Queue<char> _queue = new(BellR2MfTables.MaximumBellDigits);

    private BellR2ToneSpec _tone;
    private bool _active;
    private int _toneSamplesRemaining;
    private int _silenceSamplesRemaining;
    private double _phase1;
    private double _phase2;

    /// <summary>Number of characters currently waiting in the input queue.</summary>
    public int QueuedDigits => _queue.Count;

    /// <summary>
    /// Adds digits to the input queue. Returns zero when the complete string fits;
    /// otherwise returns the number of characters that would not fit and queues nothing.
    /// Invalid characters are retained and skipped during generation, matching the native code.
    /// </summary>
    public int Put(string digits, int length = -1) {
        ArgumentNullException.ThrowIfNull(digits);

        if (length < 0)
            length = digits.Length;
        if (length > digits.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        int free = BellR2MfTables.MaximumBellDigits - _queue.Count;
        if (free < length)
            return length - free;

        for (int i = 0; i < length; i++)
            _queue.Enqueue(digits[i]);

        return 0;
    }

    /// <summary>
    /// Generates as many samples as possible. A return value smaller than the
    /// destination length means that the digit queue and active tone are empty.
    /// </summary>
    public int Generate(Span<short> destination) {
        int written = 0;

        while (written < destination.Length) {
            if (!_active && !StartNextTone())
                break;

            if (_toneSamplesRemaining > 0) {
                int count = Math.Min(_toneSamplesRemaining, destination.Length - written);
                GenerateTone(destination.Slice(written, count));
                written += count;
                _toneSamplesRemaining -= count;

                if (_toneSamplesRemaining > 0)
                    break;
            }

            if (_silenceSamplesRemaining > 0 && written < destination.Length) {
                int count = Math.Min(_silenceSamplesRemaining, destination.Length - written);
                destination.Slice(written, count).Clear();
                written += count;
                _silenceSamplesRemaining -= count;

                if (_silenceSamplesRemaining > 0)
                    break;
            }

            if (_toneSamplesRemaining == 0 && _silenceSamplesRemaining == 0)
                _active = false;
        }

        return written;
    }

    /// <summary>Clears queued and active digits.</summary>
    public void Reset() {
        _queue.Clear();
        _active = false;
        _toneSamplesRemaining = 0;
        _silenceSamplesRemaining = 0;
        _phase1 = 0.0;
        _phase2 = 0.0;
    }

    private bool StartNextTone() {
        while (_queue.Count > 0) {
            char digit = _queue.Dequeue();
            if (!BellR2MfTables.TryGetBellTone(digit, out _tone))
                continue;

            _toneSamplesRemaining = _tone.OnTimeMilliseconds * BellR2MfTables.SampleRate / 1000;
            _silenceSamplesRemaining = _tone.OffTimeMilliseconds * BellR2MfTables.SampleRate / 1000;
            _phase1 = 0.0;
            _phase2 = 0.0;
            _active = true;
            return true;
        }

        return false;
    }

    private void GenerateTone(Span<short> destination) {
        double amplitude1 = BellR2MfTables.LevelToAmplitude(_tone.Level1Dbm0);
        double amplitude2 = BellR2MfTables.LevelToAmplitude(_tone.Level2Dbm0);
        double increment1 = BellR2MfTables.TwoPi * _tone.Frequency1 / BellR2MfTables.SampleRate;
        double increment2 = BellR2MfTables.TwoPi * _tone.Frequency2 / BellR2MfTables.SampleRate;

        for (int i = 0; i < destination.Length; i++) {
            double sample = Math.Sin(_phase1) * amplitude1 + Math.Sin(_phase2) * amplitude2;
            destination[i] = BellR2MfTables.MixAndClamp(sample);

            _phase1 += increment1;
            _phase2 += increment2;
            if (_phase1 >= BellR2MfTables.TwoPi)
                _phase1 -= BellR2MfTables.TwoPi;
            if (_phase2 >= BellR2MfTables.TwoPi)
                _phase2 -= BellR2MfTables.TwoPi;
        }
    }
}

/// <summary>Continuous forward or backward MFC/R2 tone generator.</summary>
public sealed class R2MfTx {
    private readonly bool _forward;
    private BellR2ToneSpec _tone;
    private char _digit;
    private double _phase1;
    private double _phase2;

    public R2MfTx(bool forward) {
        _forward = forward;
    }

    /// <summary>True for forward R2 signals, false for backward signals.</summary>
    public bool Forward => _forward;

    /// <summary>The currently generated code, or NUL for silence.</summary>
    public char CurrentDigit => _digit;

    /// <summary>
    /// Selects a continuous R2 tone. An invalid or NUL digit selects silence.
    /// </summary>
    public int Put(char digit) {
        if (digit != '\0' && BellR2MfTables.TryGetR2Tone(_forward, digit, out _tone)) {
            _digit = digit;
            _phase1 = 0.0;
            _phase2 = 0.0;
        } else {
            _digit = '\0';
        }

        return 0;
    }

    /// <summary>Generates exactly the requested number of tone or silence samples.</summary>
    public int Generate(Span<short> destination) {
        if (_digit == '\0') {
            destination.Clear();
            return destination.Length;
        }

        double amplitude1 = BellR2MfTables.LevelToAmplitude(_tone.Level1Dbm0);
        double amplitude2 = BellR2MfTables.LevelToAmplitude(_tone.Level2Dbm0);
        double increment1 = BellR2MfTables.TwoPi * _tone.Frequency1 / BellR2MfTables.SampleRate;
        double increment2 = BellR2MfTables.TwoPi * _tone.Frequency2 / BellR2MfTables.SampleRate;

        for (int i = 0; i < destination.Length; i++) {
            double sample = Math.Sin(_phase1) * amplitude1 + Math.Sin(_phase2) * amplitude2;
            destination[i] = BellR2MfTables.MixAndClamp(sample);

            _phase1 += increment1;
            _phase2 += increment2;
            if (_phase1 >= BellR2MfTables.TwoPi)
                _phase1 -= BellR2MfTables.TwoPi;
            if (_phase2 >= BellR2MfTables.TwoPi)
                _phase2 -= BellR2MfTables.TwoPi;
        }

        return destination.Length;
    }
}

/// <summary>
/// Bell MF receiver compliant with the original block, twist, relative-peak,
/// persistence and KP-duration checks.
/// </summary>
public sealed class BellMfRx {
    private const int SamplesPerBlock = 120;
    private const double DetectionThreshold = 3343803100.0;
    private const double Twist = 3.981;
    private const double RelativePeak = 12.589;

    private readonly BellR2Goertzel[] _filters = new BellR2Goertzel[6];
    private readonly char[] _hits = new char[5];
    private readonly List<char> _digits = new(BellR2MfTables.MaximumBellDigits);

    private BellMfDigitsHandler? _callback;
    private object? _callbackData;
    private int _currentSample;

    public BellMfRx(BellMfDigitsHandler? callback = null, object? userData = null) {
        for (int i = 0; i < _filters.Length; i++)
            _filters[i] = new BellR2Goertzel(BellR2MfTables.BellFrequencies[i], SamplesPerBlock);

        _callback = callback;
        _callbackData = userData;
    }

    /// <summary>Number of digits discarded because the receive buffer was full.</summary>
    public int LostDigits { get; private set; }

    /// <summary>Number of buffered digits waiting for <see cref="GetDigits"/>.</summary>
    public int BufferedDigits => _digits.Count;

    public void SetCallback(BellMfDigitsHandler? callback, object? userData = null) {
        _callback = callback;
        _callbackData = userData;
    }

    /// <summary>Processes a block of 8 kHz signed 16-bit audio samples.</summary>
    public int Process(ReadOnlySpan<short> samples) {
        for (int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex++) {
            short sample = samples[sampleIndex];
            for (int i = 0; i < _filters.Length; i++)
                _filters[i].Add(sample);

            _currentSample++;
            if (_currentSample < SamplesPerBlock)
                continue;

            ProcessCompletedBlock();
            _currentSample = 0;
        }

        if (_digits.Count > 0 && _callback is not null) {
            string detected = new(_digits.ToArray());
            _callback(_callbackData, detected);
            _digits.Clear();
        }

        return 0;
    }

    /// <summary>Returns and removes up to <paramref name="maximum"/> buffered digits.</summary>
    public string GetDigits(int maximum = BellR2MfTables.MaximumBellDigits) {
        if (maximum < 0)
            throw new ArgumentOutOfRangeException(nameof(maximum));

        int count = Math.Min(maximum, _digits.Count);
        if (count == 0)
            return string.Empty;

        char[] result = _digits.GetRange(0, count).ToArray();
        _digits.RemoveRange(0, count);
        return new string(result);
    }

    public void Reset() {
        foreach (BellR2Goertzel filter in _filters)
            filter.Reset();

        Array.Clear(_hits);
        _digits.Clear();
        _currentSample = 0;
        LostDigits = 0;
    }

    private void ProcessCompletedBlock() {
        Span<double> energy = stackalloc double[6];
        for (int i = 0; i < _filters.Length; i++)
            energy[i] = _filters[i].GetEnergyAndReset();

        FindTwoLargest(energy, out int best, out int secondBest);

        char hit = '\0';
        if (energy[best] >= DetectionThreshold
            && energy[secondBest] >= DetectionThreshold
            && energy[best] < energy[secondBest] * Twist
            && energy[best] * Twist > energy[secondBest]) {
            bool relativePeakPassed = true;
            for (int i = 0; i < energy.Length; i++) {
                if (i == best || i == secondBest)
                    continue;

                if (energy[i] * RelativePeak >= energy[secondBest]) {
                    relativePeakPassed = false;
                    break;
                }
            }

            if (relativePeakPassed) {
                if (secondBest < best)
                    (best, secondBest) = (secondBest, best);

                int position = best * 5 + secondBest - 1;
                if ((uint)position < BellR2MfTables.BellPositions.Length)
                    hit = BellR2MfTables.BellPositions[position];

                if (hit == '-')
                    hit = '\0';
            }
        }

        if (hit != '\0'
            && hit == _hits[4]
            && hit == _hits[3]
            && ((hit != '*' && hit != _hits[2] && hit != _hits[1])
                || (hit == '*' && hit == _hits[2] && hit != _hits[1] && hit != _hits[0]))) {
            if (_digits.Count < BellR2MfTables.MaximumBellDigits)
                _digits.Add(hit);
            else
                LostDigits++;
        }

        _hits[0] = _hits[1];
        _hits[1] = _hits[2];
        _hits[2] = _hits[3];
        _hits[3] = _hits[4];
        _hits[4] = hit;
    }

    private static void FindTwoLargest(ReadOnlySpan<double> values, out int best, out int secondBest) {
        if (values[0] > values[1]) {
            best = 0;
            secondBest = 1;
        } else {
            best = 1;
            secondBest = 0;
        }

        for (int i = 2; i < values.Length; i++) {
            if (values[i] >= values[best]) {
                secondBest = best;
                best = i;
            } else if (values[i] >= values[secondBest]) {
                secondBest = i;
            }
        }
    }
}

/// <summary>
/// Forward or backward MFC/R2 receiver using the original 133-sample
/// detection block and level, twist and relative-peak checks.
/// </summary>
public sealed class R2MfRx {
    private const int SamplesPerBlock = 133;
    private const double DetectionThreshold = 1031766650.0;
    private const double Twist = 5.012;
    private const double RelativePeak = 12.589;

    private readonly bool _forward;
    private readonly BellR2Goertzel[] _filters = new BellR2Goertzel[6];

    private R2MfToneReportHandler? _callback;
    private object? _callbackData;
    private int _currentSample;
    private char _currentDigit;

    public R2MfRx(
        bool forward,
        R2MfToneReportHandler? callback = null,
        object? userData = null) {
        _forward = forward;
        _callback = callback;
        _callbackData = userData;

        int[] frequencies = forward
            ? BellR2MfTables.R2ForwardFrequencies
            : BellR2MfTables.R2BackwardFrequencies;

        for (int i = 0; i < _filters.Length; i++)
            _filters[i] = new BellR2Goertzel(frequencies[i], SamplesPerBlock);
    }

    /// <summary>True for a forward receiver, false for a backward receiver.</summary>
    public bool Forward => _forward;

    /// <summary>Currently detected code, or NUL when no valid R2 pair is present.</summary>
    public char CurrentDigit => _currentDigit;

    public void SetCallback(R2MfToneReportHandler? callback, object? userData = null) {
        _callback = callback;
        _callbackData = userData;
    }

    /// <summary>Processes a block of 8 kHz signed 16-bit audio samples.</summary>
    public int Process(ReadOnlySpan<short> samples) {
        for (int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex++) {
            short sample = samples[sampleIndex];
            for (int i = 0; i < _filters.Length; i++)
                _filters[i].Add(sample);

            _currentSample++;
            if (_currentSample < SamplesPerBlock)
                continue;

            char hit = DecodeCompletedBlock();
            if (_currentDigit != hit) {
                _callback?.Invoke(_callbackData, hit, hit != '\0' ? -10 : -99, 0);
                _currentDigit = hit;
            }

            _currentSample = 0;
        }

        return 0;
    }

    public void Reset() {
        foreach (BellR2Goertzel filter in _filters)
            filter.Reset();

        _currentSample = 0;
        _currentDigit = '\0';
    }

    private char DecodeCompletedBlock() {
        Span<double> energy = stackalloc double[6];
        for (int i = 0; i < _filters.Length; i++)
            energy[i] = _filters[i].GetEnergyAndReset();

        FindTwoLargest(energy, out int best, out int secondBest);

        bool hit = energy[best] >= DetectionThreshold
            && energy[secondBest] >= DetectionThreshold
            && energy[best] < energy[secondBest] * Twist
            && energy[best] * Twist > energy[secondBest];

        if (hit) {
            for (int i = 0; i < energy.Length; i++) {
                if (i == best || i == secondBest)
                    continue;

                if (energy[i] * RelativePeak >= energy[secondBest]) {
                    hit = false;
                    break;
                }
            }
        }

        if (!hit)
            return '\0';

        if (secondBest < best)
            (best, secondBest) = (secondBest, best);

        int position = best * 5 + secondBest - 1;
        if ((uint)position >= BellR2MfTables.R2Positions.Length)
            return '\0';

        char result = BellR2MfTables.R2Positions[position];
        return result == '-' ? '\0' : result;
    }

    private static void FindTwoLargest(ReadOnlySpan<double> values, out int best, out int secondBest) {
        if (values[0] > values[1]) {
            best = 0;
            secondBest = 1;
        } else {
            best = 1;
            secondBest = 0;
        }

        for (int i = 2; i < values.Length; i++) {
            if (values[i] >= values[best]) {
                secondBest = best;
                best = i;
            } else if (values[i] >= values[secondBest]) {
                secondBest = i;
            }
        }
    }
}
