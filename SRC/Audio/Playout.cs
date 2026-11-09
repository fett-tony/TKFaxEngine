/*
 * TKFaxEngine - managed C# port
 *
 * Playout.cs
 *
 * Combined port of playout.h, private/playout.h and playout.c.
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2005 Steve Underwood.
 *
 * This port preserves the GNU Lesser General Public License version 2.1
 * licensing terms of the original source files.
 */

#nullable enable

namespace TKFaxEngine.Audio;

public enum PlayoutResult {
    Ok = 0,
    Error = 1,
    Empty = 2,
    NoFrame = 3,
    FillIn = 4,
    Drop = 5
}

public enum PlayoutFrameType {
    Control = 0,
    Silence = 1,
    Speech = 2
}

/// <summary>
/// Managed equivalent of <c>playout_frame_t</c>.
/// </summary>
public sealed class PlayoutFrame {
    public object? Data { get; set; }

    public int Type { get; set; }

    public int SenderStamp { get; set; }

    public int SenderLength { get; set; }

    public int ReceiverStamp { get; set; }

    internal PlayoutFrame? Earlier { get; set; }

    internal PlayoutFrame? Later { get; set; }

    public void CopyFrom(PlayoutFrame source) {
        ArgumentNullException.ThrowIfNull(source);

        Data = source.Data;
        Type = source.Type;
        SenderStamp = source.SenderStamp;
        SenderLength = source.SenderLength;
        ReceiverStamp = source.ReceiverStamp;
        Earlier = null;
        Later = null;
    }
}

/// <summary>
/// Managed equivalent of <c>playout_state_t</c>.
/// </summary>
public sealed class PlayoutState : IDisposable {
    private bool _disposed;

    public PlayoutState(int minimumLength, int maximumLength) {
        Restart(minimumLength, maximumLength);
    }

    public bool Dynamic { get; internal set; }

    public int MinimumLength { get; internal set; }

    public int MaximumLength { get; internal set; }

    public int DropableThreshold { get; internal set; }

    public bool Start { get; internal set; }

    internal PlayoutFrame? FirstFrame { get; set; }

    internal PlayoutFrame? LastFrame { get; set; }

    internal PlayoutFrame? FreeFrames { get; set; }

    public int FramesIn { get; internal set; }

    public int FramesOut { get; internal set; }

    public int FramesOutOfSequence { get; internal set; }

    public int FramesLate { get; internal set; }

    public int FramesMissing { get; internal set; }

    public int FramesTrimmed { get; internal set; }

    public int LatestExpected { get; internal set; }

    public int Current { get; internal set; }

    public int LastSpeechSenderStamp { get; internal set; }

    public int LastSpeechSenderLength { get; internal set; }

    public bool NotFirst { get; internal set; }

    public int SinceLastStep { get; internal set; }

    public int StateJustInTime { get; internal set; }

    public int StateLate { get; internal set; }

    public int TargetBufferLength { get; internal set; }

    public int ActualBufferLength { get; internal set; }

    public bool IsDisposed => _disposed;

    public int NextDue() {
        ThrowIfDisposed();
        return unchecked(LastSpeechSenderStamp + LastSpeechSenderLength);
    }

    public int CurrentLength() {
        ThrowIfDisposed();
        return TargetBufferLength;
    }

    public PlayoutFrame? GetUnconditional() {
        ThrowIfDisposed();
        return Playout.GetUnconditionalCore(this);
    }

