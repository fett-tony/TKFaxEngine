/* Managed all-in-one adapter for super_tone_tx_tests.cs. */

#nullable enable

namespace TKFaxEngineTest.Audio.Test;

public static class super_tone_tx_tests_adapter {
    public static int tkfx_super_tone_tx_tests_main(string[] testArguments) {
        try {
            testArguments ??= Array.Empty<string>();
            return super_tone_tx_tests.tkfx_super_tone_tx_tests_body_main(testArguments);
        } catch (Exception exception) {
            Console.Error.WriteLine(exception);
            return 2;
        }
    }

    public static int Run(string[] args) {
        ArgumentNullException.ThrowIfNull(args);
        return tkfx_super_tone_tx_tests_main(args);
    }
}
