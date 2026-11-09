/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * swept_tone_tests.cs
 *
 * Managed C# port of the supplied spanDSP swept_tone_tests.c test.
 * The original test sequence, duration, block length, level measurement and
 * generated PCM wave file are retained.
 */

#nullable enable

using TKFaxEngine.Audio;
using static TKFaxEngine.Audio.PowerMeterApi;
using static TKFaxEngine.Audio.SweptToneApi;

namespace TKFaxEngineTest.Audio.Test;

public static class SweptToneTests {
    private const string OUTPUT_FILE_NAME = "swept_tone.wav";
    private const int BLOCK_LEN = 160;

    public sealed class ExitException : Exception {
        public ExitException(int exitCode) => ExitCode = exitCode;
        public int ExitCode { get; }
    }

    private sealed class TelephonyWaveWriter : IDisposable {
        private readonly FileStream stream;
        private readonly BinaryWriter writer;
        private long samplesWritten;
        private bool disposed;

        public TelephonyWaveWriter(string fileName, int channels, int sampleRate) {
            if (channels <= 0)
                throw new ArgumentOutOfRangeException(nameof(channels));
            if (sampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));

            stream = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            writer = new BinaryWriter(stream);
            WriteHeader(channels, sampleRate);
        }

        public int Write(ReadOnlySpan<short> samples) {
            ObjectDisposedException.ThrowIf(disposed, this);
            foreach (short sample in samples)
                writer.Write(sample);
            samplesWritten += samples.Length;
            return samples.Length;
        }

        private void WriteHeader(int channels, int sampleRate) {
            int blockAlign = checked(channels * sizeof(short));
            int byteRate = checked(sampleRate * blockAlign);

            writer.Write(new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            writer.Write(0);
            writer.Write(new byte[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
            writer.Write(new byte[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short)blockAlign);
            writer.Write((short)16);
            writer.Write(new byte[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            writer.Write(0);
        }

        public void Dispose() {
            if (disposed)
                return;

            long dataBytes = checked(samplesWritten * sizeof(short));
            writer.Flush();
            stream.Position = 4;
            writer.Write(checked((int)(36 + dataBytes)));
            stream.Position = 40;
            writer.Write(checked((int)dataBytes));
            writer.Flush();
            writer.Dispose();
            stream.Dispose();
            disposed = true;
        }
    }

    private static void Exit(int code) => throw new ExitException(code);

    public static int tkfx_swept_tone_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        PowerMeterState? meter = power_meter_init(null, 10);
        if (meter is null) {
            Console.Error.WriteLine("    Cannot initialize power meter");
            return 2;
        }

        TelephonyWaveWriter output;
        try {
            output = new TelephonyWaveWriter(OUTPUT_FILE_NAME, 1, SweptTone.SampleRate);
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine($"    Cannot create audio file '{OUTPUT_FILE_NAME}'");
            return 2;
        }

        using (output) {
            Console.WriteLine("Test with swept tone.");

            SweptToneState state = swept_tone_init(
                null,
                200.0f,
                3900.0f,
                -10.0f,
                60 * SweptTone.SampleRate,
                1);

            short[] buffer = new short[BLOCK_LEN];
            for (int position = 0;
                 position < 60 * SweptTone.SampleRate;
                 position += BLOCK_LEN) {
                int length = swept_tone(state, buffer, BLOCK_LEN);
                for (int i = 0; i < length; ++i)
                    power_meter_update(meter, buffer[i]);

                int outputFrames;
                try {
                    outputFrames = output.Write(buffer.AsSpan(0, length));
                } catch (IOException) {
                    Console.Error.WriteLine("    Error writing audio file");
                    swept_tone_free(state);
                    power_meter_release(meter);
                    Exit(2);
                    return 2;
                }

                if (outputFrames != length) {
                    Console.Error.WriteLine("    Error writing audio file");
                    swept_tone_free(state);
                    power_meter_release(meter);
                    Exit(2);
                }

                Console.WriteLine(
                    $"{swept_tone_current_frequency(state):F1} " +
                    $"{power_meter_current_dbm0(meter):F6}");
            }

            swept_tone_free(state);
        }

        power_meter_release(meter);
        Console.WriteLine("Tests passed.");
        return 0;
    }
}
