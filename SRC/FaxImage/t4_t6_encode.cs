// Managed C# port of spanDSP t4_t6_encode.c and t4_t6_encode.h.
// ITU-T T.4 1D/2D and T.6 fax image encoder.

namespace TKFaxEngine.FaxImage;

internal readonly struct t4_run_table_entry_t {
    internal t4_run_table_entry_t(int length, int code, int run_length) {
        this.length = checked((ushort)length);
        this.code = checked((ushort)code);
        this.run_length = checked((short)run_length);
    }

    internal readonly ushort length;
    internal readonly ushort code;
    internal readonly short run_length;
}

public sealed class t4_t6_encode_state_t {
    public t4_row_read_handler_t? row_read_handler;
    public object? row_read_user_data;
    public int encoding;
    public int image_width;
    public int min_bits_per_row;
    public int max_rows_to_next_1d_row;
    public int image_length;
    public int bytes_per_row;
    public int rows_to_next_1d_row;
    public int row_bits;
    public bool row_is_2d;
    public uint tx_bitstream;
    public int tx_bits;
    public byte[] bitstream = Array.Empty<byte>();
    public int bitstream_iptr;
    public int bitstream_optr;
    public int bit_pos;
    public uint[] cur_runs = Array.Empty<uint>();
    public uint[] ref_runs = Array.Empty<uint>();
    public int ref_steps;
    public int min_row_bits;
    public int max_row_bits;
    public int compressed_image_size;
    public SpanLogState logging = new();
}

public static class t4_t6_encode {
    private const int EolsToEndT4Page = 6;
    private const int EolsToEndT6Page = 2;

    private static readonly t4_run_table_entry_t[] TwoDimensionalCodes =
    {
        new(7, 0x60, 0),
        new(6, 0x30, 0),
        new(3, 0x06, 0),
        new(1, 0x01, 0),
        new(3, 0x02, 0),
        new(6, 0x10, 0),
        new(7, 0x20, 0),
        new(3, 0x04, 0),
        new(4, 0x08, 0)
    };

