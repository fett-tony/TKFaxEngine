/*
 * TKFaxEngine - managed C# port
 *
 * V150_1.cs
 *
 * Combined port of:
 *   v150_1.h
 *   private/v150_1.h (merged into the supplied v150_1.h)
 *   v150_1_local.h
 *   v150_1.c
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2022, 2023 Steve Underwood.
 *
 * IMPORTANT: The supplied V.150.1 sources are licensed under GPL version 2.
 * This managed port retains that GPL-2.0 licensing requirement.
 */

#nullable enable

namespace TKFaxEngine.Modem.V150;

public static class V1501Constants {
    public const int CallDiscriminationDefaultTimeout = 60_000_000;
    public const int InformationPreferenceCount = 10;
    public const int JmCategoryCount = 16;
    public const int MaximumPacketBytes = 256;
    public const int QueryTimestamp = -1;
}

public enum V1501CallDiscriminationSelection {
    Indeterminate = 0,
    AudioRfc4733 = 1,
    VbdPreferred = 2,
    Mixed = 3
}

public enum V1501ModemRelayGatewayType {
    V8 = 0,
    Universal = 1
}

public enum V1501MessageId {
    Null = 0,
    Init = 1,
    XidExchange = 2,
    JmInfo = 3,
    StartJm = 4,
    Connect = 5,
    Break = 6,
    BreakAck = 7,
    MrEvent = 8,
    Cleardown = 9,
    ProfileExchange = 10,
    IRawOctet = 16,
    IRawBit = 17,
    IOctet = 18,
    ICharStatic = 19,
    ICharDynamic = 20,
    IFrame = 21,
    IOctetCharacterSequence = 22,
    ICharStaticCharacterSequence = 23,
    ICharDynamicCharacterSequence = 24,
    VendorMinimum = 100,
    VendorMaximum = 127
}

[Flags]
public enum V1501Support {
    None = 0,
    IRawBit = 0x0800,
    IFrame = 0x0400,
    ICharStatic = 0x0200,
    ICharDynamic = 0x0100,
    IOctetCharacterSequence = 0x0080,
    ICharStaticCharacterSequence = 0x0040,
    ICharDynamicCharacterSequence = 0x0020
}

public enum V1501JmCategoryId {
    Extension = 0x0,
    Protocols = 0x5,
    CallFunction1 = 0x8,
    ModulationModes = 0xA,
    PstnAccess = 0xB,
    PcmModemAvailability = 0xE
}

public static class V1501JmCallFunction {
    public const int T30Transmit = 0x1 << 9;
    public const int V18 = 0x2 << 9;
    public const int VSeries = 0x3 << 9;
    public const int H324 = 0x4 << 9;
    public const int T30Receive = 0x5 << 9;
    public const int T101 = 0x6 << 9;
}

[Flags]
public enum V1501JmModulationMode {
    None = 0,
    V34 = 0x800,
    V34HalfDuplex = 0x400,
    V32V32Bis = 0x200,
    V22V22Bis = 0x100,
    V17 = 0x080,
    V29 = 0x040,
    V27Ter = 0x020,
    V26Ter = 0x010,
    V26Bis = 0x008,
    V23 = 0x004,
    V23HalfDuplex = 0x002,
    V21 = 0x001
}

public static class V1501JmProtocol {
    public const int V42Lapm = 0x4 << 9;
}

public static class V1501JmAccess {
    public const int CallDceCellular = 0x4 << 9;
    public const int AnswerDceCellular = 0x2 << 9;
    public const int DceDigitalNetwork = 0x1 << 9;
}

public static class V1501JmPcmMode {
    public const int V90V92AnalogueModemAvailable = 0x4 << 9;
    public const int V90V92DigitalModemAvailable = 0x2 << 9;
    public const int V91ModemAvailable = 0x1 << 9;
}

public enum V1501SelectedModulation {
    Null = 0,
    V92 = 1,
    V91 = 2,
    V90 = 3,
    V34 = 4,
    V32Bis = 5,
    V32 = 6,
    V22Bis = 7,
    V22 = 8,
    V17 = 9,
    V29 = 10,
    V27Ter = 11,
    V26Ter = 12,
    V26Bis = 13,
    V23 = 14,
    V21 = 15,
    Bell212 = 16,
    Bell103 = 17,
    VendorMinimum = 18,
    VendorMaximum = 30
}

public enum V1501SymbolRate {
    Null = 0,
    Baud600 = 1,
    Baud1200 = 2,
    Baud1600 = 3,
    Baud2400 = 4,
    Baud2743 = 5,
    Baud3000 = 6,
    Baud3200 = 7,
    Baud3429 = 8,
    Baud8000 = 9
}

public enum V1501CompressionDirection {
    NeitherWay = 0,
    TransmitOnly = 1,
    ReceiveOnly = 2,
    Bidirectional = 3
}

public enum V1501Compression {
    None = 0,
    V42Bis = 1,
    V44 = 2,
    Mnp5 = 3
}

public enum V1501ErrorCorrection {
    None = 0,
    V42Lapm = 1,
    V42AnnexA = 2
}

public enum V1501BreakSource {
    V42Lapm = 0,
    V42AnnexA = 1,
    V14 = 2
}

public enum V1501BreakType {
    NotApplicable = 0,
    DestructiveExpedited = 1,
    NonDestructiveExpedited = 2,
    NonDestructiveNonExpedited = 3
}

public enum V1501MrEventId {
    Null = 0,
    RateRenegotiation = 1,
    Retrain = 2,
    PhysicallyUp = 3
}

public enum V1501MrEventReason {
    Null = 0,
    Initiation = 1,
    Responding = 2
}

public enum V1501CleardownReason {
    Unknown = 0,
    PhysicalLayerRelease = 1,
    LinkLayerDisconnect = 2,
    DataCompressionDisconnect = 3,
    Abort = 4,
    OnHook = 5,
    NetworkLayerTermination = 6,
    Administrative = 7
}

public enum V1501DataBits {
    Bits5 = 0,
    Bits6 = 1,
    Bits7 = 2,
    Bits8 = 3
}

public enum V1501Parity {
    Unknown = 0,
    None = 1,
    Even = 2,
    Odd = 3,
    Space = 4,
    Mark = 5
}

public enum V1501StopBits {
    One = 0,
    Two = 1
}

public enum V1501ConnectionState {
    Idle = 0,
    Initialized = 1,
    Retrain = 2,
    RateRenegotiation = 3,
    PhysicallyUp = 4,
    Connected = 5
}

public enum V1501MediaState {
    ItuReserved0 = 0,
    InitialAudio = 1,
    VoiceBandData = 2,
    ModemRelay = 3,
    FaxRelay = 4,
    TextRelay = 5,
    TextProbe = 6,
    ItuReservedMinimum = 7,
    ItuReservedMaximum = 31,
    VendorReservedMinimum = 32,
    VendorReservedMaximum = 63,
    Indeterminate = 64
}

public enum V1501MrModulation {
    V34 = 1,
    V34HalfDuplex = 2,
    V32Bis = 3,
    V22Bis = 4,
    V17 = 5,
    V29HalfDuplex = 6,
    V27Ter = 7,
    V26Ter = 8,
    V26Bis = 9,
    V23Duplex = 10,
    V23HalfDuplex = 11,
    V21 = 12,
    V90Analogue = 13,
    V90Digital = 14,
    V91 = 15,
    V92Analogue = 16,
    V92Digital = 17
}

public enum V1501StatusReason {
    Null = 0,
    MediaStateChanged = 1,
    ConnectionStateChanged = 2,
    DataFormatChanged = 3,
    BreakReceived = 4,
    RateRetrainReceived = 5,
    RateRenegotiationReceived = 6,
    BusyChanged = 7,
    ConnectionStatePhysicallyUp = 8,
    ConnectionStateConnected = 9
}

public enum V1501Signal {
    Tone2100Hz = 1,
    Tone2225Hz = 2,
    Ans = 3,
    AnsPhaseReversal = 4,
    Ansam = 5,
    AnsamPhaseReversal = 6,
    Ci = 7,
    Cm = 8,
    Jm = 9,
    V21Low = 10,
    V21High = 11,
    V23Low = 12,
    V23High = 13,
    Sb1 = 14,
    Usb1 = 15,
    S1 = 16,
    Aa = 17,
    Ac = 18,
    CallDiscriminationTimeout = 19,
    Unknown = 20,
    Silence = 21,
    Abort = 22,
    GenerateAns = 23,
    GenerateAnsPhaseReversal = 24,
    GenerateAnsam = 25,
    GenerateAnsamPhaseReversal = 26,
    Generate2225Hz = 27,
    ConcealModem = 28,
    Block2100HzTone = 29,
    EnableAutomode = 30,
    GenerateAudioState = 31,
    GenerateFaxRelayState = 32,
    GenerateIndeterminateState = 33,
    GenerateModemRelayState = 34,
    GenerateTextRelayState = 35,
    GenerateVbdState = 36,
    GenerateRfc4733Ans = 37,
    GenerateRfc4733AnsPhaseReversal = 38,
    GenerateRfc4733Ansam = 39,
    GenerateRfc4733AnsamPhaseReversal = 40,
    GenerateRfc4733Tone = 41,
    Audio = 42,
    FaxRelay = 43,
    Indeterminate = 44,
    ModemRelay = 45,
    TextRelay = 46,
    Vbd = 47,
    Rfc4733Ans = 48,
    Rfc4733AnsPhaseReversal = 49,
    Rfc4733Ansam = 50,
    Rfc4733AnsamPhaseReversal = 51,
    Rfc4733Tone = 52,
    AudioState = 53,
    FaxRelayState = 54,
    IndeterminateState = 55,
    ModemRelayState = 56,
    TextRelayState = 57,
    VbdState = 58,
    CallDiscriminationTimerExpired = 59
}

public enum V1501LogLevel {
    None = 0,
    Flow = 1,
    Warning = 2,
    Error = 3
}

public delegate void V1501LogHandler(V1501LogLevel level, string protocol, string message);
public delegate int V1501SpeSignalHandler(object? userData, int signal);
public delegate int V1501RxDataHandler(object? userData, ReadOnlySpan<byte> message, int fill);
public delegate int V1501RxStatusReportHandler(object? userData, V1501Status report);
public delegate int V1501SseTransmitPacketHandler(object? userData, bool repeat, ReadOnlySpan<byte> packet);
public delegate ulong V1501TimerHandler(object? userData, ulong timeout);
public delegate void V1501IpSignalHandler(V1501State state, V1501Signal signal, int reasonCode);

public sealed class V1501Logger {
    public string Protocol { get; set; } = "V.150.1";
    public V1501LogLevel Level { get; set; } = V1501LogLevel.None;
    public V1501LogHandler? Handler { get; set; }

    public void Flow(string message) => Write(V1501LogLevel.Flow, message);
    public void Warning(string message) => Write(V1501LogLevel.Warning, message);
    public void Error(string message) => Write(V1501LogLevel.Error, message);

    public void Buffer(V1501LogLevel level, string prefix, ReadOnlySpan<byte> buffer) {
        if (IsEnabled(level))
            Write(level, $"{prefix}{Convert.ToHexString(buffer)}");
    }

    private void Write(V1501LogLevel level, string message) {
        if (IsEnabled(level))
            Handler?.Invoke(level, Protocol, message);
    }

    private bool IsEnabled(V1501LogLevel level) =>
        Handler is not null && Level != V1501LogLevel.None && level >= Level;
}

public sealed class V1501Status {
    public V1501StatusReason Reason { get; init; }
    public V1501MediaState LocalMediaState { get; init; }
    public V1501MediaState RemoteMediaState { get; init; }
    public V1501ConnectionState ConnectionState { get; init; }
    public V1501CleardownReason CleardownReason { get; init; }
    public int Bits { get; init; }
    public V1501Parity Parity { get; init; }
    public int StopBits { get; init; }
    public V1501BreakSource BreakSource { get; init; }
    public V1501BreakType BreakType { get; init; }
    public int BreakDurationMilliseconds { get; init; }
    public bool LocalBusy { get; init; }
    public bool FarBusy { get; init; }
    public V1501SelectedModulation SelectedModulation { get; init; }
    public int TransmitDataSignallingRate { get; init; }
    public int ReceiveDataSignallingRate { get; init; }
    public bool TransmitSymbolRateEnabled { get; init; }
    public V1501SymbolRate TransmitSymbolRate { get; init; }
    public bool ReceiveSymbolRateEnabled { get; init; }
    public V1501SymbolRate ReceiveSymbolRate { get; init; }
    public V1501CompressionDirection SelectedCompressionDirection { get; init; }
    public V1501Compression SelectedCompression { get; init; }
    public V1501ErrorCorrection SelectedErrorCorrection { get; init; }
    public int CompressionTransmitDictionarySize { get; init; }
    public int CompressionReceiveDictionarySize { get; init; }
    public int CompressionTransmitStringLength { get; init; }
    public int CompressionReceiveStringLength { get; init; }
    public int CompressionTransmitHistorySize { get; init; }
    public int CompressionReceiveHistorySize { get; init; }
    public bool IRawOctetAvailable { get; init; }
    public bool IRawBitAvailable { get; init; }
    public bool IFrameAvailable { get; init; }
    public bool IOctetWithDlciAvailable { get; init; }
    public bool IOctetWithoutDlciAvailable { get; init; }
    public bool ICharStaticAvailable { get; init; }
    public bool ICharDynamicAvailable { get; init; }
    public bool IOctetCharacterSequenceAvailable { get; init; }
    public bool ICharStaticCharacterSequenceAvailable { get; init; }
    public bool ICharDynamicCharacterSequenceAvailable { get; init; }
}

public sealed class V1501Parameters {
    public V1501CallDiscriminationSelection CallDiscriminationSelection { get; set; }
    public V1501ModemRelayGatewayType ModemRelayGatewayType { get; set; }
    public bool V42LapmSupported { get; set; }
    public bool V42AnnexASupported { get; set; }
    public bool V42BisSupported { get; set; }
    public bool V44Supported { get; set; }
    public bool Mnp5Supported { get; set; }
    public int ErrorCorrectionProtocol { get; set; }
    public bool PreferredNonErrorControlledReceiveChannel { get; set; }
    public bool PreferredErrorControlledReceiveChannel { get; set; }
    public bool XidProfileExchangeSupported { get; set; }
    public bool AsymmetricDataTypesSupported { get; set; }
    public bool DlciSupported { get; set; }
    public bool IRawBitSupported { get; set; }
    public bool ICharStaticSupported { get; set; }
    public bool ICharDynamicSupported { get; set; }
    public bool IFrameSupported { get; set; }
    public bool IOctetCharacterSequenceSupported { get; set; }
    public bool ICharStaticCharacterSequenceSupported { get; set; }
    public bool ICharDynamicCharacterSequenceSupported { get; set; }
    public bool IRawBitAvailable { get; set; }
    public bool IFrameAvailable { get; set; }
    public bool IOctetWithDlciAvailable { get; set; }
    public bool IOctetWithoutDlciAvailable { get; set; }
    public bool ICharStaticAvailable { get; set; }
    public bool ICharDynamicAvailable { get; set; }
    public bool IOctetCharacterSequenceAvailable { get; set; }
    public bool ICharStaticCharacterSequenceAvailable { get; set; }
    public bool ICharDynamicCharacterSequenceAvailable { get; set; }
    public ushort CompressionTransmitDictionarySize { get; set; }
    public ushort CompressionReceiveDictionarySize { get; set; }
    public byte CompressionTransmitStringLength { get; set; }
    public byte CompressionReceiveStringLength { get; set; }
    public ushort CompressionTransmitHistorySize { get; set; }
    public ushort CompressionReceiveHistorySize { get; set; }
    public bool[] JmCategorySeen { get; } = new bool[V1501Constants.JmCategoryCount];
    public ushort[] JmCategoryInfo { get; } = new ushort[V1501Constants.JmCategoryCount];
    public ushort V42BisP0 { get; set; }
    public ushort V42BisP1 { get; set; }
    public ushort V42BisP2 { get; set; }
    public ushort V44C0 { get; set; }
    public ushort V44P0 { get; set; }
    public ushort V44P1Transmit { get; set; }
    public ushort V44P1Receive { get; set; }
    public ushort V44P2Transmit { get; set; }
    public ushort V44P2Receive { get; set; }
    public ushort V44P3Transmit { get; set; }
    public ushort V44P3Receive { get; set; }
    public V1501CompressionDirection SelectedCompressionDirection { get; set; }
    public V1501Compression SelectedCompression { get; set; }
    public V1501ErrorCorrection SelectedErrorCorrection { get; set; }
    public ushort Dlci { get; set; }
    public ushort OctetCharacterSequenceNextSequenceNumber { get; set; }
    public byte DataFormatCode { get; set; }
    public V1501SelectedModulation SelectedModulation { get; set; }
    public bool TransmitSymbolRateEnabled { get; set; }
    public bool ReceiveSymbolRateEnabled { get; set; }
    public ushort TransmitDataSignallingRate { get; set; }
    public ushort ReceiveDataSignallingRate { get; set; }
    public V1501SymbolRate TransmitSymbolRate { get; set; }
    public V1501SymbolRate ReceiveSymbolRate { get; set; }
    public bool Busy { get; set; }
    public int SprtSubsessionId { get; set; }
    public byte SprtPayloadType { get; set; }
    public V1501ConnectionState ConnectionState { get; set; }
    public V1501CleardownReason CleardownReason { get; set; }

