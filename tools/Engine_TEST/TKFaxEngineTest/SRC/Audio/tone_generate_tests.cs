/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * tone_generate_tests.c
 *
 * Original test written by Steve Underwood <steveu@coppice.org>.
 * Managed C# port preserving all eight spanDSP tone-generation passes.
 */

#nullable enable

using System.Text;
using TKFaxEngine.Audio;
using static TKFaxEngine.Audio.tone_generate;

namespace TKFaxEngineTest.Audio.Test;

public static class ToneGenerateTests {
    private const string OUTPUT_FILE_NAME = "tone_generate.wav";
    private const int SAMPLES_PER_CHUNK = 160;
    private const int SAMPLE_RATE = 8000;

    public sealed class ExitException : Exception {
        public ExitException(int exitCode) => ExitCode = exitCode;
        public int ExitCode { get; }
    }

    private static void Exit(int code) => throw new ExitException(code);

    private static void GeneratePass(
        Pcm16WaveWriter output,
        short[] amplitudes,
        int frequency1,
        int level1,
        int frequency2,
        int level2,
        int duration1,
        int duration2,
        int duration3,
        int duration4,
        bool repeat) {
        tone_gen_descriptor_t descriptor = tone_gen_descriptor_init(
            null,
            frequency1,
            level1,
            frequency2,
            level2,
            duration1,
            duration2,
            duration3,
            duration4,
            repeat ? 1 : 0)!;

        tone_gen_state_t state = tone_gen_init(null, descriptor)!;
        for (int iteration = 0; iteration < 1000; iteration++) {
            int length = tone_gen(state, amplitudes, SAMPLES_PER_CHUNK);
            Console.WriteLine($"Generated {length} samples");
            if (length <= 0)
                break;
            output.WriteFrames(amplitudes.AsSpan(0, length));
        }
        tone_gen_release(state);
        tone_gen_free(state);
        tone_gen_descriptor_free(descriptor);
    }

    public static int tkfx_tone_generate_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        Pcm16WaveWriter output;
        try {
            output = new Pcm16WaveWriter(OUTPUT_FILE_NAME, SAMPLE_RATE, 1);
        } catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException) {
            Console.Error.WriteLine($"    Cannot open audio file '{OUTPUT_FILE_NAME}'");
            Exit(2);
            throw;
        }

        var amplitudes = new short[16_384];
        try {
            GeneratePass(output, amplitudes, 440, -10, 620, -15, 100, 200, 300, 400, false);
            GeneratePass(output, amplitudes, 350, -10, 440, -15, 400, 300, 200, 100, true);
            GeneratePass(output, amplitudes, 400, -10, 450, -10, 100, 200, 300, 400, true);
            GeneratePass(output, amplitudes, 400, -10, 0, 0, 100, 200, 300, 400, true);
            GeneratePass(output, amplitudes, 820, -10, 0, 0, 2000, 0, 0, 0, false);
            GeneratePass(output, amplitudes, 820, 0, 0, 0, 2000, 0, 0, 0, false);
            GeneratePass(output, amplitudes, 425, -10, -50, 25, 100, 200, 300, 400, true);
            GeneratePass(output, amplitudes, 425, -10, -50, 100, 100, 200, 300, 400, true);
        } finally {
            try {
                output.Dispose();
            } catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException) {
                Console.Error.WriteLine($"    Cannot close audio file '{OUTPUT_FILE_NAME}'");
                Exit(2);
            }
        }

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

        public void WriteFrames(ReadOnlySpan<short> samples) {
            ObjectDisposedException.ThrowIf(disposed, this);
            foreach (short sample in samples)
                writer.Write(sample);
            sampleCount += samples.Length;
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
