/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * V17V32BisTxTables.cs
 *
 * Combined managed C# port of:
 *   v17_v32bis_tx_constellation_maps.h
 *   v17_v32bis_tx_rrc.h
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2004, 2012 Steve Underwood.
 *
 * This file preserves the GNU Lesser General Public License version 2.1
 * licensing terms of the original source files.
 */

#nullable enable

namespace TKFaxEngine.Modem.V17;

/// <summary>
/// Floating-point complex constellation coordinate used by the V.17 and
/// V.32bis transmitters.
/// </summary>
public readonly record struct V17V32BisTxComplex(
    float Real,
    float Imaginary) {
    public float Re => Real;

    public float Im => Imaginary;
}

/// <summary>
/// V.17/V.32bis transmit constellation maps and root-raised-cosine
/// pulse-shaping coefficients.
/// </summary>
public static class V17V32BisTxTables {
    public const int PulseShaperCoefficientSets = 10;

    public const int PulseShaperTaps = 9;

    /// <summary>Gain used by the original floating-point build.</summary>
    public const float PulseShaperGain = 1.0f;

    /// <summary>Gain used by the original fixed-point build.</summary>
    public const float FixedPointPulseShaperGain = 0.948561f;

    /// <summary>Scale used by TX_PULSESHAPER_SCALE in fixed-point builds.</summary>
    public const double FixedPointPulseShaperScale = 31081.491463;

