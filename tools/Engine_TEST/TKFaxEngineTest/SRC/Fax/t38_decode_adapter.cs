/*
 * Managed C# counterpart of t38_decode_adapter.c.
 * The C setjmp/longjmp exit boundary maps to T38DecodeExitException.
 */

#nullable enable

namespace TKFaxEngineTest.Fax.Test;

public static class t38_decode_adapter {
    public static int tkfx_t38_decode_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        try {
            return t38_decode.tkfx_t38_decode_body_main(testArguments);
        } catch (t38_decode.T38DecodeExitException ex) {
            return ex.ExitCode;
        }
    }
}
