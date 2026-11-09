/* Fresh managed C# port of sprt_tests_adapter.c. */

#nullable enable

namespace TKFaxEngineTest.Modem.Test;

public static class SprtTestsAdapter {
    public static int tkfx_sprt_tests_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);
        string[] args = testArguments;

        ArgumentNullException.ThrowIfNull(args);
        return sprt_tests_source.tkfx_sprt_tests_body_main(args);
    }
}
