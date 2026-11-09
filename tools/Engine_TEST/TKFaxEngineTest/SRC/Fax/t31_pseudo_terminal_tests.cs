/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * t31_pseudo_terminal_tests.cs - complete managed port of
 * t31_pseudo_terminal_tests.c.
 *
 * Original test written by Steve Underwood.
 * Licensed under the GNU General Public License version 2.
 */

#nullable enable
using TKFaxEngine;

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using TKFaxEngine.Daten.T30;
using TKFaxEngine.Daten.T38;
using TKFaxEngineTest.Network.Test;
using TKFaxEngineTest.System;
using g1050 = TKFaxEngineTest.Network.Test.G1050;
using g1050_state_t = TKFaxEngineTest.Network.Test.G1050State;
using static TKFaxEngine.Daten.T30.T30Api;
using static TKFaxEngine.Daten.T38.T38Core;
using static TKFaxEngineTest.System.pseudo_terminals;
using ProtocolT30SupportedModems = TKFaxEngine.Daten.T30.T30SupportedModems;

namespace TKFaxEngineTest.Fax.Test;

public static class t31_pseudo_terminal_tests {
    private const string INPUT_FILE_NAME = "Test-Files/itu/fax/itutests.tif";
    private const string OUTPUT_FILE_NAME = "t31_pseudo_terminal.tif";
    private const string OUTPUT_WAVE_FILE_NAME = "t31_tests.wav";
    private const int SAMPLE_RATE = 8_000;
    private const int SAMPLES_PER_CHUNK = 160;
    private const int FAST_IDLE_TIMEOUT_MS = 10_000;
    private const byte DLE = 0x10;
    private const byte ETX = 0x03;
    private const int MODEM_POLL_READ = 1 << 0;
    private const int MODEM_POLL_WRITE = 1 << 1;
    private const int MODEM_POLL_ERROR = 1 << 2;

    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint OPEN_EXISTING = 3;
    private const int ERROR_PIPE_BUSY = 231;
    private const int ERROR_FILE_NOT_FOUND = 2;
    private const uint WAIT_OBJECT_0 = 0;
    private static readonly nint INVALID_HANDLE_VALUE = new(-1);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern nint CreateFileA(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        nint lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        nint hTemplateFile);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WaitNamedPipeA(string lpNamedPipeName, uint nTimeOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern unsafe bool ReadFile(
        nint hFile,
        byte* lpBuffer,
        uint nNumberOfBytesToRead,
        out uint lpNumberOfBytesRead,
        nint lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern unsafe bool WriteFile(
        nint hFile,
        byte* lpBuffer,
        uint nNumberOfBytesToWrite,
        out uint lpNumberOfBytesWritten,
        nint lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekNamedPipe(
        nint hNamedPipe,
        nint lpBuffer,
        uint nBufferSize,
        nint lpBytesRead,
        out uint lpTotalBytesAvail,
        nint lpBytesLeftThisMessage);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(nint hHandle, uint dwMilliseconds);

    private enum DteStepKind {
        Exchange,
        Expect,
        FastExpect,
        End
    }

    private sealed record DteStep(
        DteStepKind Kind,
        byte[] Command,
        byte[] Expected,
        string Name);

    private sealed record t31_diag_snapshot_t(
        int done,
        int answered,
        int phase_e_seen,
        int phase_e_result,
        int dte_success,
        int dte_errors,
        int loop_chunks,
        int dte_thread_state,
        int dte_step_index,
        int dte_step_kind,
        int dte_command_len,
        int dte_expected_len,
        int dte_fast_payload_bytes,
        int dte_fast_idle_ms,
        int dte_reader_buffered,
        int dte_pipe_connected,
        int dte_pipe_closed,
        int dte_to_engine_write_calls,
        int dte_to_engine_write_bytes,
        int engine_to_dte_write_calls,
        int engine_to_dte_write_bytes,
        int engine_to_dte_last_len,
        int t31_present,
        int t30_present);

    private static readonly Encoding Latin1 = Encoding.Latin1;

    private static readonly DteStep[] FaxReceiveDteSteps =
    {
        Exchange("ATE0\r", "ATE0\r\r\nOK\r\n", "disable echo"),
        Exchange("AT+FCLASS=1\r", "\r\nOK\r\n", "select class 1"),
        Expect("\r\nRING\r\n", "incoming ring"),
        Exchange("ATA\r", "\r\nCONNECT\r\n", "answer"),
        Exchange("\xFF\x03\x40\x32\x32\x32\x32\x32\x32\x32\x32\x20\x20\x20\x20\x20\x20\x20\x20\x20\x20\x20\x20\x10\x03",
                 "\r\nCONNECT\r\n", "send CSI"),
        Exchange("\xFF\x13\x80\x01\xCE\xF4\x80\x80\x81\x80\x80\x80\x18\x10\x03",
                 "\r\nOK\r\n", "send DIS"),
        Exchange("AT+FRH=3\r", "\r\nCONNECT\r\n", "receive TSI"),
        Expect("\xFF\x03\x43\x31\x31\x31\x31\x31\x31\x31\x31\x20\x20\x20\x20\x20\x20\x20\x20\x20\x20\x20\x20\xAA\x1F\x10\x03", "TSI frame"),
        Expect("\r\nOK\r\n", "TSI complete"),
        Exchange("AT+FRH=3\r", "\r\nCONNECT\r\n", "receive DCS"),
        Expect("\xFF\x13\x83\x00\xC6\x74\x53\x00\x10\x03", "DCS frame"),
        Expect("\r\nOK\r\n", "DCS complete"),
        Exchange("AT+FRM=96\r", "\r\nCONNECT\r\n", "receive TCF"),
        Fast("TCF data"),
        Expect("\r\nNO CARRIER\r\n", "TCF complete"),
        Exchange("AT+FTH=3\r", "\r\nCONNECT\r\n", "send CFR"),
        Exchange("\xFF\x13\x84\x10\x03", "\r\nOK\r\n", "CFR frame"),
        Exchange("AT+FRM=96\r", "\r\nCONNECT\r\n", "receive page"),
        Fast("page data"),
        Expect("\r\nNO CARRIER\r\n", "page complete"),
        Exchange("AT+FRH=3\r", "\r\nCONNECT\r\n", "receive EOP"),
        Expect("\xFF\x13\x2F\x33\x66\x10\x03", "EOP frame"),
        Expect("\r\nOK\r\n", "EOP complete"),
        Exchange("AT+FTH=3\r", "\r\nCONNECT\r\n", "send MCF"),
        Exchange("\xFF\x13\x8C\x10\x03", "\r\nOK\r\n", "MCF frame"),
        Exchange("AT+FRH=3\r", "\r\nCONNECT\r\n", "receive DCN"),
        Expect("\xFF\x13\xFB\x9A\xF6\x10\x03", "DCN frame"),
        Expect("\r\nOK\r\n", "DCN complete"),
        Exchange("ATH0\r", "\r\nOK\r\n", "hangup"),
        new(DteStepKind.End, Array.Empty<byte>(), Array.Empty<byte>(), "end")
    };

    private static g1050_state_t? _pathAToB;
    private static g1050_state_t? _pathBToA;
    private static double _when;
    private static t31_state_t? _t31State;
    private static pseudo_terminal_state_t? _terminal;
    private static int _answered;
    private static bool _done;
    private static bool _phaseESeen;
    private static int _phaseEResult = -1;
    private static int _countdown;
    private static bool _autoDteMode;

    private static int _dteSuccess;
    private static int _dteErrors;
    private static int _dteThreadState;
    private static int _dteStepIndex = -1;
    private static int _dteStepKind = -1;
    private static int _dteCommandLength;
    private static int _dteExpectedLength;
    private static int _dteFastPayloadBytes;
    private static int _dteFastIdleMilliseconds;
    private static int _dteReaderBuffered;
    private static int _pipeConnected;
    private static int _pipeClosed;
    private static int _dteToEngineWriteCalls;
    private static int _dteToEngineWriteBytes;
    private static int _engineToDteWriteCalls;
    private static int _engineToDteWriteBytes;
    private static int _engineToDteLastLength;
    private static int _t31Ready;

    public static int main(string[] testArguments) {
        try {
            return tkfx_t31_pseudo_terminal_tests_body_main(testArguments);
        } catch (T31PseudoTerminalExitException exception) {
            return exception.ExitCode;
        }
    }

    public static int tkfx_t31_pseudo_terminal_tests_body_main(string[] testArguments) {
        bool logAudio = false;
        bool t38Mode = false;
        bool testSending = false;
        bool useEcm = false;
        bool useGui = false;
        int g1050ModelNumber = 0;
        int g1050SpeedPatternNumber = 1;
        string? decodeTestFile = null;
        _autoDteMode = false;

        auto_dte_reset_result();
        Console.Error.WriteLine(
            "STATE T31 BUILD revision=managed source=t31_pseudo_terminal_tests.c " +
            "fast_timeout_mode=inactivity one_page_auto=1 completion_barrier=phase_e_plus_dte");

        for (int index = 0; index < testArguments.Length; index++) {
            string option = testArguments[index];
            switch (option) {
                case "-A":
                    _autoDteMode = true;
                    break;
                case "-d":
                    decodeTestFile = next_argument(testArguments, ref index, option);
                    break;
                case "-e":
                    useEcm = true;
                    break;
                case "-g":
                    useGui = true;
                    Console.Error.WriteLine("Graphical monitoring not available");
                    exit_test(2);
                    break;
                case "-l":
                    logAudio = true;
                    break;
                case "-M":
                    g1050ModelNumber = next_argument(testArguments, ref index, option)[0] - 'A' + 1;
                    break;
                case "-r":
                    testSending = false;
                    break;
                case "-S":
                    g1050SpeedPatternNumber = int.Parse(
                        next_argument(testArguments, ref index, option),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture);
                    break;
                case "-s":
                    testSending = true;
                    break;
                case "-t":
                    t38Mode = true;
                    break;
                default:
                    exit_test(2);
                    break;
            }
        }

        pseudo_terminal_state_t? terminal = pseudo_terminal_init(new pseudo_terminal_state_t());
        if (terminal is null) {
            Console.Error.WriteLine("Cannot initialize pseudo terminal");
            return 2;
        }
        if (pseudo_terminal_listen_external(terminal) < 0) {
            Console.Error.WriteLine("Cannot expose the Windows named-pipe DTE endpoint");
            pseudo_terminal_release(terminal);
            return 2;
        }
        _terminal = terminal;
        Console.Error.WriteLine($"T.31 DTE endpoint: {terminal.devlink ?? "(unknown)"}");

        Task<int>? automaticDte = null;
        if (_autoDteMode)
            automaticDte = Task.Run(() => automatic_dte_thread(terminal.devlink!));

        int testResult = t30_tests(
            t38Mode,
            useEcm,
            useGui,
            logAudio,
            testSending,
            g1050ModelNumber,
            g1050SpeedPatternNumber,
            decodeTestFile);

        if (automaticDte is not null) {
            if (!automaticDte.Wait(TimeSpan.FromSeconds(10))) {
                Console.Error.WriteLine("ERROR T31_DTE thread completion timeout");
                auto_dte_record_error();
            } else if (automaticDte.Result != 0) {
                Console.Error.WriteLine($"ERROR T31_DTE thread result={automaticDte.Result}");
                auto_dte_record_error();
            }
        }

        if (pseudo_terminal_release(terminal) != 0)
            auto_dte_record_error();
        _terminal = null;
        Interlocked.Exchange(ref _pipeClosed, 1);

        if (testResult != 0 || t31_diag_atomic_read(ref _dteErrors) != 0 || (_autoDteMode && t31_diag_atomic_read(ref _dteSuccess) == 0))
            return 2;

        Console.WriteLine("Tests passed");
        return 0;
    }

    private static int t30_tests(
        bool t38Mode,
        bool useEcm,
        bool useGui,
        bool logAudio,
        bool testSending,
        int g1050ModelNumber,
        int g1050SpeedPatternNumber,
        string? decodeTestFile) {
        _ = useGui;
        _ = decodeTestFile;
        const int t38Version = 1;
        const bool withoutPacing = false;
        const bool useTep = false;

        _when = 0.0;
        _answered = 0;
        _done = false;
        _phaseESeen = false;
        _phaseEResult = -1;
        _countdown = testSending ? 0 : 250;

        using WaveFileWriter? waveWriter = logAudio
            ? new WaveFileWriter(OUTPUT_WAVE_FILE_NAME, SAMPLE_RATE, 2)
            : null;

        _pathAToB = g1050.g1050_init(g1050ModelNumber, g1050SpeedPatternNumber, 100, 33)
            ?? throw new InvalidOperationException("Failed to start IP network path model A to B.");
        _pathBToA = g1050.g1050_init(g1050ModelNumber, g1050SpeedPatternNumber, 100, 33)
            ?? throw new InvalidOperationException("Failed to start IP network path model B to A.");

        FaxState? faxEndpoint = null;
        T38TerminalState? t38Terminal = null;
        T30State t30;

        if (t38Mode) {
            t38Terminal = T38Terminal.t38_terminal_init(
                null,
                !testSending,
                t38_tx_packet_handler,
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

        if (testSending)
            t30_set_rx_file(t30, OUTPUT_FILE_NAME, -1);
        else
            t30_set_tx_file(t30, INPUT_FILE_NAME, _autoDteMode ? 0 : -1, _autoDteMode ? 0 : -1);

        t30_set_ecm_capability(t30, useEcm);
        t30_set_tx_ident(t30, "11111111");
        t30_set_supported_modems(
            t30,
            (int)(ProtocolT30SupportedModems.V27Ter | ProtocolT30SupportedModems.V29 | ProtocolT30SupportedModems.V17));
        t30_set_phase_b_handler(t30, phase_b_handler, t30);
        t30_set_phase_d_handler(t30, phase_d_handler, t30);
        t30_set_phase_e_handler(t30, phase_e_handler, t30);
        t30.Logging.Sink = (_, text) => Console.Error.WriteLine($"T.30: {text}");
        _t31State = t31.t31_init(
            null,
            at_tx_handler,
            _terminal,
            t31_call_control,
            _terminal,
            t31_tx_packet_handler,
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

        if (_autoDteMode)
            Interlocked.Exchange(ref _t31Ready, 1);

        var t30Samples = new short[SAMPLES_PER_CHUNK];
        var t31Samples = new short[SAMPLES_PER_CHUNK];
        var stereo = new short[2 * SAMPLES_PER_CHUNK];
        var packet = new byte[1024];
        var dteBuffer = new byte[1024];
        int loopChunks = 0;

        while (!_done) {
            if (_autoDteMode && _phaseESeen && t31_diag_atomic_read(ref _dteSuccess) != 0) {
                _done = true;
                Console.Error.WriteLine(
                    $"STATE T31 COMPLETION barrier=passed phase_e_result={_phaseEResult} " +
                    $"dte_success=1 chunks={loopChunks}");
                break;
            }

            if (++loopChunks > 3000) {
                Console.Error.WriteLine(
                    $"ERROR T31 pseudo-terminal internal timeout chunks={loopChunks} " +
                    $"answered={_answered} dte_success={t31_diag_atomic_read(ref _dteSuccess)} phase_e_seen={_phaseESeen}");
                auto_dte_record_error();
                break;
            }

            handle_call_setup();
            int pollResult = modem_wait_sock(_terminal!, 20, MODEM_POLL_READ);
            if ((pollResult & MODEM_POLL_READ) != 0) {
                uint readBytes;
                unsafe {
                    fixed (byte* raw = dteBuffer) {
                        if (!ReadFile(_terminal!.master_fd, raw, (uint)dteBuffer.Length, out readBytes, 0))
                            readBytes = 0;
                    }
                }
                if (readBytes != 0)
                    t31.t31_at_rx(_t31State, dteBuffer, checked((int)readBytes));
            }

            if (_answered == 2) {
                if (t38Mode) {
                    pump_network(_pathAToB!, t31.t31_get_t38_core_state(_t31State), packet);
                    pump_network(_pathBToA!, T38Terminal.t38_terminal_get_t38_core_state(t38Terminal!), packet);
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
                        for (int sample = 0; sample < SAMPLES_PER_CHUNK; sample++) {
                            stereo[2 * sample] = t30Samples[sample];
                            stereo[2 * sample + 1] = t31Samples[sample];
                        }
                        waveWriter.Write(stereo);
                    }
                }

            }
        }

        t31_print_snapshot("AFTER", t30, loopChunks);

        if (_pathAToB is not null)
            _ = g1050.g1050_free(_pathAToB);
        if (_pathBToA is not null)
            _ = g1050.g1050_free(_pathBToA);
        _pathAToB = null;
        _pathBToA = null;
        if (t38Terminal is not null)
            T38Terminal.t38_terminal_free(t38Terminal);
        faxEndpoint?.Dispose();
        t31.t31_free(_t31State);
        _t31State = null;

        if (!_done
            || t31_diag_atomic_read(ref _dteErrors) != 0
            || !_phaseESeen
            || _phaseEResult != (int)T30Error.Ok) {
            Console.Error.WriteLine(
                "STATE T31 REQUIREMENTS expected_done=1 " +
                $"actual_done={(_done ? 1 : 0)} expected_dte_success=1 actual_dte_success={t31_diag_atomic_read(ref _dteSuccess)} " +
                $"expected_dte_errors=0 actual_dte_errors={t31_diag_atomic_read(ref _dteErrors)} " +
                $"expected_phase_e_seen=1 actual_phase_e_seen={(_phaseESeen ? 1 : 0)} " +
                $"expected_phase_e_result={(int)T30Error.Ok} actual_phase_e_result={_phaseEResult}");
            Console.WriteLine("Tests failed");
            return 2;
        }

        Console.Error.WriteLine(
            $"T31_PSEUDO RESULT done=1 dte_success=1 dte_errors=0 phase_e_result={_phaseEResult}");
        return 0;
    }

    private static void handle_call_setup() {
        if (_countdown == 0)
            return;

        if (_answered == 0) {
            if (--_countdown == 0) {
                t31.t31_call_event(_t31State!, (int)AtCallEvent.Alerting);
                _countdown = 250;
            }
        } else if (_answered == 1) {
            _answered = 2;
            t31.t31_call_event(_t31State!, (int)AtCallEvent.Answered);
        }
    }

    private static int phase_b_handler(object? userData, int result) {
        var state = (T30State)userData!;
        Console.WriteLine($"A: Phase B handler on channel A - (0x{result:X}) {T30Logging.t30_frametype((byte)result)}");
        fax_log_rx_parameters(state, "A: Phase B");
        return (int)T30Error.Ok;
    }

    private static int phase_d_handler(object? userData, int result) {
        var state = (T30State)userData!;
        Console.WriteLine($"A: Phase D handler on channel A - (0x{result:X}) {T30Logging.t30_frametype((byte)result)}");
        fax_log_page_transfer_statistics(state, "A: Phase D");
        fax_log_tx_parameters(state, "A: Phase D");
        fax_log_rx_parameters(state, "A: Phase D");
        return (int)T30Error.Ok;
    }

    private static void phase_e_handler(object? userData, int result) {
        var state = (T30State)userData!;
        Console.WriteLine("Phase E handler on channel A");
        fax_log_final_transfer_statistics(state, "A: Phase E");
        fax_log_tx_parameters(state, "A: Phase E");
        fax_log_rx_parameters(state, "A: Phase E");
        _phaseESeen = true;
        _phaseEResult = result;
    }

    private static int at_tx_handler(object? userData, ReadOnlySpan<byte> data) {
        pseudo_terminal_state_t terminal = (pseudo_terminal_state_t)userData!;
        int totalWritten = 0;
        Interlocked.Exchange(ref _engineToDteLastLength, data.Length);
        unsafe {
            fixed (byte* raw = data) {
                while (totalWritten < data.Length) {
                    uint written;
                    uint remaining = checked((uint)(data.Length - totalWritten));
                    Interlocked.Increment(ref _engineToDteWriteCalls);
                    if (!WriteFile(terminal.master_fd, raw + totalWritten, remaining, out written, 0)) {
                        int error = Marshal.GetLastWin32Error();
                        Console.Error.WriteLine(
                            "ERROR T31_DTE WriteFile failed win32={0} expected_total={1} written_total={2} remaining={3}",
                            error,
                            data.Length,
                            totalWritten,
                            remaining);
                        auto_dte_record_error();
                        return -1;
                    }
                    if (written == 0) {
                        Console.Error.WriteLine(
                            "ERROR T31_DTE WriteFile made no progress expected_total={0} written_total={1}",
                            data.Length,
                            totalWritten);
                        auto_dte_record_error();
                        return -1;
                    }
                    totalWritten += checked((int)written);
                    Interlocked.Add(ref _engineToDteWriteBytes, checked((int)written));
                }
            }
        }
        if (totalWritten != data.Length) {
            Console.Error.WriteLine(
                "ERROR T31_DTE partial write expected={0} actual={1}",
                data.Length,
                totalWritten);
            auto_dte_record_error();
            return -1;
        }
        return 0;
    }

    private static int t31_call_control(t31_state_t state, object? userData, int operation, string? number) {
        _ = userData;
        Console.WriteLine($"Modem control - {at_modem_control_to_str(operation)}{(number is null ? string.Empty : " " + number)}");
        switch ((AtModemControlOperation)operation) {
            case AtModemControlOperation.Call:
                t31.t31_call_event(_t31State!, (int)AtCallEvent.Connected);
                _answered = 2;
                break;
            case AtModemControlOperation.Answer:
                _answered = 1;
                break;
            case AtModemControlOperation.Hangup:
            case AtModemControlOperation.OffHook:
                break;
        }
        _ = state;
        return 0;
    }

    private static int t38_tx_packet_handler(T38CoreState state, object? userData, ReadOnlyMemory<byte> packet, int count) {
        _ = userData;
        for (int repeat = 0; repeat < count; repeat++) {
            if (g1050.g1050_put(_pathAToB!, packet.ToArray(), packet.Length, state.TxSequenceNumber, _when) < 0)
                Console.WriteLine($"Lost packet {state.TxSequenceNumber}");
        }
        return 0;
    }

    private static int t31_tx_packet_handler(T38CoreState state, object? userData, ReadOnlyMemory<byte> packet, int count) {
        _ = userData;
        for (int repeat = 0; repeat < count; repeat++) {
            if (g1050.g1050_put(_pathBToA!, packet.ToArray(), packet.Length, state.TxSequenceNumber, _when) < 0)
                Console.WriteLine($"Lost packet {state.TxSequenceNumber}");
        }
        return 0;
    }

    private static void pump_network(g1050_state_t path, T38CoreState destination, byte[] packet) {
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

    private static int modem_wait_sock(pseudo_terminal_state_t terminal, int milliseconds, int flags) {
        if (terminal.master_fd == INVALID_HANDLE_VALUE)
            return MODEM_POLL_ERROR;
        long started = Environment.TickCount64;
        for (;;) {
            if (terminal.threadAbort != 0 && WaitForSingleObject(terminal.threadAbort, 0) == WAIT_OBJECT_0)
                return MODEM_POLL_ERROR;
            if (Volatile.Read(ref terminal.connected) == 0) {
                if (Environment.TickCount64 - started >= milliseconds)
                    return 0;
                Thread.Sleep(1);
                continue;
            }
            if ((flags & MODEM_POLL_READ) != 0 && terminal.block_read == 0) {
                if (!PeekNamedPipe(terminal.master_fd, 0, 0, 0, out uint available, 0))
                    return MODEM_POLL_ERROR;
                if (available > 0)
                    return MODEM_POLL_READ;
            }
            if ((flags & MODEM_POLL_WRITE) != 0)
                return MODEM_POLL_WRITE;
            if (Environment.TickCount64 - started >= milliseconds)
                return 0;
            Thread.Sleep(1);
        }
    }

    private static int automatic_dte_thread(string pipeName) {
        nint pipe = INVALID_HANDLE_VALUE;
        long started = Environment.TickCount64;
        Interlocked.Exchange(ref _dteThreadState, 1);

        while (Environment.TickCount64 - started < 10_000) {
            pipe = CreateFileA(
                pipeName,
                GENERIC_READ | GENERIC_WRITE,
                0,
                0,
                OPEN_EXISTING,
                0,
                0);
            if (pipe != INVALID_HANDLE_VALUE)
                break;
            int error = Marshal.GetLastWin32Error();
            if (error != ERROR_PIPE_BUSY && error != ERROR_FILE_NOT_FOUND) {
                Interlocked.Exchange(ref _dteThreadState, -2);
                Console.Error.WriteLine(
                    "ERROR T31_DTE CreateFile failed win32={0} pipe={1}",
                    error,
                    pipeName);
                auto_dte_record_error();
                return 2;
            }
            _ = WaitNamedPipeA(pipeName, 100);
            Thread.Sleep(10);
        }

        if (pipe == INVALID_HANDLE_VALUE) {
            Interlocked.Exchange(ref _dteThreadState, -3);
            Console.Error.WriteLine("ERROR T31_DTE connection timeout pipe={0}", pipeName);
            auto_dte_record_error();
            return 3;
        }

        Interlocked.Exchange(ref _pipeConnected, 1);
        Interlocked.Exchange(ref _dteThreadState, 2);
        Interlocked.Exchange(ref _dteThreadState, 3);
        while (Volatile.Read(ref _t31Ready) == 0) {
            if (Environment.TickCount64 - started >= 15_000) {
                Interlocked.Exchange(ref _dteThreadState, -4);
                Console.Error.WriteLine("ERROR T31_DTE T.31 readiness timeout");
                auto_dte_record_error();
                CloseHandle(pipe);
                Interlocked.Exchange(ref _pipeClosed, 1);
                return 4;
            }
            Thread.Sleep(2);
        }

        var reader = new DteReader(pipe);
        Interlocked.Exchange(ref _dteThreadState, 4);
        try {
            for (int index = 0; FaxReceiveDteSteps[index].Kind != DteStepKind.End; index++) {
                DteStep step = FaxReceiveDteSteps[index];
                Interlocked.Exchange(ref _dteStepIndex, index);
                Interlocked.Exchange(ref _dteStepKind, (int)step.Kind);
                Interlocked.Exchange(ref _dteCommandLength, step.Command.Length);
                Interlocked.Exchange(ref _dteExpectedLength, step.Expected.Length);
                Interlocked.Exchange(ref _dteFastPayloadBytes, 0);
                Interlocked.Exchange(ref _dteFastIdleMilliseconds, 0);

                if (step.Command.Length > 0) {
                    int totalWritten = 0;
                    unsafe {
                        fixed (byte* raw = step.Command) {
                            while (totalWritten < step.Command.Length) {
                                uint written;
                                uint remaining = checked((uint)(step.Command.Length - totalWritten));
                                Interlocked.Increment(ref _dteToEngineWriteCalls);
                                if (!WriteFile(pipe, raw + totalWritten, remaining, out written, 0) || written == 0) {
                                    int error = Marshal.GetLastWin32Error();
                                    Console.Error.WriteLine(
                                        "ERROR T31_DTE command write failed step={0} win32={1} expected={2} written={3}",
                                        step.Name,
                                        error,
                                        step.Command.Length,
                                        totalWritten);
                                    Interlocked.Exchange(ref _dteThreadState, -5);
                                    auto_dte_record_error();
                                    return 5;
                                }
                                totalWritten += checked((int)written);
                                Interlocked.Add(ref _dteToEngineWriteBytes, checked((int)written));
                            }
                        }
                    }
                }

                bool matched = step.Kind == DteStepKind.FastExpect
                    ? reader.dte_expect_fast_data(step.Name)
                    : reader.dte_expect_exact(step.Expected, step.Name);
                if (!matched) {
                    Interlocked.Exchange(ref _dteThreadState, -6);
                    auto_dte_record_error();
                    return 6;
                }
            }

            auto_dte_mark_success();
            Interlocked.Exchange(ref _dteThreadState, 5);
            return 0;
        } finally {
            CloseHandle(pipe);
            Interlocked.Exchange(ref _pipeClosed, 1);
        }
    }

    private static int auto_dte_error_count() => t31_diag_atomic_read(ref _dteErrors);

    private static void auto_dte_mark_success() => Interlocked.Exchange(ref _dteSuccess, 1);

    private static bool auto_dte_did_succeed() => t31_diag_atomic_read(ref _dteSuccess) != 0;

    private static t31_diag_snapshot_t t31_capture_snapshot(T30State t30, int loopChunks) {
        _ = t30;
        return new t31_diag_snapshot_t(
            _done ? 1 : 0,
            _answered,
            _phaseESeen ? 1 : 0,
            _phaseEResult,
            t31_diag_atomic_read(ref _dteSuccess),
            t31_diag_atomic_read(ref _dteErrors),
            loopChunks,
            t31_diag_atomic_read(ref _dteThreadState),
            t31_diag_atomic_read(ref _dteStepIndex),
            t31_diag_atomic_read(ref _dteStepKind),
            t31_diag_atomic_read(ref _dteCommandLength),
            t31_diag_atomic_read(ref _dteExpectedLength),
            t31_diag_atomic_read(ref _dteFastPayloadBytes),
            t31_diag_atomic_read(ref _dteFastIdleMilliseconds),
            t31_diag_atomic_read(ref _dteReaderBuffered),
            t31_diag_atomic_read(ref _pipeConnected),
            t31_diag_atomic_read(ref _pipeClosed),
            t31_diag_atomic_read(ref _dteToEngineWriteCalls),
            t31_diag_atomic_read(ref _dteToEngineWriteBytes),
            t31_diag_atomic_read(ref _engineToDteWriteCalls),
            t31_diag_atomic_read(ref _engineToDteWriteBytes),
            t31_diag_atomic_read(ref _engineToDteLastLength),
            _t31State is null ? 0 : 1,
            1);
    }

    private static void t31_print_snapshot(string name, T30State t30, int loopChunks) {
        t31_diag_snapshot_t snapshot = t31_capture_snapshot(t30, loopChunks);
        t31_state_t? terminal = _t31State;
        Console.Error.WriteLine(
            $"STATE T31 {name} CONTROL done={snapshot.done} answered={snapshot.answered} " +
            $"phase_e_seen={snapshot.phase_e_seen} phase_e_result={snapshot.phase_e_result} " +
            $"dte_success={snapshot.dte_success} dte_errors={snapshot.dte_errors} " +
            $"loop_chunks={snapshot.loop_chunks} t31_present={snapshot.t31_present} t30_present={snapshot.t30_present}");
        Console.Error.WriteLine(
            $"STATE T31 {name} DTE thread={t31_diag_atomic_read(ref _dteThreadState)} " +
            $"step={t31_diag_atomic_read(ref _dteStepIndex)} kind={t31_diag_atomic_read(ref _dteStepKind)} " +
            $"command_len={t31_diag_atomic_read(ref _dteCommandLength)} expected_len={t31_diag_atomic_read(ref _dteExpectedLength)} " +
            $"fast_payload={t31_diag_atomic_read(ref _dteFastPayloadBytes)} fast_idle_ms={t31_diag_atomic_read(ref _dteFastIdleMilliseconds)} " +
            $"reader_buffered={t31_diag_atomic_read(ref _dteReaderBuffered)} pipe_connected={t31_diag_atomic_read(ref _pipeConnected)} " +
            $"pipe_closed={t31_diag_atomic_read(ref _pipeClosed)} dte_to_engine_calls={t31_diag_atomic_read(ref _dteToEngineWriteCalls)} " +
            $"dte_to_engine_bytes={t31_diag_atomic_read(ref _dteToEngineWriteBytes)} " +
            $"engine_to_dte_calls={t31_diag_atomic_read(ref _engineToDteWriteCalls)} " +
            $"engine_to_dte_bytes={t31_diag_atomic_read(ref _engineToDteWriteBytes)} " +
            $"engine_to_dte_last_len={t31_diag_atomic_read(ref _engineToDteLastLength)}");
        if (terminal is not null) {
            Console.Error.WriteLine(
                $"STATE T31 {name} MODE modem={terminal.modem} at_mode={(terminal.at_state is null ? -1 : (int)terminal.at_state.ReceiveMode)} " +
                $"samples={terminal.t38_fe.samples} call_samples={terminal.call_samples} " +
                $"dte_timeout={terminal.dte_data_timeout} hdlc_len={terminal.hdlc_tx.len} " +
                $"hdlc_ptr={terminal.hdlc_tx.ptr} hdlc_final={(terminal.hdlc_tx.final ? 1 : 0)} " +
                $"non_ecm_in={terminal.non_ecm_tx.in_bytes} non_ecm_out={terminal.non_ecm_tx.out_bytes}");
        }
        Console.Error.WriteLine(
            $"STATE T31 {name} T30 phase={t30.Phase} state={t30.State} " +
            $"status={(int)t30.CurrentStatus} rx_type={t30.CurrentRxType} tx_type={t30.CurrentTxType}");
    }

    private static void fax_log_page_transfer_statistics(T30State state, string tag) {
        var statistics = new T30Statistics();
        T30.t30_get_transfer_statistics(state, statistics);
        Console.WriteLine($"{tag}: pages tx={statistics.PagesTransmitted} rx={statistics.PagesReceived} bit-rate={statistics.BitRate}");
    }

    private static void fax_log_final_transfer_statistics(T30State state, string tag) {
        var statistics = new T30Statistics();
        T30.t30_get_transfer_statistics(state, statistics);
        Console.WriteLine($"{tag}: status={(int)statistics.CurrentStatus} pages tx={statistics.PagesTransmitted} rx={statistics.PagesReceived}");
    }

    private static void fax_log_tx_parameters(T30State state, string tag) =>
        Console.WriteLine($"{tag}: tx ident='{state.TxInfo.Ident}' page={state.TxPageNumber} modem={state.CurrentTxType}");

    private static void fax_log_rx_parameters(T30State state, string tag) =>
        Console.WriteLine($"{tag}: rx ident='{state.RxInfo.Ident}' page={state.RxPageNumber} modem={state.CurrentRxType}");

    private static DteStep Exchange(string command, string response, string name) =>
        new(DteStepKind.Exchange, B(command), B(response), name);

    private static DteStep Expect(string response, string name) =>
        new(DteStepKind.Expect, Array.Empty<byte>(), B(response), name);

    private static DteStep Fast(string name) =>
        new(DteStepKind.FastExpect, Array.Empty<byte>(), Array.Empty<byte>(), name);

    private static byte[] B(string value) => Latin1.GetBytes(value);

    private static string next_argument(string[] testArguments, ref int index, string option) {
        if (++index >= testArguments.Length)
            throw new ArgumentException($"Missing value for {option}.");
        return testArguments[index];
    }

    private static string at_modem_control_to_str(int operation) =>
        AtInterpreterApi.at_modem_control_to_str(operation);

    private static int t31_diag_atomic_read(ref int value) => Volatile.Read(ref value);
    private static void auto_dte_record_error() => Interlocked.Increment(ref _dteErrors);

    private static void auto_dte_reset_result() {
        Interlocked.Exchange(ref _dteSuccess, 0);
        Interlocked.Exchange(ref _dteErrors, 0);
        Interlocked.Exchange(ref _dteThreadState, 0);
        Interlocked.Exchange(ref _dteStepIndex, -1);
        Interlocked.Exchange(ref _dteStepKind, -1);
        Interlocked.Exchange(ref _dteCommandLength, 0);
        Interlocked.Exchange(ref _dteExpectedLength, 0);
        Interlocked.Exchange(ref _dteFastPayloadBytes, 0);
        Interlocked.Exchange(ref _dteFastIdleMilliseconds, 0);
        Interlocked.Exchange(ref _dteReaderBuffered, 0);
        Interlocked.Exchange(ref _pipeConnected, 0);
        Interlocked.Exchange(ref _pipeClosed, 0);
        Interlocked.Exchange(ref _dteToEngineWriteCalls, 0);
        Interlocked.Exchange(ref _dteToEngineWriteBytes, 0);
        Interlocked.Exchange(ref _engineToDteWriteCalls, 0);
        Interlocked.Exchange(ref _engineToDteWriteBytes, 0);
        Interlocked.Exchange(ref _engineToDteLastLength, 0);
        Interlocked.Exchange(ref _t31Ready, 0);
    }

    private static void exit_test(int exitCode) => throw new T31PseudoTerminalExitException(exitCode);

    private sealed class T31PseudoTerminalExitException : Exception {
        public T31PseudoTerminalExitException(int exitCode) => ExitCode = exitCode;
        public int ExitCode { get; }
    }

    private sealed class DteReader {
        private readonly nint _pipe;
        private readonly List<byte> _buffer = new(65_536);
        private int _begin;

        public DteReader(nint pipe) => _pipe = pipe;

        public bool dte_expect_exact(ReadOnlySpan<byte> expected, string name) {
            while (BufferedCount < expected.Length) {
                int read = dte_reader_fill(10_000);
                if (read <= 0) {
                    Console.Error.WriteLine($"ERROR T31_DTE timeout waiting for {name}");
                    return false;
                }
            }

            ReadOnlySpan<byte> actual = _buffer.ToArray().AsSpan(_begin, expected.Length);
            if (!actual.SequenceEqual(expected)) {
                Console.Error.WriteLine($"ERROR T31_DTE mismatch in {name}");
                Console.Error.Write("Expected: ");
                print_hex(expected);
                Console.Error.Write("Actual:   ");
                print_hex(actual);
                return false;
            }

            _begin += expected.Length;
            dte_reader_compact();
            return true;
        }

        public bool dte_expect_fast_data(string name) {
            bool dlePending = false;
            int payloadBytes = 0;
            int idleMilliseconds = 0;

            while (idleMilliseconds < FAST_IDLE_TIMEOUT_MS) {
                while (BufferedCount > 0) {
                    byte value = _buffer[_begin++];
                    if (dlePending) {
                        if (value == ETX) {
                            dte_reader_compact();
                            Interlocked.Exchange(ref _dteFastPayloadBytes, payloadBytes);
                            Interlocked.Exchange(ref _dteFastIdleMilliseconds, idleMilliseconds);
                            return true;
                        }
                        dlePending = false;
                        payloadBytes++;
                    } else if (value == DLE) {
                        dlePending = true;
                    } else {
                        payloadBytes++;
                    }
                }

                dte_reader_compact();
                int wait = Math.Min(250, FAST_IDLE_TIMEOUT_MS - idleMilliseconds);
                int read = dte_reader_fill(wait);
                if (read > 0) {
                    idleMilliseconds = 0;
                } else {
                    idleMilliseconds += wait;
                }
                Interlocked.Exchange(ref _dteFastPayloadBytes, payloadBytes);
                Interlocked.Exchange(ref _dteFastIdleMilliseconds, idleMilliseconds);
            }

            Console.Error.WriteLine($"ERROR T31_DTE fast-data inactivity timeout in {name}");
            return false;
        }

        private int BufferedCount => _buffer.Count - _begin;

        private int dte_reader_fill(int timeoutMilliseconds) {
            long started = Environment.TickCount64;
            var temporary = new byte[4096];
            for (;;) {
                if (!PeekNamedPipe(_pipe, 0, 0, 0, out uint available, 0)) {
                    Console.Error.WriteLine("ERROR T31_DTE PeekNamedPipe failed win32={0}", Marshal.GetLastWin32Error());
                    return -1;
                }
                if (available > 0) {
                    uint readBytes;
                    unsafe {
                        fixed (byte* raw = temporary) {
                            if (!ReadFile(_pipe, raw, (uint)Math.Min(temporary.Length, available), out readBytes, 0)) {
                                Console.Error.WriteLine("ERROR T31_DTE ReadFile failed win32={0}", Marshal.GetLastWin32Error());
                                return -1;
                            }
                        }
                    }
                    if (readBytes > 0)
                        _buffer.AddRange(temporary.AsSpan(0, checked((int)readBytes)).ToArray());
                    Interlocked.Exchange(ref _dteReaderBuffered, BufferedCount);
                    return checked((int)readBytes);
                }
                if (Environment.TickCount64 - started >= timeoutMilliseconds)
                    return 0;
                Thread.Sleep(2);
            }
        }

        private void dte_reader_compact() {
            if (_begin > 0 && (_begin >= 2048 || _begin == _buffer.Count)) {
                _buffer.RemoveRange(0, _begin);
                _begin = 0;
            }
            Interlocked.Exchange(ref _dteReaderBuffered, BufferedCount);
        }

        private static void print_hex(ReadOnlySpan<byte> bytes) {
            foreach (byte value in bytes)
                Console.Error.Write($"{value:X2} ");
            Console.Error.WriteLine();
        }
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
