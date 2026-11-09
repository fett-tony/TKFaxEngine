/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * V29TxTables.cs
 *
 * Combined managed C# port of:
 *   v29tx_rrc.h
 *   v29tx_constellation_maps.h
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2008, 2012 Steve Underwood.
 *
 * This file preserves the GNU Lesser General Public License version 2.1
 * licensing terms of the original source files.
 */

#nullable enable

namespace TKFaxEngine.Modem.V29;

/// <summary>
/// Floating-point complex coordinate used by the V.29 transmitter tables.
/// </summary>
public readonly record struct V29TxConstellationPoint(
    float Real,
    float Imaginary) {
    public float Re => Real;

    public float Im => Imaginary;
}

/// <summary>
/// Signed 16-bit form of a V.29 constellation coordinate.
/// </summary>
public readonly record struct V29TxConstellationPointInt16(
    short Real,
    short Imaginary) {
    public short Re => Real;

    public short Im => Imaginary;
}

/// <summary>
/// V.29 transmit root-raised-cosine filter and constellation tables.
/// </summary>
public static class V29TxTables {
    public const int PulseShaperCoefficientSets = 10;

    public const int PulseShaperTaps = 9;

    /// <summary>Gain used by the original floating-point build.</summary>
    public const float PulseShaperGain = 1.000000f;

    /// <summary>Gain used by the original fixed-point build.</summary>
    public const float FixedPointPulseShaperGain = 0.948561f;

    /// <summary>Scale used by TX_PULSESHAPER_SCALE in fixed-point builds.</summary>
    public const double FixedPointPulseShaperScale = 31081.491463;

    public static readonly float[][] PulseShaper =
    [
        [
            -0.0028949626f, -0.0180558777f, 0.0644370035f,
            -0.1680546392f, 0.6136030985f, 0.6136030984f,
            -0.1680546392f, 0.0644370034f, -0.0180558778f
        ],
        [
            0.0031457248f, -0.0296755147f, 0.0821538018f,
            -0.1948071696f, 0.7563219631f, 0.4608861941f,
            -0.1273859915f, 0.0418434579f, -0.0059021774f
        ],
        [
            0.0095859909f, -0.0389394472f, 0.0918555210f,
            -0.2016880234f, 0.8793516917f, 0.3081345068f,
            -0.0792085179f, 0.0176601554f, 0.0051283325f
        ],
        [
            0.0153896883f, -0.0441001646f, 0.0909724653f,
            -0.1838386340f, 0.9741012686f, 0.1647552955f,
            -0.0297442724f, -0.0050682341f, 0.0137350940f
        ],
        [
            0.0194884088f, -0.0437412561f, 0.0779044330f,
            -0.1380831560f, 1.0338274098f, 0.0388498604f,
            0.0155354801f, -0.0238603979f, 0.0191007894f
        ],
        [
            0.0209425252f, -0.0370198693f, 0.0523524602f,
            -0.0633894605f, 1.0542286891f, -0.0633894606f,
            0.0523524602f, -0.0370198693f, 0.0209425251f
        ],
        [
            0.0191007894f, -0.0238603978f, 0.0155354801f,
            0.0388498605f, 1.0338274098f, -0.1380831561f,
            0.0779044330f, -0.0437412561f, 0.0194884087f
        ],
        [
            0.0137350940f, -0.0050682341f, -0.0297442724f,
            0.1647552955f, 0.9741012686f, -0.1838386340f,
            0.0909724652f, -0.0441001646f, 0.0153896883f
        ],
        [
            0.0051283326f, 0.0176601554f, -0.0792085179f,
            0.3081345069f, 0.8793516917f, -0.2016880235f,
            0.0918555209f, -0.0389394473f, 0.0095859909f
        ],
        [
            -0.0059021774f, 0.0418434580f, -0.1273859915f,
            0.4608861942f, 0.7563219631f, -0.1948071696f,
            0.0821538018f, -0.0296755147f, 0.0031457248f
        ]
    ];

    /// <summary>
    /// Alternating A/B training constellation.
    /// Entries 0/1 apply to 9600 bit/s, 2/3 to 7200 bit/s and
    /// 4/5 to 4800 bit/s.
    /// </summary>
    public static readonly V29TxConstellationPoint[] AbabConstellation =
    [
        new V29TxConstellationPoint(3.0000000000f, -3.0000000000f),
        new V29TxConstellationPoint(-3.0000000000f, 0.0000000000f),
        new V29TxConstellationPoint(1.0000000000f, -1.0000000000f),
        new V29TxConstellationPoint(-3.0000000000f, 0.0000000000f),
        new V29TxConstellationPoint(0.0000000000f, -3.0000000000f),
        new V29TxConstellationPoint(-3.0000000000f, 0.0000000000f)
    ];

