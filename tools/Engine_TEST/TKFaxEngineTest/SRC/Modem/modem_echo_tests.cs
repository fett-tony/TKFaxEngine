/*
 * TKFaxEngine - managed C# port of modem_echo_tests.c.
 *
 * The original 256-tap modem echo-canceller test, G.168 D.2-D.9 channel
 * models, A-law channel munging, five-second pre/post measurements and
 * fifty-second adaptation run are retained.
 */

#nullable enable

using TKFaxEngine.Audio;
using TKFaxEngine.Modem;
using TKFaxEngineTest.System;
using static TKFaxEngine.Audio.g711;

namespace TKFaxEngineTest.Modem.Test;

public static class modem_echo_tests {
    private const int SampleRate = 8000;
    private static readonly short[] Tone1Khz = { 10362, 7327, 0, -7327, -10362, -7327, 0, 7327 };
    private static readonly short[] ResidueSound = new short[8000];
    private static readonly SignalSource LocalCss = new();
    private static readonly Fir32State LineModel = new();
    private static Pcm16WaveWriter? _resultWriter;
    private static int _residueCurrent;
    private static bool _doCodecMunge = true;
    private static bool _useGui;

    public static int main(string[] testArguments) {
        try { return tkfx_modem_echo_tests_body_main(testArguments); } catch (ModemEchoTestsExitException ex) { return ex.ExitCode; }
    }

    public static int tkfx_modem_echo_tests_body_main(string[] testArguments) {
        int lineModelNumber = 0;
        _useGui = false;
        int count = testArguments.Length;
        int first = 0;
        for (int i = first; i < count; i++) {
            if (testArguments[i] == "-g") { _useGui = true; continue; }
            if (!int.TryParse(testArguments[i], out lineModelNumber)) Exit(2);
        }
        _ = _useGui;
        if ((uint)lineModelNumber >= 8U) Exit(2);

        using ModemEchoCanSegmentState context = ModemEcho.SegmentInit(256);
        var farNoiseSource = new Awgn(7162534, -50.0);
        _ = farNoiseSource; // retained source initialisation; the native test does not consume it.

        SignalLoad(LocalCss, "Test-Files/local/sound_c1_8k.wav");
        try {
            _resultWriter = new Pcm16WaveWriter("modem_echo.wav", SampleRate, 2);
            ChannelModelCreate(lineModelNumber);
            ModemEcho.Flush(context);

            var powerBefore = new PowerMeterState(5);
            var powerAfter = new PowerMeterState(5);

            ModemEcho.SetAdaptationMode(context, false);
            for (int i = 0; i < SampleRate * 5; i++) {
                short tx = Tone1Khz[i & 7];
                short rx = ChannelModel(tx, 0);
                short clean = ModemEcho.Update(context, tx, rx);
                powerBefore.Update(rx);
                powerAfter.Update(clean);
            }

            float unadaptedOutputPower = powerBefore.CurrentDbm0;
            float unadaptedEchoPower = powerAfter.CurrentDbm0;
            Console.WriteLine($"Pre-adaption: output power {unadaptedOutputPower,10:F5}dBm0, echo power {unadaptedEchoPower,10:F5}dBm0");

            SignalRestart(LocalCss);
            ModemEcho.SetAdaptationMode(context, true);
            for (int i = 0; i < 800 * 2; i++) {
                short clean = ModemEcho.Update(context, 0, 0);
                PutResidue(0, clean);
            }

            for (int i = 0; i < SampleRate * 50; i++) {
                short tx = SignalAmplitude(LocalCss);
                short rx = ChannelModel(tx, 0);
                short clean = ModemEcho.Update(context, tx, rx);
                powerBefore.Update(rx);
                powerAfter.Update(clean);
                PutResidue(tx, clean);
#if WINDOWS && ENABLE_MODEM_ECHO_GUI
                if (_useGui)
                {
                    echo_monitor.echo_can_monitor_can_update(context.FirTaps16, 256);
                    echo_monitor.echo_can_monitor_line_spectrum_update(new[] { tx }, 1);
                }
#endif
            }

            ModemEcho.SetAdaptationMode(context, false);
            for (int i = 0; i < SampleRate * 5; i++) {
                short tx = Tone1Khz[i & 7];
                short rx = ChannelModel(tx, 0);
                short clean = ModemEcho.Update(context, tx, rx);
                powerBefore.Update(rx);
                powerAfter.Update(clean);
            }

            float adaptedOutputPower = powerBefore.CurrentDbm0;
            float adaptedEchoPower = powerAfter.CurrentDbm0;
            Console.WriteLine($"Post-adaption: output power {adaptedOutputPower,10:F5}dBm0, echo power {adaptedEchoPower,10:F5}dBm0");

            float outputDelta = MathF.Abs(adaptedOutputPower - unadaptedOutputPower);
            if (outputDelta > 0.1f || adaptedEchoPower > unadaptedEchoPower - 30.0f) {
                Console.WriteLine(
                    "ERROR ModemEcho limits expected_output_power_delta<=0.100000 actual={0} expected_echo_power<={1} actual={2} unadapted_output_power={3} unadapted_echo_power={4} input_buffer=BEFUELLT output_buffer=BEFUELLT",
                    outputDelta,
                    unadaptedEchoPower - 30.0f,
                    adaptedEchoPower,
                    unadaptedOutputPower,
                    unadaptedEchoPower);
                Exit(2);
            }
        } finally {
            SignalFree(LocalCss);
            _resultWriter?.Dispose();
            _resultWriter = null;
        }

#if WINDOWS && ENABLE_MODEM_ECHO_GUI
        if (_useGui) echo_monitor.echo_can_monitor_wait_to_end();
#endif
        Console.WriteLine("Tests passed.");
        return 0;
    }

