/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * awgn_tests.cs - sanity and statistical-distribution tests for AWGN.
 *
 * Original C test written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2001 Steve Underwood
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License version 2, as
 * published by the Free Software Foundation.
 *
 * Managed C# port. The complete 11-level RMS sweep, 1,000,000 samples
 * per level and 10,000,000-sample histogram are retained.
 */

#nullable enable

using System.Globalization;
using TKFaxEngine.Audio;

namespace TKFaxEngineTest.Audio.Test;

public static class awgn_tests {
    private const string OUT_FILE_NAME = "awgn.wav";
    private const int RandomSeed = 1234567;

    public static int main(string[] testArguments) {
        try {
            return tkfx_awgn_tests_body_main(testArguments);
        } catch (AwgnTestExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_awgn_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);
        _ = OUT_FILE_NAME;

        for (int requestedLevel = -50; requestedLevel <= 0; requestedLevel += 5) {
            int clipHigh = 0;
            int clipLow = 0;
            double total = 0.0;
            var noiseSource = new Awgn(RandomSeed, requestedLevel);
            const int totalSamples = 1_000_000;

            for (int i = 0; i < totalSamples; i++) {
                short value = noiseSource.NextSample();

                if (value == short.MaxValue)
                    clipHigh++;
                else if (value == short.MinValue)
                    clipLow++;

                total += (double)value * value;
            }

            double measuredRms = Math.Sqrt(total / totalSamples);
            double error = 100.0 * (1.0 - measuredRms / noiseSource.Rms);
            double measuredLevel =
                10.0 * Math.Log10(
                    (total / totalSamples) / (32768.0 * 32768.0) + 1.0e-10) +
                Awgn.Dbm0MaximumPower;

            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "RMS = {0:F3} (expected {1}) {2:F2}% error [clipped samples {3}+{4}]",
                    measuredLevel,
                    requestedLevel,
                    error,
                    clipLow,
                    clipHigh));

            // At 0 dBm0 clipping necessarily distorts the RMS result.
            if (requestedLevel < 0 && Math.Abs(error) > 0.2)
                Fail("Test failed.");
        }

        var bins = new int[65_536];
        int histogramClipHigh = 0;
        int histogramClipLow = 0;
        var histogramNoiseSource = new Awgn(RandomSeed, -15.0);
        const int histogramSamples = 10_000_000;

        for (int i = 0; i < histogramSamples; i++) {
            short value = histogramNoiseSource.NextSample();

            if (value == short.MaxValue)
                histogramClipHigh++;
            else if (value == short.MinValue)
                histogramClipLow++;

            bins[value + 32_768]++;
        }

        _ = histogramClipHigh;
        _ = histogramClipLow;

        double standardDeviation = histogramNoiseSource.Rms;
        for (int i = 0; i < 65_536 - 10; i++) {
            double sampleValue = i - 32_768;

            double predictedProbability =
                (1.0 / (standardDeviation * Math.Sqrt(2.0 * Math.PI))) *
                Math.Exp(
                    -(sampleValue * sampleValue) /
                    (2.0 * standardDeviation * standardDeviation));

            double measuredProbability = 0.0;
            for (int j = 0; j < 10; j++)
                measuredProbability += bins[i + j];

            measuredProbability /= 10.0;
            measuredProbability /= histogramSamples;

            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0,6} {1:F7} {2:F7}",
                    i - 32_768,
                    measuredProbability,
                    predictedProbability));
        }

        Console.WriteLine("Tests passed.");
        return 0;
    }

    private static void Fail(string message) {
        Console.WriteLine(message);
        throw new AwgnTestExitException(2);
    }

    public sealed class AwgnTestExitException : Exception {
        public AwgnTestExitException(int exitCode)
            : base($"awgn_tests requested exit code {exitCode}.") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }
}
