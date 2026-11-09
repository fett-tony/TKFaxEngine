/* Managed C# port of t4_tests_adapter.c. */

#nullable enable

namespace TKFaxEngineTest.Image.Test;

public static class T4TestsAdapter {
    public static int tkfx_t4_tests_main(string[] testArguments) {
        try {
            return T4Tests.tkfx_t4_tests_body_main(testArguments);
        } catch (T4Tests.ExitException exception) {
            return exception.ExitCode;
        }
    }
}
