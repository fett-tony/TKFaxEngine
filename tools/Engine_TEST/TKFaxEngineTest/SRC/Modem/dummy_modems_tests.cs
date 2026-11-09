/*
 * TKFaxEngine - deterministic two-process data modem/socket/DTE test.
 *
 * Managed C# port of the supplied dummy_modems_tests.c, based on the original
 * spanDSP test by Steve Underwood. The test keeps the real V.22bis audio path,
 * asynchronous DTE framing, TCP audio transport, deterministic payload,
 * ready/ack barrier and CRC validation.
 */

#nullable enable
using AtModemControlOperation = TKFaxEngine.AtModemControlOperation;
using AsyncParity = TKFaxEngine.AsyncParity;
using AtCallEvent = TKFaxEngine.AtCallEvent;
using AtReceiveMode = TKFaxEngine.AtReceiveMode;

using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using TKFaxEngineTest.System;
using static TKFaxEngineTest.System.socket_harness;
using TKFaxEngine.Modem;
using static TKFaxEngine.Modem.data_modems;

namespace TKFaxEngineTest.Modem.Test;

public static class dummy_modems_tests {
    private const string OutputWaveFileName = "dummy_modems.wav";
    private const int DummyModemSyncBytes = 16;
    private const int DummyModemHeaderBytes = 7;
    private const int DummyModemChunkBytes = 256;
    private const int DummyModemChunkCount = 8;
    private const int DummyModemCheckBytes = DummyModemChunkBytes * DummyModemChunkCount;
    private const int DummyModemCrcBytes = 4;
    private const int DummyModemFrameBytes =
        DummyModemSyncBytes + DummyModemHeaderBytes + DummyModemChunkBytes + DummyModemCrcBytes;
    private const byte DummyModemFlagMutual = 0x01;
    private const byte DummyModemFlagReady = 0x02;
    private const byte DummyModemFlagReadyAck = 0x04;
    private const byte DummyModemAllowedFlags =
        DummyModemFlagMutual | DummyModemFlagReady | DummyModemFlagReadyAck;
    private const int DummyModemReadyChunk = 0xFF;
    private const int DummyModemFinalFrames = 1;
    private const int DummyModemTxDrainSamples = 320;
    private const int DummyModemTimeoutMilliseconds = 30_000;
    private const int DummyModemDteTimeoutMilliseconds = 5_000;
    private const string DummyModemTestNumber = "5551234";
    private const string SocketName = "/tmp/modemsocket";
    private const int AudioBlockSamples = 160;
    private const int AudioBlockMilliseconds = 20;

    private static readonly byte[] DummyModemCallerSync =
    [
        (byte)'T', (byte)'K', (byte)'F', (byte)'X', (byte)'-', (byte)'C', (byte)'A', (byte)'L',
        (byte)'L', (byte)'E', (byte)'R', (byte)'-', 0xC1, 0x3C, 0x96, 0x5A
    ];

    private static readonly byte[] DummyModemAnswererSync =
    [
        (byte)'T', (byte)'K', (byte)'F', (byte)'X', (byte)'-', (byte)'A', (byte)'N', (byte)'S',
        (byte)'W', (byte)'E', (byte)'R', (byte)'-', 0xA1, 0xC3, 0x69, 0xA5
    ];

    private static readonly Endpoint EndpointState = new();
    private static readonly DiagnosticSnapshot BeforeSnapshot = new();

    private static data_modems_state_t? _dataModemState;
    private static socket_harness_state_t? _activeHarness;
    private static TelephonyWaveWriter? _waveWriter;
    private static readonly short[] WaveBuffer = new short[AudioBlockSamples * 2];
    private static bool _answered;
    private static bool _done;
    private static bool _automatedMode;

    public static int tkfx_dummy_modems_tests_body_main(string[] args) {
        int logAudio = 0;
        int useGui = 0;
        bool callingParty = false;
        _automatedMode = false;

        foreach (string argument in args) {
            switch (argument) {
                case "-a":
                    callingParty = false;
                    break;
                case "-c":
                    callingParty = true;
                    break;
                case "-g":
                    useGui = 1;
                    break;
                case "-l":
                    logAudio = 1;
                    break;
                case "-x":
                    _automatedMode = true;
                    break;
                default:
                    return 2;
            }
        }

        try {
            if (modem_tests(useGui, logAudio, callingParty) != 0)
                return 2;

            Console.WriteLine("Tests passed");
            return 0;
        } catch (Exception exception) {
            Console.Error.WriteLine("ERROR DUMMY_MODEM unhandled exception: {0}", exception);
            return 2;
        }
    }

    private static int initial_at_tx_handler(object? userData, ReadOnlySpan<byte> buffer) {
        _ = userData;
        _ = buffer;
        return 0;
    }

    private static int modem_call_control(
        data_modems_state_t state,
        object? userData,
        int operation,
        string? number) {
        var endpoint = userData as Endpoint;
        if (endpoint is not null)
            endpoint.StatusEvents++;

        switch ((AtModemControlOperation)operation) {
            case AtModemControlOperation.Call:
                if (endpoint is null || !endpoint.CallingParty || string.IsNullOrEmpty(number)) {
                    if (endpoint is not null)
                        endpoint.Errors++;
                    Console.Error.WriteLine(
                        "ERROR DUMMY_MODEM invalid CALL control role={0} number={1}",
                        endpoint?.CallingParty == true ? "caller" : "answerer",
                        number ?? "<null>");
                    return -1;
                }

                Console.Error.WriteLine("DUMMY_MODEM CALL role=caller number={0}", number);
                data_modems_call_event(state, (int)AtCallEvent.Connected);
                if (state.at_state!.ReceiveMode != AtReceiveMode.Connected) {
                    endpoint.Errors++;
                    Console.Error.WriteLine(
                        "ERROR DUMMY_MODEM caller did not enter connected data mode actual={0}",
                        (int)state.at_state!.ReceiveMode);
                    return -1;
                }

                mark_call_control_seen(endpoint);
                break;

            case AtModemControlOperation.Answer:
                if (endpoint is null || endpoint.CallingParty) {
                    if (endpoint is not null)
                        endpoint.Errors++;
                    Console.Error.WriteLine(
                        "ERROR DUMMY_MODEM invalid ANSWER control role={0}",
                        endpoint?.CallingParty == true ? "caller" : "answerer");
                    return -1;
                }

                _answered = true;
                Console.Error.WriteLine("DUMMY_MODEM ANSWER role=answerer");
                data_modems_call_event(state, (int)AtCallEvent.Answered);
                if (state.at_state!.ReceiveMode != AtReceiveMode.Connected) {
                    endpoint.Errors++;
                    Console.Error.WriteLine(
                        "ERROR DUMMY_MODEM answerer did not enter connected data mode actual={0}",
                        (int)state.at_state!.ReceiveMode);
                    return -1;
                }

                mark_call_control_seen(endpoint);
                break;

            case AtModemControlOperation.Hangup:
            case AtModemControlOperation.OnHook:
                _done = true;
                break;
        }

        return 0;
    }

    private static uint dummy_modem_crc32_update(uint crc, byte value) {
        crc ^= value;
        for (int i = 0; i < 8; i++)
            crc = (crc >> 1) ^ (0xEDB88320u & (0u - (crc & 1u)));
        return crc;
    }

    private static ReadOnlySpan<byte> dummy_modem_tx_sync(Endpoint endpoint) =>
        endpoint.CallingParty ? DummyModemCallerSync : DummyModemAnswererSync;

    private static ReadOnlySpan<byte> dummy_modem_expected_sync(Endpoint endpoint) =>
        endpoint.CallingParty ? DummyModemAnswererSync : DummyModemCallerSync;

