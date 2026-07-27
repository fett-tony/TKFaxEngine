/*
 * TKFaxEngineFX - a series of DSP components for telephony
 *
 * v42.cs - direct C# conversion of v42.h and v42.c
 *
 * Written by Steve Underwood <steveu@coppice.org>
 *
 * Copyright (C) 2003, 2004, 2011 Steve Underwood
 *
 * This file preserves the GNU Lesser General Public License version 2.1
 * terms of the original source files.
 */

/* THIS IS A WORK IN PROGRESS. IT IS NOT FINISHED. */

#nullable enable

using System.Buffers.Binary;
using global::TKFaxEngine;
using global::TKFaxEngine.Modem;
using static global::TKFaxEngine.AsyncApi;
using static global::TKFaxEngine.LoggingApi;
using static global::TKFaxEngine.Modem.HdlcApi;

namespace TKFaxEngine.Modem.V42;

public sealed class v42_config_parameters_t
{
    public byte v42_tx_window_size_k;
    public byte v42_rx_window_size_k;
    public ushort v42_tx_n401;
    public ushort v42_rx_n401;

    public byte comp;
    public int comp_dict_size;
    public int comp_max_string;
}

public sealed class v42_frame_t
{
    public int len;
    public byte[] buf = new byte[4 + v42.V42_MAX_N_401];
}

public sealed class lapm_state_t
{
    public span_get_msg_func_t? iframe_get;
    public object? iframe_get_user_data;

    public span_put_msg_func_t? iframe_put;
    public object? iframe_put_user_data;

    public SpanModemStatusDelegate? status_handler;
    public object? status_user_data;

    public HdlcReceiver hdlc_rx = new();
    public HdlcTransmitter hdlc_tx = new();

    public byte tx_window_size_k;
    public byte rx_window_size_k;
    public ushort tx_n401;
    public ushort rx_n401;

    public byte cmd_addr;
    public byte rsp_addr;
    public byte vs;
    public byte va;
    public byte vr;
    public int state;
    public int configuring;
    public bool local_busy;
    public bool far_busy;
    public bool rejected;
    public int retry_count;

    public int ctrl_put;
    public int ctrl_get;
    public v42_frame_t[] ctrl_buf =
    [
        new v42_frame_t(), new v42_frame_t(), new v42_frame_t(), new v42_frame_t(),
        new v42_frame_t(), new v42_frame_t(), new v42_frame_t(), new v42_frame_t()
    ];

    public int info_put;
    public int info_get;
    public int info_acked;
    public v42_frame_t[] info_buf =
    [
        new v42_frame_t(), new v42_frame_t(), new v42_frame_t(), new v42_frame_t(),
        new v42_frame_t(), new v42_frame_t(), new v42_frame_t(), new v42_frame_t(),
        new v42_frame_t(), new v42_frame_t(), new v42_frame_t(), new v42_frame_t(),
        new v42_frame_t(), new v42_frame_t(), new v42_frame_t(), new v42_frame_t()
    ];

    public Action<v42_state_t, int>? packer_process;
}

public sealed class v42_negotiation_t
{
    public int rx_negotiation_step;
    public int rxbits;
    public int rxstream;
    public int rxoks;
    public int odp_seen;
    public int txbits;
    public int txstream;
    public int txadps;
}

public sealed class v42_state_t
{
    public bool calling_party;
    public bool detect;

    public int tx_bit_rate;

    public v42_config_parameters_t config = new();
    public v42_negotiation_t neg = new();
    public lapm_state_t lapm = new();

    public int bit_timer;
    public Action<v42_state_t>? bit_timer_func;

    public SpanLogState logging = new();
}

public static class v42
{
    public const int V42_DEFAULT_N_400 = 5;
    public const int V42_DEFAULT_N_401 = 128;
    public const int V42_MAX_N_401 = 128;
    public const int V42_DEFAULT_WINDOW_SIZE_K = 15;
    public const int V42_MAX_WINDOW_SIZE_K = 15;

    public const int V42_INFO_FRAMES = V42_MAX_WINDOW_SIZE_K + 1;
    public const int V42_CTRL_FRAMES = 8;

    private const int T_400 = 750;
    private const int T_401 = 1000;
    private const int T_402 = 1000;
    private const int T_403 = 10000;

    private const int LAPM_DLCI_DTE_TO_DTE = 0;
    private const int LAPM_DLCI_LAYER2_MANAGEMENT = 63;

    private const int LAPM_FRAMETYPE_MASK = 0x03;

    private const int LAPM_FRAMETYPE_I = 0x00;
    private const int LAPM_FRAMETYPE_I_ALT = 0x02;
    private const int LAPM_FRAMETYPE_S = 0x01;
    private const int LAPM_FRAMETYPE_U = 0x03;

    private const int LAPM_S_RR = 0x00;
    private const int LAPM_S_RNR = 0x04;
    private const int LAPM_S_REJ = 0x08;
    private const int LAPM_S_SREJ = 0x0C;

    private const int LAPM_S_PF = 0x01;

    private const int LAPM_U_UI = 0x00;
    private const int LAPM_U_DM = 0x0C;
    private const int LAPM_U_DISC = 0x40;
    private const int LAPM_U_UA = 0x60;
    private const int LAPM_U_SABME = 0x6C;
    private const int LAPM_U_FRMR = 0x84;
    private const int LAPM_U_XID = 0xAC;
    private const int LAPM_U_TEST = 0xE0;

    private const int LAPM_U_PF = 0x10;

    private const int FI_GENERAL = 0x82;
    private const int GI_PARAM_NEGOTIATION = 0x80;
    private const int GI_PRIVATE_NEGOTIATION = 0xF0;
    private const int GI_USER_DATA = 0xFF;

