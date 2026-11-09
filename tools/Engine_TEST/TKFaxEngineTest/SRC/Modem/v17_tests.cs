/*
 * Fresh managed C# port of v17_tests.c from the supplied udptl-V17 archive.
 * Runs the one-way V.17 TX/RX BER path or decodes an 8 kHz PCM WAV file.
 */
#nullable enable

using System.Buffers.Binary;
using TKFaxEngine.Audio;
using static TKFaxEngine.Audio.g711;
using static TKFaxEngine.Audio.g726;
using TKFaxEngine.Modem;
using TKFaxEngine.Modem.V17;

namespace TKFaxEngineTest.Modem.Test;

public static class V17Tests {
    private const int BlockLength = 160;
    private const string OutputFileName = "v17.wav";

    private static string? _decodeTestFile;
    private static int _symbolNumber;
    private static int _receiveBits;
    private static int _testBitsPerSecond;
    private static BertState? _bert;
    private static BertResults _latestResults = new();
    private static double _smoothPower;
    private static int _equalizerUpdateInterval = 100;

    public static int Main(string[] testArguments) {
        try {
            return Run(testArguments);
        } catch (V17TestExitException exception) {
            return exception.ExitCode;
        } catch (Exception exception) {
            Console.Error.WriteLine(exception);
            return 2;
        }
    }

    public static int Run(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        int channelCodec = 0;
        int robbedBitPattern = 0;
        _testBitsPerSecond = 14400;
        bool trainingEchoProtection = false;
        int lineModelNumber = 0;
        _decodeTestFile = null;
        int noiseLevel = -70;
        int signalLevel = -13;
        int bitsPerTest = 50_000;
        bool logAudio = false;

        for (int argumentIndex = 0; argumentIndex < testArguments.Length; argumentIndex++) {
            string argument = testArguments[argumentIndex];
            if (argument == "--")
                break;
            if (argument.Length < 2 || argument[0] != '-')
                break;

            for (int optionIndex = 1; optionIndex < argument.Length; optionIndex++) {
                char option = argument[optionIndex];
                if (option is 'g' or 'l' or 't') {
                    switch (option) {
                        case 'g':
                            Console.Error.WriteLine("Graphical monitoring is not part of the managed test port.");
                            throw new V17TestExitException(2);
                        case 'l':
                            logAudio = true;
                            break;
                        case 't':
                            trainingEchoProtection = true;
                            break;
                    }
                    continue;
                }

                if (option is not ('b' or 'B' or 'c' or 'd' or 'm' or 'n' or 'r' or 's'))
                    throw new V17TestExitException(2);

                string optionValue;
                if (optionIndex + 1 < argument.Length) {
                    optionValue = argument[(optionIndex + 1)..];
                } else if (++argumentIndex < testArguments.Length) {
                    optionValue = testArguments[argumentIndex];
                } else {
                    throw new V17TestExitException(2);
                }

                switch (option) {
                    case 'b':
                        _testBitsPerSecond = ParseInt(optionValue, "bit rate");
                        if (_testBitsPerSecond is not (4800 or 7200 or 9600 or 12000 or 14400)) {
                            Console.Error.WriteLine("Invalid bit rate specified");
                            throw new V17TestExitException(2);
                        }
                        break;
                    case 'B':
                        bitsPerTest = ParseInt(optionValue, "BERT bit count");
                        break;
                    case 'c':
                        channelCodec = ParseInt(optionValue, "channel codec");
                        break;
                    case 'd':
                        _decodeTestFile = optionValue;
                        break;
                    case 'm':
                        lineModelNumber = ParseInt(optionValue, "line model");
                        break;
                    case 'n':
                        noiseLevel = ParseInt(optionValue, "noise level");
                        break;
                    case 'r':
                        robbedBitPattern = ParseInt(optionValue, "robbed-bit pattern");
                        break;
                    case 's':
                        signalLevel = ParseInt(optionValue, "signal level");
                        break;
                }
                optionIndex = argument.Length;
            }
        }

        using V17WaveWriter? output = logAudio ? new V17WaveWriter(OutputFileName, 1, 8000) : null;
        using V17WaveReader? input = _decodeTestFile is not null
            ? new V17WaveReader(_decodeTestFile, 8000)
            : null;

        V17TxState? transmitter = null;
        V17LineModel? lineModel = null;

        if (_decodeTestFile is null) {
            _bert = Bert.Initialize(null, bitsPerTest, BertPattern.ItuO15211, _testBitsPerSecond, 20);
            _bert.SetReport(10_000, BertReporter, null);

            transmitter = V17Tx.Initialize(null, _testBitsPerSecond, trainingEchoProtection, GetBit, null)
                ?? throw new InvalidOperationException("Unable to initialize V.17 transmitter.");
            V17Tx.SetPower(transmitter, signalLevel);
            V17Tx.SetModemStatusHandler(transmitter, TransmitStatus, transmitter);
            transmitter.Logging.FlowSink = message => Console.Error.WriteLine($"V.17-tx: {message}");

            lineModel = new V17LineModel(lineModelNumber, noiseLevel, channelCodec, robbedBitPattern);
        }

        V17RxState receiver = V17Rx.Initialize(null, _testBitsPerSecond, PutBit, null)
            ?? throw new InvalidOperationException("Unable to initialize V.17 receiver.");
        V17Rx.SetModemStatusHandler(receiver, ReceiveStatus, receiver);
        V17Rx.SetQamReportHandler(receiver, QamReport, receiver);
        receiver.Logging.FlowSink = message => Console.Error.WriteLine($"V.17-rx: {message}");
        receiver.Logging.WarningSink = message => Console.Error.WriteLine($"V.17-rx warning: {message}");

        _latestResults = new BertResults();
        _symbolNumber = 0;
        _receiveBits = 0;
        _smoothPower = 0.0;
        _equalizerUpdateInterval = 100;

        var generated = new short[BlockLength];
        var received = new short[BlockLength];

        for (int blockNumber = 0; blockNumber < 100_000_000; blockNumber++) {
            int samples;
            if (input is not null) {
                samples = input.Read(received);
                if (samples == 0)
                    break;
            } else {
                if (transmitter is null || lineModel is null || _bert is null)
                    throw new InvalidOperationException("V.17 generated-path state is incomplete.");

                samples = V17Tx.Transmit(transmitter, generated);
                if (samples == 0) {
                    Console.WriteLine("Restarting on zero output");
                    Array.Clear(received);
                    V17Rx.Receive(receiver, received);

                    BertResults finalResults = _bert.GetResults();
                    PrintResult("Final result", signalLevel, noiseLevel, finalResults);
                    PrintResult("Last report ", signalLevel, noiseLevel, _latestResults);

                    if (finalResults.TotalBits < bitsPerTest ||
                        _latestResults.TotalBits < bitsPerTest - 100 ||
                        _latestResults.BadBits != 0) {
                        break;
                    }

                    _latestResults = new BertResults();
                    signalLevel--;
                    // Match spanDSP v17_tests.c: compensate the saved RX AGC by +1 dB
                    // before restarting both modem directions with short training.
                    receiver.AgcScalingSave *= 1.122f;
                    V17Tx.Restart(transmitter, _testBitsPerSecond, trainingEchoProtection, true);
                    V17Tx.SetPower(transmitter, signalLevel);
                    V17Rx.Restart(receiver, _testBitsPerSecond, 1);
                    _bert = Bert.Initialize(_bert, bitsPerTest, BertPattern.ItuO15211, _testBitsPerSecond, 20);
                    _bert.SetReport(10_000, BertReporter, null);
                    lineModel.Dispose();
                    lineModel = new V17LineModel(lineModelNumber, noiseLevel, channelCodec, 0);
                }

                output?.Write(generated.AsSpan(0, samples));
                lineModel.Process(generated.AsSpan(0, samples), received.AsSpan(0, samples));
            }

            V17Rx.Receive(receiver, received.AsSpan(0, samples));
        }

        if (_decodeTestFile is null) {
            if (_bert is null)
                throw new InvalidOperationException("BERT state is unavailable.");
            BertResults finalResults = _bert.GetResults();
            Console.Error.WriteLine("At completion:");
            PrintResult("Final result", signalLevel, noiseLevel, finalResults);
            PrintResult("Last report ", signalLevel, noiseLevel, _latestResults);

            if (signalLevel > -43) {
                Console.WriteLine("Tests failed.");
                throw new V17TestExitException(2);
            }
            Console.WriteLine("Tests passed.");
        }

        lineModel?.Dispose();
        V17Rx.Free(receiver);
        V17Tx.Free(transmitter);
        _bert?.Dispose();
        _bert = null;
        return 0;
    }

