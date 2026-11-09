/* Managed port of gsm0610_short_term.c. LGPL-2.1. */

#nullable enable

namespace TKFaxEngine.Audio;

internal static partial class Gsm0610Codec {
    private static void DecodeLogAreaRatios(short[] larc, short[] larpp) {
        DecodeLarStep(larc, 0, larpp, 0, 0, -32, 13107);
        DecodeLarStep(larc, 1, larpp, 1, 0, -32, 13107);
        DecodeLarStep(larc, 2, larpp, 2, 2048, -16, 13107);
        DecodeLarStep(larc, 3, larpp, 3, -2560, -16, 13107);
        DecodeLarStep(larc, 4, larpp, 4, 94, -8, 19223);
        DecodeLarStep(larc, 5, larpp, 5, -1792, -8, 17476);
        DecodeLarStep(larc, 6, larpp, 6, -341, -4, 31454);
        DecodeLarStep(larc, 7, larpp, 7, -1144, -4, 29708);
    }

    private static void DecodeLarStep(
        short[] larc,
        int inputIndex,
        short[] larpp,
        int outputIndex,
        short b,
        short minimum,
        short inverseA) {
        short value = (short)(Add(larc[inputIndex], minimum) << 10);
        value = Sub(value, (short)(b << 1));
        value = MultR(inverseA, value);
        larpp[outputIndex] = Add(value, value);
    }

    private static void Coefficients0To12(short[] previous, short[] current, short[] output) {
        for (int i = 0; i < 8; i++)
            output[i] = Add(Add((short)(previous[i] >> 2), (short)(current[i] >> 2)), (short)(previous[i] >> 1));
    }

    private static void Coefficients13To26(short[] previous, short[] current, short[] output) {
        for (int i = 0; i < 8; i++)
            output[i] = Add((short)(previous[i] >> 1), (short)(current[i] >> 1));
    }

    private static void Coefficients27To39(short[] previous, short[] current, short[] output) {
        for (int i = 0; i < 8; i++)
            output[i] = Add(Add((short)(previous[i] >> 2), (short)(current[i] >> 2)), (short)(current[i] >> 1));
    }

    private static void Coefficients40To159(short[] current, short[] output) =>
        Array.Copy(current, output, 8);

    private static void LarpToReflection(short[] larp) {
        for (int i = 0; i < 8; i++) {
            short value = larp[i];
            bool negative = value < 0;
            short magnitude = value == short.MinValue ? short.MaxValue : (short)Math.Abs(value);

            if (magnitude < 11059)
                magnitude = (short)(magnitude << 1);
            else if (magnitude < 20070)
                magnitude = (short)(magnitude + 11059);
            else
                magnitude = Add((short)(magnitude >> 2), 26112);

            larp[i] = negative ? (short)-magnitude : magnitude;
        }
    }

    private static void ShortTermAnalysisFiltering(
        Gsm0610State state,
        short[] reflection,
        short[] amplitude,
        int offset,
        int count) {
        for (int sampleIndex = 0; sampleIndex < count; sampleIndex++) {
            int di = amplitude[offset + sampleIndex];
            int uOut = di;

            for (int i = 0; i < 8; i++) {
                int ui = state.U[i];
                state.U[i] = (short)uOut;
                int coefficient = reflection[i];
                uOut = Sat16(ui + (((long)coefficient * di + 0x4000) >> 15));
                di = Sat16(di + (((long)coefficient * ui + 0x4000) >> 15));
            }

            amplitude[offset + sampleIndex] = (short)di;
        }
    }

    private static void ShortTermSynthesisFiltering(
        Gsm0610State state,
        short[] reflection,
        short[] residual,
        int residualOffset,
        short[] output,
        int outputOffset,
        int count) {
        for (int sampleIndex = 0; sampleIndex < count; sampleIndex++) {
            short sri = residual[residualOffset + sampleIndex];
            for (int i = 7; i >= 0; i--) {
                short coefficient = reflection[i];
                short delayed = state.V[i];
                short product = MultR(coefficient, delayed);
                sri = Sub(sri, product);
                product = MultR(coefficient, sri);
                state.V[i + 1] = Add(state.V[i], product);
            }
            output[outputOffset + sampleIndex] = state.V[0] = sri;
        }
    }

    internal static void ShortTermAnalysisFilter(
        Gsm0610State state,
        short[] larc,
        short[] amplitude) {
        short[] current = state.LarPp[state.J];
        state.J ^= 1;
        short[] previous = state.LarPp[state.J];
        short[] larp = new short[8];

        DecodeLogAreaRatios(larc, current);

        Coefficients0To12(previous, current, larp);
        LarpToReflection(larp);
        ShortTermAnalysisFiltering(state, larp, amplitude, 0, 13);

        Coefficients13To26(previous, current, larp);
        LarpToReflection(larp);
        ShortTermAnalysisFiltering(state, larp, amplitude, 13, 14);

        Coefficients27To39(previous, current, larp);
        LarpToReflection(larp);
        ShortTermAnalysisFiltering(state, larp, amplitude, 27, 13);

        Coefficients40To159(current, larp);
        LarpToReflection(larp);
        ShortTermAnalysisFiltering(state, larp, amplitude, 40, 120);
    }

    internal static void ShortTermSynthesisFilter(
        Gsm0610State state,
        short[] larc,
        short[] residual,
        short[] output,
        int outputOffset) {
        short[] current = state.LarPp[state.J];
        state.J ^= 1;
        short[] previous = state.LarPp[state.J];
        short[] larp = new short[8];

        DecodeLogAreaRatios(larc, current);

        Coefficients0To12(previous, current, larp);
        LarpToReflection(larp);
        ShortTermSynthesisFiltering(state, larp, residual, 0, output, outputOffset, 13);

        Coefficients13To26(previous, current, larp);
        LarpToReflection(larp);
        ShortTermSynthesisFiltering(state, larp, residual, 13, output, outputOffset + 13, 14);

        Coefficients27To39(previous, current, larp);
        LarpToReflection(larp);
        ShortTermSynthesisFiltering(state, larp, residual, 27, output, outputOffset + 27, 13);

        Coefficients40To159(current, larp);
        LarpToReflection(larp);
        ShortTermSynthesisFiltering(state, larp, residual, 40, output, outputOffset + 40, 120);
    }

    internal static void gsm0610_short_term_analysis_filter(
        Gsm0610State state,
        short[] larc,
        short[] amplitude) =>
        ShortTermAnalysisFilter(state, larc, amplitude);

    internal static void gsm0610_short_term_synthesis_filter(
        Gsm0610State state,
        short[] larc,
        short[] residual,
        short[] output) =>
        ShortTermSynthesisFilter(state, larc, residual, output, 0);
}
