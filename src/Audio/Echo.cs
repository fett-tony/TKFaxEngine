/*
 * TKFaxEngine - managed C# port
 *
 * Echo.cs
 *
 * Combined port of echo.h, private/echo.h and echo.c.
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2001-2003 Steve Underwood.
 *
 * This port preserves the GNU Lesser General Public License version 2.1
 * licensing terms of the original source files.
 */

#nullable enable

using System.Numerics;

namespace TKFaxEngine.Audio;

[Flags]
public enum EchoCancellerMode {
    None = 0x00,
    UseAdaption = 0x01,
    UseNonLinearProcessor = 0x02,
    UseComfortNoise = 0x04,
    UseClip = 0x08,
    UseSuppressor = 0x10,
    UseTransmitHighPassFilter = 0x20,
    UseReceiveHighPassFilter = 0x40,
    Disable = 0x80
}

/// <summary>
/// Managed equivalent of <c>echo_can_state_t</c>.
/// </summary>
public sealed class EchoCanState : IDisposable {
    private const int NonUpdateDwellTime = 600;
    private const int MinimumTransmitPowerForAdaption = 64 * 64;

    private readonly int[] _txPower = new int[4];
    private readonly int[] _rxPower = new int[3];
    private readonly int[] _lastAcf = new int[28];
    private readonly int[] _transmitHighPassState = new int[2];
    private readonly int[] _receiveHighPassState = new int[2];

    private short[][] _firTaps16 = Array.Empty<short[]>();
    private int[] _firTaps32 = Array.Empty<int>();
    private short[] _firHistory = Array.Empty<short>();
    private short[] _snapshot = Array.Empty<short>();

    private int _firCurrentPosition;
    private bool _disposed;

    public EchoCanState(int length, EchoCancellerMode adaptionMode) {
        Initialize(length, adaptionMode);
    }

    public int Taps { get; private set; }

    public int CurrentPosition { get; private set; }

    public EchoCancellerMode AdaptionMode { get; private set; }

    public int ReceivePowerThreshold { get; private set; }

    public int NonUpdateDwell { get; private set; }

    public int CleanReceivePower { get; private set; }

    public int VoiceActivity { get; private set; }

    public bool ComfortNoiseActive { get; private set; }

    public int GeigelMaximum { get; private set; }

    public int GeigelLag { get; private set; }

    public bool DoubleTalkOnset { get; private set; }

    public int TapSet { get; private set; }

    public int TapRotateCounter { get; private set; }

    public int LatestCorrection { get; private set; }

    public int NarrowbandCount { get; private set; }

    public int NarrowbandScore { get; private set; }

    public int ComfortNoiseLevel { get; private set; }

    public uint ComfortNoiseRandomNumber { get; private set; }

    public int ComfortNoiseFilter { get; private set; }

    public long ProcessedSamples { get; private set; }

    public bool IsDisposed => _disposed;

    /// <summary>
    /// Optional diagnostic sink. It replaces the unconditional printf calls
    /// present in the supplied native development source.
    /// </summary>
    public Action<string>? DiagnosticLog { get; set; }

    public ReadOnlySpan<int> TransmitPower => _txPower;

    public ReadOnlySpan<int> ReceivePower => _rxPower;

    public ReadOnlySpan<short> SnapshotCoefficients => _snapshot;

    public void Initialize(int length, EchoCancellerMode adaptionMode) {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        Taps = length;
        _firTaps16 = new short[4][];

        for (int i = 0; i < _firTaps16.Length; i++)
            _firTaps16[i] = new short[length];

        _firTaps32 = new int[length];
        _firHistory = new short[length];
        _snapshot = new short[length];

        _firCurrentPosition = length - 1;
        CurrentPosition = length - 1;

        ReceivePowerThreshold = 10_000_000;
        AdaptionMode = adaptionMode;
        _disposed = false;

        Flush();
    }

    public void SetAdaptionMode(EchoCancellerMode adaptionMode) {
        ThrowIfDisposed();
        AdaptionMode = adaptionMode;
    }

