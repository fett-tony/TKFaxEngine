/*
 * TKFaxEngine - managed C# port of the spanDSP test
 *
 * image_translate_tests_adapter.c
 *
 * Direct port of image_translate_tests.c. Original implementation written by
 * Steve Underwood <steveu@coppice.org>, Copyright (C) 2009 Steve Underwood.
 *
 * This test file preserves the GNU General Public License version 2 terms
 * of the original test source.
 */

#nullable enable

using System.Buffers.Binary;
using BitMiracle.LibTiff.Classic;
using TKFaxEngine.FaxImage;
using static TKFaxEngine.FaxImage.ImageTranslateApi;

namespace TKFaxEngineTest.Image.Test;

public static class ImageTranslateTestsAdapter
{
    private const string InputTiffFileName = "Test-Files/local/lenna-colour.tif";

    private static readonly string[] Dithered50By50 =
    {
        "  0:                               @   @  @  @ @ @ @ @ ",
        "  1:                @   @  @ @ @ @  @ @  @  @  @  @ @ @",
        "  2:           @      @   @     @  @   @  @ @ @ @ @ @ @",
        "  3:               @    @    @ @  @  @ @ @ @ @ @ @ @ @ ",
        "  4:       @  @  @   @    @      @  @   @  @  @ @ @ @ @",
        "  5:                    @  @ @ @  @  @ @  @ @ @  @ @ @ ",
        "  6:    @      @  @  @       @  @  @  @ @ @  @ @ @ @ @@",
        "  7:         @     @   @ @ @  @  @  @ @  @ @ @ @ @ @ @ ",
        "  8:      @     @    @    @  @  @ @  @ @ @ @ @ @ @ @ @ ",
        "  9:   @      @    @    @   @  @   @ @  @ @ @ @ @ @ @ @",
        " 10:      @     @    @ @  @  @  @ @  @ @  @ @ @ @ @@ @@",
        " 11:          @   @ @    @  @  @  @ @ @ @ @ @ @ @ @ @@ ",
        " 12:   @  @ @        @ @  @  @ @ @  @  @ @ @ @ @@ @ @ @",
        " 13:           @ @ @    @  @  @  @ @ @ @ @ @ @ @ @ @@ @",
        " 14:   @   @        @ @  @  @  @  @ @ @ @ @ @ @ @@ @ @@",
        " 15:     @   @ @ @ @    @  @ @ @ @ @ @ @ @ @ @ @@ @ @@ ",
        " 16:                @ @  @  @  @ @  @ @ @ @ @@ @ @@@ @@",
        " 17:   @  @ @ @ @ @  @  @  @  @ @ @ @ @ @ @ @ @ @ @ @ @",
        " 18:     @       @  @  @ @ @ @  @ @ @ @ @ @@ @@ @@ @@@ ",
        " 19: @ @     @ @  @  @ @  @  @ @ @ @ @ @ @ @ @ @ @@ @ @",
        " 20:     @ @   @   @   @ @ @ @ @  @ @ @ @@ @@ @@@ @@@@@",
        " 21:   @     @  @ @ @ @  @  @ @ @@ @ @ @ @ @ @ @ @@ @ @",
        " 22:    @ @ @  @    @  @  @ @  @  @ @ @@ @@ @@@ @ @@ @@",
        " 23:  @       @  @ @  @ @ @ @ @ @@ @ @ @ @ @ @ @@@ @@ @",
        " 24:   @ @ @ @  @ @ @ @ @ @ @ @ @ @ @@ @@ @@@ @ @ @@ @@",
        " 25: @    @   @  @   @  @  @ @ @ @ @ @ @ @ @ @@@@@ @@@@",
        " 26:   @   @ @  @ @ @ @ @ @ @ @ @ @@ @@ @@@ @ @ @@@ @ @",
        " 27: @  @ @   @  @  @  @ @ @ @ @ @ @ @ @ @ @@@ @@ @@@@@",
        " 28:   @   @ @  @ @ @ @ @ @ @ @ @ @ @@ @@ @ @ @@ @@ @ @",
        " 29: @  @ @   @ @  @  @ @ @ @ @ @@ @@ @@ @@@@@ @@@ @@@@",
        " 30:   @   @ @  @ @ @ @ @ @ @ @@ @@ @ @ @@ @ @@@ @@@ @@",
        " 31: @  @ @ @  @ @ @ @ @ @ @ @ @ @ @@ @@@ @ @@ @@@ @@@@",
        " 32:   @   @ @  @  @ @ @ @ @ @ @ @ @ @@ @@@@@ @@ @@@ @@",
        " 33: @  @ @  @ @ @ @ @ @ @ @ @@ @@@ @@ @ @ @ @@@@@ @@@@",
        " 34:  @  @ @  @ @ @ @ @ @ @@ @ @ @ @@ @@@@@@@ @ @@@@ @@",
        " 35:   @  @  @ @  @ @ @ @ @ @@ @@ @@ @@ @ @ @@@@@@ @@@@",
        " 36:  @ @  @ @ @ @ @ @ @ @ @ @@ @@ @@ @@@ @@ @ @@@@@@ @",
        " 37:  @  @ @ @ @ @ @ @ @@ @@ @ @ @@ @@ @ @@@@@@@ @@ @@@",
        " 38:  @ @ @ @ @ @ @ @ @ @ @ @@@ @@ @@ @@@@ @ @@@@@@@@@@",
        " 39:   @  @  @  @ @ @ @ @@ @@ @@ @@ @@@@ @@@@ @@ @@ @@@",
        " 40: @  @ @ @ @@ @ @ @@ @ @ @ @ @@ @@ @ @@ @@@@@@@@@@@@",
        " 41: @ @ @ @ @  @ @ @@ @ @@@ @@@ @@@ @@@@@@@@@ @@ @@ @@",
        " 42:  @ @ @ @ @@ @ @ @ @@ @ @@ @@ @@@@ @ @ @@@@@@@@@@@@",
        " 43: @ @  @ @ @ @ @@ @@ @ @@ @@ @@@ @ @@@@@@@ @@ @@@@@@",
        " 44:  @ @ @ @ @ @ @ @ @@ @@ @@ @@ @@@@@ @@ @@@@@@@@ @@@",
        " 45: @ @ @ @ @ @ @@ @@ @@ @@ @@ @@@ @ @@@@@@@@ @@@@@@@@",
        " 46:  @ @ @ @ @ @ @@ @ @ @@ @@ @@ @@@@@ @ @@ @@@@@@@@@@",
        " 47: @ @ @ @ @ @@ @ @ @@@@ @@@@ @@@@@ @@@@@@@@@@@ @@@@@",
        " 48:  @ @ @ @@ @ @@ @@ @ @@ @ @@@ @ @@@@@ @@@@@@@@@@@@@",
        " 49: @ @ @ @ @ @@ @@ @@ @@ @@@@ @@@@@@@ @@@@@@ @@@@@@@@"
    };

