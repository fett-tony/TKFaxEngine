/*
 * TKFaxEngine - managed C# port
 *
 * V29Rx.cs
 *
 * Combined and ported from v29rx.c and v29rx.h. The required V.29 receive
 * RRC coefficients, constellation map and Godard timing descriptor are
 * embedded so the receiver remains a single managed source file.
 *
 * Original implementation by Steve Underwood.
 * Licensed under the GNU Lesser General Public License version 2.1.
 */

#nullable enable

namespace TKFaxEngine.Modem.V29;

/// <summary>
/// ITU-T V.29 receiver for 4800, 7200 and 9600 bit/s signals sampled at 8000 Hz.
/// The implementation follows the floating-point path of the native TKFaxEngine code.
/// </summary>
public sealed class V29Rx {
    public const int SampleRate = 8000;
    public const int BaudRate = 2400;
    public const int EqualizerLength = 33;
    public const int EqualizerPreLength = 16;
    public const int ReceiveFilterSteps = 27;
    public const int PulseShaperCoefficientSets = 48;
    public const float ConstellationScalingFactor = 1.0f;

    private const float CarrierNominalFrequency = 1700.0f;
    private const float EqualizerDelta = 0.21f;
    private const float PulseShaperGain = 1.0f;
    private const int TrainingSegment2Length = 128;
    private const int TrainingSegment3Length = 384;
    private const int TrainingSegment4Length = 48;
    private const float Dbm0MaxPower = 6.16f;
    private const float LmsLeakRate = 0.9999f;

    /// <summary>Receives decoded bits and native negative signal-status values.</summary>
    public delegate void PutBitHandler(int bitOrStatus);

    /// <summary>Receives modem status changes.</summary>
    public delegate void ModemStatusHandler(V29RxStatus status);

    /// <summary>Receives equalized and target QAM symbols.</summary>
    public delegate void QamReportHandler(ComplexSample received, ComplexSample target, int constellationState);

    public enum V29RxStatus {
        CarrierDown = -1,
        CarrierUp = -2,
        TrainingInProgress = -3,
        TrainingSucceeded = -4,
        TrainingFailed = -5,
    }

    public readonly struct ComplexSample {
        public ComplexSample(float real, float imaginary) {
            Real = real;
            Imaginary = imaginary;
        }

        public float Real { get; }
        public float Imaginary { get; }

        public override string ToString() => $"({Real}, {Imaginary})";
    }

    private struct ComplexF {
        public ComplexF(float real, float imaginary) {
            Re = real;
            Im = imaginary;
        }

        public float Re;
        public float Im;
    }

    private enum TrainingStage {
        NormalOperation = 0,
        SymbolAcquisition,
        LogPhase,
        WaitForCdcd,
        TrainOnCdcd,
        TrainOnCdcdAndTest,
        TestOnes,
        Parked,
    }

    private sealed class PowerMeter {
        private int _shift;
        private int _reading;

        public void Initialize(int shift) {
            _shift = shift;
            _reading = 0;
        }

        public int Update(short amplitude) {
            int square = amplitude * amplitude;
            _reading += (square - _reading) >> _shift;
            return _reading;
        }

        public float CurrentDbm0 {
            get {
                if (_reading <= 0)
                    return -96.329f + Dbm0MaxPower;

                double fullScaleSquared = 32767.0 * 32767.0;
                return (float)(10.0 * Math.Log10(_reading / fullScaleSquared + 1.0e-10) + Dbm0MaxPower);
            }
        }

        public static int LevelDbm0(float level) {
            level -= Dbm0MaxPower;
            if (level > 0.0f)
                level = 0.0f;

            double ratio = Math.Pow(10.0, level / 10.0);
            return (int)(ratio * 32767.0 * 32767.0);
        }
    }

    private sealed class GodardTed {
        private static readonly float[] LowBandEdgeCoefficients =
        {
            1.829281f,
            -0.980100f,
            -0.378857f,
        };

        private static readonly float[] HighBandEdgeCoefficients =
        {
            -1.285907f,
            -0.980100f,
            -0.752802f,
        };

        private const float MixedBandEdgesCoefficient3 = -0.932130f;
        private const float CoarseTrigger = 1000.0f;
        private const float FineTrigger = 30.0f;
        private const int CoarseStep = 5;
        private const int FineStep = 1;

        private readonly float[] _lowBandEdge = new float[2];
        private readonly float[] _highBandEdge = new float[2];
        private readonly float[] _dcFilter = new float[2];
        private float _baudPhase;

        public int TotalBaudTimingCorrection { get; private set; }

        public void Initialize() {
            Array.Clear(_lowBandEdge, 0, _lowBandEdge.Length);
            Array.Clear(_highBandEdge, 0, _highBandEdge.Length);
            Array.Clear(_dcFilter, 0, _dcFilter.Length);
            _baudPhase = 0.0f;
            TotalBaudTimingCorrection = 0;
        }

        public void Receive(float sample) {
            float value = _lowBandEdge[0] * LowBandEdgeCoefficients[0]
                        + _lowBandEdge[1] * LowBandEdgeCoefficients[1]
                        + sample;
            _lowBandEdge[1] = _lowBandEdge[0];
            _lowBandEdge[0] = value;

            value = _highBandEdge[0] * HighBandEdgeCoefficients[0]
                  + _highBandEdge[1] * HighBandEdgeCoefficients[1]
                  + sample;
            _highBandEdge[1] = _highBandEdge[0];
            _highBandEdge[0] = value;
        }

        public int PerBaud() {
            float value = _lowBandEdge[1] * _highBandEdge[0] * LowBandEdgeCoefficients[2]
                        - _lowBandEdge[0] * _highBandEdge[1] * HighBandEdgeCoefficients[2]
                        + _lowBandEdge[1] * _highBandEdge[1] * MixedBandEdgesCoefficient3;

            float filtered = value - _dcFilter[1];
            _dcFilter[1] = _dcFilter[0];
            _dcFilter[0] = value;
            _baudPhase -= filtered;

            float magnitude = MathF.Abs(_baudPhase);
            if (magnitude <= FineTrigger)
                return 0;

            int correction = magnitude > CoarseTrigger ? CoarseStep : FineStep;
            if (_baudPhase < 0.0f)
                correction = -correction;

            TotalBaudTimingCorrection += correction;
            return correction;
        }
    }

    private PutBitHandler? _putBit;
    private ModemStatusHandler? _statusHandler;
    private QamReportHandler? _qamReport;

    private float _agcScaling;
    private float _agcScalingSave;
    private float _equalizerDelta;
    private readonly ComplexF[] _equalizerCoefficients = new ComplexF[EqualizerLength];
    private readonly ComplexF[] _savedEqualizerCoefficients = new ComplexF[EqualizerLength];
    private readonly ComplexF[] _equalizerBuffer = new ComplexF[EqualizerLength];
    private float _trainingError;
    private float _carrierTrackP;
    private float _carrierTrackI;
    private readonly float[] _rrcFilter = new float[ReceiveFilterSteps];
    private readonly GodardTed _godard = new GodardTed();
    private int _rrcFilterStep;
    private uint _scrambleRegister;
    private byte _trainingScrambleRegister;
    private int _trainingCd;
    private bool _oldTrain;
    private TrainingStage _trainingStage;
    private int _trainingCount;
    private short _lastSample;
    private int _signalPresent;
    private bool _carrierDropPending;
    private int _lowSamples;
    private short _highSample;
    private uint _carrierPhase;
    private int _carrierPhaseRate;
    private int _savedCarrierPhaseRate;
    private readonly PowerMeter _power = new PowerMeter();
    private int _carrierOnPower;
    private int _carrierOffPower;
    private int _equalizerStep;
    private int _equalizerPutStep;
    private int _equalizerSkip;
    private int _baudHalf;
    private readonly int[] _lastAngles = new int[2];
    private readonly int[] _differenceAngles = new int[16];
    private int _constellationState;

    public V29Rx(int bitRate, PutBitHandler? putBit = null) {
        _putBit = putBit;
        SetSignalCutoff(-28.5f);
        if (Restart(bitRate, false) != 0)
            throw new ArgumentOutOfRangeException(nameof(bitRate), bitRate, "Valid V.29 rates are 4800, 7200 and 9600 bit/s.");
    }

    public int BitRate { get; private set; }
    public bool SignalPresent => _signalPresent > 0;
    public bool IsTrained => _trainingStage == TrainingStage.NormalOperation;
    public bool IsParked => _trainingStage == TrainingStage.Parked;
    public float CarrierFrequency => DdsFrequency(_carrierPhaseRate);
    public float SymbolTimingCorrection => _godard.TotalBaudTimingCorrection / (PulseShaperCoefficientSets * 10.0f / 3.0f);
    public float SignalPower => _power.CurrentDbm0 + 3.98f;

    /// <summary>Optional diagnostic logger.</summary>
    public Action<string>? Log { get; set; }

    public SpanLogState Logging { get; } = new();

    public void SetPutBitHandler(PutBitHandler? handler) => _putBit = handler;
    public void SetModemStatusHandler(ModemStatusHandler? handler) => _statusHandler = handler;
    public void SetQamReportHandler(QamReportHandler? handler) => _qamReport = handler;

    public void SetSignalCutoff(float cutoff) {
        _carrierOnPower = (int)(PowerMeter.LevelDbm0(cutoff + 2.5f) * 0.4f);
        _carrierOffPower = (int)(PowerMeter.LevelDbm0(cutoff - 2.5f) * 0.4f);
    }

    public ComplexSample[] GetEqualizerState() {
        var result = new ComplexSample[EqualizerLength];
        for (int i = 0; i < result.Length; i++)
            result[i] = new ComplexSample(_equalizerCoefficients[i].Re, _equalizerCoefficients[i].Im);
        return result;
    }

    public int Process(short[] samples) {
        ArgumentNullException.ThrowIfNull(samples);
        return Process(samples.AsSpan());
    }

    public int Process(short[] samples, int offset, int count) {
        ArgumentNullException.ThrowIfNull(samples);
        return Process(samples.AsSpan(offset, count));
    }

    public int Process(ReadOnlySpan<short> samples) {
        for (int i = 0; i < samples.Length; i++) {
            short amplitude = samples[i];
            _rrcFilter[_rrcFilterStep] = amplitude;
            if (++_rrcFilterStep >= ReceiveFilterSteps)
                _rrcFilterStep = 0;

            int power = SignalDetect(amplitude);
            if (power == 0 || _trainingStage == TrainingStage.Parked)
                continue;

            _equalizerPutStep -= PulseShaperCoefficientSets;
            int step = -_equalizerPutStep;
            if (step < 0)
                step += PulseShaperCoefficientSets;
            step = Math.Clamp(step, 0, PulseShaperCoefficientSets - 1);

            float value = CircularDot(_rrcFilter, RxPulseShaperReal, step, ReceiveFilterSteps, _rrcFilterStep);
            var sample = new ComplexF(value * _agcScaling, 0.0f);
            _godard.Receive(sample.Re);

            if (_equalizerPutStep <= 0) {
                if (_agcScalingSave == 0.0f) {
                    int rootPower = (int)Math.Sqrt(power);
                    if (rootPower == 0)
                        rootPower = 1;
                    _agcScaling = (1.25f / PulseShaperGain) / rootPower;
                }

                value = CircularDot(_rrcFilter, RxPulseShaperImaginary, step, ReceiveFilterSteps, _rrcFilterStep);
                sample.Im = value * _agcScaling;

                ComplexF carrier = DdsLookupComplex(_carrierPhase);
                var baseband = new ComplexF(
                    sample.Re * carrier.Re - sample.Im * carrier.Im,
                    -sample.Re * carrier.Im - sample.Im * carrier.Re);

                _equalizerPutStep += PulseShaperCoefficientSets * 10 / (3 * 2);
                ProcessHalfBaud(baseband);
            }

            _carrierPhase = unchecked(_carrierPhase + (uint)_carrierPhaseRate);
        }

        return 0;
    }

    public int FillIn(int sampleCount) {
        if (sampleCount < 0)
            throw new ArgumentOutOfRangeException(nameof(sampleCount));

        LogMessage($"Fill-in {sampleCount} samples");
        if (_signalPresent <= 0 || _trainingStage == TrainingStage.Parked)
            return 0;

        for (int i = 0; i < sampleCount; i++) {
            _carrierPhase = unchecked(_carrierPhase + (uint)_carrierPhaseRate);
            _equalizerPutStep -= PulseShaperCoefficientSets;
            if (_equalizerPutStep <= 0)
                _equalizerPutStep += PulseShaperCoefficientSets * 10 / (3 * 2);
        }

        return 0;
    }

