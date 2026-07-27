/*
 * TKFaxEngineFX - managed C# port
 *
 * Combined 1:1 module port of t30.c and t30.h.
 * The original C function names and T.30 state-machine flow are retained.
 */

using System.Text;
using TKFaxEngine;
using TKFaxEngine.FaxImage;

namespace TKFaxEngine.Daten.T30;

public enum T30Error {
    Ok = 0,
    Cedtone = 1,
    T0Expired = 2,
    T1Expired = 3,
    T3Expired = 4,
    HdlcCarrier = 5,
    CannotTrain = 6,
    OperIntFail = 7,
    Incompatible = 8,
    RxIncapable = 9,
    TxIncapable = 10,
    Noressupport = 11,
    Nosizesupport = 12,
    Unexpected = 13,
    TxBaddcs = 14,
    TxBadpg = 15,
    TxEcmphd = 16,
    TxGotdcn = 17,
    TxInvalrsp = 18,
    TxNodis = 19,
    TxPhbdead = 20,
    TxPhddead = 21,
    TxT5exp = 22,
    RxEcmphd = 23,
    RxGotdcs = 24,
    RxInvalcmd = 25,
    RxNocarrier = 26,
    RxNoeol = 27,
    RxNofax = 28,
    RxT2expdcn = 29,
    RxT2expd = 30,
    RxT2expfax = 31,
    RxT2expmps = 32,
    RxT2exprr = 33,
    RxT2exp = 34,
    RxT2Exp = RxT2exp,
    RxDcnwhy = 35,
    RxDcndata = 36,
    RxDcnfax = 37,
    RxDcnphd = 38,
    RxDcnrrd = 39,
    RxDcnnortn = 40,
    Fileerror = 41,
    Nopage = 42,
    Badtiff = 43,
    Badpage = 44,
    Badtag = 45,
    Badtiffhdr = 46,
    Nomem = 47,
    Retrydcn = 48,
    Calldropped = 49,
    CallDropped = Calldropped,
    Nopoll = 50,
    IdentUnacceptable = 51,
    SubUnacceptable = 52,
    SepUnacceptable = 53,
    PsaUnacceptable = 54,
    SidUnacceptable = 55,
    PwdUnacceptable = 56,
    TsaUnacceptable = 57,
    IraUnacceptable = 58,
    CiaUnacceptable = 59,
    IspUnacceptable = 60,
    CsaUnacceptable = 61,
}

public enum T30ModemType {
    None = 0,
    Pause,
    Ced,
    Cng,
    V21,
    V27Ter,
    V29,
    V17,
    V34Hdx,
    Done
}

public enum T30FrontEndStatus {
    SendStepComplete = 0,
    ReceiveComplete,
    SignalPresent,
    SignalAbsent,
    CedPresent,
    CngPresent
}

[Flags]
public enum T30SupportedModems {
    None = 0,
    V27Ter = 0x01,
    V29 = 0x02,
    V17 = 0x04,
    V34Hdx = 0x08,
    Iaf = 0x10
}

[Flags]
public enum T30SupportedFeatures {
    None = 0,
    Identification = 0x001,
    SelectivePolling = 0x002,
    PolledSubAddressing = 0x004,
    MultipleSelectivePolling = 0x008,
    SubAddressing = 0x010,
    TransmittingSubscriberInternetAddress = 0x020,
    InternetRoutingAddress = 0x040,
    CallingSubscriberInternetAddress = 0x080,
    InternetSelectivePollingAddress = 0x100,
    CalledSubscriberInternetAddress = 0x200,
    FieldNotValid = 0x400,
    CommandRepeat = 0x800
}

[Flags]
public enum T30IafMode {
    None = 0,
    T37 = 0x01,
    T38 = 0x02,
    FlowControl = 0x04,
    ContinuousFlow = 0x08,
    NoTcf = 0x10,
    NoFillBits = 0x20,
    NoIndicators = 0x40,
    RelaxedTimers = 0x80
}

public enum T30Phase {
    Idle = 0,
    ACed,
    ACng,
    BRx,
    BTx,
    CNonEcmRx,
    CNonEcmTx,
    CEcmRx,
    CEcmTx,
    DRx,
    DTx,
    E,
    CallFinished
}

public enum T30StateCode {
    Idle = 0,
    Answering,
    B,
    C,
    D,
    DTcf,
    DPostTcf,
    FTcf,
    FCfr,
    FFtt,
    FDocumentNonEcm,
    FPostDocumentNonEcm,
    FDocumentEcm,
    FPostDocumentEcm,
    FPostRcpMcf,
    FPostRcpPpr,
    FPostRcpRnr,
    R,
    T,
    I,
    II,
    IIQ,
    IIIQ,
    IV,
    IVPpsNull,
    IVPpsQ,
    IVPpsRnr,
    IVCtc,
    IVEor,
    IVEorRnr,
    CallFinished
}

public enum T30Operation {
    None = 0,
    T4Receive,
    T4Transmit,
    PostT4Receive,
    PostT4Transmit
}

public enum T30TimerT2T4Kind {
    Idle = 0,
    T2,
    T1A,
    T2Flagged,
    T2Dropped,
    T2C,
    T4,
    T4Flagged,
    T4Dropped,
    T4C
}




public delegate int T30PhaseBHandler(object? userData, int result);
public delegate int T30PhaseDHandler(object? userData, int result);
public delegate void T30PhaseEHandler(object? userData, int completionCode);
public delegate void T30RealTimeFrameHandler(object? userData, bool incoming, ReadOnlyMemory<byte> message);
public delegate int T30DocumentHandler(object? userData, int status);
public delegate void T30SetHandler(object? userData, T30ModemType type, int bitRate, int shortTrain, bool useHdlc);
public delegate void T30SendHdlcHandler(object? userData, ReadOnlyMemory<byte>? message, int length);
public delegate int T30DocumentGetHandler(object? userData, Memory<byte> destination);
public delegate int T30DocumentPutHandler(object? userData, ReadOnlyMemory<byte> source);

public sealed class T30ExchangedInfo {
    public string? Ident { get; set; }
    public string? SubAddress { get; set; }
    public string? SelectivePollingAddress { get; set; }
    public string? PolledSubAddress { get; set; }
    public string? SenderIdent { get; set; }
    public string? Password { get; set; }
    public byte[] Nsf { get; set; } = Array.Empty<byte>();
    public byte[] Nsc { get; set; } = Array.Empty<byte>();
    public byte[] Nss { get; set; } = Array.Empty<byte>();
    public int TsaType { get; set; }
    public string? Tsa { get; set; }
    public int TsaLength { get; set; }
    public int IraType { get; set; }
    public string? Ira { get; set; }
    public int IraLength { get; set; }
    public int CiaType { get; set; }
    public string? Cia { get; set; }
    public int CiaLength { get; set; }
    public int IspType { get; set; }
    public string? Isp { get; set; }
    public int IspLength { get; set; }
    public int CsaType { get; set; }
    public string? Csa { get; set; }
    public int CsaLength { get; set; }

    public void ClearReceived() {
        Ident = SubAddress = SelectivePollingAddress = PolledSubAddress = null;
        SenderIdent = Password = Tsa = Ira = Cia = Isp = Csa = null;
        Nsf = Nsc = Nss = Array.Empty<byte>();
        TsaType = IraType = CiaType = IspType = CsaType = 0;
        TsaLength = IraLength = CiaLength = IspLength = CsaLength = 0;
    }
}

public sealed class T30Statistics {
    public int BitRate { get; set; }
    public bool ErrorCorrectingMode { get; set; }
    public int PagesTransmitted { get; set; }
    public int PagesReceived { get; set; }
    public int PagesInFile { get; set; } = -1;
    public int ImageType { get; set; }
    public int ImageXResolution { get; set; }
    public int ImageYResolution { get; set; }
    public int ImageWidth { get; set; }
    public int ImageLength { get; set; }
    public int ExchangedType { get; set; }
    public int XResolution { get; set; }
    public int YResolution { get; set; }
    public int Width { get; set; }
    public int Length { get; set; }
    public int ImageSize { get; set; }
    public int Compression { get; set; }
    public int BadRows { get; set; }
    public int LongestBadRowRun { get; set; }
    public int ErrorCorrectingModeRetries { get; set; }
    public T30Error CurrentStatus { get; set; }
    public int RtpEvents { get; set; }
    public int RtnEvents { get; set; }
}

public sealed class T30State : IDisposable {
    public const int MaxDisDtcDcsLength = 22;
    public const int MaxIdentLength = 20;
    public const int MaxPageHeaderInfoLength = 50;
    public const int MaxEcmFrames = 256;
    public const int MaxEcmFrameLength = 260;

    public t4_rx_state_t T4Rx { get; private set; } = new();
    public t4_tx_state_t T4Tx { get; private set; } = new();
    public SslFaxState SslFax { get; private set; } = new();
    internal bool T4RxInitialized { get; set; }
    internal bool T4TxInitialized { get; set; }
    public T30Operation OperationInProgress { get; set; }
    public bool CallingParty { get; set; }
    public bool KeepBadPages { get; set; }
    public T30IafMode IafMode { get; set; }
    public T30SupportedModems SupportedModems { get; set; }
    public int SupportedCompressions { get; set; }
    public int SupportedOutputCompressions { get; set; }
    public int SupportedBilevelResolutions { get; set; }
    public int SupportedColourResolutions { get; set; }
    public int SupportedImageSizes { get; set; }
    public T30SupportedFeatures SupportedFeatures { get; set; }
    public bool EcmAllowed { get; set; }
    public bool RetransmitCapable { get; set; }
    public string RxDcsString { get; set; } = string.Empty;
    public string? HeaderInfo { get; set; }
    public bool HeaderOverlaysImage { get; set; }
    public string? HeaderTimezone { get; set; }
    public bool RemoteInterruptsAllowed { get; set; }
    public T30ExchangedInfo RxInfo { get; } = new();
    public T30ExchangedInfo TxInfo { get; } = new();
    public string? Country { get; set; }
    public string? Vendor { get; set; }
    public string? Model { get; set; }

    public T30PhaseBHandler? PhaseBHandler { get; set; }
    public object? PhaseBUserData { get; set; }
    public T30PhaseDHandler? PhaseDHandler { get; set; }
    public object? PhaseDUserData { get; set; }
    public T30PhaseEHandler? PhaseEHandler { get; set; }
    public object? PhaseEUserData { get; set; }
    public T30RealTimeFrameHandler? RealTimeFrameHandler { get; set; }
    public object? RealTimeFrameUserData { get; set; }
    public T30DocumentHandler? DocumentHandler { get; set; }
    public object? DocumentUserData { get; set; }
    public T30SetHandler? SetRxTypeHandler { get; set; }
    public object? SetRxTypeUserData { get; set; }
    public T30SetHandler? SetTxTypeHandler { get; set; }
    public object? SetTxTypeUserData { get; set; }
    public T30SendHdlcHandler? SendHdlcHandler { get; set; }
    public object? SendHdlcUserData { get; set; }
    public T30DocumentGetHandler? DocumentGetHandler { get; set; }
    public object? DocumentGetUserData { get; set; }
    public T30DocumentPutHandler? DocumentPutHandler { get; set; }
    public object? DocumentPutUserData { get; set; }

    public int MaxCommandTries { get; set; }
    public int MaxResponseTries { get; set; }
    public byte LocalMinimumScanTimeCode { get; set; }
    public T30Phase Phase { get; set; }
    public T30Phase NextPhase { get; set; }
    public T30StateCode State { get; set; }
    public int Step { get; set; }
    public byte[] DcsFrame { get; } = new byte[MaxDisDtcDcsLength];
    public int DcsLength { get; set; }
    public byte[] LocalDisDtcFrame { get; } = new byte[MaxDisDtcDcsLength];
    public int LocalDisDtcLength { get; set; }
    public byte[] FarDisDtcFrame { get; } = new byte[MaxDisDtcDcsLength];
    public int FarDisDtcLength { get; set; }
    public bool DisReceived { get; set; }
    public bool ShortTrain { get; set; }
    public bool ImageCarrierAttempted { get; set; }
    public int TcfTestBits { get; set; }
    public int TcfCurrentZeros { get; set; }
    public int TcfMostZeros { get; set; }
    public int CurrentFallback { get; set; }
    public T30SupportedModems CurrentPermittedModems { get; set; }
    public bool RxSignalPresent { get; set; }
    public bool RxTrained { get; set; }
    public bool RxFrameReceived { get; set; }
    public T30ModemType CurrentRxType { get; set; }
    public T30ModemType CurrentTxType { get; set; }
    public long TimerT0T1 { get; set; }
    public long TimerT2T4 { get; set; }
    public T30TimerT2T4Kind TimerT2T4Kind { get; set; }
    public long TimerT3 { get; set; }
    public long TimerT5 { get; set; }
    public long TimerT6 { get; set; }
    public long TimerT7 { get; set; }
    public long TimerT8 { get; set; }
    public bool FarEndDetected { get; set; }
    public bool EndOfProcedureDetected { get; set; }
    public bool LocalInterruptPending { get; set; }
    public int MutualCompressions { get; set; }
    public int MutualBilevelResolutions { get; set; }
    public int MutualColourResolutions { get; set; }
    public int MutualImageSizes { get; set; }
    public int LineCompression { get; set; }
    public int LineImageType { get; set; }
    public int LineWidthCode { get; set; }
    public byte MinimumScanTimeCode { get; set; }
    public int XResolution { get; set; }
    public int YResolution { get; set; }
    public int CurrentPageResolution { get; set; }
    public int ImageWidth { get; set; }
    public int Retries { get; set; }
    public bool ErrorCorrectingMode { get; set; }
    public int ErrorCorrectingModeRetries { get; set; }
    public int PprCount { get; set; }
    public int ReceiverNotReadyCount { get; set; }
    public int OctetsPerEcmFrame { get; set; }
    public byte[][] EcmData { get; } = CreateEcmData();
    public short[] EcmLength { get; } = new short[MaxEcmFrames];
    public byte[] EcmFrameMap { get; } = new byte[35];
    public int RxPageNumber { get; set; }
    public int TxPageNumber { get; set; }
    public int EcmBlock { get; set; }
    public int EcmFrames { get; set; }
    public int EcmFramesThisTransmitBurst { get; set; }
    public int EcmCurrentTransmitFrame { get; set; }
    public bool EcmAtPageEnd { get; set; }
    public int LastRxPageResult { get; set; }
    public int NextTxStep { get; set; }
    public byte NextRxStep { get; set; }
    public string? RxFile { get; set; }
    public int RxStopPage { get; set; }
    public string? TxFile { get; set; }
    public int TxStartPage { get; set; }
    public int TxStopPage { get; set; }
    public T30Error CurrentStatus { get; set; }
    public byte LastPpsFcf2 { get; set; }
    public bool RxEcmBlockOk { get; set; }
    public int EcmProgress { get; set; }
    public int LastReceivedFrameType { get; set; }
    public byte[] LastTransmittedFrame { get; set; } = Array.Empty<byte>();
    public int LastTransmittedFrameLength { get; set; }
    public int RtpEvents { get; set; }
    public int RtnEvents { get; set; }
    public int CurrentBitRate { get; set; }
    public T30Log Logging { get; private set; } = new();
    public bool IsDisposed { get; private set; }

    private static byte[][] CreateEcmData() {
        byte[][] data = new byte[MaxEcmFrames][];
        for (int i = 0; i < data.Length; i++)
            data[i] = new byte[MaxEcmFrameLength];
        return data;
    }

    internal void ResetForInit() {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(T30State));

        t4_rx.t4_rx_free(T4Rx);
        T4Tx.Dispose();
        SslFax.Dispose();

        T4Rx = new t4_rx_state_t();
        T4Tx = new t4_tx_state_t();
        SslFax = new SslFaxState();
        Logging = new T30Log();

        T4RxInitialized = false;
        T4TxInitialized = false;
        OperationInProgress = T30Operation.None;
        CallingParty = false;
        KeepBadPages = false;
        IafMode = T30IafMode.None;
        SupportedModems = T30SupportedModems.None;
        SupportedCompressions = 0;
        SupportedOutputCompressions = 0;
        SupportedBilevelResolutions = 0;
        SupportedColourResolutions = 0;
        SupportedImageSizes = 0;
        SupportedFeatures = T30SupportedFeatures.None;
        EcmAllowed = false;
        RetransmitCapable = false;
        RxDcsString = string.Empty;
        HeaderInfo = null;
        HeaderOverlaysImage = false;
        HeaderTimezone = null;
        RemoteInterruptsAllowed = false;
        RxInfo.ClearReceived();
        TxInfo.ClearReceived();
        Country = null;
        Vendor = null;
        Model = null;

        PhaseBHandler = null;
        PhaseBUserData = null;
        PhaseDHandler = null;
        PhaseDUserData = null;
        PhaseEHandler = null;
        PhaseEUserData = null;
        RealTimeFrameHandler = null;
        RealTimeFrameUserData = null;
        DocumentHandler = null;
        DocumentUserData = null;
        SetRxTypeHandler = null;
        SetRxTypeUserData = null;
        SetTxTypeHandler = null;
        SetTxTypeUserData = null;
        SendHdlcHandler = null;
        SendHdlcUserData = null;
        DocumentGetHandler = null;
        DocumentGetUserData = null;
        DocumentPutHandler = null;
        DocumentPutUserData = null;

        MaxCommandTries = 0;
        MaxResponseTries = 0;
        LocalMinimumScanTimeCode = 0;
        Phase = T30Phase.Idle;
        NextPhase = T30Phase.Idle;
        State = T30StateCode.Idle;
        Step = 0;
        Array.Clear(DcsFrame, 0, DcsFrame.Length);
        DcsLength = 0;
        Array.Clear(LocalDisDtcFrame, 0, LocalDisDtcFrame.Length);
        LocalDisDtcLength = 0;
        Array.Clear(FarDisDtcFrame, 0, FarDisDtcFrame.Length);
        FarDisDtcLength = 0;
        DisReceived = false;
        ShortTrain = false;
        ImageCarrierAttempted = false;
        TcfTestBits = 0;
        TcfCurrentZeros = 0;
        TcfMostZeros = 0;
        CurrentFallback = 0;
        CurrentPermittedModems = T30SupportedModems.None;
        RxSignalPresent = false;
        RxTrained = false;
        RxFrameReceived = false;
        CurrentRxType = T30ModemType.None;
        CurrentTxType = T30ModemType.None;
        TimerT0T1 = 0;
        TimerT2T4 = 0;
        TimerT2T4Kind = T30TimerT2T4Kind.Idle;
        TimerT3 = 0;
        TimerT5 = 0;
        TimerT6 = 0;
        TimerT7 = 0;
        TimerT8 = 0;
        FarEndDetected = false;
        EndOfProcedureDetected = false;
        LocalInterruptPending = false;
        MutualCompressions = 0;
        MutualBilevelResolutions = 0;
        MutualColourResolutions = 0;
        MutualImageSizes = 0;
        LineCompression = 0;
        LineImageType = 0;
        LineWidthCode = 0;
        MinimumScanTimeCode = 0;
        XResolution = 0;
        YResolution = 0;
        CurrentPageResolution = 0;
        ImageWidth = 0;
        Retries = 0;
        ErrorCorrectingMode = false;
        ErrorCorrectingModeRetries = 0;
        PprCount = 0;
        ReceiverNotReadyCount = 0;
        OctetsPerEcmFrame = 0;
        for (int i = 0; i < EcmData.Length; i++)
            Array.Clear(EcmData[i], 0, EcmData[i].Length);
        Array.Clear(EcmLength, 0, EcmLength.Length);
        Array.Clear(EcmFrameMap, 0, EcmFrameMap.Length);
        RxPageNumber = 0;
        TxPageNumber = 0;
        EcmBlock = 0;
        EcmFrames = 0;
        EcmFramesThisTransmitBurst = 0;
        EcmCurrentTransmitFrame = 0;
        EcmAtPageEnd = false;
        LastRxPageResult = 0;
        NextTxStep = 0;
        NextRxStep = 0;
        RxFile = null;
        RxStopPage = 0;
        TxFile = null;
        TxStartPage = 0;
        TxStopPage = 0;
        CurrentStatus = T30Error.Ok;
        LastPpsFcf2 = 0;
        RxEcmBlockOk = false;
        EcmProgress = 0;
        LastReceivedFrameType = 0;
        LastTransmittedFrame = Array.Empty<byte>();
        LastTransmittedFrameLength = 0;
        RtpEvents = 0;
        RtnEvents = 0;
        CurrentBitRate = 0;
    }

    public void Dispose() {
        if (IsDisposed) return;
        t4_rx.t4_rx_free(T4Rx);
        T4Tx.Dispose();
        SslFax.Dispose();
        IsDisposed = true;
    }
}

public static partial class T30 {
    private const int SamplesPerSecond = 8000;
    private const int AddressField = 0xFF;
    private const int ControlNonFinal = 0x03;
    private const int ControlFinal = 0x13;
    private const int DefaultTimerT0 = 60_000;
    private const int DefaultTimerT1 = 35_000;
    private const int DefaultTimerT1A = 35_000;
    private const int DefaultTimerT2 = 7_000;
    private const int DefaultTimerT2Flagged = 3_000;
    private const int DefaultTimerT2Dropped = 200;
    private const int DefaultTimerT3 = 15_000;
    private const int DefaultTimerT4 = 3_450;
    private const int DefaultTimerT4Flagged = 3_000;
    private const int DefaultTimerT4Dropped = 200;
    private const int DefaultTimerT5 = 65_000;
    private const int DefaultTimerT6 = 5_000;
    private const int DefaultTimerT7 = 7_000;
    private const int DefaultTimerT8 = 10_000;
    private const int PprLimitBeforeCtcOrEor = 4;
    private const int FinalFlushTime = 1_000;

    private static readonly (int Rate, T30ModemType Modem, T30SupportedModems Required, byte DcsCode)[] FallbackSequence =
    {
        (14400, T30ModemType.V17, T30SupportedModems.V17, 0x20),
        (12000, T30ModemType.V17, T30SupportedModems.V17, 0x28),
        ( 9600, T30ModemType.V17, T30SupportedModems.V17, 0x24),
        ( 9600, T30ModemType.V29, T30SupportedModems.V29, 0x04),
        ( 7200, T30ModemType.V17, T30SupportedModems.V17, 0x2C),
        ( 7200, T30ModemType.V29, T30SupportedModems.V29, 0x0C),
        ( 4800, T30ModemType.V27Ter, T30SupportedModems.V27Ter, 0x08),
        ( 2400, T30ModemType.V27Ter, T30SupportedModems.V27Ter, 0x00),
    };

    public static T30State t30_init(
        T30State? state,
        bool callingParty,
        T30SetHandler? setRxTypeHandler,
        object? setRxTypeUserData,
        T30SetHandler? setTxTypeHandler,
        object? setTxTypeUserData,
        T30SendHdlcHandler? sendHdlcHandler,
        object? sendHdlcUserData) {
        if (state is null)
            state = new T30State();
        else
            state.ResetForInit();

        state.SetRxTypeHandler = setRxTypeHandler;
        state.SetRxTypeUserData = setRxTypeUserData;
        state.SetTxTypeHandler = setTxTypeHandler;
        state.SetTxTypeUserData = setTxTypeUserData;
        state.SendHdlcHandler = sendHdlcHandler;
        state.SendHdlcUserData = sendHdlcUserData;

        state.SupportedModems = T30SupportedModems.V27Ter | T30SupportedModems.V29 | T30SupportedModems.V17;
        state.SupportedCompressions = (int)(t4_image_compression_t.T4_COMPRESSION_T4_1D | t4_image_compression_t.T4_COMPRESSION_T4_2D);
        state.SupportedOutputCompressions = (int)(t4_image_compression_t.T4_COMPRESSION_T4_2D | t4_image_compression_t.T4_COMPRESSION_JPEG);
        state.SupportedBilevelResolutions =
            (int)(t4_image_resolution_t.T4_RESOLUTION_R8_STANDARD |
                  t4_image_resolution_t.T4_RESOLUTION_R8_FINE |
                  t4_image_resolution_t.T4_RESOLUTION_R8_SUPERFINE |
                  t4_image_resolution_t.T4_RESOLUTION_200_100 |
                  t4_image_resolution_t.T4_RESOLUTION_200_200 |
                  t4_image_resolution_t.T4_RESOLUTION_200_400);
        state.SupportedImageSizes =
            (int)(t4_image_support_t.T4_SUPPORT_WIDTH_215MM |
                  t4_image_support_t.T4_SUPPORT_LENGTH_US_LETTER |
                  t4_image_support_t.T4_SUPPORT_LENGTH_US_LEGAL |
                  t4_image_support_t.T4_SUPPORT_LENGTH_A4 |
                  t4_image_support_t.T4_SUPPORT_LENGTH_B4 |
                  t4_image_support_t.T4_SUPPORT_LENGTH_UNLIMITED);
        state.LocalMinimumScanTimeCode = 7;
        state.MaxCommandTries = 3;
        state.MaxResponseTries = 6;
        state.IafMode = T30IafMode.T37 | T30IafMode.T38;
        state.SslFax.Initialize();
        t30_restart(state, callingParty);
        return state;
    }

    public static T30State t30_init(
        bool callingParty,
        T30SetHandler? setRxTypeHandler,
        T30SetHandler? setTxTypeHandler,
        T30SendHdlcHandler? sendHdlcHandler)
        => t30_init(null, callingParty, setRxTypeHandler, null, setTxTypeHandler, null, sendHdlcHandler, null);