    public void Reset() {
        CallDiscriminationSelection = V1501CallDiscriminationSelection.Indeterminate;
        ModemRelayGatewayType = V1501ModemRelayGatewayType.V8;
        V42LapmSupported = false;
        V42AnnexASupported = false;
        V42BisSupported = false;
        V44Supported = false;
        Mnp5Supported = false;
        ErrorCorrectionProtocol = 0;
        PreferredNonErrorControlledReceiveChannel = false;
        PreferredErrorControlledReceiveChannel = false;
        XidProfileExchangeSupported = false;
        AsymmetricDataTypesSupported = false;
        DlciSupported = false;
        IRawBitSupported = false;
        ICharStaticSupported = false;
        ICharDynamicSupported = false;
        IFrameSupported = false;
        IOctetCharacterSequenceSupported = false;
        ICharStaticCharacterSequenceSupported = false;
        ICharDynamicCharacterSequenceSupported = false;
        IRawBitAvailable = false;
        IFrameAvailable = false;
        IOctetWithDlciAvailable = false;
        IOctetWithoutDlciAvailable = false;
        ICharStaticAvailable = false;
        ICharDynamicAvailable = false;
        IOctetCharacterSequenceAvailable = false;
        ICharStaticCharacterSequenceAvailable = false;
        ICharDynamicCharacterSequenceAvailable = false;
        CompressionTransmitDictionarySize = 0;
        CompressionReceiveDictionarySize = 0;
        CompressionTransmitStringLength = 0;
        CompressionReceiveStringLength = 0;
        CompressionTransmitHistorySize = 0;
        CompressionReceiveHistorySize = 0;
        Array.Clear(JmCategorySeen);
        Array.Clear(JmCategoryInfo);
        V42BisP0 = 0;
        V42BisP1 = 0;
        V42BisP2 = 0;
        V44C0 = 0;
        V44P0 = 0;
        V44P1Transmit = 0;
        V44P1Receive = 0;
        V44P2Transmit = 0;
        V44P2Receive = 0;
        V44P3Transmit = 0;
        V44P3Receive = 0;
        SelectedCompressionDirection = V1501CompressionDirection.NeitherWay;
        SelectedCompression = V1501Compression.None;
        SelectedErrorCorrection = V1501ErrorCorrection.None;
        Dlci = 0;
        OctetCharacterSequenceNextSequenceNumber = 0;
        DataFormatCode = 0;
        SelectedModulation = V1501SelectedModulation.Null;
        TransmitSymbolRateEnabled = false;
        ReceiveSymbolRateEnabled = false;
        TransmitDataSignallingRate = 0;
        ReceiveDataSignallingRate = 0;
        TransmitSymbolRate = V1501SymbolRate.Null;
        ReceiveSymbolRate = V1501SymbolRate.Null;
        Busy = false;
        SprtSubsessionId = 0;
        SprtPayloadType = 0;
        ConnectionState = V1501ConnectionState.Idle;
        CleardownReason = V1501CleardownReason.Unknown;
    }
}

public sealed class V1501NearState {
    public V1501Parameters Parameters { get; } = new();
    public int[] InformationMessagePreferences { get; } = new int[V1501Constants.InformationPreferenceCount];
    public int[] MaximumPayloadBytes { get; } = new int[SprtConstants.ChannelCount];
    public SprtTransmissionChannel InformationStreamChannel { get; set; } = SprtTransmissionChannel.UnreliableSequenced;
    public V1501MessageId InformationStreamMessageId { get; set; } = V1501MessageId.IRawOctet;

    public void Reset() {
        Parameters.Reset();
        Array.Fill(InformationMessagePreferences, -1);
        Array.Clear(MaximumPayloadBytes);
        InformationStreamChannel = SprtTransmissionChannel.UnreliableSequenced;
        InformationStreamMessageId = V1501MessageId.IRawOctet;
    }
}

public sealed class V1501FarState {
    public V1501Parameters Parameters { get; } = new();
    public V1501BreakSource BreakSource { get; set; }
    public V1501BreakType BreakType { get; set; }
    public int BreakDurationUnits10Milliseconds { get; set; }

    public void Reset() {
        Parameters.Reset();
        BreakSource = V1501BreakSource.V42Lapm;
        BreakType = V1501BreakType.NotApplicable;
        BreakDurationUnits10Milliseconds = 0;
    }
}

public interface IV1501SseBridge {
    void Initialize(V1501State state, V1501SseTransmitPacketHandler? transmitHandler, object? transmitUserData);
    int TimerExpired(V1501State state, ulong now);
}

public sealed class NullV1501SseBridge : IV1501SseBridge {
    public static NullV1501SseBridge Instance { get; } = new();
    private NullV1501SseBridge() { }
    public void Initialize(V1501State state, V1501SseTransmitPacketHandler? transmitHandler, object? transmitUserData) { }
    public int TimerExpired(V1501State state, ulong now) => 0;
}

public sealed partial class V1501State : IDisposable {
    private static readonly byte[] ChannelCheck =
    {
        0x0F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0F, 0x0F, 0x04, 0x04, 0x04,
        0x00, 0x00, 0x00, 0x00, 0x00,
        0x0A, 0x0A, 0x0A, 0x0A, 0x0A, 0x0A, 0x0A, 0x0A, 0x0A
    };

    private static readonly (int Minimum, int Maximum)[] ChannelPayloadLimits =
    {
        (SprtConstants.MinTc0PayloadBytes, SprtConstants.MaxTc0PayloadBytes),
        (SprtConstants.MinTc1PayloadBytes, SprtConstants.MaxTc1PayloadBytes),
        (SprtConstants.MinTc2PayloadBytes, SprtConstants.MaxTc2PayloadBytes),
        (SprtConstants.MinTc3PayloadBytes, SprtConstants.MaxTc3PayloadBytes)
    };

    private V1501RxDataHandler? _receiveDataHandler;
    private object? _receiveDataUserData;
    private V1501RxStatusReportHandler? _receiveStatusReportHandler;
    private object? _receiveStatusReportUserData;
    private V1501SpeSignalHandler? _speSignalHandler;
    private object? _speSignalUserData;
    private V1501TimerHandler? _timerHandler;
    private object? _timerUserData;
    private V1501SseTransmitPacketHandler? _sseTransmitPacketHandler;
    private object? _sseTransmitUserData;
    private IV1501SseBridge? _sseBridge;
    private SprtState? _sprt;
    private bool _disposed;

    public V1501State() => ResetState();

    public V1501State(
        SprtTransmitPacketHandler sprtTransmitPacketHandler,
        object? sprtTransmitUserData,
        byte sprtTransmitPayloadType,
        byte sprtReceivePayloadType,
        V1501SseTransmitPacketHandler? sseTransmitPacketHandler,
        object? sseTransmitUserData,
        V1501TimerHandler? timerHandler,
        object? timerUserData,
        V1501RxDataHandler receiveDataHandler,
        object? receiveDataUserData,
        V1501RxStatusReportHandler receiveStatusReportHandler,
        object? receiveStatusReportUserData,
        V1501SpeSignalHandler? speSignalHandler,
        object? speSignalUserData,
        IV1501SseBridge? sseBridge = null) {
        Initialize(
            sprtTransmitPacketHandler,
            sprtTransmitUserData,
            sprtTransmitPayloadType,
            sprtReceivePayloadType,
            sseTransmitPacketHandler,
            sseTransmitUserData,
            timerHandler,
            timerUserData,
            receiveDataHandler,
            receiveDataUserData,
            receiveStatusReportHandler,
            receiveStatusReportUserData,
            speSignalHandler,
            speSignalUserData,
            sseBridge);
    }

    public V1501Logger Logging { get; } = new();
    public V1501NearState Near { get; } = new();
    public V1501FarState Far { get; } = new();
    public V1501CallDiscriminationSelection JointCallDiscriminationSelection { get; private set; }
    public bool Rfc4733Preferred { get; private set; }
    public int CallDiscriminationTimeout { get; private set; }
    public V1501MediaState LocalMediaState { get; internal set; }
    public V1501MediaState RemoteMediaState { get; internal set; }
    public V1501MediaState RemoteAcknowledgement { get; set; }
    public V1501ConnectionState JointConnectionState { get; private set; }
    public ulong LatestTimer { get; private set; }
    public ulong CallDiscriminationTimer { get; private set; }
    public ulong SseTimer { get; private set; }
    public ulong SprtTimer { get; private set; }
    public bool IsDisposed => _disposed;
    public SprtState Sprt => _sprt ?? throw new InvalidOperationException("SPRT is not initialized.");
    public event V1501IpSignalHandler? IpSignalRequested;

    public void Initialize(
        SprtTransmitPacketHandler sprtTransmitPacketHandler,
        object? sprtTransmitUserData,
        byte sprtTransmitPayloadType,
        byte sprtReceivePayloadType,
        V1501SseTransmitPacketHandler? sseTransmitPacketHandler,
        object? sseTransmitUserData,
        V1501TimerHandler? timerHandler,
        object? timerUserData,
        V1501RxDataHandler receiveDataHandler,
        object? receiveDataUserData,
        V1501RxStatusReportHandler receiveStatusReportHandler,
        object? receiveStatusReportUserData,
        V1501SpeSignalHandler? speSignalHandler,
        object? speSignalUserData,
        IV1501SseBridge? sseBridge = null) {
        ArgumentNullException.ThrowIfNull(sprtTransmitPacketHandler);
        ArgumentNullException.ThrowIfNull(receiveDataHandler);
        ArgumentNullException.ThrowIfNull(receiveStatusReportHandler);

        _sprt?.Dispose();
        ResetState();
        _disposed = false;

        _receiveDataHandler = receiveDataHandler;
        _receiveDataUserData = receiveDataUserData;
        _receiveStatusReportHandler = receiveStatusReportHandler;
        _receiveStatusReportUserData = receiveStatusReportUserData;
        _speSignalHandler = speSignalHandler;
        _speSignalUserData = speSignalUserData;
        _timerHandler = timerHandler;
        _timerUserData = timerUserData;
        _sseTransmitPacketHandler = sseTransmitPacketHandler;
        _sseTransmitUserData = sseTransmitUserData;
        _sseBridge = sseBridge;

        Near.Parameters.SprtSubsessionId = 0;
        Near.Parameters.SprtPayloadType = sprtTransmitPayloadType;
        Far.Parameters.SprtPayloadType = sprtReceivePayloadType;

        if (_sseBridge is null)
            V1501Sse.Initialize(this, sseTransmitPacketHandler, sseTransmitUserData);
        else
            _sseBridge.Initialize(this, sseTransmitPacketHandler, sseTransmitUserData);

        _sprt = new SprtState(
            checked((byte)Near.Parameters.SprtSubsessionId),
            sprtReceivePayloadType,
            sprtTransmitPayloadType,
            null,
            sprtTransmitPacketHandler,
            sprtTransmitUserData,
            ProcessReceivedSprtMessage,
            this,
            UpdateSprtTimerCallback,
            this,
            SprtStatusCallback,
            this);
    }

    public int StateMachine(V1501Signal signal, ReadOnlySpan<byte> message = default) {
        ThrowIfDisposed();
        Logging.Flow($"State machine - {MediaStateToString(LocalMediaState)}   {MediaStateToString(RemoteMediaState)}   {SignalToString(signal)}");

        switch (signal) {
            case V1501Signal.Silence:
                if (LocalMediaState != V1501MediaState.InitialAudio || RemoteMediaState != V1501MediaState.InitialAudio) {
                    RemoteMediaState = V1501MediaState.Indeterminate;
                    LocalMediaState = V1501MediaState.InitialAudio;
                    ReportStatus(V1501StatusReason.MediaStateChanged);
                    GenericMacro(signal, 0);
                }
                return 0;

            case V1501Signal.Abort:
                RemoteMediaState = V1501MediaState.Indeterminate;
                LocalMediaState = V1501MediaState.InitialAudio;
                ReportStatus(V1501StatusReason.MediaStateChanged);
                GenericMacro(signal, 0);
                return 0;

            case V1501Signal.CallDiscriminationTimerExpired:
                RemoteMediaState = V1501MediaState.Indeterminate;
                LocalMediaState = V1501MediaState.InitialAudio;
                ReportStatus(V1501StatusReason.MediaStateChanged);
                return 0;
        }

        return LocalMediaState switch {
            V1501MediaState.InitialAudio => RemoteMediaState switch {
                V1501MediaState.InitialAudio => Figures26To31(signal, message),
                V1501MediaState.VoiceBandData => Figure33(signal),
                V1501MediaState.ModemRelay => Figure32(signal),
                _ => 0
            },
            V1501MediaState.VoiceBandData => RemoteMediaState switch {
                V1501MediaState.InitialAudio => Figure37(signal),
                V1501MediaState.VoiceBandData => Figure39(signal),
                V1501MediaState.ModemRelay => Figure38(signal),
                _ => 0
            },
            V1501MediaState.ModemRelay => RemoteMediaState switch {
                V1501MediaState.InitialAudio => Figure34(signal),
                V1501MediaState.VoiceBandData => Figure36(signal),
                V1501MediaState.ModemRelay => Figure35(signal),
                _ => 0
            },
            _ => 0
        };
    }

    public int SetBitsPerCharacter(int bits) {
        ThrowIfDisposed();
        if (bits is < 5 or > 8)
            return -1;
        int code = bits - 5;
        Near.Parameters.DataFormatCode = unchecked((byte)((Near.Parameters.DataFormatCode & 0x9F) | ((code << 5) & 0x60)));
        return 0;
    }

    public int SetParity(int mode) {
        ThrowIfDisposed();
        if ((uint)mode > 7u)
            return -1;
        Near.Parameters.DataFormatCode = unchecked((byte)((Near.Parameters.DataFormatCode & 0xE3) | ((mode << 2) & 0x1C)));
        return 0;
    }

    public int SetStopBits(int bits) {
        ThrowIfDisposed();
        if (bits is < 1 or > 2)
            return -1;
        Near.Parameters.DataFormatCode = unchecked((byte)((Near.Parameters.DataFormatCode & 0xFC) | ((bits - 1) & 0x03)));
        return 0;
    }

    public int TransmitNull() => TransmitControl(V1501MessageId.Null, new byte[] { (byte)V1501MessageId.Null }, "NULL sent");

    public int TransmitInit() {
        ThrowIfDisposed();
        Span<byte> packet = stackalloc byte[3];
        packet[0] = (byte)V1501MessageId.Init;
        byte first = 0;
        if (Near.Parameters.PreferredNonErrorControlledReceiveChannel) first |= 0x80;
        if (Near.Parameters.PreferredErrorControlledReceiveChannel) first |= 0x40;
        if (Near.Parameters.XidProfileExchangeSupported) first |= 0x20;
        if (Near.Parameters.AsymmetricDataTypesSupported) first |= 0x10;
        if (Near.Parameters.IRawBitSupported) first |= 0x08;
        if (Near.Parameters.IFrameSupported) first |= 0x04;
        if (Near.Parameters.ICharStaticSupported) first |= 0x02;
        if (Near.Parameters.ICharDynamicSupported) first |= 0x01;
        packet[1] = first;
        byte second = 0;
        if (Near.Parameters.IOctetCharacterSequenceSupported) second |= 0x80;
        if (Near.Parameters.ICharStaticCharacterSequenceSupported) second |= 0x40;
        if (Near.Parameters.ICharDynamicCharacterSequenceSupported) second |= 0x20;
        packet[2] = second;
        Logging.Flow("Sending INIT");
        LogInit(Near.Parameters);
        int result = Sprt.TransmitMessage((int)SprtTransmissionChannel.ExpeditedReliableSequenced, packet);
        if (result >= 0) {
            Near.Parameters.ConnectionState = V1501ConnectionState.Initialized;
            if (Far.Parameters.ConnectionState >= V1501ConnectionState.Initialized) {
                SelectInformationMessageType();
                JointConnectionState = V1501ConnectionState.Initialized;
            }
        }
        return result;
    }

