/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * V22BisRx.cs - managed C# port of v22bis.h and v22bis_rx.c
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2004 Steve Underwood
 *
 * This file is distributed under the GNU Lesser General Public License
 * version 2.1, matching the original source files.
 */

#nullable enable

namespace TKFaxEngine.Modem.V22;

[Flags]
public enum V22BisOptions {
    GuardToneNone = 0,
    GuardTone550Hz = 1,
    GuardTone1800Hz = 2,
    Bell212ACompatibilityMode = 0x100,
    UseUnscrambledZeroes = 0x200
}

public enum V22BisRxTrainingStage {
    NormalOperation = 0,
    SymbolAcquisition,
    LogPhase,
    UnscrambledOnes,
    UnscrambledOnesSustaining,
    ScrambledOnesAt1200,
    ScrambledOnesAt1200Sustaining,
    WaitForScrambledOnesAt2400,
    Parked
}

public enum V22BisTxTrainingStage {
    NormalOperation = 0,
    InitialSilence,
    InitialTimedSilence,
    UnscrambledOnes,
    UnscrambledZeroes,
    Unscrambled0011,
    ScrambledOnes1200,
    TimedScrambledOnes1200,
    ScrambledOnes2400,
    Parked
}

public static class V22BisSignalStatus {
    public const int CarrierDown = -1;
    public const int CarrierUp = -2;
    public const int TrainingSucceeded = -4;
    public const int EndOfData = -7;
    public const int ModemRetrainOccurred = -13;
}

public delegate int V22BisGetBitHandler(object? userData);
public delegate void V22BisPutBitHandler(object? userData, int bitOrStatus);
public delegate void V22BisStatusHandler(object? userData, int status);
public delegate void V22BisQamReportHandler(
    object? userData,
    V22BisComplex? received,
    V22BisComplex? target,
    int stateOrTimingCorrection);

public readonly struct V22BisComplex {
    public V22BisComplex(float real, float imaginary) {
        Real = real;
        Imaginary = imaginary;
    }

    public float Real { get; }
    public float Imaginary { get; }
    public float Re => Real;
    public float Im => Imaginary;

    public static V22BisComplex operator +(V22BisComplex a, V22BisComplex b) =>
        new(a.Real + b.Real, a.Imaginary + b.Imaginary);

    public static V22BisComplex operator -(V22BisComplex a, V22BisComplex b) =>
        new(a.Real - b.Real, a.Imaginary - b.Imaginary);

    public static V22BisComplex operator *(V22BisComplex a, V22BisComplex b) =>
        new(
            a.Real * b.Real - a.Imaginary * b.Imaginary,
            a.Real * b.Imaginary + a.Imaginary * b.Real);
}

public sealed class V22BisLoggingState {
    public string Protocol { get; } = "V.22bis";
    public Action<string>? Handler { get; set; }

    internal void Write(string message) {
        Handler?.Invoke(message);
    }
}

/// <summary>
/// Combined full-duplex V.22/V.22bis modem state. The implementation is split
/// between V22BisRx.cs and V22BisTx.cs, matching the original C source layout.
/// </summary>
public sealed partial class V22BisState : IDisposable {
    public const int SampleRate = 8000;
    public const int EqualizerLength = 17;
    public const int EqualizerPreLength = 8;
    public const int TxFilterSteps = 9;
    public const int RxFilterSteps = 27;
    public const int RxPulseShaperCoefficientSets = 12;
    public const int TxPulseShaperCoefficientSets = 40;

    internal const float LowCarrierFrequency = 1200.0f;
    internal const float HighCarrierFrequency = 2400.0f;
    internal const float EqualizerDelta = 0.25f;
    internal const float Dbm0MaxPower = 6.16f;
    internal const float Dbm0MaxSinePower = 3.14f;

    internal static readonly int[] PhaseSteps = { 1, 0, 2, 3 };

    internal static readonly byte[,] SpaceMap =
    {
        {11,  9,  9,  6,  6,  7},
        {10,  8,  8,  4,  4,  5},
        {10,  8,  8,  4,  4,  5},
        {13, 12, 12,  0,  0,  2},
        {13, 12, 12,  0,  0,  2},
        {15, 14, 14,  1,  1,  3}
    };

    internal sealed class RxSection {
        internal int RrcFilterStep;
        internal uint ScrambleRegister;
        internal int ScramblerPatternCount;
        internal V22BisRxTrainingStage Training;
        internal int TrainingCount;
        internal bool SignalPresent;
        internal uint CarrierPhase;
        internal int CarrierPhaseRate;
        internal V22BisQamReportHandler? QamReport;
        internal object? QamUserData;
        internal readonly PowerMeter RxPower = new(5);
        internal int CarrierOnPower;
        internal int CarrierOffPower;
        internal int ConstellationState;
        internal float AgcScaling;
        internal readonly float[] RrcFilter = new float[RxFilterSteps];
        internal float EqualizerDelta;
        internal readonly V22BisComplex[] EqualizerCoefficients = new V22BisComplex[EqualizerLength];
        internal readonly V22BisComplex[] EqualizerBuffer = new V22BisComplex[EqualizerLength];
        internal float TrainingError;
        internal float CarrierTrackProportional;
        internal float CarrierTrackIntegral;
        internal int EqualizerStep;
        internal int EqualizerPutStep;
        internal int GardnerIntegrate;
        internal int GardnerStep;
        internal int TotalBaudTimingCorrection;
        internal int BaudPhase;
        internal bool SixteenWayDecisions;
        internal int BitsPerSymbol;
        internal int SixteenWayTransitionCount;
        internal int ScrambledOnes2400Count;
        internal int PatternRepeats;
        internal int LastRawBits;
    }

    internal sealed class TxSection {
        internal float GuardToneGain;
        internal float Gain;
        internal readonly float[] RrcFilterReal = new float[TxFilterSteps];
        internal readonly float[] RrcFilterImaginary = new float[TxFilterSteps];
        internal int RrcFilterStep;
        internal uint ScrambleRegister;
        internal int ScramblerPatternCount;
        internal V22BisTxTrainingStage Training;
        internal int TrainingCount;
        internal uint CarrierPhase;
        internal int CarrierPhaseRate;
        internal uint GuardTonePhase;
        internal int GuardTonePhaseRate;
        internal int BaudPhase;
        internal int ConstellationState;
        internal int Shutdown;
        internal V22BisGetBitHandler? CurrentGetBit;
    }

    internal sealed class PowerMeter {
        internal PowerMeter(int shift) {
            Shift = shift;
        }

        internal int Shift { get; set; }
        internal int Reading { get; set; }

        internal int Update(short sample) {
            int square = sample * sample;
            Reading += (square - Reading) >> Shift;
            return Reading;
        }

        internal void Reset(int shift) {
            Shift = shift;
            Reading = 0;
        }
    }

    internal readonly RxSection Rx = new();
    internal readonly TxSection Tx = new();
    private bool _disposed;

    public int BitRate { get; internal set; }
    public int NegotiatedBitRate { get; internal set; }
    public V22BisOptions Options { get; internal set; }
    public bool CallingParty { get; internal set; }
    public V22BisGetBitHandler? GetBitHandler { get; internal set; }
    public object? GetBitUserData { get; internal set; }
    public V22BisPutBitHandler? PutBitHandler { get; internal set; }
    public object? PutBitUserData { get; internal set; }
    public V22BisStatusHandler? StatusHandler { get; internal set; }
    public object? StatusUserData { get; internal set; }
    public V22BisLoggingState Logging { get; } = new();

    public bool ReceiveSignalPresent => Rx.SignalPresent;
    public V22BisRxTrainingStage ReceiveTrainingStage => Rx.Training;
    public V22BisTxTrainingStage TransmitTrainingStage => Tx.Training;

    public float ReceiveCarrierFrequency => V22BisDsp.Frequency(TxOrRxRate: Rx.CarrierPhaseRate);

    public float ReceiveSymbolTimingCorrection =>
        Rx.TotalBaudTimingCorrection / (RxPulseShaperCoefficientSets * 40.0f / (3.0f * 2.0f));

    public float ReceiveSignalPowerDbm0 => CurrentPowerDbm0(Rx.RxPower) + 6.34f;