    public static int t30_restart(T30State state, bool callingParty) {
        ArgumentNullException.ThrowIfNull(state);
        ThrowIfDisposed(state);
        state.CallingParty = callingParty;
        state.OperationInProgress = T30Operation.None;
        state.CurrentStatus = T30Error.Ok;
        state.RxInfo.ClearReceived();
        state.Country = state.Vendor = state.Model = null;
        state.DisReceived = false;
        state.ShortTrain = false;
        state.ImageCarrierAttempted = false;
        state.CurrentFallback = 0;
        state.CurrentPermittedModems = state.SupportedModems;
        state.RxSignalPresent = false;
        state.RxTrained = false;
        state.RxFrameReceived = false;
        state.FarEndDetected = false;
        state.EndOfProcedureDetected = false;
        state.LocalInterruptPending = false;
        state.Retries = 0;
        state.ErrorCorrectingModeRetries = 0;
        state.PprCount = 0;
        state.ReceiverNotReadyCount = 0;
        state.RxPageNumber = 0;
        state.TxPageNumber = 0;
        state.EcmBlock = 0;
        state.EcmFrames = -1;
        Array.Fill(state.EcmLength, (short)-1);
        Array.Clear(state.EcmFrameMap, 0, state.EcmFrameMap.Length);
        state.LastPpsFcf2 = T30Frame.Null;
        state.RxEcmBlockOk = false;
        state.EcmProgress = 0;
        state.LastReceivedFrameType = 0;
        state.LastTransmittedFrame = Array.Empty<byte>();
        state.LastTransmittedFrameLength = 0;
        state.TimerT0T1 = MillisecondsToSamples(DefaultTimerT0);
        state.TimerT2T4 = 0;
        state.TimerT2T4Kind = T30TimerT2T4Kind.Idle;
        state.NextPhase = T30Phase.Idle;
        state.TimerT3 = state.TimerT5 = state.TimerT6 = state.TimerT7 = state.TimerT8 = 0;
        ReleaseDocumentStates(state);
        t30_build_dis_or_dtc(state);

        if (callingParty) {
            set_state(state, T30StateCode.T);
            set_phase(state, T30Phase.ACng);
            SetTransmit(state, T30ModemType.Cng, 0, 0, false);
            SetReceive(state, T30ModemType.V21, 300, 0, true);
        } else {
            set_state(state, T30StateCode.Answering);
            set_phase(state, T30Phase.ACed);
            SetTransmit(state, T30ModemType.Ced, 0, 0, false);
            SetReceive(state, T30ModemType.V21, 300, 0, true);
        }
        return 0;
    }

    public static int t30_call_active(T30State state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Phase == T30Phase.CallFinished ? 0 : 1;
    }

