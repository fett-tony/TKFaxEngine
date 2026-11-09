/*
 * TKFaxEngine - managed C# port
 *
 * AtInterpreter.cs
 *
 * Combined port of:
 *   at_interpreter.h
 *   private/at_interpreter.h
 *   at_interpreter.c
 *   at_interpreter_dictionary.h
 *
 * The generated command dictionary contains all 402 command entries and the
 * complete 4041-word command trie from the supplied native source.
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2004, 2005, 2006 Steve Underwood.
 *
 * This port preserves the LGPL-2.1 licensing terms of the original files.
 */

using System.Globalization;
using System.Text;

namespace TKFaxEngine;

public enum AtReceiveMode {
    OnHookCommand = 0,
    OffHookCommand = 1,
    Connected = 2,
    Delivery = 3,
    Hdlc = 4,
    Stuffed = 5
}

public enum AtCallEvent {
    Alerting = 1,
    Connected = 2,
    Answered = 3,
    Busy = 4,
    NoDialTone = 5,
    NoAnswer = 6,
    Hangup = 7
}

public enum AtModemControlOperation {
    Call = 0,
    Answer = 1,
    Hangup = 2,
    OffHook = 3,
    OnHook = 4,
    Dtr = 5,
    Rts = 6,
    Cts = 7,
    Carrier = 8,
    Ring = 9,
    Dsr = 10,
    SetId = 11,
    Restart = 12,
    DteTimeout = 13
}

public enum AtResponseCode {
    Ok = 0,
    Connect = 1,
    Ring = 2,
    NoCarrier = 3,
    Error = 4,
    Unknown = 5,
    NoDialTone = 6,
    Busy = 7,
    NoAnswer = 8,
    FcError = 9,
    Frh3 = 10
}

public enum AtResultCodeFormat {
    Ascii = 1,
    Numeric = 2,
    None = 3
}

/// <summary>
/// Values passed through AtModemControlOperation.Restart by the original
/// AT interpreter for the fax modem bank.
/// </summary>
public enum AtFaxModemRestartMode {
    Flush = 0,
    SilenceTransmit = 1,
    SilenceReceive = 2,
    CedToneTransmit = 3,
    CngToneTransmit = 4,
    NoCngToneTransmit = 5
}

public enum AtLogLevel {
    None = 0,
    Flow = 1,
    Warning = 2,
    Error = 3
}

public delegate int AtModemControlHandler(
    object? userData,
    int operation,
    string? value);

public delegate int AtTransmitHandler(
    object? userData,
    ReadOnlySpan<byte> data);

public delegate int AtClass1Handler(
    object? userData,
    int direction,
    int operation,
    int value);

public delegate void AtLogHandler(
    AtLogLevel level,
    string message);

public sealed class AtLogger {
    public AtLogLevel Level { get; set; } = AtLogLevel.None;

    public AtLogHandler? Handler { get; set; }

    internal void Flow(string message) => Write(AtLogLevel.Flow, message);

    internal void Warning(string message) => Write(AtLogLevel.Warning, message);

    internal void Error(string message) => Write(AtLogLevel.Error, message);

    private void Write(AtLogLevel level, string message) {
        if (Handler is null || Level == AtLogLevel.None || level < Level)
            return;

        Handler(level, message);
    }
}

public sealed class AtProfile {
    public bool Echo { get; set; }

    public bool Verbose { get; set; }

    public AtResultCodeFormat ResultCodeFormat { get; set; }

    public bool PulseDial { get; set; }

    public int DoubleEscape { get; set; }

    public int AdaptiveReceive { get; set; }

    public byte[] SRegisters { get; } = new byte[100];

    public AtProfile Clone() {
        AtProfile clone = new() {
            Echo = Echo,
            Verbose = Verbose,
            ResultCodeFormat = ResultCodeFormat,
            PulseDial = PulseDial,
            DoubleEscape = DoubleEscape,
            AdaptiveReceive = AdaptiveReceive
        };

        SRegisters.CopyTo(clone.SRegisters, 0);
        return clone;
    }

    internal static AtProfile CreateFactoryDefault() {
        AtProfile profile = new() {
            Echo = true,
            Verbose = true,
            ResultCodeFormat = AtResultCodeFormat.Ascii,
            PulseDial = false,
            DoubleEscape = 0,
            AdaptiveReceive = 0
        };

        profile.SRegisters[0] = 0;
        profile.SRegisters[3] = (byte)'\r';
        profile.SRegisters[4] = (byte)'\n';
        profile.SRegisters[5] = (byte)'\b';
        profile.SRegisters[6] = 1;
        profile.SRegisters[7] = 60;
        profile.SRegisters[8] = 5;
        profile.SRegisters[10] = 0;

        return profile;
    }

    internal static AtProfile CreateZeroProfile() {
        return new AtProfile {
            Echo = false,
            Verbose = false,
            ResultCodeFormat = 0,
            PulseDial = false,
            DoubleEscape = 0,
            AdaptiveReceive = 0
        };
    }
}

public readonly record struct AtCallInformation(
    string? Id,
    string? Value);



/// <summary>
/// Managed AT command interpreter corresponding to at_state_t.
/// </summary>
public sealed partial class AtInterpreterState : IDisposable {
    private delegate CommandResult at_cmd_service_t(
        string line,
        int argumentStart,
        ref int position);