    public int Receive(ReadOnlySpan<short> samples) {
        ThrowIfDisposed();

        for (int i = 0; i < samples.Length; i++) {
            Rx.RrcFilter[Rx.RrcFilterStep] = samples[i];
            if (++Rx.RrcFilterStep >= RxFilterSteps) {
                Rx.RrcFilterStep = 0;
            }

            float powerSample = CallingParty
                ? CircularDot(Rx.RrcFilter, RxPulseShaper2400Real[6], Rx.RrcFilterStep)
                : CircularDot(Rx.RrcFilter, RxPulseShaper1200Real[6], Rx.RrcFilterStep);

            int power = Rx.RxPower.Update(TruncateToInt16(powerSample));
            if (Rx.SignalPresent) {
                if (power < Rx.CarrierOffPower) {
                    int negotiatedBitRate = NegotiatedBitRate;
                    RestartReceiver();
                    NegotiatedBitRate = negotiatedBitRate;
                    ReportStatusChange(V22BisSignalStatus.CarrierDown);
                    continue;
                }
            } else {
                if (power < Rx.CarrierOnPower) {
                    continue;
                }

                Rx.SignalPresent = true;
                ReportStatusChange(V22BisSignalStatus.CarrierUp);
            }

            if (Rx.Training == V22BisRxTrainingStage.Parked) {
                continue;
            }

            Rx.EqualizerPutStep -= RxPulseShaperCoefficientSets;
            if (Rx.EqualizerPutStep <= 0) {
                if (Rx.Training == V22BisRxTrainingStage.SymbolAcquisition) {
                    double rootPower = Math.Sqrt(Math.Max(power, 1));
                    Rx.AgcScaling = (float)(0.18 * 3.60 / rootPower);
                }

                int step = -Rx.EqualizerPutStep;
                if (step > RxPulseShaperCoefficientSets - 1) {
                    step = RxPulseShaperCoefficientSets - 1;
                }

                float ii;
                float qq;
                if (CallingParty) {
                    ii = CircularDot(Rx.RrcFilter, RxPulseShaper2400Real[step], Rx.RrcFilterStep);
                    qq = CircularDot(Rx.RrcFilter, RxPulseShaper2400Imaginary[step], Rx.RrcFilterStep);
                } else {
                    ii = CircularDot(Rx.RrcFilter, RxPulseShaper1200Real[step], Rx.RrcFilterStep);
                    qq = CircularDot(Rx.RrcFilter, RxPulseShaper1200Imaginary[step], Rx.RrcFilterStep);
                }

                V22BisComplex sample = new(ii * Rx.AgcScaling, qq * Rx.AgcScaling);
                V22BisComplex oscillator = V22BisDsp.LookupComplex(Rx.CarrierPhase);
                V22BisComplex baseband = new(
                    sample.Real * oscillator.Real - sample.Imaginary * oscillator.Imaginary,
                    -sample.Real * oscillator.Imaginary - sample.Imaginary * oscillator.Real);

                Rx.EqualizerPutStep += RxPulseShaperCoefficientSets * 40 / (3 * 2);
                ProcessHalfBaud(baseband);
            }

            V22BisDsp.Advance(ref Rx.CarrierPhase, Rx.CarrierPhaseRate);
        }

        return 0;
    }

    public int ReceiveFillIn(int sampleCount) {
        ThrowIfDisposed();
        if (sampleCount < 0) {
            throw new ArgumentOutOfRangeException(nameof(sampleCount));
        }

        Logging.Write($"Fill-in {sampleCount} samples");
        if (!Rx.SignalPresent) {
            return 0;
        }

        for (int i = 0; i < sampleCount; i++) {
            V22BisDsp.Advance(ref Rx.CarrierPhase, Rx.CarrierPhaseRate);
        }

        return 0;
    }

    public V22BisComplex[] GetReceiveEqualizerCoefficients() {
        ThrowIfDisposed();
        return (V22BisComplex[])Rx.EqualizerCoefficients.Clone();
    }

    public void SetReceiveSignalCutoff(float cutoffDbm0) {
        ThrowIfDisposed();
        Rx.CarrierOnPower = (int)(PowerLevelDbm0(cutoffDbm0 + 2.5f) * 0.232f);
        Rx.CarrierOffPower = (int)(PowerLevelDbm0(cutoffDbm0 - 2.5f) * 0.232f);
    }

    public void SetQamReportHandler(V22BisQamReportHandler? handler, object? userData) {
        ThrowIfDisposed();
        Rx.QamReport = handler;
        Rx.QamUserData = userData;
    }

    internal int RestartReceiver() {
        Array.Clear(Rx.RrcFilter, 0, Rx.RrcFilter.Length);
        Rx.TrainingError = 0.0f;
        Rx.RrcFilterStep = 0;
        Rx.ScrambleRegister = 0;
        Rx.ScramblerPatternCount = 0;
        Rx.Training = V22BisRxTrainingStage.SymbolAcquisition;
        Rx.TrainingCount = 0;
        Rx.SignalPresent = false;
        Rx.CarrierPhaseRate = V22BisDsp.PhaseRate(CallingParty ? HighCarrierFrequency : LowCarrierFrequency);
        Rx.CarrierPhase = 0;
        Rx.RxPower.Reset(5);
        SetReceiveSignalCutoff(-45.5f);
        Rx.AgcScaling = 0.0005f * 0.025f;
        Rx.ConstellationState = 0;
        Rx.SixteenWayDecisions = false;
        Rx.BitsPerSymbol = 2;
        Rx.SixteenWayTransitionCount = 0;
        Rx.ScrambledOnes2400Count = 0;
        ResetEqualizer();
        Rx.PatternRepeats = 0;
        Rx.LastRawBits = 0;
        Rx.GardnerIntegrate = 0;
        Rx.GardnerStep = 256;
        Rx.BaudPhase = 0;
        Rx.TotalBaudTimingCorrection = 0;
        Rx.CarrierTrackIntegral = CallingParty ? 8000.0f : 40000.0f;
        Rx.CarrierTrackProportional = 8000000.0f;
        NegotiatedBitRate = 1200;
        return 0;
    }

    internal void ReportStatusChange(int status) {
        if (StatusHandler != null) {
            StatusHandler(StatusUserData, status);
        } else {
            PutBitHandler?.Invoke(PutBitUserData, status);
        }
    }

    internal void ResetEqualizerCoefficients() {
        Array.Clear(Rx.EqualizerCoefficients, 0, Rx.EqualizerCoefficients.Length);
        Rx.EqualizerCoefficients[EqualizerPreLength] = new V22BisComplex(3.0f, 0.0f);
        Rx.EqualizerDelta = EqualizerDelta / EqualizerLength;
    }

    private void ResetEqualizer() {
        ResetEqualizerCoefficients();
        Array.Clear(Rx.EqualizerBuffer, 0, Rx.EqualizerBuffer.Length);
        Rx.EqualizerPutStep = 20 - 1;
        Rx.EqualizerStep = 0;
    }

    private V22BisComplex GetEqualizedSample() {
        V22BisComplex total = default;
        int n = EqualizerLength;
        int pos = Rx.EqualizerStep;
        for (int i = 0; i < n; i++) {
            V22BisComplex a = Rx.EqualizerBuffer[(pos + i) % n];
            V22BisComplex b = Rx.EqualizerCoefficients[i];
            total = total + a * b;
        }

        return total;
    }

    private void TuneEqualizer(V22BisComplex received, V22BisComplex target) {
        V22BisComplex error = new(
            (target.Real - received.Real) * Rx.EqualizerDelta,
            (target.Imaginary - received.Imaginary) * Rx.EqualizerDelta);

        const float leak = 0.9999f;
        int n = EqualizerLength;
        int pos = Rx.EqualizerStep;
        for (int i = 0; i < n; i++) {
            int sourceIndex = (pos + i) % n;
            V22BisComplex x = Rx.EqualizerBuffer[sourceIndex];
            V22BisComplex y = Rx.EqualizerCoefficients[i];
            Rx.EqualizerCoefficients[i] = new V22BisComplex(
                y.Real * leak + x.Imaginary * error.Imaginary + x.Real * error.Real,
                y.Imaginary * leak + x.Real * error.Imaginary - x.Imaginary * error.Real);
        }
    }