    public static int tkfx_image_translate_tests_main(string[] testArguments)
    {
        ArgumentNullException.ThrowIfNull(testArguments);
        string[] args = testArguments;

        try
        {
            return Run(args);
        }
        catch (TestExitException ex)
        {
            return ex.ExitCode;
        }
    }

    public static int Run(string[] args)
    {
        _ = args;

        TranslateTestsGray16();
        TranslateTestsGray8();
        TranslateTestsColour16();
        TranslateTestsColour8();
        GrowTestsColour8();

        LennaTests(0, 0, "lenna-bw.tif");
        LennaTests(200, 0, "lenna-bw-200.tif");
        LennaTests(1728, 0, "lenna-bw-1728.tif");
        LennaTests(1728, 2, "lenna-bw-1728-superfine.tif");
        LennaTests(1728, -1, "lenna-colour-1728.tif");
        LennaTests(1728, -2, "lenna-gray-1728.tif");

        Console.WriteLine("Tests passed.");
        return 0;
    }

    private static void DisplayRow(int row, int width, ReadOnlySpan<byte> buffer)
    {
        Console.Write($"{row,3}: ");
        for (int i = 0; i < width; i++)
        {
            int testPixel = (buffer[i >> 3] >> (7 - (i & 7))) & 0x01;
            Console.Write(testPixel != 0 ? ' ' : '@');
        }
        Console.WriteLine();
    }