    private const byte Etx = 0x03;
    private const byte Dle = 0x10;

    private static readonly string[] ResponseCodes =
    [
        "OK",
        "CONNECT",
        "RING",
        "NO CARRIER",
        "ERROR",
        "???",
        "NO DIALTONE",
        "BUSY",
        "NO ANSWER",
        "+FCERROR",
        "+FRH:3"
    ];

    private static readonly AtProfile[] Profiles =
    [
        AtProfile.CreateFactoryDefault(),
        AtProfile.CreateZeroProfile(),
        AtProfile.CreateZeroProfile()
    ];

    private readonly List<AtCallInformation> _callInformation = new();
    private readonly byte[] _receiveData = new byte[256];
    private readonly char[] _line = new char[256];

    private at_cmd_service_t[]? at_commands;

    private AtModemControlHandler? _modemControlHandler;
    private object? _modemControlUserData;

    private AtTransmitHandler? _transmitHandler;
    private object? _transmitUserData;

    private AtClass1Handler? _class1Handler;
    private object? _class1UserData;

    internal int rx_data_bytes;
    private int _linePointer;
    private bool _disposed;

    public AtInterpreterState(
        AtTransmitHandler transmitHandler,
        object? transmitUserData,
        AtModemControlHandler modemControlHandler,
        object? modemControlUserData,
        string? model = null,
        string? revision = null) {
        Model = string.IsNullOrWhiteSpace(model)
            ? "TKFaxEngine"
            : model;

        Revision = string.IsNullOrWhiteSpace(revision)
            ? "managed"
            : revision;

        Initialize(
            transmitHandler,
            transmitUserData,
            modemControlHandler,
            modemControlUserData);
    }

    internal AtInterpreterState() {
    }

    public const string Manufacturer = "www.soft-switch.org";

    public const string SerialNumber = "42";

    public const string GlobalObjectIdentity = "42";

    public string Model { get; private set; } = "TKFaxEngine";

    public string Revision { get; private set; } = "managed";

    public AtProfile Profile { get; private set; } =
        AtProfile.CreateFactoryDefault();

    public AtLogger Logging { get; } = new();

    public int CountryOfInstallation { get; private set; }

    public int DteInactivityTimeout { get; private set; }

    public int DteInactivityAction { get; private set; }

    public int SpeakerVolume { get; private set; }

    public int SpeakerMode { get; private set; }

    public int DteRate { get; private set; }

    public int DteCharacterFormat { get; private set; }

    public int DteParity { get; private set; }

    public int RlsdBehaviour { get; private set; }

    public int DtrBehaviour { get; private set; }

    public int CarrierLossTimeout { get; private set; }

    public int ResultCodeMode { get; private set; }

    public int DsrOption { get; private set; }

    public int LongSpaceDisconnectOption { get; private set; }

    public int SynchronousTransmitClockSource { get; private set; }

    public int ReceiveWindow { get; private set; }

    public int TransmitWindow { get; private set; }

    public int V8BisSignal { get; private set; }

    public int V8BisFirstMessage { get; private set; }

    public int V8BisSecondMessage { get; private set; }

    public int V8BisSignalEnable { get; private set; }

    public int V8BisMessageEnable { get; private set; }

    public int V8BisSupplementaryDelay { get; private set; }

    public int DteToDceFlowControl { get; private set; }

    public int DceToDteFlowControl { get; private set; }

    public int DisplayCallInformation { get; private set; }

    public bool CallInformationDisplayed { get; private set; }

    public string? LocalId { get; private set; }

    public int FaxClassMode { get; private set; }

    public AtReceiveMode ReceiveMode { get; private set; }

    public int RingsIndicated { get; private set; }

    public bool DoHangup { get; private set; }

    public bool SilentDial { get; private set; }

    public bool CommandDial { get; private set; }

    public bool OkIsPending { get; set; }

    public bool DteIsWaiting { get; internal set; }

    public bool ReceiveSignalPresent { get; set; }

    public bool ReceiveTrained { get; set; }

    public int TransmitState { get; set; }

    public bool IsDisposed => _disposed;

    public IReadOnlyList<AtCallInformation> CallInformation =>
        _callInformation;

    public void Initialize(
        AtTransmitHandler transmitHandler,
        object? transmitUserData,
        AtModemControlHandler modemControlHandler,
        object? modemControlUserData) {
        ArgumentNullException.ThrowIfNull(transmitHandler);
        ArgumentNullException.ThrowIfNull(modemControlHandler);

