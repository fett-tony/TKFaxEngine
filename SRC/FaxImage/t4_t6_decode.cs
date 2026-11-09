/* Direct C# port of EngineFX t4_t6_decode.c and t4_t6_decode.h. */

namespace TKFaxEngine.FaxImage;

public static class t4_t6_decode {
    private const int EOLS_TO_END_ANY_RX_PAGE = 6;
    private const int EOLS_TO_END_T4_RX_PAGE = 5;
    private const int EOLS_TO_END_T6_RX_PAGE = 2;

    private const int T4_COMPRESSION_T4_1D = 0x02;
    private const int T4_COMPRESSION_T4_2D = 0x04;
    private const int T4_COMPRESSION_T6 = 0x08;
    private const int T4_DECODE_MORE_DATA = 0;
    private const int T4_DECODE_OK = -1;

    private static readonly int[] msbmask =
    {
        0x00, 0x01, 0x03, 0x07, 0x0F, 0x1F, 0x3F, 0x7F, 0xFF
    };

    public static t4_t6_decode_state_t? t4_t6_decode_init(
        t4_t6_decode_state_t? s,
        int encoding,
        int image_width,
        t4_row_write_handler_t? handler,
        object? user_data) {
        s ??= new t4_t6_decode_state_t();
        s.row_write_handler = null;
        s.row_write_user_data = null;
        s.encoding = 0;
        s.image_width = 0;
        s.image_length = 0;
        s.bytes_per_row = 0;
        s.row_bits = 0;
        s.row_buf = Array.Empty<byte>();
        s.row_is_2d = false;
        s.row_len = 0;
        s.cur_runs = Array.Empty<uint>();
        s.ref_runs = Array.Empty<uint>();
        s.consecutive_eols = 0;
        s.a0 = 0;
        s.b1 = 0;
        s.run_length = 0;
        s.black_white = 0;
        s.in_black = false;
        s.a_cursor = 0;
        s.b_cursor = 0;
        s.rx_bitstream = 0;
        s.rx_bits = 0;
        s.rx_skip_bits = 0;
        s.pixel_stream = 0;
        s.pixels = 0;
        s.min_row_bits = 0;
        s.max_row_bits = 0;
        s.compressed_image_size = 0;
        s.curr_bad_row_run = 0;
        s.longest_bad_row_run = 0;
        s.bad_rows = 0;
        s.logging.Initialize((int)SpanLogSeverity.None, null);
        s.logging.SetProtocol("T.4/T.6");

        s.encoding = encoding;
        s.row_write_handler = handler;
        s.row_write_user_data = user_data;
        t4_t6_decode_restart(s, image_width);
        return s;
    }

    public static int t4_t6_decode_set_row_write_handler(
        t4_t6_decode_state_t s,
        t4_row_write_handler_t? handler,
        object? user_data) {
        s.row_write_handler = handler;
        s.row_write_user_data = user_data;
        return 0;
    }

    public static int t4_t6_decode_set_encoding(t4_t6_decode_state_t s, int encoding) {
        switch (encoding) {
            case T4_COMPRESSION_T4_1D:
            case T4_COMPRESSION_T4_2D:
            case T4_COMPRESSION_T6:
                s.encoding = encoding;
                return 0;
        }
        return -1;
    }

