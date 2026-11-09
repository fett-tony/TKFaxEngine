/*
 * TKFaxEngine - managed C# port of v150_1_sse_tests.c.
 *
 * Deterministic two-peer V.150.1 SSE validation retaining RTP framing,
 * three-transmission repetition reliability, microsecond timer scheduling,
 * caller/answerer RIC roles, callback guards, automated stop criteria and
 * before/after diagnostics.
 */

#nullable enable

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using TKFaxEngine.Modem.V150;
using TKFaxEngineTest.System;
using static TKFaxEngine.Modem.V150.V1501Api;
using static TKFaxEngine.Modem.V150.V1501SseApi;
using static TKFaxEngineTest.System.socket_dgram_harness;

namespace TKFaxEngineTest.Modem.Test;

public static class v150_1_sse_tests_source {
    private const int PacketType = 118;
    private const int AutomatedMessages = 8;
    private const ulong AutomatedStartDelayUs = 500_000;
    private const ulong AutomatedSendIntervalUs = 120_000;
    private const ulong AutomatedTimeoutUs = 15_000_000;
    private const ulong AutomatedStopGraceUs = 500_000;
    private const string BuildRevision = "20260712_1040";

    private sealed class RtpState {
        public byte Version = 2;
        public byte Padding;
        public byte Extension;
        public byte CsrcCount;
        public byte Marker;
        public byte PayloadType = PacketType;
        public ushort SequenceNumber;
        public uint Timestamp;
        public uint Ssrc;
    }

    private sealed record DiagnosticSnapshot(
        string Role,
        bool Automated,
        bool SendMessages,
        int Errors,
        bool ContextPresent,
        bool SocketPresent,
        int RxDataCalls,
        int RxDataBytes,
        int RxDataFill,
        int RxStatusCalls,
        int RxStatusMediaChanges,
        int UnexpectedSprtTransmits,
        int TxUnique,
        int TxRepeats,
        int ScheduledAa,
        int TxAaUnique,
        int TxAaRepeats,
        int TxPUnique,
        int TxPRepeats,
        int UnexpectedRic,
        int PaceNumber,
        ushort RtpSequence,
        uint RtpTimestamp,
        uint RtpSsrc,
        int LocalMediaState,
        int RemoteAcknowledgement,
        int JointConnectionState,
        int ReliabilityMethod,
        int PendingRepetitions,
        ulong SseScheduledTimer,
        bool HaveRxPacket,
        int RxUnique,
        int RxRepeats,
        int RxAaUnique,
        int RxAaRepeats,
        int RxPUnique,
        int RxPRepeats,
        ushort LastRxSequence,
        uint LastRxTimestamp,
        int RemoteMediaState,
        uint EngineLastRxTimestamp,
        ushort EngineLastRxSequence,
        ulong TestStarted,
        ulong NextSendTime,
        ulong CompletionStarted,
        ulong PaceTimer,
        ulong ApplicationTimer,
        ulong EngineLatestTimer,
        ulong EngineSseTimer,
        ulong EngineSprtTimer,
        int SocketStarted,
        int SocketPtyClosed,
        int SocketCloseCount,
        bool NetworkSocketValid,
        bool PseudoTerminalValid);

    private static readonly RtpState TxSseRtp = new();

    private static socket_dgram_harness_state_t? _datagramState;
    private static V1501State? _v1501State;
    private static DiagnosticSnapshot? _before;

    private static int _paceNumber;
    private static ulong _paceTimer;
    private static ulong _applicationTimer;
    private static bool _sendMessages;
    private static int _sequence;
    private static int _timestamp;

