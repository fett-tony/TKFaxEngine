/*
 * TKFaxEngine - managed C# table port
 *
 * V27TerTxRrc.cs
 *
 * Converted from the automatically generated v27ter_tx_2400_rrc.h
 * and v27ter_tx_4800_rrc.h files. The polyphase transmit
 * pulse-shaping filters and their nine taps are preserved with
 * the original decimal precision.
 */

#nullable enable

namespace TKFaxEngine.Modem.V27;

/// <summary>
/// V.27ter transmit root-raised-cosine pulse-shaping coefficients.
/// </summary>
public static class V27TerTxRrc {
    /// <summary>Number of taps in each pulse-shaping filter.</summary>
    public const int TapsPerFilter = 9;

    /// <summary>Number of 2400 bit/s polyphase coefficient sets.</summary>
    public const int CoefficientSets2400 = 20;

    /// <summary>Gain used by the 2400 bit/s floating-point implementation.</summary>
    public const float FloatingPointGain2400 = 1.000000f;

    /// <summary>Gain used by the 2400 bit/s native fixed-point implementation.</summary>
    public const float FixedPointGain2400 = 0.875533f;

    /// <summary>Scale used by the 2400 bit/s native fixed-point coefficient conversion.</summary>
    public const double FixedPointScale2400 = 28688.605380;

