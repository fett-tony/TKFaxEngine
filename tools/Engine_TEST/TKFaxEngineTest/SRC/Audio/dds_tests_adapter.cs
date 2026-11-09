/* Managed adapter port for dds_tests.c. */

#nullable enable

namespace TKFaxEngineTest.Audio.Test;

public static class dds_tests_adapter {
    private static int
        tkfx_dds_tests_exit_code;

    public static void
        tkfx_dds_tests_exit(int code) {
        tkfx_dds_tests_exit_code = code;

        throw new dds_tests
            .DdsTestsExitException(code);
    }

    public static int tkfx_dds_tests_main(string[] testArguments) {
        tkfx_dds_tests_exit_code = 0;

        try {
            return dds_tests
                .tkfx_dds_tests_body_main(
                    testArguments);
        } catch (
              dds_tests
                  .DdsTestsExitException ex) {
            tkfx_dds_tests_exit_code =
                ex.ExitCode;

            return tkfx_dds_tests_exit_code;
        }
    }
}