    private static void BertReporter(object? userData, int reason, BertResults results) {
        if (reason == (int)BertReportEvent.Regular) {
            Console.Error.WriteLine(
                $"BERT report regular - {results.TotalBits} bits, {results.BadBits} bad bits, {results.Resyncs} resyncs");
            _latestResults = results.Clone();
        } else {
            Console.Error.WriteLine($"BERT report {Bert.EventToString(reason)}");
        }
    }

    private static void ReceiveStatus(object? userData, int status) {
        Console.WriteLine($"V.17 rx status is {SignalStatusToString(status)} ({status})");
        if (status != (int)V17RxSignalStatus.TrainingSucceeded || userData is not V17RxState receiver)
            return;

        Console.WriteLine("Training succeeded");
        ReadOnlySpan<V17RxComplex> coefficients = receiver.GetEqualizerState().Span;
        if (coefficients.IsEmpty)
            return;
        Console.WriteLine("Equalizer:");
        for (int i = 0; i < coefficients.Length; i++) {
            V17RxComplex coefficient = coefficients[i];
            double power = coefficient.Real * coefficient.Real + coefficient.Imaginary * coefficient.Imaginary;
            Console.WriteLine($"{i,3} ({coefficient.Real,15:F5}, {coefficient.Imaginary,15:F5}) -> {power,15:F5}");
        }
    }

    private static void PutBit(object? userData, int bit) {
        if (bit < 0) {
            ReceiveStatus(userData, bit);
            return;
        }

        if (_decodeTestFile is not null)
            Console.WriteLine($"Rx bit {_receiveBits++} - {bit}");
        else
            _bert?.PutBit(bit);
    }

