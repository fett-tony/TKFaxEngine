/*
 * TKFaxEngine - direct C# conversion of the TKFaxEngineFX/spanDSP V.34 sources.
 *
 * v34tx.cs - ITU V.34 modem, transmit part.
 * Direct translation of v34tx.c.
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2009 Steve Underwood.
 * Licensed under the GNU Lesser General Public License version 2.1.
 *
 * THIS IS A WORK IN PROGRESS - NOT YET FUNCTIONAL!
 * This status is inherited unchanged from the original V.34 source.
 */

#nullable enable

using TKFaxEngine.Audio;
using TKFaxEngine.Modem.V22;

namespace TKFaxEngine.Modem.V34;

public static partial class v34 {
    private static int scramble(v34_tx_state_t s, int in_bit) {
        int out_bit;

        out_bit = (in_bit ^ (int)(s.scramble_reg >> s.scrambler_tap) ^ (int)(s.scramble_reg >> (23 - 1))) & 1;
        s.scramble_reg = (s.scramble_reg << 1) | unchecked((uint)out_bit);
        return out_bit;
    }

    private const float FP_Q9_7_TO_F_SCALE = 1.0f / 128.0f;
    private const float EQUALIZER_DELTA = 0.21f;
    private const float EQUALIZER_SLOW_ADAPT_RATIO = 0.1f;
    private const int V34_TRAINING_SEG_1 = 0;
    private const int V34_TRAINING_SEG_4 = 0;
    private const int V34_TRAINING_END = 0;
    private const int V34_TRAINING_SHUTDOWN_END = 0;
    private const int SH_PLUS_NO_SH_SYMBOLS = 32;
    private const int INFO_FILL_AND_SYNC_BITS = 0x4EF;
    private const float TRAINING_AMP = 10.0f;
    private const int TX_PULSESHAPER_COEFF_SETS = V22BisTxRrc.CoefficientSets;
    private const float TX_PULSESHAPER_GAIN = V22BisTxRrc.FloatingPointGain;
    private static readonly float[][] tx_pulseshaper = V22BisTxRrc.TxPulseShaper;

    private const int TRAINING_TX_STAGE_NORMAL_OPERATION_V34 = 0;
    private const int TRAINING_TX_STAGE_NORMAL_OPERATION_CC = 1;
    private const int TRAINING_TX_STAGE_PARKED = 2;

    private static readonly complexf_t zero = new(0.0f, 0.0f);

    private static readonly complexf_t[] training_constellation_4 =
    {
        new(-0.7071068f * TRAINING_AMP, -0.7071068f * TRAINING_AMP),   /* 225 degrees */
        new(-0.7071068f * TRAINING_AMP,  0.7071068f * TRAINING_AMP),   /* 135 degrees */
        new( 0.7071068f * TRAINING_AMP,  0.7071068f * TRAINING_AMP),   /*  45 degrees */
        new( 0.7071068f * TRAINING_AMP, -0.7071068f * TRAINING_AMP)    /* 315 degrees */
    };

    private static readonly complexf_t[] training_constellation_16 =
    {
        new(-1.0f * TRAINING_AMP, -1.0f * TRAINING_AMP),
        new(-1.0f * TRAINING_AMP,  1.0f * TRAINING_AMP),
        new( 1.0f * TRAINING_AMP,  1.0f * TRAINING_AMP),
        new( 1.0f * TRAINING_AMP, -1.0f * TRAINING_AMP),

        new( 3.0f * TRAINING_AMP, -1.0f * TRAINING_AMP),
        new(-1.0f * TRAINING_AMP, -3.0f * TRAINING_AMP),
        new(-3.0f * TRAINING_AMP,  1.0f * TRAINING_AMP),
        new( 1.0f * TRAINING_AMP,  3.0f * TRAINING_AMP),

        new(-1.0f * TRAINING_AMP,  3.0f * TRAINING_AMP),
        new( 3.0f * TRAINING_AMP,  1.0f * TRAINING_AMP),
        new( 1.0f * TRAINING_AMP, -3.0f * TRAINING_AMP),
        new(-3.0f * TRAINING_AMP, -1.0f * TRAINING_AMP),

        new( 3.0f * TRAINING_AMP,  3.0f * TRAINING_AMP),
        new( 3.0f * TRAINING_AMP, -3.0f * TRAINING_AMP),
        new(-3.0f * TRAINING_AMP, -3.0f * TRAINING_AMP),
        new(-3.0f * TRAINING_AMP,  3.0f * TRAINING_AMP)
    };

    private static ushort crc_bit_block(byte[] buf, int first_bit, int last_bit, ushort crc) {
        last_bit++;
        int pre = first_bit & 0x7;
        first_bit >>= 3;
        if (pre != 0) {
            crc = CrcApi.crc_itu16_bits(unchecked((byte)(buf[first_bit] >> pre)), 8 - pre, crc);
            first_bit++;
        }
        int post = last_bit & 0x7;
        last_bit >>= 3;
        if (last_bit - first_bit != 0)
            crc = CrcApi.crc_itu16_calc(buf.AsSpan(first_bit), last_bit - first_bit, crc);
        if (post != 0)
            crc = CrcApi.crc_itu16_bits(buf[last_bit], post, crc);
        return crc;
    }

    private static void prepare_info1c(v34_state_t s) {
        s.tx.info1c.power_reduction = 0;
        s.tx.info1c.additional_power_reduction = 0;
        s.tx.info1c.md = 0;
        s.tx.info1c.freq_offset = 0;
        for (int i = 0; i <= V34_BAUD_RATE_3429; i++) {
            s.tx.info1c.rate_data[i].use_high_carrier = false;
            s.tx.info1c.rate_data[i].pre_emphasis = 6;
            s.tx.info1c.rate_data[i].max_bit_rate = s.tx.baud_rate >= i ? ((s.tx.parms.max_bit_rate_code >> 1) + 1) : 0;
        }
    }

    private static void prepare_info1a(v34_state_t s) {
        s.tx.info1a.power_reduction = 0;
        s.tx.info1a.additional_power_reduction = 0;
        s.tx.info1a.md = 0;
        s.tx.info1a.freq_offset = 0;
        s.tx.info1a.use_high_carrier = false;
        s.tx.info1a.preemphasis_filter = 6;
        s.tx.info1a.max_data_rate = s.tx.parms.max_bit_rate_code;
        s.tx.info1a.baud_rate_a_to_c = s.tx.baud_rate;
        s.tx.info1a.baud_rate_c_to_a = s.tx.baud_rate;
    }

    private static void prepare_infoh(v34_state_t s) {
        s.tx.infoh.power_reduction = 0;
        s.tx.infoh.length_of_trn = 30;
        s.tx.infoh.use_high_carrier = false;
        s.tx.infoh.preemphasis_filter = 0;
        s.tx.infoh.baud_rate = 14;
        s.tx.infoh.trn16 = false;
    }
    private static int info0_sequence_tx(v34_tx_state_t s) {
        int t = 0;
        ushort crc;
        bitstream_state_t bs = new();
        log_info0(s.logging!, true, v34_capabilities, s.info0_acknowledgement ? 1 : 0);
        bitstream_init(bs, true);
        /* 0:3      Fill bits: 1111. */
        /* 4:11     Frame sync: 01110010, where the left-most bit is first in time. */
        bitstream_put(bs, s.txbuf, ref t, INFO_FILL_AND_SYNC_BITS, 12);
        /* 12       Set to 1 indicates symbol rate 2743 is supported. */
        bitstream_put(bs, s.txbuf, ref t, (v34_capabilities.support_baud_rate_low_carrier[V34_BAUD_RATE_2743]) ? 1 : 0, 1);
        /* 13       Set to 1 indicates symbol rate 2800 is supported. */
        bitstream_put(bs, s.txbuf, ref t, (v34_capabilities.support_baud_rate_low_carrier[V34_BAUD_RATE_2800]) ? 1 : 0, 1);
        /* 14       Set to 1 indicates symbol rate 3429 is supported. */
        bitstream_put(bs, s.txbuf, ref t, (v34_capabilities.support_baud_rate_low_carrier[V34_BAUD_RATE_3429]) ? 1 : 0, 1);
        /* 15       Set to 1 indicates the ability to transmit at the low carrier frequency with a symbol rate of 3000. */
        bitstream_put(bs, s.txbuf, ref t, (v34_capabilities.support_baud_rate_low_carrier[V34_BAUD_RATE_3000]) ? 1 : 0, 1);
        /* 16       Set to 1 indicates the ability to transmit at the high carrier frequency with a symbol rate of 3000. */
        bitstream_put(bs, s.txbuf, ref t, (v34_capabilities.support_baud_rate_high_carrier[V34_BAUD_RATE_3000]) ? 1 : 0, 1);
        /* 17       Set to 1 indicates the ability to transmit at the low carrier frequency with a symbol rate of 3200. */
        bitstream_put(bs, s.txbuf, ref t, (v34_capabilities.support_baud_rate_low_carrier[V34_BAUD_RATE_3200]) ? 1 : 0, 1);
        /* 18       Set to 1 indicates the ability to transmit at the high carrier frequency with a symbol rate of 3200. */
        bitstream_put(bs, s.txbuf, ref t, (v34_capabilities.support_baud_rate_high_carrier[V34_BAUD_RATE_3200]) ? 1 : 0, 1);
        /* 19       Set to 0 indicates that transmission with a symbol rate of 3429 is disallowed. */
        bitstream_put(bs, s.txbuf, ref t, (v34_capabilities.rate_3429_allowed) ? 1 : 0, 1);
        /* 20       Set to 1 indicates the ability to reduce transmit power to a value lower than the nominal setting. */
        bitstream_put(bs, s.txbuf, ref t, (v34_capabilities.support_power_reduction) ? 1 : 0, 1);
        /* 21:23    Maximum allowed difference in symbol rates in the transmit and receive directions. With the symbol rates
                    labelled in increasing order, where 0 represents 2400 and 5 represents 3429, an integer between 0 and 5
                    indicates the difference allowed in number of symbol rate steps. */
        bitstream_put(bs, s.txbuf, ref t, v34_capabilities.max_baud_rate_difference, 3);
        /* 24       Set to 1 in an INFO0 sequence transmitted from a CME modem. */
        bitstream_put(bs, s.txbuf, ref t, (v34_capabilities.from_cme_modem ? 1L : 0L), 1);
        /* 25       Set to 1 indicates the ability to support up to 1664-point signal constellations. */
        bitstream_put(bs, s.txbuf, ref t, (v34_capabilities.support_1664_point_constellation) ? 1 : 0, 1);
        /* 26:27    Transmit clock source: 0 = internal; 1 = synchronized to receive timing; 2 = external; 3 = reserved for ITU-T. */
        bitstream_put(bs, s.txbuf, ref t, v34_capabilities.tx_clock_source, 2);
        /* 28       Set to 1 to acknowledge correct reception of an INFO0 frame during error recovery. */
        bitstream_put(bs, s.txbuf, ref t, (s.info0_acknowledgement ? 1L : 0L), 1);
        bitstream_emit(bs, s.txbuf, t);
        crc = crc_bit_block(s.txbuf, 12, 28, 0xFFFF);
        /* 29:44    CRC. */
        bitstream_put(bs, s.txbuf, ref t, crc, 16);
        /* 45:48    Fill bits: 1111. */
        bitstream_put(bs, s.txbuf, ref t, 0xF, 4);
        /* Add some extra postamble, so we have a whole number of bytes to work with. */
        bitstream_put(bs, s.txbuf, ref t, 0, 8);
        bitstream_flush(bs, s.txbuf, ref t);
        return 49;
    }