        at_commands ??=
        [
            at_cmd_dummy, at_cmd_amp_C, at_cmd_amp_D, at_cmd_amp_F, at_cmd_plus_A8A, at_cmd_plus_A8C,
            at_cmd_plus_A8E, at_cmd_plus_A8I, at_cmd_plus_A8J, at_cmd_plus_A8M, at_cmd_plus_A8R, at_cmd_plus_A8T,
            at_cmd_plus_ASTO, at_cmd_plus_CAAP, at_cmd_plus_CACM, at_cmd_plus_CACSP, at_cmd_plus_CAD, at_cmd_plus_CAEMLPP,
            at_cmd_plus_CAHLD, at_cmd_plus_CAJOIN, at_cmd_plus_CALA, at_cmd_plus_CALCC, at_cmd_plus_CALD, at_cmd_plus_CALM,
            at_cmd_plus_CAMM, at_cmd_plus_CANCHEV, at_cmd_plus_CAOC, at_cmd_plus_CAPD, at_cmd_plus_CAPTT, at_cmd_plus_CAREJ,
            at_cmd_plus_CAULEV, at_cmd_plus_CBC, at_cmd_plus_CBCS, at_cmd_plus_CBIP, at_cmd_plus_CBST, at_cmd_plus_CCFC,
            at_cmd_plus_CCLK, at_cmd_plus_CCS, at_cmd_plus_CCUG, at_cmd_plus_CCWA, at_cmd_plus_CCWE, at_cmd_plus_CDIP,
            at_cmd_plus_CDIS, at_cmd_plus_CDV, at_cmd_plus_CEER, at_cmd_plus_CESP, at_cmd_plus_CFCS, at_cmd_plus_CFG,
            at_cmd_plus_CFUN, at_cmd_plus_CGACT, at_cmd_plus_CGANS, at_cmd_plus_CGATT, at_cmd_plus_CGAUTO, at_cmd_plus_CGCAP,
            at_cmd_plus_CGCLASS, at_cmd_plus_CGCLOSP, at_cmd_plus_CGCLPAD, at_cmd_plus_CGCMOD, at_cmd_plus_CGCS, at_cmd_plus_CGDATA,
            at_cmd_plus_CGDCONT, at_cmd_plus_CGDSCONT, at_cmd_plus_CGEQMIN, at_cmd_plus_CGEQNEG, at_cmd_plus_CGEQREQ, at_cmd_plus_CGEREP,
            at_cmd_plus_CGMI, at_cmd_plus_CGMM, at_cmd_plus_CGMR, at_cmd_plus_CGOI, at_cmd_plus_CGPADDR, at_cmd_plus_CGQMIN,
            at_cmd_plus_CGQREQ, at_cmd_plus_CGREG, at_cmd_plus_CGSMS, at_cmd_plus_CGSN, at_cmd_plus_CGTFT, at_cmd_plus_CHLD,
            at_cmd_plus_CHSA, at_cmd_plus_CHSC, at_cmd_plus_CHSD, at_cmd_plus_CHSN, at_cmd_plus_CHSR, at_cmd_plus_CHST,
            at_cmd_plus_CHSU, at_cmd_plus_CHUP, at_cmd_plus_CHV, at_cmd_plus_CIMI, at_cmd_plus_CIND, at_cmd_plus_CIT,
            at_cmd_plus_CKPD, at_cmd_plus_CLAC, at_cmd_plus_CLAE, at_cmd_plus_CLAN, at_cmd_plus_CLCC, at_cmd_plus_CLCK,
            at_cmd_plus_CLIP, at_cmd_plus_CLIR, at_cmd_plus_CLVL, at_cmd_plus_CMAR, at_cmd_plus_CMEC, at_cmd_plus_CMEE,
            at_cmd_plus_CMER, at_cmd_plus_CMGC, at_cmd_plus_CMGD, at_cmd_plus_CMGF, at_cmd_plus_CMGL, at_cmd_plus_CMGR,
            at_cmd_plus_CMGS, at_cmd_plus_CMGW, at_cmd_plus_CMIP, at_cmd_plus_CMM, at_cmd_plus_CMMS, at_cmd_plus_CMOD,
            at_cmd_plus_CMSS, at_cmd_plus_CMUT, at_cmd_plus_CMUX, at_cmd_plus_CNMA, at_cmd_plus_CNMI, at_cmd_plus_CNUM,
            at_cmd_plus_COLP, at_cmd_plus_COPN, at_cmd_plus_COPS, at_cmd_plus_COS, at_cmd_plus_COTDI, at_cmd_plus_CPAS,
            at_cmd_plus_CPBF, at_cmd_plus_CPBR, at_cmd_plus_CPBS, at_cmd_plus_CPBW, at_cmd_plus_CPIN, at_cmd_plus_CPLS,
            at_cmd_plus_CPMS, at_cmd_plus_CPOL, at_cmd_plus_CPPS, at_cmd_plus_CPROT, at_cmd_plus_CPUC, at_cmd_plus_CPWC,
            at_cmd_plus_CPWD, at_cmd_plus_CQD, at_cmd_plus_CR, at_cmd_plus_CRC, at_cmd_plus_CREG, at_cmd_plus_CRES,
            at_cmd_plus_CRLP, at_cmd_plus_CRM, at_cmd_plus_CRMC, at_cmd_plus_CRMP, at_cmd_plus_CRSL, at_cmd_plus_CRSM,
            at_cmd_plus_CSAS, at_cmd_plus_CSCA, at_cmd_plus_CSCB, at_cmd_plus_CSCC, at_cmd_plus_CSCS, at_cmd_plus_CSDF,
            at_cmd_plus_CSDH, at_cmd_plus_CSGT, at_cmd_plus_CSIL, at_cmd_plus_CSIM, at_cmd_plus_CSMP, at_cmd_plus_CSMS,
            at_cmd_plus_CSNS, at_cmd_plus_CSQ, at_cmd_plus_CSS, at_cmd_plus_CSSN, at_cmd_plus_CSTA, at_cmd_plus_CSTF,
            at_cmd_plus_CSVM, at_cmd_plus_CTA, at_cmd_plus_CTF, at_cmd_plus_CTFR, at_cmd_plus_CTZR, at_cmd_plus_CTZU,
            at_cmd_plus_CUSD, at_cmd_plus_CUUS1, at_cmd_plus_CV120, at_cmd_plus_CVHU, at_cmd_plus_CVIB, at_cmd_plus_CXT,
            at_cmd_plus_DR, at_cmd_plus_DS, at_cmd_plus_DS44, at_cmd_plus_EB, at_cmd_plus_EFCS, at_cmd_plus_EFRAM,
            at_cmd_plus_ER, at_cmd_plus_ES, at_cmd_plus_ESA, at_cmd_plus_ESR, at_cmd_plus_ETBM, at_cmd_plus_EWIND,
            at_cmd_plus_F34, at_cmd_plus_FAA, at_cmd_plus_FAP, at_cmd_plus_FAR, at_cmd_plus_FBO, at_cmd_plus_FBS,
            at_cmd_plus_FBU, at_cmd_plus_FCC, at_cmd_plus_FCL, at_cmd_plus_FCLASS, at_cmd_plus_FCQ, at_cmd_plus_FCR,
            at_cmd_plus_FCS, at_cmd_plus_FCT, at_cmd_plus_FDD, at_cmd_plus_FDR, at_cmd_plus_FDT, at_cmd_plus_FEA,
            at_cmd_plus_FFC, at_cmd_plus_FFD, at_cmd_plus_FHS, at_cmd_plus_FIE, at_cmd_plus_FIP, at_cmd_plus_FIS,
            at_cmd_plus_FIT, at_cmd_plus_FKS, at_cmd_plus_FLI, at_cmd_plus_FLO, at_cmd_plus_FLP, at_cmd_plus_FMI,
            at_cmd_plus_FMM, at_cmd_plus_FMR, at_cmd_plus_FMS, at_cmd_plus_FND, at_cmd_plus_FNR, at_cmd_plus_FNS,
            at_cmd_plus_FPA, at_cmd_plus_FPI, at_cmd_plus_FPP, at_cmd_plus_FPR, at_cmd_plus_FPS, at_cmd_plus_FPW,
            at_cmd_plus_FRH, at_cmd_plus_FRM, at_cmd_plus_FRQ, at_cmd_plus_FRS, at_cmd_plus_FRY, at_cmd_plus_FSA,
            at_cmd_plus_FSP, at_cmd_plus_FTH, at_cmd_plus_FTM, at_cmd_plus_FTS, at_cmd_plus_GCAP, at_cmd_plus_GCI,
            at_cmd_plus_GMI, at_cmd_plus_GMM, at_cmd_plus_GMR, at_cmd_plus_GOI, at_cmd_plus_GSN, at_cmd_plus_IBC,
            at_cmd_plus_IBM, at_cmd_plus_ICF, at_cmd_plus_ICLOK, at_cmd_plus_IDSR, at_cmd_plus_IFC, at_cmd_plus_ILRR,
            at_cmd_plus_ILSD, at_cmd_plus_IPR, at_cmd_plus_IRTS, at_cmd_plus_ITF, at_cmd_plus_MA, at_cmd_plus_MR,
            at_cmd_plus_MS, at_cmd_plus_MSC, at_cmd_plus_MV18AM, at_cmd_plus_MV18P, at_cmd_plus_MV18R, at_cmd_plus_MV18S,
            at_cmd_plus_PCW, at_cmd_plus_PIG, at_cmd_plus_PMH, at_cmd_plus_PMHF, at_cmd_plus_PMHR, at_cmd_plus_PMHT,
            at_cmd_plus_PQC, at_cmd_plus_PSS, at_cmd_plus_SAC, at_cmd_plus_SAM, at_cmd_plus_SAR, at_cmd_plus_SARR,
            at_cmd_plus_SAT, at_cmd_plus_SCRR, at_cmd_plus_SDC, at_cmd_plus_SDI, at_cmd_plus_SDR, at_cmd_plus_SRSC,
            at_cmd_plus_STC, at_cmd_plus_STH, at_cmd_plus_SVC, at_cmd_plus_SVM, at_cmd_plus_SVR, at_cmd_plus_SVRR,
            at_cmd_plus_SVT, at_cmd_plus_TADR, at_cmd_plus_TAL, at_cmd_plus_TALS, at_cmd_plus_TDLS, at_cmd_plus_TE140,
            at_cmd_plus_TE141, at_cmd_plus_TEPAL, at_cmd_plus_TEPDL, at_cmd_plus_TERDL, at_cmd_plus_TLDL, at_cmd_plus_TMO,
            at_cmd_plus_TMODE, at_cmd_plus_TNUM, at_cmd_plus_TRDL, at_cmd_plus_TRDLS, at_cmd_plus_TRES, at_cmd_plus_TSELF,
            at_cmd_plus_TTER, at_cmd_plus_VAC, at_cmd_plus_VACR, at_cmd_plus_VBT, at_cmd_plus_VCID, at_cmd_plus_VCIDR,
            at_cmd_plus_VDID, at_cmd_plus_VDIDR, at_cmd_plus_VDR, at_cmd_plus_VDT, at_cmd_plus_VDX, at_cmd_plus_VEM,
            at_cmd_plus_VGM, at_cmd_plus_VGR, at_cmd_plus_VGS, at_cmd_plus_VGT, at_cmd_plus_VHC, at_cmd_plus_VIP,
            at_cmd_plus_VIT, at_cmd_plus_VLS, at_cmd_plus_VNH, at_cmd_plus_VPH, at_cmd_plus_VPP, at_cmd_plus_VPR,
            at_cmd_plus_VRA, at_cmd_plus_VRID, at_cmd_plus_VRL, at_cmd_plus_VRN, at_cmd_plus_VRX, at_cmd_plus_VSD,
            at_cmd_plus_VSID, at_cmd_plus_VSM, at_cmd_plus_VSP, at_cmd_plus_VTA, at_cmd_plus_VTD, at_cmd_plus_VTER,
            at_cmd_plus_VTH, at_cmd_plus_VTR, at_cmd_plus_VTS, at_cmd_plus_VTX, at_cmd_plus_VXT, at_cmd_plus_W,
            at_cmd_plus_WBAG, at_cmd_plus_WCDA, at_cmd_plus_WCHG, at_cmd_plus_WCID, at_cmd_plus_WCLK, at_cmd_plus_WCPN,
            at_cmd_plus_WCXF, at_cmd_plus_WDAC, at_cmd_plus_WDIR, at_cmd_plus_WECR, at_cmd_plus_WFON, at_cmd_plus_WKPD,
            at_cmd_plus_WPBA, at_cmd_plus_WPTH, at_cmd_plus_WRLK, at_cmd_plus_WS45, at_cmd_plus_WS46, at_cmd_plus_WS50,
            at_cmd_plus_WS51, at_cmd_plus_WS52, at_cmd_plus_WS53, at_cmd_plus_WS54, at_cmd_plus_WS57, at_cmd_plus_WS58,
            at_cmd_plus_WSTL, at_cmd_dummy, at_cmd_A, at_cmd_D, at_cmd_E, at_cmd_H,
            at_cmd_I, at_cmd_L, at_cmd_M, at_cmd_O, at_cmd_P, at_cmd_Q,
            at_cmd_S0, at_cmd_S10, at_cmd_S3, at_cmd_S4, at_cmd_S5, at_cmd_S6,
            at_cmd_S7, at_cmd_S8, at_cmd_T, at_cmd_V, at_cmd_X, at_cmd_Z,
        ];