    private static void TransmitStatus(object? userData, int status) =>
        Console.WriteLine($"V.17 tx status is {SignalStatusToString(status)} ({status})");

    private static int GetBit(object? userData) => _bert?.GetBit() ?? -1;

    private static void QamReport(
        object? userData,
        V17RxComplex constellation,
        V17RxComplex target,
        int symbol) {
        if (userData is not V17RxState receiver)
            return;

        double realError = constellation.Real - target.Real;
        double imaginaryError = constellation.Imaginary - target.Imaginary;
        double power = realError * realError + imaginaryError * imaginaryError;
        _smoothPower = 0.95 * _smoothPower + 0.05 * power;

        Console.WriteLine(
            $"{_symbolNumber,8} [{constellation.Real,8:F4}, {constellation.Imaginary,8:F4}] " +
            $"[{target.Real,8:F4}, {target.Imaginary,8:F4}] {symbol,2:X} " +
            $"{power,8:F4} {_smoothPower,8:F4} {receiver.CarrierFrequency,9:F4} " +
            $"{receiver.SignalPower,7:F3} {receiver.SymbolTimingCorrection,7:F4}");
        _symbolNumber++;

        if (--_equalizerUpdateInterval > 0)
            return;
        ReadOnlySpan<V17RxComplex> coefficients = receiver.GetEqualizerState().Span;
        Console.WriteLine("Equalizer A:");
        for (int i = 0; i < coefficients.Length; i++) {
            V17RxComplex coefficient = coefficients[i];
            double coefficientPower = coefficient.Real * coefficient.Real + coefficient.Imaginary * coefficient.Imaginary;
            Console.WriteLine($"{i,3} ({coefficient.Real,15:F5}, {coefficient.Imaginary,15:F5}) -> {coefficientPower,15:F5}");
        }
        _equalizerUpdateInterval = 100;
    }

    private static string SignalStatusToString(int status) => status switch {
        -1 => "carrier down",
        -2 => "carrier up",
        -3 => "training in progress",
        -4 => "training succeeded",
        -5 => "training failed",
        -6 => "framing OK",
        -7 => "end of data",
        -8 => "abort",
        -9 => "break",
        -10 => "shutdown complete",
        _ => "unknown"
    };

    private static void PrintResult(string label, int signalLevel, int noiseLevel, BertResults results) =>
        Console.Error.WriteLine(
            $"{label} {signalLevel}dBm0/{noiseLevel}dBm0, {results.TotalBits} bits, " +
            $"{results.BadBits} bad bits, {results.Resyncs} resyncs");

    private static int ParseInt(string? value, string name) {
        if (!int.TryParse(value, out int parsed))
            throw new ArgumentException($"Invalid {name}: '{value}'.");
        return parsed;
    }
}

internal sealed class V17LineModel : IDisposable {
    private const int FilterLength = 129;
    private const int BulkDelay = 8;

    private readonly float[] _coefficients;
    private readonly float[] _nearBuffer = new float[FilterLength];
    private readonly float[] _farBuffer = new float[FilterLength];
    private readonly short[] _bulkDelayBuffer = new short[BulkDelay];
    private readonly Awgn _nearNoise;
    private readonly Awgn _farNoise;
    private readonly V17CodecMunge _codecMunge;
    private int _nearBufferPointer;
    private int _farBufferPointer;
    private int _bulkDelayPointer;

    public V17LineModel(int model, int noiseLevelDbm0, int codec, int robbedBitPattern) {
        _coefficients = V17LineModels.Get(model).ToArray();
        _nearNoise = new Awgn(1_234_567, noiseLevelDbm0 - 3.02);
        _farNoise = new Awgn(1_234_567, noiseLevelDbm0 - 3.02);
        _codecMunge = new V17CodecMunge(codec, robbedBitPattern);
    }

    public void Process(ReadOnlySpan<short> input, Span<short> output) {
        if (output.Length < input.Length)
            throw new ArgumentException("Output buffer is too small.", nameof(output));

        for (int i = 0; i < input.Length; i++) {
            float sample = ApplyFilter(input[i], _nearBuffer, ref _nearBufferPointer);
            sample += _nearNoise.NextSample();

            short digitalSample = FloatToInt16(sample);
            digitalSample = _codecMunge.Process(digitalSample);

            short delayedSample = _bulkDelayBuffer[_bulkDelayPointer];
            _bulkDelayBuffer[_bulkDelayPointer] = digitalSample;
            if (++_bulkDelayPointer >= BulkDelay)
                _bulkDelayPointer = 0;

            sample = ApplyFilter(delayedSample, _farBuffer, ref _farBufferPointer);
            sample += _farNoise.NextSample();
            output[i] = FloatToInt16(sample);
        }
    }

    private float ApplyFilter(float input, float[] history, ref int pointer) {
        int p = pointer;
        history[p] = input;
        if (++p == FilterLength)
            p = 0;
        pointer = p;

        float sum = 0.0f;
        for (int i = 0; i < FilterLength; i++) {
            sum += _coefficients[i] * history[p];
            if (++p == FilterLength)
                p = 0;
        }
        return sum;
    }

    private static short FloatToInt16(float value) {
        if (value >= short.MaxValue)
            return short.MaxValue;
        if (value <= short.MinValue)
            return short.MinValue;
        return (short)value;
    }

