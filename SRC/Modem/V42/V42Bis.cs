/*
 * TKFaxEngineFX - a series of DSP components for telephony
 *
 * v42bis.cs - direct C# conversion of v42bis.h and v42bis.c
 *
 * Written by Steve Underwood <steveu@coppice.org>
 *
 * Copyright (C) 2005, 2011 Steve Underwood
 *
 * This file preserves the GNU Lesser General Public License version 2.1
 * terms of the original source files.
 */

#nullable enable

using global::TKFaxEngine;
using global::TKFaxEngine.Modem;
using static global::TKFaxEngine.LoggingApi;

namespace TKFaxEngine.Modem.V42;

public struct v42bis_dict_node_t
{
    public byte node_octet;
    public ushort parent;
    public ushort child;
    public ushort next;
}

public sealed class v42bis_comp_state_t
{
    public int v42bis_parm_p0;
    public int compression_mode;
    public span_put_msg_func_t? handler;
    public object? user_data;
    public int max_output_len;

    public bool transparent;
    public ushort v42bis_parm_c1;
    public ushort v42bis_parm_c2;
    public ushort v42bis_parm_c3;
    public ushort update_at;
    public ushort last_matched;
    public ushort last_added;
    public int v42bis_parm_n2;
    public int v42bis_parm_n7;
    public v42bis_dict_node_t[] dict = new v42bis_dict_node_t[v42bis.V42BIS_MAX_CODEWORDS];

    public byte[] @string = new byte[v42bis.V42BIS_MAX_STRING_SIZE];
    public int string_length;
    public int flushed_length;

    public ushort compression_performance;

    public uint bit_buffer;
    public int bit_count;

    public byte[] output_buf = new byte[v42bis.V42BIS_MAX_OUTPUT_LENGTH];
    public int output_octet_count;

    public byte escape_code;
    public bool escaped;
}

public sealed class v42bis_state_t
{
    public v42bis_comp_state_t compress = new();
    public v42bis_comp_state_t decompress = new();
    public SpanLogState logging = new();
}

public static class v42bis
{
    public const int V42BIS_MIN_STRING_SIZE = 6;
    public const int V42BIS_MAX_STRING_SIZE = 250;
    public const int V42BIS_MIN_DICTIONARY_SIZE = 512;
    public const int V42BIS_MAX_BITS = 12;
    public const int V42BIS_MAX_CODEWORDS = 4096;
    public const int V42BIS_MAX_OUTPUT_LENGTH = 1024;

    public const int V42BIS_P0_NEITHER_DIRECTION = 0;
    public const int V42BIS_P0_INITIATOR_RESPONDER = 1;
    public const int V42BIS_P0_RESPONDER_INITIATOR = 2;
    public const int V42BIS_P0_BOTH_DIRECTIONS = 3;

    public const int V42BIS_COMPRESSION_MODE_DYNAMIC = 0;
    public const int V42BIS_COMPRESSION_MODE_ALWAYS = 1;
    public const int V42BIS_COMPRESSION_MODE_NEVER = 2;

    private const int V42BIS_N3 = 8;
    private const int V42BIS_N4 = 256;
    private const int V42BIS_N6 = 3;
    private const int V42BIS_N5 = V42BIS_N4 + V42BIS_N6;
    private const int V42BIS_ESC_STEP = 51;

    private const int COMPRESSIBILITY_MONITOR = 256 * V42BIS_N3;
    private const int COMPRESSIBILITY_MONITOR_HYSTERESIS = 11;

    private const int V42BIS_ETM = 0;
    private const int V42BIS_FLUSH = 1;
    private const int V42BIS_STEPUP = 2;

    private const int V42BIS_ECM = 0;
    private const int V42BIS_EID = 1;
    private const int V42BIS_RESET = 2;

    private static void push_octet(v42bis_comp_state_t s, int octet)
    {
        s.output_buf[s.output_octet_count++] = (byte)octet;
        if (s.output_octet_count >= s.max_output_len)
        {
            s.handler!(s.user_data, s.output_buf, s.output_octet_count);
            s.output_octet_count = 0;
        }
    }

    private static void push_octets(v42bis_comp_state_t s, ReadOnlySpan<byte> buf, int len)
    {
        int i;
        int chunk;

        i = 0;
        while ((s.output_octet_count + len - i) >= s.max_output_len)
        {
            chunk = s.max_output_len - s.output_octet_count;
            buf.Slice(i, chunk).CopyTo(s.output_buf.AsSpan(s.output_octet_count));
            s.handler!(s.user_data, s.output_buf, s.max_output_len);
            s.output_octet_count = 0;
            i += chunk;
        }
        chunk = len - i;
        if (chunk > 0)
        {
            buf.Slice(i, chunk).CopyTo(s.output_buf.AsSpan(s.output_octet_count));
            s.output_octet_count += chunk;
        }
    }

