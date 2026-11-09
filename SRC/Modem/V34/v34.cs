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

/// <summary>
/// Represents the <c>span_get_bit_func_t</c> callback.
/// </summary>
/// <param name="user_data">The user data value.</param>
/// <returns>The result of the operation.</returns>
public delegate int span_get_bit_func_t(object? user_data);
/// <summary>
/// Represents the <c>span_put_bit_func_t</c> callback.
/// </summary>
/// <param name="user_data">The user data value.</param>
/// <param name="bit">The bit value.</param>
public delegate void span_put_bit_func_t(object? user_data, int bit);
/// <summary>
/// Represents the <c>qam_report_handler_t</c> callback.
/// </summary>
/// <param name="user_data">The user data value.</param>
/// <param name="constellation">The constellation value.</param>
/// <param name="target">The target value.</param>
/// <param name="value">The value value.</param>
public delegate void qam_report_handler_t(object? user_data, complexf_t? constellation, complexf_t? target, int value);

/// <summary>
/// Represents the <c>complexf_t</c> type.
/// </summary>
public struct complexf_t {
    /// <summary>
    /// Stores the <c>re</c> value.
    /// </summary>
    public float re;
    /// <summary>
    /// Stores the <c>im</c> value.
    /// </summary>
    public float im;

    /// <summary>
    /// Initializes a new instance of the <c>complexf_t</c> type.
    /// </summary>
    /// <param name="re">The re value.</param>
    /// <param name="im">The im value.</param>
    public complexf_t(float re, float im) {
        this.re = re;
        this.im = im;
    }

    /// <summary>
    /// Applies the <c>operator +</c> operator.
    /// </summary>
    /// <param name="a">The a value.</param>
    /// <param name="b">The b value.</param>
    /// <returns>The result of the operation.</returns>
    public static complexf_t operator +(complexf_t a, complexf_t b) => new(a.re + b.re, a.im + b.im);
    /// <summary>
    /// Applies the <c>operator -</c> operator.
    /// </summary>
    /// <param name="a">The a value.</param>
    /// <param name="b">The b value.</param>
    /// <returns>The result of the operation.</returns>
    public static complexf_t operator -(complexf_t a, complexf_t b) => new(a.re - b.re, a.im - b.im);
    /// <summary>
    /// Applies the <c>operator -</c> operator.
    /// </summary>
    /// <param name="a">The a value.</param>
    /// <returns>The result of the operation.</returns>
    public static complexf_t operator -(complexf_t a) => new(-a.re, -a.im);
    /// <summary>
    /// Applies the <c>operator *</c> operator.
    /// </summary>
    /// <param name="a">The a value.</param>
    /// <param name="b">The b value.</param>
    /// <returns>The result of the operation.</returns>
    public static complexf_t operator *(complexf_t a, complexf_t b) =>
        new(a.re * b.re - a.im * b.im, a.re * b.im + a.im * b.re);
    /// <summary>
    /// Applies the <c>operator *</c> operator.
    /// </summary>
    /// <param name="a">The a value.</param>
    /// <param name="b">The b value.</param>
    /// <returns>The result of the operation.</returns>
    public static complexf_t operator *(complexf_t a, float b) => new(a.re * b, a.im * b);
    /// <summary>
    /// Applies the <c>operator *</c> operator.
    /// </summary>
    /// <param name="a">The a value.</param>
    /// <param name="b">The b value.</param>
    /// <returns>The result of the operation.</returns>
    public static complexf_t operator *(float a, complexf_t b) => new(a * b.re, a * b.im);
    /// <summary>
    /// Applies the <c>operator /</c> operator.
    /// </summary>
    /// <param name="a">The a value.</param>
    /// <param name="b">The b value.</param>
    /// <returns>The result of the operation.</returns>
    public static complexf_t operator /(complexf_t a, float b) => new(a.re / b, a.im / b);
    /// <summary>
    /// Applies the <c>implicit operator complexf_t</c> operator.
    /// </summary>
    /// <param name="value">The value value.</param>
    /// <returns>The result of the operation.</returns>
    public static implicit operator complexf_t(DdsComplexFloat value) => new(value.Real, value.Imaginary);
}

/// <summary>
/// Represents the <c>complexi16_t</c> type.
/// </summary>
public struct complexi16_t {
    /// <summary>
    /// Stores the <c>re</c> value.
    /// </summary>
    public short re;
    /// <summary>
    /// Stores the <c>im</c> value.
    /// </summary>
    public short im;

    /// <summary>
    /// Initializes a new instance of the <c>complexi16_t</c> type.
    /// </summary>
    /// <param name="re">The re value.</param>
    /// <param name="im">The im value.</param>
    public complexi16_t(short re, short im) {
        this.re = re;
        this.im = im;
    }

    /// <summary>
    /// Initializes a new instance of the <c>complexi16_t</c> type.
    /// </summary>
    /// <param name="re">The re value.</param>
    /// <param name="im">The im value.</param>
    public complexi16_t(int re, int im) {
        this.re = unchecked((short)re);
        this.im = unchecked((short)im);
    }
}

/// <summary>
/// Represents the <c>complexi_t</c> type.
/// </summary>
public struct complexi_t {
    /// <summary>
    /// Stores the <c>re</c> value.
    /// </summary>
    public int re;
    /// <summary>
    /// Stores the <c>im</c> value.
    /// </summary>
    public int im;

    /// <summary>
    /// Initializes a new instance of the <c>complexi_t</c> type.
    /// </summary>
    /// <param name="re">The re value.</param>
    /// <param name="im">The im value.</param>
    public complexi_t(int re, int im) {
        this.re = re;
        this.im = im;
    }
}