    private static bool _automatedMode;
    private static int _testErrors;
    private static int _txUnique;
    private static int _txRepeats;
    private static int _rxUnique;
    private static int _rxRepeats;
    private static bool _haveRxPacket;
    private static ushort _lastRxSequence;
    private static uint _lastRxTimestamp;
    private static ulong _testStarted;
    private static ulong _nextSendTime;
    private static ulong _completionStarted;
    private static int _rxDataCallbackCalls;
    private static int _rxDataCallbackBytes;
    private static int _rxDataCallbackFill;
    private static int _rxStatusCallbackCalls;
    private static int _rxStatusMediaChangeCalls;
    private static int _unexpectedSprtTxCalls;
    private static bool _activeCallingParty;
    private static int _scheduledAaMessages;
    private static int _txAaUnique;
    private static int _txAaRepeats;
    private static int _txPUnique;
    private static int _txPRepeats;
    private static int _rxAaUnique;
    private static int _rxAaRepeats;
    private static int _rxPUnique;
    private static int _rxPRepeats;
    private static int _unexpectedSseRic;

    public sealed class V1501SseTestsExitException : Exception {
        public V1501SseTestsExitException(int exitCode)
            : base($"v150_1_sse_tests exited with code {exitCode}.") => ExitCode = exitCode;

        public int ExitCode { get; }
    }