    /// <summary>14,400 bit/s constellation (128 points).</summary>
    public static readonly V17V32BisTxComplex[] Constellation14400 =
    {
        new V17V32BisTxComplex(
            -8.0f,
            -3.0f), // 0x00
        new V17V32BisTxComplex(
            9.0f,
            2.0f), // 0x01
        new V17V32BisTxComplex(
            2.0f,
            -9.0f), // 0x02
        new V17V32BisTxComplex(
            -3.0f,
            8.0f), // 0x03
        new V17V32BisTxComplex(
            8.0f,
            3.0f), // 0x04
        new V17V32BisTxComplex(
            -9.0f,
            -2.0f), // 0x05
        new V17V32BisTxComplex(
            -2.0f,
            9.0f), // 0x06
        new V17V32BisTxComplex(
            3.0f,
            -8.0f), // 0x07
        new V17V32BisTxComplex(
            -8.0f,
            1.0f), // 0x08
        new V17V32BisTxComplex(
            9.0f,
            -2.0f), // 0x09
        new V17V32BisTxComplex(
            -2.0f,
            -9.0f), // 0x0A
        new V17V32BisTxComplex(
            1.0f,
            8.0f), // 0x0B
        new V17V32BisTxComplex(
            8.0f,
            -1.0f), // 0x0C
        new V17V32BisTxComplex(
            -9.0f,
            2.0f), // 0x0D
        new V17V32BisTxComplex(
            2.0f,
            9.0f), // 0x0E
        new V17V32BisTxComplex(
            -1.0f,
            -8.0f), // 0x0F
        new V17V32BisTxComplex(
            -4.0f,
            -3.0f), // 0x10
        new V17V32BisTxComplex(
            5.0f,
            2.0f), // 0x11
        new V17V32BisTxComplex(
            2.0f,
            -5.0f), // 0x12
        new V17V32BisTxComplex(
            -3.0f,
            4.0f), // 0x13
        new V17V32BisTxComplex(
            4.0f,
            3.0f), // 0x14
        new V17V32BisTxComplex(
            -5.0f,
            -2.0f), // 0x15
        new V17V32BisTxComplex(
            -2.0f,
            5.0f), // 0x16
        new V17V32BisTxComplex(
            3.0f,
            -4.0f), // 0x17
        new V17V32BisTxComplex(
            -4.0f,
            1.0f), // 0x18
        new V17V32BisTxComplex(
            5.0f,
            -2.0f), // 0x19
        new V17V32BisTxComplex(
            -2.0f,
            -5.0f), // 0x1A
        new V17V32BisTxComplex(
            1.0f,
            4.0f), // 0x1B
        new V17V32BisTxComplex(
            4.0f,
            -1.0f), // 0x1C
        new V17V32BisTxComplex(
            -5.0f,
            2.0f), // 0x1D
        new V17V32BisTxComplex(
            2.0f,
            5.0f), // 0x1E
        new V17V32BisTxComplex(
            -1.0f,
            -4.0f), // 0x1F
        new V17V32BisTxComplex(
            4.0f,
            -3.0f), // 0x20
        new V17V32BisTxComplex(
            -3.0f,
            2.0f), // 0x21
        new V17V32BisTxComplex(
            2.0f,
            3.0f), // 0x22
        new V17V32BisTxComplex(
            -3.0f,
            -4.0f), // 0x23
        new V17V32BisTxComplex(
            -4.0f,
            3.0f), // 0x24
        new V17V32BisTxComplex(
            3.0f,
            -2.0f), // 0x25
        new V17V32BisTxComplex(
            -2.0f,
            -3.0f), // 0x26
        new V17V32BisTxComplex(
            3.0f,
            4.0f), // 0x27
        new V17V32BisTxComplex(
            4.0f,
            1.0f), // 0x28
        new V17V32BisTxComplex(
            -3.0f,
            -2.0f), // 0x29
        new V17V32BisTxComplex(
            -2.0f,
            3.0f), // 0x2A
        new V17V32BisTxComplex(
            1.0f,
            -4.0f), // 0x2B
        new V17V32BisTxComplex(
            -4.0f,
            -1.0f), // 0x2C
        new V17V32BisTxComplex(
            3.0f,
            2.0f), // 0x2D
        new V17V32BisTxComplex(
            2.0f,
            -3.0f), // 0x2E
        new V17V32BisTxComplex(
            -1.0f,
            4.0f), // 0x2F
        new V17V32BisTxComplex(
            0.0f,
            -3.0f), // 0x30
        new V17V32BisTxComplex(
            1.0f,
            2.0f), // 0x31
        new V17V32BisTxComplex(
            2.0f,
            -1.0f), // 0x32
        new V17V32BisTxComplex(
            -3.0f,
            0.0f), // 0x33
        new V17V32BisTxComplex(
            0.0f,
            3.0f), // 0x34
        new V17V32BisTxComplex(
            -1.0f,
            -2.0f), // 0x35
        new V17V32BisTxComplex(
            -2.0f,
            1.0f), // 0x36
        new V17V32BisTxComplex(
            3.0f,
            0.0f), // 0x37
        new V17V32BisTxComplex(
            0.0f,
            1.0f), // 0x38
        new V17V32BisTxComplex(
            1.0f,
            -2.0f), // 0x39
        new V17V32BisTxComplex(
            -2.0f,
            -1.0f), // 0x3A
        new V17V32BisTxComplex(
            1.0f,
            0.0f), // 0x3B
        new V17V32BisTxComplex(
            0.0f,
            -1.0f), // 0x3C
        new V17V32BisTxComplex(
            -1.0f,
            2.0f), // 0x3D
        new V17V32BisTxComplex(
            2.0f,
            1.0f), // 0x3E
        new V17V32BisTxComplex(
            -1.0f,
            0.0f), // 0x3F
        new V17V32BisTxComplex(
            8.0f,
            -3.0f), // 0x40
        new V17V32BisTxComplex(
            -7.0f,
            2.0f), // 0x41
        new V17V32BisTxComplex(
            2.0f,
            7.0f), // 0x42
        new V17V32BisTxComplex(
            -3.0f,
            -8.0f), // 0x43
        new V17V32BisTxComplex(
            -8.0f,
            3.0f), // 0x44
        new V17V32BisTxComplex(
            7.0f,
            -2.0f), // 0x45
        new V17V32BisTxComplex(
            -2.0f,
            -7.0f), // 0x46
        new V17V32BisTxComplex(
            3.0f,
            8.0f), // 0x47
        new V17V32BisTxComplex(
            8.0f,
            1.0f), // 0x48
        new V17V32BisTxComplex(
            -7.0f,
            -2.0f), // 0x49
        new V17V32BisTxComplex(
            -2.0f,
            7.0f), // 0x4A
        new V17V32BisTxComplex(
            1.0f,
            -8.0f), // 0x4B
        new V17V32BisTxComplex(
            -8.0f,
            -1.0f), // 0x4C
        new V17V32BisTxComplex(
            7.0f,
            2.0f), // 0x4D
        new V17V32BisTxComplex(
            2.0f,
            -7.0f), // 0x4E
        new V17V32BisTxComplex(
            -1.0f,
            8.0f), // 0x4F
        new V17V32BisTxComplex(
            -4.0f,
            -7.0f), // 0x50
        new V17V32BisTxComplex(
            5.0f,
            6.0f), // 0x51
        new V17V32BisTxComplex(
            6.0f,
            -5.0f), // 0x52
        new V17V32BisTxComplex(
            -7.0f,
            4.0f), // 0x53
        new V17V32BisTxComplex(
            4.0f,
            7.0f), // 0x54
        new V17V32BisTxComplex(
            -5.0f,
            -6.0f), // 0x55
        new V17V32BisTxComplex(
            -6.0f,
            5.0f), // 0x56
        new V17V32BisTxComplex(
            7.0f,
            -4.0f), // 0x57
        new V17V32BisTxComplex(
            -4.0f,
            5.0f), // 0x58
        new V17V32BisTxComplex(
            5.0f,
            -6.0f), // 0x59
        new V17V32BisTxComplex(
            -6.0f,
            -5.0f), // 0x5A
        new V17V32BisTxComplex(
            5.0f,
            4.0f), // 0x5B
        new V17V32BisTxComplex(
            4.0f,
            -5.0f), // 0x5C
        new V17V32BisTxComplex(
            -5.0f,
            6.0f), // 0x5D
        new V17V32BisTxComplex(
            6.0f,
            5.0f), // 0x5E
        new V17V32BisTxComplex(
            -5.0f,
            -4.0f), // 0x5F
        new V17V32BisTxComplex(
            4.0f,
            -7.0f), // 0x60
        new V17V32BisTxComplex(
            -3.0f,
            6.0f), // 0x61
        new V17V32BisTxComplex(
            6.0f,
            3.0f), // 0x62
        new V17V32BisTxComplex(
            -7.0f,
            -4.0f), // 0x63
        new V17V32BisTxComplex(
            -4.0f,
            7.0f), // 0x64
        new V17V32BisTxComplex(
            3.0f,
            -6.0f), // 0x65
        new V17V32BisTxComplex(
            -6.0f,
            -3.0f), // 0x66
        new V17V32BisTxComplex(
            7.0f,
            4.0f), // 0x67
        new V17V32BisTxComplex(
            4.0f,
            5.0f), // 0x68
        new V17V32BisTxComplex(
            -3.0f,
            -6.0f), // 0x69
        new V17V32BisTxComplex(
            -6.0f,
            3.0f), // 0x6A
        new V17V32BisTxComplex(
            5.0f,
            -4.0f), // 0x6B
        new V17V32BisTxComplex(
            -4.0f,
            -5.0f), // 0x6C
        new V17V32BisTxComplex(
            3.0f,
            6.0f), // 0x6D
        new V17V32BisTxComplex(
            6.0f,
            -3.0f), // 0x6E
        new V17V32BisTxComplex(
            -5.0f,
            4.0f), // 0x6F
        new V17V32BisTxComplex(
            0.0f,
            -7.0f), // 0x70
        new V17V32BisTxComplex(
            1.0f,
            6.0f), // 0x71
        new V17V32BisTxComplex(
            6.0f,
            -1.0f), // 0x72
        new V17V32BisTxComplex(
            -7.0f,
            0.0f), // 0x73
        new V17V32BisTxComplex(
            0.0f,
            7.0f), // 0x74
        new V17V32BisTxComplex(
            -1.0f,
            -6.0f), // 0x75
        new V17V32BisTxComplex(
            -6.0f,
            1.0f), // 0x76
        new V17V32BisTxComplex(
            7.0f,
            0.0f), // 0x77
        new V17V32BisTxComplex(
            0.0f,
            5.0f), // 0x78
        new V17V32BisTxComplex(
            1.0f,
            -6.0f), // 0x79
        new V17V32BisTxComplex(
            -6.0f,
            -1.0f), // 0x7A
        new V17V32BisTxComplex(
            5.0f,
            0.0f), // 0x7B
        new V17V32BisTxComplex(
            0.0f,
            -5.0f), // 0x7C
        new V17V32BisTxComplex(
            -1.0f,
            6.0f), // 0x7D
        new V17V32BisTxComplex(
            6.0f,
            1.0f), // 0x7E
        new V17V32BisTxComplex(
            -5.0f,
            0.0f), // 0x7F
    };

