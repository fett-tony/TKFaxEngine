/*
 * TKFaxEngine - managed C# port
 *
 * V17Tx.cs - ITU V.17 modem transmit part
 *
 * Combined port of:
 *   v17tx.h
 *   private/v17tx.h (merged into the supplied v17tx.h)
 *   v17tx.c
 *   v17_v32bis_tx_constellation_maps.h
 *   v17_v32bis_tx_rrc.h
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2004, 2012 Steve Underwood.
 *
 * This file preserves the GNU Lesser General Public License version 2.1
 * licensing terms of the original source files.
 */

#nullable enable

using global::TKFaxEngine.Modem.V32;

namespace TKFaxEngine.Modem.V17;

/// <summary>
/// Complex floating-point sample used by the V.17 transmitter.
/// </summary>
public readonly record struct V17TxComplex(float Real, float Imaginary) {
    public float Re => Real;

    public float Im => Imaginary;
}

/// <summary>
/// Special callback values used by the modem modules.
/// </summary>
public enum V17TxSignalStatus {
    CarrierDown = -1,
    CarrierUp = -2,
    TrainingInProgress = -3,
    TrainingSucceeded = -4,
    TrainingFailed = -5,
    FramingOk = -6,
    EndOfData = -7,
    Abort = -8,
    Break = -9,
    ShutdownComplete = -10,
    OctetReport = -11,
    PoorSignalQuality = -12,
    ModemRetrainOccurred = -13,
    LinkConnected = -14,
    LinkDisconnected = -15,
    LinkError = -16,
    LinkIdle = -17
}

/// <summary>
/// Reports modem state changes.
/// </summary>
public delegate void V17TxModemStatusDelegate(
    object? userData,
    int status);

/// <summary>
/// Minimal logging context corresponding to logging_state_t.
/// </summary>
public sealed class V17TxLog {
    public string Protocol { get; set; } = "V.17 TX";

    public Action<string>? FlowSink { get; set; }

    public void Flow(string message) {
        FlowSink?.Invoke(message);
    }
}

/// <summary>
/// Managed equivalent of v17_tx_state_t.
/// </summary>
public sealed class V17TxState : IV32BisV17Transmitter {
    private bool _disposed;
    private int _scramblerTap = 17;

    public V17TxState() {
        Logging = new V17TxLog();
        CurrentGetBit = V17Tx.FakeGetBitDelegate;
    }

    public V17TxState(
        int bitRate,
        bool useTep,
        V32BisGetBitDelegate? getBit,
        object? userData = null)
        : this() {
        V17Tx.Initialize(
            this,
            bitRate,
            useTep,
            getBit,
            userData);
    }

    public int BitRate { get; internal set; }

    public V32BisGetBitDelegate? GetBit { get; internal set; }

    public object? GetBitUserData { get; internal set; }

    public V17TxModemStatusDelegate? StatusHandler { get; internal set; }

    public object? StatusUserData { get; internal set; }

    public float Gain { get; internal set; }

    public int RrcFilterStep { get; internal set; }

    public int DifferentialState { get; internal set; }

    public int ConvolutionState { get; internal set; }

    public int ConstellationState { get; internal set; }

    public uint ScrambleRegister { get; internal set; }

    public bool InTraining { get; internal set; }

    public bool ShortTraining { get; internal set; }

    public int TrainingStep { get; internal set; }

    public uint CarrierPhase { get; internal set; }

    public int CarrierPhaseRate { get; internal set; }

    public int BaudPhase { get; internal set; }

    public int BitsPerSymbol { get; internal set; }

    public V17TxLog Logging { get; }

    public bool IsDisposed => _disposed;

