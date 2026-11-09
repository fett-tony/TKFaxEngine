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

/// <summary>
/// Represents the <c>lpc10_frame_t</c> type.
/// </summary>
public sealed class lpc10_frame_t
{
    /// <summary>
    /// Stores the <c>ipitch</c> value.
    /// </summary>
    public int ipitch;
    /// <summary>
    /// Stores the <c>irms</c> value.
    /// </summary>
    public int irms;
    /// <summary>
    /// Stores the <c>irc</c> value.
    /// </summary>
    public readonly int[] irc = new int[10];
}

/// <summary>
/// Represents the <c>lpc10_encode_state_t</c> type.
/// </summary>
public sealed class lpc10_encode_state_t
{
    /// <summary>
    /// Stores the <c>error_correction</c> value.
    /// </summary>
    public int error_correction;

    /// <summary>
    /// Stores the <c>z11</c> value.
    /// </summary>
    public float z11;
    /// <summary>
    /// Stores the <c>z21</c> value.
    /// </summary>
    public float z21;
    /// <summary>
    /// Stores the <c>z12</c> value.
    /// </summary>
    public float z12;
    /// <summary>
    /// Stores the <c>z22</c> value.
    /// </summary>
    public float z22;

    /// <summary>
    /// Stores the <c>inbuf</c> value.
    /// </summary>
    public readonly float[] inbuf = new float[lpc10.LPC10_SAMPLES_PER_FRAME*3];
    /// <summary>
    /// Stores the <c>pebuf</c> value.
    /// </summary>
    public readonly float[] pebuf = new float[lpc10.LPC10_SAMPLES_PER_FRAME*3];
    /// <summary>
    /// Stores the <c>lpbuf</c> value.
    /// </summary>
    public readonly float[] lpbuf = new float[696];
    /// <summary>
    /// Stores the <c>ivbuf</c> value.
    /// </summary>
    public readonly float[] ivbuf = new float[312];
    /// <summary>
    /// Stores the <c>bias</c> value.
    /// </summary>
    public float bias;
    /// <summary>
    /// Stores the <c>osbuf</c> value.
    /// </summary>
    public readonly int[] osbuf = new int[10];
    /// <summary>
    /// Stores the <c>osptr</c> value.
    /// </summary>
    public int osptr;
    /// <summary>
    /// Stores the <c>obound</c> value.
    /// </summary>
    public readonly int[] obound = new int[3];
    /// <summary>
    /// Stores the <c>vwin</c> value.
    /// </summary>
    public readonly int[,] vwin = new int[3, 2];
    /// <summary>
    /// Stores the <c>awin</c> value.
    /// </summary>
    public readonly int[,] awin = new int[3, 2];
    /// <summary>
    /// Stores the <c>voibuf</c> value.
    /// </summary>
    public readonly int[,] voibuf = new int[4, 2];
    /// <summary>
    /// Stores the <c>rmsbuf</c> value.
    /// </summary>
    public readonly float[] rmsbuf = new float[3];
    /// <summary>
    /// Stores the <c>rcbuf</c> value.
    /// </summary>
    public readonly float[,] rcbuf = new float[3, 10];
    /// <summary>
    /// Stores the <c>zpre</c> value.
    /// </summary>
    public float zpre;

    /// <summary>
    /// Stores the <c>n</c> value.
    /// </summary>
    public float n;
    /// <summary>
    /// Stores the <c>d__</c> value.
    /// </summary>
    public float d__;
    /// <summary>
    /// Stores the <c>fpc</c> value.
    /// </summary>
    public float fpc;
    /// <summary>
    /// Stores the <c>l2buf</c> value.
    /// </summary>
    public readonly float[] l2buf = new float[16];
    /// <summary>
    /// Stores the <c>l2sum1</c> value.
    /// </summary>
    public float l2sum1;
    /// <summary>
    /// Stores the <c>l2ptr1</c> value.
    /// </summary>
    public int l2ptr1;
    /// <summary>
    /// Stores the <c>l2ptr2</c> value.
    /// </summary>
    public int l2ptr2;
    /// <summary>
    /// Stores the <c>lasti</c> value.
    /// </summary>
    public int lasti;
    /// <summary>
    /// Stores the <c>hyst</c> value.
    /// </summary>
    public bool hyst;

    /// <summary>
    /// Stores the <c>dither</c> value.
    /// </summary>
    public float dither;
    /// <summary>
    /// Stores the <c>snr</c> value.
    /// </summary>
    public float snr;
    /// <summary>
    /// Stores the <c>maxmin</c> value.
    /// </summary>
    public float maxmin;
    /// <summary>
    /// Stores the <c>voice</c> value.
    /// </summary>
    public readonly float[,] voice = new float[3, 2];
    /// <summary>
    /// Stores the <c>lbve</c> value.
    /// </summary>
    public int lbve;
    /// <summary>
    /// Stores the <c>lbue</c> value.
    /// </summary>
    public int lbue;
    /// <summary>
    /// Stores the <c>fbve</c> value.
    /// </summary>
    public int fbve;
    /// <summary>
    /// Stores the <c>fbue</c> value.
    /// </summary>
    public int fbue;
    /// <summary>
    /// Stores the <c>ofbue</c> value.
    /// </summary>
    public int ofbue;
    /// <summary>
    /// Stores the <c>sfbue</c> value.
    /// </summary>
    public int sfbue;
    /// <summary>
    /// Stores the <c>olbue</c> value.
    /// </summary>
    public int olbue;
    /// <summary>
    /// Stores the <c>slbue</c> value.
    /// </summary>
    public int slbue;

