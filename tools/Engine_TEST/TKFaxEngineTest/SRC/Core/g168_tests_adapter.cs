/* Managed adapter port for g168_tests_adapter.c. */
#nullable enable

namespace TKFaxEngineTest.Core.Test;

public static class g168_tests_adapter {
    private static int tkfx_g168_tests_exit_code;

    public static void tkfx_g168_tests_exit(int code) {
        tkfx_g168_tests_exit_code = code;
        throw new g168_tests.G168TestsExitException(code);
    }

    public static int tkfx_g168_tests_main(string[] testArguments) {
        tkfx_g168_tests_exit_code = 0;
        try { return g168_tests.tkfx_g168_tests_body_main(testArguments); } catch (g168_tests.G168TestsExitException caught) {
            tkfx_g168_tests_exit_code = caught.ExitCode;
            return tkfx_g168_tests_exit_code;
        }
    }
}