    public void Flush() {
        ThrowIfDisposed(allowDuringInitialization: true);

        Array.Clear(_txPower, 0, _txPower.Length);
        Array.Clear(_rxPower, 0, _rxPower.Length);
        Array.Clear(_lastAcf, 0, _lastAcf.Length);
        Array.Clear(_transmitHighPassState, 0, _transmitHighPassState.Length);
        Array.Clear(_receiveHighPassState, 0, _receiveHighPassState.Length);

        for (int i = 0; i < _firTaps16.Length; i++)
            Array.Clear(_firTaps16[i], 0, _firTaps16[i].Length);

        Array.Clear(_firTaps32, 0, _firTaps32.Length);
        Array.Clear(_firHistory, 0, _firHistory.Length);
        Array.Clear(_snapshot, 0, _snapshot.Length);

        _firCurrentPosition = Taps - 1;
        CurrentPosition = Taps - 1;

        CleanReceivePower = 0;
        NonUpdateDwell = 0;
        VoiceActivity = 0;
        ComfortNoiseActive = false;
        ComfortNoiseLevel = 1000;
        ComfortNoiseRandomNumber = 0;
        ComfortNoiseFilter = 0;

        GeigelMaximum = 0;
        GeigelLag = 0;
        DoubleTalkOnset = false;
        TapSet = 0;
        TapRotateCounter = 1600;

        LatestCorrection = 0;
        NarrowbandCount = 0;
        NarrowbandScore = 0;
        ProcessedSamples = 0;
    }

    public void Snapshot() {
        ThrowIfDisposed();
        Array.Copy(_firTaps16[0], _snapshot, Taps);
    }

    /// <summary>
    /// Processes one transmit/receive sample pair and returns the echo-reduced
    /// receive sample.
    /// </summary>
    public short Update(short transmitSample, short receiveSample) {
        ThrowIfDisposed();

        ProcessedSamples++;

        if ((AdaptionMode & EchoCancellerMode.Disable) != 0)
            return receiveSample;

        short tx = transmitSample;
        short rx = receiveSample;

        if ((AdaptionMode & EchoCancellerMode.UseReceiveHighPassFilter) != 0)
            rx = HighPass(_receiveHighPassState, rx);

        LatestCorrection = 0;

        short echoValue = FilterTransmitSample(tx);
        int cleanReceive = rx - echoValue;

        DiagnosticLog?.Invoke($"echo is {echoValue}");

        if (NonUpdateDwell > 0)
            NonUpdateDwell--;

        UpdatePowerEstimates(tx, rx, cleanReceive);

        int narrowbandTestScore = 0;

        if (_txPower[0] > MinimumTransmitPowerForAdaption) {
            if (_txPower[1] > _rxPower[0]) {
                if (NonUpdateDwell == 0) {
                    NarrowbandCount++;

                    if (NarrowbandCount >= 160) {
                        NarrowbandCount = 0;
                        narrowbandTestScore = DetectNarrowband();

                        DiagnosticLog?.Invoke(
                            $"Do the narrowband test {narrowbandTestScore} at {CurrentPosition}");

                        HandleNarrowbandResult(narrowbandTestScore);
                    }

                    DoubleTalkOnset = false;
                    RotateTapSetIfDue();

                    if ((AdaptionMode & EchoCancellerMode.UseAdaption) != 0
                        && NarrowbandScore == 0) {
                        int correction = cleanReceive;
                        int transmitPeak = Math.Max(Math.Abs((int)tx), _txPower[3]);

                        if (Math.Abs((int)tx) > 4L * _txPower[3])
                            transmitPeak = Math.Abs((int)tx);

                        int shift = TopBit((uint)transmitPeak) - 8;

                        if (shift > 0)
                            correction >>= shift;

                        Adapt(correction);
                    }
                }
            } else {
                HandleDoubleTalk();
                NonUpdateDwell = NonUpdateDwellTime;
            }
        }

        UpdateVoiceActivity();
        ResetIfDiverging();

        cleanReceive = ApplyNonLinearProcessor(cleanReceive);

        DiagnosticLog?.Invoke(
            $"Narrowband score {NarrowbandScore,4} {narrowbandTestScore,5} at {ProcessedSamples}");

        CurrentPosition--;

        if (CurrentPosition < 0)
            CurrentPosition = Taps - 1;

        return Saturate16(cleanReceive);
    }

