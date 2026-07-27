/*
 * TKFaxEngine - managed C# port
 *
 * t85_decode.cs
 *
 * Combined managed decoder port of:
 *   t85_decode.c
 *   t85.h / private/t85.h
 *
 * Original implementation written by Steve Underwood.
 * Copyright (C) 2008-2010 Steve Underwood.
 * Licensed under the GNU Lesser General Public License version 2.1.
 */

#nullable enable

using System.Buffers.Binary;

namespace TKFaxEngine.FaxImage;

public sealed class T85DecodeState : IDisposable {
    private bool _disposed;

    internal T85DecodeState() {
        ArithmeticDecoder = new T81T82ArithmeticDecoder();
    }

    public t4_row_write_handler_t? RowWriteHandler { get; internal set; }
    public object? RowWriteUserData { get; internal set; }
    public T85RowWriteDelegate? CommentHandler { get; internal set; }
    public object? CommentUserData { get; internal set; }
    public uint MaximumCommentLength { get; internal set; }
    public byte MinimumBitPlanes { get; internal set; }
    public byte MaximumBitPlanes { get; internal set; }
    public uint MaximumImageWidth { get; internal set; }
    public uint MaximumImageLength { get; internal set; }
    public byte BitPlanes { get; internal set; }
    public byte CurrentBitPlane { get; internal set; }
    public uint ImageWidth { get; internal set; }
    public uint ImageLength { get; internal set; }
    public uint RowsPerStripe { get; internal set; }
    public int MaximumAdaptiveTemplateX { get; internal set; }
    public T85Options Options { get; internal set; }
    public uint CurrentColumn { get; internal set; }
    public uint CurrentRow { get; internal set; }
    public uint CurrentStripeRow { get; internal set; }
    public int CompressedImageSizeBytes { get; internal set; }
    public T85Log Logging { get; } = new();
    public bool IsDisposed => _disposed;

    internal int[] RowPointers { get; } = new int[3];
    internal byte[] RowBuffer { get; set; } = [];
    internal int BytesPerRow { get; set; }
    internal int AdaptiveTemplateX { get; set; }
    internal uint BinaryImageEntityLength { get; set; }
    internal byte[] MarkerBuffer { get; } = new byte[20];
    internal int MarkerBufferLength { get; set; }
    internal int MarkerBufferNeeded { get; set; }
    internal byte[]? Comment { get; set; }
    internal uint CommentLength { get; set; }
    internal uint CommentProgress { get; set; }
    internal int AdaptiveTemplateMoves { get; set; }
    internal uint[] AdaptiveTemplateRows { get; } = new uint[T85Constants.MaximumAdaptiveTemplateMoves];
    internal int[] AdaptiveTemplateXValues { get; } = new int[T85Constants.MaximumAdaptiveTemplateMoves];
    internal uint[] RowHistory { get; } = new uint[3];
    internal bool PseudoPixel { get; set; }
    internal bool LineNotTypical { get; set; }
    internal bool Interrupt { get; set; }
    internal int EndOfData { get; set; }
    internal T81T82ArithmeticDecoder ArithmeticDecoder { get; }

    public void Dispose() {
        if (_disposed)
            return;

        T85Decode.Release(this);
        RowWriteHandler = null;
        RowWriteUserData = null;
        CommentHandler = null;
        CommentUserData = null;
        _disposed = true;
    }

    internal void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    internal void Revive() => _disposed = false;
}

public static class T85Decode {
    public static T85DecodeState Initialize(
        T85DecodeState? state,
        t4_row_write_handler_t? rowWriteHandler,
        object? userData) {
        state ??= new T85DecodeState();
        state.Revive();
        state.Logging.Protocol = "T.85";
        state.RowWriteHandler = rowWriteHandler;
        state.RowWriteUserData = userData;
        state.MinimumBitPlanes = 1;
        state.MaximumBitPlanes = 1;
        state.MaximumImageWidth = 0;
        state.MaximumImageLength = 0;
        state.ArithmeticDecoder.Restart(false);
        Restart(state);
        return state;
    }