    public int TransmitXidExchange() {
        ThrowIfDisposed();
        if (!Far.Parameters.XidProfileExchangeSupported)
            return -1;
        Span<byte> packet = stackalloc byte[19];
        packet.Clear();
        packet[0] = (byte)V1501MessageId.XidExchange;
        packet[1] = unchecked((byte)Near.Parameters.ErrorCorrectionProtocol);
        if (Near.Parameters.V42BisSupported) packet[2] |= 0x80;
        if (Near.Parameters.V44Supported) packet[2] |= 0x40;
        if (Near.Parameters.Mnp5Supported) packet[2] |= 0x20;
        if (Near.Parameters.V42BisSupported) {
            packet[3] = unchecked((byte)Near.Parameters.V42BisP0);
            WriteUInt16(packet, 4, Near.Parameters.V42BisP1);
            packet[6] = unchecked((byte)Near.Parameters.V42BisP2);
        }
        if (Near.Parameters.V44Supported) {
            packet[7] = unchecked((byte)Near.Parameters.V44C0);
            packet[8] = unchecked((byte)Near.Parameters.V44P0);
            WriteUInt16(packet, 9, Near.Parameters.V44P1Transmit);
            WriteUInt16(packet, 11, Near.Parameters.V44P1Receive);
            packet[13] = unchecked((byte)Near.Parameters.V44P2Transmit);
            packet[14] = unchecked((byte)Near.Parameters.V44P2Receive);
            WriteUInt16(packet, 15, Near.Parameters.V44P3Transmit);
            WriteUInt16(packet, 17, Near.Parameters.V44P3Receive);
        }
        int result = Sprt.TransmitMessage((int)SprtTransmissionChannel.ExpeditedReliableSequenced, packet);
        Logging.Flow("XID xchg sent");
        return result;
    }

    public int TransmitJmInfo() {
        ThrowIfDisposed();
        Span<byte> packet = stackalloc byte[V1501Constants.MaximumPacketBytes];
        packet[0] = (byte)V1501MessageId.JmInfo;
        int length = 1;
        for (int i = 0; i < V1501Constants.JmCategoryCount; i++) {
            if (!Near.Parameters.JmCategorySeen[i])
                continue;
            Logging.Flow($"    JM {JmCategoryToString(i)} 0x{Near.Parameters.JmCategoryInfo[i]:x}");
            WriteUInt16(packet, length, unchecked((ushort)((i << 12) | (Near.Parameters.JmCategoryInfo[i] & 0x0FFF))));
            length += 2;
        }
        int result = Sprt.TransmitMessage((int)SprtTransmissionChannel.ExpeditedReliableSequenced, packet[..length]);
        Logging.Flow("JM info sent");
        return result;
    }

    public int TransmitStartJm() {
        ThrowIfDisposed();
        if (Near.Parameters.ConnectionState == V1501ConnectionState.Idle)
            return -1;
        Span<byte> packet = stackalloc byte[1];
        packet[0] = (byte)V1501MessageId.StartJm;
        int result = Sprt.TransmitMessage((int)SprtTransmissionChannel.ExpeditedReliableSequenced, packet);
        Logging.Flow("Start JM sent");
        return result;
    }

    public int TransmitConnect() {
        ThrowIfDisposed();
        Span<byte> packet = stackalloc byte[19];
        packet.Clear();
        packet[0] = (byte)V1501MessageId.Connect;
        packet[1] = unchecked((byte)(((int)Near.Parameters.SelectedModulation << 2) | (int)Near.Parameters.SelectedCompressionDirection));
        packet[2] = unchecked((byte)(((int)Near.Parameters.SelectedCompression << 4) | (int)Near.Parameters.SelectedErrorCorrection));
        WriteUInt16(packet, 3, Near.Parameters.TransmitDataSignallingRate);
        WriteUInt16(packet, 5, Near.Parameters.ReceiveDataSignallingRate);
        int available = 0;
        if (Near.Parameters.IOctetWithDlciAvailable) available |= 0x8000;
        if (Near.Parameters.IOctetWithoutDlciAvailable) available |= 0x4000;
        if (Near.Parameters.IRawBitAvailable) available |= 0x2000;
        if (Near.Parameters.IFrameAvailable) available |= 0x1000;
        if (Near.Parameters.ICharStaticAvailable) available |= 0x0800;
        if (Near.Parameters.ICharDynamicAvailable) available |= 0x0400;
        if (Near.Parameters.IOctetCharacterSequenceAvailable) available |= 0x0200;
        if (Near.Parameters.ICharStaticCharacterSequenceAvailable) available |= 0x0100;
        if (Near.Parameters.ICharDynamicCharacterSequenceAvailable) available |= 0x0080;
        WriteUInt16(packet, 7, unchecked((ushort)available));
        int length = 9;
        if (Near.Parameters.SelectedCompression is V1501Compression.V42Bis or V1501Compression.V44) {
            WriteUInt16(packet, 9, Near.Parameters.CompressionTransmitDictionarySize);
            WriteUInt16(packet, 11, Near.Parameters.CompressionReceiveDictionarySize);
            packet[13] = Near.Parameters.CompressionTransmitStringLength;
            packet[14] = Near.Parameters.CompressionReceiveStringLength;
            length = 15;
        }
        if (Near.Parameters.SelectedCompression == V1501Compression.V44) {
            WriteUInt16(packet, 15, Near.Parameters.CompressionTransmitHistorySize);
            WriteUInt16(packet, 17, Near.Parameters.CompressionReceiveHistorySize);
            length = 19;
        }
        int result = Sprt.TransmitMessage((int)SprtTransmissionChannel.ExpeditedReliableSequenced, packet[..length]);
        if (result >= 0) {
            Near.Parameters.ConnectionState = V1501ConnectionState.Connected;
            if (Far.Parameters.ConnectionState >= V1501ConnectionState.Connected)
                JointConnectionState = V1501ConnectionState.Connected;
            Logging.Flow("Connect sent");
        }
        return result;
    }

    public int TransmitBreak(V1501BreakSource source, V1501BreakType type, int durationMilliseconds) {
        ThrowIfDisposed();
        if (Near.Parameters.ConnectionState == V1501ConnectionState.Idle || durationMilliseconds < 0)
            return -1;
        Span<byte> packet = stackalloc byte[3];
        packet[0] = (byte)V1501MessageId.Break;
        packet[1] = unchecked((byte)(((int)source << 4) | (int)type));
        packet[2] = unchecked((byte)Math.Clamp(durationMilliseconds / 10, 0, 255));
        int result = Sprt.TransmitMessage((int)SprtTransmissionChannel.ExpeditedReliableSequenced, packet);
        if (result >= 0) Logging.Flow("Break sent");
        return result;
    }

    public int TransmitBreakAck() {
        ThrowIfDisposed();
        if (Near.Parameters.ConnectionState == V1501ConnectionState.Idle)
            return -1;
        Span<byte> packet = stackalloc byte[1];
        packet[0] = (byte)V1501MessageId.BreakAck;
        int result = Sprt.TransmitMessage((int)SprtTransmissionChannel.ExpeditedReliableSequenced, packet);
        if (result >= 0) Logging.Flow("Break ACK sent");
        return result;
    }

    public int TransmitMrEvent(V1501MrEventId eventId) {
        ThrowIfDisposed();
        Span<byte> packet = stackalloc byte[10];
        packet.Clear();
        packet[0] = (byte)V1501MessageId.MrEvent;
        packet[1] = (byte)eventId;
        int length;
        switch (eventId) {
            case V1501MrEventId.Retrain:
                packet[2] = (byte)V1501MrEventReason.Null;
                length = 3;
                Near.Parameters.ConnectionState = V1501ConnectionState.Retrain;
                JointConnectionState = V1501ConnectionState.Retrain;
                break;
            case V1501MrEventId.RateRenegotiation:
                packet[2] = (byte)V1501MrEventReason.Null;
                length = 3;
                Near.Parameters.ConnectionState = V1501ConnectionState.RateRenegotiation;
                JointConnectionState = V1501ConnectionState.RateRenegotiation;
                break;
            case V1501MrEventId.PhysicallyUp:
                byte flags = unchecked((byte)((int)Near.Parameters.SelectedModulation << 2));
                if (Near.Parameters.TransmitSymbolRateEnabled) flags |= 0x02;
                if (Near.Parameters.ReceiveSymbolRateEnabled) flags |= 0x01;
                packet[3] = flags;
                WriteUInt16(packet, 4, Near.Parameters.TransmitDataSignallingRate);
                WriteUInt16(packet, 6, Near.Parameters.ReceiveDataSignallingRate);
                packet[8] = Near.Parameters.TransmitSymbolRateEnabled ? (byte)Near.Parameters.TransmitSymbolRate : (byte)V1501SymbolRate.Null;
                packet[9] = Near.Parameters.ReceiveSymbolRateEnabled ? (byte)Near.Parameters.ReceiveSymbolRate : (byte)V1501SymbolRate.Null;
                length = 10;
                Near.Parameters.ConnectionState = V1501ConnectionState.PhysicallyUp;
                if (Far.Parameters.ConnectionState >= V1501ConnectionState.PhysicallyUp)
                    JointConnectionState = V1501ConnectionState.PhysicallyUp;
                break;
            default:
                length = 3;
                break;
        }
        int result = Sprt.TransmitMessage((int)SprtTransmissionChannel.ExpeditedReliableSequenced, packet[..length]);
        if (result >= 0) Logging.Flow($"MR-event {MrEventTypeToString(eventId)} ({(int)eventId}) sent");
        return result;
    }

    public int TransmitCleardown(V1501CleardownReason reason) {
        ThrowIfDisposed();
        if (Near.Parameters.ConnectionState == V1501ConnectionState.Idle)
            return -1;
        Span<byte> packet = stackalloc byte[4];
        packet[0] = (byte)V1501MessageId.Cleardown;
        packet[1] = (byte)reason;
        packet[2] = 0;
        packet[3] = 0;
        int result = Sprt.TransmitMessage((int)SprtTransmissionChannel.ExpeditedReliableSequenced, packet);
        if (result >= 0) {
            Near.Parameters.ConnectionState = V1501ConnectionState.Idle;
            Logging.Flow("Cleardown sent");
        }
        return result;
    }

    public int TransmitProfileExchange() {
        ThrowIfDisposed();
        Span<byte> packet = stackalloc byte[19];
        packet.Clear();
        packet[0] = (byte)V1501MessageId.ProfileExchange;
        if (Near.Parameters.V42LapmSupported) packet[1] |= 0x40;
        if (Near.Parameters.V42AnnexASupported) packet[1] |= 0x10;
        if (Near.Parameters.V44Supported) packet[1] |= 0x04;
        if (Near.Parameters.V42BisSupported) packet[1] |= 0x01;
        if (Near.Parameters.Mnp5Supported) packet[2] |= 0x40;
        if (Near.Parameters.V42BisSupported) {
            packet[3] = unchecked((byte)Near.Parameters.V42BisP0);
            WriteUInt16(packet, 4, Near.Parameters.V42BisP1);
            packet[6] = unchecked((byte)Near.Parameters.V42BisP2);
        }
        if (Near.Parameters.V44Supported) {
            packet[7] = unchecked((byte)Near.Parameters.V44C0);
            packet[8] = unchecked((byte)Near.Parameters.V44P0);
            WriteUInt16(packet, 9, Near.Parameters.V44P1Transmit);
            WriteUInt16(packet, 11, Near.Parameters.V44P1Receive);
            packet[13] = unchecked((byte)Near.Parameters.V44P2Transmit);
            packet[14] = unchecked((byte)Near.Parameters.V44P2Receive);
            WriteUInt16(packet, 15, Near.Parameters.V44P3Transmit);
            WriteUInt16(packet, 17, Near.Parameters.V44P3Receive);
        }
        int result = Sprt.TransmitMessage((int)SprtTransmissionChannel.ExpeditedReliableSequenced, packet);
        Logging.Flow("Prof xchg sent");
        return result;
    }

    public int TransmitInformationStream(ReadOnlySpan<byte> data) {
        ThrowIfDisposed();
        int channel = (int)Near.InformationStreamChannel;
        if ((uint)channel >= SprtConstants.ChannelCount)
            return -1;
        int maximumLength = Near.MaximumPayloadBytes[channel];
        Span<byte> packet = stackalloc byte[V1501Constants.MaximumPacketBytes];
        int length = Near.InformationStreamMessageId switch {
            V1501MessageId.IRawOctet => BuildIRawOctet(packet, maximumLength, data),
            V1501MessageId.IRawBit => BuildIRawBit(packet, maximumLength, data),
            V1501MessageId.IOctet => BuildIOctet(packet, maximumLength, data),
            V1501MessageId.ICharStatic => BuildICharStatic(packet, maximumLength, data),
            V1501MessageId.ICharDynamic => BuildICharDynamic(packet, maximumLength, data),
            V1501MessageId.IFrame => BuildIFrame(packet, maximumLength, data),
            V1501MessageId.IOctetCharacterSequence => BuildIOctetCharacterSequence(packet, maximumLength, data),
            V1501MessageId.ICharStaticCharacterSequence => BuildICharStaticCharacterSequence(packet, maximumLength, data),
            V1501MessageId.ICharDynamicCharacterSequence => BuildICharDynamicCharacterSequence(packet, maximumLength, data),
            _ => -1
        };
        if (length < 0) {
            Logging.Flow("Bad message");
            return -1;
        }
        return Sprt.TransmitMessage(channel, packet[..length]);
    }

    public int ProcessReceivedSprtMessage(object? userData, int channel, int sequenceNumber, ReadOnlySpan<byte> message) {
        ThrowIfDisposed();
        Logging.Flow($"{SprtState.TransmissionChannelToString(channel)} ({channel}) seq {sequenceNumber}");
        Logging.Buffer(V1501LogLevel.Flow, string.Empty, message);
        if ((uint)channel >= SprtConstants.ChannelCount || message.IsEmpty) {
            Logging.Error($"Packet arrived on invalid channel {channel} or without payload");
            return -1;
        }
        if ((message[0] & 0x80) != 0) {
            Logging.Flow("Extended message IDs are not supported");
            return -1;
        }
        int idValue = message[0] & 0x7F;
        Logging.Flow($"Message {MessageIdToString(idValue)} received on channel {channel}, seq no {sequenceNumber}");
        if (idValue < ChannelCheck.Length && (ChannelCheck[idValue] & (1 << channel)) == 0) {
            Logging.Flow($"Bad channel for message ID {idValue}");
            return -1;
        }
        int result = (V1501MessageId)idValue switch {
            V1501MessageId.Null => ProcessNull(message),
            V1501MessageId.Init => ProcessInit(message),
            V1501MessageId.XidExchange => ProcessXidExchange(message),
            V1501MessageId.JmInfo => ProcessJmInfo(message),
            V1501MessageId.StartJm => ProcessStartJm(message),
            V1501MessageId.Connect => ProcessConnect(message),
            V1501MessageId.Break => ProcessBreak(message),
            V1501MessageId.BreakAck => ProcessBreakAck(message),
            V1501MessageId.MrEvent => ProcessMrEvent(message),
            V1501MessageId.Cleardown => ProcessCleardown(message),
            V1501MessageId.ProfileExchange => ProcessProfileExchange(message),
            V1501MessageId.IRawOctet => ProcessIRawOctet(message),
            V1501MessageId.IRawBit => ProcessIRawBit(message),
            V1501MessageId.IOctet => ProcessIOctet(message),
            V1501MessageId.ICharStatic => ProcessICharStatic(message),
            V1501MessageId.ICharDynamic => ProcessICharDynamic(message),
            V1501MessageId.IFrame => ProcessIFrame(message),
            V1501MessageId.IOctetCharacterSequence => ProcessIOctetCharacterSequence(message),
            V1501MessageId.ICharStaticCharacterSequence => ProcessICharStaticCharacterSequence(message),
            V1501MessageId.ICharDynamicCharacterSequence => ProcessICharDynamicCharacterSequence(message),
            _ => -1
        };
        if (result < 0) Logging.Flow("Bad message");
        return result;
    }

