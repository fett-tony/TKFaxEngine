/*
 * TKFaxEngine - managed C# port
 *
 * Dtmf.cs
 *
 * Combined port of dtmf.h, private/dtmf.h and dtmf.c.
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2001-2006 Steve Underwood.
 *
 * This port preserves the GNU Lesser General Public License version 2.1
 * licensing terms of the original source files.
 */

#nullable enable

namespace TKFaxEngine.Audio;

public delegate void DtmfDigitsReceivedHandler(object? userData, string digits, int length);
public delegate void DtmfTransmitQueueHandler(object? userData);
public delegate void DtmfRealtimeHandler(object? userData, int digit, int level, int duration);

/// <summary>
/// Minimal managed logging state used by the DTMF detector.
/// </summary>
public sealed class DtmfLoggingState {
    public bool DebugEnabled { get; set; }

    public Action<string>? Sink { get; set; }

    internal void WriteDebug(string message) {
        if (DebugEnabled)
            Sink?.Invoke(message);
    }
}

/// <summary>
/// Managed equivalent of <c>dtmf_rx_state_t</c>.
/// </summary>
public sealed class DtmfRxState : IDisposable {
    private readonly DtmfGoertzel[] _rowFilters = new DtmfGoertzel[4];
    private readonly DtmfGoertzel[] _columnFilters = new DtmfGoertzel[4];
    private readonly List<char> _digits = new(Dtmf.MaximumDigits);
    private readonly float[] _z350 = new float[2];
    private readonly float[] _z440 = new float[2];

    private DtmfDigitsReceivedHandler? _digitsCallback;
    private object? _digitsCallbackData;
    private DtmfRealtimeHandler? _realtimeCallback;
    private object? _realtimeCallbackData;
    private bool _disposed;

    public DtmfRxState(DtmfDigitsReceivedHandler? callback = null, object? userData = null) {
        for (int i = 0; i < 4; i++) {
            _rowFilters[i] = new DtmfGoertzel(Dtmf.RowFrequencies[i], Dtmf.SamplesPerBlock);
            _columnFilters[i] = new DtmfGoertzel(Dtmf.ColumnFrequencies[i], Dtmf.SamplesPerBlock);
        }

        Initialize(callback, userData);
    }

    public bool FilterDialTone { get; private set; }

    public float NormalTwist { get; private set; }

    public float ReverseTwist { get; private set; }

    public float Threshold { get; private set; }

    public float Energy { get; private set; }

    public int LastHit { get; private set; }

    public int InDigit { get; private set; }

    public int CurrentSample { get; private set; }

    public int Duration { get; private set; }

    public int LostDigits { get; private set; }

    public int CurrentDigits => _digits.Count;

    public bool IsDisposed => _disposed;

    public DtmfLoggingState Logging { get; } = new();

    public void Initialize(DtmfDigitsReceivedHandler? callback, object? userData) {
        _digitsCallback = callback;
        _digitsCallbackData = userData;
        _realtimeCallback = null;
        _realtimeCallbackData = null;

        FilterDialTone = false;
        NormalTwist = Dtmf.DefaultNormalTwist;
        ReverseTwist = Dtmf.DefaultReverseTwist;
        Threshold = Dtmf.DefaultThreshold;

        Array.Clear(_z350);
        Array.Clear(_z440);

        for (int i = 0; i < 4; i++) {
            _rowFilters[i].Reset();
            _columnFilters[i].Reset();
        }

        Energy = 0.0f;
        LastHit = 0;
        InDigit = 0;
        CurrentSample = 0;
        Duration = 0;
        LostDigits = 0;
        _digits.Clear();
        _disposed = false;
    }

