/*
 * TKFaxEngine - managed C# port
 *
 * generate_etsi_300_242_pages_adapter.cs
 * Direct managed port of the adapter and included ETSI ETS 300 242 page generator.
 * Original implementation by Steve Underwood.
 * GNU General Public License version 2.
 */

#nullable enable

using BitMiracle.LibTiff.Classic;
using System.Globalization;

namespace TKFaxEngineTest.System;

public static class GenerateEtsi300242PagesAdapter {
    private const int Width = 1728;
    private const int BytesPerRow = Width / 8;
    private const int T4_X_RESOLUTION_R8 = 8040;
    private const int T4_Y_RESOLUTION_STANDARD = 3850;

    private readonly record struct PageSpec(
        string Name,
        int XResolution,
        int YResolution,
        int Width,
        int Length,
        Compression Compression,
        int Type);

    private static readonly PageSpec[] Sequence =
    {
        new("etsi_300_242_a4_diago1.tif", T4_X_RESOLUTION_R8, T4_Y_RESOLUTION_STANDARD, Width, 1002, Compression.CCITTFAX3, 0),
        new("etsi_300_242_a4_diago2.tif", T4_X_RESOLUTION_R8, T4_Y_RESOLUTION_STANDARD, Width, 1002, Compression.CCITTFAX3, 1),
        new("etsi_300_242_a4_duration1.tif", T4_X_RESOLUTION_R8, T4_Y_RESOLUTION_STANDARD, Width, 237, Compression.CCITTFAX3, 2),
        new("etsi_300_242_a4_duration2.tif", T4_X_RESOLUTION_R8, T4_Y_RESOLUTION_STANDARD, Width, 237, Compression.CCITTFAX3, 3),
        new("etsi_300_242_a4_error.tif", T4_X_RESOLUTION_R8, T4_Y_RESOLUTION_STANDARD, Width, 400, Compression.CCITTFAX3, 4),
        new("etsi_300_242_a4_impress.tif", T4_X_RESOLUTION_R8, T4_Y_RESOLUTION_STANDARD, Width, 1079, Compression.CCITTFAX3, 5),
        new("etsi_300_242_a4_stairstep.tif", T4_X_RESOLUTION_R8, T4_Y_RESOLUTION_STANDARD, Width, 1728, Compression.CCITTFAX3, 6),
        new("etsi_300_242_a4_white.tif", T4_X_RESOLUTION_R8, T4_Y_RESOLUTION_STANDARD, Width, 1100, Compression.CCITTFAX3, 7),
        new("etsi_300_242_a4_white_2p.tif", T4_X_RESOLUTION_R8, T4_Y_RESOLUTION_STANDARD, Width, 1100, Compression.CCITTFAX3, 7),
        new("", T4_X_RESOLUTION_R8, T4_Y_RESOLUTION_STANDARD, Width, 1100, Compression.CCITTFAX3, 7),
        new("etsi_300_242_a4_impress_white.tif", T4_X_RESOLUTION_R8, T4_Y_RESOLUTION_STANDARD, Width, 1079, Compression.CCITTFAX3, 5),
        new("", T4_X_RESOLUTION_R8, T4_Y_RESOLUTION_STANDARD, Width, 1100, Compression.CCITTFAX3, 7)
    };

    private static Photometric _photoMetric = Photometric.MINISWHITE;
    private static FillOrder _fillOrder = FillOrder.LSB2MSB;

    private static void SetPixel(byte[] buffer, int row, int pixel) {
        row--;
        buffer[row * BytesPerRow + pixel / 8] |= (byte)(0x80 >> (pixel & 0x07));
    }

    private static void SetPixelRange(byte[] buffer, int row, int start, int end) {
        for (int i = start; i <= end; i++)
            SetPixel(buffer, row, i);
    }

    private static void ClearPixel(byte[] buffer, int row, int pixel) {
        row--;
        buffer[row * BytesPerRow + pixel / 8] &= (byte)~(0x80 >> (pixel & 0x07));
    }