    private static void PutResidue(short tx, short residue) {
        if (_resultWriter is null) throw new InvalidOperationException("Result writer is not open.");
        ResidueSound[_residueCurrent++] = tx;
        ResidueSound[_residueCurrent++] = residue;
        if (_residueCurrent >= 8000) {
            _residueCurrent >>= 1;
            int frames = _resultWriter.WriteFrames(ResidueSound, _residueCurrent);
            if (frames != _residueCurrent) Fail("    Error writing residue sound");
            _residueCurrent = 0;
        }
    }

    private static void SignalLoad(SignalSource signal, string name) {
        signal.Name = name;
        try {
            using var reader = new Pcm16WaveReader(name, SampleRate, 1);
            signal.Maximum = reader.ReadFrames(signal.Signal, 8000);
            if (signal.Maximum < 0) Fail($"    Error reading sound file '{name}'");
        } catch (Exception ex) {
            Fail($"    Cannot open sound file '{name}': {ex.Message}");
        }
    }

    private static void SignalFree(SignalSource signal) {
        _ = signal;
    }

    private static void SignalRestart(SignalSource signal) => signal.Current = 0;

    private static short SignalAmplitude(SignalSource signal) {
        if (signal.Maximum <= 0) Fail($"    Sound file '{signal.Name}' contains no samples");
        short tx = signal.Signal[signal.Current++];
        if (signal.Current >= signal.Maximum) signal.Current = 0;
        return tx;
    }

    private static short CodecMunger(short amplitude) {
        return _doCodecMunge ? alaw_to_linear(linear_to_alaw(amplitude)) : amplitude;
    }

    private static void ChannelModelCreate(int model) {
        int[][] models =
        {
            G168LineModels.D2, G168LineModels.D3, G168LineModels.D4, G168LineModels.D5,
            G168LineModels.D6, G168LineModels.D7, G168LineModels.D8, G168LineModels.D9
        };
        Fir.fir32_create(LineModel, models[model], models[model].Length);
    }

    private static short ChannelModel(short local, short far) {
        short localScaled = unchecked((short)(local / 8));
        short echo = Fir.fir32(LineModel, CodecMunger(localScaled));
        short mixed = unchecked((short)(echo + CodecMunger(far)));
        return CodecMunger(mixed);
    }

    private static void Fail(string message) {
        Console.Error.WriteLine(message);
        Exit(2);
    }

    private static void Exit(int code) => throw new ModemEchoTestsExitException(code);

    public sealed class ModemEchoTestsExitException : Exception {
        public ModemEchoTestsExitException(int exitCode) : base($"modem_echo_tests requested exit code {exitCode}.") => ExitCode = exitCode;
        public int ExitCode { get; }
    }