    private static void CreateUndithered50By50(
        ImageDescriptor imageDescriptor,
        byte[] buffer,
        int bytesPerPixel)
    {
        imageDescriptor.Image = buffer;
        imageDescriptor.Width = 50;
        imageDescriptor.Length = 50;
        imageDescriptor.BytesPerPixel = bytesPerPixel;
        imageDescriptor.CurrentRow = 0;

        switch (bytesPerPixel)
        {
        case 1:
            for (int i = 0; i < imageDescriptor.Length; i++)
            {
                for (int j = 0; j < imageDescriptor.Width; j++)
                {
                    buffer[imageDescriptor.Width * i + j] =
                        (byte)(((i + j) * 655) >> 8);
                }
            }
            break;

        case 2:
            for (int i = 0; i < imageDescriptor.Length; i++)
            {
                for (int j = 0; j < imageDescriptor.Width; j++)
                {
                    int offset = 2 * (imageDescriptor.Width * i + j);
                    BinaryPrimitives.WriteUInt16LittleEndian(
                        buffer.AsSpan(offset, 2),
                        (ushort)((i + j) * 655));
                }
            }
            break;

        case 3:
        case 4:
        {
            int samplesPerPixel = bytesPerPixel;
            for (int i = 0; i < imageDescriptor.Length; i++)
            {
                for (int j = 0; j < imageDescriptor.Width; j++)
                {
                    int offset = samplesPerPixel * (imageDescriptor.Width * i + j);
                    ulong level = (ulong)(i + j) * 655UL;
                    buffer[offset + 0] = SaturateUInt8((level * 36532UL) >> 23);
                    buffer[offset + 1] = SaturateUInt8((level * 37216UL) >> 24);
                    buffer[offset + 2] = SaturateUInt8((level * 47900UL) >> 22);
                    if (samplesPerPixel == 4)
                    {
                        buffer[offset + 3] = 0;
                    }
                }
            }
            break;
        }

        case 6:
        case 8:
        {
            int samplesPerPixel = bytesPerPixel / 2;
            for (int i = 0; i < imageDescriptor.Length; i++)
            {
                for (int j = 0; j < imageDescriptor.Width; j++)
                {
                    int pixel = samplesPerPixel * (imageDescriptor.Width * i + j);
                    ulong level = (ulong)(i + j) * 655UL;
                    WriteUInt16(buffer, 2 * (pixel + 0), SaturateUInt16((level * 36532UL) >> 15));
                    WriteUInt16(buffer, 2 * (pixel + 1), SaturateUInt16((level * 37216UL) >> 16));
                    WriteUInt16(buffer, 2 * (pixel + 2), SaturateUInt16((level * 47900UL) >> 14));
                    if (samplesPerPixel == 4)
                    {
                        WriteUInt16(buffer, 2 * (pixel + 3), 0);
                    }
                }
            }
            break;
        }

        default:
            throw new ArgumentOutOfRangeException(nameof(bytesPerPixel));
        }
    }

    private static int TestDithered50By50(int row, int width, ReadOnlySpan<byte> buffer)
    {
        int match = 0;
        for (int i = 0; i < width; i++)
        {
            int referencePixel = Dithered50By50[row][i + 5] == ' ' ? 1 : 0;
            int testPixel = (buffer[i >> 3] >> (7 - (i & 7))) & 0x01;
            if (referencePixel != testPixel)
            {
                match = -1;
            }
        }
        return match;
    }

    private static int RowRead(object? userData, Span<byte> buffer, int len)
    {
        if (userData is not ImageDescriptor imageDescriptor)
        {
            return 0;
        }
        if (imageDescriptor.CurrentRow >= imageDescriptor.Length)
        {
            return 0;
        }

        Span<byte> destination = buffer[..len];
        int offset = imageDescriptor.CurrentRow
            * imageDescriptor.Width
            * imageDescriptor.BytesPerPixel;
        imageDescriptor.Image.AsSpan(offset, destination.Length).CopyTo(destination);
        imageDescriptor.CurrentRow++;
        return destination.Length;
    }

    private static void GetBilevelImage(ImageTranslateState state, bool compare)
    {
        int rowLength = (state.OutputWidth + 7) / 8;
        byte[] rowBuffer = new byte[rowLength];

        for (int i = 0; i < state.OutputLength; i++)
        {
            int length = image_translate_row(state, rowBuffer, rowLength);
            if (length != rowLength)
            {
                Console.WriteLine($"Image finished early - {length} {rowLength}");
                Exit(2);
            }

            DisplayRow(i, state.OutputWidth, rowBuffer);
            if (compare && TestDithered50By50(i, state.OutputWidth, rowBuffer) != 0)
            {
                Console.WriteLine($"Dithered image mismatch at row {i}");
            }
        }

        int finalLength = image_translate_row(state, rowBuffer, rowLength);
        if (finalLength != 0)
        {
            Console.WriteLine($"Image finished late - {finalLength} {rowLength}");
            Exit(2);
        }
    }

    private static void GetGray8Image(ImageTranslateState state, bool compare)
    {
        byte[] rowBuffer = new byte[state.OutputWidth];
        for (int i = 0; i < state.OutputLength; i++)
        {
            int length = image_translate_row(state, rowBuffer, state.OutputWidth);
            if (length != state.OutputWidth)
            {
                Console.WriteLine($"Image finished early - {length} {state.OutputWidth}");
                Exit(2);
            }

            if (compare)
            {
                for (int j = 0; j < 50; j++)
                {
                    int expected = ((i + j) * 655) >> 8;
                    if (rowBuffer[j] != expected)
                    {
                        Console.WriteLine($"Image mismatch - {j}x{i} - {expected} {rowBuffer[j]}");
                    }
                }
            }
        }

        int finalLength = image_translate_row(state, rowBuffer, state.OutputWidth);
        if (finalLength != 0)
        {
            Console.WriteLine($"Image finished late - {finalLength} {state.OutputWidth}");
            Exit(2);
        }
    }

