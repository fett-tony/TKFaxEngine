/*
 * TKFaxEngine - managed C# port
 *
 * t85_encode.cs
 *
 * Combined managed encoder port of:
 *   t85_encode.c
 *   t85.h / private/t85.h
 *
 * Original implementation written by Steve Underwood.
 * Copyright (C) 2008-2010 Steve Underwood.
 * Licensed under the GNU Lesser General Public License version 2.1.
 */

#nullable enable

using System.Buffers.Binary;

namespace TKFaxEngine.FaxImage;

[Flags]
public enum T85Options : byte {
    None = 0,
    TypicalPredictionBottom = 0x08,
    VariableLength = 0x20,
    LowestResolutionLayerTwoRows = 0x40
}

public enum T82Marker : byte {
    Stuff = 0x00,
    Reserve = 0x01,
    StripeDataNormal = 0x02,
    StripeDataReset = 0x03,
    Abort = 0x04,
    NewLength = 0x05,
    AdaptiveTemplateMove = 0x06,
    Comment = 0x07,
    Escape = 0xFF
}

public enum T85DecodeStatus {
    MoreData = 0,
    Ok = -1,
    Interrupt = -2,
    Aborted = -3,
    NoMemory = -4,
    InvalidData = -5
}

public enum T85SignalStatus {
    CarrierDown = -1,
    CarrierUp = -2,
    TrainingInProgress = -3,
    TrainingSucceeded = -4,
    TrainingFailed = -5,
    FramingOk = -6,
    EndOfData = -7
}

public static class T85Constants {
    public const int MaximumAdaptiveTemplateMoves = 1;
    public const int TypicalPredictionTwoRowContext = 0x195;
    public const int TypicalPredictionThreeRowContext = 0x0E5;
    public const int BasicStripeLength = 128;
    public const int MaximumAdaptiveTemplateX = 127;
    public const int BinaryImageHeaderLength = 20;

    public const int T85_TPBON = (int)T85Options.TypicalPredictionBottom;
    public const int T85_VLENGTH = (int)T85Options.VariableLength;
    public const int T85_LRLTWO = (int)T85Options.LowestResolutionLayerTwoRows;
    public const int T85_ATMOVES_MAX = MaximumAdaptiveTemplateMoves;
    public const int TPB2CX = TypicalPredictionTwoRowContext;
    public const int TPB3CX = TypicalPredictionThreeRowContext;

    public const int T82_STUFF = (int)T82Marker.Stuff;
    public const int T82_RESERVE = (int)T82Marker.Reserve;
    public const int T82_SDNORM = (int)T82Marker.StripeDataNormal;
    public const int T82_SDRST = (int)T82Marker.StripeDataReset;
    public const int T82_ABORT = (int)T82Marker.Abort;
    public const int T82_NEWLEN = (int)T82Marker.NewLength;
    public const int T82_ATMOVE = (int)T82Marker.AdaptiveTemplateMove;
    public const int T82_COMMENT = (int)T82Marker.Comment;
    public const int T82_ESC = (int)T82Marker.Escape;

    public const int T4_DECODE_MORE_DATA = (int)T85DecodeStatus.MoreData;
    public const int T4_DECODE_OK = (int)T85DecodeStatus.Ok;
    public const int T4_DECODE_INTERRUPT = (int)T85DecodeStatus.Interrupt;
    public const int T4_DECODE_ABORTED = (int)T85DecodeStatus.Aborted;
    public const int T4_DECODE_NOMEM = (int)T85DecodeStatus.NoMemory;
    public const int T4_DECODE_INVALID_DATA = (int)T85DecodeStatus.InvalidData;
}

public delegate int T85RowReadDelegate(
    object? userData,
    Span<byte> destination);

public delegate int T85RowWriteDelegate(
    object? userData,
    ReadOnlySpan<byte> data);

public sealed class T85Log {
    public string Protocol { get; set; } = "T.85";
    public Action<string>? FlowSink { get; set; }
    public Action<string>? WarningSink { get; set; }

    public void Flow(string message) => FlowSink?.Invoke(message);
    public void Warning(string message) => WarningSink?.Invoke(message);
}

internal enum T85NewLengthState {
    None = 0,
    Pending = 1,
    Handled = 2
}