        _transmitHandler = transmitHandler;
        _transmitUserData = transmitUserData;
        _modemControlHandler = modemControlHandler;
        _modemControlUserData = modemControlUserData;

        _class1Handler = null;
        _class1UserData = null;

        Profile = Profiles[0].Clone();

        CountryOfInstallation = 0;
        DteInactivityTimeout = 0;
        DteInactivityAction = 0;
        SpeakerVolume = 0;
        SpeakerMode = 0;
        DteRate = 0;
        DteCharacterFormat = 0;
        DteParity = 0;
        RlsdBehaviour = 0;
        DtrBehaviour = 0;
        CarrierLossTimeout = 0;
        ResultCodeMode = 0;
        DsrOption = 0;
        LongSpaceDisconnectOption = 0;
        SynchronousTransmitClockSource = 0;
        ReceiveWindow = 0;
        TransmitWindow = 0;

        V8BisSignal = 0;
        V8BisFirstMessage = 0;
        V8BisSecondMessage = 0;
        V8BisSignalEnable = 0;
        V8BisMessageEnable = 0;
        V8BisSupplementaryDelay = 0;

        DteToDceFlowControl = 2;
        DceToDteFlowControl = 2;
        DisplayCallInformation = 0;
        CallInformationDisplayed = false;
        LocalId = null;
        FaxClassMode = 0;
        RingsIndicated = 0;
        DoHangup = false;
        SilentDial = false;
        CommandDial = false;
        OkIsPending = false;
        DteIsWaiting = false;
        ReceiveSignalPresent = false;
        ReceiveTrained = false;
        TransmitState = 0;

