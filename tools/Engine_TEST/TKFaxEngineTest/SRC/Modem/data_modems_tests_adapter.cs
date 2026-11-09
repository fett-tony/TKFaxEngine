/* Managed adapter port for data_modems_tests.c. */

#nullable enable

namespace TKFaxEngineTest.Modem.Test;

public static class data_modems_tests_adapter {
    private static int
        tkfx_data_modems_tests_exit_code;

    public static void
        tkfx_data_modems_tests_exit(int code) {
        tkfx_data_modems_tests_exit_code = code;

        throw new data_modems_tests
            .DataModemsTestsExitException(code);
    }

    public static int tkfx_data_modems_tests_main(string[] testArguments) {
        tkfx_data_modems_tests_exit_code = 0;

        try {
            return data_modems_tests
                .tkfx_data_modems_tests_body_main(
                    testArguments);
        } catch (
              data_modems_tests
                  .DataModemsTestsExitException ex) {
            tkfx_data_modems_tests_exit_code =
                ex.ExitCode;

            return
                tkfx_data_modems_tests_exit_code;
        }
    }
}
