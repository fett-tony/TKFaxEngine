/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * gsm0610_tests.c - Test the GSM 06.10 FR codec.
 *
 * Written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2006 Steve Underwood
 * GNU General Public License version 2.
 *
 * Managed C# port. The test order, vector handling, comparisons, packing
 * checks and local audio path follow the supplied spanDSP test source.
 */

#nullable enable
using TKFaxEngine;

using System.Buffers.Binary;
using System.Text;
using TKFaxEngine.Audio;
using static TKFaxEngine.Audio.g711;
using TKFaxEngineTest;
using static TKFaxEngine.Audio.Gsm0610Api;

namespace TKFaxEngineTest.Audio.Test;

public static class Gsm0610Tests {
    private const int BLOCK_LEN = 160;
    private const string TESTDATA_DIR = "Test-Files/etsi/gsm0610/unpacked/fr_";
    private const string IN_FILE_NAME = "Test-Files/local/short_nb_voice.wav";
    private const string OUT_FILE_NAME = "post_gsm0610.wav";
    private const int HIST_LEN = 1000;
    private const int VECTOR_CAPACITY = 1_000_000;

    private static readonly byte[] law_in_vector = new byte[VECTOR_CAPACITY];
    private static readonly short[] in_vector = new short[VECTOR_CAPACITY];
    private static readonly ushort[] code_vector_buf = new ushort[VECTOR_CAPACITY];
    private static readonly byte[] code_vector = new byte[VECTOR_CAPACITY];
    private static readonly byte[] ref_code_vector = new byte[VECTOR_CAPACITY];
    private static readonly byte[] decoder_code_vector = new byte[VECTOR_CAPACITY];
    private static readonly byte[] law_out_vector = new byte[VECTOR_CAPACITY];
    private static readonly short[] out_vector = new short[VECTOR_CAPACITY];
    private static readonly short[] ref_out_vector = new short[VECTOR_CAPACITY];
    private static readonly byte[] ref_law_out_vector = new byte[VECTOR_CAPACITY];

    private static int vector_len;
    private static uint _randState = 1;

    private static int Rand() {
        _randState = unchecked(_randState * 214013U + 2531011U);
        return (int)((_randState >> 16) & 0x7FFFU);
    }

    private static void Exit(int code) => throw new TestProcessExitException(code);

    private static int ReadBytes(string path, byte[] destination) {
        try {
            using FileStream stream = File.OpenRead(path);
            int total = 0;
            while (total < destination.Length) {
                int read = stream.Read(destination, total, destination.Length - total);
                if (read == 0)
                    break;
                total += read;
            }
            return total;
        } catch (IOException) {
            Console.Error.WriteLine($"Cannot open {path}");
            Exit(2);
            return 0;
        } catch (UnauthorizedAccessException) {
            Console.Error.WriteLine($"Cannot open {path}");
            Exit(2);
            return 0;
        }
    }

    private static byte[] ReadFileLimited(string path, int maximumBytes) {
        try {
            using FileStream stream = File.OpenRead(path);
            byte[] bytes = new byte[Math.Min(maximumBytes, checked((int)Math.Min(stream.Length, maximumBytes)))];
            int total = 0;
            while (total < bytes.Length) {
                int read = stream.Read(bytes, total, bytes.Length - total);
                if (read == 0)
                    break;
                total += read;
            }
            if (total != bytes.Length)
                Array.Resize(ref bytes, total);
            return bytes;
        } catch (IOException) {
            Console.Error.WriteLine($"Cannot open {path}");
            Exit(2);
            return [];
        } catch (UnauthorizedAccessException) {
            Console.Error.WriteLine($"Cannot open {path}");
            Exit(2);
            return [];
        }
    }

