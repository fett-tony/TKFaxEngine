/* Auto-generated all-in-one adapter port for alloc_tests.c. */

#nullable enable

using static TKFaxEngine.AllocApi;

namespace TKFaxEngineTest.Core.Test;

public static class alloc_tests_adapter {
    private static int tkfx_alloc_tests_exit_code;

    public static void tkfx_alloc_tests_exit(int code) {
        tkfx_alloc_tests_exit_code = code;
        throw new alloc_tests.AllocTestExitException(code);
    }

    private static void tkfx_alloc_tests_restore_allocators() {
        _ = span_mem_allocators(null, null, null, null, null);
    }

    public static int tkfx_alloc_tests_main(string[] testArguments) {
        int result;
        tkfx_alloc_tests_exit_code = 0;

        try {
            result = alloc_tests.tkfx_alloc_tests_body_main(testArguments);
        } catch (alloc_tests.AllocTestExitException ex) {
            tkfx_alloc_tests_exit_code = ex.ExitCode;
            result = tkfx_alloc_tests_exit_code;
        } finally {
            tkfx_alloc_tests_restore_allocators();
        }

        return result;
    }
}
