/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * complex_vector_float_tests.cs
 *
 * Original C test written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2006, 2008 Steve Underwood.
 *
 * Managed C# port preserving the original complex-vector multiplication
 * and dot-product verification sequence.
 */

#nullable enable

using System.Globalization;
using static TKFaxEngine.ComplexDspApi;

namespace TKFaxEngineTest.Core.Test;

public static class complex_vector_float_tests {
    private static readonly Random RandomSource = new(1);

    public static int main(string[] testArguments) {
        try {
            return tkfx_complex_vector_float_tests_body_main(testArguments);
        } catch (ComplexVectorFloatTestsExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_complex_vector_float_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        test_cvec_mulf();
        test_cvec_dot_prodf();

        Console.WriteLine("Tests passed.");
        return 0;
    }

    private static void cvec_mulf_dumb(
        TKFaxEngine.ComplexFloat[] z,
        TKFaxEngine.ComplexFloat[] x,
        TKFaxEngine.ComplexFloat[] y,
        int n) {
        for (int i = 0; i < n; i++) {
            z[i].Re = x[i].Re * y[i].Re - x[i].Im * y[i].Im;
            z[i].Im = x[i].Re * y[i].Im + x[i].Im * y[i].Re;
        }
    }

    private static int test_cvec_mulf() {
        var x = new TKFaxEngine.ComplexFloat[100];
        var y = new TKFaxEngine.ComplexFloat[100];
        var za = new TKFaxEngine.ComplexFloat[100];
        var zb = new TKFaxEngine.ComplexFloat[100];

        for (int i = 0; i < 99; i++) {
            x[i].Re = RandomSource.Next();
            x[i].Im = RandomSource.Next();
            y[i].Re = RandomSource.Next();
            y[i].Im = RandomSource.Next();
        }

        cvec_mulf(za, x, y, 99);
        cvec_mulf_dumb(zb, x, y, 99);

        for (int i = 0; i < 99; i++) {
            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "({0:F6},{1:F6}) ({2:F6},{3:F6}) ({4:F6},{5:F6})",
                    za[i].Re,
                    za[i].Im,
                    x[i].Re,
                    x[i].Im,
                    y[i].Re,
                    y[i].Im));
        }

        for (int i = 0; i < 99; i++) {
            float ratioRe = za[i].Re / zb[i].Re;
            float ratioIm = za[i].Im / zb[i].Im;

            if (ratioRe < 0.9999f || ratioRe > 1.0001f ||
                ratioIm < 0.9999f || ratioIm > 1.0001f) {
                Console.WriteLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "cvec_mulf() - ({0:F6},{1:F6}) ({2:F6},{3:F6})",
                        za[i].Re,
                        za[i].Im,
                        zb[i].Re,
                        zb[i].Im));
                Fail("Tests failed");
            }
        }

        return 0;
    }

    private static TKFaxEngine.ComplexFloat cvec_dot_prodf_dumb(
        TKFaxEngine.ComplexFloat[] x,
        TKFaxEngine.ComplexFloat[] y,
        int n) {
        TKFaxEngine.ComplexFloat z = complex_setf(0.0f, 0.0f);

        for (int i = 0; i < n; i++) {
            TKFaxEngine.ComplexFloat z1 = complex_mulf(in x[i], in y[i]);
            z = complex_addf(in z, in z1);
        }

        return z;
    }

    private static int test_cvec_dot_prodf() {
        var x = new TKFaxEngine.ComplexFloat[100];
        var y = new TKFaxEngine.ComplexFloat[100];

        for (int i = 0; i < 99; i++) {
            x[i].Re = RandomSource.Next();
            x[i].Im = RandomSource.Next();
            y[i].Re = RandomSource.Next();
            y[i].Im = RandomSource.Next();
        }

        for (int i = 1; i < 99; i++) {
            TKFaxEngine.ComplexFloat zsa = cvec_dot_prodf(x, y, i);
            TKFaxEngine.ComplexFloat zsb = cvec_dot_prodf_dumb(x, y, i);
            float ratioRe = zsa.Re / zsb.Re;
            float ratioIm = zsa.Im / zsb.Im;

            if (ratioRe < 0.9999f || ratioRe > 1.0001f ||
                ratioIm < 0.9999f || ratioIm > 1.0001f) {
                Console.WriteLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "cvec_dot_prodf() - ({0:F6},{1:F6}) ({2:F6},{3:F6})",
                        zsa.Re,
                        zsa.Im,
                        zsb.Re,
                        zsb.Im));
                Fail("Tests failed");
            }
        }

        return 0;
    }

    private static void Fail(string message) {
        Console.WriteLine(message);
        throw new ComplexVectorFloatTestsExitException(2);
    }

    public sealed class ComplexVectorFloatTestsExitException : Exception {
        public ComplexVectorFloatTestsExitException(int exitCode)
            : base($"complex_vector_float_tests requested exit code {exitCode}.") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }
}
