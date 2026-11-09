/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * hdlc_tests.c
 *
 * Written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2003 Steve Underwood
 * GNU General Public License version 2.
 *
 * Managed C# port preserving the supplied spanDSP test sequence and limits.
 */

#nullable enable
using SignalStatus = TKFaxEngine.SignalStatus;

using System.Text.RegularExpressions;
using TKFaxEngine.Modem;
using TKFaxEngineTest;
using static TKFaxEngine.AsyncApi;
using static TKFaxEngine.HighResolutionTiming;
using static TKFaxEngine.Modem.HdlcApi;

namespace TKFaxEngineTest.Modem.Test;

public static partial class HdlcTests {
    private static int ref_len;
    private static readonly byte[] buf = new byte[1000];
    private static bool abort_reported;
    private static bool frame_handled;
    private static bool frame_failed;
    private static int frame_len_errors;
    private static int frame_data_errors;
    private static bool underflow_reported;
    private static bool framing_ok_reported;
    private static int framing_ok_reports;
    private static HdlcReceiver? rx;
    private static HdlcTransmitter? tx;
    private static int frames_sent;
    private static int bytes_sent;
    private static ulong start;
    private static ulong end;

    private static uint _myRandState = 1_234_567U;
    private static uint _randState = 1U;

    private static int my_rand() {
        _myRandState = unchecked(1_664_525U * _myRandState + 1_013_904_223U);
        return unchecked((int)(_myRandState >> 8));
    }

    private static int rand() {
        _randState = unchecked(_randState * 214013U + 2531011U);
        return (int)((_randState >> 16) & 0x7FFFU);
    }

    private static void Exit(int code) => throw new TestProcessExitException(code);

    private static int cook_up_msg(byte[] destination) {
        int length = (my_rand() & 0x3F) + 100;
        for (int i = 0; i < length; i++)
            destination[i] = unchecked((byte)my_rand());
        return length;
    }

    private static void frame_handler(
        object? userData,
        ReadOnlyMemory<byte>? packet,
        int lengthOrStatus,
        bool ok) {
        _ = userData;

        if (lengthOrStatus < 0) {
            Console.WriteLine(
                $"HDLC rx status is {signal_status_to_str(lengthOrStatus)} ({lengthOrStatus})");
            switch (lengthOrStatus) {
                case (int)SignalStatus.FramingOk:
                    framing_ok_reported = true;
                    framing_ok_reports++;
                    break;
                case (int)SignalStatus.Abort:
                    abort_reported = true;
                    break;
            }
            return;
        }

        if (ok) {
            if (lengthOrStatus != ref_len) {
                Console.WriteLine($"Len error - {lengthOrStatus} {ref_len}");
                frame_len_errors++;
                return;
            }

            if (packet is null ||
                !packet.Value.Span[..lengthOrStatus].SequenceEqual(buf.AsSpan(0, lengthOrStatus))) {
                Console.WriteLine("Frame data error");
                frame_data_errors++;
                return;
            }

            frame_handled = true;
        } else {
            frame_failed = true;
        }
    }

    private static void underflow_handler(object? userData) {
        _ = userData;
        underflow_reported = true;
    }

    private static void check_result() {
        HdlcReceiveStatistics statistics = new();
        _ = hdlc_rx_get_stats(rx!, statistics);

        Console.WriteLine($"{statistics.Bytes} bytes");
        Console.WriteLine($"{statistics.GoodFrames} good frames");
        Console.WriteLine($"{statistics.CrcErrors} CRC errors");
        Console.WriteLine($"{statistics.LengthErrors} length errors");
        Console.WriteLine($"{statistics.Aborts} aborts");
        Console.WriteLine($"{frame_len_errors} frame length errors");
        Console.WriteLine($"{frame_data_errors} frame data errors");
        Console.WriteLine($"Test duration {unchecked(end - start)} ticks");

        if (statistics.Bytes != unchecked((ulong)bytes_sent) ||
            statistics.GoodFrames != unchecked((ulong)frames_sent) ||
            statistics.CrcErrors != 0 ||
            statistics.LengthErrors != 0 ||
            statistics.Aborts != 0 ||
            frame_len_errors != 0 ||
            frame_data_errors != 0) {
            Console.WriteLine("Tests failed.");
            Exit(2);
        }

        Console.WriteLine("Test passed.\n");
    }

