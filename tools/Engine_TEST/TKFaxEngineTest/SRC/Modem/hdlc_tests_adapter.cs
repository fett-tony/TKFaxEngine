/* Managed C# port of hdlc_tests_adapter.c. */

#nullable enable

using TKFaxEngineTest;

namespace TKFaxEngineTest.Modem.Test;

public static class HdlcTestsAdapter {
    public static int tkfx_hdlc_tests_main(string[] testArguments) {
        try {
            return HdlcTests.tkfx_hdlc_tests_body_main(testArguments);
        } catch (TestProcessExitException exception) {
            return exception.ExitCode;
        }
    }
}