/// <summary>
/// Represents the <c>bitstream_state_t</c> type.
/// </summary>
public sealed class bitstream_state_t {
    internal bool lsb_first;
    internal ulong residual;
    internal int residual_bits;
}

/// <summary>
/// Represents the <c>v34_capabilities_t</c> type.
/// </summary>
public sealed class v34_capabilities_t {
    /// <summary>
    /// Stores the <c>support_baud_rate_low_carrier</c> value.
    /// </summary>
    public bool[] support_baud_rate_low_carrier = new bool[6];
    /// <summary>
    /// Stores the <c>support_baud_rate_high_carrier</c> value.
    /// </summary>
    public bool[] support_baud_rate_high_carrier = new bool[6];
    /// <summary>
    /// Stores the <c>support_power_reduction</c> value.
    /// </summary>
    public bool support_power_reduction;
    /// <summary>
    /// Stores the <c>max_baud_rate_difference</c> value.
    /// </summary>
    public byte max_baud_rate_difference;
    /// <summary>
    /// Stores the <c>support_1664_point_constellation</c> value.
    /// </summary>
    public bool support_1664_point_constellation;
    /// <summary>
    /// Stores the <c>tx_clock_source</c> value.
    /// </summary>
    public byte tx_clock_source;
    /// <summary>
    /// Stores the <c>from_cme_modem</c> value.
    /// </summary>
    public bool from_cme_modem;
    /// <summary>
    /// Stores the <c>rate_3429_allowed</c> value.
    /// </summary>
    public bool rate_3429_allowed;
}

/// <summary>
/// Represents the <c>info1c_baud_rate_parms_t</c> type.
/// </summary>
public sealed class info1c_baud_rate_parms_t {
    /// <summary>
    /// Stores the <c>use_high_carrier</c> value.
    /// </summary>
    public bool use_high_carrier;
    /// <summary>
    /// Stores the <c>pre_emphasis</c> value.
    /// </summary>
    public int pre_emphasis;
    /// <summary>
    /// Stores the <c>max_bit_rate</c> value.
    /// </summary>
    public int max_bit_rate;
}

/// <summary>
/// Represents the <c>info1c_t</c> type.
/// </summary>
public sealed class info1c_t {
    /// <summary>
    /// Stores the <c>power_reduction</c> value.
    /// </summary>
    public int power_reduction;
    /// <summary>
    /// Stores the <c>additional_power_reduction</c> value.
    /// </summary>
    public int additional_power_reduction;
    /// <summary>
    /// Stores the <c>md</c> value.
    /// </summary>
    public int md;
    /// <summary>
    /// Stores the <c>freq_offset</c> value.
    /// </summary>
    public int freq_offset;
    /// <summary>
    /// Stores the <c>rate_data</c> value.
    /// </summary>
    public info1c_baud_rate_parms_t[] rate_data =
    [
        new(), new(), new(), new(), new(), new()
    ];
}

/// <summary>
/// Represents the <c>info1a_t</c> type.
/// </summary>
public sealed class info1a_t {
    /// <summary>
    /// Stores the <c>power_reduction</c> value.
    /// </summary>
    public int power_reduction;
    /// <summary>
    /// Stores the <c>additional_power_reduction</c> value.
    /// </summary>
    public int additional_power_reduction;
    /// <summary>
    /// Stores the <c>md</c> value.
    /// </summary>
    public int md;
    /// <summary>
    /// Stores the <c>freq_offset</c> value.
    /// </summary>
    public int freq_offset;
    /// <summary>
    /// Stores the <c>use_high_carrier</c> value.
    /// </summary>
    public bool use_high_carrier;
    /// <summary>
    /// Stores the <c>preemphasis_filter</c> value.
    /// </summary>
    public int preemphasis_filter;
    /// <summary>
    /// Stores the <c>max_data_rate</c> value.
    /// </summary>
    public int max_data_rate;
    /// <summary>
    /// Stores the <c>baud_rate_a_to_c</c> value.
    /// </summary>
    public int baud_rate_a_to_c;
    /// <summary>
    /// Stores the <c>baud_rate_c_to_a</c> value.
    /// </summary>
    public int baud_rate_c_to_a;
}

/// <summary>
/// Represents the <c>infoh_t</c> type.
/// </summary>
public sealed class infoh_t {
    /// <summary>
    /// Stores the <c>power_reduction</c> value.
    /// </summary>
    public int power_reduction;
    /// <summary>
    /// Stores the <c>length_of_trn</c> value.
    /// </summary>
    public int length_of_trn;
    /// <summary>
    /// Stores the <c>use_high_carrier</c> value.
    /// </summary>
    public bool use_high_carrier;
    /// <summary>
    /// Stores the <c>preemphasis_filter</c> value.
    /// </summary>
    public int preemphasis_filter;
    /// <summary>
    /// Stores the <c>baud_rate</c> value.
    /// </summary>
    public int baud_rate;
    /// <summary>
    /// Stores the <c>trn16</c> value.
    /// </summary>
    public bool trn16;
}