    private static readonly t4_run_table_entry_t[] WhiteCodes =
    {
        new(8, 0x00AC, 0),
        new(6, 0x0038, 1),
        new(4, 0x000E, 2),
        new(4, 0x0001, 3),
        new(4, 0x000D, 4),
        new(4, 0x0003, 5),
        new(4, 0x0007, 6),
        new(4, 0x000F, 7),
        new(5, 0x0019, 8),
        new(5, 0x0005, 9),
        new(5, 0x001C, 10),
        new(5, 0x0002, 11),
        new(6, 0x0004, 12),
        new(6, 0x0030, 13),
        new(6, 0x000B, 14),
        new(6, 0x002B, 15),
        new(6, 0x0015, 16),
        new(6, 0x0035, 17),
        new(7, 0x0072, 18),
        new(7, 0x0018, 19),
        new(7, 0x0008, 20),
        new(7, 0x0074, 21),
        new(7, 0x0060, 22),
        new(7, 0x0010, 23),
        new(7, 0x000A, 24),
        new(7, 0x006A, 25),
        new(7, 0x0064, 26),
        new(7, 0x0012, 27),
        new(7, 0x000C, 28),
        new(8, 0x0040, 29),
        new(8, 0x00C0, 30),
        new(8, 0x0058, 31),
        new(8, 0x00D8, 32),
        new(8, 0x0048, 33),
        new(8, 0x00C8, 34),
        new(8, 0x0028, 35),
        new(8, 0x00A8, 36),
        new(8, 0x0068, 37),
        new(8, 0x00E8, 38),
        new(8, 0x0014, 39),
        new(8, 0x0094, 40),
        new(8, 0x0054, 41),
        new(8, 0x00D4, 42),
        new(8, 0x0034, 43),
        new(8, 0x00B4, 44),
        new(8, 0x0020, 45),
        new(8, 0x00A0, 46),
        new(8, 0x0050, 47),
        new(8, 0x00D0, 48),
        new(8, 0x004A, 49),
        new(8, 0x00CA, 50),
        new(8, 0x002A, 51),
        new(8, 0x00AA, 52),
        new(8, 0x0024, 53),
        new(8, 0x00A4, 54),
        new(8, 0x001A, 55),
        new(8, 0x009A, 56),
        new(8, 0x005A, 57),
        new(8, 0x00DA, 58),
        new(8, 0x0052, 59),
        new(8, 0x00D2, 60),
        new(8, 0x004C, 61),
        new(8, 0x00CC, 62),
        new(8, 0x002C, 63),
        new(5, 0x001B, 64),
        new(5, 0x0009, 128),
        new(6, 0x003A, 192),
        new(7, 0x0076, 256),
        new(8, 0x006C, 320),
        new(8, 0x00EC, 384),
        new(8, 0x0026, 448),
        new(8, 0x00A6, 512),
        new(8, 0x0016, 576),
        new(8, 0x00E6, 640),
        new(9, 0x0066, 704),
        new(9, 0x0166, 768),
        new(9, 0x0096, 832),
        new(9, 0x0196, 896),
        new(9, 0x0056, 960),
        new(9, 0x0156, 1024),
        new(9, 0x00D6, 1088),
        new(9, 0x01D6, 1152),
        new(9, 0x0036, 1216),
        new(9, 0x0136, 1280),
        new(9, 0x00B6, 1344),
        new(9, 0x01B6, 1408),
        new(9, 0x0032, 1472),
        new(9, 0x0132, 1536),
        new(9, 0x00B2, 1600),
        new(6, 0x0006, 1664),
        new(9, 0x01B2, 1728),
        new(11, 0x0080, 1792),
        new(11, 0x0180, 1856),
        new(11, 0x0580, 1920),
        new(12, 0x0480, 1984),
        new(12, 0x0C80, 2048),
        new(12, 0x0280, 2112),
        new(12, 0x0A80, 2176),
        new(12, 0x0680, 2240),
        new(12, 0x0E80, 2304),
        new(12, 0x0380, 2368),
        new(12, 0x0B80, 2432),
        new(12, 0x0780, 2496),
        new(12, 0x0F80, 2560),
    };

