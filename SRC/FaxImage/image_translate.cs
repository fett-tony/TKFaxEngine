/*
 * TKFaxEngine - managed C# port
 *
 * image_translate.cs - combined port of image_translate.h and image_translate.c
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2009 Steve Underwood
 *
 * This file is distributed under the GNU Lesser General Public License
 * version 2.1, matching the original source files.
 */

#nullable enable

using System.Buffers.Binary;

namespace TKFaxEngine.FaxImage;

/// <summary>
/// Image formats used by the T.4/T.30 fax image pipeline. Numeric values match
/// the native T4_IMAGE_TYPE_* constants.
/// </summary>
public enum ImageTranslateFormat {
    Bilevel = 0,
    ColourBilevel = 1,
    FourColourBilevel = 2,
    Gray8Bit = 3,
    Gray12Bit = 4,
    Colour8Bit = 5,
    FourColour8Bit = 6,
    Colour12Bit = 7,
    FourColour12Bit = 8
}

/// <summary>
/// Converts, rescales and optionally dithers image rows for fax processing.
/// </summary>
public sealed class ImageTranslateState : IDisposable {
    private byte[] _inputPixelRow = Array.Empty<byte>();
    private readonly byte[][] _rawPixelRows = { Array.Empty<byte>(), Array.Empty<byte>() };
    private readonly byte[][] _pixelRows = { Array.Empty<byte>(), Array.Empty<byte>() };

    private t4_row_read_handler_t? _rowReadHandler;
    private object? _rowReadUserData;

    private int _inputBytesPerPixel;
    private int _outputBytesPerPixel;
    private int _workingBytesPerPixel;
    private ImageTranslateFormat _workingFormat;

    private bool _resize;
    private int _requestedOutputLength;
    private int _nextInputRow;
    private int _cachedRow0Index = -1;
    private int _cachedRow1Index = -1;
    private int _nextWorkingRow;
    private int _outputRow;
    private bool _ditherInitialized;
    private bool _configured;
    private bool _disposed;

    public ImageTranslateState() {
    }

    public ImageTranslateState(
        ImageTranslateFormat outputFormat,
        int outputWidth,
        int outputLength,
        ImageTranslateFormat inputFormat,
        int inputWidth,
        int inputLength,
        t4_row_read_handler_t? rowReadHandler,
        object? rowReadUserData = null) {
        Configure(
            outputFormat,
            outputWidth,
            outputLength,
            inputFormat,
            inputWidth,
            inputLength,
            rowReadHandler,
            rowReadUserData);
    }

    public ImageTranslateFormat InputFormat { get; private set; }

    public int InputWidth { get; private set; }

    public int InputLength { get; private set; }

    public ImageTranslateFormat OutputFormat { get; private set; }

    public int OutputWidth { get; private set; }

    public int OutputLength { get; private set; }

    public bool ResizeEnabled => _resize;

    public int OutputRow => _outputRow;

    public bool IsComplete => !_configured || _outputRow >= OutputLength;

    /// <summary>Number of bytes required for one translated output row.</summary>
    public int RequiredOutputRowBytes => IsBilevel(OutputFormat)
        ? checked((OutputWidth + 7) / 8)
        : checked(OutputWidth * _outputBytesPerPixel);

    /// <summary>Initialises or reinitialises this state.</summary>
    public void Configure(
        ImageTranslateFormat outputFormat,
        int outputWidth,
        int outputLength,
        ImageTranslateFormat inputFormat,
        int inputWidth,
        int inputLength,
        t4_row_read_handler_t? rowReadHandler,
        object? rowReadUserData = null) {
        ThrowIfDisposed();
        ValidateFormat(inputFormat, nameof(inputFormat));
        ValidateFormat(outputFormat, nameof(outputFormat));
        if (inputWidth <= 0) {
            throw new ArgumentOutOfRangeException(nameof(inputWidth));
        }
        if (inputLength < 0) {
            throw new ArgumentOutOfRangeException(nameof(inputLength));
        }

        InputFormat = inputFormat;
        InputWidth = inputWidth;
        InputLength = inputLength;
        OutputFormat = outputFormat;

        _inputBytesPerPixel = BytesPerPixel(InputFormat);
        _outputBytesPerPixel = BytesPerPixel(OutputFormat);

        _resize = outputWidth > 0;
        OutputWidth = _resize ? outputWidth : inputWidth;
        if (OutputWidth <= 0) {
            throw new ArgumentOutOfRangeException(nameof(outputWidth));
        }

        _requestedOutputLength = outputLength;
        _rowReadHandler = rowReadHandler;
        _rowReadUserData = rowReadUserData;
        _configured = true;

        if (Restart(inputLength) != 0) {
            _configured = false;
            throw new InvalidOperationException("The image translation state could not be initialised.");
        }
    }

    /// <summary>Changes the callback used to pull source rows.</summary>
    public int SetRowReadHandler(t4_row_read_handler_t? handler, object? userData = null) {
        ThrowIfDisposed();
        _rowReadHandler = handler;
        _rowReadUserData = userData;
        return 0;
    }