    private static void GetGray16Image(ImageTranslateState state, bool compare)
    {
        int rowLength = 2 * state.OutputWidth;
        byte[] rowBuffer = new byte[rowLength];
        for (int i = 0; i < state.OutputLength; i++)
        {
            int length = image_translate_row(state, rowBuffer, rowLength);
            if (length != rowLength)
            {
                Console.WriteLine($"Image finished early - {length} {rowLength}");
                Exit(2);
            }

            if (compare)
            {
                for (int j = 0; j < state.OutputWidth; j++)
                {
                    int actual = BinaryPrimitives.ReadUInt16LittleEndian(rowBuffer.AsSpan(2 * j, 2));
                    int expected = (i + j) * 655;
                    if (actual != expected)
                    {
                        Console.WriteLine($"Image mismatch - {j}x{i} - {expected} {actual}");
                    }
                }
            }
        }

        int finalLength = image_translate_row(state, rowBuffer, rowLength);
        if (finalLength != 0)
        {
            Console.WriteLine($"Image finished late - {finalLength} {rowLength}");
            Exit(2);
        }
    }

    private static void GetColour8Image(ImageTranslateState state, bool compare)
    {
        const int samplesPerPixel = 3;
        int rowLength = samplesPerPixel * state.OutputWidth;
        byte[] rowBuffer = new byte[rowLength];

        for (int i = 0; i < state.OutputLength; i++)
        {
            int length = image_translate_row(state, rowBuffer, rowLength);
            if (length != rowLength)
            {
                Console.WriteLine($"Image finished early - {length} {rowLength}");
                Exit(2);
            }

            if (compare)
            {
                for (int j = 0; j < state.OutputWidth; j++)
                {
                    ulong level = (ulong)(i + j) * 655UL;
                    int red = SaturateUInt8((level * 36532UL) >> 23);
                    int green = SaturateUInt8((level * 37216UL) >> 24);
                    int blue = SaturateUInt8((level * 47900UL) >> 22);
                    int offset = samplesPerPixel * j;
                    if (rowBuffer[offset + 0] != red
                        || rowBuffer[offset + 1] != green
                        || rowBuffer[offset + 2] != blue)
                    {
                        Console.WriteLine(
                            $"Image mismatch - {j}x{i} - ({red} {green} {blue}) "
                            + $"({rowBuffer[offset + 0]} {rowBuffer[offset + 1]} {rowBuffer[offset + 2]})");
                    }
                }
            }
        }

        int finalLength = image_translate_row(state, rowBuffer, rowLength);
        if (finalLength != 0)
        {
            Console.WriteLine($"Image finished late - {finalLength} {rowLength}");
            Exit(2);
        }
    }

    private static void GetColour16Image(ImageTranslateState state, bool compare)
    {
        const int samplesPerPixel = 3;
        int rowLength = 2 * samplesPerPixel * state.OutputWidth;
        byte[] rowBuffer = new byte[rowLength];

        for (int i = 0; i < state.OutputLength; i++)
        {
            int length = image_translate_row(state, rowBuffer, rowLength);
            if (length != rowLength)
            {
                Console.WriteLine($"Image finished early - {length} {rowLength}");
                Exit(2);
            }

            if (compare)
            {
                for (int j = 0; j < state.OutputWidth; j++)
                {
                    ulong level = (ulong)(i + j) * 655UL;
                    int red = SaturateUInt16((level * 36532UL) >> 15);
                    int green = SaturateUInt16((level * 37216UL) >> 16);
                    int blue = SaturateUInt16((level * 47900UL) >> 14);
                    int offset = 2 * samplesPerPixel * j;
                    int actualRed = BinaryPrimitives.ReadUInt16LittleEndian(rowBuffer.AsSpan(offset + 0, 2));
                    int actualGreen = BinaryPrimitives.ReadUInt16LittleEndian(rowBuffer.AsSpan(offset + 2, 2));
                    int actualBlue = BinaryPrimitives.ReadUInt16LittleEndian(rowBuffer.AsSpan(offset + 4, 2));
                    if (actualRed != red || actualGreen != green || actualBlue != blue)
                    {
                        Console.WriteLine(
                            $"Image mismatch - {j}x{i} - ({red} {green} {blue}) "
                            + $"({actualRed} {actualGreen} {actualBlue})");
                    }
                }
            }
        }

        int finalLength = image_translate_row(state, rowBuffer, rowLength);
        if (finalLength != 0)
        {
            Console.WriteLine($"Image finished late - {finalLength} {rowLength}");
            Exit(2);
        }
    }

