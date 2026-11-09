/*
 * TKFaxEngine - a series of DSP components for telephony
 * g711_tests.cs - A-law and u-law conversion tests.
 * Original C test written by Steve Underwood. Managed C# port preserving the complete test flow.
 */
#nullable enable

using System.Globalization;
using TKFaxEngine.Audio;
using static TKFaxEngine.Audio.g711;
using static TKFaxEngine.Audio.PowerMeterApi;

namespace TKFaxEngineTest.Audio.Test;

public static class g711_tests {
    private const int BLOCK_LEN = 160;
    private const string IN_FILE_NAME = "Test-Files/local/short_nb_voice.wav";
    private const string ENCODED_FILE_NAME = "g711.g711";
    private const string OUT_FILE_NAME = "post_g711.wav";
    private static readonly short[] amp = new short[65536];
    private static readonly byte[] ulaw_data = new byte[65536];
    private static readonly byte[] alaw_data = new byte[65536];
    private static readonly byte[] alaw_1khz_sine = { 0x34, 0x21, 0x21, 0x34, 0xB4, 0xA1, 0xA1, 0xB4 };
    private static readonly byte[] ulaw_1khz_sine = { 0x1E, 0x0B, 0x0B, 0x1E, 0x9E, 0x8B, 0x8B, 0x9E };

    public sealed class G711TestsExitException : Exception { public G711TestsExitException(int exitCode) : base($"g711_tests exited with code {exitCode}.") => ExitCode = exitCode; public int ExitCode { get; } }
    private static void Exit(int code) => throw new G711TestsExitException(code);
    public static int main(string[] testArguments) { try { return tkfx_g711_tests_body_main(testArguments); } catch (G711TestsExitException caught) { return caught.ExitCode; } }

    private static void ComplianceTests(bool logAudio) {
        using Pcm16WaveWriter? output = logAudio ? new Pcm16WaveWriter(OUT_FILE_NAME, 8000, 1) : null;
        PowerMeterState meter = power_meter_init(null, 7) ?? throw new InvalidOperationException("power_meter_init failed");
        int alawFailures = 0, ulawFailures = 0; float worstAlaw = 0, worstUlaw = 0;
        Console.WriteLine("Conversion accuracy tests.");
        for (int i = 0; i < 65536; i++) {
            int pre = i - 32768, post = alaw_to_linear(linear_to_alaw(i - 32768));
            if (Math.Abs(pre) > 140) { float error = (float)Math.Abs(post - pre) / Math.Abs(pre); if (error > .10f) { Console.WriteLine($"A-law: Excessive error at {pre} ({post})"); alawFailures++; } worstAlaw = Math.Max(worstAlaw, error); } else if (Math.Abs(post - pre) > 15) { Console.WriteLine($"A-law: Excessive error at {pre} ({post})"); alawFailures++; }
            amp[i] = (short)post;
        }
        output?.WriteFrames(amp);
        for (int i = 0; i < 65536; i++) {
            int pre = i - 32768, post = ulaw_to_linear(linear_to_ulaw(i - 32768));
            if (Math.Abs(pre) > 40) { float error = (float)Math.Abs(post - pre) / Math.Abs(pre); if (error > .10f) { Console.WriteLine($"u-law: Excessive error at {pre} ({post})"); ulawFailures++; } worstUlaw = Math.Max(worstUlaw, error); } else if (Math.Abs(post - pre) > 4) { Console.WriteLine($"u-law: Excessive error at {pre} ({post})"); ulawFailures++; }
            amp[i] = (short)post;
        }
        output?.WriteFrames(amp);
        Console.WriteLine($"Worst A-law error (ignoring small values) {(worstAlaw * 100).ToString("F6", CultureInfo.InvariantCulture)}%");
        Console.WriteLine($"Worst u-law error (ignoring small values) {(worstUlaw * 100).ToString("F6", CultureInfo.InvariantCulture)}%");
        if (alawFailures != 0 || ulawFailures != 0) { Console.WriteLine($"{alawFailures} A-law values with excessive error"); Console.WriteLine($"{ulawFailures} u-law values with excessive error"); Exit(2); }

        Console.WriteLine("Cyclic conversion repeatability tests.");
        for (int i = 0; i < 65536; i++) {
            int pre = i - 32768; int post = alaw_to_linear(linear_to_alaw(pre)); int second = alaw_to_linear(linear_to_alaw(post));
            if (second != post) { Console.WriteLine($"A-law second round trip mismatch - at {pre}, {post} != {second}"); Exit(2); }
            post = ulaw_to_linear(linear_to_ulaw(pre)); second = ulaw_to_linear(linear_to_ulaw(post));
            if (second != post) { Console.WriteLine($"u-law round trip mismatch - at {pre}, {post} != {second}"); Exit(2); }
        }

        Console.WriteLine("Reference power level tests.");
        meter.Initialize(7);
        for (int i = 0; i < 8000; i++) { amp[i] = ulaw_to_linear(ulaw_1khz_sine[i & 7]); power_meter_update(meter, amp[i]); }
        float level = power_meter_current_dbm0(meter); Console.WriteLine($"Reference u-law 1kHz tone is {level.ToString("F6", CultureInfo.InvariantCulture)}dBm0"); output?.WriteFrames(amp.AsSpan(0, 8000));
        if (MathF.Abs(level) > .1f) { Console.WriteLine("Test failed."); Exit(2); }
        meter.Initialize(7);
        for (int i = 0; i < 8000; i++) { amp[i] = alaw_to_linear(alaw_1khz_sine[i & 7]); power_meter_update(meter, amp[i]); }
        level = power_meter_current_dbm0(meter); Console.WriteLine($"Reference A-law 1kHz tone is {level.ToString("F6", CultureInfo.InvariantCulture)}dBm0"); output?.WriteFrames(amp.AsSpan(0, 8000));
        if (MathF.Abs(level) > .1f) { Console.WriteLine("Test failed."); Exit(2); }

        Console.WriteLine("Testing transcoding A-law -> u-law -> A-law");
        for (int i = 0; i < 256; i++) {
            int round = alaw_to_ulaw(ulaw_to_alaw((byte)i));
            if (round != i && Math.Abs(round - i) > 1) {
                Console.WriteLine($"u-law -> A-law -> u-law gave {i} -> {round}");
                Console.WriteLine("Test failed");
                Exit(2);
            }
        }

        Console.WriteLine("Testing transcoding u-law -> A-law -> u-law");
        for (int i = 0; i < 256; i++) {
            int round = ulaw_to_alaw(alaw_to_ulaw((byte)i));
            if (round != i) {
                // This deliberately preserves the original spanDSP/EngineFX guard expression.
                int guardRound = alaw_to_ulaw(ulaw_to_alaw((byte)i));
                if (Math.Abs(guardRound - i) > 1) {
                    Console.WriteLine($"A-law -> u-law -> A-law gave {i} -> {round}");
                    Console.WriteLine("Test failed");
                    Exit(2);
                }
            }
        }

        g711_state_t encoder = g711_init(null, G711_ALAW) ?? throw new InvalidOperationException("g711_init encoder failed");
        g711_state_t transcoder = g711_init(null, G711_ALAW) ?? throw new InvalidOperationException("g711_init transcoder failed");
        g711_state_t decoder = g711_init(null, G711_ULAW) ?? throw new InvalidOperationException("g711_init decoder failed");
        for (int i = 0; i < 65536; i++) amp[i] = (short)(i - 32768);
        int length = g711_encode(encoder, alaw_data, amp, 65536);
        length = g711_transcode(transcoder, ulaw_data, alaw_data, length);
        length = g711_decode(decoder, amp, ulaw_data, length);
        if (length != 65536) {
            Console.WriteLine($"Block coding gave the wrong length - {length} instead of 65536");
            Console.WriteLine("Test failed");
            Exit(2);
        }
        for (int i = 0; i < length; i++) {
            int pre = i - 32768, post = amp[i];
            if (Math.Abs(pre) > 140) {
                float error = (float)Math.Abs(post - pre) / Math.Abs(pre);
                if (error > .10f) {
                    Console.WriteLine($"Block: Excessive error at {pre} ({post})");
                    Exit(2);
                }
            } else if (Math.Abs(post - pre) > 15) {
                Console.WriteLine($"Block: Excessive error at {pre} ({post})");
                Exit(2);
            }
        }
        g711_free(encoder); g711_free(transcoder); g711_free(decoder); Console.WriteLine("Tests passed.");
    }

