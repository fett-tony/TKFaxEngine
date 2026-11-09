/*
 * TKFaxEngine - managed C# port
 *
 * lpc10.cs
 *
 * Direct C# conversion of EngineFX lpc10.h and lpc10_encdecs.h.
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * This port preserves the GNU Lesser General Public License version 2.1.
 */

#nullable enable

namespace TKFaxEngine.Audio;

public sealed class lpc10_frame_t
{
    public int ipitch;
    public int irms;
    public readonly int[] irc = new int[10];
}

public sealed class lpc10_encode_state_t
{
    public int error_correction;

    public float z11;
    public float z21;
    public float z12;
    public float z22;

    public readonly float[] inbuf = new float[lpc10.LPC10_SAMPLES_PER_FRAME*3];
    public readonly float[] pebuf = new float[lpc10.LPC10_SAMPLES_PER_FRAME*3];
    public readonly float[] lpbuf = new float[696];
    public readonly float[] ivbuf = new float[312];
    public float bias;
    public readonly int[] osbuf = new int[10];
    public int osptr;
    public readonly int[] obound = new int[3];
    public readonly int[,] vwin = new int[3, 2];
    public readonly int[,] awin = new int[3, 2];
    public readonly int[,] voibuf = new int[4, 2];
    public readonly float[] rmsbuf = new float[3];
    public readonly float[,] rcbuf = new float[3, 10];
    public float zpre;

    public float n;
    public float d__;
    public float fpc;
    public readonly float[] l2buf = new float[16];
    public float l2sum1;
    public int l2ptr1;
    public int l2ptr2;
    public int lasti;
    public bool hyst;

    public float dither;
    public float snr;
    public float maxmin;
    public readonly float[,] voice = new float[3, 2];
    public int lbve;
    public int lbue;
    public int fbve;
    public int fbue;
    public int ofbue;
    public int sfbue;
    public int olbue;
    public int slbue;

    public readonly float[] s = new float[60];
    public readonly int[,] p = new int[2, 60];
    public int ipoint;
    public float alphax;

    public int isync;
}

public sealed class lpc10_decode_state_t
{
    public int error_correction;

    public int iptold;
    public bool first;
    public int ivp2h;
    public int iovoic;
    public int iavgp;
    public int erate;
    public readonly int[,] drc = new int[10, 3];
    public readonly int[] dpit = new int[3];
    public readonly int[] drms = new int[3];

    public readonly float[] buf = new float[lpc10.LPC10_SAMPLES_PER_FRAME*2];
    public int buflen;

    public int ivoico;
    public int ipito;
    public float rmso;
    public readonly float[] rco = new float[10];
    public int jsamp;
    public bool first_pitsyn;

    public int ipo;
    public readonly float[] exc = new float[166];
    public readonly float[] exc2 = new float[166];
    public readonly float[] lpi = new float[3];
    public readonly float[] hpi = new float[3];
    public float rmso_bsynz;

    public int j;
    public int k;
    public readonly short[] y = new short[5];

    public readonly float[] dei = new float[2];
    public readonly float[] deo = new float[3];
}

public static partial class lpc10
{
    public const int LPC10_SAMPLES_PER_FRAME = 180;
    public const int LPC10_BITS_IN_COMPRESSED_FRAME = 54;
    public const int LPC10_ORDER = 10;
    public const int LPC10_MAX_PITCH = 20;
    public const int LPC10_MIN_PITCH = 156;

    private static int min(int a, int b) => a <= b ? a : b;
    private static float min(float a, float b) => a <= b ? a : b;
    private static int max(int a, int b) => a >= b ? a : b;
    private static float max(float a, float b) => a >= b ? a : b;

    private static int pow_ii(int x, int n)
    {
        int pow;
        uint u;

        if (n <= 0)
        {
            if (n == 0  ||  x == 1)
                return 1;
            if (x != -1)
                return x != 0 ? 1/x : 0;
            n = -n;
        }
        u = unchecked((uint) n);
        for (pow = 1;  ;  )
        {
            if ((u & 1U) != 0)
                pow = unchecked(pow*x);
            if ((u >>= 1) == 0)
                break;
            x = unchecked(x*x);
        }
        return pow;
    }

    private static float r_sign(float a, float b)
    {
        float x;

        x = MathF.Abs(a);
        return b >= 0.0f ? x : -x;
    }
}
