/* Managed adapter port for complex_tests.c. */

#nullable enable

namespace TKFaxEngineTest.Core.Test;

public static class complex_tests_adapter {
    private static int tkfx_complex_tests_exit_code;

    public static void tkfx_complex_tests_exit(int code) {
        tkfx_complex_tests_exit_code = code;
        throw new complex_tests.ComplexTestsExitException(code);
    }

    public static int tkfx_complex_tests_main(string[] testArguments) {
        tkfx_complex_tests_exit_code = 0;

        try {
            return complex_tests.tkfx_complex_tests_body_main(testArguments);
        } catch (complex_tests.ComplexTestsExitException ex) {
            tkfx_complex_tests_exit_code = ex.ExitCode;
            return tkfx_complex_tests_exit_code;
        }
    }
}