    private sealed class SignalSource {
        internal string Name = string.Empty;
        internal int Maximum;
        internal int Current;
        internal readonly short[] Signal = new short[8000];
    }

    private sealed class Pcm16WaveReader : IDisposable {
        private readonly BinaryReader _reader;
        private readonly int _channels;
        private long _remainingBytes;

        internal Pcm16WaveReader(string path, int sampleRate, int channels) {
            _reader = new BinaryReader(File.OpenRead(path));
            if (ReadFourCc(_reader) != "RIFF") throw new InvalidDataException("Missing RIFF header.");
            _reader.ReadInt32();
            if (ReadFourCc(_reader) != "WAVE") throw new InvalidDataException("Missing WAVE header.");
            ushort format = 0, foundChannels = 0, bits = 0;
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

internal static class G168LineModels {
    public static readonly int[] D2 = new int[]
    {
        -436, -829, -2797, -4208, -17968, -11215, 46150, 34480, -10427, 9049, -1309, -6320,
        390, -8191, -1751, -6051, -3796, -4055, -3948, -2557, -3372, -1808, -2259, -1300,
        -1098, -618, -340, -61, 323, 419, 745, 716, 946, 880, 1014, 976,
        1033, 1091, 1053, 1042, 794, 831, 899, 716, 390, 313, 304, 304,
        73, -119, -109, -176, -359, -407, -512, -580, -704, -618, -685, -791,
        -772, -820, -839, -724,
    };

    public static readonly int[] D3 = new int[]
    {
        -381, 658, 1730, -51, -3511, -1418, 7660, 8861, -8106, -21370, -5307, 23064,
        24020, 1020, -12374, -16296, -19524, -7480, 13509, 17115, 13952, 13952, 97, -9326,
        -9046, -15208, -9853, -3858, -1979, 6029, 5616, 7214, 6820, 3935, 3919, 921,
        1316, -693, -759, -1517, -2176, -2028, -2654, -1814, -2077, -1468, -1221, -842,
        -463, -298, -68, 64, 493, 723, 789, 954, 756, 839, 872, 1020,
        789, 822, 558, 658, 476, 377, 377, 262, 97, -68, -183, -232,
        -331, -347, -430, -314, -430, -463, -463, -414, -381, -479, -479, -512,
        -479, -397, -430, -397, -298, -265, -249, -216, -249, -265, -166, -232,
    };

    public static readonly int[] D4 = new int[]
    {
        -448, -436, 2230, 2448, -4178, -7050, 5846, 18581, 2322, -26261, -16249, 21637,
        25649, -2267, -10311, -4693, -12690, -7428, 14164, 13467, 4438, 8627, 456, -11879,
        -6352, -5104, -7496, 3271, 6566, 4277, 11131, 7562, 1475, 3728, -3525, -7301,
        -3101, -9269, -6146, -2553, -6272, 811, 124, 788, 5147, 2172, 5387, 4598,
        3535, 4004, 2311, 2150, 1017, 330, -139, -573, -1100, -1157, -1180, -1455,
        -1123, -1386, -1123, -1066, -1020, -1100, -1008, -1077, -1088, -917, -917, -963,
        -814, -871, -734, -642, -562, -356, -379, -345, -230, -233, -333, -356,
        -390, -310, -265, -368, -310, -310, -390, -482, -459, -482, -551, -573,
    };

    public static readonly int[] D5 = new int[]
    {
        160, 312, -241, -415, 897, 908, -1326, -1499, 2405, 3347, -3624, -7733,
        4041, 14484, -1477, -21739, -4470, 25356, 11458, -19696, -11800, 5766, 789, 6633,
        14624, -6975, -17156, -187, 149, 1515, 14907, 4345, -7128, -2757, -10185, -7083,
        6850, 3944, 6969, 8694, -4068, -3852, -5793, -9371, 453, 1060, 3965, 9463,
        2393, 2784, -892, -7366, -3376, -5847, -2399, 3011, 1537, 6623, 4205, 1602,
        1592, -4752, -3646, -5207, -5577, -501, -1174, 4041, 5647, 4628, 7252, 2123,
        2654, -881, -4113, -3244, -7289, -3830, -4600, -2508, 431, -144, 4184, 2372,
        4617, 3576, 2382, 2839, -404, 539, -1803, -1401, -1705, -2269, -783, -1608,
        -220, -306, 257, 615, 225, 561, 8, 344, 127, -57, 182, 41,
        203, -111, 95, -79, 30, 84, -13, -68, -241, -68, -24, 19,
        -57, -24, 30, -68, 84, -155, -68, 19,
    };

    public static readonly int[] D6 = new int[]
    {
        293, 268, 475, 460, 517, 704, 581, 879, 573, 896, 604, 787,
        561, 538, 440, 97, 265, -385, 20, -938, -523, -1438, -1134, -1887,
        -1727, -1698, -4266, -22548, -43424, 2743, 25897, 7380, 21499, 11983, 10400, 11667,
        3889, 7241, 925, 2018, -821, -2068, -2236, -4283, -3406, -5022, -4039, -4842,
        -4104, -4089, -3582, -2978, -2734, -1805, -1608, -645, -495, 279, 471, 947,
        1186, 1438, 1669, 1640, 1901, 1687, 1803, 1543, 1566, 1342, 1163, 963,
        733, 665, 323, 221, -14, -107, -279, -379, -468, -513, -473, -588,
        -612, -652, -616, -566, -515, -485, -404, -344, -290, -202, -180, -123,
    };

    public static readonly int[] D7 = new int[]
    {
        29, 109, -83, 198, -294, -135, -415, -202, -444, -337, -313, -450,
        -105, -503, 145, -490, 267, -231, 340, 77, 343, 783, 158, 1341,
        195, 1798, 344, 1845, 629, 1604, 1182, 940, 5163, 19522, 8421, -50953,
        -9043, 18046, -13553, 13336, -3471, -107, 1788, -7409, 2469, -7994, 490, -3860,
        -837, 490, -636, 3682, 1141, 5019, 2635, 5025, 3946, 4414, 4026, 3005,
        3380, 1616, 2007, 158, 388, -1198, -1117, -2134, -2547, -2589, -3310, -2778,
        -3427, -2779, -3116, -2502, -2399, -1956, -1539, -1239, -570, -377, 251, 331,
        964, 1177, 1449, 1564, 1724, 1871, 1767, 1802, 1630, 1632, 1379, 1271,
        1063, 856, 711, 482, 289, 54, -137, -321, -490, -638, -764, -836,
        -800, -859, -838, -837, -834, -740, -673, -581, -493, -436, -327, -201,
    };

    public static readonly int[] D8 = new int[]
    {
        258, -111, 337, -319, 347, -434, 192, -450, -108, -343, -596, -177,
        -1187, -52, -1781, -147, -1959, -326, -1601, -1389, -13620, -720, 33818, -10683,
        -6742, 12489, -9862, 8950, -1574, 758, 3526, -3118, 2421, -8966, -4901, 11385,
        18072, -14410, -7473, 19836, -16854, -3115, 9483, -17799, 7399, -4342, -7415, 7929,
        -10726, 6239, -2526, -1317, 5345, -4565, 6868, -2195, 3425, 1969, -109, 3963,
        -1275, 3087, -892, 1239, 2, -427, 596, -1184, 551, -1244, 141, -743,
        -415, -372, -769, -183, -785, -270, -659, -377, -523, -325, -245, -255,
        -60, 35, 218, 149, 340, 233, 365, 303, 251, 230, 209, 179,
    };

    public static readonly int[] D9 = new int[]
    {
        80, 31, 4, 42, 42, -61, -81, -64, -121, -102, -26, 1002,
        -9250, -22562, 39321, 35681, -35289, 25312, -1457, -229, 15659, -6786, 16791, 3860,
        2239, -28730, -11885, 33871, -176, -16421, 18173, -9669, -10163, 9941, -19365, 3592,
        -5907, -10257, 5336, -12933, 4348, -4802, -1791, 3035, -4433, 5553, -2596, 3992,
        1255, 1450, 4079, 324, 4340, 1059, 3083, 1917, 1756, 2478, 1027, 1871,
        845, 1284, 813, 806, 869, 471, 646, 438, 449, 432, 473, 394,
        452, 538, 717, 723, 850, 756, 753, 899, 555, 669, 619, 500,
        650, 615, 516, 492, 427, 291, 356, 147, 107, -50, -88, -59,
        -238, -165, -183,
    };
}
