/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * t31_tests.cs - complete managed port of t31_tests.c.
 *
 * Original test written by Steve Underwood.
 * Licensed under the GNU General Public License version 2.
 */

#nullable enable
using TKFaxEngine;

using System.Globalization;
using System.Text;
using TKFaxEngine.Daten.T30;
using TKFaxEngine.Daten.T38;
using static TKFaxEngine.Daten.T30.T30Api;
using static TKFaxEngine.Daten.T38.T38Core;
using ProtocolT30SupportedModems = TKFaxEngine.Daten.T30.T30SupportedModems;
using TKFaxEngineTest.Network.Test;
using g1050 = TKFaxEngineTest.Network.Test.G1050;
using g1050_state_t = TKFaxEngineTest.Network.Test.G1050State;

namespace TKFaxEngineTest.Fax.Test;

public static class t31_tests {
    private const string INPUT_FILE_NAME = "Test-Files/itu/fax/itu1.tif";
    private const string OUTPUT_FILE_NAME = "t31.tif";
    private const string OUTPUT_WAVE_FILE_NAME = "t31_tests.wav";
    private const int SAMPLE_RATE = 8_000;
    private const int SAMPLES_PER_CHUNK = 160;
    private const byte ETX = 0x03;
    private const byte DLE = 0x10;

    private enum CommandKind {
        Exchange,
        Response,
        FastResponse,
        FastSend,
        FastSendTcf,
        End
    }

    private sealed record CommandResponse(CommandKind Kind, byte[] Command, byte[] Response);

    private static readonly Encoding Latin1 = Encoding.Latin1;

    private static readonly CommandResponse[] FaxSendTestSequence =
    {
        Exchange("ATE0\r", "ATE0\r\r\nOK\r\n"),
        Exchange("AT+FCLASS=1\r", "\r\nOK\r\n"),
        Exchange("ATD123456789\r", "\r\nCONNECT\r\n"),
        Response("\xFF\x03\x40\x31\x31\x31\x31\x31\x31\x31\x31\x20\x20\x20\x20\x20\x20\x20\x20\x20\x20\x20\x20\x1E\x46\x10\x03"),
        Response("\r\nOK\r\n"),
        Exchange("AT+FRH=3\r", "\r\nCONNECT\r\n"),
        Response("\xFF\x13\x80\x05\xEE\xF8\x80\x80\x99\x80\x80\x80\x18\xE3\x91\x10\x03"),
        Response("\r\nOK\r\n"),
        Exchange("AT+FTH=3\r", "\r\nCONNECT\r\n"),
        Exchange("\xFF\x03\x43\x32\x32\x32\x32\x32\x32\x32\x32\x20\x20\x20\x20\x20\x20\x20\x20\x20\x20\x20\x20\x10\x03", "\r\nCONNECT\r\n"),
        Exchange("\xFF\x13\x83\x01\xC6\x80\x80\x80\x80\x01\x10\x03", "\r\nOK\r\n"),
        Exchange("AT+FRS=7\r", "\r\nOK\r\n"),
        Exchange("AT+FTS=8\r", "\r\nOK\r\n"),
        Exchange("AT+FTM=96\r", "\r\nCONNECT\r\n"),
        FastSendTcf("\r\nOK\r\n"),
        Exchange("AT+FRH=3\r", "\r\nCONNECT\r\n"),
        Response("\xFF\x13\x84\xEA\x7D\x10\x03"),
        Response("\r\nOK\r\n"),
        Exchange("AT+FTM=96\r", "\r\nCONNECT\r\n"),
        FastSend("\r\nOK\r\n"),
        Exchange("AT+FTS=8\r", "\r\nOK\r\n"),
        Exchange("AT+FTH=3\r", "\r\nCONNECT\r\n"),
        Exchange("\xFF\x13\x2E\x10\x03", "\r\nOK\r\n"),
        Exchange("AT+FRH=3\r", "\r\nCONNECT\r\n"),
        Response("\xFF\x13\x8C\xA2\xF1\x10\x03"),
        Response("\r\nOK\r\n"),
        Exchange("AT+FTH=3\r", "\r\nCONNECT\r\n"),
        Exchange("\xFF\x13\xFB\x10\x03", "\r\nOK\r\n"),
        Exchange("ATH0\r", "\r\nOK\r\n"),
        End()
    };