    private static void InitializeHdlc(
        bool crc32,
        int interFrameFlags,
        bool progressive,
        bool reportBadFrames,
        int framingOkThreshold) {
        tx = hdlc_tx_init(
            tx,
            crc32,
            interFrameFlags,
            progressive,
            underflow_handler,
            null);
        rx = hdlc_rx_init(
            rx,
            crc32,
            reportBadFrames,
            framingOkThreshold,
            frame_handler,
            null);
    }

    private static int test_hdlc_modes() {
        byte[] bufx = new byte[100];

        Console.WriteLine("Testing with CRC-16 (byte by byte)");
        frame_len_errors = 0;
        frame_data_errors = 0;
        InitializeHdlc(false, 1, false, false, 5);
        underflow_reported = false;
        start = rdtscll();
        _ = hdlc_tx_flags(tx!, 40);
        ref_len = cook_up_msg(buf);
        _ = hdlc_tx_frame(tx!, buf, ref_len);
        frame_handled = false;
        frame_failed = false;
        frames_sent = 0;
        bytes_sent = 0;

        for (int i = 0; i < 1_000_000; i++) {
            int nextByte = hdlc_tx_get_byte(tx!);
            hdlc_rx_put_byte(rx!, nextByte);
            if (!underflow_reported)
                continue;

            underflow_reported = false;
            nextByte = hdlc_tx_get_byte(tx!);
            hdlc_rx_put_byte(rx!, nextByte);
            frames_sent++;
            bytes_sent += ref_len;
            if (!frame_handled) {
                Console.WriteLine("Frame not received.");
                return -1;
            }

            ref_len = cook_up_msg(buf);
            _ = hdlc_tx_frame(tx!, buf, ref_len);
            frame_handled = false;
        }

        end = rdtscll();
        check_result();

        Console.WriteLine("Testing with CRC-16 (chunk by chunk)");
        frame_len_errors = 0;
        frame_data_errors = 0;
        InitializeHdlc(false, 1, false, false, 5);
        underflow_reported = false;
        start = rdtscll();
        _ = hdlc_tx_flags(tx!, 40);
        ref_len = cook_up_msg(buf);
        _ = hdlc_tx_frame(tx!, buf, ref_len);
        frame_handled = false;
        frame_failed = false;
        frames_sent = 0;
        bytes_sent = 0;

        for (int i = 0; i < 10_000; i++) {
            int length = hdlc_tx_get(tx!, bufx, 100);
            hdlc_rx_put(rx!, bufx, length);
            if (!underflow_reported)
                continue;

            underflow_reported = false;
            length = hdlc_tx_get(tx!, bufx, 100);
            hdlc_rx_put(rx!, bufx, length);
            frames_sent++;
            bytes_sent += ref_len;
            if (!frame_handled) {
                Console.WriteLine("Frame not received.");
                return -1;
            }

            ref_len = cook_up_msg(buf);
            _ = hdlc_tx_frame(tx!, buf, ref_len);
            frame_handled = false;
        }

        end = rdtscll();
        check_result();

        Console.WriteLine("Testing with CRC-16 (bit by bit)");
        frame_len_errors = 0;
        frame_data_errors = 0;
        InitializeHdlc(false, 2, false, false, 5);
        underflow_reported = false;
        start = rdtscll();
        _ = hdlc_tx_flags(tx!, 40);
        frame_handled = true;
        frame_failed = false;
        frames_sent = 0;
        bytes_sent = 0;
        ref_len = 0;

        for (int i = 0; i < 8 * 1_000_000; i++) {
            int nextByte = hdlc_tx_get_bit(tx!);
            hdlc_rx_put_bit(rx!, nextByte);
            if (!underflow_reported)
                continue;

            underflow_reported = false;
            for (int j = 0; j < 20; j++) {
                nextByte = hdlc_tx_get_bit(tx!);
                hdlc_rx_put_bit(rx!, nextByte);
            }

            if (ref_len != 0) {
                frames_sent++;
                bytes_sent += ref_len;
            }

            if (!frame_handled) {
                Console.WriteLine("Frame not received.");
                return -1;
            }

            ref_len = cook_up_msg(buf);
            _ = hdlc_tx_frame(tx!, buf, ref_len);
            frame_handled = false;
        }

        end = rdtscll();
        check_result();

        Console.WriteLine("Testing with CRC-32 (byte by byte)");
        frame_len_errors = 0;
        frame_data_errors = 0;
        InitializeHdlc(true, 1, false, false, 1);
        underflow_reported = false;
        start = rdtscll();
        _ = hdlc_tx_flags(tx!, 40);
        frame_handled = true;
        frame_failed = false;
        frames_sent = 0;
        bytes_sent = 0;
        ref_len = 0;

        for (int i = 0; i < 1_000_000; i++) {
            int nextByte = hdlc_tx_get_byte(tx!);
            hdlc_rx_put_byte(rx!, nextByte);
            if (!underflow_reported)
                continue;

            underflow_reported = false;
            nextByte = hdlc_tx_get_byte(tx!);
            hdlc_rx_put_byte(rx!, nextByte);
            if (ref_len != 0) {
                frames_sent++;
                bytes_sent += ref_len;
            }

            if (!frame_handled) {
                Console.WriteLine("Frame not received.");
                return -1;
            }

            ref_len = cook_up_msg(buf);
            _ = hdlc_tx_frame(tx!, buf, ref_len);
            frame_handled = false;
        }

        end = rdtscll();
        check_result();

        Console.WriteLine("Testing progressive mode with CRC-16 (byte by byte)");
        frame_len_errors = 0;
        frame_data_errors = 0;
        InitializeHdlc(true, 1, true, false, 1);
        underflow_reported = false;
        start = rdtscll();
        _ = hdlc_tx_flags(tx!, 40);
        frame_handled = true;
        frame_failed = false;
        int progress = 9999;
        int progressDelay = 9999;
        frames_sent = 0;
        bytes_sent = 0;
        ref_len = 0;

        for (int i = 0; i < 1_000_000; i++) {
            int nextByte = hdlc_tx_get_byte(tx!);
            hdlc_rx_put_byte(rx!, nextByte);
            if (underflow_reported) {
                underflow_reported = false;
                nextByte = hdlc_tx_get_byte(tx!);
                hdlc_rx_put_byte(rx!, nextByte);
                if (ref_len != 0) {
                    frames_sent++;
                    bytes_sent += ref_len;
                }

                if (!frame_handled) {
                    Console.WriteLine("Frame not received.");
                    return -1;
                }

                ref_len = cook_up_msg(buf);
                _ = hdlc_tx_frame(tx!, buf, 10);
                progress = 10;
                progressDelay = 8;
                frame_handled = false;
            }

            if (progress < ref_len && progressDelay-- <= 0) {
                int partLength = progress + 10 <= ref_len ? 10 : ref_len - progress;
                if (hdlc_tx_frame(tx!, buf.AsSpan(progress, partLength), partLength) < 0) {
                    Console.WriteLine("Failed to add progressively");
                    return -1;
                }
                progress += 10;
                progressDelay = 8;
            }
        }

        end = rdtscll();
        check_result();
        Console.WriteLine("Tests passed.");
        return 0;
    }