    private static int info1c_sequence_tx(v34_tx_state_t s, info1c_t info1c) {
        int t = 0;
        ushort crc;
        bitstream_state_t bs = new();

        log_info1c(s.logging!, true, info1c);
        bitstream_init(bs, true);
        /* 0:3      Fill bits: 1111. */
        /* 4:11     Frame sync: 01110010, where the left-most bit is first in time. */
        bitstream_put(bs, s.txbuf, ref t, INFO_FILL_AND_SYNC_BITS, 12);
        /* 12:14    Minimum power reduction to be implemented by the answer modem transmitter. An integer between 0 and 7
                    gives the recommended power reduction in dB. These bits shall indicate 0 if INFO0a indicated that the answer
                    modem transmitter cannot reduce its power. */
        bitstream_put(bs, s.txbuf, ref t, info1c.power_reduction, 3);
        /* 15:17    Additional power reduction, below that indicated by bits 12-14, which can be tolerated by the call modem
                    receiver. An integer between 0 and 7 gives the additional power reduction in dB. These bits shall indicate 0 if
                    INFO0a indicated that the answer modem transmitter cannot reduce its power. */
        bitstream_put(bs, s.txbuf, ref t, info1c.additional_power_reduction, 3);
        /* 18:24    Length of MD to be transmitted by the call modem during Phase 3. An integer between 0 and 127 gives the
                    length of this sequence in 35 ms increments. */
        bitstream_put(bs, s.txbuf, ref t, info1c.md, 7);
        /* 25       Set to 1 indicates that the high carrier frequency is to be used in transmitting from the answer modem to the call
                    modem for a symbol rate of 2400. */
        /* 26:29    Pre-emphasis filter to be used in transmitting from the answer modem to the call modem for a symbol
                    rate of 2400. These bits form an integer between 0 and 10 which represents the pre-emphasis filter index
                    (see Tables 3 and 4). */
        /* 30:33    Projected maximum data rate for a symbol rate of 2400. These bits form an integer between 0 and 14 which
                    gives the projected data rate as a multiple of 2400 bits/s. A 0 indicates the symbol rate cannot be used. */

        /* 34:42    Probing results pertaining to a final symbol rate selection of 2743 symbols per second. The coding of these
                    9 bits is identical to that for bits 25-33. */

        /* 43:51    Probing results pertaining to a final symbol rate selection of 2800 symbols per second. The coding of these
                    9 bits is identical to that for bits 25-33. */

        /* 52:60    Probing results pertaining to a final symbol rate selection of 3000 symbols per second. The coding of these
                    9 bits is identical to that for bits 25-33. Information in this field shall be consistent with the answer modem
                    capabilities indicated in INFO0a. */

        /* 61:69    Probing results pertaining to a final symbol rate selection of 3200 symbols per second. The coding of these
                    9 bits is identical to that for bits 25-33. Information in this field shall be consistent with the answer modem
                    capabilities indicated in INFO0a. */

        /* 70:78    Probing results pertaining to a final symbol rate selection of 3429 symbols per second. The coding of these
                    9 bits is identical to that for bits 25-33. Information in this field shall be consistent with the answer modem
                    capabilities indicated in INFO0a. */
        for (int i = 0; i <= 5; i++) {
            bitstream_put(bs, s.txbuf, ref t, (info1c.rate_data[i].use_high_carrier ? 1L : 0L), 1);
            bitstream_put(bs, s.txbuf, ref t, info1c.rate_data[i].pre_emphasis, 4);
            bitstream_put(bs, s.txbuf, ref t, info1c.rate_data[i].max_bit_rate, 4);
        }
        /* 79:88    Frequency offset of the probing tones as measured by the call modem receiver. The frequency offset number
                    shall be the difference between the nominal 1050 Hz line probing signal tone received and the 1050 Hz tone
                    transmitted, f(received) and f(transmitted). A two's complement signed integer between -511 and 511 gives the
                    measured offset in 0.02 Hz increments. Bit 88 is the sign bit of this integer. The frequency offset measurement
                    shall be accurate to 0.25 Hz. Under conditions where this accuracy cannot be achieved, the integer shall be set
                    to -512 indicating that this field is to be ignored. */
        bitstream_put(bs, s.txbuf, ref t, info1c.freq_offset, 10);
        bitstream_emit(bs, s.txbuf, t);
        crc = crc_bit_block(s.txbuf, 12, 88, 0xFFFF);
        /* 89:104   CRC. */
        bitstream_put(bs, s.txbuf, ref t, crc, 16);
        /* 105:108  Fill bits: 1111. */
        bitstream_put(bs, s.txbuf, ref t, 0xF, 4);
        /* Add some extra postamble, so we have a whole number of bytes to work with. */
        bitstream_put(bs, s.txbuf, ref t, 0, 8);
        bitstream_flush(bs, s.txbuf, ref t);
        return 109;
    }

    private static int info1a_sequence_tx(v34_tx_state_t s, info1a_t info1a) {
        int t = 0;
        ushort crc;
        bitstream_state_t bs = new();
        log_info1a(s.logging!, true, info1a);
        bitstream_init(bs, true);
        /* 0:3      Fill bits: 1111. */
        /* 4:11     Frame sync: 01110010, where the left-most bit is first in time. */
        bitstream_put(bs, s.txbuf, ref t, INFO_FILL_AND_SYNC_BITS, 12);
        /* 12:14    Minimum power reduction to be implemented by the call modem transmitter. An integer between 0 and 7 gives
                    the recommended power reduction in dB. These bits shall indicate 0 if INFO0c indicated that the call modem
                    transmitter cannot reduce its power. */
        bitstream_put(bs, s.txbuf, ref t, info1a.power_reduction, 3);
        /* 15:17    Additional power reduction, below that indicated by bits 12:14, which can be tolerated by the answer modem
                    receiver. An integer between 0 and 7 gives the additional power reduction in dB. These bits shall indicate 0 if
                    INFO0c indicated that the call modem transmitter cannot reduce its power. */
        bitstream_put(bs, s.txbuf, ref t, info1a.additional_power_reduction, 3);
        /* 18:24    Length of MD to be transmitted by the answer modem during Phase 3. An integer between 0 and 127 gives the
                    length of this sequence in 35 ms increments. */
        bitstream_put(bs, s.txbuf, ref t, info1a.md, 7);
        /* 25       Set to 1 indicates that the high carrier frequency is to be used in transmitting from the call modem to the answer
                    modem. This shall be consistent with the capabilities of the call modem indicated in INFO0c. */
        bitstream_put(bs, s.txbuf, ref t, (info1a.use_high_carrier ? 1L : 0L), 1);
        /* 26:29    Pre-emphasis filter to be used in transmitting from the call modem to the answer modem. These bits form an
                    integer between 0 and 10 which represents the pre-emphasis filter index (see Tables 3 and 4). */
        bitstream_put(bs, s.txbuf, ref t, info1a.preemphasis_filter, 4);
        /* 30:33    Projected maximum data rate for the selected symbol rate from the call modem to the answer modem. These bits
                    form an integer between 0 and 14 which gives the projected data rate as a multiple of 2400 bits/s. */
        bitstream_put(bs, s.txbuf, ref t, info1a.max_data_rate, 4);
        /* 34:36    Symbol rate to be used in transmitting from the answer modem to the call modem. An integer between 0 and 5
                    gives the symbol rate, where 0 represents 2400 and a 5 represents 3429. The symbol rate selected shall be
                    consistent with information in INFO1c and consistent with the symbol rate asymmetry allowed as indicated in
                    INFO0a and INFO0c. The carrier frequency and pre-emphasis filter to be used are those already indicated for
                    this symbol rate in INFO1c. */
        bitstream_put(bs, s.txbuf, ref t, info1a.baud_rate_a_to_c, 3);
        /* 37:39    Symbol rate to be used in transmitting from the call modem to the answer modem. An integer between 0 and 5
                    gives the symbol rate, where 0 represents 2400 and a 5 represents 3429. The symbol rate selected shall be
                    consistent with the capabilities indicated in INFO0a and consistent with the symbol rate asymmetry allowed as
                    indicated in INFO0a and INFO0c. */
        bitstream_put(bs, s.txbuf, ref t, info1a.baud_rate_c_to_a, 3);
        /* 40:49    Frequency offset of the probing tones as measured by the answer modem receiver. The frequency offset number
                    shall be the difference between the nominal 1050 Hz line probing signal tone received and the 1050 Hz tone
                    transmitted, f(received) and f(transmitted). A two's complement signed integer between -511 and 511 gives the
                    measured offset in 0.02 Hz increments. Bit 49 is the sign bit of this integer. The frequency offset measurement
                    shall be accurate to 0.25 Hz. Under conditions where this accuracy cannot be achieved, the integer shall be set
                    to -512 indicating that this field is to be ignored. */
        bitstream_put(bs, s.txbuf, ref t, info1a.freq_offset, 10);
        bitstream_emit(bs, s.txbuf, t);
        crc = crc_bit_block(s.txbuf, 12, 49, 0xFFFF);
        /* 50:65    CRC. */
        bitstream_put(bs, s.txbuf, ref t, crc, 16);
        /* 66:69    Fill bits: 1111. */
        bitstream_put(bs, s.txbuf, ref t, 0xF, 4);
        /* Add some extra postamble, so we have a whole number of bytes to work with. */
        bitstream_put(bs, s.txbuf, ref t, 0, 8);
        bitstream_flush(bs, s.txbuf, ref t);
        return 70;
    }

    private static int infoh_sequence_tx(v34_tx_state_t s, infoh_t infoh) {
        int t = 0;
        ushort crc;
        bitstream_state_t bs = new();
        log_infoh(s.logging!, true, infoh);
        bitstream_init(bs, true);
        /* 0:3      Fill bits: 1111. */
        /* 4:11     Frame sync: 01110010, where the left-most bit is first in time. */
        bitstream_put(bs, s.txbuf, ref t, INFO_FILL_AND_SYNC_BITS, 12);
        /* 12:14    Power reduction requested by the recipient modem receiver. An integer between 0 and 7
                    gives the requested power reduction in dB. These bits shall indicate 0 if the source
                    modem's INFO0 indicated that the source modem transmitter cannot reduce its power. */
        bitstream_put(bs, s.txbuf, ref t, infoh.power_reduction, 3);
        /* 15:21    Length of TRN to be transmitted by the source modem during Phase 3. An integer between
                    0 and 127 gives the length of this sequence in 35 ms increments. */
        bitstream_put(bs, s.txbuf, ref t, infoh.length_of_trn, 7);
        /* 22       Set to 1 indicates the high carrier frequency is to be used in data mode transmission. This
                    must be consistent with the capabilities indicated in the source modem's INFO0. */
        bitstream_put(bs, s.txbuf, ref t, (infoh.use_high_carrier ? 1L : 0L), 1);
        /* 23:26    Pre-emphasis filter to be used in transmitting from the source modem to the recipient modem.
                    These bits form an integer between 0 and 10 which represents the pre-emphasis filter index
                    (see Tables 3 and 4). */
        bitstream_put(bs, s.txbuf, ref t, infoh.preemphasis_filter, 4);
        /* 27:29    Symbol rate to be used for data transmission. An integer between 0 and 5 gives the symbol rate, where 0
                    represents 2400 and a 5 represents 3429. */
        bitstream_put(bs, s.txbuf, ref t, infoh.baud_rate, 3);
        /* 30       Set to 1 indicates TRN uses a 16-point constellation, 0 indicates TRN uses a 4-point constellation. */
        bitstream_put(bs, s.txbuf, ref t, (infoh.trn16 ? 1L : 0L), 1);
        bitstream_emit(bs, s.txbuf, t);
        crc = crc_bit_block(s.txbuf, 12, 30, 0xFFFF);
        /* 31:46    Code CRC. */
        bitstream_put(bs, s.txbuf, ref t, crc, 16);
        /* 47:50    Fill bits: 1111. */
        bitstream_put(bs, s.txbuf, ref t, 0xF, 4);
        /* Add some extra postamble, so we have a whole number of bytes to work with. */
        bitstream_put(bs, s.txbuf, ref t, 0, 8);
        bitstream_flush(bs, s.txbuf, ref t);
        return 51;
    }

