/*
 * TKFaxEngine - managed C# port
 *
 * math_fixed_tests.cs - Test the fixed point math functions.
 *
 * Fresh direct port of math_fixed_tests.c.
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2010 Steve Underwood.
 * Licensed under the GNU General Public License version 2.
 */

#nullable enable

using static TKFaxEngine.FixedMathApi;

namespace TKFaxEngineTest.Core.Test;

public static class math_fixed_tests {
    private const double Pi = 3.1415926535;

    private static void Fail() {
        Console.WriteLine("Test failed");
        throw new MathFixedTestsExitException(2);
    }

    private static void fixed_reciprocal16_tests() {
        Console.WriteLine("fixed_reciprocal16() function tests");
        if (fixed_reciprocal16(0, out int shift) != 0xFFFF || shift != 0)
            Fail();

        double min = 999999.0;
        double max = -999999.0;

        for (int x = 1; x < 65536; x++) {
            ushort yu16 = fixed_reciprocal16((ushort)x, out shift);
            uint yu32 = (uint)yu16 << shift;
            double z = 32768.0 * 32768.0 / x;
            double ratio = z / yu32;

            if (ratio < min)
                min = ratio;
            if (ratio > max)
                max = ratio;
            if (ratio < 0.996 || ratio > 1.004)
                Fail();
        }

        Console.WriteLine("min {0:F6}, max {1:F6}", min, max);
        Console.WriteLine("Test passed");
    }

    private static void fixed_divide16_tests() {
        Console.WriteLine("fixed_divide16() function tests");
        if (fixed_divide16(12345, 0) != 0xFFFF)
            Fail();

        double min = 999999.0;
        double max = -999999.0;

        for (int y = 32; y < 65536; y += 16) {
            for (int x = y * 16; x < 65536; x += 16) {
                ushort yu16 = fixed_divide16((ushort)y, (ushort)x);
                double z = 32768.0 * y / x;
                double ratio = z / yu16;

                if (ratio < min)
                    min = ratio;
                if (ratio > max)
                    max = ratio;
                if (ratio < 0.996 || ratio > 1.07)
                    Fail();
            }
        }

        Console.WriteLine("min {0:F6}, max {1:F6}", min, max);
        Console.WriteLine("Test passed");
    }

    private static void fixed_divide32_tests() {
        Console.WriteLine("fixed_divide32() function tests");
        if (fixed_divide32(12345, 0) != 0xFFFF)
            Fail();

        double min = 999999.0;
        double max = -999999.0;

        for (uint y = 32; y < 65536; y += 16) {
            for (uint x = y * 16; x < 65535; x += 16) {
                ushort yu16 = fixed_divide32(y, (ushort)x);
                double z = 32768.0 * y / x;
                double ratio = z / yu16;

                if (ratio < min)
                    min = ratio;
                if (ratio > max)
                    max = ratio;
                if (ratio < 0.996 || ratio > 1.07)
                    Fail();
            }
        }

        Console.WriteLine("min {0:F6}, max {1:F6}", min, max);
        Console.WriteLine("Test passed");
    }

    private static void fixed_log10_16_tests() {
        Console.WriteLine("Log10 16 bit function tests");
        double min = 999999.0;
        double max = -999999.0;

        for (int x = 1; x < 32500; x++) {
            short yi16 = fixed_log10_16((ushort)x);
            double z = 4096.0 * Math.Log10(x / 32768.0);
            double ratio = z - yi16;

            if (ratio < min)
                min = ratio;
            if (ratio > max)
                max = ratio;
            if (ratio < -8.0 || ratio > 8.0)
                Fail();
        }

        Console.WriteLine("min {0:F6}, max {1:F6}", min, max);
        Console.WriteLine("Test passed");
    }

    private static void fixed_log10_32_tests() {
        Console.WriteLine("fixed_log10_32() function tests");
        double min = 999999.0;
        double max = -999999.0;

        for (int x = 1; x < 32767 * 65536; x += 0x4000) {
            int yi32 = fixed_log10_32((uint)x);
            double z = 4096.0 * Math.Log10(x / (32768.0 * 65536.0));
            double ratio = z - yi32;

            if (ratio < min)
                min = ratio;
            if (ratio > max)
                max = ratio;
            if (ratio < -8.0 || ratio > 8.0)
                Fail();
        }

        Console.WriteLine("min {0:F6}, max {1:F6}", min, max);
        Console.WriteLine("Test passed");
    }

