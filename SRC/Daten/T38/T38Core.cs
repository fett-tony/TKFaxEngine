/*
 * TKFaxEngine - managed C# port
 *
 * Combined port of t38_core.c and t38_core.h.
 * The public and private C declarations are represented in this single file.
 */

namespace TKFaxEngine.Daten.T38;

public enum T38Indicator {
    NoSignal = 0,
    Cng,
    Ced,
    V21Preamble,
    V27Ter2400Training,
    V27Ter4800Training,
    V29_7200Training,
    V29_9600Training,
    V17_7200ShortTraining,
    V17_7200LongTraining,
    V17_9600ShortTraining,
    V17_9600LongTraining,
    V17_12000ShortTraining,
    V17_12000LongTraining,
    V17_14400ShortTraining,
    V17_14400LongTraining,
    V8Ansam,
    V8Signal,
    V34ControlChannel1200,
    V34PrimaryChannel,
    V34ControlChannelRetrain,
    V33_12000Training,
    V33_14400Training
}

public enum T38DataType {
    None = -1,
    V21 = 0,
    V27Ter2400,
    V27Ter4800,
    V29_7200,
    V29_9600,
    V17_7200,
    V17_9600,
    V17_12000,
    V17_14400,
    V8,
    V34PrimaryRate,
    V34ControlChannel1200,
    V34PrimaryChannel,
    V33_12000,
    V33_14400
}

public enum T38FieldType {
    HdlcData = 0,
    HdlcSignalEnd,
    HdlcFcsOk,
    HdlcFcsBad,
    HdlcFcsOkSignalEnd,
    HdlcFcsBadSignalEnd,
    T4NonEcmData,
    T4NonEcmSignalEnd,
    CmMessage,
    JmMessage,
    CiMessage,
    V34Rate
}

public enum T38FieldClass {
    None = 0,
    Hdlc,
    NonEcm
}

public enum T38MessageType {
    T30Indicator = 0,
    T30Data = 1
}

public enum T38TransportType {
    Udptl = 0,
    Rtp,
    Tcp,
    TcpTpkt
}

public enum T38DataRateManagement {
    LocalTcf = 1,
    TransferredTcf = 2
}

public enum T38PacketCategory {
    Indicator = 0,
    ControlData = 1,
    ControlDataEnd = 2,
    ImageData = 3,
    ImageDataEnd = 4
}

[Flags]
public enum T38ChunkingMode {
    None = 0,
    MergeFcsWithData = 0x0001,
    WholeFrames = 0x0002,
    AllowTepTime = 0x0004,
    SendRegularIndicators = 0x0008,
    SendTwoSecondRegularIndicators = 0x0010
}

public enum T38LogLevel {
    Flow,
    Warning,
    ProtocolWarning
}

public sealed class T38Log {
    public Action<T38LogLevel, string>? Sink { get; set; }

    public void Write(T38LogLevel level, string message) {
        Sink?.Invoke(level, message);
    }

    public void Flow(string message) => Write(T38LogLevel.Flow, message);
    public void Warning(string message) => Write(T38LogLevel.Warning, message);
    public void ProtocolWarning(string message) => Write(T38LogLevel.ProtocolWarning, message);
}

public static class SignalStatus {
    public const int CarrierDown = -1;
    public const int CarrierUp = -2;
    public const int TrainingInProgress = -3;
    public const int TrainingSucceeded = -4;
    public const int TrainingFailed = -5;
    public const int FramingOk = -6;
    public const int EndOfData = -7;
    public const int Abort = -8;
    public const int Break = -9;
    public const int ShutdownComplete = -10;
    public const int OctetReport = -11;
    public const int PoorSignalQuality = -12;
    public const int ModemRetrainOccurred = -13;
    public const int LinkConnected = -14;
    public const int LinkDisconnected = -15;
    public const int LinkError = -16;
    public const int LinkIdle = -17;
}

public readonly record struct T38DataField(
    T38FieldType FieldType,
    ReadOnlyMemory<byte> Field) {
    public int FieldLength => Field.Length;
}

public delegate int T38TxPacketHandler(
    T38CoreState state,
    object? userData,
    ReadOnlyMemory<byte> packet,
    int count);

public delegate int T38RxIndicatorHandler(
    T38CoreState state,
    object? userData,
    T38Indicator indicator);

public delegate int T38RxDataHandler(
    T38CoreState state,
    object? userData,
    T38DataType dataType,
    T38FieldType fieldType,
    ReadOnlyMemory<byte> field);

public delegate int T38RxMissingHandler(
    T38CoreState state,
    object? userData,
    int rxSequenceNumber,
    int expectedSequenceNumber);

public sealed class T38CoreState {
    public const int RxBufferLength = 2048;
    public const int TxBufferLength = 16384;

    public T38TxPacketHandler? TxPacketHandler { get; set; }
    public object? TxPacketUserData { get; set; }

    public T38RxIndicatorHandler? RxIndicatorHandler { get; set; }
    public T38RxDataHandler? RxDataHandler { get; set; }
    public T38RxMissingHandler? RxMissingHandler { get; set; }
    public object? RxUserData { get; set; }

    public int MicrosecondsPerTxChunk { get; set; }
    public T38ChunkingMode ChunkingModes { get; set; }
    public int Iaf { get; set; }

    public T38DataRateManagement DataRateManagementMethod { get; set; }
    public T38TransportType DataTransportProtocol { get; set; }
    public bool FillBitRemoval { get; set; }
    public bool MmrTranscoding { get; set; }
    public bool JbigTranscoding { get; set; }
    public int MaxBufferSize { get; set; }
    public int MaxDatagramSize { get; set; }
    public int T38Version { get; set; }
    public bool AllowForTep { get; set; }
    public int FastestImageDataRate { get; set; }
    public bool PaceTransmission { get; set; }
    public bool CheckSequenceNumbers { get; set; }

    public int[] CategoryControl { get; } = new int[5];

