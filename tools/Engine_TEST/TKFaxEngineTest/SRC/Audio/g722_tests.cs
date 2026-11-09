/* TKFaxEngine - g722_tests.cs. Managed 1:1 test-flow port of spanDSP g722_tests.c. */
#nullable enable

using System.Globalization;
using TKFaxEngine.Audio;
using static TKFaxEngine.Audio.Dds;
using static TKFaxEngine.Audio.g722;
using static TKFaxEngine.Audio.PowerMeterApi;
using static TKFaxEngine.Audio.SweptToneApi;

namespace TKFaxEngineTest.Audio.Test;

public static class g722_tests {
    private const int G722_SAMPLE_RATE = 16000, BLOCK_LEN = 320, MAX_TEST_VECTOR_LEN = 40000;
    private const string TESTDATA_DIR = "Test-Files/itu/g722/", EIGHTK_IN_FILE_NAME = "Test-Files/local/short_nb_voice.wav", IN_FILE_NAME = "Test-Files/local/short_wb_voice.wav", ENCODED_FILE_NAME = "g722.g722", OUT_FILE_NAME = "post_g722.wav";
    private static readonly string[] encode_test_files = { TESTDATA_DIR + "T1C1.XMT", TESTDATA_DIR + "T2R1.COD", TESTDATA_DIR + "T1C2.XMT", TESTDATA_DIR + "T2R2.COD" };
    private static readonly string[] decode_test_files = { TESTDATA_DIR + "T2R1.COD", TESTDATA_DIR + "T3L1.RC1", TESTDATA_DIR + "T3L1.RC2", TESTDATA_DIR + "T3L1.RC3", TESTDATA_DIR + "T3H1.RC0", TESTDATA_DIR + "T2R2.COD", TESTDATA_DIR + "T3L2.RC1", TESTDATA_DIR + "T3L2.RC2", TESTDATA_DIR + "T3L2.RC3", TESTDATA_DIR + "T3H2.RC0", TESTDATA_DIR + "T1D3.COD", TESTDATA_DIR + "T3L3.RC1", TESTDATA_DIR + "T3L3.RC2", TESTDATA_DIR + "T3L3.RC3", TESTDATA_DIR + "T3H3.RC0" };
    public sealed class G722TestsExitException : Exception { public G722TestsExitException(int exitCode) : base($"g722_tests exited with code {exitCode}.") => ExitCode = exitCode; public int ExitCode { get; } }
    private static void Exit(int code) => throw new G722TestsExitException(code);
    public static int main(string[] testArguments) { try { return tkfx_g722_tests_body_main(testArguments); } catch (G722TestsExitException caught) { return caught.ExitCode; } }
    private static ushort[] GetTestVector(string path) {
        List<ushort> values = new(MAX_TEST_VECTOR_LEN);
        foreach (string raw in File.ReadLines(path)) { string line = raw.Trim(); if (line.StartsWith("/*", StringComparison.Ordinal)) continue; for (int pos = 0; pos + 4 <= line.Length; pos += 4) { if (!ushort.TryParse(line.AsSpan(pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort value)) break; values.Add(value); if (values.Count > MAX_TEST_VECTOR_LEN) throw new InvalidDataException(); } }
        return [.. values];
    }
    private static void ItuComplianceTests() {
        byte[] compressed = new byte[MAX_TEST_VECTOR_LEN]; short[] decompressed = new short[MAX_TEST_VECTOR_LEN];
        for (int file = 0; file < encode_test_files.Length; file += 2) {
            Console.WriteLine($"Testing {encode_test_files[file]} -> {encode_test_files[file + 1]}"); ushort[] source = GetTestVector(encode_test_files[file]), reference = GetTestVector(encode_test_files[file + 1]);
            if (source.Length != reference.Length) { Console.WriteLine("Test data length mismatch"); Exit(2); }
            int first = 0; while (first < source.Length && (source[first] & 1) != 0) first++; int last = first; while (last < source.Length && (source[last] & 1) == 0) last++; int count = last - first; short[] input = new short[count]; for (int i = 0; i < count; i++) input[i] = unchecked((short)source[first + i]);
            g722_encode_state_t encoder = g722_encode_init(null, 64000, 0) ?? throw new InvalidOperationException("g722_encode_init failed."); encoder.itu_test_mode = true; int outputLength = g722_encode(encoder, compressed, input, count); int bad = 0;
            for (int i = 0; i < outputLength; i++) if (compressed[i] != ((reference[first + i] >> 8) & 0xFF)) { Console.WriteLine($">>> {i,6} {compressed[i],4:x} {reference[first + i],4:x}"); bad++; }
            Console.WriteLine($"{bad} bad samples, out of {count}/{source.Length} samples");
            if (bad != 0) { Console.WriteLine("Test failed"); g722_encode_free(encoder); Exit(2); }
            Console.WriteLine("Test passed"); g722_encode_free(encoder);
        }
        for (int mode = 1; mode <= 3; mode++) for (int file = 0; file < decode_test_files.Length; file += 5) {
            Console.WriteLine($"Testing mode {mode}, {decode_test_files[file]} -> {decode_test_files[file + mode]} + {decode_test_files[file + 4]}"); ushort[] source = GetTestVector(decode_test_files[file]), lower = GetTestVector(decode_test_files[file + mode]), upper = GetTestVector(decode_test_files[file + 4]);
            if (source.Length != lower.Length || source.Length != upper.Length) { Console.WriteLine("Test data length mismatch"); Exit(2); }
            int first = 0; while (first < source.Length && (source[first] & 1) != 0) first++; int last = first; while (last < source.Length && (source[last] & 1) == 0) last++; int count = last - first, shift = mode == 3 ? 10 : mode == 2 ? 9 : 8; for (int i = 0; i < count; i++) compressed[i] = (byte)(source[first + i] >> shift); int rate = mode == 3 ? 48000 : mode == 2 ? 56000 : 64000;
            g722_decode_state_t decoder = g722_decode_init(null, rate, 0) ?? throw new InvalidOperationException("g722_decode_init failed."); decoder.itu_test_mode = true; int outputLength = g722_decode(decoder, decompressed, compressed, count), bad = 0;
            for (int i = 0; i + 1 < outputLength; i += 2) { int index = (i >> 1) + first; if ((ushort)decompressed[i] != lower[index] || (ushort)decompressed[i + 1] != upper[index]) { Console.WriteLine($">>> {i >> 1,6} {(ushort)decompressed[i],4:x} {(ushort)decompressed[i + 1],4:x} {lower[index],4:x} {upper[index],4:x}"); bad++; } }
            Console.WriteLine($"{bad} bad samples, out of {count}/{source.Length} samples");
            if (bad != 0) { Console.WriteLine("Test failed"); g722_decode_free(decoder); Exit(2); }
            Console.WriteLine("Test passed"); g722_decode_free(decoder);
        }
        Console.WriteLine("Tests passed.");
    }
    private static void SignalToDistortionTests() {
        g722_encode_state_t encoder = g722_encode_init(null, 64000, 0) ?? throw new InvalidOperationException("g722_encode_init failed."); g722_decode_state_t decoder = g722_decode_init(null, 64000, 0) ?? throw new InvalidOperationException("g722_decode_init failed."); PowerMeterState inputMeter = power_meter_init(null, 7) ?? throw new InvalidOperationException(), outputMeter = power_meter_init(null, 7) ?? throw new InvalidOperationException();
        short[] original = new short[1024], decompressed = new short[2048]; byte[] compressed = new byte[1024]; int inputLevel = 0, outputLevel = 0; foreach (short sample in original) inputLevel = power_meter_update(inputMeter, sample); int bytes = g722_encode(encoder, compressed, original, original.Length); int samples = g722_decode(decoder, decompressed, compressed, bytes); for (int i = 0; i < samples; i++) outputLevel = power_meter_update(outputMeter, decompressed[i]); Console.WriteLine($"Silence produces {outputLevel} at the output");
        SweptToneState swept = swept_tone_init(null, 25.0f, 3500.0f, -10.0f, 60 * G722_SAMPLE_RATE, 0); int count; do { count = swept_tone(swept, original, original.Length); for (int i = 0; i < count; i++) inputLevel = power_meter_update(inputMeter, original[i]); bytes = g722_encode(encoder, compressed, original, count); samples = g722_decode(decoder, decompressed, compressed, bytes); for (int i = 0; i < samples; i++) outputLevel = power_meter_update(outputMeter, decompressed[i]); Console.WriteLine($"{inputLevel,10}, {outputLevel,10}, {(inputLevel == 0 ? 0 : (double)outputLevel / inputLevel).ToString("F6", CultureInfo.InvariantCulture)}"); } while (count > 0); swept_tone_free(swept);
        g722_encode_free(encoder); g722_decode_free(decoder); power_meter_free(inputMeter); power_meter_free(outputMeter);
    }
    private static short Saturate(double value) => value > short.MaxValue ? short.MaxValue : value < short.MinValue ? short.MinValue : (short)Math.Round(value);
    public static int tkfx_g722_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments); int bitRate = 64000; bool eightKIn = false, eightKOut = false, ituTests = true, encode = false, decode = false, toneTest = false; string? inputPath = null, outputPath = null;
        int ir = 0, orate = 0;
        for (int i = 0; i < testArguments.Length; i++) {
            switch (testArguments[i]) {
                case "-b":
                    if (++i >= testArguments.Length || !int.TryParse(testArguments[i], out bitRate) || (bitRate != 48000 && bitRate != 56000 && bitRate != 64000)) {
                        Console.Error.WriteLine("Invalid bit rate selected. Only 48000, 56000 and 64000 are valid.");
                        Exit(2);
                    }
                    ituTests = false;
                    break;
                case "-d": if (++i >= testArguments.Length) Exit(2); inputPath = testArguments[i]; decode = true; ituTests = false; break;
                case "-e": if (++i >= testArguments.Length) Exit(2); inputPath = testArguments[i]; encode = true; ituTests = false; break;
                case "-i":
                    if (++i >= testArguments.Length || !int.TryParse(testArguments[i], out ir) || (ir != 8000 && ir != 16000)) {
                        Console.Error.WriteLine("Invalid incoming sample rate. Only 8000 and 16000 are valid.");
                        Exit(2);
                    }
                    eightKIn = ir == 8000; if (eightKIn) inputPath = EIGHTK_IN_FILE_NAME;
                    break;
                case "-l": if (++i >= testArguments.Length) Exit(2); outputPath = testArguments[i]; break;
                case "-o":
                    if (++i >= testArguments.Length || !int.TryParse(testArguments[i], out orate) || (orate != 8000 && orate != 16000)) {
                        Console.Error.WriteLine("Invalid outgoing sample rate. Only 8000 and 16000 are valid.");
                        Exit(2);
                    }
                    eightKOut = orate == 8000;
                    break;
                case "-t": toneTest = true; ituTests = false; break;
                default: if (testArguments[i].StartsWith('-')) Exit(2); break;
            }
        }
        if (ituTests) { ItuComplianceTests(); SignalToDistortionTests(); return 0; }
        if (!decode && !encode) decode = encode = true; inputPath ??= encode ? (eightKIn ? EIGHTK_IN_FILE_NAME : IN_FILE_NAME) : ENCODED_FILE_NAME; outputPath ??= decode ? OUT_FILE_NAME : ENCODED_FILE_NAME;
        int encoderOptions = G722_PACKED | (eightKIn ? G722_SAMPLE_RATE_8000 : 0), decoderOptions = G722_PACKED | (eightKOut ? G722_SAMPLE_RATE_8000 : 0); g722_encode_state_t? encoder = encode ? g722_encode_init(null, bitRate, encoderOptions) : null; g722_decode_state_t? decoder = decode ? g722_decode_init(null, bitRate, decoderOptions) : null;
        using Pcm16WaveReader? reader = encode ? new Pcm16WaveReader(inputPath, eightKIn ? 8000 : 16000, 1) : null; using FileStream? encodedInput = !encode ? File.OpenRead(inputPath) : null; using Pcm16WaveWriter? writer = decode ? new Pcm16WaveWriter(outputPath, eightKOut ? 8000 : 16000, 1) : null; using FileStream? encodedOutput = !decode ? File.Create(outputPath) : null;
        short[] input = new short[BLOCK_LEN], output = new short[BLOCK_LEN * 2]; byte[] data = new byte[BLOCK_LEN]; uint phase = 0; int phaseRate = dds_phase_ratef(750f); float toneLevel = dds_scaling_dbm0f(2.5f);
        while (true) { int bytes; if (encode) { int count = reader!.ReadFrames(input); if (count <= 0) break; if (toneTest) for (int i = 0; i < count; i++) input[i] = Saturate(dds_modf(ref phase, phaseRate, toneLevel, 0)); bytes = g722_encode(encoder!, data, input, count); } else { bytes = encodedInput!.Read(data, 0, data.Length); if (bytes <= 0) break; } if (decode) { int count = g722_decode(decoder!, output, data, bytes); writer!.WriteFrames(output.AsSpan(0, count)); } else encodedOutput!.Write(data, 0, bytes); }
        if (encoder is not null) g722_encode_free(encoder); if (decoder is not null) g722_decode_free(decoder); Console.WriteLine($"'{inputPath}' translated to '{outputPath}' at {bitRate}bps."); return 0;
    }

    private sealed class Pcm16WaveReader : IDisposable {
        private readonly BinaryReader _reader;
        private readonly int _channels;
        private long _remaining;
        public Pcm16WaveReader(string path, int expectedRate, int expectedChannels) {
            _reader = new BinaryReader(File.OpenRead(path));
            if (new string(_reader.ReadChars(4)) != "RIFF") throw new InvalidDataException("Not RIFF");
            _reader.ReadUInt32();
            if (new string(_reader.ReadChars(4)) != "WAVE") throw new InvalidDataException("Not WAVE");
            int format = 0, channels = 0, rate = 0, bits = 0;
            while (_reader.BaseStream.Position + 8 <= _reader.BaseStream.Length) {
                string id = new(_reader.ReadChars(4));
                int size = _reader.ReadInt32();
                long next = _reader.BaseStream.Position + size + (size & 1);
                if (id == "fmt ") {
                    format = _reader.ReadUInt16(); channels = _reader.ReadUInt16(); rate = _reader.ReadInt32();
                    _reader.ReadInt32(); _reader.ReadUInt16(); bits = _reader.ReadUInt16();
                } else if (id == "data") { _remaining = size; break; }
                _reader.BaseStream.Position = next;
            }
            if (format != 1 || bits != 16 || channels != expectedChannels || rate != expectedRate)
                throw new InvalidDataException($"Unexpected WAV format in '{path}'.");
            _channels = channels;
        }
        public int ReadFrames(Span<short> destination) {
            int count = 0;
            while (count < destination.Length && _remaining >= 2L * _channels) {
                destination[count++] = _reader.ReadInt16();
                for (int channel = 1; channel < _channels; channel++) _reader.ReadInt16();
                _remaining -= 2L * _channels;
            }
            return count;
        }
        public void Dispose() => _reader.Dispose();
    }

    private sealed class Pcm16WaveWriter : IDisposable {
        private readonly BinaryWriter _writer;
        private readonly int _rate;
        private readonly short _channels;
        private long _samples;
        public Pcm16WaveWriter(string path, int rate, int channels) {
            _rate = rate; _channels = checked((short)channels); _writer = new BinaryWriter(File.Create(path));
            _writer.Write(global::System.Text.Encoding.ASCII.GetBytes("RIFF")); _writer.Write(0);
            _writer.Write(global::System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); _writer.Write(16); _writer.Write((short)1);
            _writer.Write(_channels); _writer.Write(_rate); _writer.Write(_rate * _channels * 2);
            _writer.Write((short)(_channels * 2)); _writer.Write((short)16);
            _writer.Write(global::System.Text.Encoding.ASCII.GetBytes("data")); _writer.Write(0);
        }
        public void WriteFrames(ReadOnlySpan<short> samples) {
            foreach (short sample in samples) _writer.Write(sample);
            _samples += samples.Length;
        }
        public void Dispose() {
            long end = _writer.BaseStream.Position;
            _writer.BaseStream.Position = 4; _writer.Write(checked((int)(end - 8)));
            _writer.BaseStream.Position = 40; _writer.Write(checked((int)(_samples * 2)));
            _writer.BaseStream.Position = end; _writer.Dispose();
        }
    }

}
