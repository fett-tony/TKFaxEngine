/*
 * TKFaxEngine - managed C# port
 *
 * t31.cs - A T.31 compatible class 1 FAX modem interface.
 *
 * Combined 1:1 managed port of t31.c, t31.h and private/t31.h.
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 *
 * Copyright (C) 2004, 2005, 2006, 2008 Steve Underwood
 *
 * This port preserves the GNU Lesser General Public License version 2.1
 * terms of the original source files.
 */

#nullable enable

using global::TKFaxEngine.Audio;
using global::TKFaxEngine.Daten.T38;
using global::TKFaxEngine.Modem;
using global::TKFaxEngine.Modem.V8;
using System.Globalization;
using System.Runtime.InteropServices;
using static global::TKFaxEngine.AtInterpreterApi;
using static global::TKFaxEngine.Audio.PowerMeterApi;
using static global::TKFaxEngine.Audio.Telephony;
using static global::TKFaxEngine.BitOperationsApi;
using static global::TKFaxEngine.CrcApi;
using static global::TKFaxEngine.Daten.T38.T38Core;
using static global::TKFaxEngine.LoggingApi;
using static global::TKFaxEngine.Modem.FaxModemsApi;
using static global::TKFaxEngine.Modem.HdlcApi;
using static global::TKFaxEngine.QueueApi;
using static global::TKFaxEngine.VectorDspApi;
using at_state_t = global::TKFaxEngine.AtInterpreterState;
using fax_modems_state_t = global::TKFaxEngine.Modem.FaxModems;
using hdlc_rx_state_t = global::TKFaxEngine.Modem.HdlcReceiver;
using hdlc_tx_state_t = global::TKFaxEngine.Modem.HdlcTransmitter;
using logging_state_t = global::TKFaxEngine.SpanLogState;
using power_meter_t = global::TKFaxEngine.Audio.PowerMeterState;
using queue_state_t = global::TKFaxEngine.QueueState;
using t38_core_state_t = global::TKFaxEngine.Daten.T38.T38CoreState;
using v8_state_t = global::TKFaxEngine.Modem.V8.V8State;

namespace TKFaxEngine.Daten.T30;

public delegate int at_tx_handler_t(object? user_data, ReadOnlySpan<byte> data);
public delegate int t31_modem_control_handler_t(t31_state_t s, object? user_data, int op, string? num);
public delegate int t38_tx_packet_handler_t(t38_core_state_t s, object? user_data, ReadOnlyMemory<byte> packet, int count);

public sealed class t31_hdlc_buf_t {
    public byte[] buf = new byte[t31.T31_MAX_HDLC_LEN];
    public short len;
}

public sealed class t31_hdlc_state_t {
    public t31_hdlc_buf_t[] buf;
    public int @in;
    public int @out;

    public t31_hdlc_state_t() {
        buf = new t31_hdlc_buf_t[t31.T31_TX_HDLC_BUFS];
        for (int i = 0; i < buf.Length; i++)
            buf[i] = new t31_hdlc_buf_t();
    }
}

public sealed class t31_audio_front_end_state_t {
    public fax_modems_state_t modems = new();
    public v8_state_t v8 = new();

    public SpanTxHandler? next_tx_handler;
    public object? next_tx_user_data;

    public int bit_no;
    public int current_byte;

    public power_meter_t rx_power = new(4);
    public short last_sample;
    public int silence_threshold_power;
    public int silence_heard;
}

public sealed class t31_t38_front_end_state_t {
    public t38_core_state_t t38 = new();
    public int timed_step;
    public bool rx_data_missing;
    public int octets_per_data_packet;

    public hdlc_tx_state_t? hdlc_tx_non_ecm;
    public hdlc_rx_state_t? hdlc_rx_non_ecm;
    public int hdlc_tx_non_ecm_octets_in_progress;
    public int hdlc_tx_non_ecm_num_bits;
    public byte hdlc_tx_non_ecm_idle_octet = 0x7E;

    public sealed class hdlc_rx_state {
        public byte[] buf = new byte[t31.T31_T38_MAX_HDLC_LEN + 2];
        public int len;
    }

    public sealed class hdlc_tx_state {
        public int extra_bits;
    }

    public hdlc_rx_state hdlc_rx = new();
    public hdlc_tx_state hdlc_tx = new();
    public t31_hdlc_state_t hdlc_from_t31 = new();

    public int ecm_mode;
    public int non_ecm_trailer_bytes;
    public int next_tx_indicator;
    public int current_tx_data_type;
    public int current_rx_type;
    public int current_tx_type;
    public int tx_bit_rate;
    public int samples;
    public int next_tx_samples;
    public int timeout_tx_samples;
    public int timeout_rx_samples;
}

public sealed class t31_state_t : IDisposable {
    public at_state_t? at_state;
    public t31_modem_control_handler_t? modem_control_handler;
    public object? modem_control_user_data;

    public t31_audio_front_end_state_t audio = new();
    public t31_t38_front_end_state_t t38_fe = new();

    public bool t38_mode;

    public sealed class hdlc_tx_state {
        public byte[] buf = new byte[t31.T31_MAX_HDLC_LEN + 2];
        public int len;
        public int ptr;
        public bool final;
    }

    public sealed class non_ecm_tx_state {
        public byte[] buf = new byte[t31.T31_TX_BUF_LEN];
        public int in_bytes;
        public int out_bytes;
        public bool data_started;
        public bool holding;
        public bool final;
    }

    public hdlc_tx_state hdlc_tx = new();
    public non_ecm_tx_state non_ecm_tx = new();

    public bool dled;
    public int silence_awaited;
    public int bit_rate;
    public bool rx_frame_received;
    public long call_samples;
    public long dte_data_timeout;
    public int modem;
    public bool short_train;
    public queue_state_t? rx_queue;
    public logging_state_t logging = new();

    /* Managed storage for fields held privately by at_state_t. */
    internal at_tx_handler_t? at_tx_handler;
    internal object? at_tx_user_data;
    internal byte[] at_rx_data = new byte[512];
    internal int at_rx_data_bytes;
    internal bool transmit;
    internal bool do_hangup;
    internal int dte_inactivity_timeout;
    internal bool disposed;

    public void Dispose() {
        if (disposed)
            return;
        t31.t31_release(this);
        disposed = true;
    }
}

public static class t31 {
    public const int T31_TX_BUF_LEN = 4096;
    public const int T31_TX_BUF_HIGH_TIDE = 4096 - 1024;
    public const int T31_TX_BUF_LOW_TIDE = 1024;
    public const int T31_MAX_HDLC_LEN = 284;
    public const int T31_T38_MAX_HDLC_LEN = 260;
    public const int T31_TX_HDLC_BUFS = 256;

    private const int INDICATOR_TX_COUNT = 3;
    private const int DATA_TX_COUNT = 1;
    private const int DATA_END_TX_COUNT = 3;
    private const int DEFAULT_DTE_TIMEOUT = 5;
    private const int MAX_OCTETS_PER_UNPACED_CHUNK = 300;
    private const int MID_RX_TIMEOUT = 15000;
    private const int HDLC_FRAMING_OK_THRESHOLD = 5;

    private const byte ETX = 0x03;
    private const byte DLE = 0x10;
    private const byte SUB = 0x1A;

    private const int DISBIT1 = 0x01;
    private const int DISBIT2 = 0x02;
    private const int DISBIT3 = 0x04;
    private const int DISBIT4 = 0x08;
    private const int DISBIT5 = 0x10;
    private const int DISBIT6 = 0x20;
    private const int DISBIT7 = 0x40;
    private const int DISBIT8 = 0x80;

    private const int T38_TIMED_STEP_NONE = 0;
    private const int T38_TIMED_STEP_NON_ECM_MODEM = 0x10;
    private const int T38_TIMED_STEP_NON_ECM_MODEM_2 = 0x11;
    private const int T38_TIMED_STEP_NON_ECM_MODEM_3 = 0x12;
    private const int T38_TIMED_STEP_NON_ECM_MODEM_4 = 0x13;
    private const int T38_TIMED_STEP_NON_ECM_MODEM_5 = 0x14;
    private const int T38_TIMED_STEP_HDLC_MODEM = 0x20;
    private const int T38_TIMED_STEP_HDLC_MODEM_2 = 0x21;
    private const int T38_TIMED_STEP_HDLC_MODEM_3 = 0x22;
    private const int T38_TIMED_STEP_HDLC_MODEM_4 = 0x23;
    private const int T38_TIMED_STEP_HDLC_MODEM_5 = 0x24;
    private const int T38_TIMED_STEP_CED = 0x30;
    private const int T38_TIMED_STEP_CED_2 = 0x31;
    private const int T38_TIMED_STEP_CED_3 = 0x32;
    private const int T38_TIMED_STEP_CNG = 0x40;
    private const int T38_TIMED_STEP_CNG_2 = 0x41;
    private const int T38_TIMED_STEP_PAUSE = 0x50;
    private const int T38_TIMED_STEP_NO_SIGNAL = 0x60;

    private const int T30_FRONT_END_SEND_STEP_COMPLETE = (int)T30FrontEndStatus.SendStepComplete;
    private const int T30_FRONT_END_RECEIVE_COMPLETE = (int)T30FrontEndStatus.ReceiveComplete;
    private const int T30_FRONT_END_SIGNAL_PRESENT = (int)T30FrontEndStatus.SignalPresent;
    private const int T30_FRONT_END_SIGNAL_ABSENT = (int)T30FrontEndStatus.SignalAbsent;
    private const int T30_FRONT_END_CED_PRESENT = (int)T30FrontEndStatus.CedPresent;
    private const int T30_FRONT_END_CNG_PRESENT = (int)T30FrontEndStatus.CngPresent;

    private const int T30_MODEM_V21 = (int)T30ModemType.V21;
    private const int T30_MODEM_CNG = (int)T30ModemType.Cng;
    private const int T30_MODEM_DONE = (int)T30ModemType.Done;
    private const int T30_IAF_MODE_T38 = (int)T30IafMode.T38;

    private const int T30_DCS = T30Frame.Dcs;
    private const int T30_CFR = T30Frame.Cfr;

    private const int SIG_STATUS_CARRIER_DOWN = (int)SignalStatus.CarrierDown;
    private const int SIG_STATUS_CARRIER_UP = (int)SignalStatus.CarrierUp;
    private const int SIG_STATUS_TRAINING_IN_PROGRESS = (int)SignalStatus.TrainingInProgress;
    private const int SIG_STATUS_TRAINING_SUCCEEDED = (int)SignalStatus.TrainingSucceeded;
    private const int SIG_STATUS_TRAINING_FAILED = (int)SignalStatus.TrainingFailed;
    private const int SIG_STATUS_FRAMING_OK = (int)SignalStatus.FramingOk;
    private const int SIG_STATUS_END_OF_DATA = (int)SignalStatus.EndOfData;
    private const int SIG_STATUS_ABORT = (int)SignalStatus.Abort;

    private const int AT_MODE_ONHOOK_COMMAND = (int)AtReceiveMode.OnHookCommand;
    private const int AT_MODE_OFFHOOK_COMMAND = (int)AtReceiveMode.OffHookCommand;
    private const int AT_MODE_CONNECTED = (int)AtReceiveMode.Connected;
    private const int AT_MODE_DELIVERY = (int)AtReceiveMode.Delivery;
    private const int AT_MODE_HDLC = (int)AtReceiveMode.Hdlc;
    private const int AT_MODE_STUFFED = (int)AtReceiveMode.Stuffed;

    private const int AT_MODEM_CONTROL_CALL = (int)AtModemControlOperation.Call;
    private const int AT_MODEM_CONTROL_ANSWER = (int)AtModemControlOperation.Answer;
    private const int AT_MODEM_CONTROL_HANGUP = (int)AtModemControlOperation.Hangup;
    private const int AT_MODEM_CONTROL_ONHOOK = (int)AtModemControlOperation.OnHook;
    private const int AT_MODEM_CONTROL_RESTART = (int)AtModemControlOperation.Restart;
    private const int AT_MODEM_CONTROL_DTE_TIMEOUT = (int)AtModemControlOperation.DteTimeout;
    private const int AT_MODEM_CONTROL_CTS = (int)AtModemControlOperation.Cts;

    private const int AT_RESPONSE_CODE_OK = (int)AtResponseCode.Ok;
    private const int AT_RESPONSE_CODE_CONNECT = (int)AtResponseCode.Connect;
    private const int AT_RESPONSE_CODE_NO_CARRIER = (int)AtResponseCode.NoCarrier;
    private const int AT_RESPONSE_CODE_ERROR = (int)AtResponseCode.Error;
    private const int AT_RESPONSE_CODE_FCERROR = (int)AtResponseCode.FcError;
    private const int AT_RESPONSE_CODE_FRH3 = (int)AtResponseCode.Frh3;

    private const int T38_IND_NO_SIGNAL = (int)T38Indicator.NoSignal;
    private const int T38_IND_CNG = (int)T38Indicator.Cng;
    private const int T38_IND_CED = (int)T38Indicator.Ced;
    private const int T38_IND_V21_PREAMBLE = (int)T38Indicator.V21Preamble;
    private const int T38_IND_V27TER_2400_TRAINING = (int)T38Indicator.V27Ter2400Training;
    private const int T38_IND_V27TER_4800_TRAINING = (int)T38Indicator.V27Ter4800Training;
    private const int T38_IND_V29_7200_TRAINING = (int)T38Indicator.V29_7200Training;
    private const int T38_IND_V29_9600_TRAINING = (int)T38Indicator.V29_9600Training;
    private const int T38_IND_V17_7200_SHORT_TRAINING = (int)T38Indicator.V17_7200ShortTraining;
    private const int T38_IND_V17_7200_LONG_TRAINING = (int)T38Indicator.V17_7200LongTraining;
    private const int T38_IND_V17_9600_SHORT_TRAINING = (int)T38Indicator.V17_9600ShortTraining;
    private const int T38_IND_V17_9600_LONG_TRAINING = (int)T38Indicator.V17_9600LongTraining;
    private const int T38_IND_V17_12000_SHORT_TRAINING = (int)T38Indicator.V17_12000ShortTraining;
    private const int T38_IND_V17_12000_LONG_TRAINING = (int)T38Indicator.V17_12000LongTraining;
    private const int T38_IND_V17_14400_SHORT_TRAINING = (int)T38Indicator.V17_14400ShortTraining;
    private const int T38_IND_V17_14400_LONG_TRAINING = (int)T38Indicator.V17_14400LongTraining;
    private const int T38_IND_V8_ANSAM = (int)T38Indicator.V8Ansam;
    private const int T38_IND_V8_SIGNAL = (int)T38Indicator.V8Signal;
    private const int T38_IND_V34_CNTL_CHANNEL_1200 = (int)T38Indicator.V34ControlChannel1200;
    private const int T38_IND_V34_PRI_CHANNEL = (int)T38Indicator.V34PrimaryChannel;
    private const int T38_IND_V34_CC_RETRAIN = (int)T38Indicator.V34ControlChannelRetrain;
    private const int T38_IND_V33_12000_TRAINING = (int)T38Indicator.V33_12000Training;
    private const int T38_IND_V33_14400_TRAINING = (int)T38Indicator.V33_14400Training;

