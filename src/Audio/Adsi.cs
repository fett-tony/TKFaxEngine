/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * Adsi.cs - Analogue display service interfaces of various types, including
 *           ADSI, TDD and most caller ID formats.
 *
 * Written by Steve Underwood <steveu@coppice.org>
 *
 * Copyright (C) 2003 Steve Underwood
 *
 * All rights reserved.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License version 2.1,
 * as published by the Free Software Foundation.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Lesser General Public License for more details.
 */

#nullable enable

using System;
using TKFaxEngine;

using fsk_tx_state_t = TKFaxEngine.Modem.FskTxState;
using fsk_rx_state_t = TKFaxEngine.Modem.FskRxState;
using dtmf_tx_state_t = TKFaxEngine.Audio.DtmfTxState;
using dtmf_rx_state_t = TKFaxEngine.Audio.DtmfRxState;
using async_tx_state_t = TKFaxEngine.AsyncTransmitter;
using logging_state_t = TKFaxEngine.SpanLogState;
using span_put_msg_func_t = TKFaxEngine.SpanPutMessageDelegate;

using static TKFaxEngine.AsyncApi;
using static TKFaxEngine.CrcApi;
using static TKFaxEngine.LoggingApi;
using static TKFaxEngine.Audio.Dtmf;
using static TKFaxEngine.Audio.tone_generate;
using static TKFaxEngine.Modem.FskApi;

namespace TKFaxEngine.Audio;

public sealed class adsi_tx_state_t
{
    internal int standard;

    internal tone_gen_descriptor_t alert_tone_desc = null!;
    internal tone_gen_state_t? alert_tone_gen;
    internal fsk_tx_state_t fsk_tx = null!;
    internal dtmf_tx_state_t dtmf_tx = null!;
    internal async_tx_state_t async_tx = null!;

    internal int tx_signal_on;

    internal int byte_no;
    internal int bit_pos;
    internal int bit_no;
    internal readonly byte[] msg = new byte[256];
    internal int msg_len;
    internal int preamble_len;
    internal int preamble_ones_len;
    internal int postamble_ones_len;
    internal int stop_bits;
    internal int baudot_shift;

    internal logging_state_t logging = null!;
}

public sealed class adsi_rx_state_t
{
    internal int standard;
    internal span_put_msg_func_t put_msg = null!;
    internal object? user_data;

    internal fsk_rx_state_t fsk_rx = null!;
    internal dtmf_rx_state_t dtmf_rx = null!;

    internal int consecutive_ones;
    internal int bit_pos;
    internal int in_progress;
    internal readonly byte[] msg = new byte[256];
    internal int msg_len;
    internal int baudot_shift;

    internal int framing_errors;

    internal logging_state_t logging = null!;
}

public static class Adsi
{
    public const int ADSI_STANDARD_NONE = 0;
    public const int ADSI_STANDARD_CLASS = 1;
    public const int ADSI_STANDARD_CLIP = 2;
    public const int ADSI_STANDARD_ACLIP = 3;
    public const int ADSI_STANDARD_JCLIP = 4;
    public const int ADSI_STANDARD_CLIP_DTMF = 5;
    public const int ADSI_STANDARD_TDD = 6;

    public const int CLASS_SDMF_CALLERID = 0x04;
    public const int CLASS_MDMF_CALLERID = 0x80;
    public const int CLASS_SDMF_MSG_WAITING = 0x06;
    public const int CLASS_MDMF_MSG_WAITING = 0x82;

    public const int MCLASS_DATETIME = 0x01;
    public const int MCLASS_CALLER_NUMBER = 0x02;
    public const int MCLASS_DIALED_NUMBER = 0x03;
    public const int MCLASS_ABSENCE1 = 0x04;
    public const int MCLASS_REDIRECT = 0x05;
    public const int MCLASS_QUALIFIER = 0x06;
    public const int MCLASS_CALLER_NAME = 0x07;
    public const int MCLASS_ABSENCE2 = 0x08;
    public const int MCLASS_ALT_ROUTE = 0x09;
    public const int MCLASS_VISUAL_INDICATOR = 0x0B;

    public const int CLIP_MDMF_CALLERID = 0x80;
    public const int CLIP_MDMF_MSG_WAITING = 0x82;
    public const int CLIP_MDMF_CHARGE_INFO = 0x86;
    public const int CLIP_MDMF_SMS = 0x89;