        rx_data_bytes = 0;
        _linePointer = 0;
        Array.Clear(_receiveData);
        Array.Clear(_line);
        _callInformation.Clear();

        ReceiveMode = AtReceiveMode.OnHookCommand;
        _disposed = false;
    }

    public static string CallStateToString(int state) {
        return state switch {
            (int)AtCallEvent.Alerting => "Alerting",
            (int)AtCallEvent.Connected => "Connected",
            (int)AtCallEvent.Answered => "Answered",
            (int)AtCallEvent.Busy => "Busy",
            (int)AtCallEvent.NoDialTone => "No dialtone",
            (int)AtCallEvent.NoAnswer => "No answer",
            (int)AtCallEvent.Hangup => "Hangup",
            _ => "???"
        };
    }

    public static string ModemControlToString(int operation) {
        return operation switch {
            (int)AtModemControlOperation.Call => "Call",
            (int)AtModemControlOperation.Answer => "Answer",
            (int)AtModemControlOperation.Hangup => "Hangup",
            (int)AtModemControlOperation.OffHook => "Off hook",
            (int)AtModemControlOperation.OnHook => "On hook",
            (int)AtModemControlOperation.Dtr => "DTR",
            (int)AtModemControlOperation.Rts => "RTS",
            (int)AtModemControlOperation.Cts => "CTS",
            (int)AtModemControlOperation.Carrier => "CAR",
            (int)AtModemControlOperation.Ring => "RNG",
            (int)AtModemControlOperation.Dsr => "DSR",
            (int)AtModemControlOperation.SetId => "Set ID",
            (int)AtModemControlOperation.Restart => "Restart",
            (int)AtModemControlOperation.DteTimeout => "DTE timeout",
            _ => "???"
        };
    }

