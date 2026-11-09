#nullable enable

namespace TKFaxEngineTest.Modem.Test;

public static class v18_tests_adapter
{
    public static int tkfx_v18_tests_main(string[] testArguments)
    {
        ArgumentNullException.ThrowIfNull(testArguments);
        return v18_complete_tests.Main(testArguments);
    }
}