public sealed class T85EncodeState : IDisposable {
    private bool _disposed;

    internal T85EncodeState() {
        ArithmeticEncoder = new T81T82ArithmeticEncoder(PutEncodedByte);
    }

    public T85RowReadDelegate? RowReadHandler { get; internal set; }
    public object? RowReadUserData { get; internal set; }
    public byte BitPlanes { get; internal set; }
    public byte CurrentBitPlane { get; internal set; }
    public uint ImageWidth { get; internal set; }
    public uint ImageLength { get; internal set; }
    public uint RowsPerStripe { get; internal set; }
    public int MaximumAdaptiveTemplateX { get; internal set; }
    public T85Options Options { get; internal set; }
    public uint CurrentRow { get; internal set; }
    public uint CurrentStripeRow { get; internal set; }
    public int CompressedImageSizeBytes { get; internal set; }
    public T85Log Logging { get; } = new();
    public bool IsDisposed => _disposed;

    internal ReadOnlyMemory<byte>? PendingComment { get; set; }
    internal T85NewLengthState NewLengthState { get; set; }
    internal int AdaptiveTemplateX { get; set; }
    internal uint AdaptiveTemplateTotal { get; set; }
    internal uint[] AdaptiveTemplateCounts { get; } = new uint[128];
    internal int NewAdaptiveTemplateX { get; set; }
    internal bool PreviousLineTypical { get; set; }
    internal byte[][] PreviousRows { get; } = [[], [], []];
    internal List<byte> BitStream { get; } = new();
    internal int BitStreamOutputPointer { get; set; }
    internal bool FillWithWhite { get; set; }
    internal T81T82ArithmeticEncoder ArithmeticEncoder { get; }

    public void Dispose() {
        if (_disposed)
            return;

        T85Encode.Release(this);
        RowReadHandler = null;
        RowReadUserData = null;
        _disposed = true;
    }

    internal void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    internal void Revive() => _disposed = false;

    private void PutEncodedByte(byte value) {
        BitStream.Add(value);
        CompressedImageSizeBytes++;
    }
}

public static class T85Encode {
    public static T85EncodeState Initialize(
        T85EncodeState? state,
        uint imageWidth,
        uint imageLength,
        T85RowReadDelegate? rowReadHandler,
        object? userData) {
        state ??= new T85EncodeState();
        state.Revive();
        state.Logging.Protocol = "T.85";
        state.RowReadHandler = rowReadHandler;
        state.RowReadUserData = userData;
        state.RowsPerStripe = T85Constants.BasicStripeLength;
        state.MaximumAdaptiveTemplateX = T85Constants.MaximumAdaptiveTemplateX;
        state.Options =
            T85Options.TypicalPredictionBottom |
            T85Options.VariableLength;
        state.BitPlanes = 1;
        state.CurrentBitPlane = 0;
        if (Restart(state, imageWidth, imageLength) < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(imageWidth),
                "The image dimensions could not be initialized.");
        }

