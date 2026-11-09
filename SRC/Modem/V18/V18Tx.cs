/*
 * TKFaxEngine - managed C# port
 *
 * V18Tx.cs
 *
 * V.18 transmit signal path.
 */

#nullable enable

using TKFaxEngine.Audio;

namespace TKFaxEngine.Modem.V18;

internal enum V18TxStage {
    None,
    Connected,
    CallingInitialSilence,
    CallingCi,
    CallingCiSilence,
    CallingXci,
    CallingPostXciSilence,
    CallingAnsSilence,
    CallingTxp,
    CallingMonitor,
    AnswerMonitor,
    AnswerAnsam,
    AnswerTxpSilence,
    AnswerTxp,
    ProbeAnswerTone,
    ProbeSilence,
    ProbeCarrier,
    ProbeFskMessage,
    ProbeDtmfMessage,
    ProbePause
}

public sealed partial class V18State {
    private V18TxStage _txStage;
    private int _txStageSamplesRemaining;
    private int _ciBurstsSent;
    private int _probeIndex;
    private V18Mode _probeMode;

    private FskTxState? _automodingFskTransmitter;
    private byte[]? _automodingBits;
    private int _automodingBitIndex;
    private int _automodingBitRepeats;
    private int _automodingBitRepeatLimit;
    private bool _automodingSequenceComplete;

    private ModemConnectTonesTxState? _connectToneTransmitter;
    private tone_gen_state_t? _probeToneTransmitter;
    private AsyncTransmitter? _probeAsyncTransmitter;

    private bool _stopCallingTxpAtBoundary;
    private int _probeDtmfCharacterIndex;
    private int _probeCharacterIndex;
    private int _probeBaudotShift;
    private int _probeBaudotCharactersSinceShift;
    private int _probePendingByte;

    /// <summary>Produces V.18 transmit samples for automoding or connected operation.</summary>
    public int Transmit(Span<short> samples) {
        ThrowIfDisposed();
        if (samples.IsEmpty)
            return 0;

        if (TransmitSuppressionTimer > 0)
            TransmitSuppressionTimer = Math.Max(0, TransmitSuppressionTimer - samples.Length);

        if (Autobauding && MonitorMode) {
            samples.Clear();
            return samples.Length;
        }

        int v8Samples = TransmitV8(samples);
        if (v8Samples >= 0)
            return v8Samples;

        if (Autobauding &&
            TransmitState is not V18TransmitState.OriginatingConnected and
                                 not V18TransmitState.AnsweringConnected) {
            return TransmitAutomoding(samples);
        }

        return TransmitConnected(samples);
    }

    /// <summary>Array overload retaining the native v18_tx call shape.</summary>
    public int Transmit(short[] samples, int maximumLength) {
        ArgumentNullException.ThrowIfNull(samples);
        if (maximumLength < 0 || maximumLength > samples.Length)
            throw new ArgumentOutOfRangeException(nameof(maximumLength));
        return Transmit(samples.AsSpan(0, maximumLength));
    }

    private void InitializeTransmitPath() {
        DisposeAutomodingTransmitters();
        _ciBurstsSent = 0;
        _probeIndex = 0;
        _probeMode = V18Mode.None;
        _stopCallingTxpAtBoundary = false;
        _probeDtmfCharacterIndex = 0;
        _probeCharacterIndex = 0;
        _probeBaudotShift = 2;
        _probeBaudotCharactersSinceShift = 0;
        _probePendingByte = -1;

        if (!Autobauding) {
            _txStage = V18TxStage.Connected;
            _txStageSamplesRemaining = 0;
            return;
        }

        if (MonitorMode) {
            TransmitState = V18TransmitState.Answering1;
            _txStage = V18TxStage.CallingMonitor;
            _txStageSamplesRemaining = int.MaxValue;
            TaTimer = 0;
            return;
        }

        if (CallingParty) {
            TransmitState = V18TransmitState.Originating1;
            StartSilence(V18TxStage.CallingInitialSilence, InitialCallingSilenceMilliseconds);
        } else {
            TransmitState = V18TransmitState.Answering1;
            _txStage = V18TxStage.AnswerMonitor;
            TaTimer = TaInterval;
            _txStageSamplesRemaining = TaTimer;
        }
    }