    public const int CLIP_DATETIME = 0x01;
    public const int CLIP_CALLER_NUMBER = 0x02;
    public const int CLIP_DIALED_NUMBER = 0x03;
    public const int CLIP_ABSENCE1 = 0x04;
    public const int CLIP_CALLER_NAME = 0x07;
    public const int CLIP_ABSENCE2 = 0x08;
    public const int CLIP_VISUAL_INDICATOR = 0x0B;
    public const int CLIP_MESSAGE_ID = 0x0D;
    public const int CLIP_COMPLEMENTARY_CALLER_NUMBER = 0x10;
    public const int CLIP_CALLTYPE = 0x11;
    public const int CLIP_NUM_MSG = 0x13;
    public const int CLIP_TYPE_OF_FORWARDED_CALL = 0x15;
    public const int CLIP_TYPE_OF_CALLING_USER = 0x16;
    public const int CLIP_REDIR_NUMBER = 0x1A;
    public const int CLIP_CHARGE = 0x20;
    public const int CLIP_DURATION = 0x23;
    public const int CLIP_ADD_CHARGE = 0x21;
    public const int CLIP_DISPLAY_INFO = 0x50;
    public const int CLIP_SERVICE_INFO = 0x55;

    public const int ACLIP_SDMF_CALLERID = 0x04;
    public const int ACLIP_MDMF_CALLERID = 0x80;

    public const int ACLIP_DATETIME = 0x01;
    public const int ACLIP_CALLER_NUMBER = 0x02;
    public const int ACLIP_DIALED_NUMBER = 0x03;
    public const int ACLIP_NUMBER_ABSENCE = 0x04;
    public const int ACLIP_REDIRECT = 0x05;
    public const int ACLIP_QUALIFIER = 0x06;
    public const int ACLIP_CALLER_NAME = 0x07;
    public const int ACLIP_NAME_ABSENCE = 0x08;

    public const int JCLIP_MDMF_CALLERID = 0x40;

    public const int JCLIP_CALLER_NUMBER = 0x02;
    public const int JCLIP_CALLER_NUM_DES = 0x21;
    public const int JCLIP_DIALED_NUMBER = 0x09;
    public const int JCLIP_DIALED_NUM_DES = 0x22;
    public const int JCLIP_ABSENCE = 0x04;

    public const char CLIP_DTMF_HASH_TERMINATED = '#';
    public const char CLIP_DTMF_C_TERMINATED = 'C';
    public const char CLIP_DTMF_HASH_CALLER_NUMBER = 'A';
    public const char CLIP_DTMF_HASH_ABSENCE = 'D';
    public const int CLIP_DTMF_HASH_UNSPECIFIED = 0;
    public const char CLIP_DTMF_C_CALLER_NUMBER = 'A';
    public const char CLIP_DTMF_C_REDIRECT_NUMBER = 'D';
    public const char CLIP_DTMF_C_ABSENCE = 'B';

    private const int BAUDOT_FIGURE_SHIFT = 0x1B;
    private const int BAUDOT_LETTER_SHIFT = 0x1F;

    private const int SOH = 0x01;
    private const int STX = 0x02;
    private const int ETX = 0x03;
    private const int DLE = 0x10;
    private const int SUB = 0x1A;

    private const int ASYNC_PARITY_NONE = 0;
    private const int SIG_STATUS_CARRIER_DOWN = (int)SignalStatus.CarrierDown;
    private const int SIG_STATUS_CARRIER_UP = (int)SignalStatus.CarrierUp;
    private const int SIG_STATUS_END_OF_DATA = (int)SignalStatus.EndOfData;

    private static readonly byte[] adsi_encode_baudot_conv =
    [
        0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0x42, 0xFF, 0xFF, 0x48, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0x44, 0xFF, 0xFF, 0x94, 0x89, 0xFF, 0xFF, 0x85,
        0x8F, 0x92, 0x8B, 0x91, 0x8C, 0x83, 0x9C, 0x9D,
        0x96, 0x97, 0x93, 0x81, 0x8A, 0x90, 0x95, 0x87,
        0x86, 0x98, 0x8E, 0xFF, 0xFF, 0x9E, 0xFF, 0x99,
        0xFF, 0x03, 0x19, 0x0E, 0x09, 0x01, 0x0D, 0x1A,
        0x14, 0x06, 0x0B, 0x0F, 0x12, 0x1C, 0x0C, 0x18,
        0x16, 0x17, 0x0A, 0x05, 0x10, 0x07, 0x1E, 0x13,
        0x1D, 0x15, 0x11, 0xFF, 0xFF, 0xFF, 0x9B, 0xFF,
        0xFF, 0x03, 0x19, 0x0E, 0x09, 0x01, 0x0D, 0x1A,
        0x14, 0x06, 0x0B, 0x0F, 0x12, 0x1C, 0x0C, 0x18,
        0x16, 0x17, 0x0A, 0x05, 0x10, 0x07, 0x1E, 0x13,
        0x1D, 0x15, 0x11, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
    ];

