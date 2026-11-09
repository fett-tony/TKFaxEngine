/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * complex_tests.cs - Complex arithmetic tests.
 *
 * Original C test written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2008 Steve Underwood.
 *
 * Managed C# port preserving the original test sequence.
 */

#nullable enable

using System.Globalization;
using static TKFaxEngine.ComplexDspApi;

namespace TKFaxEngineTest.Core.Test;

public static class complex_tests {
    public static int main(string[] testArguments) {
        try {
            return tkfx_complex_tests_body_main(testArguments);
        } catch (ComplexTestsExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_complex_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        var fa = complex_setf(0.5f, 0.25f);
        var fb = complex_setf(0.25f, 0.5f);
        var fz = complex_mulf(in fa, in fb);

        Console.WriteLine(
            string.Format(
                CultureInfo.InvariantCulture,
                "({0:F6}, {1:F6}) * ({2:F6}, {3:F6}) => ({4:F6}, {5:F6})",
                fa.Re,
                fa.Im,
                fb.Re,
                fb.Im,
                fz.Re,
                fz.Im));

        var i16a = complex_seti16(16383, 8191);
        var i16b = complex_seti16(8191, 16383);
        var i16z = complex_mul_q1_15(in i16a, in i16b);

        Console.WriteLine(
            string.Format(
                CultureInfo.InvariantCulture,
                "({0:F6}, {1:F6}) * ({2:F6}, {3:F6}) => ({4:F6}, {5:F6})",
                i16a.Re / 32768.0,
                i16a.Im / 32768.0,
                i16b.Re / 32768.0,
                i16b.Im / 32768.0,
                i16z.Re / 32768.0,
                i16z.Im / 32768.0));

        Console.WriteLine("Tests passed.");
        return 0;
    }

    public sealed class ComplexTestsExitException : Exception {
        public ComplexTestsExitException(int exitCode)
            : base($"complex_tests requested exit code {exitCode}.") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }
}
