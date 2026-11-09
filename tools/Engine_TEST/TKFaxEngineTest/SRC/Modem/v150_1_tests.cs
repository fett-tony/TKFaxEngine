/*
 * TKFaxEngine - managed C# port of v150_1_tests.c.
 *
 * Retains the spanDSP V.150.1 message encode/decode vectors, SPRT transport,
 * RTP/SSE packetisation, timer ordering, payload-limit checks, state-machine
 * exercise and two-process datagram harness operation.
 */

#nullable enable
using SprtConstants = TKFaxEngine.SprtConstants;
using SprtTransmissionChannel = TKFaxEngine.SprtTransmissionChannel;
using SprtLogLevel = TKFaxEngine.SprtLogLevel;

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using TKFaxEngine.Modem.V150;
using TKFaxEngineTest.System;
using static TKFaxEngine.Modem.V150.V1501Api;
using static TKFaxEngine.Modem.V150.V1501SseApi;
using static TKFaxEngine.SprtApi;
using static TKFaxEngineTest.System.socket_dgram_harness;

namespace TKFaxEngineTest.Modem.Test;

public static class v150_1_tests_source {
    private const int SsePacketType = 118;
    private const int SprtPacketType = 120;

    private sealed class RtpState {
        public byte Version = 2;
        public byte Padding;
        public byte Extension;
        public byte CsrcCount;
        public byte Marker;
        public byte PayloadType;
        public ushort SequenceNumber;
        public uint Timestamp;
        public uint Ssrc;
    }

    private static readonly RtpState Rtp = new();
    private static readonly int[] MaximumPayloads = new int[SprtConstants.ChannelCount];
    private static readonly Random Random = new(1);

    private static V1501State? _v1501;
    private static socket_dgram_harness_state_t? _datagramState;
    private static int _rtpTimestamp;
    private static int _paceNumber;
    private static bool _callingParty;
    private static ulong _paceTimer;
    private static ulong _v1501Timer;

    public sealed class V1501TestsExitException : Exception {
        public V1501TestsExitException(int exitCode)
            : base($"v150_1_tests exited with code {exitCode}.") => ExitCode = exitCode;

        public int ExitCode { get; }
    }

