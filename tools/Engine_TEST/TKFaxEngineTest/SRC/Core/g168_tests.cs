/* TKFaxEngine - g168_tests.cs. Managed C# port retaining the original G.168 filter, CSS and sweep tests. */
#nullable enable

using System.Globalization;
using TKFaxEngine.Audio;
using static TKFaxEngine.Audio.Fir;
using static TKFaxEngine.Audio.tone_generate;

namespace TKFaxEngineTest.Core.Test;

public static class g168_tests {
    private const int SAMPLE_RATE = 8000; private const float LINE_MODEL_D2_GAIN = 1.39E-5f, LINE_MODEL_D3_GAIN = 1.44E-5f, LINE_MODEL_D4_GAIN = 1.52E-5f, LINE_MODEL_D5_GAIN = 1.77E-5f, LINE_MODEL_D6_GAIN = 9.33E-6f, LINE_MODEL_D7_GAIN = 1.51E-5f, LINE_MODEL_D8_GAIN = 2.33E-5f, LINE_MODEL_D9_GAIN = 1.33E-5f;
    private static readonly int[] line_model_d2_coeffs =
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

    private static readonly int[] line_model_d3_coeffs =
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

    private static readonly int[] line_model_d4_coeffs =
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

    private static readonly int[] line_model_d5_coeffs =
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

    private static readonly int[] line_model_d6_coeffs =
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

    private static readonly int[] line_model_d7_coeffs =
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

    private static readonly int[] line_model_d8_coeffs =
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

    private static readonly int[] line_model_d9_coeffs =
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

    private static readonly float[] level_measurement_bp_coeffs =
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

    private static readonly int[] css_c1 =
    {
        -155, 276, 517, 578, 491, 302, 86, -103,
        -207, -198, 60, 190, 543, 948, 1362, 1741,
        2043, 2276, 2422, 2500, 2552, 2595, 2655, 2758,
        2896, 3060, 3224, 3370, 3500, 3569, 3603, 3603,
        3595, 3586, 3595, 3638, 3724, 3819, 3922, 4000,
        4043, 4034, 3974, 3862, 3724, 3577, 3439, 3336,
        3267, 3224, 3198, 3172, 3129, 3043, 2914, 2750,
        2560, 2353, 2155, 1991, 1853, 1750, 1672, 1603,
        1534, 1440, 1310, 1146, 965, 776, 603, 448,
        345, 276, 250, 250, 267, 267, 241, 190,
        103, -9, -138, -267, -388, -491, -569, -638,
        -698, -759, -813, -888, -957, -1034, -1103, -1146,
        -1181, -1190, -1198, -1215, -1259, -1327, -1457, -1629,
        -1853, -2121, -2414, -2707, -3017, -3319, -3612, -3913,
        -4224, -4560, -4922, -5301, -5715, -6137, -6560, -6948,
        -7301, -7568, -7732, -7758, -7620, -7310, -6810, -6155,
        -5344, -4439, -3474, -2508, -1595, -802
    };

    private static readonly int[] css_c3 =
    {
        -198, -112, -9, 103, 233, 388, 543, 724,
        896, 1060, 1233, 1388, 1517, 1638, 1747, 1810,
        1845, 1845, 1802, 1707, 1569, 1379, 1146, 871,
        560, 233, -121, -491, -871, -1250, -1638, -2043,
        -2465, -2896, -3345, -3819, -4310, -4810, -5319, -5836,
        -6353, -6853, -7353, -7836, -8292, -8715, -9077, -9370,
        -9542, -9542, -9361, -8956, -8327, -7465, -6396, -5163,
        -3827, -2448, -1103, 155, 1293, 2241, 3034, 3655,
        4138, 4517, 4827, 5094, 5344, 5594, 5827, 6043,
        6215, 6344, 6413, 6422, 6379, 6310, 6215, 6120,
        6051, 6000, 5991, 5991, 6000, 6008, 5991, 5939,
        5853, 5715, 5560, 5387, 5215, 5043, 4879, 4732,
        4586, 4439, 4276, 4086, 3870, 3629, 3370, 3086,
        2801, 2534, 2267, 2034, 1819, 1612, 1422, 1224,
        1026, 819, 603, 388, 181, 9, -181, -328,
        -448, -543, -629, -707, -784, -871, -948, -1026,
        -1112, -1181, -1241, -1276, -1293, -1302, -1293, -1267,
        -1250, -1233, -1224, -1224, -1224, -1224, -1215, -1198,
        -1172, -1129, -1077, -1026, -974, -922, -888, -871,
        -845, -828, -810, -793, -767, -741, -698, -672,
        -638, -603, -595, -586, -595, -603, -621, -629,
        -938, -638, -638, -638, -638, -638, -647, -664,
        -690, -724, -767, -793, -819, -845, -853, -871,
        -879, -888, -896, -922, -948, -974, -1009, -1026,
        -1052, -1069, -1077, -1069, -1060, -1060, -1052, -1043,
        -1043, -1052, -1060, -1060, -1060, -1052, -1034, -1017,
        -991, -957, -931, -905, -888, -862, -845, -819,
        -793, -767, -724, -672, -621, -560, -509, -457,
        -397, -345, -276, -207, -112
    };

