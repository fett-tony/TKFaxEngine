/*
 * TKFaxEngine - managed C# port
 *
 * t42_t43_local.cs
 *
 * Direct port of t42_t43_local.h and the common CIELAB routines in t42.c.
 */

#nullable enable

using System.Buffers.Binary;

namespace TKFaxEngine.FaxImage;

public sealed class LabParameters {
    public float range_L { get; internal set; }
    public float range_a { get; internal set; }
    public float range_b { get; internal set; }
    public float offset_L { get; internal set; }
    public float offset_a { get; internal set; }
    public float offset_b { get; internal set; }
    public int ab_are_signed { get; internal set; }
    public float x_n { get; internal set; }
    public float y_n { get; internal set; }
    public float z_n { get; internal set; }
    public float x_rn { get; internal set; }
    public float y_rn { get; internal set; }
    public float z_rn { get; internal set; }
}

public static class T42T43Local {
    private readonly record struct Illuminant(byte[] Tag, string Name, float Xn, float Yn, float Zn);
    private readonly record struct Uvt(double U, double V, double T);

    private static readonly Illuminant[] Illuminants =
    [
        new([0, (byte)'D', (byte)'5', (byte)'0'], "CIE D50/2°", 96.422f, 100.000f, 82.521f),
        new([], "CIE D50/10°", 96.720f, 100.000f, 81.427f),
        new([], "CIE D55/2°", 95.682f, 100.000f, 92.149f),
        new([], "CIE D55/10°", 95.799f, 100.000f, 90.926f),
        new([0, (byte)'D', (byte)'6', (byte)'5'], "CIE D65/2°", 95.047f, 100.000f, 108.883f),
        new([], "CIE D65/10°", 94.811f, 100.000f, 107.304f),
        new([0, (byte)'D', (byte)'7', (byte)'5'], "CIE D75/2°", 94.972f, 100.000f, 122.638f),
        new([], "CIE D75/10°", 94.416f, 100.000f, 120.641f),
        new([0, 0, (byte)'F', (byte)'2'], "F02/2°", 99.186f, 100.000f, 67.393f),
        new([], "F02/10°", 103.279f, 100.000f, 69.027f),
        new([0, 0, (byte)'F', (byte)'7'], "F07/2°", 95.041f, 100.000f, 108.747f),
        new([], "F07/10°", 95.792f, 100.000f, 107.686f),
        new([0, (byte)'F', (byte)'1', (byte)'1'], "F11/2°", 100.962f, 100.000f, 64.350f),
        new([], "F11/10°", 103.863f, 100.000f, 65.607f),
        new([0, 0, (byte)'S', (byte)'A'], "A/2°", 109.850f, 100.000f, 35.585f),
        new([], "A/10°", 111.144f, 100.000f, 35.200f),
        new([0, 0, (byte)'S', (byte)'C'], "C/2°", 98.074f, 100.000f, 118.232f),
        new([], "C/10°", 97.285f, 100.000f, 116.145f)
    ];

    private static readonly double[] ReciprocalTemperature =
    [
        1.17549435e-38,
        10.0e-6, 20.0e-6, 30.0e-6, 40.0e-6, 50.0e-6, 60.0e-6, 70.0e-6,
        80.0e-6, 90.0e-6, 100.0e-6, 125.0e-6, 150.0e-6, 175.0e-6,
        200.0e-6, 225.0e-6, 250.0e-6, 275.0e-6, 300.0e-6, 325.0e-6,
        350.0e-6, 375.0e-6, 400.0e-6, 425.0e-6, 450.0e-6, 475.0e-6,
        500.0e-6, 525.0e-6, 550.0e-6, 575.0e-6, 600.0e-6
    ];