    public static int main(string[] args) {
        try {
            return tkfx_v150_1_sse_tests_body_main(args);
        } catch (V1501SseTestsExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_v150_1_sse_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        bool callingParty = false;
        _automatedMode = false;
        for (int i = 0; i < testArguments.Length; i++) {
            switch (testArguments[i]) {
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

        if (v150_1_sse_tests(callingParty) != 0)
            return 2;

        Console.WriteLine("Tests passed");
        return 0;
    }

    private static DiagnosticSnapshot v150_sse_capture_snapshot(
        bool callingParty,
        V1501State? state,
        socket_dgram_harness_state_t? socketState) {
        return new DiagnosticSnapshot(
            callingParty ? "caller" : "answerer",
            _automatedMode,
            _sendMessages,
            _testErrors,
            state is not null,
            socketState is not null,
            _rxDataCallbackCalls,
            _rxDataCallbackBytes,
            _rxDataCallbackFill,
            _rxStatusCallbackCalls,
            _rxStatusMediaChangeCalls,
            _unexpectedSprtTxCalls,
            _txUnique,
            _txRepeats,
            _scheduledAaMessages,
            _txAaUnique,
            _txAaRepeats,
            _txPUnique,
            _txPRepeats,
            _unexpectedSseRic,
            _paceNumber,
            TxSseRtp.SequenceNumber,
            TxSseRtp.Timestamp,
            TxSseRtp.Ssrc,
            state is null ? -1 : (int)state.LocalMediaState,
            state is null ? -1 : (int)state.RemoteAcknowledgement,
            state is null ? -1 : (int)state.JointConnectionState,
            state is null ? -1 : (int)state.Sse.SelectedReliabilityMethod,
            state?.Sse.PendingRepetitions ?? -1,
            state?.Sse.ScheduledTimer ?? 0,
            _haveRxPacket,
            _rxUnique,
            _rxRepeats,
            _rxAaUnique,
            _rxAaRepeats,
            _rxPUnique,
            _rxPRepeats,
            _lastRxSequence,
            _lastRxTimestamp,
            state is null ? -1 : (int)state.RemoteMediaState,
            state?.Sse.LastReceivedTimestamp ?? 0,
            state?.Sse.LastReceivedSequenceNumber ?? 0,
            _testStarted,
            _nextSendTime,
            _completionStarted,
            _paceTimer,
            _applicationTimer,
            state?.LatestTimer ?? 0,
            state?.SseTimer ?? 0,
            state?.SprtTimer ?? 0,
            socketState is null ? -1 : unchecked((int)socketState.started),
            socketState is null ? -1 : unchecked((int)socketState.pty_closed),
            socketState is null ? -1 : unchecked((int)socketState.close_count),
            socketState?.net_fd is not null,
            socketState?.pty is not null);
    }

    private static void v150_sse_print_snapshot(string label, DiagnosticSnapshot snapshot) {
        Console.Error.WriteLine(
            "STATE V150_SSE {0} CONTROL role={1} automated={2} send_messages={3} errors={4} context_present={5} socket_present={6} callbacks_sprt_tx=1 callbacks_sse_tx=1 callbacks_timer=1 callbacks_rx_data=1 callbacks_rx_status=1 callbacks_spe=0 rx_data_calls={7} rx_data_bytes={8} rx_data_fill={9} rx_status_calls={10} rx_status_media_changes={11} unexpected_sprt_tx_calls={12}",
            label,
            snapshot.Role,
            bool_int(snapshot.Automated),
            bool_int(snapshot.SendMessages),
            snapshot.Errors,
            bool_int(snapshot.ContextPresent),
            bool_int(snapshot.SocketPresent),
            snapshot.RxDataCalls,
            snapshot.RxDataBytes,
            snapshot.RxDataFill,
            snapshot.RxStatusCalls,
            snapshot.RxStatusMediaChanges,
            snapshot.UnexpectedSprtTransmits);

        Console.Error.WriteLine(
            "STATE V150_SSE {0} TX role={1} tx_unique={2} tx_repeats={3} target_unique={4} target_repeats={5} scheduled_aa={6} tx_aa_unique={7} tx_aa_repeats={8} tx_p_unique={9} tx_p_repeats={10} unexpected_ric={11} pace_no={12} rtp_seq={13} rtp_timestamp={14} rtp_ssrc=0x{15:X8} local_media={16} remote_ack={17} joint_state={18} reliability={19} pending_repetitions={20} sse_timer={21}",
            label,
            snapshot.Role,
            snapshot.TxUnique,
            snapshot.TxRepeats,
            AutomatedMessages,
            AutomatedMessages * 2,
            snapshot.ScheduledAa,
            snapshot.TxAaUnique,
            snapshot.TxAaRepeats,
            snapshot.TxPUnique,
            snapshot.TxPRepeats,
            snapshot.UnexpectedRic,
            snapshot.PaceNumber,
            snapshot.RtpSequence,
            snapshot.RtpTimestamp,
            snapshot.RtpSsrc,
            snapshot.LocalMediaState,
            snapshot.RemoteAcknowledgement,
            snapshot.JointConnectionState,
            snapshot.ReliabilityMethod,
            snapshot.PendingRepetitions,
            snapshot.SseScheduledTimer);

        Console.Error.WriteLine(
            "STATE V150_SSE {0} RX role={1} have_packet={2} rx_unique={3} rx_repeats={4} target_unique={5} target_repeats={6} rx_aa_unique={7} rx_aa_repeats={8} rx_p_unique={9} rx_p_repeats={10} last_seq={11} last_timestamp={12} remote_media={13} engine_last_timestamp={14} engine_last_sequence={15}",
            label,
            snapshot.Role,
            bool_int(snapshot.HaveRxPacket),
            snapshot.RxUnique,
            snapshot.RxRepeats,
            AutomatedMessages,
            AutomatedMessages * 2,
            snapshot.RxAaUnique,
            snapshot.RxAaRepeats,
            snapshot.RxPUnique,
            snapshot.RxPRepeats,
            snapshot.LastRxSequence,
            snapshot.LastRxTimestamp,
            snapshot.RemoteMediaState,
            snapshot.EngineLastRxTimestamp,
            snapshot.EngineLastRxSequence);

        Console.Error.WriteLine(
            "STATE V150_SSE {0} TIMERS role={1} test_started={2} next_send={3} completion_started={4} pace_timer={5} app_timer={6} engine_latest={7} engine_sse={8} engine_sprt={9}",
            label,
            snapshot.Role,
            snapshot.TestStarted,
            snapshot.NextSendTime,
            snapshot.CompletionStarted,
            snapshot.PaceTimer,
            snapshot.ApplicationTimer,
            snapshot.EngineLatestTimer,
            snapshot.EngineSseTimer,
            snapshot.EngineSprtTimer);

        Console.Error.WriteLine(
            "STATE V150_SSE {0} IO role={1} socket_started={2} socket_pty_closed={3} close_count={4} net_fd_valid={5} pty_valid={6}",
            label,
            snapshot.Role,
            snapshot.SocketStarted,
            snapshot.SocketPtyClosed,
            snapshot.SocketCloseCount,
            bool_int(snapshot.NetworkSocketValid),
            bool_int(snapshot.PseudoTerminalValid));
    }

    private static void record_error(string text, int expected, int actual) {
        _testErrors++;
        Console.Error.WriteLine("ERROR V150_SSE {0} expected={1} actual={2}", text, expected, actual);
    }

    private static int sse_rx_data_handler(object? _, ReadOnlySpan<byte> message, int fill) {
        _rxDataCallbackCalls++;
        if (message.Length > 0)
            _rxDataCallbackBytes += message.Length;
        if (fill > 0)
            _rxDataCallbackFill += fill;

        if (fill < 0) {
            record_error("invalid SPRT RX callback arguments", 0, fill);
            return -1;
        }
        if (message.Length != 0 || fill != 0) {
            record_error("unexpected SPRT data in SSE-only test", 0, message.Length + fill);
            return -1;
        }
        return 0;
    }

    private static int sse_rx_status_report_handler(object? _, V1501Status report) {
        _rxStatusCallbackCalls++;
        if (report.Reason == V1501StatusReason.MediaStateChanged)
            _rxStatusMediaChangeCalls++;
        return 0;
    }

    private static int guarded_sprt_tx_packet_handler(object? _, ReadOnlySpan<byte> packet) {
        _unexpectedSprtTxCalls++;
        Console.Error.WriteLine("ERROR V150_SSE unexpected SPRT TX packet len={0}", packet.Length);
        _testErrors++;
        return -1;
    }

    private static int rtp_fill(
        RtpState rtp,
        Span<byte> destination,
        int payloadType,
        ReadOnlySpan<byte> signal,
        bool repeat,
        uint advance) {
        if (destination.Length < 12 + signal.Length)
            return -1;

        uint packetTimestamp = repeat && rtp.Timestamp >= advance
            ? rtp.Timestamp - advance
            : rtp.Timestamp;

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
        if (!repeat)
            rtp.Timestamp += advance;
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

    private static void terminal_callback(object? _, ReadOnlySpan<byte> _1) { }
    private static int termios_callback(object? _, termios _1) => 0;
    private static void hangup_callback(object? _, int _1) { }
    private static int terminal_free_space_callback(object? _) => 42;

    private static void rx_callback(object? userData, ReadOnlySpan<byte> packet) {
        if (userData is not V1501State state)
            return;

        var rtp = new RtpState();
        byte[] signal = new byte[256];
        int signalLength = rtp_extract(rtp, signal, packet);
        if (signalLength < 4) {
            record_error("RTP/SSE packet length", 4, signalLength);
            return;
        }
        if (rtp.Version != 2)
            record_error("RTP version", 2, rtp.Version);
        if (rtp.PayloadType != PacketType)
            record_error("RTP payload type", PacketType, rtp.PayloadType);

        bool repeat = false;
        int ric = signal[1];
        if (_haveRxPacket) {
            ushort expectedSequence = unchecked((ushort)(_lastRxSequence + 1));
            if (rtp.SequenceNumber != expectedSequence)
                record_error("RTP sequence", expectedSequence, rtp.SequenceNumber);
            repeat = rtp.Timestamp == _lastRxTimestamp;
        } else {
            _haveRxPacket = true;
        }

        if (repeat)
            _rxRepeats++;
        else
            _rxUnique++;

        if (ric == (int)V1501SseMoipRic.V32BisAa) {
            if (repeat)
                _rxAaRepeats++;
            else
                _rxAaUnique++;
            if (_activeCallingParty) {
                _unexpectedSseRic++;
                record_error("caller received unexpected AA", (int)V1501SseMoipRic.PStateTransition, ric);
            }
        } else if (ric == (int)V1501SseMoipRic.PStateTransition) {
            if (repeat)
                _rxPRepeats++;
            else
                _rxPUnique++;
            if (!_activeCallingParty) {
                _unexpectedSseRic++;
                record_error("answerer received unexpected P-state", (int)V1501SseMoipRic.V32BisAa, ric);
            }
        } else {
            _unexpectedSseRic++;
            record_error(
                "unexpected SSE RIC",
                _activeCallingParty ? (int)V1501SseMoipRic.PStateTransition : (int)V1501SseMoipRic.V32BisAa,
                ric);
        }

        _lastRxSequence = rtp.SequenceNumber;
        _lastRxTimestamp = rtp.Timestamp;

        int eventCode = (signal[0] >> 2) & 0x3F;
        if (eventCode != (int)V1501MediaState.ModemRelay)
            record_error("SSE media state", (int)V1501MediaState.ModemRelay, eventCode);

        byte[] exactSignal = signal.AsSpan(0, signalLength).ToArray();
        int result = v150_1_rx_sse_packet(state, rtp.SequenceNumber, rtp.Timestamp, exactSignal, signalLength);
        if (result != 0)
            record_error("v150_1_rx_sse_packet", 0, result);
        else if (state.RemoteMediaState != V1501MediaState.ModemRelay)
            record_error("engine remote media state", (int)V1501MediaState.ModemRelay, (int)state.RemoteMediaState);

        _sequence = rtp.SequenceNumber + 1;
        _timestamp = unchecked((int)(rtp.Timestamp + 160));
    }

    private static int tx_callback(object? userData, Span<byte> _, int _1) {
        if (userData is not V1501State state)
            return -1;
        return v150_1_tx_sse_packet(
            state,
            (int)V1501MediaState.ModemRelay,
            (int)V1501SseMoipRic.V32BisAa,
            0);
    }

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern unsafe int sendto(
        nuint s,
        byte* buf,
        int len,
        int flags,
        ref sockaddr_in to,
        int tolen);

    private static int tx_packet_handler(object? userData, bool repeat, ReadOnlySpan<byte> packet) {
        if (userData is not socket_dgram_harness_state_t state)
            return -1;

        byte[] datagram = new byte[256];
        int datagramLength = rtp_fill(TxSseRtp, datagram, PacketType, packet, repeat, 160);
        if (datagramLength < 0) {
            record_error("RTP packet creation", 0, datagramLength);
            return -1;
        }

        int sentLength;
        unsafe {
            fixed (byte* raw = datagram)
                sentLength = sendto(state.net_fd, raw, datagramLength, 0, ref state.far_addr, state.far_addr_len);
        }

        if (sentLength != datagramLength) {
            record_error("network write length", datagramLength, sentLength);
            return -1;
        }

        int ric = packet.Length >= 2 ? packet[1] : -1;
        if (repeat)
            _txRepeats++;
        else
            _txUnique++;

        if (ric == (int)V1501SseMoipRic.V32BisAa) {
            if (repeat)
                _txAaRepeats++;
            else
                _txAaUnique++;
            if (!_activeCallingParty) {
                _unexpectedSseRic++;
                record_error("answerer transmitted unexpected AA", (int)V1501SseMoipRic.PStateTransition, ric);
            }
        } else if (ric == (int)V1501SseMoipRic.PStateTransition) {
            if (repeat)
                _txPRepeats++;
            else
                _txPUnique++;
            if (_activeCallingParty) {
                _unexpectedSseRic++;
                record_error("caller transmitted unexpected P-state", (int)V1501SseMoipRic.V32BisAa, ric);
            }
        } else {
            _unexpectedSseRic++;
            record_error(
                "unexpected transmitted SSE RIC",
                _activeCallingParty ? (int)V1501SseMoipRic.V32BisAa : (int)V1501SseMoipRic.PStateTransition,
                ric);
        }

        return 0;
    }

    private static void paced_operations() {
        ulong now = now_us();
        if (!_sendMessages)
            return;

        if (!_automatedMode) {
            if ((_paceNumber & 0x3F) == 0) {
                v150_1_tx_sse_packet(
                    require_state(),
                    (int)V1501MediaState.ModemRelay,
                    (int)V1501SseMoipRic.V32BisAa,
                    0);
            }
            _paceNumber++;
            return;
        }

        if (now < _testStarted + AutomatedStartDelayUs)
            return;

        if (_scheduledAaMessages < AutomatedMessages && now >= _nextSendTime) {
            int result = v150_1_tx_sse_packet(
                require_state(),
                (int)V1501MediaState.ModemRelay,
                (int)V1501SseMoipRic.V32BisAa,
                0);
            if (result != 0)
                record_error("v150_1_tx_sse_packet", 0, result);
            else
                _scheduledAaMessages++;
            _nextSendTime = now + AutomatedSendIntervalUs;
        }
        _paceNumber++;
    }

    private static ulong v150_1_timer_handler(object? _, ulong timeout) {
        ulong now = now_us();
        if (timeout == 0)
            _applicationTimer = 0;
        else if (timeout != ulong.MaxValue)
            _applicationTimer = timeout < now ? now : timeout;

        socket_dgram_harness_timer = _applicationTimer != 0 && _applicationTimer < _paceTimer
            ? _applicationTimer
            : _paceTimer;
        return now;
    }

    private static void timer_callback(object? _) {
        ulong now = now_us();
        if (now >= _paceTimer) {
            paced_operations();
            _paceTimer += 20_000;
        }

        if (_applicationTimer != 0 && now >= _applicationTimer) {
            _applicationTimer = 0;
            v150_1_timer_expired(require_state(), now);
        }

        if (_automatedMode) {
            if (_testErrors != 0) {
                socket_dgram_harness_request_stop();
            } else if (_txUnique >= AutomatedMessages &&
                       _rxUnique >= AutomatedMessages &&
                       _txRepeats >= AutomatedMessages * 2 &&
                       _rxRepeats >= AutomatedMessages * 2) {
                if (_completionStarted == 0)
                    _completionStarted = now;
                else if (now >= _completionStarted + AutomatedStopGraceUs)
                    socket_dgram_harness_request_stop();
            } else if (now >= _testStarted + AutomatedTimeoutUs) {
                _testErrors++;
                Console.Error.WriteLine(
                    "ERROR V150_SSE internal timeout tx_unique={0} tx_repeats={1} rx_unique={2} rx_repeats={3}",
                    _txUnique,
                    _txRepeats,
                    _rxUnique,
                    _rxRepeats);
                socket_dgram_harness_request_stop();
            }
        }

        socket_dgram_harness_timer = _applicationTimer != 0 && _applicationTimer < _paceTimer
            ? _applicationTimer
            : _paceTimer;
    }

    private static int v150_1_sse_tests(bool callingParty) {
        reset_counters(callingParty);
        Console.Error.WriteLine(
            "STATE V150_SSE BUILD revision={0} source=v150_1_sse_tests.c adapter_rebuild=required rx_data_callback=1 rx_status_callback=1 datagram_timer_dispatch=io_and_timeout traffic=caller_AA_answerer_P_state",
            BuildRevision);

        TxSseRtp.Version = 2;
        TxSseRtp.Padding = 0;
        TxSseRtp.Extension = 0;
        TxSseRtp.CsrcCount = 0;
        TxSseRtp.Marker = 0;
        TxSseRtp.PayloadType = PacketType;
        TxSseRtp.SequenceNumber = 0;
        TxSseRtp.Timestamp = 0;
        TxSseRtp.Ssrc = callingParty ? 0x43414C4CU : 0x414E5357U;

        _datagramState = socket_dgram_harness_init(
            null,
            callingParty ? "/tmp/sse_socket_a" : "/tmp/sse_socket_b",
            callingParty ? "/tmp/sse_socket_b" : "/tmp/sse_socket_a",
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
            Console.Error.WriteLine("ERROR V150_SSE cannot start socket harness");
            return 2;
        }

        _before = v150_sse_capture_snapshot(callingParty, null, _datagramState);

        _v1501State = v150_1_init(
            null,
            guarded_sprt_tx_packet_handler,
            _datagramState,
            120,
            120,
            tx_packet_handler,
            _datagramState,
            v150_1_timer_handler,
            null,
            sse_rx_data_handler,
            _datagramState,
            sse_rx_status_report_handler,
            _datagramState,
            null,
            null);

        if (_v1501State is null) {
            DiagnosticSnapshot after = v150_sse_capture_snapshot(callingParty, null, _datagramState);
            Console.Error.WriteLine(
                "STATE V150_SSE REQUIREMENTS role={0} expected_context_present=1 actual_context_present=0 expected_sprt_tx_callback=1 actual_sprt_tx_callback=1 expected_sse_tx_callback=1 actual_sse_tx_callback=1 expected_timer_callback=1 actual_timer_callback=1 expected_rx_data_callback=1 actual_rx_data_callback=1 expected_rx_status_callback=1 actual_rx_status_callback=1 expected_spe_signal_callback=0 actual_spe_signal_callback=0",
                callingParty ? "caller" : "answerer");
            v150_sse_print_snapshot("BEFORE", _before);
            v150_sse_print_snapshot("AFTER", after);
            Console.Error.WriteLine("ERROR V150_SSE cannot initialise context");
            socket_dgram_harness_free(_datagramState);
            _datagramState = null;
            return 2;
        }

        socket_dgram_harness_set_user_data(_datagramState, _v1501State);
        _v1501State.Logging.Level = V1501LogLevel.Error;
        _v1501State.Logging.Protocol = callingParty ? "SSE-C" : "SSE-A";
        _v1501State.Logging.Handler = (_, protocol, text) => Console.Error.WriteLine("{0}: {1}", protocol, text);

        if (v150_1_set_sse_reliability_method(
                _v1501State,
                (int)V1501SseReliabilityMethod.Repetition,
                3,
                20_000,
                0) != 0) {
            Console.Error.WriteLine("ERROR V150_SSE cannot configure repetition reliability");
            _testErrors++;
        }

        _testStarted = now_us();
        _nextSendTime = _testStarted + AutomatedStartDelayUs;
        _paceTimer = _testStarted + 20_000;
        socket_dgram_harness_timer = _paceTimer;
        _before = v150_sse_capture_snapshot(callingParty, _v1501State, _datagramState);

        int runResult = socket_dgram_harness_run(_datagramState);
        if (runResult != 0) {
            _testErrors++;
            Console.Error.WriteLine("ERROR V150_SSE socket harness result={0}", runResult);
        }

        Console.Error.WriteLine(
            "V150_SSE RESULT role={0} tx_unique={1} tx_repeats={2} rx_unique={3} rx_repeats={4} target={5} remote_state={6} scheduled_aa={7} tx_aa={8}/{9} tx_p={10}/{11} rx_aa={12}/{13} rx_p={14}/{15} unexpected_ric={16}",
            callingParty ? "caller" : "answerer",
            _txUnique,
            _txRepeats,
            _rxUnique,
            _rxRepeats,
            AutomatedMessages,
            (int)_v1501State.RemoteMediaState,
            _scheduledAaMessages,
            _txAaUnique,
            _txAaRepeats,
            _txPUnique,
            _txPRepeats,
            _rxAaUnique,
            _rxAaRepeats,
            _rxPUnique,
            _rxPRepeats,
            _unexpectedSseRic);

        if (_automatedMode && !automated_expectations_satisfied(callingParty))
            _testErrors++;

        if (_testErrors != 0) {
            DiagnosticSnapshot after = v150_sse_capture_snapshot(callingParty, _v1501State, _datagramState);
            Console.Error.WriteLine(
                "STATE V150_SSE REQUIREMENTS role={0} expected_tx_unique={1} actual_tx_unique={2} expected_tx_repeats={3} actual_tx_repeats={4} expected_rx_unique={5} actual_rx_unique={6} expected_rx_repeats={7} actual_rx_repeats={8} expected_remote_media={9} actual_remote_media={10} expected_unexpected_sprt_tx=0 actual_unexpected_sprt_tx={11} expected_rx_data_bytes=0 actual_rx_data_bytes={12} expected_unexpected_sse_ric=0 actual_unexpected_sse_ric={13} scheduled_aa={14} tx_aa_unique={15} tx_aa_repeats={16} tx_p_unique={17} tx_p_repeats={18} rx_aa_unique={19} rx_aa_repeats={20} rx_p_unique={21} rx_p_repeats={22} rx_status_calls={23} rx_status_media_changes={24}",
                callingParty ? "caller" : "answerer",
                AutomatedMessages,
                _txUnique,
                AutomatedMessages * 2,
                _txRepeats,
                AutomatedMessages,
                _rxUnique,
                AutomatedMessages * 2,
                _rxRepeats,
                (int)V1501MediaState.ModemRelay,
                (int)_v1501State.RemoteMediaState,
                _unexpectedSprtTxCalls,
                _rxDataCallbackBytes + _rxDataCallbackFill,
                _unexpectedSseRic,
                _scheduledAaMessages,
                _txAaUnique,
                _txAaRepeats,
                _txPUnique,
                _txPRepeats,
                _rxAaUnique,
                _rxAaRepeats,
                _rxPUnique,
                _rxPRepeats,
                _rxStatusCallbackCalls,
                _rxStatusMediaChangeCalls);
            v150_sse_print_snapshot("BEFORE", _before);
            v150_sse_print_snapshot("AFTER", after);
        }

        v150_1_free(_v1501State);
        _v1501State = null;
        socket_dgram_harness_free(_datagramState);
        _datagramState = null;

        if (_testErrors != 0) {
            Console.Error.WriteLine("ERROR V150_SSE test failed errors={0}", _testErrors);
            return 2;
        }
        return 0;
    }

    private static bool automated_expectations_satisfied(bool callingParty) {
        if (_txUnique < AutomatedMessages ||
            _rxUnique < AutomatedMessages ||
            _txRepeats < AutomatedMessages * 2 ||
            _rxRepeats < AutomatedMessages * 2 ||
            _unexpectedSseRic != 0) {
            return false;
        }

        if (callingParty) {
            return _scheduledAaMessages == AutomatedMessages &&
                   _txAaUnique == AutomatedMessages &&
                   _txAaRepeats == AutomatedMessages * 2 &&
                   _txPUnique == 0 &&
                   _txPRepeats == 0 &&
                   _rxPUnique == AutomatedMessages &&
                   _rxPRepeats == AutomatedMessages * 2 &&
                   _rxAaUnique == 0 &&
                   _rxAaRepeats == 0;
        }

        return _scheduledAaMessages == 0 &&
               _txPUnique == AutomatedMessages &&
               _txPRepeats == AutomatedMessages * 2 &&
               _txAaUnique == 0 &&
               _txAaRepeats == 0 &&
               _rxAaUnique == AutomatedMessages &&
               _rxAaRepeats == AutomatedMessages * 2 &&
               _rxPUnique == 0 &&
               _rxPRepeats == 0;
    }

    private static void reset_counters(bool callingParty) {
        _activeCallingParty = callingParty;
        _sendMessages = !_automatedMode || callingParty;
        _testErrors = 0;
        _txUnique = 0;
        _txRepeats = 0;
        _rxUnique = 0;
        _rxRepeats = 0;
        _haveRxPacket = false;
        _lastRxSequence = 0;
        _lastRxTimestamp = 0;
        _testStarted = 0;
        _nextSendTime = 0;
        _completionStarted = 0;
        _rxDataCallbackCalls = 0;
        _rxDataCallbackBytes = 0;
        _rxDataCallbackFill = 0;
        _rxStatusCallbackCalls = 0;
        _rxStatusMediaChangeCalls = 0;
        _unexpectedSprtTxCalls = 0;
        _scheduledAaMessages = 0;
        _txAaUnique = 0;
        _txAaRepeats = 0;
        _txPUnique = 0;
        _txPRepeats = 0;
        _rxAaUnique = 0;
        _rxAaRepeats = 0;
        _rxPUnique = 0;
        _rxPRepeats = 0;
        _unexpectedSseRic = 0;
        _paceNumber = 0;
        _paceTimer = 0;
        _applicationTimer = 0;
        _sequence = 0;
        _timestamp = 0;
    }

    private static int bool_int(bool value) => value ? 1 : 0;

    private static V1501State require_state() =>
        _v1501State ?? throw new InvalidOperationException("V.150.1 SSE state is not initialized.");
}
