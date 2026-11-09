/*
 * Fresh managed adapter port for logging_tests_adapter.c.
 */

#nullable enable

namespace TKFaxEngineTest.Core.Test;

public static class logging_tests_adapter {
    private static int tkfx_logging_tests_exit_code;

    public static void tkfx_logging_tests_exit(int code) {
        tkfx_logging_tests_exit_code = code;
        throw new logging_tests.LoggingTestsExitException(code);
    }

    public static int tkfx_logging_tests_main(string[] testArguments) {
        tkfx_logging_tests_exit_code = 0;
        try {
            return logging_tests.tkfx_logging_tests_body_main(testArguments);
        } catch (logging_tests.LoggingTestsExitException ex) {
            tkfx_logging_tests_exit_code = ex.ExitCode;
            return tkfx_logging_tests_exit_code;
        }
    }
}
