/*
 * Managed C# port of TKFaxEngineTest.cpp.
 *
 * Complete OneExe test runner: 92 test registrations, child-process isolation,
 * deterministic two-peer harnesses, activity-aware timeouts, complete temporary child-output capture,
 * failure-only detail retention, required-input validation, artifact collection and test-data tools.
 */
#nullable enable
using TKFaxEngine;

using TKFaxEngineTest.Audio.Test;
using TKFaxEngineTest.Core.Test;
using TKFaxEngineTest.Fax.Test;
using TKFaxEngineTest.Image.Test;
using TKFaxEngineTest.Modem.Test;
using TKFaxEngineTest.Network.Test;
using TKFaxEngineTest.Utility.Test;
using TKFaxEngineTest.System;

using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using BitMiracle.LibTiff.Classic;
using static TKFaxEngineTest.TKFaxEngineTest_ExitGuard;

namespace TKFaxEngineTest;

public static class TKFaxEngineTest {
    private const int WaitObject0 = 0;
    private const int WaitTimeout = 258;
    private const int WaitFailed = -1;
    private const int WaitNotRun = -2;

    private delegate int TestMain(string[] testArguments);

    private sealed record TestEntry(string Id, string File, string Category, TestMain Run);

    private sealed record RunResult(int ReturnCode, bool Skipped, bool TimedOut);

    private sealed record TestArtifactFileState(long Size, DateTime WriteTimeUtc);

    private sealed record TestArtifactCollectionResult(int Moved, int Failed);

    private sealed record RequiredTestInput(string Path, string Reason, bool AllowLocalFallback = false);

    private sealed record ExternalHarnessReport(
        string MissingComponent = "",
        string ExpectedBy = "",
        string ExpectedLocation = "",
        string RequiredInvocation1 = "",
        string RequiredInvocation2 = "",
        string RequiredPeerBehavior = "",
        string ExpectedCompletion = "",
        string ImplementationFiles = "");

    private sealed record TestGroup(string Key, string Title, string Category);

    private sealed class TkfxPbmImage {
        public uint Width { get; set; }
        public uint Height { get; set; }
        public int Stride { get; set; }
        public byte[] Pixels { get; set; } = Array.Empty<byte>();
    }

    private sealed class RunnerVariableSnapshot {
        public string Label { get; init; } = "";
        public string Timestamp { get; init; } = "";
        public string TestId { get; init; } = "";
        public string TestFile { get; init; } = "";
        public string Category { get; init; } = "";
        public string Scope { get; init; } = "";
        public string ExtraArgs { get; init; } = "";
        public int Passed { get; init; }
        public int Failed { get; init; }
        public int Skipped { get; init; }
        public int NotImplemented { get; init; }
        public string ProjectRoot { get; init; } = "";
        public string ExePath { get; init; } = "";
        public string ExeDir { get; init; } = "";
        public string TestsDir { get; init; } = "";
        public string LogFile { get; init; } = "";
        public string DetailDir { get; init; } = "";
        public string ParentWorkingDir { get; init; } = "";
        public string ChildWorkingDir { get; init; } = "";
        public string DetailTmp { get; init; } = "";
        public string DetailFail { get; init; } = "";
        public string DetailTimeout { get; init; } = "";
        public string FinalDetail { get; init; } = "";
        public bool DetailTmpExists { get; init; }
        public bool DetailFailExists { get; init; }
        public bool DetailTimeoutExists { get; init; }
        public bool FinalDetailExists { get; init; }
        public long DetailTmpBytes { get; init; }
        public long DetailFailBytes { get; init; }
        public long DetailTimeoutBytes { get; init; }
        public long FinalDetailBytes { get; init; }
        public string ChildCommand { get; init; } = "";
        public int TimeoutMs { get; init; }
        public int WaitResult { get; init; }
        public string WaitName { get; init; } = "";
        public int ExitCode { get; init; }
        public bool TimedOut { get; init; }
        public long DurationMs { get; init; }
        public int CreateProcessError { get; init; }
        public string CreateProcessErrorText { get; init; } = "";
        public bool ProcessCreated { get; init; }
        public int ProcessId { get; init; }
        public long ChildOutputBytes { get; init; }
        public string EnvTimeoutSec { get; init; } = "";
        public string EnvActiveTimeoutSec { get; init; } = "";
        public string Note { get; init; } = "";
    }

    private sealed class ChildOutputCapture : IDisposable {
        private const int MaxLineCharacters = 16_384;
        private const int MaxOccurrenceEntries = 100_000;
        private const long MaxRetainedBytes = 64L * 1024L * 1024L;
        private const long MaxRetainedLines = 500_000L;
        private const int FlushIntervalLines = 256;

        private readonly object _sync = new();
        private readonly StreamWriter _writer;
        private readonly Stopwatch _watch = Stopwatch.StartNew();
        private readonly Dictionary<string, long> _lineOccurrences = new(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _kindCounts = new(StringComparer.Ordinal);
        private long _sequence;
        private int _linesSinceFlush;
        private bool _disposed;

        public ChildOutputCapture(
            string detailFile,
            TestEntry test,
            IReadOnlyList<string> effectiveArgs,
            string command,
            IReadOnlyList<RequiredTestInput> requiredInputs) {
            Directory.CreateDirectory(Path.GetDirectoryName(detailFile) ?? ".");
            _writer = new StreamWriter(new FileStream(detailFile, FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) {
                NewLine = "\r\n",
                AutoFlush = false
            };
            _writer.Write(BuildTestDetailHeader(test, effectiveArgs, command, requiredInputs));
            _writer.Flush();
        }

        public long RawBytes;
        public long RawLines;
        public long KeptLines;
        public long SuppressedLines;
        public long RetentionSuppressedLines;
        public long StdoutLines;
        public long StderrLines;
        public long RunnerLines;
        public long EmptyLines;
        public long RepeatedLines;
        public bool RawOutputNonempty;
        public bool LineTruncated;
        public bool RetentionLimitReached;
        public bool OccurrenceMapLimitReached;
        public bool ReadFailed = false;
        public int ReadError = 0;
        public long RetainedBytes;

        public void CaptureLine(string? line, string stream) {
            if (line is null)
                return;

            lock (_sync) {
                RawLines++;
                RawBytes += Encoding.UTF8.GetByteCount(line) + 2L;
                if (stream.Equals("STDERR", StringComparison.Ordinal))
                    StderrLines++;
                else if (stream.Equals("RUNNER", StringComparison.Ordinal))
                    RunnerLines++;
                else
                    StdoutLines++;

                if (line.Length == 0)
                    EmptyLines++;
                else
                    RawOutputNonempty = true;

                line = SanitizeDiagnosticLine(line, out bool truncated);
                LineTruncated |= truncated;
                string kind = ClassifyChildLine(line, stream);
                _kindCounts[kind] = _kindCounts.TryGetValue(kind, out long kindCount) ? kindCount + 1L : 1L;

                if (_lineOccurrences.TryGetValue(line, out long occurrences)) {
                    occurrences++;
                    _lineOccurrences[line] = occurrences;
                    if (occurrences > 1L)
                        RepeatedLines++;
                } else if (_lineOccurrences.Count < MaxOccurrenceEntries) {
                    _lineOccurrences.Add(line, 1L);
                } else {
                    OccurrenceMapLimitReached = true;
                }

                long sequence = ++_sequence;
                string prefix = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} +{_watch.Elapsed.TotalSeconds:000000.000}s seq={sequence:000000} stream={stream} kind={kind} | ";
                long required = Encoding.UTF8.GetByteCount(prefix) + Encoding.UTF8.GetByteCount(line) + 2L;
                if (RetainedBytes + required > MaxRetainedBytes || KeptLines >= MaxRetainedLines) {
                    RetentionLimitReached = true;
                    SuppressedLines++;
                    RetentionSuppressedLines++;
                    return;
                }

                _writer.Write(prefix);
                _writer.WriteLine(line);
                RetainedBytes += required;
                KeptLines++;
                _linesSinceFlush++;
                if (_linesSinceFlush >= FlushIntervalLines) {
                    _writer.Flush();
                    _linesSinceFlush = 0;
                }
            }
        }

        public string CompactSummary() {
            lock (_sync) {
                string kinds = string.Join(",", _kindCounts.OrderBy(static pair => pair.Key).Select(static pair => $"{pair.Key}:{pair.Value}"));
                return $"raw_lines={RawLines} kept_lines={KeptLines} stdout={StdoutLines} stderr={StderrLines} runner={RunnerLines} raw_bytes={RawBytes} retained_bytes={RetainedBytes} suppressed={SuppressedLines} truncated={BoolText(LineTruncated)} kinds=[{kinds}]";
            }
        }

        public void Finish() {
            lock (_sync) {
                if (_disposed)
                    return;

                _writer.WriteLine("----- CHILD OUTPUT END -----");
                _writer.WriteLine();
                _writer.WriteLine("----- KINDPROZESSAUSGABE ZUSAMMENFASSUNG -----");
                _writer.WriteLine($"CaptureDurationMs={_watch.ElapsedMilliseconds}");
                _writer.WriteLine($"RawOutputState={(RawOutputNonempty ? "BEFUELLT" : "LEER")}");
                _writer.WriteLine($"RawOutputBytes={RawBytes}");
                _writer.WriteLine($"RawOutputLines={RawLines}");
                _writer.WriteLine($"StdoutLines={StdoutLines}");
                _writer.WriteLine($"StderrLines={StderrLines}");
                _writer.WriteLine($"RunnerLines={RunnerLines}");
                _writer.WriteLine($"EmptyLines={EmptyLines}");
                _writer.WriteLine($"RetainedLines={KeptLines}");
                _writer.WriteLine($"RetainedBytes={RetainedBytes}");
                _writer.WriteLine($"SuppressedLines={SuppressedLines}");
                _writer.WriteLine($"SuppressedRetentionLimitLines={RetentionSuppressedLines}");
                _writer.WriteLine($"RepeatedLines={RepeatedLines}");
                _writer.WriteLine($"LineTruncated={BoolText(LineTruncated)}");
                _writer.WriteLine($"RetentionLimitReached={BoolText(RetentionLimitReached)}");
                _writer.WriteLine($"OccurrenceMapLimitReached={BoolText(OccurrenceMapLimitReached)}");
                _writer.WriteLine($"PipeReadState={(ReadFailed ? "FEHLER" : "OK")}");
                if (ReadFailed)
                    _writer.WriteLine($"PipeReadError={ReadError} | {Win32ErrorText(ReadError)}");

                foreach ((string kind, long count) in _kindCounts.OrderBy(static pair => pair.Key))
                    _writer.WriteLine($"KindCount[{kind}]={count}");

                foreach ((string sample, long count) in _lineOccurrences
                             .Where(static pair => pair.Value > 1L)
                             .OrderByDescending(static pair => pair.Value)
                             .ThenBy(static pair => pair.Key, StringComparer.Ordinal)
                             .Take(30)) {
                    _writer.WriteLine($"RepeatedLine count={count} sample={sample}");
                }

                _writer.WriteLine("----- ENDE KINDPROZESSAUSGABE ZUSAMMENFASSUNG -----");
                _writer.Flush();
            }
        }

        public void Dispose() {
            lock (_sync) {
                if (_disposed)
                    return;
                Finish();
                _writer.Dispose();
                _disposed = true;
            }
        }
    }

    private sealed class TeeTextWriter : TextWriter {
        private readonly TextWriter _consoleWriter;
        private readonly TextWriter _logWriter;
        private readonly object _sync;

        public TeeTextWriter(TextWriter consoleWriter, TextWriter logWriter, object sync) {
            _consoleWriter = consoleWriter;
            _logWriter = logWriter;
            _sync = sync;
        }

        public override Encoding Encoding => _consoleWriter.Encoding;

        public override void Write(char value) {
            lock (_sync) {
                _consoleWriter.Write(value);
                _logWriter.Write(value);
            }
        }

        public override void Write(string? value) {
            lock (_sync) {
                _consoleWriter.Write(value);
                _logWriter.Write(value);
            }
        }

        public override void Write(char[]? buffer, int index, int count) {
            lock (_sync) {
                _consoleWriter.Write(buffer, index, count);
                _logWriter.Write(buffer, index, count);
            }
        }

        public override void WriteLine() {
            lock (_sync) {
                _consoleWriter.WriteLine();
                _logWriter.WriteLine();
            }
        }

        public override void WriteLine(string? value) {
            lock (_sync) {
                _consoleWriter.WriteLine(value);
                _logWriter.WriteLine(value);
            }
        }

        public override void Flush() {
            lock (_sync) {
                _consoleWriter.Flush();
                _logWriter.Flush();
            }
        }
    }

    private sealed record ProcessResourceSample(
        long ElapsedMs,
        long OutputBytes,
        long CpuTime100Ns,
        long WorkingSetBytes,
        long PrivateMemoryBytes,
        int ThreadCount,
        int HandleCount,
        ulong IoBytes);

    private sealed class ProcessActivityWaitResult {
        public int WaitResult { get; set; } = WaitFailed;
        public int AppliedTimeoutMs { get; set; }
        public bool ActivityDetected { get; set; }
        public bool OutputActivity { get; set; }
        public bool CpuActivity { get; set; }
        public bool IoActivity { get; set; }
        public long OutputBytes { get; set; }
        public long CpuTime100Ns { get; set; }
        public ulong IoBytes { get; set; }
        public List<ProcessResourceSample> Samples { get; } = new();
    }

    private sealed class HarnessPeerProcess : IDisposable {
        public Process? Process { get; set; }
        public string Command { get; set; } = "";
        public bool Started => Process is not null;

