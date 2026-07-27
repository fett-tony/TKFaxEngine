/*
 * TKFaxEngine - managed C# port
 *
 * t4_rx.cs
 *
 * Combined port of:
 *   t4_rx.c
 *   t4_rx.h
 *   private/t4_rx.h (already merged into the supplied header)
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2003, 2007, 2010 Steve Underwood.
 *
 * This port preserves the GNU Lesser General Public License version 2.1
 * licensing terms of the original source files.
 */

#nullable enable

using BitMiracle.LibTiff.Classic;

namespace TKFaxEngine.FaxImage;

[Flags]
public enum t4_image_compression_t {
    T4_COMPRESSION_NONE = 0x00000001,
    T4_COMPRESSION_T4_1D = 0x00000002,
    T4_COMPRESSION_T4_2D = 0x00000004,
    T4_COMPRESSION_T6 = 0x00000008,
    T4_COMPRESSION_T85 = 0x00000010,
    T4_COMPRESSION_T85_L0 = 0x00000020,
    T4_COMPRESSION_T43 = 0x00000040,
    T4_COMPRESSION_T45 = 0x00000080,
    T4_COMPRESSION_T42_T81 = 0x00000100,
    T4_COMPRESSION_SYCC_T81 = 0x00000200,
    T4_COMPRESSION_T88 = 0x00000400,
    T4_COMPRESSION_UNCOMPRESSED = 0x00001000,
    T4_COMPRESSION_JPEG = 0x00002000,
    T4_COMPRESSION_NO_SUBSAMPLING = 0x00800000,
    T4_COMPRESSION_GRAYSCALE = 0x01000000,
    T4_COMPRESSION_COLOUR = 0x02000000,
    T4_COMPRESSION_12BIT = 0x04000000,
    T4_COMPRESSION_COLOUR_TO_GRAY = 0x08000000,
    T4_COMPRESSION_GRAY_TO_BILEVEL = 0x10000000,
    T4_COMPRESSION_COLOUR_TO_BILEVEL = 0x20000000,
    T4_COMPRESSION_RESCALING = 0x40000000
}

public enum t4_image_types_t {
    T4_IMAGE_TYPE_BILEVEL = 0,
    T4_IMAGE_TYPE_COLOUR_BILEVEL = 1,
    T4_IMAGE_TYPE_4COLOUR_BILEVEL = 2,
    T4_IMAGE_TYPE_GRAY_8BIT = 3,
    T4_IMAGE_TYPE_GRAY_12BIT = 4,
    T4_IMAGE_TYPE_COLOUR_8BIT = 5,
    T4_IMAGE_TYPE_4COLOUR_8BIT = 6,
    T4_IMAGE_TYPE_COLOUR_12BIT = 7,
    T4_IMAGE_TYPE_4COLOUR_12BIT = 8
}

public enum t4_image_x_resolution_t {
    T4_X_RESOLUTION_100 = 3937,
    T4_X_RESOLUTION_R4 = 4020,
    T4_X_RESOLUTION_200 = 7874,
    T4_X_RESOLUTION_R8 = 8040,
    T4_X_RESOLUTION_300 = 11811,
    T4_X_RESOLUTION_400 = 15748,
    T4_X_RESOLUTION_R16 = 16080,
    T4_X_RESOLUTION_600 = 23622,
    T4_X_RESOLUTION_1200 = 47244
}

public enum t4_image_y_resolution_t {
    T4_Y_RESOLUTION_STANDARD = 3850,
    T4_Y_RESOLUTION_100 = 3937,
    T4_Y_RESOLUTION_FINE = 7700,
    T4_Y_RESOLUTION_200 = 7874,
    T4_Y_RESOLUTION_300 = 11811,
    T4_Y_RESOLUTION_SUPERFINE = 15400,
    T4_Y_RESOLUTION_400 = 15748,
    T4_Y_RESOLUTION_600 = 23622,
    T4_Y_RESOLUTION_800 = 31496,
    T4_Y_RESOLUTION_1200 = 47244
}

[Flags]
public enum t4_image_resolution_t {
    T4_RESOLUTION_R8_STANDARD = 0x0001,
    T4_RESOLUTION_R8_FINE = 0x0002,
    T4_RESOLUTION_R8_SUPERFINE = 0x0004,
    T4_RESOLUTION_R16_SUPERFINE = 0x0008,
    T4_RESOLUTION_100_100 = 0x0010,
    T4_RESOLUTION_200_100 = 0x0020,
    T4_RESOLUTION_200_200 = 0x0040,
    T4_RESOLUTION_200_400 = 0x0080,
    T4_RESOLUTION_300_300 = 0x0100,
    T4_RESOLUTION_300_600 = 0x0200,
    T4_RESOLUTION_400_400 = 0x0400,
    T4_RESOLUTION_400_800 = 0x0800,
    T4_RESOLUTION_600_600 = 0x1000,
    T4_RESOLUTION_600_1200 = 0x2000,
    T4_RESOLUTION_1200_1200 = 0x4000
}

public enum t4_image_width_t {
    T4_WIDTH_100_A4 = 864,
    T4_WIDTH_100_B4 = 1024,
    T4_WIDTH_100_A3 = 1216,
    T4_WIDTH_200_A4 = 1728,
    T4_WIDTH_200_B4 = 2048,
    T4_WIDTH_200_A3 = 2432,
    T4_WIDTH_300_A4 = 2592,
    T4_WIDTH_300_B4 = 3072,
    T4_WIDTH_300_A3 = 3648,
    T4_WIDTH_400_A4 = 3456,
    T4_WIDTH_400_B4 = 4096,
    T4_WIDTH_400_A3 = 4864,
    T4_WIDTH_600_A4 = 5184,
    T4_WIDTH_600_B4 = 6144,
    T4_WIDTH_600_A3 = 7296,
    T4_WIDTH_1200_A4 = 10368,
    T4_WIDTH_1200_B4 = 12288,
    T4_WIDTH_1200_A3 = 14592
}

public enum t4_image_length_t {
    T4_LENGTH_STANDARD_A4 = 1143,
    T4_LENGTH_FINE_A4 = 2286,
    T4_LENGTH_300_A4 = 4665,
    T4_LENGTH_SUPERFINE_A4 = 4573,
    T4_LENGTH_600_A4 = 6998,
    T4_LENGTH_800_A4 = 9330,
    T4_LENGTH_1200_A4 = 13996,
    T4_LENGTH_STANDARD_B4 = 1359,
    T4_LENGTH_FINE_B4 = 2718,
    T4_LENGTH_300_B4 = 4169,
    T4_LENGTH_SUPERFINE_B4 = 5436,
    T4_LENGTH_600_B4 = 8338,
    T4_LENGTH_800_B4 = 11118,
    T4_LENGTH_1200_B4 = 16677,
    T4_LENGTH_STANDARD_A3 = 1617,
    T4_LENGTH_FINE_A3 = 3234,
    T4_LENGTH_300_A3 = 4960,
    T4_LENGTH_SUPERFINE_A3 = 6468,
    T4_LENGTH_600_A3 = 9921,
    T4_LENGTH_800_A3 = 13228,
    T4_LENGTH_1200_A3 = 19842,
    T4_LENGTH_STANDARD_US_LETTER = 1075,
    T4_LENGTH_FINE_US_LETTER = 2151,
    T4_LENGTH_300_US_LETTER = 3300,
    T4_LENGTH_SUPERFINE_US_LETTER = 4302,
    T4_LENGTH_600_US_LETTER = 6700,
    T4_LENGTH_800_US_LETTER = 8800,
    T4_LENGTH_1200_US_LETTER = 13200,
    T4_LENGTH_STANDARD_US_LEGAL = 1369,
    T4_LENGTH_FINE_US_LEGAL = 2738,
    T4_LENGTH_300_US_LEGAL = 4200,
    T4_LENGTH_SUPERFINE_US_LEGAL = 5476,
    T4_LENGTH_600_US_LEGAL = 8400,
    T4_LENGTH_800_US_LEGAL = 11200,
    T4_LENGTH_1200_US_LEGAL = 16800
}

[Flags]
public enum t4_image_support_t {
    T4_SUPPORT_WIDTH_215MM = 0x000001,
    T4_SUPPORT_WIDTH_255MM = 0x000002,
    T4_SUPPORT_WIDTH_303MM = 0x000004,
    T4_SUPPORT_LENGTH_UNLIMITED = 0x010000,
    T4_SUPPORT_LENGTH_A4 = 0x020000,
    T4_SUPPORT_LENGTH_B4 = 0x040000,
    T4_SUPPORT_LENGTH_US_LETTER = 0x080000,
    T4_SUPPORT_LENGTH_US_LEGAL = 0x100000
}

