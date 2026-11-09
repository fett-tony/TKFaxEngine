/*
 * TKFaxEngine - managed C# port of line_model_tests.c.
 * The complete 20,000-sample complexify run, 10,000 x 160-sample line runs,
 * speech-file modes and 120-second swept-frequency filter test are retained.
 */

#nullable enable

using System.Globalization;
using TKFaxEngine.Audio;
using TKFaxEngineTest.System;
using static TKFaxEngine.Audio.SweptToneApi;
using static TKFaxEngineTest.Network.Test.line_model;

namespace TKFaxEngineTest.Network.Test;

public static class line_model_tests {
    private const int BLOCK_LEN = 160;
    private const int SAMPLE_RATE = 8000;
    private const string OUT_FILE_COMPLEXIFY = "complexify.wav";
    private const string IN_FILE_NAME1 = "line_model_test_in1.wav";
    private const string IN_FILE_NAME2 = "line_model_test_in2.wav";
    private const string OUT_FILE_NAME1 = "line_model_one_way_test_out.wav";
    private const string OUT_FILE_NAME2 = "line_model_two_way_test_out.wav";

    public const int MUNGE_CODEC_NONE = CodecMungeState.MUNGE_CODEC_NONE;
    public const int MUNGE_CODEC_ALAW = CodecMungeState.MUNGE_CODEC_ALAW;
    public const int MUNGE_CODEC_ULAW = CodecMungeState.MUNGE_CODEC_ULAW;
    public const int MUNGE_CODEC_G726_40K = CodecMungeState.MUNGE_CODEC_G726_40K;
    public const int MUNGE_CODEC_G726_32K = CodecMungeState.MUNGE_CODEC_G726_32K;
    public const int MUNGE_CODEC_G726_24K = CodecMungeState.MUNGE_CODEC_G726_24K;
    public const int MUNGE_CODEC_G726_16K = CodecMungeState.MUNGE_CODEC_G726_16K;

    private static int channel_codec;
    private static int rbs_pattern;

    public static int main(string[] testArguments) {
        try { return tkfx_line_model_tests_body_main(testArguments); } catch (LineModelTestsExitException ex) { return ex.ExitCode; }
    }

