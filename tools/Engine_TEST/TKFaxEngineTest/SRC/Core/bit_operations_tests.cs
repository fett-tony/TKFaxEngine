/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * bit_operations_tests.cs - Bit operation verification tests.
 *
 * Original C test written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2006 Steve Underwood.
 *
 * Managed C# port. The original exhaustive loop sizes are retained.
 */

#nullable enable

using static TKFaxEngine.BitOperationsApi;

namespace TKFaxEngineTest.Core.Test;

public static class bit_operations_tests {
    private static readonly byte[] from = new byte[1_000_000];
    private static readonly byte[] to = new byte[1_000_000];

    public static int main(string[] testArguments) {
        try {
            return tkfx_bit_operations_tests_body_main(testArguments);
        } catch (BitOperationsTestsExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_bit_operations_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        var random = new Random(1);
        uint x = 0;

        for (int i = 0; i < 100_000; i++) {
            int expectedTop = top_bit_dumb(x);
            int actualTop = top_bit(x);
            if (expectedTop != actualTop)
                Fail($"Test failed: top bit mismatch 0x{x:X8} -> {expectedTop} {actualTop}");

            int expectedBottom = bottom_bit_dumb(x);
            int actualBottom = bottom_bit(x);
            if (expectedBottom != actualBottom)
                Fail($"Test failed: bottom bit mismatch 0x{x:X8} -> {expectedBottom} {actualBottom}");

            x = unchecked((uint)random.Next());
        }

        for (int i = 0; i < 256; i++) {
            byte expected = bit_reverse8_dumb(unchecked((byte)i));
            byte actual = bit_reverse8(unchecked((byte)i));
            if (expected != actual)
                Fail($"Test failed: bit reverse 8 - {i:X2} {expected:X2} {actual:X2}");
        }

        random.NextBytes(from);
        bit_reverse(to, from, from.Length);

        for (int i = 0; i < from.Length; i++) {
            byte expected = bit_reverse8_dumb(from[i]);
            if (expected != to[i]) {
                Fail(
                    $"Test failed: bit reverse - at {i}, {from[i]:X2} " +
                    $"{bit_reverse8(from[i]):X2} {to[i]:X2}");
            }
        }

        for (int i = 0; i < 256; i++) {
            x = unchecked(
                (uint)i |
                ((uint)((i + 1) & 0xFF) << 8) |
                ((uint)((i + 2) & 0xFF) << 16) |
                ((uint)((i + 3) & 0xFF) << 24));

            uint expected = bit_reverse_4bytes_dumb(x);
            uint actual = bit_reverse_4bytes(x);
            if (expected != actual)
                Fail($"Test failed: bit reverse 4 bytes - {x:X8} {expected:X8} {actual:X8}");
        }

        for (int i = 0; i < 65_536; i++) {
            ushort expected = bit_reverse16_dumb(unchecked((ushort)i));
            ushort actual = bit_reverse16(unchecked((ushort)i));
            if (expected != actual)
                Fail($"Test failed: bit reverse 16 - {i:X} {expected:X} {actual:X}");
        }

        for (int i = 0; i < 0x7FFFFF00; i += 127) {
            uint value = unchecked((uint)i);
            uint expected = bit_reverse32_dumb(value);
            uint actual = bit_reverse32(value);
            if (expected != actual)
                Fail($"Test failed: bit reverse 32 - {i} {expected:X8} {actual:X8}");
        }

        for (int i = 0; i < 256; i++) {
            int actual = parity8(unchecked((byte)i));
            int expected = parity8_dumb(unchecked((byte)i));
            if (actual != expected)
                Fail($"Test failed: parity 8 - {i:X} {actual:X} {expected:X}");
        }

        for (int i = -1; i < 32; i++) {
            uint value = 1u << i;
            uint most = most_significant_one32(value);
            if (most != value)
                Fail($"Test failed: most significant one 32 - {i:X} {most:X8} {value:X8}");

            uint least = least_significant_one32(value);
            if (least != value)
                Fail($"Test failed: least significant one 32 - {i:X} {least:X8} {value:X8}");
        }

        for (uint i = 0x80000000u; i < 0x800FFFFFu; i++) {
            int expected = one_bits32_dumb(i);
            int actual = one_bits32(i);
            if (expected != actual)
                Fail($"Test failed: one bits - {unchecked((int)i)}, {expected:X} {actual:X}");
        }

        Console.WriteLine("Tests passed.");
        return 0;
    }

    private static int top_bit_dumb(uint data) {
        if (data == 0)
            return -1;

        for (int i = 31; i >= 0; i--) {
            if ((data & (1u << i)) != 0)
                return i;
        }

        return -1;
    }

    private static int bottom_bit_dumb(uint data) {
        if (data == 0)
            return -1;

        for (int i = 0; i < 32; i++) {
            if ((data & (1u << i)) != 0)
                return i;
        }

        return -1;
    }

    private static byte bit_reverse8_dumb(byte data) {
        int result = 0;
        for (int i = 0; i < 8; i++) {
            result = (result << 1) | (data & 1);
            data >>= 1;
        }

        return unchecked((byte)result);
    }

    private static uint bit_reverse_4bytes_dumb(uint data) {
        uint result = 0;
        for (int i = 0; i < 8; i++) {
            result = unchecked((result << 1) | (data & 0x01010101u));
            data >>= 1;
        }

        return result;
    }

    private static ushort bit_reverse16_dumb(ushort data) {
        ushort result = 0;
        for (int i = 0; i < 16; i++) {
            result = unchecked((ushort)((result << 1) | (data & 1)));
            data >>= 1;
        }

        return result;
    }

    private static uint bit_reverse32_dumb(uint data) {
        uint result = 0;
        for (int i = 0; i < 32; i++) {
            result = unchecked((result << 1) | (data & 1u));
            data >>= 1;
        }

        return result;
    }

    private static int parity8_dumb(byte value) {
        byte parity = 0;
        for (int i = 0; i < 8; i++) {
            parity ^= unchecked((byte)(value & 1));
            value >>= 1;
        }

        return parity;
    }

    private static int one_bits32_dumb(uint value) {
        int bits = 0;
        for (int i = 0; i < 32; i++) {
            if ((value & 1u) != 0)
                bits++;

            value >>= 1;
        }

        return bits;
    }

    private static void Fail(string message) {
        Console.WriteLine(message);
        throw new BitOperationsTestsExitException(2);
    }

    public sealed class BitOperationsTestsExitException : Exception {
        public BitOperationsTestsExitException(int exitCode)
            : base($"bit_operations_tests requested exit code {exitCode}.") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }
}