    public PlayoutResult Get(PlayoutFrame destination, int now) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(destination);
        return Playout.GetCore(this, destination, now);
    }

    public PlayoutResult Put(
        object? data,
        int type,
        int senderLength,
        int senderStamp,
        int receiverStamp) {
        ThrowIfDisposed();
        return Playout.PutCore(
            this,
            data,
            type,
            senderLength,
            senderStamp,
            receiverStamp);
    }

    public void Restart(int minimumLength, int maximumLength) {
        if (minimumLength < 0)
            throw new ArgumentOutOfRangeException(nameof(minimumLength));
        if (maximumLength < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumLength));

        ClearFrames();

        Dynamic = minimumLength < maximumLength;
        MinimumLength = minimumLength;
        MaximumLength = maximumLength > minimumLength
            ? maximumLength
            : minimumLength;
        DropableThreshold = 0x10000000 / 100;
        Start = true;

        FramesIn = 0;
        FramesOut = 0;
        FramesOutOfSequence = 0;
        FramesLate = 0;
        FramesMissing = 0;
        FramesTrimmed = 0;
        LatestExpected = 0;
        Current = 0;
        LastSpeechSenderStamp = 0;
        LastSpeechSenderLength = 0;
        NotFirst = false;
        SinceLastStep = int.MaxValue;
        StateJustInTime = 0;
        StateLate = 0;

        int initialLength = (MaximumLength - MinimumLength) / 2;
        ActualBufferLength = initialLength;
        TargetBufferLength = initialLength;
        _disposed = false;
    }

    /// <summary>
    /// Releases all queued and pooled frame objects while retaining the state
    /// object itself.
    /// </summary>
    public int Release() {
        ThrowIfDisposed();
        ClearFrames();
        return 0;
    }

    public int Free() {
        Dispose();
        return 0;
    }

    public void Dispose() {
        if (_disposed)
            return;

        ClearFrames();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ClearFrames() {
        PlayoutFrame? frame = FirstFrame;
        while (frame is not null) {
            PlayoutFrame? next = frame.Later;
            frame.Data = null;
            frame.Earlier = null;
            frame.Later = null;
            frame = next;
        }

        frame = FreeFrames;
        while (frame is not null) {
            PlayoutFrame? next = frame.Later;
            frame.Data = null;
            frame.Earlier = null;
            frame.Later = null;
            frame = next;
        }

        FirstFrame = null;
        LastFrame = null;
        FreeFrames = null;
    }

    private void ThrowIfDisposed() {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PlayoutState));
    }
}

/// <summary>
/// Static and dynamically sized frame playout/jitter buffer.
/// </summary>
public static class Playout {
    public const int PLAYOUT_OK = (int)PlayoutResult.Ok;
    public const int PLAYOUT_ERROR = (int)PlayoutResult.Error;
    public const int PLAYOUT_EMPTY = (int)PlayoutResult.Empty;
    public const int PLAYOUT_NOFRAME = (int)PlayoutResult.NoFrame;
    public const int PLAYOUT_FILLIN = (int)PlayoutResult.FillIn;
    public const int PLAYOUT_DROP = (int)PlayoutResult.Drop;

    public const int PLAYOUT_TYPE_CONTROL = (int)PlayoutFrameType.Control;
    public const int PLAYOUT_TYPE_SILENCE = (int)PlayoutFrameType.Silence;
    public const int PLAYOUT_TYPE_SPEECH = (int)PlayoutFrameType.Speech;

    public static int playout_next_due(PlayoutState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.NextDue();
    }

    public static int playout_current_length(PlayoutState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.CurrentLength();
    }

    public static PlayoutFrame? playout_get_unconditional(PlayoutState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetUnconditional();
    }

