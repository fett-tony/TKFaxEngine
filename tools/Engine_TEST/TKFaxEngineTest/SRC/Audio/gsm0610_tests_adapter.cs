/* Managed C# port of gsm0610_tests_adapter.c. */

#nullable enable

using TKFaxEngineTest;

namespace TKFaxEngineTest.Audio.Test;

public static class Gsm0610TestsAdapter {
    public static int tkfx_gsm0610_tests_main(string[] testArguments) {
        try {
            return Gsm0610Tests.tkfx_gsm0610_tests_body_main(testArguments);
        } catch (TestProcessExitException exception) {
            return exception.ExitCode;
        }
    }
}