    public void Dispose() => _codecMunge.Dispose();
}

internal sealed class V17CodecMunge : IDisposable {
    private const int CodecNone = 0;
    private const int CodecALaw = 1;
    private const int CodecULaw = 2;
    private const int CodecG72640K = 3;
    private const int CodecG72632K = 4;
    private const int CodecG72624K = 5;
    private const int CodecG72616K = 6;

    private readonly int _codec;
    private readonly int _robbedBitPattern;
    private readonly g726_state_t? _encoder;
    private readonly g726_state_t? _decoder;
    private int _sequence;

    public V17CodecMunge(int codec, int robbedBitPattern) {
        _robbedBitPattern = robbedBitPattern;
        _sequence = 0;

        int rate = codec switch {
            CodecG72640K => 40_000,
            CodecG72632K => 32_000,
            CodecG72624K => 24_000,
            CodecG72616K => 16_000,
            _ => 0
        };

        if (rate != 0) {
            _encoder = g726_init(null, rate, G726_ENCODING_LINEAR, G726_PACKING_NONE)
                ?? throw new InvalidOperationException("Unable to initialize the SpanDSP-compatible G.726 encoder.");
            _decoder = g726_init(null, rate, G726_ENCODING_LINEAR, G726_PACKING_NONE)
                ?? throw new InvalidOperationException("Unable to initialize the SpanDSP-compatible G.726 decoder.");
            // Native codec_munge_init deliberately funnels all G.726 rates through
            // the same processing branch after initializing the selected rate.
            _codec = CodecG72632K;
        } else {
            if (codec is < CodecNone or > CodecULaw)
                throw new ArgumentOutOfRangeException(nameof(codec), codec, "Codec must be 0 through 6.");
            _codec = codec;
        }
    }

    public short Process(short sample) {
        switch (_codec) {
            case CodecNone:
                return sample;
            case CodecALaw:
                return alaw_to_linear(linear_to_alaw(sample));
            case CodecULaw: {
                    byte law = linear_to_ulaw(sample);
                    if ((_robbedBitPattern & (1 << _sequence)) != 0)
                        law &= 0xFE;
                    return ulaw_to_linear(law);
                }
            case CodecG72632K: {
                    if (_encoder is null || _decoder is null)
                        throw new InvalidOperationException("G.726 state is unavailable.");
                    short[] linear = { sample };
                    byte[] encoded = new byte[2];
                    int encodedBytes = g726_encode(_encoder, encoded, linear, 1);
                    int decodedSamples = g726_decode(_decoder, linear, encoded, encodedBytes);
                    if (decodedSamples != 1)
                        throw new InvalidDataException("The G.726 line-model codec did not return one sample.");
                    return linear[0];
                }
            default:
                throw new InvalidOperationException("Unknown codec-munging mode.");
        }
    }

    public void Dispose() {
        if (_encoder is not null)
            g726_free(_encoder);
        if (_decoder is not null)
            g726_free(_decoder);
    }
}

internal static class V17LineModels {
    private static readonly float[] Null = CreateNull();
    private static readonly float[] Proakis =
    [
        -0.00034f, -0.00049f, -0.00048f, -0.00034f, -0.00042f, -0.00037f, -0.00055f, -0.00060f,
        -0.00043f, -0.00068f, -0.00042f, -0.00029f, -0.00031f, -0.00048f, -0.00052f, -0.00064f,
        -0.00067f, -0.00043f, -0.00051f, -0.00036f, -0.00068f, -0.00022f, -0.00059f, 0.00013f,
        -0.00003f, 0.00014f, -0.00071f, -0.00031f, -0.00100f, -0.00062f, -0.00090f, -0.00091f,
        -0.00146f, -0.00164f, -0.00138f, -0.00154f, -0.00099f, -0.00118f, -0.00171f, -0.00339f,
        -0.00219f, -0.00385f, -0.00360f, -0.00311f, -0.00353f, -0.00381f, -0.00537f, -0.00707f,
        -0.00490f, -0.00947f, -0.00581f, -0.00518f, -0.00526f, -0.01086f, -0.01212f, -0.01972f,
        -0.02282f, -0.02072f, -0.02782f, -0.03151f, -0.07820f, -0.05061f, -0.24342f, 0.18191f,
        0.88907f, 0.18191f, -0.24342f, -0.05061f, -0.07820f, -0.03151f, -0.02782f, -0.02072f,
        -0.02282f, -0.01972f, -0.01212f, -0.01086f, -0.00526f, -0.00518f, -0.00581f, -0.00947f,
        -0.00490f, -0.00707f, -0.00537f, -0.00381f, -0.00353f, -0.00311f, -0.00360f, -0.00385f,
        -0.00219f, -0.00339f, -0.00171f, -0.00118f, -0.00099f, -0.00154f, -0.00138f, -0.00164f,
        -0.00146f, -0.00091f, -0.00090f, -0.00062f, -0.00100f, -0.00031f, -0.00071f, 0.00014f,
        -0.00003f, 0.00013f, -0.00059f, -0.00022f, -0.00068f, -0.00036f, -0.00051f, -0.00043f,
        -0.00067f, -0.00064f, -0.00052f, -0.00048f, -0.00031f, -0.00029f, -0.00042f, -0.00068f,
        -0.00043f, -0.00060f, -0.00055f, -0.00037f, -0.00042f, -0.00034f, -0.00048f, -0.00049f,
        -0.00034f
    ];

