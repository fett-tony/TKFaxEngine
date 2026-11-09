/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * tsb85_tests.c
 *
 * Original test written by Steve Underwood <steveu@coppice.org>.
 * Managed C# port preserving the TSB-85 XML test selection, analogue
 * sample exchange, call-clear timing checks and optional stereo WAV log.
 *
 * This source uses the separately ported FaxTesterState/FaxTesterApi test
 * harness. That harness owns the XML script engine and drives the concrete
 * FaxState and T.30 implementation directly.
 */

#nullable enable
using FaxState = TKFaxEngine.FaxState;
using SpanLogState = TKFaxEngine.SpanLogState;

using System.Text;
using static TKFaxEngine.FaxApi;
using static TKFaxEngine.LoggingApi;
using static TKFaxEngineTest.Fax.Test.fax_tester;

namespace TKFaxEngineTest.Fax.Test;

public static class Tsb85Tests {
    private const string OUTPUT_TIFF_FILE_NAME = "tsb85.tif";
    private const string OUTPUT_WAVE_FILE_NAME = "tsb85.wav";
    private const int SAMPLES_PER_CHUNK = 160;
    private const int SAMPLE_RATE = 8000;

    private static Pcm16WaveWriter? outHandle;
    public static string output_tiff_file_name = OUTPUT_TIFF_FILE_NAME;
    public static bool log_audio;
    private static FaxTesterState? state;

    public static IFaxTestsRuntime? Runtime { get; set; }

    public sealed class ExitException : Exception {
        public ExitException(int exitCode) => ExitCode = exitCode;
        public int ExitCode { get; }
    }

    private static void Exit(int code) => throw new ExitException(code);

    private static int FullLogLevel =>
        SPAN_LOG_SHOW_SEVERITY |
        SPAN_LOG_SHOW_PROTOCOL |
        SPAN_LOG_SHOW_TAG |
        SPAN_LOG_SHOW_SAMPLE_TIME |
        SPAN_LOG_FLOW;

    private static void exchange(FaxTesterState tester) {
        var amplitudes = new short[SAMPLES_PER_CHUNK];
        var interleaved = new short[2 * SAMPLES_PER_CHUNK];
        output_tiff_file_name = OUTPUT_TIFF_FILE_NAME;

        if (log_audio) {
            try {
                outHandle = new Pcm16WaveWriter(
                    OUTPUT_WAVE_FILE_NAME,
                    SAMPLE_RATE,
                    2);
            } catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or ArgumentException) {
                Console.Error.WriteLine(
                    $"    Cannot create audio file '{OUTPUT_WAVE_FILE_NAME}'");
                Console.WriteLine("Test failed");
                Exit(2);
            }
        }

        int totalAudioTime = 0;
        faxtester_set_transmit_on_idle(tester, 1);

        _ = Runtime ?? fax_tests.Runtime
            ?? throw new InvalidOperationException("A FAX test runtime must be configured before running TSB-85 tests.");

        FaxState farFax = fax_init(null, false);
        tester.FarFax = farFax;
        tester.FarT30 = fax_get_t30_state(farFax);
        tester.FarTag = 'A';

        span_log_set_level(farFax.Logging, FullLogLevel);
        span_log_set_tag(farFax.Logging, "A");

        SpanLogState testerLogging = faxtester_get_logging_state(tester);
        span_log_set_level(testerLogging, FullLogLevel);
        span_log_set_tag(testerLogging, "B");
        tester.FarT30.Logging.Sink = (_, message) =>
            span_log(testerLogging, SPAN_LOG_FLOW, "%s", message);

        while (faxtester_next_step(tester) == 0) {
        }

