/* Fresh managed port of v17_tests_adapter.c. */
#nullable enable

namespace TKFaxEngineTest.Modem.Test;

public static class V17TestsAdapter {
    public static int tkfx_v17_tests_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);
        string[] arguments = testArguments;
        return V17Tests.Main(arguments);
    }
}
