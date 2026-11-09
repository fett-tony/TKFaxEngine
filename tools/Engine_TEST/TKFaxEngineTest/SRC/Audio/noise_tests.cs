/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * noise_tests.c
 *
 * Written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2005 Steve Underwood.
 * GNU General Public License version 2.
 *
 * Managed C# port preserving the supplied spanDSP test order, sample counts,
 * statistical checks and generated audio output.
 */

#nullable enable

using System.Text;
using TKFaxEngine.Audio;
using TKFaxEngineTest;
using static TKFaxEngine.Audio.Noise;

namespace TKFaxEngineTest.Audio.Test;

public static class NoiseTests {
    private const string OUT_FILE_NAME = "noise.wav";
    private const double M_PI = 3.14159265358979323846;

    private static void Exit(int code) => throw new TestProcessExitException(code);

    public static int tkfx_noise_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        const int seed = 1_234_567;
        int[] bins = new int[65_536];
        short[] amplitude = new short[1_024];
        NoiseState? noiseSource = null;

        TelephonyWaveWriter output;
        try {
            output = new TelephonyWaveWriter(OUT_FILE_NAME, 8_000);
        } catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine($"    Cannot create audio file '{OUT_FILE_NAME}'");
            Exit(2);
            return 2;
        }

        try {
            for (int quality = 7; quality <= 20; quality += 20 - 7) {
                Console.WriteLine($"Testing AWGN power, with quality {quality}");
                for (int level = -50; level <= 0; level += 5) {
                    int clipHigh = 0;
                    int clipLow = 0;
                    double total = 0.0;
                    noiseSource = noise_init_dbov(
                        noiseSource,
                        seed,
                        level,
                        NOISE_CLASS_AWGN,
                        quality);

                    const int totalSamples = 1_000_000;
                    for (int i = 0; i < totalSamples; i++) {
                        short value = noise(noiseSource);
                        if (value == short.MaxValue)
                            clipHigh++;
                        else if (value == short.MinValue)
                            clipLow++;

                        total += (double)value * value;
                    }

                    double measuredLevel = 10.0 * Math.Log10(
                        (total / totalSamples) / (32_768.0 * 32_768.0) + 1.0e-10);
                    double error = 100.0 * (
                        1.0 -
                        Math.Sqrt(total / totalSamples) /
                        (Math.Pow(10.0, level / 20.0) * 32_768.0));
                    Console.WriteLine(
                        $"RMS = {measuredLevel:F3} (expected {level}) {error:F2}% error " +
                        $"[clipped samples {clipLow}+{clipHigh}]");

                    if (level < -5 && Math.Abs(measuredLevel - level) > 0.2) {
                        Console.WriteLine("Test failed");
                        Exit(2);
                    }
                }
            }

            const int statisticalQuality = 7;
            Console.WriteLine(
                $"Testing the statistical spread of AWGN, with quality {statisticalQuality}");
            Array.Clear(bins, 0, bins.Length);
            const int statisticalLevel = -15;
            noiseSource = noise_init_dbov(
                noiseSource,
                seed,
                statisticalLevel,
                NOISE_CLASS_AWGN,
                statisticalQuality);

            const int statisticalSamples = 10_000_000;
            for (int i = 0; i < statisticalSamples; i++) {
                short value = noise(noiseSource);
                bins[value + 32_768]++;
            }

            double expectedRms = Math.Pow(10.0, statisticalLevel / 20.0) *
                                 (32_768.0 * 0.70711);
            for (int i = 0; i < 65_536 - 10; i++) {
                double sampleValue = i - 32_768;
                double probability =
                    (1.0 / (expectedRms * Math.Sqrt(2.0 * M_PI))) *
                    Math.Exp(
                        -(sampleValue * sampleValue) /
                        (2.0 * expectedRms * expectedRms));

                double observed = 0.0;
                for (int j = 0; j < 10; j++)
                    observed += bins[i + j];
                observed /= 10.0;
                observed /= statisticalSamples;

                if (probability > 0.0000001) {
                    Console.WriteLine(
                        $"{i - 32_768,6} {observed:F7} {probability:F7}");
                }
            }

            Console.WriteLine("Generating AWGN at -15dBOv to file");
            for (int j = 0; j < 50; j++) {
                for (int i = 0; i < amplitude.Length; i++)
                    amplitude[i] = noise(noiseSource);

                int outputFrames = output.WriteFrames(amplitude, amplitude.Length);
                if (outputFrames != amplitude.Length) {
                    Console.Error.WriteLine("    Error writing audio file");
                    Exit(2);
                }
            }

            const int hothQuality = 7;
            Console.WriteLine($"Testing Hoth noise power, with quality {hothQuality}");
            for (int level = -50; level <= 0; level += 5) {
                int clipHigh = 0;
                int clipLow = 0;
                double total = 0.0;
                noiseSource = noise_init_dbov(
                    noiseSource,
                    seed,
                    level,
                    NOISE_CLASS_HOTH,
                    hothQuality);

                const int totalSamples = 1_000_000;
                for (int i = 0; i < totalSamples; i++) {
                    short value = noise(noiseSource);
                    if (value == short.MaxValue)
                        clipHigh++;
                    else if (value == short.MinValue)
                        clipLow++;

                    total += (double)value * value;
                }

                double measuredLevel = 10.0 * Math.Log10(
                    (total / totalSamples) / (32_768.0 * 32_768.0) + 1.0e-10);
                double error = 100.0 * (
                    1.0 -
                    Math.Sqrt(total / totalSamples) /
                    (Math.Pow(10.0, level / 20.0) * 32_768.0));
                Console.WriteLine(
                    $"RMS = {measuredLevel:F3} (expected {level}) {error:F2}% error " +
                    $"[clipped samples {clipLow}+{clipHigh}]");

                if (level < -5 && Math.Abs(measuredLevel - level) > 0.2) {
                    Console.WriteLine("Test failed");
                    Exit(2);
                }
            }

            Console.WriteLine("Generating Hoth noise at -15dBOv to file");
            noiseSource = noise_init_dbov(
                noiseSource,
                seed,
                -15,
                NOISE_CLASS_HOTH,
                hothQuality);
            for (int j = 0; j < 50; j++) {
                for (int i = 0; i < amplitude.Length; i++)
                    amplitude[i] = noise(noiseSource);

                int outputFrames = output.WriteFrames(amplitude, amplitude.Length);
                if (outputFrames != amplitude.Length) {
                    Console.Error.WriteLine("    Error writing audio file");
                    Exit(2);
                }
            }

            if (output.Close() != 0) {
                Console.Error.WriteLine($"    Cannot close audio file '{OUT_FILE_NAME}'");
                Exit(2);
            }

            Console.WriteLine("Tests passed.");
            return 0;
        } finally {
            output.Dispose();
            noise_free(noiseSource);
        }
    }

    private sealed class TelephonyWaveWriter : IDisposable {
        private readonly FileStream _stream;
        private readonly BinaryWriter _writer;
        private uint _dataLength;
        private bool _closed;

        public TelephonyWaveWriter(string path, int sampleRate) {
            _stream = File.Create(path);
            _writer = new BinaryWriter(_stream, Encoding.ASCII, leaveOpen: true);
            WriteFourCc("RIFF");
            _writer.Write(0U);
            WriteFourCc("WAVE");
            WriteFourCc("fmt ");
            _writer.Write(16U);
            _writer.Write((ushort)1);
            _writer.Write((ushort)1);
            _writer.Write((uint)sampleRate);
            _writer.Write((uint)(sampleRate * 2));
            _writer.Write((ushort)2);
            _writer.Write((ushort)16);
            WriteFourCc("data");
            _writer.Write(0U);
        }

        public int WriteFrames(short[] source, int frames) {
            if (_closed)
                throw new ObjectDisposedException(nameof(TelephonyWaveWriter));
            if (frames < 0 || frames > source.Length)
                throw new ArgumentOutOfRangeException(nameof(frames));

            for (int i = 0; i < frames; i++)
                _writer.Write(source[i]);
            _dataLength = checked(_dataLength + (uint)(frames * sizeof(short)));
            return frames;
        }

        public int Close() {
            if (_closed)
                return 0;

            try {
                _writer.Flush();
                _stream.Position = 4;
                _writer.Write(checked(36U + _dataLength));
                _stream.Position = 40;
                _writer.Write(_dataLength);
                _writer.Flush();
                _closed = true;
                _writer.Dispose();
                _stream.Dispose();
                return 0;
            } catch (IOException) {
                return -1;
            }
        }

        public void Dispose() {
            if (_closed)
                return;

            _ = Close();
        }

        private void WriteFourCc(string value) {
            _writer.Write(Encoding.ASCII.GetBytes(value));
        }
    }
}