    /// <summary>
    /// Restarts translation using a new source image length while retaining all
    /// format and width settings.
    /// </summary>
    public int Restart(int inputLength) {
        ThrowIfDisposed();
        if (!_configured || InputWidth <= 0 || OutputWidth <= 0 || inputLength < 0) {
            return -1;
        }

        InputLength = inputLength;
        if (_resize) {
            if (_requestedOutputLength > 0) {
                OutputLength = _requestedOutputLength;
            } else if (InputLength == 0) {
                OutputLength = 0;
            } else {
                OutputLength = Math.Max(1, checked((int)((long)InputLength * OutputWidth / InputWidth)));
            }
        } else {
            OutputLength = InputLength;
        }

        _workingFormat = IsBilevel(OutputFormat) ? ImageTranslateFormat.Gray8Bit : OutputFormat;
        _workingBytesPerPixel = BytesPerPixel(_workingFormat);

        int sourceRowSize = checked(InputWidth * _inputBytesPerPixel);
        int convertedInputRowSize = checked(InputWidth * _workingBytesPerPixel);
        int inputWorkSize = Math.Max(sourceRowSize, convertedInputRowSize);
        _inputPixelRow = ResizeAndClear(_inputPixelRow, inputWorkSize);

        int rawRowSize = Math.Max(
            convertedInputRowSize,
            checked(OutputWidth * _workingBytesPerPixel));
        for (int i = 0; i < 2; i++) {
            _rawPixelRows[i] = ResizeAndClear(_rawPixelRows[i], rawRowSize);
        }

        if (IsBilevel(OutputFormat)) {
            for (int i = 0; i < 2; i++) {
                _pixelRows[i] = ResizeAndClear(_pixelRows[i], OutputWidth);
            }
        } else {
            _pixelRows[0] = Array.Empty<byte>();
            _pixelRows[1] = Array.Empty<byte>();
        }

        _nextInputRow = 0;
        _cachedRow0Index = -1;
        _cachedRow1Index = -1;
        _nextWorkingRow = 0;
        _outputRow = 0;
        _ditherInitialized = false;
        return 0;
    }

    /// <summary>
    /// Produces the next translated row. Returns the number of bytes written,
    /// zero at end of image, or -1 if the destination is too small.
    /// </summary>
    public int TranslateRow(Span<byte> destination) {
        ThrowIfDisposed();
        if (!_configured || _outputRow >= OutputLength) {
            return 0;
        }

        int required = RequiredOutputRowBytes;
        if (destination.Length < required) {
            return -1;
        }

        if (IsBilevel(OutputFormat)) {
            int result = DitherRow(destination[..required]);
            if (result > 0) {
                _outputRow++;
            }
            return result;
        }

        int rowLength = BuildWorkingRow(_outputRow, destination[..required]);
        if (rowLength != checked(OutputWidth * _workingBytesPerPixel)) {
            _outputRow = OutputLength;
            return 0;
        }

        _outputRow++;
        return required;
    }

    public int TranslateRow(byte[] destination, int length) {
        ArgumentNullException.ThrowIfNull(destination);
        if (length < 0 || length > destination.Length) {
            return -1;
        }
        return TranslateRow(destination.AsSpan(0, length));
    }

    /// <summary>Releases internal row buffers. The state can be configured again.</summary>
    public int Release() {
        if (_disposed) {
            return -1;
        }

        _inputPixelRow = Array.Empty<byte>();
        _rawPixelRows[0] = Array.Empty<byte>();
        _rawPixelRows[1] = Array.Empty<byte>();
        _pixelRows[0] = Array.Empty<byte>();
        _pixelRows[1] = Array.Empty<byte>();
        _configured = false;
        _outputRow = 0;
        return 0;
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }
        Release();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private int BuildWorkingRow(int outputRowIndex, Span<byte> destination) {
        if (outputRowIndex != _nextWorkingRow || outputRowIndex < 0 || outputRowIndex >= OutputLength) {
            return 0;
        }

        int result = _resize
            ? ResizeRow(outputRowIndex, destination)
            : GetAndConvertSourceRow(destination);

        if (result == checked(OutputWidth * _workingBytesPerPixel)) {
            _nextWorkingRow++;
        }
        return result;
    }

    private int GetAndConvertSourceRow(Span<byte> destination) {
        if (_rowReadHandler is null || _nextInputRow >= InputLength) {
            return 0;
        }

        int sourceRowSize = checked(InputWidth * _inputBytesPerPixel);
        int convertedRowSize = checked(InputWidth * _workingBytesPerPixel);
        if (_inputPixelRow.Length < sourceRowSize || destination.Length < convertedRowSize) {
            return 0;
        }

        Span<byte> source = _inputPixelRow.AsSpan(0, sourceRowSize);
        int read = _rowReadHandler(_rowReadUserData, source, source.Length);
        if (read != sourceRowSize) {
            return 0;
        }

        ConvertRow(source, destination[..convertedRowSize], InputWidth, InputFormat, _workingFormat);
        _nextInputRow++;
        return convertedRowSize;
    }

    private int ResizeRow(int outputRowIndex, Span<byte> destination) {
        if (OutputWidth <= 0 || OutputLength <= 0 || InputWidth <= 0 || InputLength <= 0) {
            return 0;
        }

        double sourceY = OutputLength == 1 || InputLength == 1
            ? 0.0
            : (double)outputRowIndex * (InputLength - 1) / (OutputLength - 1);
        int y0 = Math.Clamp((int)Math.Floor(sourceY), 0, InputLength - 1);
        int y1 = Math.Min(y0 + 1, InputLength - 1);
        double fy = sourceY - y0;

        if (!EnsureSourceRows(y0, y1)) {
            return 0;
        }

        ReadOnlySpan<byte> row0 = GetCachedRow(y0);
        ReadOnlySpan<byte> row1 = GetCachedRow(y1);
        int outputRowBytes = checked(OutputWidth * _workingBytesPerPixel);
        if (destination.Length < outputRowBytes) {
            return 0;
        }

        if (IsSixteenBitFormat(_workingFormat)) {
            int components = _workingBytesPerPixel / 2;
            for (int xOut = 0; xOut < OutputWidth; xOut++) {
                double sourceX = OutputWidth == 1 || InputWidth == 1
                    ? 0.0
                    : (double)xOut * (InputWidth - 1) / (OutputWidth - 1);
                int x0 = Math.Clamp((int)Math.Floor(sourceX), 0, InputWidth - 1);
                int x1 = Math.Min(x0 + 1, InputWidth - 1);
                double fx = sourceX - x0;

                for (int component = 0; component < components; component++) {
                    int p00 = ReadUInt16(row0, (x0 * components + component) * 2);
                    int p01 = ReadUInt16(row0, (x1 * components + component) * 2);
                    int p10 = ReadUInt16(row1, (x0 * components + component) * 2);
                    int p11 = ReadUInt16(row1, (x1 * components + component) * 2);
                    double top = p00 + (p01 - p00) * fx;
                    double bottom = p10 + (p11 - p10) * fx;
                    ushort value = SaturateUInt16(top + (bottom - top) * fy);
                    WriteUInt16(destination, (xOut * components + component) * 2, value);
                }
            }
        } else {
            int components = _workingBytesPerPixel;
            for (int xOut = 0; xOut < OutputWidth; xOut++) {
                double sourceX = OutputWidth == 1 || InputWidth == 1
                    ? 0.0
                    : (double)xOut * (InputWidth - 1) / (OutputWidth - 1);
                int x0 = Math.Clamp((int)Math.Floor(sourceX), 0, InputWidth - 1);
                int x1 = Math.Min(x0 + 1, InputWidth - 1);
                double fx = sourceX - x0;

                for (int component = 0; component < components; component++) {
                    int p00 = row0[x0 * components + component];
                    int p01 = row0[x1 * components + component];
                    int p10 = row1[x0 * components + component];
                    int p11 = row1[x1 * components + component];
                    double top = p00 + (p01 - p00) * fx;
                    double bottom = p10 + (p11 - p10) * fx;
                    destination[xOut * components + component] = SaturateByte(top + (bottom - top) * fy);
                }
            }
        }

        return outputRowBytes;
    }