    private static readonly Uvt[] UvtTable =
    [
        new(0.18006, 0.26352, -0.24341), new(0.18066, 0.26589, -0.25479),
        new(0.18133, 0.26846, -0.26876), new(0.18208, 0.27119, -0.28539),
        new(0.18293, 0.27407, -0.30470), new(0.18388, 0.27709, -0.32675),
        new(0.18494, 0.28021, -0.35156), new(0.18611, 0.28342, -0.37915),
        new(0.18740, 0.28668, -0.40955), new(0.18880, 0.28997, -0.44278),
        new(0.19032, 0.29326, -0.47888), new(0.19462, 0.30141, -0.58204),
        new(0.19962, 0.30921, -0.70471), new(0.20525, 0.31647, -0.84901),
        new(0.21142, 0.32312, -1.01820), new(0.21807, 0.32909, -1.21680),
        new(0.22511, 0.33439, -1.45120), new(0.23247, 0.33904, -1.72980),
        new(0.24010, 0.34308, -2.06370), new(0.24792, 0.34655, -2.46810),
        new(0.25591, 0.34951, -2.96410), new(0.26400, 0.35200, -3.58140),
        new(0.27218, 0.35407, -4.36330), new(0.28039, 0.35577, -5.37620),
        new(0.28863, 0.35714, -6.72620), new(0.29685, 0.35823, -8.59550),
        new(0.30505, 0.35907, -11.3240), new(0.31320, 0.35968, -15.6280),
        new(0.32129, 0.36011, -23.3250), new(0.32931, 0.36038, -40.7700),
        new(0.33724, 0.36051, -116.450)
    ];

    public static int xyz_to_corrected_color_temp(out float temp, ReadOnlySpan<float> xyz) {
        float us;
        float vs;
        float p;
        float di = 0.0f;
        float dm;
        int i;

        temp = 0.0f;
        if (xyz.Length < 3)
            throw new ArgumentException("XYZ requires three values.", nameof(xyz));
        if (xyz[0] < 1.0e-20f && xyz[1] < 1.0e-20f && xyz[2] < 1.0e-20f)
            return -1;
        us = (4.0f * xyz[0]) / (xyz[0] + 15.0f * xyz[1] + 3.0f * xyz[2]);
        vs = (6.0f * xyz[1]) / (xyz[0] + 15.0f * xyz[1] + 3.0f * xyz[2]);
        dm = 0.0f;
        for (i = 0; i < 31; i++) {
            di = (vs - (float)UvtTable[i].V) - (float)UvtTable[i].T * (us - (float)UvtTable[i].U);
            if (i > 0 && ((di < 0.0f && dm >= 0.0f) || (di >= 0.0f && dm < 0.0f)))
                break;
            dm = di;
        }
        if (i == 31)
            return -1;
        di /= MathF.Sqrt(1.0f + (float)(UvtTable[i].T * UvtTable[i].T));
        dm /= MathF.Sqrt(1.0f + (float)(UvtTable[i - 1].T * UvtTable[i - 1].T));
        p = dm / (dm - di);
        p = 1.0f / (float)(ReciprocalTemperature[i - 1] + (ReciprocalTemperature[i] - ReciprocalTemperature[i - 1]) * p);
        temp = p;
        return 0;
    }

    public static int colour_temp_to_xyz(Span<float> xyz, float temp) {
        float x;
        float y;
        if (xyz.Length < 3)
            throw new ArgumentException("XYZ requires three values.", nameof(xyz));
        if (temp < 1667.0f || temp > 25000.0f)
            return -1;
        if (temp < 4000.0f)
            x = -0.2661239e9f / (temp * temp * temp) - 0.2343580e6f / (temp * temp) + 0.8776956e3f / temp + 0.179910f;
        else
            x = -3.0258469e9f / (temp * temp * temp) + 2.1070379e6f / (temp * temp) + 0.2226347e3f / temp + 0.240390f;
        if (temp < 2222.0f)
            y = -1.1063814f * x * x * x - 1.34811020f * x * x + 2.18555832f * x - 0.20219683f;
        else if (temp < 4000.0f)
            y = -0.9549476f * x * x * x - 1.37418593f * x * x + 2.09137015f * x - 0.16748867f;
        else
            y = 3.0817580f * x * x * x - 5.87338670f * x * x + 3.75112997f * x - 0.37001483f;
        xyz[0] = x / y;
        xyz[1] = 1.0f;
        xyz[2] = (1.0f - x - y) / y;
        return 0;
    }