/// <summary>
/// Represents the <c>mp_t</c> type.
/// </summary>
public sealed class mp_t {
    /// <summary>
    /// Stores the <c>type</c> value.
    /// </summary>
    public int type;
    /// <summary>
    /// Stores the <c>bit_rate_a_to_c</c> value.
    /// </summary>
    public int bit_rate_a_to_c;
    /// <summary>
    /// Stores the <c>bit_rate_c_to_a</c> value.
    /// </summary>
    public int bit_rate_c_to_a;
    /// <summary>
    /// Stores the <c>aux_channel_supported</c> value.
    /// </summary>
    public int aux_channel_supported;
    /// <summary>
    /// Stores the <c>trellis_size</c> value.
    /// </summary>
    public int trellis_size;
    /// <summary>
    /// Stores the <c>use_non_linear_encoder</c> value.
    /// </summary>
    public bool use_non_linear_encoder;
    /// <summary>
    /// Stores the <c>expanded_shaping</c> value.
    /// </summary>
    public bool expanded_shaping;
    /// <summary>
    /// Stores the <c>mp_acknowledged</c> value.
    /// </summary>
    public bool mp_acknowledged;
    /// <summary>
    /// Stores the <c>signalling_rate_mask</c> value.
    /// </summary>
    public int signalling_rate_mask;
    /// <summary>
    /// Stores the <c>asymmetric_rates_allowed</c> value.
    /// </summary>
    public bool asymmetric_rates_allowed;
    /// <summary>
    /// Stores the <c>precoder_coeffs</c> value.
    /// </summary>
    public complexi16_t[] precoder_coeffs = new complexi16_t[3];
}

/// <summary>
/// Represents the <c>mph_t</c> type.
/// </summary>
public sealed class mph_t {
    /// <summary>
    /// Stores the <c>type</c> value.
    /// </summary>
    public int type;
    /// <summary>
    /// Stores the <c>max_data_rate</c> value.
    /// </summary>
    public int max_data_rate;
    /// <summary>
    /// Stores the <c>control_channel_2400</c> value.
    /// </summary>
    public int control_channel_2400;
    /// <summary>
    /// Stores the <c>trellis_size</c> value.
    /// </summary>
    public int trellis_size;
    /// <summary>
    /// Stores the <c>use_non_linear_encoder</c> value.
    /// </summary>
    public bool use_non_linear_encoder;
    /// <summary>
    /// Stores the <c>expanded_shaping</c> value.
    /// </summary>
    public bool expanded_shaping;
    /// <summary>
    /// Stores the <c>signalling_rate_mask</c> value.
    /// </summary>
    public int signalling_rate_mask;
    /// <summary>
    /// Stores the <c>asymmetric_rates_allowed</c> value.
    /// </summary>
    public bool asymmetric_rates_allowed;
    /// <summary>
    /// Stores the <c>precoder_coeffs</c> value.
    /// </summary>
    public complexi16_t[] precoder_coeffs = new complexi16_t[3];
}

/// <summary>
/// Represents the <c>v34_parameters_t</c> type.
/// </summary>
public sealed class v34_parameters_t {
    /// <summary>
    /// Stores the <c>max_bit_rate_code</c> value.
    /// </summary>
    public int max_bit_rate_code;
    /// <summary>
    /// Stores the <c>bit_rate</c> value.
    /// </summary>
    public int bit_rate;
    /// <summary>
    /// Stores the <c>b</c> value.
    /// </summary>
    public int b;
    /// <summary>
    /// Stores the <c>j</c> value.
    /// </summary>
    public int j;
    /// <summary>
    /// Stores the <c>k</c> value.
    /// </summary>
    public int k;
    /// <summary>
    /// Stores the <c>l</c> value.
    /// </summary>
    public int l;
    /// <summary>
    /// Stores the <c>m</c> value.
    /// </summary>
    public int m;
    /// <summary>
    /// Stores the <c>p</c> value.
    /// </summary>
    public int p;
    /// <summary>
    /// Stores the <c>q</c> value.
    /// </summary>
    public int q;
    /// <summary>
    /// Stores the <c>q_mask</c> value.
    /// </summary>
    public int q_mask;
    /// <summary>
    /// Stores the <c>r</c> value.
    /// </summary>
    public int r;
    /// <summary>
    /// Stores the <c>w</c> value.
    /// </summary>
    public int w;
    /// <summary>
    /// Stores the <c>samples_per_symbol_numerator</c> value.
    /// </summary>
    public int samples_per_symbol_numerator;
    /// <summary>
    /// Stores the <c>samples_per_symbol_denominator</c> value.
    /// </summary>
    public int samples_per_symbol_denominator;
}

/// <summary>
/// Represents the <c>ted_t</c> type.
/// </summary>
public sealed class ted_t {
    /// <summary>
    /// Stores the <c>symbol_sync_low</c> value.
    /// </summary>
    public float[] symbol_sync_low = new float[2];
    /// <summary>
    /// Stores the <c>symbol_sync_high</c> value.
    /// </summary>
    public float[] symbol_sync_high = new float[2];
    /// <summary>
    /// Stores the <c>symbol_sync_dc_filter</c> value.
    /// </summary>
    public float[] symbol_sync_dc_filter = new float[2];
    /// <summary>
    /// Stores the <c>baud_phase</c> value.
    /// </summary>
    public float baud_phase;
    /// <summary>
    /// Stores the <c>low_band_edge_coeff</c> value.
    /// </summary>
    public float[] low_band_edge_coeff = new float[3];
    /// <summary>
    /// Stores the <c>high_band_edge_coeff</c> value.
    /// </summary>
    public float[] high_band_edge_coeff = new float[3];
    /// <summary>
    /// Stores the <c>mixed_edges_coeff_3</c> value.
    /// </summary>
    public float mixed_edges_coeff_3;
}

/// <summary>
/// Represents the <c>viterbi_slot_t</c> type.
/// </summary>
public sealed class viterbi_slot_t {
    /// <summary>
    /// Stores the <c>cumulative_path_metric</c> value.
    /// </summary>
    public uint[] cumulative_path_metric = new uint[16];
    /// <summary>
    /// Stores the <c>previous_path_ptr</c> value.
    /// </summary>
    public ushort[] previous_path_ptr = new ushort[16];
    /// <summary>
    /// Stores the <c>pts</c> value.
    /// </summary>
    public ushort[] pts = new ushort[16];
    /// <summary>
    /// Stores the <c>branch_error_x</c> value.
    /// </summary>
    public ushort[] branch_error_x = new ushort[8];
    /// <summary>
    /// Stores the <c>bb</c> value.
    /// </summary>
    public complexi16_t[,] bb = new complexi16_t[2, 8];
}

