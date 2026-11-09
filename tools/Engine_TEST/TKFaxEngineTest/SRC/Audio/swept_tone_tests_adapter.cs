/* Managed C# port of swept_tone_tests_adapter.c. */

#nullable enable

namespace TKFaxEngineTest.Audio.Test;

public static class SweptToneTestsAdapter {
    public static int tkfx_swept_tone_tests_main(string[] testArguments) {
        try {
            return SweptToneTests.tkfx_swept_tone_tests_body_main(testArguments);
        } catch (SweptToneTests.ExitException exception) {
            return exception.ExitCode;
        }
    }
}