    private void TrackCarrier(V22BisComplex received, V22BisComplex target) {
        float error = received.Imaginary * target.Real - received.Real * target.Imaginary;
        Rx.CarrierPhaseRate = unchecked(Rx.CarrierPhaseRate + (int)(Rx.CarrierTrackIntegral * error));
        Rx.CarrierPhase = unchecked(Rx.CarrierPhase + (uint)(int)(Rx.CarrierTrackProportional * error));
    }

    private int Descramble(int bit) {
        bit &= 1;
        int output = (bit ^ (int)(Rx.ScrambleRegister >> 13) ^ (int)(Rx.ScrambleRegister >> 16)) & 1;
        Rx.ScrambleRegister = unchecked((Rx.ScrambleRegister << 1) | (uint)bit);

        if (Rx.ScramblerPatternCount >= 64) {
            output ^= 1;
            Rx.ScramblerPatternCount = 0;
        }

        if (bit != 0) {
            Rx.ScramblerPatternCount++;
        } else {
            Rx.ScramblerPatternCount = 0;
        }

        return output;
    }

    private void PutDecodedBit(int bit) {
        PutBitHandler?.Invoke(PutBitUserData, Descramble(bit));
    }

    private void DecodeBaud(int nearest) {
        int rawBits = PhaseSteps[((nearest >> 2) - (Rx.ConstellationState >> 2)) & 3];
        Rx.ConstellationState = nearest;
        PutDecodedBit(rawBits >> 1);
        PutDecodedBit(rawBits);
        if (Rx.BitsPerSymbol == 4) {
            PutDecodedBit(nearest >> 1);
            PutDecodedBit(nearest);
        }
    }

    private int DecodeBaudForTraining(int nearest) {
        int rawBits = PhaseSteps[((nearest >> 2) - (Rx.ConstellationState >> 2)) & 3];
        Rx.ConstellationState = nearest;
        int output = Descramble(rawBits >> 1);
        output = (output << 1) | Descramble(rawBits);
        if (Rx.BitsPerSymbol == 4) {
            output = (output << 1) | Descramble(nearest >> 1);
            output = (output << 1) | Descramble(nearest);
        }

        return output;
    }

    private void SynchronizeSymbol() {
        int[] indices = new int[3];
        int j = Rx.EqualizerStep;
        for (int i = 0; i < 3; i++) {
            if (--j < 0) {
                j = EqualizerLength - 1;
            }
            indices[i] = j;
        }

        float p;
        float q;
        if (Rx.SixteenWayDecisions) {
            p = (Rx.EqualizerBuffer[indices[2]].Real - Rx.EqualizerBuffer[indices[0]].Real) *
                Rx.EqualizerBuffer[indices[1]].Real;
            q = (Rx.EqualizerBuffer[indices[2]].Imaginary - Rx.EqualizerBuffer[indices[0]].Imaginary) *
                Rx.EqualizerBuffer[indices[1]].Imaginary;
        } else {
            V22BisComplex rotation = new(0.894427f, 0.44721f);
            V22BisComplex a = Rx.EqualizerBuffer[indices[2]] * rotation;
            V22BisComplex b = Rx.EqualizerBuffer[indices[1]] * rotation;
            V22BisComplex c = Rx.EqualizerBuffer[indices[0]] * rotation;
            p = (a.Real - c.Real) * b.Real;
            q = (a.Imaginary - c.Imaginary) * b.Imaginary;
        }

        Rx.GardnerIntegrate += p + q > 0 ? Rx.GardnerStep : -Rx.GardnerStep;
        if (Math.Abs(Rx.GardnerIntegrate) >= 16) {
            int correction = Rx.GardnerIntegrate / 16;
            Rx.EqualizerPutStep += correction;
            Rx.TotalBaudTimingCorrection += correction;
            Rx.QamReport?.Invoke(Rx.QamUserData, null, null, Rx.GardnerIntegrate);
            Rx.GardnerIntegrate = 0;
        }
    }

