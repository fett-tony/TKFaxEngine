/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * saturated_tests.cs - complete managed port of saturated_tests.c test.
 *
 * Original test written by Steve Underwood.
 * Licensed under the GNU General Public License version 2.
 */

#nullable enable

using static TKFaxEngine.Saturated;

namespace TKFaxEngineTest.Core.Test;

public static class saturated_tests {
    public static int main(string[] testArguments) {
        try {
            return tkfx_saturated_tests_body_main(testArguments);
        } catch (SaturatedTestsExitException exception) {
            return exception.ExitCode;
        }
    }

    public static int tkfx_saturated_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        Console.WriteLine("Testing 16 bit saturation");
        if (saturate16(10000) != 10000 ||
            saturate16(-10000) != -10000 ||
            saturate16(32767) != 32767 ||
            saturate16(-32768) != -32768 ||
            saturate16(32768) != 32767 ||
            saturate16(-32769) != -32768) {
            Fail();
        }

        Console.WriteLine("Testing 15 bit saturation");
        if (saturate15(10000) != 10000 ||
            saturate15(-10000) != -10000 ||
            saturate15(16383) != 16383 ||
            saturate15(-16384) != -16384 ||
            saturate15(16384) != 16383 ||
            saturate15(-16385) != -16384) {
            Fail();
        }

        Console.WriteLine("Testing 16 bit unsigned saturation");
        if (saturateu16(10000) != 10000 ||
            saturateu16(32767) != 32767 ||
            saturateu16(65535) != 65535 ||
            saturateu16(65536) != 65535) {
            Fail();
        }

        Console.WriteLine("Testing 8 bit unsigned saturation");
        if (saturateu8(100) != 100 ||
            saturateu8(127) != 127 ||
            saturateu8(255) != 255 ||
            saturateu8(256) != 255) {
            Fail();
        }

        Console.WriteLine("Testing 16 bit saturation from float");
        if (fsaturatef(10000.0f) != 10000 ||
            fsaturatef(-10000.0f) != -10000 ||
            fsaturatef(32767.0f) != 32767 ||
            fsaturatef(-32768.0f) != -32768 ||
            fsaturatef(32768.0f) != 32767 ||
            fsaturatef(-32769.0f) != -32768) {
            Fail();
        }

        Console.WriteLine("Testing 16 bit saturation from double");
        if (fsaturate(10000.0) != 10000 ||
            fsaturate(-10000.0) != -10000 ||
            fsaturate(32767.0) != 32767 ||
            fsaturate(-32768.0) != -32768 ||
            fsaturate(32768.0) != 32767 ||
            fsaturate(-32769.0) != -32768) {
            Fail();
        }

        Console.WriteLine("Testing 16 bit fast saturation from float");
        if (ffastsaturatef(10000.0f) != 10000 ||
            ffastsaturatef(-10000.0f) != -10000 ||
            ffastsaturatef(32767.0f) != 32767 ||
            ffastsaturatef(-32768.0f) != -32768 ||
            ffastsaturatef(32768.0f) != 32767 ||
            ffastsaturatef(-32769.0f) != -32768) {
            Fail();
        }

        Console.WriteLine("Testing 16 bit fast saturation from double");
        if (ffastsaturate(10000.0) != 10000 ||
            ffastsaturate(-10000.0) != -10000 ||
            ffastsaturate(32767.0) != 32767 ||
            ffastsaturate(-32768.0) != -32768 ||
            ffastsaturate(32768.0) != 32767 ||
            ffastsaturate(-32769.0) != -32768) {
            Fail();
        }

        Console.WriteLine("Testing 16 bit float saturation from float");
        if (ffsaturatef(10000.0f) != 10000.0f ||
            ffsaturatef(-10000.0f) != -10000.0f ||
            ffsaturatef(32767.0f) != 32767.0f ||
            ffsaturatef(-32768.0f) != -32768.0f ||
            ffsaturatef(32768.0f) != 32767.0f ||
            ffsaturatef(-32769.0f) != -32768.0f) {
            Fail();
        }

        Console.WriteLine("Testing 16 bit double saturation from double");
        if (ffsaturate(10000.0) != 10000.0 ||
            ffsaturate(-10000.0) != -10000.0 ||
            ffsaturate(32767.0) != 32767.0 ||
            ffsaturate(-32768.0) != -32768.0 ||
            ffsaturate(32768.0) != 32767.0 ||
            ffsaturate(-32769.0) != -32768.0) {
            Fail();
        }

        Console.WriteLine("Testing 16 bit add");
        if (sat_add16(10000, 10000) != 20000 ||
            sat_add16(10000, -10000) != 0 ||
            sat_add16(-10000, 10000) != 0 ||
            sat_add16(-10000, -10000) != -20000 ||
            sat_add16(-30000, -30000) != short.MinValue ||
            sat_add16(30000, 30000) != short.MaxValue ||
            sat_add16(short.MinValue, short.MinValue) != short.MinValue ||
            sat_add16(short.MaxValue, short.MaxValue) != short.MaxValue) {
            Fail();
        }