    private static byte dummy_modem_header_byte(
        bool callingParty,
        int chunkIndex,
        byte acknowledgementMask,
        byte flags,
        int index) {
        return index switch {
            0 => 1,
            1 => callingParty ? (byte)0xC1 : (byte)0xA1,
            2 => (byte)chunkIndex,
            3 => (byte)(DummyModemChunkBytes & 0xFF),
            4 => (byte)((DummyModemChunkBytes >> 8) & 0xFF),
            5 => acknowledgementMask,
            6 => flags,
            _ => 0
        };
    }

    private static byte dummy_modem_payload_byte(bool callingParty, int index) {
        uint value = unchecked((uint)index + (callingParty ? 0x13579BDFu : 0x2468ACE0u));
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        value ^= value >> 16;
        return (byte)value;
    }

    private static byte dummy_modem_ready_payload_byte(bool callingParty, int index) {
        uint value = unchecked((uint)index + (callingParty ? 0xC0DEC001u : 0xA11CE002u));
        value ^= value >> 13;
        value *= 0x9E3779B1u;
        value ^= value >> 16;
        return (byte)value;
    }

    private static byte dummy_modem_frame_payload_byte(bool callingParty, int chunkIndex, byte flags, int index) =>
        (flags & DummyModemFlagReady) != 0
            ? dummy_modem_ready_payload_byte(callingParty, index)
            : dummy_modem_payload_byte(callingParty, chunkIndex * DummyModemChunkBytes + index);

    private static uint dummy_modem_frame_crc(bool callingParty, int chunkIndex, byte acknowledgementMask, byte flags) {
        uint crc = 0xFFFFFFFFu;
        for (int i = 0; i < DummyModemHeaderBytes; i++)
            crc = dummy_modem_crc32_update(crc, dummy_modem_header_byte(callingParty, chunkIndex, acknowledgementMask, flags, i));
        for (int i = 0; i < DummyModemChunkBytes; i++)
            crc = dummy_modem_crc32_update(crc, dummy_modem_frame_payload_byte(callingParty, chunkIndex, flags, i));
        return crc ^ 0xFFFFFFFFu;
    }

    private static byte dummy_modem_frame_byte(Endpoint endpoint, int offset) {
        ReadOnlySpan<byte> sync = dummy_modem_tx_sync(endpoint);
        if (offset < DummyModemSyncBytes)
            return sync[offset];

        int frameOffset = offset - DummyModemSyncBytes;
        if (frameOffset < DummyModemHeaderBytes) {
            return dummy_modem_header_byte(
                endpoint.CallingParty,
                endpoint.TxFrameChunk,
                endpoint.TxFrameAcknowledgementMask,
                endpoint.TxFrameFlags,
                frameOffset);
        }

        frameOffset -= DummyModemHeaderBytes;
        if (frameOffset < DummyModemChunkBytes) {
            return dummy_modem_frame_payload_byte(
                endpoint.CallingParty,
                endpoint.TxFrameChunk,
                endpoint.TxFrameFlags,
                frameOffset);
        }

        frameOffset -= DummyModemChunkBytes;
        if (frameOffset < DummyModemCrcBytes)
            return (byte)((endpoint.TxFrameCrc >> (8 * frameOffset)) & 0xFFu);

        return 0;
    }

    private static void dummy_modem_reset_rx_frame(Endpoint endpoint) {
        endpoint.ReceiveState = ReceiveParserState.Sync;
        endpoint.ReceiveSyncMatch = 0;
        endpoint.ReceiveHeaderPosition = 0;
        endpoint.ReceivePayloadPosition = 0;
        endpoint.ReceiveCrcPosition = 0;
        endpoint.ReceiveFrameBad = 0;
        endpoint.ReceiveCrc = 0xFFFFFFFFu;
    }

    private static void dummy_modem_rx_byte(Endpoint endpoint, byte value) {
        ReadOnlySpan<byte> sync = dummy_modem_expected_sync(endpoint);
        bool peerCallingParty = !endpoint.CallingParty;
        endpoint.ReceivedBytes++;

        switch (endpoint.ReceiveState) {
            case ReceiveParserState.Sync:
                if (value == sync[endpoint.ReceiveSyncMatch]) {
                    endpoint.ReceiveSyncMatch++;
                    if (endpoint.ReceiveSyncMatch == DummyModemSyncBytes) {
                        endpoint.ReceiveState = ReceiveParserState.Header;
                        endpoint.ReceiveHeaderPosition = 0;
                        endpoint.ReceiveCrc = 0xFFFFFFFFu;
                        endpoint.ReceiveFrameBad = 0;
                    }
                } else {
                    endpoint.ReceiveSyncMatch = value == sync[0] ? 1 : 0;
                }
                break;

            case ReceiveParserState.Header:
                endpoint.ReceiveHeader[endpoint.ReceiveHeaderPosition] = value;
                endpoint.ReceiveCrc = dummy_modem_crc32_update(endpoint.ReceiveCrc, value);
                endpoint.ReceiveHeaderPosition++;
                if (endpoint.ReceiveHeaderPosition == DummyModemHeaderBytes) {
                    bool readyFrame =
                        (endpoint.ReceiveHeader[6] & DummyModemFlagReady) != 0;
                    bool invalid =
                        endpoint.ReceiveHeader[0] != 1 ||
                        endpoint.ReceiveHeader[1] != (peerCallingParty ? 0xC1 : 0xA1) ||
                        endpoint.ReceiveHeader[3] != (DummyModemChunkBytes & 0xFF) ||
                        endpoint.ReceiveHeader[4] != ((DummyModemChunkBytes >> 8) & 0xFF) ||
                        (endpoint.ReceiveHeader[6] & ~DummyModemAllowedFlags) != 0 ||
                        (readyFrame
                            ? endpoint.ReceiveHeader[2] != DummyModemReadyChunk ||
                              (endpoint.ReceiveHeader[6] & DummyModemFlagMutual) != 0
                            : endpoint.ReceiveHeader[2] >= DummyModemChunkCount ||
                              (endpoint.ReceiveHeader[6] & DummyModemFlagReadyAck) != 0);

                    if (invalid) {
                        endpoint.Errors++;
                        Console.Error.WriteLine(
                            "ERROR DUMMY_MODEM frame header role={0} version={1} peer_role=0x{2:X2} length={3}",
                            dummy_modem_role(endpoint),
                            endpoint.ReceiveHeader[0],
                            endpoint.ReceiveHeader[1],
                            endpoint.ReceiveHeader[3] | (endpoint.ReceiveHeader[4] << 8));
                        dummy_modem_reset_rx_frame(endpoint);
                        break;
                    }

                    endpoint.ReceivePayloadPosition = 0;
                    endpoint.ReceiveState = ReceiveParserState.Payload;
                }
                break;

            case ReceiveParserState.Payload:
                bool isReadyFrame =
                    (endpoint.ReceiveHeader[6] & DummyModemFlagReady) != 0;
                byte expected = dummy_modem_frame_payload_byte(
                    peerCallingParty,
                    endpoint.ReceiveHeader[2],
                    endpoint.ReceiveHeader[6],
                    endpoint.ReceivePayloadPosition);

                endpoint.ReceiveCrc = dummy_modem_crc32_update(endpoint.ReceiveCrc, value);
                endpoint.ReceivePayloadPosition++;
                if (value != expected) {
                    endpoint.ReceiveFrameBad++;
                    if (isReadyFrame) {
                        endpoint.ReceiveReadyBadBytes++;
                    } else {
                        endpoint.BadBytes++;
                        if (endpoint.BadBytes <= 16) {
                            Console.Error.WriteLine(
                                "ERROR DUMMY_MODEM role={0} payload_byte={1} expected=0x{2:X2} actual=0x{3:X2}",
                                dummy_modem_role(endpoint),
                                endpoint.ReceivePayloadPosition,
                                expected,
                                value);
                        }
                    }
                }

                if (endpoint.ReceivePayloadPosition == DummyModemChunkBytes) {
                    endpoint.ReceiveCrcPosition = 0;
                    endpoint.ReceiveState = ReceiveParserState.Crc;
                }
                break;

            case ReceiveParserState.Crc:
                endpoint.ReceiveCrcBytes[endpoint.ReceiveCrcPosition++] = value;
                if (endpoint.ReceiveCrcPosition == DummyModemCrcBytes)
                    dummy_modem_complete_receive_frame(endpoint);
                break;

            default:
                endpoint.Errors++;
                dummy_modem_reset_rx_frame(endpoint);
                break;
        }
    }