        return state;
    }

    public static int Restart(
        T85EncodeState state,
        uint imageWidth,
        uint imageLength) {
        ValidateState(state);
        state.CurrentRow = 0;
        state.CurrentStripeRow = 0;

        if (SetImageWidth(state, imageWidth) < 0)
            return -1;

        int bytesPerRow = checked((int)((state.ImageWidth + 7) >> 3));

        foreach (byte[] row in state.PreviousRows)
            Array.Clear(row);

        state.ImageLength = imageLength;
        state.PendingComment = null;
        state.CurrentRow = 0;
        state.CurrentStripeRow = 0;
        state.NewLengthState = T85NewLengthState.None;
        state.NewAdaptiveTemplateX = -1;
        state.AdaptiveTemplateX = 0;
        state.PreviousLineTypical = false;
        state.BitStream.Clear();
        state.BitStreamOutputPointer = 0;
        state.FillWithWhite = false;
        state.CompressedImageSizeBytes = 0;
        Array.Clear(state.AdaptiveTemplateCounts);
        state.AdaptiveTemplateTotal = 0;
        state.ArithmeticEncoder.Restart(false);
        _ = bytesPerRow;
        return 0;
    }

    public static void SetOptions(
        T85EncodeState state,
        uint rowsPerStripe,
        int maximumAdaptiveTemplateX,
        int options) {
        ValidateState(state);

        if (state.CurrentRow > 0)
            return;

        if (rowsPerStripe >= 1 && rowsPerStripe <= state.ImageLength)
            state.RowsPerStripe = rowsPerStripe;

        if (maximumAdaptiveTemplateX is >= 0 and <= 127)
            state.MaximumAdaptiveTemplateX = maximumAdaptiveTemplateX;

        if (options >= 0) {
            state.Options = (T85Options)(options &
                ((int)T85Options.TypicalPredictionBottom |
                 (int)T85Options.VariableLength |
                 (int)T85Options.LowestResolutionLayerTwoRows));
        }
    }

    public static int SetImageWidth(
        T85EncodeState state,
        uint imageWidth) {
        ValidateState(state);

        if (imageWidth == 0)
            return -1;

        int expectedBytesPerRow;

        try {
            expectedBytesPerRow = checked((int)((imageWidth + 7) >> 3));
        } catch (OverflowException) {
            return -1;
        }

        if (state.ImageWidth == imageWidth &&
            state.PreviousRows[0].Length == expectedBytesPerRow &&
            state.PreviousRows[1].Length == expectedBytesPerRow &&
            state.PreviousRows[2].Length == expectedBytesPerRow) {
            return 0;
        }

        if (state.CurrentRow > 0)
            return -1;

        int bytesPerRow = expectedBytesPerRow;

        state.ImageWidth = imageWidth;

        for (int row = 0; row < state.PreviousRows.Length; row++)
            state.PreviousRows[row] = new byte[bytesPerRow];

        return 0;
    }

    public static int SetImageLength(
        T85EncodeState state,
        uint imageLength) {
        ValidateState(state);

        if ((state.Options & T85Options.VariableLength) == 0 ||
            state.NewLengthState == T85NewLengthState.Handled ||
            imageLength >= state.ImageLength ||
            imageLength < 1) {
            return -1;
        }

        if (state.CurrentRow > 0) {
            if (imageLength < state.CurrentRow)
                imageLength = state.CurrentRow;

            if (state.ImageLength != imageLength)
                state.NewLengthState = T85NewLengthState.Pending;
        }

        state.ImageLength = imageLength;

        if (state.CurrentRow == state.ImageLength) {
            if (state.CurrentStripeRow > 0) {
                state.ArithmeticEncoder.Flush();
                OutputEscapeCode(state, T82Marker.StripeDataNormal);
                state.CurrentStripeRow = 0;
            }

            OutputNewLength(state);
        }

        return 0;
    }

    public static void Abort(T85EncodeState state) {
        ValidateState(state);
        OutputEscapeCode(state, T82Marker.Abort);
        state.CurrentRow = state.ImageLength;
    }

    public static void Comment(
        T85EncodeState state,
        ReadOnlySpan<byte> comment) {
        ValidateState(state);
        state.PendingComment = comment.ToArray();
    }

    public static int ImageComplete(T85EncodeState state) {
        ValidateState(state);
        return state.CurrentRow >= state.ImageLength
            ? (int)T85SignalStatus.EndOfData
            : 0;
    }

    public static int Get(
        T85EncodeState state,
        Span<byte> destination) {
        ValidateState(state);
        int written = 0;

        while (written < destination.Length) {
            if (state.BitStreamOutputPointer >= state.BitStream.Count) {
                if (GetNextRow(state) < 0)
                    return written;
            }

            int available =
                state.BitStream.Count -
                state.BitStreamOutputPointer;

            if (available <= 0) {
                if (state.CurrentRow >= state.ImageLength)
                    return written;

                continue;
            }

            int copy = Math.Min(
                available,
                destination.Length - written);

            for (int index = 0; index < copy; index++) {
                destination[written + index] =
                    state.BitStream[
                        state.BitStreamOutputPointer + index];
            }

            state.BitStreamOutputPointer += copy;
            written += copy;
        }

        return written;
    }

    public static int Get(
        T85EncodeState state,
        byte[] destination,
        int maximumLength) {
        ArgumentNullException.ThrowIfNull(destination);

        if ((uint)maximumLength > (uint)destination.Length)
            throw new ArgumentOutOfRangeException(nameof(maximumLength));

        return Get(state, destination.AsSpan(0, maximumLength));
    }

    public static int SetRowReadHandler(
        T85EncodeState state,
        T85RowReadDelegate? handler,
        object? userData) {
        ValidateState(state);
        state.RowReadHandler = handler;
        state.RowReadUserData = userData;
        return 0;
    }

    public static uint GetImageWidth(T85EncodeState state) {
        ValidateState(state);
        return state.ImageWidth;
    }

    public static uint GetImageLength(T85EncodeState state) {
        ValidateState(state);
        return state.ImageLength;
    }

    public static int GetCompressedImageSize(T85EncodeState state) {
        ValidateState(state);
        return checked(state.CompressedImageSizeBytes * 8);
    }

    public static T85Log GetLoggingState(T85EncodeState state) {
        ValidateState(state);
        return state.Logging;
    }

    public static int Release(T85EncodeState state) {
        ArgumentNullException.ThrowIfNull(state);

        state.PendingComment = null;
        state.BitStream.Clear();
        state.BitStreamOutputPointer = 0;

        for (int row = 0; row < state.PreviousRows.Length; row++)
            state.PreviousRows[row] = [];

        return 0;
    }

    public static int Free(T85EncodeState? state) {
        state?.Dispose();
        return 0;
    }

    private static int GetNextRow(T85EncodeState state) {
        if (state.CurrentRow >= state.ImageLength)
            return -1;

        state.BitStream.Clear();
        state.BitStreamOutputPointer = 0;

        int bytesPerRow = checked((int)((state.ImageWidth + 7) >> 3));

        byte[] recycled = state.PreviousRows[2];
        state.PreviousRows[2] = state.PreviousRows[1];
        state.PreviousRows[1] = state.PreviousRows[0];
        state.PreviousRows[0] = recycled;

        byte[] current = state.PreviousRows[0];

        if (state.FillWithWhite) {
            Array.Clear(current);
        } else {
            int rowResult = state.RowReadHandler?.Invoke(
                state.RowReadUserData,
                current) ?? 0;

            if (rowResult <= 0) {
                if (SetImageLength(state, 1) == 0)
                    return 0;

                state.FillWithWhite = true;
                Array.Clear(current);
            }
        }

        int trailingBits = (int)(state.ImageWidth & 7);

        if (trailingBits != 0) {
            current[bytesPerRow - 1] &=
                (byte)~((1 << (8 - trailingBits)) - 1);
        }

        if (state.CurrentBitPlane == 0 && state.CurrentRow == 0) {
            Span<byte> header = stackalloc byte[T85Constants.BinaryImageHeaderLength];
            GenerateBinaryImageHeader(state, header);
            PutStuff(state, header);
        }

        if (state.CurrentStripeRow == 0) {
            OutputNewLength(state);
            OutputComment(state);
            OutputAdaptiveTemplateMove(state);

            if (state.MaximumAdaptiveTemplateX == 0) {
                state.NewAdaptiveTemplateX = 0;
            } else {
                state.NewAdaptiveTemplateX = -1;
                state.AdaptiveTemplateTotal = 0;
                Array.Clear(state.AdaptiveTemplateCounts);
            }

            state.ArithmeticEncoder.Restart(true);
        }

        bool lineTypical = false;

        if ((state.Options & T85Options.TypicalPredictionBottom) != 0) {
            lineTypical = current.AsSpan().SequenceEqual(
                state.PreviousRows[1]);

            state.ArithmeticEncoder.Encode(
                (state.Options & T85Options.LowestResolutionLayerTwoRows) != 0
                    ? T85Constants.TypicalPredictionTwoRowContext
                    : T85Constants.TypicalPredictionThreeRowContext,
                lineTypical == state.PreviousLineTypical ? 1 : 0);

            state.PreviousLineTypical = lineTypical;
        }

        if (!lineTypical)
            EncodeRow(state, bytesPerRow);

        state.CurrentStripeRow++;
        state.CurrentRow++;

        if (state.CurrentStripeRow == state.RowsPerStripe ||
            state.CurrentRow == state.ImageLength) {
            state.ArithmeticEncoder.Flush();
            OutputEscapeCode(state, T82Marker.StripeDataNormal);
            state.CurrentStripeRow = 0;
            OutputNewLength(state);
        }

        AnalyseAdaptiveTemplate(state);
        return 0;
    }

    private static void EncodeRow(
        T85EncodeState state,
        int bytesPerRow) {
        byte[] row0 = state.PreviousRows[0];
        byte[] row1 = state.PreviousRows[1];
        byte[] row2 = state.PreviousRows[2];

        uint[] rowHistory = new uint[3];
        rowHistory[1] = (uint)row1[0] << 8;
        rowHistory[2] = (uint)row2[0] << 8;

        int byteIndex = 0;
        uint pixelIndex = 0;

        while (pixelIndex < state.ImageWidth) {
            rowHistory[0] |= row0[byteIndex];

            if (pixelIndex < (uint)((bytesPerRow - 1) * 8)) {
                rowHistory[1] |= row1[byteIndex + 1];
                rowHistory[2] |= row2[byteIndex + 1];
            }

            if ((state.Options & T85Options.LowestResolutionLayerTwoRows) != 0) {
                do {
                    rowHistory[0] <<= 1;
                    rowHistory[1] <<= 1;
                    rowHistory[2] <<= 1;

                    int context = (int)((rowHistory[0] >> 9) & 0x00F);

                    if (state.AdaptiveTemplateX != 0) {
                        context |= (int)((rowHistory[1] >> 10) & 0x3E0);

                        if (pixelIndex >= (uint)state.AdaptiveTemplateX) {
                            int offset =
                                ((int)pixelIndex - state.AdaptiveTemplateX) -
                                ((int)pixelIndex & ~7);

                            int sourceIndex = byteIndex + (offset >> 3);
                            context |=
                                ((row0[sourceIndex] >>
                                  (7 - (offset & 7))) & 1) << 4;
                        }
                    } else {
                        context |= (int)((rowHistory[1] >> 10) & 0x3F0);
                    }

                    int pixel = (int)((rowHistory[0] >> 8) & 1);
                    state.ArithmeticEncoder.Encode(context, pixel);
                    UpdateAdaptiveTemplateStatistics(
                        state,
                        row0,
                        rowHistory,
                        byteIndex,
                        pixelIndex,
                        pixel,
                        minimumTemplateX: 5);

                    pixelIndex++;
                }
                while ((pixelIndex & 7) != 0 &&
                       pixelIndex < state.ImageWidth);
            } else {
                do {
                    rowHistory[0] <<= 1;
                    rowHistory[1] <<= 1;
                    rowHistory[2] <<= 1;

                    int context =
                        (int)((rowHistory[2] >> 8) & 0x380) |
                        (int)((rowHistory[0] >> 9) & 0x003);

                    if (state.AdaptiveTemplateX != 0) {
                        context |= (int)((rowHistory[1] >> 12) & 0x078);

                        if (pixelIndex >= (uint)state.AdaptiveTemplateX) {
                            int offset =
                                ((int)pixelIndex - state.AdaptiveTemplateX) -
                                ((int)pixelIndex & ~7);

                            int sourceIndex = byteIndex + (offset >> 3);
                            context |=
                                ((row0[sourceIndex] >>
                                  (7 - (offset & 7))) & 1) << 2;
                        }
                    } else {
                        context |= (int)((rowHistory[1] >> 12) & 0x07C);
                    }

                    int pixel = (int)((rowHistory[0] >> 8) & 1);
                    state.ArithmeticEncoder.Encode(context, pixel);
                    UpdateAdaptiveTemplateStatistics(
                        state,
                        row0,
                        rowHistory,
                        byteIndex,
                        pixelIndex,
                        pixel,
                        minimumTemplateX: 3);

                    pixelIndex++;
                }
                while ((pixelIndex & 7) != 0 &&
                       pixelIndex < state.ImageWidth);
            }

            byteIndex++;
        }
    }

    private static void UpdateAdaptiveTemplateStatistics(
        T85EncodeState state,
        byte[] currentRow,
        uint[] rowHistory,
        int byteIndex,
        uint pixelIndex,
        int pixel,
        int minimumTemplateX) {
        if (state.NewAdaptiveTemplateX >= 0 ||
            pixelIndex < (uint)state.MaximumAdaptiveTemplateX ||
            state.ImageWidth <= 2 ||
            pixelIndex >= state.ImageWidth - 2) {
            return;
        }

        if (pixel == (int)((rowHistory[1] >> 14) & 1))
            state.AdaptiveTemplateCounts[0]++;

        int templateX = minimumTemplateX;

        for (;
             templateX <= state.MaximumAdaptiveTemplateX &&
             (uint)templateX <= pixelIndex;
             templateX++) {
            int offset =
                ((int)pixelIndex - templateX) -
                ((int)pixelIndex & ~7);

            int sourceIndex = byteIndex + (offset >> 3);
            int adaptivePixel =
                (currentRow[sourceIndex] >>
                 (7 - (offset & 7))) &
                1;

            if (adaptivePixel == pixel)
                state.AdaptiveTemplateCounts[templateX]++;
        }

        if (pixel == 0) {
            for (;
                 templateX <= state.MaximumAdaptiveTemplateX;
                 templateX++) {
                state.AdaptiveTemplateCounts[templateX]++;
            }
        }

        state.AdaptiveTemplateTotal++;
    }

    private static void AnalyseAdaptiveTemplate(T85EncodeState state) {
        if (state.NewAdaptiveTemplateX >= 0 ||
            state.AdaptiveTemplateTotal <= 2048) {
            return;
        }

        uint minimum = uint.MaxValue;
        uint maximum = 0;
        uint localMinimum;
        uint localMaximum;
        int maximumIndex = 0;
        int first =
            (state.Options & T85Options.LowestResolutionLayerTwoRows) != 0
                ? 5
                : 3;

        for (int index = first;
             index <= state.MaximumAdaptiveTemplateX;
             index++) {
            uint value = state.AdaptiveTemplateCounts[index];

            if (value > maximum)
                maximum = value;

            if (value < minimum)
                minimum = value;

            if (value > state.AdaptiveTemplateCounts[maximumIndex])
                maximumIndex = index;
        }

        localMinimum = Math.Min(state.AdaptiveTemplateCounts[0], minimum);
        localMaximum = Math.Max(state.AdaptiveTemplateCounts[0], maximum);

        uint total = state.AdaptiveTemplateTotal;
        uint current = state.AdaptiveTemplateCounts[state.AdaptiveTemplateX];

        bool move =
            (total - maximum) < (total >> 3) &&
            (maximum - current) > (total - maximum) &&
            (maximum - current) > (total >> 4) &&
            (maximum - (total - current)) > (total - maximum) &&
            (maximum - (total - current)) > (total >> 4) &&
            (maximum - minimum) > (total >> 2) &&
            (state.AdaptiveTemplateX != 0 ||
             (localMaximum - localMinimum) > (total >> 3));

        state.NewAdaptiveTemplateX = move
            ? maximumIndex
            : state.AdaptiveTemplateX;
    }

    private static void GenerateBinaryImageHeader(
        T85EncodeState state,
        Span<byte> header) {
        header.Clear();
        header[2] = state.BitPlanes;
        BinaryPrimitives.WriteUInt32BigEndian(header.Slice(4, 4), state.ImageWidth);
        BinaryPrimitives.WriteUInt32BigEndian(header.Slice(8, 4), state.ImageLength);
        BinaryPrimitives.WriteUInt32BigEndian(header.Slice(12, 4), state.RowsPerStripe);
        header[16] = unchecked((byte)state.MaximumAdaptiveTemplateX);
        header[19] = (byte)state.Options;
    }

    private static void OutputNewLength(T85EncodeState state) {
        if (state.NewLengthState != T85NewLengthState.Pending)
            return;

        Span<byte> marker = stackalloc byte[6];
        marker[0] = (byte)T82Marker.Escape;
        marker[1] = (byte)T82Marker.NewLength;
        BinaryPrimitives.WriteUInt32BigEndian(marker.Slice(2, 4), state.ImageLength);
        PutStuff(state, marker);

        if (state.CurrentRow == state.ImageLength)
            OutputEscapeCode(state, T82Marker.StripeDataNormal);

        state.NewLengthState = T85NewLengthState.Handled;
    }

    private static void OutputComment(T85EncodeState state) {
        if (state.PendingComment is not { } comment)
            return;

        Span<byte> marker = stackalloc byte[6];
        marker[0] = (byte)T82Marker.Escape;
        marker[1] = (byte)T82Marker.Comment;
        BinaryPrimitives.WriteUInt32BigEndian(
            marker.Slice(2, 4),
            checked((uint)comment.Length));

        PutStuff(state, marker);
        PutStuff(state, comment.Span);
        state.PendingComment = null;
    }

    private static void OutputAdaptiveTemplateMove(T85EncodeState state) {
        if (state.NewAdaptiveTemplateX < 0 ||
            state.NewAdaptiveTemplateX == state.AdaptiveTemplateX) {
            return;
        }

        state.AdaptiveTemplateX = state.NewAdaptiveTemplateX;

        Span<byte> marker = stackalloc byte[8];
        marker[0] = (byte)T82Marker.Escape;
        marker[1] = (byte)T82Marker.AdaptiveTemplateMove;
        BinaryPrimitives.WriteUInt32BigEndian(marker.Slice(2, 4), 0);
        marker[6] = unchecked((byte)state.AdaptiveTemplateX);
        marker[7] = 0;
        PutStuff(state, marker);
    }

    private static void OutputEscapeCode(
        T85EncodeState state,
        T82Marker marker) {
        Span<byte> value = stackalloc byte[2];
        value[0] = (byte)T82Marker.Escape;
        value[1] = (byte)marker;
        PutStuff(state, value);
    }

    private static void PutStuff(
        T85EncodeState state,
        ReadOnlySpan<byte> data) {
        foreach (byte value in data) {
            state.BitStream.Add(value);
            state.CompressedImageSizeBytes++;
        }
    }

    private static void ValidateState(T85EncodeState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
    }
}

