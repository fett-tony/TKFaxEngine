/*
 * TKFaxEngine - direct C# conversion of the TKFaxEngineFX/spanDSP V.34 sources.
 * Direct translation of v34_tables.h.
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2009 Steve Underwood.
 * Licensed under the GNU Lesser General Public License version 2.1.
 */

#nullable enable

namespace TKFaxEngine.Modem.V34;

public readonly struct mapping_t {
    public readonly byte b;
    public readonly byte[] m;

    public mapping_t(byte b, byte minimum_m, byte expanded_m) {
        this.b = b;
        m = new byte[] { minimum_m, expanded_m };
    }
}

public sealed class baud_rate_parameters_t {
    public int baud_rate;
    public int max_bit_rate_code;
    public int a;
    public int c;
    public int samples_per_symbol_numerator;
    public int samples_per_symbol_denominator;
    public int[,] low_high = new int[2, 2];
    public int j;
    public int p;
    public mapping_t[] mappings = System.Array.Empty<mapping_t>();
}

public static partial class v34 {

    internal static readonly sbyte[,] conv_encode_input = new sbyte[8, 8]
    {
        { 0, 0, 1, 1, 8, 8, 9, 9 },
        { 3, 2, 2, 3, 11, 10, 10, 11 },
        { 5, 5, 4, 4, 13, 13, 12, 12 },
        { 6, 7, 7, 6, 14, 15, 15, 14 },
        { 8, 8, 9, 9, 0, 0, 1, 1 },
        { 11, 10, 10, 11, 3, 2, 2, 3 },
        { 13, 13, 12, 12, 5, 5, 4, 4 },
        { 14, 15, 15, 14, 6, 7, 7, 6 },
    };
    internal static readonly mapping_t[] mappings_2400 = new mapping_t[]
    {
        new(8, 1, 1),
        new(9, 1, 1),
        new(16, 2, 2),
        new(17, 2, 2),
        new(24, 3, 4),
        new(25, 4, 4),
        new(32, 6, 7),
        new(33, 7, 8),
        new(40, 12, 14),
        new(41, 13, 15),
        new(48, 12, 14),
        new(49, 13, 15),
        new(56, 12, 14),
        new(57, 13, 15),
        new(64, 12, 14),
        new(65, 13, 15),
        new(72, 12, 14),
        new(73, 13, 15),
        new(0, 0, 0),
        new(0, 0, 0),
        new(0, 0, 0),
        new(0, 0, 0),
        new(0, 0, 0),
        new(0, 0, 0),
        new(0, 0, 0),
        new(0, 0, 0),
        new(0, 0, 0),
        new(0, 0, 0),
    };
    internal static readonly mapping_t[] mappings_2743 = new mapping_t[]
    {
        new(0, 0, 0),
        new(0, 0, 0),
        new(14, 2, 2),
        new(15, 2, 2),
        new(21, 3, 3),
        new(22, 3, 3),
        new(28, 4, 5),
        new(29, 5, 5),
        new(35, 8, 9),
        new(36, 8, 10),
        new(42, 14, 17),
        new(43, 15, 18),
        new(49, 13, 15),
        new(50, 14, 17),
        new(56, 12, 14),
        new(57, 13, 15),
        new(63, 11, 13),
        new(64, 12, 14),
        new(70, 10, 12),
        new(71, 11, 13),
        new(77, 9, 11),
        new(78, 10, 12),
        new(0, 0, 0),
        new(0, 0, 0),
        new(0, 0, 0),
        new(0, 0, 0),
        new(0, 0, 0),
        new(0, 0, 0),
    };
    internal static readonly mapping_t[] mappings_2800 = new mapping_t[]
    {
        new(0, 0, 0),
        new(0, 0, 0),
        new(14, 2, 2),
        new(15, 2, 2),
        new(21, 3, 3),
        new(22, 3, 3),
        new(28, 4, 5),
        new(28, 4, 5),
        new(35, 8, 9),
        new(35, 8, 9),
        new(42, 14, 17),
        new(42, 14, 17),
        new(48, 12, 14),
        new(49, 13, 15),
        new(55, 11, 13),
        new(56, 12, 14),
        new(62, 10, 12),
        new(63, 11, 13),
        new(69, 9, 11),
        new(70, 10, 12),
        new(76, 8, 10),
        new(76, 8, 10),
        new(0, 0, 0),
        new(0, 0, 0),
        new(0, 0, 0),
        new(0, 0, 0),
        new(0, 0, 0),
        new(0, 0, 0),
    };
    internal static readonly mapping_t[] mappings_3000 = new mapping_t[]
    {
        new(0, 0, 0),
        new(0, 0, 0),
        new(13, 2, 2),
        new(14, 2, 2),
        new(20, 2, 3),
        new(20, 2, 3),
        new(26, 4, 4),
        new(27, 4, 5),
        new(32, 6, 7),
        new(33, 7, 8),
        new(39, 11, 13),
        new(39, 11, 13),
        new(45, 9, 11),
        new(46, 10, 12),
        new(52, 8, 10),
        new(52, 8, 10),
        new(58, 14, 17),
        new(59, 15, 18),
        new(64, 12, 14),
        new(65, 13, 15),
        new(71, 11, 13),
        new(71, 11, 13),
        new(77, 9, 11),
        new(78, 10, 12),
        new(0, 0, 0),
        new(0, 0, 0),
        new(0, 0, 0),
        new(0, 0, 0),
    };
    internal static readonly mapping_t[] mappings_3200 = new mapping_t[]
    {
        new(0, 0, 0),
        new(0, 0, 0),
        new(12, 1, 1),
        new(13, 2, 2),
        new(18, 2, 2),
        new(19, 2, 2),
        new(24, 3, 4),
        new(25, 4, 4),
        new(30, 5, 6),
        new(31, 6, 6),
        new(36, 8, 10),
        new(37, 9, 11),
        new(42, 14, 17),
        new(43, 15, 18),
        new(48, 12, 14),
        new(49, 13, 15),
        new(54, 10, 12),
        new(55, 11, 13),
        new(60, 8, 10),
        new(61, 9, 11),
        new(66, 14, 17),
        new(67, 15, 18),
        new(72, 12, 14),
        new(73, 13, 15),
        new(78, 10, 12),
        new(79, 11, 13),
        new(0, 0, 0),
        new(0, 0, 0),
    };
    internal static readonly mapping_t[] mappings_3429 = new mapping_t[]
    {
        new(0, 0, 0),
        new(0, 0, 0),
        new(12, 1, 1),
        new(12, 1, 1),
        new(17, 2, 2),
        new(18, 2, 2),
        new(23, 3, 3),
        new(23, 3, 3),
        new(28, 4, 5),
        new(29, 5, 5),
        new(34, 7, 8),
        new(35, 8, 9),
        new(40, 12, 14),
        new(40, 12, 14),
        new(45, 9, 11),
        new(46, 10, 12),
        new(51, 15, 18),
        new(51, 15, 18),
        new(56, 12, 14),
        new(57, 13, 15),
        new(62, 10, 12),
        new(63, 11, 13),
        new(68, 8, 10),
        new(68, 8, 10),
        new(73, 13, 15),
        new(74, 14, 17),
        new(79, 11, 13),
        new(79, 11, 13),
    };
    internal static readonly baud_rate_parameters_t[] baud_rate_parameters = new baud_rate_parameters_t[]
    {
        new baud_rate_parameters_t
        {
            baud_rate = 2400, max_bit_rate_code = 16, a = 1, c = 1,
            samples_per_symbol_numerator = 10, samples_per_symbol_denominator = 3,
            low_high = new int[,] { { 2, 3 }, { 3, 4 } }, j = 7, p = 12, mappings = mappings_2400
        },
        new baud_rate_parameters_t
        {
            baud_rate = 2743, max_bit_rate_code = 20, a = 8, c = 7,
            samples_per_symbol_numerator = 35, samples_per_symbol_denominator = 12,
            low_high = new int[,] { { 3, 5 }, { 2, 3 } }, j = 8, p = 12, mappings = mappings_2743
        },
        new baud_rate_parameters_t
        {
            baud_rate = 2800, max_bit_rate_code = 20, a = 7, c = 6,
            samples_per_symbol_numerator = 20, samples_per_symbol_denominator = 7,
            low_high = new int[,] { { 3, 5 }, { 2, 3 } }, j = 7, p = 14, mappings = mappings_2800
        },
        new baud_rate_parameters_t
        {
            baud_rate = 3000, max_bit_rate_code = 22, a = 5, c = 4,
            samples_per_symbol_numerator = 8, samples_per_symbol_denominator = 3,
            low_high = new int[,] { { 3, 5 }, { 2, 3 } }, j = 7, p = 15, mappings = mappings_3000
        },
        new baud_rate_parameters_t
        {
            baud_rate = 3200, max_bit_rate_code = 24, a = 4, c = 3,
            samples_per_symbol_numerator = 5, samples_per_symbol_denominator = 2,
            low_high = new int[,] { { 4, 7 }, { 3, 5 } }, j = 7, p = 16, mappings = mappings_3200
        },
        new baud_rate_parameters_t
        {
            baud_rate = 3429, max_bit_rate_code = 26, a = 10, c = 7,
            samples_per_symbol_numerator = 7, samples_per_symbol_denominator = 3,
            low_high = new int[,] { { 4, 7 }, { 4, 7 } }, j = 8, p = 15, mappings = mappings_3429
        },
    };
    internal static readonly byte[,] k_table = new byte[16, 4]
    {
        { 0, 1, 2, 3 },
        { 2, 3, 0, 1 },
        { 1, 0, 3, 2 },
        { 3, 2, 1, 0 },
        { 4, 5, 6, 7 },
        { 6, 7, 4, 5 },
        { 5, 4, 7, 6 },
        { 7, 6, 5, 4 },
        { 2, 3, 0, 1 },
        { 0, 1, 2, 3 },
        { 3, 2, 1, 0 },
        { 1, 0, 3, 2 },
        { 6, 7, 4, 5 },
        { 4, 5, 6, 7 },
        { 7, 6, 5, 4 },
        { 5, 4, 7, 6 },
    };
    internal static readonly v34_capabilities_t v34_capabilities = new()
    {
        support_baud_rate_low_carrier = [true, true, true, true, true, true],
        support_baud_rate_high_carrier = [true, true, true, true, true, true],
        support_power_reduction = true,
        max_baud_rate_difference = 0,
        support_1664_point_constellation = true,
        tx_clock_source = TX_CLOCK_SOURCE_INTERNAL,
        from_cme_modem = false,
        rate_3429_allowed = true
    };
}