    private static readonly float[] Ad1 =
    [
        -0.00082f, -0.00016f, -0.00107f, -0.00064f, -0.00116f, -0.00108f, -0.00140f, -0.00137f,
        -0.00161f, -0.00169f, -0.00173f, -0.00197f, -0.00180f, -0.00194f, -0.00200f, -0.00167f,
        -0.00185f, -0.00137f, -0.00147f, -0.00090f, -0.00080f, -0.00036f, 0.00012f, 0.00008f,
        0.00125f, 0.00083f, 0.00182f, 0.00195f, 0.00242f, 0.00285f, 0.00310f, 0.00354f,
        0.00400f, 0.00409f, 0.00505f, 0.00399f, 0.00689f, 0.00359f, 0.00793f, 0.00351f,
        0.00853f, 0.00265f, 0.00862f, 0.00045f, 0.00758f, -0.00172f, 0.00253f, -0.00393f,
        -0.00463f, -0.00766f, -0.01512f, -0.01173f, -0.02873f, -0.01422f, -0.04776f, -0.01547f,
        -0.06633f, -0.01480f, -0.09054f, -0.00927f, -0.10944f, 0.00108f, -0.15140f, 0.05136f,
        0.94343f, 0.05136f, -0.15140f, 0.00108f, -0.10944f, -0.00927f, -0.09054f, -0.01480f,
        -0.06633f, -0.01547f, -0.04776f, -0.01422f, -0.02873f, -0.01173f, -0.01512f, -0.00766f,
        -0.00463f, -0.00393f, 0.00253f, -0.00172f, 0.00758f, 0.00045f, 0.00862f, 0.00265f,
        0.00853f, 0.00351f, 0.00793f, 0.00359f, 0.00689f, 0.00399f, 0.00505f, 0.00409f,
        0.00400f, 0.00354f, 0.00310f, 0.00285f, 0.00242f, 0.00195f, 0.00182f, 0.00083f,
        0.00125f, 0.00008f, 0.00012f, -0.00036f, -0.00080f, -0.00090f, -0.00147f, -0.00137f,
        -0.00185f, -0.00167f, -0.00200f, -0.00194f, -0.00180f, -0.00197f, -0.00173f, -0.00169f,
        -0.00161f, -0.00137f, -0.00140f, -0.00108f, -0.00116f, -0.00064f, -0.00107f, -0.00016f,
        -0.00082f
    ];

    private static readonly float[] Ad5 =
    [
        -0.00059f, -0.00045f, -0.00129f, -0.00138f, -0.00214f, -0.00223f, -0.00290f, -0.00294f,
        -0.00360f, -0.00341f, -0.00352f, -0.00360f, -0.00342f, -0.00345f, -0.00327f, -0.00289f,
        -0.00252f, -0.00230f, -0.00174f, -0.00137f, -0.00056f, 0.00025f, 0.00092f, 0.00171f,
        0.00260f, 0.00322f, 0.00401f, 0.00465f, 0.00535f, 0.00536f, 0.00663f, 0.00636f,
        0.00791f, 0.00711f, 0.00812f, 0.00653f, 0.00866f, 0.00529f, 0.00776f, 0.00339f,
        0.00449f, 0.00093f, 0.00321f, -0.00202f, 0.00085f, -0.00519f, -0.00226f, -0.00815f,
        -0.00905f, -0.01050f, -0.01828f, -0.01366f, -0.03072f, -0.02024f, -0.04510f, -0.02412f,
        -0.05966f, -0.02659f, -0.07618f, -0.03235f, -0.09336f, -0.01198f, -0.16680f, 0.11748f,
        0.93043f, 0.11748f, -0.16680f, -0.01198f, -0.09336f, -0.03235f, -0.07618f, -0.02659f,
        -0.05966f, -0.02412f, -0.04510f, -0.02024f, -0.03072f, -0.01366f, -0.01828f, -0.01050f,
        -0.00905f, -0.00815f, -0.00226f, -0.00519f, 0.00085f, -0.00202f, 0.00321f, 0.00093f,
        0.00449f, 0.00339f, 0.00776f, 0.00529f, 0.00866f, 0.00653f, 0.00812f, 0.00711f,
        0.00791f, 0.00636f, 0.00663f, 0.00536f, 0.00535f, 0.00465f, 0.00401f, 0.00322f,
        0.00260f, 0.00171f, 0.00092f, 0.00025f, -0.00056f, -0.00137f, -0.00174f, -0.00230f,
        -0.00252f, -0.00289f, -0.00327f, -0.00345f, -0.00342f, -0.00360f, -0.00352f, -0.00341f,
        -0.00360f, -0.00294f, -0.00290f, -0.00223f, -0.00214f, -0.00138f, -0.00129f, -0.00045f,
        -0.00059f
    ];