    private static int mp_sequence_tx(v34_tx_state_t s, mp_t mp) {
        int len;
        int t = 0;
        ushort crc;
        bitstream_state_t bs = new();
        log_mp(s.logging!, true, mp);
        bitstream_init(bs, true);
        /* 0:16     Frame sync: 11111111111111111. */
        /* 17       Start bit: 0. */
        bitstream_put(bs, s.txbuf, ref t, 0x1FFFF, 18);
        /* 18       Type: 0 or 1. */
        bitstream_put(bs, s.txbuf, ref t, mp.type, 1);
        /* 19       Reserved for ITU-T: This bit is set to 0 by the transmitting modem and is not
                    interpreted by the receiving modem. */
        bitstream_put(bs, s.txbuf, ref t, 0, 1);
        /* 20:23    Maximum call modem to answer modem data signalling rate: Data rate = N * 2400
                    where N is a four-bit integer between 1 and 14. */
        bitstream_put(bs, s.txbuf, ref t, mp.bit_rate_c_to_a, 4);
        /* 24:27    Maximum answer modem to call modem data signalling rate: Data rate = N * 2400
                    where N is a four-bit integer between 1 and 14. */
        bitstream_put(bs, s.txbuf, ref t, mp.bit_rate_a_to_c, 4);
        /* 28       Auxiliary channel select bit. Set to 1 if modem is capable of supporting and
                    enables auxiliary channel. Auxiliary channel is used only if both modems set
                    this bit to 1. */
        bitstream_put(bs, s.txbuf, ref t, mp.aux_channel_supported, 1);
        /* 29:30    Trellis encoder select bits:
                    0 = 16 state; 1 = 32 state; 2 = 64 state; 3 = Reserved for ITU-T.
                    Receiver requires remote-end transmitter to use selected trellis encoder. */
        bitstream_put(bs, s.txbuf, ref t, mp.trellis_size, 2);
        /* 31       Non-linear encoder parameter select bit for the remote-end transmitter.
                    0: Q = 0, 1: Q = 0.3125. */
        bitstream_put(bs, s.txbuf, ref t, (mp.use_non_linear_encoder ? 1L : 0L), 1);
        /* 32       Constellation shaping select bit for the remote-end transmitter.
                    0: minimum, 1: expanded (see Table 10). */
        bitstream_put(bs, s.txbuf, ref t, (mp.expanded_shaping ? 1L : 0L), 1);
        /* 33       Acknowledge bit. 0 = modem has not received MP from far end. 1 = received MP from far end. */
        bitstream_put(bs, s.txbuf, ref t, (mp.mp_acknowledged ? 1L : 0L), 1);
        /* 34       Start bit: 0. */
        bitstream_put(bs, s.txbuf, ref t, 0, 1);
        /* 35:49    Data signalling rate capability mask.
                    Bit 35:2400; bit 36:4800; bit 37:7200;...; bit 46:28 800; bit 47:31 200; bit 48:33 600;
                    bit 49: Reserved for ITU-T. (This bit is set to 0 by the transmitting modem and is not
                    interpreted by the receiving modem.) Bits set to 1 indicate data signalling rates supported
                    and enabled in both transmitter and receiver of modem. */
        bitstream_put(bs, s.txbuf, ref t, mp.signalling_rate_mask, 15);
        /* 50       Asymmetric data signalling rate enable. Set to 1 indicates modem capable of asymmetric
                    data signalling rates. */
        bitstream_put(bs, s.txbuf, ref t, (mp.asymmetric_rates_allowed ? 1L : 0L), 1);
        if (mp.type == 1) {
            /* 51       Start bit: 0. */
            /* 52:67    Precoding coefficient h(1) real. */
            /* 68       Start bit: 0. */
            /* 69:84    Precoding coefficient h(1) imaginary. */
            /* 85       Start bit: 0. */
            /* 86:101   Precoding coefficient h(2) real. */
            /* 102      Start bit: 0. */
            /* 103:118  Precoding coefficient h(2) imaginary. */
            /* 119      Start bit: 0. */
            /* 120:135  Precoding coefficient h(3) real. */
            /* 136      Start bit: 0. */
            /* 137:152  Precoding coefficient h(3) imaginary. */
            for (int i = 0; i < 3; i++) {
                bitstream_put(bs, s.txbuf, ref t, 0, 1);
                bitstream_put(bs, s.txbuf, ref t, mp.precoder_coeffs[i].re, 16);
                bitstream_put(bs, s.txbuf, ref t, 0, 1);
                bitstream_put(bs, s.txbuf, ref t, mp.precoder_coeffs[i].im, 16);
            }
        }
        /* 51/153           Start bit: 0. */
        bitstream_put(bs, s.txbuf, ref t, 0, 1);
        /* 52:67/154:169    Reserved for ITU-T: These bits are set to 0 by the transmitting modem and are
                            not interpreted by the receiving modem. */
        bitstream_put(bs, s.txbuf, ref t, 0, 16);
        /* 68/170           Start bit: 0. */
        bitstream_put(bs, s.txbuf, ref t, 0, 1);
        bitstream_emit(bs, s.txbuf, t);
        crc = 0xFFFF;
        len = (mp.type == 1) ? 170 : 68;
        for (int i = 17; i < len; i += 17)
            crc = crc_bit_block(s.txbuf, i, i + 15, crc);
        /* 69:84/171:186    CRC. */
        bitstream_put(bs, s.txbuf, ref t, crc, 16);
        /* 85:87 Fill bits: 000.    187 Fill bit: 0. */
        if (mp.type == 1)
            bitstream_put(bs, s.txbuf, ref t, 0, 1);
        else
            bitstream_put(bs, s.txbuf, ref t, 0, 3);
        /* Add some extra postamble, so we have a whole number of bytes to work with. */
        bitstream_put(bs, s.txbuf, ref t, 0, 8);
        bitstream_flush(bs, s.txbuf, ref t);
        return (mp.type == 1) ? 188 : 88;
    }

    private static int mph_sequence_tx(v34_tx_state_t s, mph_t mph) {
        int len;
        int t = 0;
        ushort crc;
        bitstream_state_t bs = new();
        log_mph(s.logging!, true, mph);
        bitstream_init(bs, true);
        /* 0:16     Frame sync: 11111111111111111. */
        /* 17       Start bit: 0. */
        bitstream_put(bs, s.txbuf, ref t, 0x1FFFF, 18);
        /* 18       Type: */
        bitstream_put(bs, s.txbuf, ref t, mph.type, 1);
        /* 19       Reserved for ITU-T: This bit is set to 0 by the transmitting modem and is not
                    interpreted by the receiving modem. */
        bitstream_put(bs, s.txbuf, ref t, 0, 1);
        /* 20:23    Maximum data signalling rate:
                    Data rate = N * 2400 where N is a four-bit integer between 1 and 14. */
        bitstream_put(bs, s.txbuf, ref t, mph.max_data_rate, 4);
        /* 24:26    Reserved for ITU-T: These bits are set to 0 by the transmitting modem and are
                    not interpreted by the receiving modem. */
        bitstream_put(bs, s.txbuf, ref t, 0, 3);
        /* 27       Control channel data signalling rate selected for remote transmitter.
                    0 = 1200 bit/s, 1 = 2400 bit/s (see bit 50 below). */
        bitstream_put(bs, s.txbuf, ref t, mph.control_channel_2400, 1);
        /* 28       Reserved for ITU-T: This bit is set to 0 by the transmitting modem and is not
                    interpreted by the receiving modem. */
        bitstream_put(bs, s.txbuf, ref t, 0, 1);
        /* 29:30    Trellis encoder select bits:
                    0 = 16 state; 1 = 32 state; 2 = 64 state; 3 = Reserved for ITU-T.
                    Receiver requires remote-end transmitter to use selected trellis encoder. */
        bitstream_put(bs, s.txbuf, ref t, mph.trellis_size, 2);
        /* 31       Non-linear encoder parameter select bit for the remote-end transmitter.
                    0: Q = 0, 1: Q = 0.3125. */
        bitstream_put(bs, s.txbuf, ref t, (mph.use_non_linear_encoder ? 1L : 0L), 1);
        /* 32       Constellation shaping select bit for the remote-end transmitter.
                    0: minimum, 1: expanded (see Table 10). */
        bitstream_put(bs, s.txbuf, ref t, (mph.expanded_shaping ? 1L : 0L), 1);
        /* 33       Reserved for ITU-T: This bit is set to 0 by the transmitting modem and is not
                    interpreted by the receiving modem. */
        bitstream_put(bs, s.txbuf, ref t, 0, 1);
        /* 34       Start bit: 0. */
        bitstream_put(bs, s.txbuf, ref t, 0, 1);
        /* 35:49    Data signalling rate capability mask.
                    Bit 35:2400; bit 36:4800; bit 37:7200;...; bit 46:28 800; bit 47:31 200; bit 48:33 600;
                    bit 49: Reserved for ITU-T. (This bit is set to 0 by the transmitting modem and is not
                    interpreted by the receiving modem.) Bits set to 1 indicate data signalling rates supported
                    and enabled in both transmitter and receiver of modem. */
        bitstream_put(bs, s.txbuf, ref t, mph.signalling_rate_mask, 15);
        /* 50       Enables asymmetric control channel data rates:
                    0 = Asymmetric mode not allowed; 1 = Asymmetric mode allowed.
                        Asymmetric mode shall be used only when both modems set bit 50 to 1. If different data
                    rates are selected in symmetric mode, both modems shall transmit at the lower rate. */
        bitstream_put(bs, s.txbuf, ref t, (mph.asymmetric_rates_allowed ? 1L : 0L), 1);
        if (mph.type == 1) {
            /* 51       Start bit: 0. */
            /* 52:67    Precoding coefficient h(1) real. */
            /* 68       Start bit: 0. */
            /* 69:84    Precoding coefficient h(1) imaginary. */
            /* 85       Start bit: 0. */
            /* 86:101   Precoding coefficient h(2) real. */
            /* 102      Start bit: 0. */
            /* 103:118  Precoding coefficient h(2) imaginary. */
            /* 119      Start bit: 0. */
            /* 120:135  Precoding coefficient h(3) real. */
            /* 136      Start bit: 0. */
            /* 137:152  Precoding coefficient h(3) imaginary. */
            for (int i = 0; i < 3; i++) {
                bitstream_put(bs, s.txbuf, ref t, 0, 1);
                bitstream_put(bs, s.txbuf, ref t, mph.precoder_coeffs[i].re, 16);
                bitstream_put(bs, s.txbuf, ref t, 0, 1);
                bitstream_put(bs, s.txbuf, ref t, mph.precoder_coeffs[i].im, 16);
            }
        }
        /* 51/153           Start bit: 0. */
        bitstream_put(bs, s.txbuf, ref t, 0, 1);
        /* 52:67/154:169    Reserved for ITU-T: These bits are set to 0 by the transmitting modem and are not
                            interpreted by the receiving modem. */
        bitstream_put(bs, s.txbuf, ref t, 0, 16);
        /* 68/170           Start bit: 0. */
        bitstream_put(bs, s.txbuf, ref t, 0, 1);
        bitstream_emit(bs, s.txbuf, t);
        crc = 0xFFFF;
        len = (mph.type == 1) ? 170 : 68;
        for (int i = 17; i < len; i += 17)
            crc = crc_bit_block(s.txbuf, i, i + 15, crc);
        /* 69:84/171:186    CRC. */
        bitstream_put(bs, s.txbuf, ref t, crc, 16);
        /* 85:87 Fill bits: 000.    187 Fill bit: 0. */
        if (mph.type == 1)
            bitstream_put(bs, s.txbuf, ref t, 0, 1);
        else
            bitstream_put(bs, s.txbuf, ref t, 0, 3);
        /* Add some extra postamble, so we have a whole number of bytes to work with. */
        bitstream_put(bs, s.txbuf, ref t, 0, 8);
        bitstream_flush(bs, s.txbuf, ref t);
        return (mph.type == 1) ? 188 : 88;
    }

    private static int fake_get_bit(object? user_data) {
        return 1;
    }

    private static void parse_primary_channel_bitstream(v34_tx_state_t s) {
        int u = 0;
        int bb = s.parms.b;
        int kk = s.parms.k;

        bitstream_init(s.bs, true);
        s.s_bit_cnt += s.parms.r;
        if (s.s_bit_cnt >= s.parms.p) {
            s.s_bit_cnt -= s.parms.p;
        } else if (bb > 12) {
            bb--;
            kk--;
        }

        int i = 0;
        s.aux_bit_cnt += s.parms.w;
        if (s.aux_bit_cnt >= s.parms.p) {
            s.aux_bit_cnt -= s.parms.p;
            for (; i < kk; i++) {
                int bit = s.current_get_bit!(s.get_bit_user_data);
                if (bit == SIG_STATUS_END_OF_DATA)
                    s.current_get_bit = fake_get_bit;
                bitstream_put(s.bs, s.txbuf, ref u, scramble(s, bit), 1);
            }

            int aux_bit = s.get_aux_bit is not null ? s.get_aux_bit(s.get_aux_bit_user_data) : 0;
            bitstream_put(s.bs, s.txbuf, ref u, aux_bit, 1);
            i++;
        }

        for (; i < bb; i++) {
            int bit = s.current_get_bit!(s.get_bit_user_data);
            if (bit == SIG_STATUS_END_OF_DATA)
                s.current_get_bit = fake_get_bit;
            bitstream_put(s.bs, s.txbuf, ref u, scramble(s, bit), 1);
        }
        bitstream_flush(s.bs, s.txbuf, ref u);

        bitstream_init(s.bs, true);
        int read = 0;
        if (s.parms.k != 0) {
            s.r0 = bitstream_get(s.bs, s.txbuf, ref read, kk);
            for (i = 0; i < 4; i++) {
                s.ibits[i] = unchecked((ushort)bitstream_get(s.bs, s.txbuf, ref read, 3));
                if (s.parms.q != 0) {
                    s.qbits[2 * i] = unchecked((ushort)bitstream_get(s.bs, s.txbuf, ref read, s.parms.q));
                    s.qbits[2 * i + 1] = unchecked((ushort)bitstream_get(s.bs, s.txbuf, ref read, s.parms.q));
                } else {
                    s.qbits[2 * i] = 0;
                    s.qbits[2 * i + 1] = 0;
                }
            }
        } else {
            s.r0 = 0;
            int n = bb - 8;
            for (i = 0; i < n; i++)
                s.ibits[i] = unchecked((ushort)bitstream_get(s.bs, s.txbuf, ref read, 3));
            for (; i < 4; i++)
                s.ibits[i] = unchecked((ushort)bitstream_get(s.bs, s.txbuf, ref read, 2));
            for (i = 0; i < 8; i++)
                s.qbits[i] = 0;
        }

        LoggingApi.span_log(s.logging!,
                 LoggingApi.SPAN_LOG_FLOW,
                 "Tx - Parsed %p %8X - %X %X %X %X - %2X %2X %2X %2X %2X %2X %2X %2X\n",
                 s,
                 s.r0,
                 s.ibits[0],
                 s.ibits[1],
                 s.ibits[2],
                 s.ibits[3],
                 s.qbits[0],
                 s.qbits[1],
                 s.qbits[2],
                 s.qbits[3],
                 s.qbits[4],
                 s.qbits[5],
                 s.qbits[6],
                 s.qbits[7]);
    }