    /// <summary>12,000 bit/s constellation (64 points).</summary>
    public static readonly V17V32BisTxComplex[] Constellation12000 =
    {
        new V17V32BisTxComplex(
            7.0f,
            1.0f), // 0x00
        new V17V32BisTxComplex(
            -5.0f,
            -1.0f), // 0x01
        new V17V32BisTxComplex(
            -1.0f,
            5.0f), // 0x02
        new V17V32BisTxComplex(
            1.0f,
            -7.0f), // 0x03
        new V17V32BisTxComplex(
            -7.0f,
            -1.0f), // 0x04
        new V17V32BisTxComplex(
            5.0f,
            1.0f), // 0x05
        new V17V32BisTxComplex(
            1.0f,
            -5.0f), // 0x06
        new V17V32BisTxComplex(
            -1.0f,
            7.0f), // 0x07
        new V17V32BisTxComplex(
            3.0f,
            -3.0f), // 0x08
        new V17V32BisTxComplex(
            -1.0f,
            3.0f), // 0x09
        new V17V32BisTxComplex(
            3.0f,
            1.0f), // 0x0A
        new V17V32BisTxComplex(
            -3.0f,
            -3.0f), // 0x0B
        new V17V32BisTxComplex(
            -3.0f,
            3.0f), // 0x0C
        new V17V32BisTxComplex(
            1.0f,
            -3.0f), // 0x0D
        new V17V32BisTxComplex(
            -3.0f,
            -1.0f), // 0x0E
        new V17V32BisTxComplex(
            3.0f,
            3.0f), // 0x0F
        new V17V32BisTxComplex(
            7.0f,
            -7.0f), // 0x10
        new V17V32BisTxComplex(
            -5.0f,
            7.0f), // 0x11
        new V17V32BisTxComplex(
            7.0f,
            5.0f), // 0x12
        new V17V32BisTxComplex(
            -7.0f,
            -7.0f), // 0x13
        new V17V32BisTxComplex(
            -7.0f,
            7.0f), // 0x14
        new V17V32BisTxComplex(
            5.0f,
            -7.0f), // 0x15
        new V17V32BisTxComplex(
            -7.0f,
            -5.0f), // 0x16
        new V17V32BisTxComplex(
            7.0f,
            7.0f), // 0x17
        new V17V32BisTxComplex(
            -1.0f,
            -7.0f), // 0x18
        new V17V32BisTxComplex(
            3.0f,
            7.0f), // 0x19
        new V17V32BisTxComplex(
            7.0f,
            -3.0f), // 0x1A
        new V17V32BisTxComplex(
            -7.0f,
            1.0f), // 0x1B
        new V17V32BisTxComplex(
            1.0f,
            7.0f), // 0x1C
        new V17V32BisTxComplex(
            -3.0f,
            -7.0f), // 0x1D
        new V17V32BisTxComplex(
            -7.0f,
            3.0f), // 0x1E
        new V17V32BisTxComplex(
            7.0f,
            -1.0f), // 0x1F
        new V17V32BisTxComplex(
            3.0f,
            5.0f), // 0x20
        new V17V32BisTxComplex(
            -1.0f,
            -5.0f), // 0x21
        new V17V32BisTxComplex(
            -5.0f,
            1.0f), // 0x22
        new V17V32BisTxComplex(
            5.0f,
            -3.0f), // 0x23
        new V17V32BisTxComplex(
            -3.0f,
            -5.0f), // 0x24
        new V17V32BisTxComplex(
            1.0f,
            5.0f), // 0x25
        new V17V32BisTxComplex(
            5.0f,
            -1.0f), // 0x26
        new V17V32BisTxComplex(
            -5.0f,
            3.0f), // 0x27
        new V17V32BisTxComplex(
            -1.0f,
            1.0f), // 0x28
        new V17V32BisTxComplex(
            3.0f,
            -1.0f), // 0x29
        new V17V32BisTxComplex(
            -1.0f,
            -3.0f), // 0x2A
        new V17V32BisTxComplex(
            1.0f,
            1.0f), // 0x2B
        new V17V32BisTxComplex(
            1.0f,
            -1.0f), // 0x2C
        new V17V32BisTxComplex(
            -3.0f,
            1.0f), // 0x2D
        new V17V32BisTxComplex(
            1.0f,
            3.0f), // 0x2E
        new V17V32BisTxComplex(
            -1.0f,
            -1.0f), // 0x2F
        new V17V32BisTxComplex(
            -5.0f,
            5.0f), // 0x30
        new V17V32BisTxComplex(
            7.0f,
            -5.0f), // 0x31
        new V17V32BisTxComplex(
            -5.0f,
            -7.0f), // 0x32
        new V17V32BisTxComplex(
            5.0f,
            5.0f), // 0x33
        new V17V32BisTxComplex(
            5.0f,
            -5.0f), // 0x34
        new V17V32BisTxComplex(
            -7.0f,
            5.0f), // 0x35
        new V17V32BisTxComplex(
            5.0f,
            7.0f), // 0x36
        new V17V32BisTxComplex(
            -5.0f,
            -5.0f), // 0x37
        new V17V32BisTxComplex(
            -5.0f,
            -3.0f), // 0x38
        new V17V32BisTxComplex(
            7.0f,
            3.0f), // 0x39
        new V17V32BisTxComplex(
            3.0f,
            -7.0f), // 0x3A
        new V17V32BisTxComplex(
            -3.0f,
            5.0f), // 0x3B
        new V17V32BisTxComplex(
            5.0f,
            3.0f), // 0x3C
        new V17V32BisTxComplex(
            -7.0f,
            -3.0f), // 0x3D
        new V17V32BisTxComplex(
            -3.0f,
            7.0f), // 0x3E
        new V17V32BisTxComplex(
            3.0f,
            -5.0f), // 0x3F
    };