    private int TransmitAutomoding(Span<short> samples) {
        int offset = 0;
        while (offset < samples.Length) {
            int generated = _txStage switch {
                V18TxStage.CallingInitialSilence or
                V18TxStage.CallingCiSilence or
                V18TxStage.CallingPostXciSilence or
                V18TxStage.CallingAnsSilence or
                V18TxStage.CallingMonitor or
                V18TxStage.AnswerMonitor or
                V18TxStage.AnswerTxpSilence or
                V18TxStage.ProbeSilence or
                V18TxStage.ProbePause => GenerateSilence(samples[offset..]),

                V18TxStage.CallingCi or
                V18TxStage.CallingXci or
                V18TxStage.CallingTxp or
                V18TxStage.AnswerTxp => GenerateTimedFsk(samples[offset..]),

                V18TxStage.AnswerAnsam or
                V18TxStage.ProbeAnswerTone => GenerateConnectTone(samples[offset..]),

                V18TxStage.ProbeCarrier => GenerateProbeTone(samples[offset..]),
                V18TxStage.ProbeFskMessage => GenerateProbeFsk(samples[offset..]),
                V18TxStage.ProbeDtmfMessage => GenerateProbeDtmf(samples[offset..]),
                _ => 0
            };

            if (generated > 0) {
                offset += generated;
                continue;
            }

            V18TxStage before = _txStage;
            AdvanceTransmitStage();

            if (!Autobauding || _txStage == V18TxStage.Connected) {
                int connected = TransmitConnected(samples[offset..]);
                if (connected > 0) {
                    offset += connected;
                    continue;
                }
                samples[offset..].Clear();
                return samples.Length;
            }

            if (_txStage == before) {
                samples[offset..].Clear();
                return samples.Length;
            }
        }
        return samples.Length;
    }

    private int TransmitConnected(Span<short> samples) {
        if (_transmitSignalOn == 0)
            return 0;

        if ((CurrentMode & V18Mode.Dtmf) != 0) {
            int offset = 0;
            while (offset < samples.Length) {
                LoadNextDtmfCharacter();
                int generated = Dtmf.dtmf_tx(_dtmfTransmitter, samples[offset..]);
                if (generated > 0) {
                    ReceiveSuppressionTimer =
                        MillisecondsToSamples(DtmfReceiveSuppressionMilliseconds);
                    offset += generated;
                    continue;
                }

                if (_queue.Empty() && _dtmfTransmitter.QueuedDigits == 0)
                    _transmitSignalOn = 0;
                break;
            }
            return offset;
        }

        if (_fskTransmitter is null || _asyncTransmitter is null)
            return 0;

        V18Mode mode = CurrentMode & ~V18Mode.RepetitiveShiftsOption;
        if (_transmitSignalOn == 1 && mode == V18Mode.Edt) {
            _asyncTransmitter.SetPresendBits(
                CalculatePresendBits(
                    FskPreset.V21Channel1At110Bps,
                    EdtCarrierPreambleMilliseconds));
            _transmitSignalOn = 2;
        }

        int count = Fsk.Transmit(_fskTransmitter, samples);
        if (count <= 0 &&
            mode is (V18Mode.Weitbrecht5Bit4545 or
                     V18Mode.Weitbrecht5Bit476 or
                     V18Mode.Weitbrecht5Bit50 or
                     V18Mode.Edt)) {
            _transmitSignalOn = 0;
        }
        return count;
    }

    private int GenerateSilence(Span<short> samples) {
        if (_txStageSamplesRemaining <= 0)
            return 0;

        int count = Math.Min(samples.Length, _txStageSamplesRemaining);
        samples[..count].Clear();
        _txStageSamplesRemaining -= count;
        DecrementActiveTimer(count);
        return count;
    }