    public static bool AnalyseHeader(
        out uint width,
        out uint length,
        ReadOnlySpan<byte> data) {
        if (data.Length < T85Constants.BinaryImageHeaderLength) {
            width = 0;
            length = 0;
            return false;
        }

        width = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(6, 4));
        length = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(10, 4));

        if ((data[19] & (byte)T85Options.VariableLength) == 0)
            return true;

        for (int index = 20; index <= data.Length - 6; index++) {
            if (data[index] != (byte)T82Marker.Escape)
                continue;

            T82Marker marker = (T82Marker)data[index + 1];

            if (marker == T82Marker.Comment) {
                uint commentLength =
                    BinaryPrimitives.ReadUInt32BigEndian(
                        data.Slice(index + 2, 4));

                ulong total = 6UL + commentLength;

                if (total > (ulong)(data.Length - index))
                    break;

                index += checked((int)total) - 1;
            } else if (marker == T82Marker.AdaptiveTemplateMove) {
                index += 7;
            } else if (marker == T82Marker.NewLength) {
                length = BinaryPrimitives.ReadUInt32BigEndian(
                    data.Slice(index + 2, 4));
                break;
            }
        }

        return true;
    }

    public static int Restart(T85DecodeState state) {
        ValidateState(state);

        state.ImageWidth = 0;
        state.ImageLength = 0;
        state.RowsPerStripe = 0;
        state.MaximumAdaptiveTemplateX = 0;
        state.BytesPerRow = 0;
        state.AdaptiveTemplateX = 0;
        state.BinaryImageEntityLength = 0;
        Array.Clear(state.MarkerBuffer);
        state.MarkerBufferLength = 0;
        state.MarkerBufferNeeded = 0;
        state.AdaptiveTemplateMoves = 0;
        Array.Clear(state.AdaptiveTemplateRows);
        Array.Clear(state.AdaptiveTemplateXValues);
        Array.Clear(state.RowHistory);
        state.PseudoPixel = false;
        state.LineNotTypical = false;
        state.Interrupt = false;
        state.EndOfData = 0;
        state.Comment = null;
        state.CommentLength = 0;
        state.CommentProgress = 0;
        state.CompressedImageSizeBytes = 0;
        state.ArithmeticDecoder.Restart(false);
        return 0;
    }

    public static int Put(
        T85DecodeState state,
        ReadOnlySpan<byte> data) {
        ValidateState(state);

        if (data.IsEmpty) {
            if (state.CurrentRow >= state.ImageLength &&
                state.ImageLength != 0) {
                return (int)T85DecodeStatus.Ok;
            }

            if (state.EndOfData > 0)
                return (int)T85DecodeStatus.InvalidData;

            state.EndOfData = 1;
        }

        state.CompressedImageSizeBytes = checked(
            state.CompressedImageSizeBytes + data.Length);

        int consumed = 0;

        if (state.BinaryImageEntityLength < T85Constants.BinaryImageHeaderLength) {
            int copy = Math.Min(
                T85Constants.BinaryImageHeaderLength -
                (int)state.BinaryImageEntityLength,
                data.Length);

            data.Slice(0, copy).CopyTo(
                state.MarkerBuffer.AsSpan(
                    (int)state.BinaryImageEntityLength));

            state.BinaryImageEntityLength += (uint)copy;
            consumed = copy;

            if (state.BinaryImageEntityLength < T85Constants.BinaryImageHeaderLength)
                return (int)T85DecodeStatus.MoreData;

            int headerResult = ExtractBinaryImageHeader(state);

            if (headerResult != (int)T85DecodeStatus.Ok)
                return headerResult;

            int bufferedRows =
                (state.Options & T85Options.LowestResolutionLayerTwoRows) != 0
                    ? 2
                    : 3;

            int minimumLength = checked(bufferedRows * state.BytesPerRow);

            if (state.RowBuffer.Length < minimumLength)
                state.RowBuffer = new byte[minimumLength];
            else
                Array.Clear(state.RowBuffer);

            state.ArithmeticDecoder.Restart(false);
            state.ArithmeticDecoder.NoPadding =
                (state.Options & T85Options.VariableLength) != 0;

            state.Comment = null;
            state.CommentLength = 0;
            state.CommentProgress = 0;
            state.MarkerBufferLength = 0;
            state.MarkerBufferNeeded = 2;
            state.CurrentColumn = 0;
            state.CurrentRow = 0;
            state.CurrentStripeRow = 0;
            state.PseudoPixel = true;
            state.AdaptiveTemplateMoves = 0;
            state.AdaptiveTemplateX = 0;
            state.LineNotTypical = true;
            state.RowPointers[0] = 0;
            state.RowPointers[1] = -1;
            state.RowPointers[2] = -1;
        }

        while (consumed < data.Length || state.EndOfData == 1) {
            if (state.EndOfData == 1) {
                state.MarkerBufferNeeded = 2;
                state.Options &= ~T85Options.VariableLength;
                state.EndOfData = 2;
            }

            if (state.CommentLength != 0) {
                int available = data.Length - consumed;
                uint remaining =
                    state.CommentLength -
                    state.CommentProgress;

                int chunk = (int)Math.Min((uint)available, remaining);

                if (state.Comment is not null && chunk > 0) {
                    data.Slice(consumed, chunk).CopyTo(
                        state.Comment.AsSpan(
                            (int)state.CommentProgress));
                }

                state.CommentProgress += (uint)chunk;
                consumed += chunk;

                if (state.CommentProgress >= state.CommentLength) {
                    if (state.CommentHandler is not null) {
                        ReadOnlySpan<byte> comment =
                            state.Comment is null
                                ? ReadOnlySpan<byte>.Empty
                                : state.Comment.AsSpan();

                        state.Interrupt =
                            state.CommentHandler(
                                state.CommentUserData,
                                comment) != 0;
                    }

                    state.Comment = null;
                    state.CommentLength = 0;
                    state.CommentProgress = 0;

                    if (state.Interrupt)
                        return (int)T85DecodeStatus.Interrupt;
                }

                if (chunk == 0 && consumed >= data.Length)
                    break;

                continue;
            }

            if (state.MarkerBufferLength > 0) {
                while (state.MarkerBufferLength < state.MarkerBufferNeeded &&
                       consumed < data.Length) {
                    state.MarkerBuffer[state.MarkerBufferLength++] =
                        data[consumed++];
                }

                if (state.MarkerBufferLength < state.MarkerBufferNeeded)
                    continue;

                int markerResult =
                    ProcessMarker(state, data, ref consumed);

                if (markerResult != int.MaxValue)
                    return markerResult;
            } else if (consumed < data.Length &&
                       data[consumed] == (byte)T82Marker.Escape) {
                state.MarkerBuffer[state.MarkerBufferLength++] =
                    data[consumed++];
            } else {
                int used = DecodePscd(
                    state,
                    data.Slice(consumed));

                consumed += used;

                if (state.Interrupt)
                    return (int)T85DecodeStatus.Interrupt;

                if (consumed < data.Length &&
                    data[consumed] != (byte)T82Marker.Escape) {
                    state.EndOfData = 2;
                    return (int)T85DecodeStatus.InvalidData;
                }

                if (used == 0 && consumed >= data.Length)
                    break;
            }
        }

        return (int)T85DecodeStatus.MoreData;
    }

    public static int Put(
        T85DecodeState state,
        byte[]? data,
        int length) {
        if (data is null) {
            if (length != 0)
                throw new ArgumentNullException(nameof(data));

            return Put(state, ReadOnlySpan<byte>.Empty);
        }

        if ((uint)length > (uint)data.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        return Put(state, data.AsSpan(0, length));
    }

    public static void ReceiveStatus(
        T85DecodeState state,
        int status) {
        ValidateState(state);
        state.Logging.Flow($"Signal status is {status}");

        switch ((T85SignalStatus)status) {
            case T85SignalStatus.TrainingInProgress:
            case T85SignalStatus.TrainingFailed:
            case T85SignalStatus.TrainingSucceeded:
            case T85SignalStatus.CarrierUp:
                break;

            case T85SignalStatus.CarrierDown:
            case T85SignalStatus.EndOfData:
                _ = Put(state, ReadOnlySpan<byte>.Empty);
                break;

            default:
                state.Logging.Warning($"Unexpected rx status - {status}");
                break;
        }
    }

    public static int SetRowWriteHandler(
        T85DecodeState state,
        t4_row_write_handler_t? handler,
        object? userData) {
        ValidateState(state);
        state.RowWriteHandler = handler;
        state.RowWriteUserData = userData;
        return 0;
    }

    public static int SetCommentHandler(
        T85DecodeState state,
        uint maximumCommentLength,
        T85RowWriteDelegate? handler,
        object? userData) {
        ValidateState(state);
        state.MaximumCommentLength = maximumCommentLength;
        state.CommentHandler = handler;
        state.CommentUserData = userData;
        return 0;
    }

    public static int SetImageSizeConstraints(
        T85DecodeState state,
        uint maximumWidth,
        uint maximumLength) {
        ValidateState(state);
        state.MaximumImageWidth = maximumWidth;
        state.MaximumImageLength = maximumLength;
        return 0;
    }

    public static uint GetImageWidth(T85DecodeState state) {
        ValidateState(state);
        return state.ImageWidth;
    }

    public static uint GetImageLength(T85DecodeState state) {
        ValidateState(state);
        return state.ImageLength;
    }

    public static int GetCompressedImageSize(T85DecodeState state) {
        ValidateState(state);
        return checked(state.CompressedImageSizeBytes * 8);
    }

    public static int NewPlane(T85DecodeState state) {
        ValidateState(state);

        if (state.CurrentBitPlane >= state.BitPlanes - 1)
            return -1;

        state.CurrentBitPlane++;
        state.AdaptiveTemplateX = 0;
        Array.Clear(state.MarkerBuffer);
        state.MarkerBufferLength = 0;
        state.MarkerBufferNeeded = 0;
        state.AdaptiveTemplateMoves = 0;
        Array.Clear(state.AdaptiveTemplateRows);
        Array.Clear(state.AdaptiveTemplateXValues);
        Array.Clear(state.RowHistory);
        state.PseudoPixel = false;
        state.LineNotTypical = false;
        state.Interrupt = false;
        state.EndOfData = 0;
        state.Comment = null;
        state.CommentLength = 0;
        state.CommentProgress = 0;
        state.CompressedImageSizeBytes = 0;

        state.ArithmeticDecoder.Restart(false);
        state.ArithmeticDecoder.NoPadding =
            (state.Options & T85Options.VariableLength) != 0;

        state.MarkerBufferLength = 0;
        state.MarkerBufferNeeded = 2;
        state.CurrentColumn = 0;
        state.CurrentRow = 0;
        state.CurrentStripeRow = 0;
        state.PseudoPixel = true;
        state.AdaptiveTemplateMoves = 0;
        state.AdaptiveTemplateX = 0;
        state.LineNotTypical = true;
        state.RowPointers[0] = 0;
        state.RowPointers[1] = -1;
        state.RowPointers[2] = -1;
        return 0;
    }

    public static T85Log GetLoggingState(T85DecodeState state) {
        ValidateState(state);
        return state.Logging;
    }

    public static int Release(T85DecodeState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.RowBuffer = [];
        state.Comment = null;
        return 0;
    }

    public static int Free(T85DecodeState? state) {
        state?.Dispose();
        return 0;
    }

    private static int ProcessMarker(
        T85DecodeState state,
        ReadOnlySpan<byte> data,
        ref int consumed) {
        T82Marker marker = (T82Marker)state.MarkerBuffer[1];

        switch (marker) {
            case T82Marker.Stuff:
                _ = DecodePscd(
                    state,
                    state.MarkerBuffer.AsSpan(0, 2));
                state.MarkerBufferLength = 0;

                if (state.Interrupt)
                    return (int)T85DecodeStatus.Interrupt;

                break;

            case T82Marker.Abort:
                state.MarkerBufferLength = 0;
                return (int)T85DecodeStatus.Aborted;

            case T82Marker.Comment:
                state.MarkerBufferNeeded = 6;

                if (state.MarkerBufferLength < 6)
                    return int.MaxValue;

                state.MarkerBufferNeeded = 2;
                state.MarkerBufferLength = 0;
                state.CommentLength =
                    BinaryPrimitives.ReadUInt32BigEndian(
                        state.MarkerBuffer.AsSpan(2, 4));

                if (state.CommentHandler is not null &&
                    state.CommentLength > 0 &&
                    state.CommentLength <= state.MaximumCommentLength &&
                    state.CommentLength <= int.MaxValue) {
                    state.Comment = new byte[checked((int)state.CommentLength)];
                }

                state.CommentProgress = 0;
                break;

            case T82Marker.AdaptiveTemplateMove:
                state.MarkerBufferNeeded = 8;

                if (state.MarkerBufferLength < 8)
                    return int.MaxValue;

                state.MarkerBufferNeeded = 2;
                state.MarkerBufferLength = 0;

                if (state.AdaptiveTemplateMoves >=
                    T85Constants.MaximumAdaptiveTemplateMoves) {
                    state.EndOfData = 2;
                    return (int)T85DecodeStatus.InvalidData;
                }

                int move = state.AdaptiveTemplateMoves;
                state.AdaptiveTemplateRows[move] =
                    BinaryPrimitives.ReadUInt32BigEndian(
                        state.MarkerBuffer.AsSpan(2, 4));
                state.AdaptiveTemplateXValues[move] =
                    state.MarkerBuffer[6];

                int minimum =
                    (state.Options & T85Options.LowestResolutionLayerTwoRows) != 0
                        ? 5
                        : 3;

                if (state.AdaptiveTemplateXValues[move] >
                        state.MaximumAdaptiveTemplateX ||
                    (state.AdaptiveTemplateXValues[move] > 0 &&
                     state.AdaptiveTemplateXValues[move] < minimum) ||
                    state.MarkerBuffer[7] != 0) {
                    state.EndOfData = 2;
                    return (int)T85DecodeStatus.InvalidData;
                }

                state.AdaptiveTemplateMoves++;
                break;

            case T82Marker.NewLength:
                state.MarkerBufferNeeded = 6;

                if (state.MarkerBufferLength < 6)
                    return int.MaxValue;

                state.MarkerBufferNeeded = 2;
                state.MarkerBufferLength = 0;

                if ((state.Options & T85Options.VariableLength) == 0) {
                    state.EndOfData = 2;
                    return (int)T85DecodeStatus.InvalidData;
                }

                state.Options &= ~T85Options.VariableLength;
                uint newLength =
                    BinaryPrimitives.ReadUInt32BigEndian(
                        state.MarkerBuffer.AsSpan(2, 4));

                if (newLength > state.ImageLength) {
                    state.EndOfData = 2;
                    return (int)T85DecodeStatus.InvalidData;
                }

                state.ImageLength = newLength;
                break;

            case T82Marker.StripeDataNormal:
            case T82Marker.StripeDataReset:
                return ProcessStripeEndMarker(
                    state,
                    marker,
                    data,
                    ref consumed);

            default:
                state.MarkerBufferLength = 0;
                state.EndOfData = 2;
                return (int)T85DecodeStatus.InvalidData;
        }

        return int.MaxValue;
    }

    private static int ProcessStripeEndMarker(
        T85DecodeState state,
        T82Marker marker,
        ReadOnlySpan<byte> data,
        ref int consumed) {
        if ((state.Options & T85Options.VariableLength) == 0) {
            state.MarkerBufferLength = 0;

            if (FinishStripeDataEntity(state, marker))
                return (int)T85DecodeStatus.Interrupt;

            if (state.CurrentRow >= state.ImageLength) {
                state.CompressedImageSizeBytes -=
                    data.Length - consumed;
                return (int)T85DecodeStatus.Ok;
            }

            return int.MaxValue;
        }

        if (state.MarkerBufferNeeded < 3)
            state.MarkerBufferNeeded = 3;

        if (state.MarkerBufferLength < 3)
            return int.MaxValue;

        if (state.MarkerBuffer[2] != (byte)T82Marker.Escape) {
            state.MarkerBufferNeeded = 2;
            state.MarkerBufferLength = 0;
            consumed--;

            if (FinishStripeDataEntity(state, marker))
                return (int)T85DecodeStatus.Interrupt;

            if (state.CurrentRow >= state.ImageLength) {
                state.CompressedImageSizeBytes -=
                    data.Length - consumed;
                return (int)T85DecodeStatus.Ok;
            }

            return int.MaxValue;
        }

        if (state.MarkerBufferNeeded < 4)
            state.MarkerBufferNeeded = 4;

        if (state.MarkerBufferLength < 4)
            return int.MaxValue;

        if (state.MarkerBuffer[3] != (byte)T82Marker.NewLength) {
            state.MarkerBufferNeeded = 2;

            if (FinishStripeDataEntity(state, marker))
                return (int)T85DecodeStatus.Interrupt;

            if (state.CurrentRow >= state.ImageLength) {
                state.CompressedImageSizeBytes -=
                    data.Length - consumed;
                return (int)T85DecodeStatus.Ok;
            }

            state.MarkerBuffer[0] = state.MarkerBuffer[2];
            state.MarkerBuffer[1] = state.MarkerBuffer[3];
            state.MarkerBufferLength = 2;
            return int.MaxValue;
        }

        if (state.MarkerBufferNeeded < 8)
            state.MarkerBufferNeeded = 8;

        if (state.MarkerBufferLength < 8)
            return int.MaxValue;

        state.MarkerBufferNeeded = 2;
        state.MarkerBufferLength = 0;
        state.Options &= ~T85Options.VariableLength;

        uint newLength =
            BinaryPrimitives.ReadUInt32BigEndian(
                state.MarkerBuffer.AsSpan(4, 4));

        if (newLength > state.ImageLength) {
            state.EndOfData = 2;
            return (int)T85DecodeStatus.InvalidData;
        }

        state.ImageLength = newLength;

        if (FinishStripeDataEntity(state, marker))
            return (int)T85DecodeStatus.Interrupt;

        return int.MaxValue;
    }

    private static int DecodePscd(
        T85DecodeState state,
        ReadOnlySpan<byte> data) {
        int bufferedRows =
            (state.Options & T85Options.LowestResolutionLayerTwoRows) != 0
                ? 2
                : 3;

        state.ArithmeticDecoder.SetInput(data);

        while (state.CurrentStripeRow < state.RowsPerStripe &&
               state.CurrentRow < state.ImageLength &&
               !state.Interrupt) {
            if (state.CurrentColumn == 0 && state.PseudoPixel) {
                for (int move = 0;
                     move < state.AdaptiveTemplateMoves;
                     move++) {
                    if (state.AdaptiveTemplateRows[move] ==
                        state.CurrentStripeRow) {
                        state.AdaptiveTemplateX =
                            state.AdaptiveTemplateXValues[move];
                    }
                }
            }

            if ((state.Options & T85Options.TypicalPredictionBottom) != 0 &&
                state.PseudoPixel) {
                int typicalChange = state.ArithmeticDecoder.Decode(
                    (state.Options & T85Options.LowestResolutionLayerTwoRows) != 0
                        ? T85Constants.TypicalPredictionTwoRowContext
                        : T85Constants.TypicalPredictionThreeRowContext);

                if (typicalChange < 0)
                    return state.ArithmeticDecoder.Consumed;

                state.LineNotTypical =
                    !((typicalChange != 0) ^ state.LineNotTypical);

                if (!state.LineNotTypical) {
                    if (state.RowPointers[1] < 0) {
                        Span<byte> current = GetRow(state, state.RowPointers[0]);
                        current.Clear();
                        state.Interrupt = WriteRow(state, current);
                        state.RowPointers[2] = state.RowPointers[1];
                        state.RowPointers[1] = state.RowPointers[0];
                        state.RowPointers[0]++;

                        if (state.RowPointers[0] >= bufferedRows)
                            state.RowPointers[0] = 0;
                    } else {
                        state.Interrupt = WriteRow(
                            state,
                            GetRow(state, state.RowPointers[1]));
                        state.RowPointers[2] = state.RowPointers[1];
                    }

                    state.CurrentStripeRow++;
                    state.CurrentRow++;
                    continue;
                }
            }

            state.PseudoPixel = false;

            int byteOffset = checked((int)(state.CurrentColumn >> 3));

            if (state.CurrentColumn == 0) {
                state.RowHistory[0] = 0;
                state.RowHistory[1] = state.RowPointers[1] >= 0
                    ? (uint)GetRow(state, state.RowPointers[1])[0] << 8
                    : 0;
                state.RowHistory[2] = state.RowPointers[2] >= 0
                    ? (uint)GetRow(state, state.RowPointers[2])[0] << 8
                    : 0;
            }

            while (state.CurrentColumn < state.ImageWidth) {
                if ((state.CurrentColumn & 7) == 0 &&
                    state.CurrentColumn < (uint)((state.BytesPerRow - 1) * 8) &&
                    state.RowPointers[1] >= 0) {
                    int nextByte = byteOffset + 1;
                    state.RowHistory[1] |=
                        GetRow(state, state.RowPointers[1])[nextByte];

                    if (state.RowPointers[2] >= 0) {
                        state.RowHistory[2] |=
                            GetRow(state, state.RowPointers[2])[nextByte];
                    }
                }

                if ((state.Options & T85Options.LowestResolutionLayerTwoRows) != 0) {
                    do {
                        int context = (int)(state.RowHistory[0] & 0x00F);

                        if (state.AdaptiveTemplateX != 0) {
                            context |= (int)((state.RowHistory[1] >> 9) & 0x3E0);

                            if (state.CurrentColumn >=
                                (uint)state.AdaptiveTemplateX) {
                                if (state.AdaptiveTemplateX < 8) {
                                    context |=
                                        (int)((state.RowHistory[0] >>
                                               (state.AdaptiveTemplateX - 5)) &
                                              0x010);
                                } else {
                                    int offset =
                                        ((int)state.CurrentColumn -
                                         state.AdaptiveTemplateX) -
                                        ((int)state.CurrentColumn & ~7);

                                    int sourceIndex =
                                        byteOffset + (offset >> 3);

                                    context |=
                                        ((GetRow(state, state.RowPointers[0])[sourceIndex] >>
                                          (7 - (offset & 7))) & 1) << 4;
                                }
                            }
                        } else {
                            context |= (int)((state.RowHistory[1] >> 9) & 0x3F0);
                        }

                        int pixel = state.ArithmeticDecoder.Decode(context);

                        if (pixel < 0)
                            return state.ArithmeticDecoder.Consumed;

                        state.RowHistory[0] =
                            (state.RowHistory[0] << 1) |
                            (uint)pixel;
                        state.RowHistory[1] <<= 1;
                        state.CurrentColumn++;
                    }
                    while ((state.CurrentColumn & 7) != 0 &&
                           state.CurrentColumn < state.ImageWidth);
                } else {
                    do {
                        int context =
                            (int)((state.RowHistory[2] >> 7) & 0x380) |
                            (int)(state.RowHistory[0] & 0x003);

                        if (state.AdaptiveTemplateX != 0) {
                            context |= (int)((state.RowHistory[1] >> 11) & 0x078);

                            if (state.CurrentColumn >=
                                (uint)state.AdaptiveTemplateX) {
                                if (state.AdaptiveTemplateX < 8) {
                                    context |=
                                        (int)((state.RowHistory[0] >>
                                               (state.AdaptiveTemplateX - 3)) &
                                              0x004);
                                } else {
                                    int offset =
                                        ((int)state.CurrentColumn -
                                         state.AdaptiveTemplateX) -
                                        ((int)state.CurrentColumn & ~7);

                                    int sourceIndex =
                                        byteOffset + (offset >> 3);

                                    context |=
                                        ((GetRow(state, state.RowPointers[0])[sourceIndex] >>
                                          (7 - (offset & 7))) & 1) << 2;
                                }
                            }
                        } else {
                            context |= (int)((state.RowHistory[1] >> 11) & 0x07C);
                        }

                        int pixel = state.ArithmeticDecoder.Decode(context);

                        if (pixel < 0)
                            return state.ArithmeticDecoder.Consumed;

                        state.RowHistory[0] =
                            (state.RowHistory[0] << 1) |
                            (uint)pixel;
                        state.RowHistory[1] <<= 1;
                        state.RowHistory[2] <<= 1;
                        state.CurrentColumn++;
                    }
                    while ((state.CurrentColumn & 7) != 0 &&
                           state.CurrentColumn < state.ImageWidth);
                }

                GetRow(state, state.RowPointers[0])[byteOffset] =
                    unchecked((byte)state.RowHistory[0]);
                byteOffset++;
            }

            int unusedBits =
                state.BytesPerRow * 8 -
                checked((int)state.ImageWidth);

            if (unusedBits > 0) {
                Span<byte> current = GetRow(state, state.RowPointers[0]);
                current[^1] = (byte)(current[^1] << unusedBits);
            }

            state.Interrupt = WriteRow(
                state,
                GetRow(state, state.RowPointers[0]));

            state.CurrentColumn = 0;
            state.PseudoPixel = true;
            state.RowPointers[2] = state.RowPointers[1];
            state.RowPointers[1] = state.RowPointers[0];
            state.RowPointers[0]++;

            if (state.RowPointers[0] >= bufferedRows)
                state.RowPointers[0] = 0;

            state.CurrentStripeRow++;
            state.CurrentRow++;
        }

        return state.ArithmeticDecoder.Consumed;
    }

    private static bool FinishStripeDataEntity(
        T85DecodeState state,
        T82Marker marker) {
        state.ArithmeticDecoder.NoPadding = false;

        if (DecodePscd(
                state,
                state.MarkerBuffer.AsSpan(0, 2)) != 2 &&
            state.Interrupt) {
            return true;
        }

        state.ArithmeticDecoder.Restart(
            marker == T82Marker.StripeDataNormal);
        state.ArithmeticDecoder.NoPadding =
            (state.Options & T85Options.VariableLength) != 0;

        state.CurrentColumn = 0;
        state.CurrentStripeRow = 0;
        state.PseudoPixel = true;
        state.AdaptiveTemplateMoves = 0;

        if (marker == T82Marker.StripeDataReset) {
            state.AdaptiveTemplateX = 0;
            state.LineNotTypical = true;
            state.RowPointers[0] = 0;
            state.RowPointers[1] = -1;
            state.RowPointers[2] = -1;
        }

        return false;
    }

    private static int ExtractBinaryImageHeader(T85DecodeState state) {
        ReadOnlySpan<byte> header = state.MarkerBuffer;

        if (header[0] != 0 ||
            header[1] != 0 ||
            header[3] != 0 ||
            header[17] != 0 ||
            (header[18] & 0xF0) != 0) {
            state.Logging.Flow(
                "BIH invalid. Fixed bytes do not contain expected values.");
            state.EndOfData = 2;
            return (int)T85DecodeStatus.InvalidData;
        }

        state.BitPlanes = header[2];
        state.CurrentBitPlane = 0;
        state.ImageWidth =
            BinaryPrimitives.ReadUInt32BigEndian(header.Slice(4, 4));
        state.ImageLength =
            BinaryPrimitives.ReadUInt32BigEndian(header.Slice(8, 4));
        state.RowsPerStripe =
            BinaryPrimitives.ReadUInt32BigEndian(header.Slice(12, 4));
        state.MaximumAdaptiveTemplateX = header[16];
        state.Options = (T85Options)header[19];

        if (state.BitPlanes < state.MinimumBitPlanes ||
            state.BitPlanes > state.MaximumBitPlanes) {
            state.EndOfData = 2;
            return (int)T85DecodeStatus.InvalidData;
        }

        if (state.ImageWidth == 0 ||
            (state.MaximumImageWidth != 0 &&
             state.ImageWidth > state.MaximumImageWidth)) {
            state.EndOfData = 2;
            return (int)T85DecodeStatus.InvalidData;
        }

        if (state.ImageLength == 0) {
            state.EndOfData = 2;
            return (int)T85DecodeStatus.InvalidData;
        }

        if (state.MaximumImageLength != 0) {
            if ((state.Options & T85Options.VariableLength) != 0) {
                if (state.ImageLength > state.MaximumImageLength)
                    state.ImageLength = state.MaximumImageLength;
            } else if (state.ImageLength > state.MaximumImageLength) {
                state.EndOfData = 2;
                return (int)T85DecodeStatus.InvalidData;
            }
        }

        if (state.RowsPerStripe == 0 ||
            state.MaximumAdaptiveTemplateX > 127 ||
            (state.Options &
             ~(T85Options.LowestResolutionLayerTwoRows |
               T85Options.VariableLength |
               T85Options.TypicalPredictionBottom)) != 0) {
            state.EndOfData = 2;
            return (int)T85DecodeStatus.InvalidData;
        }

        state.BytesPerRow = checked((int)((state.ImageWidth + 7) >> 3));
        state.Logging.Flow(
            $"BIH is OK. Image is {state.ImageWidth}x{state.ImageLength} pixels");
        return (int)T85DecodeStatus.Ok;
    }

    private static Span<byte> GetRow(
        T85DecodeState state,
        int rowIndex) {
        if (rowIndex < 0)
            return Span<byte>.Empty;

        return state.RowBuffer.AsSpan(
            checked(rowIndex * state.BytesPerRow),
            state.BytesPerRow);
    }

    private static bool WriteRow(
        T85DecodeState state,
        ReadOnlySpan<byte> row) {
        return state.RowWriteHandler is not null &&
               state.RowWriteHandler(
                   state.RowWriteUserData,
                   row,
                   row.Length) != 0;
    }

    private static void ValidateState(T85DecodeState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
    }
}