    private void ProcessHalfBaud(V22BisComplex sample) {
        Rx.EqualizerBuffer[Rx.EqualizerStep] = sample;
        if (++Rx.EqualizerStep >= EqualizerLength) {
            Rx.EqualizerStep = 0;
        }

        Rx.BaudPhase ^= 1;
        if (Rx.BaudPhase != 0) {
            return;
        }

        SynchronizeSymbol();
        V22BisComplex received = GetEqualizedSample();

        int nearest;
        if (Rx.SixteenWayDecisions) {
            int re = Math.Clamp((int)(received.Real + 3.0f), 0, 5);
            int im = Math.Clamp((int)(received.Imaginary + 3.0f), 0, 5);
            nearest = SpaceMap[re, im];
        } else {
            V22BisComplex rotation = new(0.894427f, 0.44721f);
            V22BisComplex rotated = received * rotation;
            nearest = 0x01;
            if (rotated.Real < 0) {
                nearest |= 0x04;
            }
            if (rotated.Imaginary < 0) {
                nearest ^= 0x04;
                nearest |= 0x08;
            }
        }

        int rawBits = 0;
        V22BisComplex target = received;
        switch (Rx.Training) {
            case V22BisRxTrainingStage.NormalOperation:
                target = Constellation[nearest];
                TrackCarrier(received, target);
                TuneEqualizer(received, target);
                rawBits = PhaseSteps[((nearest >> 2) - (Rx.ConstellationState >> 2)) & 3];
                if ((Rx.LastRawBits ^ rawBits) == 0x3) {
                    Rx.PatternRepeats++;
                } else {
                    if (Rx.PatternRepeats >= 50 && (Rx.LastRawBits == 0x3 || Rx.LastRawBits == 0x0)) {
                        Logging.Write($"+++ S1 detected ({Rx.PatternRepeats} long)");
                        Logging.Write("+++ Accepting a retrain request");
                        Rx.PatternRepeats = 0;
                        Rx.TrainingCount = 0;
                        Rx.SixteenWayDecisions = false;
                        Rx.BitsPerSymbol = 2;
                        Rx.SixteenWayTransitionCount = 0;
                        Rx.ScrambledOnes2400Count = 0;
                        Rx.Training = V22BisRxTrainingStage.ScrambledOnesAt1200;
                        Tx.TrainingCount = 0;
                        Tx.Training = V22BisTxTrainingStage.Unscrambled0011;
                        ResetEqualizerCoefficients();
                        ReportStatusChange(V22BisSignalStatus.ModemRetrainOccurred);
                    }
                    Rx.PatternRepeats = 0;
                }
                DecodeBaud(nearest);
                break;

            case V22BisRxTrainingStage.SymbolAcquisition:
                target = received;
                Rx.TrainingCount++;
                if (Rx.TrainingCount >= 40) {
                    Rx.GardnerStep = 4;
                    Rx.PatternRepeats = 0;
                    Rx.Training = CallingParty
                        ? V22BisRxTrainingStage.UnscrambledOnes
                        : V22BisRxTrainingStage.ScrambledOnesAt1200;
                    NegotiatedBitRate = 1200;
                } else if (Rx.TrainingCount == 30) {
                    Rx.GardnerStep = 32;
                }
                break;

            case V22BisRxTrainingStage.UnscrambledOnes:
                target = Constellation[nearest];
                TrackCarrier(received, target);
                rawBits = PhaseSteps[((nearest >> 2) - (Rx.ConstellationState >> 2)) & 3];
                Rx.ConstellationState = nearest;
                if (rawBits != Rx.LastRawBits) {
                    Rx.PatternRepeats = 0;
                } else {
                    Rx.PatternRepeats++;
                }

                Rx.TrainingCount++;
                if (Rx.TrainingCount == MillisecondsToSymbols(155 + 456)) {
                    if (rawBits == Rx.LastRawBits &&
                        (rawBits == 0x3 || rawBits == 0x0) &&
                        Rx.PatternRepeats >= MillisecondsToSymbols(456)) {
                        if (BitRate == 2400) {
                            Logging.Write("+++ starting U0011 (S1) (Caller)");
                            Tx.Training = V22BisTxTrainingStage.Unscrambled0011;
                            Tx.TrainingCount = 0;
                        } else {
                            Logging.Write("+++ starting S11 (1200) (Caller)");
                            Tx.Training = V22BisTxTrainingStage.ScrambledOnes1200;
                            Tx.TrainingCount = 0;
                        }
                    }
                    Rx.PatternRepeats = 0;
                    Rx.TrainingCount = 0;
                    Rx.Training = V22BisRxTrainingStage.UnscrambledOnesSustaining;
                }
                break;

            case V22BisRxTrainingStage.UnscrambledOnesSustaining:
                target = Constellation[nearest];
                TrackCarrier(received, target);
                rawBits = PhaseSteps[((nearest >> 2) - (Rx.ConstellationState >> 2)) & 3];
                Rx.ConstellationState = nearest;
                if (rawBits != Rx.LastRawBits) {
                    Tx.TrainingCount = 0;
                    Tx.Training = V22BisTxTrainingStage.TimedScrambledOnes1200;
                    Rx.TrainingCount = 0;
                    Rx.Training = V22BisRxTrainingStage.ScrambledOnesAt1200;
                    Rx.PatternRepeats = 0;
                }
                break;

            case V22BisRxTrainingStage.ScrambledOnesAt1200:
                target = Constellation[nearest];
                TrackCarrier(received, target);
                TuneEqualizer(received, target);
                rawBits = PhaseSteps[((nearest >> 2) - (Rx.ConstellationState >> 2)) & 3];
                int bitstream = DecodeBaudForTraining(nearest);
                Rx.TrainingCount++;
                if (NegotiatedBitRate == 1200) {
                    if ((Rx.LastRawBits ^ rawBits) == 0x3) {
                        Rx.PatternRepeats++;
                    } else {
                        if (Rx.PatternRepeats >= 15 && (Rx.LastRawBits == 0x3 || Rx.LastRawBits == 0x0)) {
                            Logging.Write($"+++ S1 detected ({Rx.PatternRepeats} long)");
                            if (BitRate == 2400) {
                                if (!CallingParty) {
                                    Logging.Write("+++ starting U0011 (S1) (Answerer)");
                                    Tx.Training = V22BisTxTrainingStage.Unscrambled0011;
                                    Tx.TrainingCount = 0;
                                }
                                NegotiatedBitRate = 2400;
                            }
                        }
                        Rx.PatternRepeats = 0;
                    }

                    if (Rx.TrainingCount >= MillisecondsToSymbols(270)) {
                        if (CallingParty) {
                            Logging.Write("+++ Rx normal operation (1200)");
                            Tx.TrainingCount = 0;
                            Tx.Training = V22BisTxTrainingStage.TimedScrambledOnes1200;
                            Rx.Training = V22BisRxTrainingStage.NormalOperation;
                            Rx.CarrierTrackIntegral = 8000.0f;
                        } else {
                            Logging.Write("+++ starting S11 (1200) (Answerer)");
                            Tx.TrainingCount = 0;
                            Tx.Training = V22BisTxTrainingStage.TimedScrambledOnes1200;
                            Rx.Training = V22BisRxTrainingStage.ScrambledOnesAt1200Sustaining;
                        }
                    }
                } else if (CallingParty) {
                    if (Rx.TrainingCount >= MillisecondsToSymbols(100 + 450)) {
                        Logging.Write("+++ starting 16 way decisions (caller)");
                        Rx.SixteenWayDecisions = true;
                        Rx.BitsPerSymbol = 2;
                        Rx.SixteenWayTransitionCount = 0;
                        Rx.ScrambledOnes2400Count = 0;
                        Rx.Training = V22BisRxTrainingStage.WaitForScrambledOnesAt2400;
                        Rx.PatternRepeats = 0;
                        Rx.CarrierTrackIntegral = 8000.0f;
                    }
                } else if (Rx.TrainingCount >= MillisecondsToSymbols(450)) {
                    Logging.Write("+++ starting 16 way decisions (answerer)");
                    Rx.SixteenWayDecisions = true;
                    Rx.BitsPerSymbol = 2;
                    Rx.SixteenWayTransitionCount = 0;
                    Rx.ScrambledOnes2400Count = 0;
                    Rx.Training = V22BisRxTrainingStage.WaitForScrambledOnesAt2400;
                    Rx.PatternRepeats = 0;
                }
                _ = bitstream;
                break;

            case V22BisRxTrainingStage.ScrambledOnesAt1200Sustaining:
                target = Constellation[nearest];
                TrackCarrier(received, target);
                TuneEqualizer(received, target);
                DecodeBaudForTraining(nearest);
                Rx.TrainingCount++;
                if (Rx.TrainingCount > MillisecondsToSymbols(270 + 765)) {
                    Logging.Write("+++ Rx normal operation (1200)");
                    Rx.Training = V22BisRxTrainingStage.NormalOperation;
                }
                break;

            case V22BisRxTrainingStage.WaitForScrambledOnesAt2400:
                target = Constellation[nearest];
                TrackCarrier(received, target);
                TuneEqualizer(received, target);
                if (Rx.SixteenWayTransitionCount < MillisecondsToSymbols(150)) {
                    Rx.SixteenWayTransitionCount++;
                    Rx.BitsPerSymbol = 2;
                    DecodeBaudForTraining(nearest);
                    break;
                }

                Rx.BitsPerSymbol = 4;
                int trainingBits = DecodeBaudForTraining(nearest);
                if (trainingBits == 0xF) {
                    Rx.ScrambledOnes2400Count++;
                    if (Rx.ScrambledOnes2400Count >= 8) {
                        Logging.Write("+++ Rx normal operation (2400)");
                        Rx.Training = V22BisRxTrainingStage.NormalOperation;
                    }
                } else {
                    Rx.ScrambledOnes2400Count = 0;
                }
                break;

            case V22BisRxTrainingStage.LogPhase:
            case V22BisRxTrainingStage.Parked:
            default:
                target = received;
                break;
        }

        Rx.LastRawBits = rawBits;
        Rx.QamReport?.Invoke(Rx.QamUserData, received, target, Rx.ConstellationState);
    }

    internal static int MillisecondsToSymbols(int milliseconds) => milliseconds * 600 / 1000;

    internal static float CircularDot(float[] x, float[] y, int position) {
        float sum = 0.0f;
        int n = x.Length;
        for (int i = 0; i < n; i++) {
            sum += x[(position + i) % n] * y[i];
        }
        return sum;
    }

    internal static short TruncateToInt16(float value) {
        int truncated = (int)value;
        if (truncated > short.MaxValue) return short.MaxValue;
        if (truncated < short.MinValue) return short.MinValue;
        return (short)truncated;
    }

    internal static short SaturateToInt16(float value) {
        int rounded = (int)MathF.Round(value);
        if (rounded > short.MaxValue) return short.MaxValue;
        if (rounded < short.MinValue) return short.MinValue;
        return (short)rounded;
    }

    internal static int PowerLevelDbm0(float level) {
        level -= Dbm0MaxPower;
        if (level > 0.0f) {
            level = 0.0f;
        }
        double ratio = Math.Pow(10.0, level / 10.0);
        return (int)(ratio * (32767.0 * 32767.0));
    }

    internal static float CurrentPowerDbm0(PowerMeter meter) {
        if (meter.Reading <= 0) {
            return -96.329f + Dbm0MaxPower;
        }
        return 10.0f * MathF.Log10(meter.Reading / (32767.0f * 32767.0f) + 1.0e-10f) + Dbm0MaxPower;
    }

