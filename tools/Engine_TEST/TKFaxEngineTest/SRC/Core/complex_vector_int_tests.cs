/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * complex_vector_int_tests.cs
 *
 * Original C test written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2006 Steve Underwood.
 *
 * Managed C# port preserving the original integer complex dot-product
 * and circular dot-product verification sequence.
 */

#nullable enable

using static TKFaxEngine.ComplexDspApi;

namespace TKFaxEngineTest.Core.Test;

public static class complex_vector_int_tests {
    private static readonly Random RandomSource = new(1);

    public static int main(string[] testArguments) {
        try {
            return tkfx_complex_vector_int_tests_body_main(testArguments);
        } catch (ComplexVectorIntTestsExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_complex_vector_int_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        test_cvec_dot_prodi16();
        test_cvec_circular_dot_prodi16();

        Console.WriteLine("Tests passed.");
        return 0;
    }

    private static TKFaxEngine.ComplexInt32 cvec_dot_prodi16_dumb(
        TKFaxEngine.ComplexInt16[] x,
        TKFaxEngine.ComplexInt16[] y,
        int n) {
        TKFaxEngine.ComplexInt32 z = complex_seti32(0, 0);

        unchecked {
            for (int i = 0; i < n; i++) {
                z.Re += x[i].Re * y[i].Re - x[i].Im * y[i].Im;
                z.Im += x[i].Re * y[i].Im + x[i].Im * y[i].Re;
            }
        }

        return z;
    }

    private static int test_cvec_dot_prodi16() {
        var x = new TKFaxEngine.ComplexInt16[99];
        var y = new TKFaxEngine.ComplexInt16[99];

        for (int i = 0; i < 99; i++) {
            x[i].Re = unchecked((short)RandomSource.Next());
            x[i].Im = unchecked((short)RandomSource.Next());
            y[i].Re = unchecked((short)RandomSource.Next());
            y[i].Im = unchecked((short)RandomSource.Next());
        }

        for (int i = 1; i < 99; i++) {
            TKFaxEngine.ComplexInt32 za = cvec_dot_prodi16(x, y, i);
            TKFaxEngine.ComplexInt32 zb = cvec_dot_prodi16_dumb(x, y, i);

            if (za.Re != zb.Re || za.Im != zb.Im)
                Fail("Tests failed");
        }

        return 0;
    }

    private static int test_cvec_circular_dot_prodi16() {
        var x = new TKFaxEngine.ComplexInt16[99];
        var y = new TKFaxEngine.ComplexInt16[99];

        for (int i = 0; i < 99; i++) {
            x[i].Re = unchecked((short)RandomSource.Next());
            x[i].Im = unchecked((short)RandomSource.Next());
            y[i].Re = unchecked((short)RandomSource.Next());
            y[i].Im = unchecked((short)RandomSource.Next());
        }

        const int len = 95;

        for (int pos = 0; pos < len; pos++) {
            TKFaxEngine.ComplexInt32 za = cvec_circular_dot_prodi16(x, y, len, pos);
            TKFaxEngine.ComplexInt32 zb = complex_seti32(0, 0);

            unchecked {
                for (int i = 0; i < len; i++) {
                    int j = (pos + i) % len;
                    zb.Re += x[j].Re * y[i].Re - x[j].Im * y[i].Im;
                    zb.Im += x[j].Re * y[i].Im + x[j].Im * y[i].Re;
                }
            }

            if (za.Re != zb.Re || za.Im != zb.Im)
                Fail("Tests failed");
        }

        return 0;
    }

    private static void Fail(string message) {
        Console.WriteLine(message);
        throw new ComplexVectorIntTestsExitException(2);
    }

    public sealed class ComplexVectorIntTestsExitException : Exception {
        public ComplexVectorIntTestsExitException(int exitCode)
            : base($"complex_vector_int_tests requested exit code {exitCode}.") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }
}
