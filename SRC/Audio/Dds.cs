/*
 * TKFaxEngine - managed C# port
 *
 * Dds.cs
 *
 * Combined port of dds.h, dds_int.c and dds_float.c.
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2003 Steve Underwood.
 *
 * This port preserves the GNU Lesser General Public License version 2.1
 * licensing terms of the original source files.
 */

#nullable enable

namespace TKFaxEngine.Audio;

/// <summary>
/// Floating-point complex value used by the DDS routines.
/// </summary>
public readonly record struct DdsComplexFloat(float Real, float Imaginary);

/// <summary>
/// Native-width integer complex value used by the DDS routines.
/// </summary>
public readonly record struct DdsComplexInt(int Real, int Imaginary);

/// <summary>
/// Signed 16-bit integer complex value used by the DDS routines.
/// </summary>
public readonly record struct DdsComplexInt16(short Real, short Imaginary);

/// <summary>
/// Signed 32-bit integer complex value used by the DDS routines.
/// </summary>
public readonly record struct DdsComplexInt32(int Real, int Imaginary);

/// <summary>
/// Direct digital synthesis helpers for real and complex sine-wave generation.
/// </summary>
public static class Dds {
    public const int SampleRate = 8000;
    public const float Dbm0MaximumSinePower = 3.14f;
    public const float DbovMaximumSinePower = -3.02f;

    private const int IntegerTableBits = 8;
    private const int IntegerSteps = 1 << IntegerTableBits;
    private const int IntegerShift = 32 - 2 - IntegerTableBits;

    private const int FloatTableBits = 11;
    private const int FloatTableLength = 1 << FloatTableBits;
    private const int FloatTableShift = 32 - FloatTableBits;

    private const uint QuarterCycle = 1U << 30;

    private static readonly short[] IntegerSineTable =
    {
        0, 201, 402, 603, 804, 1005, 1206, 1407, 1608, 1809, 2009, 2210,
        2410, 2611, 2811, 3012, 3212, 3412, 3612, 3811, 4011, 4210, 4410, 4609,
        4808, 5007, 5205, 5404, 5602, 5800, 5998, 6195, 6393, 6590, 6786, 6983,
        7179, 7375, 7571, 7767, 7962, 8157, 8351, 8545, 8739, 8933, 9126, 9319,
        9512, 9704, 9896, 10087, 10278, 10469, 10659, 10849, 11039, 11228, 11417, 11605,
        11793, 11980, 12167, 12353, 12539, 12725, 12910, 13094, 13279, 13462, 13645, 13828,
        14010, 14191, 14372, 14553, 14732, 14912, 15090, 15269, 15446, 15623, 15800, 15976,
        16151, 16325, 16499, 16673, 16846, 17018, 17189, 17360, 17530, 17700, 17869, 18037,
        18204, 18371, 18537, 18703, 18868, 19032, 19195, 19357, 19519, 19680, 19841, 20000,
        20159, 20317, 20475, 20631, 20787, 20942, 21096, 21250, 21403, 21554, 21705, 21856,
        22005, 22154, 22301, 22448, 22594, 22739, 22884, 23027, 23170, 23311, 23452, 23592,
        23731, 23870, 24007, 24143, 24279, 24413, 24547, 24680, 24811, 24942, 25072, 25201,
        25329, 25456, 25582, 25708, 25832, 25955, 26077, 26198, 26319, 26438, 26556, 26674,
        26790, 26905, 27019, 27133, 27245, 27356, 27466, 27575, 27683, 27790, 27896, 28001,
        28105, 28208, 28310, 28411, 28510, 28609, 28706, 28803, 28898, 28992, 29085, 29177,
        29268, 29358, 29447, 29534, 29621, 29706, 29791, 29874, 29956, 30037, 30117, 30195,
        30273, 30349, 30424, 30498, 30571, 30643, 30714, 30783, 30852, 30919, 30985, 31050,
        31113, 31176, 31237, 31297, 31356, 31414, 31470, 31526, 31580, 31633, 31685, 31736,
        31785, 31833, 31880, 31926, 31971, 32014, 32057, 32098, 32137, 32176, 32213, 32250,
        32285, 32318, 32351, 32382, 32412, 32441, 32469, 32495, 32521, 32545, 32567, 32589,
        32609, 32628, 32646, 32663, 32678, 32692, 32705, 32717, 32728, 32737, 32745, 32752,
        32757, 32761, 32765, 32766, 32767
    };