    private static readonly t4_run_table_entry_t[] BlackCodes =
    {
        new(10, 0x03B0, 0),
        new(3, 0x0002, 1),
        new(2, 0x0003, 2),
        new(2, 0x0001, 3),
        new(3, 0x0006, 4),
        new(4, 0x000C, 5),
        new(4, 0x0004, 6),
        new(5, 0x0018, 7),
        new(6, 0x0028, 8),
        new(6, 0x0008, 9),
        new(7, 0x0010, 10),
        new(7, 0x0050, 11),
        new(7, 0x0070, 12),
        new(8, 0x0020, 13),
        new(8, 0x00E0, 14),
        new(9, 0x0030, 15),
        new(10, 0x03A0, 16),
        new(10, 0x0060, 17),
        new(10, 0x0040, 18),
        new(11, 0x0730, 19),
        new(11, 0x00B0, 20),
        new(11, 0x01B0, 21),
        new(11, 0x0760, 22),
        new(11, 0x00A0, 23),
        new(11, 0x0740, 24),
        new(11, 0x00C0, 25),
        new(12, 0x0530, 26),
        new(12, 0x0D30, 27),
        new(12, 0x0330, 28),
        new(12, 0x0B30, 29),
        new(12, 0x0160, 30),
        new(12, 0x0960, 31),
        new(12, 0x0560, 32),
        new(12, 0x0D60, 33),
        new(12, 0x04B0, 34),
        new(12, 0x0CB0, 35),
        new(12, 0x02B0, 36),
        new(12, 0x0AB0, 37),
        new(12, 0x06B0, 38),
        new(12, 0x0EB0, 39),
        new(12, 0x0360, 40),
        new(12, 0x0B60, 41),
        new(12, 0x05B0, 42),
        new(12, 0x0DB0, 43),
        new(12, 0x02A0, 44),
        new(12, 0x0AA0, 45),
        new(12, 0x06A0, 46),
        new(12, 0x0EA0, 47),
        new(12, 0x0260, 48),
        new(12, 0x0A60, 49),
        new(12, 0x04A0, 50),
        new(12, 0x0CA0, 51),
        new(12, 0x0240, 52),
        new(12, 0x0EC0, 53),
        new(12, 0x01C0, 54),
        new(12, 0x0E40, 55),
        new(12, 0x0140, 56),
        new(12, 0x01A0, 57),
        new(12, 0x09A0, 58),
        new(12, 0x0D40, 59),
        new(12, 0x0340, 60),
        new(12, 0x05A0, 61),
        new(12, 0x0660, 62),
        new(12, 0x0E60, 63),
        new(10, 0x03C0, 64),
        new(12, 0x0130, 128),
        new(12, 0x0930, 192),
        new(12, 0x0DA0, 256),
        new(12, 0x0CC0, 320),
        new(12, 0x02C0, 384),
        new(12, 0x0AC0, 448),
        new(13, 0x06C0, 512),
        new(13, 0x16C0, 576),
        new(13, 0x0A40, 640),
        new(13, 0x1A40, 704),
        new(13, 0x0640, 768),
        new(13, 0x1640, 832),
        new(13, 0x09C0, 896),
        new(13, 0x19C0, 960),
        new(13, 0x05C0, 1024),
        new(13, 0x15C0, 1088),
        new(13, 0x0DC0, 1152),
        new(13, 0x1DC0, 1216),
        new(13, 0x0940, 1280),
        new(13, 0x1940, 1344),
        new(13, 0x0540, 1408),
        new(13, 0x1540, 1472),
        new(13, 0x0B40, 1536),
        new(13, 0x1B40, 1600),
        new(13, 0x04C0, 1664),
        new(13, 0x14C0, 1728),
        new(11, 0x0080, 1792),
        new(11, 0x0180, 1856),
        new(11, 0x0580, 1920),
        new(12, 0x0480, 1984),
        new(12, 0x0C80, 2048),
        new(12, 0x0280, 2112),
        new(12, 0x0A80, 2176),
        new(12, 0x0680, 2240),
        new(12, 0x0E80, 2304),
        new(12, 0x0380, 2368),
        new(12, 0x0B80, 2432),
        new(12, 0x0780, 2496),
        new(12, 0x0F80, 2560),
    };

    public static t4_t6_encode_state_t? t4_t6_encode_init(
        t4_t6_encode_state_t? s,
        int encoding,
        int image_width,
        int image_length,
        t4_row_read_handler_t? handler,
        object? user_data) {
        s ??= new t4_t6_encode_state_t();
        s.logging.Initialize((int)SpanLogSeverity.None, null);
        s.logging.SetProtocol("T.4/T.6");
        s.encoding = encoding;
        s.row_read_handler = handler;
        s.row_read_user_data = user_data;
        s.max_rows_to_next_1d_row = 2;
        return t4_t6_encode_restart(s, image_width, image_length) == 0 ? s : null;
    }

    public static int t4_t6_encode_set_row_read_handler(
        t4_t6_encode_state_t state,
        t4_row_read_handler_t? handler,
        object? userData = null) {
        ArgumentNullException.ThrowIfNull(state);
        state.row_read_handler = handler;
        state.row_read_user_data = userData;
        return 0;
    }