    public static void t30_terminate(T30State state) {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Phase == T30Phase.CallFinished) return;
        state.CurrentStatus = state.CurrentStatus == T30Error.Ok ? T30Error.CallDropped : state.CurrentStatus;
        terminate_call(state);
    }

    public static void t30_front_end_status(object? userData, int status) {
        if (userData is not T30State state)
            throw new ArgumentException("The callback user data must be T30State.", nameof(userData));
        t30_front_end_status(state, (T30FrontEndStatus)status);
    }

    public static void t30_front_end_status(T30State state, T30FrontEndStatus status) {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Phase == T30Phase.CallFinished)
            return;

        switch (status) {
            case T30FrontEndStatus.SendStepComplete:
                OnSendStepComplete(state);
                break;

            case T30FrontEndStatus.ReceiveComplete:
                state.Logging.Flow($"Receive complete in phase {state.Phase}, state {state.State}");
                if (state.Phase == T30Phase.CNonEcmRx)
                    t30_non_ecm_rx_status(state, (int)SignalStatus.CarrierDown);
                else
                    t30_hdlc_rx_status(state, SignalStatus.CarrierDown);
                break;

            case T30FrontEndStatus.SignalPresent:
                state.Logging.Flow("A signal is present");
                switch (state.Phase) {
                    case T30Phase.ACed:
                    case T30Phase.ACng:
                    case T30Phase.BRx:
                    case T30Phase.DRx:
                        t30_hdlc_rx_status(state, SignalStatus.CarrierUp);
                        t30_hdlc_rx_status(state, SignalStatus.FramingOk);
                        break;
                    default:
                        state.RxSignalPresent = true;
                        break;
                }
                break;

            case T30FrontEndStatus.SignalAbsent:
                state.Logging.Flow("No signal is present");
                break;

            case T30FrontEndStatus.CedPresent:
                state.Logging.Flow("CED tone is present");
                break;

            case T30FrontEndStatus.CngPresent:
                state.Logging.Flow("CNG tone is present");
                break;
        }
    }

    public static void t30_hdlc_accept(object? userData, ReadOnlySpan<byte> message, int length, int ok) {
        if (userData is not T30State state)
            throw new ArgumentException("The callback user data must be T30State.", nameof(userData));
        if (length < 0) {
            t30_hdlc_rx_status(state, (SignalStatus)length);
            return;
        }
        if (length > message.Length) throw new ArgumentOutOfRangeException(nameof(length));
        t30_hdlc_accept(state, message[..length], ok != 0);
    }

    public static void t30_hdlc_accept(T30State state, ReadOnlySpan<byte> message, bool ok) {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Phase == T30Phase.CallFinished)
            return;

        if (!ok) {
            state.Logging.Flow("Bad HDLC CRC received");
            if (state.Phase != T30Phase.CEcmRx) {
                if (state.SupportedFeatures.HasFlag(T30SupportedFeatures.CommandRepeat)) {
                    state.Step = 0;
                    queue_phase(state, state.Phase == T30Phase.BRx ? T30Phase.BTx : T30Phase.DTx);
                    send_simple_frame(state, T30Frame.Crp);
                } else {
                    state.Logging.Flow($"Bad CRC and timer is {state.TimerT2T4Kind}");
                    if (state.TimerT2T4Kind == T30TimerT2T4Kind.T2Flagged)
                        timer_t2_t4_stop(state);
                }
            }
            return;
        }

        if (message.Length < 3) {
            state.Logging.Flow($"Bad HDLC frame length - {message.Length}");
            timer_t2_t4_stop(state);
            return;
        }

        if (message[0] != AddressField
            || (message[1] != ControlNonFinal && message[1] != ControlFinal)) {
            state.Logging.Flow($"Bad HDLC frame header - {message[0]:X2} {message[1]:X2}");
            timer_t2_t4_stop(state);
            return;
        }

        state.RxFrameReceived = true;
        state.FarEndDetected = true;
        byte fcf = message[2];
        state.LastReceivedFrameType = fcf;
        state.Logging.Flow($"Rx {T30Logging.t30_frametype(fcf)} ({message.Length} bytes).");
        state.RealTimeFrameHandler?.Invoke(state.RealTimeFrameUserData, true, message.ToArray());
        timer_t2_t4_stop(state);
        process_rx_control_msg(state, message);
    }

    public static void t30_non_ecm_put_bit(object? userData, int bit) {
        if (userData is not T30State state)
            throw new ArgumentException("Expected T30State.", nameof(userData));

        if (bit < 0) {
            t30_non_ecm_rx_status(state, bit);
            return;
        }

        switch (state.State) {
            case T30StateCode.FTcf:
                state.TcfTestBits++;
                if (bit != 0) {
                    if (state.TcfCurrentZeros > state.TcfMostZeros)
                        state.TcfMostZeros = state.TcfCurrentZeros;
                    state.TcfCurrentZeros = 0;
                } else {
                    state.TcfCurrentZeros++;
                }
                break;

            case T30StateCode.FDocumentNonEcm:
                int result = t4_rx.t4_rx_put_bit(state.T4Rx, bit);
                if (result != (int)t4_decoder_status_t.T4_DECODE_MORE_DATA) {
                    if (result != (int)t4_decoder_status_t.T4_DECODE_OK)
                        state.Logging.Flow($"Page ended with status {result}.");
                    set_state(state, T30StateCode.FPostDocumentNonEcm);
                    queue_phase(state, T30Phase.DRx);
                    timer_t2_start(state);
                }
                break;
        }
    }

    public static void t30_non_ecm_put(object? userData, byte[] buffer, int length) {
        if (userData is not T30State state)
            throw new ArgumentException("Expected T30State.", nameof(userData));

        if (length < 0) {
            t30_non_ecm_rx_status(state, length);
            return;
        }
        if (length > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        ReadOnlySpan<byte> data = buffer.AsSpan(0, length);
        switch (state.State) {
            case T30StateCode.FTcf:
                state.TcfTestBits += checked(8 * length);
                foreach (byte value in data) {
                    if (value != 0) {
                        if (state.TcfCurrentZeros > state.TcfMostZeros)
                            state.TcfMostZeros = state.TcfCurrentZeros;
                        state.TcfCurrentZeros = 0;
                    } else {
                        state.TcfCurrentZeros += 8;
                    }
                }
                break;

            case T30StateCode.FDocumentNonEcm:
                int result = t4_rx.t4_rx_put(state.T4Rx, buffer, length);
                if (result != (int)t4_decoder_status_t.T4_DECODE_MORE_DATA) {
                    if (result != (int)t4_decoder_status_t.T4_DECODE_OK)
                        state.Logging.Flow($"Page ended with status {result}.");
                    set_state(state, T30StateCode.FPostDocumentNonEcm);
                    queue_phase(state, T30Phase.DRx);
                    timer_t2_start(state);
                }
                break;
        }
    }

    public static int t30_non_ecm_get_bit(object? userData) {
        if (userData is not T30State state)
            throw new ArgumentException("Expected T30State.", nameof(userData));

        switch (state.State) {
            case T30StateCode.DTcf:
                int bit = 0;
                if (state.TcfTestBits-- < 0)
                    bit = (int)SignalStatus.EndOfData;
                return bit;

            case T30StateCode.I:
                return t4_tx.t4_tx_get_bit(state.T4Tx);

            case T30StateCode.DPostTcf:
            case T30StateCode.IIQ:
                return 0;

            default:
                state.Logging.Warning($"t30_non_ecm_get_bit in bad state {state.State}.");
                return (int)SignalStatus.EndOfData;
        }
    }

    public static int t30_non_ecm_get(object? userData, Span<byte> buffer, int maxLength) {
        if (userData is not T30State state)
            throw new ArgumentException("Expected T30State.", nameof(userData));
        if (maxLength < 0 || maxLength > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(maxLength));

        switch (state.State) {
            case T30StateCode.DTcf:
                int length = 0;
                for (; length < maxLength; length++) {
                    buffer[length] = 0;
                    if ((state.TcfTestBits -= 8) < 0) {
                        length++;
                        break;
                    }
                }
                return length;

            case T30StateCode.I:
                return t4_tx.t4_tx_get(state.T4Tx, buffer, maxLength);

            case T30StateCode.DPostTcf:
            case T30StateCode.IIQ:
                return 0;

            default:
                state.Logging.Warning($"t30_non_ecm_get in bad state {state.State}.");
                return -1;
        }
    }

    private static void t30_non_ecm_rx_status(T30State state, int status) {
        state.Logging.Flow(
            $"Non-ECM signal status is {AsyncApi.signal_status_to_str(status)} ({status}) in state {state.State}.");

        switch ((SignalStatus)status) {
            case SignalStatus.TrainingInProgress:
                state.ImageCarrierAttempted = true;
                break;

            case SignalStatus.TrainingFailed:
                state.RxTrained = false;
                break;

            case SignalStatus.TrainingSucceeded:
                state.TcfTestBits = 0;
                state.TcfCurrentZeros = 0;
                state.TcfMostZeros = 0;
                state.RxSignalPresent = true;
                state.RxTrained = true;
                state.TimerT2T4 = 0;
                break;

            case SignalStatus.CarrierUp:
                break;

            case SignalStatus.CarrierDown:
                bool wasTrained = state.RxTrained;
                state.RxSignalPresent = false;
                state.RxTrained = false;

                switch (state.State) {
                    case T30StateCode.FTcf:
                        if (sslfax_enabled(state)
                            && !string.IsNullOrEmpty(state.SslFax.Url)
                            && !state.SslFax.IsConnected) {
                            SslFax.sslfax_start_client(state.SslFax);
                            if (state.SslFax.IsConnected) {
                                state.RealTimeFrameHandler = t30_sslfax_real_time_frame_handler;
                                state.RealTimeFrameUserData = state;
                                state.CurrentStatus = T30Error.Ok;
                                wasTrained = true;
                            }
                        }
                        if (wasTrained) {
                            if (state.TcfCurrentZeros > state.TcfMostZeros)
                                state.TcfMostZeros = state.TcfCurrentZeros;
                            int requiredZeros = FallbackSequence[
                                Math.Clamp(state.CurrentFallback, 0, FallbackSequence.Length - 1)].Rate;
                            state.Logging.Flow(
                                $"Trainability (TCF) test result - {state.TcfTestBits} total bits; " +
                                $"longest run of zeros was {state.TcfMostZeros}.");

                            if (state.TcfMostZeros < requiredZeros) {
                                state.Logging.Flow(
                                    $"Trainability (TCF) test failed - longest run of zeros was {state.TcfMostZeros}.");
                                set_phase(state, T30Phase.BTx);
                                set_state(state, T30StateCode.FFtt);
                                state.Step = 0;
                                send_simple_frame(state, T30Frame.Ftt);
                            } else {
                                state.ShortTrain = true;
                                rx_start_page(state);
                                set_phase(state, T30Phase.BTx);
                                set_state(state, T30StateCode.FCfr);
                                send_cfr_sequence(state, true);
                            }
                        }
                        break;

                    case T30StateCode.FPostDocumentNonEcm:
                        if (state.CurrentStatus == T30Error.RxNocarrier)
                            state.CurrentStatus = T30Error.Ok;
                        break;

                    default:
                        if (wasTrained) {
                            state.Logging.Warning("Page did not end cleanly.");
                            set_state(state, T30StateCode.FPostDocumentNonEcm);
                            set_phase(state, T30Phase.DRx);
                            timer_t2_start(state);
                            if (state.CurrentStatus == T30Error.RxNocarrier)
                                state.CurrentStatus = T30Error.Ok;
                        } else {
                            state.Logging.Warning("Non-ECM carrier not found.");
                            state.CurrentStatus = T30Error.RxNocarrier;
                        }
                        break;
                }

                if (state.NextPhase != T30Phase.Idle) {
                    T30Phase next = state.NextPhase;
                    set_phase(state, next);
                }
                break;

            default:
                state.Logging.Warning($"Unexpected non-ECM rx status - {status}.");
                break;
        }
    }

    public static void t30_timer_update(T30State state, int samples) {
        ArgumentNullException.ThrowIfNull(state);
        if (samples <= 0 || state.Phase == T30Phase.CallFinished)
            return;

        state.TimerT0T1 = Tick(state.TimerT0T1, samples, out bool timerT0T1Expired);
        if (timerT0T1Expired) {
            if (state.FarEndDetected)
                timer_t1_expired(state);
            else
                timer_t0_expired(state);
            return;
        }

        T30TimerT2T4Kind timerKind = state.TimerT2T4Kind;
        state.TimerT2T4 = Tick(state.TimerT2T4, samples, out bool timerT2T4Expired);
        if (timerT2T4Expired) {
            state.TimerT2T4Kind = T30TimerT2T4Kind.Idle;
            switch (timerKind) {
                case T30TimerT2T4Kind.T1A:
                    timer_t1a_expired(state);
                    break;
                case T30TimerT2T4Kind.T2:
                case T30TimerT2T4Kind.T2C:
                    timer_t2_expired(state);
                    break;
                case T30TimerT2T4Kind.T2Flagged:
                    timer_t2_flagged_expired(state);
                    break;
                case T30TimerT2T4Kind.T2Dropped:
                    timer_t2_dropped_expired(state);
                    break;
                case T30TimerT2T4Kind.T4:
                case T30TimerT2T4Kind.T4C:
                    timer_t4_expired(state);
                    break;
                case T30TimerT2T4Kind.T4Flagged:
                    timer_t4_flagged_expired(state);
                    break;
                case T30TimerT2T4Kind.T4Dropped:
                    timer_t4_dropped_expired(state);
                    break;
            }
            if (state.Phase == T30Phase.CallFinished)
                return;
        }

        state.TimerT3 = Tick(state.TimerT3, samples, out bool timerT3Expired);
        if (timerT3Expired) {
            timer_t3_expired(state);
            return;
        }

        state.TimerT5 = Tick(state.TimerT5, samples, out bool timerT5Expired);
        if (timerT5Expired) {
            timer_t5_expired(state);
            return;
        }

        state.TimerT6 = Tick(state.TimerT6, samples, out bool timerT6Expired);
        if (timerT6Expired) {
            timer_t6_expired(state);
            return;
        }
        state.TimerT7 = Tick(state.TimerT7, samples, out bool timerT7Expired);
        if (timerT7Expired) {
            timer_t7_expired(state);
            return;
        }
        state.TimerT8 = Tick(state.TimerT8, samples, out bool timerT8Expired);
        if (timerT8Expired)
            timer_t8_expired(state);
    }

    public static void t30_get_transfer_statistics(T30State state, T30Statistics destination) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(destination);
        t4_stats_t source = new();
        switch (state.OperationInProgress) {
            case T30Operation.T4Transmit:
            case T30Operation.PostT4Transmit:
                t4_tx.t4_tx_get_transfer_statistics(state.T4Tx, source);
                break;
            case T30Operation.T4Receive:
            case T30Operation.PostT4Receive:
                t4_rx.t4_rx_get_transfer_statistics(state.T4Rx, source);
                break;
        }
        destination.BitRate = state.CurrentBitRate;
        destination.ErrorCorrectingMode = state.ErrorCorrectingMode;
        destination.PagesTransmitted = state.TxPageNumber;
        destination.PagesReceived = state.RxPageNumber;
        destination.PagesInFile = source.pages_in_file;
        destination.ImageType = source.image_type;
        destination.ImageXResolution = source.image_x_resolution;
        destination.ImageYResolution = source.image_y_resolution;
        destination.ImageWidth = source.image_width;
        destination.ImageLength = source.image_length;
        destination.ExchangedType = source.type;
        destination.XResolution = source.x_resolution;
        destination.YResolution = source.y_resolution;
        destination.Width = source.width;
        destination.Length = source.length;
        destination.ImageSize = source.line_image_size;
        destination.Compression = source.compression;
        destination.BadRows = source.bad_rows;
        destination.LongestBadRowRun = source.longest_bad_row_run;
        destination.ErrorCorrectingModeRetries = state.ErrorCorrectingModeRetries;
        destination.CurrentStatus = state.CurrentStatus;
        destination.RtpEvents = state.RtpEvents;
        destination.RtnEvents = state.RtnEvents;
    }

    public static void t30_local_interrupt_request(T30State state, int interruptState) {
        ArgumentNullException.ThrowIfNull(state);
        state.LocalInterruptPending = interruptState != 0;
    }

    public static void t30_remote_interrupts_allowed(T30State state, int allowed) {
        ArgumentNullException.ThrowIfNull(state);
        state.RemoteInterruptsAllowed = allowed != 0;
    }

    public static int t30_release(T30State state) {
        ArgumentNullException.ThrowIfNull(state);
        ReleaseDocumentStates(state);
        return 0;
    }

    public static int t30_free(T30State? state) {
        state?.Dispose();
        return 0;
    }


    private static void process_rx_dis_dtc(T30State state, ReadOnlySpan<byte> frame) {
        queue_phase(state, T30Phase.BTx);
        if (analyze_rx_dis_dtc(state, frame) < 0) {
            send_dcn(state);
            return;
        }

        int phaseResult = state.PhaseBHandler?.Invoke(state.PhaseBUserData, frame[2]) ?? (int)T30Error.Ok;
        if (phaseResult != (int)T30Error.Ok) {
            state.Logging.Flow($"Application rejected DIS/DTC - status {phaseResult}");
            state.CurrentStatus = (T30Error)phaseResult;
            send_dcn(state);
            return;
        }

        if (!string.IsNullOrEmpty(state.TxFile)) {
            state.Logging.Flow($"Trying to send file '{state.TxFile}'");
            if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisReadyToReceiveFaxDocument)) {
                state.Logging.Flow($"DIS/DTC far end cannot receive");
                state.CurrentStatus = T30Error.RxIncapable;
                send_dcn(state);
                return;
            }

            if (start_sending_document(state) != 0) {
                send_dcn(state);
                return;
            }

            if (build_dcs(state) != 0) {
                state.Logging.Flow("The far end is incompatible");
                send_dcn(state);
                return;
            }

            int fallback = Math.Clamp(state.CurrentFallback, 0, FallbackSequence.Length - 1);
            state.Logging.Flow(
                $"Put document with modem ({(int)FallbackSequence[fallback].Modem}) " +
                $"{FallbackSequence[fallback].Modem} at {FallbackSequence[fallback].Rate}bps");
            state.Retries = 0;
            send_dcs_sequence(state, true);
            return;
        }

        state.Logging.Flow("DIS/DTC - nothing to send");
        if (!string.IsNullOrEmpty(state.RxFile)) {
            state.Logging.Flow($"Trying to receive file '{state.RxFile}'");
            if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisReadyToTransmitFaxDocument)) {
                state.Logging.Flow("DIS/DTC far end cannot transmit");
                state.CurrentStatus = T30Error.TxIncapable;
                send_dcn(state);
                return;
            }

            if (start_receiving_document(state) != 0) {
                send_dcn(state);
                return;
            }

            if (set_dis_or_dtc(state) != 0) {
                state.CurrentStatus = T30Error.Incompatible;
                send_dcn(state);
                return;
            }

            state.Retries = 0;
            send_dis_or_dtc_sequence(state, true);
            return;
        }

        state.Logging.Flow("DIS/DTC - nothing to receive");
        send_dcn(state);
    }

    private static void process_rx_dcs(T30State state, ReadOnlySpan<byte> frame) {
        if (analyze_rx_dcs(state, frame) < 0) {
            send_dcn(state);
            return;
        }

        int phaseResult = state.PhaseBHandler?.Invoke(state.PhaseBUserData, frame[2]) ?? (int)T30Error.Ok;
        if (phaseResult != (int)T30Error.Ok) {
            state.Logging.Flow($"Application rejected DCS - status {phaseResult}");
            state.CurrentStatus = (T30Error)phaseResult;
            send_dcn(state);
            return;
        }

        int fallback = Math.Clamp(state.CurrentFallback, 0, FallbackSequence.Length - 1);
        state.Logging.Flow(
            $"Get document with modem ({(int)FallbackSequence[fallback].Modem}) " +
            $"{FallbackSequence[fallback].Modem} at {FallbackSequence[fallback].Rate}bps");

        if (string.IsNullOrEmpty(state.RxFile)) {
            state.Logging.Flow("No document to receive");
            state.CurrentStatus = T30Error.Fileerror;
            send_dcn(state);
            return;
        }

        if (state.OperationInProgress != T30Operation.T4Receive) {
            if (t4_rx.t4_rx_init(state.T4Rx, state.RxFile, state.SupportedOutputCompressions) is null) {
                state.Logging.Warning($"Cannot open target TIFF file '{state.RxFile}'");
                state.CurrentStatus = T30Error.Fileerror;
                send_dcn(state);
                return;
            }
            state.T4RxInitialized = true;
            state.OperationInProgress = T30Operation.T4Receive;
        }

        if ((state.IafMode & T30IafMode.NoTcf) == 0) {
            state.ShortTrain = false;
            set_state(state, T30StateCode.FTcf);
            queue_phase(state, T30Phase.CNonEcmRx);
            timer_t2_start(state);
        }
    }

    private static int analyze_rx_dis_dtc(T30State state, ReadOnlySpan<byte> frame) {
        if (frame.Length < 6) {
            state.Logging.Flow("Short DIS/DTC frame");
            return -1;
        }

        if ((frame[2] & 0xFE) == T30Frame.Dis)
            state.DisReceived = true;

        int length = Math.Min(frame.Length, T30State.MaxDisDtcDcsLength);
        frame[..length].CopyTo(state.FarDisDtcFrame);
        state.FarDisDtcFrame.AsSpan(length).Clear();
        state.FarDisDtcLength = length;

        state.ErrorCorrectingMode =
            state.EcmAllowed &&
            test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisEcmCapable);
        state.OctetsPerEcmFrame = 256;

        state.MutualCompressions = state.SupportedCompressions;
        if (!state.ErrorCorrectingMode) {
            state.MutualCompressions &=
                unchecked((int)0xFF800000) |
                (int)(t4_image_compression_t.T4_COMPRESSION_NONE |
                      t4_image_compression_t.T4_COMPRESSION_T4_1D |
                      t4_image_compression_t.T4_COMPRESSION_T4_2D);
            if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.Dis2dCapable))
                state.MutualCompressions &= ~(int)t4_image_compression_t.T4_COMPRESSION_T4_2D;
        } else {
            if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.Dis2dCapable))
                state.MutualCompressions &= ~(int)t4_image_compression_t.T4_COMPRESSION_T4_2D;
            if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisT6Capable))
                state.MutualCompressions &= ~(int)t4_image_compression_t.T4_COMPRESSION_T6;
            if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisT85Capable))
                state.MutualCompressions &= ~(int)(t4_image_compression_t.T4_COMPRESSION_T85 | t4_image_compression_t.T4_COMPRESSION_T85_L0);
            if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisT85L0Capable))
                state.MutualCompressions &= ~(int)t4_image_compression_t.T4_COMPRESSION_T85_L0;
            if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisFullColourCapable))
                state.MutualCompressions &= ~(int)t4_image_compression_t.T4_COMPRESSION_COLOUR;
            if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisT81Capable))
                state.MutualCompressions &= ~(int)t4_image_compression_t.T4_COMPRESSION_T42_T81;
            if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisSyccT81Capable))
                state.MutualCompressions &= ~(int)t4_image_compression_t.T4_COMPRESSION_SYCC_T81;
            if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisT43Capable))
                state.MutualCompressions &= ~(int)t4_image_compression_t.T4_COMPRESSION_T43;
            if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisT45Capable))
                state.MutualCompressions &= ~(int)t4_image_compression_t.T4_COMPRESSION_T45;
            if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.Dis12bitCapable))
                state.MutualCompressions &= ~(int)t4_image_compression_t.T4_COMPRESSION_12BIT;
            if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisNoSubsampling))
                state.MutualCompressions &= ~(int)t4_image_compression_t.T4_COMPRESSION_NO_SUBSAMPLING;
        }

        state.MutualBilevelResolutions = state.SupportedBilevelResolutions;
        state.MutualColourResolutions = state.SupportedColourResolutions;

        if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.Dis12001200Capable)) {
            state.MutualBilevelResolutions &= ~(int)t4_image_resolution_t.T4_RESOLUTION_1200_1200;
            state.MutualColourResolutions &= ~(int)t4_image_resolution_t.T4_RESOLUTION_1200_1200;
        } else if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisColourGray12001200Capable)) {
            state.MutualColourResolutions &= ~(int)t4_image_resolution_t.T4_RESOLUTION_1200_1200;
        }

        if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.Dis6001200Capable))
            state.MutualBilevelResolutions &= ~(int)t4_image_resolution_t.T4_RESOLUTION_600_1200;

        if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.Dis600600Capable)) {
            state.MutualBilevelResolutions &= ~(int)t4_image_resolution_t.T4_RESOLUTION_600_600;
            state.MutualColourResolutions &= ~(int)t4_image_resolution_t.T4_RESOLUTION_600_600;
        } else if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisColourGray600600Capable)) {
            state.MutualColourResolutions &= ~(int)t4_image_resolution_t.T4_RESOLUTION_600_600;
        }

        if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.Dis400800Capable))
            state.MutualBilevelResolutions &= ~(int)t4_image_resolution_t.T4_RESOLUTION_400_800;

        if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.Dis400400Capable)) {
            state.MutualBilevelResolutions &=
                ~(int)(t4_image_resolution_t.T4_RESOLUTION_400_400 | t4_image_resolution_t.T4_RESOLUTION_R16_SUPERFINE);
            state.MutualColourResolutions &= ~(int)t4_image_resolution_t.T4_RESOLUTION_400_400;
        } else if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisColourGray300300400400Capable)) {
            state.MutualColourResolutions &= ~(int)t4_image_resolution_t.T4_RESOLUTION_400_400;
        }

        if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.Dis300600Capable))
            state.MutualBilevelResolutions &= ~(int)t4_image_resolution_t.T4_RESOLUTION_300_600;

        if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.Dis300300Capable)) {
            state.MutualBilevelResolutions &= ~(int)t4_image_resolution_t.T4_RESOLUTION_300_300;
            state.MutualColourResolutions &= ~(int)t4_image_resolution_t.T4_RESOLUTION_300_300;
        } else if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisColourGray300300400400Capable)) {
            state.MutualColourResolutions &= ~(int)t4_image_resolution_t.T4_RESOLUTION_300_300;
        }

        if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.Dis200400Capable))
            state.MutualBilevelResolutions &=
                ~(int)(t4_image_resolution_t.T4_RESOLUTION_200_400 | t4_image_resolution_t.T4_RESOLUTION_R8_SUPERFINE);

        if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.Dis200200Capable)) {
            state.MutualBilevelResolutions &=
                ~(int)(t4_image_resolution_t.T4_RESOLUTION_200_200 | t4_image_resolution_t.T4_RESOLUTION_R8_FINE);
            state.MutualColourResolutions &= ~(int)t4_image_resolution_t.T4_RESOLUTION_200_200;
        }

        if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisInchResolutionPreferred))
            state.MutualBilevelResolutions &= ~(int)t4_image_resolution_t.T4_RESOLUTION_200_100;

        if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisColourGray100100Capable))
            state.MutualColourResolutions &= ~(int)t4_image_resolution_t.T4_RESOLUTION_100_100;

        state.MutualImageSizes = state.SupportedImageSizes;
        if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.Dis215mm255mm303mmWidthCapable)) {
            state.MutualImageSizes &= ~(int)t4_image_support_t.T4_SUPPORT_WIDTH_303MM;
            if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.Dis215mm255mmWidthCapable))
                state.MutualImageSizes &= ~(int)t4_image_support_t.T4_SUPPORT_WIDTH_255MM;
        }

        if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisUnlimitedLengthCapable)) {
            state.MutualImageSizes &= ~(int)t4_image_support_t.T4_SUPPORT_LENGTH_UNLIMITED;
            if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisA4B4LengthCapable))
                state.MutualImageSizes &= ~(int)t4_image_support_t.T4_SUPPORT_LENGTH_B4;
        }

        if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisNorthAmericanLetterCapable))
            state.MutualImageSizes &= ~(int)t4_image_support_t.T4_SUPPORT_LENGTH_US_LETTER;
        if (!test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisNorthAmericanLegalCapable))
            state.MutualImageSizes &= ~(int)t4_image_support_t.T4_SUPPORT_LENGTH_US_LEGAL;

        switch (state.FarDisDtcFrame[4] & 0x3C) {
            case 0x2C:
                if ((state.SupportedModems & T30SupportedModems.V17) != 0) {
                    state.CurrentPermittedModems =
                        T30SupportedModems.V17 |
                        T30SupportedModems.V29 |
                        T30SupportedModems.V27Ter;
                    state.CurrentFallback = 0;
                    break;
                }
                goto case 0x0C;

            case 0x0C:
                if ((state.SupportedModems & T30SupportedModems.V29) != 0) {
                    state.CurrentPermittedModems =
                        T30SupportedModems.V29 |
                        T30SupportedModems.V27Ter;
                    state.CurrentFallback = 3;
                    break;
                }
                goto case 0x08;

            case 0x08:
                state.CurrentPermittedModems = T30SupportedModems.V27Ter;
                state.CurrentFallback = 6;
                break;

            case 0x00:
                state.CurrentPermittedModems = T30SupportedModems.V27Ter;
                state.CurrentFallback = 7;
                break;

            case 0x04:
                if ((state.SupportedModems & T30SupportedModems.V29) != 0) {
                    state.CurrentPermittedModems = T30SupportedModems.V29;
                    state.CurrentFallback = 3;
                    break;
                }
                goto default;

            default:
                state.Logging.Flow("Remote does not support a compatible modem");
                state.CurrentStatus = T30Error.Incompatible;
                return -1;
        }

        state.CurrentBitRate = FallbackSequence[state.CurrentFallback].Rate;
        return 0;
    }

    private static int analyze_rx_dcs(T30State state, ReadOnlySpan<byte> frame) {
        int[,] widths = {
            {
                (int)t4_image_width_t.T4_WIDTH_100_A4,
                (int)t4_image_width_t.T4_WIDTH_100_B4,
                (int)t4_image_width_t.T4_WIDTH_100_A3,
                (int)t4_image_width_t.T4_WIDTH_100_A3
            },
            {
                (int)t4_image_width_t.T4_WIDTH_200_A4,
                (int)t4_image_width_t.T4_WIDTH_200_B4,
                (int)t4_image_width_t.T4_WIDTH_200_A3,
                (int)t4_image_width_t.T4_WIDTH_200_A3
            },
            {
                (int)t4_image_width_t.T4_WIDTH_300_A4,
                (int)t4_image_width_t.T4_WIDTH_300_B4,
                (int)t4_image_width_t.T4_WIDTH_300_A3,
                (int)t4_image_width_t.T4_WIDTH_300_A3
            },
            {
                (int)t4_image_width_t.T4_WIDTH_400_A4,
                (int)t4_image_width_t.T4_WIDTH_400_B4,
                (int)t4_image_width_t.T4_WIDTH_400_A3,
                (int)t4_image_width_t.T4_WIDTH_400_A3
            },
            {
                (int)t4_image_width_t.T4_WIDTH_600_A4,
                (int)t4_image_width_t.T4_WIDTH_600_B4,
                (int)t4_image_width_t.T4_WIDTH_600_A3,
                (int)t4_image_width_t.T4_WIDTH_600_A3
            },
            {
                (int)t4_image_width_t.T4_WIDTH_1200_A4,
                (int)t4_image_width_t.T4_WIDTH_1200_B4,
                (int)t4_image_width_t.T4_WIDTH_1200_A3,
                (int)t4_image_width_t.T4_WIDTH_1200_A3
            }
        };

        if (frame.Length < 6) {
            state.Logging.Flow("Short DCS frame");
            return -1;
        }

        state.RxDcsString = string.Join(
            " ",
            frame[3..].ToArray().Select(value =>
                global::TKFaxEngine.BitOperationsApi.bit_reverse8(value).ToString("X2")));

        Span<byte> dcsFrame = stackalloc byte[T30State.MaxDisDtcDcsLength];
        int length = Math.Min(frame.Length, dcsFrame.Length);
        frame[..length].CopyTo(dcsFrame);

        state.ErrorCorrectingMode = test_ctrl_bit(dcsFrame, T30ControlBit.DcsEcmMode);
        state.OctetsPerEcmFrame =
            test_ctrl_bit(dcsFrame, T30ControlBit.Dcs64OctetEcmFrames) ? 256 : 64;

        state.XResolution = -1;
        state.YResolution = -1;
        state.CurrentPageResolution = 0;
        state.LineCompression = -1;
        int widthRow = -1;

        bool multilevel =
            test_ctrl_bit(dcsFrame, T30ControlBit.DcsT81Mode) ||
            test_ctrl_bit(dcsFrame, T30ControlBit.DcsT43Mode) ||
            test_ctrl_bit(dcsFrame, T30ControlBit.DcsT45Mode) ||
            test_ctrl_bit(dcsFrame, T30ControlBit.DcsSyccT81Mode);

        if (multilevel) {
            if (test_ctrl_bit(dcsFrame, T30ControlBit.DcsColourGray12001200) &&
                ((t4_image_resolution_t)state.SupportedColourResolutions &
                 t4_image_resolution_t.T4_RESOLUTION_1200_1200) != 0) {
                state.XResolution = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_1200;
                state.YResolution = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_1200;
                state.CurrentPageResolution = (int)t4_image_resolution_t.T4_RESOLUTION_1200_1200;
                widthRow = 5;
            } else if (test_ctrl_bit(dcsFrame, T30ControlBit.DcsColourGray600600) &&
                       ((t4_image_resolution_t)state.SupportedColourResolutions &
                        t4_image_resolution_t.T4_RESOLUTION_600_600) != 0) {
                state.XResolution = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_600;
                state.YResolution = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_600;
                state.CurrentPageResolution = (int)t4_image_resolution_t.T4_RESOLUTION_600_600;
                widthRow = 4;
            } else if (test_ctrl_bit(dcsFrame, T30ControlBit.Dcs400400) &&
                       ((t4_image_resolution_t)state.SupportedColourResolutions &
                        t4_image_resolution_t.T4_RESOLUTION_400_400) != 0) {
                state.XResolution = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_400;
                state.YResolution = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_400;
                state.CurrentPageResolution = (int)t4_image_resolution_t.T4_RESOLUTION_400_400;
                widthRow = 3;
            } else if (test_ctrl_bit(dcsFrame, T30ControlBit.Dcs300300) &&
                       ((t4_image_resolution_t)state.SupportedColourResolutions &
                        t4_image_resolution_t.T4_RESOLUTION_300_300) != 0) {
                state.XResolution = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_300;
                state.YResolution = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_300;
                state.CurrentPageResolution = (int)t4_image_resolution_t.T4_RESOLUTION_300_300;
                widthRow = 2;
            } else if (test_ctrl_bit(dcsFrame, T30ControlBit.Dcs200200) &&
                       ((t4_image_resolution_t)state.SupportedColourResolutions &
                        t4_image_resolution_t.T4_RESOLUTION_200_200) != 0) {
                state.XResolution = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_200;
                state.YResolution = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_200;
                state.CurrentPageResolution = (int)t4_image_resolution_t.T4_RESOLUTION_200_200;
                widthRow = 1;
            } else if (test_ctrl_bit(dcsFrame, T30ControlBit.DcsColourGray100100) &&
                       ((t4_image_resolution_t)state.SupportedColourResolutions &
                        t4_image_resolution_t.T4_RESOLUTION_100_100) != 0) {
                state.XResolution = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_100;
                state.YResolution = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_100;
                state.CurrentPageResolution = (int)t4_image_resolution_t.T4_RESOLUTION_100_100;
                widthRow = 0;
            }

            if (test_ctrl_bit(dcsFrame, T30ControlBit.DcsT81Mode)) {
                if (((t4_image_compression_t)state.SupportedCompressions &
                     t4_image_compression_t.T4_COMPRESSION_T42_T81) != 0)
                    state.LineCompression = (int)t4_image_compression_t.T4_COMPRESSION_T42_T81;
            } else if (test_ctrl_bit(dcsFrame, T30ControlBit.DcsT43Mode)) {
                if (((t4_image_compression_t)state.SupportedCompressions &
                     t4_image_compression_t.T4_COMPRESSION_T43) != 0)
                    state.LineCompression = (int)t4_image_compression_t.T4_COMPRESSION_T43;
            } else if (test_ctrl_bit(dcsFrame, T30ControlBit.DcsT45Mode)) {
                if (((t4_image_compression_t)state.SupportedCompressions &
                     t4_image_compression_t.T4_COMPRESSION_T45) != 0)
                    state.LineCompression = (int)t4_image_compression_t.T4_COMPRESSION_T45;
            } else if (test_ctrl_bit(dcsFrame, T30ControlBit.DcsSyccT81Mode)) {
                if (((t4_image_compression_t)state.SupportedCompressions &
                     t4_image_compression_t.T4_COMPRESSION_SYCC_T81) != 0)
                    state.LineCompression = (int)t4_image_compression_t.T4_COMPRESSION_SYCC_T81;
            }
        } else {
            if (test_ctrl_bit(dcsFrame, T30ControlBit.Dcs12001200) &&
                ((t4_image_resolution_t)state.SupportedBilevelResolutions &
                 t4_image_resolution_t.T4_RESOLUTION_1200_1200) != 0) {
                state.XResolution = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_1200;
                state.YResolution = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_1200;
                state.CurrentPageResolution = (int)t4_image_resolution_t.T4_RESOLUTION_1200_1200;
                widthRow = 5;
            } else if (test_ctrl_bit(dcsFrame, T30ControlBit.Dcs6001200) &&
                       ((t4_image_resolution_t)state.SupportedBilevelResolutions &
                        t4_image_resolution_t.T4_RESOLUTION_600_1200) != 0) {
                state.XResolution = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_600;
                state.YResolution = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_1200;
                state.CurrentPageResolution = (int)t4_image_resolution_t.T4_RESOLUTION_600_1200;
                widthRow = 4;
            } else if (test_ctrl_bit(dcsFrame, T30ControlBit.Dcs600600) &&
                       ((t4_image_resolution_t)state.SupportedBilevelResolutions &
                        t4_image_resolution_t.T4_RESOLUTION_600_600) != 0) {
                state.XResolution = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_600;
                state.YResolution = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_600;
                state.CurrentPageResolution = (int)t4_image_resolution_t.T4_RESOLUTION_600_600;
                widthRow = 4;
            } else if (test_ctrl_bit(dcsFrame, T30ControlBit.Dcs400800) &&
                       ((t4_image_resolution_t)state.SupportedBilevelResolutions &
                        t4_image_resolution_t.T4_RESOLUTION_400_800) != 0) {
                state.XResolution = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_400;
                state.YResolution = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_800;
                state.CurrentPageResolution = (int)t4_image_resolution_t.T4_RESOLUTION_400_800;
                widthRow = 3;
            } else if (test_ctrl_bit(dcsFrame, T30ControlBit.Dcs400400)) {
                if (test_ctrl_bit(dcsFrame, T30ControlBit.DcsInchResolution) &&
                    ((t4_image_resolution_t)state.SupportedBilevelResolutions &
                     t4_image_resolution_t.T4_RESOLUTION_400_400) != 0) {
                    state.XResolution = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_400;
                    state.YResolution = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_400;
                    state.CurrentPageResolution = (int)t4_image_resolution_t.T4_RESOLUTION_400_400;
                    widthRow = 3;
                } else if (!test_ctrl_bit(dcsFrame, T30ControlBit.DcsInchResolution) &&
                           ((t4_image_resolution_t)state.SupportedBilevelResolutions &
                            t4_image_resolution_t.T4_RESOLUTION_R16_SUPERFINE) != 0) {
                    state.XResolution = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_R16;
                    state.YResolution = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_SUPERFINE;
                    state.CurrentPageResolution = (int)t4_image_resolution_t.T4_RESOLUTION_R16_SUPERFINE;
                    widthRow = 3;
                }
            } else if (test_ctrl_bit(dcsFrame, T30ControlBit.Dcs300600) &&
                       ((t4_image_resolution_t)state.SupportedBilevelResolutions &
                        t4_image_resolution_t.T4_RESOLUTION_300_600) != 0) {
                state.XResolution = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_300;
                state.YResolution = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_600;
                state.CurrentPageResolution = (int)t4_image_resolution_t.T4_RESOLUTION_300_600;
                widthRow = 2;
            } else if (test_ctrl_bit(dcsFrame, T30ControlBit.Dcs300300) &&
                       ((t4_image_resolution_t)state.SupportedBilevelResolutions &
                        t4_image_resolution_t.T4_RESOLUTION_300_300) != 0) {
                state.XResolution = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_300;
                state.YResolution = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_300;
                state.CurrentPageResolution = (int)t4_image_resolution_t.T4_RESOLUTION_300_300;
                widthRow = 2;
            } else if (test_ctrl_bit(dcsFrame, T30ControlBit.Dcs200400)) {
                if (test_ctrl_bit(dcsFrame, T30ControlBit.DcsInchResolution) &&
                    ((t4_image_resolution_t)state.SupportedBilevelResolutions &
                     t4_image_resolution_t.T4_RESOLUTION_200_400) != 0) {
                    state.XResolution = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_200;
                    state.YResolution = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_400;
                    state.CurrentPageResolution = (int)t4_image_resolution_t.T4_RESOLUTION_200_400;
                    widthRow = 1;
                } else if (!test_ctrl_bit(dcsFrame, T30ControlBit.DcsInchResolution) &&
                           ((t4_image_resolution_t)state.SupportedBilevelResolutions &
                            t4_image_resolution_t.T4_RESOLUTION_R8_SUPERFINE) != 0) {
                    state.XResolution = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_R8;
                    state.YResolution = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_SUPERFINE;
                    state.CurrentPageResolution = (int)t4_image_resolution_t.T4_RESOLUTION_R8_SUPERFINE;
                    widthRow = 1;
                }
            } else if (test_ctrl_bit(dcsFrame, T30ControlBit.Dcs200200)) {
                if (test_ctrl_bit(dcsFrame, T30ControlBit.DcsInchResolution) &&
                    ((t4_image_resolution_t)state.SupportedBilevelResolutions &
                     t4_image_resolution_t.T4_RESOLUTION_200_200) != 0) {
                    state.XResolution = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_200;
                    state.YResolution = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_200;
                    state.CurrentPageResolution = (int)t4_image_resolution_t.T4_RESOLUTION_200_200;
                    widthRow = 1;
                } else if (!test_ctrl_bit(dcsFrame, T30ControlBit.DcsInchResolution) &&
                           ((t4_image_resolution_t)state.SupportedBilevelResolutions &
                            t4_image_resolution_t.T4_RESOLUTION_R8_FINE) != 0) {
                    state.XResolution = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_R8;
                    state.YResolution = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_FINE;
                    state.CurrentPageResolution = (int)t4_image_resolution_t.T4_RESOLUTION_R8_FINE;
                    widthRow = 1;
                }
            } else if (test_ctrl_bit(dcsFrame, T30ControlBit.DcsInchResolution)) {
                state.XResolution = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_200;
                state.YResolution = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_100;
                state.CurrentPageResolution = (int)t4_image_resolution_t.T4_RESOLUTION_200_100;
                widthRow = 1;
            } else {
                state.XResolution = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_R8;
                state.YResolution = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_STANDARD;
                state.CurrentPageResolution = (int)t4_image_resolution_t.T4_RESOLUTION_R8_STANDARD;
                widthRow = 1;
            }

            if (test_ctrl_bit(dcsFrame, T30ControlBit.DcsT88Mode1) ||
                test_ctrl_bit(dcsFrame, T30ControlBit.DcsT88Mode2) ||
                test_ctrl_bit(dcsFrame, T30ControlBit.DcsT88Mode3)) {
                if (((t4_image_compression_t)state.SupportedCompressions &
                     t4_image_compression_t.T4_COMPRESSION_T88) != 0)
                    state.LineCompression = (int)t4_image_compression_t.T4_COMPRESSION_T88;
            }

            if (test_ctrl_bit(dcsFrame, T30ControlBit.DcsT85L0Mode)) {
                if (((t4_image_compression_t)state.SupportedCompressions &
                     t4_image_compression_t.T4_COMPRESSION_T85_L0) != 0)
                    state.LineCompression = (int)t4_image_compression_t.T4_COMPRESSION_T85_L0;
            } else if (test_ctrl_bit(dcsFrame, T30ControlBit.DcsT85Mode)) {
                if (((t4_image_compression_t)state.SupportedCompressions &
                     t4_image_compression_t.T4_COMPRESSION_T85) != 0)
                    state.LineCompression = (int)t4_image_compression_t.T4_COMPRESSION_T85;
            } else if (test_ctrl_bit(dcsFrame, T30ControlBit.DcsT6Mode)) {
                if (((t4_image_compression_t)state.SupportedCompressions &
                     t4_image_compression_t.T4_COMPRESSION_T6) != 0)
                    state.LineCompression = (int)t4_image_compression_t.T4_COMPRESSION_T6;
            } else if (test_ctrl_bit(dcsFrame, T30ControlBit.Dcs2dMode)) {
                if (((t4_image_compression_t)state.SupportedCompressions &
                     t4_image_compression_t.T4_COMPRESSION_T4_2D) != 0)
                    state.LineCompression = (int)t4_image_compression_t.T4_COMPRESSION_T4_2D;
            } else if (((t4_image_compression_t)state.SupportedCompressions &
                        t4_image_compression_t.T4_COMPRESSION_T4_1D) != 0) {
                state.LineCompression = (int)t4_image_compression_t.T4_COMPRESSION_T4_1D;
            }
        }

        if (state.LineCompression == -1) {
            state.CurrentStatus = T30Error.Incompatible;
            return -1;
        }

        state.Logging.Flow(
            $"Far end selected compression {t4_rx.t4_compression_to_str(state.LineCompression)} " +
            $"({state.LineCompression})");

        if (widthRow < 0) {
            state.CurrentStatus = T30Error.Noressupport;
            return -1;
        }

        state.ImageWidth = widths[widthRow, dcsFrame[5] & 0x03];

        if (!test_ctrl_bit(dcsFrame, T30ControlBit.DcsReceiveFaxDocument))
            state.Logging.Warning("Remote is not requesting receive in DCS");

        state.CurrentFallback = find_fallback_entry(dcsFrame[4] & 0x3C);
        if (state.CurrentFallback < 0) {
            state.Logging.Flow("Remote asked for a modem standard we do not support");
            return -1;
        }

        state.CurrentBitRate = FallbackSequence[state.CurrentFallback].Rate;
        return 0;
    }

    private static int build_dcs(T30State state) {
        if (state.T4TxInitialized) {
            state.CurrentPageResolution = t4_tx.t4_tx_get_tx_resolution(state.T4Tx);
            state.XResolution = t4_tx.t4_tx_get_tx_x_resolution(state.T4Tx);
            state.YResolution = t4_tx.t4_tx_get_tx_y_resolution(state.T4Tx);
            state.ImageWidth = t4_tx.t4_tx_get_tx_image_width(state.T4Tx);
            state.LineImageType = t4_tx.t4_tx_get_tx_image_type(state.T4Tx);
            state.LineCompression = t4_tx.t4_tx_get_tx_compression(state.T4Tx);
            state.LineWidthCode = t4_tx.t4_tx_get_tx_image_width_code(state.T4Tx);
        }

        Span<byte> frame = state.DcsFrame;
        frame.Clear();
        frame[0] = AddressField;
        frame[1] = ControlFinal;
        frame[2] = (byte)(T30Frame.Dcs | (state.DisReceived ? 1 : 0));
        set_ctrl_bit(frame, T30ControlBit.DcsReceiveFaxDocument);

        if (sslfax_enabled(state)
            && test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisT37)
            && test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisT38)) {
            set_ctrl_bit(frame, T30ControlBit.DcsT37);
            set_ctrl_bit(frame, T30ControlBit.DcsT38);
        }

        int fallback = Math.Clamp(state.CurrentFallback, 0, FallbackSequence.Length - 1);
        frame[4] |= FallbackSequence[fallback].DcsCode;
        state.CurrentBitRate = FallbackSequence[fallback].Rate;

        bool useBilevel = true;
        set_ctrl_bits(frame, T30ControlBit.DcsMinScanLineTime1,
            state.MinimumScanTimeCode, 4);
        switch ((t4_image_compression_t)state.LineCompression) {
            case t4_image_compression_t.T4_COMPRESSION_T4_1D:
                break;
            case t4_image_compression_t.T4_COMPRESSION_T4_2D:
                set_ctrl_bit(frame, T30ControlBit.Dcs2dMode);
                break;
            case t4_image_compression_t.T4_COMPRESSION_T6:
                set_ctrl_bit(frame, T30ControlBit.DcsT6Mode);
                break;
            case t4_image_compression_t.T4_COMPRESSION_T85:
                set_ctrl_bit(frame, T30ControlBit.DcsT85Mode);
                break;
            case t4_image_compression_t.T4_COMPRESSION_T85_L0:
                set_ctrl_bit(frame, T30ControlBit.DcsT85L0Mode);
                break;
            case t4_image_compression_t.T4_COMPRESSION_T42_T81:
                set_ctrl_bit(frame, T30ControlBit.DcsT81Mode);
                if (state.LineImageType is (int)t4_image_types_t.T4_IMAGE_TYPE_COLOUR_8BIT or (int)t4_image_types_t.T4_IMAGE_TYPE_COLOUR_12BIT)
                    set_ctrl_bit(frame, T30ControlBit.DcsFullColourMode);
                if (state.LineImageType is (int)t4_image_types_t.T4_IMAGE_TYPE_GRAY_12BIT or (int)t4_image_types_t.T4_IMAGE_TYPE_COLOUR_12BIT)
                    set_ctrl_bit(frame, T30ControlBit.Dcs12bitComponent);
                set_ctrl_bits(frame, T30ControlBit.DcsMinScanLineTime1, 0, 4);
                useBilevel = false;
                break;
            case t4_image_compression_t.T4_COMPRESSION_T43:
                set_ctrl_bit(frame, T30ControlBit.DcsT43Mode);
                if (state.LineImageType is (int)t4_image_types_t.T4_IMAGE_TYPE_COLOUR_8BIT or (int)t4_image_types_t.T4_IMAGE_TYPE_COLOUR_12BIT)
                    set_ctrl_bit(frame, T30ControlBit.DcsFullColourMode);
                if (state.LineImageType is (int)t4_image_types_t.T4_IMAGE_TYPE_GRAY_12BIT or (int)t4_image_types_t.T4_IMAGE_TYPE_COLOUR_12BIT)
                    set_ctrl_bit(frame, T30ControlBit.Dcs12bitComponent);
                set_ctrl_bits(frame, T30ControlBit.DcsMinScanLineTime1, 0, 4);
                useBilevel = false;
                break;
            case t4_image_compression_t.T4_COMPRESSION_T45:
                set_ctrl_bit(frame, T30ControlBit.DcsT45Mode);
                useBilevel = false;
                break;
            case t4_image_compression_t.T4_COMPRESSION_SYCC_T81:
                set_ctrl_bit(frame, T30ControlBit.DcsSyccT81Mode);
                useBilevel = false;
                break;
            default:
                set_ctrl_bits(frame, T30ControlBit.DcsMinScanLineTime1, 0, 4);
                break;
        }

        switch ((t4_image_support_t)state.LineWidthCode) {
            case t4_image_support_t.T4_SUPPORT_WIDTH_255MM:
                set_ctrl_bit(frame, T30ControlBit.Dcs255mmWidth);
                break;
            case t4_image_support_t.T4_SUPPORT_WIDTH_303MM:
                set_ctrl_bit(frame, T30ControlBit.Dcs303mmWidth);
                break;
        }

        t4_image_support_t mutualSizes = (t4_image_support_t)state.MutualImageSizes;
        if ((mutualSizes & t4_image_support_t.T4_SUPPORT_LENGTH_UNLIMITED) != 0)
            set_ctrl_bit(frame, T30ControlBit.DcsUnlimitedLength);
        else if ((mutualSizes & t4_image_support_t.T4_SUPPORT_LENGTH_B4) != 0)
            set_ctrl_bit(frame, T30ControlBit.DcsB4Length);
        else if ((mutualSizes & t4_image_support_t.T4_SUPPORT_LENGTH_US_LETTER) != 0)
            set_ctrl_bit(frame, T30ControlBit.DcsNorthAmericanLetter);
        else if ((mutualSizes & t4_image_support_t.T4_SUPPORT_LENGTH_US_LEGAL) != 0)
            set_ctrl_bit(frame, T30ControlBit.DcsNorthAmericanLegal);

        switch ((t4_image_resolution_t)state.CurrentPageResolution) {
            case t4_image_resolution_t.T4_RESOLUTION_1200_1200:
                set_ctrl_bit(frame, T30ControlBit.Dcs12001200);
                set_ctrl_bit(frame, T30ControlBit.DcsInchResolution);
                if (!useBilevel)
                    set_ctrl_bit(frame, T30ControlBit.DcsColourGray12001200);
                break;
            case t4_image_resolution_t.T4_RESOLUTION_600_1200:
                set_ctrl_bit(frame, T30ControlBit.Dcs6001200);
                set_ctrl_bit(frame, T30ControlBit.DcsInchResolution);
                break;
            case t4_image_resolution_t.T4_RESOLUTION_600_600:
                set_ctrl_bit(frame, T30ControlBit.Dcs600600);
                set_ctrl_bit(frame, T30ControlBit.DcsInchResolution);
                if (!useBilevel)
                    set_ctrl_bit(frame, T30ControlBit.DcsColourGray600600);
                break;
            case t4_image_resolution_t.T4_RESOLUTION_400_800:
                set_ctrl_bit(frame, T30ControlBit.Dcs400800);
                set_ctrl_bit(frame, T30ControlBit.DcsInchResolution);
                break;
            case t4_image_resolution_t.T4_RESOLUTION_400_400:
                set_ctrl_bit(frame, T30ControlBit.Dcs400400);
                set_ctrl_bit(frame, T30ControlBit.DcsInchResolution);
                if (!useBilevel)
                    set_ctrl_bit(frame, T30ControlBit.DcsColourGray300300400400);
                break;
            case t4_image_resolution_t.T4_RESOLUTION_300_600:
                set_ctrl_bit(frame, T30ControlBit.Dcs300600);
                set_ctrl_bit(frame, T30ControlBit.DcsInchResolution);
                break;
            case t4_image_resolution_t.T4_RESOLUTION_300_300:
                set_ctrl_bit(frame, T30ControlBit.Dcs300300);
                set_ctrl_bit(frame, T30ControlBit.DcsInchResolution);
                if (!useBilevel)
                    set_ctrl_bit(frame, T30ControlBit.DcsColourGray300300400400);
                break;
            case t4_image_resolution_t.T4_RESOLUTION_200_400:
                set_ctrl_bit(frame, T30ControlBit.Dcs200400);
                set_ctrl_bit(frame, T30ControlBit.DcsInchResolution);
                break;
            case t4_image_resolution_t.T4_RESOLUTION_200_200:
                set_ctrl_bit(frame, T30ControlBit.Dcs200200);
                set_ctrl_bit(frame, T30ControlBit.DcsInchResolution);
                if (!useBilevel)
                    set_ctrl_bit(frame, T30ControlBit.DcsFullColourMode);
                break;
            case t4_image_resolution_t.T4_RESOLUTION_200_100:
                set_ctrl_bit(frame, T30ControlBit.DcsInchResolution);
                break;
            case t4_image_resolution_t.T4_RESOLUTION_100_100:
                set_ctrl_bit(frame, T30ControlBit.DcsInchResolution);
                if (!useBilevel)
                    set_ctrl_bit(frame, T30ControlBit.DcsColourGray100100);
                break;
            case t4_image_resolution_t.T4_RESOLUTION_R16_SUPERFINE:
                set_ctrl_bit(frame, T30ControlBit.Dcs400400);
                break;
            case t4_image_resolution_t.T4_RESOLUTION_R8_SUPERFINE:
                set_ctrl_bit(frame, T30ControlBit.Dcs200400);
                break;
            case t4_image_resolution_t.T4_RESOLUTION_R8_FINE:
                set_ctrl_bit(frame, T30ControlBit.Dcs200200);
                break;
        }

        if (state.ErrorCorrectingMode)
            set_ctrl_bit(frame, T30ControlBit.DcsEcmMode);
        if ((state.IafMode & T30IafMode.FlowControl) != 0
            && test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisT38FlowControlCapable))
            set_ctrl_bit(frame, T30ControlBit.DcsT38FlowControlCapable);
        if ((state.IafMode & T30IafMode.ContinuousFlow) != 0
            && test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisT38FaxCapable)) {
            clr_ctrl_bit(frame, T30ControlBit.DcsModemType1);
            clr_ctrl_bit(frame, T30ControlBit.DcsModemType2);
            clr_ctrl_bit(frame, T30ControlBit.DcsModemType3);
            clr_ctrl_bit(frame, T30ControlBit.DcsModemType4);
            set_ctrl_bit(frame, T30ControlBit.DcsT38FaxMode);
        }

        state.DcsLength = 19;
        return 0;
    }

    private static int start_sending_document(T30State state) {
        if (string.IsNullOrEmpty(state.TxFile)) {
            state.Logging.Flow("No document to send");
            return -1;
        }

        state.Logging.Flow("Start sending document");
        if (t4_tx.t4_tx_init(state.T4Tx, state.TxFile, state.TxStartPage, state.TxStopPage) is null) {
            state.Logging.Warning($"Cannot open source TIFF file '{state.TxFile}'");
            state.CurrentStatus = T30Error.Fileerror;
            return -1;
        }
        state.T4TxInitialized = true;
        state.OperationInProgress = T30Operation.T4Transmit;

        t4_tx.t4_tx_set_local_ident(state.T4Tx, state.TxInfo.Ident);
        t4_tx.t4_tx_set_header_info(state.T4Tx, state.HeaderInfo);
        t4_tx.t4_tx_set_header_overlays_image(state.T4Tx, state.HeaderOverlaysImage);
        if (!string.IsNullOrEmpty(state.HeaderTimezone)) {
            try {
                t4_tx.t4_tx_set_header_tz(state.T4Tx, TimeZoneInfo.FindSystemTimeZoneById(state.HeaderTimezone));
            } catch (TimeZoneNotFoundException) {
                state.Logging.Warning($"Unknown header timezone '{state.HeaderTimezone}'.");
            } catch (InvalidTimeZoneException) {
                state.Logging.Warning($"Invalid header timezone '{state.HeaderTimezone}'.");
            }
        }

        t4_tx.t4_tx_get_pages_in_file(state.T4Tx);
        int result = t4_tx.t4_tx_set_tx_image_format(
            state.T4Tx,
            state.MutualCompressions,
            state.MutualImageSizes,
            state.MutualBilevelResolutions,
            state.MutualColourResolutions);
        if (result < 0) {
            state.CurrentStatus = result switch {
                (int)T4ImageFormatStatus.Incompatible => T30Error.Badtiffhdr,
                (int)T4ImageFormatStatus.NoSizeSupport => T30Error.Nosizesupport,
                (int)T4ImageFormatStatus.NoResolutionSupport => T30Error.Noressupport,
                _ => T30Error.Badtiff
            };
            state.Logging.Warning("Cannot negotiate the source image format");
            return -1;
        }

        state.LineImageType = t4_tx.t4_tx_get_tx_image_type(state.T4Tx);
        state.LineCompression = t4_tx.t4_tx_get_tx_compression(state.T4Tx);
        state.ImageWidth = t4_tx.t4_tx_get_tx_image_width(state.T4Tx);
        state.LineWidthCode = t4_tx.t4_tx_get_tx_image_width_code(state.T4Tx);
        state.XResolution = t4_tx.t4_tx_get_tx_x_resolution(state.T4Tx);
        state.YResolution = t4_tx.t4_tx_get_tx_y_resolution(state.T4Tx);
        state.CurrentPageResolution = t4_tx.t4_tx_get_tx_resolution(state.T4Tx);
        state.Logging.Flow(
            $"Choose image type {t4_rx.t4_image_type_to_str(state.LineImageType)} ({state.LineImageType}), " +
            $"compression {t4_rx.t4_compression_to_str(state.LineCompression)} ({state.LineCompression})");

        set_min_scan_time(state);
        if (tx_start_page(state) != 0) {
            state.Logging.Warning("Something seems to be wrong in the source file");
            state.CurrentStatus = T30Error.Badtiffhdr;
            return -1;
        }

        if (state.ErrorCorrectingMode && get_partial_ecm_page(state) == 0)
            state.Logging.Warning("No image data to send");
        return 0;
    }

    private static bool EnsureReceiveDocument(T30State state) {
        if (state.T4RxInitialized)
            return true;
        if (string.IsNullOrEmpty(state.RxFile)) {
            state.Logging.Warning("No target TIFF file is configured.");
            return false;
        }
        if (t4_rx.t4_rx_init(state.T4Rx, state.RxFile, state.SupportedOutputCompressions) is null) {
            state.Logging.Warning($"Cannot open target TIFF file '{state.RxFile}'.");
            return false;
        }
        state.T4RxInitialized = true;
        return true;
    }

    private static void OnSendStepComplete(T30State state) {
        state.Logging.Flow($"Send complete in phase {state.Phase}, state {state.State}.");
        switch (state.State) {
            case T30StateCode.Answering:
                state.Logging.Flow("Starting answer mode");
                state.DisReceived = false;
                set_phase(state, T30Phase.BTx);
                timer_t2_start(state);
                send_dis_or_dtc_sequence(state, true);
                break;

            case T30StateCode.R:
                if (send_dis_or_dtc_sequence(state, false) != 0) {
                    set_phase(state, T30Phase.BRx);
                    timer_t4_start(state);
                }
                break;

            case T30StateCode.FCfr:
                if (send_cfr_sequence(state, false) != 0) {
                    state.ImageCarrierAttempted = false;
                    state.LastRxPageResult = -1;
                    if (state.ErrorCorrectingMode) {
                        set_state(state, T30StateCode.FDocumentEcm);
                        queue_phase(state, T30Phase.CEcmRx);
                    } else {
                        set_state(state, T30StateCode.FDocumentNonEcm);
                        queue_phase(state, T30Phase.CNonEcmRx);
                    }
                    timer_t2_start(state);
                    state.NextRxStep = T30Frame.Mps;
                }
                break;

            case T30StateCode.FFtt:
                if (state.Step == 0) {
                    shut_down_hdlc_tx(state);
                    state.Step++;
                } else {
                    set_phase(state, T30Phase.BRx);
                    timer_t2_start(state);
                }
                break;

            case T30StateCode.FDocumentNonEcm:
            case T30StateCode.IIIQ:
            case T30StateCode.FPostRcpPpr:
            case T30StateCode.FPostRcpMcf:
                if (state.Step == 0) {
                    shut_down_hdlc_tx(state);
                    state.Step++;
                } else {
                    switch (state.NextRxStep) {
                        case T30Frame.PriMps:
                        case T30Frame.Mps:
                            state.ImageCarrierAttempted = false;
                            if (state.ErrorCorrectingMode) {
                                set_state(state, T30StateCode.FDocumentEcm);
                                queue_phase(state, T30Phase.CEcmRx);
                            } else {
                                set_state(state, T30StateCode.FDocumentNonEcm);
                                queue_phase(state, T30Phase.CNonEcmRx);
                            }
                            timer_t2_start(state);
                            break;
                        case T30Frame.PriEom:
                        case T30Frame.Eom:
                        case T30Frame.Eos:
                            set_phase(state, T30Phase.DRx);
                            timer_t2_start(state);
                            break;
                        case T30Frame.PriEop:
                        case T30Frame.Eop:
                            set_phase(state, T30Phase.DRx);
                            timer_t4_start(state);
                            break;
                        default:
                            state.Logging.Flow($"Unknown next rx step - {state.NextRxStep}");
                            terminate_call(state);
                            break;
                    }
                }
                break;

            case T30StateCode.IIQ:
            case T30StateCode.IVPpsNull:
            case T30StateCode.IVPpsQ:
            case T30StateCode.IVPpsRnr:
            case T30StateCode.IVEorRnr:
            case T30StateCode.FPostRcpRnr:
            case T30StateCode.IVEor:
            case T30StateCode.IVCtc:
                if (state.Step == 0) {
                    shut_down_hdlc_tx(state);
                    state.Step++;
                } else {
                    set_phase(state, T30Phase.DRx);
                    timer_t4_start(state);
                }
                break;

            case T30StateCode.B:
                terminate_call(state);
                break;

            case T30StateCode.C:
                if (state.Step == 0) {
                    shut_down_hdlc_tx(state);
                    state.Step++;
                } else {
                    start_final_pause(state);
                }
                break;

            case T30StateCode.D:
                if (send_dcs_sequence(state, false) != 0) {
                    if (state.IafMode.HasFlag(T30IafMode.NoTcf)) {
                        state.Retries = 0;
                        state.ShortTrain = true;
                        if (state.ErrorCorrectingMode) {
                            set_state(state, T30StateCode.IV);
                            queue_phase(state, T30Phase.CEcmTx);
                        } else {
                            set_state(state, T30StateCode.I);
                            queue_phase(state, T30Phase.CNonEcmTx);
                        }
                    } else {
                        state.ShortTrain = false;
                        set_state(state, T30StateCode.DTcf);
                        set_phase(state, T30Phase.CNonEcmTx);
                    }
                }
                break;

            case T30StateCode.DTcf:
                set_phase(state, T30Phase.BRx);
                timer_t4_start(state);
                set_state(state, T30StateCode.DPostTcf);
                break;

            case T30StateCode.I:
                set_phase(state, T30Phase.DTx);
                set_state(state, T30StateCode.IIQ);
                state.NextTxStep = check_next_tx_step(state);
                send_simple_frame(state, state.NextTxStep);
                break;

            case T30StateCode.IV:
                if (state.Step == 0) {
                    if (send_next_ecm_frame(state) != 0) {
                        shut_down_hdlc_tx(state);
                        state.Step++;
                    }
                } else {
                    set_phase(state, T30Phase.DTx);
                    if (state.EcmAtPageEnd)
                        state.NextTxStep = check_next_tx_step(state);
                    set_state(state, send_pps_frame(state) == T30Frame.Null
                        ? T30StateCode.IVPpsNull
                        : T30StateCode.IVPpsQ);
                }
                break;

            case T30StateCode.FDocumentEcm:
                if (state.Step == 0) {
                    shut_down_hdlc_tx(state);
                    state.Step++;
                } else {
                    queue_phase(state, T30Phase.CEcmRx);
                    timer_t2_start(state);
                }
                break;

            case T30StateCode.CallFinished:
                break;

            default:
                state.Logging.Flow($"Bad state for send complete - {state.State}.");
                break;
        }
    }

    private static void ReleaseDocumentStates(T30State state) {
        if (state.T4TxInitialized) {
            t4_tx.t4_tx_release(state.T4Tx);
            state.T4TxInitialized = false;
        }
        if (state.T4RxInitialized) {
            t4_rx.t4_rx_release(state.T4Rx);
            state.T4RxInitialized = false;
        }
        state.OperationInProgress = T30Operation.None;
    }

    private static void send_dcn(T30State state) {
        if (state.Phase == T30Phase.CallFinished)
            return;
        state.EndOfProcedureDetected = true;
        set_phase(state, T30Phase.DTx);
        set_state(state, T30StateCode.C);
        state.Step = 0;
        send_simple_frame(state, T30Frame.Dcn);
        if (state.SslFax.IsConnected)
            SslFax.sslfax_cleanup(state.SslFax, false);
    }

    private static void terminate_call(T30State state) {
        terminate_operation_in_progress(state);
        state.TimerT0T1 = 0;
        state.TimerT2T4 = 0;
        state.TimerT2T4Kind = T30TimerT2T4Kind.Idle;
        state.TimerT3 = 0;
        state.TimerT5 = 0;
        state.PhaseEHandler?.Invoke(state.PhaseEUserData, (int)state.CurrentStatus);
        set_state(state, T30StateCode.CallFinished);
        set_phase(state, T30Phase.CallFinished);
        state.Logging.Flow($"T.30 call finished: {T30Logging.t30_completion_code_to_str((int)state.CurrentStatus)}.");
    }

    private static void SetReceive(T30State state, T30ModemType type, int bitRate, int shortTrain, bool useHdlc) {
        state.CurrentRxType = type;
        state.SetRxTypeHandler?.Invoke(state.SetRxTypeUserData, type, bitRate, shortTrain, useHdlc);
    }

    private static void SetTransmit(T30State state, T30ModemType type, int bitRate, int shortTrain, bool useHdlc) {
        state.CurrentTxType = type;
        state.SetTxTypeHandler?.Invoke(state.SetTxTypeUserData, type, bitRate, shortTrain, useHdlc);
    }

    private static void queue_phase(T30State state, T30Phase phase) {
        if (state.RxSignalPresent) {
            if (state.NextPhase != T30Phase.Idle)
                state.SendHdlcHandler?.Invoke(state.SendHdlcUserData, null, -1);
            state.NextPhase = phase;
            state.Logging.Flow($"Queuing phase {phase}.");
            return;
        }
        set_phase(state, phase);
    }

    private static void set_phase(T30State state, T30Phase phase) {
        if (state.NextPhase != phase && state.NextPhase != T30Phase.Idle)
            state.SendHdlcHandler?.Invoke(state.SendHdlcUserData, null, -1);

        state.Logging.Flow($"Phase {state.Phase} -> {phase}.");
        if (state.Phase is not T30Phase.ACed and not T30Phase.ACng)
            state.RxSignalPresent = false;
        state.RxTrained = false;
        state.RxFrameReceived = false;
        state.Phase = phase;
        state.NextPhase = T30Phase.Idle;

        int fallback = Math.Clamp(state.CurrentFallback, 0, FallbackSequence.Length - 1);
        int bitRate = FallbackSequence[fallback].Rate;
        T30ModemType modem = FallbackSequence[fallback].Modem;

        switch (phase) {
            case T30Phase.Idle:
                SetReceive(state, T30ModemType.None, 0, 0, false);
                SetTransmit(state, T30ModemType.None, 0, 0, false);
                break;
            case T30Phase.ACed:
                SetReceive(state, T30ModemType.V21, 300, 0, true);
                SetTransmit(state, T30ModemType.Ced, 0, 0, false);
                break;
            case T30Phase.ACng:
                SetReceive(state, T30ModemType.V21, 300, 0, true);
                SetTransmit(state, T30ModemType.Cng, 0, 0, false);
                break;
            case T30Phase.BRx:
            case T30Phase.DRx:
                SetReceive(state, T30ModemType.V21, 300, 0, true);
                SetTransmit(state, T30ModemType.None, 0, 0, false);
                break;
            case T30Phase.BTx:
            case T30Phase.DTx:
                if (!state.FarEndDetected && state.TimerT0T1 > 0) {
                    state.TimerT0T1 = MillisecondsToSamples(DefaultTimerT1);
                    state.FarEndDetected = true;
                }
                SetReceive(state, T30ModemType.None, 0, 0, false);
                SetTransmit(state, T30ModemType.V21, 300, 0, true);
                break;
            case T30Phase.CNonEcmRx:
                SetReceive(state, T30ModemType.None, 0, 0, false);
                SetReceive(state, modem, bitRate, state.ShortTrain ? 1 : 0, false);
                SetTransmit(state, T30ModemType.None, 0, 0, false);
                break;
            case T30Phase.CNonEcmTx:
                state.TcfTestBits = checked((3 * bitRate) / 2);
                SetReceive(state, T30ModemType.None, 0, 0, false);
                SetTransmit(state, modem, bitRate, state.ShortTrain ? 1 : 0, false);
                break;
            case T30Phase.CEcmRx:
                SetReceive(state, modem, bitRate, state.ShortTrain ? 1 : 0, true);
                SetTransmit(state, T30ModemType.None, 0, 0, false);
                break;
            case T30Phase.CEcmTx:
                SetReceive(state, T30ModemType.None, 0, 0, false);
                SetTransmit(state, modem, bitRate, state.ShortTrain ? 1 : 0, true);
                break;
            case T30Phase.E:
                state.TcfTestBits = 0;
                state.TcfCurrentZeros = 0;
                state.TcfMostZeros = 0;
                SetReceive(state, T30ModemType.None, 0, 0, false);
                SetTransmit(state, T30ModemType.Pause, 0, FinalFlushTime, false);
                break;
            case T30Phase.CallFinished:
                SetReceive(state, T30ModemType.Done, 0, 0, false);
                SetTransmit(state, T30ModemType.Done, 0, 0, false);
                break;
        }
    }

    private static void set_state(T30State state, T30StateCode next) {
        state.Logging.Flow($"State {state.State} -> {next}.");
        state.State = next;
        state.Step = 0;
    }

    private static long MillisecondsToSamples(int milliseconds) => (long)milliseconds * SamplesPerSecond / 1000;

    private static long Tick(long timer, int samples, out bool expired) {
        expired = false;
        if (timer <= 0) return timer;
        timer -= samples;
        if (timer > 0) return timer;
        expired = true;
        return 0;
    }

    private static void ThrowIfDisposed(T30State state) {
        if (state.IsDisposed) throw new ObjectDisposedException(nameof(T30State));
    }
}