    private const int T38_DATA_NONE = (int)T38DataType.None;
    private const int T38_DATA_V21 = (int)T38DataType.V21;
    private const int T38_DATA_V27TER_2400 = (int)T38DataType.V27Ter2400;
    private const int T38_DATA_V27TER_4800 = (int)T38DataType.V27Ter4800;
    private const int T38_DATA_V29_7200 = (int)T38DataType.V29_7200;
    private const int T38_DATA_V29_9600 = (int)T38DataType.V29_9600;
    private const int T38_DATA_V17_7200 = (int)T38DataType.V17_7200;
    private const int T38_DATA_V17_9600 = (int)T38DataType.V17_9600;
    private const int T38_DATA_V17_12000 = (int)T38DataType.V17_12000;
    private const int T38_DATA_V17_14400 = (int)T38DataType.V17_14400;

    private const int T38_FIELD_HDLC_DATA = (int)T38FieldType.HdlcData;
    private const int T38_FIELD_HDLC_SIG_END = (int)T38FieldType.HdlcSignalEnd;
    private const int T38_FIELD_HDLC_FCS_OK = (int)T38FieldType.HdlcFcsOk;
    private const int T38_FIELD_HDLC_FCS_BAD = (int)T38FieldType.HdlcFcsBad;
    private const int T38_FIELD_HDLC_FCS_OK_SIG_END = (int)T38FieldType.HdlcFcsOkSignalEnd;
    private const int T38_FIELD_HDLC_FCS_BAD_SIG_END = (int)T38FieldType.HdlcFcsBadSignalEnd;
    private const int T38_FIELD_T4_NON_ECM_DATA = (int)T38FieldType.T4NonEcmData;
    private const int T38_FIELD_T4_NON_ECM_SIG_END = (int)T38FieldType.T4NonEcmSignalEnd;
    private const int T38_FIELD_CM_MESSAGE = (int)T38FieldType.CmMessage;
    private const int T38_FIELD_JM_MESSAGE = (int)T38FieldType.JmMessage;
    private const int T38_FIELD_CI_MESSAGE = (int)T38FieldType.CiMessage;
    private const int T38_FIELD_V34RATE = (int)T38FieldType.V34Rate;

    private const int T38_PACKET_CATEGORY_INDICATOR = (int)T38PacketCategory.Indicator;
    private const int T38_PACKET_CATEGORY_CONTROL_DATA = (int)T38PacketCategory.ControlData;
    private const int T38_PACKET_CATEGORY_CONTROL_DATA_END = (int)T38PacketCategory.ControlDataEnd;
    private const int T38_PACKET_CATEGORY_IMAGE_DATA = (int)T38PacketCategory.ImageData;
    private const int T38_PACKET_CATEGORY_IMAGE_DATA_END = (int)T38PacketCategory.ImageDataEnd;

    private const int T38_CHUNKING_MERGE_FCS_WITH_DATA = (int)T38ChunkingMode.MergeFcsWithData;
    private const int T38_CHUNKING_ALLOW_TEP_TIME = (int)T38ChunkingMode.AllowTepTime;
    private const int T38_CHUNKING_SEND_REGULAR_INDICATORS = (int)T38ChunkingMode.SendRegularIndicators;
    private const int T38_CHUNKING_SEND_2S_REGULAR_INDICATORS = (int)T38ChunkingMode.SendTwoSecondRegularIndicators;

    private static void t31_set_at_rx_mode(t31_state_t s, int new_mode) {
        if (s.at_state is not null)
            at_set_at_rx_mode(s.at_state, new_mode);

        if (new_mode == AT_MODE_HDLC || new_mode == AT_MODE_STUFFED)
            t31_modem_control_handler(s, AT_MODEM_CONTROL_DTE_TIMEOUT, (s.dte_inactivity_timeout * 1000).ToString(CultureInfo.InvariantCulture));
        else
            t31_modem_control_handler(s, AT_MODEM_CONTROL_DTE_TIMEOUT, null);
    }

    private static int front_end_status(t31_state_t s, int status) {
        span_log(s.logging, SPAN_LOG_FLOW, "Front end status %d\n", status);
        switch (status) {
            case T30_FRONT_END_SEND_STEP_COMPLETE:
                switch (s.modem) {
                    case FAX_MODEM_SILENCE_TX:
                        s.modem = FAX_MODEM_NONE;
                        at_put_response_code(s.at_state!, AT_RESPONSE_CODE_OK);
                        if (s.do_hangup) {
                            at_modem_control(s.at_state!, AT_MODEM_CONTROL_HANGUP, null);
                            t31_set_at_rx_mode(s, AT_MODE_ONHOOK_COMMAND);
                            s.do_hangup = false;
                        } else {
                            t31_set_at_rx_mode(s, AT_MODE_OFFHOOK_COMMAND);
                        }
                        break;
                    case FAX_MODEM_CED_TONE_TX:
                        s.modem = FAX_MODEM_NONE;
                        restart_modem(s, FAX_MODEM_V21_TX);
                        t31_set_at_rx_mode(s, AT_MODE_HDLC);
                        break;
                    case FAX_MODEM_V21_TX:
                    case FAX_MODEM_V17_TX:
                    case FAX_MODEM_V27TER_TX:
                    case FAX_MODEM_V29_TX:
                        s.modem = FAX_MODEM_NONE;
                        at_put_response_code(s.at_state!, AT_RESPONSE_CODE_OK);
                        t31_set_at_rx_mode(s, AT_MODE_OFFHOOK_COMMAND);
                        restart_modem(s, FAX_MODEM_SILENCE_TX);
                        break;
                }
                break;
            case T30_FRONT_END_RECEIVE_COMPLETE:
                break;
        }
        if (s.t38_fe.timed_step == T38_TIMED_STEP_NONE)
            return -1;
        return 0;
    }

    private static int extra_bits_in_stuffed_frame(byte[] buf, int len) {
        int ones = 0;
        int stuffed = 0;
        for (int i = 0; i < len; i++) {
            int bitstream = buf[i];
            for (int j = 0; j < 8; j++) {
                if ((bitstream & 1) != 0) {
                    if (++ones >= 5) {
                        ones = 0;
                        stuffed++;
                    }
                } else {
                    ones = 0;
                }
                bitstream >>= 1;
            }
        }
        return stuffed + 16 + 3 + 16;
    }

    private static int process_rx_missing(t38_core_state_t t, object? user_data, int rx_seq_no, int expected_seq_no) {
        var s = (t31_state_t)user_data!;
        s.t38_fe.rx_data_missing = true;
        return 0;
    }

    private static int process_rx_indicator(t38_core_state_t t, object? user_data, T38Indicator indicator_value) {
        var s = (t31_state_t)user_data!;
        t31_t38_front_end_state_t fe = s.t38_fe;
        int indicator = (int)indicator_value;

        if (t.CurrentRxIndicator == indicator)
            return 0;

        switch (indicator) {
            case T38_IND_NO_SIGNAL:
                if (t.CurrentRxIndicator == T38_IND_V21_PREAMBLE
                    && (fe.current_rx_type == T30_MODEM_V21 || fe.current_rx_type == T30_MODEM_CNG)) {
                    hdlc_rx_status(s, SIG_STATUS_CARRIER_DOWN);
                }
                fe.timeout_rx_samples = 0;
                front_end_status(s, T30_FRONT_END_SIGNAL_ABSENT);
                break;
            case T38_IND_CNG:
                front_end_status(s, T30_FRONT_END_CNG_PRESENT);
                break;
            case T38_IND_CED:
                front_end_status(s, T30_FRONT_END_CED_PRESENT);
                break;
            case T38_IND_V21_PREAMBLE:
                fe.timeout_rx_samples = fe.samples + milliseconds_to_samples(MID_RX_TIMEOUT);
                front_end_status(s, T30_FRONT_END_SIGNAL_PRESENT);
                break;
            case T38_IND_V27TER_2400_TRAINING:
            case T38_IND_V27TER_4800_TRAINING:
            case T38_IND_V29_7200_TRAINING:
            case T38_IND_V29_9600_TRAINING:
            case T38_IND_V17_7200_SHORT_TRAINING:
            case T38_IND_V17_7200_LONG_TRAINING:
            case T38_IND_V17_9600_SHORT_TRAINING:
            case T38_IND_V17_9600_LONG_TRAINING:
            case T38_IND_V17_12000_SHORT_TRAINING:
            case T38_IND_V17_12000_LONG_TRAINING:
            case T38_IND_V17_14400_SHORT_TRAINING:
            case T38_IND_V17_14400_LONG_TRAINING:
            case T38_IND_V33_12000_TRAINING:
            case T38_IND_V33_14400_TRAINING:
                fe.timeout_rx_samples = fe.samples + milliseconds_to_samples(MID_RX_TIMEOUT);
                front_end_status(s, T30_FRONT_END_SIGNAL_PRESENT);
                break;
            case T38_IND_V8_ANSAM:
            case T38_IND_V8_SIGNAL:
            case T38_IND_V34_CNTL_CHANNEL_1200:
            case T38_IND_V34_PRI_CHANNEL:
            case T38_IND_V34_CC_RETRAIN:
                front_end_status(s, T30_FRONT_END_SIGNAL_PRESENT);
                break;
            default:
                front_end_status(s, T30_FRONT_END_SIGNAL_ABSENT);
                break;
        }
        fe.hdlc_rx.len = 0;
        fe.rx_data_missing = false;
        return 0;
    }

    private static void process_hdlc_data(t31_t38_front_end_state_t fe, ReadOnlySpan<byte> buf, int len) {
        if (fe.hdlc_rx.len + len <= T31_T38_MAX_HDLC_LEN) {
            bit_reverse(fe.hdlc_rx.buf.AsSpan(fe.hdlc_rx.len), buf, len);
            fe.hdlc_rx.len += len;
        } else {
            fe.rx_data_missing = true;
        }
    }