        for (; ; ) {
            int length = fax_tx(
                farFax,
                amplitudes,
                SAMPLES_PER_CHUNK);
            faxtester_rx(tester, amplitudes, length);

            if (log_audio) {
                for (int index = 0; index < length; index++)
                    interleaved[2 * index] = amplitudes[index];
            }

            totalAudioTime += SAMPLES_PER_CHUNK;
            span_log_bump_samples(testerLogging, length);

            length = faxtester_tx(
                tester,
                amplitudes,
                SAMPLES_PER_CHUNK);
            fax_rx(farFax, amplitudes, length);

            if (!faxtester_far_call_active(tester) &&
                !(tester.TestForCallClear && !tester.FarEndClearedCall)) {
                span_log(
                    testerLogging,
                    SPAN_LOG_FLOW,
                    "Far end T.30 call completed - terminating test loop\n");
                break;
            }

            if (log_audio) {
                for (int index = 0; index < length; index++)
                    interleaved[2 * index + 1] = amplitudes[index];

                try {
                    outHandle!.WriteFrames(interleaved);
                } catch (IOException) {
                    break;
                }
            }

            if (tester.TestForCallClear && !tester.FarEndClearedCall) {
                tester.CallClearTimer += length;
                if (!faxtester_far_call_active(tester)) {
                    span_log(
                        testerLogging,
                        SPAN_LOG_FLOW,
                        "Far end cleared after %dms (limits %dms to %dms)\n",
                        tester.CallClearTimer / 8,
                        tester.TimeInMilliseconds,
                        tester.Timeout);

                    if (tester.CallClearTimer / 8 < tester.TimeInMilliseconds ||
                        tester.CallClearTimer / 8 > tester.TimeoutMilliseconds) {
                        Console.WriteLine("Test failed");
                        Exit(2);
                    }

                    span_log(
                        testerLogging,
                        SPAN_LOG_FLOW,
                        "Clear time OK\n");
                    tester.FarEndClearedCall = true;
                    tester.TestForCallClear = false;
                    while (faxtester_next_step(tester) == 0) {
                    }
                }
            }
        }

        _ = totalAudioTime;

        if (log_audio && outHandle is not null) {
            try {
                outHandle.Dispose();
                outHandle = null;
            } catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException) {
                Console.Error.WriteLine(
                    $"    Cannot close audio file '{OUTPUT_WAVE_FILE_NAME}'");
                Console.WriteLine("Test failed");
                Exit(2);
            }
        }
    }

    public static int tkfx_tsb85_tests_body_main(string[] testArguments) {
        string xmlFileName = "Test-Files/tsb85.xml";
        string testName = "MRGN01";
        log_audio = false;

        int argumentCount = testArguments.Length;
        int index = 0;
        while (index < argumentCount) {
            string option = testArguments[index];
            if (option == "-l") {
                log_audio = true;
                index++;
                continue;
            }

            if (option == "-x") {
                if (++index >= argumentCount)
                    Exit(2);
                xmlFileName = testArguments[index++];
                continue;
            }

            if (option.StartsWith("-", StringComparison.Ordinal))
                Exit(2);

            break;
        }

        if (index < argumentCount)
            testName = testArguments[index];

        state = faxtester_init(null, xmlFileName, testName);
        if (state is null) {
            Console.Error.WriteLine("Cannot start FAX tester instance");
            Console.WriteLine("Test failed");
            Exit(2);
        }

        SpanLogState logging = faxtester_get_logging_state(state);
        span_log_set_level(logging, FullLogLevel);
        span_log_set_tag(logging, "B");

        exchange(state);
        faxtester_free(state);
        state = null;
        Console.WriteLine("Done");
        return 0;
    }

    private sealed class Pcm16WaveWriter : IDisposable {
        private readonly FileStream stream;
        private readonly BinaryWriter writer;
        private readonly short channels;
        private readonly int sampleRate;
        private long sampleCount;
        private bool disposed;

        public Pcm16WaveWriter(string path, int sampleRate, short channels) {
            if (sampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (channels <= 0)
                throw new ArgumentOutOfRangeException(nameof(channels));

            this.sampleRate = sampleRate;
            this.channels = channels;
            stream = File.Create(path);
            writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
            WriteHeader(0);
        }

        public void WriteFrames(ReadOnlySpan<short> interleavedSamples) {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (interleavedSamples.Length % channels != 0)
                throw new ArgumentException(
                    "The sample count is not a whole number of frames.",
                    nameof(interleavedSamples));

            foreach (short sample in interleavedSamples)
                writer.Write(sample);
            sampleCount += interleavedSamples.Length;
        }

        public void Dispose() {
            if (disposed)
                return;

            long end = stream.Position;
            long dataBytes = checked(sampleCount * sizeof(short));
            stream.Position = 4;
            writer.Write(checked((int)(36 + dataBytes)));
            stream.Position = 40;
            writer.Write(checked((int)dataBytes));
            stream.Position = end;
            writer.Flush();
            writer.Dispose();
            stream.Dispose();
            disposed = true;
        }

        private void WriteHeader(int dataBytes) {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataBytes);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * sizeof(short));
            writer.Write((short)(channels * sizeof(short)));
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataBytes);
        }
    }
}