    private static readonly float[] Ad6 =
    [
        -0.00047f, -0.00067f, -0.00127f, -0.00167f, -0.00214f, -0.00248f, -0.00290f, -0.00313f,
        -0.00365f, -0.00367f, -0.00375f, -0.00391f, -0.00374f, -0.00392f, -0.00381f, -0.00308f,
        -0.00255f, -0.00238f, -0.00198f, -0.00124f, -0.00055f, 0.00016f, 0.00101f, 0.00180f,
        0.00259f, 0.00345f, 0.00428f, 0.00494f, 0.00572f, 0.00633f, 0.00672f, 0.00735f,
        0.00849f, 0.00817f, 0.00732f, 0.00710f, 0.00825f, 0.00656f, 0.00734f, 0.00498f,
        0.00459f, 0.00297f, 0.00310f, -0.00061f, 0.00014f, -0.00509f, -0.00350f, -0.00885f,
        -0.00942f, -0.01558f, -0.01784f, -0.01878f, -0.02946f, -0.02809f, -0.03827f, -0.03491f,
        -0.05304f, -0.03885f, -0.06609f, -0.04895f, -0.08610f, -0.01486f, -0.18834f, 0.17518f,
        0.90420f, 0.17518f, -0.18834f, -0.01486f, -0.08610f, -0.04895f, -0.06609f, -0.03885f,
        -0.05304f, -0.03491f, -0.03827f, -0.02809f, -0.02946f, -0.01878f, -0.01784f, -0.01558f,
        -0.00942f, -0.00885f, -0.00350f, -0.00509f, 0.00014f, -0.00061f, 0.00310f, 0.00297f,
        0.00459f, 0.00498f, 0.00734f, 0.00656f, 0.00825f, 0.00710f, 0.00732f, 0.00817f,
        0.00849f, 0.00735f, 0.00672f, 0.00633f, 0.00572f, 0.00494f, 0.00428f, 0.00345f,
        0.00259f, 0.00180f, 0.00101f, 0.00016f, -0.00055f, -0.00124f, -0.00198f, -0.00238f,
        -0.00255f, -0.00308f, -0.00381f, -0.00392f, -0.00374f, -0.00391f, -0.00375f, -0.00367f,
        -0.00365f, -0.00313f, -0.00290f, -0.00248f, -0.00214f, -0.00167f, -0.00127f, -0.00067f,
        -0.00047f
    ];

    private static readonly float[] Ad7 =
    [
        -0.00048f, -0.00082f, -0.00134f, -0.00165f, -0.00215f, -0.00245f, -0.00286f, -0.00330f,
        -0.00383f, -0.00378f, -0.00380f, -0.00398f, -0.00398f, -0.00384f, -0.00355f, -0.00344f,
        -0.00296f, -0.00238f, -0.00193f, -0.00128f, -0.00044f, 0.00022f, 0.00091f, 0.00179f,
        0.00257f, 0.00348f, 0.00423f, 0.00517f, 0.00607f, 0.00647f, 0.00708f, 0.00769f,
        0.00790f, 0.00776f, 0.00854f, 0.00785f, 0.00798f, 0.00696f, 0.00773f, 0.00518f,
        0.00415f, 0.00273f, 0.00391f, 0.00031f, 0.00032f, -0.00422f, -0.00446f, -0.01034f,
        -0.01012f, -0.01578f, -0.02028f, -0.01956f, -0.03149f, -0.02973f, -0.03725f, -0.03587f,
        -0.05040f, -0.03984f, -0.06343f, -0.05842f, -0.07842f, -0.02115f, -0.19312f, 0.19447f,
        0.89439f, 0.19447f, -0.19312f, -0.02115f, -0.07842f, -0.05842f, -0.06343f, -0.03984f,
        -0.05040f, -0.03587f, -0.03725f, -0.02973f, -0.03149f, -0.01956f, -0.02028f, -0.01578f,
        -0.01012f, -0.01034f, -0.00446f, -0.00422f, 0.00032f, 0.00031f, 0.00391f, 0.00273f,
        0.00415f, 0.00518f, 0.00773f, 0.00696f, 0.00798f, 0.00785f, 0.00854f, 0.00776f,
        0.00790f, 0.00769f, 0.00708f, 0.00647f, 0.00607f, 0.00517f, 0.00423f, 0.00348f,
        0.00257f, 0.00179f, 0.00091f, 0.00022f, -0.00044f, -0.00128f, -0.00193f, -0.00238f,
        -0.00296f, -0.00344f, -0.00355f, -0.00384f, -0.00398f, -0.00398f, -0.00380f, -0.00378f,
        -0.00383f, -0.00330f, -0.00286f, -0.00245f, -0.00215f, -0.00165f, -0.00134f, -0.00082f,
        -0.00048f
    ];

