/* Managed C# port of modem_connect_tones_tests_adapter.c. */
#nullable enable

namespace TKFaxEngineTest.Modem.Test;

public static class modem_connect_tones_tests_adapter {
    public static int tkfx_modem_connect_tones_tests_main(string[] testArguments) {
        try {
            return modem_connect_tones_tests.tkfx_modem_connect_tones_tests_body_main(testArguments);
        } catch (modem_connect_tones_tests.ModemConnectTonesTestsExitException ex) {
            return ex.ExitCode;
        }
    }
}
