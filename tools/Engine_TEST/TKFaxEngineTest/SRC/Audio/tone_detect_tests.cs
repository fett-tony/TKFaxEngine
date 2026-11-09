/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * tone_detect_tests.c
 *
 * Original test written by Steve Underwood <steveu@coppice.org>.
 * Managed C# port preserving the complete deterministic and AWGN
 * periodogram test sequence.
 */

#nullable enable

using System.Globalization;
using TKFaxEngine.Audio;

namespace TKFaxEngineTest.Audio.Test;

public static class ToneDetectTests {
    private const int DEC_SAMPLE_RATE = 800;
    private const int DEC_RATIO = 10;
    private const int BLOCK_LEN = 56;
    private const int PG_WINDOW = 56;
    private const float FREQ1 = 440.0f;
    private const float FREQ2 = 480.0f;

    public sealed class ExitException : Exception {
        public ExitException(int exitCode) => ExitCode = exitCode;
        public int ExitCode { get; }
    }

    private static void Exit(int code) => throw new ExitException(code);

    private static int periodogram_tests() {
        var coefficients = new ToneComplexF[PG_WINDOW / 2];
        var complexSamples = new ToneComplexF[BLOCK_LEN];

        int phaseRate1 = unchecked(DEC_RATIO * Dds.PhaseRate(FREQ1 - 5.0f));
        int phaseRate2 = unchecked(DEC_RATIO * Dds.PhaseRate(FREQ2));
        uint phaseAccumulator1 = 0;
        uint phaseAccumulator2 = 0;

        int coefficientCount = ToneDetect.GeneratePeriodogramCoefficients(
            coefficients,
            FREQ1,
            DEC_SAMPLE_RATE,
            PG_WINDOW);
        if (coefficientCount != PG_WINDOW / 2) {
            Console.Error.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "ERROR tone_detect coefficient count mismatch: actual={0} expected={1}",
                    coefficientCount,
                    PG_WINDOW / 2));
            return -1;
        }

        float periodogramScale = ToneDetect.GeneratePeriodogramPhaseOffset(
            out ToneComplexF phaseOffset,
            FREQ1,
            DEC_SAMPLE_RATE,
            PG_WINDOW);
        float scale1 = Dds.ScalingDbm0Float(-6.0f);
        float scale2 = Dds.ScalingDbm0Float(-6.0f);

        ToneComplexF lastResult = new(0.0f, 0.0f);
        float minimumLevel = 1.0e30f;
        float maximumLevel = 0.0f;
        int measuredWindows = 0;

        for (int window = 0; window < 100; window++) {
            for (int sample = 0; sample < PG_WINDOW; sample++) {
                DdsComplexFloat result1 = Dds.GenerateComplexFloat(
                    ref phaseAccumulator1,
                    phaseRate1);
                DdsComplexFloat result2 = Dds.GenerateComplexFloat(
                    ref phaseAccumulator2,
                    phaseRate2);
                complexSamples[sample] = new ToneComplexF(
                    result1.Real * scale1 + result2.Real * scale2,
                    result1.Imaginary * scale1 + result2.Imaginary * scale2);
            }

            ToneComplexF result = ToneDetect.Periodogram(
                coefficients,
                complexSamples,
                PG_WINDOW);
            float level = MathF.Sqrt(
                result.Real * result.Real + result.Imaginary * result.Imaginary);
            float frequencyError = ToneDetect.PeriodogramFrequencyError(
                phaseOffset,
                periodogramScale,
                lastResult,
                result);
            lastResult = result;

            if (window == 0)
                continue;

            measuredWindows++;
            if (level < minimumLevel)
                minimumLevel = level;
            if (level > maximumLevel)
                maximumLevel = level;

            if (level < scale1 * 0.8f || level > scale1 * 1.2f) {
                Console.Error.WriteLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ERROR tone_detect deterministic level: window={0} actual={1:F6} minimum={2:F6} maximum={3:F6} expected={4:F6}",
                        window,
                        level,
                        scale1 * 0.8f,
                        scale1 * 1.2f,
                        scale1));
                return -1;
            }

            if (frequencyError < -10.0f || frequencyError > 10.0f) {
                Console.Error.WriteLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ERROR tone_detect deterministic frequency: window={0} actual={1:F6} minimum=-10.000000 maximum=10.000000",
                        window,
                        frequencyError));
                return -1;
            }
        }

        Console.WriteLine(
            string.Format(
                CultureInfo.InvariantCulture,
                "STATE tone_detect phase=DETERMINISTIC windows={0} min_level={1:F6} max_level={2:F6} expected={3:F6} minimum={4:F6} maximum={5:F6} result=PASS",
                measuredWindows,
                minimumLevel,
                maximumLevel,
                scale1,
                scale1 * 0.8f,
                scale1 * 1.2f));

        phaseAccumulator1 = 0;
        phaseAccumulator2 = 0;

        for (int noiseLevelDbm0 = -50; noiseLevelDbm0 < 0; noiseLevelDbm0++) {
            var realNoise = new Awgn(1_234_567, noiseLevelDbm0);
            var imaginaryNoise = new Awgn(7_654_321, noiseLevelDbm0);

            lastResult = new ToneComplexF(0.0f, 0.0f);
            double levelSum = 0.0;
            minimumLevel = 1.0e30f;
            maximumLevel = 0.0f;
            measuredWindows = 0;
            int levelOutliers = 0;

            for (int window = 0; window < 100; window++) {
                for (int sample = 0; sample < PG_WINDOW; sample++) {
                    DdsComplexFloat result1 = Dds.GenerateComplexFloat(
                        ref phaseAccumulator1,
                        phaseRate1);
                    DdsComplexFloat result2 = Dds.GenerateComplexFloat(
                        ref phaseAccumulator2,
                        phaseRate2);
                    complexSamples[sample] = new ToneComplexF(
                        result1.Real * scale1 + result2.Real * scale2 + realNoise.NextSample(),
                        result1.Imaginary * scale1 + result2.Imaginary * scale2 + imaginaryNoise.NextSample());
                }

                ToneComplexF result = ToneDetect.Periodogram(
                    coefficients,
                    complexSamples,
                    PG_WINDOW);
                float level = MathF.Sqrt(
                    result.Real * result.Real + result.Imaginary * result.Imaginary);
                float frequencyError = ToneDetect.PeriodogramFrequencyError(
                    phaseOffset,
                    periodogramScale,
                    lastResult,
                    result);
                lastResult = result;

                if (window == 0)
                    continue;

                levelSum += level;
                measuredWindows++;
                if (level < minimumLevel)
                    minimumLevel = level;
                if (level > maximumLevel)
                    maximumLevel = level;
                if (level < scale1 * 0.8f || level > scale1 * 1.2f)
                    levelOutliers++;

                if (frequencyError < -10.0f || frequencyError > 10.0f) {
                    Console.Error.WriteLine(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "ERROR tone_detect AWGN frequency: noise_dbm0={0} window={1} actual={2:F6} minimum=-10.000000 maximum=10.000000 level={3:F6}",
                            noiseLevelDbm0,
                            window,
                            frequencyError,
                            level));
                    return -1;
                }
            }

            float meanLevel = (float)(levelSum / measuredWindows);
            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "STATE tone_detect phase=AWGN noise_dbm0={0} windows={1} mean_level={2:F6} min_level={3:F6} max_level={4:F6} outliers={5} expected={6:F6} minimum={7:F6} maximum={8:F6} frequency_limit_hz=10.000000",
                    noiseLevelDbm0,
                    measuredWindows,
                    meanLevel,
                    minimumLevel,
                    maximumLevel,
                    levelOutliers,
                    scale1,
                    scale1 * 0.8f,
                    scale1 * 1.2f));

            if (meanLevel < scale1 * 0.8f || meanLevel > scale1 * 1.2f) {
                Console.Error.WriteLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ERROR tone_detect AWGN mean level: noise_dbm0={0} actual={1:F6} minimum={2:F6} maximum={3:F6} expected={4:F6} windows={5} min_window={6:F6} max_window={7:F6} outliers={8}",
                        noiseLevelDbm0,
                        meanLevel,
                        scale1 * 0.8f,
                        scale1 * 1.2f,
                        scale1,
                        measuredWindows,
                        minimumLevel,
                        maximumLevel,
                        levelOutliers));
                return -1;
            }
        }

        return 0;
    }

    public static int tkfx_tone_detect_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        if (periodogram_tests() != 0)
            Exit(2);

        Console.WriteLine("Tests passed");
        return 0;
    }
}
