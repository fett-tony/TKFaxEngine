/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * adsi_tests.cs - tests for analogue display service handling.
 *
 * Original C test written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2003 Steve Underwood
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License version 2, as
 * published by the Free Software Foundation.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * General Public License for more details.
 *
 * Managed C# port: test sequence, message cycles, callbacks, sample block
 * size and command-line switches are retained. PCM16 WAV I/O replaces the
 * native libsndfile calls without C interop.
 */

#nullable enable

using System.Globalization;
using System.Text;
using TKFaxEngine;
using TKFaxEngine.Audio;
using static TKFaxEngine.Audio.Adsi;

namespace TKFaxEngineTest.Core.Test;

public static class adsi_tests {
    private const string OUTPUT_FILE_NAME = "adsi.wav";
    private const int BLOCK_LEN = 160;
    private const string MITEL_DIR = "Test-Files/mitel/";
    private const string BELLCORE_DIR = "Test-Files/bellcore/";

    private static readonly string[] bellcore_files =
    {
        MITEL_DIR + "mitel-cm7291-talkoff.wav",
        BELLCORE_DIR + "tr-tsy-00763-1.wav",
        BELLCORE_DIR + "tr-tsy-00763-2.wav",
        BELLCORE_DIR + "tr-tsy-00763-3.wav",
        BELLCORE_DIR + "tr-tsy-00763-4.wav",
        BELLCORE_DIR + "tr-tsy-00763-5.wav",
        BELLCORE_DIR + "tr-tsy-00763-6.wav",
        string.Empty
    };

    private static string? decode_test_file;
    private static int errors;
    private static bool basic_testing;
    private static adsi_rx_state_t? rx_adsi;
    private static adsi_tx_state_t? tx_adsi;
    private static int current_standard;
    private static bool good_message_received;
    private static bool log_audio;
    private static Pcm16WaveWriter? outhandle;
    private static bool short_preamble;
    private static int cycle;