    private static int test_hdlc_frame_length_error_handling() {
        Console.WriteLine("Testing frame length error handling using CRC-16 (bit by bit)");
        frame_len_errors = 0;
        frame_data_errors = 0;
        InitializeHdlc(false, 2, false, true, 5);
        hdlc_rx_set_max_frame_len(rx!, 100);
        underflow_reported = false;
        framing_ok_reported = false;
        framing_ok_reports = 0;
        _ = hdlc_tx_flags(tx!, 10);
        frame_handled = true;
        frame_failed = false;
        frames_sent = 0;
        bytes_sent = 0;
        ref_len = 0;

        for (int i = 0; i < 8 * 1_000_000; i++) {
            int nextByte = hdlc_tx_get_bit(tx!);
            hdlc_rx_put_bit(rx!, nextByte);
            if (framing_ok_reported) {
                Console.WriteLine($"Framing OK reported at bit {i} ({framing_ok_reports})");
                framing_ok_reported = false;
            }

            if (!underflow_reported)
                continue;

            underflow_reported = false;
            for (int j = 0; j < 20; j++) {
                nextByte = hdlc_tx_get_bit(tx!);
                hdlc_rx_put_bit(rx!, nextByte);
                if (framing_ok_reported) {
                    Console.WriteLine(
                        $"Framing OK reported at bit {i} ({framing_ok_reports}) - {(frame_handled ? 1 : 0)}");
                    framing_ok_reported = false;
                }
            }

            if (ref_len != 0) {
                frames_sent++;
                bytes_sent += ref_len;
            }

            if (!frame_handled) {
                if (ref_len == 101) {
                    Console.WriteLine("Tests passed.");
                    return 0;
                }
                if (frame_failed)
                    Console.WriteLine("Frame failed.");
                Console.WriteLine($"Frame not received at length {ref_len}.");
                return -1;
            }

            if (ref_len > 100) {
                Console.WriteLine("Tests failed.");
                return -1;
            }

            ref_len++;
            _ = hdlc_tx_frame(tx!, buf, ref_len);
            frame_handled = false;
        }

        Console.WriteLine("Tests failed.");
        return -1;
    }