    public int TxSequenceNumber { get; set; }
    public int RxExpectedSequenceNumber { get; set; }
    public int CurrentRxIndicator { get; set; }
    public int CurrentRxDataType { get; set; }
    public int CurrentRxFieldType { get; set; }
    public int CurrentTxIndicator { get; set; }
    public int V34Rate { get; set; }
    public int MissingPackets { get; set; }

    public T38Log Logging { get; } = new();
}

public static class T38Core {
    // Exact TKFaxEngineFX identifiers from t38_core.h.
    public const T38Indicator T38_IND_NO_SIGNAL = T38Indicator.NoSignal;
    public const T38Indicator T38_IND_CNG = T38Indicator.Cng;
    public const T38Indicator T38_IND_CED = T38Indicator.Ced;
    public const T38Indicator T38_IND_V21_PREAMBLE = T38Indicator.V21Preamble;
    public const T38Indicator T38_IND_V27TER_2400_TRAINING = T38Indicator.V27Ter2400Training;
    public const T38Indicator T38_IND_V27TER_4800_TRAINING = T38Indicator.V27Ter4800Training;
    public const T38Indicator T38_IND_V29_7200_TRAINING = T38Indicator.V29_7200Training;
    public const T38Indicator T38_IND_V29_9600_TRAINING = T38Indicator.V29_9600Training;
    public const T38Indicator T38_IND_V17_7200_SHORT_TRAINING = T38Indicator.V17_7200ShortTraining;
    public const T38Indicator T38_IND_V17_7200_LONG_TRAINING = T38Indicator.V17_7200LongTraining;
    public const T38Indicator T38_IND_V17_9600_SHORT_TRAINING = T38Indicator.V17_9600ShortTraining;
    public const T38Indicator T38_IND_V17_9600_LONG_TRAINING = T38Indicator.V17_9600LongTraining;
    public const T38Indicator T38_IND_V17_12000_SHORT_TRAINING = T38Indicator.V17_12000ShortTraining;
    public const T38Indicator T38_IND_V17_12000_LONG_TRAINING = T38Indicator.V17_12000LongTraining;
    public const T38Indicator T38_IND_V17_14400_SHORT_TRAINING = T38Indicator.V17_14400ShortTraining;
    public const T38Indicator T38_IND_V17_14400_LONG_TRAINING = T38Indicator.V17_14400LongTraining;
    public const T38Indicator T38_IND_V8_ANSAM = T38Indicator.V8Ansam;
    public const T38Indicator T38_IND_V8_SIGNAL = T38Indicator.V8Signal;
    public const T38Indicator T38_IND_V34_CNTL_CHANNEL_1200 = T38Indicator.V34ControlChannel1200;
    public const T38Indicator T38_IND_V34_PRI_CHANNEL = T38Indicator.V34PrimaryChannel;
    public const T38Indicator T38_IND_V34_CC_RETRAIN = T38Indicator.V34ControlChannelRetrain;
    public const T38Indicator T38_IND_V33_12000_TRAINING = T38Indicator.V33_12000Training;
    public const T38Indicator T38_IND_V33_14400_TRAINING = T38Indicator.V33_14400Training;

    public const T38DataType T38_DATA_NONE = T38DataType.None;
    public const T38DataType T38_DATA_V21 = T38DataType.V21;
    public const T38DataType T38_DATA_V27TER_2400 = T38DataType.V27Ter2400;
    public const T38DataType T38_DATA_V27TER_4800 = T38DataType.V27Ter4800;
    public const T38DataType T38_DATA_V29_7200 = T38DataType.V29_7200;
    public const T38DataType T38_DATA_V29_9600 = T38DataType.V29_9600;
    public const T38DataType T38_DATA_V17_7200 = T38DataType.V17_7200;
    public const T38DataType T38_DATA_V17_9600 = T38DataType.V17_9600;
    public const T38DataType T38_DATA_V17_12000 = T38DataType.V17_12000;
    public const T38DataType T38_DATA_V17_14400 = T38DataType.V17_14400;
    public const T38DataType T38_DATA_V8 = T38DataType.V8;
    public const T38DataType T38_DATA_V34_PRI_RATE = T38DataType.V34PrimaryRate;
    public const T38DataType T38_DATA_V34_CC_1200 = T38DataType.V34ControlChannel1200;
    public const T38DataType T38_DATA_V34_PRI_CH = T38DataType.V34PrimaryChannel;
    public const T38DataType T38_DATA_V33_12000 = T38DataType.V33_12000;
    public const T38DataType T38_DATA_V33_14400 = T38DataType.V33_14400;

    public const T38FieldType T38_FIELD_HDLC_DATA = T38FieldType.HdlcData;
    public const T38FieldType T38_FIELD_HDLC_SIG_END = T38FieldType.HdlcSignalEnd;
    public const T38FieldType T38_FIELD_HDLC_FCS_OK = T38FieldType.HdlcFcsOk;
    public const T38FieldType T38_FIELD_HDLC_FCS_BAD = T38FieldType.HdlcFcsBad;
    public const T38FieldType T38_FIELD_HDLC_FCS_OK_SIG_END = T38FieldType.HdlcFcsOkSignalEnd;
    public const T38FieldType T38_FIELD_HDLC_FCS_BAD_SIG_END = T38FieldType.HdlcFcsBadSignalEnd;
    public const T38FieldType T38_FIELD_T4_NON_ECM_DATA = T38FieldType.T4NonEcmData;
    public const T38FieldType T38_FIELD_T4_NON_ECM_SIG_END = T38FieldType.T4NonEcmSignalEnd;
    public const T38FieldType T38_FIELD_CM_MESSAGE = T38FieldType.CmMessage;
    public const T38FieldType T38_FIELD_JM_MESSAGE = T38FieldType.JmMessage;
    public const T38FieldType T38_FIELD_CI_MESSAGE = T38FieldType.CiMessage;
    public const T38FieldType T38_FIELD_V34RATE = T38FieldType.V34Rate;