    private static void push_compressed_code(v42bis_comp_state_t s, int code)
    {
        s.bit_buffer |= (uint)(code << s.bit_count);
        s.bit_count += s.v42bis_parm_c2;
        while (s.bit_count >= 8)
        {
            push_octet(s, (int)(s.bit_buffer & 0xFF));
            s.bit_buffer >>= 8;
            s.bit_count -= 8;
        }
    }

    private static void push_octet_alignment(v42bis_comp_state_t s)
    {
        if ((s.bit_count & 7) != 0)
        {
            s.bit_count += 8 - (s.bit_count & 7);
            while (s.bit_count >= 8)
            {
                push_octet(s, (int)(s.bit_buffer & 0xFF));
                s.bit_buffer >>= 8;
                s.bit_count -= 8;
            }
        }
    }

    private static void flush_octets(v42bis_comp_state_t s)
    {
        if (s.output_octet_count > 0)
        {
            s.handler!(s.user_data, s.output_buf, s.output_octet_count);
            s.output_octet_count = 0;
        }
    }

    private static void dictionary_init(v42bis_comp_state_t s)
    {
        int i;

        Array.Clear(s.dict, 0, s.dict.Length);
        for (i = 0; i < V42BIS_N4; i++)
            s.dict[i + V42BIS_N6].node_octet = (byte)i;
        s.v42bis_parm_c1 = V42BIS_N5;
        s.v42bis_parm_c2 = V42BIS_N3 + 1;
        s.v42bis_parm_c3 = V42BIS_N4 << 1;
        s.last_matched = 0;
        s.update_at = 0;
        s.last_added = 0;
        s.bit_buffer = 0;
        s.bit_count = 0;
        s.flushed_length = 0;
        s.string_length = 0;
        s.escape_code = 0;
        s.transparent = true;
        s.escaped = false;
        s.compression_performance = COMPRESSIBILITY_MONITOR;
    }

    private static ushort match_octet(v42bis_comp_state_t s, ushort at, byte octet)
    {
        ushort e;

        if (at == 0)
            return (ushort)(octet + V42BIS_N6);
        e = s.dict[at].child;
        while (e != 0)
        {
            if (s.dict[e].node_octet == octet)
                return e;
            e = s.dict[e].next;
        }
        return 0;
    }

    private static ushort add_octet_to_dictionary(v42bis_comp_state_t s, ushort at, byte octet)
    {
        ushort newx;
        ushort next;
        ushort e;

        newx = s.v42bis_parm_c1;
        s.dict[newx].node_octet = octet;
        s.dict[newx].parent = at;
        s.dict[newx].child = 0;
        s.dict[newx].next = s.dict[at].child;
        s.dict[at].child = newx;
        next = newx;
        do
        {
            if (++next == s.v42bis_parm_n2)
                next = V42BIS_N5;
        }
        while (s.dict[next].child != 0);
        if (s.dict[next].parent != 0)
        {
            e = s.dict[next].parent;
            if (s.dict[e].child == next)
            {
                s.dict[e].child = s.dict[next].next;
            }
            else
            {
                e = s.dict[e].child;
                while (s.dict[e].next != next)
                    e = s.dict[e].next;
                s.dict[e].next = s.dict[next].next;
            }
        }
        s.v42bis_parm_c1 = next;
        return newx;
    }

    private static void send_string(v42bis_comp_state_t s)
    {
        push_octets(s, s.@string, s.string_length);
        s.string_length = 0;
        s.flushed_length = 0;
    }

    private static void expand_codeword_to_string(v42bis_comp_state_t s, ushort code)
    {
        int i;
        ushort p;

        for (i = 0, p = code; p != 0; i++)
            p = s.dict[p].parent;
        s.string_length += i;
        i = s.string_length - 1;
        for (p = code; p != 0;)
        {
            s.@string[i--] = s.dict[p].node_octet;
            p = s.dict[p].parent;
        }
    }

