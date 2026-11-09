/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * t85_tests.c - ITU T.85 FAX image compression and decompression tests
 *
 * Managed C# port preserving the supplied spanDSP T.82/T.85 test sequence.
 */

#nullable enable

using System.Text;
using TKFaxEngine.FaxImage;
using static TKFaxEngine.FaxImage.T85Api;

namespace TKFaxEngineTest.Image.Test;

public static class T85Tests {
    private const int TESTBUF_SIZE = 400_000;
    private const int TEST_IMAGE_SIZE = 1951 * 1960 / 8;

    private static readonly byte[] testbuf = new byte[TESTBUF_SIZE];
    private static readonly byte[] testImage = new byte[TEST_IMAGE_SIZE];
    private static int testbufLen;
    private static int readRow;
    private static int writeRow;
    private static int clipToRow;

    public sealed class ExitException : Exception {
        public ExitException(int exitCode) => ExitCode = exitCode;
        public int ExitCode { get; }
    }

    private static void Exit(int code) => throw new ExitException(code);

    private static int row_read_handler(object? userData, Span<byte> destination) {
        _ = userData;

        if (clipToRow != 0 && readRow == clipToRow) {
            clipToRow = 0;
            return 0;
        }

        int sourceOffset = checked(destination.Length * readRow);
        if (sourceOffset < 0 || sourceOffset + destination.Length > testImage.Length)
            return 0;

        testImage.AsSpan(sourceOffset, destination.Length).CopyTo(destination);
        readRow++;
        return destination.Length;
    }

    private static int row_write_handler(object? userData, ReadOnlySpan<byte> data, int len) {
        data = data[..len];
        if (userData is not byte[] bitmap)
            throw new ArgumentException("The T.85 row writer requires a bitmap buffer.", nameof(userData));

        int destinationOffset = checked(data.Length * writeRow);
        if (destinationOffset < 0 || destinationOffset + data.Length > bitmap.Length)
            throw new InvalidOperationException("The decoded T.85 row exceeds the bitmap buffer.");

        data.CopyTo(bitmap.AsSpan(destinationOffset, data.Length));
        writeRow++;
        return 0;
    }

    private static int comment_handler(object? userData, ReadOnlySpan<byte> data) {
        _ = userData;
        if (data.IsEmpty) {
            Console.WriteLine("Comment (0): ---");
            return 0;
        }

        int stringLength = data.IndexOf((byte)0);
        if (stringLength < 0)
            stringLength = data.Length;
        string comment = Encoding.ASCII.GetString(data[..stringLength]);
        Console.WriteLine($"Comment ({data.Length}): {comment}");
        return 0;
    }

    private static void create_test_image(byte[] picture) {
        Array.Clear(picture);
        uint pseudoRandomSequenceGenerator = 1;
        uint[] repeat = new uint[8];
        int pointer = 0;

        for (int row = 0; row < 1951; row++) {
            for (int column = 0; column < 1960; column++) {
                if (row >= 192) {
                    if (row < 1023 || (column & (3 << 3)) == 0) {
                        uint sum =
                            (pseudoRandomSequenceGenerator & 1U) +
                            ((pseudoRandomSequenceGenerator >> 2) & 1U) +
                            ((pseudoRandomSequenceGenerator >> 11) & 1U) +
                            ((pseudoRandomSequenceGenerator >> 15) & 1U);
                        pseudoRandomSequenceGenerator =
                            unchecked((pseudoRandomSequenceGenerator << 1) + (sum & 1U));

                        if ((pseudoRandomSequenceGenerator & 3U) == 0) {
                            picture[pointer] |= unchecked((byte)(1 << (7 - (column & 7))));
                            repeat[column & 7] = 1;
                        } else {
                            repeat[column & 7] = 0;
                        }
                    } else if (repeat[column & 7] != 0) {
                        picture[pointer] |= unchecked((byte)(1 << (7 - (column & 7))));
                    }
                }

                if ((column & 7) == 7)
                    pointer++;
            }
        }

        uint foregroundPixels = 0;
        for (int i = 0; i < picture.Length; i++) {
            for (int bit = 0; bit < 8; bit++)
                foregroundPixels += (uint)((picture[i] >> bit) & 1);
        }

        if (foregroundPixels != 861_965) {
            Console.WriteLine(
                $"WARNING: Test image has {foregroundPixels} foreground pixels. " +
                "There should be 861965.");
        }
    }

