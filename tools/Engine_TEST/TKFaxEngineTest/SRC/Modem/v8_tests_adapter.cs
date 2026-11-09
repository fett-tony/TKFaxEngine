/* Fresh managed port of v8_tests_adapter.c. */
#nullable enable

namespace TKFaxEngineTest.Modem.Test;

public static class V8TestsAdapter {
    public static int tkfx_v8_tests_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);
        string[] arguments = testArguments;
        return V8Tests.Main(arguments);
    }
}
