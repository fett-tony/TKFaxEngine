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

/// <summary>
/// Defines the available <c>T85Options</c> values.
/// </summary>
[Flags]
public enum T85Options : byte {
    /// <summary>
    /// Represents the <c>None</c> value.
    /// </summary>
    None = 0,
    /// <summary>
    /// Represents the <c>TypicalPredictionBottom</c> value.
    /// </summary>
    TypicalPredictionBottom = 0x08,
    /// <summary>
    /// Represents the <c>VariableLength</c> value.
    /// </summary>
    VariableLength = 0x20,
    /// <summary>
    /// Represents the <c>LowestResolutionLayerTwoRows</c> value.
    /// </summary>
    LowestResolutionLayerTwoRows = 0x40
}

/// <summary>
/// Defines the available <c>T82Marker</c> values.
/// </summary>
public enum T82Marker : byte {
    /// <summary>
    /// Represents the <c>Stuff</c> value.
    /// </summary>
    Stuff = 0x00,
    /// <summary>
    /// Represents the <c>Reserve</c> value.
    /// </summary>
    Reserve = 0x01,
    /// <summary>
    /// Represents the <c>StripeDataNormal</c> value.
    /// </summary>
    StripeDataNormal = 0x02,
    /// <summary>
    /// Represents the <c>StripeDataReset</c> value.
    /// </summary>
    StripeDataReset = 0x03,
    /// <summary>
    /// Represents the <c>Abort</c> value.
    /// </summary>
    Abort = 0x04,
    /// <summary>
    /// Represents the <c>NewLength</c> value.
    /// </summary>
    NewLength = 0x05,
    /// <summary>
    /// Represents the <c>AdaptiveTemplateMove</c> value.
    /// </summary>
    AdaptiveTemplateMove = 0x06,
    /// <summary>
    /// Represents the <c>Comment</c> value.
    /// </summary>
    Comment = 0x07,
    /// <summary>
    /// Represents the <c>Escape</c> value.
    /// </summary>
    Escape = 0xFF
}

/// <summary>
/// Defines the available <c>T85DecodeStatus</c> values.
/// </summary>
public enum T85DecodeStatus {
    /// <summary>
    /// Represents the <c>MoreData</c> value.
    /// </summary>
    MoreData = 0,
    /// <summary>
    /// Represents the <c>Ok</c> value.
    /// </summary>
    Ok = -1,
    /// <summary>
    /// Represents the <c>Interrupt</c> value.
    /// </summary>
    Interrupt = -2,
    /// <summary>
    /// Represents the <c>Aborted</c> value.
    /// </summary>
    Aborted = -3,
    /// <summary>
    /// Represents the <c>NoMemory</c> value.
    /// </summary>
    NoMemory = -4,
    /// <summary>
    /// Represents the <c>InvalidData</c> value.
    /// </summary>
    InvalidData = -5
}

/// <summary>
/// Defines the available <c>T85SignalStatus</c> values.
/// </summary>
public enum T85SignalStatus {
    /// <summary>
    /// Represents the <c>CarrierDown</c> value.
    /// </summary>
    CarrierDown = -1,
    /// <summary>
    /// Represents the <c>CarrierUp</c> value.
    /// </summary>
    CarrierUp = -2,
    /// <summary>
    /// Represents the <c>TrainingInProgress</c> value.
    /// </summary>
    TrainingInProgress = -3,
    /// <summary>
    /// Represents the <c>TrainingSucceeded</c> value.
    /// </summary>
    TrainingSucceeded = -4,
    /// <summary>
    /// Represents the <c>TrainingFailed</c> value.
    /// </summary>
    TrainingFailed = -5,
    /// <summary>
    /// Represents the <c>FramingOk</c> value.
    /// </summary>
    FramingOk = -6,
    /// <summary>
    /// Represents the <c>EndOfData</c> value.
    /// </summary>
    EndOfData = -7
}