    private bool EnsureSourceRows(int y0, int y1) {
        if (_cachedRow1Index < 0) {
            if (!ReadNextConvertedSourceRow(_rawPixelRows[1])) {
                return false;
            }
            _cachedRow1Index = 0;
            _rawPixelRows[1].AsSpan().CopyTo(_rawPixelRows[0]);
            _cachedRow0Index = 0;
        }

        while (_cachedRow1Index < y1) {
            (_rawPixelRows[0], _rawPixelRows[1]) = (_rawPixelRows[1], _rawPixelRows[0]);
            _cachedRow0Index = _cachedRow1Index;
            if (!ReadNextConvertedSourceRow(_rawPixelRows[1])) {
                return false;
            }
            _cachedRow1Index++;
        }

        return (y0 == _cachedRow0Index || y0 == _cachedRow1Index)
            && (y1 == _cachedRow0Index || y1 == _cachedRow1Index);
    }

    private bool ReadNextConvertedSourceRow(byte[] destination) {
        int sourceRowSize = checked(InputWidth * _inputBytesPerPixel);
        int convertedRowSize = checked(InputWidth * _workingBytesPerPixel);
        if (_rowReadHandler is null || _nextInputRow >= InputLength || destination.Length < convertedRowSize) {
            return false;
        }

        Span<byte> source = _inputPixelRow.AsSpan(0, sourceRowSize);
        int read = _rowReadHandler(_rowReadUserData, source, source.Length);
        if (read != sourceRowSize) {
            return false;
        }

        ConvertRow(source, destination.AsSpan(0, convertedRowSize), InputWidth, InputFormat, _workingFormat);
        if (destination.Length > convertedRowSize) {
            destination.AsSpan(convertedRowSize).Clear();
        }
        _nextInputRow++;
        return true;
    }

    private ReadOnlySpan<byte> GetCachedRow(int rowIndex) {
        if (rowIndex == _cachedRow0Index) {
            return _rawPixelRows[0].AsSpan(0, checked(InputWidth * _workingBytesPerPixel));
        }
        if (rowIndex == _cachedRow1Index) {
            return _rawPixelRows[1].AsSpan(0, checked(InputWidth * _workingBytesPerPixel));
        }
        return ReadOnlySpan<byte>.Empty;
    }

    private int DitherRow(Span<byte> destination) {
        int y = _outputRow;
        if (!_ditherInitialized) {
            if (BuildWorkingRow(0, _pixelRows[0]) != OutputWidth) {
                return 0;
            }
            if (OutputLength > 1) {
                if (BuildWorkingRow(1, _pixelRows[1]) != OutputWidth) {
                    return 0;
                }
            } else {
                _pixelRows[1].AsSpan().Clear();
            }
            _ditherInitialized = true;
        } else {
            (_pixelRows[0], _pixelRows[1]) = (_pixelRows[1], _pixelRows[0]);
            int lookAheadRow = y + 1;
            if (lookAheadRow < OutputLength) {
                if (BuildWorkingRow(lookAheadRow, _pixelRows[1]) != OutputWidth) {
                    return 0;
                }
            } else {
                _pixelRows[1].AsSpan().Clear();
            }
        }

        Span<byte> current = _pixelRows[0].AsSpan(0, OutputWidth);
        Span<byte> next = _pixelRows[1].AsSpan(0, OutputWidth);
        if ((y & 1) == 0) {
            DitherLeftToRight(current, next);
        } else {
            DitherRightToLeft(current, next);
        }

        destination.Clear();
        for (int x = 0; x < OutputWidth; x++) {
            if (current[x] <= 128) {
                destination[x >> 3] |= (byte)(1 << (7 - (x & 7)));
            }
        }
        return (OutputWidth + 7) / 8;
    }

    private static void DitherLeftToRight(Span<byte> current, Span<byte> next) {
        for (int x = 0; x < current.Length; x++) {
            int oldPixel = current[x];
            int newPixel = oldPixel >= 128 ? 255 : 0;
            int error = oldPixel - newPixel;
            current[x] = (byte)newPixel;

            if (x + 1 < current.Length) {
                current[x + 1] = SaturateByte(current[x + 1] + (7 * error) / 16);
            }
            if (x > 0) {
                next[x - 1] = SaturateByte(next[x - 1] + (3 * error) / 16);
            }
            next[x] = SaturateByte(next[x] + (5 * error) / 16);
            if (x + 1 < next.Length) {
                next[x + 1] = SaturateByte(next[x + 1] + error / 16);
            }
        }
    }

