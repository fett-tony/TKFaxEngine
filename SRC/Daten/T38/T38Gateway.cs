/*
 * TKFaxEngine - managed C# port
 *
 * Combined port of t38_gateway.c and t38_gateway.h.
 *
 * The packet, HDLC, non-ECM, control-message and gateway state logic is
 * managed. The gateway directly owns and drives FaxModems, matching the
 * native t38_gateway_state_t/audio.modems ownership model.
 */

using global::TKFaxEngine.Modem;
using FaxModemChannel = global::TKFaxEngine.FaxModemChannel;
using NonEcmPutBitHandler = global::TKFaxEngine.NonEcmPutBitHandler;

namespace TKFaxEngine.Daten.T38;

[Flags]
public enum T38GatewaySupportedModems {
    None = 0,
    V27Ter = 0x01,
    V29 = 0x02,
    V17 = 0x04,
    V34 = 0x08,
    Iaf = 0x10
}

public enum T38GatewayModemType {
    None = 0,
    V21Receive,
    V27TerReceive,
    V29Receive,
    V17Receive
}

public sealed class T38GatewayStatistics {
    public int BitRate { get; set; }
    public bool ErrorCorrectingMode { get; set; }
    public int PagesTransferred { get; set; }
}

public delegate void T38GatewayRealTimeFrameHandler(
    object? userData,
    bool incoming,
    ReadOnlyMemory<byte> message);

public sealed class T38GatewayHdlcBuffer {
    public byte[] Buffer { get; } = new byte[T38Gateway.MaxHdlcLength];
    public int Length { get; set; }
    public int Flags { get; set; }
    public int Contents { get; set; }

    public void Clear() {
        Length = 0;
        Flags = 0;
        Contents = 0;
    }
}

public sealed class T38GatewayHdlcQueue {
    public T38GatewayHdlcBuffer[] Buffers { get; } = CreateBuffers();
    public int Input { get; set; }
    public int Output { get; set; }

    private static T38GatewayHdlcBuffer[] CreateBuffers() {
        T38GatewayHdlcBuffer[] result = new T38GatewayHdlcBuffer[T38Gateway.HdlcBufferCount];
        for (int index = 0; index < result.Length; index++)
            result[index] = new T38GatewayHdlcBuffer();
        return result;
    }
}

public sealed class T38GatewayToT38State {
    public byte[] Data { get; } = new byte[T38Gateway.ReceiveBufferLength];
    public int DataPointer { get; set; }
    public uint BitStream { get; set; }
    public int BitsAbsorbed { get; set; }
    public int BitNumber { get; set; }
    public ushort Crc { get; set; }
    public bool FillBitRemoval { get; set; }
    public int OctetsPerDataPacket { get; set; }
    public int InputBits { get; set; }
    public int OutputOctets { get; set; }
}

public sealed class T38GatewayHdlcReceiveState {
    public byte[] Buffer { get; } = new byte[T38Gateway.MaxHdlcLength];
    public uint RawBitStream { get; set; }
    public int Length { get; set; }
    public int NumberOfBits { get; set; }
    public int FlagsSeen { get; set; }
    public int FramingOkThreshold { get; set; } = 5;
    public bool FramingOkAnnounced { get; set; }
    public int ByteInProgress { get; set; }
    public int ReceiveAborts { get; set; }
    public int ReceiveCrcErrors { get; set; }
    public int ReceiveFrames { get; set; }
    public int ReceiveBytes { get; set; }
    public int ReceiveLengthErrors { get; set; }
}

public sealed class T38GatewayT38State {
    public T38CoreState T38 { get; set; } = new();
    public int[] SuppressNsxLength { get; } = new int[2];
    public byte[][] SuppressNsxString { get; } = new byte[][]
    {
        new byte[T38Gateway.MaximumNsxSuppression],
        new byte[T38Gateway.MaximumNsxSuppression]
    };
    public bool[] CorruptCurrentFrame { get; } = new bool[2];
    public T38FieldClass CurrentRxFieldClass { get; set; }
    public int InProgressRxIndicator { get; set; } = (int)T38Indicator.NoSignal;
    public T38DataType CurrentTxDataType { get; set; } = T38DataType.V21;
}

public sealed class T38GatewayCoreState {
    public T38GatewaySupportedModems SupportedModems { get; set; }
    public bool EcmAllowed { get; set; }
    public bool ShortTrain { get; set; }
    public bool ImageDataMode { get; set; }
    public int MinimumRowBits { get; set; }
    public bool CountPageOnMcf { get; set; }
    public int PagesConfirmed { get; set; }
    public bool EcmMode { get; set; }
    public int FastBitRate { get; set; }
    public T38GatewayModemType FastRxModem { get; set; }
    public T38GatewayModemType FastRxActive { get; set; }
    public int TimedMode { get; set; }
    public int SamplesToTimeout { get; set; }
    public T38GatewayToT38State ToT38 { get; } = new();
    public T38GatewayHdlcQueue HdlcToModem { get; } = new();
    public T38NonEcmBufferState NonEcmToModem { get; set; } = new();
    public T38GatewayHdlcReceiveState HdlcReceive { get; } = new();
    public T38GatewayRealTimeFrameHandler? RealTimeFrameHandler { get; set; }
    public object? RealTimeFrameUserData { get; set; }
}

public sealed class T38GatewayState {
    public T38GatewayT38State T38Side { get; } = new();
    public T38GatewayCoreState Core { get; } = new();
    public FaxModems Modems { get; } = new();
    public T38Log Logging { get; } = new();
}

public static class T38Gateway {
    // Exact TKFaxEngineFX identifiers from t38_gateway.c/t38_gateway.h.
    public const int MAX_NSX_SUPPRESSION = 10;
    public const int T38_TX_HDLC_BUFS = 256;
    public const int T38_MAX_HDLC_LEN = 260;
    public const int T38_RX_BUF_LEN = 2048;
    private const int HDLC_START_BUFFER_LEVEL = 8;
    private const int INDICATOR_TX_COUNT = 3;
    private const int DATA_TX_COUNT = 1;
    private const int DATA_END_TX_COUNT = 3;
    private const int HDLC_FRAMING_OK_THRESHOLD = 5;
    private const int HDLC_TRAMISSION_LAG_OCTETS = 2;

    public const int MaximumNsxSuppression = MAX_NSX_SUPPRESSION;
    public const int HdlcBufferCount = T38_TX_HDLC_BUFS;
    public const int MaxHdlcLength = T38_MAX_HDLC_LEN;
    public const int ReceiveBufferLength = T38_RX_BUF_LEN;

    private const int HdlcStartBufferLevel = HDLC_START_BUFFER_LEVEL;
    private const int IndicatorTransmitCount = INDICATOR_TX_COUNT;
    private const int DataTransmitCount = DATA_TX_COUNT;
    private const int DataEndTransmitCount = DATA_END_TX_COUNT;
    private const int HdlcFramingOkThreshold = HDLC_FRAMING_OK_THRESHOLD;
    private const int HdlcTransmissionLagOctets = HDLC_TRAMISSION_LAG_OCTETS;

    private const int HdlcFlagFinished = 0x01;
    private const int HdlcFlagCorruptCrc = 0x02;
    private const int HdlcFlagProceedWithOutput = 0x04;
    private const int HdlcFlagMissingData = 0x08;
    private const int FlagIndicator = 0x100;
    private const int FlagData = 0x200;

    private const int TimedModeStartup = 0;
    private const int TimedModeIdle = 1;
    private const int TimedModeTcfFastModemAnnounced = 2;
    private const int TimedModeTcfFastModemSeen = 3;
    private const int TimedModeTcfPastV21Modem = 4;
    private const int TimedModeTcfBegin = 5;

    private const byte DisBit1 = 0x01;
    private const byte DisBit2 = 0x02;
    private const byte DisBit3 = 0x04;
    private const byte DisBit4 = 0x08;
    private const byte DisBit5 = 0x10;
    private const byte DisBit6 = 0x20;
    private const byte DisBit7 = 0x40;
    private const byte DisBit8 = 0x80;

    private const byte T30Nsf = 0x20;
    private const byte T30Nsc = 0x21;
    private const byte T30Nss = 0x22;
    private const byte T30Dis = 0x80;
    private const byte T30Dtc = 0x81;
    private const byte T30Dcs = 0x82;
    private const byte T30Ctc = 0x12;
    private const byte T30Cfr = 0x84;
    private const byte T30Ftt = 0x44;
    private const byte T30Ctr = 0xC4;
    private const byte T30Rtp = 0xCC;
    private const byte T30Rtn = 0x4C;
    private const byte T30Pps = 0xBE;
    private const byte T30Eos = 0x1E;
    private const byte T30Eop = 0x2E;
    private const byte T30PriEop = 0x3E;
    private const byte T30Mps = 0x4E;
    private const byte T30PriMps = 0x5E;
    private const byte T30Eom = 0x8E;
    private const byte T30PriEom = 0x9E;
    private const byte T30Mcf = 0x8C;

    private readonly record struct ModemCode(
        int BitRate,
        T38GatewayModemType Modem,
        byte DcsCode);