public enum t4_decoder_status_t {
    T4_DECODE_MORE_DATA = 0,
    T4_DECODE_OK = -1,
    T4_DECODE_INTERRUPT = -2,
    T4_DECODE_ABORTED = -3,
    T4_DECODE_NOMEM = -4,
    T4_DECODE_INVALID_DATA = -5
}

public delegate int t4_row_read_handler_t(
    object? user_data,
    Span<byte> buf,
    int len);

public delegate int t4_row_write_handler_t(
    object? user_data,
    ReadOnlySpan<byte> buf,
    int len);

public delegate int t4_image_put_handler_t(
    object? user_data,
    byte[]? buf,
    int len);

public sealed class t4_stats_t {
    public int pages_transferred;
    public int pages_in_file;
    public int bad_rows;
    public int longest_bad_row_run;
    public int image_type;
    public int image_x_resolution;
    public int image_y_resolution;
    public int image_width;
    public int image_length;
    public int type;
    public int x_resolution;
    public int y_resolution;
    public int width;
    public int length;
    public int compression;
    public int line_image_size;
}

public sealed class t4_rx_tiff_state_t {
    public string? file;
    public Tiff? tiff_file;
    public int image_type;
    public int compression;
    public ushort photo_metric;
    public ushort fill_order;
    public int pages_in_file;
    public DateTime page_start_time;
    public byte[] image_buffer = Array.Empty<byte>();
    public int image_size;
    public int image_buffer_size;
}

public sealed class t4_rx_metadata_t {
    public int compression;
    public uint image_width;
    public uint image_length;
    public int x_resolution;
    public int y_resolution;
    public string? vendor;
    public string? model;
    public string? far_ident;
    public string? sub_address;
    public string? dcs;
}

public sealed class no_decoder_state_t {
    public byte[]? buf;
    public int buf_len;
    public int buf_ptr;
}

public sealed class t4_rx_decoder_t {
    public no_decoder_state_t no_decoder = new();
    public t4_t6_decode_state_t t4_t6 = new();
    public T85DecodeState? t85;
    public T42DecodeState? t42;
    public T43DecodeState? t43;
}

public sealed class t4_rx_state_t {
    public t4_row_write_handler_t? row_handler;
    public object? row_handler_user_data;
    public int supported_tiff_compressions;
    public int current_page;
    public int line_image_size;
    public t4_rx_decoder_t decoder = new();
    public t4_image_put_handler_t? image_put_handler;
    public int current_decoder;
    public t4_rx_metadata_t metadata = new();
    public t4_rx_tiff_state_t tiff = new();
    public SpanLogState logging = new();
}

internal sealed class packer_t {
    internal byte[] buffer = Array.Empty<byte>();
    internal int pointer;
    internal int row;
}

public static class t4_rx {
    // Native public constants retained for direct C-to-C# migration.
    public const int T4_COMPRESSION_NONE = (int)t4_image_compression_t.T4_COMPRESSION_NONE;
    public const int T4_COMPRESSION_T4_1D = (int)t4_image_compression_t.T4_COMPRESSION_T4_1D;
    public const int T4_COMPRESSION_T4_2D = (int)t4_image_compression_t.T4_COMPRESSION_T4_2D;
    public const int T4_COMPRESSION_T6 = (int)t4_image_compression_t.T4_COMPRESSION_T6;
    public const int T4_COMPRESSION_T85 = (int)t4_image_compression_t.T4_COMPRESSION_T85;
    public const int T4_COMPRESSION_T85_L0 = (int)t4_image_compression_t.T4_COMPRESSION_T85_L0;
    public const int T4_COMPRESSION_T43 = (int)t4_image_compression_t.T4_COMPRESSION_T43;
    public const int T4_COMPRESSION_T45 = (int)t4_image_compression_t.T4_COMPRESSION_T45;
    public const int T4_COMPRESSION_T42_T81 = (int)t4_image_compression_t.T4_COMPRESSION_T42_T81;
    public const int T4_COMPRESSION_SYCC_T81 = (int)t4_image_compression_t.T4_COMPRESSION_SYCC_T81;
    public const int T4_COMPRESSION_T88 = (int)t4_image_compression_t.T4_COMPRESSION_T88;
    public const int T4_COMPRESSION_UNCOMPRESSED = (int)t4_image_compression_t.T4_COMPRESSION_UNCOMPRESSED;
    public const int T4_COMPRESSION_JPEG = (int)t4_image_compression_t.T4_COMPRESSION_JPEG;
    public const int T4_COMPRESSION_NO_SUBSAMPLING = (int)t4_image_compression_t.T4_COMPRESSION_NO_SUBSAMPLING;
    public const int T4_COMPRESSION_GRAYSCALE = (int)t4_image_compression_t.T4_COMPRESSION_GRAYSCALE;
    public const int T4_COMPRESSION_COLOUR = (int)t4_image_compression_t.T4_COMPRESSION_COLOUR;
    public const int T4_COMPRESSION_12BIT = (int)t4_image_compression_t.T4_COMPRESSION_12BIT;
    public const int T4_COMPRESSION_COLOUR_TO_GRAY = (int)t4_image_compression_t.T4_COMPRESSION_COLOUR_TO_GRAY;
    public const int T4_COMPRESSION_GRAY_TO_BILEVEL = (int)t4_image_compression_t.T4_COMPRESSION_GRAY_TO_BILEVEL;
    public const int T4_COMPRESSION_COLOUR_TO_BILEVEL = (int)t4_image_compression_t.T4_COMPRESSION_COLOUR_TO_BILEVEL;
    public const int T4_COMPRESSION_RESCALING = (int)t4_image_compression_t.T4_COMPRESSION_RESCALING;

    public const int T4_IMAGE_TYPE_BILEVEL = (int)t4_image_types_t.T4_IMAGE_TYPE_BILEVEL;
    public const int T4_IMAGE_TYPE_COLOUR_BILEVEL = (int)t4_image_types_t.T4_IMAGE_TYPE_COLOUR_BILEVEL;
    public const int T4_IMAGE_TYPE_4COLOUR_BILEVEL = (int)t4_image_types_t.T4_IMAGE_TYPE_4COLOUR_BILEVEL;
    public const int T4_IMAGE_TYPE_GRAY_8BIT = (int)t4_image_types_t.T4_IMAGE_TYPE_GRAY_8BIT;
    public const int T4_IMAGE_TYPE_GRAY_12BIT = (int)t4_image_types_t.T4_IMAGE_TYPE_GRAY_12BIT;
    public const int T4_IMAGE_TYPE_COLOUR_8BIT = (int)t4_image_types_t.T4_IMAGE_TYPE_COLOUR_8BIT;
    public const int T4_IMAGE_TYPE_4COLOUR_8BIT = (int)t4_image_types_t.T4_IMAGE_TYPE_4COLOUR_8BIT;
    public const int T4_IMAGE_TYPE_COLOUR_12BIT = (int)t4_image_types_t.T4_IMAGE_TYPE_COLOUR_12BIT;
    public const int T4_IMAGE_TYPE_4COLOUR_12BIT = (int)t4_image_types_t.T4_IMAGE_TYPE_4COLOUR_12BIT;

    public const int T4_X_RESOLUTION_100 = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_100;
    public const int T4_X_RESOLUTION_R4 = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_R4;
    public const int T4_X_RESOLUTION_200 = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_200;
    public const int T4_X_RESOLUTION_R8 = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_R8;
    public const int T4_X_RESOLUTION_300 = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_300;
    public const int T4_X_RESOLUTION_400 = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_400;
    public const int T4_X_RESOLUTION_R16 = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_R16;
    public const int T4_X_RESOLUTION_600 = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_600;
    public const int T4_X_RESOLUTION_1200 = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_1200;

    public const int T4_Y_RESOLUTION_STANDARD = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_STANDARD;
    public const int T4_Y_RESOLUTION_100 = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_100;
    public const int T4_Y_RESOLUTION_FINE = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_FINE;
    public const int T4_Y_RESOLUTION_200 = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_200;
    public const int T4_Y_RESOLUTION_300 = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_300;
    public const int T4_Y_RESOLUTION_SUPERFINE = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_SUPERFINE;
    public const int T4_Y_RESOLUTION_400 = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_400;
    public const int T4_Y_RESOLUTION_600 = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_600;
    public const int T4_Y_RESOLUTION_800 = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_800;
    public const int T4_Y_RESOLUTION_1200 = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_1200;

