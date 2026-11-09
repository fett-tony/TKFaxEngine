/* Auto-generated all-in-one adapter port for async_tests.c. */

#nullable enable

namespace TKFaxEngineTest.Core.Test;

public static class async_tests_adapter {
    private static int tkfx_async_tests_exit_code;

    public static void tkfx_async_tests_exit(int code) {
        tkfx_async_tests_exit_code = code;
        throw new async_tests.AsyncTestExitException(code);
    }

    public static int tkfx_async_tests_main(string[] testArguments) {
        tkfx_async_tests_exit_code = 0;

        try {
            return async_tests.tkfx_async_tests_body_main(testArguments);
        } catch (async_tests.AsyncTestExitException ex) {
            tkfx_async_tests_exit_code = ex.ExitCode;
            return tkfx_async_tests_exit_code;
        }
    }
}
