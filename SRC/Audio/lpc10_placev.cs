/*
 * TKFaxEngine - managed C# port
 *
 * lpc10_placev.cs
 *
 * Direct C# conversion of EngineFX lpc10_placev.c and declarations from lpc10_encdecs.h.
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * This port preserves the GNU Lesser General Public License version 2.1.
 */

#nullable enable

namespace TKFaxEngine.Audio;

public static partial class lpc10
{
    public static void lpc10_placea(ref int ipitch,
                                    int[,] voibuf,
                                    ref int obound,
                                    int[,] vwin,
                                    int[,] awin,
                                    int[,] ewin,
                                    int lframe,
                                    int maxwin)
    {
        int allv;
        int winv;
        int i;
        int j;
        int k;
        int l;
        int hrange;
        bool ephase;
        int lrange;

        lrange = lframe + 1;
        hrange = 3*lframe;

        allv = voibuf[1, 1] == 1
               && voibuf[2, 0] == 1
               && voibuf[2, 1] == 1
               && voibuf[3, 0] == 1
               && voibuf[3, 1] == 1 ? 1 : 0;
        winv = voibuf[3, 0] == 1 || voibuf[3, 1] == 1 ? 1 : 0;
        if (allv != 0 || (winv != 0 && obound == 0))
        {
            i = (lrange + ipitch - 1 - awin[1, 0])/ipitch;
            i *= ipitch;
            i += awin[1, 0];
            l = maxwin;
            k = (vwin[2, 0] + vwin[2, 1] + 1 - l)/2;
            awin[2, 0] = i + (int) MathF.Floor((float) (k - i)/(float) ipitch + 0.5f)*ipitch;
            awin[2, 1] = awin[2, 0] + l - 1;
            if (obound >= 2 && awin[2, 1] > vwin[2, 1])
            {
                awin[2, 0] -= ipitch;
                awin[2, 1] -= ipitch;
            }
            if ((obound == 1 || obound == 3) && awin[2, 0] < vwin[2, 0])
            {
                awin[2, 0] += ipitch;
                awin[2, 1] += ipitch;
            }
            while (awin[2, 1] > hrange)
            {
                awin[2, 0] -= ipitch;
                awin[2, 1] -= ipitch;
            }
            while (awin[2, 0] < lrange)
            {
                awin[2, 0] += ipitch;
                awin[2, 1] += ipitch;
            }
            ephase = true;
        }
        else
        {
            awin[2, 0] = vwin[2, 0];
            awin[2, 1] = vwin[2, 1];
            ephase = false;
        }
        j = (awin[2, 1] - awin[2, 0] + 1)/ipitch*ipitch;
        if (j == 0 || winv == 0)
        {
            ewin[2, 0] = vwin[2, 0];
            ewin[2, 1] = vwin[2, 1];
        }
        else if (!ephase && obound == 2)
        {
            ewin[2, 0] = awin[2, 1] - j + 1;
            ewin[2, 1] = awin[2, 1];
        }
        else
        {
            ewin[2, 0] = awin[2, 0];
            ewin[2, 1] = awin[2, 0] + j - 1;
        }
    }

    public static void lpc10_placev(int[] osbuf,
                                    ref int osptr,
                                    int oslen,
                                    ref int obound,
                                    int[,] vwin,
                                    int lframe,
                                    int minwin,
                                    int maxwin,
                                    int dvwinl,
                                    int dvwinh)
    {
        int i1;
        int i2;
        bool crit;
        int q;
        int osptr1;
        int hrange;
        int lrange;
        int i;

        i1 = vwin[1, 1] + 1;
        i2 = lframe + 1;
        lrange = max(i1, i2);
        hrange = 3*lframe;
        for (osptr1 = osptr - 1; osptr1 >= 1; osptr1--)
        {
            if (osbuf[osptr1 - 1] <= hrange)
                break;
        }
        osptr1++;
        if (osptr1 <= 1 || osbuf[osptr1 - 2] < lrange)
        {
            i1 = vwin[1, 1] + 1;
            vwin[2, 0] = max(i1, dvwinl);
            vwin[2, 1] = vwin[2, 0] + maxwin - 1;
            obound = 0;
        }
        else
        {
            for (q = osptr1 - 1; q >= 1; q--)
            {
                if (osbuf[q - 1] < lrange)
                    break;
            }
            q++;
            crit = false;
            for (i = q + 1; i < osptr1; i++)
            {
                if (osbuf[i - 1] - osbuf[q - 1] >= minwin)
                {
                    crit = true;
                    break;
                }
            }
            i1 = 2*lframe;
            i2 = lrange + minwin - 1;
            if (!crit && osbuf[q - 1] > max(i1, i2))
            {
                vwin[2, 1] = osbuf[q - 1] - 1;
                i2 = vwin[2, 1] - maxwin + 1;
                vwin[2, 0] = max(lrange, i2);
                obound = 2;
            }
            else
            {
                vwin[2, 0] = osbuf[q - 1];
                do
                {
                    if (++q >= osptr1 || osbuf[q - 1] > vwin[2, 0] + maxwin)
                    {
                        i1 = vwin[2, 0] + maxwin - 1;
                        vwin[2, 1] = min(i1, hrange);
                        obound = 1;
                        return;
                    }
                }
                while (osbuf[q - 1] < vwin[2, 0] + minwin);
                vwin[2, 1] = osbuf[q - 1] - 1;
                obound = 3;
            }
        }
    }
}