    private static readonly float[] Ad8 =
    [
        -0.00024f, 0.00012f, -0.00062f, -0.00034f, -0.00078f, -0.00066f, -0.00108f, -0.00086f,
        -0.00135f, -0.00099f, -0.00130f, -0.00114f, -0.00107f, -0.00111f, -0.00110f, -0.00099f,
        -0.00062f, -0.00056f, -0.00069f, -0.00071f, 0.00003f, -0.00004f, 0.00006f, 0.00011f,
        0.00046f, 0.00052f, 0.00106f, 0.00126f, 0.00221f, 0.00114f, 0.00224f, 0.00227f,
        0.00354f, 0.00196f, 0.00337f, 0.00191f, 0.00413f, 0.00154f, 0.00338f, 0.00134f,
        0.00236f, 0.00042f, 0.00231f, -0.00072f, 0.00224f, -0.00115f, 0.00032f, 0.00077f,
        0.00043f, 0.00069f, -0.00339f, -0.00051f, -0.01056f, -0.00489f, -0.02156f, -0.00656f,
        -0.03922f, -0.01771f, -0.06476f, -0.02858f, -0.11018f, -0.03685f, -0.22334f, 0.07845f,
        0.91960f, 0.07845f, -0.22334f, -0.03685f, -0.11018f, -0.02858f, -0.06476f, -0.01771f,
        -0.03922f, -0.00656f, -0.02156f, -0.00489f, -0.01056f, -0.00051f, -0.00339f, 0.00069f,
        0.00043f, 0.00077f, 0.00032f, -0.00115f, 0.00224f, -0.00072f, 0.00231f, 0.00042f,
        0.00236f, 0.00134f, 0.00338f, 0.00154f, 0.00413f, 0.00191f, 0.00337f, 0.00196f,
        0.00354f, 0.00227f, 0.00224f, 0.00114f, 0.00221f, 0.00126f, 0.00106f, 0.00052f,
        0.00046f, 0.00011f, 0.00006f, -0.00004f, 0.00003f, -0.00071f, -0.00069f, -0.00056f,
        -0.00062f, -0.00099f, -0.00110f, -0.00111f, -0.00107f, -0.00114f, -0.00130f, -0.00099f,
        -0.00135f, -0.00086f, -0.00108f, -0.00066f, -0.00078f, -0.00034f, -0.00062f, 0.00012f,
        -0.00024f
    ];

    private static readonly float[] Ad9 =
    [
        0.00015f, 0.00030f, -0.00006f, -0.00018f, -0.00013f, -0.00018f, -0.00023f, -0.00029f,
        -0.00044f, -0.00014f, -0.00012f, -0.00021f, -0.00009f, -0.00021f, -0.00008f, -0.00028f,
        -0.00011f, -0.00005f, -0.00019f, -0.00008f, 0.00007f, 0.00020f, -0.00039f, -0.00019f,
        0.00026f, -0.00018f, -0.00038f, 0.00051f, 0.00032f, 0.00034f, 0.00007f, 0.00065f,
        0.00041f, 0.00033f, 0.00078f, 0.00053f, 0.00106f, 0.00043f, 0.00123f, 0.00049f,
        -0.00049f, -0.00092f, 0.00039f, -0.00115f, 0.00105f, -0.00240f, 0.00240f, 0.00360f,
        0.00709f, 0.00595f, 0.00739f, 0.00404f, 0.00348f, 0.00074f, -0.00186f, -0.00121f,
        -0.01742f, -0.01692f, -0.04347f, -0.04120f, -0.10291f, -0.08158f, -0.29180f, 0.12854f,
        0.86799f, 0.12854f, -0.29180f, -0.08158f, -0.10291f, -0.04120f, -0.04347f, -0.01692f,
        -0.01742f, -0.00121f, -0.00186f, 0.00074f, 0.00348f, 0.00404f, 0.00739f, 0.00595f,
        0.00709f, 0.00360f, 0.00240f, -0.00240f, 0.00105f, -0.00115f, 0.00039f, -0.00092f,
        -0.00049f, 0.00049f, 0.00123f, 0.00043f, 0.00106f, 0.00053f, 0.00078f, 0.00033f,
        0.00041f, 0.00065f, 0.00007f, 0.00034f, 0.00032f, 0.00051f, -0.00038f, -0.00018f,
        0.00026f, -0.00019f, -0.00039f, 0.00020f, 0.00007f, -0.00008f, -0.00019f, -0.00005f,
        -0.00011f, -0.00028f, -0.00008f, -0.00021f, -0.00009f, -0.00021f, -0.00012f, -0.00014f,
        -0.00044f, -0.00029f, -0.00023f, -0.00018f, -0.00013f, -0.00018f, -0.00006f, 0.00030f,
        0.00015f
    ];

    public static ReadOnlySpan<float> Get(int model) => model switch {
        0 => Null,
        1 => Proakis,
        2 or 3 or 4 => Ad1,
        5 or 6 or 7 => Ad5,
        8 or 9 or 10 => Ad6,
        11 or 12 or 13 => Ad7,
        14 or 15 or 16 => Ad8,
        17 or 18 or 19 => Ad9,
        _ => throw new ArgumentOutOfRangeException(nameof(model), model, "The SpanDSP line model must be in the range 0 through 19.")
    };

    private static float[] CreateNull() {
        var coefficients = new float[129];
        coefficients[128] = 1.0f;
        return coefficients;
    }
}

internal sealed class V17WaveReader : IDisposable {
    private readonly FileStream _stream;
    private readonly int _channels;
    private readonly int _blockAlign;
    private long _remainingBytes;

