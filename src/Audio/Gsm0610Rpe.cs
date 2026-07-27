/* Managed port of gsm0610_rpe.c. LGPL-2.1. */

#nullable enable

namespace TKFaxEngine.Audio;

internal static partial class Gsm0610Codec {
    private static readonly short[] GsmNrfac =
        [29128, 26215, 23832, 21846, 20165, 18725, 17476, 16384];

    private static readonly short[] GsmFac =
        [18431, 20479, 22527, 24575, 26623, 28671, 30719, 32767];

    private static void WeightingFilter(short[] output, short[] residual, int residualOffset) {
        for (int k = 0; k < 40; k++) {
            long result = 4096;
            int baseIndex = residualOffset - 5 + k;
            result += (long)residual[baseIndex] * -134;
            result += (long)residual[baseIndex + 1] * -374;
            result += (long)residual[baseIndex + 3] * 2054;
            result += (long)residual[baseIndex + 4] * 5741;
            result += (long)residual[baseIndex + 5] * 8192;
            result += (long)residual[baseIndex + 6] * 5741;
            result += (long)residual[baseIndex + 7] * 2054;
            result += (long)residual[baseIndex + 9] * -374;
            result += (long)residual[baseIndex + 10] * -134;
            output[k] = Sat16(result >> 13);
        }
    }

    private static void RpeGridSelection(short[] x, short[] xM, out short mc) {
        long bestEnergy = long.MinValue;
        short bestGrid = 0;

        for (short grid = 0; grid < 4; grid++) {
            long energy = 0;
            for (int i = 0; i < 13; i++) {
                int sample = x[grid + 3 * i] >> 2;
                energy += (long)sample * sample;
            }
            energy <<= 1;
            if (energy > bestEnergy) {
                bestEnergy = energy;
                bestGrid = grid;
            }
        }

        mc = bestGrid;
        for (int i = 0; i < 13; i++) xM[i] = x[bestGrid + 3 * i];
    }

    private static void XmaxcToExponentMantissa(short xmaxc, out short exponent, out short mantissa) {
        short exp = 0;
        if (xmaxc > 15) exp = (short)((xmaxc >> 3) - 1);
        short mant = (short)(xmaxc - (exp << 3));

        if (mant == 0) {
            exp = -4;
            mant = 7;
        } else {
            while (mant <= 7) {
                mant = (short)((mant << 1) | 1);
                exp--;
            }
            mant -= 8;
        }

        exponent = exp;
        mantissa = mant;
    }

    private static void ApcmQuantization(
        short[] xM,
        short[] xMc,
        out short mantissa,
        out short exponent,
        out short xmaxc) {
        short xmax = 0;
        for (int i = 0; i < 13; i++) {
            short value = Abs(xM[i]);
            if (value > xmax) xmax = value;
        }

        short exp = 0;
        short temp = (short)(xmax >> 9);
        bool test = false;
        for (int i = 0; i <= 5; i++) {
            test |= temp <= 0;
            temp >>= 1;
            if (!test) exp++;
        }

        short shift = (short)(exp + 5);
        short encodedMaximum = Add((short)(xmax >> shift), (short)(exp << 3));
        XmaxcToExponentMantissa(encodedMaximum, out exp, out short mant);

        short normalization = (short)(6 - exp);
        short inverseMantissa = GsmNrfac[mant];

        for (int i = 0; i < 13; i++) {
            short value = (short)(xM[i] << normalization);
            value = Mult(value, inverseMantissa);
            value >>= 12;
            xMc[i] = (short)(value + 4);
        }

        mantissa = mant;
        exponent = exp;
        xmaxc = encodedMaximum;
    }

    private static void ApcmInverseQuantization(
        short[] xMc,
        short mantissa,
        short exponent,
        short[] xMp) {
        short directMantissa = GsmFac[mantissa];
        short shift = Sub(6, exponent);
        short rounding = Asl(1, Sub(shift, 1));

        for (int i = 0; i < 13; i++) {
            short value = (short)((xMc[i] << 1) - 7);
            value = (short)(value << 12);
            value = MultR(directMantissa, value);
            value = Add(value, rounding);
            xMp[i] = Asr(value, shift);
        }
    }

    private static void RpeGridPositioning(short mc, short[] xMp, short[] ep, int epOffset) {
        Array.Clear(ep, epOffset, 40);
        for (int i = 0; i < 13; i++)
            ep[epOffset + mc + 3 * i] = xMp[i];
    }

    internal static void RpeEncoding(
        Gsm0610State state,
        short[] residual,
        int residualOffset,
        out short xmaxc,
        out short mc,
        short[] xMc) {
        short[] x = new short[40];
        short[] xM = new short[13];
        short[] xMp = new short[13];

        WeightingFilter(x, residual, residualOffset);
        RpeGridSelection(x, xM, out mc);
        ApcmQuantization(xM, xMc, out short mantissa, out short exponent, out xmaxc);
        ApcmInverseQuantization(xMc, mantissa, exponent, xMp);
        RpeGridPositioning(mc, xMp, residual, residualOffset);
    }

    internal static void RpeDecoding(
        Gsm0610State state,
        short xmaxc,
        short mcr,
        short[] xMcr,
        short[] erp) {
        XmaxcToExponentMantissa(xmaxc, out short exponent, out short mantissa);
        short[] xMp = new short[13];
        ApcmInverseQuantization(xMcr, mantissa, exponent, xMp);
        RpeGridPositioning(mcr, xMp, erp, 0);
    }

    internal static void gsm0610_rpe_encoding(
        Gsm0610State state,
        short[] residual,
        int residualOffset,
        out short xmaxc,
        out short mc,
        short[] xMc) =>
        RpeEncoding(state, residual, residualOffset, out xmaxc, out mc, xMc);

    internal static void gsm0610_rpe_decoding(
        Gsm0610State state,
        short xmaxc,
        short mcr,
        short[] xMcr,
        short[] erp) =>
        RpeDecoding(state, xmaxc, mcr, xMcr, erp);
}
