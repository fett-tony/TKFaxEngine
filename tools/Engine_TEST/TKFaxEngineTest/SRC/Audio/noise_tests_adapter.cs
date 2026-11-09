/* Managed C# port of noise_tests_adapter.c. */

#nullable enable

using TKFaxEngineTest;

namespace TKFaxEngineTest.Audio.Test;

public static class NoiseTestsAdapter {
    public static int tkfx_noise_tests_main(string[] testArguments) {
        try {
            return NoiseTests.tkfx_noise_tests_body_main(testArguments);
        } catch (TestProcessExitException exception) {
            return exception.ExitCode;
        }
    }
}
