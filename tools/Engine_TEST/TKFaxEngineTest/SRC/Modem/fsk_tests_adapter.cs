/* Managed adapter port for fsk_tests_adapter.c. */
#nullable enable

namespace TKFaxEngineTest.Modem.Test;

public static class fsk_tests_adapter {
    private static int tkfx_fsk_tests_exit_code;

    public static void tkfx_fsk_tests_exit(int code) {
        tkfx_fsk_tests_exit_code = code;
        throw new fsk_tests.FskTestsExitException(code);
    }

    public static int tkfx_fsk_tests_main(string[] testArguments) {
        tkfx_fsk_tests_exit_code = 0;
        try { return fsk_tests.tkfx_fsk_tests_body_main(testArguments); } catch (fsk_tests.FskTestsExitException caught) {
            tkfx_fsk_tests_exit_code = caught.ExitCode;
            return tkfx_fsk_tests_exit_code;
        }
    }
}
