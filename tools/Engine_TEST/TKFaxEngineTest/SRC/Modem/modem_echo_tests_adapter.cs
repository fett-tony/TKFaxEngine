/* Managed C# adapter for modem_echo_tests.c. */
#nullable enable

namespace TKFaxEngineTest.Modem.Test;

public static class modem_echo_tests_adapter {
    public static int tkfx_modem_echo_tests_main(string[] testArguments) {
        try {
            return modem_echo_tests.tkfx_modem_echo_tests_body_main(testArguments);
        } catch (modem_echo_tests.ModemEchoTestsExitException ex) {
            return ex.ExitCode;
        }
    }
}