    public static int t4_t6_encode_set_encoding(t4_t6_encode_state_t state, int encoding) {
        ArgumentNullException.ThrowIfNull(state);
        int selected = encoding;
        switch (selected) {
            case t4_rx.T4_COMPRESSION_T6:
                state.min_bits_per_row = 0;
                goto case t4_rx.T4_COMPRESSION_T4_2D;
            case t4_rx.T4_COMPRESSION_T4_2D:
            case t4_rx.T4_COMPRESSION_T4_1D:
                state.encoding = selected;
                state.max_rows_to_next_1d_row = 2;
                state.rows_to_next_1d_row = state.max_rows_to_next_1d_row - 1;
                state.row_is_2d = selected == t4_rx.T4_COMPRESSION_T6;
                return 0;
            default:
                return -1;
        }
    }

    public static void t4_t6_encode_set_min_bits_per_row(t4_t6_encode_state_t s, int bits) {
        ArgumentNullException.ThrowIfNull(s);
        switch (s.encoding) {
            case t4_rx.T4_COMPRESSION_T6:
                s.min_bits_per_row = 0;
                break;
            case t4_rx.T4_COMPRESSION_T4_2D:
            case t4_rx.T4_COMPRESSION_T4_1D:
                s.min_bits_per_row = bits;
                break;
        }
    }

    public static int t4_t6_encode_set_image_width(t4_t6_encode_state_t state, int imageWidth) {
        ArgumentNullException.ThrowIfNull(state);
        if (imageWidth <= 0)
            return -1;

        if (state.bytes_per_row == 0 || imageWidth != state.image_width) {
            state.image_width = imageWidth;
            state.bytes_per_row = (imageWidth + 7) / 8;
            int runSpace = checked(imageWidth + 4);
            state.cur_runs = new uint[runSpace];
            state.ref_runs = new uint[runSpace];
            state.bitstream = new byte[checked((imageWidth + 1) * 2)];
        }
        return 0;
    }

    public static int t4_t6_encode_set_image_length(t4_t6_encode_state_t s, int image_length) {
        ArgumentNullException.ThrowIfNull(s);
        _ = image_length;
        return 0;
    }

