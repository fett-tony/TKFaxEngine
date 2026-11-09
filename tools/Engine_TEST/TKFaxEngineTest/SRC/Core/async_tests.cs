/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * async_tests.cs - tests for asynchronous serial processing.
 *
 * Original C test written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2004 Steve Underwood
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License version 2, as
 * published by the Free Software Foundation.
 *
 * Managed C# port. The seven original serial formats, 1000-character load,
 * parity/framing checks and V.14 stop-bit deletion sequence are retained.
 */

#nullable enable

using static TKFaxEngine.AsyncApi;

namespace TKFaxEngineTest.Core.Test;

public static class async_tests {
    private static TKFaxEngine.AsyncReceiver? rx_async;
    private static TKFaxEngine.AsyncTransmitter? tx_async;

    private static int full_len = 0;
    private static readonly byte[] old_buf = new byte[1000];
    private static readonly byte[] new_buf = new byte[1000];

    private static volatile int tx_async_chars;
    private static volatile int rx_async_chars;
    private static volatile int rx_async_char_mask;

    private static int v14_destuff;

    public static int main(string[] testArguments) {
        try {
            return tkfx_async_tests_body_main(testArguments);
        } catch (AsyncTestExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_async_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);
        _ = full_len;
        _ = old_buf;
        _ = new_buf;

        int bit;

        Console.WriteLine("Test with async 8N1");
        tx_async = async_tx_init(tx_async, 8, (int)TKFaxEngine.AsyncParity.None, 1, false, test_get_async_byte, null);
        rx_async = async_rx_init(rx_async, 8, (int)TKFaxEngine.AsyncParity.None, 1, false, test_put_async_byte, null);
        tx_async_chars = 0;
        rx_async_chars = 0;
        rx_async_char_mask = 0xFF;
        while (rx_async_chars < 1000) {
            bit = async_tx_get_bit(tx_async);
            async_rx_put_bit(rx_async, bit);
        }
        CheckResult();

        Console.WriteLine("Test with async 8M1");
        tx_async = async_tx_init(tx_async, 8, (int)TKFaxEngine.AsyncParity.Mark, 1, false, test_get_async_byte, null);
        rx_async = async_rx_init(rx_async, 8, (int)TKFaxEngine.AsyncParity.Mark, 1, false, test_put_async_byte, null);
        tx_async_chars = 0;
        rx_async_chars = 0;
        rx_async_char_mask = 0xFF;
        while (rx_async_chars < 1000) {
            bit = async_tx_get_bit(tx_async);
            async_rx_put_bit(rx_async, bit);
        }
        CheckResult();

        Console.WriteLine("Test with async 8S1");
        tx_async = async_tx_init(tx_async, 8, (int)TKFaxEngine.AsyncParity.Space, 1, false, test_get_async_byte, null);
        rx_async = async_rx_init(rx_async, 8, (int)TKFaxEngine.AsyncParity.Space, 1, false, test_put_async_byte, null);
        tx_async_chars = 0;
        rx_async_chars = 0;
        rx_async_char_mask = 0xFF;
        while (rx_async_chars < 1000) {
            bit = async_tx_get_bit(tx_async);
            async_rx_put_bit(rx_async, bit);
        }
        CheckResult();

        Console.WriteLine("Test with async 7E1");
        tx_async = async_tx_init(tx_async, 7, (int)TKFaxEngine.AsyncParity.Even, 1, false, test_get_async_byte, null);
        rx_async = async_rx_init(rx_async, 7, (int)TKFaxEngine.AsyncParity.Even, 1, false, test_put_async_byte, null);
        tx_async_chars = 0;
        rx_async_chars = 0;
        rx_async_char_mask = 0x7F;
        while (rx_async_chars < 1000) {
            bit = async_tx_get_bit(tx_async);
            async_rx_put_bit(rx_async, bit);
        }
        CheckResult();

        Console.WriteLine("Test with async 8O1");
        tx_async = async_tx_init(tx_async, 8, (int)TKFaxEngine.AsyncParity.Odd, 1, false, test_get_async_byte, null);
        rx_async = async_rx_init(rx_async, 8, (int)TKFaxEngine.AsyncParity.Odd, 1, false, test_put_async_byte, null);
        tx_async_chars = 0;
        rx_async_chars = 0;
        rx_async_char_mask = 0xFF;
        while (rx_async_chars < 1000) {
            bit = async_tx_get_bit(tx_async);
            async_rx_put_bit(rx_async, bit);
        }
        CheckResult();

        Console.WriteLine("Test with async 8O1 and V.14");
        tx_async = async_tx_init(tx_async, 8, (int)TKFaxEngine.AsyncParity.Odd, 1, true, test_get_async_byte, null);
        rx_async = async_rx_init(rx_async, 8, (int)TKFaxEngine.AsyncParity.Odd, 1, true, test_put_async_byte, null);
        var v14State = new V14AsyncTxState(tx_async, test_get_async_byte, null);
        tx_async_chars = 0;
        rx_async_chars = 0;
        rx_async_char_mask = 0xFF;
        v14_destuff = 0;
        while (rx_async_chars < 1000) {
            bit = v14_test_async_tx_get_bit(v14State);
            async_rx_put_bit(rx_async, bit);
        }
        CheckResult();

        Console.WriteLine("Test with async 5N2");
        tx_async = async_tx_init(tx_async, 5, (int)TKFaxEngine.AsyncParity.None, 2, false, test_get_async_byte, null);
        rx_async = async_rx_init(rx_async, 5, (int)TKFaxEngine.AsyncParity.None, 2, false, test_put_async_byte, null);
        tx_async_chars = 0;
        rx_async_chars = 0;
        rx_async_char_mask = 0x1F;
        while (rx_async_chars < 1000) {
            bit = async_tx_get_bit(tx_async);
            async_rx_put_bit(rx_async, bit);
        }
        CheckResult();

        Console.WriteLine("Tests passed.");
        return 0;
    }

