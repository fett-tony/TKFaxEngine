/* Managed C# port of tsb85_tests_adapter.c. */

#nullable enable

namespace TKFaxEngineTest.Fax.Test;

public static class Tsb85TestsAdapter {
    public static int tkfx_tsb85_tests_main(string[] testArguments) {
        FaxTestRuntimeBootstrap.EnsureConfigured();
        try {
            return Tsb85Tests.tkfx_tsb85_tests_body_main(testArguments);
        } catch (Tsb85Tests.ExitException exception) {
            return exception.ExitCode;
        }
    }
}