        public void Dispose() {
            Process?.Dispose();
            Process = null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessIoCounters(IntPtr processHandle, out IoCounters counters);

    private static readonly TestEntry[] Tests =
    {
        new("ademco_contactid_tests", "ademco_contactid_tests.c", "Core", ademco_contactid_tests_adapter.tkfx_ademco_contactid_tests_main),
        new("adsi_tests", "adsi_tests.c", "Core", adsi_tests_adapter.tkfx_adsi_tests_main),
        new("agc_float_tests", "agc_float_tests.c", "Audio", agc_float_tests_adapter.tkfx_agc_float_tests_main),
        new("alloc_tests", "alloc_tests.c", "Core", alloc_tests_adapter.tkfx_alloc_tests_main),
        new("async_tests", "async_tests.c", "Core", async_tests_adapter.tkfx_async_tests_main),
        new("at_interpreter_tests", "at_interpreter_tests.c", "Core", at_interpreter_tests_adapter.tkfx_at_interpreter_tests_main),
        new("awgn_tests", "awgn_tests.c", "Audio", awgn_tests_adapter.tkfx_awgn_tests_main),
        new("bell_mf_rx_tests", "bell_mf_rx_tests.c", "Audio", bell_mf_rx_tests_adapter.tkfx_bell_mf_rx_tests_main),
        new("bell_mf_tx_tests", "bell_mf_tx_tests.c", "Audio", bell_mf_tx_tests_adapter.tkfx_bell_mf_tx_tests_main),
        new("bert_tests", "bert_tests.c", "Modem", bert_tests_adapter.tkfx_bert_tests_main),
        new("bit_operations_tests", "bit_operations_tests.c", "Core", bit_operations_tests_adapter.tkfx_bit_operations_tests_main),
        new("bitstream_tests", "bitstream_tests.c", "Modem", bitstream_tests_adapter.tkfx_bitstream_tests_main),
        new("complex_tests", "complex_tests.c", "Core", complex_tests_adapter.tkfx_complex_tests_main),
        new("complex_vector_float_tests", "complex_vector_float_tests.c", "Core", complex_vector_float_tests_adapter.tkfx_complex_vector_float_tests_main),
        new("complex_vector_int_tests", "complex_vector_int_tests.c", "Core", complex_vector_int_tests_adapter.tkfx_complex_vector_int_tests_main),
        new("crc_tests", "crc_tests.c", "Core", crc_tests_adapter.tkfx_crc_tests_main),
        new("data_modems_tests", "data_modems_tests.c", "Modem", data_modems_tests_adapter.tkfx_data_modems_tests_main),
        new("dc_restore_tests", "dc_restore_tests.c", "Audio", dc_restore_tests_adapter.tkfx_dc_restore_tests_main),
        new("dds_tests", "dds_tests.c", "Audio", dds_tests_adapter.tkfx_dds_tests_main),
        new("dtmf_rx_tests", "dtmf_rx_tests.c", "Audio", DtmfRxTestsAdapter.tkfx_dtmf_rx_tests_main),
        new("dtmf_tx_tests", "dtmf_tx_tests.c", "Audio", DtmfTxTestsAdapter.tkfx_dtmf_tx_tests_main),
        new("dummy_modems_tests", "dummy_modems_tests.c", "Modem", DummyModemsTestsAdapter.tkfx_dummy_modems_tests_main),
        new("echo_tests", "echo_tests.c", "Audio", echo_tests_adapter.tkfx_echo_tests_main),
        new("fax_decode", "fax_decode.c", "Fax/T.30", fax_decode_adapter.tkfx_fax_decode_main),
        new("fax_tests", "fax_tests.c", "Fax/T.30", fax_tests_adapter.tkfx_fax_tests_main),
        new("fsk_tests", "fsk_tests.c", "Modem", fsk_tests_adapter.tkfx_fsk_tests_main),
        new("g1050_tests", "g1050_tests.c", "Network/Packet/Sim", G1050TestsAdapter.tkfx_g1050_tests_main),
        new("g168_tests", "g168_tests.c", "Core", g168_tests_adapter.tkfx_g168_tests_main),
        new("g711_tests", "g711_tests.c", "Audio", g711_tests_adapter.tkfx_g711_tests_main),
        new("g722_tests", "g722_tests.c", "Audio", g722_tests_adapter.tkfx_g722_tests_main),
        new("g726_tests", "g726_tests.c", "Audio", g726_tests_adapter.tkfx_g726_tests_main),
        new("gsm0610_tests", "gsm0610_tests.c", "Audio", Gsm0610TestsAdapter.tkfx_gsm0610_tests_main),
        new("hdlc_tests", "hdlc_tests.c", "Modem", HdlcTestsAdapter.tkfx_hdlc_tests_main),
        new("ima_adpcm_tests", "ima_adpcm_tests.c", "Audio", ImaAdpcmTestsAdapter.tkfx_ima_adpcm_tests_main),
        new("image_translate_tests", "image_translate_tests.c", "Image/Compression", ImageTranslateTestsAdapter.tkfx_image_translate_tests_main),
        new("line_model_tests", "line_model_tests.c", "Network/Packet/Sim", line_model_tests_adapter.tkfx_line_model_tests_main),
        new("logging_tests", "logging_tests.c", "Core", logging_tests_adapter.tkfx_logging_tests_main),
        new("lpc10_tests", "lpc10_tests.c", "Audio", lpc10_tests_adapter.tkfx_lpc10_tests_main),
        new("make_g168_css", "make_g168_css.c", "Utility", make_g168_css_adapter.tkfx_make_g168_css_main),
        new("math_fixed_tests", "math_fixed_tests.c", "Core", math_fixed_tests_adapter.tkfx_math_fixed_tests_main),
        new("modem_connect_tones_tests", "modem_connect_tones_tests.c", "Modem", modem_connect_tones_tests_adapter.tkfx_modem_connect_tones_tests_main),
        new("modem_echo_tests", "modem_echo_tests.c", "Modem", modem_echo_tests_adapter.tkfx_modem_echo_tests_main),
        new("noise_tests", "noise_tests.c", "Audio", NoiseTestsAdapter.tkfx_noise_tests_main),
        new("oki_adpcm_tests", "oki_adpcm_tests.c", "Audio", OkiAdpcmTestsAdapter.tkfx_oki_adpcm_tests_main),
        new("playout_tests", "playout_tests.c", "Audio", PlayoutTestsAdapter.tkfx_playout_tests_main),
        new("plc_tests", "plc_tests.c", "Audio", PlcTestsAdapter.tkfx_plc_tests_main),
        new("power_meter_tests", "power_meter_tests.c", "Audio", PowerMeterTestsAdapter.tkfx_power_meter_tests_main),
        new("pseudo_terminal_tests", "pseudo_terminal_tests.c", "Utility", PseudoTerminalTestsAdapter.tkfx_pseudo_terminal_tests_main),
        new("queue_tests", "queue_tests.c", "Core", QueueTestsAdapter.tkfx_queue_tests_main),
        new("r2_mf_rx_tests", "r2_mf_rx_tests.c", "Audio", r2_mf_rx_tests_adapter.tkfx_r2_mf_rx_tests_main),
        new("r2_mf_tx_tests", "r2_mf_tx_tests.c", "Audio", r2_mf_tx_tests_adapter.tkfx_r2_mf_tx_tests_main),
        new("rfc2198_sim_tests", "rfc2198_sim_tests.c", "Network/Packet/Sim", rfc2198_sim_tests_adapter.tkfx_rfc2198_sim_tests_main),
        new("saturated_tests", "saturated_tests.c", "Core", saturated_tests_adapter.tkfx_saturated_tests_main),
        new("schedule_tests", "schedule_tests.c", "Core", schedule_tests_adapter.tkfx_schedule_tests_main),
        new("sig_tone_tests", "sig_tone_tests.c", "Audio", sig_tone_tests_adapter.tkfx_sig_tone_tests_main),
        new("sprt_decode", "sprt_decode.c", "Modem", SprtDecodeAdapter.tkfx_sprt_decode_main),
        new("sprt_tests", "sprt_tests.c", "Modem", SprtTestsAdapter.tkfx_sprt_tests_main),
        new("super_tone_rx_tests", "super_tone_rx_tests.c", "Audio", SuperToneRxTestsAdapter.tkfx_super_tone_rx_tests_main),
        new("super_tone_tx_tests", "super_tone_tx_tests.c", "Audio", super_tone_tx_tests_adapter.tkfx_super_tone_tx_tests_main),
        new("swept_tone_tests", "swept_tone_tests.c", "Audio", SweptToneTestsAdapter.tkfx_swept_tone_tests_main),
        new("t31_pseudo_terminal_tests", "t31_pseudo_terminal_tests.c", "Utility", t31_pseudo_terminal_tests_adapter.tkfx_t31_pseudo_terminal_tests_main),
        new("t31_tests", "t31_tests.c", "Fax/T.30", t31_tests_adapter.tkfx_t31_tests_main),
        new("t35_tests", "t35_tests.c", "Fax/T.30", t35_tests_adapter.tkfx_t35_tests_main),
        new("t38_core_tests", "t38_core_tests.c", "T.38", t38_core_tests_adapter.tkfx_t38_core_tests_main),
        new("t38_decode", "t38_decode.c", "T.38", t38_decode_adapter.tkfx_t38_decode_main),
        new("t38_non_ecm_buffer_tests", "t38_non_ecm_buffer_tests.c", "T.38", t38_non_ecm_buffer_tests_adapter.tkfx_t38_non_ecm_buffer_tests_main),
        new("t42_tests", "t42_tests.c", "Image/Compression", t42_tests_adapter.tkfx_t42_tests_main),
        new("t43_tests", "t43_tests.c", "Image/Compression", t43_tests_adapter.tkfx_t43_tests_main),
        new("t4_t6_tests", "t4_t6_tests.c", "Image/Compression", T4T6TestsAdapter.tkfx_t4_t6_tests_main),
        new("t4_tests", "t4_tests.c", "Image/Compression", T4TestsAdapter.tkfx_t4_tests_main),
        new("t81_t82_arith_coding_tests", "t81_t82_arith_coding_tests.c", "Image/Compression", t81_t82_arith_coding_tests_adapter.tkfx_t81_t82_arith_coding_tests_main),
        new("t85_tests", "t85_tests.c", "Image/Compression", T85TestsAdapter.tkfx_t85_tests_main),
        new("time_scale_tests", "time_scale_tests.c", "Audio", TimeScaleTestsAdapter.tkfx_time_scale_tests_main),
        new("timezone_tests", "timezone_tests.c", "Core", TimezoneTestsAdapter.tkfx_timezone_tests_main),
        new("tone_detect_tests", "tone_detect_tests.c", "Audio", ToneDetectTestsAdapter.tkfx_tone_detect_tests_main),
        new("tone_generate_tests", "tone_generate_tests.c", "Audio", ToneGenerateTestsAdapter.tkfx_tone_generate_tests_main),
        new("tsb85_tests", "tsb85_tests.c", "Fax/T.30", Tsb85TestsAdapter.tkfx_tsb85_tests_main),
        new("v150_1_sse_tests", "v150_1_sse_tests.c", "Modem", v150_1_sse_tests_adapter.tkfx_v150_1_sse_tests_main),
        new("v150_1_tests", "v150_1_tests.c", "Modem", v150_1_tests_adapter.tkfx_v150_1_tests_main),
        new("v17_tests", "v17_tests.c", "Modem", V17TestsAdapter.tkfx_v17_tests_main),
        new("v18_tests", "v18_tests.c", "Modem", v18_tests_adapter.tkfx_v18_tests_main),
        new("v22bis_tests", "v22bis_tests.c", "Modem", v22bis_tests_adapter.tkfx_v22bis_tests_main),
        new("v27ter_tests", "v27ter_tests.c", "Modem", v27ter_tests_adapter.tkfx_v27ter_tests_main),
        new("v29_tests", "v29_tests.c", "Modem", v29_tests_adapter.tkfx_v29_tests_main),
        new("v32bis_tests", "v32bis_tests.c", "Modem", v32bis_tests_adapter.tkfx_v32bis_tests_main),
        new("v34_tests", "v34_tests.c", "Modem", v34_tests_adapter.tkfx_v34_tests_main),
        new("v42_tests", "v42_tests.c", "Modem", v42_tests_adapter.tkfx_v42_tests_main),
        new("v42bis_tests", "v42bis_tests.c", "Modem", v42bis_tests_adapter.tkfx_v42bis_tests_main),
        new("v80_tests", "v80_tests.c", "Modem", v80_tests_adapter.tkfx_v80_tests_main),
        new("v8_tests", "v8_tests.c", "Modem", V8TestsAdapter.tkfx_v8_tests_main),
        new("vector_float_tests", "vector_float_tests.c", "Core", vector_float_tests_adapter.tkfx_vector_float_tests_main),
        new("vector_int_tests", "vector_int_tests.c", "Core", vector_int_tests_adapter.tkfx_vector_int_tests_main),
    };

    private static readonly TestGroup[] Groups =
    {
        new("core", "Core Tests", "Core"),
        new("audio", "Audio Tests", "Audio"),
        new("modem", "Modem Tests", "Modem"),
        new("fax", "Fax / T.30 Tests", "Fax/T.30"),
        new("t38", "T.38 Tests", "T.38"),
        new("image", "Image / Compression Tests", "Image/Compression"),
        new("network", "Network / Packet / Simulation Tests", "Network/Packet/Sim"),
        new("utility", "Utility / External Harness Tests", "Utility")
    };

    private const string RunRootEnvironmentVariable = "TKFAXENGINE_TEST_RUN_ROOT";
    private const string LogDirectoryEnvironmentVariable = "TKFAXENGINE_TEST_LOG_DIR";
    private const string OutputDirectoryEnvironmentVariable = "TKFAXENGINE_TEST_OUTPUT_DIR";

    private static string _projectRoot = "";
    private static string _runRootDir = "";
    private static string _logDir = "";
    private static string _exeDir = "";
    private static string _exePath = "";
    private static string _managedAssemblyPath = "";
    private static string _testsDir = "";
    private static string _outputDir = "";
    private static string _logFile = "";
    private static string _detailDir = "";
    private static bool _logOpened;
    private static int _passed;
    private static int _failed;
    private static int _skipped;
    private static int _notImplemented;
    private static int _runningTimerWidth;
    private static readonly object ConsoleCaptureSync = new();
    private static TextWriter? _originalConsoleOut;
    private static TextWriter? _originalConsoleError;
    private static StreamWriter? _consoleCaptureWriter;

    public static int Main(string[] args) {
        bool internalProcess = IsInternalProcess(args);
        if (!SetupRuntimePaths(internalProcess))
            return 3;

        if (internalProcess)
            return RunMain(args);

        try {
            InitializeConsoleCapture();
            EnsureLogOpen();
            AppendLogLine($"RUN START root={_runRootDir} logs={_logDir} output={_outputDir}");
            int exitCode = RunMain(args);
            AppendLogLine($"RUN END rc={exitCode} root={_runRootDir}");
            return exitCode;
        } catch (Exception exception) {
            AppendLogLine($"RUN UNHANDLED EXCEPTION type={exception.GetType().FullName} message={exception.Message}");
            Console.Error.WriteLine($"UNBEHANDELTE AUSNAHME: {exception}");
            throw;
        } finally {
            ShutdownConsoleCapture();
        }
    }

    private static bool IsInternalProcess(IReadOnlyList<string> args) =>
        args.Count >= 1 && args[0] is "--child-run" or "--peer-run";

    private static int RunMain(string[] args) {
        if (args.Length >= 1) {
            string command = args[0];
            if (command == "--child-run" && args.Length >= 2) {
                TestEntry? test = FindTest(args[1]);
                if (test is null)
                    return 2;

                string[] extra = args.Skip(2).ToArray();
                if (extra.Length == 0 && IsTwoPeerHarnessTest(test))
                    return RunTwoPeerHarness(test);

                if (test.Id == "t31_pseudo_terminal_tests" && !TestArgHasOption(extra, "-A"))
                    extra = new[] { "-A" }.Concat(extra).ToArray();

                return InvokeTestBody(test, extra);
            }

            if (command == "--peer-run" && args.Length >= 2) {
                TestEntry? test = FindTest(args[1]);
                return test is null ? 2 : InvokeTestBody(test, args.Skip(2).ToArray());
            }

            if (command == "--list") {
                ListTests();
                return 0;
            }

            if (command == "--groups") {
                ListGroups();
                return 0;
            }

            if (command == "--generate-test-files") {
                return RunGeneratorCommand("generate-test-files", GenerateAllTestFiles);
            }

            if (command is "--generate-g168-css" or "--validate-g168-css") {
                return RunGeneratorCommand(command[2..], GenerateG168CssTestFiles);
            }

            if (command == "--generate-itu-pbm-tiff") {
                return RunGeneratorCommand("generate-itu-pbm-tiff", generate_itu_fax_pbm_tiff_test_files);
            }

            if (command == "--generate-itu-fax-pages") {
                return RunGeneratorCommand("generate-itu-fax-pages", GenerateItuFaxToolTestFiles);
            }

            if (command == "--generate-etsi-300-242") {
                return RunGeneratorCommand("generate-etsi-300-242", GenerateEtsi300242TestFiles);
            }

            if ((command == "--group" || command == "--category") && args.Length >= 2) {
                TestGroup? group = FindGroup(args[1]);
                if (group is null) {
                    Console.Error.WriteLine($"Unbekannte Gruppe: {args[1]}");
                    return 2;
                }
                return RunGroupTests(group);
            }

            if (command == "--all")
                return RunAllTests();

            TestEntry? selected = FindTest(command);
            if (selected is not null) {
                RunResult result = RunTest(selected, args.Skip(1).ToArray());
                return result.ReturnCode == 0 ? 0 : 1;
            }

            Console.Error.WriteLine($"Unbekannter Test: {command}");
            return 2;
        }

        Console.WriteLine("TKFaxEngine Test OneExe");
        Console.WriteLine($"Tests enthalten: {Tests.Length}");
        return RunInteractiveMenu();
    }

    private static int RunInteractiveMenu() {
        for (; ; )
        {
            Console.WriteLine("\nTKFaxEngine Test-Menue");
            Console.WriteLine("1 = Core Tests starten");
            Console.WriteLine("2 = Audio Tests starten");
            Console.WriteLine("3 = Modem Tests starten");
            Console.WriteLine("4 = Fax / T.30 Tests starten");
            Console.WriteLine("5 = T.38 Tests starten");
            Console.WriteLine("6 = Image / Compression Tests starten");
            Console.WriteLine("7 = Network / Packet / Simulation Tests starten");
            Console.WriteLine("8 = Utility / External Harness Tests starten");
            Console.WriteLine("9 = Alle Tests starten");
            Console.WriteLine("10 = Tests nach Gruppen anzeigen");
            Console.WriteLine("11 = Alle Tests auflisten");
            Console.WriteLine("12 = Einzeltest starten");
            Console.WriteLine("13 = Test-Files generieren");
            Console.Write("0 = Beenden\nAuswahl: ");
            string? choice = Console.ReadLine();
            if (choice is null or "0")
                return 0;

            if (int.TryParse(choice, NumberStyles.None, CultureInfo.InvariantCulture, out int groupIndex) && groupIndex is >= 1 and <= 8) {
                RunGroupTests(Groups[groupIndex - 1]);
                PauseToMenu();
                continue;
            }

            switch (choice) {
                case "9":
                    RunAllTests();
                    PauseToMenu();
                    break;
                case "10":
                    ListGroups();
                    Console.Write("Gruppe anzeigen (Nummer/Name, leer = zurueck): ");
                    string key = Console.ReadLine() ?? "";
                    if (key.Length != 0) {
                        TestGroup? group = int.TryParse(key, out int index) && index >= 1 && index <= Groups.Length
                            ? Groups[index - 1]
                            : FindGroup(key);
                        if (group is null)
                            Console.WriteLine("Gruppe nicht gefunden.");
                        else
                            ListTestsForGroup(group);
                        PauseToMenu();
                    }
                    break;
                case "11":
                    ListTests();
                    PauseToMenu();
                    break;
                case "12":
                    Console.Write("Test-Nummer, ID oder Dateiname: ");
                    TestEntry? test = FindTest(Console.ReadLine() ?? "");
                    if (test is null) {
                        Console.WriteLine("Nicht gefunden.");
                        break;
                    }
                    Console.Write("Argumente fuer diesen Test (leer = keine): ");
                    RunTest(test, SplitArgs(Console.ReadLine() ?? ""));
                        PauseToMenu();
                    break;
                case "13":
                    ShowGenerateTestFilesMenu();
                    break;
                default:
                    Console.WriteLine("Ungueltige Auswahl.");
                    break;
            }
        }
    }

    private static string[] SplitArgs(string line) {
        List<string> args = new();
        StringBuilder current = new();
        bool quoted = false;
        foreach (char value in line) {
            if (value == '"') {
                quoted = !quoted;
                continue;
            }
            if (!quoted && char.IsWhiteSpace(value)) {
                if (current.Length != 0) {
                    args.Add(current.ToString());
                    current.Clear();
                }
            } else {
                current.Append(value);
            }
        }
        if (current.Length != 0)
            args.Add(current.ToString());
        return args.ToArray();
    }

    private static void ConfigureChildAssertions() {
        // Native CRT assertion routing is unnecessary in managed C#; unhandled
        // exceptions naturally reach redirected stderr and the child exit code.
    }

    private static void ResetProcessGlobalTestState() {
        AllocApi.span_mem_allocators(null, null, null, null, null);
    }

    private static void ConsolePlainStatus(string status) {
        ConsoleColor previous = Console.ForegroundColor;
        Console.ForegroundColor = status switch {
            "PASS" or "OK" => ConsoleColor.Green,
            "FAIL" or "TIMEOUT" or "ERROR" => ConsoleColor.Red,
            "SKIP" or "NOTIMPL" or "WARN" => ConsoleColor.Yellow,
            "RUN" or "STEP" => ConsoleColor.White,
            _ => previous
        };
        Console.Write(status);
        Console.ForegroundColor = previous;
    }

    private static void ConsoleResultLine(string status, string text) {
        ConsolePlainStatus(status);
        Console.WriteLine(text);
    }

    private static void ConsoleSummaryLine(string prefix, int passed, int failed, int skipped, int notImplemented, int total) {
        Console.Write(prefix);
        ConsolePlainStatus("PASS");
        Console.Write($"={passed}  ");
        ConsolePlainStatus("FAIL");
        Console.Write($"={failed}  ");
        ConsolePlainStatus("SKIP");
        Console.Write($"={skipped}  ");
        ConsolePlainStatus("NOTIMPL");
        Console.WriteLine($"={notImplemented}  von {total}");
    }

    private static string FormatElapsedTime(long elapsedMs, bool includeMilliseconds = false) {
        TimeSpan value = TimeSpan.FromMilliseconds(Math.Max(0, elapsedMs));
        string result = $"{(long)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";
        return includeMilliseconds ? $"{result}.{value.Milliseconds:000}" : result;
    }

    private static void ConsoleRunningTimer(string testFile, long elapsedMs) {
        string value = $"RUN  {testFile} | Laufzeit {FormatElapsedTime(elapsedMs)}";
        int width = Math.Max(_runningTimerWidth, value.Length);
        Console.Write('\r');
        ConsolePlainStatus("RUN");
        Console.Write(value[3..]);
        if (width > value.Length)
            Console.Write(new string(' ', width - value.Length));
        _runningTimerWidth = width;
    }

    private static void ConsoleClearRunningTimer() {
        if (_runningTimerWidth == 0)
            return;
        Console.Write('\r');
        Console.Write(new string(' ', _runningTimerWidth));
        Console.Write('\r');
        _runningTimerWidth = 0;
    }

    private static bool IsRunRootDirectory(string path) {
        string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string? parent = Path.GetDirectoryName(full);
        if (!string.Equals(parent, Path.GetFullPath(_exeDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            return false;

        string name = Path.GetFileName(full);
        if (name.Length < 19)
            return false;
        if (!DateTime.TryParseExact(name[..19], "yyyy.MM.dd_HH.mm.ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return false;
        return name.Length == 19 || name[19] == '_';
    }

    private static bool IsArtifactScanExcludedDirectory(string path) {
        string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(full, Path.GetFullPath(_runRootDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            || IsRunRootDirectory(full)
            || string.Equals(full, Path.GetFullPath(Path.Combine(_exeDir, "Logs")).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            || string.Equals(full, Path.GetFullPath(Path.Combine(_exeDir, "Ausgabe")).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            || string.Equals(full, Path.GetFullPath(_testsDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, TestArtifactFileState> CaptureTestArtifactSnapshot() {
        Dictionary<string, TestArtifactFileState> snapshot = new(StringComparer.OrdinalIgnoreCase);
        if (_exeDir.Length == 0 || !Directory.Exists(_exeDir))
            return snapshot;

        Stack<string> pending = new();
        pending.Push(_exeDir);
        while (pending.Count != 0) {
            string directory = pending.Pop();
            IEnumerable<string> directories;
            IEnumerable<string> files;
            try {
                directories = Directory.EnumerateDirectories(directory).ToArray();
                files = Directory.EnumerateFiles(directory).ToArray();
            } catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) {
                continue;
            }

            foreach (string child in directories) {
                if (!IsArtifactScanExcludedDirectory(child))
                    pending.Push(child);
            }

            foreach (string file in files) {
                try {
                    FileInfo info = new(file);
                    string relative = Path.GetRelativePath(_exeDir, file).Replace(Path.DirectorySeparatorChar, '/');
                    snapshot[relative] = new TestArtifactFileState(info.Length, info.LastWriteTimeUtc);
                } catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) {
                    // Match native skip-permission-denied traversal.
                }
            }
        }

        return snapshot;
    }

    private static int RunGeneratorCommand(string generatorId, Func<int> generator) {
        EnsureLogOpen();
        AppendLogLine($"GENERATOR BEGIN id={generatorId} target={_testsDir}");

        int returnCode = generator();

        AppendLogLine($"GENERATOR END id={generatorId} rc={returnCode} target={_testsDir}");
        return returnCode;
    }

    private static TestArtifactCollectionResult CollectChangedTestArtifacts(
        string testId,
        IReadOnlyDictionary<string, TestArtifactFileState> before) {
        int moved = 0;
        int failed = 0;
        Dictionary<string, TestArtifactFileState> after = CaptureTestArtifactSnapshot();
        foreach ((string relative, TestArtifactFileState state) in after) {
            bool changed = !before.TryGetValue(relative, out TestArtifactFileState? old)
                || old.Size != state.Size
                || old.WriteTimeUtc != state.WriteTimeUtc;
            if (!changed)
                continue;

            string source = Path.Combine(_exeDir, relative.Replace('/', Path.DirectorySeparatorChar));
            string testOutputDirectory = Path.Combine(_outputDir, SanitizeFilename(testId));
            string destination = Path.Combine(testOutputDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? testOutputDirectory);
                MoveFileReplacing(source, destination);
                moved++;
                AppendLogLine($"OUTPUT MOVED test={testId} file={destination}");
            } catch (Exception ex) {
                failed++;
                AppendLogLine($"OUTPUT FAIL test={testId} source={source} destination={destination} error={ex.Message}");
            }
        }
        return new TestArtifactCollectionResult(moved, failed);
    }

    private static void MoveFileReplacing(string source, string destination) {
        Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? ".");
        try {
            File.Move(source, destination, true);
        } catch (IOException) {
            File.Copy(source, destination, true);
            File.Delete(source);
        }
    }

    private static bool ValidatePcm16MonoWav(string path, int sampleRate, long expectedFrames, out string error) {
        error = "";
        try {
            using FileStream stream = File.OpenRead(path);
            using BinaryReader reader = new(stream, Encoding.ASCII, leaveOpen: true);
            if (stream.Length < 12 || Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF") {
                error = $"Kein WAV-Container: {path}";
                return false;
            }
            _ = reader.ReadUInt32();
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE") {
                error = $"Kein WAV-Container: {path}";
                return false;
            }

            ushort formatTag = 0;
            ushort channels = 0;
            int actualSampleRate = 0;
            ushort blockAlign = 0;
            ushort bitsPerSample = 0;
            long dataBytes = -1;

            while (stream.Position + 8 <= stream.Length) {
                string chunkId = Encoding.ASCII.GetString(reader.ReadBytes(4));
                uint chunkLength = reader.ReadUInt32();
                long chunkStart = stream.Position;
                if (chunkStart + chunkLength > stream.Length) {
                    error = $"Ungueltiger WAV-Chunk: {path}";
                    return false;
                }

                if (chunkId == "fmt " && chunkLength >= 16) {
                    formatTag = reader.ReadUInt16();
                    channels = reader.ReadUInt16();
                    actualSampleRate = reader.ReadInt32();
                    _ = reader.ReadInt32();
                    blockAlign = reader.ReadUInt16();
                    bitsPerSample = reader.ReadUInt16();
                } else if (chunkId == "data") {
                    dataBytes = chunkLength;
                }

                stream.Position = chunkStart + chunkLength + (chunkLength & 1U);
            }

            if (formatTag != 1 || bitsPerSample != 16) {
                error = $"Kein PCM16-WAV: {path}";
                return false;
            }
            long frames = blockAlign == 0 || dataBytes < 0 ? -1 : dataBytes / blockAlign;
            if (actualSampleRate != sampleRate || channels != 1 || frames != expectedFrames) {
                error = $"Falsche WAV-Eigenschaften: {path} | samplerate={actualSampleRate} channels={channels} frames={frames} | erwartet samplerate={sampleRate} channels=1 frames={expectedFrames}";
                return false;
            }
            return true;
        } catch (Exception ex) {
            error = $"Datei kann nicht als Audiodatei geoeffnet werden: {path} | {ex.Message}";
            return false;
        }
    }

    private static bool ValidateG168CssInputs(bool writeLog) {
        string localDirectory = Path.Combine(_projectRoot, "Test-Files", "local");
        bool c1Ok = ValidatePcm16MonoWav(Path.Combine(localDirectory, "sound_c1_8k.wav"), 8000, 5600, out string c1Error);
        bool c3Ok = ValidatePcm16MonoWav(Path.Combine(localDirectory, "sound_c3_8k.wav"), 8000, 6400, out string c3Error);
        if (writeLog) {
            AppendLogLine($"G168 C1 WAV {(c1Ok ? "PASS" : "FAIL")}{(c1Error.Length == 0 ? "" : " | " + c1Error)}");
            AppendLogLine($"G168 C3 WAV {(c3Ok ? "PASS" : "FAIL")}{(c3Error.Length == 0 ? "" : " | " + c3Error)}");
        }
        if (!c1Ok)
            Console.Error.WriteLine($"FEHLER: {c1Error}");
        if (!c3Ok)
            Console.Error.WriteLine($"FEHLER: {c3Error}");
        return c1Ok && c3Ok;
    }

    private static bool tkfx_pbm_next_token(byte[] data, ref int pos, out string token) {
        token = "";
        while (pos < data.Length) {
            byte c = data[pos];
            if (c == (byte)'#') {
                while (pos < data.Length && data[pos] != (byte)'\n' && data[pos] != (byte)'\r')
                    ++pos;
                continue;
            }
            if (!char.IsWhiteSpace((char)c))
                break;
            ++pos;
        }
        if (pos >= data.Length)
            return false;

        int start = pos;
        while (pos < data.Length) {
            byte c = data[pos];
            if (char.IsWhiteSpace((char)c) || c == (byte)'#')
                break;
            ++pos;
        }
        token = Encoding.ASCII.GetString(data, start, pos - start);
        return token.Length != 0;
    }

    private static bool tkfx_read_pbm_p4(string path, out TkfxPbmImage image, out string error) {
        image = new TkfxPbmImage();
        error = "";

        byte[] data;
        try {
            data = File.ReadAllBytes(path);
        } catch {
            error = $"PBM fehlt: {path}";
            return false;
        }

        int pos = 0;
        if (!tkfx_pbm_next_token(data, ref pos, out string tok) || tok != "P4") {
            error = $"PBM ist nicht binaer P4: {path}";
            return false;
        }
        if (!tkfx_pbm_next_token(data, ref pos, out tok)
            || !uint.TryParse(tok, NumberStyles.None, CultureInfo.InvariantCulture, out uint width)) {
            error = $"PBM Breite fehlt: {path}";
            return false;
        }
        image.Width = width;
        if (!tkfx_pbm_next_token(data, ref pos, out tok)
            || !uint.TryParse(tok, NumberStyles.None, CultureInfo.InvariantCulture, out uint height)) {
            error = $"PBM Hoehe fehlt: {path}";
            return false;
        }
        image.Height = height;

        while (pos < data.Length && char.IsWhiteSpace((char)data[pos]))
            ++pos;
        if (image.Width == 0 || image.Height == 0) {
            error = $"PBM Groesse ungueltig: {path}";
            return false;
        }

        int stride;
        int needed;
        try {
            stride = checked((int)(((ulong)image.Width + 7UL) / 8UL));
            needed = checked(stride * (int)image.Height);
        } catch (OverflowException) {
            error = $"PBM Groesse ungueltig: {path}";
            return false;
        }
        if ((long)pos + needed > data.Length) {
            error = $"PBM Raster zu kurz: {path}";
            return false;
        }

        image.Stride = stride;
        image.Pixels = new byte[needed];
        Buffer.BlockCopy(data, pos, image.Pixels, 0, needed);
        return true;
    }

    private static int GetTiffIntField(Tiff tiff, TiffTag tag, int fallback) {
        FieldValue[]? field = tiff.GetField(tag);
        return field is { Length: > 0 } ? field[0].ToInt() : fallback;
    }

    private static ushort GetTiffUShortField(Tiff tiff, TiffTag tag, ushort fallback) {
        FieldValue[]? field = tiff.GetField(tag);
        return field is { Length: > 0 } ? field[0].ToUShort() : fallback;
    }

    private static float GetTiffFloatField(Tiff tiff, TiffTag tag, float fallback) {
        FieldValue[]? field = tiff.GetField(tag);
        return field is { Length: > 0 } ? field[0].ToFloat() : fallback;
    }

    private static void SetFaxTiffPageFields(
        Tiff tiff,
        int width,
        int height,
        float xResolution,
        float yResolution,
        ResUnit resolutionUnit,
        FillOrder fillOrder,
        int pageIndex,
        int pageCount) {
        tiff.SetField(TiffTag.IMAGEWIDTH, width);
        tiff.SetField(TiffTag.IMAGELENGTH, height);
        tiff.SetField(TiffTag.BITSPERSAMPLE, 1);
        tiff.SetField(TiffTag.SAMPLESPERPIXEL, 1);
        tiff.SetField(TiffTag.COMPRESSION, Compression.CCITTFAX4);
        tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISWHITE);
        tiff.SetField(TiffTag.FILLORDER, fillOrder);
        tiff.SetField(TiffTag.ORIENTATION, Orientation.TOPLEFT);
        tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
        tiff.SetField(TiffTag.XRESOLUTION, xResolution > 0 ? xResolution : 204.0f);
        tiff.SetField(TiffTag.YRESOLUTION, yResolution > 0 ? yResolution : 196.0f);
        tiff.SetField(TiffTag.RESOLUTIONUNIT, resolutionUnit);
        tiff.SetField(TiffTag.ROWSPERSTRIP, Math.Min(height, 256));
        tiff.SetField(TiffTag.PAGENUMBER, pageIndex, pageCount);
        tiff.SetField(TiffTag.CLEANFAXDATA, CleanFaxData.CLEAN);
        tiff.SetField(TiffTag.SOFTWARE, "TKFaxEngineTest");
        tiff.SetField(TiffTag.HOSTCOMPUTER, Environment.MachineName);
        tiff.SetField(TiffTag.DATETIME, DateTime.Now.ToString("yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture));
    }

    private static bool tkfx_write_tiff_pages(
        string path,
        IReadOnlyList<(string Name, TkfxPbmImage Image)> pages,
        out string error) {
        if (pages.Count == 0) {
            error = $"Keine TIFF-Seiten zu schreiben: {path}";
            return false;
        }

        error = "";
        Tiff? tif = null;
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            tif = Tiff.Open(path, "w");
            if (tif is null) {
                error = $"TIFF konnte nicht geoeffnet werden: {path}";
                return false;
            }

            bool ok = true;
            ushort pageCount = checked((ushort)pages.Count);
            for (ushort pageNo = 0; pageNo < pageCount; ++pageNo) {
                string name = pages[pageNo].Name;
                TkfxPbmImage img = pages[pageNo].Image;
                int width = checked((int)img.Width);
                int height = checked((int)img.Height);

                tif.SetField(TiffTag.IMAGEWIDTH, width);
                tif.SetField(TiffTag.IMAGELENGTH, height);
                tif.SetField(TiffTag.BITSPERSAMPLE, 1);
                tif.SetField(TiffTag.SAMPLESPERPIXEL, 1);
                tif.SetField(TiffTag.COMPRESSION, Compression.CCITTFAX4);
                tif.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISWHITE);
                tif.SetField(TiffTag.FILLORDER, FillOrder.MSB2LSB);
                tif.SetField(TiffTag.ORIENTATION, Orientation.TOPLEFT);
                tif.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
                tif.SetField(TiffTag.ROWSPERSTRIP, height);
                tif.SetField(TiffTag.RESOLUTIONUNIT, ResUnit.INCH);
                tif.SetField(TiffTag.XRESOLUTION, 204.0f);
                tif.SetField(TiffTag.YRESOLUTION, 196.0f);
                tif.SetField(TiffTag.SOFTWARE, "TKFaxEngineTest");
                tif.SetField(TiffTag.IMAGEDESCRIPTION, name);
                tif.SetField(TiffTag.SUBFILETYPE, 2);
                tif.SetField(TiffTag.PAGENUMBER, pageNo, pageCount);

                byte[] rowBuffer = new byte[img.Stride];
                for (uint row = 0; row < img.Height; ++row) {
                    Buffer.BlockCopy(img.Pixels, checked((int)row * img.Stride), rowBuffer, 0, img.Stride);
                    if (!tif.WriteScanline(rowBuffer, checked((int)row), 0)) {
                        error = $"TIFF Schreibfehler bei {path} Seite {name}";
                        ok = false;
                        break;
                    }
                }
                if (!ok)
                    break;
                if (!tif.WriteDirectory()) {
                    error = $"TIFF Directory Schreibfehler: {path}";
                    ok = false;
                    break;
                }
            }
            return ok;
        } catch (Exception ex) {
            error = $"TIFF Schreibfehler bei {path}: {ex.Message}";
            return false;
        } finally {
            tif?.Close();
        }
    }

    private static int InvokeGeneratorInDirectory(string label, TestMain function, string workingDirectory) {
        string scope = $"GENERATOR {label}";
        AppendLogLine($"GENERATOR BEGIN {label}");
        try {
            AppendLogStep(scope, 1, $"Zielordner anlegen/pruefen: {workingDirectory}");
            Directory.CreateDirectory(workingDirectory);
            string oldDirectory = Environment.CurrentDirectory;
            AppendLogStep(scope, 2, $"Altes Arbeitsverzeichnis={oldDirectory}");
            Environment.CurrentDirectory = workingDirectory;
            try {
                ResetProcessGlobalTestState();
                AppendLogStep(scope, 3, "Process-Global-State vor Generator zurueckgesetzt");
                int result = tkfx_oneexe_test_invoke(function.Invoke, Array.Empty<string>());
                AppendLogStep(scope, 4, $"Generator Rueckgabecode={result}");
                ResetProcessGlobalTestState();
                if (result == 0) {
                    ConsoleResultLine("PASS", $"  {label}");
                    AppendLogLine($"GENERATE PASS {label}");
                } else {
                    ConsoleResultLine("FAIL", $"  {label} RC={result}");
                    AppendLogLine($"GENERATE FAIL {label} RC={result}");
                }
                AppendLogLine($"GENERATOR END {label} result={(result == 0 ? "PASS" : "FAIL")} rc={result}");
                return result;
            } finally {
                Environment.CurrentDirectory = oldDirectory;
                AppendLogStep(scope, 5, $"Arbeitsverzeichnis wiederhergestellt: {oldDirectory}");
            }
        } catch (Exception ex) {
            ConsoleResultLine("FAIL", $"  {label} - {ex.Message}");
            AppendLogLine($"GENERATE FAIL {label} exception={ex}");
            AppendLogLine($"GENERATOR END {label} result=FAIL rc=2");
            return 2;
        }
    }

    private static int GenerateG168CssTestFiles() {
        const string scope = "G.168 CSS WAV PRUEFUNG";
        AppendLogLine("GENERATOR BEGIN G.168 CSS WAV VALIDATION");
        AppendLogStep(scope, 1, "Pruefe Test-Files/local/sound_c1_8k.wav als WAV PCM16 Mono 8000 Hz mit 5600 Frames");
        AppendLogStep(scope, 2, "Pruefe Test-Files/local/sound_c3_8k.wav als WAV PCM16 Mono 8000 Hz mit 6400 Frames");
        bool valid = ValidateG168CssInputs(true);
        Console.WriteLine("\nTest-Files pruefen: G.168 CSS WAV");
        ConsoleResultLine(valid ? "PASS" : "FAIL", "  sound_c1_8k.wav / sound_c3_8k.wav");
        AppendLogLine($"GENERATOR END G.168 CSS WAV VALIDATION result={(valid ? "PASS rc=0" : "FAIL rc=1")}");
        return valid ? 0 : 1;
    }

    private static int generate_itu_fax_pbm_tiff_test_files() {
        const string scope = "GENERATOR ITU Fax PBM->TIFF";
        AppendLogLine("GENERATOR BEGIN ITU Fax PBM->TIFF");
        string faxDirectory = Path.Combine(_projectRoot, "Test-Files", "itu", "fax");
        AppendLogStep(scope, 1, $"Zielordner={faxDirectory}");
        string[] names = {
            "itu1", "itu2", "itu3", "itu4",
            "itu5", "itu6", "itu7", "itu8",
            "test1", "test2", "test3", "test4"
        };
        List<(string Name, TkfxPbmImage Image)> allPages = new();
        int generated = 0;
        int failed = 0;
        Console.WriteLine("\nTest-Files generieren: ITU Fax PBM -> TIFF");
        Console.WriteLine($"Ziel: {faxDirectory}");
        foreach (string name in names) {
            string pbm = Path.Combine(faxDirectory, name + ".pbm");
            string tif = Path.Combine(faxDirectory, name + ".tif");
            AppendLogStep(scope, 2, $"PBM lesen: {pbm}");
            if (!tkfx_read_pbm_p4(pbm, out TkfxPbmImage image, out string error)) {
                ++failed;
                ConsoleResultLine("FAIL", $"  {name}.pbm fehlt - {error}");
                AppendLogLine($"GENERATE FAIL {name}.tif {error}");
                continue;
            }
            AppendLogStep(scope, 3, $"{name}.pbm gelesen width={image.Width} height={image.Height} stride={image.Stride}");
            allPages.Add((name, image));
            AppendLogStep(scope, 4, $"TIFF schreiben: {tif}");
            if (tkfx_write_tiff_pages(tif, new[] { (name, image) }, out error)) {
                ++generated;
                ConsoleResultLine("PASS", $"  {name}.tif");
                AppendLogLine($"GENERATE PASS {Path.Combine(faxDirectory, name + ".tif")}");
            } else {
                ++failed;
                ConsoleResultLine("FAIL", $"  {name}.tif - {error}");
                AppendLogLine($"GENERATE FAIL {name}.tif {error}");
            }
        }

        string finalError = "";
        AppendLogStep(scope, 5, $"Multipage itutests.tif schreiben, pages={allPages.Count}");
        if (allPages.Count == names.Length
            && tkfx_write_tiff_pages(Path.Combine(faxDirectory, "itutests.tif"), allPages, out finalError)) {
            ++generated;
            ConsoleResultLine("PASS", "  itutests.tif");
            AppendLogLine($"GENERATE PASS {Path.Combine(faxDirectory, "itutests.tif")}");
        } else {
            ++failed;
            if (finalError.Length == 0)
                finalError = "Nicht alle PBM-Einzelseiten vorhanden.";
            ConsoleResultLine("FAIL", $"  itutests.tif - {finalError}");
            AppendLogLine($"GENERATE FAIL itutests.tif {finalError}");
        }

        List<(string Name, TkfxPbmImage Image)> hundredPages = new();
        AppendLogStep(scope, 6, "100pages.tif vorbereiten");
        if (allPages.Count == names.Length) {
            for (int r = 0; r < 8; ++r)
                hundredPages.AddRange(allPages);
            hundredPages.AddRange(allPages.Take(4));
            if (tkfx_write_tiff_pages(Path.Combine(faxDirectory, "100pages.tif"), hundredPages, out finalError)) {
                ++generated;
                ConsoleResultLine("PASS", "  100pages.tif");
                AppendLogLine($"GENERATE PASS {Path.Combine(faxDirectory, "100pages.tif")}");
            } else {
                ++failed;
                ConsoleResultLine("FAIL", $"  100pages.tif - {finalError}");
                AppendLogLine($"GENERATE FAIL 100pages.tif {finalError}");
            }
        } else {
            ++failed;
            ConsoleResultLine("FAIL", "  100pages.tif - Nicht alle PBM-Einzelseiten vorhanden.");
            AppendLogLine("GENERATE FAIL 100pages.tif missing PBM pages");
        }

        Console.Write("\nITU PBM->TIFF fertig. ");
        ConsolePlainStatus("PASS");
        Console.Write($"={generated}  ");
        ConsolePlainStatus("FAIL");
        Console.WriteLine($"={failed}");
        AppendLogLine($"GENERATOR END ITU Fax PBM->TIFF result={(failed != 0 ? "FAIL" : "PASS")} generated={generated} failed={failed}");
        return failed == 0 ? 0 : 1;
    }

    private static string[] MixedSizePageNames() => new[]
    {
        "bilevel_R8_385_A4.tif", "bilevel_R8_385_B4.tif", "bilevel_R8_385_A3.tif",
        "bilevel_R8_77_A4.tif", "bilevel_R8_77_B4.tif", "bilevel_R8_77_A3.tif",
        "bilevel_R8_154_A4.tif", "bilevel_R8_154_B4.tif", "bilevel_R8_154_A3.tif",
        "bilevel_R16_154_A4.tif", "bilevel_R16_154_B4.tif", "bilevel_R16_154_A3.tif",
        "bilevel_200_100_A4.tif", "bilevel_200_100_B4.tif", "bilevel_200_100_A3.tif",
        "bilevel_200_200_A4.tif", "bilevel_200_200_B4.tif", "bilevel_200_200_A3.tif",
        "bilevel_200_400_A4.tif", "bilevel_200_400_B4.tif", "bilevel_200_400_A3.tif",
        "bilevel_300_300_A4.tif", "bilevel_300_300_B4.tif", "bilevel_300_300_A3.tif",
        "bilevel_300_600_A4.tif", "bilevel_300_600_B4.tif", "bilevel_300_600_A3.tif",
        "bilevel_400_400_A4.tif", "bilevel_400_400_B4.tif", "bilevel_400_400_A3.tif",
        "bilevel_400_800_A4.tif", "bilevel_400_800_B4.tif", "bilevel_400_800_A3.tif",
        "bilevel_600_600_A4.tif", "bilevel_600_600_B4.tif", "bilevel_600_600_A3.tif",
        "bilevel_600_1200_A4.tif", "bilevel_600_1200_B4.tif", "bilevel_600_1200_A3.tif",
        "bilevel_1200_1200_A4.tif", "bilevel_1200_1200_B4.tif", "bilevel_1200_1200_A3.tif"
    };

    private static bool CombineTiffFirstPages(string directory, IReadOnlyList<string> inputNames, string output, out string error) {
        error = "";
        if (inputNames.Count == 0) {
            error = $"Keine Eingabe-TIFFs angegeben: {output}";
            return false;
        }

        Tiff? outputTiff = null;
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");
            outputTiff = Tiff.Open(output, "w");
            if (outputTiff is null) {
                error = $"Ausgabe-TIFF konnte nicht geoeffnet werden: {output}";
                return false;
            }

            for (int pageIndex = 0; pageIndex < inputNames.Count; pageIndex++) {
                string name = inputNames[pageIndex];
                string inputPath = Path.Combine(directory, name);
                if (!File.Exists(inputPath)) {
                    error = $"Eingabe-TIFF fehlt: {inputPath}";
                    return false;
                }

                Tiff? inputTiff = Tiff.Open(inputPath, "r");
                if (inputTiff is null) {
                    error = $"Eingabe-TIFF konnte nicht geoeffnet werden: {inputPath}";
                    return false;
                }

                try {
                    int width = GetTiffIntField(inputTiff, TiffTag.IMAGEWIDTH, 0);
                    int height = GetTiffIntField(inputTiff, TiffTag.IMAGELENGTH, 0);
                    ushort bitsPerSample = GetTiffUShortField(inputTiff, TiffTag.BITSPERSAMPLE, 1);
                    ushort samplesPerPixel = GetTiffUShortField(inputTiff, TiffTag.SAMPLESPERPIXEL, 1);
                    if (width <= 0 || height <= 0 || bitsPerSample != 1 || samplesPerPixel != 1) {
                        error = $"Eingabe-TIFF ist keine gueltige 1-Bit-Faxseite: {inputPath}";
                        return false;
                    }

                    float xResolution = GetTiffFloatField(inputTiff, TiffTag.XRESOLUTION, 204.0f);
                    float yResolution = GetTiffFloatField(inputTiff, TiffTag.YRESOLUTION, 196.0f);
                    ResUnit resolutionUnit = (ResUnit)GetTiffUShortField(inputTiff, TiffTag.RESOLUTIONUNIT, (ushort)ResUnit.INCH);
                    FillOrder fillOrder = (FillOrder)GetTiffUShortField(inputTiff, TiffTag.FILLORDER, (ushort)FillOrder.MSB2LSB);

                    SetFaxTiffPageFields(
                        outputTiff,
                        width,
                        height,
                        xResolution,
                        yResolution,
                        resolutionUnit,
                        fillOrder,
                        pageIndex,
                        inputNames.Count);
                    outputTiff.SetField(TiffTag.IMAGEDESCRIPTION, name);

                    int inputStride = inputTiff.ScanlineSize();
                    int outputStride = outputTiff.ScanlineSize();
                    if (inputStride <= 0 || outputStride <= 0) {
                        error = $"Ungueltige TIFF-Scanline-Groesse: {inputPath}";
                        return false;
                    }

                    byte[] inputRow = new byte[inputStride];
                    byte[] outputRow = new byte[outputStride];
                    for (int y = 0; y < height; y++) {
                        if (!inputTiff.ReadScanline(inputRow, y, 0)) {
                            error = $"Eingabe-TIFF-Zeile konnte nicht gelesen werden: {inputPath}, Zeile={y}";
                            return false;
                        }

                        Array.Clear(outputRow);
                        Buffer.BlockCopy(inputRow, 0, outputRow, 0, Math.Min(inputRow.Length, outputRow.Length));
                        if (!outputTiff.WriteScanline(outputRow, y, 0)) {
                            error = $"Ausgabe-TIFF-Zeile konnte nicht geschrieben werden: {output}, Seite={pageIndex}, Zeile={y}";
                            return false;
                        }
                    }

                    if (!outputTiff.WriteDirectory()) {
                        error = $"Ausgabe-TIFF-Verzeichnis konnte nicht geschrieben werden: {output}, Seite={pageIndex}";
                        return false;
                    }
                } finally {
                    inputTiff.Close();
                }
            }

            return true;
        } catch (Exception ex) {
            error = $"TIFF Kopierfehler: {ex.Message}";
            return false;
        } finally {
            outputTiff?.Close();
        }
    }

    private static int GenerateItuFaxToolTestFiles() {
        string faxDirectory = Path.Combine(_projectRoot, "Test-Files", "itu", "fax");
        int failed = 0;
        Console.WriteLine("\nTest-Files generieren: ITU Fax Zusatzgeneratoren");
        failed += InvokeGeneratorInDirectory("generate_dithered_tif -> dithered.tif", GenerateDitheredTifAdapter.tkfx_generate_dithered_tif_main, faxDirectory) == 0 ? 0 : 1;
        failed += InvokeGeneratorInDirectory("generate_sized_pages -> bilevel_*.tif", GenerateSizedPagesAdapter.tkfx_generate_sized_pages_main, faxDirectory) == 0 ? 0 : 1;
        failed += InvokeGeneratorInDirectory("generate_striped_pages -> striped.tif", GenerateStripedPagesAdapter.tkfx_generate_striped_pages_main, faxDirectory) == 0 ? 0 : 1;
        string output = Path.Combine(faxDirectory, "mixed_size_pages.tif");
        if (CombineTiffFirstPages(faxDirectory, MixedSizePageNames(), output, out string error)) {
            ConsoleResultLine("PASS", "  mixed_size_pages.tif");
            AppendLogLine($"GENERATE PASS {output}");
        } else {
            failed++;
            ConsoleResultLine("FAIL", $"  mixed_size_pages.tif - {error}");
            AppendLogLine($"GENERATE FAIL mixed_size_pages.tif {error}");
        }
        return failed == 0 ? 0 : 1;
    }

    private static int GenerateEtsi300242TestFiles() {
        string faxDirectory = Path.Combine(_projectRoot, "Test-Files", "etsi", "fax");
        Console.WriteLine("\nTest-Files generieren: ETSI ETS 300 242 Faxseiten");
        return InvokeGeneratorInDirectory("generate_etsi_300_242_pages -> etsi_300_242_*.tif", GenerateEtsi300242PagesAdapter.tkfx_generate_etsi_300_242_pages_main, faxDirectory) == 0 ? 0 : 1;
    }

    private static int GenerateAllTestFiles() {
        EnsureLogOpen();
        int failed = 0;
        failed += GenerateG168CssTestFiles() == 0 ? 0 : 1;
        failed += generate_itu_fax_pbm_tiff_test_files() == 0 ? 0 : 1;
        failed += GenerateItuFaxToolTestFiles() == 0 ? 0 : 1;
        failed += GenerateEtsi300242TestFiles() == 0 ? 0 : 1;
        Console.Write("\nAlle Generatoren fertig. ");
        ConsolePlainStatus(failed == 0 ? "PASS" : "FAIL");
        Console.WriteLine($"={failed}");
        return failed == 0 ? 0 : 1;
    }

    private static void ShowGenerateTestFilesMenu() {
        for (; ; )
        {
            Console.WriteLine("\nTest-Files generieren");
            Console.WriteLine("1 = G.168 CSS WAV pruefen");
            Console.WriteLine("2 = ITU Fax PBM -> TIFF erzeugen");
            Console.WriteLine("3 = ITU Fax Zusatzseiten erzeugen");
            Console.WriteLine("4 = ETSI ETS 300 242 Faxseiten erzeugen");
            Console.WriteLine("5 = Alles erzeugen");
            Console.Write("0 = Zurueck\nAuswahl: ");
            string choice = Console.ReadLine() ?? "";
            EnsureLogOpen();
            if (choice is "0" or "")
                return;
            switch (choice) {
                case "1": RunGeneratorCommand("generate-g168-css", GenerateG168CssTestFiles); break;
                case "2": RunGeneratorCommand("generate-itu-pbm-tiff", generate_itu_fax_pbm_tiff_test_files); break;
                case "3": RunGeneratorCommand("generate-itu-fax-pages", GenerateItuFaxToolTestFiles); break;
                case "4": RunGeneratorCommand("generate-etsi-300-242", GenerateEtsi300242TestFiles); break;
                case "5": RunGeneratorCommand("generate-test-files", GenerateAllTestFiles); break;
                default: Console.WriteLine("Ungueltige Auswahl."); continue;
            }
            PauseToMenu();
        }
    }

    private static string NowStamp(bool forFile) =>
        DateTime.Now.ToString(forFile ? "yyyyMMdd_HHmmss" : "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static string RunFolderStamp() =>
        DateTime.Now.ToString("yyyy.MM.dd_HH.mm.ss", CultureInfo.InvariantCulture);

    private static string CreateUniqueRunRootDirectory(string baseDirectory) {
        string stamp = RunFolderStamp();
        for (int suffix = 0; suffix < 1000; suffix++) {
            string name = suffix == 0 ? stamp : $"{stamp}_{suffix:000}";
            string candidate = Path.Combine(baseDirectory, name);
            if (Directory.Exists(candidate))
                continue;
            Directory.CreateDirectory(candidate);
            return candidate;
        }
        throw new IOException($"Kein eindeutiger Laufordner fuer Zeitstempel {stamp} konnte angelegt werden.");
    }

    private static void InitializeConsoleCapture() {
        if (_consoleCaptureWriter is not null)
            return;

        Directory.CreateDirectory(_logDir);
        string consoleLogFile = Path.Combine(_logDir, "Konsole.log");
        _originalConsoleOut = Console.Out;
        _originalConsoleError = Console.Error;
        _consoleCaptureWriter = new StreamWriter(
            new FileStream(consoleLogFile, FileMode.Create, FileAccess.Write, FileShare.Read),
            new UTF8Encoding(false)) {
            NewLine = "\r\n",
            AutoFlush = true
        };
        Console.SetOut(new TeeTextWriter(_originalConsoleOut, _consoleCaptureWriter, ConsoleCaptureSync));
        Console.SetError(new TeeTextWriter(_originalConsoleError, _consoleCaptureWriter, ConsoleCaptureSync));
    }

    private static void ShutdownConsoleCapture() {
        lock (ConsoleCaptureSync) {
            try { Console.Out.Flush(); } catch { }
            try { Console.Error.Flush(); } catch { }
            if (_originalConsoleOut is not null)
                Console.SetOut(_originalConsoleOut);
            if (_originalConsoleError is not null)
                Console.SetError(_originalConsoleError);
            _consoleCaptureWriter?.Dispose();
            _consoleCaptureWriter = null;
            _originalConsoleOut = null;
            _originalConsoleError = null;
        }
    }

    private static void EnsureLogOpen() {
        if (_logOpened)
            return;
        string logDirectory = _logDir;
        _detailDir = Path.Combine(logDirectory, "Details");
        Directory.CreateDirectory(_detailDir);
        _logFile = Path.Combine(logDirectory, "TKFaxEngineTest.log");
        StringBuilder header = new();
        header.AppendLine("TKFaxEngineFX Testlauf");
        header.AppendLine($"Start={NowStamp(false)}");
        header.AppendLine($"ProjectRoot={_projectRoot}");
        header.AppendLine($"RunRoot={_runRootDir}");
        header.AppendLine($"TestFilesDir={_testsDir}");
        header.AppendLine($"ExeDir={_exeDir}");
        header.AppendLine($"LogDir={logDirectory}");
        header.AppendLine($"OutputDir={_outputDir}");
        header.AppendLine("OutputLayout=Ausgabe\\<TestId>\\<erzeugte Testergebnisdatei>");
        header.AppendLine("GeneratorLayout=Test-Files\\<generierte Testdateien>");
        header.AppendLine($"WorkingDir={Environment.CurrentDirectory}");
        header.AppendLine($"Tests={Tests.Length}");
        header.AppendLine("Regel: PASS immer gruen, FAIL/TIMEOUT immer rot in der Konsole.");
        header.AppendLine("Detailregel: PASS erzeugt keine Detaildatei. SKIP/FAIL/TIMEOUT erzeugen eine eigene Detaildatei mit Ursache, Soll-/Ist-Zustand und erwarteten Fundstellen.");
        File.WriteAllText(_logFile, NormalizeCrLf(header.ToString()), new UTF8Encoding(false));
        _logOpened = true;
    }

    private static void AppendLogLine(string line) {
        EnsureLogOpen();
        File.AppendAllText(_logFile, line + "\r\n", new UTF8Encoding(false));
    }

    private static void AppendLogStep(string scope, int step, string text) =>
        AppendLogLine($"{NowStamp(false)} STEP {step:00} {scope} | {text}");

    private static void AppendLogBlock(string block) {
        EnsureLogOpen();
        string text = NormalizeCrLf(block);
        if (!text.EndsWith("\r\n", StringComparison.Ordinal))
            text += "\r\n";
        File.AppendAllText(_logFile, text, new UTF8Encoding(false));
    }

    private static string NormalizeCrLf(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Replace("\n", "\r\n", StringComparison.Ordinal);


    private static string SafeFileSha256(string path, long size) {
        const long maxHashBytes = 128L * 1024L * 1024L;
        if (size > maxHashBytes)
            return $"<nicht_berechnet_datei_groesser_als_{maxHashBytes}>";
        try {
            using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return Convert.ToHexString(SHA256.HashData(stream));
        } catch (Exception exception) {
            return $"<fehler:{exception.GetType().Name}:{exception.Message}>";
        }
    }

    private static string BuildTestDetailHeader(
        TestEntry test,
        IReadOnlyList<string> effectiveArgs,
        string command,
        IReadOnlyList<RequiredTestInput> requiredInputs) {
        StringBuilder header = new();
        header.AppendLine("----- TEST DETAIL CAPTURE BEGIN -----");
        header.AppendLine("Aufbewahrungsregel=Diese temporaere Detaildatei wird bei PASS geloescht und nur bei FAIL/TIMEOUT/NOTIMPL behalten.");
        header.AppendLine($"CaptureStart={DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        header.AppendLine($"TestId={test.Id}");
        header.AppendLine($"TestFile={test.File}");
        header.AppendLine($"Category={test.Category}");
        header.AppendLine($"EffectiveArgs={ExtraArgsText(effectiveArgs)}");
        header.AppendLine($"ChildCommand={command}");
        header.AppendLine($"ProjectRoot={_projectRoot}");
        header.AppendLine($"ExePath={_exePath}");
        header.AppendLine($"ExeDir={_exeDir}");
        header.AppendLine($"ChildWorkingDir={_projectRoot}");
        header.AppendLine($"Framework={RuntimeInformation.FrameworkDescription}");
        header.AppendLine($"OS={RuntimeInformation.OSDescription}");
        header.AppendLine($"ProcessArchitecture={RuntimeInformation.ProcessArchitecture}");
        header.AppendLine($"OSArchitecture={RuntimeInformation.OSArchitecture}");
        header.AppendLine($"IdleTimeoutMs={TestIdleTimeoutMs(test)}");
        header.AppendLine($"ActiveTimeoutMs={TestActiveTimeoutMs(test)}");
        header.AppendLine($"Env_TKFX_TEST_TIMEOUT_SEC={Environment.GetEnvironmentVariable("TKFX_TEST_TIMEOUT_SEC") ?? "<nicht gesetzt>"}");
        header.AppendLine($"Env_TKFX_TEST_ACTIVE_TIMEOUT_SEC={Environment.GetEnvironmentVariable("TKFX_TEST_ACTIVE_TIMEOUT_SEC") ?? "<nicht gesetzt>"}");
        header.AppendLine($"RequiredInputCount={requiredInputs.Count}");

        int index = 0;
        foreach (RequiredTestInput input in requiredInputs) {
            index++;
            string resolved = ResolveExistingRequiredInput(input);
            header.AppendLine($"RequiredInput[{index}].Reason={input.Reason}");
            header.AppendLine($"RequiredInput[{index}].ConfiguredPath={RequiredInputDisplayPath(input)}");
            header.AppendLine($"RequiredInput[{index}].ResolvedPath={(resolved.Length == 0 ? "<nicht gefunden>" : resolved)}");
            if (resolved.Length == 0)
                continue;

            try {
                if (File.Exists(resolved)) {
                    FileInfo info = new(resolved);
                    header.AppendLine($"RequiredInput[{index}].Type=File");
                    header.AppendLine($"RequiredInput[{index}].SizeBytes={info.Length}");
                    header.AppendLine($"RequiredInput[{index}].LastWriteUtc={info.LastWriteTimeUtc:O}");
                    header.AppendLine($"RequiredInput[{index}].SHA256={SafeFileSha256(resolved, info.Length)}");
                } else if (Directory.Exists(resolved)) {
                    DirectoryInfo info = new(resolved);
                    header.AppendLine($"RequiredInput[{index}].Type=Directory");
                    header.AppendLine($"RequiredInput[{index}].LastWriteUtc={info.LastWriteTimeUtc:O}");
                }
            } catch (Exception exception) {
                header.AppendLine($"RequiredInput[{index}].MetadataError={exception.GetType().FullName}: {exception.Message}");
            }
        }

        header.AppendLine("----- CHILD OUTPUT BEGIN -----");
        return NormalizeCrLf(header.ToString());
    }

    private static string SanitizeFilename(string value) {
        StringBuilder output = new(value.Length);
        foreach (char character in value)
            output.Append(char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.' ? character : '_');
        return output.ToString();
    }

    private static string ReadFileText(string path) {
        try { return File.Exists(path) ? File.ReadAllText(path) : ""; } catch { return ""; }
    }

    private static long FileSizeOrZero(string path) {
        try { return File.Exists(path) ? new FileInfo(path).Length : 0; } catch { return 0; }
    }

    private static string Win32ErrorText(int error) =>
        error == 0 ? "<kein Fehler>" : new global::System.ComponentModel.Win32Exception(error).Message;

    private static void AppendFileText(string path, string text) {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        string normalized = NormalizeCrLf(text);
        if (!normalized.EndsWith("\r\n", StringComparison.Ordinal))
            normalized += "\r\n";
        File.AppendAllText(path, normalized, new UTF8Encoding(false));
    }

    private static string BoolText(bool value) => value ? "true" : "false";

    private static string GetEnvironmentText(string name) =>
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)) ? "<nicht gesetzt>" : Environment.GetEnvironmentVariable(name)!;

    private static bool ExistsNoExcept(string path) {
        try { return path.Length != 0 && (File.Exists(path) || Directory.Exists(path)); } catch { return false; }
    }

    private static string PathOrEmpty(string path) => path.Length == 0 ? "<leer>" : path;

    private static string WaitResultText(int wait) => wait switch {
        WaitObject0 => "WAIT_OBJECT_0",
        WaitTimeout => "WAIT_TIMEOUT",
        WaitFailed => "WAIT_FAILED",
        WaitNotRun => "<noch nicht ausgefuehrt>",
        _ => $"UNBEKANNT_{wait}"
    };

    private static string ExtraArgsText(IReadOnlyList<string> extraArgs) =>
        extraArgs.Count == 0 ? "<keine>" : string.Join(" ", extraArgs.Select(static argument => $"[{argument}]"));

    private static RunnerVariableSnapshot MakeRunnerVariableSnapshot(
        string label,
        TestEntry test,
        string scope,
        IReadOnlyList<string> extraArgs,
        string detailTmp,
        string detailFail,
        string detailTimeout,
        string finalDetail,
        string childCommand,
        int timeoutMs,
        int waitResult,
        int exitCode,
        bool timedOut,
        long durationMs,
        int createProcessError,
        bool processCreated,
        int processId,
        long childOutputBytes,
        string note) {
        return new RunnerVariableSnapshot {
            Label = label,
            Timestamp = NowStamp(false),
            TestId = test.Id,
            TestFile = test.File,
            Category = test.Category,
            Scope = scope,
            ExtraArgs = ExtraArgsText(extraArgs),
            Passed = _passed,
            Failed = _failed,
            Skipped = _skipped,
            NotImplemented = _notImplemented,
            ProjectRoot = PathOrEmpty(_projectRoot),
            ExePath = PathOrEmpty(_exePath),
            ExeDir = PathOrEmpty(_exeDir),
            TestsDir = PathOrEmpty(_testsDir),
            LogFile = PathOrEmpty(_logFile),
            DetailDir = PathOrEmpty(_detailDir),
            ParentWorkingDir = Environment.CurrentDirectory,
            ChildWorkingDir = PathOrEmpty(_projectRoot),
            DetailTmp = PathOrEmpty(detailTmp),
            DetailFail = PathOrEmpty(detailFail),
            DetailTimeout = PathOrEmpty(detailTimeout),
            FinalDetail = PathOrEmpty(finalDetail),
            DetailTmpExists = ExistsNoExcept(detailTmp),
            DetailFailExists = ExistsNoExcept(detailFail),
            DetailTimeoutExists = ExistsNoExcept(detailTimeout),
            FinalDetailExists = ExistsNoExcept(finalDetail),
            DetailTmpBytes = FileSizeOrZero(detailTmp),
            DetailFailBytes = FileSizeOrZero(detailFail),
            DetailTimeoutBytes = FileSizeOrZero(detailTimeout),
            FinalDetailBytes = FileSizeOrZero(finalDetail),
            ChildCommand = childCommand.Length == 0 ? "<leer>" : childCommand,
            TimeoutMs = timeoutMs,
            WaitResult = waitResult,
            WaitName = WaitResultText(waitResult),
            ExitCode = exitCode,
            TimedOut = timedOut,
            DurationMs = durationMs,
            CreateProcessError = createProcessError,
            CreateProcessErrorText = Win32ErrorText(createProcessError),
            ProcessCreated = processCreated,
            ProcessId = processId,
            ChildOutputBytes = childOutputBytes,
            EnvTimeoutSec = GetEnvironmentText("TKFX_TEST_TIMEOUT_SEC"),
            EnvActiveTimeoutSec = GetEnvironmentText("TKFX_TEST_ACTIVE_TIMEOUT_SEC"),
            Note = note
        };
    }

    private static void VariableDeltaLine(StringBuilder report, string name, object? before, object? after) {
        report.AppendLine($"  {name}");
        report.AppendLine($"    vorher={before}");
        report.AppendLine($"    nachher={after}");
    }

    private static string MakeActiveVariableBeforeAfterReport(RunnerVariableSnapshot before, RunnerVariableSnapshot after) {
        StringBuilder report = new();
        report.AppendLine("Aktive Runner-Variablen VORHER/NACHHER");
        report.AppendLine("Hinweis=Das sind die aktiven Variablen des Test-Runners und Kindprozess-Starts. Interne lokale Variablen im TKFaxEngineFX-Testkoerper koennen nach Prozessende nicht direkt ausgelesen werden; deren Ausgabe steht unter ChildOutput.");
        VariableDeltaLine(report, "SnapshotLabel", before.Label, after.Label);
        VariableDeltaLine(report, "SnapshotZeit", before.Timestamp, after.Timestamp);
        VariableDeltaLine(report, "TestId", before.TestId, after.TestId);
        VariableDeltaLine(report, "TestFile", before.TestFile, after.TestFile);
        VariableDeltaLine(report, "Category", before.Category, after.Category);
        VariableDeltaLine(report, "Scope", before.Scope, after.Scope);
        VariableDeltaLine(report, "ExtraArgs", before.ExtraArgs, after.ExtraArgs);
        VariableDeltaLine(report, "CounterPassed", before.Passed, after.Passed);
        VariableDeltaLine(report, "CounterFailed", before.Failed, after.Failed);
        VariableDeltaLine(report, "CounterSkipped", before.Skipped, after.Skipped);
        VariableDeltaLine(report, "CounterNotImplemented", before.NotImplemented, after.NotImplemented);
        VariableDeltaLine(report, "ProjectRoot", before.ProjectRoot, after.ProjectRoot);
        VariableDeltaLine(report, "ExePath", before.ExePath, after.ExePath);
        VariableDeltaLine(report, "ExeDir", before.ExeDir, after.ExeDir);
        VariableDeltaLine(report, "TestsDir", before.TestsDir, after.TestsDir);
        VariableDeltaLine(report, "LogFile", before.LogFile, after.LogFile);
        VariableDeltaLine(report, "DetailDir", before.DetailDir, after.DetailDir);
        VariableDeltaLine(report, "ParentWorkingDir", before.ParentWorkingDir, after.ParentWorkingDir);
        VariableDeltaLine(report, "ChildWorkingDir", before.ChildWorkingDir, after.ChildWorkingDir);
        VariableDeltaLine(report, "DetailTmp", before.DetailTmp, after.DetailTmp);
        VariableDeltaLine(report, "DetailTmpExists", BoolText(before.DetailTmpExists), BoolText(after.DetailTmpExists));
        VariableDeltaLine(report, "DetailTmpBytes", before.DetailTmpBytes, after.DetailTmpBytes);
        VariableDeltaLine(report, "DetailFail", before.DetailFail, after.DetailFail);
        VariableDeltaLine(report, "DetailFailExists", BoolText(before.DetailFailExists), BoolText(after.DetailFailExists));
        VariableDeltaLine(report, "DetailFailBytes", before.DetailFailBytes, after.DetailFailBytes);
        VariableDeltaLine(report, "DetailTimeout", before.DetailTimeout, after.DetailTimeout);
        VariableDeltaLine(report, "DetailTimeoutExists", BoolText(before.DetailTimeoutExists), BoolText(after.DetailTimeoutExists));
        VariableDeltaLine(report, "DetailTimeoutBytes", before.DetailTimeoutBytes, after.DetailTimeoutBytes);
        VariableDeltaLine(report, "FinalDetail", before.FinalDetail, after.FinalDetail);
        VariableDeltaLine(report, "FinalDetailExists", BoolText(before.FinalDetailExists), BoolText(after.FinalDetailExists));
        VariableDeltaLine(report, "FinalDetailBytes", before.FinalDetailBytes, after.FinalDetailBytes);
        VariableDeltaLine(report, "ChildCommand", before.ChildCommand, after.ChildCommand);
        VariableDeltaLine(report, "TimeoutMs", before.TimeoutMs, after.TimeoutMs);
        VariableDeltaLine(report, "WaitResult", before.WaitResult, after.WaitResult);
        VariableDeltaLine(report, "WaitResultText", before.WaitName, after.WaitName);
        VariableDeltaLine(report, "ExitCode", before.ExitCode, after.ExitCode);
        VariableDeltaLine(report, "TimedOut", BoolText(before.TimedOut), BoolText(after.TimedOut));
        VariableDeltaLine(report, "DurationMs", before.DurationMs, after.DurationMs);
        VariableDeltaLine(report, "CreateProcessError", before.CreateProcessError, after.CreateProcessError);
        VariableDeltaLine(report, "CreateProcessErrorText", before.CreateProcessErrorText, after.CreateProcessErrorText);
        VariableDeltaLine(report, "ProcessCreated", BoolText(before.ProcessCreated), BoolText(after.ProcessCreated));
        VariableDeltaLine(report, "ProcessId", before.ProcessId, after.ProcessId);
        VariableDeltaLine(report, "ChildOutputBytes", before.ChildOutputBytes, after.ChildOutputBytes);
        VariableDeltaLine(report, "Env_TKFX_TEST_TIMEOUT_SEC", before.EnvTimeoutSec, after.EnvTimeoutSec);
        VariableDeltaLine(report, "Env_TKFX_TEST_ACTIVE_TIMEOUT_SEC", before.EnvActiveTimeoutSec, after.EnvActiveTimeoutSec);
        VariableDeltaLine(report, "Notiz", before.Note, after.Note);
        return NormalizeCrLf(report.ToString());
    }

    private static string MakeFailureDiagnosticReport(
        TestEntry test,
        string scope,
        IReadOnlyList<string> extraArgs,
        string detailFile,
        string childCommand,
        int timeoutMs,
        int waitResult,
        int exitCode,
        bool timedOut,
        long durationMs,
        int createProcessError,
        string activeVariablesBeforeAfter,
        long childOutputBytes,
        string captureSummary,
        ProcessActivityWaitResult waitInfo) {
        StringBuilder report = new();
        report.AppendLine();
        report.AppendLine("----- RUNNER FAILURE DIAGNOSTIC BEGIN -----");
        report.AppendLine($"Zeit={NowStamp(false)}");
        report.AppendLine($"Result={(timedOut ? "TIMEOUT" : "FAIL")}");
        report.AppendLine($"TestId={test.Id}");
        report.AppendLine($"TestFile={test.File}");
        report.AppendLine($"Category={test.Category}");
        report.AppendLine($"Scope={scope}");
        report.AppendLine($"ProjectRoot={_projectRoot}");
        report.AppendLine($"ExePath={_exePath}");
        report.AppendLine($"ExeDir={_exeDir}");
        report.AppendLine($"ParentWorkingDir={Environment.CurrentDirectory}");
        report.AppendLine($"ChildWorkingDir={_projectRoot}");
        report.AppendLine($"ChildCommand={childCommand}");
        report.AppendLine($"TimeoutMs={timeoutMs}");
        report.AppendLine($"WaitResult={waitResult}");
        report.AppendLine($"ExitCode={exitCode}");
        report.AppendLine($"DurationMs={durationMs}");
        report.AppendLine($"DetailFile={detailFile}");
        report.AppendLine($"DetailFileBytes={FileSizeOrZero(detailFile)}");
        if (createProcessError != 0)
            report.AppendLine($"CreateProcessError={createProcessError} | {Win32ErrorText(createProcessError)}");
        report.AppendLine($"ExtraArgs={ExtraArgsText(extraArgs)}");
        report.AppendLine();
        report.AppendLine(activeVariablesBeforeAfter);
        report.AppendLine($"ChildCaptureSummary={captureSummary}");
        AppendProcessResourceReport(report, waitInfo);
        report.AppendLine();
        report.AppendLine("Einzelschritte:");
        report.AppendLine("  01 Runtime-Pfade ermittelt");
        report.AppendLine("  02 Logdatei geoeffnet");
        report.AppendLine("  03 Testmetadaten geschrieben");
        report.AppendLine("  04 optionale externe Testdaten-Vorpruefung ausgefuehrt");
        report.AppendLine("  05 Detail-Zieldatei vorbereitet");
        report.AppendLine("  06 Child-Command gebaut");
        report.AppendLine("  07 Process-Global-State vor Start zurueckgesetzt");
        report.AppendLine("  08 Process.Start ausgefuehrt");
        report.AppendLine("  09 Aktivitaetsueberwachtes Warten ausgefuehrt");
        report.AppendLine("  10 ExitCode/Timeout ausgewertet");
        report.AppendLine("  11 Child-Prozess geschlossen");
        report.AppendLine("  12 Process-Global-State nach Test zurueckgesetzt");
        report.AppendLine("  13 Detaildatei finalisiert");
        if (childOutputBytes == 0) {
            report.AppendLine("ChildOutput=<leer>");
            report.AppendLine("Hinweis=Der Test hat keinen Text auf stdout/stderr geschrieben. Der Rueckgabecode kommt direkt vom Testkoerper oder vom ExitGuard.");
        } else {
            report.AppendLine($"ChildOutputBytes={childOutputBytes}");
            report.AppendLine("ChildOutputLocation=Am Anfang dieser Detaildatei unter CHILD OUTPUT BEGIN/END.");
        }
        report.AppendLine("----- RUNNER FAILURE DIAGNOSTIC END -----");
        return NormalizeCrLf(report.ToString());
    }

    private static void AddRequiredFile(List<RequiredTestInput> required, string path, string reason, bool allowLocalFallback = false) =>
        required.Add(new RequiredTestInput(path, reason, allowLocalFallback));

    private static void AddRequiredFileArgument(List<RequiredTestInput> required, IReadOnlyList<string> args, string option, string reason) {
        for (int index = 0; index < args.Count; index++) {
            string argument = args[index];
            if (argument == option && index + 1 < args.Count) {
                AddRequiredFile(required, args[index + 1], reason, true);
                return;
            }
            if (argument.StartsWith(option, StringComparison.Ordinal) && argument.Length > option.Length) {
                AddRequiredFile(required, argument[option.Length..], reason, true);
                return;
            }
        }
    }

    private static bool HasPathSeparator(string value) =>
        value.IndexOfAny(new[] { '/', '\\' }) >= 0;

    private static bool IsAbsoluteOrDrivePath(string path) =>
        Path.IsPathFullyQualified(path) || (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':');

    private static string ResolveExistingRequiredInput(RequiredTestInput input) {
        if (input.Path.Length == 0)
            return "";
        foreach (string candidate in RequiredInputCandidatePaths(input)) {
            if (File.Exists(candidate) || Directory.Exists(candidate))
                return candidate;
        }
        return "";
    }

    private static string RequiredInputDisplayPath(RequiredTestInput input) =>
        input.Path.Length == 0 ? "<leer>" : input.Path;

    private static IReadOnlyList<string> RequiredInputCandidatePaths(RequiredTestInput input) {
        List<string> candidates = new();
        if (input.Path.Length == 0)
            return candidates;
        if (IsAbsoluteOrDrivePath(input.Path)) {
            candidates.Add(Path.GetFullPath(input.Path));
        } else {
            candidates.Add(Path.GetFullPath(Path.Combine(_projectRoot, input.Path)));
            candidates.Add(Path.GetFullPath(Path.Combine(_exeDir, input.Path)));
            if (input.AllowLocalFallback && !HasPathSeparator(input.Path)) {
                candidates.Add(Path.GetFullPath(Path.Combine(_projectRoot, "Test-Files", "local", input.Path)));
                candidates.Add(Path.GetFullPath(Path.Combine(_exeDir, "Test-Files", "local", input.Path)));
            }
        }
        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string JoinArgumentsForReport(IReadOnlyList<string> args) => ExtraArgsText(args);

    private static ExternalHarnessReport ExternalHarnessReportForTest(TestEntry test) => test.Id switch {
        "dummy_modems_tests" => new(
            "Zweiter gleichzeitig laufender Modemprozess plus DTE/AT-Gegenpart",
            "SRC/dummy_modems_tests.cs::Main/modem_tests und socket_harness_init",
            "Windows: TCP-Loopback-Endpunkt aus logischem Socketnamen /tmp/modemsocket; DTE ueber Named Pipe aus SRC/pseudo_terminals.cs",
            "TKFaxEngineTest.exe --child-run dummy_modems_tests -a",
            "TKFaxEngineTest.exe --child-run dummy_modems_tests -c",
            "Beide Prozesse muessen parallel laufen; ein DTE muss AT-Daten ueber den ausgegebenen Named-Pipe-Endpunkt senden/empfangen.",
            "Beide Modemseiten beenden ihren Socket-/Modemablauf mit RC=0.",
            "SRC/dummy_modems_tests.cs; SRC/socket_harness.cs; SRC/pseudo_terminals.cs; SRC/dummy_modems_tests_adapter.cs"),
        "sprt_tests" => new(
            "Zweiter gleichzeitig laufender SPRT-Peerprozess",
            "SRC/sprt_tests.cs::Main/sprt_tests und socket_dgram_harness_init",
            "Windows: zwei UDP-Loopback-Endpunkte aus /tmp/sprt_socket_a und /tmp/sprt_socket_b",
            "TKFaxEngineTest.exe --child-run sprt_tests -a",
            "TKFaxEngineTest.exe --child-run sprt_tests -c",
            "Beide Prozesse muessen parallel laufen und die vier SPRT-Kanaltypen gegenseitig senden, bestaetigen und sequenzieren.",
            "Alle erwarteten Sequenzen/Laengen/Inhalte stimmen; beide Prozesse beenden mit RC=0.",
            "SRC/sprt_tests.cs; SRC/socket_dgram_harness.cs; SRC/pseudo_terminals.cs; SRC/sprt_tests_adapter.cs"),
        "v150_1_sse_tests" => new(
            "Zweiter gleichzeitig laufender V.150.1-SSE-Peerprozess",
            "SRC/v150_1_sse_tests.cs::Main/v150_1_sse_tests und socket_dgram_harness_init",
            "Windows: zwei UDP-Loopback-Endpunkte aus /tmp/sse_socket_a und /tmp/sse_socket_b",
            "TKFaxEngineTest.exe --child-run v150_1_sse_tests -a",
            "TKFaxEngineTest.exe --child-run v150_1_sse_tests -c",
            "Beide Prozesse muessen parallel laufen und SSE-Nachrichten samt Timer-/Pacing-Ablauf gegenseitig verarbeiten.",
            "SSE-Zustandsfolge und Nachrichtenuebertragung enden ohne Fehler mit RC=0.",
            "SRC/v150_1_sse_tests.cs; SRC/socket_dgram_harness.cs; SRC/pseudo_terminals.cs; SRC/v150_1_sse_tests_adapter.cs"),
        "t31_pseudo_terminal_tests" => new(
            "Externer DTE/AT-Gegenpart fuer den T.31-Pseudo-Terminal-Endpunkt",
            "SRC/t31_pseudo_terminal_tests.cs::Main/t30_tests und pseudo_terminal_listen_external",
            @"Windows Named Pipe \\.\pipe\TKFaxEngineFX_pty_<PID>_<Slot>_<Nummer>; exakter Name wird beim Einzelstart als 'T.31 DTE endpoint' ausgegeben",
            "TKFaxEngineTest.exe --child-run t31_pseudo_terminal_tests [optionale T.31-Argumente]",
            "Separater DTE-Testclient verbindet sich mit der ausgegebenen Named Pipe",
            "Der DTE muss die erwartete AT/T.31-Befehlsfolge senden, Antworten lesen und den Faxablauf bis zum definierten Ende steuern.",
            "T.31/T.30-Ablauf endet mit gueltigem Phase-E-Ergebnis und Prozess-RC=0.",
            "SRC/t31_pseudo_terminal_tests.cs; SRC/pseudo_terminals.cs; SRC/t31_pseudo_terminal_tests_adapter.cs"),
        _ => new ExternalHarnessReport()
    };

    private static string WriteSkipDetailReport(
        TestEntry test,
        IReadOnlyList<string> requestedArgs,
        IReadOnlyList<string> effectiveArgs,
        string syntheticSkipReason,
        IReadOnlyList<RequiredTestInput> missingInputs) {
        try {
            Directory.CreateDirectory(_detailDir);
            string detailFile = Path.Combine(_detailDir, $"{SanitizeFilename(test.Id)}_SKIP_{NowStamp(true)}.log");
            string sourceFile = Path.Combine(_projectRoot, "SRC", Path.ChangeExtension(test.File, ".cs"));
            string adapterFile = Path.Combine(_projectRoot, "SRC", test.Id + "_adapter.cs");
            string projectFile = Path.Combine(_projectRoot, "TKFaxEngineTest.csproj");
            bool externalHarness = syntheticSkipReason.Length != 0;
            StringBuilder report = new();
            report.AppendLine("TKFaxEngineFX Test SKIP Detailbericht");
            report.AppendLine("====================================");
            report.AppendLine("Result=SKIP");
            report.AppendLine($"SkipType={(externalHarness ? "EXTERNAL_HARNESS_REQUIRED" : "MISSING_REQUIRED_TEST_INPUT")}");
            report.AppendLine("TestProcessStarted=false");
            report.AppendLine("ReturnCode=0 (SKIP-Status; kein PASS)");
            report.AppendLine($"TestId={test.Id}");
            report.AppendLine($"TestSource={test.File}");
            report.AppendLine($"Category={test.Category}");
            report.AppendLine($"Timestamp={NowStamp(false)}");
            report.AppendLine($"RequestedArgs={JoinArgumentsForReport(requestedArgs)}");
            report.AppendLine($"EffectiveArgs={JoinArgumentsForReport(effectiveArgs)}");
            report.AppendLine($"ProjectRoot={_projectRoot}");
            report.AppendLine($"ExeDir={_exeDir}");
            report.AppendLine($"WorkingDir={Environment.CurrentDirectory}");
            report.AppendLine();
            report.AppendLine("ERWARTETE CODESTELLEN");
            report.AppendLine($"SourcePath={sourceFile}");
            report.AppendLine($"SourceExists={BoolText(ExistsNoExcept(sourceFile))}");
            report.AppendLine($"AdapterPath={adapterFile}");
            report.AppendLine($"AdapterExists={BoolText(ExistsNoExcept(adapterFile))}");
            report.AppendLine($"ProjectPath={projectFile}");
            report.AppendLine($"ProjectExists={BoolText(ExistsNoExcept(projectFile))}");
            report.AppendLine("SkipDecisionLocation=SRC/TKFaxEngineTest.cs::AddRequiredInputsForTest/RunTest");

            if (externalHarness) {
                ExternalHarnessReport harness = ExternalHarnessReportForTest(test);
                report.AppendLine();
                report.AppendLine("FEHLENDE LAUFZEITKOMPONENTE");
                report.AppendLine($"Reason={syntheticSkipReason}");
                report.AppendLine($"MissingComponent={(harness.MissingComponent.Length == 0 ? syntheticSkipReason : harness.MissingComponent)}");
                report.AppendLine($"ExpectedBy={(harness.ExpectedBy.Length == 0 ? sourceFile : harness.ExpectedBy)}");
                report.AppendLine($"ExpectedRuntimeLocation={(harness.ExpectedLocation.Length == 0 ? "<nicht spezifiziert>" : harness.ExpectedLocation)}");
                report.AppendLine($"RequiredInvocation1={(harness.RequiredInvocation1.Length == 0 ? "<nicht spezifiziert>" : harness.RequiredInvocation1)}");
                report.AppendLine($"RequiredInvocation2={(harness.RequiredInvocation2.Length == 0 ? "<nicht spezifiziert>" : harness.RequiredInvocation2)}");
                report.AppendLine($"RequiredPeerBehavior={(harness.RequiredPeerBehavior.Length == 0 ? "<nicht spezifiziert>" : harness.RequiredPeerBehavior)}");
                report.AppendLine($"ExpectedCompletion={(harness.ExpectedCompletion.Length == 0 ? "RC=0 nach vollstaendig ausgefuehrtem Harness" : harness.ExpectedCompletion)}");
                report.AppendLine($"ImplementationFiles={(harness.ImplementationFiles.Length == 0 ? sourceFile : harness.ImplementationFiles)}");
                report.AppendLine("PeerProcessState=FEHLT/NOT_STARTED");
                report.AppendLine("EndpointState=NICHT_VERBUNDEN");
                report.AppendLine("InputBufferState=NICHT_ERZEUGT_WEGEN_FEHLENDEM_GEGENPART");
                report.AppendLine("ExpectedState=Gegenpart gestartet; Endpunkt verbunden; Testdatenfluss vorhanden");
                report.AppendLine("RequiredAction=Harness als koordinierte Mehrprozess-/DTE-Ausfuehrung starten; erst danach darf der Test PASS oder FAIL liefern.");
            } else {
                report.AppendLine();
                report.AppendLine("FEHLENDE PFLICHT-TESTDATEN");
                report.AppendLine($"MissingCount={missingInputs.Count}");
                for (int index = 0; index < missingInputs.Count; index++) {
                    RequiredTestInput input = missingInputs[index];
                    IReadOnlyList<string> candidates = RequiredInputCandidatePaths(input);
                    report.AppendLine();
                    report.AppendLine($"MissingItem[{index}].Reason={input.Reason}");
                    report.AppendLine($"MissingItem[{index}].ConfiguredPath={RequiredInputDisplayPath(input)}");
                    report.AppendLine($"MissingItem[{index}].AllowLocalFallback={BoolText(input.AllowLocalFallback)}");
                    report.AppendLine($"MissingItem[{index}].ResolvedPath=<NOT_FOUND>");
                    report.AppendLine($"MissingItem[{index}].ActualState=FEHLT");
                    report.AppendLine($"MissingItem[{index}].ExpectedState=EXISTS und fuer den Test lesbar");
                    report.AppendLine($"MissingItem[{index}].ExpectedBy={sourceFile} sowie EffectiveArgs/Runner-Pflichtdateipruefung");
                    report.AppendLine($"MissingItem[{index}].CandidateCount={candidates.Count}");
                    for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++) {
                        string candidate = candidates[candidateIndex];
                        bool exists = ExistsNoExcept(candidate);
                        report.AppendLine($"MissingItem[{index}].Candidate[{candidateIndex}].Path={candidate}");
                        report.AppendLine($"MissingItem[{index}].Candidate[{candidateIndex}].Exists={BoolText(exists)}");
                        if (exists)
                            report.AppendLine($"MissingItem[{index}].Candidate[{candidateIndex}].Bytes={FileSizeOrZero(candidate)}");
                    }
                    report.AppendLine($"MissingItem[{index}].RequiredAction=Datei/Ordner an einem Kandidatenpfad bereitstellen oder beim Einzeltest einen gueltigen absoluten Pfad als Argument angeben.");
                }
                report.AppendLine();
                report.AppendLine("RuntimeDataState=NICHT_ERZEUGT_WEGEN_FEHLENDER_EINGABE");
                report.AppendLine("InputBufferState=LEER/NICHT_INITIALISIERT");
                report.AppendLine("ExpectedState=Alle Pflicht-Testdaten vorhanden und vor Prozessstart erfolgreich aufloesbar");
            }

            report.AppendLine();
            report.AppendLine("BEWERTUNG");
            report.AppendLine("PassAllowed=false");
            report.AppendLine("FailEvaluated=false");
            report.AppendLine("Reason=Der eigentliche Testkoerper wurde wegen einer nicht erfuellten Vorbedingung nicht gestartet.");
            report.AppendLine("NextResultAfterFix=PASS oder FAIL entsprechend dem echten Testlauf; SKIP darf danach nicht mehr auftreten.");
            File.WriteAllText(detailFile, NormalizeCrLf(report.ToString()), new UTF8Encoding(false));
            return detailFile;
        } catch {
            return "";
        }
    }

    private static bool TestArgHasOption(IReadOnlyList<string> args, string option) =>
        args.Any(argument => argument == option || (argument.StartsWith(option, StringComparison.Ordinal) && argument.Length > option.Length));

    private static bool TestArgHasAnyOption(IReadOnlyList<string> args, params string[] options) =>
        options.Any(option => TestArgHasOption(args, option));

    private static string TestArgOptionValue(IReadOnlyList<string> args, string option) {
        for (int index = 0; index < args.Count; index++) {
            if (args[index] == option)
                return index + 1 < args.Count ? args[index + 1] : "";
            if (args[index].StartsWith(option, StringComparison.Ordinal) && args[index].Length > option.Length)
                return args[index][option.Length..];
        }
        return "";
    }

    private static void RemoveTestArgOption(List<string> args, string option, bool consumesValue) {
        for (int index = 0; index < args.Count;) {
            if (args[index] == option) {
                args.RemoveAt(index);
                if (consumesValue && index < args.Count && (args[index].Length == 0 || args[index][0] != '-'))
                    args.RemoveAt(index);
                continue;
            }
            if (args[index].StartsWith(option, StringComparison.Ordinal) && args[index].Length > option.Length) {
                args.RemoveAt(index);
                continue;
            }
            index++;
        }
    }

    private static void AddDefaultFileOption(List<string> args, string option, string relativePath, bool allowLocalFallback = false) {
        if (TestArgHasOption(args, option))
            return;
        RequiredTestInput input = new(relativePath, "Default-Testdatei", allowLocalFallback);
        string resolved = ResolveExistingRequiredInput(input);
        args.Add(option);
        args.Add(resolved.Length == 0 ? relativePath : resolved);
    }

    private static void NormalizeFileOption(List<string> args, string option, bool allowLocalFallback = true) {
        for (int index = 0; index < args.Count; index++) {
            bool separate = args[index] == option;
            if (separate && index + 1 < args.Count) {
                RequiredTestInput input = new(args[index + 1], "Dateioption", allowLocalFallback);
                string resolved = ResolveExistingRequiredInput(input);
                if (resolved.Length != 0)
                    args[index + 1] = resolved;
                return;
            }
            if (args[index].StartsWith(option, StringComparison.Ordinal) && args[index].Length > option.Length) {
                string value = args[index][option.Length..];
                RequiredTestInput input = new(value, "Dateioption", allowLocalFallback);
                string resolved = ResolveExistingRequiredInput(input);
                if (resolved.Length != 0)
                    args[index] = option + resolved;
                return;
            }
        }
    }

    private static IEnumerable<int> PositionalArgumentIndices(IReadOnlyList<string> args, IReadOnlyCollection<string> optionsWithValues) {
        for (int index = 0; index < args.Count; index++) {
            string argument = args[index];
            if (argument.Length != 0 && argument[0] == '-') {
                if (optionsWithValues.Contains(argument) && index + 1 < args.Count)
                    index++;
                continue;
            }
            yield return index;
        }
    }

    private static string PositionalFileAt(IReadOnlyList<string> args, IReadOnlyCollection<string> optionsWithValues, int position) {
        int[] indices = PositionalArgumentIndices(args, optionsWithValues).ToArray();
        return position >= 0 && position < indices.Length ? args[indices[position]] : "";
    }

    private static string FirstPositionalFile(IReadOnlyList<string> args, IReadOnlyCollection<string> optionsWithValues) =>
        PositionalFileAt(args, optionsWithValues, 0);

    private static void NormalizePositionalFileAt(List<string> args, IReadOnlyCollection<string> optionsWithValues, int position, bool allowLocalFallback = true) {
        int[] indices = PositionalArgumentIndices(args, optionsWithValues).ToArray();
        if (position < 0 || position >= indices.Length)
            return;
        int index = indices[position];
        RequiredTestInput input = new(args[index], "Positions-Dateiargument", allowLocalFallback);
        string resolved = ResolveExistingRequiredInput(input);
        if (resolved.Length != 0)
            args[index] = resolved;
    }

    private static void AddDefaultPositionalFile(List<string> args, IReadOnlyCollection<string> optionsWithValues, string relativePath, bool allowLocalFallback = false) {
        if (FirstPositionalFile(args, optionsWithValues).Length != 0)
            return;
        RequiredTestInput input = new(relativePath, "Default-Positionsdatei", allowLocalFallback);
        string resolved = ResolveExistingRequiredInput(input);
        args.Add(resolved.Length == 0 ? relativePath : resolved);
    }

    private static string[] MakeEffectiveTestArgs(TestEntry test, IReadOnlyList<string> requestedArgs) {
        List<string> args = new(requestedArgs);
        switch (test.Id) {
            case "fax_tests":
                RemoveTestArgOption(args, "-p", true);
                AddDefaultFileOption(args, "-i", "Test-Files/itu/fax/itutests.tif");
                AddDefaultFileOption(args, "-X", "Test-Files/tsb85.xml");
                break;
            case "time_scale_tests":
                AddDefaultFileOption(args, "-i", "Test-Files/local/short_nb_voice.wav");
                break;
            case "lpc10_tests" when !TestArgHasOption(args, "-d"):
                AddDefaultFileOption(args, "-i", "Test-Files/local/dam9.wav");
                break;
            case "ima_adpcm_tests":
                AddDefaultFileOption(args, "-i", "Test-Files/local/short_nb_voice.wav");
                break;
            case "oki_adpcm_tests" when !TestArgHasOption(args, "-d"):
                AddDefaultFileOption(args, "-i", "Test-Files/local/short_nb_voice.wav");
                break;
            case "t4_tests" when !TestArgHasOption(args, "-d"):
                AddDefaultFileOption(args, "-i", "Test-Files/itu/fax/itutests.tif");
                break;
            case "tsb85_tests":
                AddDefaultFileOption(args, "-x", "Test-Files/tsb85.xml");
                break;
            case "t38_decode":
                AddDefaultFileOption(args, "-i", "Test-Files/T38.pcap");
                AddDefaultFileOption(args, "-T", "Test-Files/itu/fax/itutests.tif");
                break;
            case "sprt_decode":
                AddDefaultFileOption(args, "-i", "Test-Files/sprt.pcap");
                break;
            case "fax_decode":
                AddDefaultPositionalFile(args, new[] { "-c", "-x", "-y" }, "Test-Files/local/fax_samp.wav");
                break;
            case "t42_tests":
                AddDefaultPositionalFile(args, Array.Empty<string>(), "Test-Files/itu/t24/F21B400.TIF");
                break;
            case "t43_tests":
                AddDefaultPositionalFile(args, Array.Empty<string>(), "Test-Files/itu/tiff-fx/l04x_02x.tif");
                break;
            case "v42bis_tests":
                if (!TestArgHasAnyOption(args, "-c", "-d")) {
                    args.Insert(0, "-d");
                    args.Insert(0, "-c");
                }
                if (FirstPositionalFile(args, Array.Empty<string>()).Length == 0) {
                    const string requestedSource = @"H:\Documents\source\repos\TKFaxEngineTest\Test-Files\Fax_Testseite_A4_Fine_1bit_V0001.tif";
                    if (File.Exists(requestedSource))
                        args.Add(requestedSource);
                    else
                        AddDefaultPositionalFile(args, Array.Empty<string>(), "Test-Files/Fax_Testseite_A4_Fine_1bit_V0001.tif");
                }
                break;
        }

        (string Id, string Option)[] fileOptions =
        {
            ("bell_mf_rx_tests", "-d"), ("oki_adpcm_tests", "-d"), ("oki_adpcm_tests", "-i"),
            ("v8_tests", "-d"), ("adsi_tests", "-d"), ("ademco_contactid_tests", "-d"),
            ("time_scale_tests", "-i"), ("fsk_tests", "-d"), ("modem_connect_tones_tests", "-d"),
            ("v27ter_tests", "-d"), ("dtmf_rx_tests", "-d"), ("fax_tests", "-d"),
            ("fax_tests", "-i"), ("fax_tests", "-X"), ("v17_tests", "-d"),
            ("data_modems_tests", "-d"), ("v22bis_tests", "-d"), ("super_tone_rx_tests", "-d"),
            ("v29_tests", "-d"), ("hdlc_tests", "-d"), ("lpc10_tests", "-i"),
            ("r2_mf_rx_tests", "-d"), ("t4_tests", "-d"), ("t4_tests", "-i"),
            ("sprt_decode", "-i"), ("v18_tests", "-d"), ("tsb85_tests", "-x"),
            ("t38_decode", "-i"), ("t38_decode", "-T"), ("ima_adpcm_tests", "-i"),
            ("t31_tests", "-d"), ("g722_tests", "-d"), ("g722_tests", "-e"),
            ("g711_tests", "-d"), ("g711_tests", "-e"), ("power_meter_tests", "-d")
        };
        foreach ((string id, string option) in fileOptions)
            if (test.Id == id)
                NormalizeFileOption(args, option);

        if (test.Id == "fax_decode")
            NormalizePositionalFileAt(args, new[] { "-c", "-x", "-y" }, 0);
        else if (test.Id is "v42bis_tests" or "t42_tests" or "t43_tests")
            NormalizePositionalFileAt(args, Array.Empty<string>(), 0);
        else if (test.Id == "echo_tests" && TestArgHasOption(args, "-s")) {
            NormalizePositionalFileAt(args, new[] { "-e", "-m", "-M" }, 0);
            if (!TestArgHasOption(args, "-2"))
                NormalizePositionalFileAt(args, new[] { "-e", "-m", "-M" }, 1);
        }
        return args.ToArray();
    }

    private static void AddCommonBellcoreMitelRequiredFiles(List<RequiredTestInput> required) {
        AddRequiredFile(required, "Test-Files/mitel/mitel-cm7291-talkoff.wav", "Mitel Talkoff WAV");
        for (int index = 1; index <= 6; index++)
            AddRequiredFile(required, $"Test-Files/bellcore/tr-tsy-00763-{index}.wav", $"Bellcore Talkoff WAV {index}");
    }

    private static void AddRequiredInputsForTest(
        TestEntry test,
        IReadOnlyList<string> args,
        List<RequiredTestInput> required,
        out string syntheticSkipReason) {
        syntheticSkipReason = "";
        (string Id, string Option, string Reason)[] optionSpecs =
        {
            ("bell_mf_rx_tests", "-d", "Bell MF Decode WAV"),
            ("oki_adpcm_tests", "-d", "OKI ADPCM Eingang"), ("oki_adpcm_tests", "-i", "OKI PCM Eingang WAV"),
            ("v8_tests", "-d", "V.8 Decode WAV"), ("adsi_tests", "-d", "ADSI Decode WAV"),
            ("ademco_contactid_tests", "-d", "Ademco Decode WAV"), ("time_scale_tests", "-i", "Time-Scale Eingang WAV"),
            ("fsk_tests", "-d", "FSK Decode WAV"), ("modem_connect_tones_tests", "-d", "Connect-Tone Decode WAV"),
            ("v27ter_tests", "-d", "V.27ter Decode WAV"), ("dtmf_rx_tests", "-d", "DTMF Decode WAV"),
            ("fax_tests", "-d", "Fax Replay WAV/PCAP"), ("fax_tests", "-i", "Fax Eingang TIFF"),
            ("fax_tests", "-X", "Fax Tester XML"), ("v17_tests", "-d", "V.17 Decode WAV"),
            ("data_modems_tests", "-d", "Data-Modem Decode WAV"), ("v22bis_tests", "-d", "V.22bis Decode WAV"),
            ("super_tone_rx_tests", "-d", "Super-Tone Decode WAV"), ("v29_tests", "-d", "V.29 Decode WAV"),
            ("hdlc_tests", "-d", "HDLC Eingabedatei"), ("lpc10_tests", "-i", "LPC10 Eingang WAV"),
            ("r2_mf_rx_tests", "-d", "R2 MF Decode WAV"), ("t4_tests", "-d", "T.4 Decode Daten"),
            ("t4_tests", "-i", "T.4 Eingang TIFF"), ("sprt_decode", "-i", "SPRT Decode PCAP"),
            ("v18_tests", "-d", "V.18 Decode WAV"), ("tsb85_tests", "-x", "TSB85 XML"),
            ("t38_decode", "-i", "T.38 Decode PCAP"), ("t38_decode", "-T", "T.38 Referenz TIFF"),
            ("ima_adpcm_tests", "-i", "IMA ADPCM Eingang WAV"), ("t31_tests", "-d", "T.31 Decode WAV"),
            ("g722_tests", "-d", "G.722 Decode Eingang"), ("g722_tests", "-e", "G.722 Encode Eingang WAV"),
            ("g711_tests", "-d", "G.711 Decode Eingang"), ("g711_tests", "-e", "G.711 Encode Eingang WAV"),
            ("power_meter_tests", "-d", "Power-Meter Eingang WAV")
        };
        foreach ((string id, string option, string reason) in optionSpecs)
            if (test.Id == id)
                AddRequiredFileArgument(required, args, option, reason);

        if (test.Id is "ademco_contactid_tests" or "adsi_tests" or "dtmf_rx_tests" or "modem_connect_tones_tests" or "sig_tone_tests" or "super_tone_rx_tests")
            AddCommonBellcoreMitelRequiredFiles(required);
        if (test.Id == "t31_pseudo_terminal_tests")
            AddRequiredFile(required, "Test-Files/itu/fax/itutests.tif", "T.31 Gegenstellen-Fax-TIFF");
        if (test.Id is "plc_tests" or "playout_tests")
            AddRequiredFile(required, "Test-Files/local/short_nb_voice.wav", "lokale Sprach-WAV");
        if (test.Id == "g711_tests" && TestArgHasAnyOption(args, "-a", "-u") && !TestArgHasAnyOption(args, "-d", "-e"))
            AddRequiredFile(required, "Test-Files/local/short_nb_voice.wav", "G.711 Standard-Eingang WAV");

        if (test.Id == "g722_tests") {
            if (!TestArgHasAnyOption(args, "-b", "-d", "-e", "-t")) {
                string[] vectors =
                {
                    "T1C1.XMT", "T1C2.XMT", "T1D3.COD", "T2R1.COD", "T2R2.COD",
                    "T3L1.RC1", "T3L1.RC2", "T3L1.RC3", "T3H1.RC0",
                    "T3L2.RC1", "T3L2.RC2", "T3L2.RC3", "T3H2.RC0",
                    "T3L3.RC1", "T3L3.RC2", "T3L3.RC3", "T3H3.RC0"
                };
                foreach (string name in vectors)
                    AddRequiredFile(required, $"Test-Files/itu/g722/{name}", "ITU G.722 Testvektor");
            } else if (!TestArgHasAnyOption(args, "-d", "-e")) {
                string sampleRate = TestArgOptionValue(args, "-i");
                AddRequiredFile(required, sampleRate == "8000" ? "Test-Files/local/short_nb_voice.wav" : "Test-Files/local/short_wb_voice.wav", "G.722 Standard-Eingang WAV");
            }
        }

        if (test.Id == "g726_tests")
            AddRequiredFile(required, TestArgHasOption(args, "-b") ? "Test-Files/local/short_nb_voice.wav" : "Test-Files/itu/g726", TestArgHasOption(args, "-b") ? "G.726 Sprach-WAV" : "ITU G.726 Testvektor-Ordner");

        if (test.Id == "gsm0610_tests") {
            if (!TestArgHasOption(args, "-l")) {
                for (int sequence = 1; sequence <= 4; sequence++) {
                    string name = $"Seq{sequence:00}";
                    AddRequiredFile(required, $"Test-Files/etsi/gsm0610/unpacked/fr_L/{name}.inp", "ETSI GSM 06.10 Linear Input");
                    AddRequiredFile(required, $"Test-Files/etsi/gsm0610/unpacked/fr_L/{name}.cod", "ETSI GSM 06.10 Linear Coded");
                    AddRequiredFile(required, $"Test-Files/etsi/gsm0610/unpacked/fr_L/{name}.out", "ETSI GSM 06.10 Linear Output");
                    AddRequiredFile(required, $"Test-Files/etsi/gsm0610/unpacked/fr_A/{name}-A.inp", "ETSI GSM 06.10 A-law Input");
                    AddRequiredFile(required, $"Test-Files/etsi/gsm0610/unpacked/fr_A/{name}-A.cod", "ETSI GSM 06.10 A-law Coded");
                    AddRequiredFile(required, $"Test-Files/etsi/gsm0610/unpacked/fr_A/{name}-A.out", "ETSI GSM 06.10 A-law Output");
                    AddRequiredFile(required, $"Test-Files/etsi/gsm0610/unpacked/fr_U/{name}-U.inp", "ETSI GSM 06.10 u-law Input");
                    AddRequiredFile(required, $"Test-Files/etsi/gsm0610/unpacked/fr_U/{name}-U.cod", "ETSI GSM 06.10 u-law Coded");
                    AddRequiredFile(required, $"Test-Files/etsi/gsm0610/unpacked/fr_U/{name}-U.out", "ETSI GSM 06.10 u-law Output");
                }
                AddRequiredFile(required, "Test-Files/etsi/gsm0610/unpacked/fr_L/Seq05.cod", "ETSI GSM 06.10 Linear Seq05 Coded");
                AddRequiredFile(required, "Test-Files/etsi/gsm0610/unpacked/fr_L/Seq05.out", "ETSI GSM 06.10 Linear Seq05 Output");
                AddRequiredFile(required, "Test-Files/etsi/gsm0610/unpacked/fr_A/Seq05-A.out", "ETSI GSM 06.10 A-law Seq05 Output");
                AddRequiredFile(required, "Test-Files/etsi/gsm0610/unpacked/fr_U/Seq05-U.out", "ETSI GSM 06.10 u-law Seq05 Output");
            } else {
                AddRequiredFile(required, "Test-Files/local/short_nb_voice.wav", "GSM 06.10 Sprach-WAV");
            }
        }

        if (test.Id == "lpc10_tests") {
            if (TestArgHasOption(args, "-d"))
                AddRequiredFile(required, "lpc10_in.lpc10", "LPC10 komprimierte Eingabedatei", true);
            else
                AddRequiredFile(required, "Test-Files/local/dam9_lpc55.wav", "LPC10 Referenz WAV");
        }
        if (test.Id == "image_translate_tests")
            AddRequiredFile(required, "Test-Files/local/lenna-colour.tif", "Image-Translate Eingang TIFF");
        if (test.Id is "data_modems_tests" or "t31_tests")
            AddRequiredFile(required, "Test-Files/itu/fax/itu1.tif", "Fax-Modem Eingang TIFF");
        if (test.Id is "echo_tests" or "g168_tests") {
            AddRequiredFile(required, "Test-Files/local/sound_c1_8k.wav", "G.168 CSS Nahsignal WAV");
            AddRequiredFile(required, "Test-Files/local/sound_c3_8k.wav", "G.168 CSS Fernsignal WAV");
        }
        if (test.Id == "modem_echo_tests")
            AddRequiredFile(required, "Test-Files/local/sound_c1_8k.wav", "Modem-Echo CSS Eingang WAV");
        if (test.Id == "fax_decode") {
            string positional = FirstPositionalFile(args, new[] { "-c", "-x", "-y" });
            AddRequiredFile(required, positional.Length == 0 ? "Test-Files/local/fax_samp.wav" : positional, "Fax-Decode Eingang WAV");
        }
        if (test.Id == "t42_tests") {
            string positional = FirstPositionalFile(args, Array.Empty<string>());
            AddRequiredFile(required, positional.Length == 0 ? "Test-Files/itu/t24/F21B400.TIF" : positional, "ITU-T T.24 TIFF", true);
        }
        if (test.Id == "t43_tests") {
            string positional = FirstPositionalFile(args, Array.Empty<string>());
            AddRequiredFile(required, positional.Length == 0 ? "Test-Files/itu/tiff-fx/l04x_02x.tif" : positional, "ITU TIFF-FX Eingang TIFF", true);
        }
        if (test.Id == "echo_tests" && TestArgHasOption(args, "-s")) {
            bool twoChannel = TestArgHasOption(args, "-2");
            string input0 = PositionalFileAt(args, new[] { "-e", "-m", "-M" }, 0);
            string input1 = PositionalFileAt(args, new[] { "-e", "-m", "-M" }, 1);
            string output = PositionalFileAt(args, new[] { "-e", "-m", "-M" }, twoChannel ? 1 : 2);
            if (input0.Length == 0 || (!twoChannel && input1.Length == 0) || output.Length == 0)
                syntheticSkipReason = twoChannel ? "echo_tests -s -2 benoetigt eine zweikanalige Eingabe-WAV und eine Ausgabe-WAV." : "echo_tests -s benoetigt TX-Eingabe-WAV, RX-Eingabe-WAV und Ausgabe-WAV.";
            else {
                AddRequiredFile(required, input0, "Echo-Simulation Eingang WAV 1", true);
                if (!twoChannel)
                    AddRequiredFile(required, input1, "Echo-Simulation Eingang WAV 2", true);
            }
        }
        if (test.Id == "line_model_tests" && TestArgHasOption(args, "-s")) {
            AddRequiredFile(required, "line_model_test_in1.wav", "Line-Model Spracheingang 1", true);
            AddRequiredFile(required, "line_model_test_in2.wav", "Line-Model Spracheingang 2", true);
        }
        if (test.Id is "super_tone_tx_tests" or "super_tone_rx_tests")
            AddRequiredFile(required, "Test-Files/global-tones.xml", "Global Tone XML");
        if (test.Id == "v42bis_tests") {
            string positional = FirstPositionalFile(args, Array.Empty<string>());
            if (positional.Length == 0)
                syntheticSkipReason = "v42bis_tests benoetigt eine Eingabe-Testdatei als Argument; ohne Datei wird der Test als SKIP gewertet.";
            else
                AddRequiredFile(required, positional, "V.42bis Eingabe-Testdatei", true);
        }
    }

    private static RequiredTestInput[] MissingRequiredInputsForTest(TestEntry test, IReadOnlyList<string> args, out string syntheticSkipReason) {
        List<RequiredTestInput> required = new();
        AddRequiredInputsForTest(test, args, required, out syntheticSkipReason);
        return required.Where(input => ResolveExistingRequiredInput(input).Length == 0).ToArray();
    }

    private static bool IsExternalAudioTest(TestEntry test) =>
        test.Id is "ademco_contactid_tests" or "adsi_tests" or "dtmf_rx_tests" or "modem_connect_tones_tests" or "sig_tone_tests" or "super_tone_rx_tests";

    private static int InvokeTestBody(TestEntry test, IReadOnlyList<string> extraArgs) {
        ConfigureChildAssertions();
        string[] testArguments = extraArgs.ToArray();
        ResetProcessGlobalTestState();
        int rc = tkfx_oneexe_test_invoke(test.Run.Invoke, testArguments);
        ResetProcessGlobalTestState();
        return rc;
    }

    private static bool ContainsNonzeroNamedValue(string lower, string name) {
        int position = 0;
        while ((position = lower.IndexOf(name, position, StringComparison.Ordinal)) >= 0) {
            position += name.Length;
            while (position < lower.Length && (lower[position] is ' ' or '\t' or ':' or '='))
                position++;

            bool negative = position < lower.Length && lower[position] == '-';
            if (negative)
                position++;

            ulong value = 0;
            bool hasDigit = false;
            while (position < lower.Length && char.IsAsciiDigit(lower[position])) {
                hasDigit = true;
                value = unchecked(value * 10UL + (ulong)(lower[position] - '0'));
                position++;
            }

            if (hasDigit && (negative || value != 0UL))
                return true;
        }
        return false;
    }

    private static string ClassifyChildLine(string line, string stream) {
        string lower = line.ToLowerInvariant();
        bool Has(string value) => lower.Contains(value, StringComparison.Ordinal);

        if (stream.Equals("RUNNER", StringComparison.Ordinal))
            return "RUNNER";
        if (Has("exception") || Has("stack trace") || lower.StartsWith("   at ", StringComparison.Ordinal) || lower.StartsWith("at ", StringComparison.Ordinal))
            return "EXCEPTION";
        if (lower == "error" || lower.StartsWith("error ", StringComparison.Ordinal) || lower.StartsWith("error:", StringComparison.Ordinal)
            || lower == "fail" || lower.StartsWith("fail ", StringComparison.Ordinal) || lower.StartsWith("fail:", StringComparison.Ordinal)
            || lower.StartsWith("failed", StringComparison.Ordinal) || lower.StartsWith("failure", StringComparison.Ordinal)
            || Has("invalid") || Has("mismatch") || Has("unexpected") || Has("overflow") || Has("underflow")
            || Has("cannot") || Has("can't") || Has("unable") || Has("corrupt") || Has("abort")
            || Has("not implemented") || Has("not functional") || Has("wrong") || Has("out of range") || Has("missing")
            || ContainsNonzeroNamedValue(lower, "bad_bits") || ContainsNonzeroNamedValue(lower, "bad bits")
            || ContainsNonzeroNamedValue(lower, "errors") || ContainsNonzeroNamedValue(lower, "error_count")
            || ContainsNonzeroNamedValue(lower, "resyncs") || ContainsNonzeroNamedValue(lower, "mismatches"))
            return "ERROR";
        if (lower.StartsWith("warning", StringComparison.Ordinal) || lower.StartsWith("warn", StringComparison.Ordinal) || Has(" warning"))
            return "WARNING";
        if (Has("assert") || Has("expected=") || Has("expected ") || Has("actual=") || Has("actual ")
            || Has("got=") || Has("got ") || Has("soll=") || Has("ist="))
            return "ASSERT";
        if (lower.StartsWith("pass", StringComparison.Ordinal) || lower.StartsWith("ok", StringComparison.Ordinal)
            || lower.StartsWith("result", StringComparison.Ordinal) || lower.StartsWith("success", StringComparison.Ordinal))
            return "RESULT";
        if (Has("state") || Has("phase") || Has("status") || Has("event") || Has("carrier") || Has("training")
            || Has("frame") || Has("packet") || Has("page") || Has("tone") || Has("channel") || Has("modem"))
            return "STATE";
        if (Has(" db") || Has("db=") || Has(" hz") || Has("hz=") || Has("samples") || Has("sample=")
            || Has("bits") || Has("bit=") || Has("bytes") || Has("count=") || Has("level=") || Has("power=")
            || Has("snr") || Has("ber") || Has("duration") || Has("elapsed") || Has("rate="))
            return "METRIC";
        return stream.Equals("STDERR", StringComparison.Ordinal) ? "STDERR" : "OUTPUT";
    }

    private static string SanitizeDiagnosticLine(string line, out bool truncated) {
        const int maxLineCharacters = 16_384;
        char[] chars = line.ToCharArray();
        for (int index = 0; index < chars.Length; index++) {
            if (chars[index] < 0x20 && chars[index] != '\t')
                chars[index] = ' ';
        }

        string sanitized = new(chars);
        truncated = sanitized.Length > maxLineCharacters;
        return truncated ? sanitized[..maxLineCharacters] + " ... [ZEILE GEKUERZT]" : sanitized;
    }

    private static int EnvTimeoutMs(string name, int defaultSeconds) {
        string? text = Environment.GetEnvironmentVariable(name);
        int seconds = defaultSeconds;
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed is >= 5 and <= 3600)
            seconds = parsed;
        return checked(seconds * 1000);
    }

    private static bool HasFixed60SecondTimeout(TestEntry test) =>
        test.Id is "v22bis_tests" or "v32bis_tests" or "v34_tests";

    private static int TestIdleTimeoutMs(TestEntry test) =>
        HasFixed60SecondTimeout(test) ? 60_000 : EnvTimeoutMs("TKFX_TEST_TIMEOUT_SEC", 60);

    private static int TestActiveTimeoutMs(TestEntry test) {
        if (HasFixed60SecondTimeout(test))
            return 60_000;
        int idle = TestIdleTimeoutMs(test);
        return Math.Max(idle, EnvTimeoutMs("TKFX_TEST_ACTIVE_TIMEOUT_SEC", 300));
    }

    private static long ProcessCpuTime100Ns(Process process) {
        try {
            return process.TotalProcessorTime.Ticks;
        } catch {
            return 0;
        }
    }

    private static ulong ProcessIoBytes(Process process) {
        if (!OperatingSystem.IsWindows())
            return 0;
        try {
            return GetProcessIoCounters(process.Handle, out IoCounters counters)
                ? counters.ReadTransferCount + counters.WriteTransferCount + counters.OtherTransferCount
                : 0;
        } catch {
            return 0;
        }
    }


    private static ProcessResourceSample SampleProcessResources(Process process, long elapsedMs, long outputBytes) {
        long workingSet = 0;
        long privateMemory = 0;
        int threadCount = 0;
        int handleCount = 0;
        try { workingSet = process.WorkingSet64; } catch { }
        try { privateMemory = process.PrivateMemorySize64; } catch { }
        try { threadCount = process.Threads.Count; } catch { }
        try { handleCount = process.HandleCount; } catch { }
        return new ProcessResourceSample(
            elapsedMs,
            outputBytes,
            ProcessCpuTime100Ns(process),
            workingSet,
            privateMemory,
            threadCount,
            handleCount,
            ProcessIoBytes(process));
    }

    private static void AppendProcessResourceReport(StringBuilder report, ProcessActivityWaitResult waitInfo) {
        report.AppendLine();
        report.AppendLine("Prozessressourcen waehrend des Tests:");
        report.AppendLine($"ResourceSampleCount={waitInfo.Samples.Count}");
        foreach (ProcessResourceSample sample in waitInfo.Samples) {
            report.AppendLine(
                $"  t_ms={sample.ElapsedMs} output_bytes={sample.OutputBytes} cpu_100ns={sample.CpuTime100Ns} " +
                $"working_set={sample.WorkingSetBytes} private_memory={sample.PrivateMemoryBytes} " +
                $"threads={sample.ThreadCount} handles={sample.HandleCount} io_bytes={sample.IoBytes}");
        }
    }

    private static ProcessActivityWaitResult WaitForProcessWithActivity(
        Process process,
        ChildOutputCapture capture,
        int idleTimeoutMs,
        int activeTimeoutMs,
        string testFile) {
        ProcessActivityWaitResult result = new() { WaitResult = WaitFailed, AppliedTimeoutMs = idleTimeoutMs };
        Stopwatch watch = Stopwatch.StartNew();
        long initialOutputBytes = Interlocked.Read(ref capture.RawBytes);
        long initialCpu100Ns = ProcessCpuTime100Ns(process);
        ulong initialIoBytes = ProcessIoBytes(process);
        result.Samples.Add(SampleProcessResources(process, 0, initialOutputBytes));
        ConsoleRunningTimer(testFile, 0);

        for (; ; )
        {
            try {
                if (process.WaitForExit(1000)) {
                    result.WaitResult = WaitObject0;
                    result.OutputBytes = Interlocked.Read(ref capture.RawBytes);
                    result.CpuTime100Ns = ProcessCpuTime100Ns(process);
                    result.IoBytes = ProcessIoBytes(process);
                    result.Samples.Add(SampleProcessResources(process, watch.ElapsedMilliseconds, result.OutputBytes));
                    return result;
                }
            } catch {
                result.WaitResult = WaitFailed;
                result.Samples.Add(SampleProcessResources(process, watch.ElapsedMilliseconds, Interlocked.Read(ref capture.RawBytes)));
                return result;
            }

            result.OutputBytes = Interlocked.Read(ref capture.RawBytes);
            result.CpuTime100Ns = ProcessCpuTime100Ns(process);
            result.IoBytes = ProcessIoBytes(process);
            result.OutputActivity |= result.OutputBytes > initialOutputBytes;
            result.CpuActivity |= result.CpuTime100Ns > initialCpu100Ns + 1_000_000L;
            result.IoActivity |= result.IoBytes > initialIoBytes + 4096UL;
            result.ActivityDetected = result.OutputActivity || result.CpuActivity || result.IoActivity;

            long elapsedMs = watch.ElapsedMilliseconds;
            result.Samples.Add(SampleProcessResources(process, elapsedMs, result.OutputBytes));
            ConsoleRunningTimer(testFile, elapsedMs);
            if (!result.ActivityDetected && elapsedMs >= idleTimeoutMs) {
                result.WaitResult = WaitTimeout;
                result.AppliedTimeoutMs = idleTimeoutMs;
                return result;
            }
            if (result.ActivityDetected && elapsedMs >= activeTimeoutMs) {
                result.WaitResult = WaitTimeout;
                result.AppliedTimeoutMs = activeTimeoutMs;
                return result;
            }
        }
    }

    private static ProcessStartInfo CreateSelfProcessStartInfo(IEnumerable<string> arguments, bool redirectOutput) {
        string processPath = Environment.ProcessPath ?? _exePath;
        if (string.IsNullOrWhiteSpace(processPath))
            processPath = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";

        ProcessStartInfo startInfo = new() {
            FileName = processPath,
            WorkingDirectory = _projectRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = redirectOutput,
            RedirectStandardError = redirectOutput,
            RedirectStandardInput = false
        };

        bool dotnetHost = Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase);
        if (dotnetHost && !string.IsNullOrWhiteSpace(_managedAssemblyPath))
            startInfo.ArgumentList.Add(_managedAssemblyPath);
        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);
        return startInfo;
    }

    private static string DisplayCommand(ProcessStartInfo startInfo) {
        static string Quote(string value) {
            if (value.Length != 0 && !value.Any(static c => char.IsWhiteSpace(c) || c == '"'))
                return value;
            return "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        }

        return string.Join(" ", new[] { Quote(startInfo.FileName) }.Concat(startInfo.ArgumentList.Select(Quote)));
    }

    private static bool IsTwoPeerHarnessTest(TestEntry test) =>
        test.Id is "dummy_modems_tests" or "sprt_tests" or "v150_1_tests" or "v150_1_sse_tests";

    private static bool StartHarnessPeer(TestEntry test, string role, HarnessPeerProcess peer, out int errorCode) {
        errorCode = 0;
        try {
            string[] peerArguments = test.Id == "v150_1_tests"
                ? new[] { "--peer-run", test.Id, role }
                : new[] { "--peer-run", test.Id, role, "-x" };
            ProcessStartInfo startInfo = CreateSelfProcessStartInfo(peerArguments, true);
            string roleName = role == "-a" ? "answerer" : role == "-c" ? "caller" : role;
            peer.Command = DisplayCommand(startInfo);

            Process process = new() {
                StartInfo = startInfo
            };
            process.OutputDataReceived += (_, eventArgs) => {
                if (eventArgs.Data is not null)
                    Console.Error.WriteLine($"HARNESS PEER STDOUT id={test.Id} role={roleName} | {eventArgs.Data}");
            };
            process.ErrorDataReceived += (_, eventArgs) => {
                if (eventArgs.Data is not null)
                    Console.Error.WriteLine($"HARNESS PEER STDERR id={test.Id} role={roleName} | {eventArgs.Data}");
            };

            if (!process.Start()) {
                process.Dispose();
                errorCode = 2;
                return false;
            }

            peer.Process = process;
            Console.Error.WriteLine($"HARNESS PEER START id={test.Id} role={roleName} command={peer.Command}");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return true;
        } catch (global::System.ComponentModel.Win32Exception exception) {
            errorCode = exception.NativeErrorCode;
            return false;
        } catch {
            errorCode = 1;
            return false;
        }
    }

    private static void TerminateHarnessPeer(HarnessPeerProcess peer) {
        try {
            if (peer.Process is { HasExited: false }) {
                peer.Process.Kill(entireProcessTree: true);
                peer.Process.WaitForExit(5000);
            }
            if (peer.Process is { HasExited: true })
                peer.Process.WaitForExit();
        } catch {
            // The peer may already have exited between the state check and Kill.
        }
    }

    private static int RunTwoPeerHarness(TestEntry test) {
        using HarnessPeerProcess answerer = new();
        using HarnessPeerProcess caller = new();
        Console.Error.WriteLine($"HARNESS BEGIN id={test.Id} mode=two-peer deterministic=true");

        if (!StartHarnessPeer(test, "-a", answerer, out int errorCode)) {
            Console.Error.WriteLine($"ERROR HARNESS answerer start failed id={test.Id} win32={errorCode} text={Win32ErrorText(errorCode)}");
            return 910;
        }

        Thread.Sleep(250);
        if (!StartHarnessPeer(test, "-c", caller, out errorCode)) {
            Console.Error.WriteLine($"ERROR HARNESS caller start failed id={test.Id} win32={errorCode} text={Win32ErrorText(errorCode)}");
            TerminateHarnessPeer(answerer);
            return 911;
        }

        Stopwatch watch = Stopwatch.StartNew();
        bool answererDone = false;
        bool callerDone = false;
        int answererRc = 259;
        int callerRc = 259;

        while (!answererDone || !callerDone) {
            if (watch.ElapsedMilliseconds >= 60_000) {
                Console.Error.WriteLine($"ERROR HARNESS timeout id={test.Id} timeout_ms=60000 answerer_done={(answererDone ? 1 : 0)} caller_done={(callerDone ? 1 : 0)}");
                TerminateHarnessPeer(answerer);
                TerminateHarnessPeer(caller);
                return 258;
            }

            if (!answererDone && answerer.Process!.HasExited) {
                answererDone = true;
                answerer.Process.WaitForExit();
                answererRc = answerer.Process.ExitCode;
                Console.Error.WriteLine($"HARNESS PEER END id={test.Id} role=answerer rc={answererRc}");
            }
            if (!callerDone && caller.Process!.HasExited) {
                callerDone = true;
                caller.Process.WaitForExit();
                callerRc = caller.Process.ExitCode;
                Console.Error.WriteLine($"HARNESS PEER END id={test.Id} role=caller rc={callerRc}");
            }

            if ((answererDone && answererRc != 0) || (callerDone && callerRc != 0)) {
                if (!answererDone)
                    TerminateHarnessPeer(answerer);
                if (!callerDone)
                    TerminateHarnessPeer(caller);
                if (!answererDone && answerer.Process!.HasExited)
                    answererRc = answerer.Process.ExitCode;
                if (!callerDone && caller.Process!.HasExited)
                    callerRc = caller.Process.ExitCode;
                Console.Error.WriteLine($"ERROR HARNESS peer failure id={test.Id} answerer_rc={answererRc} caller_rc={callerRc}");
                return answererRc != 0 ? answererRc : callerRc;
            }

            Thread.Sleep(250);
        }

        if (answererRc != 0 || callerRc != 0) {
            Console.Error.WriteLine($"ERROR HARNESS result id={test.Id} answerer_rc={answererRc} caller_rc={callerRc}");
            return answererRc != 0 ? answererRc : callerRc;
        }

        Console.Error.WriteLine($"HARNESS PASS id={test.Id} answerer_rc=0 caller_rc=0");
        return 0;
    }

    private static RunResult RunTest(TestEntry test, IReadOnlyList<string> extraArgs) {
        EnsureLogOpen();
        string[] effectiveArgs = MakeEffectiveTestArgs(test, extraArgs).ToArray();
        Stopwatch watch = Stopwatch.StartNew();
        string scope = $"{test.Id} / {test.File}";
        AppendLogLine($"TEST BEGIN id={test.Id} file={test.File} category={test.Category}");
        AppendLogStep(scope, 1, $"Start: Kategorie={test.Category}");
        AppendLogStep(scope, 2, $"ProjectRoot={_projectRoot}");
        AppendLogStep(scope, 3, $"ExeDir={_exeDir}");
        AppendLogStep(scope, 4, $"WorkingDir={Environment.CurrentDirectory}");
        AppendLogStep(scope, 5, $"ExtraArgs={ExtraArgsText(extraArgs)}");
        AppendLogStep(scope, 5, $"EffectiveArgs={ExtraArgsText(effectiveArgs)}");

        RequiredTestInput[] missingInputs = MissingRequiredInputsForTest(test, effectiveArgs, out string syntheticSkipReason);
        AppendLogStep(scope, 6, $"Pflicht-Testdateien pruefen: missing={missingInputs.Length} synthetic_reason={(syntheticSkipReason.Length == 0 ? "<keine>" : syntheticSkipReason)}");
        if (syntheticSkipReason.Length != 0 || missingInputs.Length != 0) {
            _skipped++;
            string reason = syntheticSkipReason.Length != 0
                ? $"SKIP {test.File} [{test.Category}] external harness requirement: {syntheticSkipReason}"
                : $"SKIP {test.File} [{test.Category}] missing required test input";
            foreach (RequiredTestInput missing in missingInputs)
                reason += $" | {missing.Reason}={RequiredInputDisplayPath(missing)}";
            AppendLogLine(reason);
            string detail = WriteSkipDetailReport(test, extraArgs, effectiveArgs, syntheticSkipReason, missingInputs);
            AppendLogLine($"SKIP detail={(detail.Length == 0 ? "<creation_failed>" : detail)}");
            AppendLogLine($"TEST END id={test.Id} result=SKIP rc=0 reason={(syntheticSkipReason.Length != 0 ? "external_harness_required" : "missing_required_test_input")} detail={(detail.Length == 0 ? "<creation_failed>" : detail)}");
            ConsoleResultLine("SKIP", $" {test.File} - Vorbedingung fehlt; Detail: {(detail.Length == 0 ? "<nicht erzeugt>" : Path.GetFileName(detail))}");
            return new RunResult(0, true, false);
        }

        List<RequiredTestInput> requiredInputs = new();
        AddRequiredInputsForTest(test, effectiveArgs, requiredInputs, out _);

        string stamp = NowStamp(true);
        string detailTmp = Path.Combine(_detailDir, $"{SanitizeFilename(test.Id)}_last.tmp");
        string detailFail = Path.Combine(_detailDir, $"{SanitizeFilename(test.Id)}_FAIL_{stamp}.log");
        string detailNotImpl = Path.Combine(_detailDir, $"{SanitizeFilename(test.Id)}_NOTIMPL_{stamp}.log");
        string detailTimeout = Path.Combine(_detailDir, $"{SanitizeFilename(test.Id)}_TIMEOUT_{stamp}.log");
        ConsoleResultLine("RUN", $"  {test.File} [{test.Category}]");
        try { File.Delete(detailTmp); } catch { }
        Dictionary<string, TestArtifactFileState> artifactSnapshot = CaptureTestArtifactSnapshot();

        ChildOutputCapture? capture = null;
        Process? process = null;
        int createProcessError = 0;
        int idleTimeout = TestIdleTimeoutMs(test);
        int activeTimeout = TestActiveTimeoutMs(test);
        int appliedTimeout = idleTimeout;
        int waitResult = WaitNotRun;
        int exitCode = 999;
        bool timedOut = false;
        int processId = 0;
        ProcessActivityWaitResult waitInfo = new() { AppliedTimeoutMs = idleTimeout };

        ProcessStartInfo startInfo = CreateSelfProcessStartInfo(new[] { "--child-run", test.Id }.Concat(effectiveArgs), true);
        string command = DisplayCommand(startInfo);
        RunnerVariableSnapshot before = MakeRunnerVariableSnapshot(
            "VORHER_DIREKT_VOR_PROCESSSTART", test, scope, effectiveArgs,
            detailTmp, detailFail, detailTimeout, "", command, idleTimeout,
            WaitNotRun, -1, false, 0, 0, false, 0, 0,
            "Vor dem Start des Kindprozesses; DetailTmp ist stdout/stderr-Ziel.");

        try {
            capture = new ChildOutputCapture(detailTmp, test, effectiveArgs, command, requiredInputs);
            ResetProcessGlobalTestState();
            process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, eventArgs) => capture.CaptureLine(eventArgs.Data, "STDOUT");
            process.ErrorDataReceived += (_, eventArgs) => capture.CaptureLine(eventArgs.Data, "STDERR");
            if (!process.Start())
                throw new InvalidOperationException("Process.Start lieferte false.");
            processId = process.Id;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            AppendLogStep(scope, 17, $"Process.Start OK pid={processId}");
            waitInfo = WaitForProcessWithActivity(process, capture, idleTimeout, activeTimeout, test.File);
            waitResult = waitInfo.WaitResult;
            appliedTimeout = waitInfo.AppliedTimeoutMs;
            ConsoleClearRunningTimer();

            if (waitResult == WaitTimeout) {
                timedOut = true;
                try {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                } catch {
                    // The process can terminate while the timeout branch is being entered.
                }
                exitCode = 258;
            } else if (waitResult == WaitObject0) {
                process.WaitForExit();
                exitCode = process.ExitCode;
            } else {
                exitCode = 902;
            }
        } catch (global::System.ComponentModel.Win32Exception exception) {
            createProcessError = exception.NativeErrorCode;
            exitCode = 901;
        } catch (Exception exception) {
            createProcessError = Marshal.GetHRForException(exception);
            exitCode = 901;
            capture?.CaptureLine($"ERROR Process start/run failed: {exception.GetType().FullName}: {exception.Message}", "RUNNER");
        } finally {
            try {
                if (process is not null) {
                    process.WaitForExit(1000);
                    if (process.HasExited)
                        process.WaitForExit();
                }
            } catch {
                // Ignore shutdown races while finalizing the diagnostic capture.
            }
            capture?.Dispose();
            process?.Dispose();
            ConsoleClearRunningTimer();
        }

        TestArtifactCollectionResult artifactResult = CollectChangedTestArtifacts(test.Id, artifactSnapshot);
        if (!timedOut && exitCode == 0 && artifactResult.Failed > 0)
            exitCode = 903;
        ResetProcessGlobalTestState();
        long durationMs = watch.ElapsedMilliseconds;
        string duration = FormatElapsedTime(durationMs, true);

        if (!timedOut && exitCode == 0) {
            _passed++;
            try { File.Delete(detailTmp); } catch { }
            AppendLogLine($"PASS {test.File} [{test.Category}] rc=0 duration={duration} duration_ms={durationMs} detail=<none_by_rule>");
            AppendLogLine($"TEST END id={test.Id} result=PASS rc=0 duration={duration} duration_ms={durationMs}");
            ConsoleResultLine("PASS", $" {test.File} | Dauer {duration}");
            return new RunResult(0, false, false);
        }

        if (!timedOut && exitCode == 77) {
            _notImplemented++;
            MoveFileReplacing(detailTmp, detailNotImpl);
            AppendFileText(detailNotImpl,
                "\r\n----- NOT IMPLEMENTED DETAIL -----\r\n" +
                "Result=NOTIMPL\r\nReturnCode=77\r\n" +
                "ExpectedState=Echter implementierter Test oder klar als NOTIMPL markierter Zweig.\r\n" +
                "ActualState=Testquelle meldet nicht implementierte Pfade.\r\n" +
                "Action=spanDSP-Zweig vollstaendig implementieren oder weiterhin sichtbar NOTIMPL belassen; niemals PASS.\r\n" +
                "----- ENDE NOT IMPLEMENTED DETAIL -----\r\n");
            AppendLogLine($"NOTIMPL {test.File} [{test.Category}] rc=77 duration={duration} duration_ms={durationMs} detail={detailNotImpl}");
            AppendLogLine($"TEST END id={test.Id} result=NOTIMPL rc=77 duration={duration} duration_ms={durationMs}");
            ConsoleResultLine("NOTIMPL", $" {test.File} RC=77 | Detail: {Path.GetFileName(detailNotImpl)}");
            return new RunResult(77, true, false);
        }

        _failed++;
        string finalDetail = timedOut ? detailTimeout : detailFail;
        MoveFileReplacing(detailTmp, finalDetail);
        long childOutputBytes = capture is null ? 0L : Interlocked.Read(ref capture.RawBytes);
        string captureSummary = capture?.CompactSummary() ?? "<capture_not_created>";
        RunnerVariableSnapshot after = MakeRunnerVariableSnapshot(
            timedOut ? "NACHHER_TIMEOUT" : "NACHHER_FAIL", test, scope, effectiveArgs,
            detailTmp, detailFail, detailTimeout, finalDetail, command, appliedTimeout,
            waitResult, exitCode, timedOut, durationMs, createProcessError, processId != 0,
            processId, childOutputBytes,
            $"Aktivitaet={BoolText(waitInfo.ActivityDetected)} Output={BoolText(waitInfo.OutputActivity)} CPU={BoolText(waitInfo.CpuActivity)} IO={BoolText(waitInfo.IoActivity)}");
        string variables = MakeActiveVariableBeforeAfterReport(before, after);
        string diagnostic = MakeFailureDiagnosticReport(
            test, scope, effectiveArgs, finalDetail, command, appliedTimeout, waitResult,
            exitCode, timedOut, durationMs, createProcessError, variables, childOutputBytes, captureSummary, waitInfo);
        AppendFileText(finalDetail, diagnostic);
        AppendLogLine($"{(timedOut ? "TIMEOUT" : "FAIL")} {test.File} [{test.Category}] rc={exitCode} duration={duration} duration_ms={durationMs} detail={finalDetail}");
        AppendLogBlock(diagnostic);
        AppendLogLine($"TEST END id={test.Id} result={(timedOut ? "TIMEOUT" : "FAIL")} rc={exitCode} duration={duration} duration_ms={durationMs}");
        ConsoleResultLine(timedOut ? "TIMEOUT" : "FAIL", $" {test.File} RC={exitCode} | Dauer {duration} - Fehlerdiagnose im Log.");
        return new RunResult(exitCode, false, timedOut);
    }

    private static string NormalizeKey(string value) {
        StringBuilder result = new();
        foreach (char character in value) {
            if (char.IsLetterOrDigit(character))
                result.Append(char.ToLowerInvariant(character));
        }
        return result.ToString();
    }

    private static TestGroup? FindGroup(string key) {
        string normalized = NormalizeKey(key);
        TestGroup? group = Groups.FirstOrDefault(candidate =>
            normalized == NormalizeKey(candidate.Key)
            || normalized == NormalizeKey(candidate.Title)
            || normalized == NormalizeKey(candidate.Category));
        if (group is not null)
            return group;
        return normalized switch {
            "fax30" or "t30" => Groups[3],
            "faximage" or "compression" => Groups[5],
            "sim" or "simulation" or "packet" => Groups[6],
            _ => null
        };
    }

    private static void ListGroups() {
        Console.WriteLine("\nTestgruppen:");
        for (int index = 0; index < Groups.Length; index++) {
            int count = Tests.Count(test => test.Category == Groups[index].Category);
            Console.WriteLine($"{index + 1} = {Groups[index].Title} ({count} Tests)");
        }
    }

    private static void ListTestsForGroup(TestGroup group) {
        Console.WriteLine($"\n{group.Title}:");
        for (int index = 0; index < Tests.Length; index++) {
            TestEntry test = Tests[index];
            if (test.Category == group.Category)
                Console.WriteLine($"{index + 1}. [{test.Category}] {test.File}  id={test.Id}");
        }
    }

    private static int RunGroupTests(TestGroup group) {
        EnsureLogOpen();
        AppendLogLine($"GROUP BEGIN {group.Title} category={group.Category}");
        int passedBefore = _passed;
        int failedBefore = _failed;
        int skippedBefore = _skipped;
        int notImplementedBefore = _notImplemented;
        TestEntry[] selected = Tests.Where(test => test.Category == group.Category).ToArray();
        foreach (TestEntry test in selected)
            RunTest(test, Array.Empty<string>());
        int passed = _passed - passedBefore;
        int failed = _failed - failedBefore;
        int skipped = _skipped - skippedBefore;
        int notImplemented = _notImplemented - notImplementedBefore;
        AppendLogLine($"GROUP SUMMARY {group.Title} passed={passed} failed={failed} skipped={skipped} not_implemented={notImplemented} total={selected.Length}");
        ConsoleSummaryLine($"\n{group.Title} fertig. ", passed, failed, skipped, notImplemented, selected.Length);
        return failed == 0 ? 0 : 1;
    }

    private static int RunAllTests() {
        EnsureLogOpen();
        int passedBefore = _passed;
        int failedBefore = _failed;
        int skippedBefore = _skipped;
        int notImplementedBefore = _notImplemented;
        foreach (TestEntry test in Tests)
            RunTest(test, Array.Empty<string>());
        int passed = _passed - passedBefore;
        int failed = _failed - failedBefore;
        int skipped = _skipped - skippedBefore;
        int notImplemented = _notImplemented - notImplementedBefore;
        AppendLogLine($"SUMMARY passed={passed} failed={failed} skipped={skipped} not_implemented={notImplemented} total={Tests.Length}");
        ConsoleSummaryLine("\nFertig. ", passed, failed, skipped, notImplemented, Tests.Length);
        return failed == 0 ? 0 : 1;
    }

    private static void ListTests() {
        for (int index = 0; index < Tests.Length; index++) {
            TestEntry test = Tests[index];
            Console.WriteLine($"{index + 1}. [{test.Category}] {test.File}  id={test.Id}");
        }
    }

    private static TestEntry? FindTest(string key) {
        if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) && index >= 1 && index <= Tests.Length)
            return Tests[index - 1];
        return Tests.FirstOrDefault(test => key == test.Id || key == test.File);
    }

    private static void PauseToMenu() {
        Console.Write("\nDruecke ENTER fuer zurueck zum Menue...");
        Console.ReadLine();
    }

    private static void PrependDirectoryToProcessPath(string directory) {
        if (directory.Length == 0)
            return;
        string current = Environment.GetEnvironmentVariable("PATH") ?? "";
        string separator = Path.PathSeparator.ToString();
        Environment.SetEnvironmentVariable("PATH", current.Length == 0 ? directory : directory + separator + current);
    }

    private static void WriteStartupFailure(string message) {
        try {
            if (_logDir.Length == 0)
                return;
            Directory.CreateDirectory(_logDir);
            File.AppendAllText(
                Path.Combine(_logDir, "Startfehler.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}\r\n",
                new UTF8Encoding(false));
        } catch {
            // Der urspruengliche Startfehler bleibt die massgebliche Fehlermeldung.
        }
    }

    private static bool SetupRuntimePaths(bool reuseInheritedRunRoot) {
        try {
            _managedAssemblyPath = Assembly.GetEntryAssembly()?.Location ?? Assembly.GetExecutingAssembly().Location;
            _exePath = Environment.ProcessPath ?? _managedAssemblyPath;
            _exeDir = Path.GetDirectoryName(_managedAssemblyPath);
            if (string.IsNullOrWhiteSpace(_exeDir))
                _exeDir = Environment.CurrentDirectory;

            _exeDir = Path.GetFullPath(_exeDir);
            _projectRoot = _exeDir;
            _testsDir = Path.Combine(_exeDir, "Test-Files");

            string inheritedRunRoot = reuseInheritedRunRoot
                ? Environment.GetEnvironmentVariable(RunRootEnvironmentVariable) ?? ""
                : "";
            _runRootDir = inheritedRunRoot.Length == 0
                ? CreateUniqueRunRootDirectory(_exeDir)
                : Path.GetFullPath(inheritedRunRoot);
            _logDir = Path.Combine(_runRootDir, "Logs");
            _outputDir = Path.Combine(_runRootDir, "Ausgabe");
            Directory.CreateDirectory(_logDir);
            Directory.CreateDirectory(_outputDir);

            Environment.SetEnvironmentVariable(RunRootEnvironmentVariable, _runRootDir);
            Environment.SetEnvironmentVariable(LogDirectoryEnvironmentVariable, _logDir);
            Environment.SetEnvironmentVariable(OutputDirectoryEnvironmentVariable, _outputDir);

            PrependDirectoryToProcessPath(_exeDir);
            Environment.CurrentDirectory = _exeDir;

            string[] runtimeMarkers =
            {
                Path.Combine(_testsDir, "global-tones.xml"),
                Path.Combine(_testsDir, "tsb85.xml"),
                Path.Combine(_testsDir, "itu", "fax", "itutests.tif"),
                Path.Combine(_testsDir, "local", "short_nb_voice.wav")
            };
            bool runtimeReady = true;
            foreach (string marker in runtimeMarkers) {
                if (!File.Exists(marker)) {
                    runtimeReady = false;
                    Console.Error.WriteLine($"FEHLER: Test-Laufzeitdatei fehlt im Build-Ausgabeordner: {marker}");
                }
            }
            if (!runtimeReady) {
                const string message = "FEHLER: Build-Ausgabe ist unvollstaendig. Projekt neu erstellen, damit Test-Files kopiert werden.";
                Console.Error.WriteLine(message);
                WriteStartupFailure(message);
                return false;
            }

            if (!ValidateG168CssInputs(false)) {
                const string message = "FEHLER: G.168-CSS-Testdateien sind unvollstaendig oder formal ungueltig.";
                Console.Error.WriteLine(message);
                WriteStartupFailure(message);
                return false;
            }

            return true;
        } catch (Exception exception) {
            string message = $"FEHLER: Laufzeitpfade konnten nicht vorbereitet werden: {exception.Message}";
            Console.Error.WriteLine(message);
            WriteStartupFailure(message);
            return false;
        }
    }
}