    private static void TranslateTestsGray8()
    {
        byte[] image = new byte[50 * 50];
        ImageDescriptor descriptor = new();
        ImageTranslateState? state = null;

        Console.WriteLine("Dithering from a 8 bit per sample gray scale to bi-level");
        CreateUndithered50By50(descriptor, image, 1);
        state = StateOrExit(image_translate_init(null, T4_IMAGE_TYPE_BILEVEL, -1, -1, T4_IMAGE_TYPE_GRAY_8BIT, descriptor.Width, descriptor.Length, RowRead, descriptor));
        GetBilevelImage(state, true);

        Console.WriteLine("Scrunching from a 8 bit per sample gray scale to 8 bit per sample gray scale");
        CreateUndithered50By50(descriptor, image, 1);
        state = StateOrExit(image_translate_init(state, T4_IMAGE_TYPE_GRAY_8BIT, -1, -1, T4_IMAGE_TYPE_GRAY_8BIT, descriptor.Width, descriptor.Length, RowRead, descriptor));
        GetGray8Image(state, true);

        Console.WriteLine("Scrunching from a 8 bit per sample gray scale to 16 bit per sample gray scale");
        CreateUndithered50By50(descriptor, image, 1);
        state = StateOrExit(image_translate_init(state, T4_IMAGE_TYPE_GRAY_12BIT, -1, -1, T4_IMAGE_TYPE_GRAY_8BIT, descriptor.Width, descriptor.Length, RowRead, descriptor));
        GetGray16Image(state, true);

        Console.WriteLine("Scrunching from a 8 bit per sample gray scale to 3x8 bit per sample colour");
        CreateUndithered50By50(descriptor, image, 1);
        state = StateOrExit(image_translate_init(state, T4_IMAGE_TYPE_COLOUR_8BIT, -1, -1, T4_IMAGE_TYPE_GRAY_8BIT, descriptor.Width, descriptor.Length, RowRead, descriptor));
        GetColour8Image(state, true);

        Console.WriteLine("Scrunching from a 8 bit per sample gray scale to 3x16 bit per sample colour");
        CreateUndithered50By50(descriptor, image, 1);
        state = StateOrExit(image_translate_init(state, T4_IMAGE_TYPE_COLOUR_12BIT, -1, -1, T4_IMAGE_TYPE_GRAY_8BIT, descriptor.Width, descriptor.Length, RowRead, descriptor));
        GetColour16Image(state, true);

        image_translate_free(state);
    }

    private static void TranslateTestsGray16()
    {
        byte[] image = new byte[2 * 50 * 50];
        ImageDescriptor descriptor = new();
        ImageTranslateState? state = null;

        Console.WriteLine("Dithering from a 16 bit per sample gray scale to bi-level");
        CreateUndithered50By50(descriptor, image, 2);
        state = StateOrExit(image_translate_init(null, T4_IMAGE_TYPE_BILEVEL, -1, -1, T4_IMAGE_TYPE_GRAY_12BIT, descriptor.Width, descriptor.Length, RowRead, descriptor));
        GetBilevelImage(state, true);

        Console.WriteLine("Scrunching from a 16 bit per sample gray scale to 8 bit per sample gray scale");
        CreateUndithered50By50(descriptor, image, 2);
        state = StateOrExit(image_translate_init(state, T4_IMAGE_TYPE_GRAY_8BIT, -1, -1, T4_IMAGE_TYPE_GRAY_12BIT, descriptor.Width, descriptor.Length, RowRead, descriptor));
        GetGray8Image(state, true);

        Console.WriteLine("Scrunching from a 16 bit per sample gray scale to 16 bit per sample gray scale");
        CreateUndithered50By50(descriptor, image, 2);
        state = StateOrExit(image_translate_init(state, T4_IMAGE_TYPE_GRAY_12BIT, -1, -1, T4_IMAGE_TYPE_GRAY_12BIT, descriptor.Width, descriptor.Length, RowRead, descriptor));
        GetGray16Image(state, true);

        Console.WriteLine("Scrunching from a 16 bit per sample gray scale to 3x8 bit per sample colour");
        CreateUndithered50By50(descriptor, image, 2);
        state = StateOrExit(image_translate_init(state, T4_IMAGE_TYPE_COLOUR_8BIT, -1, -1, T4_IMAGE_TYPE_GRAY_12BIT, descriptor.Width, descriptor.Length, RowRead, descriptor));
        GetColour8Image(state, true);

        Console.WriteLine("Scrunching from a 16 bit per sample gray scale to 3x16 bit per sample colour");
        CreateUndithered50By50(descriptor, image, 2);
        state = StateOrExit(image_translate_init(state, T4_IMAGE_TYPE_COLOUR_12BIT, -1, -1, T4_IMAGE_TYPE_GRAY_12BIT, descriptor.Width, descriptor.Length, RowRead, descriptor));
        GetColour16Image(state, true);

        image_translate_free(state);
    }