    public int ScramblerTap {
        get {
            ThrowIfDisposed();
            return _scramblerTap;
        }
        set {
            ThrowIfDisposed();

            if ((uint)value > 31u) {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "The scrambler tap must be between 0 and 31.");
            }

            _scramblerTap = value;
        }
    }

    internal float[] RrcFilterReal { get; } =
        new float[V17Tx.FilterSteps];

    internal float[] RrcFilterImaginary { get; } =
        new float[V17Tx.FilterSteps];

    internal V17TxComplex[] Constellation { get; set; } =
        Array.Empty<V17TxComplex>();

    internal V32BisGetBitDelegate? CurrentGetBit { get; set; }

    // Native-name aliases for direct source migration.
    public int bit_rate {
        get => BitRate;
        internal set => BitRate = value;
    }

    public int scrambler_tap {
        get => ScramblerTap;
        set => ScramblerTap = value;
    }

    public int bits_per_symbol => BitsPerSymbol;

    public int training_step => TrainingStep;

    public int Transmit(Span<short> samples) {
        ThrowIfDisposed();
        return V17Tx.Transmit(this, samples);
    }

    public int Restart(
        int bitRate,
        bool useTep,
        bool shortTrain) {
        ThrowIfDisposed();
        return V17Tx.Restart(
            this,
            bitRate,
            useTep,
            shortTrain);
    }

    public void SetPower(float powerDbm0) {
        ThrowIfDisposed();
        V17Tx.SetPower(this, powerDbm0);
    }

    public void SetGetBit(
        V32BisGetBitDelegate? getBit,
        object? userData) {
        ThrowIfDisposed();
        V17Tx.SetGetBit(
            this,
            getBit,
            userData);
    }

    public void SetModemStatusHandler(
        V17TxModemStatusDelegate? handler,
        object? userData = null) {
        ThrowIfDisposed();
        V17Tx.SetModemStatusHandler(
            this,
            handler,
            userData);
    }

    public int Release() {
        return 0;
    }

    public void Dispose() {
        if (_disposed)
            return;

        GetBit = null;
        GetBitUserData = null;
        CurrentGetBit = null;
        StatusHandler = null;
        StatusUserData = null;
        Constellation = Array.Empty<V17TxComplex>();
        Array.Clear(RrcFilterReal);
        Array.Clear(RrcFilterImaginary);
        BitRate = 0;
        BitsPerSymbol = 0;
        Gain = 0.0f;
        InTraining = false;
        TrainingStep = V17Tx.TrainingShutdownEnd;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    internal void ResetForInitialization() {
        _disposed = false;
        BitRate = 0;
        GetBit = null;
        GetBitUserData = null;
        CurrentGetBit = V17Tx.FakeGetBitDelegate;
        StatusHandler = null;
        StatusUserData = null;
        Gain = 0.0f;
        Constellation = Array.Empty<V17TxComplex>();
        Array.Clear(RrcFilterReal);
        Array.Clear(RrcFilterImaginary);
        RrcFilterStep = 0;
        DifferentialState = 0;
        ConvolutionState = 0;
        ConstellationState = 0;
        ScrambleRegister = 0;
        _scramblerTap = 17;
        InTraining = false;
        ShortTraining = false;
        TrainingStep = 0;
        CarrierPhase = 0;
        CarrierPhaseRate = 0;
        BaudPhase = 0;
        BitsPerSymbol = 0;
        Logging.Protocol = "V.17 TX";
    }

    internal void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

/// <summary>
/// Managed V.17 QAM transmitter.
/// </summary>
public static class V17Tx {
    public const int SampleRate = 8000;

    public const int FilterSteps = 9;

    public const int PulseShaperCoefficientSets = 10;

    public const float PulseShaperGain = 1.0f;

    public const float CarrierNominalFrequency = 1800.0f;

    public const float Dbm0MaximumSinePower = 3.14f;

    public const int TrainingSegmentTepA = 0;

    public const int TrainingSegmentTepB =
        TrainingSegmentTepA + 480;

    public const int TrainingSegment1 =
        TrainingSegmentTepB + 48;

    public const int TrainingSegment2 =
        TrainingSegment1 + 256;

    public const int TrainingSegment3 =
        TrainingSegment2 + 2976;

    public const int TrainingSegment4 =
        TrainingSegment3 + 64;

    public const int TrainingShortSegment4 =
        TrainingSegment2 + 38;

    public const int TrainingEnd =
        TrainingSegment4 + 48;

    public const int TrainingShutdownA =
        TrainingEnd + 32;

    public const int TrainingShutdownEnd =
        TrainingShutdownA + 48;

    public const int BridgeWord = 0x8880;

    internal static readonly V32BisGetBitDelegate FakeGetBitDelegate =
        static _ => 1;

    private static readonly int[] CdbaToAbcd =
        { 2, 3, 1, 0 };

    private static readonly int[] DibitToStep =
        { 1, 0, 2, 3 };

    private static readonly byte[,] V32Bis4800DifferentialEncoder =
    {
        { 2, 3, 0, 1 },
        { 0, 2, 1, 3 },
        { 3, 1, 2, 0 },
        { 1, 0, 3, 2 }
    };

    private static readonly byte[,] V17DifferentialEncoder =
    {
        { 0, 1, 2, 3 },
        { 1, 2, 3, 0 },
        { 2, 3, 0, 1 },
        { 3, 0, 1, 2 }
    };

    private static readonly byte[,] V17ConvolutionalEncoder =
    {
        { 0, 2, 3, 1 },
        { 4, 7, 5, 6 },
        { 1, 3, 2, 0 },
        { 7, 4, 6, 5 },
        { 2, 0, 1, 3 },
        { 6, 5, 7, 4 },
        { 3, 1, 0, 2 },
        { 5, 6, 4, 7 }
    };

    private static readonly V17TxComplex[] Constellation14400 =
    {
        new(-8.0f, -3.0f),
        new(9.0f, 2.0f),
        new(2.0f, -9.0f),
        new(-3.0f, 8.0f),
        new(8.0f, 3.0f),
        new(-9.0f, -2.0f),
        new(-2.0f, 9.0f),
        new(3.0f, -8.0f),
        new(-8.0f, 1.0f),
        new(9.0f, -2.0f),
        new(-2.0f, -9.0f),
        new(1.0f, 8.0f),
        new(8.0f, -1.0f),
        new(-9.0f, 2.0f),
        new(2.0f, 9.0f),
        new(-1.0f, -8.0f),
        new(-4.0f, -3.0f),
        new(5.0f, 2.0f),
        new(2.0f, -5.0f),
        new(-3.0f, 4.0f),
        new(4.0f, 3.0f),
        new(-5.0f, -2.0f),
        new(-2.0f, 5.0f),
        new(3.0f, -4.0f),
        new(-4.0f, 1.0f),
        new(5.0f, -2.0f),
        new(-2.0f, -5.0f),
        new(1.0f, 4.0f),
        new(4.0f, -1.0f),
        new(-5.0f, 2.0f),
        new(2.0f, 5.0f),
        new(-1.0f, -4.0f),
        new(4.0f, -3.0f),
        new(-3.0f, 2.0f),
        new(2.0f, 3.0f),
        new(-3.0f, -4.0f),
        new(-4.0f, 3.0f),
        new(3.0f, -2.0f),
        new(-2.0f, -3.0f),
        new(3.0f, 4.0f),
        new(4.0f, 1.0f),
        new(-3.0f, -2.0f),
        new(-2.0f, 3.0f),
        new(1.0f, -4.0f),
        new(-4.0f, -1.0f),
        new(3.0f, 2.0f),
        new(2.0f, -3.0f),
        new(-1.0f, 4.0f),
        new(0.0f, -3.0f),
        new(1.0f, 2.0f),
        new(2.0f, -1.0f),
        new(-3.0f, 0.0f),
        new(0.0f, 3.0f),
        new(-1.0f, -2.0f),
        new(-2.0f, 1.0f),
        new(3.0f, 0.0f),
        new(0.0f, 1.0f),
        new(1.0f, -2.0f),
        new(-2.0f, -1.0f),
        new(1.0f, 0.0f),
        new(0.0f, -1.0f),
        new(-1.0f, 2.0f),
        new(2.0f, 1.0f),
        new(-1.0f, 0.0f),
        new(8.0f, -3.0f),
        new(-7.0f, 2.0f),
        new(2.0f, 7.0f),
        new(-3.0f, -8.0f),
        new(-8.0f, 3.0f),
        new(7.0f, -2.0f),
        new(-2.0f, -7.0f),
        new(3.0f, 8.0f),
        new(8.0f, 1.0f),
        new(-7.0f, -2.0f),
        new(-2.0f, 7.0f),
        new(1.0f, -8.0f),
        new(-8.0f, -1.0f),
        new(7.0f, 2.0f),
        new(2.0f, -7.0f),
        new(-1.0f, 8.0f),
        new(-4.0f, -7.0f),
        new(5.0f, 6.0f),
        new(6.0f, -5.0f),
        new(-7.0f, 4.0f),
        new(4.0f, 7.0f),
        new(-5.0f, -6.0f),
        new(-6.0f, 5.0f),
        new(7.0f, -4.0f),
        new(-4.0f, 5.0f),
        new(5.0f, -6.0f),
        new(-6.0f, -5.0f),
        new(5.0f, 4.0f),
        new(4.0f, -5.0f),
        new(-5.0f, 6.0f),
        new(6.0f, 5.0f),
        new(-5.0f, -4.0f),
        new(4.0f, -7.0f),
        new(-3.0f, 6.0f),
        new(6.0f, 3.0f),
        new(-7.0f, -4.0f),
        new(-4.0f, 7.0f),
        new(3.0f, -6.0f),
        new(-6.0f, -3.0f),
        new(7.0f, 4.0f),
        new(4.0f, 5.0f),
        new(-3.0f, -6.0f),
        new(-6.0f, 3.0f),
        new(5.0f, -4.0f),
        new(-4.0f, -5.0f),
        new(3.0f, 6.0f),
        new(6.0f, -3.0f),
        new(-5.0f, 4.0f),
        new(0.0f, -7.0f),
        new(1.0f, 6.0f),
        new(6.0f, -1.0f),
        new(-7.0f, 0.0f),
        new(0.0f, 7.0f),
        new(-1.0f, -6.0f),
        new(-6.0f, 1.0f),
        new(7.0f, 0.0f),
        new(0.0f, 5.0f),
        new(1.0f, -6.0f),
        new(-6.0f, -1.0f),
        new(5.0f, 0.0f),
        new(0.0f, -5.0f),
        new(-1.0f, 6.0f),
        new(6.0f, 1.0f),
        new(-5.0f, 0.0f)
    };

    private static readonly V17TxComplex[] Constellation12000 =
    {
        new(7.0f, 1.0f),
        new(-5.0f, -1.0f),
        new(-1.0f, 5.0f),
        new(1.0f, -7.0f),
        new(-7.0f, -1.0f),
        new(5.0f, 1.0f),
        new(1.0f, -5.0f),
        new(-1.0f, 7.0f),
        new(3.0f, -3.0f),
        new(-1.0f, 3.0f),
        new(3.0f, 1.0f),
        new(-3.0f, -3.0f),
        new(-3.0f, 3.0f),
        new(1.0f, -3.0f),
        new(-3.0f, -1.0f),
        new(3.0f, 3.0f),
        new(7.0f, -7.0f),
        new(-5.0f, 7.0f),
        new(7.0f, 5.0f),
        new(-7.0f, -7.0f),
        new(-7.0f, 7.0f),
        new(5.0f, -7.0f),
        new(-7.0f, -5.0f),
        new(7.0f, 7.0f),
        new(-1.0f, -7.0f),
        new(3.0f, 7.0f),
        new(7.0f, -3.0f),
        new(-7.0f, 1.0f),
        new(1.0f, 7.0f),
        new(-3.0f, -7.0f),
        new(-7.0f, 3.0f),
        new(7.0f, -1.0f),
        new(3.0f, 5.0f),
        new(-1.0f, -5.0f),
        new(-5.0f, 1.0f),
        new(5.0f, -3.0f),
        new(-3.0f, -5.0f),
        new(1.0f, 5.0f),
        new(5.0f, -1.0f),
        new(-5.0f, 3.0f),
        new(-1.0f, 1.0f),
        new(3.0f, -1.0f),
        new(-1.0f, -3.0f),
        new(1.0f, 1.0f),
        new(1.0f, -1.0f),
        new(-3.0f, 1.0f),
        new(1.0f, 3.0f),
        new(-1.0f, -1.0f),
        new(-5.0f, 5.0f),
        new(7.0f, -5.0f),
        new(-5.0f, -7.0f),
        new(5.0f, 5.0f),
        new(5.0f, -5.0f),
        new(-7.0f, 5.0f),
        new(5.0f, 7.0f),
        new(-5.0f, -5.0f),
        new(-5.0f, -3.0f),
        new(7.0f, 3.0f),
        new(3.0f, -7.0f),
        new(-3.0f, 5.0f),
        new(5.0f, 3.0f),
        new(-7.0f, -3.0f),
        new(-3.0f, 7.0f),
        new(3.0f, -5.0f)
    };

    private static readonly V17TxComplex[] Constellation9600 =
    {
        new(-8.0f, 2.0f),
        new(-6.0f, -4.0f),
        new(-4.0f, 6.0f),
        new(2.0f, 8.0f),
        new(8.0f, -2.0f),
        new(6.0f, 4.0f),
        new(4.0f, -6.0f),
        new(-2.0f, -8.0f),
        new(0.0f, 2.0f),
        new(-6.0f, 4.0f),
        new(4.0f, 6.0f),
        new(2.0f, 0.0f),
        new(0.0f, -2.0f),
        new(6.0f, -4.0f),
        new(-4.0f, -6.0f),
        new(-2.0f, 0.0f),
        new(0.0f, -6.0f),
        new(2.0f, -4.0f),
        new(-4.0f, -2.0f),
        new(-6.0f, 0.0f),
        new(0.0f, 6.0f),
        new(-2.0f, 4.0f),
        new(4.0f, 2.0f),
        new(6.0f, 0.0f),
        new(8.0f, 2.0f),
        new(2.0f, 4.0f),
        new(4.0f, -2.0f),
        new(2.0f, -8.0f),
        new(-8.0f, -2.0f),
        new(-2.0f, -4.0f),
        new(-4.0f, 2.0f),
        new(-2.0f, 8.0f)
    };

    private static readonly V17TxComplex[] Constellation7200 =
    {
        new(6.0f, -6.0f),
        new(-2.0f, 6.0f),
        new(6.0f, 2.0f),
        new(-6.0f, -6.0f),
        new(-6.0f, 6.0f),
        new(2.0f, -6.0f),
        new(-6.0f, -2.0f),
        new(6.0f, 6.0f),
        new(-2.0f, 2.0f),
        new(6.0f, -2.0f),
        new(-2.0f, -6.0f),
        new(2.0f, 2.0f),
        new(2.0f, -2.0f),
        new(-6.0f, 2.0f),
        new(2.0f, 6.0f),
        new(-2.0f, -2.0f)
    };

    private static readonly V17TxComplex[] Constellation4800 =
    {
        new(-6.0f, -2.0f),
        new(-2.0f, 6.0f),
        new(2.0f, -6.0f),
        new(6.0f, 2.0f)
    };

    private static readonly V17TxComplex[] AbcdConstellation =
    {
        new(-6.0f, -2.0f),
        new(2.0f, -6.0f),
        new(6.0f, 2.0f),
        new(-2.0f, 6.0f)
    };

    private static readonly float[][] PulseShaper =
    {
        new float[]
        {
            -0.0028949626f,
            -0.0180558777f,
            0.0644370035f,
            -0.1680546392f,
            0.6136030985f,
            0.6136030984f,
            -0.1680546392f,
            0.0644370034f,
            -0.0180558778f
        },
        new float[]
        {
            0.0031457248f,
            -0.0296755147f,
            0.0821538018f,
            -0.1948071696f,
            0.7563219631f,
            0.4608861941f,
            -0.1273859915f,
            0.0418434579f,
            -0.0059021774f
        },
        new float[]
        {
            0.0095859909f,
            -0.0389394472f,
            0.0918555210f,
            -0.2016880234f,
            0.8793516917f,
            0.3081345068f,
            -0.0792085179f,
            0.0176601554f,
            0.0051283325f
        },
        new float[]
        {
            0.0153896883f,
            -0.0441001646f,
            0.0909724653f,
            -0.1838386340f,
            0.9741012686f,
            0.1647552955f,
            -0.0297442724f,
            -0.0050682341f,
            0.0137350940f
        },
        new float[]
        {
            0.0194884088f,
            -0.0437412561f,
            0.0779044330f,
            -0.1380831560f,
            1.0338274098f,
            0.0388498604f,
            0.0155354801f,
            -0.0238603979f,
            0.0191007894f
        },
        new float[]
        {
            0.0209425252f,
            -0.0370198693f,
            0.0523524602f,
            -0.0633894605f,
            1.0542286891f,
            -0.0633894606f,
            0.0523524602f,
            -0.0370198693f,
            0.0209425251f
        },
        new float[]
        {
            0.0191007894f,
            -0.0238603978f,
            0.0155354801f,
            0.0388498605f,
            1.0338274098f,
            -0.1380831561f,
            0.0779044330f,
            -0.0437412561f,
            0.0194884087f
        },
        new float[]
        {
            0.0137350940f,
            -0.0050682341f,
            -0.0297442724f,
            0.1647552955f,
            0.9741012686f,
            -0.1838386340f,
            0.0909724652f,
            -0.0441001646f,
            0.0153896883f
        },
        new float[]
        {
            0.0051283326f,
            0.0176601554f,
            -0.0792085179f,
            0.3081345069f,
            0.8793516917f,
            -0.2016880235f,
            0.0918555209f,
            -0.0389394473f,
            0.0095859909f
        },
        new float[]
        {
            -0.0059021774f,
            0.0418434580f,
            -0.1273859915f,
            0.4608861942f,
            0.7563219631f,
            -0.1948071696f,
            0.0821538018f,
            -0.0296755147f,
            0.0031457248f
        }
    };

    public static bool IsValidBitRate(int bitRate) {
        return bitRate is
            4800 or
            7200 or
            9600 or
            12000 or
            14400;
    }

    public static V17TxState? Initialize(
        V17TxState? state,
        int bitRate,
        bool useTep,
        V32BisGetBitDelegate? getBit,
        object? userData) {
        if (!IsValidBitRate(bitRate))
            return null;

        state ??= new V17TxState();
        state.ResetForInitialization();
        state.GetBit = getBit;
        state.GetBitUserData = userData;
        state.ScramblerTap = 17;
        state.CarrierPhaseRate =
            PhaseRate(CarrierNominalFrequency);

        SetPower(state, -14.0f);

        if (Restart(
                state,
                bitRate,
                useTep,
                shortTrain: false) < 0) {
            return null;
        }

        return state;
    }

    public static int Restart(
        V17TxState state,
        int bitRate,
        bool useTep,
        bool shortTrain) {
        ValidateState(state);

        switch (bitRate) {
            case 14400:
                state.BitsPerSymbol = 6;
                state.Constellation = Constellation14400;
                break;

            case 12000:
                state.BitsPerSymbol = 5;
                state.Constellation = Constellation12000;
                break;

            case 9600:
                state.BitsPerSymbol = 4;
                state.Constellation = Constellation9600;
                break;

            case 7200:
                state.BitsPerSymbol = 3;
                state.Constellation = Constellation7200;
                break;

            case 4800:
                // V.17 itself does not define 4800 bit/s. The native module
                // includes this mode for complete V.32bis coverage.
                state.BitsPerSymbol = 2;
                state.Constellation = Constellation4800;
                break;

            default:
                return -1;
        }

        state.BitRate = bitRate;
        state.DifferentialState = shortTrain ? 0 : 1;
        Array.Clear(state.RrcFilterReal);
        Array.Clear(state.RrcFilterImaginary);
        state.RrcFilterStep = 0;
        state.ConvolutionState = 0;
        state.ScrambleRegister = 0x002ECDD5u;
        state.InTraining = true;
        state.ShortTraining = shortTrain;
        state.TrainingStep = useTep
            ? TrainingSegmentTepA
            : TrainingSegment1;
        state.CarrierPhase = 0;
        state.BaudPhase = 0;
        state.ConstellationState = 0;
        state.CurrentGetBit = FakeGetBitDelegate;
        return 0;
    }

    public static int Transmit(
        V17TxState state,
        Span<short> samples) {
        ValidateState(state);

        if (state.TrainingStep >=
            TrainingShutdownEnd) {
            return 0;
        }

        for (int sample = 0;
             sample < samples.Length;
             sample++) {
            state.BaudPhase += 3;

            if (state.BaudPhase >= 10) {
                state.BaudPhase -= 10;

                V17TxComplex symbol =
                    GetBaud(state);

                state.RrcFilterReal[
                    state.RrcFilterStep] = symbol.Real;

                state.RrcFilterImaginary[
                    state.RrcFilterStep] = symbol.Imaginary;

                state.RrcFilterStep++;

                if (state.RrcFilterStep >=
                    FilterSteps) {
                    state.RrcFilterStep = 0;
                }
            }

            int coefficientSet =
                PulseShaperCoefficientSets -
                1 -
                state.BaudPhase;

            float basebandReal =
                CircularDotProduct(
                    state.RrcFilterReal,
                    PulseShaper[coefficientSet],
                    state.RrcFilterStep);

            float basebandImaginary =
                CircularDotProduct(
                    state.RrcFilterImaginary,
                    PulseShaper[coefficientSet],
                    state.RrcFilterStep);

            V17TxComplex carrier =
                GenerateCarrier(state);

            float amplitude =
                basebandReal * carrier.Real -
                basebandImaginary * carrier.Imaginary;

            samples[sample] =
                FastRoundToInt16(
                    amplitude * state.Gain);
        }

        return samples.Length;
    }

    public static int Transmit(
        V17TxState state,
        short[] samples,
        int length) {
        ArgumentNullException.ThrowIfNull(samples);

        if ((uint)length > (uint)samples.Length) {
            throw new ArgumentOutOfRangeException(
                nameof(length));
        }

        return Transmit(
            state,
            samples.AsSpan(0, length));
    }

    public static void SetPower(
        V17TxState state,
        float powerDbm0) {
        ValidateState(state);

        // The constellation design maintains approximately constant average
        // power at each supported bit rate.
        state.Gain =
            0.223f *
            DbToAmplitudeRatio(
                powerDbm0 -
                Dbm0MaximumSinePower) *
            32768.0f /
            PulseShaperGain;
    }

    public static void SetGetBit(
        V17TxState state,
        V32BisGetBitDelegate? getBit,
        object? userData) {
        ValidateState(state);

        if (Equals(
                state.GetBit,
                state.CurrentGetBit)) {
            state.CurrentGetBit = getBit;
        }

        state.GetBit = getBit;
        state.GetBitUserData = userData;
    }

    public static void SetModemStatusHandler(
        V17TxState state,
        V17TxModemStatusDelegate? handler,
        object? userData) {
        ValidateState(state);
        state.StatusHandler = handler;
        state.StatusUserData = userData;
    }

    public static V17TxLog GetLoggingState(
        V17TxState state) {
        ValidateState(state);
        return state.Logging;
    }

    public static int Release(V17TxState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    public static int Free(V17TxState? state) {
        state?.Dispose();
        return 0;
    }

    private static int Scramble(
        V17TxState state,
        int inputBit) {
        int outputBit =
            (inputBit ^
             (int)(state.ScrambleRegister >>
                   state.ScramblerTap) ^
             (int)(state.ScrambleRegister >> 22)) &
            0x01;

        state.ScrambleRegister =
            unchecked(
                (state.ScrambleRegister << 1) |
                (uint)outputBit);

        return outputBit;
    }

    private static V17TxComplex TrainingGet(
        V17TxState state) {
        state.TrainingStep++;

        if (state.TrainingStep <=
            TrainingSegment3) {
            if (state.TrainingStep <=
                TrainingSegment2) {
                if (state.TrainingStep <=
                    TrainingSegmentTepB) {
                    // Optional unmodulated carrier for talker echo protection.
                    return AbcdConstellation[0];
                }

                if (state.TrainingStep <=
                    TrainingSegment1) {
                    // Optional TEP silence.
                    return default;
                }

                // Segment 1: ABAB...
                return AbcdConstellation[
                    (state.TrainingStep & 1) ^ 1];
            }

            // Segment 2: CDBA...
            int bits = Scramble(state, 1);
            bits =
                (bits << 1) |
                Scramble(state, 1);

            state.ConstellationState =
                CdbaToAbcd[bits];

            if (state.ShortTraining &&
                state.TrainingStep ==
                TrainingShortSegment4) {
                state.TrainingStep =
                    TrainingSegment4;
            }

            return AbcdConstellation[
                state.ConstellationState];
        }

        // Segment 3: bridge sequence.
        int shift =
            ((state.TrainingStep -
              TrainingSegment3 -
              1) & 0x07) << 1;

        int bridgeBits =
            Scramble(
                state,
                BridgeWord >> shift);

        bridgeBits =
            (bridgeBits << 1) |
            Scramble(
                state,
                BridgeWord >> (shift + 1));

        state.ConstellationState =
            (state.ConstellationState +
             DibitToStep[bridgeBits]) &
            0x03;

        return AbcdConstellation[
            state.ConstellationState];
    }

    private static int DifferentialAndConvolutionalEncode(
        V17TxState state,
        int input) {
        if (state.BitsPerSymbol == 2) {
            // V.32bis 4800 bit/s mode has differential encoding but no
            // trellis-coded redundant bit.
            state.DifferentialState =
                V32Bis4800DifferentialEncoder[
                    state.DifferentialState,
                    input & 0x03];

            return state.DifferentialState;
        }

        state.DifferentialState =
            V17DifferentialEncoder[
                state.DifferentialState,
                input & 0x03];

        state.ConvolutionState =
            V17ConvolutionalEncoder[
                state.ConvolutionState,
                state.DifferentialState];

        return
            ((input << 1) & 0x78) |
            (state.DifferentialState << 1) |
            ((state.ConvolutionState >> 2) & 0x01);
    }

    private static V17TxComplex GetBaud(
        V17TxState state) {
        if (state.TrainingStep >=
            TrainingShutdownEnd) {
            return default;
        }

        if (state.InTraining) {
            if (state.TrainingStep <=
                TrainingEnd) {
                if (state.TrainingStep <
                    TrainingSegment4) {
                    return TrainingGet(state);
                }

                state.TrainingStep++;

                if (state.TrainingStep >
                    TrainingEnd) {
                    state.CurrentGetBit =
                        state.GetBit ??
                        FakeGetBitDelegate;

                    state.InTraining = false;
                }
            } else {
                state.TrainingStep++;

                // The native source places the shutdown-complete callback
                // after an early return. Here it is emitted at the intended
                // end of the 48-symbol silence interval.
                if (state.TrainingStep >=
                    TrainingShutdownEnd) {
                    state.StatusHandler?.Invoke(
                        state.StatusUserData,
                        (int)V17TxSignalStatus.ShutdownComplete);

                    state.TrainingStep =
                        TrainingShutdownEnd;

                    return default;
                }

                if (state.TrainingStep >
                    TrainingShutdownA) {
                    return default;
                }
            }
        }

        int bits = 0;

        for (int index = 0;
             index < state.BitsPerSymbol;
             index++) {
            V32BisGetBitDelegate bitSource =
                state.CurrentGetBit ??
                FakeGetBitDelegate;

            int bit =
                bitSource(
                    state.GetBitUserData);

            if (bit ==
                (int)V17TxSignalStatus.EndOfData) {
                state.StatusHandler?.Invoke(
                    state.StatusUserData,
                    (int)V17TxSignalStatus.EndOfData);

                state.CurrentGetBit =
                    FakeGetBitDelegate;

                state.InTraining = true;
                bit = 1;
            }

            bits |=
                Scramble(state, bit) << index;
        }

        int constellationIndex =
            DifferentialAndConvolutionalEncode(
                state,
                bits);

        if ((uint)constellationIndex >=
            (uint)state.Constellation.Length) {
            throw new InvalidOperationException(
                "The V.17 constellation encoder produced an invalid index.");
        }

        return state.Constellation[
            constellationIndex];
    }

    private static float CircularDotProduct(
        float[] circularBuffer,
        float[] coefficients,
        int startPosition) {
        float result = 0.0f;
        int position = startPosition;

        for (int index = 0;
             index < FilterSteps;
             index++) {
            result +=
                circularBuffer[position] *
                coefficients[index];

            position++;

            if (position >= FilterSteps)
                position = 0;
        }

        return result;
    }

    private static V17TxComplex GenerateCarrier(
        V17TxState state) {
        const double PhaseScale =
            2.0 * Math.PI /
            4294967296.0;

        double radians =
            state.CarrierPhase *
            PhaseScale;

        V17TxComplex result =
            new(
                (float)Math.Cos(radians),
                (float)Math.Sin(radians));

        state.CarrierPhase =
            unchecked(
                state.CarrierPhase +
                (uint)state.CarrierPhaseRate);

        return result;
    }

    private static int PhaseRate(float frequency) {
        return unchecked((int)(
            frequency *
            4294967296.0 /
            SampleRate));
    }

    private static float DbToAmplitudeRatio(float decibels) {
        return MathF.Pow(
            10.0f,
            decibels / 20.0f);
    }

    private static short FastRoundToInt16(float value) {
        int rounded =
            (int)MathF.Round(
                value,
                MidpointRounding.ToEven);

        // The native implementation intentionally omits saturation because
        // the configured gain and constellation should not clip.
        return unchecked((short)rounded);
    }

    private static void ValidateState(
        V17TxState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
    }
}

/// <summary>
/// C-compatible facade preserving the original v17_tx_* names.
/// </summary>
public static class V17TxApi {
    public const int V17_TX_FILTER_STEPS =
        V17Tx.FilterSteps;

    public static void v17_tx_power(
        V17TxState state,
        float power) {
        V17Tx.SetPower(state, power);
    }

    public static V17TxState? v17_tx_init(
        V17TxState? state,
        int bitRate,
        bool tep,
        V32BisGetBitDelegate? getBit,
        object? userData) {
        return V17Tx.Initialize(
            state,
            bitRate,
            tep,
            getBit,
            userData);
    }

    public static int v17_tx_restart(
        V17TxState state,
        int bitRate,
        bool tep,
        bool shortTrain) {
        return V17Tx.Restart(
            state,
            bitRate,
            tep,
            shortTrain);
    }

    public static int v17_tx_release(
        V17TxState state) {
        return V17Tx.Release(state);
    }

    public static int v17_tx_free(
        V17TxState? state) {
        return V17Tx.Free(state);
    }

    public static V17TxLog v17_tx_get_logging_state(
        V17TxState state) {
        return V17Tx.GetLoggingState(state);
    }

    public static void v17_tx_set_get_bit(
        V17TxState state,
        V32BisGetBitDelegate? getBit,
        object? userData) {
        V17Tx.SetGetBit(
            state,
            getBit,
            userData);
    }

    public static void v17_tx_set_modem_status_handler(
        V17TxState state,
        V17TxModemStatusDelegate? handler,
        object? userData) {
        V17Tx.SetModemStatusHandler(
            state,
            handler,
            userData);
    }

    public static int v17_tx(
        V17TxState state,
        Span<short> samples) {
        return V17Tx.Transmit(
            state,
            samples);
    }

    public static int v17_tx(
        V17TxState state,
        short[] samples,
        int length) {
        return V17Tx.Transmit(
            state,
            samples,
            length);
    }
}
