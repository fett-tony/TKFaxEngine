/*
 * Fresh managed adapter port for fax_tests_adapter.c.
 */

#nullable enable

namespace TKFaxEngineTest.Fax.Test;

public static class fax_tests_adapter {
    private static int tkfx_fax_tests_exit_code;

    public static void tkfx_fax_tests_exit(int code) {
        tkfx_fax_tests_exit_code = code;
        throw new fax_tests.FaxTestsExitException(code);
    }

    public static int tkfx_fax_tests_main(string[] testArguments) {
        FaxTestRuntimeBootstrap.EnsureConfigured();
        tkfx_fax_tests_exit_code = 0;
        try {
            return fax_tests.tkfx_fax_tests_body_main(testArguments);
        } catch (fax_tests.FaxTestsExitException ex) {
            tkfx_fax_tests_exit_code = ex.ExitCode;
            return tkfx_fax_tests_exit_code;
        }
    }
}