    private static void ClearPixelRange(byte[] buffer, int row, int start, int end) {
        for (int i = start; i <= end; i++)
            ClearPixel(buffer, row, i);
    }

    private static void ClearRow(byte[] buffer, int width) {
        Array.Clear(buffer, 0, width / 8 + 1);
    }

    private static bool WriteScanline(Tiff tiffFile, byte[] buffer, int row) {
        if (tiffFile.WriteScanline(buffer, row, 0))
            return true;

        Console.WriteLine($"Write error at row {row}.");
        return false;
    }

    private static int CreateWhitePage(Tiff tiffFile) {
        byte[] imageBuffer = new byte[8192];
        for (int row = 0; row < 1100; row++) {
            ClearRow(imageBuffer, Width);
            if (!WriteScanline(tiffFile, imageBuffer, row))
                return -1;
        }
        return 1100;
    }

    private static int CreateStairstepPage(Tiff tiffFile) {
        byte[] imageBuffer = new byte[8192];
        int startPixel = 0;
        for (int row = 0; row < 1728; row++) {
            ClearRow(imageBuffer, Width);
            SetPixelRange(imageBuffer, 1, startPixel, startPixel + 63);
            if (_photoMetric != Photometric.MINISWHITE) {
                for (int i = 0; i < BytesPerRow; i++)
                    imageBuffer[i] = (byte)~imageBuffer[i];
            }

            if (!WriteScanline(tiffFile, imageBuffer, row))
                return -1;

            startPixel += 64;
            if (startPixel >= Width)
                startPixel = 0;
        }
        return 1728;
    }

    private static int CreateDiago1Page(Tiff tiffFile) {
        byte[] imageBuffer = new byte[BytesPerRow + 1];
        int row;
        for (row = 0; row < 1001; row++) {
            ClearRow(imageBuffer, Width);
            SetPixelRange(imageBuffer, 1, row, 1727);
            if (!WriteScanline(tiffFile, imageBuffer, row))
                return -1;
        }

        ClearRow(imageBuffer, Width);
        SetPixelRange(imageBuffer, 1, 0, 1727);
        if (!WriteScanline(tiffFile, imageBuffer, row))
            return -1;
        return 1002;
    }

    private static int CreateDiago2Page(Tiff tiffFile) {
        byte[] imageBuffer = new byte[BytesPerRow + 1];
        int row;
        for (row = 0; row < 1001; row++) {
            ClearRow(imageBuffer, Width);
            SetPixelRange(imageBuffer, 1, row + 728, 1727);
            if (!WriteScanline(tiffFile, imageBuffer, row))
                return -1;
        }

        ClearRow(imageBuffer, Width);
        SetPixelRange(imageBuffer, 1, 0, 1727);
        if (!WriteScanline(tiffFile, imageBuffer, row))
            return -1;
        return 1002;
    }

    private static int CreateImpressPage(Tiff tiffFile) {
        byte[] page = new byte[1079 * BytesPerRow];
        SetPixelRange(page, 1, 0, 1727);

        for (int row = 2; row <= 78; row++) {
            SetPixelRange(page, row, 850, 877);
            SetPixelRange(page, row, 1622, 1648);
        }
        for (int row = 80; row <= 117; row++) {
            for (int j = 0; j < 1728; j += 2)
                SetPixel(page, row, j);
        }
        for (int row = 118; row <= 155; row++) {
            for (int j = 1; j < 1728; j += 2)
                SetPixel(page, row, j);
        }
        for (int row = 194; row <= 231; row += 2)
            SetPixelRange(page, row, 0, 1727);
        for (int row = 270; row <= 276; row++)
            SetPixelRange(page, row, 60, 1667);
        for (int j = 0; j < 1728; j += 27)
            SetPixel(page, 315, j);
        for (int row = 354; row <= 480; row++)
            SetPixelRange(page, row, 209, 768);
        for (int row = 358; row <= 476; row++)
            ClearPixelRange(page, row, 488, 489);
        ClearPixelRange(page, 417, 217, 760);
        for (int row = 354; row <= 357; row++)
            SetPixelRange(page, row, 962, 1521);
        for (int row = 477; row <= 480; row++)
            SetPixelRange(page, row, 962, 1521);
        for (int row = 358; row <= 476; row++)
            SetPixelRange(page, row, 962, 969);
        for (int row = 358; row <= 476; row++)
            SetPixelRange(page, row, 1514, 1521);
        for (int row = 358; row <= 476; row++)
            SetPixel(page, row, 1241);
        SetPixelRange(page, 417, 970, 1513);
        for (int row = 354; row <= 1079; row++)
            SetPixel(page, row, 864);
        for (int row = 157; row <= 926; row++)
            SetPixelRange(page, row, 884, 899);

        for (int row = 0; row < 1079; row++) {
            if (!tiffFile.WriteScanline(page, row * BytesPerRow, row, 0)) {
                Console.WriteLine($"Write error at row {row}.");
                return -1;
            }
        }
        return 1079;
    }

