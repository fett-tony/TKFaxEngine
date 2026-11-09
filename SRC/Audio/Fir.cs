/*
 * TKFaxEngine - managed C# port
 *
 * Fir.cs
 *
 * Direct managed scalar port of Audio/fir.h.
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2002 Steve Underwood.
 *
 * The native project does not enable USE_MMX or USE_SSE2 for this header,
 * so the active scalar spanDSP paths are reproduced here.
 *
 * This port preserves the GNU Lesser General Public License version 2.1
 * licensing terms of the original source file.
 */

#nullable enable

namespace TKFaxEngine.Audio;

/// <summary>Managed equivalent of <c>fir16_state_t</c>.</summary>
public sealed class Fir16State {
    /// <summary>
    /// Gets or sets the <c>Taps</c> value.
    /// </summary>
    public int Taps { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>CurrentPosition</c> value.
    /// </summary>
    public int CurrentPosition { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>Coefficients</c> value.
    /// </summary>
    public short[] Coefficients { get; internal set; } = Array.Empty<short>();
    /// <summary>
    /// Gets or sets the <c>History</c> value.
    /// </summary>
    public short[] History { get; internal set; } = Array.Empty<short>();
}

/// <summary>Managed equivalent of <c>fir32_state_t</c>.</summary>
public sealed class Fir32State {
    /// <summary>
    /// Gets or sets the <c>Taps</c> value.
    /// </summary>
    public int Taps { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>CurrentPosition</c> value.
    /// </summary>
    public int CurrentPosition { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>Coefficients</c> value.
    /// </summary>
    public int[] Coefficients { get; internal set; } = Array.Empty<int>();
    /// <summary>
    /// Gets or sets the <c>History</c> value.
    /// </summary>
    public short[] History { get; internal set; } = Array.Empty<short>();
}

/// <summary>Managed equivalent of <c>fir_float_state_t</c>.</summary>
public sealed class FirFloatState {
    /// <summary>
    /// Gets or sets the <c>Taps</c> value.
    /// </summary>
    public int Taps { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>CurrentPosition</c> value.
    /// </summary>
    public int CurrentPosition { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>Coefficients</c> value.
    /// </summary>
    public float[] Coefficients { get; internal set; } = Array.Empty<float>();
    /// <summary>
    /// Gets or sets the <c>History</c> value.
    /// </summary>
    public float[] History { get; internal set; } = Array.Empty<float>();
}

/// <summary>General telephony finite impulse response filters.</summary>
public static class Fir {
    /// <summary>
    /// Performs the <c>fir16_create</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="coefficients">The coefficients value.</param>
    /// <param name="taps">The taps value.</param>
    /// <returns>The result of the operation.</returns>
    public static short[] fir16_create(
        Fir16State state,
        short[] coefficients,
        int taps) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(coefficients);

        state.Taps = taps;
        state.CurrentPosition = taps - 1;
        state.Coefficients = coefficients;
        state.History = new short[taps];
        return state.History;
    }

    /// <summary>
    /// Performs the <c>fir16_flush</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    public static void fir16_flush(Fir16State state) {
        ArgumentNullException.ThrowIfNull(state);
        Array.Clear(state.History);
    }

    /// <summary>
    /// Performs the <c>fir16_free</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    public static void fir16_free(Fir16State state) {
        ArgumentNullException.ThrowIfNull(state);
        state.History = Array.Empty<short>();
    }

    /// <summary>
    /// Performs the <c>fir16</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="sample">The sample value.</param>
    /// <returns>The result of the operation.</returns>
    public static short fir16(Fir16State state, short sample) {
        ArgumentNullException.ThrowIfNull(state);

        int currentPosition = state.CurrentPosition;
        state.History[currentPosition] = sample;

        int offset2 = currentPosition;
        int offset1 = state.Taps - offset2;
        int y = 0;
        int index;

        for (index = state.Taps - 1; index >= offset1; index--) {
            y = unchecked(
                y +
                state.Coefficients[index] *
                state.History[index - offset1]);
        }

        for (; index >= 0; index--) {
            y = unchecked(
                y +
                state.Coefficients[index] *
                state.History[index + offset2]);
        }

        if (currentPosition <= 0)
            currentPosition = state.Taps;

        state.CurrentPosition = currentPosition - 1;
        return unchecked((short)(y >> 15));
    }

    /// <summary>
    /// Performs the <c>fir32_create</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="coefficients">The coefficients value.</param>
    /// <param name="taps">The taps value.</param>
    /// <returns>The result of the operation.</returns>
    public static short[] fir32_create(
        Fir32State state,
        int[] coefficients,
        int taps) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(coefficients);

        state.Taps = taps;
        state.CurrentPosition = taps - 1;
        state.Coefficients = coefficients;
        state.History = new short[taps];
        return state.History;
    }

    /// <summary>
    /// Performs the <c>fir32_flush</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    public static void fir32_flush(Fir32State state) {
        ArgumentNullException.ThrowIfNull(state);
        Array.Clear(state.History);
    }

    /// <summary>
    /// Performs the <c>fir32_free</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    public static void fir32_free(Fir32State state) {
        ArgumentNullException.ThrowIfNull(state);
        state.History = Array.Empty<short>();
    }

