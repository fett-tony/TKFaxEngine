/* Fresh managed C# port of super_tone_rx_tests_adapter.c. */

#nullable enable

namespace TKFaxEngineTest.Audio.Test;

public static class SuperToneRxTestsAdapter {
    public static int tkfx_super_tone_rx_tests_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);
        string[] args = testArguments;

        ArgumentNullException.ThrowIfNull(args);
        return SuperToneRxTests.Run(args);
    }
}
