/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * echo_tests.cs - managed port of echo_tests.c.
 *
 * Original test written by Steve Underwood <steveu@coppice.org>.
 * The G.168 test timings, channel models, signal flow and test selection are retained.
 */

#nullable enable

using System.Globalization;
using System.Text;
using TKFaxEngineTest.System;
using TKFaxEngine.Audio;
using static TKFaxEngine.Audio.Echo;
using static TKFaxEngine.Audio.Fir;
using static TKFaxEngine.Audio.g711;
using static TKFaxEngine.Audio.tone_generate;

namespace TKFaxEngineTest.Audio.Test;

public static class echo_tests {
    private const int SAMPLE_RATE = 8000;
    private const int TEST_EC_TAPS = 256;
    private const int RESULT_CHANNELS = 7;
    private const string RESIDUE_FILE_NAME = "residue_sound.wav";
    private const float DBM0_MAX_POWER = 6.16f;

    private sealed class SignalSource {
        public string Name = string.Empty;
        public short[] Signal = [];
        public int Current;
        public float Gain = 1.0f;
    }

    private sealed class LevelMeasurementDevice {
        public readonly FirFloatState Fir = new();
        public readonly float[] History = new float[35 * 8];
        public int Position;
        public float Factor;
        public float Power;
        public float Peak;
        public int Type;
    }

    private sealed class ChannelModelState {
        public int ModelNumber;
        public float Erl;
        public readonly Fir32State Impulse = new();
        public float Gain;
        public int MungingCodec;
    }

    private static readonly ChannelModelState _channelModel = new();
    private static readonly SignalSource LocalCss = new();
    private static readonly SignalSource FarCss = new();
    private static Awgn LocalNoiseSource = new(1234567, -50.0);
    private static Awgn FarNoiseSource = new(7162534, -50.0);
    private static float _farHothNoise;
    private static Pcm16WaveWriter? _residueWriter;
    private static readonly short[] ResidueSound = new short[SAMPLE_RATE];
    private static int _residueCurrent;
    private static LevelMeasurementDevice _rinPowerMeter = null!;
    private static LevelMeasurementDevice _routPowerMeter = null!;
    private static LevelMeasurementDevice _sinPowerMeter = null!;
    private static LevelMeasurementDevice _soutPowerMeter = null!;
    private static LevelMeasurementDevice _sgenPowerMeter = null!;
    private static Pcm16WaveWriter? _resultWriter;
    private static readonly short[] ResultSound = new short[SAMPLE_RATE * RESULT_CHANNELS];
    private static int _resultCurrent;
    private static string _testName = string.Empty;
    private static int _lineModelNumber;
    private static int _supplementaryLineModelNumber;
    private static int _munger;
    private static bool _quiet = false;
    private static bool _useGui;
    private static float _erl;

