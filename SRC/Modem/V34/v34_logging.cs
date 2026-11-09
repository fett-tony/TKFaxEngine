/*
 * TKFaxEngine - direct C# conversion of the TKFaxEngineFX/spanDSP V.34 sources.
 *
 * v34_logging.cs - ITU V.34 modem logging.
 * Direct translation of v34_logging.c.
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2009 Steve Underwood.
 * Licensed under the GNU Lesser General Public License version 2.1.
 *
 * THIS IS A WORK IN PROGRESS - NOT YET FUNCTIONAL!
 */

#nullable enable

using TKFaxEngine;

namespace TKFaxEngine.Modem.V34;

public static partial class v34 {
    private static string trellis_size_code_to_str(int code) {
        switch (code) {
            case V34_TRELLIS_16:
                return "16 state";
            case V34_TRELLIS_32:
                return "32 state";
            case V34_TRELLIS_64:
                return "64 state";
            case V34_TRELLIS_RESERVED:
                return "Reserved for ITU-T";
        }
        return "???";
    }

    public static void log_info0(SpanLogState log, bool tx, v34_capabilities_t cap, int info0_acknowledgement) {
        string[] tx_sources =
        {
            "internal",
            "sync'd to rx",
            "external",
            "reserved for ITU-T"
        };

        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "%s INFO0:\n", tx ? "Tx" : "Rx");
        for (int i = 0; i < 6; i++) {
            LoggingApi.span_log(log,
                                LoggingApi.SPAN_LOG_FLOW,
                                "  Baud rate %d %s %s\n",
                                baud_rate_parameters[i].baud_rate,
                                cap.support_baud_rate_low_carrier[i] ? "low" : "---",
                                cap.support_baud_rate_low_carrier[i] ? "high" : "----");
        }
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  3429 baud %sallowed\n", cap.rate_3429_allowed ? "" : "dis");
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Tx power reduction %ssupported\n", cap.support_power_reduction ? "" : "not ");
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Max different between Tx and Rx baud rates is %d\n", cap.max_baud_rate_difference);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Constellations up to %d supported\n", cap.support_1664_point_constellation ? 1664 : 960);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Tx clock source - %s\n", tx_sources[cap.tx_clock_source]);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Message %sfrom a CME modem\n", cap.from_cme_modem ? "" : "not ");
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  INFO0 frame %sacknowledged\n", info0_acknowledgement != 0 ? "" : "not ");
    }

    public static void log_info1c(SpanLogState log, bool tx, info1c_t info1c) {
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "%s INFO1c:\n", tx ? "Tx" : "Rx");
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Minimum power reduction = %ddB\n", info1c.power_reduction);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Additional power reduction = %ddB\n", info1c.additional_power_reduction);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Length of MD = %dms\n", info1c.md * 35);
        for (int i = 0; i <= 5; i++) {
            LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Baud rate %d use %s carrier\n", baud_rate_parameters[i].baud_rate, info1c.rate_data[i].use_high_carrier ? "high" : "low");
            LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Baud rate %d pre-emphasis index = %d\n", baud_rate_parameters[i].baud_rate, info1c.rate_data[i].pre_emphasis);
            LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Baud rate %d max data rate = %dbps\n", baud_rate_parameters[i].baud_rate, info1c.rate_data[i].max_bit_rate * 2400);
        }
        if (info1c.freq_offset == -512)
            LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Frequency offset not available\n");
        else
            LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Frequency offset = %fHz\n", info1c.freq_offset * 0.02f);
    }

    public static void log_info1a(SpanLogState log, bool tx, info1a_t info1a) {
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "%s INFO1a:\n", tx ? "Tx" : "Rx");
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Minimum power reduction = %ddB\n", info1a.power_reduction);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Addition power reduction = %ddB\n", info1a.additional_power_reduction);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Length of MD = %dms\n", info1a.md * 35);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  %s carrier\n", info1a.use_high_carrier ? "High" : "Low");
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Pre-emphasis filter = %d\n", info1a.preemphasis_filter);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Maximum data rate = %dbps\n", info1a.max_data_rate * 2400);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Baud rate A->C = %d\n", baud_rate_parameters[info1a.baud_rate_a_to_c].baud_rate);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Baud rate C->A = %d\n", baud_rate_parameters[info1a.baud_rate_c_to_a].baud_rate);
        if (info1a.freq_offset == -512)
            LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Frequency offset not available\n");
        else
            LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Frequency offset = %fHz\n", info1a.freq_offset * 0.02f);
    }

    public static void log_infoh(SpanLogState log, bool tx, infoh_t infoh) {
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "%s INFO0h:\n", tx ? "Tx" : "Rx");
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Minimum power reduction = %ddB\n", infoh.power_reduction);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Length of TRN = %dms\n", infoh.length_of_trn * 35);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  %s carrier\n", infoh.use_high_carrier ? "High" : "Low");
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Pre-emphasis filter = %d\n", infoh.preemphasis_filter);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Baud rate = %d\n", baud_rate_parameters[infoh.baud_rate].baud_rate);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Training constellation = %d state\n", infoh.trn16 ? 16 : 4);
    }

    public static void log_mp(SpanLogState log, bool tx, mp_t mp) {
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "%s MP:\n", tx ? "Tx" : "Rx");
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Type = %d\n", mp.type);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Max data rate A to C = %dbps\n", mp.bit_rate_a_to_c * 2400);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Max data rate C to A = %dbps\n", mp.bit_rate_c_to_a * 2400);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Aux channel supported = %d\n", mp.aux_channel_supported);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Trellis size = %s\n", trellis_size_code_to_str(mp.trellis_size));
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Use non-linear encoder = %d\n", mp.use_non_linear_encoder);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Expanded shaping = %d\n", mp.expanded_shaping);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  MP acknowledged = %d\n", mp.mp_acknowledged);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Signalling rate mask = 0x%04X\n", mp.signalling_rate_mask);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Asymmetric rates allowed = %d\n", mp.asymmetric_rates_allowed);
        if (mp.type == 1) {
            for (int i = 0; i < 3; i++)
                LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Precoder coeff[%d] = (%d, %d)\n", i, mp.precoder_coeffs[i].re, mp.precoder_coeffs[i].im);
        }
    }

    public static void log_mph(SpanLogState log, bool tx, mph_t mph) {
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "%s MPh:\n", tx ? "Tx" : "Rx");
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Type = %d\n", mph.type);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Max data rate = %dbps\n", mph.max_data_rate * 2400);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Control channel data rate = %dbps\n", mph.control_channel_2400 != 0 ? 2400 : 1200);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Trellis size = %s\n", trellis_size_code_to_str(mph.trellis_size));
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Use non-linear encoder = %d\n", mph.use_non_linear_encoder);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Expanded shaping = %d\n", mph.expanded_shaping);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Signalling rate mask = 0x%04X\n", mph.signalling_rate_mask);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Asymmetric rates allowed = %d\n", mph.asymmetric_rates_allowed);
        if (mph.type == 1) {
            for (int i = 0; i < 3; i++)
                LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Precoder coeff[%d] = (%d, %d)\n", i, mph.precoder_coeffs[i].re, mph.precoder_coeffs[i].im);
        }
    }

    public static void log_parameters(SpanLogState log, bool tx, v34_parameters_t parms) {
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "%s V.34 parameters:\n", tx ? "Tx" : "Rx");
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW,
                            "  Max bit rate:       %dbps%s\n",
                            ((parms.max_bit_rate_code >> 1) + 1) * 2400,
                            (parms.max_bit_rate_code & 1) != 0 ? "+ 200bps" : "");
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  Bit rate:           %dbps\n", parms.bit_rate);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  b:                  %d\n", parms.b);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  j:                  %d\n", parms.j);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  k:                  %d\n", parms.k);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  l:                  %d points\n", parms.l);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  m:                  %d\n", parms.m);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  p:                  %d\n", parms.p);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  q:                  %d (mask %d)\n", parms.q, parms.q_mask);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  r:                  %d\n", parms.r);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW, "  w:                  %d\n", parms.w);
        LoggingApi.span_log(log, LoggingApi.SPAN_LOG_FLOW,
                            "  Samples per symbol: %d/%d\n",
                            parms.samples_per_symbol_numerator,
                            parms.samples_per_symbol_denominator);
    }
}