    private static void dummy_modem_complete_receive_frame(Endpoint endpoint) {
        uint actualCrc = BinaryPrimitives.ReadUInt32LittleEndian(endpoint.ReceiveCrcBytes);
        uint expectedCrc = endpoint.ReceiveCrc ^ 0xFFFFFFFFu;
        endpoint.ReceiveFramesSeen++;

        if ((endpoint.ReceiveHeader[6] & DummyModemFlagReady) != 0) {
            if (endpoint.ReceiveFrameBad != 0 || actualCrc != expectedCrc) {
                endpoint.ReceiveReadyBadFrames++;
                Console.Error.WriteLine(
                    "DUMMY_MODEM READY RETRY role={0} frame={1} payload_bad={2} crc_expected=0x{3:X8} crc_actual=0x{4:X8}",
                    dummy_modem_role(endpoint),
                    endpoint.ReceiveFramesSeen,
                    endpoint.ReceiveFrameBad,
                    expectedCrc,
                    actualCrc);
            } else {
                endpoint.ReceiveReadyFrames++;
                endpoint.ReceiveReadySeen = 1;
                if ((endpoint.ReceiveHeader[6] & DummyModemFlagReadyAck) != 0)
                    endpoint.PeerReadyAcknowledgementSeen = 1;

                Console.Error.WriteLine(
                    "DUMMY_MODEM READY PASS role={0} frame={1} ready_frames={2} peer_ack={3} crc=0x{4:X8}",
                    dummy_modem_role(endpoint),
                    endpoint.ReceiveFramesSeen,
                    endpoint.ReceiveReadyFrames,
                    endpoint.PeerReadyAcknowledgementSeen,
                    actualCrc);
            }
        } else if (endpoint.ReceiveReadySeen == 0 || endpoint.PeerReadyAcknowledgementSeen == 0) {
            endpoint.Errors++;
            Console.Error.WriteLine(
                "ERROR DUMMY_MODEM data before ready barrier role={0} frame={1} ready_seen={2} peer_ready_ack={3}",
                dummy_modem_role(endpoint),
                endpoint.ReceiveFramesSeen,
                endpoint.ReceiveReadySeen,
                endpoint.PeerReadyAcknowledgementSeen);
        } else if (endpoint.ReceiveFrameBad != 0 || actualCrc != expectedCrc) {
            endpoint.Errors++;
            Console.Error.WriteLine(
                "ERROR DUMMY_MODEM frame validation role={0} frame={1} payload_bad={2} crc_expected=0x{3:X8} crc_actual=0x{4:X8}",
                dummy_modem_role(endpoint),
                endpoint.ReceiveFramesSeen,
                endpoint.ReceiveFrameBad,
                expectedCrc,
                actualCrc);
        } else {
            uint chunkBit = 1u << endpoint.ReceiveHeader[2];
            uint fullMask = (1u << DummyModemChunkCount) - 1u;

            endpoint.ReceiveExactFrames++;
            if ((endpoint.ReceiveChunkMask & chunkBit) == 0) {
                endpoint.ReceiveChunkMask |= chunkBit;
                endpoint.CheckedBytes += DummyModemChunkBytes;
            }

            endpoint.PeerAcknowledgementMask = endpoint.ReceiveHeader[5];
            if ((endpoint.ReceiveHeader[6] & DummyModemFlagMutual) != 0)
                endpoint.PeerMutualSeen = 1;
            if (endpoint.ReceiveChunkMask == fullMask)
                endpoint.ReceiveFrameComplete = 1;
            if (endpoint.ReceiveFrameComplete != 0 && endpoint.PeerAcknowledgementMask == (byte)fullMask)
                endpoint.LocalMutualSeen = 1;

            Console.Error.WriteLine(
                "DUMMY_MODEM CHUNK PASS role={0} frame={1} chunk={2} checked={3} local_mask=0x{4:X2} peer_ack=0x{5:X2} local_mutual={6} peer_mutual={7} crc=0x{8:X8}",
                dummy_modem_role(endpoint),
                endpoint.ReceiveFramesSeen,
                endpoint.ReceiveHeader[2],
                endpoint.CheckedBytes,
                endpoint.ReceiveChunkMask & 0xFFu,
                endpoint.PeerAcknowledgementMask,
                endpoint.LocalMutualSeen,
                endpoint.PeerMutualSeen,
                actualCrc);

            if (endpoint.ReceiveFrameComplete != 0) {
                Console.Error.WriteLine(
                    "DUMMY_MODEM PAYLOAD PASS role={0} chunks={1} payload_bytes={2} peer_ack=0x{3:X2}",
                    dummy_modem_role(endpoint),
                    DummyModemChunkCount,
                    DummyModemCheckBytes,
                    endpoint.PeerAcknowledgementMask);
            }

            if (endpoint.LocalMutualSeen != 0 && endpoint.PeerMutualSeen != 0) {
                Console.Error.WriteLine(
                    "DUMMY_MODEM BARRIER PASS role={0} local_mask=0x{1:X2} peer_ack=0x{2:X2}",
                    dummy_modem_role(endpoint),
                    endpoint.ReceiveChunkMask & 0xFFu,
                    endpoint.PeerAcknowledgementMask);
            }
        }

        dummy_modem_reset_rx_frame(endpoint);
    }