    private static readonly byte[][] adsi_decode_baudot_conv =
    [
        [
            0x00, (byte)'E', 0x0A, (byte)'A', (byte)' ', (byte)'S', (byte)'I', (byte)'U',
            0x0D, (byte)'D', (byte)'R', (byte)'J', (byte)'N', (byte)'F', (byte)'C', (byte)'K',
            (byte)'T', (byte)'Z', (byte)'L', (byte)'W', (byte)'H', (byte)'Y', (byte)'P', (byte)'Q',
            (byte)'O', (byte)'B', (byte)'G', (byte)'^', (byte)'M', (byte)'X', (byte)'V', (byte)'^'
        ],
        [
            0x00, (byte)'3', 0x0A, (byte)'-', (byte)' ', (byte)'\'', (byte)'8', (byte)'7',
            0x0D, (byte)'$', (byte)'4', (byte)'*', (byte)',', (byte)'*', (byte)':', (byte)'(',
            (byte)'5', (byte)'+', (byte)')', (byte)'2', (byte)'#', (byte)'6', (byte)'0', (byte)'1',
            (byte)'9', (byte)'?', (byte)'*', (byte)'^', (byte)'.', (byte)'/', (byte)'=', (byte)'^'
        ]
    ];

    private static int adsi_tx_get_bit(object? user_data)
    {
        int bit;
        adsi_tx_state_t s;

        s = (adsi_tx_state_t)user_data!;
        if (s.bit_no < s.preamble_len)
        {
            bit = s.bit_no & 1;
            s.bit_no++;
        }
        else if (s.bit_no < s.preamble_len + s.preamble_ones_len)
        {
            bit = 1;
            s.bit_no++;
        }
        else if (s.bit_no <= s.preamble_len + s.preamble_ones_len)
        {
            if (s.bit_pos == 0)
            {
                bit = 0;
                s.bit_pos++;
            }
            else if (s.bit_pos < 1 + 8)
            {
                bit = (s.msg[s.byte_no] >> (s.bit_pos - 1)) & 1;
                s.bit_pos++;
            }
            else if (s.bit_pos < 1 + 8 + s.stop_bits - 1)
            {
                bit = 1;
                s.bit_pos++;
            }
            else
            {
                bit = 1;
                s.bit_pos = 0;
                if (++s.byte_no >= s.msg_len)
                    s.bit_no++;
            }
        }
        else if (s.bit_no <= s.preamble_len + s.preamble_ones_len + s.postamble_ones_len)
        {
            bit = 1;
            s.bit_no++;
        }
        else
        {
            bit = SIG_STATUS_END_OF_DATA;
            if (s.tx_signal_on != 0)
            {
                s.tx_signal_on = 0;
                s.msg_len = 0;
            }
        }
        return bit;
    }

    private static int adsi_tdd_get_async_byte(object? user_data)
    {
        adsi_tx_state_t s;

        s = (adsi_tx_state_t)user_data!;
        if (s.byte_no < s.msg_len)
            return s.msg[s.byte_no++];
        if (s.tx_signal_on != 0)
        {
            s.tx_signal_on = 0;
            s.msg_len = 0;
        }
        return 0x1F;
    }