    private static readonly CommandResponse[] FaxReceiveTestSequence =
    {
        Exchange("ATE0\r", "ATE0\r\r\nOK\r\n"),
        Exchange("AT+FCLASS=1\r", "\r\nOK\r\n"),
        Response("\r\nRING\r\n"),
        Exchange("ATA\r", "\r\nCONNECT\r\n"),
        Exchange("\xFF\x03\x40\x32\x32\x32\x32\x32\x32\x32\x32\x20\x20\x20\x20\x20\x20\x20\x20\x20\x20\x20\x20\x10\x03", "\r\nCONNECT\r\n"),
        Exchange("\xFF\x13\x80\x01\xCE\xF4\x80\x80\x81\x80\x80\x80\x18\x10\x03", "\r\nOK\r\n"),
        Exchange("AT+FRH=3\r", "\r\nCONNECT\r\n"),
        Response("\xFF\x03\x43\x31\x31\x31\x31\x31\x31\x31\x31\x20\x20\x20\x20\x20\x20\x20\x20\x20\x20\x20\x20\xAA\x1F\x10\x03"),
        Response("\r\nOK\r\n"),
        Exchange("AT+FRH=3\r", "\r\nCONNECT\r\n"),
        Response("\xFF\x13\x83\x00\xC6\x74\x53\x00\x10\x03"),
        Response("\r\nOK\r\n"),
        Exchange("AT+FRM=96\r", "\r\nCONNECT\r\n"),
        FastResponse(),
        Response("\r\nNO CARRIER\r\n"),
        Exchange("AT+FTH=3\r", "\r\nCONNECT\r\n"),
        Exchange("\xFF\x13\x84\x10\x03", "\r\nOK\r\n"),
        Exchange("AT+FRM=96\r", "\r\nCONNECT\r\n"),
        FastResponse(),
        Response("\r\nNO CARRIER\r\n"),
        Exchange("AT+FRH=3\r", "\r\nCONNECT\r\n"),
        Response("\xFF\x13\x2F\x33\x66\x10\x03"),
        Response("\r\nOK\r\n"),
        Exchange("AT+FTH=3\r", "\r\nCONNECT\r\n"),
        Exchange("\xFF\x13\x8C\x10\x03", "\r\nOK\r\n"),
        Exchange("AT+FRH=3\r", "\r\nCONNECT\r\n"),
        Response("\xFF\x13\xFB\x9A\xF6\x10\x03"),
        Response("\r\nOK\r\n"),
        Exchange("ATH0\r", "\r\nOK\r\n"),
        End()
    };

    private static readonly CommandResponse[] V34FaxSendTestSequence =
    {
        Exchange("ATE0\r", "ATE0\r\r\nOK\r\n"),
        Exchange("AT+A8E=3,,\r", "\r\nOK\r\n"),
        Exchange("AT+FCLASS=1.0\r", "\r\nOK\r\n"),
        Exchange("AT+F34=14,4,2\r", "\r\nOK\r\n"),
        Exchange("ATD123456789\r", "\r\n+A8A:1\r\nOK\r\n"),
        Exchange("AT+A8M=8185D490\r", "\r\n+A8M:8185D490\r\nOK\r\n"),
        Exchange("ATO\r", "\r\n+A8J:1\r\n+F34=14,2\r\nCONNECT\r\n"),
        Response("\x10\x6B\x10\x7D\x10\x6F\xFF\x13\x80\x00\xEE\xF8\x80\x80\x91\x80\x80\x80\x18\x78\x57\x10\x03"),
        Exchange("\xFF\x13\x83\x01\xC6\x80\x80\x80\x80\x01\xFD\x13\x10\x03", "\xFF\x13\x84\xEA\x7D\x10\x03"),
        Exchange("\x10\x04", "\x10\x04\x10\x7D"),
        Exchange("\x10\x6B", "\x10\x6B\x10\x79\x10\x6F"),
        Exchange("\x10\x03", "\xFF\x13\x8C\xA2\xF1\x10\x03"),
        Exchange("\xFF\x13\xFB\x10\x03\x10\x04", "\r\nOK\r\n"),
        Exchange("ATH\r", "\r\nOK\r\n"),
        End()
    };

