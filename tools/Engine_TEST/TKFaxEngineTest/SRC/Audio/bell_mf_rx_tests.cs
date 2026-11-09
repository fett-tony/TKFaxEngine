/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * bell_mf_rx_tests.cs - Bell MF receiver conformance tests.
 *
 * Original C test written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2003 Steve Underwood
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License version 2, as
 * published by the Free Software Foundation.
 *
 * Managed C# port. Tests 1 through 7, all 15 Bell MF symbols, the original
 * frequency/twist/dynamic-range/guard-time sweeps, 500-pass load loops,
 * AWGN stress, callback delivery and WAV decode mode are retained.
 */

#nullable enable

using System.Diagnostics;
using System.Globalization;
using System.Text;
using TKFaxEngine.Audio;
using static TKFaxEngine.Audio.tone_generate;

namespace TKFaxEngineTest.Audio.Test;

public static class bell_mf_rx_tests {
    private const int SampleRate = 8000;
    private const int SamplesPerChunk = 160;
    private const int MaximumBellMfDigits = 128;
    private const string AllPossibleDigits = "1234567890CA*B#";

    private static readonly MfDigitTone[] BellMfTones =
    [
        new( 700.0f,  900.0f, -7, -7,  68, 68),
        new( 700.0f, 1100.0f, -7, -7,  68, 68),
        new( 900.0f, 1100.0f, -7, -7,  68, 68),
        new( 700.0f, 1300.0f, -7, -7,  68, 68),
        new( 900.0f, 1300.0f, -7, -7,  68, 68),
        new(1100.0f, 1300.0f, -7, -7,  68, 68),
        new( 700.0f, 1500.0f, -7, -7,  68, 68),
        new( 900.0f, 1500.0f, -7, -7,  68, 68),
        new(1100.0f, 1500.0f, -7, -7,  68, 68),
        new(1300.0f, 1500.0f, -7, -7,  68, 68),
        new( 700.0f, 1700.0f, -7, -7,  68, 68),
        new( 900.0f, 1700.0f, -7, -7,  68, 68),
        new(1100.0f, 1700.0f, -7, -7, 100, 68),
        new(1300.0f, 1700.0f, -7, -7,  68, 68),
        new(1500.0f, 1700.0f, -7, -7,  68, 68),
        new(   0.0f,    0.0f,  0,  0,   0,  0)
    ];

    private static readonly tone_gen_descriptor_t?[] MyMfDigitTones =
        new tone_gen_descriptor_t?[16];

    private static readonly short[] Amp = new short[1_000_000];
    private static readonly object CallbackToken = new();

    private static bool callback_ok;
    private static int callback_roll;
    private static string? decode_test_file;