        Console.WriteLine("Testing 32 bit add");
        if (sat_add32(10000, 10000) != 20000 ||
            sat_add32(10000, -10000) != 0 ||
            sat_add32(-10000, 10000) != 0 ||
            sat_add32(-10000, -10000) != -20000 ||
            sat_add32(-2000000000, -2000000000) != int.MinValue ||
            sat_add32(2000000000, 2000000000) != int.MaxValue) {
            Fail();
        }

        Console.WriteLine("Testing 16 bit subtract");
        if (sat_sub16(10000, 10000) != 0 ||
            sat_sub16(10000, -10000) != 20000 ||
            sat_sub16(-10000, 10000) != -20000 ||
            sat_sub16(-10000, -10000) != 0 ||
            sat_sub16(-30000, 30000) != short.MinValue ||
            sat_sub16(30000, -30000) != short.MaxValue ||
            sat_sub16(short.MinValue, short.MaxValue) != short.MinValue ||
            sat_sub16(short.MaxValue, short.MinValue) != short.MaxValue) {
            Fail();
        }

        Console.WriteLine("Testing 32 bit subtract");
        if (sat_sub32(10000, 10000) != 0 ||
            sat_sub32(10000, -10000) != 20000 ||
            sat_sub32(-10000, 10000) != -20000 ||
            sat_sub32(-10000, -10000) != 0 ||
            sat_sub32(-2000000000, 2000000000) != int.MinValue ||
            sat_sub32(2000000000, -2000000000) != int.MaxValue) {
            Fail();
        }

        Console.WriteLine("Testing 16 x 16 => 16 bit multiply");
        if (sat_mul16(100, 100) != 0 ||
            sat_mul16(255, 255) != 1 ||
            sat_mul16(short.MaxValue, short.MinValue) != -32767 ||
            sat_mul16(short.MinValue, short.MaxValue) != -32767 ||
            sat_mul16(short.MaxValue, short.MaxValue) != 32766 ||
            sat_mul16(short.MinValue, short.MinValue) != short.MaxValue) {
            Fail();
        }

        Console.WriteLine("Testing 16 x 16 => 32 bit multiply");
        if (sat_mul32_16(100, 100) != 20000 ||
            sat_mul32_16(-100, 100) != -20000 ||
            sat_mul32_16(short.MaxValue, short.MinValue) != -2147418112 ||
            sat_mul32_16(short.MinValue, short.MaxValue) != -2147418112 ||
            sat_mul32_16(short.MaxValue, short.MaxValue) != 2147352578 ||
            sat_mul32_16(short.MinValue, short.MinValue) != int.MaxValue) {
            Fail();
        }

        Console.WriteLine("Testing 32 + 16 x 16 => 32 bit MAC");
        if (sat_mac32_16(123, 100, 100) != 123 + 20000 ||
            sat_mac32_16(123, -100, 100) != 123 - 20000 ||
            sat_mac32_16(123, short.MaxValue, short.MinValue) != 123 - 2147418112 ||
            sat_mac32_16(123, short.MinValue, short.MaxValue) != 123 - 2147418112 ||
            sat_mac32_16(123, short.MaxValue, short.MaxValue) != 123 + 2147352578 ||
            sat_mac32_16(123, short.MinValue, short.MinValue) != int.MaxValue) {
            Fail();
        }

        Console.WriteLine("Testing 32 - 16 x 16 => 32 bit MSU");
        if (sat_msu32_16(123, 100, 100) != 123 - 20000 ||
            sat_msu32_16(123, -100, 100) != 123 + 20000 ||
            sat_msu32_16(123, short.MaxValue, short.MinValue) != 123 + 2147418112 ||
            sat_msu32_16(123, short.MinValue, short.MaxValue) != 123 + 2147418112 ||
            sat_msu32_16(123, short.MaxValue, short.MaxValue) != 123 - 2147352578 ||
            sat_msu32_16(123, short.MinValue, short.MinValue) != 123 - int.MaxValue) {
            Fail();
        }

        Console.WriteLine("Testing 16 bit absolute");
        if (sat_abs16(10000) != 10000 ||
            sat_abs16(-10000) != 10000 ||
            sat_abs16(short.MaxValue) != short.MaxValue ||
            sat_abs16(short.MinValue) != short.MaxValue) {
            Fail();
        }

        Console.WriteLine("Tests passed.");
        return 0;
    }

    private static void Fail() {
        Console.WriteLine("Test failed.");
        throw new SaturatedTestsExitException(2);
    }

    public sealed class SaturatedTestsExitException : Exception {
        public SaturatedTestsExitException(int exitCode)
            : base("saturated_tests exited with code " + exitCode + ".") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }
}