    private static void TranslateTestsColour8()
    {
        byte[] image = new byte[3 * 50 * 50];
        ImageDescriptor descriptor = new();
        ImageTranslateState? state = null;

        Console.WriteLine("Dithering from a 3x8 bit per sample colour to bi-level");
        CreateUndithered50By50(descriptor, image, 3);
        state = StateOrExit(image_translate_init(null, T4_IMAGE_TYPE_BILEVEL, -1, -1, T4_IMAGE_TYPE_COLOUR_8BIT, descriptor.Width, descriptor.Length, RowRead, descriptor));
        GetBilevelImage(state, true);

        Console.WriteLine("Scrunching from a 3x8 bit per sample colour to 8 bit per sample gray scale");
        CreateUndithered50By50(descriptor, image, 3);
        state = StateOrExit(image_translate_init(state, T4_IMAGE_TYPE_GRAY_8BIT, -1, -1, T4_IMAGE_TYPE_COLOUR_8BIT, descriptor.Width, descriptor.Length, RowRead, descriptor));
        GetGray8Image(state, true);

        Console.WriteLine("Scrunching from a 3x8 bit per sample colour to 16 bit per sample gray scale");
        CreateUndithered50By50(descriptor, image, 3);
        state = StateOrExit(image_translate_init(state, T4_IMAGE_TYPE_GRAY_12BIT, -1, -1, T4_IMAGE_TYPE_COLOUR_8BIT, descriptor.Width, descriptor.Length, RowRead, descriptor));
        GetGray16Image(state, true);

        Console.WriteLine("Scrunching from a 3x8 bit per sample colour to 3x8 bit per sample colour");
        CreateUndithered50By50(descriptor, image, 3);
        state = StateOrExit(image_translate_init(state, T4_IMAGE_TYPE_COLOUR_8BIT, -1, -1, T4_IMAGE_TYPE_COLOUR_8BIT, descriptor.Width, descriptor.Length, RowRead, descriptor));
        GetColour8Image(state, true);

        Console.WriteLine("Scrunching from a 3x8 bit per sample colour to 3x16 bit per sample colour");
        CreateUndithered50By50(descriptor, image, 3);
        state = StateOrExit(image_translate_init(state, T4_IMAGE_TYPE_COLOUR_12BIT, -1, -1, T4_IMAGE_TYPE_COLOUR_8BIT, descriptor.Width, descriptor.Length, RowRead, descriptor));
        GetColour16Image(state, true);

        image_translate_free(state);
    }

    private static void TranslateTestsColour16()
    {
        byte[] image = new byte[6 * 50 * 50];
        ImageDescriptor descriptor = new();
        ImageTranslateState? state = null;

        Console.WriteLine("Dithering from a 3x16 bit per sample colour to bi-level");
        CreateUndithered50By50(descriptor, image, 6);
        state = StateOrExit(image_translate_init(null, T4_IMAGE_TYPE_BILEVEL, -1, -1, T4_IMAGE_TYPE_COLOUR_12BIT, descriptor.Width, descriptor.Length, RowRead, descriptor));
        GetBilevelImage(state, true);

        Console.WriteLine("Scrunching from a 3x16 bit per sample colour to 8 bit per sample gray scale");
        CreateUndithered50By50(descriptor, image, 6);
        state = StateOrExit(image_translate_init(state, T4_IMAGE_TYPE_GRAY_8BIT, -1, -1, T4_IMAGE_TYPE_COLOUR_12BIT, descriptor.Width, descriptor.Length, RowRead, descriptor));
        GetGray8Image(state, true);

        Console.WriteLine("Scrunching from a 3x16 bit per sample colour to 16 bit per sample gray scale");
        CreateUndithered50By50(descriptor, image, 6);
        state = StateOrExit(image_translate_init(state, T4_IMAGE_TYPE_GRAY_12BIT, -1, -1, T4_IMAGE_TYPE_COLOUR_12BIT, descriptor.Width, descriptor.Length, RowRead, descriptor));
        GetGray16Image(state, true);

        Console.WriteLine("Scrunching from a 3x16 bit per sample colour to 3x8 bit per sample colour");
        CreateUndithered50By50(descriptor, image, 6);
        state = StateOrExit(image_translate_init(state, T4_IMAGE_TYPE_COLOUR_8BIT, -1, -1, T4_IMAGE_TYPE_COLOUR_12BIT, descriptor.Width, descriptor.Length, RowRead, descriptor));
        GetColour8Image(state, true);

        Console.WriteLine("Scrunching from a 3x16 bit per sample colour to 3x16 bit per sample colour");
        CreateUndithered50By50(descriptor, image, 6);
        state = StateOrExit(image_translate_init(state, T4_IMAGE_TYPE_COLOUR_12BIT, -1, -1, T4_IMAGE_TYPE_COLOUR_12BIT, descriptor.Width, descriptor.Length, RowRead, descriptor));
        GetColour16Image(state, true);

        image_translate_free(state);
    }

