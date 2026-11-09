/*
 * TKFaxEngine - managed C# table port
 *
 * V22BisTxRrc.cs
 *
 * Converted from the automatically generated v22bis_tx_rrc.h file.
 * The 40 polyphase transmit pulse-shaping filters and their nine taps
 * are preserved with the original decimal precision.
 */

#nullable enable

namespace TKFaxEngine.Modem.V22;

/// <summary>
/// V.22bis transmit root-raised-cosine pulse-shaping coefficients.
/// </summary>
public static class V22BisTxRrc {
    /// <summary>Number of polyphase coefficient sets.</summary>
    public const int CoefficientSets = 40;

    /// <summary>Number of taps in each pulse-shaping filter.</summary>
    public const int TapsPerFilter = 9;

    /// <summary>Gain used by the floating-point implementation.</summary>
    public const float FloatingPointGain = 1.000000f;

    /// <summary>Gain used by the native fixed-point implementation.</summary>
    public const float FixedPointGain = 0.829192f;

    /// <summary>Scale used by the native fixed-point coefficient conversion.</summary>
    public const double FixedPointScale = 27170.133920;

    /// <summary>
    /// Forty polyphase filters with nine floating-point coefficients each.
    /// </summary>
    public static readonly float[][] TxPulseShaper =
    {
        new float[] // Filter 0
        {
            -0.0047225778f,
            -0.0084017803f,
            -0.0087512712f,
            0.0088069184f,
            0.5113443380f,
            0.5113443379f,
            0.0088069183f,
            -0.0087512713f,
            -0.0084017804f,
        },
        new float[] // Filter 1
        {
            -0.0044560618f,
            -0.0089299803f,
            -0.0111430058f,
            0.0023375914f,
            0.5628832678f,
            0.4603563095f,
            0.0144879368f,
            -0.0063308256f,
            -0.0077375837f,
        },
        new float[] // Filter 2
        {
            -0.0040955760f,
            -0.0093085526f,
            -0.0134608698f,
            -0.0048652138f,
            0.6146394096f,
            0.4102392982f,
            0.0193418847f,
            -0.0039255915f,
            -0.0069531334f,
        },
        new float[] // Filter 3
        {
            -0.0036459239f,
            -0.0095262937f,
            -0.0156592365f,
            -0.0127304055f,
            0.6662684760f,
            0.3612970646f,
            0.0233456693f,
            -0.0015775347f,
            -0.0060659402f,
        },
        new float[] // Filter 4
        {
            -0.0031137075f,
            -0.0095747072f,
            -0.0176928207f,
            -0.0211706529f,
            0.7174187175f,
            0.3138144545f,
            0.0264912753f,
            0.0006739941f,
            -0.0050949167f,
        },
        new float[] // Filter 5
        {
            -0.0025072439f,
            -0.0094482419f,
            -0.0195175138f,
            -0.0300826323f,
            0.7677341876f,
            0.2680550875f,
            0.0287849960f,
            0.0027928498f,
            -0.0040599953f,
        },
        new float[] // Filter 6
        {
            -0.0018364497f,
            -0.0091444835f,
            -0.0210912326f,
            -0.0393475015f,
            0.8168580988f,
            0.2242593163f,
            0.0302465047f,
            0.0047466057f,
            -0.0029817394f,
        },
        new float[] // Filter 7
        {
            -0.0011126915f,
            -0.0086642933f,
            -0.0223747670f,
            -0.0488316051f,
            0.8644362339f,
            0.1826424754f,
            0.0309077828f,
            0.0065069844f,
            -0.0018809534f,
        },
        new float[] // Filter 8
        {
            -0.0003486069f,
            -0.0080118919f,
            -0.0233326129f,
            -0.0583874086f,
            0.9101203735f,
            0.1433934355f,
            0.0308119288f,
            0.0080502012f,
            -0.0007782987f,
        },
        new float[] // Filter 9
        {
            0.0004421024f,
            -0.0071948838f,
            -0.0239337749f,
            -0.0678546569f,
            0.9535717010f,
            0.1066734725f,
            0.0300118652f,
            0.0093572183f,
            0.0003060773f,
        },
        new float[] // Filter 10
        {
            0.0012449022f,
            -0.0062242203f,
            -0.0241525253f,
            -0.0770617505f,
            0.9944641461f,
            0.0726154624f,
            0.0285689687f,
            0.0104139084f,
            0.0013528931f,
        },
        new float[] // Filter 11
        {
            0.0020446780f,
            -0.0051141006f,
            -0.0239691028f,
            -0.0858273268f,
            1.0324876292f,
            0.0413234009f,
            0.0265516432f,
            0.0112111267f,
            0.0023440603f,
        },
        new float[] // Filter 12
        {
            0.0028260046f,
            -0.0038818110f,
            -0.0233703397f,
            -0.0939620349f,
            1.0673511678f,
            0.0128722504f,
            0.0240338606f,
            0.0117446955f,
            0.0032629808f,
        },
        new float[] // Filter 13
        {
            0.0035734270f,
            -0.0025475009f,
            -0.0223502003f,
            -0.1012704845f,
            1.0987858104f,
            -0.0126918924f,
            0.0210936884f,
            0.0120153024f,
            0.0040948092f,
        },
        new float[] // Filter 14
        {
            0.0042717488f,
            -0.0011339026f,
            -0.0209102230f,
            -0.1075533516f,
            1.1265473618f,
            -0.0353513151f,
            0.0178118295f,
            0.0120283182f,
            0.0048266775f,
        },
        new float[] // Filter 15
        {
            0.0049063228f,
            0.0003340074f,
            -0.0190598496f,
            -0.1126096167f,
            1.1504188697f,
            -0.0551159095f,
            0.0142701913f,
            0.0117935391f,
            0.0054478776f,
        },
        new float[] // Filter 16
        {
            0.0054633384f,
            0.0018293973f,
            -0.0168166358f,
            -0.1162389117f,
            1.1702128427f,
            -0.0720221048f,
            0.0105505050f,
            0.0113248618f,
            0.0059500010f,
        },
        new float[] // Filter 17
        {
            0.0059301001f,
            0.0033240149f,
            -0.0142063325f,
            -0.1182439493f,
            1.1857731729f,
            -0.0861315367f,
            0.0067330149f,
            0.0106398965f,
            0.0063270333f,
        },
        new float[] // Filter 18
        {
            0.0062952925f,
            0.0047886625f,
            -0.0112628316f,
            -0.1184330050f,
            1.1969767410f,
            -0.0975294719f,
            0.0028952508f,
            0.0097595295f,
            0.0065754026f,
        },
        new float[] // Filter 19
        {
            0.0065492257f,
            0.0061937044f,
            -0.0080279717f,
            -0.1166224228f,
            1.2037346856f,
            -0.1063230135f,
            -0.0008890990f,
            0.0087074424f,
            0.0066939837f,
        },
        new float[] // Filter 20
        {
            0.0066840571f,
            0.0075095982f,
            -0.0045512015f,
            -0.1126391135f,
            1.2059933196f,
            -0.1126391136f,
            -0.0045512015f,
            0.0075095982f,
            0.0066840571f,
        },
        new float[] // Filter 21
        {
            0.0066939837f,
            0.0087074424f,
            -0.0008890989f,
            -0.1063230133f,
            1.2037346856f,
            -0.1166224229f,
            -0.0080279717f,
            0.0061937043f,
            0.0065492257f,
        },
        new float[] // Filter 22
        {
            0.0065754026f,
            0.0097595295f,
            0.0028952508f,
            -0.0975294718f,
            1.1969767410f,
            -0.1184330051f,
            -0.0112628316f,
            0.0047886624f,
            0.0062952925f,
        },
        new float[] // Filter 23
        {
            0.0063270333f,
            0.0106398965f,
            0.0067330150f,
            -0.0861315366f,
            1.1857731728f,
            -0.1182439494f,
            -0.0142063325f,
            0.0033240148f,
            0.0059301001f,
        },
        new float[] // Filter 24
        {
            0.0059500011f,
            0.0113248618f,
            0.0105505051f,
            -0.0720221047f,
            1.1702128427f,
            -0.1162389118f,
            -0.0168166358f,
            0.0018293973f,
            0.0054633383f,
        },
        new float[] // Filter 25
        {
            0.0054478776f,
            0.0117935392f,
            0.0142701913f,
            -0.0551159094f,
            1.1504188696f,
            -0.1126096168f,
            -0.0190598496f,
            0.0003340074f,
            0.0049063228f,
        },
        new float[] // Filter 26
        {
            0.0048266775f,
            0.0120283182f,
            0.0178118296f,
            -0.0353513150f,
            1.1265473617f,
            -0.1075533517f,
            -0.0209102230f,
            -0.0011339027f,
            0.0042717488f,
        },
        new float[] // Filter 27
        {
            0.0040948093f,
            0.0120153025f,
            0.0210936884f,
            -0.0126918922f,
            1.0987858104f,
            -0.1012704846f,
            -0.0223502004f,
            -0.0025475010f,
            0.0035734270f,
        },
        new float[] // Filter 28
        {
            0.0032629808f,
            0.0117446956f,
            0.0240338606f,
            0.0128722504f,
            1.0673511678f,
            -0.0939620349f,
            -0.0233703397f,
            -0.0038818110f,
            0.0028260046f,
        },
        new float[] // Filter 29
        {
            0.0023440604f,
            0.0112111268f,
            0.0265516433f,
            0.0413234010f,
            1.0324876291f,
            -0.0858273269f,
            -0.0239691029f,
            -0.0051141007f,
            0.0020446780f,
        },
        new float[] // Filter 30
        {
            0.0013528931f,
            0.0104139084f,
            0.0285689687f,
            0.0726154626f,
            0.9944641460f,
            -0.0770617506f,
            -0.0241525253f,
            -0.0062242203f,
            0.0012449021f,
        },
        new float[] // Filter 31
        {
            0.0003060773f,
            0.0093572184f,
            0.0300118653f,
            0.1066734727f,
            0.9535717008f,
            -0.0678546570f,
            -0.0239337749f,
            -0.0071948838f,
            0.0004421024f,
        },
        new float[] // Filter 32
        {
            -0.0007782987f,
            0.0080502012f,
            0.0308119288f,
            0.1433934356f,
            0.9101203734f,
            -0.0583874087f,
            -0.0233326129f,
            -0.0080118920f,
            -0.0003486069f,
        },
        new float[] // Filter 33
        {
            -0.0018809534f,
            0.0065069844f,
            0.0309077829f,
            0.1826424756f,
            0.8644362338f,
            -0.0488316052f,
            -0.0223747671f,
            -0.0086642933f,
            -0.0011126915f,
        },
        new float[] // Filter 34
        {
            -0.0029817393f,
            0.0047466058f,
            0.0302465047f,
            0.2242593164f,
            0.8168580986f,
            -0.0393475016f,
            -0.0210912327f,
            -0.0091444836f,
            -0.0018364498f,
        },
        new float[] // Filter 35
        {
            -0.0040599952f,
            0.0027928498f,
            0.0287849961f,
            0.2680550877f,
            0.7677341874f,
            -0.0300826324f,
            -0.0195175138f,
            -0.0094482420f,
            -0.0025072440f,
        },
        new float[] // Filter 36
        {
            -0.0050949167f,
            0.0006739941f,
            0.0264912753f,
            0.3138144546f,
            0.7174187174f,
            -0.0211706530f,
            -0.0176928207f,
            -0.0095747072f,
            -0.0031137075f,
        },
        new float[] // Filter 37
        {
            -0.0060659402f,
            -0.0015775347f,
            0.0233456693f,
            0.3612970648f,
            0.6662684759f,
            -0.0127304056f,
            -0.0156592365f,
            -0.0095262938f,
            -0.0036459239f,
        },
        new float[] // Filter 38
        {
            -0.0069531333f,
            -0.0039255914f,
            0.0193418848f,
            0.4102392984f,
            0.6146394095f,
            -0.0048652138f,
            -0.0134608698f,
            -0.0093085527f,
            -0.0040955760f,
        },
        new float[] // Filter 39
        {
            -0.0077375836f,
            -0.0063308256f,
            0.0144879368f,
            0.4603563097f,
            0.5628832676f,
            0.0023375914f,
            -0.0111430058f,
            -0.0089299803f,
            -0.0044560618f,
        },
    };