    /// <summary>9,600 bit/s constellation (32 points).</summary>
    public static readonly V17V32BisTxComplex[] Constellation9600 =
    {
        new V17V32BisTxComplex(
            -8.0f,
            2.0f), // 0x00
        new V17V32BisTxComplex(
            -6.0f,
            -4.0f), // 0x01
        new V17V32BisTxComplex(
            -4.0f,
            6.0f), // 0x02
        new V17V32BisTxComplex(
            2.0f,
            8.0f), // 0x03
        new V17V32BisTxComplex(
            8.0f,
            -2.0f), // 0x04
        new V17V32BisTxComplex(
            6.0f,
            4.0f), // 0x05
        new V17V32BisTxComplex(
            4.0f,
            -6.0f), // 0x06
        new V17V32BisTxComplex(
            -2.0f,
            -8.0f), // 0x07
        new V17V32BisTxComplex(
            0.0f,
            2.0f), // 0x08
        new V17V32BisTxComplex(
            -6.0f,
            4.0f), // 0x09
        new V17V32BisTxComplex(
            4.0f,
            6.0f), // 0x0A
        new V17V32BisTxComplex(
            2.0f,
            0.0f), // 0x0B
        new V17V32BisTxComplex(
            0.0f,
            -2.0f), // 0x0C
        new V17V32BisTxComplex(
            6.0f,
            -4.0f), // 0x0D
        new V17V32BisTxComplex(
            -4.0f,
            -6.0f), // 0x0E
        new V17V32BisTxComplex(
            -2.0f,
            0.0f), // 0x0F
        new V17V32BisTxComplex(
            0.0f,
            -6.0f), // 0x10
        new V17V32BisTxComplex(
            2.0f,
            -4.0f), // 0x11
        new V17V32BisTxComplex(
            -4.0f,
            -2.0f), // 0x12
        new V17V32BisTxComplex(
            -6.0f,
            0.0f), // 0x13
        new V17V32BisTxComplex(
            0.0f,
            6.0f), // 0x14
        new V17V32BisTxComplex(
            -2.0f,
            4.0f), // 0x15
        new V17V32BisTxComplex(
            4.0f,
            2.0f), // 0x16
        new V17V32BisTxComplex(
            6.0f,
            0.0f), // 0x17
        new V17V32BisTxComplex(
            8.0f,
            2.0f), // 0x18
        new V17V32BisTxComplex(
            2.0f,
            4.0f), // 0x19
        new V17V32BisTxComplex(
            4.0f,
            -2.0f), // 0x1A
        new V17V32BisTxComplex(
            2.0f,
            -8.0f), // 0x1B
        new V17V32BisTxComplex(
            -8.0f,
            -2.0f), // 0x1C
        new V17V32BisTxComplex(
            -2.0f,
            -4.0f), // 0x1D
        new V17V32BisTxComplex(
            -4.0f,
            2.0f), // 0x1E
        new V17V32BisTxComplex(
            -2.0f,
            8.0f), // 0x1F
    };

