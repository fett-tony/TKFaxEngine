/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * v34_tests.cs - Managed C# port of v34_tests.c.
 * Original test written by Steve Underwood <steveu@coppice.org>.
 *
 * The source intentionally returns 77 before its V.34 regression body because
 * the engine path cannot yet complete training/data transfer. That active
 * behavior is retained. All endpoint, V.8, HDLC, mapping-frame, decode-file
 * and bidirectional line-model logic after the return is ported below.
 */

#nullable enable
using SignalStatus = TKFaxEngine.SignalStatus;

using System.Globalization;
using TKFaxEngine.Modem;
using TKFaxEngine.Modem.V34;
using TKFaxEngine.Modem.V8;
using TKFaxEngineTest.Network.Test;
using static TKFaxEngine.AsyncApi;
using static TKFaxEngine.Modem.HdlcApi;
using static TKFaxEngine.Modem.V34.v34;
using static TKFaxEngine.Modem.V8.V8Api;
using static TKFaxEngineTest.Network.Test.line_model;

namespace TKFaxEngineTest.Modem.Test;

public static class v34_tests {
    private const int SAMPLES_PER_CHUNK = 160;
    private const string OUT_FILE_NAME = "v34_tests.wav";
    private const int MUNGE_CODEC_NONE = 0;
    private const int V34_MAX_SHUTDOWN_BAD_BITS = 20;
    private const int V34_POST_EOD_TAIL_SAMPLES = 2 * 8000;
    private const int V34_MAX_SIMULATION_SAMPLES = 60 * 8000;

    private static readonly HashSet<int> ValidBitRates =
    [
        2400, 2600, 4800, 5000, 7200, 7400, 9600, 9800,
        12000, 12200, 14400, 14600, 16800, 17000, 19200, 19400,
        21600, 21800, 24000, 24200, 26400, 26600, 28800, 29000,
        31200, 31400, 33600, 33800
    ];

    private sealed class Endpoint {
        public required string Name { get; init; }
        public Endpoint? Peer { get; set; }
        public required byte[] TxValues { get; init; }
        public int TxCapacity => TxValues.Length;
        public int TxCount { get; set; }
        public int RxCount { get; set; }
        public int BadBits { get; set; }
        public int MissingExpectedBits { get; set; }
        public int InvalidAuxBits { get; set; }
        public bool TxEod { get; set; }
        public bool RxHasValues { get; set; }
        public bool TrainingSucceeded { get; set; }
        public bool TrainingFailed { get; set; }
    }

    private static Endpoint caller_test = null!;
    private static Endpoint answerer_test = null!;
    private static bool v8_negotiation_failed;
    private static string? decode_test_file;
    private static V8State v8_caller = null!;
    private static V8State v8_answerer = null!;
    private static v34_state_t v34_caller = null!;
    private static v34_state_t v34_answerer = null!;
    private static int test_bps;
    private static int test_baud_rate;
    private static HdlcReceiver? decodeHdlc;

    public static int main(string[] testArguments) {
        try { return tkfx_v34_tests_body_main(testArguments); } catch (V34TestsExitException ex) { return ex.ExitCode; }
    }

