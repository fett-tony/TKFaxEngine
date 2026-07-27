/*
 * TKFaxEngineFX - a series of DSP components for telephony
 *
 * v32bis.cs - ITU V.32bis modem
 *
 * Direct managed C# port of:
 *   v32bis.h
 *   private/v32bis.h
 *   v32bis.c
 *
 * Written by Steve Underwood <steveu@coppice.org>
 *
 * Copyright (C) 2008 Steve Underwood
 *
 * All rights reserved.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License version 2.1,
 * as published by the Free Software Foundation.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this program; if not, write to the Free Software
 * Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.
 */

/* V.32bis SUPPORT IS A WORK IN PROGRESS - NOT YET FUNCTIONAL! */

#nullable enable

using global::TKFaxEngine.Modem.V17;
using static global::TKFaxEngine.LoggingApi;
using static global::TKFaxEngine.Modem.V17.V17RxApi;
using static global::TKFaxEngine.Modem.V17.V17TxApi;

namespace TKFaxEngine.Modem.V32;

/// <summary>
/// Managed equivalent of span_get_bit_func_t/get_bit_func_t.
/// </summary>
public delegate int V32BisGetBitDelegate(object? userData);

/// <summary>
/// Direct V.17 transmitter surface already implemented by V17TxState.
/// This interface exists only because V17TxState exposes the same native
/// operations to the V.32bis module; no factory, adapter or wrapper is used.
/// </summary>
public interface IV32BisV17Transmitter : IDisposable {
    int ScramblerTap { get; set; }
    int Transmit(Span<short> samples);
    int Restart(int bitRate, bool useTep, bool shortTrain);
    void SetPower(float powerDbm0);
    void SetGetBit(V32BisGetBitDelegate? getBit, object? userData);
}

/// <summary>
/// Managed equivalent of v32bis_state_t.
/// Field layout and ownership follow private/v32bis.h.
/// </summary>
public sealed class v32bis_state_t : IDisposable {
    /// <summary>
    /// The bit rate of the modem.
    /// </summary>
    public int bit_rate;