    private static void GrowTestsColour8()
    {
        byte[] image = new byte[3 * 50 * 50];
        ImageDescriptor descriptor = new();

        Console.WriteLine("Image growth tests");
        CreateUndithered50By50(descriptor, image, 3);
        ImageTranslateState state = StateOrExit(image_translate_init(null, T4_IMAGE_TYPE_BILEVEL, 200, -1, T4_IMAGE_TYPE_COLOUR_8BIT, descriptor.Width, descriptor.Length, RowRead, descriptor));
        GetBilevelImage(state, false);
        image_translate_free(state);
    }

    private static int RowRead2(object? userData, Span<byte> buffer, int len)
    {
        if (userData is not ImageTranslateState state)
        {
            return 0;
        }
        Span<byte> destination = buffer[..len];
        image_translate_row(state, destination);
        return destination.Length;
    }

    private static void LennaTests(int outputWidth, int outputLengthScaling, string file)
    {
        Console.WriteLine(outputLengthScaling >= 0
            ? "Dithering Lenna from colour to bi-level test"
            : "Processing Lenna test");

        Tiff? inputFile = Tiff.Open(InputTiffFileName, "r");
        if (inputFile is null)
        {
            return;
        }

        int imageWidth = GetIntField(inputFile, TiffTag.IMAGEWIDTH, 0);
        if (imageWidth <= 0)
        {
            inputFile.Close();
            return;
        }

        int imageLength = GetIntField(inputFile, TiffTag.IMAGELENGTH, 0);
        if (imageLength <= 0)
        {
            inputFile.Close();
            return;
        }

        float xResolution = GetFloatField(inputFile, TiffTag.XRESOLUTION, 200.0F);
        float yResolution = GetFloatField(inputFile, TiffTag.YRESOLUTION, 200.0F);
        ushort resolutionUnit = GetUShortField(inputFile, TiffTag.RESOLUTIONUNIT, (ushort)ResUnit.INCH);
        ushort bitsPerSample = GetUShortField(inputFile, TiffTag.BITSPERSAMPLE, 0);
        ushort samplesPerPixel = GetUShortField(inputFile, TiffTag.SAMPLESPERPIXEL, 0);

        Console.WriteLine(
            $"Original image is {imageWidth} x {imageLength}, {xResolution:F2} x {yResolution:F2} resolution, "
            + $"{bitsPerSample} bits per sample, {samplesPerPixel} samples per pixel");

        int inputByteCount = checked(imageWidth * imageLength * samplesPerPixel);
        byte[] image = new byte[inputByteCount];
        int total = 0;
        for (int strip = 0; strip < 1000; strip++)
        {
            int remaining = inputByteCount - total;
            int length = inputFile.ReadEncodedStrip(strip, image, total, remaining);
            if (length <= 0)
            {
                break;
            }

            total += length;
            if (total == inputByteCount)
            {
                Console.WriteLine("Done");
                break;
            }
        }

        Console.WriteLine($"Input image size {total} {inputByteCount}");
        inputFile.Close();

        int outputLength = outputLengthScaling > 0
            ? (int)((double)imageLength * outputLengthScaling * outputWidth / imageWidth)
            : -1;

        ImageDescriptor descriptor = new()
        {
            Image = image,
            Width = imageWidth,
            Length = imageLength,
            CurrentRow = 0,
            BytesPerPixel = samplesPerPixel
        };

        ImageTranslateState state;
        ImageTranslateState? state2 = null;
        switch (outputLengthScaling)
        {
        case -2:
            state = StateOrExit(image_translate_init(null, T4_IMAGE_TYPE_GRAY_8BIT, outputWidth, outputLength, T4_IMAGE_TYPE_COLOUR_8BIT, imageWidth, imageLength, RowRead, descriptor));
            outputWidth = image_translate_get_output_width(state);
            outputLength = image_translate_get_output_length(state);
            state2 = StateOrExit(image_translate_init(null, T4_IMAGE_TYPE_COLOUR_8BIT, -1, -1, T4_IMAGE_TYPE_GRAY_8BIT, outputWidth, outputLength, RowRead2, state));
            outputWidth = image_translate_get_output_width(state2);
            outputLength = image_translate_get_output_length(state2);
            break;

        case -1:
            state = StateOrExit(image_translate_init(null, T4_IMAGE_TYPE_COLOUR_8BIT, outputWidth, outputLength, T4_IMAGE_TYPE_COLOUR_8BIT, imageWidth, imageLength, RowRead, descriptor));
            outputWidth = image_translate_get_output_width(state);
            outputLength = image_translate_get_output_length(state);
            break;

        default:
            state = StateOrExit(image_translate_init(null, T4_IMAGE_TYPE_BILEVEL, outputWidth, outputLength, T4_IMAGE_TYPE_COLOUR_8BIT, imageWidth, imageLength, RowRead, descriptor));
            outputWidth = image_translate_get_output_width(state);
            outputLength = image_translate_get_output_length(state);
            break;
        }

        Tiff? outputFile = Tiff.Open(file, "w");
        if (outputFile is null)
        {
            return;
        }

        outputFile.SetField(TiffTag.IMAGEWIDTH, outputWidth);
        outputFile.SetField(TiffTag.IMAGELENGTH, outputLength);
        outputFile.SetField(TiffTag.RESOLUTIONUNIT, (ResUnit)resolutionUnit);

        switch (outputLengthScaling)
        {
        case -2:
        case -1:
            outputFile.SetField(TiffTag.BITSPERSAMPLE, 8);
            outputFile.SetField(TiffTag.SAMPLESPERPIXEL, 3);
            outputFile.SetField(TiffTag.PHOTOMETRIC, Photometric.RGB);
            break;

        default:
            outputFile.SetField(TiffTag.BITSPERSAMPLE, 1);
            outputFile.SetField(TiffTag.SAMPLESPERPIXEL, 1);
            outputFile.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISWHITE);
            break;
        }

