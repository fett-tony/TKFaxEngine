/* Managed C# adapter port of dummy_modems_tests_adapter.c. */

#nullable enable

namespace TKFaxEngineTest.Modem.Test;

public static class DummyModemsTestsAdapter {
    private sealed class ControlledTestExitException : Exception {
        public ControlledTestExitException(int exitCode) {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }

    public static void tkfx_dummy_modems_tests_exit(int code) {
        throw new ControlledTestExitException(code);
    }

    public static int tkfx_dummy_modems_tests_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);
        string[] args = testArguments;

        try {
            return dummy_modems_tests.tkfx_dummy_modems_tests_body_main(args);
        } catch (ControlledTestExitException exception) {
            return exception.ExitCode;
        } catch (Exception exception) {
            Console.Error.WriteLine("Unhandled dummy_modems_tests exception: {0}", exception);
            return 2;
        }
    }
}