    private static readonly ModemCode[] ModemCodes =
    {
        new(14_400, T38GatewayModemType.V17Receive, DisBit6),
        new(12_000, T38GatewayModemType.V17Receive, DisBit6 | DisBit4),
        new(9_600, T38GatewayModemType.V17Receive, DisBit6 | DisBit3),
        new(9_600, T38GatewayModemType.V29Receive, DisBit3),
        new(7_200, T38GatewayModemType.V17Receive, DisBit6 | DisBit4 | DisBit3),
        new(7_200, T38GatewayModemType.V29Receive, DisBit4 | DisBit3),
        new(4_800, T38GatewayModemType.V27TerReceive, DisBit4),
        new(2_400, T38GatewayModemType.V27TerReceive, 0),
        new(0, T38GatewayModemType.None, 0)
    };

    private static readonly int[] MinimumScanLineTimes = { 20, 5, 10, 0, 40, 0, 0, 0 };

    private static void tone_detected(
        object? user_data,
        int tone,
        int level,
        int delay) {
        _ = delay;
        if (user_data is T38GatewayState state) {
            state.Logging.Flow(
                $"{state.Modems.ConnectToneToString(tone)} detected ({level}dBm0)");
        }
    }

    private static int t38_gateway_audio_init(T38GatewayState s) {
        s.Modems.Initialize(
            useTep: false,
            hdlcAccept: static (_, _, _) => { },
            hdlcTransmitUnderflow: () => hdlc_underflow_handler(s),
            nonEcmPutBit: bit => non_ecm_put_bit(s, bit),
            nonEcmGetBit: () => T38NonEcmBuffer.t38_non_ecm_buffer_get_bit(
                s.Core.NonEcmToModem),
            toneDetected: (tone, level, delay) => tone_detected(s, tone, level, delay));

        s.Modems.InitializeHdlcTransmitter(progressive: true);
        s.Modems.ConfigureRawV21Receiver(bit => t38_hdlc_rx_put_bit(s, bit), -30.0f);
        return 0;
    }

    private static int t38_gateway_t38_init(
        T38GatewayState t,
        T38TxPacketHandler tx_packet_handler,
        object? tx_packet_user_data) {
        t.T38Side.T38 = T38Core.t38_core_init(
            t.T38Side.T38,
            process_rx_indicator,
            process_rx_data,
            process_rx_missing,
            t,
            tx_packet_handler,
            tx_packet_user_data);

        t.T38Side.T38.Logging.Sink =
            (level, message) => t.Logging.Write(level, message);

        T38Core.t38_set_redundancy_control(
            t.T38Side.T38,
            (int)T38PacketCategory.Indicator,
            IndicatorTransmitCount);
        T38Core.t38_set_redundancy_control(
            t.T38Side.T38,
            (int)T38PacketCategory.ControlData,
            DataTransmitCount);
        T38Core.t38_set_redundancy_control(
            t.T38Side.T38,
            (int)T38PacketCategory.ControlDataEnd,
            DataEndTransmitCount);
        T38Core.t38_set_redundancy_control(
            t.T38Side.T38,
            (int)T38PacketCategory.ImageData,
            DataTransmitCount);
        T38Core.t38_set_redundancy_control(
            t.T38Side.T38,
            (int)T38PacketCategory.ImageDataEnd,
            DataEndTransmitCount);
        return 0;
    }

    public static T38GatewayState? t38_gateway_init(
        T38GatewayState? s,
        T38TxPacketHandler? tx_packet_handler,
        object? tx_packet_user_data) {
        if (tx_packet_handler is null)
            return null;

        s ??= new T38GatewayState();

        // Equivalent to memset(s, 0, sizeof(*s)) in t38_gateway_init().
        s.Logging.Sink = null;
        Array.Clear(s.T38Side.SuppressNsxLength);
        Array.Clear(s.T38Side.SuppressNsxString[0]);
        Array.Clear(s.T38Side.SuppressNsxString[1]);
        Array.Clear(s.T38Side.CorruptCurrentFrame);
        s.T38Side.CurrentRxFieldClass = T38FieldClass.None;
        s.T38Side.InProgressRxIndicator = (int)T38Indicator.NoSignal;
        s.T38Side.CurrentTxDataType = T38DataType.V21;

        T38GatewayCoreState gatewayCore = s.Core;
        gatewayCore.SupportedModems = T38GatewaySupportedModems.None;
        gatewayCore.EcmAllowed = false;
        gatewayCore.ShortTrain = false;
        gatewayCore.ImageDataMode = false;
        gatewayCore.MinimumRowBits = 0;
        gatewayCore.CountPageOnMcf = false;
        gatewayCore.PagesConfirmed = 0;
        gatewayCore.EcmMode = false;
        gatewayCore.FastBitRate = 0;
        gatewayCore.FastRxModem = T38GatewayModemType.None;
        gatewayCore.FastRxActive = T38GatewayModemType.None;
        gatewayCore.TimedMode = 0;
        gatewayCore.SamplesToTimeout = 0;
        gatewayCore.RealTimeFrameHandler = null;
        gatewayCore.RealTimeFrameUserData = null;

        Array.Clear(gatewayCore.ToT38.Data);
        gatewayCore.ToT38.DataPointer = 0;
        gatewayCore.ToT38.BitStream = 0;
        gatewayCore.ToT38.BitsAbsorbed = 0;
        gatewayCore.ToT38.BitNumber = 0;
        gatewayCore.ToT38.Crc = 0;
        gatewayCore.ToT38.FillBitRemoval = false;
        gatewayCore.ToT38.OctetsPerDataPacket = 0;
        gatewayCore.ToT38.InputBits = 0;
        gatewayCore.ToT38.OutputOctets = 0;

        gatewayCore.HdlcToModem.Input = 0;
        gatewayCore.HdlcToModem.Output = 0;
        foreach (T38GatewayHdlcBuffer buffer in gatewayCore.HdlcToModem.Buffers) {
            Array.Clear(buffer.Buffer);
            buffer.Clear();
        }

        Array.Clear(gatewayCore.HdlcReceive.Buffer);
        gatewayCore.HdlcReceive.RawBitStream = 0;
        gatewayCore.HdlcReceive.Length = 0;
        gatewayCore.HdlcReceive.NumberOfBits = 0;
        gatewayCore.HdlcReceive.FlagsSeen = 0;
        gatewayCore.HdlcReceive.FramingOkThreshold = 0;
        gatewayCore.HdlcReceive.FramingOkAnnounced = false;
        gatewayCore.HdlcReceive.ByteInProgress = 0;
        gatewayCore.HdlcReceive.ReceiveAborts = 0;
        gatewayCore.HdlcReceive.ReceiveCrcErrors = 0;
        gatewayCore.HdlcReceive.ReceiveFrames = 0;
        gatewayCore.HdlcReceive.ReceiveBytes = 0;
        gatewayCore.HdlcReceive.ReceiveLengthErrors = 0;

        t38_gateway_audio_init(s);
        t38_gateway_t38_init(s, tx_packet_handler, tx_packet_user_data);

        s.Modems.SetReceiveActive(true);
        t38_gateway_set_supported_modems(
            s,
            (int)(T38GatewaySupportedModems.V27Ter |
                  T38GatewaySupportedModems.V29 |
                  T38GatewaySupportedModems.V17));

        byte[] suppression = { 0xFF, 0x00, 0x00 };
        t38_gateway_set_nsx_suppression(
            s,
            suppression,
            3,
            suppression,
            3);

        s.Core.ToT38.OctetsPerDataPacket = 1;
        s.Core.EcmAllowed = true;
        s.Core.HdlcReceive.FramingOkThreshold = HdlcFramingOkThreshold;
        s.Core.NonEcmToModem =
            T38NonEcmBuffer.t38_non_ecm_buffer_init(
                s.Core.NonEcmToModem,
                false,
                0);

        restart_rx_modem(s);
        s.Core.TimedMode = TimedModeStartup;
        s.Core.SamplesToTimeout = 1;
        return s;
    }

    public static int t38_gateway_release(T38GatewayState state) {
        ArgumentNullException.ThrowIfNull(state);
        return 0;
    }

    public static int t38_gateway_free(T38GatewayState? state) {
        _ = state;
        return 0;
    }

    public static int t38_gateway_rx(
        T38GatewayState state,
        short[] samples,
        int length) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(samples);
        if ((uint)length > (uint)samples.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        Span<short> amplitude = samples.AsSpan(0, length);
        update_rx_timing(state, length);
        for (int index = 0; index < length; index++)
            amplitude[index] = state.Modems.RestoreDc(amplitude[index]);
        state.Modems.ProcessReceive(amplitude);
        return 0;
    }



