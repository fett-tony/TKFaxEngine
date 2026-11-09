/*
 * Fresh managed adapter port for fax_decode_adapter.c.
 */

#nullable enable

namespace TKFaxEngineTest.Fax.Test;

public static class fax_decode_adapter {
    private static int tkfx_fax_decode_exit_code;

    public static void tkfx_fax_decode_exit(int code) {
        tkfx_fax_decode_exit_code = code;
        throw new fax_decode.FaxDecodeExitException(code);
    }

    public static int tkfx_fax_decode_main(string[] testArguments) {
        tkfx_fax_decode_exit_code = 0;
        try {
            return fax_decode.tkfx_fax_decode_body_main(testArguments);
        } catch (fax_decode.FaxDecodeExitException ex) {
            tkfx_fax_decode_exit_code = ex.ExitCode;
            return tkfx_fax_decode_exit_code;
        }
    }
}
