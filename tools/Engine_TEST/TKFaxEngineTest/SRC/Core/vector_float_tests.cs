/*
 * TKFaxEngine - managed C# port of vector_float_tests.c
 *
 * Original test written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2006 Steve Underwood.
 * GPL-2.0 licensing terms of the source test apply.
 */

using System.Globalization;
using static TKFaxEngine.VectorDspApi;

namespace TKFaxEngineTest.Core.Test;

public sealed class VectorFloatTestsExitException : Exception {
    public VectorFloatTestsExitException(int exitCode)
        : base($"vector_float_tests terminated with exit code {exitCode}.") {
        ExitCode = exitCode;
    }

    public int ExitCode { get; }
}

public static class vector_float_tests {
    private const float LmsLeakRate = 0.9999f;

    private sealed class CRandom {
        private uint _state = 1;

        public int Next() {
            _state = unchecked(_state * 214013U + 2531011U);
            return (int)((_state >> 16) & 0x7FFFU);
        }
    }

    private static void Exit(int code) => throw new VectorFloatTestsExitException(code);

    private static void Fail(string detail) {
        if (!string.IsNullOrEmpty(detail))
            Console.WriteLine(detail);
        Console.WriteLine("Tests failed");
        Exit(2);
    }

    private static void vec_copyf_dumb(Span<float> z, ReadOnlySpan<float> x, int n) {
        for (int i = 0; i < n; i++)
            z[i] = x[i];
    }

    private static int test_vec_copyf() {
        float[] x = new float[100];
        float[] za = new float[100];
        float[] zb = new float[100];

        Console.WriteLine("Testing vec_copyf()");
        for (int i = 0; i < 99; i++) {
            x[i] = i;
            za[i] = -0.5f;
            zb[i] = -0.5f;
        }

        foreach (int count in new[] { 0, 1, 29 }) {
            vec_copyf_dumb(za.AsSpan(3), x.AsSpan(1), count);
            vec_copyf(zb.AsSpan(3), x.AsSpan(1), count);
            for (int i = 0; i < 99; i++) {
                if (za[i] != zb[i]) {
                    Fail(string.Format(
                        CultureInfo.InvariantCulture,
                        "vec_copyf() - {0} {1:F6} {2:F6}",
                        i,
                        za[i],
                        zb[i]));
                }
            }
        }

        return 0;
    }

    private static void vec_negatef_dumb(Span<float> z, ReadOnlySpan<float> x, int n) {
        for (int i = 0; i < n; i++)
            z[i] = -x[i];
    }

    private static int test_vec_negatef() {
        float[] x = new float[100];
        float[] za = new float[100];
        float[] zb = new float[100];

        Console.WriteLine("Testing vec_negatef()");
        for (int i = 0; i < 99; i++) {
            x[i] = i;
            za[i] = -0.5f;
            zb[i] = -0.5f;
        }

        foreach (int count in new[] { 0, 1, 29 }) {
            vec_negatef_dumb(za.AsSpan(3), x.AsSpan(1), count);
            vec_negatef(zb.AsSpan(3), x.AsSpan(1), count);
            for (int i = 0; i < 99; i++) {
                if (za[i] != zb[i]) {
                    Fail(string.Format(
                        CultureInfo.InvariantCulture,
                        "vec_negatef() - {0} {1:F6} {2:F6}",
                        i,
                        za[i],
                        zb[i]));
                }
            }
        }

        return 0;
    }

    private static void vec_zerof_dumb(Span<float> z, int n) {
        for (int i = 0; i < n; i++)
            z[i] = 0.0f;
    }

    private static int test_vec_zerof() {
        float[] za = new float[100];
        float[] zb = new float[100];

        Console.WriteLine("Testing vec_zerof()");
        for (int i = 0; i < 99; i++) {
            za[i] = -1.0f;
            zb[i] = -1.0f;
        }

        foreach (int count in new[] { 0, 1, 29 }) {
            vec_zerof_dumb(za.AsSpan(3), count);
            vec_zerof(zb.AsSpan(3), count);
            for (int i = 0; i < 99; i++) {
                if (za[i] != zb[i]) {
                    Fail(string.Format(
                        CultureInfo.InvariantCulture,
                        "vec_zerof() - {0} {1:F6} {2:F6}",
                        i,
                        za[i],
                        zb[i]));
                }
            }
        }

        return 0;
    }

    private static void vec_setf_dumb(Span<float> z, float x, int n) {
        for (int i = 0; i < n; i++)
            z[i] = x;
    }

