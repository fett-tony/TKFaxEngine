/* Managed C# port of pseudo_terminal_tests_adapter.c. */

#nullable enable

using TKFaxEngineTest;

namespace TKFaxEngineTest.Utility.Test;

public static class PseudoTerminalTestsAdapter {
    public static int tkfx_pseudo_terminal_tests_main(string[] testArguments) {
        try {
            return PseudoTerminalTests.tkfx_pseudo_terminal_tests_body_main(testArguments);
        } catch (TestProcessExitException exception) {
            return exception.ExitCode;
        }
    }
}