    /// <summary>7,200 bit/s constellation (16 points).</summary>
    public static readonly V17V32BisTxComplex[] Constellation7200 =
    {
        new V17V32BisTxComplex(
            6.0f,
            -6.0f), // 0x00
        new V17V32BisTxComplex(
            -2.0f,
            6.0f), // 0x01
        new V17V32BisTxComplex(
            6.0f,
            2.0f), // 0x02
        new V17V32BisTxComplex(
            -6.0f,
            -6.0f), // 0x03
        new V17V32BisTxComplex(
            -6.0f,
            6.0f), // 0x04
        new V17V32BisTxComplex(
            2.0f,
            -6.0f), // 0x05
        new V17V32BisTxComplex(
            -6.0f,
            -2.0f), // 0x06
        new V17V32BisTxComplex(
            6.0f,
            6.0f), // 0x07
        new V17V32BisTxComplex(
            -2.0f,
            2.0f), // 0x08
        new V17V32BisTxComplex(
            6.0f,
            -2.0f), // 0x09
        new V17V32BisTxComplex(
            -2.0f,
            -6.0f), // 0x0A
        new V17V32BisTxComplex(
            2.0f,
            2.0f), // 0x0B
        new V17V32BisTxComplex(
            2.0f,
            -2.0f), // 0x0C
        new V17V32BisTxComplex(
            -6.0f,
            2.0f), // 0x0D
        new V17V32BisTxComplex(
            2.0f,
            6.0f), // 0x0E
        new V17V32BisTxComplex(
            -2.0f,
            -2.0f), // 0x0F
    };