    private static readonly int[] LineModelClearCoefficients = { 32768 };
    private static readonly int[] LineModelD2Coefficients = new int[]
    {
        -436, -829, -2797, -4208, -17968, -11215, 46150, 34480,
        -10427, 9049, -1309, -6320, 390, -8191, -1751, -6051,
        -3796, -4055, -3948, -2557, -3372, -1808, -2259, -1300,
        -1098, -618, -340, -61, 323, 419, 745, 716,
        946, 880, 1014, 976, 1033, 1091, 1053, 1042,
        794, 831, 899, 716, 390, 313, 304, 304,
        73, -119, -109, -176, -359, -407, -512, -580,
        -704, -618, -685, -791, -772, -820, -839, -724
    };
    private static readonly int[] LineModelD3Coefficients = new int[]
    {
        -381, 658, 1730, -51, -3511, -1418, 7660, 8861,
        -8106, -21370, -5307, 23064, 24020, 1020, -12374, -16296,
        -19524, -7480, 13509, 17115, 13952, 13952, 97, -9326,
        -9046, -15208, -9853, -3858, -1979, 6029, 5616, 7214,
        6820, 3935, 3919, 921, 1316, -693, -759, -1517,
        -2176, -2028, -2654, -1814, -2077, -1468, -1221, -842,
        -463, -298, -68, 64, 493, 723, 789, 954,
        756, 839, 872, 1020, 789, 822, 558, 658,
        476, 377, 377, 262, 97, -68, -183, -232,
        -331, -347, -430, -314, -430, -463, -463, -414,
        -381, -479, -479, -512, -479, -397, -430, -397,
        -298, -265, -249, -216, -249, -265, -166, -232
    };
    private static readonly int[] LineModelD4Coefficients = new int[]
    {
        -448, -436, 2230, 2448, -4178, -7050, 5846, 18581,
        2322, -26261, -16249, 21637, 25649, -2267, -10311, -4693,
        -12690, -7428, 14164, 13467, 4438, 8627, 456, -11879,
        -6352, -5104, -7496, 3271, 6566, 4277, 11131, 7562,
        1475, 3728, -3525, -7301, -3101, -9269, -6146, -2553,
        -6272, 811, 124, 788, 5147, 2172, 5387, 4598,
        3535, 4004, 2311, 2150, 1017, 330, -139, -573,
        -1100, -1157, -1180, -1455, -1123, -1386, -1123, -1066,
        -1020, -1100, -1008, -1077, -1088, -917, -917, -963,
        -814, -871, -734, -642, -562, -356, -379, -345,
        -230, -233, -333, -356, -390, -310, -265, -368,
        -310, -310, -390, -482, -459, -482, -551, -573
    };
    private static readonly int[] LineModelD5Coefficients = new int[]
    {
        160, 312, -241, -415, 897, 908, -1326, -1499,
        2405, 3347, -3624, -7733, 4041, 14484, -1477, -21739,
        -4470, 25356, 11458, -19696, -11800, 5766, 789, 6633,
        14624, -6975, -17156, -187, 149, 1515, 14907, 4345,
        -7128, -2757, -10185, -7083, 6850, 3944, 6969, 8694,
        -4068, -3852, -5793, -9371, 453, 1060, 3965, 9463,
        2393, 2784, -892, -7366, -3376, -5847, -2399, 3011,
        1537, 6623, 4205, 1602, 1592, -4752, -3646, -5207,
        -5577, -501, -1174, 4041, 5647, 4628, 7252, 2123,
        2654, -881, -4113, -3244, -7289, -3830, -4600, -2508,
        431, -144, 4184, 2372, 4617, 3576, 2382, 2839,
        -404, 539, -1803, -1401, -1705, -2269, -783, -1608,
        -220, -306, 257, 615, 225, 561, 8, 344,
        127, -57, 182, 41, 203, -111, 95, -79,
        30, 84, -13, -68, -241, -68, -24, 19,
        -57, -24, 30, -68, 84, -155, -68, 19
    };
    private static readonly int[] LineModelD6Coefficients = new int[]
    {
        293, 268, 475, 460, 517, 704, 581, 879,
        573, 896, 604, 787, 561, 538, 440, 97,
        265, -385, 20, -938, -523, -1438, -1134, -1887,
        -1727, -1698, -4266, -22548, -43424, 2743, 25897, 7380,
        21499, 11983, 10400, 11667, 3889, 7241, 925, 2018,
        -821, -2068, -2236, -4283, -3406, -5022, -4039, -4842,
        -4104, -4089, -3582, -2978, -2734, -1805, -1608, -645,
        -495, 279, 471, 947, 1186, 1438, 1669, 1640,
        1901, 1687, 1803, 1543, 1566, 1342, 1163, 963,
        733, 665, 323, 221, -14, -107, -279, -379,
        -468, -513, -473, -588, -612, -652, -616, -566,
        -515, -485, -404, -344, -290, -202, -180, -123
    };
    private static readonly int[] LineModelD7Coefficients = new int[]
    {
        29, 109, -83, 198, -294, -135, -415, -202,
        -444, -337, -313, -450, -105, -503, 145, -490,
        267, -231, 340, 77, 343, 783, 158, 1341,
        195, 1798, 344, 1845, 629, 1604, 1182, 940,
        5163, 19522, 8421, -50953, -9043, 18046, -13553, 13336,
        -3471, -107, 1788, -7409, 2469, -7994, 490, -3860,
        -837, 490, -636, 3682, 1141, 5019, 2635, 5025,
        3946, 4414, 4026, 3005, 3380, 1616, 2007, 158,
        388, -1198, -1117, -2134, -2547, -2589, -3310, -2778,
        -3427, -2779, -3116, -2502, -2399, -1956, -1539, -1239,
        -570, -377, 251, 331, 964, 1177, 1449, 1564,
        1724, 1871, 1767, 1802, 1630, 1632, 1379, 1271,
        1063, 856, 711, 482, 289, 54, -137, -321,
        -490, -638, -764, -836, -800, -859, -838, -837,
        -834, -740, -673, -581, -493, -436, -327, -201
    };
    private static readonly int[] LineModelD8Coefficients = new int[]
    {
        258, -111, 337, -319, 347, -434, 192, -450,
        -108, -343, -596, -177, -1187, -52, -1781, -147,
        -1959, -326, -1601, -1389, -13620, -720, 33818, -10683,
        -6742, 12489, -9862, 8950, -1574, 758, 3526, -3118,
        2421, -8966, -4901, 11385, 18072, -14410, -7473, 19836,
        -16854, -3115, 9483, -17799, 7399, -4342, -7415, 7929,
        -10726, 6239, -2526, -1317, 5345, -4565, 6868, -2195,
        3425, 1969, -109, 3963, -1275, 3087, -892, 1239,
        2, -427, 596, -1184, 551, -1244, 141, -743,
        -415, -372, -769, -183, -785, -270, -659, -377,
        -523, -325, -245, -255, -60, 35, 218, 149,
        340, 233, 365, 303, 251, 230, 209, 179
    };
    private static readonly int[] LineModelD9Coefficients = new int[]
    {
        80, 31, 4, 42, 42, -61, -81, -64,
        -121, -102, -26, 1002, -9250, -22562, 39321, 35681,
        -35289, 25312, -1457, -229, 15659, -6786, 16791, 3860,
        2239, -28730, -11885, 33871, -176, -16421, 18173, -9669,
        -10163, 9941, -19365, 3592, -5907, -10257, 5336, -12933,
        4348, -4802, -1791, 3035, -4433, 5553, -2596, 3992,
        1255, 1450, 4079, 324, 4340, 1059, 3083, 1917,
        1756, 2478, 1027, 1871, 845, 1284, 813, 806,
        869, 471, 646, 438, 449, 432, 473, 394,
        452, 538, 717, 723, 850, 756, 753, 899,
        555, 669, 619, 500, 650, 615, 516, 492,
        427, 291, 356, 147, 107, -50, -88, -59,
        -238, -165, -183
    };
    private static readonly float[] LevelMeasurementBandPassCoefficients = new float[]
    {
        0.0000f, 0.0006f, 0.0005f, 0.0004f, 0.0011f, 0.0000f, 0.0015f, -0.0003f,
        0.0012f, -0.0002f, 0.0000f, 0.0002f, -0.0020f, 0.0005f, -0.0040f, 0.0000f,
        -0.0047f, -0.0019f, -0.0033f, -0.0047f, 0.0000f, -0.0068f, 0.0036f, -0.0057f,
        0.0054f, 0.0000f, 0.0044f, 0.0095f, 0.0017f, 0.0188f, 0.0000f, 0.0225f,
        0.0024f, 0.0163f, 0.0092f, 0.0000f, 0.0164f, -0.0210f, 0.0161f, -0.0375f,
        0.0000f, -0.0406f, -0.0357f, -0.0267f, -0.0871f, 0.0000f, -0.1420f, 0.0289f,
        -0.1843f, 0.0475f, 0.8006f, 0.0475f, -0.1843f, 0.0289f, -0.1420f, 0.0000f,
        -0.0871f, -0.0267f, -0.0357f, -0.0406f, 0.0000f, -0.0375f, 0.0161f, -0.0210f,
        0.0164f, 0.0000f, 0.0092f, 0.0163f, 0.0024f, 0.0225f, 0.0000f, 0.0188f,
        0.0017f, 0.0095f, 0.0044f, 0.0000f, 0.0054f, -0.0057f, 0.0036f, -0.0068f,
        0.0000f, -0.0047f, -0.0033f, -0.0019f, -0.0047f, 0.0000f, -0.0040f, 0.0005f,
        -0.0020f, 0.0002f, 0.0000f, -0.0002f, 0.0012f, -0.0003f, 0.0015f, 0.0000f,
        0.0011f, 0.0004f, 0.0005f, 0.0006f, 0.0000f
    };
    private static readonly int[,] Tones6427 = new int[,]
    {
        { 697, 0 },
        { 941, 0 },
        { 1336, 0 },
        { 1633, 0 },
        { 697, 1209 },
        { 770, 1336 },
        { 852, 1477 },
        { 941, 1633 },
        { 0, 0 }
    };