public static partial class T30 {
    private static void process_rx_control_msg(T30State state, ReadOnlySpan<byte> message) {
        if ((message[1] & 0x10) == 0) {
            if (state.Phase != T30Phase.CEcmRx) {
                switch (state.TimerT2T4Kind) {
                    case T30TimerT2T4Kind.T1A:
                    case T30TimerT2T4Kind.T2:
                    case T30TimerT2T4Kind.T2Flagged:
                    case T30TimerT2T4Kind.T2Dropped:
                        timer_t2_flagged_start(state);
                        break;
                    case T30TimerT2T4Kind.T4:
                    case T30TimerT2T4Kind.T4Flagged:
                    case T30TimerT2T4Kind.T4Dropped:
                        timer_t4_flagged_start(state);
                        break;
                }
            }

            byte normalized = (byte)(message[2] & 0xFE);
            switch (normalized) {
                case T30Frame.Csi:
                    state.RxInfo.Ident = decode_20digit_msg(state, message[2..]);
                    break;

                case T30Frame.Nsf:
                    if (message[2] == T30Frame.Nsf) {
                        ReadOnlySpan<byte> payload = message.Length > 3 ? message[3..] : ReadOnlySpan<byte>.Empty;
                        T35.t35_decode(payload, out string? country, out string? vendor, out string? model);
                        state.Country = country;
                        state.Vendor = vendor;
                        state.Model = model;
                        if (country is not null) state.Logging.Flow($"The remote was made in '{country}'");
                        if (vendor is not null) state.Logging.Flow($"The remote was made by '{vendor}'");
                        if (model is not null) state.Logging.Flow($"The remote is a '{model}'");
                        state.RxInfo.Nsf = decode_nsf_nss_nsc(message[2..]);
                    } else {
                        state.RxInfo.Nsc = decode_nsf_nss_nsc(message[2..]);
                    }
                    break;

                case (T30Frame.Pwd & 0xFE):
                    if (message[2] == T30Frame.Pwd)
                        state.RxInfo.Password = decode_20digit_msg(state, message[2..]);
                    else
                        unexpected_non_final_frame(state, message);
                    break;

                case (T30Frame.Sep & 0xFE):
                    if (message[2] == T30Frame.Sep)
                        state.RxInfo.SelectivePollingAddress = decode_20digit_msg(state, message[2..]);
                    else
                        unexpected_non_final_frame(state, message);
                    break;

                case (T30Frame.Psa & 0xFE):
                    if (message[2] == T30Frame.Psa)
                        state.RxInfo.PolledSubAddress = decode_20digit_msg(state, message[2..]);
                    else
                        unexpected_non_final_frame(state, message);
                    break;

                case (T30Frame.Cia & 0xFE):
                    if (message[2] == T30Frame.Cia) {
                        decode_url_msg(state, message[2..], out int ciaType, out string? cia);
                        state.RxInfo.CiaType = ciaType;
                        state.RxInfo.Cia = cia;
                        state.RxInfo.CiaLength = cia?.Length ?? 0;
                    } else {
                        unexpected_non_final_frame(state, message);
                    }
                    break;

                case (T30Frame.Isp & 0xFE):
                    if (message[2] == T30Frame.Isp) {
                        decode_url_msg(state, message[2..], out int ispType, out string? isp);
                        state.RxInfo.IspType = ispType;
                        state.RxInfo.Isp = isp;
                        state.RxInfo.IspLength = isp?.Length ?? 0;
                    } else {
                        unexpected_non_final_frame(state, message);
                    }
                    break;

                case (T30Frame.Tsi & 0xFE):
                    state.RxInfo.Ident = decode_20digit_msg(state, message[2..]);
                    break;

                case (T30Frame.Nss & 0xFE):
                    state.RxInfo.Nss = decode_nsf_nss_nsc(message[2..]);
                    break;

                case (T30Frame.Sub & 0xFE):
                    state.RxInfo.SubAddress = decode_20digit_msg(state, message[2..]);
                    break;

                case (T30Frame.Sid & 0xFE):
                    state.RxInfo.SenderIdent = decode_20digit_msg(state, message[2..]);
                    break;

                case (T30Frame.Csa & 0xFE):
                    decode_url_msg(state, message[2..], out int csaType, out string? csa);
                    state.RxInfo.CsaType = csaType;
                    state.RxInfo.Csa = csa;
                    state.RxInfo.CsaLength = csa?.Length ?? 0;
                    break;

                case (T30Frame.Tsa & 0xFE):
                    decode_url_msg(state, message[2..], out int tsaType, out string? tsa);
                    state.RxInfo.TsaType = tsaType;
                    state.RxInfo.Tsa = tsa;
                    state.RxInfo.TsaLength = tsa?.Length ?? 0;
                    break;

                case (T30Frame.Ira & 0xFE):
                    decode_url_msg(state, message[2..], out int iraType, out string? ira);
                    state.RxInfo.IraType = iraType;
                    state.RxInfo.Ira = ira;
                    state.RxInfo.IraLength = ira?.Length ?? 0;
                    break;

                case T30Frame.Fcd:
                    process_rx_fcd(state, message);
                    break;

                case T30Frame.Rcp:
                    process_rx_rcp(state, message);
                    break;

                default:
                    unexpected_non_final_frame(state, message);
                    break;
            }
            return;
        }

        state.TimerT0T1 = 0;
        state.Logging.Flow($"Rx final frame in state {state.State}");
        process_final_frame_by_state(state, message);
    }

