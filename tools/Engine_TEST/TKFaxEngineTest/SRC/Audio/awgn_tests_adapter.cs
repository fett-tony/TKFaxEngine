/*
 * Auto-generated all-in-one adapter port for awgn_tests.c.
 *
 * The native setjmp/longjmp exit conversion is represented by a managed
 * test exception and a returned process-style exit code.
 */

#nullable enable

namespace TKFaxEngineTest.Audio.Test;

public static class awgn_tests_adapter {
    private static int tkfx_awgn_tests_exit_code;

    public static void tkfx_awgn_tests_exit(int code) {
        tkfx_awgn_tests_exit_code = code;
        throw new awgn_tests.AwgnTestExitException(code);
    }

    public static int tkfx_awgn_tests_main(string[] testArguments) {
        tkfx_awgn_tests_exit_code = 0;

        try {
            return awgn_tests.tkfx_awgn_tests_body_main(testArguments);
        } catch (awgn_tests.AwgnTestExitException ex) {
            tkfx_awgn_tests_exit_code = ex.ExitCode;
            return tkfx_awgn_tests_exit_code;
        }
    }
}
