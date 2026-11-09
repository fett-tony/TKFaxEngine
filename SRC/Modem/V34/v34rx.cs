/*
 * TKFaxEngine - direct C# conversion of the TKFaxEngineFX/spanDSP V.34 sources.
 *
 * v34rx.cs - ITU V.34 modem, receive part.
 * Direct translation of v34rx.c.
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2009 Steve Underwood.
 * Licensed under the GNU Lesser General Public License version 2.1.
 *
 * THIS IS A WORK IN PROGRESS - NOT YET FUNCTIONAL!
 * This status is inherited unchanged from the original V.34 source.
 */

#nullable enable

using TKFaxEngine.Modem.V22;

using TKFaxEngine.Audio;

namespace TKFaxEngine.Modem.V34;

public static partial class v34 {
    private const float CARRIER_NOMINAL_FREQ = 1800.0f;
    private const int RX_PULSESHAPER_2400_COEFF_SETS = V22BisRxRrc.RX_PULSESHAPER_2400_COEFF_SETS;
    private static readonly float[][] rx_pulseshaper_1200_re = V22BisRxRrc.rx_pulseshaper_1200_re;
    private static readonly float[][] rx_pulseshaper_1200_im = V22BisRxRrc.rx_pulseshaper_1200_im;
    private static readonly float[][] rx_pulseshaper_2400_re = V22BisRxRrc.rx_pulseshaper_2400_re;
    private static readonly float[][] rx_pulseshaper_2400_im = V22BisRxRrc.rx_pulseshaper_2400_im;

    private static int descramble(v34_rx_state_t s, int in_bit) {
        int out_bit = (in_bit ^ (int)(s.scramble_reg >> s.scrambler_tap) ^ (int)(s.scramble_reg >> 22)) & 1;
        s.scramble_reg = (s.scramble_reg << 1) | unchecked((uint)in_bit);
        return out_bit;
    }

    private static void pack_output_bitstream(v34_rx_state_t s) {
        int i;
        int n;
        int bit;
        int bb;
        int kk;
        int t = 0;

        LoggingApi.span_log(s.logging!,
                 LoggingApi.SPAN_LOG_FLOW,
                 "Rx - Packed %p %8X - %X %X %X %X - %2X %2X %2X %2X %2X %2X %2X %2X\n",
                 s,
                 s.r0,
                 s.ibits[0], s.ibits[1], s.ibits[2], s.ibits[3],
                 s.qbits[0], s.qbits[1], s.qbits[2], s.qbits[3],
                 s.qbits[4], s.qbits[5], s.qbits[6], s.qbits[7]);

        bitstream_init(s.bs, true);
        bb = s.parms.b;
        kk = s.parms.k;
        s.s_bit_cnt += s.parms.r;
        if (s.s_bit_cnt >= s.parms.p) {
            s.s_bit_cnt -= s.parms.p;
        } else if (bb > 12) {
            bb--;
            kk--;
        }

        if (s.parms.k != 0) {
            bitstream_put(s.bs, s.rxbuf, ref t, s.r0, kk);
            for (i = 0; i < 4; i++) {
                bitstream_put(s.bs, s.rxbuf, ref t, s.ibits[i], 3);
                if (s.parms.q != 0) {
                    bitstream_put(s.bs, s.rxbuf, ref t, s.qbits[2 * i], s.parms.q);
                    bitstream_put(s.bs, s.rxbuf, ref t, s.qbits[2 * i + 1], s.parms.q);
                }
            }
        } else {
            n = bb - 8;
            for (i = 0; i < n; i++)
                bitstream_put(s.bs, s.rxbuf, ref t, s.ibits[i], 3);
            for (; i < 4; i++)
                bitstream_put(s.bs, s.rxbuf, ref t, s.ibits[i], 2);
        }
        bitstream_flush(s.bs, s.rxbuf, ref t);

        bitstream_init(s.bs, true);
        int u = 0;
        i = 0;
        s.aux_bit_cnt += s.parms.w;
        if (s.aux_bit_cnt >= s.parms.p) {
            s.aux_bit_cnt -= s.parms.p;
            for (; i < kk; i++) {
                bit = unchecked((int)bitstream_get(s.bs, s.rxbuf, ref u, 1));
                s.put_bit!(s.put_bit_user_data, descramble(s, bit));
            }
            bit = unchecked((int)bitstream_get(s.bs, s.rxbuf, ref u, 1));
            if (s.put_aux_bit is not null)
                s.put_aux_bit(s.put_bit_user_data, bit);
            i++;
        }
        for (; i < bb; i++) {
            bit = unchecked((int)bitstream_get(s.bs, s.rxbuf, ref u, 1));
            s.put_bit!(s.put_bit_user_data, descramble(s, bit));
        }
    }

    private static void shell_unmap(v34_rx_state_t s) {
        int n21;
        int n22;
        int n23;
        int n24;
        int k;
        int w41;
        int w42;
        int w2;
        int w8;
        uint[] g2 = g2s[s.parms.m]!;
        uint[] g4 = g4s[s.parms.m]!;
        uint[] z8 = z8s[s.parms.m]!;

        n21 = s.mjk[6] < s.parms.m - s.mjk[7] ? s.mjk[6] : s.parms.m - 1 - s.mjk[7];
        n22 = s.mjk[4] < s.parms.m - s.mjk[5] ? s.mjk[4] : s.parms.m - 1 - s.mjk[5];
        n23 = s.mjk[2] < s.parms.m - s.mjk[3] ? s.mjk[2] : s.parms.m - 1 - s.mjk[3];
        n24 = s.mjk[0] < s.parms.m - s.mjk[1] ? s.mjk[0] : s.parms.m - 1 - s.mjk[1];

        w2 = s.mjk[4] + s.mjk[5];
        w41 = w2 + s.mjk[6] + s.mjk[7];
        uint n41 = 0;
        for (k = 0; k < w2; k++)
            n41 = unchecked(n41 + g2[k] * g2[w41 - k]);
        n41 = unchecked(n41 + (uint)n21 * g2[w2]);
        n41 = unchecked(n41 + (uint)n22);

        w2 = s.mjk[0] + s.mjk[1];
        w42 = w2 + s.mjk[2] + s.mjk[3];
        uint n42 = 0;
        for (k = 0; k < w2; k++)
            n42 = unchecked(n42 + g2[k] * g2[w42 - k]);
        n42 = unchecked(n42 + (uint)n23 * g2[w2]);
        n42 = unchecked(n42 + (uint)n24);

        w8 = w41 + w42;
        uint n8 = 0;
        for (k = 0; k < w42; k++)
            n8 = unchecked(n8 + g4[k] * g4[w8 - k]);
        n8 = unchecked(n8 + n41 * g4[w42]);
        n8 = unchecked(n8 + n42);
        s.r0 = unchecked(z8[w8] + n8);
    }

    private static int get_inverse_constellation_point(complexi16_t point) {
        int x = point.re + 1;
        x = (x + 43) / 4;
        if (x < 0)
            x = 0;
        else if (x > 22)
            x = 22;
        int y = point.im + 1;
        y = (y + 43) / 4;
        if (y < 0)
            y = 0;
        else if (y > 22)
            y = 22;
        return v34_inverse_superconstellation[y, x];
    }

    private static complexi16_t rotate90_counterclockwise(complexi16_t x, int quads) {
        return (quads & 3) switch {
            0 => new complexi16_t(x.re, x.im),
            1 => new complexi16_t(-x.im, x.re),
            2 => new complexi16_t(-x.re, -x.im),
            _ => new complexi16_t(x.im, -x.re),
        };
    }

    private static complexi16_t quantize_rx(v34_rx_state_t s, complexi16_t x) {
        int re = Math.Abs((int)x.re);
        int im = Math.Abs((int)x.im);
        if (s.parms.b >= 56) {
            re = ((re + 0x0FF) >> 7) & ~0x03;
            im = ((im + 0x0FF) >> 7) & ~0x03;
        } else {
            re = ((re + 0x07F) >> 7) & ~0x01;
            im = ((im + 0x07F) >> 7) & ~0x01;
        }
        if (x.re < 0) re = -re;
        if (x.im < 0) im = -im;
        return new complexi16_t(re, im);
    }

    private static complexi16_t precoder_rx_filter(v34_rx_state_t s) {
        int sumRe = 0;
        int sumIm = 0;
        for (int i = 0; i < 3; i++) {
            sumRe = unchecked(sumRe + s.x[i].re * s.h[i].re - s.x[i].im * s.h[i].im);
            sumIm = unchecked(sumIm + s.x[i].re * s.h[i].im + s.x[i].im * s.h[i].re);
        }
        int re = (Math.Abs(sumRe) + 0x01FFF) >> 14;
        if (sumRe < 0) re = -re;
        int im = (Math.Abs(sumIm) + 0x01FFF) >> 14;
        if (sumIm < 0) im = -im;
        for (int i = 2; i > 0; i--)
            s.x[i] = s.x[i - 1];
        return new complexi16_t(re, im);
    }