    public int Restart(int bitRate, bool oldTrain) {
        switch (bitRate) {
            case 9600:
                _trainingCd = 0;
                break;
            case 7200:
                _trainingCd = 2;
                break;
            case 4800:
                _trainingCd = 4;
                break;
            default:
                return -1;
        }

        BitRate = bitRate;
        Array.Clear(_rrcFilter, 0, _rrcFilter.Length);
        _rrcFilterStep = 0;
        _scrambleRegister = 0;
        _trainingScrambleRegister = 0x2A;
        _trainingStage = TrainingStage.SymbolAcquisition;
        _trainingCount = 0;
        _signalPresent = 0;
        _highSample = 0;
        _lowSamples = 0;
        _carrierDropPending = false;
        _oldTrain = oldTrain;
        Array.Clear(_differenceAngles, 0, _differenceAngles.Length);
        _carrierPhase = 0;
        _power.Initialize(4);
        _constellationState = 0;

        if (_oldTrain) {
            _carrierPhaseRate = _savedCarrierPhaseRate;
            EqualizerRestore();
            _agcScaling = _agcScalingSave;
        } else {
            _carrierPhaseRate = DdsPhaseRate(CarrierNominalFrequency);
            EqualizerReset();
            _agcScalingSave = 0.0f;
            _agcScaling = (1.25f / PulseShaperGain) / 735.0f;
        }

        _carrierTrackI = 8000.0f;
        _carrierTrackP = 8000000.0f;
        _lastSample = 0;
        _equalizerSkip = 0;
        _godard.Initialize();
        _baudHalf = 0;
        return 0;
    }

    public int Release() => 0;

    private void ReportStatusChange(V29RxStatus status) {
        if (_statusHandler is not null)
            _statusHandler(status);
        else
            _putBit?.Invoke((int)status);
    }

    private void EqualizerSave() {
        Array.Copy(_equalizerCoefficients, _savedEqualizerCoefficients, EqualizerLength);
    }

    private void EqualizerRestore() {
        Array.Copy(_savedEqualizerCoefficients, _equalizerCoefficients, EqualizerLength);
        Array.Clear(_equalizerBuffer, 0, _equalizerBuffer.Length);
        _equalizerDelta = EqualizerDelta / EqualizerLength;
        _equalizerPutStep = PulseShaperCoefficientSets * 10 / (3 * 2) - 1;
        _equalizerStep = 0;
    }

    private void EqualizerReset() {
        Array.Clear(_equalizerCoefficients, 0, _equalizerCoefficients.Length);
        _equalizerCoefficients[EqualizerPreLength] = new ComplexF(3.0f, 0.0f);
        Array.Clear(_equalizerBuffer, 0, _equalizerBuffer.Length);
        _equalizerDelta = EqualizerDelta / EqualizerLength;
        _equalizerPutStep = PulseShaperCoefficientSets * 10 / (3 * 2) - 1;
        _equalizerStep = 0;
    }

    private ComplexF EqualizerGet() {
        float re = 0.0f;
        float im = 0.0f;
        for (int i = 0; i < EqualizerLength; i++) {
            int sourceIndex = _equalizerStep + i;
            if (sourceIndex >= EqualizerLength)
                sourceIndex -= EqualizerLength;

            ComplexF x = _equalizerBuffer[sourceIndex];
            ComplexF y = _equalizerCoefficients[i];
            re += x.Re * y.Re - x.Im * y.Im;
            im += x.Re * y.Im + x.Im * y.Re;
        }
        return new ComplexF(re, im);
    }

    private void TuneEqualizer(in ComplexF value, in ComplexF target) {
        var error = new ComplexF(
            (target.Re - value.Re) * _equalizerDelta,
            (target.Im - value.Im) * _equalizerDelta);

        for (int i = 0; i < EqualizerLength; i++) {
            int sourceIndex = _equalizerStep + i;
            if (sourceIndex >= EqualizerLength)
                sourceIndex -= EqualizerLength;

            ComplexF x = _equalizerBuffer[sourceIndex];
            ComplexF y = _equalizerCoefficients[i];
            y.Re = y.Re * LmsLeakRate + x.Im * error.Im + x.Re * error.Re;
            y.Im = y.Im * LmsLeakRate + x.Re * error.Im - x.Im * error.Re;
            _equalizerCoefficients[i] = y;
        }
    }

    private void TrackCarrier(in ComplexF value, in ComplexF target) {
        float error = value.Im * target.Re - value.Re * target.Im;
        _carrierPhaseRate = unchecked(_carrierPhaseRate + (int)(_carrierTrackI * error));
        _carrierPhase = unchecked(_carrierPhase + (uint)(int)(_carrierTrackP * error));
    }

    private static int FindQuadrant(in ComplexF value) {
        int b1 = value.Im > value.Re ? 1 : 0;
        int b2 = value.Im < -value.Re ? 1 : 0;
        return (b2 << 1) | (b1 ^ b2);
    }

    private int ScrambledTrainingBit() {
        int bit = _trainingScrambleRegister & 1;
        _trainingScrambleRegister >>= 1;
        if (bit != (_trainingScrambleRegister & 1))
            _trainingScrambleRegister |= 0x40;
        return bit;
    }

    private int Descramble(int inputBit) {
        inputBit &= 1;
        int outputBit = (inputBit
                       ^ (int)(_scrambleRegister >> 17)
                       ^ (int)(_scrambleRegister >> 22)) & 1;
        _scrambleRegister = unchecked((_scrambleRegister << 1) | (uint)inputBit);
        return outputBit;
    }

    private void PutBit(int bit) {
        int outputBit = Descramble(bit);
        if (_trainingStage == TrainingStage.NormalOperation)
            _putBit?.Invoke(outputBit);
    }

    private void DecodeBaud(in ComplexF value) {
        int nearest;
        int rawBits;

        if (BitRate == 4800) {
            nearest = FindQuadrant(value) << 1;
            rawBits = PhaseSteps4800[((nearest - _constellationState) >> 1) & 3];
            PutBit(rawBits);
            PutBit(rawBits >> 1);
        } else {
            int re = (int)((value.Re + 5.0f) * 2.0f);
            int im = (int)((value.Im + 5.0f) * 2.0f);
            re = Math.Clamp(re, 0, 19);
            im = Math.Clamp(im, 0, 19);
            nearest = SpaceMap9600[re, im];

            if (BitRate == 9600)
                PutBit(nearest >> 3);
            else
                nearest &= 7;

            rawBits = PhaseSteps9600[(nearest - _constellationState) & 7];
            for (int i = 0; i < 3; i++) {
                PutBit(rawBits);
                rawBits >>= 1;
            }
        }

        ComplexF target = V29Constellation9600[nearest];
        TrackCarrier(value, target);
        if (--_equalizerSkip <= 0) {
            _equalizerSkip = 10;
            TuneEqualizer(value, target);
        }
        _constellationState = nearest;
    }

    private void ProcessHalfBaud(in ComplexF sample) {
        _equalizerBuffer[_equalizerStep] = sample;
        if (++_equalizerStep >= EqualizerLength)
            _equalizerStep = 0;

        _baudHalf ^= 1;
        if (_baudHalf != 0)
            return;

        _equalizerPutStep += _godard.PerBaud();
        ComplexF value = EqualizerGet();
        ComplexF target = default;

        switch (_trainingStage) {
            case TrainingStage.NormalOperation:
                DecodeBaud(value);
                target = V29Constellation9600[_constellationState];
                break;

            case TrainingStage.SymbolAcquisition:
                if (++_trainingCount >= 60) {
                    _trainingStage = TrainingStage.LogPhase;
                    Array.Clear(_differenceAngles, 0, _differenceAngles.Length);
                    _lastAngles[0] = FastArcTan2(value.Im, value.Re);
                    if (_agcScalingSave == 0.0f) {
                        LogMessage($"Locking AGC at {_agcScaling}");
                        _agcScalingSave = _agcScaling;
                    }
                }
                break;

            case TrainingStage.LogPhase:
                _lastAngles[1] = FastArcTan2(value.Im, value.Re);
                _trainingCount = 1;
                _trainingStage = TrainingStage.WaitForCdcd;
                break;

            case TrainingStage.WaitForCdcd: {
                    int angle = FastArcTan2(value.Im, value.Re);
                    int index = _trainingCount + 1;
                    int angularDifference = unchecked(angle - _lastAngles[index & 1]);
                    _lastAngles[index & 1] = angle;
                    _differenceAngles[index & 0xF] = unchecked(_differenceAngles[(index - 2) & 0xF] + (angularDifference >> 4));

                    if ((angularDifference > DdsPhase(45.0f) || angularDifference < DdsPhase(-45.0f))
                        && _trainingCount >= 13) {
                        index = (_trainingCount - 8) & ~1;
                        if (index > 1) {
                            int j = index & 0xF;
                            angularDifference = (_differenceAngles[j] + _differenceAngles[j | 1]) / (index - 1);
                            _carrierPhaseRate = unchecked(_carrierPhaseRate + 3 * 16 * (angularDifference / 20));
                        }

                        LogMessage($"Coarse carrier frequency {DdsFrequency(_carrierPhaseRate):F2}");
                        if (_carrierPhaseRate < DdsPhaseRate(CarrierNominalFrequency - 20.0f)
                            || _carrierPhaseRate > DdsPhaseRate(CarrierNominalFrequency + 20.0f)) {
                            FailTraining("sequence failed");
                            break;
                        }

                        float phase = DdsPhaseToRadians(unchecked((uint)angle));
                        LogMessage($"Spin by {phase:F5} rads");
                        var rotation = new ComplexF(MathF.Cos(phase), -MathF.Sin(phase));
                        for (int i = 0; i < EqualizerLength; i++)
                            _equalizerBuffer[i] = Multiply(_equalizerBuffer[i], rotation);

                        _carrierPhase = unchecked(_carrierPhase + (uint)angle);
                        int bit = ScrambledTrainingBit();
                        _constellationState = CdcdPositions[_trainingCd + bit];
                        target = V29Constellation9600[_constellationState];
                        _trainingCount = 1;
                        _trainingStage = TrainingStage.TrainOnCdcd;
                        ReportStatusChange(V29RxStatus.TrainingInProgress);
                        break;
                    }

                    if (++_trainingCount > TrainingSegment2Length)
                        FailTraining("sequence failed");
                    break;
                }

            case TrainingStage.TrainOnCdcd: {
                    int bit = ScrambledTrainingBit();
                    _constellationState = CdcdPositions[_trainingCd + bit];
                    target = V29Constellation9600[_constellationState];
                    TrackCarrier(value, target);
                    TuneEqualizer(value, target);
                    if (++_trainingCount >= TrainingSegment3Length - 48) {
                        _trainingStage = TrainingStage.TrainOnCdcdAndTest;
                        _trainingError = 0.0f;
                        _carrierTrackI = 200.0f;
                        _carrierTrackP = 1000000.0f;
                    }
                    break;
                }

            case TrainingStage.TrainOnCdcdAndTest: {
                    int bit = ScrambledTrainingBit();
                    _constellationState = CdcdPositions[_trainingCd + bit];
                    target = V29Constellation9600[_constellationState];
                    TrackCarrier(value, target);
                    TuneEqualizer(value, target);
                    _trainingError += Power(Subtract(value, target));

                    if (++_trainingCount >= TrainingSegment3Length) {
                        LogMessage($"Constellation mismatch {_trainingError}");
                        if (_trainingError < 48.0f * 2.0f) {
                            _trainingError = 0.0f;
                            _trainingCount = 0;
                            _constellationState = 0;
                            _trainingStage = TrainingStage.TestOnes;
                        } else {
                            FailTraining("convergence failed");
                        }
                    }
                    break;
                }

            case TrainingStage.TestOnes:
                DecodeBaud(value);
                target = V29Constellation9600[_constellationState];
                _trainingError += Power(Subtract(value, target));
                if (++_trainingCount >= TrainingSegment4Length) {
                    if (_trainingError < 48.0f) {
                        LogMessage($"Training succeeded at {BitRate}bps (constellation mismatch {_trainingError})");
                        ReportStatusChange(V29RxStatus.TrainingSucceeded);
                        _signalPresent = 60;
                        _trainingStage = TrainingStage.NormalOperation;
                        EqualizerSave();
                        _savedCarrierPhaseRate = _carrierPhaseRate;
                        _agcScalingSave = _agcScaling;
                    } else {
                        FailTraining($"constellation mismatch {_trainingError}");
                    }
                }
                break;

            case TrainingStage.Parked:
            default:
                break;
        }

        _qamReport?.Invoke(
            new ComplexSample(value.Re, value.Im),
            new ComplexSample(target.Re, target.Im),
            _constellationState);
    }