    internal void ThrowIfDisposed() {
        if (_disposed) {
            throw new ObjectDisposedException(nameof(V22BisState));
        }
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        Array.Clear(Rx.RrcFilter, 0, Rx.RrcFilter.Length);
        Array.Clear(Rx.EqualizerCoefficients, 0, Rx.EqualizerCoefficients.Length);
        Array.Clear(Rx.EqualizerBuffer, 0, Rx.EqualizerBuffer.Length);
        Array.Clear(Tx.RrcFilterReal, 0, Tx.RrcFilterReal.Length);
        Array.Clear(Tx.RrcFilterImaginary, 0, Tx.RrcFilterImaginary.Length);
        GetBitHandler = null;
        PutBitHandler = null;
        StatusHandler = null;
        Logging.Handler = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    internal static readonly float[][] RxPulseShaper1200Real =
    {
        new float[] {
            -0.0077199531f, -0.0020117831f, 0.0018930905f, -0.0018886601f,
            -0.0051777074f, 0.0053673583f, 0.0259041569f, 0.0306906511f,
            0.0f, -0.0480508285f, -0.0654548563f, -0.023650088f,
            0.0481953616f, 0.0848257764f, 0.0498593404f, -0.0253378011f,
            -0.0727874866f, -0.0556792264f, 0.0f, 0.0395400094f,
            0.0360790241f, 0.0084167708f, -0.0102093222f, -0.0088088419f,
            -0.0011101265f, -0.0009952566f, -0.0061916317f
        },
        new float[] {
            -0.0076484017f, -0.0019477861f, 0.001684209f, -0.0023974435f,
            -0.0055622678f, 0.0056077999f, 0.0267290372f, 0.0314277803f,
            0.0f, -0.0487276079f, -0.066136064f, -0.0238192334f,
            0.0483954586f, 0.0849352512f, 0.0497833688f, -0.025226926f,
            -0.0722519797f, -0.055090051f, 0.0f, 0.0388079455f,
            0.0352140991f, 0.0081505533f, -0.0097573632f, -0.0081660725f,
            -0.0008185179f, -0.0011011405f, -0.0063774162f
        },
        new float[] {
            -0.0075672128f, -0.0018801216f, 0.0014678277f, -0.0029188412f,
            -0.0059534896f, 0.0058509996f, 0.0275591626f, 0.0321659901f,
            0.0f, -0.0493979284f, -0.0668055385f, -0.023983445f,
            0.0485846568f, 0.0850248959f, 0.049695811f, -0.0251104277f,
            -0.0717018016f, -0.054491449f, 0.0f, 0.0380738551f,
            0.0343511453f, 0.0078862345f, -0.0093109298f, -0.0075349297f,
            -0.0005343835f, -0.0012031064f, -0.0065521383f
        },
        new float[] {
            -0.0074762239f, -0.0018087555f, 0.0012439291f, -0.0034527905f,
            -0.0063512797f, 0.0060968805f, 0.0283942262f, 0.0329049781f,
            0.0f, -0.0500614817f, -0.0674629611f, -0.0241426429f,
            0.0487628628f, 0.0850946657f, 0.0495967106f, -0.0249883635f,
            -0.071137219f, -0.0538837034f, 0.0f, 0.0373380555f,
            0.0334905086f, 0.0076239091f, -0.0088701557f, -0.0069155483f,
            -0.0002577464f, -0.0013011702f, -0.0067159185f
        },
        new float[] {
            -0.0073752765f, -0.0017336559f, 0.0010125003f, -0.0039992207f,
            -0.0067555402f, 0.0063453638f, 0.0292339159f, 0.03364444f,
            0.0f, -0.0507179614f, -0.0681080183f, -0.0242967495f,
            0.0489299885f, 0.0851445261f, 0.0494861167f, -0.0248607937f,
            -0.0705585051f, -0.0532671006f, 0.0f, 0.0366008627f,
            0.0326325308f, 0.0073636698f, -0.0084351699f, -0.0063080551f,
            0.0000113752f, -0.0013953496f, -0.006868883f
        },
        new float[] {
            -0.0072642164f, -0.0016547927f, 0.0007735326f, -0.004558053f,
            -0.0071661687f, 0.0065963684f, 0.0300779148f, 0.0343840691f,
            0.0f, -0.0513670633f, -0.0687404015f, -0.0244456895f,
            0.0490859512f, 0.0851744523f, 0.0493640842f, -0.0247277807f,
            -0.0699659394f, -0.0526419305f, 0.0f, 0.0358625918f,
            0.0317775504f, 0.0071056075f, -0.0080060972f, -0.0057125689f,
            0.0002729677f, -0.0014856648f, -0.0070111627f
        },
        new float[] {
            -0.0071428936f, -0.0015721377f, 0.0005270217f, -0.0051292006f,
            -0.0075830582f, 0.0068498114f, 0.0309259017f, 0.0351235565f,
            0.0f, -0.052008486f, -0.0693598077f, -0.0245893901f,
            0.0492306737f, 0.0851844294f, 0.0492306737f, -0.0245893901f,
            -0.0693598077f, -0.052008486f, 0.0f, 0.0351235565f,
            0.0309259017f, 0.0068498114f, -0.0075830582f, -0.0051292006f,
            0.0005270217f, -0.0015721377f, -0.0071428936f
        },
        new float[] {
            -0.0070111627f, -0.0014856648f, 0.0002729677f, -0.005712569f,
            -0.0080060972f, 0.0071056075f, 0.0317775504f, 0.0358625918f,
            0.0f, -0.0526419305f, -0.0699659394f, -0.0247277807f,
            0.0493640842f, 0.0851744523f, 0.0490859512f, -0.0244456895f,
            -0.0687404015f, -0.0513670633f, 0.0f, 0.0343840691f,
            0.0300779148f, 0.0065963684f, -0.0071661686f, -0.004558053f,
            0.0007735326f, -0.0016547927f, -0.0072642164f
        },
        new float[] {
            -0.006868883f, -0.0013953496f, 0.0000113752f, -0.0063080551f,
            -0.0084351699f, 0.0073636698f, 0.0326325308f, 0.0366008627f,
            0.0f, -0.0532671006f, -0.0705585051f, -0.0248607937f,
            0.0494861167f, 0.0851445261f, 0.0489299885f, -0.0242967495f,
            -0.0681080183f, -0.0507179613f, 0.0f, 0.03364444f,
            0.0292339158f, 0.0063453638f, -0.0067555402f, -0.0039992207f,
            0.0010125004f, -0.0017336559f, -0.0073752765f
        },
        new float[] {
            -0.0067159185f, -0.0013011702f, -0.0002577464f, -0.0069155483f,
            -0.0088701557f, 0.0076239091f, 0.0334905086f, 0.0373380555f,
            0.0f, -0.0538837034f, -0.071137219f, -0.0249883635f,
            0.0495967106f, 0.0850946657f, 0.0487628628f, -0.0241426429f,
            -0.0674629611f, -0.0500614817f, 0.0f, 0.0329049781f,
            0.0283942262f, 0.0060968805f, -0.0063512797f, -0.0034527905f,
            0.0012439292f, -0.0018087555f, -0.0074762239f
        },
        new float[] {
            -0.0065521382f, -0.0012031064f, -0.0005343835f, -0.0075349297f,
            -0.0093109298f, 0.0078862345f, 0.0343511453f, 0.0380738552f,
            0.0f, -0.054491449f, -0.0717018016f, -0.0251104277f,
            0.049695811f, 0.0850248959f, 0.0485846568f, -0.023983445f,
            -0.0668055384f, -0.0493979284f, 0.0f, 0.0321659901f,
            0.0275591626f, 0.0058509996f, -0.0059534896f, -0.0029188412f,
            0.0014678277f, -0.0018801216f, -0.0075672128f
        },
        new float[] {
            -0.0063774162f, -0.0011011405f, -0.0008185179f, -0.0081660725f,
            -0.0097573632f, 0.0081505533f, 0.0352140991f, 0.0388079455f,
            0.0f, -0.055090051f, -0.0722519797f, -0.025226926f,
            0.0497833688f, 0.0849352512f, 0.0483954586f, -0.0238192334f,
            -0.0661360639f, -0.0487276079f, 0.0f, 0.0314277803f,
            0.0267290372f, 0.0056077999f, -0.0055622677f, -0.0023974435f,
            0.001684209f, -0.0019477861f, -0.0076484017f
        }
    };

    internal static readonly float[][] RxPulseShaper1200Imaginary =
    {
        new float[] {
            -0.0025083648f, -0.0061916317f, -0.0026056155f, 0.0f,
            -0.0071265028f, -0.0165190304f, -0.0084167708f, 0.0222980632f,
            0.0488741394f, 0.0349109704f, -0.021267572f, -0.0727874866f,
            -0.0663352244f, 0.0f, 0.0686254947f, 0.0779817332f,
            0.0236500881f, -0.0404533259f, -0.0593940904f, -0.0287274984f,
            0.0117227856f, 0.0259041569f, 0.0140519265f, 0.0f,
            -0.0015279581f, 0.0030630847f, 0.0020117831f
        },
        new float[] {
            -0.0024851164f, -0.0059946693f, -0.0023181148f, 0.0f,
            -0.0076558048f, -0.0172590335f, -0.0086847906f, 0.022833619f,
            0.0497761225f, 0.0354026794f, -0.0214889098f, -0.0733080624f,
            -0.0666106342f, 0.0f, 0.0685209288f, 0.0776404948f,
            0.0234760913f, -0.0400252649f, -0.0585499453f, -0.0281956228f,
            0.0114417544f, 0.0250848237f, 0.0134298582f, 0.0f,
            -0.0011265932f, 0.003388962f, 0.0020721481f
        },
        new float[] {
            -0.0024587365f, -0.0057864192f, -0.0020202915f, 0.0f,
            -0.0081942754f, -0.018007525f, -0.0089545147f, 0.0233699597f,
            0.0506748142f, 0.0358896958f, -0.0217064353f, -0.0738134538f,
            -0.0668710432f, 0.0f, 0.0684004158f, 0.0772819499f,
            0.0232973276f, -0.0395903551f, -0.0576985862f, -0.027662275f,
            0.0111613637f, 0.0242713341f, 0.0128153955f, 0.0f,
            -0.0007355159f, 0.0037027809f, 0.0021289188f
        },
        new float[] {
            -0.0024291724f, -0.0055667771f, -0.0017121216f, 0.0f,
            -0.0087417865f, -0.0187642687f, -0.0092258433f, 0.023906866f,
            0.0515698207f, 0.0363717955f, -0.0219200448f, -0.0743034147f,
            -0.0671163227f, 0.0f, 0.0682640158f, 0.0769062751f,
            0.0231138836f, -0.0391488021f, -0.0568404006f, -0.0271276853f,
            0.0108817259f, 0.0234639794f, 0.0122087219f, 0.0f,
            -0.0003547575f, 0.0040045901f, 0.0021821342f
        },
        new float[] {
            -0.0023963726f, -0.0053356444f, -0.0013935872f, 0.0f,
            -0.0092982033f, -0.0195290216f, -0.0094986751f, 0.0244441165f,
            0.0524607475f, 0.0368487558f, -0.0221296366f, -0.0747777061f,
            -0.0673463515f, 0.0f, 0.0681117963f, 0.0765136554f,
            0.022925848f, -0.0387008139f, -0.0559757779f, -0.0265920833f,
            0.010602952f, 0.0226630452f, 0.0116100153f, 0.0f,
            0.0000156567f, 0.0042944445f, 0.0022318354f
        },
        new float[] {
            -0.002360287f, -0.0050929284f, -0.0010646763f, 0.0f,
            -0.009863385f, -0.0203015345f, -0.0097729069f, 0.0249814885f,
            0.0533472005f, 0.037320356f, -0.0223351104f, -0.0752360963f,
            -0.0675610157f, 0.0f, 0.067943833f, 0.0761042837f,
            0.0227333118f, -0.0382466012f, -0.0551051089f, -0.0260556981f,
            0.010325152f, 0.0218688113f, 0.0110194475f, 0.0f,
            0.0003757078f, 0.004572406f, 0.0022780649f
        },
        new float[] {
            -0.0023208668f, -0.0048385425f, -0.0007253831f, 0.0f,
            -0.0104371842f, -0.0210815517f, -0.0100484346f, 0.0255187576f,
            0.0542287854f, 0.0377863769f, -0.0225363676f, -0.075678361f,
            -0.0677602091f, 0.0f, 0.0677602091f, 0.075678361f,
            0.0225363676f, -0.0377863769f, -0.0542287854f, -0.0255187576f,
            0.0100484346f, 0.0210815517f, 0.0104371842f, 0.0f,
            0.0007253831f, 0.0048385425f, 0.0023208668f
        },
        new float[] {
            -0.0022780649f, -0.004572406f, -0.0003757077f, 0.0f,
            -0.0110194475f, -0.0218688113f, -0.010325152f, 0.0260556981f,
            0.0551051089f, 0.0382466012f, -0.0227333118f, -0.0761042837f,
            -0.067943833f, 0.0f, 0.0675610157f, 0.0752360963f,
            0.0223351104f, -0.037320356f, -0.0533472005f, -0.0249814885f,
            0.0097729069f, 0.0203015345f, 0.009863385f, 0.0f,
            0.0010646763f, 0.0050929284f, 0.002360287f
        },
        new float[] {
            -0.0022318354f, -0.0042944445f, -0.0000156567f, 0.0f,
            -0.0116100153f, -0.0226630452f, -0.010602952f, 0.0265920834f,
            0.0559757779f, 0.0387008139f, -0.022925848f, -0.0765136554f,
            -0.0681117963f, 0.0f, 0.0673463515f, 0.0747777061f,
            0.0221296366f, -0.0368487558f, -0.0524607475f, -0.0244441165f,
            0.0094986751f, 0.0195290216f, 0.0092982033f, 0.0f,
            0.0013935872f, 0.0053356444f, 0.0023963726f
        },
        new float[] {
            -0.0021821342f, -0.00400459f, 0.0003547575f, 0.0f,
            -0.0122087219f, -0.0234639795f, -0.0108817259f, 0.0271276853f,
            0.0568404006f, 0.0391488021f, -0.0231138836f, -0.0769062751f,
            -0.0682640158f, 0.0f, 0.0671163227f, 0.0743034147f,
            0.0219200448f, -0.0363717954f, -0.0515698207f, -0.023906866f,
            0.0092258433f, 0.0187642687f, 0.0087417865f, 0.0f,
            0.0017121216f, 0.0055667771f, 0.0024291724f
        },
        new float[] {
            -0.0021289188f, -0.0037027809f, 0.0007355159f, 0.0f,
            -0.0128153955f, -0.0242713342f, -0.0111613637f, 0.027662275f,
            0.0576985862f, 0.0395903551f, -0.0232973276f, -0.0772819499f,
            -0.0684004158f, 0.0f, 0.0668710432f, 0.0738134538f,
            0.0217064353f, -0.0358896958f, -0.0506748142f, -0.0233699597f,
            0.0089545147f, 0.018007525f, 0.0081942754f, 0.0f,
            0.0020202915f, 0.0057864192f, 0.0024587365f
        },
        new float[] {
            -0.0020721481f, -0.003388962f, 0.0011265932f, 0.0f,
            -0.0134298583f, -0.0250848237f, -0.0114417544f, 0.0281956228f,
            0.0585499453f, 0.0400252649f, -0.0234760913f, -0.0776404948f,
            -0.0685209288f, 0.0f, 0.0666106342f, 0.0733080624f,
            0.0214889098f, -0.0354026794f, -0.0497761224f, -0.022833619f,
            0.0086847906f, 0.0172590335f, 0.0076558048f, 0.0f,
            0.0023181148f, 0.0059946694f, 0.0024851164f
        }
    };

    internal static readonly float[][] RxPulseShaper2400Real =
    {
        new float[] {
            -0.0065669843f, 0.0052669165f, 0.0009952566f, 0.0018886601f,
            -0.0027220819f, -0.0140519265f, 0.022035392f, 0.0117227856f,
            -0.0488741394f, 0.0183537833f, 0.0556792264f, -0.0619167343f,
            -0.0253378011f, 0.0848257764f, -0.0262126065f, -0.0663352244f,
            0.0619167343f, 0.021267572f, -0.0593940904f, 0.0151029396f,
            0.0306906511f, -0.022035392f, -0.0053673583f, 0.0088088419f,
            -0.0005836281f, 0.0026056155f, -0.0052669165f
        },
        new float[] {
            -0.0065061191f, 0.0050993703f, 0.0008854411f, 0.0023974435f,
            -0.0029242572f, -0.0146814108f, 0.0227370771f, 0.0120043439f,
            -0.0497761225f, 0.01861229f, 0.0562586963f, -0.0623595625f,
            -0.0254429983f, 0.0849352512f, -0.0261726658f, -0.0660449496f,
            0.0614612049f, 0.021042527f, -0.0585499453f, 0.0148233161f,
            0.0299549018f, -0.0213384255f, -0.0051297494f, 0.0081660725f,
            -0.0004303203f, 0.0028828232f, -0.0054249543f
        },
        new float[] {
            -0.0064370557f, 0.0049222222f, 0.0007716827f, 0.0029188412f,
            -0.0031299347f, -0.0153181157f, 0.023443224f, 0.0122863149f,
            -0.0506748142f, 0.0188683297f, 0.0568281853f, -0.0627894742f,
            -0.0255424656f, 0.0850248959f, -0.026126634f, -0.0657399531f,
            0.0609931955f, 0.0208138814f, -0.0576985862f, 0.0145429186f,
            0.0292208295f, -0.02064643f, -0.0048950455f, 0.0075349297f,
            -0.0002809421f, 0.0031497736f, -0.0055735817f
        },
        new float[] {
            -0.0063596559f, 0.0047353834f, 0.0006539723f, 0.0034527905f,
            -0.0033390653f, -0.0159618403f, 0.0241535715f, 0.0125685832f,
            -0.0515698207f, 0.0191217845f, 0.0573874224f, -0.0632062598f,
            -0.0256361541f, 0.0850946657f, -0.0260745338f, -0.0654203851f,
            0.0605129328f, 0.0205817433f, -0.0568404006f, 0.0142618681f,
            0.0284887282f, -0.0199596531f, -0.0046633168f, 0.0069155483f,
            -0.0001355053f, 0.0034065078f, -0.0057129015f
        },
        new float[] {
            -0.0062737849f, 0.0045387702f, 0.0005323029f, 0.0039992207f,
            -0.0035515976f, -0.016612378f, 0.0248678542f, 0.0128510325f,
            -0.0524607475f, 0.0193725374f, 0.0579361408f, -0.0636097161f,
            -0.0257240173f, 0.0851445261f, -0.0260163912f, -0.0650864028f,
            0.0600206494f, 0.020346222f, -0.0559757779f, 0.0139802855f,
            0.0277588887f, -0.0192783377f, -0.0044346312f, 0.0063080551f,
            0.0000059803f, 0.0036530727f, -0.0058430209f
        },
        new float[] {
            -0.0061793115f, 0.0043323037f, 0.0004066702f, 0.004558053f,
            -0.0037674778f, -0.0172695167f, 0.0255858026f, 0.0131335457f,
            -0.0533472005f, 0.0196204723f, 0.0584740781f, -0.0639996461f,
            -0.0258060117f, 0.0851744523f, -0.0259522349f, -0.0647381704f,
            0.0595165829f, 0.0201074282f, -0.0551051089f, 0.0136982911f,
            0.0270315989f, -0.018602722f, -0.0042090544f, 0.0057125689f,
            0.0001435076f, 0.0038895208f, -0.0059640512f
        },
        new float[] {
            -0.0060761082f, 0.0041159101f, 0.0002770717f, 0.0051292006f,
            -0.0039866496f, -0.017933039f, 0.0263071433f, 0.0134160048f,
            -0.0542287854f, 0.019865474f, 0.0590009765f, -0.064375859f,
            -0.0258820968f, 0.0851844294f, -0.0258820968f, -0.064375859f,
            0.0590009765f, 0.019865474f, -0.0542287854f, 0.0134160048f,
            0.0263071433f, -0.017933039f, -0.0039866496f, 0.0051292006f,
            0.0002770717f, 0.0041159101f, -0.0060761082f
        },
        new float[] {
            -0.0059640512f, 0.0038895208f, 0.0001435076f, 0.005712569f,
            -0.0042090544f, -0.018602722f, 0.027031599f, 0.0136982911f,
            -0.0551051089f, 0.0201074282f, 0.0595165829f, -0.0647381704f,
            -0.0259522349f, 0.0851744523f, -0.0258060117f, -0.0639996461f,
            0.0584740781f, 0.0196204723f, -0.0533472005f, 0.0131335457f,
            0.0255858026f, -0.0172695167f, -0.0037674778f, 0.004558053f,
            0.0004066702f, 0.0043323037f, -0.0061793115f
        },
        new float[] {
            -0.0058430209f, 0.0036530727f, 0.0000059803f, 0.0063080551f,
            -0.0044346313f, -0.0192783377f, 0.0277588887f, 0.0139802855f,
            -0.0559757779f, 0.020346222f, 0.0600206494f, -0.0650864028f,
            -0.0260163912f, 0.0851445261f, -0.0257240173f, -0.0636097161f,
            0.0579361408f, 0.0193725374f, -0.0524607475f, 0.0128510325f,
            0.0248678542f, -0.016612378f, -0.0035515976f, 0.0039992207f,
            0.0005323029f, 0.0045387702f, -0.0062737849f
        },
        new float[] {
            -0.0057129015f, 0.0034065078f, -0.0001355053f, 0.0069155483f,
            -0.0046633168f, -0.0199596531f, 0.0284887282f, 0.0142618681f,
            -0.0568404006f, 0.0205817433f, 0.0605129328f, -0.0654203851f,
            -0.0260745338f, 0.0850946657f, -0.0256361541f, -0.0632062598f,
            0.0573874224f, 0.0191217845f, -0.0515698207f, 0.0125685832f,
            0.0241535715f, -0.0159618403f, -0.0033390653f, 0.0034527905f,
            0.0006539723f, 0.0047353834f, -0.0063596559f
        },
        new float[] {
            -0.0055735817f, 0.0031497736f, -0.0002809421f, 0.0075349297f,
            -0.0048950455f, -0.02064643f, 0.0292208296f, 0.0145429186f,
            -0.0576985862f, 0.0208138814f, 0.0609931955f, -0.0657399531f,
            -0.026126634f, 0.0850248959f, -0.0255424656f, -0.0627894742f,
            0.0568281853f, 0.0188683297f, -0.0506748142f, 0.0122863149f,
            0.023443224f, -0.0153181157f, -0.0031299347f, 0.0029188412f,
            0.0007716827f, 0.0049222222f, -0.0064370557f
        },
        new float[] {
            -0.0054249543f, 0.0028828232f, -0.0004303203f, 0.0081660725f,
            -0.0051297494f, -0.0213384256f, 0.0299549018f, 0.0148233161f,
            -0.0585499453f, 0.021042527f, 0.0614612049f, -0.0660449496f,
            -0.0261726658f, 0.0849352512f, -0.0254429983f, -0.0623595625f,
            0.0562586963f, 0.01861229f, -0.0497761224f, 0.0120043439f,
            0.0227370771f, -0.0146814108f, -0.0029242572f, 0.0023974435f,
            0.0008854411f, 0.0050993703f, -0.0065061191f
        }
    };

    internal static readonly float[][] RxPulseShaper2400Imaginary =
    {
        new float[] {
            -0.0047711934f, -0.0038266388f, 0.0030630847f, 0.0f,
            0.0083777065f, -0.0102093222f, -0.0160096494f, 0.0360790242f,
            0.0f, -0.0564871367f, 0.0404533259f, 0.0449851407f,
            -0.0779817332f, 0.0f, 0.0806741074f, -0.0481953616f,
            -0.0449851407f, 0.0654548563f, 0.0f, -0.0464820688f,
            0.0222980632f, 0.0160096494f, -0.0165190304f, 0.0f,
            0.0017962225f, 0.0018930905f, 0.0038266389f
        },
        new float[] {
            -0.0047269722f, -0.0037049094f, 0.0027251074f, 0.0f,
            0.0089999383f, -0.0106666693f, -0.0165194535f, 0.0369455716f,
            0.0f, -0.0572827386f, 0.0408743354f, 0.0453068742f,
            -0.0783054969f, 0.0f, 0.0805511828f, -0.0479844647f,
            -0.0446541792f, 0.064762239f, 0.0f, -0.045621476f,
            0.0217635101f, 0.0155032736f, -0.0157877452f, 0.0f,
            0.0013243898f, 0.0020944937f, 0.00394146f
        },
        new float[] {
            -0.0046767947f, -0.0035762038f, 0.0023749951f, 0.0f,
            0.0096329485f, -0.0111292625f, -0.0170324992f, 0.0378133892f,
            0.0f, -0.0580707476f, 0.0412880934f, 0.0456192233f,
            -0.078611626f, 0.0f, 0.0804095114f, -0.0477628717f,
            -0.0443141504f, 0.0640585402f, 0.0f, -0.0447585011f,
            0.0212301754f, 0.0150005095f, -0.0150654009f, 0.0f,
            0.0008646507f, 0.0022884444f, 0.0040494441f
        },
        new float[] {
            -0.0046205605f, -0.0034404574f, 0.0020127196f, 0.0f,
            0.0102765864f, -0.0115969558f, -0.0175485969f, 0.0386821218f,
            0.0f, -0.0588508013f, 0.041694403f, 0.0459220358f,
            -0.0788999694f, 0.0f, 0.0802491635f, -0.047530692f,
            -0.0439652192f, 0.0633440924f, 0.0f, -0.0438935168f,
            0.0206982726f, 0.0145015368f, -0.0143522134f, 0.0f,
            0.0004170424f, 0.0024749728f, 0.0041506659f
        },
        new float[] {
            -0.0045581716f, -0.0032976096f, 0.00163826f, 0.0f,
            0.0109306936f, -0.0120695991f, -0.0180675536f, 0.0395514114f,
            0.0f, -0.0596225394f, 0.0420930702f, 0.046215164f,
            -0.0791703844f, 0.0f, 0.0800702188f, -0.0472880396f,
            -0.0436075544f, 0.0626192323f, 0.0f, -0.0430268947f,
            0.0201680132f, 0.0140065322f, -0.0136483916f, 0.0f,
            -0.0000184055f, 0.0026541127f, 0.0042452032f
        },
        new float[] {
            -0.0044895326f, -0.0031476028f, 0.0012516021f, 0.0f,
            0.0115951044f, -0.0125470384f, -0.0185891737f, 0.0404208974f,
            0.0f, -0.0603856045f, 0.0424839045f, 0.0464984647f,
            -0.0794227374f, 0.0f, 0.0798727661f, -0.047035034f,
            -0.0432413286f, 0.0618843007f, 0.0f, -0.0421590052f,
            0.0196396062f, 0.0135156687f, -0.0129541374f, 0.0f,
            -0.000441671f, 0.0028259023f, 0.0043331369f
        },
        new float[] {
            -0.004414551f, -0.0029903837f, 0.000852739f, 0.0f,
            0.0122696459f, -0.0130291155f, -0.0191132584f, 0.0412902171f,
            0.0f, -0.0611396421f, 0.0428667186f, 0.0467717993f,
            -0.0796569033f, 0.0f, 0.0796569033f, -0.0467717993f,
            -0.0428667186f, 0.0611396421f, 0.0f, -0.0412902171f,
            0.0191132583f, 0.0130291155f, -0.0122696459f, 0.0f,
            -0.000852739f, 0.0029903837f, 0.004414551f
        },
        new float[] {
            -0.0043331368f, -0.0028259023f, 0.0004416709f, 0.0f,
            0.0129541375f, -0.0135156687f, -0.0196396062f, 0.0421590052f,
            0.0f, -0.0618843008f, 0.0432413286f, 0.047035034f,
            -0.0798727661f, 0.0f, 0.0794227374f, -0.0464984647f,
            -0.0424839045f, 0.0603856045f, 0.0f, -0.0404208974f,
            0.0185891737f, 0.0125470384f, -0.0115951044f, 0.0f,
            -0.0012516021f, 0.0031476029f, 0.0044895326f
        },
        new float[] {
            -0.0042452032f, -0.0026541127f, 0.0000184055f, 0.0f,
            0.0136483916f, -0.0140065322f, -0.0201680132f, 0.0430268947f,
            0.0f, -0.0626192324f, 0.0436075544f, 0.0472880396f,
            -0.0800702188f, 0.0f, 0.0791703844f, -0.046215164f,
            -0.0420930702f, 0.0596225394f, 0.0f, -0.0395514113f,
            0.0180675536f, 0.0120695991f, -0.0109306936f, 0.0f,
            -0.00163826f, 0.0032976096f, 0.0045581716f
        },
        new float[] {
            -0.0041506659f, -0.0024749728f, -0.0004170424f, 0.0f,
            0.0143522134f, -0.0145015368f, -0.0206982726f, 0.0438935168f,
            0.0f, -0.0633440924f, 0.0439652192f, 0.047530692f,
            -0.0802491635f, 0.0f, 0.0788999694f, -0.0459220358f,
            -0.041694403f, 0.0588508013f, 0.0f, -0.0386821217f,
            0.0175485968f, 0.0115969558f, -0.0102765864f, 0.0f,
            -0.0020127196f, 0.0034404575f, 0.0046205605f
        },
        new float[] {
            -0.0040494441f, -0.0022884444f, -0.0008646507f, 0.0f,
            0.0150654009f, -0.0150005095f, -0.0212301754f, 0.0447585011f,
            0.0f, -0.0640585402f, 0.0443141504f, 0.0477628717f,
            -0.0804095114f, 0.0f, 0.078611626f, -0.0456192233f,
            -0.0412880934f, 0.0580707476f, 0.0f, -0.0378133892f,
            0.0170324992f, 0.0111292625f, -0.0096329485f, 0.0f,
            -0.0023749951f, 0.0035762038f, 0.0046767947f
        },
        new float[] {
            -0.00394146f, -0.0020944937f, -0.0013243898f, 0.0f,
            0.0157877452f, -0.0155032737f, -0.0217635101f, 0.045621476f,
            0.0f, -0.064762239f, 0.0446541792f, 0.0479844647f,
            -0.0805511828f, 0.0f, 0.0783054969f, -0.0453068742f,
            -0.0408743354f, 0.0572827385f, 0.0f, -0.0369455716f,
            0.0165194535f, 0.0106666693f, -0.0089999383f, 0.0f,
            -0.0027251074f, 0.0037049094f, 0.0047269722f
        }
    };
}

internal static class V22BisDsp {
    private const double PhaseScale = 4294967296.0;

