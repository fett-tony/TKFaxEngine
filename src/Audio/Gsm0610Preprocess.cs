/* Managed port of gsm0610_preprocess.c. LGPL-2.1. */

#nullable enable

namespace TKFaxEngine.Audio;

internal static partial class Gsm0610Codec {
    internal static void Preprocess(
        Gsm0610State state,
        short[] amplitude,
        int amplitudeOffset,
        short[] output) {
        short z1 = state.Z1;
        int lz2 = state.LZ2;
        short mp = state.Mp;

        for (int k = 0; k < FrameLength; k++) {
            short so = (short)((amplitude[amplitudeOffset + k] >> 1) & ~3);
            short s1 = (short)(so - z1);
            z1 = so;

            int ls2 = s1 << 15;
            lz2 = Sat32((((long)lz2 * 32735) + 0x4000) >> 15);
            lz2 = Add32(lz2, ls2);

            int temp = Add32(lz2, 16384);
            short msp = MultR(mp, -28180);
            mp = (short)(temp >> 15);
            output[k] = Add(mp, msp);
        }

        state.Z1 = z1;
        state.LZ2 = lz2;
        state.Mp = mp;
    }

    internal static void gsm0610_preprocess(
        Gsm0610State state,
        short[] amplitude,
        short[] output) =>
        Preprocess(state, amplitude, 0, output);
}