    public static int main(string[] testArguments) {
        try {
            return tkfx_bell_mf_rx_tests_body_main(testArguments);
        } catch (BellMfRxTestExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_bell_mf_rx_tests_body_main(string[] testArguments) {
        decode_test_file = ParseDecodeFile(testArguments);

        if (decode_test_file is not null) {
            DecodeTest(decode_test_file);
            return 0;
        }

        var stopwatch = Stopwatch.StartNew();
        _ = TestToneSet();
        stopwatch.Stop();

        long duration = (long)stopwatch.Elapsed.TotalSeconds;
        Console.WriteLine($"Tests passed in {duration}s");
        return 0;
    }

    private static int TestToneSet() {
        var mfState = new BellMfRx();

        Console.WriteLine("Test 1: Calibration");
        Console.WriteLine("    Passed");

        Console.WriteLine("Test 2: Decode check");
        MyMfGenInit(0.0f, -3, 0.0f, -3, 68, 68);

        foreach (char digit in AllPossibleDigits) {
            string oneDigit = digit.ToString();
            for (int i = 0; i < 10; i++) {
                int length = MyMfGenerate(Amp, 0, oneDigit);
                CodecMunge(Amp.AsSpan(0, length));
                mfState.Process(Amp.AsSpan(0, length));
                string received = mfState.GetDigits(MaximumBellMfDigits);

                if (received.Length != 1 || received[0] != digit) {
                    Console.WriteLine($"    Sent     '{oneDigit}'");
                    Console.WriteLine($"    Received '{received}' [{received.Length}]");
                    Fail("    Failed");
                }
            }
        }

        Console.WriteLine("    Passed");

        Console.WriteLine("Test 3: Recognition bandwidth and channel centre frequency check");
        int toneIndex = 0;

        foreach (char digit in AllPossibleDigits) {
            string oneDigit = digit.ToString();
            int nplus = 0;
            int nminus = 0;

            for (int i = 1; i <= 60; i++) {
                MyMfGenInit(i / 1000.0f, -17, 0.0f, -17, 68, 68);
                int length = MyMfGenerate(Amp, 0, oneDigit);
                CodecMunge(Amp.AsSpan(0, length));
                mfState.Process(Amp.AsSpan(0, length));
                nplus += mfState.GetDigits(MaximumBellMfDigits).Length;
            }

            for (int i = -1; i >= -60; i--) {
                MyMfGenInit(i / 1000.0f, -17, 0.0f, -17, 68, 68);
                int length = MyMfGenerate(Amp, 0, oneDigit);
                CodecMunge(Amp.AsSpan(0, length));
                mfState.Process(Amp.AsSpan(0, length));
                nminus += mfState.GetDigits(MaximumBellMfDigits).Length;
            }

            float rrb = (nplus + nminus) / 10.0f;
            float rcfo = (nplus - nminus) / 10.0f;

            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "    {0} (low)  rrb = {1,5:F2}%, rcfo = {2,5:F2}%, max -ve = {3,5:F2}, max +ve = {4,5:F2}",
                    digit,
                    rrb,
                    rcfo,
                    nminus / 10.0f,
                    nplus / 10.0f));

            if (rrb < 3.0f + rcfo + (2.0f * 100.0f * 10.0f / (float)BellMfTones[toneIndex].Frequency1) ||
                rrb >= 15.0f + rcfo) {
                Fail("    Failed");
            }

            nplus = 0;
            nminus = 0;

            for (int i = 1; i <= 60; i++) {
                MyMfGenInit(0.0f, -17, i / 1000.0f, -17, 68, 68);
                int length = MyMfGenerate(Amp, 0, oneDigit);
                CodecMunge(Amp.AsSpan(0, length));
                mfState.Process(Amp.AsSpan(0, length));
                nplus += mfState.GetDigits(MaximumBellMfDigits).Length;
            }

            for (int i = -1; i >= -60; i--) {
                MyMfGenInit(0.0f, -17, i / 1000.0f, -17, 68, 68);
                int length = MyMfGenerate(Amp, 0, oneDigit);
                CodecMunge(Amp.AsSpan(0, length));
                mfState.Process(Amp.AsSpan(0, length));
                nminus += mfState.GetDigits(MaximumBellMfDigits).Length;
            }

            rrb = (nplus + nminus) / 10.0f;
            rcfo = (nplus - nminus) / 10.0f;

            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "    {0} (high) rrb = {1,5:F2}%, rcfo = {2,5:F2}%, max -ve = {3,5:F2}, max +ve = {4,5:F2}",
                    digit,
                    rrb,
                    rcfo,
                    nminus / 10.0f,
                    nplus / 10.0f));

            if (rrb < 3.0f + rcfo + (2.0f * 100.0f * 10.0f / (float)BellMfTones[toneIndex].Frequency2) ||
                rrb >= 15.0f + rcfo) {
                Fail("    Failed");
            }