    private static int test_vec_setf() {
        float[] za = new float[100];
        float[] zb = new float[100];

        Console.WriteLine("Testing vec_setf()");
        for (int i = 0; i < 99; i++) {
            za[i] = -1.0f;
            zb[i] = -1.0f;
        }

        foreach (int count in new[] { 0, 1, 29 }) {
            vec_setf_dumb(za.AsSpan(3), 42.0f, count);
            vec_setf(zb.AsSpan(3), 42.0f, count);
            for (int i = 0; i < 99; i++) {
                if (za[i] != zb[i]) {
                    Fail(string.Format(
                        CultureInfo.InvariantCulture,
                        "vec_setf() - {0} {1:F6} {2:F6}",
                        i,
                        za[i],
                        zb[i]));
                }
            }
        }

        return 0;
    }

    private static double vec_dot_prod_dumb(ReadOnlySpan<double> x, ReadOnlySpan<double> y, int n) {
        double z = 0.0;
        for (int i = 0; i < n; i++)
            z += x[i] * y[i];
        return z;
    }

    private static int test_vec_dot_prod(CRandom random) {
        double[] x = new double[100];
        double[] y = new double[100];

        Console.WriteLine("Testing vec_dot_prod()");
        for (int i = 0; i < 99; i++) {
            x[i] = random.Next();
            y[i] = random.Next();
        }

        for (int i = 1; i < 99; i++) {
            double zsa = vec_dot_prod(x, y, i);
            double zsb = vec_dot_prod_dumb(x, y, i);
            double ratio = zsa / zsb;
            if (ratio < 0.9999 || ratio > 1.0001) {
                Fail(string.Format(
                    CultureInfo.InvariantCulture,
                    "vec_dot_prod() - {0:F6} {1:F6}",
                    zsa,
                    zsb));
            }
        }

        return 0;
    }

    private static float vec_dot_prodf_dumb(ReadOnlySpan<float> x, ReadOnlySpan<float> y, int n) {
        float z = 0.0f;
        for (int i = 0; i < n; i++)
            z += x[i] * y[i];
        return z;
    }

    private static int test_vec_dot_prodf(CRandom random) {
        float[] x = new float[100];
        float[] y = new float[100];

        Console.WriteLine("Testing vec_dot_prodf()");
        for (int i = 0; i < 99; i++) {
            x[i] = random.Next();
            y[i] = random.Next();
        }

        for (int i = 1; i < 99; i++) {
            float zsa = vec_dot_prodf(x, y, i);
            float zsb = vec_dot_prodf_dumb(x, y, i);
            float ratio = zsa / zsb;
            if (ratio < 0.9999f || ratio > 1.0001f) {
                Fail(string.Format(
                    CultureInfo.InvariantCulture,
                    "vec_dot_prodf() - {0:E6} {1:E6}",
                    zsa,
                    zsb));
            }
        }

        return 0;
    }

    private static void vec_addf_dumb(Span<float> z, ReadOnlySpan<float> x, ReadOnlySpan<float> y, int n) {
        for (int i = 0; i < n; i++)
            z[i] = x[i] + y[i];
    }

    private static int test_vec_addf(CRandom random) {
        float[] x = new float[100];
        float[] y = new float[100];
        float[] zsa = new float[100];
        float[] zsb = new float[100];

        Console.WriteLine("Testing vec_addf()");
        FillRandomPair(random, x, y);
        for (int i = 1; i < 90; i++) {
            vec_addf(zsa.AsSpan(1), x.AsSpan(1), y.AsSpan(1), i);
            vec_addf_dumb(zsb.AsSpan(1), x.AsSpan(1), y.AsSpan(1), i);
            CheckVectorRatio("vec_mulf()", zsa, zsb, 1, i);
        }

        return 0;
    }

    private static void vec_subf_dumb(Span<float> z, ReadOnlySpan<float> x, ReadOnlySpan<float> y, int n) {
        for (int i = 0; i < n; i++)
            z[i] = x[i] - y[i];
    }

    private static int test_vec_subf(CRandom random) {
        float[] x = new float[100];
        float[] y = new float[100];
        float[] zsa = new float[100];
        float[] zsb = new float[100];

        Console.WriteLine("Testing vec_subf()");
        FillRandomPair(random, x, y);
        for (int i = 1; i < 90; i++) {
            vec_subf(zsa.AsSpan(1), x.AsSpan(1), y.AsSpan(1), i);
            vec_subf_dumb(zsb.AsSpan(1), x.AsSpan(1), y.AsSpan(1), i);
            CheckVectorRatio("vec_mulf()", zsa, zsb, 1, i);
        }

        return 0;
    }

    private static void vec_mulf_dumb(Span<float> z, ReadOnlySpan<float> x, ReadOnlySpan<float> y, int n) {
        for (int i = 0; i < n; i++)
            z[i] = x[i] * y[i];
    }

