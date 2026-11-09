/* Managed C# adapter for sig_tone_tests_adapter.c. */
#nullable enable

namespace TKFaxEngineTest.Audio.Test;

public static class sig_tone_tests_adapter {
    public static int tkfx_sig_tone_tests_main(string[] testArguments) {
        try {
            return sig_tone_tests.tkfx_sig_tone_tests_body_main(testArguments);
        } catch (sig_tone_tests.SigToneTestsExitException ex) {
            return ex.ExitCode;
        }
    }
}