    private static readonly CommandResponse[] V34FaxReceiveTestSequence =
    {
        Exchange("ATE0\r", "ATE0\r\r\nOK\r\n"),
        Exchange("AT+A8E=,2,\r", "\r\nOK\r\n"),
        Exchange("AT+FCLASS=1.0\r", "\r\nOK\r\n"),
        Exchange("AT+F34=10\r", "\r\nOK\r\n"),
        Response("\r\nRING\r\n"),
        Exchange("ATA\r", "\r\n+A8M:8185D490\r\nOK\r\n"),
        Exchange("AT+A8M=8185D490;O\r", "\r\n+A8J:1\r\n+F34:10,1\r\nCONNECT\r\n"),
        Response("\x10<ctrl>\x10<p224>\x10<C12>"),
        Exchange("ATH\r", "\r\nOK\r\n"),
        End()
    };

    private static readonly CommandResponse[] V34FaxReceiveATestSequence =
    {
        Exchange("ATE0\r", "ATE0\r\r\nOK\r\n"),
        Exchange("AT+A8E=,3,\r", "\r\nOK\r\n"),
        Exchange("AT+FCLASS=1.0\r", "\r\nOK\r\n"),
        Exchange("AT+F34=10\r", "\r\nOK\r\n"),
        Response("\r\nRING\r\n"),
        Exchange("ATA\r", "\r\n+A8C:1\r\n+A8C:1\r\n"),
        Exchange("X", "\r\nOK\r\n"),
        Exchange("AT+A8E=,2,\r", "\r\n+A8M:8185D490\r\nOK\r\n"),
        Exchange("AT+A8M=8185D490\r", "\r\n+A8J:1\r\n+F34:10,1\r\nCONNECT\r\n"),
        Response("\x10<ctrl>\x10<p224>\x10<C12>"),
        Exchange("ATH\r", "\r\nOK\r\n"),
        End()
    };

    private static readonly CommandResponse[] V34FaxReceiveBTestSequence =
    {
        Exchange("ATE0\r", "ATE0\r\r\nOK\r\n"),
        Exchange("AT+A8E=,3,\r", "\r\nOK\r\n"),
        Exchange("AT+FCLASS=1.0\r", "\r\nOK\r\n"),
        Exchange("AT+F34=10\r", "\r\nOK\r\n"),
        Response("\r\nRING\r\n"),
        Exchange("ATA\r", "\r\nA8I:81\r\n"),
        Response("A8I:81\r\n"),
        Exchange("X", "\r\nOK\r\n"),
        Exchange("AT+A8E=,2,\r", "\r\n+A8M:8185D490\r\nOK\r\n"),
        End()
    };

    private static g1050_state_t? _pathAToB;
    private static g1050_state_t? _pathBToA;
    private static double _when;
    private static string? _decodeTestFile;
    private static int _countdown;
    private static bool _answered;
    private static bool _kick;
    private static bool _dled;
    private static bool _done;
    private static bool _sequenceTerminated;
    private static int _testSequencePointer;
    private static CommandResponse[] _faxTestSequence = FaxReceiveTestSequence;
    private static t31_state_t? _t31State;
    private static readonly List<byte> ResponseBuffer = new(1024);