    private static int test_hdlc_crc_error_handling() {
        Console.WriteLine("Testing CRC error handling using CRC-16 (bit by bit)");
        frame_len_errors = 0;
        frame_data_errors = 0;
        InitializeHdlc(false, 2, false, true, 5);
        underflow_reported = false;
        framing_ok_reported = false;
        framing_ok_reports = 0;
        _ = hdlc_tx_flags(tx!, 10);
        frame_handled = true;
        frame_failed = false;
        frames_sent = 0;
        bytes_sent = 0;
        ref_len = 100;
        bool corrupt = false;

        for (int i = 0; i < 8 * 1_000_000; i++) {
            int nextByte = hdlc_tx_get_bit(tx!);
            hdlc_rx_put_bit(rx!, nextByte);
            if (framing_ok_reported) {
                Console.WriteLine($"Framing OK reported at bit {i} ({framing_ok_reports})");
                framing_ok_reported = false;
            }

            if (!underflow_reported)
                continue;

            underflow_reported = false;
            for (int j = 0; j < 20; j++) {
                nextByte = hdlc_tx_get_bit(tx!);
                hdlc_rx_put_bit(rx!, nextByte);
                if (framing_ok_reported) {
                    Console.WriteLine(
                        $"Framing OK reported at bit {i} ({framing_ok_reports}) - {(frame_handled ? 1 : 0)}");
                    framing_ok_reported = false;
                }
            }

            if (ref_len != 0) {
                frames_sent++;
                bytes_sent += ref_len;
            }

            if (!frame_handled) {
                if (!corrupt) {
                    Console.WriteLine("Frame not received when it should be correct.");
                    return -1;
                }
            } else if (corrupt) {
                Console.WriteLine("Frame received when it should be corrupt.");
                return -1;
            }

            ref_len = cook_up_msg(buf);
            _ = hdlc_tx_frame(tx!, buf, ref_len);
            corrupt = (rand() & 1) != 0;
            if (corrupt)
                _ = hdlc_tx_corrupt_frame(tx!);
            frame_handled = false;
        }

        Console.WriteLine("Tests passed.");
        return 0;
    }

