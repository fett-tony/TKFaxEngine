/*
 * TKFaxEngine - managed C# port
 *
 * t4_tx.cs
 *
 * Combined managed port of:
 *   t4_tx.c
 *   t4_tx.h
 *   private/t4_tx.h (merged into the supplied header)
 *   faxfont.h (header font table)
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2003, 2007, 2010 Steve Underwood.
 *
 * This port preserves the GNU Lesser General Public License version 2.1
 * licensing terms of the original source files.
 */

#nullable enable

using BitMiracle.LibTiff.Classic;
using System.Globalization;
using TKFaxEngine.FaxImage;

namespace TKFaxEngine.FaxImage;

/// <summary>Result codes returned by the image-format negotiation step.</summary>
public enum T4ImageFormatStatus {
    Ok = 0,
    Incompatible = -1,
    NoSizeSupport = -2,
    NoResolutionSupport = -3
}

public enum T4TxLogLevel {
    Flow,
    Warning
}

public enum T4TxPhotometric {
    MinIsWhite = 0,
    MinIsBlack = 1,
    Rgb = 2,
    Palette = 3,
    CieLab = 8,
    ItuLab = 10
}

public enum T4TxFillOrder {
    MostSignificantBitFirst = 1,
    LeastSignificantBitFirst = 2
}

/// <summary>Logging context used by the managed T.4 transmitter.</summary>
public sealed class T4TxLogger {
    public string Protocol { get; set; } = "T.4";
    public Action<T4TxLogLevel, string>? Sink { get; set; }
    public void Flow(string message) => Sink?.Invoke(T4TxLogLevel.Flow, message);
    public void Warning(string message) => Sink?.Invoke(T4TxLogLevel.Warning, message);
}

/// <summary>Metadata describing one source page or the negotiated wire page.</summary>
public sealed class T4TxMetadata {
    public t4_image_compression_t Compression { get; set; } = t4_image_compression_t.T4_COMPRESSION_NONE;
    public t4_image_types_t ImageType { get; set; } = t4_image_types_t.T4_IMAGE_TYPE_BILEVEL;
    public t4_image_support_t WidthCode { get; set; } = (t4_image_support_t)0;
    public int ImageWidth { get; set; }
    public int ImageLength { get; set; }
    public int XResolution { get; set; }
    public int YResolution { get; set; }
    public t4_image_resolution_t ResolutionCode { get; set; } = (t4_image_resolution_t)0;

    public T4TxMetadata Clone() => new() {
        Compression = Compression,
        ImageType = ImageType,
        WidthCode = WidthCode,
        ImageWidth = ImageWidth,
        ImageLength = ImageLength,
        XResolution = XResolution,
        YResolution = YResolution,
        ResolutionCode = ResolutionCode
    };

    public void CopyFrom(T4TxMetadata source) {
        ArgumentNullException.ThrowIfNull(source);
        Compression = source.Compression;
        ImageType = source.ImageType;
        WidthCode = source.WidthCode;
        ImageWidth = source.ImageWidth;
        ImageLength = source.ImageLength;
        XResolution = source.XResolution;
        YResolution = source.YResolution;
        ResolutionCode = source.ResolutionCode;
    }
}

/// <summary>
/// One page supplied by a managed document reader. Bilevel rows use the native
/// internal representation: one bit per pixel, least-significant bit first,
/// zero for white and one for black. Continuous-tone rows are unpacked.
/// </summary>
internal sealed class T4TxPage {
    private int _memoryRow;
    public t4_image_compression_t Compression { get; init; } = t4_image_compression_t.T4_COMPRESSION_UNCOMPRESSED;
    public t4_image_types_t ImageType { get; init; } = t4_image_types_t.T4_IMAGE_TYPE_BILEVEL;
    public int Width { get; init; }
    public int Length { get; init; }
    public int XResolution { get; init; } = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_R8;
    public int YResolution { get; init; } = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_FINE;
    public t4_image_resolution_t ResolutionCode { get; init; } = (t4_image_resolution_t)0;
    public T4TxPhotometric Photometric { get; init; } = T4TxPhotometric.MinIsWhite;
    public T4TxFillOrder FillOrder { get; init; } = T4TxFillOrder.LeastSignificantBitFirst;
    public t4_row_read_handler_t? RowReadHandler { get; init; }
    public object? RowReadUserData { get; init; }
    /// <summary>Optional decompressed row-major image data.</summary>
    public byte[] PixelData { get; init; } = Array.Empty<byte>();
    /// <summary>Optional source stride. Zero selects the format-derived stride.</summary>
    public int StrideBytes { get; init; }
    public int RequiredRowBytes => StrideBytes > 0
        ? StrideBytes
        : t4_tx_state_t.GetRowBytes(ImageType, Width);
    public void Restart() => _memoryRow = 0;
    public int ReadRow(Span<byte> destination, int len) {
        if (RowReadHandler is not null)
            return RowReadHandler(RowReadUserData, destination, len);

        Span<byte> destination_span = destination[..len];
        int rowBytes = RequiredRowBytes;
        if (_memoryRow >= Length || rowBytes <= 0 || destination_span.Length < rowBytes)
            return 0;

        int offset;
        try {
            offset = checked(_memoryRow * rowBytes);
        } catch (OverflowException) {
            return 0;
        }

        if (offset < 0 || offset > PixelData.Length - rowBytes)
            return 0;

        PixelData.AsSpan(offset, rowBytes).CopyTo(destination_span);
        _memoryRow++;
        return rowBytes;
    }

    public T4TxMetadata CreateMetadata() {
        t4_image_resolution_t code = ResolutionCode != (t4_image_resolution_t)0
            ? ResolutionCode
            : t4_tx_state_t.InferResolutionCode(XResolution, YResolution);

        return new T4TxMetadata {
            Compression = Compression,
            ImageType = ImageType,
            ImageWidth = Width,
            ImageLength = Length,
            XResolution = XResolution,
            YResolution = YResolution,
            ResolutionCode = code
        };
    }
}

/// <summary>Managed T.4 document transmitter state.</summary>
public sealed class t4_tx_state_t : IDisposable {
    internal const int EndOfData = -7;
    internal const int HeaderCharacterRows = 16;

    internal static readonly t4_image_compression_t ColourCompressions =
        t4_image_compression_t.T4_COMPRESSION_T42_T81 |
        t4_image_compression_t.T4_COMPRESSION_T43
        | t4_image_compression_t.T4_COMPRESSION_T45
        | t4_image_compression_t.T4_COMPRESSION_SYCC_T81;

    internal static readonly int[] XResolutionByBit =
    {
        (int)t4_image_x_resolution_t.T4_X_RESOLUTION_R8,
        (int)t4_image_x_resolution_t.T4_X_RESOLUTION_R8,
        (int)t4_image_x_resolution_t.T4_X_RESOLUTION_R8,
        (int)t4_image_x_resolution_t.T4_X_RESOLUTION_R16,
        (int)t4_image_x_resolution_t.T4_X_RESOLUTION_100,
        (int)t4_image_x_resolution_t.T4_X_RESOLUTION_200,
        (int)t4_image_x_resolution_t.T4_X_RESOLUTION_200,
        (int)t4_image_x_resolution_t.T4_X_RESOLUTION_200,
        (int)t4_image_x_resolution_t.T4_X_RESOLUTION_300,
        (int)t4_image_x_resolution_t.T4_X_RESOLUTION_300,
        (int)t4_image_x_resolution_t.T4_X_RESOLUTION_400,
        (int)t4_image_x_resolution_t.T4_X_RESOLUTION_400,
        (int)t4_image_x_resolution_t.T4_X_RESOLUTION_600,
        (int)t4_image_x_resolution_t.T4_X_RESOLUTION_600,
        (int)t4_image_x_resolution_t.T4_X_RESOLUTION_1200
    };