    private static complexi16_t prediction_error_filter(v34_rx_state_t s) {
        int sumRe = unchecked(s.xt[0].re * 16384);
        int sumIm = unchecked(s.xt[0].im * 16384);
        for (int i = 0; i < 3; i++) {
            sumRe = unchecked(sumRe + s.xt[i + 1].re * s.h[i].re - s.xt[i + 1].im * s.h[i].im);
            sumIm = unchecked(sumIm + s.xt[i + 1].im * s.h[i].re + s.xt[i + 1].re * s.h[i].im);
        }
        for (int i = 3; i > 0; i--)
            s.xt[i] = s.xt[i - 1];
        int re = (Math.Abs(sumRe) + 0x01FFF) >> 14;
        if (sumRe < 0) re = -re;
        int im = (Math.Abs(sumIm) + 0x01FFF) >> 14;
        if (sumIm < 0) im = -im;
        return new complexi16_t(re, im);
    }

    private static void quantize_n_ways(complexi16_t[,] xy, int row, complexi16_t yt) {
        int re = yt.re - ((1) << 7);
        int im = yt.im - ((1) << 7);
        int q = re;
        re = (Math.Abs(re) + ((2) << 7)) & ~(((4) << 7) - 1);
        if (q < 0) re = -re;
        q = im;
        im = (Math.Abs(im) + ((2) << 7)) & ~(((4) << 7) - 1);
        if (q < 0) im = -im;
        re += ((1) << 7);
        im += ((1) << 7);
        xy[row, 0] = new complexi16_t(re, im);

        int re23 = yt.re < re ? re - ((2) << 7) : re + ((2) << 7);
        int im12 = yt.im < im ? im - ((2) << 7) : im + ((2) << 7);
        xy[row, 1] = new complexi16_t(re, im12);
        xy[row, 2] = new complexi16_t(re23, im12);
        xy[row, 3] = new complexi16_t(re23, im);
    }

    private static void viterbi_calculate_candidate_errors(short[,] error, int errorRow, complexi16_t[,] xy, int xyRow, complexi16_t yt) {
        for (int i = 0; i < 4; i++) {
            int re = xy[xyRow, i].re - yt.re;
            int im = xy[xyRow, i].im - yt.im;
            int err = unchecked(re * re + im * im);
            error[errorRow, i] = unchecked((short)(err >> 4));
        }
    }

    private static void viterbi_calculate_branch_errors(viterbi_t s, complexi16_t[,] xy, int invert) {
        byte[,] kk =
        {
            {0, 0, 2, 2}, {0, 1, 2, 3}, {0, 2, 2, 0}, {0, 3, 2, 1},
            {1, 1, 3, 3}, {1, 2, 3, 0}, {1, 3, 3, 1}, {1, 0, 3, 2}
        };
        int inv = invert != 0 ? 4 : 0;
        for (int br = 0; br < 8; br++) {
            int n = br ^ inv;
            int error0 = s.error[0, kk[n, 0]] + s.error[1, kk[n, 1]];
            int error1 = s.error[0, kk[n, 2]] + s.error[1, kk[n, 3]];
            int smaller;
            int k0;
            int k1;
            if (error0 < error1) {
                smaller = error0;
                k0 = kk[n, 0];
                k1 = kk[n, 1];
            } else {
                smaller = error1;
                k0 = kk[n, 2];
                k1 = kk[n, 3];
            }
            s.branch_error[br] = unchecked((ushort)smaller);
            s.vit[s.ptr].branch_error_x[br] = unchecked((ushort)smaller);
            s.vit[s.ptr].bb[0, br] = xy[0, k0];
            s.vit[s.ptr].bb[1, br] = xy[1, k1];
        }
    }

    private static void viterbi_update_path_metrics(viterbi_t s) {
        uint curr_min_metric = uint.MaxValue;
        int prev_ptr = (s.ptr - 1) & 0xF;
        for (short i = 0; i < 16; i++) {
            uint min_metric = uint.MaxValue;
            ushort min_state = 0;
            ushort min_branch = 0;
            for (short j = 0; j < 4; j++) {
                short prev_state = unchecked((short)(s.conv_decode_table![i, j] >> 3));
                short branch = unchecked((short)(s.conv_decode_table[i, j] & 0x7));
                uint metric = unchecked(s.vit[prev_ptr].cumulative_path_metric[prev_state] + s.branch_error[branch]);
                if (metric < min_metric) {
                    min_metric = metric;
                    min_state = unchecked((ushort)prev_state);
                    min_branch = unchecked((ushort)branch);
                }
            }
            s.vit[s.ptr].cumulative_path_metric[i] = min_metric;
            s.vit[s.ptr].previous_path_ptr[i] = min_state;
            s.vit[s.ptr].pts[i] = min_branch;
            if (min_metric < curr_min_metric) {
                curr_min_metric = min_metric;
                s.curr_min_state = i;
            }
        }
        for (int i = 0; i < 16; i++)
            s.vit[s.ptr].cumulative_path_metric[i] -= curr_min_metric;
    }

    private static void viterbi_trace_back(viterbi_t s, complexi16_t[] y) {
        int next_state = s.curr_min_state;
        int last_baud = (s.ptr - 15) & 0xF;
        for (int i = s.ptr; i != last_baud; i = (i - 1) & 0xF)
            next_state = s.vit[i].previous_path_ptr[next_state];
        int branch = s.vit[last_baud].pts[next_state];
        y[0] = s.vit[last_baud].bb[0, branch];
        y[1] = s.vit[last_baud].bb[1, branch];
    }

    private static int process_rx_info0(v34_rx_state_t s, byte[] buf) {
        bitstream_state_t bs = new();
        int t = 0;
        s.far_capabilities = new v34_capabilities_t();
        bitstream_init(bs, true);
        s.far_capabilities.support_baud_rate_low_carrier[V34_BAUD_RATE_2400] =
        s.far_capabilities.support_baud_rate_high_carrier[V34_BAUD_RATE_2400] = true;
        bool b = bitstream_get(bs, buf, ref t, 1) != 0;
        s.far_capabilities.support_baud_rate_low_carrier[V34_BAUD_RATE_2743] =
        s.far_capabilities.support_baud_rate_high_carrier[V34_BAUD_RATE_2743] = b;
        b = bitstream_get(bs, buf, ref t, 1) != 0;
        s.far_capabilities.support_baud_rate_low_carrier[V34_BAUD_RATE_2800] =
        s.far_capabilities.support_baud_rate_high_carrier[V34_BAUD_RATE_2800] = b;
        b = bitstream_get(bs, buf, ref t, 1) != 0;
        s.far_capabilities.support_baud_rate_low_carrier[V34_BAUD_RATE_3429] =
        s.far_capabilities.support_baud_rate_high_carrier[V34_BAUD_RATE_3429] = b;
        s.far_capabilities.support_baud_rate_low_carrier[V34_BAUD_RATE_3000] = bitstream_get(bs, buf, ref t, 1) != 0;
        s.far_capabilities.support_baud_rate_high_carrier[V34_BAUD_RATE_3000] = bitstream_get(bs, buf, ref t, 1) != 0;
        s.far_capabilities.support_baud_rate_low_carrier[V34_BAUD_RATE_3200] = bitstream_get(bs, buf, ref t, 1) != 0;
        s.far_capabilities.support_baud_rate_high_carrier[V34_BAUD_RATE_3200] = bitstream_get(bs, buf, ref t, 1) != 0;
        s.far_capabilities.rate_3429_allowed = bitstream_get(bs, buf, ref t, 1) != 0;
        s.far_capabilities.support_power_reduction = bitstream_get(bs, buf, ref t, 1) != 0;
        s.far_capabilities.max_baud_rate_difference = unchecked((byte)bitstream_get(bs, buf, ref t, 3));
        s.far_capabilities.from_cme_modem = bitstream_get(bs, buf, ref t, 1) != 0;
        s.far_capabilities.support_1664_point_constellation = bitstream_get(bs, buf, ref t, 1) != 0;
        s.far_capabilities.tx_clock_source = unchecked((byte)bitstream_get(bs, buf, ref t, 2));
        s.info0_acknowledgement = bitstream_get(bs, buf, ref t, 1) != 0;
        log_info0(s.logging!, false, s.far_capabilities, s.info0_acknowledgement ? 1 : 0);
        return 0;
    }

    private static int process_rx_info1c(v34_rx_state_t s, info1c_t info1c, byte[] buf) {
        bitstream_state_t bs = new();
        int t = 0;
        bitstream_init(bs, true);
        info1c.power_reduction = unchecked((int)bitstream_get(bs, buf, ref t, 3));
        info1c.additional_power_reduction = unchecked((int)bitstream_get(bs, buf, ref t, 3));
        info1c.md = unchecked((int)bitstream_get(bs, buf, ref t, 7));
        for (int i = 0; i <= 5; i++) {
            info1c.rate_data[i].use_high_carrier = bitstream_get(bs, buf, ref t, 1) != 0;
            info1c.rate_data[i].pre_emphasis = unchecked((int)bitstream_get(bs, buf, ref t, 4));
            info1c.rate_data[i].max_bit_rate = unchecked((int)bitstream_get(bs, buf, ref t, 4));
        }
        info1c.freq_offset = unchecked((int)bitstream_get(bs, buf, ref t, 10));
        if ((info1c.freq_offset & 0x200) != 0)
            info1c.freq_offset = -(info1c.freq_offset ^ 0x3FF) - 1;
        log_info1c(s.logging!, false, info1c);
        return 0;
    }