    private static void shell_map(v34_tx_state_t s) {
        if (s.parms.m == 0) {
            Array.Clear(s.mjk);
            return;
        }

        uint[] g2 = g2s[s.parms.m]!;
        uint[] g4 = g4s[s.parms.m]!;
        uint[] z8 = z8s[s.parms.m]!;

        int a;
        for (a = 1; z8[a] <= s.r0; a++) {
        }
        a--;

        long t2 = (long)s.r0 - z8[a];
        int b = -1;
        long t1;
        do {
            b++;
            t1 = (long)g4[b] * g4[a - b];
            t2 -= t1;
        }
        while (t2 >= 0);
        long r1 = t2 + t1;

        long r2 = r1 % g4[b];
        long r3 = (r1 - r2) / g4[b];

        t2 = r2;
        int c = -1;
        do {
            c++;
            t1 = (long)g2[c] * g2[b - c];
            t2 -= t1;
        }
        while (t2 >= 0);
        long r4 = t2 + t1;

        t2 = r3;
        int d = -1;
        do {
            d++;
            t1 = (long)g2[d] * g2[a - b - d];
            t2 -= t1;
        }
        while (t2 >= 0);
        long r5 = t2 + t1;

        int e = unchecked((int)(r4 % g2[c]));
        int f = unchecked((int)((r4 - e) / g2[c]));
        int g = unchecked((int)(r5 % g2[d]));
        int h = unchecked((int)((r5 - g) / g2[d]));

        if (c < s.parms.m) {
            s.mjk[0] = e;
            s.mjk[1] = c - s.mjk[0];
        } else {
            s.mjk[1] = s.parms.m - 1 - e;
            s.mjk[0] = c - s.mjk[1];
        }

        if (b - c < s.parms.m) {
            s.mjk[2] = f;
            s.mjk[3] = b - c - s.mjk[2];
        } else {
            s.mjk[3] = s.parms.m - 1 - f;
            s.mjk[2] = b - c - s.mjk[3];
        }

        if (d < s.parms.m) {
            s.mjk[4] = g;
            s.mjk[5] = d - s.mjk[4];
        } else {
            s.mjk[5] = s.parms.m - 1 - g;
            s.mjk[4] = d - s.mjk[5];
        }

        if (a - b - d < s.parms.m) {
            s.mjk[6] = h;
            s.mjk[7] = a - b - d - s.mjk[6];
        } else {
            s.mjk[7] = s.parms.m - 1 - h;
            s.mjk[6] = a - b - d - s.mjk[7];
        }
    }

    private static complexi16_t v34_non_linear_encoder(complexi16_t pre) {
        int zeta = (((pre.re * pre.re + pre.im * pre.im + 0x800) >> 12) * 341 + 0x800) >> 12;
        int x = (zeta * zeta + 0x2000) >> 14;
        x = (zeta + ((x * 19661) >> 16) * 15127 + 0x4000) >> 14;
        return new complexi16_t((pre.re * x) >> 14, (pre.im * x) >> 14);
    }

    private static complexi16_t rotate90_clockwise(complexi16_t x, int quads) {
        return (quads & 3) switch {
            0 => new complexi16_t(x.re, x.im),
            1 => new complexi16_t(x.im, -x.re),
            2 => new complexi16_t(-x.re, -x.im),
            _ => new complexi16_t(-x.im, x.re)
        };
    }

    private static short get_binary_subset_label(complexi16_t pos) {
        short xored = unchecked((short)(pos.re ^ pos.im));
        short x = unchecked((short)(xored & 2));
        return unchecked((short)(((xored & 4) ^ (x << 1)) | (pos.re & 2) | (x >> 1)));
    }

    private static complexi16_t quantize_tx(v34_tx_state_t s, complexi16_t x) {
        int re = Math.Abs((int)x.re);
        int im = Math.Abs((int)x.im);
        if (s.parms.b >= 56) {
            re = ((re + 0x0FF) >> 7) & ~0x03;
            im = ((im + 0x0FF) >> 7) & ~0x03;
        } else {
            re = ((re + 0x07F) >> 7) & ~0x01;
            im = ((im + 0x07F) >> 7) & ~0x01;
        }
        if (x.re < 0)
            re = -re;
        if (x.im < 0)
            im = -im;
        return new complexi16_t(re, im);
    }

    private static complexi16_t precoder_tx_filter(v34_tx_state_t s) {
        int sum_re = 0;
        int sum_im = 0;
        for (int i = 0; i < 3; i++) {
            int j = V34_XOFF + s.step_2d - i;
            sum_re += s.x[j].re * s.precoder_coeffs[i].re - s.x[j].im * s.precoder_coeffs[i].im;
            sum_im += s.x[j].re * s.precoder_coeffs[i].im + s.x[j].im * s.precoder_coeffs[i].re;
        }

        int pre = (Math.Abs(sum_re) + 0x01FFF) >> 14;
        if (sum_re < 0)
            pre = -pre;
        int pim = (Math.Abs(sum_im) + 0x01FFF) >> 14;
        if (sum_im < 0)
            pim = -pim;
        return new complexi16_t(pre, pim);
    }

    private static void qam_mod(v34_tx_state_t s) {
        // Original v34tx.c contains only disabled printf/fflush diagnostics here.
        // No modulation logic is present in the supplied native source body.
    }

    public static int v34_get_mapping_frame(v34_tx_state_t s, short[] bits) {
                parse_primary_channel_bitstream(s);
        shell_map(s);

        int u0 = 0;
        int[] subsets = new int[2];
        for (s.step_2d = 0; s.step_2d < 8; s.step_2d++) {
            int mapping_index = (s.mjk[s.step_2d] << s.parms.q) + s.qbits[s.step_2d];
            complexi16_t v = new(v34_superconstellation[mapping_index, 0], v34_superconstellation[mapping_index, 1]);
            int rot;
            if ((s.step_2d & 1) == 0) {
                s.z = (s.z + (s.ibits[s.step_2d >> 1] >> 1)) & 3;
                rot = s.z;
            } else {
                rot = (s.z + ((s.ibits[s.step_2d >> 1] & 1) << 1) + u0) & 3;
            }

            complexi16_t u = rotate90_clockwise(v, rot);
            complexi16_t y = new(u.re + s.c.re, u.im + s.c.im);
            s.x[V34_XOFF + s.step_2d].re = unchecked((short)((y.re << 7) - s.p.re));
            s.x[V34_XOFF + s.step_2d].im = unchecked((short)((y.im << 7) - s.p.im));

            subsets[s.step_2d & 1] = get_binary_subset_label(y);
            qam_mod(s);
            bits[2 * s.step_2d] = s.x[V34_XOFF + s.step_2d].re;
            bits[2 * s.step_2d + 1] = s.x[V34_XOFF + s.step_2d].im;

            s.p = precoder_tx_filter(s);
            if (s.use_non_linear_encoder)
                s.p = v34_non_linear_encoder(s.p);
            complexi16_t c_prev = s.c;
            s.c = quantize_tx(s, s.p);

            if ((s.step_2d & 1) == 0) {
                int sum1 = (c_prev.re + c_prev.im) >> 1;
                int sum2 = (s.c.re + s.c.im) >> 1;
                int c0 = (sum1 ^ sum2) & 1;
                int v0;
                if ((s.data_frame * 8 + s.step_2d) % (4 * s.parms.p) == 0)
                    v0 = (0x5FEE >> s.v0_pattern++) & 1;
                else
                    v0 = 0;
                u0 = (s.y0 ^ c0 ^ v0) & 1;
            } else {
                int y4321 = conv_encode_input[subsets[0], subsets[1]];
                s.y0 = s.state & 1;
                s.state = s.conv_encode_table![s.state, y4321];
            }
        }

        s.x[V34_XOFF - 3] = s.x[V34_XOFF + 5];
        s.x[V34_XOFF - 2] = s.x[V34_XOFF + 6];
        s.x[V34_XOFF - 1] = s.x[V34_XOFF + 7];

        if (++s.data_frame >= s.parms.p) {
            s.data_frame = 0;
            if (++s.super_frame >= s.parms.j) {
                s.super_frame = 0;
                s.v0_pattern = 0;
            }
        }
        return 16;
    }

    private static float exact_baud_rate(int symbol_rate_code) {
        baud_rate_parameters_t p = baud_rate_parameters[symbol_rate_code];
        return (float)SAMPLE_RATE * p.samples_per_symbol_denominator / p.samples_per_symbol_numerator;
    }

    private static float carrier_frequency(int symbol_rate_code, bool low_high) {
        baud_rate_parameters_t p = baud_rate_parameters[symbol_rate_code];
        return exact_baud_rate(symbol_rate_code) * p.low_high[low_high ? 1 : 0, 0] / p.low_high[low_high ? 1 : 0, 1];
    }

    private static int get_data_bit(v34_tx_state_t s) {
        int bit = s.current_get_bit!(s.get_bit_user_data);
        if (bit == SIG_STATUS_END_OF_DATA)
            s.current_get_bit = fake_get_bit;
        return bit;
    }

    private static complexf_t get_transmission_preamble_baud(v34_state_t s) {
        if (++s.tx.txptr >= s.tx.txbits)
            info0_baud_init(s);

        return s.tx.lastbit;
    }

    private static void transmission_preamble_init(v34_state_t s) {
        /* Send some bits as the modulator starts up, to allow things to stabilise before the
           important data goes out. */
        LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Tx - transmission_preamble_init()\n");
        s.tx.txbits = 16;
        s.tx.txptr = 0;
        s.tx.lastbit = new complexf_t(TRAINING_AMP, 0.0f);
        s.tx.current_modulator = V34_MODULATION_CC;
        s.tx.current_getbaud = get_transmission_preamble_baud;
        s.tx.stage = V34_TX_STAGE_INITIAL_PREAMBLE;
    }

    private static complexf_t get_info0_baud(v34_state_t s) {
        int bit;

        bit = get_data_bit(s.tx);
        if (s.tx.txptr >= s.tx.txbits) {
            /* Are we at the initial stage, where A or B comes next, or at the retry
               stage, where we keep repeating INFO0 */
            if (s.tx.stage == V34_TX_STAGE_INFO0)
                initial_ab_not_ab_baud_init(s);
            else
                info0_baud_init(s);

        }

        if (bit != 0)
            s.tx.lastbit.re = -s.tx.lastbit.re;

        return s.tx.lastbit;
    }

    private static void info0_baud_init(v34_state_t s) {
        LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Tx - info0_baud_init()\n");
        s.tx.txbits = info0_sequence_tx(s.tx);
        /* Round up to a whole number of bytes */
        s.tx.txbits = (s.tx.txbits + 7) & ~7;
        s.tx.txptr = 0;
        s.tx.lastbit = new complexf_t(TRAINING_AMP, 0.0f);
        s.tx.current_modulator = V34_MODULATION_CC;
        s.tx.stage = (s.tx.stage >= V34_TX_STAGE_INFO0) ? V34_TX_STAGE_INFO0_RETRY : V34_TX_STAGE_INFO0;
        s.tx.current_getbaud = get_info0_baud;
    }

    private static complexf_t get_initial_fdx_a_not_a_baud(v34_state_t s) {
        /* Answering side */
        switch (s.tx.stage) {
            case V34_TX_STAGE_INITIAL_A:
                /* Send pure tone for at least 50ms (V.34/11.2.1.2.1) */
                if (++s.tx.tone_duration == 30) {
                    /* 50ms minimum A period has passed - accept an incoming INFO0c */
                    s.tx.stage = V34_TX_STAGE_FIRST_A;
                }

                break;
            case V34_TX_STAGE_FIRST_A:
                /* Continue sending pure tone until we see an INFO0c message (V.34/11.2.1.2.3) */
                if (s.rx.received_event == V34_EVENT_INFO0_OK) {
                    /* First reversal seen - send a phase reversal back */
                    s.tx.lastbit.re = -s.tx.lastbit.re;
                    s.tx.tone_duration = 1;
                    s.tx.stage = V34_TX_STAGE_FIRST_NOT_A;
                } else if (s.rx.received_event == V34_EVENT_INFO0_BAD
                           ||
                           s.rx.received_event == V34_EVENT_TONE_SEEN) {
                    /* Go back to sending INFO0a until we get a clean INFO0c */
                    info0_baud_init(s);
                }

                break;
            case V34_TX_STAGE_FIRST_NOT_A:
                /* Send phase reversed pure tone until we see another phase reversal */
                if (s.rx.received_event == V34_EVENT_REVERSAL_1) {
                    /* Second reversal seen - wait 40+=1ms */
                    s.tx.tone_duration = 0;
                    s.tx.stage = V34_TX_STAGE_FIRST_NOT_A_REVERSAL_SEEN;
                }

                break;
            case V34_TX_STAGE_FIRST_NOT_A_REVERSAL_SEEN:
                /* Continue sending phase reversed pure tone for 40+-1ms */
                if (++s.tx.tone_duration == 24) {
                    /* 40ms has passed - send another reversal back */
                    s.tx.lastbit.re = -s.tx.lastbit.re;
                    s.tx.tone_duration = 0;
                    s.tx.stage = V34_TX_STAGE_SECOND_A;
                }

                break;
            case V34_TX_STAGE_SECOND_A:
                /* Send phase reversed pure tone for 10ms */
                if (++s.tx.tone_duration == 6) {
                    /* 10ms has passed - move on to sending L1/L2 */
                    l1_l2_signal_init(s);
                }

                break;
        }

        return s.tx.lastbit;
    }

