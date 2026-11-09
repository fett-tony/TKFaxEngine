/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * oki_adpcm_tests.c - Test the OKI (Dialogic) ADPCM encoder and decoder at
 * 24 kbit/s and 32 kbit/s.
 *
 * Written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2004 Steve Underwood.
 * GNU General Public License version 2.
 *
 * Managed C# port preserving the supplied spanDSP test sequence, reset check,
 * energy limits, encoded-data mode and telephony WAV handling.
 */

#nullable enable

using System.Buffers.Binary;
using System.Text;
using TKFaxEngine.Audio;
using TKFaxEngineTest;
using static TKFaxEngine.Audio.OkiAdpcm;

namespace TKFaxEngineTest.Audio.Test;

public static class OkiAdpcmTests {
    private const string IN_FILE_NAME = "Test-Files/local/short_nb_voice.wav";
    private const string OUT_FILE_NAME = "post_oki_adpcm.wav";
    private const int HIST_LEN = 1_000;

    private static void Exit(int code) => throw new TestProcessExitException(code);

    public static int tkfx_oki_adpcm_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        int bitRate = 32_000;
        string? encodedFileName = null;
        string inputFileName = IN_FILE_NAME;
        bool logEncodedData = false;

        for (int argumentIndex = 0; argumentIndex < testArguments.Length; argumentIndex++) {
            string argument = testArguments[argumentIndex];
            if (argument == "--")
                break;
            if (argument.Length < 2 || (argument[0] != '-' && argument[0] != '/'))
                break;

            for (int optionIndex = 1; optionIndex < argument.Length; optionIndex++) {
                char option = argument[optionIndex];
                switch (option) {
                    case '2':
                        bitRate = 24_000;
                        break;

                    case 'l':
                        logEncodedData = true;
                        break;

                    case 'd':
                    case 'i': {
                        string optionValue;
                        if (optionIndex + 1 < argument.Length) {
                            optionValue = argument[(optionIndex + 1)..];
                        } else if (++argumentIndex < testArguments.Length) {
                            optionValue = testArguments[argumentIndex];
                        } else {
                            Exit(2);
                            return 2;
                        }

                        if (option == 'd')
                            encodedFileName = optionValue;
                        else
                            inputFileName = optionValue;
                        optionIndex = argument.Length;
                        break;
                    }

                    default:
                        Exit(2);
                        return 2;
                }
            }
        }

        FileStream? encodedInput = null;
        TelephonyWaveReader? input = null;
        OkiAdpcmState? encoder = null;

