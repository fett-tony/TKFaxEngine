/*
 * TKFaxEngine - managed C# port
 *
 * Biquad.cs
 *
 * Direct managed port of Audio/biquad.h.
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2001 Steve Underwood.
 *
 * This port preserves the GNU Lesser General Public License version 2.1
 * licensing terms of the original source file.
 */

#nullable enable

namespace TKFaxEngine.Audio;

/// <summary>Managed equivalent of <c>biquad2_state_t</c>.</summary>
public sealed class Biquad2State {
    /// <summary>
    /// Gets or sets the <c>Gain</c> value.
    /// </summary>
    public int Gain { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>A1</c> value.
    /// </summary>
    public int A1 { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>A2</c> value.
    /// </summary>
    public int A2 { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>B1</c> value.
    /// </summary>
    public int B1 { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>B2</c> value.
    /// </summary>
    public int B2 { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>Z1</c> value.
    /// </summary>
    public int Z1 { get; internal set; }
    /// <summary>
    /// Gets or sets the <c>Z2</c> value.
    /// </summary>
    public int Z2 { get; internal set; }

#if FIRST_ORDER_NOISE_SHAPING
    public int Residue { get; internal set; }
#elif SECOND_ORDER_NOISE_SHAPING
    public int Residue1 { get; internal set; }
    public int Residue2 { get; internal set; }
#endif
}

/// <summary>General telephony canonical/type-2 biquad section.</summary>
public static class Biquad {
    /// <summary>
    /// Performs the <c>biquad2_init</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="gain">The gain value.</param>
    /// <param name="a1">The a1 value.</param>
    /// <param name="a2">The a2 value.</param>
    /// <param name="b1">The b1 value.</param>
    /// <param name="b2">The b2 value.</param>
    public static void biquad2_init(
        Biquad2State state,
        int gain,
        int a1,
        int a2,
        int b1,
        int b2) {
        ArgumentNullException.ThrowIfNull(state);

        state.Gain = gain;
        state.A1 = a1;
        state.A2 = a2;
        state.B1 = b1;
        state.B2 = b2;
        state.Z1 = 0;
        state.Z2 = 0;

#if FIRST_ORDER_NOISE_SHAPING
        state.Residue = 0;
#elif SECOND_ORDER_NOISE_SHAPING
        state.Residue1 = 0;
        state.Residue2 = 0;
#endif
    }

    /// <summary>
    /// Performs the <c>biquad2</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="sample">The sample value.</param>
    /// <returns>The result of the operation.</returns>
    public static short biquad2(Biquad2State state, short sample) {
        ArgumentNullException.ThrowIfNull(state);

        int z0 = unchecked(
            sample * state.Gain +
            state.Z1 * state.A1 +
            state.Z2 * state.A2);

        int y = unchecked(
            z0 +
            state.Z1 * state.B1 +
            state.Z2 * state.B2);

        state.Z2 = state.Z1;
        state.Z1 = z0 >> 15;

#if FIRST_ORDER_NOISE_SHAPING
        y = unchecked(y + state.Residue);
        state.Residue = y & 0x7FFF;
#elif SECOND_ORDER_NOISE_SHAPING
        y = unchecked(y + (2 * state.Residue1 - state.Residue2));
        state.Residue2 = state.Residue1;
        state.Residue1 = y & 0x7FFF;
#endif

        y >>= 15;
        return unchecked((short)y);
    }
}
