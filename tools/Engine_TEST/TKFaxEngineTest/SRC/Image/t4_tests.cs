/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * t4_tests.cs - ITU T.4 image to/from TIFF tests
 *
 * Managed C# port of the supplied spanDSP t4_tests.c source. The original
 * command-line modes, log decoding paths, TIFF transmission/reception cycle,
 * page restart/header handling, compression stepping, bit/block paths,
 * EOL verification, error injection and pixel-exact TIFF validation are retained.
 */

#nullable enable

using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using TKFaxEngine;
using TKFaxEngine.FaxImage;
using static TKFaxEngine.FaxImage.t4_rx;
using static TKFaxEngine.FaxImage.t4_tx;

namespace TKFaxEngineTest.Image.Test;

public static class T4Tests {
    private const string IN_FILE_NAME = "Test-Files/itu/fax/itutests.tif";
    private const string OUT_FILE_NAME = "t4_tests_receive.tif";
    private const int XSIZE = 1728;

    private static readonly Regex T38RxRegex = new(
        @"^\d+:\d+:\d+\.\d+\s+T\.38 Rx (?<packet>\d+): IFP(?:\s+[0-9A-Fa-f]+){5}\s*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RxBitRegex = new(
        @"^Rx bit\s+\d+\s+-\s+(?<bit>-?\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

    private sealed class CRandom {
        private uint state = 1;

        public int Next() {
            state = unchecked(state * 214013U + 2531011U);
            return (int)((state >> 16) & 0x7FFFU);
        }
    }

    private static void Exit(int code) => throw new ExitException(code);

    private static void DumpImageAsXxx(t4_rx_state_t state) {
        _ = state;
        // The supplied C source compiles this diagnostic body out with #if 0.
    }

    private static void DisplayPageStats(t4_rx_state_t state) {
        var stats = new t4_stats_t();
        t4_rx_get_transfer_statistics(state, stats);
        Console.WriteLine($"Pages = {stats.pages_transferred}");
        Console.WriteLine($"Compression = {t4_compression_to_str(stats.compression)}");
        Console.WriteLine($"Compressed size = {stats.line_image_size}");
        Console.WriteLine($"Raw image size = {stats.image_width} pels x {stats.image_length} pels");
        Console.WriteLine($"Image size = {stats.width} pels x {stats.length} pels");
        Console.WriteLine(
            $"Raw image resolution = {stats.image_x_resolution} pels/m x {stats.image_y_resolution} pels/m");
        Console.WriteLine($"Image resolution = {stats.x_resolution} pels/m x {stats.y_resolution} pels/m");
        Console.WriteLine($"Bad rows = {stats.bad_rows}");
        Console.WriteLine($"Longest bad row run = {stats.longest_bad_row_run}");
        (int minimum, int maximum) = GetDecoderRowBitRange(state);
        Console.WriteLine($"Bits per row - min {minimum}, max {maximum}");
    }

    private static (int Minimum, int Maximum) GetDecoderRowBitRange(t4_rx_state_t state) {
        int minimum = state.decoder.t4_t6.min_row_bits;
        int maximum = state.decoder.t4_t6.max_row_bits;
        if (minimum == int.MaxValue)
            minimum = 0;
        return (minimum, maximum);
    }

    private static int ReadIntegerMember(object instance, string name) {
        const BindingFlags flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        PropertyInfo? property = instance.GetType().GetProperty(name, flags);
        if (property?.GetValue(instance) is int propertyValue)
            return propertyValue;
        FieldInfo? field = instance.GetType().GetField(name, flags);
        if (field?.GetValue(instance) is int fieldValue)
            return fieldValue;
        return 0;
    }

    private static int DetectNonEcmPageEnd(int bit, int pageEnded) {
        if (bit == -1) {
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

    private static byte ReverseByte(int value) {
        uint input = unchecked((uint)value) & 0xFFU;
        input = ((input & 0x55U) << 1) | ((input >> 1) & 0x55U);
        input = ((input & 0x33U) << 2) | ((input >> 2) & 0x33U);
        input = ((input & 0x0FU) << 4) | ((input >> 4) & 0x0FU);
        return checked((byte)input);
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
        "T85" => T4_COMPRESSION_T85,
#if TKFaxEngine_SUPPORT_T88
        "T88" => T4_COMPRESSION_T88,
#endif
        "T81" => T4_COMPRESSION_T42_T81,
        "T43" => T4_COMPRESSION_T43,
#if TKFaxEngine_SUPPORT_T45
        "T45" => T4_COMPRESSION_T45,
#endif
        _ => -1
    };

    private static TimeZoneInfo? ResolveTimeZone(string value) {
        try {
            return TimeZoneInfo.FindSystemTimeZoneById(value);
        } catch (TimeZoneNotFoundException) {
        } catch (InvalidTimeZoneException) {
        }

        string normalized = value.Trim();
        if (normalized.Equals("UTC", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("GMT", StringComparison.OrdinalIgnoreCase)) {
            return TimeZoneInfo.Utc;
        }

        Match match = Regex.Match(
            normalized,
            @"^(?:UTC|GMT)?(?<sign>[+-])(?<hours>\d{1,2})(?::?(?<minutes>\d{2}))?$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!match.Success)
            return null;

        int hours = int.Parse(match.Groups["hours"].Value, CultureInfo.InvariantCulture);
        int minutes = match.Groups["minutes"].Success
            ? int.Parse(match.Groups["minutes"].Value, CultureInfo.InvariantCulture)
            : 0;
        if (hours > 14 || minutes > 59)
            return null;

        TimeSpan offset = new(hours, minutes, 0);
        if (match.Groups["sign"].Value == "-")
            offset = -offset;
        return TimeZoneInfo.CreateCustomTimeZone(normalized, offset, normalized, normalized);
    }

    private static void ConfigureLogging(t4_tx_state_t state) =>
        t4_tx_get_logging_state(state).Sink = (_, message) => Console.WriteLine(message);

    private static void ConfigureLogging(t4_rx_state_t state) =>
        t4_rx_get_logging_state(state).SetMessageHandler(
            (_, _, message) => Console.WriteLine(message),
            null);

    private static List<byte> ParseHexBytes(string line, int startIndex, int maximum) {
        var result = new List<byte>(Math.Min(maximum, 256));
        if (startIndex < 0 || startIndex >= line.Length)
            return result;

        ReadOnlySpan<char> span = line.AsSpan(startIndex);
        int position = 0;
        while (position < span.Length && result.Count < maximum) {
            while (position < span.Length && char.IsWhiteSpace(span[position]))
                ++position;
            int start = position;
            while (position < span.Length && Uri.IsHexDigit(span[position]))
                ++position;
            if (position == start)
                break;
            if (!byte.TryParse(
                    span[start..position],
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out byte value)) {
                break;
            }
            result.Add(value);
        }
        return result;
    }

    private static bool TryParseFirstHexAfterPrefix(
        string line,
        string prefix,
        out uint value) {
        value = 0;
        if (!line.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        ReadOnlySpan<char> remaining = line.AsSpan(prefix.Length).TrimStart();
        int length = 0;
        while (length < remaining.Length && Uri.IsHexDigit(remaining[length]))
            ++length;
        return length > 0 && uint.TryParse(
            remaining[..length],
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static int DecodeLogFile(
        string fileName,
        int compression,
        int xResolution,
        int yResolution,
        bool dumpAsXxx) {
        t4_rx_state_t? receiveState = t4_rx_init(null, OUT_FILE_NAME, T4_COMPRESSION_T4_2D);
        if (receiveState is null) {
            Console.WriteLine("Failed to init T.4 rx");
            return 2;
        }

        ConfigureLogging(receiveState);
        t4_rx_set_rx_encoding(receiveState, compression);
        t4_rx_set_x_resolution(receiveState, xResolution);
        t4_rx_set_y_resolution(receiveState, yResolution);
        t4_rx_set_image_width(receiveState, XSIZE);
        t4_rx_start_page(receiveState);

        if (!File.Exists(fileName)) {
            Console.WriteLine($"file {fileName} not found");
            t4_rx_free(receiveState);
            return 2;
        }

        uint lastPacketNumber = 0;
        int endOfPage = T4_DECODE_MORE_DATA;
        foreach (string line in File.ReadLines(fileName)) {
            List<byte>? bytes = null;
            bool reverse = false;

            if (TryParseFirstHexAfterPrefix(line, "HDLC:  FCD: 06 ", out _)) {
                bytes = ParseHexBytes(line, 18, 256);
            } else if (TryParseFirstHexAfterPrefix(line, "HDLC:  ", out _)) {
                bytes = ParseHexBytes(line, 19, 256);
            } else {
                Match t38Match = T38RxRegex.Match(line);
                if (t38Match.Success) {
                    uint packetNumber = uint.Parse(
                        t38Match.Groups["packet"].Value,
                        CultureInfo.InvariantCulture);
                    if (packetNumber != lastPacketNumber + 1)
                        Console.WriteLine($"Packet {packetNumber}");
                    lastPacketNumber = packetNumber;
                    bytes = ParseHexBytes(line, t38Match.Length, 256);
                    reverse = true;
                } else if (line.StartsWith("T.30 Rx:  ", StringComparison.Ordinal)) {
                    List<byte> initial = ParseHexBytes(line, 10, 4);
                    if (initial.Count == 4) {
                        uint packetNumber = initial[3];
                        if (packetNumber != lastPacketNumber + 1)
                            Console.WriteLine($"Packet {packetNumber}");
                        lastPacketNumber = packetNumber;
                        bytes = ParseHexBytes(line, 22, 256);
                        reverse = true;
                    }
                } else if (line.Length >= 6 &&
                           line.AsSpan(4, 2).SequenceEqual("  ") &&
                           int.TryParse(line.AsSpan(0, 4), NumberStyles.HexNumber,
                               CultureInfo.InvariantCulture, out _)) {
                    bytes = ParseHexBytes(line, 6, 16);
                    reverse = true;
                } else if (line.Length >= 10 &&
                           line.AsSpan(8, 2).SequenceEqual("  ") &&
                           int.TryParse(line.AsSpan(0, 8), NumberStyles.HexNumber,
                               CultureInfo.InvariantCulture, out _)) {
                    bytes = ParseHexBytes(line, 10, 16);
                    reverse = true;
                } else {
                    Match bitMatch = RxBitRegex.Match(line);
                    if (bitMatch.Success) {
                        int bit = int.Parse(
                            bitMatch.Groups["bit"].Value,
                            CultureInfo.InvariantCulture);
                        endOfPage = t4_rx_put_bit(receiveState, bit);
                        if (endOfPage != T4_DECODE_MORE_DATA) {
                            Console.WriteLine("End of page detected");
                            break;
                        }
                    }
                }
            }

            if (bytes is not null && bytes.Count > 0) {
                byte[] block = bytes.ToArray();
                if (reverse) {
                    for (int i = 0; i < block.Length; ++i)
                        block[i] = ReverseByte(block[i]);
                }
                endOfPage = t4_rx_put(receiveState, block, block.Length);
            }
        }

        if (dumpAsXxx)
            DumpImageAsXxx(receiveState);
        t4_rx_end_page(receiveState);
        DisplayPageStats(receiveState);
        t4_rx_free(receiveState);
        return 0;
    }

    public static int tkfx_t4_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);
        int[] compressionSequence = {
            T4_COMPRESSION_T4_1D,
            T4_COMPRESSION_T4_2D,
            T4_COMPRESSION_T6,
            T4_COMPRESSION_T85,
            T4_COMPRESSION_T85_L0,
#if TKFaxEngine_SUPPORT_T88x
            T4_COMPRESSION_T88,
#endif
#if TKFaxEngine_SUPPORT_T42x
            T4_COMPRESSION_T42_T81,
            T4_COMPRESSION_SYCC_T81,
#endif
#if TKFaxEngine_SUPPORT_T43x
            T4_COMPRESSION_T43,
#endif
#if TKFaxEngine_SUPPORT_T45x
            T4_COMPRESSION_T45,
#endif
        };

        int compression = -1;
        int compressionStep = 0;
        int xResolution = -1;
        int yResolution = -1;
        bool addPageHeaders = false;
        bool overlayPageHeaders = false;
        bool restartPages = false;
        string inputFileName = IN_FILE_NAME;
        string? decodeFileName = null;
        string? pageHeaderTimeZone = null;
        int minimumRowBits = 50;
        int blockSize = 1;
        int bitErrorRate = 0;
        bool dumpAsXxx = false;
        int testsFailed = 0;

        for (int index = 0; index < testArguments.Length; ++index) {
            string argument = testArguments[index];
            if (argument.Length < 2 || argument[0] != '-')
                continue;
            char option = argument[1];

            switch (option) {
                case 'e':
                    bitErrorRate = 0x3FF;
                    break;
                case 'h':
                    addPageHeaders = true;
                    overlayPageHeaders = false;
                    break;
                case 'H':
                    addPageHeaders = true;
                    overlayPageHeaders = true;
                    break;
                case 'r':
                    restartPages = true;
                    break;
                case 'x':
                    dumpAsXxx = true;
                    break;
                default: {
                        if (!TryReadOptionValue(testArguments, ref index, argument, out string value))
                            Exit(2);
                        switch (option) {
                            case 'b':
                                if (!int.TryParse(value, out blockSize) || blockSize < 0 || blockSize > 1024) {
                                    Console.WriteLine("Block size too large. Must be 1024 or less");
                                    Exit(2);
                                }
                                break;
                            case 'c':
                                compression = ParseCompression(value);
                                if (compression < 0) {
                                    Console.WriteLine("Unrecognised compression.");
                                    Exit(2);
                                }
                                compressionStep = -1;
                                break;
                            case 'd':
                                decodeFileName = value;
                                break;
                            case 'R':
                                if (value == "standard")
                                    yResolution = T4_Y_RESOLUTION_STANDARD;
                                else if (value == "fine")
                                    yResolution = T4_Y_RESOLUTION_FINE;
                                else if (value == "superfine")
                                    yResolution = T4_Y_RESOLUTION_SUPERFINE;
                                break;
                            case 'i':
                                inputFileName = value;
                                break;
                            case 'm':
                                if (!int.TryParse(value, out minimumRowBits))
                                    Exit(2);
                                break;
                            case 't':
                                pageHeaderTimeZone = value;
                                break;
                            case 'X':
                                if (!int.TryParse(value, out xResolution))
                                    Exit(2);
                                break;
                            case 'Y':
                                if (!int.TryParse(value, out yResolution))
                                    Exit(2);
                                break;
                            default:
                                Exit(2);
                                break;
                        }
                        break;
                    }
            }
        }

        if (decodeFileName is not null) {
            if (compression < 0)
                compression = T4_COMPRESSION_T4_1D;
            if (xResolution < 0)
                xResolution = T4_X_RESOLUTION_R8;
            if (yResolution < 0)
                yResolution = T4_Y_RESOLUTION_STANDARD;
            return DecodeLogFile(
                decodeFileName,
                compression,
                xResolution,
                yResolution,
                dumpAsXxx);
        }

        Console.WriteLine("Testing TIFF->compress->decompress->TIFF cycle");
        t4_tx_state_t? sendState = t4_tx_init(null, inputFileName, -1, -1);
        if (sendState is null) {
            Console.WriteLine("Failed to init T.4 send");
            return 2;
        }
        ConfigureLogging(sendState);
        t4_tx_set_min_bits_per_row(sendState, minimumRowBits);
        t4_tx_set_local_ident(sendState, "111 2222 3333");

        t4_rx_state_t? receiveState = t4_rx_init(null, OUT_FILE_NAME, T4_COMPRESSION_T4_2D);
        if (receiveState is null) {
            Console.WriteLine($"Failed to init T.4 rx for '{OUT_FILE_NAME}'");
            t4_tx_free(sendState);
            return 2;
        }
        ConfigureLogging(receiveState);

        int sends = 0;
        if (compressionStep > 0)
            compressionStep = 0;
        int endOfPage = T4_DECODE_MORE_DATA;
        var random = new CRandom();
        byte[] block = new byte[1024];

        for (; ; ) {
            int endMarks = 0;
            t4_tx_set_header_info(
                sendState,
                addPageHeaders && (sends & 1) == 0 ? "Header" : null);

            if (!string.IsNullOrEmpty(pageHeaderTimeZone)) {
                TimeZoneInfo? timeZone = ResolveTimeZone(pageHeaderTimeZone);
                if (timeZone is not null)
                    t4_tx_set_header_tz(sendState, timeZone);
            }
            t4_tx_set_header_overlays_image(sendState, overlayPageHeaders);

            if (restartPages && (sends & 1) != 0) {
                if (t4_tx_restart_page(sendState) != 0)
                    break;
            } else {
                if (compressionStep >= 0) {
                    if (compressionStep >= compressionSequence.Length)
                        compressionStep = 0;
                    compression = compressionSequence[compressionStep++];
                    if (blockSize == 0 && compressionStep >= 3) {
                        compressionStep = 0;
                        compression = compressionSequence[compressionStep++];
                    }
                }

                int supportedImageSizes =
                    T4_SUPPORT_WIDTH_215MM |
                    T4_SUPPORT_LENGTH_US_LETTER |
                    T4_SUPPORT_LENGTH_US_LEGAL |
                    T4_SUPPORT_LENGTH_UNLIMITED;
                int supportedBilevelResolutions =
                    T4_RESOLUTION_R8_STANDARD |
                    T4_RESOLUTION_R8_FINE |
                    T4_RESOLUTION_R8_SUPERFINE |
                    T4_RESOLUTION_R16_SUPERFINE |
                    T4_RESOLUTION_200_100 |
                    T4_RESOLUTION_200_200 |
                    T4_RESOLUTION_200_400 |
                    T4_RESOLUTION_300_300 |
                    T4_RESOLUTION_300_600 |
                    T4_RESOLUTION_400_400 |
                    T4_RESOLUTION_400_800 |
                    T4_RESOLUTION_600_600 |
                    T4_RESOLUTION_600_1200 |
                    T4_RESOLUTION_1200_1200;
                int supportedColourResolutions =
                    T4_RESOLUTION_100_100 |
                    T4_RESOLUTION_200_200 |
                    T4_RESOLUTION_300_300 |
                    T4_RESOLUTION_400_400 |
                    T4_RESOLUTION_600_600 |
                    T4_RESOLUTION_1200_1200;

                if (t4_tx_set_tx_image_format(
                        sendState,
                        compression,
                        supportedImageSizes,
                        supportedBilevelResolutions,
                        supportedColourResolutions) < 0) {
                    break;
                }

                t4_rx_set_rx_encoding(receiveState, compression);
                if (t4_tx_start_page(sendState) != 0)
                    break;
                t4_rx_set_x_resolution(receiveState, t4_tx_get_tx_x_resolution(sendState));
                t4_rx_set_y_resolution(receiveState, t4_tx_get_tx_y_resolution(sendState));
                t4_rx_set_image_width(receiveState, t4_tx_get_tx_image_width(sendState));
            }

            t4_rx_start_page(receiveState);
            DetectNonEcmPageEnd(-1, compression);
            bool pageEnded = false;

            if (blockSize == 0) {
                int bit;
                while ((bit = t4_tx_get_bit(sendState)) >= 0) {
                    int result = DetectNonEcmPageEnd(bit, pageEnded ? 1 : 0);
                    if (result != 0) {
                        Console.WriteLine($"Incorrect EOLs - {result}");
                        testsFailed += result - 1;
                        break;
                    }
                    if (bitErrorRate != 0 && random.Next() % bitErrorRate == 0)
                        bit ^= 1;
                    endOfPage = t4_rx_put_bit(receiveState, bit);
                }

                while (endOfPage != T4_DECODE_OK) {
                    endOfPage = t4_rx_put_bit(receiveState, 0);
                    if (++endMarks > 50) {
                        Console.WriteLine("Receiver missed the end of page mark");
                        ++testsFailed;
                        break;
                    }
                }

                for (int i = 0; i < 1000; ++i)
                    t4_rx_put_bit(receiveState, (random.Next() >> 10) & 1);
            } else {
                int length;
                do {
                    length = t4_tx_get(sendState, block, blockSize);
                    if (length > 0)
                        endOfPage = t4_rx_put(receiveState, block, length);
                } while (length > 0);

                while (endOfPage != T4_DECODE_OK) {
                    block[0] = 0;
                    endOfPage = t4_rx_put(receiveState, block, 1);
                    if (++endMarks > 5) {
                        Console.WriteLine("Receiver missed the end of page mark");
                        ++testsFailed;
                        break;
                    }
                }
            }

            if (dumpAsXxx)
                DumpImageAsXxx(receiveState);
            DisplayPageStats(receiveState);
            if (!restartPages || (sends & 1) != 0)
                t4_tx_end_page(sendState);
            t4_rx_end_page(receiveState);
            ++sends;
        }

        t4_tx_free(sendState);
        t4_rx_free(receiveState);

        Console.Out.Flush();

        int compareResult = 0;
        if (testsFailed == 0) {
            using Process? process = Process.Start(new ProcessStartInfo {
                FileName = "cmd.exe",
                Arguments = $"/c tiffcmp -t {inputFileName} {OUT_FILE_NAME}",
                UseShellExecute = false
            });
            if (process is null) {
                compareResult = -1;
            } else {
                process.WaitForExit();
                compareResult = process.ExitCode;
            }
        }

        if (testsFailed != 0 || compareResult != 0) {
            Console.WriteLine("Tests failed");
            return 2;
        }

        Console.WriteLine("Tests passed");
        return 0;
    }
}
