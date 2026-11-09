/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * r2_mf_rx_tests_adapter.cs - complete managed port of r2_mf_rx_tests.c.
 *
 * Original test written by Steve Underwood.
 * Licensed under the GNU General Public License version 2.
 */

#nullable enable

using System.Diagnostics;
using System.Globalization;
using System.Text;
using TKFaxEngine.Audio;
using static TKFaxEngine.Audio.g711;
using static TKFaxEngine.Audio.tone_generate;

namespace TKFaxEngineTest.Audio.Test;

public static class r2_mf_rx_tests_adapter {
    private const int SAMPLE_RATE = 8000;
    private const int SAMPLES_PER_CHUNK = 160;
    private const string R2_MF_TONE_CODES = "1234567890BCDEF";

    private static readonly ToneSpec[] R2MfForwardTones =
    {
        new(1380.0, 1500.0), new(1380.0, 1620.0), new(1500.0, 1620.0),
        new(1380.0, 1740.0), new(1500.0, 1740.0), new(1620.0, 1740.0),
        new(1380.0, 1860.0), new(1500.0, 1860.0), new(1620.0, 1860.0),
        new(1740.0, 1860.0), new(1380.0, 1980.0), new(1500.0, 1980.0),
        new(1620.0, 1980.0), new(1740.0, 1980.0), new(1860.0, 1980.0)
    };

    private static readonly ToneSpec[] R2MfBackwardTones =
    {
        new(1140.0, 1020.0), new(1140.0, 900.0), new(1020.0, 900.0),
        new(1140.0, 780.0), new(1020.0, 780.0), new(900.0, 780.0),
        new(1140.0, 660.0), new(1020.0, 660.0), new(900.0, 660.0),
        new(780.0, 660.0), new(1140.0, 540.0), new(1020.0, 540.0),
        new(900.0, 540.0), new(780.0, 540.0), new(660.0, 540.0)
    };

    private static readonly tone_gen_descriptor_t?[] GeneratedTones = new tone_gen_descriptor_t?[15];
    private static readonly object CallbackSentinel = new();

    private static bool _callbackOk;
    private static int _callbackRoll;
    private static string? _decodeTestFile;

    public static int main(string[] testArguments) {
        return tkfx_r2_mf_rx_tests_main(testArguments);
    }