    public static int main(string[] testArguments) {
        try {
            return tkfx_t31_tests_body_main(testArguments);
        } catch (T31TestsExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_t31_tests_body_main(string[] testArguments) {
        bool logAudio = false;
        bool testSending = false;
        bool t38Mode = false;
        bool useGui = false;
        int g1050ModelNumber = 0;
        int g1050SpeedPatternNumber = 1;
        _decodeTestFile = null;

        for (int index = 0; index < testArguments.Length; index++) {
            string option = testArguments[index];
            switch (option) {
                case "-d":
                    _decodeTestFile = NextArgument(testArguments, ref index, option);
                    break;
                case "-g":
                    Console.Error.WriteLine("Graphical monitoring not available");
                    Exit(2);
                    break;
                case "-l":
                    logAudio = true;
                    break;
                case "-M":
                    g1050ModelNumber = NextArgument(testArguments, ref index, option)[0] - 'A' + 1;
                    break;
                case "-r":
                    testSending = false;
                    break;
                case "-S":
                    g1050SpeedPatternNumber = ParseInt(NextArgument(testArguments, ref index, option));
                    break;
                case "-s":
                    testSending = true;
                    break;
                case "-t":
                    t38Mode = true;
                    break;
                default:
                    Exit(2);
                    break;
            }
        }

        int result = RunT30Test(t38Mode, useGui, logAudio, testSending, g1050ModelNumber, g1050SpeedPatternNumber);
        if (result < 0)
            return 2;

        Console.WriteLine("Tests passed");
        return 0;
    }

    private static int RunT30Test(
        bool t38Mode,
        bool useGui,
        bool logAudio,
        bool testSending,
        int g1050ModelNumber,
        int g1050SpeedPatternNumber) {
        _ = useGui;
        const int t38Version = 1;
        const bool withoutPacing = false;
        const bool useTep = false;

        ResetRuntimeState(testSending);

        using WaveFileWriter? waveWriter = logAudio
            ? new WaveFileWriter(OUTPUT_WAVE_FILE_NAME, SAMPLE_RATE, 2)
            : null;

        _pathAToB = G1050.g1050_init(g1050ModelNumber, g1050SpeedPatternNumber, 100, 33)
            ?? throw new InvalidOperationException("Failed to start IP network path model A to B.");
        _pathBToA = G1050.g1050_init(g1050ModelNumber, g1050SpeedPatternNumber, 100, 33)
            ?? throw new InvalidOperationException("Failed to start IP network path model B to A.");

        FaxState? faxEndpoint = null;
        T38TerminalState? t38Terminal = null;
        T30State t30;

        if (t38Mode) {
            t38Terminal = T38Terminal.t38_terminal_init(
                null,
                !testSending,
                T38TxPacketHandler,
                null)
                ?? throw new InvalidOperationException("Cannot start the T.38 channel.");
            t30 = T38Terminal.t38_terminal_get_t30_state(t38Terminal);
            t38_set_t38_version(T38Terminal.t38_terminal_get_t38_core_state(t38Terminal), t38Version);
            T38Terminal.t38_terminal_set_config(t38Terminal, withoutPacing ? 1 : 0);
            T38Terminal.t38_terminal_set_tep_mode(t38Terminal, useTep);
        } else {
            faxEndpoint = new FaxState(callingParty: !testSending);
            t30 = faxEndpoint.T30;
        }

        if (testSending) {
            t30_set_rx_file(t30, OUTPUT_FILE_NAME, -1);
            _faxTestSequence = FaxSendTestSequence;
            _countdown = 0;
        } else {
            t30_set_tx_file(t30, INPUT_FILE_NAME, -1, -1);
            _faxTestSequence = FaxReceiveTestSequence;
            _countdown = 250;
        }

        t30_set_tx_ident(t30, "11111111");
        t30_set_supported_modems(
            t30,
            (int)(ProtocolT30SupportedModems.V27Ter | ProtocolT30SupportedModems.V29 | ProtocolT30SupportedModems.V17));
        t30_set_phase_b_handler(t30, PhaseBHandler, t30);
        t30_set_phase_d_handler(t30, PhaseDHandler, t30);
        t30_set_phase_e_handler(t30, PhaseEHandler, t30);
        t30.Logging.Sink = (_, text) => Console.Error.WriteLine($"T.30: {text}");
        _t31State = t31.t31_init(
            null,
            AtTxHandler,
            null,
            ModemCallControl,
            null,
            T31TxPacketHandler,
            null)
            ?? throw new InvalidOperationException("Cannot start the T.31 channel.");
        TKFaxEngine.LoggingApi.span_log_set_level(
            t31.t31_get_logging_state(_t31State),
            TKFaxEngine.LoggingApi.SPAN_LOG_FLOW);
        TKFaxEngine.LoggingApi.span_log_set_message_handler(
            t31.t31_get_logging_state(_t31State),
            (_, _, text) => Console.Error.WriteLine($"T.31: {text}"),
            null);

        if (t38Mode) {
            t31.t31_set_mode(_t31State, true);
            t38_set_t38_version(t31.t31_get_t38_core_state(_t31State), t38Version);
        }

        _kick = true;
        var t30Samples = new short[SAMPLES_PER_CHUNK];
        var t31Samples = new short[SAMPLES_PER_CHUNK];
        var stereo = new short[2 * SAMPLES_PER_CHUNK];
        var packet = new byte[1024];
        int loopChunks = 0;
        const int maximumLoopChunks = 8_000 * 60 / SAMPLES_PER_CHUNK * 10;

        while (!_done && loopChunks++ < maximumLoopChunks) {
            HandleCallSetup();
            DriveScript();
            DriveFastTransmit();

            if (t38Mode) {
                PumpNetwork(_pathAToB!, t31.t31_get_t38_core_state(_t31State), packet);
                PumpNetwork(_pathBToA!, T38Terminal.t38_terminal_get_t38_core_state(t38Terminal!), packet);
                _when += (double)SAMPLES_PER_CHUNK / SAMPLE_RATE;
                T38Terminal.t38_terminal_send_timeout(t38Terminal!, SAMPLES_PER_CHUNK);
                t31.t31_t38_send_timeout(_t31State, SAMPLES_PER_CHUNK);
            } else {
                int t30Length = faxEndpoint!.Transmit(t30Samples, SAMPLES_PER_CHUNK);
                Array.Clear(t30Samples, t30Length, SAMPLES_PER_CHUNK - t30Length);
                t31.t31_rx(_t31State, t30Samples, t30Samples.Length);

                int t31Length = t31.t31_tx(_t31State, t31Samples, t31Samples.Length);
                Array.Clear(t31Samples, t31Length, SAMPLES_PER_CHUNK - t31Length);
                faxEndpoint.Receive(t31Samples, SAMPLES_PER_CHUNK);

                if (waveWriter is not null) {
                    for (int i = 0; i < SAMPLES_PER_CHUNK; i++) {
                        stereo[2 * i] = t30Samples[i];
                        stereo[2 * i + 1] = t31Samples[i];
                    }
                    waveWriter.Write(stereo);
                }
            }

        }

        _ = G1050.g1050_free(_pathAToB!);
        _ = G1050.g1050_free(_pathBToA!);
        _pathAToB = null;
        _pathBToA = null;
        if (t38Terminal is not null)
            T38Terminal.t38_terminal_free(t38Terminal);
        faxEndpoint?.Dispose();
        t31.t31_free(_t31State);
        _t31State = null;

        if (!_done || !_sequenceTerminated) {
            Console.WriteLine("Tests failed");
            return -1;
        }

        return 0;
    }

    private static void ResetRuntimeState(bool testSending) {
        _when = 0.0;
        _answered = false;
        _kick = false;
        _dled = false;
        _done = false;
        _sequenceTerminated = false;
        _testSequencePointer = 0;
        _faxTestSequence = testSending ? FaxSendTestSequence : FaxReceiveTestSequence;
        ResponseBuffer.Clear();
        _fastSend = false;
        _fastSendTcf = false;
        _fastBlocks = 0;
    }

    private static int PhaseBHandler(object? userData, int result) {
        var state = (T30State)userData!;
        Console.WriteLine($"A: Phase B handler on channel A - (0x{result:X}) {T30Logging.t30_frametype((byte)result)}");
        LogRxParameters(state, "A: Phase B");
        return (int)T30Error.Ok;
    }

    private static int PhaseDHandler(object? userData, int result) {
        var state = (T30State)userData!;
        Console.WriteLine($"A: Phase D handler on channel A - (0x{result:X}) {T30Logging.t30_frametype((byte)result)}");
        LogPageTransferStatistics(state, "A: Phase D");
        LogTxParameters(state, "A: Phase D");
        LogRxParameters(state, "A: Phase D");
        return (int)T30Error.Ok;
    }

    private static void PhaseEHandler(object? userData, int result) {
        var state = (T30State)userData!;
        Console.WriteLine("Phase E handler on channel A");
        LogFinalTransferStatistics(state, "A: Phase E");
        LogTxParameters(state, "A: Phase E");
        LogRxParameters(state, "A: Phase E");
    }

    private static int ModemCallControl(t31_state_t state, object? userData, int operation, string? number) {
        _ = state;
        _ = userData;
        Console.WriteLine($"\nModem control - {AtModemControlToString(operation)}{(number is null ? string.Empty : " " + number)}");
        switch ((AtModemControlOperation)operation) {
            case AtModemControlOperation.Call:
                t31.t31_call_event(_t31State!, (int)AtCallEvent.Connected);
                break;
            case AtModemControlOperation.Answer:
                _answered = true;
                break;
            case AtModemControlOperation.Hangup:
                _done = true;
                break;
            case AtModemControlOperation.OffHook:
                break;
        }
        return 0;
    }

    private static int AtTxHandler(object? userData, ReadOnlySpan<byte> data) {
        _ = userData;
        ReadOnlySpan<byte> bytes = data;
        int index = 0;
        CommandResponse current = _faxTestSequence[_testSequencePointer];

        if (current.Kind == CommandKind.FastResponse) {
            for (; index < bytes.Length; index++) {
                byte value = bytes[index];
                if (_dled) {
                    if (value == ETX) {
                        Console.WriteLine("\nFast data ended");
                        ResponseBuffer.Clear();
                        AdvanceSequence();
                        index++;
                        break;
                    }
                    _dled = false;
                } else if (value == DLE) {
                    _dled = true;
                }
            }

            if (index >= bytes.Length)
                return 0;
            current = _faxTestSequence[_testSequencePointer];
        }

        for (; index < bytes.Length; index++) {
            ResponseBuffer.Add(bytes[index]);
            Console.Write((char)bytes[index]);
        }

        Console.Write("Expected ");
        PrintHex(current.Response.AsSpan(0, Math.Min(ResponseBuffer.Count, current.Response.Length)));
        Console.WriteLine();
        Console.Write("Response ");
        PrintHex(CollectionsMarshalAsSpan(ResponseBuffer));
        Console.WriteLine();
        Console.WriteLine($"Match {ResponseBuffer.Count} against {current.Response.Length}");

        if (ResponseBuffer.Count >= current.Response.Length &&
            CollectionsMarshalAsSpan(ResponseBuffer).Slice(0, current.Response.Length).SequenceEqual(current.Response)) {
            Console.WriteLine("\nMatched");
            ResponseBuffer.Clear();
            AdvanceSequence();
        }
        return 0;
    }

    private static void AdvanceSequence() {
        _testSequencePointer++;
        if (_testSequencePointer >= _faxTestSequence.Length || _faxTestSequence[_testSequencePointer].Kind == CommandKind.End) {
            _sequenceTerminated = true;
            return;
        }

        if (_faxTestSequence[_testSequencePointer].Kind is CommandKind.Exchange or CommandKind.FastSend or CommandKind.FastSendTcf)
            _kick = true;
        else
            _dled = false;
    }

    private static int T38TxPacketHandler(T38CoreState state, object? userData, ReadOnlyMemory<byte> packet, int count) {
        _ = userData;
        for (int i = 0; i < count; i++) {
            if (G1050.g1050_put(_pathAToB!, packet.ToArray(), packet.Length, state.TxSequenceNumber, _when) < 0)
                Console.WriteLine($"Lost packet {state.TxSequenceNumber}");
        }
        return 0;
    }

    private static int T31TxPacketHandler(T38CoreState state, object? userData, ReadOnlyMemory<byte> packet, int count) {
        _ = userData;
        for (int i = 0; i < count; i++) {
            if (G1050.g1050_put(_pathBToA!, packet.ToArray(), packet.Length, state.TxSequenceNumber, _when) < 0)
                Console.WriteLine($"Lost packet {state.TxSequenceNumber}");
        }
        return 0;
    }

    private static void PumpNetwork(g1050_state_t path, T38CoreState destination, byte[] packet) {
        int sequenceNumber = 0;
        double transmitTime = 0;
        double receiveTime = 0;
        while (true) {
            int length = g1050.g1050_get(
                path,
                packet,
                packet.Length,
                _when,
                out sequenceNumber,
                out transmitTime,
                out receiveTime);
            if (length < 0)
                break;
            t38_core_rx_ifp_packet(destination, packet, length, checked((ushort)sequenceNumber));
        }
    }

    private static void HandleCallSetup() {
        if (_countdown == 0)
            return;

        if (_answered) {
            _countdown = 0;
            t31.t31_call_event(_t31State!, (int)AtCallEvent.Answered);
        } else if (--_countdown == 0) {
            t31.t31_call_event(_t31State!, (int)AtCallEvent.Alerting);
            _countdown = 250;
        }
    }

    private static void DriveScript() {
        if (!_kick || _sequenceTerminated)
            return;

        _kick = false;
        CommandResponse step = _faxTestSequence[_testSequencePointer];
        switch (step.Kind) {
            case CommandKind.Exchange:
                if (step.Command.Length > 0) {
                    Console.WriteLine(Latin1.GetString(step.Command));
                    t31.t31_at_rx(_t31State!, step.Command, step.Command.Length);
                }
                break;
            case CommandKind.FastSend:
            case CommandKind.FastSendTcf:
                _fastSend = true;
                _fastSendTcf = step.Kind == CommandKind.FastSendTcf;
                _fastBlocks = 100;
                Console.WriteLine(_fastSendTcf ? "Fast send TCF" : "Fast send image");
                break;
        }
    }

    private static bool _fastSend;
    private static bool _fastSendTcf;
    private static int _fastBlocks;

    private static void DriveFastTransmit() {
        if (!_fastSend)
            return;

        var fastBuffer = new byte[36];
        if (_fastSendTcf) {
            if (_fastBlocks == 1) {
                fastBuffer[34] = DLE;
                fastBuffer[35] = ETX;
            }
        } else if (_fastBlocks > 1) {
            for (int i = 0; i < fastBuffer.Length; i += 4) {
                fastBuffer[i] = 0x00;
                fastBuffer[i + 1] = 0x80;
                fastBuffer[i + 2] = 0xB2;
                fastBuffer[i + 3] = 0x01;
            }
        } else {
            for (int i = 0; i < fastBuffer.Length; i += 3) {
                fastBuffer[i] = 0x00;
                fastBuffer[i + 1] = 0x08;
                fastBuffer[i + 2] = 0x80;
            }
            fastBuffer[34] = DLE;
            fastBuffer[35] = ETX;
        }

        t31.t31_at_rx(_t31State!, fastBuffer, fastBuffer.Length);
        if (--_fastBlocks == 0)
            _fastSend = false;
    }

    private static void LogPageTransferStatistics(T30State state, string tag) {
        var statistics = new T30Statistics();
        T30.t30_get_transfer_statistics(state, statistics);
        Console.WriteLine($"{tag}: pages tx={statistics.PagesTransmitted} rx={statistics.PagesReceived} bit-rate={statistics.BitRate}");
    }

    private static void LogFinalTransferStatistics(T30State state, string tag) {
        var statistics = new T30Statistics();
        T30.t30_get_transfer_statistics(state, statistics);
        Console.WriteLine($"{tag}: status={(int)statistics.CurrentStatus} pages tx={statistics.PagesTransmitted} rx={statistics.PagesReceived}");
    }

    private static void LogTxParameters(T30State state, string tag) {
        Console.WriteLine($"{tag}: tx ident='{state.TxInfo.Ident}' page={state.TxPageNumber} modem={state.CurrentTxType}");
    }

    private static void LogRxParameters(T30State state, string tag) {
        Console.WriteLine($"{tag}: rx ident='{state.RxInfo.Ident}' page={state.RxPageNumber} modem={state.CurrentRxType}");
    }

    private static CommandResponse Exchange(string command, string response) =>
        new(CommandKind.Exchange, B(command), B(response));

    private static CommandResponse Response(string response) =>
        new(CommandKind.Response, Array.Empty<byte>(), B(response));

    private static CommandResponse FastResponse() =>
        new(CommandKind.FastResponse, Array.Empty<byte>(), Array.Empty<byte>());

    private static CommandResponse FastSend(string response) =>
        new(CommandKind.FastSend, Array.Empty<byte>(), B(response));

    private static CommandResponse FastSendTcf(string response) =>
        new(CommandKind.FastSendTcf, Array.Empty<byte>(), B(response));

    private static CommandResponse End() =>
        new(CommandKind.End, Array.Empty<byte>(), Array.Empty<byte>());

    private static byte[] B(string value) => Latin1.GetBytes(value);

    private static string NextArgument(string[] testArguments, ref int index, string option) {
        if (++index >= testArguments.Length)
            throw new ArgumentException($"Missing value for {option}.");
        return testArguments[index];
    }

    private static int ParseInt(string value) =>
        int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static void PrintHex(ReadOnlySpan<byte> bytes) {
        foreach (byte value in bytes)
            Console.Write($"{value:X2} ");
    }

    private static ReadOnlySpan<byte> CollectionsMarshalAsSpan(List<byte> bytes) => bytes.ToArray();

    private static string AtModemControlToString(int operation) =>
        AtInterpreterApi.at_modem_control_to_str(operation);

    private static void Exit(int exitCode) => throw new T31TestsExitException(exitCode);

    private sealed class T31TestsExitException : Exception {
        public T31TestsExitException(int exitCode) => ExitCode = exitCode;
        public int ExitCode { get; }
    }

    private sealed class WaveFileWriter : IDisposable {
        private readonly FileStream _stream;
        private readonly int _sampleRate;
        private readonly short _channels;
        private int _dataLength;

        public WaveFileWriter(string path, int sampleRate, short channels) {
            _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            _sampleRate = sampleRate;
            _channels = channels;
            WriteHeader(0);
        }

        public void Write(ReadOnlySpan<short> samples) {
            Span<byte> bytes = stackalloc byte[2];
            foreach (short sample in samples) {
                bytes[0] = (byte)sample;
                bytes[1] = (byte)(sample >> 8);
                _stream.Write(bytes);
                _dataLength += 2;
            }
        }

        public void Dispose() {
            _stream.Position = 0;
            WriteHeader(_dataLength);
            _stream.Dispose();
        }

        private void WriteHeader(int dataLength) {
            using var writer = new BinaryWriter(_stream, Encoding.ASCII, leaveOpen: true);
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataLength);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(_channels);
            writer.Write(_sampleRate);
            writer.Write(_sampleRate * _channels * sizeof(short));
            writer.Write((short)(_channels * sizeof(short)));
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataLength);
        }
    }
}
