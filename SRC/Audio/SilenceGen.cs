/*
 * TKFaxEngine - managed C# port
 *
 * SilenceGen.cs
 *
 * Combined port of:
 *   silence_gen.h
 *   private/silence_gen.h (merged into the supplied header)
 *   silence_gen.c
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2006 Steve Underwood.
 *
 * This port preserves the GNU Lesser General Public License version 2.1
 * licensing terms of the original source files.
 */

#nullable enable

namespace TKFaxEngine.Audio;

/// <summary>
/// Status callback used by the silence generator. The status value uses the
/// native TKFaxEngine <c>SIG_STATUS_*</c> values.
/// </summary>
public delegate void SilenceGenStatusHandler(object? userData, int status);

/// <summary>
/// Managed equivalent of <c>silence_gen_state_t</c>.
/// </summary>
public sealed class SilenceGenState : IDisposable {
    private bool _disposed;

    /// <summary>
    /// Creates a timed silence generator.
    /// </summary>
    /// <param name="silentSamples">Initial number of silent samples.</param>
    public SilenceGenState(int silentSamples = 0) {
        Initialize(silentSamples);
    }

    /// <summary>
    /// Optional callback invoked when the final timed silence block is emitted.
    /// </summary>
    public SilenceGenStatusHandler? StatusHandler { get; private set; }

    /// <summary>
    /// Opaque value passed to <see cref="StatusHandler"/>.
    /// </summary>
    public object? StatusUserData { get; private set; }

    /// <summary>
    /// Number of silent samples still to generate. <see cref="int.MaxValue"/>
    /// represents continuous silence, matching the native implementation.
    /// </summary>
    public int RemainingSamples { get; private set; }

    /// <summary>
    /// Total number of samples generated or currently accounted for after an
    /// alteration of the configured duration.
    /// </summary>
    public int TotalSamples { get; private set; }

    public bool IsDisposed => _disposed;

    /// <summary>
    /// Reinitializes the state as <c>silence_gen_init()</c> does. Existing
    /// callback settings are cleared.
    /// </summary>
    public void Initialize(int silentSamples) {
        ValidateSilentSamples(silentSamples);

        StatusHandler = null;
        StatusUserData = null;
        RemainingSamples = silentSamples;
        TotalSamples = 0;
        _disposed = false;
    }

    /// <summary>
    /// Generates up to <paramref name="maximumLength"/> zero-valued PCM samples.
    /// </summary>
    public int Generate(Span<short> destination, int maximumLength) {
        ThrowIfDisposed();

        if (maximumLength < 0 || maximumLength > destination.Length) {
            throw new ArgumentOutOfRangeException(
                nameof(maximumLength),
                maximumLength,
                "The requested sample count must fit in the destination buffer.");
        }

        int generated = maximumLength;

        if (RemainingSamples != int.MaxValue) {
            if (generated >= RemainingSamples) {
                generated = RemainingSamples;

                if (generated != 0) {
                    StatusHandler?.Invoke(
                        StatusUserData,
                        SilenceGen.ShutdownCompleteStatus);
                }
            }

            RemainingSamples -= generated;
        }

        if (int.MaxValue - TotalSamples >= generated)
            TotalSamples += generated;

        destination[..generated].Clear();
        return generated;
    }

    /// <summary>
    /// Generates silence across the complete destination span.
    /// </summary>
    public int Generate(Span<short> destination) {
        return Generate(destination, destination.Length);
    }

    /// <summary>
    /// Selects continuous silence output.
    /// </summary>
    public void Always() {
        ThrowIfDisposed();
        RemainingSamples = int.MaxValue;
    }

    /// <summary>
    /// Replaces the current silence duration and resets the generated count.
    /// </summary>
    public void Set(int silentSamples) {
        ThrowIfDisposed();
        ValidateSilentSamples(silentSamples);

        RemainingSamples = silentSamples;
        TotalSamples = 0;
    }

    /// <summary>
    /// Changes the configured duration. A negative value cannot reduce the
    /// remaining duration below zero.
    /// </summary>
    public void Alter(int silentSamples) {
        ThrowIfDisposed();

        int adjustment = silentSamples;

        if (adjustment < 0 && -(long)adjustment > RemainingSamples)
            adjustment = -RemainingSamples;

        unchecked {
            RemainingSamples += adjustment;
            TotalSamples += adjustment;
        }
    }

