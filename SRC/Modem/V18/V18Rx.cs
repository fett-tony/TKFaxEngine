/*
 * TKFaxEngine - managed C# port
 *
 * V18Rx.cs
 *
 * V.18 receive signal path.
 */

#nullable enable

using System.Numerics;
using System.Text;
using TKFaxEngine.Audio;

namespace TKFaxEngine.Modem.V18;

public sealed partial class V18State {
    private const float V18ToneToTotalEnergy = 83.868f;
    private const float V18ToneMinimumDbm0 = -43.0f;
    private const int MinimumFormatCarrierMilliseconds = 250;
    private const int MinimumFormatCharacters = 3;

    private readonly DtmfRxState _dtmfReceiver = new();
    private readonly V18GoertzelBank _toneBank = new(
        ToneFrequencies,
        GoertzelSamplesPerBlock,
        V18ToneMinimumDbm0,
        V18ToneToTotalEnergy);
    private readonly StringBuilder _pendingDtmfDigits = new(16);
    private readonly List<V18AutomodingFskReceiver> _automodingPatternReceivers = new();
    private readonly List<V18FskFormatProbe> _formatProbes = new();

    private V18Mode _pendingDetectedMode = V18Mode.None;
    private V18Status _pendingDetectedStatus = V18Status.SwitchToNone;
    private bool? _pendingModemCallingParty;
    private bool _pendingCiDetected;
    private bool _pendingXciDetected;
    private bool _pendingTxpDetected;
    private bool _pendingAnsDetected;
    private bool _pendingAnsLost;
    private bool _activeFskCarrierPresent;

    /// <summary>
    /// Performs the <c>Receive</c> operation.
    /// </summary>
    /// <param name="samples">The samples value.</param>
    /// <returns>The result of the operation.</returns>
    public int Receive(ReadOnlySpan<short> samples) {
        ThrowIfDisposed();

        AdvanceControlTimers(samples.Length);

        if (ReceiveSuppressionTimer > 0) {
            int suppressed = Math.Min(ReceiveSuppressionTimer, samples.Length);
            ReceiveSuppressionTimer -= suppressed;
            samples = samples[suppressed..];
            if (samples.IsEmpty)
                return 0;
        }

        bool wasAutobauding = Autobauding;
        ProcessV8Receive(samples);
        if (wasAutobauding && !Autobauding)
            return 0;

        if (Autobauding) {
            ScanForMode(samples, caller: !MonitorMode && CallingParty);
        } else if ((CurrentMode & V18Mode.Dtmf) != 0) {
            DecrementMessageTimer(samples.Length);
            Dtmf.dtmf_rx(_dtmfReceiver, samples);
            CommitPendingDtmf(allowModeSwitch: false);
        } else if (_fskReceiver is not null) {
            Fsk.Receive(_fskReceiver, samples);
        }

        return 0;
    }


    /// <summary>
    /// Performs the <c>Receive</c> operation.
    /// </summary>
    /// <param name="samples">The samples value.</param>
    /// <param name="length">The length value.</param>
    /// <returns>The result of the operation.</returns>
    public int Receive(short[] samples, int length) {
        ArgumentNullException.ThrowIfNull(samples);
        if (length < 0 || length > samples.Length)
            throw new ArgumentOutOfRangeException(nameof(length));
        return Receive(samples.AsSpan(0, length));
    }


