/* Managed C# port of v29_tests_adapter.c. */
#nullable enable

namespace TKFaxEngineTest.Modem.Test;

public static class v29_tests_adapter {
    private static int tkfx_v29_tests_exit_code;
    public static void tkfx_v29_tests_exit(int code) {
        tkfx_v29_tests_exit_code = code;
        throw new v29_tests.V29TestsExitException(code);
    }
    public static int tkfx_v29_tests_main(string[] testArguments) {
        tkfx_v29_tests_exit_code = 0;
        try { return v29_tests.tkfx_v29_tests_body_main(testArguments); } catch (v29_tests.V29TestsExitException ex) { return tkfx_v29_tests_exit_code = ex.ExitCode; }
    }
}