            toneIndex++;
        }

        Console.WriteLine("    Passed");

        Console.WriteLine("Test 4: Acceptable amplitude ratio (twist)");

        foreach (char digit in AllPossibleDigits) {
            string oneDigit = digit.ToString();
            int nplus = 0;

            for (int level = -50; level >= -250; level--) {
                MyMfGenInit(0.0f, -5, 0.0f, level / 10, 68, 68);
                int length = MyMfGenerate(Amp, 0, oneDigit);
                CodecMunge(Amp.AsSpan(0, length));
                mfState.Process(Amp.AsSpan(0, length));
                nplus += mfState.GetDigits(MaximumBellMfDigits).Length;
            }

            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "    {0} normal twist  = {1:F2}dB",
                    digit,
                    nplus / 10.0f));

            if (nplus < 60)
                Fail("    Failed");

            int nminus = 0;
            for (int level = -50; level >= -250; level--) {
                MyMfGenInit(0.0f, level / 10, 0.0f, -5, 68, 68);
                int length = MyMfGenerate(Amp, 0, oneDigit);
                CodecMunge(Amp.AsSpan(0, length));
                mfState.Process(Amp.AsSpan(0, length));
                nminus += mfState.GetDigits(MaximumBellMfDigits).Length;
            }

            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "    {0} reverse twist = {1:F2}dB",
                    digit,
                    nminus / 10.0f));

            if (nminus < 60)
                Fail("    Failed");
        }

        Console.WriteLine("    Passed");

        Console.WriteLine("Test 5: Dynamic range");
        int dynamicRangeLow = -1000;
        int dynamicRangeHigh = -1000;

        for (int level = -50; level <= 3; level++) {
            MyMfGenInit(0.0f, level, 0.0f, level, 68, 68);
            int pass;

            for (pass = 0; pass < 100; pass++) {
                int length = MyMfGenerate(Amp, 0, AllPossibleDigits);
                CodecMunge(Amp.AsSpan(0, length));
                mfState.Process(Amp.AsSpan(0, length));
                string received = mfState.GetDigits(MaximumBellMfDigits);

                if (received.Length != 15)
                    break;

                if (!string.Equals(received, AllPossibleDigits, StringComparison.Ordinal))
                    break;
            }

            if (pass == 100) {
                if (dynamicRangeLow == -1000)
                    dynamicRangeLow = level;
            } else if (dynamicRangeLow != -1000 && dynamicRangeHigh == -1000) {
                dynamicRangeHigh = level;
            }
        }

        Console.WriteLine(
            $"    Dynamic range = {dynamicRangeLow}dB to {dynamicRangeHigh - 1}dB");

        if (dynamicRangeLow > -22 || dynamicRangeHigh <= -3)
            Fail("    Failed");

        Console.WriteLine("    Passed");

        Console.WriteLine("Test 6: Guard time");
        int guardTime;

        for (guardTime = 30; guardTime < 62; guardTime++) {
            MyMfGenInit(0.0f, -5, 0.0f, -3, guardTime, 68);
            int pass;

            for (pass = 0; pass < 500; pass++) {
                int length = MyMfGenerate(Amp, 0, AllPossibleDigits);
                CodecMunge(Amp.AsSpan(0, length));
                mfState.Process(Amp.AsSpan(0, length));
                string received = mfState.GetDigits(MaximumBellMfDigits);

                if (received.Length != 15)
                    break;

                if (!string.Equals(received, AllPossibleDigits, StringComparison.Ordinal))
                    break;
            }

            if (pass == 500)
                break;
        }

        Console.WriteLine($"    Guard time = {guardTime}ms");

        if (guardTime > 61)
            Fail("    Failed");

        Console.WriteLine("    Passed");

        Console.WriteLine("Test 7: Acceptable signal to noise ratio");
        MyMfGenInit(0.0f, -3, 0.0f, -3, 68, 68);

        int noiseLevel;
        for (noiseLevel = -10; noiseLevel > -50; noiseLevel--) {
            var noiseSource = new Awgn(1234567, noiseLevel);
            int pass;

            for (pass = 0; pass < 500; pass++) {
                int length = MyMfGenerate(Amp, 0, AllPossibleDigits);

                for (int sample = 0; sample < length; sample++)
                    Amp[sample] = SaturatingAdd(Amp[sample], noiseSource.NextSample());

                CodecMunge(Amp.AsSpan(0, length));
                mfState.Process(Amp.AsSpan(0, length));
                string received = mfState.GetDigits(MaximumBellMfDigits);

                if (received.Length != 15)
                    break;

                if (!string.Equals(received, AllPossibleDigits, StringComparison.Ordinal))
                    break;
            }

            if (pass == 500)
                break;
        }

        Console.WriteLine($"    Acceptable S/N ratio is {-3 - noiseLevel}dB");

        if (-3 - noiseLevel > 26)
            Fail("    Failed");

        Console.WriteLine("    Passed");

        Console.WriteLine("Test: Callback digit delivery mode.");
        callback_ok = false;
        callback_roll = 0;

        mfState = new BellMfRx(DigitDelivery, CallbackToken);
        MyMfGenInit(0.0f, -10, 0.0f, -10, 68, 68);

        for (int repeatCount = 1; repeatCount < 10; repeatCount++) {
            int length = 0;
            for (int repetition = 0; repetition < repeatCount; repetition++)
                length += MyMfGenerate(Amp, length, AllPossibleDigits);

            mfState.Process(Amp.AsSpan(0, length));
            if (!callback_ok)
                break;
        }

        if (!callback_ok)
            Fail("    Failed");

        Console.WriteLine("    Passed");
        return 0;
    }

    private static void MyMfGenInit(
        float lowFudge,
        int lowLevel,
        float highFudge,
        int highLevel,
        int duration,
        int gap) {
        for (int i = 0; i < 15; i++) {
            MyMfDigitTones[i] = tone_gen_descriptor_init(
                MyMfDigitTones[i],
                (int)(BellMfTones[i].Frequency1 * (1.0f + lowFudge)),
                lowLevel,
                (int)(BellMfTones[i].Frequency2 * (1.0f + highFudge)),
                highLevel,
                i == 12 ? 3 * duration / 2 : duration,
                gap,
                0,
                0,
                0);
        }
    }

    private static int MyMfGenerate(short[] destination, int offset, string digits) {
        int length = 0;
        tone_gen_state_t tone = new();

        foreach (char digit in digits) {
            int toneIndex = AllPossibleDigits.IndexOf(digit);
            if (toneIndex >= 0) {
                tone_gen_init(tone, MyMfDigitTones[toneIndex]!);
                length += tone_gen(tone, destination.AsSpan(offset + length), 9999);
            }
        }

        return length;
    }

    private static short SaturatingAdd(short left, short right) {
        int result = left + right;
        if (result > short.MaxValue)
            return short.MaxValue;
        if (result < short.MinValue)
            return short.MinValue;

        return (short)result;
    }

    private static void CodecMunge(Span<short> samples) {
        for (int i = 0; i < samples.Length; i++)
            samples[i] = g711.ulaw_to_linear(g711.linear_to_ulaw(samples[i]));
    }

    private static void DigitDelivery(object? data, string digits) {
        if (!ReferenceEquals(data, CallbackToken)) {
            callback_ok = false;
            return;
        }

        callback_ok = true;
        int expectedIndex = callback_roll;
        int segmentLength = 15 - callback_roll;

        for (int i = 0; i < digits.Length; i += segmentLength, segmentLength = 15) {
            if (i + segmentLength > digits.Length)
                segmentLength = digits.Length - i;

            ReadOnlySpan<char> actualSegment = digits.AsSpan(i, segmentLength);
            ReadOnlySpan<char> expectedSegment =
                AllPossibleDigits.AsSpan(expectedIndex, segmentLength);

            if (!actualSegment.SequenceEqual(expectedSegment)) {
                callback_ok = false;
                Console.WriteLine($"Fail at {i} {segmentLength}");
                break;
            }

            expectedIndex = 0;
            callback_roll = (callback_roll + segmentLength) % 15;
        }
    }

    private static void DigitDeliveryDecode(object? data, string digits) {
        if (!ReferenceEquals(data, CallbackToken))
            return;

        foreach (char digit in digits)
            Console.WriteLine($"Digit '{digit}'");
    }

    private static void DecodeTest(string testFile) {
        short[] samples = new short[SamplesPerChunk];
        var mfState = new BellMfRx(DigitDeliveryDecode, CallbackToken);

        using var input = Pcm16WaveReader.OpenMono(testFile);
        int count;

        while ((count = input.Read(samples)) > 0) {
            CodecMunge(samples.AsSpan(0, count));
            mfState.Process(samples.AsSpan(0, count));
        }
    }

    private static string? ParseDecodeFile(string[] testArguments) {
        int argumentCount = testArguments.Length;

        for (int i = 0; i < argumentCount; i++) {
            string argument = testArguments[i];

            if (string.Equals(argument, "-d", StringComparison.Ordinal)) {
                if (++i >= argumentCount)
                    Fail("Missing file name after -d.");

                return testArguments[i];
            }

            if (argument.StartsWith("-d", StringComparison.Ordinal) &&
                argument.Length > 2) {
                return argument[2..];
            }

            if (argument.StartsWith("-", StringComparison.Ordinal))
                Fail($"Unknown option '{argument}'.");
        }

        return null;
    }

    private static void Fail(string message) {
        Console.WriteLine(message);
        throw new BellMfRxTestExitException(2);
    }

    private readonly record struct MfDigitTone(
        float Frequency1,
        float Frequency2,
        sbyte Level1Dbm0,
        sbyte Level2Dbm0,
        byte OnTimeMilliseconds,
        byte OffTimeMilliseconds);

    public sealed class BellMfRxTestExitException : Exception {
        public BellMfRxTestExitException(int exitCode)
            : base($"bell_mf_rx_tests requested exit code {exitCode}.") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }

    private sealed class Pcm16WaveReader : IDisposable {
        private readonly FileStream stream;
        private readonly BinaryReader reader;
        private readonly long dataEnd;
        private readonly int channels;

        private Pcm16WaveReader(
            FileStream stream,
            BinaryReader reader,
            long dataEnd,
            int channels) {
            this.stream = stream;
            this.reader = reader;
            this.dataEnd = dataEnd;
            this.channels = channels;
        }

        public static Pcm16WaveReader OpenMono(string path) {
            var stream = File.OpenRead(path);
            var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

            try {
                if (ReadFourCc(reader) != "RIFF")
                    throw new InvalidDataException($"'{path}' is not a RIFF file.");

                _ = reader.ReadUInt32();

                if (ReadFourCc(reader) != "WAVE")
                    throw new InvalidDataException($"'{path}' is not a WAVE file.");

                ushort format = 0;
                ushort channels = 0;
                uint sampleRate = 0;
                ushort bitsPerSample = 0;
                long dataStart = -1;
                uint dataLength = 0;

                while (stream.Position + 8 <= stream.Length) {
                    string chunkId = ReadFourCc(reader);
                    uint chunkLength = reader.ReadUInt32();
                    long nextChunk = checked(stream.Position + chunkLength + (chunkLength & 1));

                    if (chunkId == "fmt ") {
                        format = reader.ReadUInt16();
                        channels = reader.ReadUInt16();
                        sampleRate = reader.ReadUInt32();
                        _ = reader.ReadUInt32();
                        _ = reader.ReadUInt16();
                        bitsPerSample = reader.ReadUInt16();
                    } else if (chunkId == "data") {
                        dataStart = stream.Position;
                        dataLength = chunkLength;
                        break;
                    }

                    stream.Position = nextChunk;
                }

                if (format != 1 || bitsPerSample != 16 || channels == 0)
                    throw new InvalidDataException($"'{path}' must be PCM16 WAV.");

                if (sampleRate != SampleRate)
                    throw new InvalidDataException($"'{path}' must use an 8000 Hz sample rate.");

                if (dataStart < 0)
                    throw new InvalidDataException($"'{path}' contains no data chunk.");

                stream.Position = dataStart;
                return new Pcm16WaveReader(
                    stream,
                    reader,
                    checked(dataStart + dataLength),
                    channels);
            } catch {
                reader.Dispose();
                stream.Dispose();
                throw;
            }
        }

        public int Read(Span<short> destination) {
            int frames = 0;

            while (frames < destination.Length &&
                   stream.Position + channels * sizeof(short) <= dataEnd) {
                destination[frames++] = reader.ReadInt16();

                for (int channel = 1; channel < channels; channel++)
                    _ = reader.ReadInt16();
            }

            return frames;
        }

        public void Dispose() {
            reader.Dispose();
            stream.Dispose();
        }

        private static string ReadFourCc(BinaryReader reader) =>
            Encoding.ASCII.GetString(reader.ReadBytes(4));
    }
}