    private static void send_encoded_data(v42bis_comp_state_t s, ushort code)
    {
        int i;

        s.compression_performance = (ushort)(s.compression_performance
            + (s.v42bis_parm_c2
            - s.compression_performance * s.string_length * V42BIS_N3 / COMPRESSIBILITY_MONITOR));
        if (s.transparent)
        {
            for (i = 0; i < s.string_length; i++)
            {
                push_octet(s, s.@string[i]);
                if (s.@string[i] == s.escape_code)
                {
                    push_octet(s, V42BIS_EID);
                    s.escape_code += V42BIS_ESC_STEP;
                }
            }
        }
        else
        {
            for (i = 0; i < s.string_length; i++)
            {
                if (s.@string[i] == s.escape_code)
                    s.escape_code += V42BIS_ESC_STEP;
            }
            while (code >= s.v42bis_parm_c3)
            {
                push_compressed_code(s, V42BIS_STEPUP);
                s.v42bis_parm_c2++;
                s.v42bis_parm_c3 <<= 1;
            }
            push_compressed_code(s, code);
        }
        s.string_length = 0;
        s.flushed_length = 0;
    }

    private static void go_compressed(v42bis_state_t ss)
    {
        v42bis_comp_state_t s;

        s = ss.compress;
        if (!s.transparent)
            return;
        span_log(ss.logging, SPAN_LOG_FLOW, "Changing to compressed mode\n");
        if (s.last_matched != 0)
        {
            s.update_at = s.last_matched;
            send_encoded_data(s, s.last_matched);
            s.last_matched = 0;
        }
        push_octet(s, s.escape_code);
        push_octet(s, V42BIS_ECM);
        s.bit_buffer = 0;
        s.transparent = false;
    }

    private static void go_transparent(v42bis_state_t ss)
    {
        v42bis_comp_state_t s;

        s = ss.compress;
        if (s.transparent)
            return;
        span_log(ss.logging, SPAN_LOG_FLOW, "Changing to transparent mode\n");
        if (s.last_matched != 0)
        {
            s.update_at = s.last_matched;
            send_encoded_data(s, s.last_matched);
            s.last_matched = 0;
        }
        s.last_added = 0;
        push_compressed_code(s, V42BIS_ETM);
        push_octet_alignment(s);
        s.transparent = true;
    }

    private static void monitor_for_mode_change(v42bis_state_t ss)
    {
        v42bis_comp_state_t s;

        s = ss.compress;
        switch (s.compression_mode)
        {
            case V42BIS_COMPRESSION_MODE_DYNAMIC:
                if (s.transparent)
                {
                    if (s.compression_performance < COMPRESSIBILITY_MONITOR - COMPRESSIBILITY_MONITOR_HYSTERESIS)
                        go_compressed(ss);
                }
                else
                {
                    if (s.compression_performance > COMPRESSIBILITY_MONITOR)
                        go_transparent(ss);
                }
                break;
            case V42BIS_COMPRESSION_MODE_ALWAYS:
                if (s.transparent)
                    go_compressed(ss);
                break;
            case V42BIS_COMPRESSION_MODE_NEVER:
                if (!s.transparent)
                    go_transparent(ss);
                break;
        }
    }

    private static int v42bis_comp_init(
        v42bis_comp_state_t s,
        int p1,
        int p2,
        span_put_msg_func_t? handler,
        object? user_data,
        int max_output_len)
    {
        s.v42bis_parm_p0 = 0;
        s.compression_mode = 0;
        s.handler = null;
        s.user_data = null;
        s.max_output_len = 0;
        s.transparent = false;
        s.v42bis_parm_c1 = 0;
        s.v42bis_parm_c2 = 0;
        s.v42bis_parm_c3 = 0;
        s.update_at = 0;
        s.last_matched = 0;
        s.last_added = 0;
        s.v42bis_parm_n2 = 0;
        s.v42bis_parm_n7 = 0;
        Array.Clear(s.dict, 0, s.dict.Length);
        Array.Clear(s.@string, 0, s.@string.Length);
        s.string_length = 0;
        s.flushed_length = 0;
        s.compression_performance = 0;
        s.bit_buffer = 0;
        s.bit_count = 0;
        Array.Clear(s.output_buf, 0, s.output_buf.Length);
        s.output_octet_count = 0;
        s.escape_code = 0;
        s.escaped = false;

        s.v42bis_parm_n2 = p1;
        s.v42bis_parm_n7 = p2;
        s.handler = handler;
        s.user_data = user_data;
        s.max_output_len = max_output_len < V42BIS_MAX_OUTPUT_LENGTH ? max_output_len : V42BIS_MAX_OUTPUT_LENGTH;
        s.output_octet_count = 0;
        dictionary_init(s);
        return 0;
    }

    private static int comp_exit(v42bis_comp_state_t s)
    {
        s.v42bis_parm_n2 = 0;
        return 0;
    }

