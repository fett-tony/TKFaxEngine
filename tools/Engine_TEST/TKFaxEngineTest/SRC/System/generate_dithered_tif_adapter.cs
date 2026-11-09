/*
 * TKFaxEngine - managed C# port
 *
 * generate_dithered_tif_adapter.cs
 * Direct managed port of the adapter and included generate_dithered_tif.c body.
 * Original implementation by Steve Underwood.
 * GNU General Public License version 2.
 */

#nullable enable

using System.Globalization;
using BitMiracle.LibTiff.Classic;

namespace TKFaxEngineTest.System;

public static class GenerateDitheredTifAdapter {
    public static int tkfx_generate_dithered_tif_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);
        string[] args = testArguments;

        _ = args;
        const int imageWidth = 1728;
        const int imageLength = 2200;

        Tiff? tiffFile = Tiff.Open("dithered.tif", "w");
        if (tiffFile is null)
            return 2;

        using (tiffFile) {
            tiffFile.SetField(TiffTag.COMPRESSION, Compression.CCITTFAX3);
            tiffFile.SetField(TiffTag.GROUP3OPTIONS, Group3Opt.FILLBITS);
            tiffFile.SetField(TiffTag.FAXMODE, FaxMode.CLASSF);
            tiffFile.SetField(TiffTag.IMAGEWIDTH, imageWidth);
            tiffFile.SetField(TiffTag.BITSPERSAMPLE, 1);
            tiffFile.SetField(TiffTag.ORIENTATION, Orientation.TOPLEFT);
            tiffFile.SetField(TiffTag.SAMPLESPERPIXEL, 1);
            tiffFile.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
            tiffFile.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISWHITE);
            tiffFile.SetField(TiffTag.FILLORDER, FillOrder.LSB2MSB);
            tiffFile.SetField(TiffTag.XRESOLUTION, 204.0);
            tiffFile.SetField(TiffTag.YRESOLUTION, 196.0);
            tiffFile.SetField(TiffTag.RESOLUTIONUNIT, ResUnit.INCH);
            tiffFile.SetField(TiffTag.HOSTCOMPUTER, Environment.MachineName);
            tiffFile.SetField(TiffTag.SOFTWARE, "TKFaxEngine");
            tiffFile.SetField(TiffTag.IMAGEDESCRIPTION, "Checkerboard or dithered ones");
            tiffFile.SetField(TiffTag.MAKE, "soft-switch.org");
            tiffFile.SetField(TiffTag.MODEL, "testy");
            tiffFile.SetField(TiffTag.DATETIME,
                DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture));
            tiffFile.SetField(TiffTag.ROWSPERSTRIP, imageLength);
            tiffFile.SetField(TiffTag.IMAGELENGTH, imageLength);
            tiffFile.SetField(TiffTag.PAGENUMBER, 0, 1);
            tiffFile.SetField(TiffTag.CLEANFAXDATA, CleanFaxData.CLEAN);
            tiffFile.CheckpointDirectory();

            byte[] imageBuffer = new byte[imageWidth / 8 + 1];
            for (int row = 0; row < imageLength; row++) {
                Array.Fill(imageBuffer, (row & 1) == 0 ? (byte)0xAA : (byte)0x55);
                if (!tiffFile.WriteScanline(imageBuffer, row, 0)) {
                    Console.WriteLine($"Write error at row {row}.");
                    return 2;
                }
            }

            return tiffFile.WriteDirectory() ? 0 : 2;
        }
    }
}
