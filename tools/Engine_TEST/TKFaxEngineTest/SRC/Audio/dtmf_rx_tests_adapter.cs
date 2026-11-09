/* Managed C# adapter port of dtmf_rx_tests_adapter.c. */

#nullable enable

namespace TKFaxEngineTest.Audio.Test;

public static class DtmfRxTestsAdapter {
    private sealed class ControlledTestExitException : Exception {
        public ControlledTestExitException(int exitCode) {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }

    public static void tkfx_dtmf_rx_tests_exit(int code) {
        throw new ControlledTestExitException(code);
    }

    public static int tkfx_dtmf_rx_tests_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);
        string[] args = testArguments;

        try {
            return DtmfRxTests.BodyMain(args);
        } catch (ControlledTestExitException exception) {
            return exception.ExitCode;
        } catch (Exception exception) {
            Console.Error.WriteLine("Unhandled dtmf_rx_tests exception: {0}", exception);
            return 2;
        }
    }
}
