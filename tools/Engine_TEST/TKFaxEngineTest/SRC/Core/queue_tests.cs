/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * queue_tests.c
 *
 * Written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2007 Steve Underwood.
 * GNU General Public License version 2.
 *
 * Managed C# port preserving the supplied spanDSP functional and threaded
 * stream/message queue test sequences and exact loop limits.
 */

#nullable enable
using TKFaxEngine;

using System.Runtime.ExceptionServices;
using TKFaxEngineTest;
using static TKFaxEngine.QueueApi;

namespace TKFaxEngineTest.Core.Test;

public static class QueueTests {
    private const int BUF_LEN = 10_000;
    private const int MSG_LEN = 17;

    private static QueueState queue = null!;
    private static volatile int put_oks;
    private static volatile int put_misses;
    private static volatile int got_oks;
    private static volatile int got_misses;
    private static int total_in;
    private static int total_out;
    private static Exception? threaded_failure;

    private static void tests_failed() => throw new TestProcessExitException(2);

    private static void display_queue_pointers() {
        int internalLength = queue.InternalLength;
        int inputPointer = internalLength == 0 ? 0 : total_in % internalLength;
        int outputPointer = internalLength == 0 ? 0 : total_out % internalLength;
        Console.WriteLine($"Pointers {inputPointer} {outputPointer} {internalLength}");
    }

    private static void RecordThreadFailure(Exception exception) {
        Interlocked.CompareExchange(ref threaded_failure, exception, null);
    }

    private static void run_stream_write() {
        try {
            byte[] buffer = new byte[MSG_LEN];
            int next = 0;

            Console.WriteLine("Write thread");
            Array.Fill(buffer, unchecked((byte)next));
            next = (next + 1) & 0xFF;
            put_oks = 0;
            put_misses = 0;

            for (; ; ) {
                if (queue_write(queue, buffer, MSG_LEN) == MSG_LEN) {
                    Array.Fill(buffer, unchecked((byte)next));
                    next = (next + 1) & 0xFF;
                    put_oks++;
                    if (put_oks % 1_000_000 == 0)
                        Console.WriteLine($"{put_oks} puts, {put_misses} misses");
                } else {
                    Thread.Yield();
                    put_misses++;
                }
            }
        } catch (Exception exception) {
            RecordThreadFailure(exception);
        }
    }

    private static void run_stream_read() {
        try {
            byte[] buffer = new byte[MSG_LEN];
            int next = 0;

            Console.WriteLine("Read thread");
            got_oks = 0;
            got_misses = 0;

            for (; ; ) {
                int length = queue_read(queue, buffer, MSG_LEN);
                if (length >= 0) {
                    if (length != MSG_LEN) {
                        Console.WriteLine($"AHH! - len {length}");
                        tests_failed();
                    }

                    for (int i = 0; i < length; ++i) {
                        if (buffer[i] != next) {
                            Console.WriteLine($"AHH! - 0x{buffer[i]:X} 0x{next:X}");
                            tests_failed();
                        }
                    }

                    next = (next + 1) & 0xFF;
                    got_oks++;
                    if (got_oks % 1_000_000 == 0)
                        Console.WriteLine($"{got_oks} gots, {got_misses} misses");
                } else {
                    Thread.Yield();
                    got_misses++;
                }
            }
        } catch (Exception exception) {
            RecordThreadFailure(exception);
        }
    }

    private static void threaded_stream_tests() {
        queue = queue_init(null, BUF_LEN, QUEUE_READ_ATOMIC | QUEUE_WRITE_ATOMIC);
        threaded_failure = null;

        Thread writer = new Thread(run_stream_write) { IsBackground = true };
        Thread reader = new Thread(run_stream_read) { IsBackground = true };
        writer.Start();
        reader.Start();

        for (; ; ) {
            Thread.Sleep(5_000);
            Exception? failure = Volatile.Read(ref threaded_failure);
            if (failure is not null)
                ExceptionDispatchInfo.Capture(failure).Throw();
            Console.WriteLine($"Main thread - {put_oks} {got_oks}");
        }
    }

