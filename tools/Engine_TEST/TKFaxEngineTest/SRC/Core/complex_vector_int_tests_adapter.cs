/* Managed adapter port for complex_vector_int_tests.c. */

#nullable enable

namespace TKFaxEngineTest.Core.Test;

public static class complex_vector_int_tests_adapter {
    private static int tkfx_complex_vector_int_tests_exit_code;

    public static void tkfx_complex_vector_int_tests_exit(int code) {
        tkfx_complex_vector_int_tests_exit_code = code;
        throw new complex_vector_int_tests.ComplexVectorIntTestsExitException(code);
    }

    public static int tkfx_complex_vector_int_tests_main(string[] testArguments) {
        tkfx_complex_vector_int_tests_exit_code = 0;

        try {
            return complex_vector_int_tests.tkfx_complex_vector_int_tests_body_main(testArguments);
        } catch (complex_vector_int_tests.ComplexVectorIntTestsExitException ex) {
            tkfx_complex_vector_int_tests_exit_code = ex.ExitCode;
            return tkfx_complex_vector_int_tests_exit_code;
        }
    }
}