        if (outputLengthScaling > 0)
        {
            yResolution *= outputLengthScaling;
        }

        outputFile.SetField(TiffTag.XRESOLUTION, xResolution);
        outputFile.SetField(TiffTag.YRESOLUTION, yResolution);
        outputFile.SetField(TiffTag.ORIENTATION, Orientation.TOPLEFT);
        outputFile.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
        outputFile.SetField(TiffTag.FILLORDER, FillOrder.LSB2MSB);
        outputFile.SetField(TiffTag.PAGENUMBER, 0, 1);

        Console.WriteLine($"Input {imageWidth} x {imageLength}, output {outputWidth} x {outputLength}");

        byte[] image2;
        int written = 0;
        switch (outputLengthScaling)
        {
        case -2:
        {
            image2 = new byte[checked(outputWidth * outputLength * 3)];
            int rowBytes = outputWidth * 3;
            for (int i = 0; i < outputLength; i++)
            {
                written += image_translate_row(state2, image2.AsSpan(written, rowBytes));
            }
            _ = outputFile.WriteEncodedStrip(0, image2, 0, written);
            break;
        }

        case -1:
        {
            image2 = new byte[checked(outputWidth * outputLength * 3)];
            int rowBytes = outputWidth * 3;
            for (int i = 0; i < outputLength; i++)
            {
                written += image_translate_row(state, image2.AsSpan(written, rowBytes));
            }
            _ = outputFile.WriteEncodedStrip(0, image2, 0, written);
            break;
        }

        default:
        {
            image2 = new byte[checked(outputWidth * outputLength / 8)];
            int rowBytes = outputWidth / 8;
            for (int i = 0; i < outputLength; i++)
            {
                written += image_translate_row(state, image2.AsSpan(written, rowBytes));
            }
            _ = outputFile.WriteEncodedStrip(0, image2, 0, written);
            break;
        }
        }

        _ = outputFile.WriteDirectory();
        outputFile.Close();
        image_translate_free(state);
        if (state2 is not null)
        {
            image_translate_free(state2);
        }
    }

    private static ImageTranslateState StateOrExit(ImageTranslateState? state)
    {
        if (state is null)
        {
            Exit(2);
        }
        return state;
    }

    private static int GetIntField(Tiff tiff, TiffTag tag, int fallback)
    {
        FieldValue[]? field = tiff.GetField(tag);
        return field is { Length: > 0 } ? field[0].ToInt() : fallback;
    }

    private static ushort GetUShortField(Tiff tiff, TiffTag tag, ushort fallback)
    {
        FieldValue[]? field = tiff.GetField(tag);
        return field is { Length: > 0 } ? field[0].ToUShort() : fallback;
    }

    private static float GetFloatField(Tiff tiff, TiffTag tag, float fallback)
    {
        FieldValue[]? field = tiff.GetField(tag);
        return field is { Length: > 0 } ? field[0].ToFloat() : fallback;
    }

    private static byte SaturateUInt8(ulong value) =>
        value > byte.MaxValue ? byte.MaxValue : (byte)value;

    private static ushort SaturateUInt16(ulong value) =>
        value > ushort.MaxValue ? ushort.MaxValue : (ushort)value;

    private static void WriteUInt16(byte[] buffer, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset, 2), value);

    private static void Exit(int code) => throw new TestExitException(code);

    private sealed class TestExitException : Exception
    {
        public TestExitException(int exitCode)
        {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }

    private sealed class ImageDescriptor
    {
        public byte[] Image { get; set; } = Array.Empty<byte>();
        public int Width { get; set; }
        public int Length { get; set; }
        public int CurrentRow { get; set; }
        public int BytesPerPixel { get; set; }
    }

}