    private static int test_hdlc_abort_handling() {
        Console.WriteLine("Testing abort handling using CRC-16 (bit by bit)");
        frame_len_errors = 0;
        frame_data_errors = 0;
        InitializeHdlc(false, 2, false, true, 0);
        underflow_reported = false;
        framing_ok_reported = false;
        framing_ok_reports = 0;
        _ = hdlc_tx_flags(tx!, 10);
        frame_handled = true;
        frame_failed = false;
        frames_sent = 0;
        bytes_sent = 0;
        ref_len = 0;
        bool abort = false;
        abort_reported = false;

        for (int i = 0; i < 8 * 1_000_000; i++) {
            int nextByte = hdlc_tx_get_bit(tx!);
            hdlc_rx_put_bit(rx!, nextByte);
            if (framing_ok_reported) {
                Console.WriteLine($"Framing OK reported at bit {i} ({framing_ok_reports})");
                framing_ok_reported = false;
            }

            if (!underflow_reported)
                continue;

            underflow_reported = false;
            for (int j = 0; j < 20; j++) {
                nextByte = hdlc_tx_get_bit(tx!);
                hdlc_rx_put_bit(rx!, nextByte);
                if (framing_ok_reported) {
                    Console.WriteLine(
                        $"Framing OK reported at bit {i} ({framing_ok_reports}) - {(frame_handled ? 1 : 0)}");
                    framing_ok_reported = false;
                }
            }

            if (ref_len != 0) {
                frames_sent++;
                bytes_sent += ref_len;
            }

            if (abort) {
                if (!abort_reported) {
                    Console.WriteLine("Abort not received when sent");
                    return -1;
                }
                if (frame_handled) {
                    if (frame_failed)
                        Console.WriteLine("Frame failed.");
                    Console.WriteLine("Frame received when abort sent.");
                    return -1;
                }
            } else {
                if (abort_reported) {
                    Console.WriteLine("Abort received when not sent");
                    return -1;
                }
                if (!frame_handled) {
                    if (frame_failed)
                        Console.WriteLine("Frame failed.");
                    Console.WriteLine("Frame not received.");
                    return -1;
                }
            }

            ref_len = cook_up_msg(buf);
            _ = hdlc_tx_frame(tx!, buf, ref_len);
            abort = (rand() & 1) != 0;
            if (abort)
                _ = hdlc_tx_abort(tx!);
            frame_handled = false;
            abort_reported = false;
        }

        Console.WriteLine("Tests passed.");
        return 0;
    }

#if INCLUDE_DISABLED_SPANDSP_TESTS
    private static int test_hdlc_octet_count_handling() {
        Console.WriteLine("Testing the octet_counting handling using CRC-16 (bit by bit)");
        frame_len_errors = 0;
        frame_data_errors = 0;
        InitializeHdlc(false, 2, false, true, 0);
        hdlc_rx_set_octet_counting_report_interval(rx!, 16);
        underflow_reported = false;
        framing_ok_reported = false;
        framing_ok_reports = 0;
        _ = hdlc_tx_flags(tx!, 10);
        frame_handled = true;
        frame_failed = false;
        frames_sent = 0;
        bytes_sent = 0;
        ref_len = 0;

        for (int i = 0; i < 8 * 1_000_000; i++) {
            int nextByte = hdlc_tx_get_bit(tx!);
            hdlc_rx_put_bit(rx!, nextByte);
            if (framing_ok_reported) {
                Console.WriteLine($"Framing OK reported at bit {i} ({framing_ok_reports})");
                framing_ok_reported = false;
            }
            if (!underflow_reported)
                continue;

            underflow_reported = false;
            for (int j = 0; j < 20; j++) {
                nextByte = hdlc_tx_get_bit(tx!);
                hdlc_rx_put_bit(rx!, nextByte);
                if (framing_ok_reported) {
                    Console.WriteLine(
                        $"Framing OK reported at bit {i} ({framing_ok_reports}) - {(frame_handled ? 1 : 0)}");
                    framing_ok_reported = false;
                }
            }

            if (ref_len != 0) {
                frames_sent++;
                bytes_sent += ref_len;
            }
            if (!frame_handled) {
                if (frame_failed)
                    Console.WriteLine("Frame failed.");
                Console.WriteLine("Frame not received.");
                return -1;
            }

            ref_len = cook_up_msg(buf);
            _ = hdlc_tx_frame(tx!, buf, ref_len);
            _ = hdlc_tx_abort(tx!);
            frame_handled = false;
        }

        Console.WriteLine("Tests passed.");
        return 0;
    }
#endif