    private static int ReadInt16(string path, short[] destination) {
        byte[] bytes = ReadFileLimited(path, VECTOR_CAPACITY);
        int count = Math.Min(bytes.Length / sizeof(short), destination.Length);
        for (int i = 0; i < count; i++)
            destination[i] = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(i * 2, 2));
        return count;
    }

    private static int ReadUInt16(string path, ushort[] destination) {
        byte[] bytes = ReadFileLimited(path, VECTOR_CAPACITY);
        int count = Math.Min(bytes.Length / sizeof(ushort), destination.Length);
        for (int i = 0; i < count; i++)
            destination[i] = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i * 2, 2));
        return count;
    }

    private static int get_test_vector(bool full, int disk, string name) {
        _ = disk;
        if (full) {
            string inputPath = $"{TESTDATA_DIR}L/{name}.inp";
            vector_len = ReadInt16(inputPath, in_vector);
        }

        string outputPath = $"{TESTDATA_DIR}L/{name}.out";
        int length = ReadInt16(outputPath, ref_out_vector);
        if (full) {
            if (length != vector_len) {
                Console.Error.WriteLine(
                    $"Input and reference vector lengths do not match - {vector_len} {length}");
                Exit(2);
            }
        } else {
            vector_len = length;
        }

        string codePath = $"{TESTDATA_DIR}L/{name}.cod";
        length = ReadUInt16(codePath, code_vector_buf);
        for (int i = 0; i < length; i++) {
            ref_code_vector[i] = unchecked((byte)code_vector_buf[i]);
            decoder_code_vector[i] = unchecked((byte)code_vector_buf[i]);
        }

        if (length * BLOCK_LEN != vector_len * 76) {
            Console.Error.WriteLine(
                $"Input and code vector lengths do not match - {vector_len} {length}");
            Exit(2);
        }

        return length;
    }

    private static int get_law_test_vector(bool full, char law, string name) {
        char lawUpper = char.ToUpperInvariant(law);

        if (full) {
            string inputPath = $"{TESTDATA_DIR}{lawUpper}/{name}-{lawUpper}.inp";
            vector_len = ReadBytes(inputPath, law_in_vector);

            string encodedPath = $"{TESTDATA_DIR}{lawUpper}/{name}-{lawUpper}.cod";
            int encodedLength = ReadUInt16(encodedPath, code_vector_buf);
            for (int i = 0; i < encodedLength; i++)
                ref_code_vector[i] = unchecked((byte)code_vector_buf[i]);

            if (encodedLength * BLOCK_LEN != vector_len * 76) {
                Console.Error.WriteLine(
                    $"Input and code vector lengths do not match - {vector_len} {encodedLength}");
                Exit(2);
            }
        }

        string outputPath = $"{TESTDATA_DIR}{lawUpper}/{name}-{lawUpper}.out";
        int length = ReadBytes(outputPath, ref_law_out_vector);
        if (full) {
            if (length != vector_len) {
                Console.Error.WriteLine(
                    $"Input and reference vector lengths do not match - {vector_len} {length}");
                Exit(2);
            }
        } else {
            vector_len = length;
        }

        string decoderPath = $"{TESTDATA_DIR}L/{name}.cod";
        length = ReadUInt16(decoderPath, code_vector_buf);
        for (int i = 0; i < length; i++)
            decoder_code_vector[i] = unchecked((byte)code_vector_buf[i]);

        return length;
    }

    private static int perform_linear_test(bool full, int disk, string name) {
        Console.WriteLine($"Performing linear test '{name}' from disk {disk}");
        get_test_vector(full, disk, name);

        if (full) {
            Gsm0610State gsm0610EncState = gsm0610_init(null, GSM0610_PACKING_NONE);
            int encodedLength = gsm0610_encode(
                gsm0610EncState,
                code_vector,
                in_vector,
                vector_len);

            Console.WriteLine($"Check code vector of length {encodedLength}");
            int mismatches = 0;
            for (int i = 0; i < encodedLength; i++) {
                if (code_vector[i] == ref_code_vector[i])
                    continue;

                Console.WriteLine(
                    $"{i / 76,8}/{i % 76,3}: {code_vector[i],6} {ref_code_vector[i],6}");
                mismatches++;
            }

            gsm0610_free(gsm0610EncState);
            if (mismatches != 0) {
                Console.WriteLine(
                    $"Test failed: {mismatches} of {encodedLength} samples mismatch");
                Exit(2);
            }
            Console.WriteLine("Test passed");
        }

        Gsm0610State gsm0610DecState = gsm0610_init(null, GSM0610_PACKING_NONE);
        _ = gsm0610_decode(
            gsm0610DecState,
            out_vector,
            decoder_code_vector,
            vector_len);

        Console.WriteLine($"Check output vector of length {vector_len}");
        int outputMismatches = 0;
        for (int i = 0; i < vector_len; i++) {
            if (out_vector[i] == ref_out_vector[i])
                continue;

            Console.WriteLine($"{i,8}: {out_vector[i],6} {ref_out_vector[i],6}");
            outputMismatches++;
        }

        if (outputMismatches != 0) {
            Console.WriteLine(
                $"Test failed: {outputMismatches} of {vector_len} samples mismatch");
            Exit(2);
        }

        gsm0610_free(gsm0610DecState);
        Console.WriteLine("Test passed");
        return 0;
    }

    private static int perform_law_test(bool full, char law, string name) {
        Console.WriteLine(
            law == 'a'
                ? $"Performing A-law test '{name}'"
                : $"Performing u-law test '{name}'");

        get_law_test_vector(full, law, name);

        if (full) {
            Gsm0610State gsm0610EncState = gsm0610_init(null, GSM0610_PACKING_NONE);

            if (law == 'a') {
                for (int i = 0; i < vector_len; i++)
                    in_vector[i] = alaw_to_linear(law_in_vector[i]);
            } else {
                for (int i = 0; i < vector_len; i++)
                    in_vector[i] = ulaw_to_linear(law_in_vector[i]);
            }

            int encodedLength = gsm0610_encode(
                gsm0610EncState,
                code_vector,
                in_vector,
                vector_len);

            Console.WriteLine($"Check code vector of length {encodedLength}");
            int mismatches = 0;
            for (int i = 0; i < encodedLength; i++) {
                if (code_vector[i] == ref_code_vector[i])
                    continue;

                Console.WriteLine(
                    $"{i / 76,8}/{i % 76,3}: {code_vector[i],6} {ref_code_vector[i],6} {decoder_code_vector[i],6}");
                mismatches++;
            }

            if (mismatches != 0) {
                Console.WriteLine(
                    $"Test failed: {mismatches} of {encodedLength} samples mismatch");
                Exit(2);
            }

            Console.WriteLine("Test passed");
            gsm0610_free(gsm0610EncState);
        }

        Gsm0610State gsm0610DecState = gsm0610_init(null, GSM0610_PACKING_NONE);
        _ = gsm0610_decode(
            gsm0610DecState,
            out_vector,
            decoder_code_vector,
            vector_len);

        if (law == 'a') {
            for (int i = 0; i < vector_len; i++)
                law_out_vector[i] = linear_to_alaw(out_vector[i]);
        } else {
            for (int i = 0; i < vector_len; i++)
                law_out_vector[i] = linear_to_ulaw(out_vector[i]);
        }

        Console.WriteLine($"Check output vector of length {vector_len}");
        int outputMismatches = 0;
        for (int i = 0; i < vector_len; i++) {
            if (law_out_vector[i] == ref_law_out_vector[i])
                continue;

            Console.WriteLine(
                $"{i,8}: {law_out_vector[i],6} {ref_law_out_vector[i],6}");
            outputMismatches++;
        }

        if (outputMismatches != 0) {
            Console.WriteLine(
                $"Test failed: {outputMismatches} of {vector_len} samples mismatch");
            Exit(2);
        }

        gsm0610_free(gsm0610DecState);
        Console.WriteLine("Test passed");
        return 0;
    }

    private static int repack_gsm0610_voip_to_wav49(byte[] c, byte[] d) {
        Gsm0610Frame[] frames = [new Gsm0610Frame(), new Gsm0610Frame()];

        byte[] firstVoipFrame = new byte[33];
        byte[] secondVoipFrame = new byte[33];
        Array.Copy(d, 0, firstVoipFrame, 0, firstVoipFrame.Length);
        int firstLength = gsm0610_unpack_voip(frames[0], firstVoipFrame);
        Array.Copy(d, firstLength, secondVoipFrame, 0, secondVoipFrame.Length);
        _ = gsm0610_unpack_voip(frames[1], secondVoipFrame);

        return gsm0610_pack_wav49(c, frames);
    }

    private static int repack_gsm0610_wav49_to_voip(byte[] d, byte[] c) {
        Gsm0610Frame[] frames = [new Gsm0610Frame(), new Gsm0610Frame()];
        _ = gsm0610_unpack_wav49(frames, c);

        byte[] first = new byte[33];
        byte[] second = new byte[33];
        int firstLength = gsm0610_pack_voip(first, frames[0]);
        int secondLength = gsm0610_pack_voip(second, frames[1]);
        Array.Copy(first, 0, d, 0, firstLength);
        Array.Copy(second, 0, d, firstLength, secondLength);
        return firstLength + secondLength;
    }

    private static int perform_pack_unpack_test() {
        byte[] a = new byte[66];
        byte[] b = new byte[66];
        byte[] c = new byte[66];

        Console.WriteLine(
            "Performing packing/unpacking tests (not part of the ETSI conformance tests).");

        for (int j = 0; j < 1000; j++) {
            for (int i = 0; i < 65; i++)
                a[i] = unchecked((byte)Rand());

            _ = repack_gsm0610_wav49_to_voip(b, a);
            _ = repack_gsm0610_voip_to_wav49(c, b);
            if (!a.AsSpan(0, 65).SequenceEqual(c.AsSpan(0, 65))) {
                Console.WriteLine("Test failed: data mismatch");
                Exit(2);
            }

            for (int i = 0; i < 66; i++)
                a[i] = unchecked((byte)Rand());

            a[0] = (byte)((a[0] & 0x0F) | 0xD0);
            a[33] = (byte)((a[33] & 0x0F) | 0xD0);
            _ = repack_gsm0610_voip_to_wav49(b, a);
            _ = repack_gsm0610_wav49_to_voip(c, b);
            if (!a.AsSpan(0, 66).SequenceEqual(c.AsSpan(0, 66))) {
                Console.WriteLine("Test failed: data mismatch");
                Exit(2);
            }
        }

        Console.WriteLine("Test passed");
        return 0;
    }

    private static void etsi_compliance_tests() {
        _ = perform_linear_test(true, 1, "Seq01");
        _ = perform_linear_test(true, 1, "Seq02");
        _ = perform_linear_test(true, 1, "Seq03");
        _ = perform_linear_test(true, 1, "Seq04");
        _ = perform_linear_test(false, 1, "Seq05");

        _ = perform_law_test(true, 'a', "Seq01");
        _ = perform_law_test(true, 'a', "Seq02");
        _ = perform_law_test(true, 'a', "Seq03");
        _ = perform_law_test(true, 'a', "Seq04");
        _ = perform_law_test(false, 'a', "Seq05");

        _ = perform_law_test(true, 'u', "Seq01");
        _ = perform_law_test(true, 'u', "Seq02");
        _ = perform_law_test(true, 'u', "Seq03");
        _ = perform_law_test(true, 'u', "Seq04");
        _ = perform_law_test(false, 'u', "Seq05");

        _ = perform_pack_unpack_test();
        Console.WriteLine("Tests passed.");
    }

    public static int tkfx_gsm0610_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        bool etsiTests = true;
        int packing = GSM0610_PACKING_NONE;

        for (int argumentIndex = 0; argumentIndex < testArguments.Length; argumentIndex++) {
            string argument = testArguments[argumentIndex];
            if (argument == "--")
                break;
            if (argument.Length < 2 || (argument[0] != '-' && argument[0] != '/'))
                break;

            for (int optionIndex = 1; optionIndex < argument.Length; optionIndex++) {
                switch (argument[optionIndex]) {
                    case 'l':
                        etsiTests = false;
                        break;

                    case 'p': {
                        string? optionValue;
                        if (optionIndex + 1 < argument.Length) {
                            optionValue = argument[(optionIndex + 1)..];
                        } else if (++argumentIndex < testArguments.Length) {
                            optionValue = testArguments[argumentIndex];
                        } else {
                            Exit(2);
                            return 2;
                        }

                        if (!int.TryParse(optionValue, out packing))
                            packing = 0;
                        optionIndex = argument.Length;
                        break;
                    }

                    default:
                        Exit(2);
                        return 2;
                }
            }
        }

        if (etsiTests) {
            etsi_compliance_tests();
        } else {
            TelephonyWaveReader inhandle;
            TelephonyWaveWriter outhandle;
            try {
                inhandle = new TelephonyWaveReader(IN_FILE_NAME);
            } catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or InvalidDataException) {
                Console.Error.WriteLine($"    Cannot open audio file '{IN_FILE_NAME}'");
                Exit(2);
                return 2;
            }

            try {
                outhandle = new TelephonyWaveWriter(OUT_FILE_NAME, 8000);
            } catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException) {
                inhandle.Dispose();
                Console.Error.WriteLine($"    Cannot create audio file '{OUT_FILE_NAME}'");
                Exit(2);
                return 2;
            }

            Gsm0610State gsm0610EncState = gsm0610_init(null, packing);
            Gsm0610State gsm0610DecState = gsm0610_init(null, packing);
            short[] preAmp = new short[HIST_LEN];
            short[] postAmp = new short[HIST_LEN];
            byte[] gsm0610Data = new byte[HIST_LEN];

            try {
                int frames;
                while ((frames = inhandle.ReadFrames(preAmp, 2 * BLOCK_LEN)) != 0) {
                    int bytes = gsm0610_encode(
                        gsm0610EncState,
                        gsm0610Data,
                        preAmp,
                        frames);
                    _ = gsm0610_decode(
                        gsm0610DecState,
                        postAmp,
                        gsm0610Data,
                        bytes);
                    outhandle.WriteFrames(postAmp, frames);
                }
            } finally {
                inhandle.Dispose();
                outhandle.Dispose();
                gsm0610_free(gsm0610EncState);
                gsm0610_free(gsm0610DecState);
            }
        }

        return 0;
    }

    private sealed class TelephonyWaveReader : IDisposable {
        private readonly FileStream _stream;
        private readonly long _dataEnd;
        private bool _disposed;

        public TelephonyWaveReader(string path) {
            _stream = File.OpenRead(path);
            using BinaryReader reader = new(_stream, Encoding.ASCII, leaveOpen: true);

            if (new string(reader.ReadChars(4)) != "RIFF")
                throw new InvalidDataException("RIFF header missing.");
            _ = reader.ReadUInt32();
            if (new string(reader.ReadChars(4)) != "WAVE")
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
                string chunkId = new(reader.ReadChars(4));
                uint chunkLength = reader.ReadUInt32();
                long nextChunk = checked(_stream.Position + chunkLength + (chunkLength & 1));

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

            if (!formatFound || !dataFound || audioFormat != 1 || channels != 1 || bitsPerSample != 16 || sampleRate != 8000)
                throw new InvalidDataException("Expected 8 kHz mono 16-bit PCM WAV data.");

            SampleRate = checked((int)sampleRate);
            _stream.Position = dataStart;
            _dataEnd = Math.Min(_stream.Length, checked(dataStart + dataLength));
        }

        public int SampleRate { get; }

        public int ReadFrames(short[] destination, int maximumFrames) {
            ObjectDisposedException.ThrowIf(_disposed, this);
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
            for (int i = 0; i < framesRead; i++)
                destination[i] = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(i * 2, 2));
            return framesRead;
        }

        public void Dispose() {
            if (_disposed)
                return;
            _disposed = true;
            _stream.Dispose();
        }
    }

    private sealed class TelephonyWaveWriter : IDisposable {
        private readonly FileStream _stream;
        private readonly BinaryWriter _writer;
        private uint _dataLength;
        private bool _disposed;

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

        public void WriteFrames(short[] source, int frames) {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (frames < 0 || frames > source.Length)
                throw new ArgumentOutOfRangeException(nameof(frames));

            for (int i = 0; i < frames; i++)
                _writer.Write(source[i]);
            _dataLength = checked(_dataLength + (uint)(frames * 2));
        }

        public void Dispose() {
            if (_disposed)
                return;
            _disposed = true;

            _writer.Flush();
            _stream.Position = 4;
            _writer.Write(checked(36U + _dataLength));
            _stream.Position = 40;
            _writer.Write(_dataLength);
            _writer.Flush();
            _writer.Dispose();
            _stream.Dispose();
        }

        private void WriteFourCc(string value) =>
            _writer.Write(Encoding.ASCII.GetBytes(value));
    }
}