public static partial class T85Api {
    public const int T4_DECODE_MORE_DATA = T85Constants.T4_DECODE_MORE_DATA;
    public const int T4_DECODE_OK = T85Constants.T4_DECODE_OK;
    public const int T4_DECODE_INTERRUPT = T85Constants.T4_DECODE_INTERRUPT;
    public const int T4_DECODE_ABORTED = T85Constants.T4_DECODE_ABORTED;
    public const int T4_DECODE_NOMEM = T85Constants.T4_DECODE_NOMEM;
    public const int T4_DECODE_INVALID_DATA = T85Constants.T4_DECODE_INVALID_DATA;

    public static bool t85_analyse_header(
        out uint width,
        out uint length,
        ReadOnlySpan<byte> data) =>
        T85Decode.AnalyseHeader(out width, out length, data);

    public static T85DecodeState t85_decode_init(
        T85DecodeState? state,
        t4_row_write_handler_t? handler,
        object? userData) =>
        T85Decode.Initialize(state, handler, userData);

    public static int t85_decode_restart(T85DecodeState state) =>
        T85Decode.Restart(state);

    public static int t85_decode_put(
        T85DecodeState state,
        ReadOnlySpan<byte> data) =>
        T85Decode.Put(state, data);

    public static int t85_decode_put(
        T85DecodeState state,
        byte[]? data,
        int length) =>
        T85Decode.Put(state, data, length);

