/*
 * TKFaxEngine - managed C# port
 *
 * V27TerRx.cs - ITU V.27ter modem receive part
 *
 * Combined port of:
 *   v27ter_rx.h
 *   private/v27ter_rx.h (merged into the supplied v27ter_rx.h)
 *   v27ter_rx.c
 *   v27ter_rx_4800_rrc.h
 *   v27ter_rx_2400_rrc.h
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2003 Steve Underwood.
 *
 * This file preserves the GNU Lesser General Public License version 2.1
 * licensing terms of the original source files.
 */

#nullable enable

namespace TKFaxEngine.Modem.V27;

/// <summary>
/// Complex floating-point value used by the V.27ter receiver.
/// </summary>
public struct V27TerRxComplex {
    public V27TerRxComplex(float real, float imaginary) {
        Real = real;
        Imaginary = imaginary;
    }

    public float Real;
    public float Imaginary;

    public float Re {
        readonly get => Real;
        set => Real = value;
    }

    public float Im {
        readonly get => Imaginary;
        set => Imaginary = value;
    }
}

/// <summary>
/// Special modem status values used by the native async/modem API.
/// </summary>
public enum V27TerRxSignalStatus {
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
/// V.27ter receiver training stages.
/// </summary>
public enum V27TerRxTrainingStage {
    NormalOperation = 0,
    SymbolAcquisition = 1,
    LogPhase = 2,
    WaitForHop = 3,
    TrainOnAbab = 4,
    TestOnes = 5,
    Parked = 6
}

/// <summary>
/// Receives decoded bits or negative V27TerRxSignalStatus values.
/// </summary>
public delegate void V27TerRxPutBitDelegate(
    object? userData,
    int bit);

/// <summary>
/// Receives modem status changes.
/// </summary>
public delegate void V27TerRxModemStatusDelegate(
    object? userData,
    int status);

/// <summary>
/// Receives QAM/PSK constellation reports. Received and target are null for
/// symbol-timing correction reports, matching the native callback convention.
/// </summary>
public delegate void V27TerRxQamReportDelegate(
    object? userData,
    V27TerRxComplex? received,
    V27TerRxComplex? target,
    int value);

/// <summary>
/// Minimal logging context corresponding to logging_state_t.
/// </summary>
public sealed class V27TerRxLog {
    public string Protocol { get; set; } = "V.27ter RX";

    public Action<string>? FlowSink { get; set; }

    public Action<string>? DebugSink { get; set; }

    public void Flow(string message) => FlowSink?.Invoke(message);

    public void Debug(string message) => DebugSink?.Invoke(message);
}

/// <summary>
/// Managed equivalent of v27ter_rx_state_t.
/// </summary>
public sealed class V27TerRxState : IDisposable {
    private bool _disposed;

    public V27TerRxState() {
        EqualizerCoefficients =
            new V27TerRxComplex[V27TerRx.EqualizerLength];

        SavedEqualizerCoefficients =
            new V27TerRxComplex[V27TerRx.EqualizerLength];

        EqualizerBuffer =
            new V27TerRxComplex[V27TerRx.EqualizerLength];

        RrcFilter =
            new float[V27TerRx.FilterSteps];

        LastAngles = new int[2];
        DifferenceAngles = new int[16];
        Logging = new V27TerRxLog();
    }

    public V27TerRxState(
        int bitRate,
        V27TerRxPutBitDelegate? putBit,
        object? userData = null)
        : this() {
        if (V27TerRx.Initialize(
                this,
                bitRate,
                putBit,
                userData) is null) {
            throw new ArgumentOutOfRangeException(
                nameof(bitRate),
                bitRate,
                "V.27ter supports 2400 or 4800 bit/s.");
        }
    }

    public int BitRate { get; internal set; }

    public V27TerRxPutBitDelegate? PutBit { get; internal set; }

    public object? PutBitUserData { get; internal set; }

    public V27TerRxModemStatusDelegate? StatusHandler { get; internal set; }

    public object? StatusUserData { get; internal set; }

    public V27TerRxQamReportDelegate? QamReport { get; internal set; }

    public object? QamUserData { get; internal set; }

    public float AgcScaling { get; internal set; }

    public float SavedAgcScaling { get; internal set; }

    public float EqualizerDelta { get; internal set; }

    public V27TerRxComplex[] EqualizerCoefficients { get; }

    public V27TerRxComplex[] SavedEqualizerCoefficients { get; }

    public V27TerRxComplex[] EqualizerBuffer { get; }

    public float TrainingError { get; internal set; }

    public float CarrierTrackProportional { get; internal set; }

    public float CarrierTrackIntegral { get; internal set; }

    public float[] RrcFilter { get; }

    public int RrcFilterStep { get; internal set; }

    public uint ScrambleRegister { get; internal set; }

    public int ScramblerPatternCount { get; internal set; }

    public int TrainingBc { get; internal set; }

    public bool OldTraining { get; internal set; }

    public V27TerRxTrainingStage TrainingStage { get; internal set; }

    public int TrainingCount { get; internal set; }

    public short LastSample { get; internal set; }

    public int SignalPresent { get; internal set; }

    public bool CarrierDropPending { get; internal set; }

    public int LowSamples { get; internal set; }

    public short HighSample { get; internal set; }

    public int ConstellationState { get; internal set; }

    public uint CarrierPhase { get; internal set; }

    public int CarrierPhaseRate { get; internal set; }

    public int SavedCarrierPhaseRate { get; internal set; }

    public int PowerReading { get; internal set; }

    public int PowerShift { get; internal set; }

    public int CarrierOnPower { get; internal set; }

    public int CarrierOffPower { get; internal set; }

    public int EqualizerStep { get; internal set; }

    public int EqualizerPutStep { get; internal set; }

    public int EqualizerSkip { get; internal set; }

    public int BaudHalf { get; internal set; }

    public int GardnerIntegrate { get; internal set; }

    public int GardnerStep { get; internal set; }

    public int TotalBaudTimingCorrection { get; internal set; }

    public int[] LastAngles { get; }

    public int[] DifferenceAngles { get; }

    public V27TerRxLog Logging { get; }

    public bool IsDisposed => _disposed;

    // Native-name aliases for direct source migration.
    public int bit_rate => BitRate;

    public int training_stage => (int)TrainingStage;

    public int signal_present => SignalPresent;

    public int constellation_state => ConstellationState;

    public int Restart(int bitRate, bool oldTraining) {
        ThrowIfDisposed();
        return V27TerRx.Restart(this, bitRate, oldTraining);
    }

    public int Receive(ReadOnlySpan<short> samples) {
        ThrowIfDisposed();
        return V27TerRx.Receive(this, samples);
    }

    public int FillIn(int sampleCount) {
        ThrowIfDisposed();
        return V27TerRx.FillIn(this, sampleCount);
    }

    public int Release() {
        if (_disposed)
            return 0;

        return V27TerRx.Release(this);
    }

    public void Dispose() {
        if (_disposed)
            return;

        PutBit = null;
        PutBitUserData = null;
        StatusHandler = null;
        StatusUserData = null;
        QamReport = null;
        QamUserData = null;
        _disposed = true;
    }

    internal void ResetForInitialization() {
        Array.Clear(EqualizerCoefficients);
        Array.Clear(SavedEqualizerCoefficients);
        Array.Clear(EqualizerBuffer);
        Array.Clear(RrcFilter);
        Array.Clear(LastAngles);
        Array.Clear(DifferenceAngles);

        BitRate = 0;
        PutBit = null;
        PutBitUserData = null;
        StatusHandler = null;
        StatusUserData = null;
        QamReport = null;
        QamUserData = null;
        AgcScaling = 0.0f;
        SavedAgcScaling = 0.0f;
        EqualizerDelta = 0.0f;
        TrainingError = 0.0f;
        CarrierTrackProportional = 0.0f;
        CarrierTrackIntegral = 0.0f;
        RrcFilterStep = 0;
        ScrambleRegister = 0;
        ScramblerPatternCount = 0;
        TrainingBc = 0;
        OldTraining = false;
        TrainingStage = V27TerRxTrainingStage.SymbolAcquisition;
        TrainingCount = 0;
        LastSample = 0;
        SignalPresent = 0;
        CarrierDropPending = false;
        LowSamples = 0;
        HighSample = 0;
        ConstellationState = 0;
        CarrierPhase = 0;
        CarrierPhaseRate = 0;
        SavedCarrierPhaseRate = 0;
        PowerReading = 0;
        PowerShift = 4;
        CarrierOnPower = 0;
        CarrierOffPower = 0;
        EqualizerStep = 0;
        EqualizerPutStep = 0;
        EqualizerSkip = 0;
        BaudHalf = 0;
        GardnerIntegrate = 0;
        GardnerStep = 0;
        TotalBaudTimingCorrection = 0;
        Logging.Protocol = "V.27ter RX";
        _disposed = false;
    }

    internal void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

/// <summary>
/// Floating-point managed implementation of the V.27ter receive modem.
/// </summary>
public static class V27TerRx {
    public const int SampleRate = 8000;

    public const float ConstellationScalingFactor = 1.0f;

    public const int EqualizerLength = 32;

    public const int EqualizerPreLength = 16;

    public const int FilterSteps4800 = 27;

    public const int FilterSteps2400 = 27;