    public int Remainder() {
        ThrowIfDisposed();
        return RemainingSamples;
    }

    public int Generated() {
        ThrowIfDisposed();
        return TotalSamples;
    }

    public void SetStatusHandler(
        SilenceGenStatusHandler? handler,
        object? userData) {
        ThrowIfDisposed();
        StatusHandler = handler;
        StatusUserData = userData;
    }

    /// <summary>
    /// Managed equivalent of <c>silence_gen_release()</c>. The native state
    /// owns no external resource, so release is intentionally a no-op.
    /// </summary>
    public int Release() {
        ThrowIfDisposed();
        return 0;
    }

    /// <summary>
    /// Managed equivalent of <c>silence_gen_free()</c>.
    /// </summary>
    public int Free() {
        Dispose();
        return 0;
    }

    public void Dispose() {
        if (_disposed)
            return;

        StatusHandler = null;
        StatusUserData = null;
        RemainingSamples = 0;
        TotalSamples = 0;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed() {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SilenceGenState));
    }

    private static void ValidateSilentSamples(int silentSamples) {
        if (silentSamples < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(silentSamples),
                silentSamples,
                "The silence duration cannot be negative.");
        }
    }
}

/// <summary>
/// C-compatible facade for the original silence generator API.
/// </summary>
public static class SilenceGen {
    /// <summary>Native <c>SIG_STATUS_SHUTDOWN_COMPLETE</c> value.</summary>
    public const int ShutdownCompleteStatus = -10;

    public static int silence_gen(
        SilenceGenState state,
        Span<short> amplitude,
        int maximumLength) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Generate(amplitude, maximumLength);
    }

    public static int silence_gen(
        SilenceGenState state,
        short[] amplitude,
        int maximumLength) {
        ArgumentNullException.ThrowIfNull(amplitude);
        return silence_gen(state, amplitude.AsSpan(), maximumLength);
    }

    public static void silence_gen_always(SilenceGenState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.Always();
    }

    public static void silence_gen_set(
        SilenceGenState state,
        int silentSamples) {
        ArgumentNullException.ThrowIfNull(state);
        state.Set(silentSamples);
    }

    public static void silence_gen_alter(
        SilenceGenState state,
        int silentSamples) {
        ArgumentNullException.ThrowIfNull(state);
        state.Alter(silentSamples);
    }

    public static int silence_gen_remainder(SilenceGenState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Remainder();
    }

    public static int silence_gen_generated(SilenceGenState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Generated();
    }

    public static void silence_gen_status_handler(
        SilenceGenState state,
        SilenceGenStatusHandler? handler,
        object? userData) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetStatusHandler(handler, userData);
    }

    public static SilenceGenState silence_gen_init(
        SilenceGenState? state,
        int silentSamples) {
        if (state is null)
            return new SilenceGenState(silentSamples);

        state.Initialize(silentSamples);
        return state;
    }

    public static int silence_gen_release(SilenceGenState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    public static int silence_gen_free(SilenceGenState? state) {
        state?.Dispose();
        return 0;
    }

    /// <summary>
    /// Dummy receive callback which consumes and ignores PCM samples.
    /// </summary>
    public static int span_dummy_rx(
        object? userData,
        ReadOnlySpan<short> amplitude,
        int length) {
        _ = userData;
        _ = amplitude;
        _ = length;
        return 0;
    }

    public static int span_dummy_rx(
        object? userData,
        short[] amplitude,
        int length) {
        ArgumentNullException.ThrowIfNull(amplitude);
        return span_dummy_rx(userData, amplitude.AsSpan(), length);
    }

    /// <summary>
    /// Dummy modifier callback which leaves the supplied samples unchanged.
    /// </summary>
    public static int span_dummy_mod(
        object? userData,
        Span<short> amplitude,
        int length) {
        _ = userData;
        _ = amplitude;
        return length;
    }

    public static int span_dummy_mod(
        object? userData,
        short[] amplitude,
        int length) {
        ArgumentNullException.ThrowIfNull(amplitude);
        return span_dummy_mod(userData, amplitude.AsSpan(), length);
    }

    /// <summary>
    /// Dummy receive fill-in callback which ignores the missing-sample count.
    /// </summary>
    public static int span_dummy_rx_fillin(object? userData, int length) {
        _ = userData;
        _ = length;
        return 0;
    }
}