    /// <summary>
    /// Performs the <c>ReceiveFillIn</c> operation.
    /// </summary>
    /// <param name="length">The length value.</param>
    /// <returns>The result of the operation.</returns>
    public int ReceiveFillIn(int length) {
        ThrowIfDisposed();
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));
        if (length == 0)
            return 0;

        AdvanceControlTimers(length);

        if (ReceiveSuppressionTimer > 0) {
            int suppressed = Math.Min(ReceiveSuppressionTimer, length);
            ReceiveSuppressionTimer -= suppressed;
            length -= suppressed;
            if (length == 0)
                return 0;
        }

        if (Autobauding) {
            ResetPendingReceiveEvents();
            Dtmf.dtmf_rx_fillin(_dtmfReceiver, length);
            _toneBank.Reset();
            ResetTonePersistence();
            foreach (V18AutomodingFskReceiver receiver in _automodingPatternReceivers)
                receiver.FillIn(length);
            foreach (V18FskFormatProbe probe in _formatProbes)
                probe.FillIn(length);
            CommitPendingReceiveEvents();
            return 0;
        }

        if (CurrentMode != V18Mode.None) {
            if ((CurrentMode & V18Mode.Dtmf) != 0) {
                DecrementMessageTimer(length);
                Dtmf.dtmf_rx_fillin(_dtmfReceiver, length);
                CommitPendingDtmf(allowModeSwitch: false);
            } else if (_fskReceiver is not null) {
                Fsk.ReceiveFillIn(_fskReceiver, length);
                if (_activeFskCarrierPresent)
                    ReceiveFskBit((int)SignalStatus.CarrierDown);
                if (_activeReceiveSpec is not null)
                    Fsk.RestartReceiver(
                        _fskReceiver,
                        _activeReceiveSpec,
                        FskFrameMode.Asynchronous);
            }
        }

        return 0;
    }


    private void InitializeReceivePath() {
        InitializeDtmfReceiver();
        _toneBank.Reset();
        ResetTonePersistence();
        _pendingDtmfDigits.Clear();
        _activeFskCarrierPresent = false;
        ResetPendingReceiveEvents();

        if (Autobauding)
            InitializeAutomodingReceivePath();
        else
            DisposeAutomodingReceivePath();
    }


    private void InitializeDtmfReceiver() {
        _dtmfReceiver.Initialize(
            static (userData, digits, length) =>
                ((V18State)userData!).QueueReceivedDtmf(digits, length),
            this);
    }


    private void InitializeAutomodingReceivePath() {
        DisposeAutomodingReceivePath();

        if (CallingParty && !MonitorMode) {
            _automodingPatternReceivers.Add(
                new V18AutomodingFskReceiver(
                    FskPreset.V21Channel2,
                    TxpBits,
                    maximumBitErrors: 1,
                    requiredMatches: 2,
                    matchWindowSamples: MillisecondsToSamples(650),
                    signal: V18AutomodingSignal.Txp,
                    onDetected: QueueAutomodingSignal));
        } else {
            _automodingPatternReceivers.Add(
                new V18AutomodingFskReceiver(
                    FskPreset.V21Channel1,
                    CiPacketBits,
                    maximumBitErrors: 1,
                    requiredMatches: 3,
                    matchWindowSamples: MillisecondsToSamples(650),
                    signal: V18AutomodingSignal.Ci,
                    onDetected: QueueAutomodingSignal));
            _automodingPatternReceivers.Add(
                new V18AutomodingFskReceiver(
                    FskPreset.V23Channel1,
                    XciMarkerBits,
                    maximumBitErrors: 1,
                    requiredMatches: 3,
                    matchWindowSamples: MillisecondsToSamples(3500),
                    signal: V18AutomodingSignal.Xci,
                    onDetected: QueueAutomodingSignal));
            _automodingPatternReceivers.Add(
                new V18AutomodingFskReceiver(
                    FskPreset.V21Channel1,
                    TxpBits,
                    maximumBitErrors: 1,
                    requiredMatches: 2,
                    matchWindowSamples: MillisecondsToSamples(650),
                    signal: V18AutomodingSignal.Txp,
                    onDetected: QueueAutomodingSignal));
        }

        AddFormatProbe(
            V18Mode.Weitbrecht5Bit4545,
            V18Status.SwitchToWeitbrecht5Bit4545,
            FskPreset.Weitbrecht4545,
            dataBits: 5,
            parity: AsyncParity.None,
            stopBits: 2,
            requiresBaudotShift: true);
        AddFormatProbe(
            V18Mode.Weitbrecht5Bit476,
            V18Status.SwitchToWeitbrecht5Bit476,
            FskPreset.Weitbrecht476,
            dataBits: 5,
            parity: AsyncParity.None,
            stopBits: 2,
            requiresBaudotShift: true);
        AddFormatProbe(
            V18Mode.Weitbrecht5Bit50,
            V18Status.SwitchToWeitbrecht5Bit50,
            FskPreset.Weitbrecht50,
            dataBits: 5,
            parity: AsyncParity.None,
            stopBits: 2,
            requiresBaudotShift: true);
        AddFormatProbe(
            V18Mode.Edt,
            V18Status.SwitchToEdt,
            FskPreset.V21Channel1At110Bps,
            dataBits: 7,
            parity: AsyncParity.Even,
            stopBits: 2,
            requiresBaudotShift: false);
        // Bell 103, V.21 and V.23 may be encountered with either channel
        // assignment. Run both common FSK presets in parallel; the detected
        // receive channel determines the local modem role after connection.
        AddFormatProbe(
            V18Mode.Bell103,
            V18Status.SwitchToBell103,
            FskPreset.Bell103Channel1,
            dataBits: 7,
            parity: AsyncParity.Even,
            stopBits: 1,
            requiresBaudotShift: false,
            ignoreParity: true,
            modemCallingParty: false);
        AddFormatProbe(
            V18Mode.Bell103,
            V18Status.SwitchToBell103,
            FskPreset.Bell103Channel2,
            dataBits: 7,
            parity: AsyncParity.Even,
            stopBits: 1,
            requiresBaudotShift: false,
            ignoreParity: true,
            modemCallingParty: true);
        AddFormatProbe(
            V18Mode.V21Textphone,
            V18Status.SwitchToV21Textphone,
            FskPreset.V21Channel1,
            dataBits: 7,
            parity: AsyncParity.Even,
            stopBits: 1,
            requiresBaudotShift: false,
            ignoreParity: true,
            modemCallingParty: false);
        AddFormatProbe(
            V18Mode.V21Textphone,
            V18Status.SwitchToV21Textphone,
            FskPreset.V21Channel2,
            dataBits: 7,
            parity: AsyncParity.Even,
            stopBits: 1,
            requiresBaudotShift: false,
            ignoreParity: true,
            modemCallingParty: true);
        AddFormatProbe(
            V18Mode.V23Videotex,
            V18Status.SwitchToV23Videotex,
            FskPreset.V23Channel1,
            dataBits: 7,
            parity: AsyncParity.Even,
            stopBits: 1,
            requiresBaudotShift: false,
            ignoreParity: true,
            modemCallingParty: true);
        AddFormatProbe(
            V18Mode.V23Videotex,
            V18Status.SwitchToV23Videotex,
            FskPreset.V23Channel2,
            dataBits: 7,
            parity: AsyncParity.Even,
            stopBits: 1,
            requiresBaudotShift: false,
            ignoreParity: true,
            modemCallingParty: false);
    }


    private void AddFormatProbe(
        V18Mode mode,
        V18Status status,
        FskPreset preset,
        int dataBits,
        AsyncParity parity,
        int stopBits,
        bool requiresBaudotShift,
        bool ignoreParity = false,
        bool? modemCallingParty = null) {
        _formatProbes.Add(
            new V18FskFormatProbe(
                mode,
                status,
                modemCallingParty ?? CallingParty,
                preset,
                dataBits,
                ToFskParity(parity),
                stopBits,
                requiresBaudotShift,
                ignoreParity,
                MillisecondsToSamples(MinimumFormatCarrierMilliseconds),
                MinimumFormatCharacters));
    }


    private void DisposeAutomodingReceivePath() {
        foreach (V18AutomodingFskReceiver receiver in _automodingPatternReceivers)
            receiver.Dispose();
        _automodingPatternReceivers.Clear();

        foreach (V18FskFormatProbe probe in _formatProbes)
            probe.Dispose();
        _formatProbes.Clear();

        ResetPendingReceiveEvents();
    }


    private void ScanForMode(ReadOnlySpan<short> samples, bool caller) {
        ResetPendingReceiveEvents();

        Dtmf.dtmf_rx(_dtmfReceiver, samples);
        if (_answerToneReceiver is not null)
            ModemConnectTones.Receive(_answerToneReceiver, samples);

        foreach (V18AutomodingFskReceiver receiver in _automodingPatternReceivers)
            receiver.Process(samples);

        foreach (V18FskFormatProbe probe in _formatProbes) {
            if (IsLocalAutomodingFskTransmission(probe.Preset))
                probe.FillIn(samples.Length);
            else
                probe.Process(samples);
        }

        ScanProbeTones(samples, caller);
        EvaluateFormatProbes();
        CommitPendingReceiveEvents();
    }


    private void ScanProbeTones(ReadOnlySpan<short> samples, bool caller) {
        int role = caller ? 0 : 1;

        foreach (short sample in samples) {
            if (!_toneBank.AddSample(sample))
                continue;

            int toneIndex = _toneBank.GetDominantTone();
            V18ProbeTone tone = toneIndex >= 0
                ? (V18ProbeTone)toneIndex
                : V18ProbeTone.None;
            if (tone != V18ProbeTone.None &&
                IsLocalAutomodingToneTransmission(tone)) {
                tone = V18ProbeTone.None;
                toneIndex = -1;
            }

            if (tone != _inTone) {
                V18ProbeTone previousTone = _inTone;
                int previousDuration = _toneDuration;
                if (previousTone != V18ProbeTone.None)
                    HandleProbeToneEnded(previousTone, caller, previousDuration);

                _inTone = tone;
                _toneDuration = tone == V18ProbeTone.None
                    ? 0
                    : GoertzelSamplesPerBlock;
                _targetToneDuration = toneIndex >= 0
                    ? ToneTargetDurations[role, toneIndex]
                    : 0;
                if (!caller &&
                    _answerAnsFollowupActive &&
                    tone == V18ProbeTone.Hz980) {
                    _targetToneDuration = MillisecondsToSamples(400);
                }
            } else if (tone != V18ProbeTone.None) {
                _toneDuration = Math.Min(
                    int.MaxValue - GoertzelSamplesPerBlock,
                    _toneDuration + GoertzelSamplesPerBlock);
            }

            if (tone != V18ProbeTone.None) {
                HandleProbeToneProgress(tone, caller, _toneDuration);
                if (_targetToneDuration > 0 &&
                    _toneDuration >= _targetToneDuration &&
                    !ProbeToneDecisionDeferred(tone)) {
                    ConfirmProbeTone(tone, caller);
                    _targetToneDuration = 0;
                }
            }
        }
    }


    private void ResetTonePersistence() {
        _toneDuration = 0;
        _targetToneDuration = 0;
        _inTone = V18ProbeTone.None;
    }


    private void ConfirmProbeTone(V18ProbeTone tone, bool caller) {
        Logging.Log(
            (int)SpanLogSeverity.Flow,
            "Tone %s (%d) seen\n",
            ToneToString(tone),
            (int)tone);

        if (MonitorMode) {
            Logging.Log(
                (int)SpanLogSeverity.Flow,
                "Monitor detected %s without response\n",
                ToneToString(tone));
            return;
        }

        switch (tone) {
            case V18ProbeTone.Hz390:
                if (CanAcceptV23ReverseCarrier(caller)) {
                    QueueDetectedMode(
                        V18Mode.V23Videotex,
                        V18Status.SwitchToV23Videotex,
                        modemCallingParty: false);
                }
                break;

            case V18ProbeTone.Hz1300:
                QueueDetectedMode(
                    V18Mode.V23Videotex,
                    V18Status.SwitchToV23Videotex,
                    modemCallingParty: true);
                break;

            case V18ProbeTone.Hz980:
                TeTimer = 0;
                TrTimer = 0;
                QueueDetectedMode(
                    V18Mode.V21Textphone,
                    V18Status.SwitchToV21Textphone,
                    modemCallingParty: false);
                break;

            case V18ProbeTone.Hz1180:
                // The data rate and frame format are resolved by the parallel
                // low-channel V.21/EDT receivers while Te or Tr is active.
                break;

            case V18ProbeTone.Hz1650:
                QueueDetectedMode(
                    V18Mode.V21Textphone,
                    V18Status.SwitchToV21Textphone,
                    modemCallingParty: true);
                break;

            case V18ProbeTone.Hz1270:
                QueueDetectedMode(
                    V18Mode.Bell103,
                    V18Status.SwitchToBell103,
                    modemCallingParty: false);
                break;

            case V18ProbeTone.Hz2225:
                QueueDetectedMode(
                    V18Mode.Bell103,
                    V18Status.SwitchToBell103,
                    modemCallingParty: true);
                break;

            case V18ProbeTone.Hz1400:
            case V18ProbeTone.Hz1800:
                // Exact Baudot rate selection is resolved by the parallel
                // framed FSK probes. A pure carrier alone does not guess a rate.
                break;
        }
    }


    private void EvaluateFormatProbes() {
        if (_pendingCiDetected || _pendingXciDetected || _pendingTxpDetected)
            return;

        V18FskFormatProbe? best = null;
        V18FskFormatProbe? second = null;

        foreach (V18FskFormatProbe probe in _formatProbes) {
            if (!probe.IsQualified)
                continue;

            if (best is null || probe.Score > best.Score) {
                second = best;
                best = probe;
            } else if (second is null || probe.Score > second.Score) {
                second = probe;
            }
        }

        if (best is null)
            return;

        int secondScore = second?.Score ?? int.MinValue / 2;
        if (second is not null && best.Score - secondScore < 8)
            return;

        QueueDetectedMode(best.Mode, best.Status, best.ModemCallingParty);
    }


    private void QueueAutomodingSignal(V18AutomodingSignal signal) {
        switch (signal) {
            case V18AutomodingSignal.Ci:
                _pendingCiDetected = true;
                break;
            case V18AutomodingSignal.Xci:
                _pendingXciDetected = true;
                break;
            case V18AutomodingSignal.Txp:
                _pendingTxpDetected = true;
                break;
        }
    }


    private void QueueDetectedMode(
        V18Mode mode,
        V18Status status,
        bool? modemCallingParty = null) {
        if (_pendingDetectedMode != V18Mode.None)
            return;

        _pendingDetectedMode = mode;
        _pendingDetectedStatus = status;
        _pendingModemCallingParty = modemCallingParty;
    }


    private void QueueReceivedDtmf(string digits, int length) {
        if (length <= 0 || digits.Length == 0)
            return;

        int count = Math.Min(length, digits.Length);
        _pendingDtmfDigits.Append(digits.AsSpan(0, count));
    }


    private void CommitPendingDtmf(bool allowModeSwitch) {
        if (_pendingDtmfDigits.Length == 0)
            return;

        string digits = _pendingDtmfDigits.ToString();
        _pendingDtmfDigits.Clear();

        if (allowModeSwitch && MonitorMode) {
            Logging.Log(
                (int)SpanLogSeverity.Flow,
                "Monitor detected DTMF without response\n");
            return;
        }

        if (allowModeSwitch && CurrentMode != V18Mode.Dtmf)
            SwitchMode(V18Mode.Dtmf, V18Status.SwitchToDtmf);

        if (CurrentMode == V18Mode.Dtmf)
            DtmfReceived(digits);
    }


    private void CommitPendingReceiveEvents() {
        if (_pendingTxpDetected) {
            Logging.Log((int)SpanLogSeverity.Flow, "TXP signal seen\n");
            HandleTxpDetected();
            ResetPendingReceiveEvents();
            return;
        }

        if (_pendingCiDetected || _pendingXciDetected) {
            Logging.Log(
                (int)SpanLogSeverity.Flow,
                "%s signal seen\n",
                _pendingXciDetected ? "XCI" : "CI");
            HandleCiOrXciDetected();
            ResetPendingReceiveEvents();
            return;
        }

        if (_pendingAnsDetected)
            HandleAnsDetected(_pendingAnswerTone);
        if (_pendingAnsLost)
            HandleAnsLost();

        if (_pendingDtmfDigits.Length > 0) {
            CommitPendingDtmf(allowModeSwitch: true);
            ResetPendingReceiveEvents(clearDtmf: false);
            return;
        }

        if (_pendingDetectedMode != V18Mode.None) {
            if (MonitorMode) {
                Logging.Log(
                    (int)SpanLogSeverity.Flow,
                    "Monitor detected %s without mode change\n",
                    V18.ModeToString((int)_pendingDetectedMode));
            } else {
                SwitchMode(_pendingDetectedMode, _pendingDetectedStatus, _pendingModemCallingParty);
            }
        }

        ResetPendingReceiveEvents();
    }


    private void ResetPendingReceiveEvents(bool clearDtmf = true) {
        _pendingDetectedMode = V18Mode.None;
        _pendingDetectedStatus = V18Status.SwitchToNone;
        _pendingModemCallingParty = null;
        _pendingCiDetected = false;
        _pendingXciDetected = false;
        _pendingTxpDetected = false;
        _pendingAnsDetected = false;
        _pendingAnsLost = false;
        _pendingAnswerTone = ModemConnectTone.None;
        if (clearDtmf)
            _pendingDtmfDigits.Clear();
    }


    private void AnswerToneReceived(
        object? userData,
        ModemConnectTone tone,
        int level,
        int duration) {
        _ = userData;
        _ = level;
        _ = duration;

        if (!Autobauding || IsLocalAutomodingConnectToneTransmission())
            return;

        if (tone == ModemConnectTone.None) {
            if (_answerTonePresent) {
                _answerTonePresent = false;
                _pendingAnsLost = true;
            }
            return;
        }

        if (tone is ModemConnectTone.Ans or
                    ModemConnectTone.AnsWithPhaseReversals or
                    ModemConnectTone.Ansam or
                    ModemConnectTone.AnsamWithPhaseReversals) {
            if (!_answerTonePresent) {
                _answerTonePresent = true;
                _pendingAnswerTone = tone;
                _pendingAnsDetected = true;
            }
        }
    }


    private void ReceiveFskBit(int bitOrStatus) {
        if (bitOrStatus == (int)SignalStatus.CarrierUp)
            _activeFskCarrierPresent = true;
        else if (bitOrStatus == (int)SignalStatus.CarrierDown)
            _activeFskCarrierPresent = false;

        HandleConnectedCarrierStatus(bitOrStatus);
        DecodeReceivedFskBit(bitOrStatus);
    }


    private void DecrementMessageTimer(int samples) {
        if (MessageInProgressTimer <= 0)
            return;

        MessageInProgressTimer -= samples;
        if (MessageInProgressTimer <= 0) {
            MessageInProgressTimer = 0;
            _receivedDtmf.Clear();
        }
    }
}