public static partial class T85Api {
    public const int T85_TPBON = T85Constants.T85_TPBON;
    public const int T85_VLENGTH = T85Constants.T85_VLENGTH;
    public const int T85_LRLTWO = T85Constants.T85_LRLTWO;

    public static T85EncodeState t85_encode_init(
        T85EncodeState? state,
        uint imageWidth,
        uint imageLength,
        T85RowReadDelegate? handler,
        object? userData) {
        return T85Encode.Initialize(
            state,
            imageWidth,
            imageLength,
            handler,
            userData);
    }

    public static int t85_encode_restart(
        T85EncodeState state,
        uint imageWidth,
        uint imageLength) =>
        T85Encode.Restart(state, imageWidth, imageLength);

    public static int t85_encode_get(
        T85EncodeState state,
        Span<byte> destination) =>
        T85Encode.Get(state, destination);

    public static int t85_encode_get(
        T85EncodeState state,
        byte[] destination,
        int maximumLength) =>
        T85Encode.Get(state, destination, maximumLength);

    public static int t85_encode_image_complete(T85EncodeState state) =>
        T85Encode.ImageComplete(state);

    public static int t85_encode_set_row_read_handler(
        T85EncodeState state,
        T85RowReadDelegate? handler,
        object? userData) =>
        T85Encode.SetRowReadHandler(state, handler, userData);