/// <summary>
/// Represents the <c>viterbi_t</c> type.
/// </summary>
public sealed class viterbi_t {
    /// <summary>
    /// Stores the <c>vit</c> value.
    /// </summary>
    public viterbi_slot_t[] vit =
    [
        new(), new(), new(), new(), new(), new(), new(), new(),
        new(), new(), new(), new(), new(), new(), new(), new()
    ];
    /// <summary>
    /// Stores the <c>ptr</c> value.
    /// </summary>
    public int ptr;
    /// <summary>
    /// Stores the <c>windup</c> value.
    /// </summary>
    public int windup;
    /// <summary>
    /// Stores the <c>curr_min_state</c> value.
    /// </summary>
    public short curr_min_state;
    /// <summary>
    /// Stores the <c>error</c> value.
    /// </summary>
    public short[,] error = new short[2, 4];
    /// <summary>
    /// Stores the <c>branch_error</c> value.
    /// </summary>
    public ushort[] branch_error = new ushort[8];
    /// <summary>
    /// Stores the <c>conv_decode_table</c> value.
    /// </summary>
    public byte[,]? conv_decode_table;
}

internal delegate complexf_t v34_getbaud_func_t(v34_state_t s);

/// <summary>
/// Represents the <c>v34_tx_state_t</c> type.
/// </summary>
public sealed class v34_tx_state_t {
    /// <summary>
    /// Stores the <c>calling_party</c> value.
    /// </summary>
    public bool calling_party;
    /// <summary>
    /// Stores the <c>duplex</c> value.
    /// </summary>
    public bool duplex;
    /// <summary>
    /// Stores the <c>half_duplex_source</c> value.
    /// </summary>
    public bool half_duplex_source;
    /// <summary>
    /// Stores the <c>half_duplex_state</c> value.
    /// </summary>
    public bool half_duplex_state;
    /// <summary>
    /// Stores the <c>bit_rate</c> value.
    /// </summary>
    public int bit_rate;
    /// <summary>
    /// Stores the <c>get_bit</c> value.
    /// </summary>
    public span_get_bit_func_t? get_bit;
    /// <summary>
    /// Stores the <c>get_bit_user_data</c> value.
    /// </summary>
    public object? get_bit_user_data;
    /// <summary>
    /// Stores the <c>get_aux_bit</c> value.
    /// </summary>
    public span_get_bit_func_t? get_aux_bit;
    /// <summary>
    /// Stores the <c>get_aux_bit_user_data</c> value.
    /// </summary>
    public object? get_aux_bit_user_data;
    /// <summary>
    /// Stores the <c>baud_rate</c> value.
    /// </summary>
    public int baud_rate;
    /// <summary>
    /// Stores the <c>high_carrier</c> value.
    /// </summary>
    public bool high_carrier;
    /// <summary>
    /// Stores the <c>scramble_reg</c> value.
    /// </summary>
    public uint scramble_reg;
    /// <summary>
    /// Stores the <c>scrambler_tap</c> value.
    /// </summary>
    public int scrambler_tap;
    /// <summary>
    /// Stores the <c>use_non_linear_encoder</c> value.
    /// </summary>
    public bool use_non_linear_encoder;
    internal v34_getbaud_func_t? current_getbaud;
    /// <summary>
    /// Stores the <c>r0</c> value.
    /// </summary>
    public uint r0;
    /// <summary>
    /// Stores the <c>qbits</c> value.
    /// </summary>
    public ushort[] qbits = new ushort[8];
    /// <summary>
    /// Stores the <c>ibits</c> value.
    /// </summary>
    public ushort[] ibits = new ushort[4];
    /// <summary>
    /// Stores the <c>mjk</c> value.
    /// </summary>
    public int[] mjk = new int[8];
    /// <summary>
    /// Stores the <c>step_2d</c> value.
    /// </summary>
    public int step_2d;
    /// <summary>
    /// Stores the <c>bs</c> value.
    /// </summary>
    public bitstream_state_t bs = new();
    /// <summary>
    /// Stores the <c>bitstream</c> value.
    /// </summary>
    public uint bitstream;
    /// <summary>
    /// Stores the <c>i</c> value.
    /// </summary>
    public int i;
    /// <summary>
    /// Stores the <c>parms</c> value.
    /// </summary>
    public v34_parameters_t parms = new();
    /// <summary>
    /// Stores the <c>x</c> value.
    /// </summary>
    public complexi16_t[] x = new complexi16_t[8 + v34.V34_XOFF];
    /// <summary>
    /// Stores the <c>precoder_coeffs</c> value.
    /// </summary>
    public complexi16_t[] precoder_coeffs = new complexi16_t[3];
    /// <summary>
    /// Stores the <c>c</c> value.
    /// </summary>
    public complexi16_t c;
    /// <summary>
    /// Stores the <c>p</c> value.
    /// </summary>
    public complexi16_t p;
    /// <summary>
    /// Stores the <c>z</c> value.
    /// </summary>
    public int z;
    /// <summary>
    /// Stores the <c>y0</c> value.
    /// </summary>
    public int y0;
    /// <summary>
    /// Stores the <c>state</c> value.
    /// </summary>
    public int state;
    /// <summary>
    /// Stores the <c>gain</c> value.
    /// </summary>
    public float gain;
    /// <summary>
    /// Stores the <c>rrc_filter_re</c> value.
    /// </summary>
    public float[] rrc_filter_re = new float[v34.V34_INFO_TX_FILTER_STEPS];
    /// <summary>
    /// Stores the <c>rrc_filter_im</c> value.
    /// </summary>
    public float[] rrc_filter_im = new float[v34.V34_INFO_TX_FILTER_STEPS];
    /// <summary>
    /// Stores the <c>lastbit</c> value.
    /// </summary>
    public complexf_t lastbit;
    /// <summary>
    /// Stores the <c>rrc_filter_step</c> value.
    /// </summary>
    public int rrc_filter_step;
    /// <summary>
    /// Stores the <c>carrier_phase</c> value.
    /// </summary>
    public uint carrier_phase;
    /// <summary>
    /// Stores the <c>cc_carrier_phase_rate</c> value.
    /// </summary>
    public int cc_carrier_phase_rate;
    /// <summary>
    /// Stores the <c>v34_carrier_phase_rate</c> value.
    /// </summary>
    public int v34_carrier_phase_rate;
    /// <summary>
    /// Stores the <c>guard_phase</c> value.
    /// </summary>
    public uint guard_phase;
    /// <summary>
    /// Stores the <c>guard_phase_rate</c> value.
    /// </summary>
    public int guard_phase_rate;
    /// <summary>
    /// Stores the <c>guard_level</c> value.
    /// </summary>
    public float guard_level;
    /// <summary>
    /// Stores the <c>baud_phase</c> value.
    /// </summary>
    public int baud_phase;
    /// <summary>
    /// Stores the <c>stage</c> value.
    /// </summary>
    public int stage;
    /// <summary>
    /// Stores the <c>convolution</c> value.
    /// </summary>
    public int convolution;
    /// <summary>
    /// Stores the <c>training_stage</c> value.
    /// </summary>
    public int training_stage;
    /// <summary>
    /// Stores the <c>current_modulator</c> value.
    /// </summary>
    public int current_modulator;
    /// <summary>
    /// Stores the <c>diff</c> value.
    /// </summary>
    public int diff;
    /// <summary>
    /// Stores the <c>line_probe_cycles</c> value.
    /// </summary>
    public int line_probe_cycles;
    /// <summary>
    /// Stores the <c>line_probe_step</c> value.
    /// </summary>
    public int line_probe_step;
    /// <summary>
    /// Stores the <c>line_probe_scaling</c> value.
    /// </summary>
    public float line_probe_scaling;
    /// <summary>
    /// Stores the <c>tone_duration</c> value.
    /// </summary>
    public int tone_duration;
    /// <summary>
    /// Stores the <c>super_frame</c> value.
    /// </summary>
    public int super_frame;
    /// <summary>
    /// Stores the <c>data_frame</c> value.
    /// </summary>
    public int data_frame;
    /// <summary>
    /// Stores the <c>s_bit_cnt</c> value.
    /// </summary>
    public int s_bit_cnt;
    /// <summary>
    /// Stores the <c>aux_bit_cnt</c> value.
    /// </summary>
    public int aux_bit_cnt;
    /// <summary>
    /// Stores the <c>v0_pattern</c> value.
    /// </summary>
    public ushort v0_pattern;
    /// <summary>
    /// Stores the <c>txbuf</c> value.
    /// </summary>
    public byte[] txbuf = new byte[50];
    /// <summary>
    /// Stores the <c>txbits</c> value.
    /// </summary>
    public int txbits;
    /// <summary>
    /// Stores the <c>txptr</c> value.
    /// </summary>
    public int txptr;
    /// <summary>
    /// Stores the <c>conv_encode_table</c> value.
    /// </summary>
    public byte[,]? conv_encode_table;
    /// <summary>
    /// Stores the <c>info0_acknowledgement</c> value.
    /// </summary>
    public bool info0_acknowledgement;
    /// <summary>
    /// Stores the <c>info1a</c> value.
    /// </summary>
    public info1a_t info1a = new();
    /// <summary>
    /// Stores the <c>info1c</c> value.
    /// </summary>
    public info1c_t info1c = new();
    /// <summary>
    /// Stores the <c>infoh</c> value.
    /// </summary>
    public infoh_t infoh = new();
    /// <summary>
    /// Stores the <c>mp</c> value.
    /// </summary>
    public mp_t mp = new();
    /// <summary>
    /// Stores the <c>mph</c> value.
    /// </summary>
    public mph_t mph = new();
    /// <summary>
    /// Stores the <c>persistence2</c> value.
    /// </summary>
    public int persistence2;
    /// <summary>
    /// Stores the <c>current_get_bit</c> value.
    /// </summary>
    public span_get_bit_func_t? current_get_bit;
    /// <summary>
    /// Stores the <c>sample_time</c> value.
    /// </summary>
    public long sample_time;
    /// <summary>
    /// Stores the <c>logging</c> value.
    /// </summary>
    public SpanLogState? logging;
}