    private static int test_cycle(
        string testId,
        byte[] image,
        uint width,
        uint height,
        uint l0,
        int mx,
        int options,
        int optionsx,
        string? comment,
        int correctLength) {
        Console.WriteLine(
            $"{testId}: TPBON={((options & T85_TPBON) != 0 ? 1 : 0)}, " +
            $"LRLTWO={((options & T85_LRLTWO) != 0 ? 1 : 0)}, Mx={mx}, L0={l0}");
        Console.WriteLine($"{testId}.1: Encode");

        int bytesPerRow = checked((int)((width + 7) / 8));
        int imageSize = checked(bytesPerRow * (int)height);

        T85EncodeState t85Encoder;
        if ((optionsx & T85_VLENGTH) != 0) {
            t85Encoder = t85_encode_init(
                null,
                width,
                checked(height + 10),
                row_read_handler,
                null);
            clipToRow = checked((int)height);
        } else {
            t85Encoder = t85_encode_init(
                null,
                width,
                height,
                row_read_handler,
                null);
            clipToRow = 0;
        }

        readRow = 0;
        t85_encode_set_options(t85Encoder, l0, mx, options);

        byte[]? commentBytes = comment is null
            ? null
            : Encoding.ASCII.GetBytes(comment + "\0");
        if (commentBytes is not null)
            t85_encode_comment(t85Encoder, commentBytes);

        testbufLen = 0;
        int maximumLength = 100;
        while (maximumLength > 0) {
            int length = t85_encode_get(
                t85Encoder,
                testbuf.AsSpan(testbufLen, maximumLength));
            if (length <= 0)
                break;

            testbufLen += length;
            maximumLength = 100;
            if (testbufLen + 100 > TESTBUF_SIZE)
                maximumLength = TESTBUF_SIZE - testbufLen;

            if (commentBytes is not null && testbufLen == 1000)
                t85_encode_comment(t85Encoder, commentBytes);
        }

        Console.WriteLine($"Encoded BIE has {testbufLen} bytes");
        if (correctLength > 0) {
            if (testbufLen != correctLength) {
                Console.WriteLine($"Incorrect encoded length. Should have been {correctLength}");
                Console.WriteLine("Test failed");
                Exit(2);
            }
            Console.WriteLine("Test passed");
        }

        int encodedBitCount = t85_encode_get_compressed_image_size(t85Encoder);
        t85_encode_free(t85Encoder);

        Console.WriteLine($"{testId}.2: Decode in one big chunk");
        byte[] decodedImage = new byte[imageSize];
        T85DecodeState t85Decoder = t85_decode_init(null, row_write_handler, decodedImage);
        if (commentBytes is not null && commentBytes[0] != (byte)'X') {
            t85_decode_set_comment_handler(
                t85Decoder,
                1000,
                comment_handler,
                null);
        }

        writeRow = 0;
        int result = t85_decode_put(t85Decoder, testbuf.AsSpan(0, testbufLen));
        if (result == T4_DECODE_MORE_DATA)
            result = t85_decode_put(t85Decoder, null, 0);

        int decodedBitCount = t85_decode_get_compressed_image_size(t85Decoder);
        if (encodedBitCount != decodedBitCount ||
            encodedBitCount != testbufLen * 8 ||
            result != T4_DECODE_OK) {
            Console.WriteLine($"Decode result {result}");
            Console.WriteLine(
                $"{encodedBitCount}/{decodedBitCount} bits of {testbufLen * 8} bits " +
                $"of BIE read. {t85Decoder.CurrentRow} lines decoded.");
            Console.WriteLine("Test failed");
            Exit(2);
        }

        if (!decodedImage.AsSpan().SequenceEqual(image.AsSpan(0, imageSize))) {
            Console.WriteLine("Image mismatch");
            Console.WriteLine("Test failed");
            Exit(2);
        }

        t85_decode_free(t85Decoder);
        Console.WriteLine("Test passed");

        Console.WriteLine($"{testId}.3: Decode byte by byte");
        decodedImage = new byte[imageSize];
        t85Decoder = t85_decode_init(null, row_write_handler, decodedImage);
        if (commentBytes is not null && commentBytes[0] != (byte)'X') {
            t85_decode_set_comment_handler(
                t85Decoder,
                1000,
                comment_handler,
                null);
        }

        writeRow = 0;
        result = T4_DECODE_MORE_DATA;
        int byteIndex;
        for (byteIndex = 0; byteIndex < testbufLen; byteIndex++) {
            result = t85_decode_put(t85Decoder, testbuf.AsSpan(byteIndex, 1));
            if (result == T4_DECODE_MORE_DATA)
                continue;
            byteIndex++;
            break;
        }

        if (result == T4_DECODE_MORE_DATA)
            result = t85_decode_put(t85Decoder, null, 0);

        if (byteIndex != testbufLen || result != T4_DECODE_OK) {
            Console.WriteLine($"Decode result {result}");
            Console.WriteLine(
                $"{byteIndex} bytes of {testbufLen} bytes of BIE read. " +
                $"{t85Decoder.CurrentRow} lines decoded.");
            Console.WriteLine("Test failed");
            Exit(2);
        }

        if (!decodedImage.AsSpan().SequenceEqual(image.AsSpan(0, imageSize))) {
            Console.WriteLine("Image mismatch");
            Console.WriteLine("Test failed");
            Exit(2);
        }

        t85_decode_free(t85Decoder);
        Console.WriteLine("Test passed");
        return 0;
    }