    public const int T4_RESOLUTION_R8_STANDARD = (int)t4_image_resolution_t.T4_RESOLUTION_R8_STANDARD;
    public const int T4_RESOLUTION_R8_FINE = (int)t4_image_resolution_t.T4_RESOLUTION_R8_FINE;
    public const int T4_RESOLUTION_R8_SUPERFINE = (int)t4_image_resolution_t.T4_RESOLUTION_R8_SUPERFINE;
    public const int T4_RESOLUTION_R16_SUPERFINE = (int)t4_image_resolution_t.T4_RESOLUTION_R16_SUPERFINE;
    public const int T4_RESOLUTION_100_100 = (int)t4_image_resolution_t.T4_RESOLUTION_100_100;
    public const int T4_RESOLUTION_200_100 = (int)t4_image_resolution_t.T4_RESOLUTION_200_100;
    public const int T4_RESOLUTION_200_200 = (int)t4_image_resolution_t.T4_RESOLUTION_200_200;
    public const int T4_RESOLUTION_200_400 = (int)t4_image_resolution_t.T4_RESOLUTION_200_400;
    public const int T4_RESOLUTION_300_300 = (int)t4_image_resolution_t.T4_RESOLUTION_300_300;
    public const int T4_RESOLUTION_300_600 = (int)t4_image_resolution_t.T4_RESOLUTION_300_600;
    public const int T4_RESOLUTION_400_400 = (int)t4_image_resolution_t.T4_RESOLUTION_400_400;
    public const int T4_RESOLUTION_400_800 = (int)t4_image_resolution_t.T4_RESOLUTION_400_800;
    public const int T4_RESOLUTION_600_600 = (int)t4_image_resolution_t.T4_RESOLUTION_600_600;
    public const int T4_RESOLUTION_600_1200 = (int)t4_image_resolution_t.T4_RESOLUTION_600_1200;
    public const int T4_RESOLUTION_1200_1200 = (int)t4_image_resolution_t.T4_RESOLUTION_1200_1200;

    public const int T4_WIDTH_100_A4 = (int)t4_image_width_t.T4_WIDTH_100_A4;
    public const int T4_WIDTH_100_B4 = (int)t4_image_width_t.T4_WIDTH_100_B4;
    public const int T4_WIDTH_100_A3 = (int)t4_image_width_t.T4_WIDTH_100_A3;
    public const int T4_WIDTH_200_A4 = (int)t4_image_width_t.T4_WIDTH_200_A4;
    public const int T4_WIDTH_200_B4 = (int)t4_image_width_t.T4_WIDTH_200_B4;
    public const int T4_WIDTH_200_A3 = (int)t4_image_width_t.T4_WIDTH_200_A3;
    public const int T4_WIDTH_300_A4 = (int)t4_image_width_t.T4_WIDTH_300_A4;
    public const int T4_WIDTH_300_B4 = (int)t4_image_width_t.T4_WIDTH_300_B4;
    public const int T4_WIDTH_300_A3 = (int)t4_image_width_t.T4_WIDTH_300_A3;
    public const int T4_WIDTH_400_A4 = (int)t4_image_width_t.T4_WIDTH_400_A4;
    public const int T4_WIDTH_400_B4 = (int)t4_image_width_t.T4_WIDTH_400_B4;
    public const int T4_WIDTH_400_A3 = (int)t4_image_width_t.T4_WIDTH_400_A3;
    public const int T4_WIDTH_600_A4 = (int)t4_image_width_t.T4_WIDTH_600_A4;
    public const int T4_WIDTH_600_B4 = (int)t4_image_width_t.T4_WIDTH_600_B4;
    public const int T4_WIDTH_600_A3 = (int)t4_image_width_t.T4_WIDTH_600_A3;
    public const int T4_WIDTH_1200_A4 = (int)t4_image_width_t.T4_WIDTH_1200_A4;
    public const int T4_WIDTH_1200_B4 = (int)t4_image_width_t.T4_WIDTH_1200_B4;
    public const int T4_WIDTH_1200_A3 = (int)t4_image_width_t.T4_WIDTH_1200_A3;

    public const int T4_LENGTH_STANDARD_A4 = (int)t4_image_length_t.T4_LENGTH_STANDARD_A4;
    public const int T4_LENGTH_FINE_A4 = (int)t4_image_length_t.T4_LENGTH_FINE_A4;
    public const int T4_LENGTH_300_A4 = (int)t4_image_length_t.T4_LENGTH_300_A4;
    public const int T4_LENGTH_SUPERFINE_A4 = (int)t4_image_length_t.T4_LENGTH_SUPERFINE_A4;
    public const int T4_LENGTH_600_A4 = (int)t4_image_length_t.T4_LENGTH_600_A4;
    public const int T4_LENGTH_800_A4 = (int)t4_image_length_t.T4_LENGTH_800_A4;
    public const int T4_LENGTH_1200_A4 = (int)t4_image_length_t.T4_LENGTH_1200_A4;
    public const int T4_LENGTH_STANDARD_B4 = (int)t4_image_length_t.T4_LENGTH_STANDARD_B4;
    public const int T4_LENGTH_FINE_B4 = (int)t4_image_length_t.T4_LENGTH_FINE_B4;
    public const int T4_LENGTH_300_B4 = (int)t4_image_length_t.T4_LENGTH_300_B4;
    public const int T4_LENGTH_SUPERFINE_B4 = (int)t4_image_length_t.T4_LENGTH_SUPERFINE_B4;
    public const int T4_LENGTH_600_B4 = (int)t4_image_length_t.T4_LENGTH_600_B4;
    public const int T4_LENGTH_800_B4 = (int)t4_image_length_t.T4_LENGTH_800_B4;
    public const int T4_LENGTH_1200_B4 = (int)t4_image_length_t.T4_LENGTH_1200_B4;
    public const int T4_LENGTH_STANDARD_A3 = (int)t4_image_length_t.T4_LENGTH_STANDARD_A3;
    public const int T4_LENGTH_FINE_A3 = (int)t4_image_length_t.T4_LENGTH_FINE_A3;
    public const int T4_LENGTH_300_A3 = (int)t4_image_length_t.T4_LENGTH_300_A3;
    public const int T4_LENGTH_SUPERFINE_A3 = (int)t4_image_length_t.T4_LENGTH_SUPERFINE_A3;
    public const int T4_LENGTH_600_A3 = (int)t4_image_length_t.T4_LENGTH_600_A3;
    public const int T4_LENGTH_800_A3 = (int)t4_image_length_t.T4_LENGTH_800_A3;
    public const int T4_LENGTH_1200_A3 = (int)t4_image_length_t.T4_LENGTH_1200_A3;
    public const int T4_LENGTH_STANDARD_US_LETTER = (int)t4_image_length_t.T4_LENGTH_STANDARD_US_LETTER;
    public const int T4_LENGTH_FINE_US_LETTER = (int)t4_image_length_t.T4_LENGTH_FINE_US_LETTER;
    public const int T4_LENGTH_300_US_LETTER = (int)t4_image_length_t.T4_LENGTH_300_US_LETTER;
    public const int T4_LENGTH_SUPERFINE_US_LETTER = (int)t4_image_length_t.T4_LENGTH_SUPERFINE_US_LETTER;
    public const int T4_LENGTH_600_US_LETTER = (int)t4_image_length_t.T4_LENGTH_600_US_LETTER;
    public const int T4_LENGTH_800_US_LETTER = (int)t4_image_length_t.T4_LENGTH_800_US_LETTER;
    public const int T4_LENGTH_1200_US_LETTER = (int)t4_image_length_t.T4_LENGTH_1200_US_LETTER;
    public const int T4_LENGTH_STANDARD_US_LEGAL = (int)t4_image_length_t.T4_LENGTH_STANDARD_US_LEGAL;
    public const int T4_LENGTH_FINE_US_LEGAL = (int)t4_image_length_t.T4_LENGTH_FINE_US_LEGAL;
    public const int T4_LENGTH_300_US_LEGAL = (int)t4_image_length_t.T4_LENGTH_300_US_LEGAL;
    public const int T4_LENGTH_SUPERFINE_US_LEGAL = (int)t4_image_length_t.T4_LENGTH_SUPERFINE_US_LEGAL;
    public const int T4_LENGTH_600_US_LEGAL = (int)t4_image_length_t.T4_LENGTH_600_US_LEGAL;
    public const int T4_LENGTH_800_US_LEGAL = (int)t4_image_length_t.T4_LENGTH_800_US_LEGAL;
    public const int T4_LENGTH_1200_US_LEGAL = (int)t4_image_length_t.T4_LENGTH_1200_US_LEGAL;