    public const int FilterSteps = 27;

    public const int RrcCoefficientSets4800 = 8;

    public const int RrcCoefficientSets2400 = 12;

    private const float CarrierNominalFrequency = 1800.0f;

    private const float EqualizerAdaptationRate = 0.25f;

    private const int TrainingSegment3Length = 50;

    private const int TrainingSegment5Length = 1074;

    private const int TrainingSegment6Length = 8;

    private const float Dbm0MaximumPower = 6.16f;

    private const float RrcGain4800 = 1.0f;

    private const float RrcGain2400 = 1.0f;

    private const float LmsLeakRate = 0.9999f;

    private static readonly V27TerRxComplex Zero =
        new(0.0f, 0.0f);

    private static readonly V27TerRxComplex[] Constellation =
    [
        new( 1.414f,  0.0f),
        new( 1.0f,    1.0f),
        new( 0.0f,    1.414f),
        new(-1.0f,    1.0f),
        new(-1.414f,  0.0f),
        new(-1.0f,   -1.0f),
        new( 0.0f,   -1.414f),
        new( 1.0f,   -1.0f)
    ];

    private static readonly float[][] Rrc4800Real =
    [
        [
            -0.0033256219f, 0.0009305772f, -0.0015971838f,
            0.0000000000f, 0.0079803617f, 0.0000856198f,
            0.0134186586f, 0.0173489888f, -0.0212482254f,
            -0.0043725357f, -0.0212278153f, -0.1104697431f,
            0.0285220989f, 0.2227359397f, 0.0348435776f,
            -0.1734025047f, -0.0527331584f, 0.0378282438f,
            -0.0038217364f, 0.0176626697f, 0.0262988263f,
            -0.0046538307f, 0.0002736603f, 0.0000000000f,
            -0.0070402821f, -0.0004997092f, -0.0026831868f
        ],
        [
            -0.0034458236f, 0.0008096318f, -0.0023281302f,
            0.0000000000f, 0.0075308685f, -0.0004118107f,
            0.0152885230f, 0.0179999198f, -0.0201560093f,
            -0.0002450502f, -0.0248951622f, -0.1189640829f,
            0.0295975050f, 0.2251016204f, 0.0343715965f,
            -0.1664714791f, -0.0486568474f, 0.0315222376f,
            -0.0071130012f, 0.0183322514f, 0.0251163776f,
            -0.0040031310f, 0.0017373696f, 0.0000000000f,
            -0.0064896001f, -0.0002763861f, -0.0029729850f
        ],
        [
            -0.0035131442f, 0.0006690952f, -0.0030664478f,
            0.0000000000f, 0.0069350882f, -0.0009454877f,
            0.0171306802f, 0.0185057203f, -0.0187663592f,
            0.0042370436f, -0.0286756350f, -0.1273684927f,
            0.0306020333f, 0.2268016445f, 0.0338005390f,
            -0.1591873172f, -0.0445801856f, 0.0254709344f,
            -0.0100839971f, 0.0187737881f, 0.0237628397f,
            -0.0033572431f, 0.0030664748f, 0.0000000000f,
            -0.0058786966f, -0.0000612735f, -0.0032022932f
        ],
        [
            -0.0035245124f, 0.0005101234f, -0.0038017457f,
            0.0000000000f, 0.0061906456f, -0.0015115773f,
            0.0189227449f, 0.0188507568f, -0.0170703344f,
            0.0090637502f, -0.0325525586f, -0.1356378449f,
            0.0315298194f, 0.2278257756f, 0.0331338202f,
            -0.1515915544f, -0.0405239117f, 0.0196974304f,
            -0.0127330839f, 0.0189988027f, 0.0222629790f,
            -0.0027227277f, 0.0042544818f, 0.0000000000f,
            -0.0052193004f, 0.0001430275f, -0.0033704184f
        ],
        [
            -0.0034775077f, 0.0003341791f, -0.0045231274f,
            0.0000000000f, 0.0052967893f, -0.0021056563f,
            0.0206414815f, 0.0190199258f, -0.0150609081f,
            0.0142223503f, -0.0365082066f, -0.1437270847f,
            0.0323754133f, 0.2281678404f, 0.0323754133f,
            -0.1437270847f, -0.0365082066f, 0.0142223503f,
            -0.0150609081f, 0.0190199258f, 0.0206414815f,
            -0.0021056563f, 0.0052967893f, 0.0000000000f,
            -0.0045231274f, 0.0003341791f, -0.0034775077f
        ],
        [
            -0.0033704184f, 0.0001430275f, -0.0052193004f,
            0.0000000000f, 0.0042544818f, -0.0027227277f,
            0.0222629790f, 0.0189988027f, -0.0127330839f,
            0.0196974305f, -0.0405239117f, -0.1515915544f,
            0.0331338202f, 0.2278257756f, 0.0315298194f,
            -0.1356378448f, -0.0325525586f, 0.0090637501f,
            -0.0170703344f, 0.0188507568f, 0.0189227449f,
            -0.0015115774f, 0.0061906456f, 0.0000000000f,
            -0.0038017457f, 0.0005101234f, -0.0035245125f
        ],
        [
            -0.0032022931f, -0.0000612735f, -0.0058786966f,
            0.0000000000f, 0.0030664748f, -0.0033572431f,
            0.0237628397f, 0.0187737880f, -0.0100839971f,
            0.0254709344f, -0.0445801856f, -0.1591873172f,
            0.0338005390f, 0.2268016445f, 0.0306020333f,
            -0.1273684927f, -0.0286756350f, 0.0042370436f,
            -0.0187663592f, 0.0185057204f, 0.0171306802f,
            -0.0009454877f, 0.0069350882f, 0.0000000000f,
            -0.0030664478f, 0.0006690952f, -0.0035131442f
        ],
        [
            -0.0029729850f, -0.0002763861f, -0.0064896002f,
            0.0000000000f, 0.0017373696f, -0.0040031310f,
            0.0251163776f, 0.0183322514f, -0.0071130012f,
            0.0315222376f, -0.0486568474f, -0.1664714791f,
            0.0343715965f, 0.2251016204f, 0.0295975050f,
            -0.1189640829f, -0.0248951622f, -0.0002450503f,
            -0.0201560093f, 0.0179999198f, 0.0152885231f,
            -0.0004118107f, 0.0075308685f, 0.0000000000f,
            -0.0023281302f, 0.0008096318f, -0.0034458236f
        ]
    ];

    private static readonly float[][] Rrc4800Imaginary =
    [
        [
            -0.0016944890f, -0.0028640220f, -0.0002529691f,
            -0.0071280401f, -0.0012639651f, 0.0002635107f,
            -0.0068371480f, 0.0238788346f, 0.0212482254f,
            -0.0031768332f, 0.0416619332f, -0.0358937954f,
            -0.1800814454f, 0.0000000000f, 0.2199936907f,
            0.0563418892f, -0.1034946505f, -0.0274838279f,
            -0.0038217364f, -0.0243105793f, 0.0133999213f,
            0.0143230182f, 0.0000433435f, 0.0080798379f,
            0.0011150711f, -0.0015379468f, 0.0013671520f
        ],
        [
            -0.0017557348f, -0.0024917903f, -0.0003687396f,
            -0.0076128684f, -0.0011927724f, -0.0012674230f,
            -0.0077898916f, 0.0247747641f, 0.0201560092f,
            -0.0001780394f, 0.0488595069f, -0.0386537737f,
            -0.1868712917f, 0.0000000000f, 0.2170137194f,
            0.0540898624f, -0.0954944398f, -0.0229022462f,
            -0.0071130012f, -0.0252321794f, 0.0127974336f,
            0.0123203703f, 0.0002751723f, 0.0083907691f,
            0.0010278517f, -0.0008506289f, 0.0015148115f
        ],
        [
            -0.0017900364f, -0.0020592634f, -0.0004856776f,
            -0.0080135093f, -0.0010984101f, -0.0029099120f,
            -0.0087285175f, 0.0254709389f, 0.0187663592f,
            0.0030783924f, 0.0562791024f, -0.0413845320f,
            -0.1932136338f, 0.0000000000f, 0.2134082043f,
            0.0517230948f, -0.0874935406f, -0.0185057171f,
            -0.0100839971f, -0.0258399025f, 0.0121077716f,
            0.0103325318f, 0.0004856819f, 0.0085629051f,
            0.0009310941f, -0.0001885805f, 0.0016316499f
        ],
        [
            -0.0017958288f, -0.0015699983f, -0.0006021374f,
            -0.0083189197f, -0.0009805019f, -0.0046521567f,
            -0.0096416201f, 0.0259458409f, 0.0170703344f,
            0.0065852000f, 0.0638879935f, -0.0440714074f,
            -0.1990714451f, 0.0000000000f, 0.2091987072f,
            0.0492550818f, -0.0795326547f, -0.0143110209f,
            -0.0127330839f, -0.0261496085f, 0.0113435544f,
            0.0083796941f, 0.0006738437f, 0.0086029153f,
            0.0008266560f, 0.0004401934f, 0.0017173140f
        ],
        [
            -0.0017718787f, -0.0010284975f, -0.0007163930f,
            -0.0085186340f, -0.0008389290f, -0.0064805437f,
            -0.0105173601f, 0.0261786820f, 0.0150609081f,
            0.0103331423f, 0.0716513897f, -0.0466997607f,
            -0.2044103145f, 0.0000000000f, 0.2044103145f,
            0.0466997607f, -0.0716513897f, -0.0103331423f,
            -0.0150609081f, -0.0261786820f, 0.0105173601f,
            0.0064805437f, 0.0008389290f, 0.0085186340f,
            0.0007163930f, 0.0010284975f, 0.0017718787f
        ],
        [
            -0.0017173139f, -0.0004401934f, -0.0008266560f,
            -0.0086029153f, -0.0006738437f, -0.0083796941f,
            -0.0113435544f, 0.0261496085f, 0.0127330839f,
            0.0143110209f, 0.0795326548f, -0.0492550818f,
            -0.2091987072f, 0.0000000000f, 0.1990714450f,
            0.0440714074f, -0.0638879934f, -0.0065851999f,
            -0.0170703344f, -0.0259458409f, 0.0096416201f,
            0.0046521567f, 0.0009805019f, 0.0083189197f,
            0.0006021374f, 0.0015699983f, 0.0017958288f
        ],
        [
            -0.0016316499f, 0.0001885805f, -0.0009310941f,
            -0.0085629051f, -0.0004856819f, -0.0103325318f,
            -0.0121077716f, 0.0258399025f, 0.0100839971f,
            0.0185057171f, 0.0874935407f, -0.0517230948f,
            -0.2134082043f, 0.0000000000f, 0.1932136338f,
            0.0413845320f, -0.0562791024f, -0.0030783924f,
            -0.0187663592f, -0.0254709389f, 0.0087285175f,
            0.0029099120f, 0.0010984101f, 0.0080135093f,
            0.0004856776f, 0.0020592634f, 0.0017900364f
        ],
        [
            -0.0015148115f, 0.0008506289f, -0.0010278517f,
            -0.0083907691f, -0.0002751723f, -0.0123203703f,
            -0.0127974336f, 0.0252321794f, 0.0071130012f,
            0.0229022462f, 0.0954944398f, -0.0540898624f,
            -0.2170137194f, 0.0000000000f, 0.1868712917f,
            0.0386537737f, -0.0488595068f, 0.0001780394f,
            -0.0201560093f, -0.0247747642f, 0.0077898916f,
            0.0012674230f, 0.0011927724f, 0.0076128683f,
            0.0003687396f, 0.0024917903f, 0.0017557348f
        ]
    ];