    public static int tkfx_g711_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments); bool basic = true, encode = false, decode = false; int law = G711_ALAW; string? inputPath = null, outputPath = null;
        for (int i = 0; i < testArguments.Length; i++) {
            switch (testArguments[i]) {
                case "-a": law = G711_ALAW; basic = false; break;
                case "-u": law = G711_ULAW; basic = false; break;
                case "-d": if (++i >= testArguments.Length) Exit(2); inputPath = testArguments[i]; basic = false; decode = true; break;
                case "-e": if (++i >= testArguments.Length) Exit(2); inputPath = testArguments[i]; basic = false; encode = true; break;
                case "-l": if (++i >= testArguments.Length) Exit(2); outputPath = testArguments[i]; break;
                default: if (testArguments[i].StartsWith("-", StringComparison.Ordinal)) Exit(2); break;
            }
        }
        if (basic) { ComplianceTests(true); return 0; }
        if (!encode && !decode) encode = decode = true;
        inputPath ??= encode ? IN_FILE_NAME : ENCODED_FILE_NAME; outputPath ??= decode ? OUT_FILE_NAME : ENCODED_FILE_NAME;
        g711_state_t? encoder = encode ? g711_init(null, law) : null; g711_state_t? decoder = decode ? g711_init(null, law) : null;
        using Pcm16WaveReader? reader = encode ? new Pcm16WaveReader(inputPath, 8000, 1) : null;
        using FileStream? encodedInput = !encode ? File.OpenRead(inputPath) : null;
        using Pcm16WaveWriter? writer = decode ? new Pcm16WaveWriter(outputPath, 8000, 1) : null;
        using FileStream? encodedOutput = !decode ? File.Create(outputPath) : null;
        short[] input = new short[BLOCK_LEN], outputSamples = new short[BLOCK_LEN]; byte[] data = new byte[BLOCK_LEN];
        while (true) {
            int bytes;
            if (encode) { int count = reader!.ReadFrames(input); if (count <= 0) break; bytes = g711_encode(encoder!, data, input, count); } else { bytes = encodedInput!.Read(data, 0, data.Length); if (bytes <= 0) break; }
            if (decode) { int count = g711_decode(decoder!, outputSamples, data, bytes); writer!.WriteFrames(outputSamples.AsSpan(0, count)); } else encodedOutput!.Write(data, 0, bytes);
        }
        if (encoder is not null) g711_free(encoder); if (decoder is not null) g711_free(decoder);
        Console.WriteLine($"'{inputPath}' translated to '{outputPath}' using {(law == G711_ALAW ? "A-law" : "u-law")}."); return 0;
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
                string id = new string(_reader.ReadChars(4));
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