    public static int main(string[] args) {
        try {
            return tkfx_v150_1_tests_body_main(args);
        } catch (V1501TestsExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_v150_1_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        _callingParty = false;
        for (int i = 0; i < testArguments.Length; i++) {
            switch (testArguments[i]) {
                case "-a":
                    _callingParty = false;
                    break;
                case "-c":
                    _callingParty = true;
                    break;
                default:
                    exit_test(2);
                    break;
            }
        }

        if (v150_1_tests() != 0)
            exit_test(2);

        Console.Error.WriteLine("Tests passed");
        return 0;
    }

    private static void rtp_init(RtpState rtp, uint timestamp, uint ssrc) {
        rtp.Version = 2;
        rtp.Padding = 0;
        rtp.Extension = 0;
        rtp.CsrcCount = 0;
        rtp.Marker = 0;
        rtp.SequenceNumber = unchecked((ushort)Random.Next(0, 65536));
        rtp.Timestamp = timestamp;
        rtp.Ssrc = ssrc;
    }

    private static int rtp_fill(
        RtpState rtp,
        Span<byte> destination,
        int payloadType,
        ReadOnlySpan<byte> signal,
        uint timestampAdvance,
        bool repeat = false) {
        if (destination.Length < 12 + signal.Length)
            return -1;

        uint packetTimestamp = repeat ? rtp.Timestamp : unchecked(rtp.Timestamp + timestampAdvance);
        if (!repeat)
            rtp.Timestamp = packetTimestamp;

        destination[0] = (byte)(((rtp.Version & 0x03) << 6) |
                                ((rtp.Padding & 0x01) << 5) |
                                ((rtp.Extension & 0x01) << 4) |
                                (rtp.CsrcCount & 0x0F));
        destination[1] = (byte)(((rtp.Marker & 0x01) << 7) | (payloadType & 0x7F));
        BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(2, 2), rtp.SequenceNumber);
        BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(4, 4), packetTimestamp);
        BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(8, 4), rtp.Ssrc);
        signal.CopyTo(destination[12..]);
        rtp.SequenceNumber++;
        return 12 + signal.Length;
    }

    private static int rtp_extract(RtpState rtp, Span<byte> signal, ReadOnlySpan<byte> packet) {
        if (packet.Length < 12 || signal.Length < packet.Length - 12)
            return -1;

        rtp.Version = (byte)((packet[0] >> 6) & 0x03);
        rtp.Padding = (byte)((packet[0] >> 5) & 0x01);
        rtp.Extension = (byte)((packet[0] >> 4) & 0x01);
        rtp.CsrcCount = (byte)(packet[0] & 0x0F);
        rtp.Marker = (byte)((packet[1] >> 7) & 0x01);
        rtp.PayloadType = (byte)(packet[1] & 0x7F);
        rtp.SequenceNumber = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(2, 2));
        rtp.Timestamp = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(4, 4));
        rtp.Ssrc = BinaryPrimitives.ReadUInt32BigEndian(packet.Slice(8, 4));
        packet[12..].CopyTo(signal);
        return packet.Length - 12;
    }

    private static void terminal_callback(object? userData, ReadOnlySpan<byte> message) {
        _ = userData;
        Console.Error.WriteLine("terminal callback {0}", message.Length);
        for (int i = 0; i < message.Length; i++)
            Console.Error.Write("0x{0:x} ", message[i]);
        Console.Error.WriteLine();
    }

    private static int termios_callback(object? userData, termios termios) {
        Console.Error.WriteLine("termios callback");
        return 0;
    }

    private static void hangup_callback(object? userData, int status) {
    }

    private static int terminal_free_space_callback(object? userData) => 42;

    private static void dgram_rx_callback(object? userData, ReadOnlySpan<byte> packet) {
        if (userData is not V1501State state)
            return;

        ReadOnlySpan<byte> received = packet;
        if (received.Length <= 1)
            return;

        int payloadType = received[1] & 0x7F;
        Console.Error.WriteLine("Packet type {0}", payloadType);
        if ((received[0] & 0xC0) == 0x80) {
            Console.Error.WriteLine("Looks RTPish");
            var rtp = new RtpState();
            byte[] signal = new byte[Math.Max(160, received.Length)];
            int signalLength = rtp_extract(rtp, signal, received);
            if (signalLength < 0)
                return;

            if (rtp.PayloadType == SsePacketType) {
                byte[] exact = signal.AsSpan(0, signalLength).ToArray();
                v150_1_rx_sse_packet(state, rtp.SequenceNumber, rtp.Timestamp, exact, signalLength);
            }
            _rtpTimestamp = unchecked((int)(rtp.Timestamp + 160));
        } else {
            Console.Error.WriteLine("Looks SPRTish");
            if (payloadType == SprtPacketType)
                sprt_rx_packet(state.Sprt, received);
        }

    }

    private static int tx_callback(object? userData, Span<byte> buffer, int maximumLength) => 0;

    private static int v150_1_status_report_handler(object? userData, V1501Status report) {
        Console.Error.WriteLine("V.150.1 status report received");
        switch (report.Reason) {
            case V1501StatusReason.MediaStateChanged:
                Console.Error.WriteLine(
                    "Media state changed to {0} {1}",
                    v150_1_media_state_to_str((int)report.LocalMediaState),
                    v150_1_media_state_to_str((int)report.RemoteMediaState));
                break;

            case V1501StatusReason.ConnectionStateChanged:
                Console.Error.WriteLine("Connection state changed to {0}", v150_1_state_to_str((int)report.ConnectionState));
                if (report.ConnectionState == V1501ConnectionState.Idle) {
                    Console.Error.WriteLine("    Cleardown reason {0}", v150_1_cleardown_reason_to_str((int)report.CleardownReason));
                } else if (report.ConnectionState == V1501ConnectionState.Initialized) {
                    if (_callingParty)
                        v150_1_tx_init(require_state());
                    else
                        v150_1_tx_jm_info(require_state());
                }
                break;

            case V1501StatusReason.ConnectionStatePhysicallyUp:
                Console.Error.WriteLine("Physup received");
                v150_1_tx_mr_event(require_state(), (int)V1501MrEventId.PhysicallyUp);
                break;

            case V1501StatusReason.ConnectionStateConnected:
                Console.Error.WriteLine("Connected received");
                v150_1_tx_connect(require_state());
                break;

            case V1501StatusReason.DataFormatChanged:
                Console.Error.WriteLine("Data format changed");
                Console.Error.WriteLine(
                    "    Format is {0} data bits, {1} stop bits, {2} parity",
                    report.Bits,
                    report.StopBits,
                    v150_1_parity_to_str((int)report.Parity));
                break;

            case V1501StatusReason.BreakReceived:
                Console.Error.WriteLine("Break received");
                Console.Error.WriteLine("    Break source {0}", v150_1_break_source_to_str((int)report.BreakSource));
                Console.Error.WriteLine("    Break type {0}", v150_1_break_type_to_str((int)report.BreakType));
                Console.Error.WriteLine("    Break duration {0} ms", report.BreakDurationMilliseconds);
                break;

            case V1501StatusReason.RateRetrainReceived:
                Console.Error.WriteLine("Retrain received");
                break;

            case V1501StatusReason.RateRenegotiationReceived:
                Console.Error.WriteLine("Rate renegotiation received");
                break;

            case V1501StatusReason.BusyChanged:
                Console.Error.WriteLine("Busy status change received");
                Console.Error.WriteLine("Near side now {0}busy", report.LocalBusy ? string.Empty : "not ");
                Console.Error.WriteLine("Far side now {0}busy", report.FarBusy ? string.Empty : "not ");
                break;

            default:
                Console.Error.WriteLine("Unknown status report reason {0} received", (int)report.Reason);
                break;
        }

        return 0;
    }

    private static int v150_1_data_stream_handler(object? userData, ReadOnlySpan<byte> message, int fill) {
        if (fill > 0)
            Console.Error.WriteLine("{0} missing characters", fill);
        Console.Error.Write(">>>");
        foreach (byte value in message)
            Console.Error.Write(" {0:x2}", value);
        Console.Error.WriteLine("<<<");
        return 0;
    }

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern unsafe int sendto(
        nuint s,
        byte* buf,
        int len,
        int flags,
        ref sockaddr_in to,
        int tolen);

    private static int sprt_tx_packet_handler(object? userData, ReadOnlySpan<byte> packet) {
        if (userData is not socket_dgram_harness_state_t state)
            return -1;

        if (Random.Next(20) == 0) {
            Console.Error.WriteLine("Block");
            return 0;
        }

        Console.Error.WriteLine("Pass");
        int sent;
        unsafe {
            fixed (byte* raw = packet)
                sent = sendto(state.net_fd, raw, packet.Length, 0, ref state.far_addr, state.far_addr_len);
        }
        if (sent != packet.Length)
            Console.Error.WriteLine("Net write = {0}", sent);
        return sent < 0 ? -1 : 0;
    }

    private static int sse_tx_packet_handler(object? userData, bool repeat, ReadOnlySpan<byte> packet) {
        if (userData is not socket_dgram_harness_state_t state)
            return -1;

        Console.Error.Write("Tx message");
        foreach (byte value in packet)
            Console.Error.Write(" {0:x2}", value);
        Console.Error.WriteLine();

        byte[] datagram = new byte[256];
        int length = rtp_fill(Rtp, datagram, SsePacketType, packet, repeat ? 0U : 160U, repeat);
        if (length < 0)
            return -1;

        int sent;
        unsafe {
            fixed (byte* raw = datagram)
                sent = sendto(state.net_fd, raw, length, 0, ref state.far_addr, state.far_addr_len);
        }
        if (sent != length)
            Console.Error.WriteLine("Net write = {0}", sent);
        return sent < 0 ? -1 : 0;
    }

    private static void paced_operations() {
        Console.Error.WriteLine("Pace at {0}", now_us());
        _paceNumber++;
        if (_paceNumber == 50 && !_callingParty) {
            if (v150_1_tx_sse_packet(
                    require_state(),
                    (int)V1501MediaState.ModemRelay,
                    (int)V1501SseMoipRic.V32BisAa,
                    0) != 0) {
                Console.Error.WriteLine("ERROR: Failed to send message");
            }
        }
    }

    private static void timer_callback(object? userData) {
        ulong now = now_us();
        if (_paceTimer != 0 && now >= _paceTimer) {
            Console.Error.WriteLine("Pace timer expired at {0}", now);
            paced_operations();
            _paceTimer += 20_000;
        }

        if (_v1501Timer != 0 && now >= _v1501Timer) {
            Console.Error.WriteLine("V150.1 timer expired at {0}", now);
            _v1501Timer = 0;
            v150_1_timer_expired(require_state(), now);
        }

        ulong candidate = ulong.MaxValue;
        if (_v1501Timer != 0 && _v1501Timer < candidate)
            candidate = _v1501Timer;
        if (_paceTimer != 0 && _paceTimer < candidate)
            candidate = _paceTimer;
        socket_dgram_harness_timer = candidate;
    }

    private static ulong v150_1_timer_handler(object? userData, ulong timeout) {
        ulong now = now_us();
        if (timeout == 0) {
            Console.Error.WriteLine("V.150.1 timer stopped at {0}", now);
            _v1501Timer = 0;
            socket_dgram_harness_timer = _paceTimer;
        } else if (timeout == ulong.MaxValue) {
            Console.Error.WriteLine("V.150.1 get the time {0}", now);
        } else {
            Console.Error.WriteLine("V.150.1 timer set to {0} at {1}", timeout, now);
            _v1501Timer = timeout < now ? now : timeout;
            socket_dgram_harness_timer = _paceTimer == 0 || _paceTimer > _v1501Timer
                ? _v1501Timer
                : _paceTimer;
        }
        return now;
    }

    private static int message_decode_tests() {
        V1501State state = require_state();
        Console.WriteLine("INIT test");
        v150_1_test_rx_sprt_msg(
            state,
            (int)SprtTransmissionChannel.ExpeditedReliableSequenced,
            0,
            new byte[] { 0x01, 0x40, 0x80 });

        Console.WriteLine("MR-EVENT test");
        v150_1_test_rx_sprt_msg(
            state,
            (int)SprtTransmissionChannel.ExpeditedReliableSequenced,
            1,
            new byte[] { 0x08, 0x03, 0x00, 0x18, 0x25, 0x80, 0x25, 0x80, 0x00, 0x00 });

        Console.WriteLine("CONNECT test");
        v150_1_test_rx_sprt_msg(
            state,
            (int)SprtTransmissionChannel.ExpeditedReliableSequenced,
            2,
            new byte[] { 0x05, 0x18, 0x00, 0x25, 0x80, 0x25, 0x80, 0x02, 0x00 });

        Console.WriteLine("I_OCTET-CS sending \"TEST\" test");
        byte[] test = { 0x16, 0x00, 0x00, 0x54, 0x45, 0x53, 0x54 };
        v150_1_test_rx_sprt_msg(state, (int)SprtTransmissionChannel.ReliableSequenced, 0, test);
        v150_1_test_rx_sprt_msg(state, (int)SprtTransmissionChannel.UnreliableSequenced, 0, test);

        Console.WriteLine("CLEARDOWN test");
        v150_1_test_rx_sprt_msg(
            state,
            (int)SprtTransmissionChannel.ExpeditedReliableSequenced,
            3,
            new byte[] { 0x09, 0x05, 0x01, 0x02 });
        return 0;
    }

    private static int message_encode_tests() {
        V1501State state = require_state();
        byte[] buffer = new byte[256];

        v150_1_tx_null(state);

        Console.WriteLine("INIT test");
        v150_1_test_rx_sprt_msg(
            state,
            (int)SprtTransmissionChannel.ExpeditedReliableSequenced,
            0,
            new byte[] { 0x01, 0x40, 0x80 });
        v150_1_tx_init(state);
        v150_1_tx_jm_info(state);

        Console.WriteLine("MR-EVENT test");
        v150_1_test_rx_sprt_msg(
            state,
            (int)SprtTransmissionChannel.ExpeditedReliableSequenced,
            1,
            new byte[] { 0x08, 0x03, 0x00, 0x18, 0x25, 0x80, 0x25, 0x80, 0x00, 0x00 });
        v150_1_tx_mr_event(state, (int)V1501MrEventId.PhysicallyUp);

        Console.WriteLine("CONNECT test");
        v150_1_test_rx_sprt_msg(
            state,
            (int)SprtTransmissionChannel.ExpeditedReliableSequenced,
            2,
            new byte[] { 0x05, 0x18, 0x00, 0x25, 0x80, 0x25, 0x80, 0x02, 0x00 });
        v150_1_tx_connect(state);

        if (v150_1_tx_info_stream(state, Encoding.ASCII.GetBytes("Test side 0")) < 0)
            Console.Error.WriteLine("Failed to send good message");

        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = unchecked((byte)i);
        if (v150_1_tx_info_stream(state, buffer.AsSpan(0, 129)) < 0)
            Console.Error.WriteLine("Failed to send good message");

        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = unchecked((byte)(255 - i));
        if (v150_1_tx_info_stream(state, buffer.AsSpan(0, 129)) < 0)
            Console.Error.WriteLine("Failed to send good message");
        if (v150_1_tx_info_stream(state, buffer.AsSpan(0, 198)) >= 0)
            Console.Error.WriteLine("Able to send bad length message");

        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = unchecked((byte)i);
        if (v150_1_tx_info_stream(state, buffer.AsSpan(0, 129)) < 0)
            Console.Error.WriteLine("Failed to send byte stream");

        v150_1_tx_break(
            state,
            (int)V1501BreakSource.V42Lapm,
            (int)V1501BreakType.DestructiveExpedited,
            1230);
        v150_1_tx_break_ack(state);
        v150_1_tx_cleardown(state, (int)V1501CleardownReason.LinkLayerDisconnect);
        v150_1_tx_cleardown(state, (int)V1501CleardownReason.LinkLayerDisconnect);
        return 0;
    }

    private static int state_machine_tests() {
        V1501State state = require_state();
        v150_1_state_machine(state, (int)V1501Signal.Cm, ReadOnlySpan<byte>.Empty);
        v150_1_state_machine(state, (int)V1501Signal.ModemRelay, ReadOnlySpan<byte>.Empty);
        return 0;
    }

    private static int dynamic_tests() {
        _paceTimer = now_us() + 20_000;
        socket_dgram_harness_timer = _paceTimer;
        return socket_dgram_harness_run(require_harness());
    }

    private static int v150_1_tests() {
        string localName = _callingParty ? "/tmp/v150_1_socket_a" : "/tmp/v150_1_socket_b";
        string remoteName = _callingParty ? "/tmp/v150_1_socket_b" : "/tmp/v150_1_socket_a";

        _datagramState = socket_dgram_harness_init(
            null,
            localName,
            remoteName,
            _callingParty ? "C" : "A",
            _callingParty ? 1 : 0,
            terminal_callback,
            termios_callback,
            hangup_callback,
            terminal_free_space_callback,
            dgram_rx_callback,
            tx_callback,
            timer_callback,
            null);

        if (_datagramState is null) {
            Console.Error.WriteLine("    Cannot start the socket harness");
            exit_test(2);
        }

        _v1501 = v150_1_init(
            null,
            sprt_tx_packet_handler,
            _datagramState,
            SprtPacketType,
            SprtPacketType,
            sse_tx_packet_handler,
            _datagramState,
            v150_1_timer_handler,
            null,
            v150_1_data_stream_handler,
            null,
            v150_1_status_report_handler,
            null,
            null,
            null);

        if (_v1501 is null) {
            Console.Error.WriteLine("    Cannot start V.150.1");
            exit_test(2);
        }

        _v1501.Logging.Level = V1501LogLevel.Flow;
        _v1501.Logging.Protocol = _callingParty ? "C" : "A";
        _v1501.Logging.Handler = (_, protocol, text) => Console.Error.WriteLine("{0}: {1}", protocol, text);
        _v1501.Sprt.Logging.Level = SprtLogLevel.Flow;
        _v1501.Sprt.Logging.Handler = (_, text) => Console.Error.WriteLine("{0}: {1}", _callingParty ? "C" : "A", text);

        v150_1_set_info_stream_tx_mode(
            _v1501,
            (int)SprtTransmissionChannel.ReliableSequenced,
            (int)V1501MessageId.IOctetCharacterSequence);
        v150_1_set_info_stream_msg_priorities(
            _v1501,
            new[]
            {
                (int)V1501MessageId.IOctetCharacterSequence,
                (int)V1501MessageId.IOctet,
                -1
            });

        v150_1_set_modulation(_v1501, (int)V1501SelectedModulation.V34);
        v150_1_set_compression_direction(_v1501, (int)V1501CompressionDirection.NeitherWay);
        v150_1_set_compression(_v1501, (int)V1501Compression.None);
        v150_1_set_compression_parameters(_v1501, 512, 512, 6, 6, 0, 0);
        v150_1_set_error_correction(_v1501, (int)V1501ErrorCorrection.None);
        v150_1_set_tx_symbol_rate(_v1501, true, (int)V1501SymbolRate.Baud3429);
        v150_1_set_rx_symbol_rate(_v1501, true, (int)V1501SymbolRate.Baud3429);
        v150_1_set_tx_data_signalling_rate(_v1501, 33_600);
        v150_1_set_rx_data_signalling_rate(_v1501, 33_600);

        v150_1_set_sse_reliability_method(
            _v1501,
            (int)V1501SseReliabilityMethod.Repetition,
            3,
            100_000,
            0);
        v150_1_set_near_cdscselect(_v1501, V1501CallDiscriminationSelection.AudioRfc4733);
        v150_1_set_far_cdscselect(_v1501, V1501CallDiscriminationSelection.Mixed);

        for (int channel = 0; channel < SprtConstants.ChannelCount; channel++) {
            MaximumPayloads[channel] = sprt_get_far_tc_payload_bytes(_v1501.Sprt, channel);
            Console.Error.WriteLine("Max payload {0} is {1}", channel, MaximumPayloads[channel]);
        }

        socket_dgram_harness_set_user_data(_datagramState, _v1501);

        if (v150_1_set_local_tc_payload_bytes(_v1501, (int)SprtTransmissionChannel.ReliableSequenced, 256) < 0)
            Console.Error.WriteLine("Failed to set new max payload bytes");
        if (v150_1_set_local_tc_payload_bytes(_v1501, (int)SprtTransmissionChannel.ReliableSequenced, 257) >= 0)
            Console.Error.WriteLine("Able to set bad new max payload bytes");
        if (v150_1_set_local_tc_payload_bytes(_v1501, (int)SprtTransmissionChannel.ReliableSequenced, 132) < 0)
            Console.Error.WriteLine("Failed to set new max payload bytes");
        if (v150_1_set_local_tc_payload_bytes(_v1501, (int)SprtTransmissionChannel.ReliableSequenced, 131) >= 0)
            Console.Error.WriteLine("Able to set bad new max payload bytes");

        Console.Error.WriteLine(
            "Max payload bytes is {0}",
            v150_1_get_local_tc_payload_bytes(_v1501, (int)SprtTransmissionChannel.ReliableSequenced));

        rtp_init(Rtp, 0, 0x12345678);
        message_encode_tests();

        v150_1_free(_v1501);
        _v1501 = null;
        socket_dgram_harness_free(_datagramState);
        _datagramState = null;
        return 0;
    }

    private static V1501State require_state() =>
        _v1501 ?? throw new InvalidOperationException("V.150.1 state is not initialized.");

    private static socket_dgram_harness_state_t require_harness() =>
        _datagramState ?? throw new InvalidOperationException("Datagram harness is not initialized.");

    private static void exit_test(int code) => throw new V1501TestsExitException(code);
}
