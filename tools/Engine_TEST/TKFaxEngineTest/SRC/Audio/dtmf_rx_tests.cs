/*
 * TKFaxEngine - managed C# test port
 *
 * dtmf_rx_tests.cs - Test the DTMF detector against the established
 * spanDSP/Mitel/Bellcore test sequence.
 *
 * Ported directly from the supplied spanDSP/TKFaxEngine test source.
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2001, 2006 Steve Underwood.
 *
 * This test retains the GNU General Public License version 2 terms of the
 * supplied source file.
 */

#nullable enable
using TKFaxEngine;

using System.Diagnostics;
using System.Text;
using TKFaxEngine.Audio;
using static TKFaxEngine.Audio.g711;
using static TKFaxEngine.Audio.g726;
using static TKFaxEngine.Audio.tone_generate;

namespace TKFaxEngineTest.Audio.Test;

public static class DtmfRxTests {
    private const int DefaultDtmfTxLevel = -10;
    private const int DefaultDtmfTxOnTime = 50;
    private const int DefaultDtmfTxOffTime = 50;
    private const int SamplesPerChunk = 160;
    private const int SampleRate = 8000;
    private const string AllPossibleDigits = "123A456B789C*0#D";
    private const string MitelDirectory = "Test-Files/mitel/";
    private const string BellcoreDirectory = "Test-Files/bellcore/";

    private static readonly string[] BellcoreFiles =
    [
        MitelDirectory + "mitel-cm7291-talkoff.wav",
        BellcoreDirectory + "tr-tsy-00763-1.wav",
        BellcoreDirectory + "tr-tsy-00763-2.wav",
        BellcoreDirectory + "tr-tsy-00763-3.wav",
        BellcoreDirectory + "tr-tsy-00763-4.wav",
        BellcoreDirectory + "tr-tsy-00763-5.wav",
        BellcoreDirectory + "tr-tsy-00763-6.wav"
    ];

    private static readonly tone_gen_descriptor_t[] MyDtmfDigitTones = CreateToneDescriptors();
    private static readonly float[] DtmfRows = [697.0f, 770.0f, 852.0f, 941.0f];
    private static readonly float[] DtmfColumns = [1209.0f, 1336.0f, 1477.0f, 1633.0f];
    private const string DtmfPositions = "123A456B789C*0#D";
    private static readonly object CallbackMarker = new();

    private static readonly short[] Amplitudes = new short[1_000_000];
    private static readonly short[] Amplitudes2 = new short[1_000_000];

    private static bool _callbackHit;
    private static bool _callbackOk;
    private static int _callbackRoll;
    private static int _step;
    private static int _lastStatusStep;
    private static bool _firstStatusCallback;
    private static float _maxForwardTwist;
    private static float _maxReverseTwist;
    private static bool _useDialToneFilter;
    private static string? _decodeTestFile;
    private static CodecMunge? _munge;

    public static int BodyMain(string[] args) {
        try {
            _useDialToneFilter = false;
            int channelCodec = (int)MungeCodec.None;
            _decodeTestFile = null;
            _maxForwardTwist = -1.0f;
            _maxReverseTwist = -1.0f;

            if (!ParseArguments(args, ref channelCodec))
                return 2;

            using var munge = new CodecMunge(channelCodec, 0);
            _munge = munge;

            if (_decodeTestFile is not null) {
                DecodeTest(_decodeTestFile);
            } else {
                var stopwatch = Stopwatch.StartNew();
                MitelCm7291Side1Tests();
                MitelCm7291Side2AndBellcoreTests();
                DialToneToleranceTests();
                CallbackFunctionTests();
                Console.WriteLine("    Passed");
                Console.WriteLine("Tests passed in {0}s", (int)stopwatch.Elapsed.TotalSeconds);
            }

            _munge = null;
            return 0;
        } catch (TestFailureException exception) {
            Console.Error.WriteLine(exception.Message);
            return 2;
        } catch (Exception exception) {
            Console.Error.WriteLine("DTMF RX test failed: {0}", exception);
            return 2;
        } finally {
            _munge = null;
        }
    }