    private static int test_vec_mulf(CRandom random) {
        float[] x = new float[100];
        float[] y = new float[100];
        float[] zsa = new float[100];
        float[] zsb = new float[100];

        Console.WriteLine("Testing vec_mulf()");
        FillRandomPair(random, x, y);
        for (int i = 1; i < 90; i++) {
            vec_mulf(zsa.AsSpan(1), x.AsSpan(1), y.AsSpan(1), i);
            vec_mulf_dumb(zsb.AsSpan(1), x.AsSpan(1), y.AsSpan(1), i);
            CheckVectorRatio("vec_mulf()", zsa, zsb, 1, i);
        }

        return 0;
    }

    private static void vec_lmsf_dumb(ReadOnlySpan<float> x, Span<float> y, int n, float error) {
        for (int i = 0; i < n; i++)
            y[i] = y[i] * LmsLeakRate + x[i] * error;
    }

    private static int test_vec_lmsf(CRandom random) {
        float[] x = new float[100];
        float[] ya = new float[100];
        float[] yb = new float[100];

        Console.WriteLine("Testing vec_lmsf()");
        for (int i = 0; i < 99; i++) {
            x[i] = random.Next();
            float value = random.Next();
            ya[i] = value;
            yb[i] = value;
        }

        for (int i = 1; i < 99; i++) {
            vec_lmsf(x, ya, i, 0.1f);
            vec_lmsf_dumb(x, yb, i, 0.1f);
            CheckVectorRatio("vec_lmsf()", ya, yb, 0, i - 1);
        }

        return 0;
    }

    private static void vec_scaledxy_addf_dumb(
        Span<float> z,
        ReadOnlySpan<float> x,
        float xScale,
        ReadOnlySpan<float> y,
        float yScale,
        int n) {
        for (int i = 0; i < n; i++)
            z[i] = x[i] * xScale + y[i] * yScale;
    }

    private static int test_vec_scaledxy_addf(CRandom random) {
        float[] x = new float[100];
        float[] y = new float[100];
        float[] za = new float[100];
        float[] zb = new float[100];

        Console.WriteLine("Testing vec_scaledxy_addf()");
        FillRandomPair(random, x, y);
        for (int i = 1; i < 99; i++) {
            vec_scaledxy_addf(za, x, 2.5f, y, 1.5f, i);
            vec_scaledxy_addf_dumb(zb, x, 2.5f, y, 1.5f, i);
            CheckVectorRatio("vec_scaledxy_addf()", za, zb, 0, i - 1);
        }

        return 0;
    }

    private static void vec_scaledy_addf_dumb(
        Span<float> z,
        ReadOnlySpan<float> x,
        ReadOnlySpan<float> y,
        float yScale,
        int n) {
        for (int i = 0; i < n; i++)
            z[i] = x[i] + y[i] * yScale;
    }

    private static int test_vec_scaledy_addf(CRandom random) {
        float[] x = new float[100];
        float[] y = new float[100];
        float[] za = new float[100];
        float[] zb = new float[100];

        Console.WriteLine("Testing vec_scaledy_addf()");
        FillRandomPair(random, x, y);
        for (int i = 1; i < 99; i++) {
            vec_scaledy_addf(za, x, y, 1.5f, i);
            vec_scaledy_addf_dumb(zb, x, y, 1.5f, i);
            CheckVectorRatio("vec_scaledy_addf()", za, zb, 0, i - 1);
        }

        return 0;
    }

    private static void FillRandomPair(CRandom random, float[] x, float[] y) {
        for (int i = 0; i < 99; i++) {
            x[i] = random.Next();
            y[i] = random.Next();
        }
    }

    private static void CheckVectorRatio(
        string functionName,
        float[] actual,
        float[] expected,
        int first,
        int lastInclusive) {
        for (int i = first; i <= lastInclusive; i++) {
            float ratio = actual[i] / expected[i];
            if (ratio < 0.9999f || ratio > 1.0001f) {
                Fail(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} - {1} {2:E6} {3:E6}",
                    functionName,
                    i,
                    actual[i],
                    expected[i]));
            }
        }
    }

    public static int body_main(string[] args) {
        _ = args;
        CRandom random = new();

        test_vec_copyf();
        test_vec_negatef();
        test_vec_zerof();
        test_vec_setf();
        test_vec_addf(random);
        test_vec_subf(random);
        test_vec_mulf(random);
        test_vec_scaledxy_addf(random);
        test_vec_scaledy_addf(random);
        test_vec_dot_prod(random);
        test_vec_dot_prodf(random);
        test_vec_lmsf(random);

        Console.WriteLine("Tests passed.");
        return 0;
    }
}