/// <summary>
/// Represents the <c>T85Constants</c> type.
/// </summary>
public static class T85Constants {
    /// <summary>
    /// Defines the <c>MaximumAdaptiveTemplateMoves</c> constant.
    /// </summary>
    public const int MaximumAdaptiveTemplateMoves = 1;
    /// <summary>
    /// Defines the <c>TypicalPredictionTwoRowContext</c> constant.
    /// </summary>
    public const int TypicalPredictionTwoRowContext = 0x195;
    /// <summary>
    /// Defines the <c>TypicalPredictionThreeRowContext</c> constant.
    /// </summary>
    public const int TypicalPredictionThreeRowContext = 0x0E5;
    /// <summary>
    /// Defines the <c>BasicStripeLength</c> constant.
    /// </summary>
    public const int BasicStripeLength = 128;
    /// <summary>
    /// Defines the <c>MaximumAdaptiveTemplateX</c> constant.
    /// </summary>
    public const int MaximumAdaptiveTemplateX = 127;
    /// <summary>
    /// Defines the <c>BinaryImageHeaderLength</c> constant.
    /// </summary>
    public const int BinaryImageHeaderLength = 20;

    /// <summary>
    /// Defines the <c>T85_TPBON</c> constant.
    /// </summary>
    public const int T85_TPBON = (int)T85Options.TypicalPredictionBottom;
    /// <summary>
    /// Defines the <c>T85_VLENGTH</c> constant.
    /// </summary>
    public const int T85_VLENGTH = (int)T85Options.VariableLength;
    /// <summary>
    /// Defines the <c>T85_LRLTWO</c> constant.
    /// </summary>
    public const int T85_LRLTWO = (int)T85Options.LowestResolutionLayerTwoRows;
    /// <summary>
    /// Defines the <c>T85_ATMOVES_MAX</c> constant.
    /// </summary>
    public const int T85_ATMOVES_MAX = MaximumAdaptiveTemplateMoves;
    /// <summary>
    /// Defines the <c>TPB2CX</c> constant.
    /// </summary>
    public const int TPB2CX = TypicalPredictionTwoRowContext;
    /// <summary>
    /// Defines the <c>TPB3CX</c> constant.
    /// </summary>
    public const int TPB3CX = TypicalPredictionThreeRowContext;

    /// <summary>
    /// Defines the <c>T82_STUFF</c> constant.
    /// </summary>
    public const int T82_STUFF = (int)T82Marker.Stuff;
    /// <summary>
    /// Defines the <c>T82_RESERVE</c> constant.
    /// </summary>
    public const int T82_RESERVE = (int)T82Marker.Reserve;
    /// <summary>
    /// Defines the <c>T82_SDNORM</c> constant.
    /// </summary>
    public const int T82_SDNORM = (int)T82Marker.StripeDataNormal;
    /// <summary>
    /// Defines the <c>T82_SDRST</c> constant.
    /// </summary>
    public const int T82_SDRST = (int)T82Marker.StripeDataReset;
    /// <summary>
    /// Defines the <c>T82_ABORT</c> constant.
    /// </summary>
    public const int T82_ABORT = (int)T82Marker.Abort;
    /// <summary>
    /// Defines the <c>T82_NEWLEN</c> constant.
    /// </summary>
    public const int T82_NEWLEN = (int)T82Marker.NewLength;
    /// <summary>
    /// Defines the <c>T82_ATMOVE</c> constant.
    /// </summary>
    public const int T82_ATMOVE = (int)T82Marker.AdaptiveTemplateMove;
    /// <summary>
    /// Defines the <c>T82_COMMENT</c> constant.
    /// </summary>
    public const int T82_COMMENT = (int)T82Marker.Comment;
    /// <summary>
    /// Defines the <c>T82_ESC</c> constant.
    /// </summary>
    public const int T82_ESC = (int)T82Marker.Escape;

    /// <summary>
    /// Defines the <c>T4_DECODE_MORE_DATA</c> constant.
    /// </summary>
    public const int T4_DECODE_MORE_DATA = (int)T85DecodeStatus.MoreData;
    /// <summary>
    /// Defines the <c>T4_DECODE_OK</c> constant.
    /// </summary>
    public const int T4_DECODE_OK = (int)T85DecodeStatus.Ok;
    /// <summary>
    /// Defines the <c>T4_DECODE_INTERRUPT</c> constant.
    /// </summary>
    public const int T4_DECODE_INTERRUPT = (int)T85DecodeStatus.Interrupt;
    /// <summary>
    /// Defines the <c>T4_DECODE_ABORTED</c> constant.
    /// </summary>
    public const int T4_DECODE_ABORTED = (int)T85DecodeStatus.Aborted;
    /// <summary>
    /// Defines the <c>T4_DECODE_NOMEM</c> constant.
    /// </summary>
    public const int T4_DECODE_NOMEM = (int)T85DecodeStatus.NoMemory;
    /// <summary>
    /// Defines the <c>T4_DECODE_INVALID_DATA</c> constant.
    /// </summary>
    public const int T4_DECODE_INVALID_DATA = (int)T85DecodeStatus.InvalidData;
}

