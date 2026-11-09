/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * t4_t6_tests.cs - ITU T.4 and T.6 image compression/decompression tests
 *
 * Managed C# port of the supplied spanDSP t4_t6_tests.c source. The original
 * sixteen rows, T.4/T.6 compression sequence, bit-stream checks, EOL checks,
 * bit-at-a-time mode and block mode are retained.
 */

#nullable enable

using TKFaxEngine;
using TKFaxEngine.FaxImage;
using static TKFaxEngine.FaxImage.t4_t6_decode;
using static TKFaxEngine.FaxImage.t4_t6_encode;
using static TKFaxEngine.FaxImage.t4_rx;

namespace TKFaxEngineTest.Image.Test;

public static class T4T6Tests {
    private const int XSIZE = 1728;
    private const int TEST_ROWS = 16;
    private const int RESET_PAGE_END = -1_000_000;

    private static readonly string FILL_70 = new(' ', 70);
    private static readonly string FILL_80 = new(' ', 80);
    private static readonly string FILL_100 = new(' ', 100);
    private static readonly string FILL_670 =
        FILL_100 + FILL_100 + FILL_100 + FILL_100 + FILL_100 + FILL_100 + FILL_70;
    private static readonly string FILL_980 =
        FILL_100 + FILL_100 + FILL_100 + FILL_100 + FILL_100 +
        FILL_100 + FILL_100 + FILL_100 + FILL_100 + FILL_80;

    private static readonly string[] testPatterns = {
        "XXXXXX              " + FILL_980 + "  XXX  XXX X                  " + FILL_670 + "                        XXXX",
        "XXXXXX              " + FILL_980 + "     XXX   X                  " + FILL_670 + "                        XXXX",
        " XXXX               " + FILL_980 + "    XXXXXXX                   " + FILL_670 + "                        XX  ",
        "XXXXX               " + FILL_980 + "XX       XX                   " + FILL_670 + "                        XXXX",
        "XXX                 " + FILL_980 + " XX  XX   XX        XXX       " + FILL_670 + "                      X     ",
        "                    " + FILL_980 + " X       XXX   XXXX           " + FILL_670 + "                      X   XX",
        "XXXXX               " + FILL_980 + "                              " + FILL_670 + "                        XXXX",
        "  XXX               " + FILL_980 + "                              " + FILL_670 + "                        XX  ",
        "         XX         " + FILL_980 + "                              " + FILL_670 + "                      X  XXX",
        "XXX       X         " + FILL_980 + "                              " + FILL_670 + "                      X     ",
        "                    " + FILL_980 + "                              " + FILL_670 + "                         XX ",
        "                    " + FILL_980 + "                              " + FILL_670 + "                            ",
        "                    " + FILL_980 + "                              " + FILL_670 + "                  XXXXXXXXXX",
        "                    " + FILL_980 + "                              " + FILL_670 + "              XXXXXX  XXXXXX",
        "                    " + FILL_980 + "                              " + FILL_670 + "                   XX  XXXXX",
        "                    " + FILL_980 + "                              " + FILL_670 + "                   XX       "
    };

    private static int readRow;
    private static int writeRow;
    private static int consecutiveEols;
    private static int maximumConsecutiveEols;
    private static int consecutiveZeros;
    private static int consecutiveOnes;
    private static int eolZeros;
    private static int eolOnes;
    private static int expectedEols;
    private static int pageEndMarks;

    public sealed class ExitException : Exception {
        public ExitException(int exitCode) => ExitCode = exitCode;
        public int ExitCode { get; }
    }

    private static void Exit(int code) => throw new ExitException(code);

    private static int RowReadHandler(object? userData, Span<byte> destination, int len) {
        Span<byte> destination_span = destination[..len];
        _ = userData;
        if (readRow >= TEST_ROWS) {
            readRow = 0;
            return 0;
        }

        string source = testPatterns[readRow++];
        destination_span.Clear();
        int sourcePosition = 0;
        for (int i = 0; i < destination_span.Length; ++i) {
            for (int j = 0; j < 8; ++j) {
                if (sourcePosition >= source.Length)
                    Exit(2);
                if (source[sourcePosition++] != ' ')
                    destination_span[i] |= checked((byte)(0x80 >> j));
            }
        }

        if (sourcePosition < source.Length)
            Console.WriteLine($"Oops - '{source[sourcePosition]}' at end of row {readRow}");
        return len;
    }