    public const T38FieldClass T38_FIELD_CLASS_NONE = T38FieldClass.None;
    public const T38FieldClass T38_FIELD_CLASS_HDLC = T38FieldClass.Hdlc;
    public const T38FieldClass T38_FIELD_CLASS_NON_ECM = T38FieldClass.NonEcm;
    public const T38MessageType T38_TYPE_OF_MSG_T30_INDICATOR = T38MessageType.T30Indicator;
    public const T38MessageType T38_TYPE_OF_MSG_T30_DATA = T38MessageType.T30Data;
    public const T38TransportType T38_TRANSPORT_UDPTL = T38TransportType.Udptl;
    public const T38TransportType T38_TRANSPORT_RTP = T38TransportType.Rtp;
    public const T38TransportType T38_TRANSPORT_TCP = T38TransportType.Tcp;
    public const T38TransportType T38_TRANSPORT_TCP_TPKT = T38TransportType.TcpTpkt;
    public const T38DataRateManagement T38_DATA_RATE_MANAGEMENT_LOCAL_TCF = T38DataRateManagement.LocalTcf;
    public const T38DataRateManagement T38_DATA_RATE_MANAGEMENT_TRANSFERRED_TCF = T38DataRateManagement.TransferredTcf;
    public const T38PacketCategory T38_PACKET_CATEGORY_INDICATOR = T38PacketCategory.Indicator;
    public const T38PacketCategory T38_PACKET_CATEGORY_CONTROL_DATA = T38PacketCategory.ControlData;
    public const T38PacketCategory T38_PACKET_CATEGORY_CONTROL_DATA_END = T38PacketCategory.ControlDataEnd;
    public const T38PacketCategory T38_PACKET_CATEGORY_IMAGE_DATA = T38PacketCategory.ImageData;
    public const T38PacketCategory T38_PACKET_CATEGORY_IMAGE_DATA_END = T38PacketCategory.ImageDataEnd;
    public const T38ChunkingMode T38_CHUNKING_MERGE_FCS_WITH_DATA = T38ChunkingMode.MergeFcsWithData;
    public const T38ChunkingMode T38_CHUNKING_WHOLE_FRAMES = T38ChunkingMode.WholeFrames;
    public const T38ChunkingMode T38_CHUNKING_ALLOW_TEP_TIME = T38ChunkingMode.AllowTepTime;
    public const T38ChunkingMode T38_CHUNKING_SEND_REGULAR_INDICATORS = T38ChunkingMode.SendRegularIndicators;
    public const T38ChunkingMode T38_CHUNKING_SEND_2S_REGULAR_INDICATORS = T38ChunkingMode.SendTwoSecondRegularIndicators;
    public const int T38_RX_BUF_LEN = 2048;
    public const int T38_TX_BUF_LEN = 16384;

    private const int ACCEPTABLE_SEQ_NO_OFFSET = 2000;
    private const int DEFAULT_MICROSECONDS_PER_TX_CHUNK = 30000;

    private readonly record struct ModemStartupTime(int Tep, int Training, int Flags);

    private static readonly ModemStartupTime[] ModemStartupTimes =
    {
        new(0, 75_000, 0),
        new(0, 0, 0),
        new(0, 3_000_000, 0),
        new(0, 0, 1_000_000),
        new(215_000, 943_000, 200_000),
        new(215_000, 708_000, 200_000),
        new(215_000, 234_000, 200_000),
        new(215_000, 234_000, 200_000),
        new(215_000, 142_000, 200_000),
        new(215_000, 1_393_000, 200_000),
        new(215_000, 142_000, 200_000),
        new(215_000, 1_393_000, 200_000),
        new(215_000, 142_000, 200_000),
        new(215_000, 1_393_000, 200_000),
        new(215_000, 142_000, 200_000),
        new(215_000, 1_393_000, 200_000),
        new(0, 0, 0),
        new(0, 0, 0),
        new(0, 0, 200_000),
        new(0, 0, 200_000),
        new(0, 0, 0),
        new(215_000, 0, 200_000),
        new(215_000, 0, 200_000)
    };

    public static string t38_indicator_to_str(int indicator) {
        return (T38Indicator)indicator switch {
            T38Indicator.NoSignal => "no-signal",
            T38Indicator.Cng => "cng",
            T38Indicator.Ced => "ced",
            T38Indicator.V21Preamble => "v21-preamble",
            T38Indicator.V27Ter2400Training => "v27-2400-training",
            T38Indicator.V27Ter4800Training => "v27-4800-training",
            T38Indicator.V29_7200Training => "v29-7200-training",
            T38Indicator.V29_9600Training => "v29-9600-training",
            T38Indicator.V17_7200ShortTraining => "v17-7200-short-training",
            T38Indicator.V17_7200LongTraining => "v17-7200-long-training",
            T38Indicator.V17_9600ShortTraining => "v17-9600-short-training",
            T38Indicator.V17_9600LongTraining => "v17-9600-long-training",
            T38Indicator.V17_12000ShortTraining => "v17-12000-short-training",
            T38Indicator.V17_12000LongTraining => "v17-12000-long-training",
            T38Indicator.V17_14400ShortTraining => "v17-14400-short-training",
            T38Indicator.V17_14400LongTraining => "v17-14400-long-training",
            T38Indicator.V8Ansam => "v8-ansam",
            T38Indicator.V8Signal => "v8-signal",
            T38Indicator.V34ControlChannel1200 => "v34-cntl-channel-1200",
            T38Indicator.V34PrimaryChannel => "v34-pri-channel",
            T38Indicator.V34ControlChannelRetrain => "v34-CC-retrain",
            T38Indicator.V33_12000Training => "v33-12000-training",
            T38Indicator.V33_14400Training => "v33-14400-training",
            _ => "???"
        };
    }

