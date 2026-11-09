/* Managed C# adapter for v42_tests_adapter.c. */
#nullable enable

namespace TKFaxEngineTest.Modem.Test;

public static class v42_tests_adapter {
    public static int tkfx_v42_tests_main(string[] testArguments) {
        try {
            return v42_tests.tkfx_v42_tests_body_main(testArguments);
        } catch (v42_tests.V42TestsExitException ex) {
            return ex.ExitCode;
        }
    }
}
