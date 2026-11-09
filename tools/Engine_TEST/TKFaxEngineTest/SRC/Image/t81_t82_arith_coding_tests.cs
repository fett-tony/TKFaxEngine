/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * t81_t82_arith_coding_tests.cs - Managed C# port of the ITU-T T.81/T.82
 * arithmetic encoder/decoder test from spanDSP.
 *
 * Original test written by Steve Underwood.
 * Copyright (C) 2009 Steve Underwood.
 * Licensed under the GNU General Public License version 2.
 */

#nullable enable

using TKFaxEngine.FaxImage;

namespace TKFaxEngineTest.Image.Test;

public static class t81_t82_arith_coding_tests {
    private const int MsgSize = 10_000;
    private const int Sde71Length = 30;
    private const int Sde71FullLength = 32;

    private static readonly byte[] Msg = new byte[MsgSize];
    private static int _msgLength;

    private static readonly ushort[] Pix71 =
    {
        0x05E0, 0x0000, 0x8B00, 0x01C4, 0x1700, 0x0034, 0x7FFF, 0x1A3F,
        0x951B, 0x05D8, 0x1D17, 0xE770, 0x0000, 0x0000, 0x0656, 0x0E6A
    };

    private static readonly ushort[] Cx71 =
    {
        0x0FE0, 0x0000, 0x0F00, 0x00F0, 0xFF00, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000
    };

    private static readonly byte[] Sde71 =
    {
        0x69, 0x89, 0x99, 0x5C, 0x32, 0xEA, 0xFA, 0xA0,
        0xD5, 0xFF, 0x00, 0x52, 0x7F, 0xFF, 0x00, 0xFF,
        0x00, 0xFF, 0x00, 0xC0, 0x00, 0x00, 0x00, 0x3F,
        0xFF, 0x00, 0x2D, 0x20, 0x82, 0x91, 0xFF, 0x02
    };

    private static void write_byte(byte value) {
        if (_msgLength < MsgSize)
            Msg[_msgLength++] = value;
    }

    public static int tkfx_t81_t82_arith_coding_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        Console.WriteLine("T.81/T.82 arithmetic encoder tests, from ITU-T T.82");
        Console.WriteLine();
        Console.WriteLine("Arithmetic encoder tests from ITU-T T.82/7.1");

        T81T82ArithmeticEncoder encoder = new(write_byte);
        _msgLength = 0;

        for (int i = 0; i < 16; i++) {
            for (int j = 0; j < 16; j++) {
                encoder.Encode(
                    (Cx71[i] >> (15 - j)) & 1,
                    (Pix71[i] >> (15 - j)) & 1);
            }
        }

        encoder.Flush();

        if (_msgLength != Sde71Length || !EqualPrefix(Msg, Sde71, Sde71Length)) {
            Console.Write("Encoded data:  ");
            PrintHex(Msg, _msgLength);
            Console.Write("Expected data: ");
            PrintHex(Sde71, Sde71Length);
            Console.WriteLine("Test failed");
            return 2;
        }

        Console.WriteLine("Test passed");
        Console.WriteLine("Arithmetic decoder tests from ITU-T T.82/7.1");
        Console.WriteLine("Decoding byte by byte...");

        if (!DecodeByteByByte()) {
            Console.WriteLine("Test failed");
            return 2;
        }

        Console.WriteLine("Test passed");
        Console.WriteLine("Decoding chunk by chunk...");

        if (!DecodeWholeChunk()) {
            Console.WriteLine("Test failed");
            return 2;
        }

        Console.WriteLine("Test passed");
        Console.WriteLine("Tests passed");
        return 0;
    }

    private static bool DecodeByteByByte() {
        T81T82ArithmeticDecoder decoder = new();
        decoder.SetInput(Sde71.AsSpan(0, 1));
        int nextByte = 1;

        for (int i = 0; i < 16; i++) {
            for (int j = 0; j < 16; j++) {
                int pixel;
                while (true) {
                    pixel = decoder.Decode((Cx71[i] >> (15 - j)) & 1);
                    if (pixel >= 0)
                        break;

                    if (nextByte >= Sde71FullLength) {
                        Console.WriteLine($"Bad pixel {i * 16 + j + 1}, input exhausted.");
                        return false;
                    }

                    decoder.AppendInput(Sde71.AsSpan(nextByte, 1));
                    nextByte++;
                }

                int expected = (Pix71[i] >> (15 - j)) & 1;
                if (pixel != expected) {
                    Console.WriteLine($"Bad PIX ({pixel}) at pixel {i * 16 + j + 1}.");
                    return false;
                }
            }
        }

        while (nextByte < Sde71FullLength) {
            decoder.AppendInput(Sde71.AsSpan(nextByte, 1));
            nextByte++;
        }

        if (decoder.RemainingInputBytes != 2) {
            Console.WriteLine($"{decoder.RemainingInputBytes - 2} bytes left after decoder finished.");
            return false;
        }

        return true;
    }

    private static bool DecodeWholeChunk() {
        T81T82ArithmeticDecoder decoder = new();
        decoder.SetInput(Sde71.AsSpan(0, Sde71FullLength));

        for (int i = 0; i < 16; i++) {
            for (int j = 0; j < 16; j++) {
                int pixel = decoder.Decode((Cx71[i] >> (15 - j)) & 1);
                if (pixel < 0) {
                    Console.WriteLine($"Bad pixel {i * 16 + j + 1}, decoder returned {pixel}.");
                    return false;
                }

                int expected = (Pix71[i] >> (15 - j)) & 1;
                if (pixel != expected) {
                    Console.WriteLine($"Bad PIX ({pixel}) at pixel {i * 16 + j + 1}.");
                    return false;
                }
            }
        }

        if (decoder.RemainingInputBytes != 2) {
            Console.WriteLine($"{decoder.RemainingInputBytes - 2} bytes left after decoder finished.");
            return false;
        }

        return true;
    }

    private static bool EqualPrefix(byte[] left, byte[] right, int length) {
        if (left.Length < length || right.Length < length)
            return false;

        for (int i = 0; i < length; i++) {
            if (left[i] != right[i])
                return false;
        }

        return true;
    }

    private static void PrintHex(byte[] data, int length) {
        for (int i = 0; i < length; i++)
            Console.Write(data[i].ToString("X2"));
        Console.WriteLine();
    }
}
