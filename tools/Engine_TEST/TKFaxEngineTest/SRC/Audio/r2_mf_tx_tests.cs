/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * r2_mf_tx_tests.cs - complete managed port of r2_mf_tx_tests.c.
 *
 * Original test written by Steve Underwood.
 * Licensed under the GNU General Public License version 2.
 */

#nullable enable

using System.Text;
using TKFaxEngine.Audio;

namespace TKFaxEngineTest.Audio.Test;

public static class r2_mf_tx_tests {
    private const string OUTPUT_FILE_NAME = "r2_mf.wav";
    private const string DIGITS = "0123456789BCDEF";

    public static int main(string[] testArguments) {
        try {
            return tkfx_r2_mf_tx_tests_body_main(testArguments);
        } catch (R2MfTxTestsExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_r2_mf_tx_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        try {
            using var output = new Pcm16WaveWriter(OUTPUT_FILE_NAME, 8000, 1);
            var samples = new short[1000];

            GenerateToneSet(output, samples, forward: false);
            GenerateToneSet(output, samples, forward: true);

            return 0;
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) {
            Console.Error.WriteLine($"    Cannot write audio file '{OUTPUT_FILE_NAME}': {ex.Message}");
            Exit(2);
            return 2;
        }
    }

    private static void GenerateToneSet(Pcm16WaveWriter output, short[] samples, bool forward) {
        var generator = new R2MfTx(forward);

        foreach (char digit in DIGITS) {
            generator.Put(digit);
            int length = generator.Generate(samples);
            Console.WriteLine($"Generated {length} samples of {digit}");
            if (length > 0)
                output.WriteFrames(samples.AsSpan(0, length));

            generator.Put('\0');
            length = generator.Generate(samples);
            Console.WriteLine($"Generated {length} samples");
            if (length > 0)
                output.WriteFrames(samples.AsSpan(0, length));
        }
    }

    private static void Exit(int code) => throw new R2MfTxTestsExitException(code);

    public sealed class R2MfTxTestsExitException : Exception {
        public R2MfTxTestsExitException(int exitCode)
            : base($"r2_mf_tx_tests exited with code {exitCode}.") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }

    private sealed class Pcm16WaveWriter : IDisposable {
        private readonly FileStream _stream;
        private readonly BinaryWriter _writer;
        private readonly short _channels;
        private long _sampleCount;
        private bool _disposed;

        public Pcm16WaveWriter(string path, int sampleRate, short channels) {
            _channels = channels;
            _stream = File.Create(path);
            _writer = new BinaryWriter(_stream, Encoding.ASCII, leaveOpen: true);

            _writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            _writer.Write(0);
            _writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            _writer.Write(Encoding.ASCII.GetBytes("fmt "));
            _writer.Write(16);
            _writer.Write((short)1);
            _writer.Write(channels);
            _writer.Write(sampleRate);
            _writer.Write(sampleRate * channels * sizeof(short));
            _writer.Write((short)(channels * sizeof(short)));
            _writer.Write((short)16);
            _writer.Write(Encoding.ASCII.GetBytes("data"));
            _writer.Write(0);
        }

        public void WriteFrames(ReadOnlySpan<short> samples) {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Pcm16WaveWriter));

            foreach (short sample in samples)
                _writer.Write(sample);

            _sampleCount += samples.Length;
        }

        public void Dispose() {
            if (_disposed)
                return;

            _disposed = true;
            long dataBytes = checked(_sampleCount * sizeof(short));
            _stream.Position = 4;
            _writer.Write(checked((int)(36 + dataBytes)));
            _stream.Position = 40;
            _writer.Write(checked((int)dataBytes));
            _writer.Flush();
            _writer.Dispose();
            _stream.Dispose();
        }
    }
}