    private static tone_gen_descriptor_t[] CreateToneDescriptors() {
        var descriptors = new tone_gen_descriptor_t[16];
        for (int i = 0; i < descriptors.Length; i++)
            descriptors[i] = new tone_gen_descriptor_t();
        return descriptors;
    }

    private static bool ParseArguments(string[] args, ref int channelCodec) {
        for (int index = 0; index < args.Length; index++) {
            string argument = args[index];
            switch (argument) {
                case "-c":
                    if (!TryTakeValue(args, ref index, out string? codecText) ||
                        !int.TryParse(codecText, out channelCodec)) {
                        return false;
                    }
                    break;

                case "-d":
                    if (!TryTakeValue(args, ref index, out _decodeTestFile))
                        return false;
                    break;

                case "-F":
                    if (!TryTakeValue(args, ref index, out string? forwardText) ||
                        !float.TryParse(forwardText, global::System.Globalization.NumberStyles.Float,
                            global::System.Globalization.CultureInfo.InvariantCulture, out _maxForwardTwist)) {
                        return false;
                    }
                    break;

                case "-f":
                    _useDialToneFilter = true;
                    break;

                case "-R":
                    if (!TryTakeValue(args, ref index, out string? reverseText) ||
                        !float.TryParse(reverseText, global::System.Globalization.NumberStyles.Float,
                            global::System.Globalization.CultureInfo.InvariantCulture, out _maxReverseTwist)) {
                        return false;
                    }
                    break;

                default:
                    return false;
            }
        }

        return true;
    }

    private static bool TryTakeValue(string[] args, ref int index, out string? value) {
        if (++index >= args.Length) {
            value = null;
            return false;
        }

        value = args[index];
        return true;
    }

    private static void MyDtmfGenInit(
        float lowFudge,
        int lowLevel,
        float highFudge,
        int highLevel,
        int duration,
        int gap) {
        for (int row = 0; row < 4; row++) {
            for (int column = 0; column < 4; column++) {
                int position = row * 4 + column;
                MyDtmfDigitTones[position] = tone_gen_descriptor_init(
                    MyDtmfDigitTones[position],
                    (int)(DtmfRows[row] * (1.0f + lowFudge)),
                    lowLevel,
                    (int)(DtmfColumns[column] * (1.0f + highFudge)),
                    highLevel,
                    duration,
                    gap,
                    0,
                    0,
                    0)!;
            }
        }
    }

    private static int MyDtmfGenerate(short[] destination, int offset, string digits) {
        int length = 0;

        foreach (char digit in digits) {
            int position = DtmfPositions.IndexOf(digit);
            if (position < 0)
                continue;

            tone_gen_state_t tone = tone_gen_init(null, MyDtmfDigitTones[position])!;
            int maximum = Math.Min(1000, destination.Length - offset - length);
            if (maximum <= 0)
                throw new InvalidOperationException("DTMF generation buffer exhausted.");

            length += tone_gen(tone, destination.AsSpan(offset + length), maximum);
            tone_gen_free(tone);
        }

        return length;
    }

    private static int MyDtmfGenerate(short[] destination, string digits) =>
        MyDtmfGenerate(destination, 0, digits);

    private static void DigitDelivery(object? data, string digits, int length) {
        _callbackHit = true;

        if (!ReferenceEquals(data, CallbackMarker)) {
            _callbackOk = false;
            return;
        }

        string expected = AllPossibleDigits;
        int sourceOffset = _callbackRoll;
        int segment = 16 - _callbackRoll;

        for (int index = 0; index < length; index += segment, segment = 16) {
            if (index + segment > length)
                segment = length - index;

            ReadOnlySpan<char> actualSegment = digits.AsSpan(index, segment);
            ReadOnlySpan<char> expectedSegment = expected.AsSpan(sourceOffset, segment);
            if (!actualSegment.SequenceEqual(expectedSegment)) {
                _callbackOk = false;
                Console.WriteLine("Fail at {0} {1}", index, segment);
                break;
            }

            sourceOffset = 0;
            _callbackRoll = (_callbackRoll + segment) % 16;
        }
    }