    private static int CreateDuration1Page(Tiff tiffFile) {
        byte[] imageBuffer = new byte[BytesPerRow + 1];
        int row = 0;

        ClearRow(imageBuffer, Width);
        SetPixelRange(imageBuffer, 1, 0, 1727);
        if (!WriteScanline(tiffFile, imageBuffer, row++))
            return -1;

        for (; row < 117; row++) {
            ClearRow(imageBuffer, Width);
            if (!WriteScanline(tiffFile, imageBuffer, row))
                return -1;
        }

        ClearRow(imageBuffer, Width);
        SetPixelRange(imageBuffer, 1, 0, 1727);
        if (!WriteScanline(tiffFile, imageBuffer, row++))
            return -1;

        ClearRow(imageBuffer, Width);
        for (int i = 1; i < 1728; i += 2)
            SetPixel(imageBuffer, 1, i);
        if (!WriteScanline(tiffFile, imageBuffer, row++))
            return -1;

        ClearRow(imageBuffer, Width);
        SetPixelRange(imageBuffer, 1, 0, 1727);
        if (!WriteScanline(tiffFile, imageBuffer, row++))
            return -1;

        for (; row < 236; row++) {
            ClearRow(imageBuffer, Width);
            if (!WriteScanline(tiffFile, imageBuffer, row))
                return -1;
        }

        ClearRow(imageBuffer, Width);
        SetPixelRange(imageBuffer, 1, 0, 1727);
        if (!WriteScanline(tiffFile, imageBuffer, row))
            return -1;
        return 237;
    }

    private static int CreateDuration2Page(Tiff tiffFile) {
        return CreateDuration1Page(tiffFile);
    }

    private static int CreateErrorPage(Tiff tiffFile) {
        byte[] imageBuffer = new byte[BytesPerRow + 1];
        int row;
        for (row = 0; row < 68; row++) {
            ClearRow(imageBuffer, Width);
            if (!WriteScanline(tiffFile, imageBuffer, row))
                return -1;
        }

        ClearRow(imageBuffer, Width);
        SetPixelRange(imageBuffer, 1, 0, 1727);
        if (!WriteScanline(tiffFile, imageBuffer, row++))
            return -1;
        ClearRow(imageBuffer, Width);
        if (!WriteScanline(tiffFile, imageBuffer, row++))
            return -1;

        for (int i = 0; i < 10; i++) {
            for (int startPixel = 16; startPixel <= 1616; startPixel += 64) {
                ClearRow(imageBuffer, Width);
                SetPixelRange(imageBuffer, 1, startPixel, startPixel + 63);
                if (!WriteScanline(tiffFile, imageBuffer, row++))
                    return -1;
            }
        }

        ClearRow(imageBuffer, Width);
        if (!WriteScanline(tiffFile, imageBuffer, row++))
            return -1;
        ClearRow(imageBuffer, Width);
        SetPixelRange(imageBuffer, 1, 0, 1727);
        if (!WriteScanline(tiffFile, imageBuffer, row++))
            return -1;

        for (row = 332; row < 400; row++) {
            ClearRow(imageBuffer, Width);
            if (!WriteScanline(tiffFile, imageBuffer, row))
                return -1;
        }
        return 400;
    }

