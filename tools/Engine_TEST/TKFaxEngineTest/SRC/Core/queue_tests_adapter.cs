/* Managed C# port of queue_tests_adapter.c. */

#nullable enable

using TKFaxEngineTest;

namespace TKFaxEngineTest.Core.Test;

public static class QueueTestsAdapter {
    public static int tkfx_queue_tests_main(string[] testArguments) {
        try {
            return QueueTests.tkfx_queue_tests_body_main(testArguments);
        } catch (TestProcessExitException exception) {
            return exception.ExitCode;
        }
    }
}
