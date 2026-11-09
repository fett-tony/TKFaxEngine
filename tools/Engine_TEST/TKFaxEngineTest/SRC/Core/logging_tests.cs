/*
 * TKFaxEngine - managed C# port
 *
 * logging_tests.cs - Tests for the logging functions.
 *
 * Fresh direct port of logging_tests.c.
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2006 Steve Underwood.
 * Licensed under the GNU General Public License version 2.
 */

#nullable enable

using static TKFaxEngine.LoggingApi;

namespace TKFaxEngineTest.Core.Test;

public static class logging_tests {
    private static bool tests_failed;
    private static int msg_step;
    private static int msg2_step;
    private static bool msg_done;
    private static bool msg2_done;

    private static readonly string[] ReferenceMessages =
    [
        "TAG Log with tag 1 2 3\n",
        "Log with protocol 1 2 3\n",
        "ERROR Log with severity log 1 2 3\n",
        "FLOW NewTag Log with new tag 1 2 3\n",
        "FLOW Protocol NewTag Log with protocol 1 2 3\n",
        "FLOW Protocol NewTag Buf 00 01 02 03 04 05 06 07 08 09\n",
        "FLOW Protocol NewTag Buf 00 01 02 03 04 05 06 07 08 09 0a 0b 0c 0d 0e 0f 10 11 12 13 14 15 16 17 18 19 1a 1b 1c 1d 1e 1f 20 21 22 23 24 25 26 27 28 29 2a 2b 2c 2d 2e 2f 30 31 32 33 34 35 36 37 38 39 3a 3b 3c 3d 3e 3f 40 41 42 43 44 45 46 47 48 49 4a 4b 4c 4d 4e 4f 50 51 52 53 54 55 56 57 58 59 5a 5b 5c 5d 5e 5f 60 61 62 63 64 65 66 67 68 69 6a 6b 6c 6d 6e 6f 70 71 72 73 74 75 76 77 78 79 7a 7b 7c 7d 7e 7f 80 81 82 83 84 85 86 87 88 89 8a 8b 8c 8d 8e 8f 90 91 92 93 94 95 96 97 98 99 9a 9b 9c 9d 9e 9f a0 a1 a2 a3 a4 a5 a6 a7 a8 a9 aa ab ac ad ae af b0 b1 b2 b3 b4 b5 b6 b7 b8 b9 ba bb bc bd be bf c0 c1 c2 c3 c4 c5 c6 c7 c8 c9 ca cb cc cd ce cf d0 d1 d2 d3 d4 d5 d6 d7 d8 d9 da db dc dd de df e0 e1 e2 e3 e4 e5 e6 e7 e8 e9 ea eb ec ed ee ef f0 f1 f2 f3 f4 f5 f6 f7 f8 f9 fa fb fc fd fe ff 00 01 02 03 04 05 06 07 08 09\n",
        "00:00:00.000 FLOW Protocol NewTag Time tagged log 1 2 3\n",
        "00:00:00.020 FLOW Protocol NewTag Time tagged log 1 2 3\n",
        "00:00:00.040 FLOW Protocol NewTag Time tagged log 1 2 3\n",
        "00:00:00.060 FLOW Protocol NewTag Time tagged log 1 2 3\n",
        "00:00:00.080 FLOW Protocol NewTag Time tagged log 1 2 3\n",
        "00:00:00.100 FLOW Protocol NewTag Time tagged log 1 2 3\n",
        "00:00:00.120 FLOW Protocol NewTag Time tagged log 1 2 3\n",
        "00:00:00.140 FLOW Protocol NewTag Time tagged log 1 2 3\n",
        "00:00:00.160 FLOW Protocol NewTag Time tagged log 1 2 3\n",
        "00:00:00.180 FLOW Protocol NewTag Time tagged log 1 2 3\n",
        string.Empty
    ];

    private static void message_handler(object? user_data, int level, string text) {
        if (msg_step >= ReferenceMessages.Length || ReferenceMessages[msg_step].Length == 0)
            return;

        if (!string.Equals(ReferenceMessages[msg_step], text, StringComparison.Ordinal)) {
            Console.Write(">>>: {0}", ReferenceMessages[msg_step]);
            tests_failed = true;
        }

        msg_step++;
        if (msg_step < ReferenceMessages.Length && ReferenceMessages[msg_step].Length == 0)
            msg_done = true;

        Console.Write("MSG: {0}", text);
    }

    private static void message_handler2(object? user_data, int level, string text) {
        const string expected = " FLOW Protocol NewTag Date/time tagged log 1 2 3\n";
        string suffix = text.Length >= 23 ? text[23..] : text;

        if (!string.Equals(expected, suffix, StringComparison.Ordinal)) {
            Console.Write(">>>: {0}", suffix);
            tests_failed = true;
        }

        if (++msg2_step == 10)
            msg2_done = true;

        Console.Write("MSG: {0}", text);
    }

