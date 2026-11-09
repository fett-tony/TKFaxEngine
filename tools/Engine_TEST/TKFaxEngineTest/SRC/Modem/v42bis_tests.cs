/*
 * TKFaxEngine - managed C# port of v42bis_tests.c.
 *
 * The complete spanDSP file compression/decompression test is retained:
 * command-line modes, optional stuttered compression with flushes, binary
 * streams, byte counters, elapsed-time reporting and exact round-trip
 * verification.
 */

#nullable enable

using System.Diagnostics;
using static TKFaxEngine.LoggingApi;
using TKFaxEngine.Modem.V42;
using static TKFaxEngine.Modem.V42.v42bis;

namespace TKFaxEngineTest.Modem.Test;

public static class v42bis_tests {
    private const string CompressedFileName = "v42bis_tests.v42bis";
    private const string DecompressedFileName = "v42bis_tests.out";

    private static readonly CCompatibleRandom Random = new(1u);

    private static int _inOctetsToDate;
    private static int _outOctetsToDate;

    public sealed class V42BisTestsExitException : Exception {
        public V42BisTestsExitException(int exitCode)
            : base($"v42bis_tests exited with code {exitCode}.") => ExitCode = exitCode;

        public int ExitCode { get; }
    }

    public static int Main(string[] args) {
        try {
            return tkfx_v42bis_tests_body_main(args);
        } catch (V42BisTestsExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_v42bis_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        bool doCompression = false;
        bool doDecompression = false;
        bool stutterCompression = false;
        int argumentIndex = 0;

        while (argumentIndex < testArguments.Length) {
            string argument = testArguments[argumentIndex];
            if (argument == "--") {
                argumentIndex++;
                break;
            }

            if (argument.Length < 2 || argument[0] != '-')
                break;

            for (int optionIndex = 1; optionIndex < argument.Length; optionIndex++) {
                switch (argument[optionIndex]) {
                    case 'c':
                        doCompression = true;
                        break;
                    case 'd':
                        doDecompression = true;
                        break;
                    case 's':
                        stutterCompression = true;
                        break;
                    default:
                        Exit(2);
                        break;
                }
            }

            argumentIndex++;
        }

        int remainingArgumentCount = testArguments.Length - argumentIndex;
        if (remainingArgumentCount < 1) {
            Console.Error.WriteLine(
                "Usage: v42bis_tests [-c] [-d] [-s] <in-file> [<out-file>]");
            Exit(2);
        }

        string firstPath = testArguments[argumentIndex];
        string? originalFile = doCompression ? firstPath : null;
        string compressedFile = doCompression ? CompressedFileName : firstPath;
        string decompressedFile = remainingArgumentCount > 1
            ? testArguments[argumentIndex + 1]
            : DecompressedFileName;

        if (doCompression) {
            int stutterTime = Random.Next() & 0x3FF;
            using FileStream input = OpenRead(originalFile!);
            using FileStream compressedOutput = OpenWrite(compressedFile);
            v42bis_state_t stateA = v42bis_init(
                null,
                3,
                512,
                6,
                FrameHandler,
                compressedOutput,
                512,
                DataHandler,
                null,
                512)!;

            span_log_set_level(v42bis_get_logging_state(stateA), SPAN_LOG_SHOW_SEVERITY | SPAN_LOG_SHOW_PROTOCOL | SPAN_LOG_FLOW);
            span_log_set_tag(v42bis_get_logging_state(stateA), "V.42bis");

            _inOctetsToDate = 0;
            _outOctetsToDate = 0;
            byte[] buffer = new byte[1024];
            Stopwatch stopwatch = Stopwatch.StartNew();

            int length;
            while ((length = input.Read(buffer, 0, buffer.Length)) > 0) {
                int segmentOffset = 0;
                if (stutterCompression) {
                    while (length - segmentOffset >= stutterTime) {
                        if (v42bis_compress(
                                stateA,
                                buffer.AsSpan(segmentOffset, stutterTime),
                                stutterTime) != 0) {
                            Console.Error.WriteLine("Bad return code from compression");
                            Exit(2);
                        }

                        v42bis_compress_flush(stateA);
                        segmentOffset += stutterTime;
                        stutterTime = Random.Next() & 0x3FF;
                    }
                }

                if (v42bis_compress(
                        stateA,
                        buffer.AsSpan(segmentOffset, length - segmentOffset),
                        length - segmentOffset) != 0) {
                    Console.Error.WriteLine("Bad return code from compression");
                    Exit(2);
                }

                _inOctetsToDate += length;
            }

            v42bis_compress_flush(stateA);
            stopwatch.Stop();
            Console.WriteLine(
                "{0} bytes compressed to {1} bytes in {2}s",
                _inOctetsToDate,
                _outOctetsToDate,
                (long)stopwatch.Elapsed.TotalSeconds);
        }

        if (doDecompression) {
            using FileStream compressedInput = OpenRead(compressedFile);
            using FileStream output = OpenWrite(decompressedFile);
            v42bis_state_t stateB = v42bis_init(
                null,
                3,
                512,
                6,
                FrameHandler,
                compressedInput,
                512,
                DataHandler,
                output,
                512)!;

            span_log_set_level(v42bis_get_logging_state(stateB), SPAN_LOG_SHOW_SEVERITY | SPAN_LOG_SHOW_PROTOCOL | SPAN_LOG_FLOW);
            span_log_set_tag(v42bis_get_logging_state(stateB), "V.42bis");

            _inOctetsToDate = 0;
            _outOctetsToDate = 0;
            byte[] buffer = new byte[1024];
            Stopwatch stopwatch = Stopwatch.StartNew();

            int length;
            while ((length = compressedInput.Read(buffer, 0, buffer.Length)) > 0) {
                if (v42bis_decompress(stateB, buffer, length) != 0) {
                    Console.Error.WriteLine("Bad return code from decompression");
                    Exit(2);
                }

                _inOctetsToDate += length;
            }

            v42bis_decompress_flush(stateB);
            stopwatch.Stop();
            Console.WriteLine(
                "{0} bytes decompressed to {1} bytes in {2}s",
                _inOctetsToDate,
                _outOctetsToDate,
                (long)stopwatch.Elapsed.TotalSeconds);
        }

        if (doCompression && doDecompression) {
            int match = FilesMatchExactly(originalFile!, decompressedFile);
            if (match < 0) {
                Console.Error.WriteLine(
                    "V.42bis verification could not read '{0}' or '{1}'.",
                    originalFile,
                    decompressedFile);
                return 2;
            }

            if (match == 0) {
                Console.Error.WriteLine(
                    "V.42bis round-trip mismatch: '{0}' differs from '{1}'.",
                    originalFile,
                    decompressedFile);
                return 2;
            }

            Console.WriteLine(
                "V.42bis round-trip verified: decompressed data exactly matches the input file.");
        }

        return 0;
    }

    private static int FrameHandler(object? userData, byte[]? buffer, int len) {
        return WriteHandler(userData, buffer, len);
    }

    private static int DataHandler(object? userData, byte[]? buffer, int len) {
        return WriteHandler(userData, buffer, len);
    }

    private static int WriteHandler(object? userData, byte[]? buffer, int len) {
        try {
            if (userData is not FileStream stream || !stream.CanWrite)
                throw new IOException("The callback stream is not writable.");

            stream.Write(buffer!, 0, len);
        } catch (IOException ex) {
            Console.Error.WriteLine("Write error 0/{0}: {1}", len, ex.Message);
        }

        _outOctetsToDate += len;
        return 0;
    }

    private static int FilesMatchExactly(string leftFile, string rightFile) {
        try {
            using FileStream left = File.OpenRead(leftFile);
            using FileStream right = File.OpenRead(rightFile);
            byte[] leftBuffer = new byte[4096];
            byte[] rightBuffer = new byte[4096];

            for (; ; )
            {
                int leftLength = left.Read(leftBuffer, 0, leftBuffer.Length);
                int rightLength = right.Read(rightBuffer, 0, rightBuffer.Length);

                if (leftLength != rightLength)
                    return 0;

                if (!leftBuffer.AsSpan(0, leftLength).SequenceEqual(
                        rightBuffer.AsSpan(0, rightLength))) {
                    return 0;
                }

                if (leftLength == 0)
                    return 1;
            }
        } catch (IOException) {
            return -1;
        } catch (UnauthorizedAccessException) {
            return -1;
        }
    }

    private static FileStream OpenRead(string path) {
        try {
            return new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.SequentialScan);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine("Error opening file '{0}'.", path);
            Exit(2);
            throw;
        }
    }

    private static FileStream OpenWrite(string path) {
        try {
            return new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.SequentialScan);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine("Error opening file '{0}'.", path);
            Exit(2);
            throw;
        }
    }

    private static void Exit(int exitCode) => throw new V42BisTestsExitException(exitCode);

    private sealed class CCompatibleRandom {
        private uint _state;

        public CCompatibleRandom(uint seed) => _state = seed;

        public int Next() {
            _state = unchecked(_state * 1_103_515_245u + 12_345u);
            return unchecked((int)((_state >> 16) & 0x7FFFu));
        }
    }
}
