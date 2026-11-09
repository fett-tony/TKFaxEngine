/*
 * TKFaxEngine - managed C# port
 *
 * t42.cs
 *
 * Direct port of t42.c and t42.h/private/t42.h.
 * The JPEG calls are mapped to the bundled libjpeg-turbo binding.
 */

#nullable enable

using System.Buffers.Binary;
using TKFaxEngine;

namespace TKFaxEngine.FaxImage;

public sealed class T42EncodeState : IDisposable {
    private bool disposed;
    public T85RowReadDelegate? row_read_handler { get; internal set; }
    public object? row_read_user_data { get; internal set; }
    public uint image_width { get; internal set; }
    public uint image_length { get; internal set; }
    public ushort samples_per_pixel { get; internal set; }
    public int image_type { get; internal set; }
    public int no_subsampling { get; internal set; }
    public int itu_ycc { get; internal set; }
    public int quality { get; internal set; }
    public int spatial_resolution { get; internal set; }
    public LabParameters lab { get; } = new();
    public byte[] illuminant_code { get; } = new byte[4];
    public int illuminant_colour_temperature { get; internal set; }
    public int compressed_image_size { get; internal set; }
    public int compressed_image_ptr { get; internal set; }
    public byte[] compressed_buf { get; internal set; } = [];
    public T85Log logging { get; } = new() { Protocol = "T.42" };

    public void Dispose() {
        if (disposed)
            return;
        _ = T42.t42_encode_release(this);
        disposed = true;
    }

    internal void revive() => disposed = false;
    internal void validate() => ObjectDisposedException.ThrowIf(disposed, this);
}

public sealed class T42DecodeState : IDisposable {
    private bool disposed;

    public t4_row_write_handler_t? row_write_handler { get; internal set; }
    public object? row_write_user_data { get; internal set; }
    public T85RowWriteDelegate? comment_handler { get; internal set; }
    public object? comment_user_data { get; internal set; }
    public uint max_comment_len { get; internal set; }
    public uint image_width { get; internal set; }
    public uint image_length { get; internal set; }
    public ushort samples_per_pixel { get; internal set; }
    public int image_type { get; internal set; }
    public int itu_ycc { get; internal set; }
    public int spatial_resolution { get; internal set; }
    public LabParameters lab { get; } = new();
    public byte[] illuminant_code { get; } = new byte[4];
    public int illuminant_colour_temperature { get; internal set; }
    public int compressed_image_size { get; internal set; }
    public List<byte> compressed_buf { get; } = [];
    public bool end_of_data { get; internal set; }
    public T85Log logging { get; } = new() { Protocol = "T.42" };

    public void Dispose() {
        if (disposed)
            return;
        _ = T42.t42_decode_release(this);
        disposed = true;
    }

    internal void revive() => disposed = false;
    internal void validate() => ObjectDisposedException.ThrowIf(disposed, this);
}

public static class T42 {
    private const int T4_IMAGE_TYPE_COLOUR_8BIT = 5;