    private static int get_msg(object? userData, byte[] message, int maximumLength) {
        var endpoint = userData as Endpoint;
        if (endpoint is null || maximumLength <= 0)
            return 0;

        int length = Math.Min(maximumLength, message.Length);
        for (int i = 0; i < length; i++) {
            if (endpoint.TxFrameOffset == 0) {
                if (endpoint.TxDataStarted == 0) {
                    endpoint.TxFrameChunk = DummyModemReadyChunk;
                    endpoint.TxFrameAcknowledgementMask = 0;
                    endpoint.TxFrameFlags = (byte)(
                        DummyModemFlagReady |
                        (endpoint.ReceiveReadySeen != 0 ? DummyModemFlagReadyAck : 0));
                    endpoint.TxFrameStartedAfterBarrier = 0;
                } else {
                    endpoint.TxFrameChunk = endpoint.TxChunkIndex;
                    endpoint.TxFrameAcknowledgementMask = (byte)(endpoint.ReceiveChunkMask & 0xFFu);
                    endpoint.TxFrameFlags = endpoint.LocalMutualSeen != 0 ? DummyModemFlagMutual : (byte)0;
                    endpoint.TxFrameStartedAfterBarrier =
                        endpoint.LocalMutualSeen != 0 && endpoint.PeerMutualSeen != 0 ? 1 : 0;
                }

                endpoint.TxFrameCrc = dummy_modem_frame_crc(
                    endpoint.CallingParty,
                    endpoint.TxFrameChunk,
                    endpoint.TxFrameAcknowledgementMask,
                    endpoint.TxFrameFlags);
            }

            message[i] = dummy_modem_frame_byte(endpoint, endpoint.TxFrameOffset);
            endpoint.TxFrameOffset++;

            if (endpoint.TxFrameOffset == DummyModemFrameBytes) {
                endpoint.TxFrameOffset = 0;
                endpoint.TxFramesCompleted++;

                if ((endpoint.TxFrameFlags & DummyModemFlagReady) != 0) {
                    endpoint.TxReadyFramesCompleted++;
                    if ((endpoint.TxFrameFlags & DummyModemFlagReadyAck) != 0)
                        endpoint.TxReadyAcknowledgementFramesCompleted++;

                    if (endpoint.ReceiveReadySeen != 0 &&
                        endpoint.PeerReadyAcknowledgementSeen != 0 &&
                        endpoint.TxReadyAcknowledgementFramesCompleted > 0) {
                        endpoint.TxDataStarted = 1;
                        Console.Error.WriteLine(
                            "DUMMY_MODEM READY BARRIER role={0} tx_ready_frames={1} tx_ready_ack_frames={2} rx_ready_frames={3} rx_ready_retries={4}",
                            dummy_modem_role(endpoint),
                            endpoint.TxReadyFramesCompleted,
                            endpoint.TxReadyAcknowledgementFramesCompleted,
                            endpoint.ReceiveReadyFrames,
                            endpoint.ReceiveReadyBadFrames);
                    }
                } else {
                    endpoint.TxChunkIndex = (endpoint.TxChunkIndex + 1) % DummyModemChunkCount;
                    if (endpoint.TxFrameStartedAfterBarrier != 0)
                        endpoint.TxFinalFramesCompleted++;

                    if (endpoint.LocalMutualSeen != 0 &&
                        endpoint.PeerMutualSeen != 0 &&
                        endpoint.TxFinalFramesCompleted >= DummyModemFinalFrames) {
                        endpoint.TxStopPending = 1;
                    }
                }
            }
        }

        return length;
    }

    private static int put_msg(object? userData, byte[]? message, int lengthOrStatus) {
        var endpoint = userData as Endpoint;
        if (endpoint is null)
            return 0;

        if (lengthOrStatus < 0) {
            endpoint.StatusEvents++;
            return 0;
        }

        if (message is null)
            return 0;

        int length = Math.Min(lengthOrStatus, message.Length);
        for (int i = 0; i < length; i++)
            dummy_modem_rx_byte(endpoint, message[i]);

        if (length > 0 && _dataModemState is not null)
            _dataModemState.rx_frame_received = true;

        return 0;
    }

    private static void terminal_callback(object? userData, ReadOnlySpan<byte> message) {
        if (userData is not data_modems_state_t state)
            throw new ArgumentException("The terminal callback requires a data_modems_state_t.", nameof(userData));

        TKFaxEngine.AtInterpreterApi.at_interpreter(
            state.at_state!,
            Encoding.Latin1.GetString(message),
            message.Length);
    }

    private static int termios_callback(object? userData, termios termios) {
        _ = userData;
        _ = termios;
        return 0;
    }

    private static void hangup_callback(object? userData, int status) {
        _ = userData;
        _ = status;
        EndpointState.StatusEvents++;
    }

    private static int terminal_free_space_callback(object? userData) {
        _ = userData;
        return 1024;
    }

    private static int rx_fillin_callback(object? userData, int samples) {
        check_internal_timeout(EndpointState);
        return data_modems_rx_fillin((data_modems_state_t)userData!, samples);
    }

