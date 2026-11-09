/*
 * Auto-generated all-in-one adapter port for at_interpreter_tests.c.
 */

#nullable enable

namespace TKFaxEngineTest.Core.Test;

public static class at_interpreter_tests_adapter {
    private static int tkfx_at_interpreter_tests_exit_code;

    public static void tkfx_at_interpreter_tests_exit(int code) {
        tkfx_at_interpreter_tests_exit_code = code;
        throw new at_interpreter_tests.AtInterpreterTestExitException(code);
    }

    public static int tkfx_at_interpreter_tests_main(string[] testArguments) {
        tkfx_at_interpreter_tests_exit_code = 0;

        try {
            return at_interpreter_tests.tkfx_at_interpreter_tests_body_main(testArguments);
        } catch (at_interpreter_tests.AtInterpreterTestExitException ex) {
            tkfx_at_interpreter_tests_exit_code = ex.ExitCode;
            return tkfx_at_interpreter_tests_exit_code;
        }
    }
}
