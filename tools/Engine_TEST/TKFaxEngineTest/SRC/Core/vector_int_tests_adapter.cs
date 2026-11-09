/* Managed C# adapter for vector_int_tests.c. */

namespace TKFaxEngineTest.Core.Test;

public static class vector_int_tests_adapter {
    public static int tkfx_vector_int_tests_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        try {
            return vector_int_tests.body_main(testArguments);
        } catch (VectorIntTestsExitException exception) {
            return exception.ExitCode;
        }
    }
}