    private static void fixed_sqrt16_tests() {
        Console.WriteLine("fixed_sqrt16() function tests");
        double min = 999999.0;
        double max = -999999.0;

        for (int x = 1; x < 65536; x++) {
            ushort yu16 = fixed_sqrt16((ushort)x);
            double z = Math.Sqrt(x) * 256.0;
            double ratio = z / yu16;

            if (ratio < min)
                min = ratio;
            if (ratio > max)
                max = ratio;
            if (ratio < 0.999 || ratio > 1.008)
                Fail();
        }

        Console.WriteLine("min {0:F6}, max {1:F6}", min, max);
        Console.WriteLine("Test passed");
    }

    private static void fixed_sqrt32_tests() {
        Console.WriteLine("fixed_sqrt32() function tests");
        double min = 999999.0;
        double max = -999999.0;

        for (uint x = 20000; x < 0xFFFF0000U; x += 10000) {
            ushort yu16 = fixed_sqrt32(x);
            double z = Math.Sqrt(x);
            double ratio = z / yu16;

            if (ratio < min)
                min = ratio;
            if (ratio > max)
                max = ratio;
            if (ratio < 0.999 || ratio > 1.009)
                Fail();
        }

        Console.WriteLine("min {0:F6}, max {1:F6}", min, max);
        Console.WriteLine("Test passed");
    }

    private static void fixed_sin_tests() {
        Console.WriteLine("fixed_sin() function tests");
        double min = 999999.0;
        double max = -999999.0;

        for (int x = 0; x < 65536; x++) {
            short yi16 = fixed_sin((ushort)x);
            double z = Math.Sin(2.0 * Pi * x / 65536.0) * 32768.0;
            double ratio = z - yi16;

            if (ratio < min)
                min = ratio;
            if (ratio > max)
                max = ratio;
            if (ratio < -2.0 || ratio > 2.0)
                Fail();
        }

        Console.WriteLine("min {0:F6}, max {1:F6}", min, max);
        Console.WriteLine("Test passed");
    }

    private static void fixed_cos_tests() {
        Console.WriteLine("fixed_cos() function tests");
        double min = 999999.0;
        double max = -999999.0;

        for (int x = 0; x < 65536; x++) {
            short yi16 = fixed_cos((ushort)x);
            double z = Math.Cos(2.0 * Pi * x / 65536.0) * 32768.0;
            double ratio = z - yi16;

            if (ratio < min)
                min = ratio;
            if (ratio > max)
                max = ratio;
            if (ratio < -2.0 || ratio > 2.0)
                Fail();
        }

        Console.WriteLine("min {0:F6}, max {1:F6}", min, max);
        Console.WriteLine("Test passed");
    }

    private static void fixed_atan2_tests() {
        Console.WriteLine("fixed_atan2() function tests");
        double min = 999999.0;
        double max = -999999.0;

        for (int i = 0; i < 65536; i++) {
            int x = (int)(16384.0 * Math.Cos(i * 2.0 * Pi / 65536.0));
            int y = (int)(16384.0 * Math.Sin(i * 2.0 * Pi / 65536.0));
            ushort yu16 = fixed_atan2((short)y, (short)x);
            double z = Math.Atan2(y / 32768.0, x / 32768.0) * 65536.0 / (2.0 * Pi);
            if (z < 0.0)
                z += 65536.0;
            double ratio = z - yu16;

            if (ratio < min)
                min = ratio;
            if (ratio > max)
                max = ratio;
            if (ratio < -43.0 || ratio > 43.0)
                Fail();
        }

        Console.WriteLine("min {0:F6}, max {1:F6}", min, max);
        Console.WriteLine("Test passed");
    }

    public static int tkfx_math_fixed_tests_body_main(string[] testArguments) {
        fixed_reciprocal16_tests();
        fixed_divide16_tests();
        fixed_divide32_tests();
        fixed_log10_16_tests();
        fixed_log10_32_tests();
        fixed_sqrt16_tests();
        fixed_sqrt32_tests();
        fixed_sin_tests();
        fixed_cos_tests();
        fixed_atan2_tests();

        Console.WriteLine("Tests passed");
        return 0;
    }

    public static int Main(string[] args) => tkfx_math_fixed_tests_body_main(args);

    public sealed class MathFixedTestsExitException : Exception {
        public MathFixedTestsExitException(int exitCode)
            : base($"math_fixed_tests requested process exit {exitCode}.") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }
}