    /// <summary>
    /// Processes signed 16-bit PCM samples at 8 kHz.
    /// </summary>
    public int Process(ReadOnlySpan<short> samples) {
        ThrowIfDisposed();

        int sample = 0;

        while (sample < samples.Length) {
            int needed = Dtmf.SamplesPerBlock - CurrentSample;
            int limit = Math.Min(samples.Length, sample + needed);

            for (int j = sample; j < limit; j++) {
                float amplitude = samples[j];

                if (FilterDialTone) {
                    float v1 = 0.98356f * amplitude
                        + 1.8954426f * _z350[0]
                        - 0.9691396f * _z350[1];

                    amplitude = v1
                        - 1.9251480f * _z350[0]
                        + _z350[1];

                    _z350[1] = _z350[0];
                    _z350[0] = v1;

                    v1 = 0.98456f * amplitude
                        + 1.8529543f * _z440[0]
                        - 0.9691396f * _z440[1];

                    amplitude = v1
                        - 1.8819938f * _z440[0]
                        + _z440[1];

                    _z440[1] = _z440[0];
                    _z440[0] = v1;
                }

                Energy += amplitude * amplitude;

                for (int i = 0; i < 4; i++) {
                    _rowFilters[i].Sample(amplitude);
                    _columnFilters[i].Sample(amplitude);
                }
            }

            int processed = limit - sample;

            if (Duration < int.MaxValue - processed)
                Duration += processed;

            CurrentSample += processed;
            sample = limit;

            if (CurrentSample < Dtmf.SamplesPerBlock)
                continue;

            ProcessCompletedBlock();
        }

        FlushDigitsCallback();
        return 0;
    }

    public int FillIn(int samples) {
        ThrowIfDisposed();

        for (int i = 0; i < 4; i++) {
            _rowFilters[i].Reset();
            _columnFilters[i].Reset();
        }

        Energy = 0.0f;
        CurrentSample = 0;
        return 0;
    }

    public int GetStatus() {
        ThrowIfDisposed();

        if (InDigit != 0)
            return InDigit;

        return LastHit != 0 ? 'x' : 0;
    }

    public string GetDigits(int maximum) {
        ThrowIfDisposed();

        if (maximum < 0)
            throw new ArgumentOutOfRangeException(nameof(maximum));

        int count = Math.Min(maximum, _digits.Count);

        if (count == 0)
            return string.Empty;

        char[] result = _digits.GetRange(0, count).ToArray();
        _digits.RemoveRange(0, count);
        return new string(result);
    }

    public void SetRealtimeCallback(DtmfRealtimeHandler? callback, object? userData) {
        ThrowIfDisposed();

        _realtimeCallback = callback;
        _realtimeCallbackData = userData;
        Duration = 0;
    }

    public void Configure(
        int filterDialTone,
        float twist,
        float reverseTwist,
        float threshold) {
        ThrowIfDisposed();

        if (filterDialTone >= 0) {
            Array.Clear(_z350);
            Array.Clear(_z440);
            FilterDialTone = filterDialTone != 0;
        }

        if (twist >= 0.0f)
            NormalTwist = Dtmf.DbToPowerRatio(twist);

        if (reverseTwist >= 0.0f)
            ReverseTwist = Dtmf.DbToPowerRatio(reverseTwist);

        if (threshold > -99.0f)
            Threshold = Dtmf.GoertzelThresholdDbm0(Dtmf.SamplesPerBlock, threshold);
    }

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

        _digits.Clear();
        _digitsCallback = null;
        _digitsCallbackData = null;
        _realtimeCallback = null;
        _realtimeCallbackData = null;