    private static complexf_t get_initial_fdx_b_not_b_baud(v34_state_t s) {
        /* Calling side */
        switch (s.tx.stage) {
            case V34_TX_STAGE_FIRST_B:
                /* Send pure tone (V.34/11.2.1.1.1) */
                if (s.rx.received_event == V34_EVENT_INFO0_OK) {
                    s.tx.stage = V34_TX_STAGE_FIRST_B_INFO_SEEN;
                } else if (s.rx.received_event == V34_EVENT_INFO0_BAD
                           ||
                           s.rx.received_event == V34_EVENT_TONE_SEEN) {
                    /* Go back to sending INFO0c until we get a clean INFO0a */
                    info0_baud_init(s);
                }

                break;
            case V34_TX_STAGE_FIRST_B_INFO_SEEN:
                /* Continue sending pure tone (V.34/11.2.1.1.1) */
                if (s.rx.received_event == V34_EVENT_REVERSAL_1) {
                    /* First reversal seen - continue sending pure tone for 40+-1ms */
                    s.tx.tone_duration = 1;
                    s.tx.stage = V34_TX_STAGE_FIRST_NOT_B_WAIT;
                }

                break;
            case V34_TX_STAGE_FIRST_NOT_B_WAIT:
                /* Continue sending pure tone for 40+-1ms (V.34/11.2.1.1.3) */
                if (++s.tx.tone_duration == 24) {
                    /* 40ms has passed - send a phase reversal back */
                    s.tx.lastbit.re = -s.tx.lastbit.re;
                    s.tx.tone_duration = 1;
                    s.tx.stage = V34_TX_STAGE_FIRST_NOT_B;
                }

                break;
            case V34_TX_STAGE_FIRST_NOT_B:
                /* Send phase reversed pure tone for 10ms (V.34/11.2.1.1.3) */
                if (++s.tx.tone_duration == 6) {
                    /* 10ms has passed */
                    /* Move on to sending silence */
                    s.tx.tone_duration = 0;
                    s.tx.stage = V34_TX_STAGE_FIRST_B_SILENCE;
                }

                break;
            case V34_TX_STAGE_FIRST_B_SILENCE:
                /* Send silence, as we wait for reversal (V.34/11.2.1.1.4) */
                if (s.rx.received_event == V34_EVENT_REVERSAL_1) {
                    /* Second reversal seen. We now have the round trip timed */
                    s.tx.tone_duration = 1;
                    s.tx.stage = V34_TX_STAGE_FIRST_B_POST_REVERSAL_SILENCE;
                } else if (s.tx.tone_duration == (1200 - 30)) {
                    /* Timeout, as we have not received a round trip time indication after 2s */
                }

                return zero;
            case V34_TX_STAGE_FIRST_B_POST_REVERSAL_SILENCE:
                /* Send silence, as we wait for L2 (V.34/11.2.1.1.4) */
                if (s.rx.received_event == V34_EVENT_L2_SEEN
                    ||
                    ++s.tx.tone_duration >= 400) {
                    /* L2 recognised */
                    s.tx.lastbit.re = -s.tx.lastbit.re;
                    s.tx.tone_duration = 1;
                    s.tx.stage = V34_TX_STAGE_SECOND_B;
                }

                return zero;
            case V34_TX_STAGE_SECOND_B:
                /* Send pure tone (V.34/11.2.1.1.5) */
                if (++s.tx.tone_duration >= 100)
                //if (s.rx.received_event == V34_EVENT_REVERSAL_3)
                {
                    /* Second reversal seen - continue sending pure tone for 40+-1ms */
                    s.tx.tone_duration = 1;
                    s.tx.stage = V34_TX_STAGE_SECOND_B_WAIT;
                }

                break;
            case V34_TX_STAGE_SECOND_B_WAIT:
                /* Continue sending pure tone for 40+-1ms (V.34/11.2.1.1.6) */
                if (++s.tx.tone_duration == 24) {
                    /* 40ms has passed - send a phase reversal back */
                    s.tx.lastbit.re = -s.tx.lastbit.re;
                    s.tx.tone_duration = 1;
                    s.tx.stage = V34_TX_STAGE_SECOND_NOT_B;
                }

                break;
            case V34_TX_STAGE_SECOND_NOT_B:
                /* Send phase reversed pure tone for 10ms (V.34/11.2.1.1.6) */
                if (++s.tx.tone_duration == 6) {
                    /* 10ms has passed - move on to sending L1/L2 */
                    s.tx.tone_duration = 0;
                    l1_l2_signal_init(s);
                }

                break;
        }

        return s.tx.lastbit;
    }

    private static complexf_t get_initial_hdx_a_not_a_baud(v34_state_t s) {
        /* Answering side */
        switch (s.tx.stage) {
            case V34_TX_STAGE_HDX_INITIAL_A:
                /* Send pure tone (V.34/12.2.1.2.1) */
                if (++s.tx.tone_duration == 30) {
                    /* 50ms minimum A period has passed - accept an incoming INFO0c */
                    s.tx.stage = V34_TX_STAGE_HDX_FIRST_A;
                }

                break;
            case V34_TX_STAGE_HDX_FIRST_A:
                /* Continue sending pure tone until we see an INFO0c message (V.34/12.2.1.2.3) */
                if (s.rx.received_event == V34_EVENT_INFO0_OK) {
                    /* First reversal seen - send a phase reversal back */
                    s.tx.lastbit.re = -s.tx.lastbit.re;
                    s.tx.tone_duration = 1;
                    s.tx.stage = V34_TX_STAGE_HDX_FIRST_NOT_A;
                } else if (s.rx.received_event == V34_EVENT_INFO0_BAD
                           ||
                           s.rx.received_event == V34_EVENT_TONE_SEEN) {
                    /* Go back to sending INFO0a until we get a clean INFO0c */
                    info0_baud_init(s);
                }

                break;
            case V34_TX_STAGE_HDX_FIRST_NOT_A:
                /* Send phase reversed pure tone for 10ms (V.34/12.2.1.2.3) */
                if (++s.tx.tone_duration == 6) {
                    /* 10ms has passed - send silence */
                    s.tx.tone_duration = 0;
                    s.tx.stage = V34_TX_STAGE_HDX_FIRST_A_SILENCE;
                }

                break;
            case V34_TX_STAGE_HDX_FIRST_A_SILENCE:
                /* Send silence, as we wait for L2 (V.34/12.2.1.2.3) */
                if (s.rx.received_event == V34_EVENT_L2_SEEN
                    ||
                    ++s.tx.tone_duration >= 400) {
                    /* L2 recognised */
                    s.tx.lastbit.re = -s.tx.lastbit.re;
                    s.tx.tone_duration = 1;
                    s.tx.stage = V34_TX_STAGE_HDX_SECOND_A;
                }

                return zero;
            case V34_TX_STAGE_HDX_SECOND_A:
                /* Send pure tone (V.34/12.2.1.2.5) */
                if (++s.tx.tone_duration >= 100)
                //if (s.rx.received_event == V34_EVENT_REVERSAL_2)
                {
                    /* Second reversal seen - continue sending pure tone for 25ms */
                    s.tx.lastbit.re = -s.tx.lastbit.re;
                    s.tx.tone_duration = 1;
                    s.tx.stage = V34_TX_STAGE_HDX_SECOND_A_WAIT;
                }

                break;
            case V34_TX_STAGE_HDX_SECOND_A_WAIT:
                /* Continue sending pure tone for 25ms (V.34/12.2.1.2.6) */
                if (++s.tx.tone_duration == 15) {
                    /* 25ms has passed - send INFOh */
                    s.tx.tone_duration = 0;
                    infoh_baud_init(s);
                }

                break;
        }

        return s.tx.lastbit;
    }

    private static complexf_t get_initial_hdx_b_not_b_baud(v34_state_t s) {
        /* Calling side */
        switch (s.tx.stage) {
            case V34_TX_STAGE_HDX_FIRST_B:
                /* Send pure tone (V.34/12.2.1.1.1) */
                if (s.rx.received_event == V34_EVENT_INFO0_OK) {
                    s.tx.stage = V34_TX_STAGE_HDX_FIRST_B_INFO_SEEN;
                } else if (s.rx.received_event == V34_EVENT_INFO0_BAD
                           ||
                           s.rx.received_event == V34_EVENT_TONE_SEEN) {
                    /* Go back to sending INFO0c until we get a clean INFO0a */
                    info0_baud_init(s);
                }

                break;
            case V34_TX_STAGE_HDX_FIRST_B_INFO_SEEN:
                /* Continue sending pure tone (V.34/12.2.1.1.1) */
                if (s.rx.received_event == V34_EVENT_REVERSAL_1) {
                    /* First reversal seen - continue sending pure tone for 40+-1ms */
                    s.tx.tone_duration = 1;
                    s.tx.stage = V34_TX_STAGE_HDX_FIRST_NOT_B_WAIT;
                }

                break;
            case V34_TX_STAGE_HDX_FIRST_NOT_B_WAIT:
                /* Continue sending pure tone for 40+-10ms (V.34/12.2.1.1.3) */
                if (++s.tx.tone_duration == 24) {
                    /* 40ms has passed - send a phase reversal back */
                    s.tx.lastbit.re = -s.tx.lastbit.re;
                    s.tx.tone_duration = 1;
                    s.tx.stage = V34_TX_STAGE_HDX_FIRST_NOT_B;
                }

                break;
            case V34_TX_STAGE_HDX_FIRST_NOT_B:
                /* Send phase reversed pure tone for 10ms (V.34/12.2.1.1.3) */
                if (++s.tx.tone_duration == 6) {
                    /* 10ms has passed */
                    /* Move on to sending L1/L2 */
                    s.tx.tone_duration = 0;
                    l1_l2_signal_init(s);
                }

                break;
        }

        return s.tx.lastbit;
    }

    private static void initial_ab_not_ab_baud_init(v34_state_t s) {
        LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Tx - initial_ab_not_ab_baud_init()\n");
        s.tx.tone_duration = 0;
        s.tx.current_modulator = V34_MODULATION_CC;
        s.tx.lastbit = new complexf_t(TRAINING_AMP, 0.0f);
        if (s.tx.duplex) {
            if (s.tx.calling_party) {
                s.tx.current_getbaud = get_initial_fdx_b_not_b_baud;
                s.tx.stage = V34_TX_STAGE_FIRST_B;
            } else {
                s.tx.current_getbaud = get_initial_fdx_a_not_a_baud;
                s.tx.stage = V34_TX_STAGE_INITIAL_A;
            }

        } else {
            if (s.tx.calling_party) {
                s.tx.current_getbaud = get_initial_hdx_b_not_b_baud;
                s.tx.stage = V34_TX_STAGE_HDX_FIRST_B;
            } else {
                s.tx.current_getbaud = get_initial_hdx_a_not_a_baud;
                s.tx.stage = V34_TX_STAGE_HDX_INITIAL_A;
            }

        }

        s.tx.persistence2 = 0;
    }

    private static int tx_l1_l2(v34_state_t s, Span<short> amp, int offset, int max_len) {
        int sample;

        /* This signal repeats every 160 samples, so we have the appropriate
           pattern stored, and we just scale and repeat it. We start 6dB above nominal
           power (L1) and then drop the amplitude to nominal power after the first 160ms
           (8 cycles) (L2). L2 should not last longer than 550ms + a round trip time. */
        /* This can occur between:
                !B and INFO1c for a FDX caller
                !B and B for a HDX caller
                A and A for a FDX answerer
                !A and A for a HDX answerer
         */
        for (sample = 0; sample < max_len; sample++) {
            amp[offset + sample] = (short)global::TKFaxEngine.FastConvert.lfastrintf(line_probe_samples[s.tx.line_probe_step] * s.tx.line_probe_scaling);
            if (++s.tx.line_probe_step >= LINE_PROBE_SAMPLES) {
                s.tx.line_probe_step = 0;
                if (++s.tx.line_probe_cycles == 8) {
                    /* Move to the L2 stage, by dropping 6dB */
                    s.tx.line_probe_scaling *= 0.5f;
                    s.tx.state = V34_TX_STAGE_L2;
                } else if (s.tx.line_probe_cycles == (8 + 20)) {
                    /* End of line probe sequence */
                    if (s.tx.duplex) {
                        if (s.tx.calling_party)
                            info1_baud_init(s);
                        else
                            second_a_baud_init(s);

                    } else {
                        if (s.tx.calling_party)
                            second_b_baud_init(s);
                        else
                            second_a_baud_init(s);

                    }

                    break;
                }

            }

        }

        return sample;
    }