    private static void adsi_rx_put_bit(object? user_data, int bit)
    {
        adsi_rx_state_t s;
        int i;
        int sum;

        s = (adsi_rx_state_t)user_data!;
        if (bit < 0)
        {
            span_log(s.logging, SPAN_LOG_FLOW, "ADSI signal status is %s (%d)\n", signal_status_to_str(bit), bit);
            switch (bit)
            {
                case SIG_STATUS_CARRIER_UP:
                    s.consecutive_ones = 0;
                    s.bit_pos = 0;
                    s.in_progress = 0;
                    s.msg_len = 0;
                    break;
                case SIG_STATUS_CARRIER_DOWN:
                    break;
                default:
                    span_log(s.logging, SPAN_LOG_WARNING, "Unexpected special put bit value - %d!\n", bit);
                    break;
            }
            return;
        }
        bit &= 1;
        if (s.bit_pos == 0)
        {
            if (bit == 0)
            {
                s.bit_pos++;
                if (s.consecutive_ones > 10)
                    s.msg_len = 0;
                s.consecutive_ones = 0;
            }
            else
            {
                s.consecutive_ones++;
            }
        }
        else if (s.bit_pos <= 8)
        {
            s.in_progress >>= 1;
            if (bit != 0)
                s.in_progress |= 0x80;
            s.bit_pos++;
        }
        else
        {
            if (bit != 0)
            {
                if (s.msg_len < 256)
                {
                    if (s.standard == ADSI_STANDARD_JCLIP)
                    {
                        if (s.msg_len == 0)
                        {
                            if (s.in_progress == (0x80 | DLE))
                                s.msg[s.msg_len++] = (byte)s.in_progress;
                        }
                        else
                        {
                            s.msg[s.msg_len++] = (byte)s.in_progress;
                        }
                        if (s.msg_len >= 11 && s.msg_len == ((s.msg[6] & 0x7F) + 11))
                        {
                            if (crc_itu16_calc(s.msg.AsSpan(2), s.msg_len - 2, 0) == 0)
                            {
                                for (i = 0; i < s.msg_len - 2; i++)
                                    s.msg[i] &= 0x7F;
                                s.put_msg(s.user_data, s.msg.AsSpan(0, s.msg_len - 2));
                            }
                            else
                            {
                                span_log(s.logging, SPAN_LOG_WARNING, "CRC failed\n");
                            }
                            s.msg_len = 0;
                        }
                    }
                    else
                    {
                        s.msg[s.msg_len++] = (byte)s.in_progress;
                        if (s.msg_len >= 3 && s.msg_len == (s.msg[1] + 3))
                        {
                            sum = 0;
                            for (i = 0; i < s.msg_len - 1; i++)
                                sum += s.msg[i];
                            if (((-sum) & 0xFF) == s.msg[i])
                                s.put_msg(s.user_data, s.msg.AsSpan(0, s.msg_len - 1));
                            else
                                span_log(s.logging, SPAN_LOG_WARNING, "Sumcheck failed\n");
                            s.msg_len = 0;
                        }
                    }
                }
            }
            else
            {
                s.framing_errors++;
            }
            s.bit_pos = 0;
            s.in_progress = 0;
        }
    }

    private static void adsi_tdd_put_async_byte(object? user_data, int @byte)
    {
        adsi_rx_state_t s;
        byte octet;

        s = (adsi_rx_state_t)user_data!;
        if (@byte < 0)
        {
            span_log(s.logging, SPAN_LOG_FLOW, "ADSI signal status is %s (%d)\n", signal_status_to_str(@byte), @byte);
            switch (@byte)
            {
                case SIG_STATUS_CARRIER_UP:
                    s.consecutive_ones = 0;
                    s.bit_pos = 0;
                    s.in_progress = 0;
                    s.msg_len = 0;
                    break;
                case SIG_STATUS_CARRIER_DOWN:
                    if (s.msg_len > 0)
                    {
                        s.put_msg(s.user_data, s.msg.AsSpan(0, s.msg_len));
                        s.msg_len = 0;
                    }
                    break;
                default:
                    span_log(s.logging, SPAN_LOG_WARNING, "Unexpected special put byte value - %d!\n", @byte);
                    break;
            }
            return;
        }
        if ((octet = adsi_decode_baudot(s, (byte)(@byte & 0x1F))) != 0)
            s.msg[s.msg_len++] = octet;
        if (s.msg_len >= 256)
        {
            s.put_msg(s.user_data, s.msg.AsSpan(0, s.msg_len));
            s.msg_len = 0;
        }
    }

    private static void adsi_rx_dtmf(object? user_data, string digits, int len)
    {
        adsi_rx_state_t s;
        int pos;

        s = (adsi_rx_state_t)user_data!;
        if (s.msg_len == 0)
            s.in_progress = 80000;
        pos = 0;
        for (; len != 0 && s.msg_len < 256; len--)
        {
            s.msg[s.msg_len++] = (byte)digits[pos];
            if (digits[pos] == '#' || digits[pos] == 'C')
            {
                s.put_msg(s.user_data, s.msg.AsSpan(0, s.msg_len));
                s.msg_len = 0;
            }
            pos++;
        }
    }