/// <summary>
/// Represents the <c>v34_rx_state_t</c> type.
/// </summary>
public sealed class v34_rx_state_t {
    internal v34_state_t? owner;
    /// <summary>
    /// Stores the <c>calling_party</c> value.
    /// </summary>
    public bool calling_party;
    /// <summary>
    /// Stores the <c>duplex</c> value.
    /// </summary>
    public bool duplex;
    /// <summary>
    /// Stores the <c>half_duplex_source</c> value.
    /// </summary>
    public bool half_duplex_source;
    /// <summary>
    /// Stores the <c>half_duplex_state</c> value.
    /// </summary>
    public bool half_duplex_state;
    /// <summary>
    /// Stores the <c>bit_rate</c> value.
    /// </summary>
    public int bit_rate;
    /// <summary>
    /// Stores the <c>put_bit</c> value.
    /// </summary>
    public span_put_bit_func_t? put_bit;
    /// <summary>
    /// Stores the <c>put_bit_user_data</c> value.
    /// </summary>
    public object? put_bit_user_data;
    /// <summary>
    /// Stores the <c>put_aux_bit</c> value.
    /// </summary>
    public span_put_bit_func_t? put_aux_bit;
    /// <summary>
    /// Stores the <c>put_aux_bit_user_data</c> value.
    /// </summary>
    public object? put_aux_bit_user_data;
    /// <summary>
    /// Stores the <c>qam_report</c> value.
    /// </summary>
    public qam_report_handler_t? qam_report;
    /// <summary>
    /// Stores the <c>qam_user_data</c> value.
    /// </summary>
    public object? qam_user_data;
    /// <summary>
    /// Stores the <c>baud_rate</c> value.
    /// </summary>
    public int baud_rate;
    /// <summary>
    /// Stores the <c>high_carrier</c> value.
    /// </summary>
    public bool high_carrier;
    /// <summary>
    /// Stores the <c>stage</c> value.
    /// </summary>
    public int stage;
    /// <summary>
    /// Stores the <c>received_event</c> value.
    /// </summary>
    public int received_event;
    /// <summary>
    /// Stores the <c>scramble_reg</c> value.
    /// </summary>
    public uint scramble_reg;
    /// <summary>
    /// Stores the <c>scrambler_tap</c> value.
    /// </summary>
    public int scrambler_tap;
    /// <summary>
    /// Stores the <c>v0_pattern</c> value.
    /// </summary>
    public ushort v0_pattern;
    /// <summary>
    /// Stores the <c>power</c> value.
    /// </summary>
    public PowerMeterState power = new(4);
    /// <summary>
    /// Stores the <c>carrier_on_power</c> value.
    /// </summary>
    public int carrier_on_power;
    /// <summary>
    /// Stores the <c>carrier_off_power</c> value.
    /// </summary>
    public int carrier_off_power;
    /// <summary>
    /// Stores the <c>signal_present</c> value.
    /// </summary>
    public bool signal_present;
    /// <summary>
    /// Stores the <c>bs</c> value.
    /// </summary>
    public bitstream_state_t bs = new();
    /// <summary>
    /// Stores the <c>bitstream</c> value.
    /// </summary>
    public uint bitstream;
    /// <summary>
    /// Stores the <c>r0</c> value.
    /// </summary>
    public uint r0;
    /// <summary>
    /// Stores the <c>qbits</c> value.
    /// </summary>
    public ushort[] qbits = new ushort[8];
    /// <summary>
    /// Stores the <c>ibits</c> value.
    /// </summary>
    public ushort[] ibits = new ushort[4];
    /// <summary>
    /// Stores the <c>mjk</c> value.
    /// </summary>
    public int[] mjk = new int[8];
    /// <summary>
    /// Stores the <c>step_2d</c> value.
    /// </summary>
    public int step_2d;
    /// <summary>
    /// Stores the <c>parms</c> value.
    /// </summary>
    public v34_parameters_t parms = new();
    /// <summary>
    /// Stores the <c>yt</c> value.
    /// </summary>
    public complexi16_t yt;
    /// <summary>
    /// Stores the <c>xt</c> value.
    /// </summary>
    public complexi16_t[] xt = new complexi16_t[4];
    /// <summary>
    /// Stores the <c>x</c> value.
    /// </summary>
    public complexi16_t[] x = new complexi16_t[3];
    /// <summary>
    /// Stores the <c>h</c> value.
    /// </summary>
    public complexi16_t[] h = new complexi16_t[3];
    /// <summary>
    /// Stores the <c>xy</c> value.
    /// </summary>
    public complexi16_t[,] xy = new complexi16_t[2, 4];
    /// <summary>
    /// Stores the <c>viterbi</c> value.
    /// </summary>
    public viterbi_t viterbi = new();
    /// <summary>
    /// Stores the <c>ww</c> value.
    /// </summary>
    public short[] ww = new short[3];
    /// <summary>
    /// Stores the <c>carrier_phase</c> value.
    /// </summary>
    public uint carrier_phase;
    /// <summary>
    /// Stores the <c>carrier_phase_rate_save</c> value.
    /// </summary>
    public int carrier_phase_rate_save;
    /// <summary>
    /// Stores the <c>cc_carrier_phase_rate</c> value.
    /// </summary>
    public int cc_carrier_phase_rate;
    /// <summary>
    /// Stores the <c>v34_carrier_phase_rate</c> value.
    /// </summary>
    public int v34_carrier_phase_rate;
    /// <summary>
    /// Stores the <c>rrc_filter</c> value.
    /// </summary>
    public float[] rrc_filter = new float[v34.V34_RX_FILTER_STEPS];
    /// <summary>
    /// Stores the <c>rrc_filter_step</c> value.
    /// </summary>
    public int rrc_filter_step;
    /// <summary>
    /// Stores the <c>eq_step</c> value.
    /// </summary>
    public int eq_step;
    /// <summary>
    /// Stores the <c>eq_put_step</c> value.
    /// </summary>
    public int eq_put_step;
    /// <summary>
    /// Stores the <c>shaper_sets</c> value.
    /// </summary>
    public int shaper_sets;
    /// <summary>
    /// Stores the <c>agc_scaling</c> value.
    /// </summary>
    public float agc_scaling;
    /// <summary>
    /// Stores the <c>agc_scaling_save</c> value.
    /// </summary>
    public float agc_scaling_save;
    /// <summary>
    /// Stores the <c>pri_ted</c> value.
    /// </summary>
    public ted_t pri_ted = new();
    /// <summary>
    /// Stores the <c>cc_ted</c> value.
    /// </summary>
    public ted_t cc_ted = new();
    /// <summary>
    /// Stores the <c>carrier_track_p</c> value.
    /// </summary>
    public float carrier_track_p;
    /// <summary>
    /// Stores the <c>carrier_track_i</c> value.
    /// </summary>
    public float carrier_track_i;
    /// <summary>
    /// Stores the <c>shaper_re</c> value.
    /// </summary>
    public float[,]? shaper_re;
    /// <summary>
    /// Stores the <c>shaper_im</c> value.
    /// </summary>
    public float[,]? shaper_im;
    /// <summary>
    /// Stores the <c>total_baud_timing_correction</c> value.
    /// </summary>
    public int total_baud_timing_correction;
    /// <summary>
    /// Stores the <c>baud_half</c> value.
    /// </summary>
    public int baud_half;
    /// <summary>
    /// Stores the <c>round_trip_delay_estimate</c> value.
    /// </summary>
    public int round_trip_delay_estimate;
    /// <summary>
    /// Stores the <c>duration</c> value.
    /// </summary>
    public int duration;
    /// <summary>
    /// Stores the <c>bit_count</c> value.
    /// </summary>
    public int bit_count;
    /// <summary>
    /// Stores the <c>target_bits</c> value.
    /// </summary>
    public int target_bits;
    /// <summary>
    /// Stores the <c>crc</c> value.
    /// </summary>
    public ushort crc;
    /// <summary>
    /// Stores the <c>last_angles</c> value.
    /// </summary>
    public uint[] last_angles = new uint[2];
    /// <summary>
    /// Stores the <c>info_buf</c> value.
    /// </summary>
    public byte[] info_buf = new byte[25];
    /// <summary>
    /// Stores the <c>super_frame</c> value.
    /// </summary>
    public int super_frame;
    /// <summary>
    /// Stores the <c>data_frame</c> value.
    /// </summary>
    public int data_frame;
    /// <summary>
    /// Stores the <c>s_bit_cnt</c> value.
    /// </summary>
    public int s_bit_cnt;
    /// <summary>
    /// Stores the <c>aux_bit_cnt</c> value.
    /// </summary>
    public int aux_bit_cnt;
    /// <summary>
    /// Stores the <c>rxbuf</c> value.
    /// </summary>
    public byte[] rxbuf = new byte[50];
    /// <summary>
    /// Stores the <c>rxbits</c> value.
    /// </summary>
    public int rxbits;
    /// <summary>
    /// Stores the <c>rxptr</c> value.
    /// </summary>
    public int rxptr;
    /// <summary>
    /// Stores the <c>blip_duration</c> value.
    /// </summary>
    public int blip_duration;
    /// <summary>
    /// Stores the <c>far_capabilities</c> value.
    /// </summary>
    public v34_capabilities_t far_capabilities = new();
    /// <summary>
    /// Stores the <c>carrier_drop_pending</c> value.
    /// </summary>
    public int carrier_drop_pending;
    /// <summary>
    /// Stores the <c>low_samples</c> value.
    /// </summary>
    public int low_samples;
    /// <summary>
    /// Stores the <c>high_sample</c> value.
    /// </summary>
    public short high_sample;
    /// <summary>
    /// Stores the <c>info0_acknowledgement</c> value.
    /// </summary>
    public bool info0_acknowledgement;
    /// <summary>
    /// Stores the <c>info1a</c> value.
    /// </summary>
    public info1a_t info1a = new();
    /// <summary>
    /// Stores the <c>info1c</c> value.
    /// </summary>
    public info1c_t info1c = new();
    /// <summary>
    /// Stores the <c>infoh</c> value.
    /// </summary>
    public infoh_t infoh = new();
    /// <summary>
    /// Stores the <c>step</c> value.
    /// </summary>
    public int step;
    /// <summary>
    /// Stores the <c>persistence1</c> value.
    /// </summary>
    public int persistence1;
    /// <summary>
    /// Stores the <c>persistence2</c> value.
    /// </summary>
    public int persistence2;
    /// <summary>
    /// Stores the <c>mp_count</c> value.
    /// </summary>
    public int mp_count;
    /// <summary>
    /// Stores the <c>mp_len</c> value.
    /// </summary>
    public int mp_len;
    /// <summary>
    /// Stores the <c>mp_and_fill_len</c> value.
    /// </summary>
    public int mp_and_fill_len;
    /// <summary>
    /// Stores the <c>mp_seen</c> value.
    /// </summary>
    public int mp_seen;
    /// <summary>
    /// Stores the <c>dft_ptr</c> value.
    /// </summary>
    public int dft_ptr;
    /// <summary>
    /// Stores the <c>dft_buffer</c> value.
    /// </summary>
    public complexf_t[] dft_buffer = new complexf_t[160];
    /// <summary>
    /// Stores the <c>l1_l2_gains</c> value.
    /// </summary>
    public float[] l1_l2_gains = new float[25];
    /// <summary>
    /// Stores the <c>l1_l2_phases</c> value.
    /// </summary>
    public float[] l1_l2_phases = new float[25];
    /// <summary>
    /// Stores the <c>base_phase</c> value.
    /// </summary>
    public float base_phase;
    /// <summary>
    /// Stores the <c>last_sample</c> value.
    /// </summary>
    public complexf_t last_sample;
    /// <summary>
    /// Stores the <c>l1_l2_duration</c> value.
    /// </summary>
    public int l1_l2_duration;
    /// <summary>
    /// Stores the <c>current_demodulator</c> value.
    /// </summary>
    public int current_demodulator;
    /// <summary>
    /// Stores the <c>sample_time</c> value.
    /// </summary>
    public long sample_time;
    /// <summary>
    /// Stores the <c>tone_ab_hop_time</c> value.
    /// </summary>
    public long tone_ab_hop_time;
    /// <summary>
    /// Stores the <c>logging</c> value.
    /// </summary>
    public SpanLogState? logging;