    private static int process_rx_info1a(v34_rx_state_t s, info1a_t info1a, byte[] buf) {
        bitstream_state_t bs = new();
        int t = 0;
        bitstream_init(bs, true);
        info1a.power_reduction = unchecked((int)bitstream_get(bs, buf, ref t, 3));
        info1a.additional_power_reduction = unchecked((int)bitstream_get(bs, buf, ref t, 3));
        info1a.md = unchecked((int)bitstream_get(bs, buf, ref t, 7));
        info1a.use_high_carrier = bitstream_get(bs, buf, ref t, 1) != 0;
        info1a.preemphasis_filter = unchecked((int)bitstream_get(bs, buf, ref t, 4));
        info1a.max_data_rate = unchecked((int)bitstream_get(bs, buf, ref t, 4));
        info1a.baud_rate_a_to_c = unchecked((int)bitstream_get(bs, buf, ref t, 3));
        info1a.baud_rate_c_to_a = unchecked((int)bitstream_get(bs, buf, ref t, 3));
        info1a.freq_offset = unchecked((int)bitstream_get(bs, buf, ref t, 10));
        if ((info1a.freq_offset & 0x200) != 0)
            info1a.freq_offset = -(info1a.freq_offset ^ 0x3FF) - 1;
        s.baud_rate = info1a.baud_rate_c_to_a;
        s.v34_carrier_phase_rate = Dds.dds_phase_ratef(carrier_frequency(s.baud_rate, s.high_carrier));
        log_info1a(s.logging!, false, info1a);
        return 0;
    }

    private static int process_rx_infoh(v34_rx_state_t s, infoh_t infoh, byte[] buf) {
        bitstream_state_t bs = new();
        int t = 0;
        bitstream_init(bs, true);
        infoh.power_reduction = unchecked((int)bitstream_get(bs, buf, ref t, 3));
        infoh.length_of_trn = unchecked((int)bitstream_get(bs, buf, ref t, 7));
        infoh.use_high_carrier = bitstream_get(bs, buf, ref t, 1) != 0;
        infoh.preemphasis_filter = unchecked((int)bitstream_get(bs, buf, ref t, 4));
        infoh.baud_rate = unchecked((int)bitstream_get(bs, buf, ref t, 3));
        infoh.trn16 = bitstream_get(bs, buf, ref t, 1) != 0;
        log_infoh(s.logging!, false, infoh);
        return 0;
    }

    private static int process_rx_mp(v34_rx_state_t s, mp_t mp, byte[] buf) {
        bitstream_state_t bs = new();
        int t = 0;
        bitstream_init(bs, true);
        mp.type = unchecked((int)bitstream_get(bs, buf, ref t, 1));
        bitstream_get(bs, buf, ref t, 1);
        mp.bit_rate_c_to_a = unchecked((int)bitstream_get(bs, buf, ref t, 4));
        mp.bit_rate_a_to_c = unchecked((int)bitstream_get(bs, buf, ref t, 4));
        mp.aux_channel_supported = unchecked((int)bitstream_get(bs, buf, ref t, 1));
        mp.trellis_size = unchecked((int)bitstream_get(bs, buf, ref t, 2));
        mp.use_non_linear_encoder = bitstream_get(bs, buf, ref t, 1) != 0;
        mp.expanded_shaping = bitstream_get(bs, buf, ref t, 1) != 0;
        mp.mp_acknowledged = bitstream_get(bs, buf, ref t, 1) != 0;
        bitstream_get(bs, buf, ref t, 1);
        mp.signalling_rate_mask = unchecked((int)bitstream_get(bs, buf, ref t, 15));
        mp.asymmetric_rates_allowed = bitstream_get(bs, buf, ref t, 1) != 0;
        if (mp.type == 1) {
            for (int i = 0; i < 3; i++) {
                bitstream_get(bs, buf, ref t, 1);
                mp.precoder_coeffs[i].re = unchecked((short)bitstream_get(bs, buf, ref t, 16));
                bitstream_get(bs, buf, ref t, 1);
                mp.precoder_coeffs[i].im = unchecked((short)bitstream_get(bs, buf, ref t, 16));
            }
        } else {
            Array.Clear(mp.precoder_coeffs);
        }
        log_mp(s.logging!, false, mp);
        return 0;
    }

    private static int process_rx_mph(v34_rx_state_t s, mph_t mph, byte[] buf) {
        bitstream_state_t bs = new();
        int t = 0;
        bitstream_init(bs, true);
        mph.type = unchecked((int)bitstream_get(bs, buf, ref t, 1));
        bitstream_get(bs, buf, ref t, 1);
        mph.max_data_rate = unchecked((int)bitstream_get(bs, buf, ref t, 4));
        bitstream_get(bs, buf, ref t, 3);
        mph.control_channel_2400 = unchecked((int)bitstream_get(bs, buf, ref t, 1));
        bitstream_get(bs, buf, ref t, 1);
        mph.trellis_size = unchecked((int)bitstream_get(bs, buf, ref t, 2));
        mph.use_non_linear_encoder = bitstream_get(bs, buf, ref t, 1) != 0;
        mph.expanded_shaping = bitstream_get(bs, buf, ref t, 1) != 0;
        bitstream_get(bs, buf, ref t, 2);
        mph.signalling_rate_mask = unchecked((int)bitstream_get(bs, buf, ref t, 15));
        mph.asymmetric_rates_allowed = bitstream_get(bs, buf, ref t, 1) != 0;
        if (mph.type == 1) {
            for (int i = 0; i < 3; i++) {
                bitstream_get(bs, buf, ref t, 1);
                mph.precoder_coeffs[i].re = unchecked((short)bitstream_get(bs, buf, ref t, 16));
                bitstream_get(bs, buf, ref t, 1);
                mph.precoder_coeffs[i].im = unchecked((short)bitstream_get(bs, buf, ref t, 16));
            }
        } else {
            Array.Clear(mph.precoder_coeffs);
        }
        log_mph(s.logging!, false, mph);
        return 0;
    }

    private static void put_info_bit(v34_rx_state_t s, int bit, int time_offset) {
        /* Put info0, info1, tone A or tone B bits */
        Console.Error.Write(CPrintfFormatter.Format("Rx bit = %d\n", new object?[] { bit }));
        s.bitstream = (s.bitstream << 1) | unchecked((uint)bit);
        switch (s.stage) {
            case V34_RX_STAGE_TONE_A:
                /* Calling side */
                if (++s.persistence1 < 10)
                    break;

                if (bit == 0) {
                    if (++s.persistence2 == 20) {
                        //s.received_event = V34_EVENT_TONE_SEEN;
                    }

                    break;
                }

                if (!s.signal_present)
                    s.persistence2 = 0;

                /* We have a reversal, but we should only recognise it if it has been
                   a little while since the last one */
                if (s.persistence2 > 20) {
                    Console.Error.Write("Rx bit reversal in tone A\n");
                    switch (s.received_event) {
                        case V34_EVENT_REVERSAL_1:
                            LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Rx - reversal 2 in tone A\n");
                            s.tone_ab_hop_time = s.sample_time + time_offset;
                            s.received_event = V34_EVENT_REVERSAL_2;
                            l1_l2_analysis_init(s);
                            break;
                        case V34_EVENT_REVERSAL_2:
                        case V34_EVENT_L2_SEEN:
                            LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Rx - reversal 3 in tone A\n");
                            s.tone_ab_hop_time = s.sample_time + time_offset;
                            s.received_event = V34_EVENT_REVERSAL_3;
                            /* The next info message will be INFO1a */
                            s.target_bits = 70 - (4 + 8 + 4);
                            s.stage = V34_RX_STAGE_INFO1A;
                            break;
                        default:
                            LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Rx - reversal 1 in tone A\n");
                            s.tone_ab_hop_time = s.sample_time + time_offset;
                            s.received_event = V34_EVENT_REVERSAL_1;
                            break;
                    }

                    s.persistence1 = 0;
                }

                s.persistence2 = 0;
                break;
            case V34_RX_STAGE_TONE_B:
                /* Answering side */
                if (++s.persistence1 < 10)
                    break;

                if (bit == 0) {
                    if (++s.persistence2 == 20) {
                        //s.received_event = V34_EVENT_TONE_SEEN;
                    }

                    break;
                }

                if (!s.signal_present)
                    s.persistence2 = 0;

                /* We have a reversal, but we should only recognise it if it has been
                   a little while since the last one */
                if (s.persistence2 > 20) {
                    Console.Error.Write("Rx bit reversal in tone B\n");
                    switch (s.received_event) {
                        case V34_EVENT_REVERSAL_2:
                            LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Rx - reversal 3 in tone B\n");
                            s.tone_ab_hop_time = s.sample_time + time_offset;
                            s.received_event = V34_EVENT_REVERSAL_3;
                            break;
                        case V34_EVENT_REVERSAL_1:
                            /* TODO: Need to avoid getting here falsely, just because the tone has resumed */
                            LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Rx - reversal 2 in tone B\n");
                            s.tone_ab_hop_time = s.sample_time + time_offset;
                            s.received_event = V34_EVENT_REVERSAL_2;
                            /* The next info message will be INFO1c */
                            s.target_bits = 109 - (4 + 8 + 4);
                            l1_l2_analysis_init(s);
                            break;
                        default:
                            LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Rx - reversal 1 in tone B\n");
                            s.tone_ab_hop_time = s.sample_time + time_offset;
                            s.received_event = V34_EVENT_REVERSAL_1;
                            break;
                    }

                    s.persistence1 = 0;
                }

                s.persistence2 = 0;
                break;
        }
        /* Search for INFO0, INFOh, INFO1a or INFO1c messages. */
        if (s.bit_count == 0) {
            /* Look for info message sync code */
            if ((s.bitstream & 0x3FF) == 0x372) {
                LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Rx - info sync code detected\n");
                Console.Error.Write("Rx bit info sync code detected\n");
                s.crc = 0xFFFF;
                s.bit_count = 1;
            }

        } else {
            /* Every 8 bits save the resulting byte */
            if ((s.bit_count & 0x07) == 0)
                s.info_buf[(s.bit_count >> 3) - 1] = global::TKFaxEngine.BitOperationsApi.bit_reverse8(unchecked((byte)(s.bitstream & 0xFF)) );

            s.crc = CrcApi.crc_itu16_bits(unchecked((byte)bit), 1, s.crc);
            if (s.bit_count++ == s.target_bits) {
                LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Rx - info CRC result 0x%x\n", s.crc);
                Console.Error.Write(CPrintfFormatter.Format("Rx bit CRC result 0x%X\n", new object?[] { s.crc }));
                Console.Error.Write(CPrintfFormatter.Format("Rx 0x%02X 0x%02X 0x%02X 0x%02X 0x%02X 0x%02X 0x%02X 0x%02X 0x%02X\n",
                       new object?[] {
                           s.info_buf[0],
                           s.info_buf[1],
                           s.info_buf[2],
                           s.info_buf[3],
                           s.info_buf[4],
                           s.info_buf[5],
                           s.info_buf[6],
                           s.info_buf[7],
                           s.info_buf[8]
                       }));
                if (s.crc == 0) {
                    switch (s.stage) {
                        case V34_RX_STAGE_TONE_A:
                        case V34_RX_STAGE_TONE_B:
                        case V34_RX_STAGE_INFO0:
                            process_rx_info0(s, s.info_buf);
                            s.stage = (s.calling_party) ? V34_RX_STAGE_TONE_A : V34_RX_STAGE_TONE_B;
                            s.received_event = V34_EVENT_INFO0_OK;
                            break;
                        case V34_RX_STAGE_INFOH:
                            process_rx_infoh(s, s.infoh, s.info_buf);
                            s.received_event = V34_EVENT_INFO1_OK;
                            break;
                        case V34_RX_STAGE_INFO1C:
                            process_rx_info1c(s, s.info1c, s.info_buf);
                            s.received_event = V34_EVENT_INFO1_OK;
                            break;
                        case V34_RX_STAGE_INFO1A:
                            process_rx_info1a(s, s.info1a, s.info_buf);
                            s.received_event = V34_EVENT_INFO1_OK;
                            break;
                    }

                } else {
                    switch (s.stage) {
                        case V34_RX_STAGE_TONE_A:
                        case V34_RX_STAGE_TONE_B:
                        case V34_RX_STAGE_INFO0:
                            s.received_event = V34_EVENT_INFO0_BAD;
                            break;
                        case V34_RX_STAGE_INFOH:
                            break;
                        case V34_RX_STAGE_INFO1C:
                        case V34_RX_STAGE_INFO1A:
                            s.received_event = V34_EVENT_INFO1_BAD;
                            break;
                    }

                }

                s.bit_count = 0;
            }

        }

    }