    private int GenerateTimedFsk(Span<short> samples) {
        if (_txStageSamplesRemaining <= 0 || _automodingFskTransmitter is null)
            return 0;

        int count = Math.Min(samples.Length, _txStageSamplesRemaining);
        int generated = Fsk.Transmit(_automodingFskTransmitter, samples[..count]);

        if (_automodingFskTransmitter.IsShutdown) {
            _txStageSamplesRemaining = 0;
            return generated;
        }

        if (generated < count)
            samples.Slice(generated, count - generated).Clear();
        _txStageSamplesRemaining -= count;
        return count;
    }

    private int GenerateConnectTone(Span<short> samples) {
        if (_txStageSamplesRemaining <= 0 || _connectToneTransmitter is null)
            return 0;

        int count = Math.Min(samples.Length, _txStageSamplesRemaining);
        int generated = ModemConnectTones.Transmit(_connectToneTransmitter, samples[..count]);
        if (generated < count)
            samples.Slice(generated, count - generated).Clear();
        _txStageSamplesRemaining -= count;
        DecrementActiveTimer(count);
        return count;
    }

    private int GenerateProbeTone(Span<short> samples) {
        if (_txStageSamplesRemaining <= 0 || _probeToneTransmitter is null)
            return 0;

        int count = Math.Min(samples.Length, _txStageSamplesRemaining);
        int generated = tone_generate.tone_gen(_probeToneTransmitter, samples[..count], count);
        if (generated < count)
            samples.Slice(generated, count - generated).Clear();
        _txStageSamplesRemaining -= count;
        TcTimer = Math.Max(0, TcTimer - count);
        return count;
    }

    private int GenerateProbeFsk(Span<short> samples) {
        if (_automodingFskTransmitter is null)
            return 0;
        int generated = Fsk.Transmit(_automodingFskTransmitter, samples);
        return generated > 0 ? generated : 0;
    }

    private int GenerateProbeDtmf(Span<short> samples) {
        FeedProbeDtmf();
        int generated = Dtmf.dtmf_tx(_dtmfTransmitter, samples);
        if (generated > 0) {
            ReceiveSuppressionTimer =
                MillisecondsToSamples(DtmfReceiveSuppressionMilliseconds);
        }
        FeedProbeDtmf();
        if (generated == 0 &&
            _dtmfTransmitter.QueuedDigits == 0 &&
            _probeDtmfCharacterIndex >= StoredMessage.Length) {
            return 0;
        }
        return generated;
    }

    private void AdvanceTransmitStage() {
        switch (_txStage) {
            case V18TxStage.CallingInitialSilence:
                StartCallingCi();
                break;

            case V18TxStage.CallingCi:
                DisposeAutomodingFsk();
                _ciBurstsSent++;
                StartSilence(
                    V18TxStage.CallingCiSilence,
                    _ciBurstsSent < 3
                        ? CiInterBurstSilenceMilliseconds
                        : CiToXciSilenceMilliseconds);
                break;

            case V18TxStage.CallingCiSilence:
                if (_ciBurstsSent < 3)
                    StartCallingCi();
                else
                    StartCallingXci();
                break;

            case V18TxStage.CallingXci:
                DisposeAutomodingFsk();
                StartSilence(V18TxStage.CallingPostXciSilence, XciPostSilenceMilliseconds);
                break;

            case V18TxStage.CallingPostXciSilence:
                _ciBurstsSent = 0;
                StartCallingCi();
                break;

            case V18TxStage.CallingAnsSilence:
                StartCallingTxp();
                break;

            case V18TxStage.CallingTxp:
                DisposeAutomodingFsk();
                _txStage = V18TxStage.CallingMonitor;
                _txStageSamplesRemaining = int.MaxValue;
                break;

            case V18TxStage.AnswerMonitor:
                StartNextProbe();
                break;

            case V18TxStage.AnswerAnsam:
                DisposeConnectTone();
                TtTimer = 0;
                RestartAutomodingMonitor();
                break;

            case V18TxStage.AnswerTxpSilence:
                StartAnswerTxp();
                break;

            case V18TxStage.AnswerTxp:
                DisposeAutomodingFsk();
                SwitchMode(V18Mode.V18Textphone, V18Status.SwitchToV18Textphone);
                _txStage = V18TxStage.Connected;
                break;

            case V18TxStage.ProbeAnswerTone:
                DisposeConnectTone();
                StartSilence(
                    V18TxStage.ProbeSilence,
                    ProbeAnswerToneSilenceMilliseconds);
                break;

            case V18TxStage.ProbeSilence:
                StartPreparedProbe();
                break;

            case V18TxStage.ProbeCarrier:
                DisposeProbeTone();
                StartNextProbe();
                break;

            case V18TxStage.ProbeFskMessage:
                DisposeAutomodingFsk();
                DisposeProbeFsk();
                ResetTransmitTimers();
                TmTimer = TmInterval;
                _txStage = V18TxStage.ProbePause;
                _txStageSamplesRemaining = TmTimer;
                break;

            case V18TxStage.ProbeDtmfMessage:
                ResetTransmitTimers();
                TmTimer = TmInterval;
                _txStage = V18TxStage.ProbePause;
                _txStageSamplesRemaining = TmTimer;
                break;

            case V18TxStage.ProbePause:
                StartNextProbe();
                break;
        }
    }