    private static void start_tx(adsi_tx_state_t s)
    {
        switch (s.standard)
        {
            case ADSI_STANDARD_CLASS:
                s.fsk_tx = fsk_tx_init(s.fsk_tx, preset_fsk_specs[FSK_BELL202], adsi_tx_get_bit, s);
                break;
            case ADSI_STANDARD_CLIP:
            case ADSI_STANDARD_ACLIP:
            case ADSI_STANDARD_JCLIP:
                s.fsk_tx = fsk_tx_init(s.fsk_tx, preset_fsk_specs[FSK_V23CH1], adsi_tx_get_bit, s);
                break;
            case ADSI_STANDARD_CLIP_DTMF:
                s.dtmf_tx = dtmf_tx_init(s.dtmf_tx, null, null);
                break;
            case ADSI_STANDARD_TDD:
                s.fsk_tx = fsk_tx_init(s.fsk_tx, preset_fsk_specs[FSK_WEITBRECHT_4545], async_tx_get_bit, s.async_tx);
                s.async_tx = async_tx_init(s.async_tx, 5, ASYNC_PARITY_NONE, 2, false, adsi_tdd_get_async_byte, s);
                s.baudot_shift = 2;
                break;
        }
        s.tx_signal_on = 1;
    }

    public static int adsi_rx(adsi_rx_state_t s, short[] amp, int len)
    {
        switch (s.standard)
        {
            case ADSI_STANDARD_CLIP_DTMF:
                s.in_progress -= len;
                if (s.in_progress <= 0)
                    s.msg_len = 0;
                dtmf_rx(s.dtmf_rx, amp, len);
                break;
            default:
                fsk_rx(s.fsk_rx, amp, len);
                break;
        }
        return 0;
    }

    public static logging_state_t adsi_rx_get_logging_state(adsi_rx_state_t s)
    {
        return s.logging;
    }

    public static adsi_rx_state_t adsi_rx_init(
        adsi_rx_state_t? s,
        int standard,
        span_put_msg_func_t put_msg,
        object? user_data)
    {
        if (s == null)
            s = new adsi_rx_state_t();

        s.standard = 0;
        s.put_msg = null!;
        s.user_data = null;
        s.fsk_rx = new fsk_rx_state_t();
        s.dtmf_rx = new dtmf_rx_state_t();
        s.consecutive_ones = 0;
        s.bit_pos = 0;
        s.in_progress = 0;
        Array.Clear(s.msg, 0, s.msg.Length);
        s.msg_len = 0;
        s.baudot_shift = 0;
        s.framing_errors = 0;
        s.logging = new logging_state_t();

        s.put_msg = put_msg;
        s.user_data = user_data;
        switch (standard)
        {
            case ADSI_STANDARD_CLASS:
                s.fsk_rx = fsk_rx_init(s.fsk_rx, preset_fsk_specs[FSK_BELL202], FSK_FRAME_MODE_ASYNC, adsi_rx_put_bit, s);
                break;
            case ADSI_STANDARD_CLIP:
            case ADSI_STANDARD_ACLIP:
            case ADSI_STANDARD_JCLIP:
                s.fsk_rx = fsk_rx_init(s.fsk_rx, preset_fsk_specs[FSK_V23CH1], FSK_FRAME_MODE_ASYNC, adsi_rx_put_bit, s);
                break;
            case ADSI_STANDARD_CLIP_DTMF:
                s.dtmf_rx = dtmf_rx_init(s.dtmf_rx, adsi_rx_dtmf, s);
                break;
            case ADSI_STANDARD_TDD:
                s.fsk_rx = fsk_rx_init(
                    s.fsk_rx,
                    preset_fsk_specs[FSK_WEITBRECHT_4545],
                    FSK_FRAME_MODE_FRAMED,
                    adsi_tdd_put_async_byte,
                    s);
                fsk_rx_set_frame_parameters(s.fsk_rx, 5, ASYNC_PARITY_NONE, 2);
                break;
        }
        s.standard = standard;
        s.logging = span_log_init(s.logging, SPAN_LOG_NONE, null);
        return s;
    }

    public static int adsi_rx_release(adsi_rx_state_t s)
    {
        return 0;
    }

    public static int adsi_rx_free(adsi_rx_state_t? s)
    {
        return 0;
    }

    public static int adsi_tx(adsi_tx_state_t s, short[] amp, int max_len)
    {
        int len;
        int lenx;

        len = s.alert_tone_gen == null ? 0 : tone_gen(s.alert_tone_gen, amp, max_len);
        if (s.tx_signal_on != 0)
        {
            switch (s.standard)
            {
                case ADSI_STANDARD_CLIP_DTMF:
                    if (len < max_len)
                        len += dtmf_tx(s.dtmf_tx, amp, max_len - len);
                    break;
                default:
                    if (len < max_len)
                    {
                        lenx = fsk_tx(s.fsk_tx, amp.AsSpan(len, max_len - len));
                        if (lenx <= 0)
                            s.tx_signal_on = 0;
                        len += lenx;
                    }
                    break;
            }
        }
        return len;
    }

