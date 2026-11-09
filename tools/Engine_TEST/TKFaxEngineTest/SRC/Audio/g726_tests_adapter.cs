/* Managed adapter port for g726_tests_adapter.c. */
#nullable enable

namespace TKFaxEngineTest.Audio.Test;

public static class g726_tests_adapter {
    private static int tkfx_g726_tests_exit_code;

    public static void tkfx_g726_tests_exit(int code) {
        tkfx_g726_tests_exit_code = code;
        throw new g726_tests.G726TestsExitException(code);
    }

    public static int tkfx_g726_tests_main(string[] testArguments) {
        tkfx_g726_tests_exit_code = 0;
        try { return g726_tests.tkfx_g726_tests_body_main(testArguments); } catch (g726_tests.G726TestsExitException caught) {
            tkfx_g726_tests_exit_code = caught.ExitCode;
            return tkfx_g726_tests_exit_code;
        }
    }
}