    public static string t38_data_type_to_str(int dataType) {
        return (T38DataType)dataType switch {
            T38DataType.V21 => "v21",
            T38DataType.V27Ter2400 => "v27-2400",
            T38DataType.V27Ter4800 => "v27-4800",
            T38DataType.V29_7200 => "v29-7200",
            T38DataType.V29_9600 => "v29-9600",
            T38DataType.V17_7200 => "v17-7200",
            T38DataType.V17_9600 => "v17-9600",
            T38DataType.V17_12000 => "v17-12000",
            T38DataType.V17_14400 => "v17-14400",
            T38DataType.V8 => "v8",
            T38DataType.V34PrimaryRate => "v34-pri-rate",
            T38DataType.V34ControlChannel1200 => "v34-CC-1200",
            T38DataType.V34PrimaryChannel => "v34-pri-ch",
            T38DataType.V33_12000 => "v33-12000",
            T38DataType.V33_14400 => "v33-14400",
            _ => "???"
        };
    }

    public static string t38_field_type_to_str(int fieldType) {
        return (T38FieldType)fieldType switch {
            T38FieldType.HdlcData => "hdlc-data",
            T38FieldType.HdlcSignalEnd => "hdlc-sig-end",
            T38FieldType.HdlcFcsOk => "hdlc-fcs-OK",
            T38FieldType.HdlcFcsBad => "hdlc-fcs-BAD",
            T38FieldType.HdlcFcsOkSignalEnd => "hdlc-fcs-OK-sig-end",
            T38FieldType.HdlcFcsBadSignalEnd => "hdlc-fcs-BAD-sig-end",
            T38FieldType.T4NonEcmData => "t4-non-ecm-data",
            T38FieldType.T4NonEcmSignalEnd => "t4-non-ecm-sig-end",
            T38FieldType.CmMessage => "cm-message",
            T38FieldType.JmMessage => "jm-message",
            T38FieldType.CiMessage => "ci-message",
            T38FieldType.V34Rate => "v34rate",
            _ => "???"
        };
    }

    public static string t38_cm_profile_to_str(int profile) {
        return profile switch {
            '1' => "G3 FAX sending terminal",
            '2' => "G3 FAX receiving terminal",
            '3' => "V.34 HDX and G3 FAX sending terminal",
            '4' => "V.34 HDX and G3 FAX receiving terminal",
            '5' => "V.34 HDX-only FAX sending terminal",
            '6' => "V.34 HDX-only FAX receiving terminal",
            _ => "???"
        };
    }



    public static string t38_jm_to_str(byte[] data, int len) {
        ArgumentNullException.ThrowIfNull(data);
        ReadOnlySpan<byte> message = data.AsSpan(0, Math.Clamp(len, 0, data.Length));
        if (message.Length < 2)
            return "???";

        return (message[0], message[1]) switch {
            ((byte)'A', (byte)'0') => "ACK",
            ((byte)'N', (byte)'0') => "NACK: No compatible mode available",
            ((byte)'N', (byte)'1') => "NACK: No V.34 FAX, use G3 FAX",
            ((byte)'N', (byte)'2') => "NACK: V.34 only FAX.",
            _ => "???"
        };
    }



    public static int t38_v34rate_to_bps(byte[] data, int len) {
        ArgumentNullException.ThrowIfNull(data);
        ReadOnlySpan<byte> message = data.AsSpan(0, Math.Clamp(len, 0, data.Length));
        if (message.Length < 3)
            return -1;

        int rate = 0;
        for (int index = 0; index < 3; index++) {
            if (message[index] < (byte)'0' || message[index] > (byte)'9')
                return -1;
            rate = rate * 10 + message[index] - (byte)'0';
        }
        return rate * 100;
    }

    private static int classify_seq_no_offset(int expected, int actual) {
        if (expected > actual) {
            if (expected > actual + 0x10000 - ACCEPTABLE_SEQ_NO_OFFSET)
                return 1;
            if (expected < actual + ACCEPTABLE_SEQ_NO_OFFSET)
                return -1;
        } else {
            if (expected + ACCEPTABLE_SEQ_NO_OFFSET > actual)
                return 1;
            if (expected + 0x10000 - ACCEPTABLE_SEQ_NO_OFFSET < actual)
                return -1;
        }

        return 0;
    }



    public static int t38_core_rx_ifp_stream(
        T38CoreState state,
        byte[] buf,
        int len,
        ushort logSequenceNumber) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(buf);
        if ((uint)len > (uint)buf.Length)
            throw new ArgumentOutOfRangeException(nameof(len));
        ReadOnlySpan<byte> buffer = buf.AsSpan(0, len);

        int pointer = 0;
        int packetLength = buffer.Length;
        int incompleteResult;

        switch (state.DataTransportProtocol) {
            case T38TransportType.Tcp:
                incompleteResult = 0;
                break;

            case T38TransportType.TcpTpkt:
                if (buffer.Length >= 4) {
                    if (buffer[0] != 3 || buffer[1] != 0)
                        return -1;

                    packetLength = get_net_unaligned_uint16(buffer, 2);
                    if (buffer.Length < packetLength)
                        return 0;
                    pointer = 4;
                }
                incompleteResult = -1;
                break;

            default:
                incompleteResult = -1;
                break;
        }

        if (pointer + 1 > packetLength)
            return incompleteResult;

        byte first = buffer[pointer];
        bool dataFieldPresent = (first & 0x80) != 0;
        T38MessageType messageType = (T38MessageType)((first >> 6) & 1);