    public static int t4_t6_decode_restart(t4_t6_decode_state_t s, int image_width) {
        int run_space = image_width + 4;
        if (s.bytes_per_row == 0 || image_width != s.image_width) {
            if (s.cur_runs.Length != run_space)
                Array.Resize(ref s.cur_runs, run_space);
            if (s.ref_runs.Length != run_space)
                Array.Resize(ref s.ref_runs, run_space);
            s.image_width = image_width;
        }

        int bytes_per_row = (image_width + 7) / 8;
        if (bytes_per_row != s.bytes_per_row) {
            if (s.row_buf.Length != bytes_per_row)
                Array.Resize(ref s.row_buf, bytes_per_row);
            s.bytes_per_row = bytes_per_row;
        }

        s.rx_bits = 0;
        s.rx_skip_bits = 0;
        s.rx_bitstream = 0;
        s.row_bits = 0;
        s.min_row_bits = int.MaxValue;
        s.max_row_bits = 0;
        s.compressed_image_size = 0;
        s.bad_rows = 0;
        s.longest_bad_row_run = 0;
        s.curr_bad_row_run = 0;
        s.image_length = 0;
        s.pixel_stream = 0;
        s.pixels = 8;
        s.row_len = 0;
        s.in_black = false;
        s.black_white = 0;
        s.b_cursor = 1;
        s.a_cursor = 0;
        s.b1 = s.image_width;
        s.a0 = 0;
        s.run_length = 0;
        s.row_is_2d = s.encoding == T4_COMPRESSION_T6;
        s.consecutive_eols = s.encoding == T4_COMPRESSION_T6 ? 0 : -1;

        Array.Clear(s.cur_runs);
        Array.Clear(s.ref_runs);
        if (s.ref_runs.Length != 0)
            s.ref_runs[0] = unchecked((uint)s.image_width);
        Array.Clear(s.row_buf);
        return 0;
    }

    private static void t4_t6_decode_rx_status(t4_t6_decode_state_t s, int status) {
        s.logging.Log((int)SpanLogSeverity.Flow, "Signal status is %d\n", status);
        switch ((SignalStatus)status) {
            case SignalStatus.TrainingInProgress:
            case SignalStatus.TrainingFailed:
            case SignalStatus.TrainingSucceeded:
            case SignalStatus.CarrierUp:
                break;
            case SignalStatus.CarrierDown:
            case SignalStatus.EndOfData:
                t4_t6_decode_put(s, null, 0);
                break;
            default:
                s.logging.Log((int)SpanLogSeverity.Warning, "Unexpected rx status - %d!\n", status);
                break;
        }
    }

    public static int t4_t6_decode_put_bit(t4_t6_decode_state_t s, int bit) {
        if (bit < 0) {
            t4_t6_decode_rx_status(s, bit);
            return 1;
        }
        s.compressed_image_size++;
        if (put_bits(s, unchecked((uint)(bit & 1)), 1))
            return T4_DECODE_OK;
        return T4_DECODE_MORE_DATA;
    }

    public static int t4_t6_decode_put(t4_t6_decode_state_t s, byte[]? buf, int len) {
        if (len == 0) {
            if (s.consecutive_eols != EOLS_TO_END_ANY_RX_PAGE) {
                put_bits(s, 0, 8);
                put_bits(s, 0, 5);
            }
            if (s.curr_bad_row_run != 0) {
                if (s.curr_bad_row_run > s.longest_bad_row_run)
                    s.longest_bad_row_run = s.curr_bad_row_run;
                s.curr_bad_row_run = 0;
            }
            if (s.row_write_handler is not null)
                s.row_write_handler(s.row_write_user_data, ReadOnlySpan<byte>.Empty, 0);
            s.rx_bits = 0;
            s.rx_skip_bits = 0;
            s.rx_bitstream = 0;
            s.consecutive_eols = EOLS_TO_END_ANY_RX_PAGE;
            return T4_DECODE_OK;
        }

        for (int i = 0; i < len; i++) {
            s.compressed_image_size += 8;
            byte value = buf![i];
            if (put_bits(s, value, 8))
                return T4_DECODE_OK;
        }
        return T4_DECODE_MORE_DATA;
    }

    public static uint t4_t6_decode_get_image_width(t4_t6_decode_state_t s) => unchecked((uint)s.image_width);
    public static uint t4_t6_decode_get_image_length(t4_t6_decode_state_t s) => unchecked((uint)s.image_length);
    public static int t4_t6_decode_get_compressed_image_size(t4_t6_decode_state_t s) => s.compressed_image_size;
    public static SpanLogState t4_t6_decode_get_logging_state(t4_t6_decode_state_t s) => s.logging;