    public static int main(string[] testArguments) {
        try {
            return tkfx_adsi_tests_body_main(testArguments);
        } catch (AdsiTestExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_adsi_tests_body_main(string[] testArguments) {
        short[] amp = new short[BLOCK_LEN];
        int test_standard = -1;
        int enable_basic_tests = 1;
        int enable_talkoff_tests = 0;

        errors = 0;
        log_audio = false;
        decode_test_file = null;
        short_preamble = false;
        cycle = 0;

        for (int index = 0; index < testArguments.Length; index++) {
            string option = testArguments[index];
            switch (option) {
                case "-b":
                    enable_basic_tests = 1;
                    enable_talkoff_tests = 0;
                    break;
                case "-d":
                    decode_test_file = RequireOptionArgument(testArguments, ref index, option);
                    break;
                case "-l":
                    log_audio = true;
                    break;
                case "-p":
                    short_preamble = true;
                    break;
                case "-s":
                    test_standard = ParseStandard(RequireOptionArgument(testArguments, ref index, option));
                    break;
                case "-t":
                    enable_basic_tests = 0;
                    enable_talkoff_tests = 1;
                    break;
                default:
                    Exit(2);
                    break;
            }
        }

        tdd_character_set_tests();

        if (decode_test_file is not null) {
            using var inhandle = Pcm16WaveReader.OpenMono(decode_test_file);
            current_standard = test_standard < 0 ? ADSI_STANDARD_CLASS : test_standard;
            rx_adsi = adsi_rx_init(null, current_standard, put_adsi_msg, null);
            LoggingApi.span_log_set_level(
                adsi_rx_get_logging_state(rx_adsi),
                LoggingApi.SPAN_LOG_SHOW_SEVERITY | LoggingApi.SPAN_LOG_SHOW_PROTOCOL | LoggingApi.SPAN_LOG_FLOW);
            LoggingApi.span_log_set_tag(adsi_rx_get_logging_state(rx_adsi), "ADSI");

            while (true) {
                int len = inhandle.ReadFrames(amp, BLOCK_LEN);
                if (len == 0)
                    break;
                adsi_rx(rx_adsi, amp, len);
            }

            adsi_rx_free(rx_adsi);
            rx_adsi = null;
            return 0;
        }

        try {
            if (log_audio)
                outhandle = new Pcm16WaveWriter(OUTPUT_FILE_NAME, 1, 8000);

            int first_standard;
            int last_standard;
            if (test_standard < 0) {
                first_standard = ADSI_STANDARD_CLASS;
                last_standard = ADSI_STANDARD_TDD;
            } else {
                first_standard = test_standard;
                last_standard = test_standard;
            }

            for (current_standard = first_standard; current_standard <= last_standard; current_standard++) {
                if (enable_basic_tests != 0)
                    basic_tests(current_standard);
                if (enable_talkoff_tests != 0)
                    mitel_cm7291_side_2_and_bellcore_tests(current_standard);
            }
        } finally {
            outhandle?.Dispose();
            outhandle = null;
        }

        Console.WriteLine("Tests passed.");
        return errors == 0 ? 0 : 2;
    }

    private static string RequireOptionArgument(string[] testArguments, ref int index, string option) {
        if (++index >= testArguments.Length)
            throw new ArgumentException($"Missing argument for {option}.");
        return testArguments[index];
    }

    private static int ParseStandard(string value) {
        if (value.Equals("CLASS", StringComparison.OrdinalIgnoreCase))
            return ADSI_STANDARD_CLASS;
        if (value.Equals("CLIP", StringComparison.OrdinalIgnoreCase))
            return ADSI_STANDARD_CLIP;
        if (value.Equals("A-CLIP", StringComparison.OrdinalIgnoreCase))
            return ADSI_STANDARD_ACLIP;
        if (value.Equals("J-CLIP", StringComparison.OrdinalIgnoreCase))
            return ADSI_STANDARD_JCLIP;
        if (value.Equals("CLIP-DTMF", StringComparison.OrdinalIgnoreCase))
            return ADSI_STANDARD_CLIP_DTMF;
        if (value.Equals("TDD", StringComparison.OrdinalIgnoreCase))
            return ADSI_STANDARD_TDD;
        return int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private static int adsi_create_message(adsi_tx_state_t state, byte[] msg) {
        int len = 0;

        switch (current_standard) {
            case ADSI_STANDARD_CLASS:
                if (cycle > 3)
                    cycle = 0;
                switch (cycle) {
                    case 0:
                        len = Add(state, msg, -1, CLASS_MDMF_CALLERID);
                        len = AddAscii(state, msg, len, MCLASS_DATETIME, "10011750");
                        len = AddAscii(state, msg, len, MCLASS_CALLER_NUMBER, "12345678");
                        len = AddAscii(state, msg, len, MCLASS_DIALED_NUMBER, "87654321");
                        len = AddAscii(state, msg, len, MCLASS_CALLER_NAME, "Chan Dai Man", 15);
                        break;
                    case 1:
                        len = Add(state, msg, -1, CLASS_SDMF_MSG_WAITING);
                        len = AddRaw(state, msg, len, 0, 0x42);
                        len = AddRaw(state, msg, len, 0, 0x42);
                        len = AddRaw(state, msg, len, 0, 0x42);
                        break;
                    case 2:
                        len = Add(state, msg, -1, CLASS_SDMF_MSG_WAITING);
                        len = AddRaw(state, msg, len, 0, 0x6F);
                        len = AddRaw(state, msg, len, 0, 0x6F);
                        len = AddRaw(state, msg, len, 0, 0x6F);
                        break;
                    case 3:
                        len = Add(state, msg, -1, CLASS_SDMF_CALLERID);
                        len = AddAscii(state, msg, len, 0, "10011750");
                        len = AddAscii(state, msg, len, 0, "6095551212");
                        break;
                }
                break;

            case ADSI_STANDARD_CLIP:
                if (cycle > 4)
                    cycle = 0;
                switch (cycle) {
                    case 0:
                        len = Add(state, msg, -1, CLIP_MDMF_CALLERID);
                        len = AddRaw(state, msg, len, CLIP_CALLTYPE, 0x81);
                        len = AddAscii(state, msg, len, CLIP_DATETIME, "10011750");
                        len = AddAscii(state, msg, len, CLIP_DIALED_NUMBER, "12345678");
                        len = AddAscii(state, msg, len, CLIP_CALLER_NUMBER, "87654321");
                        len = AddAscii(state, msg, len, CLIP_CALLER_NAME, "Chan Dai Man", 15);
                        break;
                    case 1:
                        len = Add(state, msg, -1, CLIP_MDMF_MSG_WAITING);
                        len = AddRaw(state, msg, len, CLIP_VISUAL_INDICATOR, 0x00);
                        break;
                    case 2:
                        len = Add(state, msg, -1, CLIP_MDMF_MSG_WAITING);
                        len = AddRaw(state, msg, len, CLIP_VISUAL_INDICATOR, 0xFF);
                        len = AddRaw(state, msg, len, CLIP_NUM_MSG, 0x05);
                        break;
                    case 3:
                        len = Add(state, msg, -1, CLIP_MDMF_SMS);
                        len = AddRaw(state, msg, len, CLIP_DISPLAY_INFO, 0x00, (byte)'A', (byte)'B', (byte)'C');
                        break;
                    case 4:
                        len = Add(state, msg, -1, CLIP_MDMF_CALLERID);
                        len = AddRaw(state, msg, len, CLIP_NUM_MSG, 0x03);
                        break;
                }
                break;

            case ADSI_STANDARD_ACLIP:
                if (cycle > 0)
                    cycle = 0;
                len = Add(state, msg, -1, ACLIP_MDMF_CALLERID);
                len = AddAscii(state, msg, len, ACLIP_DATETIME, "10011750");
                len = AddAscii(state, msg, len, ACLIP_DIALED_NUMBER, "12345678");
                len = AddAscii(state, msg, len, ACLIP_CALLER_NUMBER, "87654321");
                len = AddAscii(state, msg, len, ACLIP_CALLER_NAME, "Chan Dai Man", 15);
                break;

            case ADSI_STANDARD_JCLIP:
                len = Add(state, msg, -1, JCLIP_MDMF_CALLERID);
                len = AddAscii(state, msg, len, JCLIP_CALLER_NUMBER, "12345678");
                len = AddAscii(state, msg, len, JCLIP_CALLER_NUM_DES, "215");
                len = AddAscii(state, msg, len, JCLIP_DIALED_NUMBER, "87654321");
                len = AddAscii(state, msg, len, JCLIP_DIALED_NUM_DES, "215");
                break;

            case ADSI_STANDARD_CLIP_DTMF:
                if (cycle > 4)
                    cycle = 0;
                switch (cycle) {
                    case 0:
                        len = Add(state, msg, -1, CLIP_DTMF_C_TERMINATED);
                        len = AddAscii(state, msg, len, CLIP_DTMF_C_CALLER_NUMBER, "12345678");
                        len = AddAscii(state, msg, len, CLIP_DTMF_C_ABSENCE, "10");
                        len = AddAscii(state, msg, len, CLIP_DTMF_C_REDIRECT_NUMBER, "87654321");
                        break;
                    case 1:
                        len = Add(state, msg, -1, CLIP_DTMF_HASH_TERMINATED);
                        len = AddAscii(state, msg, len, CLIP_DTMF_HASH_CALLER_NUMBER, "12345678");
                        break;
                    case 2:
                        len = Add(state, msg, -1, CLIP_DTMF_HASH_TERMINATED);
                        len = AddAscii(state, msg, len, CLIP_DTMF_HASH_ABSENCE, "1");
                        break;
                    case 3:
                        len = Add(state, msg, -1, CLIP_DTMF_HASH_TERMINATED);
                        len = AddAscii(state, msg, len, CLIP_DTMF_HASH_ABSENCE, "12345678");
                        break;
                    case 4:
                        len = Add(state, msg, -1, CLIP_DTMF_HASH_TERMINATED);
                        len = AddAscii(state, msg, len, CLIP_DTMF_HASH_UNSPECIFIED, "12345678");
                        break;
                }
                break;

            case ADSI_STANDARD_TDD:
                const string text = "The quick Brown Fox Jumps Over The Lazy dog 0123456789!@#$%^&*()";
                len = AddAscii(state, msg, -1, 0, text);
                break;
        }

        cycle++;
        return len;
    }

    private static int Add(adsi_tx_state_t state, byte[] message, int length, int fieldType) =>
        adsi_add_field(state, message, length, checked((byte)fieldType), ReadOnlySpan<byte>.Empty, 0);

    private static int AddRaw(adsi_tx_state_t state, byte[] message, int length, int fieldType, params byte[] body) =>
        adsi_add_field(state, message, length, checked((byte)fieldType), body, body.Length);

    private static int AddAscii(adsi_tx_state_t state, byte[] message, int length, int fieldType, string body, int? forcedLength = null) {
        byte[] bytes = Encoding.ASCII.GetBytes(body);
        int fieldLength = forcedLength ?? bytes.Length;
        if (fieldLength > bytes.Length)
            Array.Resize(ref bytes, fieldLength);
        return adsi_add_field(state, message, length, checked((byte)fieldType), bytes, fieldLength);
    }

    private static void put_adsi_msg(object? user_data, ReadOnlySpan<byte> message) {
        byte[] msg = message.ToArray();
        int len = msg.Length;
        Console.WriteLine($"Good message received ({len} bytes)");
        good_message_received = true;

        for (int i = 0; i < len; i++) {
            Console.Write($"{msg[i]:x2} ");
            if ((i & 0xF) == 0xF)
                Console.WriteLine();
        }
        Console.WriteLine();

        if (rx_adsi is null)
            Exit(2);

        int position = -1;
        int message_type = -1;
        Console.WriteLine("Message breakdown");

        do {
            position = adsi_next_field(rx_adsi!, msg, len, position, out byte field_type, out ReadOnlyMemory<byte> field_body, out int field_len);
            if (position > 0) {
                if (!field_body.IsEmpty) {
                    string body = Encoding.ASCII.GetString(field_body.Span);
                    Console.Write($"Field type 0x{field_type:x}, len {field_len}, '{body}' - ");
                    Console.Write(FieldDescription(current_standard, message_type, field_type));
                } else {
                    message_type = field_type;
                    Console.Write($"Message type 0x{field_type:x} - ");
                    Console.Write(MessageDescription(current_standard, message_type));
                }
                Console.WriteLine();
            }
        }
        while (position > 0);

        if (position < -1) {
            Console.WriteLine("Bad message contents");
            Exit(2);
        }

        if (current_standard == ADSI_STANDARD_TDD && basic_testing) {
            byte[] expected = Encoding.ASCII.GetBytes("THE QUICK BROWN FOX JUMPS OVER THE LAZY DOG 0123456789#$*()");
            if (msg.Length != expected.Length || !msg.AsSpan().SequenceEqual(expected)) {
                Console.WriteLine("String error");
                Exit(2);
            }
        }

        Console.WriteLine();
    }

    private static string MessageDescription(int standard, int messageType) => standard switch {
        ADSI_STANDARD_CLASS => messageType switch {
            CLASS_SDMF_CALLERID => "Single data message caller ID",
            CLASS_MDMF_CALLERID => "Multiple data message caller ID",
            CLASS_SDMF_MSG_WAITING => "Single data message message waiting",
            CLASS_MDMF_MSG_WAITING => "Multiple data message message waiting",
            _ => "Unknown"
        },
        ADSI_STANDARD_CLIP => messageType switch {
            CLIP_MDMF_CALLERID => "Multiple data message caller ID",
            CLIP_MDMF_MSG_WAITING => "Multiple data message message waiting",
            CLIP_MDMF_CHARGE_INFO => "Multiple data message charge info",
            CLIP_MDMF_SMS => "Multiple data message SMS",
            _ => "Unknown"
        },
        ADSI_STANDARD_ACLIP => messageType switch {
            ACLIP_SDMF_CALLERID => "Single data message caller ID frame",
            ACLIP_MDMF_CALLERID => "Multiple data message caller ID frame",
            _ => "Unknown"
        },
        ADSI_STANDARD_JCLIP => messageType == JCLIP_MDMF_CALLERID ? "Multiple data message caller ID frame" : "Unknown",
        ADSI_STANDARD_CLIP_DTMF => messageType switch {
            CLIP_DTMF_HASH_TERMINATED => "# terminated",
            CLIP_DTMF_C_TERMINATED => "C terminated",
            _ => "Unknown"
        },
        _ => "Unknown"
    };

    private static string FieldDescription(int standard, int messageType, int fieldType) {
        if (standard == ADSI_STANDARD_CLASS && messageType == CLASS_MDMF_CALLERID) {
            return fieldType switch {
                MCLASS_DATETIME => "Date and time (MMDDHHMM)",
                MCLASS_CALLER_NUMBER => "Caller's number",
                MCLASS_DIALED_NUMBER => "Dialed number",
                MCLASS_ABSENCE1 => "Caller's number absent: 'O' or 'P'",
                MCLASS_REDIRECT => "Call forward: universal ('0'), on busy ('1'), or on unanswered ('2')",
                MCLASS_QUALIFIER => "Long distance: 'L'",
                MCLASS_CALLER_NAME => "Caller's name",
                MCLASS_ABSENCE2 => "Caller's name absent: 'O' or 'P'",
                _ => string.Empty
            };
        }

        if (standard == ADSI_STANDARD_CLASS && messageType == CLASS_MDMF_MSG_WAITING && fieldType == MCLASS_VISUAL_INDICATOR)
            return "Message waiting/not waiting";

        if (standard == ADSI_STANDARD_CLIP) {
            return fieldType switch {
                CLIP_DATETIME => "Date and time (MMDDHHMM)",
                CLIP_CALLER_NUMBER => "Caller's number",
                CLIP_DIALED_NUMBER => "Dialed number",
                CLIP_ABSENCE1 => "Caller's number absent",
                CLIP_CALLER_NAME => "Caller's name",
                CLIP_ABSENCE2 => "Caller's name absent",
                CLIP_VISUAL_INDICATOR => "Visual indicator",
                CLIP_MESSAGE_ID => "Message ID",
                CLIP_CALLTYPE => "Voice call, ring-back-when-free call, or msg waiting call",
                CLIP_NUM_MSG => "Number of messages",
                CLIP_CHARGE => "Charge",
                CLIP_DURATION => "Duration of the call",
                CLIP_ADD_CHARGE => "Additional charge",
                CLIP_DISPLAY_INFO => "Display information",
                CLIP_SERVICE_INFO => "Service information",
                _ => string.Empty
            };
        }

        if (standard == ADSI_STANDARD_ACLIP && messageType == ACLIP_MDMF_CALLERID) {
            return fieldType switch {
                ACLIP_DATETIME => "Date and time (MMDDHHMM)",
                ACLIP_CALLER_NUMBER => "Caller's number",
                ACLIP_DIALED_NUMBER => "Dialed number",
                ACLIP_NUMBER_ABSENCE => "Caller's number absent: 'O' or 'P'",
                ACLIP_REDIRECT => "Call forward: universal, on busy, or on unanswered",
                ACLIP_QUALIFIER => "Long distance call: 'L'",
                ACLIP_CALLER_NAME => "Caller's name",
                ACLIP_NAME_ABSENCE => "Caller's name absent: 'O' or 'P'",
                _ => string.Empty
            };
        }

        if (standard == ADSI_STANDARD_JCLIP && messageType == JCLIP_MDMF_CALLERID) {
            return fieldType switch {
                JCLIP_CALLER_NUMBER => "Caller's number",
                JCLIP_CALLER_NUM_DES => "Caller's number data extension signal",
                JCLIP_DIALED_NUMBER => "Dialed number",
                JCLIP_DIALED_NUM_DES => "Dialed number data extension signal",
                JCLIP_ABSENCE => "Caller's number absent: 'C', 'O', 'P' or 'S'",
                _ => string.Empty
            };
        }

        if (standard == ADSI_STANDARD_CLIP_DTMF) {
            if (messageType == CLIP_DTMF_HASH_TERMINATED) {
                return fieldType switch {
                    CLIP_DTMF_HASH_CALLER_NUMBER => "Caller's number",
                    CLIP_DTMF_HASH_ABSENCE => "Caller's number absent: private (1), overseas (2) or not available (3)",
                    CLIP_DTMF_HASH_UNSPECIFIED => "Unspecified",
                    _ => string.Empty
                };
            }
            if (messageType == CLIP_DTMF_C_TERMINATED) {
                return fieldType switch {
                    CLIP_DTMF_C_CALLER_NUMBER => "Caller's number",
                    CLIP_DTMF_C_REDIRECT_NUMBER => "Redirect number",
                    CLIP_DTMF_C_ABSENCE => "Caller's number absent",
                    _ => string.Empty
                };
            }
        }

        return string.Empty;
    }

    private static void tdd_character_set_tests() {
        // The original active build also leaves the internal static Baudot
        // encode/decode test disabled with #if 0. The callable test is retained
        // as an intentionally empty method for exact build-path parity.
    }

    private static void basic_tests(int standard) {
        short[] amp = new short[BLOCK_LEN];
        byte[] adsi_msg = new byte[256 + 42];
        basic_testing = true;
        Console.WriteLine($"Testing {adsi_standard_to_str(standard)}");

        tx_adsi = adsi_tx_init(null, standard);
        if (short_preamble)
            adsi_tx_set_preamble(tx_adsi, 50, 20, 5, -1);
        rx_adsi = adsi_rx_init(null, standard, put_adsi_msg, null);

        good_message_received = true;
        int push = 0;
        for (int i = 0; i < 100000; i++) {
            int len;
            if (push == 0) {
                len = adsi_tx(tx_adsi, amp, BLOCK_LEN);
                if (len == 0)
                    push = 10;
            } else {
                len = 0;
                if (--push == 0) {
                    if (!good_message_received) {
                        Console.WriteLine($"No message received {adsi_standard_to_str(standard)} ({i})");
                        Exit(2);
                    }

                    good_message_received = false;
                    int adsi_msg_len = adsi_create_message(tx_adsi, adsi_msg);
                    _ = adsi_tx_put_message(tx_adsi, adsi_msg, adsi_msg_len);
                }
            }

            if (len < BLOCK_LEN) {
                Array.Clear(amp, len, BLOCK_LEN - len);
                len = BLOCK_LEN;
            }

            if (log_audio)
                outhandle!.WriteFrames(amp.AsSpan(0, len));

            adsi_rx(rx_adsi, amp, len);
        }

        adsi_rx_free(rx_adsi);
        adsi_tx_free(tx_adsi);
        rx_adsi = null;
        tx_adsi = null;
        basic_testing = false;
    }

    private static void mitel_cm7291_side_2_and_bellcore_tests(int standard) {
        short[] amp = new short[BLOCK_LEN];
        Console.WriteLine($"Talk-off tests for {adsi_standard_to_str(standard)}");
        rx_adsi = adsi_rx_init(null, standard, put_adsi_msg, null);

        int j;
        for (j = 0; j < bellcore_files.Length && bellcore_files[j].Length != 0; j++) {
            string file = bellcore_files[j];
            Console.WriteLine($"Testing with {file}");
            using var inhandle = Pcm16WaveReader.OpenMono(file);
            while (true) {
                int frames = inhandle.ReadFrames(amp, BLOCK_LEN);
                if (frames == 0)
                    break;
                adsi_rx(rx_adsi, amp, frames);
            }
        }

        adsi_rx_free(rx_adsi);
        rx_adsi = null;

        if (j > 470) {
            Console.WriteLine("    Failed");
            Exit(2);
        }
        Console.WriteLine("    Passed");
    }

    internal static void Exit(int code) => adsi_tests_adapter.tkfx_adsi_tests_exit(code);

    internal sealed class AdsiTestExitException : Exception {
        public AdsiTestExitException(int exitCode) => ExitCode = exitCode;
        public int ExitCode { get; }
    }

    private sealed class Pcm16WaveReader : IDisposable {
        private readonly FileStream stream;
        private readonly BinaryReader reader;
        private readonly long dataEnd;
        private readonly int channels;

        private Pcm16WaveReader(FileStream stream, BinaryReader reader, long dataEnd, int channels) {
            this.stream = stream;
            this.reader = reader;
            this.dataEnd = dataEnd;
            this.channels = channels;
        }

        public static Pcm16WaveReader OpenMono(string path) {
            var stream = File.OpenRead(path);
            var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
            if (ReadFourCc(reader) != "RIFF")
                throw new InvalidDataException($"'{path}' is not a RIFF file.");
            _ = reader.ReadUInt32();
            if (ReadFourCc(reader) != "WAVE")
                throw new InvalidDataException($"'{path}' is not a WAVE file.");

            int channels = 0;
            int bits = 0;
            ushort format = 0;
            long dataStart = -1;
            uint dataLength = 0;

            while (stream.Position + 8 <= stream.Length) {
                string id = ReadFourCc(reader);
                uint size = reader.ReadUInt32();
                long next = stream.Position + size + (size & 1);
                if (id == "fmt ") {
                    format = reader.ReadUInt16();
                    channels = reader.ReadUInt16();
                    _ = reader.ReadUInt32();
                    _ = reader.ReadUInt32();
                    _ = reader.ReadUInt16();
                    bits = reader.ReadUInt16();
                } else if (id == "data") {
                    dataStart = stream.Position;
                    dataLength = size;
                    break;
                }
                stream.Position = next;
            }

            if (format != 1 || bits != 16 || channels < 1 || dataStart < 0) {
                reader.Dispose();
                stream.Dispose();
                throw new InvalidDataException($"'{path}' must be PCM16 WAV.");
            }

            stream.Position = dataStart;
            return new Pcm16WaveReader(stream, reader, dataStart + dataLength, channels);
        }

        public int ReadFrames(short[] destination, int maximumFrames) {
            int frames = 0;
            while (frames < maximumFrames && stream.Position + channels * 2L <= dataEnd) {
                short first = reader.ReadInt16();
                for (int channel = 1; channel < channels; channel++)
                    _ = reader.ReadInt16();
                destination[frames++] = first;
            }
            return frames;
        }

        public void Dispose() {
            reader.Dispose();
            stream.Dispose();
        }

        private static string ReadFourCc(BinaryReader reader) => Encoding.ASCII.GetString(reader.ReadBytes(4));
    }

    private sealed class Pcm16WaveWriter : IDisposable {
        private readonly FileStream stream;
        private readonly BinaryWriter writer;
        private readonly short channels;
        private readonly int sampleRate;
        private long samplesWritten;
        private bool disposed;

        public Pcm16WaveWriter(string path, short channels, int sampleRate) {
            this.channels = channels;
            this.sampleRate = sampleRate;
            stream = File.Create(path);
            writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
            WriteHeader(0);
        }

        public void WriteFrames(ReadOnlySpan<short> interleavedSamples) {
            foreach (short sample in interleavedSamples)
                writer.Write(sample);
            samplesWritten += interleavedSamples.Length;
        }

        public void Dispose() {
            if (disposed)
                return;
            disposed = true;
            long dataBytes = samplesWritten * sizeof(short);
            stream.Position = 0;
            WriteHeader(checked((uint)dataBytes));
            writer.Flush();
            writer.Dispose();
            stream.Dispose();
        }

        private void WriteHeader(uint dataBytes) {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36u + dataBytes);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16u);
            writer.Write((ushort)1);
            writer.Write((ushort)channels);
            writer.Write((uint)sampleRate);
            writer.Write((uint)(sampleRate * channels * sizeof(short)));
            writer.Write((ushort)(channels * sizeof(short)));
            writer.Write((ushort)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataBytes);
        }
    }
}
