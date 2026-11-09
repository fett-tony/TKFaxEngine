/* Managed adapter port for g722_tests_adapter.c. */
#nullable enable

namespace TKFaxEngineTest.Audio.Test;

public static class g722_tests_adapter {
    private static int tkfx_g722_tests_exit_code;

    public static void tkfx_g722_tests_exit(int code) {
        tkfx_g722_tests_exit_code = code;
        throw new g722_tests.G722TestsExitException(code);
    }

    public static int tkfx_g722_tests_main(string[] testArguments) {
        tkfx_g722_tests_exit_code = 0;
        try { return g722_tests.tkfx_g722_tests_body_main(testArguments); } catch (g722_tests.G722TestsExitException caught) {
            tkfx_g722_tests_exit_code = caught.ExitCode;
            return tkfx_g722_tests_exit_code;
        }
    }
}
