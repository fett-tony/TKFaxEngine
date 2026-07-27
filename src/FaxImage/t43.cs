/*
 * TKFaxEngine - managed C# port
 *
 * t43.cs
 *
 * Direct port of t43.c and t43.h/private/t43.h.
 */

#nullable enable

using System.Buffers.Binary;

namespace TKFaxEngine.FaxImage;

public sealed class T43EncodeState : IDisposable {
    private bool disposed;

    public T85RowReadDelegate? row_read_handler { get; internal set; }
    public object? row_read_user_data { get; internal set; }
    public LabParameters lab { get; } = new();
    public T85EncodeState t85 { get; internal set; } = null!;
    public int image_type { get; internal set; }
    public byte[] bit_planes { get; } = new byte[4];
    public int colour_map_entries { get; internal set; }
    public byte[] colour_map { get; } = new byte[3 * 256];
    public byte[] illuminant_code { get; } = new byte[4];
    public int illuminant_colour_temperature { get; internal set; }
    public uint xd { get; internal set; }
    public uint yd { get; internal set; }
    public int spatial_resolution { get; internal set; }
    public T85Log logging { get; } = new() { Protocol = "T.43" };

    public void Dispose() {
        if (disposed)
            return;
        _ = T43.t43_encode_release(this);
        disposed = true;
    }

    internal void revive() => disposed = false;
    internal void validate() => ObjectDisposedException.ThrowIf(disposed, this);
}

public sealed class T43DecodeState : IDisposable {
    private bool disposed;

    public t4_row_write_handler_t? row_write_handler { get; internal set; }
    public object? row_write_user_data { get; internal set; }
    public LabParameters lab { get; } = new();
    public T85DecodeState t85 { get; internal set; } = null!;
    public int image_type { get; internal set; }
    public byte[] bit_planes { get; } = new byte[4];
    public byte bit_plane_mask { get; internal set; }
    public int current_bit_plane { get; internal set; }
    public int plane_ptr { get; internal set; }
    public int colour_map_entries { get; internal set; }
    public byte[] colour_map { get; } = new byte[3 * 256];
    public byte[] illuminant_code { get; } = new byte[4];
    public int illuminant_colour_temperature { get; internal set; }
    public int spatial_resolution { get; internal set; }
    public int samples_per_pixel { get; internal set; }
    public byte[]? buf { get; internal set; }
    public int ptr { get; internal set; }
    public int row { get; internal set; }
    public T85Log logging { get; } = new() { Protocol = "T.43" };

    public void Dispose() {
        if (disposed)
            return;
        _ = T43.t43_decode_release(this);
        disposed = true;
    }

    internal void revive() => disposed = false;
    internal void validate() => ObjectDisposedException.ThrowIf(disposed, this);
}

public static class T43 {
    public const int T43_IMAGE_TYPE_RGB_BILEVEL = 0;
    public const int T43_IMAGE_TYPE_CMY_BILEVEL = 1;
    public const int T43_IMAGE_TYPE_CMYK_BILEVEL = 2;
    public const int T43_IMAGE_TYPE_8BIT_COLOUR_PALETTE = 16;
    public const int T43_IMAGE_TYPE_12BIT_COLOUR_PALETTE = 17;
    public const int T43_IMAGE_TYPE_GRAY = 32;
    public const int T43_IMAGE_TYPE_COLOUR = 48;

    public static string t43_image_type_to_str(int type) {
        return type switch {
            T43_IMAGE_TYPE_RGB_BILEVEL => "1 bit/colour image (RGB primaries)",
            T43_IMAGE_TYPE_CMY_BILEVEL => "1 bit/colour image (CMY primaries)",
            T43_IMAGE_TYPE_CMYK_BILEVEL => "1 bit/colour image (CMYK primaries)",
            T43_IMAGE_TYPE_8BIT_COLOUR_PALETTE => "Palettized colour image (CIELAB 8 bits/component precision table)",
            T43_IMAGE_TYPE_12BIT_COLOUR_PALETTE => "Palettized colour image (CIELAB 12 bits/component precision table)",
            T43_IMAGE_TYPE_GRAY => "Gray-scale image (using L*)",
            T43_IMAGE_TYPE_COLOUR => "Continuous-tone colour image (CIELAB)",
            _ => "???"
        };
    }