    internal static int PhaseRate(float frequency) =>
        unchecked((int)Math.Round(frequency * PhaseScale / V22BisState.SampleRate));

    internal static float Frequency(int TxOrRxRate) =>
        (float)(TxOrRxRate * V22BisState.SampleRate / PhaseScale);

    internal static V22BisComplex LookupComplex(uint phase) {
        double radians = phase * (2.0 * Math.PI / PhaseScale);
        return new V22BisComplex((float)Math.Cos(radians), (float)Math.Sin(radians));
    }

    internal static V22BisComplex NextComplex(ref uint phase, int phaseRate) {
        V22BisComplex result = LookupComplex(phase);
        Advance(ref phase, phaseRate);
        return result;
    }

    internal static float NextModulated(ref uint phase, int phaseRate, float scale) {
        double radians = phase * (2.0 * Math.PI / PhaseScale);
        float result = (float)Math.Sin(radians) * scale;
        Advance(ref phase, phaseRate);
        return result;
    }

    internal static void Advance(ref uint phase, int phaseRate) {
        phase = unchecked(phase + (uint)phaseRate);
    }
}

public static partial class V22BisApi {
    public static int v22bis_rx(V22BisState state, short[] samples, int length) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(samples);
        if ((uint)length > (uint)samples.Length) {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        return state.Receive(samples.AsSpan(0, length));
    }