    public static int tkfx_t85_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        Console.WriteLine("T.85 JBIG for FAX encoder and decoder tests, from ITU-T T.82\n");
        Console.WriteLine("Generating the test image from T.82...");
        create_test_image(testImage);

        test_cycle("1", testImage, 1960, 1951, 1951, 0, 0, 0, null, 317_384);
        test_cycle("2", testImage, 1960, 1951, 1951, 0, T85_LRLTWO, 0, null, 317_132);
        test_cycle("3", testImage, 1960, 1951, 128, 8, T85_TPBON, 0, null, 253_653);

        test_cycle("4", testImage, 1960, 1951, 1951, 0, 0, 0, "Comment 4", 317_384 + 16);
        test_cycle("5", testImage, 1960, 1951, 1951, 0, T85_LRLTWO, 0, "Comment 5", 317_132 + 16);
        test_cycle("6", testImage, 1960, 1951, 128, 8, T85_TPBON, 0, "Comment 6", 253_653 + 2 * 16);

        test_cycle("7", testImage, 1960, 1951, 1951, 0, 0, 0, "Xomment 7", 317_384 + 16);
        test_cycle("8", testImage, 1960, 1951, 1951, 0, T85_LRLTWO, 0, "Xomment 8", 317_132 + 16);
        test_cycle("9", testImage, 1960, 1951, 128, 8, T85_TPBON, 0, "Xomment 9", 253_653 + 2 * 16);

        test_cycle("10", testImage, 1960, 1951, 1951, 0, T85_VLENGTH, T85_VLENGTH, null, 317_384 + 8);
        test_cycle("11", testImage, 1960, 1951, 1951, 0, T85_VLENGTH | T85_LRLTWO, T85_VLENGTH, null, 317_132 + 8);
        test_cycle("12", testImage, 1960, 1951, 128, 8, T85_VLENGTH | T85_TPBON, T85_VLENGTH, null, 253_653 + 8);

        test_cycle("13", testImage, 1960, 1951, 1951, 0, T85_VLENGTH, 0, null, 317_384);
        test_cycle("14", testImage, 1960, 1951, 1951, 0, T85_VLENGTH | T85_LRLTWO, 0, null, 317_132);
        test_cycle("15", testImage, 1960, 1951, 128, 8, T85_VLENGTH | T85_TPBON, 0, null, 253_653);

        Console.WriteLine("Tests passed");
        return 0;
    }
}