    public static int tkfx_line_model_tests_body_main(string[] testArguments) {
        channel_codec = MUNGE_CODEC_NONE;
        int lineModelNumber = 0;
        int value = 0;
        rbs_pattern = 0;
        bool speechTest = false;

        int argumentCount = testArguments.Length;
        int firstArgument = 0;
        for (int i = firstArgument; i < argumentCount; i++) {
            string arg = testArguments[i];
            if (arg is "-c" or "-m" or "-r" or "-s") {
                if (++i >= argumentCount || !int.TryParse(testArguments[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                    Exit(2);
                switch (arg) {
                    case "-c": channel_codec = value; break;
                    case "-m": lineModelNumber = value; break;
                    case "-r": rbs_pattern = value; break;
                    case "-s": speechTest = value != 0; break;
                }
            } else if (!string.IsNullOrEmpty(arg)) {
                Exit(2);
            }
        }

        complexify_tests();
        test_one_way_model(lineModelNumber, speechTest);
        test_both_ways_model(lineModelNumber, speechTest);
        test_line_filter(lineModelNumber);
        return 0;
    }

    private static void complexify_tests() {
        var state = new ComplexifyState();
        var noise = new Awgn(1234567, -10.0);
        var output = new short[40000];
        for (int i = 0; i < 20000; i++) {
            short sample = noise.NextSample();
            ComplexFloat value = state.Process(sample);
            output[2 * i] = CInt16(value.Real);
            output[2 * i + 1] = CInt16(value.Imaginary);
        }

        using var writer = new Pcm16WaveWriter(OUT_FILE_COMPLEXIFY, SAMPLE_RATE, 2);
        int frames = writer.WriteFrames(output, 20000);
        if (frames != 20000) Fail("    Error writing audio file");
    }

    private static void test_one_way_model(int lineModelNumber, bool speechTest) {
        using one_way_line_model_state_t model = one_way_line_model_init(lineModelNumber, -50.0f, channel_codec, rbs_pattern);
        var input = new short[BLOCK_LEN];
        var output = new short[BLOCK_LEN];
        var noise = new Awgn(1234567, -10.0);
        using Pcm16WaveReader? reader = speechTest ? OpenReader(IN_FILE_NAME1, 1) : null;
        using var writer = new Pcm16WaveWriter(OUT_FILE_NAME1, SAMPLE_RATE, 1);

        for (int i = 0; i < 10000; i++) {
            int samples;
            if (speechTest) {
                samples = reader!.ReadFrames(input, BLOCK_LEN);
                if (samples == 0) break;
            } else {
                for (int j = 0; j < BLOCK_LEN; j++) input[j] = noise.NextSample();
                samples = BLOCK_LEN;
            }

            for (int j = 0; j < samples; j++)
                one_way_line_model(model, output, j, input, j, 1);

            int frames = writer.WriteFrames(output, samples);
            if (frames != samples) Fail("    Error writing audio file");
        }
    }

    private static void test_both_ways_model(int lineModelNumber, bool speechTest) {
        using both_ways_line_model_state_t model = both_ways_line_model_init(
            lineModelNumber, -50.0f, -15.0f, -15.0f,
            lineModelNumber + 1, -35.0f, -15.0f, -15.0f,
            channel_codec, rbs_pattern);

        var input1 = new short[BLOCK_LEN];
        var input2 = new short[BLOCK_LEN];
        var output1 = new short[BLOCK_LEN];
        var output2 = new short[BLOCK_LEN];
        var interleaved = new short[2 * BLOCK_LEN];
        var noise1 = new Awgn(1234567, -10.0);
        var noise2 = new Awgn(1234567, -10.0);
        using Pcm16WaveReader? reader1 = speechTest ? OpenReader(IN_FILE_NAME1, 1) : null;
        using Pcm16WaveReader? reader2 = speechTest ? OpenReader(IN_FILE_NAME2, 1) : null;
        using var writer = new Pcm16WaveWriter(OUT_FILE_NAME2, SAMPLE_RATE, 2);

        for (int i = 0; i < 10000; i++) {
            int samples;
            if (speechTest) {
                samples = reader1!.ReadFrames(input1, BLOCK_LEN);
                if (samples == 0) break;
                samples = reader2!.ReadFrames(input2, samples);
                if (samples == 0) break;
            } else {
                for (int j = 0; j < BLOCK_LEN; j++) {
                    input1[j] = noise1.NextSample();
                    input2[j] = noise2.NextSample();
                }
                samples = BLOCK_LEN;
            }

            for (int j = 0; j < samples; j++) {
                both_ways_line_model(model, output1, j, input1, j, output2, j, input2, j, 1);
                interleaved[2 * j] = output1[j];
                interleaved[2 * j + 1] = output2[j];
            }

            int frames = writer.WriteFrames(interleaved, samples);
            if (frames != samples) Fail("    Error writing audio file");
        }
    }

    private static void test_line_filter(int lineModelNumber) {
        using SweptToneState sweep = swept_tone_init(null, 200.0f, 3900.0f, -10.0f, 120 * SAMPLE_RATE, 0);
        var filter = new float[129];
        var buffer = new short[BLOCK_LEN];
        int pointer = 0;

        for (; ; )
        {
            int length = swept_tone(sweep, buffer, BLOCK_LEN);
            if (length <= 0) break;
            double sumInput = 0.0;
            double sumOutput = 0.0;

            for (int i = 0; i < length; i++) {
                int p = pointer;
                filter[p] = buffer[i];
                if (++p == 129) p = 0;
                pointer = p;

                float output = 0.0f;
                for (int j = 0; j < 129; j++) {
                    output += line_model.line_models[lineModelNumber][128 - j] * filter[p];
                    if (++p >= 129) p = 0;
                }
                sumInput += (double)buffer[i] * buffer[i];
                sumOutput += output * output;
            }

            double gain = sumInput != 0.0 ? 10.0 * Math.Log10(sumOutput / sumInput + 1.0e-10) : 0.0;
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,7:F1} {1:F6}", swept_tone_current_frequency(sweep), gain));
        }
    }

    private static Pcm16WaveReader OpenReader(string path, int channels) {
        try { return new Pcm16WaveReader(path, SAMPLE_RATE, channels); } catch (Exception ex) { Fail($"    Cannot open audio file '{path}': {ex.Message}"); throw; }
    }

    private static void Fail(string message) {
        Console.Error.WriteLine(message);
        Exit(2);
    }

    private static void Exit(int code) => throw new LineModelTestsExitException(code);
    private static short CInt16(float value) => unchecked((short)(int)value);

    public sealed class LineModelTestsExitException : Exception {
        public LineModelTestsExitException(int exitCode) : base($"line_model_tests requested exit code {exitCode}.") => ExitCode = exitCode;
        public int ExitCode { get; }
    }

    private readonly record struct ComplexFloat(float Real, float Imaginary);

    private sealed class ComplexifyState {
        private const float HILBERT_GAIN = 1.569546344f;
        private static readonly float[] HilbertCoefficients = new float[]
        {
            0.0012698413f, 0.0013489483f, 0.0015105196f, 0.0017620440f,
            0.0021112899f, 0.0025663788f, 0.0031358856f, 0.0038289705f,
            0.0046555545f, 0.0056265487f, 0.0067541562f, 0.0080522707f,
            0.0095370033f, 0.0112273888f, 0.0131463382f, 0.0153219442f,
            0.0177892941f, 0.0205930381f, 0.0237910974f, 0.0274601544f,
            0.0317040029f, 0.0366666667f, 0.0425537942f, 0.0496691462f,
            0.0584802574f, 0.0697446887f, 0.0847739823f, 0.1060495199f,
            0.1388940865f, 0.1971551103f, 0.3316207267f, 0.9994281838f
        };

        private readonly float[] _history = new float[128];
        private int _pointer;

        internal ComplexFloat Process(short amplitude) {
            _history[_pointer] = amplitude;
            int i = _pointer - 63;
            if (i < 0) i += 128;
            float real = _history[i];

            float imaginary = 0.0f;
            int j = _pointer - 126;
            if (j < 0) j += 128;
            int intK = _pointer;
            for (i = 0; i < 32; i++) {
                imaginary += (_history[intK] - _history[j]) * HilbertCoefficients[i];
                j += 2;
                if (j >= 128) j -= 128;
                intK -= 2;
                if (intK < 0) intK += 128;
            }
            imaginary /= HILBERT_GAIN;
            if (++_pointer >= 128) _pointer = 0;
            return new ComplexFloat(real, imaginary);
        }
    }

    private sealed class Pcm16WaveReader : IDisposable {
        private readonly BinaryReader _reader;
        private readonly int _channels;
        private long _remainingBytes;

        internal Pcm16WaveReader(string path, int sampleRate, int channels) {
            _reader = new BinaryReader(File.OpenRead(path));
            if (ReadFourCc(_reader) != "RIFF") throw new InvalidDataException("RIFF header missing.");
            _reader.ReadUInt32();
            if (ReadFourCc(_reader) != "WAVE") throw new InvalidDataException("WAVE header missing.");

            ushort format = 0, bits = 0, foundChannels = 0;
            int foundRate = 0;
            while (_reader.BaseStream.Position + 8 <= _reader.BaseStream.Length) {
                string id = ReadFourCc(_reader);
                int size = _reader.ReadInt32();
                long next = _reader.BaseStream.Position + size + (size & 1);
                if (id == "fmt ") {
                    format = _reader.ReadUInt16();
                    foundChannels = _reader.ReadUInt16();
                    foundRate = _reader.ReadInt32();
                    _reader.ReadInt32();
                    _reader.ReadUInt16();
                    bits = _reader.ReadUInt16();
                } else if (id == "data") {
                    _remainingBytes = size;
                    break;
                }
                _reader.BaseStream.Position = next;
            }

            if (format != 1 || bits != 16 || foundRate != sampleRate || foundChannels != channels)
                throw new InvalidDataException("Unexpected WAV format.");
            _channels = channels;
        }

        internal int ReadFrames(short[] destination, int maximumFrames) {
            int frames = 0;
            while (frames < maximumFrames && _remainingBytes >= 2L * _channels) {
                destination[frames++] = _reader.ReadInt16();
                for (int channel = 1; channel < _channels; channel++) _reader.ReadInt16();
                _remainingBytes -= 2L * _channels;
            }
            return frames;
        }

        public void Dispose() => _reader.Dispose();
    }

    private sealed class Pcm16WaveWriter : IDisposable {
        private readonly BinaryWriter _writer;
        private readonly short _channels;
        private long _samples;
        private bool _disposed;

        internal Pcm16WaveWriter(string path, int sampleRate, int channels) {
            _channels = checked((short)channels);
            _writer = new BinaryWriter(File.Create(path));
            _writer.Write(global::System.Text.Encoding.ASCII.GetBytes("RIFF"));
            _writer.Write(0);
            _writer.Write(global::System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            _writer.Write(16);
            _writer.Write((short)1);
            _writer.Write(_channels);
            _writer.Write(sampleRate);
            _writer.Write(sampleRate * channels * 2);
            _writer.Write((short)(channels * 2));
            _writer.Write((short)16);
            _writer.Write(global::System.Text.Encoding.ASCII.GetBytes("data"));
            _writer.Write(0);
        }

        internal int WriteFrames(short[] interleaved, int frames) {
            int samples = checked(frames * _channels);
            if (samples > interleaved.Length) throw new ArgumentOutOfRangeException(nameof(frames));
            for (int i = 0; i < samples; i++) _writer.Write(interleaved[i]);
            _samples += samples;
            return frames;
        }

        public void Dispose() {
            if (_disposed) return;
            long dataBytes = _samples * 2;
            _writer.BaseStream.Position = 4;
            _writer.Write(checked((int)(36 + dataBytes)));
            _writer.BaseStream.Position = 40;
            _writer.Write(checked((int)dataBytes));
            _writer.Dispose();
            _disposed = true;
        }
    }

    private static string ReadFourCc(BinaryReader reader) => new(reader.ReadChars(4));
}