    /// <summary>
    /// Stores the <c>eq_coeff</c> value.
    /// </summary>
    public complexf_t[] eq_coeff = new complexf_t[v34.V34_EQUALIZER_MASK + 1];
    /// <summary>
    /// Stores the <c>eq_coeff_save</c> value.
    /// </summary>
    public complexf_t[] eq_coeff_save = new complexf_t[v34.V34_EQUALIZER_MASK + 1];
    /// <summary>
    /// Stores the <c>eq_buf</c> value.
    /// </summary>
    public complexf_t[] eq_buf = new complexf_t[v34.V34_EQUALIZER_MASK + 1];
}

/// <summary>
/// Represents the <c>v34_state_t</c> type.
/// </summary>
public sealed class v34_state_t {
    /// <summary>
    /// Stores the <c>calling_party</c> value.
    /// </summary>
    public bool calling_party;
    /// <summary>
    /// Stores the <c>duplex</c> value.
    /// </summary>
    public bool duplex;
    /// <summary>
    /// Stores the <c>half_duplex_source</c> value.
    /// </summary>
    public bool half_duplex_source;
    /// <summary>
    /// Stores the <c>half_duplex_state</c> value.
    /// </summary>
    public bool half_duplex_state;
    /// <summary>
    /// Stores the <c>bit_rate</c> value.
    /// </summary>
    public int bit_rate;
    /// <summary>
    /// Stores the <c>tx</c> value.
    /// </summary>
    public v34_tx_state_t tx = new();
    /// <summary>
    /// Stores the <c>rx</c> value.
    /// </summary>
    public v34_rx_state_t rx = new();
    /// <summary>
    /// Stores the <c>ec</c> value.
    /// </summary>
    public object? ec;
    /// <summary>
    /// Stores the <c>logging</c> value.
    /// </summary>
    public SpanLogState logging = new();
}