    private static readonly float[][] Rrc2400Real =
    [
        [
            0.0055897356f, -0.0017768552f, -0.0013103941f,
            -0.0000000000f, -0.0158167681f, -0.0071354797f,
            0.0209536107f, 0.0079805593f, 0.0059737025f,
            0.0334507234f, -0.0366873781f, -0.1142993304f,
            0.0237732038f, 0.1697241604f, 0.0265507082f,
            -0.1445311961f, -0.0545612266f, 0.0653773864f,
            0.0292370043f, -0.0049656633f, 0.0120974972f,
            -0.0072670869f, -0.0228066102f, -0.0000000000f,
            0.0065619587f, -0.0004099816f, 0.0051233092f
        ],
        [
            0.0056661325f, -0.0017109301f, -0.0007708413f,
            -0.0000000000f, -0.0165439665f, -0.0072433071f,
            0.0206042605f, 0.0071755761f, 0.0076467862f,
            0.0359728645f, -0.0382251396f, -0.1171902140f,
            0.0240999711f, 0.1704283334f, 0.0264209159f,
            -0.1424503636f, -0.0531570676f, 0.0626369636f,
            0.0270658035f, -0.0036203810f, 0.0132414083f,
            -0.0073656402f, -0.0224159887f, -0.0000000000f,
            0.0058122222f, -0.0005708450f, 0.0052884997f
        ],
        [
            0.0057239309f, -0.0016362892f, -0.0002066393f,
            -0.0000000000f, -0.0172533639f, -0.0073359678f,
            0.0201882078f, 0.0063201374f, 0.0093733612f,
            0.0385302708f, -0.0397608520f, -0.1200263609f,
            0.0244107582f, 0.1710059279f, 0.0262716834f,
            -0.1402783704f, -0.0517306914f, 0.0598999340f,
            0.0249301575f, -0.0023214913f, 0.0143092556f,
            -0.0074421426f, -0.0219819122f, -0.0000000000f,
            0.0050747268f, -0.0007235570f, 0.0054290958f
        ],
        [
            0.0057623291f, -0.0015528737f, 0.0003813733f,
            -0.0000000000f, -0.0179424457f, -0.0074126923f,
            0.0197040293f, 0.0054142368f, 0.0111521026f,
            0.0411200945f, -0.0412925125f, -0.1228037551f,
            0.0247051052f, 0.1714560737f, 0.0261032350f,
            -0.1380184031f, -0.0502840857f, 0.0571697616f,
            0.0228323020f, -0.0010698190f, 0.0153014445f,
            -0.0074971801f, -0.0215069306f, -0.0000000000f,
            0.0043511039f, -0.0008679400f, 0.0055454604f
        ],
        [
            0.0057805625f, -0.0014606492f, 0.0009922673f,
            -0.0000000000f, -0.0186086716f, -0.0074727244f,
            0.0191503995f, 0.0044579572f, 0.0129815761f,
            0.0437393991f, -0.0428181058f, -0.1255184446f,
            0.0249825757f, 0.1717780923f, 0.0259158235f,
            -0.1356737669f, -0.0488192541f, 0.0544498636f,
            0.0207743812f, 0.0001339047f, 0.0162185070f,
            -0.0075313659f, -0.0209936125f, -0.0000000000f,
            0.0036429019f, -0.0010038446f, 0.0056380165f
        ],
        [
            0.0057779062f, -0.0013596057f, 0.0016250224f,
            -0.0000000000f, -0.0192494812f, -0.0075153228f,
            0.0185260945f, 0.0034514725f, 0.0148602401f,
            0.0463851642f, -0.0443356073f, -0.1281665489f,
            0.0252427573f, 0.1719714979f, 0.0257097299f,
            -0.1332478799f, -0.0473382128f, 0.0517436045f,
            0.0187584451f, 0.0012890418f, 0.0170610990f,
            -0.0075453392f, -0.0204445392f, -0.0000000000f,
            0.0029515844f, -0.0011311490f, 0.0057072444f
        ],
        [
            0.0057536787f, -0.0012497586f, 0.0022785282f,
            -0.0000000000f, -0.0198622989f, -0.0075397630f,
            0.0178299969f, 0.0023950480f, 0.0167864470f,
            0.0490542905f, -0.0458429870f, -0.1307442660f,
            0.0254852627f, 0.1720359988f, 0.0254852627f,
            -0.1307442660f, -0.0458429870f, 0.0490542905f,
            0.0167864469f, 0.0023950480f, 0.0178299969f,
            -0.0075397630f, -0.0198622989f, -0.0000000000f,
            0.0022785282f, -0.0012497586f, 0.0057536787f
        ],
        [
            0.0057072444f, -0.0011311490f, 0.0029515844f,
            -0.0000000000f, -0.0204445392f, -0.0075453391f,
            0.0170610990f, 0.0012890417f, 0.0187584451f,
            0.0517436045f, -0.0473382128f, -0.1332478799f,
            0.0257097299f, 0.1719714979f, 0.0252427573f,
            -0.1281665489f, -0.0443356073f, 0.0463851642f,
            0.0148602401f, 0.0034514725f, 0.0185260945f,
            -0.0075153228f, -0.0192494813f, -0.0000000000f,
            0.0016250224f, -0.0013596057f, 0.0057779062f
        ],
        [
            0.0056380165f, -0.0010038446f, 0.0036429019f,
            -0.0000000000f, -0.0209936125f, -0.0075313659f,
            0.0162185070f, 0.0001339047f, 0.0207743812f,
            0.0544498636f, -0.0488192541f, -0.1356737669f,
            0.0259158235f, 0.1717780922f, 0.0249825757f,
            -0.1255184446f, -0.0428181058f, 0.0437393991f,
            0.0129815761f, 0.0044579573f, 0.0191503995f,
            -0.0074727244f, -0.0186086716f, -0.0000000000f,
            0.0009922673f, -0.0014606492f, 0.0057805625f
        ],
        [
            0.0055454604f, -0.0008679400f, 0.0043511039f,
            -0.0000000000f, -0.0215069306f, -0.0074971801f,
            0.0153014445f, -0.0010698190f, 0.0228323020f,
            0.0571697616f, -0.0502840857f, -0.1380184031f,
            0.0261032350f, 0.1714560737f, 0.0247051052f,
            -0.1228037551f, -0.0412925125f, 0.0411200945f,
            0.0111521026f, 0.0054142368f, 0.0197040294f,
            -0.0074126923f, -0.0179424457f, -0.0000000000f,
            0.0003813733f, -0.0015528737f, 0.0057623291f
        ],
        [
            0.0054290958f, -0.0007235570f, 0.0050747268f,
            -0.0000000000f, -0.0219819122f, -0.0074421426f,
            0.0143092556f, -0.0023214914f, 0.0249301575f,
            0.0598999340f, -0.0517306914f, -0.1402783705f,
            0.0262716834f, 0.1710059279f, 0.0244107582f,
            -0.1200263609f, -0.0397608520f, 0.0385302708f,
            0.0093733612f, 0.0063201374f, 0.0201882078f,
            -0.0073359678f, -0.0172533639f, -0.0000000000f,
            -0.0002066393f, -0.0016362892f, 0.0057239309f
        ],
        [
            0.0052884997f, -0.0005708450f, 0.0058122222f,
            -0.0000000000f, -0.0224159887f, -0.0073656402f,
            0.0132414083f, -0.0036203811f, 0.0270658035f,
            0.0626369636f, -0.0531570676f, -0.1424503637f,
            0.0264209159f, 0.1704283334f, 0.0240999711f,
            -0.1171902140f, -0.0382251396f, 0.0359728644f,
            0.0076467862f, 0.0071755761f, 0.0206042605f,
            -0.0072433071f, -0.0165439665f, -0.0000000000f,
            -0.0007708413f, -0.0017109301f, 0.0056661325f
        ]
    ];