    private static int process_rx_data(
        t38_core_state_t t,
        object? user_data,
        T38DataType data_type_value,
        T38FieldType field_type_value,
        ReadOnlyMemory<byte> field) {
        var s = (t31_state_t)user_data!;
        t31_t38_front_end_state_t fe = s.t38_fe;
        ReadOnlySpan<byte> buf = field.Span;
        int len = buf.Length;
        int data_type = (int)data_type_value;
        int field_type = (int)field_type_value;
        byte[] buf2 = new byte[len];

        switch (field_type) {
            case T38_FIELD_HDLC_DATA:
                if (fe.timeout_rx_samples == 0) {
                    fe.timeout_rx_samples = fe.samples + milliseconds_to_samples(MID_RX_TIMEOUT);
                    front_end_status(s, T30_FRONT_END_SIGNAL_PRESENT);
                    if (len <= 0 || buf[0] != 0xFF)
                        fe.rx_data_missing = true;
                }
                if (len > 0)
                    process_hdlc_data(fe, buf, len);
                fe.timeout_rx_samples = fe.samples + milliseconds_to_samples(MID_RX_TIMEOUT);
                break;

            case T38_FIELD_HDLC_FCS_OK:
                if (len > 0) {
                    span_log(s.logging, SPAN_LOG_WARNING, "There is data in a T38_FIELD_HDLC_FCS_OK!\n");
                    process_hdlc_data(fe, buf, len);
                }
                if (fe.hdlc_rx.len > 0) {
                    span_log(
                        s.logging,
                        SPAN_LOG_FLOW,
                        "Type %s - CRC OK (%s)\n",
                        fe.hdlc_rx.len >= 3 ? T30Logging.t30_frametype(fe.hdlc_rx.buf[2]) : "???",
                        fe.rx_data_missing ? "missing octets" : "clean");
                    if (data_type == T38_DATA_V21) {
                        if (fe.hdlc_rx.len >= 3) {
                            if ((fe.hdlc_rx.buf[2] & 0xFE) == T30_DCS) {
                                fe.ecm_mode = fe.hdlc_rx.len >= 7 && (fe.hdlc_rx.buf[6] & DISBIT3) != 0 ? 1 : 0;
                                span_log(s.logging, SPAN_LOG_FLOW, "ECM mode: %d\n", fe.ecm_mode);
                            } else if (s.t38_fe.ecm_mode == 1 && (fe.hdlc_rx.buf[2] & 0xFE) == T30_CFR) {
                                s.t38_fe.ecm_mode = 2;
                            }
                        }
                        crc_itu16_append(fe.hdlc_rx.buf, fe.hdlc_rx.len);
                        hdlc_accept_frame(s, fe.hdlc_rx.buf, fe.hdlc_rx.len, !fe.rx_data_missing);
                    } else {
                        hdlc_accept_t38_frame(s, fe.hdlc_rx.buf, fe.hdlc_rx.len, !fe.rx_data_missing);
                    }
                    fe.hdlc_rx.len = 0;
                }
                fe.rx_data_missing = false;
                fe.timeout_rx_samples = fe.samples + milliseconds_to_samples(MID_RX_TIMEOUT);
                break;

            case T38_FIELD_HDLC_FCS_BAD:
                if (len > 0) {
                    span_log(s.logging, SPAN_LOG_WARNING, "There is data in a T38_FIELD_HDLC_FCS_BAD!\n");
                    process_hdlc_data(fe, buf, len);
                }
                if (fe.hdlc_rx.len > 0) {
                    span_log(
                        s.logging,
                        SPAN_LOG_FLOW,
                        "Type %s - CRC bad (%s)\n",
                        fe.hdlc_rx.len >= 3 ? T30Logging.t30_frametype(fe.hdlc_rx.buf[2]) : "???",
                        fe.rx_data_missing ? "missing octets" : "clean");
                    if (data_type == T38_DATA_V21)
                        hdlc_accept_frame(s, fe.hdlc_rx.buf, fe.hdlc_rx.len, false);
                    else
                        hdlc_accept_t38_frame(s, fe.hdlc_rx.buf, fe.hdlc_rx.len, false);
                    fe.hdlc_rx.len = 0;
                }
                fe.rx_data_missing = false;
                fe.timeout_rx_samples = fe.samples + milliseconds_to_samples(MID_RX_TIMEOUT);
                break;

            case T38_FIELD_HDLC_FCS_OK_SIG_END:
                if (len > 0) {
                    span_log(s.logging, SPAN_LOG_WARNING, "There is data in a T38_FIELD_HDLC_FCS_OK_SIG_END!\n");
                    process_hdlc_data(fe, buf, len);
                }
                if (fe.hdlc_rx.len > 0) {
                    span_log(
                        s.logging,
                        SPAN_LOG_FLOW,
                        "Type %s - CRC OK, sig end (%s)\n",
                        fe.hdlc_rx.len >= 3 ? T30Logging.t30_frametype(fe.hdlc_rx.buf[2]) : "???",
                        fe.rx_data_missing ? "missing octets" : "clean");
                    if (data_type == T38_DATA_V21) {
                        if (fe.hdlc_rx.len >= 3) {
                            if ((fe.hdlc_rx.buf[2] & 0xFE) == T30_DCS) {
                                fe.ecm_mode = fe.hdlc_rx.len >= 7 && (fe.hdlc_rx.buf[6] & DISBIT3) != 0 ? 1 : 0;
                                span_log(s.logging, SPAN_LOG_FLOW, "ECM mode: %d\n", fe.ecm_mode);
                            } else if (s.t38_fe.ecm_mode == 1 && (fe.hdlc_rx.buf[2] & 0xFE) == T30_CFR) {
                                s.t38_fe.ecm_mode = 2;
                            }
                        }
                        crc_itu16_append(fe.hdlc_rx.buf, fe.hdlc_rx.len);
                        hdlc_accept_frame(s, fe.hdlc_rx.buf, fe.hdlc_rx.len, !fe.rx_data_missing);
                    } else {
                        hdlc_accept_t38_frame(s, fe.hdlc_rx.buf, fe.hdlc_rx.len, !fe.rx_data_missing);
                    }
                    fe.hdlc_rx.len = 0;
                }
                fe.rx_data_missing = false;
                if (t.CurrentRxDataType != data_type || t.CurrentRxFieldType != field_type) {
                    if (data_type == T38_DATA_V21)
                        hdlc_rx_status(s, SIG_STATUS_CARRIER_DOWN);
                    else
                        non_ecm_rx_status(s, SIG_STATUS_CARRIER_DOWN);
                }
                fe.timeout_rx_samples = 0;
                break;

            case T38_FIELD_HDLC_FCS_BAD_SIG_END:
                if (len > 0) {
                    span_log(s.logging, SPAN_LOG_WARNING, "There is data in a T38_FIELD_HDLC_FCS_BAD_SIG_END!\n");
                    process_hdlc_data(fe, buf, len);
                }
                if (fe.hdlc_rx.len > 0) {
                    span_log(
                        s.logging,
                        SPAN_LOG_FLOW,
                        "Type %s - CRC bad, sig end (%s)\n",
                        fe.hdlc_rx.len >= 3 ? T30Logging.t30_frametype(fe.hdlc_rx.buf[2]) : "???",
                        fe.rx_data_missing ? "missing octets" : "clean");
                    if (data_type == T38_DATA_V21)
                        hdlc_accept_frame(s, fe.hdlc_rx.buf, fe.hdlc_rx.len, false);
                    else
                        hdlc_accept_t38_frame(s, fe.hdlc_rx.buf, fe.hdlc_rx.len, false);
                    fe.hdlc_rx.len = 0;
                }
                fe.rx_data_missing = false;
                if (t.CurrentRxDataType != data_type || t.CurrentRxFieldType != field_type) {
                    if (data_type == T38_DATA_V21)
                        hdlc_rx_status(s, SIG_STATUS_CARRIER_DOWN);
                    else
                        non_ecm_rx_status(s, SIG_STATUS_CARRIER_DOWN);
                }
                fe.timeout_rx_samples = 0;
                break;

            case T38_FIELD_HDLC_SIG_END:
                if (len > 0)
                    span_log(s.logging, SPAN_LOG_WARNING, "There is data in a T38_FIELD_HDLC_SIG_END!\n");
                if (t.CurrentRxDataType != data_type || t.CurrentRxFieldType != field_type) {
                    fe.hdlc_rx.len = 0;
                    fe.rx_data_missing = false;
                    fe.timeout_rx_samples = 0;
                    if (data_type == T38_DATA_V21)
                        hdlc_rx_status(s, SIG_STATUS_CARRIER_DOWN);
                    else
                        non_ecm_rx_status(s, SIG_STATUS_CARRIER_DOWN);
                }
                break;

            case T38_FIELD_T4_NON_ECM_DATA:
                if (len > 0) {
                    if (s.at_state?.ReceiveSignalPresent != true) {
                        non_ecm_rx_status(s, SIG_STATUS_TRAINING_SUCCEEDED);
                        if (s.at_state is not null)
                            s.at_state.ReceiveSignalPresent = true;
                    }
                    bit_reverse(buf2, buf, len);
                    non_ecm_put(s, buf2, len);
                }
                fe.timeout_rx_samples = fe.samples + milliseconds_to_samples(MID_RX_TIMEOUT);
                break;

            case T38_FIELD_T4_NON_ECM_SIG_END:
                if (t.CurrentRxDataType != data_type || t.CurrentRxFieldType != field_type) {
                    if (len > 0) {
                        if (s.at_state?.ReceiveSignalPresent != true) {
                            non_ecm_rx_status(s, SIG_STATUS_TRAINING_SUCCEEDED);
                            if (s.at_state is not null)
                                s.at_state.ReceiveSignalPresent = true;
                        }
                        bit_reverse(buf2, buf, len);
                        non_ecm_put(s, buf2, len);
                    }
                    non_ecm_rx_status(s, SIG_STATUS_CARRIER_DOWN);
                }
                if (s.at_state is not null)
                    s.at_state.ReceiveSignalPresent = false;
                fe.timeout_rx_samples = 0;
                break;

            case T38_FIELD_CM_MESSAGE:
                if (len >= 1)
                    span_log(s.logging, SPAN_LOG_FLOW, "CM profile %d - %s\n", buf[0] - (byte)'0', t38_cm_profile_to_str(buf[0]));
                else
                    span_log(s.logging, SPAN_LOG_FLOW, "Bad length for CM message - %d\n", len);
                break;

            case T38_FIELD_JM_MESSAGE:
                if (len >= 2)
                    span_log(s.logging, SPAN_LOG_FLOW, "JM - %s\n", t38_jm_to_str(buf.ToArray(), len));
                else
                    span_log(s.logging, SPAN_LOG_FLOW, "Bad length for JM message - %d\n", len);
                break;

            case T38_FIELD_CI_MESSAGE:
                if (len >= 1)
                    span_log(s.logging, SPAN_LOG_FLOW, "CI 0x%X\n", buf[0]);
                else
                    span_log(s.logging, SPAN_LOG_FLOW, "Bad length for CI message - %d\n", len);
                break;

            case T38_FIELD_V34RATE:
                if (len >= 3) {
                    fe.t38.V34Rate = t38_v34rate_to_bps(buf.ToArray(), len);
                    span_log(s.logging, SPAN_LOG_FLOW, "V.34 rate %d bps\n", fe.t38.V34Rate);
                } else {
                    span_log(s.logging, SPAN_LOG_FLOW, "Bad length for V34rate message - %d\n", len);
                }
                break;
        }
        return 0;
    }

    private static void send_hdlc(t31_state_t s, byte[] msg, int len) {
        if (len <= 0) {
            s.hdlc_tx.len = -1;
        } else {
            if (len >= 3) {
                if ((msg[2] & 0xFE) == T30_DCS) {
                    s.t38_fe.ecm_mode = len >= 7 && (msg[6] & DISBIT3) != 0 ? 1 : 0;
                    span_log(s.logging, SPAN_LOG_FLOW, "ECM mode: %d\n", s.t38_fe.ecm_mode);
                } else if (s.t38_fe.ecm_mode == 1 && (msg[2] & 0xFE) == T30_CFR) {
                    s.t38_fe.ecm_mode = 2;
                }
            }
            s.t38_fe.hdlc_tx.extra_bits = extra_bits_in_stuffed_frame(msg, len);
            bit_reverse(s.hdlc_tx.buf, msg, len);
            s.hdlc_tx.len = len;
            s.hdlc_tx.ptr = 0;
        }
    }

    private static int bits_to_us(t31_state_t s, int bits) {
        if (!s.t38_fe.t38.PaceTransmission || s.t38_fe.tx_bit_rate == 0)
            return 0;
        return bits * 1_000_000 / s.t38_fe.tx_bit_rate;
    }

    private static void set_octets_per_data_packet(t31_state_t s, int bit_rate) {
        s.t38_fe.tx_bit_rate = bit_rate;
        if (s.t38_fe.t38.PaceTransmission) {
            s.t38_fe.octets_per_data_packet =
                (s.t38_fe.t38.MicrosecondsPerTxChunk / 1000) * bit_rate / (8 * 1000);
            if (s.t38_fe.octets_per_data_packet < 1)
                s.t38_fe.octets_per_data_packet = 1;
        } else {
            s.t38_fe.octets_per_data_packet = MAX_OCTETS_PER_UNPACED_CHUNK;
        }
    }

    private static int set_no_signal(t31_state_t s) {
        int delay;
        if (((int)s.t38_fe.t38.ChunkingModes & T38_CHUNKING_SEND_REGULAR_INDICATORS) != 0) {
            delay = t38_core_send_indicator(s.t38_fe.t38, 0x100 | T38_IND_NO_SIGNAL);
            if (delay < 0)
                return delay;
            s.t38_fe.timed_step = T38_TIMED_STEP_NO_SIGNAL;
            if (((int)s.t38_fe.t38.ChunkingModes & T38_CHUNKING_SEND_2S_REGULAR_INDICATORS) != 0)
                s.t38_fe.timeout_tx_samples = s.t38_fe.next_tx_samples + microseconds_to_samples(2_000_000);
            else
                s.t38_fe.timeout_tx_samples = 0;
            return s.t38_fe.t38.MicrosecondsPerTxChunk;
        }
        delay = t38_core_send_indicator(s.t38_fe.t38, T38_IND_NO_SIGNAL);
        if (delay < 0)
            return delay;
        s.t38_fe.timed_step = T38_TIMED_STEP_NONE;
        return delay;
    }

    private static int stream_no_signal(t31_state_t s) {
        int delay = t38_core_send_indicator(s.t38_fe.t38, 0x100 | T38_IND_NO_SIGNAL);
        if (delay < 0)
            return delay;
        if (s.t38_fe.timeout_tx_samples != 0 && s.t38_fe.next_tx_samples >= s.t38_fe.timeout_tx_samples)
            s.t38_fe.timed_step = T38_TIMED_STEP_NONE;
        return s.t38_fe.t38.MicrosecondsPerTxChunk;
    }

    private static int stream_non_ecm(t31_state_t s) {
        t31_t38_front_end_state_t fe = s.t38_fe;
        byte[] buf = new byte[MAX_OCTETS_PER_UNPACED_CHUNK + 50];
        int delay = 0;

        while (delay == 0) {
            switch (fe.timed_step) {
                case T38_TIMED_STEP_NON_ECM_MODEM:
                    if (fe.t38.CurrentTxIndicator != T38_IND_NO_SIGNAL) {
                        delay = t38_core_send_indicator(fe.t38, T38_IND_NO_SIGNAL);
                        if (delay < 0)
                            return delay;
                    } else if (fe.t38.PaceTransmission) {
                        delay = 75000;
                    }
                    fe.timed_step = T38_TIMED_STEP_NON_ECM_MODEM_2;
                    fe.timeout_tx_samples = fe.next_tx_samples
                                          + microseconds_to_samples(t38_core_send_training_delay(fe.t38, fe.next_tx_indicator));
                    fe.next_tx_samples = fe.samples;
                    break;

                case T38_TIMED_STEP_NON_ECM_MODEM_2:
                    if (((int)fe.t38.ChunkingModes & T38_CHUNKING_SEND_REGULAR_INDICATORS) != 0) {
                        delay = t38_core_send_indicator(fe.t38, 0x100 | fe.next_tx_indicator);
                        if (delay < 0)
                            return delay;
                        if (fe.next_tx_samples >= fe.timeout_tx_samples)
                            fe.timed_step = T38_TIMED_STEP_NON_ECM_MODEM_3;
                        return fe.t38.MicrosecondsPerTxChunk;
                    }
                    delay = t38_core_send_indicator(fe.t38, fe.next_tx_indicator);
                    if (delay < 0)
                        return delay;
                    fe.timed_step = T38_TIMED_STEP_NON_ECM_MODEM_3;
                    break;

                case T38_TIMED_STEP_NON_ECM_MODEM_3: {
                        int len = non_ecm_get(s, buf, fe.octets_per_data_packet);
                        if (len > 0)
                            bit_reverse(buf, buf, len);
                        if (len < fe.octets_per_data_packet) {
                            if (fe.t38.PaceTransmission) {
                                Array.Clear(buf, len, fe.octets_per_data_packet - len);
                                fe.non_ecm_trailer_bytes = 3 * fe.octets_per_data_packet + len;
                                len = fe.octets_per_data_packet;
                                fe.timed_step = T38_TIMED_STEP_NON_ECM_MODEM_4;
                            } else {
                                int res = t38_core_send_data(
                                    fe.t38,
                                    fe.current_tx_data_type,
                                    T38_FIELD_T4_NON_ECM_SIG_END,
                                    buf,
                                    len,
                                    T38_PACKET_CATEGORY_IMAGE_DATA_END);
                                if (res < 0)
                                    return res;
                                fe.timed_step = T38_TIMED_STEP_NON_ECM_MODEM_5;
                                if (front_end_status(s, T30_FRONT_END_SEND_STEP_COMPLETE) < 0)
                                    return -1;
                                break;
                            }
                        }
                        int send_res = t38_core_send_data(
                            fe.t38,
                            fe.current_tx_data_type,
                            T38_FIELD_T4_NON_ECM_DATA,
                            buf,
                            len,
                            T38_PACKET_CATEGORY_IMAGE_DATA);
                        if (send_res < 0)
                            return send_res;
                        if (fe.t38.PaceTransmission)
                            delay = bits_to_us(s, 8 * len);
                        break;
                    }

                case T38_TIMED_STEP_NON_ECM_MODEM_4: {
                        int len = fe.octets_per_data_packet;
                        fe.non_ecm_trailer_bytes -= fe.octets_per_data_packet;
                        if (fe.non_ecm_trailer_bytes <= 0) {
                            len += fe.non_ecm_trailer_bytes;
                            Array.Clear(buf, 0, len);
                            int res = t38_core_send_data(
                                fe.t38,
                                fe.current_tx_data_type,
                                T38_FIELD_T4_NON_ECM_SIG_END,
                                buf,
                                len,
                                T38_PACKET_CATEGORY_IMAGE_DATA_END);
                            if (res < 0)
                                return res;
                            fe.timed_step = T38_TIMED_STEP_NON_ECM_MODEM_5;
                            if (fe.t38.PaceTransmission)
                                delay = bits_to_us(s, 8 * len) + 60000;
                            if (front_end_status(s, T30_FRONT_END_SEND_STEP_COMPLETE) < 0)
                                return -1;
                            break;
                        }
                        Array.Clear(buf, 0, len);
                        int send_res = t38_core_send_data(
                            fe.t38,
                            fe.current_tx_data_type,
                            T38_FIELD_T4_NON_ECM_DATA,
                            buf,
                            len,
                            T38_PACKET_CATEGORY_IMAGE_DATA);
                        if (send_res < 0)
                            return send_res;
                        if (fe.t38.PaceTransmission)
                            delay = bits_to_us(s, 8 * len);
                        break;
                    }

                case T38_TIMED_STEP_NON_ECM_MODEM_5:
                    delay = set_no_signal(s);
                    fe.timed_step = T38_TIMED_STEP_NONE;
                    return delay;

                default:
                    return delay;
            }
        }
        return delay;
    }