    private static string decode_20digit_msg(T30State state, ReadOnlySpan<byte> packet) {
        if (packet.Length > T30State.MaxIdentLength + 1) {
            unexpected_frame_length(state, packet);
            return string.Empty;
        }

        int end = packet.Length;
        while (end > 1 && packet[end - 1] == (byte)' ')
            end--;

        Span<char> chars = stackalloc char[Math.Max(0, end - 1)];
        int output = 0;
        while (end > 1)
            chars[output++] = (char)packet[--end];

        string result = chars[..output].ToString();
        byte fcf = packet.Length > 0 ? packet[0] : (byte)0;
        state.Logging.Flow($"Remote gave {T30Logging.t30_frametype(fcf)} as: \"{result}\"");
        return result;
    }

    private static void decode_url_msg(
        T30State state,
        ReadOnlySpan<byte> packet,
        out int addressType,
        out string? address) {
        addressType = 0;
        address = null;
        if (packet.Length < 4 || packet.Length > 81 || packet.Length != packet[3] + 4) {
            unexpected_frame_length(state, packet);
            return;
        }

        addressType = packet[2] & 0x0F;
        address = Encoding.ASCII.GetString(packet[4..]);
        state.Logging.Flow(
            $"Remote fax gave {T30Logging.t30_frametype(packet[0])} as: {packet[1]}, {packet[2]}, \"{address}\"");

        if (sslfax_enabled(state)
            && addressType == 0x02
            && address.Length > 6
            && address.StartsWith("ssl://", StringComparison.Ordinal)) {
            state.SslFax.Url = address[6..];
        }
    }

    private static byte[] decode_nsf_nss_nsc(ReadOnlySpan<byte> packet) {
        return packet.Length > 1 ? packet[1..].ToArray() : Array.Empty<byte>();
    }

    private static void t30_hdlc_rx_status(T30State state, SignalStatus status) {
        state.Logging.Flow($"HDLC signal status is {status} ({(int)status}) in state {state.State}");
        switch (status) {
            case SignalStatus.TrainingInProgress:
                break;

            case SignalStatus.TrainingFailed:
                state.RxTrained = false;
                break;

            case SignalStatus.TrainingSucceeded:
                state.RxSignalPresent = true;
                state.RxTrained = true;
                break;

            case SignalStatus.CarrierUp:
                state.RxSignalPresent = true;
                switch (state.TimerT2T4Kind) {
                    case T30TimerT2T4Kind.T2Dropped:
                        timer_t2_t4_stop(state);
                        state.TimerT2T4Kind = T30TimerT2T4Kind.T2C;
                        break;
                    case T30TimerT2T4Kind.T4Dropped:
                        timer_t2_t4_stop(state);
                        state.TimerT2T4Kind = T30TimerT2T4Kind.T4C;
                        break;
                }
                break;

            case SignalStatus.CarrierDown:
                bool wasTrained = state.RxTrained;
                state.RxSignalPresent = false;
                state.RxTrained = false;

                if (sslfax_enabled(state)
                    && !string.IsNullOrEmpty(state.SslFax.Url)
                    && !state.SslFax.IsConnected
                    && state.State is T30StateCode.I or T30StateCode.IV) {
                    SslFax.sslfax_start_client(state.SslFax);
                    if (state.SslFax.IsConnected) {
                        state.RealTimeFrameHandler = t30_sslfax_real_time_frame_handler;
                        state.RealTimeFrameUserData = state;
                        state.EcmCurrentTransmitFrame = 0;
                        state.EcmFramesThisTransmitBurst = 0;
                        if (state.State == T30StateCode.IV) {
                            state.Step = 0;
                            state.SslFax.DoUnderflow = true;
                        }
                    }
                }

                if (state.State == T30StateCode.FDocumentEcm) {
                    if (wasTrained) {
                        state.Logging.Warning("ECM signal did not end cleanly");
                        set_state(state, T30StateCode.FPostDocumentEcm);
                        queue_phase(state, T30Phase.DRx);
                        timer_t2_start(state);
                        if (state.CurrentStatus == T30Error.RxNocarrier)
                            state.CurrentStatus = T30Error.Ok;
                    } else {
                        state.Logging.Warning("ECM carrier not found");
                        state.CurrentStatus = T30Error.RxNocarrier;
                    }
                }

                if (state.NextPhase != T30Phase.Idle) {
                    set_phase(state, state.NextPhase);
                } else {
                    switch (state.TimerT2T4Kind) {
                        case T30TimerT2T4Kind.T1A:
                        case T30TimerT2T4Kind.T2Flagged:
                        case T30TimerT2T4Kind.T2C:
                            timer_t2_dropped_start(state);
                            break;
                        case T30TimerT2T4Kind.T4Flagged:
                        case T30TimerT2T4Kind.T4C:
                            timer_t4_dropped_start(state);
                            break;
                    }
                }
                break;

            case SignalStatus.FramingOk:
                if (!state.FarEndDetected && state.TimerT0T1 > 0) {
                    state.TimerT0T1 = MillisecondsToSamples(DefaultTimerT1);
                    state.FarEndDetected = true;
                    if (state.Phase is T30Phase.ACed or T30Phase.ACng)
                        set_phase(state, T30Phase.BRx);
                }

                if (state.TimerT2T4 > 0) {
                    switch (state.TimerT2T4Kind) {
                        case T30TimerT2T4Kind.T1A:
                        case T30TimerT2T4Kind.T2:
                        case T30TimerT2T4Kind.T2Flagged:
                            timer_t2_flagged_start(state);
                            break;
                        case T30TimerT2T4Kind.T4:
                        case T30TimerT2T4Kind.T4Flagged:
                            timer_t4_flagged_start(state);
                            break;
                    }
                }
                break;

            case SignalStatus.Abort:
                break;

            default:
                state.Logging.Flow($"Unexpected HDLC special length - {(int)status}!");
                break;
        }
    }
}

public static partial class T30 {
    private const int T30CopyQualityPerfect = 0;
    private const int T30CopyQualityGood = 1;
    private const int T30CopyQualityPoor = 2;
    private const int T30CopyQualityBad = 3;

    private static int find_fallback_entry(int dcsCode) {
        for (int i = 0; i < FallbackSequence.Length; i++) {
            if (FallbackSequence[i].DcsCode == dcsCode)
                return i;
        }
        return -1;
    }

    private static int step_fallback_entry(T30State state) {
        do {
            state.CurrentFallback++;
            if (state.CurrentFallback >= FallbackSequence.Length) {
                state.CurrentFallback = 0;
                return -1;
            }
        } while ((FallbackSequence[state.CurrentFallback].Required & state.CurrentPermittedModems) == 0);

        set_min_scan_time(state);
        build_dcs(state);
        return state.CurrentFallback;
    }


    private static void set_min_scan_time(T30State state) {
        ReadOnlySpan<byte> translateNormal = [0, 1, 2, 0, 4, 4, 2, 7];
        ReadOnlySpan<byte> translateFine = [0, 1, 2, 2, 4, 0, 1, 7];
        ReadOnlySpan<byte> translateSuperfine = [2, 1, 1, 1, 0, 2, 1, 7];
        ReadOnlySpan<int> minimumScanTimes = [20, 5, 10, 0, 40, 0, 0, 0];

        int minimumBitsField = state.ErrorCorrectingMode
            ? 7
            : state.FarDisDtcFrame.Length > 5
                ? (state.FarDisDtcFrame[5] >> 4) & 7
                : 7;

        state.MinimumScanTimeCode = (t4_image_y_resolution_t)state.YResolution switch {
            t4_image_y_resolution_t.T4_Y_RESOLUTION_SUPERFINE or t4_image_y_resolution_t.T4_Y_RESOLUTION_400 =>
                test_ctrl_bit(state.FarDisDtcFrame, 46)
                    ? translateSuperfine[minimumBitsField]
                    : translateFine[minimumBitsField],
            t4_image_y_resolution_t.T4_Y_RESOLUTION_FINE or t4_image_y_resolution_t.T4_Y_RESOLUTION_200 =>
                translateFine[minimumBitsField],
            t4_image_y_resolution_t.T4_Y_RESOLUTION_STANDARD or t4_image_y_resolution_t.T4_Y_RESOLUTION_100 =>
                translateNormal[minimumBitsField],
            _ => 7
        };

        int fallback = Math.Clamp(state.CurrentFallback, 0, FallbackSequence.Length - 1);
        int minimumRowBits = (state.IafMode & T30IafMode.NoFillBits) != 0
            ? 0
            : FallbackSequence[fallback].Rate * minimumScanTimes[state.MinimumScanTimeCode] / 1000;

        state.Logging.Flow($"Minimum bits per row will be {minimumRowBits}");
        t4_tx.t4_tx_set_min_bits_per_row(state.T4Tx, minimumRowBits);
    }

    private static bool test_ctrl_bit(ReadOnlySpan<byte> frame, int bit) {
        int index = 3 + ((bit - 1) >> 3);
        return (uint)index < (uint)frame.Length
            && (frame[index] & (1 << ((bit - 1) & 7))) != 0;
    }

    private static int terminate_operation_in_progress(T30State state) {
        switch (state.OperationInProgress) {
            case T30Operation.T4Transmit:
                t4_tx.t4_tx_release(state.T4Tx);
                state.T4TxInitialized = false;
                state.OperationInProgress = T30Operation.PostT4Transmit;
                break;

            case T30Operation.T4Receive:
                t4_rx.t4_rx_release(state.T4Rx);
                state.T4RxInitialized = false;
                state.OperationInProgress = T30Operation.PostT4Receive;
                break;
        }
        return 0;
    }

    private static int tx_start_page(T30State state) {
        if (t4_tx.t4_tx_start_page(state.T4Tx) != 0) {
            terminate_operation_in_progress(state);
            return -1;
        }
        state.EcmBlock = 0;
        state.ErrorCorrectingModeRetries = 0;
        state.Logging.Flow($"Starting page {state.TxPageNumber + 1} of transfer");
        return 0;
    }

    private static int tx_end_page(T30State state) {
        state.Retries = 0;
        if (t4_tx.t4_tx_end_page(state.T4Tx) == 0) {
            state.TxPageNumber++;
            state.EcmBlock = 0;
        }
        return 0;
    }

    private static int rx_start_page(T30State state) {
        t4_rx.t4_rx_set_image_width(state.T4Rx, state.ImageWidth);
        t4_rx.t4_rx_set_sub_address(state.T4Rx, state.RxInfo.SubAddress);
        t4_rx.t4_rx_set_dcs(state.T4Rx, state.RxDcsString);
        t4_rx.t4_rx_set_far_ident(state.T4Rx, state.RxInfo.Ident);
        t4_rx.t4_rx_set_vendor(state.T4Rx, state.Vendor);
        t4_rx.t4_rx_set_model(state.T4Rx, state.Model);
        t4_rx.t4_rx_set_rx_encoding(state.T4Rx, state.LineCompression);
        t4_rx.t4_rx_set_x_resolution(state.T4Rx, state.XResolution);
        t4_rx.t4_rx_set_y_resolution(state.T4Rx, state.YResolution);

        if (t4_rx.t4_rx_start_page(state.T4Rx) != 0)
            return -1;

        Array.Fill(state.EcmLength, (short)-1);
        state.EcmBlock = 0;
        state.EcmFrames = -1;
        state.EcmFramesThisTransmitBurst = 0;
        state.ErrorCorrectingModeRetries = 0;
        return 0;
    }

    private static int rx_end_page(T30State state) {
        if (t4_rx.t4_rx_end_page(state.T4Rx) == 0) {
            state.RxPageNumber++;
            state.EcmBlock = 0;
        }
        return 0;
    }

    private static void report_rx_ecm_page_result(T30State state) {
        t4_stats_t stats = new();
        t4_rx.t4_rx_get_transfer_statistics(state.T4Rx, stats);
        state.Logging.Flow($"Page no = {stats.pages_transferred}");
        state.Logging.Flow($"Image size = {stats.width} x {stats.length} pixels");
        state.Logging.Flow($"Image resolution = {stats.x_resolution}/m x {stats.y_resolution}/m");
        state.Logging.Flow($"Compression = {t4_rx.t4_compression_to_str((int)stats.compression)} ({stats.compression})");
        state.Logging.Flow($"Compressed image size = {stats.line_image_size} bytes");
    }

    private static int copy_quality(T30State state) {
        t4_stats_t stats = new();
        t4_rx.t4_rx_get_transfer_statistics(state.T4Rx, stats);
        state.Logging.Flow($"Page no = {stats.pages_transferred + 1}");
        state.Logging.Flow($"Image size = {stats.width} x {stats.length} pixels");
        state.Logging.Flow($"Image resolution = {stats.x_resolution}/m x {stats.y_resolution}/m");
        state.Logging.Flow($"Compression = {t4_rx.t4_compression_to_str((int)stats.compression)} ({stats.compression})");
        state.Logging.Flow($"Compressed image size = {stats.line_image_size} bytes");
        state.Logging.Flow($"Bad rows = {stats.bad_rows}");
        state.Logging.Flow($"Longest bad row run = {stats.longest_bad_row_run}");

        if (stats.bad_rows == 0 && stats.length != 0) {
            state.Logging.Flow("Page quality is perfect");
            return T30CopyQualityPerfect;
        }
        if (stats.bad_rows * 20 < stats.length) {
            state.Logging.Flow("Page quality is good");
            return T30CopyQualityGood;
        }
        if (stats.bad_rows * 20 < stats.length * 3) {
            state.Logging.Flow("Page quality is poor");
            return T30CopyQualityPoor;
        }

        state.Logging.Flow("Page quality is bad");
        return T30CopyQualityBad;
    }

    private static void report_tx_result(T30State state, int result) {
        t4_stats_t stats = new();
        t4_tx.t4_tx_get_transfer_statistics(state.T4Tx, stats);
        state.Logging.Flow($"{(result != 0 ? "Success" : "Failure")} - delivered {stats.pages_transferred} pages");
    }

    private static void release_resources(T30State state) {
        state.TxInfo.Nsf = Array.Empty<byte>();
        state.TxInfo.Nsc = Array.Empty<byte>();
        state.TxInfo.Nss = Array.Empty<byte>();
        state.TxInfo.Tsa = null;
        state.TxInfo.Ira = null;
        state.TxInfo.Cia = null;
        state.TxInfo.Isp = null;
        state.TxInfo.Csa = null;

        state.RxInfo.Nsf = Array.Empty<byte>();
        state.RxInfo.Nsc = Array.Empty<byte>();
        state.RxInfo.Nss = Array.Empty<byte>();
        state.RxInfo.Tsa = null;
        state.RxInfo.Ira = null;
        state.RxInfo.Cia = null;
        state.RxInfo.Isp = null;
        state.RxInfo.Csa = null;

        if (state.SslFax.IsConnected)
            SslFax.sslfax_cleanup(state.SslFax, false);
    }

    private static byte check_next_tx_step(T30State state) {
        int result = t4_tx.t4_tx_next_page_has_different_format(state.T4Tx);
        if (result == 0) {
            state.Logging.Flow("More pages to come with the same format");
            return state.LocalInterruptPending ? T30Frame.PriMps : T30Frame.Mps;
        }
        if (result > 0) {
            state.Logging.Flow("More pages to come with a different format");
            state.TxStartPage = t4_tx.t4_tx_get_current_page_in_file(state.T4Tx) + 1;
            return state.LocalInterruptPending ? T30Frame.PriEom : T30Frame.Eom;
        }

        int more = state.DocumentHandler?.Invoke(state.DocumentUserData, 0) ?? 0;
        if (more != 0) {
            state.Logging.Flow("Another document to send");
            return state.LocalInterruptPending ? T30Frame.PriEom : T30Frame.Eom;
        }

        state.Logging.Flow("No more pages to send");
        return state.LocalInterruptPending ? T30Frame.PriEop : T30Frame.Eop;
    }

    private static int get_partial_ecm_page(T30State state) {
        state.PprCount = 0;
        state.EcmProgress = 0;
        for (int i = 3; i < 35; i++)
            state.EcmFrameMap[i] = 0xFF;

        for (int i = 0; i < T30State.MaxEcmFrames; i++) {
            state.EcmLength[i] = -1;
            byte[] frame = state.EcmData[i];
            frame[0] = AddressField;
            frame[1] = ControlNonFinal;
            frame[2] = T30Frame.Fcd;
            frame[3] = (byte)i;

            int length = state.DocumentGetHandler is not null
                ? state.DocumentGetHandler(state.DocumentGetUserData, frame.AsMemory(4, state.OctetsPerEcmFrame))
                : t4_tx.t4_tx_get(state.T4Tx, frame.AsSpan(4, state.OctetsPerEcmFrame), state.OctetsPerEcmFrame);

            if (length < state.OctetsPerEcmFrame) {
                if (length > 0) {
                    frame.AsSpan(4 + length, state.OctetsPerEcmFrame - length).Clear();
                    state.EcmLength[i] = (short)(state.OctetsPerEcmFrame + 4);
                    i++;
                }
                state.EcmFrames = i;
                state.Logging.Flow($"Partial document buffer contains {i} frames ({state.OctetsPerEcmFrame} per frame)");
                state.EcmAtPageEnd = true;
                return i;
            }

            state.EcmLength[i] = (short)(4 + length);
        }

        state.EcmFrames = T30State.MaxEcmFrames;
        state.Logging.Flow($"Partial page buffer full ({state.OctetsPerEcmFrame} per frame)");
        state.EcmAtPageEnd = t4_tx.t4_tx_image_complete(state.T4Tx) == (int)SignalStatus.EndOfData;
        return T30State.MaxEcmFrames;
    }

    private static int send_next_ecm_frame(T30State state) {
        if (state.EcmCurrentTransmitFrame < state.EcmFrames) {
            for (int i = state.EcmCurrentTransmitFrame; i < state.EcmFrames; i++) {
                if (state.EcmLength[i] < 0)
                    continue;
                send_frame(state, state.EcmData[i], state.EcmLength[i]);
                state.EcmCurrentTransmitFrame = i + 1;
                state.EcmFramesThisTransmitBurst++;
                return 0;
            }
            state.EcmCurrentTransmitFrame = state.EcmFrames;
        }

        if (state.EcmCurrentTransmitFrame < state.EcmFrames + 3) {
            state.EcmCurrentTransmitFrame++;
            Span<byte> frame = stackalloc byte[3] { AddressField, ControlNonFinal, T30Frame.Rcp };
            send_frame(state, frame);
            state.ShortTrain = true;
            return 0;
        }
        return -1;
    }

    private static void send_rr(T30State state) {
        if (state.CurrentStatus != T30Error.TxT5exp)
            send_simple_frame(state, T30Frame.Rr);
        else
            send_dcn(state);
    }

    private static int send_first_ecm_frame(T30State state) {
        state.EcmCurrentTransmitFrame = 0;
        state.EcmFramesThisTransmitBurst = 0;
        return send_next_ecm_frame(state);
    }

    private static void print_frame(T30State state, string io, ReadOnlySpan<byte> message) {
        if (message.Length < 3)
            return;
        state.Logging.Flow($"{io} {T30Logging.t30_frametype(message[2])} with{((message[1] & 0x10) != 0 ? string.Empty : "out")} final frame tag");
        state.Logging.Flow($"{io} {Convert.ToHexString(message)}");
    }

    private static void shut_down_hdlc_tx(T30State state) {
        state.SendHdlcHandler?.Invoke(state.SendHdlcUserData, null, 0);
    }

    private static void send_frame(T30State state, byte[] frame, int length) {
        if ((uint)length > (uint)frame.Length)
            throw new ArgumentOutOfRangeException(nameof(length));
        byte[] transmitted = frame.AsSpan(0, length).ToArray();
        state.LastTransmittedFrame = transmitted;
        state.LastTransmittedFrameLength = transmitted.Length;
        print_frame(state, "Tx", transmitted);
        state.RealTimeFrameHandler?.Invoke(state.RealTimeFrameUserData, false, transmitted);
        state.SendHdlcHandler?.Invoke(state.SendHdlcUserData, transmitted, transmitted.Length);
    }

    private static void send_frame(T30State state, ReadOnlySpan<byte> frame) {
        byte[] transmitted = frame.ToArray();
        state.LastTransmittedFrame = transmitted;
        state.LastTransmittedFrameLength = transmitted.Length;
        print_frame(state, "Tx", transmitted);
        state.RealTimeFrameHandler?.Invoke(state.RealTimeFrameUserData, false, transmitted);
        state.SendHdlcHandler?.Invoke(state.SendHdlcUserData, transmitted, transmitted.Length);
    }

    private static void send_simple_frame(T30State state, int type) {
        Span<byte> frame = stackalloc byte[3] {
            AddressField,
            ControlFinal,
            (byte)(type | (state.DisReceived ? 1 : 0))
        };
        send_frame(state, frame);
    }
}

public static partial class T30 {
    private const byte DcsModemMask = 0x3C;

    private static byte get_frame_type(ReadOnlySpan<byte> message) {
        if (message.Length >= 3 && message[0] == AddressField)
            return message[2];
        return message.Length > 0 ? message[0] : (byte)0;
    }

    private static void unexpected_non_final_frame(T30State state, ReadOnlySpan<byte> message) {
        byte fcf = get_frame_type(message);
        state.Logging.Flow($"Unexpected {T30Logging.t30_frametype(fcf)} frame in state {state.State}");
        if (state.CurrentStatus == T30Error.Ok)
            state.CurrentStatus = T30Error.Unexpected;
    }

    private static void unexpected_final_frame(T30State state, ReadOnlySpan<byte> message) {
        byte fcf = get_frame_type(message);
        state.Logging.Flow($"Unexpected {T30Logging.t30_frametype(fcf)} frame in state {state.State}");
        if (state.CurrentStatus == T30Error.Ok)
            state.CurrentStatus = T30Error.Unexpected;
        send_dcn(state);
    }

    private static void unexpected_frame_length(T30State state, ReadOnlySpan<byte> message) {
        byte fcf = get_frame_type(message);
        state.Logging.Flow($"Unexpected {T30Logging.t30_frametype(fcf)} frame length - {message.Length}");
        if (state.CurrentStatus == T30Error.Ok)
            state.CurrentStatus = T30Error.Unexpected;
        send_dcn(state);
    }

