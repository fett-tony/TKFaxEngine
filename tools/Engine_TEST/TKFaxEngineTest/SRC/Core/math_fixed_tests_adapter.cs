/*
 * Fresh managed adapter port for math_fixed_tests_adapter.c.
 */

#nullable enable

namespace TKFaxEngineTest.Core.Test;

public static class math_fixed_tests_adapter {
    private static int tkfx_math_fixed_tests_exit_code;

    public static void tkfx_math_fixed_tests_exit(int code) {
        tkfx_math_fixed_tests_exit_code = code;
        throw new math_fixed_tests.MathFixedTestsExitException(code);
    }

    public static int tkfx_math_fixed_tests_main(string[] testArguments) {
        tkfx_math_fixed_tests_exit_code = 0;
        try {
            return math_fixed_tests.tkfx_math_fixed_tests_body_main(testArguments);
        } catch (math_fixed_tests.MathFixedTestsExitException ex) {
            tkfx_math_fixed_tests_exit_code = ex.ExitCode;
            return tkfx_math_fixed_tests_exit_code;
        }
    }
}