    private static int info_rx(v34_rx_state_t s, ReadOnlySpan<short> amp, int offset, int len) {
        int i;
        int step;
        complexf_t z;
        complexf_t zz;
        complexf_t sample;
        float ii;
        float qq;
        uint angle;
        int power;

        s.agc_scaling = 0.01f;
        step = 6;
        for (i = 0; i < len; i++) {
            power = s.power.Update(amp[offset + i]);
            if (s.signal_present) {
                if (power < s.carrier_off_power) {
                    LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Signal down\n");
                    s.signal_present = false;
                    s.persistence2 = 0;
                }

            } else {
                if (power > s.carrier_on_power) {
                    LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Signal up\n");
                    s.signal_present = true;
                    s.persistence2 = 0;
                }

            }

            s.rrc_filter[s.rrc_filter_step] = amp[offset + i];
            if (++s.rrc_filter_step >= V34_RX_FILTER_STEPS)
                s.rrc_filter_step = 0;

            if (s.calling_party) {
                ii = vec_circular_dot_prodf(s.rrc_filter, s.rrc_filter_step, rx_pulseshaper_2400_re[step], V34_RX_FILTER_STEPS);
                qq = vec_circular_dot_prodf(s.rrc_filter, s.rrc_filter_step, rx_pulseshaper_2400_im[step], V34_RX_FILTER_STEPS);
            } else {
                ii = vec_circular_dot_prodf(s.rrc_filter, s.rrc_filter_step, rx_pulseshaper_1200_re[step], V34_RX_FILTER_STEPS);
                qq = vec_circular_dot_prodf(s.rrc_filter, s.rrc_filter_step, rx_pulseshaper_1200_im[step], V34_RX_FILTER_STEPS);
            }

            sample.re = ii * s.agc_scaling;
            sample.im = qq * s.agc_scaling;
            /* Shift to baseband - since this is done in full complex form, the result is clean. */
            z = Dds.dds_lookup_complexf(s.carrier_phase);
            zz.re = sample.re * z.re - sample.im * z.im;
            zz.im = -sample.re * z.im - sample.im * z.re;
            angle = unchecked((uint)global::TKFaxEngine.FastArcTan.arctan2(zz.im, zz.re));
            Console.Error.Write(CPrintfFormatter.Format("XXX%d, %7d, %f, %f, 0x%08X, %d\n", new object?[] { s.calling_party, amp[offset + i], zz.re, zz.im, angle, angle }));
            if (unchecked((uint)Math.Abs(unchecked((int)(angle - s.last_angles[1])))) > unchecked((uint)Dds.PhaseDegrees(90.0f)) && s.blip_duration > 3) {
                put_info_bit(s, 1, i);
                s.duration = 0;
                s.blip_duration = 0;
            } else {
                if (s.blip_duration > 60) {
                    /* We are getting rather late for a transition. This must be a zero bit. */
                    put_info_bit(s, 0, i);
                    /* Step on by one bit time. */
                    s.blip_duration -= 40;
                }

            }

            s.last_angles[1] = s.last_angles[0];
            s.last_angles[0] = angle;
            s.duration++;
            s.blip_duration += 3;
            Dds.dds_advancef(ref s.carrier_phase, s.cc_carrier_phase_rate);
        }

        return 0;
    }

    private static void cc_symbol_sync(v34_rx_state_t s) {
        int i;
        float v;
        float p;

        /* This routine adapts the position of the half baud samples entering the equalizer. */

        /* This symbol sync scheme is based on the technique first described by Dominique Godard in
            Passband Timing Recovery in an All-Digital Modem Receiver
            IEEE TRANSACTIONS ON COMMUNICATIONS, VOL. COM-26, NO. 5, MAY 1978 */

        /* This is slightly rearranged from figure 3b of the Godard paper, as this saves a couple of
           maths operations */
        /* Cross correlate */
        v = s.cc_ted.symbol_sync_low[1] * s.cc_ted.symbol_sync_high[0] * s.cc_ted.low_band_edge_coeff[2]
          - s.cc_ted.symbol_sync_low[0] * s.cc_ted.symbol_sync_high[1] * s.cc_ted.high_band_edge_coeff[2]
          + s.cc_ted.symbol_sync_low[1] * s.cc_ted.symbol_sync_high[1] * s.cc_ted.mixed_edges_coeff_3;
        /* Filter away any DC component  */
        p = v - s.cc_ted.symbol_sync_dc_filter[1];
        s.cc_ted.symbol_sync_dc_filter[1] = s.cc_ted.symbol_sync_dc_filter[0];
        s.cc_ted.symbol_sync_dc_filter[0] = v;
        /* A little integration will now filter away much of the HF noise */
        s.cc_ted.baud_phase -= p;
        v = MathF.Abs(s.cc_ted.baud_phase);
        if (v > 100.0f) {
            i = (v > 200.0f) ? 2 : 1;
            if (s.cc_ted.baud_phase < 0.0f)
                i = -i;

            //printf("v = %10.5f %5d - %f %f %d\n", v, i, p, s.cc_ted.baud_phase, s.total_baud_timing_correction);
            s.eq_put_step += i;
            s.total_baud_timing_correction += i;
        }

    }

    private static void pri_symbol_sync(v34_rx_state_t s) {
        int i;
        float v;
        float p;

        /* This routine adapts the position of the half baud samples entering the equalizer. */

        /* This symbol sync scheme is based on the technique first described by Dominique Godard in
            Passband Timing Recovery in an All-Digital Modem Receiver
            IEEE TRANSACTIONS ON COMMUNICATIONS, VOL. COM-26, NO. 5, MAY 1978 */

        /* This is slightly rearranged from figure 3b of the Godard paper, as this saves a couple of
           maths operations */
        /* Cross correlate */
        v = s.pri_ted.symbol_sync_low[1] * s.pri_ted.symbol_sync_high[0] * s.pri_ted.low_band_edge_coeff[2]
          - s.pri_ted.symbol_sync_low[0] * s.pri_ted.symbol_sync_high[1] * s.pri_ted.high_band_edge_coeff[2]
          + s.pri_ted.symbol_sync_low[1] * s.pri_ted.symbol_sync_high[1] * s.pri_ted.mixed_edges_coeff_3;
        /* Filter away any DC component  */
        p = v - s.pri_ted.symbol_sync_dc_filter[1];
        s.pri_ted.symbol_sync_dc_filter[1] = s.pri_ted.symbol_sync_dc_filter[0];
        s.pri_ted.symbol_sync_dc_filter[0] = v;
        /* A little integration will now filter away much of the HF noise */
        s.pri_ted.baud_phase -= p;
        v = MathF.Abs(s.pri_ted.baud_phase);
        if (v > 100.0f) {
            i = (v > 200.0f) ? 2 : 1;
            if (s.pri_ted.baud_phase < 0.0f)
                i = -i;

            //printf("v = %10.5f %5d - %f %f %d\n", v, i, p, s.pri_ted.baud_phase, s.total_baud_timing_correction);
            s.eq_put_step += i;
            s.total_baud_timing_correction += i;
        }

    }