    private static void DitherRightToLeft(Span<byte> current, Span<byte> next) {
        for (int x = current.Length - 1; x >= 0; x--) {
            int oldPixel = current[x];
            int newPixel = oldPixel >= 128 ? 255 : 0;
            int error = oldPixel - newPixel;
            current[x] = (byte)newPixel;

            if (x > 0) {
                current[x - 1] = SaturateByte(current[x - 1] + (7 * error) / 16);
            }
            if (x + 1 < next.Length) {
                next[x + 1] = SaturateByte(next[x + 1] + (3 * error) / 16);
            }
            next[x] = SaturateByte(next[x] + (5 * error) / 16);
            if (x > 0) {
                next[x - 1] = SaturateByte(next[x - 1] + error / 16);
            }
        }
    }

    private static void ConvertRow(
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        int pixels,
        ImageTranslateFormat inputFormat,
        ImageTranslateFormat outputFormat) {
        if (inputFormat == outputFormat) {
            source[..checked(pixels * BytesPerPixel(inputFormat))].CopyTo(destination);
            return;
        }

        for (int i = 0; i < pixels; i++) {
            ReadPixel(source, i, inputFormat, out ushort red, out ushort green, out ushort blue, out ushort black, out bool sourceWasGray, out ushort grayValue);
            WritePixel(destination, i, outputFormat, red, green, blue, black, sourceWasGray, grayValue, inputFormat);
        }
    }

    private static void ReadPixel(
        ReadOnlySpan<byte> source,
        int pixel,
        ImageTranslateFormat format,
        out ushort red,
        out ushort green,
        out ushort blue,
        out ushort black,
        out bool sourceWasGray,
        out ushort grayValue) {
        red = green = blue = black = grayValue = 0;
        sourceWasGray = false;

        switch (format) {
            case ImageTranslateFormat.Bilevel:
            case ImageTranslateFormat.Gray8Bit: {
                    byte gray8 = source[pixel];
                    grayValue = (ushort)(gray8 << 8);
                    red = green = blue = grayValue;
                    sourceWasGray = true;
                    break;
                }
            case ImageTranslateFormat.Gray12Bit:
                grayValue = ReadUInt16(source, pixel * 2);
                red = green = blue = grayValue;
                sourceWasGray = true;
                break;
            case ImageTranslateFormat.ColourBilevel:
            case ImageTranslateFormat.Colour8Bit: {
                    int offset = pixel * 3;
                    red = (ushort)(source[offset] << 8);
                    green = (ushort)(source[offset + 1] << 8);
                    blue = (ushort)(source[offset + 2] << 8);
                    break;
                }
            case ImageTranslateFormat.Colour12Bit: {
                    int offset = pixel * 6;
                    red = ReadUInt16(source, offset);
                    green = ReadUInt16(source, offset + 2);
                    blue = ReadUInt16(source, offset + 4);
                    break;
                }
            case ImageTranslateFormat.FourColourBilevel:
            case ImageTranslateFormat.FourColour8Bit: {
                    int offset = pixel * 4;
                    ushort cyan = (ushort)(source[offset] << 8);
                    ushort magenta = (ushort)(source[offset + 1] << 8);
                    ushort yellow = (ushort)(source[offset + 2] << 8);
                    black = (ushort)(source[offset + 3] << 8);
                    CmykToRgb(cyan, magenta, yellow, black, out red, out green, out blue);
                    break;
                }
            case ImageTranslateFormat.FourColour12Bit: {
                    int offset = pixel * 8;
                    ushort cyan = ReadUInt16(source, offset);
                    ushort magenta = ReadUInt16(source, offset + 2);
                    ushort yellow = ReadUInt16(source, offset + 4);
                    black = ReadUInt16(source, offset + 6);
                    CmykToRgb(cyan, magenta, yellow, black, out red, out green, out blue);
                    break;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(format));
        }
    }

    private static void WritePixel(
        Span<byte> destination,
        int pixel,
        ImageTranslateFormat format,
        ushort red,
        ushort green,
        ushort blue,
        ushort black,
        bool sourceWasGray,
        ushort grayValue,
        ImageTranslateFormat inputFormat) {
        switch (format) {
            case ImageTranslateFormat.Bilevel:
            case ImageTranslateFormat.Gray8Bit:
                destination[pixel] = sourceWasGray
                    ? (byte)(grayValue >> 8)
                    : Luma8(red, green, blue);
                break;

            case ImageTranslateFormat.Gray12Bit: {
                    ushort gray16 = sourceWasGray ? grayValue : Luma16(red, green, blue);
                    WriteUInt16(destination, pixel * 2, gray16);
                    break;
                }

            case ImageTranslateFormat.ColourBilevel:
            case ImageTranslateFormat.Colour8Bit: {
                    int offset = pixel * 3;
                    if (sourceWasGray) {
                        WriteGrayAsColour8(destination, offset, grayValue, inputFormat == ImageTranslateFormat.Gray12Bit);
                    } else {
                        destination[offset] = (byte)(red >> 8);
                        destination[offset + 1] = (byte)(green >> 8);
                        destination[offset + 2] = (byte)(blue >> 8);
                    }
                    break;
                }

            case ImageTranslateFormat.Colour12Bit: {
                    int offset = pixel * 6;
                    if (sourceWasGray) {
                        WriteGrayAsColour16(destination, offset, grayValue, inputFormat == ImageTranslateFormat.Gray12Bit);
                    } else {
                        WriteUInt16(destination, offset, red);
                        WriteUInt16(destination, offset + 2, green);
                        WriteUInt16(destination, offset + 4, blue);
                    }
                    break;
                }

            case ImageTranslateFormat.FourColourBilevel:
            case ImageTranslateFormat.FourColour8Bit: {
                    RgbToCmyk(red, green, blue, out ushort cyan, out ushort magenta, out ushort yellow, out ushort key);
                    int offset = pixel * 4;
                    destination[offset] = (byte)(cyan >> 8);
                    destination[offset + 1] = (byte)(magenta >> 8);
                    destination[offset + 2] = (byte)(yellow >> 8);
                    destination[offset + 3] = (byte)(key >> 8);
                    break;
                }

            case ImageTranslateFormat.FourColour12Bit: {
                    RgbToCmyk(red, green, blue, out ushort cyan, out ushort magenta, out ushort yellow, out ushort key);
                    int offset = pixel * 8;
                    WriteUInt16(destination, offset, cyan);
                    WriteUInt16(destination, offset + 2, magenta);
                    WriteUInt16(destination, offset + 4, yellow);
                    WriteUInt16(destination, offset + 6, key);
                    break;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(format));
        }
    }