    /// <summary>4,800 bit/s V.32bis/training constellation (4 points).</summary>
    public static readonly V17V32BisTxComplex[] Constellation4800 =
    {
        new V17V32BisTxComplex(
            -6.0f,
            -2.0f), // 0x00
        new V17V32BisTxComplex(
            -2.0f,
            6.0f), // 0x01
        new V17V32BisTxComplex(
            2.0f,
            -6.0f), // 0x02
        new V17V32BisTxComplex(
            6.0f,
            2.0f), // 0x03
    };

    /// <summary>A/B/C/D training constellation (4 points).</summary>
    public static readonly V17V32BisTxComplex[] AbcdConstellation =
    {
        new V17V32BisTxComplex(
            -6.0f,
            -2.0f), // A
        new V17V32BisTxComplex(
            2.0f,
            -6.0f), // B
        new V17V32BisTxComplex(
            6.0f,
            2.0f), // C
        new V17V32BisTxComplex(
            -2.0f,
            6.0f), // D
    };

    /// <summary>Ten polyphase RRC filters with nine taps each.</summary>
    public static readonly float[][] TxPulseShaper =
    {
        new float[] // Filter 0
        {
            -0.0028949626f,
            -0.0180558777f,
            0.0644370035f,
            -0.1680546392f,
            0.6136030985f,
            0.6136030984f,
            -0.1680546392f,
            0.0644370034f,
            -0.0180558778f,
        },
        new float[] // Filter 1
        {
            0.0031457248f,
            -0.0296755147f,
            0.0821538018f,
            -0.1948071696f,
            0.7563219631f,
            0.4608861941f,
            -0.1273859915f,
            0.0418434579f,
            -0.0059021774f,
        },
        new float[] // Filter 2
        {
            0.0095859909f,
            -0.0389394472f,
            0.0918555210f,
            -0.2016880234f,
            0.8793516917f,
            0.3081345068f,
            -0.0792085179f,
            0.0176601554f,
            0.0051283325f,
        },
        new float[] // Filter 3
        {
            0.0153896883f,
            -0.0441001646f,
            0.0909724653f,
            -0.1838386340f,
            0.9741012686f,
            0.1647552955f,
            -0.0297442724f,
            -0.0050682341f,
            0.0137350940f,
        },
        new float[] // Filter 4
        {
            0.0194884088f,
            -0.0437412561f,
            0.0779044330f,
            -0.1380831560f,
            1.0338274098f,
            0.0388498604f,
            0.0155354801f,
            -0.0238603979f,
            0.0191007894f,
        },
        new float[] // Filter 5
        {
            0.0209425252f,
            -0.0370198693f,
            0.0523524602f,
            -0.0633894605f,
            1.0542286891f,
            -0.0633894606f,
            0.0523524602f,
            -0.0370198693f,
            0.0209425251f,
        },
        new float[] // Filter 6
        {
            0.0191007894f,
            -0.0238603978f,
            0.0155354801f,
            0.0388498605f,
            1.0338274098f,
            -0.1380831561f,
            0.0779044330f,
            -0.0437412561f,
            0.0194884087f,
        },
        new float[] // Filter 7
        {
            0.0137350940f,
            -0.0050682341f,
            -0.0297442724f,
            0.1647552955f,
            0.9741012686f,
            -0.1838386340f,
            0.0909724652f,
            -0.0441001646f,
            0.0153896883f,
        },
        new float[] // Filter 8
        {
            0.0051283326f,
            0.0176601554f,
            -0.0792085179f,
            0.3081345069f,
            0.8793516917f,
            -0.2016880235f,
            0.0918555209f,
            -0.0389394473f,
            0.0095859909f,
        },
        new float[] // Filter 9
        {
            -0.0059021774f,
            0.0418434580f,
            -0.1273859915f,
            0.4608861942f,
            0.7563219631f,
            -0.1948071696f,
            0.0821538018f,
            -0.0296755147f,
            0.0031457248f,
        },
    };