    private static void DigitStatus(object? data, int signal, int level, int delay) {
        _ = delay;
        _callbackHit = true;
        int length = _step - _lastStatusStep;

        if (!ReferenceEquals(data, CallbackMarker)) {
            _callbackOk = false;
            return;
        }

        if (length < 320 || length > 480) {
            if (_firstStatusCallback) {
                _firstStatusCallback = false;
            } else {
                Console.WriteLine(
                    "Failed for signal {0} length {1} at {2}",
                    (_callbackRoll & 1) != 0 ? "on" : "off",
                    length,
                    _step);
                _callbackOk = false;
            }
        }

        if ((_callbackRoll & 1) != 0) {
            if (signal != 0) {
                Console.WriteLine("Failed for signal 0x{0:X} instead of 0", signal);
                _callbackOk = false;
            }
        } else {
            int expectedSignal = AllPossibleDigits[_callbackRoll >> 1];
            if (signal != expectedSignal) {
                Console.WriteLine(
                    "Failed for signal 0x{0:X} instead of 0x{1:X}",
                    signal,
                    expectedSignal);
                _callbackOk = false;
            }

            if (level < DefaultDtmfTxLevel - 1 || level > DefaultDtmfTxLevel + 1) {
                Console.WriteLine(
                    "Failed for level {0} instead of {1}",
                    level,
                    DefaultDtmfTxLevel);
                _callbackOk = false;
            }
        }

        if (++_callbackRoll >= 32)
            _callbackRoll = 0;

        _lastStatusStep = _step;
    }

    private static DtmfRxState CreateReceiver(
        DtmfDigitsReceivedHandler? callback = null,
        object? callbackData = null) {
        var state = new DtmfRxState(callback, callbackData);
        ConfigureReceiver(state);
        state.Logging.DebugEnabled = true;
        state.Logging.Sink = message => Console.Error.WriteLine("DTMF-rx: {0}", message);
        return state;
    }

    private static void ConfigureReceiver(DtmfRxState state) {
        if (_useDialToneFilter || _maxForwardTwist >= 0.0f || _maxReverseTwist >= 0.0f) {
            state.Configure(
                _useDialToneFilter ? 1 : 0,
                _maxForwardTwist,
                _maxReverseTwist,
                -99.0f);
        }
    }

    private static void Process(DtmfRxState state, short[] samples, int offset, int length) {
        state.Process(samples.AsSpan(offset, length));
    }

    private static string ReadDigits(DtmfRxState state, int maximum = 128) =>
        state.GetDigits(maximum);