    private const int PI_HDLC_OPTIONAL_FUNCTIONS = 0x03;
    private const int PI_TX_INFO_MAXSIZE = 0x05;
    private const int PI_RX_INFO_MAXSIZE = 0x06;
    private const int PI_TX_WINDOW_SIZE = 0x07;
    private const int PI_RX_WINDOW_SIZE = 0x08;

    private const int PI_PARAMETER_SET_ID = 0x00;
    private const int PI_V42BIS_COMPRESSION_REQUEST = 0x01;
    private const int PI_V42BIS_NUM_CODEWORDS = 0x02;
    private const int PI_V42BIS_MAX_STRING_LENGTH = 0x03;

    public const int LAPM_DETECT = 0;
    public const int LAPM_IDLE = 1;
    public const int LAPM_ESTABLISH = 2;
    public const int LAPM_DATA = 3;
    public const int LAPM_RELEASE = 4;
    public const int LAPM_SIGNAL = 5;
    public const int LAPM_SETPARM = 6;
    public const int LAPM_TEST = 7;
    public const int LAPM_V42_UNSUPPORTED = 8;

    public static string lapm_status_to_str(int status)
    {
        switch (status)
        {
            case LAPM_DETECT:
                return "LAPM_DETECT";
            case LAPM_IDLE:
                return "LAPM_IDLE";
            case LAPM_ESTABLISH:
                return "LAPM_ESTABLISH";
            case LAPM_DATA:
                return "LAPM_DATA";
            case LAPM_RELEASE:
                return "LAPM_RELEASE";
            case LAPM_SIGNAL:
                return "LAPM_SIGNAL";
            case LAPM_SETPARM:
                return "LAPM_SETPARM";
            case LAPM_TEST:
                return "LAPM_TEST";
            case LAPM_V42_UNSUPPORTED:
                return "LAPM_V42_UNSUPPORTED";
        }
        return "???";
    }

    private static void report_rx_status_change(v42_state_t s, int status)
    {
        if (s.lapm.status_handler is not null)
            s.lapm.status_handler(s.lapm.status_user_data, status);
        else if (s.lapm.iframe_put is not null)
            s.lapm.iframe_put(s.lapm.iframe_put_user_data, null, status);
    }

    private static uint pack_value(ReadOnlySpan<byte> buf, int len)
    {
        uint val;
        int at;

        val = 0;
        at = 0;
        while (len-- != 0)
        {
            val <<= 8;
            val |= buf[at++];
        }
        return val;
    }

    private static v42_frame_t? get_next_free_ctrl_frame(lapm_state_t s)
    {
        v42_frame_t f;
        int ctrl_put_next;

        if ((ctrl_put_next = s.ctrl_put + 1) >= V42_CTRL_FRAMES)
            ctrl_put_next = 0;
        if (ctrl_put_next == s.ctrl_get)
            return null;
        f = s.ctrl_buf[s.ctrl_put];
        s.ctrl_put = ctrl_put_next;
        return f;
    }

    private static int tx_unnumbered_frame(lapm_state_t s, byte addr, byte ctrl, byte[]? info, int len)
    {
        v42_frame_t? f;
        byte[] buf;

        f = get_next_free_ctrl_frame(s);
        if (f is null)
            return -1;
        buf = f.buf;
        buf[0] = addr;
        buf[1] = (byte)(LAPM_FRAMETYPE_U | ctrl);
        f.len = 2;
        if (info is not null && len != 0)
        {
            Array.Copy(info, 0, buf, f.len, len);
            f.len += len;
        }
        return 0;
    }

    private static int tx_supervisory_frame(lapm_state_t s, byte addr, byte ctrl, byte pf_mask)
    {
        v42_frame_t? f;
        byte[] buf;

        f = get_next_free_ctrl_frame(s);
        if (f is null)
            return -1;
        buf = f.buf;
        buf[0] = addr;
        buf[1] = (byte)(LAPM_FRAMETYPE_S | ctrl);
        buf[2] = (byte)((s.vr << 1) | pf_mask);
        f.len = 3;
        return 0;
    }

    private static int set_param(int param, int value, int def)
    {
        if ((value < def && param >= def) || (value >= def && param < def))
            return def;
        if ((value < def && param < value) || (value >= def && param > value))
            return value;
        return param;
    }