    private int SignalDetect(short amplitude) {
        short x = (short)(amplitude >> 1);
        short difference = unchecked((short)(x - _lastSample));
        _lastSample = x;
        int power = _power.Update(difference);

        int magnitude = Math.Abs((int)difference);
        if (10 * magnitude < _highSample) {
            if (++_lowSamples > 120) {
                _power.Initialize(4);
                _highSample = 0;
                _lowSamples = 0;
            }
        } else {
            _lowSamples = 0;
            if (magnitude > _highSample)
                _highSample = (short)magnitude;
        }

        if (_signalPresent > 0) {
            if (_carrierDropPending || power < _carrierOffPower) {
                if (--_signalPresent <= 0) {
                    Restart(BitRate, false);
                    ReportStatusChange(V29RxStatus.CarrierDown);
                    return 0;
                }
                _carrierDropPending = true;
            }
        } else {
            if (power < _carrierOnPower)
                return 0;

            _signalPresent = 1;
            _carrierDropPending = false;
            ReportStatusChange(V29RxStatus.CarrierUp);
        }

        return power;
    }

    private void FailTraining(string reason) {
        LogMessage($"Training failed ({reason})");
        _agcScalingSave = 0.0f;
        _trainingStage = TrainingStage.Parked;
        ReportStatusChange(V29RxStatus.TrainingFailed);
    }

    private void LogMessage(string message) {
        Log?.Invoke(message);
        Logging.Log((int)SpanLogSeverity.Flow, "%s", message);
    }

    private static ComplexF Multiply(in ComplexF left, in ComplexF right) =>
        new ComplexF(
            left.Re * right.Re - left.Im * right.Im,
            left.Re * right.Im + left.Im * right.Re);

    private static ComplexF Subtract(in ComplexF left, in ComplexF right) =>
        new ComplexF(left.Re - right.Re, left.Im - right.Im);

    private static float Power(in ComplexF value) => value.Re * value.Re + value.Im * value.Im;

    private static float CircularDot(float[] samples, float[,] coefficients, int row, int count, int position) {
        float result = 0.0f;
        for (int i = 0; i < count; i++) {
            int sampleIndex = position + i;
            if (sampleIndex >= count)
                sampleIndex -= count;
            result += samples[sampleIndex] * coefficients[row, i];
        }
        return result;
    }

    private static int FastArcTan2(float y, float x) {
        if (y == 0.0f)
            return x < 0.0f ? int.MinValue : 0;
        if (x == 0.0f)
            return y < 0.0f ? unchecked((int)0xC0000000u) : 0x40000000;

        float absoluteY = MathF.Abs(y);
        float angle = x < 0.0f
            ? 3.0f - (x + absoluteY) / (absoluteY - x)
            : 1.0f - (x - absoluteY) / (absoluteY + x);
        angle *= 536870912.0f;
        if (y < 0.0f)
            angle = -angle;
        return unchecked((int)angle);
    }

    private static int DdsPhaseRate(float frequency) =>
        unchecked((int)(frequency * 4294967296.0 / SampleRate));

    private static float DdsFrequency(int phaseRate) =>
        (float)(phaseRate * (double)SampleRate / 4294967296.0);

    private static int DdsPhase(float angleDegrees) {
        double normalized = angleDegrees < 0.0f ? 360.0 + angleDegrees : angleDegrees;
        uint phase = unchecked((uint)(normalized * 4294967296.0 / 360.0));
        return unchecked((int)phase);
    }

    private static float DdsPhaseToRadians(uint phase) =>
        (float)(phase * (2.0 * Math.PI) / 4294967296.0);

    private static ComplexF DdsLookupComplex(uint phase) {
        double radians = phase * (2.0 * Math.PI) / 4294967296.0;
        return new ComplexF((float)Math.Cos(radians), (float)Math.Sin(radians));
    }

    private static readonly byte[] PhaseSteps9600 = { 4, 0, 2, 6, 7, 3, 1, 5 };
    private static readonly byte[] PhaseSteps4800 = { 0, 2, 3, 1 };
    private static readonly int[] CdcdPositions = { 0, 11, 0, 3, 0, 2 };

    private static readonly ComplexF[] V29Constellation9600 =
    {
        new ComplexF( 3.0f,  0.0f),
        new ComplexF( 1.0f,  1.0f),
        new ComplexF( 0.0f,  3.0f),
        new ComplexF(-1.0f,  1.0f),
        new ComplexF(-3.0f,  0.0f),
        new ComplexF(-1.0f, -1.0f),
        new ComplexF( 0.0f, -3.0f),
        new ComplexF( 1.0f, -1.0f),
        new ComplexF( 5.0f,  0.0f),
        new ComplexF( 3.0f,  3.0f),
        new ComplexF( 0.0f,  5.0f),
        new ComplexF(-3.0f,  3.0f),
        new ComplexF(-5.0f,  0.0f),
        new ComplexF(-3.0f, -3.0f),
        new ComplexF( 0.0f, -5.0f),
        new ComplexF( 3.0f, -3.0f),
    };

    private static readonly byte[,] SpaceMap9600 = new byte[,]
    {
        { 13, 13, 13, 13, 13, 13, 12, 12, 12, 12, 12, 12, 12, 12, 11, 11, 11, 11, 11, 11 },
        { 13, 13, 13, 13, 13, 13, 13, 12, 12, 12, 12, 12, 12, 11, 11, 11, 11, 11, 11, 11 },
        { 13, 13, 13, 13, 13, 13, 13,  4,  4,  4,  4,  4,  4, 11, 11, 11, 11, 11, 11, 11 },
        { 13, 13, 13, 13, 13, 13, 13,  4,  4,  4,  4,  4,  4, 11, 11, 11, 11, 11, 11, 11 },
        { 13, 13, 13, 13, 13, 13, 13,  4,  4,  4,  4,  4,  4, 11, 11, 11, 11, 11, 11, 11 },
        { 13, 13, 13, 13, 13, 13, 13,  5,  4,  4,  4,  4,  3, 11, 11, 11, 11, 11, 11, 11 },
        { 14, 13, 13, 13, 13, 13,  5,  5,  5,  5,  3,  3,  3,  3, 11, 11, 11, 11, 11, 10 },
        { 14, 14,  6,  6,  6,  5,  5,  5,  5,  5,  3,  3,  3,  3,  3,  2,  2,  2, 10, 10 },
        { 14, 14,  6,  6,  6,  6,  5,  5,  5,  5,  3,  3,  3,  3,  2,  2,  2,  2, 10, 10 },
        { 14, 14,  6,  6,  6,  6,  5,  5,  5,  5,  3,  3,  3,  3,  2,  2,  2,  2, 10, 10 },
        { 14, 14,  6,  6,  6,  6,  7,  7,  7,  7,  1,  1,  1,  1,  2,  2,  2,  2, 10, 10 },
        { 14, 14,  6,  6,  6,  6,  7,  7,  7,  7,  1,  1,  1,  1,  2,  2,  2,  2, 10, 10 },
        { 14, 14,  6,  6,  6,  7,  7,  7,  7,  7,  1,  1,  1,  1,  1,  2,  2,  2, 10, 10 },
        { 14, 15, 15, 15, 15, 15,  7,  7,  7,  7,  1,  1,  1,  1,  9,  9,  9,  9,  9, 10 },
        { 15, 15, 15, 15, 15, 15, 15,  7,  0,  0,  0,  0,  1,  9,  9,  9,  9,  9,  9,  9 },
        { 15, 15, 15, 15, 15, 15, 15,  0,  0,  0,  0,  0,  0,  9,  9,  9,  9,  9,  9,  9 },
        { 15, 15, 15, 15, 15, 15, 15,  0,  0,  0,  0,  0,  0,  9,  9,  9,  9,  9,  9,  9 },
        { 15, 15, 15, 15, 15, 15, 15,  0,  0,  0,  0,  0,  0,  9,  9,  9,  9,  9,  9,  9 },
        { 15, 15, 15, 15, 15, 15, 15,  8,  8,  8,  8,  8,  8,  9,  9,  9,  9,  9,  9,  9 },
        { 15, 15, 15, 15, 15, 15,  8,  8,  8,  8,  8,  8,  8,  8,  9,  9,  9,  9,  9,  9 },
    };