    public static void adsi_tx_send_alert_tone(adsi_tx_state_t s)
    {
        s.alert_tone_gen = tone_gen_init(s.alert_tone_gen, s.alert_tone_desc);
    }

    public static void adsi_tx_set_preamble(
        adsi_tx_state_t s,
        int preamble_len,
        int preamble_ones_len,
        int postamble_ones_len,
        int stop_bits)
    {
        if (preamble_len < 0)
        {
            if (s.standard == ADSI_STANDARD_JCLIP)
                s.preamble_len = 0;
            else
                s.preamble_len = 300;
        }
        else
        {
            s.preamble_len = preamble_len;
        }
        if (preamble_ones_len < 0)
        {
            if (s.standard == ADSI_STANDARD_JCLIP)
                s.preamble_ones_len = 75;
            else
                s.preamble_ones_len = 80;
        }
        else
        {
            s.preamble_ones_len = preamble_ones_len;
        }
        if (postamble_ones_len < 0)
        {
            s.postamble_ones_len = 5;
        }
        else
        {
            s.postamble_ones_len = postamble_ones_len;
        }
        if (stop_bits < 0)
        {
            if (s.standard == ADSI_STANDARD_JCLIP)
                s.stop_bits = 4;
            else
                s.stop_bits = 1;
        }
        else
        {
            s.stop_bits = stop_bits;
        }
    }

    public static int adsi_tx_put_message(adsi_tx_state_t s, byte[] msg, int len)
    {
        int i;
        int j;
        int k;
        int @byte;
        int parity;
        int sum;
        ushort crc_value;

        if (s.msg_len > 0)
            return 0;
        if (s.tx_signal_on == 0)
            start_tx(s);
        switch (s.standard)
        {
            case ADSI_STANDARD_CLIP_DTMF:
                if (len >= 128)
                    return -1;
                char[] digits = new char[len];
                for (i = 0; i < len; i++)
                    digits[i] = (char)msg[i];
                len -= dtmf_tx_put(s.dtmf_tx, new string(digits), len);
                break;
            case ADSI_STANDARD_JCLIP:
                if (len > 128 - 9)
                    return -1;
                i = 0;
                s.msg[i++] = DLE;
                s.msg[i++] = SOH;
                s.msg[i++] = 0x07;
                s.msg[i++] = DLE;
                s.msg[i++] = STX;
                s.msg[i++] = msg[0];
                s.msg[i++] = (byte)(len - 2);
                if (len - 2 == DLE)
                    s.msg[i++] = DLE;
                Array.Copy(msg, 2, s.msg, i, len - 2);
                i += len - 2;
                s.msg[i++] = DLE;
                s.msg[i++] = ETX;
                for (j = 0; j < i; j++)
                {
                    @byte = s.msg[j];
                    parity = 0;
                    for (k = 1; k <= 7; k++)
                        parity ^= @byte << k;
                    s.msg[j] = (byte)((s.msg[j] & 0x7F) | (parity & 0x80));
                }
                crc_value = crc_itu16_calc(s.msg.AsSpan(2), i - 2, 0);
                s.msg[i++] = (byte)(crc_value & 0xFF);
                s.msg[i++] = (byte)((crc_value >> 8) & 0xFF);
                s.msg_len = i;
                break;
            case ADSI_STANDARD_TDD:
                if (len > 255)
                    return -1;
                Array.Copy(msg, 0, s.msg, 0, len);
                s.msg_len = len;
                break;
            default:
                if (len > 255)
                    return -1;
                Array.Copy(msg, 0, s.msg, 0, len);
                s.msg[1] = (byte)(len - 2);
                sum = 0;
                for (i = 0; i < len; i++)
                    sum += s.msg[i];
                s.msg[len] = (byte)((-sum) & 0xFF);
                s.msg_len = len + 1;
                break;
        }
        s.byte_no = 0;
        s.bit_pos = 0;
        s.bit_no = 0;
        return len;
    }

    public static logging_state_t adsi_tx_get_logging_state(adsi_tx_state_t s)
    {
        return s.logging;
    }