    public static int main(string[] testArguments) {
        try { return tkfx_echo_tests_body_main(testArguments); } catch (EchoTestsExitException ex) { return ex.ExitCode; }
    }

    public static int tkfx_echo_tests_body_main(string[] testArguments) {
        bool simulate = false;
        bool comfortNoise = false;
        bool highPass = false;
        bool twoChannelFile = false;
        _lineModelNumber = 0;
        _supplementaryLineModelNumber = 0;
        _useGui = false;
        _munger = -1;
        _erl = -12.0f;

        var positional = new List<string>();
        for (int i = 0; i < testArguments.Length; i++) {
            string arg = testArguments[i];
            if (arg == "--") { for (i++; i < testArguments.Length; i++) positional.Add(testArguments[i]); break; }
            if (!arg.StartsWith('-') || arg == "-") { positional.Add(arg); continue; }
            switch (arg) {
                case "-2": twoChannelFile = true; break;
                case "-a": _munger = G711_ALAW; break;
                case "-c": comfortNoise = true; break;
                case "-g": _useGui = true; break;
                case "-h": highPass = true; break;
                case "-s": simulate = true; break;
                case "-u": _munger = G711_ULAW; break;
                case "-e": _erl = -MathF.Abs(ParseFloat(NextArgument(testArguments, ref i, arg))); break;
                case "-m": _lineModelNumber = ParseInt(NextArgument(testArguments, ref i, arg)); break;
                case "-M": _supplementaryLineModelNumber = ParseInt(NextArgument(testArguments, ref i, arg)); break;
                default: Exit(2); break;
            }
        }

        if (_useGui)
            echo_monitor.start_echo_can_monitor(TEST_EC_TAPS);

        LevelMeasurementsCreate(0);
        if (simulate) {
            int mode = ECHO_CAN_USE_NLP | (comfortNoise ? ECHO_CAN_USE_CNG : ECHO_CAN_USE_CLIP);
            if (highPass) mode |= ECHO_CAN_USE_TX_HPF | ECHO_CAN_USE_RX_HPF;
            int needed = twoChannelFile ? 2 : 3;
            if (positional.Count < needed) { Console.WriteLine("not enough arguments for a simulation"); Exit(2); }
            SimulateEchoCanceller([.. positional], twoChannelFile, mode);
        } else {
            SignalLoad(LocalCss, "Test-Files/local/sound_c1_8k.wav");
            SignalLoad(FarCss, "Test-Files/local/sound_c3_8k.wav");
            using var residue = new Pcm16WaveWriter(RESIDUE_FILE_NAME, 1, SAMPLE_RATE);
            _residueWriter = residue;
            if (positional.Count <= 0) {
                Console.WriteLine("No tests specified");
            } else {
                using var result = new Pcm16WaveWriter("echo_tests_result.wav", RESULT_CHANNELS, SAMPLE_RATE);
                _resultWriter = result;
                _resultCurrent = 0;
                DateTime started = DateTime.UtcNow;
                foreach (string test in positional) {
                    if (ChannelModelCreate(_channelModel, _lineModelNumber, _erl, _munger) != 0) {
                        Console.Error.WriteLine("    Failed to create line model");
                        Exit(2);
                    }
                    MatchTestName(test);
                }
                FlushResult();
                FlushResidue();
                Console.WriteLine($"Run time {(long)(DateTime.UtcNow - started).TotalSeconds}s");
            }
            _residueWriter = null;
            _resultWriter = null;
        }

        if (_useGui)
            echo_monitor.echo_can_monitor_wait_to_end();
        Console.WriteLine("Tests passed.");
        return 0;
    }

    private static void DumpEchoCancellerState(EchoCanState context) {
        echo_can_snapshot(context);
        using var writer = new StreamWriter("echo_tests_state.txt", false, new UTF8Encoding(false));
        foreach (short coefficient in context.SnapshotCoefficients)
            writer.WriteLine((coefficient / 32768.0f).ToString("R", CultureInfo.InvariantCulture));
    }

    private static void PutResidue(short amplitude) {
        if (_residueWriter is null) return;
        ResidueSound[_residueCurrent++] = amplitude;
        if (_residueCurrent >= SAMPLE_RATE) FlushResidue();
    }

