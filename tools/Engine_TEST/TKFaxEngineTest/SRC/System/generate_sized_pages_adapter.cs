/*
 * TKFaxEngine - managed C# port
 *
 * generate_sized_pages_adapter.cs
 * Direct managed port of the adapter and included generate_sized_pages.c body.
 * Original implementation by Steve Underwood.
 * GNU General Public License version 2.
 */

#nullable enable

using BitMiracle.LibTiff.Classic;
using System.Globalization;

namespace TKFaxEngineTest.System;

public static class GenerateSizedPagesAdapter {
    private readonly record struct PageSpec(
        string Name,
        int XResolution,
        int YResolution,
        int Width,
        int Length,
        int SquashingFactor);

    private static readonly PageSpec[] Sequence =
    {
        new("bilevel_R8_385_A4.tif", 8040, 3850, 1728, 1100, 1),
        new("bilevel_R8_385_B4.tif", 8040, 3850, 2048, 1200, 1),
        new("bilevel_R8_385_A3.tif", 8040, 3850, 2432, 1556, 1),
        new("bilevel_R8_77_A4.tif", 8040, 7700, 1728, 2200, 1),
        new("bilevel_R8_77_B4.tif", 8040, 7700, 2048, 2400, 1),
        new("bilevel_R8_77_A3.tif", 8040, 7700, 2432, 3112, 1),
        new("bilevel_R8_154_A4.tif", 8040, 15400, 1728, 4400, 1),
        new("bilevel_R8_154_B4.tif", 8040, 15400, 2048, 4800, 1),
        new("bilevel_R8_154_A3.tif", 8040, 15400, 2432, 6224, 1),
        new("bilevel_R16_154_A4.tif", 16080, 15400, 3456, 4400, 1),
        new("bilevel_R16_154_B4.tif", 16080, 15400, 4096, 4800, 1),
        new("bilevel_R16_154_A3.tif", 16080, 15400, 4864, 6224, 1),
        new("bilevel_200_100_A4.tif", 7874, 3937, 1728, 1100, 1),
        new("bilevel_200_100_B4.tif", 7874, 3937, 2048, 1200, 1),
        new("bilevel_200_100_A3.tif", 7874, 3937, 2432, 1556, 1),
        new("bilevel_200_200_A4.tif", 7874, 7874, 1728, 2200, 1),
        new("bilevel_200_200_B4.tif", 7874, 7874, 2048, 2400, 1),
        new("bilevel_200_200_A3.tif", 7874, 7874, 2432, 3112, 1),
        new("bilevel_200_400_A4.tif", 7874, 15748, 1728, 4400, 1),
        new("bilevel_200_400_B4.tif", 7874, 15748, 2048, 4800, 1),
        new("bilevel_200_400_A3.tif", 7874, 15748, 2432, 6224, 1),
        new("bilevel_300_300_A4.tif", 11811, 11811, 2592, 3300, 1),
        new("bilevel_300_300_B4.tif", 11811, 11811, 3072, 3600, 1),
        new("bilevel_300_300_A3.tif", 11811, 11811, 3648, 4668, 1),
        new("bilevel_300_600_A4.tif", 11811, 23622, 2592, 6600, 1),
        new("bilevel_300_600_B4.tif", 11811, 23622, 3072, 7200, 1),
        new("bilevel_300_600_A3.tif", 11811, 23622, 3648, 9336, 1),
        new("bilevel_400_400_A4.tif", 15748, 15748, 3456, 4400, 1),
        new("bilevel_400_400_B4.tif", 15748, 15748, 4096, 4800, 1),
        new("bilevel_400_400_A3.tif", 15748, 15748, 4864, 6224, 1),
        new("bilevel_400_800_A4.tif", 15748, 31496, 3456, 8800, 1),
        new("bilevel_400_800_B4.tif", 15748, 31496, 4096, 9600, 1),
        new("bilevel_400_800_A3.tif", 15748, 31496, 4864, 12448, 1),
        new("bilevel_600_600_A4.tif", 23622, 23622, 5184, 6600, 1),
        new("bilevel_600_600_B4.tif", 23622, 23622, 6144, 7200, 1),
        new("bilevel_600_600_A3.tif", 23622, 23622, 7296, 9336, 1),
        new("bilevel_600_1200_A4.tif", 23622, 47244, 5184, 13200, 1),
        new("bilevel_600_1200_B4.tif", 23622, 47244, 6144, 14400, 1),
        new("bilevel_600_1200_A3.tif", 23622, 47244, 7296, 18672, 1),
        new("bilevel_1200_1200_A4.tif", 47244, 47244, 10368, 13200, 1),
        new("bilevel_1200_1200_B4.tif", 47244, 47244, 12288, 14400, 1),
        new("bilevel_1200_1200_A3.tif", 47244, 47244, 14592, 18672, 1),
        new("bilevel_R8_77SQ_A4.tif", 8040, 3850, 1728, 1100, 2),
        new("bilevel_R8_77SQ_B4.tif", 8040, 3850, 2048, 1200, 2),
        new("bilevel_R8_77SQ_A3.tif", 8040, 3850, 2432, 1556, 2),
        new("bilevel_R8_154SQSQ_A4.tif", 8040, 3850, 1728, 1100, 4),
        new("bilevel_R8_154SQSQ_B4.tif", 8040, 3850, 2048, 1200, 4),
        new("bilevel_R8_154SQSQ_A3.tif", 8040, 3850, 2432, 1556, 4),
        new("bilevel_R8_154SQ_A4.tif", 8040, 7700, 1728, 2200, 2),
        new("bilevel_R8_154SQ_B4.tif", 8040, 7700, 2048, 2400, 2),
        new("bilevel_R8_154SQ_A3.tif", 8040, 7700, 2432, 3112, 2),
        new("bilevel_200_200SQ_A4.tif", 7874, 3937, 1728, 1100, 2),
        new("bilevel_200_200SQ_B4.tif", 7874, 3937, 2048, 1200, 2),
        new("bilevel_200_200SQ_A3.tif", 7874, 3937, 2432, 1556, 2),
        new("bilevel_200_400SQSQ_A4.tif", 7874, 3937, 1728, 1100, 4),
        new("bilevel_200_400SQSQ_B4.tif", 7874, 3937, 2048, 1200, 4),
        new("bilevel_200_400SQSQ_A3.tif", 7874, 3937, 2432, 1556, 4),
        new("bilevel_200_400SQ_A4.tif", 7874, 7874, 1728, 2200, 2),
        new("bilevel_200_400SQ_B4.tif", 7874, 7874, 2048, 2400, 2),
        new("bilevel_200_400SQ_A3.tif", 7874, 7874, 2432, 3112, 2),
    };