    public static int tkfx_generate_etsi_300_242_pages_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);
        string[] args = testArguments;

        _photoMetric = Photometric.MINISWHITE;
        _fillOrder = FillOrder.LSB2MSB;
        foreach (string arg in args) {
            switch (arg) {
                case "-i":
                    _photoMetric = Photometric.MINISBLACK;
                    break;
                case "-r":
                    _fillOrder = FillOrder.MSB2LSB;
                    break;
                default:
                    return 2;
            }
        }

        Tiff? tiffFile = null;
        try {
            for (int i = 0; i < Sequence.Length; i++) {
                PageSpec spec = Sequence[i];
                if (spec.Name.Length != 0) {
                    tiffFile?.Dispose();
                    tiffFile = Tiff.Open(spec.Name, "w");
                    if (tiffFile is null)
                        return 2;
                }

                if (tiffFile is null)
                    return 2;

                tiffFile.SetField(TiffTag.COMPRESSION, spec.Compression);
                if (spec.Compression == Compression.CCITTFAX3) {
                    tiffFile.SetField(TiffTag.GROUP3OPTIONS, Group3Opt.FILLBITS);
                    tiffFile.SetField(TiffTag.FAXMODE, FaxMode.CLASSF);
                }
                tiffFile.SetField(TiffTag.IMAGEWIDTH, spec.Width);
                tiffFile.SetField(TiffTag.BITSPERSAMPLE, 1);
                tiffFile.SetField(TiffTag.ORIENTATION, Orientation.TOPLEFT);
                tiffFile.SetField(TiffTag.SAMPLESPERPIXEL, 1);
                tiffFile.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
                tiffFile.SetField(TiffTag.PHOTOMETRIC, _photoMetric);
                tiffFile.SetField(TiffTag.FILLORDER, _fillOrder);
                tiffFile.SetField(TiffTag.XRESOLUTION, Math.Floor(spec.XResolution / 100.0 * 2.54 + 0.5));
                tiffFile.SetField(TiffTag.YRESOLUTION, Math.Floor(spec.YResolution / 100.0 * 2.54 + 0.5));
                tiffFile.SetField(TiffTag.RESOLUTIONUNIT, ResUnit.INCH);
                tiffFile.SetField(TiffTag.SOFTWARE, "TKFaxEngine");
                tiffFile.SetField(TiffTag.HOSTCOMPUTER, Environment.MachineName);
                tiffFile.SetField(TiffTag.IMAGEDESCRIPTION, "Blank test image");
                tiffFile.SetField(TiffTag.MAKE, "soft-switch.org");
                tiffFile.SetField(TiffTag.MODEL, "test data");
                tiffFile.SetField(TiffTag.DATETIME,
                    DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture));
                tiffFile.SetField(TiffTag.PAGENUMBER, 0, 1);
                tiffFile.SetField(TiffTag.CLEANFAXDATA, CleanFaxData.CLEAN);
                tiffFile.SetField(TiffTag.ROWSPERSTRIP, 128);
                tiffFile.SetField(TiffTag.IMAGELENGTH, spec.Length);
                tiffFile.CheckpointDirectory();

                int imageLength = spec.Type switch {
                    0 => CreateDiago1Page(tiffFile),
                    1 => CreateDiago2Page(tiffFile),
                    2 => CreateDuration1Page(tiffFile),
                    3 => CreateDuration2Page(tiffFile),
                    4 => CreateErrorPage(tiffFile),
                    5 => CreateImpressPage(tiffFile),
                    6 => CreateStairstepPage(tiffFile),
                    7 => CreateWhitePage(tiffFile),
                    _ => -1
                };

                if (imageLength != spec.Length) {
                    Console.WriteLine($"Length mismatch - {i}: {imageLength} vs {spec.Length}");
                    return 2;
                }
                if (!tiffFile.WriteDirectory())
                    return 2;
            }
        } finally {
            tiffFile?.Dispose();
        }

        return 0;
    }
}
