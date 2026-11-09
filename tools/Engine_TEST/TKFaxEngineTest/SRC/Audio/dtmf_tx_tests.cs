/*
 * TKFaxEngine - managed C# test port
 *
 * dtmf_tx_tests.cs - Test the DTMF generator.
 *
 * Ported directly from the supplied spanDSP/TKFaxEngine test source.
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2003 Steve Underwood.
 *
 * This test retains the GNU General Public License version 2 terms of the
 * supplied source file.
 */

#nullable enable

using System.Text;
using TKFaxEngine.Audio;

namespace TKFaxEngineTest.Audio.Test;

public static class DtmfTxTests {
    private const string OutputFileName = "dtmf.wav";

    public static int BodyMain(string[] args) {
        _ = args;

        try {
            using var output = new TelephonyWaveWriter(OutputFileName, 1, 8000);
            using var generator = new DtmfTxState();
            var amplitudes = new short[16384];

            int length = generator.Generate(amplitudes);
            Console.WriteLine("Generated {0} samples", length);
            output.WriteFrames(amplitudes.AsSpan(0, length));

            PutOrFail(generator, "123");
            length = generator.Generate(amplitudes);
            Console.WriteLine("Generated {0} samples", length);
            output.WriteFrames(amplitudes.AsSpan(0, length));

            PutOrFail(generator, "456");
            length = generator.Generate(amplitudes.AsSpan(0, 160));
            Console.WriteLine("Generated {0} samples", length);
            output.WriteFrames(amplitudes.AsSpan(0, length));

            PutOrFail(generator, "789");
            length = generator.Generate(amplitudes.AsSpan(0, 160));
            Console.WriteLine("Generated {0} samples", length);
            output.WriteFrames(amplitudes.AsSpan(0, length));

            PutOrFail(generator, "*#");
            length = generator.Generate(amplitudes.AsSpan(0, 160));
            Console.WriteLine("Generated {0} samples", length);
            output.WriteFrames(amplitudes.AsSpan(0, length));

            bool addDigits = true;
            do {
                length = generator.Generate(amplitudes.AsSpan(0, 160));
                Console.WriteLine("Generated {0} samples", length);
                if (length > 0)
                    output.WriteFrames(amplitudes.AsSpan(0, length));

                if (addDigits && generator.Put("1234567890", -1) != 0) {
                    Console.WriteLine("Digit buffer full");
                    addDigits = false;
                }
            }
            while (length > 0);

            generator.Initialize(null, null);

            length = generator.Generate(amplitudes);
            Console.WriteLine("Generated {0} samples", length);
            output.WriteFrames(amplitudes.AsSpan(0, length));

            PutOrFail(generator, "123");
            length = generator.Generate(amplitudes);
            Console.WriteLine("Generated {0} samples", length);
            output.WriteFrames(amplitudes.AsSpan(0, length));

            PutOrFail(generator, "456");
            length = generator.Generate(amplitudes.AsSpan(0, 160));
            Console.WriteLine("Generated {0} samples", length);
            output.WriteFrames(amplitudes.AsSpan(0, length));

            PutOrFail(generator, "789");
            length = generator.Generate(amplitudes.AsSpan(0, 160));
            Console.WriteLine("Generated {0} samples", length);
            output.WriteFrames(amplitudes.AsSpan(0, length));

            PutOrFail(generator, "0*#");
            length = generator.Generate(amplitudes.AsSpan(0, 160));
            Console.WriteLine("Generated {0} samples", length);
            output.WriteFrames(amplitudes.AsSpan(0, length));

            PutOrFail(generator, "ABCD");
            length = generator.Generate(amplitudes.AsSpan(0, 160));
            Console.WriteLine("Generated {0} samples", length);
            output.WriteFrames(amplitudes.AsSpan(0, length));

            Console.WriteLine("Try different levels and timing");
            generator.SetLevel(-20, 5);
            generator.SetTiming(100, 200);
            PutOrFail(generator, "123");

            do {
                length = generator.Generate(amplitudes.AsSpan(0, 160));
                Console.WriteLine("Generated {0} samples", length);
                if (length > 0)
                    output.WriteFrames(amplitudes.AsSpan(0, length));
            }
            while (length > 0);

            Console.WriteLine("Restore normal levels and timing");
            generator.SetLevel(-10, 0);
            generator.SetTiming(50, 55);
            PutOrFail(generator, "A");

            addDigits = true;
            do {
                length = generator.Generate(amplitudes.AsSpan(0, 160));
                Console.WriteLine("Generated {0} samples", length);
                if (length > 0)
                    output.WriteFrames(amplitudes.AsSpan(0, length));

                if (addDigits && generator.Put("1234567890", -1) != 0) {
                    Console.WriteLine("Digit buffer full");
                    addDigits = false;
                }
            }
            while (length > 0);

            return 0;
        } catch (Exception exception) {
            Console.Error.WriteLine("DTMF TX test failed: {0}", exception);
            return 2;
        }
    }

    private static void PutOrFail(DtmfTxState generator, string digits) {
        if (generator.Put(digits, -1) != 0)
            throw new InvalidOperationException($"Unable to queue DTMF digits '{digits}'.");
    }

    private sealed class TelephonyWaveWriter : IDisposable {
        private readonly FileStream _stream;
        private readonly BinaryWriter _writer;
        private readonly int _channels;
        private readonly int _sampleRate;
        private long _sampleCount;
        private bool _disposed;

        public TelephonyWaveWriter(string path, int channels, int sampleRate) {
            if (channels <= 0)
                throw new ArgumentOutOfRangeException(nameof(channels));
            if (sampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));

            _channels = channels;
            _sampleRate = sampleRate;
            _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            _writer = new BinaryWriter(_stream, Encoding.ASCII, leaveOpen: true);
            WriteHeader(0);
        }

        public void WriteFrames(ReadOnlySpan<short> samples) {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (samples.Length % _channels != 0)
                throw new ArgumentException("The sample count must contain complete frames.", nameof(samples));

            foreach (short sample in samples)
                _writer.Write(sample);

            _sampleCount += samples.Length;
        }

        public void Dispose() {
            if (_disposed)
                return;

            long dataBytes = checked(_sampleCount * sizeof(short));
            _writer.Flush();
            _stream.Position = 0;
            WriteHeader(dataBytes);
            _writer.Flush();
            _writer.Dispose();
            _stream.Dispose();
            _disposed = true;
        }

        private void WriteHeader(long dataBytes) {
            if (dataBytes > uint.MaxValue - 36)
                throw new InvalidOperationException("The WAVE file is too large.");

            int blockAlign = checked(_channels * sizeof(short));
            int byteRate = checked(_sampleRate * blockAlign);

            _writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            _writer.Write((uint)(36 + dataBytes));
            _writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            _writer.Write(Encoding.ASCII.GetBytes("fmt "));
            _writer.Write(16u);
            _writer.Write((ushort)1);
            _writer.Write((ushort)_channels);
            _writer.Write((uint)_sampleRate);
            _writer.Write((uint)byteRate);
            _writer.Write((ushort)blockAlign);
            _writer.Write((ushort)16);
            _writer.Write(Encoding.ASCII.GetBytes("data"));
            _writer.Write((uint)dataBytes);
        }
    }
}