    private static int RowWriteHandler(object? userData, ReadOnlySpan<byte> row, int len) {
        row = row[..len];
        _ = userData;
        if (row.IsEmpty)
            return 0;

        string source = testPatterns[writeRow++];
        if (writeRow >= TEST_ROWS)
            writeRow = 0;

        byte[] reference = new byte[row.Length];
        int sourcePosition = 0;
        for (int i = 0; i < reference.Length; ++i) {
            for (int j = 0; j < 8; ++j) {
                if (sourcePosition >= source.Length)
                    Exit(2);
                if (source[sourcePosition++] != ' ')
                    reference[i] |= checked((byte)(0x80 >> j));
            }
        }

        if (sourcePosition < source.Length)
            Console.WriteLine($"Oops - '{source[sourcePosition]}' at end of row {writeRow}");

        if (!row.SequenceEqual(reference)) {
            Console.WriteLine($"Test failed at row {writeRow}");
            Exit(2);
        }
        return 0;
    }

    private static int DetectPageEnd(int bit, int pageEnded) {
        if (bit == RESET_PAGE_END) {
            consecutiveEols = 0;
            maximumConsecutiveEols = 0;
            consecutiveZeros = 0;
            consecutiveOnes = 0;
            pageEndMarks = 0;
            eolZeros = 11;
            eolOnes = pageEnded == T4_COMPRESSION_T4_2D ? 2 : 1;
            expectedEols = pageEnded == T4_COMPRESSION_T6 ? 2 : 6;
            return 0;
        }

        if (bit == 0) {
            ++consecutiveZeros;
            consecutiveOnes = 0;
        } else if (bit == 1) {
            if (++consecutiveOnes == eolOnes) {
                if (consecutiveEols == 0 && consecutiveZeros >= eolZeros)
                    ++consecutiveEols;
                else if (consecutiveZeros == eolZeros)
                    ++consecutiveEols;
                else
                    consecutiveEols = 0;

                consecutiveZeros = 0;
                consecutiveOnes = 0;
            }

            if (maximumConsecutiveEols < consecutiveEols)
                maximumConsecutiveEols = consecutiveEols;
        } else if (bit == (int)SignalStatus.EndOfData) {
            if (pageEndMarks == 0) {
                if (maximumConsecutiveEols != expectedEols) {
                    Console.WriteLine(
                        $"Only {maximumConsecutiveEols} EOLs (should be {expectedEols})");
                    return 2;
                }
                consecutiveZeros = 0;
                consecutiveEols = 0;
                maximumConsecutiveEols = 0;
            }

            if (pageEnded == 0) {
                if (++pageEndMarks > 50) {
                    Console.WriteLine("Receiver missed the end of page mark");
                    return 2;
                }
                return 0;
            }
            return 1;
        }
        return 0;
    }

    private static bool TryReadOptionValue(string[] testArguments, ref int index, string current, out string value) {
        if (current.Length > 2) {
            value = current[2..];
            return true;
        }
        if (index + 1 < testArguments.Length) {
            value = testArguments[++index];
            return true;
        }
        value = string.Empty;
        return false;
    }

    private static int ParseCompression(string value) => value switch {
        "T41D" => T4_COMPRESSION_T4_1D,
        "T42D" => T4_COMPRESSION_T4_2D,
        "T6" => T4_COMPRESSION_T6,
        _ => throw new ArgumentException($"Unrecognised compression type {value}")
    };