    private static void MitelCm7291Side1Tests() {
        using DtmfRxState state = CreateReceiver();

        Console.WriteLine("Test 1: Calibration");
        Console.WriteLine("    Passed");

        Console.WriteLine("Test 2: Decode check");
        MyDtmfGenInit(0.0f, -4, 0.0f, -4, 50, 50);

        foreach (char digit in AllPossibleDigits) {
            string digitText = digit.ToString();
            for (int repeat = 0; repeat < 10; repeat++) {
                int length = MyDtmfGenerate(Amplitudes, digitText);
                Munge(Amplitudes, length);
                Process(state, Amplitudes, 0, length);
                string actual = ReadDigits(state);

                if (actual.Length != 1 || actual[0] != digit) {
                    Console.WriteLine("    Sent     '{0}'", digitText);
                    Console.WriteLine("    Received '{0}'", actual);
                    Fail("    Failed");
                }
            }
        }
        Console.WriteLine("    Passed");

        Console.WriteLine("Test 3: Recognition bandwidth and channel centre frequency check");
        foreach (char digit in "159D") {
            string digitText = digit.ToString();
            int positive = 0;
            for (int i = 1; i <= 60; i++) {
                MyDtmfGenInit(i / 1000.0f, -17, 0.0f, -17, 50, 50);
                int length = MyDtmfGenerate(Amplitudes, digitText);
                Munge(Amplitudes, length);
                Process(state, Amplitudes, 0, length);
                positive += ReadDigits(state).Length;
            }

            int negative = 0;
            for (int i = -1; i >= -60; i--) {
                MyDtmfGenInit(i / 1000.0f, -17, 0.0f, -17, 50, 50);
                int length = MyDtmfGenerate(Amplitudes, digitText);
                Munge(Amplitudes, length);
                Process(state, Amplitudes, 0, length);
                negative += ReadDigits(state).Length;
            }

            float recognitionBandwidth = (positive + negative) / 10.0f;
            float centreFrequencyOffset = (positive - negative) / 10.0f;
            Console.WriteLine(
                "    {0} (low)  rrb = {1,5:F2}%, rcfo = {2,5:F2}%, max -ve = {3,5:F2}, max +ve = {4,5:F2}",
                digit,
                recognitionBandwidth,
                centreFrequencyOffset,
                negative / 10.0f,
                positive / 10.0f);

            if (recognitionBandwidth < 3.0f + centreFrequencyOffset ||
                recognitionBandwidth >= 15.0f + centreFrequencyOffset) {
                Fail("    Failed");
            }

            positive = 0;
            for (int i = 1; i <= 60; i++) {
                MyDtmfGenInit(0.0f, -17, i / 1000.0f, -17, 50, 50);
                int length = MyDtmfGenerate(Amplitudes, digitText);
                Munge(Amplitudes, length);
                Process(state, Amplitudes, 0, length);
                positive += ReadDigits(state).Length;
            }

            negative = 0;
            for (int i = -1; i >= -60; i--) {
                MyDtmfGenInit(0.0f, -17, i / 1000.0f, -17, 50, 50);
                int length = MyDtmfGenerate(Amplitudes, digitText);
                Munge(Amplitudes, length);
                Process(state, Amplitudes, 0, length);
                negative += ReadDigits(state).Length;
            }

            recognitionBandwidth = (positive + negative) / 10.0f;
            centreFrequencyOffset = (positive - negative) / 10.0f;
            Console.WriteLine(
                "    {0} (high) rrb = {1,5:F2}%, rcfo = {2,5:F2}%, max -ve = {3,5:F2}, max +ve = {4,5:F2}",
                digit,
                recognitionBandwidth,
                centreFrequencyOffset,
                negative / 10.0f,
                positive / 10.0f);

            if (recognitionBandwidth < 3.0f + centreFrequencyOffset ||
                recognitionBandwidth >= 15.0f + centreFrequencyOffset) {
                Fail("    Failed");
            }
        }
        Console.WriteLine("    Passed");

        Console.WriteLine("Test 4: Acceptable amplitude ratio (twist)");
        foreach (char digit in "159D") {
            string digitText = digit.ToString();
            int positive = 0;
            for (int i = -30; i >= -230; i--) {
                MyDtmfGenInit(0.0f, -3, 0.0f, i / 10, 50, 50);
                int length = MyDtmfGenerate(Amplitudes, digitText);
                Munge(Amplitudes, length);
                Process(state, Amplitudes, 0, length);
                positive += ReadDigits(state).Length;
            }

            Console.WriteLine("    {0} normal twist  = {1:F2}dB", digit, positive / 10.0f);
            if (positive < 80)
                Fail("    Failed");

            int negative = 0;
            for (int i = -30; i >= -230; i--) {
                MyDtmfGenInit(0.0f, i / 10, 0.0f, -3, 50, 50);
                int length = MyDtmfGenerate(Amplitudes, digitText);
                Munge(Amplitudes, length);
                Process(state, Amplitudes, 0, length);
                negative += ReadDigits(state).Length;
            }

            Console.WriteLine("    {0} reverse twist = {1:F2}dB", digit, negative / 10.0f);
            if (negative < 40)
                Fail("    Failed");
        }
        Console.WriteLine("    Passed");

        Console.WriteLine("Test 5: Dynamic range");
        int dynamicRange = 0;
        for (int level = 3; level >= -50; level--) {
            MyDtmfGenInit(0.0f, level, 0.0f, level, 50, 50);
            int length = MyDtmfGenerate(Amplitudes, "1");
            Munge(Amplitudes, length);
            Process(state, Amplitudes, 0, length);
            dynamicRange += ReadDigits(state).Length;
        }

        Console.WriteLine("    Dynamic range = {0}dB", dynamicRange);
        if (dynamicRange < 35)
            Fail("    Failed");
        Console.WriteLine("    Passed");

        Console.WriteLine("Test 6: Guard time");
        int guardHits = 0;
        for (int duration = 490; duration >= 100; duration--) {
            MyDtmfGenInit(0.0f, -3, 0.0f, -3, duration / 10, 50);
            int length = MyDtmfGenerate(Amplitudes, "1");
            Munge(Amplitudes, length);
            Process(state, Amplitudes, 0, length);
            guardHits += ReadDigits(state).Length;
        }
        Console.WriteLine("    Guard time = {0}ms", (500 - guardHits) / 10);
        Console.WriteLine("    Passed");

        Console.WriteLine("Test 7: Acceptable signal to noise ratio");
        MyDtmfGenInit(0.0f, -4, 0.0f, -4, 50, 50);
        int noiseLevel;
        for (noiseLevel = -13; noiseLevel > -50; noiseLevel--) {
            var noiseSource = new Awgn(1_234_567, noiseLevel);
            int repeat;
            for (repeat = 0; repeat < 1000; repeat++) {
                int length = MyDtmfGenerate(Amplitudes, "1");
                for (int sample = 0; sample < length; sample++)
                    Amplitudes[sample] = Saturated.sat_add16(Amplitudes[sample], noiseSource.NextSample());

                Munge(Amplitudes, length);
                Process(state, Amplitudes, 0, length);
                if (ReadDigits(state).Length != 1)
                    break;
            }

            if (repeat == 1000)
                break;
        }

        Console.WriteLine("    Acceptable S/N ratio is {0}dB", -4 - noiseLevel);
        if (-4 - noiseLevel > 26)
            Fail("    Failed");
        Console.WriteLine("    Passed");
    }