    public const int T4_SUPPORT_WIDTH_215MM = (int)t4_image_support_t.T4_SUPPORT_WIDTH_215MM;
    public const int T4_SUPPORT_WIDTH_255MM = (int)t4_image_support_t.T4_SUPPORT_WIDTH_255MM;
    public const int T4_SUPPORT_WIDTH_303MM = (int)t4_image_support_t.T4_SUPPORT_WIDTH_303MM;
    public const int T4_SUPPORT_LENGTH_UNLIMITED = (int)t4_image_support_t.T4_SUPPORT_LENGTH_UNLIMITED;
    public const int T4_SUPPORT_LENGTH_A4 = (int)t4_image_support_t.T4_SUPPORT_LENGTH_A4;
    public const int T4_SUPPORT_LENGTH_B4 = (int)t4_image_support_t.T4_SUPPORT_LENGTH_B4;
    public const int T4_SUPPORT_LENGTH_US_LETTER = (int)t4_image_support_t.T4_SUPPORT_LENGTH_US_LETTER;
    public const int T4_SUPPORT_LENGTH_US_LEGAL = (int)t4_image_support_t.T4_SUPPORT_LENGTH_US_LEGAL;

    public const int T4_DECODE_MORE_DATA = (int)t4_decoder_status_t.T4_DECODE_MORE_DATA;
    public const int T4_DECODE_OK = (int)t4_decoder_status_t.T4_DECODE_OK;
    public const int T4_DECODE_INTERRUPT = (int)t4_decoder_status_t.T4_DECODE_INTERRUPT;
    public const int T4_DECODE_ABORTED = (int)t4_decoder_status_t.T4_DECODE_ABORTED;
    public const int T4_DECODE_NOMEM = (int)t4_decoder_status_t.T4_DECODE_NOMEM;
    public const int T4_DECODE_INVALID_DATA = (int)t4_decoder_status_t.T4_DECODE_INVALID_DATA;

    public const int T4_WIDTH_R4_A4 =
        (int)t4_image_width_t.T4_WIDTH_100_A4;
    public const int T4_WIDTH_R4_B4 =
        (int)t4_image_width_t.T4_WIDTH_100_B4;
    public const int T4_WIDTH_R4_A3 =
        (int)t4_image_width_t.T4_WIDTH_100_A3;

    public const int T4_WIDTH_R8_A4 =
        (int)t4_image_width_t.T4_WIDTH_200_A4;
    public const int T4_WIDTH_R8_B4 =
        (int)t4_image_width_t.T4_WIDTH_200_B4;
    public const int T4_WIDTH_R8_A3 =
        (int)t4_image_width_t.T4_WIDTH_200_A3;

    public const int T4_WIDTH_R16_A4 =
        (int)t4_image_width_t.T4_WIDTH_400_A4;
    public const int T4_WIDTH_R16_B4 =
        (int)t4_image_width_t.T4_WIDTH_400_B4;
    public const int T4_WIDTH_R16_A3 =
        (int)t4_image_width_t.T4_WIDTH_400_A3;

    public static string t4_compression_to_str(
        int compression) {
        return (t4_image_compression_t)compression switch {
            t4_image_compression_t.T4_COMPRESSION_NONE =>
                "None",
            t4_image_compression_t.T4_COMPRESSION_T4_1D =>
                "T.4 1-D",
            t4_image_compression_t.T4_COMPRESSION_T4_2D =>
                "T.4 2-D",
            t4_image_compression_t.T4_COMPRESSION_T6 =>
                "T.6",
            t4_image_compression_t.T4_COMPRESSION_T85 =>
                "T.85",
            t4_image_compression_t.T4_COMPRESSION_T85_L0 =>
                "T.85(L0)",
            t4_image_compression_t.T4_COMPRESSION_T88 =>
                "T.88",
            t4_image_compression_t.T4_COMPRESSION_T42_T81 =>
                "T.81+T.42",
            t4_image_compression_t.T4_COMPRESSION_SYCC_T81 =>
                "T.81+sYCC",
            t4_image_compression_t.T4_COMPRESSION_T43 =>
                "T.43",
            t4_image_compression_t.T4_COMPRESSION_T45 =>
                "T.45",
            t4_image_compression_t.T4_COMPRESSION_UNCOMPRESSED =>
                "Uncompressed",
            t4_image_compression_t.T4_COMPRESSION_JPEG =>
                "JPEG",
            _ =>
                "???"
        };
    }

    public static string t4_image_type_to_str(int type) {
        return (t4_image_types_t)type switch {
            t4_image_types_t.T4_IMAGE_TYPE_BILEVEL =>
                "bi-level",
            t4_image_types_t.T4_IMAGE_TYPE_COLOUR_BILEVEL =>
                "bi-level colour",
            t4_image_types_t.T4_IMAGE_TYPE_4COLOUR_BILEVEL =>
                "CMYK bi-level colour",
            t4_image_types_t.T4_IMAGE_TYPE_GRAY_8BIT =>
                "8-bit gray scale",
            t4_image_types_t.T4_IMAGE_TYPE_GRAY_12BIT =>
                "12-bit gray scale",
            t4_image_types_t.T4_IMAGE_TYPE_COLOUR_8BIT =>
                "8-bit colour",
            t4_image_types_t.T4_IMAGE_TYPE_4COLOUR_8BIT =>
                "CMYK 8-bit colour",
            t4_image_types_t.T4_IMAGE_TYPE_COLOUR_12BIT =>
                "12-bit colour",
            t4_image_types_t.T4_IMAGE_TYPE_4COLOUR_12BIT =>
                "CMYK 12-bit colour",
            _ =>
                "???"
        };
    }

    public static string t4_image_resolution_to_str(
        int resolutionCode) {
        return (t4_image_resolution_t)resolutionCode switch {
            t4_image_resolution_t.T4_RESOLUTION_R8_STANDARD =>
                "204dpi x 98dpi",
            t4_image_resolution_t.T4_RESOLUTION_R8_FINE =>
                "204dpi x 196dpi",
            t4_image_resolution_t.T4_RESOLUTION_R8_SUPERFINE =>
                "204dpi x 391dpi",
            t4_image_resolution_t.T4_RESOLUTION_R16_SUPERFINE =>
                "408dpi x 391dpi",
            t4_image_resolution_t.T4_RESOLUTION_100_100 =>
                "100dpi x 100dpi",
            t4_image_resolution_t.T4_RESOLUTION_200_100 =>
                "200dpi x 100dpi",
            t4_image_resolution_t.T4_RESOLUTION_200_200 =>
                "200dpi x 200dpi",
            t4_image_resolution_t.T4_RESOLUTION_200_400 =>
                "200dpi x 400dpi",
            t4_image_resolution_t.T4_RESOLUTION_300_300 =>
                "300dpi x 300dpi",
            t4_image_resolution_t.T4_RESOLUTION_300_600 =>
                "300dpi x 600dpi",
            t4_image_resolution_t.T4_RESOLUTION_400_400 =>
                "400dpi x 400dpi",
            t4_image_resolution_t.T4_RESOLUTION_400_800 =>
                "400dpi x 800dpi",
            t4_image_resolution_t.T4_RESOLUTION_600_600 =>
                "600dpi x 600dpi",
            t4_image_resolution_t.T4_RESOLUTION_600_1200 =>
                "600dpi x 1200dpi",
            t4_image_resolution_t.T4_RESOLUTION_1200_1200 =>
                "1200dpi x 1200dpi",
            _ =>
                "???"
        };
    }

    private const int IMAGE_BUFFER_GROWTH = 65536;