    public static int tkfx_t4_t6_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);
        int[] compressionSequence = {
            T4_COMPRESSION_T4_1D,
            T4_COMPRESSION_T4_2D,
            T4_COMPRESSION_T6
        };

        int compression = T4_COMPRESSION_T4_1D;
        bool fixedCompression = false;
        int minimumRowBits = 50;
        int blockSize = 0;
        int testsFailed = 0;

        for (int index = 0; index < testArguments.Length; ++index) {
            string argument = testArguments[index];
            if (argument.Length < 2 || argument[0] != '-')
                continue;

            char option = argument[1];
            if (!TryReadOptionValue(testArguments, ref index, argument, out string value))
                Exit(2);

            switch (option) {
                case 'b':
                    if (!int.TryParse(value, out blockSize))
                        Exit(2);
                    if (blockSize > 1024)
                        blockSize = 1024;
                    if (blockSize < 0)
                        Exit(2);
                    break;
                case 'c':
                    try {
                        compression = ParseCompression(value);
                    } catch (ArgumentException) {
                        Console.Error.WriteLine($"Unrecognised compression type {value}");
                        Exit(2);
                    }
                    fixedCompression = true;
                    break;
                case 'm':
                    if (!int.TryParse(value, out minimumRowBits))
                        Exit(2);
                    break;
                default:
                    Exit(2);
                    break;
            }
        }

        Console.WriteLine("Testing image_function->compress->decompress->image_function");
        t4_t6_encode_state_t encoder = t4_t6_encode_init(
            null,
            compression,
            XSIZE,
            -1,
            RowReadHandler,
            null)
            ?? throw new InvalidOperationException("Cannot initialize the T.4/T.6 encoder.");
        t4_t6_encode_set_min_bits_per_row(encoder, minimumRowBits);
        t4_t6_encode_set_max_2d_rows_per_1d_row(encoder, 2);

        t4_t6_decode_state_t decoder = t4_t6_decode_init(
            null,
            compression,
            XSIZE,
            RowWriteHandler,
            null)
            ?? throw new InvalidOperationException("Cannot initialize the T.4/T.6 decoder.");

        int compressionIndex = 0;
        for (; ; ) {
            if (!fixedCompression) {
                if (compressionIndex >= compressionSequence.Length)
                    break;
                compression = compressionSequence[compressionIndex++];
            }

            if (t4_t6_encode_set_encoding(encoder, compression) != 0 ||
                t4_t6_decode_set_encoding(decoder, compression) != 0 ||
                t4_t6_encode_restart(encoder, XSIZE, -1) != 0 ||
                t4_t6_decode_restart(decoder, XSIZE) != 0) {
                break;
            }

            DetectPageEnd(RESET_PAGE_END, compression);
            int endOfPage = T4_DECODE_MORE_DATA;
            int endMarks = 0;

            if (blockSize == 0) {
                for (; ; ) {
                    int bit = t4_t6_encode_get_bit(encoder);
                    int result = DetectPageEnd(bit, endOfPage);
                    if (result != 0) {
                        if (result != 1) {
                            Console.WriteLine("Test failed");
                            Exit(2);
                        }
                        break;
                    }
                    if (endOfPage == T4_DECODE_MORE_DATA)
                        endOfPage = t4_t6_decode_put_bit(decoder, bit & 1);
                }
            } else {
                byte[] chunk = new byte[1024];
                do {
                    int length = t4_t6_encode_get(encoder, chunk.AsSpan(0, blockSize), blockSize);
                    if (length == 0) {
                        if (++endMarks > 50) {
                            Console.WriteLine("Receiver missed the end of page mark");
                            ++testsFailed;
                            break;
                        }
                        chunk[0] = 0xFF;
                        length = 1;
                    }
                    endOfPage = t4_t6_decode_put(decoder, chunk, length);
                } while (endOfPage == T4_DECODE_MORE_DATA);
            }

            if (fixedCompression)
                break;
        }

        _ = t4_t6_encode_free(encoder);
        _ = t4_t6_decode_free(decoder);

        if (testsFailed > 0) {
            Console.WriteLine($"{testsFailed} tests failed");
            return 2;
        }

        Console.WriteLine("Tests passed");
        return 0;
    }
}
