/* Auto-generated all-in-one adapter port for ademco_contactid_tests.c. */

#nullable enable

namespace TKFaxEngineTest.Core.Test;

public static class ademco_contactid_tests_adapter {
    private static int tkfx_ademco_contactid_tests_exit_code;

    public static void tkfx_ademco_contactid_tests_exit(int code) {
        tkfx_ademco_contactid_tests_exit_code = code;
        throw new ademco_contactid_tests.AdemcoTestExitException(code);
    }

    public static int tkfx_ademco_contactid_tests_main(string[] testArguments) {
        tkfx_ademco_contactid_tests_exit_code = 0;
        try {
            return ademco_contactid_tests.tkfx_ademco_contactid_tests_body_main(testArguments);
        } catch (ademco_contactid_tests.AdemcoTestExitException ex) {
            tkfx_ademco_contactid_tests_exit_code = ex.ExitCode;
            return tkfx_ademco_contactid_tests_exit_code;
        }
    }
}
