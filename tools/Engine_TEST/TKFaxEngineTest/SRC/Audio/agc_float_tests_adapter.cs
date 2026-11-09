/*
 * Auto-generated all-in-one adapter port for agc_float_tests.c.
 *
 * Original adapter logic retained: execute the managed test entry point and
 * convert a requested process exit into a return code for the test runner.
 */

#nullable enable

namespace TKFaxEngineTest.Audio.Test;

public static class agc_float_tests_adapter {
    private static int tkfx_agc_float_tests_exit_code;

    public static void tkfx_agc_float_tests_exit(int code) {
        tkfx_agc_float_tests_exit_code = code;
        throw new AgcFloatAdapterExitException(code);
    }

    public static int tkfx_agc_float_tests_main(string[] testArguments) {
        tkfx_agc_float_tests_exit_code = 0;

        try {
            return agc_float_tests.main(testArguments);
        } catch (AgcFloatAdapterExitException ex) {
            tkfx_agc_float_tests_exit_code = ex.ExitCode;
            return tkfx_agc_float_tests_exit_code;
        }
    }

    private sealed class AgcFloatAdapterExitException : Exception {
        public AgcFloatAdapterExitException(int exitCode)
            : base($"agc_float_tests requested exit code {exitCode}.") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }
}