    private static readonly float[,] RxPulseShaperReal = new float[,]
    {
        { // Filter 0
            -0.0002255872f, 0.0010772222f, -0.0020489445f,
            0.0027904150f, -0.0024613387f, 0.0016519065f,
            -0.0046141778f, -0.0019967002f, -0.0042213209f,
            -0.0233976908f, 0.0257378154f, -0.0418758480f,
            0.0466891757f, 0.3227157401f, 0.0753364934f,
            -0.1782016976f, -0.0305229951f, -0.0232941009f,
            -0.0367764376f, 0.0007147686f, -0.0127244661f,
            -0.0014302684f, -0.0045579430f, -0.0020412237f,
            -0.0020619062f, -0.0037295068f, -0.0000888674f,
        },
        { // Filter 1
            -0.0002275045f, 0.0009871030f, -0.0020910005f,
            0.0027251718f, -0.0025870150f, 0.0016252864f,
            -0.0048916461f, -0.0019896282f, -0.0048397183f,
            -0.0237455030f, 0.0251964824f, -0.0443225480f,
            0.0474524786f, 0.3241661817f, 0.0749845594f,
            -0.1752765472f, -0.0287604572f, -0.0237602558f,
            -0.0362156055f, 0.0006115933f, -0.0127572522f,
            -0.0013441760f, -0.0046259388f, -0.0019355984f,
            -0.0021082373f, -0.0036506219f, -0.0000961621f,
        },
        { // Filter 2
            -0.0002292356f, 0.0008953516f, -0.0021317307f,
            0.0026574247f, -0.0027108768f, 0.0015966924f,
            -0.0051685886f, -0.0019806028f, -0.0054668819f,
            -0.0240839366f, 0.0246287070f, -0.0467972740f,
            0.0482124309f, 0.3255589583f, 0.0746194218f,
            -0.1723405841f, -0.0270191668f, -0.0242027906f,
            -0.0356412310f, 0.0005099998f, -0.0127779291f,
            -0.0012580760f, -0.0046884769f, -0.0018286778f,
            -0.0021526629f, -0.0035694830f, -0.0001033120f,
        },
        { // Filter 3
            -0.0002307776f, 0.0008020323f, -0.0021710924f,
            0.0025872146f, -0.0028327981f, 0.0015661291f,
            -0.0054447504f, -0.0019695991f, -0.0061025085f,
            -0.0244126100f, 0.0240343329f, -0.0492994401f,
            0.0489687740f, 0.3268935489f, 0.0742412167f,
            -0.1693947961f, -0.0252995164f, -0.0246218749f,
            -0.0350539125f, 0.0004100340f, -0.0127866764f,
            -0.0011720447f, -0.0047455585f, -0.0017205672f,
            -0.0021951580f, -0.0034861722f, -0.0001103123f,
        },
        { // Filter 4
            -0.0002321276f, 0.0007072109f, -0.0022090439f,
            0.0025145852f, -0.0029526538f, 0.0015336029f,
            -0.0057198736f, -0.0019565932f, -0.0067462851f,
            -0.0247311415f, 0.0234132139f, -0.0518284447f,
            0.0497212496f, 0.3281694539f, 0.0738500851f,
            -0.1664401711f, -0.0236018863f, -0.0250176873f,
            -0.0344542470f, 0.0003117404f, -0.0127836799f,
            -0.0010861574f, -0.0047971897f, -0.0016113718f,
            -0.0022356997f, -0.0034007723f, -0.0001171583f,
        },
        { // Filter 5
            -0.0002332832f, 0.0006109552f, -0.0022455442f,
            0.0024395822f, -0.0030703191f, 0.0014991218f,
            -0.0059936981f, -0.0019415623f, -0.0073978882f,
            -0.0250391499f, 0.0227652148f, -0.0543836710f,
            0.0504695999f, 0.3293861955f, 0.0734461726f,
            -0.1634776955f, -0.0219266450f, -0.0253904152f,
            -0.0338428304f, 0.0002151615f, -0.0127691317f,
            -0.0010004881f, -0.0048433807f, -0.0015011966f,
            -0.0022742665f, -0.0033133675f, -0.0001238454f,
        },
        { // Filter 6
            -0.0002342418f, 0.0005133345f, -0.0022805534f,
            0.0023622537f, -0.0031856702f, 0.0014626957f,
            -0.0062659613f, -0.0019244851f, -0.0080569845f,
            -0.0253362545f, 0.0220902113f, -0.0569644870f,
            0.0512135677f, 0.3305433175f, 0.0730296294f,
            -0.1605083547f, -0.0202741490f, -0.0257402545f,
            -0.0332202566f, 0.0001203381f, -0.0127432298f,
            -0.0009151100f, -0.0048841464f, -0.0013901470f,
            -0.0023108390f, -0.0032240431f, -0.0001303693f,
        },
        { // Filter 7
            -0.0002350011f, 0.0004144199f, -0.0023140326f,
            0.0022826505f, -0.0032985840f, 0.0014243362f,
            -0.0065363986f, -0.0019053410f, -0.0087232308f,
            -0.0256220751f, 0.0213880900f, -0.0595702457f,
            0.0519528964f, 0.3316403858f, 0.0726006104f,
            -0.1575331322f, -0.0186447426f, -0.0260674099f,
            -0.0325871175f, 0.0000273092f, -0.0127061779f,
            -0.0008300949f, -0.0049195064f, -0.0012783277f,
            -0.0023453996f, -0.0031328853f, -0.0001367258f,
        },
        { // Filter 8
            -0.0002355590f, 0.0003142838f, -0.0023459437f,
            0.0022008254f, -0.0034089384f, 0.0013840566f,
            -0.0068047432f, -0.0018841111f, -0.0093962739f,
            -0.0258962327f, 0.0206587488f, -0.0622002854f,
            0.0526873304f, 0.3326769885f, 0.0721592747f,
            -0.1545530092f, -0.0170387578f, -0.0263720942f,
            -0.0319440027f, -0.0000638881f, -0.0126581851f,
            -0.0007455136f, -0.0049494841f, -0.0011658436f,
            -0.0023779323f, -0.0030399812f, -0.0001429108f,
        },
        { // Filter 9
            -0.0002359134f, 0.0002130005f, -0.0023762497f,
            0.0021168335f, -0.0035166124f, 0.0013418721f,
            -0.0070707265f, -0.0018607777f, -0.0100757509f,
            -0.0261583493f, 0.0199020968f, -0.0648539300f,
            0.0534166148f, 0.3336527365f, 0.0717057859f,
            -0.1515689641f, -0.0154565143f, -0.0266545283f,
            -0.0312914988f, -0.0001532182f, -0.0125994661f,
            -0.0006614356f, -0.0049741077f, -0.0010527989f,
            -0.0024084233f, -0.0029454189f, -0.0001489205f,
        },
        { // Filter 10
            -0.0002360624f, 0.0001106454f, -0.0024049149f,
            0.0020307323f, -0.0036214861f, 0.0012977996f,
            -0.0073340784f, -0.0018353241f, -0.0107612894f,
            -0.0264080482f, 0.0191180546f, -0.0675304890f,
            0.0541404958f, 0.3345672628f, 0.0712403120f,
            -0.1485819726f, -0.0138983195f, -0.0269149414f,
            -0.0306301899f, -0.0002406474f, -0.0125302403f,
            -0.0005779289f, -0.0049934093f, -0.0009392976f,
            -0.0024368602f, -0.0028492872f, -0.0001547513f,
        },
        { // Filter 11
            -0.0002360044f, 0.0000072955f, -0.0024319045f,
            0.0019425816f, -0.0037234411f, 0.0012518579f,
            -0.0075945272f, -0.0018077352f, -0.0114525072f,
            -0.0266449544f, 0.0183065544f, -0.0702292580f,
            0.0548587208f, 0.3354202238f, 0.0707630251f,
            -0.1455930065f, -0.0123644682f, -0.0271535701f,
            -0.0299606565f, -0.0003261439f, -0.0124507324f,
            -0.0004950606f, -0.0050074254f, -0.0008254432f,
            -0.0024632328f, -0.0027516756f, -0.0001603995f,
        },
        { // Filter 12
            -0.0002357379f, -0.0000969708f, -0.0024571850f,
            0.0018524433f, -0.0038223602f, 0.0012040675f,
            -0.0078518000f, -0.0017779972f, -0.0121490129f,
            -0.0268686945f, 0.0174675399f, -0.0729495187f,
            0.0555710383f, 0.3362112984f, 0.0702741014f,
            -0.1426030339f, -0.0108552427f, -0.0273706589f,
            -0.0292834757f, -0.0004096776f, -0.0123611714f,
            -0.0004128962f, -0.0050161965f, -0.0007113383f,
            -0.0024875325f, -0.0026526740f, -0.0001658620f,
        },
        { // Filter 13
            -0.0002352615f, -0.0002020740f, -0.0024807239f,
            0.0017603814f, -0.0039181276f, 0.0011544506f,
            -0.0081056228f, -0.0017460976f, -0.0128504058f,
            -0.0270788974f, 0.0166009668f, -0.0756905396f,
            0.0562771984f, 0.3369401889f, 0.0697737213f,
            -0.1396130182f, -0.0093709128f, -0.0275664596f,
            -0.0285992206f, -0.0004912202f, -0.0122617910f,
            -0.0003314998f, -0.0050197671f, -0.0005970853f,
            -0.0025097524f, -0.0025523733f, -0.0001711354f,
        },
        { // Filter 14
            -0.0002345739f, -0.0003079330f, -0.0025024901f,
            0.0016664622f, -0.0040106295f, 0.0011030314f,
            -0.0083557207f, -0.0017120255f, -0.0135562760f,
            -0.0272751938f, 0.0157068026f, -0.0784515756f,
            0.0569769525f, 0.3376066210f, 0.0692620692f,
            -0.1366239185f, -0.0079117354f, -0.0277412312f,
            -0.0279084604f, -0.0005707451f, -0.0121528290f,
            -0.0002509341f, -0.0050181856f, -0.0004827856f,
            -0.0025298877f, -0.0024508643f, -0.0001762169f,
        },
        { // Filter 15
            -0.0002336743f, -0.0004144658f, -0.0025224539f,
            0.0015707541f, -0.0040997535f, 0.0010498359f,
            -0.0086018182f, -0.0016757712f, -0.0142662045f,
            -0.0274572171f, 0.0147850268f, -0.0812318688f,
            0.0576700538f, 0.3382103435f, 0.0687393331f,
            -0.1336366882f, -0.0064779550f, -0.0278952399f,
            -0.0272117599f, -0.0006482275f, -0.0120345272f,
            -0.0001712605f, -0.0050115044f, -0.0003685397f,
            -0.0025479352f, -0.0023482385f, -0.0001811037f,
        },
        { // Filter 16
            -0.0002325617f, -0.0005215892f, -0.0025405864f,
            0.0014733275f, -0.0041853892f, 0.0009948916f,
            -0.0088436391f, -0.0016373265f, -0.0149797637f,
            -0.0276246035f, 0.0138356311f, -0.0840306488f,
            0.0583562570f, 0.3387511289f, 0.0682057050f,
            -0.1306522756f, -0.0050698033f, -0.0280287586f,
            -0.0265096790f, -0.0007236445f, -0.0119071312f,
            -0.0000925387f, -0.0049997797f, -0.0002544474f,
            -0.0025638935f, -0.0022445878f, -0.0001857931f,
        },
        { // Filter 17
            -0.0002312354f, -0.0006292189f, -0.0025568607f,
            0.0013742548f, -0.0042674280f, 0.0009382283f,
            -0.0090809071f, -0.0015966849f, -0.0156965171f,
            -0.0277769916f, 0.0128586191f, -0.0868471326f,
            0.0590353189f, 0.3392287735f, 0.0676613808f,
            -0.1276716224f, -0.0036874991f, -0.0281420669f,
            -0.0258027730f, -0.0007969747f, -0.0117708901f,
            -0.0000148271f, -0.0049830712f, -0.0001406075f,
            -0.0025777629f, -0.0021400040f, -0.0001902827f,
        },
        { // Filter 18
            -0.0002296950f, -0.0007372695f, -0.0025712507f,
            0.0012736106f, -0.0043457636f, 0.0008798770f,
            -0.0093133456f, -0.0015538410f, -0.0164160196f,
            -0.0279140236f, 0.0118540071f, -0.0896805253f,
            0.0597069980f, 0.3396430972f, 0.0671065596f,
            -0.1246956645f, -0.0023312486f, -0.0282354509f,
            -0.0250915918f, -0.0008681986f, -0.0116260565f,
            0.0000618177f, -0.0049614424f, -0.0000271176f,
            -0.0025895456f, -0.0020345793f, -0.0001945702f,
        },
        { // Filter 19
            -0.0002279401f, -0.0008456549f, -0.0025837321f,
            0.0011714711f, -0.0044202916f, 0.0008198711f,
            -0.0095406785f, -0.0015087913f, -0.0171378178f,
            -0.0280353446f, 0.0108218234f, -0.0925300200f,
            0.0603710549f, 0.3399939436f, 0.0665414442f,
            -0.1217253306f, -0.0010012450f, -0.0283092029f,
            -0.0243766800f, -0.0009372985f, -0.0114728860f,
            0.0001373403f, -0.0049349603f, 0.0000859256f,
            -0.0025992453f, -0.0019284058f, -0.0001986536f,
        },
        { // Filter 20
            -0.0002259705f, -0.0009542879f, -0.0025942817f,
            0.0010679146f, -0.0044909099f, 0.0007582452f,
            -0.0097626295f, -0.0014615337f, -0.0178614501f,
            -0.0281406035f, 0.0097621089f, -0.0953947986f,
            0.0610272526f, 0.3402811804f, 0.0659662411f,
            -0.1187615423f, 0.0003023310f, -0.0283636214f,
            -0.0236585765f, -0.0010042584f, -0.0113116371f,
            0.0002116871f, -0.0049036954f, 0.0001984268f,
            -0.0026068677f, -0.0018215756f, -0.0002025310f,
        },
        { // Filter 21
            -0.0002237864f, -0.0010630805f, -0.0026028781f,
            0.0009630212f, -0.0045575190f, 0.0006950361f,
            -0.0099789231f, -0.0014120676f, -0.0185864469f,
            -0.0282294527f, 0.0086749169f, -0.0982740317f,
            0.0616753560f, 0.3405046994f, 0.0653811596f,
            -0.1158052137f, 0.0015793118f, -0.0283990105f,
            -0.0229378142f, -0.0010690639f, -0.0111425710f,
            0.0002848063f, -0.0048677214f, 0.0003102917f,
            -0.0026124200f, -0.0017141808f, -0.0002062008f,
        },
        { // Filter 22
            -0.0002213880f, -0.0011719442f, -0.0026095011f,
            0.0008568729f, -0.0046200215f, 0.0006302822f,
            -0.0101892847f, -0.0013603940f, -0.0193123305f,
            -0.0283015486f, 0.0075603132f, -0.1011668792f,
            0.0623151328f, 0.3406644160f, 0.0647864126f,
            -0.1128572509f, 0.0028295425f, -0.0284156803f,
            -0.0222149199f, -0.0011317024f, -0.0109659514f,
            0.0003566474f, -0.0048271155f, 0.0004214275f,
            -0.0026159112f, -0.0016063133f, -0.0002096614f,
        },
        { // Filter 23
            -0.0002187756f, -0.0012807895f, -0.0026141321f,
            0.0007495533f, -0.0046783230f, 0.0005640236f,
            -0.0103934403f, -0.0013065156f, -0.0200386159f,
            -0.0283565521f, 0.0064183765f, -0.1040724905f,
            0.0629463529f, 0.3407602701f, 0.0641822160f,
            -0.1099185516f, 0.0040528809f, -0.0284139459f,
            -0.0214904140f, -0.0011921631f, -0.0107820442f,
            0.0004271617f, -0.0047819577f, 0.0005317428f,
            -0.0026173518f, -0.0014980648f, -0.0002129114f,
        },
        { // Filter 24
            -0.0002159498f, -0.0013895266f, -0.0026167542f,
            0.0006411476f, -0.0047323315f, 0.0004963022f,
            -0.0105911172f, -0.0012504367f, -0.0207648103f,
            -0.0283941279f, 0.0052491978f, -0.1069900048f,
            0.0635687888f, 0.3407922255f, 0.0635687888f,
            -0.1069900048f, 0.0052491978f, -0.0283941279f,
            -0.0207648103f, -0.0012504367f, -0.0105911172f,
            0.0004963022f, -0.0047323315f, 0.0006411476f,
            -0.0026167542f, -0.0013895266f, -0.0002159498f,
        },
        { // Filter 25
            -0.0002129114f, -0.0014980648f, -0.0026173518f,
            0.0005317429f, -0.0047819577f, 0.0004271617f,
            -0.0107820442f, -0.0011921631f, -0.0214904140f,
            -0.0284139458f, 0.0040528809f, -0.1099185516f,
            0.0641822160f, 0.3407602701f, 0.0629463529f,
            -0.1040724905f, 0.0064183765f, -0.0283565521f,
            -0.0200386159f, -0.0013065156f, -0.0103934403f,
            0.0005640236f, -0.0046783230f, 0.0007495533f,
            -0.0026141321f, -0.0012807895f, -0.0002187756f,
        },
        { // Filter 26
            -0.0002096614f, -0.0016063134f, -0.0026159112f,
            0.0004214275f, -0.0048271154f, 0.0003566474f,
            -0.0109659514f, -0.0011317024f, -0.0222149198f,
            -0.0284156802f, 0.0028295425f, -0.1128572510f,
            0.0647864126f, 0.3406644160f, 0.0623151328f,
            -0.1011668792f, 0.0075603133f, -0.0283015487f,
            -0.0193123305f, -0.0013603940f, -0.0101892847f,
            0.0006302822f, -0.0046200216f, 0.0008568729f,
            -0.0026095011f, -0.0011719442f, -0.0002213880f,
        },
        { // Filter 27
            -0.0002062008f, -0.0017141808f, -0.0026124200f,
            0.0003102917f, -0.0048677214f, 0.0002848063f,
            -0.0111425710f, -0.0010690639f, -0.0229378141f,
            -0.0283990105f, 0.0015793118f, -0.1158052138f,
            0.0653811596f, 0.3405046994f, 0.0616753560f,
            -0.0982740316f, 0.0086749169f, -0.0282294527f,
            -0.0185864469f, -0.0014120676f, -0.0099789231f,
            0.0006950361f, -0.0045575190f, 0.0009630212f,
            -0.0026028781f, -0.0010630805f, -0.0002237864f,
        },
        { // Filter 28
            -0.0002025310f, -0.0018215756f, -0.0026068677f,
            0.0001984268f, -0.0049036954f, 0.0002116871f,
            -0.0113116371f, -0.0010042584f, -0.0236585765f,
            -0.0283636214f, 0.0003023310f, -0.1187615424f,
            0.0659662411f, 0.3402811804f, 0.0610272526f,
            -0.0953947985f, 0.0097621089f, -0.0281406035f,
            -0.0178614501f, -0.0014615337f, -0.0097626294f,
            0.0007582452f, -0.0044909099f, 0.0010679146f,
            -0.0025942817f, -0.0009542879f, -0.0002259705f,
        },
        { // Filter 29
            -0.0001986536f, -0.0019284058f, -0.0025992453f,
            0.0000859256f, -0.0049349603f, 0.0001373403f,
            -0.0114728860f, -0.0009372985f, -0.0243766800f,
            -0.0283092029f, -0.0010012450f, -0.1217253307f,
            0.0665414443f, 0.3399939436f, 0.0603710549f,
            -0.0925300199f, 0.0108218235f, -0.0280353446f,
            -0.0171378178f, -0.0015087913f, -0.0095406784f,
            0.0008198711f, -0.0044202916f, 0.0011714711f,
            -0.0025837321f, -0.0008456549f, -0.0002279401f,
        },
        { // Filter 30
            -0.0001945702f, -0.0020345793f, -0.0025895456f,
            -0.0000271176f, -0.0049614424f, 0.0000618176f,
            -0.0116260566f, -0.0008681986f, -0.0250915918f,
            -0.0282354509f, -0.0023312486f, -0.1246956646f,
            0.0671065596f, 0.3396430972f, 0.0597069980f,
            -0.0896805252f, 0.0118540072f, -0.0279140236f,
            -0.0164160196f, -0.0015538410f, -0.0093133456f,
            0.0008798770f, -0.0043457636f, 0.0012736106f,
            -0.0025712507f, -0.0007372695f, -0.0002296950f,
        },
        { // Filter 31
            -0.0001902827f, -0.0021400041f, -0.0025777629f,
            -0.0001406075f, -0.0049830712f, -0.0000148271f,
            -0.0117708901f, -0.0007969747f, -0.0258027730f,
            -0.0281420669f, -0.0036874991f, -0.1276716225f,
            0.0676613808f, 0.3392287735f, 0.0590353189f,
            -0.0868471325f, 0.0128586192f, -0.0277769917f,
            -0.0156965171f, -0.0015966849f, -0.0090809070f,
            0.0009382283f, -0.0042674281f, 0.0013742548f,
            -0.0025568607f, -0.0006292188f, -0.0002312354f,
        },
        { // Filter 32
            -0.0001857931f, -0.0022445878f, -0.0025638935f,
            -0.0002544474f, -0.0049997796f, -0.0000925387f,
            -0.0119071312f, -0.0007236445f, -0.0265096790f,
            -0.0280287586f, -0.0050698033f, -0.1306522756f,
            0.0682057050f, 0.3387511289f, 0.0583562570f,
            -0.0840306488f, 0.0138356311f, -0.0276246035f,
            -0.0149797637f, -0.0016373265f, -0.0088436391f,
            0.0009948916f, -0.0041853892f, 0.0014733275f,
            -0.0025405864f, -0.0005215892f, -0.0002325617f,
        },
        { // Filter 33
            -0.0001811037f, -0.0023482386f, -0.0025479352f,
            -0.0003685397f, -0.0050115044f, -0.0001712605f,
            -0.0120345272f, -0.0006482275f, -0.0272117599f,
            -0.0278952399f, -0.0064779551f, -0.1336366883f,
            0.0687393331f, 0.3382103435f, 0.0576700538f,
            -0.0812318688f, 0.0147850268f, -0.0274572172f,
            -0.0142662045f, -0.0016757712f, -0.0086018182f,
            0.0010498359f, -0.0040997535f, 0.0015707541f,
            -0.0025224538f, -0.0004144658f, -0.0002336743f,
        },
        { // Filter 34
            -0.0001762169f, -0.0024508643f, -0.0025298877f,
            -0.0004827856f, -0.0050181856f, -0.0002509341f,
            -0.0121528290f, -0.0005707451f, -0.0279084604f,
            -0.0277412312f, -0.0079117354f, -0.1366239185f,
            0.0692620692f, 0.3376066210f, 0.0569769525f,
            -0.0784515755f, 0.0157068026f, -0.0272751938f,
            -0.0135562760f, -0.0017120255f, -0.0083557207f,
            0.0011030314f, -0.0040106295f, 0.0016664622f,
            -0.0025024901f, -0.0003079330f, -0.0002345739f,
        },
        { // Filter 35
            -0.0001711354f, -0.0025523733f, -0.0025097524f,
            -0.0005970853f, -0.0050197670f, -0.0003314998f,
            -0.0122617910f, -0.0004912202f, -0.0285992206f,
            -0.0275664596f, -0.0093709128f, -0.1396130183f,
            0.0697737213f, 0.3369401889f, 0.0562771984f,
            -0.0756905395f, 0.0166009668f, -0.0270788974f,
            -0.0128504058f, -0.0017460976f, -0.0081056228f,
            0.0011544506f, -0.0039181276f, 0.0017603814f,
            -0.0024807239f, -0.0002020740f, -0.0002352615f,
        },
        { // Filter 36
            -0.0001658620f, -0.0026526741f, -0.0024875325f,
            -0.0007113383f, -0.0050161965f, -0.0004128962f,
            -0.0123611714f, -0.0004096776f, -0.0292834756f,
            -0.0273706589f, -0.0108552428f, -0.1426030339f,
            0.0702741014f, 0.3362112984f, 0.0555710383f,
            -0.0729495187f, 0.0174675399f, -0.0268686946f,
            -0.0121490129f, -0.0017779972f, -0.0078518000f,
            0.0012040675f, -0.0038223602f, 0.0018524432f,
            -0.0024571850f, -0.0000969708f, -0.0002357379f,
        },
        { // Filter 37
            -0.0001603995f, -0.0027516756f, -0.0024632328f,
            -0.0008254431f, -0.0050074254f, -0.0004950606f,
            -0.0124507324f, -0.0003261439f, -0.0299606565f,
            -0.0271535701f, -0.0123644683f, -0.1455930066f,
            0.0707630251f, 0.3354202237f, 0.0548587208f,
            -0.0702292579f, 0.0183065544f, -0.0266449544f,
            -0.0114525072f, -0.0018077352f, -0.0075945272f,
            0.0012518579f, -0.0037234411f, 0.0019425816f,
            -0.0024319045f, 0.0000072955f, -0.0002360044f,
        },
        { // Filter 38
            -0.0001547513f, -0.0028492873f, -0.0024368602f,
            -0.0009392976f, -0.0049934093f, -0.0005779289f,
            -0.0125302403f, -0.0002406474f, -0.0306301899f,
            -0.0269149414f, -0.0138983196f, -0.1485819727f,
            0.0712403120f, 0.3345672628f, 0.0541404958f,
            -0.0675304890f, 0.0191180546f, -0.0264080483f,
            -0.0107612894f, -0.0018353241f, -0.0073340784f,
            0.0012977997f, -0.0036214862f, 0.0020307323f,
            -0.0024049149f, 0.0001106454f, -0.0002360624f,
        },
        { // Filter 39
            -0.0001489205f, -0.0029454190f, -0.0024084233f,
            -0.0010527989f, -0.0049741077f, -0.0006614356f,
            -0.0125994661f, -0.0001532182f, -0.0312914988f,
            -0.0266545283f, -0.0154565144f, -0.1515689642f,
            0.0717057859f, 0.3336527364f, 0.0534166148f,
            -0.0648539299f, 0.0199020968f, -0.0261583493f,
            -0.0100757509f, -0.0018607777f, -0.0070707265f,
            0.0013418721f, -0.0035166124f, 0.0021168335f,
            -0.0023762497f, 0.0002130005f, -0.0002359134f,
        },
        { // Filter 40
            -0.0001429108f, -0.0030399812f, -0.0023779323f,
            -0.0011658436f, -0.0049494841f, -0.0007455136f,
            -0.0126581851f, -0.0000638881f, -0.0319440026f,
            -0.0263720941f, -0.0170387578f, -0.1545530092f,
            0.0721592747f, 0.3326769885f, 0.0526873304f,
            -0.0622002854f, 0.0206587488f, -0.0258962328f,
            -0.0093962739f, -0.0018841111f, -0.0068047432f,
            0.0013840566f, -0.0034089384f, 0.0022008254f,
            -0.0023459437f, 0.0003142838f, -0.0002355590f,
        },
        { // Filter 41
            -0.0001367258f, -0.0031328853f, -0.0023453996f,
            -0.0012783277f, -0.0049195063f, -0.0008300949f,
            -0.0127061779f, 0.0000273091f, -0.0325871175f,
            -0.0260674099f, -0.0186447426f, -0.1575331323f,
            0.0726006104f, 0.3316403858f, 0.0519528964f,
            -0.0595702456f, 0.0213880901f, -0.0256220751f,
            -0.0087232308f, -0.0019053410f, -0.0065363986f,
            0.0014243362f, -0.0032985840f, 0.0022826505f,
            -0.0023140326f, 0.0004144199f, -0.0002350011f,
        },
        { // Filter 42
            -0.0001303693f, -0.0032240431f, -0.0023108390f,
            -0.0013901470f, -0.0048841464f, -0.0009151100f,
            -0.0127432299f, 0.0001203381f, -0.0332202566f,
            -0.0257402545f, -0.0202741491f, -0.1605083548f,
            0.0730296294f, 0.3305433175f, 0.0512135677f,
            -0.0569644869f, 0.0220902113f, -0.0253362545f,
            -0.0080569845f, -0.0019244850f, -0.0062659613f,
            0.0014626957f, -0.0031856702f, 0.0023622537f,
            -0.0022805534f, 0.0005133345f, -0.0002342418f,
        },
        { // Filter 43
            -0.0001238454f, -0.0033133676f, -0.0022742665f,
            -0.0015011966f, -0.0048433806f, -0.0010004881f,
            -0.0127691318f, 0.0002151615f, -0.0338428304f,
            -0.0253904152f, -0.0219266451f, -0.1634776956f,
            0.0734461726f, 0.3293861955f, 0.0504695998f,
            -0.0543836710f, 0.0227652149f, -0.0250391499f,
            -0.0073978882f, -0.0019415623f, -0.0059936981f,
            0.0014991218f, -0.0030703191f, 0.0024395821f,
            -0.0022455442f, 0.0006109552f, -0.0002332832f,
        },
        { // Filter 44
            -0.0001171583f, -0.0034007723f, -0.0022356997f,
            -0.0016113718f, -0.0047971896f, -0.0010861574f,
            -0.0127836799f, 0.0003117404f, -0.0344542470f,
            -0.0250176873f, -0.0236018863f, -0.1664401711f,
            0.0738500851f, 0.3281694539f, 0.0497212495f,
            -0.0518284447f, 0.0234132139f, -0.0247311415f,
            -0.0067462851f, -0.0019565932f, -0.0057198736f,
            0.0015336029f, -0.0029526538f, 0.0025145852f,
            -0.0022090439f, 0.0007072109f, -0.0002321276f,
        },
        { // Filter 45
            -0.0001103123f, -0.0034861722f, -0.0021951581f,
            -0.0017205672f, -0.0047455585f, -0.0011720448f,
            -0.0127866764f, 0.0004100340f, -0.0350539125f,
            -0.0246218749f, -0.0252995164f, -0.1693947962f,
            0.0742412167f, 0.3268935488f, 0.0489687740f,
            -0.0492994400f, 0.0240343329f, -0.0244126100f,
            -0.0061025085f, -0.0019695991f, -0.0054447503f,
            0.0015661291f, -0.0028327981f, 0.0025872146f,
            -0.0021710924f, 0.0008020323f, -0.0002307776f,
        },
        { // Filter 46
            -0.0001033120f, -0.0035694831f, -0.0021526629f,
            -0.0018286778f, -0.0046884769f, -0.0012580760f,
            -0.0127779291f, 0.0005099998f, -0.0356412310f,
            -0.0242027906f, -0.0270191669f, -0.1723405841f,
            0.0746194218f, 0.3255589582f, 0.0482124309f,
            -0.0467972740f, 0.0246287071f, -0.0240839366f,
            -0.0054668819f, -0.0019806028f, -0.0051685886f,
            0.0015966924f, -0.0027108768f, 0.0026574246f,
            -0.0021317307f, 0.0008953516f, -0.0002292356f,
        },
        { // Filter 47
            -0.0000961621f, -0.0036506219f, -0.0021082373f,
            -0.0019355984f, -0.0046259388f, -0.0013441760f,
            -0.0127572522f, 0.0006115933f, -0.0362156055f,
            -0.0237602557f, -0.0287604572f, -0.1752765473f,
            0.0749845594f, 0.3241661817f, 0.0474524786f,
            -0.0443225480f, 0.0251964824f, -0.0237455030f,
            -0.0048397183f, -0.0019896282f, -0.0048916461f,
            0.0016252864f, -0.0025870150f, 0.0027251718f,
            -0.0020910005f, 0.0009871030f, -0.0002275045f,
        },
    };

