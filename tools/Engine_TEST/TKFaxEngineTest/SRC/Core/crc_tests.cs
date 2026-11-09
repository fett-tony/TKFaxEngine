/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * crc_tests.cs - ITU CRC-16 and CRC-32 tests.
 *
 * Original C test written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2003 Steve Underwood.
 *
 * Managed C# port preserving the original PRNG, message lengths,
 * iteration counts and byte-by-byte versus bit-by-bit comparison.
 */

#nullable enable

using static TKFaxEngine.CrcApi;

namespace TKFaxEngineTest.Core.Test;

public static class crc_tests {
    private static readonly byte[] buf = new byte[1000];
    private static uint rndnum = 1_234_567u;
    private static int ref_len;

    public static int main(string[] testArguments) {
        try {
            return tkfx_crc_tests_body_main(testArguments);
        } catch (CrcTestsExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_crc_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        Console.WriteLine("CRC module tests");
        Console.WriteLine("Testing the CRC-16 routines");

        for (int i = 0; i < 100; i++) {
            ref_len = cook_up_msg(buf);
            int len = crc_itu16_append(buf, ref_len);

            if (!crc_itu16_check(buf, len))
                Fail("CRC-16 failure");
        }

        Console.WriteLine("Test passed.\n");
        Console.WriteLine("Testing the CRC-16 byte by byte and bit by bit routines");

        for (int i = 0; i < 100; i++) {
            ref_len = cook_up_msg(buf);

            ushort crc16a = 0xFFFF;
            crc16a = crc_itu16_calc(buf, ref_len, crc16a);

            ushort crc16b = 0xFFFF;
            for (int j = 0; j < ref_len; j++)
                crc16b = crc_itu16_bits(buf[j], 8, crc16b);

            if (crc16a != crc16b)
                Fail("CRC-16 failure");
        }

        Console.WriteLine("Test passed.\n");
        Console.WriteLine("Testing the CRC-32 routines");

        for (int i = 0; i < 100; i++) {
            ref_len = cook_up_msg(buf);
            int len = crc_itu32_append(buf, ref_len);

            if (!crc_itu32_check(buf, len))
                Fail("CRC-32 failure");
        }

        Console.WriteLine("Test passed.");
        return 0;
    }

    private static int my_rand() {
        rndnum = unchecked(1_664_525u * rndnum + 1_013_904_223u);
        return unchecked((int)(rndnum >> 8));
    }

    private static int cook_up_msg(byte[] destination) {
        int len = (my_rand() & 0x3F) + 100;

        for (int i = 0; i < len; i++)
            destination[i] = unchecked((byte)my_rand());

        return len;
    }

    private static void Fail(string message) {
        Console.WriteLine(message);
        throw new CrcTestsExitException(2);
    }

    public sealed class CrcTestsExitException : Exception {
        public CrcTestsExitException(int exitCode)
            : base($"crc_tests requested exit code {exitCode}.") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }
}