    /// <summary>
    /// Returns the constellation selected by the native V.17/V.32bis
    /// transmitter for the supplied bit rate.
    /// </summary>
    public static ReadOnlySpan<V17V32BisTxComplex> GetConstellation(
        int bitRate) {
        return bitRate switch {
            14400 => Constellation14400,
            12000 => Constellation12000,
            9600 => Constellation9600,
            7200 => Constellation7200,
            4800 => Constellation4800,
            _ => throw new ArgumentOutOfRangeException(
                nameof(bitRate),
                bitRate,
                "Valid bit rates are 4800, 7200, 9600, 12000 and 14400 bit/s."),
        };
    }

    /// <summary>Returns the uncoded input bits per transmitted symbol.</summary>
    public static int GetBitsPerSymbol(
        int bitRate) {
        return bitRate switch {
            14400 => 6,
            12000 => 5,
            9600 => 4,
            7200 => 3,
            4800 => 2,
            _ => throw new ArgumentOutOfRangeException(
                nameof(bitRate),
                bitRate,
                "Valid bit rates are 4800, 7200, 9600, 12000 and 14400 bit/s."),
        };
    }

    /// <summary>Returns one of the ten nine-tap pulse-shaping filters.</summary>
    public static ReadOnlySpan<float> GetPulseShaper(
        int phase) {
        if ((uint)phase >= PulseShaperCoefficientSets)
            throw new ArgumentOutOfRangeException(nameof(phase));

        return TxPulseShaper[phase];
    }

    /// <summary>
    /// Converts a floating-point RRC coefficient with the exact rounding
    /// expression used by TX_PULSESHAPER_SCALE.
    /// </summary>
    public static short ScalePulseShaperCoefficient(
        float coefficient) {
        double scaled =
            FixedPointPulseShaperScale * coefficient +
            (coefficient >= 0.0f ? 0.5 : -0.5);

        return checked((short)scaled);
    }

    /// <summary>Creates the fixed-point form of one pulse-shaping filter.</summary>
    public static short[] GetFixedPointPulseShaper(
        int phase) {
        ReadOnlySpan<float> source = GetPulseShaper(phase);
        short[] result = new short[source.Length];

        for (int index = 0; index < source.Length; index++)
            result[index] = ScalePulseShaperCoefficient(source[index]);

        return result;
    }

    // Native-name aliases retained for straightforward source migration.
    public static ReadOnlySpan<V17V32BisTxComplex>
        v17_v32bis_14400_constellation => Constellation14400;

    public static ReadOnlySpan<V17V32BisTxComplex>
        v17_v32bis_12000_constellation => Constellation12000;

    public static ReadOnlySpan<V17V32BisTxComplex>
        v17_v32bis_9600_constellation => Constellation9600;

    public static ReadOnlySpan<V17V32BisTxComplex>
        v17_v32bis_7200_constellation => Constellation7200;

    public static ReadOnlySpan<V17V32BisTxComplex>
        v17_v32bis_4800_constellation => Constellation4800;

    public static ReadOnlySpan<V17V32BisTxComplex>
        v17_v32bis_abcd_constellation => AbcdConstellation;

    public static float[][] tx_pulseshaper => TxPulseShaper;
}
