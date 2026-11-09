/*
 * TKFaxEngine - managed C# port
 *
 * make_g168_css.cs - Create the composite source signal (CSS) for G.168 testing.
 *
 * Fresh direct port of make_g168_css.c plus the used css_c1/css_c3 data from
 * spandsp/g168models.h.
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2003 Steve Underwood.
 * Licensed under the GNU General Public License version 2.
 */

#nullable enable

using System.Buffers.Binary;
using System.Numerics;
using TKFaxEngine.Audio;

namespace TKFaxEngineTest.Utility.Test;

public static class make_g168_css {
    private const double FAST_SAMPLE_RATE = 44_100.0;

    private const int C1_VOICED_SAMPLES = 2144;
    private const int C1_NOISE_SAMPLES = 8820;
    private const int C1_SILENCE_SAMPLES = 4471;

    private const int C3_VOICED_SAMPLES = 3206;
    private const int C3_NOISE_SAMPLES = 8820;
    private const int C3_SILENCE_SAMPLES = 5614;

    private static readonly short[] css_c1 =
    [
         -155,   276,   517,   578,   491,   302,    86,  -103,
         -207,  -198,    60,   190,   543,   948,  1362,  1741,
         2043,  2276,  2422,  2500,  2552,  2595,  2655,  2758,
         2896,  3060,  3224,  3370,  3500,  3569,  3603,  3603,
         3595,  3586,  3595,  3638,  3724,  3819,  3922,  4000,
         4043,  4034,  3974,  3862,  3724,  3577,  3439,  3336,
         3267,  3224,  3198,  3172,  3129,  3043,  2914,  2750,
         2560,  2353,  2155,  1991,  1853,  1750,  1672,  1603,
         1534,  1440,  1310,  1146,   965,   776,   603,   448,
          345,   276,   250,   250,   267,   267,   241,   190,
          103,    -9,  -138,  -267,  -388,  -491,  -569,  -638,
         -698,  -759,  -813,  -888,  -957, -1034, -1103, -1146,
        -1181, -1190, -1198, -1215, -1259, -1327, -1457, -1629,
        -1853, -2121, -2414, -2707, -3017, -3319, -3612, -3913,
        -4224, -4560, -4922, -5301, -5715, -6137, -6560, -6948,
        -7301, -7568, -7732, -7758, -7620, -7310, -6810, -6155,
        -5344, -4439, -3474, -2508, -1595,  -802
    ];

    private static readonly short[] css_c3 =
    [
         -198,  -112,    -9,   103,   233,   388,   543,   724,
          896,  1060,  1233,  1388,  1517,  1638,  1747,  1810,
         1845,  1845,  1802,  1707,  1569,  1379,  1146,   871,
          560,   233,  -121,  -491,  -871, -1250, -1638, -2043,
        -2465, -2896, -3345, -3819, -4310, -4810, -5319, -5836,
        -6353, -6853, -7353, -7836, -8292, -8715, -9077, -9370,
        -9542, -9542, -9361, -8956, -8327, -7465, -6396, -5163,
        -3827, -2448, -1103,   155,  1293,  2241,  3034,  3655,
         4138,  4517,  4827,  5094,  5344,  5594,  5827,  6043,
         6215,  6344,  6413,  6422,  6379,  6310,  6215,  6120,
         6051,  6000,  5991,  5991,  6000,  6008,  5991,  5939,
         5853,  5715,  5560,  5387,  5215,  5043,  4879,  4732,
         4586,  4439,  4276,  4086,  3870,  3629,  3370,  3086,
         2801,  2534,  2267,  2034,  1819,  1612,  1422,  1224,
         1026,   819,   603,   388,   181,     9,  -181,  -328,
         -448,  -543,  -629,  -707,  -784,  -871,  -948, -1026,
        -1112, -1181, -1241, -1276, -1293, -1302, -1293, -1267,
        -1250, -1233, -1224, -1224, -1224, -1224, -1215, -1198,
        -1172, -1129, -1077, -1026,  -974,  -922,  -888,  -871,
         -845,  -828,  -810,  -793,  -767,  -741,  -698,  -672,
         -638,  -603,  -595,  -586,  -595,  -603,  -621,  -629,
         -938,  -638,  -638,  -638,  -638,  -638,  -647,  -664,
         -690,  -724,  -767,  -793,  -819,  -845,  -853,  -871,
         -879,  -888,  -896,  -922,  -948,  -974, -1009, -1026,
        -1052, -1069, -1077, -1069, -1060, -1060, -1052, -1043,
        -1043, -1052, -1060, -1060, -1060, -1052, -1034, -1017,
         -991,  -957,  -931,  -905,  -888,  -862,  -845,  -819,
         -793,  -767,  -724,  -672,  -621,  -560,  -509,  -457,
         -397,  -345,  -276,  -207,  -112
    ];

    private static double scaling(double f, double start, double end, double start_gain, double end_gain) {
        return start_gain + (f - start) * (end_gain - start_gain) / (end - start);
    }