    public static void t43_encode_set_options(T43EncodeState s, uint l0, int mx, int options) {
        s.validate();
        T85Encode.SetOptions(s.t85, l0, mx, options);
    }

    public static int t43_encode_set_image_width(T43EncodeState s, uint image_width) {
        s.validate();
        return T85Encode.SetImageWidth(s.t85, image_width);
    }

    public static int t43_encode_set_image_length(T43EncodeState s, uint image_length) {
        s.validate();
        return T85Encode.SetImageLength(s.t85, image_length);
    }

    public static int t43_encode_set_image_type(T43EncodeState s, int image_type) {
        s.validate();
        _ = image_type;
        return 0;
    }

    public static void t43_encode_abort(T43EncodeState s) {
        s.validate();
    }

    public static void t43_encode_comment(T43EncodeState s, ReadOnlySpan<byte> comment) {
        s.validate();
        T85Encode.Comment(s.t85, comment);
    }

    public static int t43_encode_image_complete(T43EncodeState s) {
        s.validate();
        return 0;
    }

    public static int t43_encode_get(T43EncodeState s, Span<byte> buf) {
        s.validate();
        _ = buf;
        return 0;
    }

    public static int t43_encode_get(T43EncodeState s, byte[] buf, int max_len) {
        ArgumentNullException.ThrowIfNull(buf);
        return t43_encode_get(s, buf.AsSpan(0, Math.Min(max_len, buf.Length)));
    }

    public static uint t43_encode_get_image_width(T43EncodeState s) {
        s.validate();
        return T85Encode.GetImageWidth(s.t85);
    }

    public static uint t43_encode_get_image_length(T43EncodeState s) {
        s.validate();
        return T85Encode.GetImageLength(s.t85);
    }

    public static int t43_encode_get_compressed_image_size(T43EncodeState s) {
        s.validate();
        return 0;
    }

    public static int t43_encode_set_row_read_handler(T43EncodeState s, T85RowReadDelegate? handler, object? user_data) {
        s.validate();
        s.row_read_handler = handler;
        s.row_read_user_data = user_data;
        return 0;
    }

    public static T85Log t43_encode_get_logging_state(T43EncodeState s) {
        s.validate();
        return s.logging;
    }

    public static int t43_encode_restart(T43EncodeState s, uint image_width, uint image_length) {
        s.validate();
        _ = image_width;
        _ = image_length;
        return 0;
    }

    public static T43EncodeState t43_encode_init(T43EncodeState? s, uint image_width, uint image_length, T85RowReadDelegate? handler, object? user_data) {
        s ??= new T43EncodeState();
        s.revive();
        s.row_read_handler = handler;
        s.row_read_user_data = user_data;
        Array.Clear(s.bit_planes);
        Array.Clear(s.colour_map);
        Array.Clear(s.illuminant_code);
        s.colour_map_entries = 0;
        s.illuminant_colour_temperature = 0;
        s.xd = 0;
        s.yd = 0;
        s.spatial_resolution = 0;
        s.logging.Protocol = "T.43";
        s.t85 = T85Encode.Initialize(s.t85, image_width, image_length, handler, user_data);
        s.image_type = T43_IMAGE_TYPE_8BIT_COLOUR_PALETTE;
        return s;
    }

    public static int t43_encode_release(T43EncodeState s) {
        if (s.t85 is not null)
            return T85Encode.Release(s.t85);
        return 0;
    }

    public static int t43_encode_free(T43EncodeState? s) {
        s?.Dispose();
        return 0;
    }

