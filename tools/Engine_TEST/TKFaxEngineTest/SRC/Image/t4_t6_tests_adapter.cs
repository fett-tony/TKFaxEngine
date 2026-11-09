/* Managed C# port of t4_t6_tests_adapter.c. */

#nullable enable

namespace TKFaxEngineTest.Image.Test;

public static class T4T6TestsAdapter {
    public static int tkfx_t4_t6_tests_main(string[] testArguments) {
        try {
            return T4T6Tests.tkfx_t4_t6_tests_body_main(testArguments);
        } catch (T4T6Tests.ExitException exception) {
            return exception.ExitCode;
        }
    }
}
