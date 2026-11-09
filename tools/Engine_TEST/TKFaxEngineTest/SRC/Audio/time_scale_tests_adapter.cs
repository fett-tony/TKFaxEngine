/* Managed C# port of time_scale_tests_adapter.c. */

#nullable enable

namespace TKFaxEngineTest.Audio.Test;

public static class TimeScaleTestsAdapter {
    public static int tkfx_time_scale_tests_main(string[] testArguments) {
        try {
            return TimeScaleTests.tkfx_time_scale_tests_body_main(testArguments);
        } catch (TimeScaleTests.ExitException exception) {
            return exception.ExitCode;
        }
    }
}