    public int SetLocalBusy(bool busy) {
        ThrowIfDisposed();
        bool previous = Near.Parameters.Busy;
        Near.Parameters.Busy = busy;
        if (previous != busy)
            ReportStatus(V1501StatusReason.BusyChanged);
        return previous ? 1 : 0;
    }

    public bool GetFarBusyStatus() {
        ThrowIfDisposed();
        return Far.Parameters.Busy;
    }

    public int SetLocalTransportChannelPayloadBytes(int channel, int maximumLength) {
        ThrowIfDisposed();
        if ((uint)channel >= SprtConstants.ChannelCount)
            return -1;
        (int minimum, int maximum) = ChannelPayloadLimits[channel];
        if (maximumLength < minimum || maximumLength > maximum)
            return -1;
        Near.MaximumPayloadBytes[channel] = maximumLength;
        return Sprt.SetLocalPayloadBytes(channel, maximumLength);
    }

    public int GetLocalTransportChannelPayloadBytes(int channel) {
        ThrowIfDisposed();
        return (uint)channel < SprtConstants.ChannelCount ? Near.MaximumPayloadBytes[channel] : -1;
    }

    public int SetInformationStreamTransmitMode(int channel, int messageId) {
        ThrowIfDisposed();
        if ((uint)channel >= SprtConstants.ChannelCount || !IsInformationMessage((V1501MessageId)messageId))
            return -1;
        Near.InformationStreamChannel = (SprtTransmissionChannel)channel;
        Near.InformationStreamMessageId = (V1501MessageId)messageId;
        return 0;
    }

    public int SetInformationStreamMessagePriorities(ReadOnlySpan<int> messageIds) {
        ThrowIfDisposed();
        int count = Math.Min(messageIds.Length, V1501Constants.InformationPreferenceCount);
        int i = 0;
        for (; i < count && messageIds[i] >= 0; i++) {
            if (!IsInformationMessage((V1501MessageId)messageIds[i]))
                return -1;
        }
        Array.Fill(Near.InformationMessagePreferences, -1);
        for (i = 0; i < count && messageIds[i] >= 0; i++)
            Near.InformationMessagePreferences[i] = messageIds[i];
        if (JointConnectionState >= V1501ConnectionState.Initialized)
            SelectInformationMessageType();
        return 0;
    }

    public int SetModulation(int modulation) {
        ThrowIfDisposed();
        Near.Parameters.SelectedModulation = (V1501SelectedModulation)modulation;
        return 0;
    }

    public int SetCompressionDirection(int direction) {
        ThrowIfDisposed();
        Near.Parameters.SelectedCompressionDirection = (V1501CompressionDirection)direction;
        return 0;
    }

    public int SetCompression(int compression) {
        ThrowIfDisposed();
        Near.Parameters.SelectedCompression = (V1501Compression)compression;
        return 0;
    }

    public int SetCompressionParameters(int txDictionarySize, int rxDictionarySize, int txStringLength, int rxStringLength, int txHistorySize, int rxHistorySize) {
        ThrowIfDisposed();
        if (!FitsUInt16(txDictionarySize) || !FitsUInt16(rxDictionarySize) || !FitsByte(txStringLength) || !FitsByte(rxStringLength) || !FitsUInt16(txHistorySize) || !FitsUInt16(rxHistorySize))
            return -1;
        Near.Parameters.CompressionTransmitDictionarySize = (ushort)txDictionarySize;
        Near.Parameters.CompressionReceiveDictionarySize = (ushort)rxDictionarySize;
        Near.Parameters.CompressionTransmitStringLength = (byte)txStringLength;
        Near.Parameters.CompressionReceiveStringLength = (byte)rxStringLength;
        Near.Parameters.CompressionTransmitHistorySize = (ushort)txHistorySize;
        Near.Parameters.CompressionReceiveHistorySize = (ushort)rxHistorySize;
        return 0;
    }

    public int SetErrorCorrection(int errorCorrection) {
        ThrowIfDisposed();
        Near.Parameters.SelectedErrorCorrection = (V1501ErrorCorrection)errorCorrection;
        return 0;
    }

    public int SetTransmitSymbolRate(bool enable, int rate) {
        ThrowIfDisposed();
        Near.Parameters.TransmitSymbolRateEnabled = enable;
        Near.Parameters.TransmitSymbolRate = enable ? (V1501SymbolRate)rate : V1501SymbolRate.Null;
        return 0;
    }

    public int SetReceiveSymbolRate(bool enable, int rate) {
        ThrowIfDisposed();
        Near.Parameters.ReceiveSymbolRateEnabled = enable;
        Near.Parameters.ReceiveSymbolRate = enable ? (V1501SymbolRate)rate : V1501SymbolRate.Null;
        return 0;
    }

    public int SetTransmitDataSignallingRate(int rate) {
        ThrowIfDisposed();
        if (!FitsUInt16(rate)) return -1;
        Near.Parameters.TransmitDataSignallingRate = (ushort)rate;
        return 0;
    }

    public int SetReceiveDataSignallingRate(int rate) {
        ThrowIfDisposed();
        if (!FitsUInt16(rate)) return -1;
        Near.Parameters.ReceiveDataSignallingRate = (ushort)rate;
        return 0;
    }

    public void SetNearCallDiscriminationSelection(V1501CallDiscriminationSelection selection) {
        ThrowIfDisposed();
        Near.Parameters.CallDiscriminationSelection = selection;
        SetJointCallDiscriminationSelection();
    }

    public void SetFarCallDiscriminationSelection(V1501CallDiscriminationSelection selection) {
        ThrowIfDisposed();
        Far.Parameters.CallDiscriminationSelection = selection;
        SetJointCallDiscriminationSelection();
    }

    public void SetNearModemRelayGatewayType(V1501ModemRelayGatewayType type) {
        ThrowIfDisposed();
        Near.Parameters.ModemRelayGatewayType = type;
    }

    public void SetFarModemRelayGatewayType(V1501ModemRelayGatewayType type) {
        ThrowIfDisposed();
        Far.Parameters.ModemRelayGatewayType = type;
    }

    public void SetRfc4733Mode(bool preferred) {
        ThrowIfDisposed();
        Rfc4733Preferred = preferred;
    }

    public void SetCallDiscriminationTimeout(int timeout) {
        ThrowIfDisposed();
        CallDiscriminationTimeout = Math.Max(0, timeout);
    }

    public int TimerExpired(ulong now) {
        ThrowIfDisposed();
        Logging.Flow($"V.150.1 timer expired at {now}");
        if (now < LatestTimer) {
            Logging.Flow($"V.150.1 timer returned {LatestTimer - now}us early");
            _timerHandler?.Invoke(_timerUserData, LatestTimer);
            return 0;
        }
        if (CallDiscriminationTimer != 0 && CallDiscriminationTimer <= now) {
            Logging.Flow("Call discrimination timer expired");
            CallDiscriminationTimer = 0;
            StateMachine(V1501Signal.CallDiscriminationTimerExpired);
        }
        if (SseTimer != 0 && SseTimer <= now) {
            Logging.Flow("SSE timer expired");
            if (_sseBridge is null)
                V1501Sse.TimerExpired(this, now);
            else
                _sseBridge.TimerExpired(this, now);
        }
        if (SprtTimer != 0 && SprtTimer <= now) {
            Logging.Flow("SPRT timer expired");
            Sprt.TimerExpired(now);
        }
        return 0;
    }

    public void InitializeSse(
        V1501SseTransmitPacketHandler? transmitPacketHandler,
        object? transmitUserData) {
        ThrowIfDisposed();
        _sseTransmitPacketHandler = transmitPacketHandler;
        _sseTransmitUserData = transmitUserData;
        if (_sseBridge is null)
            V1501Sse.Initialize(this, transmitPacketHandler, transmitUserData);
        else
            _sseBridge.Initialize(this, transmitPacketHandler, transmitUserData);
    }

    public int SseTimerExpired(ulong now) {
        ThrowIfDisposed();
        return _sseBridge is null
            ? V1501Sse.TimerExpired(this, now)
            : _sseBridge.TimerExpired(this, now);
    }

    public ulong UpdateSseTimer(ulong timeout) {
        ThrowIfDisposed();
        if (timeout != ulong.MaxValue) {
            SseTimer = timeout;
            timeout = SelectTimer();
        }
        return _timerHandler?.Invoke(_timerUserData, timeout) ?? 0;
    }

    public int SseStatusHandler(int status) {
        ThrowIfDisposed();
        Logging.Flow($"SSE status event {status}");
        return 0;
    }

    public int Release() {
        if (_disposed) return 0;
        _sprt?.Release();
        return 0;
    }

