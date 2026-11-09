/* Managed adapter port for bitstream_tests.c. */

#nullable enable

namespace TKFaxEngineTest.Modem.Test;

public static class bitstream_tests_adapter {
    private static int tkfx_bitstream_tests_exit_code;

    public static void tkfx_bitstream_tests_exit(int code) {
        tkfx_bitstream_tests_exit_code = code;
        throw new bitstream_tests.BitstreamTestsExitException(code);
    }

    public static int tkfx_bitstream_tests_main(string[] testArguments) {
        tkfx_bitstream_tests_exit_code = 0;

        try {
            return bitstream_tests.tkfx_bitstream_tests_body_main(testArguments);
        } catch (bitstream_tests.BitstreamTestsExitException ex) {
            tkfx_bitstream_tests_exit_code = ex.ExitCode;
            return tkfx_bitstream_tests_exit_code;
        }
    }
}
