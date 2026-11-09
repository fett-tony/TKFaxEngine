/* Managed adapter port for echo_tests_adapter.c. */

#nullable enable

namespace TKFaxEngineTest.Audio.Test;

public static class echo_tests_adapter {
    private static int tkfx_echo_tests_exit_code;

    public static void tkfx_echo_tests_exit(int code) {
        tkfx_echo_tests_exit_code = code;
        throw new echo_tests.EchoTestsExitException(code);
    }

    public static int tkfx_echo_tests_main(string[] testArguments) {
        tkfx_echo_tests_exit_code = 0;
        try { return echo_tests.tkfx_echo_tests_body_main(testArguments); } catch (echo_tests.EchoTestsExitException ex) { tkfx_echo_tests_exit_code = ex.ExitCode; return tkfx_echo_tests_exit_code; }
    }
}