    public static adsi_tx_state_t adsi_tx_init(adsi_tx_state_t? s, int standard)
    {
        if (s == null)
            s = new adsi_tx_state_t();

        s.standard = 0;
        s.alert_tone_desc = new tone_gen_descriptor_t();
        s.alert_tone_gen = null;
        s.fsk_tx = new fsk_tx_state_t();
        s.dtmf_tx = new dtmf_tx_state_t();
        s.async_tx = new async_tx_state_t();
        s.tx_signal_on = 0;
        s.byte_no = 0;
        s.bit_pos = 0;
        s.bit_no = 0;
        Array.Clear(s.msg, 0, s.msg.Length);
        s.msg_len = 0;
        s.preamble_len = 0;
        s.preamble_ones_len = 0;
        s.postamble_ones_len = 0;
        s.stop_bits = 0;
        s.baudot_shift = 0;
        s.logging = new logging_state_t();

        s.alert_tone_desc = tone_gen_descriptor_init(
            s.alert_tone_desc,
            2130,
            -13,
            2750,
            -13,
            110,
            60,
            0,
            0,
            0)!;
        s.standard = standard;
        adsi_tx_set_preamble(s, -1, -1, -1, -1);
        s.logging = span_log_init(s.logging, SPAN_LOG_NONE, null);
        start_tx(s);
        return s;
    }

    public static int adsi_tx_release(adsi_tx_state_t s)
    {
        return 0;
    }

    public static int adsi_tx_free(adsi_tx_state_t? s)
    {
        return 0;
    }

    private static ushort adsi_encode_baudot(adsi_tx_state_t s, byte ch)
    {
        ushort shift;

        ch = adsi_encode_baudot_conv[ch];
        if (ch == 0xFF)
            return 0;
        if ((ch & 0x40) != 0)
            return (ushort)(ch & 0x1F);
        if ((ch & 0x80) != 0)
        {
            if (s.baudot_shift == 1)
                return (ushort)(ch & 0x1F);
            s.baudot_shift = 1;
            shift = BAUDOT_FIGURE_SHIFT;
        }
        else
        {
            if (s.baudot_shift == 0)
                return (ushort)(ch & 0x1F);
            s.baudot_shift = 0;
            shift = BAUDOT_LETTER_SHIFT;
        }
        return (ushort)((shift << 5) | (ch & 0x1F));
    }

    private static byte adsi_decode_baudot(adsi_rx_state_t s, byte ch)
    {
        switch (ch)
        {
            case BAUDOT_FIGURE_SHIFT:
                s.baudot_shift = 1;
                break;
            case BAUDOT_LETTER_SHIFT:
                s.baudot_shift = 0;
                break;
            default:
                return adsi_decode_baudot_conv[s.baudot_shift][ch];
        }
        return 0;
    }

    public static int adsi_next_field(
        adsi_rx_state_t s,
        byte[] msg,
        int msg_len,
        int pos,
        out byte field_type,
        out ReadOnlyMemory<byte> field_body,
        out int field_len)
    {
        int i;
        int field_body_pos;

        field_type = 0;
        field_body = default;
        field_len = 0;
        switch (s.standard)
        {
            case ADSI_STANDARD_CLASS:
            case ADSI_STANDARD_CLIP:
            case ADSI_STANDARD_ACLIP:
                if (pos >= msg_len)
                    return -1;
                if (pos <= 0)
                {
                    field_type = msg[0];
                    field_len = 0;
                    field_body = default;
                    pos = 2;
                }
                else
                {
                    if ((msg[0] & 0x80) != 0)
                    {
                        field_type = msg[pos++];
                        field_len = msg[pos++];
                        field_body_pos = pos;
                    }
                    else
                    {
                        field_type = 0;
                        field_len = msg_len - pos;
                        field_body_pos = pos;
                    }
                    pos += field_len;
                    if (pos > msg_len)
                        return -2;
                    field_body = new ReadOnlyMemory<byte>(msg, field_body_pos, field_len);
                }
                if (pos > msg_len)
                    return -2;
                break;
            case ADSI_STANDARD_JCLIP:
                if (pos >= msg_len - 2)
                    return -1;
                if (pos <= 0)
                {
                    pos = 5;
                    field_type = msg[pos++];
                    if (field_type == DLE)
                        pos++;
                    if (msg[pos++] == DLE)
                        pos++;
                    field_len = 0;
                    field_body = default;
                }
                else
                {
                    field_type = msg[pos++];
                    if (field_type == DLE)
                        pos++;
                    field_len = msg[pos++];
                    if (field_len == DLE)
                        pos++;
                    field_body_pos = pos;
                    pos += field_len;
                    if (pos > msg_len - 2)
                        return -2;
                    field_body = new ReadOnlyMemory<byte>(msg, field_body_pos, field_len);
                }
                if (pos > msg_len - 2)
                    return -2;
                break;
            case ADSI_STANDARD_CLIP_DTMF:
                if (pos > msg_len)
                    return -1;
                if (pos <= 0)
                {
                    pos = 1;
                    field_type = msg[msg_len - 1];
                    field_len = 0;
                    field_body = default;
                }
                else
                {
                    pos--;
                    if (msg[pos] >= '0' && msg[pos] <= '9')
                        field_type = CLIP_DTMF_HASH_UNSPECIFIED;
                    else
                        field_type = msg[pos++];
                    field_body_pos = pos;
                    i = pos;
                    while (i < msg_len && msg[i] >= '0' && msg[i] <= '9')
                        i++;
                    field_len = i - pos;
                    field_body = new ReadOnlyMemory<byte>(msg, field_body_pos, field_len);
                    pos = i;
                    if (msg[pos] == '#' || msg[pos] == 'C')
                        pos++;
                    if (pos > msg_len)
                        return -2;
                    pos++;
                }
                break;
            case ADSI_STANDARD_TDD:
                if (pos >= msg_len)
                    return -1;
                field_type = 0;
                field_body = new ReadOnlyMemory<byte>(msg, 0, msg_len);
                field_len = msg_len;
                pos = msg_len;
                break;
        }
        return pos;
    }

