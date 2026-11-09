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

/// <summary>
/// Defines the available <c>PlayoutResult</c> values.
/// </summary>
public enum PlayoutResult {
    /// <summary>
    /// Represents the <c>Ok</c> value.
    /// </summary>
    Ok = 0,
    /// <summary>
    /// Represents the <c>Error</c> value.
    /// </summary>
    Error = 1,
    /// <summary>
    /// Represents the <c>Empty</c> value.
    /// </summary>
    Empty = 2,
    /// <summary>
    /// Represents the <c>NoFrame</c> value.
    /// </summary>
    NoFrame = 3,
    /// <summary>
    /// Represents the <c>FillIn</c> value.
    /// </summary>
    FillIn = 4,
    /// <summary>
    /// Represents the <c>Drop</c> value.
    /// </summary>
    Drop = 5
}

/// <summary>
/// Defines the available <c>PlayoutFrameType</c> values.
/// </summary>
public enum PlayoutFrameType {
    /// <summary>
    /// Represents the <c>Control</c> value.
    /// </summary>
    Control = 0,
    /// <summary>
    /// Represents the <c>Silence</c> value.
    /// </summary>
    Silence = 1,
    /// <summary>
    /// Represents the <c>Speech</c> value.
    /// </summary>
    Speech = 2
}

/// <summary>
/// Managed equivalent of <c>playout_frame_t</c>.
/// </summary>
public sealed class PlayoutFrame {
    /// <summary>
    /// Gets or sets the <c>Data</c> value.
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Gets or sets the <c>Type</c> value.
    /// </summary>
    public int Type { get; set; }

    /// <summary>
    /// Gets or sets the <c>SenderStamp</c> value.
    /// </summary>
    public int SenderStamp { get; set; }

    /// <summary>
    /// Gets or sets the <c>SenderLength</c> value.
    /// </summary>
    public int SenderLength { get; set; }

    /// <summary>
    /// Gets or sets the <c>ReceiverStamp</c> value.
    /// </summary>
    public int ReceiverStamp { get; set; }

    internal PlayoutFrame? Earlier { get; set; }

    internal PlayoutFrame? Later { get; set; }

    /// <summary>
    /// Performs the <c>CopyFrom</c> operation.
    /// </summary>
    /// <param name="source">The source value.</param>
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

    /// <summary>
    /// Initializes a new instance of the <c>PlayoutState</c> type.
    /// </summary>
    /// <param name="minimumLength">The minimum length value.</param>
    /// <param name="maximumLength">The maximum length value.</param>
    public PlayoutState(int minimumLength, int maximumLength) {
        Restart(minimumLength, maximumLength);
    }

    /// <summary>
    /// Gets or sets the <c>Dynamic</c> value.
    /// </summary>
    public bool Dynamic { get; internal set; }

    /// <summary>
    /// Gets or sets the <c>MinimumLength</c> value.
    /// </summary>
    public int MinimumLength { get; internal set; }

    /// <summary>
    /// Gets or sets the <c>MaximumLength</c> value.
    /// </summary>
    public int MaximumLength { get; internal set; }

    /// <summary>
    /// Gets or sets the <c>DropableThreshold</c> value.
    /// </summary>
    public int DropableThreshold { get; internal set; }

    /// <summary>
    /// Gets or sets the <c>Start</c> value.
    /// </summary>
    public bool Start { get; internal set; }

    internal PlayoutFrame? FirstFrame { get; set; }

    internal PlayoutFrame? LastFrame { get; set; }

    internal PlayoutFrame? FreeFrames { get; set; }

    /// <summary>
    /// Gets or sets the <c>FramesIn</c> value.
    /// </summary>
    public int FramesIn { get; internal set; }

    /// <summary>
    /// Gets or sets the <c>FramesOut</c> value.
    /// </summary>
    public int FramesOut { get; internal set; }