    private static void WriteGrayAsColour8(Span<byte> destination, int offset, ushort gray16, bool inputWas16Bit) {
        uint gray = inputWas16Bit ? gray16 : (uint)(gray16 >> 8);
        if (inputWas16Bit) {
            destination[offset] = SaturateByte((gray * 36532U) >> 23);
            destination[offset + 1] = SaturateByte((gray * 37216U) >> 24);
            destination[offset + 2] = SaturateByte((gray * 47900U) >> 22);
        } else {
            destination[offset] = SaturateByte((gray * 36532U) >> 15);
            destination[offset + 1] = SaturateByte((gray * 37216U) >> 16);
            destination[offset + 2] = SaturateByte((gray * 47900U) >> 14);
        }
    }

    private static void WriteGrayAsColour16(Span<byte> destination, int offset, ushort gray16, bool inputWas16Bit) {
        uint gray = inputWas16Bit ? gray16 : (uint)(gray16 >> 8);
        ushort red;
        ushort green;
        ushort blue;
        if (inputWas16Bit) {
            red = SaturateUInt16((gray * 36532U) >> 15);
            green = SaturateUInt16((gray * 37216U) >> 16);
            blue = SaturateUInt16((gray * 47900U) >> 14);
        } else {
            red = SaturateUInt16((gray * 36532U) >> 7);
            green = SaturateUInt16((gray * 37216U) >> 8);
            blue = SaturateUInt16((gray * 47900U) >> 6);
        }
        WriteUInt16(destination, offset, red);
        WriteUInt16(destination, offset + 2, green);
        WriteUInt16(destination, offset + 4, blue);
    }

    private static byte Luma8(ushort red, ushort green, ushort blue) {
        ulong value = (ulong)red * 19595UL + (ulong)green * 38469UL + (ulong)blue * 7472UL;
        return SaturateByte(value >> 24);
    }

    private static ushort Luma16(ushort red, ushort green, ushort blue) {
        ulong value = (ulong)red * 19595UL + (ulong)green * 38469UL + (ulong)blue * 7472UL;
        return SaturateUInt16(value >> 16);
    }

    private static void CmykToRgb(
        ushort cyan,
        ushort magenta,
        ushort yellow,
        ushort key,
        out ushort red,
        out ushort green,
        out ushort blue) {
        red = (ushort)(ushort.MaxValue - Math.Min(ushort.MaxValue, cyan + key));
        green = (ushort)(ushort.MaxValue - Math.Min(ushort.MaxValue, magenta + key));
        blue = (ushort)(ushort.MaxValue - Math.Min(ushort.MaxValue, yellow + key));
    }

    private static void RgbToCmyk(
        ushort red,
        ushort green,
        ushort blue,
        out ushort cyan,
        out ushort magenta,
        out ushort yellow,
        out ushort key) {
        int maximum = Math.Max(red, Math.Max(green, blue));
        key = (ushort)(ushort.MaxValue - maximum);
        if (key == ushort.MaxValue) {
            cyan = magenta = yellow = 0;
            return;
        }

        int denominator = ushort.MaxValue - key;
        cyan = SaturateUInt16((long)(ushort.MaxValue - red - key) * ushort.MaxValue / denominator);
        magenta = SaturateUInt16((long)(ushort.MaxValue - green - key) * ushort.MaxValue / denominator);
        yellow = SaturateUInt16((long)(ushort.MaxValue - blue - key) * ushort.MaxValue / denominator);
    }

