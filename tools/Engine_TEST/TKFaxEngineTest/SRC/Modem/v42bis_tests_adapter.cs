/* Managed C# adapter for v42bis_tests_adapter.c. */
#nullable enable

namespace TKFaxEngineTest.Modem.Test;

public static class v42bis_tests_adapter {
    public static int tkfx_v42bis_tests_main(string[] testArguments) {
        try {
            return v42bis_tests.tkfx_v42bis_tests_body_main(testArguments);
        } catch (v42bis_tests.V42BisTestsExitException ex) {
            return ex.ExitCode;
        }
    }
}