    private static void l1_l2_signal_init(v34_state_t s) {
        LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Tx - l2_l2_signal_init()\n");
        s.tx.line_probe_step = 0;
        s.tx.line_probe_cycles = 0;
        s.tx.line_probe_scaling = 0.0008f * s.tx.gain;
        s.tx.current_modulator = V34_MODULATION_L1_L2;
        s.tx.state = V34_TX_STAGE_L1;
    }

    private static complexf_t get_second_a_baud(v34_state_t s) {
        switch (s.tx.stage) {
            case V34_TX_STAGE_POST_L2_A:
                /* Send pure tone for 50ms (V.34/11.2.1.2.6) */
                if (++s.tx.tone_duration == 30) {
                    /* 50ms has passed - reverse */
                    s.tx.lastbit.re = -s.tx.lastbit.re;
                    s.tx.tone_duration = 0;
                    s.tx.stage = V34_TX_STAGE_POST_L2_NOT_A;
                }

                break;
            case V34_TX_STAGE_POST_L2_NOT_A:
                /* Send phase reversed pure tone for 10ms (V.34/11.2.1.2.6) */
                if (++s.tx.tone_duration == 6) {
                    /* 10ms has passed - change to silence */
                    s.tx.tone_duration = 0;
                    s.tx.stage = V34_TX_STAGE_A_SILENCE;
                }

                break;
            case V34_TX_STAGE_A_SILENCE:
                /* Send silence, as we wait for L2 (V.34/11.2.1.2.6) */
                if (s.rx.received_event == V34_EVENT_L2_SEEN
                    ||
                    ++s.tx.tone_duration >= 390) {
                    /* 650ms has passed - wait for INFO1c message */
                    s.tx.lastbit.re = -s.tx.lastbit.re;
                    s.tx.tone_duration = 0;
                    s.tx.stage = V34_TX_STAGE_PRE_INFO1_A;
                }

                return zero;
            case V34_TX_STAGE_PRE_INFO1_A:
                //if (s.rx.received_event == V34_EVENT_INFO1_OK)
                if (++s.tx.tone_duration == 180) {
                    /* INFO1c received - send INFO1a */
                    s.tx.tone_duration = 0;
                    info1_baud_init(s);
                } else if (s.rx.received_event == V34_EVENT_INFO1_BAD
                           ||
                           s.rx.received_event == V34_EVENT_TONE_SEEN) {
                } else if (s.tx.tone_duration == 1200) {
                    /* Timeout, as we have not received INFO1c after 2s */
                }

                break;
        }

        return s.tx.lastbit;
    }

    private static void second_a_baud_init(v34_state_t s) {
        LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Tx - second_a_baud_init()\n");
        s.tx.tone_duration = 0;
        s.tx.current_modulator = V34_MODULATION_CC;
        s.tx.lastbit = new complexf_t(TRAINING_AMP, 0.0f);
        s.tx.stage = V34_TX_STAGE_POST_L2_A;
        s.tx.current_getbaud = get_second_a_baud;
    }

    private static complexf_t get_second_b_baud(v34_state_t s) {
        switch (s.tx.stage) {
            case V34_TX_STAGE_HDX_POST_L2_B:
                /* Send pure tone until we receive INFOh (V.34/12.2.1.1.4) */
                if (s.rx.received_event == V34_EVENT_INFOH_OK) {
                    s.tx.tone_duration = 0;
                    s.tx.stage = V34_TX_STAGE_HDX_POST_L2_SILENCE;
                } else if (s.rx.received_event == V34_EVENT_INFO0_BAD
                           ||
                           s.rx.received_event == V34_EVENT_TONE_SEEN) {
                } else if (++s.tx.tone_duration == 1200) {
                    /* Timeout, as we have not received INFOh after 2s */
                }

                break;
            case V34_TX_STAGE_HDX_POST_L2_SILENCE:
                /* Send silence for 75ms (V.34/12.3.1.1) */
                if (++s.tx.tone_duration == 45) {
                    s.tx.tone_duration = 0;
                }

                return zero;
        }

        return s.tx.lastbit;
    }

    private static void second_b_baud_init(v34_state_t s) {
        /* This is for half-duplex */
        LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Tx - second_b_baud_init()\n");
        s.tx.tone_duration = 0;
        s.tx.current_modulator = V34_MODULATION_CC;
        s.tx.lastbit = new complexf_t(TRAINING_AMP, 0.0f);
        s.tx.stage = V34_TX_STAGE_HDX_POST_L2_B;
        s.tx.current_getbaud = get_second_b_baud;
    }

    private static complexf_t get_infoh_baud(v34_state_t s) {
        int bit;

        bit = get_data_bit(s.tx);
        if (s.tx.txptr >= s.tx.txbits) {
            if (s.tx.calling_party)
                tx_silence_init(s, 30000);
            else
                s_not_s_baud_init(s);

        }

        if (bit != 0)
            s.tx.lastbit.re = -s.tx.lastbit.re;

        return s.tx.lastbit;
    }

    private static void infoh_baud_init(v34_state_t s) {
        LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Tx - infoh_baud_init()\n");
        prepare_infoh(s);
        s.tx.txbits = infoh_sequence_tx(s.tx, s.tx.infoh);
        s.tx.txbits += 8;
        s.tx.txptr = 0;
        s.tx.lastbit = new complexf_t(TRAINING_AMP, 0.0f);
        /* Round up to a whole number of bytes */
        s.tx.txbits = (s.tx.txbits + 7) & ~7;
        s.tx.current_modulator = V34_MODULATION_CC;
        s.tx.current_getbaud = get_infoh_baud;
    }

    private static complexf_t get_info1_baud(v34_state_t s) {
        int bit;

        bit = get_data_bit(s.tx);
        if (s.tx.txptr >= s.tx.txbits) {
            if (s.tx.calling_party) {
                Console.Error.Write("info 1 Tx silence\n");
                tx_silence_init(s, 30000);
            } else {
                Console.Error.Write("info 1 Tx S !S\n");
                s_not_s_baud_init(s);
            }

        }

        if (bit != 0)
            s.tx.lastbit.re = -s.tx.lastbit.re;

        return s.tx.lastbit;
    }

    private static void info1_baud_init(v34_state_t s) {
        LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Tx - info1_baud_init()\n");
        if (s.tx.calling_party) {
            prepare_info1c(s);
            s.tx.txbits = info1c_sequence_tx(s.tx, s.tx.info1c);
            s.tx.txbits += 8;
        } else {
            prepare_info1a(s);
            s.tx.txbits = info1a_sequence_tx(s.tx, s.tx.info1a);
        }

        /* Round up to a whole number of bytes */
        s.tx.txbits = (s.tx.txbits + 7) & ~7;
        s.tx.txptr = 0;
        s.tx.lastbit = new complexf_t(TRAINING_AMP, 0.0f);
        s.tx.current_modulator = V34_MODULATION_CC;
        s.tx.stage = V34_TX_STAGE_INFO1;
        s.tx.current_getbaud = get_info1_baud;
    }

    private static complexf_t get_s_not_s_baud(v34_state_t s) {
        float x;

        switch (s.tx.stage) {
            case V34_TX_STAGE_FIRST_S:
                if (++s.tx.tone_duration < 180)
                    return zero;

                if (s.tx.tone_duration == (128 + 180)) {
                    s.tx.lastbit.re = -s.tx.lastbit.re;
                    s.tx.stage = V34_TX_STAGE_FIRST_NOT_S;
                    s.tx.tone_duration = 0;
                }

                break;
            case V34_TX_STAGE_FIRST_NOT_S:
                if (++s.tx.tone_duration == 16) {
                    s.tx.lastbit.re = -s.tx.lastbit.re;
                    if (s.tx.duplex && s.tx.info1c.md != 0)
                        s.tx.stage = V34_TX_STAGE_SECOND_S;
                    else
                        pp_baud_init(s);

                    s.tx.tone_duration = 0;
                }

                break;
            case V34_TX_STAGE_MD:
                /* This is where MD would go */
                break;
            case V34_TX_STAGE_SECOND_S:
                if (++s.tx.tone_duration == 128) {
                    s.tx.lastbit.re = -s.tx.lastbit.re;
                    s.tx.stage = V34_TX_STAGE_SECOND_NOT_S;
                    s.tx.tone_duration = 0;
                }

                break;
            case V34_TX_STAGE_SECOND_NOT_S:
                if (++s.tx.tone_duration == 16)
                    pp_baud_init(s);

                break;
        }

        x = s.tx.lastbit.re;
        s.tx.lastbit.re = s.tx.lastbit.im;
        s.tx.lastbit.im = x;
        return s.tx.lastbit;
    }

    private static void s_not_s_baud_init(v34_state_t s) {
        LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Tx - s_not_s_baud_init()\n");
        s.tx.lastbit = new complexf_t(TRAINING_AMP, 0.0f);
        s.tx.tone_duration = 0;
        s.tx.current_modulator = V34_MODULATION_V34;
        s.tx.stage = V34_TX_STAGE_FIRST_S;
        s.tx.current_getbaud = get_s_not_s_baud;
    }

    private static complexf_t get_pp_baud(v34_state_t s) {
        complexf_t x;
        int i;

        /* The 48 symbol PP signal, which is repeated 6 times, to make a 288 symbol sequence */
        /* See V.34/10.1.3.6 */
        i = s.tx.tone_duration % 48;
        if (++s.tx.tone_duration == PP_SYMBOLS * PP_REPEATS)
            trn_baud_init(s);

        x = pp_symbols[i];
        x.re *= (TRAINING_AMP);
        x.im *= (TRAINING_AMP);
        return x;
    }

    private static void pp_baud_init(v34_state_t s) {
        LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Tx - pp_baud_init()\n");
        s.tx.tone_duration = 0;
        s.tx.current_getbaud = get_pp_baud;
    }

    private static complexf_t get_trn_baud(v34_state_t s) {
        ushort[] j_pattern = new ushort[] {
            0x8990, /* 4 point constellation */
            0x89B0  /* 16 point constellation */
        };
        int bit;

        /* See V.34/10.1.3.8 */
        bit = 0;
        switch (s.tx.stage) {
            case V34_TX_STAGE_TRN:
                /* Send the TRN signal */
                bit = scramble(s.tx, 1);
                bit = (scramble(s.tx, 1) << 1) | bit;
                /* In half-duplex modem the length of the training comes from the INFOh message, in 35ms increments */
                if ((!s.tx.duplex && ++s.tx.tone_duration >= s.rx.infoh.length_of_trn * 35 * s.rx.infoh.baud_rate / 1000)
                    ||
                    (s.tx.duplex && ++s.tx.tone_duration >= 512)) {
                    s.tx.stage = V34_TX_STAGE_J;
                    s.tx.persistence2 = j_pattern[0];
                    s.tx.tone_duration = 0;
                }

                break;
            case V34_TX_STAGE_J:
                /* Send the terminal J signal */
                bit = scramble(s.tx, (s.tx.persistence2 & 1));
                s.tx.persistence2 >>= 1;
                bit = (scramble(s.tx, (s.tx.persistence2 & 1)) << 1) | bit;
                s.tx.persistence2 >>= 1;
                if (++s.tx.tone_duration >= 16) {
                    if (s.tx.duplex) {
                        if (s.rx.received_event == V34_EVENT_S) {
                            if (s.tx.calling_party) {
                                /* Change to J' */
                                s.tx.stage = V34_TX_STAGE_J_DASHED;
                                s.tx.persistence2 = j_pattern[0];
                                s.tx.tone_duration = 0;
                            } else {
                                /* Send silence */
                            }

                        } else {
                            /* Continue with repeats of J */
                            s.tx.persistence2 = j_pattern[0];
                            s.tx.tone_duration = 0;
                        }

                    } else {
                        mp_or_mph_baud_init(s);
                    }

                }

                break;
            case V34_TX_STAGE_J_DASHED:
                /* Send J' */
                bit = scramble(s.tx, (s.tx.persistence2 & 1));
                s.tx.persistence2 >>= 1;
                bit = (scramble(s.tx, (s.tx.persistence2 & 1)) << 1) | bit;
                s.tx.persistence2 >>= 1;
                if (++s.tx.tone_duration >= 16) {
                }

                break;
        }

        return training_constellation_4[bit];
    }

