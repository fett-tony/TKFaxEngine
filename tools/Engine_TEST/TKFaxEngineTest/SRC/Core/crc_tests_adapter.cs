/* Managed adapter port for crc_tests.c. */

#nullable enable

namespace TKFaxEngineTest.Core.Test;

public static class crc_tests_adapter {
    private static int tkfx_crc_tests_exit_code;

    public static void tkfx_crc_tests_exit(int code) {
        tkfx_crc_tests_exit_code = code;
        throw new crc_tests.CrcTestsExitException(code);
    }

    public static int tkfx_crc_tests_main(string[] testArguments) {
        tkfx_crc_tests_exit_code = 0;

        try {
            return crc_tests.tkfx_crc_tests_body_main(testArguments);
        } catch (crc_tests.CrcTestsExitException ex) {
            tkfx_crc_tests_exit_code = ex.ExitCode;
            return tkfx_crc_tests_exit_code;
        }
    }
}
