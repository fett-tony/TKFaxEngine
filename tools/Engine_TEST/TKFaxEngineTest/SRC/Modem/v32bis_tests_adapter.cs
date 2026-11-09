/* Managed C# port of v32bis_tests_adapter.c. */
#nullable enable

namespace TKFaxEngineTest.Modem.Test;

public static class v32bis_tests_adapter {
    private static int tkfx_v32bis_tests_exit_code;
    public static void tkfx_v32bis_tests_exit(int code) {
        tkfx_v32bis_tests_exit_code = code;
        throw new v32bis_tests.V32BisTestsExitException(code);
    }
    public static int tkfx_v32bis_tests_main(string[] testArguments) {
        tkfx_v32bis_tests_exit_code = 0;
        try { return v32bis_tests.tkfx_v32bis_tests_body_main(testArguments); } catch (v32bis_tests.V32BisTestsExitException ex) { return tkfx_v32bis_tests_exit_code = ex.ExitCode; }
    }
}