    public static void set_lab_illuminant(LabParameters lab, float new_xn, float new_yn, float new_zn) {
        if (new_yn > 10.0f) {
            lab.x_n = new_xn / 100.0f;
            lab.y_n = new_yn / 100.0f;
            lab.z_n = new_zn / 100.0f;
        } else {
            lab.x_n = new_xn;
            lab.y_n = new_yn;
            lab.z_n = new_zn;
        }
        lab.x_rn = 1.0f / lab.x_n;
        lab.y_rn = 1.0f / lab.y_n;
        lab.z_rn = 1.0f / lab.z_n;
    }

    public static void set_lab_gamut(LabParameters lab, int L_min, int L_max, int a_min, int a_max, int b_min, int b_max, int ab_are_signed) {
        lab.range_L = L_max - L_min;
        lab.range_a = a_max - a_min;
        lab.range_b = b_max - b_min;
        lab.offset_L = -256.0f * L_min / lab.range_L;
        lab.offset_a = -256.0f * a_min / lab.range_a;
        lab.offset_b = -256.0f * b_min / lab.range_b;
        lab.range_L /= 255.0f;
        lab.range_a /= 255.0f;
        lab.range_b /= 255.0f;
        lab.ab_are_signed = ab_are_signed;
    }

    public static void set_lab_gamut2(LabParameters lab, int L_P, int L_Q, int a_P, int a_Q, int b_P, int b_Q) {
        lab.range_L = L_Q / 255.0f;
        lab.range_a = a_Q / 255.0f;
        lab.range_b = b_Q / 255.0f;
        lab.offset_L = L_P;
        lab.offset_a = a_P;
        lab.offset_b = b_P;
        lab.ab_are_signed = 0;
    }

    public static void get_lab_gamut2(LabParameters lab, out int L_P, out int L_Q, out int a_P, out int a_Q, out int b_P, out int b_Q) {
        L_Q = (int)(lab.range_L * 255.0f);
        a_Q = (int)(lab.range_a * 255.0f);
        b_Q = (int)(lab.range_b * 255.0f);
        L_P = (int)lab.offset_L;
        a_P = (int)lab.offset_a;
        b_P = (int)lab.offset_b;
    }

    public static int set_illuminant_from_code(T85Log logging, LabParameters lab, ReadOnlySpan<byte> code) {
        if (code.Length < 4)
            throw new ArgumentException("Illuminant code requires four bytes.", nameof(code));
        if (code[0] == (byte)'C' && code[1] == (byte)'T') {
            int colour_temp = BinaryPrimitives.ReadUInt16BigEndian(code.Slice(2, 2));
            logging.Flow($"Illuminant colour temp {colour_temp}K");
            Span<float> xyz = stackalloc float[3];
            _ = colour_temp_to_xyz(xyz, colour_temp);
            set_lab_illuminant(lab, xyz[0], xyz[1], xyz[2]);
            return colour_temp;
        }
        foreach (Illuminant illuminant in Illuminants) {
            if (illuminant.Tag.Length == 4 && code.Slice(0, 4).SequenceEqual(illuminant.Tag)) {
                logging.Flow($"Illuminant {illuminant.Name}");
                set_lab_illuminant(lab, illuminant.Xn, illuminant.Yn, illuminant.Zn);
                return 0;
            }
        }
        logging.Flow($"Unrecognised illuminant 0x{code[0]:x} 0x{code[1]:x} 0x{code[2]:x} 0x{code[3]:x}");
        return -1;
    }

    public static void set_gamut_from_code(T85Log logging, LabParameters lab, ReadOnlySpan<byte> code) {
        if (code.Length < 12)
            throw new ArgumentException("Gamut code requires twelve bytes.", nameof(code));
        Span<int> val = stackalloc int[6];
        for (int i = 0; i < 6; i++)
            val[i] = BinaryPrimitives.ReadUInt16BigEndian(code.Slice(2 * i, 2));
        logging.Flow($"Gamut L=[{val[0]},{val[1]}], a*=[{val[2]},{val[3]}], b*=[{val[4]},{val[5]}]");
        set_lab_gamut2(lab, val[0], val[1], val[2], val[3], val[4], val[5]);
    }