        for (int i = 0; i < 4; i++) {
            _rowFilters[i].Reset();
            _columnFilters[i].Reset();
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ProcessCompletedBlock() {
        Span<float> rowEnergy = stackalloc float[4];
        Span<float> columnEnergy = stackalloc float[4];

        int bestRow = 0;
        int bestColumn = 0;

        for (int i = 0; i < 4; i++) {
            rowEnergy[i] = _rowFilters[i].Result();
            columnEnergy[i] = _columnFilters[i].Result();

            if (rowEnergy[i] > rowEnergy[bestRow])
                bestRow = i;

            if (columnEnergy[i] > columnEnergy[bestColumn])
                bestColumn = i;
        }

        int hit = 0;

        if (rowEnergy[bestRow] >= Threshold
            && columnEnergy[bestColumn] >= Threshold
            && columnEnergy[bestColumn] < rowEnergy[bestRow] * ReverseTwist
            && columnEnergy[bestColumn] * NormalTwist > rowEnergy[bestRow]) {
            int i;

            for (i = 0; i < 4; i++) {
                bool competingColumn =
                    i != bestColumn
                    && columnEnergy[i] * Dtmf.RelativePeakColumn > columnEnergy[bestColumn];

                bool competingRow =
                    i != bestRow
                    && rowEnergy[i] * Dtmf.RelativePeakRow > rowEnergy[bestRow];

                if (competingColumn || competingRow)
                    break;
            }

            if (i >= 4
                && rowEnergy[bestRow] + columnEnergy[bestColumn]
                    > Dtmf.ToneToTotalEnergy * Energy) {
                hit = Dtmf.Positions[(bestRow << 2) + bestColumn];
            }

            if (Logging.DebugEnabled) {
                char candidate = Dtmf.Positions[(bestRow << 2) + bestColumn];

                Logging.WriteDebug(
                    $"Potentially '{candidate}' - total "
                    + $"{Dtmf.PowerRatioToDb(Energy) - Dtmf.PowerOffset:F2}dB, row "
                    + $"{Dtmf.PowerRatioToDb(rowEnergy[bestRow] / Dtmf.ToneToTotalEnergy) - Dtmf.PowerOffset:F2}dB, col "
                    + $"{Dtmf.PowerRatioToDb(columnEnergy[bestColumn] / Dtmf.ToneToTotalEnergy) - Dtmf.PowerOffset:F2}dB, "
                    + $"duration {Duration} - {(hit != 0 ? "hit" : "miss")}");
            }
        }

        if (hit != InDigit && LastHit != InDigit) {
            hit = hit != 0 && hit == LastHit ? hit : 0;

            if (_realtimeCallback is not null) {
                if (InDigit != 0 || hit != 0) {
                    int level = InDigit != 0 && hit == 0
                        ? -99
                        : (int)MathF.Round(
                            Dtmf.PowerRatioToDb(Energy) - Dtmf.PowerOffset,
                            MidpointRounding.ToEven);

                    _realtimeCallback(
                        _realtimeCallbackData,
                        hit,
                        level,
                        Duration);

                    Duration = 0;
                }
            } else if (hit != 0) {
                if (_digits.Count < Dtmf.MaximumDigits) {
                    _digits.Add((char)hit);

                    if (_digitsCallback is not null)
                        FlushDigitsCallback();
                } else {
                    LostDigits++;
                }
            }

            InDigit = hit;
        }

        LastHit = hit;
        Energy = 0.0f;
        CurrentSample = 0;
    }

    private void FlushDigitsCallback() {
        if (_digitsCallback is null || _digits.Count == 0)
            return;

        string digits = new(_digits.ToArray());
        _digitsCallback(_digitsCallbackData, digits, digits.Length);
        _digits.Clear();
    }

    private void ThrowIfDisposed() {
        if (_disposed)
            throw new ObjectDisposedException(nameof(DtmfRxState));
    }
}

/// <summary>
/// Managed equivalent of <c>dtmf_tx_state_t</c>.
/// </summary>
public sealed class DtmfTxState : IDisposable {
    private readonly Queue<char> _queue = new(Dtmf.MaximumDigits);

    private DtmfTransmitQueueHandler? _callback;
    private object? _callbackData;
    private bool _disposed;

    private bool _digitActive;
    private bool _toneSection;
    private int _sectionRemaining;
    private int _lowPhaseRate;
    private int _highPhaseRate;
    private uint _lowPhase;
    private uint _highPhase;

    public DtmfTxState(DtmfTransmitQueueHandler? callback = null, object? userData = null) {
        Initialize(callback, userData);
    }