    public static int playout_get(
        PlayoutState state,
        PlayoutFrame frame,
        int now) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(frame);
        return (int)state.Get(frame, now);
    }

    public static int playout_put(
        PlayoutState state,
        object? data,
        int type,
        int senderLength,
        int senderStamp,
        int receiverStamp) {
        ArgumentNullException.ThrowIfNull(state);
        return (int)state.Put(
            data,
            type,
            senderLength,
            senderStamp,
            receiverStamp);
    }

    public static void playout_restart(
        PlayoutState state,
        int minimumLength,
        int maximumLength) {
        ArgumentNullException.ThrowIfNull(state);
        state.Restart(minimumLength, maximumLength);
    }

    public static PlayoutState playout_init(
        int minimumLength,
        int maximumLength) {
        return new PlayoutState(minimumLength, maximumLength);
    }

    public static int playout_release(PlayoutState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    public static int playout_free(PlayoutState? state) {
        state?.Dispose();
        return 0;
    }

    internal static PlayoutFrame? GetUnconditionalCore(PlayoutState state) {
        PlayoutFrame? frame = QueueGet(state, int.MaxValue);
        if (frame is null)
            return null;

        ReturnToFreeList(state, frame);

        // As in the native implementation, the returned object is already on
        // the free list and must be copied before another Put() can reuse it.
        return frame;
    }

    internal static PlayoutResult GetCore(
        PlayoutState state,
        PlayoutFrame destination,
        int now) {
        _ = now; // Kept for API compatibility with the native implementation.

        state.LastSpeechSenderStamp = unchecked(
            state.LastSpeechSenderStamp + state.LastSpeechSenderLength);

        PlayoutFrame? frame = QueueGet(state, state.LastSpeechSenderStamp);
        if (frame is null) {
            state.FramesMissing++;
            return PlayoutResult.FillIn;
        }

        if (state.Dynamic && frame.Type == PLAYOUT_TYPE_SPEECH)
            AdjustDynamicBuffer(state, frame);

        if (frame.Type != PLAYOUT_TYPE_SPEECH) {
            state.LastSpeechSenderStamp = unchecked(
                state.LastSpeechSenderStamp - state.LastSpeechSenderLength);

            destination.CopyFrom(frame);
            ReturnToFreeList(state, frame);
            state.FramesOut++;
            return PlayoutResult.Ok;
        }

        if (frame.SenderStamp < state.LastSpeechSenderStamp) {
            destination.CopyFrom(frame);
            ReturnToFreeList(state, frame);

            state.LastSpeechSenderStamp = unchecked(
                state.LastSpeechSenderStamp - state.LastSpeechSenderLength);
            state.FramesOut++;
            state.FramesLate++;
            state.FramesMissing--;
            return PlayoutResult.Drop;
        }

        if (frame.SenderLength > 0)
            state.LastSpeechSenderLength = frame.SenderLength;

        destination.CopyFrom(frame);
        ReturnToFreeList(state, frame);
        state.FramesOut++;
        return PlayoutResult.Ok;
    }

    internal static PlayoutResult PutCore(
        PlayoutState state,
        object? data,
        int type,
        int senderLength,
        int senderStamp,
        int receiverStamp) {
        state.FramesIn++;

        PlayoutFrame frame;
        if (state.FreeFrames is not null) {
            frame = state.FreeFrames;
            state.FreeFrames = frame.Later;
        } else {
            frame = new PlayoutFrame();
        }

        frame.Data = data;
        frame.Type = type;
        frame.SenderStamp = senderStamp;
        frame.SenderLength = senderLength;
        frame.ReceiverStamp = receiverStamp;
        frame.Earlier = null;
        frame.Later = null;

        if (state.LastFrame is null) {
            state.FirstFrame = frame;
            state.LastFrame = frame;
        } else if (senderStamp >= state.LastFrame.SenderStamp) {
            frame.Earlier = state.LastFrame;
            state.LastFrame.Later = frame;
            state.LastFrame = frame;
        } else {
            state.FramesOutOfSequence++;
            InsertOutOfSequence(state, frame);
        }

        if (state.Start && type == PLAYOUT_TYPE_SPEECH) {
            state.LastSpeechSenderStamp = unchecked(
                senderStamp - senderLength - state.MinimumLength);
            state.LastSpeechSenderLength = senderLength;
            state.Start = false;
        }

        return PlayoutResult.Ok;
    }

    private static PlayoutFrame? QueueGet(PlayoutState state, int senderStamp) {
        PlayoutFrame? frame = state.FirstFrame;
        if (frame is null || senderStamp < frame.SenderStamp)
            return null;

        if (frame.Later is not null) {
            frame.Later.Earlier = null;
            state.FirstFrame = frame.Later;
        } else {
            state.FirstFrame = null;
            state.LastFrame = null;
        }

        frame.Earlier = null;
        return frame;
    }

    private static void ReturnToFreeList(
        PlayoutState state,
        PlayoutFrame frame) {
        frame.Earlier = null;
        frame.Later = state.FreeFrames;
        state.FreeFrames = frame;
    }

    private static void InsertOutOfSequence(
        PlayoutState state,
        PlayoutFrame frame) {
        PlayoutFrame current = state.LastFrame!;

        while (current.Earlier is not null
            && frame.SenderStamp < current.SenderStamp) {
            current = current.Earlier;
        }

        if (frame.SenderStamp < current.SenderStamp) {
            frame.Later = current;
            frame.Earlier = current.Earlier;

            if (current.Earlier is not null)
                current.Earlier.Later = frame;
            else
                state.FirstFrame = frame;

            current.Earlier = frame;
            return;
        }

        frame.Earlier = current;
        frame.Later = current.Later;

        if (current.Later is not null)
            current.Later.Earlier = frame;
        else
            state.LastFrame = frame;

        current.Later = frame;
    }

    private static void AdjustDynamicBuffer(
        PlayoutState state,
        PlayoutFrame frame) {
        if (!state.NotFirst) {
            state.NotFirst = true;
            state.LatestExpected = unchecked(
                frame.ReceiverStamp + state.MinimumLength);
        }

        int lateInput = frame.ReceiverStamp > state.LatestExpected
            ? 0x10000000
            : 0;
        state.StateLate += (lateInput - state.StateLate) >> 8;

        int justInTimeInput = frame.ReceiverStamp
            > state.LatestExpected - frame.SenderLength
                ? 0x10000000
                : 0;
        state.StateJustInTime +=
            (justInTimeInput - state.StateJustInTime) >> 8;

        state.LatestExpected = unchecked(
            state.LatestExpected + frame.SenderLength);

        if (state.StateLate > state.DropableThreshold) {
            if (state.SinceLastStep < 10) {
                if (state.TargetBufferLength < state.MaximumLength - 2) {
                    state.TargetBufferLength = unchecked(
                        state.TargetBufferLength + 3 * frame.SenderLength);
                    state.LatestExpected = unchecked(
                        state.LatestExpected + 3 * frame.SenderLength);
                    state.StateJustInTime = state.DropableThreshold;
                    state.StateLate = 0;
                    state.SinceLastStep = 0;
                    state.LastSpeechSenderStamp = unchecked(
                        state.LastSpeechSenderStamp
                        - 3 * state.LastSpeechSenderLength);
                }
            } else if (state.TargetBufferLength < state.MaximumLength) {
                state.TargetBufferLength = unchecked(
                    state.TargetBufferLength + frame.SenderLength);
                state.LatestExpected = unchecked(
                    state.LatestExpected + frame.SenderLength);
                state.StateJustInTime = state.DropableThreshold;
                state.StateLate = 0;
                state.SinceLastStep = 0;
                state.LastSpeechSenderStamp = unchecked(
                    state.LastSpeechSenderStamp
                    - state.LastSpeechSenderLength);
            }
        } else if (state.SinceLastStep > 500
              && state.StateJustInTime < state.DropableThreshold
              && state.TargetBufferLength > state.MinimumLength) {
            state.TargetBufferLength = unchecked(
                state.TargetBufferLength - frame.SenderLength);
            state.LatestExpected = unchecked(
                state.LatestExpected - frame.SenderLength);
            state.StateJustInTime = state.DropableThreshold;
            state.StateLate = 0;
            state.SinceLastStep = 0;
            state.LastSpeechSenderStamp = unchecked(
                state.LastSpeechSenderStamp
                + state.LastSpeechSenderLength);
        }

        state.SinceLastStep = unchecked(state.SinceLastStep + 1);
    }
}