    private static void create_godard_coeffs(ted_t coeffs, float carrier, float baud_rate, float alpha) {
        float low_edge;
        float high_edge;

        /* Create the coefficient set for an arbitrary Godard TED/symbol sync filter */
        low_edge = 2.0f * MathF.PI * (carrier - baud_rate / 2.0f) / SAMPLE_RATE;
        high_edge = 2.0f * MathF.PI * (carrier + baud_rate / 2.0f) / SAMPLE_RATE;

        coeffs.low_band_edge_coeff[0] = 2.0f * alpha * MathF.Cos(low_edge);
        coeffs.high_band_edge_coeff[0] = 2.0f * alpha * MathF.Cos(high_edge);
        coeffs.low_band_edge_coeff[1] =
        coeffs.high_band_edge_coeff[1] = -alpha * alpha;
        coeffs.low_band_edge_coeff[2] = -alpha * MathF.Sin(low_edge);
        coeffs.high_band_edge_coeff[2] = -alpha * MathF.Sin(high_edge);
        coeffs.mixed_edges_coeff_3 = -alpha * alpha * (MathF.Sin(high_edge) * MathF.Cos(low_edge) - MathF.Sin(low_edge) * MathF.Cos(high_edge));
    }

    public static float v34_rx_carrier_frequency(v34_state_t s) {
        return Dds.dds_frequencyf(s.rx.v34_carrier_phase_rate);
    }

    public static float v34_rx_symbol_timing_correction(v34_state_t s) {
        return (float) s.rx.total_baud_timing_correction / ((float) V34_RX_PULSESHAPER_COEFF_SETS * 10.0f / 3.0f);
    }

    public static float v34_rx_signal_power(v34_state_t s) {
        return s.rx.power.CurrentDbm0;
    }

    public static int v34_equalizer_state(v34_state_t s, out complexf_t[] coeffs) {
        coeffs = s.rx.eq_coeff;
        return V34_EQUALIZER_PRE_LEN + 1 + V34_EQUALIZER_POST_LEN;
    }

    private static void straight_line_fit(out float slope, out float intercept, float[] x, float[] y, int data_points) {
        float sum_x;
        float sum_y;
        float sum_xy;
        float sum_x2;
        float slopex;
        int i;

        sum_x = 0.0f;
        sum_y = 0.0f;
        sum_xy = 0.0f;
        sum_x2 = 0.0f;
        for (i = 0; i < data_points; i++) {
            sum_x += x[i];
            sum_y += y[i];
            sum_xy += x[i] * y[i];
            sum_x2 += x[i] * x[i];
        }

        slopex = (sum_xy - sum_x * sum_y / data_points) / (sum_x2 - sum_x * sum_x / data_points);
        slope = slopex;
        intercept = (sum_y - slopex * sum_x) / data_points;

    }

    private static void slow_dft(complexf_t[] data, int len) {
        int i;
        int bin;
        float arg;
        complexf_t[] buf = new complexf_t[len];

        for (i = 0; i < len; i++) {
            buf[i].re = data[i].re;
            buf[i].im = data[i].im;
        }

        for (bin = 0; bin <= len / 2; bin++) {
            data[bin].re =
            data[bin].im = 0.0f;
            for (i = 0; i < len; i++) {
                arg = bin * 2.0f * 3.1415926535f * i / (float)len;
                data[bin].re -= buf[i].re * MathF.Sin(arg);
                data[bin].im += buf[i].re * MathF.Cos(arg);
            }

        }

    }

    private static int perform_l1_l2_analysis(v34_rx_state_t s) {
        /* Phase adjustments to compensate for the tones which are sent phase inverted */
        float[] adjust = new float[] {
            0.0f,           /**/
            3.14159265f,    /* 300 */
            0.0f,           /**/
            0.0f,           /**/
            0.0f,           /**/
            42.0f,          /* Tone not sent */
            0.0f,           /* 1050 nominal line probe frequency */
            42.0f,          /* Tone not sent */
            0.0f,           /**/
            0.0f,           /**/
            3.14159265f,    /* 1650 */
            42.0f,          /* Tone not sent */
            0.0f,           /**/
            0.0f,           /**/
            3.14159265f,    /* 2250 */
            42.0f,          /* Tone not sent */
            0.0f,           /**/
            3.14159265f,    /* 2700 */
            0.0f,           /**/
            3.14159265f,    /* 3000 */
            3.14159265f,    /* 3150 */
            3.14159265f,    /* 3300 */
            3.14159265f,    /* 3450 */
            0.0f,           /**/
            0.0f            /**/
        };
        int i;
        int j;

        slow_dft(s.dft_buffer, LINE_PROBE_SAMPLES);
        /* Now resolve the analysis into gain and phase values for the bins which contain the tones */
        /* Base things around what happens at 1050Hz the first time through. */
        if (s.l1_l2_duration == 0)
            s.base_phase = MathF.Atan2(s.dft_buffer[21].im, s.dft_buffer[21].re);

        for (i = 0; i < 25; i++) {
            if (adjust[i] < 7.0f) {
                /* This tone should be present in the transmitted signal. */
                j = 3 * (i + 1);
                s.l1_l2_gains[i] = MathF.Sqrt(s.dft_buffer[j].re * s.dft_buffer[j].re
                                        + s.dft_buffer[j].im * s.dft_buffer[j].im);
                s.l1_l2_phases[i] = (MathF.Atan2(s.dft_buffer[j].im, s.dft_buffer[j].re) - s.base_phase + adjust[i]) % 3.14159265f;
            } else {
                /* This tone should not be present in the transmitted signal. */
                s.l1_l2_gains[i] = 0.0f;
                s.l1_l2_phases[i] = 0.0f;
            }

        }

        for (i = 0; i < 25; i++) {
            Console.Error.Write(CPrintfFormatter.Format("DFT %4d, %12.5f, %12.5f, %12.5f\n",
                   new object?[] {
                       i,
                       (i + 1) * 150.0f,
                       s.l1_l2_gains[i],
                       s.l1_l2_phases[i]
                   }));
            LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "DFT %4d, %12.5f, %12.5f, %12.5f\n",
                     i,
                     (i + 1) * 150.0f,
                     s.l1_l2_gains[i],
                     s.l1_l2_phases[i]);
        }