/// <summary>
/// Represents the <c>T85RowReadDelegate</c> callback.
/// </summary>
/// <param name="userData">The user data value.</param>
/// <param name="destination">The destination value.</param>
/// <returns>The result of the operation.</returns>
public delegate int T85RowReadDelegate(
    object? userData,
    Span<byte> destination);

/// <summary>
/// Represents the <c>T85RowWriteDelegate</c> callback.
/// </summary>
/// <param name="userData">The user data value.</param>
/// <param name="data">The data value.</param>
/// <returns>The result of the operation.</returns>
public delegate int T85RowWriteDelegate(
    object? userData,
    ReadOnlySpan<byte> data);

/// <summary>
/// Represents the <c>T85Log</c> type.
/// </summary>
public sealed class T85Log {
    /// <summary>
    /// Gets or sets the <c>Protocol</c> value.
    /// </summary>
    public string Protocol { get; set; } = "T.85";
    /// <summary>
    /// Gets or sets the <c>FlowSink</c> value.
    /// </summary>
    public Action<string>? FlowSink { get; set; }
    /// <summary>
    /// Gets or sets the <c>WarningSink</c> value.
    /// </summary>
    public Action<string>? WarningSink { get; set; }

    /// <summary>
    /// Performs the <c>Flow</c> operation.
    /// </summary>
    /// <param name="message">The message value.</param>
    public void Flow(string message) => FlowSink?.Invoke(message);
    /// <summary>
    /// Performs the <c>Warning</c> operation.
    /// </summary>
    /// <param name="message">The message value.</param>
    public void Warning(string message) => WarningSink?.Invoke(message);
}

internal enum T85NewLengthState {
    None = 0,
    Pending = 1,
    Handled = 2
}

/// <summary>
/// Represents the <c>T85EncodeState</c> type.
/// </summary>
public sealed class T85EncodeState : IDisposable {
    private bool _disposed;

    internal T85EncodeState() {
        ArithmeticEncoder = new T81T82ArithmeticEncoder(PutEncodedByte);
    }