    public static int tkfx_r2_mf_rx_tests_main(string[] testArguments) {
        try {
            return tkfx_r2_mf_rx_tests_adapter_body_main(testArguments);
        } catch (R2MfRxTestsAdapterExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_r2_mf_rx_tests_adapter_body_main(string[] testArguments) {
        _decodeTestFile = null;

        for (int index = 0; index < testArguments.Length; index++) {
            string option = testArguments[index];
            if (option == "-d") {
                if (++index >= testArguments.Length)
                    Exit(2);
                _decodeTestFile = testArguments[index];
            } else {
                Exit(2);
            }
        }

        if (_decodeTestFile is not null) {
            DecodeTest(_decodeTestFile);
            return 0;
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        Console.WriteLine("R2 forward tones");
        TestAToneSet(forward: true);
        Console.WriteLine("R2 backward tones");
        TestAToneSet(forward: false);
        stopwatch.Stop();

        Console.WriteLine(
            string.Format(
                CultureInfo.InvariantCulture,
                "Tests passed in {0}s",
                (long)stopwatch.Elapsed.TotalSeconds));
        return 0;
    }

    private static int TestAToneSet(bool forward) {
        var samples = new short[100_000];
        var receiver = new R2MfRx(forward);

        Console.WriteLine("Test 1: Calibration");
        Console.WriteLine("    Passed");

        Console.WriteLine("Test 2: Decode check");
        InitialiseGeneratedTones(0.0f, -3, 0.0f, -3, 68, forward);
        foreach (char digit in R2_MF_TONE_CODES) {
            for (int repeat = 0; repeat < 10; repeat++) {
                int length = GenerateTone(samples, digit);
                MungeALaw(samples.AsSpan(0, length));
                receiver.Process(samples.AsSpan(0, length));
                char actual = receiver.CurrentDigit;
                if (actual != digit) {
                    Console.WriteLine($"    Sent     '{digit}'");
                    Console.WriteLine($"    Received 0x{(int)actual:X}");
                    Fail();
                }
            }
        }
        Console.WriteLine("    Passed");

        Console.WriteLine("Test 3: Recognition bandwidth and channel centre frequency check");
        int toneIndex = 0;
        foreach (char digit in R2_MF_TONE_CODES) {
            int positive = 0;
            for (int offset = 1; offset <= 60; offset++) {
                InitialiseGeneratedTones(offset / 1000.0f, -17, 0.0f, -17, 68, forward);
                int length = GenerateTone(samples, digit);
                MungeALaw(samples.AsSpan(0, length));
                receiver.Process(samples.AsSpan(0, length));
                if (receiver.CurrentDigit == digit)
                    positive++;
            }

            int negative = 0;
            for (int offset = -1; offset >= -60; offset--) {
                InitialiseGeneratedTones(offset / 1000.0f, -17, 0.0f, -17, 68, forward);
                int length = GenerateTone(samples, digit);
                MungeALaw(samples.AsSpan(0, length));
                receiver.Process(samples.AsSpan(0, length));
                if (receiver.CurrentDigit == digit)
                    negative++;
            }

            double rrb = (positive + negative) / 10.0;
            double rcfo = (positive - negative) / 10.0;
            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "    {0} (low)  rrb = {1,5:F2}%, rcfo = {2,5:F2}%, max -ve = {3,5:F2}, max +ve = {4,5:F2}",
                    digit,
                    rrb,
                    rcfo,
                    negative / 10.0,
                    positive / 10.0));

            // Preserve the original test's use of the forward-frequency table for
            // both the forward and backward test sets.
            if (rrb < rcfo + 2.0 * 100.0 * 14.0 / R2MfForwardTones[toneIndex].Frequency1 ||
                rrb >= 15.0 + rcfo) {
                Fail();
            }

            positive = 0;
            for (int offset = 1; offset <= 60; offset++) {
                InitialiseGeneratedTones(0.0f, -17, offset / 1000.0f, -17, 68, forward);
                int length = GenerateTone(samples, digit);
                MungeALaw(samples.AsSpan(0, length));
                receiver.Process(samples.AsSpan(0, length));
                if (receiver.CurrentDigit == digit)
                    positive++;
            }

            negative = 0;
            for (int offset = -1; offset >= -60; offset--) {
                InitialiseGeneratedTones(0.0f, -17, offset / 1000.0f, -17, 68, forward);
                int length = GenerateTone(samples, digit);
                MungeALaw(samples.AsSpan(0, length));
                receiver.Process(samples.AsSpan(0, length));
                if (receiver.CurrentDigit == digit)
                    negative++;
            }

            rrb = (positive + negative) / 10.0;
            rcfo = (positive - negative) / 10.0;
            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "    {0} (high) rrb = {1,5:F2}%, rcfo = {2,5:F2}%, max -ve = {3,5:F2}, max +ve = {4,5:F2}",
                    digit,
                    rrb,
                    rcfo,
                    negative / 10.0,
                    positive / 10.0));

            if (rrb < rcfo + 2.0 * 100.0 * 14.0 / R2MfForwardTones[toneIndex].Frequency2 ||
                rrb >= 15.0 + rcfo) {
                Fail();
            }

            toneIndex++;
        }
        Console.WriteLine("    Passed");