    private static readonly float[,] RxPulseShaperImaginary = new float[,]
    {
        { // Filter 0
            -0.0028663577f, -0.0003500107f, -0.0033435735f,
            -0.0027904150f, -0.0015083103f, -0.0050840455f,
            -0.0003631437f, -0.0126066690f, 0.0017485284f,
            -0.0322041586f, -0.0301351100f, -0.0213368103f,
            -0.1944744127f, 0.0000000000f, 0.3137990785f,
            0.0907983001f, -0.0357378355f, 0.0320615793f,
            -0.0152332992f, -0.0045128713f, 0.0010014372f,
            -0.0044019135f, 0.0027931110f, -0.0020412237f,
            0.0033647251f, -0.0012117902f, 0.0011291668f,
        },
        { // Filter 1
            -0.0028907192f, -0.0003207292f, -0.0034122027f,
            -0.0027251718f, -0.0015853248f, -0.0050021172f,
            -0.0003849809f, -0.0125620181f, 0.0020046770f,
            -0.0326828810f, -0.0295012904f, -0.0225834662f,
            -0.1976537983f, 0.0000000000f, 0.3123331681f,
            0.0893078615f, -0.0336741688f, 0.0327031865f,
            -0.0150009950f, -0.0038614483f, 0.0010040175f,
            -0.0041369483f, 0.0028347789f, -0.0019355984f,
            0.0034403305f, -0.0011861589f, 0.0012218552f,
        },
        { // Filter 2
            -0.0029127146f, -0.0002909174f, -0.0034786683f,
            -0.0026574247f, -0.0016612274f, -0.0049141139f,
            -0.0004067767f, -0.0125050341f, 0.0022644566f,
            -0.0331486949f, -0.0288365110f, -0.0238444021f,
            -0.2008192274f, 0.0000000000f, 0.3108122604f,
            0.0878119136f, -0.0316353798f, 0.0333122834f,
            -0.0147630813f, -0.0032200118f, 0.0010056448f,
            -0.0038719599f, 0.0028731023f, -0.0018286778f,
            0.0035128265f, -0.0011597953f, 0.0013127038f,
        },
        { // Filter 3
            -0.0029323070f, -0.0002605961f, -0.0035429008f,
            -0.0025872146f, -0.0017359409f, -0.0048200498f,
            -0.0004285111f, -0.0124355594f, 0.0025277418f,
            -0.0336010750f, -0.0281405882f, -0.0251193194f,
            -0.2039696230f, 0.0000000000f, 0.3092369227f,
            0.0863109597f, -0.0296219278f, 0.0338891035f,
            -0.0145198060f, -0.0025888530f, 0.0010063333f,
            -0.0036071828f, 0.0029080820f, -0.0017205672f,
            0.0035821724f, -0.0011327260f, 0.0014016509f,
        },
        { // Filter 4
            -0.0029494613f, -0.0002297867f, -0.0036048319f,
            -0.0025145852f, -0.0018093886f, -0.0047199444f,
            -0.0004501638f, -0.0123534432f, 0.0027944028f,
            -0.0340394960f, -0.0274133513f, -0.0264079116f,
            -0.2071039093f, 0.0000000000f, 0.3076077423f,
            0.0848055030f, -0.0276342583f, 0.0344338926f,
            -0.0142714164f, -0.0019682517f, 0.0010060974f,
            -0.0033428487f, 0.0029397216f, -0.0016113718f,
            0.0036483303f, -0.0011049779f, 0.0014886371f,
        },
        { // Filter 5
            -0.0029641443f, -0.0001985114f, -0.0036643951f,
            -0.0024395821f, -0.0018814940f, -0.0046138225f,
            -0.0004717143f, -0.0122585422f, 0.0030643056f,
            -0.0344634332f, -0.0266546419f, -0.0277098644f,
            -0.2102210127f, 0.0000000000f, 0.3059253257f,
            0.0832960463f, -0.0256728028f, 0.0349469084f,
            -0.0140181593f, -0.0013584764f, 0.0010049525f,
            -0.0030791858f, 0.0029680275f, -0.0015011966f,
            0.0037112657f, -0.0010765784f, 0.0015736048f,
        },
        { // Filter 6
            -0.0029763245f, -0.0001667925f, -0.0037215250f,
            -0.0023622537f, -0.0019521812f, -0.0045017145f,
            -0.0004931419f, -0.0121507204f, 0.0033373123f,
            -0.0348723626f, -0.0258643143f, -0.0290248559f,
            -0.2133198616f, 0.0000000000f, 0.3041902986f,
            0.0817830916f, -0.0237379786f, 0.0354284210f,
            -0.0137602808f, -0.0007597849f, 0.0010029139f,
            -0.0028164189f, 0.0029930088f, -0.0013901470f,
            0.0037709466f, -0.0010475551f, 0.0016564987f,
        },
        { // Filter 7
            -0.0029859725f, -0.0001346532f, -0.0037761580f,
            -0.0022826505f, -0.0020213749f, -0.0043836560f,
            -0.0005144257f, -0.0120298497f, 0.0036132805f,
            -0.0352657610f, -0.0250422359f, -0.0303525562f,
            -0.2163993876f, 0.0000000000f, 0.3024033057f,
            0.0802671400f, -0.0218301888f, 0.0358787117f,
            -0.0134980260f, -0.0001724232f, 0.0009999979f,
            -0.0025547695f, 0.0030146774f, -0.0012783277f,
            0.0038273443f, -0.0010179361f, 0.0017372654f,
        },
        { // Filter 8
            -0.0029930606f, -0.0001021170f, -0.0038282321f,
            -0.0022008254f, -0.0020890001f, -0.0042596882f,
            -0.0005355449f, -0.0118958096f, 0.0038920641f,
            -0.0356431066f, -0.0241882871f, -0.0316926284f,
            -0.2194585253f, 0.0000000000f, 0.3005650102f,
            0.0787486915f, -0.0199498222f, 0.0362980736f,
            -0.0132316391f, 0.0004033735f, 0.0009962208f,
            -0.0022944550f, 0.0030330478f, -0.0011658436f,
            0.0038804329f, -0.0009877498f, 0.0018158537f,
        },
        { // Filter 9
            -0.0029975634f, -0.0000692081f, -0.0038776871f,
            -0.0021168335f, -0.0021549828f, -0.0041298577f,
            -0.0005564782f, -0.0117484878f, 0.0041735127f,
            -0.0360038791f, -0.0233023614f, -0.0330447278f,
            -0.2224962134f, 0.0000000000f, 0.2986760936f,
            0.0772282446f, -0.0180972531f, 0.0366868109f,
            -0.0129613632f, 0.0009673813f, 0.0009915995f,
            -0.0020356893f, 0.0030481371f, -0.0010527989f,
            0.0039301896f, -0.0009570246f, 0.0018922147f,
        },
        { // Filter 10
            -0.0029994572f, -0.0000359509f, -0.0039244645f,
            -0.0020307323f, -0.0022192496f, -0.0039942166f,
            -0.0005772045f, -0.0115877801f, 0.0044574720f,
            -0.0363475602f, -0.0223843659f, -0.0344085028f,
            -0.2255113948f, 0.0000000000f, 0.2967372552f,
            0.0757062964f, -0.0162728414f, 0.0370452387f,
            -0.0126874401f, 0.0015193879f, 0.0009861513f,
            -0.0017786823f, 0.0030599652f, -0.0009392976f,
            0.0039765945f, -0.0009257895f, 0.0019663015f,
        },
        { // Filter 11
            -0.0029987206f, -0.0000023704f, -0.0039685075f,
            -0.0019425816f, -0.0022817277f, -0.0038528225f,
            -0.0005977023f, -0.0114135907f, 0.0047437838f,
            -0.0366736335f, -0.0214342212f, -0.0357835943f,
            -0.2285030170f, 0.0000000000f, 0.2947492121f,
            0.0741833421f, -0.0144769322f, 0.0373736830f,
            -0.0124101103f, 0.0020591917f, 0.0009798939f,
            -0.0015236399f, 0.0030685542f, -0.0008254432f,
            0.0040196306f, -0.0008940736f, 0.0020380695f,
        },
        { // Filter 12
            -0.0029953341f, 0.0000315077f, -0.0040097615f,
            -0.0018524433f, -0.0023423453f, -0.0037057386f,
            -0.0006179501f, -0.0112258324f, 0.0050322859f,
            -0.0369815854f, -0.0204518615f, -0.0371696363f,
            -0.2314700330f, 0.0000000000f, 0.2927126984f,
            0.0726598749f, -0.0127098562f, 0.0376724801f,
            -0.0121296128f, 0.0025866029f, 0.0009728453f,
            -0.0012707638f, 0.0030739292f, -0.0007113383f,
            0.0040592840f, -0.0008619060f, 0.0021074764f,
        },
        { // Filter 13
            -0.0029892805f, 0.0000656578f, -0.0040481735f,
            -0.0017603814f, -0.0024010317f, -0.0035530336f,
            -0.0006379264f, -0.0110244266f, 0.0053228123f,
            -0.0372709048f, -0.0194372347f, -0.0385662562f,
            -0.2344114015f, 0.0000000000f, 0.2906284654f,
            0.0711363859f, -0.0109719291f, 0.0379419766f,
            -0.0118461850f, 0.0031014424f, 0.0009650239f,
            -0.0010202514f, 0.0030761172f, -0.0005970853f,
            0.0040955437f, -0.0008293163f, 0.0021744820f,
        },
        { // Filter 14
            -0.0029805446f, 0.0001000535f, -0.0040836928f,
            -0.0016664622f, -0.0024577169f, -0.0033947816f,
            -0.0006576095f, -0.0108093036f, 0.0056151934f,
            -0.0375410836f, -0.0183903030f, -0.0399730743f,
            -0.2373260872f, 0.0000000000f, 0.2884972807f,
            0.0696133635f, -0.0092634520f, 0.0381825291f,
            -0.0115600628f, 0.0036035428f, 0.0009564484f,
            -0.0007722957f, 0.0030751481f, -0.0004827856f,
            0.0041284016f, -0.0007963341f, 0.0022390486f,
        },
        { // Filter 15
            -0.0029691134f, 0.0001346681f, -0.0041162706f,
            -0.0015707541f, -0.0025123322f, -0.0032310625f,
            -0.0006769778f, -0.0105804029f, 0.0059092554f,
            -0.0377916173f, -0.0173110422f, -0.0413897045f,
            -0.2402130618f, 0.0000000000f, 0.2863199281f,
            0.0680912936f, -0.0075847108f, 0.0383945039f,
            -0.0112714800f, 0.0040927476f, 0.0009471378f,
            -0.0005270855f, 0.0030710539f, -0.0003685397f,
            0.0041578524f, -0.0007629890f, 0.0023011406f,
        },
        { // Filter 16
            -0.0029549762f, 0.0001694746f, -0.0041458603f,
            -0.0014733275f, -0.0025648098f, -0.0030619616f,
            -0.0006960095f, -0.0103376729f, 0.0062048213f,
            -0.0380220048f, -0.0161994426f, -0.0428157541f,
            -0.2430713040f, 0.0000000000f, 0.2840972074f,
            0.0665706594f, -0.0059359770f, 0.0385782766f,
            -0.0109806686f, 0.0045689113f, 0.0009371116f,
            -0.0002848048f, 0.0030638689f, -0.0002544474f,
            0.0041838939f, -0.0007293108f, 0.0023607249f,
        },
        { // Filter 17
            -0.0029381242f, 0.0002044456f, -0.0041724174f,
            -0.0013742548f, -0.0026150833f, -0.0028875696f,
            -0.0007146829f, -0.0100810715f, 0.0065017103f,
            -0.0382317491f, -0.0150555086f, -0.0442508243f,
            -0.2458998002f, 0.0000000000f, 0.2818299337f,
            0.0650519408f, -0.0043175067f, 0.0387342321f,
            -0.0106878585f, 0.0050318999f, 0.0009263891f,
            -0.0000456330f, 0.0030536299f, -0.0001406075f,
            0.0042065267f, -0.0006953295f, 0.0024177704f,
        },
        { // Filter 18
            -0.0029185512f, 0.0002395534f, -0.0041958998f,
            -0.0012736106f, -0.0026630874f, -0.0027079830f,
            -0.0007329762f, -0.0098105661f, 0.0067997380f,
            -0.0384203574f, -0.0138792591f, -0.0456945099f,
            -0.2486975449f, 0.0000000000f, 0.2795189371f,
            0.0635356145f, -0.0027295414f, 0.0388627641f,
            -0.0103932776f, 0.0054815902f, 0.0009149905f,
            0.0001902552f, 0.0030403758f, -0.0000271176f,
            0.0042257543f, -0.0006610749f, 0.0024722487f,
        },
        { // Filter 19
            -0.0028962530f, 0.0002747699f, -0.0042162676f,
            -0.0011714711f, -0.0027087582f, -0.0025233036f,
            -0.0007508677f, -0.0095261335f, 0.0070987166f,
            -0.0385873415f, -0.0126707273f, -0.0471464000f,
            -0.2514635412f, 0.0000000000f, 0.2771650624f,
            0.0620221538f, -0.0011723073f, 0.0389642751f,
            -0.0100971515f, 0.0059178699f, 0.0009029357f,
            0.0004226898f, 0.0030241476f, 0.0000859256f,
            0.0042415828f, -0.0006265770f, 0.0025241335f,
        },
        { // Filter 20
            -0.0028712280f, 0.0003100669f, -0.0042334830f,
            -0.0010679146f, -0.0027520331f, -0.0023336387f,
            -0.0007683356f, -0.0092277606f, 0.0073984549f,
            -0.0387322179f, -0.0114299610f, -0.0486060776f,
            -0.2541968011f, 0.0000000000f, 0.2747691687f,
            0.0605120283f, 0.0003539841f, 0.0390391757f,
            -0.0097997032f, 0.0063406379f, 0.0008902452f,
            0.0006515060f, 0.0030049884f, 0.0001984268f,
            0.0042540215f, -0.0005918658f, 0.0025734010f,
        },
        { // Filter 21
            -0.0028434764f, 0.0003454158f, -0.0042475110f,
            -0.0009630212f, -0.0027928512f, -0.0021391012f,
            -0.0007853583f, -0.0089154438f, 0.0076987584f,
            -0.0388545083f, -0.0101570226f, -0.0500731202f,
            -0.2568963462f, 0.0000000000f, 0.2723321290f,
            0.0590057036f, 0.0018491366f, 0.0390878847f,
            -0.0095011537f, 0.0067498038f, 0.0008769394f,
            0.0008765437f, 0.0029829435f, 0.0003102917f,
            0.0042630820f, -0.0005569711f, 0.0026200296f,
        },
        { // Filter 22
            -0.0028130012f, 0.0003807878f, -0.0042583187f,
            -0.0008568729f, -0.0028311528f, -0.0019398091f,
            -0.0008019141f, -0.0085891897f, 0.0079994292f,
            -0.0389537399f, -0.0088519895f, -0.0515470996f,
            -0.2595612082f, 0.0000000000f, 0.2698548295f,
            0.0575036415f, 0.0033129686f, 0.0391108286f,
            -0.0092017211f, 0.0071452880f, 0.0008630391f,
            0.0010976479f, 0.0029580602f, 0.0004214275f,
            0.0042687791f, -0.0005219228f, 0.0026640002f,
        },
        { // Filter 23
            -0.0027798073f, 0.0004161537f, -0.0042658759f,
            -0.0007495533f, -0.0028668800f, -0.0017358861f,
            -0.0008179815f, -0.0082490150f, 0.0083002665f,
            -0.0390294456f, -0.0075149533f, -0.0530275825f,
            -0.2621904291f, 0.0000000000f, 0.2673381697f,
            0.0560062994f, 0.0047453139f, 0.0391084414f,
            -0.0089016209f, 0.0075270216f, 0.0008485653f,
            0.0013146686f, 0.0029303875f, 0.0005317428f,
            0.0042711300f, -0.0004867508f, 0.0027052959f,
        },
        { // Filter 24
            -0.0027439023f, 0.0004514845f, -0.0042701548f,
            -0.0006411476f, -0.0028999764f, -0.0015274612f,
            -0.0008335390f, -0.0078949465f, 0.0086010661f,
            -0.0390811643f, -0.0061460209f, -0.0545141303f,
            -0.2647830615f, 0.0000000000f, 0.2647830615f,
            0.0545141303f, 0.0061460209f, 0.0390811644f,
            -0.0086010661f, 0.0078949465f, 0.0008335390f,
            0.0015274612f, 0.0028999765f, 0.0006411476f,
            0.0042701547f, -0.0004514845f, 0.0027439023f,
        },
        { // Filter 25
            -0.0027052959f, 0.0004867508f, -0.0042711300f,
            -0.0005317429f, -0.0029303874f, -0.0013146686f,
            -0.0008485653f, -0.0075270217f, 0.0089016209f,
            -0.0391084414f, -0.0047453138f, -0.0560062994f,
            -0.2673381697f, 0.0000000000f, 0.2621904290f,
            0.0530275825f, 0.0075149533f, 0.0390294456f,
            -0.0083002665f, 0.0082490149f, 0.0008179815f,
            0.0017358861f, 0.0028668800f, 0.0007495533f,
            0.0042658759f, -0.0004161537f, 0.0027798073f,
        },
        { // Filter 26
            -0.0026640002f, 0.0005219228f, -0.0042687791f,
            -0.0004214275f, -0.0029580602f, -0.0010976478f,
            -0.0008630391f, -0.0071452880f, 0.0092017211f,
            -0.0391108285f, -0.0033129686f, -0.0575036415f,
            -0.2698548295f, 0.0000000000f, 0.2595612082f,
            0.0515470996f, 0.0088519895f, 0.0389537399f,
            -0.0079994292f, 0.0085891897f, 0.0008019141f,
            0.0019398091f, 0.0028311529f, 0.0008568729f,
            0.0042583187f, -0.0003807877f, 0.0028130012f,
        },
        { // Filter 27
            -0.0026200296f, 0.0005569711f, -0.0042630820f,
            -0.0003102917f, -0.0029829435f, -0.0008765437f,
            -0.0008769394f, -0.0067498038f, 0.0095011537f,
            -0.0390878846f, -0.0018491365f, -0.0590057036f,
            -0.2723321290f, 0.0000000000f, 0.2568963462f,
            0.0500731202f, 0.0101570227f, 0.0388545083f,
            -0.0076987584f, 0.0089154438f, 0.0007853583f,
            0.0021391012f, 0.0027928512f, 0.0009630212f,
            0.0042475110f, -0.0003454158f, 0.0028434764f,
        },
        { // Filter 28
            -0.0025734010f, 0.0005918658f, -0.0042540215f,
            -0.0001984268f, -0.0030049884f, -0.0006515060f,
            -0.0008902452f, -0.0063406379f, 0.0097997032f,
            -0.0390391757f, -0.0003539841f, -0.0605120283f,
            -0.2747691687f, 0.0000000000f, 0.2541968010f,
            0.0486060776f, 0.0114299610f, 0.0387322179f,
            -0.0073984549f, 0.0092277606f, 0.0007683356f,
            0.0023336388f, 0.0027520331f, 0.0010679146f,
            0.0042334830f, -0.0003100669f, 0.0028712280f,
        },
        { // Filter 29
            -0.0025241335f, 0.0006265770f, -0.0042415828f,
            -0.0000859256f, -0.0030241476f, -0.0004226898f,
            -0.0009029357f, -0.0059178699f, 0.0100971515f,
            -0.0389642751f, 0.0011723073f, -0.0620221538f,
            -0.2771650624f, 0.0000000000f, 0.2514635411f,
            0.0471464000f, 0.0126707273f, 0.0385873415f,
            -0.0070987166f, 0.0095261335f, 0.0007508677f,
            0.0025233037f, 0.0027087582f, 0.0011714711f,
            0.0042162676f, -0.0002747699f, 0.0028962531f,
        },
        { // Filter 30
            -0.0024722487f, 0.0006610749f, -0.0042257543f,
            0.0000271176f, -0.0030403758f, -0.0001902552f,
            -0.0009149905f, -0.0054815902f, 0.0103932776f,
            -0.0388627641f, 0.0027295414f, -0.0635356145f,
            -0.2795189371f, 0.0000000000f, 0.2486975449f,
            0.0456945099f, 0.0138792591f, 0.0384203575f,
            -0.0067997380f, 0.0098105661f, 0.0007329762f,
            0.0027079831f, 0.0026630874f, 0.0012736106f,
            0.0041958998f, -0.0002395534f, 0.0029185512f,
        },
        { // Filter 31
            -0.0024177704f, 0.0006953295f, -0.0042065268f,
            0.0001406075f, -0.0030536299f, 0.0000456330f,
            -0.0009263891f, -0.0050319000f, 0.0106878585f,
            -0.0387342321f, 0.0043175068f, -0.0650519408f,
            -0.2818299337f, 0.0000000000f, 0.2458998002f,
            0.0442508243f, 0.0150555087f, 0.0382317491f,
            -0.0065017103f, 0.0100810715f, 0.0007146829f,
            0.0028875697f, 0.0026150833f, 0.0013742548f,
            0.0041724174f, -0.0002044456f, 0.0029381242f,
        },
        { // Filter 32
            -0.0023607249f, 0.0007293108f, -0.0041838939f,
            0.0002544474f, -0.0030638689f, 0.0002848048f,
            -0.0009371116f, -0.0045689114f, 0.0109806686f,
            -0.0385782766f, 0.0059359770f, -0.0665706595f,
            -0.2840972074f, 0.0000000000f, 0.2430713040f,
            0.0428157541f, 0.0161994427f, 0.0380220048f,
            -0.0062048213f, 0.0103376729f, 0.0006960095f,
            0.0030619616f, 0.0025648098f, 0.0014733275f,
            0.0041458603f, -0.0001694746f, 0.0029549762f,
        },
        { // Filter 33
            -0.0023011406f, 0.0007629890f, -0.0041578524f,
            0.0003685397f, -0.0030710538f, 0.0005270855f,
            -0.0009471378f, -0.0040927476f, 0.0112714800f,
            -0.0383945039f, 0.0075847109f, -0.0680912937f,
            -0.2863199281f, 0.0000000000f, 0.2402130618f,
            0.0413897045f, 0.0173110423f, 0.0377916173f,
            -0.0059092554f, 0.0105804029f, 0.0006769778f,
            0.0032310625f, 0.0025123322f, 0.0015707541f,
            0.0041162706f, -0.0001346681f, 0.0029691134f,
        },
        { // Filter 34
            -0.0022390486f, 0.0007963341f, -0.0041284016f,
            0.0004827856f, -0.0030751481f, 0.0007722957f,
            -0.0009564484f, -0.0036035428f, 0.0115600628f,
            -0.0381825291f, 0.0092634520f, -0.0696133635f,
            -0.2884972807f, 0.0000000000f, 0.2373260871f,
            0.0399730743f, 0.0183903030f, 0.0375410836f,
            -0.0056151934f, 0.0108093036f, 0.0006576095f,
            0.0033947816f, 0.0024577169f, 0.0016664622f,
            0.0040836928f, -0.0001000535f, 0.0029805446f,
        },
        { // Filter 35
            -0.0021744820f, 0.0008293163f, -0.0040955437f,
            0.0005970853f, -0.0030761172f, 0.0010202514f,
            -0.0009650239f, -0.0031014425f, 0.0118461850f,
            -0.0379419766f, 0.0109719292f, -0.0711363859f,
            -0.2906284654f, 0.0000000000f, 0.2344114014f,
            0.0385662562f, 0.0194372348f, 0.0372709048f,
            -0.0053228124f, 0.0110244265f, 0.0006379264f,
            0.0035530336f, 0.0024010317f, 0.0017603814f,
            0.0040481735f, -0.0000656578f, 0.0029892805f,
        },
        { // Filter 36
            -0.0021074764f, 0.0008619060f, -0.0040592841f,
            0.0007113383f, -0.0030739292f, 0.0012707638f,
            -0.0009728453f, -0.0025866029f, 0.0121296128f,
            -0.0376724801f, 0.0127098563f, -0.0726598750f,
            -0.2927126985f, 0.0000000000f, 0.2314700330f,
            0.0371696363f, 0.0204518615f, 0.0369815854f,
            -0.0050322859f, 0.0112258323f, 0.0006179501f,
            0.0037057386f, 0.0023423453f, 0.0018524432f,
            0.0040097615f, -0.0000315077f, 0.0029953341f,
        },
        { // Filter 37
            -0.0020380695f, 0.0008940736f, -0.0040196306f,
            0.0008254431f, -0.0030685542f, 0.0015236400f,
            -0.0009798939f, -0.0020591917f, 0.0124101102f,
            -0.0373736830f, 0.0144769323f, -0.0741833421f,
            -0.2947492121f, 0.0000000000f, 0.2285030170f,
            0.0357835942f, 0.0214342213f, 0.0366736336f,
            -0.0047437838f, 0.0114135907f, 0.0005977023f,
            0.0038528225f, 0.0022817277f, 0.0019425816f,
            0.0039685075f, 0.0000023705f, 0.0029987206f,
        },
        { // Filter 38
            -0.0019663015f, 0.0009257895f, -0.0039765945f,
            0.0009392976f, -0.0030599652f, 0.0017786824f,
            -0.0009861513f, -0.0015193879f, 0.0126874401f,
            -0.0370452387f, 0.0162728414f, -0.0757062964f,
            -0.2967372553f, 0.0000000000f, 0.2255113947f,
            0.0344085027f, 0.0223843660f, 0.0363475602f,
            -0.0044574720f, 0.0115877801f, 0.0005772045f,
            0.0039942166f, 0.0022192496f, 0.0020307323f,
            0.0039244645f, 0.0000359509f, 0.0029994572f,
        },
        { // Filter 39
            -0.0018922147f, 0.0009570246f, -0.0039301896f,
            0.0010527989f, -0.0030481371f, 0.0020356893f,
            -0.0009915995f, -0.0009673814f, 0.0129613632f,
            -0.0366868109f, 0.0180972531f, -0.0772282446f,
            -0.2986760936f, 0.0000000000f, 0.2224962134f,
            0.0330447278f, 0.0233023615f, 0.0360038791f,
            -0.0041735127f, 0.0117484878f, 0.0005564782f,
            0.0041298577f, 0.0021549829f, 0.0021168335f,
            0.0038776871f, 0.0000692081f, 0.0029975634f,
        },
        { // Filter 40
            -0.0018158537f, 0.0009877498f, -0.0038804329f,
            0.0011658436f, -0.0030330478f, 0.0022944550f,
            -0.0009962208f, -0.0004033735f, 0.0132316391f,
            -0.0362980736f, 0.0199498222f, -0.0787486915f,
            -0.3005650102f, 0.0000000000f, 0.2194585253f,
            0.0316926284f, 0.0241882871f, 0.0356431066f,
            -0.0038920641f, 0.0118958096f, 0.0005355449f,
            0.0042596882f, 0.0020890001f, 0.0022008254f,
            0.0038282321f, 0.0001021170f, 0.0029930606f,
        },
        { // Filter 41
            -0.0017372654f, 0.0010179361f, -0.0038273443f,
            0.0012783277f, -0.0030146774f, 0.0025547695f,
            -0.0009999979f, 0.0001724232f, 0.0134980260f,
            -0.0358787117f, 0.0218301888f, -0.0802671400f,
            -0.3024033057f, 0.0000000000f, 0.2163993875f,
            0.0303525562f, 0.0250422360f, 0.0352657610f,
            -0.0036132805f, 0.0120298497f, 0.0005144257f,
            0.0043836560f, 0.0020213749f, 0.0022826505f,
            0.0037761580f, 0.0001346532f, 0.0029859725f,
        },
        { // Filter 42
            -0.0016564987f, 0.0010475551f, -0.0037709466f,
            0.0013901470f, -0.0029930088f, 0.0028164190f,
            -0.0010029139f, 0.0007597848f, 0.0137602808f,
            -0.0354284209f, 0.0237379786f, -0.0817830916f,
            -0.3041902986f, 0.0000000000f, 0.2133198615f,
            0.0290248558f, 0.0258643144f, 0.0348723626f,
            -0.0033373123f, 0.0121507204f, 0.0004931419f,
            0.0045017145f, 0.0019521812f, 0.0023622537f,
            0.0037215250f, 0.0001667925f, 0.0029763245f,
        },
        { // Filter 43
            -0.0015736048f, 0.0010765784f, -0.0037112657f,
            0.0015011966f, -0.0029680275f, 0.0030791858f,
            -0.0010049525f, 0.0013584764f, 0.0140181593f,
            -0.0349469084f, 0.0256728028f, -0.0832960463f,
            -0.3059253257f, 0.0000000000f, 0.2102210126f,
            0.0277098644f, 0.0266546419f, 0.0344634333f,
            -0.0030643056f, 0.0122585422f, 0.0004717143f,
            0.0046138225f, 0.0018814940f, 0.0024395821f,
            0.0036643951f, 0.0001985114f, 0.0029641443f,
        },
        { // Filter 44
            -0.0014886371f, 0.0011049779f, -0.0036483303f,
            0.0016113717f, -0.0029397216f, 0.0033428487f,
            -0.0010060974f, 0.0019682516f, 0.0142714164f,
            -0.0344338925f, 0.0276342584f, -0.0848055030f,
            -0.3076077423f, 0.0000000000f, 0.2071039093f,
            0.0264079116f, 0.0274133514f, 0.0340394960f,
            -0.0027944028f, 0.0123534432f, 0.0004501638f,
            0.0047199444f, 0.0018093886f, 0.0025145852f,
            0.0036048319f, 0.0002297867f, 0.0029494613f,
        },
        { // Filter 45
            -0.0014016509f, 0.0011327260f, -0.0035821724f,
            0.0017205672f, -0.0029080820f, 0.0036071828f,
            -0.0010063333f, 0.0025888530f, 0.0145198059f,
            -0.0338891035f, 0.0296219278f, -0.0863109597f,
            -0.3092369228f, 0.0000000000f, 0.2039696229f,
            0.0251193193f, 0.0281405883f, 0.0336010750f,
            -0.0025277418f, 0.0124355594f, 0.0004285111f,
            0.0048200498f, 0.0017359409f, 0.0025872146f,
            0.0035429008f, 0.0002605961f, 0.0029323070f,
        },
        { // Filter 46
            -0.0013127038f, 0.0011597954f, -0.0035128265f,
            0.0018286778f, -0.0028731023f, 0.0038719599f,
            -0.0010056448f, 0.0032200118f, 0.0147630813f,
            -0.0333122834f, 0.0316353798f, -0.0878119136f,
            -0.3108122604f, 0.0000000000f, 0.2008192274f,
            0.0238444021f, 0.0288365110f, 0.0331486950f,
            -0.0022644566f, 0.0125050341f, 0.0004067767f,
            0.0049141139f, 0.0016612274f, 0.0026574246f,
            0.0034786683f, 0.0002909174f, 0.0029127146f,
        },
        { // Filter 47
            -0.0012218552f, 0.0011861589f, -0.0034403305f,
            0.0019355984f, -0.0028347789f, 0.0041369484f,
            -0.0010040175f, 0.0038614482f, 0.0150009950f,
            -0.0327031865f, 0.0336741688f, -0.0893078616f,
            -0.3123331681f, 0.0000000000f, 0.1976537983f,
            0.0225834662f, 0.0295012905f, 0.0326828811f,
            -0.0020046770f, 0.0125620181f, 0.0003849809f,
            0.0050021172f, 0.0015853249f, 0.0027251718f,
            0.0034122027f, 0.0003207292f, 0.0028907192f,
        },
    };
}