    private static void hdlc_tests() {
        Console.WriteLine("HDLC module tests");
        if (test_hdlc_modes() != 0) {
            Console.WriteLine("Tests failed");
            Exit(2);
        }
        if (test_hdlc_frame_length_error_handling() != 0) {
            Console.WriteLine("Tests failed");
            Exit(2);
        }
        if (test_hdlc_crc_error_handling() != 0) {
            Console.WriteLine("Tests failed");
            Exit(2);
        }
        if (test_hdlc_abort_handling() != 0) {
            Console.WriteLine("Tests failed");
            Exit(2);
        }
#if INCLUDE_DISABLED_SPANDSP_TESTS
        if (test_hdlc_octet_count_handling() != 0) {
            Console.WriteLine("Tests failed");
            Exit(2);
        }
#endif
        Console.WriteLine("Tests passed.");
    }

    private static void decode_handler(
        object? userData,
        ReadOnlyMemory<byte>? packet,
        int lengthOrStatus,
        bool ok) {
        _ = userData;
        if (lengthOrStatus < 0) {
            Console.WriteLine(
                $"HDLC rx status is {signal_status_to_str(lengthOrStatus)} ({lengthOrStatus})");
            return;
        }

        if (ok) {
            Console.WriteLine($"Good frame, len = {lengthOrStatus}");
            Console.Write("HDLC:  ");
            ReadOnlySpan<byte> data = packet.HasValue
                ? packet.Value.Span
                : ReadOnlySpan<byte>.Empty;
            for (int i = 0; i < lengthOrStatus; i++)
                Console.Write($"{data[i]:X2} ");
            Console.WriteLine();
        } else {
            Console.WriteLine($"Bad frame, len = {lengthOrStatus}");
        }
    }

    private static readonly Regex DecodeLine = new(
        @"^Rx bit\s+(-?\d+)\s+-\s+(-?\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static void decode_bitstream(string inputFileName) {
        StreamReader input;
        try {
            input = File.OpenText(inputFileName);
        } catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine($"Failed to open '{inputFileName}'");
            Exit(2);
            return;
        }

        using (input) {
            HdlcReceiver decodeRx = hdlc_rx_init(
                null,
                false,
                true,
                2,
                decode_handler,
                null);

            string? line;
            while ((line = input.ReadLine()) is not null) {
                Match match = DecodeLine.Match(line);
                if (!match.Success)
                    continue;
                int bit = int.Parse(match.Groups[2].Value);
                hdlc_rx_put_bit(decodeRx, bit);
            }
        }
    }

    public static int tkfx_hdlc_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        string? inputFileName = null;
        for (int argumentIndex = 0; argumentIndex < testArguments.Length; argumentIndex++) {
            string argument = testArguments[argumentIndex];
            if (argument == "--")
                break;
            if (argument.Length < 2 || (argument[0] != '-' && argument[0] != '/'))
                break;

            for (int optionIndex = 1; optionIndex < argument.Length; optionIndex++) {
                if (argument[optionIndex] != 'd') {
                    Exit(2);
                    return 2;
                }

                if (optionIndex + 1 < argument.Length) {
                    inputFileName = argument[(optionIndex + 1)..];
                } else if (++argumentIndex < testArguments.Length) {
                    inputFileName = testArguments[argumentIndex];
                } else {
                    Exit(2);
                    return 2;
                }
                optionIndex = argument.Length;
            }
        }

        if (inputFileName is not null)
            decode_bitstream(inputFileName);
        else
            hdlc_tests();

        return 0;
    }
}
