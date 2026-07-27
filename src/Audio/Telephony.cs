/*
 * TKFaxEngine - managed C# port
 *
 * Telephony.cs
 *
 * Direct managed port of Audio/telephony.h.
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 *
 * This port preserves the GNU Lesser General Public License version 2.1
 * licensing terms of the original source file.
 */

#nullable enable

namespace TKFaxEngine.Audio;

/// <summary>Managed equivalent of <c>span_timestamp_t</c>.</summary>
public readonly record struct SpanTimestamp(ulong Value) {
    public static implicit operator ulong(SpanTimestamp value) => value.Value;
    public static implicit operator SpanTimestamp(ulong value) => new(value);
}

/// <summary>Managed equivalent of <c>span_sample_timer_t</c>.</summary>
public readonly record struct SpanSampleTimer(int Value) {
    public static implicit operator int(SpanSampleTimer value) => value.Value;
    public static implicit operator SpanSampleTimer(int value) => new(value);
}

/// <summary>Managed equivalent of <c>span_rx_handler_t</c>.</summary>
public delegate int SpanRxHandler(
    object? state,
    ReadOnlySpan<short> amplitude,
    int length);

/// <summary>Managed equivalent of <c>span_mod_handler_t</c>.</summary>
public delegate int SpanModHandler(
    object? state,
    Span<short> amplitude,
    int length);

/// <summary>Managed equivalent of <c>span_rx_fillin_handler_t</c>.</summary>
public delegate int SpanRxFillInHandler(
    object? state,
    int length);

/// <summary>Managed equivalent of <c>span_tx_handler_t</c>.</summary>
public delegate int SpanTxHandler(
    object? state,
    Span<short> amplitude,
    int maximumLength);

/// <summary>Common telephony constants and fixed-point conversion helpers.</summary>
public static class Telephony {
    public const int SAMPLE_RATE = 8000;

    public const float DBM0_MAX_POWER = 3.14f + 3.02f;
    public const float DBM0_MAX_SINE_POWER = 3.14f;
    public const float DBOV_MAX_POWER = 0.0f;
    public const float DBOV_MAX_SINE_POWER = -3.02f;

    public static int seconds_to_samples(int time) =>
        unchecked(time * SAMPLE_RATE);

    public static int milliseconds_to_samples(int time) =>
        unchecked(time * (SAMPLE_RATE / 1000));

    public static int microseconds_to_samples(int time) =>
        time / (1_000_000 / SAMPLE_RATE);

    public static short FP_Q16_0(double value) => Fixed16(value, 1.0);
    public static short FP_Q15_1(double value) => Fixed16(value, 2.0);
    public static short FP_Q14_2(double value) => Fixed16(value, 4.0);
    public static short FP_Q13_3(double value) => Fixed16(value, 8.0);
    public static short FP_Q12_4(double value) => Fixed16(value, 16.0);
    public static short FP_Q11_5(double value) => Fixed16(value, 32.0);
    public static short FP_Q10_6(double value) => Fixed16(value, 64.0);
    public static short FP_Q9_7(double value) => Fixed16(value, 128.0);
    public static short FP_Q8_8(double value) => Fixed16(value, 256.0);
    public static short FP_Q7_9(double value) => Fixed16(value, 512.0);
    public static short FP_Q6_10(double value) => Fixed16(value, 1024.0);
    public static short FP_Q5_11(double value) => Fixed16(value, 2048.0);
    public static short FP_Q4_12(double value) => Fixed16(value, 4096.0);
    public static short FP_Q3_13(double value) => Fixed16(value, 8192.0);
    public static short FP_Q2_14(double value) => Fixed16(value, 16384.0);
    public static short FP_Q1_15(double value) => Fixed16(value, 32768.0);

