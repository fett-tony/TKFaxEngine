/* Managed C# port of line_model_tests_adapter.c. */

#nullable enable

namespace TKFaxEngineTest.Network.Test;

public static class line_model_tests_adapter {
    private static int tkfx_line_model_tests_exit_code;

    public static void tkfx_line_model_tests_exit(int code) {
        tkfx_line_model_tests_exit_code = code;
        throw new line_model_tests.LineModelTestsExitException(code);
    }

    public static int tkfx_line_model_tests_main(string[] testArguments) {
        tkfx_line_model_tests_exit_code = 0;
        try {
            return line_model_tests.tkfx_line_model_tests_body_main(testArguments);
        } catch (line_model_tests.LineModelTestsExitException ex) {
            tkfx_line_model_tests_exit_code = ex.ExitCode;
            return tkfx_line_model_tests_exit_code;
        }
    }
}
