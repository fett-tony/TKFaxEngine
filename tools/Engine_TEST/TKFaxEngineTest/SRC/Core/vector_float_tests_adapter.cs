/* Managed C# adapter for vector_float_tests.c. */

namespace TKFaxEngineTest.Core.Test;

public static class vector_float_tests_adapter {
    public static int tkfx_vector_float_tests_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        try {
            return vector_float_tests.body_main(testArguments);
        } catch (VectorFloatTestsExitException exception) {
            return exception.ExitCode;
        }
    }
}