    public void Dispose() {
        if (_disposed) return;
        _sprt?.Dispose();
        _sprt = null;
        _receiveDataHandler = null;
        _receiveDataUserData = null;
        _receiveStatusReportHandler = null;
        _receiveStatusReportUserData = null;
        _speSignalHandler = null;
        _speSignalUserData = null;
        _timerHandler = null;
        _timerUserData = null;
        _sseTransmitPacketHandler = null;
        _sseTransmitUserData = null;
        IpSignalRequested = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private int Figures26To31(V1501Signal signal, ReadOnlySpan<byte> message) {
        bool vbdAllowed = JointCallDiscriminationSelection is V1501CallDiscriminationSelection.VbdPreferred or V1501CallDiscriminationSelection.Mixed;
        switch (signal) {
            case V1501Signal.Tone2100Hz:
                if (vbdAllowed) {
                    UpdateMediaStates(V1501MediaState.VoiceBandData, RemoteMediaState);
                    GenericMacro(V1501Signal.Ans, 0);
                } else SendSpeSignal(V1501Signal.Block2100HzTone);
                break;
            case V1501Signal.Ans:
                if (vbdAllowed) {
                    UpdateMediaStates(V1501MediaState.VoiceBandData, RemoteMediaState);
                    GenericMacro(V1501Signal.Ans, 0);
                } else {
                    GenericMacro(V1501Signal.GenerateRfc4733Ans, 0);
                    SendSpeSignal(V1501Signal.ConcealModem);
                }
                break;
            case V1501Signal.Ansam:
                if (vbdAllowed) {
                    UpdateMediaStates(V1501MediaState.VoiceBandData, RemoteMediaState);
                    GenericMacro(V1501Signal.Ansam, 0);
                } else {
                    GenericMacro(V1501Signal.GenerateRfc4733Ansam, 0);
                    SendSpeSignal(V1501Signal.ConcealModem);
                }
                break;
            case V1501Signal.Rfc4733Ans:
                SendSpeSignal(V1501Signal.GenerateAns);
                SendSpeSignal(V1501Signal.ConcealModem);
                break;
            case V1501Signal.Rfc4733Ansam:
                SendSpeSignal(V1501Signal.GenerateAnsam);
                SendSpeSignal(V1501Signal.ConcealModem);
                break;
            case V1501Signal.Rfc4733AnsPhaseReversal:
                SendSpeSignal(V1501Signal.GenerateAnsPhaseReversal);
                SendSpeSignal(V1501Signal.ConcealModem);
                break;
            case V1501Signal.Rfc4733AnsamPhaseReversal:
                SendSpeSignal(V1501Signal.GenerateAnsamPhaseReversal);
                SendSpeSignal(V1501Signal.ConcealModem);
                break;
            case V1501Signal.Unknown:
            case V1501Signal.CallDiscriminationTimeout:
                if (vbdAllowed) {
                    UpdateMediaStates(V1501MediaState.VoiceBandData, RemoteMediaState);
                    GenericMacro(signal, 0);
                }
                break;
            case V1501Signal.Vbd:
                UpdateMediaStates(V1501MediaState.VoiceBandData, vbdAllowed ? V1501MediaState.VoiceBandData : RemoteMediaState);
                GenericMacro(signal, 0);
                break;
            case V1501Signal.ModemRelay:
                Logging.Flow($"Modem relay signal {SignalToString(signal)}");
                break;
            case V1501Signal.Cm:
                Logging.Flow($"SPE signal {SignalToString(signal)}");
                UpdateMediaStates(vbdAllowed ? V1501MediaState.VoiceBandData : V1501MediaState.ModemRelay, V1501MediaState.ModemRelay);
                GenericMacro(V1501Signal.GenerateModemRelayState, 2);
                break;
            case V1501Signal.AnsPhaseReversal:
            case V1501Signal.AnsamPhaseReversal:
                break;
            default:
                Logging.Flow($"Unexpected signal {SignalToString(signal)}");
                break;
        }
        return 0;
    }

    private int Figure32(V1501Signal signal) {
        if (signal == V1501Signal.Audio) GenericMacro(signal, 0);
        else Logging.Flow($"Unexpected signal {SignalToString(signal)}");
        return 0;
    }

    private int Figure33(V1501Signal signal) => Figure32(signal);

    private int Figure34(V1501Signal signal) {
        switch (signal) {
            case V1501Signal.Audio: GenericMacro(signal, 0); break;
            case V1501Signal.ModemRelay: StopTimer(); break;
            case V1501Signal.Vbd: GenericMacro(signal, 0); break;
            default: Logging.Flow($"Unexpected signal {SignalToString(signal)}"); break;
        }
        return 0;
    }

    private int Figure35(V1501Signal signal) {
        switch (signal) {
            case V1501Signal.Jm: break;
            case V1501Signal.Vbd: UpdateMediaStates(LocalMediaState, V1501MediaState.VoiceBandData); break;
            default: Logging.Flow($"Unexpected signal {SignalToString(signal)}"); break;
        }
        return 0;
    }

    private int Figure36(V1501Signal signal) {
        switch (signal) {
            case V1501Signal.Audio:
                UpdateMediaStates(V1501MediaState.InitialAudio, V1501MediaState.VoiceBandData);
                break;
            case V1501Signal.ModemRelay:
                StopTimer();
                break;
            case V1501Signal.Vbd:
                StopTimer();
                UpdateMediaStates(V1501MediaState.InitialAudio, V1501MediaState.VoiceBandData);
                GenericMacro(signal, 0);
                break;
            default:
                Logging.Flow($"Unexpected signal {SignalToString(signal)}");
                break;
        }
        return 0;
    }

    private int Figure37(V1501Signal signal) {
        switch (signal) {
            case V1501Signal.Audio: UpdateMediaStates(V1501MediaState.InitialAudio, V1501MediaState.InitialAudio); break;
            case V1501Signal.ModemRelay:
            case V1501Signal.Vbd: StopTimer(); break;
            default: Logging.Flow($"Unexpected signal {SignalToString(signal)}"); break;
        }
        return 0;
    }

    private int Figure38(V1501Signal signal) {
        switch (signal) {
            case V1501Signal.Audio: UpdateMediaStates(V1501MediaState.InitialAudio, V1501MediaState.InitialAudio); break;
            case V1501Signal.Vbd: StopTimer(); break;
            default: Logging.Flow($"Unexpected signal {SignalToString(signal)}"); break;
        }
        return 0;
    }

    private int Figure39(V1501Signal signal) {
        switch (signal) {
            case V1501Signal.ModemRelay:
            case V1501Signal.Cm:
                break;
            case V1501Signal.Rfc4733Ans: SendSpeSignal(V1501Signal.GenerateAns); break;
            case V1501Signal.Rfc4733Ansam: SendSpeSignal(V1501Signal.GenerateAnsam); break;
            case V1501Signal.Rfc4733AnsPhaseReversal: SendSpeSignal(V1501Signal.GenerateAns); break;
            case V1501Signal.Rfc4733AnsamPhaseReversal: SendSpeSignal(V1501Signal.GenerateAnsam); break;
            case V1501Signal.Ans: if (Rfc4733Preferred) GenericMacro(V1501Signal.GenerateRfc4733Ans, 0); break;
            case V1501Signal.Ansam: if (Rfc4733Preferred) GenericMacro(V1501Signal.GenerateRfc4733Ansam, 0); break;
            case V1501Signal.AnsPhaseReversal: if (Rfc4733Preferred) GenericMacro(V1501Signal.GenerateRfc4733AnsPhaseReversal, 0); break;
            case V1501Signal.AnsamPhaseReversal: if (Rfc4733Preferred) GenericMacro(V1501Signal.GenerateRfc4733AnsamPhaseReversal, 0); break;
            default: Logging.Flow($"Unexpected signal {SignalToString(signal)}"); break;
        }
        return 0;
    }

    private int GenericMacro(V1501Signal signal, int reasonCode) {
        Logging.Flow($"IP signal {MediaStateToString(LocalMediaState)}({SignalToString(signal)}, {reasonCode})");
        IpSignalRequested?.Invoke(this, signal, reasonCode);
        if (LocalMediaState == RemoteMediaState) {
            CallDiscriminationTimer = 0;
            UpdateCallDiscriminationTimer(0);
        } else if (CallDiscriminationTimer == 0) {
            ulong now = UpdateCallDiscriminationTimer(ulong.MaxValue);
            CallDiscriminationTimer = unchecked(now + (ulong)CallDiscriminationTimeout);
            UpdateCallDiscriminationTimer(CallDiscriminationTimer);
        }
        return 0;
    }

    private int SendSpeSignal(V1501Signal signal) {
        Logging.Flow($"Signal to SPE {SignalToString(signal)}");
        return _speSignalHandler?.Invoke(_speSignalUserData, (int)signal) ?? 0;
    }

    private void StopTimer() {
        Logging.Flow("Stop timer");
        CallDiscriminationTimer = 0;
        SelectAndArmTimer();
    }

    private void UpdateMediaStates(V1501MediaState local, V1501MediaState remote) {
        if (local == LocalMediaState && remote == RemoteMediaState)
            return;
        LocalMediaState = local;
        RemoteMediaState = remote;
        ReportStatus(V1501StatusReason.MediaStateChanged);
    }

    private int ProcessNull(ReadOnlySpan<byte> message) => message.Length == 1 ? 0 : -1;

    private int ProcessInit(ReadOnlySpan<byte> message) {
        if (message.Length != 3) {
            Logging.Warning($"Invalid INIT message length {message.Length}");
            return -1;
        }
        V1501Parameters far = Far.Parameters;
        far.PreferredNonErrorControlledReceiveChannel = (message[1] & 0x80) != 0;
        far.PreferredErrorControlledReceiveChannel = (message[1] & 0x40) != 0;
        far.XidProfileExchangeSupported = (message[1] & 0x20) != 0;
        far.AsymmetricDataTypesSupported = (message[1] & 0x10) != 0;
        far.IRawBitSupported = (message[1] & 0x08) != 0;
        far.IFrameSupported = (message[1] & 0x04) != 0;
        far.ICharStaticSupported = (message[1] & 0x02) != 0;
        far.ICharDynamicSupported = (message[1] & 0x01) != 0;
        far.IOctetCharacterSequenceSupported = (message[2] & 0x80) != 0;
        far.ICharStaticCharacterSequenceSupported = (message[2] & 0x40) != 0;
        far.ICharDynamicCharacterSequenceSupported = (message[2] & 0x20) != 0;

        V1501Parameters near = Near.Parameters;
        near.IRawBitAvailable = near.IRawBitSupported && far.IRawBitSupported;
        near.IFrameAvailable = near.IFrameSupported && far.IFrameSupported;
        near.IOctetWithDlciAvailable = near.DlciSupported;
        near.IOctetWithoutDlciAvailable = !near.DlciSupported;
        near.ICharStaticAvailable = near.ICharStaticSupported && far.ICharStaticSupported;
        near.ICharDynamicAvailable = near.ICharDynamicSupported && far.ICharDynamicSupported;
        near.IOctetCharacterSequenceAvailable = near.IOctetCharacterSequenceSupported && far.IOctetCharacterSequenceSupported;
        near.ICharStaticCharacterSequenceAvailable = near.ICharStaticCharacterSequenceSupported && far.ICharStaticCharacterSequenceSupported;
        near.ICharDynamicCharacterSequenceAvailable = near.ICharDynamicCharacterSequenceSupported && far.ICharDynamicCharacterSequenceSupported;

        far.ConnectionState = V1501ConnectionState.Initialized;
        if (near.ConnectionState >= V1501ConnectionState.Initialized) {
            JointConnectionState = V1501ConnectionState.Initialized;
            SelectInformationMessageType();
        }
        Logging.Flow("Received INIT");
        LogInit(far);
        ReportStatus(V1501StatusReason.ConnectionStateChanged);
        return 0;
    }

    private int ProcessXidExchange(ReadOnlySpan<byte> message) {
        if (JointConnectionState < V1501ConnectionState.Initialized) {
            Logging.Warning("XID_XCHG received before INIT. Ignored.");
            return -1;
        }
        if (message.Length != 19) {
            Logging.Warning($"Invalid XID_XCHG message length {message.Length}");
            return -1;
        }
        ParseCompressionProfile(message);
        return 0;
    }

    private int ProcessJmInfo(ReadOnlySpan<byte> message) {
        if (JointConnectionState < V1501ConnectionState.Initialized) {
            Logging.Warning("JM_INFO received before INIT. Ignored.");
            return -1;
        }
        if ((message.Length & 1) != 1) {
            Logging.Warning($"Invalid JM_INFO message length {message.Length}");
            return -1;
        }
        for (int i = 1; i < message.Length; i += 2) {
            int id = (message[i] >> 4) & 0x0F;
            Far.Parameters.JmCategorySeen[id] = true;
            Far.Parameters.JmCategoryInfo[id] = unchecked((ushort)(ReadUInt16(message, i) & 0x0FFF));
        }
        for (int i = 0; i < V1501Constants.JmCategoryCount; i++)
            if (Far.Parameters.JmCategorySeen[i]) Logging.Flow($"    JM {JmCategoryToString(i)} 0x{Far.Parameters.JmCategoryInfo[i]:x}");
        return 0;
    }

    private int ProcessStartJm(ReadOnlySpan<byte> message) {
        if (JointConnectionState < V1501ConnectionState.Initialized) {
            Logging.Warning("START_JM received before INIT. Ignored.");
            return -1;
        }
        if (message.Length != 1) {
            Logging.Warning($"Invalid START_JM message length {message.Length}");
            return -1;
        }
        return 0;
    }

    private int ProcessConnect(ReadOnlySpan<byte> message) {
        if (JointConnectionState < V1501ConnectionState.Initialized) {
            Logging.Warning("CONNECT received before INIT. Ignored.");
            return -1;
        }
        if (message.Length is < 9 or > 19) {
            Logging.Warning($"Invalid CONNECT message length {message.Length}");
            return -1;
        }
        V1501Parameters far = Far.Parameters;
        far.SelectedModulation = (V1501SelectedModulation)((message[1] >> 2) & 0x3F);
        far.SelectedCompressionDirection = (V1501CompressionDirection)(message[1] & 0x03);
        far.SelectedCompression = (V1501Compression)((message[2] >> 4) & 0x0F);
        far.SelectedErrorCorrection = (V1501ErrorCorrection)(message[2] & 0x0F);
        far.TransmitDataSignallingRate = ReadUInt16(message, 3);
        far.ReceiveDataSignallingRate = ReadUInt16(message, 5);
        int available = ReadUInt16(message, 7);
        far.IOctetWithDlciAvailable = (available & 0x8000) != 0;
        far.IOctetWithoutDlciAvailable = (available & 0x4000) != 0;
        far.IRawBitAvailable = (available & 0x2000) != 0;
        far.IFrameAvailable = (available & 0x1000) != 0;
        far.ICharStaticAvailable = (available & 0x0800) != 0;
        far.ICharDynamicAvailable = (available & 0x0400) != 0;
        far.IOctetCharacterSequenceAvailable = (available & 0x0200) != 0;
        far.ICharStaticCharacterSequenceAvailable = (available & 0x0100) != 0;
        far.ICharDynamicCharacterSequenceAvailable = (available & 0x0080) != 0;

        if (message.Length >= 15 && far.SelectedCompression is V1501Compression.V42Bis or V1501Compression.V44) {
            far.CompressionTransmitDictionarySize = ReadUInt16(message, 9);
            far.CompressionReceiveDictionarySize = ReadUInt16(message, 11);
            far.CompressionTransmitStringLength = message[13];
            far.CompressionReceiveStringLength = message[14];
        } else {
            far.CompressionTransmitDictionarySize = 0;
            far.CompressionReceiveDictionarySize = 0;
            far.CompressionTransmitStringLength = 0;
            far.CompressionReceiveStringLength = 0;
        }
        if (message.Length >= 19 && far.SelectedCompression == V1501Compression.V44) {
            far.CompressionTransmitHistorySize = ReadUInt16(message, 15);
            far.CompressionReceiveHistorySize = ReadUInt16(message, 17);
        } else {
            far.CompressionTransmitHistorySize = 0;
            far.CompressionReceiveHistorySize = 0;
        }

        Logging.Flow($"    Modulation {ModulationToString(far.SelectedModulation)}");
        Logging.Flow($"    Compression direction {CompressionDirectionToString(far.SelectedCompressionDirection)}");
        Logging.Flow($"    Compression {CompressionToString(far.SelectedCompression)}");
        Logging.Flow($"    Error correction {ErrorCorrectionToString(far.SelectedErrorCorrection)}");
        Logging.Flow($"    Tx data rate {far.TransmitDataSignallingRate}");
        Logging.Flow($"    Rx data rate {far.ReceiveDataSignallingRate}");

        far.ConnectionState = V1501ConnectionState.Connected;
        if (Near.Parameters.ConnectionState >= V1501ConnectionState.Connected)
            JointConnectionState = V1501ConnectionState.Connected;
        ReportStatus(V1501StatusReason.ConnectionStateConnected);
        return 0;
    }

    private int ProcessBreak(ReadOnlySpan<byte> message) {
        if (JointConnectionState != V1501ConnectionState.Connected) {
            Logging.Warning("BREAK received before CONNECT. Ignored.");
            return -1;
        }
        if (message.Length != 3) {
            Logging.Warning($"Invalid BREAK message length {message.Length}");
            return -1;
        }
        Far.BreakSource = (V1501BreakSource)((message[1] >> 4) & 0x0F);
        Far.BreakType = (V1501BreakType)(message[1] & 0x0F);
        Far.BreakDurationUnits10Milliseconds = message[2];
        Logging.Flow($"Break source {BreakSourceToString(Far.BreakSource)}");
        Logging.Flow($"Break type {BreakTypeToString(Far.BreakType)}");
        Logging.Flow($"Break len {Far.BreakDurationUnits10Milliseconds * 10} ms");
        ReportStatus(V1501StatusReason.BreakReceived);
        return 0;
    }

    private int ProcessBreakAck(ReadOnlySpan<byte> message) {
        if (JointConnectionState != V1501ConnectionState.Connected) {
            Logging.Warning("BREAKACK received before CONNECT. Ignored.");
            return -1;
        }
        return message.Length == 1 ? 0 : -1;
    }

    private int ProcessMrEvent(ReadOnlySpan<byte> message) {
        if (JointConnectionState < V1501ConnectionState.Initialized) {
            Logging.Warning("MR-EVENT received before INIT. Ignored.");
            return -1;
        }
        if (message.Length < 3) {
            Logging.Warning($"Invalid MR_EVENT message length {message.Length}");
            return -1;
        }
        V1501MrEventId eventId = (V1501MrEventId)message[1];
        Logging.Flow($"MR-event {MrEventTypeToString(eventId)} ({(int)eventId}) received");
        switch (eventId) {
            case V1501MrEventId.Null:
                return message.Length == 3 ? 0 : -1;
            case V1501MrEventId.Retrain:
            case V1501MrEventId.RateRenegotiation:
                if (message.Length != 3) return -1;
                Far.Parameters.ConnectionState = eventId == V1501MrEventId.Retrain ? V1501ConnectionState.Retrain : V1501ConnectionState.RateRenegotiation;
                JointConnectionState = Far.Parameters.ConnectionState;
                ReportStatus(eventId == V1501MrEventId.Retrain ? V1501StatusReason.RateRetrainReceived : V1501StatusReason.RateRenegotiationReceived);
                return 0;
            case V1501MrEventId.PhysicallyUp:
                if (message.Length != 10) return -1;
                Far.Parameters.SelectedModulation = (V1501SelectedModulation)((message[3] >> 2) & 0x3F);
                Far.Parameters.TransmitSymbolRateEnabled = (message[3] & 0x02) != 0;
                Far.Parameters.ReceiveSymbolRateEnabled = (message[3] & 0x01) != 0;
                Far.Parameters.TransmitDataSignallingRate = ReadUInt16(message, 4);
                Far.Parameters.ReceiveDataSignallingRate = ReadUInt16(message, 6);
                Far.Parameters.TransmitSymbolRate = (V1501SymbolRate)message[8];
                Far.Parameters.ReceiveSymbolRate = (V1501SymbolRate)message[9];
                Far.Parameters.ConnectionState = V1501ConnectionState.PhysicallyUp;
                if (Near.Parameters.ConnectionState >= V1501ConnectionState.PhysicallyUp)
                    JointConnectionState = V1501ConnectionState.PhysicallyUp;
                ReportStatus(V1501StatusReason.ConnectionStatePhysicallyUp);
                return 0;
            default:
                Logging.Warning($"Unknown MR-event type {(int)eventId} received");
                return -1;
        }
    }

    private int ProcessCleardown(ReadOnlySpan<byte> message) {
        if (JointConnectionState < V1501ConnectionState.Initialized) {
            Logging.Warning("CLEARDOWN received before INIT. Ignored.");
            return -1;
        }
        if (message.Length != 4) {
            Logging.Warning($"Invalid CLEARDOWN message length {message.Length}");
            return -1;
        }
        Far.Parameters.CleardownReason = (V1501CleardownReason)message[1];
        Logging.Flow($"    Reason {CleardownReasonToString(Far.Parameters.CleardownReason)}");
        Far.Parameters.ConnectionState = V1501ConnectionState.Idle;
        JointConnectionState = V1501ConnectionState.Idle;
        ReportStatus(V1501StatusReason.ConnectionStateChanged);
        return 0;
    }

    private int ProcessProfileExchange(ReadOnlySpan<byte> message) {
        if (JointConnectionState < V1501ConnectionState.Initialized) {
            Logging.Warning("PROF_XCHG received before INIT. Ignored.");
            return -1;
        }
        if (message.Length != 19) {
            Logging.Warning($"Invalid PROF_XCHG message length {message.Length}");
            return -1;
        }
        V1501Parameters far = Far.Parameters;
        far.V42LapmSupported = (message[1] & 0xC0) == 0x40;
        far.V42AnnexASupported = (message[1] & 0x30) == 0x10;
        far.V44Supported = (message[1] & 0x0C) == 0x04;
        far.V42BisSupported = (message[1] & 0x03) == 0x01;
        far.Mnp5Supported = (message[2] & 0xC0) == 0x40;
        ParseCompressionParameters(message);
        return 0;
    }

    private int ProcessIRawOctet(ReadOnlySpan<byte> message) {
        if (!RequireConnected("I_RAW-OCTET")) return -1;
        if (message.Length < 2) return InvalidLength("I_RAW-OCTET", message.Length);
        int length;
        int repetitions;
        int header;
        if ((message[1] & 0x80) != 0) {
            length = message[1] & 0x7F;
            repetitions = 1;
            header = 2;
        } else {
            length = message[1];
            if (message.Length < 3) return InvalidLength("I_RAW-OCTET", message.Length);
            repetitions = message[2] + 2;
            header = 3;
        }
        if (message.Length != length + header) return InvalidLength("I_RAW-OCTET", message.Length);
        for (int i = 0; i < repetitions; i++) DeliverData(message[header..], -1);
        return 0;
    }

    private int ProcessIRawBit(ReadOnlySpan<byte> message) {
        if (!RequireConnected("I_RAW-BIT")) return -1;
        if (message.Length < 2) return InvalidLength("I_RAW-BIT", message.Length);
        int length;
        int repetitions;
        int header;
        if ((message[1] & 0x80) == 0) {
            length = (message[1] & 0x40) == 0 ? message[1] & 0x3F : (message[1] >> 3) & 0x07;
            repetitions = 1;
            header = 2;
        } else {
            if (message.Length < 3) return InvalidLength("I_RAW-BIT", message.Length);
            length = (message[1] >> 3) & 0x0F;
            repetitions = message[2] + 2;
            header = 3;
        }
        if (message.Length != length + header) return InvalidLength("I_RAW-BIT", message.Length);
        for (int i = 0; i < repetitions; i++) DeliverData(message[header..], -1);
        return 0;
    }

    private int ProcessIOctet(ReadOnlySpan<byte> message) {
        if (!RequireConnected("I_OCTET")) return -1;
        if (message.Length < 2) return InvalidLength("I_OCTET", message.Length);
        int header = 1;
        if (Far.Parameters.IOctetWithDlciAvailable) {
            if ((message[1] & 0x01) == 0) {
                if (message.Length < 3) return InvalidLength("I_OCTET", message.Length);
                header = 3;
                Far.Parameters.Dlci = ReadUInt16(message, 1);
            } else {
                header = 2;
                Far.Parameters.Dlci = message[1];
            }
        }
        if (message.Length > header) DeliverData(message[header..], -1);
        return 0;
    }

    private int ProcessICharStatic(ReadOnlySpan<byte> message) => ProcessCharacterMessage(message, "I_CHAR-STAT", false, false);
    private int ProcessICharDynamic(ReadOnlySpan<byte> message) => ProcessCharacterMessage(message, "I_CHAR-DYN", false, true);

    private int ProcessIFrame(ReadOnlySpan<byte> message) {
        if (!RequireConnected("I_FRAME")) return -1;
        if (message.Length < 2) return InvalidLength("I_FRAME", message.Length);
        if ((message[1] >> 2) != 0) Logging.Warning("I_FRAME with non-zero reserved field");
        if (message.Length > 2) DeliverData(message[2..], -1);
        return 0;
    }

    private int ProcessIOctetCharacterSequence(ReadOnlySpan<byte> message) {
        if (!RequireConnected("I_OCTET-CS")) return -1;
        if (message.Length < 3) return InvalidLength("I_OCTET-CS", message.Length);
        ushort sequence = ReadUInt16(message, 1);
        int fill = (sequence - Far.Parameters.OctetCharacterSequenceNextSequenceNumber) & 0xFFFF;
        DeliverData(message[3..], fill);
        Far.Parameters.OctetCharacterSequenceNextSequenceNumber = unchecked((ushort)(sequence + message.Length - 3));
        return 0;
    }

    private int ProcessICharStaticCharacterSequence(ReadOnlySpan<byte> message) => ProcessCharacterMessage(message, "I_CHAR-STAT-CS", true, false);
    private int ProcessICharDynamicCharacterSequence(ReadOnlySpan<byte> message) => ProcessCharacterMessage(message, "I_CHAR-DYN-CS", true, true);

    private int ProcessCharacterMessage(ReadOnlySpan<byte> message, string name, bool withCharacterSequence, bool dynamicFormat) {
        if (!RequireConnected(name)) return -1;
        int header = withCharacterSequence ? 4 : 2;
        if (message.Length < header) return InvalidLength(name, message.Length);
        if (Far.Parameters.DataFormatCode != message[1]) {
            Far.Parameters.DataFormatCode = message[1];
            ReportStatus(V1501StatusReason.DataFormatChanged);
        }
        int fill = -1;
        if (withCharacterSequence) {
            ushort sequence = ReadUInt16(message, 2);
            fill = (sequence - Far.Parameters.OctetCharacterSequenceNextSequenceNumber) & 0xFFFF;
            Far.Parameters.OctetCharacterSequenceNextSequenceNumber = unchecked((ushort)(sequence + message.Length - header));
        }
        if (message.Length > header) DeliverData(message[header..], fill);
        return 0;
    }

    private int BuildIRawOctet(Span<byte> packet, int maximumLength, ReadOnlySpan<byte> data) {
        if (data.Length > maximumLength - 2 || data.Length > 0x7F) return -1;
        packet[0] = (byte)V1501MessageId.IRawOctet;
        packet[1] = unchecked((byte)(0x80 | data.Length));
        data.CopyTo(packet[2..]);
        return data.Length + 2;
    }

    private int BuildIRawBit(Span<byte> packet, int maximumLength, ReadOnlySpan<byte> data) {
        if (!Far.Parameters.IRawBitAvailable || data.Length > maximumLength - 2 || data.Length > 7) return -1;
        packet[0] = (byte)V1501MessageId.IRawBit;
        packet[1] = unchecked((byte)(0x40 | (data.Length << 3)));
        data.CopyTo(packet[2..]);
        return data.Length + 2;
    }

    private int BuildIOctet(Span<byte> packet, int maximumLength, ReadOnlySpan<byte> data) {
        if (!Far.Parameters.IOctetWithoutDlciAvailable && !Far.Parameters.IOctetWithDlciAvailable) return -1;
        int header = 1;
        packet[0] = (byte)V1501MessageId.IOctet;
        if (Far.Parameters.IOctetWithDlciAvailable) {
            if ((Near.Parameters.Dlci & 0x01) != 0) {
                packet[1] = unchecked((byte)Near.Parameters.Dlci);
                header = 2;
            } else {
                WriteUInt16(packet, 1, Near.Parameters.Dlci);
                header = 3;
            }
        }
        if (data.Length > maximumLength - header) return -1;
        data.CopyTo(packet[header..]);
        return data.Length + header;
    }

    private int BuildICharStatic(Span<byte> packet, int maximumLength, ReadOnlySpan<byte> data) => BuildCharacterPacket(packet, maximumLength, data, V1501MessageId.ICharStatic, Far.Parameters.ICharStaticAvailable, false);
    private int BuildICharDynamic(Span<byte> packet, int maximumLength, ReadOnlySpan<byte> data) => BuildCharacterPacket(packet, maximumLength, data, V1501MessageId.ICharDynamic, Far.Parameters.ICharDynamicAvailable, false);

    private int BuildIFrame(Span<byte> packet, int maximumLength, ReadOnlySpan<byte> data) {
        if (!Far.Parameters.IFrameAvailable || data.Length > maximumLength - 2) return -1;
        packet[0] = (byte)V1501MessageId.IFrame;
        packet[1] = 0;
        data.CopyTo(packet[2..]);
        return data.Length + 2;
    }

    private int BuildIOctetCharacterSequence(Span<byte> packet, int maximumLength, ReadOnlySpan<byte> data) {
        if (!Far.Parameters.IOctetCharacterSequenceAvailable || data.Length > maximumLength - 3) return -1;
        packet[0] = (byte)V1501MessageId.IOctetCharacterSequence;
        WriteUInt16(packet, 1, Near.Parameters.OctetCharacterSequenceNextSequenceNumber);
        data.CopyTo(packet[3..]);
        Near.Parameters.OctetCharacterSequenceNextSequenceNumber = unchecked((ushort)(Near.Parameters.OctetCharacterSequenceNextSequenceNumber + data.Length));
        return data.Length + 3;
    }

    private int BuildICharStaticCharacterSequence(Span<byte> packet, int maximumLength, ReadOnlySpan<byte> data) => BuildCharacterPacket(packet, maximumLength, data, V1501MessageId.ICharStaticCharacterSequence, Far.Parameters.ICharStaticCharacterSequenceAvailable, true);
    private int BuildICharDynamicCharacterSequence(Span<byte> packet, int maximumLength, ReadOnlySpan<byte> data) => BuildCharacterPacket(packet, maximumLength, data, V1501MessageId.ICharDynamicCharacterSequence, Far.Parameters.ICharDynamicCharacterSequenceAvailable, true);

    private int BuildCharacterPacket(Span<byte> packet, int maximumLength, ReadOnlySpan<byte> data, V1501MessageId messageId, bool available, bool withCharacterSequence) {
        int header = withCharacterSequence ? 4 : 2;
        if (!available || data.Length > maximumLength - header) return -1;
        packet[0] = (byte)messageId;
        packet[1] = Near.Parameters.DataFormatCode;
        if (withCharacterSequence)
            WriteUInt16(packet, 2, Near.Parameters.OctetCharacterSequenceNextSequenceNumber);
        data.CopyTo(packet[header..]);
        if (withCharacterSequence)
            Near.Parameters.OctetCharacterSequenceNextSequenceNumber = unchecked((ushort)(Near.Parameters.OctetCharacterSequenceNextSequenceNumber + data.Length));
        return data.Length + header;
    }

    private void ParseCompressionProfile(ReadOnlySpan<byte> message) {
        V1501Parameters far = Far.Parameters;
        far.ErrorCorrectionProtocol = message[1];
        far.V42BisSupported = (message[2] & 0x80) != 0;
        far.V44Supported = (message[2] & 0x40) != 0;
        far.Mnp5Supported = (message[2] & 0x20) != 0;
        ParseCompressionParameters(message);
    }

    private void ParseCompressionParameters(ReadOnlySpan<byte> message) {
        V1501Parameters far = Far.Parameters;
        far.V42BisP0 = message[3];
        far.V42BisP1 = ReadUInt16(message, 4);
        far.V42BisP2 = message[6];
        far.V44C0 = message[7];
        far.V44P0 = message[8];
        far.V44P1Transmit = ReadUInt16(message, 9);
        far.V44P1Receive = ReadUInt16(message, 11);
        far.V44P2Transmit = message[13];
        far.V44P2Receive = message[14];
        far.V44P3Transmit = ReadUInt16(message, 15);
        far.V44P3Receive = ReadUInt16(message, 17);
        Logging.Flow($"    V.42bis {(far.V42BisSupported ? string.Empty : "not ")}supported");
        Logging.Flow($"    V.44 {(far.V44Supported ? string.Empty : "not ")}supported");
        Logging.Flow($"    MNP5 {(far.Mnp5Supported ? string.Empty : "not ")}supported");
    }

    private int SelectInformationMessageType() {
        for (int i = 0; i < Near.InformationMessagePreferences.Length && Near.InformationMessagePreferences[i] >= 0; i++) {
            V1501MessageId id = (V1501MessageId)Near.InformationMessagePreferences[i];
            bool available = id switch {
                V1501MessageId.IRawOctet => true,
                V1501MessageId.IRawBit => Near.Parameters.IRawBitAvailable,
                V1501MessageId.IOctet => true,
                V1501MessageId.ICharStatic => Near.Parameters.ICharStaticAvailable,
                V1501MessageId.ICharDynamic => Near.Parameters.ICharDynamicAvailable,
                V1501MessageId.IFrame => Near.Parameters.IFrameAvailable,
                V1501MessageId.IOctetCharacterSequence => Near.Parameters.IOctetCharacterSequenceAvailable,
                V1501MessageId.ICharStaticCharacterSequence => Near.Parameters.ICharStaticCharacterSequenceAvailable,
                V1501MessageId.ICharDynamicCharacterSequence => Near.Parameters.ICharDynamicCharacterSequenceAvailable,
                _ => false
            };
            if (available) {
                Near.InformationStreamMessageId = id;
                return 0;
            }
        }
        Near.InformationStreamMessageId = (V1501MessageId)(-1);
        return -1;
    }

    private void LogInit(V1501Parameters parameters) {
        Logging.Flow($"    Preferred non-error controlled Rx channel: {(parameters.PreferredNonErrorControlledReceiveChannel ? "RSC" : "USC")}");
        Logging.Flow($"    Preferred error controlled Rx channel: {(parameters.PreferredErrorControlledReceiveChannel ? "USC" : "RSC")}");
        Logging.Flow($"    XID profile exchange {(parameters.XidProfileExchangeSupported ? string.Empty : "not ")}supported");
        Logging.Flow($"    Asymmetric data types {(parameters.AsymmetricDataTypesSupported ? string.Empty : "not ")}supported");
        Logging.Flow("    I_RAW-OCTET supported");
        Logging.Flow($"    I_RAW-BIT {(parameters.IRawBitSupported ? string.Empty : "not ")}supported");
        Logging.Flow($"    I_FRAME {(parameters.IFrameSupported ? string.Empty : "not ")}supported");
        Logging.Flow($"    I_CHAR-STAT {(parameters.ICharStaticSupported ? string.Empty : "not ")}supported");
        Logging.Flow($"    I_CHAR-DYN {(parameters.ICharDynamicSupported ? string.Empty : "not ")}supported");
        Logging.Flow($"    I_OCTET-CS {(parameters.IOctetCharacterSequenceSupported ? string.Empty : "not ")}supported");
    }

    private void ReportStatus(V1501StatusReason reason) {
        V1501Parameters far = Far.Parameters;
        V1501Status report = new() {
            Reason = reason,
            LocalMediaState = LocalMediaState,
            RemoteMediaState = RemoteMediaState,
            ConnectionState = far.ConnectionState,
            CleardownReason = far.CleardownReason,
            Bits = 5 + ((far.DataFormatCode >> 5) & 0x03),
            Parity = (V1501Parity)((far.DataFormatCode >> 2) & 0x07),
            StopBits = 1 + (far.DataFormatCode & 0x03),
            BreakSource = Far.BreakSource,
            BreakType = Far.BreakType,
            BreakDurationMilliseconds = Far.BreakDurationUnits10Milliseconds * 10,
            LocalBusy = Near.Parameters.Busy,
            FarBusy = far.Busy,
            SelectedModulation = far.SelectedModulation,
            TransmitDataSignallingRate = far.TransmitDataSignallingRate,
            ReceiveDataSignallingRate = far.ReceiveDataSignallingRate,
            TransmitSymbolRateEnabled = far.TransmitSymbolRateEnabled,
            TransmitSymbolRate = far.TransmitSymbolRate,
            ReceiveSymbolRateEnabled = far.ReceiveSymbolRateEnabled,
            ReceiveSymbolRate = far.ReceiveSymbolRate,
            SelectedCompressionDirection = far.SelectedCompressionDirection,
            SelectedCompression = far.SelectedCompression,
            SelectedErrorCorrection = far.SelectedErrorCorrection,
            CompressionTransmitDictionarySize = far.CompressionTransmitDictionarySize,
            CompressionReceiveDictionarySize = far.CompressionReceiveDictionarySize,
            CompressionTransmitStringLength = far.CompressionTransmitStringLength,
            CompressionReceiveStringLength = far.CompressionReceiveStringLength,
            CompressionTransmitHistorySize = far.CompressionTransmitHistorySize,
            CompressionReceiveHistorySize = far.CompressionReceiveHistorySize,
            IRawOctetAvailable = true,
            IRawBitAvailable = far.IRawBitAvailable,
            IFrameAvailable = far.IFrameAvailable,
            IOctetWithDlciAvailable = far.IOctetWithDlciAvailable,
            IOctetWithoutDlciAvailable = far.IOctetWithoutDlciAvailable,
            ICharStaticAvailable = far.ICharStaticAvailable,
            ICharDynamicAvailable = far.ICharDynamicAvailable,
            IOctetCharacterSequenceAvailable = far.IOctetCharacterSequenceAvailable,
            ICharStaticCharacterSequenceAvailable = far.ICharStaticCharacterSequenceAvailable,
            ICharDynamicCharacterSequenceAvailable = far.ICharDynamicCharacterSequenceAvailable
        };
        _receiveStatusReportHandler?.Invoke(_receiveStatusReportUserData, report);
    }

    private void SetJointCallDiscriminationSelection() {
        V1501CallDiscriminationSelection near = Near.Parameters.CallDiscriminationSelection;
        V1501CallDiscriminationSelection far = Far.Parameters.CallDiscriminationSelection;
        JointCallDiscriminationSelection = near == V1501CallDiscriminationSelection.Indeterminate || far == V1501CallDiscriminationSelection.Indeterminate
            ? V1501CallDiscriminationSelection.Indeterminate
            : near == V1501CallDiscriminationSelection.AudioRfc4733 || far == V1501CallDiscriminationSelection.AudioRfc4733
                ? V1501CallDiscriminationSelection.AudioRfc4733
                : near == V1501CallDiscriminationSelection.VbdPreferred || far == V1501CallDiscriminationSelection.VbdPreferred
                    ? V1501CallDiscriminationSelection.VbdPreferred
                    : V1501CallDiscriminationSelection.Mixed;
    }

    private ulong SelectTimer() {
        ulong shortest = ulong.MaxValue;
        if (SprtTimer != 0 && SprtTimer < shortest) shortest = SprtTimer;
        if (SseTimer != 0 && SseTimer < shortest) shortest = SseTimer;
        if (CallDiscriminationTimer != 0 && CallDiscriminationTimer < shortest) shortest = CallDiscriminationTimer;
        if (shortest == ulong.MaxValue) shortest = 0;
        LatestTimer = shortest;
        Logging.Flow($"Update timer to {shortest}");
        return shortest;
    }

    private void SelectAndArmTimer() {
        ulong selected = SelectTimer();
        _timerHandler?.Invoke(_timerUserData, selected);
    }

    private ulong UpdateCallDiscriminationTimer(ulong timeout) {
        if (timeout != ulong.MaxValue) {
            CallDiscriminationTimer = timeout;
            timeout = SelectTimer();
        }
        return _timerHandler?.Invoke(_timerUserData, timeout) ?? 0;
    }

    private ulong UpdateSprtTimerCallback(object? userData, ulong timeout) {
        if (timeout != ulong.MaxValue) {
            SprtTimer = timeout;
            timeout = SelectTimer();
        }
        return _timerHandler?.Invoke(_timerUserData, timeout) ?? 0;
    }

    private void SprtStatusCallback(object? userData, int status) => Logging.Flow($"SPRT status event {status}");

    private int TransmitControl(V1501MessageId id, ReadOnlySpan<byte> packet, string log) {
        ThrowIfDisposed();
        int result = Sprt.TransmitMessage((int)SprtTransmissionChannel.ExpeditedReliableSequenced, packet);
        Logging.Flow(log);
        return result;
    }

    private void DeliverData(ReadOnlySpan<byte> data, int fill) => _receiveDataHandler?.Invoke(_receiveDataUserData, data, fill);

    private bool RequireConnected(string name) {
        if (JointConnectionState == V1501ConnectionState.Connected) return true;
        Logging.Warning($"{name} received before CONNECT. Ignored.");
        return false;
    }

    private int InvalidLength(string name, int length) {
        Logging.Warning($"Invalid {name} message length {length}");
        return -1;
    }

    private void ResetState() {
        Near.Reset();
        Far.Reset();
        Logging.Protocol = "V.150.1";
        JointCallDiscriminationSelection = V1501CallDiscriminationSelection.Indeterminate;
        CallDiscriminationSelection = V1501CallDiscriminationSelection.Indeterminate;
        Sse.ClearRuntimeState();
        Rfc4733Preferred = false;
        CallDiscriminationTimeout = V1501Constants.CallDiscriminationDefaultTimeout;
        LocalMediaState = V1501MediaState.InitialAudio;
        RemoteMediaState = V1501MediaState.InitialAudio;
        RemoteAcknowledgement = V1501MediaState.Indeterminate;
        JointConnectionState = V1501ConnectionState.Idle;
        LatestTimer = 0;
        CallDiscriminationTimer = 0;
        SseTimer = 0;
        SprtTimer = 0;

        Near.MaximumPayloadBytes[(int)SprtTransmissionChannel.UnreliableUnsequenced] = SprtConstants.DefaultTc0PayloadBytes;
        Near.MaximumPayloadBytes[(int)SprtTransmissionChannel.ReliableSequenced] = SprtConstants.DefaultTc1PayloadBytes;
        Near.MaximumPayloadBytes[(int)SprtTransmissionChannel.ExpeditedReliableSequenced] = SprtConstants.DefaultTc2PayloadBytes;
        Near.MaximumPayloadBytes[(int)SprtTransmissionChannel.UnreliableSequenced] = SprtConstants.DefaultTc3PayloadBytes;

        V1501Parameters near = Near.Parameters;
        near.V42BisP0 = 3;
        near.V42BisP1 = 512;
        near.V42BisP2 = 6;
        near.JmCategorySeen[(int)V1501JmCategoryId.CallFunction1] = true;
        near.JmCategoryInfo[(int)V1501JmCategoryId.CallFunction1] = V1501JmCallFunction.VSeries;
        near.JmCategorySeen[(int)V1501JmCategoryId.ModulationModes] = true;
        near.JmCategoryInfo[(int)V1501JmCategoryId.ModulationModes] = (ushort)(V1501JmModulationMode.V34 | V1501JmModulationMode.V32V32Bis | V1501JmModulationMode.V22V22Bis | V1501JmModulationMode.V21);
        near.JmCategorySeen[(int)V1501JmCategoryId.Protocols] = true;
        near.JmCategoryInfo[(int)V1501JmCategoryId.Protocols] = V1501JmProtocol.V42Lapm;
        near.JmCategorySeen[(int)V1501JmCategoryId.PstnAccess] = true;
        near.SelectedModulation = V1501SelectedModulation.Null;
        near.SelectedCompressionDirection = V1501CompressionDirection.NeitherWay;
        near.SelectedCompression = V1501Compression.None;
        near.SelectedErrorCorrection = V1501ErrorCorrection.None;
        near.CompressionTransmitDictionarySize = 512;
        near.CompressionReceiveDictionarySize = 512;
        near.CompressionTransmitStringLength = 6;
        near.CompressionReceiveStringLength = 6;
        near.ErrorCorrectionProtocol = (int)V1501ErrorCorrection.V42Lapm;
        near.V42LapmSupported = true;
        near.V42AnnexASupported = false;
        near.V42BisSupported = true;
        near.V44Supported = false;
        near.Mnp5Supported = false;
        near.PreferredNonErrorControlledReceiveChannel = false;
        near.PreferredErrorControlledReceiveChannel = true;
        near.XidProfileExchangeSupported = false;
        near.AsymmetricDataTypesSupported = false;
        near.IRawBitSupported = false;
        near.IFrameSupported = false;
        near.ICharStaticSupported = false;
        near.ICharDynamicSupported = false;
        near.IOctetCharacterSequenceSupported = true;
        near.ICharStaticCharacterSequenceSupported = false;
        near.ICharDynamicCharacterSequenceSupported = false;
        near.DataFormatCode = unchecked((byte)(((int)V1501DataBits.Bits7 << 5) | ((int)V1501Parity.Even << 2) | (int)V1501StopBits.One));
        Far.Parameters.DataFormatCode = 0xFF;
        Near.InformationMessagePreferences[0] = (int)V1501MessageId.IRawOctet;
        Near.InformationMessagePreferences[1] = -1;
    }

    private static bool IsInformationMessage(V1501MessageId id) => id is
        V1501MessageId.IRawOctet or
        V1501MessageId.IRawBit or
        V1501MessageId.IOctet or
        V1501MessageId.ICharStatic or
        V1501MessageId.ICharDynamic or
        V1501MessageId.IFrame or
        V1501MessageId.IOctetCharacterSequence or
        V1501MessageId.ICharStaticCharacterSequence or
        V1501MessageId.ICharDynamicCharacterSequence;

    private static ushort ReadUInt16(ReadOnlySpan<byte> source, int offset) => unchecked((ushort)((source[offset] << 8) | source[offset + 1]));
    private static void WriteUInt16(Span<byte> destination, int offset, ushort value) {
        destination[offset] = unchecked((byte)(value >> 8));
        destination[offset + 1] = unchecked((byte)value);
    }
    private static bool FitsUInt16(int value) => (uint)value <= ushort.MaxValue;
    private static bool FitsByte(int value) => (uint)value <= byte.MaxValue;
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public static string MessageIdToString(int id) => ((V1501MessageId)id) switch {
        V1501MessageId.Null => "NULL",
        V1501MessageId.Init => "INIT",
        V1501MessageId.XidExchange => "XID xchg",
        V1501MessageId.JmInfo => "JM info",
        V1501MessageId.StartJm => "Start JM",
        V1501MessageId.Connect => "Connect",
        V1501MessageId.Break => "Break",
        V1501MessageId.BreakAck => "Break ack",
        V1501MessageId.MrEvent => "MR event",
        V1501MessageId.Cleardown => "Cleardown",
        V1501MessageId.ProfileExchange => "Prof xchg",
        V1501MessageId.IRawOctet => "I raw octet",
        V1501MessageId.IRawBit => "I raw bit",
        V1501MessageId.IOctet => "I octet",
        V1501MessageId.ICharStatic => "I char stat",
        V1501MessageId.ICharDynamic => "I char dyn",
        V1501MessageId.IFrame => "I frame",
        V1501MessageId.IOctetCharacterSequence => "I octet cs",
        V1501MessageId.ICharStaticCharacterSequence => "I char stat cs",
        V1501MessageId.ICharDynamicCharacterSequence => "I char dyn cs",
        _ => "unknown"
    };

    public static string DataBitsToString(int code) => ((V1501DataBits)code) switch {
        V1501DataBits.Bits5 => "5 bits",
        V1501DataBits.Bits6 => "6 bits",
        V1501DataBits.Bits7 => "7 bits",
        V1501DataBits.Bits8 => "8 bits",
        _ => "unknown"
    };

    public static string ParityToString(int code) => ((V1501Parity)code) switch {
        V1501Parity.Unknown => "unknown",
        V1501Parity.None => "none",
        V1501Parity.Even => "even",
        V1501Parity.Odd => "odd",
        V1501Parity.Space => "space",
        V1501Parity.Mark => "mark",
        _ => "unknown"
    };

    public static string StopBitsToString(int code) => ((V1501StopBits)code) switch {
        V1501StopBits.One => "1 bit",
        V1501StopBits.Two => "2 bits",
        _ => "unknown"
    };

    public static string MrEventTypeToString(V1501MrEventId type) => type switch {
        V1501MrEventId.Null => "NULL",
        V1501MrEventId.RateRenegotiation => "Renegotiation",
        V1501MrEventId.Retrain => "Retrain",
        V1501MrEventId.PhysicallyUp => "Physically up",
        _ => "unknown"
    };

    public static string CleardownReasonToString(V1501CleardownReason reason) => reason switch {
        V1501CleardownReason.Unknown => "Unknown",
        V1501CleardownReason.PhysicalLayerRelease => "Physical layer release",
        V1501CleardownReason.LinkLayerDisconnect => "Link layer disconnect",
        V1501CleardownReason.DataCompressionDisconnect => "Data compression disconnect",
        V1501CleardownReason.Abort => "Abort",
        V1501CleardownReason.OnHook => "On hook",
        V1501CleardownReason.NetworkLayerTermination => "Network layer termination",
        V1501CleardownReason.Administrative => "Administrative",
        _ => "unknown"
    };

    public static string SymbolRateToString(V1501SymbolRate rate) => rate switch {
        V1501SymbolRate.Null => "NULL",
        V1501SymbolRate.Baud600 => "600 baud",
        V1501SymbolRate.Baud1200 => "1200 baud",
        V1501SymbolRate.Baud1600 => "1600 baud",
        V1501SymbolRate.Baud2400 => "2400 baud",
        V1501SymbolRate.Baud2743 => "2743 baud",
        V1501SymbolRate.Baud3000 => "3000 baud",
        V1501SymbolRate.Baud3200 => "3200 baud",
        V1501SymbolRate.Baud3429 => "3429 baud",
        V1501SymbolRate.Baud8000 => "8000 baud",
        _ => "unknown"
    };

    public static string ModulationToString(V1501SelectedModulation modulation) => modulation switch {
        V1501SelectedModulation.Null => "NULL",
        V1501SelectedModulation.V92 => "V.92",
        V1501SelectedModulation.V91 => "V.91",
        V1501SelectedModulation.V90 => "V.90",
        V1501SelectedModulation.V34 => "V.34",
        V1501SelectedModulation.V32Bis => "V.32bis",
        V1501SelectedModulation.V32 => "V.32",
        V1501SelectedModulation.V22Bis => "V.22bis",
        V1501SelectedModulation.V22 => "V.22",
        V1501SelectedModulation.V17 => "V.17",
        V1501SelectedModulation.V29 => "V.29",
        V1501SelectedModulation.V27Ter => "V.27ter",
        V1501SelectedModulation.V26Ter => "V.26ter",
        V1501SelectedModulation.V26Bis => "V.26bis",
        V1501SelectedModulation.V23 => "V.23",
        V1501SelectedModulation.V21 => "V.21",
        V1501SelectedModulation.Bell212 => "Bell 212",
        V1501SelectedModulation.Bell103 => "Bell 103",
        _ => "unknown"
    };

    public static string CompressionToString(V1501Compression compression) => compression switch {
        V1501Compression.None => "None",
        V1501Compression.V42Bis => "V.42bis",
        V1501Compression.V44 => "V.44",
        V1501Compression.Mnp5 => "MNP5",
        _ => "unknown"
    };

    public static string CompressionDirectionToString(V1501CompressionDirection direction) => direction switch {
        V1501CompressionDirection.NeitherWay => "Neither way",
        V1501CompressionDirection.TransmitOnly => "Tx only",
        V1501CompressionDirection.ReceiveOnly => "Rx only",
        V1501CompressionDirection.Bidirectional => "Bidirectional",
        _ => "unknown"
    };

    public static string ErrorCorrectionToString(V1501ErrorCorrection correction) => correction switch {
        V1501ErrorCorrection.None => "None",
        V1501ErrorCorrection.V42Lapm => "V.42 LAPM",
        V1501ErrorCorrection.V42AnnexA => "V.42 annex A",
        _ => "unknown"
    };

    public static string BreakSourceToString(V1501BreakSource source) => source switch {
        V1501BreakSource.V42Lapm => "V.42 LAPM",
        V1501BreakSource.V42AnnexA => "V.42 annex A",
        V1501BreakSource.V14 => "V.14",
        _ => "unknown"
    };

    public static string BreakTypeToString(V1501BreakType type) => type switch {
        V1501BreakType.NotApplicable => "Non applicable",
        V1501BreakType.DestructiveExpedited => "Destructive, expedited",
        V1501BreakType.NonDestructiveExpedited => "Non-destructive, expedited",
        V1501BreakType.NonDestructiveNonExpedited => "Non-destructive, non-expedited",
        _ => "unknown"
    };

    public static string ConnectionStateToString(V1501ConnectionState state) => state switch {
        V1501ConnectionState.Idle => "Idle",
        V1501ConnectionState.Initialized => "Inited",
        V1501ConnectionState.Retrain => "Retrain",
        V1501ConnectionState.RateRenegotiation => "Rate renegotiation",
        V1501ConnectionState.PhysicallyUp => "Physically up",
        V1501ConnectionState.Connected => "Connected",
        _ => "unknown"
    };

    public static string StatusReasonToString(V1501StatusReason reason) => reason switch {
        V1501StatusReason.Null => "NULL",
        V1501StatusReason.MediaStateChanged => "media state changed",
        V1501StatusReason.ConnectionStateChanged => "connection state changed",
        V1501StatusReason.DataFormatChanged => "format changed",
        V1501StatusReason.BreakReceived => "break received",
        V1501StatusReason.RateRetrainReceived => "retrain request received",
        V1501StatusReason.RateRenegotiationReceived => "rate renegotiation received",
        V1501StatusReason.BusyChanged => "busy changed",
        V1501StatusReason.ConnectionStatePhysicallyUp => "physically up",
        V1501StatusReason.ConnectionStateConnected => "connected",
        _ => "unknown"
    };

    public static string JmCategoryToString(int category) => category switch {
        (int)V1501JmCategoryId.Protocols => "protocols",
        (int)V1501JmCategoryId.CallFunction1 => "call function 1",
        (int)V1501JmCategoryId.ModulationModes => "modulation modes",
        (int)V1501JmCategoryId.PstnAccess => "PSTN access",
        (int)V1501JmCategoryId.PcmModemAvailability => "PCM modem availability",
        (int)V1501JmCategoryId.Extension => "extension",
        _ => "unknown"
    };

    public static string JmInfoModulationToString(int modulation) => modulation switch {
        (int)V1501JmModulationMode.V34 => "V.34",
        (int)V1501JmModulationMode.V34HalfDuplex => "V.34 half-duplex",
        (int)V1501JmModulationMode.V32V32Bis => "V.32bis/V.32",
        (int)V1501JmModulationMode.V22V22Bis => "V.22bis/V.22",
        (int)V1501JmModulationMode.V17 => "V.17",
        (int)V1501JmModulationMode.V29 => "V.29",
        (int)V1501JmModulationMode.V27Ter => "V.27ter",
        (int)V1501JmModulationMode.V26Ter => "V.26ter",
        (int)V1501JmModulationMode.V26Bis => "V.26bis",
        (int)V1501JmModulationMode.V23 => "V.23",
        (int)V1501JmModulationMode.V23HalfDuplex => "V.23 half-duplex",
        (int)V1501JmModulationMode.V21 => "V.21",
        _ => "unknown"
    };

    public static string SignalToString(V1501Signal signal) => signal switch {
        V1501Signal.Tone2100Hz => "2100Hz detected",
        V1501Signal.Tone2225Hz => "2225Hz detected",
        V1501Signal.Ans => "V.25 ANS detected",
        V1501Signal.AnsPhaseReversal => "V.25 ANS reversal detected",
        V1501Signal.Ansam => "V.8 ANSam detected",
        V1501Signal.AnsamPhaseReversal => "V.8 ANSam reversal detected",
        V1501Signal.Ci => "V.8 CI detected",
        V1501Signal.Cm => "V.8 CM detected",
        V1501Signal.Jm => "V.8 JM detected",
        V1501Signal.V21Low => "V.21 low channel detected",
        V1501Signal.V21High => "V.21 high channel detected",
        V1501Signal.V23Low => "V.23 low channel detected",
        V1501Signal.V23High => "V.23 high channel detected",
        V1501Signal.Sb1 => "V.22bis scrambled ones detected",
        V1501Signal.Usb1 => "V.22bis unscrambled ones detected",
        V1501Signal.S1 => "V.22bis S1 detected",
        V1501Signal.Aa => "V.32/V.32bis AA detected",
        V1501Signal.Ac => "V.32/V.32bis AC detected",
        V1501Signal.CallDiscriminationTimeout => "Call discrimination time-out",
        V1501Signal.Unknown => "unrecognised signal detected",
        V1501Signal.Silence => "silence detected",
        V1501Signal.Abort => "SPE has initiated an abort request",
        V1501Signal.GenerateAns => "Generate V.25 ANS",
        V1501Signal.GenerateAnsPhaseReversal => "Generate V.25 ANS reversal",
        V1501Signal.GenerateAnsam => "Generate V.8 ANSam",
        V1501Signal.GenerateAnsamPhaseReversal => "Generate V.8 ANSam reversal",
        V1501Signal.Generate2225Hz => "Generate 2225Hz",
        V1501Signal.ConcealModem => "Block modem signal",
        V1501Signal.Block2100HzTone => "Block 2100Hz",
        V1501Signal.EnableAutomode => "Enable automode",
        V1501Signal.GenerateAudioState => "Send audio state",
        V1501Signal.GenerateFaxRelayState => "Send fax relay state",
        V1501Signal.GenerateIndeterminateState => "Send indeterminate state",
        V1501Signal.GenerateModemRelayState => "Send modem relay state",
        V1501Signal.GenerateTextRelayState => "Send text relay state",
        V1501Signal.GenerateVbdState => "Send VBD state",
        V1501Signal.GenerateRfc4733Ans => "Send RFC4733 ANS",
        V1501Signal.GenerateRfc4733AnsPhaseReversal => "Send RFC4733 ANS reversal",
        V1501Signal.GenerateRfc4733Ansam => "Send RFC4733 ANSam",
        V1501Signal.GenerateRfc4733AnsamPhaseReversal => "Send RFC4733 ANSam reversal",
        V1501Signal.GenerateRfc4733Tone => "Send RFC4733 tone",
        V1501Signal.Audio => "Audio state detected",
        V1501Signal.FaxRelay => "Facsimile relay state detected",
        V1501Signal.Indeterminate => "Indeterminate state detected",
        V1501Signal.ModemRelay => "Modem relay state detected",
        V1501Signal.TextRelay => "Text relay state detected",
        V1501Signal.Vbd => "VBD state detected",
        V1501Signal.Rfc4733Ans => "RFC4733 ANS event detected",
        V1501Signal.Rfc4733AnsPhaseReversal => "RFC4733 ANS reversal detected",
        V1501Signal.Rfc4733Ansam => "RFC4733 ANSam detected",
        V1501Signal.Rfc4733AnsamPhaseReversal => "RFC4733 ANSam reversal detected",
        V1501Signal.Rfc4733Tone => "RFC4733 tone detected",
        V1501Signal.AudioState => "Audio",
        V1501Signal.FaxRelayState => "Fax relay",
        V1501Signal.IndeterminateState => "Indeterminate",
        V1501Signal.ModemRelayState => "Modem relay",
        V1501Signal.TextRelayState => "Text relay",
        V1501Signal.VbdState => "VBD",
        V1501Signal.CallDiscriminationTimerExpired => "Call discrimination timer expired",
        _ => "unknown"
    };

    public static string MediaStateToString(V1501MediaState state) => state switch {
        V1501MediaState.InitialAudio => "Initial Audio",
        V1501MediaState.VoiceBandData => "Voice Band Data (VBD)",
        V1501MediaState.ModemRelay => "Modem Relay",
        V1501MediaState.FaxRelay => "Fax Relay",
        V1501MediaState.TextRelay => "Text Relay",
        V1501MediaState.TextProbe => "Text Probe",
        V1501MediaState.Indeterminate => "Indeterminate",
        _ => "unknown"
    };
}

public static class V1501Api {
    public static string v150_1_msg_id_to_str(int id) => V1501State.MessageIdToString(id);
    public static string v150_1_data_bits_to_str(int code) => V1501State.DataBitsToString(code);
    public static string v150_1_parity_to_str(int code) => V1501State.ParityToString(code);
    public static string v150_1_stop_bits_to_str(int code) => V1501State.StopBitsToString(code);
    public static string v150_1_mr_event_type_to_str(int type) => V1501State.MrEventTypeToString((V1501MrEventId)type);
    public static string v150_1_cleardown_reason_to_str(int type) => V1501State.CleardownReasonToString((V1501CleardownReason)type);
    public static string v150_1_symbol_rate_to_str(int code) => V1501State.SymbolRateToString((V1501SymbolRate)code);
    public static string v150_1_modulation_to_str(int modulation) => V1501State.ModulationToString((V1501SelectedModulation)modulation);
    public static string v150_1_compression_to_str(int compression) => V1501State.CompressionToString((V1501Compression)compression);
    public static string v150_1_compression_direction_to_str(int direction) => V1501State.CompressionDirectionToString((V1501CompressionDirection)direction);
    public static string v150_1_error_correction_to_str(int correction) => V1501State.ErrorCorrectionToString((V1501ErrorCorrection)correction);
    public static string v150_1_break_source_to_str(int source) => V1501State.BreakSourceToString((V1501BreakSource)source);
    public static string v150_1_break_type_to_str(int type) => V1501State.BreakTypeToString((V1501BreakType)type);
    public static string v150_1_state_to_str(int state) => V1501State.ConnectionStateToString((V1501ConnectionState)state);
    public static string v150_1_status_reason_to_str(int status) => V1501State.StatusReasonToString((V1501StatusReason)status);
    public static string v150_1_jm_category_to_str(int category) => V1501State.JmCategoryToString(category);
    public static string v150_1_jm_info_modulation_to_str(int modulation) => V1501State.JmInfoModulationToString(modulation);
    public static string v150_1_signal_to_str(int signal) => V1501State.SignalToString((V1501Signal)signal);
    public static string v150_1_media_state_to_str(int state) => V1501State.MediaStateToString((V1501MediaState)state);
    public static int v150_1_state_machine(V1501State state, int signal, ReadOnlySpan<byte> message) { ArgumentNullException.ThrowIfNull(state); return state.StateMachine((V1501Signal)signal, message); }
    public static int v150_1_set_bits_per_character(V1501State state, int bits) { ArgumentNullException.ThrowIfNull(state); return state.SetBitsPerCharacter(bits); }
    public static int v150_1_set_parity(V1501State state, int mode) { ArgumentNullException.ThrowIfNull(state); return state.SetParity(mode); }
    public static int v150_1_set_stop_bits(V1501State state, int bits) { ArgumentNullException.ThrowIfNull(state); return state.SetStopBits(bits); }
    public static int v150_1_tx_null(V1501State state) { ArgumentNullException.ThrowIfNull(state); return state.TransmitNull(); }
    public static int v150_1_tx_init(V1501State state) { ArgumentNullException.ThrowIfNull(state); return state.TransmitInit(); }
    public static int v150_1_tx_xid_xchg(V1501State state) { ArgumentNullException.ThrowIfNull(state); return state.TransmitXidExchange(); }
    public static int v150_1_tx_jm_info(V1501State state) { ArgumentNullException.ThrowIfNull(state); return state.TransmitJmInfo(); }
    public static int v150_1_tx_start_jm(V1501State state) { ArgumentNullException.ThrowIfNull(state); return state.TransmitStartJm(); }
    public static int v150_1_tx_connect(V1501State state) { ArgumentNullException.ThrowIfNull(state); return state.TransmitConnect(); }
    public static int v150_1_tx_break(V1501State state, int source, int type, int duration) { ArgumentNullException.ThrowIfNull(state); return state.TransmitBreak((V1501BreakSource)source, (V1501BreakType)type, duration); }
    public static int v150_1_tx_break_ack(V1501State state) { ArgumentNullException.ThrowIfNull(state); return state.TransmitBreakAck(); }
    public static int v150_1_tx_mr_event(V1501State state, int eventId) { ArgumentNullException.ThrowIfNull(state); return state.TransmitMrEvent((V1501MrEventId)eventId); }
    public static int v150_1_tx_cleardown(V1501State state, int reason) { ArgumentNullException.ThrowIfNull(state); return state.TransmitCleardown((V1501CleardownReason)reason); }
    public static int v150_1_tx_prof_xchg(V1501State state) { ArgumentNullException.ThrowIfNull(state); return state.TransmitProfileExchange(); }
    public static int v150_1_tx_info_stream(V1501State state, ReadOnlySpan<byte> data) { ArgumentNullException.ThrowIfNull(state); return state.TransmitInformationStream(data); }
    public static int v150_1_process_rx_msg(V1501State state, int channel, int sequenceNumber, ReadOnlySpan<byte> message) { ArgumentNullException.ThrowIfNull(state); return state.ProcessReceivedSprtMessage(state, channel, sequenceNumber, message); }
    public static int v150_1_test_rx_sprt_msg(V1501State state, int channel, int sequenceNumber, ReadOnlySpan<byte> message) { ArgumentNullException.ThrowIfNull(state); state.ProcessReceivedSprtMessage(state, channel, sequenceNumber, message); return 0; }
    public static int v150_1_set_local_tc_payload_bytes(V1501State state, int channel, int maximumLength) { ArgumentNullException.ThrowIfNull(state); return state.SetLocalTransportChannelPayloadBytes(channel, maximumLength); }
    public static int v150_1_get_local_tc_payload_bytes(V1501State state, int channel) { ArgumentNullException.ThrowIfNull(state); return state.GetLocalTransportChannelPayloadBytes(channel); }
    public static int v150_1_set_info_stream_tx_mode(V1501State state, int channel, int messageId) { ArgumentNullException.ThrowIfNull(state); return state.SetInformationStreamTransmitMode(channel, messageId); }
    public static int v150_1_set_info_stream_msg_priorities(V1501State state, ReadOnlySpan<int> messageIds) { ArgumentNullException.ThrowIfNull(state); return state.SetInformationStreamMessagePriorities(messageIds); }
    public static int v150_1_set_local_busy(V1501State state, bool busy) { ArgumentNullException.ThrowIfNull(state); return state.SetLocalBusy(busy); }
    public static bool v150_1_get_far_busy_status(V1501State state) { ArgumentNullException.ThrowIfNull(state); return state.GetFarBusyStatus(); }
    public static int v150_1_set_modulation(V1501State state, int modulation) { ArgumentNullException.ThrowIfNull(state); return state.SetModulation(modulation); }
    public static int v150_1_set_compression_direction(V1501State state, int direction) { ArgumentNullException.ThrowIfNull(state); return state.SetCompressionDirection(direction); }
    public static int v150_1_set_compression(V1501State state, int compression) { ArgumentNullException.ThrowIfNull(state); return state.SetCompression(compression); }
    public static int v150_1_set_compression_parameters(V1501State state, int txDictionarySize, int rxDictionarySize, int txStringLength, int rxStringLength, int txHistorySize, int rxHistorySize) { ArgumentNullException.ThrowIfNull(state); return state.SetCompressionParameters(txDictionarySize, rxDictionarySize, txStringLength, rxStringLength, txHistorySize, rxHistorySize); }
    public static int v150_1_set_error_correction(V1501State state, int errorCorrection) { ArgumentNullException.ThrowIfNull(state); return state.SetErrorCorrection(errorCorrection); }
    public static int v150_1_set_tx_symbol_rate(V1501State state, bool enable, int rate) { ArgumentNullException.ThrowIfNull(state); return state.SetTransmitSymbolRate(enable, rate); }
    public static int v150_1_set_rx_symbol_rate(V1501State state, bool enable, int rate) { ArgumentNullException.ThrowIfNull(state); return state.SetReceiveSymbolRate(enable, rate); }
    public static int v150_1_set_tx_data_signalling_rate(V1501State state, int rate) { ArgumentNullException.ThrowIfNull(state); return state.SetTransmitDataSignallingRate(rate); }
    public static int v150_1_set_rx_data_signalling_rate(V1501State state, int rate) { ArgumentNullException.ThrowIfNull(state); return state.SetReceiveDataSignallingRate(rate); }
    public static void v150_1_set_near_cdscselect(V1501State state, V1501CallDiscriminationSelection selection) { ArgumentNullException.ThrowIfNull(state); state.SetNearCallDiscriminationSelection(selection); }
    public static void v150_1_set_far_cdscselect(V1501State state, V1501CallDiscriminationSelection selection) { ArgumentNullException.ThrowIfNull(state); state.SetFarCallDiscriminationSelection(selection); }
    public static void v150_1_set_near_modem_relay_gateway_type(V1501State state, V1501ModemRelayGatewayType type) { ArgumentNullException.ThrowIfNull(state); state.SetNearModemRelayGatewayType(type); }
    public static void v150_1_set_far_modem_relay_gateway_type(V1501State state, V1501ModemRelayGatewayType type) { ArgumentNullException.ThrowIfNull(state); state.SetFarModemRelayGatewayType(type); }
    public static void v150_1_set_rfc4733_mode(V1501State state, bool preferred) { ArgumentNullException.ThrowIfNull(state); state.SetRfc4733Mode(preferred); }
    public static void v150_1_set_call_discrimination_timeout(V1501State state, int timeout) { ArgumentNullException.ThrowIfNull(state); state.SetCallDiscriminationTimeout(timeout); }
    public static int v150_1_timer_expired(V1501State state, ulong now) { ArgumentNullException.ThrowIfNull(state); return state.TimerExpired(now); }
    public static V1501Logger v150_1_get_logging_state(V1501State state) { ArgumentNullException.ThrowIfNull(state); return state.Logging; }
    public static int sse_status_handler(V1501State state, int status) { ArgumentNullException.ThrowIfNull(state); return state.SseStatusHandler(status); }
    public static ulong update_sse_timer(V1501State state, ulong timeout) { ArgumentNullException.ThrowIfNull(state); return state.UpdateSseTimer(timeout); }
    public static int v150_1_sse_timer_expired(V1501State state, ulong now) { ArgumentNullException.ThrowIfNull(state); return state.SseTimerExpired(now); }
    public static void v150_1_sse_init(V1501State state, V1501SseTransmitPacketHandler? transmitPacketHandler, object? transmitUserData) { ArgumentNullException.ThrowIfNull(state); state.InitializeSse(transmitPacketHandler, transmitUserData); }

