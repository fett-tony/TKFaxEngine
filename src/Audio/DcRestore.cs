/*
 * TKFaxEngine - managed C# port
 *
 * DcRestore.cs
 *
 * Direct managed port of Audio/dc_restore.h.
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2001 Steve Underwood.
 *
 * This port preserves the GNU Lesser General Public License version 2.1
 * licensing terms of the original source file.
 */

#nullable enable

namespace TKFaxEngine.Audio;

/// <summary>Managed equivalent of <c>dc_restore_state_t</c>.</summary>
public sealed class DcRestoreState {
    public int State { get; internal set; }
}

/// <summary>Leaky-integrator DC restoration for telephony audio.</summary>
public static class DcRestore {
    public static void dc_restore_init(DcRestoreState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.State = 0;
    }

    public static short dc_restore(DcRestoreState state, short sample) {
        ArgumentNullException.ThrowIfNull(state);

        state.State = unchecked(
            state.State +
            ((((int)sample << 15) - state.State) >> 14));

        return unchecked((short)(sample - (state.State >> 15)));
    }

    public static short dc_restore_estimate(DcRestoreState state) {
        ArgumentNullException.ThrowIfNull(state);
        return unchecked((short)(state.State >> 15));
    }
}
