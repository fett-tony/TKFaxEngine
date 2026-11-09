/* Managed C# port of oki_adpcm_tests_adapter.c. */

#nullable enable

using TKFaxEngineTest;

namespace TKFaxEngineTest.Audio.Test;

public static class OkiAdpcmTestsAdapter {
    public static int tkfx_oki_adpcm_tests_main(string[] testArguments) {
        try {
            return OkiAdpcmTests.tkfx_oki_adpcm_tests_body_main(testArguments);
        } catch (TestProcessExitException exception) {
            return exception.ExitCode;
        }
    }
}