    /// <summary>
    /// Returns one nine-tap filter for the requested polyphase position.
    /// </summary>
    public static ReadOnlySpan<float> GetPulseShaper(int phase) {
        if ((uint)phase >= CoefficientSets)
            throw new ArgumentOutOfRangeException(nameof(phase));

        return TxPulseShaper[phase];
    }

    /// <summary>
    /// Applies the exact rounding expression used by TX_PULSESHAPER_SCALE
    /// in the fixed-point C implementation.
    /// </summary>
    public static short ScaleCoefficient(float coefficient) {
        double scaled =
            FixedPointScale * coefficient +
            (coefficient >= 0.0f ? 0.5 : -0.5);

        return checked((short)scaled);
    }

    /// <summary>
    /// Creates the fixed-point representation of one filter.
    /// </summary>
    public static short[] GetFixedPointPulseShaper(int phase) {
        ReadOnlySpan<float> source = GetPulseShaper(phase);
        short[] result = new short[source.Length];

        for (int index = 0; index < source.Length; index++)
            result[index] = ScaleCoefficient(source[index]);

        return result;
    }

    // Native-name aliases retained for straightforward source migration.
    public const int TX_PULSESHAPER_COEFF_SETS = CoefficientSets;
    public const float TX_PULSESHAPER_GAIN = FloatingPointGain;
    public static float[][] tx_pulseshaper => TxPulseShaper;
}