    private static double peak(short[] amp, int len) {
        int peakValue = 0;
        for (int i = 0; i < len; i++) {
            int absolute = Math.Abs((int)amp[i]);
            if (absolute > peakValue)
                peakValue = absolute;
        }
        return peakValue / 32767.0;
    }

    private static double rms(short[] amp, int len) {
        double ms = 0.0;
        for (int i = 0; i < len; i++)
            ms += (double)amp[i] * amp[i];
        return Math.Sqrt(ms / len) / 32767.0;
    }

    private static double rms_to_dbm0(double rmsValue) {
        return 20.0 * Math.Log10(rmsValue) + Awgn.Dbm0MaximumPower;
    }

    private static double rms_to_db(double rmsValue) {
        return 20.0 * Math.Log10(rmsValue);
    }

    public static int tkfx_make_g168_css_body_main(string[] testArguments) {
        short[] voiced_sound = new short[8192];
        short[] noise_sound = new short[8830];
        short[] silence_sound = new short[8192];

        GenerateC1(voiced_sound, noise_sound, silence_sound);
        GenerateC3(voiced_sound, noise_sound, silence_sound);
        return 0;
    }

    public static int Main(string[] args) => tkfx_make_g168_css_body_main(args);

    private static void GenerateC1(short[] voicedSound, short[] noiseSound, short[] silenceSound) {
        using Pcm16WaveWriter filehandle = OpenWriter("sound_c1.wav");

        Console.WriteLine("Generate C1");
        int voicedLength = css_c1.Length;
        Array.Copy(css_c1, voicedSound, voicedLength);

        double pk = peak(voicedSound, voicedLength);
        double ms = rms(voicedSound, voicedLength);
        Console.WriteLine(
            "Voiced level = {0:F2}dB, crest factor = {1:F2}dB",
            rms_to_dbm0(ms),
            rms_to_db(pk / ms));

        Complex[] spectrum = new Complex[8192];
        MsvcRand random = new(1);
        for (int i = 1; i <= 3715; i++) {
            double f = FAST_SAMPLE_RATE * i / 8192.0;
            double scale = SpectralScale(f);
            int randy = ((random.Next() >> 10) & 1) != 0 ? 1 : -1;
            double value = randy * Math.Pow(10.0, scale / 20.0) * 35.0;
            spectrum[i] = new Complex(value, 0.0);
            spectrum[8192 - i] = new Complex(-value, 0.0);
        }

        FilterTools.Ifft(spectrum, 8192);
        for (int i = 0; i < 8192; i++)
            noiseSound[i] = unchecked((short)(int)spectrum[i].Imaginary);

        pk = peak(noiseSound, 8192);
        ms = rms(noiseSound, 8192);
        Console.WriteLine(
            "Filtered noise level = {0:F2}dB, crest factor = {1:F2}dB",
            rms_to_dbm0(ms),
            rms_to_db(pk / ms));

        Array.Clear(silenceSound);

        WriteRepeated(filehandle, voicedSound, voicedLength, 16);
        Console.WriteLine("{0} samples of voice", 16 * voicedLength);
        filehandle.WriteFrames(noiseSound, 8192);
        filehandle.WriteFrames(noiseSound, C1_NOISE_SAMPLES - 8192);
        Console.WriteLine("{0} samples of noise", C1_NOISE_SAMPLES);
        filehandle.WriteFrames(silenceSound, C1_SILENCE_SAMPLES);
        Console.WriteLine("{0} samples of silence", C1_SILENCE_SAMPLES);

        for (int i = 0; i < voicedLength; i++)
            voicedSound[i] = unchecked((short)-css_c1[i]);
        for (int i = 0; i < 8192; i++)
            noiseSound[i] = unchecked((short)-noiseSound[i]);

        WriteRepeated(filehandle, voicedSound, voicedLength, 16);
        Console.WriteLine("{0} samples of voice", 16 * voicedLength);
        filehandle.WriteFrames(noiseSound, 8192);
        filehandle.WriteFrames(noiseSound, C1_NOISE_SAMPLES - 8192);
        Console.WriteLine("{0} samples of noise", C1_NOISE_SAMPLES);
        filehandle.WriteFrames(silenceSound, C1_SILENCE_SAMPLES);
        Console.WriteLine("{0} samples of silence", C1_SILENCE_SAMPLES);
    }