    public short HighPassTransmit(short transmitSample) {
        ThrowIfDisposed();

        if ((AdaptionMode & EchoCancellerMode.UseTransmitHighPassFilter) != 0)
            return HighPass(_transmitHighPassState, transmitSample);

        return transmitSample;
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

        _firTaps16 = Array.Empty<short[]>();
        _firTaps32 = Array.Empty<int>();
        _firHistory = Array.Empty<short>();
        _snapshot = Array.Empty<short>();
        DiagnosticLog = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private short FilterTransmitSample(short sample) {
        _firHistory[_firCurrentPosition] = sample;

        int offset2 = _firCurrentPosition;
        int offset1 = Taps - offset2;
        long accumulator = 0;
        short[] coefficients = _firTaps16[TapSet];

        int i;

        for (i = Taps - 1; i >= offset1; i--)
            accumulator += coefficients[i] * (long)_firHistory[i - offset1];

        for (; i >= 0; i--)
            accumulator += coefficients[i] * (long)_firHistory[i + offset2];

        _firCurrentPosition--;

        if (_firCurrentPosition < 0)
            _firCurrentPosition = Taps - 1;

        return unchecked((short)(accumulator >> 15));
    }

    private void Adapt(int factor) {
        int offset2 = CurrentPosition;
        int offset1 = Taps - offset2;
        int i;

        for (i = Taps - 1; i >= offset1; i--)
            UpdateTap(i, _firHistory[i - offset1], factor);

        for (; i >= 0; i--)
            UpdateTap(i, _firHistory[i + offset2], factor);
    }

    private void UpdateTap(int tap, short history, int factor) {
        int correction = unchecked(history * factor);
        _firTaps32[tap] = unchecked(_firTaps32[tap] + correction);
        _firTaps16[TapSet][tap] = unchecked((short)(_firTaps32[tap] >> 15));
        LatestCorrection = correction;
    }

    private int DetectNarrowband() {
        const int signalLength = 32;
        const int correlationLength = 9;

        if (Taps == 0)
            return 0;

        Span<float> signal = stackalloc float[signalLength];
        Span<float> autocorrelation = stackalloc float[correlationLength];
        Span<int> scaledAutocorrelation = stackalloc int[correlationLength];

        int position = CurrentPosition;

        for (int i = 0; i < signalLength; i++) {
            signal[i] = _firHistory[position];
            position++;

            if (position >= Taps)
                position = 0;
        }

        for (int lag = 0; lag < correlationLength; lag++) {
            float value = 0.0f;

            for (int i = lag; i < signalLength; i++)
                value += signal[i] * signal[i - lag];

            autocorrelation[lag] = value;
        }

        if (autocorrelation[0] <= 0.0f) {
            for (int i = 0; i < correlationLength; i++)
                _lastAcf[i] = 0;

            return 0;
        }

        float scale = 0x1FFFFFFF / autocorrelation[0];

        for (int i = 0; i < correlationLength; i++)
            scaledAutocorrelation[i] = (int)(autocorrelation[i] * scale);

        int score = 0;

        for (int i = 0; i < correlationLength; i++) {
            int previous = _lastAcf[i];
            int current = scaledAutocorrelation[i];

            if (previous >= 0 && current >= 0) {
                if ((previous >> 1) < current
                    && current < SaturatingShiftLeft(previous, 1)) {
                    score++;
                }
            } else if (previous < 0 && current < 0) {
                if ((previous >> 1) > current
                    && current > SaturatingShiftLeft(previous, 1)) {
                    score++;
                }
            }
        }

        for (int i = 0; i < correlationLength; i++)
            _lastAcf[i] = scaledAutocorrelation[i];

        return score;
    }

    private void HandleNarrowbandResult(int score) {
        if (score > 6) {
            if (NarrowbandScore == 0)
                CopyTapSet(OldestTapSet, 3);

            NarrowbandScore = SaturatingAdd(NarrowbandScore, score);
            return;
        }

        if (NarrowbandScore > 200) {
            DiagnosticLog?.Invoke(
                $"Revert to {OldestTapSet} at {ProcessedSamples}");

            CopyTapSet(3, TapSet);
            CopyTapSet(3, PreviousTapSet);
            RestoreAdaptiveTapsFrom(3);
            TapRotateCounter = 1600;
        }

        NarrowbandScore = 0;
    }

    private void RotateTapSetIfDue() {
        TapRotateCounter--;

        if (TapRotateCounter > 0)
            return;

        DiagnosticLog?.Invoke($"Rotate to {TapSet} at {ProcessedSamples}");

        TapRotateCounter = 1600;
        TapSet++;

        if (TapSet > 2)
            TapSet = 0;
    }

    private void HandleDoubleTalk() {
        if (DoubleTalkOnset)
            return;

        DiagnosticLog?.Invoke(
            $"Revert to {OldestTapSet} at {ProcessedSamples}");

        CopyTapSet(OldestTapSet, TapSet);
        CopyTapSet(OldestTapSet, PreviousTapSet);
        RestoreAdaptiveTapsFrom(OldestTapSet);

        TapRotateCounter = 1600;
        DoubleTalkOnset = true;
    }

    private void RestoreAdaptiveTapsFrom(int sourceTapSet) {
        short[] source = _firTaps16[sourceTapSet];

        for (int i = 0; i < Taps; i++)
            _firTaps32[i] = source[i] << 15;
    }

    private void CopyTapSet(int source, int destination) {
        Array.Copy(_firTaps16[source], _firTaps16[destination], Taps);
    }

    private int OldestTapSet => (TapSet + 1) % 3;

    private int PreviousTapSet => (TapSet + 2) % 3;

    private void UpdatePowerEstimates(short tx, short rx, int cleanReceive) {
        int absoluteTransmit = Math.Abs((int)tx);
        int transmitSquare = SquareToInt(tx);
        int receiveSquare = SquareToInt(rx);
        int cleanSquare = SquareToInt(cleanReceive);

        _txPower[3] = IirUpdate(_txPower[3], absoluteTransmit, 5);
        _txPower[2] = IirUpdate(_txPower[2], transmitSquare, 8);
        _txPower[1] = IirUpdate(_txPower[1], transmitSquare, 5);
        _txPower[0] = IirUpdate(_txPower[0], transmitSquare, 3);

        _rxPower[1] = IirUpdate(_rxPower[1], receiveSquare, 6);
        _rxPower[0] = IirUpdate(_rxPower[0], receiveSquare, 3);

        CleanReceivePower = IirUpdate(CleanReceivePower, cleanSquare, 6);
    }

    private void UpdateVoiceActivity() {
        if (_rxPower[1] > 0) {
            long value = 8000L * CleanReceivePower / _rxPower[1];
            VoiceActivity = value > int.MaxValue ? int.MaxValue : (int)value;
        } else {
            VoiceActivity = 0;
        }
    }

    private void ResetIfDiverging() {
        if (_rxPower[1] <= 2048 * 2048
            || CleanReceivePower <= 4L * _rxPower[1]) {
            return;
        }

        Array.Clear(_firTaps32, 0, _firTaps32.Length);

        for (int i = 0; i < _firTaps16.Length; i++)
            Array.Clear(_firTaps16[i], 0, _firTaps16[i].Length);
    }

    private int ApplyNonLinearProcessor(int cleanReceive) {
        if ((AdaptionMode & EchoCancellerMode.UseNonLinearProcessor) == 0) {
            ComfortNoiseActive = false;
            return cleanReceive;
        }

        if (_rxPower[1] < 30_000_000) {
            if (!ComfortNoiseActive) {
                ComfortNoiseLevel = CleanReceivePower;
                ComfortNoiseActive = true;
            }

            if ((AdaptionMode & EchoCancellerMode.UseComfortNoise) != 0) {
                ComfortNoiseRandomNumber = unchecked(
                    1664525U * ComfortNoiseRandomNumber + 1013904223U);

                int randomSample =
                    (int)(ComfortNoiseRandomNumber & 0xFFFFU) - 32768;

                ComfortNoiseFilter =
                    (randomSample + 5 * ComfortNoiseFilter) >> 3;

                long generated =
                    (long)ComfortNoiseFilter * ComfortNoiseLevel >> 17;

                return ClampToInt(generated);
            }

            return 0;
        }

        ComfortNoiseActive = false;
        return cleanReceive;
    }

    private static short HighPass(int[] coefficients, short amplitude) {
        int z = amplitude << 15;
        z -= z >> 4;

        coefficients[0] = unchecked(
            coefficients[0]
            + z
            - (coefficients[0] >> 3)
            - coefficients[1]);

        coefficients[1] = z;
        return Saturate16(coefficients[0] >> 15);
    }

    private static int IirUpdate(int state, int input, int shift) {
        long delta = (long)input - state;
        long value = state + (delta >> shift);
        return ClampToInt(value);
    }

    private static int SquareToInt(int value) {
        long square = (long)value * value;
        return square > int.MaxValue ? int.MaxValue : (int)square;
    }

    private static int TopBit(uint value) {
        return value == 0
            ? -1
            : 31 - BitOperations.LeadingZeroCount(value);
    }

    private static int SaturatingAdd(int left, int right) {
        long value = (long)left + right;
        return ClampToInt(value);
    }

    private static int SaturatingShiftLeft(int value, int count) {
        long shifted = (long)value << count;
        return ClampToInt(shifted);
    }

    private static int ClampToInt(long value) {
        if (value > int.MaxValue)
            return int.MaxValue;

        if (value < int.MinValue)
            return int.MinValue;

        return (int)value;
    }

    private static short Saturate16(int value) {
        if (value > short.MaxValue)
            return short.MaxValue;

        if (value < short.MinValue)
            return short.MinValue;

        return (short)value;
    }

    private void ThrowIfDisposed(bool allowDuringInitialization = false) {
        if (_disposed && !allowDuringInitialization)
            throw new ObjectDisposedException(nameof(EchoCanState));
    }
}

/// <summary>
/// Native-compatible echo canceller facade.
/// </summary>
public static class Echo {
    public const int ECHO_CAN_USE_ADAPTION = (int)EchoCancellerMode.UseAdaption;
    public const int ECHO_CAN_USE_NLP = (int)EchoCancellerMode.UseNonLinearProcessor;
    public const int ECHO_CAN_USE_CNG = (int)EchoCancellerMode.UseComfortNoise;
    public const int ECHO_CAN_USE_CLIP = (int)EchoCancellerMode.UseClip;
    public const int ECHO_CAN_USE_SUPPRESSOR = (int)EchoCancellerMode.UseSuppressor;
    public const int ECHO_CAN_USE_TX_HPF = (int)EchoCancellerMode.UseTransmitHighPassFilter;
    public const int ECHO_CAN_USE_RX_HPF = (int)EchoCancellerMode.UseReceiveHighPassFilter;
    public const int ECHO_CAN_DISABLE = (int)EchoCancellerMode.Disable;

    public static EchoCanState echo_can_init(int length, int adaptionMode) {
        return new EchoCanState(
            length,
            (EchoCancellerMode)adaptionMode);
    }

    public static int echo_can_release(EchoCanState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    public static int echo_can_free(EchoCanState? state) {
        return state?.Free() ?? 0;
    }

    public static void echo_can_flush(EchoCanState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.Flush();
    }

    public static void echo_can_adaption_mode(
        EchoCanState state,
        int adaptionMode) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetAdaptionMode((EchoCancellerMode)adaptionMode);
    }

    public static short echo_can_update(
        EchoCanState state,
        short transmitSample,
        short receiveSample) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Update(transmitSample, receiveSample);
    }

    public static short echo_can_hpf_tx(
        EchoCanState state,
        short transmitSample) {
        ArgumentNullException.ThrowIfNull(state);
        return state.HighPassTransmit(transmitSample);
    }

    public static void echo_can_snapshot(EchoCanState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.Snapshot();
    }
}
