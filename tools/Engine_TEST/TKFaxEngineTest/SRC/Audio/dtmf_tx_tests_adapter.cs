/* Managed C# adapter port of dtmf_tx_tests_adapter.c. */

#nullable enable

namespace TKFaxEngineTest.Audio.Test;

public static class DtmfTxTestsAdapter {
    private sealed class ControlledTestExitException : Exception {
        public ControlledTestExitException(int exitCode) {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }

    public static void tkfx_dtmf_tx_tests_exit(int code) {
        throw new ControlledTestExitException(code);
    }

    public static int tkfx_dtmf_tx_tests_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);
        string[] args = testArguments;

        try {
            return DtmfTxTests.BodyMain(args);
        } catch (ControlledTestExitException exception) {
            return exception.ExitCode;
        } catch (Exception exception) {
            Console.Error.WriteLine("Unhandled dtmf_tx_tests exception: {0}", exception);
            return 2;
        }
    }
}