    private static void GenerateC3(short[] voicedSound, short[] noiseSound, short[] silenceSound) {
        using Pcm16WaveWriter filehandle = OpenWriter("sound_c3.wav");

        Console.WriteLine("Generate C3");
        int voicedLength = css_c3.Length;
        Array.Copy(css_c3, voicedSound, voicedLength);

        double pk = peak(voicedSound, voicedLength);
        double ms = rms(voicedSound, voicedLength);
        Console.WriteLine(
            "Voiced level = {0:F2}dB, crest factor = {1:F2}dB",
            rms_to_dbm0(ms),
            rms_to_db(pk / ms));

        Awgn noiseSource = new(7_162_534, rms_to_dbm0(ms));
        for (int i = 0; i < 8192; i++)
            noiseSound[i] = noiseSource.NextSample();

        pk = peak(noiseSound, 8192);
        ms = rms(noiseSound, 8192);
        Console.WriteLine(
            "Unfiltered noise level = {0:F2}dB, crest factor = {1:F2}dB",
            rms_to_dbm0(ms),
            rms_to_db(pk / ms));

        Complex[] spectrum = new Complex[8192];
        for (int i = 1; i <= 3715; i++) {
            double f = FAST_SAMPLE_RATE * i / 8192.0;
            double scale = SpectralScale(f);
            double value = noiseSound[i] * Math.Pow(10.0, scale / 20.0) * 0.0106;
            spectrum[i] = new Complex(value, 0.0);
            spectrum[8192 - i] = new Complex(-value, 0.0);
        }

        FilterTools.Ifft(spectrum, 8192);
        for (int i = 0; i < 8192; i++)
            noiseSound[i] = unchecked((short)(int)spectrum[i].Imaginary);

        pk = peak(noiseSound, 8192);
        ms = rms(noiseSound, 8192);
        Console.WriteLine(
            "Filtered noise level = {0:F2}dB, crest factor = {1:F2}dB",
            rms_to_dbm0(ms),
            rms_to_db(pk / ms));

        WriteRepeated(filehandle, voicedSound, voicedLength, 14);
        Console.WriteLine("{0} samples of voice", 14 * voicedLength);
        filehandle.WriteFrames(noiseSound, 8192);
        filehandle.WriteFrames(noiseSound, C3_NOISE_SAMPLES - 8192);
        Console.WriteLine("{0} samples of noise", C3_NOISE_SAMPLES);
        filehandle.WriteFrames(silenceSound, C3_SILENCE_SAMPLES);
        Console.WriteLine("{0} samples of silence", C3_SILENCE_SAMPLES);

        for (int i = 0; i < voicedLength; i++)
            voicedSound[i] = unchecked((short)-css_c3[i]);
        for (int i = 0; i < 8192; i++)
            noiseSound[i] = unchecked((short)-noiseSound[i]);

        WriteRepeated(filehandle, voicedSound, voicedLength, 14);
        Console.WriteLine("{0} samples of voice", 14 * voicedLength);
        filehandle.WriteFrames(noiseSound, 8192);
        filehandle.WriteFrames(noiseSound, C3_NOISE_SAMPLES - 8192);
        Console.WriteLine("{0} samples of noise", C3_NOISE_SAMPLES);
        filehandle.WriteFrames(silenceSound, C3_SILENCE_SAMPLES);
        Console.WriteLine("{0} samples of silence", C3_SILENCE_SAMPLES);
    }

    private static Pcm16WaveWriter OpenWriter(string path) {
        try {
            return new Pcm16WaveWriter(path, checked((int)FAST_SAMPLE_RATE), 1);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine("    Failed to open result file");
            throw new MakeG168CssExitException(2);
        }
    }

    private static double SpectralScale(double frequency) {
        if (frequency < 50.0)
            return -60.0;
        if (frequency < 100.0)
            return scaling(frequency, 50.0, 100.0, -25.8, -12.8);
        if (frequency < 200.0)
            return scaling(frequency, 100.0, 200.0, -12.8, 17.4);
        if (frequency < 215.0)
            return scaling(frequency, 200.0, 215.0, 17.4, 17.8);
        if (frequency < 500.0)
            return scaling(frequency, 215.0, 500.0, 17.8, 12.2);
        if (frequency < 1000.0)
            return scaling(frequency, 500.0, 1000.0, 12.2, 7.2);
        if (frequency < 2850.0)
            return scaling(frequency, 1000.0, 2850.0, 7.2, 0.0);
        if (frequency < 3600.0)
            return scaling(frequency, 2850.0, 3600.0, 0.0, -2.0);
        if (frequency < 3660.0)
            return scaling(frequency, 3600.0, 3660.0, -2.0, -20.0);
        if (frequency < 3680.0)
            return scaling(frequency, 3600.0, 3680.0, -20.0, -30.0);
        return -60.0;
    }

    private static void WriteRepeated(Pcm16WaveWriter writer, short[] samples, int length, int repeats) {
        for (int i = 0; i < repeats; i++)
            writer.WriteFrames(samples, length);
    }

    public sealed class MakeG168CssExitException : Exception {
        public MakeG168CssExitException(int exitCode)
            : base($"make_g168_css requested process exit {exitCode}.") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }

    private sealed class MsvcRand {
        private uint _state;

        public MsvcRand(uint seed) {
            _state = seed;
        }

        public int Next() {
            _state = unchecked(_state * 214013U + 2531011U);
            return (int)((_state >> 16) & 0x7FFFU);
        }
    }

    private sealed class Pcm16WaveWriter : IDisposable {
        private readonly FileStream _stream;
        private readonly int _sampleRate;
        private readonly int _channels;
        private long _dataBytes;
        private bool _disposed;

        public Pcm16WaveWriter(string path, int sampleRate, int channels) {
            _stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            _sampleRate = sampleRate;
            _channels = channels;
            _stream.Position = 44;
        }

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
            try {
                WriteHeader();
            } finally {
                _stream.Dispose();
            }
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
}