    private static void process_rx_fnv(T30State state, ReadOnlySpan<byte> message) {
        if (message.Length > 3) {
            byte flags = message[3];
            if ((flags & 0x01) != 0) state.Logging.Flow("  Incorrect password (PWD).");
            if ((flags & 0x02) != 0) state.Logging.Flow("  Selective polling reference (SEP) not known.");
            if ((flags & 0x04) != 0) state.Logging.Flow("  Sub-address (SUB) not known.");
            if ((flags & 0x08) != 0) state.Logging.Flow("  Sender identity (SID) not known.");
            if ((flags & 0x10) != 0) state.Logging.Flow("  Secure fax error.");
            if ((flags & 0x20) != 0) state.Logging.Flow("  Transmitting subscriber identity (TSI) not accepted.");
            if ((flags & 0x40) != 0) state.Logging.Flow("  Polled sub-address (PSA) not known.");
        }
        if (message.Length > 4 && (message[3] & 0x80) != 0) {
            if ((message[4] & 0x01) != 0) state.Logging.Flow("  BFT negotiations request not accepted.");
            if ((message[4] & 0x02) != 0) state.Logging.Flow("  Internet routing address (IRA) not known.");
            if ((message[4] & 0x04) != 0) state.Logging.Flow("  Internet selective polling address (ISP) not known.");
        }
        if (message.Length > 5)
            state.Logging.Flow($"  FNV sequence number {message[5]}.");
        if (message.Length > 6)
            state.Logging.Flow($"  FNV diagnostic info type 0x{message[6]:X2}.");
        if (message.Length > 7)
            state.Logging.Flow($"  FNV length {message[7]}.");
        unexpected_final_frame(state, message);
    }

    private static void return_to_phase_b(T30State state, bool withFallback) {
        _ = withFallback;
        state.Logging.Warning("Returning to phase B");
        state.TimerT0T1 = MillisecondsToSamples(DefaultTimerT1);
        set_state(state, state.CallingParty ? T30StateCode.T : T30StateCode.R);
    }

    private static bool process_ecm_final_frame_by_state(T30State state, ReadOnlySpan<byte> message) {
        switch (state.State) {
            case T30StateCode.FDocumentEcm:
            case T30StateCode.FPostDocumentEcm:
                process_state_f_doc_and_post_doc_ecm(state, message);
                return true;
            case T30StateCode.FPostRcpMcf:
                process_state_f_post_rcp_mcf(state, message);
                return true;
            case T30StateCode.FPostRcpPpr:
                process_state_f_post_rcp_ppr(state, message);
                return true;
            case T30StateCode.FPostRcpRnr:
                process_state_f_post_rcp_rnr(state, message);
                return true;
            case T30StateCode.IV:
                process_state_iv(state, message);
                return true;
            case T30StateCode.IVPpsNull:
                process_state_iv_pps_null(state, message);
                return true;
            case T30StateCode.IVPpsQ:
                process_state_iv_pps_q(state, message);
                return true;
            case T30StateCode.IVPpsRnr:
                process_state_iv_pps_rnr(state, message);
                return true;
            case T30StateCode.IVCtc:
                process_state_iv_ctc(state, message);
                return true;
            case T30StateCode.IVEor:
                process_state_iv_eor(state, message);
                return true;
            case T30StateCode.IVEorRnr:
                process_state_iv_eor_rnr(state, message);
                return true;
            default:
                return false;
        }
    }

    private static void process_state_f_doc_and_post_doc_ecm(T30State state, ReadOnlySpan<byte> message) {
        byte fcf = (byte)(message[2] & 0xFE);
        switch (fcf) {
            case T30Frame.Dis:
                process_rx_dis_dtc(state, message);
                break;
            case T30Frame.Dcs:
                process_rx_dcs(state, message);
                break;
            case T30Frame.Rcp:
                process_rx_rcp(state, message);
                break;
            case T30Frame.Eor:
                if (message.Length != 4) {
                    unexpected_frame_length(state, message);
                    break;
                }
                byte fcf2 = (byte)(message[3] & 0xFE);
                state.Logging.Flow($"Received EOR + {T30Logging.t30_frametype(message[3])}");
                switch (fcf2) {
                    case T30Frame.PriEop:
                    case T30Frame.PriEom:
                    case T30Frame.PriMps:
                    case T30Frame.Null:
                    case T30Frame.Eop:
                    case T30Frame.Eom:
                    case T30Frame.Eos:
                    case T30Frame.Mps:
                        state.ImageCarrierAttempted = false;
                        state.NextRxStep = fcf2;
                        queue_phase(state, T30Phase.DTx);
                        set_state(state, T30StateCode.FDocumentEcm);
                        send_simple_frame(state, T30Frame.Err);
                        break;
                    default:
                        unexpected_final_frame(state, message);
                        break;
                }
                break;
            case T30Frame.Pps:
                process_rx_pps(state, message);
                break;
            case T30Frame.Ctc:
                if (message.Length < 5) {
                    unexpected_frame_length(state, message);
                    break;
                }
                int requestedCode = message[4] & DcsModemMask;
                int currentCode = FallbackSequence[Math.Clamp(state.CurrentFallback, 0, FallbackSequence.Length - 1)].DcsCode;
                if (requestedCode != currentCode) {
                    state.Logging.Flow("Modem changed in CTC.");
                    int fallback = find_fallback_entry(requestedCode);
                    if (fallback < 0)
                        state.Logging.Flow("Remote asked for a modem standard we do not support");
                    else
                        state.CurrentFallback = fallback;
                }
                state.ImageCarrierAttempted = false;
                state.ShortTrain = false;
                queue_phase(state, T30Phase.DTx);
                set_state(state, T30StateCode.FDocumentEcm);
                send_simple_frame(state, T30Frame.Ctr);
                break;
            case T30Frame.Rr:
                break;
            case T30Frame.Dcn:
                state.CurrentStatus = T30Error.RxDcndata;
                terminate_call(state);
                break;
            case T30Frame.Crp:
                repeat_last_command(state);
                break;
            case T30Frame.Fnv:
                process_rx_fnv(state, message);
                break;
            default:
                state.CurrentStatus = T30Error.RxInvalcmd;
                unexpected_final_frame(state, message);
                break;
        }
    }

    private static void process_state_f_post_rcp_mcf(T30State state, ReadOnlySpan<byte> message) {
        switch ((byte)(message[2] & 0xFE)) {
            case T30Frame.Crp:
                repeat_last_command(state);
                break;
            case T30Frame.Fnv:
                process_rx_fnv(state, message);
                break;
            case T30Frame.Dcn:
                terminate_call(state);
                break;
            default:
                unexpected_final_frame(state, message);
                break;
        }
    }

    private static void process_state_f_post_rcp_ppr(T30State state, ReadOnlySpan<byte> message) {
        switch ((byte)(message[2] & 0xFE)) {
            case T30Frame.Crp:
                repeat_last_command(state);
                break;
            case T30Frame.Fnv:
                process_rx_fnv(state, message);
                break;
            default:
                unexpected_final_frame(state, message);
                break;
        }
    }

    private static void process_state_f_post_rcp_rnr(T30State state, ReadOnlySpan<byte> message) {
        switch ((byte)(message[2] & 0xFE)) {
            case T30Frame.Rr:
                if (state.ReceiverNotReadyCount > 0) {
                    state.ReceiverNotReadyCount--;
                    queue_phase(state, T30Phase.DTx);
                    set_state(state, T30StateCode.FPostRcpRnr);
                    send_simple_frame(state, T30Frame.Rnr);
                } else if (send_response_to_pps(state)
                           && state.LastPpsFcf2 is T30Frame.PriEop or T30Frame.Eop) {
                    state.Logging.Flow("End of procedure detected");
                    state.EndOfProcedureDetected = true;
                }
                break;
            case T30Frame.Crp:
                repeat_last_command(state);
                break;
            case T30Frame.Fnv:
                process_rx_fnv(state, message);
                break;
            default:
                unexpected_final_frame(state, message);
                break;
        }
    }

    private static void process_state_iv(T30State state, ReadOnlySpan<byte> message) {
        switch ((byte)(message[2] & 0xFE)) {
            case T30Frame.Crp:
                repeat_last_command(state);
                break;
            case T30Frame.Fnv:
                process_rx_fnv(state, message);
                break;
            default:
                unexpected_final_frame(state, message);
                break;
        }
    }

    private static void process_state_iv_pps_null(T30State state, ReadOnlySpan<byte> message) {
        process_state_iv_pps_common(state, message, false, false);
    }

    private static void process_state_iv_pps_q(T30State state, ReadOnlySpan<byte> message) {
        process_state_iv_pps_common(state, message, true, false);
    }

    private static void process_state_iv_pps_rnr(T30State state, ReadOnlySpan<byte> message) {
        process_state_iv_pps_common(state, message, true, true);
    }

    private static void process_state_iv_pps_common(
        T30State state,
        ReadOnlySpan<byte> message,
        bool allowInterruptFrames,
        bool receiverNotReadyState) {
        byte fcf = (byte)(message[2] & 0xFE);
        switch (fcf) {
            case T30Frame.Pip when allowInterruptFrames:
                if (state.RemoteInterruptsAllowed) {
                    state.Retries = 0;
                    if (state.PhaseDHandler is not null) {
                        state.PhaseDHandler(state.PhaseDUserData, fcf);
                        state.TimerT3 = MillisecondsToSamples(DefaultTimerT3);
                    }
                }
                process_ecm_mcf_after_pps(state, fcf);
                break;
            case T30Frame.Mcf:
                process_ecm_mcf_after_pps(state, fcf);
                break;
            case T30Frame.Ppr:
                process_rx_ppr(state, message);
                break;
            case T30Frame.Rnr:
                if (state.TimerT5 == 0)
                    state.TimerT5 = MillisecondsToSamples(DefaultTimerT5);
                queue_phase(state, T30Phase.DTx);
                set_state(state, T30StateCode.IVPpsRnr);
                send_rr(state);
                break;
            case T30Frame.Dcn:
                state.CurrentStatus = receiverNotReadyState ? T30Error.RxDcnrrd : T30Error.TxBadpg;
                terminate_call(state);
                break;
            case T30Frame.Crp:
                repeat_last_command(state);
                break;
            case T30Frame.Fnv:
                process_rx_fnv(state, message);
                break;
            case T30Frame.Pps:
                if (message.Length > 3 && message[3] == state.NextTxStep) {
                    state.Logging.Flow($"Received an echo of our own PPS-{T30Logging.t30_frametype(message[3])}");
                    timer_t4_start(state);
                    break;
                }
                unexpected_final_frame(state, message);
                if (!receiverNotReadyState)
                    state.CurrentStatus = T30Error.TxEcmphd;
                break;
            case T30Frame.Pin when allowInterruptFrames:
                if (state.RemoteInterruptsAllowed) {
                    state.Retries = 0;
                    if (state.PhaseDHandler is not null) {
                        state.PhaseDHandler(state.PhaseDUserData, fcf);
                        state.TimerT3 = MillisecondsToSamples(DefaultTimerT3);
                    }
                }
                unexpected_final_frame(state, message);
                if (!receiverNotReadyState)
                    state.CurrentStatus = T30Error.TxEcmphd;
                break;
            default:
                unexpected_final_frame(state, message);
                if (!receiverNotReadyState)
                    state.CurrentStatus = T30Error.TxEcmphd;
                break;
        }
    }

    private static void process_ecm_mcf_after_pps(T30State state, byte fcf) {
        state.Retries = 0;
        state.TimerT5 = 0;
        state.Logging.Flow($"Is there more to send? - {state.EcmFrames} {state.EcmLength[255]}");
        if (!state.EcmAtPageEnd && get_partial_ecm_page(state) > 0) {
            state.Logging.Flow("Additional image data to send");
            state.EcmBlock++;
            set_state(state, T30StateCode.IV);
            queue_phase(state, T30Phase.CEcmTx);
            send_first_ecm_frame(state);
            return;
        }

        state.Logging.Flow("Moving on to the next page");
        switch ((byte)(state.NextTxStep & 0xFE)) {
            case T30Frame.PriMps:
            case T30Frame.Mps:
                tx_end_page(state);
                state.PhaseDHandler?.Invoke(state.PhaseDUserData, fcf);
                if (tx_start_page(state) != 0)
                    break;
                if (get_partial_ecm_page(state) > 0) {
                    set_state(state, T30StateCode.IV);
                    queue_phase(state, T30Phase.CEcmTx);
                    send_first_ecm_frame(state);
                }
                break;
            case T30Frame.PriEom:
            case T30Frame.Eom:
            case T30Frame.Eos:
                tx_end_page(state);
                state.PhaseDHandler?.Invoke(state.PhaseDUserData, fcf);
                terminate_operation_in_progress(state);
                report_tx_result(state, 1);
                return_to_phase_b(state, false);
                break;
            case T30Frame.PriEop:
            case T30Frame.Eop:
                tx_end_page(state);
                state.PhaseDHandler?.Invoke(state.PhaseDUserData, fcf);
                terminate_operation_in_progress(state);
                send_dcn(state);
                report_tx_result(state, 1);
                break;
        }
    }

    private static void process_state_iv_ctc(T30State state, ReadOnlySpan<byte> message) {
        switch ((byte)(message[2] & 0xFE)) {
            case T30Frame.Ctr:
                state.ShortTrain = false;
                set_state(state, T30StateCode.IV);
                queue_phase(state, T30Phase.CEcmTx);
                send_first_ecm_frame(state);
                break;
            case T30Frame.Crp:
                repeat_last_command(state);
                break;
            case T30Frame.Fnv:
                process_rx_fnv(state, message);
                break;
            default:
                unexpected_final_frame(state, message);
                break;
        }
    }

    private static void process_state_iv_eor(T30State state, ReadOnlySpan<byte> message) {
        process_state_iv_eor_common(state, message, false);
    }

    private static void process_state_iv_eor_rnr(T30State state, ReadOnlySpan<byte> message) {
        process_state_iv_eor_common(state, message, true);
    }

    private static void process_state_iv_eor_common(T30State state, ReadOnlySpan<byte> message, bool receiverNotReadyState) {
        byte fcf = (byte)(message[2] & 0xFE);
        switch (fcf) {
            case T30Frame.Rnr:
                if (state.TimerT5 == 0)
                    state.TimerT5 = MillisecondsToSamples(DefaultTimerT5);
                queue_phase(state, T30Phase.DTx);
                set_state(state, T30StateCode.IVEorRnr);
                send_rr(state);
                break;
            case T30Frame.Err:
                state.CurrentStatus = T30Error.Retrydcn;
                state.TimerT5 = 0;
                send_dcn(state);
                break;
            case T30Frame.Dcn when receiverNotReadyState:
                state.CurrentStatus = T30Error.RxDcnrrd;
                terminate_call(state);
                break;
            case T30Frame.Crp:
                repeat_last_command(state);
                break;
            case T30Frame.Fnv:
                process_rx_fnv(state, message);
                break;
            case T30Frame.Pin:
                if (state.RemoteInterruptsAllowed) {
                    state.Retries = 0;
                    if (state.PhaseDHandler is not null) {
                        state.PhaseDHandler(state.PhaseDUserData, fcf);
                        state.TimerT3 = MillisecondsToSamples(DefaultTimerT3);
                    }
                }
                unexpected_final_frame(state, message);
                break;
            default:
                unexpected_final_frame(state, message);
                break;
        }
    }
}

public static partial class T30 {
    private static void set_ctrl_bit(Span<byte> frame, int bit) {
        int index = 3 + ((bit - 1) >> 3);
        if ((uint)index >= (uint)frame.Length)
            throw new ArgumentOutOfRangeException(nameof(bit));
        frame[index] |= (byte)(1 << ((bit - 1) & 7));
    }

    private static void clr_ctrl_bit(Span<byte> frame, int bit) {
        int index = 3 + ((bit - 1) >> 3);
        if ((uint)index >= (uint)frame.Length)
            throw new ArgumentOutOfRangeException(nameof(bit));
        frame[index] &= (byte)~(1 << ((bit - 1) & 7));
    }

    private static void set_ctrl_bits(Span<byte> frame, int firstBit, int value, int width) {
        for (int i = 0; i < width; i++) {
            if ((value & (1 << i)) != 0)
                set_ctrl_bit(frame, firstBit + i);
            else
                clr_ctrl_bit(frame, firstBit + i);
        }
    }

    private static int prune_dis_dtc(T30State state) {
        int i;
        for (i = T30State.MaxDisDtcDcsLength - 1; i >= 6; i--) {
            state.LocalDisDtcFrame[i] &= 0x7F;
            if (state.LocalDisDtcFrame[i] != 0)
                break;
        }
        state.LocalDisDtcLength = i + 1;
        state.LocalDisDtcFrame[i] &= 0x7F;
        for (i--; i > 4; i--)
            state.LocalDisDtcFrame[i] |= 0x80;
        return state.LocalDisDtcLength;
    }

    private static int prune_dcs(T30State state) {
        int i;
        for (i = T30State.MaxDisDtcDcsLength - 1; i >= 6; i--) {
            state.DcsFrame[i] &= 0x7F;
            if (state.DcsFrame[i] != 0)
                break;
        }
        state.DcsLength = i + 1;
        state.DcsFrame[i] &= 0x7F;
        for (i--; i > 4; i--)
            state.DcsFrame[i] |= 0x80;
        return state.DcsLength;
    }

    private static void timer_t2_start(T30State state) {
        state.Logging.Flow("Start T2");
        state.TimerT2T4 = MillisecondsToSamples(DefaultTimerT2);
        state.TimerT2T4Kind = T30TimerT2T4Kind.T2;
    }

    private static void timer_t1a_start(T30State state) {
        state.Logging.Flow("Start T1A");
        state.TimerT2T4 = MillisecondsToSamples(DefaultTimerT1A);
        state.TimerT2T4Kind = T30TimerT2T4Kind.T1A;
    }

    private static void timer_t2_flagged_start(T30State state) {
        if (state.Phase == T30Phase.CEcmRx) {
            timer_t1a_start(state);
        } else {
            state.Logging.Flow("Start T2-flagged");
            state.TimerT2T4 = MillisecondsToSamples(DefaultTimerT2Flagged);
            state.TimerT2T4Kind = T30TimerT2T4Kind.T2Flagged;
        }
    }

    private static void timer_t2_dropped_start(T30State state) {
        state.Logging.Flow("Start T2-dropped");
        state.TimerT2T4 = MillisecondsToSamples(DefaultTimerT2Dropped);
        state.TimerT2T4Kind = T30TimerT2T4Kind.T2Dropped;
    }

    private static void timer_t4_start(T30State state) {
        state.Logging.Flow("Start T4");
        state.TimerT2T4 = MillisecondsToSamples(DefaultTimerT4);
        state.TimerT2T4Kind = T30TimerT2T4Kind.T4;
    }

    private static void timer_t4_flagged_start(T30State state) {
        state.Logging.Flow("Start T4-flagged");
        state.TimerT2T4 = MillisecondsToSamples(DefaultTimerT4Flagged);
        state.TimerT2T4Kind = T30TimerT2T4Kind.T4Flagged;
    }

    private static void timer_t4_dropped_start(T30State state) {
        state.Logging.Flow("Start T4-dropped");
        state.TimerT2T4 = MillisecondsToSamples(DefaultTimerT4Dropped);
        state.TimerT2T4Kind = T30TimerT2T4Kind.T4Dropped;
    }

    private static void timer_t2_t4_stop(T30State state) {
        state.Logging.Flow($"Stop {state.TimerT2T4Kind} ({state.TimerT2T4} remaining)");
        state.TimerT2T4 = 0;
        state.TimerT2T4Kind = T30TimerT2T4Kind.Idle;
    }

    private static int send_pps_frame(T30State state) {
        Span<byte> frame = stackalloc byte[7];
        frame[0] = AddressField;
        frame[1] = ControlFinal;
        frame[2] = (byte)(T30Frame.Pps | (state.DisReceived ? 1 : 0));
        frame[3] = state.EcmAtPageEnd
            ? (byte)(state.NextTxStep | (state.DisReceived ? 1 : 0))
            : T30Frame.Null;
        frame[4] = (byte)(state.TxPageNumber & 0xFF);
        frame[5] = (byte)(state.EcmBlock & 0xFF);
        frame[6] = (byte)(state.EcmFramesThisTransmitBurst == 0
            ? 0
            : state.EcmFramesThisTransmitBurst - 1);
        state.Logging.Flow($"Sending PPS-{T30Logging.t30_frametype(frame[3])}");
        send_frame(state, frame);
        return frame[3] & 0xFE;
    }

    private static bool send_response_to_pps(T30State state) {
        queue_phase(state, T30Phase.DTx);
        if (state.RxEcmBlockOk) {
            set_state(state, T30StateCode.FPostRcpMcf);
            send_simple_frame(state, T30Frame.Mcf);
            return true;
        }

        set_state(state, T30StateCode.FPostRcpPpr);
        state.EcmFrameMap[0] = AddressField;
        state.EcmFrameMap[1] = ControlFinal;
        state.EcmFrameMap[2] = (byte)(T30Frame.Ppr | (state.DisReceived ? 1 : 0));
        send_frame(state, state.EcmFrameMap, 35);
        return false;
    }

    private static int process_rx_pps(T30State state, ReadOnlySpan<byte> message) {
        if (message.Length < 7) {
            state.Logging.Warning($"Bad PPS message length {message.Length}.");
            return -1;
        }

        state.LastPpsFcf2 = (byte)(message[3] & 0xFE);
        int frames = message[6] + 1;
        int block = message[5];
        int page = message[4];

        if (state.EcmFrames < 0)
            state.EcmFrames = frames;
        else if (frames == 0xFF)
            frames = 0;

        state.Logging.Flow(
            $"Received PPS-{T30Logging.t30_frametype(message[3])} - page {page}, block {block}, {frames} frames");

        if ((state.RxPageNumber & 0xFF) != page || (state.EcmBlock & 0xFF) != block) {
            state.Logging.Warning(
                $"ECM rx page/block mismatch - expected {state.RxPageNumber & 0xFF}/{state.EcmBlock & 0xFF}, " +
                $"but received {page}/{block}.");

            bool repeat = ((state.RxPageNumber & 0xFF) == page && ((state.EcmBlock - 1) & 0xFF) == block)
                || (((state.RxPageNumber - 1) & 0xFF) == page && state.EcmBlock == 0);
            if (repeat) {
                state.Logging.Flow("Looks like a repeat from the previous page/block - send MCF again.");
                Array.Fill(state.EcmLength, (short)-1);
                state.EcmFrames = -1;
                queue_phase(state, T30Phase.DTx);
                set_state(state, T30StateCode.FPostRcpMcf);
                send_simple_frame(state, T30Frame.Mcf);
            } else {
                state.CurrentStatus = T30Error.RxEcmphd;
                send_dcn(state);
            }
            return 0;
        }

        int firstBadFrame = T30State.MaxEcmFrames;
        bool first = true;
        int expectedLength = 256;
        for (int i = 0; i < 32; i++) {
            state.EcmFrameMap[i + 3] = 0;
            for (int j = 0; j < 8; j++) {
                int frameNo = (i << 3) + j;
                if (state.EcmLength[frameNo] >= 0 && frameNo < state.EcmFrames - 1) {
                    if (first) {
                        if (state.EcmLength[frameNo] == 64)
                            expectedLength = 64;
                        first = false;
                    }
                    if (state.EcmLength[frameNo] != expectedLength) {
                        state.Logging.Warning($"Bad length ECM frame - {state.EcmLength[frameNo]}");
                        state.EcmLength[frameNo] = -1;
                    }
                }

                if (state.EcmLength[frameNo] < 0) {
                    state.EcmFrameMap[i + 3] |= (byte)(1 << j);
                    if (frameNo < firstBadFrame)
                        firstBadFrame = frameNo;
                    if (frameNo < state.EcmFrames)
                        state.ErrorCorrectingModeRetries++;
                }
            }
        }

        state.RxEcmBlockOk = firstBadFrame >= state.EcmFrames;
        if (state.RxEcmBlockOk) {
            state.Logging.Flow(
                $"Partial page OK - committing block {state.EcmBlock}, {state.EcmFrames} frames");
            for (int i = 0; i < state.EcmFrames; i++) {
                int result = state.DocumentPutHandler is not null
                    ? state.DocumentPutHandler(
                        state.DocumentPutUserData,
                        state.EcmData[i].AsMemory(0, state.EcmLength[i]))
                    : t4_rx.t4_rx_put(
                        state.T4Rx,
                        state.EcmData[i],
                        state.EcmLength[i]);
                if (result != (int)t4_decoder_status_t.T4_DECODE_MORE_DATA) {
                    if (result != (int)t4_decoder_status_t.T4_DECODE_OK)
                        state.Logging.Flow($"Document ended with status {result}");
                    break;
                }
            }

            Array.Fill(state.EcmLength, (short)-1);
            state.EcmBlock++;
            state.EcmFrames = -1;

            if (state.LastPpsFcf2 != T30Frame.Null) {
                state.NextRxStep = state.LastPpsFcf2;
                rx_end_page(state);
                report_rx_ecm_page_result(state);
                state.PhaseDHandler?.Invoke(state.PhaseDUserData, state.LastPpsFcf2);
                rx_start_page(state);
            }
        }

        switch (state.LastPpsFcf2) {
            case T30Frame.PriMps:
            case T30Frame.PriEom:
            case T30Frame.PriEop:
            case T30Frame.Null:
            case T30Frame.Mps:
            case T30Frame.Eom:
            case T30Frame.Eos:
            case T30Frame.Eop:
                if (state.ReceiverNotReadyCount > 0) {
                    state.ReceiverNotReadyCount--;
                    queue_phase(state, T30Phase.DTx);
                    set_state(state, T30StateCode.FPostRcpRnr);
                    send_simple_frame(state, T30Frame.Rnr);
                } else if (send_response_to_pps(state)
                           && state.LastPpsFcf2 is T30Frame.PriEop or T30Frame.Eop) {
                    state.Logging.Flow("End of procedure detected");
                    state.EndOfProcedureDetected = true;
                }
                break;

            default:
                state.Logging.Warning(
                    $"Unexpected final frame {T30Logging.t30_frametype(message[2])} in {state.State}.");
                break;
        }
        return 0;
    }