    private static void FlushResidue() {
        if (_residueWriter is null || _residueCurrent <= 0) return;
        _residueWriter.WriteSamples(ResidueSound.AsSpan(0, _residueCurrent));
        _residueCurrent = 0;
    }

    private static void SignalLoad(SignalSource signal, string name) {
        using var reader = Pcm16WaveReader.Open(name);
        if (reader.Channels != 1) throw new InvalidDataException($"'{name}' must be mono.");
        signal.Name = name;
        signal.Signal = reader.ReadFrames(SAMPLE_RATE);
        if (signal.Signal.Length <= 0) { Console.Error.WriteLine($"    Error reading sound file '{name}'"); Exit(2); }
    }

    private static void SignalRestart(SignalSource signal, float gain) {
        signal.Current = 0;
        signal.Gain = MathF.Pow(10.0f, gain / 20.0f);
    }

    private static short SignalAmplitude(SignalSource signal) {
        short value = Saturate16(signal.Signal[signal.Current++] * signal.Gain);
        if (signal.Current >= signal.Signal.Length) signal.Current = 0;
        return value;
    }

    private static LevelMeasurementDevice LevelMeasurementDeviceCreate(int type) {
        var device = new LevelMeasurementDevice { Type = type, Position = 0, Factor = MathF.Exp(-1.0f / (SAMPLE_RATE * 0.035f)), Power = 0.0f, Peak = -99.0f };
        fir_float_create(device.Fir, LevelMeasurementBandPassCoefficients, LevelMeasurementBandPassCoefficients.Length);
        return device;
    }

    private static float LevelMeasurementDeviceGetPeak(LevelMeasurementDevice device) => device.Peak;
    private static float LevelMeasurementDeviceResetPeak(LevelMeasurementDevice device) { float old = device.Peak; device.Peak = -99.0f; return old; }

    private static float LevelMeasurement(LevelMeasurementDevice device, short amplitude) {
        float signal = fir_float(device.Fir, amplitude);
        signal *= signal;
        if (device.Type == 0) {
            device.Power = device.Power * device.Factor + signal * (1.0f - device.Factor);
            signal = MathF.Sqrt(device.Power);
        } else {
            device.Power += signal - device.History[device.Position];
            device.History[device.Position] = signal;
            device.Position = (device.Position + 1) % device.History.Length;
            signal = MathF.Sqrt(device.Power / (35.8f * 8.0f));
        }
        if (signal <= 0.0f) return -99.0f;
        float power = DBM0_MAX_POWER + 20.0f * MathF.Log10(signal / 32767.0f + 1.0e-10f);
        if (power > device.Peak) device.Peak = power;
        return power;
    }

    private static void LevelMeasurementsCreate(int type) {
        _rinPowerMeter = LevelMeasurementDeviceCreate(type);
        _routPowerMeter = LevelMeasurementDeviceCreate(type);
        _sinPowerMeter = LevelMeasurementDeviceCreate(type);
        _soutPowerMeter = LevelMeasurementDeviceCreate(type);
        _sgenPowerMeter = LevelMeasurementDeviceCreate(type);
    }

    private static void LevelMeasurementsUpdate(short rin, short sin, short rout, short sout, short sgen) {
        LevelMeasurement(_rinPowerMeter, rin); LevelMeasurement(_routPowerMeter, rout); LevelMeasurement(_sinPowerMeter, sin); LevelMeasurement(_soutPowerMeter, sout); LevelMeasurement(_sgenPowerMeter, sgen);
    }

    private static void LevelMeasurementsResetPeaks() {
        LevelMeasurementDeviceResetPeak(_rinPowerMeter); LevelMeasurementDeviceResetPeak(_routPowerMeter); LevelMeasurementDeviceResetPeak(_sinPowerMeter); LevelMeasurementDeviceResetPeak(_soutPowerMeter); LevelMeasurementDeviceResetPeak(_sgenPowerMeter);
    }

