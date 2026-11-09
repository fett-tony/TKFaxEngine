/*
 * Fresh managed C# port of sprt_tests.c.
 * Deterministic two-peer tests for the V.150.1 SPRT protocol.
 */

#nullable enable
using SprtState = TKFaxEngine.SprtState;
using SprtConstants = TKFaxEngine.SprtConstants;
using SprtTransmissionChannel = TKFaxEngine.SprtTransmissionChannel;
using SprtLogLevel = TKFaxEngine.SprtLogLevel;

using System.Runtime.InteropServices;
using System.Text;
using TKFaxEngineTest.System;
using static TKFaxEngineTest.System.socket_dgram_harness;

namespace TKFaxEngineTest.Modem.Test;

public static class sprt_tests_source {
    private const int ChannelCount = SprtConstants.ChannelCount;
    private const int AutomatedMessagesPerChannel = 12;
    private const ulong AutomatedStartDelayMicroseconds = 500_000;
    private const ulong AutomatedTimeoutMicroseconds = 15_000_000;
    private const ulong AutomatedStopGraceMicroseconds = 500_000;
    private const string CallerSocketName = "/tmp/sprt_socket_a";
    private const string AnswererSocketName = "/tmp/sprt_socket_b";

    private static readonly ushort[] TxSequenceNumbers = new ushort[ChannelCount];
    private static readonly ushort[] RxSequenceNumbers = new ushort[ChannelCount];
    private static readonly int[] MaximumPayloads = new int[ChannelCount];
    private static readonly int[] TxMessages = new int[ChannelCount];
    private static readonly int[] RxMessages = new int[ChannelCount];

    private static SprtState? _sprtState;
    private static socket_dgram_harness_state_t? _datagramState;
    private static int _paceNumber;
    private static ulong _paceTimer;
    private static ulong _sprtTimer;
    private static bool _sendMessages;
    private static bool _automatedMode;
    private static int _testErrors;
    private static ulong _testStarted;
    private static ulong _completionStarted;

    public static int tkfx_sprt_tests_body_main(string[] args) {
        ArgumentNullException.ThrowIfNull(args);

        bool callingParty = false;
        _automatedMode = false;

        foreach (string option in args) {
            switch (option) {
                case "-a":
                    callingParty = false;
                    break;
                case "-c":
                    callingParty = true;
                    break;
                case "-x":
                    _automatedMode = true;
                    break;
                default:
                    return 2;
            }
        }

        int result = sprt_tests(callingParty);
        if (result != 0)
            return 2;

        Console.WriteLine("Tests passed");
        return 0;
    }

    private static void record_error(string text, int channel, int expected, int actual) {
        _testErrors++;
        Console.Error.WriteLine(
            $"ERROR SPRT {text} channel={channel} expected={expected} actual={actual}");
    }

    private static void terminal_callback(object? userData, ReadOnlySpan<byte> message) {
    }

    private static int termios_callback(object? userData, termios termios) {
        _ = userData;
        _ = termios;
        return 0;
    }

    private static void hangup_callback(object? userData, int status) {
    }

    private static int terminal_free_space_callback(object? userData) => 42;

    private static void rx_callback(object? userData, ReadOnlySpan<byte> buffer) {
        if (userData is not SprtState state || state.ReceivePacket(buffer) != 0)
            record_error("sprt_rx_packet", -1, 0, -1);
    }

    private static int tx_callback(object? userData, Span<byte> buffer, int len) {
        if (userData is not SprtState state)
            return -1;
        return state.TransmitMessage(
            (int)SprtTransmissionChannel.ReliableSequenced,
            buffer[..len]);
    }

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern unsafe int sendto(
        nuint s,
        byte* buf,
        int len,
        int flags,
        ref sockaddr_in to,
        int tolen);

    private static unsafe int tx_packet_handler(object? userData, ReadOnlySpan<byte> packet) {
        if (userData is not socket_dgram_harness_state_t state) {
            record_error("network state", -1, 1, 0);
            return -1;
        }

        int sentLength;
        fixed (byte* raw = packet)
            sentLength = sendto(state.net_fd, raw, packet.Length, 0, ref state.far_addr, state.far_addr_len);
        if (sentLength != packet.Length) {
            record_error("network write length", -1, packet.Length, sentLength);
            return -1;
        }
        return 0;
    }

