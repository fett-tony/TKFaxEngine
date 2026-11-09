/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * bitstream_tests.cs - Bitstream composition and decomposition tests.
 *
 * Original C test written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2007 Steve Underwood.
 *
 * Managed C# port preserving both LSB-first and MSB-first test cycles.
 */

#nullable enable

using TKFaxEngine.Modem;
using static TKFaxEngine.Modem.BitstreamApi;

namespace TKFaxEngineTest.Modem.Test;

public static class bitstream_tests {
    private const uint PATTERN = 0x1111111u;
    private const int SEQUENCE_LENGTH = 17;

    private static readonly byte[] buffer = new byte[256];

    private static readonly byte[] left =
    {
        0x28, 0xC8, 0xAE, 0x67, 0x74, 0x43, 0x32, 0xAA, 0xAE, 0xED,
        0x99, 0x8E, 0xEE, 0xEE, 0xEE, 0xFF, 0xFF, 0x88, 0x88, 0x00
    };

    private static readonly byte[] right =
    {
        0xD2, 0x90, 0xCA, 0x7C, 0x87, 0x28, 0x33, 0x55, 0xED, 0x2E,
        0x33, 0xEB, 0xEE, 0xDC, 0xDD, 0xFF, 0xFF, 0x10, 0x11, 0x01
    };

    public static int main(string[] testArguments) {
        try {
            return tkfx_bitstream_tests_body_main(testArguments);
        } catch (BitstreamTestsExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_bitstream_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        RunDirection(lsbFirst: true, expected: right);
        RunDirection(lsbFirst: false, expected: left);

        Console.WriteLine("Tests passed.");
        return 0;
    }

    private static void RunDirection(bool lsbFirst, byte[] expected) {
        Array.Clear(buffer, 0, buffer.Length);
        BitstreamState state = bitstream_init(null, lsbFirst ? 1 : 0);
        int writeOffset = 0;
        int totalBits = 0;

        for (int i = 0; i < SEQUENCE_LENGTH; i++) {
            bitstream_put(state, buffer, ref writeOffset, unchecked((uint)i * PATTERN), i + 1);
            totalBits += i + 1;
        }

        bitstream_flush(state, buffer, ref writeOffset);

        Console.WriteLine($"{totalBits} bits written");
        PrintBytes(buffer, writeOffset);
        PrintBytes(expected, expected.Length);

        if (writeOffset != expected.Length ||
            !buffer.AsSpan(0, expected.Length).SequenceEqual(expected)) {
            Fail("Test failed");
        }

        state = bitstream_init(state, lsbFirst ? 1 : 0);
        int readOffset = 0;

        for (int i = 0; i < SEQUENCE_LENGTH; i++) {
            uint actual = bitstream_get(state, buffer, ref readOffset, i + 1);
            uint mask = (1u << (i + 1)) - 1u;
            uint wanted = unchecked((PATTERN * (uint)i) & mask);

            if (actual != wanted) {
                Console.WriteLine($"Error 0x{actual:X} 0x{wanted:X}");
                Fail("Test failed");
            }
        }

        bitstream_release(state);
    }

    private static void PrintBytes(byte[] data, int length) {
        for (int i = 0; i < length; i++)
            Console.Write($"{data[i]:X2} ");

        Console.WriteLine();
    }

    private static void Fail(string message) {
        Console.WriteLine(message);
        throw new BitstreamTestsExitException(2);
    }

    public sealed class BitstreamTestsExitException : Exception {
        public BitstreamTestsExitException(int exitCode)
            : base($"bitstream_tests requested exit code {exitCode}.") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }
}