    private static readonly float[][] Rrc2400Imaginary =
    [
        [
            0.0028481125f, 0.0054685981f, -0.0002075460f,
            0.0066437543f, 0.0025051300f, -0.0219607484f,
            -0.0106763979f, 0.0109842976f, -0.0059737025f,
            0.0243033731f, 0.0720030336f, -0.0371381037f,
            -0.1500981012f, 0.0000000000f, 0.1676345743f,
            0.0469610323f, -0.1070824366f, -0.0474994516f,
            0.0292370043f, 0.0068346493f, 0.0061639827f,
            0.0223657936f, -0.0036122122f, -0.0160139262f,
            -0.0010393122f, -0.0012617936f, -0.0026104564f
        ],
        [
            0.0028870387f, 0.0052657015f, -0.0001220893f,
            0.0074134957f, 0.0026203069f, -0.0222926070f,
            -0.0104983951f, 0.0098763332f, -0.0076467862f,
            0.0261358159f, 0.0750210606f, -0.0380774087f,
            -0.1521612293f, 0.0000000000f, 0.1668150977f,
            0.0462849289f, -0.1043266192f, -0.0455084179f,
            0.0270658034f, 0.0049830270f, 0.0067468345f,
            0.0226691095f, -0.0035503438f, -0.0152621555f,
            -0.0009205656f, -0.0017568803f, -0.0026946252f
        ],
        [
            0.0029164885f, 0.0050359802f, -0.0000327285f,
            0.0081920826f, 0.0027326644f, -0.0225777872f,
            -0.0102864056f, 0.0086989229f, -0.0093733612f,
            0.0279938804f, 0.0780350658f, -0.0389989287f,
            -0.1541234615f, 0.0000000000f, 0.1658728807f,
            0.0455792055f, -0.1015271984f, -0.0435198495f,
            0.0249301575f, 0.0031952587f, 0.0072909299f,
            0.0229045598f, -0.0034815929f, -0.0144973567f,
            -0.0008037578f, -0.0022268794f, -0.0027662625f
        ],
        [
            0.0029360533f, 0.0047792539f, 0.0000604036f,
            0.0089776235f, 0.0028418042f, -0.0228139211f,
            -0.0100397044f, 0.0074520577f, -0.0111521026f,
            0.0298754975f, 0.0810411188f, -0.0399013588f,
            -0.1559818953f, 0.0000000000f, 0.1648093392f,
            0.0448448976f, -0.0986880748f, -0.0415362631f,
            0.0228323020f, 0.0014724795f, 0.0077964754f,
            0.0230739477f, -0.0034063632f, -0.0137219685f,
            -0.0006891472f, -0.0026712446f, -0.0028255532f
        ],
        [
            0.0029453437f, 0.0044954161f, 0.0001571597f,
            0.0097681524f, 0.0029473240f, -0.0229986810f,
            -0.0097576159f, 0.0061358518f, -0.0129815761f,
            0.0317785336f, 0.0840352642f, -0.0407834149f,
            -0.1577337751f, 0.0000000000f, 0.1636260696f,
            0.0440830791f, -0.0958131808f, -0.0395601416f,
            0.0207743811f, -0.0001843040f, 0.0082637421f,
            0.0231791608f, -0.0033250616f, -0.0129383848f,
            -0.0005769790f, -0.0030895160f, -0.0028727129f
        ],
        [
            0.0029439903f, 0.0041844362f, 0.0002573783f,
            0.0105616315f, 0.0030488183f, -0.0231297854f,
            -0.0094395166f, 0.0047505443f, -0.0148602401f,
            0.0337007945f, 0.0870135287f, -0.0416438361f,
            -0.1593764973f, 0.0000000000f, 0.1623248458f,
            0.0432948607f, -0.0929064737f, -0.0375939292f,
            0.0187584451f, -0.0017742138f, 0.0086930641f,
            0.0232221661f, -0.0032380969f, -0.0121489499f,
            -0.0004674850f, -0.0034813185f, -0.0029079863f
        ],
        [
            0.0029316457f, 0.0038463613f, 0.0003608834f,
            0.0113559544f, 0.0031458791f, -0.0232050045f,
            -0.0090848372f, 0.0032965008f, -0.0167864469f,
            0.0356400282f, 0.0899719279f, -0.0424813872f,
            -0.1609076156f, 0.0000000000f, 0.1609076156f,
            0.0424813872f, -0.0899719279f, -0.0356400282f,
            0.0167864469f, -0.0032965008f, 0.0090848372f,
            0.0232050045f, -0.0031458791f, -0.0113559545f,
            -0.0003608834f, -0.0038463613f, -0.0029316457f
        ],
        [
            0.0029079863f, 0.0034813185f, 0.0004674850f,
            0.0121489499f, 0.0032380969f, -0.0232221661f,
            -0.0086930641f, 0.0017742138f, -0.0187584451f,
            0.0375939292f, 0.0929064737f, -0.0432948607f,
            -0.1623248458f, 0.0000000000f, 0.1593764973f,
            0.0416438361f, -0.0870135287f, -0.0337007945f,
            0.0148602401f, -0.0047505443f, 0.0094395166f,
            0.0231297854f, -0.0030488183f, -0.0105616315f,
            -0.0002573783f, -0.0041844362f, -0.0029439903f
        ],
        [
            0.0028727129f, 0.0030895160f, 0.0005769790f,
            0.0129383848f, 0.0033250616f, -0.0231791608f,
            -0.0082637421f, 0.0001843039f, -0.0207743812f,
            0.0395601416f, 0.0958131809f, -0.0440830791f,
            -0.1636260696f, 0.0000000000f, 0.1577337751f,
            0.0407834149f, -0.0840352642f, -0.0317785336f,
            0.0129815761f, -0.0061358518f, 0.0097576159f,
            0.0229986810f, -0.0029473240f, -0.0097681524f,
            -0.0001571597f, -0.0044954161f, -0.0029453437f
        ],
        [
            0.0028255532f, 0.0026712446f, 0.0006891472f,
            0.0137219685f, 0.0034063632f, -0.0230739476f,
            -0.0077964754f, -0.0014724795f, -0.0228323020f,
            0.0415362631f, 0.0986880748f, -0.0448448976f,
            -0.1648093392f, 0.0000000000f, 0.1559818953f,
            0.0399013588f, -0.0810411187f, -0.0298754974f,
            0.0111521026f, -0.0074520577f, 0.0100397044f,
            0.0228139211f, -0.0028418042f, -0.0089776235f,
            -0.0000604036f, -0.0047792539f, -0.0029360533f
        ],
        [
            0.0027662625f, 0.0022268794f, 0.0008037578f,
            0.0144973567f, 0.0034815928f, -0.0229045598f,
            -0.0072909299f, -0.0031952587f, -0.0249301575f,
            0.0435198495f, 0.1015271984f, -0.0455792055f,
            -0.1658728807f, 0.0000000000f, 0.1541234615f,
            0.0389989287f, -0.0780350658f, -0.0279938804f,
            0.0093733612f, -0.0086989229f, 0.0102864057f,
            0.0225777872f, -0.0027326644f, -0.0081920826f,
            0.0000327285f, -0.0050359802f, -0.0029164885f
        ],
        [
            0.0026946252f, 0.0017568804f, 0.0009205656f,
            0.0152621555f, 0.0035503438f, -0.0226691095f,
            -0.0067468345f, -0.0049830270f, -0.0270658035f,
            0.0455084179f, 0.1043266192f, -0.0462849289f,
            -0.1668150977f, 0.0000000000f, 0.1521612292f,
            0.0380774087f, -0.0750210606f, -0.0261358159f,
            0.0076467861f, -0.0098763332f, 0.0104983951f,
            0.0222926070f, -0.0026203069f, -0.0074134957f,
            0.0001220893f, -0.0052657015f, -0.0028870387f
        ]
    ];

    public static bool IsValidBitRate(int bitRate) =>
        bitRate is 2400 or 4800;

    public static V27TerRxState? Initialize(
        V27TerRxState? state,
        int bitRate,
        V27TerRxPutBitDelegate? putBit,
        object? userData) {
        if (!IsValidBitRate(bitRate))
            return null;

        if (state is null || state.IsDisposed)
            state = new V27TerRxState();

        state.ResetForInitialization();
        state.PutBit = putBit;
        state.PutBitUserData = userData;
        SetSignalCutoff(state, -45.5f);
        Restart(state, bitRate, oldTraining: false);
        return state;
    }