internal enum V18AutomodingSignal {
    Ci,
    Xci,
    Txp
}

internal sealed class V18AutomodingFskReceiver : IDisposable {
    private readonly FskRxState _receiver;
    private readonly FskSpec _spec;
    private readonly V18BitSequenceDetector _detector;
    private readonly V18AutomodingSignal _signal;
    private readonly Action<V18AutomodingSignal> _onDetected;
    private bool _disposed;

    public V18AutomodingFskReceiver(
        FskPreset preset,
        byte[] pattern,
        int maximumBitErrors,
        int requiredMatches,
        int matchWindowSamples,
        V18AutomodingSignal signal,
        Action<V18AutomodingSignal> onDetected) {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(onDetected);

        _detector = new V18BitSequenceDetector(
            pattern,
            maximumBitErrors,
            requiredMatches,
            matchWindowSamples);
        _signal = signal;
        _onDetected = onDetected;
        _spec = Fsk.GetPreset(preset);
        _receiver = Fsk.InitializeReceiver(
            null,
            _spec,
            FskFrameMode.Synchronous,
            static (userData, value) =>
                ((V18AutomodingFskReceiver)userData!).PutBit(value),
            this);
    }

    public void Process(ReadOnlySpan<short> samples) {
        ThrowIfDisposed();
        _detector.AdvanceSamples(samples.Length);
        Fsk.Receive(_receiver, samples);
    }

