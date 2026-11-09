/* Fresh managed C# port of sprt_decode_adapter.c. */

#nullable enable

namespace TKFaxEngineTest.Modem.Test;

public static class SprtDecodeAdapter {
    public static int tkfx_sprt_decode_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);
        string[] args = testArguments;

        ArgumentNullException.ThrowIfNull(args);
        return SprtDecodeTest.Run(args);
    }
}