public delegate void V29RxPutBitDelegate(object? userData, int bitOrStatus);
public delegate void V29RxModemStatusDelegate(object? userData, int status);
public delegate void V29RxQamReportDelegate(
    object? userData,
    V29Rx.ComplexSample received,
    V29Rx.ComplexSample target,
    int constellationState);

public static class V29RxApi {
    public static V29Rx? v29_rx_init(
        V29Rx? state,
        int bitRate,
        V29RxPutBitDelegate? putBit,
        object? userData) {
        if (bitRate is not 4800 and not 7200 and not 9600)
            return null;

        V29Rx.PutBitHandler? handler = putBit is null
            ? null
            : bit => putBit(userData, bit);

        if (state is null) {
            state = new V29Rx(bitRate, handler);
            state.Logging.Initialize((int)SpanLogSeverity.None, null);
            state.Logging.SetProtocol("V.29 RX");
            return state;
        }

        state.SetPutBitHandler(handler);
        return state.Restart(bitRate, oldTrain: false) == 0 ? state : null;
    }

    public static int v29_rx_restart(V29Rx state, int bitRate, bool oldTrain) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Restart(bitRate, oldTrain);
    }

    public static int v29_rx_release(V29Rx state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    public static int v29_rx_free(V29Rx? state) => state?.Release() ?? 0;

    public static SpanLogState v29_rx_get_logging_state(V29Rx state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Logging;
    }

    public static void v29_rx_set_put_bit(
        V29Rx state,
        V29RxPutBitDelegate? putBit,
        object? userData) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetPutBitHandler(putBit is null ? null : bit => putBit(userData, bit));
    }

    public static void v29_rx_set_modem_status_handler(
        V29Rx state,
        V29RxModemStatusDelegate? handler,
        object? userData) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetModemStatusHandler(handler is null
            ? null
            : status => handler(userData, (int)status));
    }

    public static int v29_rx(V29Rx state, ReadOnlySpan<short> samples) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Process(samples);
    }

    public static int v29_rx(V29Rx state, short[] samples, int length) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(samples);
        if ((uint)length > (uint)samples.Length)
            throw new ArgumentOutOfRangeException(nameof(length));
        return state.Process(samples.AsSpan(0, length));
    }

    public static int v29_rx_fillin(V29Rx state, int length) {
        ArgumentNullException.ThrowIfNull(state);
        return state.FillIn(length);
    }

    public static int v29_rx_equalizer_state(
        V29Rx state,
        out ReadOnlyMemory<V29Rx.ComplexSample> coefficients) {
        ArgumentNullException.ThrowIfNull(state);
        coefficients = state.GetEqualizerState();
        return coefficients.Length;
    }

    public static float v29_rx_carrier_frequency(V29Rx state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.CarrierFrequency;
    }

    public static float v29_rx_symbol_timing_correction(V29Rx state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.SymbolTimingCorrection;
    }

    public static float v29_rx_signal_power(V29Rx state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.SignalPower;
    }

    public static void v29_rx_set_signal_cutoff(V29Rx state, float cutoff) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetSignalCutoff(cutoff);
    }

    public static void v29_rx_set_qam_report_handler(
        V29Rx state,
        V29RxQamReportDelegate? handler,
        object? userData) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetQamReportHandler(handler is null
            ? null
            : (received, target, constellationState) =>
                handler(userData, received, target, constellationState));
    }
}

