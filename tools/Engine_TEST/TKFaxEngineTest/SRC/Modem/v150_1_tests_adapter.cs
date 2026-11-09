/* Managed C# adapter for v150_1_tests_adapter.c. */
#nullable enable

namespace TKFaxEngineTest.Modem.Test;

public static class v150_1_tests_adapter {
    public static int tkfx_v150_1_tests_main(string[] testArguments) {
        try {
            return v150_1_tests_source.tkfx_v150_1_tests_body_main(testArguments);
        } catch (v150_1_tests_source.V1501TestsExitException ex) {
            return ex.ExitCode;
        }
    }
}