    private static void run_message_write() {
        try {
            byte[] buffer = new byte[MSG_LEN];
            int next = 0;

            Console.WriteLine("Write thread");
            Array.Fill(buffer, unchecked((byte)next));
            next = (next + 1) & 0xFF;
            put_oks = 0;
            put_misses = 0;

            for (; ; ) {
                if (queue_write_msg(queue, buffer, MSG_LEN) == MSG_LEN) {
                    Array.Fill(buffer, unchecked((byte)next));
                    next = (next + 1) & 0xFF;
                    put_oks++;
                    if (put_oks % 1_000_000 == 0)
                        Console.WriteLine($"{put_oks} puts, {put_misses} misses");
                } else {
                    Thread.Yield();
                    put_misses++;
                }
            }
        } catch (Exception exception) {
            RecordThreadFailure(exception);
        }
    }

    private static void run_message_read() {
        try {
            byte[] buffer = new byte[1_024];
            int next = 0;

            Console.WriteLine("Read thread");
            got_oks = 0;
            got_misses = 0;

            for (; ; ) {
                int length = queue_read_msg(queue, buffer, buffer.Length);
                if (length >= 0) {
                    if (length != MSG_LEN) {
                        Console.WriteLine($"AHH! - len {length}");
                        tests_failed();
                    }

                    for (int i = 0; i < length; ++i) {
                        if (buffer[i] != next) {
                            Console.WriteLine($"AHH! - 0x{buffer[i]:X} 0x{next:X}");
                            tests_failed();
                        }
                    }

                    next = (next + 1) & 0xFF;
                    got_oks++;
                    if (got_oks % 1_000_000 == 0)
                        Console.WriteLine($"{got_oks} gots, {got_misses} misses");
                } else {
                    Thread.Yield();
                    got_misses++;
                }
            }
        } catch (Exception exception) {
            RecordThreadFailure(exception);
        }
    }

    private static void threaded_message_tests() {
        queue = queue_init(null, BUF_LEN, QUEUE_READ_ATOMIC | QUEUE_WRITE_ATOMIC);
        threaded_failure = null;

        Thread writer = new Thread(run_message_write) { IsBackground = true };
        Thread reader = new Thread(run_message_read) { IsBackground = true };
        writer.Start();
        reader.Start();

        for (; ; ) {
            Thread.Sleep(5_000);
            Exception? failure = Volatile.Read(ref threaded_failure);
            if (failure is not null)
                ExceptionDispatchInfo.Capture(failure).Throw();
            Console.WriteLine($"Main thread - {put_oks} {got_oks}");
        }
    }

    private static void check_contents(int totalIn, int totalOut) {
        int expectedContents = totalIn - totalOut;
        if (queue_contents(queue) != expectedContents) {
            Console.WriteLine($"Contents = {queue_contents(queue)} ({expectedContents})");
            display_queue_pointers();
            tests_failed();
        }

        int expectedFree = BUF_LEN - expectedContents;
        if (queue_free_space(queue) != expectedFree) {
            Console.WriteLine($"Free space = {queue_free_space(queue)} ({expectedFree})");
            display_queue_pointers();
            tests_failed();
        }
    }

    private static int monitored_queue_write(byte[] buffer, int length) {
        int actual = queue_write(queue, buffer, length);
        if (actual >= 0)
            total_in += actual;
        check_contents(total_in, total_out);
        return actual;
    }

    private static int monitored_queue_write_byte(int value) {
        int result = queue_write_byte(queue, unchecked((byte)value));
        if (result >= 0)
            total_in++;
        check_contents(total_in, total_out);
        return result;
    }

    private static int monitored_queue_read(byte[] buffer, int length) {
        int actual = queue_read(queue, buffer, length);
        if (actual >= 0)
            total_out += actual;
        check_contents(total_in, total_out);
        return actual;
    }