    public float LowLevel { get; private set; }

    public float HighLevel { get; private set; }

    public int OnTime { get; private set; }

    public int OffTime { get; private set; }

    public int QueuedDigits => _queue.Count;

    public bool IsDisposed => _disposed;

    public void Initialize(DtmfTransmitQueueHandler? callback, object? userData) {
        _callback = callback;
        _callbackData = userData;
        _queue.Clear();

        _digitActive = false;
        _toneSection = false;
        _sectionRemaining = 0;
        _lowPhaseRate = 0;
        _highPhaseRate = 0;
        _lowPhase = 0;
        _highPhase = 0;

        SetLevel(Dtmf.DefaultTransmitLevel, 0);
        SetTiming(-1, -1);
        _disposed = false;
    }

    /// <summary>
    /// Generates DTMF PCM samples into the destination buffer.
    /// </summary>
    public int Generate(Span<short> destination) {
        ThrowIfDisposed();

        int written = 0;

        while (written < destination.Length) {
            if (_digitActive) {
                int count = Math.Min(destination.Length - written, _sectionRemaining);

                if (_toneSection)
                    GenerateTone(destination.Slice(written, count));
                else
                    destination.Slice(written, count).Clear();

                written += count;
                _sectionRemaining -= count;

                if (_sectionRemaining > 0)
                    continue;

                if (_toneSection) {
                    _toneSection = false;
                    _sectionRemaining = OffTime;

                    if (_sectionRemaining == 0)
                        FinishDigit();
                } else {
                    FinishDigit();
                }

                continue;
            }

            if (!TryReadNextDigit(out char digit))
                break;

            int position = Dtmf.Positions.IndexOf(digit);

            if (position < 0)
                continue;

            StartDigit(position);
        }

        return written;
    }

    /// <summary>
    /// Atomically queues a string. The return value matches the native
    /// implementation: zero on success or the number of characters which
    /// would not fit.
    /// </summary>
    public int Put(string digits, int length = -1) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(digits);

        int count = length < 0 ? digits.Length : length;

        if (count < 0 || count > digits.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        if (count == 0)
            return 0;

        int freeSpace = Dtmf.MaximumDigits - _queue.Count;

        if (freeSpace < count)
            return count - freeSpace;

        for (int i = 0; i < count; i++)
            _queue.Enqueue(digits[i]);

        return 0;
    }

    public void SetLevel(int level, int twist) {
        ThrowIfDisposed(allowDuringInitialization: true);

        LowLevel = Dds.ScalingDbm0Float(level);
        HighLevel = Dds.ScalingDbm0Float(level + twist);
    }

    public void SetTiming(int onTime, int offTime) {
        ThrowIfDisposed(allowDuringInitialization: true);

        int selectedOnTime = onTime >= 0 ? onTime : Dtmf.DefaultOnTimeMilliseconds;
        int selectedOffTime = offTime >= 0 ? offTime : Dtmf.DefaultOffTimeMilliseconds;

        OnTime = checked(selectedOnTime * Dds.SampleRate / 1000);
        OffTime = checked(selectedOffTime * Dds.SampleRate / 1000);
    }

    public int Release() {
        ThrowIfDisposed();
        _queue.Clear();
        _digitActive = false;
        return 0;
    }

    public int Free() {
        Dispose();
        return 0;
    }