    public static void t43_decode_rx_status(T43DecodeState s, int status) {
        s.validate();
        s.logging.Flow($"Signal status is {status}");
        switch ((T85SignalStatus)status) {
            case T85SignalStatus.TrainingInProgress:
            case T85SignalStatus.TrainingFailed:
            case T85SignalStatus.TrainingSucceeded:
            case T85SignalStatus.CarrierUp:
                break;
            case T85SignalStatus.CarrierDown:
            case T85SignalStatus.EndOfData:
                _ = T85Decode.Put(s.t85, ReadOnlySpan<byte>.Empty);
                break;
            default:
                s.logging.Warning($"Unexpected rx status - {status}!");
                break;
        }
    }

    private static void set_simple_colour_map(T43DecodeState s, int code) {
        int i;
        switch (code) {
            case T43_IMAGE_TYPE_RGB_BILEVEL:
                Array.Clear(s.colour_map);
                s.colour_map[3 * 0x20 + 2] = 0xF0;
                s.colour_map[3 * 0x40 + 1] = 0xF0;
                s.colour_map[3 * 0x60 + 1] = 0xF0;
                s.colour_map[3 * 0x60 + 2] = 0xF0;
                s.colour_map[3 * 0x80] = 0xF0;
                s.colour_map[3 * 0xA0] = 0xF0;
                s.colour_map[3 * 0xA0 + 2] = 0xF0;
                s.colour_map[3 * 0xC0] = 0xF0;
                s.colour_map[3 * 0xC0 + 1] = 0xF0;
                s.colour_map[3 * 0xE0] = 0xF0;
                s.colour_map[3 * 0xE0 + 1] = 0xF0;
                s.colour_map[3 * 0xE0 + 2] = 0xF0;
                s.colour_map_entries = 256;
                break;
            case T43_IMAGE_TYPE_CMY_BILEVEL:
            case T43_IMAGE_TYPE_CMYK_BILEVEL:
                Array.Clear(s.colour_map);
                s.colour_map[0] = 0xF0;
                s.colour_map[1] = 0xF0;
                s.colour_map[2] = 0xF0;
                s.colour_map[3 * 0x20] = 0xF0;
                s.colour_map[3 * 0x20 + 1] = 0xF0;
                s.colour_map[3 * 0x40] = 0xF0;
                s.colour_map[3 * 0x40 + 2] = 0xF0;
                s.colour_map[3 * 0x60] = 0xF0;
                s.colour_map[3 * 0x80 + 1] = 0xF0;
                s.colour_map[3 * 0xA0 + 1] = 0xF0;
                s.colour_map[3 * 0xC0 + 2] = 0xF0;
                s.colour_map_entries = 256;
                break;
            case T43_IMAGE_TYPE_8BIT_COLOUR_PALETTE:
                for (i = 0; i < 3 * 256; i += 3) {
                    s.colour_map[i] = unchecked((byte)i);
                    s.colour_map[i + 1] = unchecked((byte)i);
                    s.colour_map[i + 2] = unchecked((byte)i);
                }
                s.colour_map_entries = 256;
                break;
            case T43_IMAGE_TYPE_12BIT_COLOUR_PALETTE:
                break;
            case T43_IMAGE_TYPE_GRAY:
                for (i = 0; i < 256; i++)
                    s.colour_map[i] = (byte)i;
                s.colour_map_entries = 256;
                break;
            case T43_IMAGE_TYPE_COLOUR:
                break;
        }
    }

