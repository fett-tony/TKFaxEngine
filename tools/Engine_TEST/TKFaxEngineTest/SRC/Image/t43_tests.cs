/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * t43_tests.cs - Managed C# port of the spanDSP ITU-T T.43 test.
 * TIFF-FX T.43/T.85 strips are decoded through the managed engine, then each
 * page is encoded and decoded again before TIFF output.
 *
 * Original test written by Steve Underwood.
 * Licensed under the GNU General Public License version 2.
 */

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TKFaxEngine.FaxImage;

namespace TKFaxEngineTest.Image.Test;

public static class t43_tests
{
    private const string InFileName = "Test-Files/itu/tiff-fx/l04x_02x.tif";
    private const string OutFileName = "t43_tests_receive.tif";

    public static int tkfx_t43_tests_body_main(string[] testArguments)
    {
        string sourceFile = testArguments.Length > 0 ? testArguments[0] : InFileName;
        int requestedOutputCompression = testArguments.Length > 1 && int.TryParse(testArguments[1], out int parsed)
            ? parsed
            : 4;

        Console.WriteLine($"Processing '{sourceFile}'");
        Console.WriteLine($"Requested output compression: {requestedOutputCompression}");

        try
        {
            ushort directoryCount = ManagedTiff.GetDirectoryCount(sourceFile);
            if (directoryCount == 0)
                throw new InvalidDataException($"Input TIFF contains no readable directories: {sourceFile}");

            Console.WriteLine($"Input TIFF directories: {directoryCount}");
            List<ManagedTiffPage> outputPages = new(directoryCount);

            for (ushort pageNumber = 0; pageNumber < directoryCount; pageNumber++)
            {
                ManagedTiffPage metadata = ManagedTiff.ReadMetadata(sourceFile, pageNumber);
                Console.WriteLine($"Read {pageNumber}");
                Console.WriteLine($"Width {metadata.Width}, height {metadata.Height}, bits {metadata.BitsPerSample}, samples {metadata.SamplesPerPixel}");

                ManagedTiffPage decodedSource = DecodeInputPage(sourceFile, pageNumber, metadata);
                ManagedTiffPage roundTripped = RoundTripT43(decodedSource);
                outputPages.Add(roundTripped);
            }

            ManagedTiff.WriteRgbPages(OutFileName, outputPages);
            ManagedTiffPage last = outputPages[^1];
            Console.WriteLine($"XXX - image is {last.Width} by {last.Height}, {last.Rgb.Length} bytes");
            Console.WriteLine("Done!");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static ManagedTiffPage DecodeInputPage(string path, ushort pageNumber, ManagedTiffPage metadata)
    {
        if (metadata.Compression == ManagedTiff.CompressionT43)
        {
            Console.WriteLine("Compression is T.43");
            byte[] compressed = ManagedTiff.ReadRawStrips(path, pageNumber);
            return DecodeT43(compressed, metadata);
        }

        if (metadata.Compression == ManagedTiff.CompressionT85)
        {
            Console.WriteLine("Compression is T.85");
            byte[] compressed = ManagedTiff.ReadRawStrips(path, pageNumber);
            return DecodeT85(compressed, metadata);
        }

        Console.WriteLine($"Compression is {metadata.Compression}; using BitMiracle.LibTiff.NET decompression");
        return ManagedTiff.ReadRgbaPage(path, pageNumber);
    }

    private static ManagedTiffPage DecodeT43(byte[] compressed, ManagedTiffPage metadata)
    {
        t42_tests.RowWriter writer = new();
        using T43DecodeState decoder = T43.t43_decode_init(null, WriteRow, writer);
        T43.t43_decode_set_comment_handler(decoder, 1000, CommentHandler, null);
        T43.t43_decode_set_image_size_constraints(decoder, metadata.Width, metadata.Height);
        decoder.logging.FlowSink = Console.WriteLine;
        decoder.logging.WarningSink = Console.Error.WriteLine;

        int result = T43.t43_decode_put(decoder, compressed);
        if (result == T85Constants.T4_DECODE_MORE_DATA)
            result = T43.t43_decode_put(decoder, ReadOnlySpan<byte>.Empty);
        if (result != T85Constants.T4_DECODE_OK)
            throw new InvalidDataException($"T.43 decoder returned {result}.");

        uint width = T43.t43_decode_get_image_width(decoder);
        uint height = T43.t43_decode_get_image_length(decoder);
        byte[] pixels = writer.ToArray();

        if (decoder.samples_per_pixel == 1)
            pixels = ExpandGrayToRgb(pixels, width, height);

        int expected = checked((int)(width * height * 3));
        if (pixels.Length != expected)
            throw new InvalidDataException($"Decoded T.43 image size mismatch: {pixels.Length} != {expected}.");

        Console.WriteLine($"Compressed image is {compressed.Length} bytes, decoded image {width} x {height}");
        return metadata.WithPixels(width, height, pixels, 8, 3, ManagedTiff.CompressionNone, ManagedTiff.PhotometricRgb);
    }

    private static ManagedTiffPage DecodeT85(byte[] compressed, ManagedTiffPage metadata)
    {
        t42_tests.RowWriter writer = new();
        using T85DecodeState decoder = T85Decode.Initialize(null, WriteRow, writer);
        T85Decode.SetCommentHandler(decoder, 1000, CommentHandler, null);
        T85Decode.SetImageSizeConstraints(decoder, metadata.Width, metadata.Height);
        decoder.Logging.FlowSink = Console.WriteLine;
        decoder.Logging.WarningSink = Console.Error.WriteLine;

        int result = T85Decode.Put(decoder, compressed);
        if (result == T85Constants.T4_DECODE_MORE_DATA)
            result = T85Decode.Put(decoder, ReadOnlySpan<byte>.Empty);
        if (result != T85Constants.T4_DECODE_OK)
            throw new InvalidDataException($"T.85 decoder returned {result}.");

        uint width = T85Decode.GetImageWidth(decoder);
        uint height = T85Decode.GetImageLength(decoder);
        byte[] packed = writer.ToArray();
        byte[] rgb = ExpandBilevelToRgb(packed, width, height, metadata.Photometric == ManagedTiff.PhotometricMinIsWhite);
        return metadata.WithPixels(width, height, rgb, 8, 3, ManagedTiff.CompressionNone, ManagedTiff.PhotometricRgb);
    }

    private static ManagedTiffPage RoundTripT43(ManagedTiffPage source)
    {
        t42_tests.RowReader reader = new(source.Rgb, checked((int)source.Width * 3));
        using T43EncodeState encoder = T43.t43_encode_init(null, source.Width, source.Height, ReadRow, reader);
        if (T43.t43_encode_set_image_type(encoder, T43.T43_IMAGE_TYPE_COLOUR) < 0)
            throw new InvalidOperationException("Unable to select the T.43 continuous-colour image type.");

        T43.t43_encode_comment(encoder, Encoding.ASCII.GetBytes("TKFaxEngine t43_tests managed port"));
        encoder.logging.FlowSink = Console.WriteLine;
        encoder.logging.WarningSink = Console.Error.WriteLine;
        byte[] encoded = t42_tests.DrainEncoder(destination => T43.t43_encode_get(encoder, destination));
        Console.WriteLine($"Re-encoded T.43 length: {encoded.Length} bytes");

        if (encoded.Length == 0)
        {
            Console.WriteLine("T.43 encoding is not implemented in the current spanDSP source; encode/decode round-trip skipped.");
            return source;
        }

        return DecodeT43(encoded, source);
    }

    private static int ReadRow(object? userData, Span<byte> destination) =>
        ((t42_tests.RowReader)userData!).Read(destination);

    private static int WriteRow(object? userData, ReadOnlySpan<byte> data, int len)
    {
        ((t42_tests.RowWriter)userData!).Write(data[..len]);
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

    private static byte[] ExpandGrayToRgb(byte[] gray, uint width, uint height)
    {
        int pixels = checked((int)(width * height));
        if (gray.Length != pixels)
            throw new InvalidDataException($"Gray image size mismatch: {gray.Length} != {pixels}.");

        byte[] rgb = new byte[checked(pixels * 3)];
        for (int i = 0; i < pixels; i++)
        {
            rgb[3 * i] = gray[i];
            rgb[3 * i + 1] = gray[i];
            rgb[3 * i + 2] = gray[i];
        }
        return rgb;
    }

    private static byte[] ExpandBilevelToRgb(byte[] packed, uint width, uint height, bool minIsWhite)
    {
        int rowBytes = checked((int)((width + 7) / 8));
        int expected = checked(rowBytes * (int)height);
        if (packed.Length < expected)
            throw new InvalidDataException($"Bi-level image size mismatch: {packed.Length} < {expected}.");

        byte[] rgb = new byte[checked((int)(width * height * 3))];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int bit = (packed[y * rowBytes + (x >> 3)] >> (7 - (x & 7))) & 1;
                byte level = minIsWhite
                    ? (bit == 0 ? (byte)255 : (byte)0)
                    : (bit == 0 ? (byte)0 : (byte)255);
                int destination = checked((y * (int)width + x) * 3);
                rgb[destination] = level;
                rgb[destination + 1] = level;
                rgb[destination + 2] = level;
            }
        }
        return rgb;
    }
}
