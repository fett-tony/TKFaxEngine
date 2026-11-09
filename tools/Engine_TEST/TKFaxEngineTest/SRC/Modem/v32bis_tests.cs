/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * v32bis_tests.cs - Managed C# port of v32bis_tests.c.
 * Original test written by Steve Underwood <steveu@coppice.org>.
 *
 * The native source intentionally returns 77 before executing the regression
 * body because V.32bis is explicitly work-in-progress. That visible behavior
 * is retained. The complete post-return regression flow is ported in
 * PortedRegressionBody for source parity and future activation.
 */

#nullable enable
using SignalStatus = TKFaxEngine.SignalStatus;

using System.Globalization;
using TKFaxEngine.Modem;
using TKFaxEngine.Modem.V17;
using TKFaxEngine.Modem.V32;
using TKFaxEngineTest.Network.Test;
using static TKFaxEngine.AsyncApi;
using static TKFaxEngine.Modem.BertApi;
using static TKFaxEngine.Modem.V32.V32Bis;
using static TKFaxEngineTest.Network.Test.line_model;

namespace TKFaxEngineTest.Modem.Test;

public static class v32bis_tests {
    private const int BLOCK_LEN = 160;
    private const string OUT_FILE_NAME = "v32bis.wav";
    private const int MUNGE_CODEC_NONE = 0;
    private const int V32BIS_MAX_SHUTDOWN_BAD_BITS = 20;

    private static string? decode_test_file = null;
    private static int rx_bits;
    private static BertState caller_bert = null!;
    private static BertState answerer_bert = null!;
    private static BertState caller_rx_bert = null!;
    private static BertState answerer_rx_bert = null!;
    private static v32bis_state_t caller = null!;
    private static v32bis_state_t answerer = null!;

    private sealed class BertReportControl {
        public required string Name { get; init; }
        public BertResults LatestResults { get; set; } = new();
        public int LastReportTotalBits { get; set; }
        public int LastReportBadBits { get; set; }
        public int LastReportResyncs { get; set; }
    }

    private sealed class QamReportControl {
        public required v32bis_state_t State { get; init; }
        public float SmoothPower { get; set; }
        public int SymbolNo { get; set; }
    }

    public static int main(string[] testArguments) {
        try { return tkfx_v32bis_tests_body_main(testArguments); } catch (V32BisTestsExitException ex) { return ex.ExitCode; }
    }

