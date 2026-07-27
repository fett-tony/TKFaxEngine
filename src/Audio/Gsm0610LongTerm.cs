/* Managed port of gsm0610_long_term.c. LGPL-2.1. */

#nullable enable

namespace TKFaxEngine.Audio;

internal static partial class Gsm0610Codec {
    private static readonly short[] GsmDlb = [6554, 16384, 26214, 32767];
    private static readonly short[] GsmQlb = [3277, 11469, 21299, 32767];

    private static int MaxCrossCorrelation(
        short[] signal,
        int signalOffset,
        short[] history,
        int historyOffset,
        out short index) {
        int maximum = 0;
        int best = 40;

        for (int lag = 40; lag <= 120; lag++) {
            long sum = 0;
            int baseIndex = historyOffset - lag;
            for (int i = 0; i < 40; i++)
                sum += (long)signal[signalOffset + i] * history[baseIndex + i];
            int result = Sat32(sum);
            if (result > maximum) {
                maximum = result;
                best = lag;
            }
        }

        index = (short)best;
        return maximum;
    }

    private static short EvaluateLtpParameters(
        short[] signal,
        int signalOffset,
        short[] history,
        int historyOffset,
        out short nc) {
        short dmax = 0;
        for (int i = 0; i < 40; i++) {
            short value = Abs(signal[signalOffset + i]);
            if (value > dmax) dmax = value;
        }

        short temp = dmax == 0 ? (short)0 : Norm(dmax << 16);
        short scale = temp > 6 ? (short)0 : (short)(6 - temp);

        short[] work = new short[40];
        for (int i = 0; i < 40; i++)
            work[i] = (short)(signal[signalOffset + i] >> scale);

        int lMax = MaxCrossCorrelation(work, 0, history, historyOffset, out nc);
        lMax = Sat32((long)lMax << 1);
        lMax >>= 6 - scale;

        long power = 0;
        for (int i = 0; i < 40; i++) {
            int value = history[historyOffset + i - nc] >> 3;
            power += (long)value * value;
        }
        int lPower = Sat32(power << 1);

        if (lMax <= 0) return 0;
        if (lMax >= lPower) return 3;

        short norm = Norm(lPower);
        short r = (short)(((long)lMax << norm) >> 16);
        short s = (short)(((long)lPower << norm) >> 16);

        short bc;
        for (bc = 0; bc <= 2; bc++) {
            if (r <= Mult(s, GsmDlb[bc])) break;
        }
        return bc;
    }

    private static void LongTermAnalysisFiltering(
        short bc,
        short nc,
        short[] history,
        int historyOffset,
        short[] signal,
        int signalOffset,
        short[] dpp,
        int dppOffset,
        short[] residual,
        int residualOffset) {
        for (int i = 0; i < 40; i++) {
            short predicted = MultR(GsmQlb[bc], history[historyOffset + i - nc]);
            dpp[dppOffset + i] = predicted;
            residual[residualOffset + i] = Sub(signal[signalOffset + i], predicted);
        }
    }

    internal static void LongTermPredictor(
        Gsm0610State state,
        short[] signal,
        int signalOffset,
        short[] history,
        int historyOffset,
        short[] residual,
        int residualOffset,
        short[] dpp,
        int dppOffset,
        out short nc,
        out short bc) {
        bc = EvaluateLtpParameters(signal, signalOffset, history, historyOffset, out nc);
        LongTermAnalysisFiltering(
            bc, nc,
            history, historyOffset,
            signal, signalOffset,
            dpp, dppOffset,
            residual, residualOffset);
    }

    internal static void LongTermSynthesisFiltering(
        Gsm0610State state,
        short ncr,
        short bcr,
        short[] erp,
        short[] drp,
        int drpOffset) {
        short nr = ncr < 40 || ncr > 120 ? state.Nrp : ncr;
        state.Nrp = nr;
        short brp = GsmQlb[bcr & 3];

        for (int i = 0; i < 40; i++) {
            short drpp = MultR(brp, drp[drpOffset + i - nr]);
            drp[drpOffset + i] = Add(erp[i], drpp);
        }

        Array.Copy(drp, drpOffset - 80, drp, drpOffset - 120, 120);
    }

    internal static void gsm0610_long_term_predictor(
        Gsm0610State state,
        short[] signal,
        int signalOffset,
        short[] history,
        int historyOffset,
        short[] residual,
        int residualOffset,
        short[] dpp,
        int dppOffset,
        out short nc,
        out short bc) =>
        LongTermPredictor(
            state, signal, signalOffset, history, historyOffset,
            residual, residualOffset, dpp, dppOffset, out nc, out bc);

    internal static void gsm0610_long_term_synthesis_filtering(
        Gsm0610State state,
        short ncr,
        short bcr,
        short[] erp,
        short[] drp,
        int drpOffset) =>
        LongTermSynthesisFiltering(state, ncr, bcr, erp, drp, drpOffset);
}