    /// <summary>
    /// Performs the <c>fir32</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="sample">The sample value.</param>
    /// <returns>The result of the operation.</returns>
    public static short fir32(Fir32State state, short sample) {
        ArgumentNullException.ThrowIfNull(state);

        int currentPosition = state.CurrentPosition;
        state.History[currentPosition] = sample;

        int offset2 = currentPosition;
        int offset1 = state.Taps - offset2;
        int y = 0;
        int index;

        for (index = state.Taps - 1; index >= offset1; index--) {
            y = unchecked(
                y +
                state.Coefficients[index] *
                state.History[index - offset1]);
        }

        for (; index >= 0; index--) {
            y = unchecked(
                y +
                state.Coefficients[index] *
                state.History[index + offset2]);
        }

        if (currentPosition <= 0)
            currentPosition = state.Taps;

        state.CurrentPosition = currentPosition - 1;
        return unchecked((short)(y >> 15));
    }

    /// <summary>
    /// Performs the <c>fir_float_create</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="coefficients">The coefficients value.</param>
    /// <param name="taps">The taps value.</param>
    /// <returns>The result of the operation.</returns>
    public static float[]? fir_float_create(
        FirFloatState? state,
        float[] coefficients,
        int taps) {
        if (state is null)
            return null;

        ArgumentNullException.ThrowIfNull(coefficients);

        state.Taps = taps;
        state.CurrentPosition = taps - 1;
        state.Coefficients = coefficients;
        state.History = new float[taps];
        return state.History;
    }

    /// <summary>
    /// Performs the <c>fir_float_free</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    public static void fir_float_free(FirFloatState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.History = Array.Empty<float>();
    }

    /// <summary>
    /// Performs the <c>fir_float</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="sample">The sample value.</param>
    /// <returns>The result of the operation.</returns>
    public static short fir_float(FirFloatState state, short sample) {
        ArgumentNullException.ThrowIfNull(state);

        int currentPosition = state.CurrentPosition;
        state.History[currentPosition] = sample;

        int offset2 = currentPosition;
        int offset1 = state.Taps - offset2;
        float y = 0.0f;
        int index;

        for (index = state.Taps - 1; index >= offset1; index--) {
            y +=
                state.Coefficients[index] *
                state.History[index - offset1];
        }

        for (; index >= 0; index--) {
            y +=
                state.Coefficients[index] *
                state.History[index + offset2];
        }

        if (currentPosition <= 0)
            currentPosition = state.Taps;

        state.CurrentPosition = currentPosition - 1;
        return unchecked((short)y);
    }
}