    private static int monitored_queue_read_byte() {
        int result = queue_read_byte(queue);
        if (result >= 0)
            total_out++;
        check_contents(total_in, total_out);
        return result;
    }

    private static void functional_stream_tests() {
        byte[] buffer = new byte[MSG_LEN];
        total_in = 0;
        total_out = 0;

        for (int index = 0; index < MSG_LEN; ++index)
            buffer[index] = unchecked((byte)index);

        queue = queue_init(null, BUF_LEN, QUEUE_READ_ATOMIC | QUEUE_WRITE_ATOMIC);
        check_contents(total_in, total_out);

        int i;
        int result = -1;

        for (i = 1; i < 5_000; ++i) {
            if (monitored_queue_write_byte(i & 0xFF) != 1) {
                Console.WriteLine($"Byte by byte full at {i}/{BUF_LEN}");
                tests_failed();
            }
        }

        for (i = 1; i < 5_001; ++i) {
            result = monitored_queue_read_byte();
            if (result != (i & 0xFF))
                break;
        }

        Console.WriteLine($"Byte by byte read breaks at {i} (expected 5000) - {result}");
        if (i != 5_000)
            tests_failed();

        for (i = 1; i < 20_000; ++i) {
            if (monitored_queue_write_byte(i & 0xFF) != 1)
                break;
        }

        Console.WriteLine($"Byte by byte full at {i} (expected 10001)");
        if (i != 10_001)
            tests_failed();

        for (i = 1; i < 20_000; ++i) {
            result = monitored_queue_read_byte();
            if (result != (i & 0xFF))
                break;
        }

        Console.WriteLine($"Byte by byte read breaks at {i} (expected 10001) - {result}");
        if (i != 10_001)
            tests_failed();

        for (i = 1; i < 1_000; ++i) {
            if (monitored_queue_write(buffer, MSG_LEN) != MSG_LEN)
                break;
        }

        Console.WriteLine($"Full at chunk {i} (expected {BUF_LEN / MSG_LEN + 1})");
        if (i != BUF_LEN / MSG_LEN + 1)
            tests_failed();

        if (monitored_queue_write(buffer, 5) == 5) {
            Console.WriteLine("Write of 5 succeeded");
            tests_failed();
        }

        if (monitored_queue_write(buffer, 4) != 4) {
            Console.WriteLine("Write of 4 failed");
            tests_failed();
        }

        if (monitored_queue_read(buffer, MSG_LEN) != MSG_LEN) {
            Console.WriteLine("Read failed");
            tests_failed();
        }

        if (monitored_queue_write(buffer, MSG_LEN) != MSG_LEN) {
            Console.WriteLine("Write failed");
            tests_failed();
        }

        for (; ; ) {
            if (monitored_queue_read(buffer, MSG_LEN) != MSG_LEN)
                break;
        }

        if (monitored_queue_read(buffer, 4) != 4) {
            Console.WriteLine("Read failed");
            tests_failed();
        }

        for (i = 1; i < 588; ++i) {
            if (monitored_queue_write(buffer, MSG_LEN) != MSG_LEN) {
                Console.WriteLine("Write failed");
                tests_failed();
            }
            if (monitored_queue_read(buffer, MSG_LEN) != MSG_LEN) {
                Console.WriteLine("Read failed");
                tests_failed();
            }
        }

        for (i = 1; i < 1_000; ++i) {
            if (monitored_queue_write(buffer, MSG_LEN) != MSG_LEN)
                break;
        }

        Console.WriteLine($"Full at chunk {i} (expected {BUF_LEN / MSG_LEN + 1})");
        if (i != BUF_LEN / MSG_LEN + 1)
            tests_failed();
        display_queue_pointers();

        if (monitored_queue_read(buffer, MSG_LEN) != MSG_LEN) {
            Console.WriteLine("Read failed");
            tests_failed();
        }
        if (monitored_queue_write(buffer, MSG_LEN) != MSG_LEN) {
            Console.WriteLine("Write failed");
            tests_failed();
        }
        display_queue_pointers();

        for (i = 1; i < 5_000; ++i) {
            if (monitored_queue_read(buffer, MSG_LEN) != MSG_LEN) {
                Console.WriteLine("Read failed");
                tests_failed();
            }
            if (monitored_queue_write(buffer, MSG_LEN) != MSG_LEN) {
                Console.WriteLine("Write failed");
                tests_failed();
            }
        }
        display_queue_pointers();

        if (monitored_queue_write(buffer, 5) == 5) {
            Console.WriteLine("Write of 5 succeeded");
            tests_failed();
        }
        if (monitored_queue_write(buffer, 4) != 4) {
            Console.WriteLine("Write of 4 failed");
            tests_failed();
        }
        display_queue_pointers();

        for (i = 1; i < 5_000; ++i) {
            if (monitored_queue_read(buffer, MSG_LEN) != MSG_LEN) {
                Console.WriteLine("Read failed");
                tests_failed();
            }
            if (monitored_queue_write(buffer, MSG_LEN) != MSG_LEN) {
                Console.WriteLine("Write failed");
                tests_failed();
            }
        }
        display_queue_pointers();
        queue_free(queue);
    }

