/* Managed C# port of timezone_tests_adapter.c. */

#nullable enable

namespace TKFaxEngineTest.Core.Test;

public static class TimezoneTestsAdapter {
    public static int tkfx_timezone_tests_main(string[] testArguments) {
        try {
            return TimezoneTests.tkfx_timezone_tests_body_main(testArguments);
        } catch (TimezoneTests.ExitException exception) {
            return exception.ExitCode;
        }
    }
}
