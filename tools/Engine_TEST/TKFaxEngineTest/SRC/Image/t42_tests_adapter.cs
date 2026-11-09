/* Managed all-in-one adapter for t42_tests.cs. */

#nullable enable

namespace TKFaxEngineTest.Image.Test;

public static class t42_tests_adapter {
    public static int tkfx_t42_tests_main(string[] testArguments) {
        try {
            testArguments ??= Array.Empty<string>();
            return t42_tests.tkfx_t42_tests_body_main(testArguments);
        } catch (Exception exception) {
            Console.Error.WriteLine(exception);
            return 2;
        }
    }

    public static int Run(string[] args) {
        ArgumentNullException.ThrowIfNull(args);
        return tkfx_t42_tests_main(args);
    }
}
