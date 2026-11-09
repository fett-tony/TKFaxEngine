/* Managed adapter port for bit_operations_tests.c. */

#nullable enable

namespace TKFaxEngineTest.Core.Test;

public static class bit_operations_tests_adapter {
    private static int tkfx_bit_operations_tests_exit_code;

    public static void tkfx_bit_operations_tests_exit(int code) {
        tkfx_bit_operations_tests_exit_code = code;
        throw new bit_operations_tests.BitOperationsTestsExitException(code);
    }

    public static int tkfx_bit_operations_tests_main(string[] testArguments) {
        tkfx_bit_operations_tests_exit_code = 0;

        try {
            return bit_operations_tests.tkfx_bit_operations_tests_body_main(testArguments);
        } catch (bit_operations_tests.BitOperationsTestsExitException ex) {
            tkfx_bit_operations_tests_exit_code = ex.ExitCode;
            return tkfx_bit_operations_tests_exit_code;
        }
    }
}