    /// <summary>
    /// Twenty 2400 bit/s polyphase filters with nine floating-point coefficients each.
    /// </summary>
    public static readonly float[][] TxPulseShaper2400 =
    {
        new float[] // Filter 0
        {
            0.0050262000f,
            0.0107704139f,
            -0.0150784957f,
            -0.0753922186f,
            0.5814534468f,
            0.5814534467f,
            -0.0753922186f,
            -0.0150784958f,
            0.0107704138f,
        },
        new float[] // Filter 1
        {
            0.0036769615f,
            0.0132151788f,
            -0.0108416505f,
            -0.0962477546f,
            0.6703977440f,
            0.4915574819f,
            -0.0543875540f,
            -0.0179957590f,
            0.0079493141f,
        },
        new float[] // Filter 2
        {
            0.0020271558f,
            0.0151310510f,
            -0.0054150757f,
            -0.1159725361f,
            0.7564987991f,
            0.4025543098f,
            -0.0341116997f,
            -0.0195425378f,
            0.0049156947f,
        },
        new float[] // Filter 3
        {
            0.0001575810f,
            0.0163856892f,
            0.0009922305f,
            -0.1335090670f,
            0.8378713095f,
            0.3161906111f,
            -0.0153166439f,
            -0.0197430347f,
            0.0018355829f,
        },
        new float[] // Filter 4
        {
            -0.0018345654f,
            0.0168753676f,
            0.0080958440f,
            -0.1477565768f,
            0.9126905920f,
            0.2340689766f,
            0.0013877594f,
            -0.0186894802f,
            -0.0011314547f,
        },
        new float[] // Filter 5
        {
            -0.0038402663f,
            0.0165323368f,
            0.0155436576f,
            -0.1576073958f,
            0.9792460719f,
            0.1576074027f,
            0.0155436234f,
            -0.0165323579f,
            -0.0038401980f,
        },
        new float[] // Filter 6
        {
            -0.0057441249f,
            0.0153307048f,
            0.0229275670f,
            -0.1619859170f,
            1.0359921022f,
            0.0880058111f,
            0.0268485018f,
            -0.0134685577f,
            -0.0061665144f,
        },
        new float[] // Filter 7
        {
            -0.0074304100f,
            0.0132904398f,
            0.0297988399f,
            -0.1598887983f,
            1.0815943709f,
            0.0262205341f,
            0.0351527390f,
            -0.0097281388f,
            -0.0080126759f,
        },
        new float[] // Filter 8
        {
            -0.0087894106f,
            0.0104791762f,
            0.0356867213f,
            -0.1504249558f,
            1.1149702967f,
            -0.0270525930f,
            0.0404511628f,
            -0.0055604096f,
            -0.0093110523f,
        },
        new float[] // Filter 9
        {
            -0.0097237709f,
            0.0070115966f,
            0.0401196552f,
            -0.1328538467f,
            1.1353220123f,
            -0.0713862188f,
            0.0428697867f,
            -0.0012200205f,
            -0.0100260766f,
        },
        new float[] // Filter 10
        {
            -0.0101544658f,
            0.0030462740f,
            0.0426483506f,
            -0.1066205506f,
            1.1421607836f,
            -0.1066205506f,
            0.0426483506f,
            0.0030462740f,
            -0.0101544658f,
        },
        new float[] // Filter 11
        {
            -0.0100260766f,
            -0.0012200205f,
            0.0428697867f,
            -0.0713862187f,
            1.1353220123f,
            -0.1328538468f,
            0.0401196552f,
            0.0070115966f,
            -0.0097237709f,
        },
        new float[] // Filter 12
        {
            -0.0093110523f,
            -0.0055604096f,
            0.0404511629f,
            -0.0270525929f,
            1.1149702967f,
            -0.1504249558f,
            0.0356867212f,
            0.0104791761f,
            -0.0087894106f,
        },
        new float[] // Filter 13
        {
            -0.0080126759f,
            -0.0097281388f,
            0.0351527391f,
            0.0262205342f,
            1.0815943708f,
            -0.1598887984f,
            0.0297988399f,
            0.0132904397f,
            -0.0074304100f,
        },
        new float[] // Filter 14
        {
            -0.0061665144f,
            -0.0134685577f,
            0.0268485019f,
            0.0880058111f,
            1.0359921022f,
            -0.1619859171f,
            0.0229275670f,
            0.0153307048f,
            -0.0057441249f,
        },
        new float[] // Filter 15
        {
            -0.0038401980f,
            -0.0165323579f,
            0.0155436234f,
            0.1576074029f,
            0.9792460718f,
            -0.1576073958f,
            0.0155436575f,
            0.0165323368f,
            -0.0038402663f,
        },
        new float[] // Filter 16
        {
            -0.0011314547f,
            -0.0186894801f,
            0.0013877595f,
            0.2340689767f,
            0.9126905919f,
            -0.1477565768f,
            0.0080958439f,
            0.0168753675f,
            -0.0018345654f,
        },
        new float[] // Filter 17
        {
            0.0018355830f,
            -0.0197430346f,
            -0.0153166438f,
            0.3161906112f,
            0.8378713094f,
            -0.1335090671f,
            0.0009922304f,
            0.0163856892f,
            0.0001575810f,
        },
        new float[] // Filter 18
        {
            0.0049156947f,
            -0.0195425377f,
            -0.0341116997f,
            0.4025543099f,
            0.7564987990f,
            -0.1159725361f,
            -0.0054150757f,
            0.0151310509f,
            0.0020271558f,
        },
        new float[] // Filter 19
        {
            0.0079493141f,
            -0.0179957590f,
            -0.0543875540f,
            0.4915574821f,
            0.6703977439f,
            -0.0962477546f,
            -0.0108416506f,
            0.0132151788f,
            0.0036769615f,
        },
    };

    /// <summary>Number of 4800 bit/s polyphase coefficient sets.</summary>
    public const int CoefficientSets4800 = 5;

    /// <summary>Gain used by the 4800 bit/s floating-point implementation.</summary>
    public const float FloatingPointGain4800 = 1.000000f;

    /// <summary>Gain used by the 4800 bit/s native fixed-point implementation.</summary>
    public const float FixedPointGain4800 = 0.875534f;

    /// <summary>Scale used by the 4800 bit/s native fixed-point coefficient conversion.</summary>
    public const double FixedPointScale4800 = 28688.606885;