        Console.WriteLine("Test 4: Acceptable amplitude ratio (twist)");
        foreach (char digit in R2_MF_TONE_CODES) {
            int normalTwist = 0;
            for (int tenthDb = -50; tenthDb >= -250; tenthDb--) {
                InitialiseGeneratedTones(0.0f, -5, 0.0f, tenthDb / 10, 68, forward);
                int length = GenerateTone(samples, digit);
                MungeALaw(samples.AsSpan(0, length));
                receiver.Process(samples.AsSpan(0, length));
                if (receiver.CurrentDigit == digit)
                    normalTwist++;
            }

            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "    {0} normal twist  = {1:F2}dB",
                    digit,
                    normalTwist / 10.0));
            if (normalTwist < 70)
                Fail();

            int reverseTwist = 0;
            for (int tenthDb = -50; tenthDb >= -250; tenthDb--) {
                InitialiseGeneratedTones(0.0f, tenthDb / 10, 0.0f, -5, 68, forward);
                int length = GenerateTone(samples, digit);
                MungeALaw(samples.AsSpan(0, length));
                receiver.Process(samples.AsSpan(0, length));
                if (receiver.CurrentDigit == digit)
                    reverseTwist++;
            }

            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "    {0} reverse twist = {1:F2}dB",
                    digit,
                    reverseTwist / 10.0));
            if (reverseTwist < 70)
                Fail();
        }
        Console.WriteLine("    Passed");

        Console.WriteLine("Test 5: Dynamic range");
        int nplus = -1000;
        int nminus = -1000;
        for (int level = -50; level <= 3; level++) {
            int repeatCounter = 0;

            foreach (char digit in R2_MF_TONE_CODES) {
                InitialiseGeneratedTones(0.0f, level, 0.0f, level, 68, forward);

                for (repeatCounter = 0; repeatCounter < 100; repeatCounter++) {
                    int length = GenerateTone(samples, digit);
                    MungeALaw(samples.AsSpan(0, length));
                    receiver.Process(samples.AsSpan(0, length));
                    if (receiver.CurrentDigit != digit)
                        break;
                }

                if (repeatCounter < 100)
                    break;
            }

            if (repeatCounter == 100) {
                if (nplus == -1000)
                    nplus = level;
            } else if (nplus != -1000 && nminus == -1000) {
                nminus = level;
            }
        }

        Console.WriteLine($"    Dynamic range = {nplus}dB to {nminus - 1}dB");
        if (nplus > -35 || nminus <= -5)
            Fail();
        Console.WriteLine("    Passed");

        Console.WriteLine("Test 6: Guard time");
        int guardTime;
        for (guardTime = 30; guardTime < 62; guardTime++) {
            bool allPassed = true;
            foreach (char digit in R2_MF_TONE_CODES) {
                InitialiseGeneratedTones(0.0f, -5, 0.0f, -3, guardTime, forward);
                int repeatCounter;
                for (repeatCounter = 0; repeatCounter < 500; repeatCounter++) {
                    int length = GenerateTone(samples, digit);
                    MungeALaw(samples.AsSpan(0, length));
                    receiver.Process(samples.AsSpan(0, length));
                    if (receiver.CurrentDigit != digit)
                        break;
                }

                if (repeatCounter < 500) {
                    allPassed = false;
                    break;
                }
            }

            if (allPassed)
                break;
        }

        Console.WriteLine($"    Guard time = {guardTime}ms");
        if (guardTime > 61)
            Fail();
        Console.WriteLine("    Passed");

        Console.WriteLine("Test 7: Acceptable signal to noise ratio");
        InitialiseGeneratedTones(0.0f, -3, 0.0f, -3, 68, forward);
        int noiseLevel;
        for (noiseLevel = -3; noiseLevel > -50; noiseLevel--) {
            bool allPassed = true;
            foreach (char digit in R2_MF_TONE_CODES) {
                var noiseSource = new Awgn(1234567, noiseLevel);
                int repeatCounter;
                for (repeatCounter = 0; repeatCounter < 500; repeatCounter++) {
                    int length = GenerateTone(samples, digit);
                    for (int sample = 0; sample < length; sample++)
                        samples[sample] = SaturatingAdd(samples[sample], noiseSource.NextSample());

                    MungeALaw(samples.AsSpan(0, length));
                    receiver.Process(samples.AsSpan(0, length));
                    if (receiver.CurrentDigit != digit)
                        break;
                }

                if (repeatCounter < 500) {
                    allPassed = false;
                    break;
                }
            }

            if (allPassed)
                break;
        }

        Console.WriteLine($"    Acceptable S/N ratio is {-3 - noiseLevel}dB");
        if (-3 - noiseLevel > 26)
            Fail();
        Console.WriteLine("    Passed");

        Console.WriteLine("Test 8: Callback digit delivery mode.");
        _callbackOk = false;
        _callbackRoll = 0;
        receiver = new R2MfRx(forward, DigitDelivery, CallbackSentinel);
        InitialiseGeneratedTones(0.0f, -3, 0.0f, -3, 68, forward);
        var callbackNoise = new Awgn(1234567, -40.0);

        foreach (char digit in R2_MF_TONE_CODES) {
            int length = GenerateTone(samples, digit);
            for (int sample = 0; sample < length; sample++)
                samples[sample] = SaturatingAdd(samples[sample], callbackNoise.NextSample());

            MungeALaw(samples.AsSpan(0, length));
            receiver.Process(samples.AsSpan(0, length));

            length = 160;
            Array.Clear(samples, 0, length);
            for (int sample = 0; sample < length; sample++)
                samples[sample] = SaturatingAdd(samples[sample], callbackNoise.NextSample());

            MungeALaw(samples.AsSpan(0, length));
            receiver.Process(samples.AsSpan(0, length));
        }

        if (!_callbackOk)
            Fail();

        Console.WriteLine("    Passed");
        return 0;
    }

    private static void InitialiseGeneratedTones(
        float lowFudge,
        int lowLevel,
        float highFudge,
        int highLevel,
        int durationMilliseconds,
        bool forward) {
        ToneSpec[] source = forward ? R2MfForwardTones : R2MfBackwardTones;

        for (int index = 0; index < 15; index++) {
            // The native test passes the adjusted floating-point frequencies to
            // an integer parameter. The cast therefore truncates toward zero.
            int frequency1 = (int)((float)source[index].Frequency1 * (1.0f + lowFudge));
            int frequency2 = (int)((float)source[index].Frequency2 * (1.0f + highFudge));

            GeneratedTones[index] = tone_gen_descriptor_init(
                GeneratedTones[index],
                frequency1,
                lowLevel,
                frequency2,
                highLevel,
                durationMilliseconds,
                0,
                0,
                0,
                0) ?? throw new InvalidOperationException("Unable to initialise R2 tone descriptor.");
        }
    }

    private static int GenerateTone(short[] output, char digit) {
        int index = R2_MF_TONE_CODES.IndexOf(digit);
        if (index < 0)
            return 0;

        tone_gen_descriptor_t descriptor = GeneratedTones[index]
            ?? throw new InvalidOperationException("R2 tone descriptors are not initialised.");
        tone_gen_state_t state = tone_gen_init(null, descriptor)
            ?? throw new InvalidOperationException("Unable to initialise R2 tone generator.");

        // Match my_mf_generate() in the native FX test. tone_gen() performs the
        // unchecked int16 conversion used by spanDSP and intentionally does not
        // saturate overdriven multi-frequency samples.
        int length = tone_gen(state, output, Math.Min(9999, output.Length));
        tone_gen_free(state);
        return length;
    }

    private static void MungeALaw(Span<short> samples) {
        for (int index = 0; index < samples.Length; index++)
            samples[index] = alaw_to_linear(linear_to_alaw(samples[index]));
    }

    private static short SaturatingAdd(short left, short right) {
        int value = left + right;
        if (value > short.MaxValue)
            return short.MaxValue;
        if (value < short.MinValue)
            return short.MinValue;
        return (short)value;
    }

    private static void DigitDelivery(object? data, int digit, int level, int delay) {
        _ = level;
        _ = delay;

        if (!ReferenceEquals(data, CallbackSentinel)) {
            _callbackOk = false;
            return;
        }

        char expected = (_callbackRoll & 1) != 0
            ? '\0'
            : ToneCodeAt(_callbackRoll >> 1);

        _callbackOk = expected == (char)digit;

        if (ToneCodeAt(_callbackRoll >> 1) != '\0')
            _callbackRoll++;
        else
            _callbackOk = false;
    }

    private static char ToneCodeAt(int index) =>
        (uint)index < (uint)R2_MF_TONE_CODES.Length
            ? R2_MF_TONE_CODES[index]
            : '\0';

    private static void DecodeTest(string testFile) {
        var forwardReceiver = new R2MfRx(true, DigitDeliveryForward, CallbackSentinel);
        var backwardReceiver = new R2MfRx(false, DigitDeliveryBackward, CallbackSentinel);
        var samples = new short[SAMPLES_PER_CHUNK];

        try {
            using var input = Pcm16WaveReader.OpenMono8Khz(testFile);
            int sampleCount;
            while ((sampleCount = input.ReadFrames(samples)) > 0) {
                MungeALaw(samples.AsSpan(0, sampleCount));
                forwardReceiver.Process(samples.AsSpan(0, sampleCount));
                backwardReceiver.Process(samples.AsSpan(0, sampleCount));
            }
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) {
            Console.Error.WriteLine($"    Cannot open audio file '{testFile}': {ex.Message}");
            Exit(2);
        }
    }

    private static void DigitDeliveryForward(object? data, int digit, int level, int delay) {
        if (!ReferenceEquals(data, CallbackSentinel)) {
            _callbackOk = false;
            return;
        }

        char printable = digit == 0 ? '-' : (char)digit;
        Console.WriteLine($"FWD '{printable}' {level} {delay}");
    }

    private static void DigitDeliveryBackward(object? data, int digit, int level, int delay) {
        if (!ReferenceEquals(data, CallbackSentinel)) {
            _callbackOk = false;
            return;
        }

        char printable = digit == 0 ? '-' : (char)digit;
        Console.WriteLine($"BWD '{printable}' {level} {delay}");
    }

    private static void Fail() {
        Console.WriteLine("    Failed");
        Exit(2);
    }

    private static void Exit(int code) => throw new R2MfRxTestsAdapterExitException(code);

    private readonly record struct ToneSpec(double Frequency1, double Frequency2);

    public sealed class R2MfRxTestsAdapterExitException : Exception {
        public R2MfRxTestsAdapterExitException(int exitCode)
            : base($"r2_mf_rx_tests_adapter exited with code {exitCode}.") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }

    private sealed class Pcm16WaveReader : IDisposable {
        private readonly FileStream _stream;
        private readonly BinaryReader _reader;
        private readonly long _dataEnd;

        private Pcm16WaveReader(
            FileStream stream,
            BinaryReader reader,
            long dataStart,
            long dataLength) {
            _stream = stream;
            _reader = reader;
            _stream.Position = dataStart;
            _dataEnd = checked(dataStart + dataLength);
        }

        public static Pcm16WaveReader OpenMono8Khz(string path) {
            FileStream stream = File.OpenRead(path);
            var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

            try {
                if (ReadFourCc(reader) != "RIFF")
                    throw new InvalidDataException("RIFF header missing.");

                _ = reader.ReadUInt32();
                if (ReadFourCc(reader) != "WAVE")
                    throw new InvalidDataException("WAVE header missing.");

                ushort format = 0;
                ushort channels = 0;
                int sampleRate = 0;
                ushort bitsPerSample = 0;
                long dataStart = -1;
                uint dataLength = 0;

                while (stream.Position + 8 <= stream.Length) {
                    string chunkId = ReadFourCc(reader);
                    uint chunkLength = reader.ReadUInt32();
                    long nextChunk = checked(stream.Position + chunkLength + (chunkLength & 1));

                    if (chunkId == "fmt ") {
                        if (chunkLength < 16)
                            throw new InvalidDataException("Invalid fmt chunk.");

                        format = reader.ReadUInt16();
                        channels = reader.ReadUInt16();
                        sampleRate = reader.ReadInt32();
                        _ = reader.ReadUInt32();
                        _ = reader.ReadUInt16();
                        bitsPerSample = reader.ReadUInt16();
                    } else if (chunkId == "data") {
                        dataStart = stream.Position;
                        dataLength = chunkLength;
                    }

                    stream.Position = Math.Min(nextChunk, stream.Length);
                }

                if (format != 1 || channels != 1 || sampleRate != SAMPLE_RATE || bitsPerSample != 16) {
                    throw new InvalidDataException(
                        "The input must be mono, 8000 Hz, signed 16-bit PCM.");
                }

                if (dataStart < 0)
                    throw new InvalidDataException("WAVE data chunk missing.");

                return new Pcm16WaveReader(stream, reader, dataStart, dataLength);
            } catch {
                reader.Dispose();
                stream.Dispose();
                throw;
            }
        }

        public int ReadFrames(Span<short> destination) {
            int count = 0;
            while (count < destination.Length && _stream.Position + 2 <= _dataEnd)
                destination[count++] = _reader.ReadInt16();
            return count;
        }

        public void Dispose() {
            _reader.Dispose();
            _stream.Dispose();
        }

        private static string ReadFourCc(BinaryReader reader) =>
            Encoding.ASCII.GetString(reader.ReadBytes(4));
    }
}