    private static int monitored_queue_write_msg(byte[] buffer, int length) {
        int actual = queue_write_msg(queue, buffer, length);
        if (actual >= 0)
            total_in += actual + sizeof(ushort);
        check_contents(total_in, total_out);
        return actual;
    }

    private static int monitored_queue_read_msg(byte[] buffer, int length) {
        int actual = queue_read_msg(queue, buffer, length);
        if (actual >= 0)
            total_out += actual + sizeof(ushort);
        check_contents(total_in, total_out);
        return actual;
    }

    private static void functional_message_tests() {
        byte[] buffer = new byte[MSG_LEN];
        total_in = 0;
        total_out = 0;

        for (int index = 0; index < MSG_LEN; ++index)
            buffer[index] = unchecked((byte)index);

        queue = queue_init(null, BUF_LEN, QUEUE_READ_ATOMIC | QUEUE_WRITE_ATOMIC);
        check_contents(total_in, total_out);

        int i;
        int length;
        for (i = 1; i < 1_000; ++i) {
            if (monitored_queue_write_msg(buffer, MSG_LEN) != MSG_LEN)
                break;
        }

        Console.WriteLine(
            $"Full at chunk {i} (expected {BUF_LEN / (MSG_LEN + sizeof(ushort)) + 1})");
        if (i != BUF_LEN / (MSG_LEN + sizeof(ushort)) + 1)
            tests_failed();

        length = monitored_queue_write_msg(buffer, 5);
        if (length == 5) {
            Console.WriteLine("Write of 5 succeeded");
            tests_failed();
        }

        length = monitored_queue_write_msg(buffer, 4);
        if (length != 4) {
            Console.WriteLine("Write of 4 failed");
            tests_failed();
        }

        length = monitored_queue_read_msg(buffer, MSG_LEN);
        if (length != MSG_LEN) {
            Console.WriteLine($"Read failed - {length}");
            tests_failed();
        }

        length = monitored_queue_write_msg(buffer, MSG_LEN);
        if (length != MSG_LEN) {
            Console.WriteLine($"Write failed - {length}");
            tests_failed();
        }

        for (; ; ) {
            length = monitored_queue_read_msg(buffer, MSG_LEN);
            if (length != MSG_LEN)
                break;
        }

        if (length != 4) {
            Console.WriteLine($"Read failed - {length}");
            tests_failed();
        }

        for (i = 1; i < 527; ++i) {
            length = monitored_queue_write_msg(buffer, MSG_LEN);
            if (length != MSG_LEN) {
                Console.WriteLine($"Write failed - {length}");
                tests_failed();
            }
            length = monitored_queue_read_msg(buffer, MSG_LEN);
            if (length != MSG_LEN) {
                Console.WriteLine($"Read failed - {length}");
                tests_failed();
            }
        }

        for (i = 1; i < 1_000; ++i) {
            length = monitored_queue_write_msg(buffer, MSG_LEN);
            if (length != MSG_LEN)
                break;
        }

        Console.WriteLine($"Free space = {queue_free_space(queue)} ({BUF_LEN - (total_in - total_out)})");
        display_queue_pointers();
        Console.WriteLine(
            $"Full at chunk {i} (expected {BUF_LEN / (MSG_LEN + sizeof(ushort))})");
        if (i != BUF_LEN / (MSG_LEN + sizeof(ushort)))
            tests_failed();
        display_queue_pointers();

        length = monitored_queue_read_msg(buffer, MSG_LEN);
        if (length != MSG_LEN) {
            Console.WriteLine($"Read failed - {length}");
            tests_failed();
        }
        length = monitored_queue_write_msg(buffer, MSG_LEN);
        if (length != MSG_LEN) {
            Console.WriteLine($"Write failed - {length}");
            tests_failed();
        }
        display_queue_pointers();

        for (i = 1; i < 5_000; ++i) {
            length = monitored_queue_read_msg(buffer, MSG_LEN);
            if (length != MSG_LEN) {
                Console.WriteLine($"Read failed - {length}");
                tests_failed();
            }
            length = monitored_queue_write_msg(buffer, MSG_LEN);
            if (length != MSG_LEN) {
                Console.WriteLine($"Write failed - {length}");
                tests_failed();
            }
        }
        display_queue_pointers();

        length = monitored_queue_write_msg(buffer, 5);
        if (length == 5) {
            Console.WriteLine("Write of 5 succeeded");
            tests_failed();
        }
        length = monitored_queue_write_msg(buffer, 4);
        if (length != 4) {
            Console.WriteLine("Write of 4 failed");
            tests_failed();
        }
        display_queue_pointers();

        for (i = 1; i < 5_000; ++i) {
            if (i == 527) {
                length = monitored_queue_read_msg(buffer, MSG_LEN);
                if (length != 4) {
                    Console.WriteLine($"Read failed - {length}");
                    tests_failed();
                }
            }

            length = monitored_queue_read_msg(buffer, MSG_LEN);
            if (length != MSG_LEN) {
                Console.WriteLine($"Read failed - {length}");
                tests_failed();
            }
            length = monitored_queue_write_msg(buffer, MSG_LEN);
            if (length != MSG_LEN) {
                Console.WriteLine($"Write failed - {length}");
                tests_failed();
            }
        }
        display_queue_pointers();
        queue_free(queue);
    }

    public static int tkfx_queue_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        bool threadedMessages = false;
        bool threadedStreams = false;

        for (int argumentIndex = 0; argumentIndex < testArguments.Length; argumentIndex++) {
            string argument = testArguments[argumentIndex];
            if (argument == "--")
                break;
            if (argument.Length < 2 || (argument[0] != '-' && argument[0] != '/'))
                break;

            for (int optionIndex = 1; optionIndex < argument.Length; optionIndex++) {
                switch (argument[optionIndex]) {
                    case 'm':
                        threadedMessages = true;
                        break;
                    case 's':
                        threadedStreams = true;
                        break;
                }
            }
        }

        Console.WriteLine("Stream mode functional tests");
        functional_stream_tests();
        Console.WriteLine("Message mode functional tests");
        functional_message_tests();

        if (threadedStreams) {
            Console.WriteLine("Stream mode threaded tests");
            threaded_stream_tests();
        }

        if (threadedMessages) {
            Console.WriteLine("Message mode threaded tests");
            threaded_message_tests();
        }

        return 0;
    }
}