    private static int receive_xid(v42_state_t ss, ReadOnlySpan<byte> frame, int len)
    {
        lapm_state_t s;
        v42_config_parameters_t config;
        int frame_at;
        int buf_at;
        byte group_id;
        ushort group_len;
        uint param_val;
        byte param_id;
        byte param_len;

        s = ss.lapm;
        if (frame[2] != FI_GENERAL)
            return -1;
        config = new v42_config_parameters_t();
        frame_at = 3;
        len -= 3;
        while (len > 0)
        {
            group_id = frame[frame_at];
            group_len = frame[frame_at + 1];
            group_len = (ushort)((group_len << 8) | frame[frame_at + 2]);
            frame_at += 3;
            len -= 3 + group_len;
            if (len < 0)
                break;
            buf_at = frame_at;
            frame_at += group_len;
            switch (group_id)
            {
                case GI_PARAM_NEGOTIATION:
                    while (group_len > 0)
                    {
                        param_id = frame[buf_at];
                        param_len = frame[buf_at + 1];
                        buf_at += 2;
                        if (group_len < 2 + param_len)
                            break;
                        group_len = (ushort)(group_len - (2 + param_len));
                        switch (param_id)
                        {
                            case PI_HDLC_OPTIONAL_FUNCTIONS:
                                break;
                            case PI_TX_INFO_MAXSIZE:
                                param_val = pack_value(frame[buf_at..], param_len);
                                param_val >>= 3;
                                config.v42_tx_n401 =
                                s.tx_n401 = (ushort)set_param(s.tx_n401, (int)param_val, ss.config.v42_tx_n401);
                                break;
                            case PI_RX_INFO_MAXSIZE:
                                param_val = pack_value(frame[buf_at..], param_len);
                                param_val >>= 3;
                                config.v42_rx_n401 =
                                s.rx_n401 = (ushort)set_param(s.rx_n401, (int)param_val, ss.config.v42_rx_n401);
                                break;
                            case PI_TX_WINDOW_SIZE:
                                param_val = pack_value(frame[buf_at..], param_len);
                                config.v42_tx_window_size_k =
                                s.tx_window_size_k = (byte)set_param(s.tx_window_size_k, (int)param_val, ss.config.v42_tx_window_size_k);
                                break;
                            case PI_RX_WINDOW_SIZE:
                                param_val = pack_value(frame[buf_at..], param_len);
                                config.v42_rx_window_size_k =
                                s.rx_window_size_k = (byte)set_param(s.rx_window_size_k, (int)param_val, ss.config.v42_rx_window_size_k);
                                break;
                            default:
                                break;
                        }
                        buf_at += param_len;
                    }
                    break;
                case GI_PRIVATE_NEGOTIATION:
                    while (group_len > 0)
                    {
                        param_id = frame[buf_at];
                        param_len = frame[buf_at + 1];
                        buf_at += 2;
                        if (group_len < 2 + param_len)
                            break;
                        group_len = (ushort)(group_len - (2 + param_len));
                        switch (param_id)
                        {
                            case PI_PARAMETER_SET_ID:
                                break;
                            case PI_V42BIS_COMPRESSION_REQUEST:
                                config.comp = (byte)pack_value(frame[buf_at..], param_len);
                                break;
                            case PI_V42BIS_NUM_CODEWORDS:
                                config.comp_dict_size = (int)pack_value(frame[buf_at..], param_len);
                                break;
                            case PI_V42BIS_MAX_STRING_LENGTH:
                                config.comp_max_string = (int)pack_value(frame[buf_at..], param_len);
                                break;
                            default:
                                break;
                        }
                        buf_at += param_len;
                    }
                    break;
                default:
                    break;
            }
        }
        return 0;
    }

    private static void transmit_xid(v42_state_t ss, byte addr)
    {
        lapm_state_t s;
        byte[] buf;
        int at;
        int len;
        int group_len;
        v42_frame_t? f;

        s = ss.lapm;
        f = get_next_free_ctrl_frame(s);
        if (f is null)
            return;

        buf = f.buf;
        at = 0;
        len = 0;

        buf[at++] = addr;
        buf[at++] = (byte)(LAPM_U_XID | LAPM_FRAMETYPE_U);
        buf[at++] = FI_GENERAL;
        len += 3;

        group_len = 20;
        buf[at++] = GI_PARAM_NEGOTIATION;
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(at, 2), (ushort)group_len);
        at += 2;
        len += 3;