    /// <summary>
    /// Gets or sets the <c>FramesOutOfSequence</c> value.
    /// </summary>
    public int FramesOutOfSequence { get; internal set; }

    /// <summary>
    /// Gets or sets the <c>FramesLate</c> value.
    /// </summary>
    public int FramesLate { get; internal set; }

    /// <summary>
    /// Gets or sets the <c>FramesMissing</c> value.
    /// </summary>
    public int FramesMissing { get; internal set; }

    /// <summary>
    /// Gets or sets the <c>FramesTrimmed</c> value.
    /// </summary>
    public int FramesTrimmed { get; internal set; }

    /// <summary>
    /// Gets or sets the <c>LatestExpected</c> value.
    /// </summary>
    public int LatestExpected { get; internal set; }

    /// <summary>
    /// Gets or sets the <c>Current</c> value.
    /// </summary>
    public int Current { get; internal set; }

    /// <summary>
    /// Gets or sets the <c>LastSpeechSenderStamp</c> value.
    /// </summary>
    public int LastSpeechSenderStamp { get; internal set; }

    /// <summary>
    /// Gets or sets the <c>LastSpeechSenderLength</c> value.
    /// </summary>
    public int LastSpeechSenderLength { get; internal set; }

    /// <summary>
    /// Gets or sets the <c>NotFirst</c> value.
    /// </summary>
    public bool NotFirst { get; internal set; }

    /// <summary>
    /// Gets or sets the <c>SinceLastStep</c> value.
    /// </summary>
    public int SinceLastStep { get; internal set; }

    /// <summary>
    /// Gets or sets the <c>StateJustInTime</c> value.
    /// </summary>
    public int StateJustInTime { get; internal set; }

    /// <summary>
    /// Gets or sets the <c>StateLate</c> value.
    /// </summary>
    public int StateLate { get; internal set; }

    /// <summary>
    /// Gets or sets the <c>TargetBufferLength</c> value.
    /// </summary>
    public int TargetBufferLength { get; internal set; }

    /// <summary>
    /// Gets or sets the <c>ActualBufferLength</c> value.
    /// </summary>
    public int ActualBufferLength { get; internal set; }

