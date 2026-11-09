/*
 * TKFaxEngine - managed C# port
 *
 * generate_striped_pages_adapter.cs
 * Direct managed port of the adapter and included generate_striped_pages.c body.
 * Original implementation by Steve Underwood.
 * GNU General Public License version 2.
 */

#nullable enable

using BitMiracle.LibTiff.Classic;
using System.Globalization;

namespace TKFaxEngineTest.System;

public static class GenerateStripedPagesAdapter {
    private const int IMAGE_WIDTH = 1728;
    private const int IMAGE_LENGTH = 2600;
    private const int ROWS_PER_STRIPE = 37;

    public static int tkfx_generate_striped_pages_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);
        string[] args = testArguments;

        _ = args;
        Tiff? tiffFile = Tiff.Open("striped.tif", "w");
        if (tiffFile is null)
            return -1;

        using (tiffFile) {
            tiffFile.SetField(TiffTag.COMPRESSION, Compression.CCITTFAX4);
            tiffFile.SetField(TiffTag.BITSPERSAMPLE, 1);
            tiffFile.SetField(TiffTag.ORIENTATION, Orientation.TOPLEFT);
            tiffFile.SetField(TiffTag.SAMPLESPERPIXEL, 1);
            tiffFile.SetField(TiffTag.ROWSPERSTRIP, ROWS_PER_STRIPE);
            tiffFile.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
            tiffFile.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISWHITE);
            tiffFile.SetField(TiffTag.FILLORDER, FillOrder.LSB2MSB);
            tiffFile.SetField(TiffTag.XRESOLUTION, 204.0);
            tiffFile.SetField(TiffTag.YRESOLUTION, 196.0);
            tiffFile.SetField(TiffTag.RESOLUTIONUNIT, ResUnit.INCH);
            tiffFile.SetField(TiffTag.FAXSUBADDRESS, "1111");
            tiffFile.SetField(TiffTag.HOSTCOMPUTER, Environment.MachineName);
            tiffFile.SetField(TiffTag.SOFTWARE, "TKFaxEngine");
            tiffFile.SetField(TiffTag.IMAGEDESCRIPTION, "Image in stripes");
            tiffFile.SetField(TiffTag.MAKE, "soft-switch.org");
            tiffFile.SetField(TiffTag.MODEL, "testy");
            tiffFile.SetField(TiffTag.DATETIME,
                DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture));
            tiffFile.SetField(TiffTag.FAXRECVTIME, 10);
            tiffFile.SetField(TiffTag.IMAGEWIDTH, IMAGE_WIDTH);
            tiffFile.SetField(TiffTag.IMAGELENGTH, IMAGE_LENGTH);
            tiffFile.SetField(TiffTag.PAGENUMBER, 0, 1);
            tiffFile.CheckpointDirectory();

            byte[] imageBuffer = new byte[IMAGE_WIDTH * ROWS_PER_STRIPE / 8];
            Array.Fill(imageBuffer, (byte)0x18);
            for (int i = 0; i * ROWS_PER_STRIPE < IMAGE_LENGTH; i++) {
                int rows = Math.Min(ROWS_PER_STRIPE, IMAGE_LENGTH - i * ROWS_PER_STRIPE);
                int imageSize = IMAGE_WIDTH * rows / 8;
                if (tiffFile.WriteEncodedStrip(i, imageBuffer, imageSize) < 0)
                    return -1;
            }

            return tiffFile.WriteDirectory() ? 0 : -1;
        }
    }
}