    public void FillIn(int samples) {
        ThrowIfDisposed();
        Fsk.ReceiveFillIn(_receiver, samples);
        Fsk.RestartReceiver(_receiver, _spec, FskFrameMode.Synchronous);
        _detector.Reset();
    }

    public void Dispose() {
        if (_disposed)
            return;
        _receiver.Dispose();
        _disposed = true;
    }

    private void PutBit(int bitOrStatus) {
        if (bitOrStatus < 0) {
            if (bitOrStatus == (int)SignalStatus.CarrierDown)
                _detector.Reset();
            return;
        }

        if (_detector.PutBit(bitOrStatus))
            _onDetected(_signal);
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);
}

internal sealed class V18BitSequenceDetector {
    private readonly ulong _pattern;
    private readonly ulong _mask;
    private readonly int _patternLength;
    private readonly int _maximumBitErrors;
    private readonly int _requiredMatches;
    private readonly int _matchWindowSamples;

    private ulong _shiftRegister;
    private int _receivedBits;
    private int _matches;
    private int _samplesSinceFirstMatch;
    private int _matchCooldown;

    public V18BitSequenceDetector(
        byte[] pattern,
        int maximumBitErrors,
        int requiredMatches,
        int matchWindowSamples) {
        ArgumentNullException.ThrowIfNull(pattern);
        if (pattern.Length is <= 0 or > 64)
            throw new ArgumentOutOfRangeException(nameof(pattern));
        if (maximumBitErrors < 0 || maximumBitErrors >= pattern.Length)
            throw new ArgumentOutOfRangeException(nameof(maximumBitErrors));
        if (requiredMatches <= 0)
            throw new ArgumentOutOfRangeException(nameof(requiredMatches));
        if (matchWindowSamples <= 0)
            throw new ArgumentOutOfRangeException(nameof(matchWindowSamples));

        _patternLength = pattern.Length;
        _maximumBitErrors = maximumBitErrors;
        _requiredMatches = requiredMatches;
        _matchWindowSamples = matchWindowSamples;
        _mask = pattern.Length == 64
            ? ulong.MaxValue
            : (1UL << pattern.Length) - 1UL;

        ulong value = 0;
        foreach (byte bit in pattern)
            value = (value << 1) | (uint)(bit & 1);
        _pattern = value;
    }

