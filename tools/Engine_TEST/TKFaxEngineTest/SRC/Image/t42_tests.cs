/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * t42_tests.cs - Managed C# port of the spanDSP ITU-T T.42 test.
 * The test reads TIFF through the managed BitMiracle.LibTiff.NET API,
 * runs a streaming T.42 encode/decode cycle, and writes the decoded RGB image.
 *
 * Original test written by Steve Underwood.
 * Copyright (C) 2011 Steve Underwood.
 * Licensed under the GNU General Public License version 2.
 */

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using BitMiracle.LibTiff.Classic;
using System.Text;
using TKFaxEngine.FaxImage;
using static TKFaxEngineTest.Image.Test.ImageTranslateTests;

namespace TKFaxEngineTest.Image.Test;

public static class t42_tests
{
    private const string InFileName = "Test-Files/itu/t24/F21B400.TIF";
    private const string OutFileName = "t42_tests_receive.tif";

    public static int tkfx_t42_tests_body_main(string[] testArguments)
    {
        string sourceFile = testArguments.Length > 0 ? testArguments[0] : InFileName;
        Console.WriteLine("Demo of ITU/Lab library.");

        try
        {
            ManagedTiffPage metadata = ManagedTiff.ReadMetadata(sourceFile, 0);
            Console.WriteLine($"Width {metadata.Width}, height {metadata.Height}, bits {metadata.BitsPerSample}, samples {metadata.SamplesPerPixel}");
            Console.Write("Compression is ");
            switch (metadata.Compression)
            {
                case ManagedTiff.CompressionT4:
                    Console.WriteLine("T.4");
                    return 0;
                case ManagedTiff.CompressionT6:
                    Console.WriteLine("T.6");
                    return 0;
                case ManagedTiff.CompressionT85:
                    Console.WriteLine("T.85");
                    break;
                case ManagedTiff.CompressionT43:
                    Console.WriteLine("T.43");
                    break;
                case ManagedTiff.CompressionJpeg:
                    Console.Write("JPEG");
                    if (metadata.Photometric == ManagedTiff.PhotometricItuLab)
                        Console.Write(" ITULAB");
                    Console.WriteLine();
                    break;
                case ManagedTiff.CompressionNone:
                    Console.WriteLine("No compression");
                    break;
                default:
                    Console.WriteLine($"Unexpected compression {metadata.Compression}");
                    break;
            }

            ManagedTiffPage source = ManagedTiff.ReadRgbaPage(sourceFile, 0);
            byte[] encoded = EncodeT42(source);
            Console.WriteLine($"Compressed image is {encoded.Length} bytes");

            ManagedTiffPage decoded = DecodeT42(encoded, source);
            Console.WriteLine($"Decoded image is {decoded.Width} by {decoded.Height}, {decoded.Rgb.Length} bytes");

            ManagedTiff.WriteRgbPages(OutFileName, new[] { decoded });
            Console.WriteLine($"Mean absolute RGB error: {CalculateMeanAbsoluteError(source.Rgb, decoded.Rgb):F3}");
            Console.WriteLine("Done!");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static byte[] EncodeT42(ManagedTiffPage source)
    {
        RowReader reader = new(source.Rgb, checked((int)source.Width * 3));
        using T42EncodeState encoder = T42.t42_encode_init(
            null,
            source.Width,
            source.Height,
            ReadRow,
            reader);

        if (T42.t42_encode_set_image_type(encoder, (int)t4_image_types_t.T4_IMAGE_TYPE_COLOUR_8BIT) < 0)
            throw new InvalidOperationException("Unable to select the T.42 colour image type.");

        T42.t42_encode_set_options(encoder, 0, 90, 1);
        T42.t42_encode_comment(encoder, Encoding.ASCII.GetBytes("TKFaxEngine t42_tests managed port"));

        return DrainEncoder(destination => T42.t42_encode_get(encoder, destination));
    }

    private static ManagedTiffPage DecodeT42(byte[] encoded, ManagedTiffPage source)
    {
        RowWriter writer = new();
        using T42DecodeState decoder = T42.t42_decode_init(null, WriteRow, writer);
        T42.t42_decode_set_comment_handler(decoder, 1000, CommentHandler, null);
        T42.t42_decode_set_image_size_constraints(decoder, source.Width, source.Height);

        FeedStreaming(encoded, chunk => T42.t42_decode_put(decoder, chunk));
        int result = T42.t42_decode_put(decoder, ReadOnlySpan<byte>.Empty);
        if (result != T85Constants.T4_DECODE_OK)
            throw new InvalidDataException($"T.42 decoder returned {result}.");

        uint width = T42.t42_decode_get_image_width(decoder);
        uint height = T42.t42_decode_get_image_length(decoder);
        int samples = decoder.samples_per_pixel;
        if (samples != 3)
            throw new InvalidDataException($"Expected three decoded samples per pixel, got {samples}.");

        byte[] rgb = writer.ToArray();
        int expected = checked((int)(width * height * 3));
        if (rgb.Length != expected)
            throw new InvalidDataException($"Decoded RGB size mismatch: {rgb.Length} != {expected}.");

        return source.WithPixels(width, height, rgb, 8, 3, ManagedTiff.CompressionNone, ManagedTiff.PhotometricRgb);
    }

    internal static byte[] DrainEncoder(SpanEncoderGet get)
    {
        using MemoryStream output = new();
        byte[] buffer = new byte[50_000];

        while (true)
        {
            int length = get(buffer);
            if (length < 0)
                throw new InvalidDataException($"Encoder returned {length}.");
            if (length == 0)
                break;
            output.Write(buffer, 0, length);
        }

        return output.ToArray();
    }

    internal static void FeedStreaming(byte[] data, SpanDecoderPut put)
    {
        const int chunkSize = 4096;
        int offset = 0;

        while (offset < data.Length)
        {
            int length = Math.Min(chunkSize, data.Length - offset);
            int result = put(data.AsSpan(offset, length));
            if (result < T85Constants.T4_DECODE_MORE_DATA)
                throw new InvalidDataException($"Decoder rejected stream data with status {result}.");
            offset += length;
        }
    }

    private static int ReadRow(object? userData, Span<byte> destination)
    {
        return ((RowReader)userData!).Read(destination);
    }

    private static int WriteRow(object? userData, ReadOnlySpan<byte> data, int len)
    {
        ((RowWriter)userData!).Write(data[..len]);
        return 0;
    }

    private static int CommentHandler(object? userData, ReadOnlySpan<byte> data)
    {
        _ = userData;
        Console.WriteLine(data.IsEmpty
            ? "Comment (0): ---"
            : $"Comment ({data.Length}): {Encoding.UTF8.GetString(data)}");
        return 0;
    }

    private static double CalculateMeanAbsoluteError(byte[] original, byte[] decoded)
    {
        int length = Math.Min(original.Length, decoded.Length);
        if (length == 0)
            return 0.0;

        long total = 0;
        for (int i = 0; i < length; i++)
            total += Math.Abs(original[i] - decoded[i]);
        return (double)total / length;
    }

    internal delegate int SpanEncoderGet(Span<byte> destination);
    internal delegate int SpanDecoderPut(ReadOnlySpan<byte> source);

    internal sealed class RowReader
    {
        private readonly byte[] _data;
        private readonly int _rowLength;
        private int _offset;

        public RowReader(byte[] data, int rowLength)
        {
            _data = data;
            _rowLength = rowLength;
        }

        public int Read(Span<byte> destination)
        {
            if (_offset >= _data.Length)
                return 0;

            int length = Math.Min(Math.Min(destination.Length, _rowLength), _data.Length - _offset);
            _data.AsSpan(_offset, length).CopyTo(destination);
            _offset += length;
            return length;
        }
    }

    internal sealed class RowWriter
    {
        private readonly MemoryStream _stream = new();

        public void Write(ReadOnlySpan<byte> data) => _stream.Write(data);
        public byte[] ToArray() => _stream.ToArray();
    }
}

internal sealed class ManagedTiffPage
{
    public required uint Width { get; init; }
    public required uint Height { get; init; }
    public required byte[] Rgb { get; init; }
    public required ushort BitsPerSample { get; init; }
    public required ushort SamplesPerPixel { get; init; }
    public required ushort Compression { get; init; }
    public required ushort Photometric { get; init; }
    public float XResolution { get; init; } = 200.0f;
    public float YResolution { get; init; } = 200.0f;
    public ushort ResolutionUnit { get; init; } = ManagedTiff.ResolutionUnitInch;

    public ManagedTiffPage WithPixels(
        uint width,
        uint height,
        byte[] rgb,
        ushort bitsPerSample,
        ushort samplesPerPixel,
        ushort compression,
        ushort photometric) => new()
    {
        Width = width,
        Height = height,
        Rgb = rgb,
        BitsPerSample = bitsPerSample,
        SamplesPerPixel = samplesPerPixel,
        Compression = compression,
        Photometric = photometric,
        XResolution = XResolution,
        YResolution = YResolution,
        ResolutionUnit = ResolutionUnit
    };
}

internal static class ManagedTiff
{
    internal const ushort CompressionNone = (ushort)Compression.NONE;
    internal const ushort CompressionT4 = (ushort)Compression.CCITTFAX3;
    internal const ushort CompressionT6 = (ushort)Compression.CCITTFAX4;
    internal const ushort CompressionJpeg = (ushort)Compression.JPEG;
    internal const ushort CompressionT85 = 9;
    internal const ushort CompressionT43 = 10;
    internal const ushort PhotometricMinIsWhite = (ushort)Photometric.MINISWHITE;
    internal const ushort PhotometricMinIsBlack = (ushort)Photometric.MINISBLACK;
    internal const ushort PhotometricRgb = (ushort)Photometric.RGB;
    internal const ushort PhotometricItuLab = 10;
    internal const ushort PlanarContiguous = (ushort)PlanarConfig.CONTIG;
    internal const ushort OrientationTopLeft = (ushort)Orientation.TOPLEFT;
    internal const ushort ResolutionUnitInch = (ushort)ResUnit.INCH;

    public static ushort GetDirectoryCount(string path)
    {
        Tiff? tif = Tiff.Open(path, "r");
        if (tif is null)
            throw new IOException($"Unable to open '{path}'.");

        using (tif)
            return checked((ushort)tif.NumberOfDirectories());
    }

    public static ManagedTiffPage ReadRgbaPage(string path, ushort directory)
    {
        Tiff? tif = Tiff.Open(path, "r");
        if (tif is null)
            throw new IOException($"Unable to open '{path}'.");

        using (tif)
        {
            if (!tif.SetDirectory(checked((short)directory)))
                throw new InvalidDataException($"Unable to select TIFF directory {directory}.");

            ManagedTiffPage metadata = ReadMetadata(tif);
            int[] raster = new int[checked((int)(metadata.Width * metadata.Height))];
            if (!tif.ReadRGBAImageOriented(
                    checked((int)metadata.Width),
                    checked((int)metadata.Height),
                    raster,
                    Orientation.TOPLEFT,
                    true))
            {
                throw new InvalidDataException("ReadRGBAImageOriented failed.");
            }

            byte[] rgb = new byte[checked(raster.Length * 3)];
            for (int i = 0; i < raster.Length; i++)
            {
                int pixel = raster[i];
                rgb[3 * i] = checked((byte)Tiff.GetR(pixel));
                rgb[3 * i + 1] = checked((byte)Tiff.GetG(pixel));
                rgb[3 * i + 2] = checked((byte)Tiff.GetB(pixel));
            }

            return metadata.WithPixels(
                metadata.Width,
                metadata.Height,
                rgb,
                metadata.BitsPerSample,
                metadata.SamplesPerPixel,
                metadata.Compression,
                metadata.Photometric);
        }
    }

    public static ManagedTiffPage ReadMetadata(string path, ushort directory)
    {
        Tiff? tif = Tiff.Open(path, "r");
        if (tif is null)
            throw new IOException($"Unable to open '{path}'.");

        using (tif)
        {
            if (!tif.SetDirectory(checked((short)directory)))
                throw new InvalidDataException($"Unable to select TIFF directory {directory}.");
            return ReadMetadata(tif);
        }
    }

    public static byte[] ReadRawStrips(string path, ushort directory)
    {
        Tiff? tif = Tiff.Open(path, "r");
        if (tif is null)
            throw new IOException($"Unable to open '{path}'.");

        using (tif)
        {
            if (!tif.SetDirectory(checked((short)directory)))
                throw new InvalidDataException($"Unable to select TIFF directory {directory}.");

            int count = tif.NumberOfStrips();
            using MemoryStream output = new();

            for (int strip = 0; strip < count; strip++)
            {
                long rawSize = tif.RawStripSize(strip);
                if (rawSize <= 0 || rawSize > int.MaxValue)
                    throw new InvalidDataException($"Invalid raw strip size {rawSize} for strip {strip}.");

                byte[] buffer = new byte[(int)rawSize];
                int read = tif.ReadRawStrip(strip, buffer, 0, checked((int)rawSize));
                if (read < 0)
                    throw new InvalidDataException($"Failed to read raw TIFF strip {strip}.");
                output.Write(buffer, 0, read);
            }

            return output.ToArray();
        }
    }

    public static void WriteRgbPages(string path, IReadOnlyList<ManagedTiffPage> pages)
    {
        if (pages.Count == 0)
            throw new ArgumentException("At least one TIFF page is required.", nameof(pages));

        Tiff? tif = Tiff.Open(path, "w");
        if (tif is null)
            throw new IOException($"Unable to open '{path}' for writing.");

        using (tif)
        {
            for (int pageIndex = 0; pageIndex < pages.Count; pageIndex++)
            {
                ManagedTiffPage page = pages[pageIndex];
                int rowLength = checked((int)page.Width * 3);
                int expected = checked(rowLength * (int)page.Height);
                if (page.Rgb.Length < expected)
                    throw new InvalidDataException("RGB page buffer is too short.");

                SetField(tif, TiffTag.IMAGEWIDTH, page.Width);
                SetField(tif, TiffTag.IMAGELENGTH, page.Height);
                SetField(tif, TiffTag.COMPRESSION, Compression.NONE);
                SetField(tif, TiffTag.BITSPERSAMPLE, 8);
                SetField(tif, TiffTag.SAMPLESPERPIXEL, 3);
                SetField(tif, TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
                SetField(tif, TiffTag.ORIENTATION, Orientation.TOPLEFT);
                SetField(tif, TiffTag.ROWSPERSTRIP, page.Height);
                SetField(tif, TiffTag.XRESOLUTION, page.XResolution);
                SetField(tif, TiffTag.YRESOLUTION, page.YResolution);
                SetField(tif, TiffTag.RESOLUTIONUNIT, (ResUnit)page.ResolutionUnit);
                SetField(tif, TiffTag.PHOTOMETRIC, Photometric.RGB);
                SetField(tif, TiffTag.SOFTWARE, "TKFaxEngine managed C# test");
                SetField(tif, TiffTag.IMAGEDESCRIPTION, $"Converted page {pageIndex + 1}");
                SetField(tif, TiffTag.DATETIME, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
                SetField(tif, TiffTag.MAKE, "soft-switch.org");
                SetField(tif, TiffTag.MODEL, "TKFaxEngine");
                SetField(tif, TiffTag.HOSTCOMPUTER, Environment.MachineName);

                byte[] row = new byte[rowLength];
                for (int y = 0; y < page.Height; y++)
                {
                    Buffer.BlockCopy(page.Rgb, y * rowLength, row, 0, rowLength);
                    if (!tif.WriteScanline(row, y, 0))
                        throw new IOException($"Failed to write TIFF row {y}.");
                }

                if (!tif.WriteDirectory())
                    throw new IOException($"Failed to write TIFF directory {pageIndex}.");
            }
        }
    }

    private static ManagedTiffPage ReadMetadata(Tiff tif)
    {
        uint width = GetUInt(tif, TiffTag.IMAGEWIDTH, 0);
        uint height = GetUInt(tif, TiffTag.IMAGELENGTH, 0);
        if (width == 0 || height == 0)
            throw new InvalidDataException("TIFF page has invalid dimensions.");

        return new ManagedTiffPage
        {
            Width = width,
            Height = height,
            Rgb = Array.Empty<byte>(),
            BitsPerSample = GetUShort(tif, TiffTag.BITSPERSAMPLE, 1),
            SamplesPerPixel = GetUShort(tif, TiffTag.SAMPLESPERPIXEL, 1),
            Compression = GetUShort(tif, TiffTag.COMPRESSION, CompressionNone),
            Photometric = GetUShort(tif, TiffTag.PHOTOMETRIC, PhotometricMinIsWhite),
            XResolution = GetFloat(tif, TiffTag.XRESOLUTION, 200.0f),
            YResolution = GetFloat(tif, TiffTag.YRESOLUTION, 200.0f),
            ResolutionUnit = GetUShort(tif, TiffTag.RESOLUTIONUNIT, ResolutionUnitInch)
        };
    }

    private static uint GetUInt(Tiff tif, TiffTag tag, uint fallback)
    {
        FieldValue[]? field = tif.GetField(tag);
        return field is { Length: > 0 } ? field[0].ToUInt() : fallback;
    }

    private static ushort GetUShort(Tiff tif, TiffTag tag, ushort fallback)
    {
        FieldValue[]? field = tif.GetField(tag);
        return field is { Length: > 0 } ? field[0].ToUShort() : fallback;
    }

    private static float GetFloat(Tiff tif, TiffTag tag, float fallback)
    {
        FieldValue[]? field = tif.GetField(tag);
        return field is { Length: > 0 } ? field[0].ToFloat() : fallback;
    }

    private static void SetField(Tiff tif, TiffTag tag, params object[] values)
    {
        if (!tif.SetField(tag, values))
            throw new IOException($"SetField failed for TIFF tag {(int)tag}.");
    }
}