    public static T85Log t85_encode_get_logging_state(T85EncodeState state) =>
        T85Encode.GetLoggingState(state);

    public static void t85_encode_set_options(
        T85EncodeState state,
        uint l0,
        int mx,
        int options) =>
        T85Encode.SetOptions(state, l0, mx, options);

    public static void t85_encode_comment(
        T85EncodeState state,
        ReadOnlySpan<byte> comment) =>
        T85Encode.Comment(state, comment);

    public static int t85_encode_set_image_width(
        T85EncodeState state,
        uint imageWidth) =>
        T85Encode.SetImageWidth(state, imageWidth);

    public static int t85_encode_set_image_length(
        T85EncodeState state,
        uint imageLength) =>
        T85Encode.SetImageLength(state, imageLength);

    public static uint t85_encode_get_image_width(T85EncodeState state) =>
        T85Encode.GetImageWidth(state);

    public static uint t85_encode_get_image_length(T85EncodeState state) =>
        T85Encode.GetImageLength(state);

    public static int t85_encode_get_compressed_image_size(T85EncodeState state) =>
        T85Encode.GetCompressedImageSize(state);

    public static void t85_encode_abort(T85EncodeState state) =>
        T85Encode.Abort(state);

    public static int t85_encode_release(T85EncodeState state) =>
        T85Encode.Release(state);

    public static int t85_encode_free(T85EncodeState? state) =>
        T85Encode.Free(state);
}
