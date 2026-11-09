/*
 * TKFaxEngine - managed C# port
 *
 * lpc10_tests.cs - Test the LPC10 low bit rate speech codec.
 *
 * Fresh direct port of lpc10_tests.c.
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2006 Steve Underwood.
 * Licensed under the GNU General Public License version 2.
 */

#nullable enable

using System.Buffers.Binary;
using TKFaxEngine.Audio;

namespace TKFaxEngineTest.Audio.Test;

public static class lpc10_tests {
    private const int BLOCK_LEN = 180;
    private const int BLOCKS_PER_READ = 5;

    private const string IN_FILE_NAME = "Test-Files/local/dam9.wav";
    private const string REF_FILE_NAME = "Test-Files/local/dam9_lpc55.wav";
    private const string COMPRESS_FILE_NAME = "lpc10_out.lpc10";
    private const string DECOMPRESS_FILE_NAME = "lpc10_in.lpc10";
    private const string OUT_FILE_NAME = "post_lpc10.wav";

    public static int tkfx_lpc10_tests_body_main(string[] testArguments) {
        bool compress = false;
        bool decompress = false;
        bool log_error = true;
        string in_file_name = IN_FILE_NAME;

        ParseOptions(testArguments, ref compress, ref decompress, ref log_error, ref in_file_name);

        Pcm16WaveReader? inhandle = null;
        Pcm16WaveReader? refhandle = null;
        FileStream? decompress_file = null;
        FileStream? compress_file = null;
        Pcm16WaveWriter? outhandle = null;
        lpc10_encode_state_t? lpc10_enc_state = null;
        lpc10_decode_state_t? lpc10_dec_state = null;

        try {
            if (!decompress) {
                try {
                    inhandle = Pcm16WaveReader.OpenTelephony(in_file_name, 1);
                } catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) {
                    Console.Error.WriteLine("    Cannot open audio file '{0}'", in_file_name);
                    throw new Lpc10TestsExitException(2);
                }

                try {
                    refhandle = Pcm16WaveReader.OpenTelephony(REF_FILE_NAME, 1);
                } catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) {
                    Console.Error.WriteLine("    Cannot open audio file '{0}'", REF_FILE_NAME);
                    throw new Lpc10TestsExitException(2);
                }
            } else {
                try {
                    decompress_file = new FileStream(
                        DECOMPRESS_FILE_NAME,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read);
                } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                    Console.Error.WriteLine("    Cannot open decompressed data file '{0}'", DECOMPRESS_FILE_NAME);
                    throw new Lpc10TestsExitException(2);
                }
            }

            try {
                outhandle = Pcm16WaveWriter.CreateTelephony(OUT_FILE_NAME, 1);
            } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                Console.Error.WriteLine("    Cannot create audio file '{0}'", OUT_FILE_NAME);
                throw new Lpc10TestsExitException(2);
            }

            lpc10_enc_state = TKFaxEngine.Audio.lpc10.lpc10_encode_init(null, 1);
            if (lpc10_enc_state is null) {
                Console.Error.WriteLine("    Cannot create encoder");
                throw new Lpc10TestsExitException(2);
            }

            lpc10_dec_state = TKFaxEngine.Audio.lpc10.lpc10_decode_init(null, 1);
            if (lpc10_dec_state is null) {
                Console.Error.WriteLine("    Cannot create decoder");
                throw new Lpc10TestsExitException(2);
            }

            if (compress) {
                try {
                    compress_file = new FileStream(
                        COMPRESS_FILE_NAME,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None);
                } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                    Console.Error.WriteLine("    Cannot create compressed data file '{0}'", COMPRESS_FILE_NAME);
                    throw new Lpc10TestsExitException(2);
                }
            }

            double pre_energy = 0.0;
            double post_energy = 0.0;
            double ref_energy = 0.0;
            double diff_energy = 0.0;

            short[] pre_amp = new short[BLOCKS_PER_READ * BLOCK_LEN];
            short[] post_amp = new short[BLOCKS_PER_READ * BLOCK_LEN];
            short[] ref_amp = new short[BLOCKS_PER_READ * BLOCK_LEN];
            short[] log_amp = new short[BLOCKS_PER_READ * BLOCK_LEN * 3];
            byte[] lpc10_data = new byte[BLOCKS_PER_READ * 7];

            if (decompress) {
                int len;
                while ((len = decompress_file!.Read(lpc10_data, 0, lpc10_data.Length)) > 0) {
                    // Preserves the exact source call, which passes len/7 to lpc10_decode.
                    TKFaxEngine.Audio.lpc10.lpc10_decode(lpc10_dec_state, post_amp, lpc10_data, len / 7);
                    outhandle.WriteFrames(post_amp, BLOCK_LEN * len / 7);
                }
            } else {
                int block_no = 0;
                while (inhandle!.ReadFrames(pre_amp, pre_amp.Length) == pre_amp.Length &&
                       refhandle!.ReadFrames(ref_amp, ref_amp.Length) == ref_amp.Length) {
                    int enc_len = TKFaxEngine.Audio.lpc10.lpc10_encode(
                        lpc10_enc_state,
                        lpc10_data,
                        pre_amp,
                        BLOCKS_PER_READ * BLOCK_LEN);

                    if (compress)
                        compress_file!.Write(lpc10_data, 0, enc_len);

                    int dec_len = TKFaxEngine.Audio.lpc10.lpc10_decode(
                        lpc10_dec_state,
                        post_amp,
                        lpc10_data,
                        enc_len);

                    for (int i = 0; i < dec_len; i++) {
                        pre_energy += (double)pre_amp[i] * pre_amp[i];
                        post_energy += (double)post_amp[i] * post_amp[i];
                        ref_energy += (double)ref_amp[i] * ref_amp[i];

                        double xx;
                        if (ref_amp[i] == short.MaxValue || ref_amp[i] == short.MinValue)
                            xx = 0.0;
                        else
                            xx = post_amp[i] - ref_amp[i];

                        diff_energy += xx * xx;
                        log_amp[i] = unchecked((short)(int)xx);
                    }

                    block_no++;
                    if (log_error)
                        outhandle.WriteFrames(log_amp, dec_len);
                    else
                        outhandle.WriteFrames(post_amp, dec_len);
                }
            }

            if (!decompress) {
                Console.WriteLine("Output energy is {0:F6}% of input energy.", 100.0 * post_energy / pre_energy);
                Console.WriteLine("Difference energy is {0:F6}% of the total.", 100.0 * diff_energy / ref_energy);

                double outputEnergyRatioDelta = Math.Abs(1.0 - post_energy / pre_energy);
                double differenceEnergyRatio = Math.Abs(diff_energy / post_energy);
                if (outputEnergyRatioDelta > 0.05 || differenceEnergyRatio > 0.03) {
                    Console.WriteLine(
                        "ERROR LPC10 energy limits expected_output_energy_ratio_delta<=0.050000 actual={0:F6} " +
                        "expected_difference_energy_ratio<=0.030000 actual={1:F6} " +
                        "input_buffer=BEFUELLT output_buffer=BEFUELLT",
                        outputEnergyRatioDelta,
                        differenceEnergyRatio);
                    throw new Lpc10TestsExitException(2);
                }

                Console.WriteLine("Tests passed.");
            }

            return 0;
        } finally {
            compress_file?.Dispose();
            decompress_file?.Dispose();
            inhandle?.Dispose();
            refhandle?.Dispose();
            outhandle?.Dispose();

            if (lpc10_enc_state is not null)
                TKFaxEngine.Audio.lpc10.lpc10_encode_free(lpc10_enc_state);
            if (lpc10_dec_state is not null)
                TKFaxEngine.Audio.lpc10.lpc10_decode_free(lpc10_dec_state);
        }
    }

    public static int Main(string[] args) => tkfx_lpc10_tests_body_main(args);

    private static void ParseOptions(
        string[] args,
        ref bool compress,
        ref bool decompress,
        ref bool logError,
        ref string inputFileName) {
        for (int argumentIndex = 0; argumentIndex < args.Length; argumentIndex++) {
            string argument = args[argumentIndex];
            if (argument.Length < 2 || argument[0] != '-')
                throw new Lpc10TestsExitException(2);

            for (int optionIndex = 1; optionIndex < argument.Length; optionIndex++) {
                switch (argument[optionIndex]) {
                    case 'c':
                        compress = true;
                        break;
                    case 'd':
                        decompress = true;
                        break;
                    case 'l':
                        logError = false;
                        break;
                    case 'i':
                        if (optionIndex + 1 < argument.Length) {
                            inputFileName = argument[(optionIndex + 1)..];
                            optionIndex = argument.Length;
                        } else {
                            argumentIndex++;
                            if (argumentIndex >= args.Length)
                                throw new Lpc10TestsExitException(2);
                            inputFileName = args[argumentIndex];
                        }
                        break;
                    default:
                        throw new Lpc10TestsExitException(2);
                }
            }
        }
    }

    public sealed class Lpc10TestsExitException : Exception {
        public Lpc10TestsExitException(int exitCode)
            : base($"lpc10_tests requested process exit {exitCode}.") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }

    private sealed class Pcm16WaveReader : IDisposable {
        private readonly FileStream _stream;
        private long _dataBytesRemaining;

        private Pcm16WaveReader(FileStream stream, long dataBytesRemaining) {
            _stream = stream;
            _dataBytesRemaining = dataBytesRemaining;
        }

        public static Pcm16WaveReader OpenTelephony(string path, int channels) {
            FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            try {
                Span<byte> header = stackalloc byte[12];
                ReadExactly(stream, header);
                if (!header[..4].SequenceEqual("RIFF"u8) || !header[8..12].SequenceEqual("WAVE"u8))
                    throw new InvalidDataException("The file is not a RIFF/WAVE stream.");

                bool haveFormat = false;
                ushort formatTag = 0;
                ushort channelCount = 0;
                uint sampleRate = 0;
                ushort bitsPerSample = 0;
                long dataPosition = -1;
                uint dataLength = 0;

                Span<byte> chunkHeader = stackalloc byte[8];
                while (stream.Position + 8 <= stream.Length) {
                    ReadExactly(stream, chunkHeader);
                    uint chunkLength = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader[4..8]);
                    long nextChunk = checked(stream.Position + chunkLength + (chunkLength & 1));
                    if (nextChunk > stream.Length)
                        throw new InvalidDataException("The WAVE chunk length is invalid.");

                    if (chunkHeader[..4].SequenceEqual("fmt "u8)) {
                        if (chunkLength < 16)
                            throw new InvalidDataException("The WAVE format chunk is too short.");

                        byte[] format = new byte[checked((int)chunkLength)];
                        ReadExactly(stream, format);
                        formatTag = BinaryPrimitives.ReadUInt16LittleEndian(format.AsSpan(0, 2));
                        channelCount = BinaryPrimitives.ReadUInt16LittleEndian(format.AsSpan(2, 2));
                        sampleRate = BinaryPrimitives.ReadUInt32LittleEndian(format.AsSpan(4, 4));
                        bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(format.AsSpan(14, 2));
                        haveFormat = true;
                    } else if (chunkHeader[..4].SequenceEqual("data"u8)) {
                        dataPosition = stream.Position;
                        dataLength = chunkLength;
                        stream.Position = nextChunk;
                    } else {
                        stream.Position = nextChunk;
                    }

                    if ((stream.Position & 1) != 0)
                        stream.Position++;
                }

                if (!haveFormat || dataPosition < 0)
                    throw new InvalidDataException("The WAVE file has no PCM format or data chunk.");
                if (formatTag != 1 || channelCount != channels || sampleRate != 8000 || bitsPerSample != 16)
                    throw new InvalidDataException("The WAVE file is not 8 kHz, mono, 16-bit PCM telephony audio.");

                stream.Position = dataPosition;
                return new Pcm16WaveReader(stream, dataLength);
            } catch {
                stream.Dispose();
                throw;
            }
        }

        public int ReadFrames(short[] destination, int frames) {
            ArgumentNullException.ThrowIfNull(destination);
            frames = Math.Min(frames, destination.Length);
            int bytesRequested = checked(frames * sizeof(short));
            int bytesToRead = (int)Math.Min(bytesRequested, _dataBytesRemaining);
            bytesToRead &= ~1;
            if (bytesToRead == 0)
                return 0;

            byte[] bytes = new byte[bytesToRead];
            int bytesRead = 0;
            while (bytesRead < bytesToRead) {
                int count = _stream.Read(bytes, bytesRead, bytesToRead - bytesRead);
                if (count == 0)
                    break;
                bytesRead += count;
            }

            bytesRead &= ~1;
            _dataBytesRemaining -= bytesRead;
            int sampleCount = bytesRead / 2;
            for (int i = 0; i < sampleCount; i++)
                destination[i] = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(i * 2, 2));
            return sampleCount;
        }

        public void Dispose() => _stream.Dispose();
    }

    private sealed class Pcm16WaveWriter : IDisposable {
        private readonly FileStream _stream;
        private readonly int _channels;
        private readonly int _sampleRate;
        private long _dataBytes;
        private bool _disposed;

        private Pcm16WaveWriter(FileStream stream, int channels, int sampleRate) {
            _stream = stream;
            _channels = channels;
            _sampleRate = sampleRate;
            _stream.Position = 44;
        }

        public static Pcm16WaveWriter CreateTelephony(string path, int channels) =>
            new(new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None), channels, 8000);

        public void WriteFrames(short[] samples, int frames) {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(samples);
            if (frames < 0 || frames > samples.Length)
                throw new ArgumentOutOfRangeException(nameof(frames));

            byte[] bytes = new byte[checked(frames * sizeof(short))];
            for (int i = 0; i < frames; i++)
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2, 2), samples[i]);
            _stream.Write(bytes);
            _dataBytes += bytes.Length;
        }

        public void Dispose() {
            if (_disposed)
                return;

            _disposed = true;
            WriteHeader();
            _stream.Dispose();
        }

        private void WriteHeader() {
            if (_dataBytes > uint.MaxValue - 36)
                throw new IOException("The WAVE output exceeds the RIFF size limit.");

            Span<byte> header = stackalloc byte[44];
            "RIFF"u8.CopyTo(header);
            BinaryPrimitives.WriteUInt32LittleEndian(header[4..8], checked((uint)(36 + _dataBytes)));
            "WAVE"u8.CopyTo(header[8..12]);
            "fmt "u8.CopyTo(header[12..16]);
            BinaryPrimitives.WriteUInt32LittleEndian(header[16..20], 16);
            BinaryPrimitives.WriteUInt16LittleEndian(header[20..22], 1);
            BinaryPrimitives.WriteUInt16LittleEndian(header[22..24], checked((ushort)_channels));
            BinaryPrimitives.WriteUInt32LittleEndian(header[24..28], checked((uint)_sampleRate));
            uint byteRate = checked((uint)(_sampleRate * _channels * sizeof(short)));
            BinaryPrimitives.WriteUInt32LittleEndian(header[28..32], byteRate);
            BinaryPrimitives.WriteUInt16LittleEndian(header[32..34], checked((ushort)(_channels * sizeof(short))));
            BinaryPrimitives.WriteUInt16LittleEndian(header[34..36], 16);
            "data"u8.CopyTo(header[36..40]);
            BinaryPrimitives.WriteUInt32LittleEndian(header[40..44], checked((uint)_dataBytes));

            long position = _stream.Position;
            _stream.Position = 0;
            _stream.Write(header);
            _stream.Position = position;
            _stream.Flush();
        }
    }

    private static void ReadExactly(Stream stream, Span<byte> destination) {
        int offset = 0;
        while (offset < destination.Length) {
            int count = stream.Read(destination[offset..]);
            if (count == 0)
                throw new EndOfStreamException();
            offset += count;
        }
    }

    private static void ReadExactly(Stream stream, byte[] destination) {
        int offset = 0;
        while (offset < destination.Length) {
            int count = stream.Read(destination, offset, destination.Length - offset);
            if (count == 0)
                throw new EndOfStreamException();
            offset += count;
        }
    }
}