    private static void MitelCm7291Side2AndBellcoreTests() {
        using DtmfRxState state = CreateReceiver();
        var hitTypes = new int[256];

        Console.WriteLine("Test 8: Talk-off test");

        for (int fileIndex = 0; fileIndex < BellcoreFiles.Length; fileIndex++) {
            string file = BellcoreFiles[fileIndex];
            using var input = new TelephonyWaveReader(file, 1, SampleRate);
            int hits = 0;

            while (true) {
                int frames = input.ReadFrames(Amplitudes.AsSpan(0, SampleRate));
                if (frames == 0)
                    break;

                Process(state, Amplitudes, 0, frames);
                string digits = ReadDigits(state);
                foreach (char digit in digits) {
                    if (digit <= byte.MaxValue)
                        hitTypes[digit]++;
                }
                hits += digits.Length;
            }

            Console.WriteLine("    File {0} gave {1} false hits.", fileIndex + 1, hits);
        }

        int total = 0;
        for (int digit = 0; digit < hitTypes.Length; digit++) {
            if (hitTypes[digit] == 0)
                continue;

            Console.WriteLine("    Digit {0} had {1} false hits.", (char)digit, hitTypes[digit]);
            total += hitTypes[digit];
        }

        Console.WriteLine("    {0} false hits in total.", total);
        if (total > 470)
            Fail("    Failed");

        Console.WriteLine("    Passed");
    }