    public void AdvanceSamples(int samples) {
        if (_matches == 0)
            return;

        _samplesSinceFirstMatch = Math.Min(
            int.MaxValue - samples,
            _samplesSinceFirstMatch) + samples;
        if (_samplesSinceFirstMatch > _matchWindowSamples)
            ResetMatches();
    }

    public bool PutBit(int bit) {
        _shiftRegister = ((_shiftRegister << 1) | (uint)(bit & 1)) & _mask;
        if (_receivedBits < _patternLength)
            _receivedBits++;
        if (_matchCooldown > 0)
            _matchCooldown--;

        if (_receivedBits < _patternLength || _matchCooldown > 0)
            return false;

        int errors = BitOperations.PopCount(_shiftRegister ^ _pattern);
        if (errors > _maximumBitErrors)
            return false;

        if (_matches == 0)
            _samplesSinceFirstMatch = 0;
        _matches++;
        _matchCooldown = Math.Max(1, _patternLength / 2);

        if (_matches < _requiredMatches)
            return false;

        ResetMatches();
        return true;
    }

    public void Reset() {
        _shiftRegister = 0;
        _receivedBits = 0;
        ResetMatches();
    }

    private void ResetMatches() {
        _matches = 0;
        _samplesSinceFirstMatch = 0;
        _matchCooldown = 0;
    }
}