    private static readonly float[] FloatSineTable = CreateFloatSineTable();

    /// <summary>
    /// Managed equivalent of the native DDS_PHASE_RATE macro.
    /// </summary>
    public static int PhaseRate(float frequency) {
        return unchecked((int)(frequency * 65536.0f * 65536.0f / SampleRate));
    }

    /// <summary>
    /// Managed equivalent of the native DDS_PHASE macro.
    /// </summary>
    public static int PhaseDegrees(float angle) {
        float normalized = angle < 0.0f ? 360.0f + angle : angle;
        uint phase = unchecked((uint)(normalized * 65536.0f * 65536.0f / 360.0f));
        return unchecked((int)phase);
    }

    public static float PhaseToRadians(uint phase) {
        return phase * 2.0f * 3.1415926f / (65536.0f * 65536.0f);
    }

    public static float Frequency(int phaseRate) {
        return phaseRate * (float)SampleRate / (65536.0f * 65536.0f);
    }

    public static short ScalingDbm0(float level) {
        float value = MathF.Pow(10.0f, (level - Dbm0MaximumSinePower) / 20.0f) * 32767.0f;
        return unchecked((short)value);
    }

    public static short ScalingDbov(float level) {
        float value = MathF.Pow(10.0f, (level - DbovMaximumSinePower) / 20.0f) * 32767.0f;
        return unchecked((short)value);
    }

    public static float ScalingDbm0Float(float level) {
        return MathF.Pow(10.0f, (level - Dbm0MaximumSinePower) / 20.0f) * 32767.0f;
    }

    public static float ScalingDbovFloat(float level) {
        return MathF.Pow(10.0f, (level - DbovMaximumSinePower) / 20.0f) * 32767.0f;
    }

    public static short Lookup(uint phase) {
        uint reducedPhase = phase >> IntegerShift;
        uint step = reducedPhase & (IntegerSteps - 1U);

        if ((reducedPhase & IntegerSteps) != 0)
            step = IntegerSteps - step;

        short amplitude = IntegerSineTable[step];

        if ((reducedPhase & (2U * IntegerSteps)) != 0)
            amplitude = unchecked((short)-amplitude);

        return amplitude;
    }

    public static short Offset(uint phaseAccumulator, int phaseOffset) {
        return Lookup(AddPhase(phaseAccumulator, phaseOffset));
    }

    public static void Advance(ref uint phaseAccumulator, int phaseRate) {
        phaseAccumulator = AddPhase(phaseAccumulator, phaseRate);
    }

    public static short Generate(ref uint phaseAccumulator, int phaseRate) {
        short amplitude = Lookup(phaseAccumulator);
        phaseAccumulator = AddPhase(phaseAccumulator, phaseRate);
        return amplitude;
    }

    public static short GenerateModulated(
        ref uint phaseAccumulator,
        int phaseRate,
        short scale,
        int phaseOffset) {
        int sample = (Lookup(AddPhase(phaseAccumulator, phaseOffset)) * scale) >> 15;
        phaseAccumulator = AddPhase(phaseAccumulator, phaseRate);
        return unchecked((short)sample);
    }

    public static DdsComplexInt LookupComplexInt(uint phase) {
        return new DdsComplexInt(
            Lookup(unchecked(phase + QuarterCycle)),
            Lookup(phase));
    }

    public static DdsComplexInt GenerateComplexInt(ref uint phaseAccumulator, int phaseRate) {
        DdsComplexInt amplitude = LookupComplexInt(phaseAccumulator);
        phaseAccumulator = AddPhase(phaseAccumulator, phaseRate);
        return amplitude;
    }

    public static DdsComplexInt GenerateComplexIntModulated(
        ref uint phaseAccumulator,
        int phaseRate,
        short scale,
        int phaseOffset) {
        uint phase = AddPhase(phaseAccumulator, phaseOffset);
        DdsComplexInt amplitude = new(
            (Lookup(unchecked(phase + QuarterCycle)) * scale) >> 15,
            (Lookup(phase) * scale) >> 15);

        phaseAccumulator = AddPhase(phaseAccumulator, phaseRate);
        return amplitude;
    }

    public static DdsComplexInt16 LookupComplexInt16(uint phase) {
        return new DdsComplexInt16(
            Lookup(unchecked(phase + QuarterCycle)),
            Lookup(phase));
    }