    private static void DialToneToleranceTests() {
        using DtmfRxState state = CreateReceiver();

        Console.WriteLine("Test: Dial tone tolerance.");
        MyDtmfGenInit(
            0.0f,
            -15,
            0.0f,
            -15,
            DefaultDtmfTxOnTime,
            DefaultDtmfTxOffTime);

        int dialToneLevel;
        for (dialToneLevel = -30; dialToneLevel < -3; dialToneLevel++) {
            tone_gen_descriptor_t descriptor = tone_gen_descriptor_init(
                null,
                350,
                dialToneLevel,
                440,
                dialToneLevel,
                1,
                0,
                0,
                0,
                1)!;
            tone_gen_state_t dialTone = tone_gen_init(null, descriptor)!;

            int repeat;
            for (repeat = 0; repeat < 10; repeat++) {
                int length = MyDtmfGenerate(Amplitudes, AllPossibleDigits);
                int generated = tone_gen(dialTone, Amplitudes2, length);
                if (generated != length)
                    throw new InvalidOperationException("Dial tone generator returned an incomplete block.");

                for (int sample = 0; sample < length; sample++)
                    Amplitudes[sample] = Saturated.sat_add16(Amplitudes[sample], Amplitudes2[sample]);

                Munge(Amplitudes, length);
                Process(state, Amplitudes, 0, length);
                if (ReadDigits(state).Length != AllPossibleDigits.Length)
                    break;
            }

            tone_gen_free(dialTone);
            tone_gen_descriptor_free(descriptor);
            if (repeat != 10)
                break;
        }

        Console.WriteLine(
            "    Acceptable signal to dial tone ratio is {0}dB",
            -15 - dialToneLevel);

        if ((_useDialToneFilter && (-15 - dialToneLevel) > -12) ||
            (!_useDialToneFilter && (-15 - dialToneLevel) > 10)) {
            Fail("    Failed");
        }

        Console.WriteLine("    Passed");
    }

    private static void CallbackFunctionTests() {
        Console.WriteLine("Test: Callback digit delivery mode.");

        _callbackHit = false;
        _callbackOk = true;
        _callbackRoll = 0;

        using var state = CreateReceiver(DigitDelivery, CallbackMarker);
        MyDtmfGenInit(
            0.0f,
            DefaultDtmfTxLevel,
            0.0f,
            DefaultDtmfTxLevel,
            DefaultDtmfTxOnTime,
            DefaultDtmfTxOffTime);

        int repeatCount;
        for (repeatCount = 1; repeatCount < 10; repeatCount++) {
            int length = 0;
            for (int repeat = 0; repeat < repeatCount; repeat++)
                length += MyDtmfGenerate(Amplitudes, length, AllPossibleDigits);

            Process(state, Amplitudes, 0, length);
            if (!_callbackHit || !_callbackOk)
                break;
        }

        if (!_callbackHit || !_callbackOk)
            Fail("    Failed");
        Console.WriteLine("    Passed");

        Console.WriteLine("Test: Realtime callback digit delivery mode.");

        _callbackHit = false;
        _callbackOk = true;
        _callbackRoll = 0;
        _lastStatusStep = 0;
        _firstStatusCallback = true;

        state.Initialize(null, null);
        state.SetRealtimeCallback(DigitStatus, CallbackMarker);
        ConfigureReceiver(state);
        state.Logging.DebugEnabled = true;
        state.Logging.Sink = message => Console.Error.WriteLine("DTMF-rx: {0}", message);

        MyDtmfGenInit(
            0.0f,
            DefaultDtmfTxLevel,
            0.0f,
            DefaultDtmfTxLevel,
            DefaultDtmfTxOnTime,
            DefaultDtmfTxOffTime);

        _step = 0;
        for (repeatCount = 1; repeatCount < 10; repeatCount++) {
            int length = 0;
            for (int repeat = 0; repeat < repeatCount; repeat++)
                length += MyDtmfGenerate(Amplitudes, length, AllPossibleDigits);

            for (int sample = 0; sample < length; sample += SamplesPerChunk) {
                int chunk = Math.Min(SamplesPerChunk, length - sample);
                Process(state, Amplitudes, sample, chunk);
                if (!_callbackOk)
                    break;
                _step += chunk;
            }

            if (!_callbackHit || !_callbackOk)
                break;
        }

        if (!_callbackHit || !_callbackOk)
            Fail("    Failed");
    }