    public V17WaveReader(string path, int requiredSampleRate) {
        _stream = File.OpenRead(path);
        var header = ParseHeader(_stream);
        _channels = header.Channels;
        _blockAlign = header.BlockAlign;
        _remainingBytes = header.DataLength;
        if (header.SampleRate != requiredSampleRate || header.BitsPerSample != 16)
            throw new InvalidDataException("The V.17 decoder requires 8 kHz, 16-bit PCM WAV input.");
        _stream.Position = header.DataOffset;
    }

    public int Read(Span<short> destination) {
        if (_remainingBytes <= 0)
            return 0;
        int frames = (int)Math.Min(destination.Length, _remainingBytes / _blockAlign);
        byte[] buffer = new byte[frames * _blockAlign];
        int bytesRead = _stream.Read(buffer, 0, buffer.Length);
        frames = bytesRead / _blockAlign;
        for (int frame = 0; frame < frames; frame++)
            destination[frame] = BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(frame * _blockAlign, 2));
        _remainingBytes -= frames * _blockAlign;
        return frames;
    }

    public void Dispose() => _stream.Dispose();

    private static (int Channels, int SampleRate, int BitsPerSample, int BlockAlign, long DataOffset, long DataLength)
        ParseHeader(Stream stream) {
        Span<byte> header = stackalloc byte[12];
        ReadExactly(stream, header);
        if (!header[..4].SequenceEqual("RIFF"u8) || !header[8..12].SequenceEqual("WAVE"u8))
            throw new InvalidDataException("Not a RIFF/WAVE file.");

        int channels = 0;
        int sampleRate = 0;
        int bitsPerSample = 0;
        int blockAlign = 0;
        long dataOffset = 0;
        long dataLength = 0;

        Span<byte> chunkHeader = stackalloc byte[8];
        while (stream.Position + 8 <= stream.Length) {
            ReadExactly(stream, chunkHeader);
            int chunkLength = BinaryPrimitives.ReadInt32LittleEndian(chunkHeader[4..]);
            string id = global::System.Text.Encoding.ASCII.GetString(chunkHeader[..4]);
            long next = stream.Position + chunkLength + (chunkLength & 1);
            if (id == "fmt ") {
                byte[] format = new byte[chunkLength];
                ReadExactly(stream, format);
                int formatTag = BinaryPrimitives.ReadInt16LittleEndian(format.AsSpan(0, 2));
                if (formatTag != 1)
                    throw new InvalidDataException("Only PCM WAV is supported.");
                channels = BinaryPrimitives.ReadInt16LittleEndian(format.AsSpan(2, 2));
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(format.AsSpan(4, 4));
                blockAlign = BinaryPrimitives.ReadInt16LittleEndian(format.AsSpan(12, 2));
                bitsPerSample = BinaryPrimitives.ReadInt16LittleEndian(format.AsSpan(14, 2));
            } else if (id == "data") {
                dataOffset = stream.Position;
                dataLength = chunkLength;
                stream.Position = next;
            } else {
                stream.Position = next;
            }
            if (channels > 0 && dataOffset > 0)
                break;
        }

        if (channels <= 0 || blockAlign <= 0 || dataOffset <= 0)
            throw new InvalidDataException("Incomplete WAV header.");
        return (channels, sampleRate, bitsPerSample, blockAlign, dataOffset, dataLength);
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer) {
        int offset = 0;
        while (offset < buffer.Length) {
            int read = stream.Read(buffer[offset..]);
            if (read == 0)
                throw new EndOfStreamException();
            offset += read;
        }
    }
}

internal sealed class V17WaveWriter : IDisposable {
    private readonly FileStream _stream;
    private readonly int _channels;
    private readonly int _sampleRate;
    private long _sampleCount;

    public V17WaveWriter(string path, int channels, int sampleRate) {
        _channels = channels;
        _sampleRate = sampleRate;
        _stream = File.Create(path);
        _stream.Write(new byte[44]);
    }

    public void Write(ReadOnlySpan<short> samples) {
        byte[] bytes = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2, 2), samples[i]);
        _stream.Write(bytes);
        _sampleCount += samples.Length;
    }

    public void Dispose() {
        long dataBytes = _sampleCount * 2;
        _stream.Position = 0;
        Span<byte> header = stackalloc byte[44];
        "RIFF"u8.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..8], checked((int)(36 + dataBytes)));
        "WAVEfmt "u8.CopyTo(header[8..16]);
        BinaryPrimitives.WriteInt32LittleEndian(header[16..20], 16);
        BinaryPrimitives.WriteInt16LittleEndian(header[20..22], 1);
        BinaryPrimitives.WriteInt16LittleEndian(header[22..24], (short)_channels);
        BinaryPrimitives.WriteInt32LittleEndian(header[24..28], _sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(header[28..32], _sampleRate * _channels * 2);
        BinaryPrimitives.WriteInt16LittleEndian(header[32..34], (short)(_channels * 2));
        BinaryPrimitives.WriteInt16LittleEndian(header[34..36], 16);
        "data"u8.CopyTo(header[36..40]);
        BinaryPrimitives.WriteInt32LittleEndian(header[40..44], checked((int)dataBytes));
        _stream.Write(header);
        _stream.Dispose();
    }
}

public sealed class V17TestExitException : Exception {
    public V17TestExitException(int exitCode) : base($"V.17 test requested exit code {exitCode}.") {
        ExitCode = exitCode;
    }

    public int ExitCode { get; }
}