    public static int tkfx_v32bis_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);
        Console.Error.WriteLine("NOT IMPLEMENTED: V.32bis engine path is explicitly work-in-progress/not functional and currently delegates core modem processing to V.17.");
        return 77;
    }

    // Port of all native statements after the source's unconditional return 77.
    // It is deliberately not called while the production engine remains marked
    // non-functional, exactly matching the active native build path.
    internal static int PortedRegressionBody(string[] testArguments) {
        bool testFailed = false;
        int channelCodec = MUNGE_CODEC_NONE;
        int testBps = 14_400;
        int lineModelNo = 0;
        int noiseLevel = -70;
        int signalLevel = -13;
        float echoLevel = -99.0f;
        int bitsPerTest = 50_000;
        bool logAudio = false;
        ParseArguments(testArguments, ref channelCodec, ref testBps, ref lineModelNo,
            ref noiseLevel, ref signalLevel, ref echoLevel, ref bitsPerTest, ref logAudio);

        V29V34Pcm16WaveWriter? output = logAudio ? new V29V34Pcm16WaveWriter(OUT_FILE_NAME, 8000, 2) : null;
        both_ways_line_model_state_t? model = null;

        var callerReport = new BertReportControl { Name = "caller-rx" };
        var answererReport = new BertReportControl { Name = "answerer-rx" };

        try {
            caller = v32bis_init(null, testBps, true, v32bis_getbit, null, v32bis_putbit, null)
                ?? throw new InvalidOperationException("Cannot initialize the calling V.32bis modem.");
            answerer = v32bis_init(null, testBps, false, v32bis_getbit, null, v32bis_putbit, null)
                ?? throw new InvalidOperationException("Cannot initialize the answering V.32bis modem.");
            v32bis_set_get_bit(caller, v32bis_getbit, caller);
            v32bis_set_put_bit(caller, v32bis_putbit, caller);
            v32bis_set_get_bit(answerer, v32bis_getbit, answerer);
            v32bis_set_put_bit(answerer, v32bis_putbit, answerer);
            v32bis_tx_power(caller, signalLevel);
            v32bis_tx_power(answerer, signalLevel);

            var callerQam = new QamReportControl { State = caller };
            var answererQam = new QamReportControl { State = answerer };
            v32bis_set_qam_report_handler(caller, qam_report, callerQam);
            v32bis_set_qam_report_handler(answerer, qam_report, answererQam);

            caller_bert = bert_init(null, bitsPerTest, BERT_PATTERN_ITU_O152_11, testBps, 20);
            answerer_bert = bert_init(null, bitsPerTest, BERT_PATTERN_ITU_O152_11, testBps, 20);
            caller_rx_bert = bert_init(null, bitsPerTest, BERT_PATTERN_ITU_O152_11, testBps, 20);
            answerer_rx_bert = bert_init(null, bitsPerTest, BERT_PATTERN_ITU_O152_11, testBps, 20);
            bert_set_report(caller_rx_bert, 10_000, reporter, callerReport);
            bert_set_report(answerer_rx_bert, 10_000, reporter, answererReport);

            model = both_ways_line_model_init(
                lineModelNo, noiseLevel, echoLevel, echoLevel,
                lineModelNo, noiseLevel, echoLevel, echoLevel,
                channelCodec, 0);
            if (model is null) Fail("    Failed to create line model");

            var callerAmp = new short[BLOCK_LEN];
            var answererAmp = new short[BLOCK_LEN];
            var callerModelAmp = new short[BLOCK_LEN];
            var answererModelAmp = new short[BLOCK_LEN];
            var stereo = new short[2 * BLOCK_LEN];
            bool callerTxComplete = false;
            bool answererTxComplete = false;

            for (; ; )
            {
                int callerSamples = callerTxComplete ? 0 : v32bis_tx(caller, callerAmp, BLOCK_LEN);
                int answererSamples = answererTxComplete ? 0 : v32bis_tx(answerer, answererAmp, BLOCK_LEN);
                if (callerSamples == 0) callerTxComplete = true;
                if (answererSamples == 0) answererTxComplete = true;
                if (callerTxComplete && answererTxComplete) break;

                int samples = Math.Max(callerSamples, answererSamples);
                if (callerSamples < samples) Array.Clear(callerAmp, callerSamples, samples - callerSamples);
                if (answererSamples < samples) Array.Clear(answererAmp, answererSamples, samples - answererSamples);

                both_ways_line_model(model, callerModelAmp, callerAmp, answererModelAmp, answererAmp, samples);
                v32bis_rx(answerer, callerModelAmp, samples);
                v32bis_rx(caller, answererModelAmp, samples);

                if (logAudio) {
                    Array.Clear(stereo, 0, stereo.Length);
                    for (int i = 0; i < samples; i++) {
                        stereo[2 * i] = callerModelAmp[i];
                        stereo[2 * i + 1] = answererModelAmp[i];
                    }
                    output!.WriteFrames(stereo);
                }
            }

            if (decode_test_file is null) {
                bert_result(caller_rx_bert, callerReport.LatestResults);
                bert_result(answerer_rx_bert, answererReport.LatestResults);
                PrintFinal(callerReport);
                PrintFinal(answererReport);

                if (callerReport.LatestResults.BadBits > V32BIS_MAX_SHUTDOWN_BAD_BITS
                    || callerReport.LatestResults.Resyncs != 0
                    || answererReport.LatestResults.BadBits > V32BIS_MAX_SHUTDOWN_BAD_BITS
                    || answererReport.LatestResults.Resyncs != 0) {
                    testFailed = true;
                }
            }

            if (testFailed) {
                Console.Error.WriteLine($"ERROR V.32bis BERT exceeded limit: max_bad_bits={V32BIS_MAX_SHUTDOWN_BAD_BITS}, resyncs must be zero");
                return 2;
            }
            return 0;
        } finally {
            if (model is not null) both_ways_line_model_free(model);
            ReleaseBert(caller_bert); ReleaseBert(answerer_bert);
            ReleaseBert(caller_rx_bert); ReleaseBert(answerer_rx_bert);
            if (caller is not null) v32bis_release(caller);
            if (answerer is not null) v32bis_release(answerer);
            output?.Dispose();
        }
    }

    private static void reporter(object? userData, int reason, BertResults results) {
        var report = (BertReportControl)(userData ?? throw new ArgumentNullException(nameof(userData)));
        int deltaBits = results.TotalBits - report.LastReportTotalBits;
        int deltaBad = results.BadBits - report.LastReportBadBits;
        int deltaResyncs = results.Resyncs - report.LastReportResyncs;
        if (deltaBits < 0 || deltaBad < 0 || deltaResyncs < 0) {
            deltaBits = results.TotalBits;
            deltaBad = results.BadBits;
            deltaResyncs = results.Resyncs;
        }

        if (reason == BERT_REPORT_REGULAR) {
            report.LatestResults = results.Clone();
            if (deltaBad > 0 || deltaResyncs > 0)
                Console.Error.WriteLine($"ERROR V.32bis channel={report.Name} interval_bits={deltaBits} new_bad_bits={deltaBad} new_resyncs={deltaResyncs} total_bits={results.TotalBits} total_bad_bits={results.BadBits} total_resyncs={results.Resyncs}");
            report.LastReportTotalBits = results.TotalBits;
            report.LastReportBadBits = results.BadBits;
            report.LastReportResyncs = results.Resyncs;
        } else if (reason == BERT_REPORT_UNSYNCED || reason == BERT_REPORT_GT_10_2) {
            Console.Error.WriteLine($"ERROR V.32bis channel={report.Name} BERT {bert_event_to_str(reason)} interval_bits={deltaBits} new_bad_bits={deltaBad} new_resyncs={deltaResyncs}");
        }
    }

    private static void v32bis_rx_status(object? userData, int status) {
        if (status == (int)SignalStatus.TrainingFailed || status == (int)SignalStatus.Abort || status == (int)SignalStatus.Break)
            Console.Error.WriteLine($"ERROR V.32bis rx {userData} status {signal_status_to_str(status)} ({status})");
    }

    private static void v32bis_putbit(object? userData, int bit) {
        if (bit < 0) { v32bis_rx_status(userData, bit); return; }
        if (decode_test_file is not null) { rx_bits++; return; }
        if (ReferenceEquals(userData, caller)) bert_put_bit(caller_rx_bert, bit);
        else bert_put_bit(answerer_rx_bert, bit);
    }

    private static int v32bis_getbit(object? userData) =>
        ReferenceEquals(userData, caller) ? bert_get_bit(caller_bert) : bert_get_bit(answerer_bert);

    private static void qam_report(object? userData, V17RxComplex received, V17RxComplex target, int symbol) {
        var state = (QamReportControl)(userData ?? throw new ArgumentNullException(nameof(userData)));
        float dr = received.Real - target.Real;
        float di = received.Imaginary - target.Imaginary;
        float power = dr * dr + di * di;
        state.SmoothPower = 0.95f * state.SmoothPower + 0.05f * power;
        state.SymbolNo++;
        _ = symbol;
    }

    private static void ParseArguments(string[] testArguments, ref int codec, ref int bps,
        ref int model, ref int noise, ref int signal, ref float echo, ref int bits, ref bool log) {
        for (int i = 0; i < testArguments.Length; i++) {
            switch (testArguments[i]) {
                case "-b": bps = Int(Next(testArguments, ref i)); break;
                case "-B": bits = Int(Next(testArguments, ref i)); break;
                case "-c": codec = Int(Next(testArguments, ref i)); break;
                case "-e": echo = Int(Next(testArguments, ref i)); break;
                case "-g": Console.Error.WriteLine("Graphical monitoring not available"); Exit(2); break;
                case "-l": log = true; break;
                case "-m": model = Int(Next(testArguments, ref i)); break;
                case "-n": noise = Int(Next(testArguments, ref i)); break;
                case "-s": signal = Int(Next(testArguments, ref i)); break;
                default: Exit(2); break;
            }
        }
        if (bps is not (14400 or 12000 or 9600 or 7200 or 4800)) {
            Console.Error.WriteLine("Invalid bit rate specified");
            Exit(2);
        }
    }

    private static void PrintFinal(BertReportControl report) =>
        Console.Error.WriteLine($"V.32bis final channel={report.Name} total_bits={report.LatestResults.TotalBits} bad_bits={report.LatestResults.BadBits} resyncs={report.LatestResults.Resyncs}");

    private static void ReleaseBert(BertState? state) { if (state is not null) bert_release(state); }
    private static string Next(string[] testArguments, ref int i) { if (++i >= testArguments.Length) Exit(2); return testArguments[i]; }
    private static int Int(string value) { if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)) Exit(2); return result; }
    private static void Fail(string message) { Console.Error.WriteLine(message); Exit(2); }
    internal static void Exit(int code) => throw new V32BisTestsExitException(code);

    public sealed class V32BisTestsExitException : Exception {
        public V32BisTestsExitException(int exitCode) : base($"v32bis_tests exited with code {exitCode}.") => ExitCode = exitCode;
        public int ExitCode { get; }
    }
}