    private static int BytesPerPixel(ImageTranslateFormat format) => format switch {
        ImageTranslateFormat.Bilevel => 1,
        ImageTranslateFormat.Gray8Bit => 1,
        ImageTranslateFormat.Gray12Bit => 2,
        ImageTranslateFormat.ColourBilevel => 3,
        ImageTranslateFormat.Colour8Bit => 3,
        ImageTranslateFormat.FourColourBilevel => 4,
        ImageTranslateFormat.FourColour8Bit => 4,
        ImageTranslateFormat.Colour12Bit => 6,
        ImageTranslateFormat.FourColour12Bit => 8,
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    private static bool IsBilevel(ImageTranslateFormat format) => format is
        ImageTranslateFormat.Bilevel or
        ImageTranslateFormat.ColourBilevel or
        ImageTranslateFormat.FourColourBilevel;

    private static bool IsSixteenBitFormat(ImageTranslateFormat format) => format is
        ImageTranslateFormat.Gray12Bit or
        ImageTranslateFormat.Colour12Bit or
        ImageTranslateFormat.FourColour12Bit;

    private static void ValidateFormat(ImageTranslateFormat format, string parameterName) {
        if ((int)format < (int)ImageTranslateFormat.Bilevel
            || (int)format > (int)ImageTranslateFormat.FourColour12Bit) {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static byte[] ResizeAndClear(byte[] source, int length) {
        if (source.Length != length) {
            source = new byte[length];
        } else {
            source.AsSpan().Clear();
        }
        return source;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> source, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(offset, 2));

    private static void WriteUInt16(Span<byte> destination, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(offset, 2), value);

    private static byte SaturateByte(double value) =>
        value <= byte.MinValue ? byte.MinValue :
        value >= byte.MaxValue ? byte.MaxValue :
        (byte)value;

    private static byte SaturateByte(long value) =>
        value <= byte.MinValue ? byte.MinValue :
        value >= byte.MaxValue ? byte.MaxValue :
        (byte)value;

    private static byte SaturateByte(uint value) =>
        value >= byte.MaxValue ? byte.MaxValue : (byte)value;

    private static byte SaturateByte(ulong value) =>
        value >= byte.MaxValue ? byte.MaxValue : (byte)value;

    private static ushort SaturateUInt16(double value) =>
        value <= ushort.MinValue ? ushort.MinValue :
        value >= ushort.MaxValue ? ushort.MaxValue :
        (ushort)value;

    private static ushort SaturateUInt16(long value) =>
        value <= ushort.MinValue ? ushort.MinValue :
        value >= ushort.MaxValue ? ushort.MaxValue :
        (ushort)value;

    private static ushort SaturateUInt16(uint value) =>
        value >= ushort.MaxValue ? ushort.MaxValue : (ushort)value;

    private static ushort SaturateUInt16(ulong value) =>
        value >= ushort.MaxValue ? ushort.MaxValue : (ushort)value;

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}

/// <summary>
/// Compatibility façade retaining the original C function names.
/// </summary>
public static class ImageTranslateApi {
    public const int T4_IMAGE_TYPE_BILEVEL = (int)ImageTranslateFormat.Bilevel;
    public const int T4_IMAGE_TYPE_COLOUR_BILEVEL = (int)ImageTranslateFormat.ColourBilevel;
    public const int T4_IMAGE_TYPE_4COLOUR_BILEVEL = (int)ImageTranslateFormat.FourColourBilevel;
    public const int T4_IMAGE_TYPE_GRAY_8BIT = (int)ImageTranslateFormat.Gray8Bit;
    public const int T4_IMAGE_TYPE_GRAY_12BIT = (int)ImageTranslateFormat.Gray12Bit;
    public const int T4_IMAGE_TYPE_COLOUR_8BIT = (int)ImageTranslateFormat.Colour8Bit;
    public const int T4_IMAGE_TYPE_4COLOUR_8BIT = (int)ImageTranslateFormat.FourColour8Bit;
    public const int T4_IMAGE_TYPE_COLOUR_12BIT = (int)ImageTranslateFormat.Colour12Bit;
    public const int T4_IMAGE_TYPE_4COLOUR_12BIT = (int)ImageTranslateFormat.FourColour12Bit;

    public static ImageTranslateState? image_translate_init(
        ImageTranslateState? state,
        int outputFormat,
        int outputWidth,
        int outputLength,
        int inputFormat,
        int inputWidth,
        int inputLength,
        t4_row_read_handler_t? rowReadHandler,
        object? rowReadUserData) {
        try {
            state ??= new ImageTranslateState();
            state.Configure(
                (ImageTranslateFormat)outputFormat,
                outputWidth,
                outputLength,
                (ImageTranslateFormat)inputFormat,
                inputWidth,
                inputLength,
                rowReadHandler,
                rowReadUserData);
            return state;
        } catch (ArgumentException) {
            return null;
        } catch (InvalidOperationException) {
            return null;
        } catch (OverflowException) {
            return null;
        }
    }

    public static int image_translate_row(ImageTranslateState? state, byte[]? buffer, int length) {
        if (state is null || buffer is null) {
            return 0;
        }
        return state.TranslateRow(buffer, length);
    }

    public static int image_translate_row(ImageTranslateState? state, Span<byte> buffer) =>
        state?.TranslateRow(buffer) ?? 0;

    public static int image_translate_get_output_width(ImageTranslateState? state) =>
        state?.OutputWidth ?? 0;

    public static int image_translate_get_output_length(ImageTranslateState? state) =>
        state?.OutputLength ?? 0;

    public static int image_translate_set_row_read_handler(
        ImageTranslateState? state,
        t4_row_read_handler_t? rowReadHandler,
        object? rowReadUserData) =>
        state?.SetRowReadHandler(rowReadHandler, rowReadUserData) ?? -1;

    public static int image_translate_restart(ImageTranslateState? state, int inputLength) =>
        state?.Restart(inputLength) ?? -1;

    public static int image_translate_release(ImageTranslateState? state) =>
        state?.Release() ?? -1;

    public static int image_translate_free(ImageTranslateState? state) {
        if (state is null) {
            return -1;
        }
        state.Dispose();
        return 0;
    }
}

/// <summary>
/// Optional proportional fit and white-canvas padding extension.
/// This API is never invoked automatically by the T.4 transmitter.
/// </summary>
[Flags]
public enum ImageTranslateFitAxes {
    None = 0,
    Width = 1,
    Height = 2
}

/// <summary>
/// Optional streaming image preparation state. It uses the spanDSP-compatible
/// <see cref="ImageTranslateState"/> for conversion and proportional scaling,
/// then pads the translated image to a caller-selected canvas without distortion.
/// </summary>
public sealed class ImageTranslateFitPadState : IDisposable {
    private readonly ImageTranslateState _translator = new();
    private byte[] _translatedRow = Array.Empty<byte>();

    private int _contentWidth;
    private int _contentLength;
    private int _contentRowBytes;
    private int _canvasRowBytes;
    private int _outputRow;
    private bool _configured;
    private bool _disposed;

    public ImageTranslateFormat InputFormat { get; private set; }

    public int InputWidth { get; private set; }

    public int InputLength { get; private set; }

    public ImageTranslateFormat OutputFormat { get; private set; }

    /// <summary>Final canvas width, including white padding.</summary>
    public int OutputWidth { get; private set; }

    /// <summary>Final canvas length, including white padding.</summary>
    public int OutputLength { get; private set; }

    /// <summary>Width of the proportionally translated image inside the canvas.</summary>
    public int ContentWidth => _contentWidth;

    /// <summary>Length of the proportionally translated image inside the canvas.</summary>
    public int ContentLength => _contentLength;

    public ImageTranslateFitAxes FitAxes { get; private set; }

    public int OutputRow => _outputRow;

    public bool IsComplete => !_configured || _outputRow >= OutputLength;

    /// <summary>Number of bytes required for one final canvas row.</summary>
    public int RequiredOutputRowBytes => _canvasRowBytes;

    /// <summary>
    /// Configures proportional scaling and white padding.
    /// </summary>
    /// <remarks>
    /// <para><c>fitWidth=1, fitHeight=0</c>: width controls the scale; height is derived.</para>
    /// <para><c>fitWidth=0, fitHeight=1</c>: height controls the scale; width is derived.</para>
    /// <para><c>fitWidth=1, fitHeight=1</c>: the image is fitted inside both dimensions.</para>
    /// <para><c>fitWidth=0, fitHeight=0</c>: no scaling; only white padding is applied.</para>
    /// The source is never stretched independently by axis and is never cropped.
    /// </remarks>
    public void Configure(
        ImageTranslateFormat outputFormat,
        int targetWidth,
        int targetLength,
        int fitWidth,
        int fitHeight,
        ImageTranslateFormat inputFormat,
        int inputWidth,
        int inputLength,
        t4_row_read_handler_t? rowReadHandler,
        object? rowReadUserData = null) {
        ThrowIfDisposed();
        ValidateFormat(inputFormat, nameof(inputFormat));
        ValidateFormat(outputFormat, nameof(outputFormat));
        if (targetWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetWidth));
        if (targetLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetLength));
        if (fitWidth is not 0 and not 1)
            throw new ArgumentOutOfRangeException(nameof(fitWidth));
        if (fitHeight is not 0 and not 1)
            throw new ArgumentOutOfRangeException(nameof(fitHeight));
        if (inputWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(inputWidth));
        if (inputLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(inputLength));

        InputFormat = inputFormat;
        InputWidth = inputWidth;
        InputLength = inputLength;
        OutputFormat = outputFormat;
        OutputWidth = targetWidth;
        OutputLength = targetLength;
        FitAxes = (fitWidth != 0 ? ImageTranslateFitAxes.Width : ImageTranslateFitAxes.None)
                  | (fitHeight != 0 ? ImageTranslateFitAxes.Height : ImageTranslateFitAxes.None);

        CalculateContentSize(
            inputWidth,
            inputLength,
            targetWidth,
            targetLength,
            FitAxes,
            out _contentWidth,
            out _contentLength);

        if (_contentWidth <= 0 || _contentLength <= 0 ||
            _contentWidth > OutputWidth || _contentLength > OutputLength) {
            throw new InvalidOperationException("The proportionally scaled image does not fit inside the target canvas.");
        }

        _translator.Configure(
            outputFormat,
            _contentWidth,
            _contentLength,
            inputFormat,
            inputWidth,
            inputLength,
            rowReadHandler,
            rowReadUserData);

        _contentRowBytes = RowBytes(outputFormat, _contentWidth);
        _canvasRowBytes = RowBytes(outputFormat, OutputWidth);
        _translatedRow = new byte[_contentRowBytes];
        _outputRow = 0;
        _configured = true;
    }

    /// <summary>
    /// Produces the next row of the final target canvas. Returns the number of
    /// bytes written, zero at end of image, or -1 if the destination is too small.
    /// </summary>
    public int TranslateRow(Span<byte> destination) {
        ThrowIfDisposed();
        if (!_configured || _outputRow >= OutputLength)
            return 0;
        if (destination.Length < _canvasRowBytes)
            return -1;

        Span<byte> output = destination[.._canvasRowBytes];
        FillWhite(output, OutputFormat);

        if (_outputRow < _contentLength) {
            int translated = _translator.TranslateRow(_translatedRow);
            if (translated != _contentRowBytes) {
                _outputRow = OutputLength;
                return 0;
            }
            CopyContentRow(_translatedRow, output, OutputFormat, _contentWidth);
        }

        _outputRow++;
        return _canvasRowBytes;
    }

    public int TranslateRow(byte[] destination, int length) {
        ArgumentNullException.ThrowIfNull(destination);
        if (length < 0 || length > destination.Length)
            return -1;
        return TranslateRow(destination.AsSpan(0, length));
    }

    public int Release() {
        if (_disposed)
            return -1;

        _translator.Release();
        _translatedRow = Array.Empty<byte>();
        _contentWidth = 0;
        _contentLength = 0;
        _contentRowBytes = 0;
        _canvasRowBytes = 0;
        _outputRow = 0;
        _configured = false;
        return 0;
    }

    public void Dispose() {
        if (_disposed)
            return;
        Release();
        _translator.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static void CalculateContentSize(
        int inputWidth,
        int inputLength,
        int targetWidth,
        int targetLength,
        ImageTranslateFitAxes fitAxes,
        out int contentWidth,
        out int contentLength) {
        switch (fitAxes) {
            case ImageTranslateFitAxes.None:
                contentWidth = inputWidth;
                contentLength = inputLength;
                return;

            case ImageTranslateFitAxes.Width:
                contentWidth = targetWidth;
                contentLength = RoundRatio(inputLength, targetWidth, inputWidth);
                return;

            case ImageTranslateFitAxes.Height:
                contentLength = targetLength;
                contentWidth = RoundRatio(inputWidth, targetLength, inputLength);
                return;

            case ImageTranslateFitAxes.Width | ImageTranslateFitAxes.Height: {
                    int lengthWhenWidthControls = RoundRatio(inputLength, targetWidth, inputWidth);
                    if (lengthWhenWidthControls <= targetLength) {
                        contentWidth = targetWidth;
                        contentLength = lengthWhenWidthControls;
                    } else {
                        contentLength = targetLength;
                        contentWidth = RoundRatio(inputWidth, targetLength, inputLength);
                    }
                    return;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(fitAxes));
        }
    }

    private static int RoundRatio(int value, int multiplier, int divisor) {
        long numerator = checked((long)value * multiplier);
        return checked((int)((numerator + divisor / 2L) / divisor));
    }

    private static int RowBytes(ImageTranslateFormat format, int width) => IsPackedBilevel(format)
        ? checked((width + 7) / 8)
        : checked(width * BytesPerPixel(format));

    private static void FillWhite(Span<byte> row, ImageTranslateFormat format) {
        switch (format) {
            case ImageTranslateFormat.Bilevel:
            case ImageTranslateFormat.ColourBilevel:
            case ImageTranslateFormat.FourColourBilevel:
            case ImageTranslateFormat.FourColour8Bit:
            case ImageTranslateFormat.FourColour12Bit:
                row.Clear();
                break;

            case ImageTranslateFormat.Gray8Bit:
            case ImageTranslateFormat.Colour8Bit:
                row.Fill(byte.MaxValue);
                break;

            case ImageTranslateFormat.Gray12Bit:
            case ImageTranslateFormat.Colour12Bit:
                row.Fill(byte.MaxValue);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(format));
        }
    }

    private static void CopyContentRow(
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        ImageTranslateFormat format,
        int contentWidth) {
        if (!IsPackedBilevel(format)) {
            source.CopyTo(destination);
            return;
        }

        int fullBytes = contentWidth / 8;
        int remainingBits = contentWidth & 7;
        if (fullBytes > 0)
            source[..fullBytes].CopyTo(destination);
        if (remainingBits != 0) {
            byte mask = (byte)(0xFF << (8 - remainingBits));
            destination[fullBytes] = (byte)(source[fullBytes] & mask);
        }
    }

    private static bool IsPackedBilevel(ImageTranslateFormat format) => format is
        ImageTranslateFormat.Bilevel or
        ImageTranslateFormat.ColourBilevel or
        ImageTranslateFormat.FourColourBilevel;

    private static int BytesPerPixel(ImageTranslateFormat format) => format switch {
        ImageTranslateFormat.Gray8Bit => 1,
        ImageTranslateFormat.Gray12Bit => 2,
        ImageTranslateFormat.Colour8Bit => 3,
        ImageTranslateFormat.FourColour8Bit => 4,
        ImageTranslateFormat.Colour12Bit => 6,
        ImageTranslateFormat.FourColour12Bit => 8,
        ImageTranslateFormat.Bilevel or
        ImageTranslateFormat.ColourBilevel or
        ImageTranslateFormat.FourColourBilevel => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    private static void ValidateFormat(ImageTranslateFormat format, string parameterName) {
        if ((int)format < (int)ImageTranslateFormat.Bilevel ||
            (int)format > (int)ImageTranslateFormat.FourColour12Bit) {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}

/// <summary>
/// Public C-style entry points for optional proportional fit and white padding.
/// These functions are never called automatically by the fax engine.
/// </summary>
public static class ImageTranslateFitPadApi {
    public const int IMAGE_TRANSLATE_FIT_NONE = 0;
    public const int IMAGE_TRANSLATE_FIT_WIDTH = 1;
    public const int IMAGE_TRANSLATE_FIT_HEIGHT = 2;
    public const int IMAGE_TRANSLATE_FIT_BOTH = 3;

    public static ImageTranslateFitPadState? image_translate_fit_and_pad_init(
        ImageTranslateFitPadState? state,
        int outputFormat,
        int targetWidth,
        int targetLength,
        int fitWidth,
        int fitHeight,
        int inputFormat,
        int inputWidth,
        int inputLength,
        t4_row_read_handler_t? rowReadHandler,
        object? rowReadUserData) {
        try {
            state ??= new ImageTranslateFitPadState();
            state.Configure(
                (ImageTranslateFormat)outputFormat,
                targetWidth,
                targetLength,
                fitWidth,
                fitHeight,
                (ImageTranslateFormat)inputFormat,
                inputWidth,
                inputLength,
                rowReadHandler,
                rowReadUserData);
            return state;
        } catch (ArgumentException) {
            return null;
        } catch (InvalidOperationException) {
            return null;
        } catch (OverflowException) {
            return null;
        }
    }

    public static int image_translate_fit_and_pad_row(
        ImageTranslateFitPadState? state,
        byte[]? buffer,
        int length) {
        if (state is null || buffer is null)
            return 0;
        return state.TranslateRow(buffer, length);
    }

    public static int image_translate_fit_and_pad_row(
        ImageTranslateFitPadState? state,
        Span<byte> buffer) => state?.TranslateRow(buffer) ?? 0;

    public static int image_translate_fit_and_pad_get_output_width(
        ImageTranslateFitPadState? state) => state?.OutputWidth ?? 0;

    public static int image_translate_fit_and_pad_get_output_length(
        ImageTranslateFitPadState? state) => state?.OutputLength ?? 0;

    public static int image_translate_fit_and_pad_get_content_width(
        ImageTranslateFitPadState? state) => state?.ContentWidth ?? 0;

    public static int image_translate_fit_and_pad_get_content_length(
        ImageTranslateFitPadState? state) => state?.ContentLength ?? 0;

    public static int image_translate_fit_and_pad_release(
        ImageTranslateFitPadState? state) => state?.Release() ?? -1;

    public static int image_translate_fit_and_pad_free(
        ImageTranslateFitPadState? state) {
        if (state is null)
            return -1;
        state.Dispose();
        return 0;
    }
}
