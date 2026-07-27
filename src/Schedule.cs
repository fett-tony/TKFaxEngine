/*
 * TKFaxEngine - managed C# port
 *
 * Schedule.cs
 *
 * Combined port of:
 *   schedule.h
 *   private/schedule.h
 *   schedule.c
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2004 Steve Underwood.
 *
 * This port preserves the LGPL-2.1 licensing terms of the original files.
 */

namespace TKFaxEngine;

/// <summary>
/// Callback for a scheduled event. This corresponds to
/// <c>span_sched_callback_func_t</c>.
/// </summary>
public delegate void SpanScheduleCallback(
    SpanScheduleState state,
    object? userData);

public enum SpanScheduleLogLevel {
    None = 0,
    Warning = 1
}

public delegate void SpanScheduleLogHandler(
    SpanScheduleLogLevel level,
    string protocol,
    string message);

/// <summary>
/// Minimal managed logging state used by the scheduler.
/// The native scheduler only emits a warning when an invalid event ID is
/// deleted.
/// </summary>
public sealed class SpanScheduleLogger {
    public string Protocol { get; set; } = "SCHEDULE";

    public SpanScheduleLogLevel Level { get; set; } =
        SpanScheduleLogLevel.None;

    public SpanScheduleLogHandler? Handler { get; set; }

    internal void Warning(string message) {
        if (Handler is null ||
            Level == SpanScheduleLogLevel.None ||
            (int)SpanScheduleLogLevel.Warning < (int)Level) {
            return;
        }

        Handler(
            SpanScheduleLogLevel.Warning,
            Protocol,
            message);
    }
}

/// <summary>
/// One scheduled event entry. This is the managed equivalent of
/// <c>span_sched_t</c>.
/// </summary>
internal struct SpanScheduledEvent {
    public ulong When;

    public SpanScheduleCallback? Callback;

    public object? UserData;
}

/// <summary>
/// Managed event scheduler corresponding to <c>span_sched_state_t</c>.
/// Time is expressed in microseconds, as in the native implementation.
/// </summary>
/// <remarks>
/// The scheduler intentionally keeps the original slot-based behavior:
/// <list type="bullet">
/// <item><description>event IDs are array-slot indexes;</description></item>
/// <item><description>free slots are reused from the lowest index;</description></item>
/// <item><description>capacity grows in blocks of five entries;</description></item>
/// <item><description>due callbacks run in ascending slot order;</description></item>
/// <item><description>the scheduler is not thread-safe.</description></item>
/// </list>
/// </remarks>
public sealed class SpanScheduleState : IDisposable {
    private const int AllocationIncrement = 5;

    private SpanScheduledEvent[] _events =
        Array.Empty<SpanScheduledEvent>();

    private int _maxToDate;
    private bool _disposed;

    public SpanScheduleState() {
        Initialize();
    }

    /// <summary>
    /// Current scheduler time in microseconds.
    /// </summary>
    public ulong Ticker { get; private set; }

    /// <summary>
    /// Current allocated slot count.
    /// </summary>
    public int Allocated => _events.Length;

    /// <summary>
    /// Highest slot range ever used since initialization.
    /// </summary>
    public int MaximumSlotCount => _maxToDate;

    public bool IsDisposed => _disposed;

    public SpanScheduleLogger Logging { get; } = new();

    /// <summary>
    /// Reinitializes the scheduler, corresponding to
    /// <c>span_schedule_init()</c>.
    /// </summary>
    public void Initialize() {
        _events = Array.Empty<SpanScheduledEvent>();
        _maxToDate = 0;
        Ticker = 0;

        Logging.Level = SpanScheduleLogLevel.None;
        Logging.Protocol = "SCHEDULE";
        Logging.Handler = null;

        _disposed = false;
    }

    /// <summary>
    /// Adds an event relative to the current ticker and returns its slot ID.
    /// This corresponds to <c>span_schedule_event()</c>.
    /// </summary>
    /// <param name="microseconds">
    /// Relative delay in microseconds. The native unsigned arithmetic is
    /// preserved, including wraparound for negative values or overflow.
    /// </param>
    /// <param name="callback">
    /// Event callback. A null callback is accepted for exact compatibility
    /// with the C function, although such an entry is immediately considered
    /// unused.
    /// </param>
    public int ScheduleEvent(
        int microseconds,
        SpanScheduleCallback? callback,
        object? userData) {
        ThrowIfDisposed();

        int slot;

        for (slot = 0; slot < _maxToDate; slot++) {
            if (_events[slot].Callback is null)
                break;
        }

        if (slot >= _events.Length) {
            int newLength = checked(
                _events.Length + AllocationIncrement);

            Array.Resize(ref _events, newLength);
        }

        if (slot >= _maxToDate)
            _maxToDate = slot + 1;

