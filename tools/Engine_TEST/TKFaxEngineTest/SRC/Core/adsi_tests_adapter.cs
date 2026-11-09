/* Auto-generated all-in-one adapter port for adsi_tests.c. */

#nullable enable

namespace TKFaxEngineTest.Core.Test;

public static class adsi_tests_adapter {
    private static int tkfx_adsi_tests_exit_code;

    public static void tkfx_adsi_tests_exit(int code) {
        tkfx_adsi_tests_exit_code = code;
        throw new adsi_tests.AdsiTestExitException(code);
    }

    public static int tkfx_adsi_tests_main(string[] testArguments) {
        tkfx_adsi_tests_exit_code = 0;
        try {
            return adsi_tests.tkfx_adsi_tests_body_main(testArguments);
        } catch (adsi_tests.AdsiTestExitException ex) {
            tkfx_adsi_tests_exit_code = ex.ExitCode;
            return tkfx_adsi_tests_exit_code;
        }
    }
}