    public static int tkfx_logging_tests_body_main(string[] testArguments) {
        tests_failed = false;
        msg_step = 0;
        msg2_step = 0;
        msg_done = false;
        msg2_done = false;

        TKFaxEngine.SpanLogState log = span_log_init(null, 123, "TAG");
        if (log is null) {
            Console.Error.WriteLine("Failed to initialise log.");
            throw new LoggingTestsExitException(2);
        }

        span_log_set_level(
            log,
            SPAN_LOG_SHOW_SEVERITY |
            SPAN_LOG_SHOW_PROTOCOL |
            SPAN_LOG_SHOW_TAG |
            SPAN_LOG_FLOW);

        if (span_log(log, SPAN_LOG_FLOW, "Logging to fprintf, as simple as %d %d %d\n", 1, 2, 3) != 0)
            Console.Error.WriteLine("Logged.");
        else
            Console.Error.WriteLine("Not logged.");

        span_log_set_message_handler(log, message_handler, null);
        span_log_set_sample_rate(log, 44_100);

        span_log_set_level(log, SPAN_LOG_SHOW_TAG | SPAN_LOG_FLOW);
        if (span_log(log, SPAN_LOG_FLOW, "Log with tag %d %d %d\n", 1, 2, 3) != 0)
            Console.Error.WriteLine("Logged.");
        else
            Console.Error.WriteLine("Not logged.");

        span_log_set_level(log, SPAN_LOG_SHOW_PROTOCOL | SPAN_LOG_FLOW);
        if (span_log(log, SPAN_LOG_FLOW, "Log with protocol %d %d %d\n", 1, 2, 3) != 0)
            Console.Error.WriteLine("Logged.");
        else
            Console.Error.WriteLine("Not logged.");

        span_log_set_level(log, SPAN_LOG_SHOW_SEVERITY | SPAN_LOG_FLOW);
        if (span_log(log, SPAN_LOG_ERROR, "Log with severity log %d %d %d\n", 1, 2, 3) != 0)
            Console.Error.WriteLine("Logged.");
        else
            Console.Error.WriteLine("Not logged.");

        span_log_set_level(
            log,
            SPAN_LOG_SHOW_SEVERITY |
            SPAN_LOG_SHOW_PROTOCOL |
            SPAN_LOG_SHOW_TAG |
            SPAN_LOG_FLOW);
        span_log_set_tag(log, "NewTag");

        if (span_log(log, SPAN_LOG_FLOW, "Log with new tag %d %d %d\n", 1, 2, 3) != 0)
            Console.Error.WriteLine("Logged.");
        else
            Console.Error.WriteLine("Not logged.");

        span_log_set_protocol(log, "Protocol");
        if (span_log(log, SPAN_LOG_FLOW, "Log with protocol %d %d %d\n", 1, 2, 3) != 0)
            Console.Error.WriteLine("Logged.");
        else
            Console.Error.WriteLine("Not logged.");

        byte[] buffer = new byte[1000];
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = unchecked((byte)i);

        if (span_log_buf(log, SPAN_LOG_FLOW, "Buf", buffer, 10) != 0)
            Console.Error.WriteLine("Logged.");
        else
            Console.Error.WriteLine("Not logged.");

        if (span_log_buf(log, SPAN_LOG_FLOW, "Buf", buffer, 1000) != 0)
            Console.Error.WriteLine("Logged.");
        else
            Console.Error.WriteLine("Not logged.");

        for (int i = 0; i < 10; i++) {
            if (!span_log_test(log, i)) {
                if (i != 6)
                    tests_failed = true;
                break;
            }
        }

        span_log_set_level(
            log,
            SPAN_LOG_SHOW_SEVERITY |
            SPAN_LOG_SHOW_PROTOCOL |
            SPAN_LOG_SHOW_TAG |
            SPAN_LOG_FLOW |
            SPAN_LOG_SHOW_SAMPLE_TIME);

        for (int i = 0; i < 10; i++) {
            span_log(log, SPAN_LOG_FLOW, "Time tagged log %d %d %d\n", 1, 2, 3);
            span_log_bump_samples(log, 441 * 2);
        }

        span_log_set_message_handler(log, message_handler2, null);
        span_log_set_level(
            log,
            SPAN_LOG_SHOW_SEVERITY |
            SPAN_LOG_SHOW_PROTOCOL |
            SPAN_LOG_SHOW_TAG |
            SPAN_LOG_FLOW |
            SPAN_LOG_SHOW_DATE);

        for (int i = 0; i < 10; i++) {
            span_log(log, SPAN_LOG_FLOW, "Date/time tagged log %d %d %d\n", 1, 2, 3);
            Thread.Sleep(20);
        }

        if (tests_failed || !msg_done || !msg2_done) {
            Console.WriteLine("Tests failed - failed={0} handler1={1} handler2={2}.",
                tests_failed ? 1 : 0, msg_done ? 1 : 0, msg2_done ? 1 : 0);
            return 2;
        }

        span_log_set_message_handler(log, message_handler, null);
        span_log_free(log);

        Console.WriteLine("Tests passed.");
        return 0;
    }

    public static int Main(string[] args) => tkfx_logging_tests_body_main(args);

    public sealed class LoggingTestsExitException : Exception {
        public LoggingTestsExitException(int exitCode)
            : base($"logging_tests requested process exit {exitCode}.") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }
}