    private static void trn_baud_init(v34_state_t s) {
        LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Tx - trn_baud_init()\n");
        s.tx.tone_duration = 0;
        s.tx.stage = V34_TX_STAGE_TRN;
        s.tx.current_getbaud = get_trn_baud;
    }

    private static complexf_t get_mp_or_mph_baud(v34_state_t s) {
        int bit;
        int c_condition;

        bit = scramble(s.tx, get_data_bit(s.tx));
        bit = (scramble(s.tx, get_data_bit(s.tx)) << 1) | bit;
        if (s.tx.txptr >= s.tx.txbits) {
            c_condition = 1;
            if (c_condition != 0) {
                if (s.tx.duplex) {
                    /* See if we need to set the acknowledge bit, so MP becomes MP' */
                    c_condition = 1;
                    if (c_condition != 0) {
                        s.tx.mp.mp_acknowledged = true;
                        /* We need to rebuild the message we send */
                        s.tx.txbits = mp_sequence_tx(s.tx, s.tx.mp);
                    }

                }

                /* Restart the message */
                s.tx.txptr = 0;
            } else {
                e_baud_init(s);
            }

        }

        s.tx.diff = (s.tx.diff + bit) & 3;
        return training_constellation_4[s.tx.diff];
    }

    private static void mp_or_mph_baud_init(v34_state_t s) {
        LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Tx - mp_baud_init()\n");
        s.tx.current_modulator = V34_MODULATION_V34;
        if (s.tx.duplex) {
            s.tx.txbits = mp_sequence_tx(s.tx, s.tx.mp);
            s.tx.stage = V34_TX_STAGE_MP;
        } else {
            s.tx.txbits = mph_sequence_tx(s.tx, s.tx.mph);
            s.tx.stage = V34_TX_STAGE_HDX_MPH;
        }

        s.tx.txptr = 0;
        s.tx.current_getbaud = get_mp_or_mph_baud;
    }

    private static complexf_t get_e_baud(v34_state_t s) {
        ushort[] e_pattern = new ushort[] {
            0x8990, /* 4 point constellation */
            0x89B0  /* 16 point constellation */
        };
        int bit;

        bit = (e_pattern[0] >> s.tx.tone_duration) & 1;
        if (++s.tx.tone_duration == 16) {
            //if (s.tx.duplex)
            /* CC comes next */
            //else
            /* B1 comes next */
            //
        }

        return training_constellation_4[bit];
    }

    private static void e_baud_init(v34_state_t s) {
        LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Tx - e_baud_init()\n");
        s.tx.tone_duration = 0;
        s.tx.stage = V34_TX_STAGE_HDX_E;
        s.tx.current_getbaud = get_e_baud;
    }

    private static complexf_t get_pph_baud(v34_state_t s) {
        int i;

        /* This is the beginning of half-duplex control channel restart */
        /* The 8 symbol PPh signal, which is repeated 4 times, to make a 32 symbol sequence */
        /* See V.34/10.2.4.5 */
        i = s.tx.tone_duration & 0x7;
        if (++s.tx.tone_duration == PPH_SYMBOLS * PPH_REPEATS)
            second_alt_baud_init(s);

        return pph_symbols[i];
    }

    private static void pph_baud_init(v34_state_t s) {
        LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Tx - pph_baud_init()\n");
        s.tx.tone_duration = 0;
        s.tx.current_modulator = V34_MODULATION_CC;
        s.tx.stage = V34_TX_STAGE_HDX_PPH;
        s.tx.current_getbaud = get_pph_baud;
    }

    private static complexf_t get_second_alt_baud(v34_state_t s) {
        int bit;
        int c_condition;

        /* Signal ALT is transmitted using the control channel modulation with the differential
           encoder enabled and consists of scrambled alternations of binary 0 and 1 at 1200 bit/s.
           The initial state of the scrambler shall be all zeroes. */
        /* See V.34/10.2.4.2 */
        bit = scramble(s.tx, 0);
        bit = (scramble(s.tx, 1) << 1) | bit;
        s.tx.diff = (s.tx.diff + bit) & 3;
        if (++s.tx.tone_duration >= 16) {
            /* We have reached the absolute minimum allowed for the duration of ALT */
            if (s.tx.tone_duration >= 120) {
                /* TODO: Should allow for early termination. */
                c_condition = 1;
                if (c_condition != 0) {
                    /* Control channel training */
                    mp_or_mph_baud_init(s);
                } else {
                    /* Control channel resynchronisation */
                    e_baud_init(s);
                }

            }

        }

        return training_constellation_4[s.tx.diff];
    }

    private static void second_alt_baud_init(v34_state_t s) {
        LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Tx - second_alt_baud_init()\n");
        s.tx.tone_duration = 0;
        s.tx.current_modulator = V34_MODULATION_V34;
        s.tx.scramble_reg = 0;
        s.tx.diff = 0;
        s.tx.stage = V34_TX_STAGE_HDX_SECOND_ALT;
        s.tx.current_getbaud = get_second_alt_baud;
    }

    private static complexf_t get_first_alt_baud(v34_state_t s) {
        int bit;

        /* Signal ALT is transmitted using the control channel modulation with the differential
           encoder enabled and consists of scrambled alternations of binary 0 and 1 at 1200 bit/s.
           The initial state of the scrambler shall be all zeroes. */
        /* See V.34/10.2.4.2 */
        bit = scramble(s.tx, 0);
        bit = (scramble(s.tx, 1) << 1) | bit;
        s.tx.diff = (s.tx.diff + bit) & 3;
        if (++s.tx.tone_duration >= 16) {
            /* We have reached the absolute minimum allowed for the duration of ALT */
            if (s.tx.tone_duration >= 120) {
                /* TODO: Should allow for early termination. */
                /* Control channel training */
                pph_baud_init(s);
            }

        }

        return training_constellation_4[s.tx.diff];
    }

    private static void first_alt_baud_init(v34_state_t s) {
        LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Tx - first_alt_baud_init()\n");
        s.tx.tone_duration = 0;
        s.tx.current_modulator = V34_MODULATION_V34;
        s.tx.scramble_reg = 0;
        s.tx.diff = 0;
        s.tx.stage = V34_TX_STAGE_HDX_FIRST_ALT;
        s.tx.current_getbaud = get_first_alt_baud;
    }

    private static complexf_t get_sh_baud(v34_state_t s) {

        byte[] sh_plus_not_sh = new byte[] {
            2, 1, 2, 1, 2, 1, 2, 1, 2, 1, 2, 1, 2, 1, 2, 1, 2, 1, 2, 1, 2, 1, 2, 1,     /* Sh */
            0, 3, 0, 3, 0, 3, 0, 3                                                      /* !Sh */
        };
        int i;

        /* See V.34/10.2.3.3 */
        i = s.tx.tone_duration;
        if (++s.tx.tone_duration == SH_PLUS_NO_SH_SYMBOLS) {
            /* The Sh and !Sh have finished */
            first_alt_baud_init(s);
        }

        return training_constellation_4[sh_plus_not_sh[i]];
    }

    private static void sh_baud_init(v34_state_t s) {
        /* This is the beginning of half-duplex control channel startup */
        LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Tx - sh_baud_init()\n");
        s.tx.lastbit = new complexf_t(TRAINING_AMP, 0.0f);
        s.tx.tone_duration = 0;
        s.tx.current_modulator = V34_MODULATION_V34;
        s.tx.stage = V34_TX_STAGE_HDX_SH;
        s.tx.current_getbaud = get_sh_baud;
    }

    private static uint dist_sq(complexi_t x, complexi_t y) {
        return unchecked((uint)((x.re - y.re) * (x.re - y.re) + (x.im - y.im) * (x.im - y.im)));
    }

    private static float dist_sq(complexf_t x, complexf_t y) {
        return (x.re - y.re) * (x.re - y.re) + (x.im - y.im) * (x.im - y.im);
    }

    private static complexf_t training_get(v34_tx_state_t s) {
        return zero;
    }

    private static complexf_t connect_sequence_get(v34_tx_state_t s) {
        return zero;
    }

    private static int tx_v34_modulation(v34_state_t s, Span<short> amp, int offset, int max_len) {
        complexf_t v;
        complexf_t x;
        complexf_t z;
        float[,] shaper;
        int num;
        int den;
        int i;
        int sample;

        /* The V.34 modulator. */
        Console.Error.Write(CPrintfFormatter.Format("ZZZ baud rate %d\n", new object?[] { s.tx.baud_rate }));
        num = s.tx.parms.samples_per_symbol_numerator;
        den = s.tx.parms.samples_per_symbol_denominator;
        switch (s.tx.baud_rate) {
            case V34_BAUD_RATE_2400:
                shaper = tx_pulseshaper_2400;
                break;
            case V34_BAUD_RATE_2743:
                shaper = tx_pulseshaper_2743;
                break;
            case V34_BAUD_RATE_2800:
                shaper = tx_pulseshaper_2800;
                break;
            case V34_BAUD_RATE_3000:
                shaper = tx_pulseshaper_3000;
                break;
            case V34_BAUD_RATE_3200:
                shaper = tx_pulseshaper_3200;
                break;
            default:
                shaper = tx_pulseshaper_3429;
                break;
        }
        for (sample = 0; sample < max_len; sample++) {
            if ((s.tx.baud_phase += den) >= num) {
                s.tx.baud_phase -= num;
                v = s.tx.current_getbaud!(s);
                s.tx.rrc_filter_re[s.tx.rrc_filter_step] = v.re;
                s.tx.rrc_filter_im[s.tx.rrc_filter_step] = v.im;
                Console.Error.Write(CPrintfFormatter.Format("V.34 baud %10.5f %10.5f - %10.5f\n", new object?[] { s.tx.rrc_filter_re[s.tx.rrc_filter_step], s.tx.rrc_filter_im[s.tx.rrc_filter_step], s.tx.gain }));
                if (++s.tx.rrc_filter_step >= V34_TX_FILTER_STEPS)
                    s.tx.rrc_filter_step = 0;

            }

            /* Root raised cosine pulse shaping at baseband */
            x = zero;
            for (i = 0; i < V34_TX_FILTER_STEPS; i++) {
                x.re += shaper[num - 1 - s.tx.baud_phase, i] * s.tx.rrc_filter_re[(i + s.tx.rrc_filter_step) % V34_TX_FILTER_STEPS];
                x.im += shaper[num - 1 - s.tx.baud_phase, i] * s.tx.rrc_filter_im[(i + s.tx.rrc_filter_step) % V34_TX_FILTER_STEPS];
            }

            /* Now create and modulate the carrier */
            z = Dds.dds_complexf(ref s.tx.carrier_phase, s.tx.v34_carrier_phase_rate);
            /* Don't bother saturating. We should never clip. */
            amp[offset + sample] = (short)global::TKFaxEngine.FastConvert.lfastrintf((x.re * z.re - x.im * z.im) * s.tx.gain);
            Console.Error.Write(CPrintfFormatter.Format("V.34 sample %d\n", new object?[] { amp[offset + sample] }));
        }

        return sample;
    }

    private static int tx_cc_modulation(v34_state_t s, Span<short> amp, int offset, int max_len) {
        complexf_t v;
        complexf_t x;
        complexf_t z;
        float famp;
        int sample;

        /* The V.22bis like split band modulator for configuration data and the
           half-duplex control channel. */
        for (sample = 0; sample < max_len; sample++) {
            if ((s.tx.baud_phase += 3) >= 40) {
                s.tx.baud_phase -= 40;
                v = s.tx.current_getbaud!(s);
                s.tx.rrc_filter_re[s.tx.rrc_filter_step] = v.re;
                s.tx.rrc_filter_im[s.tx.rrc_filter_step] = v.im;
                Console.Error.Write(CPrintfFormatter.Format("CC baud %10.5f %10.5f - %10.5f\n", new object?[] { s.tx.rrc_filter_re[s.tx.rrc_filter_step], s.tx.rrc_filter_im[s.tx.rrc_filter_step], s.tx.gain }));
                if (++s.tx.rrc_filter_step >= V34_INFO_TX_FILTER_STEPS)
                    s.tx.rrc_filter_step = 0;

            }

            /* Root raised cosine pulse shaping at baseband */
            x.re = vec_circular_dot_prodf(s.tx.rrc_filter_re, s.tx.rrc_filter_step, tx_pulseshaper[TX_PULSESHAPER_COEFF_SETS - 1 - s.tx.baud_phase], V34_INFO_TX_FILTER_STEPS);
            x.im = vec_circular_dot_prodf(s.tx.rrc_filter_im, s.tx.rrc_filter_step, tx_pulseshaper[TX_PULSESHAPER_COEFF_SETS - 1 - s.tx.baud_phase], V34_INFO_TX_FILTER_STEPS);
            /* Now create and modulate the carrier */
            z = Dds.dds_complexf(ref s.tx.carrier_phase, s.tx.cc_carrier_phase_rate);
            famp = x.re * z.re - x.im * z.im;
            if (s.tx.guard_phase_rate != 0 && (s.tx.rrc_filter_re[s.tx.rrc_filter_step] != 0.0f || s.tx.rrc_filter_im[s.tx.rrc_filter_step] != 0.0f)) {
                /* Add the guard tone */
                famp += Dds.dds_modf(ref s.tx.guard_phase, s.tx.guard_phase_rate, s.tx.guard_level, 0);
            }

            /* Don't bother saturating. We should never clip. */
            amp[offset + sample] = (short)global::TKFaxEngine.FastConvert.lfastrintf(famp * s.tx.gain);
            Console.Error.Write(CPrintfFormatter.Format("CC sample %d\n", new object?[] { amp[offset + sample] }));
        }
        return sample;
    }