internal sealed class V18FskFormatProbe : IDisposable {
    private readonly FskRxState _receiver;
    private readonly FskSpec _spec;
    private readonly int _frameDataBits;
    private readonly FskParity _frameParity;
    private readonly int _stopBits;
    private readonly bool _ignoreParity;
    private readonly int _valueMask;
    private readonly bool _requiresBaudotShift;
    private readonly int _minimumCarrierSamples;
    private readonly int _minimumCharacters;
    private bool _carrierPresent;
    private bool _sawBaudotShift;
    private int _carrierSamples;
    private int _validCharacters;
    private int _rejectedCharacters;
    private bool _disposed;

    public V18FskFormatProbe(
        V18Mode mode,
        V18Status status,
        bool modemCallingParty,
        FskPreset preset,
        int dataBits,
        FskParity parity,
        int stopBits,
        bool requiresBaudotShift,
        bool ignoreParity,
        int minimumCarrierSamples,
        int minimumCharacters) {
        Mode = mode;
        Status = status;
        ModemCallingParty = modemCallingParty;
        _requiresBaudotShift = requiresBaudotShift;
        _minimumCarrierSamples = minimumCarrierSamples;
        _minimumCharacters = minimumCharacters;
        Preset = preset;
        _spec = Fsk.GetPreset(preset);
        _ignoreParity = ignoreParity && parity != FskParity.None;
        _frameDataBits = _ignoreParity ? dataBits + 1 : dataBits;
        _frameParity = _ignoreParity ? FskParity.None : parity;
        _stopBits = stopBits;
        _valueMask = (1 << dataBits) - 1;

        _receiver = Fsk.InitializeReceiver(
            null,
            _spec,
            FskFrameMode.Framed,
            static (userData, value) =>
                ((V18FskFormatProbe)userData!).PutFrame(value),
            this);
        Fsk.SetReceiveFrameParameters(_receiver, _frameDataBits, _frameParity, stopBits);
    }