        buf[at++] = PI_HDLC_OPTIONAL_FUNCTIONS;
        buf[at++] = 4;
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(at, 4), 0x8A890000);

        buf[at++] = PI_TX_INFO_MAXSIZE;
        buf[at++] = 2;
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(at, 2), (ushort)(ss.config.v42_tx_n401 << 3));
        at += 2;

        buf[at++] = PI_RX_INFO_MAXSIZE;
        buf[at++] = 2;
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(at, 2), (ushort)(ss.config.v42_rx_n401 << 3));
        at += 2;

        buf[at++] = PI_TX_WINDOW_SIZE;
        buf[at++] = 1;
        buf[at++] = ss.config.v42_tx_window_size_k;

        buf[at++] = PI_RX_WINDOW_SIZE;
        buf[at++] = 1;
        buf[at++] = ss.config.v42_rx_window_size_k;

        len += group_len;

        if (ss.config.comp != 0)
        {
            group_len = 15;
            buf[at++] = GI_PRIVATE_NEGOTIATION;
            BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(at, 2), (ushort)group_len);
            at += 2;
            len += 3;

            buf[at++] = PI_PARAMETER_SET_ID;
            buf[at++] = 3;
            buf[at++] = (byte)'V';
            buf[at++] = (byte)'4';
            buf[at++] = (byte)'2';

            buf[at++] = PI_V42BIS_COMPRESSION_REQUEST;
            buf[at++] = 1;
            buf[at++] = ss.config.comp;

            buf[at++] = PI_V42BIS_NUM_CODEWORDS;
            buf[at++] = 2;
            BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(at, 2), (ushort)ss.config.comp_dict_size);
            at += 2;

            buf[at++] = PI_V42BIS_MAX_STRING_LENGTH;
            buf[at++] = 1;
            buf[at++] = (byte)ss.config.comp_max_string;

            len += group_len;
        }

        f.len = len;
    }

    private static int ms_to_bits(v42_state_t s, int time)
    {
        return time * s.tx_bit_rate / 1000;
    }

    private static void t400_expired(v42_state_t ss)
    {
        ss.bit_timer = 0;
        ss.lapm.state = LAPM_V42_UNSUPPORTED;
        report_rx_status_change(ss, ss.lapm.state);
    }

    private static void t400_start(v42_state_t s)
    {
        s.bit_timer = ms_to_bits(s, T_400);
        s.bit_timer_func = t400_expired;
    }

    private static void t400_stop(v42_state_t s)
    {
        s.bit_timer = 0;
    }

    private static void t401_expired(v42_state_t ss)
    {
        lapm_state_t s;

        span_log(ss.logging, SPAN_LOG_FLOW, "T.401 expired\n");
        s = ss.lapm;
        if (s.retry_count > V42_DEFAULT_N_400)
        {
            s.retry_count = 0;
            switch (s.state)
            {
                case LAPM_ESTABLISH:
                case LAPM_RELEASE:
                    s.state = LAPM_IDLE;
                    report_rx_status_change(ss, (int)SignalStatus.LinkDisconnected);
                    break;
                case LAPM_DATA:
                    lapm_disconnect(ss);
                    break;
            }
            return;
        }
        s.retry_count++;
        if (s.configuring != 0)
        {
            transmit_xid(ss, s.cmd_addr);
        }
        else
        {
            switch (s.state)
            {
                case LAPM_ESTABLISH:
                    tx_unnumbered_frame(s, s.cmd_addr, LAPM_U_SABME | LAPM_U_PF, null, 0);
                    break;
                case LAPM_RELEASE:
                    tx_unnumbered_frame(s, s.cmd_addr, LAPM_U_DISC | LAPM_U_PF, null, 0);
                    break;
                case LAPM_DATA:
                    tx_supervisory_frame(s, s.cmd_addr, (byte)(s.local_busy ? LAPM_S_RNR : LAPM_S_RR), 1);
                    break;
            }
        }
        ss.bit_timer = ms_to_bits(ss, T_401);
        ss.bit_timer_func = t401_expired;
    }

    private static void t401_start(v42_state_t s)
    {
        s.bit_timer = ms_to_bits(s, T_401);
        s.bit_timer_func = t401_expired;
        s.lapm.retry_count = 0;
    }

    private static void t401_stop(v42_state_t s)
    {
        s.bit_timer = 0;
        s.lapm.retry_count = 0;
    }

    private static void t403_expired(v42_state_t ss)
    {
        lapm_state_t s;

        span_log(ss.logging, SPAN_LOG_FLOW, "T.403 expired\n");
        if (ss.lapm.state != LAPM_DATA)
            return;
        s = ss.lapm;
        tx_supervisory_frame(s, s.cmd_addr, (byte)(ss.lapm.local_busy ? LAPM_S_RNR : LAPM_S_RR), 1);
        t401_start(ss);
        ss.lapm.retry_count = 1;
    }

    private static void t401_stop_t403_start(v42_state_t s)
    {
        s.bit_timer = ms_to_bits(s, T_403);
        s.bit_timer_func = t403_expired;
        s.lapm.retry_count = 0;
    }

    private static void initiate_negotiation_expired(v42_state_t s)
    {
        span_log(s.logging, SPAN_LOG_FLOW, "Start negotiation\n");
        lapm_config(s);
        lapm_hdlc_underflow(s);
    }

    private static int tx_information_frame(v42_state_t ss)
    {
        lapm_state_t s;
        v42_frame_t f;
        byte[] tmp;
        int n;
        int info_put_next;

        s = ss.lapm;
        if (s.far_busy || ((s.vs - s.va) & 0x7F) >= s.tx_window_size_k)
            return 0;
        if (s.info_get != s.info_put)
            return 1;
        if ((info_put_next = s.info_put + 1) >= V42_INFO_FRAMES)
            info_put_next = 0;
        if (info_put_next == s.info_get || info_put_next == s.info_acked)
            return 0;
        f = s.info_buf[s.info_put];
        if (s.iframe_get is null)
            return 0;
        tmp = new byte[s.tx_n401];
        n = s.iframe_get(s.iframe_get_user_data, tmp, s.tx_n401);
        if (n < 0)
        {
            report_rx_status_change(ss, (int)SignalStatus.LinkError);
            return 0;
        }
        if (n == 0)
            return 0;

        Array.Copy(tmp, 0, f.buf, 3, n);
        f.len = n + 3;
        s.info_put = info_put_next;
        return 1;
    }

    private static void tx_information_rr_rnr_response(v42_state_t ss, ReadOnlySpan<byte> frame, int len)
    {
        lapm_state_t s;

        s = ss.lapm;
        if ((frame[2] & 0x1) != 0 || tx_information_frame(ss) == 0)
            tx_supervisory_frame(s, frame[0], (byte)(s.local_busy ? LAPM_S_RNR : LAPM_S_RR), 1);
    }

    private static int reject_info(lapm_state_t s)
    {
        byte n;

        if (s.state != LAPM_DATA)
            return 0;
        n = (byte)((s.vs - s.va) & 0x7F);
        s.vs = s.va;
        s.info_get = s.info_acked;
        return n;
    }

    private static int ack_info(v42_state_t ss, byte nr)
    {
        lapm_state_t s;
        int n;

        s = ss.lapm;
        if (!(((((nr - s.va) & 0x7F) + ((s.vs - nr) & 0x7F)) <= s.tx_window_size_k)
              && (((s.vs - s.va) & 0x7F) <= s.tx_window_size_k)))
        {
            lapm_disconnect(ss);
            return -1;
        }
        n = 0;
        while (s.va != nr && s.info_acked != s.info_get)
        {
            if (++s.info_acked >= V42_INFO_FRAMES)
                s.info_acked = 0;
            s.va = (byte)((s.va + 1) & 0x7F);
            n++;
        }
        if (n > 0 && s.retry_count == 0)
        {
            t401_stop_t403_start(ss);
            if (((s.vs - s.va) & 0x7F) != 0)
                t401_start(ss);
        }
        return n;
    }

    private static int valid_data_state(v42_state_t ss)
    {
        lapm_state_t s;

        s = ss.lapm;
        switch (s.state)
        {
            case LAPM_DETECT:
            case LAPM_IDLE:
                break;
            case LAPM_ESTABLISH:
                reset_lapm(ss);
                s.state = LAPM_DATA;
                report_rx_status_change(ss, (int)SignalStatus.LinkConnected);
                return 1;
            case LAPM_DATA:
                return 1;
            case LAPM_RELEASE:
                reset_lapm(ss);
                s.state = LAPM_IDLE;
                report_rx_status_change(ss, (int)SignalStatus.LinkDisconnected);
                break;
            case LAPM_SIGNAL:
            case LAPM_SETPARM:
            case LAPM_TEST:
            case LAPM_V42_UNSUPPORTED:
                break;
        }
        return 0;
    }

    private static void receive_information_frame(v42_state_t ss, ReadOnlySpan<byte> frame, int len)
    {
        lapm_state_t s;

        s = ss.lapm;
        if (valid_data_state(ss) == 0)
            return;
        if (len > s.rx_n401 + 3)
            return;
        ack_info(ss, (byte)(frame[2] >> 1));
        if (s.local_busy)
        {
            if ((frame[2] & 0x1) != 0)
                tx_supervisory_frame(s, s.rsp_addr, LAPM_S_RNR, 1);
            return;
        }
        if ((frame[1] >> 1) != s.vr)
        {
            if (!s.rejected)
            {
                tx_supervisory_frame(s, s.rsp_addr, LAPM_S_REJ, (byte)(frame[2] & 0x1));
                s.rejected = true;
            }
            return;
        }
        s.rejected = false;

        s.iframe_put!(s.iframe_put_user_data, frame.Slice(3, len - 3).ToArray(), len - 3);
        s.vr = (byte)((s.vr + 1) & 0x7F);
        tx_information_rr_rnr_response(ss, frame, len);
    }

    private static void rx_supervisory_cmd_frame(v42_state_t ss, ReadOnlySpan<byte> frame, int len)
    {
        lapm_state_t s;

        s = ss.lapm;
        switch (frame[1] & 0x0C)
        {
            case LAPM_S_RR:
                s.far_busy = false;
                ack_info(ss, (byte)(frame[2] >> 1));
                tx_information_rr_rnr_response(ss, frame, len);
                break;
            case LAPM_S_RNR:
                s.far_busy = true;
                ack_info(ss, (byte)(frame[2] >> 1));
                if ((frame[2] & 0x1) != 0)
                    tx_supervisory_frame(s, s.rsp_addr, (byte)(s.local_busy ? LAPM_S_RNR : LAPM_S_RR), 1);
                break;
            case LAPM_S_REJ:
                s.far_busy = false;
                ack_info(ss, (byte)(frame[2] >> 1));
                if (s.retry_count == 0)
                {
                    t401_stop_t403_start(ss);
                    reject_info(s);
                }
                tx_information_rr_rnr_response(ss, frame, len);
                break;
            case LAPM_S_SREJ:
                return;
            default:
                return;
        }
    }

    private static void rx_supervisory_rsp_frame(v42_state_t ss, ReadOnlySpan<byte> frame, int len)
    {
        lapm_state_t s;

        s = ss.lapm;
        if (s.retry_count == 0 && (frame[2] & 0x1) != 0)
            return;
        switch (frame[1] & 0x0C)
        {
            case LAPM_S_RR:
                s.far_busy = false;
                ack_info(ss, (byte)(frame[2] >> 1));
                if (s.retry_count != 0 && (frame[2] & 0x1) != 0)
                {
                    reject_info(s);
                    t401_stop_t403_start(ss);
                }
                break;
            case LAPM_S_RNR:
                s.far_busy = true;
                ack_info(ss, (byte)(frame[2] >> 1));
                if (s.retry_count != 0 && (frame[2] & 0x1) != 0)
                {
                    reject_info(s);
                    t401_stop_t403_start(ss);
                }
                if (s.retry_count == 0)
                    t401_start(ss);
                break;
            case LAPM_S_REJ:
                s.far_busy = false;
                ack_info(ss, (byte)(frame[2] >> 1));
                if (s.retry_count == 0 || (frame[2] & 0x1) != 0)
                {
                    reject_info(s);
                    t401_stop_t403_start(ss);
                }
                break;
            case LAPM_S_SREJ:
                return;
            default:
                return;
        }
    }

    private static int rx_unnumbered_cmd_frame(v42_state_t ss, ReadOnlySpan<byte> frame, int len)
    {
        lapm_state_t s;

        s = ss.lapm;
        switch (frame[1] & 0xEC)
        {
            case LAPM_U_SABME:
                reset_lapm(ss);
                s.state = LAPM_DATA;
                tx_unnumbered_frame(s, s.rsp_addr, (byte)(LAPM_U_UA | (frame[1] & 0x10)), null, 0);
                t401_stop_t403_start(ss);
                report_rx_status_change(ss, (int)SignalStatus.LinkConnected);
                break;
            case LAPM_U_UI:
                break;
            case LAPM_U_DISC:
                if (s.state == LAPM_IDLE)
                {
                    tx_unnumbered_frame(s, s.rsp_addr, LAPM_U_DM | LAPM_U_PF, null, 0);
                }
                else
                {
                    s.state = LAPM_IDLE;
                    reset_lapm(ss);
                    tx_unnumbered_frame(s, s.rsp_addr, (byte)(LAPM_U_UA | (frame[1] & 0x10)), null, 0);
                    t401_stop(ss);
                    report_rx_status_change(ss, (int)SignalStatus.LinkDisconnected);
                }
                break;
            case LAPM_U_XID:
                receive_xid(ss, frame, len);
                transmit_xid(ss, s.rsp_addr);
                break;
            case LAPM_U_TEST:
                break;
            default:
                return -1;
        }
        return 0;
    }

    private static int rx_unnumbered_rsp_frame(v42_state_t ss, ReadOnlySpan<byte> frame, int len)
    {
        lapm_state_t s;

        s = ss.lapm;
        switch (frame[1] & 0xEC)
        {
            case LAPM_U_DM:
                switch (s.state)
                {
                    case LAPM_IDLE:
                        if ((frame[1] & 0x10) == 0)
                            report_rx_status_change(ss, (int)SignalStatus.LinkConnected);
                        break;
                    case LAPM_ESTABLISH:
                    case LAPM_RELEASE:
                        if ((frame[1] & 0x10) != 0)
                        {
                            s.state = LAPM_IDLE;
                            reset_lapm(ss);
                            t401_stop(ss);
                            report_rx_status_change(ss, (int)SignalStatus.LinkDisconnected);
                        }
                        break;
                    case LAPM_DATA:
                        if (s.retry_count != 0 || (frame[1] & 0x10) == 0)
                        {
                            s.state = LAPM_IDLE;
                            reset_lapm(ss);
                            report_rx_status_change(ss, (int)SignalStatus.LinkDisconnected);
                        }
                        break;
                    default:
                        break;
                }
                break;
            case LAPM_U_UI:
                break;
            case LAPM_U_UA:
                switch (s.state)
                {
                    case LAPM_ESTABLISH:
                        s.state = LAPM_DATA;
                        reset_lapm(ss);
                        t401_stop_t403_start(ss);
                        report_rx_status_change(ss, (int)SignalStatus.LinkConnected);
                        break;
                    case LAPM_RELEASE:
                        s.state = LAPM_IDLE;
                        reset_lapm(ss);
                        t401_stop(ss);
                        report_rx_status_change(ss, (int)SignalStatus.LinkDisconnected);
                        break;
                    default:
                        break;
                }
                break;
            case LAPM_U_FRMR:
                break;
            case LAPM_U_XID:
                if (s.configuring != 0)
                {
                    receive_xid(ss, frame, len);
                    s.configuring = 0;
                    t401_stop(ss);
                    switch (s.state)
                    {
                        case LAPM_IDLE:
                            lapm_connect(ss);
                            break;
                        case LAPM_DATA:
                            s.local_busy = false;
                            tx_supervisory_frame(s, s.cmd_addr, LAPM_S_RR, 0);
                            break;
                    }
                }
                break;
            default:
                break;
        }
        return 0;
    }

    private static void lapm_hdlc_underflow(object? user_data)
    {
        lapm_state_t s;
        v42_frame_t f;
        v42_state_t ss;

        ss = (v42_state_t)user_data!;
        s = ss.lapm;
        if (s.ctrl_get != s.ctrl_put)
        {
            f = s.ctrl_buf[s.ctrl_get];
            if (++s.ctrl_get >= V42_CTRL_FRAMES)
                s.ctrl_get = 0;
        }
        else
        {
            if (s.far_busy || s.configuring != 0 || s.state != LAPM_DATA)
            {
                hdlc_tx_flags(s.hdlc_tx, 10);
                return;
            }
            if (s.info_get == s.info_put && tx_information_frame(ss) == 0)
            {
                hdlc_tx_flags(s.hdlc_tx, 10);
                return;
            }
            f = s.info_buf[s.info_get];
            if (++s.info_get >= V42_INFO_FRAMES)
                s.info_get = 0;

            f.buf[0] = s.cmd_addr;
            f.buf[1] = (byte)(s.vs << 1);
            f.buf[2] = (byte)(s.vr << 1);
            s.vs = (byte)((s.vs + 1) & 0x7F);
            if (ss.bit_timer == 0)
                t401_start(ss);
        }
        hdlc_tx_frame(s.hdlc_tx, f.buf, f.len);
    }

    public static void lapm_receive(object? user_data, ReadOnlyMemory<byte>? frame_memory, int len, bool ok)
    {
        lapm_state_t s;
        v42_state_t ss;
        ReadOnlySpan<byte> frame;

        ss = (v42_state_t)user_data!;
        s = ss.lapm;
        if (len < 0)
        {
            span_log(ss.logging, SPAN_LOG_DEBUG, "V.42 rx status is %s (%d)\n", signal_status_to_str(len), len);
            return;
        }
        if (!ok)
            return;

        frame = frame_memory!.Value.Span;
        switch (frame[1] & LAPM_FRAMETYPE_MASK)
        {
            case LAPM_FRAMETYPE_I:
            case LAPM_FRAMETYPE_I_ALT:
                receive_information_frame(ss, frame, len);
                break;
            case LAPM_FRAMETYPE_S:
                if (valid_data_state(ss) == 0)
                    return;
                if (frame[0] == s.rsp_addr)
                    rx_supervisory_cmd_frame(ss, frame, len);
                else
                    rx_supervisory_rsp_frame(ss, frame, len);
                break;
            case LAPM_FRAMETYPE_U:
                if (frame[0] == s.rsp_addr)
                    rx_unnumbered_cmd_frame(ss, frame, len);
                else
                    rx_unnumbered_rsp_frame(ss, frame, len);
                break;
        }
    }

    private static int lapm_connect(v42_state_t ss)
    {
        lapm_state_t s;

        s = ss.lapm;
        if (s.state != LAPM_IDLE)
            return -1;

        reset_lapm(ss);
        s.state = LAPM_ESTABLISH;
        tx_unnumbered_frame(s, s.cmd_addr, LAPM_U_SABME | LAPM_U_PF, null, 0);
        t401_start(ss);
        return 0;
    }

    private static int lapm_disconnect(v42_state_t ss)
    {
        lapm_state_t s;

        s = ss.lapm;
        s.state = LAPM_RELEASE;
        tx_unnumbered_frame(s, s.cmd_addr, LAPM_U_DISC | LAPM_U_PF, null, 0);
        t401_start(ss);
        return 0;
    }

    private static int lapm_config(v42_state_t ss)
    {
        lapm_state_t s;

        s = ss.lapm;
        s.configuring = 1;
        if (s.state == LAPM_DATA)
        {
            s.local_busy = true;
            tx_supervisory_frame(s, s.cmd_addr, LAPM_S_RNR, 1);
        }
        transmit_xid(ss, s.cmd_addr);
        t401_start(ss);
        return 0;
    }

    private static void reset_lapm(v42_state_t ss)
    {
        lapm_state_t s;

        s = ss.lapm;
        s.local_busy = false;
        s.far_busy = false;
        s.vs = 0;
        s.va = 0;
        s.vr = 0;
        s.info_put = 0;
        s.info_acked = 0;
        s.info_get = 0;
        s.ctrl_put = 0;
        s.ctrl_get = 0;

        s.tx_window_size_k = ss.config.v42_tx_window_size_k;
        s.rx_window_size_k = ss.config.v42_rx_window_size_k;
        s.tx_n401 = ss.config.v42_tx_n401;
        s.rx_n401 = ss.config.v42_rx_n401;
    }

    // v42_start is declared in v42.h, but neither EngineFX v42.c nor spanDSP v42.c implements it.
    // No C# body is added here, because that would invent behaviour absent from the source.

    public static void v42_stop(v42_state_t ss)
    {
        lapm_state_t s;

        s = ss.lapm;
        ss.bit_timer = 0;
        s.packer_process = null;
        lapm_disconnect(ss);
    }

    private static void restart_lapm(v42_state_t s)
    {
        if (s.calling_party)
        {
            s.bit_timer = 48 * 8;
            s.bit_timer_func = initiate_negotiation_expired;
        }
        else
        {
            lapm_hdlc_underflow(s);
        }
        s.lapm.packer_process = null;
        s.lapm.state = LAPM_IDLE;
    }

    private static void negotiation_rx_bit(v42_state_t s, int new_bit)
    {
        if (new_bit < 0)
        {
            span_log(s.logging, SPAN_LOG_DEBUG, "V.42 rx status is %s (%d)\n", signal_status_to_str(new_bit), new_bit);
            return;
        }
        new_bit &= 1;
        s.neg.rxstream = unchecked((s.neg.rxstream << 1) | new_bit);
        switch (s.neg.rx_negotiation_step)
        {
            case 0:
                if (new_bit != 0)
                    break;
                s.neg.rx_negotiation_step = 1;
                s.neg.rxbits = 0;
                s.neg.rxstream = ~1;
                s.neg.rxoks = 0;
                break;
            case 1:
                if (++s.neg.rxbits < 9)
                    break;
                s.neg.rxstream &= 0x3FF;
                if (s.calling_party && s.neg.rxstream == 0x145)
                {
                    s.neg.rx_negotiation_step++;
                }
                else if (!s.calling_party && s.neg.rxstream == 0x111)
                {
                    s.neg.rx_negotiation_step++;
                }
                else
                {
                    s.neg.rx_negotiation_step = 0;
                }
                s.neg.rxbits = 0;
                s.neg.rxstream = ~0;
                break;
            case 2:
                s.neg.rxbits++;
                if (new_bit != 0)
                    break;
                if (s.neg.rxbits >= 8 && s.neg.rxbits <= 16)
                    s.neg.rx_negotiation_step++;
                else
                    s.neg.rx_negotiation_step = 0;
                s.neg.rxbits = 0;
                s.neg.rxstream = ~1;
                break;
            case 3:
                if (++s.neg.rxbits < 9)
                    break;
                s.neg.rxstream &= 0x3FF;
                if (s.calling_party && s.neg.rxstream == 0x185)
                {
                    s.neg.rx_negotiation_step++;
                }
                else if (s.calling_party && s.neg.rxstream == 0x001)
                {
                    s.neg.rx_negotiation_step++;
                }
                else if (!s.calling_party && s.neg.rxstream == 0x113)
                {
                    s.neg.rx_negotiation_step++;
                }
                else
                {
                    s.neg.rx_negotiation_step = 0;
                }
                s.neg.rxbits = 0;
                s.neg.rxstream = ~0;
                break;
            case 4:
                s.neg.rxbits++;
                if (new_bit != 0)
                    break;
                if (s.neg.rxbits >= 8 && s.neg.rxbits <= 16)
                {
                    if (++s.neg.rxoks >= 2)
                    {
                        s.neg.rx_negotiation_step++;
                        if (s.calling_party)
                        {
                            t400_stop(s);
                            s.lapm.state = LAPM_IDLE;
                            report_rx_status_change(s, s.lapm.state);
                            restart_lapm(s);
                        }
                        else
                        {
                            s.neg.odp_seen = 1;
                        }
                        break;
                    }
                    s.neg.rx_negotiation_step = 1;
                    s.neg.rxbits = 0;
                    s.neg.rxstream = ~1;
                }
                else
                {
                    s.neg.rx_negotiation_step = 0;
                    s.neg.rxbits = 0;
                    s.neg.rxstream = ~0;
                }
                break;
            case 5:
                break;
        }
    }

    private static int v42_support_negotiation_tx_bit(v42_state_t s)
    {
        int bit;

        if (s.calling_party)
        {
            if (s.neg.txbits <= 0)
            {
                s.neg.txstream = 0x3FE22;
                s.neg.txbits = 36;
            }
            else if (s.neg.txbits == 18)
            {
                s.neg.txstream = 0x3FF22;
            }
            bit = s.neg.txstream & 1;
            s.neg.txstream >>= 1;
            s.neg.txbits--;
        }
        else
        {
            if (s.neg.odp_seen != 0 && s.neg.txadps < 10)
            {
                if (s.neg.txbits <= 0)
                {
                    if (++s.neg.txadps >= 10)
                    {
                        t400_stop(s);
                        s.lapm.state = LAPM_IDLE;
                        report_rx_status_change(s, s.lapm.state);
                        s.neg.txstream = 1;
                        restart_lapm(s);
                    }
                    else
                    {
                        s.neg.txstream = 0x3FE8A;
                        s.neg.txbits = 36;
                    }
                }
                else if (s.neg.txbits == 18)
                {
                    s.neg.txstream = 0x3FE86;
                }
                bit = s.neg.txstream & 1;
                s.neg.txstream >>= 1;
                s.neg.txbits--;
            }
            else
            {
                bit = 1;
            }
        }
        return bit;
    }

    public static void v42_rx_bit(object? user_data, int bit)
    {
        v42_state_t s;

        s = (v42_state_t)user_data!;
        if (s.lapm.state == LAPM_DETECT)
            negotiation_rx_bit(s, bit);
        else
            hdlc_rx_put_bit(s.lapm.hdlc_rx, bit);
    }

    public static int v42_tx_bit(object? user_data)
    {
        v42_state_t s;
        int bit;

        s = (v42_state_t)user_data!;
        if (s.bit_timer != 0 && --s.bit_timer <= 0)
        {
            s.bit_timer = 0;
            s.bit_timer_func!(s);
        }
        if (s.lapm.state == LAPM_DETECT)
            bit = v42_support_negotiation_tx_bit(s);
        else
            bit = hdlc_tx_get_bit(s.lapm.hdlc_tx);
        return bit;
    }

    public static bool v42_set_local_busy_status(v42_state_t s, bool busy)
    {
        bool previous_busy;

        previous_busy = s.lapm.local_busy;
        s.lapm.local_busy = busy;
        return previous_busy;
    }

    public static bool v42_get_far_busy_status(v42_state_t s)
    {
        return s.lapm.far_busy;
    }

    public static SpanLogState v42_get_logging_state(v42_state_t s)
    {
        return s.logging;
    }

    public static void v42_set_status_callback(v42_state_t s, SpanModemStatusDelegate? status_handler, object? user_data)
    {
        s.lapm.status_handler = status_handler;
        s.lapm.status_user_data = user_data;
    }

    public static void v42_restart(v42_state_t s)
    {
        s.lapm.hdlc_tx = hdlc_tx_init(s.lapm.hdlc_tx, false, 1, true, lapm_hdlc_underflow, s);
        s.lapm.hdlc_rx = hdlc_rx_init(s.lapm.hdlc_rx, false, false, 1, lapm_receive, s);

        if (s.detect)
        {
            s.neg.txstream = ~0;
            s.neg.txbits = 0;
            s.neg.rxstream = ~0;
            s.neg.rxbits = 0;
            s.neg.rxoks = 0;
            s.neg.txadps = 0;
            s.neg.rx_negotiation_step = 0;
            s.neg.odp_seen = 0;
            t400_start(s);
            s.lapm.state = LAPM_DETECT;
        }
        else
        {
            s.lapm.state = LAPM_IDLE;
            restart_lapm(s);
        }
    }

    public static v42_state_t? v42_init(
        v42_state_t? ss,
        bool calling_party,
        bool detect,
        span_get_msg_func_t? iframe_get,
        span_put_msg_func_t? iframe_put,
        object? user_data)
    {
        lapm_state_t s;

        if (ss is null)
            ss = new v42_state_t();

        ss.calling_party = false;
        ss.detect = false;
        ss.tx_bit_rate = 0;
        ss.config = new v42_config_parameters_t();
        ss.neg = new v42_negotiation_t();
        ss.lapm = new lapm_state_t();
        ss.bit_timer = 0;
        ss.bit_timer_func = null;
        ss.logging = new SpanLogState();

        s = ss.lapm;
        ss.calling_party = calling_party;
        ss.detect = detect;
        s.iframe_get = iframe_get;
        s.iframe_get_user_data = user_data;
        s.iframe_put = iframe_put;
        s.iframe_put_user_data = user_data;

        s.state = ss.detect ? LAPM_DETECT : LAPM_IDLE;
        s.local_busy = false;
        s.far_busy = false;

        s.cmd_addr = (byte)((LAPM_DLCI_DTE_TO_DTE << 2) | (ss.calling_party ? 0x02 : 0x00) | 0x01);
        s.rsp_addr = (byte)((LAPM_DLCI_DTE_TO_DTE << 2) | (ss.calling_party ? 0x00 : 0x02) | 0x01);

        ss.config.v42_tx_window_size_k = V42_DEFAULT_WINDOW_SIZE_K;
        ss.config.v42_rx_window_size_k = V42_DEFAULT_WINDOW_SIZE_K;
        ss.config.v42_tx_n401 = V42_DEFAULT_N_401;
        ss.config.v42_rx_n401 = V42_DEFAULT_N_401;

        ss.config.comp = 1;
        ss.config.comp_dict_size = 512;
        ss.config.comp_max_string = 6;

        ss.tx_bit_rate = 28800;

        reset_lapm(ss);

        ss.logging = span_log_init(ss.logging, SPAN_LOG_NONE, null);
        span_log_set_protocol(ss.logging, "V.42");
        return ss;
    }

    public static int v42_release(v42_state_t s)
    {
        reset_lapm(s);
        return 0;
    }

    public static int v42_free(v42_state_t s)
    {
        v42_release(s);
        return 0;
    }
}