    private static int stream_hdlc(t31_state_t s) {
        t31_t38_front_end_state_t fe = s.t38_fe;
        byte[] buf = new byte[MAX_OCTETS_PER_UNPACED_CHUNK + 50];
        int category;
        int res;
        int delay = 0;

        while (delay == 0) {
            switch (fe.timed_step) {
                case T38_TIMED_STEP_HDLC_MODEM:
                    if (fe.t38.CurrentTxIndicator != T38_IND_NO_SIGNAL) {
                        delay = t38_core_send_indicator(fe.t38, T38_IND_NO_SIGNAL);
                        if (delay < 0)
                            return delay;
                    } else {
                        delay = fe.t38.PaceTransmission ? 75000 : 0;
                    }
                    fe.timed_step = T38_TIMED_STEP_HDLC_MODEM_2;
                    fe.timeout_tx_samples = fe.next_tx_samples
                                          + microseconds_to_samples(t38_core_send_training_delay(fe.t38, fe.next_tx_indicator))
                                          + microseconds_to_samples(t38_core_send_flags_delay(fe.t38, fe.next_tx_indicator))
                                          + microseconds_to_samples(delay);
                    fe.next_tx_samples = fe.samples;
                    break;

                case T38_TIMED_STEP_HDLC_MODEM_2:
                    if (((int)fe.t38.ChunkingModes & T38_CHUNKING_SEND_REGULAR_INDICATORS) != 0) {
                        delay = t38_core_send_indicator(fe.t38, 0x100 | fe.next_tx_indicator);
                        if (delay < 0)
                            return delay;
                        if (fe.next_tx_samples >= fe.timeout_tx_samples)
                            fe.timed_step = T38_TIMED_STEP_HDLC_MODEM_3;
                        return fe.t38.MicrosecondsPerTxChunk;
                    }
                    delay = t38_core_send_indicator(fe.t38, fe.next_tx_indicator);
                    if (delay < 0)
                        return delay;
                    delay += t38_core_send_flags_delay(fe.t38, fe.next_tx_indicator);
                    if (fe.current_tx_data_type == T38_DATA_V21)
                        at_put_response_code(s.at_state!, AT_RESPONSE_CODE_CONNECT);
                    fe.timed_step = T38_TIMED_STEP_HDLC_MODEM_3;
                    break;

                case T38_TIMED_STEP_HDLC_MODEM_3: {
                        if (s.hdlc_tx.len == 0) {
                            if (fe.current_tx_data_type != T38_DATA_V21
                                && s.t38_fe.hdlc_from_t31.@in != s.t38_fe.hdlc_from_t31.@out) {
                                t31_hdlc_buf_t source = s.t38_fe.hdlc_from_t31.buf[s.t38_fe.hdlc_from_t31.@out];
                                bit_reverse(s.hdlc_tx.buf, source.buf, source.len);
                                s.hdlc_tx.len = source.len;
                                s.hdlc_tx.ptr = 0;
                                if (++s.t38_fe.hdlc_from_t31.@out >= T31_TX_HDLC_BUFS)
                                    s.t38_fe.hdlc_from_t31.@out = 0;
                                if (s.t38_fe.hdlc_from_t31.@in == s.t38_fe.hdlc_from_t31.@out)
                                    s.hdlc_tx.final = s.non_ecm_tx.final;
                            } else {
                                delay = 30000;
                                break;
                            }
                        }

                        int i = s.hdlc_tx.len - s.hdlc_tx.ptr;
                        if (fe.octets_per_data_packet >= i) {
                            if (((int)fe.t38.ChunkingModes & T38_CHUNKING_MERGE_FCS_WITH_DATA) != 0) {
                                Array.Copy(s.hdlc_tx.buf, s.hdlc_tx.ptr, buf, 0, i);
                                var data_fields = new T38DataField[2];
                                data_fields[0] = new T38DataField(T38FieldType.HdlcData, new ReadOnlyMemory<byte>(buf, 0, i));

                                s.hdlc_tx.ptr = 0;
                                s.hdlc_tx.len = 0;
                                if (front_end_status(s, T30_FRONT_END_SEND_STEP_COMPLETE) < 0)
                                    return -1;

                                if (!s.hdlc_tx.final) {
                                    data_fields[1] = new T38DataField(T38FieldType.HdlcFcsOk, ReadOnlyMemory<byte>.Empty);
                                    category = fe.current_tx_data_type == T38_DATA_V21
                                        ? T38_PACKET_CATEGORY_CONTROL_DATA
                                        : T38_PACKET_CATEGORY_IMAGE_DATA;
                                    res = t38_core_send_data_multi_field(fe.t38, fe.current_tx_data_type, data_fields, 2, category);
                                    if (res < 0)
                                        return res;
                                    fe.timed_step = T38_TIMED_STEP_HDLC_MODEM_3;
                                    delay = bits_to_us(s, i * 8 + fe.hdlc_tx.extra_bits);
                                    if (fe.current_tx_data_type == T38_DATA_V21)
                                        at_put_response_code(s.at_state!, AT_RESPONSE_CODE_CONNECT);
                                } else {
                                    data_fields[1] = new T38DataField(T38FieldType.HdlcFcsOkSignalEnd, ReadOnlyMemory<byte>.Empty);
                                    category = fe.current_tx_data_type == T38_DATA_V21
                                        ? T38_PACKET_CATEGORY_CONTROL_DATA_END
                                        : T38_PACKET_CATEGORY_IMAGE_DATA_END;
                                    res = t38_core_send_data_multi_field(fe.t38, fe.current_tx_data_type, data_fields, 2, category);
                                    if (res < 0)
                                        return res;
                                    fe.timed_step = T38_TIMED_STEP_HDLC_MODEM_5;
                                    delay = bits_to_us(s, i * 8 + fe.hdlc_tx.extra_bits);
                                    if (fe.t38.PaceTransmission)
                                        delay += 100000;
                                    at_put_response_code(s.at_state!, AT_RESPONSE_CODE_OK);
                                    t31_set_at_rx_mode(s, AT_MODE_OFFHOOK_COMMAND);
                                }
                                break;
                            }

                            category = fe.current_tx_data_type == T38_DATA_V21
                                ? T38_PACKET_CATEGORY_CONTROL_DATA
                                : T38_PACKET_CATEGORY_IMAGE_DATA;
                            byte[] part = new byte[i];
                            Array.Copy(s.hdlc_tx.buf, s.hdlc_tx.ptr, part, 0, i);
                            res = t38_core_send_data(
                                fe.t38,
                                fe.current_tx_data_type,
                                T38_FIELD_HDLC_DATA,
                                part,
                                i,
                                category);
                            if (res < 0)
                                return res;
                            fe.timed_step = T38_TIMED_STEP_HDLC_MODEM_4;
                        } else {
                            i = fe.octets_per_data_packet;
                            category = fe.current_tx_data_type == T38_DATA_V21
                                ? T38_PACKET_CATEGORY_CONTROL_DATA
                                : T38_PACKET_CATEGORY_IMAGE_DATA;
                            byte[] part = new byte[i];
                            Array.Copy(s.hdlc_tx.buf, s.hdlc_tx.ptr, part, 0, i);
                            res = t38_core_send_data(
                                fe.t38,
                                fe.current_tx_data_type,
                                T38_FIELD_HDLC_DATA,
                                part,
                                i,
                                category);
                            if (res < 0)
                                return res;
                            s.hdlc_tx.ptr += i;
                        }
                        delay = bits_to_us(s, i * 8);
                        break;
                    }

                case T38_TIMED_STEP_HDLC_MODEM_4: {
                        int previous = fe.current_tx_data_type;
                        s.hdlc_tx.ptr = 0;
                        s.hdlc_tx.len = 0;
                        if (!s.hdlc_tx.final) {
                            category = fe.current_tx_data_type == T38_DATA_V21
                                ? T38_PACKET_CATEGORY_CONTROL_DATA
                                : T38_PACKET_CATEGORY_IMAGE_DATA;
                            res = t38_core_send_data(fe.t38, previous, T38_FIELD_HDLC_FCS_OK, null, 0, category);
                            if (res < 0)
                                return res;
                            fe.timed_step = T38_TIMED_STEP_HDLC_MODEM_3;
                            if (fe.current_tx_data_type == T38_DATA_V21)
                                at_put_response_code(s.at_state!, AT_RESPONSE_CODE_CONNECT);
                            delay = bits_to_us(s, fe.hdlc_tx.extra_bits);
                        } else {
                            s.hdlc_tx.final = false;
                            category = fe.current_tx_data_type == T38_DATA_V21
                                ? T38_PACKET_CATEGORY_CONTROL_DATA_END
                                : T38_PACKET_CATEGORY_IMAGE_DATA_END;
                            res = t38_core_send_data(fe.t38, previous, T38_FIELD_HDLC_FCS_OK_SIG_END, null, 0, category);
                            if (res < 0)
                                return res;
                            fe.timed_step = T38_TIMED_STEP_HDLC_MODEM_5;
                            delay = bits_to_us(s, fe.hdlc_tx.extra_bits);
                            if (fe.t38.PaceTransmission)
                                delay += 100000;
                            if (front_end_status(s, T30_FRONT_END_SEND_STEP_COMPLETE) < 0)
                                return -1;
                        }
                        break;
                    }

                case T38_TIMED_STEP_HDLC_MODEM_5:
                    delay = set_no_signal(s);
                    fe.timed_step = T38_TIMED_STEP_NONE;
                    at_put_response_code(s.at_state!, AT_RESPONSE_CODE_OK);
                    t31_set_at_rx_mode(s, AT_MODE_OFFHOOK_COMMAND);
                    return delay;

                default:
                    return delay;
            }
        }
        return delay;
    }

    private static int stream_ced(t31_state_t s) {
        t31_t38_front_end_state_t fe = s.t38_fe;
        int delay = 0;
        while (delay == 0) {
            switch (fe.timed_step) {
                case T38_TIMED_STEP_CED:
                    fe.timed_step = T38_TIMED_STEP_CED_2;
                    delay = t38_core_send_indicator(fe.t38, T38_IND_NO_SIGNAL);
                    if (delay < 0)
                        return delay;
                    delay = fe.t38.PaceTransmission ? 200000 : 0;
                    fe.next_tx_samples = fe.samples;
                    break;
                case T38_TIMED_STEP_CED_2:
                    fe.timed_step = T38_TIMED_STEP_CED_3;
                    delay = t38_core_send_indicator(fe.t38, T38_IND_CED);
                    if (delay < 0)
                        return delay;
                    fe.current_tx_data_type = T38_DATA_NONE;
                    break;
                case T38_TIMED_STEP_CED_3:
                    fe.timed_step = T38_TIMED_STEP_NONE;
                    if (front_end_status(s, T30_FRONT_END_SEND_STEP_COMPLETE) < 0)
                        return -1;
                    return 0;
                default:
                    return delay;
            }
        }
        return delay;
    }

    private static int stream_cng(t31_state_t s) {
        t31_t38_front_end_state_t fe = s.t38_fe;
        int delay = 0;
        while (delay == 0) {
            switch (fe.timed_step) {
                case T38_TIMED_STEP_CNG:
                    fe.timed_step = T38_TIMED_STEP_CNG_2;
                    delay = t38_core_send_indicator(fe.t38, T38_IND_NO_SIGNAL);
                    if (delay < 0)
                        return delay;
                    delay = fe.t38.PaceTransmission ? 200000 : 0;
                    fe.next_tx_samples = fe.samples;
                    break;
                case T38_TIMED_STEP_CNG_2:
                    delay = t38_core_send_indicator(fe.t38, T38_IND_CNG);
                    fe.timed_step = T38_TIMED_STEP_NONE;
                    fe.current_tx_data_type = T38_DATA_NONE;
                    return delay;
                default:
                    return delay;
            }
        }
        return delay;
    }

    public static int t31_t38_send_timeout(t31_state_t s, int samples) {
        ArgumentNullException.ThrowIfNull(s);
        t31_t38_front_end_state_t fe = s.t38_fe;
        if (fe.current_rx_type == T30_MODEM_DONE || fe.current_tx_type == T30_MODEM_DONE)
            return 1;

        fe.samples += samples;
        if (fe.timeout_rx_samples != 0 && fe.samples > fe.timeout_rx_samples) {
            span_log(s.logging, SPAN_LOG_FLOW, "Timeout mid-receive\n");
            fe.timeout_rx_samples = 0;
            front_end_status(s, T30_FRONT_END_RECEIVE_COMPLETE);
        }
        if (fe.timed_step == T38_TIMED_STEP_NONE)
            return 0;
        if (fe.t38.PaceTransmission && fe.samples < fe.next_tx_samples)
            return 0;

        int delay = 0;
        switch (fe.timed_step & 0xFFF0) {
            case T38_TIMED_STEP_NON_ECM_MODEM:
                delay = stream_non_ecm(s);
                break;
            case T38_TIMED_STEP_HDLC_MODEM:
                delay = stream_hdlc(s);
                break;
            case T38_TIMED_STEP_CED:
                delay = stream_ced(s);
                break;
            case T38_TIMED_STEP_CNG:
                delay = stream_cng(s);
                break;
            case T38_TIMED_STEP_PAUSE:
                fe.timed_step = T38_TIMED_STEP_NONE;
                front_end_status(s, T30_FRONT_END_SEND_STEP_COMPLETE);
                break;
            case T38_TIMED_STEP_NO_SIGNAL:
                delay = stream_no_signal(s);
                break;
        }
        fe.next_tx_samples += microseconds_to_samples(delay);
        return 0;
    }

