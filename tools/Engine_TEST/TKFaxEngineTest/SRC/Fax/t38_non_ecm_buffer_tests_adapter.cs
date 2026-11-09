/* Managed C# adapter for t38_non_ecm_buffer_tests_adapter.c. */
#nullable enable

namespace TKFaxEngineTest.Fax.Test;

public static class t38_non_ecm_buffer_tests_adapter {
    public static int tkfx_t38_non_ecm_buffer_tests_main(string[] testArguments) {
        try {
            return t38_non_ecm_buffer_tests.tkfx_t38_non_ecm_buffer_tests_body_main(testArguments);
        } catch (t38_non_ecm_buffer_tests.T38NonEcmBufferTestsExitException ex) {
            return ex.ExitCode;
        }
    }
}