    public V18Mode Mode { get; }

    public V18Status Status { get; }

    public FskPreset Preset { get; }

    public bool ModemCallingParty { get; }

    public int Score {
        get {
            int parityErrors = _ignoreParity
                ? 0
                : Fsk.GetParityErrors(_receiver, reset: false);
            int framingErrors = Fsk.GetFramingErrors(_receiver, reset: false);
            return _validCharacters * 12
                - _rejectedCharacters * 6
                - parityErrors * 8
                - framingErrors * 8;
        }
    }

    public bool IsQualified {
        get {
            if (!_carrierPresent || _carrierSamples < _minimumCarrierSamples)
                return false;
            if (_validCharacters < _minimumCharacters)
                return false;
            if (_requiresBaudotShift && !_sawBaudotShift)
                return false;

            int errors = (_ignoreParity
                    ? 0
                    : Fsk.GetParityErrors(_receiver, reset: false))
                + Fsk.GetFramingErrors(_receiver, reset: false)
                + _rejectedCharacters;
            return errors <= Math.Max(1, _validCharacters / 2) && Score >= 28;
        }
    }

    public void Process(ReadOnlySpan<short> samples) {
        ThrowIfDisposed();
        Fsk.Receive(_receiver, samples);
        if (_carrierPresent) {
            _carrierSamples = Math.Min(
                int.MaxValue - samples.Length,
                _carrierSamples) + samples.Length;
        }
    }

