/* Managed C# adapter for t38_core_tests_adapter.c. */
#nullable enable

namespace TKFaxEngineTest.Fax.Test;

public static class t38_core_tests_adapter {
    public static int tkfx_t38_core_tests_main(string[] testArguments) {
        try {
            return t38_core_tests.tkfx_t38_core_tests_body_main(testArguments);
        } catch (t38_core_tests.T38CoreTestsExitException ex) {
            return ex.ExitCode;
        }
    }
}