    public static bool t42_analyse_header(out uint width, out uint length, ReadOnlySpan<byte> data) {
        length = 0;
        width = 0;
        int pos = 0;
        if (data.Length < 2 || BinaryPrimitives.ReadUInt16BigEndian(data) != 0xFFD8)
            return false;
        pos += 2;
        while (pos < data.Length) {
            if (pos + 4 > data.Length)
                return false;
            int type = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(pos, 2));
            pos += 2;
            int seg = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(pos, 2)) - 2;
            pos += 2;
            if (seg < 0 || pos + seg > data.Length)
                return false;
            if (type == 0xFFC0) {
                if (seg < 5)
                    return false;
                length = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(pos + 1, 2));
                width = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(pos + 3, 2));
                return true;
            }
            pos += seg;
        }
        return false;
    }

    public static void t42_encode_set_options(T42EncodeState s, uint l0, int quality, int options) {
        s.validate();
        _ = l0;
        s.quality = quality;
        s.no_subsampling = options & 1;
    }

    public static int t42_encode_set_image_width(T42EncodeState s, uint image_width) {
        s.validate();
        s.image_width = image_width;
        return 0;
    }

    public static int t42_encode_set_image_length(T42EncodeState s, uint image_length) {
        s.validate();
        s.image_length = image_length;
        return 0;
    }

    public static int t42_encode_set_image_type(T42EncodeState s, int image_type) {
        s.validate();
        s.image_type = image_type;
        return 0;
    }

    public static void t42_encode_abort(T42EncodeState s) {
        s.validate();
    }

    public static void t42_encode_comment(T42EncodeState s, ReadOnlySpan<byte> comment) {
        s.validate();
        _ = comment;
    }

    public static int t42_encode_image_complete(T42EncodeState s) {
        s.validate();
        return 0;
    }

    public static int t42_encode_get(T42EncodeState s, Span<byte> buf) {
        s.validate();
        if (s.compressed_image_size == 0) {
            if (t42_srgb_to_itulab_jpeg(s) < 0)
                return -1;
        }
        int len = Math.Min(buf.Length, s.compressed_image_size - s.compressed_image_ptr);
        if (len > 0) {
            s.compressed_buf.AsSpan(s.compressed_image_ptr, len).CopyTo(buf);
            s.compressed_image_ptr += len;
        }
        return len;
    }

    public static int t42_encode_get(T42EncodeState s, byte[] buf, int max_len) {
        ArgumentNullException.ThrowIfNull(buf);
        return t42_encode_get(s, buf.AsSpan(0, Math.Min(max_len, buf.Length)));
    }

    public static uint t42_encode_get_image_width(T42EncodeState s) {
        s.validate();
        return s.image_width;
    }

    public static uint t42_encode_get_image_length(T42EncodeState s) {
        s.validate();
        return s.image_length;
    }

    public static int t42_encode_get_compressed_image_size(T42EncodeState s) {
        s.validate();
        return s.compressed_image_size;
    }

    public static int t42_encode_set_row_read_handler(T42EncodeState s, T85RowReadDelegate? handler, object? user_data) {
        s.validate();
        s.row_read_handler = handler;
        s.row_read_user_data = user_data;
        return 0;
    }

    public static T85Log t42_encode_get_logging_state(T42EncodeState s) {
        s.validate();
        return s.logging;
    }

    public static int t42_encode_restart(T42EncodeState s, uint image_width, uint image_length) {
        s.validate();
        s.image_width = image_width;
        s.image_length = image_length;
        if (s.itu_ycc != 0) {
            T42T43Local.set_lab_illuminant(s.lab, 100.0f, 100.0f, 100.0f);
            T42T43Local.set_lab_gamut(s.lab, 0, 100, -127, 127, -127, 127, 0);
        } else {
            T42T43Local.set_lab_illuminant(s.lab, 100.0f, 100.0f, 100.0f);
            T42T43Local.set_lab_gamut(s.lab, 0, 100, -85, 85, -75, 125, 0);
        }
        s.compressed_image_size = 0;
        s.compressed_image_ptr = 0;
        s.compressed_buf = [];
        s.spatial_resolution = 200;
        return 0;
    }

    public static T42EncodeState t42_encode_init(T42EncodeState? s, uint image_width, uint image_length, T85RowReadDelegate? handler, object? user_data) {
        s ??= new T42EncodeState();
        s.revive();
        s.row_read_handler = handler;
        s.row_read_user_data = user_data;
        s.image_width = 0;
        s.image_length = 0;
        s.samples_per_pixel = 0;
        s.image_type = T4_IMAGE_TYPE_COLOUR_8BIT;
        s.no_subsampling = 0;
        s.itu_ycc = 0;
        s.quality = 90;
        s.spatial_resolution = 0;
        Array.Clear(s.illuminant_code);
        s.illuminant_colour_temperature = 0;
        s.compressed_image_size = 0;
        s.compressed_image_ptr = 0;
        s.compressed_buf = [];
        s.logging.Protocol = "T.42";
        _ = t42_encode_restart(s, image_width, image_length);
        return s;
    }

    public static int t42_encode_release(T42EncodeState s) {
        _ = s;
        return 0;
    }

    public static int t42_encode_free(T42EncodeState? s) {
        s?.Dispose();
        return 0;
    }

    public static void t42_decode_rx_status(T42DecodeState s, int status) {
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
                if (!s.end_of_data) {
                    if (t42_itulab_jpeg_to_srgb(s) != 0)
                        s.logging.Flow("Failed to convert from ITULAB.");
                    s.end_of_data = true;
                }
                break;
            default:
                s.logging.Warning($"Unexpected rx status - {status}!");
                break;
        }
    }

    public static int t42_decode_put(T42DecodeState s, ReadOnlySpan<byte> data) {
        s.validate();
        if (data.IsEmpty) {
            if (!s.end_of_data) {
                if (t42_itulab_jpeg_to_srgb(s) != 0)
                    s.logging.Flow("Failed to convert from ITULAB.");
                s.end_of_data = true;
            }
            return T85Constants.T4_DECODE_OK;
        }
        foreach (byte value in data)
            s.compressed_buf.Add(value);
        s.compressed_image_size += data.Length;
        return 0;
    }

    public static int t42_decode_put(T42DecodeState s, byte[] data, int len) {
        ArgumentNullException.ThrowIfNull(data);
        return t42_decode_put(s, data.AsSpan(0, Math.Min(len, data.Length)));
    }

    public static int t42_decode_set_row_write_handler(T42DecodeState s, t4_row_write_handler_t? handler, object? user_data) {
        s.validate();
        s.row_write_handler = handler;
        s.row_write_user_data = user_data;
        return 0;
    }

    public static int t42_decode_set_comment_handler(T42DecodeState s, uint max_comment_len, T85RowWriteDelegate? handler, object? user_data) {
        s.validate();
        s.max_comment_len = max_comment_len;
        s.comment_handler = handler;
        s.comment_user_data = user_data;
        return 0;
    }

    public static int t42_decode_set_image_size_constraints(T42DecodeState s, uint max_xd, uint max_yd) {
        s.validate();
        _ = max_xd;
        _ = max_yd;
        return 0;
    }

    public static uint t42_decode_get_image_width(T42DecodeState s) {
        s.validate();
        return s.image_width;
    }

    public static uint t42_decode_get_image_length(T42DecodeState s) {
        s.validate();
        return s.image_length;
    }

    public static int t42_decode_get_compressed_image_size(T42DecodeState s) {
        s.validate();
        return s.compressed_image_size;
    }

    public static T85Log t42_decode_get_logging_state(T42DecodeState s) {
        s.validate();
        return s.logging;
    }

    public static int t42_decode_restart(T42DecodeState s) {
        s.validate();
        if (s.itu_ycc != 0) {
            T42T43Local.set_lab_illuminant(s.lab, 100.0f, 100.0f, 100.0f);
            T42T43Local.set_lab_gamut(s.lab, 0, 100, -127, 127, -127, 127, 0);
        } else {
            T42T43Local.set_lab_illuminant(s.lab, 100.0f, 100.0f, 100.0f);
            T42T43Local.set_lab_gamut(s.lab, 0, 100, -85, 85, -75, 125, 0);
        }
        s.end_of_data = false;
        s.compressed_image_size = 0;
        s.compressed_buf.Clear();
        return 0;
    }

    public static T42DecodeState t42_decode_init(T42DecodeState? s, t4_row_write_handler_t? handler, object? user_data) {
        s ??= new T42DecodeState();
        s.revive();
        s.row_write_handler = handler;
        s.row_write_user_data = user_data;
        s.comment_handler = null;
        s.comment_user_data = null;
        s.max_comment_len = 0;
        s.image_width = 0;
        s.image_length = 0;
        s.samples_per_pixel = 0;
        s.image_type = 0;
        s.itu_ycc = 0;
        s.spatial_resolution = 0;
        Array.Clear(s.illuminant_code);
        s.illuminant_colour_temperature = 0;
        s.compressed_image_size = 0;
        s.compressed_buf.Clear();
        s.end_of_data = false;
        s.logging.Protocol = "T.42";
        _ = t42_decode_restart(s);
        return s;
    }

    public static int t42_decode_release(T42DecodeState s) {
        _ = s;
        return 0;
    }

    public static int t42_decode_free(T42DecodeState? s) {
        s?.Dispose();
        return 0;
    }

    private static int t42_srgb_to_itulab_jpeg(T42EncodeState s) {
        try {
            int width = checked((int)s.image_width);
            int height = checked((int)s.image_length);
            int components = s.image_type == T4_IMAGE_TYPE_COLOUR_8BIT ? 3 : 1;
            s.samples_per_pixel = (ushort)components;
            int rowBytes = checked(width * components);
            byte[] pixels = new byte[checked(rowBytes * height)];
            byte[] input = new byte[rowBytes];
            byte[] output = components == 3 ? new byte[rowBytes] : input;
            for (int row = 0; row < height; row++) {
                _ = s.row_read_handler!(s.row_read_user_data, input);
                if (components == 3) {
                    T42T43Local.srgb_to_lab(s.lab, output, input, width);
                    output.CopyTo(pixels, row * rowBytes);
                } else {
                    input.CopyTo(pixels, row * rowBytes);
                }
            }

            byte[] jpeg = TurboJpeg.EncodeYuvComponents(width, height, components, pixels, 75, s.no_subsampling != 0);
            s.compressed_buf = add_itu_fax_markers(s, jpeg);
            s.compressed_image_size = s.compressed_buf.Length;
            s.compressed_image_ptr = 0;
            return 0;
        } catch (Exception ex) {
            s.logging.Warning(ex.Message);
            s.compressed_buf = [];
            s.compressed_image_size = 0;
            s.compressed_image_ptr = 0;
            return -1;
        }
    }

    private static byte[] add_itu_fax_markers(T42EncodeState s, byte[] jpeg) {
        if (jpeg.Length < 2 || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
            return jpeg;
        int insertion = 2;
        while (insertion + 4 <= jpeg.Length && jpeg[insertion] == 0xFF && jpeg[insertion + 1] == 0xE0) {
            int segmentLength = BinaryPrimitives.ReadUInt16BigEndian(jpeg.AsSpan(insertion + 2, 2));
            if (segmentLength < 2 || insertion + 2 + segmentLength > jpeg.Length)
                break;
            insertion += 2 + segmentLength;
        }
        List<byte> result = new(jpeg.Length + 80);
        result.AddRange(jpeg.AsSpan(0, insertion).ToArray());
        Span<byte> payload0 = stackalloc byte[10];
        "G3FAX"u8.CopyTo(payload0);
        payload0[5] = 0;
        BinaryPrimitives.WriteUInt16BigEndian(payload0.Slice(6, 2), 1994);
        BinaryPrimitives.WriteUInt16BigEndian(payload0.Slice(8, 2), unchecked((ushort)s.spatial_resolution));
        append_app1(result, payload0);

        if (s.lab.offset_L != 0 || s.lab.range_L != 100 || s.lab.offset_a != 128 || s.lab.range_a != 170 || s.lab.offset_b != 96 || s.lab.range_b != 200) {
            s.logging.Flow("Putting G3FAX1");
            T42T43Local.get_lab_gamut2(s.lab, out int L_P, out int L_Q, out int a_P, out int a_Q, out int b_P, out int b_Q);
            Span<byte> payload1 = stackalloc byte[18];
            "G3FAX"u8.CopyTo(payload1);
            payload1[5] = 1;
            BinaryPrimitives.WriteUInt16BigEndian(payload1.Slice(6, 2), unchecked((ushort)L_P));
            BinaryPrimitives.WriteUInt16BigEndian(payload1.Slice(8, 2), unchecked((ushort)L_Q));
            BinaryPrimitives.WriteUInt16BigEndian(payload1.Slice(10, 2), unchecked((ushort)a_P));
            BinaryPrimitives.WriteUInt16BigEndian(payload1.Slice(12, 2), unchecked((ushort)a_Q));
            BinaryPrimitives.WriteUInt16BigEndian(payload1.Slice(14, 2), unchecked((ushort)b_P));
            BinaryPrimitives.WriteUInt16BigEndian(payload1.Slice(16, 2), unchecked((ushort)b_Q));
            append_app1(result, payload1);
        }

        bool hasCode = s.illuminant_code[0] != 0 || s.illuminant_code[1] != 0 || s.illuminant_code[2] != 0 || s.illuminant_code[3] != 0;
        if (hasCode || s.illuminant_colour_temperature > 0) {
            s.logging.Flow("Putting G3FAX2");
            Span<byte> payload2 = stackalloc byte[10];
            "G3FAX"u8.CopyTo(payload2);
            payload2[5] = 2;
            if (hasCode) {
                s.illuminant_code.CopyTo(payload2.Slice(6, 4));
            } else {
                payload2[6] = (byte)'C';
                payload2[7] = (byte)'T';
                BinaryPrimitives.WriteUInt16BigEndian(payload2.Slice(8, 2), unchecked((ushort)s.illuminant_colour_temperature));
            }
            append_app1(result, payload2);
        }

        result.AddRange(jpeg.AsSpan(insertion).ToArray());
        return result.ToArray();
    }

    private static void append_app1(List<byte> result, ReadOnlySpan<byte> payload) {
        result.Add(0xFF);
        result.Add(0xE1);
        int length = payload.Length + 2;
        result.Add((byte)(length >> 8));
        result.Add((byte)length);
        foreach (byte value in payload)
            result.Add(value);
    }

    private static int t42_itulab_jpeg_to_srgb(T42DecodeState s) {
        try {
            byte[] jpeg = s.compressed_buf.ToArray();
            if (!is_itu_fax(s, jpeg)) {
                s.logging.Flow("Is not an ITU FAX.");
                return -1;
            }
            byte[] input = TurboJpeg.DecodeYuvComponents(jpeg, out int width, out int height, out int components);
            s.image_width = checked((uint)width);
            s.image_length = checked((uint)height);
            s.samples_per_pixel = checked((ushort)components);
            int rowBytes = checked(width * components);
            byte[] output = components == 3 ? new byte[rowBytes] : [];
            for (int row = 0; row < height; row++) {
                ReadOnlySpan<byte> rowInput = input.AsSpan(row * rowBytes, rowBytes);
                if (components == 3) {
                    T42T43Local.lab_to_srgb(s.lab, output, rowInput, width);
                    _ = s.row_write_handler!(s.row_write_user_data, output, output.Length);
                } else {
                    _ = s.row_write_handler!(s.row_write_user_data, rowInput, rowInput.Length);
                }
            }
            return 0;
        } catch (Exception ex) {
            s.logging.Warning(ex.Message);
            return -1;
        }
    }

    private static bool is_itu_fax(T42DecodeState s, ReadOnlySpan<byte> jpeg) {
        bool ok = false;
        if (jpeg.Length < 2 || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
            return false;
        int pos = 2;
        while (pos + 1 < jpeg.Length) {
            if (jpeg[pos] != 0xFF) {
                pos++;
                continue;
            }
            while (pos < jpeg.Length && jpeg[pos] == 0xFF)
                pos++;
            if (pos >= jpeg.Length)
                break;
            byte marker = jpeg[pos++];
            if (marker == 0xDA || marker == 0xD9)
                break;
            if (marker is >= 0xD0 and <= 0xD7 || marker == 0x01)
                continue;
            if (pos + 2 > jpeg.Length)
                return false;
            int length = BinaryPrimitives.ReadUInt16BigEndian(jpeg.Slice(pos, 2));
            pos += 2;
            if (length < 2 || pos + length - 2 > jpeg.Length)
                return false;
            ReadOnlySpan<byte> payload = jpeg.Slice(pos, length - 2);
            pos += length - 2;
            if (marker != 0xE1)
                continue;
            if (payload.Length < 6)
                return false;
            if (!payload.Slice(0, 5).SequenceEqual("G3FAX"u8))
                return false;
            switch (payload[5]) {
                case 0:
                    if (payload.Length < 10) {
                        s.logging.Flow($"Got bad G3FAX0 length - {payload.Length}");
                        return false;
                    }
                    int version = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(6, 2));
                    s.spatial_resolution = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(8, 2));
                    s.logging.Flow($"Version {version}, resolution {s.spatial_resolution}dpi");
                    ok = true;
                    break;
                case 1:
                    s.logging.Flow("Set gamut");
                    if (payload.Length < 18) {
                        s.logging.Flow($"Got bad G3FAX1 length - {payload.Length}");
                        return false;
                    }
                    T42T43Local.set_gamut_from_code(s.logging, s.lab, payload.Slice(6, 12));
                    break;
                case 2:
                    s.logging.Flow("Set illuminant");
                    if (payload.Length < 10) {
                        s.logging.Flow($"Got bad G3FAX2 length - {payload.Length}");
                        return false;
                    }
                    s.illuminant_colour_temperature = T42T43Local.set_illuminant_from_code(s.logging, s.lab, payload.Slice(6, 4));
                    break;
                default:
                    s.logging.Flow($"Got unexpected G3FAX{payload[5]} length - {payload.Length}");
                    return false;
            }
        }
        return ok;
    }
}
