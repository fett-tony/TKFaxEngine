/* Managed C# adapter for v80_tests_adapter.c. */
#nullable enable

namespace TKFaxEngineTest.Modem.Test;

public static class v80_tests_adapter {
    public static int tkfx_v80_tests_main(string[] testArguments) =>
        v80_tests.tkfx_v80_tests_body_main(testArguments);
}