    public static int tkfx_v34_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);
        Console.Error.WriteLine("NOT IMPLEMENTED: V.34 engine path is explicitly work-in-progress/not functional and does not currently complete training or data transfer.");
        return 77;
    }

    // Exact managed counterpart of the source statements following the
    // unconditional return 77. It remains dormant until the engine itself is
    // promoted from its explicit work-in-progress state.
    internal static int PortedRegressionBody(string[] testArguments) {
        int channelCodec = MUNGE_CODEC_NONE;
        test_baud_rate = 3429;
        test_bps = 33_600;
        int lineModelNo = 0;
        int noiseLevel = -70;
        int signalLevel = -13;
        float echoLevel = -99.0f;
        int bitsPerTest = 50_000;
        decode_test_file = null;
        bool logAudio = false;
        bool callingParty = true;
        bool test4d = false;
        bool duplex = true;

        int parseResult = ParseArguments(testArguments, ref channelCodec, ref lineModelNo,
            ref noiseLevel, ref signalLevel, ref echoLevel, ref bitsPerTest,
            ref logAudio, ref callingParty, ref test4d, ref duplex);
        if (parseResult != 0) return parseResult;
        if (bitsPerTest <= 0) {
            Console.Error.WriteLine($"ERROR V.34 test bit count invalid expected_gt=0 actual={bitsPerTest}");
            return 2;
        }

        int capacity = test4d ? Math.Max(bitsPerTest, 20_000) : bitsPerTest;
        caller_test = new Endpoint { Name = "caller", TxValues = new byte[capacity] };
        answerer_test = new Endpoint { Name = "answerer", TxValues = new byte[capacity] };
        caller_test.Peer = answerer_test;
        answerer_test.Peer = caller_test;
        v8_negotiation_failed = false;

        if (test4d)
            return v34_mapping_frame_tests(test_baud_rate, test_bps, duplex);

        return decode_test_file is not null
            ? DecodeFile(callingParty, duplex, bitsPerTest, logAudio)
            : RunBidirectional(channelCodec, lineModelNo, noiseLevel, signalLevel,
                echoLevel, bitsPerTest, logAudio, duplex);
    }

    private static int DecodeFile(bool callingParty, bool duplex, int bitsPerTest, bool logAudio) {
        bool failed = false;
        V29V34Pcm16WaveReader? input = null;
        V29V34Pcm16WaveWriter? output = null;
        bool initialized = false;

        try {
            try { input = new V29V34Pcm16WaveReader(decode_test_file!, 8000, 1); } catch {
                Console.Error.WriteLine($"ERROR V.34 audio input open expected=SUCCESS actual=FAILED file={decode_test_file}");
                return 2;
            }

            if (logAudio)
                output = new V29V34Pcm16WaveWriter(OUT_FILE_NAME, 8000, 1);

            v34_state_t? initializedCaller = v34_init(null, test_baud_rate, test_bps, callingParty, duplex,
                v34_get_bit, caller_test, v34_decode_putbit, caller_test);
            if (initializedCaller is null) {
                Console.Error.WriteLine("ERROR V.34 decode init expected=SUCCESS actual=NULL");
                return 2;
            }
            v34_caller = initializedCaller;
            initialized = true;
            v34_set_get_aux_bit(v34_caller, v34_get_aux_bit, caller_test);
            v34_set_put_aux_bit(v34_caller, v34_put_aux_bit, caller_test);

            var samples = new short[SAMPLES_PER_CHUNK];
            for (; ; )
            {
                int count = input.ReadFrames(samples);
                if (count <= 0) break;
                int processed = v34_rx(v34_caller, samples.AsSpan(0, count), count);
                if (processed != count) {
                    Console.Error.WriteLine($"ERROR V.34 decode receive length expected={count} actual={processed} input_buffer={BufferState(samples, count)}");
                    failed = true;
                    break;
                }

                int generated = v34_tx(v34_caller, samples.AsSpan(0, count), count);
                if (generated > 0 && output is not null)
                    output.WriteFrames(samples.AsSpan(0, generated));
            }

            if (!caller_test.RxHasValues) {
                Console.Error.WriteLine($"ERROR V.34 decode receive data expected=BEFUELLT actual=LEER input_file={decode_test_file}");
                failed = true;
            }

            if (failed) ReportEndpointState(caller_test, bitsPerTest, false);
            return failed ? 2 : 0;
        } finally {
            if (initialized) v34_release(v34_caller);
            decodeHdlc?.Dispose();
            decodeHdlc = null;
            input?.Dispose();
            output?.Dispose();
        }
    }

    private static int RunBidirectional(int channelCodec, int lineModelNo, int noiseLevel,
        int signalLevel, float echoLevel, int bitsPerTest, bool logAudio, bool duplex) {
        bool failed = false;
        bool v8CallerInitialized = false;
        bool v8AnswererInitialized = false;
        bool v34CallerInitialized = false;
        bool v34AnswererInitialized = false;
        both_ways_line_model_state_t? model = null;
        V29V34Pcm16WaveWriter? output = logAudio ? new V29V34Pcm16WaveWriter(OUT_FILE_NAME, 8000, 2) : null;

        try {
            var callerParameters = CreateV8Parameters(true, duplex);
            var answererParameters = CreateV8Parameters(false, duplex);

            v34_state_t? initializedCaller = v34_init(null, test_baud_rate, test_bps, true, duplex,
                v34_get_bit, caller_test, v34_put_bit, caller_test);
            if (initializedCaller is null) {
                Console.Error.WriteLine("ERROR V.34 caller init expected=SUCCESS actual=NULL");
                failed = true;
                return 2;
            }
            v34_caller = initializedCaller;
            v34CallerInitialized = true;
            v34_set_get_aux_bit(v34_caller, v34_get_aux_bit, caller_test);
            v34_set_put_aux_bit(v34_caller, v34_put_aux_bit, caller_test);
            v34_tx_power(v34_caller, signalLevel);
            v8_caller = v8_init(null, true, callerParameters, v8_handler, v34_caller);
            v8CallerInitialized = true;

            v34_state_t? initializedAnswerer = v34_init(null, test_baud_rate, test_bps, false, duplex,
                v34_get_bit, answerer_test, v34_put_bit, answerer_test);
            if (initializedAnswerer is null) {
                Console.Error.WriteLine("ERROR V.34 answerer init expected=SUCCESS actual=NULL");
                failed = true;
                return 2;
            }
            v34_answerer = initializedAnswerer;
            v34AnswererInitialized = true;
            v34_set_get_aux_bit(v34_answerer, v34_get_aux_bit, answerer_test);
            v34_set_put_aux_bit(v34_answerer, v34_put_aux_bit, answerer_test);
            v34_tx_power(v34_answerer, signalLevel);
            v8_answerer = v8_init(null, false, answererParameters, v8_handler, v34_answerer);
            v8AnswererInitialized = true;

            model = both_ways_line_model_init(
                lineModelNo, noiseLevel, echoLevel, echoLevel,
                lineModelNo, noiseLevel, echoLevel, echoLevel,
                channelCodec, 0);
            if (model is null) {
                Console.Error.WriteLine("ERROR V.34 line model init expected=SUCCESS actual=NULL");
                return 2;
            }

            var callerAmp = new short[SAMPLES_PER_CHUNK];
            var answererAmp = new short[SAMPLES_PER_CHUNK];
            var callerModel = new short[SAMPLES_PER_CHUNK];
            var answererModel = new short[SAMPLES_PER_CHUNK];
            var stereo = new short[2 * SAMPLES_PER_CHUNK];
            int simulatedSamples = 0;
            int postEodSamples = 0;

            for (; ; )
            {
                int callerSamples = v8_tx(v8_caller, callerAmp, SAMPLES_PER_CHUNK);
                if (callerSamples < SAMPLES_PER_CHUNK)
                    callerSamples += v34_tx(v34_caller, callerAmp.AsSpan(callerSamples, SAMPLES_PER_CHUNK - callerSamples), SAMPLES_PER_CHUNK - callerSamples);

                int answererSamples = v8_tx(v8_answerer, answererAmp, SAMPLES_PER_CHUNK);
                if (answererSamples < SAMPLES_PER_CHUNK)
                    answererSamples += v34_tx(v34_answerer, answererAmp.AsSpan(answererSamples, SAMPLES_PER_CHUNK - answererSamples), SAMPLES_PER_CHUNK - answererSamples);

                int samples = Math.Max(callerSamples, answererSamples);
                if (samples <= 0) {
                    if (!caller_test.TxEod || !answerer_test.TxEod) {
                        Console.Error.WriteLine($"ERROR V.34 transmit stopped before end-of-data expected_caller_eod=1 actual={Bool(caller_test.TxEod)} expected_answerer_eod=1 actual={Bool(answerer_test.TxEod)}");
                        failed = true;
                    }
                    break;
                }

                if (callerSamples < samples) Array.Clear(callerAmp, callerSamples, samples - callerSamples);
                if (answererSamples < samples) Array.Clear(answererAmp, answererSamples, samples - answererSamples);
                both_ways_line_model(model, callerModel, callerAmp, answererModel, answererAmp, samples);

                int residual = v8_rx(v8_answerer, callerModel, samples);
                if (residual > 0) {
                    int processed = v34_rx(v34_answerer, callerModel.AsSpan(samples - residual, residual), residual);
                    if (processed != residual) {
                        Console.Error.WriteLine($"ERROR V.34 answerer receive length expected={residual} actual={processed} input_buffer={BufferState(callerModel, samples)}");
                        failed = true;
                        break;
                    }
                }

                residual = v8_rx(v8_caller, answererModel, samples);
                if (residual > 0) {
                    int processed = v34_rx(v34_caller, answererModel.AsSpan(samples - residual, residual), residual);
                    if (processed != residual) {
                        Console.Error.WriteLine($"ERROR V.34 caller receive length expected={residual} actual={processed} input_buffer={BufferState(answererModel, samples)}");
                        failed = true;
                        break;
                    }
                }

                if (output is not null) {
                    for (int i = 0; i < samples; i++) {
                        stereo[2 * i] = callerModel[i];
                        stereo[2 * i + 1] = answererModel[i];
                    }
                    output.WriteFrames(stereo.AsSpan(0, 2 * samples));
                }

                simulatedSamples += samples;
                if (caller_test.TxEod && answerer_test.TxEod) {
                    postEodSamples += samples;
                    if (postEodSamples >= V34_POST_EOD_TAIL_SAMPLES) break;
                }
                if (simulatedSamples >= V34_MAX_SIMULATION_SAMPLES) {
                    Console.Error.WriteLine($"ERROR V.34 simulation timeout expected_max_samples={V34_MAX_SIMULATION_SAMPLES} actual_samples={simulatedSamples} caller_tx_count={caller_test.TxCount} answerer_tx_count={answerer_test.TxCount} caller_rx_count={caller_test.RxCount} answerer_rx_count={answerer_test.RxCount}");
                    failed = true;
                    break;
                }
            }

            if (caller_test.TxCount != bitsPerTest
                || answerer_test.TxCount != bitsPerTest
                || !caller_test.RxHasValues || !answerer_test.RxHasValues
                || caller_test.BadBits > V34_MAX_SHUTDOWN_BAD_BITS
                || answerer_test.BadBits > V34_MAX_SHUTDOWN_BAD_BITS
                || caller_test.MissingExpectedBits != 0
                || answerer_test.MissingExpectedBits != 0
                || caller_test.TrainingFailed || answerer_test.TrainingFailed
                || v8_negotiation_failed) {
                failed = true;
            }

            return failed ? 2 : 0;
        } finally {
            if (failed) {
                ReportEndpointState(caller_test, bitsPerTest, true);
                ReportEndpointState(answerer_test, bitsPerTest, true);
                if (v8_negotiation_failed)
                    Console.Error.WriteLine("ERROR V.34 V.8 negotiation expected=SUCCESS actual=FAILED");
            }
            if (model is not null) both_ways_line_model_free(model);
            output?.Dispose();
            if (v8CallerInitialized) v8_release(v8_caller);
            if (v8AnswererInitialized) v8_release(v8_answerer);
            if (v34CallerInitialized) v34_release(v34_caller);
            if (v34AnswererInitialized) v34_release(v34_answerer);
        }
    }

    private static V8Parameters CreateV8Parameters(bool callerSide, bool duplex) {
        var parameters = new V8Parameters {
            ModemConnectTone = callerSide ? ModemConnectTone.None : ModemConnectTone.AnsamWithPhaseReversals
        };
        parameters.JmCm.CallFunction = duplex
            ? V8CallFunction.VSeriesModem
            : callerSide ? V8CallFunction.T30TransmitFax : V8CallFunction.T30ReceiveFax;
        parameters.JmCm.Modulations = duplex
            ? V8Modulation.V32 | V8Modulation.V34
            : V8Modulation.V21 | V8Modulation.V27Ter | V8Modulation.V29 | V8Modulation.V17 | V8Modulation.V34HalfDuplex;
        parameters.JmCm.Protocols = duplex ? V8Protocol.LapmV42 : V8Protocol.None;
        parameters.JmCm.PcmModemAvailability = V8PcmModemAvailability.None;
        parameters.JmCm.PstnAccess = V8PstnAccess.None;
        parameters.JmCm.Nsf = -1;
        parameters.JmCm.T66 = -1;
        return parameters;
    }

    private static void v8_handler(object? userData, V8Parameters result) {
        var state = (v34_state_t)(userData ?? throw new ArgumentNullException(nameof(userData)));
        string side = ReferenceEquals(state, v34_caller) ? "caller" : "answerer";

        switch (result.Status) {
            case V8Status.InProgress:
            case V8Status.V8Offered:
                break;
            case V8Status.V8Call:
                switch (result.JmCm.CallFunction) {
                    case V8CallFunction.T30TransmitFax:
                        v34_restart(state, test_baud_rate, test_bps, false);
                        v34_half_duplex_change_mode(state, V34_HALF_DUPLEX_SOURCE);
                        break;
                    case V8CallFunction.T30ReceiveFax:
                        v34_restart(state, test_baud_rate, test_bps, false);
                        v34_half_duplex_change_mode(state, V34_HALF_DUPLEX_RECIPIENT);
                        break;
                    case V8CallFunction.VSeriesModem:
                        v34_restart(state, test_baud_rate, test_bps, true);
                        break;
                    default:
                        Console.Error.WriteLine($"ERROR V.34 V.8 endpoint={side} call function unexpected expected=V_SERIES_OR_T30 actual={(int)result.JmCm.CallFunction}");
                        v8_negotiation_failed = true;
                        break;
                }
                break;
            case V8Status.NonV8Call:
                break;
            case V8Status.Failed:
                Console.Error.WriteLine($"ERROR V.34 V.8 endpoint={side} negotiation expected=SUCCESS actual=FAILED");
                v8_negotiation_failed = true;
                break;
            default:
                Console.Error.WriteLine($"ERROR V.34 V.8 endpoint={side} status unexpected expected=KNOWN_STATUS actual={(int)result.Status}");
                v8_negotiation_failed = true;
                break;
        }

        if (result.Status == V8Status.V8Offered)
            result.JmCm.Modulations &= V8Modulation.V21 | V8Modulation.V27Ter | V8Modulation.V29 | V8Modulation.V17 | V8Modulation.V34HalfDuplex;
    }

    private static void hdlc_handler(object? userData, ReadOnlyMemory<byte>? packet, int length, bool ok) {
        _ = userData;
        if (!ok || length <= 0 || packet is null)
            Console.Error.WriteLine($"ERROR V.34 HDLC frame invalid expected_ok=1 actual_ok={Bool(ok)} expected_len_gt=0 actual_len={length} buffer={(packet is not null && length > 0 ? "BEFUELLT" : "LEER")}");
    }

    private static void v34_decode_putbit(object? userData, int bit) {
        var endpoint = (Endpoint)(userData ?? throw new ArgumentNullException(nameof(userData)));
        if (bit < 0) {
            UpdateStatus(endpoint, bit, "decode");
            return;
        }
        endpoint.RxHasValues = true;
        endpoint.RxCount++;
        decodeHdlc ??= hdlc_rx_init(null, false, true, 2, hdlc_handler, null);
        hdlc_rx_put_bit(decodeHdlc, bit);
    }

    private static int v34_get_aux_bit(object? userData) { _ = userData; return 1; }

    private static void v34_put_aux_bit(object? userData, int bit) {
        var endpoint = (Endpoint)(userData ?? throw new ArgumentNullException(nameof(userData)));
        if (bit is 0 or 1) return;
        endpoint.InvalidAuxBits++;
        if (endpoint.InvalidAuxBits <= 3)
            Console.Error.WriteLine($"ERROR V.34 endpoint={endpoint.Name} auxiliary bit invalid expected=0_OR_1 actual={bit}");
    }

    private static int v34_get_bit(object? userData) {
        var endpoint = (Endpoint)(userData ?? throw new ArgumentNullException(nameof(userData)));
        if (endpoint.TxCount >= endpoint.TxCapacity) {
            endpoint.TxEod = true;
            return (int)SignalStatus.EndOfData;
        }
        uint pattern = unchecked((uint)(endpoint.TxCount + 1) * 1103515245u + 12345u);
        int bit = (int)((pattern >> 30) & 1u);
        endpoint.TxValues[endpoint.TxCount++] = (byte)bit;
        return bit;
    }

    private static void v34_put_bit(object? userData, int bit) {
        var endpoint = (Endpoint)(userData ?? throw new ArgumentNullException(nameof(userData)));
        if (bit < 0) { UpdateStatus(endpoint, bit, "receive"); return; }
        endpoint.RxHasValues = true;
        if (endpoint.Peer is null || endpoint.RxCount >= endpoint.Peer.TxCount) {
            endpoint.MissingExpectedBits++;
            endpoint.RxCount++;
            return;
        }
        int expected = endpoint.Peer.TxValues[endpoint.RxCount];
        if (bit != expected) {
            endpoint.BadBits++;
            if (endpoint.BadBits <= 3)
                Console.Error.WriteLine($"ERROR V.34 endpoint={endpoint.Name} bit mismatch index={endpoint.RxCount} expected={expected} actual={bit} source_buffer={BufferState(endpoint.Peer.TxValues, endpoint.Peer.TxCount)}");
        }
        endpoint.RxCount++;
    }

    private static void UpdateStatus(Endpoint endpoint, int status, string context) {
        if (status == (int)SignalStatus.TrainingSucceeded)
            endpoint.TrainingSucceeded = true;
        else if (status == (int)SignalStatus.TrainingFailed || status == (int)SignalStatus.Abort || status == (int)SignalStatus.Break) {
            endpoint.TrainingFailed = true;
            Console.Error.WriteLine($"ERROR V.34 {context} endpoint={endpoint.Name} status expected=TRAINING_SUCCEEDED actual={signal_status_to_str(status)}({status})");
        }
    }

    private static int v34_mapping_frame_tests(int baudRate, int bitRate, bool duplex) {
        bool failed = false;
        bool anyValue = false;
        v34_state_t? mappingCaller = null;
        v34_state_t? mappingAnswerer = null;
        try {
            mappingCaller = v34_init(null, baudRate, bitRate, false, duplex, v34_get_bit, caller_test, v34_put_bit, caller_test);
            if (mappingCaller is null) {
                Console.Error.WriteLine("ERROR V.34 mapping caller init expected=SUCCESS actual=NULL");
                return 2;
            }
            v34_set_get_aux_bit(mappingCaller, v34_get_aux_bit, caller_test);
            v34_set_put_aux_bit(mappingCaller, v34_put_aux_bit, caller_test);

            mappingAnswerer = v34_init(null, baudRate, bitRate, true, duplex, v34_get_bit, answerer_test, v34_put_bit, answerer_test);
            if (mappingAnswerer is null) {
                Console.Error.WriteLine("ERROR V.34 mapping answerer init expected=SUCCESS actual=NULL");
                return 2;
            }
            v34_set_get_aux_bit(mappingAnswerer, v34_get_aux_bit, answerer_test);
            v34_set_put_aux_bit(mappingAnswerer, v34_put_aux_bit, answerer_test);

            short[] coordinates = new short[16];
            for (int frame = 0; frame < 1000; frame++) {
                Array.Clear(coordinates, 0, coordinates.Length);
                int length = v34_get_mapping_frame(mappingAnswerer.tx, coordinates);
                if (length != 16) {
                    Console.Error.WriteLine($"ERROR V.34 mapping frame length expected=16 actual={length} frame={frame} buffer={BufferState(coordinates, length)}");
                    failed = true;
                    break;
                }
                for (int i = 0; i < 16; i++) if (coordinates[i] != 0) anyValue = true;
                v34_put_mapping_frame(mappingCaller.rx, coordinates);
            }

            if (!anyValue) {
                Console.Error.WriteLine("ERROR V.34 mapping frame data expected=BEFUELLT actual=LEER frames=1000");
                failed = true;
            }
            return failed ? 2 : 0;
        } finally {
            if (mappingCaller is not null) v34_release(mappingCaller);
            if (mappingAnswerer is not null) v34_release(mappingAnswerer);
        }
    }

    private static void ReportEndpointState(Endpoint endpoint, int expectedBits, bool validateTransmitCount) {
        Console.Error.WriteLine($"STATE V.34 endpoint={endpoint.Name} tx_buffer={BufferState(endpoint.TxValues, endpoint.TxCount)} tx_count={endpoint.TxCount} tx_capacity={endpoint.TxCapacity} rx_buffer={(endpoint.RxHasValues ? "BEFUELLT" : "LEER")} rx_count={endpoint.RxCount} bad_bits={endpoint.BadBits} missing_expected_bits={endpoint.MissingExpectedBits} invalid_aux_bits={endpoint.InvalidAuxBits} tx_eod={Bool(endpoint.TxEod)} training_succeeded={Bool(endpoint.TrainingSucceeded)} training_failed={Bool(endpoint.TrainingFailed)}");
        if (validateTransmitCount && endpoint.TxCount != expectedBits)
            Console.Error.WriteLine($"ERROR V.34 endpoint={endpoint.Name} transmitted bit count mismatch expected={expectedBits} actual={endpoint.TxCount} tx_buffer={BufferState(endpoint.TxValues, endpoint.TxCount)}");
        if (!endpoint.RxHasValues)
            Console.Error.WriteLine($"ERROR V.34 endpoint={endpoint.Name} receive buffer empty expected=BEFUELLT actual=LEER");
        if (endpoint.BadBits > V34_MAX_SHUTDOWN_BAD_BITS)
            Console.Error.WriteLine($"ERROR V.34 endpoint={endpoint.Name} bit errors exceeded limit expected_max={V34_MAX_SHUTDOWN_BAD_BITS} actual={endpoint.BadBits}");
        if (endpoint.MissingExpectedBits != 0)
            Console.Error.WriteLine($"ERROR V.34 endpoint={endpoint.Name} received bits without expected source value expected=0 actual={endpoint.MissingExpectedBits}");
        if (endpoint.TrainingFailed)
            Console.Error.WriteLine($"ERROR V.34 endpoint={endpoint.Name} training state expected=SUCCEEDED actual=FAILED");
    }

    private static int ParseArguments(string[] testArguments, ref int codec, ref int model,
        ref int noise, ref int signal, ref float echo, ref int bits, ref bool log,
        ref bool callingParty, ref bool test4d, ref bool duplex) {
        for (int i = 0; i < testArguments.Length; i++) {
            switch (testArguments[i]) {
                case "-4": test4d = true; break;
                case "-a":
                    test_baud_rate = Int(Next(testArguments, ref i));
                    if (test_baud_rate is not (2400 or 2743 or 2800 or 3000 or 3200 or 3429)) {
                        Console.Error.WriteLine($"ERROR V.34 baud rate invalid expected=2400|2743|2800|3000|3200|3429 actual={test_baud_rate}");
                        return 2;
                    }
                    break;
                case "-b":
                    test_bps = Int(Next(testArguments, ref i));
                    if (!ValidBitRates.Contains(test_bps)) {
                        Console.Error.WriteLine($"ERROR V.34 bit rate invalid expected=SUPPORTED_RATE actual={test_bps}");
                        return 2;
                    }
                    break;
                case "-B": bits = Int(Next(testArguments, ref i)); break;
                case "-c": codec = Int(Next(testArguments, ref i)); break;
                case "-d": decode_test_file = Next(testArguments, ref i); callingParty = true; break;
                case "-D": decode_test_file = Next(testArguments, ref i); callingParty = false; break;
                case "-e": echo = Int(Next(testArguments, ref i)); break;
                case "-g": Console.Error.WriteLine("ERROR V.34 graphical monitoring expected=AVAILABLE actual=UNAVAILABLE"); return 2;
                case "-h": duplex = false; break;
                case "-l": log = true; break;
                case "-m": model = Int(Next(testArguments, ref i)); break;
                case "-n": noise = Int(Next(testArguments, ref i)); break;
                case "-s": signal = Int(Next(testArguments, ref i)); break;
                default: return 2;
            }
        }
        return 0;
    }

    private static string BufferState<T>(T[]? buffer, int count) => buffer is not null && count > 0 ? "BEFUELLT" : "LEER";
    private static string BufferState<T>(Span<T> buffer, int count) => buffer.Length > 0 && count > 0 ? "BEFUELLT" : "LEER";
    private static int Bool(bool value) => value ? 1 : 0;
    private static string Next(string[] testArguments, ref int i) { if (++i >= testArguments.Length) Exit(2); return testArguments[i]; }
    private static int Int(string value) { if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)) Exit(2); return result; }
    internal static void Exit(int code) => throw new V34TestsExitException(code);

    public sealed class V34TestsExitException : Exception {
        public V34TestsExitException(int exitCode) : base($"v34_tests exited with code {exitCode}.") => ExitCode = exitCode;
        public int ExitCode { get; }
    }
}