    public static int v22bis_rx_fillin(V22BisState state, int length) => state.ReceiveFillIn(length);

    public static int v22bis_rx_equalizer_state(V22BisState state, out V22BisComplex[] coefficients) {
        coefficients = state.GetReceiveEqualizerCoefficients();
        return coefficients.Length;
    }

    public static float v22bis_rx_carrier_frequency(V22BisState state) => state.ReceiveCarrierFrequency;
    public static float v22bis_rx_symbol_timing_correction(V22BisState state) => state.ReceiveSymbolTimingCorrection;
    public static float v22bis_rx_signal_power(V22BisState state) => state.ReceiveSignalPowerDbm0;
    public static void v22bis_rx_set_signal_cutoff(V22BisState state, float cutoff) => state.SetReceiveSignalCutoff(cutoff);

    public static void v22bis_rx_set_qam_report_handler(
        V22BisState state,
        V22BisQamReportHandler? handler,
        object? userData) => state.SetQamReportHandler(handler, userData);

    public static int v22bis_rx_restart(V22BisState state) => state.RestartReceiver();
    public static void v22bis_equalizer_coefficient_reset(V22BisState state) => state.ResetEqualizerCoefficients();
    public static void v22bis_report_status_change(V22BisState state, int status) => state.ReportStatusChange(status);
}