    public static DdsComplexInt16 GenerateComplexInt16(ref uint phaseAccumulator, int phaseRate) {
        DdsComplexInt16 amplitude = LookupComplexInt16(phaseAccumulator);
        phaseAccumulator = AddPhase(phaseAccumulator, phaseRate);
        return amplitude;
    }

    public static DdsComplexInt16 GenerateComplexInt16Modulated(
        ref uint phaseAccumulator,
        int phaseRate,
        short scale,
        int phaseOffset) {
        uint phase = AddPhase(phaseAccumulator, phaseOffset);
        DdsComplexInt16 amplitude = new(
            unchecked((short)((Lookup(unchecked(phase + QuarterCycle)) * scale) >> 15)),
            unchecked((short)((Lookup(phase) * scale) >> 15)));

        phaseAccumulator = AddPhase(phaseAccumulator, phaseRate);
        return amplitude;
    }

    public static DdsComplexInt32 LookupComplexInt32(uint phase) {
        return new DdsComplexInt32(
            Lookup(unchecked(phase + QuarterCycle)),
            Lookup(phase));
    }

    public static DdsComplexInt32 GenerateComplexInt32(ref uint phaseAccumulator, int phaseRate) {
        DdsComplexInt32 amplitude = LookupComplexInt32(phaseAccumulator);
        phaseAccumulator = AddPhase(phaseAccumulator, phaseRate);
        return amplitude;
    }

    public static DdsComplexInt32 GenerateComplexInt32Modulated(
        ref uint phaseAccumulator,
        int phaseRate,
        short scale,
        int phaseOffset) {
        uint phase = AddPhase(phaseAccumulator, phaseOffset);
        DdsComplexInt32 amplitude = new(
            (Lookup(unchecked(phase + QuarterCycle)) * scale) >> 15,
            (Lookup(phase) * scale) >> 15);

        phaseAccumulator = AddPhase(phaseAccumulator, phaseRate);
        return amplitude;
    }

    public static float LookupFloat(uint phase) {
        return FloatSineTable[phase >> FloatTableShift];
    }

    public static float OffsetFloat(uint phaseAccumulator, int phaseOffset) {
        return LookupFloat(AddPhase(phaseAccumulator, phaseOffset));
    }

    public static void AdvanceFloat(ref uint phaseAccumulator, int phaseRate) {
        phaseAccumulator = AddPhase(phaseAccumulator, phaseRate);
    }

    public static float GenerateFloat(ref uint phaseAccumulator, int phaseRate) {
        float amplitude = LookupFloat(phaseAccumulator);
        phaseAccumulator = AddPhase(phaseAccumulator, phaseRate);
        return amplitude;
    }

    public static float GenerateFloatModulated(
        ref uint phaseAccumulator,
        int phaseRate,
        float scale,
        int phaseOffset) {
        float amplitude = LookupFloat(AddPhase(phaseAccumulator, phaseOffset)) * scale;
        phaseAccumulator = AddPhase(phaseAccumulator, phaseRate);
        return amplitude;
    }

    public static DdsComplexFloat LookupComplexFloat(uint phase) {
        return new DdsComplexFloat(
            LookupFloat(unchecked(phase + QuarterCycle)),
            LookupFloat(phase));
    }

    public static DdsComplexFloat GenerateComplexFloat(ref uint phaseAccumulator, int phaseRate) {
        DdsComplexFloat amplitude = LookupComplexFloat(phaseAccumulator);
        phaseAccumulator = AddPhase(phaseAccumulator, phaseRate);
        return amplitude;
    }

    public static DdsComplexFloat GenerateComplexFloatModulated(
        ref uint phaseAccumulator,
        int phaseRate,
        float scale,
        int phaseOffset) {
        uint phase = AddPhase(phaseAccumulator, phaseOffset);
        DdsComplexFloat amplitude = new(
            LookupFloat(unchecked(phase + QuarterCycle)) * scale,
            LookupFloat(phase) * scale);

        phaseAccumulator = AddPhase(phaseAccumulator, phaseRate);
        return amplitude;
    }

    // Native-compatible facade ------------------------------------------------