    private static int free_buffers(t4_t6_decode_state_t s) {
        s.cur_runs = Array.Empty<uint>();
        s.ref_runs = Array.Empty<uint>();
        s.row_buf = Array.Empty<byte>();
        s.bytes_per_row = 0;
        return 0;
    }

    public static int t4_t6_decode_release(t4_t6_decode_state_t s) {
        free_buffers(s);
        return 0;
    }

    public static int t4_t6_decode_free(t4_t6_decode_state_t s) {
        return t4_t6_decode_release(s);
    }

    private static void add_run_to_row(t4_t6_decode_state_t s) {
        if (s.run_length >= 0) {
            s.row_len += s.run_length;
            if (s.row_len <= s.image_width)
                s.cur_runs[s.a_cursor++] = unchecked((uint)s.run_length);
        }
        s.run_length = 0;
    }

    private static void update_row_bit_info(t4_t6_decode_state_t s) {
        if (s.row_bits > s.max_row_bits)
            s.max_row_bits = s.row_bits;
        if (s.row_bits < s.min_row_bits)
            s.min_row_bits = s.row_bits;
        s.row_bits = 0;
    }

    private static int put_decoded_row(t4_t6_decode_state_t s) {
        if (s.run_length != 0)
            add_run_to_row(s);
        update_row_bit_info(s);

        int row_pos = 0;
        if (s.row_len == s.image_width) {
            if (s.curr_bad_row_run != 0) {
                if (s.curr_bad_row_run > s.longest_bad_row_run)
                    s.longest_bad_row_run = s.curr_bad_row_run;
                s.curr_bad_row_run = 0;
            }

            // A decoded TIFF scanline is byte-aligned even when image_width is
            // not divisible by 8. Start a fresh output byte for every row and
            // flush the final fractional byte with white padding. Without this
            // row boundary, a 1275-pixel row carries five unused bits into the
            // next row and shifts every following scanline.
            s.pixel_stream = 0;
            s.pixels = 8;
            Array.Clear(s.row_buf);

            for (int x = 0, fudge = 0; x < s.a_cursor; x++, fudge ^= 0xFF) {
                uint i = s.cur_runs[x];
                if ((int)i >= s.pixels) {
                    s.pixel_stream = (s.pixel_stream << s.pixels) | unchecked((uint)(msbmask[s.pixels] & fudge));
                    for (i += unchecked((uint)(8 - s.pixels)); i >= 8; i -= 8) {
                        s.pixels = 8;
                        s.row_buf[row_pos++] = unchecked((byte)s.pixel_stream);
                        s.pixel_stream = unchecked((uint)fudge);
                    }
                }
                s.pixel_stream = (s.pixel_stream << unchecked((int)i)) | unchecked((uint)(msbmask[unchecked((int)i)] & fudge));
                s.pixels -= unchecked((int)i);
            }

            if (s.pixels != 8) {
                s.row_buf[row_pos++] = unchecked((byte)(s.pixel_stream << s.pixels));
                s.pixel_stream = 0;
                s.pixels = 8;
            }

            if (row_pos != s.bytes_per_row)
                return -1;

            s.image_length++;
        } else {
            int j;
            int fudge;
            for (j = 0, fudge = 0; j < s.a_cursor && fudge < s.image_width; j++)
                fudge += unchecked((int)s.cur_runs[j]);
            if (fudge < s.image_width) {
                if ((s.a_cursor & 1) != 0) {
                    s.cur_runs[s.a_cursor++] = 1;
                    fudge++;
                    if (fudge < s.image_width)
                        s.cur_runs[s.a_cursor++] = unchecked((uint)(s.image_width - fudge));
                } else {
                    s.cur_runs[s.a_cursor++] = unchecked((uint)(s.image_width - fudge));
                }
            } else {
                s.cur_runs[s.a_cursor] = unchecked((uint)(unchecked((int)s.cur_runs[s.a_cursor]) + s.image_width - fudge));
            }
            s.image_length++;
            s.bad_rows++;
            s.curr_bad_row_run++;
        }

        s.cur_runs[s.a_cursor] = 0;
        s.cur_runs[s.a_cursor + 1] = 0;
        uint[] p = s.cur_runs;
        s.cur_runs = s.ref_runs;
        s.ref_runs = p;
        s.b_cursor = 1;
        s.a_cursor = 0;
        s.b1 = unchecked((int)s.ref_runs[0]);
        s.a0 = 0;
        s.run_length = 0;
        if (s.row_write_handler is not null)
            return s.row_write_handler(s.row_write_user_data, s.row_buf.AsSpan(0, s.bytes_per_row), s.bytes_per_row);
        return 0;
    }