    /// <summary>
    /// Gets or sets the <c>RowReadHandler</c> value.
    /// </summary>
    public T85RowReadDelegate? RowReadHandler { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>RowReadUserData</c> value.
    /// </summary>
    public object? RowReadUserData { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>BitPlanes</c> value.
    /// </summary>
    public byte BitPlanes { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>CurrentBitPlane</c> value.
    /// </summary>
    public byte CurrentBitPlane { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>ImageWidth</c> value.
    /// </summary>
    public uint ImageWidth { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>ImageLength</c> value.
    /// </summary>
    public uint ImageLength { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>RowsPerStripe</c> value.
    /// </summary>
    public uint RowsPerStripe { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>MaximumAdaptiveTemplateX</c> value.
    /// </summary>
    public int MaximumAdaptiveTemplateX { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>Options</c> value.
    /// </summary>
    public T85Options Options { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>CurrentRow</c> value.
    /// </summary>
    public uint CurrentRow { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>CurrentStripeRow</c> value.
    /// </summary>
    public uint CurrentStripeRow { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>CompressedImageSizeBytes</c> value.
    /// </summary>
    public int CompressedImageSizeBytes { get; internal set; }
    /// <summary>
    /// Gets the <c>Logging</c> value.
    /// </summary>
    public T85Log Logging { get; } = new();
    /// <summary>
    /// Gets the <c>IsDisposed</c> value.
    /// </summary>
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

    /// <summary>
    /// Performs the <c>Dispose</c> operation.
    /// </summary>
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

/// <summary>
/// Represents the <c>T85Encode</c> type.
/// </summary>
public static class T85Encode {
    /// <summary>
    /// Performs the <c>Initialize</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="imageWidth">The image width value.</param>
    /// <param name="imageLength">The image length value.</param>
    /// <param name="rowReadHandler">The row read handler value.</param>
    /// <param name="userData">The user data value.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Performs the <c>Restart</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="imageWidth">The image width value.</param>
    /// <param name="imageLength">The image length value.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Performs the <c>SetOptions</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="rowsPerStripe">The rows per stripe value.</param>
    /// <param name="maximumAdaptiveTemplateX">The maximum adaptive template x value.</param>
    /// <param name="options">The options value.</param>
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

    /// <summary>
    /// Performs the <c>SetImageWidth</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="imageWidth">The image width value.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Performs the <c>SetImageLength</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="imageLength">The image length value.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Performs the <c>Abort</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    public static void Abort(T85EncodeState state) {
        ValidateState(state);
        OutputEscapeCode(state, T82Marker.Abort);
        state.CurrentRow = state.ImageLength;
    }

    /// <summary>
    /// Performs the <c>Comment</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="comment">The comment value.</param>
    public static void Comment(
        T85EncodeState state,
        ReadOnlySpan<byte> comment) {
        ValidateState(state);
        state.PendingComment = comment.ToArray();
    }

    /// <summary>
    /// Performs the <c>ImageComplete</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int ImageComplete(T85EncodeState state) {
        ValidateState(state);
        return state.CurrentRow >= state.ImageLength
            ? (int)T85SignalStatus.EndOfData
            : 0;
    }

    /// <summary>
    /// Performs the <c>Get</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="destination">The destination value.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Performs the <c>Get</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="destination">The destination value.</param>
    /// <param name="maximumLength">The maximum length value.</param>
    /// <returns>The result of the operation.</returns>
    public static int Get(
        T85EncodeState state,
        byte[] destination,
        int maximumLength) {
        ArgumentNullException.ThrowIfNull(destination);

        if ((uint)maximumLength > (uint)destination.Length)
            throw new ArgumentOutOfRangeException(nameof(maximumLength));

        return Get(state, destination.AsSpan(0, maximumLength));
    }

    /// <summary>
    /// Performs the <c>SetRowReadHandler</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="handler">The handler value.</param>
    /// <param name="userData">The user data value.</param>
    /// <returns>The result of the operation.</returns>
    public static int SetRowReadHandler(
        T85EncodeState state,
        T85RowReadDelegate? handler,
        object? userData) {
        ValidateState(state);
        state.RowReadHandler = handler;
        state.RowReadUserData = userData;
        return 0;
    }

    /// <summary>
    /// Performs the <c>GetImageWidth</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static uint GetImageWidth(T85EncodeState state) {
        ValidateState(state);
        return state.ImageWidth;
    }

    /// <summary>
    /// Performs the <c>GetImageLength</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static uint GetImageLength(T85EncodeState state) {
        ValidateState(state);
        return state.ImageLength;
    }

    /// <summary>
    /// Performs the <c>GetCompressedImageSize</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int GetCompressedImageSize(T85EncodeState state) {
        ValidateState(state);
        return checked(state.CompressedImageSizeBytes * 8);
    }

    /// <summary>
    /// Performs the <c>GetLoggingState</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static T85Log GetLoggingState(T85EncodeState state) {
        ValidateState(state);
        return state.Logging;
    }

    /// <summary>
    /// Performs the <c>Release</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int Release(T85EncodeState state) {
        ArgumentNullException.ThrowIfNull(state);

        state.PendingComment = null;
        state.BitStream.Clear();
        state.BitStreamOutputPointer = 0;

        for (int row = 0; row < state.PreviousRows.Length; row++)
            state.PreviousRows[row] = [];

        return 0;
    }

    /// <summary>
    /// Performs the <c>Free</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
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

/// <summary>
/// Represents the <c>T85Api</c> type.
/// </summary>
public static partial class T85Api {
    /// <summary>
    /// Defines the <c>T85_TPBON</c> constant.
    /// </summary>
    public const int T85_TPBON = T85Constants.T85_TPBON;
    /// <summary>
    /// Defines the <c>T85_VLENGTH</c> constant.
    /// </summary>
    public const int T85_VLENGTH = T85Constants.T85_VLENGTH;
    /// <summary>
    /// Defines the <c>T85_LRLTWO</c> constant.
    /// </summary>
    public const int T85_LRLTWO = T85Constants.T85_LRLTWO;

    /// <summary>
    /// Performs the <c>t85_encode_init</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="imageWidth">The image width value.</param>
    /// <param name="imageLength">The image length value.</param>
    /// <param name="handler">The handler value.</param>
    /// <param name="userData">The user data value.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Performs the <c>t85_encode_restart</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="imageWidth">The image width value.</param>
    /// <param name="imageLength">The image length value.</param>
    /// <returns>The result of the operation.</returns>
    public static int t85_encode_restart(
        T85EncodeState state,
        uint imageWidth,
        uint imageLength) =>
        T85Encode.Restart(state, imageWidth, imageLength);

    /// <summary>
    /// Performs the <c>t85_encode_get</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="destination">The destination value.</param>
    /// <returns>The result of the operation.</returns>
    public static int t85_encode_get(
        T85EncodeState state,
        Span<byte> destination) =>
        T85Encode.Get(state, destination);

    /// <summary>
    /// Performs the <c>t85_encode_get</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="destination">The destination value.</param>
    /// <param name="maximumLength">The maximum length value.</param>
    /// <returns>The result of the operation.</returns>
    public static int t85_encode_get(
        T85EncodeState state,
        byte[] destination,
        int maximumLength) =>
        T85Encode.Get(state, destination, maximumLength);

    /// <summary>
    /// Performs the <c>t85_encode_image_complete</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int t85_encode_image_complete(T85EncodeState state) =>
        T85Encode.ImageComplete(state);

    /// <summary>
    /// Performs the <c>t85_encode_set_row_read_handler</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="handler">The handler value.</param>
    /// <param name="userData">The user data value.</param>
    /// <returns>The result of the operation.</returns>
    public static int t85_encode_set_row_read_handler(
        T85EncodeState state,
        T85RowReadDelegate? handler,
        object? userData) =>
        T85Encode.SetRowReadHandler(state, handler, userData);

    /// <summary>
    /// Performs the <c>t85_encode_get_logging_state</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static T85Log t85_encode_get_logging_state(T85EncodeState state) =>
        T85Encode.GetLoggingState(state);

    /// <summary>
    /// Performs the <c>t85_encode_set_options</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="l0">The l0 value.</param>
    /// <param name="mx">The mx value.</param>
    /// <param name="options">The options value.</param>
    public static void t85_encode_set_options(
        T85EncodeState state,
        uint l0,
        int mx,
        int options) =>
        T85Encode.SetOptions(state, l0, mx, options);

    /// <summary>
    /// Performs the <c>t85_encode_comment</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="comment">The comment value.</param>
    public static void t85_encode_comment(
        T85EncodeState state,
        ReadOnlySpan<byte> comment) =>
        T85Encode.Comment(state, comment);

    /// <summary>
    /// Performs the <c>t85_encode_set_image_width</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="imageWidth">The image width value.</param>
    /// <returns>The result of the operation.</returns>
    public static int t85_encode_set_image_width(
        T85EncodeState state,
        uint imageWidth) =>
        T85Encode.SetImageWidth(state, imageWidth);

    /// <summary>
    /// Performs the <c>t85_encode_set_image_length</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="imageLength">The image length value.</param>
    /// <returns>The result of the operation.</returns>
    public static int t85_encode_set_image_length(
        T85EncodeState state,
        uint imageLength) =>
        T85Encode.SetImageLength(state, imageLength);

    /// <summary>
    /// Performs the <c>t85_encode_get_image_width</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static uint t85_encode_get_image_width(T85EncodeState state) =>
        T85Encode.GetImageWidth(state);

    /// <summary>
    /// Performs the <c>t85_encode_get_image_length</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static uint t85_encode_get_image_length(T85EncodeState state) =>
        T85Encode.GetImageLength(state);

    /// <summary>
    /// Performs the <c>t85_encode_get_compressed_image_size</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int t85_encode_get_compressed_image_size(T85EncodeState state) =>
        T85Encode.GetCompressedImageSize(state);

    /// <summary>
    /// Performs the <c>t85_encode_abort</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    public static void t85_encode_abort(T85EncodeState state) =>
        T85Encode.Abort(state);

    /// <summary>
    /// Performs the <c>t85_encode_release</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int t85_encode_release(T85EncodeState state) =>
        T85Encode.Release(state);

    /// <summary>
    /// Performs the <c>t85_encode_free</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int t85_encode_free(T85EncodeState? state) =>
        T85Encode.Free(state);
}