    public static int Restart(
        V27TerRxState state,
        int bitRate,
        bool oldTraining) {
        ValidateState(state);

        state.Logging.Flow("Restarting V.27ter");

        if (!IsValidBitRate(bitRate))
            return -1;

        state.BitRate = bitRate;
        state.OldTraining = oldTraining;

        Array.Clear(state.RrcFilter);
        state.TrainingError = 0.0f;
        state.RrcFilterStep = 0;

        state.ScrambleRegister = 0x3Cu;
        state.ScramblerPatternCount = 0;
        state.TrainingStage =
            V27TerRxTrainingStage.SymbolAcquisition;

        state.TrainingBc = 0;
        state.TrainingCount = 0;
        state.SignalPresent = 0;
        state.HighSample = 0;
        state.LowSamples = 0;
        state.CarrierDropPending = false;
        Array.Clear(state.DifferenceAngles);

        state.CarrierPhase = 0;
        state.CarrierTrackIntegral = 200000.0f;
        state.CarrierTrackProportional = 10000000.0f;
        state.PowerShift = 4;
        state.PowerReading = 0;
        state.ConstellationState = 0;

        if (oldTraining) {
            state.CarrierPhaseRate =
                state.SavedCarrierPhaseRate;

            state.AgcScaling =
                state.SavedAgcScaling;

            RestoreEqualizer(state);
        } else {
            state.CarrierPhaseRate =
                DdsPhaseRate(CarrierNominalFrequency);

            state.AgcScaling =
                (1.414f / RrcGain4800) / 283.0f;

            ResetEqualizer(state);
        }

        state.EqualizerSkip = 0;
        state.LastSample = 0;
        state.GardnerIntegrate = 0;
        state.TotalBaudTimingCorrection = 0;
        state.GardnerStep = 512;
        state.BaudHalf = 0;
        return 0;
    }

    public static int Receive(
        V27TerRxState state,
        ReadOnlySpan<short> samples) {
        ValidateState(state);

        if (state.BitRate == 4800) {
            ReceiveWithFilter(
                state,
                samples,
                Rrc4800Real,
                Rrc4800Imaginary,
                RrcCoefficientSets4800,
                RrcGain4800,
                equalizerIncrement: RrcCoefficientSets4800 * 5 / 2);
        } else {
            ReceiveWithFilter(
                state,
                samples,
                Rrc2400Real,
                Rrc2400Imaginary,
                RrcCoefficientSets2400,
                RrcGain2400,
                equalizerIncrement:
                    RrcCoefficientSets2400 * 20 / (3 * 2));
        }

        return 0;
    }