    public static int v42bis_compress(v42bis_state_t ss, ReadOnlySpan<byte> buf, int len)
    {
        v42bis_comp_state_t s;
        int i;
        ushort code;

        s = ss.compress;
        if (s.v42bis_parm_p0 == 0)
        {
            push_octets(s, buf, len);
            return 0;
        }
        for (i = 0; i < len;)
        {
            if (s.update_at != 0)
            {
                if (match_octet(s, s.update_at, buf[i]) == 0)
                    s.last_added = add_octet_to_dictionary(s, s.update_at, buf[i]);
                s.update_at = 0;
            }
            while (i < len)
            {
                code = match_octet(s, s.last_matched, buf[i]);
                if (code == 0)
                {
                    s.update_at = s.last_matched;
                    send_encoded_data(s, s.last_matched);
                    s.last_matched = 0;
                    break;
                }
                if (code == s.last_added)
                {
                    s.last_added = 0;
                    send_encoded_data(s, s.last_matched);
                    s.last_matched = 0;
                    break;
                }
                s.last_matched = code;
                s.@string[s.string_length++] = buf[i++];
                if (s.string_length + s.flushed_length == s.v42bis_parm_n7)
                {
                    send_encoded_data(s, s.last_matched);
                    s.last_matched = 0;
                    break;
                }
            }
            monitor_for_mode_change(ss);
        }
        return 0;
    }

    public static int v42bis_compress_flush(v42bis_state_t ss)
    {
        v42bis_comp_state_t s;
        int len;

        s = ss.compress;
        if (s.update_at != 0)
            return 0;
        if (s.last_matched != 0)
        {
            len = s.string_length;
            send_encoded_data(s, s.last_matched);
            s.flushed_length += len;
        }
        if (!s.transparent)
        {
            s.update_at = s.last_matched;
            s.last_matched = 0;
            s.flushed_length = 0;
            push_compressed_code(s, V42BIS_FLUSH);
            push_octet_alignment(s);
        }
        flush_octets(s);
        return 0;
    }

    public static int v42bis_decompress(object? user_data, byte[]? buf, int len)
    {
        v42bis_state_t ss;
        v42bis_comp_state_t s;
        int i;
        int j;
        int yyy;
        ushort code;
        ushort p;
        byte ch;
        byte @in;

        ss = (v42bis_state_t)user_data!;
        byte[] input = buf!;
        s = ss.decompress;
        if (s.v42bis_parm_p0 == 0)
        {
            push_octets(s, input, len);
            return 0;
        }
        for (i = 0; i < len;)
        {
            if (s.transparent)
            {
                @in = input[i];
                if (s.escaped)
                {
                    s.escaped = false;
                    switch (@in)
                    {
                        case V42BIS_ECM:
                            span_log(ss.logging, SPAN_LOG_FLOW, "Hit V42BIS_ECM\n");
                            send_string(s);
                            s.transparent = false;
                            s.update_at = s.last_matched;
                            s.last_matched = 0;
                            i++;
                            continue;
                        case V42BIS_EID:
                            span_log(ss.logging, SPAN_LOG_FLOW, "Hit V42BIS_EID\n");
                            @in = s.escape_code;
                            s.escape_code += V42BIS_ESC_STEP;
                            break;
                        case V42BIS_RESET:
                            span_log(ss.logging, SPAN_LOG_FLOW, "Hit V42BIS_RESET\n");
                            send_string(s);
                            dictionary_init(s);
                            i++;
                            continue;
                        default:
                            span_log(ss.logging, SPAN_LOG_FLOW, "Hit V42BIS_???? - %u\n", @in);
                            return -1;
                    }
                }
                else if (@in == s.escape_code)
                {
                    s.escaped = true;
                    i++;
                    continue;
                }

                yyy = 1;
                for (j = 0; j < 2 && yyy != 0; j++)
                {
                    if (s.update_at != 0)
                    {
                        if (match_octet(s, s.update_at, @in) == 0)
                            s.last_added = add_octet_to_dictionary(s, s.update_at, @in);
                        s.update_at = 0;
                    }

                    code = match_octet(s, s.last_matched, @in);
                    if (code == 0)
                    {
                        s.update_at = s.last_matched;
                        send_string(s);
                        s.last_matched = 0;
                    }
                    else if (code == s.last_added)
                    {
                        s.last_added = 0;
                        send_string(s);
                        s.last_matched = 0;
                    }
                    else
                    {
                        s.last_matched = code;
                        s.@string[s.string_length++] = @in;
                        if (s.string_length + s.flushed_length == s.v42bis_parm_n7)
                        {
                            send_string(s);
                            s.last_matched = 0;
                        }
                        i++;
                        yyy = 0;
                    }
                }
            }
            else
            {
                while (s.bit_count < s.v42bis_parm_c2 && i < len)
                {
                    s.bit_buffer |= (uint)(input[i++] << s.bit_count);
                    s.bit_count += 8;
                }
                if (s.bit_count < s.v42bis_parm_c2)
                    continue;
                code = (ushort)(s.bit_buffer & ((1u << s.v42bis_parm_c2) - 1u));
                s.bit_buffer >>= s.v42bis_parm_c2;
                s.bit_count -= s.v42bis_parm_c2;

                if (code < V42BIS_N6)
                {
                    switch (code)
                    {
                        case V42BIS_ETM:
                            span_log(ss.logging, SPAN_LOG_FLOW, "Hit V42BIS_ETM\n");
                            s.bit_count = 0;
                            s.transparent = true;
                            s.last_matched = 0;
                            s.last_added = 0;
                            break;
                        case V42BIS_FLUSH:
                            span_log(ss.logging, SPAN_LOG_FLOW, "Hit V42BIS_FLUSH\n");
                            s.bit_count = 0;
                            break;
                        case V42BIS_STEPUP:
                            span_log(ss.logging, SPAN_LOG_FLOW, "Hit V42BIS_STEPUP\n");
                            s.v42bis_parm_c2++;
                            s.v42bis_parm_c3 <<= 1;
                            if (s.v42bis_parm_c2 > (s.v42bis_parm_n2 >> 3))
                                return -1;
                            break;
                    }
                    continue;
                }
                if (code == s.v42bis_parm_c1)
                    return -1;
                expand_codeword_to_string(s, code);
                if (s.update_at != 0)
                {
                    ch = s.@string[0];
                    p = match_octet(s, s.update_at, ch);
                    if (p == 0)
                    {
                        s.last_added = add_octet_to_dictionary(s, s.update_at, ch);
                        if (code == s.v42bis_parm_c1)
                            return -1;
                    }
                    else if (p == s.last_added)
                    {
                        s.last_added = 0;
                    }
                }
                s.update_at = (ushort)((s.string_length + s.flushed_length) == s.v42bis_parm_n7 ? 0 : code);
                for (j = 0; j < s.string_length; j++)
                {
                    if (s.@string[j] == s.escape_code)
                        s.escape_code += V42BIS_ESC_STEP;
                }
                send_string(s);
            }
        }
        return 0;
    }