    private static void SetPixel(byte[] buffer, int row, int pixel) {
        row--;
        buffer[row * 1728 / 8 + pixel / 8] |= (byte)(0x80 >> (pixel & 0x07));
    }

    private static void SetPixelRange(byte[] buffer, int row, int start, int end) {
        for (int i = start; i <= end; i++)
            SetPixel(buffer, row, i);
    }

    private static void ClearRow(byte[] buffer, int width) {
        Array.Clear(buffer, 0, width / 8 + 1);
    }

    public static int tkfx_generate_sized_pages_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);
        string[] args = testArguments;

        Compression compression = Compression.CCITTFAX4;
        Group3Opt outputT4Options = (Group3Opt)0;
        Photometric photoMetric = Photometric.MINISWHITE;
        FillOrder fillOrder = FillOrder.LSB2MSB;

        foreach (string arg in args) {
            if (arg.Length < 2 || arg[0] != '-')
                return 2;

            for (int index = 1; index < arg.Length; index++) {
                switch (arg[index]) {
                    case '1':
                        compression = Compression.CCITTFAX3;
                        outputT4Options = Group3Opt.FILLBITS;
                        break;
                    case '2':
                        compression = Compression.CCITTFAX3;
                        outputT4Options = Group3Opt.FILLBITS | Group3Opt.ENCODING2D;
                        break;
                    case '6':
                        compression = Compression.CCITTFAX4;
                        outputT4Options = (Group3Opt)0;
                        break;
                    case 'i':
                        photoMetric = Photometric.MINISBLACK;
                        break;
                    case 'r':
                        fillOrder = FillOrder.MSB2LSB;
                        break;
                    default:
                        return 2;
                }
            }
        }

        byte[] imageBuffer = new byte[8192];
        foreach (PageSpec spec in Sequence) {
            Tiff? tiffFile = Tiff.Open(spec.Name, "w");
            if (tiffFile is null)
                return 2;

            using (tiffFile) {
                tiffFile.SetField(TiffTag.COMPRESSION, compression);
                if (outputT4Options != (Group3Opt)0)
                    tiffFile.SetField(TiffTag.GROUP3OPTIONS, outputT4Options);
                tiffFile.SetField(TiffTag.FAXMODE, FaxMode.CLASSF);
                tiffFile.SetField(TiffTag.IMAGEWIDTH, spec.Width);
                tiffFile.SetField(TiffTag.BITSPERSAMPLE, 1);
                tiffFile.SetField(TiffTag.ORIENTATION, Orientation.TOPLEFT);
                tiffFile.SetField(TiffTag.SAMPLESPERPIXEL, 1);
                tiffFile.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
                tiffFile.SetField(TiffTag.PHOTOMETRIC, photoMetric);
                tiffFile.SetField(TiffTag.FILLORDER, fillOrder);
                tiffFile.SetField(TiffTag.XRESOLUTION,
                    Math.Floor(spec.XResolution / 100.0 * 2.54 + 0.5));
                tiffFile.SetField(TiffTag.YRESOLUTION,
                    Math.Floor(spec.YResolution / 100.0 * 2.54 + 0.5));
                tiffFile.SetField(TiffTag.RESOLUTIONUNIT, ResUnit.INCH);
                tiffFile.SetField(TiffTag.HOSTCOMPUTER, Environment.MachineName);
                tiffFile.SetField(TiffTag.SOFTWARE, "TKFaxEngine");
                tiffFile.SetField(TiffTag.IMAGEDESCRIPTION, "Diagonally striped test image");
                tiffFile.SetField(TiffTag.MAKE, "soft-switch.org");
                tiffFile.SetField(TiffTag.MODEL, "testy");
                tiffFile.SetField(TiffTag.DATETIME,
                    DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture));
                tiffFile.SetField(TiffTag.ROWSPERSTRIP, spec.Length);
                tiffFile.SetField(TiffTag.IMAGELENGTH, spec.Length);
                tiffFile.SetField(TiffTag.PAGENUMBER, 0, 1);
                tiffFile.SetField(TiffTag.CLEANFAXDATA, CleanFaxData.CLEAN);
                tiffFile.CheckpointDirectory();

                for (int row = 0; row < spec.Length; row++) {
                    ClearRow(imageBuffer, spec.Width);
                    for (int j = 0; j < spec.SquashingFactor; j++) {
                        int k = row * spec.SquashingFactor + j;
                        if (((k / spec.Width) & 1) == 0)
                            SetPixelRange(imageBuffer, 1, k % spec.Width, spec.Width - 1);
                        else
                            SetPixelRange(imageBuffer, 1, 0, k % spec.Width);
                    }

                    if (!tiffFile.WriteScanline(imageBuffer, row, 0)) {
                        Console.WriteLine($"Write error at row {row}.");
                        return 2;
                    }
                }

                if (!tiffFile.WriteDirectory())
                    return 2;
            }
        }

        return 0;
    }
}