    public static float dds_phase_to_radians(uint phase) => PhaseToRadians(phase);
    public static int dds_phase_rate(float frequency) => PhaseRate(frequency);
    public static float dds_frequency(int phaseRate) => Frequency(phaseRate);
    public static short dds_scaling_dbm0(float level) => ScalingDbm0(level);
    public static short dds_scaling_dbov(float level) => ScalingDbov(level);
    public static short dds_lookup(uint phase) => Lookup(phase);
    public static short dds_offset(uint phaseAccumulator, int phaseOffset) => Offset(phaseAccumulator, phaseOffset);
    public static void dds_advance(ref uint phaseAccumulator, int phaseRate) => Advance(ref phaseAccumulator, phaseRate);
    public static short dds(ref uint phaseAccumulator, int phaseRate) => Generate(ref phaseAccumulator, phaseRate);

    public static short dds_mod(
        ref uint phaseAccumulator,
        int phaseRate,
        short scale,
        int phase) =>
        GenerateModulated(ref phaseAccumulator, phaseRate, scale, phase);

    public static DdsComplexInt dds_lookup_complexi(uint phase) => LookupComplexInt(phase);

    public static DdsComplexInt dds_complexi(ref uint phaseAccumulator, int phaseRate) =>
        GenerateComplexInt(ref phaseAccumulator, phaseRate);

    public static DdsComplexInt dds_complexi_mod(
        ref uint phaseAccumulator,
        int phaseRate,
        short scale,
        int phase) =>
        GenerateComplexIntModulated(ref phaseAccumulator, phaseRate, scale, phase);

    public static DdsComplexInt16 dds_lookup_complexi16(uint phase) => LookupComplexInt16(phase);

    public static DdsComplexInt16 dds_complexi16(ref uint phaseAccumulator, int phaseRate) =>
        GenerateComplexInt16(ref phaseAccumulator, phaseRate);

    public static DdsComplexInt16 dds_complexi16_mod(
        ref uint phaseAccumulator,
        int phaseRate,
        short scale,
        int phase) =>
        GenerateComplexInt16Modulated(ref phaseAccumulator, phaseRate, scale, phase);

    public static DdsComplexInt32 dds_lookup_complexi32(uint phase) => LookupComplexInt32(phase);

    public static DdsComplexInt32 dds_complexi32(ref uint phaseAccumulator, int phaseRate) =>
        GenerateComplexInt32(ref phaseAccumulator, phaseRate);

    public static DdsComplexInt32 dds_complexi32_mod(
        ref uint phaseAccumulator,
        int phaseRate,
        short scale,
        int phase) =>
        GenerateComplexInt32Modulated(ref phaseAccumulator, phaseRate, scale, phase);

    public static int dds_phase_ratef(float frequency) => PhaseRate(frequency);
    public static float dds_frequencyf(int phaseRate) => Frequency(phaseRate);
    public static float dds_scaling_dbm0f(float level) => ScalingDbm0Float(level);
    public static float dds_scaling_dbovf(float level) => ScalingDbovFloat(level);
    public static void dds_advancef(ref uint phaseAccumulator, int phaseRate) => AdvanceFloat(ref phaseAccumulator, phaseRate);
    public static float ddsf(ref uint phaseAccumulator, int phaseRate) => GenerateFloat(ref phaseAccumulator, phaseRate);
    public static float dds_lookupf(uint phase) => LookupFloat(phase);
    public static float dds_offsetf(uint phaseAccumulator, int phaseOffset) => OffsetFloat(phaseAccumulator, phaseOffset);

    public static float dds_modf(
        ref uint phaseAccumulator,
        int phaseRate,
        float scale,
        int phase) =>
        GenerateFloatModulated(ref phaseAccumulator, phaseRate, scale, phase);

    public static DdsComplexFloat dds_lookup_complexf(uint phase) => LookupComplexFloat(phase);

    public static DdsComplexFloat dds_complexf(ref uint phaseAccumulator, int phaseRate) =>
        GenerateComplexFloat(ref phaseAccumulator, phaseRate);

    public static DdsComplexFloat dds_complex_modf(
        ref uint phaseAccumulator,
        int phaseRate,
        float scale,
        int phase) =>
        GenerateComplexFloatModulated(ref phaseAccumulator, phaseRate, scale, phase);

    private static float[] CreateFloatSineTable() {
        float[] table = new float[FloatTableLength];
        double angularStep = 2.0 * Math.PI / FloatTableLength;

        for (int i = 0; i < table.Length; i++)
            table[i] = (float)Math.Sin(i * angularStep);

        return table;
    }

    private static uint AddPhase(uint phase, int increment) {
        return unchecked(phase + (uint)increment);
    }

}