    private void StartCallingCi() {
        TransmitState = V18TransmitState.Originating1;
        StartTimedFsk(
            V18TxStage.CallingCi,
            FskPreset.V21Channel1,
            CiBits,
            repeatLimit: 1,
            CiSignalMilliseconds);
    }

    private void StartCallingXci() {
        TransmitState = V18TransmitState.Originating1;
        StartTimedFsk(
            V18TxStage.CallingXci,
            FskPreset.V23Channel1,
            XciBits,
            repeatLimit: 1,
            milliseconds: XciSignalMilliseconds);
    }

    private void StartCallingTxp() {
        TransmitState = V18TransmitState.Originating3;
        StartContinuousFsk(
            V18TxStage.CallingTxp,
            FskPreset.V21Channel1,
            TxpBits);
    }

    private void StartAnswerTxp() {
        TransmitState = V18TransmitState.Answering3;
        StartTimedFsk(
            V18TxStage.AnswerTxp,
            FskPreset.V21Channel2,
            TxpBits,
            repeatLimit: TxpAnswerRepetitions,
            milliseconds: AnswerTxpMilliseconds);
    }

    private void StartNextProbe() {
        V18Mode[] sequence = GetAutomodingSequence();
        if (_probeIndex >= sequence.Length)
            _probeIndex = 0;

        _probeMode = sequence[_probeIndex++];
        TransmitState = V18TransmitState.Answering3;

        if (IsCarrierProbeMode(_probeMode)) {
            StartConnectTone(
                V18TxStage.ProbeAnswerTone,
                ModemConnectTone.Ans,
                ProbeAnswerToneMilliseconds);
        } else {
            StartPreparedProbe();
        }
    }

    private static bool IsCarrierProbeMode(V18Mode mode) {
        return mode is V18Mode.Bell103 or
                       V18Mode.V23Videotex or
                       V18Mode.V21Textphone or
                       V18Mode.V18Textphone;
    }

    private void StartPreparedProbe() {
        switch (_probeMode) {
            case V18Mode.Weitbrecht5Bit4545:
            case V18Mode.Weitbrecht5Bit476:
            case V18Mode.Weitbrecht5Bit50:
                StartProbeFskMessage(_probeMode);
                break;

            case V18Mode.Edt:
                StartProbeFskMessage(V18Mode.Edt);
                break;

            case V18Mode.Dtmf:
                StartProbeDtmfMessage();
                break;

            case V18Mode.Bell103:
                StartCarrierProbe(2225);
                break;

            case V18Mode.V23Videotex:
                StartCarrierProbe(1300);
                break;

            case V18Mode.V21Textphone:
            case V18Mode.V18Textphone:
                StartCarrierProbe(1650);
                break;

            default:
                StartNextProbe();
                break;
        }
    }