    public void FillIn(int samples) {
        ThrowIfDisposed();
        Fsk.ReceiveFillIn(_receiver, samples);
        Fsk.RestartReceiver(_receiver, _spec, FskFrameMode.Framed);
        Fsk.SetReceiveFrameParameters(_receiver, _frameDataBits, _frameParity, _stopBits);
        ResetObservation();
    }

    public void Dispose() {
        if (_disposed)
            return;
        _receiver.Dispose();
        _disposed = true;
    }

    private void PutFrame(int value) {
        if (value < 0) {
            if (value == (int)SignalStatus.CarrierUp) {
                ResetObservation();
                _carrierPresent = true;
            } else if (value == (int)SignalStatus.CarrierDown) {
                _carrierPresent = false;
                _carrierSamples = 0;
            }
            return;
        }

        int decodedValue = value & _valueMask;

        if (_requiresBaudotShift) {
            int code = decodedValue & 0x1F;
            if (code is 0x1B or 0x1F)
                _sawBaudotShift = true;
            if (code <= 0x1F)
                _validCharacters++;
            else
                _rejectedCharacters++;
            return;
        }

        if (decodedValue is >= 0x20 and <= 0x7E or 0x08 or 0x0A or 0x0D or 0x15)
            _validCharacters++;
        else
            _rejectedCharacters++;
    }

    private void ResetObservation() {
        _carrierPresent = false;
        _sawBaudotShift = false;
        _carrierSamples = 0;
        _validCharacters = 0;
        _rejectedCharacters = 0;
        Fsk.GetParityErrors(_receiver, reset: true);
        Fsk.GetFramingErrors(_receiver, reset: true);
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);
}

internal sealed class V18GoertzelBank {
    private readonly GoertzelState[] _bins;
    private readonly int _blockLength;
    private readonly float _minimumToneEnergy;
    private readonly float _toneToTotalEnergy;
    private float _totalEnergy;
    private int _sampleCount;

    public V18GoertzelBank(
        float[] frequencies,
        int blockLength,
        float minimumDbm0,
        float toneToTotalEnergy) {
        ArgumentNullException.ThrowIfNull(frequencies);
        if (blockLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(blockLength));

        _blockLength = blockLength;
        _minimumToneEnergy = ToneDetect.GoertzelThresholdDbm0(blockLength, minimumDbm0);
        _toneToTotalEnergy = toneToTotalEnergy;
        _bins = new GoertzelState[frequencies.Length];
        for (int i = 0; i < frequencies.Length; i++) {
            _bins[i] = new GoertzelState(
                ToneDetect.MakeGoertzelDescriptor(frequencies[i], blockLength));
        }
    }

    public bool AddSample(short sample) {
        float adjusted = ToneDetect.PreadjustAmplitude(sample);
        _totalEnergy += adjusted * adjusted;
        foreach (GoertzelState bin in _bins)
            bin.SampleAdjusted(adjusted);

        _sampleCount++;
        return _sampleCount >= _blockLength;
    }

    public int GetDominantTone() {
        if (_sampleCount < _blockLength || _totalEnergy <= 0.0f)
            return -1;

        int bestIndex = -1;
        float bestEnergy = 0.0f;
        for (int i = 0; i < _bins.Length; i++) {
            float energy = _bins[i].Result();
            if (energy > bestEnergy) {
                bestEnergy = energy;
                bestIndex = i;
            }
        }

        bool detected = bestEnergy >= _minimumToneEnergy &&
            bestEnergy > _toneToTotalEnergy * _totalEnergy;
        _totalEnergy = 0.0f;
        _sampleCount = 0;
        return detected ? bestIndex : -1;
    }

    public void Reset() {
        foreach (GoertzelState bin in _bins)
            bin.Reset();
        _totalEnergy = 0.0f;
        _sampleCount = 0;
    }
}