    private static void PrintResults() {
        if (!_quiet) Console.WriteLine("test  model  ERL   time     Max Rin  Max Rout Max Sgen Max Sin  Max Sout");
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,-4}  {1,-1}      {2,-5:F1}{3,6:F2}s{4,9:F2}{5,9:F2}{6,9:F2}{7,9:F2}{8,9:F2}", _testName, _channelModel.ModelNumber, 20.0f * MathF.Log10(-_channelModel.Erl + 1.0e-10f), 0.0f, _rinPowerMeter.Peak, _routPowerMeter.Peak, _sgenPowerMeter.Peak, _sinPowerMeter.Peak, _soutPowerMeter.Peak));
    }

    private static int ChannelModelCreate(ChannelModelState channel, int model, float erl, int codec) {
        int[][] models = { LineModelClearCoefficients, LineModelD2Coefficients, LineModelD3Coefficients, LineModelD4Coefficients, LineModelD5Coefficients, LineModelD6Coefficients, LineModelD7Coefficients, LineModelD8Coefficients, LineModelD9Coefficients };
        float[] gains = { 3.05e-5f, 1.39e-5f, 1.44e-5f, 1.52e-5f, 1.77e-5f, 9.33e-6f, 1.51e-5f, 2.33e-5f, 1.33e-5f };
        if ((uint)model >= models.Length) return -1;
        fir32_create(channel.Impulse, models[model], models[model].Length);
        channel.Gain = 32768.0f * MathF.Pow(10.0f, erl / 20.0f) * gains[model];
        channel.MungingCodec = codec; channel.ModelNumber = model; channel.Erl = erl;
        if (_useGui) echo_monitor.echo_can_monitor_line_model_update(models[model], models[model].Length);
        return 0;
    }

    private static short ChannelModel(short rout, short sgen) {
        sgen = Munge(_channelModel.MungingCodec, sgen);
        rout = Munge(_channelModel.MungingCodec, rout);
        short echo = fir32(_channelModel.Impulse, unchecked((short)(rout * _channelModel.Gain)));
        short sin = SaturatingAdd(echo, sgen);
        return Munge(_channelModel.MungingCodec, sin);
    }

    private static short Munge(int codec, short value) => codec switch { G711_ALAW => alaw_to_linear(linear_to_alaw(value)), G711_ULAW => ulaw_to_linear(linear_to_ulaw(value)), _ => value };
    private static short Silence() => 0;
    private static short LocalCssSignal() => SignalAmplitude(LocalCss);
    private static short FarCssSignal() => SignalAmplitude(FarCss);
    private static short LocalNoiseSignal() => LocalNoiseSource.NextSample();
    private static short FarNoiseSignal() => FarNoiseSource.NextSample();
    private static short FarHothNoiseSignal() { _farHothNoise = _farHothNoise * 0.625f + FarNoiseSource.NextSample() * 0.375f; return Saturate16(_farHothNoise); }

    private static void RunTest(EchoCanState context, Func<short> transmitSource, Func<short> receiveSource, int periodMilliseconds) {
        int samples = checked(periodMilliseconds * SAMPLE_RATE / 1000);
        for (int i = 0; i < samples; i++) {
            short rin = transmitSource(); short sgen = receiveSource(); short rout = echo_can_hpf_tx(context, rin); short sin = ChannelModel(rout, sgen); short sout = echo_can_update(context, rout, sin);
            LevelMeasurementsUpdate(rin, sin, rout, sout, sgen);
            AddResultFrame(rin, sgen, sin, rout, sout, Saturate16(sout - sgen), 0);
        }
        FlushResult();
        if (_useGui) { echo_can_snapshot(context); short[] taps = context.SnapshotCoefficients.ToArray(); echo_monitor.echo_can_monitor_can_update(taps, taps.Length); }
    }

    private static void AddResultFrame(short a, short b, short c, short d, short e, short f, short g) {
        ResultSound[_resultCurrent++] = a; ResultSound[_resultCurrent++] = b; ResultSound[_resultCurrent++] = c; ResultSound[_resultCurrent++] = d; ResultSound[_resultCurrent++] = e; ResultSound[_resultCurrent++] = f; ResultSound[_resultCurrent++] = g;
        if (_resultCurrent >= ResultSound.Length) FlushResult();
    }

    private static void FlushResult() {
        if (_resultWriter is null || _resultCurrent <= 0) return;
        _resultWriter.WriteSamples(ResultSound.AsSpan(0, _resultCurrent));
        _resultCurrent = 0;
    }

    private static void PrintTestTitle(string title) { if (!_quiet) Console.Write(title); }

    private static int PerformTestSanity() {
        PrintTestTitle("Performing basic sanity test\n"); using EchoCanState context = echo_can_init(TEST_EC_TAPS, 0); echo_can_flush(context); SignalRestart(LocalCss, 0.0f);
        RunTest(context, Silence, Silence, 200); RunTest(context, LocalCssSignal, Silence, 5000); echo_can_adaption_mode(context, ECHO_CAN_USE_ADAPTION | ECHO_CAN_USE_NLP | ECHO_CAN_USE_CNG); RunTest(context, LocalCssSignal, Silence, 5000); echo_can_adaption_mode(context, ECHO_CAN_USE_ADAPTION);
        for (int i = 0; i < SAMPLE_RATE * 10; i++) { short tx = LocalCssSignal(); short farTx = 0; short rx = ChannelModel(tx, farTx); short clean = echo_can_update(context, tx, rx); AddResultFrame(tx, 0, rx, tx, clean, Saturate16(clean - farTx), 0); PutResidue(Saturate16(clean - farTx)); }
        FlushResult(); return 0;
    }

    private static int PerformTest2A() {
        PrintTestTitle("Performing test 2A - Convergence and reconvergence test with NLP enabled\n"); using EchoCanState context = echo_can_init(TEST_EC_TAPS, 0); echo_can_flush(context); echo_can_adaption_mode(context, ECHO_CAN_USE_ADAPTION | ECHO_CAN_USE_NLP); RunTest(context, Silence, Silence, 200); SignalRestart(LocalCss, 0.0f); RunTest(context, LocalCssSignal, Silence, 1000); LevelMeasurementsResetPeaks(); RunTest(context, LocalCssSignal, Silence, 9000); PrintResults(); PrintPass(_soutPowerMeter.Peak <= -65.0f);
        if (ChannelModelCreate(_channelModel, _supplementaryLineModelNumber, _erl, _munger) != 0) Exit(2); SignalRestart(LocalCss, 0.0f); RunTest(context, LocalCssSignal, Silence, 1000); LevelMeasurementsResetPeaks(); RunTest(context, LocalCssSignal, Silence, 9000); PrintResults(); PrintPass(_soutPowerMeter.Peak <= -65.0f); return 0;
    }

    private static int PerformTest2B() {
        PrintTestTitle("Performing test 2B - Convergence and reconverge with NLP disabled\n"); using EchoCanState context = echo_can_init(TEST_EC_TAPS, 0); echo_can_flush(context); echo_can_adaption_mode(context, ECHO_CAN_USE_ADAPTION); SignalRestart(LocalCss, 0.0f); RunTest(context, Silence, Silence, 200); RunTest(context, LocalCssSignal, Silence, 1000); LevelMeasurementsResetPeaks(); RunTest(context, LocalCssSignal, Silence, 9000); PrintResults(); LevelMeasurementsResetPeaks(); RunTest(context, LocalCssSignal, Silence, 170000); PrintResults(); PrintPass(_soutPowerMeter.Peak <= -65.0f);
        if (ChannelModelCreate(_channelModel, _supplementaryLineModelNumber, _erl, _munger) != 0) Exit(2); RunTest(context, LocalCssSignal, Silence, 1000); LevelMeasurementsResetPeaks(); RunTest(context, LocalCssSignal, Silence, 9000); PrintResults(); LevelMeasurementsResetPeaks(); RunTest(context, LocalCssSignal, Silence, 170000); PrintResults(); PrintPass(_soutPowerMeter.Peak <= -65.0f); return 0;
    }

    private static int PerformTest2Ca() {
        PrintTestTitle("Performing test 2C(a) - Convergence with background noise present\n"); using EchoCanState context = echo_can_init(TEST_EC_TAPS, 0); FarNoiseSource.RestartDbm0(7162534, -50.0); echo_can_flush(context); echo_can_adaption_mode(context, ECHO_CAN_USE_ADAPTION); SignalRestart(LocalCss, 0.0f); RunTest(context, Silence, Silence, 200); FarNoiseSource.RestartDbm0(7162534, -40.0); RunTest(context, LocalCssSignal, FarHothNoiseSignal, 5000); echo_can_adaption_mode(context, 0); LevelMeasurementsResetPeaks(); RunTest(context, LocalCssSignal, Silence, 5000); PrintResults(); PrintPass(_soutPowerMeter.Peak <= _sgenPowerMeter.Peak); return 0;
    }

    private static int PerformTest3A() => RunDoubleTalk("Performing test 3A - Double talk test with low cancelled-end levels\n", -20.0f, false, false);
    private static int PerformTest3Ba() => RunDoubleTalk("Performing test 3B(b) - Double talk stability test with high cancelled-end levels\n", 0.0f, true, true);
    private static int PerformTest3Bb() => RunDoubleTalk("Performing test 3B(b) - Double talk stability test with low cancelled-end levels\n", -15.0f, true, false);

    private static int RunDoubleTalk(string title, float farGain, bool convergeFirst, bool farDuringFreeze) {
        PrintTestTitle(title); using EchoCanState context = echo_can_init(TEST_EC_TAPS, 0); echo_can_flush(context); echo_can_adaption_mode(context, ECHO_CAN_USE_ADAPTION); RunTest(context, Silence, Silence, 200); SignalRestart(LocalCss, 0.0f); SignalRestart(FarCss, farGain); if (convergeFirst) RunTest(context, LocalCssSignal, Silence, 5000); RunTest(context, LocalCssSignal, FarCssSignal, 5000); echo_can_adaption_mode(context, 0); RunTest(context, LocalCssSignal, farDuringFreeze ? FarCssSignal : Silence, convergeFirst ? 1000 : 500); if (convergeFirst) RunTest(context, LocalCssSignal, Silence, 500); LevelMeasurementsResetPeaks(); RunTest(context, LocalCssSignal, Silence, 5000); PrintResults(); if (!convergeFirst) PrintPass(_soutPowerMeter.Peak <= _sgenPowerMeter.Peak); return 0;
    }

    private static int PerformTest3C() {
        PrintTestTitle("Performing test 3C - Double talk test with simulated conversation\n"); using EchoCanState context = echo_can_init(TEST_EC_TAPS, 0); echo_can_flush(context); echo_can_adaption_mode(context, ECHO_CAN_USE_ADAPTION); RunTest(context, Silence, Silence, 200); SignalRestart(LocalCss, 0.0f); SignalRestart(FarCss, -15.0f); RunTest(context, LocalCssSignal, FarCssSignal, 5600); LevelMeasurementsResetPeaks(); RunTest(context, LocalCssSignal, Silence, 1400); PrintResults(); RunTest(context, LocalCssSignal, Silence, 5000); SignalRestart(FarCss, 0.0f); RunTest(context, LocalCssSignal, FarCssSignal, 5600); RunTest(context, Silence, FarCssSignal, 5600); return 0;
    }

    private static int PerformTest4() {
        PrintTestTitle("Performing test 4 - Leak rate test\n"); using EchoCanState context = echo_can_init(TEST_EC_TAPS, 0); echo_can_flush(context); echo_can_adaption_mode(context, ECHO_CAN_USE_ADAPTION); RunTest(context, Silence, Silence, 200); SignalRestart(LocalCss, 0.0f); RunTest(context, LocalCssSignal, Silence, 5000); RunTest(context, Silence, Silence, 120000); echo_can_adaption_mode(context, 0); LevelMeasurementsResetPeaks(); RunTest(context, LocalCssSignal, Silence, 5000); PrintResults(); return 0;
    }

    private static int PerformTest5() {
        PrintTestTitle("Performing test 5 - Infinite return loss convergence test\n"); using EchoCanState context = echo_can_init(TEST_EC_TAPS, 0); echo_can_flush(context); echo_can_adaption_mode(context, ECHO_CAN_USE_ADAPTION); SignalRestart(LocalCss, 0.0f); RunTest(context, LocalCssSignal, Silence, 5000); RunTest(context, LocalCssSignal, Silence, 5000); PrintResults(); return 0;
    }

    private static int PerformTest6() {
        PrintTestTitle("Performing test 6 - Non-divergence on narrow-band signals\n");
        using EchoCanState context = echo_can_init(TEST_EC_TAPS, 0);
        echo_can_flush(context);
        echo_can_adaption_mode(context, ECHO_CAN_USE_ADAPTION);
        SignalRestart(LocalCss, 0.0f);
        RunTest(context, LocalCssSignal, Silence, 5000);
        short[] localSound = new short[SAMPLE_RATE];

        for (int k = 0; k < Tones6427.GetLength(0) && Tones6427[k, 0] != 0; k++) {
            tone_gen_descriptor_t descriptor = tone_gen_descriptor_init(
                null, Tones6427[k, 0], -11, Tones6427[k, 1], -9, 1, 0, 0, 0, 1)!;
            tone_gen_state_t tone = tone_gen_init(null, descriptor)!;
            for (int second = 0; second < 5; second++) {
                tone_gen(tone, localSound, SAMPLE_RATE);
                for (int j = 0; j < SAMPLE_RATE; j++) {
                    short tx = localSound[j];
                    short clean = echo_can_update(context, tx, ChannelModel(tx, 0));
                    PutResidue(clean);
                }
            }
            tone_gen_free(tone);
            tone_gen_descriptor_free(descriptor);
        }
        return 0;
    }

    private static int PerformTest7() {
        PrintTestTitle("Performing test 7 - Stability\n");
        using EchoCanState context = echo_can_init(TEST_EC_TAPS, 0);
        echo_can_flush(context);
        echo_can_adaption_mode(context, ECHO_CAN_USE_ADAPTION);
        tone_gen_descriptor_t descriptor = tone_gen_descriptor_init(
            null, Tones6427[0, 0], -11, Tones6427[0, 1], -9, 1, 0, 0, 0, 1)!;
        tone_gen_state_t tone = tone_gen_init(null, descriptor)!;
        short[] localSound = new short[SAMPLE_RATE];
        for (int second = 0; second < 120; second++) {
            tone_gen(tone, localSound, SAMPLE_RATE);
            for (int j = 0; j < SAMPLE_RATE; j++) {
                short tx = localSound[j];
                short clean = echo_can_update(context, tx, ChannelModel(tx, 0));
                PutResidue(clean);
            }
        }
        tone_gen_free(tone);
        tone_gen_descriptor_free(descriptor);
        return 0;
    }

    private static int PerformTest9() {
        PrintTestTitle("Performing test 9 - Comfort noise test\n"); using EchoCanState context = echo_can_init(TEST_EC_TAPS, 0); FarNoiseSource.RestartDbm0(7162534, -50.0); echo_can_flush(context); echo_can_adaption_mode(context, ECHO_CAN_USE_ADAPTION | ECHO_CAN_USE_NLP | ECHO_CAN_USE_CNG); SignalRestart(LocalCss, 0.0f); RunTest(context, LocalCssSignal, Silence, 5000); FarNoiseSource.RestartDbm0(7162534, -45.0); RunTest(context, Silence, FarNoiseSignal, 30000); LocalNoiseSource.RestartDbm0(1234567, -10.0); RunTest(context, LocalNoiseSignal, FarNoiseSignal, 2000); FarNoiseSource.RestartDbm0(7162534, -55.0); RunTest(context, Silence, FarNoiseSignal, 10000); RunTest(context, LocalNoiseSignal, FarNoiseSignal, 2000); return 0;
    }

    private static int SourceUnimplementedTest(string title, string message) {
        PrintTestTitle(title); using EchoCanState context = echo_can_init(TEST_EC_TAPS, 0); Console.Error.WriteLine(message); return 0;
    }

    private static int MatchTestName(string name) {
        var tests = new Dictionary<string, Func<int>>(StringComparer.OrdinalIgnoreCase) {
            ["sanity"] = PerformTestSanity,
            ["2a"] = PerformTest2A,
            ["2b"] = PerformTest2B,
            ["2ca"] = PerformTest2Ca,
            ["3a"] = PerformTest3A,
            ["3ba"] = PerformTest3Ba,
            ["3bb"] = PerformTest3Bb,
            ["3c"] = PerformTest3C,
            ["4"] = PerformTest4,
            ["5"] = PerformTest5,
            ["6"] = PerformTest6,
            ["7"] = PerformTest7,
            ["8"] = () => SourceUnimplementedTest("Performing test 8 - Non-convergence on No 5, 6, and 7 in-band signalling\n", "Test 8 not yet implemented"),
            ["9"] = PerformTest9,
            ["10a"] = () => SourceUnimplementedTest("Performing test 10A - Canceller operation on the calling station side\n", "Test 10A not yet implemented"),
            ["10b"] = () => SourceUnimplementedTest("Performing test 10B - Canceller operation on the called station side\n", "Test 10B not yet implemented"),
            ["10c"] = () => SourceUnimplementedTest("Performing test 10C - Canceller operation on the calling station side during page transmission and page breaks (for further study)\n", "Test 10C not yet implemented"),
            ["11"] = () => SourceUnimplementedTest("Performing test 11 - Tandem echo canceller test (for further study)\n", "Test 11 not yet implemented"),
            ["12"] = () => SourceUnimplementedTest("Performing test 12 - Residual acoustic echo test (for further study)\n", "Test 12 not yet implemented"),
            ["13"] = () => SourceUnimplementedTest("Performing test 13 - Performance with ITU-T low-bit rate coders in echo path (Optional, under study)\n", "Test 13 not yet implemented"),
            ["14"] = () => SourceUnimplementedTest("Performing test 14 - Performance with V-series low-speed data modems\n", "Test 14 not yet implemented"),
            ["15"] = () => SourceUnimplementedTest("Performing test 15 - PCM offset test (Optional)\n", "Test 15 not yet implemented")
        };
        if (!tests.TryGetValue(name, out Func<int>? test)) { Console.WriteLine($"Unknown test name '{name}' specified. The known test names are {string.Join(", ", tests.Keys)}"); return -1; }
        _testName = name; return test();
    }

    private static void SimulateEchoCanceller(string[] paths, bool twoChannelFile, int mode) {
        mode |= ECHO_CAN_USE_ADAPTION; using EchoCanState context = echo_can_init(TEST_EC_TAPS, 0); echo_can_adaption_mode(context, mode);
        if (twoChannelFile) {
            using var combined = Pcm16WaveReader.Open(paths[0]); if (combined.Channels != 2) throw new InvalidDataException("The simulation source must contain two channels."); using var output = new Pcm16WaveWriter(paths[1], 1, SAMPLE_RATE); short[] frames = combined.ReadFrames(int.MaxValue); for (int i = 0; i + 1 < frames.Length; i += 2) ProcessSimulationFrame(context, frames[i], frames[i + 1], output);
        } else {
            using var tx = Pcm16WaveReader.Open(paths[0]); using var rx = Pcm16WaveReader.Open(paths[1]); using var output = new Pcm16WaveWriter(paths[2], 1, SAMPLE_RATE); short[] txSamples = tx.ReadFrames(int.MaxValue); short[] rxSamples = rx.ReadFrames(int.MaxValue); int count = Math.Min(txSamples.Length, rxSamples.Length); for (int i = 0; i < count; i++) ProcessSimulationFrame(context, txSamples[i], rxSamples[i], output);
        }
        DumpEchoCancellerState(context);
    }

    private static void ProcessSimulationFrame(EchoCanState context, short rin, short sin, Pcm16WaveWriter output) {
        short rout = echo_can_hpf_tx(context, rin); short sout = echo_can_update(context, rout, sin); output.WriteSample(sout); LevelMeasurementsUpdate(rin, sin, rout, sout, 0); if (_useGui) { echo_can_snapshot(context); short[] taps = context.SnapshotCoefficients.ToArray(); echo_monitor.echo_can_monitor_can_update(taps, taps.Length); }
    }

    private static void PrintPass(bool passed) => Console.WriteLine(passed ? "Test passed" : "Test failed");
    private static short SaturatingAdd(short left, short right) => Saturate16(left + right);
    private static short Saturate16(double value) => value >= short.MaxValue ? short.MaxValue : value <= short.MinValue ? short.MinValue : (short)Math.Round(value, MidpointRounding.ToEven);
    private static int ParseInt(string value) => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    private static float ParseFloat(string value) => float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    private static string NextArgument(string[] args, ref int index, string option) { index++; if (index >= args.Length) throw new ArgumentException($"Missing value for {option}."); return args[index]; }
    private static void Exit(int code) => throw new EchoTestsExitException(code);

    public sealed class EchoTestsExitException : Exception {
        public EchoTestsExitException(int exitCode) : base($"echo_tests exited with code {exitCode}.") => ExitCode = exitCode;
        public int ExitCode { get; }
    }

    private sealed class Pcm16WaveWriter : IDisposable {
        private readonly FileStream _stream; private readonly BinaryWriter _writer; private long _samples;
        public Pcm16WaveWriter(string path, short channels, int sampleRate) { Channels = channels; _stream = File.Create(path); _writer = new BinaryWriter(_stream, Encoding.ASCII, true); _writer.Write(Encoding.ASCII.GetBytes("RIFF")); _writer.Write(0); _writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); _writer.Write(16); _writer.Write((short)1); _writer.Write(channels); _writer.Write(sampleRate); _writer.Write(sampleRate * channels * 2); _writer.Write((short)(channels * 2)); _writer.Write((short)16); _writer.Write(Encoding.ASCII.GetBytes("data")); _writer.Write(0); }
        public short Channels { get; }
        public void WriteSample(short sample) { _writer.Write(sample); _samples++; }
        public void WriteSamples(ReadOnlySpan<short> samples) { foreach (short sample in samples) _writer.Write(sample); _samples += samples.Length; }
        public void Dispose() { long dataLength = _samples * 2; _stream.Position = 4; _writer.Write(checked((int)(36 + dataLength))); _stream.Position = 40; _writer.Write(checked((int)dataLength)); _writer.Dispose(); _stream.Dispose(); }
    }

    private sealed class Pcm16WaveReader : IDisposable {
        private readonly FileStream _stream; private readonly BinaryReader _reader; private readonly long _dataEnd;
        private Pcm16WaveReader(FileStream stream, BinaryReader reader, short channels, int sampleRate, long dataStart, long dataLength) { _stream = stream; _reader = reader; Channels = channels; SampleRate = sampleRate; _stream.Position = dataStart; _dataEnd = dataStart + dataLength; }
        public short Channels { get; }
        public int SampleRate { get; }
        public static Pcm16WaveReader Open(string path) { var stream = File.OpenRead(path); var reader = new BinaryReader(stream, Encoding.ASCII, true); if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF") throw new InvalidDataException("RIFF header missing."); reader.ReadInt32(); if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE") throw new InvalidDataException("WAVE header missing."); short format = 0, channels = 0, bits = 0; int rate = 0; long dataStart = 0, dataLength = 0; while (stream.Position + 8 <= stream.Length) { string id = Encoding.ASCII.GetString(reader.ReadBytes(4)); int size = reader.ReadInt32(); long next = stream.Position + size + (size & 1); if (id == "fmt ") { format = reader.ReadInt16(); channels = reader.ReadInt16(); rate = reader.ReadInt32(); reader.ReadInt32(); reader.ReadInt16(); bits = reader.ReadInt16(); } else if (id == "data") { dataStart = stream.Position; dataLength = size; } stream.Position = next; if (dataStart != 0 && format != 0) break; } if (format != 1 || bits != 16 || channels <= 0 || dataStart == 0) throw new InvalidDataException("Only PCM16 WAV is supported."); return new Pcm16WaveReader(stream, reader, channels, rate, dataStart, dataLength); }
        public short[] ReadFrames(int maximumFrames) { int frameBytes = Channels * 2; long availableFrames = (_dataEnd - _stream.Position) / frameBytes; int frames = (int)Math.Min(maximumFrames, availableFrames); short[] data = new short[frames * Channels]; for (int i = 0; i < data.Length; i++) data[i] = _reader.ReadInt16(); return data; }
        public void Dispose() { _reader.Dispose(); _stream.Dispose(); }
    }
}