    /// <summary>
    /// Five 4800 bit/s polyphase filters with nine floating-point coefficients each.
    /// </summary>
    public static readonly float[][] TxPulseShaper4800 =
    {
        new float[] // Filter 0
        {
            0.0020271593f,
            0.0151309274f,
            -0.0054150609f,
            -0.1159724027f,
            0.7564986489f,
            0.4025541374f,
            -0.0341116447f,
            -0.0195424311f,
            0.0049156263f,
        },
        new float[] // Filter 1
        {
            -0.0057440218f,
            0.0153306251f,
            0.0229274764f,
            -0.1619858035f,
            1.0359920119f,
            0.0880056982f,
            0.0268484410f,
            -0.0134684453f,
            -0.0061664720f,
        },
        new float[] // Filter 2
        {
            -0.0101543453f,
            0.0030463017f,
            0.0426482251f,
            -0.1066205433f,
            1.1421607236f,
            -0.1066205433f,
            0.0426482251f,
            0.0030463016f,
            -0.0101543453f,
        },
        new float[] // Filter 3
        {
            -0.0061664720f,
            -0.0134684453f,
            0.0268484411f,
            0.0880056982f,
            1.0359920119f,
            -0.1619858035f,
            0.0229274764f,
            0.0153306251f,
            -0.0057440218f,
        },
        new float[] // Filter 4
        {
            0.0049156264f,
            -0.0195424310f,
            -0.0341116447f,
            0.4025541375f,
            0.7564986489f,
            -0.1159724028f,
            -0.0054150609f,
            0.0151309274f,
            0.0020271593f,
        },
    };

    /// <summary>
    /// Returns one nine-tap 2400 bit/s filter for the requested polyphase position.
    /// </summary>
    public static ReadOnlySpan<float> GetPulseShaper2400(int phase) {
        if ((uint)phase >= CoefficientSets2400)
            throw new ArgumentOutOfRangeException(nameof(phase));

        return TxPulseShaper2400[phase];
    }

    /// <summary>
    /// Applies the exact rounding expression used by TX_PULSESHAPER_2400_SCALE
    /// in the fixed-point C implementation.
    /// </summary>
    public static short ScaleCoefficient2400(float coefficient) {
        double scaled =
            FixedPointScale2400 * coefficient +
            (coefficient >= 0.0f ? 0.5 : -0.5);

        return checked((short)scaled);
    }

    /// <summary>
    /// Creates the fixed-point representation of one 2400 bit/s filter.
    /// </summary>
    public static short[] GetFixedPointPulseShaper2400(int phase) {
        ReadOnlySpan<float> source = GetPulseShaper2400(phase);
        short[] result = new short[source.Length];

        for (int index = 0; index < source.Length; index++)
            result[index] = ScaleCoefficient2400(source[index]);

        return result;
    }

    /// <summary>
    /// Returns one nine-tap 4800 bit/s filter for the requested polyphase position.
    /// </summary>
    public static ReadOnlySpan<float> GetPulseShaper4800(int phase) {
        if ((uint)phase >= CoefficientSets4800)
            throw new ArgumentOutOfRangeException(nameof(phase));

        return TxPulseShaper4800[phase];
    }

    /// <summary>
    /// Applies the exact rounding expression used by TX_PULSESHAPER_4800_SCALE
    /// in the fixed-point C implementation.
    /// </summary>
    public static short ScaleCoefficient4800(float coefficient) {
        double scaled =
            FixedPointScale4800 * coefficient +
            (coefficient >= 0.0f ? 0.5 : -0.5);

        return checked((short)scaled);
    }

    /// <summary>
    /// Creates the fixed-point representation of one 4800 bit/s filter.
    /// </summary>
    public static short[] GetFixedPointPulseShaper4800(int phase) {
        ReadOnlySpan<float> source = GetPulseShaper4800(phase);
        short[] result = new short[source.Length];

        for (int index = 0; index < source.Length; index++)
            result[index] = ScaleCoefficient4800(source[index]);

        return result;
    }

    // Native-name aliases retained for straightforward source migration.
    public const int TX_PULSESHAPER_2400_COEFF_SETS = CoefficientSets2400;
    public const float TX_PULSESHAPER_2400_GAIN = FloatingPointGain2400;
    public static float[][] tx_pulseshaper_2400 => TxPulseShaper2400;

    public const int TX_PULSESHAPER_4800_COEFF_SETS = CoefficientSets4800;
    public const float TX_PULSESHAPER_4800_GAIN = FloatingPointGain4800;
    public static float[][] tx_pulseshaper_4800 => TxPulseShaper4800;
}