    /// <summary>
    /// True if this is the calling-side modem.
    /// </summary>
    public bool calling_party;
    public V17RxState rx = null!;
    public V17TxState tx = null!;
    public ModemEchoCanSegmentState? ec;
    public ushort permitted_rates_signal;
    public SpanLogState logging = new();
    private bool disposed;
    public void Dispose() {
        if (disposed)
            return;

        ModemEcho.SegmentFree(ec);
        ec = null;

        tx?.Dispose();
        rx?.Dispose();
        logging.Dispose();

        disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Direct C# implementation of the original v32bis_* API.
/// </summary>
public static class V32Bis {

    public const double V32BIS_CONSTELLATION_SCALING_FACTOR = 4096.0;


    public const int V32BIS_RATE_14400 = 0x1000;
    public const int V32BIS_RATE_12000 = 0x0400;
    public const int V32BIS_RATE_9600 = 0x0200;
    public const int V32BIS_RATE_7200 = 0x0040;
    public const int V32BIS_RATE_4800 = 0x0020;

    /// <summary>
    /// The original private header declares
    /// extern const complexf_t v32bis_constellation[16].
    /// The supplied v32bis.c neither defines nor references that object;
    /// therefore this translation does not invent replacement data.
    /// </summary>

    public static int v32bis_rx_restart(v32bis_state_t s, int bit_rate) {
        ArgumentNullException.ThrowIfNull(s);
        return v17_rx_restart(s.rx, bit_rate, 0);
    }

    public static int v32bis_equalizer_state(
        v32bis_state_t s,
        out ReadOnlyMemory<V17RxComplex> coeffs) {
        ArgumentNullException.ThrowIfNull(s);
        return v17_rx_equalizer_state(s.rx, out coeffs);
    }

    public static float v32bis_rx_carrier_frequency(v32bis_state_t s) {
        ArgumentNullException.ThrowIfNull(s);
        return v17_rx_carrier_frequency(s.rx);
    }

    public static float v32bis_rx_symbol_timing_correction(v32bis_state_t s) {
        ArgumentNullException.ThrowIfNull(s);
        return v17_rx_symbol_timing_correction(s.rx);
    }

    public static float v32bis_rx_signal_power(v32bis_state_t s) {
        ArgumentNullException.ThrowIfNull(s);
        return v17_rx_signal_power(s.rx);
    }

    public static void v32bis_rx_set_signal_cutoff(v32bis_state_t s, float cutoff) {
        ArgumentNullException.ThrowIfNull(s);
        v17_rx_set_signal_cutoff(s.rx, cutoff);
    }

    public static int v32bis_tx(v32bis_state_t s, short[] amp, int len) {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(amp);
        return v17_tx(s.tx, amp, len);
    }

    public static int v32bis_tx(v32bis_state_t s, Span<short> amp) {
        ArgumentNullException.ThrowIfNull(s);
        return v17_tx(s.tx, amp);
    }

    public static int v32bis_rx(v32bis_state_t s, short[] amp, int len) {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(amp);
        return v17_rx(s.rx, amp, len);
    }

    public static int v32bis_rx(v32bis_state_t s, ReadOnlySpan<short> amp) {
        ArgumentNullException.ThrowIfNull(s);
        return v17_rx(s.rx, amp);
    }

    public static int v32bis_rx_fillin(v32bis_state_t s, int len) {
        ArgumentNullException.ThrowIfNull(s);
        return v17_rx_fillin(s.rx, len);
    }

    public static void v32bis_tx_power(v32bis_state_t s, float power) {
        ArgumentNullException.ThrowIfNull(s);
        v17_tx_power(s.tx, power);
    }

    public static void v32bis_set_get_bit(
        v32bis_state_t s,
        V32BisGetBitDelegate? get_bit,
        object? user_data) {
        ArgumentNullException.ThrowIfNull(s);
        v17_tx_set_get_bit(s.tx, get_bit, user_data);
    }

    public static void v32bis_set_put_bit(
        v32bis_state_t s,
        V17RxPutBitHandler? put_bit,
        object? user_data) {
        ArgumentNullException.ThrowIfNull(s);
        v17_rx_set_put_bit(s.rx, put_bit, user_data);
    }

    public static int v32bis_set_supported_bit_rates(v32bis_state_t s, int rates) {
        ArgumentNullException.ThrowIfNull(s);

        s.permitted_rates_signal =
            unchecked((ushort)((rates & 0x1660) | 0x8990));

        // Rate signal sync test is (value & 0x888F) == 0x8880
        // E signal sync test is (value & 0x888F) == 0x888F
        return 0;
    }

    public static int v32bis_current_bit_rate(v32bis_state_t s) {
        ArgumentNullException.ThrowIfNull(s);
        return 14400;
    }

    public static SpanLogState v32bis_get_logging_state(v32bis_state_t s) {
        ArgumentNullException.ThrowIfNull(s);
        return s.logging;
    }

    public static int v32bis_restart(v32bis_state_t s, int bit_rate) {
        ArgumentNullException.ThrowIfNull(s);

        s.rx.Logging.Flow($"Restarting V.32bis, {bit_rate}bps\n");
        v17_tx_restart(s.tx, bit_rate, false, false);
        v17_rx_restart(s.rx, bit_rate, 0);
        return 0;
    }

    public static v32bis_state_t? v32bis_init(
        v32bis_state_t? s,
        int bit_rate,
        bool calling_party,
        V32BisGetBitDelegate? get_bit,
        object? get_bit_user_data,
        V17RxPutBitHandler? put_bit,
        object? put_bit_user_data) {
        s ??= new v32bis_state_t();

        s.logging = span_log_init(s.logging, SPAN_LOG_NONE, null);
        span_log_set_protocol(s.logging, "V.32bis");

        s.bit_rate = bit_rate;
        s.calling_party = calling_party;

        // V.32bis never uses TEP.
        s.tx = v17_tx_init(
            null,
            bit_rate,
            false,
            get_bit,
            get_bit_user_data)!;

        s.rx = v17_rx_init(
            null,
            bit_rate,
            put_bit,
            put_bit_user_data)!;

        s.ec = ModemEcho.SegmentInit(256);

        // Initialise the parts which differ from V.17.
        if (s.calling_party) {
            s.tx.scrambler_tap = 17;
            s.rx.ScramblerTapValue = 4;
        } else {
            s.tx.scrambler_tap = 4;
            s.rx.ScramblerTapValue = 17;
        }

        v32bis_set_supported_bit_rates(
            s,
            V32BIS_RATE_14400
            | V32BIS_RATE_12000
            | V32BIS_RATE_9600
            | V32BIS_RATE_7200
            | V32BIS_RATE_4800);

        v32bis_restart(s, bit_rate);
        return s;
    }

    public static int v32bis_release(v32bis_state_t s) {
        ArgumentNullException.ThrowIfNull(s);

        ModemEcho.SegmentFree(s.ec);
        s.ec = null;
        return 0;
    }

    public static int v32bis_free(v32bis_state_t? s) {
        s?.Dispose();
        return 0;
    }

    public static void v32bis_set_qam_report_handler(
        v32bis_state_t s,
        V17RxQamReportHandler? handler,
        object? user_data) {
        ArgumentNullException.ThrowIfNull(s);
        v17_rx_set_qam_report_handler(s.rx, handler, user_data);
    }
}
/*- End of file ------------------------------------------------------------*/