    internal static readonly int[] YResolutionByBit =
    {
        (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_STANDARD,
        (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_FINE,
        (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_SUPERFINE,
        (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_SUPERFINE,
        (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_100,
        (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_100,
        (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_200,
        (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_400,
        (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_300,
        (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_600,
        (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_400,
        (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_800,
        (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_600,
        (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_1200,
        (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_1200
    };

    internal static readonly float[] XResolutionTable =
    {
        100.0f * 100.0f / 2.54f,
        102.0f * 100.0f / 2.54f,
        200.0f * 100.0f / 2.54f,
        204.0f * 100.0f / 2.54f,
        300.0f * 100.0f / 2.54f,
        400.0f * 100.0f / 2.54f,
        408.0f * 100.0f / 2.54f,
        600.0f * 100.0f / 2.54f,
        1200.0f * 100.0f / 2.54f
    };

    internal static readonly float[] YResolutionTable =
    {
        38.50f * 100.0f,
        100.0f * 100.0f / 2.54f,
        77.00f * 100.0f,
        200.0f * 100.0f / 2.54f,
        300.0f * 100.0f / 2.54f,
        154.00f * 100.0f,
        400.0f * 100.0f / 2.54f,
        600.0f * 100.0f / 2.54f,
        800.0f * 100.0f / 2.54f,
        1200.0f * 100.0f / 2.54f
    };

    internal static readonly t4_image_resolution_t[,] ResolutionMap =
    {
        { 0, 0, 0, t4_image_resolution_t.T4_RESOLUTION_R8_STANDARD, 0, 0, 0, 0, 0 },
        { t4_image_resolution_t.T4_RESOLUTION_100_100, 0, t4_image_resolution_t.T4_RESOLUTION_200_100, 0, 0, 0, 0, 0, 0 },
        { 0, 0, 0, t4_image_resolution_t.T4_RESOLUTION_R8_FINE, 0, 0, 0, 0, 0 },
        { 0, 0, t4_image_resolution_t.T4_RESOLUTION_200_200, 0, 0, 0, 0, 0, 0 },
        { 0, 0, 0, 0, t4_image_resolution_t.T4_RESOLUTION_300_300, 0, 0, 0, 0 },
        { 0, 0, 0, t4_image_resolution_t.T4_RESOLUTION_R8_SUPERFINE, 0, 0, t4_image_resolution_t.T4_RESOLUTION_R16_SUPERFINE, 0, 0 },
        { 0, 0, t4_image_resolution_t.T4_RESOLUTION_200_400, 0, 0, t4_image_resolution_t.T4_RESOLUTION_400_400, 0, 0, 0 },
        { 0, 0, 0, 0, t4_image_resolution_t.T4_RESOLUTION_300_600, 0, 0, t4_image_resolution_t.T4_RESOLUTION_600_600, 0 },
        { 0, 0, 0, 0, 0, t4_image_resolution_t.T4_RESOLUTION_400_800, 0, 0, 0 },
        { 0, 0, 0, 0, 0, 0, 0, t4_image_resolution_t.T4_RESOLUTION_600_1200, t4_image_resolution_t.T4_RESOLUTION_1200_1200 }
    };

    internal static readonly WidthResolutionInfo[] WidthAndResolutionInfo = CreateWidthResolutionInfo();

    internal static readonly SquashInfo[] Squashable =
    {
        new(
            t4_image_resolution_t.T4_RESOLUTION_200_400,
            new ResolutionFallback(t4_image_resolution_t.T4_RESOLUTION_200_200, 2),
            new ResolutionFallback(t4_image_resolution_t.T4_RESOLUTION_R8_FINE, 2),
            new ResolutionFallback(t4_image_resolution_t.T4_RESOLUTION_200_100, 4),
            new ResolutionFallback(t4_image_resolution_t.T4_RESOLUTION_R8_STANDARD, 4)),
        new(
            t4_image_resolution_t.T4_RESOLUTION_200_200,
            new ResolutionFallback(t4_image_resolution_t.T4_RESOLUTION_200_100, 2),
            new ResolutionFallback(t4_image_resolution_t.T4_RESOLUTION_R8_STANDARD, 2)),
        new(
            t4_image_resolution_t.T4_RESOLUTION_R8_SUPERFINE,
            new ResolutionFallback(t4_image_resolution_t.T4_RESOLUTION_R8_FINE, 2),
            new ResolutionFallback(t4_image_resolution_t.T4_RESOLUTION_200_200, 2),
            new ResolutionFallback(t4_image_resolution_t.T4_RESOLUTION_R8_STANDARD, 4),
            new ResolutionFallback(t4_image_resolution_t.T4_RESOLUTION_200_100, 4)),
        new(
            t4_image_resolution_t.T4_RESOLUTION_R8_FINE,
            new ResolutionFallback(t4_image_resolution_t.T4_RESOLUTION_R8_STANDARD, 2),
            new ResolutionFallback(t4_image_resolution_t.T4_RESOLUTION_200_100, 2))
    };

    internal Tiff? _tiff;
    internal t4_t6_encode_state_t? _t4T6Encoder;
    internal T85EncodeState? _t85Encoder;
    internal T42EncodeState? _t42Encoder;
    internal T43EncodeState? _t43Encoder;
    internal t4_image_compression_t _encoderCompression = (t4_image_compression_t)0;
    internal byte[] _noEncoderBuffer = Array.Empty<byte>();
    internal int _noEncoderBufferLength;
    internal int _noEncoderBufferPointer;
    internal int _noEncoderBit;
    internal byte[] _colourMap = Array.Empty<byte>();
    internal int _colourMapEntries;
    internal readonly byte[] _bitBuffer = new byte[1];
    internal int _bitBufferValue;
    internal int _bitBufferBits;
    internal T4TxPage? _sourcePage;
    internal readonly ImageTranslateState _translator = new();

    internal t4_row_read_handler_t? _rowHandler;
    internal object? _rowHandlerUserData;
    internal t4_row_read_handler_t? _imageRowHandler;
    internal object? _imageRowUserData;

    internal byte[] _sourceRowBuffer = Array.Empty<byte>();
    internal byte[] _extraRowBuffer = Array.Empty<byte>();
    internal int _sourceRowsRead;
    internal int _rowSquashingRatio = 1;

    internal string? _headerText;
    internal int _headerRow;
    internal int _headerRows;
    internal int _headerXRepeats = 1;
    internal int _headerYRepeats = 1;

    internal int _minimumBitsPerRow;
    internal int _maximum2DRowsPer1DRow;
    internal bool _formatNegotiated;
    internal bool _pageOpen;
    internal bool _released;
    internal bool _disposed;

    public t4_tx_state_t() {
        _rowHandler = t4_tx.tiff_row_read_handler;
        _rowHandlerUserData = this;
    }

    public T4TxLogger Logging { get; } = new();

    public T4TxMetadata SourceMetadata { get; } = new();

    public T4TxMetadata Metadata { get; } = new();

    public int StartPageNumber { get; internal set; }

    public int StopPageNumber { get; internal set; } = int.MaxValue;

    public int CurrentPageNumber { get; internal set; }

    public int PagesInFile { get; internal set; } = -1;

    public bool HeaderOverlaysImage { get; internal set; }

    public string? HeaderInfo { get; internal set; }

    public string? LocalIdent { get; internal set; }

    public TimeZoneInfo? HeaderTimeZone { get; internal set; }

    public bool PageOpen => _pageOpen;

    public string? SourceFile { get; internal set; }

    public void Dispose() {
        if (_disposed)
            return;
        t4_tx.t4_tx_release(this);
        _translator.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    internal static int GetRowBytes(t4_image_types_t imageType, int width) {
        if (width <= 0)
            return 0;
        return imageType switch {
            t4_image_types_t.T4_IMAGE_TYPE_BILEVEL => checked((width + 7) / 8),
            t4_image_types_t.T4_IMAGE_TYPE_COLOUR_BILEVEL => checked(width * 3),
            t4_image_types_t.T4_IMAGE_TYPE_4COLOUR_BILEVEL => checked(width * 4),
            t4_image_types_t.T4_IMAGE_TYPE_GRAY_8BIT => width,
            t4_image_types_t.T4_IMAGE_TYPE_GRAY_12BIT => checked(width * 2),
            t4_image_types_t.T4_IMAGE_TYPE_COLOUR_8BIT => checked(width * 3),
            t4_image_types_t.T4_IMAGE_TYPE_4COLOUR_8BIT => checked(width * 4),
            t4_image_types_t.T4_IMAGE_TYPE_COLOUR_12BIT => checked(width * 6),
            t4_image_types_t.T4_IMAGE_TYPE_4COLOUR_12BIT => checked(width * 8),
            _ => 0
        };
    }

    internal static t4_image_resolution_t InferResolutionCode(int xResolution, int yResolution) {
        int x = match_resolution(xResolution, XResolutionTable);
        int y = match_resolution(yResolution, YResolutionTable);
        if (x < 0 || y < 0)
            return (t4_image_resolution_t)0;
        return ResolutionMap[y, x];
    }

    internal int EncoderImageWidth => _encoderCompression switch {
        t4_image_compression_t.T4_COMPRESSION_T4_1D or t4_image_compression_t.T4_COMPRESSION_T4_2D or t4_image_compression_t.T4_COMPRESSION_T6 =>
            _t4T6Encoder?.image_width ?? Metadata.ImageWidth,
        t4_image_compression_t.T4_COMPRESSION_T85 or t4_image_compression_t.T4_COMPRESSION_T85_L0 =>
            checked((int)(_t85Encoder?.ImageWidth ??
                (uint)Math.Max(Metadata.ImageWidth, 0))),
        t4_image_compression_t.T4_COMPRESSION_T42_T81 or t4_image_compression_t.T4_COMPRESSION_SYCC_T81 =>
            _t42Encoder is null
                ? Math.Max(Metadata.ImageWidth, 0)
                : checked((int)T42.t42_encode_get_image_width(_t42Encoder)),
        t4_image_compression_t.T4_COMPRESSION_T43 =>
            _t43Encoder is null
                ? Math.Max(Metadata.ImageWidth, 0)
                : checked((int)T43.t43_encode_get_image_width(_t43Encoder)),
        _ => Metadata.ImageWidth
    };

    internal int EncoderImageLength => _encoderCompression switch {
        t4_image_compression_t.T4_COMPRESSION_T4_1D or t4_image_compression_t.T4_COMPRESSION_T4_2D or t4_image_compression_t.T4_COMPRESSION_T6 =>
            _t4T6Encoder?.image_length ?? Metadata.ImageLength,
        t4_image_compression_t.T4_COMPRESSION_T85 or t4_image_compression_t.T4_COMPRESSION_T85_L0 =>
            checked((int)(_t85Encoder?.ImageLength ??
                (uint)Math.Max(Metadata.ImageLength, 0))),
        t4_image_compression_t.T4_COMPRESSION_T42_T81 or t4_image_compression_t.T4_COMPRESSION_SYCC_T81 =>
            _t42Encoder is null
                ? Math.Max(Metadata.ImageLength, 0)
                : checked((int)T42.t42_encode_get_image_length(_t42Encoder)),
        t4_image_compression_t.T4_COMPRESSION_T43 =>
            _t43Encoder is null
                ? Math.Max(Metadata.ImageLength, 0)
                : checked((int)T43.t43_encode_get_image_length(_t43Encoder)),
        _ => Metadata.ImageLength
    };

    internal long EncoderCompressedImageSizeBits => _encoderCompression switch {
        t4_image_compression_t.T4_COMPRESSION_T4_1D or t4_image_compression_t.T4_COMPRESSION_T4_2D or t4_image_compression_t.T4_COMPRESSION_T6 =>
            _t4T6Encoder?.compressed_image_size ?? 0,
        t4_image_compression_t.T4_COMPRESSION_T85 or t4_image_compression_t.T4_COMPRESSION_T85_L0 =>
            checked((long)(_t85Encoder?.CompressedImageSizeBytes ?? 0) * 8L),
        t4_image_compression_t.T4_COMPRESSION_T42_T81 or t4_image_compression_t.T4_COMPRESSION_SYCC_T81 =>
            _t42Encoder is null
                ? 0
                : checked((long)T42.t42_encode_get_compressed_image_size(_t42Encoder) * 8L),
        t4_image_compression_t.T4_COMPRESSION_T43 =>
            _t43Encoder is null
                ? 0
                : T43.t43_encode_get_compressed_image_size(_t43Encoder),
        _ => 0
    };

    internal static int ToT4T6Compression(
        t4_image_compression_t compression) =>
        compression switch {
            t4_image_compression_t.T4_COMPRESSION_T4_1D => t4_rx.T4_COMPRESSION_T4_1D,
            t4_image_compression_t.T4_COMPRESSION_T4_2D => t4_rx.T4_COMPRESSION_T4_2D,
            t4_image_compression_t.T4_COMPRESSION_T6 => t4_rx.T4_COMPRESSION_T6,
            _ => t4_rx.T4_COMPRESSION_NONE
        };

    internal static int ToT43ImageType(t4_image_types_t imageType) =>
        imageType switch {
            t4_image_types_t.T4_IMAGE_TYPE_COLOUR_BILEVEL =>
                (int)T43.T43_IMAGE_TYPE_RGB_BILEVEL,
            t4_image_types_t.T4_IMAGE_TYPE_4COLOUR_BILEVEL =>
                (int)T43.T43_IMAGE_TYPE_CMYK_BILEVEL,
            t4_image_types_t.T4_IMAGE_TYPE_GRAY_8BIT or t4_image_types_t.T4_IMAGE_TYPE_GRAY_12BIT =>
                (int)T43.T43_IMAGE_TYPE_GRAY,
            t4_image_types_t.T4_IMAGE_TYPE_COLOUR_8BIT or t4_image_types_t.T4_IMAGE_TYPE_COLOUR_12BIT =>
                (int)T43.T43_IMAGE_TYPE_COLOUR,
            _ =>
                (int)T43.T43_IMAGE_TYPE_8BIT_COLOUR_PALETTE
        };

    internal static int get_tiff_total_pages(Tiff tiff) {
        if (!tiff.SetDirectory(0))
            return 0;

        int count = 0;
        do {
            count++;
        } while (tiff.ReadDirectory());

        tiff.SetDirectory(0);
        return count;
    }

    internal void GetHeaderScale(out int xRepeats, out int yRepeats) {
        switch (Metadata.ResolutionCode) {
            default:
            case t4_image_resolution_t.T4_RESOLUTION_100_100:
                xRepeats = 1;
                yRepeats = 1;
                break;
            case t4_image_resolution_t.T4_RESOLUTION_R8_STANDARD:
            case t4_image_resolution_t.T4_RESOLUTION_200_100:
                xRepeats = 2;
                yRepeats = 1;
                break;
            case t4_image_resolution_t.T4_RESOLUTION_R8_FINE:
            case t4_image_resolution_t.T4_RESOLUTION_200_200:
                xRepeats = 2;
                yRepeats = 2;
                break;
            case t4_image_resolution_t.T4_RESOLUTION_300_300:
                xRepeats = 3;
                yRepeats = 3;
                break;
            case t4_image_resolution_t.T4_RESOLUTION_R8_SUPERFINE:
            case t4_image_resolution_t.T4_RESOLUTION_200_400:
                xRepeats = 2;
                yRepeats = 4;
                break;
            case t4_image_resolution_t.T4_RESOLUTION_R16_SUPERFINE:
            case t4_image_resolution_t.T4_RESOLUTION_400_400:
                xRepeats = 4;
                yRepeats = 4;
                break;
            case t4_image_resolution_t.T4_RESOLUTION_400_800:
                xRepeats = 4;
                yRepeats = 8;
                break;
            case t4_image_resolution_t.T4_RESOLUTION_300_600:
                xRepeats = 3;
                yRepeats = 6;
                break;
            case t4_image_resolution_t.T4_RESOLUTION_600_600:
                xRepeats = 6;
                yRepeats = 6;
                break;
            case t4_image_resolution_t.T4_RESOLUTION_600_1200:
                xRepeats = 6;
                yRepeats = 12;
                break;
            case t4_image_resolution_t.T4_RESOLUTION_1200_1200:
                xRepeats = 12;
                yRepeats = 12;
                break;
        }

        if (Metadata.WidthCode == t4_image_support_t.T4_SUPPORT_WIDTH_255MM)
            xRepeats *= 2;
        else if (Metadata.WidthCode == t4_image_support_t.T4_SUPPORT_WIDTH_303MM)
            xRepeats *= 3;
    }

    internal void DrawHeaderRow(Span<byte> destination, int outputHeaderRow) {
        if (_headerText is null)
            return;

        int fontRow = outputHeaderRow / Math.Max(_headerYRepeats, 1);
        fontRow = Math.Clamp(fontRow, 0, 15);

        switch (Metadata.ImageType) {
            case t4_image_types_t.T4_IMAGE_TYPE_BILEVEL:
                DrawBilevelHeader(destination, fontRow);
                break;
            case t4_image_types_t.T4_IMAGE_TYPE_GRAY_8BIT:
                DrawByteHeader(destination, fontRow, 1);
                break;
            case t4_image_types_t.T4_IMAGE_TYPE_COLOUR_8BIT:
                DrawByteHeader(destination, fontRow, 3);
                break;
            default:
                break;
        }
    }

    internal void DrawBilevelHeader(Span<byte> destination, int fontRow) {
        destination.Clear();
        int pixel = 0;
        foreach (char character in _headerText!) {
            ushort pattern = HeaderFont[(byte)character * 16 + fontRow];
            for (int sourcePixel = 0; sourcePixel < 16; sourcePixel++) {
                bool black = (pattern & (0x8000 >> sourcePixel)) != 0;
                for (int repeat = 0; repeat < _headerXRepeats; repeat++) {
                    if (pixel >= Metadata.ImageWidth)
                        return;
                    if (black)
                        destination[pixel >> 3] |= (byte)(1 << (pixel & 7));
                    pixel++;
                }
            }
        }
    }

    internal void DrawByteHeader(Span<byte> destination, int fontRow, int components) {
        FillWhite(destination, Metadata.ImageType);
        int pixel = 0;
        foreach (char character in _headerText!) {
            ushort pattern = HeaderFont[(byte)character * 16 + fontRow];
            for (int sourcePixel = 0; sourcePixel < 16; sourcePixel++) {
                byte value = (pattern & (0x8000 >> sourcePixel)) != 0 ? (byte)0 : (byte)255;
                for (int repeat = 0; repeat < _headerXRepeats; repeat++) {
                    if (pixel >= Metadata.ImageWidth)
                        return;
                    int offset = pixel * components;
                    for (int component = 0; component < components && offset + component < destination.Length; component++)
                        destination[offset + component] = value;
                    pixel++;
                }
            }
        }
    }

    internal static t4_image_compression_t SelectBilevelCompression(t4_image_compression_t supported) {
        if ((supported & t4_image_compression_t.T4_COMPRESSION_T85_L0) != 0)
            return t4_image_compression_t.T4_COMPRESSION_T85_L0;
        if ((supported & t4_image_compression_t.T4_COMPRESSION_T85) != 0)
            return t4_image_compression_t.T4_COMPRESSION_T85;
        if ((supported & t4_image_compression_t.T4_COMPRESSION_T6) != 0)
            return t4_image_compression_t.T4_COMPRESSION_T6;
        if ((supported & t4_image_compression_t.T4_COMPRESSION_T4_2D) != 0)
            return t4_image_compression_t.T4_COMPRESSION_T4_2D;
        if ((supported & t4_image_compression_t.T4_COMPRESSION_T4_1D) != 0)
            return t4_image_compression_t.T4_COMPRESSION_T4_1D;
        return (t4_image_compression_t)0;
    }

    internal static int FindExactWidthResolution(int width, t4_image_resolution_t resolution) {
        for (int index = 0; index < WidthAndResolutionInfo.Length; index++) {
            WidthResolutionInfo info = WidthAndResolutionInfo[index];
            if (info.Width == width && info.ResolutionCode == resolution)
                return index;
        }
        return -1;
    }

    internal static int FindPaddedWidthResolution(
        int width,
        t4_image_resolution_t resolution,
        t4_image_support_t supportedSizes) {
        for (int index = 0; index < WidthAndResolutionInfo.Length; index++) {
            WidthResolutionInfo info = WidthAndResolutionInfo[index];
            if (info.Width < width)
                continue;
            if (info.ResolutionCode != resolution && info.AlternateResolutionCode != resolution)
                continue;
            if ((supportedSizes & info.WidthCode) == 0)
                continue;
            return index;
        }
        return -1;
    }

    internal static int code_to_x_resolution(t4_image_resolution_t code) {
        int bit = HighestSetBit((int)code);
        return (uint)bit < (uint)XResolutionByBit.Length ? XResolutionByBit[bit] : 0;
    }

    internal static int code_to_y_resolution(t4_image_resolution_t code) {
        int bit = HighestSetBit((int)code);
        return (uint)bit < (uint)YResolutionByBit.Length ? YResolutionByBit[bit] : 0;
    }

    internal static int HighestSetBit(int value) {
        if (value <= 0)
            return -1;
        int bit = -1;
        while (value != 0) {
            value >>= 1;
            bit++;
        }
        return bit;
    }

    internal static int match_resolution(float actual, IReadOnlyList<float> table) {
        if (actual == 0.0f)
            return -1;
        float bestRatio = 0.0f;
        int bestEntry = -1;
        for (int index = 0; index < table.Count; index++) {
            float ratio = actual > table[index] ? table[index] / actual : actual / table[index];
            if (ratio > bestRatio) {
                bestRatio = ratio;
                bestEntry = index;
            }
        }
        return bestRatio < 0.95f ? -1 : bestEntry;
    }

    internal static t4_image_resolution_t FirstResolution(t4_image_resolution_t resolutions) {
        int value = (int)resolutions;
        if (value == 0)
            return (t4_image_resolution_t)0;
        int lowest = value & -value;
        return (t4_image_resolution_t)lowest;
    }

    internal static bool IsColourType(t4_image_types_t type) => type is
        t4_image_types_t.T4_IMAGE_TYPE_COLOUR_BILEVEL or
        t4_image_types_t.T4_IMAGE_TYPE_4COLOUR_BILEVEL or
        t4_image_types_t.T4_IMAGE_TYPE_COLOUR_8BIT or
        t4_image_types_t.T4_IMAGE_TYPE_4COLOUR_8BIT or
        t4_image_types_t.T4_IMAGE_TYPE_COLOUR_12BIT or
        t4_image_types_t.T4_IMAGE_TYPE_4COLOUR_12BIT;

    internal static bool IsGrayType(t4_image_types_t type) => type is
        t4_image_types_t.T4_IMAGE_TYPE_GRAY_8BIT or t4_image_types_t.T4_IMAGE_TYPE_GRAY_12BIT;

    internal static t4_image_support_t WidthCodeForWidth(int width) {
        foreach (WidthResolutionInfo info in WidthAndResolutionInfo) {
            if (info.Width == width)
                return info.WidthCode;
        }
        return (t4_image_support_t)0;
    }

    internal static string? NormalizeOptional(string? value, int maximumLength) {
        if (string.IsNullOrEmpty(value))
            return null;
        return value.Length <= maximumLength ? value : value[..maximumLength];
    }

    internal static byte[] EnsureBuffer(byte[] source, int length) =>
        source.Length >= length ? source : new byte[length];

    internal static void FillWhite(Span<byte> destination, t4_image_types_t imageType) {
        if (imageType == t4_image_types_t.T4_IMAGE_TYPE_BILEVEL)
            destination.Clear();
        else
            destination.Fill(0xFF);
    }

    internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    internal readonly struct WidthResolutionInfo {
        public WidthResolutionInfo(
            int width,
            t4_image_support_t widthCode,
            t4_image_resolution_t resolutionCode,
            t4_image_resolution_t alternateResolutionCode = (t4_image_resolution_t)0) {
            Width = width;
            WidthCode = widthCode;
            ResolutionCode = resolutionCode;
            AlternateResolutionCode = alternateResolutionCode;
        }

        public int Width { get; }
        public t4_image_support_t WidthCode { get; }
        public t4_image_resolution_t ResolutionCode { get; }
        public t4_image_resolution_t AlternateResolutionCode { get; }
    }

    internal readonly struct ResolutionFallback {
        public ResolutionFallback(t4_image_resolution_t resolution, int squashingFactor) {
            Resolution = resolution;
            SquashingFactor = squashingFactor;
        }

        public t4_image_resolution_t Resolution { get; }
        public int SquashingFactor { get; }
    }

    internal sealed class SquashInfo {
        public SquashInfo(t4_image_resolution_t sourceResolution, params ResolutionFallback[] fallbacks) {
            SourceResolution = sourceResolution;
            Fallbacks = fallbacks;
        }

        public t4_image_resolution_t SourceResolution { get; }
        public IReadOnlyList<ResolutionFallback> Fallbacks { get; }
    }

    internal static WidthResolutionInfo[] CreateWidthResolutionInfo() =>
    [
        new((int)t4_image_width_t.T4_WIDTH_100_A4, t4_image_support_t.T4_SUPPORT_WIDTH_215MM, t4_image_resolution_t.T4_RESOLUTION_100_100),
        new((int)t4_image_width_t.T4_WIDTH_100_B4, t4_image_support_t.T4_SUPPORT_WIDTH_255MM, t4_image_resolution_t.T4_RESOLUTION_100_100),
        new((int)t4_image_width_t.T4_WIDTH_100_A3, t4_image_support_t.T4_SUPPORT_WIDTH_303MM, t4_image_resolution_t.T4_RESOLUTION_100_100),

        new((int)t4_image_width_t.T4_WIDTH_200_A4, t4_image_support_t.T4_SUPPORT_WIDTH_215MM, t4_image_resolution_t.T4_RESOLUTION_200_100, t4_image_resolution_t.T4_RESOLUTION_R8_STANDARD),
        new((int)t4_image_width_t.T4_WIDTH_200_A4, t4_image_support_t.T4_SUPPORT_WIDTH_215MM, t4_image_resolution_t.T4_RESOLUTION_200_200, t4_image_resolution_t.T4_RESOLUTION_R8_FINE),
        new((int)t4_image_width_t.T4_WIDTH_200_A4, t4_image_support_t.T4_SUPPORT_WIDTH_215MM, t4_image_resolution_t.T4_RESOLUTION_200_400, t4_image_resolution_t.T4_RESOLUTION_R8_SUPERFINE),
        new((int)t4_image_width_t.T4_WIDTH_200_A4, t4_image_support_t.T4_SUPPORT_WIDTH_215MM, t4_image_resolution_t.T4_RESOLUTION_R8_STANDARD, t4_image_resolution_t.T4_RESOLUTION_200_100),
        new((int)t4_image_width_t.T4_WIDTH_200_A4, t4_image_support_t.T4_SUPPORT_WIDTH_215MM, t4_image_resolution_t.T4_RESOLUTION_R8_FINE, t4_image_resolution_t.T4_RESOLUTION_200_200),
        new((int)t4_image_width_t.T4_WIDTH_200_A4, t4_image_support_t.T4_SUPPORT_WIDTH_215MM, t4_image_resolution_t.T4_RESOLUTION_R8_SUPERFINE, t4_image_resolution_t.T4_RESOLUTION_200_400),

        new((int)t4_image_width_t.T4_WIDTH_200_B4, t4_image_support_t.T4_SUPPORT_WIDTH_255MM, t4_image_resolution_t.T4_RESOLUTION_200_100, t4_image_resolution_t.T4_RESOLUTION_R8_STANDARD),
        new((int)t4_image_width_t.T4_WIDTH_200_B4, t4_image_support_t.T4_SUPPORT_WIDTH_255MM, t4_image_resolution_t.T4_RESOLUTION_200_200, t4_image_resolution_t.T4_RESOLUTION_R8_FINE),
        new((int)t4_image_width_t.T4_WIDTH_200_B4, t4_image_support_t.T4_SUPPORT_WIDTH_255MM, t4_image_resolution_t.T4_RESOLUTION_200_400, t4_image_resolution_t.T4_RESOLUTION_R8_SUPERFINE),
        new((int)t4_image_width_t.T4_WIDTH_200_B4, t4_image_support_t.T4_SUPPORT_WIDTH_255MM, t4_image_resolution_t.T4_RESOLUTION_R8_STANDARD, t4_image_resolution_t.T4_RESOLUTION_200_100),
        new((int)t4_image_width_t.T4_WIDTH_200_B4, t4_image_support_t.T4_SUPPORT_WIDTH_255MM, t4_image_resolution_t.T4_RESOLUTION_R8_FINE, t4_image_resolution_t.T4_RESOLUTION_200_200),
        new((int)t4_image_width_t.T4_WIDTH_200_B4, t4_image_support_t.T4_SUPPORT_WIDTH_255MM, t4_image_resolution_t.T4_RESOLUTION_R8_SUPERFINE, t4_image_resolution_t.T4_RESOLUTION_200_400),

        new((int)t4_image_width_t.T4_WIDTH_200_A3, t4_image_support_t.T4_SUPPORT_WIDTH_303MM, t4_image_resolution_t.T4_RESOLUTION_200_100, t4_image_resolution_t.T4_RESOLUTION_R8_STANDARD),
        new((int)t4_image_width_t.T4_WIDTH_200_A3, t4_image_support_t.T4_SUPPORT_WIDTH_303MM, t4_image_resolution_t.T4_RESOLUTION_200_200, t4_image_resolution_t.T4_RESOLUTION_R8_FINE),
        new((int)t4_image_width_t.T4_WIDTH_200_A3, t4_image_support_t.T4_SUPPORT_WIDTH_303MM, t4_image_resolution_t.T4_RESOLUTION_200_400, t4_image_resolution_t.T4_RESOLUTION_R8_SUPERFINE),
        new((int)t4_image_width_t.T4_WIDTH_200_A3, t4_image_support_t.T4_SUPPORT_WIDTH_303MM, t4_image_resolution_t.T4_RESOLUTION_R8_STANDARD, t4_image_resolution_t.T4_RESOLUTION_200_100),
        new((int)t4_image_width_t.T4_WIDTH_200_A3, t4_image_support_t.T4_SUPPORT_WIDTH_303MM, t4_image_resolution_t.T4_RESOLUTION_R8_FINE, t4_image_resolution_t.T4_RESOLUTION_200_200),
        new((int)t4_image_width_t.T4_WIDTH_200_A3, t4_image_support_t.T4_SUPPORT_WIDTH_303MM, t4_image_resolution_t.T4_RESOLUTION_R8_SUPERFINE, t4_image_resolution_t.T4_RESOLUTION_200_400),

        new((int)t4_image_width_t.T4_WIDTH_300_A4, t4_image_support_t.T4_SUPPORT_WIDTH_215MM, t4_image_resolution_t.T4_RESOLUTION_300_300),
        new((int)t4_image_width_t.T4_WIDTH_300_A4, t4_image_support_t.T4_SUPPORT_WIDTH_215MM, t4_image_resolution_t.T4_RESOLUTION_300_600),
        new((int)t4_image_width_t.T4_WIDTH_300_B4, t4_image_support_t.T4_SUPPORT_WIDTH_255MM, t4_image_resolution_t.T4_RESOLUTION_300_300),
        new((int)t4_image_width_t.T4_WIDTH_300_B4, t4_image_support_t.T4_SUPPORT_WIDTH_255MM, t4_image_resolution_t.T4_RESOLUTION_300_600),
        new((int)t4_image_width_t.T4_WIDTH_300_A3, t4_image_support_t.T4_SUPPORT_WIDTH_303MM, t4_image_resolution_t.T4_RESOLUTION_300_300),
        new((int)t4_image_width_t.T4_WIDTH_300_A3, t4_image_support_t.T4_SUPPORT_WIDTH_303MM, t4_image_resolution_t.T4_RESOLUTION_300_600),

        new((int)t4_image_width_t.T4_WIDTH_400_A4, t4_image_support_t.T4_SUPPORT_WIDTH_215MM, t4_image_resolution_t.T4_RESOLUTION_400_400, t4_image_resolution_t.T4_RESOLUTION_R16_SUPERFINE),
        new((int)t4_image_width_t.T4_WIDTH_400_A4, t4_image_support_t.T4_SUPPORT_WIDTH_215MM, t4_image_resolution_t.T4_RESOLUTION_400_800),
        new((int)t4_image_width_t.T4_WIDTH_400_A4, t4_image_support_t.T4_SUPPORT_WIDTH_215MM, t4_image_resolution_t.T4_RESOLUTION_R16_SUPERFINE, t4_image_resolution_t.T4_RESOLUTION_400_400),
        new((int)t4_image_width_t.T4_WIDTH_400_B4, t4_image_support_t.T4_SUPPORT_WIDTH_255MM, t4_image_resolution_t.T4_RESOLUTION_400_400, t4_image_resolution_t.T4_RESOLUTION_R16_SUPERFINE),
        new((int)t4_image_width_t.T4_WIDTH_400_B4, t4_image_support_t.T4_SUPPORT_WIDTH_255MM, t4_image_resolution_t.T4_RESOLUTION_400_800),
        new((int)t4_image_width_t.T4_WIDTH_400_B4, t4_image_support_t.T4_SUPPORT_WIDTH_255MM, t4_image_resolution_t.T4_RESOLUTION_R16_SUPERFINE, t4_image_resolution_t.T4_RESOLUTION_400_400),
        new((int)t4_image_width_t.T4_WIDTH_400_A3, t4_image_support_t.T4_SUPPORT_WIDTH_303MM, t4_image_resolution_t.T4_RESOLUTION_400_400, t4_image_resolution_t.T4_RESOLUTION_R16_SUPERFINE),
        new((int)t4_image_width_t.T4_WIDTH_400_A3, t4_image_support_t.T4_SUPPORT_WIDTH_303MM, t4_image_resolution_t.T4_RESOLUTION_400_800),
        new((int)t4_image_width_t.T4_WIDTH_400_A3, t4_image_support_t.T4_SUPPORT_WIDTH_303MM, t4_image_resolution_t.T4_RESOLUTION_R16_SUPERFINE, t4_image_resolution_t.T4_RESOLUTION_400_400),

        new((int)t4_image_width_t.T4_WIDTH_600_A4, t4_image_support_t.T4_SUPPORT_WIDTH_215MM, t4_image_resolution_t.T4_RESOLUTION_600_600),
        new((int)t4_image_width_t.T4_WIDTH_600_A4, t4_image_support_t.T4_SUPPORT_WIDTH_215MM, t4_image_resolution_t.T4_RESOLUTION_600_1200),
        new((int)t4_image_width_t.T4_WIDTH_600_B4, t4_image_support_t.T4_SUPPORT_WIDTH_255MM, t4_image_resolution_t.T4_RESOLUTION_600_600),
        new((int)t4_image_width_t.T4_WIDTH_600_B4, t4_image_support_t.T4_SUPPORT_WIDTH_255MM, t4_image_resolution_t.T4_RESOLUTION_600_1200),
        new((int)t4_image_width_t.T4_WIDTH_600_A3, t4_image_support_t.T4_SUPPORT_WIDTH_303MM, t4_image_resolution_t.T4_RESOLUTION_600_600),
        new((int)t4_image_width_t.T4_WIDTH_600_A3, t4_image_support_t.T4_SUPPORT_WIDTH_303MM, t4_image_resolution_t.T4_RESOLUTION_600_1200),

        new((int)t4_image_width_t.T4_WIDTH_1200_A4, t4_image_support_t.T4_SUPPORT_WIDTH_215MM, t4_image_resolution_t.T4_RESOLUTION_1200_1200),
        new((int)t4_image_width_t.T4_WIDTH_1200_B4, t4_image_support_t.T4_SUPPORT_WIDTH_255MM, t4_image_resolution_t.T4_RESOLUTION_1200_1200),
        new((int)t4_image_width_t.T4_WIDTH_1200_A3, t4_image_support_t.T4_SUPPORT_WIDTH_303MM, t4_image_resolution_t.T4_RESOLUTION_1200_1200)
    ];

    internal static readonly ushort[] HeaderFont =
    {
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x3FFC, 0xC003, 0xCC33, 0xC003, 0xC003, 0xCFF3, 0xC3C3, 0xC003, 0xC003, 0x3FFC, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x3FFC, 0xFFFF, 0xF3CF, 0xFFFF, 0xFFFF, 0xF00F, 0xFC3F, 0xFFFF, 0xFFFF, 0x3FFC, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x1E78, 0x7FFE, 0x7FFE, 0x7FFE, 0x7FFE, 0x1FF8, 0x07E0, 0x0180, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0180, 0x07E0, 0x1FF8, 0x7FFE, 0x1FF8, 0x07E0, 0x0180, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x03C0, 0x0FF0, 0x0FF0, 0x7C3E, 0x7C3E, 0x7C3E, 0x63C6, 0x03C0, 0x0FF0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0180, 0x07E0, 0x1FF8, 0x7FFE, 0x7FFE, 0x1FF8, 0x0180, 0x0180, 0x07E0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x03C0, 0x0FF0, 0x0FF0, 0x03C0, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFC3F, 0xF00F, 0xF00F, 0xFC3F, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0FF0, 0x3C3C, 0x300C, 0x300C, 0x3C3C, 0x0FF0, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xF00F, 0xC3C3, 0xCFF3, 0xCFF3, 0xC3C3, 0xF00F, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF,
        0x0000, 0x0000, 0x01FE, 0x007E, 0x01F6, 0x0786, 0x1FE0, 0x3870, 0x3870, 0x3870, 0x3870, 0x1FE0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0FF0, 0x3C3C, 0x3C3C, 0x3C3C, 0x3C3C, 0x0FF0, 0x03C0, 0x3FFC, 0x03C0, 0x03C0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0FFE, 0x0F0E, 0x0FFE, 0x0E00, 0x0E00, 0x0E00, 0x0E00, 0x3E00, 0x7E00, 0x3C00, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x1FFE, 0x1C0E, 0x1FFE, 0x1C0E, 0x1C0E, 0x1C0E, 0x1C0E, 0x1C1E, 0x3C3E, 0x7C1C, 0x3800, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x4002, 0x23C4, 0x13C8, 0x0E70, 0x781E, 0x0E70, 0x13C8, 0x23C4, 0x4002, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x3000, 0x3C00, 0x3F00, 0x3FC0, 0x3FF0, 0x3FF8, 0x3FF0, 0x3FC0, 0x3F00, 0x3C00, 0x3000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x000C, 0x003C, 0x00FC, 0x03FC, 0x0FFC, 0x1FFC, 0x0FFC, 0x03FC, 0x00FC, 0x003C, 0x000C, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0180, 0x03C0, 0x0FF0, 0x3FFC, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x3FFC, 0x0FF0, 0x03C0, 0x0180, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x3C3C, 0x3C3C, 0x3C3C, 0x3C3C, 0x3C3C, 0x3C3C, 0x3C3C, 0x0000, 0x3C3C, 0x3C3C, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x3FFE, 0x71CE, 0x71CE, 0x71CE, 0x3FCE, 0x01CE, 0x01CE, 0x01CE, 0x01CE, 0x01CE, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x3FF0, 0x7038, 0x3C18, 0x0FC0, 0x3CF0, 0x7038, 0x7038, 0x7038, 0x3CF0, 0x0FC0, 0x60F0, 0x7038, 0x3FF0, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x3FFC, 0x3FFC, 0x3FFC, 0x3FFC, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0180, 0x03C0, 0x0FF0, 0x3FFC, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x3FFC, 0x0FF0, 0x03C0, 0x0180, 0x3FFC, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0180, 0x03C0, 0x0FF0, 0x3FFC, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x3FFC, 0x0FF0, 0x03C0, 0x0180, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0040, 0x0060, 0x0070, 0x0078, 0x3FFC, 0x0078, 0x0070, 0x0060, 0x0040, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0200, 0x0600, 0x0E00, 0x1E00, 0x3FFC, 0x1E00, 0x0E00, 0x0600, 0x0200, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x3800, 0x3800, 0x3800, 0x3800, 0x3FFE, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0420, 0x0C30, 0x1C38, 0x3FFC, 0x1C38, 0x0C30, 0x0420, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0180, 0x07E0, 0x07E0, 0x1FF8, 0x1FF8, 0x7FFE, 0x7FFE, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0xFFFC, 0xFFFC, 0x3FF0, 0x3FF0, 0x0FC0, 0x0FC0, 0x0300, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x03C0, 0x07E0, 0x07E0, 0x07E0, 0x03C0, 0x03C0, 0x03C0, 0x0000, 0x03C0, 0x03C0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x3C3C, 0x3C3C, 0x3C3C, 0x0C30, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x1C38, 0x1C38, 0x7FFE, 0x1C38, 0x1C38, 0x1C38, 0x7FFE, 0x1C38, 0x1C38, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x01E0, 0x01E0, 0x1FF8, 0x781E, 0x7806, 0x7800, 0x1FF8, 0x001E, 0x601E, 0x781E, 0x1FF8, 0x01E0, 0x01E0, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x7806, 0x781E, 0x0078, 0x01E0, 0x0780, 0x1E00, 0x781E, 0x601E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x07E0, 0x1E78, 0x1E78, 0x07E0, 0x1F9E, 0x79F8, 0x7878, 0x7878, 0x7878, 0x1F9E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0F00, 0x0F00, 0x0F00, 0x3C00, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x00F0, 0x03C0, 0x0F00, 0x0F00, 0x0F00, 0x0F00, 0x0F00, 0x0F00, 0x03C0, 0x00F0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0F00, 0x03C0, 0x00F0, 0x00F0, 0x00F0, 0x00F0, 0x00F0, 0x00F0, 0x03C0, 0x0F00, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x3C3C, 0x0FF0, 0x7FFE, 0x0FF0, 0x3C3C, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x03C0, 0x03C0, 0x3FFC, 0x03C0, 0x03C0, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x03C0, 0x03C0, 0x03C0, 0x0F00, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x3FFC, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x03C0, 0x03C0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0006, 0x001E, 0x0078, 0x01E0, 0x0780, 0x1E00, 0x7800, 0x6000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x1FF8, 0x781E, 0x781E, 0x787E, 0x799E, 0x799E, 0x7E1E, 0x781E, 0x781E, 0x1FF8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x03C0, 0x0FC0, 0x3FC0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x3FFC, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x1FF8, 0x781E, 0x001E, 0x0078, 0x01E0, 0x0780, 0x1E00, 0x7800, 0x781E, 0x7FFE, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x1FF8, 0x781E, 0x001E, 0x001E, 0x07F8, 0x001E, 0x001E, 0x001E, 0x781E, 0x1FF8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0078, 0x01F8, 0x07F8, 0x1E78, 0x7878, 0x7FFE, 0x0078, 0x0078, 0x0078, 0x01FE, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x7FFE, 0x7800, 0x7800, 0x7800, 0x7FF8, 0x007E, 0x001E, 0x001E, 0x781E, 0x1FF8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x07E0, 0x1E00, 0x7800, 0x7800, 0x7FF8, 0x781E, 0x781E, 0x781E, 0x781E, 0x1FF8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x7FFE, 0x781E, 0x001E, 0x001E, 0x0078, 0x01E0, 0x0780, 0x0780, 0x0780, 0x0780, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x1FF8, 0x781E, 0x781E, 0x781E, 0x1FF8, 0x781E, 0x781E, 0x781E, 0x781E, 0x1FF8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x1FF8, 0x781E, 0x781E, 0x781E, 0x1FFE, 0x001E, 0x001E, 0x001E, 0x0078, 0x1FE0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x03C0, 0x03C0, 0x0000, 0x0000, 0x0000, 0x03C0, 0x03C0, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x03C0, 0x03C0, 0x0000, 0x0000, 0x0000, 0x03C0, 0x03C0, 0x0F00, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x003C, 0x00F0, 0x03C0, 0x0F00, 0x3C00, 0x0F00, 0x03C0, 0x00F0, 0x003C, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x3FFC, 0x0000, 0x0000, 0x3FFC, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x3C00, 0x0F00, 0x03C0, 0x00F0, 0x003C, 0x00F0, 0x03C0, 0x0F00, 0x3C00, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x1FF8, 0x781E, 0x781E, 0x0078, 0x01E0, 0x01E0, 0x01E0, 0x0000, 0x01E0, 0x01E0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x1FF8, 0x781E, 0x781E, 0x79FE, 0x79FE, 0x79FE, 0x79F8, 0x7800, 0x1FF8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0180, 0x07E0, 0x1E78, 0x781E, 0x781E, 0x7FFE, 0x781E, 0x781E, 0x781E, 0x781E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x7FF8, 0x1E1E, 0x1E1E, 0x1E1E, 0x1FF8, 0x1E1E, 0x1E1E, 0x1E1E, 0x1E1E, 0x7FF8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x07F8, 0x1E1E, 0x7806, 0x7800, 0x7800, 0x7800, 0x7800, 0x7806, 0x1E1E, 0x07F8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x7FE0, 0x1E78, 0x1E1E, 0x1E1E, 0x1E1E, 0x1E1E, 0x1E1E, 0x1E1E, 0x1E78, 0x7FE0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x7FFE, 0x1E1E, 0x1E06, 0x1E60, 0x1FE0, 0x1E60, 0x1E00, 0x1E06, 0x1E1E, 0x7FFE, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x7FFE, 0x1E1E, 0x1E06, 0x1E60, 0x1FE0, 0x1E60, 0x1E00, 0x1E00, 0x1E00, 0x7F80, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x07F8, 0x1E1E, 0x7806, 0x7800, 0x7800, 0x79FE, 0x781E, 0x781E, 0x1E1E, 0x07E6, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x781E, 0x781E, 0x781E, 0x781E, 0x7FFE, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x07F8, 0x01E0, 0x01E0, 0x01E0, 0x01E0, 0x01E0, 0x01E0, 0x01E0, 0x01E0, 0x07F8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x01FE, 0x0078, 0x0078, 0x0078, 0x0078, 0x0078, 0x7878, 0x7878, 0x7878, 0x1FE0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x7E1E, 0x1E1E, 0x1E78, 0x1E78, 0x1FE0, 0x1FE0, 0x1E78, 0x1E1E, 0x1E1E, 0x7E1E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x7F80, 0x1E00, 0x1E00, 0x1E00, 0x1E00, 0x1E00, 0x1E00, 0x1E06, 0x1E1E, 0x7FFE, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x781E, 0x7E7E, 0x7FFE, 0x7FFE, 0x799E, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x781E, 0x7E1E, 0x7F9E, 0x7FFE, 0x79FE, 0x787E, 0x781E, 0x781E, 0x781E, 0x781E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x07E0, 0x1E78, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x1E78, 0x07E0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x7FF8, 0x1E1E, 0x1E1E, 0x1E1E, 0x1FF8, 0x1E00, 0x1E00, 0x1E00, 0x1E00, 0x7F80, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x1FF8, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x799E, 0x79FE, 0x1FF8, 0x0078, 0x007E, 0x0000, 0x0000,
        0x0000, 0x0000, 0x7FF8, 0x1E1E, 0x1E1E, 0x1E1E, 0x1FF8, 0x1E78, 0x1E1E, 0x1E1E, 0x1E1E, 0x7E1E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x1FF8, 0x781E, 0x781E, 0x1E00, 0x07E0, 0x0078, 0x001E, 0x781E, 0x781E, 0x1FF8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x1FFE, 0x1FFE, 0x19E6, 0x01E0, 0x01E0, 0x01E0, 0x01E0, 0x01E0, 0x01E0, 0x07F8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x1FF8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x1E78, 0x07E0, 0x0180, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x799E, 0x799E, 0x7FFE, 0x1E78, 0x1E78, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x781E, 0x781E, 0x1E78, 0x1E78, 0x07E0, 0x07E0, 0x1E78, 0x1E78, 0x781E, 0x781E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x3C3C, 0x3C3C, 0x3C3C, 0x3C3C, 0x0FF0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x0FF0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x7FFE, 0x781E, 0x601E, 0x0078, 0x01E0, 0x0780, 0x1E00, 0x7806, 0x781E, 0x7FFE, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0FF0, 0x0F00, 0x0F00, 0x0F00, 0x0F00, 0x0F00, 0x0F00, 0x0F00, 0x0F00, 0x0FF0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x6000, 0x7800, 0x7E00, 0x1F80, 0x07E0, 0x01F8, 0x007E, 0x001E, 0x0006, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0FF0, 0x00F0, 0x00F0, 0x00F0, 0x00F0, 0x00F0, 0x00F0, 0x00F0, 0x00F0, 0x0FF0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0180, 0x07E0, 0x1E78, 0x781E, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0xFFFF, 0x0000, 0x0000,
        0x0F00, 0x0F00, 0x03C0, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x1FE0, 0x0078, 0x1FF8, 0x7878, 0x7878, 0x7878, 0x1F9E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x7E00, 0x1E00, 0x1E00, 0x1FE0, 0x1E78, 0x1E1E, 0x1E1E, 0x1E1E, 0x1E1E, 0x79F8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x1FF8, 0x781E, 0x7800, 0x7800, 0x7800, 0x781E, 0x1FF8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x01F8, 0x0078, 0x0078, 0x07F8, 0x1E78, 0x7878, 0x7878, 0x7878, 0x7878, 0x1F9E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x1FF8, 0x781E, 0x7FFE, 0x7800, 0x7800, 0x781E, 0x1FF8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x07E0, 0x1E78, 0x1E18, 0x1E00, 0x7F80, 0x1E00, 0x1E00, 0x1E00, 0x1E00, 0x7F80, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x1F9E, 0x7878, 0x7878, 0x7878, 0x7878, 0x7878, 0x1FF8, 0x0078, 0x7878, 0x1FE0, 0x0000,
        0x0000, 0x0000, 0x7E00, 0x1E00, 0x1E00, 0x1E78, 0x1F9E, 0x1E1E, 0x1E1E, 0x1E1E, 0x1E1E, 0x7E1E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x01E0, 0x01E0, 0x0000, 0x07E0, 0x01E0, 0x01E0, 0x01E0, 0x01E0, 0x01E0, 0x07F8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x001E, 0x001E, 0x0000, 0x007E, 0x001E, 0x001E, 0x001E, 0x001E, 0x001E, 0x001E, 0x1E1E, 0x1E1E, 0x07F8, 0x0000,
        0x0000, 0x0000, 0x7E00, 0x1E00, 0x1E00, 0x1E1E, 0x1E78, 0x1FE0, 0x1FE0, 0x1E78, 0x1E1E, 0x7E1E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x07E0, 0x01E0, 0x01E0, 0x01E0, 0x01E0, 0x01E0, 0x01E0, 0x01E0, 0x01E0, 0x07F8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x7E78, 0x7FFE, 0x799E, 0x799E, 0x799E, 0x799E, 0x799E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x79F8, 0x1E1E, 0x1E1E, 0x1E1E, 0x1E1E, 0x1E1E, 0x1E1E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x1FF8, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x1FF8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x79F8, 0x1E1E, 0x1E1E, 0x1E1E, 0x1E1E, 0x1E1E, 0x1FF8, 0x1E00, 0x1E00, 0x7F80, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x1F9E, 0x7878, 0x7878, 0x7878, 0x7878, 0x7878, 0x1FF8, 0x0078, 0x0078, 0x01FE, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x79F8, 0x1F9E, 0x1E06, 0x1E00, 0x1E00, 0x1E00, 0x7F80, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x1FF8, 0x781E, 0x1E00, 0x07E0, 0x0078, 0x781E, 0x1FF8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0180, 0x0780, 0x0780, 0x7FF8, 0x0780, 0x0780, 0x0780, 0x0780, 0x079E, 0x01F8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x7878, 0x7878, 0x7878, 0x7878, 0x7878, 0x7878, 0x1F9E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x1E1E, 0x1E1E, 0x1E1E, 0x1E1E, 0x1E1E, 0x07F8, 0x01E0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x781E, 0x781E, 0x781E, 0x799E, 0x799E, 0x7FFE, 0x1E78, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x781E, 0x1E78, 0x07E0, 0x07E0, 0x07E0, 0x1E78, 0x781E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x1FFE, 0x001E, 0x0078, 0x7FE0, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x7FFE, 0x7878, 0x01E0, 0x0780, 0x1E00, 0x781E, 0x7FFE, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x00FC, 0x03C0, 0x03C0, 0x03C0, 0x0F00, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x00FC, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x0000, 0x0000, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x0000,
        0x0000, 0x0000, 0x3F00, 0x03C0, 0x03C0, 0x03C0, 0x00F0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x3F00, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x1F9E, 0x79F8, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0180, 0x07E0, 0x1E78, 0x781E, 0x781E, 0x781E, 0x7FFE, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x07F8, 0x1E1E, 0x7806, 0x7800, 0x7800, 0x7800, 0x7806, 0x1E1E, 0x07F8, 0x0078, 0x001E, 0x1FF8, 0x0000, 0x0000,
        0x0000, 0x0000, 0x7878, 0x7878, 0x0000, 0x7878, 0x7878, 0x7878, 0x7878, 0x7878, 0x7878, 0x1F9E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0078, 0x01E0, 0x0780, 0x0000, 0x1FF8, 0x781E, 0x7FFE, 0x7800, 0x7800, 0x781E, 0x1FF8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0180, 0x07E0, 0x1E78, 0x0000, 0x1FE0, 0x0078, 0x1FF8, 0x7878, 0x7878, 0x7878, 0x1F9E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x7878, 0x7878, 0x0000, 0x1FE0, 0x0078, 0x1FF8, 0x7878, 0x7878, 0x7878, 0x1F9E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x1E00, 0x0780, 0x01E0, 0x0000, 0x1FE0, 0x0078, 0x1FF8, 0x7878, 0x7878, 0x7878, 0x1F9E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x07E0, 0x1E78, 0x07E0, 0x0000, 0x1FE0, 0x0078, 0x1FF8, 0x7878, 0x7878, 0x7878, 0x1F9E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0FF0, 0x3C3C, 0x3C00, 0x3C00, 0x3C3C, 0x0FF0, 0x00F0, 0x003C, 0x0FF0, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0180, 0x07E0, 0x1E78, 0x0000, 0x1FF8, 0x781E, 0x7FFE, 0x7800, 0x7800, 0x781E, 0x1FF8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x781E, 0x781E, 0x0000, 0x1FF8, 0x781E, 0x7FFE, 0x7800, 0x7800, 0x781E, 0x1FF8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x1E00, 0x0780, 0x01E0, 0x0000, 0x1FF8, 0x781E, 0x7FFE, 0x7800, 0x7800, 0x781E, 0x1FF8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x3C3C, 0x3C3C, 0x0000, 0x0FC0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x0FF0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x03C0, 0x0FF0, 0x3C3C, 0x0000, 0x0FC0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x0FF0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x3C00, 0x0F00, 0x03C0, 0x0000, 0x0FC0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x0FF0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x781E, 0x781E, 0x0180, 0x07E0, 0x1E78, 0x781E, 0x781E, 0x7FFE, 0x781E, 0x781E, 0x781E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x07E0, 0x1E78, 0x07E0, 0x0000, 0x07E0, 0x1E78, 0x781E, 0x781E, 0x7FFE, 0x781E, 0x781E, 0x781E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x01E0, 0x0780, 0x1E00, 0x0000, 0x7FFE, 0x1E1E, 0x1E00, 0x1FF8, 0x1E00, 0x1E00, 0x1E1E, 0x7FFE, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x7878, 0x1F9E, 0x079E, 0x1FFE, 0x79E0, 0x79E0, 0x1E7E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x07FE, 0x1E78, 0x7878, 0x7878, 0x7FFE, 0x7878, 0x7878, 0x7878, 0x7878, 0x787E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0180, 0x07E0, 0x1E78, 0x0000, 0x1FF8, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x1FF8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x781E, 0x781E, 0x0000, 0x1FF8, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x1FF8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x1E00, 0x0780, 0x01E0, 0x0000, 0x1FF8, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x1FF8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0780, 0x1FE0, 0x7878, 0x0000, 0x7878, 0x7878, 0x7878, 0x7878, 0x7878, 0x7878, 0x1F9E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x1E00, 0x0780, 0x01E0, 0x0000, 0x7878, 0x7878, 0x7878, 0x7878, 0x7878, 0x7878, 0x1F9E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x781E, 0x781E, 0x0000, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x1FFE, 0x001E, 0x0078, 0x1FE0, 0x0000,
        0x0000, 0x781E, 0x781E, 0x0000, 0x07E0, 0x1E78, 0x781E, 0x781E, 0x781E, 0x781E, 0x1E78, 0x07E0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x781E, 0x781E, 0x0000, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x1FF8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x03C0, 0x03C0, 0x0FF0, 0x3C3C, 0x3C00, 0x3C00, 0x3C00, 0x3C3C, 0x0FF0, 0x03C0, 0x03C0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x07E0, 0x1E78, 0x1E18, 0x1E00, 0x7F80, 0x1E00, 0x1E00, 0x1E00, 0x1E00, 0x7E1E, 0x7FF8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x3C3C, 0x3C3C, 0x0FF0, 0x03C0, 0x3FFC, 0x03C0, 0x3FFC, 0x03C0, 0x03C0, 0x03C0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x7FE0, 0x7878, 0x7878, 0x7FE0, 0x7818, 0x7878, 0x79FE, 0x7878, 0x7878, 0x7878, 0x781E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x00FC, 0x03CF, 0x03C0, 0x03C0, 0x03C0, 0x3FFC, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0xF3C0, 0x3F00, 0x0000, 0x0000,
        0x0000, 0x01E0, 0x0780, 0x1E00, 0x0000, 0x1FE0, 0x0078, 0x1FF8, 0x7878, 0x7878, 0x7878, 0x1F9E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x00F0, 0x03C0, 0x0F00, 0x0000, 0x0FC0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x0FF0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x01E0, 0x0780, 0x1E00, 0x0000, 0x1FF8, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x1FF8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x01E0, 0x0780, 0x1E00, 0x0000, 0x7878, 0x7878, 0x7878, 0x7878, 0x7878, 0x7878, 0x1F9E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x1F9E, 0x79F8, 0x0000, 0x79F8, 0x1E1E, 0x1E1E, 0x1E1E, 0x1E1E, 0x1E1E, 0x1E1E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x1F9E, 0x79F8, 0x0000, 0x781E, 0x7E1E, 0x7F9E, 0x7FFE, 0x79FE, 0x787E, 0x781E, 0x781E, 0x781E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0FF0, 0x3CF0, 0x3CF0, 0x0FFC, 0x0000, 0x3FFC, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x07E0, 0x1E78, 0x1E78, 0x07E0, 0x0000, 0x1FF8, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0780, 0x0780, 0x0000, 0x0780, 0x0780, 0x1E00, 0x7800, 0x781E, 0x781E, 0x1FF8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0xFFFC, 0xF000, 0xF000, 0xF000, 0xF000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0xFFFC, 0x003C, 0x003C, 0x003C, 0x003C, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x7000, 0x7000, 0x700C, 0x703C, 0x70F0, 0x03C0, 0x0F00, 0x3C00, 0xF0FC, 0xC30E, 0x003C, 0x00F0, 0x03FE, 0x0000, 0x0000,
        0x0000, 0x7000, 0x7000, 0x700C, 0x703C, 0x70F0, 0x03C0, 0x0F00, 0x3C3C, 0xF0FC, 0xC38C, 0x07FE, 0x003C, 0x00FE, 0x0000, 0x0000,
        0x0000, 0x0000, 0x03C0, 0x03C0, 0x0000, 0x03C0, 0x03C0, 0x03C0, 0x07E0, 0x07E0, 0x07E0, 0x03C0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0E0E, 0x3C3C, 0x7070, 0x3C3C, 0x0E0E, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x7070, 0x3C3C, 0x0E0E, 0x3C3C, 0x7070, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0303, 0x3030, 0x0303, 0x3030, 0x0303, 0x3030, 0x0303, 0x3030, 0x0303, 0x3030, 0x0303, 0x3030, 0x0303, 0x3030, 0x0303, 0x3030,
        0xAAAA, 0x5555, 0xAAAA, 0x5555, 0xAAAA, 0x5555, 0xAAAA, 0x5555, 0xAAAA, 0x5555, 0xAAAA, 0x5555, 0xAAAA, 0x5555, 0xAAAA, 0x5555,
        0xF3F3, 0x3F3F, 0xF3F3, 0x3F3F, 0xF3F3, 0x3F3F, 0xF3F3, 0x3F3F, 0xF3F3, 0x3F3F, 0xF3F3, 0x3F3F, 0xF3F3, 0x3F3F, 0xF3F3, 0x3F3F,
        0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0,
        0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0xFFC0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0,
        0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0xFFC0, 0x03C0, 0xFFC0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0,
        0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0xFF3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0xFFFC, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0xFFC0, 0x03C0, 0xFFC0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0,
        0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0xFF3C, 0x003C, 0xFF3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C,
        0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0xFFFC, 0x003C, 0xFF3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C,
        0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0xFF3C, 0x003C, 0xFFFC, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0xFFFC, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0xFFC0, 0x03C0, 0xFFC0, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0xFFC0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0,
        0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03FF, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0xFFFF, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0xFFFF, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0,
        0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03FF, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0xFFFF, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0xFFFF, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0,
        0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03FF, 0x03C0, 0x03FF, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0,
        0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3F, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C,
        0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3F, 0x0F00, 0x0FFF, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0FFF, 0x0F00, 0x0F3F, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C,
        0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0xFF3F, 0x0000, 0xFFFF, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0xFFFF, 0x0000, 0xFF3F, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C,
        0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3F, 0x0F00, 0x0F3F, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0xFFFF, 0x0000, 0xFFFF, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0xFF3F, 0x0000, 0xFF3F, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C,
        0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0xFFFF, 0x0000, 0xFFFF, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0xFFFF, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0xFFFF, 0x0000, 0xFFFF, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0xFFFF, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C,
        0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0FFF, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03FF, 0x03C0, 0x03FF, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x03FF, 0x03C0, 0x03FF, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0FFF, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C,
        0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0xFFFF, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C, 0x0F3C,
        0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0xFFFF, 0x03C0, 0xFFFF, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0,
        0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0xFFC0, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x03FF, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0,
        0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF,
        0xFF00, 0xFF00, 0xFF00, 0xFF00, 0xFF00, 0xFF00, 0xFF00, 0xFF00, 0xFF00, 0xFF00, 0xFF00, 0xFF00, 0xFF00, 0xFF00, 0xFF00, 0xFF00,
        0x00FF, 0x00FF, 0x00FF, 0x00FF, 0x00FF, 0x00FF, 0x00FF, 0x00FF, 0x00FF, 0x00FF, 0x00FF, 0x00FF, 0x00FF, 0x00FF, 0x00FF, 0x00FF,
        0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x1F9E, 0x79F8, 0x79E0, 0x79E0, 0x79E0, 0x79F8, 0x1F9E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x7FF8, 0x781E, 0x7FF8, 0x781E, 0x781E, 0x7FF8, 0x7800, 0x7800, 0x7800, 0x0000, 0x0000,
        0x0000, 0x0000, 0x7FFE, 0x781E, 0x781E, 0x7800, 0x7800, 0x7800, 0x7800, 0x7800, 0x7800, 0x7800, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x7FFE, 0x7FFE, 0x1E78, 0x1E78, 0x1E78, 0x1E78, 0x1E78, 0x1E78, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x7FFE, 0x781E, 0x1E00, 0x0780, 0x01E0, 0x0780, 0x1E00, 0x781E, 0x7FFE, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x1FFE, 0x79E0, 0x79E0, 0x79E0, 0x79E0, 0x79E0, 0x1F80, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x1E1E, 0x1E1E, 0x1E1E, 0x1E1E, 0x1E1E, 0x1FF8, 0x1E00, 0x1E00, 0x7800, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x1F9E, 0x79F8, 0x01E0, 0x01E0, 0x01E0, 0x01E0, 0x01E0, 0x01E0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x3FFC, 0x03C0, 0x0FF0, 0x3C3C, 0x3C3C, 0x3C3C, 0x0FF0, 0x03C0, 0x3FFC, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x07E0, 0x1E78, 0x781E, 0x781E, 0x7FFE, 0x781E, 0x781E, 0x1E78, 0x07E0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x07E0, 0x1E78, 0x781E, 0x781E, 0x781E, 0x1E78, 0x1E78, 0x1E78, 0x1E78, 0x7E7E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x03FC, 0x0F00, 0x03C0, 0x00F0, 0x0FFC, 0x3C3C, 0x3C3C, 0x3C3C, 0x3C3C, 0x0FF0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x3E7C, 0x73CE, 0x73CE, 0x73CE, 0x3E7C, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x000F, 0x003C, 0x3FFC, 0xF0FF, 0xF3CF, 0xFF0F, 0x3FFC, 0x3C00, 0xF000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x03F0, 0x0F00, 0x1C00, 0x1C00, 0x1FF0, 0x1C00, 0x1C00, 0x1C00, 0x0F00, 0x03F0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x1FF8, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x781E, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x7FFE, 0x0000, 0x0000, 0x7FFE, 0x0000, 0x0000, 0x7FFE, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x03C0, 0x03C0, 0x3FFC, 0x03C0, 0x03C0, 0x0000, 0x0000, 0x3FFC, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0F00, 0x03C0, 0x00F0, 0x0038, 0x00F0, 0x03C0, 0x0F00, 0x0000, 0x0FF8, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x00F0, 0x03C0, 0x0F00, 0x1C00, 0x0F00, 0x03C0, 0x00F0, 0x0000, 0x1FF0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x00FC, 0x03CF, 0x03CF, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0,
        0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0x03C0, 0xF3C0, 0xF3C0, 0xF3C0, 0x3F00, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x03C0, 0x03C0, 0x0000, 0x3FFC, 0x0000, 0x03C0, 0x03C0, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x1F9E, 0x79F8, 0x0000, 0x1F9E, 0x79F8, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x07E0, 0x1E78, 0x1E78, 0x07E0, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x03C0, 0x03C0, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x03C0, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x00FF, 0x00F0, 0x00F0, 0x00F0, 0x00F0, 0x00F0, 0x70F0, 0x38F0, 0x1CF0, 0x0FF0, 0x03F0, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x79E0, 0x1E78, 0x1E78, 0x1E78, 0x1E78, 0x1E78, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x1F80, 0x61E0, 0x0780, 0x1E00, 0x7860, 0x7FE0, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x1FF8, 0x1FF8, 0x1FF8, 0x1FF8, 0x1FF8, 0x1FF8, 0x1FF8, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
    };
}

/// <summary>C-compatible facade retaining the public t4_tx.h function names.</summary>
public static class t4_tx {

    internal static int tiff_row_read_handler(object? user_data, Span<byte> buf, int len) {
        if (user_data is not t4_tx_state_t s || s._sourcePage is null)
            return 0;
        if (len <= 0 || buf.Length < len)
            return 0;

        s._sourceRowBuffer = t4_tx_state_t.EnsureBuffer(s._sourceRowBuffer, len);
        Span<byte> source_row = s._sourceRowBuffer.AsSpan(0, len);
        int read = s._sourcePage.ReadRow(source_row, len);
        if (read != len)
            return 0;

        source_row.CopyTo(buf);
        s._sourceRowsRead++;

        for (int i = 1;
             i < s._rowSquashingRatio && s._sourceRowsRead < s.SourceMetadata.ImageLength;
             i++) {
            s._extraRowBuffer = t4_tx_state_t.EnsureBuffer(s._extraRowBuffer, len);
            Span<byte> extra_row = s._extraRowBuffer.AsSpan(0, len);
            int extra = s._sourcePage.ReadRow(extra_row, len);
            if (extra != len)
                return 0;
            for (int j = 0; j < len; j++)
                buf[j] |= extra_row[j];
            s._sourceRowsRead++;
        }

        return len;
    }

    private static int translate_row_read2(object? user_data, Span<byte> buf, int len) {
        // In the managed port the unpacked source page is the equivalent of
        // spanDSP's pack_buf/pack_ptr storage. Consume it through the same
        // row callback rather than through an OO forwarding method.
        return tiff_row_read_handler(user_data, buf, len);
    }

    private static int translate_row_read(object? user_data, Span<byte> buf, int len) {
        if (user_data is not t4_tx_state_t s)
            return 0;
        return s._translator.TranslateRow(buf[..len]);
    }

    private static int make_header(t4_tx_state_t s) {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (s.HeaderTimeZone is not null)
            now = TimeZoneInfo.ConvertTime(now, s.HeaderTimeZone);
        else
            now = now.ToLocalTime();

        string info = (s.HeaderInfo ?? string.Empty).PadRight(50);
        if (info.Length > 50)
            info = info[..50];

        string ident = (s.LocalIdent ?? string.Empty).PadRight(21);
        if (ident.Length > 21)
            ident = ident[..21];

        s._headerText = string.Format(
            CultureInfo.InvariantCulture,
            "  {0,2}-{1}-{2}  {3:00}:{4:00}    {5} {6}   p.{7}",
            now.Day,
            now.ToString("MMM", CultureInfo.InvariantCulture),
            now.Year,
            now.Hour,
            now.Minute,
            info,
            ident,
            s.CurrentPageNumber + 1);
        return 0;
    }

    private static int header_row_read_handler(object? user_data, Span<byte> buf, int len) {
        Span<byte> buf_span = buf[..len];
        if (user_data is not t4_tx_state_t s)
            return 0;

        if (s._headerRow < s._headerRows) {
            if (s.HeaderOverlaysImage && s._imageRowHandler is not null)
                s._imageRowHandler(s._imageRowUserData, buf, len);
            else
                t4_tx_state_t.FillWhite(buf_span, s.Metadata.ImageType);

            s.DrawHeaderRow(buf_span, s._headerRow);
            s._headerRow++;
            return len;
        }

        return s._imageRowHandler?.Invoke(s._imageRowUserData, buf, len) ?? 0;
    }

    private static Tiff.TiffExtendProc? _ParentExtender;
    private static bool _tiffFxInitialized;

    public const int T4_IMAGE_FORMAT_OK = (int)T4ImageFormatStatus.Ok;
    public const int T4_IMAGE_FORMAT_INCOMPATIBLE = (int)T4ImageFormatStatus.Incompatible;
    public const int T4_IMAGE_FORMAT_NOSIZESUPPORT = (int)T4ImageFormatStatus.NoSizeSupport;
    public const int T4_IMAGE_FORMAT_NORESSUPPORT = (int)T4ImageFormatStatus.NoResolutionSupport;

    public const int COMPRESSION_T85 = 9;
    public const int COMPRESSION_T43 = 10;
    public const int TIFFTAG_INDEXED = 346;
    public const int TIFFTAG_GLOBALPARAMETERSIFD = 400;
    public const int TIFFTAG_PROFILETYPE = 401;
    public const int TIFFTAG_FAXPROFILE = 402;
    public const int TIFFTAG_CODINGMETHODS = 403;
    public const int TIFFTAG_VERSIONYEAR = 404;
    public const int TIFFTAG_MODENUMBER = 405;
    public const int TIFFTAG_DECODE = 433;
    public const int TIFFTAG_IMAGEBASECOLOR = 434;
    public const int TIFFTAG_T82OPTIONS = 435;
    public const int TIFFTAG_STRIPROWCOUNTS = 559;
    public const int TIFFTAG_IMAGELAYER = 34732;

    public const int PROFILETYPE_UNSPECIFIED = 0;
    public const int PROFILETYPE_G3_FAX = 1;
    public const int FAXPROFILE_S = 1;
    public const int FAXPROFILE_F = 2;
    public const int FAXPROFILE_J = 3;
    public const int FAXPROFILE_C = 4;
    public const int FAXPROFILE_L = 5;
    public const int FAXPROFILE_M = 6;
    public const int CODINGMETHODS_T4_1D = 1 << 1;
    public const int CODINGMETHODS_T4_2D = 1 << 2;
    public const int CODINGMETHODS_T6 = 1 << 3;
    public const int CODINGMETHODS_T85 = 1 << 4;
    public const int CODINGMETHODS_T42 = 1 << 5;
    public const int CODINGMETHODS_T43 = 1 << 6;

    private static void TIFFFXDefaultDirectory(Tiff tif) {
        TiffFieldInfo[] fields =
        {
            new((TiffTag)TIFFTAG_INDEXED, 1, 1, TiffType.SHORT, FieldBit.Custom, false, false, "Indexed"),
            new((TiffTag)TIFFTAG_GLOBALPARAMETERSIFD, 1, 1, TiffType.LONG, FieldBit.Custom, false, false, "GlobalParametersIFD"),
            new((TiffTag)TIFFTAG_PROFILETYPE, 1, 1, TiffType.LONG, FieldBit.Custom, false, false, "ProfileType"),
            new((TiffTag)TIFFTAG_FAXPROFILE, 1, 1, TiffType.BYTE, FieldBit.Custom, false, false, "FaxProfile"),
            new((TiffTag)TIFFTAG_CODINGMETHODS, 1, 1, TiffType.LONG, FieldBit.Custom, false, false, "CodingMethods"),
            new((TiffTag)TIFFTAG_VERSIONYEAR, 4, 4, TiffType.BYTE, FieldBit.Custom, false, false, "VersionYear"),
            new((TiffTag)TIFFTAG_MODENUMBER, 1, 1, TiffType.BYTE, FieldBit.Custom, false, false, "ModeNumber"),
            new((TiffTag)TIFFTAG_DECODE, -1, -1, TiffType.SRATIONAL, FieldBit.Custom, false, true, "Decode"),
            new((TiffTag)TIFFTAG_IMAGEBASECOLOR, -1, -1, TiffType.SHORT, FieldBit.Custom, false, true, "ImageBaseColor"),
            new((TiffTag)TIFFTAG_T82OPTIONS, 1, 1, TiffType.LONG, FieldBit.Custom, false, false, "T82Options"),
            new((TiffTag)TIFFTAG_STRIPROWCOUNTS, -1, -1, TiffType.LONG, FieldBit.Custom, false, true, "StripRowCounts"),
            new((TiffTag)TIFFTAG_IMAGELAYER, 2, 2, TiffType.LONG, FieldBit.Custom, false, false, "ImageLayer")
        };
        tif.MergeFieldInfo(fields, fields.Length);
        _ParentExtender?.Invoke(tif);
    }

    public static void TIFF_FX_init() {
        if (_tiffFxInitialized)
            return;
        _tiffFxInitialized = true;
        _ParentExtender = Tiff.SetTagExtender(TIFFFXDefaultDirectory);
    }

    private static int best_colour_resolution(float actual, int allowed_resolutions) {
        (float Resolution, int Code)[] table =
        {
            (100.0f * 100.0f / 2.54f, (int)t4_image_resolution_t.T4_RESOLUTION_100_100),
            (200.0f * 100.0f / 2.54f, (int)t4_image_resolution_t.T4_RESOLUTION_200_200),
            (300.0f * 100.0f / 2.54f, (int)t4_image_resolution_t.T4_RESOLUTION_300_300),
            (400.0f * 100.0f / 2.54f, (int)t4_image_resolution_t.T4_RESOLUTION_400_400),
            (600.0f * 100.0f / 2.54f, (int)t4_image_resolution_t.T4_RESOLUTION_600_600),
            (1200.0f * 100.0f / 2.54f, (int)t4_image_resolution_t.T4_RESOLUTION_1200_1200)
        };
        if (actual == 0.0f)
            return -1;
        int best_entry = -1;
        float best_ratio = 0.0f;
        for (int i = 0; i < table.Length; i++) {
            if ((allowed_resolutions & table[i].Code) == 0)
                continue;
            float ratio = actual > table[i].Resolution
                ? table[i].Resolution / actual
                : actual / table[i].Resolution;
            if (ratio > best_ratio) {
                best_ratio = ratio;
                best_entry = i;
            }
        }
        return best_entry < 0 ? -1 : table[best_entry].Code;
    }

    private static void load_source_metadata(t4_tx_state_t s, T4TxPage page) {
        T4TxMetadata metadata = page.CreateMetadata();
        s.SourceMetadata.CopyFrom(metadata);
        if (!s._formatNegotiated) {
            s.Metadata.CopyFrom(metadata);
            s.Metadata.WidthCode = t4_tx_state_t.WidthCodeForWidth(metadata.ImageWidth);
        }
    }

    private static void apply_resolution(t4_tx_state_t s, t4_image_resolution_t resolution) {
        s.Metadata.ResolutionCode = resolution;
        s.Metadata.XResolution = t4_tx_state_t.code_to_x_resolution(resolution);
        s.Metadata.YResolution = t4_tx_state_t.code_to_y_resolution(resolution);
    }

    private static T4ImageFormatStatus select_resolution(
        t4_tx_state_t s,
        t4_tx_state_t.WidthResolutionInfo info,
        t4_image_compression_t supported_compressions,
        t4_image_resolution_t supported_bilevel_resolutions,
        t4_image_resolution_t supported_colour_resolutions) {
        if (s.Metadata.ImageType == t4_image_types_t.T4_IMAGE_TYPE_BILEVEL) {
            if ((s.SourceMetadata.ResolutionCode & supported_bilevel_resolutions) != 0) {
                apply_resolution(s, s.SourceMetadata.ResolutionCode);
                return T4ImageFormatStatus.Ok;
            }
            if ((info.ResolutionCode & supported_bilevel_resolutions) != 0) {
                apply_resolution(s, info.ResolutionCode);
                return T4ImageFormatStatus.Ok;
            }
            if ((info.AlternateResolutionCode & supported_bilevel_resolutions) != 0) {
                apply_resolution(s, info.AlternateResolutionCode);
                return T4ImageFormatStatus.Ok;
            }
            if (s.SourceMetadata.ImageType == t4_image_types_t.T4_IMAGE_TYPE_BILEVEL) {
                foreach (t4_tx_state_t.SquashInfo squash in t4_tx_state_t.Squashable) {
                    if ((s.SourceMetadata.ResolutionCode & squash.SourceResolution) == 0)
                        continue;
                    foreach (t4_tx_state_t.ResolutionFallback fallback in squash.Fallbacks) {
                        if (fallback.Resolution == (t4_image_resolution_t)0 ||
                            (supported_bilevel_resolutions & fallback.Resolution) == 0)
                            continue;
                        s._rowSquashingRatio = fallback.SquashingFactor;
                        apply_resolution(s, fallback.Resolution);
                        return T4ImageFormatStatus.Ok;
                    }
                }
            }
            if (s.SourceMetadata.ImageType == t4_image_types_t.T4_IMAGE_TYPE_BILEVEL)
                return T4ImageFormatStatus.NoResolutionSupport;
            if ((supported_compressions & t4_image_compression_t.T4_COMPRESSION_RESCALING) == 0)
                return T4ImageFormatStatus.NoSizeSupport;
            t4_image_resolution_t fallback_resolution = t4_tx_state_t.FirstResolution(supported_bilevel_resolutions);
            if (fallback_resolution == (t4_image_resolution_t)0)
                return T4ImageFormatStatus.NoResolutionSupport;
            apply_resolution(s, fallback_resolution);
            return T4ImageFormatStatus.Ok;
        }
        t4_image_resolution_t preferred =
            (s.SourceMetadata.ResolutionCode & supported_colour_resolutions) != 0
                ? s.SourceMetadata.ResolutionCode
                : t4_tx_state_t.FirstResolution(supported_colour_resolutions);
        if (preferred == (t4_image_resolution_t)0)
            return T4ImageFormatStatus.NoResolutionSupport;
        apply_resolution(s, preferred);
        return T4ImageFormatStatus.Ok;
    }

    private static int read_colour_map(t4_tx_state_t s, int bits_per_sample) {
        if (s._tiff is null)
            return -1;
        FieldValue[]? map = s._tiff.GetField(TiffTag.COLORMAP);
        if (map is null || map.Length < 3)
            return -1;
        short[]? map_l = map[0].ToShortArray();
        short[]? map_a = map[1].ToShortArray();
        short[]? map_b = map[2].ToShortArray();
        if (map_l is null || map_a is null || map_b is null)
            return -1;
        int entries = 1 << bits_per_sample;
        if (map_l.Length < entries || map_a.Length < entries || map_b.Length < entries)
            return -1;
        s._colourMapEntries = entries;
        s._colourMap = new byte[checked(entries * 3)];
        for (int i = 0; i < entries; i++) {
            s._colourMap[i] = unchecked((byte)((ushort)map_l[i] >> 8));
            s._colourMap[entries + i] = unchecked((byte)((ushort)map_a[i] >> 8));
            s._colourMap[2 * entries + i] = unchecked((byte)((ushort)map_b[i] >> 8));
        }
        return 0;
    }

    private static int get_tiff_directory_info(t4_tx_state_t s) {
        if (s._tiff is null)
            return -1;

        int bits_per_sample = s._tiff.GetFieldDefaulted(TiffTag.BITSPERSAMPLE)?[0].ToInt() ?? 1;
        int samples_per_pixel = s._tiff.GetFieldDefaulted(TiffTag.SAMPLESPERPIXEL)?[0].ToInt() ?? 1;
        t4_image_types_t image_type;
        if (samples_per_pixel == 1 && bits_per_sample == 1)
            image_type = t4_image_types_t.T4_IMAGE_TYPE_BILEVEL;
        else if (samples_per_pixel == 3 && bits_per_sample == 1)
            image_type = t4_image_types_t.T4_IMAGE_TYPE_COLOUR_BILEVEL;
        else if (samples_per_pixel == 4 && bits_per_sample == 1)
            image_type = t4_image_types_t.T4_IMAGE_TYPE_4COLOUR_BILEVEL;
        else if (samples_per_pixel == 1 && bits_per_sample == 8)
            image_type = t4_image_types_t.T4_IMAGE_TYPE_GRAY_8BIT;
        else if (samples_per_pixel == 1 && bits_per_sample > 8)
            image_type = t4_image_types_t.T4_IMAGE_TYPE_GRAY_12BIT;
        else if (samples_per_pixel == 3 && bits_per_sample == 8)
            image_type = t4_image_types_t.T4_IMAGE_TYPE_COLOUR_8BIT;
        else if (samples_per_pixel == 4 && bits_per_sample == 8)
            image_type = t4_image_types_t.T4_IMAGE_TYPE_4COLOUR_8BIT;
        else if (samples_per_pixel == 3 && bits_per_sample > 8)
            image_type = t4_image_types_t.T4_IMAGE_TYPE_COLOUR_12BIT;
        else if (samples_per_pixel == 4 && bits_per_sample > 8)
            image_type = t4_image_types_t.T4_IMAGE_TYPE_4COLOUR_12BIT;
        else
            return -1;

        int width = s._tiff.GetField(TiffTag.IMAGEWIDTH)?[0].ToInt() ?? 0;
        int length = s._tiff.GetField(TiffTag.IMAGELENGTH)?[0].ToInt() ?? 0;
        if (width <= 0 || length <= 0)
            return -1;

        float x_resolution = s._tiff.GetField(TiffTag.XRESOLUTION)?[0].ToFloat() ?? 0.0f;
        float y_resolution = s._tiff.GetField(TiffTag.YRESOLUTION)?[0].ToFloat() ?? 0.0f;
        ResUnit res_unit = (ResUnit)(s._tiff.GetFieldDefaulted(TiffTag.RESOLUTIONUNIT)?[0].ToInt() ?? (int)ResUnit.INCH);
        x_resolution *= 100.0f;
        y_resolution *= 100.0f;
        if (res_unit == ResUnit.INCH) {
            x_resolution /= 2.54f;
            y_resolution /= 2.54f;
        }
        int x = t4_tx_state_t.match_resolution(x_resolution, t4_tx_state_t.XResolutionTable);
        int y = t4_tx_state_t.match_resolution(y_resolution, t4_tx_state_t.YResolutionTable);
        t4_image_resolution_t resolution_code = x >= 0 && y >= 0
            ? t4_tx_state_t.ResolutionMap[y, x]
            : (t4_image_resolution_t)0;

        Compression compression = (Compression)(s._tiff.GetFieldDefaulted(TiffTag.COMPRESSION)?[0].ToInt() ?? (int)Compression.NONE);
        Photometric photo = (Photometric)(s._tiff.GetFieldDefaulted(TiffTag.PHOTOMETRIC)?[0].ToInt() ?? (int)Photometric.MINISWHITE);
        FillOrder fill = (FillOrder)(s._tiff.GetFieldDefaulted(TiffTag.FILLORDER)?[0].ToInt() ?? (int)FillOrder.LSB2MSB);
        t4_image_compression_t source_compression = compression switch {
            Compression.CCITTFAX3 => ((s._tiff.GetField(TiffTag.GROUP3OPTIONS)?[0].ToInt() ?? 0) & (int)Group3Opt.ENCODING2D) != 0
                ? t4_image_compression_t.T4_COMPRESSION_T4_2D : t4_image_compression_t.T4_COMPRESSION_T4_1D,
            Compression.CCITTFAX4 => t4_image_compression_t.T4_COMPRESSION_T6,
            Compression.JPEG => photo == (Photometric)10 ? t4_image_compression_t.T4_COMPRESSION_T42_T81 : t4_image_compression_t.T4_COMPRESSION_SYCC_T81,
            _ when (int)compression == COMPRESSION_T85 => t4_image_compression_t.T4_COMPRESSION_T85,
            _ when (int)compression == COMPRESSION_T43 => t4_image_compression_t.T4_COMPRESSION_T43,
            _ => t4_image_compression_t.T4_COMPRESSION_UNCOMPRESSED
        };

        var page = new T4TxPage {
            Compression = source_compression,
            ImageType = image_type,
            Width = width,
            Length = length,
            XResolution = checked((int)x_resolution),
            YResolution = checked((int)y_resolution),
            ResolutionCode = resolution_code,
            Photometric = (T4TxPhotometric)(int)photo,
            FillOrder = (T4TxFillOrder)(int)fill,
            PixelData = Array.Empty<byte>()
        };
        s._sourcePage = page;
        load_source_metadata(s, page);
        _ = read_colour_map(s, bits_per_sample);
        return 0;
    }

    private static int test_tiff_directory_info(t4_tx_state_t s) {
        if (s._tiff is null || s._sourcePage is null)
            return -1;
        int bits = s._tiff.GetFieldDefaulted(TiffTag.BITSPERSAMPLE)?[0].ToInt() ?? 1;
        int samples = s._tiff.GetFieldDefaulted(TiffTag.SAMPLESPERPIXEL)?[0].ToInt() ?? 1;
        t4_image_types_t type = samples switch {
            1 when bits == 1 => t4_image_types_t.T4_IMAGE_TYPE_BILEVEL,
            3 when bits == 1 => t4_image_types_t.T4_IMAGE_TYPE_COLOUR_BILEVEL,
            4 when bits == 1 => t4_image_types_t.T4_IMAGE_TYPE_4COLOUR_BILEVEL,
            1 when bits == 8 => t4_image_types_t.T4_IMAGE_TYPE_GRAY_8BIT,
            1 => t4_image_types_t.T4_IMAGE_TYPE_GRAY_12BIT,
            3 when bits == 8 => t4_image_types_t.T4_IMAGE_TYPE_COLOUR_8BIT,
            4 when bits == 8 => t4_image_types_t.T4_IMAGE_TYPE_4COLOUR_8BIT,
            3 => t4_image_types_t.T4_IMAGE_TYPE_COLOUR_12BIT,
            4 => t4_image_types_t.T4_IMAGE_TYPE_4COLOUR_12BIT,
            _ => (t4_image_types_t)(-1)
        };
        if (s.SourceMetadata.ImageType != type)
            return 1;
        int width = s._tiff.GetField(TiffTag.IMAGEWIDTH)?[0].ToInt() ?? 0;
        if (s.SourceMetadata.ImageWidth != width)
            return 2;
        float xr = s._tiff.GetField(TiffTag.XRESOLUTION)?[0].ToFloat() ?? 0;
        float yr = s._tiff.GetField(TiffTag.YRESOLUTION)?[0].ToFloat() ?? 0;
        ResUnit unit = (ResUnit)(s._tiff.GetFieldDefaulted(TiffTag.RESOLUTIONUNIT)?[0].ToInt() ?? (int)ResUnit.INCH);
        xr *= 100.0f;
        yr *= 100.0f;
        if (unit == ResUnit.INCH) { xr /= 2.54f; yr /= 2.54f; }
        if (s.SourceMetadata.XResolution != (int)xr)
            return 3;
        if (s.SourceMetadata.YResolution != (int)yr)
            return 4;
        return 0;
    }

    private static int open_tiff_input_file(t4_tx_state_t s, string file) {
        s._tiff = Tiff.Open(file, "r");
        return s._tiff is null ? -1 : 0;
    }

    private static int metadata_row_read_handler(object? user_data, Span<byte> buf, int len) {
        if (user_data is not t4_tx_state_t s || s._sourcePage is null)
            return 0;
        return s._sourcePage.ReadRow(buf, len);
    }

    private static int packing_row_write_handler(object? user_data, ReadOnlySpan<byte> buf, int len) {
        buf = buf[..len];
        if (user_data is not packer_t packer || packer.pointer < 0 || packer.pointer > packer.buffer.Length - buf.Length)
            return -1;
        buf.CopyTo(packer.buffer.AsSpan(packer.pointer));
        packer.pointer += buf.Length;
        packer.row++;
        return 0;
    }

    private static int embedded_comment_handler(object? user_data, ReadOnlySpan<byte> buf) {
        if (user_data is t4_tx_state_t s)
            s.Logging.Warning(buf.IsEmpty ? "T.85 comment: ---" : $"T.85 comment ({buf.Length}): {System.Text.Encoding.ASCII.GetString(buf)}");
        return 0;
    }

    private static int read_tiff_raw_image(t4_tx_state_t s) {
        if (s._tiff is null)
            return -1;
        int strips = s._tiff.NumberOfStrips();
        int total = 0;
        for (int i = 0; i < strips; i++)
            total = checked(total + checked((int)s._tiff.RawStripSize(i)));
        byte[] buffer = new byte[total];
        int offset = 0;
        for (int i = 0; i < strips; i++) {
            int length = checked((int)s._tiff.RawStripSize(i));
            int read = s._tiff.ReadRawStrip(i, buffer, offset, length);
            if (read < 0)
                return -1;
            offset += read;
        }
        s._noEncoderBuffer = buffer;
        s._noEncoderBufferLength = offset;
        s._noEncoderBufferPointer = 0;
        s._noEncoderBit = 0;
        return 0;
    }

    private static byte[] read_all_raw_strips(t4_tx_state_t s) {
        if (s._tiff is null)
            return Array.Empty<byte>();
        int strips = s._tiff.NumberOfStrips();
        int total = 0;
        for (int i = 0; i < strips; i++) total = checked(total + checked((int)s._tiff.RawStripSize(i)));
        byte[] data = new byte[total];
        int offset = 0;
        for (int i = 0; i < strips; i++) {
            int size = checked((int)s._tiff.RawStripSize(i));
            int count = s._tiff.ReadRawStrip(i, data, offset, size);
            if (count < 0) return Array.Empty<byte>();
            offset += count;
        }
        if (offset != data.Length) Array.Resize(ref data, offset);
        return data;
    }

    private static int install_decoded_page(t4_tx_state_t s, byte[] pixels) {
        if (s._sourcePage is null)
            return -1;
        T4TxPage source = s._sourcePage;
        s._sourcePage = new T4TxPage {
            Compression = t4_image_compression_t.T4_COMPRESSION_UNCOMPRESSED,
            ImageType = source.ImageType,
            Width = source.Width,
            Length = source.Length,
            XResolution = source.XResolution,
            YResolution = source.YResolution,
            ResolutionCode = source.ResolutionCode,
            Photometric = source.Photometric,
            FillOrder = T4TxFillOrder.LeastSignificantBitFirst,
            PixelData = pixels,
            StrideBytes = t4_tx_state_t.GetRowBytes(source.ImageType, source.Width)
        };
        return 0;
    }

    private static int read_tiff_t85_image(t4_tx_state_t s) {
        if (s._sourcePage is null)
            return -1;
        int row_bytes = checked((s.SourceMetadata.ImageWidth + 7) / 8);
        byte[] pixels = new byte[checked(row_bytes * s.SourceMetadata.ImageLength)];
        var packer = new packer_t { buffer = pixels };
        using T85DecodeState decoder = T85Decode.Initialize(null, packing_row_write_handler, packer);
        T85Decode.SetCommentHandler(decoder, 1000, embedded_comment_handler, s);
        T85Decode.SetImageSizeConstraints(decoder, checked((uint)s.SourceMetadata.ImageWidth), checked((uint)s.SourceMetadata.ImageLength));
        byte[] raw = read_all_raw_strips(s);
        if (raw.Length == 0)
            return -1;
        int result = T85Decode.Put(decoder, raw);
        if (result == (int)T85DecodeStatus.MoreData)
            result = T85Decode.Put(decoder, ReadOnlySpan<byte>.Empty);
        if (result != (int)T85DecodeStatus.Ok)
            return -1;
        return install_decoded_page(s, pixels);
    }

    private static int read_tiff_t43_image(t4_tx_state_t s) {
        if (s._sourcePage is null)
            return -1;
        int samples = 3;
        byte[] pixels = new byte[checked(samples * s.SourceMetadata.ImageWidth * s.SourceMetadata.ImageLength)];
        var packer = new packer_t { buffer = pixels };
        using T43DecodeState decoder = T43.t43_decode_init(null, packing_row_write_handler, packer);
        T43.t43_decode_set_comment_handler(decoder, 1000, embedded_comment_handler, s);
        T43.t43_decode_set_image_size_constraints(decoder, checked((uint)s.SourceMetadata.ImageWidth), checked((uint)s.SourceMetadata.ImageLength));
        byte[] raw = read_all_raw_strips(s);
        if (raw.Length == 0)
            return -1;
        int result = T43.t43_decode_put(decoder, raw);
        if (result == (int)T85DecodeStatus.MoreData)
            result = T43.t43_decode_put(decoder, ReadOnlySpan<byte>.Empty);
        if (result != (int)T85DecodeStatus.Ok)
            return -1;
        return install_decoded_page(s, pixels);
    }

    private static int read_tiff_t42_t81_image(t4_tx_state_t s) {
        if (s._sourcePage is null || s._tiff is null)
            return -1;
        int samples = s.SourceMetadata.ImageType is t4_image_types_t.T4_IMAGE_TYPE_COLOUR_8BIT or t4_image_types_t.T4_IMAGE_TYPE_COLOUR_12BIT ? 3 : 1;
        byte[] pixels = new byte[checked(samples * s.SourceMetadata.ImageWidth * s.SourceMetadata.ImageLength)];
        var packer = new packer_t { buffer = pixels };
        using T42DecodeState decoder = T42.t42_decode_init(null, packing_row_write_handler, packer);
        byte[] raw = read_all_raw_strips(s);
        if (raw.Length == 0)
            return -1;
        FieldValue[]? tables_field = s._tiff.GetField(TiffTag.JPEGTABLES);
        if (tables_field is not null && tables_field.Length > 1) {
            byte[]? tables = tables_field[1].ToByteArray();
            if (tables is not null && tables.Length > 4) {
                byte[] combined = new byte[checked(tables.Length - 4 + raw.Length)];
                tables.AsSpan(0, tables.Length - 2).CopyTo(combined);
                raw.AsSpan().CopyTo(combined.AsSpan(tables.Length - 4));
                raw = combined;
            }
        }
        _ = T42.t42_decode_put(decoder, raw);
        int result = T42.t42_decode_put(decoder, ReadOnlySpan<byte>.Empty);
        if (result != (int)T85DecodeStatus.Ok)
            return -1;
        return install_decoded_page(s, pixels);
    }

    private static int read_tiff_decompressed_image(t4_tx_state_t s) {
        if (s._tiff is null || s._sourcePage is null)
            return -1;
        int scanline = s._tiff.ScanlineSize();
        if (scanline <= 0)
            return -1;
        byte[] pixels = new byte[checked(scanline * s.SourceMetadata.ImageLength)];
        byte[] row = new byte[scanline];
        for (int y = 0; y < s.SourceMetadata.ImageLength; y++) {
            if (!s._tiff.ReadScanline(row, y, 0))
                return -1;
            row.CopyTo(pixels, y * scanline);
        }
        return install_decoded_page(s, pixels);
    }

    private static int read_tiff_image(t4_tx_state_t s) {
        if (s._sourcePage is null)
            return -1;
        s._noEncoderBufferLength = 0;
        s._noEncoderBufferPointer = 0;
        s._noEncoderBit = 0;

        bool alter_image = s.Metadata.ImageType != s.SourceMetadata.ImageType ||
                           s.Metadata.ImageWidth != s.SourceMetadata.ImageWidth ||
                           s.Metadata.ImageLength != s.SourceMetadata.ImageLength ||
                           !string.IsNullOrEmpty(s.HeaderInfo);
        bool raw_compatible = !alter_image && s.Metadata.Compression == s.SourceMetadata.Compression &&
            s.SourceMetadata.Compression is t4_image_compression_t.T4_COMPRESSION_T85 or t4_image_compression_t.T4_COMPRESSION_T85_L0 or
                t4_image_compression_t.T4_COMPRESSION_T43 or t4_image_compression_t.T4_COMPRESSION_T42_T81 or t4_image_compression_t.T4_COMPRESSION_SYCC_T81;
        if (raw_compatible)
            return read_tiff_raw_image(s);

        int result = s.SourceMetadata.Compression switch {
            t4_image_compression_t.T4_COMPRESSION_T85 or t4_image_compression_t.T4_COMPRESSION_T85_L0 => read_tiff_t85_image(s),
            t4_image_compression_t.T4_COMPRESSION_T43 => read_tiff_t43_image(s),
            t4_image_compression_t.T4_COMPRESSION_T42_T81 or t4_image_compression_t.T4_COMPRESSION_SYCC_T81 => read_tiff_t42_t81_image(s),
            _ => read_tiff_decompressed_image(s)
        };
        if (result < 0)
            return -1;
        s._sourcePage!.Restart();
        s._rowHandler = tiff_row_read_handler;
        s._rowHandlerUserData = s;
        return s.Metadata.ImageLength;
    }

    private static void tiff_tx_release(t4_tx_state_t s) {
        s._tiff?.Dispose();
        s._tiff = null;
        s._sourcePage = null;
        s._noEncoderBuffer = Array.Empty<byte>();
        s._noEncoderBufferLength = 0;
        s._noEncoderBufferPointer = 0;
        s._colourMap = Array.Empty<byte>();
        s._colourMapEntries = 0;
    }

    private static void set_image_width(t4_tx_state_t s, uint image_width) {
        s.Metadata.ImageWidth = checked((int)image_width);
        switch (s.Metadata.Compression) {
            case t4_image_compression_t.T4_COMPRESSION_T4_1D:
            case t4_image_compression_t.T4_COMPRESSION_T4_2D:
            case t4_image_compression_t.T4_COMPRESSION_T6:
                if (s._t4T6Encoder is not null) t4_t6_encode.t4_t6_encode_set_image_width(s._t4T6Encoder, checked((int)image_width));
                break;
            case t4_image_compression_t.T4_COMPRESSION_T85:
            case t4_image_compression_t.T4_COMPRESSION_T85_L0:
                if (s._t85Encoder is not null) T85Encode.SetImageWidth(s._t85Encoder, image_width);
                break;
            case t4_image_compression_t.T4_COMPRESSION_T42_T81:
            case t4_image_compression_t.T4_COMPRESSION_SYCC_T81:
                if (s._t42Encoder is not null) T42.t42_encode_set_image_width(s._t42Encoder, image_width);
                break;
            case t4_image_compression_t.T4_COMPRESSION_T43:
                if (s._t43Encoder is not null) T43.t43_encode_set_image_width(s._t43Encoder, image_width);
                break;
        }
    }

    private static void set_image_length(t4_tx_state_t s, uint image_length) {
        s.Metadata.ImageLength = checked((int)image_length);
        switch (s.Metadata.Compression) {
            case t4_image_compression_t.T4_COMPRESSION_T4_1D:
            case t4_image_compression_t.T4_COMPRESSION_T4_2D:
            case t4_image_compression_t.T4_COMPRESSION_T6:
                if (s._t4T6Encoder is not null) t4_t6_encode.t4_t6_encode_set_image_length(s._t4T6Encoder, checked((int)image_length));
                break;
            case t4_image_compression_t.T4_COMPRESSION_T85:
            case t4_image_compression_t.T4_COMPRESSION_T85_L0:
                if (s._t85Encoder is not null) T85Encode.SetImageLength(s._t85Encoder, image_length);
                break;
            case t4_image_compression_t.T4_COMPRESSION_T42_T81:
            case t4_image_compression_t.T4_COMPRESSION_SYCC_T81:
                if (s._t42Encoder is not null) T42.t42_encode_set_image_length(s._t42Encoder, image_length);
                break;
            case t4_image_compression_t.T4_COMPRESSION_T43:
                if (s._t43Encoder is not null) T43.t43_encode_set_image_length(s._t43Encoder, image_length);
                break;
        }
    }

    private static void t4_tx_set_image_type(t4_tx_state_t s, int image_type) {
        s.Metadata.ImageType = (t4_image_types_t)image_type;
        switch (s.Metadata.Compression) {
            case t4_image_compression_t.T4_COMPRESSION_T42_T81:
            case t4_image_compression_t.T4_COMPRESSION_SYCC_T81:
                if (s._t42Encoder is not null) T42.t42_encode_set_image_type(s._t42Encoder, image_type);
                break;
            case t4_image_compression_t.T4_COMPRESSION_T43:
                if (s._t43Encoder is not null) T43.t43_encode_set_image_type(s._t43Encoder, image_type);
                break;
        }
    }

    private static void reset_for_initialization(t4_tx_state_t s) {
        if (!s._released)
            t4_tx_release(s);
        s._released = false;
        s.SourceMetadata.CopyFrom(new T4TxMetadata());
        s.Metadata.CopyFrom(new T4TxMetadata());
        s._formatNegotiated = false;
        s._pageOpen = false;
        s._sourcePage = null;
        s._tiff = null;
        s._noEncoderBuffer = Array.Empty<byte>();
        s._noEncoderBufferLength = 0;
        s._noEncoderBufferPointer = 0;
        s._noEncoderBit = 0;
        s.PagesInFile = -1;
    }

    private static int prepare_image_row_pipeline(t4_tx_state_t s) {
        if (s._sourcePage is null)
            return -1;
        s._sourcePage.Restart();
        int source_row_bytes = s._sourcePage.RequiredRowBytes;
        if (source_row_bytes <= 0)
            return -1;
        s._sourceRowBuffer = t4_tx_state_t.EnsureBuffer(s._sourceRowBuffer, source_row_bytes);
        s._extraRowBuffer = t4_tx_state_t.EnsureBuffer(s._extraRowBuffer, source_row_bytes);
        s._imageRowHandler = tiff_row_read_handler;
        s._imageRowUserData = s;

        if (s.SourceMetadata.ImageType != s.Metadata.ImageType ||
            s.SourceMetadata.ImageWidth != s.Metadata.ImageWidth) {
            try {
                s._translator.Configure(
                    (ImageTranslateFormat)s.Metadata.ImageType,
                    s.Metadata.ImageWidth,
                    -1,
                    (ImageTranslateFormat)s.SourceMetadata.ImageType,
                    s.SourceMetadata.ImageWidth,
                    s.SourceMetadata.ImageLength,
                    translate_row_read2,
                    s);
            } catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException) {
                return -1;
            }
            s.Metadata.ImageLength = s._translator.OutputLength;
            s._imageRowHandler = translate_row_read;
            s._imageRowUserData = s;
        } else {
            s.Metadata.ImageLength = Math.Max(1, s.SourceMetadata.ImageLength / Math.Max(s._rowSquashingRatio, 1));
        }
        s._rowHandler = s._imageRowHandler;
        s._rowHandlerUserData = s._imageRowUserData;
        return 0;
    }

    private static void prepare_header_pipeline(t4_tx_state_t s) {
        s._headerText = null;
        s._headerRow = 0;
        s._headerRows = 0;
        if (string.IsNullOrEmpty(s.HeaderInfo)) {
            s._rowHandler = s._imageRowHandler;
            s._rowHandlerUserData = s._imageRowUserData;
            return;
        }
        make_header(s);
        s.GetHeaderScale(out s._headerXRepeats, out s._headerYRepeats);
        s._headerRows = t4_tx_state_t.HeaderCharacterRows * s._headerYRepeats;
        if (!s.HeaderOverlaysImage)
            s.Metadata.ImageLength = checked(s.Metadata.ImageLength + s._headerRows);
        s._rowHandler = header_row_read_handler;
        s._rowHandlerUserData = s;
    }

    private static bool encoder_exists(t4_tx_state_t s) => s._encoderCompression switch {
        t4_image_compression_t.T4_COMPRESSION_T4_1D or t4_image_compression_t.T4_COMPRESSION_T4_2D or t4_image_compression_t.T4_COMPRESSION_T6 => s._t4T6Encoder is not null,
        t4_image_compression_t.T4_COMPRESSION_T85 or t4_image_compression_t.T4_COMPRESSION_T85_L0 => s._t85Encoder is not null,
        t4_image_compression_t.T4_COMPRESSION_T42_T81 or t4_image_compression_t.T4_COMPRESSION_SYCC_T81 => s._t42Encoder is not null,
        t4_image_compression_t.T4_COMPRESSION_T43 => s._t43Encoder is not null,
        _ => false
    };

    private static int read_encoder_row(object? user_data, Span<byte> destination) {
        if (user_data is not t4_tx_state_t s)
            return 0;
        return s._rowHandler?.Invoke(s._rowHandlerUserData, destination, destination.Length) ?? 0;
    }

    private static int ensure_encoder(t4_tx_state_t s) {
        if (s._encoderCompression == s.Metadata.Compression && encoder_exists(s))
            return 0;
        release_encoder(s);
        s._encoderCompression = s.Metadata.Compression;
        try {
            switch (s.Metadata.Compression) {
                case t4_image_compression_t.T4_COMPRESSION_T4_1D:
                case t4_image_compression_t.T4_COMPRESSION_T4_2D:
                case t4_image_compression_t.T4_COMPRESSION_T6:
                    s._t4T6Encoder = t4_t6_encode.t4_t6_encode_init(
                        null,
                        (int)t4_tx_state_t.ToT4T6Compression(s.Metadata.Compression),
                        s.Metadata.ImageWidth,
                        s.Metadata.ImageLength,
                        s._rowHandler,
                        s._rowHandlerUserData);
                    return s._t4T6Encoder is null ? -1 : 0;
                case t4_image_compression_t.T4_COMPRESSION_T85:
                case t4_image_compression_t.T4_COMPRESSION_T85_L0:
                    s._t85Encoder = T85Encode.Initialize(null, checked((uint)s.Metadata.ImageWidth), checked((uint)s.Metadata.ImageLength), read_encoder_row, s);
                    return 0;
                case t4_image_compression_t.T4_COMPRESSION_T42_T81:
                case t4_image_compression_t.T4_COMPRESSION_SYCC_T81:
                    s._t42Encoder = T42.t42_encode_init(null, checked((uint)s.Metadata.ImageWidth), checked((uint)s.Metadata.ImageLength), read_encoder_row, s);
                    return 0;
                case t4_image_compression_t.T4_COMPRESSION_T43:
                    s._t43Encoder = T43.t43_encode_init(null, checked((uint)s.Metadata.ImageWidth), checked((uint)s.Metadata.ImageLength), read_encoder_row, s);
                    return 0;
                default:
                    s._encoderCompression = (t4_image_compression_t)0;
                    return -1;
            }
        } catch (Exception exception) when (exception is ArgumentException or OverflowException or InvalidOperationException) {
            s.Logging.Warning($"Unable to initialize {s.Metadata.Compression} encoder: {exception.Message}");
            release_encoder(s);
            return -1;
        }
    }

    private static int restart_encoder(t4_tx_state_t s) => s._encoderCompression switch {
        t4_image_compression_t.T4_COMPRESSION_T4_1D or t4_image_compression_t.T4_COMPRESSION_T4_2D or t4_image_compression_t.T4_COMPRESSION_T6 =>
            s._t4T6Encoder is null ? -1 : t4_t6_encode.t4_t6_encode_restart(s._t4T6Encoder, s.Metadata.ImageWidth, s.Metadata.ImageLength),
        t4_image_compression_t.T4_COMPRESSION_T85 or t4_image_compression_t.T4_COMPRESSION_T85_L0 =>
            s._t85Encoder is null ? -1 : T85Encode.Restart(s._t85Encoder, checked((uint)s.Metadata.ImageWidth), checked((uint)s.Metadata.ImageLength)),
        t4_image_compression_t.T4_COMPRESSION_T42_T81 or t4_image_compression_t.T4_COMPRESSION_SYCC_T81 =>
            s._t42Encoder is null ? -1 : T42.t42_encode_restart(s._t42Encoder, checked((uint)s.Metadata.ImageWidth), checked((uint)s.Metadata.ImageLength)),
        t4_image_compression_t.T4_COMPRESSION_T43 =>
            s._t43Encoder is null ? -1 : T43.t43_encode_restart(s._t43Encoder, checked((uint)s.Metadata.ImageWidth), checked((uint)s.Metadata.ImageLength)),
        _ => -1
    };

    private static int set_row_read_handler(t4_tx_state_t s, t4_row_read_handler_t? handler, object? user_data) {
        s._rowHandler = handler;
        s._rowHandlerUserData = user_data;
        return s._encoderCompression switch {
            t4_image_compression_t.T4_COMPRESSION_T4_1D or t4_image_compression_t.T4_COMPRESSION_T4_2D or t4_image_compression_t.T4_COMPRESSION_T6 =>
                s._t4T6Encoder is null ? -1 : t4_t6_encode.t4_t6_encode_set_row_read_handler(s._t4T6Encoder, handler, user_data),
            t4_image_compression_t.T4_COMPRESSION_T85 or t4_image_compression_t.T4_COMPRESSION_T85_L0 =>
                s._t85Encoder is null ? -1 : T85Encode.SetRowReadHandler(s._t85Encoder, read_encoder_row, s),
            t4_image_compression_t.T4_COMPRESSION_T42_T81 or t4_image_compression_t.T4_COMPRESSION_SYCC_T81 =>
                s._t42Encoder is null ? -1 : T42.t42_encode_set_row_read_handler(s._t42Encoder, read_encoder_row, s),
            t4_image_compression_t.T4_COMPRESSION_T43 =>
                s._t43Encoder is null ? -1 : T43.t43_encode_set_row_read_handler(s._t43Encoder, read_encoder_row, s),
            _ => -1
        };
    }

    private static void configure_encoder_options(t4_tx_state_t s) {
        if (s._t4T6Encoder is not null) {
            t4_t6_encode.t4_t6_encode_set_encoding(s._t4T6Encoder, (int)t4_tx_state_t.ToT4T6Compression(s.Metadata.Compression));
            t4_t6_encode.t4_t6_encode_set_min_bits_per_row(s._t4T6Encoder, s._minimumBitsPerRow);
            t4_t6_encode.t4_t6_encode_set_max_2d_rows_per_1d_row(
                s._t4T6Encoder,
                s._maximum2DRowsPer1DRow != 0 ? s._maximum2DRowsPer1DRow : -s.Metadata.YResolution);
        }
        if (s._t42Encoder is not null) {
            T42.t42_encode_set_image_type(s._t42Encoder, (int)s.Metadata.ImageType);
            s._t42Encoder.itu_ycc = s.Metadata.Compression == t4_image_compression_t.T4_COMPRESSION_SYCC_T81 ? 1 : 0;
        }
        if (s._t43Encoder is not null)
            T43.t43_encode_set_image_type(s._t43Encoder, t4_tx_state_t.ToT43ImageType(s.Metadata.ImageType));
    }

    private static int encoder_image_complete(t4_tx_state_t s) {
        if (s._noEncoderBufferLength > 0)
            return s._noEncoderBufferPointer >= s._noEncoderBufferLength ? t4_tx_state_t.EndOfData : 0;
        return s._encoderCompression switch {
            t4_image_compression_t.T4_COMPRESSION_T4_1D or t4_image_compression_t.T4_COMPRESSION_T4_2D or t4_image_compression_t.T4_COMPRESSION_T6 => s._t4T6Encoder is null ? t4_tx_state_t.EndOfData : t4_t6_encode.t4_t6_encode_image_complete(s._t4T6Encoder),
            t4_image_compression_t.T4_COMPRESSION_T85 or t4_image_compression_t.T4_COMPRESSION_T85_L0 => s._t85Encoder is null ? t4_tx_state_t.EndOfData : T85Encode.ImageComplete(s._t85Encoder),
            t4_image_compression_t.T4_COMPRESSION_T42_T81 or t4_image_compression_t.T4_COMPRESSION_SYCC_T81 => s._t42Encoder is null ? t4_tx_state_t.EndOfData : T42.t42_encode_image_complete(s._t42Encoder),
            t4_image_compression_t.T4_COMPRESSION_T43 => s._t43Encoder is null ? t4_tx_state_t.EndOfData : T43.t43_encode_image_complete(s._t43Encoder),
            _ => t4_tx_state_t.EndOfData
        };
    }

    private static int encoder_get(t4_tx_state_t s, Span<byte> destination) => s._encoderCompression switch {
        t4_image_compression_t.T4_COMPRESSION_T4_1D or t4_image_compression_t.T4_COMPRESSION_T4_2D or t4_image_compression_t.T4_COMPRESSION_T6 => s._t4T6Encoder is null ? 0 : t4_t6_encode.t4_t6_encode_get(s._t4T6Encoder, destination, destination.Length),
        t4_image_compression_t.T4_COMPRESSION_T85 or t4_image_compression_t.T4_COMPRESSION_T85_L0 => s._t85Encoder is null ? 0 : T85Encode.Get(s._t85Encoder, destination),
        t4_image_compression_t.T4_COMPRESSION_T42_T81 or t4_image_compression_t.T4_COMPRESSION_SYCC_T81 => s._t42Encoder is null ? 0 : T42.t42_encode_get(s._t42Encoder, destination),
        t4_image_compression_t.T4_COMPRESSION_T43 => s._t43Encoder is null ? 0 : T43.t43_encode_get(s._t43Encoder, destination),
        _ => 0
    };

    private static int release_encoder(t4_tx_state_t s) {
        int result = 0;
        if (s._t4T6Encoder is not null) result = t4_t6_encode.t4_t6_encode_release(s._t4T6Encoder);
        s._t4T6Encoder = null;
        if (s._t85Encoder is not null) result = T85Encode.Release(s._t85Encoder);
        s._t85Encoder = null;
        if (s._t42Encoder is not null) result = T42.t42_encode_release(s._t42Encoder);
        s._t42Encoder = null;
        if (s._t43Encoder is not null) result = T43.t43_encode_release(s._t43Encoder);
        s._t43Encoder = null;
        s._encoderCompression = (t4_image_compression_t)0;
        s._bitBufferBits = 0;
        s._bitBufferValue = 0;
        return result;
    }

    public static t4_tx_state_t? t4_tx_init(t4_tx_state_t? s, string? file, int start_page, int stop_page) {
        t4_tx_state_t result = s ?? new t4_tx_state_t();
        result.ThrowIfDisposed();
        reset_for_initialization(result);
        TIFF_FX_init();
        result.StartPageNumber = result.CurrentPageNumber = start_page >= 0 ? start_page : 0;
        result.StopPageNumber = stop_page >= 0 ? stop_page : int.MaxValue;
        result.Metadata.Compression = t4_image_compression_t.T4_COMPRESSION_NONE;
        result._rowHandler = tiff_row_read_handler;
        result._rowHandlerUserData = result;
        result._rowSquashingRatio = 1;
        result.SourceFile = string.IsNullOrWhiteSpace(file) ? null : file;
        result.Logging.Protocol = "T.4";
        result.Logging.Flow("Start tx document");
        if (result.SourceFile is not null) {
            if (open_tiff_input_file(result, result.SourceFile) < 0 ||
                !result._tiff!.SetDirectory(checked((short)result.CurrentPageNumber)) ||
                get_tiff_directory_info(result) != 0) {
                tiff_tx_release(result);
                if (s is null) result.Dispose();
                return null;
            }
            result.PagesInFile = t4_tx_state_t.get_tiff_total_pages(result._tiff);
        }
        result._released = false;
        return result;
    }

    public static int t4_tx_start_page(t4_tx_state_t s) {
        ArgumentNullException.ThrowIfNull(s);
        s.ThrowIfDisposed();
        s.Logging.Flow($"Start tx page {s.CurrentPageNumber} - compression {t4_rx.t4_compression_to_str((int)s.Metadata.Compression)}");
        if (s.CurrentPageNumber > s.StopPageNumber)
            return -1;
        if (s._tiff is not null) {
            if (!s._tiff.SetDirectory(checked((short)s.CurrentPageNumber)) ||
                get_tiff_directory_info(s) != 0 ||
                read_tiff_image(s) < 0)
                return -1;
        } else if (s._sourcePage is null) {
            s.Metadata.ImageLength = int.MaxValue;
        }

        if (s._noEncoderBufferLength > 0) {
            s._noEncoderBufferPointer = 0;
            s._noEncoderBit = 0;
            s._pageOpen = true;
            return 0;
        }

        if (s._sourcePage is null)
            return -1;
        s._sourcePage.Restart();
        s._sourceRowsRead = 0;
        s._bitBufferValue = 0;
        s._bitBufferBits = 0;
        s._pageOpen = false;
        if (prepare_image_row_pipeline(s) < 0)
            return -1;
        prepare_header_pipeline(s);
        if (ensure_encoder(s) < 0)
            return -1;
        set_image_width(s, checked((uint)s.Metadata.ImageWidth));
        set_image_length(s, checked((uint)s.Metadata.ImageLength));
        t4_tx_set_image_type(s, (int)s.Metadata.ImageType);
        if (restart_encoder(s) < 0 || set_row_read_handler(s, s._rowHandler, s._rowHandlerUserData) < 0)
            return -1;
        configure_encoder_options(s);
        s._pageOpen = true;
        return 0;
    }

    public static int t4_tx_next_page_has_different_format(t4_tx_state_t s) {
        ArgumentNullException.ThrowIfNull(s);
        s.ThrowIfDisposed();
        int next_page = s.CurrentPageNumber + 1;
        s.Logging.Flow($"Checking for the existence of page {next_page}");
        if (next_page > s.StopPageNumber || s._tiff is null || next_page >= s.PagesInFile)
            return -1;
        if (!s._tiff.SetDirectory(checked((short)next_page)))
            return -1;
        int result = test_tiff_directory_info(s);
        s._tiff.SetDirectory(checked((short)s.CurrentPageNumber));
        return result;
    }

    public static int t4_tx_image_complete(t4_tx_state_t s) {
        s.ThrowIfDisposed();
        return encoder_image_complete(s);

    }

    public static int t4_tx_get_bit(t4_tx_state_t s) {
        ArgumentNullException.ThrowIfNull(s);
        s.ThrowIfDisposed();
        if (s._noEncoderBufferLength > 0) {
            if (s._noEncoderBufferPointer >= s._noEncoderBufferLength)
                return t4_tx_state_t.EndOfData;
            int bit = (s._noEncoderBuffer[s._noEncoderBufferPointer] >> s._noEncoderBit) & 1;
            if (++s._noEncoderBit >= 8) {
                s._noEncoderBit = 0;
                s._noEncoderBufferPointer++;
            }
            return bit;
        }
        if (s._encoderCompression is t4_image_compression_t.T4_COMPRESSION_T4_1D or t4_image_compression_t.T4_COMPRESSION_T4_2D or t4_image_compression_t.T4_COMPRESSION_T6)
            return s._t4T6Encoder is null ? t4_tx_state_t.EndOfData : t4_t6_encode.t4_t6_encode_get_bit(s._t4T6Encoder);
        if (s._bitBufferBits == 0) {
            int count = encoder_get(s, s._bitBuffer);
            if (count <= 0) return t4_tx_state_t.EndOfData;
            s._bitBufferValue = s._bitBuffer[0];
            s._bitBufferBits = 8;
        }
        int result = s._bitBufferValue & 1;
        s._bitBufferValue >>= 1;
        s._bitBufferBits--;
        return result;
    }

    public static int t4_tx_set_tx_image_format(t4_tx_state_t s, int supported_compressions, int supported_image_sizes, int supported_bilevel_resolutions, int supported_colour_resolutions) {
        s.ThrowIfDisposed();

        // Every page owns its TIFF geometry and format. Load the current
        // directory before negotiating this page; never reuse page 0 values.
        if (s._tiff is not null) {
            if (s.CurrentPageNumber < 0 ||
                s.CurrentPageNumber >= s.PagesInFile ||
                !s._tiff.SetDirectory(checked((short)s.CurrentPageNumber)) ||
                get_tiff_directory_info(s) != 0) {
                return (int)T4ImageFormatStatus.Incompatible;
            }
        }

        if (s._sourcePage is null && s.SourceMetadata.ImageWidth <= 0)
            return (int)T4ImageFormatStatus.Incompatible;

        t4_image_compression_t supported = (t4_image_compression_t)supported_compressions;
        t4_image_support_t sizes = (t4_image_support_t)supported_image_sizes;
        t4_image_resolution_t bilevelResolutions = (t4_image_resolution_t)supported_bilevel_resolutions;
        t4_image_resolution_t colourResolutions = (t4_image_resolution_t)supported_colour_resolutions;

        t4_image_compression_t compression = (t4_image_compression_t)0;
        s.Metadata.ImageType = s.SourceMetadata.ImageType;

        if (s.SourceMetadata.ImageType != t4_image_types_t.T4_IMAGE_TYPE_BILEVEL) {
            bool colourOrGrayAllowed =
                colourResolutions != (t4_image_resolution_t)0 &&
                (supported & t4_tx_state_t.ColourCompressions) != 0 &&
                ((t4_tx_state_t.IsColourType(s.SourceMetadata.ImageType) && (supported & t4_image_compression_t.T4_COMPRESSION_COLOUR) != 0) ||
                 (t4_tx_state_t.IsGrayType(s.SourceMetadata.ImageType) && (supported & t4_image_compression_t.T4_COMPRESSION_GRAYSCALE) != 0));

            if (colourOrGrayAllowed) {
                if (s.SourceMetadata.ImageType == t4_image_types_t.T4_IMAGE_TYPE_COLOUR_BILEVEL &&
                    (supported & t4_image_compression_t.T4_COMPRESSION_T43) != 0) {
                    compression = t4_image_compression_t.T4_COMPRESSION_T43;
                } else if ((supported & t4_image_compression_t.T4_COMPRESSION_T42_T81) != 0) {
                    compression = t4_image_compression_t.T4_COMPRESSION_T42_T81;
                } else if ((supported & t4_image_compression_t.T4_COMPRESSION_T43) != 0) {
                    compression = t4_image_compression_t.T4_COMPRESSION_T43;
                } else if ((supported & t4_image_compression_t.T4_COMPRESSION_T45) != 0) {
                    compression = t4_image_compression_t.T4_COMPRESSION_T45;
                } else if ((supported & t4_image_compression_t.T4_COMPRESSION_SYCC_T81) != 0) {
                    compression = t4_image_compression_t.T4_COMPRESSION_SYCC_T81;
                }
            } else {
                if (t4_tx_state_t.IsColourType(s.SourceMetadata.ImageType) &&
                    (supported & t4_image_compression_t.T4_COMPRESSION_COLOUR_TO_BILEVEL) == 0) {
                    return (int)T4ImageFormatStatus.Incompatible;
                }

                if (t4_tx_state_t.IsGrayType(s.SourceMetadata.ImageType) &&
                    (supported & t4_image_compression_t.T4_COMPRESSION_GRAY_TO_BILEVEL) == 0) {
                    return (int)T4ImageFormatStatus.Incompatible;
                }

                s.Metadata.ImageType = t4_image_types_t.T4_IMAGE_TYPE_BILEVEL;
            }
        }

        if (s.Metadata.ImageType == t4_image_types_t.T4_IMAGE_TYPE_BILEVEL) {
            compression = t4_tx_state_t.SelectBilevelCompression(supported);
            if (compression == (t4_image_compression_t)0)
                return (int)T4ImageFormatStatus.Incompatible;
        }

        int entry = t4_tx_state_t.FindExactWidthResolution(s.SourceMetadata.ImageWidth, s.SourceMetadata.ResolutionCode);
        if (entry < 0 || (sizes & t4_tx_state_t.WidthAndResolutionInfo[entry].WidthCode) == 0)
            entry = t4_tx_state_t.FindPaddedWidthResolution(s.SourceMetadata.ImageWidth, s.SourceMetadata.ResolutionCode, sizes);

        s._rowSquashingRatio = 1;
        if (entry >= 0) {
            t4_tx_state_t.WidthResolutionInfo info = t4_tx_state_t.WidthAndResolutionInfo[entry];
            s.Metadata.WidthCode = info.WidthCode;
            // The width code describes the negotiated capability only. The
            // encoded page keeps the exact width and height of this directory.
            s.Metadata.ImageWidth = s.SourceMetadata.ImageWidth;
            s.Metadata.ImageLength = s.SourceMetadata.ImageLength;

            T4ImageFormatStatus resolutionStatus = select_resolution(
                s,
                info,
                supported,
                bilevelResolutions,
                colourResolutions);

            if (resolutionStatus != T4ImageFormatStatus.Ok)
                return (int)resolutionStatus;
        } else {
            if (s.SourceMetadata.ImageType is t4_image_types_t.T4_IMAGE_TYPE_BILEVEL or t4_image_types_t.T4_IMAGE_TYPE_COLOUR_BILEVEL)
                return (int)T4ImageFormatStatus.NoResolutionSupport;

            if ((supported & t4_image_compression_t.T4_COMPRESSION_RESCALING) == 0)
                return (int)T4ImageFormatStatus.NoSizeSupport;

            s.Metadata.WidthCode = t4_image_support_t.T4_SUPPORT_WIDTH_215MM;
            s.Metadata.ImageWidth = (int)t4_image_width_t.T4_WIDTH_200_A4;
            s.Metadata.ResolutionCode = t4_image_resolution_t.T4_RESOLUTION_200_200;
            s.Metadata.XResolution = t4_tx_state_t.code_to_x_resolution(s.Metadata.ResolutionCode);
            s.Metadata.YResolution = t4_tx_state_t.code_to_y_resolution(s.Metadata.ResolutionCode);
            s.Metadata.ImageLength = Math.Max(
                1,
                checked((int)((long)s.SourceMetadata.ImageLength * s.Metadata.ImageWidth /
                              Math.Max(s.SourceMetadata.ImageWidth, 1))));
        }

        s.Metadata.Compression = compression;
        s._formatNegotiated = true;
        if (ensure_encoder(s) < 0)
            return (int)T4ImageFormatStatus.Incompatible;
        set_image_width(s, checked((uint)s.Metadata.ImageWidth));
        set_image_length(s, checked((uint)s.Metadata.ImageLength));
        t4_tx_set_image_type(s, (int)s.Metadata.ImageType);
        configure_encoder_options(s);
        return (int)T4ImageFormatStatus.Ok;

    }

    public static void t4_tx_set_min_bits_per_row(t4_tx_state_t s, int bits) {
        s.ThrowIfDisposed();
        s._minimumBitsPerRow = Math.Max(bits, 0);
        if (s._t4T6Encoder is not null)
            t4_t6_encode.t4_t6_encode_set_min_bits_per_row(s._t4T6Encoder, s._minimumBitsPerRow);

    }

    public static void t4_tx_set_max_2d_rows_per_1d_row(t4_tx_state_t s, int max) {
        s.ThrowIfDisposed();
        s._maximum2DRowsPer1DRow = max;
        if (s._t4T6Encoder is not null)
            t4_t6_encode.t4_t6_encode_set_max_2d_rows_per_1d_row(s._t4T6Encoder, max);

    }

    public static void t4_tx_set_local_ident(t4_tx_state_t s, string? ident) {
        s.ThrowIfDisposed();
        s.LocalIdent = t4_tx_state_t.NormalizeOptional(ident, 21);

    }

    public static void t4_tx_set_header_info(t4_tx_state_t s, string? info) {
        s.ThrowIfDisposed();
        s.HeaderInfo = t4_tx_state_t.NormalizeOptional(info, 50);

    }

    public static void t4_tx_set_header_tz(t4_tx_state_t s, TimeZoneInfo? tz) {
        s.ThrowIfDisposed();
        s.HeaderTimeZone = tz;

    }

    public static void t4_tx_set_header_overlays_image(t4_tx_state_t s, bool header_overlays_image) {
        s.ThrowIfDisposed();
        s.HeaderOverlaysImage = header_overlays_image;

    }

    public static int t4_tx_get_pages_in_file(t4_tx_state_t s) {
        s.ThrowIfDisposed();
        if (s._tiff is not null)
            s.PagesInFile = t4_tx_state_t.get_tiff_total_pages(s._tiff);
        else if (s._sourcePage is not null)
            s.PagesInFile = 1;
        return s.PagesInFile;

    }

    public static void t4_tx_get_transfer_statistics(t4_tx_state_t s, t4_stats_t t) {
        ArgumentNullException.ThrowIfNull(t);
        s.ThrowIfDisposed();
        t.pages_transferred = Math.Max(s.CurrentPageNumber - s.StartPageNumber, 0);
        t.pages_in_file = s.PagesInFile;
        t.bad_rows = 0;
        t.longest_bad_row_run = 0;
        t.image_type = (int)s.SourceMetadata.ImageType;
        t.image_width = s.SourceMetadata.ImageWidth;
        t.image_length = s.SourceMetadata.ImageLength;
        t.image_x_resolution = s.SourceMetadata.XResolution;
        t.image_y_resolution = s.SourceMetadata.YResolution;
        t.type = (int)s.Metadata.ImageType;
        t.compression = (int)s.Metadata.Compression;
        t.x_resolution = s.Metadata.XResolution;
        t.y_resolution = s.Metadata.YResolution;
        t.width = s.EncoderImageWidth;
        t.length = s.EncoderImageLength;
        t.line_image_size = checked((int)Math.Min(
            int.MaxValue,
            s.EncoderCompressedImageSizeBits / 8L));

    }

    public static int t4_tx_restart_page(t4_tx_state_t s) {
        ArgumentNullException.ThrowIfNull(s);
        return t4_tx_start_page(s);
    }

    public static int t4_tx_end_page(t4_tx_state_t s) {
        ArgumentNullException.ThrowIfNull(s);
        s.CurrentPageNumber++;
        return 0;
    }

    public static int t4_tx_get(t4_tx_state_t s, Span<byte> buf, int max_len) {
        ArgumentNullException.ThrowIfNull(s);
        if ((uint)max_len > (uint)buf.Length)
            throw new ArgumentOutOfRangeException(nameof(max_len));
        s.ThrowIfDisposed();
        if (s._noEncoderBufferLength > 0) {
            int length = Math.Min(max_len, s._noEncoderBufferLength - s._noEncoderBufferPointer);
            if (length <= 0) return 0;
            s._noEncoderBuffer.AsSpan(s._noEncoderBufferPointer, length).CopyTo(buf);
            s._noEncoderBufferPointer += length;
            return length;
        }
        s._bitBufferBits = 0;
        s._bitBufferValue = 0;
        return encoder_get(s, buf[..max_len]);
    }

    public static int t4_tx_set_row_read_handler(t4_tx_state_t s, t4_row_read_handler_t? handler, object? user_data) {
        ArgumentNullException.ThrowIfNull(s);
        s.ThrowIfDisposed();
        s._rowHandler = handler;
        s._rowHandlerUserData = user_data;
        return set_row_read_handler(s, handler, user_data);
    }

    public static int t4_tx_get_tx_compression(t4_tx_state_t s) { ArgumentNullException.ThrowIfNull(s); return (int)s.Metadata.Compression; }
    public static int t4_tx_get_tx_image_type(t4_tx_state_t s) { ArgumentNullException.ThrowIfNull(s); return (int)s.Metadata.ImageType; }
    public static int t4_tx_get_tx_resolution(t4_tx_state_t s) { ArgumentNullException.ThrowIfNull(s); return (int)s.Metadata.ResolutionCode; }
    public static int t4_tx_get_tx_x_resolution(t4_tx_state_t s) { ArgumentNullException.ThrowIfNull(s); return s.Metadata.XResolution; }
    public static int t4_tx_get_tx_y_resolution(t4_tx_state_t s) { ArgumentNullException.ThrowIfNull(s); return s.Metadata.YResolution; }
    public static int t4_tx_get_tx_image_width(t4_tx_state_t s) { ArgumentNullException.ThrowIfNull(s); return s.Metadata.ImageWidth; }
    public static int t4_tx_get_tx_image_width_code(t4_tx_state_t s) { ArgumentNullException.ThrowIfNull(s); return (int)s.Metadata.WidthCode; }
    public static int t4_tx_get_current_page_in_file(t4_tx_state_t s) { ArgumentNullException.ThrowIfNull(s); return s.CurrentPageNumber; }
    public static T4TxLogger t4_tx_get_logging_state(t4_tx_state_t s) { ArgumentNullException.ThrowIfNull(s); return s.Logging; }

    public static int t4_tx_release(t4_tx_state_t s) {
        ArgumentNullException.ThrowIfNull(s);

        if (s._disposed)
            return -1;
        if (s._released)
            return 0;

        int result = release_encoder(s);
        s._translator.Release();
        tiff_tx_release(s);
        s._sourceRowBuffer = Array.Empty<byte>();
        s._extraRowBuffer = Array.Empty<byte>();
        s._headerText = null;
        s._pageOpen = false;
        s._released = true;
        return result;

    }

    public static int t4_tx_free(t4_tx_state_t? s) {
        if (s is null) return 0;
        int result = t4_tx_release(s);
        s.Dispose();
        return result;
    }
}