    private static int tx_silence(v34_state_t s, Span<short> amp, int offset, int max_len) {
        if (s.tx.tone_duration <= max_len) {
            max_len = s.tx.tone_duration;
            s.tx.tone_duration = 0;
            if (s.tx.training_stage == 0x100) {
                s.tx.training_stage = 0x101;
                transmission_preamble_init(s);
            }

        } else {
            s.tx.tone_duration -= max_len;
        }

        amp.Slice(offset, max_len).Clear();
        return max_len;
    }

    private static void tx_silence_init(v34_state_t s, int duration) {
        s.tx.tone_duration = Telephony.milliseconds_to_samples(duration);
        s.tx.current_modulator = V34_MODULATION_SILENCE;
    }

    public static int v34_tx(v34_state_t s, Span<short> amp, int len) {
        int generated_len;
        int lenx;

        generated_len = 0;
        lenx = -1;
        do {
            switch (s.tx.current_modulator) {
                case V34_MODULATION_V34:
                    lenx = tx_v34_modulation(s, amp, generated_len, len - generated_len);
                    break;
                case V34_MODULATION_CC:
                    lenx = tx_cc_modulation(s, amp, generated_len, len - generated_len);
                    break;
                case V34_MODULATION_L1_L2:
                    lenx = tx_l1_l2(s, amp, generated_len, len - generated_len);
                    break;
                case V34_MODULATION_SILENCE:
                    lenx = tx_silence(s, amp, generated_len, len - generated_len);
                    break;
            }

            generated_len += lenx;
            /* Add step by step, so each segment is seen up to date */
            s.tx.sample_time += lenx;
        }
        while (lenx > 0 && generated_len < len);
        /* If the transmission is short, this should be the end of operation of the modem,
           so we don't really need to worry about the residue and keeping the sample time
           current. */
        return generated_len;
    }

    public static void v34_tx_power(v34_state_t s, float power) {
        /* The constellation design seems to keep the average power the same, regardless
           of which bit rate is in use. */
        s.tx.gain = 0.223f * Telephony.db_to_amplitude_ratio(power - Telephony.DBM0_MAX_SINE_POWER) * 32768.0f / TX_PULSESHAPER_GAIN;
    }

    public static void v34_set_get_bit(v34_state_t s, span_get_bit_func_t get_bit, object? user_data) {
        if (s.tx.get_bit == s.tx.current_get_bit)
            s.tx.current_get_bit = get_bit;

        s.tx.get_bit = get_bit;
        s.tx.get_bit_user_data = user_data;
    }

    public static void v34_set_get_aux_bit(v34_state_t s, span_get_bit_func_t get_bit, object? user_data) {
        s.tx.get_aux_bit = get_bit;
        s.tx.get_aux_bit_user_data = user_data;
    }

    public static SpanLogState v34_get_logging_state(v34_state_t s) {
        return s.logging;
    }

    public static void v34_set_working_parameters(v34_parameters_t s, int baud_rate, int bit_rate, bool expanded) {
        s.bit_rate = ((bit_rate >> 1) + 1) * 2400 + (bit_rate & 1) * 200;
        s.b = baud_rate_parameters[baud_rate].mappings[bit_rate].b;
        if (s.b <= 12) {
            s.k = 0;
            s.q = 0;
        } else {
            s.k = s.b - 12;
            s.q = 0;
            while (s.k >= 32) {
                s.k -= 8;
                s.q++;
            }
        }
        s.q_mask = (1 << s.q) - 1;
        s.m = baud_rate_parameters[baud_rate].mappings[bit_rate].m[expanded ? 1 : 0];
        s.l = 4 * s.m * (1 << s.q);
        s.j = baud_rate_parameters[baud_rate].j;
        s.p = baud_rate_parameters[baud_rate].p;
        s.w = (bit_rate & 1) != 0 ? 15 - s.j : 0;
        s.r = (s.bit_rate * 28) / (s.j * 100) - (s.b - 1) * s.p;
        s.max_bit_rate_code = baud_rate_parameters[baud_rate].max_bit_rate_code;
        s.samples_per_symbol_numerator = baud_rate_parameters[baud_rate].samples_per_symbol_numerator;
        s.samples_per_symbol_denominator = baud_rate_parameters[baud_rate].samples_per_symbol_denominator;
    }

    public static int v34_get_current_bit_rate(v34_state_t s) {
        return s.bit_rate;
    }

    public static int v34_half_duplex_change_mode(v34_state_t s, int mode) {
        switch (mode) {
            case V34_HALF_DUPLEX_SOURCE:
            case V34_HALF_DUPLEX_RECIPIENT:
                s.rx.half_duplex_source =
                s.tx.half_duplex_source =
                s.half_duplex_source = mode != 0;
                break;
            case V34_HALF_DUPLEX_CONTROL_CHANNEL:
                s.rx.half_duplex_state =
                s.tx.half_duplex_state =
                s.half_duplex_state = mode != 0;
                break;
            case V34_HALF_DUPLEX_PRIMARY_CHANNEL:
                s.rx.half_duplex_state =
                s.tx.half_duplex_state =
                s.half_duplex_state = mode != 0;
                break;
            case V34_HALF_DUPLEX_SILENCE:
                s.rx.half_duplex_state =
                s.tx.half_duplex_state =
                s.half_duplex_state = mode != 0;
                break;
        }

        return 0;
    }

    private static int v34_tx_restart(v34_state_t s, int baud_rate, int bit_rate, bool high_carrier) {
        s.tx.bit_rate = bit_rate;
        s.tx.baud_rate = baud_rate;
        s.tx.high_carrier = high_carrier;

        s.tx.v34_carrier_phase_rate = Dds.dds_phase_ratef(carrier_frequency(s.tx.baud_rate, s.tx.high_carrier));
        if (s.calling_party) {
            s.tx.cc_carrier_phase_rate = Dds.dds_phase_ratef(1200.0f);
            s.tx.guard_phase_rate = 0;
            s.tx.guard_level = 0.0f;
        } else {
            s.tx.cc_carrier_phase_rate = Dds.dds_phase_ratef(2400.0f);
            s.tx.guard_phase_rate = 0; //Dds.dds_phase_ratef(1800.0f);
            s.tx.guard_level = 4.0f;
        }

        v34_set_working_parameters(s.tx.parms, s.tx.baud_rate, s.tx.bit_rate, true);

        Array.Clear(s.tx.rrc_filter_re);
        Array.Clear(s.tx.rrc_filter_im);
        s.tx.lastbit = new complexf_t(0.0f, 0.0f);
        s.tx.rrc_filter_step = 0;
        s.tx.convolution = 0;
        s.tx.scramble_reg = 0;
        s.tx.carrier_phase = 0;

        s.tx.txbits = 0;
        s.tx.txptr = 0;
        s.tx.diff = 0;

        s.tx.line_probe_step = 0;
        s.tx.line_probe_cycles = 0;
        s.tx.line_probe_scaling = 0.0008f * s.tx.gain;

        s.tx.training_stage = 0x100;
        tx_silence_init(s, 75);

        s.tx.v0_pattern = 0;
        s.tx.super_frame = 0;
        s.tx.data_frame = 0;
        s.tx.s_bit_cnt = 0;
        s.tx.aux_bit_cnt = 0;

        s.tx.conv_encode_table = v34_conv16_encode_table;

        s.tx.current_get_bit = s.tx.get_bit;
        return 0;
    }

    private static int bit_rate_to_code(int bit_rate) {
        int code;
        int rate;

        /* Translate between the bit rate as an integer and an internal code that
           represents the N*2400 bps and the possible extra 200 bps for auxilliary data. */
        if (bit_rate > 36800)
            return -1;

        code = bit_rate / 2400;
        rate = code * 2400;
        code = (code - 1) << 1;
        if (rate == bit_rate)
            return code;

        if ((rate + 200) == bit_rate)
            return (code | 1);

        return -1;
    }

    private static int baud_rate_to_code(int baud_rate) {
        int i;

        /* Translate between the baud rate, as the integer nearest approaximation to the
           actual baud rate, and a 0-5 code used internally */
        for (i = 0; i < 6; i++) {
            if (baud_rate_parameters[i].baud_rate == baud_rate)
                return i;

        }

        return -1;
    }

    public static int v34_restart(v34_state_t s, int baud_rate, int bit_rate, bool duplex) {
        int bit_rate_code;
        int baud_rate_code;
        bool high_carrier;

        LoggingApi.span_log(s.logging!, LoggingApi.SPAN_LOG_FLOW, "Tx - Restarting V.34, %d baud, %dbps\n", baud_rate, bit_rate);
        high_carrier = true;
        if ((bit_rate_code = bit_rate_to_code(bit_rate)) < 0)
            return -1;

        if ((baud_rate_code = baud_rate_to_code(baud_rate)) < 0)
            return -1;

        /* Check the bit rate and baud rate combination is valid */
        if (baud_rate_parameters[baud_rate_code].mappings[bit_rate_code].b == 0)
            return -1;

        s.duplex =
        s.rx.duplex =
        s.tx.duplex = duplex;

        /* Select the default half-duplex configuration */
        s.rx.half_duplex_source =
        s.tx.half_duplex_source =
        s.half_duplex_source = ((s.calling_party) ? V34_HALF_DUPLEX_SOURCE : V34_HALF_DUPLEX_RECIPIENT) != 0;

        v34_tx_restart(s, baud_rate_code, bit_rate_code, high_carrier);
        v34_rx_restart(s, baud_rate_code, bit_rate_code, high_carrier ? 1 : 0);

        return 0;
    }

    public static v34_state_t? v34_init(v34_state_t? s,
                                     int baud_rate,
                                     int bit_rate,
                                     bool calling_party,
                                     bool duplex,
                                     span_get_bit_func_t get_bit,
                                     object? get_bit_user_data,
                                     span_put_bit_func_t put_bit,
                                     object? put_bit_user_data) {
        int bit_rate_code;
        int baud_rate_code;

        if ((baud_rate_code = baud_rate_to_code(baud_rate)) < 0)
            return null;

        if ((bit_rate_code = bit_rate_to_code(bit_rate)) < 0)
            return null;

        /* Check the bit rate and baud rate combination is valid */
        if (baud_rate_parameters[baud_rate_code].mappings[bit_rate_code].b == 0)
            return null;

        if (s is null)
            s = new v34_state_t();
        else {
            s.calling_party = false;
            s.duplex = false;
            s.half_duplex_source = false;
            s.half_duplex_state = false;
            s.bit_rate = 0;
            s.tx = new v34_tx_state_t();
            s.rx = new v34_rx_state_t();
            s.ec = null;
            s.logging = new SpanLogState();
        }

        LoggingApi.span_log_init(s.logging, LoggingApi.SPAN_LOG_NONE, null);
        LoggingApi.span_log_set_protocol(s.logging, "V.34");
        s.rx.logging = s.logging;
        s.tx.logging = s.logging;
        s.bit_rate = bit_rate;
        s.calling_party =
        s.rx.calling_party =
        s.tx.calling_party = calling_party;

        s.rx.stage = V34_RX_STAGE_INFO0;

        s.tx.get_bit = get_bit;
        s.tx.get_bit_user_data = get_bit_user_data;
        v34_tx_power(s, -14.0f);
        v34_restart(s, baud_rate, bit_rate, duplex);

        s.rx.put_bit = put_bit;
        s.rx.put_bit_user_data = put_bit_user_data;
        v34_rx_set_signal_cutoff(s, -45.5f);
        s.rx.agc_scaling = 0.0017f / V34_RX_PULSESHAPER_GAIN;
        s.rx.agc_scaling_save = 0.0f;
        s.rx.carrier_phase_rate_save = 0;

        if (calling_party) {
            s.tx.scrambler_tap = 17;
            s.rx.scrambler_tap = 4;
        } else {
            s.tx.scrambler_tap = 4;
            s.rx.scrambler_tap = 17;
        }

        return s;
    }

    public static int v34_release(v34_state_t s) {
        return 0;
    }

    public static int v34_free(v34_state_t? s) {
        return 0;
    }
}
