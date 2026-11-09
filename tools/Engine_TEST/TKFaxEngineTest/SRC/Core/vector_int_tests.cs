/*
 * TKFaxEngine - managed C# port of vector_int_tests.c
 *
 * Original test written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2006 Steve Underwood.
 * GPL-2.0 licensing terms of the source test apply.
 */

using static TKFaxEngine.VectorDspApi;

namespace TKFaxEngineTest.Core.Test;

public sealed class VectorIntTestsExitException : Exception {
    public VectorIntTestsExitException(int exitCode)
        : base($"vector_int_tests terminated with exit code {exitCode}.") {
        ExitCode = exitCode;
    }

    public int ExitCode { get; }
}

public static class vector_int_tests {
    private sealed class CRandom {
        private uint _state = 1;

        public int Next() {
            _state = unchecked(_state * 214013U + 2531011U);
            return (int)((_state >> 16) & 0x7FFFU);
        }
    }

    private static void Exit(int code) => throw new VectorIntTestsExitException(code);

    private static void Fail() {
        Console.WriteLine("Tests failed");
        Exit(2);
    }

    private static int vec_dot_prodi16_dumb(ReadOnlySpan<short> x, ReadOnlySpan<short> y, int n) {
        int z = 0;
        unchecked {
            for (int i = 0; i < n; i++)
                z += x[i] * y[i];
        }
        return z;
    }

    private static int test_vec_dot_prodi16(CRandom random) {
        short[] x = new short[99];
        short[] y = new short[99];

        for (int i = 0; i < 99; i++) {
            x[i] = unchecked((short)random.Next());
            y[i] = unchecked((short)random.Next());
        }

        for (int i = 1; i < 99; i++) {
            int za = vec_dot_prodi16(x, y, i);
            int zb = vec_dot_prodi16_dumb(x, y, i);
            if (za != zb)
                Fail();
        }

        return 0;
    }

    private static int vec_min_maxi16_dumb(ReadOnlySpan<short> x, int n, Span<short> output) {
        short minimum = short.MaxValue;
        short maximum = short.MinValue;

        for (int i = 0; i < n; i++) {
            short value = x[i];
            if (value > maximum)
                maximum = value;
            if (value < minimum)
                minimum = value;
        }

        output[0] = maximum;
        output[1] = minimum;

        int absoluteMinimum = Math.Abs((int)minimum);
        return absoluteMinimum > maximum ? absoluteMinimum : maximum;
    }

    private static int test_vec_min_maxi16(CRandom random) {
        short[] x = new short[99];
        short[] outA = new short[2];
        short[] outB = new short[2];

        for (int i = 0; i < 99; i++)
            x[i] = unchecked((short)random.Next());

        x[42] = short.MinValue;
        int za = vec_min_maxi16_dumb(x, 99, outA);
        int zb = vec_min_maxi16(x, 99, outB);

        if (za != zb || outA[0] != outB[0] || outA[1] != outB[1])
            Fail();

        return 0;
    }

    private static int test_vec_circular_dot_prodi16(CRandom random) {
        short[] x = new short[99];
        short[] y = new short[99];

        for (int i = 0; i < 99; i++) {
            x[i] = unchecked((short)random.Next());
            y[i] = unchecked((short)random.Next());
        }

        const int length = 95;
        for (int position = 0; position < length; position++) {
            int za = vec_circular_dot_prodi16(x, y, length, position);
            int zb = 0;
            unchecked {
                for (int i = 0; i < length; i++) {
                    int index = (position + i) % length;
                    zb += x[index] * y[i];
                }
            }

            if (za != zb)
                Fail();
        }

        return 0;
    }

    public static int body_main(string[] args) {
        _ = args;
        CRandom random = new();

        test_vec_dot_prodi16(random);
        test_vec_min_maxi16(random);
        test_vec_circular_dot_prodi16(random);

        Console.WriteLine("Tests passed.");
        return 0;
    }
}
