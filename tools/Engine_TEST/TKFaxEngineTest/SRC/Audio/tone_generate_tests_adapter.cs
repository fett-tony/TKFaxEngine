/* Managed C# port of tone_generate_tests_adapter.c. */

#nullable enable

namespace TKFaxEngineTest.Audio.Test;

public static class ToneGenerateTestsAdapter {
    public static int tkfx_tone_generate_tests_main(string[] testArguments) {
        try {
            return ToneGenerateTests.tkfx_tone_generate_tests_body_main(testArguments);
        } catch (ToneGenerateTests.ExitException exception) {
            return exception.ExitCode;
        }
    }
}