        switch (messageType) {
            case T38MessageType.T30Indicator: {
                    if (dataFieldPresent) {
                        state.Logging.ProtocolWarning($"Rx {logSequenceNumber,5}: Data field with indicator");
                        return -1;
                    }

                    state.CurrentRxDataType = -1;
                    state.CurrentRxFieldType = -1;

                    int indicator;
                    if ((buffer[pointer] & 0x20) != 0) {
                        if (pointer + 2 > packetLength)
                            return incompleteResult;

                        indicator = (int)T38Indicator.V8Ansam
                                  + (((buffer[pointer] << 2) & 0x3C)
                                  | ((buffer[pointer + 1] >> 6) & 0x03));

                        if (indicator > (int)T38Indicator.V33_14400Training) {
                            state.Logging.ProtocolWarning($"Rx {logSequenceNumber,5}: Unknown indicator - {indicator}");
                            return -1;
                        }

                        pointer += 2;
                    } else {
                        indicator = (buffer[pointer] >> 1) & 0x0F;
                        pointer++;
                    }

                    state.Logging.Flow($"Rx {logSequenceNumber,5}: indicator {t38_indicator_to_str(indicator)}");
                    state.RxIndicatorHandler!.Invoke(
                        state,
                        state.RxUserData,
                        (T38Indicator)indicator);
                    state.CurrentRxIndicator = indicator;
                    break;
                }

            case T38MessageType.T30Data: {
                    int dataType;
                    if ((buffer[pointer] & 0x20) != 0) {
                        if (pointer + 2 > packetLength)
                            return incompleteResult;

                        dataType = (int)T38DataType.V8
                                 + (((buffer[pointer] << 2) & 0x3C)
                                 | ((buffer[pointer + 1] >> 6) & 0x03));

                        if (dataType > (int)T38DataType.V33_14400) {
                            state.Logging.ProtocolWarning($"Rx {logSequenceNumber,5}: Unknown data type - {dataType}");
                            return -1;
                        }

                        pointer += 2;
                    } else {
                        dataType = (buffer[pointer] >> 1) & 0x0F;
                        if (dataType > (int)T38DataType.V17_14400) {
                            state.Logging.ProtocolWarning($"Rx {logSequenceNumber,5}: Unknown data type - {dataType}");
                            return -1;
                        }
                        pointer++;
                    }

                    if (!dataFieldPresent) {
                        state.Logging.ProtocolWarning($"Rx {logSequenceNumber,5}: Data type with no data field");
                        break;
                    }

                    if (pointer >= packetLength)
                        return incompleteResult;

                    int fieldCount = buffer[pointer++];
                    int fieldsStart = pointer;
                    bool otherHalf = false;

                    // Validation pass.
                    for (int i = 0; i < fieldCount; i++) {
                        if (!TryReadFieldHeader(
                                state,
                                buffer,
                                packetLength,
                                ref pointer,
                                ref otherHalf,
                                out int fieldType,
                                out bool fieldPresent)) {
                            return incompleteResult;
                        }

                        if (state.T38Version == 0
                            && fieldType > (int)T38FieldType.T4NonEcmSignalEnd) {
                            state.Logging.ProtocolWarning($"Rx {logSequenceNumber,5}: Unknown field type - {fieldType}");
                            return -1;
                        }

                        if (state.T38Version != 0
                            && fieldType > (int)T38FieldType.V34Rate) {
                            state.Logging.ProtocolWarning($"Rx {logSequenceNumber,5}: Unknown field type - {fieldType}");
                            return -1;
                        }

                        if (fieldPresent) {
                            if (pointer + 2 > packetLength)
                                return incompleteResult;

                            int octets = get_net_unaligned_uint16(buffer, pointer) + 1;
                            pointer += 2;
                            if (octets < 1 || pointer + octets > packetLength)
                                return incompleteResult;
                            pointer += octets;
                        }
                    }

                    if (otherHalf)
                        pointer++;
                    if (pointer > packetLength)
                        return incompleteResult;

                    // Processing pass.
                    pointer = fieldsStart;
                    otherHalf = false;
                    for (int i = 0; i < fieldCount; i++) {
                        if (!TryReadFieldHeader(
                                state,
                                buffer,
                                packetLength,
                                ref pointer,
                                ref otherHalf,
                                out int fieldType,
                                out bool fieldPresent)) {
                            return incompleteResult;
                        }

                        ReadOnlyMemory<byte> field = ReadOnlyMemory<byte>.Empty;
                        if (fieldPresent) {
                            int octets = get_net_unaligned_uint16(buffer, pointer) + 1;
                            pointer += 2;
                            field = buffer.Slice(pointer, octets).ToArray();
                            pointer += octets;
                        }

                        state.Logging.Flow(
                            $"Rx {logSequenceNumber,5}: ({i}) data " +
                            $"{t38_data_type_to_str(dataType)}/{t38_field_type_to_str(fieldType)} + {field.Length} byte(s)");

                        state.RxDataHandler!.Invoke(
                            state,
                            state.RxUserData,
                            (T38DataType)dataType,
                            (T38FieldType)fieldType,
                            field);

                        state.CurrentRxDataType = dataType;
                        state.CurrentRxFieldType = fieldType;
                    }

                    if (otherHalf)
                        pointer++;
                    break;
                }
        }