    public void SetReceiveMode(AtReceiveMode newMode) {
        ThrowIfDisposed();

        if (newMode is AtReceiveMode.Hdlc or AtReceiveMode.Stuffed) {
            // The supplied C source has a malformed argument order in this
            // branch. This managed port applies the documented intent:
            // configure the DTE inactivity timeout in milliseconds.
            ModemControl(
                AtModemControlOperation.DteTimeout,
                checked(DteInactivityTimeout * 1000)
                    .ToString(CultureInfo.InvariantCulture));
        } else {
            ModemControl(
                AtModemControlOperation.DteTimeout,
                null);
        }

        ReceiveMode = newMode;
    }

    public void PutResponse(string text) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(text);

        Span<byte> formatting = stackalloc byte[2]
        {
            Profile.SRegisters[3],
            Profile.SRegisters[4]
        };

        if (Profile.ResultCodeFormat == AtResultCodeFormat.Ascii)
            Transmit(formatting);

        TransmitAscii(text);
        Transmit(formatting);
    }

    public void PutNumericResponse(int value) {
        PutResponse(value.ToString(CultureInfo.InvariantCulture));
    }

    public void PutResponseCode(AtResponseCode code) {
        ThrowIfDisposed();

        int index = (int)code;
        if ((uint)index >= (uint)ResponseCodes.Length)
            index = (int)AtResponseCode.Unknown;

        Logging.Flow($"Sending AT response code {ResponseCodes[index]}");

        switch (Profile.ResultCodeFormat) {
            case AtResultCodeFormat.Ascii:
                PutResponse(ResponseCodes[index]);
                break;

            case AtResultCodeFormat.Numeric:
                TransmitAscii(
                    index.ToString(CultureInfo.InvariantCulture) +
                    (char)Profile.SRegisters[3]);
                break;

            case AtResultCodeFormat.None:
            default:
                break;
        }
    }

    public void ResetCallInformation() {
        ThrowIfDisposed();

        _callInformation.Clear();
        RingsIndicated = 0;
        CallInformationDisplayed = false;
    }

    public void SetCallInformation(string? id, string? value) {
        ThrowIfDisposed();
        _callInformation.Add(new AtCallInformation(id, value));
    }

    public void DisplayStoredCallInformation() {
        ThrowIfDisposed();

        foreach (AtCallInformation item in _callInformation) {
            PutResponse(
                $"{item.Id ?? "NULL"}={item.Value ?? "<NONE>"}");
        }

        CallInformationDisplayed = true;
    }

    public int ModemControl(
        AtModemControlOperation operation,
        string? value) {
        ThrowIfDisposed();

        AtModemControlHandler callback =
            _modemControlHandler ??
            throw new InvalidOperationException(
                "The modem-control handler is not configured.");

        return callback(
            _modemControlUserData,
            (int)operation,
            value);
    }

    public void CallEvent(AtCallEvent callEvent) {
        ThrowIfDisposed();

        Logging.Flow(
            $"Call event {(int)callEvent} received");

        switch (callEvent) {
            case AtCallEvent.Alerting:
                ModemControl(
                    AtModemControlOperation.Ring,
                    "1");

                if (DisplayCallInformation != 0 &&
                    !CallInformationDisplayed) {
                    DisplayStoredCallInformation();
                }

                PutResponseCode(AtResponseCode.Ring);
                RingsIndicated++;

                if (Profile.SRegisters[0] != 0 &&
                    RingsIndicated >= Profile.SRegisters[0]) {
                    answer_call();
                }

                break;

            case AtCallEvent.Answered:
                ModemControl(
                    AtModemControlOperation.Ring,
                    "0");

                if (FaxClassMode == 0) {
                    SetReceiveMode(AtReceiveMode.Connected);
                } else {
                    SetReceiveMode(AtReceiveMode.Delivery);
                    RestartFaxModem(
                        AtFaxModemRestartMode.CedToneTransmit);
                }

                break;

            case AtCallEvent.Connected:
                Logging.Flow(
                    $"Dial call - connected. FCLASS={FaxClassMode}");

                ModemControl(
                    AtModemControlOperation.Ring,
                    "0");

                if (FaxClassMode == 0) {
                    SetReceiveMode(AtReceiveMode.Connected);
                } else if (CommandDial) {
                    PutResponseCode(AtResponseCode.Ok);
                    SetReceiveMode(AtReceiveMode.OffHookCommand);
                } else {
                    SetReceiveMode(AtReceiveMode.Delivery);

                    RestartFaxModem(
                        SilentDial
                            ? AtFaxModemRestartMode.NoCngToneTransmit
                            : AtFaxModemRestartMode.CngToneTransmit);

                    DteIsWaiting = true;
                }

                break;

            case AtCallEvent.Busy:
                SetReceiveMode(AtReceiveMode.OnHookCommand);
                PutResponseCode(AtResponseCode.Busy);
                break;

            case AtCallEvent.NoDialTone:
                SetReceiveMode(AtReceiveMode.OnHookCommand);
                PutResponseCode(AtResponseCode.NoDialTone);
                break;

            case AtCallEvent.NoAnswer:
                SetReceiveMode(AtReceiveMode.OnHookCommand);
                PutResponseCode(AtResponseCode.NoAnswer);
                break;

            case AtCallEvent.Hangup:
                HandleHangupEvent();
                break;

            default:
                Logging.Warning(
                    $"Invalid call event {(int)callEvent} received.");
                break;
        }
    }

    public void Interpret(string command) {
        ArgumentNullException.ThrowIfNull(command);
        Interpret(Encoding.ASCII.GetBytes(command));
    }

    public void Interpret(ReadOnlySpan<byte> command) {
        ThrowIfDisposed();

        if (Profile.Echo)
            Transmit(command);

        foreach (byte originalByte in command) {
            int character = originalByte & 0x7F;

            if (_linePointer < 2) {
                ProcessPrefixCharacter(character);
                continue;
            }

            if (character is >= 0x20 and <= 0x7E) {
                if (_linePointer < _line.Length - 1) {
                    _line[_linePointer++] =
                        char.ToUpperInvariant((char)character);
                }

                continue;
            }

            if (character == Profile.SRegisters[3]) {
                ExecuteBufferedLine();
                _linePointer = 0;
                continue;
            }

            if (character == Profile.SRegisters[5]) {
                if (_linePointer > 0)
                    _linePointer--;

                continue;
            }

            // Ignore the complete line when an unsupported control character
            // is encountered, matching the native implementation.
            _linePointer = 0;
        }
    }

    public void SetClass1Handler(
        AtClass1Handler? handler,
        object? userData) {
        ThrowIfDisposed();
        _class1Handler = handler;
        _class1UserData = userData;
    }

    public void SetModemControlHandler(
        AtModemControlHandler handler,
        object? userData) {
        ThrowIfDisposed();
        _modemControlHandler =
            handler ?? throw new ArgumentNullException(nameof(handler));
        _modemControlUserData = userData;
    }

    public void SetTransmitHandler(
        AtTransmitHandler handler,
        object? userData) {
        ThrowIfDisposed();
        _transmitHandler =
            handler ?? throw new ArgumentNullException(nameof(handler));
        _transmitUserData = userData;
    }

    public int Release() {
        ResetCallInformation();
        LocalId = null;
        return 0;
    }

    public void Dispose() {
        if (_disposed)
            return;

        _callInformation.Clear();
        LocalId = null;
        _modemControlHandler = null;
        _modemControlUserData = null;
        _transmitHandler = null;
        _transmitUserData = null;
        _class1Handler = null;
        _class1UserData = null;
        Array.Clear(_receiveData);
        Array.Clear(_line);
        rx_data_bytes = 0;
        _linePointer = 0;
        _disposed = true;
    }

    private void ProcessPrefixCharacter(int character) {
        if (char.ToLowerInvariant((char)character) == 'a') {
            _linePointer = 0;
            _line[_linePointer++] = 'A';
            return;
        }

        if (_linePointer != 1)
            return;

        if (char.ToLowerInvariant((char)character) == 't') {
            _line[_linePointer++] = 'T';
        } else if (character == '/') {
            // A/ repeat is also TODO in the supplied native source.
            _line[_linePointer++] = '/';
        } else {
            _linePointer = 0;
        }
    }

    private static int command_search(
        string line,
        int start,
        out int matched) {
        int entry = 0;
        int indexInCommand = 0;
        int pointer = 0;
        ReadOnlySpan<ushort> command_trie = at_interpreter_dictionary.command_trie;

        while (pointer < at_interpreter_dictionary.COMMAND_TRIE_LEN - 2) {
            int character = start + indexInCommand < line.Length
                ? char.ToUpperInvariant(line[start + indexInCommand])
                : 0;

            int first = command_trie[pointer++];
            int last = command_trie[pointer++];
            entry = command_trie[pointer++];

            if (character < first || character > last)
                break;

            pointer = command_trie[pointer + character - first];
            if (pointer == 0)
                break;

            pointer--;
            indexInCommand++;
        }

        matched = indexInCommand;
        return entry;
    }

    private void ExecuteBufferedLine() {
        if (_linePointer == 2) {
            PutResponseCode(AtResponseCode.Ok);
            return;
        }

        if (_linePointer < 2)
            return;

        string line = new(_line, 0, _linePointer);
        int position = 2;
        CommandResult result = CommandResult.Success;

        while (position < line.Length) {
            int commandStart = position;
            int entry = command_search(line, commandStart, out int matched);
            if (entry <= 0)
                break;

            at_cmd_service_t[] commands = at_commands
                ?? throw new InvalidOperationException(
                    "The AT command function table is not initialized.");

            if (entry > commands.Length)
                break;

            result = commands[entry - 1](
                line,
                commandStart + matched,
                ref position);

            if (result != CommandResult.Success)
                break;
        }

        if (result == CommandResult.SuppressImmediateResponse)
            return;

        PutResponseCode(
            result == CommandResult.Failure
                ? AtResponseCode.Error
                : AtResponseCode.Ok);
    }

    private void HandleHangupEvent() {
        Logging.Flow(
            $"Hangup... at_rx_mode {(int)ReceiveMode}");

        ModemControl(
            AtModemControlOperation.OnHook,
            null);

        if (DteIsWaiting) {
            if (OkIsPending) {
                PutResponseCode(AtResponseCode.Ok);
                OkIsPending = false;
            } else {
                PutResponseCode(AtResponseCode.NoCarrier);
            }

            DteIsWaiting = false;
            SetReceiveMode(AtReceiveMode.OnHookCommand);
        } else if (FaxClassMode != 0 &&
                   ReceiveSignalPresent) {
            if (rx_data_bytes <= _receiveData.Length - 2) {
                _receiveData[rx_data_bytes++] = Dle;
                _receiveData[rx_data_bytes++] = Etx;
            }

            Transmit(
                _receiveData.AsSpan(0, rx_data_bytes));

            rx_data_bytes = 0;
        }

        if (ReceiveMode is not
                AtReceiveMode.OffHookCommand and not
                AtReceiveMode.OnHookCommand) {
            PutResponseCode(AtResponseCode.NoCarrier);
        }

        ReceiveSignalPresent = false;

        ModemControl(
            AtModemControlOperation.Ring,
            "0");

        SetReceiveMode(
            AtReceiveMode.OnHookCommand);
    }

    private void RestartFaxModem(
        AtFaxModemRestartMode mode) {
        ModemControl(
            AtModemControlOperation.Restart,
            ((int)mode).ToString(CultureInfo.InvariantCulture));
    }

    private void TransmitAscii(string text) {
        Transmit(Encoding.ASCII.GetBytes(text));
    }

    private int Transmit(ReadOnlySpan<byte> data) {
        AtTransmitHandler callback =
            _transmitHandler ??
            throw new InvalidOperationException(
                "The AT transmit handler is not configured.");

        return callback(_transmitUserData, data);
    }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }

    private enum CommandResult {
        Success = 0,
        Failure = 1,
        SuppressImmediateResponse = 2
    }
}