        if (encodedFileName is not null) {
            try {
                encodedInput = File.OpenRead(encodedFileName);
            } catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException) {
                Console.Error.WriteLine(
                    $"    Cannot open encoded file '{encodedFileName}'");
                Exit(2);
            }
        } else {
            try {
                input = new TelephonyWaveReader(inputFileName);
            } catch (Exception exception) when (
                exception is IOException or
                UnauthorizedAccessException or
                InvalidDataException) {
                Console.Error.WriteLine(
                    $"    Cannot open audio file '{inputFileName}'");
                Exit(2);
            }

            encoder = oki_adpcm_init(null, bitRate);
            if (encoder is null) {
                Console.Error.WriteLine("    Cannot create encoder");
                Exit(2);
            }
        }

        TelephonyWaveWriter output;
        try {
            output = new TelephonyWaveWriter(OUT_FILE_NAME, 8_000);
        } catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException) {
            encodedInput?.Dispose();
            input?.Dispose();
            oki_adpcm_free(encoder);
            Console.Error.WriteLine($"    Cannot create audio file '{OUT_FILE_NAME}'");
            Exit(2);
            return 2;
        }

        OkiAdpcmState? decoder = oki_adpcm_init(null, bitRate);
        if (decoder is null) {
            output.Dispose();
            Console.Error.WriteLine("    Cannot create decoder");
            Exit(2);
        }

        OkiAdpcmState? byteDecoder = oki_adpcm_init(null, bitRate);
        if (byteDecoder is null) {
            output.Dispose();
            oki_adpcm_free(decoder);
            Console.Error.WriteLine("    Cannot create decoder");
            Exit(2);
        }

        short[] preAmplitude = new short[HIST_LEN];
        short[] postAmplitude = new short[HIST_LEN];
        byte[] okiData = new byte[HIST_LEN];
        short[] history = new short[HIST_LEN];
        int historyInput = 0;
        int historyOutput = bitRate == 32_000 ? 0 : HIST_LEN - 27;
        double preEnergy = 0.0;
        double postEnergy = 0.0;
        double differenceEnergy = 0.0;
        int totalPreSamples = 0;
        int totalCompressedBytes = 0;
        int totalPostSamples = 0;

        try {
            if (encodedFileName is not null) {
                while (true) {
                    int okiBytes = encodedInput!.Read(okiData, 0, 80);
                    if (okiBytes <= 0)
                        break;

                    totalCompressedBytes += okiBytes;
                    int decodedFrames = oki_adpcm_decode(
                        decoder,
                        postAmplitude,
                        okiData,
                        okiBytes);
                    totalPostSamples += decodedFrames;

                    for (int i = 0; i < decodedFrames; i++) {
                        postEnergy += (double)postAmplitude[i] * postAmplitude[i];
                        int difference = postAmplitude[i] - history[historyOutput++];
                        if (historyOutput >= HIST_LEN)
                            historyOutput = 0;
                        differenceEnergy += (double)difference * difference;
                    }

                    _ = output.WriteFrames(postAmplitude, decodedFrames);
                }

                encodedInput.Dispose();
                encodedInput = null;
            } else {
                int successive08Bytes = 0;
                int successive80Bytes = 0;
                Stream? standardOutput = logEncodedData
                    ? Console.OpenStandardOutput()
                    : null;

                int frames;
                while ((frames = input!.ReadFrames(preAmplitude, 159)) != 0) {
                    totalPreSamples += frames;
                    int okiBytes = oki_adpcm_encode(
                        encoder!,
                        okiData,
                        preAmplitude,
                        frames);

                    if (logEncodedData)
                        standardOutput!.Write(okiData, 0, okiBytes);

                    totalCompressedBytes += okiBytes;

                    for (int i = 0; i < okiBytes; i++) {
                        _ = oki_adpcm_decode(
                            byteDecoder,
                            postAmplitude.AsSpan(),
                            okiData.AsSpan(i, 1),
                            1);

                        if (okiData[i] == 0x08)
                            successive08Bytes++;
                        else
                            successive08Bytes = 0;

                        if (okiData[i] == 0x80)
                            successive80Bytes++;
                        else
                            successive80Bytes = 0;

                        if ((successive08Bytes == 24 || successive80Bytes == 24) &&
                            byteDecoder.StepIndex != 0) {
                            Console.Error.WriteLine("Decoder reset failure");
                            Exit(2);
                        }
                    }

                    int decodedFrames = oki_adpcm_decode(
                        decoder,
                        postAmplitude,
                        okiData,
                        okiBytes);
                    totalPostSamples += decodedFrames;

                    for (int i = 0; i < frames; i++) {
                        history[historyInput++] = preAmplitude[i];
                        if (historyInput >= HIST_LEN)
                            historyInput = 0;
                        preEnergy += (double)preAmplitude[i] * preAmplitude[i];
                    }

                    for (int i = 0; i < decodedFrames; i++) {
                        postEnergy += (double)postAmplitude[i] * postAmplitude[i];
                        int difference = postAmplitude[i] - history[historyOutput++];
                        if (historyOutput >= HIST_LEN)
                            historyOutput = 0;
                        differenceEnergy += (double)difference * difference;
                    }

                    _ = output.WriteFrames(postAmplitude, decodedFrames);
                }

                Console.WriteLine($"Pre samples: {totalPreSamples}");
                Console.WriteLine($"Compressed bytes: {totalCompressedBytes}");
                Console.WriteLine($"Post samples: {totalPostSamples}");
                Console.WriteLine(
                    $"Output energy is {100.0 * postEnergy / preEnergy:F6}% of input energy.");
                Console.WriteLine(
                    $"Residual energy is {100.0 * differenceEnergy / postEnergy:F6}% of the total.");

                double outputEnergyRatioDelta = Math.Abs(1.0 - postEnergy / preEnergy);
                double residualEnergyRatio = Math.Abs(differenceEnergy / postEnergy);
                if (bitRate == 32_000) {
                    if (outputEnergyRatioDelta > 0.01 || residualEnergyRatio > 0.01) {
                        Console.WriteLine(
                            "ERROR OKI_ADPCM limits " +
                            $"bit_rate={bitRate} " +
                            "expected_output_energy_ratio_delta<=0.010000 " +
                            $"actual={outputEnergyRatioDelta:F6} " +
                            "expected_residual_energy_ratio<=0.010000 " +
                            $"actual={residualEnergyRatio:F6} " +
                            "input_buffer=BEFUELLT output_buffer=BEFUELLT");
                        Exit(2);
                    }
                } else {
                    if (outputEnergyRatioDelta > 0.11 || residualEnergyRatio > 0.05) {
                        Console.WriteLine(
                            "ERROR OKI_ADPCM limits " +
                            $"bit_rate={bitRate} " +
                            "expected_output_energy_ratio_delta<=0.110000 " +
                            $"actual={outputEnergyRatioDelta:F6} " +
                            "expected_residual_energy_ratio<=0.050000 " +
                            $"actual={residualEnergyRatio:F6} " +
                            "input_buffer=BEFUELLT output_buffer=BEFUELLT");
                        Exit(2);
                    }
                }

                oki_adpcm_free(encoder);
                encoder = null;
                if (input.Close() != 0) {
                    Console.Error.WriteLine(
                        $"    Cannot close audio file '{inputFileName}'");
                    Exit(2);
                }
                input = null;
            }

            oki_adpcm_free(decoder);
            decoder = null;
            oki_adpcm_free(byteDecoder);
            byteDecoder = null;

            if (output.Close() != 0) {
                Console.Error.WriteLine($"    Cannot close audio file '{OUT_FILE_NAME}'");
                Exit(2);
            }

            Console.WriteLine("Tests passed.");
            return 0;
        } finally {
            encodedInput?.Dispose();
            input?.Dispose();
            output.Dispose();
            oki_adpcm_free(encoder);
            oki_adpcm_free(decoder);
            oki_adpcm_free(byteDecoder);
        }
    }

    private sealed class TelephonyWaveReader : IDisposable {
        private readonly FileStream _stream;
        private readonly long _dataEnd;
        private bool _closed;

        public TelephonyWaveReader(string path) {
            _stream = File.OpenRead(path);
            using BinaryReader reader = new(_stream, Encoding.ASCII, leaveOpen: true);

            if (ReadFourCc(reader) != "RIFF")
                throw new InvalidDataException("RIFF header missing.");
            _ = reader.ReadUInt32();
            if (ReadFourCc(reader) != "WAVE")
                throw new InvalidDataException("WAVE header missing.");

            bool formatFound = false;
            bool dataFound = false;
            ushort audioFormat = 0;
            ushort channels = 0;
            ushort bitsPerSample = 0;
            uint sampleRate = 0;
            long dataStart = 0;
            uint dataLength = 0;

            while (_stream.Position + 8 <= _stream.Length) {
                string chunkId = ReadFourCc(reader);
                uint chunkLength = reader.ReadUInt32();
                long nextChunk = checked(
                    _stream.Position + chunkLength + (chunkLength & 1U));

                if (chunkId == "fmt ") {
                    if (chunkLength < 16)
                        throw new InvalidDataException("Invalid fmt chunk.");

                    audioFormat = reader.ReadUInt16();
                    channels = reader.ReadUInt16();
                    sampleRate = reader.ReadUInt32();
                    _ = reader.ReadUInt32();
                    _ = reader.ReadUInt16();
                    bitsPerSample = reader.ReadUInt16();
                    formatFound = true;
                } else if (chunkId == "data") {
                    dataStart = _stream.Position;
                    dataLength = chunkLength;
                    dataFound = true;
                }

                _stream.Position = Math.Min(nextChunk, _stream.Length);
                if (formatFound && dataFound)
                    break;
            }

            if (!formatFound ||
                !dataFound ||
                audioFormat != 1 ||
                channels != 1 ||
                bitsPerSample != 16 ||
                sampleRate != 8_000) {
                throw new InvalidDataException(
                    "Expected 8 kHz mono 16-bit PCM WAV data.");
            }

            _stream.Position = dataStart;
            _dataEnd = Math.Min(_stream.Length, checked(dataStart + dataLength));
        }

        public int ReadFrames(short[] destination, int maximumFrames) {
            if (_closed)
                throw new ObjectDisposedException(nameof(TelephonyWaveReader));

            int framesToRead = Math.Min(maximumFrames, destination.Length);
            long remainingBytes = _dataEnd - _stream.Position;
            framesToRead = Math.Min(framesToRead, checked((int)(remainingBytes / 2)));
            if (framesToRead <= 0)
                return 0;

            byte[] bytes = new byte[framesToRead * 2];
            int total = 0;
            while (total < bytes.Length) {
                int read = _stream.Read(bytes, total, bytes.Length - total);
                if (read == 0)
                    break;
                total += read;
            }

            int framesRead = total / 2;
            for (int i = 0; i < framesRead; i++) {
                destination[i] = BinaryPrimitives.ReadInt16LittleEndian(
                    bytes.AsSpan(i * 2, 2));
            }

            return framesRead;
        }

        public int Close() {
            if (_closed)
                return 0;

            try {
                _stream.Dispose();
                _closed = true;
                return 0;
            } catch (IOException) {
                return -1;
            }
        }

        public void Dispose() {
            _ = Close();
        }

        private static string ReadFourCc(BinaryReader reader) {
            byte[] bytes = reader.ReadBytes(4);
            if (bytes.Length != 4)
                throw new EndOfStreamException();
            return Encoding.ASCII.GetString(bytes);
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