    private void StartCarrierProbe(int frequency) {
        ResetTransmitTimers();
        DisposeAutomodingFsk();
        DisposeConnectTone();
        DisposeProbeFsk();
        DisposeProbeTone();
        ResetAutomodingDtmf();

        tone_gen_descriptor_t descriptor = tone_generate.tone_gen_descriptor_init(
            null,
            frequency,
            -11,
            0,
            0,
            TcInterval * 1000 / SampleRate,
            0,
            0,
            0,
            0)!;
        _probeToneTransmitter = tone_generate.tone_gen_init(null, descriptor);
        TcTimer = TcInterval;
        _txStageSamplesRemaining = TcTimer;
        _txStage = V18TxStage.ProbeCarrier;
    }

    private void StartProbeFskMessage(V18Mode mode) {
        ResetTransmitTimers();
        DisposeAutomodingFsk();
        DisposeConnectTone();
        DisposeProbeFsk();
        DisposeProbeTone();
        ResetAutomodingDtmf();

        _probeMode = mode;
        _probeCharacterIndex = 0;
        _probeBaudotShift = 2;
        _probeBaudotCharactersSinceShift = 0;
        _probePendingByte = -1;

        FskPreset preset;
        int dataBits;
        AsyncParity parity;
        int stopBits;
        int presendBits;

        if (mode == V18Mode.Edt) {
            preset = FskPreset.V21Channel1At110Bps;
            dataBits = 7;
            parity = AsyncParity.Even;
            stopBits = 2;
            presendBits = CalculatePresendBits(
                preset,
                EdtCarrierPreambleMilliseconds);
        } else {
            preset = mode switch {
                V18Mode.Weitbrecht5Bit4545 => FskPreset.Weitbrecht4545,
                V18Mode.Weitbrecht5Bit50 => FskPreset.Weitbrecht50,
                _ => FskPreset.Weitbrecht476
            };
            dataBits = 5;
            parity = AsyncParity.None;
            stopBits = 2;
            presendBits = CalculatePresendBits(
                preset,
                BaudotCarrierPreambleMilliseconds);
        }

        _probeAsyncTransmitter = new AsyncTransmitter(
            dataBits,
            parity,
            stopBits,
            false,
            ProbeGetAsyncByte,
            this);
        _probeAsyncTransmitter.SetPresendBits(presendBits);
        _automodingFskTransmitter = Fsk.InitializeTransmitter(
            null,
            Fsk.GetPreset(preset),
            static userData => ((V18State)userData!)._probeAsyncTransmitter!.GetBit(),
            this);
        _txStage = V18TxStage.ProbeFskMessage;
    }


    private static int CalculatePresendBits(
        FskPreset preset,
        int milliseconds) {
        FskSpec spec = Fsk.GetPreset(preset);
        int scaled = checked(milliseconds * spec.BaudRateHundredths);
        return (scaled + 99_999) / 100_000;
    }

    private void LoadNextDtmfCharacter() {
        if (_dtmfTransmitter.QueuedDigits > 0 || TransmitSuppressionTimer > 0)
            return;

        int character = _queue.ReadByte();
        if (character < 0)
            return;

        string sequence;
        if ((character & 0x80) != 0) {
            sequence = character switch {
                0xC6 => AsciiToDtmf[0x5B],
                0xD8 => AsciiToDtmf[0x5C],
                0xC5 => AsciiToDtmf[0x5D],
                0xE6 => AsciiToDtmf[0x7B],
                0xF8 => AsciiToDtmf[0x7C],
                0xE5 => AsciiToDtmf[0x7D],
                _ => AsciiToDtmf[(byte)'?']
            };
        } else {
            sequence = AsciiToDtmf[character];
        }

        if (sequence.Length == 0)
            return;

        Dtmf.dtmf_tx_put(_dtmfTransmitter, sequence, sequence.Length);
        ReceiveSuppressionTimer = MillisecondsToSamples(DtmfReceiveSuppressionMilliseconds);
    }