    public static void srgb_to_lab(LabParameters s, Span<byte> lab, ReadOnlySpan<byte> srgb, int pixels) {
        for (int i = 0; i < 3 * pixels; i += 3) {
            float r = CielabLuts.srgb_to_linear[srgb[i]];
            float g = CielabLuts.srgb_to_linear[srgb[i + 1]];
            float b = CielabLuts.srgb_to_linear[srgb[i + 2]];
            float x = 0.4124f * r + 0.3576f * g + 0.1805f * b;
            float y = 0.2126f * r + 0.7152f * g + 0.0722f * b;
            float z = 0.0193f * r + 0.1192f * g + 0.9505f * b;
            x *= s.x_rn;
            y *= s.y_rn;
            z *= s.z_rn;
            float xx = x <= 0.008856f ? 7.787f * x + 0.1379f : MathF.Cbrt(x);
            float yy = y <= 0.008856f ? 7.787f * y + 0.1379f : MathF.Cbrt(y);
            float zz = z <= 0.008856f ? 7.787f * z + 0.1379f : MathF.Cbrt(z);
            float L = 116.0f * yy - 16.0f;
            float a = 500.0f * (xx - yy);
            float bb = 200.0f * (yy - zz);
            lab[i] = saturateu8(MathF.Floor(L / s.range_L + s.offset_L));
            lab[i + 1] = saturateu8(MathF.Floor(a / s.range_a + s.offset_a));
            lab[i + 2] = saturateu8(MathF.Floor(bb / s.range_b + s.offset_b));
            if (s.ab_are_signed != 0) {
                lab[i + 1] = unchecked((byte)(lab[i + 1] - 128));
                lab[i + 2] = unchecked((byte)(lab[i + 2] - 128));
            }
        }
    }

    public static void lab_to_srgb(LabParameters s, Span<byte> srgb, ReadOnlySpan<byte> lab, int pixels) {
        for (int i = 0; i < 3 * pixels; i += 3) {
            byte a8 = lab[i + 1];
            byte b8 = lab[i + 2];
            if (s.ab_are_signed != 0) {
                a8 = unchecked((byte)(a8 + 128));
                b8 = unchecked((byte)(b8 + 128));
            }
            float L = s.range_L * (lab[i] - s.offset_L);
            float a = s.range_a * (a8 - s.offset_a);
            float bb = s.range_b * (b8 - s.offset_b);
            float ll = (L + 16.0f) / 116.0f;
            float y = ll <= 0.2068f ? 0.1284f * (ll - 0.1379f) : ll * ll * ll;
            float x0 = ll + a / 500.0f;
            float x = x0 <= 0.2068f ? 0.1284f * (x0 - 0.1379f) : x0 * x0 * x0;
            float z0 = ll - bb / 200.0f;
            float z = z0 <= 0.2068f ? 0.1284f * (z0 - 0.1379f) : z0 * z0 * z0;
            x *= s.x_n;
            y *= s.y_n;
            z *= s.z_n;
            float r = 3.2406f * x - 1.5372f * y - 0.4986f * z;
            float g = -0.9689f * x + 1.8758f * y + 0.0415f * z;
            float b = 0.0557f * x - 0.2040f * y + 1.0570f * z;
            int val = (int)(r * 4096.0f);
            srgb[i] = CielabLuts.linear_to_srgb[val < 0 ? 0 : val < 4095 ? val : 4095];
            val = (int)(g * 4096.0f);
            srgb[i + 1] = CielabLuts.linear_to_srgb[val < 0 ? 0 : val < 4095 ? val : 4095];
            val = (int)(b * 4096.0f);
            srgb[i + 2] = CielabLuts.linear_to_srgb[val < 0 ? 0 : val < 4095 ? val : 4095];
        }
    }

    private static byte saturateu8(float value) {
        if (value < 0.0f)
            return 0;
        if (value > 255.0f)
            return 255;
        return (byte)value;
    }
}