    private static byte[] expected_message(int channel, int sequence) {
        string text = channel switch {
            (int)SprtTransmissionChannel.UnreliableUnsequenced =>
                $"Unreliable unsequenced {sequence}",
            (int)SprtTransmissionChannel.ReliableSequenced =>
                $"Reliable sequenced {sequence}",
            (int)SprtTransmissionChannel.ExpeditedReliableSequenced =>
                $"Expedited reliable sequenced {sequence}",
            (int)SprtTransmissionChannel.UnreliableSequenced =>
                $"Unreliable sequenced {sequence}",
            _ => throw new ArgumentOutOfRangeException(nameof(channel))
        };

        // snprintf(...)+1 in the native source includes the terminating NUL.
        return Encoding.ASCII.GetBytes(text + '\0');
    }

    private static int rx_delivery_handler(
        object? userData,
        int channel,
        int sequenceNumber,
        ReadOnlySpan<byte> message) {
        if (channel < 0 || channel >= ChannelCount) {
            record_error("invalid channel", channel, ChannelCount - 1, channel);
            return -1;
        }

        int expectedSequence;
        if (channel == (int)SprtTransmissionChannel.UnreliableUnsequenced) {
            if (sequenceNumber != 0)
                record_error("unsequenced sequence number", channel, 0, sequenceNumber);
            bump_sequence_number(ref RxSequenceNumbers[channel]);
            expectedSequence = RxSequenceNumbers[channel];
        } else {
            expectedSequence = (RxSequenceNumbers[channel] + 1) & 0x3FFF;
            if (sequenceNumber != expectedSequence) {
                record_error(
                    "non-consecutive sequence number",
                    channel,
                    expectedSequence,
                    sequenceNumber);
            }
            RxSequenceNumbers[channel] = unchecked((ushort)sequenceNumber);
        }

        byte[] expected = expected_message(channel, expectedSequence);
        if (expected.Length != message.Length) {
            record_error("message length", channel, expected.Length, message.Length);
        } else if (!message.SequenceEqual(expected)) {
            _testErrors++;
            Console.Error.WriteLine(
                $"ERROR SPRT message mismatch channel={channel} sequence={expectedSequence}");
        }

        RxMessages[channel]++;
        return 0;
    }

    private static int send_one_message(int channel) {
        SprtState? sprtState = _sprtState;
        if (sprtState is null) {
            record_error("protocol state", channel, 1, 0);
            return -1;
        }

        byte[] message;
        try {
            message = expected_message(channel, TxSequenceNumbers[channel]);
        } catch (ArgumentOutOfRangeException) {
            record_error("message creation", channel, 1, -1);
            return -1;
        }

        int result = sprtState.TransmitMessage(channel, message);
        if (result == 0) {
            bump_sequence_number(ref TxSequenceNumbers[channel]);
            TxMessages[channel]++;
        } else if (result != -1) {
            record_error("sprt_tx return", channel, 0, result);
        }
        return result;
    }

    private static bool all_channels_reached(int[] counters, int target) {
        for (int channel = 0; channel < ChannelCount; channel++) {
            if (counters[channel] < target)
                return false;
        }
        return true;
    }

    private static void paced_operations() {
        if (!_sendMessages)
            return;

        if (!_automatedMode) {
            send_one_message((int)SprtTransmissionChannel.ReliableSequenced);
            _paceNumber++;
            return;
        }

        if (now_us() < _testStarted + AutomatedStartDelayMicroseconds)
            return;

        for (int channel = 0; channel < ChannelCount; channel++) {
            if (TxMessages[channel] < AutomatedMessagesPerChannel)
                send_one_message(channel);
        }
        _paceNumber++;
    }

    private static void timer_callback(object? userData) {
        ulong now = now_us();

        while (now >= _paceTimer) {
            paced_operations();
            _paceTimer += 20_000;
        }

        if (_sprtTimer != 0 && now >= _sprtTimer) {
            _sprtTimer = 0;
            if (userData is SprtState state)
                state.TimerExpired(now);
        }

        if (_automatedMode) {
            if (_testErrors != 0) {
                socket_dgram_harness_request_stop();
            } else if (all_channels_reached(TxMessages, AutomatedMessagesPerChannel)
                       && all_channels_reached(RxMessages, AutomatedMessagesPerChannel)) {
                if (_completionStarted == 0)
                    _completionStarted = now;
                else if (now >= _completionStarted + AutomatedStopGraceMicroseconds)
                    socket_dgram_harness_request_stop();
            } else if (now >= _testStarted + AutomatedTimeoutMicroseconds) {
                _testErrors++;
                Console.Error.WriteLine("ERROR SPRT internal timeout");
                socket_dgram_harness_request_stop();
            }
        }
    }

