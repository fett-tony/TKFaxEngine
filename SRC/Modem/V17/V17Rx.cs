/*
 * TKFaxEngine - managed C# port
 *
 * V17Rx.cs
 *
 * Combined port of:
 *   v17rx.h
 *   private/v17rx.h (merged into the supplied v17rx.h)
 *   v17rx.c
 *
 * The generated V.17/V.32bis receive tables used by v17rx.c are embedded
 * in this managed module so the receiver remains a single C# source file.
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2003-2007 Steve Underwood.
 *
 * This file preserves the GNU Lesser General Public License version 2.1
 * licensing terms of the original sources.
 */

#nullable enable

namespace TKFaxEngine.Modem.V17;

public readonly record struct V17RxComplex(float Real, float Imaginary) {
    public float Re => Real;
    public float Im => Imaginary;

    public static V17RxComplex operator +(V17RxComplex a, V17RxComplex b) =>
        new(a.Real + b.Real, a.Imaginary + b.Imaginary);

    public static V17RxComplex operator -(V17RxComplex a, V17RxComplex b) =>
        new(a.Real - b.Real, a.Imaginary - b.Imaginary);

    public static V17RxComplex operator *(V17RxComplex a, V17RxComplex b) =>
        new(
            a.Real * b.Real - a.Imaginary * b.Imaginary,
            a.Real * b.Imaginary + a.Imaginary * b.Real);
}

public delegate void V17RxPutBitHandler(object? userData, int bitOrStatus);

public delegate void V17RxModemStatusHandler(object? userData, int status);

public delegate void V17RxQamReportHandler(
    object? userData,
    V17RxComplex received,
    V17RxComplex target,
    int symbol);

public enum V17RxSignalStatus {
    CarrierDown = -1,
    CarrierUp = -2,
    TrainingInProgress = -3,
    TrainingSucceeded = -4,
    TrainingFailed = -5
}

public enum V17RxTrainingStage {
    NormalOperation = 0,
    SymbolAcquisition = 1,
    LogPhase = 2,
    ShortWaitForCdba = 3,
    WaitForCdba = 4,
    CoarseTrainOnCdba = 5,
    FineTrainOnCdba = 6,
    ShortTrainOnCdbaAndTest = 7,
    TrainOnCdbaAndTest = 8,
    Bridge = 9,
    TcmWindup = 10,
    TestOnes = 11,
    Parked = 12
}

public sealed class V17RxLogger {
    public string Protocol { get; set; } = "V.17 RX";

    public Action<string>? FlowSink { get; set; }

    public Action<string>? WarningSink { get; set; }

    public void Flow(string message) => FlowSink?.Invoke(message);

    public void Warning(string message) =>
        (WarningSink ?? FlowSink)?.Invoke(message);
}

public sealed class V17RxState : IDisposable {
    internal const int EqualizerLength = 33;
    internal const int EqualizerPreLength = 16;
    internal const int ReceiveFilterSteps = 27;
    internal const int TrellisStorageDepth = 16;
    internal const int TrellisLookbackDepth = 16;

    internal V17RxPutBitHandler? PutBitHandler;
    internal object? PutBitUserData;
    internal V17RxModemStatusHandler? StatusHandler;
    internal object? StatusUserData;
    internal V17RxQamReportHandler? QamReportHandler;
    internal object? QamUserData;

    internal float AgcScaling;
    internal float AgcScalingSave;
    internal float EqualizerDelta;
    internal readonly V17RxComplex[] EqualizerCoefficients = new V17RxComplex[EqualizerLength];
    internal readonly V17RxComplex[] EqualizerCoefficientsSave = new V17RxComplex[EqualizerLength];
    internal readonly V17RxComplex[] EqualizerBuffer = new V17RxComplex[EqualizerLength];
    internal float TrainingError;
    internal float CarrierTrackProportional;
    internal float CarrierTrackIntegral;
    internal readonly float[] ReceiveFilter = new float[ReceiveFilterSteps];
    internal V17RxComplex[] Constellation = Array.Empty<V17RxComplex>();
    internal readonly V17RxGodardState Godard = new();

    internal int ReceiveFilterStep;
    internal int DifferentialState;
    internal uint ScrambleRegister;
    internal int ScramblerTap = 17;
    internal bool ShortTrain;
    internal V17RxTrainingStage TrainingStage;
    internal int TrainingCount;
    internal short LastSample;
    internal int SignalPresent;
    internal bool CarrierDropPending;
    internal int LowSamples;
    internal short HighSample;
    internal uint CarrierPhase;
    internal int CarrierPhaseRate;
    internal int CarrierPhaseRateSave;
    internal readonly V17RxPowerMeter Power = new();
    internal int CarrierOnPower;
    internal int CarrierOffPower;
    internal int EqualizerStep;
    internal int EqualizerPutStep;
    internal int EqualizerSkip;
    internal int BaudHalf;
    internal readonly int[] LastAngles = new int[2];
    internal readonly int[] DifferenceAngles = new int[16];
    internal int SpaceMap;
    internal int BitsPerSymbol;
    internal int TrellisPointer;
    internal readonly int[,] FullPathToPastStateLocations =
        new int[TrellisStorageDepth, 8];
    internal readonly int[,] PastStateLocations =
        new int[TrellisStorageDepth, 8];
    internal readonly float[] Distances = new float[8];

    private bool _disposed;

    internal V17RxState() {
    }

    public V17RxState(
        int bitRate,
        V17RxPutBitHandler? putBit,
        object? userData = null) {
        if (V17Rx.Initialize(this, bitRate, putBit, userData) is null) {
            throw new ArgumentOutOfRangeException(
                nameof(bitRate),
                bitRate,
                "Valid V.17 receive bit rates are 4800, 7200, 9600, 12000 and 14400 bit/s.");
        }
    }

    public int BitRate { get; internal set; }

    public int ScramblerTapValue {
        get => ScramblerTap;
        set => ScramblerTap = value;
    }

    public bool UsesShortTraining => ShortTrain;

    public V17RxTrainingStage CurrentTrainingStage => TrainingStage;

    public int CurrentTrainingCount => TrainingCount;

    public bool CarrierPresent => SignalPresent > 0;

    public float CarrierFrequency => V17Rx.CarrierFrequency(this);

    public float SymbolTimingCorrection =>
        V17Rx.SymbolTimingCorrection(this);

    public float SignalPower => V17Rx.SignalPower(this);

    public V17RxLogger Logging { get; } = new();

    public int Receive(ReadOnlySpan<short> samples) =>
        V17Rx.Receive(this, samples);

    public int ReceiveFillIn(int length) =>
        V17Rx.ReceiveFillIn(this, length);

    public int Restart(int bitRate, int shortTrain) =>
        V17Rx.Restart(this, bitRate, shortTrain);

    public void SetSignalCutoff(float cutoffDbm0) =>
        V17Rx.SetSignalCutoff(this, cutoffDbm0);

    public void SetPutBit(
        V17RxPutBitHandler? handler,
        object? userData) =>
        V17Rx.SetPutBit(this, handler, userData);

    public void SetModemStatusHandler(
        V17RxModemStatusHandler? handler,
        object? userData) =>
        V17Rx.SetModemStatusHandler(this, handler, userData);

    public void SetQamReportHandler(
        V17RxQamReportHandler? handler,
        object? userData) =>
        V17Rx.SetQamReportHandler(this, handler, userData);

    public ReadOnlyMemory<V17RxComplex> GetEqualizerState() =>
        EqualizerCoefficients;

    public int Release() => 0;

    public void Dispose() {
        if (_disposed)
            return;

        PutBitHandler = null;
        PutBitUserData = null;
        StatusHandler = null;
        StatusUserData = null;
        QamReportHandler = null;
        QamUserData = null;
        Array.Clear(EqualizerCoefficients);
        Array.Clear(EqualizerCoefficientsSave);
        Array.Clear(EqualizerBuffer);
        Array.Clear(ReceiveFilter);
        Constellation = Array.Empty<V17RxComplex>();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    internal void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

internal sealed class V17RxPowerMeter {
    internal int Shift;
    internal int Reading;

    internal void Initialize(int shift) {
        Shift = shift;
        Reading = 0;
    }

    internal int Update(short sample) {
        int square = sample * sample;
        Reading += (square - Reading) >> Shift;
        return Reading;
    }
}

internal sealed class V17RxGodardState {
    private readonly float[] _lowBandEdge = new float[2];
    private readonly float[] _highBandEdge = new float[2];
    private readonly float[] _dcFilter = new float[2];
    private float _baudPhase;

    internal int TotalBaudTimingCorrection { get; private set; }

    internal void Reset() {
        Array.Clear(_lowBandEdge);
        Array.Clear(_highBandEdge);
        Array.Clear(_dcFilter);
        _baudPhase = 0f;
        TotalBaudTimingCorrection = 0;
    }

    internal void Receive(float sample) {
        float value =
            _lowBandEdge[0] * 1.764193f +
            _lowBandEdge[1] * -0.980100f +
            sample;

        _lowBandEdge[1] = _lowBandEdge[0];
        _lowBandEdge[0] = value;

        value =
            _highBandEdge[0] * -1.400071f +
            _highBandEdge[1] * -0.980100f +
            sample;

        _highBandEdge[1] = _highBandEdge[0];
        _highBandEdge[0] = value;
    }

    internal int PerBaud() {
        float value =
            _lowBandEdge[1] * _highBandEdge[0] * -0.449451f -
            _lowBandEdge[0] * _highBandEdge[1] * -0.700036f +
            _lowBandEdge[1] * _highBandEdge[1] * -0.932130f;

        float filtered = value - _dcFilter[1];
        _dcFilter[1] = _dcFilter[0];
        _dcFilter[0] = value;
        _baudPhase -= filtered;

        float magnitude = MathF.Abs(_baudPhase);
        if (magnitude <= 100.0f)
            return 0;

        int correction = magnitude > 1000.0f ? 15 : 1;
        if (_baudPhase < 0f)
            correction = -correction;

        TotalBaudTimingCorrection += correction;
        return correction;
    }
}

public static class V17Rx {
    public const float ConstellationScalingFactor = 1.0f;
    public const int SampleRate = 8000;
    public const float CarrierNominalFrequency = 1800.0f;
    public const int BaudRate = 2400;
    public const int ReceivePulseShaperCoefficientSets = 192;
    public const float ReceivePulseShaperGain = 1.0f;

    private const int TrainingSegment1Length = 256;
    private const int TrainingSegment2Length = 2976;
    private const int TrainingShortSegment2Length = 38;
    private const int TrainingSegment3Length = 64;
    private const int TrainingSegment4ALength = 15;
    private const int TrainingSegment4Length = 48;
    private const int BridgeWord = 0x8880;
    private const float EqualizerFastAdaptationDelta =
        0.21f / V17RxState.EqualizerLength;
    private const float EqualizerSlowAdaptationDelta =
        0.1f * EqualizerFastAdaptationDelta;
    private const float Dbm0MaximumPower = 6.16f;
    private const double PhaseScale = 4294967296.0;

    private static readonly float[] ConstellationSpacing =
    {
        1.414f,
        2.0f,
        2.828f,
        4.0f
    };

    private static readonly V17RxComplex[] Cdba =
    {
        new(6.0f, 2.0f),
        new(-2.0f, 6.0f),
        new(2.0f, -6.0f),
        new(-6.0f, -2.0f)
    };

    private static readonly byte[,] V32Bis4800DifferentialDecoder =
    {
        { 2, 3, 0, 1 },
        { 0, 2, 1, 3 },
        { 3, 1, 2, 0 },
        { 1, 0, 3, 2 }
    };

    private static readonly byte[,] V17DifferentialDecoder =
    {
        { 0, 1, 2, 3 },
        { 3, 0, 1, 2 },
        { 2, 3, 0, 1 },
        { 1, 2, 3, 0 }
    };

    private static readonly byte[,] TcmPaths =
    {
        { 0, 6, 2, 4 },
        { 6, 0, 4, 2 },
        { 2, 4, 0, 6 },
        { 4, 2, 6, 0 },
        { 1, 3, 7, 5 },
        { 5, 7, 3, 1 },
        { 7, 5, 1, 3 },
        { 3, 1, 5, 7 }
    };

    public static V17RxState? Initialize(
        V17RxState? state,
        int bitRate,
        V17RxPutBitHandler? putBit,
        object? userData) {
        if (!IsValidBitRate(bitRate))
            return null;

        state ??= new V17RxState();

        state.PutBitHandler = putBit;
        state.PutBitUserData = userData;
        state.ShortTrain = false;
        state.ScramblerTap = 17;
        state.Logging.Protocol = "V.17 RX";
        SetSignalCutoff(state, -45.5f);
        state.CarrierPhaseRateSave = PhaseRate(CarrierNominalFrequency);

        if (Restart(state, bitRate, 0) < 0)
            return null;

        return state;
    }

    public static bool IsValidBitRate(int bitRate) =>
        bitRate is 4800 or 7200 or 9600 or 12000 or 14400;

    public static float CarrierFrequency(V17RxState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        return Frequency(state.CarrierPhaseRate);
    }

    public static float SymbolTimingCorrection(V17RxState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();

        return state.Godard.TotalBaudTimingCorrection /
            (ReceivePulseShaperCoefficientSets * 10.0f / 3.0f);
    }

    public static float SignalPower(V17RxState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        return CurrentPowerDbm0(state.Power) + 3.98f;
    }

    public static void SetSignalCutoff(
        V17RxState state,
        float cutoffDbm0) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();

        state.CarrierOnPower =
            (int)(PowerLevelDbm0(cutoffDbm0 + 2.5f) * 0.4f);

        state.CarrierOffPower =
            (int)(PowerLevelDbm0(cutoffDbm0 - 2.5f) * 0.4f);
    }

    public static int EqualizerState(
        V17RxState state,
        out ReadOnlyMemory<V17RxComplex> coefficients) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        coefficients = state.EqualizerCoefficients;
        return V17RxState.EqualizerLength;
    }

    public static int Receive(
        V17RxState state,
        ReadOnlySpan<short> samples) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();

        for (int index = 0; index < samples.Length; index++) {
            short amplitude = samples[index];

            state.ReceiveFilter[state.ReceiveFilterStep] = amplitude;
            if (++state.ReceiveFilterStep >= V17RxState.ReceiveFilterSteps)
                state.ReceiveFilterStep = 0;

            int power = SignalDetect(state, amplitude);
            if (power == 0 ||
                state.TrainingStage == V17RxTrainingStage.Parked) {
                continue;
            }

            state.EqualizerPutStep -=
                ReceivePulseShaperCoefficientSets;

            int step = -state.EqualizerPutStep;
            if (step < 0)
                step += ReceivePulseShaperCoefficientSets;

            step = Math.Clamp(
                step,
                0,
                ReceivePulseShaperCoefficientSets - 1);

            float real = CircularDot(
                state.ReceiveFilter,
                ReceivePulseShaperReal,
                step * V17RxState.ReceiveFilterSteps,
                V17RxState.ReceiveFilterSteps,
                state.ReceiveFilterStep);

            V17RxComplex sample = new(
                real * state.AgcScaling,
                0f);

            state.Godard.Receive(sample.Real);

            if (state.EqualizerPutStep <= 0) {
                if (state.AgcScalingSave == 0f) {
                    int rootPower = IntegerSquareRoot(power);
                    if (rootPower == 0)
                        rootPower = 1;

                    state.AgcScaling =
                        (2.17f / ReceivePulseShaperGain) /
                        rootPower;
                }

                float imaginary = CircularDot(
                    state.ReceiveFilter,
                    ReceivePulseShaperImaginary,
                    step * V17RxState.ReceiveFilterSteps,
                    V17RxState.ReceiveFilterSteps,
                    state.ReceiveFilterStep);

                sample = new(
                    sample.Real,
                    imaginary * state.AgcScaling);

                V17RxComplex oscillator =
                    LookupComplex(state.CarrierPhase);

                V17RxComplex baseband = new(
                    sample.Real * oscillator.Real -
                    sample.Imaginary * oscillator.Imaginary,
                    -sample.Real * oscillator.Imaginary -
                    sample.Imaginary * oscillator.Real);

                state.EqualizerPutStep +=
                    ReceivePulseShaperCoefficientSets * 10 / (3 * 2);

                ProcessHalfBaud(state, baseband);
            }

            state.CarrierPhase = unchecked(
                state.CarrierPhase +
                (uint)state.CarrierPhaseRate);
        }

        return 0;
    }

    public static int Receive(
        V17RxState state,
        short[] samples,
        int length) {
        ArgumentNullException.ThrowIfNull(samples);
        if (length < 0 || length > samples.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        return Receive(state, samples.AsSpan(0, length));
    }

    public static int ReceiveFillIn(
        V17RxState state,
        int length) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();

        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        state.Logging.Flow($"Fill-in {length} samples");

        if (state.SignalPresent <= 0 ||
            state.TrainingStage == V17RxTrainingStage.Parked) {
            return 0;
        }

        for (int index = 0; index < length; index++) {
            state.CarrierPhase = unchecked(
                state.CarrierPhase +
                (uint)state.CarrierPhaseRate);

            state.EqualizerPutStep -=
                ReceivePulseShaperCoefficientSets;

            if (state.EqualizerPutStep <= 0) {
                state.EqualizerPutStep +=
                    ReceivePulseShaperCoefficientSets * 10 / (3 * 2);
            }
        }

        return 0;
    }

    public static void SetPutBit(
        V17RxState state,
        V17RxPutBitHandler? putBit,
        object? userData) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        state.PutBitHandler = putBit;
        state.PutBitUserData = userData;
    }

    public static void SetModemStatusHandler(
        V17RxState state,
        V17RxModemStatusHandler? handler,
        object? userData) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        state.StatusHandler = handler;
        state.StatusUserData = userData;
    }

    public static V17RxLogger GetLoggingState(V17RxState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        return state.Logging;
    }

    public static void SetQamReportHandler(
        V17RxState state,
        V17RxQamReportHandler? handler,
        object? userData) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        state.QamReportHandler = handler;
        state.QamUserData = userData;
    }

    public static int Restart(
        V17RxState state,
        int bitRate,
        int shortTrain) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();

        state.Logging.Flow(
            $"Restarting V.17, {bitRate}bps, " +
            $"{(shortTrain != 0 ? "short" : "long")} training");

        switch (bitRate) {
            case 14400:
                state.Constellation = Constellation14400;
                state.SpaceMap = 0;
                state.BitsPerSymbol = 6;
                break;

            case 12000:
                state.Constellation = Constellation12000;
                state.SpaceMap = 1;
                state.BitsPerSymbol = 5;
                break;

            case 9600:
                state.Constellation = Constellation9600;
                state.SpaceMap = 2;
                state.BitsPerSymbol = 4;
                break;

            case 7200:
                state.Constellation = Constellation7200;
                state.SpaceMap = 3;
                state.BitsPerSymbol = 3;
                break;

            case 4800:
                state.Constellation = Constellation4800;
                state.SpaceMap = 0;
                state.BitsPerSymbol = 2;
                break;

            default:
                return -1;
        }

        state.BitRate = bitRate;
        Array.Clear(state.ReceiveFilter);
        state.TrainingError = 0f;
        state.ReceiveFilterStep = 0;
        state.DifferentialState = 1;
        state.ScrambleRegister = 0x2ECDD5;
        state.TrainingStage = V17RxTrainingStage.SymbolAcquisition;
        state.TrainingCount = 0;
        state.SignalPresent = 0;
        state.HighSample = 0;
        state.LowSamples = 0;
        state.CarrierDropPending = false;

        if (shortTrain != 2)
            state.ShortTrain = shortTrain != 0;

        Array.Clear(state.LastAngles);
        Array.Clear(state.DifferenceAngles);

        for (int index = 0; index < state.Distances.Length; index++)
            state.Distances[index] = 99.0f;

        Array.Clear(state.FullPathToPastStateLocations);
        Array.Clear(state.PastStateLocations);
        state.Distances[0] = 0f;
        state.TrellisPointer = 14;
        state.CarrierPhase = 0;
        state.Power.Initialize(4);

        if (state.ShortTrain) {
            state.CarrierPhaseRate =
                state.CarrierPhaseRateSave;

            EqualizerRestore(state);
            state.AgcScaling =
                state.AgcScalingSave;

            state.CarrierTrackIntegral = 0f;
            state.CarrierTrackProportional = 40000f;
        } else {
            state.CarrierPhaseRate =
                PhaseRate(CarrierNominalFrequency);

            EqualizerReset(state);
            state.AgcScalingSave = 0f;
            state.AgcScaling =
                (2.17f / ReceivePulseShaperGain) / 735.0f;

            state.CarrierTrackIntegral = 5000f;
            state.CarrierTrackProportional = 40000f;
        }

        state.LastSample = 0;

        state.Logging.Flow(
            $"Gains {state.AgcScalingSave} {state.AgcScaling}");

        state.Logging.Flow(
            $"Phase rates {Frequency(state.CarrierPhaseRate)} " +
            $"{Frequency(state.CarrierPhaseRateSave)}");

        state.Godard.Reset();
        state.BaudHalf = 0;
        return 0;
    }

    public static int Release(V17RxState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    public static int Free(V17RxState? state) {
        state?.Dispose();
        return 0;
    }

    private static void ReportStatusChange(
        V17RxState state,
        V17RxSignalStatus status) {
        if (state.StatusHandler is not null) {
            state.StatusHandler(
                state.StatusUserData,
                (int)status);
        } else {
            state.PutBitHandler?.Invoke(
                state.PutBitUserData,
                (int)status);
        }
    }

    private static void EqualizerSave(V17RxState state) {
        Array.Copy(
            state.EqualizerCoefficients,
            state.EqualizerCoefficientsSave,
            V17RxState.EqualizerLength);
    }

    private static void EqualizerRestore(V17RxState state) {
        Array.Copy(
            state.EqualizerCoefficientsSave,
            state.EqualizerCoefficients,
            V17RxState.EqualizerLength);

        Array.Clear(state.EqualizerBuffer);
        state.EqualizerDelta = EqualizerSlowAdaptationDelta;
        state.EqualizerPutStep =
            ReceivePulseShaperCoefficientSets * 10 / (3 * 2) - 1;
        state.EqualizerStep = 0;
        state.EqualizerSkip = 0;
    }

    private static void EqualizerReset(V17RxState state) {
        Array.Clear(state.EqualizerCoefficients);
        state.EqualizerCoefficients[
            V17RxState.EqualizerPreLength] =
            new V17RxComplex(3.0f, 0.0f);

        Array.Clear(state.EqualizerBuffer);
        state.EqualizerDelta = EqualizerFastAdaptationDelta;
        state.EqualizerPutStep =
            ReceivePulseShaperCoefficientSets * 10 / (3 * 2) - 1;
        state.EqualizerStep = 0;
        state.EqualizerSkip = 0;
    }

    private static V17RxComplex EqualizerGet(V17RxState state) {
        V17RxComplex result = default;
        int coefficient = 0;

        for (int index = state.EqualizerStep;
             index < V17RxState.EqualizerLength;
             index++, coefficient++) {
            result += state.EqualizerBuffer[index] *
                state.EqualizerCoefficients[coefficient];
        }

        for (int index = 0;
             index < state.EqualizerStep;
             index++, coefficient++) {
            result += state.EqualizerBuffer[index] *
                state.EqualizerCoefficients[coefficient];
        }

        return result;
    }

    private static void TuneEqualizer(
        V17RxState state,
        V17RxComplex received,
        V17RxComplex target) {
        V17RxComplex difference = target - received;
        V17RxComplex error = new(
            difference.Real * state.EqualizerDelta,
            difference.Imaginary * state.EqualizerDelta);

        int coefficient = 0;

        for (int index = state.EqualizerStep;
             index < V17RxState.EqualizerLength;
             index++, coefficient++) {
            ApplyLms(
                state,
                coefficient,
                state.EqualizerBuffer[index],
                error);
        }

        for (int index = 0;
             index < state.EqualizerStep;
             index++, coefficient++) {
            ApplyLms(
                state,
                coefficient,
                state.EqualizerBuffer[index],
                error);
        }
    }

    private static void ApplyLms(
        V17RxState state,
        int coefficient,
        V17RxComplex sample,
        V17RxComplex error) {
        V17RxComplex previous =
            state.EqualizerCoefficients[coefficient];

        state.EqualizerCoefficients[coefficient] =
            new V17RxComplex(
                previous.Real * 0.9999f +
                sample.Imaginary * error.Imaginary +
                sample.Real * error.Real,

                previous.Imaginary * 0.9999f +
                sample.Real * error.Imaginary -
                sample.Imaginary * error.Real);
    }

    private static void TrackCarrier(
        V17RxState state,
        V17RxComplex received,
        V17RxComplex target) {
        float error =
            received.Imaginary * target.Real -
            received.Real * target.Imaginary;

        state.CarrierPhaseRate = unchecked(
            state.CarrierPhaseRate +
            (int)(state.CarrierTrackIntegral * error));

        state.CarrierPhase = unchecked(
            state.CarrierPhase +
            (uint)(int)(state.CarrierTrackProportional * error));
    }

    private static int Descramble(
        V17RxState state,
        int inputBit) {
        inputBit &= 1;

        int outputBit =
            (inputBit ^
             (int)(state.ScrambleRegister >> state.ScramblerTap) ^
             (int)(state.ScrambleRegister >> 22)) & 1;

        state.ScrambleRegister <<= 1;

        if (state.TrainingStage > V17RxTrainingStage.NormalOperation &&
            state.TrainingStage < V17RxTrainingStage.TcmWindup) {
            state.ScrambleRegister |= (uint)outputBit;
        } else {
            state.ScrambleRegister |= (uint)inputBit;
        }

        return outputBit;
    }

    private static void PutBit(
        V17RxState state,
        int bit) {
        int outputBit = Descramble(state, bit);

        if (state.TrainingStage ==
            V17RxTrainingStage.NormalOperation) {
            state.PutBitHandler?.Invoke(
                state.PutBitUserData,
                outputBit);
        }
    }

    private static int DecodeBaud(
        V17RxState state,
        V17RxComplex received) {
        int real = (int)((received.Real + 9.0f) * 2.0f);
        int imaginary = (int)((received.Imaginary + 9.0f) * 2.0f);

        real = Math.Clamp(real, 0, 35);
        imaginary = Math.Clamp(imaginary, 0, 35);

        if (state.BitsPerSymbol == 2) {
            int constellationState =
                ConstellationMap4800[real * 36 + imaginary];

            int raw =
                V32Bis4800DifferentialDecoder[
                    state.DifferentialState,
                    constellationState];

            state.DifferentialState = constellationState;
            PutBit(state, raw);
            PutBit(state, raw >> 1);
            return constellationState;
        }

        Span<float> distances = stackalloc float[8];
        Span<float> newDistances = stackalloc float[8];

        float minimum = float.MaxValue;
        int minimumIndex = 0;

        for (int index = 0; index < 8; index++) {
            int nearest = GetConstellationMap(
                state.SpaceMap,
                real,
                imaginary,
                index);

            distances[index] =
                DistanceSquared(
                    state.Constellation[nearest],
                    received);

            if (minimum > distances[index]) {
                minimum = distances[index];
                minimumIndex = index;
            }
        }

        int constellationStateResult =
            GetConstellationMap(
                state.SpaceMap,
                real,
                imaginary,
                minimumIndex);

        TrackCarrier(
            state,
            received,
            state.Constellation[constellationStateResult]);

        if (++state.TrellisPointer >=
            V17RxState.TrellisStorageDepth) {
            state.TrellisPointer = 0;
        }

        for (int currentState = 0;
             currentState < 8;
             currentState++) {
            int set = currentState >> 2;

            minimum =
                distances[TcmPaths[currentState, 0]] +
                state.Distances[set];

            minimumIndex = 0;

            for (int path = 1; path < 4; path++) {
                int previousState = (path << 1) + set;
                float candidate =
                    distances[TcmPaths[currentState, path]] +
                    state.Distances[previousState];

                if (minimum > candidate) {
                    minimum = candidate;
                    minimumIndex = path;
                }
            }

            int selectedPreviousState =
                (minimumIndex << 1) + set;

            newDistances[currentState] =
                state.Distances[selectedPreviousState] * 0.9f +
                distances[TcmPaths[currentState, minimumIndex]] *
                0.1f;

            state.FullPathToPastStateLocations[
                state.TrellisPointer,
                currentState] =
                GetConstellationMap(
                    state.SpaceMap,
                    real,
                    imaginary,
                    TcmPaths[currentState, minimumIndex]);

            state.PastStateLocations[
                state.TrellisPointer,
                currentState] =
                selectedPreviousState;
        }

        newDistances.CopyTo(state.Distances);

        minimum = state.Distances[0];
        minimumIndex = 0;

        for (int index = 1; index < 8; index++) {
            if (minimum > state.Distances[index]) {
                minimum = state.Distances[index];
                minimumIndex = index;
            }
        }

        int tracebackState = minimumIndex;
        int tracebackPointer = state.TrellisPointer;

        for (int depth = 0;
             depth < V17RxState.TrellisLookbackDepth - 1;
             depth++) {
            tracebackState =
                state.PastStateLocations[
                    tracebackPointer,
                    tracebackState];

            if (--tracebackPointer < 0) {
                tracebackPointer =
                    V17RxState.TrellisStorageDepth - 1;
            }
        }

        int nearestResult =
            state.FullPathToPastStateLocations[
                tracebackPointer,
                tracebackState] >> 1;

        int decoded =
            (nearestResult & 0x3C) |
            V17DifferentialDecoder[
                state.DifferentialState,
                nearestResult & 0x03];

        state.DifferentialState =
            nearestResult & 0x03;

        for (int bit = 0;
             bit < state.BitsPerSymbol;
             bit++) {
            PutBit(state, decoded);
            decoded >>= 1;
        }

        return constellationStateResult;
    }

    private static void ProcessHalfBaud(
        V17RxState state,
        V17RxComplex sample) {
        state.EqualizerBuffer[state.EqualizerStep] = sample;

        if (++state.EqualizerStep >=
            V17RxState.EqualizerLength) {
            state.EqualizerStep = 0;
        }

        state.BaudHalf ^= 1;
        if (state.BaudHalf != 0)
            return;

        state.EqualizerPutStep += state.Godard.PerBaud();

        V17RxComplex received = EqualizerGet(state);
        V17RxComplex target = default;
        int constellationState = 0;

        switch (state.TrainingStage) {
            case V17RxTrainingStage.NormalOperation:
                constellationState =
                    DecodeBaud(state, received);

                target =
                    state.Constellation[constellationState];
                break;

            case V17RxTrainingStage.SymbolAcquisition:
                if (++state.TrainingCount >= 100) {
                    state.TrainingStage =
                        V17RxTrainingStage.LogPhase;

                    Array.Clear(state.DifferenceAngles);

                    state.LastAngles[0] =
                        ArcTan2Phase(
                            received.Imaginary,
                            received.Real);

                    if (state.AgcScalingSave == 0f) {
                        state.AgcScalingSave =
                            state.AgcScaling;
                    }
                }
                break;

            case V17RxTrainingStage.LogPhase: {
                    int angle = ArcTan2Phase(
                        received.Imaginary,
                        received.Real);

                    state.TrainingCount = 1;

                    if (state.ShortTrain) {
                        if (unchecked((uint)(
                                angle -
                                state.LastAngles[0])) <
                            unchecked((uint)Phase(180.0f))) {
                            angle = state.LastAngles[0];
                            state.LastAngles[0] =
                                Phase(270.0f + 18.433f);
                            state.LastAngles[1] =
                                Phase(180.0f + 18.433f);
                        } else {
                            state.LastAngles[0] =
                                Phase(180.0f + 18.433f);
                            state.LastAngles[1] =
                                Phase(270.0f + 18.433f);
                        }

                        uint phaseStep = unchecked(
                            (uint)(angle -
                            Phase(180.0f + 18.433f)));

                        RotateEqualizerBuffer(
                            state,
                            phaseStep,
                            "short");

                        state.CarrierTrackProportional =
                            500000.0f;

                        state.CarrierPhase = unchecked(
                            state.CarrierPhase + phaseStep);

                        state.TrainingStage =
                            V17RxTrainingStage.ShortWaitForCdba;
                    } else {
                        state.LastAngles[1] = angle;
                        state.TrainingStage =
                            V17RxTrainingStage.WaitForCdba;
                    }

                    break;
                }

            case V17RxTrainingStage.WaitForCdba: {
                    int angle = ArcTan2Phase(
                        received.Imaginary,
                        received.Real);

                    int count = state.TrainingCount + 1;
                    int angularDifference = unchecked(
                        angle -
                        state.LastAngles[count & 1]);

                    state.LastAngles[count & 1] = angle;

                    state.DifferenceAngles[count & 0x0F] =
                        unchecked(
                            state.DifferenceAngles[
                                (count - 2) & 0x0F] +
                            (angularDifference >> 4));

                    if ((angularDifference > Phase(90.0f) ||
                         angularDifference < Phase(-90.0f)) &&
                        state.TrainingCount >= 13) {
                        state.Logging.Flow(
                            $"We seem to have a reversal at symbol " +
                            $"{state.TrainingCount}");

                        int distance =
                            (state.TrainingCount - 8) & ~1;

                        if (distance > 1) {
                            int historyIndex = distance & 0x0F;
                            angularDifference =
                                (state.DifferenceAngles[historyIndex] +
                                 state.DifferenceAngles[
                                    historyIndex | 1]) /
                                (distance - 1);

                            state.CarrierPhaseRate = unchecked(
                                state.CarrierPhaseRate +
                                3 * 16 *
                                (angularDifference / 20));
                        }

                        state.Logging.Flow(
                            $"Coarse carrier frequency " +
                            $"{Frequency(state.CarrierPhaseRate):F2} " +
                            $"({state.TrainingCount})");

                        if (state.CarrierPhaseRate <
                                PhaseRate(
                                    CarrierNominalFrequency - 20.0f) ||
                            state.CarrierPhaseRate >
                                PhaseRate(
                                    CarrierNominalFrequency + 20.0f)) {
                            FailTraining(
                                state,
                                "Training failed (sequence failed)",
                                clearSavedAgc: true);
                            break;
                        }

                        uint phaseStep = unchecked(
                            (uint)(angle - Phase(18.433f)));

                        RotateEqualizerBuffer(
                            state,
                            phaseStep,
                            "long");

                        state.CarrierPhase = unchecked(
                            state.CarrierPhase + phaseStep);

                        int bit = Descramble(state, 1);
                        bit =
                            (bit << 1) |
                            Descramble(state, 1);

                        target = Cdba[bit];
                        state.TrainingCount = 1;
                        state.TrainingStage =
                            V17RxTrainingStage.CoarseTrainOnCdba;

                        ReportStatusChange(
                            state,
                            V17RxSignalStatus.TrainingInProgress);

                        break;
                    }

                    if (++state.TrainingCount >
                        TrainingSegment1Length) {
                        FailTraining(
                            state,
                            "Training failed (sequence failed)",
                            clearSavedAgc: true);
                    }

                    break;
                }

            case V17RxTrainingStage.CoarseTrainOnCdba: {
                    int bit = Descramble(state, 1);
                    bit =
                        (bit << 1) |
                        Descramble(state, 1);

                    target = Cdba[bit];
                    TrackCarrier(state, received, target);
                    TuneEqualizer(state, received, target);

                    state.TrainingError =
                        Power(received - target);

                    state.TrainingCount++;

                    if (state.TrainingCount ==
                            TrainingSegment2Length - 2000 ||
                        state.TrainingError < 1.0f ||
                        state.TrainingError > 200.0f) {
                        state.EqualizerDelta =
                            EqualizerSlowAdaptationDelta;

                        state.CarrierTrackIntegral =
                            1000.0f;

                        state.TrainingStage =
                            V17RxTrainingStage.FineTrainOnCdba;
                    }

                    break;
                }

            case V17RxTrainingStage.FineTrainOnCdba: {
                    int bit = Descramble(state, 1);
                    bit =
                        (bit << 1) |
                        Descramble(state, 1);

                    target = Cdba[bit];
                    TrackCarrier(state, received, target);
                    TuneEqualizer(state, received, target);

                    if (++state.TrainingCount >=
                        TrainingSegment2Length - 48) {
                        state.TrainingError = 0f;
                        state.CarrierTrackIntegral = 100.0f;
                        state.CarrierTrackProportional =
                            500000.0f;

                        state.TrainingStage =
                            V17RxTrainingStage.TrainOnCdbaAndTest;
                    }

                    break;
                }

            case V17RxTrainingStage.TrainOnCdbaAndTest: {
                    int bit = Descramble(state, 1);
                    bit =
                        (bit << 1) |
                        Descramble(state, 1);

                    target = Cdba[bit];
                    state.TrainingCount++;

                    if (state.TrainingCount <
                        TrainingSegment2Length - 20) {
                        TrackCarrier(state, received, target);
                        TuneEqualizer(state, received, target);
                        state.TrainingError +=
                            Power(received - target);
                    } else if (state.TrainingCount >=
                          TrainingSegment2Length) {
                        state.Logging.Flow(
                            $"Long training error " +
                            $"{state.TrainingError}");

                        if (state.TrainingError <
                            20.0f * 1.414f *
                            ConstellationSpacing[state.SpaceMap]) {
                            state.TrainingError = 0f;
                            state.TrainingCount = 0;
                            state.TrainingStage =
                                V17RxTrainingStage.Bridge;
                        } else {
                            FailTraining(
                                state,
                                "Training failed (convergence failed)",
                                clearSavedAgc: true);
                        }
                    }

                    break;
                }

            case V17RxTrainingStage.Bridge:
                Descramble(
                    state,
                    BridgeWord >>
                    ((state.TrainingCount & 0x07) << 1));

                Descramble(
                    state,
                    BridgeWord >>
                    (((state.TrainingCount & 0x07) << 1) + 1));

                target = received;

                if (++state.TrainingCount >=
                    TrainingSegment3Length) {
                    state.TrainingError = 0f;
                    state.TrainingCount = 0;

                    if (state.BitsPerSymbol == 2) {
                        state.DifferentialState =
                            state.ShortTrain ? 0 : 1;

                        state.TrainingStage =
                            V17RxTrainingStage.TestOnes;
                    } else {
                        state.TrainingStage =
                            V17RxTrainingStage.TcmWindup;
                    }
                }

                break;

            case V17RxTrainingStage.ShortWaitForCdba: {
                    int angle = ArcTan2Phase(
                        received.Imaginary,
                        received.Real);

                    int angularDifference = unchecked(
                        angle -
                        state.LastAngles[
                            state.TrainingCount & 1]);

                    if (angularDifference > Phase(90.0f) ||
                        angularDifference < Phase(-90.0f)) {
                        int bit = Descramble(state, 1);
                        bit =
                            (bit << 1) |
                            Descramble(state, 1);

                        target = Cdba[bit];
                        state.TrainingError = 0f;
                        state.TrainingCount = 1;
                        state.TrainingStage =
                            V17RxTrainingStage.ShortTrainOnCdbaAndTest;
                    } else {
                        target =
                            Cdba[
                                (state.TrainingCount & 1) + 2];

                        TrackCarrier(
                            state,
                            received,
                            target);

                        if (++state.TrainingCount >
                            TrainingSegment1Length) {
                            FailTraining(
                                state,
                                "Training failed (sequence failed)",
                                clearSavedAgc: false);
                        }
                    }

                    break;
                }

            case V17RxTrainingStage.ShortTrainOnCdbaAndTest: {
                    int bit = Descramble(state, 1);
                    bit =
                        (bit << 1) |
                        Descramble(state, 1);

                    target = Cdba[bit];
                    TrackCarrier(state, received, target);

                    if (state.TrainingCount > 8) {
                        state.TrainingError +=
                            Power(received - target);
                    }

                    if (++state.TrainingCount >=
                        TrainingShortSegment2Length) {
                        state.Logging.Flow(
                            $"Short training error " +
                            $"{state.TrainingError}");

                        state.CarrierTrackIntegral = 100.0f;
                        state.CarrierTrackProportional =
                            500000.0f;

                        if (state.TrainingError <
                            (TrainingShortSegment2Length - 8) *
                            4.0f *
                            ConstellationSpacing[state.SpaceMap]) {
                            state.TrainingCount = 0;

                            if (state.BitsPerSymbol == 2) {
                                state.DifferentialState =
                                    state.ShortTrain ? 0 : 1;

                                state.TrainingError = 0f;
                                state.TrainingStage =
                                    V17RxTrainingStage.TestOnes;
                            } else {
                                state.TrainingStage =
                                    V17RxTrainingStage.TcmWindup;
                            }

                            ReportStatusChange(
                                state,
                                V17RxSignalStatus.TrainingInProgress);
                        } else {
                            FailTraining(
                                state,
                                "Short training failed " +
                                "(convergence failed)",
                                clearSavedAgc: false);
                        }
                    }

                    break;
                }

            case V17RxTrainingStage.TcmWindup:
                constellationState =
                    DecodeBaud(state, received);

                target =
                    state.Constellation[constellationState];

                state.TrainingError +=
                    Power(received - target);

                if (++state.TrainingCount >=
                    TrainingSegment4ALength) {
                    state.TrainingError = 0f;
                    state.TrainingCount = 0;
                    state.DifferentialState =
                        state.ShortTrain ? 0 : 1;
                    state.TrainingStage =
                        V17RxTrainingStage.TestOnes;
                }

                break;

            case V17RxTrainingStage.TestOnes:
                constellationState =
                    DecodeBaud(state, received);

                target =
                    state.Constellation[constellationState];

                state.TrainingError +=
                    Power(received - target);

                if (++state.TrainingCount >=
                    TrainingSegment4Length) {
                    if (state.TrainingError <
                        TrainingSegment4Length *
                        ConstellationSpacing[state.SpaceMap]) {
                        state.Logging.Flow(
                            $"Training succeeded at " +
                            $"{state.BitRate}bps " +
                            $"(constellation mismatch " +
                            $"{state.TrainingError})");

                        ReportStatusChange(
                            state,
                            V17RxSignalStatus.TrainingSucceeded);

                        state.SignalPresent = 60;
                        EqualizerSave(state);
                        state.CarrierPhaseRateSave =
                            state.CarrierPhaseRate;
                        state.ShortTrain = true;
                        state.TrainingStage =
                            V17RxTrainingStage.NormalOperation;
                    } else {
                        state.Logging.Flow(
                            $"Training failed " +
                            $"(constellation mismatch " +
                            $"{state.TrainingError})");

                        if (!state.ShortTrain)
                            state.AgcScalingSave = 0f;

                        state.TrainingStage =
                            V17RxTrainingStage.Parked;

                        ReportStatusChange(
                            state,
                            V17RxSignalStatus.TrainingFailed);
                    }
                }

                break;

            case V17RxTrainingStage.Parked:
            default:
                target = default;
                break;
        }

        state.QamReportHandler?.Invoke(
            state.QamUserData,
            received,
            target,
            constellationState);
    }

    private static int SignalDetect(
        V17RxState state,
        short amplitude) {
        short half = (short)(amplitude >> 1);
        short difference = unchecked(
            (short)(half - state.LastSample));

        state.LastSample = half;
        int power = state.Power.Update(difference);

        int magnitude = Math.Abs((int)difference);

        if (10 * magnitude < state.HighSample) {
            if (++state.LowSamples > 120) {
                state.Power.Initialize(4);
                state.HighSample = 0;
                state.LowSamples = 0;
            }
        } else {
            state.LowSamples = 0;
            if (magnitude > state.HighSample) {
                state.HighSample =
                    (short)Math.Min(
                        magnitude,
                        short.MaxValue);
            }
        }

        if (state.SignalPresent > 0) {
            if (state.CarrierDropPending ||
                power < state.CarrierOffPower) {
                if (--state.SignalPresent <= 0) {
                    Restart(
                        state,
                        state.BitRate,
                        state.ShortTrain ? 1 : 0);

                    ReportStatusChange(
                        state,
                        V17RxSignalStatus.CarrierDown);

                    return 0;
                }

                state.CarrierDropPending = true;
            }
        } else {
            if (power < state.CarrierOnPower)
                return 0;

            state.SignalPresent = 1;
            state.CarrierDropPending = false;

            ReportStatusChange(
                state,
                V17RxSignalStatus.CarrierUp);
        }

        return power;
    }

    private static void FailTraining(
        V17RxState state,
        string message,
        bool clearSavedAgc) {
        state.Logging.Flow(message);

        if (clearSavedAgc)
            state.AgcScalingSave = 0f;

        state.TrainingStage =
            V17RxTrainingStage.Parked;

        ReportStatusChange(
            state,
            V17RxSignalStatus.TrainingFailed);
    }

    private static void RotateEqualizerBuffer(
        V17RxState state,
        uint phaseStep,
        string trainingMode) {
        float radians =
            PhaseToRadians(phaseStep);

        state.Logging.Flow(
            $"Spin ({trainingMode}) by " +
            $"{radians:F5} rads");

        V17RxComplex rotation = new(
            MathF.Cos(radians),
            -MathF.Sin(radians));

        for (int index = 0;
             index < state.EqualizerBuffer.Length;
             index++) {
            state.EqualizerBuffer[index] =
                state.EqualizerBuffer[index] *
                rotation;
        }
    }

    private static int GetConstellationMap(
        int map,
        int real,
        int imaginary,
        int candidate) {
        int index =
            (((map * 36) + real) * 36 + imaginary) *
            8 + candidate;

        return ConstellationMaps[index];
    }

    private static float DistanceSquared(
        V17RxComplex left,
        V17RxComplex right) {
        float real = left.Real - right.Real;
        float imaginary =
            left.Imaginary - right.Imaginary;

        return real * real +
            imaginary * imaginary;
    }

    private static float Power(V17RxComplex value) =>
        value.Real * value.Real +
        value.Imaginary * value.Imaginary;

    private static float CircularDot(
        float[] buffer,
        float[] coefficients,
        int coefficientOffset,
        int length,
        int position) {
        float result = 0f;
        int coefficient = coefficientOffset;

        for (int index = position;
             index < length;
             index++) {
            result +=
                buffer[index] *
                coefficients[coefficient++];
        }

        for (int index = 0;
             index < position;
             index++) {
            result +=
                buffer[index] *
                coefficients[coefficient++];
        }

        return result;
    }

    private static int IntegerSquareRoot(int value) {
        if (value <= 0)
            return 0;

        return (int)MathF.Sqrt(value);
    }

    private static int PowerLevelDbm0(float level) {
        level -= Dbm0MaximumPower;
        if (level > 0f)
            level = 0f;

        return (int)(
            MathF.Pow(10f, level / 10f) *
            (32767.0f * 32767.0f));
    }

    private static float CurrentPowerDbm0(
        V17RxPowerMeter meter) {
        if (meter.Reading <= 0)
            return -96.329f + Dbm0MaximumPower;

        return
            10.0f *
            MathF.Log10(
                meter.Reading /
                (32767.0f * 32767.0f) +
                1.0e-10f) +
            Dbm0MaximumPower;
    }

    private static int PhaseRate(float frequency) =>
        (int)(frequency * PhaseScale / SampleRate);

    private static float Frequency(int phaseRate) =>
        (float)((double)phaseRate * SampleRate / PhaseScale);

    private static int Phase(float degrees) {
        double normalized =
            degrees < 0f
                ? 360.0 + degrees
                : degrees;

        uint phase = unchecked(
            (uint)(normalized *
            PhaseScale / 360.0));

        return unchecked((int)phase);
    }

    private static float PhaseToRadians(uint phase) =>
        (float)(phase * (Math.PI * 2.0) /
        PhaseScale);

    private static int ArcTan2Phase(
        float imaginary,
        float real) {
        double radians =
            Math.Atan2(imaginary, real);

        long phase = (long)(
            radians *
            PhaseScale /
            (Math.PI * 2.0));

        return unchecked((int)phase);
    }

    private static V17RxComplex LookupComplex(
        uint phase) {
        float radians = PhaseToRadians(phase);
        return new V17RxComplex(
            MathF.Cos(radians),
            MathF.Sin(radians));
    }

    private static readonly V17RxComplex[] Constellation14400 =
    {
        new(-8.0f, -3.0f), new(9.0f, 2.0f), new(2.0f, -9.0f), new(-3.0f, 8.0f),
        new(8.0f, 3.0f), new(-9.0f, -2.0f), new(-2.0f, 9.0f), new(3.0f, -8.0f),
        new(-8.0f, 1.0f), new(9.0f, -2.0f), new(-2.0f, -9.0f), new(1.0f, 8.0f),
        new(8.0f, -1.0f), new(-9.0f, 2.0f), new(2.0f, 9.0f), new(-1.0f, -8.0f),
        new(-4.0f, -3.0f), new(5.0f, 2.0f), new(2.0f, -5.0f), new(-3.0f, 4.0f),
        new(4.0f, 3.0f), new(-5.0f, -2.0f), new(-2.0f, 5.0f), new(3.0f, -4.0f),
        new(-4.0f, 1.0f), new(5.0f, -2.0f), new(-2.0f, -5.0f), new(1.0f, 4.0f),
        new(4.0f, -1.0f), new(-5.0f, 2.0f), new(2.0f, 5.0f), new(-1.0f, -4.0f),
        new(4.0f, -3.0f), new(-3.0f, 2.0f), new(2.0f, 3.0f), new(-3.0f, -4.0f),
        new(-4.0f, 3.0f), new(3.0f, -2.0f), new(-2.0f, -3.0f), new(3.0f, 4.0f),
        new(4.0f, 1.0f), new(-3.0f, -2.0f), new(-2.0f, 3.0f), new(1.0f, -4.0f),
        new(-4.0f, -1.0f), new(3.0f, 2.0f), new(2.0f, -3.0f), new(-1.0f, 4.0f),
        new(0f, -3.0f), new(1.0f, 2.0f), new(2.0f, -1.0f), new(-3.0f, 0f),
        new(0f, 3.0f), new(-1.0f, -2.0f), new(-2.0f, 1.0f), new(3.0f, 0f),
        new(0f, 1.0f), new(1.0f, -2.0f), new(-2.0f, -1.0f), new(1.0f, 0f),
        new(0f, -1.0f), new(-1.0f, 2.0f), new(2.0f, 1.0f), new(-1.0f, 0f),
        new(8.0f, -3.0f), new(-7.0f, 2.0f), new(2.0f, 7.0f), new(-3.0f, -8.0f),
        new(-8.0f, 3.0f), new(7.0f, -2.0f), new(-2.0f, -7.0f), new(3.0f, 8.0f),
        new(8.0f, 1.0f), new(-7.0f, -2.0f), new(-2.0f, 7.0f), new(1.0f, -8.0f),
        new(-8.0f, -1.0f), new(7.0f, 2.0f), new(2.0f, -7.0f), new(-1.0f, 8.0f),
        new(-4.0f, -7.0f), new(5.0f, 6.0f), new(6.0f, -5.0f), new(-7.0f, 4.0f),
        new(4.0f, 7.0f), new(-5.0f, -6.0f), new(-6.0f, 5.0f), new(7.0f, -4.0f),
        new(-4.0f, 5.0f), new(5.0f, -6.0f), new(-6.0f, -5.0f), new(5.0f, 4.0f),
        new(4.0f, -5.0f), new(-5.0f, 6.0f), new(6.0f, 5.0f), new(-5.0f, -4.0f),
        new(4.0f, -7.0f), new(-3.0f, 6.0f), new(6.0f, 3.0f), new(-7.0f, -4.0f),
        new(-4.0f, 7.0f), new(3.0f, -6.0f), new(-6.0f, -3.0f), new(7.0f, 4.0f),
        new(4.0f, 5.0f), new(-3.0f, -6.0f), new(-6.0f, 3.0f), new(5.0f, -4.0f),
        new(-4.0f, -5.0f), new(3.0f, 6.0f), new(6.0f, -3.0f), new(-5.0f, 4.0f),
        new(0f, -7.0f), new(1.0f, 6.0f), new(6.0f, -1.0f), new(-7.0f, 0f),
        new(0f, 7.0f), new(-1.0f, -6.0f), new(-6.0f, 1.0f), new(7.0f, 0f),
        new(0f, 5.0f), new(1.0f, -6.0f), new(-6.0f, -1.0f), new(5.0f, 0f),
        new(0f, -5.0f), new(-1.0f, 6.0f), new(6.0f, 1.0f), new(-5.0f, 0f)
    };

    private static readonly V17RxComplex[] Constellation12000 =
    {
        new(7.0f, 1.0f), new(-5.0f, -1.0f), new(-1.0f, 5.0f), new(1.0f, -7.0f),
        new(-7.0f, -1.0f), new(5.0f, 1.0f), new(1.0f, -5.0f), new(-1.0f, 7.0f),
        new(3.0f, -3.0f), new(-1.0f, 3.0f), new(3.0f, 1.0f), new(-3.0f, -3.0f),
        new(-3.0f, 3.0f), new(1.0f, -3.0f), new(-3.0f, -1.0f), new(3.0f, 3.0f),
        new(7.0f, -7.0f), new(-5.0f, 7.0f), new(7.0f, 5.0f), new(-7.0f, -7.0f),
        new(-7.0f, 7.0f), new(5.0f, -7.0f), new(-7.0f, -5.0f), new(7.0f, 7.0f),
        new(-1.0f, -7.0f), new(3.0f, 7.0f), new(7.0f, -3.0f), new(-7.0f, 1.0f),
        new(1.0f, 7.0f), new(-3.0f, -7.0f), new(-7.0f, 3.0f), new(7.0f, -1.0f),
        new(3.0f, 5.0f), new(-1.0f, -5.0f), new(-5.0f, 1.0f), new(5.0f, -3.0f),
        new(-3.0f, -5.0f), new(1.0f, 5.0f), new(5.0f, -1.0f), new(-5.0f, 3.0f),
        new(-1.0f, 1.0f), new(3.0f, -1.0f), new(-1.0f, -3.0f), new(1.0f, 1.0f),
        new(1.0f, -1.0f), new(-3.0f, 1.0f), new(1.0f, 3.0f), new(-1.0f, -1.0f),
        new(-5.0f, 5.0f), new(7.0f, -5.0f), new(-5.0f, -7.0f), new(5.0f, 5.0f),
        new(5.0f, -5.0f), new(-7.0f, 5.0f), new(5.0f, 7.0f), new(-5.0f, -5.0f),
        new(-5.0f, -3.0f), new(7.0f, 3.0f), new(3.0f, -7.0f), new(-3.0f, 5.0f),
        new(5.0f, 3.0f), new(-7.0f, -3.0f), new(-3.0f, 7.0f), new(3.0f, -5.0f)
    };

    private static readonly V17RxComplex[] Constellation9600 =
    {
        new(-8.0f, 2.0f), new(-6.0f, -4.0f), new(-4.0f, 6.0f), new(2.0f, 8.0f),
        new(8.0f, -2.0f), new(6.0f, 4.0f), new(4.0f, -6.0f), new(-2.0f, -8.0f),
        new(0f, 2.0f), new(-6.0f, 4.0f), new(4.0f, 6.0f), new(2.0f, 0f),
        new(0f, -2.0f), new(6.0f, -4.0f), new(-4.0f, -6.0f), new(-2.0f, 0f),
        new(0f, -6.0f), new(2.0f, -4.0f), new(-4.0f, -2.0f), new(-6.0f, 0f),
        new(0f, 6.0f), new(-2.0f, 4.0f), new(4.0f, 2.0f), new(6.0f, 0f),
        new(8.0f, 2.0f), new(2.0f, 4.0f), new(4.0f, -2.0f), new(2.0f, -8.0f),
        new(-8.0f, -2.0f), new(-2.0f, -4.0f), new(-4.0f, 2.0f), new(-2.0f, 8.0f)
    };

    private static readonly V17RxComplex[] Constellation7200 =
    {
        new(6.0f, -6.0f), new(-2.0f, 6.0f), new(6.0f, 2.0f), new(-6.0f, -6.0f),
        new(-6.0f, 6.0f), new(2.0f, -6.0f), new(-6.0f, -2.0f), new(6.0f, 6.0f),
        new(-2.0f, 2.0f), new(6.0f, -2.0f), new(-2.0f, -6.0f), new(2.0f, 2.0f),
        new(2.0f, -2.0f), new(-6.0f, 2.0f), new(2.0f, 6.0f), new(-2.0f, -2.0f)
    };

    private static readonly V17RxComplex[] Constellation4800 =
    {
        new(-6.0f, -2.0f), new(-2.0f, 6.0f), new(2.0f, -6.0f), new(6.0f, 2.0f)
    };

    private static readonly float[] ReceivePulseShaperReal =
    {
        -0.0020619019f, 0.0003585524f, -0.003320551f, 0f, -0.0024369456f, -0.0015483291f, -0.0039043547f, -0.0072869117f,
        -0.0031365194f, -0.0318625955f, 0.0179723866f, -0.0443341749f, 0.0312604836f, 0.3230605459f, 0.0505378037f, -0.190050745f,
        -0.0211630895f, -0.0320270274f, -0.0278489297f, 0.002607244f, -0.011046017f, 0.0013541f, -0.0049488107f, 0f,
        -0.0036972877f, -0.0010388973f, -0.0010338347f, -0.0020683703f, 0.0003525572f, -0.0033401244f, 0f, -0.002471696f,
        -0.0015426378f, -0.0039641289f, -0.007281806f, -0.0032521647f, -0.0319837525f, 0.0178808458f, -0.0449835119f, 0.0313889726f,
        0.3234303876f, 0.0504793866f, -0.1892688068f, -0.0208540459f, -0.0321881097f, -0.0277423732f, 0.0025114777f, -0.0110530881f,
        0.0013333686f, -0.004966622f, 0f, -0.0037159694f, -0.0010327347f, -0.0010510197f, -0.0020747355f, 0.0003465272f,
        -0.003359573f, 0f, -0.0025063443f, -0.0015368277f, -0.0040238977f, -0.0072762576f, -0.0033682448f, -0.0321041445f,
        0.0177881557f, -0.0456347666f, 0.031517331f, 0.323796634f, 0.0504204099f, -0.188486107f, -0.0205459267f, -0.0323471508f,
        -0.0276351624f, 0.0024160784f, -0.0110594931f, 0.0013126376f, -0.0049840538f, 0f, -0.0037344365f, -0.0010265339f,
        -0.0010681027f, -0.0020809969f, 0.0003404626f, -0.0033788955f, 0f, -0.0025408883f, -0.0015308989f, -0.0040836578f,
        -0.0072702649f, -0.0034847564f, -0.0322237633f, 0.0176943144f, -0.0462879296f, 0.0316455558f, 0.3241592766f, 0.0503608748f,
        -0.1877026622f, -0.0202387362f, -0.0325041544f, -0.0275273046f, 0.002321049f, -0.0110652345f, 0.0012919082f, -0.0050011063f,
        0f, -0.0037526884f, -0.0010202953f, -0.0010850831f, -0.0020871539f, 0.0003343637f, -0.0033980907f, 0f,
        -0.0025753258f, -0.0015248514f, -0.0041434056f, -0.0072638265f, -0.0036016961f, -0.0323426007f, 0.01759932f, -0.0469429914f,
        0.0317736445f, 0.3245183067f, 0.0503007828f, -0.1869184891f, -0.0199324788f, -0.032659124f, -0.0274188069f, 0.002226392f,
        -0.0110703147f, 0.0012711817f, -0.0050177794f, 0f, -0.0037707246f, -0.0010140193f, -0.0011019602f, -0.0020932058f,
        0.0003282306f, -0.0034171573f, 0f, -0.0026096547f, -0.0015186851f, -0.0042031378f, -0.0072569406f, -0.0037190604f,
        -0.0324606484f, 0.0175031707f, -0.0475999425f, 0.0319015942f, 0.324873716f, 0.0502401353f, -0.1861336042f, -0.019627159f,
        -0.0328120633f, -0.0273096765f, 0.0021321101f, -0.0110747363f, 0.0012504592f, -0.0050340735f, 0f, -0.0037885445f,
        -0.0010077062f, -0.0011187334f, -0.0020991522f, 0.0003220637f, -0.0034360941f, 0f, -0.0026438726f, -0.0015124002f,
        -0.0042628509f, -0.0072496059f, -0.0038368459f, -0.0325778982f, 0.0174058646f, -0.0482587731f, 0.0320294023f, 0.3252254961f,
        0.0501789338f, -0.1853480241f, -0.019322781f, -0.0329629759f, -0.0271999206f, 0.0020382061f, -0.0110785018f, 0.0012297418f,
        -0.0050499884f, 0f, -0.0038061474f, -0.0010013565f, -0.0011354018f, -0.0021049923f, 0.0003158633f, -0.0034548998f,
        0f, -0.0026779775f, -0.0015059966f, -0.0043225415f, -0.0072418207f, -0.0039550491f, -0.0326943419f, 0.0173074f,
        -0.0489194735f, 0.0321570659f, 0.3255736387f, 0.0501171796f, -0.1845617654f, -0.019019349f, -0.0331118657f, -0.0270895463f,
        0.0019446825f, -0.0110816139f, 0.0012090307f, -0.0050655245f, 0f, -0.0038235328f, -0.0009949706f, -0.001151965f,
        -0.0021107257f, 0.0003096295f, -0.0034735731f, 0f, -0.002711967f, -0.0014994743f, -0.0043822061f, -0.0072335836f,
        -0.0040736663f, -0.0328099713f, 0.0172077751f, -0.049582034f, 0.0322845825f, 0.3259181356f, 0.0500548741f, -0.1837748448f,
        -0.0187168674f, -0.0332587364f, -0.0269785608f, 0.001851542f, -0.0110840749f, 0.001188327f, -0.0050806818f, 0f,
        -0.0038407002f, -0.0009885489f, -0.0011684221f, -0.0021163518f, 0.0003033626f, -0.0034921128f, 0f, -0.002745839f,
        -0.0014928334f, -0.0044418413f, -0.0072248931f, -0.0041926941f, -0.0329247781f, 0.0171069881f, -0.0502464448f, 0.0324119492f,
        0.3262589788f, 0.049992019f, -0.1829872788f, -0.0184153403f, -0.0334035918f, -0.0268669712f, 0.0017587871f, -0.0110858877f,
        0.0011676319f, -0.0050954606f, 0f, -0.003857649f, -0.0009820917f, -0.0011847726f, -0.0021218699f, 0.000297063f,
        -0.0035105177f, 0f, -0.0027795914f, -0.001486074f, -0.0045014435f, -0.0072157476f, -0.0043121289f, -0.0330387541f,
        0.0170050373f, -0.050912696f, 0.0325391634f, 0.3265961602f, 0.0499286156f, -0.1821990841f, -0.018114772f, -0.0335464359f,
        -0.0267547848f, 0.0016664204f, -0.0110870548f, 0.0011469465f, -0.0051098611f, 0f, -0.0038743788f, -0.0009755996f,
        -0.0012010158f, -0.0021272795f, 0.0002907308f, -0.0035287866f, 0f, -0.0028132218f, -0.001479196f, -0.0045610093f,
        -0.0072061457f, -0.004431967f, -0.0331518911f, 0.0169019212f, -0.0515807775f, 0.0326662223f, 0.3269296719f, 0.0498646654f,
        -0.1814102772f, -0.0178151665f, -0.0336872725f, -0.0266420086f, 0.0015744444f, -0.0110875788f, 0.001126272f, -0.0051238834f,
        0f, -0.0038908889f, -0.0009690729f, -0.0012171511f, -0.0021325801f, 0.0002843663f, -0.0035469181f, 0f,
        -0.0028467281f, -0.0014721996f, -0.0046205352f, -0.007196086f, -0.0045522048f, -0.0332641808f, 0.0167976379f, -0.0522506793f,
        0.0327931231f, 0.327259506f, 0.0498001699f, -0.1806208747f, -0.0175165281f, -0.0338261058f, -0.0265286498f, 0.0014828615f,
        -0.0110874625f, 0.0011056094f, -0.0051375279f, 0f, -0.003907179f, -0.0009625121f, -0.0012331778f, -0.0021377711f,
        0.0002779699f, -0.0035649111f, 0f, -0.0028801082f, -0.0014650847f, -0.0046800177f, -0.0071855671f, -0.0046728385f,
        -0.033375615f, 0.0166921859f, -0.0529223913f, 0.0329198633f, 0.3275856548f, 0.0497351307f, -0.1798308932f, -0.0172188607f,
        -0.0339629396f, -0.0264147156f, 0.0013916744f, -0.0110867085f, 0.00108496f, -0.0051507947f, 0f, -0.0039232485f,
        -0.0009559175f, -0.0012490953f, -0.0021428519f, 0.0002715418f, -0.0035827643f, 0f, -0.0029133597f, -0.0014578516f,
        -0.0047394532f, -0.0071745874f, -0.0047938646f, -0.0334861854f, 0.0165855637f, -0.0535959035f, 0.0330464399f, 0.3279081106f,
        0.0496695492f, -0.1790403493f, -0.0169221685f, -0.0340977781f, -0.0263002131f, 0.0013008854f, -0.0110853195f, 0.0010643249f,
        -0.0051636843f, 0f, -0.0039390969f, -0.0009492895f, -0.001264903f, -0.0021478221f, 0.0002650822f, -0.0036004766f,
        0f, -0.0029464806f, -0.0014505002f, -0.0047988382f, -0.0071631457f, -0.0049152791f, -0.0335958839f, 0.0164777695f,
        -0.0542712054f, 0.0331728504f, 0.3282268658f, 0.049603427f, -0.1782492596f, -0.0166264554f, -0.0342306254f, -0.0261851494f,
        0.0012104969f, -0.0110832983f, 0.001043705f, -0.0051761968f, 0f, -0.0039547239f, -0.0009426286f, -0.0012806003f,
        -0.0021526809f, 0.0002585915f, -0.0036180467f, 0f, -0.0029794686f, -0.0014430306f, -0.0048581693f, -0.0071512406f,
        -0.0050370783f, -0.0337047022f, 0.016368802f, -0.0549482868f, 0.0332990919f, 0.3285419129f, 0.0495367656f, -0.1774576406f,
        -0.0163317254f, -0.0343614857f, -0.0260695316f, 0.0011205114f, -0.0110806476f, 0.0010231017f, -0.0051883327f, 0f,
        -0.0039701289f, -0.0009359353f, -0.0012961865f, -0.002157428f, 0.00025207f, -0.0036354735f, 0f, -0.0030123215f,
        -0.001435443f, -0.0049174428f, -0.0071388706f, -0.0051592585f, -0.0338126321f, 0.0162586596f, -0.0556271375f, 0.0334251619f,
        0.3288532445f, 0.0494695666f, -0.176665509f, -0.0160379826f, -0.0344903632f, -0.025953367f, 0.0010309312f, -0.0110773701f,
        0.0010025159f, -0.0052000923f, 0f, -0.0039853116f, -0.0009292098f, -0.0013116611f, -0.0021620628f, 0.0002455178f,
        -0.0036527556f, 0f, -0.0030450371f, -0.0014277374f, -0.0049766552f, -0.0071260345f, -0.0052818157f, -0.0339196653f,
        0.0161473408f, -0.0563077468f, 0.0335510574f, 0.3291608532f, 0.0494018316f, -0.1758728812f, -0.0157452307f, -0.0346172622f,
        -0.0258366625f, 0.0009417586f, -0.0110734688f, 0.0009819489f, -0.0052114758f, 0f, -0.0040002714f, -0.0009224527f,
        -0.0013270235f, -0.0021665848f, 0.0002389353f, -0.003669892f, 0f, -0.0030776134f, -0.0014199139f, -0.0050358029f,
        -0.0071127308f, -0.0054047462f, -0.0340257937f, 0.0160348443f, -0.0569901045f, 0.033676776f, 0.3294647318f, 0.0493335621f,
        -0.1750797738f, -0.0154534737f, -0.0347421869f, -0.0257194253f, 0.000852996f, -0.0110689464f, 0.0009614016f, -0.0052224838f,
        0f, -0.0040150081f, -0.0009156643f, -0.0013422731f, -0.0021709934f, 0.0002323229f, -0.0036868814f, 0f,
        -0.003110048f, -0.0014119726f, -0.0050948823f, -0.0070989584f, -0.0055280459f, -0.0341310089f, 0.0159211685f, -0.0576741998f,
        0.0338023147f, 0.3297648731f, 0.0492647598f, -0.1742862034f, -0.0151627155f, -0.0348651418f, -0.0256016624f, 0.0007646457f,
        -0.0110638057f, 0.0009408752f, -0.0052331166f, 0f, -0.0040295213f, -0.0009088451f, -0.0013574093f, -0.0021752881f,
        0.0002256807f, -0.0037037227f, 0f, -0.0031423388f, -0.0014039137f, -0.00515389f, -0.0070847158f, -0.005651711f,
        -0.0342353029f, 0.0158063121f, -0.0583600222f, 0.033927671f, 0.3300612701f, 0.0491954263f, -0.1734921865f, -0.0148729598f,
        -0.0349861313f, -0.0254833811f, 0.0006767098f, -0.0110580495f, 0.0009203708f, -0.0052433747f, 0f, -0.0040438104f,
        -0.0009019955f, -0.0013724316f, -0.0021794684f, 0.0002190091f, -0.0037204147f, 0f, -0.0031744836f, -0.0013957373f,
        -0.0052128222f, -0.0070700019f, -0.0057757375f, -0.0343386673f, 0.0156902738f, -0.059047561f, 0.0340528421f, 0.3303539157f,
        0.0491255631f, -0.1726977397f, -0.0145842104f, -0.0351051598f, -0.0253645884f, 0.0005891908f, -0.0110516808f, 0.0008998894f,
        -0.0052532584f, 0f, -0.0040578753f, -0.0008951159f, -0.0013873393f, -0.0021835338f, 0.0002123083f, -0.0037369562f,
        0f, -0.0032064803f, -0.0013874435f, -0.0052716754f, -0.0070548153f, -0.0059001214f, -0.034441094f, 0.0155730522f,
        -0.0597368054f, 0.0341778252f, 0.3306428031f, 0.0490551719f, -0.1719028794f, -0.0142964712f, -0.0352222317f, -0.0252452913f,
        0.0005020907f, -0.0110447025f, 0.0008794322f, -0.0052627683f, 0f, -0.0040717154f, -0.0008882067f, -0.001402132f,
        -0.0021874839f, 0.0002055787f, -0.003753346f, 0f, -0.0032383267f, -0.0013790324f, -0.005330446f, -0.0070391548f,
        -0.0060248587f, -0.0345425747f, 0.015454646f, -0.0604277447f, 0.0343026178f, 0.3309279254f, 0.0489842543f, -0.1711076223f,
        -0.0140097457f, -0.0353373517f, -0.0251254969f, 0.0004154118f, -0.0110371173f, 0.0008590002f, -0.0052719047f, 0f,
        -0.0040853306f, -0.0008812684f, -0.0014168091f, -0.0021913181f, 0.0001988206f, -0.003769583f, 0f, -0.0032700205f,
        -0.0013705042f, -0.0053891304f, -0.0070230192f, -0.0061499454f, -0.0346431013f, 0.015335054f, -0.061120368f, 0.034427217f,
        0.331209276f, 0.0489128121f, -0.1703119847f, -0.0137240378f, -0.0354505244f, -0.0250052123f, 0.0003291562f, -0.0110289284f,
        0.0008385945f, -0.0052806684f, 0f, -0.0040987205f, -0.0008743014f, -0.00143137f, -0.0021950359f, 0.0001920343f,
        -0.003785666f, 0f, -0.0033015597f, -0.0013618591f, -0.0054477249f, -0.0070064072f, -0.0062753773f, -0.0347426655f,
        0.015214275f, -0.0618146643f, 0.0345516202f, 0.3314868482f, 0.0488408469f, -0.1695159831f, -0.0134393509f, -0.0355617543f,
        -0.0248844446f, 0.000243326f, -0.0110201385f, 0.0008182162f, -0.0052890596f, 0f, -0.0041118846f, -0.000867306f,
        -0.0014458142f, -0.0021986368f, 0.00018522f, -0.0038015939f, 0f, -0.0033329421f, -0.0013530971f, -0.0055062259f,
        -0.0069893178f, -0.0064011503f, -0.0348412592f, 0.0150923077f, -0.0625106227f, 0.0346758247f, 0.3317606353f, 0.0487683603f,
        -0.1687196342f, -0.0131556888f, -0.0356710461f, -0.0247632008f, 0.0001579235f, -0.0110107508f, 0.0007978663f, -0.0052970791f,
        0f, -0.0041248229f, -0.0008602829f, -0.0014601413f, -0.0022021204f, 0.0001783781f, -0.0038173656f, 0f,
        -0.0033641655f, -0.0013442185f, -0.0055646298f, -0.0069717496f, -0.0065272603f, -0.0349388742f, 0.0149691509f, -0.063208232f,
        0.0347998277f, 0.3320306311f, 0.048695354f, -0.1679229542f, -0.0128730551f, -0.0357784046f, -0.0246414879f, 7.29505e-05f,
        -0.011000768f, 0.0007775458f, -0.0053047272f, 0f, -0.0041375349f, -0.0008532322f, -0.0014743506f, -0.0022054862f,
        0.0001715089f, -0.0038329799f, 0f, -0.0033952278f, -0.0013352235f, -0.005622933f, -0.0069537016f, -0.0066537032f,
        -0.0350355022f, 0.0148448037f, -0.0639074813f, 0.0349236266f, 0.3322968291f, 0.0486218298f, -0.1671259598f, -0.0125914532f,
        -0.0358838345f, -0.0245193131f, -1.15907e-05f, -0.0109901934f, 0.0007572559f, -0.0053120047f, 0f, -0.0041500204f,
        -0.0008461546f, -0.0014884417f, -0.0022087338f, 0.0001646126f, -0.0038484356f, 0f, -0.0034261268f, -0.0013261121f,
        -0.0056811317f, -0.0069351726f, -0.0067804747f, -0.0351311351f, 0.0147192647f, -0.0646083592f, 0.0350472187f, 0.332559223f,
        0.0485477893f, -0.1663286673f, -0.0123108867f, -0.0359873407f, -0.0243966832f, -9.56983e-05f, -0.0109790297f, 0.0007369976f,
        -0.0053189121f, 0f, -0.0041622791f, -0.0008390504f, -0.0015024141f, -0.0022118626f, 0.0001576897f, -0.0038637317f,
        0f, -0.0034568604f, -0.0013168846f, -0.0057392224f, -0.0069161616f, -0.0069075706f, -0.0352257647f, 0.014592533f,
        -0.0653108547f, 0.0351706013f, 0.3328178066f, 0.0484732343f, -0.1655310932f, -0.0120313591f, -0.0360889279f, -0.0242736053f,
        -0.0001793701f, -0.0109672802f, 0.0007167719f, -0.0053254499f, 0f, -0.0041743108f, -0.00083192f, -0.0015162673f,
        -0.0022148722f, 0.0001507404f, -0.0038788671f, 0f, -0.0034874263f, -0.0013075412f, -0.0057972013f, -0.0068966673f,
        -0.0070349866f, -0.0353193829f, 0.0144646076f, -0.0660149562f, 0.0352937717f, 0.3330725738f, 0.0483981666f, -0.164733254f,
        -0.0117528738f, -0.036188601f, -0.0241500864f, -0.0002626042f, -0.0109549479f, 0.0006965798f, -0.0053316189f, 0f,
        -0.0041861153f, -0.0008247639f, -0.0015300007f, -0.0022177622f, 0.000143765f, -0.0038938405f, 0f, -0.0035178226f,
        -0.0012980821f, -0.0058550648f, -0.0068766887f, -0.0071627185f, -0.0354119815f, 0.0143354873f, -0.0667206525f, 0.0354167272f,
        0.3333235186f, 0.0483225878f, -0.1639351659f, -0.0114754342f, -0.0362863651f, -0.0240261336f, -0.0003453986f, -0.0109420357f,
        0.0006764225f, -0.0053374197f, 0f, -0.0041976923f, -0.0008175825f, -0.001543614f, -0.0022205322f, 0.0001367639f,
        -0.0039086511f, 0f, -0.003548047f, -0.0012885075f, -0.0059128092f, -0.0068562248f, -0.0072907619f, -0.0355035523f,
        0.0142051712f, -0.0674279323f, 0.0355394652f, 0.3335706351f, 0.0482464997f, -0.1631368455f, -0.0111990437f, -0.0363822251f,
        -0.0239017537f, -0.0004277515f, -0.0109285469f, 0.0006563009f, -0.005342853f, 0f, -0.0042090416f, -0.0008103762f,
        -0.0015571066f, -0.0022231816f, 0.0001297374f, -0.0039232975f, 0f, -0.0035780974f, -0.0012788176f, -0.0059704308f,
        -0.0068352745f, -0.0074191125f, -0.0355940871f, 0.0140736584f, -0.0681367839f, 0.0356619829f, 0.3338139174f, 0.0481699042f,
        -0.162338309f, -0.0109237056f, -0.036476186f, -0.0237769539f, -0.0005096609f, -0.0109144844f, 0.000636216f, -0.0053479194f,
        0f, -0.004220163f, -0.0008031455f, -0.001570478f, -0.0022257101f, 0.0001226857f, -0.0039377788f, 0f,
        -0.0036079717f, -0.0012690125f, -0.0060279259f, -0.0068138368f, -0.0075477659f, -0.0356835779f, 0.013940948f, -0.0688471959f,
        0.0357842777f, 0.3340533598f, 0.0480928029f, -0.1615395729f, -0.0106494233f, -0.0365682528f, -0.0236517409f, -0.000591125f,
        -0.0108998515f, 0.0006161688f, -0.0053526196f, 0f, -0.0042310564f, -0.0007958908f, -0.0015837279f, -0.0022281172f,
        0.0001156093f, -0.0039520938f, 0f, -0.0036376678f, -0.0012590927f, -0.0060852909f, -0.0067919106f, -0.0076767177f,
        -0.0357720165f, 0.013807039f, -0.0695591566f, 0.0359063468f, 0.3342889566f, 0.0480151977f, -0.1607406536f, -0.0103762f,
        -0.0366584307f, -0.0235261219f, -0.0006721419f, -0.0108846512f, 0.0005961605f, -0.0053569544f, 0f, -0.0042417215f,
        -0.0007886125f, -0.0015968558f, -0.0022304025f, 0.0001085084f, -0.0039662415f, 0f, -0.0036671835f, -0.0012490582f,
        -0.0061425221f, -0.0067694951f, -0.0078059635f, -0.0358593948f, 0.0136719306f, -0.0702726545f, 0.0360281877f, 0.3345207023f,
        0.0479370904f, -0.1599415673f, -0.0101040389f, -0.0367467248f, -0.0234001037f, -0.0007527098f, -0.0108688867f, 0.0005761919f,
        -0.0053609244f, 0f, -0.0042521582f, -0.0007813111f, -0.0016098612f, -0.0022325656f, 0.0001013834f, -0.0039802209f,
        0f, -0.0036965167f, -0.0012389093f, -0.0061996156f, -0.0067465891f, -0.0079354987f, -0.0359457046f, 0.013535622f,
        -0.0709876777f, 0.0361497976f, 0.3347485914f, 0.0478584828f, -0.1591423304f, -0.0098329433f, -0.0368331404f, -0.0232736933f,
        -0.0008328269f, -0.0108525611f, 0.000556264f, -0.0053645305f, 0f, -0.0042623664f, -0.0007739869f, -0.0016227437f,
        -0.0022346062f, 9.42346e-05f, -0.0039940308f, 0f, -0.0037256654f, -0.0012286462f, -0.0062565678f, -0.0067231918f,
        -0.008065319f, -0.0360309379f, 0.0133981124f, -0.0717042144f, 0.036271174f, 0.3349726184f, 0.0477793767f, -0.1583429593f,
        -0.0095629163f, -0.0369176826f, -0.0231468977f, -0.0009124914f, -0.0108356777f, 0.000536378f, -0.0053677735f, 0f,
        -0.0042723459f, -0.0007666405f, -0.0016355029f, -0.0022365237f, 8.70624e-05f, -0.0040076702f, 0f, -0.0037546273f,
        -0.0012182693f, -0.0063133751f, -0.0066993023f, -0.0081954197f, -0.0361150865f, 0.013259401f, -0.0724222529f, 0.036392314f,
        0.3351927782f, 0.0476997739f, -0.15754347f, -0.0092939611f, -0.0370003566f, -0.0230197236f, -0.0009917016f, -0.0108182396f,
        0.0005165347f, -0.005370654f, 0f, -0.0042820965f, -0.0007592722f, -0.0016481383f, -0.0022383178f, 7.9867e-05f,
        -0.0040211381f, 0f, -0.0037834005f, -0.0012077787f, -0.0063700335f, -0.0066749196f, -0.0083257964f, -0.0361981423f,
        0.0131194871f, -0.0731417813f, 0.0365132151f, 0.3354090654f, 0.0476196764f, -0.1567438791f, -0.0090260808f, -0.037081168f,
        -0.022892178f, -0.0010704558f, -0.0108002501f, 0.0004967351f, -0.005373173f, 0f, -0.0042916183f, -0.0007518825f,
        -0.0016606496f, -0.0022399882f, 7.26488e-05f, -0.0040344333f, 0f, -0.0038119828f, -0.0011971748f, -0.0064265395f,
        -0.0066500428f, -0.0084564445f, -0.0362800972f, 0.01297837f, -0.0738627876f, 0.0366338746f, 0.335621475f, 0.047539086f,
        -0.1559442027f, -0.0087592783f, -0.0371601218f, -0.0227642679f, -0.0011487523f, -0.0107817124f, 0.0004769803f, -0.0053753312f,
        0f, -0.0043009111f, -0.0007444718f, -0.0016730363f, -0.0022415343f, 6.54082e-05f, -0.004047555f, 0f,
        -0.0038403722f, -0.0011864578f, -0.0064828893f, -0.0066246711f, -0.0085873593f, -0.0363609433f, 0.012836049f, -0.0745852597f,
        0.0367542899f, 0.335830002f, 0.0474580044f, -0.1551444571f, -0.0084935568f, -0.0372372237f, -0.022636f, -0.0012265895f,
        -0.0107626296f, 0.0004572712f, -0.0053771295f, 0f, -0.0043099748f, -0.0007370406f, -0.0016852981f, -0.0022429559f,
        5.81455e-05f, -0.004060502f, 0f, -0.0038685664f, -0.001175628f, -0.0065390792f, -0.0065988036f, -0.0087185362f,
        -0.0364406723f, 0.0126925236f, -0.0753091857f, 0.0368744582f, 0.3360346414f, 0.0473764337f, -0.1543446584f, -0.0082289193f,
        -0.0373124789f, -0.0225073813f, -0.0013039656f, -0.0107430052f, 0.0004376087f, -0.0053785688f, 0f, -0.0043188094f,
        -0.0007295893f, -0.0016974345f, -0.0022442526f, 5.0861e-05f, -0.0040732733f, 0f, -0.0038965636f, -0.0011646856f,
        -0.0065951053f, -0.0065724395f, -0.0088499705f, -0.0365192763f, 0.012547793f, -0.0760345533f, 0.036994377f, 0.3362353883f,
        0.0472943758f, -0.1535448229f, -0.0079653686f, -0.037385893f, -0.0223784187f, -0.0013808792f, -0.0107228422f, 0.0004179938f,
        -0.0053796499f, 0f, -0.0043274147f, -0.0007221183f, -0.0017094452f, -0.002245424f, 4.3555e-05f, -0.004085868f,
        0f, -0.0039243615f, -0.0011536311f, -0.006650964f, -0.006545578f, -0.0089816577f, -0.0365967472f, 0.0124018568f,
        -0.0767613505f, 0.0371140437f, 0.3364322381f, 0.0472118324f, -0.1527449667f, -0.0077029078f, -0.0374574715f, -0.0222491188f,
        -0.0014573285f, -0.0107021441f, 0.0003984276f, -0.0053803737f, 0f, -0.0043357908f, -0.000714628f, -0.0017213299f,
        -0.0022464698f, 3.6228e-05f, -0.004098285f, 0f, -0.0039519582f, -0.0011424646f, -0.0067066515f, -0.0065182182f,
        -0.0091135929f, -0.036673077f, 0.0122547144f, -0.0774895649f, 0.0372334555f, 0.336625186f, 0.0471288056f, -0.1519451061f,
        -0.0074415397f, -0.0375272199f, -0.0221194886f, -0.0015333121f, -0.0106809141f, 0.0003789108f, -0.0053807412f, 0f,
        -0.0043439376f, -0.0007071189f, -0.001733088f, -0.0022473896f, 2.88803e-05f, -0.0041105233f, 0f, -0.0039793516f,
        -0.0011311864f, -0.006762164f, -0.0064903595f, -0.0092457713f, -0.0367482576f, 0.0121063653f, -0.0782191843f, 0.0373526098f,
        0.3368142276f, 0.0470452972f, -0.1511452571f, -0.0071812672f, -0.0375951438f, -0.0219895349f, -0.0016088285f, -0.0106591556f,
        0.0003594446f, -0.0053807532f, 0f, -0.0043518551f, -0.0006995915f, -0.0017447194f, -0.002248183f, 2.15121e-05f,
        -0.004122582f, 0f, -0.0040065395f, -0.001119797f, -0.0068174978f, -0.006462001f, -0.0093781883f, -0.0368222811f,
        0.011956809f, -0.0789501962f, 0.037471504f, 0.3369993582f, 0.0469613092f, -0.150345436f, -0.0069220931f, -0.0376612488f,
        -0.0218592646f, -0.001683876f, -0.0106368718f, 0.0003400297f, -0.0053804107f, 0f, -0.0043595433f, -0.0006920461f,
        -0.0017562235f, -0.0022488498f, 1.41239e-05f, -0.00413446f, 0f, -0.00403352f, -0.0011082966f, -0.0068726491f,
        -0.006433142f, -0.009510839f, -0.0368951394f, 0.0118060451f, -0.0796825884f, 0.0375901356f, 0.3371805737f, 0.0468768435f,
        -0.1495456586f, -0.0066640202f, -0.0377255407f, -0.0217286843f, -0.0017584533f, -0.0106140661f, 0.0003206673f, -0.0053797147f,
        0f, -0.0043670021f, -0.0006844832f, -0.0017676002f, -0.0022493895f, 6.7161e-06f, -0.0041461564f, 0f,
        -0.0040602911f, -0.0010966855f, -0.0069276141f, -0.0064037818f, -0.0096437186f, -0.0369668245f, 0.0116540732f, -0.0804163483f,
        0.0377085017f, 0.3373578695f, 0.0467919021f, -0.1487459413f, -0.0064070512f, -0.0377880251f, -0.0215978008f, -0.0018325589f,
        -0.0105907419f, 0.000301358f, -0.0053786662f, 0f, -0.0043742317f, -0.0006769033f, -0.001778849f, -0.002249802f,
        -7.111e-07f, -0.0041576702f, 0f, -0.0040868506f, -0.0010849642f, -0.0069823891f, -0.0063739198f, -0.0097768222f,
        -0.0370373284f, 0.011500893f, -0.0811514635f, 0.0378265999f, 0.3375312416f, 0.046706487f, -0.1479463f, -0.0061511889f,
        -0.0378487077f, -0.021466621f, -0.0019061913f, -0.0105669025f, 0.000282103f, -0.0053772661f, 0f, -0.004381232f,
        -0.0006693067f, -0.0017899697f, -0.0022500867f, -8.1572e-06f, -0.0041690005f, 0f, -0.0041131966f, -0.0010731328f,
        -0.0070369703f, -0.0063435552f, -0.009910145f, -0.0371066433f, 0.011346504f, -0.0818879212f, 0.0379444276f, 0.3377006859f,
        0.0466206001f, -0.1471467507f, -0.0058964359f, -0.0379075943f, -0.0213351515f, -0.0019793492f, -0.0105425512f, 0.0002629032f,
        -0.0053755154f, 0f, -0.0043880031f, -0.0006616939f, -0.0018009619f, -0.0022502435f, -1.5622e-05f, -0.0041801464f,
        0f, -0.0041393271f, -0.0010611918f, -0.0070913538f, -0.0063126874f, -0.010043682f, -0.037174761f, 0.0111909059f,
        -0.082625709f, 0.038061982f, 0.3378661983f, 0.0465342434f, -0.1463473095f, -0.0056427949f, -0.0379646908f, -0.0212033992f,
        -0.0020520312f, -0.0105176916f, 0.0002437593f, -0.0053734153f, 0f, -0.004394545f, -0.0006540653f, -0.0018118253f,
        -0.002250272f, -2.3105e-05f, -0.0041911068f, 0f, -0.0041652399f, -0.0010491416f, -0.007145536f, -0.0062813158f,
        -0.0101774283f, -0.0372416738f, 0.0110340986f, -0.0833648141f, 0.0381792607f, 0.338027775f, 0.046447419f, -0.1455479923f,
        -0.0053902684f, -0.038020003f, -0.0210713707f, -0.0021242359f, -0.010492327f, 0.0002246724f, -0.0053709667f, 0f,
        -0.0044008579f, -0.0006464215f, -0.0018225596f, -0.002250172f, -3.0606e-05f, -0.0042018809f, 0f, -0.0041909332f,
        -0.0010369824f, -0.007199513f, -0.0062494397f, -0.0103113789f, -0.0373073736f, 0.0108760818f, -0.0841052238f, 0.0382962609f,
        0.338185412f, 0.0463601287f, -0.1447488151f, -0.0051388592f, -0.0380735368f, -0.0209390727f, -0.0021959621f, -0.0104664607f,
        0.0002056433f, -0.0053681706f, 0f, -0.0044069417f, -0.0006387627f, -0.0018331645f, -0.0022499431f, -3.81244e-05f,
        -0.0042124677f, 0f, -0.0042164049f, -0.0010247147f, -0.0072532811f, -0.0062170586f, -0.0104455288f, -0.0373718526f,
        0.0107168552f, -0.0848469253f, 0.0384129802f, 0.3383391057f, 0.0462723747f, -0.1439497939f, -0.0048885696f, -0.0381252981f,
        -0.0208065119f, -0.0022672083f, -0.0104400964f, 0.0001866729f, -0.0053650283f, 0f, -0.0044127966f, -0.0006310895f,
        -0.0018436397f, -0.002249585f, -4.566e-05f, -0.0042228663f, 0f, -0.004241653f, -0.0010123389f, -0.0073068364f,
        -0.0061841719f, -0.0105798731f, -0.0374351029f, 0.0105564187f, -0.0855899057f, 0.0385294158f, 0.3384888524f, 0.0461841589f,
        -0.1431509445f, -0.0046394022f, -0.0381752928f, -0.020673695f, -0.0023379734f, -0.0104132372f, 0.0001677622f, -0.0053615407f,
        0f, -0.0044184226f, -0.0006234022f, -0.001853985f, -0.0022490975f, -5.32124e-05f, -0.0042330758f, 0f,
        -0.0042666755f, -0.0009998553f, -0.0073601751f, -0.0061507791f, -0.0107144066f, -0.0374971165f, 0.0103947721f, -0.0863341521f,
        0.0386455653f, 0.3386346485f, 0.0460954835f, -0.1423522829f, -0.0043913596f, -0.0382235271f, -0.0205406287f, -0.0024082561f,
        -0.0103858869f, 0.0001489119f, -0.0053577089f, 0f, -0.00442382f, -0.0006157014f, -0.0018642f, -0.0022484803f,
        -6.07812e-05f, -0.0042430953f, 0f, -0.0042914705f, -0.0009872642f, -0.0074132935f, -0.0061168797f, -0.0108491242f,
        -0.0375578855f, 0.0102319154f, -0.0870796517f, 0.038761426f, 0.3387764907f, 0.0460063503f, -0.1415538249f, -0.004144444f,
        -0.0382700068f, -0.0204073196f, -0.0024780552f, -0.0103580487f, 0.000130123f, -0.0053535342f, 0f, -0.0044289888f,
        -0.0006079874f, -0.0018742846f, -0.0022477331f, -6.83661e-05f, -0.004252924f, 0f, -0.004316036f, -0.0009745663f,
        -0.0074661878f, -0.006082473f, -0.010984021f, -0.0376174023f, 0.0100678484f, -0.0878263913f, 0.0388769954f, 0.3389143756f,
        0.0459167616f, -0.1407555864f, -0.003898658f, -0.0383147381f, -0.0202737743f, -0.0025473695f, -0.0103297262f, 0.0001113964f,
        -0.0053490176f, 0f, -0.0044339291f, -0.0006002607f, -0.0018842384f, -0.0022468557f, -7.59667e-05f, -0.0042625608f,
        0f, -0.0043403699f, -0.0009617617f, -0.0075188541f, -0.0060475588f, -0.0111190917f, -0.0376756588f, 0.0099025711f,
        -0.088574358f, 0.0389922709f, 0.3390482998f, 0.0458267193f, -0.1399575831f, -0.0036540038f, -0.0383577271f, -0.0201399994f,
        -0.0026161978f, -0.0103009228f, 9.27328e-05f, -0.0053441603f, 0f, -0.0044386412f, -0.0005925217f, -0.0018940612f,
        -0.0022458478f, -8.35826e-05f, -0.0042720051f, 0f, -0.0043644705f, -0.000948851f, -0.0075712886f, -0.0060121364f,
        -0.0112543312f, -0.0377326472f, 0.0097360836f, -0.0893235386f, 0.0391072499f, 0.3391782602f, 0.0457362256f, -0.139159831f,
        -0.0034104838f, -0.0383989799f, -0.0200060016f, -0.0026845389f, -0.0102716421f, 7.41333e-05f, -0.0053389634f, 0f,
        -0.0044431251f, -0.0005847708f, -0.0019037527f, -0.0022447092f, -9.12135e-05f, -0.0042812558f, 0f, -0.0043883356f,
        -0.0009358345f, -0.0076234877f, -0.0059762055f, -0.0113897343f, -0.0377883599f, 0.0095683859f, -0.09007392f, 0.0392219298f,
        0.3393042538f, 0.0456452824f, -0.1383623455f, -0.0031681004f, -0.0384385027f, -0.0198717875f, -0.0027523919f, -0.0102418876f,
        5.55985e-05f, -0.0053334282f, 0f, -0.0044473811f, -0.0005770086f, -0.0019133128f, -0.0022434396f, -9.88589e-05f,
        -0.0042903122f, 0f, -0.0044119634f, -0.0009227127f, -0.0076754473f, -0.0059397656f, -0.0115252958f, -0.0378427888f,
        0.0093994779f, -0.0908254889f, 0.0393363082f, 0.3394262775f, 0.045553892f, -0.1375651427f, -0.0029268557f, -0.0384763017f,
        -0.0197373635f, -0.0028197555f, -0.0102116628f, 3.71294e-05f, -0.0053275559f, 0f, -0.0044514094f, -0.0005692354f,
        -0.0019227412f, -0.0022420388f, -0.0001065185f, -0.0042991735f, 0f, -0.0044353519f, -0.000909486f, -0.0077271638f,
        -0.0059028164f, -0.0116610105f, -0.0378959264f, 0.0092293599f, -0.0915782321f, 0.0394503824f, 0.3395443285f, 0.0454620564f,
        -0.1367682381f, -0.002686752f, -0.0385123832f, -0.0196027363f, -0.0028866287f, -0.0101809711f, 1.87267e-05f, -0.0053213476f,
        0f, -0.0044552101f, -0.0005614516f, -0.0019320376f, -0.0022405065f, -0.0001141919f, -0.0043078386f, 0f,
        -0.0044584992f, -0.0008961548f, -0.0077786333f, -0.0058653575f, -0.0117968732f, -0.0379477647f, 0.0090580319f, -0.0923321362f,
        0.0395641498f, 0.3396584039f, 0.0453697778f, -0.1359716474f, -0.0024477914f, -0.0385467534f, -0.0194679123f, -0.0029530104f,
        -0.0101498163f, 3.915e-07f, -0.0053148046f, 0f, -0.0044587834f, -0.0005536577f, -0.0019412019f, -0.0022388427f,
        -0.0001218787f, -0.0043163069f, 0f, -0.0044814034f, -0.0008827197f, -0.0078298521f, -0.0058273885f, -0.0119328784f,
        -0.0379982961f, 0.0088854941f, -0.093087188f, 0.0396776081f, 0.3397685011f, 0.0452770582f, -0.1351753863f, -0.0022099762f,
        -0.0385794186f, -0.0193328982f, -0.0030188998f, -0.0101182017f, -1.78757e-05f, -0.0053079282f, 0f, -0.0044621296f,
        -0.0005458541f, -0.0019502339f, -0.0022370469f, -0.0001295786f, -0.0043245776f, 0f, -0.0045040626f, -0.000869181f,
        -0.0078808162f, -0.0057889091f, -0.012069021f, -0.0380475129f, 0.0087117467f, -0.093843374f, 0.0397907545f, 0.3398746174f,
        0.0451838998f, -0.1343794704f, -0.0019733084f, -0.0386103852f, -0.0191977003f, -0.0030842957f, -0.010086131f, -3.60739e-05f,
        -0.0053007195f, 0f, -0.004465249f, -0.0005380413f, -0.0019591334f, -0.0022351191f, -0.0001372911f, -0.0043326497f,
        0f, -0.0045264749f, -0.0008555392f, -0.007931522f, -0.005749919f, -0.0122052955f, -0.0380954073f, 0.0085367899f,
        -0.0946006807f, 0.0399035866f, 0.3399767503f, 0.0450903046f, -0.1335839152f, -0.0017377902f, -0.0386396595f, -0.0190623253f,
        -0.0031491971f, -0.0100536077f, -5.42022e-05f, -0.00529318f, 0f, -0.0044681417f, -0.0005302197f, -0.0019679001f,
        -0.002233059f, -0.0001450159f, -0.0043405226f, 0f, -0.0045486383f, -0.0008417948f, -0.0079819655f, -0.0057104179f,
        -0.0123416967f, -0.0381419716f, 0.008360624f, -0.0953590946f, 0.0400161019f, 0.3400748975f, 0.044996275f, -0.1327887364f,
        -0.0015034235f, -0.038667248f, -0.0189267794f, -0.0032136033f, -0.0100206354f, -7.226e-05f, -0.0052853108f, 0f,
        -0.0044708081f, -0.0005223897f, -0.001976534f, -0.0022308665f, -0.0001527527f, -0.0043481953f, 0f, -0.0045705511f,
        -0.0008279483f, -0.008032143f, -0.0056704055f, -0.0124782191f, -0.0381871981f, 0.0081832493f, -0.0961186022f, 0.0401282978f,
        0.3401690565f, 0.044901813f, -0.1319939494f, -0.0012702104f, -0.038693157f, -0.0187910693f, -0.0032775132f, -0.0099872178f,
        -9.02464e-05f, -0.0052771133f, 0f, -0.0044732483f, -0.0005145518f, -0.0019850348f, -0.0022285413f, -0.0001605009f,
        -0.0043556672f, 0f, -0.0045922113f, -0.0008140001f, -0.0080820507f, -0.0056298817f, -0.0126148574f, -0.0382310793f,
        0.0080046662f, -0.0968791898f, 0.0402401719f, 0.3402592251f, 0.0448069208f, -0.1311995698f, -0.0010381528f, -0.0387173932f,
        -0.0186552012f, -0.0033409259f, -0.0099533583f, -0.0001081606f, -0.0052685888f, 0f, -0.0044754628f, -0.0005067063f,
        -0.0019934023f, -0.0022260833f, -0.0001682602f, -0.0043629375f, 0f, -0.004613617f, -0.0007999507f, -0.0081316848f,
        -0.0055888461f, -0.012751606f, -0.0382736074f, 0.0078248749f, -0.0976408438f, 0.0403517215f, 0.3403454013f, 0.0447116006f,
        -0.1304056129f, -0.0008072528f, -0.0387399629f, -0.0185191818f, -0.0034038407f, -0.0099190606f, -0.0001260018f, -0.0052597386f,
        0f, -0.0044774517f, -0.0004988537f, -0.0020016365f, -0.0022234923f, -0.0001760303f, -0.0043700054f, 0f,
        -0.0046347665f, -0.0007858006f, -0.0081810414f, -0.0055472987f, -0.0128884596f, -0.0383147748f, 0.0076438759f, -0.0984035505f,
        0.0404629442f, 0.3404275829f, 0.0446158546f, -0.1296120944f, -0.0005775122f, -0.0387608727f, -0.0183830172f, -0.0034662566f,
        -0.0098843284f, -0.0001437693f, -0.0052505642f, 0f, -0.0044792154f, -0.0004909945f, -0.0020097371f, -0.0022207681f,
        -0.0001838106f, -0.0043768701f, 0f, -0.0046556579f, -0.0007715504f, -0.0082301168f, -0.0055052391f, -0.0130254126f,
        -0.038354574f, 0.0074616697f, -0.0991672961f, 0.0405738376f, 0.3405057681f, 0.0445196849f, -0.1288190295f, -0.0003489328f,
        -0.0387801292f, -0.0182467139f, -0.0035281729f, -0.0098491652f, -0.0001614622f, -0.0052410667f, 0f, -0.0044807542f,
        -0.0004831291f, -0.0020177041f, -0.0022179107f, -0.000191601f, -0.0043835309f, 0f, -0.0046762893f, -0.0007572005f,
        -0.0082789071f, -0.0054626674f, -0.0131624595f, -0.0383929973f, 0.0072782567f, -0.0999320668f, 0.040684399f, 0.3405799549f,
        0.0444230937f, -0.1280264336f, -0.0001215166f, -0.038797739f, -0.0181102783f, -0.0035895887f, -0.0098135746f, -0.0001790798f,
        -0.0052312477f, 0f, -0.0044820685f, -0.0004752579f, -0.0020255372f, -0.0022149198f, -0.0001994008f, -0.0043899871f,
        0f, -0.0046966589f, -0.0007427514f, -0.0083274086f, -0.0054195832f, -0.0132995949f, -0.0384300371f, 0.0070936373f,
        -0.1006978488f, 0.0407946261f, 0.3406501416f, 0.0443260834f, -0.127234322f, 0.0001047348f, -0.0388137087f, -0.0179737167f,
        -0.0036505033f, -0.0097775604f, -0.0001966213f, -0.0052211085f, 0f, -0.0044831585f, -0.0004673813f, -0.0020332364f,
        -0.0022117953f, -0.0002072099f, -0.0043962379f, 0f, -0.0047167648f, -0.0007282037f, -0.0083756173f, -0.0053759867f,
        -0.013436813f, -0.0384656861f, 0.0069078123f, -0.1014646281f, 0.0409045164f, 0.3407163265f, 0.044228656f, -0.1264427102f,
        0.0003298194f, -0.038828045f, -0.0178370354f, -0.003710916f, -0.0097411262f, -0.000214086f, -0.0052106505f, 0f,
        -0.0044840247f, -0.0004594997f, -0.0020408016f, -0.002208537f, -0.0002150276f, -0.0044022827f, 0f, -0.0047366054f,
        -0.0007135579f, -0.0084235296f, -0.0053318775f, -0.0135741084f, -0.0384999365f, 0.006720782f, -0.1022323909f, 0.0410140673f,
        0.3407785081f, 0.0441308139f, -0.1256516132f, 0.0005537357f, -0.0388407545f, -0.0177002408f, -0.0037708261f, -0.0097042755f,
        -0.0002314731f, -0.0051998752f, 0f, -0.0044846674f, -0.0004516137f, -0.0020482326f, -0.0022051449f, -0.0002228537f,
        -0.0044081207f, 0f, -0.0047561788f, -0.0006988145f, -0.0084711416f, -0.0052872558f, -0.0137114755f, -0.038532781f,
        0.0065325472f, -0.1030011231f, 0.0411232764f, 0.3408366849f, 0.0440325592f, -0.1248610464f, 0.0007764819f, -0.038851844f,
        -0.017563339f, -0.0038302328f, -0.0096670121f, -0.0002487819f, -0.0051887839f, 0f, -0.0044850869f, -0.0004437235f,
        -0.0020555293f, -0.0022016188f, -0.0002306878f, -0.0044137512f, 0f, -0.0047754831f, -0.0006839742f, -0.0085184496f,
        -0.0052421215f, -0.0138489086f, -0.038564212f, 0.0063431085f, -0.1037708108f, 0.0412321413f, 0.3408908554f, 0.0439338942f,
        -0.124071025f, 0.0009980563f, -0.0388613202f, -0.0174263364f, -0.0038891355f, -0.0096293397f, -0.0002660117f, -0.0051773782f,
        0f, -0.0044852837f, -0.0004358298f, -0.0020626916f, -0.0021979586f, -0.0002385294f, -0.0044191735f, 0f,
        -0.0047945167f, -0.0006690373f, -0.0085654496f, -0.0051964745f, -0.013986402f, -0.038594222f, 0.0061524665f, -0.1045414397f,
        0.0413406594f, 0.3409410186f, 0.0438348212f, -0.1232815641f, 0.0012184573f, -0.0388691899f, -0.0172892392f, -0.0039475336f,
        -0.0095912619f, -0.0002831616f, -0.0051656594f, 0f, -0.0044852582f, -0.0004279327f, -0.0020697195f, -0.0021941642f,
        -0.0002463782f, -0.004424387f, 0f, -0.0048132777f, -0.0006540045f, -0.008612138f, -0.0051503148f, -0.0141239501f,
        -0.0386228037f, 0.005960622f, -0.1053129959f, 0.0414488284f, 0.340987173f, 0.0437353424f, -0.1224926788f, 0.0014376834f,
        -0.0388754599f, -0.0171520536f, -0.0040054265f, -0.0095527824f, -0.000300231f, -0.0051536291f, 0f, -0.0044850107f,
        -0.0004200329f, -0.0020766129f, -0.0021902355f, -0.0002542338f, -0.0044293911f, 0f, -0.0048317645f, -0.0006388764f,
        -0.0086585109f, -0.0051036426f, -0.0142615471f, -0.0386499496f, 0.0057675757f, -0.1060854651f, 0.0415566459f, 0.3410293177f,
        0.0436354601f, -0.1217043843f, 0.0016557329f, -0.038880137f, -0.0170147859f, -0.0040628135f, -0.0095139049f, -0.0003172192f,
        -0.0051412887f, 0f, -0.0044845417f, -0.0004121307f, -0.0020833717f, -0.0021861725f, -0.0002620956f, -0.0044341849f,
        0f, -0.0048499751f, -0.0006236535f, -0.0087045646f, -0.0050564578f, -0.0143991874f, -0.0386756524f, 0.0055733284f,
        -0.1068588331f, 0.0416641092f, 0.3410674516f, 0.0435351766f, -0.1209166956f, 0.0018726045f, -0.0388832282f, -0.0168774422f,
        -0.0041196941f, -0.0094746331f, -0.0003341255f, -0.0051286397f, 0f, -0.0044838516f, -0.0004042265f, -0.0020899958f,
        -0.0021819749f, -0.0002699634f, -0.004438768f, 0f, -0.0048679081f, -0.0006083364f, -0.0087502952f, -0.0050087606f,
        -0.0145368652f, -0.0386999046f, 0.0053778809f, -0.1076330856f, 0.0417712161f, 0.3411015738f, 0.0434344942f, -0.1201296278f,
        0.0020882965f, -0.0388847402f, -0.0167400286f, -0.0041760678f, -0.0094349707f, -0.0003509491f, -0.0051156836f, 0f,
        -0.0044829409f, -0.0003963208f, -0.0020964852f, -0.0021776428f, -0.0002778368f, -0.0044431396f, 0f, -0.0048855615f,
        -0.0005929257f, -0.0087956989f, -0.004960551f, -0.0146745747f, -0.0387226989f, 0.0051812341f, -0.1084082085f, 0.0418779642f,
        0.3411316836f, 0.0433334151f, -0.1193431957f, 0.0023028075f, -0.0388846801f, -0.0166025514f, -0.0042319341f, -0.0093949214f,
        -0.0003676893f, -0.0051024221f, 0f, -0.0044818101f, -0.000388414f, -0.0021028398f, -0.0021731761f, -0.0002857152f,
        -0.0044472991f, 0f, -0.0049029336f, -0.000577422f, -0.0088407721f, -0.0049118292f, -0.0148123101f, -0.0387440279f,
        0.0049833888f, -0.1091841872f, 0.0419843509f, 0.3411577802f, 0.0432319416f, -0.1185574144f, 0.0025161361f, -0.0388830548f,
        -0.0164650166f, -0.0042872924f, -0.009354489f, -0.0003843455f, -0.0050888565f, 0f, -0.0044804595f, -0.0003805065f,
        -0.0021090596f, -0.0021685747f, -0.0002935984f, -0.004451246f, 0f, -0.0049200229f, -0.0005618258f, -0.0088855108f,
        -0.0048625953f, -0.0149500657f, -0.0387638845f, 0.0047843459f, -0.1099610075f, 0.042090374f, 0.3411798629f, 0.0431300762f,
        -0.1177722988f, 0.002728281f, -0.0388798712f, -0.0163274304f, -0.0043421424f, -0.009313677f, -0.000400917f, -0.0050749885f,
        0f, -0.0044788896f, -0.0003725988f, -0.0021151445f, -0.0021638385f, -0.0003014859f, -0.0044549796f, 0f,
        -0.0049368276f, -0.0005461378f, -0.0089299114f, -0.0048128495f, -0.0150878354f, -0.0387822612f, 0.0045841065f, -0.1107386548f,
        0.0421960309f, 0.3411979314f, 0.043027821f, -0.1169878638f, 0.0029392409f, -0.0388751363f, -0.0161897988f, -0.0043964835f,
        -0.0092724894f, -0.0004174031f, -0.0050608195f, 0f, -0.0044771011f, -0.0003646912f, -0.0021210945f, -0.0021589676f,
        -0.0003093773f, -0.0044584993f, 0f, -0.0049533459f, -0.0005303586f, -0.00897397f, -0.004762592f, -0.0152256136f,
        -0.0387991509f, 0.0043826715f, -0.1115171148f, 0.0423013194f, 0.3412119851f, 0.0429251784f, -0.1162041241f, 0.0031490143f,
        -0.0388688572f, -0.0160521279f, -0.0044503153f, -0.0092309298f, -0.0004338031f, -0.0050463513f, 0f, -0.0044750942f,
        -0.0003567841f, -0.0021269097f, -0.0021539618f, -0.0003172721f, -0.0044618046f, 0f, -0.0049695763f, -0.0005144888f,
        -0.0090176829f, -0.004711823f, -0.0153633943f, -0.0388145462f, 0.004180042f, -0.1122963729f, 0.0424062369f, 0.3412220236f,
        0.0428221508f, -0.1154210945f, 0.0033576f, -0.038861041f, -0.0159144237f, -0.0045036375f, -0.0091890019f, -0.0004501163f,
        -0.0050315853f, 0f, -0.0044728697f, -0.0003488781f, -0.0021325899f, -0.0021488212f, -0.00032517f, -0.0044648948f,
        0f, -0.0049855171f, -0.0004985291f, -0.0090610463f, -0.0046605428f, -0.0155011716f, -0.0388284401f, 0.003976219f,
        -0.1130764146f, 0.0425107812f, 0.3412280469f, 0.0427187405f, -0.11463879f, 0.0035649968f, -0.0388516946f, -0.0157766923f,
        -0.0045564497f, -0.0091467095f, -0.0004663422f, -0.0050165232f, 0f, -0.0044704279f, -0.0003409734f, -0.0021381353f,
        -0.0021435457f, -0.0003330706f, -0.0044677694f, 0f, -0.0050011666f, -0.00048248f, -0.0091040564f, -0.0046087516f,
        -0.0156389396f, -0.0388408253f, 0.0037712035f, -0.1138572251f, 0.0426149499f, 0.3412300546f, 0.0426149499f, -0.1138572251f,
        0.0037712035f, -0.0388408253f, -0.0156389396f, -0.0046087516f, -0.0091040564f, -0.00048248f, -0.0050011666f, 0f,
        -0.0044677694f, -0.0003330706f, -0.0021435457f, -0.0021381352f, -0.0003409734f, -0.0044704279f, 0f, -0.0050165232f,
        -0.0004663422f, -0.0091467095f, -0.0045564498f, -0.0157766923f, -0.0388516946f, 0.0035649968f, -0.11463879f, 0.0427187405f,
        0.3412280469f, 0.0425107812f, -0.1130764145f, 0.003976219f, -0.0388284401f, -0.0155011716f, -0.0046605428f, -0.0090610462f,
        -0.0004985291f, -0.0049855171f, 0f, -0.0044648948f, -0.00032517f, -0.0021488212f, -0.0021325899f, -0.0003488781f,
        -0.0044728697f, 0f, -0.0050315853f, -0.0004501163f, -0.0091890019f, -0.0045036376f, -0.0159144237f, -0.0388610409f,
        0.0033576f, -0.1154210946f, 0.0428221508f, 0.3412220236f, 0.0424062369f, -0.1122963728f, 0.004180042f, -0.0388145463f,
        -0.0153633943f, -0.004711823f, -0.0090176828f, -0.0005144888f, -0.0049695763f, 0f, -0.0044618046f, -0.0003172721f,
        -0.0021539618f, -0.0021269097f, -0.0003567841f, -0.0044750943f, 0f, -0.0050463513f, -0.0004338031f, -0.0092309298f,
        -0.0044503153f, -0.0160521279f, -0.0388688572f, 0.0031490142f, -0.1162041242f, 0.0429251784f, 0.3412119851f, 0.0423013193f,
        -0.1115171147f, 0.0043826716f, -0.0387991509f, -0.0152256136f, -0.004762592f, -0.00897397f, -0.0005303586f, -0.0049533459f,
        0f, -0.0044584993f, -0.0003093773f, -0.0021589676f, -0.0021210945f, -0.0003646912f, -0.0044771011f, 0f,
        -0.0050608195f, -0.0004174031f, -0.0092724894f, -0.0043964835f, -0.0161897988f, -0.0388751363f, 0.0029392408f, -0.1169878638f,
        0.043027821f, 0.3411979314f, 0.0421960309f, -0.1107386547f, 0.0045841066f, -0.0387822612f, -0.0150878355f, -0.0048128495f,
        -0.0089299114f, -0.0005461378f, -0.0049368276f, 0f, -0.0044549796f, -0.0003014859f, -0.0021638385f, -0.0021151445f,
        -0.0003725988f, -0.0044788897f, 0f, -0.0050749884f, -0.000400917f, -0.0093136771f, -0.0043421424f, -0.0163274304f,
        -0.0388798711f, 0.002728281f, -0.1177722989f, 0.0431300762f, 0.3411798629f, 0.042090374f, -0.1099610074f, 0.004784346f,
        -0.0387638845f, -0.0149500657f, -0.0048625953f, -0.0088855108f, -0.0005618258f, -0.0049200229f, 0f, -0.004451246f,
        -0.0002935984f, -0.0021685747f, -0.0021090595f, -0.0003805065f, -0.0044804595f, 0f, -0.0050888565f, -0.0003843455f,
        -0.009354489f, -0.0042872924f, -0.0164650166f, -0.0388830547f, 0.0025161361f, -0.1185574145f, 0.0432319416f, 0.3411577802f,
        0.0419843509f, -0.1091841871f, 0.0049833888f, -0.038744028f, -0.0148123101f, -0.0049118292f, -0.0088407721f, -0.000577422f,
        -0.0049029337f, 0f, -0.0044472991f, -0.0002857152f, -0.0021731761f, -0.0021028398f, -0.000388414f, -0.0044818101f,
        0f, -0.005102422f, -0.0003676893f, -0.0093949214f, -0.0042319341f, -0.0166025514f, -0.0388846801f, 0.0023028075f,
        -0.1193431958f, 0.0433334151f, 0.3411316836f, 0.0418779642f, -0.1084082084f, 0.0051812341f, -0.0387226989f, -0.0146745747f,
        -0.004960551f, -0.0087956989f, -0.0005929257f, -0.0048855615f, 0f, -0.0044431396f, -0.0002778368f, -0.0021776428f,
        -0.0020964852f, -0.0003963208f, -0.0044829409f, 0f, -0.0051156836f, -0.0003509491f, -0.0094349707f, -0.0041760679f,
        -0.0167400286f, -0.0388847402f, 0.0020882964f, -0.1201296278f, 0.0434344942f, 0.3411015738f, 0.0417712161f, -0.1076330856f,
        0.0053778809f, -0.0386999046f, -0.0145368652f, -0.0050087606f, -0.0087502952f, -0.0006083364f, -0.0048679081f, 0f,
        -0.0044387679f, -0.0002699634f, -0.0021819749f, -0.0020899958f, -0.0004042265f, -0.0044838516f, 0f, -0.0051286397f,
        -0.0003341255f, -0.0094746331f, -0.0041196941f, -0.0168774421f, -0.0388832281f, 0.0018726044f, -0.1209166957f, 0.0435351766f,
        0.3410674516f, 0.0416641092f, -0.106858833f, 0.0055733284f, -0.0386756524f, -0.0143991874f, -0.0050564578f, -0.0087045646f,
        -0.0006236535f, -0.0048499752f, 0f, -0.0044341849f, -0.0002620956f, -0.0021861725f, -0.0020833717f, -0.0004121307f,
        -0.0044845417f, 0f, -0.0051412886f, -0.0003172192f, -0.0095139049f, -0.0040628135f, -0.0170147859f, -0.038880137f,
        0.0016557329f, -0.1217043844f, 0.0436354601f, 0.3410293177f, 0.0415566458f, -0.106085465f, 0.0057675757f, -0.0386499497f,
        -0.0142615471f, -0.0051036426f, -0.0086585109f, -0.0006388764f, -0.0048317645f, 0f, -0.004429391f, -0.0002542338f,
        -0.0021902356f, -0.0020766129f, -0.0004200329f, -0.0044850107f, 0f, -0.005153629f, -0.000300231f, -0.0095527824f,
        -0.0040054265f, -0.0171520536f, -0.0388754598f, 0.0014376834f, -0.1224926789f, 0.0437353424f, 0.340987173f, 0.0414488284f,
        -0.1053129958f, 0.005960622f, -0.0386228038f, -0.0141239501f, -0.0051503148f, -0.008612138f, -0.0006540045f, -0.0048132777f,
        0f, -0.004424387f, -0.0002463782f, -0.0021941642f, -0.0020697195f, -0.0004279327f, -0.0044852582f, 0f,
        -0.0051656594f, -0.0002831616f, -0.0095912619f, -0.0039475336f, -0.0172892392f, -0.0388691898f, 0.0012184573f, -0.1232815642f,
        0.0438348212f, 0.3409410185f, 0.0413406594f, -0.1045414397f, 0.0061524666f, -0.0385942221f, -0.013986402f, -0.0051964745f,
        -0.0085654496f, -0.0006690373f, -0.0047945167f, 0f, -0.0044191735f, -0.0002385294f, -0.0021979586f, -0.0020626916f,
        -0.0004358298f, -0.0044852837f, 0f, -0.0051773782f, -0.0002660117f, -0.0096293397f, -0.0038891355f, -0.0174263364f,
        -0.0388613201f, 0.0009980563f, -0.1240710251f, 0.0439338942f, 0.3408908554f, 0.0412321413f, -0.1037708107f, 0.0063431085f,
        -0.038564212f, -0.0138489086f, -0.0052421215f, -0.0085184495f, -0.0006839742f, -0.0047754831f, 0f, -0.0044137512f,
        -0.0002306878f, -0.0022016188f, -0.0020555293f, -0.0004437235f, -0.0044850869f, 0f, -0.0051887839f, -0.0002487819f,
        -0.0096670121f, -0.0038302328f, -0.017563339f, -0.0388518439f, 0.0007764818f, -0.1248610465f, 0.0440325592f, 0.3408366849f,
        0.0411232764f, -0.1030011231f, 0.0065325473f, -0.038532781f, -0.0137114755f, -0.0052872558f, -0.0084711416f, -0.0006988145f,
        -0.0047561788f, 0f, -0.0044081207f, -0.0002228537f, -0.0022051449f, -0.0020482325f, -0.0004516137f, -0.0044846674f,
        0f, -0.0051998752f, -0.0002314731f, -0.0097042755f, -0.0037708261f, -0.0177002407f, -0.0388407544f, 0.0005537357f,
        -0.1256516133f, 0.0441308139f, 0.3407785081f, 0.0410140673f, -0.1022323908f, 0.0067207821f, -0.0384999365f, -0.0135741085f,
        -0.0053318775f, -0.0084235296f, -0.0007135579f, -0.0047366054f, 0f, -0.0044022827f, -0.0002150276f, -0.002208537f,
        -0.0020408016f, -0.0004594997f, -0.0044840247f, 0f, -0.0052106505f, -0.000214086f, -0.0097411262f, -0.003710916f,
        -0.0178370354f, -0.0388280449f, 0.0003298194f, -0.1264427102f, 0.044228656f, 0.3407163265f, 0.0409045164f, -0.1014646281f,
        0.0069078123f, -0.0384656861f, -0.013436813f, -0.0053759866f, -0.0083756173f, -0.0007282037f, -0.0047167649f, 0f,
        -0.0043962379f, -0.0002072099f, -0.0022117953f, -0.0020332364f, -0.0004673813f, -0.0044831585f, 0f, -0.0052211085f,
        -0.0001966213f, -0.0097775604f, -0.0036505033f, -0.0179737167f, -0.0388137087f, 0.0001047348f, -0.1272343221f, 0.0443260834f,
        0.3406501416f, 0.0407946261f, -0.1006978487f, 0.0070936374f, -0.0384300372f, -0.0132995949f, -0.0054195832f, -0.0083274085f,
        -0.0007427514f, -0.0046966589f, 0f, -0.0043899871f, -0.0001994008f, -0.0022149198f, -0.0020255372f, -0.0004752579f,
        -0.0044820685f, 0f, -0.0052312477f, -0.0001790798f, -0.0098135747f, -0.0035895887f, -0.0181102783f, -0.038797739f,
        -0.0001215166f, -0.1280264337f, 0.0444230937f, 0.3405799549f, 0.040684399f, -0.0999320667f, 0.0072782567f, -0.0383929973f,
        -0.0131624596f, -0.0054626673f, -0.0082789071f, -0.0007572005f, -0.0046762893f, 0f, -0.0043835309f, -0.000191601f,
        -0.0022179107f, -0.0020177041f, -0.0004831291f, -0.0044807543f, 0f, -0.0052410667f, -0.0001614622f, -0.0098491652f,
        -0.0035281729f, -0.0182467139f, -0.0387801292f, -0.0003489328f, -0.1288190296f, 0.0445196849f, 0.3405057681f, 0.0405738376f,
        -0.099167296f, 0.0074616697f, -0.038354574f, -0.0130254126f, -0.0055052391f, -0.0082301168f, -0.0007715504f, -0.0046556579f,
        0f, -0.0043768701f, -0.0001838106f, -0.0022207681f, -0.0020097371f, -0.0004909945f, -0.0044792154f, 0f,
        -0.0052505641f, -0.0001437693f, -0.0098843284f, -0.0034662566f, -0.0183830172f, -0.0387608727f, -0.0005775122f, -0.1296120945f,
        0.0446158546f, 0.3404275829f, 0.0404629442f, -0.0984035504f, 0.007643876f, -0.0383147748f, -0.0128884596f, -0.0055472986f,
        -0.0081810414f, -0.0007858006f, -0.0046347665f, 0f, -0.0043700054f, -0.0001760303f, -0.0022234923f, -0.0020016365f,
        -0.0004988537f, -0.0044774517f, 0f, -0.0052597386f, -0.0001260018f, -0.0099190607f, -0.0034038407f, -0.0185191817f,
        -0.0387399629f, -0.0008072528f, -0.130405613f, 0.0447116006f, 0.3403454013f, 0.0403517215f, -0.0976408437f, 0.0078248749f,
        -0.0382736074f, -0.012751606f, -0.0055888461f, -0.0081316848f, -0.0007999507f, -0.0046136171f, 0f, -0.0043629375f,
        -0.0001682602f, -0.0022260833f, -0.0019934023f, -0.0005067063f, -0.0044754628f, 0f, -0.0052685888f, -0.0001081606f,
        -0.0099533583f, -0.0033409259f, -0.0186552012f, -0.0387173931f, -0.0010381529f, -0.1311995698f, 0.0448069208f, 0.3402592251f,
        0.0402401719f, -0.0968791897f, 0.0080046662f, -0.0382310793f, -0.0126148574f, -0.0056298817f, -0.0080820507f, -0.0008140001f,
        -0.0045922113f, 0f, -0.0043556672f, -0.0001605009f, -0.0022285413f, -0.0019850347f, -0.0005145518f, -0.0044732484f,
        0f, -0.0052771133f, -9.02464e-05f, -0.0099872178f, -0.0032775132f, -0.0187910693f, -0.038693157f, -0.0012702104f,
        -0.1319939495f, 0.044901813f, 0.3401690565f, 0.0401282978f, -0.0961186021f, 0.0081832493f, -0.0381871982f, -0.0124782192f,
        -0.0056704055f, -0.008032143f, -0.0008279483f, -0.0045705511f, 0f, -0.0043481953f, -0.0001527527f, -0.0022308665f,
        -0.001976534f, -0.0005223897f, -0.0044708081f, 0f, -0.0052853108f, -7.226e-05f, -0.0100206355f, -0.0032136033f,
        -0.0189267794f, -0.0386672479f, -0.0015034235f, -0.1327887364f, 0.044996275f, 0.3400748975f, 0.0400161019f, -0.0953590945f,
        0.008360624f, -0.0381419716f, -0.0123416967f, -0.0057104179f, -0.0079819655f, -0.0008417948f, -0.0045486383f, 0f,
        -0.0043405225f, -0.0001450159f, -0.002233059f, -0.0019679001f, -0.0005302197f, -0.0044681417f, 0f, -0.00529318f,
        -5.42022e-05f, -0.0100536077f, -0.0031491971f, -0.0190623252f, -0.0386396594f, -0.0017377902f, -0.1335839153f, 0.0450903047f,
        0.3399767503f, 0.0399035866f, -0.0946006806f, 0.0085367899f, -0.0380954073f, -0.0122052956f, -0.005749919f, -0.0079315219f,
        -0.0008555392f, -0.0045264749f, 0f, -0.0043326497f, -0.0001372911f, -0.0022351191f, -0.0019591334f, -0.0005380413f,
        -0.004465249f, 0f, -0.0053007195f, -3.60739e-05f, -0.010086131f, -0.0030842957f, -0.0191977003f, -0.0386103851f,
        -0.0019733085f, -0.1343794704f, 0.0451838998f, 0.3398746174f, 0.0397907545f, -0.0938433739f, 0.0087117467f, -0.0380475129f,
        -0.012069021f, -0.0057889091f, -0.0078808162f, -0.000869181f, -0.0045040626f, 0f, -0.0043245776f, -0.0001295786f,
        -0.0022370469f, -0.0019502339f, -0.0005458541f, -0.0044621297f, 0f, -0.0053079281f, -1.78757e-05f, -0.0101182017f,
        -0.0030188998f, -0.0193328982f, -0.0385794185f, -0.0022099762f, -0.1351753864f, 0.0452770582f, 0.3397685011f, 0.0396776081f,
        -0.0930871879f, 0.0088854941f, -0.0379982962f, -0.0119328784f, -0.0058273885f, -0.007829852f, -0.0008827197f, -0.0044814034f,
        0f, -0.0043163069f, -0.0001218787f, -0.0022388427f, -0.0019412019f, -0.0005536577f, -0.0044587834f, 0f,
        -0.0053148046f, 3.915e-07f, -0.0101498163f, -0.0029530105f, -0.0194679123f, -0.0385467533f, -0.0024477915f, -0.1359716475f,
        0.0453697778f, 0.3396584039f, 0.0395641498f, -0.0923321362f, 0.0090580319f, -0.0379477648f, -0.0117968732f, -0.0058653575f,
        -0.0077786333f, -0.0008961548f, -0.0044584992f, 0f, -0.0043078386f, -0.0001141919f, -0.0022405065f, -0.0019320376f,
        -0.0005614516f, -0.0044552101f, 0f, -0.0053213476f, 1.87268e-05f, -0.0101809712f, -0.0028866287f, -0.0196027363f,
        -0.0385123832f, -0.002686752f, -0.1367682382f, 0.0454620565f, 0.3395443285f, 0.0394503824f, -0.091578232f, 0.0092293599f,
        -0.0378959264f, -0.0116610106f, -0.0059028164f, -0.0077271638f, -0.000909486f, -0.0044353519f, 0f, -0.0042991734f,
        -0.0001065185f, -0.0022420388f, -0.0019227411f, -0.0005692354f, -0.0044514094f, 0f, -0.0053275559f, 3.71294e-05f,
        -0.0102116628f, -0.0028197555f, -0.0197373635f, -0.0384763017f, -0.0029268557f, -0.1375651428f, 0.045553892f, 0.3394262775f,
        0.0393363082f, -0.0908254888f, 0.009399478f, -0.0378427889f, -0.0115252959f, -0.0059397656f, -0.0076754473f, -0.0009227127f,
        -0.0044119634f, 0f, -0.0042903122f, -9.88589e-05f, -0.0022434396f, -0.0019133128f, -0.0005770086f, -0.0044473811f,
        0f, -0.0053334282f, 5.55985e-05f, -0.0102418876f, -0.0027523919f, -0.0198717875f, -0.0384385027f, -0.0031681004f,
        -0.1383623456f, 0.0456452825f, 0.3393042538f, 0.0392219298f, -0.0900739199f, 0.0095683859f, -0.0377883599f, -0.0113897343f,
        -0.0059762055f, -0.0076234876f, -0.0009358345f, -0.0043883356f, 0f, -0.0042812558f, -9.12135e-05f, -0.0022447092f,
        -0.0019037527f, -0.0005847708f, -0.0044431251f, 0f, -0.0053389634f, 7.41333e-05f, -0.0102716422f, -0.0026845389f,
        -0.0200060016f, -0.0383989799f, -0.0034104839f, -0.139159831f, 0.0457362256f, 0.3391782602f, 0.0391072499f, -0.0893235386f,
        0.0097360837f, -0.0377326473f, -0.0112543312f, -0.0060121364f, -0.0075712886f, -0.000948851f, -0.0043644705f, 0f,
        -0.0042720051f, -8.35826e-05f, -0.0022458478f, -0.0018940612f, -0.0005925217f, -0.0044386412f, 0f, -0.0053441603f,
        9.27328e-05f, -0.0103009229f, -0.0026161978f, -0.0201399994f, -0.0383577271f, -0.0036540038f, -0.1399575832f, 0.0458267193f,
        0.3390482998f, 0.0389922709f, -0.0885743579f, 0.0099025712f, -0.0376756588f, -0.0111190917f, -0.0060475588f, -0.0075188541f,
        -0.0009617617f, -0.0043403699f, 0f, -0.0042625608f, -7.59667e-05f, -0.0022468557f, -0.0018842384f, -0.0006002607f,
        -0.0044339291f, 0f, -0.0053490176f, 0.0001113964f, -0.0103297262f, -0.0025473695f, -0.0202737743f, -0.0383147381f,
        -0.003898658f, -0.1407555865f, 0.0459167616f, 0.3389143755f, 0.0388769954f, -0.0878263913f, 0.0100678484f, -0.0376174023f,
        -0.010984021f, -0.006082473f, -0.0074661878f, -0.0009745663f, -0.004316036f, 0f, -0.0042529239f, -6.83661e-05f,
        -0.0022477331f, -0.0018742846f, -0.0006079874f, -0.0044289888f, 0f, -0.0053535342f, 0.000130123f, -0.0103580487f,
        -0.0024780552f, -0.0204073195f, -0.0382700068f, -0.004144444f, -0.141553825f, 0.0460063503f, 0.3387764907f, 0.038761426f,
        -0.0870796516f, 0.0102319154f, -0.0375578856f, -0.0108491242f, -0.0061168796f, -0.0074132935f, -0.0009872643f, -0.0042914705f,
        0f, -0.0042430953f, -6.07812e-05f, -0.0022484803f, -0.0018642f, -0.0006157014f, -0.00442382f, 0f,
        -0.0053577089f, 0.0001489119f, -0.0103858869f, -0.0024082561f, -0.0205406287f, -0.038223527f, -0.0043913596f, -0.142352283f,
        0.0460954835f, 0.3386346485f, 0.0386455653f, -0.0863341521f, 0.0103947721f, -0.0374971165f, -0.0107144066f, -0.0061507791f,
        -0.0073601751f, -0.0009998553f, -0.0042666755f, 0f, -0.0042330758f, -5.32124e-05f, -0.0022490975f, -0.001853985f,
        -0.0006234022f, -0.0044184227f, 0f, -0.0053615406f, 0.0001677622f, -0.0104132373f, -0.0023379734f, -0.020673695f,
        -0.0381752928f, -0.0046394023f, -0.1431509446f, 0.0461841589f, 0.3384888524f, 0.0385294158f, -0.0855899056f, 0.0105564187f,
        -0.0374351029f, -0.0105798731f, -0.0061841719f, -0.0073068364f, -0.0010123389f, -0.004241653f, 0f, -0.0042228663f,
        -4.566e-05f, -0.002249585f, -0.0018436397f, -0.0006310895f, -0.0044127966f, 0f, -0.0053650282f, 0.0001866729f,
        -0.0104400964f, -0.0022672083f, -0.0208065119f, -0.038125298f, -0.0048885696f, -0.143949794f, 0.0462723747f, 0.3383391057f,
        0.0384129801f, -0.0848469252f, 0.0107168552f, -0.0373718527f, -0.0104455288f, -0.0062170586f, -0.0072532811f, -0.0010247147f,
        -0.0042164049f, 0f, -0.0042124676f, -3.81244e-05f, -0.0022499431f, -0.0018331645f, -0.0006387627f, -0.0044069417f,
        0f, -0.0053681706f, 0.0002056433f, -0.0104664608f, -0.0021959621f, -0.0209390726f, -0.0380735367f, -0.0051388592f,
        -0.1447488152f, 0.0463601287f, 0.338185412f, 0.0382962609f, -0.0841052237f, 0.0108760818f, -0.0373073737f, -0.0103113789f,
        -0.0062494397f, -0.007199513f, -0.0010369824f, -0.0041909332f, 0f, -0.0042018808f, -3.0606e-05f, -0.002250172f,
        -0.0018225596f, -0.0006464215f, -0.0044008579f, 0f, -0.0053709666f, 0.0002246724f, -0.010492327f, -0.0021242359f,
        -0.0210713706f, -0.038020003f, -0.0053902685f, -0.1455479924f, 0.046447419f, 0.338027775f, 0.0381792607f, -0.083364814f,
        0.0110340987f, -0.0372416738f, -0.0101774283f, -0.0062813158f, -0.007145536f, -0.0010491416f, -0.00416524f, 0f,
        -0.0041911068f, -2.3105e-05f, -0.002250272f, -0.0018118253f, -0.0006540653f, -0.0043945451f, 0f, -0.0053734153f,
        0.0002437593f, -0.0105176916f, -0.0020520312f, -0.0212033992f, -0.0379646908f, -0.0056427949f, -0.1463473095f, 0.0465342435f,
        0.3378661983f, 0.038061982f, -0.0826257089f, 0.011190906f, -0.0371747611f, -0.010043682f, -0.0063126874f, -0.0070913538f,
        -0.0010611918f, -0.0041393271f, 0f, -0.0041801464f, -1.5622e-05f, -0.0022502435f, -0.0018009619f, -0.0006616939f,
        -0.0043880031f, 0f, -0.0053755154f, 0.0002629032f, -0.0105425513f, -0.0019793492f, -0.0213351515f, -0.0379075943f,
        -0.0058964359f, -0.1471467508f, 0.0466206001f, 0.3377006859f, 0.0379444276f, -0.0818879212f, 0.011346504f, -0.0371066433f,
        -0.009910145f, -0.0063435552f, -0.0070369703f, -0.0010731328f, -0.0041131967f, 0f, -0.0041690005f, -8.1572e-06f,
        -0.0022500867f, -0.0017899697f, -0.0006693067f, -0.004381232f, 0f, -0.0053772661f, 0.000282103f, -0.0105669025f,
        -0.0019061913f, -0.021466621f, -0.0378487076f, -0.0061511889f, -0.1479463001f, 0.046706487f, 0.3375312416f, 0.0378265999f,
        -0.0811514634f, 0.011500893f, -0.0370373284f, -0.0097768223f, -0.0063739198f, -0.0069823891f, -0.0010849642f, -0.0040868507f,
        0f, -0.0041576702f, -7.111e-07f, -0.002249802f, -0.001778849f, -0.0006769033f, -0.0043742317f, 0f,
        -0.0053786662f, 0.000301358f, -0.0105907419f, -0.0018325589f, -0.0215978008f, -0.037788025f, -0.0064070513f, -0.1487459414f,
        0.0467919022f, 0.3373578695f, 0.0377085017f, -0.0804163483f, 0.0116540733f, -0.0369668245f, -0.0096437186f, -0.0064037818f,
        -0.0069276141f, -0.0010966856f, -0.0040602911f, 0f, -0.0041461564f, 6.7161e-06f, -0.0022493895f, -0.0017676002f,
        -0.0006844832f, -0.0043670022f, 0f, -0.0053797147f, 0.0003206673f, -0.0106140661f, -0.0017584533f, -0.0217286842f,
        -0.0377255407f, -0.0066640202f, -0.1495456587f, 0.0468768435f, 0.3371805736f, 0.0375901355f, -0.0796825883f, 0.0118060452f,
        -0.0368951394f, -0.009510839f, -0.006433142f, -0.0068726491f, -0.0011082966f, -0.0040335201f, 0f, -0.00413446f,
        1.4124e-05f, -0.0022488498f, -0.0017562235f, -0.0006920461f, -0.0043595433f, 0f, -0.0053804107f, 0.0003400297f,
        -0.0106368718f, -0.001683876f, -0.0218592645f, -0.0376612488f, -0.0069220932f, -0.150345436f, 0.0469613092f, 0.3369993582f,
        0.037471504f, -0.0789501962f, 0.0119568091f, -0.0368222811f, -0.0093781883f, -0.006462001f, -0.0068174978f, -0.001119797f,
        -0.0040065395f, 0f, -0.0041225819f, 2.15121e-05f, -0.002248183f, -0.0017447193f, -0.0006995915f, -0.0043518551f,
        0f, -0.0053807532f, 0.0003594446f, -0.0106591556f, -0.0016088285f, -0.0219895349f, -0.0375951438f, -0.0071812673f,
        -0.1511452572f, 0.0470452972f, 0.3368142276f, 0.0373526098f, -0.0782191842f, 0.0121063653f, -0.0367482577f, -0.0092457713f,
        -0.0064903595f, -0.006762164f, -0.0011311865f, -0.0039793516f, 0f, -0.0041105233f, 2.88803e-05f, -0.0022473896f,
        -0.001733088f, -0.0007071189f, -0.0043439376f, 0f, -0.0053807412f, 0.0003789108f, -0.0106809142f, -0.0015333122f,
        -0.0221194886f, -0.0375272199f, -0.0074415398f, -0.1519451062f, 0.0471288056f, 0.336625186f, 0.0372334555f, -0.0774895648f,
        0.0122547144f, -0.0366730771f, -0.0091135929f, -0.0065182182f, -0.0067066515f, -0.0011424646f, -0.0039519582f, 0f,
        -0.004098285f, 3.6228e-05f, -0.0022464698f, -0.0017213298f, -0.000714628f, -0.0043357908f, 0f, -0.0053803737f,
        0.0003984276f, -0.0107021441f, -0.0014573285f, -0.0222491188f, -0.0374574714f, -0.0077029079f, -0.1527449668f, 0.0472118324f,
        0.3364322381f, 0.0371140437f, -0.0767613504f, 0.0124018569f, -0.0365967473f, -0.0089816577f, -0.006545578f, -0.006650964f,
        -0.0011536311f, -0.0039243615f, 0f, -0.004085868f, 4.3555e-05f, -0.002245424f, -0.0017094452f, -0.0007221183f,
        -0.0043274147f, 0f, -0.0053796499f, 0.0004179938f, -0.0107228422f, -0.0013808792f, -0.0223784186f, -0.037385893f,
        -0.0079653687f, -0.1535448229f, 0.0472943758f, 0.3362353883f, 0.036994377f, -0.0760345533f, 0.0125477931f, -0.0365192764f,
        -0.0088499706f, -0.0065724395f, -0.0065951053f, -0.0011646856f, -0.0038965636f, 0f, -0.0040732733f, 5.0861e-05f,
        -0.0022442526f, -0.0016974345f, -0.0007295893f, -0.0043188094f, 0f, -0.0053785688f, 0.0004376087f, -0.0107430052f,
        -0.0013039657f, -0.0225073813f, -0.0373124789f, -0.0082289193f, -0.1543446584f, 0.0473764338f, 0.3360346413f, 0.0368744582f,
        -0.0753091856f, 0.0126925236f, -0.0364406724f, -0.0087185362f, -0.0065988036f, -0.0065390792f, -0.001175628f, -0.0038685664f,
        0f, -0.004060502f, 5.81455e-05f, -0.0022429559f, -0.0016852981f, -0.0007370406f, -0.0043099748f, 0f,
        -0.0053771295f, 0.0004572712f, -0.0107626296f, -0.0012265895f, -0.022636f, -0.0372372236f, -0.0084935568f, -0.1551444571f,
        0.0474580044f, 0.335830002f, 0.0367542899f, -0.0745852596f, 0.0128360491f, -0.0363609433f, -0.0085873593f, -0.0066246711f,
        -0.0064828893f, -0.0011864578f, -0.0038403722f, 0f, -0.004047555f, 6.54082e-05f, -0.0022415343f, -0.0016730363f,
        -0.0007444718f, -0.0043009111f, 0f, -0.0053753312f, 0.0004769803f, -0.0107817124f, -0.0011487524f, -0.0227642679f,
        -0.0371601218f, -0.0087592783f, -0.1559442028f, 0.047539086f, 0.335621475f, 0.0366338746f, -0.0738627875f, 0.01297837f,
        -0.0362800973f, -0.0084564445f, -0.0066500428f, -0.0064265395f, -0.0011971748f, -0.0038119828f, 0f, -0.0040344333f,
        7.26488e-05f, -0.0022399882f, -0.0016606496f, -0.0007518825f, -0.0042916184f, 0f, -0.005373173f, 0.0004967351f,
        -0.0108002501f, -0.0010704558f, -0.022892178f, -0.0370811679f, -0.0090260808f, -0.1567438792f, 0.0476196764f, 0.3354090654f,
        0.0365132151f, -0.0731417812f, 0.0131194871f, -0.0361981423f, -0.0083257964f, -0.0066749196f, -0.0063700335f, -0.0012077788f,
        -0.0037834005f, 0f, -0.004021138f, 7.9867e-05f, -0.0022383178f, -0.0016481383f, -0.0007592722f, -0.0042820966f,
        0f, -0.005370654f, 0.0005165347f, -0.0108182397f, -0.0009917016f, -0.0230197236f, -0.0370003566f, -0.0092939612f,
        -0.1575434701f, 0.0476997739f, 0.3351927782f, 0.036392314f, -0.0724222528f, 0.013259401f, -0.0361150865f, -0.0081954197f,
        -0.0066993023f, -0.006313375f, -0.0012182693f, -0.0037546274f, 0f, -0.0040076702f, 8.70624e-05f, -0.0022365237f,
        -0.0016355029f, -0.0007666405f, -0.0042723459f, 0f, -0.0053677735f, 0.000536378f, -0.0108356777f, -0.0009124914f,
        -0.0231468976f, -0.0369176825f, -0.0095629164f, -0.1583429593f, 0.0477793767f, 0.3349726184f, 0.036271174f, -0.0717042143f,
        0.0133981124f, -0.0360309379f, -0.008065319f, -0.0067231918f, -0.0062565678f, -0.0012286462f, -0.0037256654f, 0f,
        -0.0039940308f, 9.42346e-05f, -0.0022346062f, -0.0016227437f, -0.0007739869f, -0.0042623664f, 0f, -0.0053645305f,
        0.000556264f, -0.0108525611f, -0.0008328269f, -0.0232736933f, -0.0368331403f, -0.0098329434f, -0.1591423305f, 0.0478584828f,
        0.3347485913f, 0.0361497976f, -0.0709876776f, 0.013535622f, -0.0359457046f, -0.0079354987f, -0.0067465891f, -0.0061996156f,
        -0.0012389093f, -0.0036965167f, 0f, -0.0039802209f, 0.0001013834f, -0.0022325657f, -0.0016098612f, -0.0007813111f,
        -0.0042521582f, 0f, -0.0053609244f, 0.0005761919f, -0.0108688867f, -0.0007527098f, -0.0234001037f, -0.0367467248f,
        -0.010104039f, -0.1599415674f, 0.0479370904f, 0.3345207023f, 0.0360281877f, -0.0702726544f, 0.0136719306f, -0.0358593948f,
        -0.0078059635f, -0.006769495f, -0.006142522f, -0.0012490582f, -0.0036671835f, 0f, -0.0039662415f, 0.0001085084f,
        -0.0022304025f, -0.0015968558f, -0.0007886125f, -0.0042417215f, 0f, -0.0053569543f, 0.0005961605f, -0.0108846512f,
        -0.0006721419f, -0.0235261219f, -0.0366584307f, -0.0103762f, -0.1607406537f, 0.0480151977f, 0.3342889566f, 0.0359063468f,
        -0.0695591566f, 0.013807039f, -0.0357720166f, -0.0076767177f, -0.0067919106f, -0.0060852909f, -0.0012590927f, -0.0036376678f,
        0f, -0.0039520938f, 0.0001156093f, -0.0022281172f, -0.0015837279f, -0.0007958908f, -0.0042310564f, 0f,
        -0.0053526196f, 0.0006161688f, -0.0108998515f, -0.000591125f, -0.0236517409f, -0.0365682528f, -0.0106494233f, -0.161539573f,
        0.0480928029f, 0.3340533598f, 0.0357842777f, -0.0688471958f, 0.013940948f, -0.035683578f, -0.0075477659f, -0.0068138368f,
        -0.0060279259f, -0.0012690125f, -0.0036079717f, 0f, -0.0039377788f, 0.0001226857f, -0.0022257101f, -0.001570478f,
        -0.0008031455f, -0.004220163f, 0f, -0.0053479193f, 0.000636216f, -0.0109144844f, -0.000509661f, -0.0237769538f,
        -0.0364761859f, -0.0109237057f, -0.1623383091f, 0.0481699042f, 0.3338139174f, 0.0356619829f, -0.0681367838f, 0.0140736585f,
        -0.0355940872f, -0.0074191125f, -0.0068352745f, -0.0059704308f, -0.0012788176f, -0.0035780974f, 0f, -0.0039232975f,
        0.0001297374f, -0.0022231816f, -0.0015571066f, -0.0008103762f, -0.0042090416f, 0f, -0.005342853f, 0.0006563009f,
        -0.0109285469f, -0.0004277515f, -0.0239017537f, -0.036382225f, -0.0111990437f, -0.1631368455f, 0.0482464997f, 0.3335706351f,
        0.0355394652f, -0.0674279322f, 0.0142051713f, -0.0355035523f, -0.0072907619f, -0.0068562248f, -0.0059128091f, -0.0012885075f,
        -0.003548047f, 0f, -0.003908651f, 0.0001367639f, -0.0022205322f, -0.001543614f, -0.0008175825f, -0.0041976923f,
        0f, -0.0053374197f, 0.0006764225f, -0.0109420357f, -0.0003453987f, -0.0240261335f, -0.0362863651f, -0.0114754342f,
        -0.163935166f, 0.0483225878f, 0.3333235186f, 0.0354167272f, -0.0667206525f, 0.0143354873f, -0.0354119815f, -0.0071627185f,
        -0.0068766887f, -0.0058550647f, -0.0012980821f, -0.0035178226f, 0f, -0.0038938405f, 0.000143765f, -0.0022177622f,
        -0.0015300007f, -0.0008247639f, -0.0041861153f, 0f, -0.0053316189f, 0.0006965798f, -0.0109549479f, -0.0002626042f,
        -0.0241500864f, -0.036188601f, -0.0117528738f, -0.164733254f, 0.0483981666f, 0.3330725738f, 0.0352937717f, -0.0660149561f,
        0.0144646076f, -0.0353193829f, -0.0070349866f, -0.0068966673f, -0.0057972013f, -0.0013075412f, -0.0034874264f, 0f,
        -0.0038788671f, 0.0001507404f, -0.0022148722f, -0.0015162673f, -0.00083192f, -0.0041743109f, 0f, -0.0053254499f,
        0.0007167719f, -0.0109672803f, -0.0001793701f, -0.0242736053f, -0.0360889278f, -0.0120313591f, -0.1655310933f, 0.0484732343f,
        0.3328178065f, 0.0351706013f, -0.0653108546f, 0.0145925331f, -0.0352257648f, -0.0069075706f, -0.0069161616f, -0.0057392223f,
        -0.0013168846f, -0.0034568604f, 0f, -0.0038637317f, 0.0001576897f, -0.0022118626f, -0.0015024141f, -0.0008390504f,
        -0.0041622791f, 0f, -0.005318912f, 0.0007369976f, -0.0109790298f, -9.56983e-05f, -0.0243966831f, -0.0359873406f,
        -0.0123108867f, -0.1663286674f, 0.0485477893f, 0.3325592229f, 0.0350472187f, -0.0646083592f, 0.0147192647f, -0.0351311351f,
        -0.0067804747f, -0.0069351726f, -0.0056811317f, -0.0013261121f, -0.0034261268f, 0f, -0.0038484356f, 0.0001646126f,
        -0.0022087338f, -0.0014884417f, -0.0008461546f, -0.0041500204f, 0f, -0.0053120047f, 0.0007572559f, -0.0109901934f,
        -1.15908e-05f, -0.024519313f, -0.0358838345f, -0.0125914532f, -0.1671259599f, 0.0486218298f, 0.3322968291f, 0.0349236266f,
        -0.0639074812f, 0.0148448037f, -0.0350355022f, -0.0066537032f, -0.0069537016f, -0.005622933f, -0.0013352235f, -0.0033952278f,
        0f, -0.0038329799f, 0.0001715089f, -0.0022054862f, -0.0014743506f, -0.0008532322f, -0.0041375349f, 0f,
        -0.0053047272f, 0.0007775458f, -0.0110007681f, 7.29505e-05f, -0.0246414879f, -0.0357784046f, -0.0128730551f, -0.1679229543f,
        0.048695354f, 0.3320306311f, 0.0347998277f, -0.063208232f, 0.014969151f, -0.0349388742f, -0.0065272604f, -0.0069717496f,
        -0.0055646298f, -0.0013442185f, -0.0033641655f, 0f, -0.0038173656f, 0.0001783781f, -0.0022021204f, -0.0014601413f,
        -0.0008602829f, -0.0041248229f, 0f, -0.005297079f, 0.0007978663f, -0.0110107508f, 0.0001579234f, -0.0247632008f,
        -0.0356710461f, -0.0131556889f, -0.1687196342f, 0.0487683603f, 0.3317606353f, 0.0346758246f, -0.0625106226f, 0.0150923077f,
        -0.0348412592f, -0.0064011503f, -0.0069893178f, -0.0055062259f, -0.0013530971f, -0.0033329421f, 0f, -0.0038015939f,
        0.00018522f, -0.0021986368f, -0.0014458142f, -0.000867306f, -0.0041118847f, 0f, -0.0052890596f, 0.0008182162f,
        -0.0110201386f, 0.000243326f, -0.0248844446f, -0.0355617542f, -0.013439351f, -0.1695159832f, 0.0488408469f, 0.3314868481f,
        0.0345516202f, -0.0618146642f, 0.015214275f, -0.0347426656f, -0.0062753773f, -0.0070064072f, -0.0054477249f, -0.0013618591f,
        -0.0033015597f, 0f, -0.003785666f, 0.0001920343f, -0.0021950359f, -0.00143137f, -0.0008743014f, -0.0040987205f,
        0f, -0.0052806684f, 0.0008385945f, -0.0110289284f, 0.0003291562f, -0.0250052123f, -0.0354505243f, -0.0137240378f,
        -0.1703119847f, 0.0489128121f, 0.331209276f, 0.034427217f, -0.0611203679f, 0.015335054f, -0.0346431013f, -0.0061499454f,
        -0.0070230191f, -0.0053891304f, -0.0013705042f, -0.0032700205f, 0f, -0.003769583f, 0.0001988206f, -0.0021913181f,
        -0.0014168091f, -0.0008812684f, -0.0040853306f, 0f, -0.0052719047f, 0.0008590002f, -0.0110371174f, 0.0004154118f,
        -0.0251254969f, -0.0353373517f, -0.0140097458f, -0.1711076223f, 0.0489842544f, 0.3309279254f, 0.0343026178f, -0.0604277446f,
        0.015454646f, -0.0345425747f, -0.0060248587f, -0.0070391548f, -0.005330446f, -0.0013790324f, -0.0032383267f, 0f,
        -0.003753346f, 0.0002055787f, -0.0021874839f, -0.001402132f, -0.0008882067f, -0.0040717155f, 0f, -0.0052627682f,
        0.0008794322f, -0.0110447025f, 0.0005020907f, -0.0252452913f, -0.0352222317f, -0.0142964712f, -0.1719028795f, 0.0490551719f,
        0.3306428031f, 0.0341778252f, -0.0597368053f, 0.0155730522f, -0.034441094f, -0.0059001214f, -0.0070548152f, -0.0052716754f,
        -0.0013874435f, -0.0032064803f, 0f, -0.0037369562f, 0.0002123083f, -0.0021835338f, -0.0013873393f, -0.0008951159f,
        -0.0040578753f, 0f, -0.0052532584f, 0.0008998894f, -0.0110516808f, 0.0005891908f, -0.0253645883f, -0.0351051597f,
        -0.0145842105f, -0.1726977398f, 0.0491255631f, 0.3303539157f, 0.034052842f, -0.0590475609f, 0.0156902738f, -0.0343386673f,
        -0.0057757375f, -0.0070700018f, -0.0052128222f, -0.0013957373f, -0.0031744836f, 0f, -0.0037204147f, 0.0002190091f,
        -0.0021794684f, -0.0013724315f, -0.0009019955f, -0.0040438104f, 0f, -0.0052433746f, 0.0009203708f, -0.0110580495f,
        0.0006767098f, -0.0254833811f, -0.0349861313f, -0.0148729598f, -0.1734921866f, 0.0491954263f, 0.33006127f, 0.033927671f,
        -0.0583600221f, 0.0158063121f, -0.0342353029f, -0.005651711f, -0.0070847158f, -0.00515389f, -0.0014039137f, -0.0031423388f,
        0f, -0.0037037227f, 0.0002256807f, -0.0021752881f, -0.0013574093f, -0.0009088451f, -0.0040295213f, 0f,
        -0.0052331166f, 0.0009408752f, -0.0110638057f, 0.0007646456f, -0.0256016624f, -0.0348651418f, -0.0151627155f, -0.1742862035f,
        0.0492647598f, 0.3297648731f, 0.0338023147f, -0.0576741997f, 0.0159211685f, -0.0341310089f, -0.0055280459f, -0.0070989584f,
        -0.0050948823f, -0.0014119726f, -0.003110048f, 0f, -0.0036868814f, 0.0002323229f, -0.0021709934f, -0.0013422731f,
        -0.0009156643f, -0.0040150082f, 0f, -0.0052224838f, 0.0009614016f, -0.0110689464f, 0.000852996f, -0.0257194252f,
        -0.0347421869f, -0.0154534738f, -0.1750797739f, 0.0493335621f, 0.3294647317f, 0.033676776f, -0.0569901044f, 0.0160348443f,
        -0.0340257937f, -0.0054047462f, -0.0071127308f, -0.0050358029f, -0.0014199139f, -0.0030776134f, 0f, -0.003669892f,
        0.0002389354f, -0.0021665848f, -0.0013270235f, -0.0009224527f, -0.0040002715f, 0f, -0.0052114758f, 0.0009819489f,
        -0.0110734688f, 0.0009417586f, -0.0258366624f, -0.0346172621f, -0.0157452307f, -0.1758728812f, 0.0494018316f, 0.3291608531f,
        0.0335510574f, -0.0563077468f, 0.0161473409f, -0.0339196653f, -0.0052818157f, -0.0071260344f, -0.0049766551f, -0.0014277374f,
        -0.0030450372f, 0f, -0.0036527556f, 0.0002455178f, -0.0021620628f, -0.0013116611f, -0.0009292098f, -0.0039853116f,
        0f, -0.0052000922f, 0.001002516f, -0.0110773702f, 0.0010309311f, -0.0259533669f, -0.0344903631f, -0.0160379826f,
        -0.176665509f, 0.0494695666f, 0.3288532444f, 0.0334251618f, -0.0556271374f, 0.0162586596f, -0.0338126321f, -0.0051592585f,
        -0.0071388706f, -0.0049174428f, -0.001435443f, -0.0030123215f, 0f, -0.0036354734f, 0.00025207f, -0.002157428f,
        -0.0012961865f, -0.0009359353f, -0.0039701289f, 0f, -0.0051883327f, 0.0010231017f, -0.0110806476f, 0.0011205114f,
        -0.0260695316f, -0.0343614857f, -0.0163317255f, -0.1774576407f, 0.0495367656f, 0.3285419129f, 0.0332990919f, -0.0549482868f,
        0.016368802f, -0.0337047022f, -0.0050370783f, -0.0071512406f, -0.0048581693f, -0.0014430306f, -0.0029794686f, 0f,
        -0.0036180467f, 0.0002585915f, -0.0021526809f, -0.0012806003f, -0.0009426286f, -0.0039547239f, 0f, -0.0051761968f,
        0.001043705f, -0.0110832983f, 0.0012104969f, -0.0261851494f, -0.0342306253f, -0.0166264554f, -0.1782492597f, 0.049603427f,
        0.3282268658f, 0.0331728504f, -0.0542712053f, 0.0164777696f, -0.0335958839f, -0.0049152791f, -0.0071631457f, -0.0047988382f,
        -0.0014505002f, -0.0029464806f, 0f, -0.0036004766f, 0.0002650823f, -0.0021478221f, -0.001264903f, -0.0009492895f,
        -0.0039390969f, 0f, -0.0051636843f, 0.0010643249f, -0.0110853195f, 0.0013008854f, -0.0263002131f, -0.034097778f,
        -0.0169221685f, -0.1790403494f, 0.0496695492f, 0.3279081106f, 0.0330464399f, -0.0535959034f, 0.0165855637f, -0.0334861855f,
        -0.0047938646f, -0.0071745874f, -0.0047394532f, -0.0014578516f, -0.0029133597f, 0f, -0.0035827643f, 0.0002715418f,
        -0.0021428519f, -0.0012490953f, -0.0009559175f, -0.0039232485f, 0f, -0.0051507947f, 0.00108496f, -0.0110867085f,
        0.0013916744f, -0.0264147156f, -0.0339629395f, -0.0172188608f, -0.1798308933f, 0.0497351307f, 0.3275856548f, 0.0329198633f,
        -0.0529223913f, 0.0166921859f, -0.033375615f, -0.0046728386f, -0.0071855671f, -0.0046800177f, -0.0014650847f, -0.0028801082f,
        0f, -0.0035649111f, 0.0002779699f, -0.0021377711f, -0.0012331778f, -0.0009625121f, -0.003907179f, 0f,
        -0.0051375278f, 0.0011056095f, -0.0110874625f, 0.0014828615f, -0.0265286498f, -0.0338261057f, -0.0175165281f, -0.1806208748f,
        0.0498001699f, 0.327259506f, 0.0327931231f, -0.0522506792f, 0.0167976379f, -0.0332641808f, -0.0045522048f, -0.007196086f,
        -0.0046205352f, -0.0014721996f, -0.0028467282f, 0f, -0.0035469181f, 0.0002843663f, -0.0021325801f, -0.0012171511f,
        -0.0009690729f, -0.0038908889f, 0f, -0.0051238834f, 0.001126272f, -0.0110875788f, 0.0015744443f, -0.0266420086f,
        -0.0336872725f, -0.0178151666f, -0.1814102773f, 0.0498646654f, 0.3269296719f, 0.0326662223f, -0.0515807774f, 0.0169019212f,
        -0.0331518911f, -0.004431967f, -0.0072061457f, -0.0045610093f, -0.001479196f, -0.0028132218f, 0f, -0.0035287865f,
        0.0002907308f, -0.0021272795f, -0.0012010158f, -0.0009755996f, -0.0038743788f, 0f, -0.005109861f, 0.0011469465f,
        -0.0110870548f, 0.0016664204f, -0.0267547848f, -0.0335464358f, -0.018114772f, -0.1821990842f, 0.0499286156f, 0.3265961602f,
        0.0325391634f, -0.0509126959f, 0.0170050374f, -0.0330387542f, -0.0043121289f, -0.0072157476f, -0.0045014435f, -0.001486074f,
        -0.0027795914f, 0f, -0.0035105177f, 0.000297063f, -0.0021218699f, -0.0011847726f, -0.0009820917f, -0.003857649f,
        0f, -0.0050954606f, 0.0011676319f, -0.0110858877f, 0.0017587871f, -0.0268669712f, -0.0334035918f, -0.0184153404f,
        -0.1829872789f, 0.049992019f, 0.3262589788f, 0.0324119492f, -0.0502464448f, 0.0171069881f, -0.0329247782f, -0.0041926942f,
        -0.0072248931f, -0.0044418413f, -0.0014928334f, -0.0027458391f, 0f, -0.0034921128f, 0.0003033626f, -0.0021163518f,
        -0.0011684221f, -0.0009885489f, -0.0038407002f, 0f, -0.0050806818f, 0.001188327f, -0.011084075f, 0.001851542f,
        -0.0269785608f, -0.0332587363f, -0.0187168674f, -0.1837748449f, 0.0500548741f, 0.3259181356f, 0.0322845825f, -0.049582034f,
        0.0172077751f, -0.0328099714f, -0.0040736663f, -0.0072335836f, -0.0043822061f, -0.0014994743f, -0.002711967f, 0f,
        -0.0034735731f, 0.0003096295f, -0.0021107257f, -0.001151965f, -0.0009949706f, -0.0038235328f, 0f, -0.0050655245f,
        0.0012090307f, -0.0110816139f, 0.0019446825f, -0.0270895463f, -0.0331118656f, -0.0190193491f, -0.1845617655f, 0.0501171796f,
        0.3255736386f, 0.0321570659f, -0.0489194734f, 0.0173074f, -0.032694342f, -0.0039550491f, -0.0072418207f, -0.0043225415f,
        -0.0015059966f, -0.0026779775f, 0f, -0.0034548998f, 0.0003158633f, -0.0021049923f, -0.0011354018f, -0.0010013565f,
        -0.0038061474f, 0f, -0.0050499884f, 0.0012297418f, -0.0110785019f, 0.0020382061f, -0.0271999206f, -0.0329629759f,
        -0.019322781f, -0.1853480242f, 0.0501789338f, 0.325225496f, 0.0320294022f, -0.048258773f, 0.0174058647f, -0.0325778982f,
        -0.0038368459f, -0.0072496059f, -0.0042628509f, -0.0015124002f, -0.0026438726f, 0f, -0.0034360941f, 0.0003220637f,
        -0.0020991522f, -0.0011187334f, -0.0010077062f, -0.0037885445f, 0f, -0.0050340734f, 0.0012504592f, -0.0110747363f,
        0.0021321101f, -0.0273096765f, -0.0328120632f, -0.019627159f, -0.1861336043f, 0.0502401353f, 0.324873716f, 0.0319015942f,
        -0.0475999424f, 0.0175031707f, -0.0324606484f, -0.0037190604f, -0.0072569406f, -0.0042031378f, -0.0015186852f, -0.0026096547f,
        0f, -0.0034171573f, 0.0003282306f, -0.0020932058f, -0.0011019602f, -0.0010140193f, -0.0037707246f, 0f,
        -0.0050177794f, 0.0012711817f, -0.0110703147f, 0.002226392f, -0.0274188069f, -0.0326591239f, -0.0199324789f, -0.1869184892f,
        0.0503007828f, 0.3245183067f, 0.0317736444f, -0.0469429914f, 0.01759932f, -0.0323426007f, -0.0036016961f, -0.0072638265f,
        -0.0041434056f, -0.0015248514f, -0.0025753258f, 0f, -0.0033980907f, 0.0003343637f, -0.0020871539f, -0.0010850831f,
        -0.0010202953f, -0.0037526885f, 0f, -0.0050011062f, 0.0012919082f, -0.0110652345f, 0.002321049f, -0.0275273046f,
        -0.0325041543f, -0.0202387362f, -0.1877026623f, 0.0503608748f, 0.3241592765f, 0.0316455558f, -0.0462879295f, 0.0176943144f,
        -0.0322237633f, -0.0034847564f, -0.0072702649f, -0.0040836578f, -0.0015308989f, -0.0025408883f, 0f, -0.0033788955f,
        0.0003404626f, -0.0020809969f, -0.0010681027f, -0.0010265339f, -0.0037344365f, 0f, -0.0049840538f, 0.0013126376f,
        -0.0110594932f, 0.0024160784f, -0.0276351624f, -0.0323471508f, -0.0205459267f, -0.1884861071f, 0.0504204099f, 0.323796634f,
        0.0315173309f, -0.0456347665f, 0.0177881557f, -0.0321041445f, -0.0033682448f, -0.0072762576f, -0.0040238977f, -0.0015368277f,
        -0.0025063443f, 0f, -0.003359573f, 0.0003465272f, -0.0020747355f, -0.0010510197f, -0.0010327347f, -0.0037159694f,
        0f, -0.0049666219f, 0.0013333686f, -0.0110530882f, 0.0025114777f, -0.0277423732f, -0.0321881096f, -0.020854046f,
        -0.1892688069f, 0.0504793866f, 0.3234303876f, 0.0313889726f, -0.0449835118f, 0.0178808458f, -0.0319837525f, -0.0032521647f,
        -0.007281806f, -0.0039641289f, -0.0015426378f, -0.002471696f, 0f, -0.0033401244f, 0.0003525572f, -0.0020683703f
    };

    private static readonly float[] ReceivePulseShaperImaginary =
    {
        -0.0010505915f, -0.0011035107f, -0.0005259236f, -0.0037433748f, 0.0003859743f, -0.004765267f, 0.0019893681f, -0.0100295736f,
        0.0031365194f, -0.0231495307f, -0.0352727947f, -0.0144050466f, -0.1973709256f, 0f, 0.3190831345f, 0.0617512303f,
        -0.0415349018f, 0.0232689975f, -0.0278489297f, -0.0035885634f, -0.0056282268f, -0.0041674913f, -0.0007838146f, -0.0024673225f,
        0.0005855928f, -0.003197397f, 0.0005267651f, -0.0010538873f, -0.0010850596f, -0.0005290237f, -0.0037242437f, 0.0003914782f,
        -0.0047477509f, 0.0020198246f, -0.0100225461f, 0.0032521647f, -0.0232375564f, -0.0350931358f, -0.014616029f, -0.1981821735f,
        0f, 0.3187143037f, 0.0614971632f, -0.0409283696f, 0.0233860306f, -0.0277423732f, -0.0034567525f, -0.0056318297f,
        -0.0041036864f, -0.0007866356f, -0.0024320378f, 0.0005885517f, -0.0031784306f, 0.0005355213f, -0.0010571305f, -0.0010665012f,
        -0.0005321041f, -0.0037048966f, 0.0003969659f, -0.0047298694f, 0.0020502783f, -0.0100149094f, 0.0033682448f, -0.0233250263f,
        -0.0349112212f, -0.0148276345f, -0.198992596f, 0f, 0.3183419391f, 0.0612428487f, -0.0403236515f, 0.0235015807f,
        -0.0276351624f, -0.0033254467f, -0.0056350932f, -0.004039883f, -0.0007893966f, -0.0023966543f, 0.0005914766f, -0.0031593466f,
        0.0005442255f, -0.0010603209f, -0.0010478363f, -0.0005351645f, -0.0036853342f, 0.0004024372f, -0.0047116224f, 0.0020807276f,
        -0.0100066612f, 0.0034847564f, -0.0234119345f, -0.0347270473f, -0.01503986f, -0.1998021758f, 0f, 0.3179660495f,
        0.060988292f, -0.0397207562f, 0.0236156505f, -0.0275273046f, -0.0031946498f, -0.0056380186f, -0.0039760847f, -0.0007920974f,
        -0.0023611741f, 0.0005943675f, -0.0031401461f, 0.0005528775f, -0.001063458f, -0.0010290656f, -0.0005382047f, -0.0036655571f,
        0.0004078915f, -0.00469301f, 0.0021111706f, -0.0099977994f, 0.0036016961f, -0.0234982749f, -0.0345406103f, -0.0152527025f,
        -0.2006108958f, 0f, 0.3175866436f, 0.0607334987f, -0.0391196924f, 0.0237282425f, -0.0274188069f, -0.0030643657f,
        -0.0056406071f, -0.0039122951f, -0.0007947382f, -0.0023255995f, 0.0005972241f, -0.0031208304f, 0.0005614768f, -0.0010665416f,
        -0.00101019f, -0.0005412246f, -0.0036455662f, 0.0004133287f, -0.0046740323f, 0.0021416057f, -0.0099883219f, 0.0037190604f,
        -0.0235840415f, -0.0343519067f, -0.0154661589f, -0.2014187385f, 0f, 0.3172037305f, 0.0604784741f, -0.0385204684f,
        0.0238393594f, -0.0273096765f, -0.0029345978f, -0.00564286f, -0.0038485177f, -0.0007973189f, -0.0022899328f, 0.0006000465f,
        -0.0031014008f, 0.0005700231f, -0.0010695714f, -0.0009912103f, -0.0005442238f, -0.0036253619f, 0.0004187483f, -0.0046546892f,
        0.002172031f, -0.0099782265f, 0.0038368459f, -0.0236692285f, -0.0341609328f, -0.0156802259f, -0.2022256869f, 0f,
        0.316817319f, 0.0602232237f, -0.0379230929f, 0.0239490039f, -0.0271999206f, -0.00280535f, -0.0056447786f, -0.003784756f,
        -0.0007998396f, -0.0022541761f, 0.0006028345f, -0.0030818584f, 0.0005785161f, -0.0010725472f, -0.0009721272f, -0.0005472024f,
        -0.0036049452f, 0.00042415f, -0.0046349809f, 0.0022024449f, -0.0099675111f, 0.003955049f, -0.0237538299f, -0.0339676851f,
        -0.0158949005f, -0.2030317237f, 0f, 0.3164274182f, 0.0599677528f, -0.0373275742f, 0.0240571786f, -0.0270895463f,
        -0.0026766259f, -0.0056463643f, -0.0037210138f, -0.0008023003f, -0.0022183317f, 0.0006055881f, -0.0030622046f, 0.0005869555f,
        -0.0010754685f, -0.0009529415f, -0.0005501599f, -0.0035843166f, 0.0004295334f, -0.0046149074f, 0.0022328455f, -0.0099561737f,
        0.0040736663f, -0.0238378395f, -0.0337721601f, -0.0161101794f, -0.2038368317f, 0f, 0.3160340373f, 0.0597120668f,
        -0.0367339206f, 0.0241638864f, -0.0269785608f, -0.0025484289f, -0.0056476183f, -0.0036572944f, -0.0008047009f, -0.0021824019f,
        0.0006083072f, -0.0030424405f, 0.0005953408f, -0.0010783351f, -0.0009336541f, -0.0005530963f, -0.003563477f, 0.0004348982f,
        -0.0045944688f, 0.0022632312f, -0.0099442122f, 0.0041926941f, -0.0239212516f, -0.0335743545f, -0.0163260596f, -0.2046409936f,
        0f, 0.3156371855f, 0.0594561711f, -0.0361421404f, 0.02426913f, -0.0268669712f, -0.0024207628f, -0.0056485419f,
        -0.0035936014f, -0.0008070417f, -0.0021463888f, 0.0006109916f, -0.0030225675f, 0.0006036718f, -0.0010811467f, -0.0009142658f,
        -0.0005560114f, -0.0035424271f, 0.000440244f, -0.0045736654f, 0.0022936f, -0.0099316245f, 0.0043121289f, -0.02400406f,
        -0.0333742649f, -0.0165425377f, -0.2054441923f, 0f, 0.3152368721f, 0.0592000711f, -0.0355522418f, 0.0243729123f,
        -0.0267547848f, -0.0022936309f, -0.0056491366f, -0.0035299384f, -0.0008093225f, -0.0021102947f, 0.0006136413f, -0.0030025869f,
        0.0006119481f, -0.0010839031f, -0.0008947773f, -0.0005589049f, -0.0035211675f, 0.0004455706f, -0.0045524971f, 0.0023239503f,
        -0.0099184087f, 0.004431967f, -0.0240862588f, -0.033171888f, -0.0167596106f, -0.2062464104f, 0f, 0.3148331065f,
        0.0589437722f, -0.034964233f, 0.0244752362f, -0.0266420086f, -0.0021670367f, -0.0056494036f, -0.0034663087f, -0.0008115434f,
        -0.0020741218f, 0.0006162563f, -0.0029824998f, 0.0006201695f, -0.0010866038f, -0.0008751896f, -0.0005617766f, -0.0034996992f,
        0.0004508774f, -0.0045309644f, 0.0023542803f, -0.0099045627f, 0.0045522048f, -0.024167842f, -0.0329672206f, -0.0169772748f,
        -0.2070476309f, 0f, 0.3144258981f, 0.0586872798f, -0.034378122f, 0.0245761044f, -0.0265286498f, -0.0020409838f,
        -0.0056493443f, -0.003402716f, -0.0008137045f, -0.0020378723f, 0.0006188364f, -0.0029623075f, 0.0006283355f, -0.0010892488f,
        -0.0008555035f, -0.0005646264f, -0.003478023f, 0.0004561643f, -0.0045090672f, 0.0023845881f, -0.0098900846f, 0.0046728385f,
        -0.0242488037f, -0.0327602594f, -0.0171955273f, -0.2078478365f, 0f, 0.3140152564f, 0.0584305992f, -0.0337939169f,
        0.02467552f, -0.0264147156f, -0.0019154755f, -0.0056489601f, -0.0033391636f, -0.0008158057f, -0.0020015486f, 0.0006213815f,
        -0.0029420114f, 0.0006364459f, -0.0010918376f, -0.0008357197f, -0.0005674541f, -0.0034561395f, 0.0004614308f, -0.0044868058f,
        0.002414872f, -0.0098749725f, 0.0047938646f, -0.0243291378f, -0.0325510015f, -0.0174143647f, -0.20864701f, 0f,
        0.3136011912f, 0.0581737359f, -0.0332116256f, 0.0247734859f, -0.0263002131f, -0.0017905151f, -0.0056482524f, -0.0032756551f,
        -0.0008178472f, -0.0019651527f, 0.0006238917f, -0.0029216127f, 0.0006445003f, -0.00109437f, -0.0008158393f, -0.0005702595f,
        -0.0034340496f, 0.0004666767f, -0.0044641806f, 0.0024451302f, -0.0098592243f, 0.0049152791f, -0.0244088384f, -0.0323394436f,
        -0.0176337836f, -0.2094451343f, 0f, 0.313183712f, 0.0579166953f, -0.032631256f, 0.0248700051f, -0.0261851494f,
        -0.0016661061f, -0.0056472225f, -0.0032121938f, -0.000819829f, -0.0019286871f, 0.0006263667f, -0.0029011126f, 0.0006524984f,
        -0.0010968457f, -0.0007958629f, -0.0005730423f, -0.0034117542f, 0.0004719015f, -0.0044411916f, 0.0024753609f, -0.0098428383f,
        0.0050370783f, -0.0244878996f, -0.0321255828f, -0.0178537807f, -0.2102421921f, 0f, 0.3127628287f, 0.0576594827f,
        -0.0320528159f, 0.0249650807f, -0.0260695316f, -0.0015422516f, -0.0056458719f, -0.0031487833f, -0.0008217512f, -0.0018921537f,
        0.0006288066f, -0.0028805125f, 0.00066044f, -0.0010992645f, -0.0007757916f, -0.0005758024f, -0.0033892541f, 0.0004771049f,
        -0.0044178393f, 0.0025055622f, -0.0098258124f, 0.0051592585f, -0.0245663152f, -0.0319094161f, -0.0180743526f, -0.2110381663f,
        0f, 0.312338551f, 0.0574021035f, -0.0314763131f, 0.0250587157f, -0.0259533669f, -0.001418955f, -0.005644202f,
        -0.0030854268f, -0.0008236137f, -0.001855555f, 0.0006312113f, -0.0028598137f, 0.0006683247f, -0.001101626f, -0.0007556261f,
        -0.0005785397f, -0.0033665502f, 0.0004822865f, -0.0043941238f, 0.0025357325f, -0.009808145f, 0.0052818157f, -0.0246440794f,
        -0.0316909408f, -0.018295496f, -0.2118330397f, 0f, 0.311910889f, 0.0571445631f, -0.0309017552f, 0.0251509132f,
        -0.0258366624f, -0.0012962195f, -0.0056422142f, -0.0030221279f, -0.0008254167f, -0.001818893f, 0.0006335808f, -0.0028390174f,
        0.0006761523f, -0.0011039301f, -0.0007353674f, -0.0005812538f, -0.0033436432f, 0.0004874461f, -0.0043700456f, 0.0025658697f,
        -0.0097898341f, 0.0054047462f, -0.0247211861f, -0.0314701538f, -0.0185172074f, -0.2126267953f, 0f, 0.3114798525f,
        0.0568868669f, -0.0303291498f, 0.0252416763f, -0.0257194252f, -0.0011740483f, -0.0056399099f, -0.0029588899f, -0.0008271602f,
        -0.0017821701f, 0.0006359148f, -0.0028181249f, 0.0006839223f, -0.0011061764f, -0.0007150163f, -0.0005839447f, -0.0033205342f,
        0.0004925832f, -0.004345605f, 0.0025959722f, -0.0097708779f, 0.0055280459f, -0.0247976295f, -0.0312470525f, -0.0187394835f,
        -0.2134194158f, 0f, 0.3110454518f, 0.0566290203f, -0.0297585047f, 0.0253310083f, -0.0256016624f, -0.0010524444f,
        -0.0056372905f, -0.0028957161f, -0.0008288442f, -0.0017453885f, 0.0006382135f, -0.0027971375f, 0.0006916346f, -0.0011083646f,
        -0.0006945737f, -0.0005866121f, -0.0032972239f, 0.0004976976f, -0.0043208022f, 0.0026260381f, -0.0097512747f, 0.005651711f,
        -0.0248734035f, -0.0310216342f, -0.0189623207f, -0.2142108841f, 0f, 0.3106076969f, 0.0563710286f, -0.0291898271f,
        0.0254189123f, -0.0254833811f, -0.0009314112f, -0.0056343576f, -0.00283261f, -0.000830469f, -0.0017085503f, 0.0006404767f,
        -0.0027760566f, 0.0006992888f, -0.0011104946f, -0.0006740406f, -0.0005892558f, -0.0032737133f, 0.0005027888f, -0.0042956378f,
        0.0026560656f, -0.0097310227f, 0.0057757375f, -0.0249485021f, -0.0307938961f, -0.0191857156f, -0.2150011831f, 0f,
        0.3101665981f, 0.0561128971f, -0.0286231246f, 0.0255053915f, -0.0253645884f, -0.0008109515f, -0.0056311126f, -0.0027695748f,
        -0.0008320344f, -0.0016716577f, 0.0006427043f, -0.0027548833f, 0.0007068847f, -0.0011125661f, -0.0006534178f, -0.0005918757f,
        -0.0032500034f, 0.0005078566f, -0.004270112f, 0.0026860528f, -0.0097101202f, 0.0059001214f, -0.0250229195f, -0.0305638358f,
        -0.0194096647f, -0.2157902957f, 0f, 0.3097221657f, 0.0558546314f, -0.0280584045f, 0.0255904493f, -0.0252452913f,
        -0.0006910686f, -0.005627557f, -0.002706614f, -0.0008335406f, -0.0016347131f, 0.0006448964f, -0.0027336191f, 0.0007144219f,
        -0.0011145787f, -0.0006327063f, -0.0005944716f, -0.003226095f, 0.0005129006f, -0.0042442254f, 0.0027159979f, -0.0096885654f,
        0.0060248587f, -0.0250966496f, -0.0303314506f, -0.0196341645f, -0.2165782048f, 0f, 0.30927441f, 0.0555962367f,
        -0.0274956741f, 0.0256740889f, -0.0251254969f, -0.0005717653f, -0.0056236922f, -0.0026437308f, -0.0008349877f, -0.0015977186f,
        0.0006470528f, -0.0027122652f, 0.0007219003f, -0.0011165323f, -0.000611907f, -0.0005970433f, -0.003201989f, 0.0005179204f,
        -0.0042179783f, 0.0027458991f, -0.0096663566f, 0.0061499454f, -0.0251696864f, -0.0300967381f, -0.0198592114f, -0.2173648933f,
        0f, 0.3088233415f, 0.0553377183f, -0.0269349407f, 0.0257563136f, -0.0250052123f, -0.0004530446f, -0.0056195197f,
        -0.0025809285f, -0.0008363757f, -0.0015606764f, 0.0006491735f, -0.0026908229f, 0.0007293194f, -0.0011184266f, -0.0005910207f,
        -0.0005995906f, -0.0031776865f, 0.0005229157f, -0.0041913713f, 0.0027757545f, -0.0096434922f, 0.0062753773f, -0.0252420241f,
        -0.0298596959f, -0.020084802f, -0.2181503442f, 0f, 0.3083689708f, 0.0550790818f, -0.0263762113f, 0.0258371269f,
        -0.0248844446f, -0.0003349096f, -0.005615041f, -0.0025182104f, -0.0008377047f, -0.0015235887f, 0.0006512585f, -0.0026692935f,
        0.0007366792f, -0.0011202614f, -0.0005700485f, -0.0006021133f, -0.0031531884f, 0.0005278862f, -0.0041644048f, 0.0028055622f,
        -0.0096199706f, 0.0064011503f, -0.0253136566f, -0.0296203216f, -0.0203109325f, -0.2189345404f, 0f, 0.3079113083f,
        0.0548203323f, -0.0258194931f, 0.025916532f, -0.0247632008f, -0.000217363f, -0.0056102577f, -0.0024555798f, -0.0008389749f,
        -0.0014864577f, 0.0006533078f, -0.0026476784f, 0.0007439791f, -0.0011220364f, -0.0005489913f, -0.0006046113f, -0.0031284958f,
        0.0005328315f, -0.0041370792f, 0.0028353205f, -0.0095957901f, 0.0065272603f, -0.025384578f, -0.0293786129f, -0.0205375996f,
        -0.2197174648f, 0f, 0.3074503649f, 0.0545614753f, -0.0252647931f, 0.0259945325f, -0.0246414879f, -0.0001004078f,
        -0.0056051713f, -0.00239304f, -0.0008401862f, -0.0014492856f, 0.0006553212f, -0.0026259788f, 0.0007512192f, -0.0011237514f,
        -0.00052785f, -0.0006070844f, -0.0031036095f, 0.0005377513f, -0.0041093953f, 0.0028650275f, -0.0095709492f, 0.0066537032f,
        -0.0254547823f, -0.0291345676f, -0.0207647994f, -0.2204991004f, 0f, 0.3069861513f, 0.0543025161f, -0.0247121183f,
        0.0260711319f, -0.024519313f, 1.59533e-05f, -0.0055997832f, -0.0023305941f, -0.0008413389f, -0.0014120747f, 0.0006572987f,
        -0.0026041961f, 0.0007583989f, -0.0011254061f, -0.0005066256f, -0.0006095323f, -0.0030785306f, 0.0005426452f, -0.0040813534f,
        0.0028946812f, -0.0095454463f, 0.0067804747f, -0.0255242637f, -0.0288881835f, -0.0209925285f, -0.2212794303f, 0f,
        0.3065186782f, 0.0540434601f, -0.0241614756f, 0.0261463335f, -0.0243966831f, 0.0001317174f, -0.0055940951f, -0.0022682454f,
        -0.0008424329f, -0.001374827f, 0.0006592403f, -0.0025823316f, 0.0007655182f, -0.0011270003f, -0.000485319f, -0.000611955f,
        -0.0030532601f, 0.0005475129f, -0.0040529541f, 0.0029242799f, -0.0095192797f, 0.0069075706f, -0.0255930162f, -0.0286394586f,
        -0.0212207831f, -0.2220584374f, 0f, 0.3060479566f, 0.0537843125f, -0.0236128717f, 0.0262201409f, -0.0242736053f,
        0.0002468817f, -0.0055881084f, -0.002205997f, -0.0008434684f, -0.0013375449f, 0.0006611459f, -0.0025603865f, 0.0007725768f,
        -0.0011285338f, -0.0004639312f, -0.0006143522f, -0.0030277991f, 0.0005523541f, -0.0040241981f, 0.0029538216f, -0.0094924482f,
        0.0070349866f, -0.0256610338f, -0.0283883908f, -0.0214495595f, -0.2228361046f, 0f, 0.3055739975f, 0.0535250789f,
        -0.0230663136f, 0.0262925577f, -0.0241500864f, 0.0003614437f, -0.0055818247f, -0.0021438523f, -0.0008444455f, -0.0013002304f,
        0.0006630155f, -0.0025383623f, 0.0007795743f, -0.0011300063f, -0.0004424633f, -0.0006167238f, -0.0030021485f, 0.0005571684f,
        -0.003995086f, 0.0029833045f, -0.00946495f, 0.0071627185f, -0.0257283106f, -0.0281349779f, -0.0216788542f, -0.2236124152f,
        0f, 0.3050968118f, 0.0532657643f, -0.0225218077f, 0.0263635875f, -0.0240261336f, 0.0004754004f, -0.0055752457f,
        -0.0020818144f, -0.0008453642f, -0.0012628859f, 0.0006648491f, -0.0025162602f, 0.0007865106f, -0.0011314176f, -0.000420916f,
        -0.0006190695f, -0.0029763095f, 0.0005619554f, -0.0039656183f, 0.0030127267f, -0.0094367839f, 0.0072907619f, -0.0257948406f,
        -0.0278792183f, -0.0219086633f, -0.2243873522f, 0f, 0.3046164107f, 0.0530063743f, -0.0219793608f, 0.0264332338f,
        -0.0239017537f, 0.0005887495f, -0.0055683728f, -0.0020198864f, -0.0008462248f, -0.0012255133f, 0.0006666467f, -0.0024940816f,
        0.0007933854f, -0.0011327676f, -0.0003992906f, -0.0006213893f, -0.0029502832f, 0.000566715f, -0.0039357958f, 0.0030420864f,
        -0.0094079482f, 0.0074191125f, -0.0258606181f, -0.0276211099f, -0.0221389831f, -0.2251608985f, 0f, 0.3041328054f,
        0.0527469141f, -0.0214389794f, 0.0265015004f, -0.0237769538f, 0.0007014881f, -0.0055612076f, -0.0019580714f, -0.0008470272f,
        -0.001188115f, 0.0006684082f, -0.0024718278f, 0.0008001985f, -0.0011340559f, -0.0003775879f, -0.0006236829f, -0.0029240705f,
        0.0005714466f, -0.003905619f, 0.0030713817f, -0.0093784417f, 0.0075477659f, -0.0259256369f, -0.027360651f, -0.02236981f,
        -0.2259330372f, 0f, 0.3036460071f, 0.052487389f, -0.02090067f, 0.0265683908f, -0.0236517409f, 0.0008136138f,
        -0.0055537517f, -0.0018963727f, -0.0008477717f, -0.0011506931f, 0.0006701335f, -0.0024495f, 0.0008069497f, -0.0011352824f,
        -0.0003558089f, -0.0006259502f, -0.0028976727f, 0.00057615f, -0.0038750888f, 0.0031006106f, -0.009348263f, 0.0076767177f,
        -0.0259898913f, -0.0270978398f, -0.0226011401f, -0.2267037516f, 0f, 0.3031560272f, 0.0522278043f, -0.0203644391f,
        0.0266339089f, -0.0235261219f, 0.000925124f, -0.0055460068f, -0.0018347932f, -0.0008484582f, -0.0011132498f, 0.0006718227f,
        -0.0024270997f, 0.0008136387f, -0.0011364468f, -0.0003339546f, -0.0006281909f, -0.0028710908f, 0.0005808248f, -0.0038442057f,
        0.0031297713f, -0.0093174106f, 0.0078059634f, -0.0260533754f, -0.0268326746f, -0.0228329695f, -0.2274730247f, 0f,
        0.302662877f, 0.0519681655f, -0.0198302929f, 0.0266980584f, -0.0234001037f, 0.0010360161f, -0.0055379744f, -0.0017733362f,
        -0.000849087f, -0.0010757872f, 0.0006734757f, -0.0024046282f, 0.0008202653f, -0.001137549f, -0.0003120261f, -0.0006304051f,
        -0.0028443259f, 0.0005854707f, -0.0038129707f, 0.0031588619f, -0.0092858833f, 0.0079354987f, -0.0261160831f, -0.0265651539f,
        -0.0230652947f, -0.2282408396f, 0f, 0.302166568f, 0.0517084777f, -0.0192982378f, 0.0267608429f, -0.0232736933f,
        0.0011462878f, -0.0055296561f, -0.0017120047f, -0.0008496582f, -0.0010383075f, 0.0006750925f, -0.0023820868f, 0.0008268292f,
        -0.0011385887f, -0.0002900244f, -0.0006325923f, -0.0028173791f, 0.0005900874f, -0.0037813843f, 0.0031878805f, -0.0092536797f,
        0.008065319f, -0.0261780087f, -0.0262952761f, -0.0232981116f, -0.2290071795f, 0f, 0.3016671118f, 0.0514487462f,
        -0.0187682801f, 0.0268222664f, -0.0231468977f, 0.0012559367f, -0.0055210536f, -0.0016508017f, -0.0008501718f, -0.0010008128f,
        0.0006766731f, -0.0023594768f, 0.0008333304f, -0.0011395657f, -0.0002679505f, -0.0006347526f, -0.0027902517f, 0.0005946745f,
        -0.0037494474f, 0.0032168253f, -0.0092207986f, 0.0081954197f, -0.0262391462f, -0.0260230397f, -0.0235314164f, -0.2297720276f,
        0f, 0.3011645199f, 0.0511889764f, -0.0182404257f, 0.0268823327f, -0.0230197236f, 0.0013649602f, -0.0055121684f,
        -0.0015897303f, -0.000850628f, -0.0009633054f, 0.0006782175f, -0.0023367996f, 0.0008397684f, -0.0011404799f, -0.0002458054f,
        -0.0006368857f, -0.0027629448f, 0.0005992318f, -0.0037171608f, 0.0032456942f, -0.0091872387f, 0.0083257964f, -0.0262994898f,
        -0.0257484432f, -0.0237652054f, -0.2305353671f, 0f, 0.300658804f, 0.0509291736f, -0.0177146809f, 0.0269410455f,
        -0.022892178f, 0.0014733561f, -0.0055030023f, -0.0015287935f, -0.000851027f, -0.0009257874f, 0.0006797256f, -0.0023140564f,
        0.0008461432f, -0.001141331f, -0.0002235901f, -0.0006389915f, -0.0027354594f, 0.0006037588f, -0.0036845253f, 0.0032744854f,
        -0.0091529987f, 0.0084564445f, -0.0263590336f, -0.0254714853f, -0.0239994745f, -0.2312971813f, 0f, 0.3001499759f,
        0.050669343f, -0.0171910516f, 0.0269984089f, -0.0227642679f, 0.001581122f, -0.0054935568f, -0.0014679944f, -0.0008513688f,
        -0.0008882608f, 0.0006811974f, -0.0022912487f, 0.0008524546f, -0.0011421188f, -0.0002013058f, -0.0006410697f, -0.0027077969f,
        0.0006082552f, -0.0036515417f, 0.0033031971f, -0.0091180776f, 0.0085873593f, -0.0264177717f, -0.0251921647f, -0.0242342199f,
        -0.2320574533f, 0f, 0.2996380473f, 0.0504094899f, -0.0166695438f, 0.0270544266f, -0.022636f, 0.0016882556f,
        -0.0054838337f, -0.0014073359f, -0.0008516537f, -0.0008507279f, 0.000682633f, -0.0022683777f, 0.0008587023f, -0.0011428431f,
        -0.0001789534f, -0.0006431203f, -0.0026799585f, 0.0006127207f, -0.0036182109f, 0.0033318273f, -0.009082474f, 0.0087185362f,
        -0.0264756982f, -0.0249104801f, -0.0244694377f, -0.2328161665f, 0f, 0.2991230302f, 0.0501496195f, -0.0161501634f,
        0.0271091028f, -0.0225073813f, 0.0017947547f, -0.0054738345f, -0.0013468211f, -0.0008518816f, -0.0008131908f, 0.0006840322f,
        -0.0022454449f, 0.0008648861f, -0.0011435038f, -0.000156534f, -0.0006451431f, -0.0026519452f, 0.000617155f, -0.0035845338f,
        0.003360374f, -0.009046187f, 0.0088499705f, -0.0265328074f, -0.0246264304f, -0.024705124f, -0.233573304f, 0f,
        0.2986049365f, 0.0498897372f, -0.0156329161f, 0.0271624412f, -0.0223784186f, 0.0019006171f, -0.005463561f, -0.0012864528f,
        -0.0008520528f, -0.0007756516f, 0.0006853952f, -0.0022224515f, 0.0008710059f, -0.0011441007f, -0.0001340486f, -0.0006471379f,
        -0.0026237584f, 0.0006215578f, -0.0035505114f, 0.0033888354f, -0.0090092152f, 0.0089816577f, -0.0265890933f, -0.0243400145f,
        -0.0249412747f, -0.2343288494f, 0f, 0.2980837782f, 0.0496298482f, -0.0151178078f, 0.027214446f, -0.0222491188f,
        0.0020058406f, -0.0054530148f, -0.001226234f, -0.0008521675f, -0.0007381125f, 0.0006867218f, -0.0021993988f, 0.0008770614f,
        -0.0011446335f, -0.0001114984f, -0.0006491046f, -0.0025953992f, 0.0006259287f, -0.0035161444f, 0.0034172096f, -0.0089715577f,
        0.0091135929f, -0.0266445501f, -0.0240512313f, -0.0251778859f, -0.2350827858f, 0f, 0.2975595675f, 0.0493699577f,
        -0.0146048441f, 0.0272651212f, -0.0221194886f, 0.0021104231f, -0.0054421976f, -0.0011661677f, -0.0008522257f, -0.0007005755f,
        0.0006880121f, -0.0021762883f, 0.0008830524f, -0.0011451022f, -8.88843e-05f, -0.0006510429f, -0.002566869f, 0.0006302674f,
        -0.0034814339f, 0.0034454947f, -0.0089332135f, 0.0092457713f, -0.026699172f, -0.0237600797f, -0.0254149536f, -0.2358350966f,
        0f, 0.2970323163f, 0.049110071f, -0.0140940305f, 0.0273144708f, -0.0219895349f, 0.0022143624f, -0.005431111f,
        -0.0011062567f, -0.0008522276f, -0.0006630428f, 0.0006892661f, -0.0021531212f, 0.0008889789f, -0.0011455065f, -6.62075e-05f,
        -0.0006529528f, -0.0025381689f, 0.0006345735f, -0.0034463809f, 0.0034736886f, -0.0088941813f, 0.0093781883f, -0.0267529532f,
        -0.023466559f, -0.0256524738f, -0.2365857653f, 0f, 0.2965020369f, 0.0488501934f, -0.0135853727f, 0.027362499f,
        -0.0218592646f, 0.0023176565f, -0.0054197569f, -0.0010465039f, -0.0008521733f, -0.0006255165f, 0.0006904838f, -0.0021298989f,
        0.0008948406f, -0.0011458462f, -4.3469e-05f, -0.0006548341f, -0.0025093002f, 0.0006388468f, -0.0034109862f, 0.0035017896f,
        -0.0088544604f, 0.009510839f, -0.0268058878f, -0.0231706682f, -0.0258904424f, -0.2373347753f, 0f, 0.2959687417f,
        0.04859033f, -0.0130788761f, 0.0274092097f, -0.0217286842f, 0.0024203033f, -0.0054081368f, -0.0009869123f, -0.0008520631f,
        -0.0005879987f, 0.0006916652f, -0.0021066227f, 0.0009006373f, -0.0011461212f, -2.067e-05f, -0.0006566867f, -0.0024802643f,
        0.0006430869f, -0.0033752511f, 0.0035297957f, -0.0088140496f, 0.0096437186f, -0.0268579701f, -0.0228724066f, -0.0261288555f,
        -0.2380821099f, 0f, 0.295432443f, 0.0483304861f, -0.012574546f, 0.0274546073f, -0.0215978008f, 0.0025223009f,
        -0.0053962525f, -0.0009274847f, -0.000851897f, -0.0005504915f, 0.0006928102f, -0.002083294f, 0.0009063689f, -0.0011463314f,
        2.1885e-06f, -0.0006585103f, -0.0024510624f, 0.0006472935f, -0.0033391764f, 0.0035577049f, -0.008772948f, 0.0097768222f,
        -0.0269091942f, -0.0225717734f, -0.0263677089f, -0.2388277526f, 0f, 0.2948931531f, 0.0480706669f, -0.0120723879f,
        0.0274986958f, -0.021466621f, 0.0026236473f, -0.0053841057f, -0.0008682239f, -0.0008516753f, -0.000512997f, 0.000693919f,
        -0.0020599141f, 0.0009120351f, -0.0011464764f, 2.51054e-05f, -0.0006603048f, -0.0024216958f, 0.0006514663f, -0.0033027632f,
        0.0035855154f, -0.0087311547f, 0.009910145f, -0.0269595544f, -0.0222687679f, -0.0266069985f, -0.239571687f, 0f,
        0.2943508846f, 0.0478108775f, -0.011572407f, 0.0275414794f, -0.0213351515f, 0.0027243405f, -0.0053716982f, -0.0008091327f,
        -0.000851398f, -0.0004755173f, 0.0006949914f, -0.0020364844f, 0.0009176359f, -0.0011465563f, 4.80796e-05f, -0.0006620701f,
        -0.0023921658f, 0.000655605f, -0.0032660126f, 0.0036132252f, -0.0086886688f, 0.010043682f, -0.0270090449f, -0.0219633896f,
        -0.0268467203f, -0.2403138965f, 0f, 0.29380565f, 0.0475511233f, -0.0110746085f, 0.0275829625f, -0.0212033992f,
        0.0028243786f, -0.0053590315f, -0.000750214f, -0.0008510654f, -0.0004380545f, 0.0006960276f, -0.0020130061f, 0.0009231711f,
        -0.0011465709f, 7.111e-05f, -0.0006638061f, -0.0023624737f, 0.0006597092f, -0.0032289258f, 0.0036408324f, -0.0086454895f,
        0.0101774283f, -0.0270576598f, -0.0216556379f, -0.0270868701f, -0.2410543647f, 0f, 0.2932574618f, 0.0472914095f,
        -0.0105789975f, 0.0276231491f, -0.0210713706f, 0.0029237599f, -0.0053461076f, -0.0006914705f, -0.0008506775f, -0.0004006107f,
        0.0006970274f, -0.0019894807f, 0.0009286405f, -0.0011465199f, 9.41954e-05f, -0.0006655126f, -0.0023326209f, 0.0006637786f,
        -0.0031915037f, 0.0036683351f, -0.0086016158f, 0.0103113789f, -0.0271053936f, -0.0213455123f, -0.0273274438f, -0.2417930752f,
        0f, 0.2927063328f, 0.047031741f, -0.010085579f, 0.0276620437f, -0.0209390727f, 0.0030224825f, -0.0053329281f,
        -0.0006329049f, -0.0008502347f, -0.0003631879f, 0.000697991f, -0.0019659094f, 0.000934044f, -0.0011464033f, 0.0001173348f,
        -0.0006671893f, -0.0023026088f, 0.0006678129f, -0.0031537477f, 0.0036957313f, -0.0085570471f, 0.0104455288f, -0.0271522403f,
        -0.0210330125f, -0.0275684372f, -0.2425300116f, 0f, 0.2921522758f, 0.0467721233f, -0.0095943581f, 0.0276996504f,
        -0.0208065119f, 0.0031205445f, -0.0053194948f, -0.0005745202f, -0.000849737f, -0.0003257881f, 0.0006989183f, -0.0019422936f,
        0.0009393814f, -0.0011462208f, 0.000140527f, -0.0006688363f, -0.0022724386f, 0.0006718118f, -0.0031156587f, 0.0037230191f,
        -0.0085117825f, 0.0105798731f, -0.0271981943f, -0.0207181381f, -0.0278098462f, -0.2432651575f, 0f, 0.2915953033f,
        0.0465125614f, -0.0091053396f, 0.0277359738f, -0.020673695f, 0.0032179443f, -0.0053058094f, -0.0005163189f, -0.0008491846f,
        -0.0002884136f, 0.0006998094f, -0.0019186347f, 0.0009446525f, -0.0011459724f, 0.000163771f, -0.0006704533f, -0.0022421118f,
        0.000675775f, -0.0030772381f, 0.0037501965f, -0.0084658212f, 0.0107144066f, -0.0272432498f, -0.0204008889f, -0.0280516665f,
        -0.2439984964f, 0f, 0.2910354284f, 0.0462530605f, -0.0086185284f, 0.027771018f, -0.0205406287f, 0.0033146802f,
        -0.0052918737f, -0.0004583037f, -0.0008485777f, -0.0002510662f, 0.0007006643f, -0.0018949339f, 0.0009498574f, -0.0011456579f,
        0.0001870654f, -0.0006720403f, -0.0022116298f, 0.0006797022f, -0.0030384869f, 0.0037772617f, -0.0084191626f, 0.0108491242f,
        -0.0272874011f, -0.0200812646f, -0.028293894f, -0.2447300123f, 0f, 0.290472664f, 0.0459936258f, -0.0081339293f,
        0.0278047875f, -0.0204073196f, 0.0034107504f, -0.0052776894f, -0.0004004775f, -0.0008479165f, -0.0002137482f, 0.0007014829f,
        -0.0018711927f, 0.0009549957f, -0.0011452772f, 0.0002104094f, -0.000673597f, -0.0021809939f, 0.0006835929f, -0.0029994065f,
        0.0038042127f, -0.0083718059f, 0.010984021f, -0.0273306425f, -0.019759265f, -0.0285365244f, -0.2454596887f, 0f,
        0.289907023f, 0.0457342624f, -0.0076515471f, 0.0278372867f, -0.0202737743f, 0.0035061533f, -0.0052632584f, -0.0003428428f,
        -0.0008472012f, -0.0001764614f, 0.0007022654f, -0.0018474124f, 0.0009600674f, -0.0011448302f, 0.0002338015f, -0.0006751233f,
        -0.0021502055f, 0.0006874471f, -0.0029599981f, 0.0038310475f, -0.0083237505f, 0.0111190917f, -0.0273729684f, -0.0194348902f,
        -0.0287795535f, -0.2461875094f, 0f, 0.2893385184f, 0.0454749754f, -0.0071713862f, 0.02786852f, -0.0201399994f,
        0.0036008873f, -0.0052485823f, -0.0002854023f, -0.0008464318f, -0.000139208f, 0.0007030117f, -0.0018235942f, 0.0009650724f,
        -0.0011443166f, 0.0002572409f, -0.0006766191f, -0.0021192661f, 0.0006912642f, -0.002920263f, 0.0038577642f, -0.0082749958f,
        0.0112543312f, -0.0274143729f, -0.01910814f, -0.029022977f, -0.2469134582f, 0f, 0.2887671634f, 0.04521577f,
        -0.0066934514f, 0.0278984919f, -0.0200060016f, 0.0036949508f, -0.0052336631f, -0.0002281587f, -0.0008456087f, -0.00010199f,
        0.0007037219f, -0.0017997396f, 0.0009700105f, -0.0011437365f, 0.0002807262f, -0.0006780843f, -0.0020881772f, 0.0006950441f,
        -0.0028802024f, 0.003884361f, -0.0082255412f, 0.0113897343f, -0.0274548505f, -0.0187790146f, -0.0292667907f, -0.2476375189f,
        0f, 0.2881929711f, 0.0449566513f, -0.0062177471f, 0.027927207f, -0.0198717875f, 0.0037883424f, -0.0052185024f,
        -0.0001711145f, -0.000844732f, -6.48094e-05f, 0.000704396f, -0.0017758498f, 0.0009748816f, -0.0011430896f, 0.0003042563f,
        -0.0006795187f, -0.00205694f, 0.0006987863f, -0.0028398176f, 0.0039108357f, -0.008175386f, 0.0115252958f, -0.0274943955f,
        -0.0184475141f, -0.0295109903f, -0.2483596753f, 0f, 0.2876159548f, 0.0446976244f, -0.0057442777f, 0.0279546695f,
        -0.0197373635f, 0.0038810604f, -0.0052031021f, -0.0001142724f, -0.000843802f, -2.76682e-05f, 0.000705034f, -0.0017519263f,
        0.0009796856f, -0.0011423758f, 0.0003278301f, -0.0006809222f, -0.0020255562f, 0.0007024907f, -0.00279911f, 0.0039371866f,
        -0.0081245298f, 0.0116610105f, -0.0275330022f, -0.0181136386f, -0.0297555714f, -0.2490799114f, 0f, 0.2870361277f,
        0.0444386944f, -0.0052730476f, 0.0279808843f, -0.0196027363f, 0.0039731035f, -0.0051874639f, -5.7635e-05f, -0.0008428187f,
        9.4316e-06f, 0.000705636f, -0.0017279703f, 0.0009844223f, -0.0011415951f, 0.0003514465f, -0.0006822946f, -0.0019940271f,
        0.0007061569f, -0.002758081f, 0.0039634116f, -0.008072972f, 0.0117968732f, -0.0275706649f, -0.0177773885f, -0.0300005297f,
        -0.249798211f, 0f, 0.2864535032f, 0.0441798664f, -0.0048040612f, 0.0280058556f, -0.0194679123f, 0.0040644702f,
        -0.0051715897f, -1.2048e-06f, -0.0008417824f, 4.64879e-05f, 0.0007062019f, -0.0017039832f, 0.0009890918f, -0.0011407473f,
        0.0003751041f, -0.0006836359f, -0.0019623542f, 0.0007097846f, -0.0027167319f, 0.0039895089f, -0.0080207122f, 0.0119328784f,
        -0.0276073781f, -0.017438764f, -0.0302458609f, -0.250514558f, 0f, 0.2858680946f, 0.0439211455f, -0.0043373225f,
        0.0280295883f, -0.0193328982f, 0.0041551591f, -0.0051554813f, 5.50157e-05f, -0.0008406932f, 8.34988e-05f, 0.0007067319f,
        -0.0016799663f, 0.0009936938f, -0.0011398323f, 0.0003988019f, -0.0006849458f, -0.001930539f, 0.0007133734f, -0.0026750641f,
        0.0040154764f, -0.0079677498f, 0.012069021f, -0.0276431362f, -0.0170977655f, -0.0304915606f, -0.2512289366f, 0f,
        0.2852799154f, 0.0436625367f, -0.0038728358f, 0.0280520868f, -0.0191977003f, 0.0042451688f, -0.0051391404f, 0.0001110239f,
        -0.0008395515f, 0.0001204623f, 0.000707226f, -0.001655921f, 0.0009982283f, -0.0011388501f, 0.0004225387f, -0.0006862243f,
        -0.0018985831f, 0.0007169232f, -0.002633079f, 0.0040413123f, -0.0079140845f, 0.0122052955f, -0.0276779335f, -0.0167543935f,
        -0.0307376244f, -0.2519413306f, 0f, 0.2846889792f, 0.0434040452f, -0.0034106052f, 0.0280733559f, -0.0190623252f,
        0.004334498f, -0.005122569f, 0.0001668173f, -0.0008383573f, 0.0001573763f, 0.0007076841f, -0.0016318485f, 0.0010026952f,
        -0.0011378004f, 0.0004463132f, -0.0006874712f, -0.0018664879f, 0.0007204335f, -0.0025907781f, 0.0040670146f, -0.0078597159f,
        0.0123416967f, -0.0277117645f, -0.0164086485f, -0.0309840481f, -0.2526517241f, 0f, 0.2840952995f, 0.0431456759f,
        -0.0029506347f, 0.0280934001f, -0.0189267794f, 0.0044231455f, -0.0051057688f, 0.0002223935f, -0.000837111f, 0.000194239f,
        0.0007081064f, -0.0016077502f, 0.0010070944f, -0.0011366832f, 0.0004701243f, -0.0006886865f, -0.0018342549f, 0.0007239042f,
        -0.0025481628f, 0.0040925813f, -0.0078046436f, 0.0124782191f, -0.0277446235f, -0.0160605311f, -0.031230827f, -0.2533601012f,
        0f, 0.2834988899f, 0.042887434f, -0.0024929283f, 0.0281122241f, -0.0187910693f, 0.0045111099f, -0.0050887416f,
        0.0002777499f, -0.0008358126f, 0.0002310483f, 0.0007084929f, -0.0015836275f, 0.0010114257f, -0.0011354985f, 0.0004939709f,
        -0.0006898699f, -0.0018018856f, 0.0007273348f, -0.0025052346f, 0.0041180105f, -0.0077488674f, 0.0126148574f, -0.027776505f,
        -0.0157100419f, -0.0314779569f, -0.254066446f, 0f, 0.2828997642f, 0.0426293244f, -0.0020374897f, 0.0281298327f,
        -0.0186552012f, 0.00459839f, -0.0050714894f, 0.0003328841f, -0.0008344625f, 0.0002678023f, 0.0007088437f, -0.0015594816f,
        0.0010156892f, -0.0011342461f, 0.0005178517f, -0.0006910214f, -0.0017693817f, 0.0007307252f, -0.002461995f, 0.0041433004f,
        -0.0076923868f, 0.012751606f, -0.0278074035f, -0.0153571817f, -0.0317254333f, -0.2547707427f, 0f, 0.282297936f,
        0.0423713522f, -0.0015843228f, 0.0281462306f, -0.0185191817f, 0.0046849848f, -0.0050540138f, 0.0003877937f, -0.0008330608f,
        0.000304499f, 0.0007091587f, -0.0015353139f, 0.0010198847f, -0.0011329259f, 0.0005417654f, -0.0006921409f, -0.0017367447f,
        0.0007340749f, -0.0024184456f, 0.0041684488f, -0.0076352016f, 0.0128884596f, -0.0278373134f, -0.0150019512f, -0.0319732517f,
        -0.2554729754f, 0f, 0.2816934193f, 0.0421135224f, -0.0011334314f, 0.0281614225f, -0.0183830172f, 0.0047708929f,
        -0.0050363169f, 0.0004424763f, -0.0008316077f, 0.0003411366f, 0.000709438f, -0.0015111258f, 0.0010240122f, -0.0011315379f,
        0.000565711f, -0.0006932281f, -0.001703976f, 0.0007373838f, -0.0023745879f, 0.004193454f, -0.0075773116f, 0.0130254126f,
        -0.0278662291f, -0.0146443513f, -0.0322214077f, -0.2561731284f, 0f, 0.2810862277f, 0.0418558399f, -0.0006848192f,
        0.0281754132f, -0.0182467139f, 0.0048561133f, -0.0050184003f, 0.0004969295f, -0.0008301034f, 0.0003777129f, 0.0007096818f,
        -0.0014869184f, 0.0010280716f, -0.0011300819f, 0.0005896871f, -0.0006942831f, -0.0016710773f, 0.0007406515f, -0.0023304234f,
        0.0042183139f, -0.0075187166f, 0.0131624595f, -0.0278941453f, -0.014284383f, -0.0324698968f, -0.256871186f, 0f,
        0.2804763753f, 0.0415983099f, -0.0002384897f, 0.0281882074f, -0.0181102783f, 0.004940645f, -0.005000266f, 0.0005511509f,
        -0.0008285482f, 0.0004142262f, 0.0007098899f, -0.0014626933f, 0.0010320628f, -0.001128558f, 0.0006136927f, -0.0006953057f,
        -0.0016380502f, 0.0007438777f, -0.0022859537f, 0.0042430266f, -0.0074594164f, 0.0132995949f, -0.0279210563f, -0.0139220472f,
        -0.0327187145f, -0.2575671325f, 0f, 0.279863876f, 0.0413409373f, 0.0002055536f, 0.0281998101f, -0.0179737167f,
        0.0050244868f, -0.0049819159f, 0.0006051382f, -0.0008269423f, 0.0004506744f, 0.0007100626f, -0.0014384516f, 0.0010359857f,
        -0.001126966f, 0.0006377264f, -0.0006962957f, -0.0016048963f, 0.0007470622f, -0.0022411805f, 0.0042675902f, -0.0073994108f,
        0.013436813f, -0.0279469568f, -0.013557345f, -0.0329678562f, -0.2582609521f, 0f, 0.2792487438f, 0.041083727f,
        0.0006473071f, 0.028210226f, -0.0178370354f, 0.0051076377f, -0.0049633517f, 0.000658889f, -0.000825286f, 0.0004870557f,
        0.0007101997f, -0.0014141947f, 0.0010398403f, -0.0011253058f, 0.000661787f, -0.0006972531f, -0.0015716171f, 0.0007502046f,
        -0.0021961054f, 0.0042920027f, -0.0073386998f, 0.0135741084f, -0.0279718412f, -0.0131902775f, -0.0332173174f, -0.2589526294f,
        0f, 0.2786309928f, 0.040826684f, 0.0010867675f, 0.02821946f, -0.0177002407f, 0.0051900968f, -0.0049445753f,
        0.0007124011f, -0.0008235793f, 0.0005233682f, 0.0007103015f, -0.001389924f, 0.0010436266f, -0.0011235774f, 0.0006858733f,
        -0.0006981777f, -0.0015382144f, 0.0007533047f, -0.00215073f, 0.0043162622f, -0.0072772833f, 0.0137114755f, -0.0279957041f,
        -0.0128208458f, -0.0334670937f, -0.2596421486f, 0f, 0.2780106372f, 0.0405698133f, 0.0015239315f, 0.0282275169f,
        -0.017563339f, 0.0052718632f, -0.0049255887f, 0.0007656721f, -0.0008218226f, 0.00055961f, 0.000710368f, -0.0013656406f,
        0.0010473445f, -0.0011217808f, 0.0007099841f, -0.0006990695f, -0.0015046896f, 0.0007563622f, -0.002105056f, 0.0043403668f,
        -0.0072151612f, 0.0138489085f, -0.0280185401f, -0.0124490514f, -0.0337171803f, -0.2603294944f, 0f, 0.2773876911f,
        0.0403131198f, 0.0019587958f, 0.0282344018f, -0.0174263364f, 0.0053529358f, -0.0049063936f, 0.0008186998f, -0.0008200161f,
        0.0005957791f, 0.0007103991f, -0.001341346f, 0.0010509939f, -0.0011199159f, 0.0007341182f, -0.0006999283f, -0.0014710444f,
        0.0007593768f, -0.0020590851f, 0.0043643146f, -0.0071523335f, 0.013986402f, -0.0280403436f, -0.0120748954f, -0.0339675728f,
        -0.2610146511f, 0f, 0.2767621687f, 0.0400566084f, 0.0023913572f, 0.0282401195f, -0.0172892392f, 0.0054333139f,
        -0.004886992f, 0.0008714818f, -0.0008181601f, 0.0006318737f, 0.0007103951f, -0.0013170415f, 0.0010545748f, -0.0011179825f,
        0.0007582742f, -0.0007007541f, -0.0014372806f, 0.0007623483f, -0.002012819f, 0.0043881035f, -0.0070888002f, 0.01412395f,
        -0.0280611095f, -0.0116983794f, -0.0342182666f, -0.2616976033f, 0f, 0.2761340843f, 0.039800284f, 0.0028216125f,
        0.0282446749f, -0.0171520536f, 0.0055129966f, -0.0048673857f, 0.0009240161f, -0.0008162547f, 0.0006678919f, 0.0007103559f,
        -0.0012927284f, 0.0010580871f, -0.0011159808f, 0.000782451f, -0.0007015466f, -0.0014033996f, 0.0007652763f, -0.0019662594f,
        0.0044117317f, -0.0070245614f, 0.0142615471f, -0.0280808321f, -0.0113195047f, -0.0344692571f, -0.2623783356f, 0f,
        0.2755034524f, 0.0395441516f, 0.0032495589f, 0.028248073f, -0.0170147859f, 0.0055919831f, -0.0048475767f, 0.0009763004f,
        -0.0008143001f, 0.0007038319f, 0.0007102816f, -0.0012684079f, 0.0010615309f, -0.0011139105f, 0.0008066474f, -0.0007023059f,
        -0.0013694033f, 0.0007681606f, -0.0019194082f, 0.0044351972f, -0.0069596172f, 0.0143991874f, -0.0280995063f, -0.0109382728f,
        -0.0347205396f, -0.2630568327f, 0f, 0.2748702872f, 0.039288216f, 0.0036751932f, 0.0282503189f, -0.0168774422f,
        0.0056702725f, -0.0048275667f, 0.0010283325f, -0.0008122967f, 0.0007396917f, 0.0007101723f, -0.0012440814f, 0.001064906f,
        -0.0011117717f, 0.000830862f, -0.0007030318f, -0.0013352933f, 0.0007710009f, -0.001872267f, 0.0044584981f, -0.0068939676f,
        0.0145368652f, -0.0281171265f, -0.0105546855f, -0.0349721095f, -0.2637330792f, 0f, 0.2742346032f, 0.0390324822f,
        0.0040985126f, 0.0282514175f, -0.0167400286f, 0.0057478643f, -0.0048073577f, 0.0010801102f, -0.0008102447f, 0.0007754696f,
        0.0007100281f, -0.0012197501f, 0.0010682125f, -0.0011095644f, 0.0008550936f, -0.0007037242f, -0.0013010712f, 0.0007737969f,
        -0.0018248377f, 0.0044816324f, -0.0068276127f, 0.0146745747f, -0.0281336875f, -0.0101687444f, -0.0352239622f, -0.2644070598f,
        0f, 0.2735964149f, 0.0387769549f, 0.0045195142f, 0.0282513738f, -0.0166025514f, 0.0058247576f, -0.0047869516f,
        0.0011316314f, -0.0008081443f, 0.0008111637f, 0.000709849f, -0.0011954155f, 0.0010714504f, -0.0011072885f, 0.0008793411f,
        -0.000704383f, -0.0012667387f, 0.0007765484f, -0.0017771221f, 0.0045045984f, -0.0067605529f, 0.0148123101f, -0.028149184f,
        -0.0097804511f, -0.0354760929f, -0.2650787592f, 0f, 0.2729557368f, 0.0385216391f, 0.0049381952f, 0.0282501929f,
        -0.0164650166f, 0.0059009518f, -0.0047663502f, 0.0011828939f, -0.0008059957f, 0.0008467721f, 0.0007096351f, -0.0011710787f,
        0.0010746195f, -0.001104944f, 0.000903603f, -0.0007050081f, -0.0012322977f, 0.0007792551f, -0.001729122f, 0.0045273939f,
        -0.0066927883f, 0.0149500656f, -0.0281636106f, -0.0093898076f, -0.0357284972f, -0.2657481624f, 0f, 0.2723125836f,
        0.0382665396f, 0.005354553f, 0.0282478799f, -0.0163274304f, 0.0059764463f, -0.0047455555f, 0.0012338956f, -0.0008037992f,
        0.0008822931f, 0.0007093864f, -0.0011467411f, 0.0010777199f, -0.0011025308f, 0.0009278781f, -0.0007055995f, -0.0011977496f,
        0.0007819167f, -0.0016808394f, 0.0045500171f, -0.006624319f, 0.0150878354f, -0.0281769621f, -0.0089968156f, -0.0359811701f,
        -0.266415254f, 0f, 0.2716669698f, 0.0380116612f, 0.005768585f, 0.0282444398f, -0.0161897988f, 0.0060512404f,
        -0.0047245693f, 0.0012846346f, -0.0008015551f, 0.0009177248f, 0.0007091031f, -0.001122404f, 0.0010807516f, -0.0011000489f,
        0.0009521653f, -0.0007061569f, -0.0011630964f, 0.0007845329f, -0.001632276f, 0.0045724661f, -0.0065551455f, 0.0152256136f,
        -0.0281892332f, -0.0086014772f, -0.0362341071f, -0.2670800191f, 0f, 0.2710189102f, 0.0377570087f, 0.0061802885f,
        0.0282398778f, -0.0160521279f, 0.0061253336f, -0.0047033936f, 0.0013351085f, -0.0007992635f, 0.0009530655f, 0.0007087853f,
        -0.0010980686f, 0.0010837146f, -0.0010974984f, 0.0009764632f, -0.0007066804f, -0.0011283397f, 0.0007871036f, -0.0015834338f,
        0.0045947389f, -0.006485268f, 0.0153633943f, -0.0282004185f, -0.0082037943f, -0.0364873034f, -0.2677424425f, 0f,
        0.2703684196f, 0.037502587f, 0.006589661f, 0.028234199f, -0.0159144237f, 0.0061987253f, -0.0046820303f, 0.0013853156f,
        -0.0007969248f, 0.0009883132f, 0.000708433f, -0.0010737363f, 0.0010866088f, -0.0010948791f, 0.0010007705f, -0.0007071699f,
        -0.0010934813f, 0.0007896283f, -0.0015343147f, 0.0046168337f, -0.0064146869f, 0.0155011716f, -0.028210513f, -0.0078037691f,
        -0.0367407543f, -0.2684025092f, 0f, 0.2697155126f, 0.0372484008f, 0.0069967002f, 0.0282274084f, -0.0157766923f,
        0.0062714151f, -0.0046604813f, 0.0014352536f, -0.0007945392f, 0.0010234663f, 0.0007080462f, -0.0010494083f, 0.0010894343f,
        -0.0010921911f, 0.0010250859f, -0.0007076252f, -0.0010585229f, 0.000792107f, -0.0014849206f, 0.0046387484f, -0.0063434024f,
        0.0156389396f, -0.0282195114f, -0.0074014036f, -0.036994455f, -0.2690602042f, 0f, 0.2690602042f, 0.036994455f,
        0.0074014037f, 0.0282195114f, -0.0156389396f, 0.0063434024f, -0.0046387484f, 0.0014849206f, -0.000792107f, 0.0010585229f,
        0.0007076252f, -0.0010250859f, 0.0010921911f, -0.0010894343f, 0.0010494083f, -0.0007080462f, -0.0010234663f, 0.0007945392f,
        -0.0014352536f, 0.0046604813f, -0.0062714151f, 0.0157766923f, -0.0282274084f, -0.0069967002f, -0.0372484008f, -0.2697155126f,
        0f, 0.2684025091f, 0.0367407542f, 0.0078037692f, 0.028210513f, -0.0155011716f, 0.0064146868f, -0.0046168337f,
        0.0015343147f, -0.0007896283f, 0.0010934813f, 0.0007071699f, -0.0010007705f, 0.0010948791f, -0.0010866088f, 0.0010737363f,
        -0.000708433f, -0.0009883132f, 0.0007969248f, -0.0013853156f, 0.0046820303f, -0.0061987253f, 0.0159144237f, -0.0282341989f,
        -0.006589661f, -0.037502587f, -0.2703684196f, 0f, 0.2677424424f, 0.0364873034f, 0.0082037944f, 0.0282004186f,
        -0.0153633943f, 0.006485268f, -0.0045947389f, 0.0015834338f, -0.0007871036f, 0.0011283397f, 0.0007066804f, -0.0009764631f,
        0.0010974984f, -0.0010837146f, 0.0010980686f, -0.0007087853f, -0.0009530655f, 0.0007992635f, -0.0013351085f, 0.0047033937f,
        -0.0061253336f, 0.0160521279f, -0.0282398778f, -0.0061802884f, -0.0377570087f, -0.2710189103f, 0f, 0.267080019f,
        0.036234107f, 0.0086014773f, 0.0281892332f, -0.0152256136f, 0.0065551455f, -0.0045724661f, 0.001632276f, -0.0007845329f,
        0.0011630964f, 0.0007061569f, -0.0009521653f, 0.0011000489f, -0.0010807516f, 0.001122404f, -0.0007091032f, -0.0009177248f,
        0.0008015551f, -0.0012846345f, 0.0047245693f, -0.0060512404f, 0.0161897988f, -0.0282444398f, -0.0057685849f, -0.0380116612f,
        -0.2716669699f, 0f, 0.266415254f, 0.0359811701f, 0.0089968157f, 0.0281769621f, -0.0150878355f, 0.006624319f,
        -0.0045500171f, 0.0016808394f, -0.0007819167f, 0.0011977496f, 0.0007055995f, -0.0009278781f, 0.0011025308f, -0.0010777199f,
        0.0011467411f, -0.0007093864f, -0.0008822931f, 0.0008037992f, -0.0012338956f, 0.0047455555f, -0.0059764463f, 0.0163274304f,
        -0.0282478799f, -0.005354553f, -0.0382665396f, -0.2723125836f, 0f, 0.2657481623f, 0.0357284971f, 0.0093898077f,
        0.0281636107f, -0.0149500657f, 0.0066927883f, -0.0045273939f, 0.001729122f, -0.0007792551f, 0.0012322976f, 0.0007050081f,
        -0.000903603f, 0.001104944f, -0.0010746195f, 0.0011710787f, -0.0007096351f, -0.0008467722f, 0.0008059957f, -0.0011828939f,
        0.0047663502f, -0.0059009518f, 0.0164650166f, -0.0282501929f, -0.0049381952f, -0.0385216391f, -0.2729557369f, 0f,
        0.2650787592f, 0.0354760929f, 0.0097804512f, 0.028149184f, -0.0148123101f, 0.0067605529f, -0.0045045984f, 0.0017771221f,
        -0.0007765484f, 0.0012667387f, 0.000704383f, -0.000879341f, 0.0011072885f, -0.0010714504f, 0.0011954155f, -0.000709849f,
        -0.0008111637f, 0.0008081443f, -0.0011316313f, 0.0047869516f, -0.0058247576f, 0.0166025514f, -0.0282513738f, -0.0045195141f,
        -0.0387769549f, -0.2735964149f, 0f, 0.2644070597f, 0.0352239621f, 0.0101687444f, 0.0281336876f, -0.0146745747f,
        0.0068276127f, -0.0044816324f, 0.0018248377f, -0.0007737969f, 0.0013010712f, 0.0007037242f, -0.0008550936f, 0.0011095644f,
        -0.0010682125f, 0.0012197501f, -0.0007100281f, -0.0007754696f, 0.0008102447f, -0.0010801102f, 0.0048073577f, -0.0057478643f,
        0.0167400286f, -0.0282514175f, -0.0040985125f, -0.0390324822f, -0.2742346032f, 0f, 0.2637330791f, 0.0349721095f,
        0.0105546856f, 0.0281171265f, -0.0145368652f, 0.0068939675f, -0.0044584981f, 0.001872267f, -0.0007710009f, 0.0013352933f,
        0.0007030318f, -0.000830862f, 0.0011117718f, -0.001064906f, 0.0012440814f, -0.0007101723f, -0.0007396917f, 0.0008122967f,
        -0.0010283325f, 0.0048275667f, -0.0056702725f, 0.0168774421f, -0.0282503189f, -0.0036751931f, -0.0392882161f, -0.2748702872f,
        0f, 0.2630568326f, 0.0347205396f, 0.0109382729f, 0.0280995063f, -0.0143991874f, 0.0069596172f, -0.0044351972f,
        0.0019194082f, -0.0007681606f, 0.0013694033f, 0.0007023059f, -0.0008066474f, 0.0011139105f, -0.0010615309f, 0.0012684079f,
        -0.0007102816f, -0.0007038319f, 0.0008143001f, -0.0009763004f, 0.0048475767f, -0.0055919831f, 0.0170147859f, -0.028248073f,
        -0.0032495588f, -0.0395441516f, -0.2755034524f, 0f, 0.2623783356f, 0.0344692571f, 0.0113195047f, 0.0280808322f,
        -0.0142615471f, 0.0070245614f, -0.0044117317f, 0.0019662594f, -0.0007652763f, 0.0014033996f, 0.0007015466f, -0.000782451f,
        0.0011159808f, -0.0010580871f, 0.0012927284f, -0.0007103559f, -0.0006678919f, 0.0008162547f, -0.0009240161f, 0.0048673857f,
        -0.0055129966f, 0.0171520536f, -0.0282446749f, -0.0028216125f, -0.039800284f, -0.2761340844f, 0f, 0.2616976032f,
        0.0342182666f, 0.0116983794f, 0.0280611095f, -0.0141239501f, 0.0070888002f, -0.0043881035f, 0.002012819f, -0.0007623483f,
        0.0014372806f, 0.0007007541f, -0.0007582742f, 0.0011179825f, -0.0010545748f, 0.0013170415f, -0.0007103951f, -0.0006318737f,
        0.0008181601f, -0.0008714818f, 0.004886992f, -0.0054333139f, 0.0172892392f, -0.0282401194f, -0.0023913571f, -0.0400566084f,
        -0.2767621687f, 0f, 0.261014651f, 0.0339675728f, 0.0120748955f, 0.0280403437f, -0.013986402f, 0.0071523335f,
        -0.0043643146f, 0.0020590851f, -0.0007593768f, 0.0014710444f, 0.0006999283f, -0.0007341181f, 0.0011199159f, -0.0010509939f,
        0.0013413461f, -0.0007103992f, -0.0005957791f, 0.0008200161f, -0.0008186997f, 0.0049063936f, -0.0053529358f, 0.0174263364f,
        -0.0282344018f, -0.0019587957f, -0.0403131198f, -0.2773876911f, 0f, 0.2603294943f, 0.0337171803f, 0.0124490514f,
        0.0280185401f, -0.0138489086f, 0.0072151612f, -0.0043403668f, 0.002105056f, -0.0007563622f, 0.0015046896f, 0.0006990695f,
        -0.0007099841f, 0.0011217808f, -0.0010473445f, 0.0013656406f, -0.000710368f, -0.00055961f, 0.0008218226f, -0.0007656721f,
        0.0049255887f, -0.0052718632f, 0.017563339f, -0.0282275169f, -0.0015239314f, -0.0405698133f, -0.2780106372f, 0f,
        0.2596421486f, 0.0334670936f, 0.0128208459f, 0.0279957041f, -0.0137114755f, 0.0072772833f, -0.0043162622f, 0.00215073f,
        -0.0007533047f, 0.0015382143f, 0.0006981777f, -0.0006858733f, 0.0011235775f, -0.0010436266f, 0.001389924f, -0.0007103015f,
        -0.0005233682f, 0.0008235793f, -0.0007124011f, 0.0049445753f, -0.0051900969f, 0.0177002407f, -0.0282194599f, -0.0010867675f,
        -0.040826684f, -0.2786309929f, 0f, 0.2589526293f, 0.0332173174f, 0.0131902775f, 0.0279718412f, -0.0135741085f,
        0.0073386998f, -0.0042920027f, 0.0021961054f, -0.0007502046f, 0.0015716171f, 0.0006972531f, -0.000661787f, 0.0011253058f,
        -0.0010398403f, 0.0014141947f, -0.0007101997f, -0.0004870557f, 0.000825286f, -0.000658889f, 0.0049633517f, -0.0051076377f,
        0.0178370354f, -0.0282102259f, -0.0006473071f, -0.041083727f, -0.2792487438f, 0f, 0.2582609521f, 0.0329678561f,
        0.013557345f, 0.0279469568f, -0.013436813f, 0.0073994108f, -0.0042675902f, 0.0022411805f, -0.0007470622f, 0.0016048963f,
        0.0006962957f, -0.0006377263f, 0.001126966f, -0.0010359857f, 0.0014384516f, -0.0007100626f, -0.0004506744f, 0.0008269423f,
        -0.0006051382f, 0.0049819159f, -0.0050244868f, 0.0179737167f, -0.02819981f, -0.0002055535f, -0.0413409373f, -0.279863876f,
        0f, 0.2575671324f, 0.0327187144f, 0.0139220472f, 0.0279210564f, -0.0132995949f, 0.0074594164f, -0.0042430266f,
        0.0022859538f, -0.0007438777f, 0.0016380502f, 0.0006953056f, -0.0006136927f, 0.001128558f, -0.0010320628f, 0.0014626933f,
        -0.0007098899f, -0.0004142262f, 0.0008285482f, -0.0005511509f, 0.005000266f, -0.004940645f, 0.0181102783f, -0.0281882074f,
        0.0002384897f, -0.0415983099f, -0.2804763753f, 0f, 0.256871186f, 0.0324698968f, 0.0142843831f, 0.0278941453f,
        -0.0131624595f, 0.0075187166f, -0.0042183139f, 0.0023304234f, -0.0007406515f, 0.0016710773f, 0.0006942831f, -0.0005896871f,
        0.0011300819f, -0.0010280716f, 0.0014869185f, -0.0007096818f, -0.0003777129f, 0.0008301034f, -0.0004969295f, 0.0050184003f,
        -0.0048561134f, 0.0182467139f, -0.0281754131f, 0.0006848193f, -0.04185584f, -0.2810862278f, 0f, 0.2561731284f,
        0.0322214077f, 0.0146443514f, 0.0278662292f, -0.0130254126f, 0.0075773116f, -0.0041934539f, 0.0023745879f, -0.0007373838f,
        0.001703976f, 0.0006932281f, -0.000565711f, 0.0011315379f, -0.0010240122f, 0.0015111258f, -0.000709438f, -0.0003411366f,
        0.0008316077f, -0.0004424763f, 0.0050363169f, -0.0047708929f, 0.0183830172f, -0.0281614225f, 0.0011334315f, -0.0421135224f,
        -0.2816934193f, 0f, 0.2554729753f, 0.0319732517f, 0.0150019512f, 0.0278373134f, -0.0128884596f, 0.0076352016f,
        -0.0041684488f, 0.0024184456f, -0.0007340749f, 0.0017367446f, 0.0006921409f, -0.0005417654f, 0.0011329259f, -0.0010198847f,
        0.0015353139f, -0.0007091587f, -0.0003044991f, 0.0008330608f, -0.0003877937f, 0.0050540138f, -0.0046849848f, 0.0185191817f,
        -0.0281462306f, 0.0015843229f, -0.0423713522f, -0.2822979361f, 0f, 0.2547707426f, 0.0317254333f, 0.0153571817f,
        0.0278074035f, -0.012751606f, 0.0076923867f, -0.0041433003f, 0.0024619951f, -0.0007307252f, 0.0017693817f, 0.0006910214f,
        -0.0005178517f, 0.0011342461f, -0.0010156892f, 0.0015594816f, -0.0007088437f, -0.0002678023f, 0.0008344625f, -0.0003328841f,
        0.0050714894f, -0.0045983901f, 0.0186552012f, -0.0281298327f, 0.0020374897f, -0.0426293244f, -0.2828997643f, 0f,
        0.2540664459f, 0.0314779569f, 0.0157100419f, 0.027776505f, -0.0126148574f, 0.0077488673f, -0.0041180105f, 0.0025052346f,
        -0.0007273348f, 0.0018018856f, 0.0006898699f, -0.0004939709f, 0.0011354985f, -0.0010114257f, 0.0015836275f, -0.0007084929f,
        -0.0002310483f, 0.0008358126f, -0.0002777499f, 0.0050887416f, -0.0045111099f, 0.0187910692f, -0.0281122241f, 0.0024929283f,
        -0.042887434f, -0.28349889f, 0f, 0.2533601011f, 0.031230827f, 0.0160605311f, 0.0277446235f, -0.0124782192f,
        0.0078046436f, -0.0040925813f, 0.0025481628f, -0.0007239042f, 0.0018342549f, 0.0006886865f, -0.0004701243f, 0.0011366832f,
        -0.0010070944f, 0.0016077502f, -0.0007081064f, -0.000194239f, 0.000837111f, -0.0002223935f, 0.0051057688f, -0.0044231455f,
        0.0189267794f, -0.0280934001f, 0.0029506348f, -0.0431456759f, -0.2840952996f, 0f, 0.252651724f, 0.030984048f,
        0.0164086485f, 0.0277117645f, -0.0123416967f, 0.0078597159f, -0.0040670146f, 0.0025907781f, -0.0007204335f, 0.0018664878f,
        0.0006874712f, -0.0004463132f, 0.0011378004f, -0.0010026952f, 0.0016318485f, -0.0007076841f, -0.0001573764f, 0.0008383573f,
        -0.0001668173f, 0.005122569f, -0.004334498f, 0.0190623252f, -0.0280733559f, 0.0034106053f, -0.0434040452f, -0.2846889793f,
        0f, 0.2519413305f, 0.0307376244f, 0.0167543936f, 0.0276779335f, -0.0122052956f, 0.0079140845f, -0.0040413123f,
        0.002633079f, -0.0007169232f, 0.0018985831f, 0.0006862243f, -0.0004225386f, 0.0011388501f, -0.0009982283f, 0.001655921f,
        -0.000707226f, -0.0001204623f, 0.0008395515f, -0.0001110239f, 0.0051391404f, -0.0042451688f, 0.0191977003f, -0.0280520868f,
        0.0038728359f, -0.0436625367f, -0.2852799155f, 0f, 0.2512289365f, 0.0304915606f, 0.0170977656f, 0.0276431362f,
        -0.012069021f, 0.0079677498f, -0.0040154764f, 0.0026750641f, -0.0007133734f, 0.001930539f, 0.0006849458f, -0.0003988019f,
        0.0011398323f, -0.0009936938f, 0.0016799663f, -0.0007067319f, -8.34988e-05f, 0.0008406932f, -5.50157e-05f, 0.0051554813f,
        -0.0041551591f, 0.0193328982f, -0.0280295883f, 0.0043373226f, -0.0439211455f, -0.2858680946f, 0f, 0.250514558f,
        0.0302458608f, 0.0174387641f, 0.0276073782f, -0.0119328784f, 0.0080207121f, -0.0039895089f, 0.0027167319f, -0.0007097846f,
        0.0019623542f, 0.0006836359f, -0.0003751041f, 0.0011407473f, -0.0009890918f, 0.0017039832f, -0.0007062019f, -4.6488e-05f,
        0.0008417823f, 1.2048e-06f, 0.0051715897f, -0.0040644702f, 0.0194679123f, -0.0280058556f, 0.0048040612f, -0.0441798664f,
        -0.2864535032f, 0f, 0.2497982109f, 0.0300005296f, 0.0177773886f, 0.0275706649f, -0.0117968732f, 0.008072972f,
        -0.0039634116f, 0.002758081f, -0.0007061569f, 0.001994027f, 0.0006822946f, -0.0003514464f, 0.0011415951f, -0.0009844223f,
        0.0017279703f, -0.000705636f, -9.4316e-06f, 0.0008428187f, 5.7635e-05f, 0.0051874639f, -0.0039731035f, 0.0196027363f,
        -0.0279808842f, 0.0052730477f, -0.0444386944f, -0.2870361277f, 0f, 0.2490799113f, 0.0297555713f, 0.0181136387f,
        0.0275330022f, -0.0116610106f, 0.0081245298f, -0.0039371866f, 0.00279911f, -0.0007024907f, 0.0020255561f, 0.0006809222f,
        -0.0003278301f, 0.0011423758f, -0.0009796855f, 0.0017519263f, -0.000705034f, 2.76681e-05f, 0.000843802f, 0.0001142725f,
        0.0052031021f, -0.0038810605f, 0.0197373635f, -0.0279546695f, 0.0057442778f, -0.0446976244f, -0.2876159549f, 0f,
        0.2483596752f, 0.0295109902f, 0.0184475142f, 0.0274943955f, -0.0115252958f, 0.008175386f, -0.0039108357f, 0.0028398176f,
        -0.0006987863f, 0.00205694f, 0.0006795187f, -0.0003042563f, 0.0011430896f, -0.0009748816f, 0.0017758499f, -0.000704396f,
        6.48093e-05f, 0.000844732f, 0.0001711146f, 0.0052185024f, -0.0037883424f, 0.0198717874f, -0.0279272069f, 0.0062177472f,
        -0.0449566513f, -0.2881929712f, 0f, 0.2476375188f, 0.0292667907f, 0.0187790147f, 0.0274548505f, -0.0113897343f,
        0.0082255412f, -0.003884361f, 0.0028802024f, -0.0006950441f, 0.0020881771f, 0.0006780843f, -0.0002807262f, 0.0011437365f,
        -0.0009700105f, 0.0017997396f, -0.0007037219f, 0.00010199f, 0.0008456087f, 0.0002281587f, 0.0052336631f, -0.0036949509f,
        0.0200060016f, -0.0278984919f, 0.0066934515f, -0.04521577f, -0.2887671634f, 0f, 0.2469134581f, 0.029022977f,
        0.0191081401f, 0.0274143729f, -0.0112543312f, 0.0082749958f, -0.0038577642f, 0.002920263f, -0.0006912642f, 0.0021192661f,
        0.0006766191f, -0.0002572409f, 0.0011443166f, -0.0009650724f, 0.0018235942f, -0.0007030117f, 0.000139208f, 0.0008464318f,
        0.0002854023f, 0.0052485823f, -0.0036008873f, 0.0201399994f, -0.02786852f, 0.0071713863f, -0.0454749754f, -0.2893385184f,
        0f, 0.2461875093f, 0.0287795535f, 0.0194348902f, 0.0273729684f, -0.0111190917f, 0.0083237505f, -0.0038310475f,
        0.0029599981f, -0.0006874471f, 0.0021502055f, 0.0006751233f, -0.0002338015f, 0.0011448302f, -0.0009600674f, 0.0018474124f,
        -0.0007022654f, 0.0001764614f, 0.0008472012f, 0.0003428428f, 0.0052632584f, -0.0035061533f, 0.0202737742f, -0.0278372867f,
        0.0076515471f, -0.0457342624f, -0.289907023f, 0f, 0.2454596886f, 0.0285365244f, 0.0197592651f, 0.0273306426f,
        -0.010984021f, 0.0083718059f, -0.0038042127f, 0.0029994066f, -0.0006835929f, 0.0021809939f, 0.000673597f, -0.0002104093f,
        0.0011452772f, -0.0009549957f, 0.0018711927f, -0.0007014829f, 0.0002137482f, 0.0008479165f, 0.0004004775f, 0.0052776894f,
        -0.0034107504f, 0.0204073195f, -0.0278047875f, 0.0081339294f, -0.0459936258f, -0.290472664f, 0f, 0.2447300122f,
        0.028293894f, 0.0200812646f, 0.0272874011f, -0.0108491242f, 0.0084191626f, -0.0037772617f, 0.0030384869f, -0.0006797022f,
        0.0022116298f, 0.0006720403f, -0.0001870654f, 0.0011456579f, -0.0009498574f, 0.001894934f, -0.0007006643f, 0.0002510662f,
        0.0008485777f, 0.0004583038f, 0.0052918737f, -0.0033146802f, 0.0205406287f, -0.027771018f, 0.0086185285f, -0.0462530606f,
        -0.2910354285f, 0f, 0.2439984964f, 0.0280516665f, 0.0204008889f, 0.0272432498f, -0.0107144066f, 0.0084658212f,
        -0.0037501965f, 0.0030772381f, -0.000675775f, 0.0022421118f, 0.0006704533f, -0.000163771f, 0.0011459724f, -0.0009446525f,
        0.0019186347f, -0.0006998094f, 0.0002884136f, 0.0008491846f, 0.0005163189f, 0.0053058094f, -0.0032179443f, 0.020673695f,
        -0.0277359737f, 0.0091053397f, -0.0465125615f, -0.2915953034f, 0f, 0.2432651574f, 0.0278098461f, 0.0207181382f,
        0.0271981943f, -0.0105798731f, 0.0085117824f, -0.0037230191f, 0.0031156587f, -0.0006718118f, 0.0022724386f, 0.0006688363f,
        -0.000140527f, 0.0011462208f, -0.0009393814f, 0.0019422936f, -0.0006989183f, 0.0003257881f, 0.000849737f, 0.0005745202f,
        0.0053194948f, -0.0031205445f, 0.0208065119f, -0.0276996504f, 0.0095943581f, -0.0467721233f, -0.2921522758f, 0f,
        0.2425300115f, 0.0275684372f, 0.0210330126f, 0.0271522403f, -0.0104455288f, 0.0085570471f, -0.0036957313f, 0.0031537477f,
        -0.0006678129f, 0.0023026087f, 0.0006671893f, -0.0001173348f, 0.0011464033f, -0.000934044f, 0.0019659094f, -0.000697991f,
        0.0003631878f, 0.0008502347f, 0.000632905f, 0.0053329281f, -0.0030224825f, 0.0209390726f, -0.0276620436f, 0.0100855791f,
        -0.0470317411f, -0.2927063329f, 0f, 0.2417930752f, 0.0273274437f, 0.0213455124f, 0.0271053936f, -0.0103113789f,
        0.0086016158f, -0.0036683351f, 0.0031915037f, -0.0006637786f, 0.0023326209f, 0.0006655125f, -9.41954e-05f, 0.0011465199f,
        -0.0009286405f, 0.0019894807f, -0.0006970274f, 0.0004006107f, 0.0008506775f, 0.0006914705f, 0.0053461076f, -0.0029237599f,
        0.0210713706f, -0.0276231491f, 0.0105789975f, -0.0472914095f, -0.2932574619f, 0f, 0.2410543647f, 0.0270868701f,
        0.0216556379f, 0.0270576599f, -0.0101774283f, 0.0086454895f, -0.0036408324f, 0.0032289258f, -0.0006597092f, 0.0023624737f,
        0.0006638061f, -7.111e-05f, 0.0011465709f, -0.0009231711f, 0.0020130061f, -0.0006960276f, 0.0004380545f, 0.0008510654f,
        0.000750214f, 0.0053590316f, -0.0028243787f, 0.0212033992f, -0.0275829624f, 0.0110746086f, -0.0475511234f, -0.29380565f,
        0f, 0.2403138965f, 0.0268467203f, 0.0219633896f, 0.0270090449f, -0.010043682f, 0.0086886688f, -0.0036132252f,
        0.0032660126f, -0.000655605f, 0.0023921657f, 0.0006620701f, -4.80796e-05f, 0.0011465563f, -0.0009176359f, 0.0020364844f,
        -0.0006949914f, 0.0004755173f, 0.000851398f, 0.0008091327f, 0.0053716982f, -0.0027243405f, 0.0213351515f, -0.0275414794f,
        0.0115724071f, -0.0478108776f, -0.2943508846f, 0f, 0.2395716869f, 0.0266069985f, 0.0222687679f, 0.0269595544f,
        -0.009910145f, 0.0087311547f, -0.0035855154f, 0.0033027632f, -0.0006514663f, 0.0024216957f, 0.0006603048f, -2.51054e-05f,
        0.0011464765f, -0.0009120351f, 0.0020599141f, -0.000693919f, 0.000512997f, 0.0008516753f, 0.0008682239f, 0.0053841057f,
        -0.0026236473f, 0.021466621f, -0.0274986957f, 0.012072388f, -0.0480706669f, -0.2948931532f, 0f, 0.2388277525f,
        0.0263677088f, 0.0225717734f, 0.0269091942f, -0.0097768223f, 0.008772948f, -0.0035577049f, 0.0033391764f, -0.0006472936f,
        0.0024510624f, 0.0006585103f, -2.1885e-06f, 0.0011463314f, -0.0009063689f, 0.002083294f, -0.0006928102f, 0.0005504915f,
        0.000851897f, 0.0009274847f, 0.0053962525f, -0.0025223009f, 0.0215978008f, -0.0274546072f, 0.0125745461f, -0.0483304861f,
        -0.295432443f, 0f, 0.2380821098f, 0.0261288555f, 0.0228724066f, 0.0268579701f, -0.0096437186f, 0.0088140495f,
        -0.0035297957f, 0.0033752511f, -0.0006430869f, 0.0024802643f, 0.0006566867f, 2.067e-05f, 0.0011461212f, -0.0009006373f,
        0.0021066227f, -0.0006916652f, 0.0005879987f, 0.0008520631f, 0.0009869123f, 0.0054081368f, -0.0024203034f, 0.0217286842f,
        -0.0274092097f, 0.0130788761f, -0.04859033f, -0.2959687418f, 0f, 0.2373347752f, 0.0258904424f, 0.0231706683f,
        0.0268058878f, -0.009510839f, 0.0088544603f, -0.0035017896f, 0.0034109863f, -0.0006388468f, 0.0025093002f, 0.0006548341f,
        4.34691e-05f, 0.0011458462f, -0.0008948406f, 0.0021298989f, -0.0006904838f, 0.0006255165f, 0.0008521733f, 0.001046504f,
        0.0054197569f, -0.0023176565f, 0.0218592645f, -0.0273624989f, 0.0135853728f, -0.0488501934f, -0.296502037f, 0f,
        0.2365857653f, 0.0256524738f, 0.0234665591f, 0.0267529532f, -0.0093781883f, 0.0088941813f, -0.0034736886f, 0.0034463809f,
        -0.0006345735f, 0.0025381689f, 0.0006529528f, 6.62075e-05f, 0.0011455065f, -0.0008889789f, 0.0021531212f, -0.0006892661f,
        0.0006630428f, 0.0008522276f, 0.0011062567f, 0.0054311111f, -0.0022143624f, 0.0219895349f, -0.0273144708f, 0.0140940306f,
        -0.0491100711f, -0.2970323163f, 0f, 0.2358350966f, 0.0254149536f, 0.0237600798f, 0.026699172f, -0.0092457713f,
        0.0089332134f, -0.0034454946f, 0.0034814339f, -0.0006302674f, 0.002566869f, 0.0006510429f, 8.88843e-05f, 0.0011451022f,
        -0.0008830524f, 0.0021762883f, -0.0006880121f, 0.0007005755f, 0.0008522257f, 0.0011661677f, 0.0054421976f, -0.0021104231f,
        0.0221194886f, -0.0272651212f, 0.0146048441f, -0.0493699578f, -0.2975595675f, 0f, 0.2350827857f, 0.0251778859f,
        0.0240512313f, 0.0266445501f, -0.0091135929f, 0.0089715577f, -0.0034172096f, 0.0035161444f, -0.0006259287f, 0.0025953992f,
        0.0006491046f, 0.0001114984f, 0.0011446335f, -0.0008770614f, 0.0021993988f, -0.0006867218f, 0.0007381125f, 0.0008521675f,
        0.001226234f, 0.0054530148f, -0.0020058407f, 0.0222491188f, -0.027214446f, 0.0151178079f, -0.0496298482f, -0.2980837783f,
        0f, 0.2343288493f, 0.0249412747f, 0.0243400145f, 0.0265890933f, -0.0089816577f, 0.0090092152f, -0.0033888354f,
        0.0035505114f, -0.0006215578f, 0.0026237584f, 0.0006471379f, 0.0001340486f, 0.0011441007f, -0.0008710059f, 0.0022224515f,
        -0.0006853952f, 0.0007756516f, 0.0008520528f, 0.0012864528f, 0.005463561f, -0.0019006172f, 0.0223784186f, -0.0271624412f,
        0.0156329162f, -0.0498897372f, -0.2986049366f, 0f, 0.233573304f, 0.024705124f, 0.0246264305f, 0.0265328074f,
        -0.0088499706f, 0.0090461869f, -0.003360374f, 0.0035845338f, -0.000617155f, 0.0026519452f, 0.0006451431f, 0.000156534f,
        0.0011435038f, -0.0008648861f, 0.0022454449f, -0.0006840322f, 0.0008131908f, 0.0008518816f, 0.0013468211f, 0.0054738345f,
        -0.0017947547f, 0.0225073813f, -0.0271091027f, 0.0161501635f, -0.0501496196f, -0.2991230303f, 0f, 0.2328161664f,
        0.0244694377f, 0.0249104802f, 0.0264756982f, -0.0087185362f, 0.009082474f, -0.0033318272f, 0.0036182109f, -0.0006127207f,
        0.0026799584f, 0.0006431203f, 0.0001789534f, 0.0011428431f, -0.0008587023f, 0.0022683778f, -0.000682633f, 0.0008507279f,
        0.0008516536f, 0.0014073359f, 0.0054838337f, -0.0016882556f, 0.022636f, -0.0270544266f, 0.0166695439f, -0.0504094899f,
        -0.2996380474f, 0f, 0.2320574532f, 0.0242342199f, 0.0251921647f, 0.0264177717f, -0.0085873593f, 0.0091180776f,
        -0.0033031971f, 0.0036515417f, -0.0006082552f, 0.0027077969f, 0.0006410697f, 0.0002013058f, 0.0011421188f, -0.0008524546f,
        0.0022912487f, -0.0006811974f, 0.0008882608f, 0.0008513688f, 0.0014679944f, 0.0054935568f, -0.001581122f, 0.0227642679f,
        -0.0269984088f, 0.0171910517f, -0.050669343f, -0.3001499759f, 0f, 0.2312971812f, 0.0239994745f, 0.0254714854f,
        0.0263590336f, -0.0084564445f, 0.0091529987f, -0.0032744854f, 0.0036845253f, -0.0006037588f, 0.0027354594f, 0.0006389915f,
        0.0002235902f, 0.001141331f, -0.0008461432f, 0.0023140564f, -0.0006797256f, 0.0009257874f, 0.000851027f, 0.0015287936f,
        0.0055030023f, -0.0014733561f, 0.022892178f, -0.0269410455f, 0.017714681f, -0.0509291736f, -0.300658804f, 0f,
        0.2305353671f, 0.0237652053f, 0.0257484433f, 0.0262994898f, -0.0083257964f, 0.0091872387f, -0.0032456942f, 0.0037171608f,
        -0.0005992318f, 0.0027629447f, 0.0006368857f, 0.0002458054f, 0.0011404799f, -0.0008397684f, 0.0023367996f, -0.0006782175f,
        0.0009633054f, 0.000850628f, 0.0015897303f, 0.0055121684f, -0.0013649602f, 0.0230197236f, -0.0268823326f, 0.0182404258f,
        -0.0511889765f, -0.3011645199f, 0f, 0.2297720275f, 0.0235314164f, 0.0260230397f, 0.0262391462f, -0.0081954197f,
        0.0092207986f, -0.0032168253f, 0.0037494474f, -0.0005946746f, 0.0027902517f, 0.0006347526f, 0.0002679505f, 0.0011395657f,
        -0.0008333304f, 0.0023594768f, -0.0006766731f, 0.0010008128f, 0.0008501718f, 0.0016508017f, 0.0055210536f, -0.0012559367f,
        0.0231468976f, -0.0268222664f, 0.0187682801f, -0.0514487463f, -0.3016671118f, 0f, 0.2290071794f, 0.0232981115f,
        0.0262952762f, 0.0261780087f, -0.008065319f, 0.0092536797f, -0.0031878805f, 0.0037813843f, -0.0005900874f, 0.0028173791f,
        0.0006325923f, 0.0002900244f, 0.0011385887f, -0.0008268292f, 0.0023820868f, -0.0006750925f, 0.0010383075f, 0.0008496582f,
        0.0017120047f, 0.0055296561f, -0.0011462879f, 0.0232736933f, -0.0267608429f, 0.0192982379f, -0.0517084777f, -0.3021665681f,
        0f, 0.2282408395f, 0.0230652946f, 0.026565154f, 0.0261160831f, -0.0079354987f, 0.0092858833f, -0.0031588619f,
        0.0038129707f, -0.0005854707f, 0.0028443259f, 0.0006304051f, 0.0003120261f, 0.001137549f, -0.0008202653f, 0.0024046282f,
        -0.0006734757f, 0.0010757872f, 0.000849087f, 0.0017733362f, 0.0055379744f, -0.0010360162f, 0.0234001037f, -0.0266980583f,
        0.019830293f, -0.0519681655f, -0.302662877f, 0f, 0.2274730246f, 0.0228329695f, 0.0268326747f, 0.0260533754f,
        -0.0078059635f, 0.0093174106f, -0.0031297713f, 0.0038442058f, -0.0005808248f, 0.0028710908f, 0.0006281909f, 0.0003339546f,
        0.0011364468f, -0.0008136387f, 0.0024270997f, -0.0006718227f, 0.0011132498f, 0.0008484582f, 0.0018347932f, 0.0055460068f,
        -0.000925124f, 0.0235261219f, -0.0266339089f, 0.0203644392f, -0.0522278044f, -0.3031560272f, 0f, 0.2267037515f,
        0.02260114f, 0.0270978398f, 0.0259898914f, -0.0076767177f, 0.009348263f, -0.0031006106f, 0.0038750888f, -0.00057615f,
        0.0028976727f, 0.0006259502f, 0.0003558089f, 0.0011352824f, -0.0008069497f, 0.0024495f, -0.0006701335f, 0.0011506931f,
        0.0008477717f, 0.0018963727f, 0.0055537517f, -0.0008136138f, 0.0236517409f, -0.0265683908f, 0.0209006701f, -0.052487389f,
        -0.3036460072f, 0f, 0.2259330372f, 0.02236981f, 0.027360651f, 0.025925637f, -0.0075477659f, 0.0093784417f,
        -0.0030713817f, 0.003905619f, -0.0005714466f, 0.0029240705f, 0.0006236829f, 0.0003775879f, 0.0011340559f, -0.0008001985f,
        0.0024718278f, -0.0006684082f, 0.001188115f, 0.0008470272f, 0.0019580714f, 0.0055612076f, -0.0007014881f, 0.0237769538f,
        -0.0265015003f, 0.0214389795f, -0.0527469141f, -0.3041328055f, 0f, 0.2251608984f, 0.0221389831f, 0.0276211099f,
        0.0258606181f, -0.0074191125f, 0.0094079482f, -0.0030420864f, 0.0039357958f, -0.000566715f, 0.0029502832f, 0.0006213893f,
        0.0003992906f, 0.0011327676f, -0.0007933854f, 0.0024940816f, -0.0006666467f, 0.0012255133f, 0.0008462248f, 0.0020198864f,
        0.0055683728f, -0.0005887495f, 0.0239017537f, -0.0264332338f, 0.0219793609f, -0.0530063743f, -0.3046164108f, 0f,
        0.2243873521f, 0.0219086633f, 0.0278792183f, 0.0257948406f, -0.0072907619f, 0.0094367838f, -0.0030127267f, 0.0039656183f,
        -0.0005619554f, 0.0029763095f, 0.0006190695f, 0.0004209161f, 0.0011314176f, -0.0007865106f, 0.0025162602f, -0.0006648492f,
        0.0012628858f, 0.0008453642f, 0.0020818144f, 0.0055752457f, -0.0004754005f, 0.0240261335f, -0.0263635874f, 0.0225218078f,
        -0.0532657643f, -0.3050968119f, 0f, 0.2236124152f, 0.0216788541f, 0.028134978f, 0.0257283106f, -0.0071627185f,
        0.00946495f, -0.0029833045f, 0.003995086f, -0.0005571684f, 0.0030021485f, 0.0006167238f, 0.0004424633f, 0.0011300063f,
        -0.0007795743f, 0.0025383623f, -0.0006630155f, 0.0013002304f, 0.0008444455f, 0.0021438523f, 0.0055818247f, -0.0003614437f,
        0.0241500864f, -0.0262925577f, 0.0230663136f, -0.0535250789f, -0.3055739975f, 0f, 0.2228361046f, 0.0214495595f,
        0.0283883908f, 0.0256610338f, -0.0070349866f, 0.0094924482f, -0.0029538216f, 0.0040241981f, -0.0005523541f, 0.003027799f,
        0.0006143522f, 0.0004639312f, 0.0011285338f, -0.0007725768f, 0.0025603865f, -0.0006611459f, 0.0013375449f, 0.0008434684f,
        0.0022059971f, 0.0055881084f, -0.0002468818f, 0.0242736052f, -0.0262201409f, 0.0236128718f, -0.0537843126f, -0.3060479567f,
        0f, 0.2220584373f, 0.021220783f, 0.0286394587f, 0.0255930162f, -0.0069075706f, 0.0095192797f, -0.0029242798f,
        0.0040529541f, -0.0005475129f, 0.0030532601f, 0.000611955f, 0.000485319f, 0.0011270003f, -0.0007655182f, 0.0025823316f,
        -0.0006592403f, 0.001374827f, 0.0008424329f, 0.0022682454f, 0.0055940951f, -0.0001317174f, 0.0243966831f, -0.0261463334f,
        0.0241614756f, -0.0540434601f, -0.3065186783f, 0f, 0.2212794302f, 0.0209925284f, 0.0288881836f, 0.0255242637f,
        -0.0067804747f, 0.0095454462f, -0.0028946812f, 0.0040813534f, -0.0005426452f, 0.0030785306f, 0.0006095323f, 0.0005066256f,
        0.0011254061f, -0.0007583989f, 0.0026041961f, -0.0006572987f, 0.0014120747f, 0.0008413389f, 0.0023305941f, 0.0055997832f,
        -1.59533e-05f, 0.024519313f, -0.0260711318f, 0.0247121184f, -0.0543025161f, -0.3069861513f, 0f, 0.2204991004f,
        0.0207647994f, 0.0291345677f, 0.0254547824f, -0.0066537032f, 0.0095709492f, -0.0028650274f, 0.0041093953f, -0.0005377513f,
        0.0031036095f, 0.0006070844f, 0.00052785f, 0.0011237514f, -0.0007512192f, 0.0026259788f, -0.0006553212f, 0.0014492856f,
        0.0008401862f, 0.00239304f, 0.0056051713f, 0.0001004077f, 0.0246414879f, -0.0259945325f, 0.0252647932f, -0.0545614753f,
        -0.307450365f, 0f, 0.2197174647f, 0.0205375995f, 0.0293786129f, 0.025384578f, -0.0065272604f, 0.0095957901f,
        -0.0028353205f, 0.0041370793f, -0.0005328315f, 0.0031284957f, 0.0006046113f, 0.0005489913f, 0.0011220364f, -0.0007439791f,
        0.0026476784f, -0.0006533078f, 0.0014864577f, 0.0008389749f, 0.0024555799f, 0.0056102577f, 0.000217363f, 0.0247632008f,
        -0.025916532f, 0.0258194932f, -0.0548203323f, -0.3079113084f, 0f, 0.2189345403f, 0.0203109325f, 0.0296203216f,
        0.0253136566f, -0.0064011503f, 0.0096199706f, -0.0028055622f, 0.0041644048f, -0.0005278862f, 0.0031531884f, 0.0006021133f,
        0.0005700485f, 0.0011202614f, -0.0007366791f, 0.0026692935f, -0.0006512586f, 0.0015235887f, 0.0008377047f, 0.0025182105f,
        0.005615041f, 0.0003349096f, 0.0248844446f, -0.0258371268f, 0.0263762114f, -0.0550790818f, -0.3083689708f, 0f,
        0.2181503441f, 0.0200848019f, 0.0298596959f, 0.0252420241f, -0.0062753773f, 0.0096434922f, -0.0027757545f, 0.0041913713f,
        -0.0005229157f, 0.0031776865f, 0.0005995906f, 0.0005910207f, 0.0011184266f, -0.0007293194f, 0.0026908229f, -0.0006491735f,
        0.0015606764f, 0.0008363757f, 0.0025809285f, 0.0056195197f, 0.0004530446f, 0.0250052123f, -0.0257563136f, 0.0269349408f,
        -0.0553377184f, -0.3088233415f, 0f, 0.2173648932f, 0.0198592114f, 0.0300967381f, 0.0251696864f, -0.0061499454f,
        0.0096663566f, -0.0027458991f, 0.0042179784f, -0.0005179204f, 0.003201989f, 0.0005970433f, 0.000611907f, 0.0011165323f,
        -0.0007219003f, 0.0027122652f, -0.0006470528f, 0.0015977186f, 0.0008349877f, 0.0026437308f, 0.0056236922f, 0.0005717653f,
        0.0251254969f, -0.0256740889f, 0.0274956742f, -0.0555962367f, -0.30927441f, 0f, 0.2165782047f, 0.0196341644f,
        0.0303314506f, 0.0250966496f, -0.0060248587f, 0.0096885653f, -0.0027159979f, 0.0042442254f, -0.0005129006f, 0.0032260949f,
        0.0005944716f, 0.0006327063f, 0.0011145787f, -0.0007144219f, 0.0027336191f, -0.0006448964f, 0.0016347131f, 0.0008335406f,
        0.002706614f, 0.005627557f, 0.0006910686f, 0.0252452912f, -0.0255904493f, 0.0280584046f, -0.0558546314f, -0.3097221657f,
        0f, 0.2157902956f, 0.0194096646f, 0.0305638358f, 0.0250229195f, -0.0059001214f, 0.0097101202f, -0.0026860528f,
        0.004270112f, -0.0005078566f, 0.0032500034f, 0.0005918757f, 0.0006534178f, 0.0011125661f, -0.0007068847f, 0.0027548834f,
        -0.0006427043f, 0.0016716577f, 0.0008320344f, 0.0027695749f, 0.0056311126f, 0.0008109515f, 0.0253645883f, -0.0255053915f,
        0.0286231247f, -0.0561128972f, -0.3101665981f, 0f, 0.215001183f, 0.0191857156f, 0.0307938962f, 0.0249485022f,
        -0.0057757375f, 0.0097310227f, -0.0026560656f, 0.0042956378f, -0.0005027888f, 0.0032737133f, 0.0005892558f, 0.0006740406f,
        0.0011104946f, -0.0006992888f, 0.0027760566f, -0.0006404767f, 0.0017085502f, 0.000830469f, 0.00283261f, 0.0056343577f,
        0.0009314112f, 0.0254833811f, -0.0254189123f, 0.0291898272f, -0.0563710286f, -0.310607697f, 0f, 0.214210884f,
        0.0189623207f, 0.0310216342f, 0.0248734035f, -0.005651711f, 0.0097512747f, -0.0026260381f, 0.0043208022f, -0.0004976976f,
        0.0032972239f, 0.000586612f, 0.0006945738f, 0.0011083646f, -0.0006916346f, 0.0027971376f, -0.0006382135f, 0.0017453884f,
        0.0008288442f, 0.0028957161f, 0.0056372906f, 0.0010524444f, 0.0256016624f, -0.0253310083f, 0.0297585047f, -0.0566290203f,
        -0.3110454518f, 0f, 0.2134194157f, 0.0187394835f, 0.0312470526f, 0.0247976295f, -0.0055280459f, 0.0097708779f,
        -0.0025959722f, 0.004345605f, -0.0004925832f, 0.0033205342f, 0.0005839447f, 0.0007150163f, 0.0011061764f, -0.0006839223f,
        0.0028181249f, -0.0006359148f, 0.0017821701f, 0.0008271602f, 0.0029588899f, 0.0056399099f, 0.0011740482f, 0.0257194252f,
        -0.0252416763f, 0.0303291499f, -0.056886867f, -0.3114798526f, 0f, 0.2126267952f, 0.0185172074f, 0.0314701539f,
        0.0247211862f, -0.0054047462f, 0.0097898341f, -0.0025658697f, 0.0043700456f, -0.0004874461f, 0.0033436432f, 0.0005812538f,
        0.0007353674f, 0.0011039301f, -0.0006761523f, 0.0028390174f, -0.0006335808f, 0.001818893f, 0.0008254167f, 0.0030221279f,
        0.0056422142f, 0.0012962195f, 0.0258366624f, -0.0251509131f, 0.0309017552f, -0.0571445632f, -0.311910889f, 0f,
        0.2118330397f, 0.018295496f, 0.0316909408f, 0.0246440794f, -0.0052818157f, 0.009808145f, -0.0025357325f, 0.0043941239f,
        -0.0004822865f, 0.0033665502f, 0.0005785397f, 0.0007556261f, 0.001101626f, -0.0006683247f, 0.0028598137f, -0.0006312113f,
        0.001855555f, 0.0008236137f, 0.0030854268f, 0.005644202f, 0.001418955f, 0.0259533669f, -0.0250587156f, 0.0314763131f,
        -0.0574021036f, -0.312338551f, 0f, 0.2110381662f, 0.0180743526f, 0.0319094162f, 0.0245663152f, -0.0051592585f,
        0.0098258124f, -0.0025055622f, 0.0044178393f, -0.0004771049f, 0.0033892541f, 0.0005758024f, 0.0007757916f, 0.0010992645f,
        -0.00066044f, 0.0028805126f, -0.0006288066f, 0.0018921537f, 0.0008217512f, 0.0031487833f, 0.0056458719f, 0.0015422516f,
        0.0260695316f, -0.0249650807f, 0.0320528159f, -0.0576594827f, -0.3127628287f, 0f, 0.210242192f, 0.0178537807f,
        0.0321255828f, 0.0244878996f, -0.0050370783f, 0.0098428382f, -0.0024753609f, 0.0044411916f, -0.0004719015f, 0.0034117542f,
        0.0005730423f, 0.0007958629f, 0.0010968457f, -0.0006524984f, 0.0029011126f, -0.0006263667f, 0.001928687f, 0.000819829f,
        0.0032121939f, 0.0056472225f, 0.001666106f, 0.0261851494f, -0.0248700051f, 0.0326312561f, -0.0579166953f, -0.313183712f,
        0f, 0.2094451342f, 0.0176337835f, 0.0323394436f, 0.0244088385f, -0.0049152791f, 0.0098592243f, -0.0024451302f,
        0.0044641806f, -0.0004666767f, 0.0034340496f, 0.0005702595f, 0.0008158393f, 0.00109437f, -0.0006445003f, 0.0029216127f,
        -0.0006238917f, 0.0019651527f, 0.0008178472f, 0.0032756551f, 0.0056482524f, 0.0017905151f, 0.0263002131f, -0.0247734859f,
        0.0332116257f, -0.058173736f, -0.3136011912f, 0f, 0.2086470099f, 0.0174143647f, 0.0325510015f, 0.0243291378f,
        -0.0047938646f, 0.0098749724f, -0.002414872f, 0.0044868059f, -0.0004614309f, 0.0034561395f, 0.0005674541f, 0.0008357197f,
        0.0010918376f, -0.0006364459f, 0.0029420114f, -0.0006213815f, 0.0020015486f, 0.0008158057f, 0.0033391637f, 0.0056489601f,
        0.0019154755f, 0.0264147156f, -0.02467552f, 0.033793917f, -0.0584305992f, -0.3140152565f, 0f, 0.2078478364f,
        0.0171955273f, 0.0327602595f, 0.0242488037f, -0.0046728386f, 0.0098900846f, -0.0023845881f, 0.0045090672f, -0.0004561643f,
        0.0034780229f, 0.0005646264f, 0.0008555035f, 0.0010892488f, -0.0006283355f, 0.0029623075f, -0.0006188364f, 0.0020378723f,
        0.0008137045f, 0.003402716f, 0.0056493443f, 0.0020409838f, 0.0265286498f, -0.0245761044f, 0.0343781221f, -0.0586872798f,
        -0.3144258981f, 0f, 0.2070476308f, 0.0169772748f, 0.0329672206f, 0.024167842f, -0.0045522048f, 0.0099045627f,
        -0.0023542803f, 0.0045309644f, -0.0004508774f, 0.0034996992f, 0.0005617766f, 0.0008751896f, 0.0010866038f, -0.0006201695f,
        0.0029824998f, -0.0006162563f, 0.0020741218f, 0.0008115434f, 0.0034663088f, 0.0056494036f, 0.0021670367f, 0.0266420086f,
        -0.0244752361f, 0.0349642331f, -0.0589437722f, -0.3148331065f, 0f, 0.2062464103f, 0.0167596105f, 0.0331718881f,
        0.0240862588f, -0.004431967f, 0.0099184087f, -0.0023239503f, 0.0045524972f, -0.0004455706f, 0.0035211675f, 0.0005589049f,
        0.0008947773f, 0.0010839031f, -0.0006119481f, 0.0030025869f, -0.0006136413f, 0.0021102946f, 0.0008093225f, 0.0035299384f,
        0.0056491366f, 0.0022936309f, 0.0267547847f, -0.0243729123f, 0.0355522419f, -0.0592000711f, -0.3152368721f, 0f,
        0.2054441922f, 0.0165425377f, 0.033374265f, 0.02400406f, -0.0043121289f, 0.0099316245f, -0.0022936f, 0.0045736654f,
        -0.000440244f, 0.003542427f, 0.0005560114f, 0.0009142658f, 0.0010811467f, -0.0006036718f, 0.0030225676f, -0.0006109916f,
        0.0021463887f, 0.0008070417f, 0.0035936014f, 0.0056485419f, 0.0024207628f, 0.0268669712f, -0.02426913f, 0.0361421405f,
        -0.0594561711f, -0.3156371856f, 0f, 0.2046409935f, 0.0163260596f, 0.0335743546f, 0.0239212516f, -0.0041926942f,
        0.0099442122f, -0.0022632312f, 0.0045944688f, -0.0004348982f, 0.003563477f, 0.0005530963f, 0.0009336541f, 0.0010783351f,
        -0.0005953408f, 0.0030424405f, -0.0006083072f, 0.0021824019f, 0.0008047009f, 0.0036572944f, 0.0056476183f, 0.0025484289f,
        0.0269785608f, -0.0241638864f, 0.0367339206f, -0.0597120668f, -0.3160340374f, 0f, 0.2038368316f, 0.0161101794f,
        0.0337721602f, 0.0238378395f, -0.0040736663f, 0.0099561737f, -0.0022328455f, 0.0046149074f, -0.0004295334f, 0.0035843166f,
        0.0005501599f, 0.0009529415f, 0.0010754685f, -0.0005869555f, 0.0030622046f, -0.0006055881f, 0.0022183317f, 0.0008023003f,
        0.0037210138f, 0.0056463643f, 0.0026766259f, 0.0270895463f, -0.0240571786f, 0.0373275742f, -0.0599677528f, -0.3164274183f,
        0f, 0.2030317237f, 0.0158949005f, 0.0339676851f, 0.0237538299f, -0.0039550491f, 0.0099675111f, -0.0022024449f,
        0.0046349809f, -0.00042415f, 0.0036049452f, 0.0005472024f, 0.0009721272f, 0.0010725472f, -0.0005785161f, 0.0030818584f,
        -0.0006028345f, 0.0022541761f, 0.0007998396f, 0.0037847561f, 0.0056447786f, 0.00280535f, 0.0271999206f, -0.0239490038f,
        0.037923093f, -0.0602232237f, -0.316817319f, 0f, 0.2022256869f, 0.0156802259f, 0.0341609328f, 0.0236692286f,
        -0.0038368459f, 0.0099782265f, -0.002172031f, 0.0046546892f, -0.0004187483f, 0.0036253619f, 0.0005442238f, 0.0009912103f,
        0.0010695715f, -0.0005700231f, 0.0031014008f, -0.0006000465f, 0.0022899328f, 0.0007973189f, 0.0038485177f, 0.00564286f,
        0.0029345978f, 0.0273096765f, -0.0238393594f, 0.0385204685f, -0.0604784742f, -0.3172037305f, 0f, 0.2014187385f,
        0.0154661588f, 0.0343519068f, 0.0235840416f, -0.0037190604f, 0.0099883219f, -0.0021416057f, 0.0046740323f, -0.0004133287f,
        0.0036455661f, 0.0005412245f, 0.00101019f, 0.0010665416f, -0.0005614768f, 0.0031208304f, -0.0005972241f, 0.0023255995f,
        0.0007947382f, 0.0039122951f, 0.0056406071f, 0.0030643656f, 0.0274188069f, -0.0237282425f, 0.0391196924f, -0.0607334987f,
        -0.3175866437f, 0f, 0.2006108957f, 0.0152527025f, 0.0345406104f, 0.0234982749f, -0.0036016961f, 0.0099977994f,
        -0.0021111706f, 0.0046930101f, -0.0004078915f, 0.0036655571f, 0.0005382047f, 0.0010290656f, 0.001063458f, -0.0005528775f,
        0.0031401461f, -0.0005943675f, 0.0023611741f, 0.0007920974f, 0.0039760847f, 0.0056380186f, 0.0031946498f, 0.0275273046f,
        -0.0236156505f, 0.0397207563f, -0.060988292f, -0.3179660495f, 0f, 0.1998021758f, 0.01503986f, 0.0347270474f,
        0.0234119345f, -0.0034847564f, 0.0100066612f, -0.0020807276f, 0.0047116224f, -0.0004024372f, 0.0036853342f, 0.0005351645f,
        0.0010478363f, 0.0010603209f, -0.0005442255f, 0.0031593466f, -0.0005914766f, 0.0023966543f, 0.0007893966f, 0.0040398831f,
        0.0056350932f, 0.0033254467f, 0.0276351624f, -0.0235015807f, 0.0403236516f, -0.0612428487f, -0.3183419391f, 0f,
        0.1989925959f, 0.0148276345f, 0.0349112213f, 0.0233250263f, -0.0033682448f, 0.0100149094f, -0.0020502783f, 0.0047298694f,
        -0.0003969659f, 0.0037048966f, 0.0005321041f, 0.0010665012f, 0.0010571305f, -0.0005355213f, 0.0031784306f, -0.0005885517f,
        0.0024320378f, 0.0007866356f, 0.0041036865f, 0.0056318297f, 0.0034567525f, 0.0277423732f, -0.0233860305f, 0.0409283697f,
        -0.0614971632f, -0.3187143037f, 0f, 0.1981821735f, 0.014616029f, 0.0350931358f, 0.0232375564f, -0.0032521647f,
        0.0100225461f, -0.0020198246f, 0.0047477509f, -0.0003914782f, 0.0037242437f, 0.0005290237f, 0.0010850596f, 0.0010538873f
    };

    private static readonly byte[] ConstellationMaps =
    {
        80, 105, 90, 99, 108, 85, 102, 95, 80, 105, 90, 99, 108, 85, 102, 95, 80, 105, 90, 99, 108, 85, 102, 95,
        0, 73, 90, 99, 108, 85, 102, 95, 0, 73, 90, 99, 108, 85, 102, 95, 0, 73, 90, 99, 108, 85, 102, 95,
        0, 73, 90, 99, 108, 85, 102, 95, 0, 73, 90, 99, 76, 5, 102, 95, 0, 73, 90, 99, 76, 5, 102, 95,
        0, 73, 90, 99, 76, 5, 102, 95, 0, 73, 90, 99, 76, 5, 102, 95, 0, 73, 90, 99, 76, 5, 102, 95,
        0, 73, 122, 99, 76, 5, 102, 95, 0, 73, 122, 99, 76, 5, 102, 95, 0, 73, 122, 115, 76, 5, 102, 127,
        0, 73, 122, 115, 76, 5, 102, 127, 8, 73, 122, 115, 76, 5, 118, 127, 8, 73, 122, 115, 76, 5, 118, 127,
        8, 65, 122, 115, 76, 13, 118, 127, 8, 65, 122, 115, 76, 13, 118, 127, 8, 65, 106, 115, 68, 13, 118, 127,
        8, 65, 106, 115, 68, 13, 118, 127, 8, 65, 106, 83, 68, 13, 118, 111, 8, 65, 106, 83, 68, 13, 118, 111,
        8, 65, 106, 83, 68, 13, 86, 111, 8, 65, 106, 83, 68, 13, 86, 111, 8, 65, 106, 83, 68, 13, 86, 111,
        8, 65, 106, 83, 68, 13, 86, 111, 8, 65, 106, 83, 68, 13, 86, 111, 8, 65, 106, 83, 68, 13, 86, 111,
        88, 65, 106, 83, 68, 93, 86, 111, 88, 65, 106, 83, 68, 93, 86, 111, 88, 65, 106, 83, 68, 93, 86, 111,
        88, 65, 106, 83, 68, 93, 86, 111, 88, 97, 106, 83, 100, 93, 86, 111, 88, 97, 106, 83, 100, 93, 86, 111,
        80, 105, 90, 99, 108, 85, 102, 95, 80, 105, 90, 99, 108, 85, 102, 95, 80, 105, 90, 99, 108, 85, 102, 95,
        80, 105, 90, 99, 108, 85, 102, 95, 0, 73, 90, 99, 108, 85, 102, 95, 0, 73, 90, 99, 108, 85, 102, 95,
        0, 73, 90, 99, 108, 85, 102, 95, 0, 73, 90, 99, 108, 85, 102, 95, 0, 73, 90, 99, 76, 5, 102, 95,
        0, 73, 90, 99, 76, 5, 102, 95, 0, 73, 90, 99, 76, 5, 102, 95, 0, 73, 90, 99, 76, 5, 102, 95,
        0, 73, 122, 99, 76, 5, 102, 95, 0, 73, 122, 99, 76, 5, 102, 95, 0, 73, 122, 115, 76, 5, 102, 127,
        0, 73, 122, 115, 76, 5, 102, 127, 8, 73, 122, 115, 76, 5, 118, 127, 8, 73, 122, 115, 76, 5, 118, 127,
        8, 65, 122, 115, 76, 13, 118, 127, 8, 65, 122, 115, 76, 13, 118, 127, 8, 65, 106, 115, 68, 13, 118, 127,
        8, 65, 106, 115, 68, 13, 118, 127, 8, 65, 106, 83, 68, 13, 118, 111, 8, 65, 106, 83, 68, 13, 118, 111,
        8, 65, 106, 83, 68, 13, 86, 111, 8, 65, 106, 83, 68, 13, 86, 111, 8, 65, 106, 83, 68, 13, 86, 111,
        8, 65, 106, 83, 68, 13, 86, 111, 8, 65, 106, 83, 68, 13, 86, 111, 88, 65, 106, 83, 68, 93, 86, 111,
        88, 65, 106, 83, 68, 93, 86, 111, 88, 65, 106, 83, 68, 93, 86, 111, 88, 65, 106, 83, 68, 93, 86, 111,
        88, 97, 106, 83, 100, 93, 86, 111, 88, 97, 106, 83, 100, 93, 86, 111, 88, 97, 106, 83, 100, 93, 86, 111,
        80, 105, 90, 99, 108, 85, 102, 95, 80, 105, 90, 99, 108, 85, 102, 95, 80, 105, 90, 99, 108, 85, 102, 95,
        80, 105, 90, 99, 108, 85, 102, 95, 80, 105, 90, 99, 108, 85, 102, 95, 0, 73, 90, 99, 108, 85, 102, 95,
        0, 73, 90, 99, 108, 85, 102, 95, 0, 73, 90, 99, 108, 85, 102, 95, 0, 73, 90, 99, 108, 85, 102, 95,
        0, 73, 90, 99, 76, 5, 102, 95, 0, 73, 90, 99, 76, 5, 102, 95, 0, 73, 90, 99, 76, 5, 102, 95,
        0, 73, 122, 99, 76, 5, 102, 95, 0, 73, 122, 99, 76, 5, 102, 95, 0, 73, 122, 115, 76, 5, 102, 127,
        0, 73, 122, 115, 76, 5, 102, 127, 8, 73, 122, 115, 76, 5, 118, 127, 8, 73, 122, 115, 76, 5, 118, 127,
        8, 65, 122, 115, 76, 13, 118, 127, 8, 65, 122, 115, 76, 13, 118, 127, 8, 65, 106, 115, 68, 13, 118, 127,
        8, 65, 106, 115, 68, 13, 118, 127, 8, 65, 106, 83, 68, 13, 118, 111, 8, 65, 106, 83, 68, 13, 118, 111,
        8, 65, 106, 83, 68, 13, 86, 111, 8, 65, 106, 83, 68, 13, 86, 111, 8, 65, 106, 83, 68, 13, 86, 111,
        8, 65, 106, 83, 68, 13, 86, 111, 88, 65, 106, 83, 68, 93, 86, 111, 88, 65, 106, 83, 68, 93, 86, 111,
        88, 65, 106, 83, 68, 93, 86, 111, 88, 65, 106, 83, 68, 93, 86, 111, 88, 97, 106, 83, 100, 93, 86, 111,
        88, 97, 106, 83, 100, 93, 86, 111, 88, 97, 106, 83, 100, 93, 86, 111, 88, 97, 106, 83, 100, 93, 86, 111,
        80, 105, 90, 67, 108, 85, 70, 95, 80, 105, 90, 99, 108, 85, 102, 95, 80, 105, 90, 99, 108, 85, 102, 95,
        80, 105, 90, 99, 108, 85, 102, 95, 80, 105, 90, 99, 108, 85, 102, 95, 80, 105, 90, 99, 108, 85, 102, 95,
        0, 73, 90, 99, 108, 85, 102, 95, 0, 73, 90, 99, 108, 85, 102, 95, 0, 73, 90, 99, 108, 85, 102, 95,
        0, 73, 90, 99, 108, 85, 102, 95, 0, 73, 90, 99, 76, 5, 102, 95, 0, 73, 90, 99, 76, 5, 102, 95,
        0, 73, 122, 99, 76, 5, 102, 95, 0, 73, 122, 99, 76, 5, 102, 95, 0, 73, 122, 115, 76, 5, 102, 127,
        0, 73, 122, 115, 76, 5, 102, 127, 8, 73, 122, 115, 76, 5, 118, 127, 8, 73, 122, 115, 76, 5, 118, 127,
        8, 65, 122, 115, 76, 13, 118, 127, 8, 65, 122, 115, 76, 13, 118, 127, 8, 65, 106, 115, 68, 13, 118, 127,
        8, 65, 106, 115, 68, 13, 118, 127, 8, 65, 106, 83, 68, 13, 118, 111, 8, 65, 106, 83, 68, 13, 118, 111,
        8, 65, 106, 83, 68, 13, 86, 111, 8, 65, 106, 83, 68, 13, 86, 111, 8, 65, 106, 83, 68, 13, 86, 111,
        88, 65, 106, 83, 68, 93, 86, 111, 88, 65, 106, 83, 68, 93, 86, 111, 88, 65, 106, 83, 68, 93, 86, 111,
        88, 65, 106, 83, 68, 93, 86, 111, 88, 97, 106, 83, 100, 93, 86, 111, 88, 97, 106, 83, 100, 93, 86, 111,
        88, 97, 106, 83, 100, 93, 86, 111, 88, 97, 106, 83, 100, 93, 86, 111, 88, 97, 74, 3, 100, 93, 86, 111,
        80, 105, 90, 67, 108, 85, 70, 95, 80, 105, 90, 67, 108, 85, 70, 95, 80, 105, 90, 99, 108, 85, 102, 95,
        80, 105, 90, 99, 108, 85, 102, 95, 80, 105, 90, 99, 108, 85, 102, 95, 80, 105, 90, 99, 108, 85, 102, 95,
        80, 105, 90, 99, 108, 85, 102, 95, 0, 73, 90, 99, 108, 85, 102, 95, 0, 73, 90, 99, 108, 85, 102, 95,
        0, 73, 90, 99, 108, 85, 102, 95, 0, 73, 90, 99, 108, 21, 102, 95, 0, 73, 90, 99, 76, 21, 102, 95,
        0, 73, 122, 99, 76, 21, 102, 95, 0, 73, 122, 99, 76, 21, 102, 95, 0, 73, 122, 115, 76, 21, 102, 127,
        0, 73, 122, 115, 76, 21, 102, 127, 8, 73, 122, 115, 76, 21, 118, 127, 8, 73, 122, 115, 76, 21, 118, 127,
        8, 65, 122, 115, 76, 29, 118, 127, 8, 65, 122, 115, 76, 29, 118, 127, 8, 65, 106, 115, 68, 29, 118, 127,
        8, 65, 106, 115, 68, 29, 118, 127, 8, 65, 106, 83, 68, 29, 118, 111, 8, 65, 106, 83, 68, 29, 118, 111,
        8, 65, 106, 83, 68, 29, 86, 111, 8, 65, 106, 83, 68, 29, 86, 111, 88, 65, 106, 83, 68, 93, 86, 111,
        88, 65, 106, 83, 68, 93, 86, 111, 88, 65, 106, 83, 68, 93, 86, 111, 88, 65, 106, 83, 68, 93, 86, 111,
        88, 97, 106, 83, 100, 93, 86, 111, 88, 97, 106, 83, 100, 93, 86, 111, 88, 97, 106, 83, 100, 93, 86, 111,
        88, 97, 106, 83, 100, 93, 86, 111, 88, 97, 74, 3, 100, 93, 86, 111, 88, 97, 74, 3, 100, 93, 86, 111,
        80, 105, 90, 67, 108, 85, 70, 95, 80, 105, 90, 67, 108, 85, 70, 95, 80, 105, 90, 67, 108, 85, 70, 95,
        80, 105, 90, 99, 108, 85, 102, 95, 80, 105, 90, 99, 108, 85, 102, 95, 80, 105, 90, 99, 108, 85, 102, 95,
        80, 105, 90, 99, 108, 85, 102, 95, 80, 105, 90, 99, 108, 85, 102, 95, 0, 73, 90, 99, 108, 85, 102, 95,
        0, 73, 90, 99, 108, 85, 102, 95, 0, 73, 90, 99, 108, 21, 102, 95, 0, 73, 90, 99, 108, 21, 102, 95,
        0, 73, 122, 99, 76, 21, 102, 95, 0, 73, 122, 99, 76, 21, 102, 95, 0, 73, 122, 115, 76, 21, 102, 127,
        0, 73, 122, 115, 76, 21, 102, 127, 8, 73, 122, 115, 76, 21, 118, 127, 8, 73, 122, 115, 76, 21, 118, 127,
        8, 65, 122, 115, 76, 29, 118, 127, 8, 65, 122, 115, 76, 29, 118, 127, 8, 65, 106, 115, 68, 29, 118, 127,
        8, 65, 106, 115, 68, 29, 118, 127, 8, 65, 106, 83, 68, 29, 118, 111, 8, 65, 106, 83, 68, 29, 118, 111,
        8, 65, 106, 83, 68, 29, 86, 111, 88, 65, 106, 83, 68, 29, 86, 111, 88, 65, 106, 83, 68, 93, 86, 111,
        88, 65, 106, 83, 68, 93, 86, 111, 88, 65, 106, 83, 68, 93, 86, 111, 88, 97, 106, 83, 100, 93, 86, 111,
        88, 97, 106, 83, 100, 93, 86, 111, 88, 97, 106, 83, 100, 93, 86, 111, 88, 97, 106, 83, 100, 93, 86, 111,
        88, 97, 74, 3, 100, 93, 86, 111, 88, 97, 74, 3, 100, 93, 86, 111, 88, 97, 74, 3, 100, 93, 86, 111,
        80, 105, 90, 67, 108, 85, 70, 95, 80, 105, 90, 67, 108, 85, 70, 95, 80, 105, 90, 67, 108, 85, 70, 95,
        80, 105, 90, 67, 108, 85, 70, 95, 80, 105, 90, 99, 108, 85, 102, 95, 80, 105, 90, 99, 108, 85, 102, 95,
        80, 105, 90, 99, 108, 85, 102, 95, 80, 105, 90, 99, 108, 85, 102, 95, 16, 105, 90, 99, 108, 85, 102, 95,
        16, 73, 90, 99, 108, 85, 102, 95, 16, 73, 90, 99, 108, 21, 102, 95, 16, 73, 90, 99, 108, 21, 102, 95,
        16, 73, 122, 99, 44, 21, 102, 95, 16, 73, 122, 99, 44, 21, 102, 95, 16, 73, 122, 115, 44, 21, 102, 127,
        16, 73, 122, 115, 44, 21, 102, 127, 24, 73, 122, 115, 44, 21, 118, 127, 24, 73, 122, 115, 44, 21, 118, 127,
        24, 65, 122, 115, 44, 29, 118, 127, 24, 65, 122, 115, 44, 29, 118, 127, 24, 65, 106, 115, 36, 29, 118, 127,
        24, 65, 106, 115, 36, 29, 118, 127, 24, 65, 106, 83, 36, 29, 118, 111, 24, 65, 106, 83, 36, 29, 118, 111,
        88, 65, 106, 83, 36, 29, 86, 111, 88, 65, 106, 83, 36, 29, 86, 111, 88, 65, 106, 83, 36, 93, 86, 111,
        88, 65, 106, 83, 36, 93, 86, 111, 88, 97, 106, 83, 100, 93, 86, 111, 88, 97, 106, 83, 100, 93, 86, 111,
        88, 97, 106, 83, 100, 93, 86, 111, 88, 97, 106, 83, 100, 93, 86, 111, 88, 97, 74, 3, 100, 93, 86, 111,
        88, 97, 74, 3, 100, 93, 86, 111, 88, 97, 74, 3, 100, 93, 86, 111, 88, 97, 74, 3, 100, 93, 86, 111,
        80, 105, 10, 67, 108, 85, 70, 15, 80, 105, 90, 67, 108, 85, 70, 95, 80, 105, 90, 67, 108, 85, 70, 95,
        80, 105, 90, 67, 108, 85, 70, 95, 80, 105, 90, 67, 108, 85, 70, 95, 80, 105, 90, 99, 108, 85, 102, 95,
        80, 105, 90, 99, 108, 85, 102, 95, 80, 105, 90, 99, 108, 85, 102, 95, 16, 105, 90, 99, 108, 85, 102, 95,
        16, 105, 90, 99, 108, 85, 102, 95, 16, 73, 90, 99, 108, 21, 102, 95, 16, 73, 90, 99, 108, 21, 102, 95,
        16, 73, 122, 99, 44, 21, 102, 95, 16, 73, 122, 99, 44, 21, 102, 95, 16, 73, 122, 115, 44, 21, 102, 127,
        16, 73, 122, 115, 44, 21, 102, 127, 24, 73, 122, 115, 44, 21, 118, 127, 24, 73, 122, 115, 44, 21, 118, 127,
        24, 65, 122, 115, 44, 29, 118, 127, 24, 65, 122, 115, 44, 29, 118, 127, 24, 65, 106, 115, 36, 29, 118, 127,
        24, 65, 106, 115, 36, 29, 118, 127, 24, 65, 106, 83, 36, 29, 118, 111, 24, 65, 106, 83, 36, 29, 118, 111,
        88, 65, 106, 83, 36, 29, 86, 111, 88, 65, 106, 83, 36, 29, 86, 111, 88, 65, 106, 83, 36, 93, 86, 111,
        88, 97, 106, 83, 36, 93, 86, 111, 88, 97, 106, 83, 100, 93, 86, 111, 88, 97, 106, 83, 100, 93, 86, 111,
        88, 97, 106, 83, 100, 93, 86, 111, 88, 97, 74, 3, 100, 93, 86, 111, 88, 97, 74, 3, 100, 93, 86, 111,
        88, 97, 74, 3, 100, 93, 86, 111, 88, 97, 74, 3, 100, 93, 86, 111, 88, 97, 74, 3, 100, 93, 6, 79,
        80, 105, 10, 67, 108, 85, 70, 15, 80, 105, 10, 67, 108, 85, 70, 15, 80, 105, 90, 67, 108, 85, 70, 95,
        80, 105, 90, 67, 108, 85, 70, 95, 80, 105, 90, 67, 108, 85, 70, 95, 80, 105, 90, 67, 108, 85, 70, 95,
        80, 105, 90, 35, 108, 85, 102, 95, 80, 105, 90, 35, 108, 85, 102, 95, 16, 105, 90, 35, 108, 85, 102, 95,
        16, 105, 90, 35, 108, 85, 102, 95, 16, 41, 90, 35, 108, 21, 102, 95, 16, 41, 90, 35, 108, 21, 102, 95,
        16, 41, 122, 35, 44, 21, 102, 95, 16, 41, 122, 35, 44, 21, 102, 95, 16, 41, 122, 51, 44, 21, 102, 127,
        16, 41, 122, 51, 44, 21, 102, 127, 24, 41, 122, 51, 44, 21, 118, 127, 24, 41, 122, 51, 44, 21, 118, 127,
        24, 33, 122, 51, 44, 29, 118, 127, 24, 33, 122, 51, 44, 29, 118, 127, 24, 33, 106, 51, 36, 29, 118, 127,
        24, 33, 106, 51, 36, 29, 118, 127, 24, 33, 106, 19, 36, 29, 118, 111, 24, 33, 106, 19, 36, 29, 118, 111,
        88, 33, 106, 19, 36, 29, 86, 111, 88, 33, 106, 19, 36, 29, 86, 111, 88, 97, 106, 19, 36, 93, 86, 111,
        88, 97, 106, 19, 36, 93, 86, 111, 88, 97, 106, 19, 100, 93, 86, 111, 88, 97, 106, 19, 100, 93, 86, 111,
        88, 97, 74, 3, 100, 93, 86, 111, 88, 97, 74, 3, 100, 93, 86, 111, 88, 97, 74, 3, 100, 93, 86, 111,
        88, 97, 74, 3, 100, 93, 86, 111, 88, 97, 74, 3, 100, 93, 6, 79, 88, 97, 74, 3, 100, 93, 6, 79,
        80, 105, 10, 67, 108, 85, 70, 15, 80, 105, 10, 67, 108, 85, 70, 15, 80, 105, 10, 67, 108, 85, 70, 15,
        80, 105, 90, 67, 108, 85, 70, 95, 80, 105, 90, 67, 108, 85, 70, 95, 80, 105, 90, 67, 108, 85, 70, 95,
        80, 105, 90, 35, 108, 85, 70, 95, 80, 105, 90, 35, 108, 85, 102, 95, 16, 105, 90, 35, 108, 85, 102, 95,
        16, 105, 90, 35, 108, 85, 102, 95, 16, 41, 90, 35, 108, 21, 102, 95, 16, 41, 90, 35, 108, 21, 102, 95,
        16, 41, 122, 35, 44, 21, 102, 95, 16, 41, 122, 35, 44, 21, 102, 95, 16, 41, 122, 51, 44, 21, 102, 127,
        16, 41, 122, 51, 44, 21, 102, 127, 24, 41, 122, 51, 44, 21, 118, 127, 24, 41, 122, 51, 44, 21, 118, 127,
        24, 33, 122, 51, 44, 29, 118, 127, 24, 33, 122, 51, 44, 29, 118, 127, 24, 33, 106, 51, 36, 29, 118, 127,
        24, 33, 106, 51, 36, 29, 118, 127, 24, 33, 106, 19, 36, 29, 118, 111, 24, 33, 106, 19, 36, 29, 118, 111,
        88, 33, 106, 19, 36, 29, 86, 111, 88, 33, 106, 19, 36, 29, 86, 111, 88, 97, 106, 19, 36, 93, 86, 111,
        88, 97, 106, 19, 36, 93, 86, 111, 88, 97, 106, 19, 100, 93, 86, 111, 88, 97, 74, 19, 100, 93, 86, 111,
        88, 97, 74, 3, 100, 93, 86, 111, 88, 97, 74, 3, 100, 93, 86, 111, 88, 97, 74, 3, 100, 93, 86, 111,
        88, 97, 74, 3, 100, 93, 6, 79, 88, 97, 74, 3, 100, 93, 6, 79, 88, 97, 74, 3, 100, 93, 6, 79,
        80, 105, 10, 67, 108, 85, 70, 15, 80, 105, 10, 67, 108, 85, 70, 15, 80, 105, 10, 67, 108, 85, 70, 15,
        80, 105, 10, 67, 108, 85, 70, 15, 80, 105, 26, 67, 108, 85, 70, 95, 80, 105, 26, 67, 108, 85, 70, 95,
        80, 105, 26, 35, 108, 85, 70, 95, 80, 105, 26, 35, 108, 85, 70, 95, 16, 105, 26, 35, 108, 85, 38, 95,
        16, 105, 26, 35, 108, 85, 38, 95, 16, 41, 26, 35, 108, 21, 38, 95, 16, 41, 26, 35, 108, 21, 38, 95,
        16, 41, 58, 35, 44, 21, 38, 95, 16, 41, 58, 35, 44, 21, 38, 95, 16, 41, 58, 51, 44, 21, 38, 127,
        16, 41, 58, 51, 44, 21, 38, 127, 24, 41, 58, 51, 44, 21, 54, 127, 24, 41, 58, 51, 44, 21, 54, 127,
        24, 33, 58, 51, 44, 29, 54, 127, 24, 33, 58, 51, 44, 29, 54, 127, 24, 33, 42, 51, 36, 29, 54, 127,
        24, 33, 42, 51, 36, 29, 54, 127, 24, 33, 42, 19, 36, 29, 54, 111, 24, 33, 42, 19, 36, 29, 54, 111,
        88, 33, 42, 19, 36, 29, 22, 111, 88, 33, 42, 19, 36, 29, 22, 111, 88, 97, 42, 19, 36, 93, 22, 111,
        88, 97, 42, 19, 36, 93, 22, 111, 88, 97, 74, 19, 100, 93, 22, 111, 88, 97, 74, 19, 100, 93, 22, 111,
        88, 97, 74, 3, 100, 93, 22, 111, 88, 97, 74, 3, 100, 93, 22, 111, 88, 97, 74, 3, 100, 93, 6, 79,
        88, 97, 74, 3, 100, 93, 6, 79, 88, 97, 74, 3, 100, 93, 6, 79, 88, 97, 74, 3, 100, 93, 6, 79,
        80, 105, 10, 67, 108, 85, 70, 15, 80, 105, 10, 67, 108, 85, 70, 15, 80, 105, 10, 67, 108, 85, 70, 15,
        80, 105, 10, 67, 108, 85, 70, 15, 80, 105, 26, 67, 108, 85, 70, 15, 80, 105, 26, 67, 108, 85, 70, 95,
        80, 105, 26, 35, 108, 85, 70, 95, 80, 105, 26, 35, 108, 85, 70, 95, 16, 105, 26, 35, 108, 85, 38, 95,
        16, 105, 26, 35, 108, 85, 38, 95, 16, 41, 26, 35, 108, 21, 38, 95, 16, 41, 26, 35, 108, 21, 38, 95,
        16, 41, 58, 35, 44, 21, 38, 95, 16, 41, 58, 35, 44, 21, 38, 95, 16, 41, 58, 51, 44, 21, 38, 127,
        16, 41, 58, 51, 44, 21, 38, 127, 24, 41, 58, 51, 44, 21, 54, 127, 24, 41, 58, 51, 44, 21, 54, 127,
        24, 33, 58, 51, 44, 29, 54, 127, 24, 33, 58, 51, 44, 29, 54, 127, 24, 33, 42, 51, 36, 29, 54, 127,
        24, 33, 42, 51, 36, 29, 54, 127, 24, 33, 42, 19, 36, 29, 54, 111, 24, 33, 42, 19, 36, 29, 54, 111,
        88, 33, 42, 19, 36, 29, 22, 111, 88, 33, 42, 19, 36, 29, 22, 111, 88, 97, 42, 19, 36, 93, 22, 111,
        88, 97, 42, 19, 36, 93, 22, 111, 88, 97, 74, 19, 100, 93, 22, 111, 88, 97, 74, 19, 100, 93, 22, 111,
        88, 97, 74, 3, 100, 93, 22, 111, 88, 97, 74, 3, 100, 93, 22, 79, 88, 97, 74, 3, 100, 93, 6, 79,
        88, 97, 74, 3, 100, 93, 6, 79, 88, 97, 74, 3, 100, 93, 6, 79, 88, 97, 74, 3, 100, 93, 6, 79,
        80, 105, 10, 67, 108, 117, 70, 15, 80, 105, 10, 67, 108, 117, 70, 15, 80, 105, 10, 67, 108, 117, 70, 15,
        80, 105, 10, 67, 108, 117, 70, 15, 80, 105, 26, 67, 108, 117, 70, 15, 80, 105, 26, 67, 108, 117, 70, 15,
        80, 105, 26, 35, 108, 117, 70, 31, 80, 105, 26, 35, 108, 117, 70, 31, 16, 105, 26, 35, 108, 117, 38, 31,
        16, 105, 26, 35, 108, 117, 38, 31, 16, 41, 26, 35, 108, 53, 38, 31, 16, 41, 26, 35, 108, 53, 38, 31,
        16, 41, 58, 35, 44, 53, 38, 31, 16, 41, 58, 35, 44, 53, 38, 31, 16, 41, 58, 51, 44, 53, 38, 63,
        16, 41, 58, 51, 44, 53, 38, 63, 24, 41, 58, 51, 44, 53, 54, 63, 24, 41, 58, 51, 44, 53, 54, 63,
        24, 33, 58, 51, 44, 61, 54, 63, 24, 33, 58, 51, 44, 61, 54, 63, 24, 33, 42, 51, 36, 61, 54, 63,
        24, 33, 42, 51, 36, 61, 54, 63, 24, 33, 42, 19, 36, 61, 54, 47, 24, 33, 42, 19, 36, 61, 54, 47,
        88, 33, 42, 19, 36, 61, 22, 47, 88, 33, 42, 19, 36, 61, 22, 47, 88, 97, 42, 19, 36, 125, 22, 47,
        88, 97, 42, 19, 36, 125, 22, 47, 88, 97, 74, 19, 100, 125, 22, 47, 88, 97, 74, 19, 100, 125, 22, 47,
        88, 97, 74, 3, 100, 125, 22, 79, 88, 97, 74, 3, 100, 125, 22, 79, 88, 97, 74, 3, 100, 125, 6, 79,
        88, 97, 74, 3, 100, 125, 6, 79, 88, 97, 74, 3, 100, 125, 6, 79, 88, 97, 74, 3, 100, 125, 6, 79,
        80, 105, 10, 67, 108, 117, 70, 15, 80, 105, 10, 67, 108, 117, 70, 15, 80, 105, 10, 67, 108, 117, 70, 15,
        80, 105, 10, 67, 108, 117, 70, 15, 80, 105, 26, 67, 108, 117, 70, 15, 80, 105, 26, 67, 108, 117, 70, 15,
        80, 105, 26, 35, 108, 117, 70, 31, 80, 105, 26, 35, 108, 117, 70, 31, 16, 105, 26, 35, 108, 117, 38, 31,
        16, 105, 26, 35, 108, 117, 38, 31, 16, 41, 26, 35, 108, 53, 38, 31, 16, 41, 26, 35, 108, 53, 38, 31,
        16, 41, 58, 35, 44, 53, 38, 31, 16, 41, 58, 35, 44, 53, 38, 31, 16, 41, 58, 51, 44, 53, 38, 63,
        16, 41, 58, 51, 44, 53, 38, 63, 24, 41, 58, 51, 44, 53, 54, 63, 24, 41, 58, 51, 44, 53, 54, 63,
        24, 33, 58, 51, 44, 61, 54, 63, 24, 33, 58, 51, 44, 61, 54, 63, 24, 33, 42, 51, 36, 61, 54, 63,
        24, 33, 42, 51, 36, 61, 54, 63, 24, 33, 42, 19, 36, 61, 54, 47, 24, 33, 42, 19, 36, 61, 54, 47,
        88, 33, 42, 19, 36, 61, 22, 47, 88, 33, 42, 19, 36, 61, 22, 47, 88, 97, 42, 19, 36, 125, 22, 47,
        88, 97, 42, 19, 36, 125, 22, 47, 88, 97, 74, 19, 100, 125, 22, 47, 88, 97, 74, 19, 100, 125, 22, 47,
        88, 97, 74, 3, 100, 125, 22, 79, 88, 97, 74, 3, 100, 125, 22, 79, 88, 97, 74, 3, 100, 125, 6, 79,
        88, 97, 74, 3, 100, 125, 6, 79, 88, 97, 74, 3, 100, 125, 6, 79, 88, 97, 74, 3, 100, 125, 6, 79,
        112, 105, 10, 67, 124, 117, 70, 15, 112, 105, 10, 67, 124, 117, 70, 15, 112, 105, 10, 67, 124, 117, 70, 15,
        112, 105, 10, 67, 124, 117, 70, 15, 112, 105, 26, 67, 124, 117, 70, 15, 112, 105, 26, 67, 124, 117, 70, 15,
        112, 105, 26, 35, 124, 117, 70, 31, 112, 105, 26, 35, 124, 117, 70, 31, 48, 105, 26, 35, 124, 117, 38, 31,
        48, 105, 26, 35, 124, 117, 38, 31, 48, 41, 26, 35, 124, 53, 38, 31, 48, 41, 26, 35, 124, 53, 38, 31,
        48, 41, 58, 35, 60, 53, 38, 31, 48, 41, 58, 35, 60, 53, 38, 31, 48, 41, 58, 51, 60, 53, 38, 63,
        48, 41, 58, 51, 60, 53, 38, 63, 56, 41, 58, 51, 60, 53, 54, 63, 56, 41, 58, 51, 60, 53, 54, 63,
        56, 33, 58, 51, 60, 61, 54, 63, 56, 33, 58, 51, 60, 61, 54, 63, 56, 33, 42, 51, 52, 61, 54, 63,
        56, 33, 42, 51, 52, 61, 54, 63, 56, 33, 42, 19, 52, 61, 54, 47, 56, 33, 42, 19, 52, 61, 54, 47,
        120, 33, 42, 19, 52, 61, 22, 47, 120, 33, 42, 19, 52, 61, 22, 47, 120, 97, 42, 19, 52, 125, 22, 47,
        120, 97, 42, 19, 52, 125, 22, 47, 120, 97, 74, 19, 116, 125, 22, 47, 120, 97, 74, 19, 116, 125, 22, 47,
        120, 97, 74, 3, 116, 125, 22, 79, 120, 97, 74, 3, 116, 125, 22, 79, 120, 97, 74, 3, 116, 125, 6, 79,
        120, 97, 74, 3, 116, 125, 6, 79, 120, 97, 74, 3, 116, 125, 6, 79, 120, 97, 74, 3, 116, 125, 6, 79,
        112, 105, 10, 67, 124, 117, 70, 15, 112, 105, 10, 67, 124, 117, 70, 15, 112, 105, 10, 67, 124, 117, 70, 15,
        112, 105, 10, 67, 124, 117, 70, 15, 112, 105, 26, 67, 124, 117, 70, 15, 112, 105, 26, 67, 124, 117, 70, 15,
        112, 105, 26, 35, 124, 117, 70, 31, 112, 105, 26, 35, 124, 117, 70, 31, 48, 105, 26, 35, 124, 117, 38, 31,
        48, 105, 26, 35, 124, 117, 38, 31, 48, 41, 26, 35, 124, 53, 38, 31, 48, 41, 26, 35, 124, 53, 38, 31,
        48, 41, 58, 35, 60, 53, 38, 31, 48, 41, 58, 35, 60, 53, 38, 31, 48, 41, 58, 51, 60, 53, 38, 63,
        48, 41, 58, 51, 60, 53, 38, 63, 56, 41, 58, 51, 60, 53, 54, 63, 56, 41, 58, 51, 60, 53, 54, 63,
        56, 33, 58, 51, 60, 61, 54, 63, 56, 33, 58, 51, 60, 61, 54, 63, 56, 33, 42, 51, 52, 61, 54, 63,
        56, 33, 42, 51, 52, 61, 54, 63, 56, 33, 42, 19, 52, 61, 54, 47, 56, 33, 42, 19, 52, 61, 54, 47,
        120, 33, 42, 19, 52, 61, 22, 47, 120, 33, 42, 19, 52, 61, 22, 47, 120, 97, 42, 19, 52, 125, 22, 47,
        120, 97, 42, 19, 52, 125, 22, 47, 120, 97, 74, 19, 116, 125, 22, 47, 120, 97, 74, 19, 116, 125, 22, 47,
        120, 97, 74, 3, 116, 125, 22, 79, 120, 97, 74, 3, 116, 125, 22, 79, 120, 97, 74, 3, 116, 125, 6, 79,
        120, 97, 74, 3, 116, 125, 6, 79, 120, 97, 74, 3, 116, 125, 6, 79, 120, 97, 74, 3, 116, 125, 6, 79,
        112, 121, 10, 75, 124, 117, 70, 15, 112, 121, 10, 75, 124, 117, 70, 15, 112, 121, 10, 75, 124, 117, 70, 15,
        112, 121, 10, 75, 124, 117, 70, 15, 112, 121, 26, 75, 124, 117, 70, 15, 112, 121, 26, 75, 124, 117, 70, 15,
        112, 121, 26, 43, 124, 117, 70, 31, 112, 121, 26, 43, 124, 117, 70, 31, 48, 121, 26, 43, 124, 117, 38, 31,
        48, 121, 26, 43, 124, 117, 38, 31, 48, 57, 26, 43, 124, 53, 38, 31, 48, 57, 26, 43, 124, 53, 38, 31,
        48, 57, 58, 43, 60, 53, 38, 31, 48, 57, 58, 43, 60, 53, 38, 31, 48, 57, 58, 59, 60, 53, 38, 63,
        48, 57, 58, 59, 60, 53, 38, 63, 56, 57, 58, 59, 60, 53, 54, 63, 56, 57, 58, 59, 60, 53, 54, 63,
        56, 49, 58, 59, 60, 61, 54, 63, 56, 49, 58, 59, 60, 61, 54, 63, 56, 49, 42, 59, 52, 61, 54, 63,
        56, 49, 42, 59, 52, 61, 54, 63, 56, 49, 42, 27, 52, 61, 54, 47, 56, 49, 42, 27, 52, 61, 54, 47,
        120, 49, 42, 27, 52, 61, 22, 47, 120, 49, 42, 27, 52, 61, 22, 47, 120, 113, 42, 27, 52, 125, 22, 47,
        120, 113, 42, 27, 52, 125, 22, 47, 120, 113, 74, 27, 116, 125, 22, 47, 120, 113, 74, 27, 116, 125, 22, 47,
        120, 113, 74, 11, 116, 125, 22, 79, 120, 113, 74, 11, 116, 125, 22, 79, 120, 113, 74, 11, 116, 125, 6, 79,
        120, 113, 74, 11, 116, 125, 6, 79, 120, 113, 74, 11, 116, 125, 6, 79, 120, 113, 74, 11, 116, 125, 6, 79,
        112, 121, 10, 75, 124, 117, 70, 15, 112, 121, 10, 75, 124, 117, 70, 15, 112, 121, 10, 75, 124, 117, 70, 15,
        112, 121, 10, 75, 124, 117, 70, 15, 112, 121, 26, 75, 124, 117, 70, 15, 112, 121, 26, 75, 124, 117, 70, 15,
        112, 121, 26, 43, 124, 117, 70, 31, 112, 121, 26, 43, 124, 117, 70, 31, 48, 121, 26, 43, 124, 117, 38, 31,
        48, 121, 26, 43, 124, 117, 38, 31, 48, 57, 26, 43, 124, 53, 38, 31, 48, 57, 26, 43, 124, 53, 38, 31,
        48, 57, 58, 43, 60, 53, 38, 31, 48, 57, 58, 43, 60, 53, 38, 31, 48, 57, 58, 59, 60, 53, 38, 63,
        48, 57, 58, 59, 60, 53, 38, 63, 56, 57, 58, 59, 60, 53, 54, 63, 56, 57, 58, 59, 60, 53, 54, 63,
        56, 49, 58, 59, 60, 61, 54, 63, 56, 49, 58, 59, 60, 61, 54, 63, 56, 49, 42, 59, 52, 61, 54, 63,
        56, 49, 42, 59, 52, 61, 54, 63, 56, 49, 42, 27, 52, 61, 54, 47, 56, 49, 42, 27, 52, 61, 54, 47,
        120, 49, 42, 27, 52, 61, 22, 47, 120, 49, 42, 27, 52, 61, 22, 47, 120, 113, 42, 27, 52, 125, 22, 47,
        120, 113, 42, 27, 52, 125, 22, 47, 120, 113, 74, 27, 116, 125, 22, 47, 120, 113, 74, 27, 116, 125, 22, 47,
        120, 113, 74, 11, 116, 125, 22, 79, 120, 113, 74, 11, 116, 125, 22, 79, 120, 113, 74, 11, 116, 125, 6, 79,
        120, 113, 74, 11, 116, 125, 6, 79, 120, 113, 74, 11, 116, 125, 6, 79, 120, 113, 74, 11, 116, 125, 6, 79,
        112, 121, 2, 75, 124, 117, 78, 15, 112, 121, 2, 75, 124, 117, 78, 15, 112, 121, 2, 75, 124, 117, 78, 15,
        112, 121, 2, 75, 124, 117, 78, 15, 112, 121, 18, 75, 124, 117, 78, 15, 112, 121, 18, 75, 124, 117, 78, 15,
        112, 121, 18, 43, 124, 117, 78, 31, 112, 121, 18, 43, 124, 117, 78, 31, 48, 121, 18, 43, 124, 117, 46, 31,
        48, 121, 18, 43, 124, 117, 46, 31, 48, 57, 18, 43, 124, 53, 46, 31, 48, 57, 18, 43, 124, 53, 46, 31,
        48, 57, 50, 43, 60, 53, 46, 31, 48, 57, 50, 43, 60, 53, 46, 31, 48, 57, 50, 59, 60, 53, 46, 63,
        48, 57, 50, 59, 60, 53, 46, 63, 56, 57, 50, 59, 60, 53, 62, 63, 56, 57, 50, 59, 60, 53, 62, 63,
        56, 49, 50, 59, 60, 61, 62, 63, 56, 49, 50, 59, 60, 61, 62, 63, 56, 49, 34, 59, 52, 61, 62, 63,
        56, 49, 34, 59, 52, 61, 62, 63, 56, 49, 34, 27, 52, 61, 62, 47, 56, 49, 34, 27, 52, 61, 62, 47,
        120, 49, 34, 27, 52, 61, 30, 47, 120, 49, 34, 27, 52, 61, 30, 47, 120, 113, 34, 27, 52, 125, 30, 47,
        120, 113, 34, 27, 52, 125, 30, 47, 120, 113, 66, 27, 116, 125, 30, 47, 120, 113, 66, 27, 116, 125, 30, 47,
        120, 113, 66, 11, 116, 125, 30, 79, 120, 113, 66, 11, 116, 125, 30, 79, 120, 113, 66, 11, 116, 125, 14, 79,
        120, 113, 66, 11, 116, 125, 14, 79, 120, 113, 66, 11, 116, 125, 14, 79, 120, 113, 66, 11, 116, 125, 14, 79,
        112, 121, 2, 75, 124, 117, 78, 15, 112, 121, 2, 75, 124, 117, 78, 15, 112, 121, 2, 75, 124, 117, 78, 15,
        112, 121, 2, 75, 124, 117, 78, 15, 112, 121, 18, 75, 124, 117, 78, 15, 112, 121, 18, 75, 124, 117, 78, 15,
        112, 121, 18, 43, 124, 117, 78, 31, 112, 121, 18, 43, 124, 117, 78, 31, 48, 121, 18, 43, 124, 117, 46, 31,
        48, 121, 18, 43, 124, 117, 46, 31, 48, 57, 18, 43, 124, 53, 46, 31, 48, 57, 18, 43, 124, 53, 46, 31,
        48, 57, 50, 43, 60, 53, 46, 31, 48, 57, 50, 43, 60, 53, 46, 31, 48, 57, 50, 59, 60, 53, 46, 63,
        48, 57, 50, 59, 60, 53, 46, 63, 56, 57, 50, 59, 60, 53, 62, 63, 56, 57, 50, 59, 60, 53, 62, 63,
        56, 49, 50, 59, 60, 61, 62, 63, 56, 49, 50, 59, 60, 61, 62, 63, 56, 49, 34, 59, 52, 61, 62, 63,
        56, 49, 34, 59, 52, 61, 62, 63, 56, 49, 34, 27, 52, 61, 62, 47, 56, 49, 34, 27, 52, 61, 62, 47,
        120, 49, 34, 27, 52, 61, 30, 47, 120, 49, 34, 27, 52, 61, 30, 47, 120, 113, 34, 27, 52, 125, 30, 47,
        120, 113, 34, 27, 52, 125, 30, 47, 120, 113, 66, 27, 116, 125, 30, 47, 120, 113, 66, 27, 116, 125, 30, 47,
        120, 113, 66, 11, 116, 125, 30, 79, 120, 113, 66, 11, 116, 125, 30, 79, 120, 113, 66, 11, 116, 125, 14, 79,
        120, 113, 66, 11, 116, 125, 14, 79, 120, 113, 66, 11, 116, 125, 14, 79, 120, 113, 66, 11, 116, 125, 14, 79,
        112, 121, 2, 75, 124, 101, 78, 7, 112, 121, 2, 75, 124, 101, 78, 7, 112, 121, 2, 75, 124, 101, 78, 7,
        112, 121, 2, 75, 124, 101, 78, 7, 112, 121, 18, 75, 124, 101, 78, 7, 112, 121, 18, 75, 124, 101, 78, 7,
        112, 121, 18, 43, 124, 101, 78, 23, 112, 121, 18, 43, 124, 101, 78, 23, 48, 121, 18, 43, 124, 101, 46, 23,
        48, 121, 18, 43, 124, 101, 46, 23, 48, 57, 18, 43, 124, 37, 46, 23, 48, 57, 18, 43, 124, 37, 46, 23,
        48, 57, 50, 43, 60, 37, 46, 23, 48, 57, 50, 43, 60, 37, 46, 23, 48, 57, 50, 59, 60, 37, 46, 55,
        48, 57, 50, 59, 60, 37, 46, 55, 56, 57, 50, 59, 60, 37, 62, 55, 56, 57, 50, 59, 60, 37, 62, 55,
        56, 49, 50, 59, 60, 45, 62, 55, 56, 49, 50, 59, 60, 45, 62, 55, 56, 49, 34, 59, 52, 45, 62, 55,
        56, 49, 34, 59, 52, 45, 62, 55, 56, 49, 34, 27, 52, 45, 62, 39, 56, 49, 34, 27, 52, 45, 62, 39,
        120, 49, 34, 27, 52, 45, 30, 39, 120, 49, 34, 27, 52, 45, 30, 39, 120, 113, 34, 27, 52, 109, 30, 39,
        120, 113, 34, 27, 52, 109, 30, 39, 120, 113, 66, 27, 116, 109, 30, 39, 120, 113, 66, 27, 116, 109, 30, 39,
        120, 113, 66, 11, 116, 109, 30, 71, 120, 113, 66, 11, 116, 109, 30, 71, 120, 113, 66, 11, 116, 109, 14, 71,
        120, 113, 66, 11, 116, 109, 14, 71, 120, 113, 66, 11, 116, 109, 14, 71, 120, 113, 66, 11, 116, 109, 14, 71,
        112, 121, 2, 75, 124, 101, 78, 7, 112, 121, 2, 75, 124, 101, 78, 7, 112, 121, 2, 75, 124, 101, 78, 7,
        112, 121, 2, 75, 124, 101, 78, 7, 112, 121, 18, 75, 124, 101, 78, 7, 112, 121, 18, 75, 124, 101, 78, 7,
        112, 121, 18, 43, 124, 101, 78, 23, 112, 121, 18, 43, 124, 101, 78, 23, 48, 121, 18, 43, 124, 101, 46, 23,
        48, 121, 18, 43, 124, 101, 46, 23, 48, 57, 18, 43, 124, 37, 46, 23, 48, 57, 18, 43, 124, 37, 46, 23,
        48, 57, 50, 43, 60, 37, 46, 23, 48, 57, 50, 43, 60, 37, 46, 23, 48, 57, 50, 59, 60, 37, 46, 55,
        48, 57, 50, 59, 60, 37, 46, 55, 56, 57, 50, 59, 60, 37, 62, 55, 56, 57, 50, 59, 60, 37, 62, 55,
        56, 49, 50, 59, 60, 45, 62, 55, 56, 49, 50, 59, 60, 45, 62, 55, 56, 49, 34, 59, 52, 45, 62, 55,
        56, 49, 34, 59, 52, 45, 62, 55, 56, 49, 34, 27, 52, 45, 62, 39, 56, 49, 34, 27, 52, 45, 62, 39,
        120, 49, 34, 27, 52, 45, 30, 39, 120, 49, 34, 27, 52, 45, 30, 39, 120, 113, 34, 27, 52, 109, 30, 39,
        120, 113, 34, 27, 52, 109, 30, 39, 120, 113, 66, 27, 116, 109, 30, 39, 120, 113, 66, 27, 116, 109, 30, 39,
        120, 113, 66, 11, 116, 109, 30, 71, 120, 113, 66, 11, 116, 109, 30, 71, 120, 113, 66, 11, 116, 109, 14, 71,
        120, 113, 66, 11, 116, 109, 14, 71, 120, 113, 66, 11, 116, 109, 14, 71, 120, 113, 66, 11, 116, 109, 14, 71,
        96, 121, 2, 75, 92, 101, 78, 7, 96, 121, 2, 75, 92, 101, 78, 7, 96, 121, 2, 75, 92, 101, 78, 7,
        96, 121, 2, 75, 92, 101, 78, 7, 96, 121, 18, 75, 92, 101, 78, 7, 96, 121, 18, 75, 92, 101, 78, 7,
        96, 121, 18, 43, 92, 101, 78, 23, 96, 121, 18, 43, 92, 101, 78, 23, 32, 121, 18, 43, 92, 101, 46, 23,
        32, 121, 18, 43, 92, 101, 46, 23, 32, 57, 18, 43, 92, 37, 46, 23, 32, 57, 18, 43, 92, 37, 46, 23,
        32, 57, 50, 43, 28, 37, 46, 23, 32, 57, 50, 43, 28, 37, 46, 23, 32, 57, 50, 59, 28, 37, 46, 55,
        32, 57, 50, 59, 28, 37, 46, 55, 40, 57, 50, 59, 28, 37, 62, 55, 40, 57, 50, 59, 28, 37, 62, 55,
        40, 49, 50, 59, 28, 45, 62, 55, 40, 49, 50, 59, 28, 45, 62, 55, 40, 49, 34, 59, 20, 45, 62, 55,
        40, 49, 34, 59, 20, 45, 62, 55, 40, 49, 34, 27, 20, 45, 62, 39, 40, 49, 34, 27, 20, 45, 62, 39,
        104, 49, 34, 27, 20, 45, 30, 39, 104, 49, 34, 27, 20, 45, 30, 39, 104, 113, 34, 27, 20, 109, 30, 39,
        104, 113, 34, 27, 20, 109, 30, 39, 104, 113, 66, 27, 84, 109, 30, 39, 104, 113, 66, 27, 84, 109, 30, 39,
        104, 113, 66, 11, 84, 109, 30, 71, 104, 113, 66, 11, 84, 109, 30, 71, 104, 113, 66, 11, 84, 109, 14, 71,
        104, 113, 66, 11, 84, 109, 14, 71, 104, 113, 66, 11, 84, 109, 14, 71, 104, 113, 66, 11, 84, 109, 14, 71,
        96, 121, 2, 75, 92, 101, 78, 7, 96, 121, 2, 75, 92, 101, 78, 7, 96, 121, 2, 75, 92, 101, 78, 7,
        96, 121, 2, 75, 92, 101, 78, 7, 96, 121, 18, 75, 92, 101, 78, 7, 96, 121, 18, 75, 92, 101, 78, 7,
        96, 121, 18, 43, 92, 101, 78, 23, 96, 121, 18, 43, 92, 101, 78, 23, 32, 121, 18, 43, 92, 101, 46, 23,
        32, 121, 18, 43, 92, 101, 46, 23, 32, 57, 18, 43, 92, 37, 46, 23, 32, 57, 18, 43, 92, 37, 46, 23,
        32, 57, 50, 43, 28, 37, 46, 23, 32, 57, 50, 43, 28, 37, 46, 23, 32, 57, 50, 59, 28, 37, 46, 55,
        32, 57, 50, 59, 28, 37, 46, 55, 40, 57, 50, 59, 28, 37, 62, 55, 40, 57, 50, 59, 28, 37, 62, 55,
        40, 49, 50, 59, 28, 45, 62, 55, 40, 49, 50, 59, 28, 45, 62, 55, 40, 49, 34, 59, 20, 45, 62, 55,
        40, 49, 34, 59, 20, 45, 62, 55, 40, 49, 34, 27, 20, 45, 62, 39, 40, 49, 34, 27, 20, 45, 62, 39,
        104, 49, 34, 27, 20, 45, 30, 39, 104, 49, 34, 27, 20, 45, 30, 39, 104, 113, 34, 27, 20, 109, 30, 39,
        104, 113, 34, 27, 20, 109, 30, 39, 104, 113, 66, 27, 84, 109, 30, 39, 104, 113, 66, 27, 84, 109, 30, 39,
        104, 113, 66, 11, 84, 109, 30, 71, 104, 113, 66, 11, 84, 109, 30, 71, 104, 113, 66, 11, 84, 109, 14, 71,
        104, 113, 66, 11, 84, 109, 14, 71, 104, 113, 66, 11, 84, 109, 14, 71, 104, 113, 66, 11, 84, 109, 14, 71,
        96, 89, 2, 75, 92, 101, 78, 7, 96, 89, 2, 75, 92, 101, 78, 7, 96, 89, 2, 75, 92, 101, 78, 7,
        96, 89, 2, 75, 92, 101, 78, 7, 96, 89, 18, 75, 92, 101, 78, 7, 96, 89, 18, 75, 92, 101, 78, 7,
        96, 89, 18, 107, 92, 101, 78, 23, 96, 89, 18, 107, 92, 101, 78, 23, 32, 89, 18, 107, 92, 101, 46, 23,
        32, 89, 18, 107, 92, 101, 46, 23, 32, 25, 18, 107, 92, 37, 46, 23, 32, 25, 18, 107, 92, 37, 46, 23,
        32, 25, 50, 107, 28, 37, 46, 23, 32, 25, 50, 107, 28, 37, 46, 23, 32, 25, 50, 123, 28, 37, 46, 55,
        32, 25, 50, 123, 28, 37, 46, 55, 40, 25, 50, 123, 28, 37, 62, 55, 40, 25, 50, 123, 28, 37, 62, 55,
        40, 17, 50, 123, 28, 45, 62, 55, 40, 17, 50, 123, 28, 45, 62, 55, 40, 17, 34, 123, 20, 45, 62, 55,
        40, 17, 34, 123, 20, 45, 62, 55, 40, 17, 34, 91, 20, 45, 62, 39, 40, 17, 34, 91, 20, 45, 62, 39,
        104, 17, 34, 91, 20, 45, 30, 39, 104, 17, 34, 91, 20, 45, 30, 39, 104, 81, 34, 91, 20, 109, 30, 39,
        104, 81, 34, 91, 20, 109, 30, 39, 104, 81, 66, 91, 84, 109, 30, 39, 104, 81, 66, 91, 84, 109, 30, 39,
        104, 81, 66, 91, 84, 109, 30, 71, 104, 81, 66, 11, 84, 109, 30, 71, 104, 81, 66, 11, 84, 109, 14, 71,
        104, 81, 66, 11, 84, 109, 14, 71, 104, 81, 66, 11, 84, 109, 14, 71, 104, 81, 66, 11, 84, 109, 14, 71,
        96, 89, 2, 75, 92, 101, 78, 7, 96, 89, 2, 75, 92, 101, 78, 7, 96, 89, 2, 75, 92, 101, 78, 7,
        96, 89, 2, 75, 92, 101, 78, 7, 96, 89, 18, 75, 92, 101, 78, 7, 96, 89, 18, 107, 92, 101, 78, 7,
        96, 89, 18, 107, 92, 101, 78, 23, 96, 89, 18, 107, 92, 101, 78, 23, 32, 89, 18, 107, 92, 101, 46, 23,
        32, 89, 18, 107, 92, 101, 46, 23, 32, 25, 18, 107, 92, 37, 46, 23, 32, 25, 18, 107, 92, 37, 46, 23,
        32, 25, 50, 107, 28, 37, 46, 23, 32, 25, 50, 107, 28, 37, 46, 23, 32, 25, 50, 123, 28, 37, 46, 55,
        32, 25, 50, 123, 28, 37, 46, 55, 40, 25, 50, 123, 28, 37, 62, 55, 40, 25, 50, 123, 28, 37, 62, 55,
        40, 17, 50, 123, 28, 45, 62, 55, 40, 17, 50, 123, 28, 45, 62, 55, 40, 17, 34, 123, 20, 45, 62, 55,
        40, 17, 34, 123, 20, 45, 62, 55, 40, 17, 34, 91, 20, 45, 62, 39, 40, 17, 34, 91, 20, 45, 62, 39,
        104, 17, 34, 91, 20, 45, 30, 39, 104, 17, 34, 91, 20, 45, 30, 39, 104, 81, 34, 91, 20, 109, 30, 39,
        104, 81, 34, 91, 20, 109, 30, 39, 104, 81, 66, 91, 84, 109, 30, 39, 104, 81, 66, 91, 84, 109, 30, 39,
        104, 81, 66, 91, 84, 109, 30, 71, 104, 81, 66, 91, 84, 109, 30, 71, 104, 81, 66, 11, 84, 109, 14, 71,
        104, 81, 66, 11, 84, 109, 14, 71, 104, 81, 66, 11, 84, 109, 14, 71, 104, 81, 66, 11, 84, 109, 14, 71,
        96, 89, 2, 75, 92, 101, 78, 7, 96, 89, 2, 75, 92, 101, 78, 7, 96, 89, 2, 75, 92, 101, 78, 7,
        96, 89, 2, 75, 92, 101, 78, 7, 96, 89, 82, 107, 92, 101, 78, 7, 96, 89, 82, 107, 92, 101, 78, 7,
        96, 89, 82, 107, 92, 101, 78, 23, 96, 89, 82, 107, 92, 101, 78, 23, 32, 89, 82, 107, 92, 101, 110, 23,
        32, 89, 82, 107, 92, 101, 110, 23, 32, 25, 82, 107, 92, 37, 110, 23, 32, 25, 82, 107, 92, 37, 110, 23,
        32, 25, 114, 107, 28, 37, 110, 23, 32, 25, 114, 107, 28, 37, 110, 23, 32, 25, 114, 123, 28, 37, 110, 55,
        32, 25, 114, 123, 28, 37, 110, 55, 40, 25, 114, 123, 28, 37, 126, 55, 40, 25, 114, 123, 28, 37, 126, 55,
        40, 17, 114, 123, 28, 45, 126, 55, 40, 17, 114, 123, 28, 45, 126, 55, 40, 17, 98, 123, 20, 45, 126, 55,
        40, 17, 98, 123, 20, 45, 126, 55, 40, 17, 98, 91, 20, 45, 126, 39, 40, 17, 98, 91, 20, 45, 126, 39,
        104, 17, 98, 91, 20, 45, 94, 39, 104, 17, 98, 91, 20, 45, 94, 39, 104, 81, 98, 91, 20, 109, 94, 39,
        104, 81, 98, 91, 20, 109, 94, 39, 104, 81, 98, 91, 84, 109, 94, 39, 104, 81, 66, 91, 84, 109, 94, 39,
        104, 81, 66, 91, 84, 109, 94, 71, 104, 81, 66, 91, 84, 109, 94, 71, 104, 81, 66, 91, 84, 109, 94, 71,
        104, 81, 66, 11, 84, 109, 14, 71, 104, 81, 66, 11, 84, 109, 14, 71, 104, 81, 66, 11, 84, 109, 14, 71,
        96, 89, 2, 75, 92, 101, 78, 7, 96, 89, 2, 75, 92, 101, 78, 7, 96, 89, 2, 75, 92, 101, 78, 7,
        96, 89, 82, 107, 92, 101, 78, 7, 96, 89, 82, 107, 92, 101, 78, 7, 96, 89, 82, 107, 92, 101, 78, 7,
        96, 89, 82, 107, 92, 101, 78, 23, 96, 89, 82, 107, 92, 101, 110, 23, 32, 89, 82, 107, 92, 101, 110, 23,
        32, 89, 82, 107, 92, 101, 110, 23, 32, 25, 82, 107, 92, 37, 110, 23, 32, 25, 82, 107, 92, 37, 110, 23,
        32, 25, 114, 107, 28, 37, 110, 23, 32, 25, 114, 107, 28, 37, 110, 23, 32, 25, 114, 123, 28, 37, 110, 55,
        32, 25, 114, 123, 28, 37, 110, 55, 40, 25, 114, 123, 28, 37, 126, 55, 40, 25, 114, 123, 28, 37, 126, 55,
        40, 17, 114, 123, 28, 45, 126, 55, 40, 17, 114, 123, 28, 45, 126, 55, 40, 17, 98, 123, 20, 45, 126, 55,
        40, 17, 98, 123, 20, 45, 126, 55, 40, 17, 98, 91, 20, 45, 126, 39, 40, 17, 98, 91, 20, 45, 126, 39,
        104, 17, 98, 91, 20, 45, 94, 39, 104, 17, 98, 91, 20, 45, 94, 39, 104, 81, 98, 91, 20, 109, 94, 39,
        104, 81, 98, 91, 20, 109, 94, 39, 104, 81, 98, 91, 84, 109, 94, 39, 104, 81, 98, 91, 84, 109, 94, 39,
        104, 81, 66, 91, 84, 109, 94, 71, 104, 81, 66, 91, 84, 109, 94, 71, 104, 81, 66, 91, 84, 109, 94, 71,
        104, 81, 66, 91, 84, 109, 94, 71, 104, 81, 66, 11, 84, 109, 14, 71, 104, 81, 66, 11, 84, 109, 14, 71,
        96, 89, 2, 75, 92, 101, 78, 7, 96, 89, 2, 75, 92, 101, 78, 7, 96, 89, 82, 107, 92, 101, 78, 7,
        96, 89, 82, 107, 92, 101, 78, 7, 96, 89, 82, 107, 92, 101, 78, 7, 96, 89, 82, 107, 92, 101, 78, 7,
        96, 89, 82, 107, 92, 101, 110, 87, 96, 89, 82, 107, 92, 101, 110, 87, 32, 89, 82, 107, 92, 101, 110, 87,
        32, 89, 82, 107, 92, 101, 110, 87, 32, 25, 82, 107, 92, 69, 110, 87, 32, 25, 82, 107, 92, 69, 110, 87,
        32, 25, 114, 107, 28, 69, 110, 87, 32, 25, 114, 107, 28, 69, 110, 87, 32, 25, 114, 123, 28, 69, 110, 119,
        32, 25, 114, 123, 28, 69, 110, 119, 40, 25, 114, 123, 28, 69, 126, 119, 40, 25, 114, 123, 28, 69, 126, 119,
        40, 17, 114, 123, 28, 77, 126, 119, 40, 17, 114, 123, 28, 77, 126, 119, 40, 17, 98, 123, 20, 77, 126, 119,
        40, 17, 98, 123, 20, 77, 126, 119, 40, 17, 98, 91, 20, 77, 126, 103, 40, 17, 98, 91, 20, 77, 126, 103,
        104, 17, 98, 91, 20, 77, 94, 103, 104, 17, 98, 91, 20, 77, 94, 103, 104, 81, 98, 91, 20, 109, 94, 103,
        104, 81, 98, 91, 20, 109, 94, 103, 104, 81, 98, 91, 84, 109, 94, 103, 104, 81, 98, 91, 84, 109, 94, 103,
        104, 81, 98, 91, 84, 109, 94, 103, 104, 81, 66, 91, 84, 109, 94, 71, 104, 81, 66, 91, 84, 109, 94, 71,
        104, 81, 66, 91, 84, 109, 94, 71, 104, 81, 66, 11, 84, 109, 14, 71, 104, 81, 66, 11, 84, 109, 14, 71,
        96, 89, 2, 75, 92, 101, 78, 7, 96, 89, 82, 107, 92, 101, 78, 7, 96, 89, 82, 107, 92, 101, 78, 7,
        96, 89, 82, 107, 92, 101, 78, 7, 96, 89, 82, 107, 92, 101, 78, 7, 96, 89, 82, 107, 92, 101, 110, 87,
        96, 89, 82, 107, 92, 101, 110, 87, 96, 89, 82, 107, 92, 101, 110, 87, 32, 89, 82, 107, 92, 101, 110, 87,
        32, 89, 82, 107, 92, 69, 110, 87, 32, 25, 82, 107, 92, 69, 110, 87, 32, 25, 82, 107, 92, 69, 110, 87,
        32, 25, 114, 107, 28, 69, 110, 87, 32, 25, 114, 107, 28, 69, 110, 87, 32, 25, 114, 123, 28, 69, 110, 119,
        32, 25, 114, 123, 28, 69, 110, 119, 40, 25, 114, 123, 28, 69, 126, 119, 40, 25, 114, 123, 28, 69, 126, 119,
        40, 17, 114, 123, 28, 77, 126, 119, 40, 17, 114, 123, 28, 77, 126, 119, 40, 17, 98, 123, 20, 77, 126, 119,
        40, 17, 98, 123, 20, 77, 126, 119, 40, 17, 98, 91, 20, 77, 126, 103, 40, 17, 98, 91, 20, 77, 126, 103,
        104, 17, 98, 91, 20, 77, 94, 103, 104, 17, 98, 91, 20, 77, 94, 103, 104, 81, 98, 91, 20, 77, 94, 103,
        104, 81, 98, 91, 20, 109, 94, 103, 104, 81, 98, 91, 84, 109, 94, 103, 104, 81, 98, 91, 84, 109, 94, 103,
        104, 81, 98, 91, 84, 109, 94, 103, 104, 81, 98, 91, 84, 109, 94, 103, 104, 81, 66, 91, 84, 109, 94, 71,
        104, 81, 66, 91, 84, 109, 94, 71, 104, 81, 66, 91, 84, 109, 94, 71, 104, 81, 66, 11, 84, 109, 14, 71,
        96, 89, 82, 107, 92, 101, 78, 7, 96, 89, 82, 107, 92, 101, 78, 7, 96, 89, 82, 107, 92, 101, 78, 7,
        96, 89, 82, 107, 92, 101, 78, 7, 96, 89, 82, 107, 92, 101, 110, 87, 96, 89, 82, 107, 92, 101, 110, 87,
        96, 89, 82, 107, 92, 101, 110, 87, 96, 89, 82, 107, 92, 101, 110, 87, 64, 89, 82, 107, 92, 69, 110, 87,
        64, 89, 82, 107, 92, 69, 110, 87, 64, 25, 82, 107, 92, 69, 110, 87, 64, 25, 82, 107, 92, 69, 110, 87,
        64, 25, 114, 107, 12, 69, 110, 87, 64, 25, 114, 107, 12, 69, 110, 87, 64, 25, 114, 123, 12, 69, 110, 119,
        64, 25, 114, 123, 12, 69, 110, 119, 72, 25, 114, 123, 12, 69, 126, 119, 72, 25, 114, 123, 12, 69, 126, 119,
        72, 17, 114, 123, 12, 77, 126, 119, 72, 17, 114, 123, 12, 77, 126, 119, 72, 17, 98, 123, 4, 77, 126, 119,
        72, 17, 98, 123, 4, 77, 126, 119, 72, 17, 98, 91, 4, 77, 126, 103, 72, 17, 98, 91, 4, 77, 126, 103,
        104, 17, 98, 91, 4, 77, 94, 103, 104, 17, 98, 91, 4, 77, 94, 103, 104, 81, 98, 91, 4, 77, 94, 103,
        104, 81, 98, 91, 4, 77, 94, 103, 104, 81, 98, 91, 84, 109, 94, 103, 104, 81, 98, 91, 84, 109, 94, 103,
        104, 81, 98, 91, 84, 109, 94, 103, 104, 81, 98, 91, 84, 109, 94, 103, 104, 81, 98, 91, 84, 109, 94, 103,
        104, 81, 66, 91, 84, 109, 94, 71, 104, 81, 66, 91, 84, 109, 94, 71, 104, 81, 66, 91, 84, 109, 94, 71,
        96, 89, 82, 107, 92, 101, 78, 7, 96, 89, 82, 107, 92, 101, 78, 7, 96, 89, 82, 107, 92, 101, 78, 7,
        96, 89, 82, 107, 92, 101, 110, 87, 96, 89, 82, 107, 92, 101, 110, 87, 96, 89, 82, 107, 92, 101, 110, 87,
        96, 89, 82, 107, 92, 101, 110, 87, 64, 89, 82, 107, 92, 69, 110, 87, 64, 89, 82, 107, 92, 69, 110, 87,
        64, 89, 82, 107, 92, 69, 110, 87, 64, 25, 82, 107, 92, 69, 110, 87, 64, 25, 82, 107, 12, 69, 110, 87,
        64, 25, 114, 107, 12, 69, 110, 87, 64, 25, 114, 107, 12, 69, 110, 87, 64, 25, 114, 123, 12, 69, 110, 119,
        64, 25, 114, 123, 12, 69, 110, 119, 72, 25, 114, 123, 12, 69, 126, 119, 72, 25, 114, 123, 12, 69, 126, 119,
        72, 17, 114, 123, 12, 77, 126, 119, 72, 17, 114, 123, 12, 77, 126, 119, 72, 17, 98, 123, 4, 77, 126, 119,
        72, 17, 98, 123, 4, 77, 126, 119, 72, 17, 98, 91, 4, 77, 126, 103, 72, 17, 98, 91, 4, 77, 126, 103,
        72, 17, 98, 91, 4, 77, 94, 103, 104, 17, 98, 91, 4, 77, 94, 103, 104, 81, 98, 91, 4, 77, 94, 103,
        104, 81, 98, 91, 4, 77, 94, 103, 104, 81, 98, 91, 4, 77, 94, 103, 104, 81, 98, 91, 84, 109, 94, 103,
        104, 81, 98, 91, 84, 109, 94, 103, 104, 81, 98, 91, 84, 109, 94, 103, 104, 81, 98, 91, 84, 109, 94, 103,
        104, 81, 98, 91, 84, 109, 94, 103, 104, 81, 66, 91, 84, 109, 94, 71, 104, 81, 66, 91, 84, 109, 94, 71,
        96, 89, 82, 107, 92, 101, 78, 7, 96, 89, 82, 107, 92, 101, 78, 7, 96, 89, 82, 107, 92, 101, 110, 87,
        96, 89, 82, 107, 92, 101, 110, 87, 96, 89, 82, 107, 92, 101, 110, 87, 96, 89, 82, 107, 92, 101, 110, 87,
        64, 89, 82, 107, 92, 69, 110, 87, 64, 89, 82, 107, 92, 69, 110, 87, 64, 89, 82, 107, 92, 69, 110, 87,
        64, 89, 82, 107, 92, 69, 110, 87, 64, 9, 82, 107, 12, 69, 110, 87, 64, 9, 82, 107, 12, 69, 110, 87,
        64, 9, 114, 107, 12, 69, 110, 87, 64, 9, 114, 107, 12, 69, 110, 87, 64, 9, 114, 123, 12, 69, 110, 119,
        64, 9, 114, 123, 12, 69, 110, 119, 72, 9, 114, 123, 12, 69, 126, 119, 72, 9, 114, 123, 12, 69, 126, 119,
        72, 1, 114, 123, 12, 77, 126, 119, 72, 1, 114, 123, 12, 77, 126, 119, 72, 1, 98, 123, 4, 77, 126, 119,
        72, 1, 98, 123, 4, 77, 126, 119, 72, 1, 98, 91, 4, 77, 126, 103, 72, 1, 98, 91, 4, 77, 126, 103,
        72, 1, 98, 91, 4, 77, 94, 103, 72, 1, 98, 91, 4, 77, 94, 103, 104, 81, 98, 91, 4, 77, 94, 103,
        104, 81, 98, 91, 4, 77, 94, 103, 104, 81, 98, 91, 4, 77, 94, 103, 104, 81, 98, 91, 4, 77, 94, 103,
        104, 81, 98, 91, 84, 109, 94, 103, 104, 81, 98, 91, 84, 109, 94, 103, 104, 81, 98, 91, 84, 109, 94, 103,
        104, 81, 98, 91, 84, 109, 94, 103, 104, 81, 66, 91, 84, 109, 94, 71, 104, 81, 66, 91, 84, 109, 94, 71,
        96, 89, 82, 107, 92, 101, 78, 7, 96, 89, 82, 107, 92, 101, 110, 87, 96, 89, 82, 107, 92, 101, 110, 87,
        96, 89, 82, 107, 92, 101, 110, 87, 96, 89, 82, 107, 92, 101, 110, 87, 64, 89, 82, 107, 92, 69, 110, 87,
        64, 89, 82, 107, 92, 69, 110, 87, 64, 89, 82, 107, 92, 69, 110, 87, 64, 89, 82, 107, 92, 69, 110, 87,
        64, 9, 82, 107, 12, 69, 110, 87, 64, 9, 82, 107, 12, 69, 110, 87, 64, 9, 82, 107, 12, 69, 110, 87,
        64, 9, 114, 107, 12, 69, 110, 87, 64, 9, 114, 107, 12, 69, 110, 87, 64, 9, 114, 123, 12, 69, 110, 119,
        64, 9, 114, 123, 12, 69, 110, 119, 72, 9, 114, 123, 12, 69, 126, 119, 72, 9, 114, 123, 12, 69, 126, 119,
        72, 1, 114, 123, 12, 77, 126, 119, 72, 1, 114, 123, 12, 77, 126, 119, 72, 1, 98, 123, 4, 77, 126, 119,
        72, 1, 98, 123, 4, 77, 126, 119, 72, 1, 98, 91, 4, 77, 126, 103, 72, 1, 98, 91, 4, 77, 126, 103,
        72, 1, 98, 91, 4, 77, 94, 103, 72, 1, 98, 91, 4, 77, 94, 103, 72, 1, 98, 91, 4, 77, 94, 103,
        104, 81, 98, 91, 4, 77, 94, 103, 104, 81, 98, 91, 4, 77, 94, 103, 104, 81, 98, 91, 4, 77, 94, 103,
        104, 81, 98, 91, 4, 77, 94, 103, 104, 81, 98, 91, 84, 109, 94, 103, 104, 81, 98, 91, 84, 109, 94, 103,
        104, 81, 98, 91, 84, 109, 94, 103, 104, 81, 98, 91, 84, 109, 94, 103, 104, 81, 66, 91, 84, 109, 94, 71,
        96, 89, 82, 107, 92, 101, 110, 87, 96, 89, 82, 107, 92, 101, 110, 87, 96, 89, 82, 107, 92, 101, 110, 87,
        96, 89, 82, 107, 92, 101, 110, 87, 64, 89, 82, 107, 92, 69, 110, 87, 64, 89, 82, 107, 92, 69, 110, 87,
        64, 89, 82, 107, 92, 69, 110, 87, 64, 89, 82, 107, 92, 69, 110, 87, 64, 9, 82, 107, 12, 69, 110, 87,
        64, 9, 82, 107, 12, 69, 110, 87, 64, 9, 82, 107, 12, 69, 110, 87, 64, 9, 82, 107, 12, 69, 110, 87,
        64, 9, 114, 107, 12, 69, 110, 87, 64, 9, 114, 107, 12, 69, 110, 87, 64, 9, 114, 123, 12, 69, 110, 119,
        64, 9, 114, 123, 12, 69, 110, 119, 72, 9, 114, 123, 12, 69, 126, 119, 72, 9, 114, 123, 12, 69, 126, 119,
        72, 1, 114, 123, 12, 77, 126, 119, 72, 1, 114, 123, 12, 77, 126, 119, 72, 1, 98, 123, 4, 77, 126, 119,
        72, 1, 98, 123, 4, 77, 126, 119, 72, 1, 98, 91, 4, 77, 126, 103, 72, 1, 98, 91, 4, 77, 126, 103,
        72, 1, 98, 91, 4, 77, 94, 103, 72, 1, 98, 91, 4, 77, 94, 103, 72, 1, 98, 91, 4, 77, 94, 103,
        72, 1, 98, 91, 4, 77, 94, 103, 72, 1, 98, 91, 4, 77, 94, 103, 104, 81, 98, 91, 4, 77, 94, 103,
        104, 81, 98, 91, 4, 77, 94, 103, 104, 81, 98, 91, 4, 77, 94, 103, 104, 81, 98, 91, 4, 77, 94, 103,
        104, 81, 98, 91, 84, 109, 94, 103, 104, 81, 98, 91, 84, 109, 94, 103, 104, 81, 98, 91, 84, 109, 94, 103,
        96, 89, 82, 107, 92, 101, 110, 87, 96, 89, 82, 107, 92, 101, 110, 87, 96, 89, 82, 107, 92, 101, 110, 87,
        64, 89, 82, 107, 92, 69, 110, 87, 64, 89, 82, 107, 92, 69, 110, 87, 64, 89, 82, 107, 92, 69, 110, 87,
        64, 89, 82, 107, 92, 69, 110, 87, 64, 9, 82, 107, 12, 69, 110, 87, 64, 9, 82, 107, 12, 69, 110, 87,
        64, 9, 82, 107, 12, 69, 110, 87, 64, 9, 82, 107, 12, 69, 110, 87, 64, 9, 82, 107, 12, 69, 110, 87,
        64, 9, 114, 107, 12, 69, 110, 87, 64, 9, 114, 107, 12, 69, 110, 87, 64, 9, 114, 123, 12, 69, 110, 119,
        64, 9, 114, 123, 12, 69, 110, 119, 72, 9, 114, 123, 12, 69, 126, 119, 72, 9, 114, 123, 12, 69, 126, 119,
        72, 1, 114, 123, 12, 77, 126, 119, 72, 1, 114, 123, 12, 77, 126, 119, 72, 1, 98, 123, 4, 77, 126, 119,
        72, 1, 98, 123, 4, 77, 126, 119, 72, 1, 98, 91, 4, 77, 126, 103, 72, 1, 98, 91, 4, 77, 126, 103,
        72, 1, 98, 91, 4, 77, 94, 103, 72, 1, 98, 91, 4, 77, 94, 103, 72, 1, 98, 91, 4, 77, 94, 103,
        72, 1, 98, 91, 4, 77, 94, 103, 72, 1, 98, 91, 4, 77, 94, 103, 72, 1, 98, 91, 4, 77, 94, 103,
        104, 81, 98, 91, 4, 77, 94, 103, 104, 81, 98, 91, 4, 77, 94, 103, 104, 81, 98, 91, 4, 77, 94, 103,
        104, 81, 98, 91, 4, 77, 94, 103, 104, 81, 98, 91, 84, 109, 94, 103, 104, 81, 98, 91, 84, 109, 94, 103,
        56, 33, 50, 19, 36, 61, 22, 55, 56, 1, 50, 19, 36, 61, 22, 55, 56, 1, 50, 19, 36, 61, 22, 55,
        56, 1, 50, 19, 36, 61, 22, 55, 56, 1, 50, 19, 36, 61, 22, 55, 56, 1, 50, 19, 4, 61, 22, 55,
        56, 1, 50, 19, 4, 61, 22, 55, 56, 1, 50, 19, 4, 61, 22, 55, 56, 1, 50, 19, 4, 61, 22, 55,
        56, 1, 50, 19, 4, 61, 22, 55, 56, 1, 50, 19, 4, 61, 22, 55, 56, 1, 50, 19, 4, 61, 22, 55,
        56, 1, 34, 27, 4, 61, 22, 55, 56, 1, 34, 27, 4, 61, 22, 55, 56, 1, 34, 27, 4, 61, 22, 55,
        56, 1, 34, 27, 4, 61, 22, 55, 56, 1, 34, 27, 4, 61, 30, 39, 56, 1, 34, 27, 4, 61, 30, 39,
        56, 1, 34, 27, 4, 61, 30, 39, 56, 1, 34, 27, 4, 61, 30, 39, 48, 1, 34, 27, 4, 53, 30, 39,
        48, 1, 34, 27, 4, 53, 30, 39, 48, 1, 34, 27, 4, 53, 30, 39, 48, 1, 34, 27, 4, 53, 30, 39,
        48, 17, 34, 27, 20, 53, 30, 39, 48, 17, 34, 27, 20, 53, 30, 39, 48, 17, 34, 27, 20, 53, 30, 39,
        48, 17, 34, 27, 20, 53, 30, 39, 48, 17, 34, 27, 20, 53, 30, 39, 48, 17, 34, 27, 20, 53, 30, 39,
        48, 17, 34, 27, 20, 53, 30, 39, 48, 17, 34, 27, 20, 53, 30, 39, 48, 17, 34, 59, 20, 53, 30, 39,
        48, 17, 34, 59, 20, 53, 30, 39, 48, 17, 34, 59, 20, 53, 30, 39, 48, 17, 34, 59, 20, 53, 30, 39,
        56, 33, 50, 19, 36, 29, 22, 55, 56, 33, 50, 19, 36, 61, 22, 55, 56, 1, 50, 19, 36, 61, 22, 55,
        56, 1, 50, 19, 36, 61, 22, 55, 56, 1, 50, 19, 36, 61, 22, 55, 56, 1, 50, 19, 36, 61, 22, 55,
        56, 1, 50, 19, 4, 61, 22, 55, 56, 1, 50, 19, 4, 61, 22, 55, 56, 1, 50, 19, 4, 61, 22, 55,
        56, 1, 50, 19, 4, 61, 22, 55, 56, 1, 50, 19, 4, 61, 22, 55, 56, 1, 50, 19, 4, 61, 22, 55,
        56, 1, 34, 27, 4, 61, 22, 55, 56, 1, 34, 27, 4, 61, 22, 55, 56, 1, 34, 27, 4, 61, 22, 55,
        56, 1, 34, 27, 4, 61, 22, 55, 56, 1, 34, 27, 4, 61, 30, 39, 56, 1, 34, 27, 4, 61, 30, 39,
        56, 1, 34, 27, 4, 61, 30, 39, 56, 1, 34, 27, 4, 61, 30, 39, 48, 1, 34, 27, 4, 53, 30, 39,
        48, 1, 34, 27, 4, 53, 30, 39, 48, 1, 34, 27, 4, 53, 30, 39, 48, 1, 34, 27, 4, 53, 30, 39,
        48, 17, 34, 27, 20, 53, 30, 39, 48, 17, 34, 27, 20, 53, 30, 39, 48, 17, 34, 27, 20, 53, 30, 39,
        48, 17, 34, 27, 20, 53, 30, 39, 48, 17, 34, 27, 20, 53, 30, 39, 48, 17, 34, 27, 20, 53, 30, 39,
        48, 17, 34, 27, 20, 53, 30, 39, 48, 17, 34, 59, 20, 53, 30, 39, 48, 17, 34, 59, 20, 53, 30, 39,
        48, 17, 34, 59, 20, 53, 30, 39, 48, 17, 34, 59, 20, 53, 30, 39, 48, 17, 2, 59, 20, 53, 62, 39,
        56, 33, 50, 19, 36, 29, 22, 55, 56, 33, 50, 19, 36, 29, 22, 55, 56, 33, 50, 19, 36, 61, 22, 55,
        56, 1, 50, 19, 36, 61, 22, 55, 56, 1, 50, 19, 36, 61, 22, 55, 56, 1, 50, 19, 36, 61, 22, 55,
        56, 1, 50, 19, 36, 61, 22, 55, 56, 1, 50, 19, 4, 61, 22, 55, 56, 1, 50, 19, 4, 61, 22, 55,
        56, 1, 50, 19, 4, 61, 22, 55, 56, 1, 50, 19, 4, 61, 22, 55, 56, 1, 50, 19, 4, 61, 22, 55,
        56, 1, 34, 27, 4, 61, 22, 55, 56, 1, 34, 27, 4, 61, 22, 55, 56, 1, 34, 27, 4, 61, 22, 55,
        56, 1, 34, 27, 4, 61, 22, 55, 56, 1, 34, 27, 4, 61, 30, 39, 56, 1, 34, 27, 4, 61, 30, 39,
        56, 1, 34, 27, 4, 61, 30, 39, 56, 1, 34, 27, 4, 61, 30, 39, 48, 1, 34, 27, 4, 53, 30, 39,
        48, 1, 34, 27, 4, 53, 30, 39, 48, 1, 34, 27, 4, 53, 30, 39, 48, 1, 34, 27, 4, 53, 30, 39,
        48, 17, 34, 27, 20, 53, 30, 39, 48, 17, 34, 27, 20, 53, 30, 39, 48, 17, 34, 27, 20, 53, 30, 39,
        48, 17, 34, 27, 20, 53, 30, 39, 48, 17, 34, 27, 20, 53, 30, 39, 48, 17, 34, 27, 20, 53, 30, 39,
        48, 17, 34, 59, 20, 53, 30, 39, 48, 17, 34, 59, 20, 53, 30, 39, 48, 17, 34, 59, 20, 53, 30, 39,
        48, 17, 34, 59, 20, 53, 30, 39, 48, 17, 2, 59, 20, 53, 62, 39, 48, 17, 2, 59, 20, 53, 62, 39,
        56, 33, 50, 19, 36, 29, 22, 55, 56, 33, 50, 19, 36, 29, 22, 55, 56, 33, 50, 19, 36, 29, 22, 55,
        56, 33, 50, 19, 36, 61, 22, 55, 56, 1, 50, 19, 36, 61, 22, 55, 56, 1, 50, 19, 36, 61, 22, 55,
        56, 1, 50, 19, 36, 61, 22, 55, 56, 1, 50, 19, 36, 61, 22, 55, 56, 1, 50, 19, 4, 61, 22, 55,
        56, 1, 50, 19, 4, 61, 22, 55, 56, 1, 50, 19, 4, 61, 22, 55, 56, 1, 50, 19, 4, 61, 22, 55,
        56, 1, 34, 27, 4, 61, 22, 55, 56, 1, 34, 27, 4, 61, 22, 55, 56, 1, 34, 27, 4, 61, 22, 55,
        56, 1, 34, 27, 4, 61, 22, 55, 56, 1, 34, 27, 4, 61, 30, 39, 56, 1, 34, 27, 4, 61, 30, 39,
        56, 1, 34, 27, 4, 61, 30, 39, 56, 1, 34, 27, 4, 61, 30, 39, 48, 1, 34, 27, 4, 53, 30, 39,
        48, 1, 34, 27, 4, 53, 30, 39, 48, 1, 34, 27, 4, 53, 30, 39, 48, 1, 34, 27, 4, 53, 30, 39,
        48, 17, 34, 27, 20, 53, 30, 39, 48, 17, 34, 27, 20, 53, 30, 39, 48, 17, 34, 27, 20, 53, 30, 39,
        48, 17, 34, 27, 20, 53, 30, 39, 48, 17, 34, 27, 20, 53, 30, 39, 48, 17, 34, 59, 20, 53, 30, 39,
        48, 17, 34, 59, 20, 53, 30, 39, 48, 17, 34, 59, 20, 53, 30, 39, 48, 17, 34, 59, 20, 53, 30, 39,
        48, 17, 2, 59, 20, 53, 62, 39, 48, 17, 2, 59, 20, 53, 62, 39, 48, 17, 2, 59, 20, 53, 62, 39,
        56, 33, 50, 19, 36, 29, 22, 55, 56, 33, 50, 19, 36, 29, 22, 55, 56, 33, 50, 19, 36, 29, 22, 55,
        56, 33, 50, 19, 36, 29, 22, 55, 56, 33, 50, 19, 36, 61, 22, 55, 56, 1, 50, 19, 36, 61, 22, 55,
        56, 1, 50, 19, 36, 61, 22, 55, 56, 1, 50, 19, 36, 61, 22, 55, 56, 1, 50, 19, 36, 61, 22, 55,
        56, 1, 50, 19, 4, 61, 22, 55, 56, 1, 50, 19, 4, 61, 22, 55, 56, 1, 50, 19, 4, 61, 22, 55,
        56, 1, 34, 27, 4, 61, 22, 55, 56, 1, 34, 27, 4, 61, 22, 55, 56, 1, 34, 27, 4, 61, 22, 55,
        56, 1, 34, 27, 4, 61, 22, 55, 56, 1, 34, 27, 4, 61, 30, 39, 56, 1, 34, 27, 4, 61, 30, 39,
        56, 1, 34, 27, 4, 61, 30, 39, 56, 1, 34, 27, 4, 61, 30, 39, 48, 1, 34, 27, 4, 53, 30, 39,
        48, 1, 34, 27, 4, 53, 30, 39, 48, 1, 34, 27, 4, 53, 30, 39, 48, 1, 34, 27, 4, 53, 30, 39,
        48, 17, 34, 27, 20, 53, 30, 39, 48, 17, 34, 27, 20, 53, 30, 39, 48, 17, 34, 27, 20, 53, 30, 39,
        48, 17, 34, 27, 20, 53, 30, 39, 48, 17, 34, 59, 20, 53, 30, 39, 48, 17, 34, 59, 20, 53, 30, 39,
        48, 17, 34, 59, 20, 53, 30, 39, 48, 17, 34, 59, 20, 53, 30, 39, 48, 17, 2, 59, 20, 53, 62, 39,
        48, 17, 2, 59, 20, 53, 62, 39, 48, 17, 2, 59, 20, 53, 62, 39, 48, 17, 2, 59, 20, 53, 62, 39,
        24, 33, 50, 19, 36, 29, 22, 55, 56, 33, 50, 19, 36, 29, 22, 55, 56, 33, 50, 19, 36, 29, 22, 55,
        56, 33, 50, 19, 36, 29, 22, 55, 56, 33, 50, 19, 36, 29, 22, 55, 56, 33, 50, 19, 36, 61, 22, 55,
        56, 1, 50, 19, 36, 61, 22, 55, 56, 1, 50, 19, 36, 61, 22, 55, 56, 1, 50, 19, 36, 61, 22, 55,
        56, 1, 50, 19, 36, 61, 22, 55, 56, 1, 50, 19, 4, 61, 22, 55, 56, 1, 50, 11, 4, 61, 22, 55,
        56, 1, 34, 11, 4, 61, 22, 55, 56, 1, 34, 27, 4, 61, 22, 55, 56, 1, 34, 27, 4, 61, 22, 55,
        56, 1, 34, 27, 4, 61, 14, 55, 56, 1, 34, 27, 4, 61, 14, 39, 56, 1, 34, 27, 4, 61, 30, 39,
        56, 1, 34, 27, 4, 61, 30, 39, 56, 1, 34, 27, 4, 45, 30, 39, 48, 1, 34, 27, 4, 45, 30, 39,
        48, 1, 34, 27, 4, 53, 30, 39, 48, 1, 34, 27, 4, 53, 30, 39, 48, 1, 34, 27, 12, 53, 30, 39,
        48, 17, 34, 27, 12, 53, 30, 39, 48, 17, 34, 27, 20, 53, 30, 39, 48, 17, 34, 27, 20, 53, 30, 39,
        48, 17, 34, 59, 20, 53, 30, 39, 48, 17, 34, 59, 20, 53, 30, 39, 48, 17, 34, 59, 20, 53, 30, 39,
        48, 17, 34, 59, 20, 53, 30, 39, 48, 17, 2, 59, 20, 53, 62, 39, 48, 17, 2, 59, 20, 53, 62, 39,
        48, 17, 2, 59, 20, 53, 62, 39, 48, 17, 2, 59, 20, 53, 62, 39, 48, 17, 2, 59, 20, 53, 62, 7,
        24, 33, 50, 19, 36, 29, 22, 55, 24, 33, 50, 19, 36, 29, 22, 55, 56, 33, 50, 19, 36, 29, 22, 55,
        56, 33, 50, 19, 36, 29, 22, 55, 56, 33, 50, 19, 36, 29, 22, 55, 56, 33, 50, 19, 36, 29, 22, 55,
        56, 33, 50, 19, 36, 61, 22, 55, 56, 1, 50, 19, 36, 61, 22, 55, 56, 1, 50, 19, 36, 61, 22, 55,
        56, 1, 50, 19, 36, 61, 22, 55, 56, 1, 50, 11, 36, 61, 22, 55, 56, 1, 50, 11, 4, 61, 22, 55,
        56, 1, 34, 11, 4, 61, 22, 55, 56, 1, 34, 11, 4, 61, 22, 55, 56, 1, 34, 27, 4, 61, 14, 55,
        56, 1, 34, 27, 4, 61, 14, 55, 56, 1, 34, 27, 4, 61, 14, 39, 56, 1, 34, 27, 4, 61, 14, 39,
        56, 1, 34, 27, 4, 45, 30, 39, 56, 1, 34, 27, 4, 45, 30, 39, 48, 1, 34, 27, 4, 45, 30, 39,
        48, 1, 34, 27, 4, 45, 30, 39, 48, 1, 34, 27, 12, 53, 30, 39, 48, 1, 34, 27, 12, 53, 30, 39,
        48, 17, 34, 27, 12, 53, 30, 39, 48, 17, 34, 27, 12, 53, 30, 39, 48, 17, 34, 59, 20, 53, 30, 39,
        48, 17, 34, 59, 20, 53, 30, 39, 48, 17, 34, 59, 20, 53, 30, 39, 48, 17, 34, 59, 20, 53, 30, 39,
        48, 17, 2, 59, 20, 53, 62, 39, 48, 17, 2, 59, 20, 53, 62, 39, 48, 17, 2, 59, 20, 53, 62, 39,
        48, 17, 2, 59, 20, 53, 62, 39, 48, 17, 2, 59, 20, 53, 62, 7, 48, 17, 2, 59, 20, 53, 62, 7,
        24, 33, 50, 19, 36, 29, 22, 55, 24, 33, 50, 19, 36, 29, 22, 55, 24, 33, 50, 19, 36, 29, 22, 55,
        56, 33, 50, 19, 36, 29, 22, 55, 56, 33, 50, 19, 36, 29, 22, 55, 56, 33, 50, 19, 36, 29, 22, 55,
        56, 33, 50, 19, 36, 29, 22, 55, 56, 33, 50, 19, 36, 61, 22, 55, 56, 1, 50, 19, 36, 61, 22, 55,
        56, 1, 50, 11, 36, 61, 22, 55, 56, 1, 50, 11, 36, 61, 22, 55, 56, 1, 50, 11, 36, 61, 22, 55,
        56, 1, 34, 11, 4, 61, 22, 55, 56, 1, 34, 11, 4, 61, 14, 55, 56, 1, 34, 11, 4, 61, 14, 55,
        56, 1, 34, 27, 4, 61, 14, 55, 56, 1, 34, 27, 4, 61, 14, 39, 56, 1, 34, 27, 4, 45, 14, 39,
        56, 1, 34, 27, 4, 45, 14, 39, 56, 1, 34, 27, 4, 45, 30, 39, 48, 1, 34, 27, 4, 45, 30, 39,
        48, 1, 34, 27, 12, 45, 30, 39, 48, 1, 34, 27, 12, 45, 30, 39, 48, 1, 34, 27, 12, 53, 30, 39,
        48, 17, 34, 27, 12, 53, 30, 39, 48, 17, 34, 59, 12, 53, 30, 39, 48, 17, 34, 59, 12, 53, 30, 39,
        48, 17, 34, 59, 20, 53, 30, 39, 48, 17, 34, 59, 20, 53, 30, 39, 48, 17, 2, 59, 20, 53, 62, 39,
        48, 17, 2, 59, 20, 53, 62, 39, 48, 17, 2, 59, 20, 53, 62, 39, 48, 17, 2, 59, 20, 53, 62, 39,
        48, 17, 2, 59, 20, 53, 62, 7, 48, 17, 2, 59, 20, 53, 62, 7, 48, 17, 2, 59, 20, 53, 62, 7,
        24, 33, 50, 19, 36, 29, 22, 55, 24, 33, 50, 19, 36, 29, 22, 55, 24, 33, 50, 19, 36, 29, 22, 55,
        24, 33, 50, 19, 36, 29, 22, 55, 56, 33, 50, 19, 36, 29, 22, 55, 56, 33, 50, 19, 36, 29, 22, 55,
        56, 33, 50, 19, 36, 29, 22, 55, 56, 33, 50, 19, 36, 29, 22, 55, 56, 33, 50, 11, 36, 61, 22, 55,
        56, 1, 50, 11, 36, 61, 22, 55, 56, 1, 50, 11, 36, 61, 22, 55, 56, 1, 50, 11, 36, 61, 22, 55,
        56, 1, 42, 11, 36, 61, 14, 55, 56, 1, 34, 11, 4, 61, 14, 55, 56, 1, 34, 11, 4, 61, 14, 55,
        56, 1, 34, 11, 4, 61, 14, 55, 56, 1, 34, 27, 4, 45, 14, 39, 56, 1, 34, 27, 4, 45, 14, 39,
        56, 1, 34, 27, 4, 45, 14, 39, 56, 1, 34, 27, 4, 45, 14, 39, 48, 1, 34, 27, 12, 45, 30, 39,
        48, 1, 34, 27, 12, 45, 30, 39, 48, 1, 34, 27, 12, 45, 30, 39, 48, 1, 34, 27, 12, 45, 30, 39,
        48, 17, 34, 59, 12, 53, 30, 39, 48, 17, 34, 59, 12, 53, 30, 39, 48, 17, 34, 59, 12, 53, 30, 39,
        48, 17, 34, 59, 12, 53, 30, 39, 48, 17, 2, 59, 20, 53, 62, 39, 48, 17, 2, 59, 20, 53, 62, 39,
        48, 17, 2, 59, 20, 53, 62, 39, 48, 17, 2, 59, 20, 53, 62, 39, 48, 17, 2, 59, 20, 53, 62, 7,
        48, 17, 2, 59, 20, 53, 62, 7, 48, 17, 2, 59, 20, 53, 62, 7, 48, 17, 2, 59, 20, 53, 62, 7,
        24, 33, 50, 19, 36, 29, 22, 55, 24, 33, 50, 19, 36, 29, 22, 55, 24, 33, 50, 19, 36, 29, 22, 55,
        24, 33, 50, 19, 36, 29, 22, 55, 24, 33, 50, 19, 36, 29, 22, 55, 56, 33, 50, 19, 36, 29, 22, 55,
        56, 33, 50, 19, 36, 29, 22, 55, 56, 33, 50, 11, 36, 29, 22, 55, 56, 33, 50, 11, 36, 29, 22, 55,
        56, 33, 50, 11, 36, 61, 22, 55, 56, 1, 50, 11, 36, 61, 22, 55, 56, 1, 42, 11, 36, 61, 14, 55,
        56, 1, 42, 11, 36, 61, 14, 55, 56, 1, 42, 11, 36, 61, 14, 55, 56, 1, 34, 11, 4, 61, 14, 55,
        56, 1, 34, 11, 4, 45, 14, 47, 56, 1, 34, 11, 4, 45, 14, 47, 56, 1, 34, 27, 4, 45, 14, 39,
        56, 1, 34, 27, 4, 45, 14, 39, 40, 1, 34, 27, 12, 45, 14, 39, 40, 1, 34, 27, 12, 45, 14, 39,
        48, 1, 34, 27, 12, 45, 30, 39, 48, 1, 34, 27, 12, 45, 30, 39, 48, 9, 34, 59, 12, 45, 30, 39,
        48, 9, 34, 59, 12, 45, 30, 39, 48, 17, 34, 59, 12, 53, 30, 39, 48, 17, 34, 59, 12, 53, 30, 39,
        48, 17, 2, 59, 12, 53, 62, 39, 48, 17, 2, 59, 12, 53, 62, 39, 48, 17, 2, 59, 20, 53, 62, 39,
        48, 17, 2, 59, 20, 53, 62, 39, 48, 17, 2, 59, 20, 53, 62, 7, 48, 17, 2, 59, 20, 53, 62, 7,
        48, 17, 2, 59, 20, 53, 62, 7, 48, 17, 2, 59, 20, 53, 62, 7, 48, 17, 2, 59, 20, 53, 62, 7,
        24, 33, 50, 19, 36, 29, 22, 55, 24, 33, 50, 19, 36, 29, 22, 55, 24, 33, 50, 19, 36, 29, 22, 55,
        24, 33, 50, 19, 36, 29, 22, 55, 24, 33, 50, 19, 36, 29, 22, 55, 24, 33, 50, 19, 36, 29, 22, 55,
        56, 33, 50, 11, 36, 29, 22, 55, 56, 33, 50, 11, 36, 29, 22, 55, 56, 33, 50, 11, 36, 29, 22, 55,
        56, 33, 50, 11, 36, 29, 22, 55, 56, 33, 42, 11, 36, 61, 14, 55, 56, 1, 42, 11, 36, 61, 14, 55,
        56, 1, 42, 11, 36, 61, 14, 55, 56, 1, 42, 11, 36, 61, 14, 55, 56, 1, 34, 11, 4, 45, 14, 47,
        56, 1, 34, 11, 4, 45, 14, 47, 56, 1, 34, 11, 4, 45, 14, 47, 56, 1, 34, 11, 4, 45, 14, 47,
        40, 1, 34, 27, 12, 45, 14, 39, 40, 1, 34, 27, 12, 45, 14, 39, 40, 1, 34, 27, 12, 45, 14, 39,
        40, 1, 34, 27, 12, 45, 14, 39, 48, 9, 34, 59, 12, 45, 30, 39, 48, 9, 34, 59, 12, 45, 30, 39,
        48, 9, 34, 59, 12, 45, 30, 39, 48, 9, 34, 59, 12, 45, 30, 39, 48, 17, 2, 59, 12, 53, 62, 39,
        48, 17, 2, 59, 12, 53, 62, 39, 48, 17, 2, 59, 12, 53, 62, 39, 48, 17, 2, 59, 12, 53, 62, 39,
        48, 17, 2, 59, 20, 53, 62, 7, 48, 17, 2, 59, 20, 53, 62, 7, 48, 17, 2, 59, 20, 53, 62, 7,
        48, 17, 2, 59, 20, 53, 62, 7, 48, 17, 2, 59, 20, 53, 62, 7, 48, 17, 2, 59, 20, 53, 62, 7,
        24, 33, 50, 19, 36, 29, 22, 55, 24, 33, 50, 19, 36, 29, 22, 55, 24, 33, 50, 19, 36, 29, 22, 55,
        24, 33, 50, 19, 36, 29, 22, 55, 24, 33, 50, 19, 36, 29, 22, 55, 24, 33, 50, 11, 36, 29, 22, 55,
        24, 33, 50, 11, 36, 29, 22, 55, 56, 33, 50, 11, 36, 29, 22, 55, 56, 33, 50, 11, 36, 29, 22, 55,
        56, 33, 42, 11, 36, 29, 14, 55, 56, 33, 42, 11, 36, 29, 14, 55, 56, 33, 42, 11, 36, 61, 14, 55,
        56, 1, 42, 11, 36, 61, 14, 55, 56, 1, 42, 11, 36, 45, 14, 47, 56, 1, 42, 11, 36, 45, 14, 47,
        56, 1, 34, 11, 4, 45, 14, 47, 56, 1, 34, 11, 4, 45, 14, 47, 40, 1, 34, 11, 12, 45, 14, 47,
        40, 1, 34, 11, 12, 45, 14, 47, 40, 1, 34, 27, 12, 45, 14, 39, 40, 1, 34, 27, 12, 45, 14, 39,
        40, 9, 34, 59, 12, 45, 14, 39, 40, 9, 34, 59, 12, 45, 14, 39, 48, 9, 34, 59, 12, 45, 30, 39,
        48, 9, 34, 59, 12, 45, 30, 39, 48, 9, 2, 59, 12, 45, 62, 39, 48, 9, 2, 59, 12, 45, 62, 39,
        48, 17, 2, 59, 12, 53, 62, 39, 48, 17, 2, 59, 12, 53, 62, 39, 48, 17, 2, 59, 12, 53, 62, 7,
        48, 17, 2, 59, 12, 53, 62, 7, 48, 17, 2, 59, 20, 53, 62, 7, 48, 17, 2, 59, 20, 53, 62, 7,
        48, 17, 2, 59, 20, 53, 62, 7, 48, 17, 2, 59, 20, 53, 62, 7, 48, 17, 2, 59, 20, 53, 62, 7,
        24, 33, 50, 3, 36, 29, 6, 55, 24, 33, 50, 3, 36, 29, 6, 55, 24, 33, 50, 3, 36, 29, 6, 55,
        24, 33, 50, 3, 36, 29, 6, 55, 24, 33, 50, 11, 36, 29, 6, 55, 24, 33, 50, 11, 36, 29, 6, 55,
        24, 33, 50, 11, 36, 29, 6, 55, 24, 33, 50, 11, 36, 29, 6, 55, 56, 33, 42, 11, 36, 29, 14, 55,
        56, 33, 42, 11, 36, 29, 14, 55, 56, 33, 42, 11, 36, 29, 14, 55, 56, 33, 42, 11, 36, 29, 14, 55,
        56, 33, 42, 11, 36, 45, 14, 47, 56, 1, 42, 11, 36, 45, 14, 47, 56, 1, 42, 11, 36, 45, 14, 47,
        56, 1, 42, 11, 36, 45, 14, 47, 40, 1, 34, 11, 12, 45, 14, 47, 40, 1, 34, 11, 12, 45, 14, 47,
        40, 1, 34, 11, 12, 45, 14, 47, 40, 1, 34, 11, 12, 45, 14, 47, 40, 9, 34, 59, 12, 45, 14, 39,
        40, 9, 34, 59, 12, 45, 14, 39, 40, 9, 34, 59, 12, 45, 14, 39, 40, 9, 34, 59, 12, 45, 14, 39,
        48, 9, 2, 59, 12, 45, 62, 39, 48, 9, 2, 59, 12, 45, 62, 39, 48, 9, 2, 59, 12, 45, 62, 39,
        48, 9, 2, 59, 12, 45, 62, 39, 48, 17, 2, 59, 12, 37, 62, 7, 48, 17, 2, 59, 12, 37, 62, 7,
        48, 17, 2, 59, 12, 37, 62, 7, 48, 17, 2, 59, 12, 37, 62, 7, 48, 17, 2, 59, 28, 37, 62, 7,
        48, 17, 2, 59, 28, 37, 62, 7, 48, 17, 2, 59, 28, 37, 62, 7, 48, 17, 2, 59, 28, 37, 62, 7,
        24, 33, 50, 3, 36, 29, 6, 55, 24, 33, 50, 3, 36, 29, 6, 55, 24, 33, 50, 3, 36, 29, 6, 55,
        24, 33, 50, 3, 36, 29, 6, 55, 24, 33, 50, 3, 36, 29, 6, 55, 24, 33, 50, 11, 36, 29, 6, 55,
        24, 33, 50, 11, 36, 29, 6, 55, 24, 33, 42, 11, 36, 29, 6, 55, 24, 33, 42, 11, 36, 29, 6, 55,
        56, 33, 42, 11, 36, 29, 14, 55, 56, 33, 42, 11, 36, 29, 14, 55, 56, 33, 42, 11, 36, 13, 14, 47,
        56, 33, 42, 11, 36, 13, 14, 47, 56, 33, 42, 11, 36, 45, 14, 47, 56, 1, 42, 11, 36, 45, 14, 47,
        40, 1, 42, 11, 44, 45, 14, 47, 40, 1, 42, 11, 44, 45, 14, 47, 40, 1, 34, 11, 12, 45, 14, 47,
        40, 1, 34, 11, 12, 45, 14, 47, 40, 9, 34, 43, 12, 45, 14, 47, 40, 9, 34, 43, 12, 45, 14, 47,
        40, 9, 34, 59, 12, 45, 14, 39, 40, 9, 34, 59, 12, 45, 14, 39, 40, 9, 2, 59, 12, 45, 46, 39,
        40, 9, 2, 59, 12, 45, 46, 39, 48, 9, 2, 59, 12, 45, 62, 39, 48, 9, 2, 59, 12, 45, 62, 39,
        48, 9, 2, 59, 12, 37, 62, 7, 48, 9, 2, 59, 12, 37, 62, 7, 48, 17, 2, 59, 12, 37, 62, 7,
        48, 17, 2, 59, 12, 37, 62, 7, 48, 17, 2, 59, 28, 37, 62, 7, 48, 17, 2, 59, 28, 37, 62, 7,
        48, 17, 2, 59, 28, 37, 62, 7, 48, 17, 2, 59, 28, 37, 62, 7, 48, 17, 2, 59, 28, 37, 62, 7,
        24, 33, 50, 3, 36, 29, 6, 55, 24, 33, 50, 3, 36, 29, 6, 55, 24, 33, 50, 3, 36, 29, 6, 55,
        24, 33, 50, 3, 36, 29, 6, 55, 24, 33, 50, 3, 36, 29, 6, 55, 24, 33, 50, 3, 36, 29, 6, 55,
        24, 33, 42, 3, 36, 29, 6, 55, 24, 33, 42, 11, 36, 29, 6, 55, 24, 33, 42, 11, 36, 29, 6, 55,
        24, 33, 42, 11, 36, 29, 6, 55, 24, 33, 42, 11, 36, 13, 6, 47, 56, 33, 42, 11, 36, 13, 14, 47,
        56, 33, 42, 11, 36, 13, 14, 47, 56, 33, 42, 11, 36, 13, 14, 47, 40, 33, 42, 11, 44, 45, 14, 47,
        40, 1, 42, 11, 44, 45, 14, 47, 40, 1, 42, 11, 44, 45, 14, 47, 40, 1, 42, 11, 44, 45, 14, 47,
        40, 9, 42, 43, 44, 45, 14, 47, 40, 9, 34, 43, 12, 45, 14, 47, 40, 9, 34, 43, 12, 45, 14, 47,
        40, 9, 34, 43, 12, 45, 14, 47, 40, 9, 2, 43, 12, 45, 46, 47, 40, 9, 2, 59, 12, 45, 46, 39,
        40, 9, 2, 59, 12, 45, 46, 39, 40, 9, 2, 59, 12, 45, 46, 39, 40, 9, 2, 59, 12, 37, 46, 7,
        48, 9, 2, 59, 12, 37, 62, 7, 48, 9, 2, 59, 12, 37, 62, 7, 48, 9, 2, 59, 12, 37, 62, 7,
        48, 9, 2, 59, 28, 37, 62, 7, 48, 17, 2, 59, 28, 37, 62, 7, 48, 17, 2, 59, 28, 37, 62, 7,
        48, 17, 2, 59, 28, 37, 62, 7, 48, 17, 2, 59, 28, 37, 62, 7, 48, 17, 2, 59, 28, 37, 62, 7,
        24, 33, 50, 3, 36, 29, 6, 55, 24, 33, 50, 3, 36, 29, 6, 55, 24, 33, 50, 3, 36, 29, 6, 55,
        24, 33, 50, 3, 36, 29, 6, 55, 24, 33, 50, 3, 36, 29, 6, 55, 24, 33, 42, 3, 36, 29, 6, 55,
        24, 33, 42, 3, 36, 29, 6, 55, 24, 33, 42, 3, 36, 29, 6, 55, 24, 33, 42, 11, 36, 29, 6, 55,
        24, 33, 42, 11, 36, 13, 6, 47, 24, 33, 42, 11, 36, 13, 6, 47, 24, 33, 42, 11, 36, 13, 6, 47,
        56, 33, 42, 11, 36, 13, 14, 47, 40, 33, 42, 11, 44, 13, 14, 47, 40, 33, 42, 11, 44, 13, 14, 47,
        40, 33, 42, 11, 44, 45, 14, 47, 40, 1, 42, 11, 44, 45, 14, 47, 40, 9, 42, 43, 44, 45, 14, 47,
        40, 9, 42, 43, 44, 45, 14, 47, 40, 9, 42, 43, 44, 45, 14, 47, 40, 9, 34, 43, 12, 45, 14, 47,
        40, 9, 2, 43, 12, 45, 46, 47, 40, 9, 2, 43, 12, 45, 46, 47, 40, 9, 2, 43, 12, 45, 46, 47,
        40, 9, 2, 59, 12, 45, 46, 39, 40, 9, 2, 59, 12, 37, 46, 7, 40, 9, 2, 59, 12, 37, 46, 7,
        40, 9, 2, 59, 12, 37, 46, 7, 48, 9, 2, 59, 12, 37, 62, 7, 48, 9, 2, 59, 28, 37, 62, 7,
        48, 9, 2, 59, 28, 37, 62, 7, 48, 9, 2, 59, 28, 37, 62, 7, 48, 17, 2, 59, 28, 37, 62, 7,
        48, 17, 2, 59, 28, 37, 62, 7, 48, 17, 2, 59, 28, 37, 62, 7, 48, 17, 2, 59, 28, 37, 62, 7,
        24, 33, 58, 3, 36, 29, 6, 63, 24, 33, 58, 3, 36, 29, 6, 63, 24, 33, 58, 3, 36, 29, 6, 63,
        24, 33, 58, 3, 36, 29, 6, 63, 24, 33, 58, 3, 36, 29, 6, 63, 24, 33, 42, 3, 36, 29, 6, 63,
        24, 33, 42, 3, 36, 29, 6, 63, 24, 33, 42, 3, 36, 29, 6, 63, 24, 33, 42, 3, 36, 13, 6, 63,
        24, 33, 42, 11, 36, 13, 6, 47, 24, 33, 42, 11, 36, 13, 6, 47, 24, 33, 42, 11, 36, 13, 6, 47,
        8, 33, 42, 11, 44, 13, 6, 47, 40, 33, 42, 11, 44, 13, 14, 47, 40, 33, 42, 11, 44, 13, 14, 47,
        40, 33, 42, 11, 44, 13, 14, 47, 40, 41, 42, 43, 44, 45, 14, 47, 40, 9, 42, 43, 44, 45, 14, 47,
        40, 9, 42, 43, 44, 45, 14, 47, 40, 9, 42, 43, 44, 45, 14, 47, 40, 9, 10, 43, 44, 45, 46, 47,
        40, 9, 2, 43, 12, 45, 46, 47, 40, 9, 2, 43, 12, 45, 46, 47, 40, 9, 2, 43, 12, 45, 46, 47,
        40, 9, 2, 43, 12, 37, 46, 15, 40, 9, 2, 59, 12, 37, 46, 7, 40, 9, 2, 59, 12, 37, 46, 7,
        40, 9, 2, 59, 12, 37, 46, 7, 32, 9, 2, 59, 28, 37, 46, 7, 32, 9, 2, 59, 28, 37, 62, 7,
        32, 9, 2, 59, 28, 37, 62, 7, 32, 9, 2, 59, 28, 37, 62, 7, 32, 25, 2, 59, 28, 37, 62, 7,
        32, 25, 2, 59, 28, 37, 62, 7, 32, 25, 2, 59, 28, 37, 62, 7, 32, 25, 2, 59, 28, 37, 62, 7,
        24, 33, 58, 3, 36, 29, 6, 63, 24, 33, 58, 3, 36, 29, 6, 63, 24, 33, 58, 3, 36, 29, 6, 63,
        24, 33, 58, 3, 36, 29, 6, 63, 24, 33, 58, 3, 36, 29, 6, 63, 24, 33, 58, 3, 36, 29, 6, 63,
        24, 33, 42, 3, 36, 29, 6, 63, 24, 33, 42, 3, 36, 13, 6, 63, 24, 33, 42, 3, 36, 13, 6, 63,
        24, 33, 42, 3, 36, 13, 6, 63, 24, 33, 42, 11, 36, 13, 6, 47, 8, 33, 42, 11, 44, 13, 6, 47,
        8, 33, 42, 11, 44, 13, 6, 47, 8, 33, 42, 11, 44, 13, 6, 47, 40, 33, 42, 11, 44, 13, 14, 47,
        40, 41, 42, 43, 44, 13, 14, 47, 40, 41, 42, 43, 44, 13, 14, 47, 40, 41, 42, 43, 44, 45, 14, 47,
        40, 9, 42, 43, 44, 45, 14, 47, 40, 9, 10, 43, 44, 45, 46, 47, 40, 9, 10, 43, 44, 45, 46, 47,
        40, 9, 10, 43, 44, 45, 46, 47, 40, 9, 2, 43, 12, 45, 46, 47, 40, 9, 2, 43, 12, 37, 46, 15,
        40, 9, 2, 43, 12, 37, 46, 15, 40, 9, 2, 43, 12, 37, 46, 15, 40, 9, 2, 59, 12, 37, 46, 7,
        32, 9, 2, 59, 28, 37, 46, 7, 32, 9, 2, 59, 28, 37, 46, 7, 32, 9, 2, 59, 28, 37, 46, 7,
        32, 9, 2, 59, 28, 37, 62, 7, 32, 25, 2, 59, 28, 37, 62, 7, 32, 25, 2, 59, 28, 37, 62, 7,
        32, 25, 2, 59, 28, 37, 62, 7, 32, 25, 2, 59, 28, 37, 62, 7, 32, 25, 2, 59, 28, 37, 62, 7,
        24, 33, 58, 3, 36, 29, 6, 63, 24, 33, 58, 3, 36, 29, 6, 63, 24, 33, 58, 3, 36, 29, 6, 63,
        24, 33, 58, 3, 36, 29, 6, 63, 24, 33, 58, 3, 36, 29, 6, 63, 24, 33, 58, 3, 36, 29, 6, 63,
        24, 33, 58, 3, 36, 13, 6, 63, 24, 33, 42, 3, 36, 13, 6, 63, 24, 33, 42, 3, 36, 13, 6, 63,
        24, 33, 42, 3, 36, 13, 6, 63, 8, 33, 42, 3, 44, 13, 6, 63, 8, 33, 42, 11, 44, 13, 6, 47,
        8, 33, 42, 11, 44, 13, 6, 47, 8, 33, 42, 11, 44, 13, 6, 47, 40, 41, 42, 43, 44, 13, 14, 47,
        40, 41, 42, 43, 44, 13, 14, 47, 40, 41, 42, 43, 44, 13, 14, 47, 40, 41, 42, 43, 44, 13, 14, 47,
        40, 41, 10, 43, 44, 45, 46, 47, 40, 9, 10, 43, 44, 45, 46, 47, 40, 9, 10, 43, 44, 45, 46, 47,
        40, 9, 10, 43, 44, 45, 46, 47, 40, 9, 10, 43, 44, 37, 46, 15, 40, 9, 2, 43, 12, 37, 46, 15,
        40, 9, 2, 43, 12, 37, 46, 15, 40, 9, 2, 43, 12, 37, 46, 15, 32, 9, 2, 43, 28, 37, 46, 15,
        32, 9, 2, 59, 28, 37, 46, 7, 32, 9, 2, 59, 28, 37, 46, 7, 32, 9, 2, 59, 28, 37, 46, 7,
        32, 25, 2, 59, 28, 37, 46, 7, 32, 25, 2, 59, 28, 37, 62, 7, 32, 25, 2, 59, 28, 37, 62, 7,
        32, 25, 2, 59, 28, 37, 62, 7, 32, 25, 2, 59, 28, 37, 62, 7, 32, 25, 2, 59, 28, 37, 62, 7,
        24, 33, 58, 3, 36, 29, 6, 63, 24, 33, 58, 3, 36, 29, 6, 63, 24, 33, 58, 3, 36, 29, 6, 63,
        24, 33, 58, 3, 36, 29, 6, 63, 24, 33, 58, 3, 36, 29, 6, 63, 24, 33, 58, 3, 36, 13, 6, 63,
        24, 33, 58, 3, 36, 13, 6, 63, 24, 33, 58, 3, 36, 13, 6, 63, 24, 33, 42, 3, 36, 13, 6, 63,
        8, 33, 42, 3, 44, 13, 6, 63, 8, 33, 42, 3, 44, 13, 6, 63, 8, 33, 42, 3, 44, 13, 6, 63,
        8, 33, 42, 11, 44, 13, 6, 47, 8, 41, 42, 43, 44, 13, 6, 47, 8, 41, 42, 43, 44, 13, 6, 47,
        40, 41, 42, 43, 44, 13, 14, 47, 40, 41, 42, 43, 44, 13, 14, 47, 40, 41, 10, 43, 44, 13, 46, 47,
        40, 41, 10, 43, 44, 13, 46, 47, 40, 41, 10, 43, 44, 45, 46, 47, 40, 9, 10, 43, 44, 45, 46, 47,
        40, 9, 10, 43, 44, 37, 46, 15, 40, 9, 10, 43, 44, 37, 46, 15, 40, 9, 10, 43, 44, 37, 46, 15,
        40, 9, 2, 43, 12, 37, 46, 15, 32, 9, 2, 43, 28, 37, 46, 15, 32, 9, 2, 43, 28, 37, 46, 15,
        32, 9, 2, 43, 28, 37, 46, 15, 32, 9, 2, 59, 28, 37, 46, 7, 32, 25, 2, 59, 28, 37, 46, 7,
        32, 25, 2, 59, 28, 37, 46, 7, 32, 25, 2, 59, 28, 37, 46, 7, 32, 25, 2, 59, 28, 37, 62, 7,
        32, 25, 2, 59, 28, 37, 62, 7, 32, 25, 2, 59, 28, 37, 62, 7, 32, 25, 2, 59, 28, 37, 62, 7,
        24, 33, 58, 3, 52, 21, 6, 63, 24, 33, 58, 3, 52, 21, 6, 63, 24, 33, 58, 3, 52, 21, 6, 63,
        24, 33, 58, 3, 52, 21, 6, 63, 24, 33, 58, 3, 52, 21, 6, 63, 24, 33, 58, 3, 52, 13, 6, 63,
        24, 33, 58, 3, 52, 13, 6, 63, 24, 33, 58, 3, 52, 13, 6, 63, 8, 33, 58, 3, 52, 13, 6, 63,
        8, 33, 42, 3, 44, 13, 6, 63, 8, 33, 42, 3, 44, 13, 6, 63, 8, 33, 42, 3, 44, 13, 6, 63,
        8, 41, 42, 35, 44, 13, 6, 63, 8, 41, 42, 43, 44, 13, 6, 47, 8, 41, 42, 43, 44, 13, 6, 47,
        8, 41, 42, 43, 44, 13, 6, 47, 40, 41, 10, 43, 44, 13, 46, 47, 40, 41, 10, 43, 44, 13, 46, 47,
        40, 41, 10, 43, 44, 13, 46, 47, 40, 41, 10, 43, 44, 13, 46, 47, 40, 41, 10, 43, 44, 37, 46, 15,
        40, 9, 10, 43, 44, 37, 46, 15, 40, 9, 10, 43, 44, 37, 46, 15, 40, 9, 10, 43, 44, 37, 46, 15,
        32, 9, 10, 43, 60, 37, 46, 15, 32, 9, 2, 43, 28, 37, 46, 15, 32, 9, 2, 43, 28, 37, 46, 15,
        32, 9, 2, 43, 28, 37, 46, 15, 32, 25, 2, 51, 28, 37, 46, 15, 32, 25, 2, 51, 28, 37, 46, 7,
        32, 25, 2, 51, 28, 37, 46, 7, 32, 25, 2, 51, 28, 37, 46, 7, 32, 25, 2, 51, 28, 37, 54, 7,
        32, 25, 2, 51, 28, 37, 54, 7, 32, 25, 2, 51, 28, 37, 54, 7, 32, 25, 2, 51, 28, 37, 54, 7,
        24, 33, 58, 3, 52, 21, 6, 63, 24, 33, 58, 3, 52, 21, 6, 63, 24, 33, 58, 3, 52, 21, 6, 63,
        24, 33, 58, 3, 52, 21, 6, 63, 24, 33, 58, 3, 52, 21, 6, 63, 24, 33, 58, 3, 52, 21, 6, 63,
        24, 33, 58, 3, 52, 13, 6, 63, 8, 33, 58, 3, 52, 13, 6, 63, 8, 33, 58, 3, 52, 13, 6, 63,
        8, 33, 58, 3, 52, 13, 6, 63, 8, 33, 42, 3, 44, 13, 6, 63, 8, 41, 42, 35, 44, 13, 6, 63,
        8, 41, 42, 35, 44, 13, 6, 63, 8, 41, 42, 35, 44, 13, 6, 63, 8, 41, 42, 43, 44, 13, 6, 47,
        8, 41, 10, 43, 44, 13, 38, 47, 8, 41, 10, 43, 44, 13, 38, 47, 40, 41, 10, 43, 44, 13, 46, 47,
        40, 41, 10, 43, 44, 13, 46, 47, 40, 41, 10, 43, 44, 5, 46, 15, 40, 41, 10, 43, 44, 5, 46, 15,
        40, 41, 10, 43, 44, 37, 46, 15, 40, 9, 10, 43, 44, 37, 46, 15, 32, 9, 10, 43, 60, 37, 46, 15,
        32, 9, 10, 43, 60, 37, 46, 15, 32, 9, 10, 43, 60, 37, 46, 15, 32, 9, 2, 43, 28, 37, 46, 15,
        32, 25, 2, 51, 28, 37, 46, 15, 32, 25, 2, 51, 28, 37, 46, 15, 32, 25, 2, 51, 28, 37, 46, 15,
        32, 25, 2, 51, 28, 37, 46, 7, 32, 25, 2, 51, 28, 37, 54, 7, 32, 25, 2, 51, 28, 37, 54, 7,
        32, 25, 2, 51, 28, 37, 54, 7, 32, 25, 2, 51, 28, 37, 54, 7, 32, 25, 2, 51, 28, 37, 54, 7,
        24, 33, 58, 3, 52, 21, 6, 63, 24, 33, 58, 3, 52, 21, 6, 63, 24, 33, 58, 3, 52, 21, 6, 63,
        24, 33, 58, 3, 52, 21, 6, 63, 24, 33, 58, 3, 52, 21, 6, 63, 24, 33, 58, 3, 52, 21, 6, 63,
        8, 33, 58, 3, 52, 21, 6, 63, 8, 33, 58, 3, 52, 13, 6, 63, 8, 33, 58, 3, 52, 13, 6, 63,
        8, 33, 58, 3, 52, 13, 6, 63, 8, 41, 58, 35, 52, 13, 6, 63, 8, 41, 42, 35, 44, 13, 6, 63,
        8, 41, 42, 35, 44, 13, 6, 63, 8, 41, 42, 35, 44, 13, 6, 63, 8, 41, 10, 43, 44, 13, 38, 47,
        8, 41, 10, 43, 44, 13, 38, 47, 8, 41, 10, 43, 44, 13, 38, 47, 8, 41, 10, 43, 44, 13, 38, 47,
        40, 41, 10, 43, 44, 5, 46, 15, 40, 41, 10, 43, 44, 5, 46, 15, 40, 41, 10, 43, 44, 5, 46, 15,
        40, 41, 10, 43, 44, 5, 46, 15, 32, 41, 10, 43, 60, 37, 46, 15, 32, 9, 10, 43, 60, 37, 46, 15,
        32, 9, 10, 43, 60, 37, 46, 15, 32, 9, 10, 43, 60, 37, 46, 15, 32, 25, 10, 51, 60, 37, 46, 15,
        32, 25, 2, 51, 28, 37, 46, 15, 32, 25, 2, 51, 28, 37, 46, 15, 32, 25, 2, 51, 28, 37, 46, 15,
        32, 25, 2, 51, 28, 37, 54, 15, 32, 25, 2, 51, 28, 37, 54, 7, 32, 25, 2, 51, 28, 37, 54, 7,
        32, 25, 2, 51, 28, 37, 54, 7, 32, 25, 2, 51, 28, 37, 54, 7, 32, 25, 2, 51, 28, 37, 54, 7,
        24, 33, 58, 3, 52, 21, 6, 63, 24, 33, 58, 3, 52, 21, 6, 63, 24, 33, 58, 3, 52, 21, 6, 63,
        24, 33, 58, 3, 52, 21, 6, 63, 24, 33, 58, 3, 52, 21, 6, 63, 8, 33, 58, 3, 52, 21, 6, 63,
        8, 33, 58, 3, 52, 21, 6, 63, 8, 33, 58, 3, 52, 21, 6, 63, 8, 33, 58, 3, 52, 13, 6, 63,
        8, 41, 58, 35, 52, 13, 6, 63, 8, 41, 58, 35, 52, 13, 6, 63, 8, 41, 58, 35, 52, 13, 6, 63,
        8, 41, 42, 35, 44, 13, 6, 63, 8, 41, 10, 35, 44, 13, 38, 63, 8, 41, 10, 35, 44, 13, 38, 63,
        8, 41, 10, 43, 44, 13, 38, 47, 8, 41, 10, 43, 44, 13, 38, 47, 8, 41, 10, 43, 44, 5, 38, 15,
        8, 41, 10, 43, 44, 5, 38, 15, 40, 41, 10, 43, 44, 5, 46, 15, 40, 41, 10, 43, 44, 5, 46, 15,
        32, 41, 10, 43, 60, 5, 46, 15, 32, 41, 10, 43, 60, 5, 46, 15, 32, 41, 10, 43, 60, 37, 46, 15,
        32, 9, 10, 43, 60, 37, 46, 15, 32, 25, 10, 51, 60, 37, 46, 15, 32, 25, 10, 51, 60, 37, 46, 15,
        32, 25, 10, 51, 60, 37, 46, 15, 32, 25, 2, 51, 28, 37, 46, 15, 32, 25, 2, 51, 28, 37, 54, 15,
        32, 25, 2, 51, 28, 37, 54, 15, 32, 25, 2, 51, 28, 37, 54, 15, 32, 25, 2, 51, 28, 37, 54, 7,
        32, 25, 2, 51, 28, 37, 54, 7, 32, 25, 2, 51, 28, 37, 54, 7, 32, 25, 2, 51, 28, 37, 54, 7,
        16, 49, 58, 3, 52, 21, 6, 63, 16, 49, 58, 3, 52, 21, 6, 63, 16, 49, 58, 3, 52, 21, 6, 63,
        16, 49, 58, 3, 52, 21, 6, 63, 16, 49, 58, 3, 52, 21, 6, 63, 8, 49, 58, 3, 52, 21, 6, 63,
        8, 49, 58, 3, 52, 21, 6, 63, 8, 49, 58, 3, 52, 21, 6, 63, 8, 49, 58, 35, 52, 21, 6, 63,
        8, 41, 58, 35, 52, 13, 6, 63, 8, 41, 58, 35, 52, 13, 6, 63, 8, 41, 58, 35, 52, 13, 6, 63,
        8, 41, 26, 35, 52, 13, 38, 63, 8, 41, 10, 35, 44, 13, 38, 63, 8, 41, 10, 35, 44, 13, 38, 63,
        8, 41, 10, 35, 44, 13, 38, 63, 8, 41, 10, 43, 44, 5, 38, 15, 8, 41, 10, 43, 44, 5, 38, 15,
        8, 41, 10, 43, 44, 5, 38, 15, 8, 41, 10, 43, 44, 5, 38, 15, 32, 41, 10, 43, 60, 5, 46, 15,
        32, 41, 10, 43, 60, 5, 46, 15, 32, 41, 10, 43, 60, 5, 46, 15, 32, 41, 10, 43, 60, 5, 46, 15,
        32, 57, 10, 51, 60, 37, 46, 15, 32, 25, 10, 51, 60, 37, 46, 15, 32, 25, 10, 51, 60, 37, 46, 15,
        32, 25, 10, 51, 60, 37, 46, 15, 32, 25, 18, 51, 60, 37, 54, 15, 32, 25, 18, 51, 28, 37, 54, 15,
        32, 25, 18, 51, 28, 37, 54, 15, 32, 25, 18, 51, 28, 37, 54, 15, 32, 25, 18, 51, 28, 37, 54, 23,
        32, 25, 18, 51, 28, 37, 54, 23, 32, 25, 18, 51, 28, 37, 54, 23, 32, 25, 18, 51, 28, 37, 54, 23,
        16, 49, 58, 3, 52, 21, 6, 63, 16, 49, 58, 3, 52, 21, 6, 63, 16, 49, 58, 3, 52, 21, 6, 63,
        16, 49, 58, 3, 52, 21, 6, 63, 16, 49, 58, 3, 52, 21, 6, 63, 16, 49, 58, 3, 52, 21, 6, 63,
        8, 49, 58, 3, 52, 21, 6, 63, 8, 49, 58, 35, 52, 21, 6, 63, 8, 49, 58, 35, 52, 21, 6, 63,
        8, 49, 58, 35, 52, 21, 6, 63, 8, 41, 58, 35, 52, 13, 6, 63, 8, 41, 26, 35, 52, 13, 38, 63,
        8, 41, 26, 35, 52, 13, 38, 63, 8, 41, 26, 35, 52, 13, 38, 63, 8, 41, 10, 35, 44, 13, 38, 63,
        8, 41, 10, 35, 44, 5, 38, 31, 8, 41, 10, 35, 44, 5, 38, 31, 8, 41, 10, 43, 44, 5, 38, 15,
        8, 41, 10, 43, 44, 5, 38, 15, 0, 41, 10, 43, 60, 5, 38, 15, 0, 41, 10, 43, 60, 5, 38, 15,
        32, 41, 10, 43, 60, 5, 46, 15, 32, 41, 10, 43, 60, 5, 46, 15, 32, 57, 10, 51, 60, 5, 46, 15,
        32, 57, 10, 51, 60, 5, 46, 15, 32, 57, 10, 51, 60, 37, 46, 15, 32, 25, 10, 51, 60, 37, 46, 15,
        32, 25, 18, 51, 60, 37, 54, 15, 32, 25, 18, 51, 60, 37, 54, 15, 32, 25, 18, 51, 60, 37, 54, 15,
        32, 25, 18, 51, 28, 37, 54, 15, 32, 25, 18, 51, 28, 37, 54, 23, 32, 25, 18, 51, 28, 37, 54, 23,
        32, 25, 18, 51, 28, 37, 54, 23, 32, 25, 18, 51, 28, 37, 54, 23, 32, 25, 18, 51, 28, 37, 54, 23,
        16, 49, 58, 3, 52, 21, 6, 63, 16, 49, 58, 3, 52, 21, 6, 63, 16, 49, 58, 3, 52, 21, 6, 63,
        16, 49, 58, 3, 52, 21, 6, 63, 16, 49, 58, 3, 52, 21, 6, 63, 16, 49, 58, 3, 52, 21, 6, 63,
        16, 49, 58, 35, 52, 21, 6, 63, 8, 49, 58, 35, 52, 21, 6, 63, 8, 49, 58, 35, 52, 21, 6, 63,
        8, 49, 58, 35, 52, 21, 6, 63, 8, 49, 26, 35, 52, 21, 38, 63, 8, 41, 26, 35, 52, 13, 38, 63,
        8, 41, 26, 35, 52, 13, 38, 63, 8, 41, 26, 35, 52, 13, 38, 63, 8, 41, 10, 35, 44, 5, 38, 31,
        8, 41, 10, 35, 44, 5, 38, 31, 8, 41, 10, 35, 44, 5, 38, 31, 8, 41, 10, 35, 44, 5, 38, 31,
        0, 41, 10, 43, 60, 5, 38, 15, 0, 41, 10, 43, 60, 5, 38, 15, 0, 41, 10, 43, 60, 5, 38, 15,
        0, 41, 10, 43, 60, 5, 38, 15, 32, 57, 10, 51, 60, 5, 46, 15, 32, 57, 10, 51, 60, 5, 46, 15,
        32, 57, 10, 51, 60, 5, 46, 15, 32, 57, 10, 51, 60, 5, 46, 15, 32, 57, 18, 51, 60, 37, 54, 15,
        32, 25, 18, 51, 60, 37, 54, 15, 32, 25, 18, 51, 60, 37, 54, 15, 32, 25, 18, 51, 60, 37, 54, 15,
        32, 25, 18, 51, 60, 37, 54, 23, 32, 25, 18, 51, 28, 37, 54, 23, 32, 25, 18, 51, 28, 37, 54, 23,
        32, 25, 18, 51, 28, 37, 54, 23, 32, 25, 18, 51, 28, 37, 54, 23, 32, 25, 18, 51, 28, 37, 54, 23,
        16, 49, 58, 3, 52, 21, 6, 63, 16, 49, 58, 3, 52, 21, 6, 63, 16, 49, 58, 3, 52, 21, 6, 63,
        16, 49, 58, 3, 52, 21, 6, 63, 16, 49, 58, 3, 52, 21, 6, 63, 16, 49, 58, 35, 52, 21, 6, 63,
        16, 49, 58, 35, 52, 21, 6, 63, 16, 49, 58, 35, 52, 21, 6, 63, 8, 49, 58, 35, 52, 21, 6, 63,
        8, 49, 26, 35, 52, 21, 38, 63, 8, 49, 26, 35, 52, 21, 38, 63, 8, 49, 26, 35, 52, 21, 38, 63,
        8, 41, 26, 35, 52, 13, 38, 63, 8, 41, 26, 35, 52, 5, 38, 31, 8, 41, 26, 35, 52, 5, 38, 31,
        8, 41, 10, 35, 44, 5, 38, 31, 8, 41, 10, 35, 44, 5, 38, 31, 0, 41, 10, 35, 60, 5, 38, 31,
        0, 41, 10, 35, 60, 5, 38, 31, 0, 41, 10, 43, 60, 5, 38, 15, 0, 41, 10, 43, 60, 5, 38, 15,
        0, 57, 10, 51, 60, 5, 38, 15, 0, 57, 10, 51, 60, 5, 38, 15, 32, 57, 10, 51, 60, 5, 46, 15,
        32, 57, 10, 51, 60, 5, 46, 15, 32, 57, 18, 51, 60, 5, 54, 15, 32, 57, 18, 51, 60, 5, 54, 15,
        32, 57, 18, 51, 60, 37, 54, 15, 32, 25, 18, 51, 60, 37, 54, 15, 32, 25, 18, 51, 60, 37, 54, 23,
        32, 25, 18, 51, 60, 37, 54, 23, 32, 25, 18, 51, 60, 37, 54, 23, 32, 25, 18, 51, 28, 37, 54, 23,
        32, 25, 18, 51, 28, 37, 54, 23, 32, 25, 18, 51, 28, 37, 54, 23, 32, 25, 18, 51, 28, 37, 54, 23,
        16, 49, 58, 3, 52, 21, 6, 63, 16, 49, 58, 3, 52, 21, 6, 63, 16, 49, 58, 3, 52, 21, 6, 63,
        16, 49, 58, 3, 52, 21, 6, 63, 16, 49, 58, 35, 52, 21, 6, 63, 16, 49, 58, 35, 52, 21, 6, 63,
        16, 49, 58, 35, 52, 21, 6, 63, 16, 49, 58, 35, 52, 21, 6, 63, 16, 49, 26, 35, 52, 21, 38, 63,
        8, 49, 26, 35, 52, 21, 38, 63, 8, 49, 26, 35, 52, 21, 38, 63, 8, 49, 26, 35, 52, 21, 38, 63,
        8, 49, 26, 35, 52, 5, 38, 31, 8, 41, 26, 35, 52, 5, 38, 31, 8, 41, 26, 35, 52, 5, 38, 31,
        8, 41, 26, 35, 52, 5, 38, 31, 0, 41, 10, 35, 60, 5, 38, 31, 0, 41, 10, 35, 60, 5, 38, 31,
        0, 41, 10, 35, 60, 5, 38, 31, 0, 41, 10, 35, 60, 5, 38, 31, 0, 57, 10, 51, 60, 5, 38, 15,
        0, 57, 10, 51, 60, 5, 38, 15, 0, 57, 10, 51, 60, 5, 38, 15, 0, 57, 10, 51, 60, 5, 38, 15,
        32, 57, 18, 51, 60, 5, 54, 15, 32, 57, 18, 51, 60, 5, 54, 15, 32, 57, 18, 51, 60, 5, 54, 15,
        32, 57, 18, 51, 60, 5, 54, 15, 32, 57, 18, 51, 60, 37, 54, 23, 32, 25, 18, 51, 60, 37, 54, 23,
        32, 25, 18, 51, 60, 37, 54, 23, 32, 25, 18, 51, 60, 37, 54, 23, 32, 25, 18, 51, 60, 37, 54, 23,
        32, 25, 18, 51, 28, 37, 54, 23, 32, 25, 18, 51, 28, 37, 54, 23, 32, 25, 18, 51, 28, 37, 54, 23,
        16, 49, 58, 3, 52, 21, 6, 63, 16, 49, 58, 3, 52, 21, 6, 63, 16, 49, 58, 3, 52, 21, 6, 63,
        16, 49, 58, 35, 52, 21, 6, 63, 16, 49, 58, 35, 52, 21, 6, 63, 16, 49, 58, 35, 52, 21, 6, 63,
        16, 49, 58, 35, 52, 21, 6, 63, 16, 49, 26, 35, 52, 21, 38, 63, 16, 49, 26, 35, 52, 21, 38, 63,
        16, 49, 26, 35, 52, 21, 38, 63, 8, 49, 26, 35, 52, 21, 38, 63, 8, 49, 26, 35, 52, 21, 38, 31,
        8, 49, 26, 35, 52, 5, 38, 31, 8, 49, 26, 35, 52, 5, 38, 31, 8, 41, 26, 35, 52, 5, 38, 31,
        0, 41, 26, 35, 52, 5, 38, 31, 0, 41, 26, 35, 60, 5, 38, 31, 0, 41, 10, 35, 60, 5, 38, 31,
        0, 41, 10, 35, 60, 5, 38, 31, 0, 57, 10, 35, 60, 5, 38, 31, 0, 57, 10, 51, 60, 5, 38, 31,
        0, 57, 10, 51, 60, 5, 38, 15, 0, 57, 10, 51, 60, 5, 38, 15, 0, 57, 18, 51, 60, 5, 38, 15,
        0, 57, 18, 51, 60, 5, 54, 15, 32, 57, 18, 51, 60, 5, 54, 15, 32, 57, 18, 51, 60, 5, 54, 15,
        32, 57, 18, 51, 60, 5, 54, 23, 32, 57, 18, 51, 60, 5, 54, 23, 32, 57, 18, 51, 60, 37, 54, 23,
        32, 25, 18, 51, 60, 37, 54, 23, 32, 25, 18, 51, 60, 37, 54, 23, 32, 25, 18, 51, 60, 37, 54, 23,
        32, 25, 18, 51, 60, 37, 54, 23, 32, 25, 18, 51, 28, 37, 54, 23, 32, 25, 18, 51, 28, 37, 54, 23,
        16, 49, 58, 3, 52, 21, 6, 63, 16, 49, 58, 3, 52, 21, 6, 63, 16, 49, 58, 35, 52, 21, 6, 63,
        16, 49, 58, 35, 52, 21, 6, 63, 16, 49, 58, 35, 52, 21, 6, 63, 16, 49, 58, 35, 52, 21, 6, 63,
        16, 49, 26, 35, 52, 21, 38, 63, 16, 49, 26, 35, 52, 21, 38, 63, 16, 49, 26, 35, 52, 21, 38, 63,
        16, 49, 26, 35, 52, 21, 38, 63, 16, 49, 26, 35, 52, 21, 38, 31, 8, 49, 26, 35, 52, 21, 38, 31,
        8, 49, 26, 35, 52, 5, 38, 31, 8, 49, 26, 35, 52, 5, 38, 31, 0, 41, 26, 35, 52, 5, 38, 31,
        0, 41, 26, 35, 52, 5, 38, 31, 0, 41, 26, 35, 60, 5, 38, 31, 0, 41, 26, 35, 60, 5, 38, 31,
        0, 57, 10, 35, 60, 5, 38, 31, 0, 57, 10, 35, 60, 5, 38, 31, 0, 57, 10, 51, 60, 5, 38, 31,
        0, 57, 10, 51, 60, 5, 38, 31, 0, 57, 18, 51, 60, 5, 38, 31, 0, 57, 18, 51, 60, 5, 38, 15,
        0, 57, 18, 51, 60, 5, 54, 15, 0, 57, 18, 51, 60, 5, 54, 15, 32, 57, 18, 51, 60, 5, 54, 23,
        32, 57, 18, 51, 60, 5, 54, 23, 32, 57, 18, 51, 60, 5, 54, 23, 32, 57, 18, 51, 60, 5, 54, 23,
        32, 57, 18, 51, 60, 37, 54, 23, 32, 25, 18, 51, 60, 37, 54, 23, 32, 25, 18, 51, 60, 37, 54, 23,
        32, 25, 18, 51, 60, 37, 54, 23, 32, 25, 18, 51, 28, 37, 54, 23, 32, 25, 18, 51, 28, 37, 54, 23,
        16, 49, 58, 3, 52, 21, 6, 63, 16, 49, 58, 35, 52, 21, 6, 63, 16, 49, 58, 35, 52, 21, 6, 63,
        16, 49, 58, 35, 52, 21, 6, 63, 16, 49, 58, 35, 52, 21, 6, 63, 16, 49, 26, 35, 52, 21, 38, 63,
        16, 49, 26, 35, 52, 21, 38, 63, 16, 49, 26, 35, 52, 21, 38, 63, 16, 49, 26, 35, 52, 21, 38, 63,
        16, 49, 26, 35, 52, 21, 38, 31, 16, 49, 26, 35, 52, 21, 38, 31, 16, 49, 26, 35, 52, 21, 38, 31,
        8, 49, 26, 35, 52, 5, 38, 31, 0, 49, 26, 35, 52, 5, 38, 31, 0, 49, 26, 35, 52, 5, 38, 31,
        0, 41, 26, 35, 52, 5, 38, 31, 0, 41, 26, 35, 60, 5, 38, 31, 0, 57, 26, 35, 60, 5, 38, 31,
        0, 57, 26, 35, 60, 5, 38, 31, 0, 57, 10, 35, 60, 5, 38, 31, 0, 57, 10, 51, 60, 5, 38, 31,
        0, 57, 18, 51, 60, 5, 38, 31, 0, 57, 18, 51, 60, 5, 38, 31, 0, 57, 18, 51, 60, 5, 38, 31,
        0, 57, 18, 51, 60, 5, 54, 15, 0, 57, 18, 51, 60, 5, 54, 23, 0, 57, 18, 51, 60, 5, 54, 23,
        32, 57, 18, 51, 60, 5, 54, 23, 32, 57, 18, 51, 60, 5, 54, 23, 32, 57, 18, 51, 60, 5, 54, 23,
        32, 57, 18, 51, 60, 5, 54, 23, 32, 57, 18, 51, 60, 37, 54, 23, 32, 25, 18, 51, 60, 37, 54, 23,
        32, 25, 18, 51, 60, 37, 54, 23, 32, 25, 18, 51, 60, 37, 54, 23, 32, 25, 18, 51, 28, 37, 54, 23,
        16, 49, 58, 35, 52, 21, 6, 63, 16, 49, 58, 35, 52, 21, 6, 63, 16, 49, 58, 35, 52, 21, 6, 63,
        16, 49, 58, 35, 52, 21, 6, 63, 16, 49, 26, 35, 52, 21, 38, 63, 16, 49, 26, 35, 52, 21, 38, 63,
        16, 49, 26, 35, 52, 21, 38, 63, 16, 49, 26, 35, 52, 21, 38, 63, 16, 49, 26, 35, 52, 21, 38, 31,
        16, 49, 26, 35, 52, 21, 38, 31, 16, 49, 26, 35, 52, 21, 38, 31, 16, 49, 26, 35, 52, 21, 38, 31,
        0, 49, 26, 35, 52, 5, 38, 31, 0, 49, 26, 35, 52, 5, 38, 31, 0, 49, 26, 35, 52, 5, 38, 31,
        0, 49, 26, 35, 52, 5, 38, 31, 0, 57, 26, 35, 60, 5, 38, 31, 0, 57, 26, 35, 60, 5, 38, 31,
        0, 57, 26, 35, 60, 5, 38, 31, 0, 57, 26, 35, 60, 5, 38, 31, 0, 57, 18, 51, 60, 5, 38, 31,
        0, 57, 18, 51, 60, 5, 38, 31, 0, 57, 18, 51, 60, 5, 38, 31, 0, 57, 18, 51, 60, 5, 38, 31,
        0, 57, 18, 51, 60, 5, 54, 23, 0, 57, 18, 51, 60, 5, 54, 23, 0, 57, 18, 51, 60, 5, 54, 23,
        0, 57, 18, 51, 60, 5, 54, 23, 32, 57, 18, 51, 60, 5, 54, 23, 32, 57, 18, 51, 60, 5, 54, 23,
        32, 57, 18, 51, 60, 5, 54, 23, 32, 57, 18, 51, 60, 5, 54, 23, 32, 57, 18, 51, 60, 37, 54, 23,
        32, 25, 18, 51, 60, 37, 54, 23, 32, 25, 18, 51, 60, 37, 54, 23, 32, 25, 18, 51, 60, 37, 54, 23,
        16, 49, 58, 35, 52, 21, 6, 63, 16, 49, 58, 35, 52, 21, 6, 63, 16, 49, 58, 35, 52, 21, 6, 63,
        16, 49, 26, 35, 52, 21, 38, 63, 16, 49, 26, 35, 52, 21, 38, 63, 16, 49, 26, 35, 52, 21, 38, 63,
        16, 49, 26, 35, 52, 21, 38, 63, 16, 49, 26, 35, 52, 21, 38, 31, 16, 49, 26, 35, 52, 21, 38, 31,
        16, 49, 26, 35, 52, 21, 38, 31, 16, 49, 26, 35, 52, 21, 38, 31, 16, 49, 26, 35, 52, 21, 38, 31,
        0, 49, 26, 35, 52, 5, 38, 31, 0, 49, 26, 35, 52, 5, 38, 31, 0, 49, 26, 35, 52, 5, 38, 31,
        0, 49, 26, 35, 52, 5, 38, 31, 0, 57, 26, 35, 60, 5, 38, 31, 0, 57, 26, 35, 60, 5, 38, 31,
        0, 57, 26, 35, 60, 5, 38, 31, 0, 57, 26, 35, 60, 5, 38, 31, 0, 57, 18, 51, 60, 5, 38, 31,
        0, 57, 18, 51, 60, 5, 38, 31, 0, 57, 18, 51, 60, 5, 38, 31, 0, 57, 18, 51, 60, 5, 38, 31,
        0, 57, 18, 51, 60, 5, 54, 23, 0, 57, 18, 51, 60, 5, 54, 23, 0, 57, 18, 51, 60, 5, 54, 23,
        0, 57, 18, 51, 60, 5, 54, 23, 0, 57, 18, 51, 60, 5, 54, 23, 32, 57, 18, 51, 60, 5, 54, 23,
        32, 57, 18, 51, 60, 5, 54, 23, 32, 57, 18, 51, 60, 5, 54, 23, 32, 57, 18, 51, 60, 5, 54, 23,
        32, 57, 18, 51, 60, 37, 54, 23, 32, 25, 18, 51, 60, 37, 54, 23, 32, 25, 18, 51, 60, 37, 54, 23,
        16, 49, 58, 35, 52, 21, 6, 63, 16, 49, 58, 35, 52, 21, 6, 63, 16, 49, 26, 35, 52, 21, 38, 63,
        16, 49, 26, 35, 52, 21, 38, 63, 16, 49, 26, 35, 52, 21, 38, 63, 16, 49, 26, 35, 52, 21, 38, 63,
        16, 49, 26, 35, 52, 21, 38, 31, 16, 49, 26, 35, 52, 21, 38, 31, 16, 49, 26, 35, 52, 21, 38, 31,
        16, 49, 26, 35, 52, 21, 38, 31, 16, 49, 26, 35, 52, 21, 38, 31, 16, 49, 26, 35, 52, 21, 38, 31,
        0, 49, 26, 35, 52, 5, 38, 31, 0, 49, 26, 35, 52, 5, 38, 31, 0, 49, 26, 35, 52, 5, 38, 31,
        0, 49, 26, 35, 52, 5, 38, 31, 0, 57, 26, 35, 60, 5, 38, 31, 0, 57, 26, 35, 60, 5, 38, 31,
        0, 57, 26, 35, 60, 5, 38, 31, 0, 57, 26, 35, 60, 5, 38, 31, 0, 57, 18, 51, 60, 5, 38, 31,
        0, 57, 18, 51, 60, 5, 38, 31, 0, 57, 18, 51, 60, 5, 38, 31, 0, 57, 18, 51, 60, 5, 38, 31,
        0, 57, 18, 51, 60, 5, 54, 23, 0, 57, 18, 51, 60, 5, 54, 23, 0, 57, 18, 51, 60, 5, 54, 23,
        0, 57, 18, 51, 60, 5, 54, 23, 0, 57, 18, 51, 60, 5, 54, 23, 0, 57, 18, 51, 60, 5, 54, 23,
        0, 57, 18, 51, 60, 5, 54, 23, 32, 57, 18, 51, 60, 5, 54, 23, 32, 57, 18, 51, 60, 5, 54, 23,
        32, 57, 18, 51, 60, 5, 54, 23, 32, 57, 18, 51, 60, 37, 54, 23, 32, 25, 18, 51, 60, 37, 54, 23,
        16, 49, 58, 35, 52, 21, 6, 63, 16, 49, 26, 35, 52, 21, 38, 63, 16, 49, 26, 35, 52, 21, 38, 63,
        16, 49, 26, 35, 52, 21, 38, 63, 16, 49, 26, 35, 52, 21, 38, 63, 16, 49, 26, 35, 52, 21, 38, 31,
        16, 49, 26, 35, 52, 21, 38, 31, 16, 49, 26, 35, 52, 21, 38, 31, 16, 49, 26, 35, 52, 21, 38, 31,
        16, 49, 26, 35, 52, 21, 38, 31, 16, 49, 26, 35, 52, 21, 38, 31, 16, 49, 26, 35, 52, 21, 38, 31,
        0, 49, 26, 35, 52, 5, 38, 31, 0, 49, 26, 35, 52, 5, 38, 31, 0, 49, 26, 35, 52, 5, 38, 31,
        0, 49, 26, 35, 52, 5, 38, 31, 0, 57, 26, 35, 60, 5, 38, 31, 0, 57, 26, 35, 60, 5, 38, 31,
        0, 57, 26, 35, 60, 5, 38, 31, 0, 57, 26, 35, 60, 5, 38, 31, 0, 57, 18, 51, 60, 5, 38, 31,
        0, 57, 18, 51, 60, 5, 38, 31, 0, 57, 18, 51, 60, 5, 38, 31, 0, 57, 18, 51, 60, 5, 38, 31,
        0, 57, 18, 51, 60, 5, 54, 23, 0, 57, 18, 51, 60, 5, 54, 23, 0, 57, 18, 51, 60, 5, 54, 23,
        0, 57, 18, 51, 60, 5, 54, 23, 0, 57, 18, 51, 60, 5, 54, 23, 0, 57, 18, 51, 60, 5, 54, 23,
        0, 57, 18, 51, 60, 5, 54, 23, 0, 57, 18, 51, 60, 5, 54, 23, 32, 57, 18, 51, 60, 5, 54, 23,
        32, 57, 18, 51, 60, 5, 54, 23, 32, 57, 18, 51, 60, 5, 54, 23, 32, 57, 18, 51, 60, 37, 54, 23,
        16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7,
        16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7, 0, 1, 18, 19, 28, 29, 14, 7,
        0, 1, 18, 19, 28, 29, 14, 7, 0, 1, 18, 19, 28, 29, 14, 7, 0, 1, 18, 19, 28, 29, 14, 7,
        0, 1, 18, 19, 28, 29, 14, 7, 0, 1, 18, 19, 28, 29, 14, 15, 0, 1, 18, 19, 28, 29, 14, 15,
        0, 1, 18, 19, 28, 29, 14, 15, 0, 1, 18, 19, 28, 29, 14, 15, 0, 1, 18, 19, 28, 29, 30, 15,
        0, 1, 18, 19, 28, 29, 30, 15, 0, 1, 18, 19, 28, 29, 30, 15, 0, 1, 18, 19, 28, 29, 30, 15,
        0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 18, 19, 28, 21, 30, 15,
        0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 15,
        0, 9, 2, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 31,
        0, 9, 2, 19, 28, 21, 30, 31, 0, 9, 2, 19, 28, 21, 30, 31, 0, 9, 2, 19, 28, 21, 30, 31,
        0, 9, 2, 19, 28, 21, 30, 31, 0, 9, 2, 19, 28, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31,
        0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31,
        16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7,
        16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7,
        0, 1, 18, 19, 28, 29, 14, 7, 0, 1, 18, 19, 28, 29, 14, 7, 0, 1, 18, 19, 28, 29, 14, 7,
        0, 1, 18, 19, 28, 29, 14, 7, 0, 1, 18, 19, 28, 29, 14, 15, 0, 1, 18, 19, 28, 29, 14, 15,
        0, 1, 18, 19, 28, 29, 14, 15, 0, 1, 18, 19, 28, 29, 14, 15, 0, 1, 18, 19, 28, 29, 30, 15,
        0, 1, 18, 19, 28, 29, 30, 15, 0, 1, 18, 19, 28, 29, 30, 15, 0, 1, 18, 19, 28, 29, 30, 15,
        0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 18, 19, 28, 21, 30, 15,
        0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 15,
        0, 9, 2, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 31,
        0, 9, 2, 19, 28, 21, 30, 31, 0, 9, 2, 19, 28, 21, 30, 31, 0, 9, 2, 19, 28, 21, 30, 31,
        0, 9, 2, 19, 28, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31,
        0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31,
        16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7,
        16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7,
        16, 1, 18, 19, 28, 29, 14, 7, 0, 1, 18, 19, 28, 29, 14, 7, 0, 1, 18, 19, 28, 29, 14, 7,
        0, 1, 18, 19, 28, 29, 14, 7, 0, 1, 18, 19, 28, 29, 14, 15, 0, 1, 18, 19, 28, 29, 14, 15,
        0, 1, 18, 19, 28, 29, 14, 15, 0, 1, 18, 19, 28, 29, 14, 15, 0, 1, 18, 19, 28, 29, 30, 15,
        0, 1, 18, 19, 28, 29, 30, 15, 0, 1, 18, 19, 28, 29, 30, 15, 0, 1, 18, 19, 28, 29, 30, 15,
        0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 18, 19, 28, 21, 30, 15,
        0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 15,
        0, 9, 2, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 31,
        0, 9, 2, 19, 28, 21, 30, 31, 0, 9, 2, 19, 28, 21, 30, 31, 0, 9, 2, 19, 28, 21, 30, 31,
        0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31,
        0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31,
        16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7,
        16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7,
        16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7, 0, 1, 18, 19, 28, 29, 14, 7,
        0, 1, 18, 19, 28, 29, 14, 7, 0, 1, 18, 19, 28, 29, 14, 15, 0, 1, 18, 19, 28, 29, 14, 15,
        0, 1, 18, 19, 28, 29, 14, 15, 0, 1, 18, 19, 28, 29, 14, 15, 0, 1, 18, 19, 28, 29, 30, 15,
        0, 1, 18, 19, 28, 29, 30, 15, 0, 1, 18, 19, 28, 29, 30, 15, 0, 1, 18, 19, 28, 29, 30, 15,
        0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 18, 19, 28, 21, 30, 15,
        0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 15,
        0, 9, 2, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 31,
        0, 9, 2, 19, 28, 21, 30, 31, 0, 9, 2, 19, 28, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31,
        0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31,
        0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31,
        16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7,
        16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7,
        16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7,
        0, 1, 18, 19, 28, 29, 14, 7, 0, 1, 18, 19, 28, 29, 14, 15, 0, 1, 18, 19, 28, 29, 14, 15,
        0, 1, 18, 19, 28, 29, 14, 15, 0, 1, 18, 19, 28, 29, 14, 15, 0, 1, 18, 19, 28, 29, 30, 15,
        0, 1, 18, 19, 28, 29, 30, 15, 0, 1, 18, 19, 28, 29, 30, 15, 0, 1, 18, 19, 28, 29, 30, 15,
        0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 18, 19, 28, 21, 30, 15,
        0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 15,
        0, 9, 2, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 31,
        0, 9, 2, 19, 28, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31,
        0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31,
        0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31,
        16, 1, 18, 27, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7,
        16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7,
        16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7,
        16, 1, 18, 19, 28, 29, 14, 7, 0, 1, 18, 19, 28, 29, 14, 15, 0, 1, 18, 19, 28, 29, 14, 15,
        0, 1, 18, 19, 28, 29, 14, 15, 0, 1, 18, 19, 28, 29, 14, 15, 0, 1, 18, 19, 28, 29, 30, 15,
        0, 1, 18, 19, 28, 29, 30, 15, 0, 1, 18, 19, 28, 29, 30, 15, 0, 1, 18, 19, 28, 29, 30, 15,
        0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 18, 19, 28, 21, 30, 15,
        0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 15,
        0, 9, 2, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 31,
        0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31,
        0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31,
        0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 3, 20, 21, 30, 31,
        16, 1, 18, 27, 28, 29, 14, 7, 16, 1, 18, 27, 28, 29, 14, 7, 16, 1, 18, 27, 28, 29, 14, 7,
        16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7,
        16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7,
        16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 15, 0, 1, 18, 19, 28, 29, 14, 15,
        0, 1, 18, 19, 28, 29, 14, 15, 0, 1, 18, 19, 28, 29, 14, 15, 0, 1, 18, 19, 28, 29, 30, 15,
        0, 1, 18, 19, 28, 29, 30, 15, 0, 1, 18, 19, 28, 29, 30, 15, 0, 1, 18, 19, 28, 29, 30, 15,
        0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 18, 19, 28, 21, 30, 15,
        0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 15,
        0, 9, 2, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 15, 0, 9, 2, 19, 20, 21, 30, 31,
        0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31,
        0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31,
        0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 3, 20, 21, 30, 31, 0, 9, 2, 3, 20, 21, 30, 31,
        16, 1, 18, 27, 28, 29, 14, 7, 16, 1, 18, 27, 28, 29, 14, 7, 16, 1, 18, 27, 28, 29, 14, 7,
        16, 1, 18, 27, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7,
        16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7,
        16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 15, 16, 1, 18, 19, 28, 29, 14, 15,
        0, 1, 18, 19, 28, 29, 14, 15, 0, 1, 18, 19, 28, 29, 14, 15, 0, 1, 18, 19, 28, 29, 30, 15,
        0, 1, 18, 19, 28, 29, 30, 15, 0, 1, 18, 19, 28, 29, 30, 15, 0, 1, 18, 19, 28, 29, 30, 15,
        0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 18, 19, 28, 21, 30, 15,
        0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 15,
        0, 9, 2, 19, 28, 21, 30, 15, 0, 9, 2, 19, 20, 21, 30, 15, 0, 9, 2, 19, 20, 21, 30, 31,
        0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31,
        0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31,
        0, 9, 2, 3, 20, 21, 30, 31, 0, 9, 2, 3, 20, 21, 30, 31, 0, 9, 2, 3, 20, 21, 30, 31,
        16, 1, 18, 27, 28, 29, 14, 7, 16, 1, 18, 27, 28, 29, 14, 7, 16, 1, 18, 27, 28, 29, 14, 7,
        16, 1, 18, 27, 28, 29, 14, 7, 16, 1, 18, 27, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7,
        16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7,
        16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 15, 16, 1, 18, 19, 28, 29, 14, 15,
        16, 1, 18, 19, 28, 29, 14, 15, 0, 1, 18, 19, 28, 29, 14, 15, 0, 1, 18, 19, 28, 29, 30, 15,
        0, 1, 18, 19, 28, 29, 30, 15, 0, 1, 18, 19, 28, 29, 30, 15, 0, 1, 18, 19, 28, 29, 30, 15,
        0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 18, 19, 28, 21, 30, 15,
        0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 15,
        0, 9, 2, 19, 20, 21, 30, 15, 0, 9, 2, 19, 20, 21, 30, 15, 0, 9, 2, 19, 20, 21, 30, 31,
        0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31,
        0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 3, 20, 21, 30, 31,
        0, 9, 2, 3, 20, 21, 30, 31, 0, 9, 2, 3, 20, 21, 30, 31, 0, 9, 2, 3, 20, 21, 30, 31,
        16, 1, 18, 27, 28, 29, 14, 7, 16, 1, 18, 27, 28, 29, 14, 7, 16, 1, 18, 27, 28, 29, 14, 7,
        16, 1, 18, 27, 28, 29, 14, 7, 16, 1, 18, 27, 28, 29, 14, 7, 16, 1, 18, 27, 28, 29, 14, 7,
        16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 7,
        16, 1, 18, 19, 28, 29, 14, 7, 16, 1, 18, 19, 28, 29, 14, 15, 16, 1, 18, 19, 28, 29, 14, 15,
        16, 1, 18, 19, 28, 29, 14, 15, 16, 1, 18, 19, 28, 29, 14, 15, 0, 1, 18, 19, 28, 29, 30, 15,
        0, 1, 18, 19, 28, 29, 30, 15, 0, 1, 18, 19, 28, 29, 30, 15, 0, 1, 18, 19, 28, 29, 30, 15,
        0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 18, 19, 28, 21, 30, 15,
        0, 9, 18, 19, 28, 21, 30, 15, 0, 9, 2, 19, 28, 21, 30, 15, 0, 9, 2, 19, 20, 21, 30, 15,
        0, 9, 2, 19, 20, 21, 30, 15, 0, 9, 2, 19, 20, 21, 30, 15, 0, 9, 2, 19, 20, 21, 30, 31,
        0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 19, 20, 21, 30, 31,
        0, 9, 2, 19, 20, 21, 30, 31, 0, 9, 2, 3, 20, 21, 30, 31, 0, 9, 2, 3, 20, 21, 30, 31,
        0, 9, 2, 3, 20, 21, 30, 31, 0, 9, 2, 3, 20, 21, 30, 31, 0, 9, 2, 3, 20, 21, 30, 31,
        16, 1, 18, 27, 12, 29, 14, 7, 16, 1, 18, 27, 12, 29, 14, 7, 16, 1, 18, 27, 12, 29, 14, 7,
        16, 1, 18, 27, 12, 29, 14, 7, 16, 1, 18, 27, 12, 29, 14, 7, 16, 1, 18, 27, 12, 29, 14, 7,
        16, 1, 18, 27, 12, 29, 14, 7, 16, 1, 18, 19, 12, 29, 14, 7, 16, 1, 18, 19, 12, 29, 14, 7,
        16, 1, 18, 19, 12, 29, 14, 7, 16, 1, 18, 19, 12, 29, 14, 15, 16, 1, 18, 19, 12, 29, 14, 15,
        16, 1, 18, 19, 12, 29, 14, 15, 16, 1, 18, 19, 12, 29, 14, 15, 8, 1, 18, 19, 12, 29, 30, 15,
        8, 1, 18, 19, 12, 29, 30, 15, 8, 1, 18, 19, 12, 29, 30, 15, 8, 1, 18, 19, 12, 29, 30, 15,
        8, 9, 18, 19, 12, 21, 30, 15, 8, 9, 18, 19, 12, 21, 30, 15, 8, 9, 18, 19, 12, 21, 30, 15,
        8, 9, 18, 19, 12, 21, 30, 15, 8, 9, 2, 19, 20, 21, 30, 15, 8, 9, 2, 19, 20, 21, 30, 15,
        8, 9, 2, 19, 20, 21, 30, 15, 8, 9, 2, 19, 20, 21, 30, 15, 8, 9, 2, 19, 20, 21, 30, 31,
        8, 9, 2, 19, 20, 21, 30, 31, 8, 9, 2, 19, 20, 21, 30, 31, 8, 9, 2, 19, 20, 21, 30, 31,
        8, 9, 2, 3, 20, 21, 30, 31, 8, 9, 2, 3, 20, 21, 30, 31, 8, 9, 2, 3, 20, 21, 30, 31,
        8, 9, 2, 3, 20, 21, 30, 31, 8, 9, 2, 3, 20, 21, 30, 31, 8, 9, 2, 3, 20, 21, 30, 31,
        16, 1, 18, 27, 12, 29, 14, 7, 16, 1, 18, 27, 12, 29, 14, 7, 16, 1, 18, 27, 12, 29, 14, 7,
        16, 1, 18, 27, 12, 29, 14, 7, 16, 1, 18, 27, 12, 29, 14, 7, 16, 1, 18, 27, 12, 29, 14, 7,
        16, 1, 18, 27, 12, 29, 14, 7, 16, 1, 18, 27, 12, 29, 14, 7, 16, 1, 18, 19, 12, 29, 14, 7,
        16, 1, 18, 19, 12, 29, 14, 7, 16, 1, 18, 19, 12, 29, 14, 15, 16, 1, 18, 19, 12, 29, 14, 15,
        16, 1, 18, 19, 12, 29, 14, 15, 16, 1, 18, 19, 12, 29, 14, 15, 8, 1, 18, 19, 12, 29, 30, 15,
        8, 1, 18, 19, 12, 29, 30, 15, 8, 1, 18, 19, 12, 29, 30, 15, 8, 1, 18, 19, 12, 29, 30, 15,
        8, 9, 18, 19, 12, 21, 30, 15, 8, 9, 18, 19, 12, 21, 30, 15, 8, 9, 18, 19, 12, 21, 30, 15,
        8, 9, 18, 19, 12, 21, 30, 15, 8, 9, 2, 19, 20, 21, 30, 15, 8, 9, 2, 19, 20, 21, 30, 15,
        8, 9, 2, 19, 20, 21, 30, 15, 8, 9, 2, 19, 20, 21, 30, 15, 8, 9, 2, 19, 20, 21, 30, 31,
        8, 9, 2, 19, 20, 21, 30, 31, 8, 9, 2, 19, 20, 21, 30, 31, 8, 9, 2, 3, 20, 21, 30, 31,
        8, 9, 2, 3, 20, 21, 30, 31, 8, 9, 2, 3, 20, 21, 30, 31, 8, 9, 2, 3, 20, 21, 30, 31,
        8, 9, 2, 3, 20, 21, 30, 31, 8, 9, 2, 3, 20, 21, 30, 31, 8, 9, 2, 3, 20, 21, 30, 31,
        16, 1, 18, 27, 12, 29, 14, 7, 16, 1, 18, 27, 12, 29, 14, 7, 16, 1, 18, 27, 12, 29, 14, 7,
        16, 1, 18, 27, 12, 29, 14, 7, 16, 1, 18, 27, 12, 29, 14, 7, 16, 1, 18, 27, 12, 29, 14, 7,
        16, 1, 18, 27, 12, 29, 14, 7, 16, 1, 18, 27, 12, 29, 14, 7, 16, 1, 18, 27, 12, 29, 14, 7,
        16, 1, 18, 19, 12, 29, 14, 7, 16, 1, 18, 19, 12, 29, 14, 15, 16, 1, 18, 19, 12, 29, 14, 15,
        16, 1, 18, 19, 12, 29, 14, 15, 16, 1, 18, 19, 12, 29, 14, 15, 8, 1, 18, 19, 12, 29, 30, 15,
        8, 1, 18, 19, 12, 29, 30, 15, 8, 1, 18, 19, 12, 29, 30, 15, 8, 1, 18, 19, 12, 29, 30, 15,
        8, 9, 18, 19, 12, 21, 30, 15, 8, 9, 18, 19, 12, 21, 30, 15, 8, 9, 18, 19, 12, 21, 30, 15,
        8, 9, 18, 19, 12, 21, 30, 15, 8, 9, 2, 19, 20, 21, 30, 15, 8, 9, 2, 19, 20, 21, 30, 15,
        8, 9, 2, 19, 20, 21, 30, 15, 8, 9, 2, 19, 20, 21, 30, 15, 8, 9, 2, 19, 20, 21, 30, 31,
        8, 9, 2, 19, 20, 21, 30, 31, 8, 9, 2, 3, 20, 21, 30, 31, 8, 9, 2, 3, 20, 21, 30, 31,
        8, 9, 2, 3, 20, 21, 30, 31, 8, 9, 2, 3, 20, 21, 30, 31, 8, 9, 2, 3, 20, 21, 30, 31,
        8, 9, 2, 3, 20, 21, 30, 31, 8, 9, 2, 3, 20, 21, 30, 31, 8, 9, 2, 3, 20, 21, 30, 31,
        16, 1, 18, 27, 12, 29, 14, 7, 16, 1, 18, 27, 12, 29, 14, 7, 16, 1, 18, 27, 12, 29, 14, 7,
        16, 1, 18, 27, 12, 29, 14, 7, 16, 1, 18, 27, 12, 29, 14, 7, 16, 1, 18, 27, 12, 29, 14, 7,
        16, 1, 18, 27, 12, 29, 14, 7, 16, 1, 18, 27, 12, 29, 14, 7, 16, 1, 18, 27, 12, 29, 14, 7,
        16, 1, 18, 27, 12, 29, 14, 7, 16, 1, 18, 19, 12, 29, 14, 15, 16, 1, 18, 19, 12, 29, 14, 15,
        16, 1, 18, 19, 12, 29, 14, 15, 16, 1, 18, 19, 12, 29, 14, 15, 8, 1, 18, 19, 12, 29, 30, 15,
        8, 1, 18, 19, 12, 29, 30, 15, 8, 1, 18, 19, 12, 29, 30, 15, 8, 1, 18, 19, 12, 29, 30, 15,
        8, 9, 18, 19, 12, 21, 30, 15, 8, 9, 18, 19, 12, 21, 30, 15, 8, 9, 18, 19, 12, 21, 30, 15,
        8, 9, 18, 19, 12, 21, 30, 15, 8, 9, 2, 19, 20, 21, 30, 15, 8, 9, 2, 19, 20, 21, 30, 15,
        8, 9, 2, 19, 20, 21, 30, 15, 8, 9, 2, 19, 20, 21, 30, 15, 8, 9, 2, 19, 20, 21, 30, 31,
        8, 9, 2, 3, 20, 21, 30, 31, 8, 9, 2, 3, 20, 21, 30, 31, 8, 9, 2, 3, 20, 21, 30, 31,
        8, 9, 2, 3, 20, 21, 30, 31, 8, 9, 2, 3, 20, 21, 30, 31, 8, 9, 2, 3, 20, 21, 30, 31,
        8, 9, 2, 3, 20, 21, 30, 31, 8, 9, 2, 3, 20, 21, 30, 31, 8, 9, 2, 3, 20, 21, 30, 31,
        16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 27, 12, 29, 14, 7,
        16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 27, 12, 29, 14, 7,
        16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 27, 12, 29, 14, 7,
        16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 11, 12, 29, 14, 15, 16, 17, 18, 11, 12, 29, 14, 15,
        16, 17, 18, 11, 12, 29, 14, 15, 16, 17, 18, 11, 12, 29, 14, 15, 8, 17, 18, 11, 12, 29, 30, 15,
        8, 17, 18, 11, 12, 29, 30, 15, 8, 17, 18, 11, 12, 29, 30, 15, 8, 17, 18, 11, 12, 29, 30, 15,
        8, 25, 18, 11, 12, 21, 30, 15, 8, 25, 18, 11, 12, 21, 30, 15, 8, 25, 18, 11, 12, 21, 30, 15,
        8, 25, 18, 11, 12, 21, 30, 15, 8, 25, 2, 11, 20, 21, 30, 15, 8, 25, 2, 11, 20, 21, 30, 15,
        8, 25, 2, 11, 20, 21, 30, 15, 8, 25, 2, 11, 20, 21, 30, 15, 8, 25, 2, 3, 20, 21, 30, 31,
        8, 25, 2, 3, 20, 21, 30, 31, 8, 25, 2, 3, 20, 21, 30, 31, 8, 25, 2, 3, 20, 21, 30, 31,
        8, 25, 2, 3, 20, 21, 30, 31, 8, 25, 2, 3, 20, 21, 30, 31, 8, 25, 2, 3, 20, 21, 30, 31,
        8, 25, 2, 3, 20, 21, 30, 31, 8, 25, 2, 3, 20, 21, 30, 31, 8, 25, 2, 3, 20, 21, 30, 31,
        16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 27, 12, 29, 14, 7,
        16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 27, 12, 29, 14, 7,
        16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 27, 12, 29, 14, 7,
        16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 11, 12, 29, 14, 15, 16, 17, 18, 11, 12, 29, 14, 15,
        16, 17, 18, 11, 12, 29, 14, 15, 16, 17, 18, 11, 12, 29, 14, 15, 8, 17, 18, 11, 12, 29, 30, 15,
        8, 17, 18, 11, 12, 29, 30, 15, 8, 17, 18, 11, 12, 29, 30, 15, 8, 17, 18, 11, 12, 29, 30, 15,
        8, 25, 18, 11, 12, 21, 30, 15, 8, 25, 18, 11, 12, 21, 30, 15, 8, 25, 18, 11, 12, 21, 30, 15,
        8, 25, 18, 11, 12, 21, 30, 15, 8, 25, 2, 11, 20, 21, 30, 15, 8, 25, 2, 11, 20, 21, 30, 15,
        8, 25, 2, 11, 20, 21, 30, 15, 8, 25, 2, 11, 20, 21, 30, 15, 8, 25, 2, 3, 20, 21, 30, 31,
        8, 25, 2, 3, 20, 21, 30, 31, 8, 25, 2, 3, 20, 21, 30, 31, 8, 25, 2, 3, 20, 21, 30, 31,
        8, 25, 2, 3, 20, 21, 30, 31, 8, 25, 2, 3, 20, 21, 30, 31, 8, 25, 2, 3, 20, 21, 30, 31,
        8, 25, 2, 3, 20, 21, 30, 31, 8, 25, 2, 3, 20, 21, 30, 31, 8, 25, 2, 3, 20, 21, 30, 31,
        16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 27, 12, 29, 14, 7,
        16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 27, 12, 29, 14, 7,
        16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 27, 12, 29, 14, 7,
        16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 11, 12, 29, 14, 15, 16, 17, 18, 11, 12, 29, 14, 15,
        16, 17, 18, 11, 12, 29, 14, 15, 16, 17, 18, 11, 12, 29, 14, 15, 8, 17, 18, 11, 12, 29, 30, 15,
        8, 17, 18, 11, 12, 29, 30, 15, 8, 17, 18, 11, 12, 29, 30, 15, 8, 17, 18, 11, 12, 29, 30, 15,
        8, 25, 18, 11, 12, 21, 30, 15, 8, 25, 18, 11, 12, 21, 30, 15, 8, 25, 18, 11, 12, 21, 30, 15,
        8, 25, 18, 11, 12, 21, 30, 15, 8, 25, 2, 11, 20, 21, 30, 15, 8, 25, 2, 11, 20, 21, 30, 15,
        8, 25, 2, 11, 20, 21, 30, 15, 8, 25, 2, 11, 20, 21, 30, 15, 8, 25, 2, 3, 20, 21, 30, 31,
        8, 25, 2, 3, 20, 21, 30, 31, 8, 25, 2, 3, 20, 21, 30, 31, 8, 25, 2, 3, 20, 21, 30, 31,
        8, 25, 2, 3, 20, 21, 30, 31, 8, 25, 2, 3, 20, 21, 30, 31, 8, 25, 2, 3, 20, 21, 30, 31,
        8, 25, 2, 3, 20, 21, 30, 31, 8, 25, 2, 3, 20, 21, 30, 31, 8, 25, 2, 3, 20, 21, 30, 31,
        16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 27, 12, 29, 14, 7,
        16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 27, 12, 29, 14, 7,
        16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 27, 12, 29, 14, 7,
        16, 17, 18, 27, 12, 29, 14, 7, 16, 17, 18, 11, 12, 29, 14, 15, 16, 17, 18, 11, 12, 29, 14, 15,
        16, 17, 18, 11, 12, 29, 14, 15, 16, 17, 18, 11, 12, 29, 14, 15, 8, 17, 18, 11, 12, 29, 30, 15,
        8, 17, 18, 11, 12, 29, 30, 15, 8, 17, 18, 11, 12, 29, 30, 15, 8, 17, 18, 11, 12, 29, 30, 15,
        8, 25, 18, 11, 12, 21, 30, 15, 8, 25, 18, 11, 12, 21, 30, 15, 8, 25, 18, 11, 12, 21, 30, 15,
        8, 25, 18, 11, 12, 21, 30, 15, 8, 25, 2, 11, 20, 21, 30, 15, 8, 25, 2, 11, 20, 21, 30, 15,
        8, 25, 2, 11, 20, 21, 30, 15, 8, 25, 2, 11, 20, 21, 30, 15, 8, 25, 2, 3, 20, 21, 30, 31,
        8, 25, 2, 3, 20, 21, 30, 31, 8, 25, 2, 3, 20, 21, 30, 31, 8, 25, 2, 3, 20, 21, 30, 31,
        8, 25, 2, 3, 20, 21, 30, 31, 8, 25, 2, 3, 20, 21, 30, 31, 8, 25, 2, 3, 20, 21, 30, 31,
        8, 25, 2, 3, 20, 21, 30, 31, 8, 25, 2, 3, 20, 21, 30, 31, 8, 25, 2, 3, 20, 21, 30, 31,
        16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 27, 12, 29, 6, 7,
        16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 27, 12, 29, 6, 7,
        16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 27, 12, 29, 6, 7,
        16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 11, 12, 29, 6, 15, 16, 17, 26, 11, 12, 29, 6, 15,
        16, 17, 26, 11, 12, 29, 6, 15, 16, 17, 26, 11, 12, 29, 6, 15, 8, 17, 26, 11, 12, 29, 22, 15,
        8, 17, 26, 11, 12, 29, 22, 15, 8, 17, 26, 11, 12, 29, 22, 15, 8, 17, 26, 11, 12, 29, 22, 15,
        8, 25, 26, 11, 12, 21, 22, 15, 8, 25, 26, 11, 12, 21, 22, 15, 8, 25, 26, 11, 12, 21, 22, 15,
        8, 25, 26, 11, 12, 21, 22, 15, 8, 25, 10, 11, 20, 21, 22, 15, 8, 25, 10, 11, 20, 21, 22, 15,
        8, 25, 10, 11, 20, 21, 22, 15, 8, 25, 10, 11, 20, 21, 22, 15, 8, 25, 10, 3, 20, 21, 22, 31,
        8, 25, 10, 3, 20, 21, 22, 31, 8, 25, 10, 3, 20, 21, 22, 31, 8, 25, 10, 3, 20, 21, 22, 31,
        8, 25, 10, 3, 20, 21, 22, 31, 8, 25, 10, 3, 20, 21, 22, 31, 8, 25, 10, 3, 20, 21, 22, 31,
        8, 25, 10, 3, 20, 21, 22, 31, 8, 25, 10, 3, 20, 21, 22, 31, 8, 25, 10, 3, 20, 21, 22, 31,
        16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 27, 12, 29, 6, 7,
        16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 27, 12, 29, 6, 7,
        16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 27, 12, 29, 6, 7,
        16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 11, 12, 29, 6, 15, 16, 17, 26, 11, 12, 29, 6, 15,
        16, 17, 26, 11, 12, 29, 6, 15, 16, 17, 26, 11, 12, 29, 6, 15, 8, 17, 26, 11, 12, 29, 22, 15,
        8, 17, 26, 11, 12, 29, 22, 15, 8, 17, 26, 11, 12, 29, 22, 15, 8, 17, 26, 11, 12, 29, 22, 15,
        8, 25, 26, 11, 12, 21, 22, 15, 8, 25, 26, 11, 12, 21, 22, 15, 8, 25, 26, 11, 12, 21, 22, 15,
        8, 25, 26, 11, 12, 21, 22, 15, 8, 25, 10, 11, 20, 21, 22, 15, 8, 25, 10, 11, 20, 21, 22, 15,
        8, 25, 10, 11, 20, 21, 22, 15, 8, 25, 10, 11, 20, 21, 22, 15, 8, 25, 10, 3, 20, 21, 22, 31,
        8, 25, 10, 3, 20, 21, 22, 31, 8, 25, 10, 3, 20, 21, 22, 31, 8, 25, 10, 3, 20, 21, 22, 31,
        8, 25, 10, 3, 20, 21, 22, 31, 8, 25, 10, 3, 20, 21, 22, 31, 8, 25, 10, 3, 20, 21, 22, 31,
        8, 25, 10, 3, 20, 21, 22, 31, 8, 25, 10, 3, 20, 21, 22, 31, 8, 25, 10, 3, 20, 21, 22, 31,
        16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 27, 12, 29, 6, 7,
        16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 27, 12, 29, 6, 7,
        16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 27, 12, 29, 6, 7,
        16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 11, 12, 29, 6, 15, 16, 17, 26, 11, 12, 29, 6, 15,
        16, 17, 26, 11, 12, 29, 6, 15, 16, 17, 26, 11, 12, 29, 6, 15, 8, 17, 26, 11, 12, 29, 22, 15,
        8, 17, 26, 11, 12, 29, 22, 15, 8, 17, 26, 11, 12, 29, 22, 15, 8, 17, 26, 11, 12, 29, 22, 15,
        8, 25, 26, 11, 12, 21, 22, 15, 8, 25, 26, 11, 12, 21, 22, 15, 8, 25, 26, 11, 12, 21, 22, 15,
        8, 25, 26, 11, 12, 21, 22, 15, 8, 25, 10, 11, 20, 21, 22, 15, 8, 25, 10, 11, 20, 21, 22, 15,
        8, 25, 10, 11, 20, 21, 22, 15, 8, 25, 10, 11, 20, 21, 22, 15, 8, 25, 10, 3, 20, 21, 22, 31,
        8, 25, 10, 3, 20, 21, 22, 31, 8, 25, 10, 3, 20, 21, 22, 31, 8, 25, 10, 3, 20, 21, 22, 31,
        8, 25, 10, 3, 20, 21, 22, 31, 8, 25, 10, 3, 20, 21, 22, 31, 8, 25, 10, 3, 20, 21, 22, 31,
        8, 25, 10, 3, 20, 21, 22, 31, 8, 25, 10, 3, 20, 21, 22, 31, 8, 25, 10, 3, 20, 21, 22, 31,
        16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 27, 12, 29, 6, 7,
        16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 27, 12, 29, 6, 7,
        16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 27, 12, 29, 6, 7,
        16, 17, 26, 27, 12, 29, 6, 7, 16, 17, 26, 11, 12, 29, 6, 15, 16, 17, 26, 11, 12, 29, 6, 15,
        16, 17, 26, 11, 12, 29, 6, 15, 16, 17, 26, 11, 12, 29, 6, 15, 8, 17, 26, 11, 12, 29, 22, 15,
        8, 17, 26, 11, 12, 29, 22, 15, 8, 17, 26, 11, 12, 29, 22, 15, 8, 17, 26, 11, 12, 29, 22, 15,
        8, 25, 26, 11, 12, 21, 22, 15, 8, 25, 26, 11, 12, 21, 22, 15, 8, 25, 26, 11, 12, 21, 22, 15,
        8, 25, 26, 11, 12, 21, 22, 15, 8, 25, 10, 11, 20, 21, 22, 15, 8, 25, 10, 11, 20, 21, 22, 15,
        8, 25, 10, 11, 20, 21, 22, 15, 8, 25, 10, 11, 20, 21, 22, 15, 8, 25, 10, 3, 20, 21, 22, 31,
        8, 25, 10, 3, 20, 21, 22, 31, 8, 25, 10, 3, 20, 21, 22, 31, 8, 25, 10, 3, 20, 21, 22, 31,
        8, 25, 10, 3, 20, 21, 22, 31, 8, 25, 10, 3, 20, 21, 22, 31, 8, 25, 10, 3, 20, 21, 22, 31,
        8, 25, 10, 3, 20, 21, 22, 31, 8, 25, 10, 3, 20, 21, 22, 31, 8, 25, 10, 3, 20, 21, 22, 31,
        16, 17, 26, 27, 12, 13, 6, 7, 16, 17, 26, 27, 12, 13, 6, 7, 16, 17, 26, 27, 12, 13, 6, 7,
        16, 17, 26, 27, 12, 13, 6, 7, 16, 17, 26, 27, 12, 13, 6, 7, 16, 17, 26, 27, 12, 13, 6, 7,
        16, 17, 26, 27, 12, 13, 6, 7, 16, 17, 26, 27, 12, 13, 6, 7, 16, 17, 26, 27, 12, 13, 6, 7,
        16, 17, 26, 27, 12, 13, 6, 7, 16, 17, 26, 11, 12, 13, 6, 23, 16, 17, 26, 11, 12, 13, 6, 23,
        16, 17, 26, 11, 12, 13, 6, 23, 16, 17, 26, 11, 12, 13, 6, 23, 8, 17, 26, 11, 12, 13, 22, 23,
        8, 17, 26, 11, 12, 13, 22, 23, 8, 17, 26, 11, 12, 13, 22, 23, 8, 17, 26, 11, 12, 13, 22, 23,
        8, 25, 26, 11, 12, 5, 22, 23, 8, 25, 26, 11, 12, 5, 22, 23, 8, 25, 26, 11, 12, 5, 22, 23,
        8, 25, 26, 11, 12, 5, 22, 23, 8, 25, 10, 11, 20, 5, 22, 23, 8, 25, 10, 11, 20, 5, 22, 23,
        8, 25, 10, 11, 20, 5, 22, 23, 8, 25, 10, 11, 20, 5, 22, 23, 8, 25, 10, 3, 20, 5, 22, 31,
        8, 25, 10, 3, 20, 5, 22, 31, 8, 25, 10, 3, 20, 5, 22, 31, 8, 25, 10, 3, 20, 5, 22, 31,
        8, 25, 10, 3, 20, 5, 22, 31, 8, 25, 10, 3, 20, 5, 22, 31, 8, 25, 10, 3, 20, 5, 22, 31,
        8, 25, 10, 3, 20, 5, 22, 31, 8, 25, 10, 3, 20, 5, 22, 31, 8, 25, 10, 3, 20, 5, 22, 31,
        16, 17, 26, 27, 12, 13, 6, 7, 16, 17, 26, 27, 12, 13, 6, 7, 16, 17, 26, 27, 12, 13, 6, 7,
        16, 17, 26, 27, 12, 13, 6, 7, 16, 17, 26, 27, 12, 13, 6, 7, 16, 17, 26, 27, 12, 13, 6, 7,
        16, 17, 26, 27, 12, 13, 6, 7, 16, 17, 26, 27, 12, 13, 6, 7, 16, 17, 26, 27, 12, 13, 6, 7,
        16, 17, 26, 27, 12, 13, 6, 23, 16, 17, 26, 11, 12, 13, 6, 23, 16, 17, 26, 11, 12, 13, 6, 23,
        16, 17, 26, 11, 12, 13, 6, 23, 16, 17, 26, 11, 12, 13, 6, 23, 8, 17, 26, 11, 12, 13, 22, 23,
        8, 17, 26, 11, 12, 13, 22, 23, 8, 17, 26, 11, 12, 13, 22, 23, 8, 17, 26, 11, 12, 13, 22, 23,
        8, 25, 26, 11, 12, 5, 22, 23, 8, 25, 26, 11, 12, 5, 22, 23, 8, 25, 26, 11, 12, 5, 22, 23,
        8, 25, 26, 11, 12, 5, 22, 23, 8, 25, 10, 11, 20, 5, 22, 23, 8, 25, 10, 11, 20, 5, 22, 23,
        8, 25, 10, 11, 20, 5, 22, 23, 8, 25, 10, 11, 20, 5, 22, 23, 8, 25, 10, 3, 20, 5, 22, 23,
        8, 25, 10, 3, 20, 5, 22, 31, 8, 25, 10, 3, 20, 5, 22, 31, 8, 25, 10, 3, 20, 5, 22, 31,
        8, 25, 10, 3, 20, 5, 22, 31, 8, 25, 10, 3, 20, 5, 22, 31, 8, 25, 10, 3, 20, 5, 22, 31,
        8, 25, 10, 3, 20, 5, 22, 31, 8, 25, 10, 3, 20, 5, 22, 31, 8, 25, 10, 3, 20, 5, 22, 31,
        16, 17, 26, 27, 12, 13, 6, 7, 16, 17, 26, 27, 12, 13, 6, 7, 16, 17, 26, 27, 12, 13, 6, 7,
        16, 17, 26, 27, 12, 13, 6, 7, 16, 17, 26, 27, 12, 13, 6, 7, 16, 17, 26, 27, 12, 13, 6, 7,
        16, 17, 26, 27, 12, 13, 6, 7, 16, 17, 26, 27, 12, 13, 6, 7, 16, 17, 26, 27, 12, 13, 6, 23,
        16, 17, 26, 27, 12, 13, 6, 23, 16, 17, 26, 11, 12, 13, 6, 23, 16, 17, 26, 11, 12, 13, 6, 23,
        16, 17, 26, 11, 12, 13, 6, 23, 16, 17, 26, 11, 12, 13, 6, 23, 8, 17, 26, 11, 12, 13, 22, 23,
        8, 17, 26, 11, 12, 13, 22, 23, 8, 17, 26, 11, 12, 13, 22, 23, 8, 17, 26, 11, 12, 13, 22, 23,
        8, 25, 26, 11, 12, 5, 22, 23, 8, 25, 26, 11, 12, 5, 22, 23, 8, 25, 26, 11, 12, 5, 22, 23,
        8, 25, 26, 11, 12, 5, 22, 23, 8, 25, 10, 11, 20, 5, 22, 23, 8, 25, 10, 11, 20, 5, 22, 23,
        8, 25, 10, 11, 20, 5, 22, 23, 8, 25, 10, 11, 20, 5, 22, 23, 8, 25, 10, 3, 20, 5, 22, 23,
        8, 25, 10, 3, 20, 5, 22, 23, 8, 25, 10, 3, 20, 5, 22, 31, 8, 25, 10, 3, 20, 5, 22, 31,
        8, 25, 10, 3, 20, 5, 22, 31, 8, 25, 10, 3, 20, 5, 22, 31, 8, 25, 10, 3, 20, 5, 22, 31,
        8, 25, 10, 3, 20, 5, 22, 31, 8, 25, 10, 3, 20, 5, 22, 31, 8, 25, 10, 3, 20, 5, 22, 31,
        16, 17, 26, 27, 12, 13, 6, 7, 16, 17, 26, 27, 12, 13, 6, 7, 16, 17, 26, 27, 12, 13, 6, 7,
        16, 17, 26, 27, 12, 13, 6, 7, 16, 17, 26, 27, 12, 13, 6, 7, 16, 17, 26, 27, 12, 13, 6, 7,
        16, 17, 26, 27, 12, 13, 6, 7, 16, 17, 26, 27, 12, 13, 6, 23, 16, 17, 26, 27, 12, 13, 6, 23,
        16, 17, 26, 27, 12, 13, 6, 23, 16, 17, 26, 11, 12, 13, 6, 23, 16, 17, 26, 11, 12, 13, 6, 23,
        16, 17, 26, 11, 12, 13, 6, 23, 16, 17, 26, 11, 12, 13, 6, 23, 8, 17, 26, 11, 12, 13, 22, 23,
        8, 17, 26, 11, 12, 13, 22, 23, 8, 17, 26, 11, 12, 13, 22, 23, 8, 17, 26, 11, 12, 13, 22, 23,
        8, 25, 26, 11, 12, 5, 22, 23, 8, 25, 26, 11, 12, 5, 22, 23, 8, 25, 26, 11, 12, 5, 22, 23,
        8, 25, 26, 11, 12, 5, 22, 23, 8, 25, 10, 11, 20, 5, 22, 23, 8, 25, 10, 11, 20, 5, 22, 23,
        8, 25, 10, 11, 20, 5, 22, 23, 8, 25, 10, 11, 20, 5, 22, 23, 8, 25, 10, 3, 20, 5, 22, 23,
        8, 25, 10, 3, 20, 5, 22, 23, 8, 25, 10, 3, 20, 5, 22, 23, 8, 25, 10, 3, 20, 5, 22, 31,
        8, 25, 10, 3, 20, 5, 22, 31, 8, 25, 10, 3, 20, 5, 22, 31, 8, 25, 10, 3, 20, 5, 22, 31,
        8, 25, 10, 3, 20, 5, 22, 31, 8, 25, 10, 3, 20, 5, 22, 31, 8, 25, 10, 3, 20, 5, 22, 31,
        16, 17, 26, 27, 4, 13, 6, 7, 16, 17, 26, 27, 4, 13, 6, 7, 16, 17, 26, 27, 4, 13, 6, 7,
        16, 17, 26, 27, 4, 13, 6, 7, 16, 17, 26, 27, 4, 13, 6, 7, 16, 17, 26, 27, 4, 13, 6, 7,
        16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23,
        16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 11, 4, 13, 6, 23, 16, 17, 26, 11, 4, 13, 6, 23,
        16, 17, 26, 11, 4, 13, 6, 23, 16, 17, 26, 11, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 22, 23,
        24, 17, 26, 11, 4, 13, 22, 23, 24, 17, 26, 11, 4, 13, 22, 23, 24, 17, 26, 11, 4, 13, 22, 23,
        24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 26, 11, 4, 5, 22, 23,
        24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 10, 11, 20, 5, 22, 23, 24, 25, 10, 11, 20, 5, 22, 23,
        24, 25, 10, 11, 20, 5, 22, 23, 24, 25, 10, 11, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23,
        24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23,
        24, 25, 10, 3, 20, 5, 22, 31, 24, 25, 10, 3, 20, 5, 22, 31, 24, 25, 10, 3, 20, 5, 22, 31,
        24, 25, 10, 3, 20, 5, 22, 31, 24, 25, 10, 3, 20, 5, 22, 31, 24, 25, 10, 3, 20, 5, 22, 31,
        16, 17, 26, 27, 4, 13, 6, 7, 16, 17, 26, 27, 4, 13, 6, 7, 16, 17, 26, 27, 4, 13, 6, 7,
        16, 17, 26, 27, 4, 13, 6, 7, 16, 17, 26, 27, 4, 13, 6, 7, 16, 17, 26, 27, 4, 13, 6, 23,
        16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23,
        16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 11, 4, 13, 6, 23, 16, 17, 26, 11, 4, 13, 6, 23,
        16, 17, 26, 11, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 22, 23,
        24, 17, 26, 11, 4, 13, 22, 23, 24, 17, 26, 11, 4, 13, 22, 23, 24, 17, 26, 11, 4, 13, 22, 23,
        24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 26, 11, 4, 5, 22, 23,
        24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 10, 11, 4, 5, 22, 23, 24, 25, 10, 11, 20, 5, 22, 23,
        24, 25, 10, 11, 20, 5, 22, 23, 24, 25, 10, 11, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23,
        24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23,
        24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 31, 24, 25, 10, 3, 20, 5, 22, 31,
        24, 25, 10, 3, 20, 5, 22, 31, 24, 25, 10, 3, 20, 5, 22, 31, 24, 25, 10, 3, 20, 5, 22, 31,
        16, 17, 26, 27, 4, 13, 6, 7, 16, 17, 26, 27, 4, 13, 6, 7, 16, 17, 26, 27, 4, 13, 6, 7,
        16, 17, 26, 27, 4, 13, 6, 7, 16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23,
        16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23,
        16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 11, 4, 13, 6, 23, 16, 17, 26, 11, 4, 13, 6, 23,
        24, 17, 26, 11, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 22, 23,
        24, 17, 26, 11, 4, 13, 22, 23, 24, 17, 26, 11, 4, 13, 22, 23, 24, 17, 26, 11, 4, 13, 22, 23,
        24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 26, 11, 4, 5, 22, 23,
        24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 10, 11, 4, 5, 22, 23, 24, 25, 10, 11, 4, 5, 22, 23,
        24, 25, 10, 11, 20, 5, 22, 23, 24, 25, 10, 11, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23,
        24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23,
        24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 31,
        24, 25, 10, 3, 20, 5, 22, 31, 24, 25, 10, 3, 20, 5, 22, 31, 24, 25, 10, 3, 20, 5, 22, 31,
        16, 17, 26, 27, 4, 13, 6, 7, 16, 17, 26, 27, 4, 13, 6, 7, 16, 17, 26, 27, 4, 13, 6, 7,
        16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23,
        16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23,
        16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 11, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 6, 23,
        24, 17, 26, 11, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 22, 23,
        24, 17, 26, 11, 4, 13, 22, 23, 24, 17, 26, 11, 4, 13, 22, 23, 24, 17, 26, 11, 4, 13, 22, 23,
        24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 26, 11, 4, 5, 22, 23,
        24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 10, 11, 4, 5, 22, 23, 24, 25, 10, 11, 4, 5, 22, 23,
        24, 25, 10, 11, 4, 5, 22, 23, 24, 25, 10, 11, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23,
        24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23,
        24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23,
        24, 25, 10, 3, 20, 5, 22, 31, 24, 25, 10, 3, 20, 5, 22, 31, 24, 25, 10, 3, 20, 5, 22, 31,
        16, 17, 26, 27, 4, 13, 6, 7, 16, 17, 26, 27, 4, 13, 6, 7, 16, 17, 26, 27, 4, 13, 6, 23,
        16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23,
        16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23,
        16, 17, 26, 27, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 6, 23,
        24, 17, 26, 11, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 22, 23,
        24, 17, 26, 11, 4, 13, 22, 23, 24, 17, 26, 11, 4, 13, 22, 23, 24, 17, 26, 11, 4, 13, 22, 23,
        24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 26, 11, 4, 5, 22, 23,
        24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 10, 11, 4, 5, 22, 23, 24, 25, 10, 11, 4, 5, 22, 23,
        24, 25, 10, 11, 4, 5, 22, 23, 24, 25, 10, 11, 4, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23,
        24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23,
        24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23,
        24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 31, 24, 25, 10, 3, 20, 5, 22, 31,
        16, 17, 26, 27, 4, 13, 6, 7, 16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23,
        16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23,
        16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23,
        24, 17, 26, 27, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 6, 23,
        24, 17, 26, 11, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 22, 23,
        24, 17, 26, 11, 4, 13, 22, 23, 24, 17, 26, 11, 4, 13, 22, 23, 24, 17, 26, 11, 4, 13, 22, 23,
        24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 26, 11, 4, 5, 22, 23,
        24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 10, 11, 4, 5, 22, 23, 24, 25, 10, 11, 4, 5, 22, 23,
        24, 25, 10, 11, 4, 5, 22, 23, 24, 25, 10, 11, 4, 5, 22, 23, 24, 25, 10, 3, 4, 5, 22, 23,
        24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23,
        24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23,
        24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 31,
        16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23,
        16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23,
        16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23, 24, 17, 26, 27, 4, 13, 6, 23,
        24, 17, 26, 27, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 6, 23,
        24, 17, 26, 11, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 22, 23,
        24, 17, 26, 11, 4, 13, 22, 23, 24, 17, 26, 11, 4, 13, 22, 23, 24, 17, 26, 11, 4, 13, 22, 23,
        24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 26, 11, 4, 5, 22, 23,
        24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 10, 11, 4, 5, 22, 23, 24, 25, 10, 11, 4, 5, 22, 23,
        24, 25, 10, 11, 4, 5, 22, 23, 24, 25, 10, 11, 4, 5, 22, 23, 24, 25, 10, 3, 4, 5, 22, 23,
        24, 25, 10, 3, 4, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23,
        24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23,
        24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23,
        16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23,
        16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23,
        16, 17, 26, 27, 4, 13, 6, 23, 24, 17, 26, 27, 4, 13, 6, 23, 24, 17, 26, 27, 4, 13, 6, 23,
        24, 17, 26, 27, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 6, 23,
        24, 17, 26, 11, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 22, 23,
        24, 17, 26, 11, 4, 13, 22, 23, 24, 17, 26, 11, 4, 13, 22, 23, 24, 17, 26, 11, 4, 13, 22, 23,
        24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 26, 11, 4, 5, 22, 23,
        24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 10, 11, 4, 5, 22, 23, 24, 25, 10, 11, 4, 5, 22, 23,
        24, 25, 10, 11, 4, 5, 22, 23, 24, 25, 10, 11, 4, 5, 22, 23, 24, 25, 10, 3, 4, 5, 22, 23,
        24, 25, 10, 3, 4, 5, 22, 23, 24, 25, 10, 3, 4, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23,
        24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23,
        24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23,
        16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23,
        16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23,
        24, 17, 26, 27, 4, 13, 6, 23, 24, 17, 26, 27, 4, 13, 6, 23, 24, 17, 26, 27, 4, 13, 6, 23,
        24, 17, 26, 27, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 6, 23,
        24, 17, 26, 11, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 22, 23,
        24, 17, 26, 11, 4, 13, 22, 23, 24, 17, 26, 11, 4, 13, 22, 23, 24, 17, 26, 11, 4, 13, 22, 23,
        24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 26, 11, 4, 5, 22, 23,
        24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 10, 11, 4, 5, 22, 23, 24, 25, 10, 11, 4, 5, 22, 23,
        24, 25, 10, 11, 4, 5, 22, 23, 24, 25, 10, 11, 4, 5, 22, 23, 24, 25, 10, 3, 4, 5, 22, 23,
        24, 25, 10, 3, 4, 5, 22, 23, 24, 25, 10, 3, 4, 5, 22, 23, 24, 25, 10, 3, 4, 5, 22, 23,
        24, 25, 10, 3, 4, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23,
        24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23,
        16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23,
        16, 17, 26, 27, 4, 13, 6, 23, 16, 17, 26, 27, 4, 13, 6, 23, 24, 17, 26, 27, 4, 13, 6, 23,
        24, 17, 26, 27, 4, 13, 6, 23, 24, 17, 26, 27, 4, 13, 6, 23, 24, 17, 26, 27, 4, 13, 6, 23,
        24, 17, 26, 27, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 6, 23,
        24, 17, 26, 11, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 6, 23, 24, 17, 26, 11, 4, 13, 22, 23,
        24, 17, 26, 11, 4, 13, 22, 23, 24, 17, 26, 11, 4, 13, 22, 23, 24, 17, 26, 11, 4, 13, 22, 23,
        24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 26, 11, 4, 5, 22, 23,
        24, 25, 26, 11, 4, 5, 22, 23, 24, 25, 10, 11, 4, 5, 22, 23, 24, 25, 10, 11, 4, 5, 22, 23,
        24, 25, 10, 11, 4, 5, 22, 23, 24, 25, 10, 11, 4, 5, 22, 23, 24, 25, 10, 3, 4, 5, 22, 23,
        24, 25, 10, 3, 4, 5, 22, 23, 24, 25, 10, 3, 4, 5, 22, 23, 24, 25, 10, 3, 4, 5, 22, 23,
        24, 25, 10, 3, 4, 5, 22, 23, 24, 25, 10, 3, 4, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23,
        24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23, 24, 25, 10, 3, 20, 5, 22, 23,
        8, 9, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        0, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 7,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 11, 12, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 11, 12, 13, 6, 15,
        8, 1, 10, 11, 12, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        0, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 3, 12, 13, 6, 15, 8, 1, 10, 11, 12, 13, 6, 15, 8, 1, 10, 11, 12, 13, 6, 15,
        8, 1, 10, 11, 12, 13, 6, 15, 8, 1, 10, 11, 12, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 7,
        8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 13, 6, 15, 8, 1, 10, 3, 12, 13, 6, 15,
        8, 1, 10, 11, 12, 13, 6, 15, 8, 1, 10, 11, 12, 13, 6, 15, 8, 1, 10, 11, 12, 13, 6, 15,
        8, 1, 10, 11, 12, 13, 6, 15, 8, 1, 10, 11, 12, 13, 6, 15, 8, 1, 10, 11, 12, 13, 6, 15,
        8, 1, 10, 11, 12, 13, 6, 15, 8, 1, 10, 11, 4, 13, 6, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        0, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 11, 12, 13, 6, 15,
        8, 1, 10, 11, 12, 13, 6, 15, 8, 1, 10, 11, 12, 13, 6, 15, 8, 1, 10, 11, 12, 13, 6, 15,
        8, 1, 10, 11, 12, 13, 6, 15, 8, 1, 10, 11, 12, 13, 6, 15, 8, 1, 10, 11, 12, 13, 6, 15,
        8, 1, 10, 11, 12, 13, 6, 15, 8, 1, 2, 11, 12, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 11, 12, 5, 6, 15, 8, 9, 10, 11, 12, 5, 6, 15,
        8, 9, 10, 11, 12, 13, 6, 15, 8, 1, 10, 11, 12, 13, 6, 15, 8, 1, 10, 11, 12, 13, 6, 15,
        8, 1, 10, 11, 12, 13, 6, 15, 8, 1, 10, 11, 12, 13, 6, 15, 8, 1, 10, 11, 12, 13, 6, 15,
        8, 1, 2, 11, 12, 13, 14, 15, 8, 1, 2, 11, 12, 13, 14, 15, 8, 1, 2, 11, 12, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 7,
        8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 11, 12, 5, 6, 15, 8, 9, 10, 11, 12, 5, 6, 15, 8, 9, 10, 11, 12, 5, 6, 15,
        8, 9, 10, 11, 12, 5, 6, 15, 8, 9, 10, 11, 12, 13, 6, 15, 8, 1, 10, 11, 12, 13, 6, 15,
        8, 1, 10, 11, 12, 13, 6, 15, 8, 1, 10, 11, 12, 13, 6, 15, 8, 1, 2, 11, 12, 13, 14, 15,
        8, 1, 2, 11, 12, 13, 14, 15, 8, 1, 2, 11, 12, 13, 14, 15, 8, 1, 2, 11, 12, 13, 14, 15,
        8, 1, 2, 11, 12, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 11, 12, 5, 6, 15,
        8, 9, 10, 11, 12, 5, 6, 15, 8, 9, 10, 11, 12, 5, 6, 15, 8, 9, 10, 11, 12, 5, 6, 15,
        8, 9, 10, 11, 12, 5, 6, 15, 8, 9, 10, 11, 12, 5, 6, 15, 8, 9, 10, 11, 12, 13, 6, 15,
        8, 1, 10, 11, 12, 13, 6, 15, 8, 1, 2, 11, 12, 13, 14, 15, 8, 1, 2, 11, 12, 13, 14, 15,
        8, 1, 2, 11, 12, 13, 14, 15, 8, 1, 2, 11, 12, 13, 14, 15, 8, 1, 2, 11, 12, 13, 14, 15,
        8, 1, 2, 11, 12, 13, 14, 15, 8, 1, 2, 11, 12, 13, 14, 15, 8, 1, 2, 11, 4, 13, 14, 15,
        8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        0, 9, 10, 3, 12, 5, 6, 15, 8, 9, 10, 11, 12, 5, 6, 15, 8, 9, 10, 11, 12, 5, 6, 15,
        8, 9, 10, 11, 12, 5, 6, 15, 8, 9, 10, 11, 12, 5, 6, 15, 8, 9, 10, 11, 12, 5, 6, 15,
        8, 9, 10, 11, 12, 5, 6, 15, 8, 9, 10, 11, 12, 5, 6, 15, 8, 9, 10, 11, 12, 5, 6, 15,
        8, 9, 2, 11, 12, 13, 14, 15, 8, 1, 2, 11, 12, 13, 14, 15, 8, 1, 2, 11, 12, 13, 14, 15,
        8, 1, 2, 11, 12, 13, 14, 15, 8, 1, 2, 11, 12, 13, 14, 15, 8, 1, 2, 11, 12, 13, 14, 15,
        8, 1, 2, 11, 12, 13, 14, 15, 8, 1, 2, 11, 12, 13, 14, 15, 8, 1, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        8, 9, 10, 11, 12, 5, 6, 15, 8, 9, 10, 11, 12, 5, 6, 15, 8, 9, 10, 11, 12, 5, 6, 15,
        8, 9, 10, 11, 12, 5, 6, 15, 8, 9, 10, 11, 12, 5, 6, 15, 8, 9, 2, 11, 12, 5, 14, 15,
        8, 9, 2, 11, 12, 5, 14, 15, 8, 9, 2, 11, 12, 13, 14, 15, 8, 1, 2, 11, 12, 13, 14, 15,
        8, 1, 2, 11, 12, 13, 14, 15, 8, 1, 2, 11, 12, 13, 14, 15, 8, 1, 2, 11, 12, 13, 14, 15,
        8, 1, 2, 11, 12, 13, 14, 15, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 8, 9, 10, 11, 12, 5, 6, 15, 8, 9, 10, 11, 12, 5, 6, 15,
        8, 9, 10, 11, 12, 5, 6, 15, 8, 9, 2, 11, 12, 5, 14, 15, 8, 9, 2, 11, 12, 5, 14, 15,
        8, 9, 2, 11, 12, 5, 14, 15, 8, 9, 2, 11, 12, 5, 14, 15, 8, 9, 2, 11, 12, 13, 14, 15,
        8, 1, 2, 11, 12, 13, 14, 15, 8, 1, 2, 11, 12, 13, 14, 15, 8, 1, 2, 11, 12, 13, 14, 15,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 8, 9, 10, 11, 12, 5, 6, 15, 8, 9, 10, 11, 12, 5, 6, 15,
        8, 9, 2, 11, 12, 5, 14, 15, 8, 9, 2, 11, 12, 5, 14, 15, 8, 9, 2, 11, 12, 5, 14, 15,
        8, 9, 2, 11, 12, 5, 14, 15, 8, 9, 2, 11, 12, 5, 14, 15, 8, 9, 2, 11, 12, 5, 14, 15,
        8, 9, 2, 11, 12, 13, 14, 15, 8, 1, 2, 11, 12, 13, 14, 15, 8, 1, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 8, 9, 2, 11, 12, 5, 14, 15,
        8, 9, 2, 11, 12, 5, 14, 15, 8, 9, 2, 11, 12, 5, 14, 15, 8, 9, 2, 11, 12, 5, 14, 15,
        8, 9, 2, 11, 12, 5, 14, 15, 8, 9, 2, 11, 12, 5, 14, 15, 8, 9, 2, 11, 12, 5, 14, 15,
        8, 9, 2, 11, 12, 5, 14, 15, 8, 9, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        8, 9, 2, 11, 12, 5, 14, 15, 8, 9, 2, 11, 12, 5, 14, 15, 8, 9, 2, 11, 12, 5, 14, 15,
        8, 9, 2, 11, 12, 5, 14, 15, 8, 9, 2, 11, 12, 5, 14, 15, 8, 9, 2, 11, 12, 5, 14, 15,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 8, 9, 2, 11, 12, 5, 14, 15, 8, 9, 2, 11, 12, 5, 14, 15,
        8, 9, 2, 11, 12, 5, 14, 15, 8, 9, 2, 11, 12, 5, 14, 15, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15, 8, 9, 2, 11, 12, 5, 14, 15,
        8, 9, 2, 11, 12, 5, 14, 15, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        0, 9, 10, 3, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 4, 13, 14, 7,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 13, 14, 7,
        8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 13, 14, 7, 8, 1, 2, 11, 12, 13, 14, 7,
        0, 9, 10, 11, 12, 5, 6, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15, 0, 9, 2, 11, 12, 5, 14, 15,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7, 0, 9, 2, 11, 12, 5, 14, 7,
        0, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7,
        8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 5, 14, 7, 8, 9, 2, 11, 12, 13, 14, 7
    };

    private static readonly byte[] ConstellationMap4800 =
    {
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        2, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        2, 2, 2, 2, 2, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        2, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 0, 0, 0, 0, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        2, 2, 2, 2, 2, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        2, 2, 2, 2, 2, 3, 3, 3, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 1, 1, 1, 1, 1, 1, 1,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 1, 1, 1,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3
    };

}

public static class V17RxApi {
    public static V17RxState? v17_rx_init(
        V17RxState? state,
        int bitRate,
        V17RxPutBitHandler? putBit,
        object? userData) =>
        V17Rx.Initialize(
            state,
            bitRate,
            putBit,
            userData);

    public static int v17_rx_restart(
        V17RxState state,
        int bitRate,
        int shortTrain) =>
        V17Rx.Restart(
            state,
            bitRate,
            shortTrain);

    public static int v17_rx_release(
        V17RxState state) =>
        V17Rx.Release(state);

    public static int v17_rx_free(
        V17RxState? state) =>
        V17Rx.Free(state);

    public static V17RxLogger v17_rx_get_logging_state(
        V17RxState state) =>
        V17Rx.GetLoggingState(state);

    public static void v17_rx_set_put_bit(
        V17RxState state,
        V17RxPutBitHandler? putBit,
        object? userData) =>
        V17Rx.SetPutBit(
            state,
            putBit,
            userData);

    public static void v17_rx_set_modem_status_handler(
        V17RxState state,
        V17RxModemStatusHandler? handler,
        object? userData) =>
        V17Rx.SetModemStatusHandler(
            state,
            handler,
            userData);

    public static int v17_rx(
        V17RxState state,
        ReadOnlySpan<short> samples) =>
        V17Rx.Receive(state, samples);

    public static int v17_rx(
        V17RxState state,
        short[] samples,
        int length) =>
        V17Rx.Receive(
            state,
            samples,
            length);

    public static int v17_rx_fillin(
        V17RxState state,
        int length) =>
        V17Rx.ReceiveFillIn(
            state,
            length);

    public static int v17_rx_equalizer_state(
        V17RxState state,
        out ReadOnlyMemory<V17RxComplex> coefficients) =>
        V17Rx.EqualizerState(
            state,
            out coefficients);

    public static float v17_rx_carrier_frequency(
        V17RxState state) =>
        V17Rx.CarrierFrequency(state);

    public static float v17_rx_symbol_timing_correction(
        V17RxState state) =>
        V17Rx.SymbolTimingCorrection(state);

    public static float v17_rx_signal_power(
        V17RxState state) =>
        V17Rx.SignalPower(state);

    public static void v17_rx_set_signal_cutoff(
        V17RxState state,
        float cutoff) =>
        V17Rx.SetSignalCutoff(
            state,
            cutoff);

    public static void v17_rx_set_qam_report_handler(
        V17RxState state,
        V17RxQamReportHandler? handler,
        object? userData) =>
        V17Rx.SetQamReportHandler(
            state,
            handler,
            userData);
}
