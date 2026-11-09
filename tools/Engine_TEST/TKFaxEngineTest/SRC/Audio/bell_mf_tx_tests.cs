/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * bell_mf_tx_tests.cs - Bell MF generator load and queue tests.
 *
 * Original C test written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2003 Steve Underwood
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License version 2, as
 * published by the Free Software Foundation.
 *
 * Managed C# port. Both original generator passes, partial-buffer
 * generation, queue saturation and PCM16 WAV output are retained.
 */

#nullable enable

using System.Text;
using TKFaxEngine.Audio;

namespace TKFaxEngineTest.Audio.Test;

public static class bell_mf_tx_tests {
    private const string OUTPUT_FILE_NAME = "bell_mf.wav";

    public static int main(string[] testArguments) {
        try {
            return tkfx_bell_mf_tx_tests_body_main(testArguments);
        } catch (BellMfTxTestExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_bell_mf_tx_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        var generator = new BellMfTx();
        var samples = new short[16_384];

        using var output = new Pcm16WaveWriter(
            OUTPUT_FILE_NAME,
            channels: 1,
            sampleRate: 8000);

        int length = generator.Generate(samples);
        WriteGenerated(output, samples, length);

        if (generator.Put("123", -1) != 0)
            Console.WriteLine("Ooops");

        length = generator.Generate(samples);
        WriteGenerated(output, samples, length);

        if (generator.Put("456", -1) != 0)
            Console.WriteLine("Ooops");

        length = generator.Generate(samples.AsSpan(0, 160));
        WriteGenerated(output, samples, length);

        if (generator.Put("789", -1) != 0)
            Console.WriteLine("Ooops");

        length = generator.Generate(samples.AsSpan(0, 160));
        WriteGenerated(output, samples, length);

        if (generator.Put("*#", -1) != 0)
            Console.WriteLine("Ooops");

        length = generator.Generate(samples.AsSpan(0, 160));
        WriteGenerated(output, samples, length);

        int addDigits = 1;
        do {
            length = generator.Generate(samples.AsSpan(0, 160));
            WriteGenerated(output, samples, length);

            if (addDigits != 0 && generator.Put("1234567890", -1) != 0) {
                Console.WriteLine("Digit buffer full");
                addDigits = 0;
            }
        }
        while (length > 0);

        generator.Reset();

        length = generator.Generate(samples);
        WriteGenerated(output, samples, length);

        if (generator.Put("123", -1) != 0)
            Console.WriteLine("Ooops");

        length = generator.Generate(samples);
        WriteGenerated(output, samples, length);

        if (generator.Put("456", -1) != 0)
            Console.WriteLine("Ooops");

        length = generator.Generate(samples.AsSpan(0, 160));
        WriteGenerated(output, samples, length);

        if (generator.Put("789", -1) != 0)
            Console.WriteLine("Ooops");

        length = generator.Generate(samples.AsSpan(0, 160));
        WriteGenerated(output, samples, length);

        if (generator.Put("0*#", -1) != 0)
            Console.WriteLine("Ooops");

        length = generator.Generate(samples.AsSpan(0, 160));
        WriteGenerated(output, samples, length);

        if (generator.Put("ABC", -1) != 0)
            Console.WriteLine("Ooops");

        length = generator.Generate(samples.AsSpan(0, 160));
        WriteGenerated(output, samples, length);

        addDigits = 1;
        do {
            length = generator.Generate(samples.AsSpan(0, 160));
            WriteGenerated(output, samples, length);

            if (addDigits != 0 && generator.Put("1234567890", -1) != 0) {
                Console.WriteLine("Digit buffer full");
                addDigits = 0;
            }
        }
        while (length > 0);

        return 0;
    }

    private static void WriteGenerated(
        Pcm16WaveWriter output,
        short[] samples,
        int length) {
        Console.WriteLine($"Generated {length} samples");

        if (length > 0)
            output.WriteSamples(samples.AsSpan(0, length));
    }

    public sealed class BellMfTxTestExitException : Exception {
        public BellMfTxTestExitException(int exitCode)
            : base($"bell_mf_tx_tests requested exit code {exitCode}.") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }

    private sealed class Pcm16WaveWriter : IDisposable {
        private readonly FileStream stream;
        private readonly BinaryWriter writer;
        private readonly short channels;
        private readonly int sampleRate;
        private long sampleCount;
        private bool disposed;

        public Pcm16WaveWriter(string path, short channels, int sampleRate) {
            if (channels <= 0)
                throw new ArgumentOutOfRangeException(nameof(channels));
            if (sampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));

            this.channels = channels;
            this.sampleRate = sampleRate;
            stream = File.Create(path);
            writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
            WriteHeader(dataLength: 0);
        }

        public void WriteSamples(ReadOnlySpan<short> samples) {
            ObjectDisposedException.ThrowIf(disposed, this);

            foreach (short sample in samples)
                writer.Write(sample);

            sampleCount += samples.Length;
        }

        public void Dispose() {
            if (disposed)
                return;

            disposed = true;
            long dataLength = checked(sampleCount * sizeof(short));
            writer.Flush();
            stream.Position = 0;
            WriteHeader(dataLength);
            writer.Dispose();
            stream.Dispose();
        }

        private void WriteHeader(long dataLength) {
            if (dataLength > uint.MaxValue)
                throw new InvalidOperationException("The WAV data exceeds the RIFF size limit.");

            ushort blockAlign = checked((ushort)(channels * sizeof(short)));
            uint byteRate = checked((uint)(sampleRate * blockAlign));
            uint riffLength = checked((uint)(36 + dataLength));

            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(riffLength);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16u);
            writer.Write((ushort)1);
            writer.Write((ushort)channels);
            writer.Write((uint)sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write((ushort)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write((uint)dataLength);
        }
    }
}