    public static V1501State? v150_1_init(
        V1501State? state,
        SprtTransmitPacketHandler? sprtTransmitPacketHandler,
        object? sprtTransmitUserData,
        byte sprtTransmitPayloadType,
        byte sprtReceivePayloadType,
        V1501SseTransmitPacketHandler? sseTransmitPacketHandler,
        object? sseTransmitUserData,
        V1501TimerHandler? timerHandler,
        object? timerUserData,
        V1501RxDataHandler? receiveDataHandler,
        object? receiveDataUserData,
        V1501RxStatusReportHandler? receiveStatusReportHandler,
        object? receiveStatusReportUserData,
        V1501SpeSignalHandler? speSignalHandler,
        object? speSignalUserData,
        IV1501SseBridge? sseBridge = null) {
        if (sprtTransmitPacketHandler is null || receiveDataHandler is null || receiveStatusReportHandler is null)
            return null;
        state ??= new V1501State();
        state.Initialize(
            sprtTransmitPacketHandler,
            sprtTransmitUserData,
            sprtTransmitPayloadType,
            sprtReceivePayloadType,
            sseTransmitPacketHandler,
            sseTransmitUserData,
            timerHandler,
            timerUserData,
            receiveDataHandler,
            receiveDataUserData,
            receiveStatusReportHandler,
            receiveStatusReportUserData,
            speSignalHandler,
            speSignalUserData,
            sseBridge);
        return state;
    }

    public static int v150_1_release(V1501State state) { ArgumentNullException.ThrowIfNull(state); return state.Release(); }
    public static int v150_1_free(V1501State? state) { if (state is null) return 0; int result = state.Release(); state.Dispose(); return result; }
}