    private void StartProbeDtmfMessage() {
        ResetTransmitTimers();
        DisposeAutomodingFsk();
        DisposeConnectTone();
        DisposeProbeTone();
        DisposeProbeFsk();
        _dtmfTransmitter.Initialize(null, null);
        Dtmf.dtmf_tx_set_timing(_dtmfTransmitter, DtmfOnMilliseconds, DtmfOffMilliseconds);
        _probeDtmfCharacterIndex = 0;
        FeedProbeDtmf();
        _txStage = V18TxStage.ProbeDtmfMessage;
    }

    private void FeedProbeDtmf() {
        while (_probeDtmfCharacterIndex < StoredMessage.Length &&
               _dtmfTransmitter.QueuedDigits < 32) {
            char character = StoredMessage[_probeDtmfCharacterIndex];
            string sequence = character switch {
                '\u00E6' => "#*1",
                '\u00F8' => "#*2",
                '\u00E5' => "#*3",
                '\u00C6' => "#*4",
                '\u00D8' => "#*5",
                '\u00C5' => "#*6",
                '\u2028' or '\u2029' => "**9",
                <= '\u007F' => AsciiToDtmf[(byte)character],
                _ => AsciiToDtmf[(byte)'?']
            };
            if (sequence.Length > 0 &&
                Dtmf.dtmf_tx_put(_dtmfTransmitter, sequence, sequence.Length) != 0) {
                return;
            }
            _probeDtmfCharacterIndex++;
        }
    }

    private int ProbeGetAsyncByte(object? userData) {
        _ = userData;

        if (_probePendingByte >= 0) {
            int pending = _probePendingByte;
            _probePendingByte = -1;
            return pending;
        }

        while (_probeCharacterIndex < StoredMessage.Length) {
            char character = StoredMessage[_probeCharacterIndex++];
            byte legacy = character <= 0x7F
                ? (byte)character
                : (byte)'?';

            if (_probeMode == V18Mode.Edt)
                return legacy;

            byte code = BaudotEncodeTable[legacy & 0x7F];
            if (code == 0xFF)
                code = BaudotEncodeTable[(byte)'?'];

            int dataCode = code & 0x1F;
            if ((code & 0x40) != 0) {
                _probeBaudotCharactersSinceShift++;
                return dataCode;
            }

            int wantedShift = (code & 0x80) != 0 ? 1 : 0;
            bool needsShift = _probeBaudotShift != wantedShift;
            bool periodicShift = _probeBaudotCharactersSinceShift >= 72;

            _probeBaudotCharactersSinceShift++;
            if (!needsShift && !periodicShift)
                return dataCode;

            _probeBaudotShift = wantedShift;
            _probeBaudotCharactersSinceShift = 1;
            _probePendingByte = dataCode;
            return wantedShift != 0
                ? BaudotFigureShift
                : BaudotLetterShift;
        }

        return (int)SignalStatus.EndOfData;
    }

    private void StartTimedFsk(
        V18TxStage stage,
        FskPreset preset,
        byte[] bits,
        int repeatLimit,
        int milliseconds) {
        PrepareAutomodingFsk(stage, preset, bits, repeatLimit);
        _txStageSamplesRemaining = MillisecondsToSamples(milliseconds);
    }

    private void StartContinuousFsk(
        V18TxStage stage,
        FskPreset preset,
        byte[] bits) {
        PrepareAutomodingFsk(stage, preset, bits, repeatLimit: 0);
        _txStageSamplesRemaining = int.MaxValue;
    }

    private void PrepareAutomodingFsk(
        V18TxStage stage,
        FskPreset preset,
        byte[] bits,
        int repeatLimit) {
        ResetTransmitTimers();
        DisposeAutomodingFsk();
        DisposeConnectTone();
        DisposeProbeTone();
        DisposeProbeFsk();
        ResetAutomodingDtmf();

        _automodingBits = bits;
        _automodingBitIndex = 0;
        _automodingBitRepeats = 0;
        _automodingBitRepeatLimit = repeatLimit;
        _automodingSequenceComplete = false;
        _automodingFskTransmitter = Fsk.InitializeTransmitter(
            null,
            Fsk.GetPreset(preset),
            static userData => ((V18State)userData!).GetAutomodingBit(),
            this);
        _txStage = stage;
    }

