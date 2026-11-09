/*
 * Fresh managed adapter port for lpc10_tests_adapter.c.
 */

#nullable enable

namespace TKFaxEngineTest.Audio.Test;

public static class lpc10_tests_adapter {
    private static int tkfx_lpc10_tests_exit_code;

    public static void tkfx_lpc10_tests_exit(int code) {
        tkfx_lpc10_tests_exit_code = code;
        throw new lpc10_tests.Lpc10TestsExitException(code);
    }

    public static int tkfx_lpc10_tests_main(string[] testArguments) {
        tkfx_lpc10_tests_exit_code = 0;
        try {
            return lpc10_tests.tkfx_lpc10_tests_body_main(testArguments);
        } catch (lpc10_tests.Lpc10TestsExitException ex) {
            tkfx_lpc10_tests_exit_code = ex.ExitCode;
            return tkfx_lpc10_tests_exit_code;
        }
    }
}