        return pointer > packetLength ? incompleteResult : pointer;
        }



    public static int t38_core_rx_ifp_packet(
        T38CoreState state,
        byte[] buf,
        int len,
        ushort sequenceNumber) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(buf);
        if ((uint)len > (uint)buf.Length)
            throw new ArgumentOutOfRangeException(nameof(len));
        ReadOnlySpan<byte> buffer = buf.AsSpan(0, len);

        int logSequenceNumber = state.CheckSequenceNumbers
            ? sequenceNumber
            : state.RxExpectedSequenceNumber;

        if (state.CheckSequenceNumbers) {
            int actual = sequenceNumber & 0xFFFF;
            if (actual != state.RxExpectedSequenceNumber) {
                if (state.RxExpectedSequenceNumber != -1) {
                    if (((actual + 1) & 0xFFFF) == state.RxExpectedSequenceNumber) {
                        state.Logging.Flow($"Rx {logSequenceNumber,5}: Repeat packet number");
                        return 0;
                    }

                    switch (classify_seq_no_offset(state.RxExpectedSequenceNumber, actual)) {
                        case -1:
                            state.Logging.Flow(
                                $"Rx {logSequenceNumber,5}: Late packet - expected {state.RxExpectedSequenceNumber}");
                            return 0;

                        case 1:
                            state.Logging.Flow(
                                $"Rx {logSequenceNumber,5}: Missing from {state.RxExpectedSequenceNumber}");
                            state.RxMissingHandler!.Invoke(
                                state,
                                state.RxUserData,
                                state.RxExpectedSequenceNumber,
                                actual);
                            state.MissingPackets += actual - state.RxExpectedSequenceNumber;
                            break;

                        default:
                            state.Logging.Flow($"Rx {logSequenceNumber,5}: Sequence restart");
                            state.RxMissingHandler!.Invoke(state, state.RxUserData, -1, -1);
                            state.MissingPackets++;
                            break;
                    }
                }

                state.RxExpectedSequenceNumber = actual;
            }
        }

        if (buffer.Length < 1) {
            state.Logging.ProtocolWarning($"Rx {logSequenceNumber,5}: Bad packet length - {buffer.Length}");
            return -1;
        }

        state.RxExpectedSequenceNumber = (state.RxExpectedSequenceNumber + 1) & 0xFFFF;
        int consumed = t38_core_rx_ifp_stream(state, buf, len, sequenceNumber);
        if (consumed != buffer.Length) {
            if (consumed >= 0) {
                state.Logging.ProtocolWarning(
                    $"Rx {logSequenceNumber,5}: Invalid length for packet - {consumed} {buffer.Length}");
            }
            return -1;
        }

        return 0;
        }

    public static int t38_core_send_indicator(T38CoreState state, int indicator) {
        ArgumentNullException.ThrowIfNull(state);

        int delay = 0;
        if (state.CurrentTxIndicator == indicator)
            return 0;

        int transmissions = (indicator & 0x100) != 0
            ? 1
            : state.CategoryControl[(int)T38PacketCategory.Indicator];

        indicator &= 0xFF;
        if (state.CategoryControl[(int)T38PacketCategory.Indicator] != 0) {
            byte[] packet = new byte[100];
            int packetLength = t38_encode_indicator(state, packet, indicator);
            if (packetLength < 0) {
                state.Logging.Flow($"T.38 indicator len is {packetLength}");
                return packetLength;
            }

            state.Logging.Flow(
                $"Tx {state.TxSequenceNumber,5}: indicator {t38_indicator_to_str(indicator)}");

            if (state.TxPacketHandler!.Invoke(
                    state,
                    state.TxPacketUserData,
                    new ReadOnlyMemory<byte>(packet, 0, packetLength),
                    transmissions) < 0) {
                state.Logging.ProtocolWarning("Tx packet handler failure");
                return -1;
            }

            state.TxSequenceNumber = (state.TxSequenceNumber + 1) & 0xFFFF;
            if (state.PaceTransmission) {
                delay = ModemStartupTimes[indicator].Training;
                if (state.AllowForTep)
                    delay += ModemStartupTimes[indicator].Tep;
            }
        }

        state.CurrentTxIndicator = indicator;
        return delay;
    }

    public static int t38_core_send_flags_delay(T38CoreState state, int indicator) {
        ArgumentNullException.ThrowIfNull(state);
        return state.PaceTransmission
            ? ModemStartupTimes[indicator].Flags
            : 0;
    }

    public static int t38_core_send_training_delay(T38CoreState state, int indicator) {
        ArgumentNullException.ThrowIfNull(state);
        return state.PaceTransmission
            ? ModemStartupTimes[indicator].Training
            : 0;
    }

    public static int t38_core_send_data(
        T38CoreState state,
        int dataType,
        int fieldType,
        byte[]? field,
        int fieldLength,
        int category) {
        ArgumentNullException.ThrowIfNull(state);
        if (fieldLength < 0 || (field is not null && fieldLength > field.Length))
            throw new ArgumentOutOfRangeException(nameof(fieldLength));
        if (field is null && fieldLength != 0)
            throw new ArgumentNullException(nameof(field));

        ReadOnlyMemory<byte> memory = fieldLength == 0
            ? ReadOnlyMemory<byte>.Empty
            : new ReadOnlyMemory<byte>(field!, 0, fieldLength);
        T38DataField[] fields = {
            new((T38FieldType)fieldType, memory)
        };
        byte[] packet = new byte[1000];
        int packetLength = t38_encode_data(state, packet, dataType, fields, 1);
        if (packetLength < 0) {
            state.Logging.Flow($"T.38 data len is {packetLength}");
            return packetLength;
        }

        if (state.TxPacketHandler!.Invoke(
                state,
                state.TxPacketUserData,
                new ReadOnlyMemory<byte>(packet, 0, packetLength),
                state.CategoryControl[category]) < 0) {
            state.Logging.ProtocolWarning("Tx packet handler failure");
            return -1;
        }
        state.TxSequenceNumber = (state.TxSequenceNumber + 1) & 0xFFFF;
        return 0;
    }

    public static int t38_core_send_data_multi_field(
        T38CoreState state,
        int dataType,
        T38DataField[] field,
        int fields,
        int category) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(field);
        if (fields < 0 || fields > field.Length)
            throw new ArgumentOutOfRangeException(nameof(fields));
        if ((uint)category >= (uint)state.CategoryControl.Length)
            throw new ArgumentOutOfRangeException(nameof(category));

        byte[] packet = new byte[1000];
        int packetLength = t38_encode_data(state, packet, dataType, field, fields);
        if (packetLength < 0) {
            state.Logging.Flow($"T.38 data len is {packetLength}");
            return packetLength;
        }

        int repeats = state.CategoryControl[category];
        if (state.TxPacketHandler!.Invoke(
                state,
                state.TxPacketUserData,
                new ReadOnlyMemory<byte>(packet, 0, packetLength),
                repeats) < 0) {
            state.Logging.ProtocolWarning("Tx packet handler failure");
            return -1;
        }

        state.TxSequenceNumber = (state.TxSequenceNumber + 1) & 0xFFFF;
        return 0;
    }

    public static void t38_set_data_rate_management_method(T38CoreState state, int method)
        => state.DataRateManagementMethod = (T38DataRateManagement)method;

    public static void t38_set_data_transport_protocol(T38CoreState state, int protocol)
        => state.DataTransportProtocol = (T38TransportType)protocol;

    public static void t38_set_fill_bit_removal(T38CoreState state, bool enabled)
        => state.FillBitRemoval = enabled;

    public static void t38_set_mmr_transcoding(T38CoreState state, bool enabled)
        => state.MmrTranscoding = enabled;

    public static void t38_set_jbig_transcoding(T38CoreState state, bool enabled)
        => state.JbigTranscoding = enabled;

    public static void t38_set_max_buffer_size(T38CoreState state, int size)
        => state.MaxBufferSize = size;

    public static void t38_set_max_datagram_size(T38CoreState state, int size)
        => state.MaxDatagramSize = size;

    public static void t38_set_t38_version(T38CoreState state, int version)
        => state.T38Version = version;

    public static void t38_set_sequence_number_handling(T38CoreState state, bool check)
        => state.CheckSequenceNumbers = check;

    public static void t38_set_pace_transmission(T38CoreState state, int paceTransmission)
        => state.PaceTransmission = paceTransmission != 0;

    public static void t38_set_tep_handling(T38CoreState state, bool allowForTep)
        => state.AllowForTep = allowForTep;

    public static void t38_set_redundancy_control(T38CoreState state, int category, int setting) {
        if ((uint)category >= (uint)state.CategoryControl.Length)
            throw new ArgumentOutOfRangeException(nameof(category));
        state.CategoryControl[category] = setting;
    }

    public static void t38_set_fastest_image_data_rate(T38CoreState state, int maximumRate)
        => state.FastestImageDataRate = maximumRate;

    public static int t38_get_fastest_image_data_rate(T38CoreState state)
        => state.FastestImageDataRate;

    public static void t38_set_tx_packet_interval(T38CoreState state, int microseconds)
        => state.MicrosecondsPerTxChunk = microseconds;

    public static int t38_get_tx_packet_interval(T38CoreState state)
        => state.MicrosecondsPerTxChunk;

    public static T38Log t38_core_get_logging_state(T38CoreState state)
        => state.Logging;

    public static int t38_core_restart(T38CoreState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.CurrentRxIndicator = -1;
        state.CurrentRxDataType = -1;
        state.CurrentRxFieldType = -1;
        state.CurrentTxIndicator = -1;
        state.RxExpectedSequenceNumber = -1;
        return 0;
    }

    public static T38CoreState t38_core_init(
        T38CoreState? state,
        T38RxIndicatorHandler? rxIndicatorHandler,
        T38RxDataHandler? rxDataHandler,
        T38RxMissingHandler? rxMissingHandler,
        object? rxUserData,
        T38TxPacketHandler? txPacketHandler,
        object? txPacketUserData) {
        state ??= new T38CoreState();

        state.TxPacketHandler = null;
        state.TxPacketUserData = null;
        state.RxIndicatorHandler = null;
        state.RxDataHandler = null;
        state.RxMissingHandler = null;
        state.RxUserData = null;
        state.MicrosecondsPerTxChunk = 0;
        state.ChunkingModes = T38ChunkingMode.None;
        state.Iaf = 0;
        state.DataRateManagementMethod = 0;
        state.DataTransportProtocol = 0;
        state.FillBitRemoval = false;
        state.MmrTranscoding = false;
        state.JbigTranscoding = false;
        state.MaxBufferSize = 0;
        state.MaxDatagramSize = 0;
        state.T38Version = 0;
        state.AllowForTep = false;
        state.FastestImageDataRate = 0;
        state.PaceTransmission = false;
        state.CheckSequenceNumbers = false;
        Array.Clear(state.CategoryControl, 0, state.CategoryControl.Length);
        state.TxSequenceNumber = 0;
        state.RxExpectedSequenceNumber = 0;
        state.CurrentRxIndicator = 0;
        state.CurrentRxDataType = 0;
        state.CurrentRxFieldType = 0;
        state.CurrentTxIndicator = 0;
        state.V34Rate = 0;
        state.MissingPackets = 0;
        state.Logging.Sink = null;

        state.DataRateManagementMethod = T38DataRateManagement.TransferredTcf;
        state.DataTransportProtocol = T38TransportType.Udptl;
        state.FillBitRemoval = false;
        state.MmrTranscoding = false;
        state.JbigTranscoding = false;
        state.MaxBufferSize = 400;
        state.MaxDatagramSize = 100;
        state.T38Version = 0;
        state.CheckSequenceNumbers = true;
        state.PaceTransmission = true;
        state.MicrosecondsPerTxChunk = DEFAULT_MICROSECONDS_PER_TX_CHUNK;

        for (int i = 0; i < state.CategoryControl.Length; i++)
            state.CategoryControl[i] = 1;

        state.RxIndicatorHandler = rxIndicatorHandler;
        state.RxDataHandler = rxDataHandler;
        state.RxMissingHandler = rxMissingHandler;
        state.RxUserData = rxUserData;
        state.TxPacketHandler = txPacketHandler;
        state.TxPacketUserData = txPacketUserData;

        t38_core_restart(state);
        return state;
    }

    public static int t38_core_release(T38CoreState state) {
        ArgumentNullException.ThrowIfNull(state);
        return 0;
    }

    public static int t38_core_free(T38CoreState? state) {
        _ = state;
        return 0;
    }

    private static int t38_encode_indicator(
        T38CoreState state,
        byte[] buffer,
        int indicator) {
        int length = 0;
        if (state.DataTransportProtocol == T38TransportType.TcpTpkt)
            length = 4;

        if (indicator <= (int)T38Indicator.V17_14400LongTraining) {
            buffer[length++] = (byte)(indicator << 1);
        } else if (state.T38Version != 0
                   && indicator <= (int)T38Indicator.V33_14400Training) {
            put_net_unaligned_uint16(
                buffer,
                length,
                0x2000 | ((indicator - (int)T38Indicator.V8Ansam) << 6));
            length += 2;
        } else {
            length = -1;
        }

        if (state.DataTransportProtocol == T38TransportType.TcpTpkt) {
            buffer[0] = 3;
            buffer[1] = 0;
            put_net_unaligned_uint16(buffer, 2, length);
        }
        return length;
    }

    private static int t38_encode_data(
        T38CoreState state,
        byte[] buffer,
        int dataType,
        T38DataField[] field,
        int fields) {
        int length = 0;
        if (state.DataTransportProtocol == T38TransportType.TcpTpkt)
            length = 4;

        int dataFieldPresent = fields > 0 ? 0x80 : 0x00;
        if (dataType <= (int)T38DataType.V17_14400) {
            buffer[length++] = (byte)(dataFieldPresent | 0x40 | (dataType << 1));
        } else if (state.T38Version != 0
                   && dataType <= (int)T38DataType.V33_14400) {
            put_net_unaligned_uint16(
                buffer,
                length,
                (dataFieldPresent << 8)
                | 0x6000
                | ((dataType - (int)T38DataType.V8) << 6));
            length += 2;
        } else {
            return -1;
        }

        if (dataFieldPresent != 0) {
            uint encodedLength = 0;
            int dataFieldNumber = 0;
            uint fragmentLength;
            do {
                uint value = (uint)fields - encodedLength;
                int encodedFragmentLength;
                if (value < 0x80) {
                    buffer[length++] = (byte)value;
                    encodedFragmentLength = (int)value;
                } else if (value < 0x4000) {
                    put_net_unaligned_uint16(buffer, length, 0x8000 | (int)value);
                    length += 2;
                    encodedFragmentLength = (int)value;
                } else {
                    int multiplier = value / 0x4000 < 4
                        ? (int)(value / 0x4000)
                        : 4;
                    buffer[length++] = (byte)(0xC0 | multiplier);
                    encodedFragmentLength = 0x4000 * multiplier;
                }

                fragmentLength = (uint)encodedFragmentLength;
                encodedLength += fragmentLength;
                for (int index = 0; index < (int)encodedLength; index++) {
                    T38DataField item = field[dataFieldNumber];
                    int fieldType = (int)item.FieldType;
                    int fieldDataPresent = item.FieldLength > 0 ? 1 : 0;

                    if (state.T38Version == 0) {
                        if (fieldType > (int)T38FieldType.T4NonEcmSignalEnd)
                            return -1;
                        buffer[length++] = (byte)((fieldDataPresent << 7) | (fieldType << 4));
                    } else if (fieldType <= (int)T38FieldType.T4NonEcmSignalEnd) {
                        buffer[length++] = (byte)((fieldDataPresent << 7) | (fieldType << 3));
                    } else if (fieldType <= (int)T38FieldType.V34Rate) {
                        buffer[length++] = (byte)(
                            (fieldDataPresent << 7)
                            | 0x40
                            | ((fieldType - (int)T38FieldType.CmMessage) >> 2));
                        buffer[length++] = (byte)(
                            ((fieldType - (int)T38FieldType.CmMessage) << 6) & 0xC0);
                    } else {
                        return -1;
                    }

                    if (fieldDataPresent != 0) {
                        if (item.FieldLength < 1 || item.FieldLength > 65_535)
                            return -1;
                        put_net_unaligned_uint16(buffer, length, item.FieldLength - 1);
                        length += 2;
                        item.Field.Span.CopyTo(buffer.AsSpan(length));
                        length += item.FieldLength;
                    }
                    dataFieldNumber++;
                }
            } while ((int)encodedLength != fields || fragmentLength >= 16_384);
        }

        for (int dataFieldNumber = 0; dataFieldNumber < fields; dataFieldNumber++) {
            state.Logging.Flow(
                $"Tx {state.TxSequenceNumber,5}: ({dataFieldNumber}) data " +
                $"{t38_data_type_to_str(dataType)}/" +
                $"{t38_field_type_to_str((int)field[dataFieldNumber].FieldType)} " +
                $"+ {field[dataFieldNumber].FieldLength} byte(s)");
        }

        if (state.DataTransportProtocol == T38TransportType.TcpTpkt) {
            buffer[0] = 3;
            buffer[1] = 0;
            put_net_unaligned_uint16(buffer, 2, length);
        }
        return length;
    }

    private static bool TryReadFieldHeader(
        T38CoreState state,
        ReadOnlySpan<byte> buffer,
        int packetLength,
        ref int pointer,
        ref bool otherHalf,
        out int fieldType,
        out bool fieldDataPresent) {
        fieldType = 0;
        fieldDataPresent = false;
        if (pointer >= packetLength)
            return false;

        if (state.T38Version == 0) {
            if (otherHalf) {
                fieldDataPresent = ((buffer[pointer] >> 3) & 1) != 0;
                fieldType = buffer[pointer] & 0x07;
                pointer++;
                otherHalf = false;
            } else {
                fieldDataPresent = ((buffer[pointer] >> 7) & 1) != 0;
                fieldType = (buffer[pointer] >> 4) & 0x07;
                if (fieldDataPresent)
                    pointer++;
                else
                    otherHalf = true;
            }

            return true;
        }

        fieldDataPresent = ((buffer[pointer] >> 7) & 1) != 0;
        if ((buffer[pointer] & 0x40) != 0) {
            if (pointer + 2 > packetLength)
                return false;
            fieldType = (int)T38FieldType.CmMessage
                      + (((buffer[pointer] << 2) & 0x3C)
                      | ((buffer[pointer + 1] >> 6) & 0x03));
            pointer += 2;
        } else {
            fieldType = (buffer[pointer] >> 3) & 0x07;
            pointer++;
        }

        return true;
    }

    private static void put_net_unaligned_uint16(byte[] buffer, int offset, int value) {
        buffer[offset] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 1] = (byte)(value & 0xFF);
    }

    private static int get_net_unaligned_uint16(ReadOnlySpan<byte> data, int offset) {
        return (data[offset] << 8) | data[offset + 1];
    }







}