    private static int t43_analyse_header(T43DecodeState s, ReadOnlySpan<byte> data) {
        int pos = 0;
        if (data.Length < 2 || BinaryPrimitives.ReadUInt16BigEndian(data) != 0xFFA8)
            return 0;
        s.logging.Flow("Got BCIH (bit-plane colour image header)");
        pos += 2;
        for (;;) {
            if (pos + 2 > data.Length)
                break;
            ushort marker = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(pos, 2));
            if (marker == 0xFFE1) {
                if (pos + 4 > data.Length)
                    break;
                pos += 2;
                int seg = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(pos, 2));
                pos += 2;
                seg -= 2;
                if (seg < 0 || pos + seg > data.Length)
                    break;
                ReadOnlySpan<byte> payload = data.Slice(pos, seg);
                if (seg >= 6 && payload.Slice(0, 5).SequenceEqual("G3FAX"u8)) {
                    if (payload[5] == 0xFF) {
                        s.logging.Flow("Got ECIH (end of colour image header)");
                        if (seg != 6)
                            s.logging.Flow($"Got bad ECIH length - {seg}");
                        pos += seg;
                        break;
                    }
                    switch (payload[5]) {
                        case 0:
                            s.logging.Flow("Got G3FAX0");
                            if (seg < 16) {
                                s.logging.Flow($"Got bad G3FAX0 length - {seg}");
                            } else {
                                int version = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(6, 2));
                                s.spatial_resolution = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(8, 2));
                                int coding = payload[10];
                                s.image_type = payload[11];
                                payload.Slice(12, 4).CopyTo(s.bit_planes);
                                s.samples_per_pixel = s.image_type == T43_IMAGE_TYPE_GRAY ? 1 : s.image_type == T43_IMAGE_TYPE_CMYK_BILEVEL ? 4 : 3;
                                s.logging.Flow($"Version {version}, resolution {s.spatial_resolution}dpi, coding method {coding}, type {t43_image_type_to_str(s.image_type)} ({s.image_type}), bit planes {s.bit_planes[0]},{s.bit_planes[1]},{s.bit_planes[2]},{s.bit_planes[3]}");
                                set_simple_colour_map(s, s.image_type);
                            }
                            break;
                        case 1:
                            s.logging.Flow("Set gamut");
                            if (seg < 18)
                                s.logging.Flow($"Got bad G3FAX1 length - {seg}");
                            else
                                T42T43Local.set_gamut_from_code(s.logging, s.lab, payload.Slice(6, 12));
                            break;
                        case 2:
                            s.logging.Flow("Set illuminant");
                            if (seg < 10)
                                s.logging.Flow($"Got bad G3FAX2 length - {seg}");
                            else
                                s.illuminant_colour_temperature = T42T43Local.set_illuminant_from_code(s.logging, s.lab, payload.Slice(6, 4));
                            break;
                        default:
                            s.logging.Flow($"Got unexpected G3FAX{payload[5]} length - {seg}");
                            break;
                    }
                }
                pos += seg;
            } else if (marker == 0xFFE3) {
                if (pos + 6 > data.Length)
                    break;
                pos += 2;
                long segmentLength = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(pos, 4));
                pos += 4;
                long segLong = segmentLength - 4;
                if (segLong < 0 || segLong > int.MaxValue || pos + segLong > data.Length)
                    break;
                int seg = (int)segLong;
                ReadOnlySpan<byte> payload = data.Slice(pos, seg);
                if (seg >= 6 && payload.Slice(0, 6).SequenceEqual(new byte[] { (byte)'G', (byte)'3', (byte)'F', (byte)'A', (byte)'X', 3 })) {
                    s.logging.Flow("Got G3FAX3");
                    if (seg >= 12) {
                        int table_id = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(6, 2));
                        s.logging.Flow($"  Table ID {table_id,3}");
                        uint entriesRaw = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(8, 4));
                        s.colour_map_entries = unchecked((int)entriesRaw);
                        switch (table_id) {
                            case 0:
                                s.logging.Flow($"  Entries {s.colour_map_entries,6} (len {seg})");
                                if (s.colour_map_entries >= 0 && s.colour_map_entries <= 256 && seg >= 12 + s.colour_map_entries * 3)
                                    T42T43Local.lab_to_srgb(s.lab, s.colour_map.AsSpan(0, 3 * s.colour_map_entries), payload.Slice(12, 3 * s.colour_map_entries), s.colour_map_entries);
                                else
                                    s.logging.Flow($"Got bad G3FAX3 length - {seg}");
                                break;
                            case 4:
                                s.logging.Flow($"  Entries {s.colour_map_entries,6}");
                                if (s.colour_map_entries >= 0 && s.colour_map_entries <= 256 && seg >= 12 + s.colour_map_entries * 6) {
                                    Span<byte> col = stackalloc byte[3];
                                    for (int i = 0; i < s.colour_map_entries; i++) {
                                        col[0] = unchecked((byte)(BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(12 + 6 * i, 2)) >> 4));
                                        col[1] = unchecked((byte)(BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(14 + 6 * i, 2)) >> 4));
                                        col[2] = unchecked((byte)(BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(16 + 6 * i, 2)) >> 4));
                                        T42T43Local.lab_to_srgb(s.lab, s.colour_map.AsSpan(3 * i, 3), col, 1);
                                    }
                                } else {
                                    s.logging.Flow($"Got bad G3FAX3 length - {seg}");
                                }
                                break;
                            default:
                                s.logging.Flow($"Got bad G3FAX3 table ID - {table_id}");
                                break;
                        }
                    }
                }
                pos += seg;
            } else {
                break;
            }
        }
        return pos;
    }

    private static int t85_row_write_handler(object? user_data, ReadOnlySpan<byte> data, int len) {
        data = data[..len];
        T43DecodeState s = (T43DecodeState)user_data!;
        if (s.buf is null) {
            int image_size = checked(s.samples_per_pixel * checked((int)s.t85.ImageWidth) * checked((int)s.t85.ImageLength));
            s.buf = new byte[image_size];
        }
        for (int i = 0; i < data.Length; i++) {
            byte mask = 0x80;
            int limit = s.samples_per_pixel == 1 ? 8 : s.samples_per_pixel * 8;
            for (int j = 0; j < limit; j += s.samples_per_pixel) {
                int index = s.ptr + j;
                if ((data[i] & mask) != 0 && s.buf is not null && (uint)index < (uint)s.buf.Length)
                    s.buf[index] |= s.bit_plane_mask;
                mask >>= 1;
            }
            s.ptr += s.samples_per_pixel * 8;
        }
        s.row++;
        return 0;
    }

    public static int t43_decode_put(T43DecodeState s, ReadOnlySpan<byte> data) {
        s.validate();
        if (s.current_bit_plane < 0) {
            int consumedHeader = t43_analyse_header(s, data);
            data = data.Slice(Math.Min(consumedHeader, data.Length));
            s.bit_plane_mask = 0x80;
            s.current_bit_plane++;
            s.t85.BitPlanes = 1;
            s.ptr = 0;
            s.row = 0;
            s.buf = null;
            s.plane_ptr = 0;
            _ = T85Decode.NewPlane(s.t85);
        }

        int total_len = 0;
        int result = 0;
        while (s.current_bit_plane < s.t85.BitPlanes) {
            result = T85Decode.Put(s.t85, data);
            if (result != T85Constants.T4_DECODE_OK) {
                s.plane_ptr += data.Length;
                return result;
            }
            int plane_len = T85Decode.GetCompressedImageSize(s.t85);
            int consumed = plane_len / 8 - s.plane_ptr;
            if (consumed < 0)
                consumed = 0;
            if (consumed > data.Length)
                consumed = data.Length;
            data = data.Slice(consumed);
            total_len = s.ptr;
            s.bit_plane_mask >>= 1;
            s.ptr = 0;
            s.row = 0;
            s.plane_ptr = 0;
            s.current_bit_plane++;
            _ = T85Decode.NewPlane(s.t85);
        }

        if (s.buf is not null) {
            if (s.samples_per_pixel == 1) {
                for (int j = 0; j < total_len && j < s.buf.Length; j += s.samples_per_pixel)
                    s.buf[j] = s.colour_map[s.buf[j]];
            } else {
                for (int j = 0; j + 2 < total_len && j + 2 < s.buf.Length; j += s.samples_per_pixel) {
                    int i = s.buf[j];
                    s.buf[j] = s.colour_map[3 * i];
                    s.buf[j + 1] = s.colour_map[3 * i + 1];
                    s.buf[j + 2] = s.colour_map[3 * i + 2];
                }
            }
            int rowLength = checked(s.samples_per_pixel * checked((int)s.t85.ImageWidth));
            for (int j = 0; j < s.t85.ImageLength; j++) {
                int offset = checked(j * rowLength);
                if (offset + rowLength <= s.buf.Length)
                    _ = s.row_write_handler?.Invoke(s.row_write_user_data, s.buf.AsSpan(offset, rowLength), rowLength);
            }
        }
        return result;
    }

    public static int t43_decode_put(T43DecodeState s, byte[] data, int len) {
        ArgumentNullException.ThrowIfNull(data);
        return t43_decode_put(s, data.AsSpan(0, Math.Min(len, data.Length)));
    }

    public static int t43_decode_set_row_write_handler(T43DecodeState s, t4_row_write_handler_t? handler, object? user_data) {
        s.validate();
        s.row_write_handler = handler;
        s.row_write_user_data = user_data;
        s.t85.RowWriteHandler = handler;
        s.t85.RowWriteUserData = user_data;
        return 0;
    }

    public static int t43_decode_set_comment_handler(T43DecodeState s, uint max_comment_len, T85RowWriteDelegate? handler, object? user_data) {
        s.validate();
        return T85Decode.SetCommentHandler(s.t85, max_comment_len, handler, user_data);
    }

    public static int t43_decode_set_image_size_constraints(T43DecodeState s, uint max_xd, uint max_yd) {
        s.validate();
        return T85Decode.SetImageSizeConstraints(s.t85, max_xd, max_yd);
    }

    public static uint t43_decode_get_image_width(T43DecodeState s) {
        s.validate();
        return T85Decode.GetImageWidth(s.t85);
    }

    public static uint t43_decode_get_image_length(T43DecodeState s) {
        s.validate();
        return T85Decode.GetImageLength(s.t85);
    }

    public static int t43_decode_get_compressed_image_size(T43DecodeState s) {
        s.validate();
        return T85Decode.GetCompressedImageSize(s.t85);
    }

    public static T85Log t43_decode_get_logging_state(T43DecodeState s) {
        s.validate();
        return s.logging;
    }

    public static int t43_decode_restart(T43DecodeState s) {
        s.validate();
        T42T43Local.set_lab_illuminant(s.lab, 100.0f, 100.0f, 100.0f);
        T42T43Local.set_lab_gamut(s.lab, 0, 100, -85, 85, -75, 125, 0);
        s.t85.MinimumBitPlanes = 1;
        s.t85.MaximumBitPlanes = 8;
        s.bit_plane_mask = 0x80;
        s.current_bit_plane = -1;
        s.image_type = T43_IMAGE_TYPE_8BIT_COLOUR_PALETTE;
        return T85Decode.Restart(s.t85);
    }

    public static T43DecodeState t43_decode_init(T43DecodeState? s, t4_row_write_handler_t? handler, object? user_data) {
        s ??= new T43DecodeState();
        s.revive();
        s.logging.Protocol = "T.43";
        s.row_write_handler = handler;
        s.row_write_user_data = user_data;
        Array.Clear(s.bit_planes);
        Array.Clear(s.colour_map);
        Array.Clear(s.illuminant_code);
        s.colour_map_entries = 0;
        s.illuminant_colour_temperature = 0;
        s.spatial_resolution = 0;
        s.samples_per_pixel = 0;
        s.buf = null;
        s.ptr = 0;
        s.row = 0;
        s.plane_ptr = 0;
        s.t85 = T85Decode.Initialize(s.t85, t85_row_write_handler, s);
        T42T43Local.set_lab_illuminant(s.lab, 100.0f, 100.0f, 100.0f);
        T42T43Local.set_lab_gamut(s.lab, 0, 100, -85, 85, -75, 125, 0);
        s.t85.MinimumBitPlanes = 1;
        s.t85.MaximumBitPlanes = 8;
        s.bit_plane_mask = 0x80;
        s.current_bit_plane = -1;
        s.image_type = T43_IMAGE_TYPE_8BIT_COLOUR_PALETTE;
        return s;
    }

    public static int t43_decode_release(T43DecodeState s) {
        if (s.t85 is not null)
            return T85Decode.Release(s.t85);
        return 0;
    }

    public static int t43_decode_free(T43DecodeState? s) {
        s?.Dispose();
        return 0;
    }
}