    public static void t85_decode_rx_status(
        T85DecodeState state,
        int status) =>
        T85Decode.ReceiveStatus(state, status);

    public static int t85_decode_set_row_write_handler(
        T85DecodeState state,
        t4_row_write_handler_t? handler,
        object? userData) =>
        T85Decode.SetRowWriteHandler(state, handler, userData);

    public static int t85_decode_set_comment_handler(
        T85DecodeState state,
        uint maximumCommentLength,
        T85RowWriteDelegate? handler,
        object? userData) =>
        T85Decode.SetCommentHandler(
            state,
            maximumCommentLength,
            handler,
            userData);

    public static int t85_decode_set_image_size_constraints(
        T85DecodeState state,
        uint maximumWidth,
        uint maximumLength) =>
        T85Decode.SetImageSizeConstraints(
            state,
            maximumWidth,
            maximumLength);

    public static uint t85_decode_get_image_width(T85DecodeState state) =>
        T85Decode.GetImageWidth(state);

    public static uint t85_decode_get_image_length(T85DecodeState state) =>
        T85Decode.GetImageLength(state);

    public static int t85_decode_get_compressed_image_size(T85DecodeState state) =>
        T85Decode.GetCompressedImageSize(state);

    public static int t85_decode_new_plane(T85DecodeState state) =>
        T85Decode.NewPlane(state);

    public static T85Log t85_decode_get_logging_state(T85DecodeState state) =>
        T85Decode.GetLoggingState(state);

    public static int t85_decode_release(T85DecodeState state) =>
        T85Decode.Release(state);

    public static int t85_decode_free(T85DecodeState? state) =>
        T85Decode.Free(state);
}