    private static int t31_modem_control_handler(object? user_data, int op, string? num) {
        var s = (t31_state_t)user_data!;
        switch (op) {
            case AT_MODEM_CONTROL_CALL:
                s.call_samples = 0;
                t38_core_restart(s.t38_fe.t38);
                break;
            case AT_MODEM_CONTROL_ANSWER:
                s.call_samples = 0;
                t38_core_restart(s.t38_fe.t38);
                break;
            case AT_MODEM_CONTROL_ONHOOK:
                if (s.non_ecm_tx.holding) {
                    s.non_ecm_tx.holding = false;
                    if (s.at_state is not null)
                        at_modem_control(s.at_state, AT_MODEM_CONTROL_CTS, "1");
                }
                if (s.at_state?.ReceiveSignalPresent == true) {
                    s.at_rx_data[s.at_rx_data_bytes++] = DLE;
                    s.at_rx_data[s.at_rx_data_bytes++] = ETX;
                    s.at_tx_handler?.Invoke(s.at_tx_user_data, s.at_rx_data.AsSpan(0, s.at_rx_data_bytes));
                    s.at_rx_data_bytes = 0;
                }
                restart_modem(s, FAX_MODEM_SILENCE_TX);
                break;
            case AT_MODEM_CONTROL_RESTART:
                if (int.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out int new_modem)) {
                    if (new_modem == FAX_MODEM_FLUSH)
                        s.do_hangup = true;
                    return restart_modem(s, new_modem);
                }
                return -1;
            case AT_MODEM_CONTROL_DTE_TIMEOUT:
                if (long.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out long timeout_ms))
                    s.dte_data_timeout = s.call_samples + milliseconds_to_samples((int)timeout_ms);
                else
                    s.dte_data_timeout = 0;
                return 0;
        }
        return s.modem_control_handler?.Invoke(s, s.modem_control_user_data, op, num) ?? 0;
    }

    private static void non_ecm_rx_status(t31_state_t s, int status) {
        switch (status) {
            case SIG_STATUS_TRAINING_IN_PROGRESS:
                break;
            case SIG_STATUS_TRAINING_FAILED:
                if (s.at_state is not null)
                    s.at_state.ReceiveTrained = false;
                s.audio.modems.RxTrained = false;
                break;
            case SIG_STATUS_TRAINING_SUCCEEDED:
                at_put_response_code(s.at_state!, AT_RESPONSE_CODE_CONNECT);
                if (s.at_state is not null) {
                    s.at_state.ReceiveSignalPresent = true;
                    s.at_state.ReceiveTrained = true;
                }
                s.audio.modems.RxTrained = true;
                break;
            case SIG_STATUS_CARRIER_UP:
                break;
            case SIG_STATUS_CARRIER_DOWN:
                if (s.at_state?.ReceiveSignalPresent == true) {
                    s.at_rx_data[s.at_rx_data_bytes++] = DLE;
                    s.at_rx_data[s.at_rx_data_bytes++] = ETX;
                    s.at_tx_handler?.Invoke(s.at_tx_user_data, s.at_rx_data.AsSpan(0, s.at_rx_data_bytes));
                    s.at_rx_data_bytes = 0;
                    at_put_response_code(s.at_state!, AT_RESPONSE_CODE_NO_CARRIER);
                    t31_set_at_rx_mode(s, AT_MODE_OFFHOOK_COMMAND);
                }
                if (s.at_state is not null) {
                    s.at_state.ReceiveSignalPresent = false;
                    s.at_state.ReceiveTrained = false;
                }
                s.audio.modems.RxTrained = false;
                break;
            default:
                if (s.at_state?.Profile.ResultCodeFormat != AtResultCodeFormat.None)
                    span_log(s.logging, SPAN_LOG_FLOW, "Eh!\n");
                break;
        }
    }

    private static void non_ecm_put_bit(t31_state_t s, int bit) {
        if (bit < 0) {
            non_ecm_rx_status(s, bit);
            return;
        }
        s.audio.current_byte = (s.audio.current_byte >> 1) | (bit << 7);
        if (++s.audio.bit_no >= 8) {
            if (s.audio.current_byte == DLE)
                s.at_rx_data[s.at_rx_data_bytes++] = DLE;
            s.at_rx_data[s.at_rx_data_bytes++] = (byte)s.audio.current_byte;
            if (s.at_rx_data_bytes >= 250) {
                s.at_tx_handler?.Invoke(s.at_tx_user_data, s.at_rx_data.AsSpan(0, s.at_rx_data_bytes));
                s.at_rx_data_bytes = 0;
            }
            s.audio.bit_no = 0;
            s.audio.current_byte = 0;
        }
    }

    private static void non_ecm_put(t31_state_t s, byte[] buf, int len) {
        if (s.at_state?.ReceiveSignalPresent != true) {
            non_ecm_rx_status(s, SIG_STATUS_TRAINING_SUCCEEDED);
            if (s.at_state is not null)
                s.at_state.ReceiveSignalPresent = true;
        }
        for (int i = 0; i < len; i++) {
            if (buf[i] == DLE)
                s.at_rx_data[s.at_rx_data_bytes++] = DLE;
            s.at_rx_data[s.at_rx_data_bytes++] = buf[i];
            if (s.at_rx_data_bytes >= 250) {
                s.at_tx_handler?.Invoke(s.at_tx_user_data, s.at_rx_data.AsSpan(0, s.at_rx_data_bytes));
                s.at_rx_data_bytes = 0;
            }
        }
        s.audio.bit_no = 0;
        s.audio.current_byte = 0;
    }

    private static int non_ecm_get_bit(t31_state_t s) {
        if (s.audio.bit_no <= 0) {
            if (s.non_ecm_tx.out_bytes != s.non_ecm_tx.in_bytes) {
                s.audio.current_byte = s.non_ecm_tx.buf[s.non_ecm_tx.out_bytes++];
                if (s.non_ecm_tx.out_bytes > T31_TX_BUF_LEN - 1) {
                    s.non_ecm_tx.out_bytes = T31_TX_BUF_LEN - 1;
                    span_log(s.logging, SPAN_LOG_FLOW, "End of transmit buffer reached!\n");
                }
                if (s.non_ecm_tx.holding) {
                    if (s.non_ecm_tx.out_bytes > T31_TX_BUF_LOW_TIDE) {
                        s.non_ecm_tx.holding = false;
                        if (s.at_state is not null)
                            at_modem_control(s.at_state, AT_MODEM_CONTROL_CTS, "1");
                    }
                }
                s.non_ecm_tx.data_started = true;
            } else {
                if (s.non_ecm_tx.final) {
                    s.non_ecm_tx.final = false;
                    return SIG_STATUS_END_OF_DATA;
                }
                s.audio.current_byte = s.non_ecm_tx.data_started ? 0x00 : 0xFF;
            }
            s.audio.bit_no = 8;
        }
        s.audio.bit_no--;
        int bit = s.audio.current_byte & 1;
        s.audio.current_byte >>= 1;
        return bit;
    }

    private static int non_ecm_get(t31_state_t s, byte[] buf, int len) {
        int i;
        for (i = 0; i < len; i++) {
            if (s.non_ecm_tx.out_bytes != s.non_ecm_tx.in_bytes) {
                buf[i] = s.non_ecm_tx.buf[s.non_ecm_tx.out_bytes++];
                if (s.non_ecm_tx.out_bytes > T31_TX_BUF_LEN - 1) {
                    s.non_ecm_tx.out_bytes = T31_TX_BUF_LEN - 1;
                    span_log(s.logging, SPAN_LOG_FLOW, "End of transmit buffer reached!\n");
                }
                if (s.non_ecm_tx.holding) {
                    if (s.non_ecm_tx.out_bytes > T31_TX_BUF_LOW_TIDE) {
                        s.non_ecm_tx.holding = false;
                        if (s.at_state is not null)
                            at_modem_control(s.at_state, AT_MODEM_CONTROL_CTS, "1");
                    }
                }
                s.non_ecm_tx.data_started = true;
            } else {
                if (s.non_ecm_tx.final) {
                    s.non_ecm_tx.final = false;
                    return i;
                }
                buf[i] = s.non_ecm_tx.data_started ? (byte)0x00 : (byte)0xFF;
            }
        }
        s.audio.bit_no = 0;
        s.audio.current_byte = 0;
        return len;
    }

    private static void tone_detected(t31_state_t s, int tone, int level, int delay) {
        span_log(s.logging, SPAN_LOG_FLOW, "%s detected (%ddBm0)\n", s.audio.modems.ConnectToneToString(tone), level);
    }

    private static void v8_handler(object? user_data, V8Parameters result) {
        var s = (t31_state_t)user_data!;
        span_log(s.logging, SPAN_LOG_FLOW, "V.8 report received\n");
    }

    private static void hdlc_tx_underflow(t31_state_t s) {
        if (s.hdlc_tx.final) {
            s.hdlc_tx.final = false;
            fax_modems_hdlc_tx_frame(s.audio.modems, null, 0);
        } else {
            at_put_response_code(s.at_state!, AT_RESPONSE_CODE_CONNECT);
        }
    }

    private static void hdlc_tx_underflow2(object? user_data) {
    }

    private static void hdlc_rx_status(t31_state_t s, int status) {
        byte[] buf = new byte[2];
        switch (status) {
            case SIG_STATUS_TRAINING_IN_PROGRESS:
                break;
            case SIG_STATUS_TRAINING_FAILED:
                if (s.at_state is not null)
                    s.at_state.ReceiveTrained = false;
                s.audio.modems.RxTrained = false;
                break;
            case SIG_STATUS_TRAINING_SUCCEEDED:
                if (s.at_state is not null) {
                    s.at_state.ReceiveSignalPresent = true;
                    s.at_state.ReceiveTrained = true;
                }
                s.audio.modems.RxTrained = true;
                break;
            case SIG_STATUS_CARRIER_UP:
                if (s.modem == FAX_MODEM_CNG_TONE_TX
                    || s.modem == FAX_MODEM_NOCNG_TONE_TX
                    || s.modem == FAX_MODEM_V21_RX) {
                    if (s.at_state is not null)
                        s.at_state.ReceiveSignalPresent = true;
                    s.rx_frame_received = false;
                    s.audio.modems.RxFrameReceived = false;
                }
                break;
            case SIG_STATUS_CARRIER_DOWN:
                if (s.rx_frame_received) {
                    if (s.at_state?.DteIsWaiting == true) {
                        if (s.at_state?.OkIsPending == true) {
                            at_put_response_code(s.at_state!, AT_RESPONSE_CODE_OK);
                            s.at_state.OkIsPending = false;
                        } else {
                            at_put_response_code(s.at_state!, AT_RESPONSE_CODE_NO_CARRIER);
                        }
                        s.at_state!.DteIsWaiting = false;
                        t31_set_at_rx_mode(s, AT_MODE_OFFHOOK_COMMAND);
                    } else if (s.rx_queue is not null) {
                        buf[0] = AT_RESPONSE_CODE_NO_CARRIER;
                        queue_write_msg(s.rx_queue, buf, 1);
                    }
                }
                if (s.at_state is not null) {
                    s.at_state.ReceiveSignalPresent = false;
                    s.at_state.ReceiveTrained = false;
                }
                s.audio.modems.RxTrained = false;
                break;
            case SIG_STATUS_FRAMING_OK:
                if (s.modem == FAX_MODEM_CNG_TONE_TX || s.modem == FAX_MODEM_NOCNG_TONE_TX) {
                    s.modem = FAX_MODEM_V21_RX;
                    s.transmit = false;
                }
                if (s.modem == FAX_MODEM_V17_RX || s.modem == FAX_MODEM_V27TER_RX || s.modem == FAX_MODEM_V29_RX) {
                    if (s.at_state?.Profile.AdaptiveReceive != 0) {
                        if (s.at_state is not null)
                            s.at_state.ReceiveSignalPresent = true;
                        s.rx_frame_received = true;
                        s.audio.modems.RxFrameReceived = true;
                        s.modem = FAX_MODEM_V21_RX;
                        s.transmit = false;
                        s.at_state!.DteIsWaiting = true;
                        at_put_response_code(s.at_state!, AT_RESPONSE_CODE_FRH3);
                        at_put_response_code(s.at_state!, AT_RESPONSE_CODE_CONNECT);
                    } else {
                        s.modem = FAX_MODEM_SILENCE_TX;
                        t31_set_at_rx_mode(s, AT_MODE_OFFHOOK_COMMAND);
                        s.rx_frame_received = false;
                        s.audio.modems.RxFrameReceived = false;
                        at_put_response_code(s.at_state!, AT_RESPONSE_CODE_FCERROR);
                    }
                } else if (!s.rx_frame_received) {
                    if (s.at_state?.DteIsWaiting == true) {
                        at_put_response_code(s.at_state!, AT_RESPONSE_CODE_CONNECT);
                    } else if (s.rx_queue is not null) {
                        buf[0] = AT_RESPONSE_CODE_CONNECT;
                        queue_write_msg(s.rx_queue, buf, 1);
                    }
                    s.rx_frame_received = true;
                    s.audio.modems.RxFrameReceived = true;
                }
                break;
            case SIG_STATUS_ABORT:
                break;
            default:
                span_log(s.logging, SPAN_LOG_WARNING, "Unexpected HDLC rx status - %d!\n", status);
                break;
        }
    }

    private static void hdlc_accept_frame(t31_state_t s, byte[] msg, int len, bool ok) {
        if (len < 0) {
            hdlc_rx_status(s, len);
            return;
        }

        byte[] buf = new byte[Math.Max(256, len + 3)];
        if (!s.rx_frame_received) {
            if (s.at_state?.DteIsWaiting == true) {
                at_put_response_code(s.at_state!, AT_RESPONSE_CODE_CONNECT);
            } else if (s.rx_queue is not null) {
                buf[0] = AT_RESPONSE_CODE_CONNECT;
                queue_write_msg(s.rx_queue, buf, 1);
            }
            s.rx_frame_received = true;
            s.audio.modems.RxFrameReceived = true;
        }

        if (s.at_state?.OkIsPending != true) {
            if (s.at_state?.DteIsWaiting == true) {
                int count = Math.Min(len + 2, msg.Length);
                for (int i = 0; i < count; i++) {
                    if (msg[i] == DLE)
                        s.at_rx_data[s.at_rx_data_bytes++] = DLE;
                    s.at_rx_data[s.at_rx_data_bytes++] = msg[i];
                }
                s.at_rx_data[s.at_rx_data_bytes++] = DLE;
                s.at_rx_data[s.at_rx_data_bytes++] = ETX;
                s.at_tx_handler?.Invoke(s.at_tx_user_data, s.at_rx_data.AsSpan(0, s.at_rx_data_bytes));
                s.at_rx_data_bytes = 0;
                if (len > 1 && msg[1] == 0x13 && ok) {
                    if (s.at_state is not null)
                        s.at_state.OkIsPending = true;
                } else {
                    at_put_response_code(s.at_state!, ok ? AT_RESPONSE_CODE_OK : AT_RESPONSE_CODE_ERROR);
                    s.at_state!.DteIsWaiting = false;
                    s.rx_frame_received = false;
                    s.audio.modems.RxFrameReceived = false;
                }
            } else if (s.rx_queue is not null) {
                buf[0] = ok ? (byte)AT_RESPONSE_CODE_OK : (byte)AT_RESPONSE_CODE_ERROR;
                int count = Math.Min(len + 2, msg.Length);
                Array.Copy(msg, 0, buf, 1, count);
                queue_write_msg(s.rx_queue, buf, count + 1);
            }
        }
        t31_set_at_rx_mode(s, AT_MODE_OFFHOOK_COMMAND);
    }

    private static void hdlc_accept_t38_frame(t31_state_t s, byte[] msg, int len, bool ok) {
        if (len < 0)
            return;

        span_log(s.logging, SPAN_LOG_FLOW, "Accept2 %d %d\n", len, ok ? 1 : 0);
        ushort crc = crc_itu16_calc(msg, len, 0xFFFF);
        if (ok)
            crc ^= 0xFFFF;

        byte[] buf2 = new byte[2 * len + 20];
        int ptr = 0;
        buf2[ptr++] = s.t38_fe.hdlc_tx_non_ecm_idle_octet;
        buf2[ptr++] = s.t38_fe.hdlc_tx_non_ecm_idle_octet;

        for (int pos = 0; pos < len; pos++) {
            int byte_in_progress = msg[pos];
            int i = bottom_bit((uint)(byte_in_progress | 0x100));
            s.t38_fe.hdlc_tx_non_ecm_octets_in_progress <<= i;
            byte_in_progress >>= i;
            for (; i < 8; i++) {
                s.t38_fe.hdlc_tx_non_ecm_octets_in_progress =
                    (s.t38_fe.hdlc_tx_non_ecm_octets_in_progress << 1) | (byte_in_progress & 0x01);
                byte_in_progress >>= 1;
                if ((s.t38_fe.hdlc_tx_non_ecm_octets_in_progress & 0x1F) == 0x1F) {
                    s.t38_fe.hdlc_tx_non_ecm_octets_in_progress <<= 1;
                    s.t38_fe.hdlc_tx_non_ecm_num_bits++;
                }
            }
            buf2[ptr++] = (byte)((s.t38_fe.hdlc_tx_non_ecm_octets_in_progress >> s.t38_fe.hdlc_tx_non_ecm_num_bits) & 0xFF);
            if (s.t38_fe.hdlc_tx_non_ecm_num_bits >= 8) {
                s.t38_fe.hdlc_tx_non_ecm_num_bits -= 8;
                buf2[ptr++] = (byte)((s.t38_fe.hdlc_tx_non_ecm_octets_in_progress >> s.t38_fe.hdlc_tx_non_ecm_num_bits) & 0xFF);
            }
        }

        for (int pos = 0; pos < 2; pos++) {
            int byte_in_progress = crc & 0xFF;
            crc >>= 8;
            int i = bottom_bit((uint)(byte_in_progress | 0x100));
            s.t38_fe.hdlc_tx_non_ecm_octets_in_progress <<= i;
            byte_in_progress >>= i;
            for (; i < 8; i++) {
                s.t38_fe.hdlc_tx_non_ecm_octets_in_progress =
                    (s.t38_fe.hdlc_tx_non_ecm_octets_in_progress << 1) | (byte_in_progress & 0x01);
                byte_in_progress >>= 1;
                if ((s.t38_fe.hdlc_tx_non_ecm_octets_in_progress & 0x1F) == 0x1F) {
                    s.t38_fe.hdlc_tx_non_ecm_octets_in_progress <<= 1;
                    s.t38_fe.hdlc_tx_non_ecm_num_bits++;
                }
            }
            buf2[ptr++] = (byte)((s.t38_fe.hdlc_tx_non_ecm_octets_in_progress >> s.t38_fe.hdlc_tx_non_ecm_num_bits) & 0xFF);
            if (s.t38_fe.hdlc_tx_non_ecm_num_bits >= 8) {
                s.t38_fe.hdlc_tx_non_ecm_num_bits -= 8;
                buf2[ptr++] = (byte)((s.t38_fe.hdlc_tx_non_ecm_octets_in_progress >> s.t38_fe.hdlc_tx_non_ecm_num_bits) & 0xFF);
            }
        }

        int txbyte = (byte)((s.t38_fe.hdlc_tx_non_ecm_octets_in_progress << (8 - s.t38_fe.hdlc_tx_non_ecm_num_bits))
                            | (0x7E >> s.t38_fe.hdlc_tx_non_ecm_num_bits));
        s.t38_fe.hdlc_tx_non_ecm_idle_octet =
            (byte)((0x7E7E >> s.t38_fe.hdlc_tx_non_ecm_num_bits) & 0xFF);
        s.t38_fe.hdlc_tx_non_ecm_octets_in_progress =
            s.t38_fe.hdlc_tx_non_ecm_idle_octet >> (8 - s.t38_fe.hdlc_tx_non_ecm_num_bits);
        buf2[ptr++] = (byte)txbyte;
        buf2[ptr++] = s.t38_fe.hdlc_tx_non_ecm_idle_octet;
        buf2[ptr++] = s.t38_fe.hdlc_tx_non_ecm_idle_octet;
        bit_reverse(buf2, buf2, ptr);
        non_ecm_put(s, buf2, ptr);
    }

    private static void hdlc_accept_non_ecm_frame(t31_state_t s, ReadOnlyMemory<byte>? message, int len, bool ok) {
        if (len < 0 || message is null)
            return;
        t31_hdlc_buf_t target = s.t38_fe.hdlc_from_t31.buf[s.t38_fe.hdlc_from_t31.@in];
        int copy = Math.Min(len, Math.Min(message.Value.Length, target.buf.Length));
        message.Value.Span[..copy].CopyTo(target.buf);
        target.len = (short)copy;
        if (++s.t38_fe.hdlc_from_t31.@in >= T31_TX_HDLC_BUFS)
            s.t38_fe.hdlc_from_t31.@in = 0;
    }

    private static void t31_v21_rx(t31_state_t s) {
        if (s.at_state is not null)
            s.at_state.OkIsPending = false;
        s.hdlc_tx.len = 0;
        s.hdlc_tx.final = false;
        s.dled = false;
        fax_modems_start_slow_modem(s.audio.modems, FAX_MODEM_V21_RX);
        s.audio.modems.InitializeHdlcReceiver(false, true, HDLC_FRAMING_OK_THRESHOLD);
        s.rx_frame_received = false;
        s.audio.modems.RxFrameReceived = false;
        s.transmit = true;
    }

    private static int restart_modem(t31_state_t s, int new_modem) {
        int use_hdlc = 0;
        fax_modems_state_t t = s.audio.modems;

        span_log(s.logging, SPAN_LOG_FLOW, "Restart modem %d\n", new_modem);
        if (s.modem == new_modem)
            return 0;
        if (s.rx_queue is not null)
            queue_flush(s.rx_queue);
        s.modem = new_modem;
        s.non_ecm_tx.final = false;
        if (s.at_state is not null) {
            s.at_state.ReceiveSignalPresent = false;
            s.at_state.ReceiveTrained = false;
        }
        s.audio.modems.RxTrained = false;
        s.rx_frame_received = false;
        s.audio.modems.RxFrameReceived = false;
        t.SetReceiveIdle();

        switch (s.modem) {
            case FAX_MODEM_CNG_TONE_TX:
                if (s.t38_mode) {
                    s.t38_fe.next_tx_samples = s.t38_fe.samples;
                    s.t38_fe.timed_step = T38_TIMED_STEP_CNG;
                    s.t38_fe.current_tx_data_type = T38_DATA_NONE;
                } else {
                    fax_modems_start_slow_modem(t, FAX_MODEM_CNG_TONE_TX);
                    t31_v21_rx(s);
                    fax_modems_set_next_tx_handler(t, null);
                }
                s.transmit = true;
                break;

            case FAX_MODEM_NOCNG_TONE_TX:
                if (!s.t38_mode) {
                    t31_v21_rx(s);
                    fax_modems_start_slow_modem(t, FAX_MODEM_NOCNG_TONE_TX);
                }
                s.transmit = false;
                break;

            case FAX_MODEM_CED_TONE_TX:
                if (s.t38_mode) {
                    s.t38_fe.next_tx_samples = s.t38_fe.samples;
                    s.t38_fe.timed_step = T38_TIMED_STEP_CED;
                    s.t38_fe.current_tx_data_type = T38_DATA_NONE;
                } else {
                    fax_modems_start_slow_modem(t, FAX_MODEM_CED_TONE_TX);
                    fax_modems_set_next_tx_handler(t, null);
                }
                s.transmit = true;
                break;

            case FAX_MODEM_V21_RX:
                if (!s.t38_mode)
                    t31_v21_rx(s);
                break;

            case FAX_MODEM_V21_TX:
                if (s.t38_mode) {
                    s.t38_fe.next_tx_indicator = T38_IND_V21_PREAMBLE;
                    s.t38_fe.current_tx_data_type = T38_DATA_V21;
                    s.t38_fe.timed_step = T38_TIMED_STEP_HDLC_MODEM;
                    set_octets_per_data_packet(s, 300);
                } else {
                    t.InitializeHdlcTransmitter(false);
                    fax_modems_hdlc_tx_flags(t, 32);
                    fax_modems_start_slow_modem(t, FAX_MODEM_V21_TX);
                    fax_modems_set_next_tx_handler(t, null);
                }
                s.hdlc_tx.len = 0;
                s.hdlc_tx.final = false;
                s.dled = false;
                s.transmit = true;
                break;

            case FAX_MODEM_V17_RX:
            case FAX_MODEM_V27TER_RX:
            case FAX_MODEM_V29_RX:
                if (!s.t38_mode) {
                    t31_v21_rx(s);
                    fax_modems_start_fast_modem(t, s.modem, s.bit_rate, s.short_train ? 1 : 0, use_hdlc);
                }
                s.transmit = false;
                break;

            case FAX_MODEM_V17_TX:
                if (s.t38_mode) {
                    switch (s.bit_rate) {
                        case 7200:
                            s.t38_fe.next_tx_indicator = s.short_train
                                ? T38_IND_V17_7200_SHORT_TRAINING
                                : T38_IND_V17_7200_LONG_TRAINING;
                            s.t38_fe.current_tx_data_type = T38_DATA_V17_7200;
                            break;
                        case 9600:
                            s.t38_fe.next_tx_indicator = s.short_train
                                ? T38_IND_V17_9600_SHORT_TRAINING
                                : T38_IND_V17_9600_LONG_TRAINING;
                            s.t38_fe.current_tx_data_type = T38_DATA_V17_9600;
                            break;
                        case 12000:
                            s.t38_fe.next_tx_indicator = s.short_train
                                ? T38_IND_V17_12000_SHORT_TRAINING
                                : T38_IND_V17_12000_LONG_TRAINING;
                            s.t38_fe.current_tx_data_type = T38_DATA_V17_12000;
                            break;
                        case 14400:
                            s.t38_fe.next_tx_indicator = s.short_train
                                ? T38_IND_V17_14400_SHORT_TRAINING
                                : T38_IND_V17_14400_LONG_TRAINING;
                            s.t38_fe.current_tx_data_type = T38_DATA_V17_14400;
                            break;
                    }
                    set_octets_per_data_packet(s, s.bit_rate);
                    s.t38_fe.timed_step = s.t38_fe.ecm_mode == 2
                        ? T38_TIMED_STEP_HDLC_MODEM
                        : T38_TIMED_STEP_NON_ECM_MODEM;
                } else {
                    fax_modems_start_fast_modem(t, s.modem, s.bit_rate, s.short_train ? 1 : 0, use_hdlc);
                }
                s.non_ecm_tx.out_bytes = 0;
                s.non_ecm_tx.data_started = false;
                s.transmit = true;
                break;

            case FAX_MODEM_V27TER_TX:
                if (s.t38_mode) {
                    switch (s.bit_rate) {
                        case 2400:
                            s.t38_fe.next_tx_indicator = T38_IND_V27TER_2400_TRAINING;
                            s.t38_fe.current_tx_data_type = T38_DATA_V27TER_2400;
                            break;
                        case 4800:
                            s.t38_fe.next_tx_indicator = T38_IND_V27TER_4800_TRAINING;
                            s.t38_fe.current_tx_data_type = T38_DATA_V27TER_4800;
                            break;
                    }
                    set_octets_per_data_packet(s, s.bit_rate);
                    s.t38_fe.timed_step = s.t38_fe.ecm_mode == 2
                        ? T38_TIMED_STEP_HDLC_MODEM
                        : T38_TIMED_STEP_NON_ECM_MODEM;
                } else {
                    fax_modems_start_fast_modem(t, s.modem, s.bit_rate, s.short_train ? 1 : 0, use_hdlc);
                }
                s.non_ecm_tx.out_bytes = 0;
                s.non_ecm_tx.data_started = false;
                s.transmit = true;
                break;

            case FAX_MODEM_V29_TX:
                if (s.t38_mode) {
                    switch (s.bit_rate) {
                        case 7200:
                            s.t38_fe.next_tx_indicator = T38_IND_V29_7200_TRAINING;
                            s.t38_fe.current_tx_data_type = T38_DATA_V29_7200;
                            break;
                        case 9600:
                            s.t38_fe.next_tx_indicator = T38_IND_V29_9600_TRAINING;
                            s.t38_fe.current_tx_data_type = T38_DATA_V29_9600;
                            break;
                    }
                    set_octets_per_data_packet(s, s.bit_rate);
                    s.t38_fe.timed_step = s.t38_fe.ecm_mode == 2
                        ? T38_TIMED_STEP_HDLC_MODEM
                        : T38_TIMED_STEP_NON_ECM_MODEM;
                } else {
                    fax_modems_start_fast_modem(t, s.modem, s.bit_rate, s.short_train ? 1 : 0, use_hdlc);
                }
                s.non_ecm_tx.out_bytes = 0;
                s.non_ecm_tx.data_started = false;
                s.transmit = true;
                break;

            case FAX_MODEM_SILENCE_TX:
                if (s.t38_mode) {
                    int res = t38_core_send_indicator(s.t38_fe.t38, T38_IND_NO_SIGNAL);
                    if (res < 0)
                        return res;
                    s.t38_fe.next_tx_samples = s.t38_fe.samples + milliseconds_to_samples(700);
                    s.t38_fe.timed_step = T38_TIMED_STEP_PAUSE;
                    s.t38_fe.current_tx_data_type = T38_DATA_NONE;
                } else {
                    fax_modems_start_slow_modem(t, FAX_MODEM_SILENCE_TX);
                    fax_modems_set_next_tx_handler(t, null);
                }
                s.transmit = false;
                break;

            case FAX_MODEM_SILENCE_RX:
                if (!s.t38_mode) {
                    t.SetReceiveHandler(
                        samples => silence_rx(s, samples),
                        length => 0);
                    fax_modems_start_slow_modem(t, FAX_MODEM_SILENCE_TX);
                    fax_modems_set_next_tx_handler(t, null);
                }
                s.transmit = false;
                break;

            case FAX_MODEM_FLUSH:
                if (s.t38_mode) {
                    int res = t38_core_send_indicator(s.t38_fe.t38, T38_IND_NO_SIGNAL);
                    if (res < 0)
                        return res;
                } else {
                    s.modem = FAX_MODEM_SILENCE_TX;
                    t.ConfigureTransmitPause(milliseconds_to_samples(200));
                    fax_modems_set_next_tx_handler(t, null);
                    s.transmit = true;
                }
                break;
        }

        s.audio.bit_no = 0;
        s.audio.current_byte = 0xFF;
        s.non_ecm_tx.in_bytes = 0;
        s.non_ecm_tx.out_bytes = 0;
        return 0;
    }

    private static void dle_unstuff_hdlc(t31_state_t s, ReadOnlySpan<byte> stuffed, int len) {
        for (int i = 0; i < len; i++) {
            if (s.dled) {
                s.dled = false;
                if (stuffed[i] == ETX) {
                    s.hdlc_tx.final = (s.hdlc_tx.buf[1] & 0x10) != 0;
                    if (s.t38_mode) {
                        send_hdlc(s, s.hdlc_tx.buf, s.hdlc_tx.len);
                    } else {
                        fax_modems_hdlc_tx_frame(
                            s.audio.modems,
                            new ReadOnlyMemory<byte>(s.hdlc_tx.buf, 0, s.hdlc_tx.len),
                            s.hdlc_tx.len);
                        s.hdlc_tx.len = 0;
                    }
                } else if (s.at_state?.Profile.DoubleEscape != 0 && stuffed[i] == SUB) {
                    s.hdlc_tx.buf[s.hdlc_tx.len++] = DLE;
                    s.hdlc_tx.buf[s.hdlc_tx.len++] = DLE;
                } else {
                    s.hdlc_tx.buf[s.hdlc_tx.len++] = stuffed[i];
                }
            } else {
                if (stuffed[i] == DLE)
                    s.dled = true;
                else
                    s.hdlc_tx.buf[s.hdlc_tx.len++] = stuffed[i];
            }
        }
    }

    private static void dle_unstuff_fake_hdlc(t31_state_t s, ReadOnlySpan<byte> stuffed, int len) {
        if (s.t38_fe.hdlc_rx_non_ecm is null)
            return;

        for (int i = 0; i < len; i++) {
            if (s.dled) {
                s.dled = false;
                if (stuffed[i] == ETX) {
                    s.non_ecm_tx.final = true;
                    t31_set_at_rx_mode(s, AT_MODE_OFFHOOK_COMMAND);
                    return;
                }
                if (s.at_state?.Profile.DoubleEscape != 0 && stuffed[i] == SUB) {
                    hdlc_rx_put_byte(s.t38_fe.hdlc_rx_non_ecm, bit_reverse8(DLE));
                    hdlc_rx_put_byte(s.t38_fe.hdlc_rx_non_ecm, bit_reverse8(DLE));
                } else {
                    hdlc_rx_put_byte(s.t38_fe.hdlc_rx_non_ecm, bit_reverse8(stuffed[i]));
                }
            } else {
                if (stuffed[i] == DLE)
                    s.dled = true;
                else
                    hdlc_rx_put_byte(s.t38_fe.hdlc_rx_non_ecm, bit_reverse8(stuffed[i]));
            }
        }
    }

    private static void dle_unstuff(t31_state_t s, ReadOnlySpan<byte> stuffed, int len) {
        for (int i = 0; i < len; i++) {
            if (s.dled) {
                s.dled = false;
                if (stuffed[i] == ETX) {
                    s.non_ecm_tx.final = true;
                    return;
                }
                if (s.at_state?.Profile.DoubleEscape != 0 && stuffed[i] == SUB) {
                    s.non_ecm_tx.buf[s.non_ecm_tx.in_bytes++] = DLE;
                    s.non_ecm_tx.buf[s.non_ecm_tx.in_bytes++] = DLE;
                } else {
                    s.non_ecm_tx.buf[s.non_ecm_tx.in_bytes++] = stuffed[i];
                }
            } else {
                if (stuffed[i] == DLE)
                    s.dled = true;
                else
                    s.non_ecm_tx.buf[s.non_ecm_tx.in_bytes++] = stuffed[i];
            }

            if (s.non_ecm_tx.in_bytes > T31_TX_BUF_LEN - 2) {
                span_log(s.logging, SPAN_LOG_FLOW, "No room in buffer for new data!\n");
                return;
            }
        }

        if (!s.non_ecm_tx.holding && s.non_ecm_tx.in_bytes > T31_TX_BUF_HIGH_TIDE) {
            s.non_ecm_tx.holding = true;
            if (s.at_state is not null)
                at_modem_control(s.at_state, AT_MODEM_CONTROL_CTS, "0");
        }
    }

    private static int process_class1_cmd(object? user_data, int direction, int operation, int val) {
        var s = (t31_state_t)user_data!;
        int new_transmit = direction;
        int new_modem;
        byte[] msg = new byte[256];

        switch (operation) {
            case 'S':
                s.transmit = new_transmit != 0;
                if (new_transmit != 0) {
                    restart_modem(s, FAX_MODEM_SILENCE_TX);
                    if (s.t38_mode)
                        s.t38_fe.next_tx_samples = s.t38_fe.samples + milliseconds_to_samples(val * 10);
                    else
                        s.audio.modems.ConfigureTransmitPause(milliseconds_to_samples(val * 10));
                    s.transmit = true;
                } else {
                    if (s.rx_queue is not null)
                        queue_flush(s.rx_queue);
                    s.silence_awaited = milliseconds_to_samples(val * 10);
                    t31_set_at_rx_mode(s, AT_MODE_DELIVERY);
                    if (s.t38_mode) {
                        at_put_response_code(s.at_state!, AT_RESPONSE_CODE_OK);
                        t31_set_at_rx_mode(s, AT_MODE_OFFHOOK_COMMAND);
                    } else {
                        restart_modem(s, FAX_MODEM_SILENCE_RX);
                    }
                }
                span_log(s.logging, SPAN_LOG_FLOW, "Silence %dms\n", val * 10);
                break;

            case 'H':
                switch (val) {
                    case 3:
                        new_modem = new_transmit != 0 ? FAX_MODEM_V21_TX : FAX_MODEM_V21_RX;
                        s.short_train = false;
                        s.bit_rate = 300;
                        break;
                    default:
                        return -1;
                }
                span_log(s.logging, SPAN_LOG_FLOW, "HDLC\n");
                if (new_modem != s.modem)
                    restart_modem(s, new_modem);
                s.transmit = new_transmit != 0;
                if (new_transmit != 0) {
                    t31_set_at_rx_mode(s, AT_MODE_HDLC);
                } else {
                    t31_set_at_rx_mode(s, AT_MODE_DELIVERY);
                    s.rx_frame_received = false;
                    s.audio.modems.RxFrameReceived = false;
                    do {
                        if (s.rx_queue is not null && !queue_empty(s.rx_queue)) {
                            int len = queue_read_msg(s.rx_queue, msg, 256);
                            if (len > 1) {
                                if (msg[0] == AT_RESPONSE_CODE_OK)
                                    at_put_response_code(s.at_state!, AT_RESPONSE_CODE_CONNECT);
                                for (int i = 1; i < len; i++) {
                                    if (msg[i] == DLE)
                                        s.at_rx_data[s.at_rx_data_bytes++] = DLE;
                                    s.at_rx_data[s.at_rx_data_bytes++] = msg[i];
                                }
                                s.at_rx_data[s.at_rx_data_bytes++] = DLE;
                                s.at_rx_data[s.at_rx_data_bytes++] = ETX;
                                s.at_tx_handler?.Invoke(s.at_tx_user_data, s.at_rx_data.AsSpan(0, s.at_rx_data_bytes));
                                s.at_rx_data_bytes = 0;
                            }
                            at_put_response_code(s.at_state!, msg[0]);
                            if (msg[0] == AT_RESPONSE_CODE_CONNECT) {
                                s.rx_frame_received = true;
                                s.audio.modems.RxFrameReceived = true;
                            }
                        } else {
                            s.at_state!.DteIsWaiting = true;
                            break;
                        }
                    }
                    while (msg[0] == AT_RESPONSE_CODE_CONNECT);
                }
                break;

            default:
                switch (val) {
                    case 24:
                        s.t38_fe.next_tx_indicator = T38_IND_V27TER_2400_TRAINING;
                        s.t38_fe.current_tx_data_type = T38_DATA_V27TER_2400;
                        new_modem = new_transmit != 0 ? FAX_MODEM_V27TER_TX : FAX_MODEM_V27TER_RX;
                        s.short_train = false;
                        s.bit_rate = 2400;
                        break;
                    case 48:
                        s.t38_fe.next_tx_indicator = T38_IND_V27TER_4800_TRAINING;
                        s.t38_fe.current_tx_data_type = T38_DATA_V27TER_4800;
                        new_modem = new_transmit != 0 ? FAX_MODEM_V27TER_TX : FAX_MODEM_V27TER_RX;
                        s.short_train = false;
                        s.bit_rate = 4800;
                        break;
                    case 72:
                        s.t38_fe.next_tx_indicator = T38_IND_V29_7200_TRAINING;
                        s.t38_fe.current_tx_data_type = T38_DATA_V29_7200;
                        new_modem = new_transmit != 0 ? FAX_MODEM_V29_TX : FAX_MODEM_V29_RX;
                        s.short_train = false;
                        s.bit_rate = 7200;
                        break;
                    case 96:
                        s.t38_fe.next_tx_indicator = T38_IND_V29_9600_TRAINING;
                        s.t38_fe.current_tx_data_type = T38_DATA_V29_9600;
                        new_modem = new_transmit != 0 ? FAX_MODEM_V29_TX : FAX_MODEM_V29_RX;
                        s.short_train = false;
                        s.bit_rate = 9600;
                        break;
                    case 73:
                        s.t38_fe.next_tx_indicator = T38_IND_V17_7200_LONG_TRAINING;
                        s.t38_fe.current_tx_data_type = T38_DATA_V17_7200;
                        new_modem = new_transmit != 0 ? FAX_MODEM_V17_TX : FAX_MODEM_V17_RX;
                        s.short_train = false;
                        s.bit_rate = 7200;
                        break;
                    case 74:
                        s.t38_fe.next_tx_indicator = T38_IND_V17_7200_SHORT_TRAINING;
                        s.t38_fe.current_tx_data_type = T38_DATA_V17_7200;
                        new_modem = new_transmit != 0 ? FAX_MODEM_V17_TX : FAX_MODEM_V17_RX;
                        s.short_train = true;
                        s.bit_rate = 7200;
                        break;
                    case 97:
                        s.t38_fe.next_tx_indicator = T38_IND_V17_9600_LONG_TRAINING;
                        s.t38_fe.current_tx_data_type = T38_DATA_V17_9600;
                        new_modem = new_transmit != 0 ? FAX_MODEM_V17_TX : FAX_MODEM_V17_RX;
                        s.short_train = false;
                        s.bit_rate = 9600;
                        break;
                    case 98:
                        s.t38_fe.next_tx_indicator = T38_IND_V17_9600_SHORT_TRAINING;
                        s.t38_fe.current_tx_data_type = T38_DATA_V17_9600;
                        new_modem = new_transmit != 0 ? FAX_MODEM_V17_TX : FAX_MODEM_V17_RX;
                        s.short_train = true;
                        s.bit_rate = 9600;
                        break;
                    case 121:
                        s.t38_fe.next_tx_indicator = T38_IND_V17_12000_LONG_TRAINING;
                        s.t38_fe.current_tx_data_type = T38_DATA_V17_12000;
                        new_modem = new_transmit != 0 ? FAX_MODEM_V17_TX : FAX_MODEM_V17_RX;
                        s.short_train = false;
                        s.bit_rate = 12000;
                        break;
                    case 122:
                        s.t38_fe.next_tx_indicator = T38_IND_V17_12000_SHORT_TRAINING;
                        s.t38_fe.current_tx_data_type = T38_DATA_V17_12000;
                        new_modem = new_transmit != 0 ? FAX_MODEM_V17_TX : FAX_MODEM_V17_RX;
                        s.short_train = true;
                        s.bit_rate = 12000;
                        break;
                    case 145:
                        s.t38_fe.next_tx_indicator = T38_IND_V17_14400_LONG_TRAINING;
                        s.t38_fe.current_tx_data_type = T38_DATA_V17_14400;
                        new_modem = new_transmit != 0 ? FAX_MODEM_V17_TX : FAX_MODEM_V17_RX;
                        s.short_train = false;
                        s.bit_rate = 14400;
                        break;
                    case 146:
                        s.t38_fe.next_tx_indicator = T38_IND_V17_14400_SHORT_TRAINING;
                        s.t38_fe.current_tx_data_type = T38_DATA_V17_14400;
                        new_modem = new_transmit != 0 ? FAX_MODEM_V17_TX : FAX_MODEM_V17_RX;
                        s.short_train = true;
                        s.bit_rate = 14400;
                        break;
                    default:
                        return -1;
                }
                span_log(s.logging, SPAN_LOG_FLOW, "Short training = %d, bit rate = %d\n", s.short_train ? 1 : 0, s.bit_rate);
                if (new_transmit != 0) {
                    t31_set_at_rx_mode(s, AT_MODE_STUFFED);
                    at_put_response_code(s.at_state!, AT_RESPONSE_CODE_CONNECT);
                } else {
                    t31_set_at_rx_mode(s, AT_MODE_DELIVERY);
                }
                restart_modem(s, new_modem);
                break;
        }
        return 0;
    }

    public static void t31_call_event(t31_state_t s, int event_value) {
        ArgumentNullException.ThrowIfNull(s);
        span_log(s.logging, SPAN_LOG_FLOW, "Call event %s (%d) received\n", at_call_state_to_str(event_value), event_value);
        if (s.at_state is not null) {
            at_call_event(s.at_state, event_value);
            s.do_hangup = s.at_state.DoHangup;
        }
    }

    public static int t31_at_rx_free_space(t31_state_t s) {
        ArgumentNullException.ThrowIfNull(s);
        return T31_TX_BUF_LEN - (s.non_ecm_tx.in_bytes - s.non_ecm_tx.out_bytes) - 1;
    }

    public static int t31_at_rx(t31_state_t s, ReadOnlySpan<byte> t, int len) {
        ArgumentNullException.ThrowIfNull(s);
        if (len < 0 || len > t.Length)
            throw new ArgumentOutOfRangeException(nameof(len));

        if (s.dte_data_timeout != 0)
            s.dte_data_timeout = s.call_samples + milliseconds_to_samples(5000);

        int mode = s.at_state is null ? AT_MODE_ONHOOK_COMMAND : (int)s.at_state.ReceiveMode;
        switch (mode) {
            case AT_MODE_ONHOOK_COMMAND:
            case AT_MODE_OFFHOOK_COMMAND:
                if (s.at_state is not null) {
                    string text = System.Text.Encoding.Latin1.GetString(t[..len]);
                    at_interpreter(s.at_state, text, text.Length);
                    s.do_hangup = s.at_state.DoHangup;
                }
                break;

            case AT_MODE_DELIVERY:
                if (len != 0) {
                    if (s.at_state?.ReceiveSignalPresent == true) {
                        s.at_rx_data[s.at_rx_data_bytes++] = DLE;
                        s.at_rx_data[s.at_rx_data_bytes++] = ETX;
                        s.at_tx_handler?.Invoke(s.at_tx_user_data, s.at_rx_data.AsSpan(0, s.at_rx_data_bytes));
                    }
                    s.at_rx_data_bytes = 0;
                    s.transmit = false;
                    s.modem = FAX_MODEM_SILENCE_TX;
                    s.audio.modems.SetReceiveIdle();
                    t31_set_at_rx_mode(s, AT_MODE_OFFHOOK_COMMAND);
                    at_put_response_code(s.at_state!, AT_RESPONSE_CODE_OK);
                }
                break;

            case AT_MODE_HDLC:
                dle_unstuff_hdlc(s, t, len);
                break;

            case AT_MODE_STUFFED:
                if (s.non_ecm_tx.out_bytes != 0) {
                    s.non_ecm_tx.in_bytes -= s.non_ecm_tx.out_bytes;
                    Array.Copy(
                        s.non_ecm_tx.buf,
                        s.non_ecm_tx.out_bytes,
                        s.non_ecm_tx.buf,
                        0,
                        s.non_ecm_tx.in_bytes);
                    s.non_ecm_tx.out_bytes = 0;
                }
                if (s.t38_fe.ecm_mode == 2)
                    dle_unstuff_fake_hdlc(s, t, len);
                else
                    dle_unstuff(s, t, len);
                break;

            case AT_MODE_CONNECTED:
                break;
        }
        return len;
    }

    public static int t31_at_rx(t31_state_t s, string t, int len) {
        ArgumentNullException.ThrowIfNull(t);
        if (len < 0 || len > t.Length)
            throw new ArgumentOutOfRangeException(nameof(len));
        byte[] data = System.Text.Encoding.Latin1.GetBytes(t[..len]);
        return t31_at_rx(s, data, data.Length);
    }

    private static int silence_rx(t31_state_t s, ReadOnlySpan<short> amp) {
        if (s.silence_awaited != 0 && s.audio.silence_heard >= s.silence_awaited) {
            at_put_response_code(s.at_state!, AT_RESPONSE_CODE_OK);
            t31_set_at_rx_mode(s, AT_MODE_OFFHOOK_COMMAND);
            s.audio.silence_heard = 0;
            s.silence_awaited = 0;
        }
        return 0;
    }

    private static int initial_timed_rx(t31_state_t s, ReadOnlySpan<short> amp) {
        int s7 = s.at_state?.Profile.SRegisters[7] ?? 60;
        if (s.call_samples > milliseconds_to_samples(s7 * 1000)) {
            at_put_response_code(s.at_state!, AT_RESPONSE_CODE_NO_CARRIER);
            restart_modem(s, FAX_MODEM_SILENCE_TX);
            if (s.at_state is not null)
                at_modem_control(s.at_state, AT_MODEM_CONTROL_HANGUP, null);
            t31_set_at_rx_mode(s, AT_MODE_ONHOOK_COMMAND);
            return 0;
        }
        return s.audio.modems.ProcessReceive(amp);
    }

    public static int t31_rx(t31_state_t s, short[] amp, int len) {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(amp);
        if (len < 0 || len > amp.Length)
            throw new ArgumentOutOfRangeException(nameof(len));

        for (int i = 0; i < len; i++) {
            int sample = amp[i] - s.audio.last_sample;
            int power = power_meter_update(s.audio.rx_power, unchecked((short)sample));
            s.audio.last_sample = amp[i];
            if (power > s.audio.silence_threshold_power) {
                s.audio.silence_heard = 0;
            } else if (s.audio.silence_heard <= milliseconds_to_samples(255 * 10)) {
                s.audio.silence_heard++;
            }
        }

        s.call_samples += len;
        if (s.dte_data_timeout != 0 && s.call_samples > s.dte_data_timeout) {
            t31_set_at_rx_mode(s, AT_MODE_OFFHOOK_COMMAND);
            at_put_response_code(s.at_state!, AT_RESPONSE_CODE_ERROR);
            restart_modem(s, FAX_MODEM_SILENCE_TX);
        }

        if (!s.t38_mode
            && (s.modem == FAX_MODEM_CNG_TONE_TX || s.modem == FAX_MODEM_NOCNG_TONE_TX)) {
            return initial_timed_rx(s, amp.AsSpan(0, len));
        }

        s.audio.modems.ProcessReceive(amp.AsSpan(0, len));
        return 0;
    }

    public static int t31_rx_fillin(t31_state_t s, int len) {
        ArgumentNullException.ThrowIfNull(s);
        s.call_samples += len;
        if (s.dte_data_timeout != 0 && s.call_samples > s.dte_data_timeout) {
            t31_set_at_rx_mode(s, AT_MODE_OFFHOOK_COMMAND);
            at_put_response_code(s.at_state!, AT_RESPONSE_CODE_ERROR);
            restart_modem(s, FAX_MODEM_SILENCE_TX);
        }
        s.audio.modems.ProcessReceiveFillIn(len);
        return 0;
    }

    public static int t31_tx(t31_state_t s, short[] amp, int max_len) {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(amp);
        if (max_len < 0 || max_len > amp.Length)
            throw new ArgumentOutOfRangeException(nameof(max_len));

        int len = 0;
        if (s.transmit) {
            len = s.audio.modems.GenerateTransmit(amp.AsSpan(0, max_len));
            if (len < max_len) {
                fax_modems_set_next_tx_type(s.audio.modems);
                len += s.audio.modems.GenerateTransmit(amp.AsSpan(len, max_len - len));
                if (len < max_len)
                    front_end_status(s, T30_FRONT_END_SEND_STEP_COMPLETE);
            }
        }
        if (s.audio.modems.TransmitOnIdle) {
            vec_zeroi16(amp.AsSpan(len, max_len - len), max_len - len);
            len = max_len;
        }
        return len;
    }

    public static void t31_set_transmit_on_idle(t31_state_t s, bool transmit_on_idle) {
        ArgumentNullException.ThrowIfNull(s);
        s.audio.modems.TransmitOnIdle = transmit_on_idle;
    }

    public static void t31_set_tep_mode(t31_state_t s, bool use_tep) {
        ArgumentNullException.ThrowIfNull(s);
        fax_modems_set_tep_mode(s.audio.modems, use_tep ? 1 : 0);
    }

    public static void t31_set_t38_config(t31_state_t s, bool without_pacing) {
        ArgumentNullException.ThrowIfNull(s);
        if (without_pacing) {
            t38_set_redundancy_control(s.t38_fe.t38, T38_PACKET_CATEGORY_INDICATOR, 0);
            t38_set_redundancy_control(s.t38_fe.t38, T38_PACKET_CATEGORY_CONTROL_DATA, 1);
            t38_set_redundancy_control(s.t38_fe.t38, T38_PACKET_CATEGORY_CONTROL_DATA_END, 1);
            t38_set_redundancy_control(s.t38_fe.t38, T38_PACKET_CATEGORY_IMAGE_DATA, 1);
            t38_set_redundancy_control(s.t38_fe.t38, T38_PACKET_CATEGORY_IMAGE_DATA_END, 1);
            t38_set_tx_packet_interval(s.t38_fe.t38, 0);
            t38_set_pace_transmission(s.t38_fe.t38, 0);
        } else {
            t38_set_redundancy_control(s.t38_fe.t38, T38_PACKET_CATEGORY_INDICATOR, INDICATOR_TX_COUNT);
            t38_set_redundancy_control(s.t38_fe.t38, T38_PACKET_CATEGORY_CONTROL_DATA, DATA_TX_COUNT);
            t38_set_redundancy_control(s.t38_fe.t38, T38_PACKET_CATEGORY_CONTROL_DATA_END, DATA_END_TX_COUNT);
            t38_set_redundancy_control(s.t38_fe.t38, T38_PACKET_CATEGORY_IMAGE_DATA, DATA_TX_COUNT);
            t38_set_redundancy_control(s.t38_fe.t38, T38_PACKET_CATEGORY_IMAGE_DATA_END, DATA_END_TX_COUNT);
            t38_set_tx_packet_interval(s.t38_fe.t38, 30000);
            t38_set_pace_transmission(s.t38_fe.t38, 1);
        }
        set_octets_per_data_packet(s, 300);
    }

    public static void t31_set_mode(t31_state_t s, bool t38_mode) {
        ArgumentNullException.ThrowIfNull(s);
        s.t38_mode = t38_mode;
        span_log(s.logging, SPAN_LOG_FLOW, "Mode set to %d\n", s.t38_mode ? 1 : 0);
    }

    public static logging_state_t t31_get_logging_state(t31_state_t s) {
        ArgumentNullException.ThrowIfNull(s);
        return s.logging;
    }

    public static at_state_t? t31_get_at_state(t31_state_t s) {
        ArgumentNullException.ThrowIfNull(s);
        return s.at_state;
    }

    public static t38_core_state_t t31_get_t38_core_state(t31_state_t s) {
        ArgumentNullException.ThrowIfNull(s);
        return s.t38_fe.t38;
    }

    private static int t31_t38_fe_init(
        t31_state_t s,
        t38_tx_packet_handler_t tx_packet_handler,
        object? tx_packet_user_data) {
        t31_t38_front_end_state_t fe = s.t38_fe;

        fe.t38 = t38_core_init(
            fe.t38,
            process_rx_indicator,
            process_rx_data,
            process_rx_missing,
            s,
            (state, user_data, packet, count) => tx_packet_handler(state, user_data, packet, count),
            tx_packet_user_data);
        fe.t38.FastestImageDataRate = 14400;
        fe.timed_step = T38_TIMED_STEP_NONE;
        fe.t38.Iaf = T30_IAF_MODE_T38;
        fe.current_tx_data_type = T38_DATA_NONE;
        fe.next_tx_samples = 0;
        fe.t38.ChunkingModes = T38ChunkingMode.AllowTepTime;
        s.hdlc_tx.ptr = 0;

        fe.hdlc_tx_non_ecm = hdlc_tx_init(
            fe.hdlc_tx_non_ecm,
            false,
            1,
            false,
            hdlc_tx_underflow2,
            fe);
        fe.hdlc_rx_non_ecm = hdlc_rx_init(
            fe.hdlc_rx_non_ecm,
            false,
            true,
            2,
            (user_data, packet, length_or_status, ok) =>
                hdlc_accept_non_ecm_frame(s, packet, length_or_status, ok),
            s);
        return 0;
    }

    public static t31_state_t? t31_init(
        t31_state_t? s,
        at_tx_handler_t? at_tx_handler,
        object? at_tx_user_data,
        t31_modem_control_handler_t? modem_control_handler,
        object? modem_control_user_data,
        t38_tx_packet_handler_t? tx_t38_packet_handler,
        object? tx_t38_packet_user_data) {
        if (at_tx_handler is null || modem_control_handler is null)
            return null;

        s ??= new t31_state_t();

        if (s.rx_queue is not null) {
            queue_free(s.rx_queue);
            s.rx_queue = null;
        }
        s.at_state?.Dispose();
        s.audio.modems.Dispose();
        s.t38_fe.hdlc_tx_non_ecm?.Dispose();
        s.t38_fe.hdlc_rx_non_ecm?.Dispose();

        s.audio = new t31_audio_front_end_state_t();
        s.t38_fe = new t31_t38_front_end_state_t();
        s.hdlc_tx = new t31_state_t.hdlc_tx_state();
        s.non_ecm_tx = new t31_state_t.non_ecm_tx_state();
        s.at_rx_data = new byte[512];
        s.at_rx_data_bytes = 0;
        s.do_hangup = false;
        s.disposed = false;

        s.logging = span_log_init(s.logging, SPAN_LOG_NONE, null);
        span_log_set_protocol(s.logging, "T.31");

        s.modem_control_handler = modem_control_handler;
        s.modem_control_user_data = modem_control_user_data;
        s.at_tx_handler = at_tx_handler;
        s.at_tx_user_data = at_tx_user_data;

        s.audio.modems = fax_modems_init(
            s.audio.modems,
            0,
            (message, length_or_status, ok) => {
                if (length_or_status < 0) {
                    hdlc_accept_frame(s, Array.Empty<byte>(), length_or_status, ok);
                    return;
                }
                int payload_length = Math.Max(0, length_or_status);
                byte[] frame = new byte[payload_length + 2];
                int copied = 0;
                if (message.HasValue
                    && MemoryMarshal.TryGetArray(message.Value, out ArraySegment<byte> segment)
                    && segment.Array is not null) {
                    copied = Math.Min(frame.Length, segment.Array.Length - segment.Offset);
                    Array.Copy(segment.Array, segment.Offset, frame, 0, copied);
                } else if (message.HasValue) {
                    copied = Math.Min(payload_length, message.Value.Length);
                    message.Value.Span[..copied].CopyTo(frame);
                }
                if (ok && copied < payload_length + 2)
                    crc_itu16_append(frame, payload_length);
                hdlc_accept_frame(s, frame, payload_length, ok);
            },
            () => hdlc_tx_underflow(s),
            bit => non_ecm_put_bit(s, bit),
            () => non_ecm_get_bit(s),
            (tone, level, delay) => tone_detected(s, tone, level, delay));
        s.audio.modems.SetReceiveIdle();

        var v8_parms = new V8Parameters {
            ModemConnectTone = ModemConnectTone.AnsamWithPhaseReversals
        };
        v8_parms.JmCm.CallFunction = V8CallFunction.T30ReceiveFax;
        v8_parms.JmCm.Modulations =
            V8Modulation.V21 |
            V8Modulation.V17 |
            V8Modulation.V29 |
            V8Modulation.V27Ter;
        v8_parms.JmCm.Protocols = V8Protocol.None;
        v8_parms.JmCm.PcmModemAvailability = (V8PcmModemAvailability)0;
        v8_parms.JmCm.PstnAccess = (V8PstnAccess)0;
        v8_parms.JmCm.Nsf = -1;
        v8_parms.JmCm.T66 = -1;
        s.audio.v8 = V8Api.v8_init(s.audio.v8, false, v8_parms, v8_handler, s);

        s.audio.rx_power = power_meter_init(s.audio.rx_power, 4) ?? new power_meter_t(4);
        s.audio.last_sample = 0;
        s.audio.silence_threshold_power = power_meter_level_dbm0(-36);

        s.rx_queue = queue_init(
            null,
            4096,
            QUEUE_WRITE_ATOMIC | QUEUE_READ_ATOMIC);
        if (s.rx_queue is null)
            return null;

        s.at_state = at_init(
            s.at_state,
            (user_data, data) => at_tx_handler(user_data, data),
            at_tx_user_data,
            t31_modem_control_handler,
            s);
        at_set_class1_handler(s.at_state, process_class1_cmd, s);

        s.dte_inactivity_timeout = DEFAULT_DTE_TIMEOUT;
        s.modem = FAX_MODEM_NONE;
        s.transmit = true;
        s.silence_awaited = 0;
        s.call_samples = 0;
        s.dte_data_timeout = 0;
        s.dled = false;
        s.short_train = false;
        s.bit_rate = 0;
        s.rx_frame_received = false;
        s.audio.modems.RxFrameReceived = false;
        s.t38_mode = false;

        if (tx_t38_packet_handler is not null) {
            t31_t38_fe_init(s, tx_t38_packet_handler, tx_t38_packet_user_data);
            t31_set_t38_config(s, false);
        }
        return s;
    }

    public static int t31_release(t31_state_t s) {
        ArgumentNullException.ThrowIfNull(s);
        if (s.at_state is not null)
            at_reset_call_info(s.at_state);
        V8Api.v8_release(s.audio.v8);
        fax_modems_release(s.audio.modems);
        if (s.t38_fe.hdlc_tx_non_ecm is not null)
            hdlc_tx_release(s.t38_fe.hdlc_tx_non_ecm);
        if (s.t38_fe.hdlc_rx_non_ecm is not null)
            hdlc_rx_release(s.t38_fe.hdlc_rx_non_ecm);
        if (s.rx_queue is not null)
            queue_release(s.rx_queue);
        return 0;
    }

    public static int t31_free(t31_state_t s) {
        ArgumentNullException.ThrowIfNull(s);
        t31_release(s);
        s.at_state?.Dispose();
        s.audio.v8.Dispose();
        s.audio.modems.Dispose();
        s.t38_fe.hdlc_tx_non_ecm?.Dispose();
        s.t38_fe.hdlc_rx_non_ecm?.Dispose();
        s.rx_queue?.Dispose();
        s.disposed = true;
        return 0;
    }
}
