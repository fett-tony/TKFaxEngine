/* Managed C# port of v34_tests_adapter.c. */
#nullable enable

namespace TKFaxEngineTest.Modem.Test;

public static class v34_tests_adapter {
    private static int tkfx_v34_tests_exit_code;
    public static void tkfx_v34_tests_exit(int code) {
        tkfx_v34_tests_exit_code = code;
        throw new v34_tests.V34TestsExitException(code);
    }
    public static int tkfx_v34_tests_main(string[] testArguments) {
        tkfx_v34_tests_exit_code = 0;
        try { return v34_tests.tkfx_v34_tests_body_main(testArguments); } catch (v34_tests.V34TestsExitException ex) { return tkfx_v34_tests_exit_code = ex.ExitCode; }
    }
}