    public sealed class G168TestsExitException : Exception { public G168TestsExitException(int exitCode) : base($"g168_tests exited with code {exitCode}.") => ExitCode = exitCode; public int ExitCode { get; } }
    private static void Exit(int code) => throw new G168TestsExitException(code);
    public static int main(string[] testArguments) { try { return tkfx_g168_tests_body_main(testArguments); } catch (G168TestsExitException caught) { return caught.ExitCode; } }
    private sealed class SignalSource : IDisposable { private short[] _signal; private int _current; private float _gain; public SignalSource(string path) { using Pcm16WaveReader reader = new(path, SAMPLE_RATE, 1); _signal = new short[SAMPLE_RATE]; int count = reader.ReadFrames(_signal); if (count <= 0) Exit(2); if (count != _signal.Length) Array.Resize(ref _signal, count); } public void Restart(float gainDb) { _current = 0; _gain = MathF.Pow(10, gainDb / 20); } public short Next() { int value = (int)(_signal[_current++] * _gain); if (_current >= _signal.Length) _current = 0; return unchecked((short)value); } public void Dispose() { } }
    public static int tkfx_g168_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments); using SignalSource local = new("Test-Files/local/sound_c1_8k.wav"); using SignalSource far = new("Test-Files/local/sound_c3_8k.wav"); _ = far;
        Fir32State[] filters = new Fir32State[8]; int[][] coeffs = { line_model_d2_coeffs, line_model_d3_coeffs, line_model_d4_coeffs, line_model_d5_coeffs, line_model_d6_coeffs, line_model_d7_coeffs, line_model_d8_coeffs, line_model_d9_coeffs }; for (int i = 0; i < 8; i++) { filters[i] = new Fir32State(); fir32_create(filters[i], coeffs[i], coeffs[i].Length); }
        FirFloatState bandpass = new(); fir_float_create(bandpass, level_measurement_bp_coeffs, level_measurement_bp_coeffs.Length); float[] gains = { LINE_MODEL_D2_GAIN, LINE_MODEL_D3_GAIN, LINE_MODEL_D4_GAIN, LINE_MODEL_D5_GAIN, LINE_MODEL_D6_GAIN, LINE_MODEL_D7_GAIN, LINE_MODEL_D8_GAIN, LINE_MODEL_D9_GAIN }; short[] samples = new short[SAMPLE_RATE]; float[] power = new float[10];
        for (int frequency = 10; frequency < 4000; frequency++) { tone_gen_descriptor_t descriptor = tone_gen_descriptor_init(null, frequency, -10, 0, 0, 1, 0, 0, 0, 1) ?? throw new InvalidOperationException(); tone_gen_state_t tone = tone_gen_init(null, descriptor) ?? throw new InvalidOperationException(); tone_gen(tone, samples, SAMPLE_RATE); Array.Clear(power); for (int i = 0; i < 800; i++) { for (int model = 0; model < 8; model++) { int signal = fir32(filters[model], samples[i]); power[model] += (signal * signal - power[model]) / 32f; } int bp = fir_float(bandpass, samples[i]); power[8] += (bp * bp - power[8]) / 32f; int raw = samples[i]; power[9] += (raw * raw - power[9]) / 32f; } WritePower(frequency, power, gains, true); tone_gen_free(tone); }
        local.Restart(0); for (int pass = 0; pass < 2; pass++) { Array.Clear(power); for (int i = 0; i < SAMPLE_RATE; i++) { short value = local.Next(); for (int model = 0; model < 8; model++) { int signal = fir32(filters[model], value); power[model] += (signal * signal - power[model]) / 32f; } int bp = fir_float(bandpass, value); power[8] += (bp * bp - power[8]) / 32f; int raw = value; power[9] += (raw * raw - power[9]) / 32f; } }
        WritePower(0, power, gains, true); WritePower(0, power, gains, false); double reference = Math.Sqrt(power[9]); Console.Write("0"); for (int i = 0; i < 8; i++) Console.Write(" " + (Math.Sqrt(power[i]) / reference).ToString("F6", CultureInfo.InvariantCulture)); Console.WriteLine(" " + Math.Sqrt(power[8]).ToString("F6", CultureInfo.InvariantCulture) + " " + reference.ToString("F6", CultureInfo.InvariantCulture)); Console.Write("0"); for (int i = 0; i < 8; i++) Console.Write(" " + (Math.Sqrt(power[i]) * gains[i] / reference).ToString("F6", CultureInfo.InvariantCulture)); Console.WriteLine(" " + Math.Sqrt(power[8]).ToString("F6", CultureInfo.InvariantCulture) + " " + reference.ToString("F6", CultureInfo.InvariantCulture)); foreach (int value in css_c1) Console.WriteLine(value); Console.WriteLine(); foreach (int value in css_c3) Console.WriteLine(value); foreach (Fir32State filter in filters) fir32_free(filter); fir_float_free(bandpass); return 0;
    }
    private static void WritePower(int index, float[] power, float[] gains, bool applyGain) { Console.Write(index); for (int i = 0; i < 8; i++) { double value = Math.Sqrt(power[i]); if (applyGain) value *= gains[i]; Console.Write(" " + value.ToString("F6", CultureInfo.InvariantCulture)); } Console.Write(" " + Math.Sqrt(power[8]).ToString("F6", CultureInfo.InvariantCulture)); Console.WriteLine(" " + Math.Sqrt(power[9]).ToString("F6", CultureInfo.InvariantCulture)); }

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

}
