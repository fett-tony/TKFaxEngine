/*
 * g1050_tests_adapter.cs
 * Managed equivalent of the native all-in-one adapter.
 */

#nullable enable

namespace TKFaxEngineTest.Network.Test;

public static class G1050TestsAdapter {
    public static int tkfx_g1050_tests_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);
        string[] args = testArguments;

        return G1050Tests.Main(args);
    }
}