    private static void DecodeTest(string testFile) {
        var block = new short[SamplesPerChunk];
        using DtmfRxState state = CreateReceiver();
        using var input = new TelephonyWaveReader(testFile, 1, SampleRate);

        int total = 0;
        while (true) {
            int samples = input.ReadFrames(block);
            if (samples == 0)
                break;

            Munge(block, samples);
            state.Process(block.AsSpan(0, samples));
            string actual = ReadDigits(state);
            if (actual.Length > 0)
                Console.WriteLine("Received '{0}'", actual);
            total += actual.Length;
        }

        Console.WriteLine("{0} digits received", total);
    }

    private static void Munge(short[] samples, int length) {
        CodecMunge codec = _munge ?? throw new InvalidOperationException("Codec munging is not initialized.");
        codec.Process(samples.AsSpan(0, length));
    }

    private static void Fail(string message) => throw new TestFailureException(message);

    private enum MungeCodec {
        None = 0,
        ALaw = 1,
        ULaw = 2,
        G72640K = 3,
        G72632K = 4,
        G72624K = 5,
        G72616K = 6
    }

    private sealed class CodecMunge : IDisposable {
        private readonly MungeCodec _codec;
        private readonly int _rbsPattern;
        private readonly g726_state_t? _encoder;
        private readonly g726_state_t? _decoder;
        private int _sequence = 0;

        public CodecMunge(int codec, int information) {
            _codec = (MungeCodec)codec;
            _rbsPattern = information;

            int bitRate = _codec switch {
                MungeCodec.G72640K => 40_000,
                MungeCodec.G72632K => 32_000,
                MungeCodec.G72624K => 24_000,
                MungeCodec.G72616K => 16_000,
                _ => 0
            };

            if (bitRate != 0) {
                _encoder = g726_init(null, bitRate, G726_ENCODING_LINEAR, G726_PACKING_NONE)
                    ?? throw new InvalidOperationException("Unable to initialize the G.726 encoder.");
                _decoder = g726_init(null, bitRate, G726_ENCODING_LINEAR, G726_PACKING_NONE)
                    ?? throw new InvalidOperationException("Unable to initialize the G.726 decoder.");
            } else if (!Enum.IsDefined(_codec)) {
                throw new ArgumentOutOfRangeException(nameof(codec));
            }
        }

        public void Process(Span<short> samples) {
            switch (_codec) {
                case MungeCodec.None:
                    return;

                case MungeCodec.ALaw:
                    for (int i = 0; i < samples.Length; i++)
                        samples[i] = alaw_to_linear(linear_to_alaw(samples[i]));
                    return;

                case MungeCodec.ULaw:
                    for (int i = 0; i < samples.Length; i++) {
                        byte law = linear_to_ulaw(samples[i]);
                        if ((_rbsPattern & (1 << _sequence)) != 0)
                            law &= 0xFE;
                        samples[i] = ulaw_to_linear(law);
                    }
                    return;

                case MungeCodec.G72640K:
                case MungeCodec.G72632K:
                case MungeCodec.G72624K:
                case MungeCodec.G72616K:
                    ProcessG726(samples);
                    return;

                default:
                    throw new InvalidOperationException("Unsupported codec munging mode.");
            }
        }

        private void ProcessG726(Span<short> samples) {
            g726_state_t encoder = _encoder ?? throw new InvalidOperationException();
            g726_state_t decoder = _decoder ?? throw new InvalidOperationException();
            short[] input = new short[160];
            short[] output = new short[160];
            byte[] encoded = new byte[160];

            for (int offset = 0; offset < samples.Length; offset += 160) {
                int count = Math.Min(160, samples.Length - offset);
                samples.Slice(offset, count).CopyTo(input);
                int bytes = g726_encode(encoder, encoded, input, count);
                int decoded = g726_decode(decoder, output, encoded, bytes);
                if (decoded != count)
                    throw new InvalidOperationException("G.726 codec munging returned an incomplete sample block.");
                output.AsSpan(0, decoded).CopyTo(samples.Slice(offset, decoded));
            }
        }