    public void Dispose() {
        if (_disposed)
            return;

        _queue.Clear();
        _callback = null;
        _callbackData = null;
        _digitActive = false;
        _sectionRemaining = 0;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private bool TryReadNextDigit(out char digit) {
        if (_queue.Count == 0) {
            if (_callback is null) {
                digit = default;
                return false;
            }

            _callback(_callbackData);

            if (_queue.Count == 0) {
                digit = default;
                return false;
            }
        }

        digit = _queue.Dequeue();
        return true;
    }

    private void StartDigit(int position) {
        int row = position >> 2;
        int column = position & 3;

        _lowPhaseRate = Dds.PhaseRate(Dtmf.RowFrequencies[row]);
        _highPhaseRate = Dds.PhaseRate(Dtmf.ColumnFrequencies[column]);
        _lowPhase = 0;
        _highPhase = 0;

        _digitActive = true;
        _toneSection = true;
        _sectionRemaining = OnTime;

        if (_sectionRemaining == 0) {
            _toneSection = false;
            _sectionRemaining = OffTime;

            if (_sectionRemaining == 0)
                FinishDigit();
        }
    }

    private void GenerateTone(Span<short> destination) {
        for (int i = 0; i < destination.Length; i++) {
            float low = Dds.GenerateFloatModulated(
                ref _lowPhase,
                _lowPhaseRate,
                LowLevel,
                0);

            float high = Dds.GenerateFloatModulated(
                ref _highPhase,
                _highPhaseRate,
                HighLevel,
                0);

            destination[i] = Saturate16(low + high);
        }
    }

    private void FinishDigit() {
        _digitActive = false;
        _toneSection = false;
        _sectionRemaining = 0;
    }

    private void ThrowIfDisposed(bool allowDuringInitialization = false) {
        if (_disposed && !allowDuringInitialization)
            throw new ObjectDisposedException(nameof(DtmfTxState));
    }

    private static short Saturate16(float value) {
        if (value >= short.MaxValue)
            return short.MaxValue;

        if (value <= short.MinValue)
            return short.MinValue;

        return (short)value;
    }
}

/// <summary>
/// Native-compatible DTMF facade.
/// </summary>
public static class Dtmf {
    public const int MaximumDigits = 128;
    public const int MAX_DTMF_DIGITS = MaximumDigits;

    public const int DefaultTransmitLevel = -10;
    public const int DefaultOnTimeMilliseconds = 50;
    public const int DefaultOffTimeMilliseconds = 55;
    public const int SamplesPerBlock = 102;

    internal const float DefaultThreshold = 171029200.0f;
    internal const float DefaultNormalTwist = 6.309f;
    internal const float DefaultReverseTwist = 2.512f;
    internal const float RelativePeakRow = 6.309f;
    internal const float RelativePeakColumn = 6.309f;
    internal const float ToneToTotalEnergy = 83.868f;
    internal const float PowerOffset = 107.255f;

    internal static readonly float[] RowFrequencies =
    {
        697.0f,
        770.0f,
        852.0f,
        941.0f
    };

    internal static readonly float[] ColumnFrequencies =
    {
        1209.0f,
        1336.0f,
        1477.0f,
        1633.0f
    };

    internal const string Positions = "123A456B789C*0#D";

    public static DtmfRxState dtmf_rx_init(
        DtmfRxState? state,
        DtmfDigitsReceivedHandler? callback,
        object? userData) {
        if (state is null)
            return new DtmfRxState(callback, userData);

        state.Initialize(callback, userData);
        return state;
    }

    public static int dtmf_rx(
        DtmfRxState state,
        ReadOnlySpan<short> samples) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Process(samples);
    }

    public static int dtmf_rx(
        DtmfRxState state,
        short[] samples,
        int sampleCount) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(samples);

        if ((uint)sampleCount > (uint)samples.Length)
            throw new ArgumentOutOfRangeException(nameof(sampleCount));

        return state.Process(samples.AsSpan(0, sampleCount));
    }

    public static int dtmf_rx_fillin(DtmfRxState state, int samples) {
        ArgumentNullException.ThrowIfNull(state);
        return state.FillIn(samples);
    }

    public static int dtmf_rx_status(DtmfRxState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetStatus();
    }

    public static int dtmf_rx_get(
        DtmfRxState state,
        Span<char> destination,
        int maximum) {
        ArgumentNullException.ThrowIfNull(state);

        if (maximum < 0)
            throw new ArgumentOutOfRangeException(nameof(maximum));

        int allowed = Math.Min(maximum, Math.Max(0, destination.Length - 1));
        string digits = state.GetDigits(allowed);
        digits.AsSpan().CopyTo(destination);

        if (destination.Length > digits.Length)
            destination[digits.Length] = '\0';

        return digits.Length;
    }

