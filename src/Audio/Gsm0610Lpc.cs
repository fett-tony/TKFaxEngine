/* Managed port of gsm0610_lpc.c. LGPL-2.1. */

#nullable enable

namespace TKFaxEngine.Audio;

internal static partial class Gsm0610Codec {
    private static short Divide(short numerator, short denominator) {
        if (numerator == 0) return 0;
        int num = numerator;
        int den = denominator;
        short result = 0;
        for (int i = 0; i < 15; i++) {
            result = (short)(result << 1);
            num <<= 1;
            if (num >= den) {
                num -= den;
                result++;
            }
        }
        return result;
    }

    private static void Autocorrelation(short[] amplitude, int[] acf) {
        short maximum = 0;
        for (int i = 0; i < FrameLength; i++) {
            short value = Abs(amplitude[i]);
            if (value > maximum) maximum = value;
        }

        short scale = maximum == 0
            ? (short)0
            : (short)(4 - Norm(maximum << 16));

        if (scale > 0) {
            short factor = (short)(16384 >> (scale - 1));
            for (int i = 0; i < FrameLength; i++)
                amplitude[i] = MultR(amplitude[i], factor);
        }

        for (int lag = 0; lag <= 8; lag++) {
            long sum = 0;
            for (int i = lag; i < FrameLength; i++)
                sum += (long)amplitude[i] * amplitude[i - lag];
            acf[lag] = Sat32(sum << 1);
        }

        if (scale > 0) {
            for (int i = 0; i < FrameLength; i++)
                amplitude[i] = (short)(amplitude[i] << scale);
        }
    }

    private static void ReflectionCoefficients(int[] lacf, short[] reflection) {
        if (lacf[0] == 0) {
            Array.Clear(reflection);
            return;
        }

        short normalization = Norm(lacf[0]);
        short[] acf = new short[9];
        short[] p = new short[9];
        short[] k = new short[9];

        for (int i = 0; i <= 8; i++)
            acf[i] = (short)(((long)lacf[i] << normalization) >> 16);
        for (int i = 1; i <= 7; i++) k[i] = acf[i];
        Array.Copy(acf, p, 9);

        for (int n = 1; n <= 8; n++) {
            short temp = Abs(p[1]);
            if (p[0] < temp) {
                for (int i = n - 1; i < 8; i++) reflection[i] = 0;
                return;
            }

            short r = Divide(temp, p[0]);
            if (p[1] > 0) r = (short)-r;
            reflection[n - 1] = r;
            if (n == 8) return;

            temp = MultR(p[1], r);
            p[0] = Add(p[0], temp);

            for (int m = 1; m <= 8 - n; m++) {
                temp = MultR(k[m], r);
                short nextP = Add(p[m + 1], temp);
                temp = MultR(p[m + 1], r);
                k[m] = Add(k[m], temp);
                p[m] = nextP;
            }
        }
    }

    private static void TransformToLogAreaRatios(short[] ratios) {
        for (int i = 0; i < 8; i++) {
            short value = Abs(ratios[i]);
            if (value < 22118)
                value >>= 1;
            else if (value < 31130)
                value = (short)(value - 11059);
            else
                value = (short)((value - 26112) << 2);
            ratios[i] = ratios[i] < 0 ? (short)-value : value;
        }
    }

    private static short QuantizeLar(short value, short a, short b, short maximum, short minimum) {
        short temp = Mult(a, value);
        temp = Add(temp, (short)(b + 256));
        temp >>= 9;
        if (temp > maximum) return (short)(maximum - minimum);
        if (temp < minimum) return 0;
        return (short)(temp - minimum);
    }

    private static void QuantizationAndCoding(short[] lar) {
        lar[0] = QuantizeLar(lar[0], 20480, 0, 31, -32);
        lar[1] = QuantizeLar(lar[1], 20480, 0, 31, -32);
        lar[2] = QuantizeLar(lar[2], 20480, 2048, 15, -16);
        lar[3] = QuantizeLar(lar[3], 20480, -2560, 15, -16);
        lar[4] = QuantizeLar(lar[4], 13964, 94, 7, -8);
        lar[5] = QuantizeLar(lar[5], 15360, -1792, 7, -8);
        lar[6] = QuantizeLar(lar[6], 8534, -341, 3, -4);
        lar[7] = QuantizeLar(lar[7], 9036, -1144, 3, -4);
    }

    internal static void LpcAnalysis(
        Gsm0610State state,
        short[] amplitude,
        short[] larc) {
        int[] acf = new int[9];
        Autocorrelation(amplitude, acf);
        ReflectionCoefficients(acf, larc);
        TransformToLogAreaRatios(larc);
        QuantizationAndCoding(larc);
    }

    internal static short gsm0610_norm(int value) => Norm(value);

    internal static void gsm0610_lpc_analysis(
        Gsm0610State state,
        short[] amplitude,
        short[] larc) =>
        LpcAnalysis(state, amplitude, larc);
}