    public static int FP_Q32_0(double value) => Fixed32(value, 1.0);
    public static int FP_Q31_1(double value) => Fixed32(value, 2.0);
    public static int FP_Q30_2(double value) => Fixed32(value, 4.0);
    public static int FP_Q29_3(double value) => Fixed32(value, 8.0);
    public static int FP_Q28_4(double value) => Fixed32(value, 16.0);
    public static int FP_Q27_5(double value) => Fixed32(value, 32.0);
    public static int FP_Q26_6(double value) => Fixed32(value, 64.0);
    public static int FP_Q25_7(double value) => Fixed32(value, 128.0);
    public static int FP_Q24_8(double value) => Fixed32(value, 256.0);
    public static int FP_Q23_9(double value) => Fixed32(value, 512.0);
    public static int FP_Q22_10(double value) => Fixed32(value, 1024.0);
    public static int FP_Q21_11(double value) => Fixed32(value, 2048.0);
    public static int FP_Q20_12(double value) => Fixed32(value, 4096.0);
    public static int FP_Q19_13(double value) => Fixed32(value, 8192.0);
    public static int FP_Q18_14(double value) => Fixed32(value, 16384.0);
    public static int FP_Q17_15(double value) => Fixed32(value, 32768.0);
    public static int FP_Q16_16(double value) => Fixed32(value, 65536.0);
    public static int FP_Q15_17(double value) => Fixed32(value, 131072.0);
    public static int FP_Q14_18(double value) => Fixed32(value, 262144.0);
    public static int FP_Q13_19(double value) => Fixed32(value, 524288.0);
    public static int FP_Q12_20(double value) => Fixed32(value, 1048576.0);
    public static int FP_Q11_21(double value) => Fixed32(value, 2097152.0);
    public static int FP_Q10_22(double value) => Fixed32(value, 4194304.0);
    public static int FP_Q9_23(double value) => Fixed32(value, 8388608.0);
    public static int FP_Q8_24(double value) => Fixed32(value, 16777216.0);
    public static int FP_Q7_25(double value) => Fixed32(value, 33554432.0);
    public static int FP_Q6_26(double value) => Fixed32(value, 67108864.0);
    public static int FP_Q5_27(double value) => Fixed32(value, 134217728.0);
    public static int FP_Q4_28(double value) => Fixed32(value, 268435456.0);
    public static int FP_Q3_29(double value) => Fixed32(value, 536870912.0);
    public static int FP_Q2_30(double value) => Fixed32(value, 1073741824.0);
    public static int FP_Q1_31(double value) => Fixed32(value, 2147483648.0);

    public static float db_to_power_ratio(float value) =>
        MathF.Pow(10.0f, value / 10.0f);

    public static float db_to_amplitude_ratio(float value) =>
        MathF.Pow(10.0f, value / 20.0f);

    public static float power_ratio_to_db(float value) =>
        10.0f * MathF.Log10(value);

    public static float amplitude_ratio_to_db(float value) =>
        20.0f * MathF.Log10(value);

#if TKFAXENGINE_USE_FIXED_POINT
    public static int energy_threshold_dbm0(int length, float threshold) =>
        unchecked((int)(
            (length * 256.0f * 256.0f / 2.0f) *
            MathF.Pow(
                10.0f,
                (threshold - DBM0_MAX_SINE_POWER) / 10.0f)));

    public static int energy_threshold_dbmov(int length, float threshold) =>
        unchecked((int)(
            (length * 256.0f * 256.0f / 2.0f) *
            MathF.Pow(
                10.0f,
                (threshold - DBOV_MAX_SINE_POWER) / 10.0f)));
#else
    public static float energy_threshold_dbm0(int length, float threshold) =>
        (length * 32768.0f * 32768.0f / 2.0f) *
        MathF.Pow(
            10.0f,
            (threshold - DBM0_MAX_SINE_POWER) / 10.0f);

    public static float energy_threshold_dbmov(int length, float threshold) =>
        (length * 32768.0f * 32768.0f / 2.0f) *
        MathF.Pow(
            10.0f,
            (threshold - DBOV_MAX_SINE_POWER) / 10.0f);
#endif

    private static short Fixed16(double value, double scale) =>
        unchecked((short)(
            scale * value +
            (value >= 0.0 ? 0.5 : -0.5)));

    private static int Fixed32(double value, double scale) =>
        unchecked((int)(
            scale * value +
            (value >= 0.0 ? 0.5 : -0.5)));
}