    public static int v42bis_decompress_flush(v42bis_state_t ss)
    {
        v42bis_comp_state_t s;
        int len;

        s = ss.decompress;
        len = s.string_length;
        send_string(s);
        s.flushed_length += len;
        flush_octets(s);
        return 0;
    }

    public static void v42bis_compression_control(v42bis_state_t s, int mode)
    {
        s.compress.compression_mode = mode;
    }

    public static SpanLogState v42bis_get_logging_state(v42bis_state_t s)
    {
        return s.logging;
    }

    public static v42bis_state_t? v42bis_init(
        v42bis_state_t? s,
        int negotiated_p0,
        int negotiated_p1,
        int negotiated_p2,
        span_put_msg_func_t? encode_handler,
        object? encode_user_data,
        int max_encode_len,
        span_put_msg_func_t? decode_handler,
        object? decode_user_data,
        int max_decode_len)
    {
        int ret;

        if (negotiated_p1 < V42BIS_MIN_DICTIONARY_SIZE || negotiated_p1 > 65535)
            return null;
        if (negotiated_p2 < V42BIS_MIN_STRING_SIZE || negotiated_p2 > V42BIS_MAX_STRING_SIZE)
            return null;
        if (s is null)
            s = new v42bis_state_t();

        s.compress = new v42bis_comp_state_t();
        s.decompress = new v42bis_comp_state_t();
        s.logging = span_log_init(s.logging, SPAN_LOG_NONE, null);
        span_log_set_protocol(s.logging, "V.42bis");

        ret = v42bis_comp_init(s.compress, negotiated_p1, negotiated_p2, encode_handler, encode_user_data, max_encode_len);
        if (ret != 0)
            return null;
        ret = v42bis_comp_init(s.decompress, negotiated_p1, negotiated_p2, decode_handler, decode_user_data, max_decode_len);
        if (ret != 0)
        {
            comp_exit(s.compress);
            return null;
        }
        s.compress.v42bis_parm_p0 = negotiated_p0 & 2;
        s.decompress.v42bis_parm_p0 = negotiated_p0 & 1;

        return s;
    }

    public static int v42bis_release(v42bis_state_t s)
    {
        return 0;
    }

    public static int v42bis_free(v42bis_state_t s)
    {
        comp_exit(s.compress);
        comp_exit(s.decompress);
        return 0;
    }
}