        //straight_line_fit(&slope, &intercept, x, y, data_points);
        return 0;
    }

    private static void l1_l2_analysis_init(v34_rx_state_t s) {
        LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Rx - Expect L1/L2\n");
        s.dft_ptr = 0;
        s.base_phase = 42.0f;
        s.l1_l2_duration = 0;
        s.current_demodulator = V34_MODULATION_L1_L2;
        s.stage = V34_RX_STAGE_L1_L2;
    }

    private static int l1_l2_analysis(v34_rx_state_t s, ReadOnlySpan<short> amp, int offset, int len) {
        int i;

        /* We need to work over whole cycles of the L1/L2 pattern, to avoid windowing and
           all its ills. One cycle takes 160/3 samples at 8000 samples/second, so we will
           process groups of 3 cycles, and run a Fourier transform every 160 samples (20ms).
           Since this is not a suitable length for an FFT we have to run a slow DFT. However,
           we don't do this for much of the time, so its not that big a deal. */
        for (i = 0; i < len; i++) {
            s.dft_buffer[s.dft_ptr].re = amp[offset + i];
            s.dft_buffer[s.dft_ptr].im = 0.0f;
            if (++s.dft_ptr >= LINE_PROBE_SAMPLES) {
                /* We now have 160 samples, so process the 3 cycles we should have in the buffer. */
                perform_l1_l2_analysis(s);
                s.dft_ptr = 0;
                LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "L1/L2 analysis x %d\n", s.l1_l2_duration);
                if (++s.l1_l2_duration > 20) {
                    LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "L1/L2 analysis done\n");
                    s.received_event = V34_EVENT_L2_SEEN;
                    s.current_demodulator = V34_MODULATION_TONES;
                    s.stage = (s.calling_party) ? V34_RX_STAGE_TONE_A : V34_RX_STAGE_INFO1C;
                }

            }

        }

        /* Also run this signal through the info analysis, so we pick up A or B tones */
        info_rx(s, amp, offset, len);

        return 0;
    }

    private static void process_cc_half_baud(v34_rx_state_t s, complexf_t sample) {
        int i;
        int data_bits;
        mp_t mp = new();
        mph_t mph = new();
        uint ang1;
        uint ang2;
        uint ang3;
        int[] bits = new int[4];
        v34_state_t t;

        /* This routine processes every half a baud, as we put things into the equalizer
           at the T/2 rate. This routine adapts the position of the half baud samples,
           which the caller takes. */

        /* On alternate insertions we have a whole baud and must process it. */
        if ((s.baud_half ^= 1) != 0)
            return;

        cc_symbol_sync(s);

        /* Slice the phase difference, to get a pair of data bits */
        ang1 = unchecked((uint)global::TKFaxEngine.FastArcTan.arctan2(sample.re, sample.im));
        ang2 = unchecked((uint)global::TKFaxEngine.FastArcTan.arctan2(s.last_sample.re, s.last_sample.im));
        ang3 = unchecked(ang1 - ang2 + unchecked((uint)Dds.PhaseDegrees(45.0f)));
        data_bits = unchecked((int)(ang3 >> 30));

        /* Descramble the data bits. */
        for (i = 0; i < 2; i++) {
            bits[i] = descramble(s, data_bits & 1);
            data_bits >>= 1;
        }

        /* Scan for MP/MPh and HDLC messages. */
        for (i = 0; i < 2; i++) {
            s.bitstream = (s.bitstream << 1) | unchecked((uint)bits[i]);
            if (s.mp_seen >= 2) {
                /* Real control channel data */
                s.put_bit!(s.put_bit_user_data, bits[i]);
                continue;
            }

            if (s.mp_seen == 1 && (s.bitstream & 0xFFFFF) == 0xFFFFF) {
                /* E is 20 consecutive ones, which signals the end of the MPh messages,
                   and the start of actual user data */
                if (s.duplex) {
                    /* TODO: start data reception */
                } else {
                    s.mp_seen = 2;
                }

            } else if ((s.bitstream & 0x7FFFE) == 0x7FFFC) {
                s.crc = 0xFFFF;
                s.bit_count = 0;
                s.mp_count = 17;
                /* Check the type bit, and set the expected length accordingly. */
                if (bits[i] != 0) {
                    s.mp_len = 186 + 1;
                    s.mp_and_fill_len = 186 + 1 + 1;
                } else {
                    s.mp_len = 84 + 1;
                    s.mp_and_fill_len = 84 + 3 + 1;
                }

            }

            if (s.mp_count >= 0) {
                s.mp_count++;
                /* Don't include the start bits in the CRC calculation. These occur every 16 bits of
                   real data - i.e. every 17 bits, including the start bits themselves. */
                if (s.mp_count % 17 != 0)
                    s.crc = CrcApi.crc_itu16_bits(unchecked((byte)bits[i]), 1, s.crc);

                s.bit_count++;
                if ((s.bit_count & 0x07) == 0)
                    s.info_buf[(s.bit_count >> 3) - 1] = global::TKFaxEngine.BitOperationsApi.bit_reverse8(unchecked((byte)(s.bitstream & 0xFF)) );

                if (s.mp_count >= s.mp_len) {
                    if (s.mp_count == s.mp_len) {
                        /* This should be the end of the MPh message */
                        if (s.crc == 0) {
                            if (s.duplex) {
                                process_rx_mp(s, mp, s.info_buf);
                                t = s.owner!;
                                if (mp.type == 1) {
                                    /* Set the precoder coefficients we are to use */
                                    Array.Copy(mp.precoder_coeffs, t.tx.precoder_coeffs, t.tx.precoder_coeffs.Length);
                                }

                                switch (mp.trellis_size) {
                                    case V34_TRELLIS_16:
                                        t.tx.conv_encode_table = v34_conv16_encode_table;
                                        break;
                                    case V34_TRELLIS_32:
                                        t.tx.conv_encode_table = v34_conv32_encode_table;
                                        break;
                                    case V34_TRELLIS_64:
                                        t.tx.conv_encode_table = v34_conv64_encode_table;
                                        break;
                                    default:
                                        LoggingApi.span_log(t.logging!, LoggingApi.SPAN_LOG_FLOW, "Rx - Unexpected trellis size code %d\n", mp.trellis_size);
                                        break;
                                }

                            } else {
                                process_rx_mph(s, mph, s.info_buf);
                                t = s.owner!;
                                if (mph.type == 1) {
                                    /* Set the precoder coefficients we are to use */
                                    Array.Copy(mph.precoder_coeffs, t.tx.precoder_coeffs, t.tx.precoder_coeffs.Length);
                                }

                                switch (mph.trellis_size) {
                                    case V34_TRELLIS_16:
                                        t.tx.conv_encode_table = v34_conv16_encode_table;
                                        break;
                                    case V34_TRELLIS_32:
                                        t.tx.conv_encode_table = v34_conv32_encode_table;
                                        break;
                                    case V34_TRELLIS_64:
                                        t.tx.conv_encode_table = v34_conv64_encode_table;
                                        break;
                                    default:
                                        LoggingApi.span_log(t.logging!, LoggingApi.SPAN_LOG_FLOW, "Rx - Unexpected trellis size code %d\n", mph.trellis_size);
                                        break;
                                }

                            }

                            s.mp_seen = 1;
                        }

                    }

                    /* Allow for the fill bits before ending the MP message */
                    if (s.mp_count == s.mp_and_fill_len)
                        s.mp_count = -1;

                }

            }

        }

        s.last_sample = sample;
    }

    private static int cc_rx(v34_rx_state_t s, ReadOnlySpan<short> amp, int offset, int len) {
        int i;
        int step;
        complexf_t z;
        complexf_t zz;
        complexf_t sample;
        float ii;
        float qq;
        float v;

        step = 6;
        Console.Error.Write(CPrintfFormatter.Format("XYX0 %d\n", new object?[] { len }));
        for (i = 0; i < len; i++) {
            s.rrc_filter[s.rrc_filter_step] = amp[offset + i];
            if (++s.rrc_filter_step >= V34_RX_FILTER_STEPS)
                s.rrc_filter_step = 0;

            //if ((power = signal_detect(s, amp[offset + i])) == 0)
            //    continue;
            //
            //if (s.training_stage == TRAINING_STAGE_PARKED)
            //    continue;
            //
            /* Only spend effort processing this data if the modem is not
               parked, after training failure. */
            s.eq_put_step -= RX_PULSESHAPER_2400_COEFF_SETS;
            step = -s.eq_put_step;
            if (step > RX_PULSESHAPER_2400_COEFF_SETS - 1)
                step = RX_PULSESHAPER_2400_COEFF_SETS - 1;

            while (step < 0)
                step += RX_PULSESHAPER_2400_COEFF_SETS;

            if (s.calling_party) {
                ii = vec_circular_dot_prodf(s.rrc_filter, s.rrc_filter_step, rx_pulseshaper_2400_re[step], V34_RX_FILTER_STEPS);
            } else {
                ii = vec_circular_dot_prodf(s.rrc_filter, s.rrc_filter_step, rx_pulseshaper_1200_re[step], V34_RX_FILTER_STEPS);
            }

            sample.re = ii * s.agc_scaling;
            /* Symbol timing synchronisation band edge filters */
            /* Low Nyquist band edge filter */
            v = s.cc_ted.symbol_sync_low[0] * s.cc_ted.low_band_edge_coeff[0] + s.cc_ted.symbol_sync_low[1] * s.cc_ted.low_band_edge_coeff[1] + sample.re;
            s.cc_ted.symbol_sync_low[1] = s.cc_ted.symbol_sync_low[0];
            s.cc_ted.symbol_sync_low[0] = v;
            /* High Nyquist band edge filter */
            v = s.cc_ted.symbol_sync_high[0] * s.cc_ted.high_band_edge_coeff[0] + s.cc_ted.symbol_sync_high[1] * s.cc_ted.high_band_edge_coeff[1] + sample.re;
            s.cc_ted.symbol_sync_high[1] = s.cc_ted.symbol_sync_high[0];
            s.cc_ted.symbol_sync_high[0] = v;

            /* Put things into the equalization buffer at T/2 rate. The symbol synchcronisation
               will fiddle the step to align this with the symbols. */
            if (s.eq_put_step <= 0) {
                /* Only AGC until we have locked down the setting. */
                //if (s.agc_scaling_save == 0.0f)
                //s.agc_scaling = (FP_SCALE(2.17f)/RX_PULSESHAPER_GAIN)/fixed_sqrt32(power);
                s.eq_put_step += RX_PULSESHAPER_2400_COEFF_SETS * 40 / (3 * 2);
                if (s.calling_party) {
                    qq = vec_circular_dot_prodf(s.rrc_filter, s.rrc_filter_step, rx_pulseshaper_2400_im[step], V34_RX_FILTER_STEPS);
                } else {
                    qq = vec_circular_dot_prodf(s.rrc_filter, s.rrc_filter_step, rx_pulseshaper_1200_im[step], V34_RX_FILTER_STEPS);
                }

                sample.im = qq * s.agc_scaling;
                z = Dds.dds_lookup_complexf(s.carrier_phase);
                zz.re = sample.re * z.re - sample.im * z.im;
                zz.im = -sample.re * z.im - sample.im * z.re;
                process_cc_half_baud(s, zz);

                //angle = unchecked((uint)global::TKFaxEngine.FastArcTan.arctan2(zz.im, zz.re));
                //printf("XYX1 %10.5f %10.5f\n", MathF.Atan2(zz.re, zz.im), MathF.Sqrt(zz.re*zz.re + zz.im*zz.im));
                Console.Error.Write(CPrintfFormatter.Format("XYX2 %10.5f %10.5f\n", new object?[] { zz.re, zz.im }));
            }

            Dds.dds_advancef(ref s.carrier_phase, s.v34_carrier_phase_rate);
        }

        return 0;
    }

    private static void process_primary_half_baud(v34_rx_state_t s, complexf_t sample) {

        /* This routine processes every half a baud, as we put things into the equalizer at the T/2 rate.
           This routine adapts the position of the half baud samples, which the caller takes. */

        /* On alternate insertions we have a whole baud and must process it. */
        if ((s.baud_half ^= 1) != 0)
            return;

        pri_symbol_sync(s);

        s.last_sample = sample;

    }

    private static int primary_channel_rx(v34_rx_state_t s, ReadOnlySpan<short> amp, int offset, int len) {
        int i;
        int step;
        complexf_t z;
        complexf_t zz;
        complexf_t sample;
        float ii;
        float qq;
        float v;
        /* The following lead to integer values for the rx increments per symbol, for each of the 6 baud rates */
        int[] steps_per_baud = new int[] {
            192*8000/2400,
            192*8000*7/(2400*8),
            189*8000*6/(2400*7),
            192*8000*4/(2400*5),
            192*8000*3/(2400*4),
            192*8000*7/(2400*10)
        };

        s.baud_rate = 5;
        switch (s.baud_rate) {
            case V34_BAUD_RATE_2400:
                s.shaper_re = s.high_carrier ? rx_pulseshaper_2400_high_carrier_re : rx_pulseshaper_2400_low_carrier_re;
                s.shaper_im = s.high_carrier ? rx_pulseshaper_2400_high_carrier_im : rx_pulseshaper_2400_low_carrier_im;
                break;
            case V34_BAUD_RATE_2743:
                s.shaper_re = s.high_carrier ? rx_pulseshaper_2743_high_carrier_re : rx_pulseshaper_2743_low_carrier_re;
                s.shaper_im = s.high_carrier ? rx_pulseshaper_2743_high_carrier_im : rx_pulseshaper_2743_low_carrier_im;
                break;
            case V34_BAUD_RATE_2800:
                s.shaper_re = s.high_carrier ? rx_pulseshaper_2800_high_carrier_re : rx_pulseshaper_2800_low_carrier_re;
                s.shaper_im = s.high_carrier ? rx_pulseshaper_2800_high_carrier_im : rx_pulseshaper_2800_low_carrier_im;
                break;
            case V34_BAUD_RATE_3000:
                s.shaper_re = s.high_carrier ? rx_pulseshaper_3000_high_carrier_re : rx_pulseshaper_3000_low_carrier_re;
                s.shaper_im = s.high_carrier ? rx_pulseshaper_3000_high_carrier_im : rx_pulseshaper_3000_low_carrier_im;
                break;
            case V34_BAUD_RATE_3200:
                s.shaper_re = s.high_carrier ? rx_pulseshaper_3200_high_carrier_re : rx_pulseshaper_3200_low_carrier_re;
                s.shaper_im = s.high_carrier ? rx_pulseshaper_3200_high_carrier_im : rx_pulseshaper_3200_low_carrier_im;
                break;
            default:
                s.shaper_re = rx_pulseshaper_3429_re;
                s.shaper_im = rx_pulseshaper_3429_im;
                break;
        }
        s.shaper_sets = steps_per_baud[s.baud_rate];
        s.v34_carrier_phase_rate = Dds.dds_phase_ratef(carrier_frequency(s.baud_rate, false));
        Console.Error.Write(CPrintfFormatter.Format("XYX0 %d\n", new object?[] { len }));
        for (i = 0; i < len; i++) {
            s.rrc_filter[s.rrc_filter_step] = amp[offset + i];
            if (++s.rrc_filter_step >= V34_RX_FILTER_STEPS)
                s.rrc_filter_step = 0;

            //if ((power = signal_detect(s, amp[offset + i])) == 0)
            //    continue;
            //
            //if (s.training_stage == TRAINING_STAGE_PARKED)
            //    continue;
            //
            /* Only spend effort processing this data if the modem is not parked, after training failure. */
            s.eq_put_step -= V34_RX_PULSESHAPER_COEFF_SETS;
            step = -s.eq_put_step;
            if (step > V34_RX_PULSESHAPER_COEFF_SETS - 1)
                step = V34_RX_PULSESHAPER_COEFF_SETS - 1;

            while (step < 0)
                step += V34_RX_PULSESHAPER_COEFF_SETS;

            ii = vec_circular_dot_prodf(s.rrc_filter, s.rrc_filter_step, s.shaper_re!, step, V34_RX_FILTER_STEPS);
            sample.re = ii * s.agc_scaling;
            /* Symbol timing synchronisation band edge filters */
            /* Low Nyquist band edge filter */
            v = s.pri_ted.symbol_sync_low[0] * s.pri_ted.low_band_edge_coeff[0] + s.pri_ted.symbol_sync_low[1] * s.pri_ted.low_band_edge_coeff[1] + sample.re;
            s.pri_ted.symbol_sync_low[1] = s.pri_ted.symbol_sync_low[0];
            s.pri_ted.symbol_sync_low[0] = v;
            /* High Nyquist band edge filter */
            v = s.pri_ted.symbol_sync_high[0] * s.pri_ted.high_band_edge_coeff[0] + s.pri_ted.symbol_sync_high[1] * s.pri_ted.high_band_edge_coeff[1] + sample.re;
            s.pri_ted.symbol_sync_high[1] = s.pri_ted.symbol_sync_high[0];
            s.pri_ted.symbol_sync_high[0] = v;

            /* Put things into the equalization buffer at T/2 rate. The symbol synchcronisation
               will fiddle the step to align this with the symbols. */
            if (s.eq_put_step <= 0) {
                /* Only AGC until we have locked down the setting. */
                //if (s.agc_scaling_save == 0.0f)
                //    s.agc_scaling = (FP_SCALE(2.17f)/RX_PULSESHAPER_GAIN)/fixed_sqrt32(power);
                //
                s.eq_put_step += s.shaper_sets;
                qq = vec_circular_dot_prodf(s.rrc_filter, s.rrc_filter_step, s.shaper_im!, step, V34_RX_FILTER_STEPS);
                sample.im = qq * s.agc_scaling;
                z = Dds.dds_lookup_complexf(s.carrier_phase);
                zz.re = sample.re * z.re - sample.im * z.im;
                zz.im = -sample.re * z.im - sample.im * z.re;
                process_primary_half_baud(s, zz);

                //angle = unchecked((uint)global::TKFaxEngine.FastArcTan.arctan2(zz.im, zz.re));
                Console.Error.Write(CPrintfFormatter.Format("XYX1 %10.5f %10.5f\n", new object?[] { MathF.Atan2(zz.re, zz.im), MathF.Sqrt(zz.re * zz.re + zz.im * zz.im) }));
                Console.Error.Write(CPrintfFormatter.Format("XYX2 %10.5f %10.5f\n", new object?[] { zz.re, zz.im }));
            }

            Dds.dds_advancef(ref s.carrier_phase, s.v34_carrier_phase_rate);
        }

        return 0;
    }

    public static void v34_put_mapping_frame(v34_rx_state_t s, short[] bits) {
        int i;
        int j;
        int constel;
        int invert;
        complexi16_t c;
        complexi16_t p;
        complexi16_t u;
        complexi16_t v;
        complexi16_t[] y = new complexi16_t[2];

        /* Put the four 4D symbols (eight 2D symbols) of a mapping frame */
        for (i = 0; i < 8; i++) {
            s.xt[0].re = bits[2 * i];
            s.xt[0].im = bits[2 * i + 1];
            //printf("AMZ %p [%6d, %6d] [%8.3f, %8.3f]\n", s, s.xt[0].re, s.xt[0].im, FP_Q9_7_TO_F(s.xt[0].re), FP_Q9_7_TO_F(s.xt[0].im));
            s.yt = prediction_error_filter(s);
            quantize_n_ways(s.xy, i & 1, s.yt);
            //printf("CCC %p [%8.3f, %8.3f] [%8.3f, %8.3f] [%8.3f, %8.3f] [%8.3f, %8.3f]\n",
            //       s,
            //       FP_Q9_7_TO_F(s.xy[i & 1, 0].re),
            //       FP_Q9_7_TO_F(s.xy[i & 1, 0].im),
            //       FP_Q9_7_TO_F(s.xy[i & 1, 1].re),
            //       FP_Q9_7_TO_F(s.xy[i & 1, 1].im),
            //       FP_Q9_7_TO_F(s.xy[i & 1, 2].re),
            //       FP_Q9_7_TO_F(s.xy[i & 1, 2].im),
            //       FP_Q9_7_TO_F(s.xy[i & 1, 3].re),
            //       FP_Q9_7_TO_F(s.xy[i & 1, 3].im));
            viterbi_calculate_candidate_errors(s.viterbi.error, i & 1, s.xy, i & 1, s.yt);
            y[i & 1].re = s.xt[0].re;
            y[i & 1].im = s.xt[0].im;
            //printf("CCD %p [%8.3f, %8.3f]\n", s, FP_Q9_7_TO_F(y[i & 1].re), FP_Q9_7_TO_F(y[i & 1].im));
            if ((i & 1) != 0) {
                /* Deal with super-frame sync inversion */
                if ((s.data_frame * 8 + s.step_2d) % (4 * s.parms.p) == 0)
                    invert = (0x5FEE >> s.v0_pattern++) & 1;
                else
                    invert = 0;

                viterbi_calculate_branch_errors(s.viterbi, s.xy, invert);
                viterbi_update_path_metrics(s.viterbi);
                //printf("EEE %p %4d %4d %4d %4d %4d %4d %4d %4d (%d)\n",
                //       s,
                //       s.viterbi.branch_error[0],
                //       s.viterbi.branch_error[1],
                //       s.viterbi.branch_error[2],
                //       s.viterbi.branch_error[3],
                //       s.viterbi.branch_error[4],
                //       s.viterbi.branch_error[5],
                //       s.viterbi.branch_error[6],
                //       s.viterbi.branch_error[7],
                //       s.viterbi.windup);
                if (s.viterbi.windup != 0) {
                    /* Wait for the Viterbi buffer to fill with symbols. */
                    s.viterbi.windup--;
                } else {
                    viterbi_trace_back(s.viterbi, y);
                    /* We now have two points in y to be decoded. They are in Q9.7 format. */
                    //printf("AAA %p [%8.3f, %8.3f] [%8.3f, %8.3f]\n",
                    //       s,
                    //       FP_Q9_7_TO_F(y[0].re),
                    //       FP_Q9_7_TO_F(y[0].im),
                    //       FP_Q9_7_TO_F(y[1].re),
                    //       FP_Q9_7_TO_F(y[1].im));
                    for (j = 0; j < 2; j++) {
                        p = precoder_rx_filter(s);

                        c = quantize_rx(s, p);
                        s.x[0].re = unchecked((short)(y[j].re - p.re));
                        s.x[0].im = unchecked((short)(y[j].im - p.im));
                        u.re = unchecked((short)((y[j].re >> 7) - c.re));
                        u.im = unchecked((short)((y[j].im >> 7) - c.im));

                        s.ww[j + 1] = get_binary_subset_label(u);
                        v = rotate90_counterclockwise(u, s.ww[j + 1]);
                        constel = get_inverse_constellation_point(v);
                        //printf("AMQ %p %d [%d, %d] [%d, %d] %d\n", s, constel, v.re, v.im, u.re, u.im, s.ww[j + 1]);
                        //printf("AMQ %p [%6d, %6d] (%d) [%6d, %6d] [%8.3f, %8.3f]\n", s, v.re, v.im, s.ww[j + 1], u.re, u.im, FP_Q9_7_TO_F(y[j].re), FP_Q9_7_TO_F(y[j].im));
                        s.qbits[s.step_2d + j] = unchecked((ushort)(constel & s.parms.q_mask));
                        s.mjk[s.step_2d + j] = constel >> s.parms.q;
                    }

                    /* Compute the I bits */
                    s.ibits[s.step_2d >> 1] = unchecked((ushort)((((s.ww[1] - s.ww[0]) & 3) << 1)
                                              | (((s.ww[2] - s.ww[1]) >> 1) & 1)));
                    s.ww[0] = s.ww[1];
                    s.step_2d += 2;
                    if (s.step_2d == 8) {
                        shell_unmap(s);
                        pack_output_bitstream(s);
                        if (++s.data_frame >= s.parms.p) {
                            s.data_frame = 0;
                            if (++s.super_frame >= s.parms.j) {
                                s.super_frame = 0;
                                s.v0_pattern = 0;
                            }

                        }

                        //printf("ZAQ data frame %d, super frame %d\n", s.data_frame, s.super_frame);
                        s.step_2d = 0;
                    }

                }

                s.viterbi.ptr = (s.viterbi.ptr + 1) & 0xF;
            }

        }

    }

    public static int v34_rx_fillin(v34_state_t s, int len) {
        int i;

        /* We want to sustain the current state (i.e carrier on<.carrier off), and
           try to sustain the carrier phase. We should probably push the filters, as well */
        LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Rx - Fill-in %d samples\n", len);
        for (i = 0; i < len; i++) {
            Dds.dds_advancef(ref s.rx.carrier_phase, s.rx.v34_carrier_phase_rate);
        }

        return 0;
    }

    public static int v34_rx(v34_state_t s, ReadOnlySpan<short> amp, int len) {
        int leny;
        int lenx;

        leny = 0;
        lenx = -1;
        do {
            switch (s.rx.current_demodulator) {
                case V34_MODULATION_V34:
                    lenx = primary_channel_rx(s.rx, amp, leny, len - leny);
                    break;
                case V34_MODULATION_CC:
                    lenx = cc_rx(s.rx, amp, leny, len - leny);
                    break;
                case V34_MODULATION_L1_L2:
                    lenx = l1_l2_analysis(s.rx, amp, leny, len - leny);
                    break;
                case V34_MODULATION_TONES:
                    lenx = info_rx(s.rx, amp, leny, len - leny);
                    break;
            }

            leny += lenx;
            /* Add step by step, so each segment is seen up to date */
            s.rx.sample_time += lenx;
        }
        while (lenx > 0 && leny < len);
        /* If there is any residue, this should be the end of operation of the modem,
           so we don't really need to add that residue to the sample time. */
        return leny;
    }

    public static void v34_rx_set_signal_cutoff(v34_state_t s, float cutoff) {
        /* The 0.4 factor allows for the gain of the DC blocker */
        s.rx.carrier_on_power = (int)(PowerMeter.LevelDbm0(cutoff + 2.5f) * 0.4f);
        s.rx.carrier_off_power = (int)(PowerMeter.LevelDbm0(cutoff - 2.5f) * 0.4f);
    }

    public static void v34_set_put_bit(v34_state_t s, span_put_bit_func_t put_bit, object? user_data) {
        s.rx.put_bit = put_bit;
        s.rx.put_bit_user_data = user_data;
    }

    public static void v34_set_put_aux_bit(v34_state_t s, span_put_bit_func_t put_bit, object? user_data) {
        s.rx.put_aux_bit = put_bit;
        s.rx.put_aux_bit_user_data = user_data;
    }

    public static int v34_rx_restart(v34_state_t s, int baud_rate, int bit_rate, int high_carrier) {
        int i;

        s.rx.owner = s;
        s.rx.baud_rate = baud_rate;
        s.rx.bit_rate = bit_rate;
        s.rx.high_carrier = high_carrier != 0;

        s.rx.v34_carrier_phase_rate = Dds.dds_phase_ratef(carrier_frequency(s.rx.baud_rate, s.rx.high_carrier));
        s.rx.cc_carrier_phase_rate = Dds.dds_phase_ratef((s.calling_party) ? 2400.0f : 1200.0f);
        v34_set_working_parameters(s.rx.parms, s.rx.baud_rate, s.rx.bit_rate, true);

        s.rx.high_sample = 0;
        s.rx.low_samples = 0;
        s.rx.carrier_drop_pending = 0;

        s.rx.power.Initialize(4);

        s.rx.carrier_phase = 0;
        s.rx.agc_scaling_save = 0.0f;
        s.rx.agc_scaling = 0.0017f / V34_RX_PULSESHAPER_GAIN;
        //equalizer_reset(s.rx);
        s.rx.carrier_track_i = 5000.0f;
        s.rx.carrier_track_p = 40000.0f;

        /* Create a default symbol sync filter */
        create_godard_coeffs(s.rx.pri_ted,
                             (s.rx.high_carrier ? 1.0f : 0.0f),
                             s.rx.baud_rate,
                             0.99f);
        create_godard_coeffs(s.rx.cc_ted,
                             (s.calling_party) ? 2400.0f : 1200.0f,
                             600,
                             0.99f);
        /* Initialise the working data for symbol timing synchronisation */
        for (i = 0; i < 2; i++) {
            s.rx.pri_ted.symbol_sync_low[i] = 0.0f;
            s.rx.pri_ted.symbol_sync_high[i] = 0.0f;
            s.rx.pri_ted.symbol_sync_dc_filter[i] = 0.0f;
        }

        s.rx.pri_ted.baud_phase = 0.0f;
        for (i = 0; i < 2; i++) {
            s.rx.cc_ted.symbol_sync_low[i] = 0.0f;
            s.rx.cc_ted.symbol_sync_high[i] = 0.0f;
            s.rx.cc_ted.symbol_sync_dc_filter[i] = 0.0f;
        }

        s.rx.cc_ted.baud_phase = 0.0f;
        s.rx.baud_half = 0;

        s.rx.bitstream = 0;
        s.rx.bit_count = 0;
        s.rx.duration = 0;
        s.rx.blip_duration = 0;
        s.rx.last_angles[0] = 0;
        s.rx.last_angles[1] = 0;
        s.rx.total_baud_timing_correction = 0;

        s.rx.stage = V34_RX_STAGE_INFO0;
        /* The next info message will be INFO0 or INFOH, depending whether we are in half or full duplex mode. */
        s.rx.target_bits = (s.rx.duplex) ? (49 - (4 + 8 + 4)) : (51 - (4 + 8 + 4));

        s.rx.mp_count = -1;
        s.rx.mp_len = 0;
        s.rx.mp_seen = -1;

        s.rx.viterbi.ptr = 0;
        s.rx.viterbi.windup = 15;

        s.rx.eq_put_step = RX_PULSESHAPER_2400_COEFF_SETS * 40 / (3 * 2) - 1;
        s.rx.eq_step = 0;
        s.rx.scramble_reg = 0;

        s.rx.current_demodulator = V34_MODULATION_TONES;
        s.rx.viterbi.conv_decode_table = v34_conv16_decode_table;

        s.rx.v0_pattern = 0;
        s.rx.super_frame = 0;
        s.rx.data_frame = 0;
        s.rx.s_bit_cnt = 0;
        s.rx.aux_bit_cnt = 0;

        return 0;
    }

    public static void v34_set_qam_report_handler(v34_state_t s, qam_report_handler_t handler, object? user_data) {
        s.rx.qam_report = handler;
        s.rx.qam_user_data = user_data;
    }
}