    public static string dtmf_rx_get(DtmfRxState state, int maximum) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetDigits(maximum);
    }

    public static void dtmf_rx_set_realtime_callback(
        DtmfRxState state,
        DtmfRealtimeHandler? callback,
        object? userData) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetRealtimeCallback(callback, userData);
    }

    public static void dtmf_rx_parms(
        DtmfRxState state,
        int filterDialTone,
        float twist,
        float reverseTwist,
        float threshold) {
        ArgumentNullException.ThrowIfNull(state);
        state.Configure(filterDialTone, twist, reverseTwist, threshold);
    }

    public static DtmfLoggingState dtmf_rx_get_logging_state(DtmfRxState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Logging;
    }

    public static int dtmf_rx_release(DtmfRxState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    public static int dtmf_rx_free(DtmfRxState? state) {
        return state?.Free() ?? 0;
    }

    public static DtmfTxState dtmf_tx_init(
        DtmfTxState? state,
        DtmfTransmitQueueHandler? callback,
        object? userData) {
        if (state is null)
            return new DtmfTxState(callback, userData);

        state.Initialize(callback, userData);
        return state;
    }

    public static int dtmf_tx(DtmfTxState state, Span<short> destination) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Generate(destination);
    }

    public static int dtmf_tx(
        DtmfTxState state,
        short[] destination,
        int maximumSamples) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(destination);

        if ((uint)maximumSamples > (uint)destination.Length)
            throw new ArgumentOutOfRangeException(nameof(maximumSamples));

        return state.Generate(destination.AsSpan(0, maximumSamples));
    }

    public static int dtmf_tx_put(
        DtmfTxState state,
        string digits,
        int length = -1) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Put(digits, length);
    }

    public static void dtmf_tx_set_level(DtmfTxState state, int level, int twist) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetLevel(level, twist);
    }

    public static void dtmf_tx_set_timing(DtmfTxState state, int onTime, int offTime) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetTiming(onTime, offTime);
    }

    public static int dtmf_tx_release(DtmfTxState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    public static int dtmf_tx_free(DtmfTxState? state) {
        return state?.Free() ?? 0;
    }

    internal static float DbToPowerRatio(float value) {
        return MathF.Pow(10.0f, value / 10.0f);
    }

    internal static float PowerRatioToDb(float value) {
        return value > 0.0f
            ? 10.0f * MathF.Log10(value)
            : float.NegativeInfinity;
    }

    internal static float GoertzelThresholdDbm0(int length, float threshold) {
        double scale =
            length
            * (double)length
            * 32768.0
            * 32768.0
            / 2.0;

        return (float)(
            scale
            * Math.Pow(
                10.0,
                (threshold - Dds.Dbm0MaximumSinePower) / 10.0));
    }
}

internal sealed class DtmfGoertzel {
    private readonly float _factor;

    private float _v2;
    private float _v3;

    public DtmfGoertzel(float frequency, int samples) {
        _factor = 2.0f * MathF.Cos(
            2.0f
            * MathF.PI
            * frequency
            / Dds.SampleRate);

        Samples = samples;
    }

    public int Samples { get; }

    public void Sample(float amplitude) {
        float previousV2 = _v2;
        _v2 = _v3;
        _v3 = _factor * _v2 - previousV2 + amplitude;
    }

    public float Result() {
        float previousV2 = _v2;
        _v2 = _v3;
        _v3 = _factor * _v2 - previousV2;

        float result =
            _v3 * _v3
            + _v2 * _v2
            - _v2 * _v3 * _factor;

        result *= 2.0f;
        Reset();
        return result;
    }

    public void Reset() {
        _v2 = 0.0f;
        _v3 = 0.0f;
    }
}