    private int GetAutomodingBit() {
        if (_automodingSequenceComplete)
            return 1;

        byte[] bits = _automodingBits ?? TxpBits;
        if (_automodingBitIndex >= bits.Length) {
            _automodingBitIndex = 0;
            _automodingBitRepeats++;

            if (_txStage == V18TxStage.CallingTxp &&
                _stopCallingTxpAtBoundary) {
                _automodingSequenceComplete = true;
                return (int)FskSignalStatus.EndOfData;
            }

            if (_automodingBitRepeatLimit > 0 &&
                _automodingBitRepeats >= _automodingBitRepeatLimit) {
                _automodingSequenceComplete = true;
                return 1;
            }
        }

        return bits[_automodingBitIndex++];
    }

    private void StartConnectTone(
        V18TxStage stage,
        ModemConnectTone tone,
        int milliseconds) {
        ResetTransmitTimers();
        DisposeAutomodingFsk();
        DisposeConnectTone();
        DisposeProbeTone();
        DisposeProbeFsk();
        ResetAutomodingDtmf();

        _connectToneTransmitter = ModemConnectTones.TransmitInit(tone);
        _connectToneTransmitter.DurationTimer = MillisecondsToSamples(milliseconds);
        _txStageSamplesRemaining = MillisecondsToSamples(milliseconds);
        _txStage = stage;
    }

    private void StartSilence(V18TxStage stage, int milliseconds) {
        ResetTransmitTimers();
        DisposeAutomodingFsk();
        DisposeConnectTone();
        DisposeProbeTone();
        DisposeProbeFsk();
        ResetAutomodingDtmf();
        _txStage = stage;
        _txStageSamplesRemaining = MillisecondsToSamples(milliseconds);
    }

    private void PauseAutomodingTransmitForToneTimer() {
        DisposeAutomodingTransmitters();
        ResetTransmitTimers();
        _txStage = V18TxStage.CallingMonitor;
        _txStageSamplesRemaining = int.MaxValue;
    }

    private void ResetTransmitTimers() {
        TaTimer = 0;
        TcTimer = 0;
        TmTimer = 0;
        TtTimer = 0;
    }

    private void DecrementActiveTimer(int count) {
        if (_txStage == V18TxStage.AnswerMonitor)
            TaTimer = Math.Max(0, TaTimer - count);
        else if (_txStage == V18TxStage.AnswerAnsam)
            TtTimer = Math.Max(0, TtTimer - count);
        else if (_txStage == V18TxStage.ProbePause)
            TmTimer = Math.Max(0, TmTimer - count);
    }

    private void ResetAutomodingDtmf() {
        _dtmfTransmitter.Initialize(null, null);
        Dtmf.dtmf_tx_set_timing(
            _dtmfTransmitter,
            DtmfOnMilliseconds,
            DtmfOffMilliseconds);
        _probeDtmfCharacterIndex = 0;
    }

    private void DisposeAutomodingTransmitters() {
        DisposeAutomodingFsk();
        DisposeConnectTone();
        DisposeProbeTone();
        DisposeProbeFsk();
        ResetAutomodingDtmf();
    }

    private void DisposeAutomodingFsk() {
        _automodingFskTransmitter?.Dispose();
        _automodingFskTransmitter = null;
        _automodingBits = null;
        _automodingBitIndex = 0;
        _automodingBitRepeats = 0;
        _automodingBitRepeatLimit = 0;
        _automodingSequenceComplete = false;
    }

    private void DisposeConnectTone() {
        _connectToneTransmitter?.Dispose();
        _connectToneTransmitter = null;
    }

    private void DisposeProbeTone() {
        if (_probeToneTransmitter is not null)
            tone_generate.tone_gen_release(_probeToneTransmitter);
        _probeToneTransmitter = null;
    }

    private void DisposeProbeFsk() {
        _probeAsyncTransmitter?.Dispose();
        _probeAsyncTransmitter = null;
        _probeCharacterIndex = 0;
        _probeBaudotShift = 2;
        _probeBaudotCharactersSinceShift = 0;
        _probePendingByte = -1;
    }

}
