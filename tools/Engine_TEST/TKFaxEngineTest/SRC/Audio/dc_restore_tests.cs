/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * dc_restore_tests.cs - Tests for the DC restoration functions.
 *
 * Original C test written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2001 Steve Underwood.
 *
 * Managed C# port preserving both 100,000-sample passes, the original
 * AWGN seed and level, the 5000-count DC offset and the +/-50 limit.
 */

#nullable enable

using TKFaxEngine.Audio;

namespace TKFaxEngineTest.Audio.Test;

public static class dc_restore_tests {
    public static int main(string[] testArguments) {
        try {
            return tkfx_dc_restore_tests_body_main(
                testArguments);
        } catch (DcRestoreTestsExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_dc_restore_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        const int seed = 1_234_567;
        const int dcOffset = 5000;

        var noiseSource =
            new Awgn(
                seed,
                -10.0);

        var dcState =
            new DcRestoreState();

        DcRestore.dc_restore_init(dcState);

        for (int i = 0; i < 100_000; i++) {
            short dirty =
                unchecked(
                    (short)(
                        noiseSource.NextSample() +
                        dcOffset));

            _ = DcRestore.dc_restore(
                dcState,
                dirty);

            if (i % 1000 == 0) {
                Console.WriteLine(
                    $"Sample {i,6}: " +
                    $"{DcRestore.dc_restore_estimate(dcState)} " +
                    $"(expect {dcOffset})");
            }
        }

        int minimum = 99_999;
        int maximum = -99_999;

        for (int i = 0; i < 100_000; i++) {
            short dirty =
                unchecked(
                    (short)(
                        noiseSource.NextSample() +
                        dcOffset));

            _ = DcRestore.dc_restore(
                dcState,
                dirty);

            int estimate =
                DcRestore.dc_restore_estimate(
                    dcState);

            if (estimate < minimum)
                minimum = estimate;

            if (estimate > maximum)
                maximum = estimate;
        }

        Console.WriteLine(
            "Spread of DC estimate for an offset of " +
            $"{dcOffset} was {minimum} to {maximum}");

        if (minimum < dcOffset - 50 ||
            maximum > dcOffset + 50) {
            Console.WriteLine("Test failed.");

            throw new DcRestoreTestsExitException(
                2);
        }

        Console.WriteLine("Test passed.");
        return 0;
    }

    public sealed class DcRestoreTestsExitException :
        Exception {
        public DcRestoreTestsExitException(
            int exitCode)
            : base(
                "dc_restore_tests requested exit code " +
                $"{exitCode}.") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }
}
