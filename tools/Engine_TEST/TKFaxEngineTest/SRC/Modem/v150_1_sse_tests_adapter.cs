/* Managed C# adapter for v150_1_sse_tests_adapter.c. */
#nullable enable

namespace TKFaxEngineTest.Modem.Test;

public static class v150_1_sse_tests_adapter {
    public const string BuildRevision = "20260712_1040";

    public static int tkfx_v150_1_sse_tests_main(string[] testArguments) {
        try {
            return v150_1_sse_tests_source.tkfx_v150_1_sse_tests_body_main(testArguments);
        } catch (v150_1_sse_tests_source.V1501SseTestsExitException ex) {
            return ex.ExitCode;
        }
    }
}