    /// <summary>
    /// Stores the <c>s</c> value.
    /// </summary>
    public readonly float[] s = new float[60];
    /// <summary>
    /// Stores the <c>p</c> value.
    /// </summary>
    public readonly int[,] p = new int[2, 60];
    /// <summary>
    /// Stores the <c>ipoint</c> value.
    /// </summary>
    public int ipoint;
    /// <summary>
    /// Stores the <c>alphax</c> value.
    /// </summary>
    public float alphax;

    /// <summary>
    /// Stores the <c>isync</c> value.
    /// </summary>
    public int isync;
}

/// <summary>
/// Represents the <c>lpc10_decode_state_t</c> type.
/// </summary>
public sealed class lpc10_decode_state_t
{
    /// <summary>
    /// Stores the <c>error_correction</c> value.
    /// </summary>
    public int error_correction;

    /// <summary>
    /// Stores the <c>iptold</c> value.
    /// </summary>
    public int iptold;
    /// <summary>
    /// Stores the <c>first</c> value.
    /// </summary>
    public bool first;
    /// <summary>
    /// Stores the <c>ivp2h</c> value.
    /// </summary>
    public int ivp2h;
    /// <summary>
    /// Stores the <c>iovoic</c> value.
    /// </summary>
    public int iovoic;
    /// <summary>
    /// Stores the <c>iavgp</c> value.
    /// </summary>
    public int iavgp;
    /// <summary>
    /// Stores the <c>erate</c> value.
    /// </summary>
    public int erate;
    /// <summary>
    /// Stores the <c>drc</c> value.
    /// </summary>
    public readonly int[,] drc = new int[10, 3];
    /// <summary>
    /// Stores the <c>dpit</c> value.
    /// </summary>
    public readonly int[] dpit = new int[3];
    /// <summary>
    /// Stores the <c>drms</c> value.
    /// </summary>
    public readonly int[] drms = new int[3];

    /// <summary>
    /// Stores the <c>buf</c> value.
    /// </summary>
    public readonly float[] buf = new float[lpc10.LPC10_SAMPLES_PER_FRAME*2];
    /// <summary>
    /// Stores the <c>buflen</c> value.
    /// </summary>
    public int buflen;

    /// <summary>
    /// Stores the <c>ivoico</c> value.
    /// </summary>
    public int ivoico;
    /// <summary>
    /// Stores the <c>ipito</c> value.
    /// </summary>
    public int ipito;
    /// <summary>
    /// Stores the <c>rmso</c> value.
    /// </summary>
    public float rmso;
    /// <summary>
    /// Stores the <c>rco</c> value.
    /// </summary>
    public readonly float[] rco = new float[10];
    /// <summary>
    /// Stores the <c>jsamp</c> value.
    /// </summary>
    public int jsamp;
    /// <summary>
    /// Stores the <c>first_pitsyn</c> value.
    /// </summary>
    public bool first_pitsyn;

    /// <summary>
    /// Stores the <c>ipo</c> value.
    /// </summary>
    public int ipo;
    /// <summary>
    /// Stores the <c>exc</c> value.
    /// </summary>
    public readonly float[] exc = new float[166];
    /// <summary>
    /// Stores the <c>exc2</c> value.
    /// </summary>
    public readonly float[] exc2 = new float[166];
    /// <summary>
    /// Stores the <c>lpi</c> value.
    /// </summary>
    public readonly float[] lpi = new float[3];
    /// <summary>
    /// Stores the <c>hpi</c> value.
    /// </summary>
    public readonly float[] hpi = new float[3];
    /// <summary>
    /// Stores the <c>rmso_bsynz</c> value.
    /// </summary>
    public float rmso_bsynz;

    /// <summary>
    /// Stores the <c>j</c> value.
    /// </summary>
    public int j;
    /// <summary>
    /// Stores the <c>k</c> value.
    /// </summary>
    public int k;
    /// <summary>
    /// Stores the <c>y</c> value.
    /// </summary>
    public readonly short[] y = new short[5];

    /// <summary>
    /// Stores the <c>dei</c> value.
    /// </summary>
    public readonly float[] dei = new float[2];
    /// <summary>
    /// Stores the <c>deo</c> value.
    /// </summary>
    public readonly float[] deo = new float[3];
}

/// <summary>
/// Represents the <c>lpc10</c> type.
/// </summary>
public static partial class lpc10
{
    /// <summary>
    /// Defines the <c>LPC10_SAMPLES_PER_FRAME</c> constant.
    /// </summary>
    public const int LPC10_SAMPLES_PER_FRAME = 180;
    /// <summary>
    /// Defines the <c>LPC10_BITS_IN_COMPRESSED_FRAME</c> constant.
    /// </summary>
    public const int LPC10_BITS_IN_COMPRESSED_FRAME = 54;
    /// <summary>
    /// Defines the <c>LPC10_ORDER</c> constant.
    /// </summary>
    public const int LPC10_ORDER = 10;
    /// <summary>
    /// Defines the <c>LPC10_MAX_PITCH</c> constant.
    /// </summary>
    public const int LPC10_MAX_PITCH = 20;
    /// <summary>
    /// Defines the <c>LPC10_MIN_PITCH</c> constant.
    /// </summary>
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