    private static void process_rx_ppr(T30State state, ReadOnlySpan<byte> message) {
        if (message.Length != 35) {
            state.Logging.Warning($"Bad length for PPR bits - {(message.Length - 3) * 8}");
            state.CurrentStatus = T30Error.TxEcmphd;
            terminate_call(state);
            return;
        }

        state.Retries = 0;
        for (int i = 0; i < 32; i++) {
            for (int j = 0; j < 8; j++) {
                int frameNo = (i << 3) + j;
                if ((message[i + 3] & (1 << j)) == 0) {
                    if (state.EcmLength[frameNo] >= 0)
                        state.EcmProgress++;
                    state.EcmLength[frameNo] = -1;
                } else if (frameNo < state.EcmFrames) {
                    state.Logging.Flow($"Frame {frameNo} to be resent");
                    state.ErrorCorrectingModeRetries++;
                }
            }
        }

        if (++state.PprCount >= PprLimitBeforeCtcOrEor) {
            state.PprCount = 0;
            bool canFallback = state.EcmProgress != 0
                && state.CurrentFallback + 1 < FallbackSequence.Length
                && (FallbackSequence[state.CurrentFallback + 1].Required & state.CurrentPermittedModems) != 0;
            if (canFallback) {
                state.CurrentFallback++;
                state.EcmProgress = 0;
                queue_phase(state, T30Phase.DTx);
                set_state(state, T30StateCode.IVCtc);
                Span<byte> frame = stackalloc byte[5];
                frame[0] = AddressField;
                frame[1] = ControlFinal;
                frame[2] = (byte)(T30Frame.Ctc | (state.DisReceived ? 1 : 0));
                frame[3] = 0;
                frame[4] = FallbackSequence[state.CurrentFallback].DcsCode;
                send_frame(state, frame);
            } else {
                set_state(state, T30StateCode.IVEor);
                queue_phase(state, T30Phase.DTx);
                Span<byte> frame = stackalloc byte[4];
                frame[0] = AddressField;
                frame[1] = ControlFinal;
                frame[2] = (byte)(T30Frame.Eor | (state.DisReceived ? 1 : 0));
                frame[3] = state.EcmAtPageEnd
                    ? (byte)(state.NextTxStep | (state.DisReceived ? 1 : 0))
                    : T30Frame.Null;
                state.Logging.Flow($"Sending EOR + {T30Logging.t30_frametype(frame[3])}");
                send_frame(state, frame);
            }
        } else {
            set_state(state, T30StateCode.IV);
            queue_phase(state, T30Phase.CEcmTx);
            send_first_ecm_frame(state);
        }
    }

    private static void process_rx_fcd(T30State state, ReadOnlySpan<byte> message) {
        if (message.Length < 4) {
            state.Logging.Warning($"Bad length for FCD frame - {message.Length}");
            state.CurrentStatus = T30Error.TxEcmphd;
            terminate_call(state);
            return;
        }

        if (state.State != T30StateCode.FDocumentEcm) {
            state.Logging.Warning($"Unexpected non-final FCD frame in {state.State}.");
            return;
        }

        if (message.Length > T30State.MaxEcmFrameLength) {
            state.Logging.Warning($"Unexpected FCD frame length - {message.Length}");
        } else {
            int frameNo = message[3];
            int payloadLength = message.Length - 4;
            state.Logging.Flow($"Storing ECM frame {frameNo}, length {payloadLength}");
            message[4..].CopyTo(state.EcmData[frameNo]);
            state.EcmLength[frameNo] = (short)payloadLength;
            state.ShortTrain = true;
        }

        if (state.CurrentStatus == T30Error.RxNocarrier)
            state.CurrentStatus = T30Error.Ok;
    }

    private static void process_rx_rcp(T30State state, ReadOnlySpan<byte> message) {
        switch (state.State) {
            case T30StateCode.FDocumentEcm:
                set_state(state, T30StateCode.FPostDocumentEcm);
                queue_phase(state, T30Phase.DRx);
                timer_t2_start(state);
                if (state.CurrentStatus == T30Error.RxNocarrier)
                    state.CurrentStatus = T30Error.Ok;
                break;

            case T30StateCode.FPostDocumentEcm:
                timer_t2_start(state);
                break;

            default:
                state.Logging.Warning($"Unexpected non-final RCP frame in {state.State}.");
                break;
        }
    }

    private static void timer_t0_expired(T30State state) {
        state.Logging.Flow($"T0 expired in state {state.State}");
        state.CurrentStatus = T30Error.T0Expired;
        terminate_call(state);
    }

    private static void timer_t1_expired(T30State state) {
        state.Logging.Flow($"T1 expired in state {state.State}");
        state.CurrentStatus = T30Error.T1Expired;
        switch (state.State) {
            case T30StateCode.T:
                terminate_call(state);
                break;
            case T30StateCode.R:
                send_dcn(state);
                break;
            default:
                terminate_call(state);
                break;
        }
    }

    private static void timer_t1a_expired(T30State state) {
        state.Logging.Flow($"T1A expired in phase {state.Phase}, state {state.State}. An HDLC frame lasted too long.");
        state.CurrentStatus = T30Error.HdlcCarrier;
        terminate_call(state);
    }

    private static void timer_t2_expired(T30State state) {
        state.Logging.Flow($"T2 expired in phase {state.Phase}, state {state.State}");
        switch (state.State) {
            case T30StateCode.IIIQ:
            case T30StateCode.FPostRcpPpr:
            case T30StateCode.FPostRcpMcf:
                if (state.NextRxStep is T30Frame.PriEom or T30Frame.Eom or T30Frame.Eos) {
                    state.Logging.Flow($"Returning to phase B after {T30Logging.t30_frametype(state.NextRxStep)}");
                    state.TimerT0T1 = MillisecondsToSamples(DefaultTimerT1);
                    state.DisReceived = false;
                    set_phase(state, T30Phase.BTx);
                    timer_t2_start(state);
                    send_dis_or_dtc_sequence(state, true);
                    return;
                }
                break;

            case T30StateCode.FTcf:
                state.Logging.Flow("No TCF data received");
                set_phase(state, T30Phase.BTx);
                set_state(state, T30StateCode.FFtt);
                send_simple_frame(state, T30Frame.Ftt);
                return;

            case T30StateCode.FDocumentEcm:
            case T30StateCode.FDocumentNonEcm:
                state.CurrentStatus = T30Error.RxT2expfax;
                break;

            case T30StateCode.FPostDocumentEcm:
            case T30StateCode.FPostDocumentNonEcm:
                state.CurrentStatus = T30Error.RxT2expmps;
                break;

            case T30StateCode.IVPpsRnr:
            case T30StateCode.IVEorRnr:
                state.CurrentStatus = T30Error.RxT2exprr;
                break;

            case T30StateCode.R:
                state.CurrentStatus = T30Error.RxT2exp;
                break;
        }

        queue_phase(state, T30Phase.BTx);
        if (!EnsureReceiveDocument(state))
            send_dcn(state);
    }

    private static void timer_t2_flagged_expired(T30State state) {
        state.Logging.Flow($"T2-flagged expired in phase {state.Phase}, state {state.State}. An HDLC frame lasted too long.");
        state.CurrentStatus = T30Error.HdlcCarrier;
        terminate_call(state);
    }

    private static void timer_t2_dropped_expired(T30State state) {
        state.Logging.Flow($"T2-dropped expired in phase {state.Phase}, state {state.State}. The line is now quiet.");
        timer_t2_expired(state);
    }

    private static void timer_t3_expired(T30State state) {
        state.Logging.Flow($"T3 expired in phase {state.Phase}, state {state.State}");
        state.CurrentStatus = T30Error.T3Expired;
        terminate_call(state);
    }

    private static void timer_t4_expired(T30State state) {
        state.Logging.Flow($"T4 expired in phase {state.Phase}, state {state.State}");
        repeat_last_command(state);
    }

    private static void timer_t4_flagged_expired(T30State state) {
        state.Logging.Flow($"T4-flagged expired in phase {state.Phase}, state {state.State}. An HDLC frame lasted too long.");
        state.CurrentStatus = T30Error.HdlcCarrier;
        terminate_call(state);
    }

    private static void timer_t4_dropped_expired(T30State state) {
        state.Logging.Flow($"T4-dropped expired in phase {state.Phase}, state {state.State}. The line is now quiet.");
        timer_t4_expired(state);
    }

    private static void timer_t5_expired(T30State state) {
        state.Logging.Flow($"T5 expired in phase {state.Phase}, state {state.State}");
        state.CurrentStatus = T30Error.TxT5exp;
    }

    private static void timer_t6_expired(T30State state) {
        state.Logging.Flow($"T6 expired in phase {state.Phase}, state {state.State}");
        state.CurrentStatus = T30Error.Unexpected;
        send_dcn(state);
    }

    private static void timer_t7_expired(T30State state) {
        state.Logging.Flow($"T7 expired in phase {state.Phase}, state {state.State}");
        state.CurrentStatus = T30Error.Unexpected;
        send_dcn(state);
    }

    private static void timer_t8_expired(T30State state) {
        state.Logging.Flow($"T8 expired in phase {state.Phase}, state {state.State}");
        state.CurrentStatus = T30Error.Unexpected;
        send_dcn(state);
    }

    private static bool process_rx_mcf_ecm(T30State state, byte fcf) {
        if (state.State is not (T30StateCode.IVPpsNull or T30StateCode.IVPpsQ or T30StateCode.IVPpsRnr))
            return false;

        state.Retries = 0;
        state.TimerT5 = 0;
        state.Logging.Flow($"Is there more to send? - {state.EcmFrames} {state.EcmLength[255]}");
        if (!state.EcmAtPageEnd && get_partial_ecm_page(state) > 0) {
            state.Logging.Flow("Additional image data to send");
            state.EcmBlock++;
            set_state(state, T30StateCode.IV);
            queue_phase(state, T30Phase.CEcmTx);
            send_first_ecm_frame(state);
            return true;
        }

        state.Logging.Flow("Moving on to the next page");
        switch ((byte)(state.NextTxStep & 0xFE)) {
            case T30Frame.Mps:
                tx_end_page(state);
                state.PhaseDHandler?.Invoke(state.PhaseDUserData, fcf);
                if (t4_tx.t4_tx_start_page(state.T4Tx) != 0) {
                    state.CurrentStatus = T30Error.Nopage;
                    send_dcn(state);
                    break;
                }
                state.OperationInProgress = T30Operation.T4Transmit;
                if (get_partial_ecm_page(state) > 0) {
                    set_state(state, T30StateCode.IV);
                    queue_phase(state, T30Phase.CEcmTx);
                    send_first_ecm_frame(state);
                }
                break;

            case T30Frame.Eom:
            case T30Frame.Eos:
                tx_end_page(state);
                state.PhaseDHandler?.Invoke(state.PhaseDUserData, fcf);
                terminate_operation_in_progress(state);
                report_tx_result(state, 1);
                state.DisReceived = false;
                queue_phase(state, T30Phase.BTx);
                send_dis_or_dtc_sequence(state, true);
                break;

            case T30Frame.Eop:
                tx_end_page(state);
                state.PhaseDHandler?.Invoke(state.PhaseDUserData, fcf);
                terminate_operation_in_progress(state);
                send_dcn(state);
                report_tx_result(state, 1);
                break;
        }
        return true;
    }

    private static bool process_rx_rnr_ecm(T30State state) {
        if (state.State is not (T30StateCode.IVPpsNull or T30StateCode.IVPpsQ or T30StateCode.IVPpsRnr or T30StateCode.IVEor or T30StateCode.IVEorRnr))
            return false;

        if (state.TimerT5 == 0)
            state.TimerT5 = MillisecondsToSamples(DefaultTimerT5);
        queue_phase(state, T30Phase.DTx);
        set_state(state, state.State is T30StateCode.IVEor or T30StateCode.IVEorRnr
            ? T30StateCode.IVEorRnr
            : T30StateCode.IVPpsRnr);
        send_rr(state);
        return true;
    }

    private static bool process_rx_rr_ecm(T30State state) {
        if (state.State != T30StateCode.FPostRcpRnr)
            return false;

        if (state.ReceiverNotReadyCount > 0) {
            state.ReceiverNotReadyCount--;
            queue_phase(state, T30Phase.DTx);
            set_state(state, T30StateCode.FPostRcpRnr);
            send_simple_frame(state, T30Frame.Rnr);
        } else if (send_response_to_pps(state)
                   && state.LastPpsFcf2 is T30Frame.PriEop or T30Frame.Eop) {
            state.Logging.Flow("End of procedure detected");
            state.EndOfProcedureDetected = true;
        }
        return true;
    }

}

public static partial class T30 {
    private static int set_dis_or_dtc(T30State state) {
        state.LocalDisDtcFrame[2] = (byte)(T30Frame.Dis | (state.DisReceived ? 1 : 0));
        if (!string.IsNullOrEmpty(state.RxFile))
            set_ctrl_bit(state.LocalDisDtcFrame, T30ControlBit.DisReadyToReceiveFaxDocument);
        else
            clr_ctrl_bit(state.LocalDisDtcFrame, T30ControlBit.DisReadyToReceiveFaxDocument);

        if (!string.IsNullOrEmpty(state.TxFile))
            set_ctrl_bit(state.LocalDisDtcFrame, T30ControlBit.DisReadyToTransmitFaxDocument);
        else
            clr_ctrl_bit(state.LocalDisDtcFrame, T30ControlBit.DisReadyToTransmitFaxDocument);
        return 0;
    }

    private static void send_20digit_msg_frame(T30State state, byte command, string message) {
        Span<byte> frame = stackalloc byte[23];
        frame[0] = AddressField;
        frame[1] = ControlNonFinal;
        frame[2] = (byte)(command | (state.DisReceived ? 1 : 0));

        byte[] encoded = Encoding.ASCII.GetBytes(message);
        int count = Math.Min(encoded.Length, T30State.MaxIdentLength);
        int position = 3;
        for (int i = count - 1; i >= 0; i--)
            frame[position++] = encoded[i];
        while (position < frame.Length)
            frame[position++] = (byte)' ';
        send_frame(state, frame);
    }

    private static int send_nsf_frame(T30State state) {
        if (state.TxInfo.Nsf.Length == 0)
            return 0;
        state.Logging.Flow($"Sending user supplied NSF - {state.TxInfo.Nsf.Length} octets");
        byte[] frame = new byte[state.TxInfo.Nsf.Length + 3];
        frame[0] = AddressField;
        frame[1] = ControlNonFinal;
        frame[2] = T30Frame.Nsf;
        state.TxInfo.Nsf.CopyTo(frame, 3);
        send_frame(state, frame, frame.Length);
        return 1;
    }

    private static int send_nss_frame(T30State state) {
        if (state.TxInfo.Nss.Length == 0)
            return 0;
        state.Logging.Flow($"Sending user supplied NSS - {state.TxInfo.Nss.Length} octets");
        byte[] frame = new byte[state.TxInfo.Nss.Length + 3];
        frame[0] = AddressField;
        frame[1] = ControlNonFinal;
        frame[2] = (byte)(T30Frame.Nss | (state.DisReceived ? 1 : 0));
        state.TxInfo.Nss.CopyTo(frame, 3);
        send_frame(state, frame, frame.Length);
        return 1;
    }

    private static int send_nsc_frame(T30State state) {
        if (state.TxInfo.Nsc.Length == 0)
            return 0;
        state.Logging.Flow($"Sending user supplied NSC - {state.TxInfo.Nsc.Length} octets");
        byte[] frame = new byte[state.TxInfo.Nsc.Length + 3];
        frame[0] = AddressField;
        frame[1] = ControlNonFinal;
        frame[2] = T30Frame.Nsc;
        state.TxInfo.Nsc.CopyTo(frame, 3);
        send_frame(state, frame, frame.Length);
        return 1;
    }

    private static int send_ident_frame(T30State state, byte command) {
        if (string.IsNullOrEmpty(state.TxInfo.Ident))
            return 0;
        state.Logging.Flow($"Sending ident '{state.TxInfo.Ident}'");
        send_20digit_msg_frame(state, command, state.TxInfo.Ident);
        return 1;
    }

    private static int send_psa_frame(T30State state) {
        if (test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisPolledSubaddressingCapable)
            && !string.IsNullOrEmpty(state.TxInfo.PolledSubAddress)) {
            state.Logging.Flow($"Sending polled sub-address '{state.TxInfo.PolledSubAddress}'");
            send_20digit_msg_frame(state, T30Frame.Psa, state.TxInfo.PolledSubAddress);
            set_ctrl_bit(state.LocalDisDtcFrame, T30ControlBit.DisPolledSubaddressingCapable);
            return 1;
        }
        clr_ctrl_bit(state.LocalDisDtcFrame, T30ControlBit.DisPolledSubaddressingCapable);
        return 0;
    }

    private static int send_sep_frame(T30State state) {
        if (test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisSelectivePollingCapable)
            && !string.IsNullOrEmpty(state.TxInfo.SelectivePollingAddress)) {
            state.Logging.Flow($"Sending selective polling address '{state.TxInfo.SelectivePollingAddress}'");
            send_20digit_msg_frame(state, T30Frame.Sep, state.TxInfo.SelectivePollingAddress);
            set_ctrl_bit(state.LocalDisDtcFrame, T30ControlBit.DisSelectivePollingCapable);
            return 1;
        }
        clr_ctrl_bit(state.LocalDisDtcFrame, T30ControlBit.DisSelectivePollingCapable);
        return 0;
    }

    private static int send_sid_frame(T30State state) {
        if (test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisPassword)
            && !string.IsNullOrEmpty(state.TxInfo.SenderIdent)) {
            state.Logging.Flow($"Sending sender identification '{state.TxInfo.SenderIdent}'");
            send_20digit_msg_frame(state, T30Frame.Sid, state.TxInfo.SenderIdent);
            set_ctrl_bit(state.DcsFrame, T30ControlBit.DcsSenderIdTransmission);
            return 1;
        }
        clr_ctrl_bit(state.DcsFrame, T30ControlBit.DcsSenderIdTransmission);
        return 0;
    }

    private static int send_pwd_frame(T30State state) {
        if (test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisPassword)
            && !string.IsNullOrEmpty(state.TxInfo.Password)) {
            state.Logging.Flow($"Sending password '{state.TxInfo.Password}'");
            send_20digit_msg_frame(state, T30Frame.Pwd, state.TxInfo.Password);
            set_ctrl_bit(state.LocalDisDtcFrame, T30ControlBit.DisPassword);
            return 1;
        }
        clr_ctrl_bit(state.LocalDisDtcFrame, T30ControlBit.DisPassword);
        return 0;
    }

    private static int send_sub_frame(T30State state) {
        if (test_ctrl_bit(state.FarDisDtcFrame, T30ControlBit.DisSubaddressingCapable)
            && !string.IsNullOrEmpty(state.TxInfo.SubAddress)) {
            state.Logging.Flow($"Sending sub-address '{state.TxInfo.SubAddress}'");
            send_20digit_msg_frame(state, T30Frame.Sub, state.TxInfo.SubAddress);
            set_ctrl_bit(state.DcsFrame, T30ControlBit.DcsSubaddressTransmission);
            return 1;
        }
        clr_ctrl_bit(state.DcsFrame, T30ControlBit.DcsSubaddressTransmission);
        return 0;
    }

    private static int send_tsa_frame(T30State state) {
        _ = state;
        return 0;
    }

    private static int send_ira_frame(T30State state) {
        clr_ctrl_bit(state.DcsFrame, T30ControlBit.DcsInternetRoutingAddressTransmission);
        return 0;
    }

    private static int send_cia_frame(T30State state) {
        _ = state;
        return 0;
    }

    private static int send_isp_frame(T30State state) {
        clr_ctrl_bit(state.LocalDisDtcFrame, T30ControlBit.DisInternetSelectivePollingAddress);
        return 0;
    }

    private static int send_csa_frame(T30State state) {
        _ = state;
        return 0;
    }

    private static int send_dis_or_dtc_sequence(T30State state, bool start) {
        if (start) {
            set_dis_or_dtc(state);
            set_state(state, T30StateCode.R);
            state.Step = 0;
        }

        if (!state.DisReceived) {
            switch (state.Step) {
                case 0:
                    state.Step++;
                    if (send_nsf_frame(state) != 0)
                        break;
                    goto case 1;
                case 1:
                    state.Step++;
                    if (send_ident_frame(state, T30Frame.Csi) != 0)
                        break;
                    goto case 2;
                case 2:
                    state.Step++;
                    prune_dis_dtc(state);
                    send_frame(state, state.LocalDisDtcFrame, state.LocalDisDtcLength);
                    break;
                case 3:
                    state.Step++;
                    shut_down_hdlc_tx(state);
                    break;
                default:
                    return -1;
            }
        } else {
            switch (state.Step) {
                case 0:
                    state.Step++;
                    if (send_nsc_frame(state) != 0)
                        break;
                    goto case 1;
                case 1:
                    state.Step++;
                    if (send_ident_frame(state, T30Frame.Cig) != 0)
                        break;
                    goto case 2;
                case 2:
                    state.Step++;
                    if (send_pwd_frame(state) != 0)
                        break;
                    goto case 3;
                case 3:
                    state.Step++;
                    if (send_sep_frame(state) != 0)
                        break;
                    goto case 4;
                case 4:
                    state.Step++;
                    if (send_psa_frame(state) != 0)
                        break;
                    goto case 5;
                case 5:
                    state.Step++;
                    if (send_cia_frame(state) != 0)
                        break;
                    goto case 6;
                case 6:
                    state.Step++;
                    if (send_isp_frame(state) != 0)
                        break;
                    goto case 7;
                case 7:
                    state.Step++;
                    prune_dis_dtc(state);
                    send_frame(state, state.LocalDisDtcFrame, state.LocalDisDtcLength);
                    break;
                case 8:
                    state.Step++;
                    shut_down_hdlc_tx(state);
                    break;
                default:
                    return -1;
            }
        }
        return 0;
    }

    private static int send_dcs_sequence(T30State state, bool start) {
        if (start) {
            set_state(state, T30StateCode.D);
            state.Step = 0;
        }
        switch (state.Step) {
            case 0:
                state.Step++;
                if (send_nss_frame(state) != 0)
                    break;
                goto case 1;
            case 1:
                state.Step++;
                if (send_ident_frame(state, T30Frame.Tsi) != 0)
                    break;
                goto case 2;
            case 2:
                state.Step++;
                if (send_sub_frame(state) != 0)
                    break;
                goto case 3;
            case 3:
                state.Step++;
                if (send_sid_frame(state) != 0)
                    break;
                goto case 4;
            case 4:
                state.Step++;
                if (send_tsa_frame(state) != 0)
                    break;
                goto case 5;
            case 5:
                state.Step++;
                if (send_ira_frame(state) != 0)
                    break;
                goto case 6;
            case 6:
                state.Step++;
                prune_dcs(state);
                send_frame(state, state.DcsFrame, state.DcsLength);
                break;
            case 7:
                state.Step++;
                shut_down_hdlc_tx(state);
                break;
            default:
                return -1;
        }
        return 0;
    }

    private static int send_cfr_sequence(T30State state, bool start) {
        if (start)
            state.Step = 0;
        switch (state.Step) {
            case 0:
                state.Step++;
                if (send_csa_frame(state) != 0)
                    break;
                goto case 1;
            case 1:
                state.Step++;
                send_simple_frame(state, T30Frame.Cfr);
                break;
            case 2:
                state.Step++;
                shut_down_hdlc_tx(state);
                break;
            default:
                return -1;
        }
        return 0;
    }

    private static void repeat_last_command(T30State state) {
        state.Step = 0;
        state.Retries++;
        if (state.TimerT0T1 == 0 && state.Retries >= state.MaxCommandTries) {
            state.Logging.Flow("Too many retries. Giving up.");
            state.CurrentStatus = state.State switch {
                T30StateCode.DPostTcf => T30Error.TxPhbdead,
                T30StateCode.IIQ or T30StateCode.IVPpsNull or T30StateCode.IVPpsQ => T30Error.TxPhddead,
                _ => T30Error.Retrydcn
            };
            send_dcn(state);
            return;
        }

        state.Logging.Flow($"Command reattempt number {state.Retries}");
        switch (state.State) {
            case T30StateCode.R:
                state.DisReceived = false;
                queue_phase(state, T30Phase.BTx);
                send_dis_or_dtc_sequence(state, true);
                break;
            case T30StateCode.FDocumentNonEcm:
            case T30StateCode.IIIQ:
                queue_phase(state, T30Phase.DTx);
                send_simple_frame(state, state.LastRxPageResult);
                break;
            case T30StateCode.IIQ:
                queue_phase(state, T30Phase.DTx);
                send_simple_frame(state, state.NextTxStep);
                break;
            case T30StateCode.IVPpsNull:
            case T30StateCode.IVPpsQ:
                queue_phase(state, T30Phase.DTx);
                send_pps_frame(state);
                break;
            case T30StateCode.IVPpsRnr:
            case T30StateCode.IVEorRnr:
                queue_phase(state, T30Phase.DTx);
                send_rr(state);
                break;
            case T30StateCode.D:
                queue_phase(state, T30Phase.BTx);
                send_dcs_sequence(state, true);
                break;
            case T30StateCode.FFtt:
                queue_phase(state, T30Phase.BTx);
                send_simple_frame(state, T30Frame.Ftt);
                break;
            case T30StateCode.FCfr:
                queue_phase(state, T30Phase.BTx);
                send_cfr_sequence(state, true);
                break;
            case T30StateCode.DPostTcf:
                state.ShortTrain = false;
                queue_phase(state, T30Phase.BTx);
                send_dcs_sequence(state, true);
                break;
            case T30StateCode.FPostRcpPpr:
                queue_phase(state, T30Phase.DTx);
                send_frame(state, state.EcmFrameMap, 35);
                break;
            case T30StateCode.FPostRcpMcf:
                queue_phase(state, T30Phase.DTx);
                send_simple_frame(state, T30Frame.Mcf);
                break;
            case T30StateCode.FPostRcpRnr:
                break;
            default:
                state.Logging.Flow($"Repeat command called with nothing to repeat - phase {state.Phase}, state {state.State}");
                break;
        }
    }

    private static void start_final_pause(T30State state) {
        state.Logging.Flow("Starting final pause before disconnecting");
        terminate_operation_in_progress(state);
        state.TimerT0T1 = 0;
        state.TimerT2T4 = 0;
        state.TimerT2T4Kind = T30TimerT2T4Kind.Idle;
        state.TimerT3 = 0;
        state.TimerT5 = 0;
        set_phase(state, T30Phase.E);
        set_state(state, T30StateCode.B);
    }
}