    public static void t4_t6_encode_set_max_2d_rows_per_1d_row(t4_t6_encode_state_t s, int max) {
        ArgumentNullException.ThrowIfNull(s);
        if (max < 0) {
            int resolution = -max;
            max = resolution switch {
                (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_STANDARD or (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_100 => 2,
                (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_FINE or (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_200 => 4,
                (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_300 => 6,
                (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_SUPERFINE or (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_400 => 8,
                (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_600 => 12,
                (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_800 => 16,
                (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_1200 => 24,
                _ => 2
            };
        }
        s.max_rows_to_next_1d_row = max;
        s.rows_to_next_1d_row = max - 1;
        s.row_is_2d = false;
    }

    public static int t4_t6_encode_restart(
        t4_t6_encode_state_t s,
        int image_width,
        int image_length) {
        ArgumentNullException.ThrowIfNull(s);
        _ = image_length;
        if (t4_t6_encode_set_image_width(s, image_width) != 0)
            return -1;
        s.row_is_2d = s.encoding == t4_rx.T4_COMPRESSION_T6;
        s.rows_to_next_1d_row = s.max_rows_to_next_1d_row - 1;
        s.tx_bitstream = 0;
        s.bitstream_iptr = 0;
        s.bitstream_optr = 0;
        s.bit_pos = 7;
        s.tx_bits = 0;
        s.row_bits = 0;
        s.min_row_bits = int.MaxValue;
        s.max_row_bits = 0;
        s.image_length = 0;
        s.compressed_image_size = 0;
        s.ref_runs[0] = checked((uint)s.image_width);
        s.ref_runs[1] = checked((uint)s.image_width);
        s.ref_runs[2] = checked((uint)s.image_width);
        s.ref_runs[3] = checked((uint)s.image_width);
        s.ref_steps = 1;
        return 0;
    }

    public static int t4_t6_encode_image_complete(t4_t6_encode_state_t state) {
        ArgumentNullException.ThrowIfNull(state);
        if (state.bitstream_optr >= state.bitstream_iptr && get_next_row(state) < 0)
            return (int)SignalStatus.EndOfData;
        return 0;
    }

    public static int t4_t6_encode_get_bit(t4_t6_encode_state_t s) {
        ArgumentNullException.ThrowIfNull(s);
        if (s.bitstream_optr >= s.bitstream_iptr && get_next_row(s) < 0)
            return (int)SignalStatus.EndOfData;
        int bit = (s.bitstream[s.bitstream_optr] >> (7 - s.bit_pos)) & 1;
        if (--s.bit_pos < 0) {
            s.bitstream_optr++;
            s.bit_pos = 7;
        }
        return bit;
    }

    public static int t4_t6_encode_get(
        t4_t6_encode_state_t s,
        Span<byte> buf,
        int max_len) {
        ArgumentNullException.ThrowIfNull(s);
        if ((uint)max_len > (uint)buf.Length)
            throw new ArgumentOutOfRangeException(nameof(max_len));
        int len = 0;
        while (len < max_len) {
            if (s.bitstream_optr >= s.bitstream_iptr && get_next_row(s) < 0)
                return len;
            int n = s.bitstream_iptr - s.bitstream_optr;
            if (n > max_len - len)
                n = max_len - len;
            s.bitstream.AsSpan(s.bitstream_optr, n).CopyTo(buf.Slice(len, n));
            s.bitstream_optr += n;
            len += n;
        }
        return len;
    }
public static uint t4_t6_encode_get_image_width(t4_t6_encode_state_t state) =>
        checked((uint)(state ?? throw new ArgumentNullException(nameof(state))).image_width);

    public static uint t4_t6_encode_get_image_length(t4_t6_encode_state_t state) =>
        checked((uint)(state ?? throw new ArgumentNullException(nameof(state))).image_length);

    public static int t4_t6_encode_get_compressed_image_size(t4_t6_encode_state_t state) =>
        (state ?? throw new ArgumentNullException(nameof(state))).compressed_image_size;

    public static SpanLogState t4_t6_encode_get_logging_state(t4_t6_encode_state_t state) =>
        (state ?? throw new ArgumentNullException(nameof(state))).logging;

    private static int free_buffers(t4_t6_encode_state_t s) {
        s.cur_runs = Array.Empty<uint>();
        s.ref_runs = Array.Empty<uint>();
        s.bitstream = Array.Empty<byte>();
        s.bytes_per_row = 0;
        return 0;
    }

    public static int t4_t6_encode_release(t4_t6_encode_state_t s) {
        ArgumentNullException.ThrowIfNull(s);
        return free_buffers(s);
    }

    public static int t4_t6_encode_free(t4_t6_encode_state_t? state) =>
        state is null ? 0 : t4_t6_encode_release(state);

    private static void update_row_bit_info(t4_t6_encode_state_t s) {
        if (s.row_bits > s.max_row_bits)
            s.max_row_bits = s.row_bits;
        if (s.row_bits < s.min_row_bits)
            s.min_row_bits = s.row_bits;
        s.row_bits = 0;
    }

    private static int put_encoded_bits(t4_t6_encode_state_t s, uint bits, int length) {
        s.tx_bitstream |= bits << s.tx_bits;
        s.tx_bits += length;
        s.row_bits += length;
        while (s.tx_bits >= 8) {
            if (s.bitstream_iptr >= s.bitstream.Length)
                Array.Resize(ref s.bitstream, Math.Max(s.bitstream.Length * 2, s.bitstream_iptr + 1));
            s.bitstream[s.bitstream_iptr++] = unchecked((byte)s.tx_bitstream);
            s.tx_bitstream >>= 8;
            s.tx_bits -= 8;
        }
        return 0;
    }

    private static int put_1d_span(t4_t6_encode_state_t s, int span, t4_run_table_entry_t[] table) {
        t4_run_table_entry_t entry = table[63 + (2560 >> 6)];
        while (span >= 2624) {
            put_encoded_bits(s, entry.code, entry.length);
            span -= entry.run_length;
        }

        entry = table[63 + (span >> 6)];
        if (span >= 64) {
            put_encoded_bits(s, entry.code, entry.length);
            span -= entry.run_length;
        }

        put_encoded_bits(s, table[span].code, table[span].length);
        return 0;
    }

    private static int row_to_run_lengths(uint[] list, ReadOnlySpan<byte> row, int width) {
        bool black = false;
        int entry = 0;
        for (int position = 0; position < width; position++) {
            bool pixelBlack = pixel_is_black(row, position);
            if (pixelBlack != black) {
                list[entry++] = checked((uint)position);
                black = pixelBlack;
            }
        }
        list[entry++] = checked((uint)width);
        return entry;
    }

    private static bool pixel_is_black(ReadOnlySpan<byte> row, int bit)
        => ((row[bit >> 3] << (bit & 7)) & 0x80) != 0;

    private static void encode_eol(t4_t6_encode_state_t s) {
        uint code;
        int length;
        if (s.encoding == t4_rx.T4_COMPRESSION_T4_2D) {
            code = checked((uint)(0x0800 | (!s.row_is_2d ? 1 << 12 : 0)));
            length = 13;
        } else {
            code = 0x0800;
            length = 12;
        }

        if (s.row_bits != 0) {
            if (s.encoding != t4_rx.T4_COMPRESSION_T6
                && s.row_bits + length < s.min_bits_per_row) {
                put_encoded_bits(s, 0, s.min_bits_per_row - (s.row_bits + length));
            }
            put_encoded_bits(s, code, length);
            update_row_bit_info(s);
        } else {
            put_encoded_bits(s, code, length);
            s.row_bits = 0;
        }
    }

    private static void encode_2d_row(t4_t6_encode_state_t s, ReadOnlySpan<byte> row) {
        int currentSteps = row_to_run_lengths(s.cur_runs, row, s.image_width);
        s.cur_runs[currentSteps] = s.cur_runs[currentSteps - 1];
        s.cur_runs[currentSteps + 1] = s.cur_runs[currentSteps - 1];
        s.cur_runs[currentSteps + 2] = s.cur_runs[currentSteps - 1];

        int a0 = 0;
        int a1 = checked((int)s.cur_runs[0]);
        int b1 = checked((int)s.ref_runs[0]);
        int aCursor = 0;
        int bCursor = 0;

        for (; ; )
        {
            int b2 = checked((int)s.ref_runs[bCursor + 1]);
            if (b2 >= a1) {
                int difference = b1 - a1;
                if (Math.Abs(difference) <= 3) {
                    t4_run_table_entry_t code = TwoDimensionalCodes[difference + 3];
                    put_encoded_bits(s, code.code, code.length);
                    a0 = a1;
                    aCursor++;
                } else {
                    int a2 = checked((int)s.cur_runs[aCursor + 1]);
                    t4_run_table_entry_t horizontal = TwoDimensionalCodes[7];
                    put_encoded_bits(s, horizontal.code, horizontal.length);
                    if (a0 + a1 == 0 || !pixel_is_black(row, a0)) {
                        put_1d_span(s, a1 - a0, WhiteCodes);
                        put_1d_span(s, a2 - a1, BlackCodes);
                    } else {
                        put_1d_span(s, a1 - a0, BlackCodes);
                        put_1d_span(s, a2 - a1, WhiteCodes);
                    }
                    a0 = a2;
                    aCursor += 2;
                }

                if (a0 >= s.image_width)
                    break;
                if (aCursor >= currentSteps)
                    aCursor = currentSteps - 1;
                a1 = checked((int)s.cur_runs[aCursor]);
            } else {
                t4_run_table_entry_t pass = TwoDimensionalCodes[8];
                put_encoded_bits(s, pass.code, pass.length);
                a0 = b2;
                if (a0 >= s.image_width)
                    break;
            }

            if (pixel_is_black(row, a0))
                bCursor |= 1;
            else
                bCursor &= ~1;

            if (a0 < checked((int)s.ref_runs[bCursor])) {
                for (; bCursor >= 0; bCursor -= 2) {
                    if (a0 >= checked((int)s.ref_runs[bCursor]))
                        break;
                }
                bCursor += 2;
            } else {
                for (; bCursor < s.ref_steps; bCursor += 2) {
                    if (a0 < checked((int)s.ref_runs[bCursor]))
                        break;
                }
                if (bCursor >= s.ref_steps)
                    bCursor = s.ref_steps - 1;
            }
            b1 = checked((int)s.ref_runs[bCursor]);
        }

        s.ref_steps = currentSteps;
        (s.cur_runs, s.ref_runs) = (s.ref_runs, s.cur_runs);
    }

    private static void encode_1d_row(t4_t6_encode_state_t s, ReadOnlySpan<byte> row) {
        s.ref_steps = row_to_run_lengths(s.ref_runs, row, s.image_width);
        put_1d_span(s, checked((int)s.ref_runs[0]), WhiteCodes);
        for (int i = 1; i < s.ref_steps; i++) {
            int span = checked((int)(s.ref_runs[i] - s.ref_runs[i - 1]));
            put_1d_span(s, span, (i & 1) != 0 ? BlackCodes : WhiteCodes);
        }
        s.ref_runs[s.ref_steps] = s.ref_runs[s.ref_steps - 1];
        s.ref_runs[s.ref_steps + 1] = s.ref_runs[s.ref_steps - 1];
        s.ref_runs[s.ref_steps + 2] = s.ref_runs[s.ref_steps - 1];
    }

    private static int encode_row(t4_t6_encode_state_t s, ReadOnlySpan<byte> row) {
        switch (s.encoding) {
            case t4_rx.T4_COMPRESSION_T6:
                encode_2d_row(s, row);
                break;

            case t4_rx.T4_COMPRESSION_T4_2D:
                encode_eol(s);
                if (s.row_is_2d) {
                    encode_2d_row(s, row);
                    s.rows_to_next_1d_row--;
                } else {
                    encode_1d_row(s, row);
                    s.row_is_2d = true;
                }
                if (s.rows_to_next_1d_row <= 0) {
                    s.row_is_2d = false;
                    s.rows_to_next_1d_row = s.max_rows_to_next_1d_row - 1;
                }
                break;

            default:
                encode_eol(s);
                encode_1d_row(s, row);
                break;
        }
        s.image_length++;
        return 0;
    }

    private static int finalise_page(t4_t6_encode_state_t s) {
        int count = s.encoding == t4_rx.T4_COMPRESSION_T6
            ? EolsToEndT6Page
            : EolsToEndT4Page;
        if (s.encoding != t4_rx.T4_COMPRESSION_T6)
            s.row_is_2d = false;
        for (int i = 0; i < count; i++)
            encode_eol(s);
        put_encoded_bits(s, 0xFF, 7);
        s.row_bits = -1;
        return 0;
    }

    private static int get_next_row(t4_t6_encode_state_t s) {
        if (s.row_bits < 0 || s.row_read_handler is null)
            return -1;

        byte[] row = new byte[s.bytes_per_row];
        s.bitstream_iptr = 0;
        s.bitstream_optr = 0;
        s.bit_pos = 7;
        int length;
        do {
            Array.Clear(row);
            length = s.row_read_handler(
                s.row_read_user_data,
                row,
                s.bytes_per_row);
            if (length == s.bytes_per_row)
                encode_row(s, row);
            else
                finalise_page(s);
        }
        while (length > 0 && s.bitstream_iptr == 0);

        s.compressed_image_size += 8 * s.bitstream_iptr;
        return length;
    }
}