    public static int Receive(
        V27TerRxState state,
        short[] samples,
        int length) {
        ArgumentNullException.ThrowIfNull(samples);

        if ((uint)length > (uint)samples.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        return Receive(
            state,
            samples.AsSpan(0, length));
    }

    public static int FillIn(
        V27TerRxState state,
        int sampleCount) {
        ValidateState(state);

        if (sampleCount < 0)
            throw new ArgumentOutOfRangeException(nameof(sampleCount));

        state.Logging.Flow($"Fill-in {sampleCount} samples");

        if (state.SignalPresent <= 0 ||
            state.TrainingStage ==
                V27TerRxTrainingStage.Parked) {
            return 0;
        }

        for (int index = 0; index < sampleCount; index++) {
            AdvanceCarrier(state);

            if (state.BitRate == 4800) {
                state.EqualizerPutStep -=
                    RrcCoefficientSets4800;

                if (state.EqualizerPutStep <= 0) {
                    state.EqualizerPutStep +=
                        RrcCoefficientSets4800 * 5 / 2;
                }
            } else {
                state.EqualizerPutStep -=
                    RrcCoefficientSets2400;

                if (state.EqualizerPutStep <= 0) {
                    state.EqualizerPutStep +=
                        RrcCoefficientSets2400 *
                        20 /
                        (3 * 2);
                }
            }
        }

        return 0;
    }

    public static int GetEqualizerState(
        V27TerRxState state,
        out ReadOnlyMemory<V27TerRxComplex> coefficients) {
        ValidateState(state);
        coefficients = state.EqualizerCoefficients;
        return EqualizerLength;
    }

    public static float GetCarrierFrequency(
        V27TerRxState state) {
        ValidateState(state);
        return DdsFrequency(state.CarrierPhaseRate);
    }

    public static float GetSymbolTimingCorrection(
        V27TerRxState state) {
        ValidateState(state);

        int stepsPerSymbol =
            state.BitRate == 4800
                ? RrcCoefficientSets4800 * 5
                : RrcCoefficientSets2400 * 20 / 3;

        return (float)state.TotalBaudTimingCorrection /
               stepsPerSymbol;
    }

    public static float GetSignalPower(
        V27TerRxState state) {
        ValidateState(state);
        return PowerMeterCurrentDbm0(state.PowerReading) + 3.98f;
    }

    public static void SetSignalCutoff(
        V27TerRxState state,
        float cutoffDbm0) {
        ValidateState(state);

        state.CarrierOnPower =
            (int)(PowerMeterLevelDbm0(
                cutoffDbm0 + 2.5f) * 0.4f);

        state.CarrierOffPower =
            (int)(PowerMeterLevelDbm0(
                cutoffDbm0 - 2.5f) * 0.4f);
    }

    public static void SetPutBit(
        V27TerRxState state,
        V27TerRxPutBitDelegate? putBit,
        object? userData) {
        ValidateState(state);
        state.PutBit = putBit;
        state.PutBitUserData = userData;
    }

    public static void SetModemStatusHandler(
        V27TerRxState state,
        V27TerRxModemStatusDelegate? handler,
        object? userData) {
        ValidateState(state);
        state.StatusHandler = handler;
        state.StatusUserData = userData;
    }

    public static void SetQamReportHandler(
        V27TerRxState state,
        V27TerRxQamReportDelegate? handler,
        object? userData) {
        ValidateState(state);
        state.QamReport = handler;
        state.QamUserData = userData;
    }

    public static V27TerRxLog GetLoggingState(
        V27TerRxState state) {
        ValidateState(state);
        return state.Logging;
    }

    public static int Release(V27TerRxState state) {
        ArgumentNullException.ThrowIfNull(state);
        return 0;
    }

    public static int Free(V27TerRxState? state) {
        state?.Dispose();
        return 0;
    }

    private static void ReceiveWithFilter(
        V27TerRxState state,
        ReadOnlySpan<short> samples,
        float[][] realFilter,
        float[][] imaginaryFilter,
        int coefficientSets,
        float filterGain,
        int equalizerIncrement) {
        foreach (short amplitude in samples) {
            state.RrcFilter[state.RrcFilterStep] =
                amplitude;

            state.RrcFilterStep++;

            if (state.RrcFilterStep >= FilterSteps)
                state.RrcFilterStep = 0;

            int power = SignalDetect(state, amplitude);

            if (power == 0)
                continue;

            if (state.TrainingStage ==
                V27TerRxTrainingStage.Parked) {
                continue;
            }

            state.EqualizerPutStep -= coefficientSets;

            if (state.EqualizerPutStep <= 0) {
                if (state.TrainingStage ==
                    V27TerRxTrainingStage.SymbolAcquisition) {
                    int rootPower = IntegerSquareRoot(power);

                    if (rootPower == 0)
                        rootPower = 1;

                    state.AgcScaling =
                        (1.414f / filterGain) /
                        rootPower;
                }

                int filterIndex =
                    -state.EqualizerPutStep;

                if (filterIndex > coefficientSets - 1)
                    filterIndex = coefficientSets - 1;

                float real =
                    CircularDot(
                        state.RrcFilter,
                        realFilter[filterIndex],
                        state.RrcFilterStep) *
                    state.AgcScaling;

                float imaginary =
                    CircularDot(
                        state.RrcFilter,
                        imaginaryFilter[filterIndex],
                        state.RrcFilterStep) *
                    state.AgcScaling;

                V27TerRxComplex carrier =
                    DdsLookupComplex(state.CarrierPhase);

                V27TerRxComplex baseband = new(
                    real * carrier.Real -
                    imaginary * carrier.Imaginary,
                    -real * carrier.Imaginary -
                    imaginary * carrier.Real);

                state.EqualizerPutStep +=
                    equalizerIncrement;

                ProcessHalfBaud(
                    state,
                    baseband);
            }

            AdvanceCarrier(state);
        }
    }

    private static void ReportStatusChange(
        V27TerRxState state,
        int status) {
        if (state.StatusHandler is not null) {
            state.StatusHandler(
                state.StatusUserData,
                status);
        } else {
            state.PutBit?.Invoke(
                state.PutBitUserData,
                status);
        }
    }

    private static void SaveEqualizer(
        V27TerRxState state) {
        Array.Copy(
            state.EqualizerCoefficients,
            state.SavedEqualizerCoefficients,
            EqualizerLength);
    }

    private static void RestoreEqualizer(
        V27TerRxState state) {
        Array.Copy(
            state.SavedEqualizerCoefficients,
            state.EqualizerCoefficients,
            EqualizerLength);

        Array.Clear(state.EqualizerBuffer);

        state.EqualizerDelta =
            EqualizerAdaptationRate /
            EqualizerLength;

        state.EqualizerPutStep =
            state.BitRate == 4800
                ? RrcCoefficientSets4800 * 5 / 2 - 1
                : RrcCoefficientSets2400 *
                  20 /
                  (3 * 2) -
                  1;

        state.EqualizerStep = 0;
    }

    private static void ResetEqualizer(
        V27TerRxState state) {
        Array.Clear(state.EqualizerCoefficients);
        Array.Clear(state.EqualizerBuffer);

        state.EqualizerCoefficients[
            EqualizerPreLength + 1] =
            new V27TerRxComplex(1.414f, 0.0f);

        state.EqualizerDelta =
            EqualizerAdaptationRate /
            EqualizerLength;

        state.EqualizerPutStep =
            state.BitRate == 4800
                ? RrcCoefficientSets4800 * 5 / 2
                : RrcCoefficientSets2400 *
                  20 /
                  (3 * 2);

        state.EqualizerStep = 0;
    }

    private static V27TerRxComplex GetEqualizedSymbol(
        V27TerRxState state) {
        return CircularComplexDot(
            state.EqualizerBuffer,
            state.EqualizerCoefficients,
            state.EqualizerStep);
    }

    private static void TuneEqualizer(
        V27TerRxState state,
        in V27TerRxComplex symbol,
        in V27TerRxComplex target) {
        V27TerRxComplex error = new(
            (target.Real - symbol.Real) *
            state.EqualizerDelta,
            (target.Imaginary - symbol.Imaginary) *
            state.EqualizerDelta);

        int position = state.EqualizerStep;

        for (int coefficient = 0;
             coefficient < EqualizerLength;
             coefficient++) {
            V27TerRxComplex input =
                state.EqualizerBuffer[position];

            V27TerRxComplex current =
                state.EqualizerCoefficients[coefficient];

            state.EqualizerCoefficients[coefficient] =
                new V27TerRxComplex(
                    current.Real * LmsLeakRate +
                    input.Imaginary * error.Imaginary +
                    input.Real * error.Real,
                    current.Imaginary * LmsLeakRate +
                    input.Real * error.Imaginary -
                    input.Imaginary * error.Real);

            position++;

            if (position >= EqualizerLength)
                position = 0;
        }
    }

    private static void TrackCarrier(
        V27TerRxState state,
        in V27TerRxComplex symbol,
        in V27TerRxComplex target) {
        float error =
            symbol.Imaginary * target.Real -
            symbol.Real * target.Imaginary;

        state.CarrierPhaseRate =
            unchecked(
                state.CarrierPhaseRate +
                (int)(state.CarrierTrackIntegral *
                      error));

        state.CarrierPhase =
            unchecked(
                state.CarrierPhase +
                (uint)(int)(
                    state.CarrierTrackProportional *
                    error));
    }

    private static int FindQuadrant(
        in V27TerRxComplex symbol) {
        int first =
            symbol.Imaginary > symbol.Real
                ? 1
                : 0;

        int second =
            symbol.Imaginary < -symbol.Real
                ? 1
                : 0;

        return (second << 1) |
               (first ^ second);
    }

    private static int FindOctant(
        in V27TerRxComplex symbol) {
        float absoluteReal =
            MathF.Abs(symbol.Real);

        float absoluteImaginary =
            MathF.Abs(symbol.Imaginary);

        int first;
        int second;

        if (absoluteImaginary >
                absoluteReal * 0.4142136f &&
            absoluteImaginary <
                absoluteReal * 2.4142136f) {
            first = symbol.Real < 0.0f ? 1 : 0;
            second = symbol.Imaginary < 0.0f ? 1 : 0;

            return (second << 2) |
                   ((first ^ second) << 1) |
                   1;
        }

        first =
            symbol.Imaginary > symbol.Real
                ? 1
                : 0;

        second =
            symbol.Imaginary < -symbol.Real
                ? 1
                : 0;

        return (second << 2) |
               ((first ^ second) << 1);
    }

    private static int Descramble(
        V27TerRxState state,
        int inputBit) {
        inputBit &= 1;

        int outputBit =
            (inputBit ^
             (int)(state.ScrambleRegister >> 5) ^
             (int)(state.ScrambleRegister >> 6)) &
            1;

        if (state.ScramblerPatternCount >= 33) {
            outputBit ^= 1;
            state.ScramblerPatternCount = 0;
        } else if ((int)state.TrainingStage >
                       (int)V27TerRxTrainingStage.NormalOperation &&
                   (int)state.TrainingStage <
                       (int)V27TerRxTrainingStage.TestOnes) {
            state.ScramblerPatternCount = 0;
        } else {
            uint bit = (uint)inputBit;

            bool repeatedPattern =
                ((((state.ScrambleRegister >> 7) ^
                   bit) &
                  ((state.ScrambleRegister >> 8) ^
                   bit) &
                  ((state.ScrambleRegister >> 11) ^
                   bit) &
                  1u) != 0);

            if (repeatedPattern)
                state.ScramblerPatternCount = 0;
            else
                state.ScramblerPatternCount++;
        }

        state.ScrambleRegister <<= 1;

        if ((int)state.TrainingStage >
                (int)V27TerRxTrainingStage.NormalOperation &&
            (int)state.TrainingStage <
                (int)V27TerRxTrainingStage.TestOnes) {
            state.ScrambleRegister |=
                (uint)outputBit;
        } else {
            state.ScrambleRegister |=
                (uint)inputBit;
        }

        return outputBit;
    }

    private static void PutDecodedBit(
        V27TerRxState state,
        int bit) {
        int outputBit =
            Descramble(state, bit);

        if (state.TrainingStage ==
            V27TerRxTrainingStage.NormalOperation) {
            state.PutBit?.Invoke(
                state.PutBitUserData,
                outputBit);
        }
    }

    private static void DecodeBaud(
        V27TerRxState state,
        in V27TerRxComplex symbol) {
        ReadOnlySpan<byte> phaseSteps4800 =
        [
            4, 0, 2, 6, 7, 3, 1, 5
        ];

        ReadOnlySpan<byte> phaseSteps2400 =
        [
            0, 2, 3, 1
        ];

        int nearest;
        int rawBits;
        int targetIndex;

        if (state.BitRate == 2400) {
            nearest = FindQuadrant(symbol);

            rawBits =
                phaseSteps2400[
                    (nearest -
                     state.ConstellationState) &
                    3];

            PutDecodedBit(state, rawBits);
            PutDecodedBit(state, rawBits >> 1);

            state.ConstellationState = nearest;
            targetIndex = nearest << 1;
        } else {
            nearest = FindOctant(symbol);

            rawBits =
                phaseSteps4800[
                    (nearest -
                     state.ConstellationState) &
                    7];

            PutDecodedBit(state, rawBits);
            PutDecodedBit(state, rawBits >> 1);
            PutDecodedBit(state, rawBits >> 2);

            state.ConstellationState = nearest;
            targetIndex = nearest;
        }

        V27TerRxComplex target =
            Constellation[targetIndex];

        TrackCarrier(
            state,
            symbol,
            target);

        state.EqualizerSkip--;

        if (state.EqualizerSkip <= 0) {
            state.EqualizerSkip = 100;

            TuneEqualizer(
                state,
                symbol,
                target);
        }
    }

    private static void SymbolSynchronize(
        V27TerRxState state) {
        int mask = EqualizerLength - 1;

        float realDifference =
            state.EqualizerBuffer[
                (state.EqualizerStep - 3) &
                mask].Real -
            state.EqualizerBuffer[
                (state.EqualizerStep - 1) &
                mask].Real;

        float realTest =
            realDifference *
            state.EqualizerBuffer[
                (state.EqualizerStep - 2) &
                mask].Real;

        float imaginaryDifference =
            state.EqualizerBuffer[
                (state.EqualizerStep - 3) &
                mask].Imaginary -
            state.EqualizerBuffer[
                (state.EqualizerStep - 1) &
                mask].Imaginary;

        float imaginaryTest =
            imaginaryDifference *
            state.EqualizerBuffer[
                (state.EqualizerStep - 2) &
                mask].Imaginary;

        state.GardnerIntegrate +=
            realTest + imaginaryTest > 0.0f
                ? state.GardnerStep
                : -state.GardnerStep;

        if (Math.Abs(state.GardnerIntegrate) >= 128) {
            int correction =
                state.GardnerIntegrate / 128;

            state.EqualizerPutStep +=
                correction;

            state.TotalBaudTimingCorrection +=
                correction;

            state.QamReport?.Invoke(
                state.QamUserData,
                null,
                null,
                state.GardnerIntegrate);

            state.GardnerIntegrate = 0;
        }
    }

    private static void ProcessHalfBaud(
        V27TerRxState state,
        in V27TerRxComplex sample) {
        ReadOnlySpan<int> ababPositions = [0, 4];

        state.EqualizerBuffer[
            state.EqualizerStep] = sample;

        state.EqualizerStep++;

        if (state.EqualizerStep >= EqualizerLength)
            state.EqualizerStep = 0;

        state.BaudHalf ^= 1;

        if (state.BaudHalf != 0)
            return;

        SymbolSynchronize(state);

        V27TerRxComplex symbol =
            GetEqualizedSymbol(state);

        V27TerRxComplex target = Zero;

        switch (state.TrainingStage) {
            case V27TerRxTrainingStage.NormalOperation: {
                    DecodeBaud(state, symbol);

                    int targetIndex =
                        state.BitRate == 4800
                            ? state.ConstellationState
                            : state.ConstellationState << 1;

                    target = Constellation[targetIndex];
                    break;
                }

            case V27TerRxTrainingStage.SymbolAcquisition:
                state.TrainingCount++;

                if (state.TrainingCount >= 30) {
                    state.GardnerStep = 32;
                    state.TrainingStage =
                        V27TerRxTrainingStage.LogPhase;

                    Array.Clear(
                        state.DifferenceAngles);

                    state.LastAngles[0] =
                        ApproximateArctan2(
                            symbol.Imaginary,
                            symbol.Real);
                }

                break;

            case V27TerRxTrainingStage.LogPhase:
                state.LastAngles[1] =
                    ApproximateArctan2(
                        symbol.Imaginary,
                        symbol.Real);

                state.TrainingCount = 1;
                state.TrainingStage =
                    V27TerRxTrainingStage.WaitForHop;

                break;

            case V27TerRxTrainingStage.WaitForHop: {
                    int angle =
                        ApproximateArctan2(
                            symbol.Imaginary,
                            symbol.Real);

                    int historyIndex =
                        state.TrainingCount + 1;

                    int angleDifference =
                        unchecked(
                            angle -
                            state.LastAngles[
                                historyIndex & 1]);

                    state.LastAngles[
                        historyIndex & 1] = angle;

                    state.DifferenceAngles[
                        historyIndex & 0x0F] =
                        unchecked(
                            state.DifferenceAngles[
                                (historyIndex - 2) &
                                0x0F] +
                            (angleDifference >> 4));

                    bool phaseReversal =
                        angleDifference >
                            DdsPhaseDegrees(45.0f) ||
                        angleDifference <
                            DdsPhaseDegrees(-45.0f);

                    if (phaseReversal &&
                        state.TrainingCount >= 13) {
                        int averagedSymbols =
                            (state.TrainingCount - 8) &
                            ~1;

                        if (averagedSymbols > 1) {
                            int index =
                                averagedSymbols & 0x0F;

                            int averageDifference =
                                (state.DifferenceAngles[index] +
                                 state.DifferenceAngles[
                                     index | 1]) /
                                (averagedSymbols - 1);

                            if (state.BitRate == 4800) {
                                state.CarrierPhaseRate =
                                    unchecked(
                                        state.CarrierPhaseRate +
                                        16 *
                                        (averageDifference /
                                         10));
                            } else {
                                state.CarrierPhaseRate =
                                    unchecked(
                                        state.CarrierPhaseRate +
                                        3 *
                                        16 *
                                        (averageDifference /
                                         40));
                            }
                        }

                        state.Logging.Flow(
                            $"Coarse carrier frequency " +
                            $"{DdsFrequency(state.CarrierPhaseRate):F2} " +
                            $"({state.TrainingCount})");

                        if (state.CarrierPhaseRate <
                                DdsPhaseRate(
                                    CarrierNominalFrequency -
                                    20.0f) ||
                            state.CarrierPhaseRate >
                                DdsPhaseRate(
                                    CarrierNominalFrequency +
                                    20.0f)) {
                            state.Logging.Flow(
                                "Training failed " +
                                "(sequence failed)");

                            state.TrainingStage =
                                V27TerRxTrainingStage.Parked;

                            ReportStatusChange(
                                state,
                                (int)V27TerRxSignalStatus
                                    .TrainingFailed);

                            break;
                        }

                        angle = unchecked(
                            angle +
                            DdsPhaseDegrees(180.0f));

                        float radians =
                            DdsPhaseToRadians(
                                unchecked((uint)angle));

                        V27TerRxComplex rotation =
                            new(
                                MathF.Cos(radians),
                                -MathF.Sin(radians));

                        for (int index = 0;
                             index < EqualizerLength;
                             index++) {
                            state.EqualizerBuffer[index] =
                                Multiply(
                                    state.EqualizerBuffer[index],
                                    rotation);
                        }

                        state.CarrierPhase =
                            unchecked(
                                state.CarrierPhase +
                                (uint)angle);

                        state.GardnerStep = 2;
                        state.TrainingBc = 1;
                        state.TrainingBc ^=
                            Descramble(state, 1);

                        _ = Descramble(state, 1);
                        _ = Descramble(state, 1);

                        state.ConstellationState =
                            ababPositions[state.TrainingBc];

                        target =
                            Constellation[
                                state.ConstellationState];

                        state.TrainingCount = 1;
                        state.TrainingStage =
                            V27TerRxTrainingStage
                                .TrainOnAbab;

                        ReportStatusChange(
                            state,
                            (int)V27TerRxSignalStatus
                                .TrainingInProgress);
                    } else {
                        state.TrainingCount++;

                        if (state.TrainingCount >
                            TrainingSegment3Length) {
                            state.Logging.Flow(
                                "Training failed " +
                                "(sequence failed)");

                            state.TrainingStage =
                                V27TerRxTrainingStage.Parked;

                            ReportStatusChange(
                                state,
                                (int)V27TerRxSignalStatus
                                    .TrainingFailed);
                        }
                    }

                    break;
                }

            case V27TerRxTrainingStage.TrainOnAbab:
                state.TrainingBc ^=
                    Descramble(state, 1);

                _ = Descramble(state, 1);
                _ = Descramble(state, 1);

                state.ConstellationState =
                    ababPositions[state.TrainingBc];

                target =
                    Constellation[
                        state.ConstellationState];

                TrackCarrier(
                    state,
                    symbol,
                    target);

                TuneEqualizer(
                    state,
                    symbol,
                    target);

                float remaining =
                    (float)(
                        TrainingSegment5Length -
                        state.TrainingCount) /
                    TrainingSegment5Length;

                state.CarrierTrackIntegral =
                    400.0f +
                    (200000.0f - 400.0f) *
                    remaining;

                state.CarrierTrackProportional =
                    1000000.0f +
                    (10000000.0f -
                     1000000.0f) *
                    remaining;

                state.TrainingCount++;

                if (state.TrainingCount >=
                    TrainingSegment5Length) {
                    state.ConstellationState =
                        state.BitRate == 4800
                            ? 4
                            : 2;

                    state.TrainingCount = 0;
                    state.TrainingStage =
                        V27TerRxTrainingStage.TestOnes;
                }

                break;

            case V27TerRxTrainingStage.TestOnes: {
                    DecodeBaud(state, symbol);

                    int targetIndex =
                        state.BitRate == 4800
                            ? state.ConstellationState
                            : state.ConstellationState << 1;

                    target = Constellation[targetIndex];

                    V27TerRxComplex error =
                        Subtract(symbol, target);

                    state.TrainingError +=
                        Power(error);

                    state.TrainingCount++;

                    if (state.TrainingCount >=
                        TrainingSegment6Length) {
                        float maximumError =
                            state.BitRate == 4800
                                ? TrainingSegment6Length *
                                  0.25f
                                : TrainingSegment6Length *
                                  0.5f;

                        if (state.TrainingError <
                            maximumError) {
                            state.Logging.Flow(
                                $"Training succeeded at " +
                                $"{state.BitRate}bps " +
                                $"(constellation mismatch " +
                                $"{state.TrainingError})");

                            ReportStatusChange(
                                state,
                                (int)V27TerRxSignalStatus
                                    .TrainingSucceeded);

                            state.SignalPresent =
                                state.BitRate == 4800
                                    ? 90
                                    : 120;

                            state.TrainingStage =
                                V27TerRxTrainingStage
                                    .NormalOperation;

                            SaveEqualizer(state);

                            state.SavedCarrierPhaseRate =
                                state.CarrierPhaseRate;

                            state.SavedAgcScaling =
                                state.AgcScaling;
                        } else {
                            state.Logging.Flow(
                                "Training failed " +
                                "(constellation mismatch " +
                                $"{state.TrainingError})");

                            state.TrainingStage =
                                V27TerRxTrainingStage.Parked;

                            ReportStatusChange(
                                state,
                                (int)V27TerRxSignalStatus
                                    .TrainingFailed);
                        }
                    }

                    break;
                }

            case V27TerRxTrainingStage.Parked:
            default:
                target = Zero;
                break;
        }

        state.QamReport?.Invoke(
            state.QamUserData,
            symbol,
            target,
            state.ConstellationState);
    }

    private static int SignalDetect(
        V27TerRxState state,
        short amplitude) {
        short halfAmplitude =
            unchecked((short)(amplitude >> 1));

        short difference =
            unchecked((short)(
                halfAmplitude -
                state.LastSample));

        state.LastSample = halfAmplitude;

        int power =
            PowerMeterUpdate(
                state,
                difference);

        int magnitude =
            Math.Abs((int)difference);

        if (10 * magnitude <
            state.HighSample) {
            state.LowSamples++;

            if (state.LowSamples > 120) {
                state.PowerReading = 0;
                state.PowerShift = 4;
                state.HighSample = 0;
                state.LowSamples = 0;
                power = 0;
            }
        } else {
            state.LowSamples = 0;

            if (magnitude > state.HighSample) {
                state.HighSample =
                    unchecked((short)magnitude);
            }
        }

        if (state.SignalPresent > 0) {
            if (state.CarrierDropPending ||
                power < state.CarrierOffPower) {
                state.SignalPresent--;

                if (state.SignalPresent <= 0) {
                    Restart(
                        state,
                        state.BitRate,
                        oldTraining: false);

                    ReportStatusChange(
                        state,
                        (int)V27TerRxSignalStatus
                            .CarrierDown);

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
                (int)V27TerRxSignalStatus
                    .CarrierUp);
        }

        return power;
    }

    private static int PowerMeterUpdate(
        V27TerRxState state,
        short amplitude) {
        int square =
            amplitude * amplitude;

        state.PowerReading =
            unchecked(
                state.PowerReading +
                ((square -
                  state.PowerReading) >>
                 state.PowerShift));

        return state.PowerReading;
    }

    private static int PowerMeterLevelDbm0(
        float level) {
        level -= Dbm0MaximumPower;

        if (level > 0.0f)
            level = 0.0f;

        double ratio =
            Math.Pow(10.0, level / 10.0);

        return (int)(
            ratio *
            32767.0 *
            32767.0);
    }

    private static float PowerMeterCurrentDbm0(
        int reading) {
        if (reading <= 0)
            return -96.329f + Dbm0MaximumPower;

        return 10.0f *
               MathF.Log10(
                   reading /
                   (32767.0f * 32767.0f) +
                   1.0e-10f) +
               Dbm0MaximumPower;
    }

    private static float CircularDot(
        float[] samples,
        float[] coefficients,
        int position) {
        float result = 0.0f;
        int sampleIndex = position;

        for (int index = 0;
             index < coefficients.Length;
             index++) {
            result +=
                samples[sampleIndex] *
                coefficients[index];

            sampleIndex++;

            if (sampleIndex >= samples.Length)
                sampleIndex = 0;
        }

        return result;
    }

    private static V27TerRxComplex CircularComplexDot(
        V27TerRxComplex[] samples,
        V27TerRxComplex[] coefficients,
        int position) {
        float real = 0.0f;
        float imaginary = 0.0f;
        int sampleIndex = position;

        for (int index = 0;
             index < coefficients.Length;
             index++) {
            V27TerRxComplex sample =
                samples[sampleIndex];

            V27TerRxComplex coefficient =
                coefficients[index];

            real +=
                sample.Real * coefficient.Real -
                sample.Imaginary *
                coefficient.Imaginary;

            imaginary +=
                sample.Real *
                coefficient.Imaginary +
                sample.Imaginary *
                coefficient.Real;

            sampleIndex++;

            if (sampleIndex >= samples.Length)
                sampleIndex = 0;
        }

        return new V27TerRxComplex(
            real,
            imaginary);
    }

    private static V27TerRxComplex Multiply(
        in V27TerRxComplex first,
        in V27TerRxComplex second) {
        return new V27TerRxComplex(
            first.Real * second.Real -
            first.Imaginary *
            second.Imaginary,
            first.Real * second.Imaginary +
            first.Imaginary * second.Real);
    }

    private static V27TerRxComplex Subtract(
        in V27TerRxComplex first,
        in V27TerRxComplex second) {
        return new V27TerRxComplex(
            first.Real - second.Real,
            first.Imaginary -
            second.Imaginary);
    }

    private static float Power(
        in V27TerRxComplex value) {
        return value.Real * value.Real +
               value.Imaginary *
               value.Imaginary;
    }

    private static int ApproximateArctan2(
        float y,
        float x) {
        if (y == 0.0f) {
            return x < 0.0f
                ? unchecked((int)0x80000000u)
                : 0;
        }

        if (x == 0.0f) {
            return y < 0.0f
                ? unchecked((int)0xC0000000u)
                : 0x40000000;
        }

        float absoluteY =
            MathF.Abs(y);

        float angle =
            x < 0.0f
                ? 3.0f -
                  (x + absoluteY) /
                  (absoluteY - x)
                : 1.0f -
                  (x - absoluteY) /
                  (absoluteY + x);

        angle *= 536870912.0f;

        if (y < 0.0f)
            angle = -angle;

        return (int)angle;
    }

    private static int DdsPhaseRate(
        float frequency) {
        return unchecked((int)(
            frequency *
            4294967296.0 /
            SampleRate));
    }

    private static float DdsFrequency(
        int phaseRate) {
        return (float)(
            phaseRate *
            (double)SampleRate /
            4294967296.0);
    }

    private static int DdsPhaseDegrees(
        float degrees) {
        return unchecked((int)(
            degrees *
            4294967296.0 /
            360.0));
    }

    private static float DdsPhaseToRadians(
        uint phase) {
        return (float)(
            phase *
            (2.0 * Math.PI) /
            4294967296.0);
    }

    private static V27TerRxComplex DdsLookupComplex(
        uint phase) {
        float radians =
            DdsPhaseToRadians(phase);

        return new V27TerRxComplex(
            MathF.Cos(radians),
            MathF.Sin(radians));
    }

    private static void AdvanceCarrier(
        V27TerRxState state) {
        state.CarrierPhase =
            unchecked(
                state.CarrierPhase +
                (uint)state.CarrierPhaseRate);
    }

    private static int IntegerSquareRoot(
        int value) {
        if (value <= 0)
            return 0;

        return (int)Math.Sqrt(value);
    }

    private static void ValidateState(
        V27TerRxState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
    }
}

/// <summary>
/// C-compatible facade preserving the original v27ter_rx_* names.
/// </summary>
public static class V27TerRxApi {
    public const float V27TER_CONSTELLATION_SCALING_FACTOR =
        V27TerRx.ConstellationScalingFactor;

    public const int V27TER_EQUALIZER_LEN =
        V27TerRx.EqualizerLength;

    public const int V27TER_EQUALIZER_PRE_LEN =
        V27TerRx.EqualizerPreLength;

    public const int V27TER_RX_4800_FILTER_STEPS =
        V27TerRx.FilterSteps4800;

    public const int V27TER_RX_2400_FILTER_STEPS =
        V27TerRx.FilterSteps2400;

    public const int V27TER_RX_FILTER_STEPS =
        V27TerRx.FilterSteps;

    public const int RX_PULSESHAPER_4800_COEFF_SETS =
        V27TerRx.RrcCoefficientSets4800;

    public const int RX_PULSESHAPER_2400_COEFF_SETS =
        V27TerRx.RrcCoefficientSets2400;

    public static V27TerRxState? v27ter_rx_init(
        V27TerRxState? state,
        int bitRate,
        V27TerRxPutBitDelegate? putBit,
        object? userData) {
        return V27TerRx.Initialize(
            state,
            bitRate,
            putBit,
            userData);
    }

    public static int v27ter_rx_restart(
        V27TerRxState state,
        int bitRate,
        bool oldTrain) {
        return V27TerRx.Restart(
            state,
            bitRate,
            oldTrain);
    }

    public static int v27ter_rx_release(
        V27TerRxState state) {
        return V27TerRx.Release(state);
    }

    public static int v27ter_rx_free(
        V27TerRxState? state) {
        return V27TerRx.Free(state);
    }

    public static V27TerRxLog v27ter_rx_get_logging_state(
        V27TerRxState state) {
        return V27TerRx.GetLoggingState(state);
    }

    public static void v27ter_rx_set_put_bit(
        V27TerRxState state,
        V27TerRxPutBitDelegate? putBit,
        object? userData) {
        V27TerRx.SetPutBit(
            state,
            putBit,
            userData);
    }

    public static void v27ter_rx_set_modem_status_handler(
        V27TerRxState state,
        V27TerRxModemStatusDelegate? handler,
        object? userData) {
        V27TerRx.SetModemStatusHandler(
            state,
            handler,
            userData);
    }

    public static int v27ter_rx(
        V27TerRxState state,
        ReadOnlySpan<short> samples) {
        return V27TerRx.Receive(
            state,
            samples);
    }

    public static int v27ter_rx(
        V27TerRxState state,
        short[] samples,
        int length) {
        return V27TerRx.Receive(
            state,
            samples,
            length);
    }

    public static int v27ter_rx_fillin(
        V27TerRxState state,
        int length) {
        return V27TerRx.FillIn(
            state,
            length);
    }

    public static int v27ter_rx_equalizer_state(
        V27TerRxState state,
        out ReadOnlyMemory<V27TerRxComplex> coefficients) {
        return V27TerRx.GetEqualizerState(
            state,
            out coefficients);
    }

    public static float v27ter_rx_carrier_frequency(
        V27TerRxState state) {
        return V27TerRx.GetCarrierFrequency(state);
    }

    public static float v27ter_rx_symbol_timing_correction(
        V27TerRxState state) {
        return V27TerRx.GetSymbolTimingCorrection(
            state);
    }

    public static float v27ter_rx_signal_power(
        V27TerRxState state) {
        return V27TerRx.GetSignalPower(state);
    }

    public static void v27ter_rx_set_signal_cutoff(
        V27TerRxState state,
        float cutoff) {
        V27TerRx.SetSignalCutoff(
            state,
            cutoff);
    }

    public static void v27ter_rx_set_qam_report_handler(
        V27TerRxState state,
        V27TerRxQamReportDelegate? handler,
        object? userData) {
        V27TerRx.SetQamReportHandler(
            state,
            handler,
            userData);
    }
}
