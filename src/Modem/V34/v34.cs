/*
 * TKFaxEngine - direct C# conversion of the TKFaxEngineFX/spanDSP V.34 sources.
 *
 * v34.cs - public and private V.34 declarations translated from v34.h.
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

namespace TKFaxEngine.Modem.V34;

public delegate int span_get_bit_func_t(object? user_data);
public delegate void span_put_bit_func_t(object? user_data, int bit);
public delegate void qam_report_handler_t(object? user_data, complexf_t? constellation, complexf_t? target, int value);

public struct complexf_t {
    public float re;
    public float im;

    public complexf_t(float re, float im) {
        this.re = re;
        this.im = im;
    }

    public static complexf_t operator +(complexf_t a, complexf_t b) => new(a.re + b.re, a.im + b.im);
    public static complexf_t operator -(complexf_t a, complexf_t b) => new(a.re - b.re, a.im - b.im);
    public static complexf_t operator -(complexf_t a) => new(-a.re, -a.im);
    public static complexf_t operator *(complexf_t a, complexf_t b) =>
        new(a.re * b.re - a.im * b.im, a.re * b.im + a.im * b.re);
    public static complexf_t operator *(complexf_t a, float b) => new(a.re * b, a.im * b);
    public static complexf_t operator *(float a, complexf_t b) => new(a * b.re, a * b.im);
    public static complexf_t operator /(complexf_t a, float b) => new(a.re / b, a.im / b);
    public static implicit operator complexf_t(DdsComplexFloat value) => new(value.Real, value.Imaginary);
}

public struct complexi16_t {
    public short re;
    public short im;

    public complexi16_t(short re, short im) {
        this.re = re;
        this.im = im;
    }

    public complexi16_t(int re, int im) {
        this.re = unchecked((short)re);
        this.im = unchecked((short)im);
    }
}

public struct complexi_t {
    public int re;
    public int im;

    public complexi_t(int re, int im) {
        this.re = re;
        this.im = im;
    }
}

public sealed class bitstream_state_t {
    internal bool lsb_first;
    internal ulong residual;
    internal int residual_bits;
}

public sealed class v34_capabilities_t {
    public bool[] support_baud_rate_low_carrier = new bool[6];
    public bool[] support_baud_rate_high_carrier = new bool[6];
    public bool support_power_reduction;
    public byte max_baud_rate_difference;
    public bool support_1664_point_constellation;
    public byte tx_clock_source;
    public bool from_cme_modem;
    public bool rate_3429_allowed;
}

public sealed class info1c_baud_rate_parms_t {
    public bool use_high_carrier;
    public int pre_emphasis;
    public int max_bit_rate;
}

public sealed class info1c_t {
    public int power_reduction;
    public int additional_power_reduction;
    public int md;
    public int freq_offset;
    public info1c_baud_rate_parms_t[] rate_data =
    [
        new(), new(), new(), new(), new(), new()
    ];
}

public sealed class info1a_t {
    public int power_reduction;
    public int additional_power_reduction;
    public int md;
    public int freq_offset;
    public bool use_high_carrier;
    public int preemphasis_filter;
    public int max_data_rate;
    public int baud_rate_a_to_c;
    public int baud_rate_c_to_a;
}

public sealed class infoh_t {
    public int power_reduction;
    public int length_of_trn;
    public bool use_high_carrier;
    public int preemphasis_filter;
    public int baud_rate;
    public bool trn16;
}

public sealed class mp_t {
    public int type;
    public int bit_rate_a_to_c;
    public int bit_rate_c_to_a;
    public int aux_channel_supported;
    public int trellis_size;
    public bool use_non_linear_encoder;
    public bool expanded_shaping;
    public bool mp_acknowledged;
    public int signalling_rate_mask;
    public bool asymmetric_rates_allowed;
    public complexi16_t[] precoder_coeffs = new complexi16_t[3];
}

public sealed class mph_t {
    public int type;
    public int max_data_rate;
    public int control_channel_2400;
    public int trellis_size;
    public bool use_non_linear_encoder;
    public bool expanded_shaping;
    public int signalling_rate_mask;
    public bool asymmetric_rates_allowed;
    public complexi16_t[] precoder_coeffs = new complexi16_t[3];
}

public sealed class v34_parameters_t {
    public int max_bit_rate_code;
    public int bit_rate;
    public int b;
    public int j;
    public int k;
    public int l;
    public int m;
    public int p;
    public int q;
    public int q_mask;
    public int r;
    public int w;
    public int samples_per_symbol_numerator;
    public int samples_per_symbol_denominator;
}

public sealed class ted_t {
    public float[] symbol_sync_low = new float[2];
    public float[] symbol_sync_high = new float[2];
    public float[] symbol_sync_dc_filter = new float[2];
    public float baud_phase;
    public float[] low_band_edge_coeff = new float[3];
    public float[] high_band_edge_coeff = new float[3];
    public float mixed_edges_coeff_3;
}

public sealed class viterbi_slot_t {
    public uint[] cumulative_path_metric = new uint[16];
    public ushort[] previous_path_ptr = new ushort[16];
    public ushort[] pts = new ushort[16];
    public ushort[] branch_error_x = new ushort[8];
    public complexi16_t[,] bb = new complexi16_t[2, 8];
}

public sealed class viterbi_t {
    public viterbi_slot_t[] vit =
    [
        new(), new(), new(), new(), new(), new(), new(), new(),
        new(), new(), new(), new(), new(), new(), new(), new()
    ];
    public int ptr;
    public int windup;
    public short curr_min_state;
    public short[,] error = new short[2, 4];
    public ushort[] branch_error = new ushort[8];
    public byte[,]? conv_decode_table;
}

internal delegate complexf_t v34_getbaud_func_t(v34_state_t s);

public sealed class v34_tx_state_t {
    public bool calling_party;
    public bool duplex;
    public bool half_duplex_source;
    public bool half_duplex_state;
    public int bit_rate;
    public span_get_bit_func_t? get_bit;
    public object? get_bit_user_data;
    public span_get_bit_func_t? get_aux_bit;
    public object? get_aux_bit_user_data;
    public int baud_rate;
    public bool high_carrier;
    public uint scramble_reg;
    public int scrambler_tap;
    public bool use_non_linear_encoder;
    internal v34_getbaud_func_t? current_getbaud;
    public uint r0;
    public ushort[] qbits = new ushort[8];
    public ushort[] ibits = new ushort[4];
    public int[] mjk = new int[8];
    public int step_2d;
    public bitstream_state_t bs = new();
    public uint bitstream;
    public int i;
    public v34_parameters_t parms = new();
    public complexi16_t[] x = new complexi16_t[8 + v34.V34_XOFF];
    public complexi16_t[] precoder_coeffs = new complexi16_t[3];
    public complexi16_t c;
    public complexi16_t p;
    public int z;
    public int y0;
    public int state;
    public float gain;
    public float[] rrc_filter_re = new float[v34.V34_INFO_TX_FILTER_STEPS];
    public float[] rrc_filter_im = new float[v34.V34_INFO_TX_FILTER_STEPS];
    public complexf_t lastbit;
    public int rrc_filter_step;
    public uint carrier_phase;
    public int cc_carrier_phase_rate;
    public int v34_carrier_phase_rate;
    public uint guard_phase;
    public int guard_phase_rate;
    public float guard_level;
    public int baud_phase;
    public int stage;
    public int convolution;
    public int training_stage;
    public int current_modulator;
    public int diff;
    public int line_probe_cycles;
    public int line_probe_step;
    public float line_probe_scaling;
    public int tone_duration;
    public int super_frame;
    public int data_frame;
    public int s_bit_cnt;
    public int aux_bit_cnt;
    public ushort v0_pattern;
    public byte[] txbuf = new byte[50];
    public int txbits;
    public int txptr;
    public byte[,]? conv_encode_table;
    public bool info0_acknowledgement;
    public info1a_t info1a = new();
    public info1c_t info1c = new();
    public infoh_t infoh = new();
    public mp_t mp = new();
    public mph_t mph = new();
    public int persistence2;
    public span_get_bit_func_t? current_get_bit;
    public long sample_time;
    public SpanLogState? logging;
}

public sealed class v34_rx_state_t {
    internal v34_state_t? owner;
    public bool calling_party;
    public bool duplex;
    public bool half_duplex_source;
    public bool half_duplex_state;
    public int bit_rate;
    public span_put_bit_func_t? put_bit;
    public object? put_bit_user_data;
    public span_put_bit_func_t? put_aux_bit;
    public object? put_aux_bit_user_data;
    public qam_report_handler_t? qam_report;
    public object? qam_user_data;
    public int baud_rate;
    public bool high_carrier;
    public int stage;
    public int received_event;
    public uint scramble_reg;
    public int scrambler_tap;
    public ushort v0_pattern;
    public PowerMeterState power = new(4);
    public int carrier_on_power;
    public int carrier_off_power;
    public bool signal_present;
    public bitstream_state_t bs = new();
    public uint bitstream;
    public uint r0;
    public ushort[] qbits = new ushort[8];
    public ushort[] ibits = new ushort[4];
    public int[] mjk = new int[8];
    public int step_2d;
    public v34_parameters_t parms = new();
    public complexi16_t yt;
    public complexi16_t[] xt = new complexi16_t[4];
    public complexi16_t[] x = new complexi16_t[3];
    public complexi16_t[] h = new complexi16_t[3];
    public complexi16_t[,] xy = new complexi16_t[2, 4];
    public viterbi_t viterbi = new();
    public short[] ww = new short[3];
    public uint carrier_phase;
    public int carrier_phase_rate_save;
    public int cc_carrier_phase_rate;
    public int v34_carrier_phase_rate;
    public float[] rrc_filter = new float[v34.V34_RX_FILTER_STEPS];
    public int rrc_filter_step;
    public int eq_step;
    public int eq_put_step;
    public int shaper_sets;
    public float agc_scaling;
    public float agc_scaling_save;
    public ted_t pri_ted = new();
    public ted_t cc_ted = new();
    public float carrier_track_p;
    public float carrier_track_i;
    public float[,]? shaper_re;
    public float[,]? shaper_im;
    public int total_baud_timing_correction;
    public int baud_half;
    public int round_trip_delay_estimate;
    public int duration;
    public int bit_count;
    public int target_bits;
    public ushort crc;
    public uint[] last_angles = new uint[2];
    public byte[] info_buf = new byte[25];
    public int super_frame;
    public int data_frame;
    public int s_bit_cnt;
    public int aux_bit_cnt;
    public byte[] rxbuf = new byte[50];
    public int rxbits;
    public int rxptr;
    public int blip_duration;
    public v34_capabilities_t far_capabilities = new();
    public int carrier_drop_pending;
    public int low_samples;
    public short high_sample;
    public bool info0_acknowledgement;
    public info1a_t info1a = new();
    public info1c_t info1c = new();
    public infoh_t infoh = new();
    public int step;
    public int persistence1;
    public int persistence2;
    public int mp_count;
    public int mp_len;
    public int mp_and_fill_len;
    public int mp_seen;
    public int dft_ptr;
    public complexf_t[] dft_buffer = new complexf_t[160];
    public float[] l1_l2_gains = new float[25];
    public float[] l1_l2_phases = new float[25];
    public float base_phase;
    public complexf_t last_sample;
    public int l1_l2_duration;
    public int current_demodulator;
    public long sample_time;
    public long tone_ab_hop_time;
    public SpanLogState? logging;

    public complexf_t[] eq_coeff = new complexf_t[v34.V34_EQUALIZER_MASK + 1];
    public complexf_t[] eq_coeff_save = new complexf_t[v34.V34_EQUALIZER_MASK + 1];
    public complexf_t[] eq_buf = new complexf_t[v34.V34_EQUALIZER_MASK + 1];
}

public sealed class v34_state_t {
    public bool calling_party;
    public bool duplex;
    public bool half_duplex_source;
    public bool half_duplex_state;
    public int bit_rate;
    public v34_tx_state_t tx = new();
    public v34_rx_state_t rx = new();
    public object? ec;
    public SpanLogState logging = new();
}

public static partial class v34 {
    public const float V34_CONSTELLATION_SCALING_FACTOR = 1.0f;

    public const int V34_SUPPORT_2400 = 0x0001;
    public const int V34_SUPPORT_4800 = 0x0002;
    public const int V34_SUPPORT_7200 = 0x0004;
    public const int V34_SUPPORT_9600 = 0x0008;
    public const int V34_SUPPORT_12000 = 0x0010;
    public const int V34_SUPPORT_14400 = 0x0020;
    public const int V34_SUPPORT_16800 = 0x0040;
    public const int V34_SUPPORT_19200 = 0x0080;
    public const int V34_SUPPORT_21600 = 0x0100;
    public const int V34_SUPPORT_24000 = 0x0200;
    public const int V34_SUPPORT_26400 = 0x0400;
    public const int V34_SUPPORT_28800 = 0x0800;
    public const int V34_SUPPORT_31200 = 0x1000;
    public const int V34_SUPPORT_33600 = 0x2000;

    public const int V34_HALF_DUPLEX_SOURCE = 0;
    public const int V34_HALF_DUPLEX_RECIPIENT = 1;
    public const int V34_HALF_DUPLEX_CONTROL_CHANNEL = 2;
    public const int V34_HALF_DUPLEX_PRIMARY_CHANNEL = 3;
    public const int V34_HALF_DUPLEX_SILENCE = 4;

    internal const int V34_INFO_TX_FILTER_STEPS = 9;
    internal const int V34_TX_FILTER_STEPS = 9;
    internal const int V34_RX_FILTER_STEPS = 27;
    internal const int V34_RX_PULSESHAPER_COEFF_SETS = 192;
    internal const int V34_RX_CC_PULSESHAPER_COEFF_SETS = 12;
    internal const int V34_EQUALIZER_PRE_LEN = 63;
    internal const int V34_EQUALIZER_POST_LEN = 63;
    internal const int V34_EQUALIZER_MASK = 127;
    internal const int V34_XOFF = 3;
    internal const float V34_RX_PULSESHAPER_GAIN = 1.0f;

    internal const int V34_MODULATION_V34 = 0;
    internal const int V34_MODULATION_CC = 1;
    internal const int V34_MODULATION_TONES = 2;
    internal const int V34_MODULATION_L1_L2 = 3;
    internal const int V34_MODULATION_SILENCE = 4;

    internal const int V34_RX_STAGE_INFO0 = 1;
    internal const int V34_RX_STAGE_INFOH = 2;
    internal const int V34_RX_STAGE_INFO1C = 3;
    internal const int V34_RX_STAGE_INFO1A = 4;
    internal const int V34_RX_STAGE_TONE_A = 5;
    internal const int V34_RX_STAGE_TONE_B = 6;
    internal const int V34_RX_STAGE_L1_L2 = 7;
    internal const int V34_RX_STAGE_CC = 8;
    internal const int V34_RX_STAGE_PRIMARY_CHANNEL = 9;

    internal const int V34_TX_STAGE_INITIAL_PREAMBLE = 1;
    internal const int V34_TX_STAGE_INFO0 = 2;
    internal const int V34_TX_STAGE_INITIAL_A = 3;
    internal const int V34_TX_STAGE_FIRST_A = 4;
    internal const int V34_TX_STAGE_FIRST_NOT_A = 5;
    internal const int V34_TX_STAGE_FIRST_NOT_A_REVERSAL_SEEN = 6;
    internal const int V34_TX_STAGE_SECOND_A = 7;
    internal const int V34_TX_STAGE_L1 = 8;
    internal const int V34_TX_STAGE_L2 = 9;
    internal const int V34_TX_STAGE_POST_L2_A = 10;
    internal const int V34_TX_STAGE_POST_L2_NOT_A = 11;
    internal const int V34_TX_STAGE_A_SILENCE = 12;
    internal const int V34_TX_STAGE_PRE_INFO1_A = 13;
    internal const int V34_TX_STAGE_INFO1 = 14;
    internal const int V34_TX_STAGE_FIRST_B = 15;
    internal const int V34_TX_STAGE_FIRST_B_INFO_SEEN = 16;
    internal const int V34_TX_STAGE_FIRST_NOT_B_WAIT = 17;
    internal const int V34_TX_STAGE_FIRST_NOT_B = 18;
    internal const int V34_TX_STAGE_FIRST_B_SILENCE = 19;
    internal const int V34_TX_STAGE_FIRST_B_POST_REVERSAL_SILENCE = 20;
    internal const int V34_TX_STAGE_SECOND_B = 21;
    internal const int V34_TX_STAGE_SECOND_B_WAIT = 22;
    internal const int V34_TX_STAGE_SECOND_NOT_B = 23;
    internal const int V34_TX_STAGE_INFO0_RETRY = 24;
    internal const int V34_TX_STAGE_FIRST_S = 25;
    internal const int V34_TX_STAGE_FIRST_NOT_S = 26;
    internal const int V34_TX_STAGE_MD = 27;
    internal const int V34_TX_STAGE_SECOND_S = 28;
    internal const int V34_TX_STAGE_SECOND_NOT_S = 29;
    internal const int V34_TX_STAGE_TRN = 30;
    internal const int V34_TX_STAGE_J = 31;
    internal const int V34_TX_STAGE_J_DASHED = 32;
    internal const int V34_TX_STAGE_MP = 33;
    internal const int V34_TX_STAGE_HDX_INITIAL_A = 34;
    internal const int V34_TX_STAGE_HDX_FIRST_A = 35;
    internal const int V34_TX_STAGE_HDX_FIRST_NOT_A = 36;
    internal const int V34_TX_STAGE_HDX_FIRST_A_SILENCE = 37;
    internal const int V34_TX_STAGE_HDX_SECOND_A = 38;
    internal const int V34_TX_STAGE_HDX_SECOND_A_WAIT = 39;
    internal const int V34_TX_STAGE_HDX_FIRST_B = 40;
    internal const int V34_TX_STAGE_HDX_FIRST_B_INFO_SEEN = 41;
    internal const int V34_TX_STAGE_HDX_FIRST_NOT_B_WAIT = 42;
    internal const int V34_TX_STAGE_HDX_FIRST_NOT_B = 43;
    internal const int V34_TX_STAGE_HDX_POST_L2_B = 44;
    internal const int V34_TX_STAGE_HDX_POST_L2_SILENCE = 45;
    internal const int V34_TX_STAGE_HDX_SH = 46;
    internal const int V34_TX_STAGE_HDX_FIRST_ALT = 47;
    internal const int V34_TX_STAGE_HDX_PPH = 48;
    internal const int V34_TX_STAGE_HDX_SECOND_ALT = 49;
    internal const int V34_TX_STAGE_HDX_MPH = 50;
    internal const int V34_TX_STAGE_HDX_E = 51;

    internal const int V34_EVENT_NONE = 0;
    internal const int V34_EVENT_TONE_SEEN = 1;
    internal const int V34_EVENT_REVERSAL_1 = 2;
    internal const int V34_EVENT_REVERSAL_2 = 3;
    internal const int V34_EVENT_REVERSAL_3 = 4;
    internal const int V34_EVENT_INFO0_OK = 5;
    internal const int V34_EVENT_INFO0_BAD = 6;
    internal const int V34_EVENT_INFO1_OK = 7;
    internal const int V34_EVENT_INFO1_BAD = 8;
    internal const int V34_EVENT_INFOH_OK = 9;
    internal const int V34_EVENT_INFOH_BAD = 10;
    internal const int V34_EVENT_L2_SEEN = 11;
    internal const int V34_EVENT_S = 12;

    internal const int V34_BAUD_RATE_2400 = 0;
    internal const int V34_BAUD_RATE_2743 = 1;
    internal const int V34_BAUD_RATE_2800 = 2;
    internal const int V34_BAUD_RATE_3000 = 3;
    internal const int V34_BAUD_RATE_3200 = 4;
    internal const int V34_BAUD_RATE_3429 = 5;

    internal const int V34_TRELLIS_16 = 0;
    internal const int V34_TRELLIS_32 = 1;
    internal const int V34_TRELLIS_64 = 2;
    internal const int V34_TRELLIS_RESERVED = 3;

    internal const int TX_CLOCK_SOURCE_INTERNAL = 0;
    internal const int TX_CLOCK_SOURCE_SYNCED_TO_RX = 1;
    internal const int TX_CLOCK_SOURCE_EXTERNAL = 2;
    internal const int TX_CLOCK_SOURCE_RESERVED_FOR_ITU_T = 3;

    internal const int V34_RATER = 0x00;
    internal const int V34_RATEU = 0x03;
    internal const int V34_PRECODER = 0x05;
    internal const int V34_PRECODEU = 0x0A;

    internal const int SAMPLE_RATE = 8000;
    internal const int SIG_STATUS_CARRIER_DOWN = -1;
    internal const int SIG_STATUS_CARRIER_UP = -2;
    internal const int SIG_STATUS_TRAINING_IN_PROGRESS = -3;
    internal const int SIG_STATUS_TRAINING_SUCCEEDED = -4;
    internal const int SIG_STATUS_TRAINING_FAILED = -5;
    internal const int SIG_STATUS_END_OF_DATA = -7;
    internal const int SIG_STATUS_SHUTDOWN_COMPLETE = -10;
}
