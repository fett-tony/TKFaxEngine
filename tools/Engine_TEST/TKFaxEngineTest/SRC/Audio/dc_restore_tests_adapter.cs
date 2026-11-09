/* Managed adapter port for dc_restore_tests.c. */

#nullable enable

namespace TKFaxEngineTest.Audio.Test;

public static class dc_restore_tests_adapter {
    private static int
        tkfx_dc_restore_tests_exit_code;

    public static void
        tkfx_dc_restore_tests_exit(int code) {
        tkfx_dc_restore_tests_exit_code = code;

        throw new dc_restore_tests
            .DcRestoreTestsExitException(code);
    }

    public static int tkfx_dc_restore_tests_main(string[] testArguments) {
        tkfx_dc_restore_tests_exit_code = 0;

        try {
            return dc_restore_tests
                .tkfx_dc_restore_tests_body_main(
                    testArguments);
        } catch (
              dc_restore_tests
                  .DcRestoreTestsExitException ex) {
            tkfx_dc_restore_tests_exit_code =
                ex.ExitCode;

            return
                tkfx_dc_restore_tests_exit_code;
        }
    }
}
