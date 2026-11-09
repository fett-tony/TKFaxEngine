/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * time_scale_tests.c
 *
 * Managed C# port preserving the supplied spanDSP time-scaling test.
 */

#nullable enable

using System.Globalization;
using TKFaxEngineTest.System;
using TKFaxEngine.Audio;
using static TKFaxEngine.Audio.TimeScaleApi;

namespace TKFaxEngineTest.Audio.Test;

public static class TimeScaleTests {
    private const int BLOCK_LEN = 160;
    private const string IN_FILE_NAME = "Test-Files/local/short_nb_voice.wav";
    private const string OUT_FILE_NAME = "time_scale_result.wav";
    private const int SAMPLE_RATE = 8000;

    public sealed class ExitException : Exception {
        public ExitException(int exitCode) => ExitCode = exitCode;
        public int ExitCode { get; }
    }

    private static void Exit(int code) => throw new ExitException(code);

    public static int tkfx_time_scale_tests_body_main(string[] testArguments) {
        float rate = 1.8f;
        bool sweepRate = false;
        string inputFileName = IN_FILE_NAME;

        int argumentCount = testArguments.Length;
        for (int index = 0; index < argumentCount; index++) {
            string argument = testArguments[index];
            switch (argument) {
                case "-i":
                    if (++index >= argumentCount)
                        Exit(2);
                    inputFileName = testArguments[index];
                    break;
                case "-r":
                    if (++index >= argumentCount ||
                        !float.TryParse(
                            testArguments[index],
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out rate)) {
                        Exit(2);
                    }
                    break;
                case "-s":
                    sweepRate = true;
                    break;
                default:
                    Exit(2);
                    break;
            }
        }

        Pcm16WaveFile input;
        try {
            input = Pcm16WaveFile.OpenRead(inputFileName);
        } catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException) {
            Console.WriteLine($"    Cannot open audio file '{inputFileName}'");
            return 2;
        }

        using (input) {
            if (input.Channels != 1) {
                Console.WriteLine($"    Unexpected number of channels in audio file '{inputFileName}'");
                return 2;
            }

            int sampleRate = input.SampleRate;
            Pcm16WaveFile output;
            try {
                output = Pcm16WaveFile.OpenWrite(OUT_FILE_NAME, sampleRate, 1);
            } catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or ArgumentException) {
                Console.Error.WriteLine($"    Cannot create audio file '{OUT_FILE_NAME}'");
                return 2;
            }

            using (output) {
                TimeScaleState? state = time_scale_init(null, sampleRate, rate);
                if (state is null) {
                    Console.Error.WriteLine("    Cannot start the time scaler");
                    return 2;
                }

                short[] inputSamples = new short[BLOCK_LEN];
                short[] outputSamples = new short[
                    5 * (BLOCK_LEN +
                         TimeScaleState.MaximumSampleRate /
                         TimeScaleState.MinimumPitchHz)];

                int maximum = time_scale_max_output_len(state, BLOCK_LEN);
                Console.WriteLine($"Rate is {rate:F6}, longest output block is {maximum}");

                int count = 0;
                int samplesIn = 0;
                int samplesOut = 0;
                int frames;
                while ((frames = input.ReadFrames(inputSamples, BLOCK_LEN)) != 0) {
                    samplesIn += frames;
                    int newFrames = time_scale(state, outputSamples, inputSamples, frames);
                    if (newFrames > maximum) {
                        Console.WriteLine(
                            "Generated signal has more than the expected maximum samples - " +
                            $"{newFrames} vs {maximum}");
                        Console.WriteLine("Tests failed");
                        time_scale_free(state);
                        Exit(2);
                    }
                    if (newFrames < 0) {
                        Console.WriteLine("The time scaler returned an error");
                        Console.WriteLine("Tests failed");
                        time_scale_free(state);
                        Exit(2);
                    }

                    samplesOut += newFrames;
                    int outputFrames;
                    try {
                        outputFrames = output.WriteFrames(outputSamples.AsSpan(0, newFrames));
                    } catch (IOException) {
                        Console.Error.WriteLine("    Error writing audio file");
                        time_scale_free(state);
                        Exit(2);
                        return 2;
                    }

                    if (outputFrames != newFrames) {
                        Console.Error.WriteLine("    Error writing audio file");
                        time_scale_free(state);
                        Exit(2);
                    }

                    if (sweepRate && ++count > 100) {
                        if (rate > 0.5f) {
                            rate -= 0.1f;
                            if (rate >= 0.99f && rate <= 1.01f)
                                rate -= 0.1f;

                            if (time_scale_init(state, SAMPLE_RATE, rate) is null) {
                                Console.Error.WriteLine("    Cannot restart the time scaler");
                                time_scale_free(state);
                                Exit(2);
                            }

                            maximum = time_scale_max_output_len(state, BLOCK_LEN);
                            Console.WriteLine(
                                $"Rate is {rate:F6}, longest output block is {maximum}");
                        }
                        count = 0;
                    }
                }

                int flushedFrames = time_scale_flush(state, outputSamples);
                if (flushedFrames > maximum) {
                    Console.WriteLine(
                        "Generated signal has more than the expected maximum samples - " +
                        $"{flushedFrames} vs {maximum}");
                    Console.WriteLine("Tests failed");
                    time_scale_free(state);
                    Exit(2);
                }
                if (flushedFrames < 0) {
                    Console.WriteLine("The time scaler flush returned an error");
                    Console.WriteLine("Tests failed");
                    time_scale_free(state);
                    Exit(2);
                }

                samplesOut += flushedFrames;
                int flushedOutputFrames;
                try {
                    flushedOutputFrames = output.WriteFrames(
                        outputSamples.AsSpan(0, flushedFrames));
                } catch (IOException) {
                    Console.Error.WriteLine("    Error writing audio file");
                    time_scale_free(state);
                    Exit(2);
                    return 2;
                }

                if (flushedOutputFrames != flushedFrames) {
                    Console.Error.WriteLine("    Error writing audio file");
                    time_scale_free(state);
                    Exit(2);
                }

                time_scale_release(state);
                int expectedSamples = (int)(rate * samplesIn);
                if (expectedSamples < samplesOut - 1 || expectedSamples > samplesOut + 1) {
                    Console.WriteLine($"{expectedSamples} samples became {samplesOut} samples");
                    Console.WriteLine("Tests failed");
                    time_scale_free(state);
                    Exit(2);
                }

                time_scale_free(state);
            }
        }

        return 0;
    }
}
