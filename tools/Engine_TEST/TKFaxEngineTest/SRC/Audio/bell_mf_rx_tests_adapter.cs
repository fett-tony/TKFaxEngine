/*
 * Auto-generated all-in-one adapter port for bell_mf_rx_tests.c.
 */

#nullable enable

namespace TKFaxEngineTest.Audio.Test;

public static class bell_mf_rx_tests_adapter {
    private static int tkfx_bell_mf_rx_tests_exit_code;

    public static void tkfx_bell_mf_rx_tests_exit(int code) {
        tkfx_bell_mf_rx_tests_exit_code = code;
        throw new bell_mf_rx_tests.BellMfRxTestExitException(code);
    }

    public static int tkfx_bell_mf_rx_tests_main(string[] testArguments) {
        tkfx_bell_mf_rx_tests_exit_code = 0;

        try {
            return bell_mf_rx_tests.tkfx_bell_mf_rx_tests_body_main(testArguments);
        } catch (bell_mf_rx_tests.BellMfRxTestExitException ex) {
            tkfx_bell_mf_rx_tests_exit_code = ex.ExitCode;
            return tkfx_bell_mf_rx_tests_exit_code;
        }
    }
}