    public static int t38_gateway_rx_fillin(T38GatewayState state, int length) {
        ArgumentNullException.ThrowIfNull(state);
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));
        update_rx_timing(state, length);
        state.Modems.ProcessReceiveFillIn(length);
        return 0;
    }

    public static int t38_gateway_tx(
        T38GatewayState state,
        short[] samples,
        int maximumLength) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(samples);
        if ((uint)maximumLength > (uint)samples.Length)
            throw new ArgumentOutOfRangeException(nameof(maximumLength));

        Span<short> amplitude = samples.AsSpan(0, maximumLength);
        int length = Math.Clamp(state.Modems.GenerateTransmit(amplitude), 0, maximumLength);
        if (length < maximumLength && set_next_tx_type(state) != 0) {
            length += Math.Clamp(
                state.Modems.GenerateTransmit(amplitude[length..]),
                0,
                maximumLength - length);
            if (length < maximumLength) {
                state.Modems.ConfigureTransmitPause(0);
                set_next_tx_type(state);
            }
        }

        if (state.Modems.TransmitOnIdle) {
            amplitude[length..].Clear();
            length = maximumLength;
        }
        return length;
    }



    public static void t38_gateway_get_transfer_statistics(
        T38GatewayState state,
        T38GatewayStatistics statistics) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(statistics);
        statistics.BitRate = state.Core.FastBitRate;
        statistics.ErrorCorrectingMode = state.Core.EcmMode;
        statistics.PagesTransferred = state.Core.PagesConfirmed;
    }

    public static T38CoreState t38_gateway_get_t38_core_state(T38GatewayState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.T38Side.T38;
    }

    public static T38Log t38_gateway_get_logging_state(T38GatewayState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Logging;
    }

    public static void t38_gateway_set_ecm_capability(
        T38GatewayState state,
        bool enabled) {
        ArgumentNullException.ThrowIfNull(state);
        state.Core.EcmAllowed = enabled;
    }

    public static void t38_gateway_set_transmit_on_idle(
        T38GatewayState state,
        bool enabled) {
        ArgumentNullException.ThrowIfNull(state);
        state.Modems.TransmitOnIdle = enabled;
    }

    public static void t38_gateway_set_supported_modems(
        T38GatewayState state,
        int supported_modems) {
        ArgumentNullException.ThrowIfNull(state);
        state.Core.SupportedModems = (T38GatewaySupportedModems)supported_modems;

        int fastest = (supported_modems & (int)T38GatewaySupportedModems.V17) != 0
            ? 14_400
            : (supported_modems & (int)T38GatewaySupportedModems.V29) != 0
                ? 9_600
                : 4_800;
        T38Core.t38_set_fastest_image_data_rate(state.T38Side.T38, fastest);
    }

    public static void t38_gateway_set_nsx_suppression(
        T38GatewayState state,
        byte[]? fromT38,
        int fromT38Length,
        byte[]? fromModem,
        int fromModemLength) {
        ArgumentNullException.ThrowIfNull(state);
        if (fromT38Length >= 0)
            state.T38Side.SuppressNsxLength[0] =
                Math.Min(fromT38Length, MAX_NSX_SUPPRESSION) + 3;
        if (fromT38 is not null) {
            int payloadLength = fromT38Length >= 0
                ? Math.Min(fromT38Length, MAX_NSX_SUPPRESSION)
                : Math.Clamp(
                    state.T38Side.SuppressNsxLength[0] - 3,
                    0,
                    MAX_NSX_SUPPRESSION);
            Array.Clear(state.T38Side.SuppressNsxString[0]);
            Array.Copy(
                fromT38,
                state.T38Side.SuppressNsxString[0],
                Math.Min(payloadLength, fromT38.Length));
        }

        if (fromModemLength >= 0)
            state.T38Side.SuppressNsxLength[1] =
                Math.Min(fromModemLength, MAX_NSX_SUPPRESSION) + 3;
        if (fromModem is not null) {
            int payloadLength = fromModemLength >= 0
                ? Math.Min(fromModemLength, MAX_NSX_SUPPRESSION)
                : Math.Clamp(
                    state.T38Side.SuppressNsxLength[1] - 3,
                    0,
                    MAX_NSX_SUPPRESSION);
            Array.Clear(state.T38Side.SuppressNsxString[1]);
            Array.Copy(
                fromModem,
                state.T38Side.SuppressNsxString[1],
                Math.Min(payloadLength, fromModem.Length));
        }
    }

    public static void t38_gateway_set_tep_mode(
        T38GatewayState state,
        bool enabled) {
        ArgumentNullException.ThrowIfNull(state);
        state.Modems.SetTepMode(enabled);
    }

    public static void t38_gateway_set_fill_bit_removal(
        T38GatewayState state,
        bool enabled) {
        ArgumentNullException.ThrowIfNull(state);
        state.Core.ToT38.FillBitRemoval = enabled;
    }

    public static void t38_gateway_set_real_time_frame_handler(
        T38GatewayState state,
        T38GatewayRealTimeFrameHandler? handler,
        object? userData) {
        ArgumentNullException.ThrowIfNull(state);
        state.Core.RealTimeFrameHandler = handler;
        state.Core.RealTimeFrameUserData = userData;
    }

    /// <summary>
    /// Supplies one demodulated HDLC bit or a negative SignalStatus value.
    /// This is the raw V.21/fast-modem callback installed in FaxModems.
    /// </summary>
    private static void t38_hdlc_rx_put_bit(T38GatewayState state, int newBit) {
        ArgumentNullException.ThrowIfNull(state);
        T38GatewayHdlcReceiveState receiver = state.Core.HdlcReceive;

        if (newBit < 0) {
            hdlc_rx_status(state, newBit);
            return;
        }

        receiver.RawBitStream = (receiver.RawBitStream << 1) | (uint)(newBit & 1);
        if ((receiver.RawBitStream & 0x3E) == 0x3E) {
            if ((receiver.RawBitStream & 0x41) == 0)
                return;

            if ((receiver.RawBitStream & 0xFE) == 0x7E) {
                rx_flag_or_abort(state);
                return;
            }
        }

        receiver.NumberOfBits++;
        if (receiver.FlagsSeen < receiver.FramingOkThreshold)
            return;

        receiver.ByteInProgress =
            (receiver.ByteInProgress >> 1) |
            (int)((receiver.RawBitStream & 1) << 7);

        if (receiver.NumberOfBits != 8)
            return;

        receiver.NumberOfBits = 0;
        T38GatewayToT38State toT38 = state.Core.ToT38;
        if (receiver.Length >= receiver.Buffer.Length) {
            if (receiver.Length + HdlcTransmissionLagOctets >= toT38.OctetsPerDataPacket) {
                int category = state.T38Side.CurrentTxDataType == T38DataType.V21
                    ? (int)T38PacketCategory.ControlData
                    : (int)T38PacketCategory.ImageData;
                if (T38Core.t38_core_send_data(
                        state.T38Side.T38,
                        (int)state.T38Side.CurrentTxDataType,
                        (int)T38FieldType.HdlcFcsBad,
                        Array.Empty<byte>(),
                        0,
                        category) < 0) {
                    state.Logging.Warning("T.38 send failed");
                }
            }

            receiver.ReceiveLengthErrors++;
            receiver.FlagsSeen = receiver.FramingOkThreshold - 1;
            receiver.Length = 0;
            return;
        }

        receiver.Buffer[receiver.Length] = (byte)receiver.ByteInProgress;
        if (receiver.Length == 1 &&
            (receiver.Buffer[0] != 0xFF || (receiver.Buffer[1] & 0xEF) != 0x03)) {
            state.Logging.Flow("Bad HDLC frame header. Abandoning frame.");
            receiver.FlagsSeen = receiver.FramingOkThreshold - 1;
            receiver.Length = 0;
            return;
        }

        toT38.Crc = CrcItu16Update(receiver.Buffer[receiver.Length], toT38.Crc);
        receiver.Length++;
        if (receiver.Length <= HdlcTransmissionLagOctets)
            return;

        if (state.T38Side.CurrentTxDataType == T38DataType.V21)
            edit_control_messages(state, true, receiver.Buffer, receiver.Length);

        toT38.DataPointer++;
        if (toT38.DataPointer >= Math.Max(1, toT38.OctetsPerDataPacket)) {
            int start = receiver.Length - HdlcTransmissionLagOctets - toT38.DataPointer;
            BitReverse(
                toT38.Data.AsSpan(0, toT38.DataPointer),
                receiver.Buffer.AsSpan(start, toT38.DataPointer));
            int category = state.T38Side.CurrentTxDataType == T38DataType.V21
                ? (int)T38PacketCategory.ControlData
                : (int)T38PacketCategory.ImageData;
            if (T38Core.t38_core_send_data(
                    state.T38Side.T38,
                    (int)state.T38Side.CurrentTxDataType,
                    (int)T38FieldType.HdlcData,
                    toT38.Data,
                    toT38.DataPointer,
                    category) < 0) {
                state.Logging.Warning("T.38 send failed");
            }
            toT38.DataPointer = 0;
        }
    }

    private static int process_rx_missing(
        T38CoreState core,
        object? userData,
        int sequenceNumber,
        int expectedSequenceNumber) {
        _ = core;
        _ = sequenceNumber;
        _ = expectedSequenceNumber;
        if (userData is not T38GatewayState state)
            return -1;
        state.Core.HdlcToModem.Buffers[state.Core.HdlcToModem.Input].Flags |=
            HdlcFlagMissingData;
        return 0;
    }

    private static int process_rx_indicator(
        T38CoreState core,
        object? userData,
        T38Indicator indicator) {
        if (userData is not T38GatewayState state)
            return -1;
        T38NonEcmBuffer.t38_non_ecm_buffer_report_input_status(
            state.Core.NonEcmToModem,
            state.Logging);

        if (core.CurrentRxIndicator == (int)indicator)
            return 0;

        T38GatewayHdlcQueue queue = state.Core.HdlcToModem;
        bool immediate = queue.Input == queue.Output;
        T38GatewayHdlcBuffer current = queue.Buffers[queue.Input];
        if (current.Contents != 0) {
            queue.Input = NextQueueIndex(queue.Input);
            current = queue.Buffers[queue.Input];
        }

        current.Contents = (int)indicator | FlagIndicator;
        queue.Input = NextQueueIndex(queue.Input);

        state.Logging.Flow(
            immediate
                ? $"Changing {T38Core.t38_indicator_to_str(core.CurrentRxIndicator)} -> {T38Core.t38_indicator_to_str((int)indicator)}"
                : $"Queued change {T38Core.t38_indicator_to_str(core.CurrentRxIndicator)} -> {T38Core.t38_indicator_to_str((int)indicator)}");
        if (immediate && state.T38Side.CurrentRxFieldClass == T38FieldClass.Hdlc) {
            state.Logging.Flow("HDLC shutdown");
            state.Modems.StopHdlcTransmit();
        }

        state.T38Side.CurrentRxFieldClass = T38FieldClass.None;
        core.CurrentRxIndicator = (int)indicator;
        return 0;
    }

    private static int process_rx_data(
        T38CoreState core,
        object? userData,
        T38DataType dataType,
        T38FieldType fieldType,
        ReadOnlyMemory<byte> field) {
        if (userData is not T38GatewayState state)
            return -1;
        ReadOnlySpan<byte> data = field.Span;

        if (dataType == T38DataType.V8) {
            LogV8Field(state, fieldType, data);
            return 0;
        }

        if (dataType == T38DataType.V34PrimaryRate) {
            if (fieldType == T38FieldType.V34Rate) {
                if (data.Length >= 3) {
                    int rate = T38Core.t38_v34rate_to_bps(data.ToArray(), data.Length);
                    state.T38Side.T38.V34Rate = rate;
                    state.Logging.Flow($"V.34 rate {rate} bps");
                } else {
                    state.Logging.Flow($"Bad length for V34rate message - {data.Length}");
                }
            }
            return 0;
        }

        T38GatewayHdlcBuffer hdlcBuffer =
            state.Core.HdlcToModem.Buffers[state.Core.HdlcToModem.Input];

        switch (fieldType) {
            case T38FieldType.HdlcData:
                state.T38Side.CurrentRxFieldClass = T38FieldClass.Hdlc;
                if (hdlcBuffer.Contents != ((int)dataType | FlagData)) {
                    queue_missing_indicator(state, dataType);
                    if (data.Length == 0 || data[0] != 0xFF)
                        hdlcBuffer.Flags |= HdlcFlagMissingData;
                }
                if (data.Length > 0)
                    process_hdlc_data(state, dataType, data, data.Length);
                break;

            case T38FieldType.HdlcFcsOk:
            case T38FieldType.HdlcFcsBad:
            case T38FieldType.HdlcFcsOkSignalEnd:
            case T38FieldType.HdlcFcsBadSignalEnd:
                state.T38Side.CurrentRxFieldClass = T38FieldClass.Hdlc;
                if (data.Length > 0) {
                    state.Logging.Warning($"There is data in {fieldType}.");
                    process_hdlc_data(state, dataType, data, data.Length);
                }
                hdlcBuffer = state.Core.HdlcToModem.Buffers[state.Core.HdlcToModem.Input];
                if (hdlcBuffer.Length > 0) {
                    if (hdlcBuffer.Contents != ((int)dataType | FlagData)) {
                        queue_missing_indicator(state, dataType);
                        hdlcBuffer = state.Core.HdlcToModem.Buffers[state.Core.HdlcToModem.Input];
                    }

                    bool good = fieldType is T38FieldType.HdlcFcsOk or
                        T38FieldType.HdlcFcsOkSignalEnd;
                    if (dataType == T38DataType.V21 &&
                        good &&
                        (hdlcBuffer.Flags & HdlcFlagMissingData) == 0) {
                        monitor_control_messages(
                            state,
                            false,
                            hdlcBuffer.Buffer,
                            hdlcBuffer.Length);
                        state.Core.RealTimeFrameHandler?.Invoke(
                            state.Core.RealTimeFrameUserData,
                            false,
                            hdlcBuffer.Buffer.AsMemory(0, hdlcBuffer.Length));
                    } else if (dataType != T38DataType.V21 && good) {
                        state.Core.ShortTrain = true;
                    }

                    hdlcBuffer.Contents = (int)dataType | FlagData;
                    finalise_hdlc_frame(state, good);
                } else {
                    hdlcBuffer.Contents = 0;
                }

                if (fieldType is T38FieldType.HdlcFcsOkSignalEnd or
                    T38FieldType.HdlcFcsBadSignalEnd) {
                    if (core.CurrentRxDataType != (int)dataType ||
                        core.CurrentRxFieldType != (int)fieldType) {
                        queue_missing_indicator(state, T38DataType.None);
                        state.T38Side.CurrentRxFieldClass = T38FieldClass.None;
                    }
                }
                state.T38Side.CorruptCurrentFrame[0] = false;
                break;

            case T38FieldType.HdlcSignalEnd:
                if (data.Length > 0)
                    state.Logging.Warning("There is data in T38_FIELD_HDLC_SIG_END.");
                if (core.CurrentRxDataType != (int)dataType ||
                    core.CurrentRxFieldType != (int)fieldType) {
                    if (hdlcBuffer.Contents != ((int)dataType | FlagData)) {
                        queue_missing_indicator(state, dataType);
                        hdlcBuffer = state.Core.HdlcToModem.Buffers[state.Core.HdlcToModem.Input];
                    }
                    if (state.T38Side.CurrentRxFieldClass == T38FieldClass.NonEcm) {
                        state.Logging.Warning(
                            "T38_FIELD_HDLC_SIG_END received at the end of non-ECM data.");
                        T38NonEcmBuffer.t38_non_ecm_buffer_push(state.Core.NonEcmToModem);
                    } else {
                        hdlcBuffer.Clear();
                    }
                    queue_missing_indicator(state, T38DataType.None);
                    state.T38Side.CurrentRxFieldClass = T38FieldClass.None;
                }
                state.T38Side.CorruptCurrentFrame[0] = false;
                break;

            case T38FieldType.T4NonEcmData:
                if (state.T38Side.CurrentRxFieldClass == T38FieldClass.None) {
                    T38NonEcmBuffer.t38_non_ecm_buffer_set_mode(
                        state.Core.NonEcmToModem,
                        state.Core.ImageDataMode,
                        state.Core.MinimumRowBits);
                }
                state.T38Side.CurrentRxFieldClass = T38FieldClass.NonEcm;
                if (hdlcBuffer.Contents != ((int)dataType | FlagData))
                    queue_missing_indicator(state, dataType);
                if (data.Length > 0)
                    T38NonEcmBuffer.t38_non_ecm_buffer_inject(
                        state.Core.NonEcmToModem,
                        data.ToArray(),
                        data.Length);
                state.T38Side.CorruptCurrentFrame[0] = false;
                break;

            case T38FieldType.T4NonEcmSignalEnd:
                if (state.T38Side.CurrentRxFieldClass == T38FieldClass.None) {
                    T38NonEcmBuffer.t38_non_ecm_buffer_set_mode(
                        state.Core.NonEcmToModem,
                        state.Core.ImageDataMode,
                        state.Core.MinimumRowBits);
                }

                if (core.CurrentRxDataType != (int)dataType ||
                    core.CurrentRxFieldType != (int)fieldType) {
                    if (state.T38Side.CurrentRxFieldClass == T38FieldClass.NonEcm) {
                        if (data.Length > 0) {
                            if (hdlcBuffer.Contents != ((int)dataType | FlagData)) {
                                queue_missing_indicator(state, dataType);
                                hdlcBuffer = state.Core.HdlcToModem.Buffers[state.Core.HdlcToModem.Input];
                            }
                            T38NonEcmBuffer.t38_non_ecm_buffer_inject(
                                state.Core.NonEcmToModem,
                                data.ToArray(),
                                data.Length);
                        }
                        if (hdlcBuffer.Contents != ((int)dataType | FlagData))
                            queue_missing_indicator(state, dataType);
                        T38NonEcmBuffer.t38_non_ecm_buffer_push(state.Core.NonEcmToModem);
                    } else {
                        state.Logging.Warning(
                            "T38_FIELD_T4_NON_ECM_SIG_END received at the end of HDLC data.");
                        if (hdlcBuffer.Contents != ((int)dataType | FlagData)) {
                            queue_missing_indicator(state, dataType);
                            hdlcBuffer = state.Core.HdlcToModem.Buffers[state.Core.HdlcToModem.Input];
                        }
                        hdlcBuffer.Clear();
                    }
                    queue_missing_indicator(state, T38DataType.None);
                    state.T38Side.CurrentRxFieldClass = T38FieldClass.None;
                }
                state.T38Side.CorruptCurrentFrame[0] = false;
                break;
        }

        return 0;
    }

    private static void process_hdlc_data(
        T38GatewayState state,
        T38DataType dataType,
        ReadOnlySpan<byte> data,
        int length) {
        T38GatewayHdlcBuffer buffer =
            state.Core.HdlcToModem.Buffers[state.Core.HdlcToModem.Input];

        if (buffer.Length + length > buffer.Buffer.Length) {
            buffer.Flags |= HdlcFlagMissingData;
            return;
        }

        buffer.Contents = (int)dataType | FlagData;
        BitReverse(
            buffer.Buffer.AsSpan(buffer.Length, length),
            data[..length]);

        if (dataType == T38DataType.V21) {
            for (int index = 1; index <= length; index++)
                edit_control_messages(state, false, buffer.Buffer, buffer.Length + index);

            if (buffer.Length + length >= HdlcStartBufferLevel) {
                if (state.Core.HdlcToModem.Input == state.Core.HdlcToModem.Output) {
                    bool append = (buffer.Flags & HdlcFlagProceedWithOutput) != 0;
                    int start = append ? buffer.Length : 0;
                    int count = append ? length : buffer.Length + length;
                    state.Modems.StartHdlcTransmit(
                        buffer.Buffer.AsMemory(start, count),
                        append);
                }
                buffer.Flags |= HdlcFlagProceedWithOutput;
            }
        }

        buffer.Length += length;
    }

    private static void finalise_hdlc_frame(T38GatewayState state, bool goodFcs) {
        T38GatewayHdlcQueue queue = state.Core.HdlcToModem;
        T38GatewayHdlcBuffer buffer = queue.Buffers[queue.Input];
        if (!goodFcs || (buffer.Flags & HdlcFlagMissingData) != 0)
            buffer.Flags |= HdlcFlagCorruptCrc;

        if (queue.Input == queue.Output) {
            if ((buffer.Flags & HdlcFlagProceedWithOutput) == 0) {
                state.Modems.StartHdlcTransmit(
                    buffer.Buffer.AsMemory(0, buffer.Length),
                    false);
            }
            if ((buffer.Flags & HdlcFlagCorruptCrc) != 0)
                state.Modems.CorruptHdlcTransmit();
        }

        buffer.Flags |= HdlcFlagProceedWithOutput | HdlcFlagFinished;
        queue.Input = NextQueueIndex(queue.Input);
        queue.Buffers[queue.Input].Clear();
    }

    private static void hdlc_underflow_handler(T38GatewayState state) {
        T38GatewayHdlcQueue queue = state.Core.HdlcToModem;
        T38GatewayHdlcBuffer current = queue.Buffers[queue.Output];
        if ((current.Flags & HdlcFlagProceedWithOutput) == 0)
            return;

        current.Clear();
        queue.Output = NextQueueIndex(queue.Output);
        T38GatewayHdlcBuffer next = queue.Buffers[queue.Output];

        if ((next.Contents & FlagIndicator) != 0) {
            state.Modems.StopHdlcTransmit();
        } else if ((next.Contents & FlagData) != 0 &&
                   (next.Flags & HdlcFlagProceedWithOutput) != 0) {
            state.Modems.StartHdlcTransmit(
                next.Buffer.AsMemory(0, next.Length),
                false);
            if ((next.Flags & HdlcFlagCorruptCrc) != 0)
                state.Modems.CorruptHdlcTransmit();
        }
    }

    private static int set_next_tx_type(T38GatewayState state) {
        T38NonEcmBuffer.t38_non_ecm_buffer_report_output_status(
            state.Core.NonEcmToModem,
            state.Logging);

        if (state.Modems.HasNextTransmitHandler) {
            bool receiveActive = state.Modems.NextTransmitIsSilence;
            state.Modems.SetNextTransmitType();
            state.Modems.SetReceiveActive(receiveActive);
            return 1;
        }

        T38GatewayHdlcQueue queue = state.Core.HdlcToModem;
        if (queue.Output == queue.Input)
            return 0;

        T38GatewayHdlcBuffer item = queue.Buffers[queue.Output];
        if ((item.Contents & FlagIndicator) == 0)
            return 0;

        T38Indicator indicator = (T38Indicator)(item.Contents & 0xFF);
        item.Clear();
        queue.Output = NextQueueIndex(queue.Output);

        bool useHdlc = state.Core.ImageDataMode && state.Core.EcmMode;
        (int bitRate, bool shortTrain) = IndicatorModemParameters(indicator);
        ConfigureGatewayTransmitter(
            state,
            indicator,
            bitRate,
            shortTrain,
            useHdlc);
        state.T38Side.InProgressRxIndicator = (int)indicator;
        return 1;
    }

    private static void ConfigureGatewayTransmitter(
        T38GatewayState state,
        T38Indicator indicator,
        int bitRate,
        bool shortTrain,
        bool useHdlc) {
        FaxModems modems = state.Modems;

        if (useHdlc) {
            modems.InitializeHdlcTransmitter(progressive: true);
        } else {
            modems.SetGetBit(
                () => T38NonEcmBuffer.t38_non_ecm_buffer_get_bit(
                    state.Core.NonEcmToModem));
        }

        switch (indicator) {
            case T38Indicator.NoSignal:
                modems.TxBitRate = 0;
                modems.ConfigureTransmitPause(0);
                modems.SetReceiveActive(true);
                break;

            case T38Indicator.Cng:
                modems.TxBitRate = 0;
                modems.ConfigureTransmitTone(
                    FaxModemChannel.CngToneTx,
                    continueWithSilence: true);
                modems.SetReceiveActive(true);
                break;

            case T38Indicator.Ced:
                modems.TxBitRate = 0;
                modems.ConfigureTransmitTone(FaxModemChannel.CedToneTx);
                modems.SetReceiveActive(true);
                break;

            case T38Indicator.V21Preamble:
                modems.TxBitRate = 300;
                modems.InitializeHdlcTransmitter(progressive: true);
                state.Core.HdlcToModem.Buffers[
                    state.Core.HdlcToModem.Input].Length = 0;
                modems.ConfigureTransmitV21(
                    preambleFlags: 32,
                    pauseSamples: MillisecondsToSamples(75));
                modems.SetReceiveActive(true);
                break;

            case T38Indicator.V27Ter2400Training:
            case T38Indicator.V27Ter4800Training:
                ConfigureGatewayFastTransmitter(
                    modems,
                    FaxModemChannel.V27TerTx,
                    bitRate,
                    shortTrain,
                    useHdlc);
                break;

            case T38Indicator.V29_7200Training:
            case T38Indicator.V29_9600Training:
                ConfigureGatewayFastTransmitter(
                    modems,
                    FaxModemChannel.V29Tx,
                    bitRate,
                    shortTrain,
                    useHdlc);
                break;

            case T38Indicator.V17_7200ShortTraining:
            case T38Indicator.V17_7200LongTraining:
            case T38Indicator.V17_9600ShortTraining:
            case T38Indicator.V17_9600LongTraining:
            case T38Indicator.V17_12000ShortTraining:
            case T38Indicator.V17_12000LongTraining:
            case T38Indicator.V17_14400ShortTraining:
            case T38Indicator.V17_14400LongTraining:
                ConfigureGatewayFastTransmitter(
                    modems,
                    FaxModemChannel.V17Tx,
                    bitRate,
                    shortTrain,
                    useHdlc);
                break;

            case T38Indicator.V8Ansam:
            case T38Indicator.V8Signal:
            case T38Indicator.V34ControlChannel1200:
            case T38Indicator.V34PrimaryChannel:
            case T38Indicator.V34ControlChannelRetrain:
            case T38Indicator.V33_12000Training:
            case T38Indicator.V33_14400Training:
                modems.TxBitRate = bitRate;
                break;

            default:
                break;
        }
    }

    private static void ConfigureGatewayFastTransmitter(
        FaxModems modems,
        FaxModemChannel channel,
        int bitRate,
        bool shortTrain,
        bool useHdlc) {
        int preambleFlags = bitRate > 300 ? bitRate / (8 * 5) : 0;
        modems.ConfigureTransmitFast(
            channel,
            bitRate,
            shortTrain,
            useHdlc,
            preambleFlags,
            MillisecondsToSamples(75));
        modems.SetReceiveActive(true);
    }

    private static (int BitRate, bool ShortTrain) IndicatorModemParameters(
        T38Indicator indicator) {
        return indicator switch {
            T38Indicator.V21Preamble => (300, false),
            T38Indicator.V27Ter2400Training => (2_400, false),
            T38Indicator.V27Ter4800Training => (4_800, false),
            T38Indicator.V29_7200Training => (7_200, false),
            T38Indicator.V29_9600Training => (9_600, false),
            T38Indicator.V17_7200ShortTraining => (7_200, true),
            T38Indicator.V17_7200LongTraining => (7_200, false),
            T38Indicator.V17_9600ShortTraining => (9_600, true),
            T38Indicator.V17_9600LongTraining => (9_600, false),
            T38Indicator.V17_12000ShortTraining => (12_000, true),
            T38Indicator.V17_12000LongTraining => (12_000, false),
            T38Indicator.V17_14400ShortTraining => (14_400, true),
            T38Indicator.V17_14400LongTraining => (14_400, false),
            T38Indicator.V8Ansam => (300, false),
            T38Indicator.V8Signal => (300, false),
            T38Indicator.V34ControlChannel1200 => (1_200, false),
            T38Indicator.V34PrimaryChannel => (33_600, false),
            T38Indicator.V33_12000Training => (12_000, false),
            T38Indicator.V33_14400Training => (14_400, false),
            _ => (0, false)
        };
    }

    private static void edit_control_messages(
        T38GatewayState state,
        bool fromModem,
        byte[] buffer,
        int length) {
        if (length <= 0 || length > buffer.Length)
            return;

        int direction = fromModem ? 1 : 0;
        if (state.T38Side.CorruptCurrentFrame[direction]) {
            if (length <= state.T38Side.SuppressNsxLength[direction]) {
                int replacementIndex = length - 4;
                if ((uint)replacementIndex <
                    (uint)state.T38Side.SuppressNsxString[direction].Length) {
                    buffer[length - 1] =
                        state.T38Side.SuppressNsxString[direction][replacementIndex];
                }
            }
            return;
        }

        switch (length) {
            case 3:
                if ((buffer[2] is T30Nsf or T30Nsc or T30Nss) &&
                    state.T38Side.SuppressNsxLength[direction] != 0) {
                    state.Logging.Flow("Corrupting NSX message to prevent recognition");
                    state.T38Side.CorruptCurrentFrame[direction] = true;
                }
                break;

            case 4:
                if (buffer[2] == T30Dis)
                    buffer[3] &= unchecked((byte)~DisBit6);
                break;

            case 5:
                if (buffer[2] == T30Dis)
                    ApplyModemCapabilityConstraints(state, buffer);
                break;

            case 7:
                if (buffer[2] == T30Dis && !state.Core.EcmAllowed)
                    buffer[6] &= unchecked((byte)~(DisBit3 | DisBit7));
                break;
        }
    }

    private static void ApplyModemCapabilityConstraints(
        T38GatewayState state,
        byte[] buffer) {
        byte mask = (byte)(buffer[4] & (DisBit6 | DisBit5 | DisBit4 | DisBit3));
        switch (mask) {
            case 0:
            case DisBit4:
                break;

            case DisBit3:
            case DisBit4 | DisBit3:
                if (!state.Core.SupportedModems.HasFlag(T38GatewaySupportedModems.V29))
                    buffer[4] &= unchecked((byte)~DisBit3);
                break;

            case DisBit6 | DisBit4 | DisBit3:
                if (!state.Core.SupportedModems.HasFlag(T38GatewaySupportedModems.V17))
                    buffer[4] &= unchecked((byte)~DisBit6);
                if (!state.Core.SupportedModems.HasFlag(T38GatewaySupportedModems.V29))
                    buffer[4] &= unchecked((byte)~DisBit3);
                break;

            default:
                buffer[4] &= unchecked((byte)~(DisBit6 | DisBit5));
                buffer[4] |= DisBit4 | DisBit3;
                break;
        }
    }

    private static void monitor_control_messages(
        T38GatewayState state,
        bool fromModem,
        byte[] buffer,
        int length) {
        if (length < 3)
            return;

        byte fcf = buffer[2];
        state.Logging.Flow($"Monitoring FCF 0x{fcf:X2}");
        state.Core.TimedMode = TimedModeIdle;

        switch (fcf) {
            case T30Cfr:
                state.Core.ImageDataMode = true;
                state.Core.ShortTrain = true;
                if (!fromModem)
                    restart_rx_modem(state);
                break;

            case T30Ftt:
                state.Core.ImageDataMode = false;
                state.Core.ShortTrain = false;
                if (!fromModem)
                    state.Core.FastRxModem = T38GatewayModemType.None;
                break;

            case T30Rtn:
            case T30Rtp:
                state.Core.ImageDataMode = false;
                state.Core.ShortTrain = false;
                break;

            case T30Ctc:
                if (length >= 5) {
                    ModemCode code = FindModemCode(buffer[4]);
                    state.Core.FastBitRate = code.BitRate;
                    if (fromModem)
                        state.Core.FastRxModem = code.Modem;
                }
                break;

            case T30Ctr:
                state.Core.ShortTrain = false;
                break;

            case T30Dtc:
            case T30Dcs:
            case T30Dcs | 1:
                MonitorDcsOrDtc(state, fromModem, buffer, length, fcf);
                break;

            case T30Pps:
            case T30Pps | 1:
                if (length >= 4 && IsPageEndCommand((byte)(buffer[3] & 0xFE)))
                    state.Core.CountPageOnMcf = true;
                break;

            case T30Mcf:
            case T30Mcf | 1:
                if (state.Core.CountPageOnMcf) {
                    state.Core.PagesConfirmed++;
                    state.Core.CountPageOnMcf = false;
                }
                break;

            default:
                if (IsPageEndCommand((byte)(fcf & 0xFE)))
                    state.Core.CountPageOnMcf = true;
                break;
        }
    }

    private static void MonitorDcsOrDtc(
        T38GatewayState state,
        bool fromModem,
        byte[] buffer,
        int length,
        byte fcf) {
        state.Core.FastBitRate = 0;
        state.Core.FastRxModem = T38GatewayModemType.None;
        state.Core.ImageDataMode = false;
        state.Core.ShortTrain = false;
        if (fromModem)
            state.Core.TimedMode = TimedModeTcfBegin;

        if (length >= 5) {
            ModemCode code = FindModemCode(buffer[4]);
            state.Core.FastBitRate = code.BitRate;
            bool selectReceiveModem =
                (fcf == T30Dtc && !fromModem) ||
                (fcf != T30Dtc && fromModem);
            if (selectReceiveModem)
                state.Core.FastRxModem = code.Modem;
        }

        state.Core.MinimumRowBits = length >= 6
            ? state.Core.FastBitRate *
              MinimumScanLineTimes[(buffer[5] & (DisBit7 | DisBit6 | DisBit5)) >> 4] /
              1000
            : 0;
        state.Core.EcmMode = length >= 7 && (buffer[6] & DisBit3) != 0;
    }

    private static bool IsPageEndCommand(byte fcf) {
        return fcf is T30Eop or T30PriEop or T30Eom or T30PriEom or
            T30Eos or T30Mps or T30PriMps;
    }

    private static ModemCode FindModemCode(byte value) {
        byte code = (byte)(value & (DisBit6 | DisBit5 | DisBit4 | DisBit3));
        foreach (ModemCode item in ModemCodes) {
            if (item.DcsCode == code || item.BitRate == 0)
                return item;
        }
        return ModemCodes[^1];
    }

    private static void queue_missing_indicator(
        T38GatewayState state,
        T38DataType dataType) {
        (T38Indicator? expected, T38Indicator? alternate) = dataType switch {
            T38DataType.None => (T38Indicator.NoSignal, null),
            T38DataType.V21 => (T38Indicator.V21Preamble, null),
            T38DataType.V27Ter2400 => (T38Indicator.V27Ter2400Training, null),
            T38DataType.V27Ter4800 => (T38Indicator.V27Ter4800Training, null),
            T38DataType.V29_7200 => (T38Indicator.V29_7200Training, null),
            T38DataType.V29_9600 => (T38Indicator.V29_9600Training, null),
            T38DataType.V17_7200 => TrainingPair(
                state.Core.ShortTrain,
                T38Indicator.V17_7200ShortTraining,
                T38Indicator.V17_7200LongTraining),
            T38DataType.V17_9600 => TrainingPair(
                state.Core.ShortTrain,
                T38Indicator.V17_9600ShortTraining,
                T38Indicator.V17_9600LongTraining),
            T38DataType.V17_12000 => TrainingPair(
                state.Core.ShortTrain,
                T38Indicator.V17_12000ShortTraining,
                T38Indicator.V17_12000LongTraining),
            T38DataType.V17_14400 => TrainingPair(
                state.Core.ShortTrain,
                T38Indicator.V17_14400ShortTraining,
                T38Indicator.V17_14400LongTraining),
            _ => (null, null)
        };

        if (expected is null)
            return;
        if (state.T38Side.T38.CurrentRxIndicator == (int)expected.Value)
            return;
        if (alternate is not null &&
            state.T38Side.T38.CurrentRxIndicator == (int)alternate.Value)
            return;

        state.Logging.Flow(
            $"Queuing missing indicator - {T38Core.t38_indicator_to_str((int)expected.Value)}");
        process_rx_indicator(
            state.T38Side.T38,
            state,
            expected.Value);
        state.T38Side.T38.CurrentRxIndicator = (int)expected.Value;
    }

    private static (T38Indicator?, T38Indicator?) TrainingPair(
        bool shortTrain,
        T38Indicator shortIndicator,
        T38Indicator longIndicator) =>
        shortTrain
            ? (shortIndicator, longIndicator)
            : (longIndicator, shortIndicator);

    private static void non_ecm_rx_status(T38GatewayState state, int status) {
        state.Logging.Flow($"Non-ECM signal status is {status}");
        switch (status) {
            case SignalStatus.TrainingInProgress:
                if (state.Core.TimedMode == TimedModeIdle) {
                    announce_training(state);
                } else {
                    if (state.Core.TimedMode == TimedModeTcfPastV21Modem)
                        state.Core.TimedMode = TimedModeTcfFastModemSeen;
                    else
                        state.Core.SamplesToTimeout = MillisecondsToSamples(500);
                    set_fast_packetisation(state);
                }
                break;

            case SignalStatus.TrainingFailed:
                break;

            case SignalStatus.TrainingSucceeded:
                state.Modems.RxSignalPresent = true;
                state.Modems.RxTrained = true;
                state.Core.TimedMode = TimedModeIdle;
                state.Core.SamplesToTimeout = 0;
                state.Core.ShortTrain = true;
                to_t38_buffer_init(state.Core.ToT38);
                break;

            case SignalStatus.CarrierDown:
                if (IsFastDataType(state.T38Side.CurrentTxDataType)) {
                    if (state.Core.TimedMode != TimedModeTcfFastModemAnnounced) {
                        non_ecm_push_residue(state);
                        T38Core.t38_core_send_indicator(
                            state.T38Side.T38,
                            (int)T38Indicator.NoSignal);
                    }
                    restart_rx_modem(state);
                }
                break;

            case SignalStatus.CarrierUp:
                break;

            default:
                state.Logging.Warning($"Unexpected non-ECM special bit - {status}!");
                break;
        }
    }

    private static void non_ecm_put_bit(T38GatewayState state, int bit) {
        if (bit < 0) {
            non_ecm_rx_status(state, bit);
            return;
        }

        T38GatewayToT38State toT38 = state.Core.ToT38;
        toT38.InputBits++;
        toT38.BitStream = (toT38.BitStream << 1) | (uint)(bit & 1);
        toT38.BitNumber++;
        if (toT38.BitNumber >= 8) {
            toT38.Data[toT38.DataPointer++] = (byte)toT38.BitStream;
            if (toT38.DataPointer >= Math.Max(1, toT38.OctetsPerDataPacket))
                non_ecm_push(state);
            toT38.BitNumber = 0;
        }
    }

    private static void non_ecm_remove_fill_and_put_bit(T38GatewayState state, int bit) {
        if (bit < 0) {
            non_ecm_rx_status(state, bit);
            return;
        }

        T38GatewayToT38State toT38 = state.Core.ToT38;
        toT38.BitsAbsorbed++;
        bit &= 1;
        if ((toT38.BitStream & 0x3FFF) == 0 && bit == 0) {
            if (toT38.BitsAbsorbed > 16 * Math.Max(1, toT38.OctetsPerDataPacket))
                non_ecm_push(state);
            return;
        }

        toT38.BitStream = (toT38.BitStream << 1) | (uint)bit;
        toT38.BitNumber++;
        if (toT38.BitNumber >= 8) {
            toT38.Data[toT38.DataPointer++] = (byte)toT38.BitStream;
            if (toT38.DataPointer >= Math.Max(1, toT38.OctetsPerDataPacket))
                non_ecm_push(state);
            toT38.BitNumber = 0;
        }
    }

    private static void non_ecm_push(T38GatewayState state) {
        T38GatewayToT38State toT38 = state.Core.ToT38;
        if (toT38.DataPointer <= 0)
            return;

        int result = T38Core.t38_core_send_data(
            state.T38Side.T38,
            (int)state.T38Side.CurrentTxDataType,
            (int)T38FieldType.T4NonEcmData,
            toT38.Data,
            toT38.DataPointer,
            (int)T38PacketCategory.ImageData);
        if (result < 0)
            state.Logging.Warning("T.38 send failed");

        toT38.InputBits += toT38.BitsAbsorbed;
        toT38.OutputOctets += toT38.DataPointer;
        toT38.BitsAbsorbed = 0;
        toT38.DataPointer = 0;
    }

    private static void non_ecm_push_residue(T38GatewayState state) {
        T38GatewayToT38State toT38 = state.Core.ToT38;
        if (toT38.BitNumber != 0) {
            toT38.Data[toT38.DataPointer++] =
                (byte)(toT38.BitStream << (8 - toT38.BitNumber));
        }

        int result = T38Core.t38_core_send_data(
            state.T38Side.T38,
            (int)state.T38Side.CurrentTxDataType,
            (int)T38FieldType.T4NonEcmSignalEnd,
            toT38.Data,
            toT38.DataPointer,
            (int)T38PacketCategory.ImageDataEnd);
        if (result < 0)
            state.Logging.Warning("T.38 send failed");

        toT38.InputBits += toT38.BitsAbsorbed;
        toT38.OutputOctets += toT38.DataPointer;
        toT38.DataPointer = 0;
        toT38.BitNumber = 0;
    }

    private static void hdlc_rx_status(T38GatewayState state, int status) {
        T38GatewayHdlcReceiveState receiver = state.Core.HdlcReceive;
        state.Logging.Flow($"HDLC signal status is {status}");
        switch (status) {
            case SignalStatus.TrainingInProgress:
                announce_training(state);
                break;

            case SignalStatus.TrainingFailed:
                break;

            case SignalStatus.TrainingSucceeded:
                state.Modems.RxSignalPresent = true;
                state.Modems.RxTrained = true;
                state.Core.ShortTrain = true;
                receiver.FramingOkAnnounced = true;
                to_t38_buffer_init(state.Core.ToT38);
                break;

            case SignalStatus.CarrierUp:
                receiver.RawBitStream = 0;
                receiver.Length = 0;
                receiver.NumberOfBits = 0;
                receiver.FlagsSeen = 0;
                receiver.FramingOkAnnounced = false;
                to_t38_buffer_init(state.Core.ToT38);
                break;

            case SignalStatus.CarrierDown:
                if (receiver.FramingOkAnnounced) {
                    int category = state.T38Side.CurrentTxDataType == T38DataType.V21
                        ? (int)T38PacketCategory.ControlDataEnd
                        : (int)T38PacketCategory.ImageDataEnd;
                    if (T38Core.t38_core_send_data(
                            state.T38Side.T38,
                            (int)state.T38Side.CurrentTxDataType,
                            (int)T38FieldType.HdlcSignalEnd,
                            Array.Empty<byte>(),
                            0,
                            category) < 0) {
                        state.Logging.Warning("T.38 send failed");
                    }
                    T38Core.t38_core_send_indicator(
                        state.T38Side.T38,
                        (int)T38Indicator.NoSignal);
                    receiver.FramingOkAnnounced = false;
                }

                restart_rx_modem(state);
                if (state.Core.TimedMode == TimedModeTcfBegin) {
                    state.Core.SamplesToTimeout = MillisecondsToSamples(75);
                    state.Core.TimedMode = TimedModeTcfPastV21Modem;
                }
                break;

            default:
                state.Logging.Warning($"Unexpected HDLC special bit - {status}!");
                break;
        }
    }

    private static void rx_flag_or_abort(T38GatewayState state) {
        T38GatewayHdlcReceiveState receiver = state.Core.HdlcReceive;
        T38GatewayToT38State toT38 = state.Core.ToT38;
        bool abort = (receiver.RawBitStream & 1) != 0;

        if (abort) {
            receiver.ReceiveAborts++;
            receiver.FlagsSeen = receiver.FlagsSeen < receiver.FramingOkThreshold
                ? 0
                : receiver.FramingOkThreshold - 1;
            if (receiver.Length > 2) {
                int category = state.T38Side.CurrentTxDataType == T38DataType.V21
                    ? (int)T38PacketCategory.ControlData
                    : (int)T38PacketCategory.ImageData;
                if (T38Core.t38_core_send_data(
                        state.T38Side.T38,
                        (int)state.T38Side.CurrentTxDataType,
                        (int)T38FieldType.HdlcFcsBad,
                        Array.Empty<byte>(),
                        0,
                        category) < 0) {
                    state.Logging.Warning("T.38 send failed");
                }
            }
        } else if (receiver.FlagsSeen >= receiver.FramingOkThreshold) {
            if (receiver.Length > 0)
                CompleteReceivedHdlcFrame(state);
        } else {
            if (receiver.FlagsSeen != receiver.FramingOkThreshold - 1 &&
                receiver.NumberOfBits != 7) {
                receiver.FlagsSeen = 0;
            }

            receiver.FlagsSeen++;
            if (receiver.FlagsSeen >= receiver.FramingOkThreshold &&
                !receiver.FramingOkAnnounced) {
                if (state.T38Side.CurrentTxDataType == T38DataType.V21) {
                    T38Core.t38_core_send_indicator(
                        state.T38Side.T38,
                        set_slow_packetisation(state));
                    state.Modems.RxSignalPresent = true;
                }
                if (state.T38Side.InProgressRxIndicator == (int)T38Indicator.Cng)
                    set_next_tx_type(state);
                receiver.FramingOkAnnounced = true;
            }
        }

        receiver.Length = 0;
        receiver.NumberOfBits = 0;
        toT38.Crc = 0xFFFF;
        toT38.DataPointer = 0;
        state.T38Side.CorruptCurrentFrame[1] = false;
    }

    private static void CompleteReceivedHdlcFrame(T38GatewayState state) {
        T38GatewayHdlcReceiveState receiver = state.Core.HdlcReceive;
        T38GatewayToT38State toT38 = state.Core.ToT38;

        if (receiver.Length < 2) {
            receiver.ReceiveLengthErrors++;
            return;
        }

        int category = state.T38Side.CurrentTxDataType == T38DataType.V21
            ? (int)T38PacketCategory.ControlData
            : (int)T38PacketCategory.ImageData;

        if (toT38.DataPointer > 0) {
            int start = receiver.Length - 2 - toT38.DataPointer;
            BitReverse(
                toT38.Data.AsSpan(0, toT38.DataPointer),
                receiver.Buffer.AsSpan(start, toT38.DataPointer));
            if (T38Core.t38_core_send_data(
                    state.T38Side.T38,
                    (int)state.T38Side.CurrentTxDataType,
                    (int)T38FieldType.HdlcData,
                    toT38.Data,
                    toT38.DataPointer,
                    category) < 0) {
                state.Logging.Warning("T.38 send failed");
            }
        }

        bool aligned = receiver.NumberOfBits == 7;
        bool goodCrc = toT38.Crc == 0xF0B8;
        if (!aligned || !goodCrc) {
            receiver.ReceiveCrcErrors++;
            if (T38Core.t38_core_send_data(
                    state.T38Side.T38,
                    (int)state.T38Side.CurrentTxDataType,
                    (int)T38FieldType.HdlcFcsBad,
                    Array.Empty<byte>(),
                    0,
                    category) < 0) {
                state.Logging.Warning("T.38 send failed");
            }
            return;
        }

        receiver.ReceiveFrames++;
        receiver.ReceiveBytes += receiver.Length - 2;
        if (state.T38Side.CurrentTxDataType == T38DataType.V21) {
            monitor_control_messages(
                state,
                true,
                receiver.Buffer,
                receiver.Length - 2);
            state.Core.RealTimeFrameHandler?.Invoke(
                state.Core.RealTimeFrameUserData,
                true,
                receiver.Buffer.AsMemory(0, receiver.Length - 2));
        } else {
            state.Core.ShortTrain = true;
        }
        if (T38Core.t38_core_send_data(
                state.T38Side.T38,
                (int)state.T38Side.CurrentTxDataType,
                (int)T38FieldType.HdlcFcsOk,
                Array.Empty<byte>(),
                0,
                category) < 0) {
            state.Logging.Warning("T.38 send failed");
        }
    }

    private static void set_octets_per_data_packet(
        T38GatewayState state,
        int bitRate) {
        int octets = state.T38Side.T38.MicrosecondsPerTxChunk * bitRate /
            (8 * 1_000 * 1_000);
        state.Core.ToT38.OctetsPerDataPacket = Math.Max(1, octets);
    }

    private static int set_slow_packetisation(T38GatewayState state) {
        set_octets_per_data_packet(state, 300);
        state.T38Side.CurrentTxDataType = T38DataType.V21;
        return (int)T38Indicator.V21Preamble;
    }

    private static int set_fast_packetisation(T38GatewayState state) {
        int indicator = (int)T38Indicator.NoSignal;
        set_octets_per_data_packet(state, Math.Max(1, state.Core.FastBitRate));

        switch (state.Core.FastRxActive) {
            case T38GatewayModemType.V17Receive:
                (indicator, state.T38Side.CurrentTxDataType) = state.Core.FastBitRate switch {
                    7_200 => state.Core.ShortTrain
                        ? ((int)T38Indicator.V17_7200ShortTraining, T38DataType.V17_7200)
                        : ((int)T38Indicator.V17_7200LongTraining, T38DataType.V17_7200),
                    9_600 => state.Core.ShortTrain
                        ? ((int)T38Indicator.V17_9600ShortTraining, T38DataType.V17_9600)
                        : ((int)T38Indicator.V17_9600LongTraining, T38DataType.V17_9600),
                    12_000 => state.Core.ShortTrain
                        ? ((int)T38Indicator.V17_12000ShortTraining, T38DataType.V17_12000)
                        : ((int)T38Indicator.V17_12000LongTraining, T38DataType.V17_12000),
                    _ => state.Core.ShortTrain
                        ? ((int)T38Indicator.V17_14400ShortTraining, T38DataType.V17_14400)
                        : ((int)T38Indicator.V17_14400LongTraining, T38DataType.V17_14400)
                };
                break;

            case T38GatewayModemType.V27TerReceive:
                (indicator, state.T38Side.CurrentTxDataType) =
                    state.Core.FastBitRate == 2_400
                        ? ((int)T38Indicator.V27Ter2400Training, T38DataType.V27Ter2400)
                        : ((int)T38Indicator.V27Ter4800Training, T38DataType.V27Ter4800);
                break;

            case T38GatewayModemType.V29Receive:
                (indicator, state.T38Side.CurrentTxDataType) =
                    state.Core.FastBitRate == 7_200
                        ? ((int)T38Indicator.V29_7200Training, T38DataType.V29_7200)
                        : ((int)T38Indicator.V29_9600Training, T38DataType.V29_9600);
                break;
        }

        return indicator;
    }

    private static void announce_training(T38GatewayState state) {
        T38Core.t38_core_send_indicator(
            state.T38Side.T38,
            set_fast_packetisation(state));
    }

    private static int restart_rx_modem(T38GatewayState state) {
        if (state.Core.ToT38.InputBits != 0 || state.Core.ToT38.OutputOctets != 0) {
            state.Logging.Flow(
                $"{state.Core.ToT38.InputBits} incoming audio bits. " +
                $"{state.Core.ToT38.OutputOctets} outgoing T.38 octets");
            state.Core.ToT38.InputBits = 0;
            state.Core.ToT38.OutputOctets = 0;
        }

        state.Logging.Flow(
            $"Restart rx modem - modem = {(int)state.Core.FastRxModem}, " +
            $"short train = {(state.Core.ShortTrain ? 1 : 0)}, " +
            $"ECM = {(state.Core.EcmMode ? 1 : 0)}");

        state.Modems.RxSignalPresent = false;
        state.Modems.RxTrained = false;
        state.T38Side.CurrentTxDataType = T38DataType.V21;
        to_t38_buffer_init(state.Core.ToT38);
        state.Core.ToT38.OctetsPerDataPacket = 1;

        state.Modems.ConfigureRawV21Receiver(
            bit => t38_hdlc_rx_put_bit(state, bit));

        bool useHdlc = state.Core.ImageDataMode && state.Core.EcmMode;
        NonEcmPutBitHandler sink = useHdlc
            ? bit => t38_hdlc_rx_put_bit(state, bit)
            : state.Core.ImageDataMode && state.Core.ToT38.FillBitRemoval
                ? bit => non_ecm_remove_fill_and_put_bit(state, bit)
                : bit => non_ecm_put_bit(state, bit);
        state.Modems.SetPutBit(sink);
        state.Modems.DeferredReceiveHandlerUpdates = true;

        switch (state.Core.FastRxModem) {
            case T38GatewayModemType.V27TerReceive:
                state.Modems.StartFastModem(
                    FaxModemChannel.V27TerRx,
                    state.Core.FastBitRate,
                    state.Core.ShortTrain,
                    useHdlc: false);
                state.Core.FastRxActive = state.Core.FastRxModem;
                break;

            case T38GatewayModemType.V29Receive:
                state.Modems.StartFastModem(
                    FaxModemChannel.V29Rx,
                    state.Core.FastBitRate,
                    state.Core.ShortTrain,
                    useHdlc: false);
                state.Core.FastRxActive = state.Core.FastRxModem;
                break;

            case T38GatewayModemType.V17Receive:
                state.Modems.StartFastModem(
                    FaxModemChannel.V17Rx,
                    state.Core.FastBitRate,
                    state.Core.ShortTrain,
                    useHdlc: false);
                state.Core.FastRxActive = state.Core.FastRxModem;
                break;

            default:
                state.Modems.ConfigureRawV21Receiver(
                    bit => t38_hdlc_rx_put_bit(state, bit));
                state.Core.FastRxActive = T38GatewayModemType.None;
                break;
        }

        return 0;
    }

    private static void update_rx_timing(T38GatewayState state, int length) {
        if (state.Core.SamplesToTimeout <= 0)
            return;

        state.Core.SamplesToTimeout -= length;
        if (state.Core.SamplesToTimeout > 0)
            return;

        switch (state.Core.TimedMode) {
            case TimedModeTcfPastV21Modem:
                announce_training(state);
                state.Core.TimedMode = TimedModeTcfFastModemAnnounced;
                break;

            case TimedModeTcfFastModemSeen:
                announce_training(state);
                state.Core.SamplesToTimeout = MillisecondsToSamples(500);
                state.Core.TimedMode = TimedModeTcfFastModemAnnounced;
                break;

            case TimedModeTcfFastModemAnnounced:
                state.Core.TimedMode = TimedModeIdle;
                break;

            case TimedModeStartup:
                T38Core.t38_core_send_indicator(
                    state.T38Side.T38,
                    (int)T38Indicator.NoSignal);
                state.Core.TimedMode = TimedModeIdle;
                break;
        }
    }

    private static void to_t38_buffer_init(T38GatewayToT38State state) {
        state.DataPointer = 0;
        state.BitStream = 0xFFFF;
        state.BitNumber = 0;
        state.InputBits = 0;
        state.OutputOctets = 0;
        state.BitsAbsorbed = 0;
        state.Crc = 0xFFFF;
    }

    private static void LogV8Field(
        T38GatewayState state,
        T38FieldType fieldType,
        ReadOnlySpan<byte> data) {
        switch (fieldType) {
            case T38FieldType.CmMessage:
                state.Logging.Flow(data.Length >= 1
                    ? $"CM profile {data[0] - (byte)'0'} - {T38Core.t38_cm_profile_to_str(data[0])}"
                    : "Bad length for CM message");
                break;
            case T38FieldType.JmMessage:
                state.Logging.Flow(data.Length >= 2
                    ? $"JM - {T38Core.t38_jm_to_str(data.ToArray(), data.Length)}"
                    : "Bad length for JM message");
                break;
            case T38FieldType.CiMessage:
                state.Logging.Flow(data.Length >= 1
                    ? $"CI 0x{data[0]:X2}"
                    : "Bad length for CI message");
                break;
        }
    }

    private static bool IsFastDataType(T38DataType dataType) {
        return dataType is
            T38DataType.V17_7200 or
            T38DataType.V17_9600 or
            T38DataType.V17_12000 or
            T38DataType.V17_14400 or
            T38DataType.V27Ter2400 or
            T38DataType.V27Ter4800 or
            T38DataType.V29_7200 or
            T38DataType.V29_9600;
    }

    private static ushort CrcItu16Update(byte value, ushort crc) {
        uint result = (uint)(crc ^ value);
        for (int bit = 0; bit < 8; bit++) {
            result = (result & 1) != 0
                ? (result >> 1) ^ 0x8408u
                : result >> 1;
        }
        return (ushort)result;
    }

    private static void BitReverse(Span<byte> destination, ReadOnlySpan<byte> source) {
        if (destination.Length < source.Length)
            throw new ArgumentException("Destination is shorter than source.", nameof(destination));
        for (int index = 0; index < source.Length; index++)
            destination[index] = ReverseBits(source[index]);
    }

    private static byte ReverseBits(byte value) {
        value = (byte)(((value & 0x55) << 1) | ((value >> 1) & 0x55));
        value = (byte)(((value & 0x33) << 2) | ((value >> 2) & 0x33));
        return (byte)((value << 4) | (value >> 4));
    }

    private static int MillisecondsToSamples(int milliseconds) =>
        checked(milliseconds * 8);

    private static int NextQueueIndex(int index) =>
        (index + 1) & (HdlcBufferCount - 1);


}
