/* Managed C# port of tone_detect_tests_adapter.c. */

#nullable enable

namespace TKFaxEngineTest.Audio.Test;

public static class ToneDetectTestsAdapter {
    public static int tkfx_tone_detect_tests_main(string[] testArguments) {
        try {
            return ToneDetectTests.tkfx_tone_detect_tests_body_main(testArguments);
        } catch (ToneDetectTests.ExitException exception) {
            return exception.ExitCode;
        }
    }
}