    private static void drop_rx_bits(t4_t6_decode_state_t s, int bits) {
        s.row_bits += bits;
        s.rx_skip_bits += bits - 1;
        s.rx_bits--;
        s.rx_bitstream >>= 1;
    }

    private static void force_drop_rx_bits(t4_t6_decode_state_t s, int bits) {
        s.row_bits += bits;
        s.rx_skip_bits = 0;
        s.rx_bits -= bits;
        s.rx_bitstream >>= bits;
    }

    private static bool put_bits(t4_t6_decode_state_t s, uint bitString, int quantity) {
        s.rx_bitstream |= bitString << s.rx_bits;
        s.rx_bits += quantity;
        if (s.rx_bits < 13)
            return false;

        if (s.consecutive_eols != 0) {
            if (s.consecutive_eols >= EOLS_TO_END_ANY_RX_PAGE)
                return true;

            if (s.consecutive_eols < 0) {
                while ((s.rx_bitstream & 0x0FFFU) != 0x0800U) {
                    s.rx_bitstream >>= 1;
                    if (--s.rx_bits < 13)
                        return false;
                }

                s.consecutive_eols = 0;
                if (s.encoding == T4_COMPRESSION_T4_1D) {
                    s.row_is_2d = false;
                    force_drop_rx_bits(s, 12);
                } else {
                    s.row_is_2d = (s.rx_bitstream & 0x1000U) == 0;
                    force_drop_rx_bits(s, 13);
                }
            }
        }

        while (s.rx_bits >= 13) {
            if ((s.rx_bitstream & 0x0FFFU) == 0x0800U) {
                if (s.row_len == 0) {
                    s.consecutive_eols++;
                    if (s.encoding == T4_COMPRESSION_T6) {
                        if (s.consecutive_eols >= EOLS_TO_END_T6_RX_PAGE) {
                            s.consecutive_eols = EOLS_TO_END_ANY_RX_PAGE;
                            return true;
                        }
                    } else if (s.consecutive_eols >= EOLS_TO_END_T4_RX_PAGE) {
                        s.consecutive_eols = EOLS_TO_END_ANY_RX_PAGE;
                        return true;
                    }
                } else {
                    if (s.run_length > 0)
                        add_run_to_row(s);
                    s.consecutive_eols = 0;
                    if (put_decoded_row(s) != 0)
                        return true;
                }

                if (s.encoding == T4_COMPRESSION_T4_2D) {
                    s.row_is_2d = (s.rx_bitstream & 0x1000U) == 0;
                    force_drop_rx_bits(s, 13);
                } else {
                    force_drop_rx_bits(s, 12);
                }

                s.in_black = false;
                s.black_white = 0;
                s.run_length = 0;
                s.row_len = 0;
                continue;
            }

            if (s.rx_skip_bits != 0) {
                s.rx_skip_bits--;
                s.rx_bits--;
                s.rx_bitstream >>= 1;
                continue;
            }

            if (s.row_is_2d && s.black_white == 0) {
                int index = unchecked((int)(s.rx_bitstream & 0x7FU));
                t4_table_entry_t entry = t4_t6_decode_states.t4_2d_table[index];
                if (s.row_len >= s.image_width) {
                    drop_rx_bits(s, entry.width);
                    continue;
                }

                if (s.a_cursor != 0) {
                    while (s.b1 <= s.a0) {
                        s.b1 += unchecked((int)(s.ref_runs[s.b_cursor] + s.ref_runs[s.b_cursor + 1]));
                        s.b_cursor += 2;
                    }
                }

                switch (entry.state) {
                    case t4_decode_state_code_t.S_Horiz:
                        s.in_black = (s.a_cursor & 1) != 0;
                        s.black_white = 2;
                        break;

                    case t4_decode_state_code_t.S_Vert: {
                            int oldA0 = s.a0;
                            s.a0 = s.b1 + entry.param;
                            if (s.a0 <= oldA0 && (s.a0 < oldA0 || s.b_cursor > 1)) {
                                s.a0 = oldA0;
                                break;
                            }

                            s.run_length += s.a0 - oldA0;
                            add_run_to_row(s);
                            if (entry.param >= 0) {
                                s.b1 += unchecked((int)s.ref_runs[s.b_cursor++]);
                            } else {
                                if (s.b_cursor != 0)
                                    s.b1 -= unchecked((int)s.ref_runs[--s.b_cursor]);
                            }
                            break;
                        }

                    case t4_decode_state_code_t.S_Pass: {
                            s.b1 += unchecked((int)s.ref_runs[s.b_cursor++]);
                            int oldA0 = s.a0;
                            s.a0 = s.b1;
                            s.run_length += s.a0 - oldA0;
                            s.b1 += unchecked((int)s.ref_runs[s.b_cursor++]);
                            break;
                        }

                    case t4_decode_state_code_t.S_Ext:
                        break;
                    case t4_decode_state_code_t.S_Null:
                        break;
                    default:
                        s.logging.Log((int)SpanLogSeverity.Warning, "Unexpected T.4 state %d\n", entry.state);
                        break;
                }

                drop_rx_bits(s, entry.width);
            } else if (s.in_black) {
                int index = unchecked((int)(s.rx_bitstream & 0x1FFFU));
                t4_table_entry_t entry = t4_t6_decode_states.t4_1d_black_table[index];
                switch (entry.state) {
                    case t4_decode_state_code_t.S_MakeUpB:
                    case t4_decode_state_code_t.S_MakeUp:
                        s.run_length += entry.param;
                        s.a0 += entry.param;
                        break;

                    case t4_decode_state_code_t.S_TermB:
                        s.in_black = false;
                        if (s.row_len < s.image_width) {
                            s.run_length += entry.param;
                            s.a0 += entry.param;
                            add_run_to_row(s);
                        }
                        if (s.black_white != 0)
                            s.black_white--;
                        break;

                    default:
                        s.black_white = 0;
                        break;
                }
                drop_rx_bits(s, entry.width);
            } else {
                int index = unchecked((int)(s.rx_bitstream & 0x0FFFU));
                t4_table_entry_t entry = t4_t6_decode_states.t4_1d_white_table[index];
                switch (entry.state) {
                    case t4_decode_state_code_t.S_MakeUpW:
                    case t4_decode_state_code_t.S_MakeUp:
                        s.run_length += entry.param;
                        s.a0 += entry.param;
                        break;

                    case t4_decode_state_code_t.S_TermW:
                        s.in_black = true;
                        if (s.row_len < s.image_width) {
                            s.run_length += entry.param;
                            s.a0 += entry.param;
                            add_run_to_row(s);
                        }
                        if (s.black_white != 0)
                            s.black_white--;
                        break;

                    default:
                        s.black_white = 0;
                        break;
                }
                drop_rx_bits(s, entry.width);
            }

            if (s.a0 >= s.image_width)
                s.a0 = s.image_width - 1;

            if (s.encoding == T4_COMPRESSION_T6
                && s.black_white == 0
                && s.row_len >= s.image_width) {
                if (s.run_length > 0)
                    add_run_to_row(s);
                if (put_decoded_row(s) != 0)
                    return true;
                s.in_black = false;
                s.black_white = 0;
                s.run_length = 0;
                s.row_len = 0;
            }
        }

        return false;
    }
}
