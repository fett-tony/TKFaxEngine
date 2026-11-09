/* Managed C# port of t85_tests_adapter.c. */

#nullable enable

namespace TKFaxEngineTest.Image.Test;

public static class T85TestsAdapter {
    public static int tkfx_t85_tests_main(string[] testArguments) {
        try {
            return T85Tests.tkfx_t85_tests_body_main(testArguments);
        } catch (T85Tests.ExitException exception) {
            return exception.ExitCode;
        }
    }
}