    public static int adsi_add_field(
        adsi_tx_state_t s,
        byte[] msg,
        int len,
        byte field_type,
        ReadOnlySpan<byte> field_body,
        int field_len)
    {
        int i;
        int x;

        switch (s.standard)
        {
            case ADSI_STANDARD_CLASS:
            case ADSI_STANDARD_CLIP:
            case ADSI_STANDARD_ACLIP:
                if (len <= 0)
                {
                    msg[0] = field_type;
                    msg[1] = 0;
                    len = 2;
                }
                else
                {
                    if (field_type != 0)
                    {
                        msg[len++] = field_type;
                        msg[len++] = (byte)field_len;
                        if (field_len == DLE)
                            msg[len++] = (byte)field_len;
                        field_body[..field_len].CopyTo(msg.AsSpan(len));
                        len += field_len;
                    }
                    else
                    {
                        field_body[..field_len].CopyTo(msg.AsSpan(len));
                        len += field_len;
                    }
                }
                break;
            case ADSI_STANDARD_JCLIP:
                if (len <= 0)
                {
                    msg[0] = field_type;
                    msg[1] = 0;
                    len = 2;
                }
                else
                {
                    msg[len++] = field_type;
                    if (field_type == DLE)
                        msg[len++] = field_type;
                    msg[len++] = (byte)field_len;
                    if (field_len == DLE)
                        msg[len++] = (byte)field_len;
                    for (i = 0; i < field_len; i++)
                    {
                        msg[len++] = field_body[i];
                        if (field_body[i] == DLE)
                            msg[len++] = field_body[i];
                    }
                }
                break;
            case ADSI_STANDARD_CLIP_DTMF:
                if (len <= 0)
                {
                    msg[0] = field_type;
                    len = 1;
                }
                else
                {
                    x = msg[--len];
                    if (field_type != CLIP_DTMF_HASH_UNSPECIFIED)
                        msg[len++] = field_type;
                    field_body[..field_len].CopyTo(msg.AsSpan(len));
                    msg[len + field_len] = (byte)x;
                    len += field_len + 1;
                }
                break;
            case ADSI_STANDARD_TDD:
                if (len < 0)
                    len = 0;
                for (i = 0; i < field_len; i++)
                {
                    if ((x = adsi_encode_baudot(s, field_body[i])) != 0)
                    {
                        if ((x & 0x3E0) != 0)
                            msg[len++] = (byte)((x >> 5) & 0x1F);
                        msg[len++] = (byte)(x & 0x1F);
                    }
                }
                break;
        }
        return len;
    }

    public static string adsi_standard_to_str(int standard)
    {
        switch (standard)
        {
            case ADSI_STANDARD_CLASS:
                return "CLASS";
            case ADSI_STANDARD_CLIP:
                return "CLIP";
            case ADSI_STANDARD_ACLIP:
                return "A-CLIP";
            case ADSI_STANDARD_JCLIP:
                return "J-CLIP";
            case ADSI_STANDARD_CLIP_DTMF:
                return "CLIP-DTMF";
            case ADSI_STANDARD_TDD:
                return "TDD";
        }
        return "???";
    }
}