        public void Dispose() {
            if (_encoder is not null)
                g726_free(_encoder);
            if (_decoder is not null)
                g726_free(_decoder);
        }
    }

    private sealed class TelephonyWaveReader : IDisposable {
        private readonly FileStream _stream;
        private readonly BinaryReader _reader;
        private readonly long _dataEnd;
        private readonly int _channels;
        private bool _disposed;

        public TelephonyWaveReader(string path, int expectedChannels, int expectedSampleRate) {
            try {
                _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            } catch (Exception exception) {
                throw new TestFailureException($"    Cannot open speech/audio file '{path}': {exception.Message}");
            }

            _reader = new BinaryReader(_stream, Encoding.ASCII, leaveOpen: true);
            ParseHeader(out int channels, out int sampleRate, out int bitsPerSample, out long dataOffset, out long dataLength);

            if (channels != expectedChannels || sampleRate != expectedSampleRate || bitsPerSample != 16) {
                throw new TestFailureException(
                    $"    Audio file '{path}' must be {expectedChannels} channel(s), {expectedSampleRate} Hz, PCM16.");
            }

            _channels = channels;
            _stream.Position = dataOffset;
            _dataEnd = dataOffset + dataLength;
        }

        public int ReadFrames(Span<short> destination) {
            ObjectDisposedException.ThrowIf(_disposed, this);
            int maximumSamples = destination.Length * _channels;
            long availableSamples = (_dataEnd - _stream.Position) / sizeof(short);
            int samplesToRead = (int)Math.Min(maximumSamples, availableSamples);
            int frames = samplesToRead / _channels;
            samplesToRead = frames * _channels;

            for (int i = 0; i < samplesToRead; i++)
                destination[i] = _reader.ReadInt16();

            return frames;
        }

        public void Dispose() {
            if (_disposed)
                return;
            _reader.Dispose();
            _stream.Dispose();
            _disposed = true;
        }

        private void ParseHeader(
            out int channels,
            out int sampleRate,
            out int bitsPerSample,
            out long dataOffset,
            out long dataLength) {
            if (ReadFourCc() != "RIFF")
                throw new InvalidDataException("Missing RIFF header.");
            _ = _reader.ReadUInt32();
            if (ReadFourCc() != "WAVE")
                throw new InvalidDataException("Missing WAVE header.");

            channels = 0;
            sampleRate = 0;
            bitsPerSample = 0;
            dataOffset = -1;
            dataLength = 0;
            bool pcmFormat = false;

            while (_stream.Position + 8 <= _stream.Length) {
                string chunkId = ReadFourCc();
                uint chunkLength = _reader.ReadUInt32();
                long chunkStart = _stream.Position;

                if (chunkId == "fmt ") {
                    ushort format = _reader.ReadUInt16();
                    channels = _reader.ReadUInt16();
                    sampleRate = checked((int)_reader.ReadUInt32());
                    _ = _reader.ReadUInt32();
                    _ = _reader.ReadUInt16();
                    bitsPerSample = _reader.ReadUInt16();
                    pcmFormat = format == 1;
                } else if (chunkId == "data") {
                    dataOffset = chunkStart;
                    dataLength = Math.Min(chunkLength, _stream.Length - chunkStart);
                }

                long next = chunkStart + chunkLength + (chunkLength & 1u);
                if (next > _stream.Length)
                    break;
                _stream.Position = next;
            }

            if (!pcmFormat || dataOffset < 0)
                throw new InvalidDataException("Unsupported or incomplete WAVE file.");
        }

        private string ReadFourCc() {
            byte[] bytes = _reader.ReadBytes(4);
            if (bytes.Length != 4)
                throw new EndOfStreamException();
            return Encoding.ASCII.GetString(bytes);
        }
    }

    private sealed class TestFailureException : Exception {
        public TestFailureException(string message) : base(message) {
        }
    }
}