    private static int set_tiff_directory_info(t4_rx_state_t s) {
        if (s.tiff.tiff_file is null)
            return -1;

        int width = get_current_image_width(s);
        int length = get_current_image_length(s);
        int bits_per_sample = 1;
        int samples_per_pixel = 1;
        Photometric photometric = Photometric.MINISWHITE;
        Compression output_compression;
        Group3Opt output_t4_options = 0;

        switch (s.tiff.compression) {
            case T4_COMPRESSION_T4_1D:
                output_compression = Compression.CCITTFAX3;
                output_t4_options = Group3Opt.FILLBITS;
                break;
            case T4_COMPRESSION_T4_2D:
                output_compression = Compression.CCITTFAX3;
                output_t4_options = Group3Opt.FILLBITS | Group3Opt.ENCODING2D;
                break;
            case T4_COMPRESSION_T6:
                output_compression = Compression.CCITTFAX4;
                break;
            case T4_COMPRESSION_T85:
            case T4_COMPRESSION_T85_L0:
                output_compression = (Compression)9;
                break;
            case T4_COMPRESSION_JPEG:
                output_compression = Compression.JPEG;
                bits_per_sample = 8;
                samples_per_pixel = s.tiff.image_type == T4_IMAGE_TYPE_COLOUR_8BIT ? 3 : 1;
                photometric = samples_per_pixel == 3 ? Photometric.YCBCR : Photometric.MINISBLACK;
                break;
            case T4_COMPRESSION_T42_T81:
                output_compression = Compression.JPEG;
                bits_per_sample = 8;
                samples_per_pixel = s.tiff.image_type == T4_IMAGE_TYPE_COLOUR_8BIT ? 3 : 1;
                photometric = samples_per_pixel == 3 ? (Photometric)10 : Photometric.MINISBLACK;
                break;
            case T4_COMPRESSION_SYCC_T81:
                output_compression = Compression.JPEG;
                bits_per_sample = 8;
                samples_per_pixel = s.tiff.image_type == T4_IMAGE_TYPE_COLOUR_8BIT ? 3 : 1;
                photometric = samples_per_pixel == 3 ? Photometric.YCBCR : Photometric.MINISBLACK;
                break;
            case T4_COMPRESSION_T43:
                output_compression = (Compression)10;
                bits_per_sample = 8;
                samples_per_pixel = 3;
                photometric = (Photometric)10;
                break;
            case T4_COMPRESSION_UNCOMPRESSED:
            default:
                output_compression = Compression.NONE;
                bits_per_sample = s.tiff.image_type == T4_IMAGE_TYPE_BILEVEL ? 1 : 8;
                samples_per_pixel = s.tiff.image_type == T4_IMAGE_TYPE_COLOUR_8BIT ? 3 : 1;
                photometric = samples_per_pixel == 1
                    ? (bits_per_sample == 1 ? Photometric.MINISWHITE : Photometric.MINISBLACK)
                    : Photometric.RGB;
                break;
        }

        Tiff tiff = s.tiff.tiff_file;
        tiff.SetField(TiffTag.COMPRESSION, output_compression);
        if (output_compression == Compression.CCITTFAX3)
            tiff.SetField(TiffTag.GROUP3OPTIONS, output_t4_options);
        else if (output_compression == Compression.CCITTFAX4)
            tiff.SetField(TiffTag.GROUP4OPTIONS, 0);

        tiff.SetField(TiffTag.ORIENTATION, Orientation.TOPLEFT);
        tiff.SetField(TiffTag.BITSPERSAMPLE, bits_per_sample);
        tiff.SetField(TiffTag.SAMPLESPERPIXEL, samples_per_pixel);
        tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
        tiff.SetField(TiffTag.PHOTOMETRIC, photometric);
        tiff.SetField(TiffTag.FILLORDER, FillOrder.LSB2MSB);
        tiff.SetField(TiffTag.IMAGEWIDTH, width);
        tiff.SetField(TiffTag.IMAGELENGTH, length);
        tiff.SetField(TiffTag.ROWSPERSTRIP, length);
        tiff.SetField(TiffTag.XRESOLUTION, MathF.Floor(s.metadata.x_resolution * 0.0254f + 0.5f));
        tiff.SetField(TiffTag.YRESOLUTION, MathF.Floor(s.metadata.y_resolution * 0.0254f + 0.5f));
        tiff.SetField(TiffTag.RESOLUTIONUNIT, ResUnit.INCH);
        tiff.SetField(TiffTag.SOFTWARE, "TKFaxEngine");
        tiff.SetField(TiffTag.DATETIME, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss", global::System.Globalization.CultureInfo.InvariantCulture));
        tiff.SetField(TiffTag.PAGENUMBER, checked((short)s.current_page), (short)0);

        if (!string.IsNullOrEmpty(s.metadata.sub_address))
            tiff.SetField((TiffTag)34909, s.metadata.sub_address);
        if (!string.IsNullOrEmpty(s.metadata.far_ident))
            tiff.SetField(TiffTag.IMAGEDESCRIPTION, s.metadata.far_ident);
        if (!string.IsNullOrEmpty(s.metadata.vendor))
            tiff.SetField(TiffTag.MAKE, s.metadata.vendor);
        if (!string.IsNullOrEmpty(s.metadata.model))
            tiff.SetField(TiffTag.MODEL, s.metadata.model);

        if (s.metadata.compression is T4_COMPRESSION_T4_1D or T4_COMPRESSION_T4_2D) {
            if (s.decoder.t4_t6.bad_rows > 0) {
                tiff.SetField((TiffTag)326, s.decoder.t4_t6.bad_rows);
                tiff.SetField((TiffTag)328, s.decoder.t4_t6.longest_bad_row_run);
                tiff.SetField((TiffTag)327, 1);
            } else {
                tiff.SetField((TiffTag)327, 0);
            }
        }
        return 0;
    }

    private static int open_tiff_output_file(t4_rx_state_t s, string file) {
        s.tiff.tiff_file = Tiff.Open(file, "w");
        return s.tiff.tiff_file is null ? -1 : 0;
    }

    private static int row_read_handler(object? user_data, Span<byte> row) {
        if (user_data is not packer_t packer)
            return 0;
        if (packer.pointer < 0 || packer.pointer > packer.buffer.Length - row.Length)
            return 0;
        packer.buffer.AsSpan(packer.pointer, row.Length).CopyTo(row);
        packer.pointer += row.Length;
        packer.row++;
        return row.Length;
    }

    private static int write_tiff_t85_image(t4_rx_state_t s) {
        if (s.tiff.tiff_file is null)
            return -1;
        var packer = new packer_t { buffer = s.tiff.image_buffer, pointer = 0 };
        using T85EncodeState encoder = T85Encode.Initialize(
            null,
            s.metadata.image_width,
            s.metadata.image_length,
            row_read_handler,
            packer);
        byte[] output = new byte[IMAGE_BUFFER_GROWTH];
        int image_length = 0;
        for (;;) {
            if (output.Length - image_length < IMAGE_BUFFER_GROWTH)
                Array.Resize(ref output, output.Length + IMAGE_BUFFER_GROWTH);
            int len = T85Encode.Get(encoder, output.AsSpan(image_length));
            if (len <= 0)
                break;
            image_length += len;
        }
        return s.tiff.tiff_file.WriteRawStrip(0, output, image_length) < 0 ? -1 : 0;
    }

    private static int write_tiff_t43_image(t4_rx_state_t s) {
        if (s.tiff.tiff_file is null)
            return -1;
        var packer = new packer_t { buffer = s.tiff.image_buffer, pointer = 0 };
        using T43EncodeState encoder = T43.t43_encode_init(
            null,
            s.metadata.image_width,
            s.metadata.image_length,
            row_read_handler,
            packer);
        byte[] output = new byte[IMAGE_BUFFER_GROWTH];
        int image_length = 0;
        for (;;) {
            if (output.Length - image_length < IMAGE_BUFFER_GROWTH)
                Array.Resize(ref output, output.Length + IMAGE_BUFFER_GROWTH);
            int len = T43.t43_encode_get(encoder, output.AsSpan(image_length));
            if (len <= 0)
                break;
            image_length += len;
        }
        return s.tiff.tiff_file.WriteRawStrip(0, output, image_length) < 0 ? -1 : 0;
    }

    private static int write_tiff_image(t4_rx_state_t s) {
        if (s.tiff.tiff_file is null)
            return 0;
        int width = get_current_image_width(s);
        int length = get_current_image_length(s);
        if (width <= 0 || length <= 0)
            return -1;
        if (set_tiff_directory_info(s) < 0)
            return -1;

        if (s.current_decoder == 0) {
            byte[] data = s.decoder.no_decoder.buf ?? Array.Empty<byte>();
            if (s.tiff.tiff_file.WriteRawStrip(0, data, s.decoder.no_decoder.buf_ptr) < 0)
                return -1;
        } else {
            switch (s.tiff.compression) {
                case T4_COMPRESSION_T85:
                case T4_COMPRESSION_T85_L0:
                    if (write_tiff_t85_image(s) < 0)
                        return -1;
                    break;
                case T4_COMPRESSION_T43:
                    if (write_tiff_t43_image(s) < 0)
                        return -1;
                    break;
                default: {
                    int samples = s.tiff.image_type == T4_IMAGE_TYPE_COLOUR_8BIT ? 3 : 1;
                    int bits = s.tiff.image_type == T4_IMAGE_TYPE_BILEVEL ? 1 : 8;
                    int row_bytes = bits == 1 ? (width + 7) / 8 : width * samples;
                    int image_bytes = row_bytes * length;
                    if (s.tiff.image_size < image_bytes)
                        return -1;
                    byte[] row = new byte[row_bytes];
                    for (int y = 0; y < length; y++) {
                        Buffer.BlockCopy(s.tiff.image_buffer, y * row_bytes, row, 0, row_bytes);
                        if (!s.tiff.tiff_file.WriteScanline(row, y))
                            return -1;
                    }
                    break;
                }
            }
        }
        return s.tiff.tiff_file.WriteDirectory() ? 0 : -1;
    }

    private static int close_tiff_output_file(t4_rx_state_t s) {
        if (s.tiff.tiff_file is null)
            return 0;
        if (s.current_page > 1) {
            for (int page = 0; page < s.current_page; page++) {
                if (!s.tiff.tiff_file.SetDirectory(checked((short)page))) {
                    s.logging.Log((int)SpanLogSeverity.Warning, "Failed to set TIFF directory to page %d.\n", page);
                } else {
                    s.tiff.tiff_file.SetField(TiffTag.PAGENUMBER, checked((short)page), checked((short)s.current_page));
                    if (!s.tiff.tiff_file.RewriteDirectory())
                        s.logging.Log((int)SpanLogSeverity.Warning, "Failed to rewrite TIFF directory for page %d.\n", page);
                }
            }
        }
        s.tiff.tiff_file.Dispose();
        s.tiff.tiff_file = null;
        if (s.current_page == 0 && !string.IsNullOrEmpty(s.tiff.file)) {
            try {
                File.Delete(s.tiff.file);
            } catch (IOException) {
            } catch (UnauthorizedAccessException) {
            }
        }
        return 0;
    }

    private static void tiff_rx_release(t4_rx_state_t s) {
        close_tiff_output_file(s);
        s.tiff.image_buffer = Array.Empty<byte>();
        s.tiff.image_size = 0;
        s.tiff.image_buffer_size = 0;
        s.tiff.file = null;
    }

    public static int t4_rx_put_bit(t4_rx_state_t s, int bit) {
        s.line_image_size += 1;
        return t4_t6_decode.t4_t6_decode_put_bit(s.decoder.t4_t6, bit);
    }

    private static void pre_encoded_restart(no_decoder_state_t s) {
        s.buf_ptr = 0;
    }

    private static void pre_encoded_init(no_decoder_state_t s) {
        s.buf = null;
        s.buf_len = 0;
        s.buf_ptr = 0;
    }

    private static int pre_encoded_release(no_decoder_state_t s) {
        s.buf = null;
        s.buf_len = 0;
        s.buf_ptr = 0;
        return 0;
    }

    private static int pre_encoded_put(no_decoder_state_t s, byte[]? data, int len) {
        if (s.buf_len < s.buf_ptr + len) {
            s.buf_len += IMAGE_BUFFER_GROWTH;
            Array.Resize(ref s.buf, s.buf_len);
        }
        if (len != 0)
            Buffer.BlockCopy(data!, 0, s.buf!, s.buf_ptr, len);
        s.buf_ptr += len;
        return T4_DECODE_MORE_DATA;
    }

    private static int image_put(object? user_data, byte[]? buf, int len) {
        if (user_data is not t4_rx_state_t s)
            return T4_DECODE_INVALID_DATA;
        switch (s.current_decoder) {
            case 0:
                return pre_encoded_put(s.decoder.no_decoder, buf, len);
            case T4_COMPRESSION_T4_1D | T4_COMPRESSION_T4_2D | T4_COMPRESSION_T6:
                return t4_t6_decode.t4_t6_decode_put(s.decoder.t4_t6, buf, len);
            case T4_COMPRESSION_T85 | T4_COMPRESSION_T85_L0:
                return s.decoder.t85 is null
                    ? T4_DECODE_INVALID_DATA
                    : T85Decode.Put(s.decoder.t85, len == 0 ? ReadOnlySpan<byte>.Empty : buf!.AsSpan(0, len));
            case T4_COMPRESSION_T42_T81:
                return s.decoder.t42 is null
                    ? T4_DECODE_INVALID_DATA
                    : T42.t42_decode_put(s.decoder.t42, buf ?? Array.Empty<byte>(), len);
            case T4_COMPRESSION_T43:
                return s.decoder.t43 is null
                    ? T4_DECODE_INVALID_DATA
                    : T43.t43_decode_put(s.decoder.t43, buf ?? Array.Empty<byte>(), len);
        }
        return T4_DECODE_OK;
    }

    public static int t4_rx_put(t4_rx_state_t s, byte[]? buf, int len) {
        s.line_image_size += 8 * len;
        if (s.image_put_handler is not null)
            return s.image_put_handler(s, buf, len);
        return T4_DECODE_OK;
    }


    public static void t4_rx_set_y_resolution(t4_rx_state_t s, int resolution) {
        s.metadata.y_resolution = resolution;
    }

    public static void t4_rx_set_x_resolution(t4_rx_state_t s, int resolution) {
        s.metadata.x_resolution = resolution;
    }

    public static void t4_rx_set_dcs(t4_rx_state_t s, string? dcs) {
        s.metadata.dcs = dcs is not null && dcs.Length != 0 ? dcs : null;
    }

    public static void t4_rx_set_sub_address(t4_rx_state_t s, string? sub_address) {
        s.metadata.sub_address = sub_address is not null && sub_address.Length != 0 ? sub_address : null;
    }

    public static void t4_rx_set_far_ident(t4_rx_state_t s, string? ident) {
        s.metadata.far_ident = ident is not null && ident.Length != 0 ? ident : null;
    }

    public static void t4_rx_set_vendor(t4_rx_state_t s, string? vendor) {
        s.metadata.vendor = vendor;
    }

    public static void t4_rx_set_model(t4_rx_state_t s, string? model) {
        s.metadata.model = model;
    }

    private static bool select_tiff_compression(t4_rx_state_t s, int output_image_type) {
        s.tiff.image_type = output_image_type;
        if ((s.metadata.compression & (s.supported_tiff_compressions &
             (T4_COMPRESSION_T85 | T4_COMPRESSION_T85_L0 | T4_COMPRESSION_T42_T81 | T4_COMPRESSION_SYCC_T81))) != 0) {
            s.logging.Log((int)SpanLogSeverity.Flow, "Image can be written without recoding\n");
            s.tiff.compression = s.metadata.compression;
            return false;
        }

        if (output_image_type == T4_IMAGE_TYPE_BILEVEL) {
            if ((s.supported_tiff_compressions & T4_COMPRESSION_T88) != 0)
                s.tiff.compression = T4_COMPRESSION_T88;
            else if ((s.supported_tiff_compressions & T4_COMPRESSION_T85) != 0)
                s.tiff.compression = T4_COMPRESSION_T85;
            else if ((s.supported_tiff_compressions & T4_COMPRESSION_T6) != 0)
                s.tiff.compression = T4_COMPRESSION_T6;
            else if ((s.supported_tiff_compressions & T4_COMPRESSION_T4_2D) != 0)
                s.tiff.compression = T4_COMPRESSION_T4_2D;
            else if ((s.supported_tiff_compressions & T4_COMPRESSION_T4_1D) != 0)
                s.tiff.compression = T4_COMPRESSION_T4_1D;
        } else {
            if ((s.supported_tiff_compressions & T4_COMPRESSION_JPEG) != 0)
                s.tiff.compression = T4_COMPRESSION_JPEG;
            else if ((s.supported_tiff_compressions & T4_COMPRESSION_T42_T81) != 0)
                s.tiff.compression = T4_COMPRESSION_T42_T81;
            else if ((s.supported_tiff_compressions & T4_COMPRESSION_T43) != 0)
                s.tiff.compression = T4_COMPRESSION_T43;
            else if ((s.supported_tiff_compressions & T4_COMPRESSION_T45) != 0)
                s.tiff.compression = T4_COMPRESSION_T45;
            else if ((s.supported_tiff_compressions & T4_COMPRESSION_UNCOMPRESSED) != 0)
                s.tiff.compression = T4_COMPRESSION_UNCOMPRESSED;
        }
        return true;
    }

    private static int release_current_decoder(t4_rx_state_t s) {
        switch (s.current_decoder) {
            case 0:
                return pre_encoded_release(s.decoder.no_decoder);
            case T4_COMPRESSION_T4_1D | T4_COMPRESSION_T4_2D | T4_COMPRESSION_T6:
                return t4_t6_decode.t4_t6_decode_release(s.decoder.t4_t6);
            case T4_COMPRESSION_T85 | T4_COMPRESSION_T85_L0:
                return s.decoder.t85 is null ? 0 : T85Decode.Release(s.decoder.t85);
            case T4_COMPRESSION_T42_T81:
                return s.decoder.t42 is null ? 0 : T42.t42_decode_release(s.decoder.t42);
            case T4_COMPRESSION_T43:
                return s.decoder.t43 is null ? 0 : T43.t43_decode_release(s.decoder.t43);
        }
        return 0;
    }


    public static int t4_rx_set_rx_encoding(t4_rx_state_t s, int compression) {
        switch (compression) {
            case T4_COMPRESSION_T4_1D:
            case T4_COMPRESSION_T4_2D:
            case T4_COMPRESSION_T6:
                switch (s.metadata.compression) {
                    case T4_COMPRESSION_T4_1D:
                    case T4_COMPRESSION_T4_2D:
                    case T4_COMPRESSION_T6:
                        break;
                    default:
                        release_current_decoder(s);
                        t4_t6_decode.t4_t6_decode_init(s.decoder.t4_t6, compression, checked((int)s.metadata.image_width), s.row_handler, s.row_handler_user_data);
                        s.current_decoder = T4_COMPRESSION_T4_1D | T4_COMPRESSION_T4_2D | T4_COMPRESSION_T6;
                        break;
                }
                s.metadata.compression = compression;
                if (!select_tiff_compression(s, T4_IMAGE_TYPE_BILEVEL)) {
                    release_current_decoder(s);
                    s.current_decoder = 0;
                    pre_encoded_init(s.decoder.no_decoder);
                }
                return t4_t6_decode.t4_t6_decode_set_encoding(s.decoder.t4_t6, compression);

            case T4_COMPRESSION_T85:
            case T4_COMPRESSION_T85_L0:
                switch (s.metadata.compression) {
                    case T4_COMPRESSION_T85:
                    case T4_COMPRESSION_T85_L0:
                        break;
                    default:
                        release_current_decoder(s);
                        s.decoder.t85 = T85Decode.Initialize(s.decoder.t85, s.row_handler, s.row_handler_user_data);
                        s.current_decoder = T4_COMPRESSION_T85 | T4_COMPRESSION_T85_L0;
                        T85Decode.SetImageSizeConstraints(s.decoder.t85, T4_WIDTH_1200_A3, 0);
                        break;
                }
                s.metadata.compression = compression;
                if (!select_tiff_compression(s, T4_IMAGE_TYPE_BILEVEL)) {
                    release_current_decoder(s);
                    s.current_decoder = 0;
                    pre_encoded_init(s.decoder.no_decoder);
                }
                return 0;

            case T4_COMPRESSION_T42_T81:
            case T4_COMPRESSION_SYCC_T81:
                switch (s.metadata.compression) {
                    case T4_COMPRESSION_T42_T81:
                    case T4_COMPRESSION_SYCC_T81:
                        break;
                    default:
                        release_current_decoder(s);
                        s.decoder.t42 = T42.t42_decode_init(s.decoder.t42, s.row_handler, s.row_handler_user_data);
                        s.current_decoder = T4_COMPRESSION_T42_T81;
                        T42.t42_decode_set_image_size_constraints(s.decoder.t42, T4_WIDTH_1200_A3, 0);
                        break;
                }
                s.metadata.compression = compression;
                if (!select_tiff_compression(s, T4_IMAGE_TYPE_COLOUR_8BIT)) {
                    release_current_decoder(s);
                    s.current_decoder = 0;
                    pre_encoded_init(s.decoder.no_decoder);
                }
                return 0;

            case T4_COMPRESSION_T43:
                if (s.metadata.compression != T4_COMPRESSION_T43) {
                    release_current_decoder(s);
                    s.decoder.t43 = T43.t43_decode_init(s.decoder.t43, s.row_handler, s.row_handler_user_data);
                    s.current_decoder = T4_COMPRESSION_T43;
                    T43.t43_decode_set_image_size_constraints(s.decoder.t43, T4_WIDTH_1200_A3, 0);
                }
                s.metadata.compression = compression;
                if (!select_tiff_compression(s, T4_IMAGE_TYPE_COLOUR_8BIT)) {
                    release_current_decoder(s);
                    s.current_decoder = 0;
                    pre_encoded_init(s.decoder.no_decoder);
                }
                return 0;
        }
        return -1;
    }

    public static void t4_rx_set_image_width(t4_rx_state_t s, int width) {
        s.metadata.image_width = unchecked((uint)width);
    }

    public static int t4_rx_set_row_write_handler(t4_rx_state_t s, t4_row_write_handler_t? handler, object? user_data) {
        s.row_handler = handler;
        s.row_handler_user_data = user_data;
        switch (s.current_decoder) {
            case T4_COMPRESSION_T4_1D | T4_COMPRESSION_T4_2D | T4_COMPRESSION_T6:
                return t4_t6_decode.t4_t6_decode_set_row_write_handler(s.decoder.t4_t6, handler, user_data);
            case T4_COMPRESSION_T85 | T4_COMPRESSION_T85_L0:
                return s.decoder.t85 is null ? -1 : T85Decode.SetRowWriteHandler(s.decoder.t85, handler, user_data);
            case T4_COMPRESSION_T42_T81:
                return s.decoder.t42 is null ? -1 : T42.t42_decode_set_row_write_handler(s.decoder.t42, handler, user_data);
            case T4_COMPRESSION_T43:
                return s.decoder.t43 is null ? -1 : T43.t43_decode_set_row_write_handler(s.decoder.t43, handler, user_data);
        }
        return -1;
    }

    public static void t4_rx_get_transfer_statistics(t4_rx_state_t s, t4_stats_t t) {
        t.pages_transferred = 0;
        t.pages_in_file = 0;
        t.bad_rows = 0;
        t.longest_bad_row_run = 0;
        t.image_type = 0;
        t.image_x_resolution = 0;
        t.image_y_resolution = 0;
        t.image_width = 0;
        t.image_length = 0;
        t.type = 0;
        t.x_resolution = 0;
        t.y_resolution = 0;
        t.width = 0;
        t.length = 0;
        t.compression = 0;
        t.line_image_size = 0;

        t.pages_transferred = s.current_page;
        t.pages_in_file = s.tiff.pages_in_file;
        t.image_x_resolution = s.metadata.x_resolution;
        t.image_y_resolution = s.metadata.y_resolution;
        t.x_resolution = s.metadata.x_resolution;
        t.y_resolution = s.metadata.y_resolution;
        t.compression = s.metadata.compression;

        switch (s.current_decoder) {
            case 0:
                t.type = 0;
                t.width = checked((int)s.metadata.image_width);
                t.length = checked((int)s.metadata.image_length);
                t.image_type = 0;
                t.image_width = t.width;
                t.image_length = t.length;
                t.line_image_size = s.line_image_size;
                break;
            case T4_COMPRESSION_T4_1D | T4_COMPRESSION_T4_2D | T4_COMPRESSION_T6:
                t.type = T4_IMAGE_TYPE_BILEVEL;
                t.width = checked((int)t4_t6_decode.t4_t6_decode_get_image_width(s.decoder.t4_t6));
                t.length = checked((int)t4_t6_decode.t4_t6_decode_get_image_length(s.decoder.t4_t6));
                t.image_type = t.type;
                t.image_width = t.width;
                t.image_length = t.length;
                t.line_image_size = t4_t6_decode.t4_t6_decode_get_compressed_image_size(s.decoder.t4_t6) / 8;
                t.bad_rows = s.decoder.t4_t6.bad_rows;
                t.longest_bad_row_run = s.decoder.t4_t6.longest_bad_row_run;
                break;
            case T4_COMPRESSION_T85 | T4_COMPRESSION_T85_L0:
                t.type = T4_IMAGE_TYPE_BILEVEL;
                if (s.decoder.t85 is not null) {
                    t.width = checked((int)T85Decode.GetImageWidth(s.decoder.t85));
                    t.length = checked((int)T85Decode.GetImageLength(s.decoder.t85));
                    t.line_image_size = T85Decode.GetCompressedImageSize(s.decoder.t85) / 8;
                }
                t.image_type = t.type;
                t.image_width = t.width;
                t.image_length = t.length;
                break;
            case T4_COMPRESSION_T42_T81:
                t.type = T4_IMAGE_TYPE_COLOUR_8BIT;
                if (s.decoder.t42 is not null) {
                    t.width = checked((int)T42.t42_decode_get_image_width(s.decoder.t42));
                    t.length = checked((int)T42.t42_decode_get_image_length(s.decoder.t42));
                    t.line_image_size = T42.t42_decode_get_compressed_image_size(s.decoder.t42) / 8;
                }
                t.image_type = t.type;
                t.image_width = t.width;
                t.image_length = t.length;
                break;
            case T4_COMPRESSION_T43:
                t.type = T4_IMAGE_TYPE_COLOUR_8BIT;
                if (s.decoder.t43 is not null) {
                    t.width = checked((int)T43.t43_decode_get_image_width(s.decoder.t43));
                    t.length = checked((int)T43.t43_decode_get_image_length(s.decoder.t43));
                    t.line_image_size = T43.t43_decode_get_compressed_image_size(s.decoder.t43) / 8;
                }
                t.image_type = t.type;
                t.image_width = t.width;
                t.image_length = t.length;
                break;
        }
    }

    public static int t4_rx_start_page(t4_rx_state_t s) {
        s.logging.Log((int)SpanLogSeverity.Flow, "Start rx page %d - compression %s\n", s.current_page, t4_compression_to_str(s.metadata.compression));
        switch (s.current_decoder) {
            case 0:
                pre_encoded_restart(s.decoder.no_decoder);
                s.image_put_handler = image_put;
                break;
            case T4_COMPRESSION_T4_1D | T4_COMPRESSION_T4_2D | T4_COMPRESSION_T6:
                t4_t6_decode.t4_t6_decode_restart(s.decoder.t4_t6, checked((int)s.metadata.image_width));
                s.image_put_handler = image_put;
                break;
            case T4_COMPRESSION_T85 | T4_COMPRESSION_T85_L0:
                if (s.decoder.t85 is not null)
                    T85Decode.Restart(s.decoder.t85);
                s.image_put_handler = image_put;
                break;
            case T4_COMPRESSION_T42_T81:
                if (s.decoder.t42 is not null)
                    T42.t42_decode_restart(s.decoder.t42);
                s.image_put_handler = image_put;
                break;
            case T4_COMPRESSION_T43:
                if (s.decoder.t43 is not null)
                    T43.t43_decode_restart(s.decoder.t43);
                s.image_put_handler = image_put;
                break;
        }
        s.line_image_size = 0;
        s.tiff.image_size = 0;
        s.tiff.page_start_time = DateTime.Now;
        return 0;
    }

    private static int tiff_row_write_handler(object? user_data, ReadOnlySpan<byte> buf, int len) {
        if (user_data is not t4_rx_state_t s)
            return -1;
        if (len > 0) {
            if (s.tiff.image_size + len >= s.tiff.image_buffer_size) {
                s.tiff.image_buffer_size += 100 * len;
                Array.Resize(ref s.tiff.image_buffer, s.tiff.image_buffer_size);
            }
            buf[..len].CopyTo(s.tiff.image_buffer.AsSpan(s.tiff.image_size, len));
            s.tiff.image_size += len;
        }
        return 0;
    }

    private static int get_current_image_width(t4_rx_state_t s) {
        return s.current_decoder switch {
            0 => checked((int)s.metadata.image_width),
            T4_COMPRESSION_T4_1D | T4_COMPRESSION_T4_2D | T4_COMPRESSION_T6 => checked((int)t4_t6_decode.t4_t6_decode_get_image_width(s.decoder.t4_t6)),
            T4_COMPRESSION_T85 | T4_COMPRESSION_T85_L0 => s.decoder.t85 is null ? 0 : checked((int)T85Decode.GetImageWidth(s.decoder.t85)),
            T4_COMPRESSION_T42_T81 => s.decoder.t42 is null ? 0 : checked((int)T42.t42_decode_get_image_width(s.decoder.t42)),
            T4_COMPRESSION_T43 => s.decoder.t43 is null ? 0 : checked((int)T43.t43_decode_get_image_width(s.decoder.t43)),
            _ => 0
        };
    }

    private static int get_current_image_length(t4_rx_state_t s) {
        return s.current_decoder switch {
            0 => checked((int)s.metadata.image_length),
            T4_COMPRESSION_T4_1D | T4_COMPRESSION_T4_2D | T4_COMPRESSION_T6 => checked((int)t4_t6_decode.t4_t6_decode_get_image_length(s.decoder.t4_t6)),
            T4_COMPRESSION_T85 | T4_COMPRESSION_T85_L0 => s.decoder.t85 is null ? 0 : checked((int)T85Decode.GetImageLength(s.decoder.t85)),
            T4_COMPRESSION_T42_T81 => s.decoder.t42 is null ? 0 : checked((int)T42.t42_decode_get_image_length(s.decoder.t42)),
            T4_COMPRESSION_T43 => s.decoder.t43 is null ? 0 : checked((int)T43.t43_decode_get_image_length(s.decoder.t43)),
            _ => 0
        };
    }

    public static int t4_rx_end_page(t4_rx_state_t s) {
        int length = 0;
        if (s.image_put_handler is not null)
            s.image_put_handler(s, null, 0);

        switch (s.current_decoder) {
            case 0:
                length = s.decoder.no_decoder.buf_ptr;
                break;
            case T4_COMPRESSION_T4_1D | T4_COMPRESSION_T4_2D | T4_COMPRESSION_T6:
                length = checked((int)t4_t6_decode.t4_t6_decode_get_image_length(s.decoder.t4_t6));
                break;
            case T4_COMPRESSION_T85 | T4_COMPRESSION_T85_L0:
                if (s.decoder.t85 is not null)
                    length = checked((int)T85Decode.GetImageLength(s.decoder.t85));
                break;
            case T4_COMPRESSION_T42_T81:
                if (s.decoder.t42 is not null) {
                    length = checked((int)T42.t42_decode_get_image_length(s.decoder.t42));
                    s.tiff.image_type = s.decoder.t42.samples_per_pixel == 3
                        ? T4_IMAGE_TYPE_COLOUR_8BIT
                        : T4_IMAGE_TYPE_GRAY_8BIT;
                }
                break;
            case T4_COMPRESSION_T43:
                if (s.decoder.t43 is not null)
                    length = checked((int)T43.t43_decode_get_image_length(s.decoder.t43));
                break;
        }
        int width = get_current_image_width(s);
        if (width <= 0 || length <= 0)
            return -1;

        // Persist the geometry decoded for this page before creating its TIFF
        // directory. This prevents dimensions from a previous page being reused.
        s.metadata.image_width = checked((uint)width);
        s.metadata.image_length = checked((uint)length);

        if (s.tiff.tiff_file is not null) {
            if (write_tiff_image(s) == 0)
                s.current_page++;
            s.tiff.image_size = 0;
        } else {
            s.current_page++;
        }
        return 0;
    }

    public static SpanLogState t4_rx_get_logging_state(t4_rx_state_t s) {
        return s.logging;
    }

    public static t4_rx_state_t? t4_rx_init(t4_rx_state_t? s, string? file, int supported_output_compressions) {
        s ??= new t4_rx_state_t();
        s.row_handler = null;
        s.row_handler_user_data = null;
        s.supported_tiff_compressions = 0;
        s.current_page = 0;
        s.line_image_size = 0;
        s.decoder = new t4_rx_decoder_t();
        s.image_put_handler = null;
        s.current_decoder = 0;
        s.metadata = new t4_rx_metadata_t();
        s.tiff = new t4_rx_tiff_state_t();
        s.logging.Initialize((int)SpanLogSeverity.None, null);
        s.logging.SetProtocol("T.4");
        s.logging.Log((int)SpanLogSeverity.Flow, "Start rx document\n");
        s.supported_tiff_compressions = supported_output_compressions;
        s.metadata.x_resolution = T4_X_RESOLUTION_R8;
        s.metadata.y_resolution = T4_Y_RESOLUTION_FINE;
        s.current_page = 0;
        s.current_decoder = 0;
        s.row_handler = tiff_row_write_handler;
        s.row_handler_user_data = s;

        if (file is not null) {
            s.tiff.pages_in_file = 0;
            if (open_tiff_output_file(s, file) < 0)
                return null;
            s.tiff.file = file;
        }
        return s;
    }

    public static int t4_rx_release(t4_rx_state_t s) {
        if (s.tiff.file is not null)
            tiff_rx_release(s);
        release_current_decoder(s);
        return -1;
    }

    public static int t4_rx_free(t4_rx_state_t s) {
        return t4_rx_release(s);
    }
}