    public static int v14_test_async_tx_get_bit(object? userData) {
        if (userData is not V14AsyncTxState state)
            throw new ArgumentException("userData must reference a V14AsyncTxState.", nameof(userData));

        int bit;

        if (state.BitPosition == 0) {
            state.FrameInProgress = unchecked((ushort)state.GetByte(state.UserData));
            state.FrameInProgress &= unchecked((ushort)(0xFFFF >> (16 - state.DataBits)));

            if (state.Parity != TKFaxEngine.AsyncParity.None) {
                int parityBit = Parity8(state.FrameInProgress);
                if (state.Parity == TKFaxEngine.AsyncParity.Odd)
                    parityBit ^= 1;

                state.FrameInProgress |= unchecked((ushort)(parityBit << state.DataBits));
                state.FrameInProgress |= unchecked((ushort)(0xFFFF << (state.DataBits + 1)));
            } else {
                state.FrameInProgress |= unchecked((ushort)(0xFFFF << state.DataBits));
            }

            bit = 0;
            state.BitPosition++;
        } else {
            bit = state.FrameInProgress & 1;
            state.FrameInProgress >>= 1;

            if ((++v14_destuff & 3) == 0) {
                if (++state.BitPosition > state.TotalBits - 1)
                    state.BitPosition = 0;
            } else {
                if (++state.BitPosition > state.TotalBits)
                    state.BitPosition = 0;
            }
        }

        return bit;
    }

    private static int test_get_async_byte(object? userData) {
        _ = userData;
        int value = tx_async_chars & 0xFF;
        tx_async_chars++;
        return value;
    }

    private static void test_put_async_byte(object? userData, int value) {
        _ = userData;
        int expected = rx_async_chars & rx_async_char_mask;
        if (expected != value)
            Console.WriteLine($"Received byte is 0x{value:X} (expected 0x{rx_async_chars:X})");

        rx_async_chars++;
    }

    private static void CheckResult() {
        TKFaxEngine.AsyncReceiver receiver = rx_async
            ?? throw new InvalidOperationException("The asynchronous receiver is not initialized.");

        Console.WriteLine(
            $"Chars={tx_async_chars}/{rx_async_chars}, " +
            $"PE={receiver.ParityErrors}, FE={receiver.FramingErrors}");

        if (tx_async_chars != rx_async_chars ||
            receiver.ParityErrors != 0 ||
            receiver.FramingErrors != 0) {
            Console.WriteLine("Test failed.");
            Exit(2);
        }
    }

    private static int Parity8(ushort value) {
        byte byteValue = unchecked((byte)value);
        byteValue = unchecked((byte)((byteValue ^ (byteValue >> 4)) & 0x0F));
        return (0x6996 >> byteValue) & 1;
    }

    private static void Exit(int code) {
        throw new AsyncTestExitException(code);
    }

    public sealed class AsyncTestExitException : Exception {
        public AsyncTestExitException(int exitCode)
            : base($"async_tests requested exit code {exitCode}.") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }

    public sealed class V14AsyncTxState {
        public V14AsyncTxState(
            TKFaxEngine.AsyncTransmitter transmitter,
            TKFaxEngine.SpanGetByteDelegate getByte,
            object? userData) {
            ArgumentNullException.ThrowIfNull(transmitter);
            ArgumentNullException.ThrowIfNull(getByte);

            DataBits = transmitter.DataBits;
            Parity = transmitter.Parity;
            TotalBits = transmitter.TotalBits;
            GetByte = getByte;
            UserData = userData;
        }

        public short DataBits { get; }

        public TKFaxEngine.AsyncParity Parity { get; }

        public short TotalBits { get; }

        public TKFaxEngine.SpanGetByteDelegate GetByte { get; }

        public object? UserData { get; }

        public ushort FrameInProgress { get; set; }

        public short BitPosition { get; set; }
    }
}