    /// <summary>
    /// Alternating C/D training constellation.
    /// Entries 0/1 apply to 9600 bit/s, 2/3 to 7200 bit/s and
    /// 4/5 to 4800 bit/s.
    /// </summary>
    public static readonly V29TxConstellationPoint[] CdcdConstellation =
    [
        new V29TxConstellationPoint(3.0000000000f, 0.0000000000f),
        new V29TxConstellationPoint(-3.0000000000f, 3.0000000000f),
        new V29TxConstellationPoint(3.0000000000f, 0.0000000000f),
        new V29TxConstellationPoint(-1.0000000000f, 1.0000000000f),
        new V29TxConstellationPoint(3.0000000000f, 0.0000000000f),
        new V29TxConstellationPoint(0.0000000000f, 3.0000000000f)
    ];

    /// <summary>
    /// Complete sixteen-point V.29 constellation used for 9600 bit/s.
    /// The first eight points are also used by the 7200 bit/s mode.
    /// </summary>
    public static readonly V29TxConstellationPoint[] Constellation9600 =
    [
        new V29TxConstellationPoint(3.0000000000f, 0.0000000000f),
        new V29TxConstellationPoint(1.0000000000f, 1.0000000000f),
        new V29TxConstellationPoint(0.0000000000f, 3.0000000000f),
        new V29TxConstellationPoint(-1.0000000000f, 1.0000000000f),
        new V29TxConstellationPoint(-3.0000000000f, 0.0000000000f),
        new V29TxConstellationPoint(-1.0000000000f, -1.0000000000f),
        new V29TxConstellationPoint(0.0000000000f, -3.0000000000f),
        new V29TxConstellationPoint(1.0000000000f, -1.0000000000f),
        new V29TxConstellationPoint(5.0000000000f, 0.0000000000f),
        new V29TxConstellationPoint(3.0000000000f, 3.0000000000f),
        new V29TxConstellationPoint(0.0000000000f, 5.0000000000f),
        new V29TxConstellationPoint(-3.0000000000f, 3.0000000000f),
        new V29TxConstellationPoint(-5.0000000000f, 0.0000000000f),
        new V29TxConstellationPoint(-3.0000000000f, -3.0000000000f),
        new V29TxConstellationPoint(0.0000000000f, -5.0000000000f),
        new V29TxConstellationPoint(3.0000000000f, -3.0000000000f)
    ];

    /// <summary>
    /// Returns one floating-point RRC coefficient set.
    /// </summary>
    public static ReadOnlySpan<float> GetPulseShaper(int coefficientSet) {
        if ((uint)coefficientSet >= (uint)PulseShaper.Length)
            throw new ArgumentOutOfRangeException(nameof(coefficientSet));

        return PulseShaper[coefficientSet];
    }

    /// <summary>
    /// Converts one RRC coefficient set using the native fixed-point
    /// TX_PULSESHAPER_SCALE rounding rule.
    /// </summary>
    public static short[] GetFixedPointPulseShaper(int coefficientSet) {
        ReadOnlySpan<float> source = GetPulseShaper(coefficientSet);
        short[] result = new short[source.Length];

        for (int index = 0; index < source.Length; index++)
            result[index] = ScalePulseShaperToFixedPoint(source[index]);

        return result;
    }

    public static short ScalePulseShaperToFixedPoint(float value) {
        double rounded =
            FixedPointPulseShaperScale * value +
            (value >= 0.0f ? 0.5 : -0.5);

        return checked((short)(int)rounded);
    }

    /// <summary>
    /// Converts the floating-point constellation values like the native
    /// FP_CONSTELLATION_SCALE/FP_SCALE fixed-point path.
    /// </summary>
    public static V29TxConstellationPointInt16 ToFixedPoint(
        V29TxConstellationPoint point) {
        return new V29TxConstellationPointInt16(
            checked((short)point.Real),
            checked((short)point.Imaginary));
    }

    public static ReadOnlySpan<V29TxConstellationPoint> GetTrainingConstellation(
        bool cdcd) {
        return cdcd ? CdcdConstellation : AbabConstellation;
    }

    public static ReadOnlySpan<V29TxConstellationPoint> GetDataConstellation(
        int bitRate) {
        return bitRate switch {
            9600 => Constellation9600,
            7200 => Constellation9600.AsSpan(0, 8),
            4800 => Constellation9600.AsSpan(0, 8),
            _ => throw new ArgumentOutOfRangeException(
                nameof(bitRate),
                bitRate,
                "Only 4800, 7200 and 9600 bit/s are supported.")
        };
    }

    // Native-style aliases retained for straightforward migration.
    public static float[][] tx_pulseshaper => PulseShaper;

    public static V29TxConstellationPoint[] v29_abab_constellation =>
        AbabConstellation;

    public static V29TxConstellationPoint[] v29_cdcd_constellation =>
        CdcdConstellation;

    public static V29TxConstellationPoint[] v29_9600_constellation =>
        Constellation9600;
}