public static partial class T30 {
    private static void process_final_frame_by_state(T30State state, ReadOnlySpan<byte> message) {
        switch (state.State) {
            case T30StateCode.Answering:
                process_state_answering(state, message);
                break;
            case T30StateCode.B:
                process_state_b(state, message);
                break;
            case T30StateCode.C:
                process_state_c(state, message);
                break;
            case T30StateCode.D:
                process_state_d(state, message);
                break;
            case T30StateCode.DTcf:
                process_state_d_tcf(state, message);
                break;
            case T30StateCode.DPostTcf:
                process_state_d_post_tcf(state, message);
                break;
            case T30StateCode.FTcf:
                process_state_f_tcf(state, message);
                break;
            case T30StateCode.FCfr:
                process_state_f_cfr(state, message);
                break;
            case T30StateCode.FFtt:
                process_state_f_ftt(state, message);
                break;
            case T30StateCode.FDocumentNonEcm:
                process_state_f_doc_non_ecm(state, message);
                break;
            case T30StateCode.FPostDocumentNonEcm:
                process_state_f_post_doc_non_ecm(state, message);
                break;
            case T30StateCode.FDocumentEcm:
            case T30StateCode.FPostDocumentEcm:
            case T30StateCode.FPostRcpMcf:
            case T30StateCode.FPostRcpPpr:
            case T30StateCode.FPostRcpRnr:
            case T30StateCode.IV:
            case T30StateCode.IVPpsNull:
            case T30StateCode.IVPpsQ:
            case T30StateCode.IVPpsRnr:
            case T30StateCode.IVCtc:
            case T30StateCode.IVEor:
            case T30StateCode.IVEorRnr:
                if (!process_ecm_final_frame_by_state(state, message))
                    unexpected_final_frame(state, message);
                break;
            case T30StateCode.R:
                process_state_r(state, message);
                break;
            case T30StateCode.T:
                process_state_t(state, message);
                break;
            case T30StateCode.I:
                process_state_i(state, message);
                break;
            case T30StateCode.II:
                process_state_ii(state, message);
                break;
            case T30StateCode.IIQ:
                process_state_ii_q(state, message);
                break;
            case T30StateCode.IIIQ:
                process_state_iii_q(state, message);
                break;
            case T30StateCode.CallFinished:
                process_state_call_finished(state, message);
                break;
            default:
                unexpected_final_frame(state, message);
                break;
        }
    }

    private static int start_receiving_document(T30State state) {
        if (string.IsNullOrEmpty(state.RxFile)) {
            state.Logging.Flow("No document to receive");
            return -1;
        }
        state.Logging.Flow("Start receiving document");
        state.EcmBlock = 0;
        send_dis_or_dtc_sequence(state, true);
        return 0;
    }

    private static int restart_sending_document(T30State state) {
        t4_tx.t4_tx_restart_page(state.T4Tx);
        state.Retries = 0;
        state.EcmBlock = 0;
        send_dcs_sequence(state, true);
        return 0;
    }

    private static void assess_copy_quality(T30State state, byte fcf) {
        int quality = copy_quality(state);
        switch (quality) {
            case T30CopyQualityPerfect:
            case T30CopyQualityGood:
            case T30CopyQualityPoor:
                rx_end_page(state);
                break;
            case T30CopyQualityBad:
                if (state.KeepBadPages)
                    rx_end_page(state);
                break;
        }

        state.PhaseDHandler?.Invoke(state.PhaseDUserData, fcf);
        if (fcf == T30Frame.Eop)
            terminate_operation_in_progress(state);
        else
            rx_start_page(state);

        state.LastRxPageResult = quality switch {
            T30CopyQualityPerfect or T30CopyQualityGood => T30Frame.Mcf,
            T30CopyQualityPoor => T30Frame.Rtp,
            _ => T30Frame.Rtn
        };
        set_state(state, T30StateCode.IIIQ);
        send_simple_frame(state, state.LastRxPageResult);
    }

    private static void process_state_answering(T30State state, ReadOnlySpan<byte> message) {
        switch ((byte)(message[2] & 0xFE)) {
            case T30Frame.Dis:
                state.Logging.Flow("DIS/DTC before DIS");
                process_rx_dis_dtc(state, message);
                break;
            case T30Frame.Dcs:
                state.Logging.Flow("DCS before DIS");
                process_rx_dcs(state, message);
                break;
            case T30Frame.Dcn:
                state.CurrentStatus = T30Error.TxGotdcn;
                terminate_call(state);
                break;
            default:
                unexpected_final_frame(state, message);
                break;
        }
    }

    private static void process_state_b(T30State state, ReadOnlySpan<byte> message) {
        switch ((byte)(message[2] & 0xFE)) {
            case T30Frame.Dcn:
                break;
            case T30Frame.Crp:
                repeat_last_command(state);
                break;
            case T30Frame.Fnv:
                process_rx_fnv(state, message);
                break;
            default:
                unexpected_final_frame(state, message);
                break;
        }
    }

    private static void process_state_c(T30State state, ReadOnlySpan<byte> message) {
        switch ((byte)(message[2] & 0xFE)) {
            case T30Frame.Dcn:
                break;
            case T30Frame.Crp:
                repeat_last_command(state);
                break;
            case T30Frame.Fnv:
                process_rx_fnv(state, message);
                break;
            default:
                unexpected_final_frame(state, message);
                break;
        }
    }

    private static void process_state_d(T30State state, ReadOnlySpan<byte> message) {
        switch ((byte)(message[2] & 0xFE)) {
            case T30Frame.Dcn:
                state.CurrentStatus = T30Error.TxBaddcs;
                terminate_call(state);
                break;
            case T30Frame.Crp:
                repeat_last_command(state);
                break;
            case T30Frame.Fnv:
                process_rx_fnv(state, message);
                break;
            default:
                unexpected_final_frame(state, message);
                break;
        }
    }

    private static void process_state_d_tcf(T30State state, ReadOnlySpan<byte> message) {
        switch ((byte)(message[2] & 0xFE)) {
            case T30Frame.Dcn:
                state.CurrentStatus = T30Error.TxBaddcs;
                terminate_call(state);
                break;
            case T30Frame.Crp:
                repeat_last_command(state);
                break;
            case T30Frame.Fnv:
                process_rx_fnv(state, message);
                break;
            default:
                unexpected_final_frame(state, message);
                break;
        }
    }

    private static void process_state_d_post_tcf(T30State state, ReadOnlySpan<byte> message) {
        switch ((byte)(message[2] & 0xFE)) {
            case T30Frame.Cfr:
                state.Logging.Flow("Trainability test succeeded");
                state.Retries = 0;
                state.ShortTrain = true;
                if (state.ErrorCorrectingMode) {
                    set_state(state, T30StateCode.IV);
                    queue_phase(state, T30Phase.CEcmTx);
                    send_first_ecm_frame(state);
                } else {
                    set_state(state, T30StateCode.I);
                    queue_phase(state, T30Phase.CNonEcmTx);
                }
                break;

            case T30Frame.Ftt:
                state.Logging.Flow("Trainability test failed");
                state.Retries = 0;
                state.ShortTrain = false;
                if (step_fallback_entry(state) < 0) {
                    state.CurrentStatus = T30Error.CannotTrain;
                    send_dcn(state);
                    break;
                }
                queue_phase(state, T30Phase.BTx);
                send_dcs_sequence(state, true);
                break;

            case T30Frame.Dis:
                if (++state.Retries >= state.MaxCommandTries) {
                    state.Logging.Flow("Too many retries. Giving up.");
                    state.CurrentStatus = T30Error.Retrydcn;
                    send_dcn(state);
                    break;
                }
                state.Logging.Flow($"Retry number {state.Retries}");
                queue_phase(state, T30Phase.BTx);
                send_dcs_sequence(state, true);
                break;

            case T30Frame.Dcn:
                state.CurrentStatus = T30Error.TxBaddcs;
                terminate_call(state);
                break;
            case T30Frame.Crp:
                repeat_last_command(state);
                break;
            case T30Frame.Fnv:
                process_rx_fnv(state, message);
                break;
            default:
                unexpected_final_frame(state, message);
                break;
        }
    }

    private static void process_state_f_tcf(T30State state, ReadOnlySpan<byte> message) {
        switch ((byte)(message[2] & 0xFE)) {
            case T30Frame.Crp:
                repeat_last_command(state);
                break;
            case T30Frame.Fnv:
                process_rx_fnv(state, message);
                break;
            default:
                unexpected_final_frame(state, message);
                break;
        }
    }

    private static void process_state_f_cfr(T30State state, ReadOnlySpan<byte> message) {
        switch ((byte)(message[2] & 0xFE)) {
            case T30Frame.Dcs:
                process_rx_dcs(state, message);
                break;
            case T30Frame.Crp:
                repeat_last_command(state);
                break;
            case T30Frame.Fnv:
                process_rx_fnv(state, message);
                break;
            default:
                unexpected_final_frame(state, message);
                break;
        }
    }

    private static void process_state_f_ftt(T30State state, ReadOnlySpan<byte> message) {
        switch ((byte)(message[2] & 0xFE)) {
            case T30Frame.Dcs:
                process_rx_dcs(state, message);
                break;
            case T30Frame.Crp:
                repeat_last_command(state);
                break;
            case T30Frame.Fnv:
                process_rx_fnv(state, message);
                break;
            default:
                unexpected_final_frame(state, message);
                break;
        }
    }

    private static void process_state_f_doc_non_ecm(T30State state, ReadOnlySpan<byte> message) {
        byte fcf = (byte)(message[2] & 0xFE);
        switch (fcf) {
            case T30Frame.Dis:
                process_rx_dis_dtc(state, message);
                break;
            case T30Frame.Dcs:
                process_rx_dcs(state, message);
                break;
            case T30Frame.PriMps:
            case T30Frame.Mps:
                process_bad_or_repeated_non_ecm_post_page(state, fcf, T30Phase.DTx);
                break;
            case T30Frame.PriEom:
            case T30Frame.Eom:
            case T30Frame.Eos:
                process_bad_or_repeated_non_ecm_post_page(state, fcf, T30Phase.BTx);
                break;
            case T30Frame.PriEop:
            case T30Frame.Eop:
                process_bad_or_repeated_non_ecm_post_page(state, fcf, T30Phase.DTx);
                break;
            case T30Frame.Dcn:
                state.CurrentStatus = T30Error.RxDcndata;
                terminate_call(state);
                break;
            case T30Frame.Crp:
                repeat_last_command(state);
                break;
            case T30Frame.Fnv:
                process_rx_fnv(state, message);
                break;
            default:
                state.CurrentStatus = T30Error.RxInvalcmd;
                unexpected_final_frame(state, message);
                break;
        }
    }

    private static void process_bad_or_repeated_non_ecm_post_page(T30State state, byte fcf, T30Phase phase) {
        if (state.ImageCarrierAttempted) {
            state.PhaseDHandler?.Invoke(state.PhaseDUserData, fcf);
            state.NextRxStep = fcf;
            state.LastRxPageResult = T30Frame.Rtn;
            queue_phase(state, phase);
            set_state(state, T30StateCode.IIIQ);
            send_simple_frame(state, T30Frame.Rtn);
        } else {
            repeat_last_command(state);
        }
    }

    private static void process_state_f_post_doc_non_ecm(T30State state, ReadOnlySpan<byte> message) {
        byte fcf = (byte)(message[2] & 0xFE);
        switch (fcf) {
            case T30Frame.PriMps:
            case T30Frame.Mps:
                state.NextRxStep = fcf;
                queue_phase(state, T30Phase.DTx);
                assess_copy_quality(state, fcf);
                break;
            case T30Frame.PriEom:
            case T30Frame.Eom:
            case T30Frame.Eos:
                state.NextRxStep = fcf;
                queue_phase(state, T30Phase.BTx);
                assess_copy_quality(state, fcf);
                break;
            case T30Frame.PriEop:
            case T30Frame.Eop:
                state.Logging.Flow("End of procedure detected");
                state.EndOfProcedureDetected = true;
                state.NextRxStep = fcf;
                queue_phase(state, T30Phase.DTx);
                assess_copy_quality(state, fcf);
                break;
            case T30Frame.Dcs:
                state.Logging.Flow("DCS received after CFR");
                process_rx_dcs(state, message);
                break;
            case T30Frame.Dcn:
                state.CurrentStatus = T30Error.RxDcnfax;
                terminate_call(state);
                break;
            case T30Frame.Crp:
                repeat_last_command(state);
                break;
            case T30Frame.Fnv:
                process_rx_fnv(state, message);
                break;
            default:
                state.CurrentStatus = T30Error.RxInvalcmd;
                unexpected_final_frame(state, message);
                break;
        }
    }

    private static void process_state_r(T30State state, ReadOnlySpan<byte> message) {
        switch ((byte)(message[2] & 0xFE)) {
            case T30Frame.Dis:
                process_rx_dis_dtc(state, message);
                break;
            case T30Frame.Dcs:
                process_rx_dcs(state, message);
                break;
            case T30Frame.Dcn:
                state.CurrentStatus = T30Error.RxDcnwhy;
                terminate_call(state);
                break;
            case T30Frame.Crp:
                repeat_last_command(state);
                break;
            case T30Frame.Fnv:
                process_rx_fnv(state, message);
                break;
            default:
                unexpected_final_frame(state, message);
                break;
        }
    }

    private static void process_state_t(T30State state, ReadOnlySpan<byte> message) {
        switch ((byte)(message[2] & 0xFE)) {
            case T30Frame.Dis:
                process_rx_dis_dtc(state, message);
                break;
            case T30Frame.Dcn:
                state.CurrentStatus = T30Error.TxGotdcn;
                terminate_call(state);
                break;
            case T30Frame.Crp:
                repeat_last_command(state);
                break;
            case T30Frame.Fnv:
                process_rx_fnv(state, message);
                break;
            default:
                unexpected_final_frame(state, message);
                state.CurrentStatus = T30Error.TxNodis;
                break;
        }
    }

    private static void process_state_i(T30State state, ReadOnlySpan<byte> message) {
        switch ((byte)(message[2] & 0xFE)) {
            case T30Frame.Crp:
                repeat_last_command(state);
                break;
            case T30Frame.Fnv:
                process_rx_fnv(state, message);
                break;
            default:
                unexpected_final_frame(state, message);
                break;
        }
    }

    private static void process_state_ii(T30State state, ReadOnlySpan<byte> message) {
        switch ((byte)(message[2] & 0xFE)) {
            case T30Frame.Crp:
                repeat_last_command(state);
                break;
            case T30Frame.Fnv:
                process_rx_fnv(state, message);
                break;
            default:
                unexpected_final_frame(state, message);
                break;
        }
    }

    private static void process_state_ii_q(T30State state, ReadOnlySpan<byte> message) {
        byte fcf = (byte)(message[2] & 0xFE);
        switch (fcf) {
            case T30Frame.Pip:
                if (state.RemoteInterruptsAllowed) {
                    state.Retries = 0;
                    if (state.PhaseDHandler is not null) {
                        state.PhaseDHandler(state.PhaseDUserData, fcf);
                        state.TimerT3 = MillisecondsToSamples(DefaultTimerT3);
                    }
                }
                process_non_ecm_mcf(state, fcf);
                break;
            case T30Frame.Mcf:
                process_non_ecm_mcf(state, fcf);
                break;
            case T30Frame.Rtp:
                process_non_ecm_rtp(state, fcf);
                break;
            case T30Frame.Pin:
                if (state.RemoteInterruptsAllowed) {
                    state.Retries = 0;
                    if (state.PhaseDHandler is not null) {
                        state.PhaseDHandler(state.PhaseDUserData, fcf);
                        state.TimerT3 = MillisecondsToSamples(DefaultTimerT3);
                    }
                }
                process_non_ecm_rtn(state, fcf);
                break;
            case T30Frame.Rtn:
                process_non_ecm_rtn(state, fcf);
                break;
            case T30Frame.Dcn:
                switch ((byte)(state.NextTxStep & 0xFE)) {
                    case T30Frame.PriMps:
                    case T30Frame.PriEom:
                    case T30Frame.Mps:
                    case T30Frame.Eom:
                    case T30Frame.Eos:
                        state.CurrentStatus = T30Error.RxDcnphd;
                        break;
                    default:
                        state.CurrentStatus = T30Error.TxBadpg;
                        break;
                }
                terminate_call(state);
                break;
            case T30Frame.Crp:
                repeat_last_command(state);
                break;
            case T30Frame.Fnv:
                process_rx_fnv(state, message);
                break;
            case T30Frame.Mps:
            case T30Frame.PriEom:
            case T30Frame.Eom:
            case T30Frame.Eos:
            case T30Frame.PriEop:
            case T30Frame.Eop:
                if (fcf == state.NextTxStep) {
                    state.Logging.Flow($"Received an echo of our own {T30Logging.t30_frametype(fcf)}");
                    timer_t4_start(state);
                    break;
                }
                state.CurrentStatus = T30Error.TxInvalrsp;
                unexpected_final_frame(state, message);
                break;
            default:
                state.CurrentStatus = T30Error.TxInvalrsp;
                unexpected_final_frame(state, message);
                break;
        }
    }

    private static void process_non_ecm_mcf(T30State state, byte fcf) {
        switch ((byte)(state.NextTxStep & 0xFE)) {
            case T30Frame.PriMps:
            case T30Frame.Mps:
                tx_end_page(state);
                state.PhaseDHandler?.Invoke(state.PhaseDUserData, fcf);
                if (tx_start_page(state) != 0)
                    break;
                set_state(state, T30StateCode.I);
                queue_phase(state, T30Phase.CNonEcmTx);
                break;
            case T30Frame.PriEom:
            case T30Frame.Eom:
            case T30Frame.Eos:
                tx_end_page(state);
                state.PhaseDHandler?.Invoke(state.PhaseDUserData, fcf);
                terminate_operation_in_progress(state);
                report_tx_result(state, 1);
                return_to_phase_b(state, false);
                break;
            case T30Frame.PriEop:
            case T30Frame.Eop:
                tx_end_page(state);
                state.PhaseDHandler?.Invoke(state.PhaseDUserData, fcf);
                terminate_operation_in_progress(state);
                send_dcn(state);
                report_tx_result(state, 1);
                break;
        }
    }

    private static void process_non_ecm_rtp(T30State state, byte fcf) {
        state.RtpEvents++;
        switch ((byte)(state.NextTxStep & 0xFE)) {
            case T30Frame.PriMps:
            case T30Frame.Mps:
                tx_end_page(state);
                state.PhaseDHandler?.Invoke(state.PhaseDUserData, fcf);
                if (tx_start_page(state) != 0)
                    break;
                if (step_fallback_entry(state) < 0) {
                    state.CurrentStatus = T30Error.CannotTrain;
                    send_dcn(state);
                    break;
                }
                queue_phase(state, T30Phase.BTx);
                restart_sending_document(state);
                break;
            case T30Frame.PriEom:
            case T30Frame.Eom:
            case T30Frame.Eos:
                tx_end_page(state);
                state.PhaseDHandler?.Invoke(state.PhaseDUserData, fcf);
                t4_tx.t4_tx_release(state.T4Tx);
                state.T4TxInitialized = false;
                return_to_phase_b(state, true);
                break;
            case T30Frame.PriEop:
            case T30Frame.Eop:
                tx_end_page(state);
                state.PhaseDHandler?.Invoke(state.PhaseDUserData, fcf);
                t4_tx.t4_tx_release(state.T4Tx);
                state.T4TxInitialized = false;
                send_dcn(state);
                break;
        }
    }

    private static void process_non_ecm_rtn(T30State state, byte fcf) {
        state.RtnEvents++;
        switch ((byte)(state.NextTxStep & 0xFE)) {
            case T30Frame.PriMps:
            case T30Frame.Mps:
                state.Retries = 0;
                state.PhaseDHandler?.Invoke(state.PhaseDUserData, fcf);
                if (!state.RetransmitCapable && tx_start_page(state) != 0)
                    break;
                if (step_fallback_entry(state) < 0) {
                    state.CurrentStatus = T30Error.CannotTrain;
                    send_dcn(state);
                    break;
                }
                queue_phase(state, T30Phase.BTx);
                restart_sending_document(state);
                break;
            case T30Frame.PriEom:
            case T30Frame.Eom:
            case T30Frame.Eos:
                state.Retries = 0;
                state.PhaseDHandler?.Invoke(state.PhaseDUserData, fcf);
                if (!state.RetransmitCapable)
                    return_to_phase_b(state, true);
                break;
            case T30Frame.PriEop:
            case T30Frame.Eop:
                state.Retries = 0;
                state.PhaseDHandler?.Invoke(state.PhaseDUserData, fcf);
                if (state.RetransmitCapable) {
                    if (step_fallback_entry(state) < 0) {
                        state.CurrentStatus = T30Error.CannotTrain;
                        send_dcn(state);
                        break;
                    }
                    queue_phase(state, T30Phase.BTx);
                    restart_sending_document(state);
                } else {
                    if (state.TxPageNumber == 0)
                        state.CurrentStatus = T30Error.Retrydcn;
                    send_dcn(state);
                }
                break;
        }
    }

    private static void process_state_iii_q(T30State state, ReadOnlySpan<byte> message) {
        byte fcf = (byte)(message[2] & 0xFE);
        switch (fcf) {
            case T30Frame.Eop:
            case T30Frame.Eom:
            case T30Frame.Eos:
            case T30Frame.Mps:
                queue_phase(state, T30Phase.DTx);
                set_state(state, T30StateCode.IIIQ);
                send_simple_frame(state, state.LastRxPageResult);
                break;
            case T30Frame.Dis:
                if (message[2] == T30Frame.Dtc)
                    process_rx_dis_dtc(state, message);
                break;
            case T30Frame.Crp:
                repeat_last_command(state);
                break;
            case T30Frame.Fnv:
                process_rx_fnv(state, message);
                break;
            case T30Frame.Dcn:
                if (state.LastRxPageResult == T30Frame.Rtn)
                    state.CurrentStatus = T30Error.RxDcnnortn;
                terminate_call(state);
                break;
            default:
                unexpected_final_frame(state, message);
                break;
        }
    }

    private static void process_state_call_finished(T30State state, ReadOnlySpan<byte> message) {
        _ = state;
        _ = message;
    }
}

public static partial class T30 {
    private static bool sslfax_enabled(T30State state) {
        return (state.IafMode & T30IafMode.T37) != 0
            && (state.IafMode & T30IafMode.T38) != 0;
    }

    private static void sslfax_bitstuffing(T30State state, byte value, bool stuff) {
        byte[] buffer = new byte[1];
        for (int bitIndex = 0; bitIndex < 8; bitIndex++) {
            int bit = (value & (1 << bitIndex)) != 0 ? 1 : 0;
            state.SslFax.EcmByte |= unchecked((byte)(bit << state.SslFax.EcmBitPosition));
            state.SslFax.EcmBitPosition++;
            if (state.SslFax.EcmBitPosition == 8) {
                buffer[0] = state.SslFax.EcmByte;
                SslFax.sslfax_write(state.SslFax, buffer, 1, 60_000, true, false);
                state.SslFax.EcmBitPosition = 0;
                state.SslFax.EcmByte = 0;
            }

            if (bit == 1 && stuff)
                state.SslFax.EcmOnes++;
            else
                state.SslFax.EcmOnes = 0;

            if (state.SslFax.EcmOnes == 5) {
                state.SslFax.EcmBitPosition++;
                if (state.SslFax.EcmBitPosition == 8) {
                    buffer[0] = state.SslFax.EcmByte;
                    SslFax.sslfax_write(state.SslFax, buffer, 1, 60_000, true, false);
                    state.SslFax.EcmBitPosition = 0;
                    state.SslFax.EcmByte = 0;
                }
                state.SslFax.EcmOnes = 0;
            }
        }
    }

    private static void t30_sslfax_real_time_frame_handler(
        object? userData,
        bool incoming,
        ReadOnlyMemory<byte> message) {
        if (userData is not T30State state || !state.SslFax.IsConnected || incoming)
            return;

        ReadOnlySpan<byte> source = message.Span;
        byte[] buffer = new byte[source.Length + 2];
        source.CopyTo(buffer);
        int length = CrcItu16.Append(buffer, source.Length);

        if (length > 2
            && source[0] == AddressField
            && source[1] == ControlNonFinal
            && (source[2] == T30Frame.Fcd || source[2] == T30Frame.Rcp)) {
            sslfax_bitstuffing(state, 0x7E, false);
            for (int i = 0; i < length; i++)
                sslfax_bitstuffing(state, buffer[i], true);

            if (source[2] == T30Frame.Rcp) {
                state.SslFax.RcpCount++;
                if (state.SslFax.RcpCount == 3) {
                    state.SslFax.RcpCount = 0;
                    sslfax_bitstuffing(state, 0x7E, false);
                    byte[] terminator = [0x10, 0x03];
                    SslFax.sslfax_write(state.SslFax, terminator, 2, 60_000, false, false);
                    state.SslFax.Signal = 2;
                    return;
                }
            }
            state.SslFax.DoUnderflow = true;
        } else {
            SslFax.sslfax_write(state.SslFax, buffer, length, 60_000, true, false);
            byte[] terminator = [0x10, 0x03];
            SslFax.sslfax_write(state.SslFax, terminator, 2, 60_000, false, false);
            state.SslFax.Signal = 2;
        }
    }
}