        _events[slot].When =
            AddNativeUnsigned(Ticker, microseconds);

        _events[slot].Callback = callback;
        _events[slot].UserData = userData;

        return slot;
    }

    /// <summary>
    /// Returns the absolute time of the next active event, or
    /// <see cref="ulong.MaxValue"/> when no event is active.
    /// </summary>
    public ulong Next() {
        ThrowIfDisposed();

        ulong earliest = ulong.MaxValue;

        for (int slot = 0; slot < _maxToDate; slot++) {
            ref SpanScheduledEvent scheduledEvent =
                ref _events[slot];

            if (scheduledEvent.Callback is not null &&
                earliest > scheduledEvent.When) {
                earliest = scheduledEvent.When;
            }
        }

        return earliest;
    }

    /// <summary>
    /// Returns the current scheduler time.
    /// </summary>
    public ulong Time() {
        ThrowIfDisposed();
        return Ticker;
    }

    /// <summary>
    /// Advances the ticker and runs all events whose absolute time is less
    /// than or equal to the resulting ticker.
    /// </summary>
    /// <remarks>
    /// The callback and user-data fields are cleared before invocation. This
    /// matches the native code and allows a callback to reuse its own slot.
    /// Events created by a callback may run during the same update when they
    /// occupy a later slot and are already due.
    /// </remarks>
    public void Update(int microseconds) {
        ThrowIfDisposed();

        Ticker = AddNativeUnsigned(
            Ticker,
            microseconds);

        for (int slot = 0; slot < _maxToDate; slot++) {
            SpanScheduleCallback? callback =
                _events[slot].Callback;

            if (callback is null ||
                _events[slot].When > Ticker) {
                continue;
            }

            object? userData =
                _events[slot].UserData;

            _events[slot].Callback = null;
            _events[slot].UserData = null;

            callback(this, userData);
        }
    }

    /// <summary>
    /// Deletes an active event by slot ID.
    /// Invalid IDs produce the same warning as the native implementation.
    /// </summary>
    public void Delete(int id) {
        ThrowIfDisposed();

        if (id < 0 ||
            id >= _maxToDate ||
            _events[id].Callback is null) {
            Logging.Warning(
                $"Requested to delete invalid scheduled ID {id} ?");

            return;
        }

        _events[id].Callback = null;
    }

    /// <summary>
    /// Returns whether the supplied event ID currently contains an active
    /// callback.
    /// </summary>
    public bool IsActive(int id) {
        ThrowIfDisposed();

        return id >= 0 &&
               id < _maxToDate &&
               _events[id].Callback is not null;
    }

    /// <summary>
    /// Releases all scheduled entries. The managed state can be reused by
    /// calling <see cref="Initialize"/>.
    /// </summary>
    public int Release() {
        if (_disposed)
            return 0;

        Array.Clear(_events);
        _events = Array.Empty<SpanScheduledEvent>();
        _maxToDate = 0;

        return 0;
    }

    public void Dispose() {
        if (_disposed)
            return;

        Release();
        Ticker = 0;
        _disposed = true;
    }

    private static ulong AddNativeUnsigned(
        ulong current,
        int microseconds) {
        // In C, uint64_t + int converts the int to uint64_t first. The
        // unchecked cast reproduces that modulo-2^64 behavior.
        return unchecked(
            current + (ulong)(long)microseconds);
    }

    private void ThrowIfDisposed() {
        if (_disposed) {
            throw new ObjectDisposedException(
                nameof(SpanScheduleState));
        }
    }
}

/// <summary>
/// Compatibility facade retaining the original C function names.
/// </summary>
public static class ScheduleApi {
    public static ulong span_schedule_next(
        SpanScheduleState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Next();
    }

    public static ulong span_schedule_time(
        SpanScheduleState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Time();
    }

    public static int span_schedule_event(
        SpanScheduleState state,
        int us,
        SpanScheduleCallback? function,
        object? userData) {
        ArgumentNullException.ThrowIfNull(state);

        return state.ScheduleEvent(
            us,
            function,
            userData);
    }

    public static void span_schedule_update(
        SpanScheduleState state,
        int us) {
        ArgumentNullException.ThrowIfNull(state);
        state.Update(us);
    }

    public static void span_schedule_del(
        SpanScheduleState state,
        int id) {
        ArgumentNullException.ThrowIfNull(state);
        state.Delete(id);
    }

    public static SpanScheduleState span_schedule_init(
        SpanScheduleState? state) {
        state ??= new SpanScheduleState();
        state.Initialize();
        return state;
    }

    public static int span_schedule_release(
        SpanScheduleState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    public static int span_schedule_free(
        SpanScheduleState? state) {
        if (state is null)
            return 0;

        int result = state.Release();
        state.Dispose();
        return result;
    }
}