/// <summary>
/// Represents the <c>v34</c> type.
/// </summary>
public static partial class v34 {
    /// <summary>
    /// Defines the <c>V34_CONSTELLATION_SCALING_FACTOR</c> constant.
    /// </summary>
    public const float V34_CONSTELLATION_SCALING_FACTOR = 1.0f;

    /// <summary>
    /// Defines the <c>V34_SUPPORT_2400</c> constant.
    /// </summary>
    public const int V34_SUPPORT_2400 = 0x0001;
    /// <summary>
    /// Defines the <c>V34_SUPPORT_4800</c> constant.
    /// </summary>
    public const int V34_SUPPORT_4800 = 0x0002;
    /// <summary>
    /// Defines the <c>V34_SUPPORT_7200</c> constant.
    /// </summary>
    public const int V34_SUPPORT_7200 = 0x0004;
    /// <summary>
    /// Defines the <c>V34_SUPPORT_9600</c> constant.
    /// </summary>
    public const int V34_SUPPORT_9600 = 0x0008;
    /// <summary>
    /// Defines the <c>V34_SUPPORT_12000</c> constant.
    /// </summary>
    public const int V34_SUPPORT_12000 = 0x0010;
    /// <summary>
    /// Defines the <c>V34_SUPPORT_14400</c> constant.
    /// </summary>
    public const int V34_SUPPORT_14400 = 0x0020;
    /// <summary>
    /// Defines the <c>V34_SUPPORT_16800</c> constant.
    /// </summary>
    public const int V34_SUPPORT_16800 = 0x0040;
    /// <summary>
    /// Defines the <c>V34_SUPPORT_19200</c> constant.
    /// </summary>
    public const int V34_SUPPORT_19200 = 0x0080;
    /// <summary>
    /// Defines the <c>V34_SUPPORT_21600</c> constant.
    /// </summary>
    public const int V34_SUPPORT_21600 = 0x0100;
    /// <summary>
    /// Defines the <c>V34_SUPPORT_24000</c> constant.
    /// </summary>
    public const int V34_SUPPORT_24000 = 0x0200;
    /// <summary>
    /// Defines the <c>V34_SUPPORT_26400</c> constant.
    /// </summary>
    public const int V34_SUPPORT_26400 = 0x0400;
    /// <summary>
    /// Defines the <c>V34_SUPPORT_28800</c> constant.
    /// </summary>
    public const int V34_SUPPORT_28800 = 0x0800;
    /// <summary>
    /// Defines the <c>V34_SUPPORT_31200</c> constant.
    /// </summary>
    public const int V34_SUPPORT_31200 = 0x1000;
    /// <summary>
    /// Defines the <c>V34_SUPPORT_33600</c> constant.
    /// </summary>
    public const int V34_SUPPORT_33600 = 0x2000;

    /// <summary>
    /// Defines the <c>V34_HALF_DUPLEX_SOURCE</c> constant.
    /// </summary>
    public const int V34_HALF_DUPLEX_SOURCE = 0;
    /// <summary>
    /// Defines the <c>V34_HALF_DUPLEX_RECIPIENT</c> constant.
    /// </summary>
    public const int V34_HALF_DUPLEX_RECIPIENT = 1;
    /// <summary>
    /// Defines the <c>V34_HALF_DUPLEX_CONTROL_CHANNEL</c> constant.
    /// </summary>
    public const int V34_HALF_DUPLEX_CONTROL_CHANNEL = 2;
    /// <summary>
    /// Defines the <c>V34_HALF_DUPLEX_PRIMARY_CHANNEL</c> constant.
    /// </summary>
    public const int V34_HALF_DUPLEX_PRIMARY_CHANNEL = 3;
    /// <summary>
    /// Defines the <c>V34_HALF_DUPLEX_SILENCE</c> constant.
    /// </summary>
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