    private static ulong timer_handler(object? userData, ulong timeout) {
        ulong now = now_us();
        if (timeout == 0) {
            _sprtTimer = 0;
        } else if (timeout != ulong.MaxValue) {
            _sprtTimer = timeout < now ? now : timeout;
        }
        return now;
    }

    private static void status_handler(object? userData, int status) {
        Console.Error.WriteLine($"SPRT status event={status}");
    }

    private static int sprt_tests(bool callingParty) {
        _sendMessages = true;
        _testErrors = 0;
        _completionStarted = 0;
        _paceNumber = 0;
        _sprtTimer = 0;
        Array.Clear(TxMessages);
        Array.Clear(RxMessages);
        for (int channel = 0; channel < ChannelCount; channel++) {
            TxSequenceNumbers[channel] = 0;
            RxSequenceNumbers[channel] = 0x3FFF;
        }

        _datagramState = socket_dgram_harness_init(
            null,
            callingParty ? CallerSocketName : AnswererSocketName,
            callingParty ? AnswererSocketName : CallerSocketName,
            callingParty ? "C" : "A",
            callingParty ? 1 : 0,
            terminal_callback,
            termios_callback,
            hangup_callback,
            terminal_free_space_callback,
            rx_callback,
            tx_callback,
            timer_callback,
            null);
        if (_datagramState is null) {
            Console.Error.WriteLine("ERROR SPRT cannot start socket harness");
            return 2;
        }

        SprtState sprtState;
        try {
            sprtState = new SprtState(
                0,
                120,
                120,
                null,
                tx_packet_handler,
                _datagramState,
                rx_delivery_handler,
                _datagramState,
                timer_handler,
                _datagramState,
                status_handler,
                _datagramState);
            _sprtState = sprtState;
        } catch {
            Console.Error.WriteLine("ERROR SPRT cannot initialise protocol state");
            socket_dgram_harness_free(_datagramState);
            _datagramState = null;
            return 2;
        }

        for (int channel = 0; channel < ChannelCount; channel++)
            MaximumPayloads[channel] = sprtState.GetFarPayloadBytes(channel);

        socket_dgram_harness_set_user_data(_datagramState, sprtState);
        sprtState.Logging.Level = SprtLogLevel.Error;
        sprtState.Logging.Handler = (_, message) =>
            Console.Error.WriteLine("{0}: {1}", callingParty ? "SPRT-C" : "SPRT-A", message);

        _testStarted = now_us();
        socket_dgram_harness_timer = _paceTimer = _testStarted + 20_000;
        int runResult = socket_dgram_harness_run(_datagramState);
        if (runResult != 0) {
            _testErrors++;
            Console.Error.WriteLine("ERROR SPRT socket harness result={0}", runResult);
        }

        for (int channel = 0; channel < ChannelCount; channel++) {
            Console.Error.WriteLine(
                "SPRT RESULT role={0} channel={1} tx={2} rx={3} target={4}",
                callingParty ? "caller" : "answerer",
                channel,
                TxMessages[channel],
                RxMessages[channel],
                _automatedMode ? AutomatedMessagesPerChannel : 0);
            if (_automatedMode &&
                (TxMessages[channel] < AutomatedMessagesPerChannel ||
                 RxMessages[channel] < AutomatedMessagesPerChannel))
                _testErrors++;
        }

        sprtState.Dispose();
        _sprtState = null;
        socket_dgram_harness_free(_datagramState);
        _datagramState = null;

        if (_testErrors != 0) {
            Console.Error.WriteLine("ERROR SPRT test failed errors={0}", _testErrors);
            return 2;
        }
        return 0;
    }

    private static void bump_sequence_number(ref ushort sequenceNumber) {
        sequenceNumber = unchecked((ushort)((sequenceNumber + 1) & 0x3FFF));
    }

}