/// <summary>
/// Compatibility facade retaining the original C function names.
/// </summary>
public static class AtInterpreterApi {
    public static string at_call_state_to_str(int state) =>
        AtInterpreterState.CallStateToString(state);

    public static string at_modem_control_to_str(int state) =>
        AtInterpreterState.ModemControlToString(state);

    public static void at_set_at_rx_mode(
        AtInterpreterState state,
        int newMode) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetReceiveMode((AtReceiveMode)newMode);
    }

    public static void at_put_response(
        AtInterpreterState state,
        string text) {
        ArgumentNullException.ThrowIfNull(state);
        state.PutResponse(text);
    }

    public static void at_put_numeric_response(
        AtInterpreterState state,
        int value) {
        ArgumentNullException.ThrowIfNull(state);
        state.PutNumericResponse(value);
    }

    public static void at_put_response_code(
        AtInterpreterState state,
        int code) {
        ArgumentNullException.ThrowIfNull(state);
        state.PutResponseCode((AtResponseCode)code);
    }

    public static void at_reset_call_info(
        AtInterpreterState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ResetCallInformation();
    }

    public static void at_set_call_info(
        AtInterpreterState state,
        string? id,
        string? value) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetCallInformation(id, value);
    }

    public static void at_display_call_info(
        AtInterpreterState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.DisplayStoredCallInformation();
    }

    public static int at_modem_control(
        AtInterpreterState state,
        int operation,
        string? value) {
        ArgumentNullException.ThrowIfNull(state);

        return state.ModemControl(
            (AtModemControlOperation)operation,
            value);
    }

    public static void at_call_event(
        AtInterpreterState state,
        int callEvent) {
        ArgumentNullException.ThrowIfNull(state);
        state.CallEvent((AtCallEvent)callEvent);
    }

    public static void at_interpreter(
        AtInterpreterState state,
        string command,
        int length) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);

        if (length < 0 || length > command.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        state.Interpret(command[..length]);
    }

    public static AtLogger at_get_logging_state(
        AtInterpreterState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Logging;
    }

    public static void at_set_class1_handler(
        AtInterpreterState state,
        AtClass1Handler? handler,
        object? userData) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetClass1Handler(handler, userData);
    }

    public static void at_set_modem_control_handler(
        AtInterpreterState state,
        AtModemControlHandler handler,
        object? userData) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetModemControlHandler(handler, userData);
    }

    public static void at_set_at_tx_handler(
        AtInterpreterState state,
        AtTransmitHandler handler,
        object? userData) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetTransmitHandler(handler, userData);
    }

    public static AtInterpreterState at_init(
        AtInterpreterState? state,
        AtTransmitHandler transmitHandler,
        object? transmitUserData,
        AtModemControlHandler modemControlHandler,
        object? modemControlUserData) {
        state ??= new AtInterpreterState();

        state.Initialize(
            transmitHandler,
            transmitUserData,
            modemControlHandler,
            modemControlUserData);

        return state;
    }

    public static int at_release(
        AtInterpreterState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    public static int at_free(
        AtInterpreterState? state) {
        if (state is null)
            return 0;

        int result = state.Release();
        state.Dispose();
        return result;
    }
}
