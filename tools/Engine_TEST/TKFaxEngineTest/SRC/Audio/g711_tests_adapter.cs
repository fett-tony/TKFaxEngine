/* Managed adapter port for g711_tests_adapter.c. */
#nullable enable

namespace TKFaxEngineTest.Audio.Test;

public static class g711_tests_adapter {
    private static int tkfx_g711_tests_exit_code;

    public static void tkfx_g711_tests_exit(int code) {
        tkfx_g711_tests_exit_code = code;
        throw new g711_tests.G711TestsExitException(code);
    }

    public static int tkfx_g711_tests_main(string[] testArguments) {
        tkfx_g711_tests_exit_code = 0;
        try { return g711_tests.tkfx_g711_tests_body_main(testArguments); } catch (g711_tests.G711TestsExitException caught) {
            tkfx_g711_tests_exit_code = caught.ExitCode;
            return tkfx_g711_tests_exit_code;
        }
    }
}