    private static int configure_automated_v22bis(data_modems_state_t state) {
        state.queued_modem = DATA_MODEM_V22BIS;
        state.queued_baud_rate = 600;
        state.queued_bit_rate = 2400;
        data_modems_set_modem_type(state, DATA_MODEM_V22BIS, 600, 2400);

        if (state.current_modem != DATA_MODEM_V22BIS ||
            state.queued_modem != DATA_MODEM_V22BIS ||
            state.queued_baud_rate != 600 ||
            state.queued_bit_rate != 2400) {
            Console.Error.WriteLine(
                "ERROR DUMMY_MODEM V.22bis state mismatch current={0} queued={1} baud={2} bit_rate={3}",
                (int)state.current_modem,
                (int)state.queued_modem,
                state.queued_baud_rate,
                state.queued_bit_rate);
            return -1;
        }

        return 0;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteFile(nint hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, nint lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadFile(nint hFile, byte[] lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, nint lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekNamedPipe(nint hNamedPipe, nint lpBuffer, uint nBufferSize, nint lpBytesRead, out uint lpTotalBytesAvail, nint lpBytesLeftThisMessage);

    [DllImport("kernel32.dll")]
    private static extern ulong GetTickCount64();

    [DllImport("kernel32.dll")]
    private static extern void Sleep(uint dwMilliseconds);

    private static int dte_write_all(nint pipe, string role, string command) {
        byte[] data = Encoding.ASCII.GetBytes(command);
        int offset = 0;
        while (offset < data.Length) {
            byte[] remaining = data.AsSpan(offset).ToArray();
            if (!WriteFile(pipe, remaining, (uint)remaining.Length, out uint written, 0)) {
                Console.Error.WriteLine(
                    "ERROR DUMMY_MODEM DTE write failed role={0} command={1} win32={2}",
                    role, command, Marshal.GetLastWin32Error());
                return -1;
            }
            if (written == 0) {
                Console.Error.WriteLine(
                    "ERROR DUMMY_MODEM DTE zero-byte write role={0} command={1}",
                    role, command);
                return -1;
            }
            offset += (int)written;
        }
        return 0;
    }

    private static int dte_wait_for_text(
        nint pipe,
        string role,
        string expected,
        byte[] response,
        uint timeoutMilliseconds) {
        int used = 0;
        ulong started = GetTickCount64();
        if (response.Length < 2)
            return -1;
        response[0] = 0;
        while (GetTickCount64() - started < timeoutMilliseconds) {
            if (!PeekNamedPipe(pipe, 0, 0, 0, out uint available, 0)) {
                Console.Error.WriteLine(
                    "ERROR DUMMY_MODEM DTE PeekNamedPipe failed role={0} win32={1}",
                    role, Marshal.GetLastWin32Error());
                return -1;
            }
            if (available > 0) {
                uint toRead = available;
                if (toRead > response.Length - 1 - used)
                    toRead = (uint)(response.Length - 1 - used);
                if (toRead == 0) {
                    Console.Error.WriteLine(
                        "ERROR DUMMY_MODEM DTE response overflow role={0} expected={1} bytes={2}",
                        role, expected, used);
                    return -1;
                }
                byte[] readBuffer = new byte[toRead];
                if (!ReadFile(pipe, readBuffer, toRead, out uint readBytes, 0)) {
                    Console.Error.WriteLine(
                        "ERROR DUMMY_MODEM DTE read failed role={0} win32={1}",
                        role, Marshal.GetLastWin32Error());
                    return -1;
                }
                readBuffer.AsSpan(0, (int)readBytes).CopyTo(response.AsSpan(used));
                used += (int)readBytes;
                response[used] = 0;
                string text = Encoding.ASCII.GetString(response, 0, used);
                if (text.Contains(expected, StringComparison.Ordinal))
                    return 0;
            }
            Sleep(5);
        }
        Console.Error.WriteLine(
            "ERROR DUMMY_MODEM DTE response timeout role={0} expected={1} bytes={2} response={3}",
            role, expected, used, Encoding.ASCII.GetString(response, 0, used));
        return -1;
    }

    private static int automated_dte_thread(Endpoint endpoint, nint pipe) {
        string role = dummy_modem_role(endpoint);
        string connectCommand = endpoint.CallingParty
            ? "ATD" + DummyModemTestNumber + "\r"
            : "ATA\r";
        byte[] response = new byte[256];

        if (pipe == 0 || pipe == new nint(-1))
            return 1;
        if (dte_write_all(pipe, role, "AT\r") != 0)
            return 2;
        if (dte_wait_for_text(pipe, role, "OK", response, DummyModemDteTimeoutMilliseconds) != 0)
            return 3;
        if (dte_write_all(pipe, role, connectCommand) != 0)
            return 4;

        ulong started = GetTickCount64();
        while (GetTickCount64() - started < DummyModemDteTimeoutMilliseconds) {
            if (has_seen_call_control(endpoint)) {
                endpoint.DteOk = 1;
                Console.Error.WriteLine(
                    "DUMMY_MODEM DTE PASS role={0} command={1} control_callback=completed",
                    role, endpoint.CallingParty ? "ATD" + DummyModemTestNumber : "ATA");
                return 0;
            }
            Sleep(5);
        }

        Console.Error.WriteLine(
            "ERROR DUMMY_MODEM DTE control timeout role={0} command={1} call_control_seen={2}",
            role, endpoint.CallingParty ? "ATD" + DummyModemTestNumber : "ATA",
            has_seen_call_control(endpoint) ? 1 : 0);
        return 5;
    }

    private static int modem_tests(int useGui, int logAudio, bool callingParty) {
        _ = useGui;
        EndpointState.Reset(callingParty);
        dummy_modem_reset_rx_frame(EndpointState);
        _done = false;
        _answered = false;

        _dataModemState = data_modems_init(
            null,
            callingParty,
            initial_at_tx_handler,
            EndpointState,
            modem_call_control,
            EndpointState,
            put_msg,
            get_msg,
            EndpointState);
        if (_dataModemState is null) {
            Console.Error.WriteLine("ERROR DUMMY_MODEM cannot initialise data modem");
            return 2;
        }

        data_modems_set_async_mode(_dataModemState, 8, (int)AsyncParity.None, 1);
        if (_automatedMode && configure_automated_v22bis(_dataModemState) != 0) {
            data_modems_free(_dataModemState);
            _dataModemState = null;
            return 2;
        }

        TKFaxEngine.SpanLogState logging = data_modems_get_logging_state(_dataModemState);
        TKFaxEngine.LoggingApi.span_log_set_level(
            logging,
            TKFaxEngine.LoggingApi.SPAN_LOG_SHOW_SEVERITY | TKFaxEngine.LoggingApi.SPAN_LOG_ERROR);
        TKFaxEngine.LoggingApi.span_log_set_level(
            logging,
            TKFaxEngine.LoggingApi.SPAN_LOG_SHOW_SEVERITY | TKFaxEngine.LoggingApi.SPAN_LOG_ERROR);
        TKFaxEngine.LoggingApi.span_log_set_tag(logging, callingParty ? "Dummy-C" : "Dummy-A");

        socket_harness_state_t? harness = socket_harness_init(
            null,
            SocketName,
            callingParty ? "modemC" : "modemA",
            callingParty ? 1 : 0,
            terminal_callback,
            termios_callback,
            hangup_callback,
            terminal_free_space_callback,
            rx_callback,
            rx_fillin_callback,
            tx_callback,
            _dataModemState);
        if (harness is null) {
            Console.Error.WriteLine("ERROR DUMMY_MODEM cannot start socket harness");
            data_modems_free(_dataModemState);
            _dataModemState = null;
            return 2;
        }
        _activeHarness = harness;
        data_modems_set_at_tx_handler(_dataModemState, socket_harness_terminal_write, harness);

        if (logAudio != 0) {
            try {
                _waveWriter = new TelephonyWaveWriter(OutputWaveFileName, 2, 8000);
            } catch {
                Console.Error.WriteLine("ERROR DUMMY_MODEM cannot create '{0}'", OutputWaveFileName);
                socket_harness_free(harness);
                data_modems_free(_dataModemState);
                _dataModemState = null;
                return 2;
            }
        }

        Task<int>? dteTask = null;
        if (_automatedMode)
            dteTask = Task.Run(() => automated_dte_thread(EndpointState, harness.pty.slave_fd));

        dummy_modem_capture_snapshot(BeforeSnapshot, EndpointState, _dataModemState, harness);
        int runResult = socket_harness_run(harness, callingParty ? 1 : 0);
        if (runResult != 0) {
            EndpointState.Errors++;
            Console.Error.WriteLine("ERROR DUMMY_MODEM socket harness result={0}", runResult);
        }

        if (dteTask is not null) {
            if (!dteTask.Wait(5000)) {
                EndpointState.Errors++;
                Console.Error.WriteLine("ERROR DUMMY_MODEM DTE thread did not finish");
            } else if (dteTask.Result != 0) {
                EndpointState.Errors++;
            }
        }

        _waveWriter?.Dispose();
        _waveWriter = null;

        validate_automated_result(EndpointState, _dataModemState);
        if (EndpointState.Errors != 0) {
            var after = new DiagnosticSnapshot();
            dummy_modem_capture_snapshot(after, EndpointState, _dataModemState, harness);
            dummy_modem_print_requirements(EndpointState, _dataModemState);
            dummy_modem_print_snapshot("BEFORE", BeforeSnapshot);
            dummy_modem_print_snapshot("AFTER", after);
        }
        dummy_modem_print_result(EndpointState, _dataModemState);

        socket_harness_free(harness);
        _activeHarness = null;
        data_modems_free(_dataModemState);
        _dataModemState = null;
        return EndpointState.Errors != 0 ? 2 : 0;
    }

    private static void validate_automated_result(Endpoint endpoint, data_modems_state_t state) {
        if (!_automatedMode)
            return;

        uint fullMask = (1u << DummyModemChunkCount) - 1u;
        if (endpoint.TxDataStarted != 1 ||
            endpoint.ReceiveReadySeen != 1 ||
            endpoint.PeerReadyAcknowledgementSeen != 1 ||
            endpoint.ReceiveFrameComplete != 1 ||
            endpoint.CheckedBytes != DummyModemCheckBytes ||
            endpoint.BadBytes != 0 ||
            endpoint.ReceiveExactFrames < DummyModemChunkCount ||
            endpoint.ReceiveChunkMask != fullMask ||
            endpoint.PeerAcknowledgementMask != (byte)fullMask ||
            endpoint.LocalMutualSeen != 1 ||
            endpoint.PeerMutualSeen != 1 ||
            endpoint.DteOk != 1 ||
            !has_seen_call_control(endpoint) ||
            state.current_modem != DATA_MODEM_V22BIS ||
            state.queued_modem != DATA_MODEM_V22BIS) {
            endpoint.Errors++;
        }
    }

    private static int rx_callback(object? userData, short[] amp, int samples) {
        data_modems_state_t state = (data_modems_state_t)userData!;
        int outSamples = data_modems_rx(state, amp, samples);
        if (_waveWriter is not null) {
            for (int i = 0; i < samples; ++i)
                WaveBuffer[2 * i] = amp[i];
        }
        check_internal_timeout(EndpointState);
        return outSamples;
    }

    private static int tx_callback(object? userData, short[] amp, int samples) {
        data_modems_state_t state = (data_modems_state_t)userData!;
        int outSamples;
        if (_automatedMode && !has_seen_call_control(EndpointState)) {
            amp.AsSpan(0, samples).Clear();
            outSamples = samples;
        } else {
            outSamples = data_modems_tx(state, amp, samples);
            if (outSamples < 0 || outSamples > samples) {
                EndpointState.Errors++;
                Console.Error.WriteLine(
                    "ERROR DUMMY_MODEM invalid TX sample count role={0} actual={1} maximum={2}",
                    dummy_modem_role(EndpointState), outSamples, samples);
                socket_harness_request_stop();
                return -1;
            }
            if (outSamples < samples)
                amp.AsSpan(outSamples, samples - outSamples).Clear();
        }

        if (_waveWriter is not null) {
            for (int i = 0; i < samples; ++i)
                WaveBuffer[2 * i + 1] = amp[i];
            _waveWriter.WriteFrames(WaveBuffer.AsSpan(0, samples * 2));
        }

        if (EndpointState.TxStopPending != 0) {
            EndpointState.TxDrainSamples += samples;
            if (EndpointState.TxDrainSamples >= DummyModemTxDrainSamples)
                socket_harness_request_stop();
        }
        check_internal_timeout(EndpointState);
        return samples;
    }

    private static void check_internal_timeout(Endpoint endpoint) {
        if (!_automatedMode || endpoint.TimeoutReported != 0 ||
            endpoint.Stopwatch.ElapsedMilliseconds < DummyModemTimeoutMilliseconds) {
            return;
        }

        endpoint.TimeoutReported = 1;
        endpoint.Errors++;
        Console.Error.WriteLine(
            "ERROR DUMMY_MODEM internal timeout role={0} received={1} checked={2} bad={3} rx_state={4} sync_match={5} frames={6} exact_frames={7} tx_frames={8} ready_seen={9} peer_ready_ack={10} ready_frames={11} ready_retries={12} data_started={13} local_mask=0x{14:X2} peer_ack=0x{15:X2} local_mutual={16} peer_mutual={17} dte_ok={18} call_control_seen={19} current_modem={20} queued_modem={21}",
            dummy_modem_role(endpoint),
            endpoint.ReceivedBytes,
            endpoint.CheckedBytes,
            endpoint.BadBytes,
            (int)endpoint.ReceiveState,
            endpoint.ReceiveSyncMatch,
            endpoint.ReceiveFramesSeen,
            endpoint.ReceiveExactFrames,
            endpoint.TxFramesCompleted,
            endpoint.ReceiveReadySeen,
            endpoint.PeerReadyAcknowledgementSeen,
            endpoint.ReceiveReadyFrames,
            endpoint.ReceiveReadyBadFrames,
            endpoint.TxDataStarted,
            endpoint.ReceiveChunkMask & 0xFFu,
            endpoint.PeerAcknowledgementMask,
            endpoint.LocalMutualSeen,
            endpoint.PeerMutualSeen,
            endpoint.DteOk,
            has_seen_call_control(endpoint) ? 1 : 0,
            _dataModemState is null ? -1 : (int)_dataModemState.current_modem,
            _dataModemState is null ? -1 : (int)_dataModemState.queued_modem);
        socket_harness_request_stop();
    }

    private static void mark_call_control_seen(Endpoint endpoint) =>
        Interlocked.Exchange(ref endpoint.CallControlSeen, 1);

    private static bool has_seen_call_control(Endpoint endpoint) =>
        Volatile.Read(ref endpoint.CallControlSeen) != 0;

    private static string dummy_modem_role(Endpoint endpoint) =>
        endpoint.CallingParty ? "caller" : "answerer";

    private static void dummy_modem_capture_snapshot(
        DiagnosticSnapshot snapshot,
        Endpoint endpoint,
        data_modems_state_t modem,
        socket_harness_state_t harness) {
        snapshot.RoleCallingParty = endpoint.CallingParty ? 1 : 0;
        snapshot.Done = _done ? 1 : 0;
        snapshot.Answered = _answered ? 1 : 0;
        snapshot.StatusEvents = endpoint.StatusEvents;
        snapshot.DteOk = endpoint.DteOk;
        snapshot.CallControlSeen = has_seen_call_control(endpoint) ? 1 : 0;
        snapshot.Errors = endpoint.Errors;
        snapshot.TimeoutReported = endpoint.TimeoutReported;
        snapshot.AtReceiveMode = (int)modem.at_state!.ReceiveMode;
        snapshot.CurrentModem = (int)modem.current_modem;
        snapshot.QueuedModem = (int)modem.queued_modem;
        snapshot.QueuedBaudRate = modem.queued_baud_rate;
        snapshot.QueuedBitRate = modem.queued_bit_rate;
        snapshot.CurrentTransmitType = modem.current_tx_type;
        snapshot.CurrentReceiveType = modem.current_rx_type;
        snapshot.ReceiveSignalPresent = modem.rx_signal_present ? 1 : 0;
        snapshot.ReceiveTrained = modem.rx_trained ? 1 : 0;
        snapshot.ReceiveFrameReceived = modem.rx_frame_received ? 1 : 0;
        snapshot.CallSamples = modem.call_samples;
        snapshot.CopyEndpoint(endpoint);
        snapshot.SocketStarted = (int)harness.started;
        snapshot.SocketClosed = (int)harness.pty_closed;
        snapshot.NetworkValid = harness.net_fd != nuint.MaxValue ? 1 : 0;
        snapshot.DteConnected = Volatile.Read(ref harness.pty.connected);
    }

    private static void dummy_modem_print_snapshot(string label, DiagnosticSnapshot snapshot) {
        string role = snapshot.RoleCallingParty != 0 ? "caller" : "answerer";
        Console.Error.WriteLine(
            "STATE DUMMY_MODEM {0} CONTROL role={1} done={2} answered={3} status_events={4} dte_ok={5} call_control_seen={6} at_rx_mode={7} errors={8} timeout_reported={9}",
            label, role, snapshot.Done, snapshot.Answered, snapshot.StatusEvents, snapshot.DteOk,
            snapshot.CallControlSeen, snapshot.AtReceiveMode, snapshot.Errors, snapshot.TimeoutReported);
        Console.Error.WriteLine(
            "STATE DUMMY_MODEM {0} TX role={1} current_modem={2} queued_modem={3} queued_baud={4} queued_bit_rate={5} current_tx_type={6} call_samples={7} frame_offset={8} chunk_index={9} frame_chunk={10} frames={11} ready_frames={12} ready_ack_frames={13} data_started={14} final_frames={15} ack_mask=0x{16:X2} flags=0x{17:X2} started_after_barrier={18} stop_pending={19} drain_samples={20}",
            label, role, snapshot.CurrentModem, snapshot.QueuedModem, snapshot.QueuedBaudRate,
            snapshot.QueuedBitRate, snapshot.CurrentTransmitType, snapshot.CallSamples,
            snapshot.TxFrameOffset, snapshot.TxChunkIndex, snapshot.TxFrameChunk,
            snapshot.TxFramesCompleted, snapshot.TxReadyFramesCompleted,
            snapshot.TxReadyAcknowledgementFramesCompleted, snapshot.TxDataStarted,
            snapshot.TxFinalFramesCompleted, snapshot.TxFrameAcknowledgementMask,
            snapshot.TxFrameFlags, snapshot.TxFrameStartedAfterBarrier,
            snapshot.TxStopPending, snapshot.TxDrainSamples);
        Console.Error.WriteLine(
            "STATE DUMMY_MODEM {0} RX role={1} current_rx_type={2} signal_present={3} trained={4} frame_received={5} received={6} checked={7} bad={8} parser_state={9} sync_match={10} header_pos={11} payload_pos={12} crc_pos={13} frame_bad={14} frames={15} exact_frames={16} ready_seen={17} peer_ready_ack={18} ready_frames={19} ready_bad_frames={20} ready_bad_bytes={21} local_mask=0x{22:X2} peer_ack=0x{23:X2} frame_complete={24} local_mutual={25} peer_mutual={26}",
            label, role, snapshot.CurrentReceiveType, snapshot.ReceiveSignalPresent,
            snapshot.ReceiveTrained, snapshot.ReceiveFrameReceived, snapshot.ReceivedBytes,
            snapshot.CheckedBytes, snapshot.BadBytes, snapshot.ReceiveState,
            snapshot.ReceiveSyncMatch, snapshot.ReceiveHeaderPosition,
            snapshot.ReceivePayloadPosition, snapshot.ReceiveCrcPosition,
            snapshot.ReceiveFrameBad, snapshot.ReceiveFramesSeen,
            snapshot.ReceiveExactFrames, snapshot.ReceiveReadySeen,
            snapshot.PeerReadyAcknowledgementSeen, snapshot.ReceiveReadyFrames,
            snapshot.ReceiveReadyBadFrames, snapshot.ReceiveReadyBadBytes,
            snapshot.ReceiveChunkMask & 0xFFu, snapshot.PeerAcknowledgementMask,
            snapshot.ReceiveFrameComplete, snapshot.LocalMutualSeen, snapshot.PeerMutualSeen);
        Console.Error.WriteLine(
            "STATE DUMMY_MODEM {0} IO role={1} socket_started={2} socket_closed={3} net_valid={4} dte_connected={5}",
            label, role, snapshot.SocketStarted, snapshot.SocketClosed,
            snapshot.NetworkValid, snapshot.DteConnected);
    }

    private static void dummy_modem_print_requirements(Endpoint endpoint, data_modems_state_t state) {
        uint fullMask = (1u << DummyModemChunkCount) - 1u;
        Console.Error.WriteLine(
            "STATE DUMMY_MODEM REQUIREMENTS role={0} expected_ready=1 actual_ready={1} expected_peer_ready_ack=1 actual_peer_ready_ack={2} expected_data_started=1 actual_data_started={3} expected_checked={4} actual_checked={5} expected_bad=0 actual_bad={6} expected_mask=0x{7:X2} actual_mask=0x{8:X2} expected_peer_ack=0x{9:X2} actual_peer_ack=0x{10:X2} expected_dte_ok=1 actual_dte_ok={11} expected_call_control=1 actual_call_control={12} expected_modem={13} actual_current={14} actual_queued={15}",
            dummy_modem_role(endpoint), endpoint.ReceiveReadySeen, endpoint.PeerReadyAcknowledgementSeen,
            endpoint.TxDataStarted, DummyModemCheckBytes, endpoint.CheckedBytes,
            endpoint.BadBytes, fullMask, endpoint.ReceiveChunkMask & 0xFFu,
            fullMask, endpoint.PeerAcknowledgementMask, endpoint.DteOk,
            has_seen_call_control(endpoint) ? 1 : 0, DATA_MODEM_V22BIS,
            (int)state.current_modem, (int)state.queued_modem);
    }

    private static void dummy_modem_print_result(Endpoint endpoint, data_modems_state_t state) {
        Console.Error.WriteLine(
            "DUMMY_MODEM RESULT role={0} received={1} checked={2} bad={3} rx_frames={4} exact_frames={5} ready_frames={6} ready_retries={7} ready_bad_bytes={8} tx_frames={9} tx_ready_frames={10} tx_ready_ack_frames={11} data_started={12} final_frames={13} local_mask=0x{14:X2} peer_ack=0x{15:X2} local_mutual={16} peer_mutual={17} dte_ok={18} call_control_seen={19} current_modem={20} queued_modem={21} errors={22}",
            dummy_modem_role(endpoint), endpoint.ReceivedBytes, endpoint.CheckedBytes, endpoint.BadBytes,
            endpoint.ReceiveFramesSeen, endpoint.ReceiveExactFrames, endpoint.ReceiveReadyFrames,
            endpoint.ReceiveReadyBadFrames, endpoint.ReceiveReadyBadBytes,
            endpoint.TxFramesCompleted, endpoint.TxReadyFramesCompleted,
            endpoint.TxReadyAcknowledgementFramesCompleted, endpoint.TxDataStarted,
            endpoint.TxFinalFramesCompleted, endpoint.ReceiveChunkMask & 0xFFu,
            endpoint.PeerAcknowledgementMask, endpoint.LocalMutualSeen,
            endpoint.PeerMutualSeen, endpoint.DteOk,
            has_seen_call_control(endpoint) ? 1 : 0, (int)state.current_modem,
            (int)state.queued_modem, endpoint.Errors);
    }

    private enum ReceiveParserState {
        Sync = 0,
        Header = 1,
        Payload = 2,
        Crc = 3
    }

    private sealed class Endpoint {
        public bool CallingParty;
        public int ReceivedBytes;
        public int CheckedBytes;
        public int BadBytes;
        public int StatusEvents;
        public int DteOk;
        public int CallControlSeen;
        public int TimeoutReported;
        public int Errors;
        public int TxFrameOffset;
        public int TxChunkIndex;
        public int TxFrameChunk;
        public int TxFramesCompleted;
        public int TxReadyFramesCompleted;
        public int TxReadyAcknowledgementFramesCompleted;
        public int TxDataStarted;
        public byte TxFrameAcknowledgementMask;
        public byte TxFrameFlags;
        public uint TxFrameCrc;
        public int TxFrameStartedAfterBarrier;
        public int TxFinalFramesCompleted;
        public int TxStopPending;
        public int TxDrainSamples;
        public ReceiveParserState ReceiveState;
        public int ReceiveSyncMatch;
        public readonly byte[] ReceiveHeader = new byte[DummyModemHeaderBytes];
        public int ReceiveHeaderPosition;
        public int ReceivePayloadPosition;
        public uint ReceiveCrc;
        public readonly byte[] ReceiveCrcBytes = new byte[DummyModemCrcBytes];
        public int ReceiveCrcPosition;
        public int ReceiveFrameBad;
        public int ReceiveFramesSeen;
        public int ReceiveExactFrames;
        public int ReceiveReadySeen;
        public int PeerReadyAcknowledgementSeen;
        public int ReceiveReadyFrames;
        public int ReceiveReadyBadFrames;
        public int ReceiveReadyBadBytes;
        public uint ReceiveChunkMask;
        public byte PeerAcknowledgementMask;
        public int ReceiveFrameComplete;
        public int LocalMutualSeen;
        public int PeerMutualSeen;
        public Stopwatch Stopwatch { get; } = new();

        public void Reset(bool callingParty) {
            CallingParty = callingParty;
            ReceivedBytes = 0;
            CheckedBytes = 0;
            BadBytes = 0;
            StatusEvents = 0;
            DteOk = 0;
            CallControlSeen = 0;
            TimeoutReported = 0;
            Errors = 0;
            TxFrameOffset = 0;
            TxChunkIndex = 0;
            TxFrameChunk = 0;
            TxFramesCompleted = 0;
            TxReadyFramesCompleted = 0;
            TxReadyAcknowledgementFramesCompleted = 0;
            TxDataStarted = 0;
            TxFrameAcknowledgementMask = 0;
            TxFrameFlags = 0;
            TxFrameCrc = 0;
            TxFrameStartedAfterBarrier = 0;
            TxFinalFramesCompleted = 0;
            TxStopPending = 0;
            TxDrainSamples = 0;
            ReceiveState = ReceiveParserState.Sync;
            ReceiveSyncMatch = 0;
            Array.Clear(ReceiveHeader);
            ReceiveHeaderPosition = 0;
            ReceivePayloadPosition = 0;
            ReceiveCrc = 0xFFFFFFFFu;
            Array.Clear(ReceiveCrcBytes);
            ReceiveCrcPosition = 0;
            ReceiveFrameBad = 0;
            ReceiveFramesSeen = 0;
            ReceiveExactFrames = 0;
            ReceiveReadySeen = 0;
            PeerReadyAcknowledgementSeen = 0;
            ReceiveReadyFrames = 0;
            ReceiveReadyBadFrames = 0;
            ReceiveReadyBadBytes = 0;
            ReceiveChunkMask = 0;
            PeerAcknowledgementMask = 0;
            ReceiveFrameComplete = 0;
            LocalMutualSeen = 0;
            PeerMutualSeen = 0;
            Stopwatch.Restart();
        }
    }

    private sealed class DiagnosticSnapshot {
        public int RoleCallingParty;
        public int Done;
        public int Answered;
        public int StatusEvents;
        public int DteOk;
        public int CallControlSeen;
        public int Errors;
        public int TimeoutReported;
        public int AtReceiveMode;
        public int CurrentModem;
        public int QueuedModem;
        public int QueuedBaudRate;
        public int QueuedBitRate;
        public int CurrentTransmitType;
        public int CurrentReceiveType;
        public int ReceiveSignalPresent;
        public int ReceiveTrained;
        public int ReceiveFrameReceived;
        public long CallSamples;
        public int TxFrameOffset;
        public int TxChunkIndex;
        public int TxFrameChunk;
        public int TxFramesCompleted;
        public int TxReadyFramesCompleted;
        public int TxReadyAcknowledgementFramesCompleted;
        public int TxDataStarted;
        public int TxFinalFramesCompleted;
        public byte TxFrameAcknowledgementMask;
        public byte TxFrameFlags;
        public int TxFrameStartedAfterBarrier;
        public int TxStopPending;
        public int TxDrainSamples;
        public int ReceivedBytes;
        public int CheckedBytes;
        public int BadBytes;
        public int ReceiveState;
        public int ReceiveSyncMatch;
        public int ReceiveHeaderPosition;
        public int ReceivePayloadPosition;
        public int ReceiveCrcPosition;
        public int ReceiveFrameBad;
        public int ReceiveFramesSeen;
        public int ReceiveExactFrames;
        public int ReceiveReadySeen;
        public int PeerReadyAcknowledgementSeen;
        public int ReceiveReadyFrames;
        public int ReceiveReadyBadFrames;
        public int ReceiveReadyBadBytes;
        public uint ReceiveChunkMask;
        public byte PeerAcknowledgementMask;
        public int ReceiveFrameComplete;
        public int LocalMutualSeen;
        public int PeerMutualSeen;
        public int SocketStarted;
        public int SocketClosed;
        public int NetworkValid;
        public int DteConnected;

        public void CopyEndpoint(Endpoint endpoint) {
            TxFrameOffset = endpoint.TxFrameOffset;
            TxChunkIndex = endpoint.TxChunkIndex;
            TxFrameChunk = endpoint.TxFrameChunk;
            TxFramesCompleted = endpoint.TxFramesCompleted;
            TxReadyFramesCompleted = endpoint.TxReadyFramesCompleted;
            TxReadyAcknowledgementFramesCompleted = endpoint.TxReadyAcknowledgementFramesCompleted;
            TxDataStarted = endpoint.TxDataStarted;
            TxFinalFramesCompleted = endpoint.TxFinalFramesCompleted;
            TxFrameAcknowledgementMask = endpoint.TxFrameAcknowledgementMask;
            TxFrameFlags = endpoint.TxFrameFlags;
            TxFrameStartedAfterBarrier = endpoint.TxFrameStartedAfterBarrier;
            TxStopPending = endpoint.TxStopPending;
            TxDrainSamples = endpoint.TxDrainSamples;
            ReceivedBytes = endpoint.ReceivedBytes;
            CheckedBytes = endpoint.CheckedBytes;
            BadBytes = endpoint.BadBytes;
            ReceiveState = (int)endpoint.ReceiveState;
            ReceiveSyncMatch = endpoint.ReceiveSyncMatch;
            ReceiveHeaderPosition = endpoint.ReceiveHeaderPosition;
            ReceivePayloadPosition = endpoint.ReceivePayloadPosition;
            ReceiveCrcPosition = endpoint.ReceiveCrcPosition;
            ReceiveFrameBad = endpoint.ReceiveFrameBad;
            ReceiveFramesSeen = endpoint.ReceiveFramesSeen;
            ReceiveExactFrames = endpoint.ReceiveExactFrames;
            ReceiveReadySeen = endpoint.ReceiveReadySeen;
            PeerReadyAcknowledgementSeen = endpoint.PeerReadyAcknowledgementSeen;
            ReceiveReadyFrames = endpoint.ReceiveReadyFrames;
            ReceiveReadyBadFrames = endpoint.ReceiveReadyBadFrames;
            ReceiveReadyBadBytes = endpoint.ReceiveReadyBadBytes;
            ReceiveChunkMask = endpoint.ReceiveChunkMask;
            PeerAcknowledgementMask = endpoint.PeerAcknowledgementMask;
            ReceiveFrameComplete = endpoint.ReceiveFrameComplete;
            LocalMutualSeen = endpoint.LocalMutualSeen;
            PeerMutualSeen = endpoint.PeerMutualSeen;
        }
    }

    private sealed class TelephonyWaveWriter : IDisposable {
        private readonly FileStream _stream;
        private readonly BinaryWriter _writer;
        private readonly int _channels;
        private readonly int _sampleRate;
        private long _sampleCount;
        private bool _disposed;

        public TelephonyWaveWriter(string path, int channels, int sampleRate) {
            _channels = channels;
            _sampleRate = sampleRate;
            _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            _writer = new BinaryWriter(_stream, Encoding.ASCII, leaveOpen: true);
            WriteHeader(0);
        }

        public void WriteFrames(ReadOnlySpan<short> samples) {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (samples.Length % _channels != 0)
                throw new ArgumentException("The sample count must contain complete frames.", nameof(samples));
            foreach (short sample in samples)
                _writer.Write(sample);
            _sampleCount += samples.Length;
        }

        public void Dispose() {
            if (_disposed)
                return;
            long dataBytes = checked(_sampleCount * sizeof(short));
            _writer.Flush();
            _stream.Position = 0;
            WriteHeader(dataBytes);
            _writer.Flush();
            _writer.Dispose();
            _stream.Dispose();
            _disposed = true;
        }

        private void WriteHeader(long dataBytes) {
            int blockAlign = checked(_channels * sizeof(short));
            int byteRate = checked(_sampleRate * blockAlign);
            _writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            _writer.Write((uint)(36 + dataBytes));
            _writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            _writer.Write(Encoding.ASCII.GetBytes("fmt "));
            _writer.Write(16u);
            _writer.Write((ushort)1);
            _writer.Write((ushort)_channels);
            _writer.Write((uint)_sampleRate);
            _writer.Write((uint)byteRate);
            _writer.Write((ushort)blockAlign);
            _writer.Write((ushort)16);
            _writer.Write(Encoding.ASCII.GetBytes("data"));
            _writer.Write((uint)dataBytes);
        }
    }
}