    /// <summary>
    /// Gets the <c>IsDisposed</c> value.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Performs the <c>NextDue</c> operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public int NextDue() {
        ThrowIfDisposed();
        return unchecked(LastSpeechSenderStamp + LastSpeechSenderLength);
    }

    /// <summary>
    /// Performs the <c>CurrentLength</c> operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public int CurrentLength() {
        ThrowIfDisposed();
        return TargetBufferLength;
    }

    /// <summary>
    /// Performs the <c>GetUnconditional</c> operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public PlayoutFrame? GetUnconditional() {
        ThrowIfDisposed();
        return Playout.GetUnconditionalCore(this);
    }

    /// <summary>
    /// Performs the <c>Get</c> operation.
    /// </summary>
    /// <param name="destination">The destination value.</param>
    /// <param name="now">The now value.</param>
    /// <returns>The result of the operation.</returns>
    public PlayoutResult Get(PlayoutFrame destination, int now) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(destination);
        return Playout.GetCore(this, destination, now);
    }

    /// <summary>
    /// Performs the <c>Put</c> operation.
    /// </summary>
    /// <param name="data">The data value.</param>
    /// <param name="type">The type value.</param>
    /// <param name="senderLength">The sender length value.</param>
    /// <param name="senderStamp">The sender stamp value.</param>
    /// <param name="receiverStamp">The receiver stamp value.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Performs the <c>Restart</c> operation.
    /// </summary>
    /// <param name="minimumLength">The minimum length value.</param>
    /// <param name="maximumLength">The maximum length value.</param>
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

    /// <summary>
    /// Performs the <c>Free</c> operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public int Free() {
        Dispose();
        return 0;
    }

    /// <summary>
    /// Performs the <c>Dispose</c> operation.
    /// </summary>
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
    /// <summary>
    /// Defines the <c>PLAYOUT_OK</c> constant.
    /// </summary>
    public const int PLAYOUT_OK = (int)PlayoutResult.Ok;
    /// <summary>
    /// Defines the <c>PLAYOUT_ERROR</c> constant.
    /// </summary>
    public const int PLAYOUT_ERROR = (int)PlayoutResult.Error;
    /// <summary>
    /// Defines the <c>PLAYOUT_EMPTY</c> constant.
    /// </summary>
    public const int PLAYOUT_EMPTY = (int)PlayoutResult.Empty;
    /// <summary>
    /// Defines the <c>PLAYOUT_NOFRAME</c> constant.
    /// </summary>
    public const int PLAYOUT_NOFRAME = (int)PlayoutResult.NoFrame;
    /// <summary>
    /// Defines the <c>PLAYOUT_FILLIN</c> constant.
    /// </summary>
    public const int PLAYOUT_FILLIN = (int)PlayoutResult.FillIn;
    /// <summary>
    /// Defines the <c>PLAYOUT_DROP</c> constant.
    /// </summary>
    public const int PLAYOUT_DROP = (int)PlayoutResult.Drop;

    /// <summary>
    /// Defines the <c>PLAYOUT_TYPE_CONTROL</c> constant.
    /// </summary>
    public const int PLAYOUT_TYPE_CONTROL = (int)PlayoutFrameType.Control;
    /// <summary>
    /// Defines the <c>PLAYOUT_TYPE_SILENCE</c> constant.
    /// </summary>
    public const int PLAYOUT_TYPE_SILENCE = (int)PlayoutFrameType.Silence;
    /// <summary>
    /// Defines the <c>PLAYOUT_TYPE_SPEECH</c> constant.
    /// </summary>
    public const int PLAYOUT_TYPE_SPEECH = (int)PlayoutFrameType.Speech;

    /// <summary>
    /// Performs the <c>playout_next_due</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int playout_next_due(PlayoutState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.NextDue();
    }

    /// <summary>
    /// Performs the <c>playout_current_length</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int playout_current_length(PlayoutState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.CurrentLength();
    }

    /// <summary>
    /// Performs the <c>playout_get_unconditional</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static PlayoutFrame? playout_get_unconditional(PlayoutState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetUnconditional();
    }

    /// <summary>
    /// Performs the <c>playout_get</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="frame">The frame value.</param>
    /// <param name="now">The now value.</param>
    /// <returns>The result of the operation.</returns>
    public static int playout_get(
        PlayoutState state,
        PlayoutFrame frame,
        int now) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(frame);
        return (int)state.Get(frame, now);
    }

    /// <summary>
    /// Performs the <c>playout_put</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="data">The data value.</param>
    /// <param name="type">The type value.</param>
    /// <param name="senderLength">The sender length value.</param>
    /// <param name="senderStamp">The sender stamp value.</param>
    /// <param name="receiverStamp">The receiver stamp value.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Performs the <c>playout_restart</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="minimumLength">The minimum length value.</param>
    /// <param name="maximumLength">The maximum length value.</param>
    public static void playout_restart(
        PlayoutState state,
        int minimumLength,
        int maximumLength) {
        ArgumentNullException.ThrowIfNull(state);
        state.Restart(minimumLength, maximumLength);
    }

    /// <summary>
    /// Performs the <c>playout_init</c> operation.
    /// </summary>
    /// <param name="minimumLength">The minimum length value.</param>
    /// <param name="maximumLength">The maximum length value.</param>
    /// <returns>The result of the operation.</returns>
    public static PlayoutState playout_init(
        int minimumLength,
        int maximumLength) {
        return new PlayoutState(minimumLength, maximumLength);
    }

    /// <summary>
    /// Performs the <c>playout_release</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int playout_release(PlayoutState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    /// <summary>
    /// Performs the <c>playout_free</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
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
