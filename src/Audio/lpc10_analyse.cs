/*
 * TKFaxEngine - managed C# port
 *
 * lpc10_analyse.cs
 *
 * Direct C# conversion of EngineFX lpc10_analyse.c and declarations from lpc10_encdecs.h.
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * This port preserves the GNU Lesser General Public License version 2.1.
 */

#nullable enable

namespace TKFaxEngine.Audio;

public static partial class lpc10
{
    private static readonly int[] tau =
    {
         20,  21,  22,  23,  24,  25,  26,  27,  28,  29,  30,  31,  32,  33,  34,
         35,  36,  37,  38,  39,  40,  42,  44,  46,  48,  50,  52,  54,  56,  58,
         60,  62,  64,  66,  68,  70,  72,  74,  76,  78,  80,  84,  88,  92,  96,
        100, 104, 108, 112, 116, 120, 124, 128, 132, 136, 140, 144, 148, 152, 156
    };

    private static readonly int[] buflim = { 181, 720, 25, 720 };

    private static float energyf(ReadOnlySpan<float> amp, int len)
    {
        int i;
        float rms;

        rms = 0.0f;
        for (i = 0; i < len; i++)
            rms += amp[i]*amp[i];
        rms = MathF.Sqrt(rms/len);
        return rms;
    }

    private static void remove_dc_bias(ReadOnlySpan<float> speech, int len, Span<float> sigout)
    {
        float bias;
        int i;

        bias = 0.0f;
        for (i = 0; i < len; i++)
            bias += speech[i];
        bias /= len;
        for (i = 0; i < len; i++)
            sigout[i] = speech[i] - bias;
    }
    private static void eval_amdf(float[] speech,
                                  int lpita,
                                  int[] tau,
                                  int ltau,
                                  int maxlag,
                                  float[] amdf,
                                  out int minptr,
                                  out int maxptr)
    {
        float sum;
        int i;
        int j;
        int n1;
        int n2;

        minptr = 0;
        maxptr = 0;
        for (i = 0; i < ltau; i++)
        {
            n1 = (maxlag - tau[i])/2 + 1;
            n2 = n1 + lpita - 1;
            sum = 0.0f;
            for (j = n1; j <= n2; j += 4)
                sum += MathF.Abs(speech[j - 1] - speech[j + tau[i] - 1]);
            amdf[i] = sum;
            if (amdf[i] < amdf[minptr])
                minptr = i;
            if (amdf[i] > amdf[maxptr])
                maxptr = i;
        }
    }



    private static void eval_highres_amdf(float[] speech,
                                           int lpita,
                                           int[] tau,
                                           int ltau,
                                           float[] amdf,
                                           out int minptr,
                                           out int maxptr,
                                           out int mintau)
    {
        float[] amdf2 = new float[6];
        int[] tau2 = new int[6];
        int minp2;
        int ltau2;
        int maxp2;
        int minamd;
        int i;
        int i2;
        int ptr;

        eval_amdf(speech, lpita, tau, ltau, tau[ltau - 1], amdf, out minptr, out maxptr);
        mintau = tau[minptr];
        minamd = (int) amdf[minptr];
        ltau2 = 0;
        ptr = minptr - 2;
        i2 = min(mintau + 4, tau[ltau - 1]);
        for (i = max(mintau - 3, 41); i < i2; i++)
        {
            while (tau[ptr] < i)
                ptr++;
            if (tau[ptr] != i)
                tau2[ltau2++] = i;
        }
        if (ltau2 > 0)
        {
            eval_amdf(speech, lpita, tau2, ltau2, tau[ltau - 1], amdf2, out minp2, out maxp2);
            if (amdf2[minp2] < minamd)
            {
                mintau = tau2[minp2];
                minamd = (int) amdf2[minp2];
            }
        }
        if (mintau >= 80)
        {
            i = mintau/2;
            if ((i & 1) == 0)
            {
                ltau2 = 2;
                tau2[0] = i - 1;
                tau2[1] = i + 1;
            }
            else
            {
                ltau2 = 1;
                tau2[0] = i;
            }
            eval_amdf(speech, lpita, tau2, ltau2, tau[ltau - 1], amdf2, out minp2, out maxp2);
            if (amdf2[minp2] < minamd)
            {
                mintau = tau2[minp2];
                minamd = (int) amdf2[minp2];
                minptr -= 20;
            }
        }
        amdf[minptr] = minamd;
        maxptr = max(minptr - 5, 0);
        i2 = min(minptr + 6, ltau);
        for (i = maxptr; i < i2; i++)
        {
            if (amdf[i] > amdf[maxptr])
                maxptr = i;
        }
    }
    private static void dynamic_pitch_tracking(lpc10_encode_state_t s,
                                               float[] amdf,
                                               int ltau,
                                               ref int minptr,
                                               int voice,
                                               out int pitch,
                                               out int midx)
    {
        int pbar;
        float sbar;
        int i;
        int j;
        float alpha;
        float minsc;
        float maxsc;

        if (voice == 1)
            s.alphax = s.alphax*0.75f + amdf[minptr - 1]*0.5f;
        else
            s.alphax *= 0.984375f;
        alpha = s.alphax/16;
        if (voice == 0 && s.alphax < 128.0f)
            alpha = 8.0f;
        s.p[s.ipoint, 0] = 1;
        pbar = 1;
        sbar = s.s[0];
        for (i = 0; i < ltau; i++)
        {
            sbar += alpha;
            if (sbar < s.s[i])
            {
                s.s[i] = sbar;
            }
            else
            {
                pbar = i + 1;
                sbar = s.s[i];
            }
            s.p[s.ipoint, i] = pbar;
        }
        sbar = s.s[pbar - 1];
        for (i = pbar - 2; i >= 0; i--)
        {
            sbar += alpha;
            if (sbar < s.s[i])
            {
                s.s[i] = sbar;
                s.p[s.ipoint, i] = pbar;
            }
            else
            {
                pbar = s.p[s.ipoint, i];
                i = pbar - 1;
                sbar = s.s[i];
            }
        }
        s.s[0] += amdf[0]/2;
        minsc = s.s[0];
        maxsc = minsc;
        midx = 1;
        for (i = 1; i < ltau; i++)
        {
            s.s[i] += amdf[i]/2;
            if (s.s[i] > maxsc)
                maxsc = s.s[i];
            if (s.s[i] < minsc)
            {
                midx = i + 1;
                minsc = s.s[i];
            }
        }
        for (i = 0; i < ltau; i++)
            s.s[i] -= minsc;
        maxsc -= minsc;
        j = 0;
        for (i = 20; i <= 40; i += 10)
        {
            if (midx > i)
            {
                if (s.s[midx - i - 1] < maxsc/4)
                    j = i;
            }
        }
        midx -= j;
        pitch = midx;
        for (i = 0, j = s.ipoint; i < 2; i++, j++)
            pitch = s.p[j & 1, pitch - 1];
        s.ipoint = (s.ipoint + 1) & 1;
    }
    private static void onset(lpc10_encode_state_t s,
                              float[] pebuf,
                              int[] osbuf,
                              ref int osptr,
                              int oslen,
                              int sbufl,
                              int sbufh,
                              int lframe)
    {
        int i;
        float r1;
        float l2sum2;

        if (s.hyst)
            s.lasti -= lframe;
        for (i = sbufh - lframe + 1; i <= sbufh; i++)
        {
            s.n = (pebuf[i - sbufl]*pebuf[i - 1 - sbufl] + s.n*63.0f)/64.0f;
            r1 = pebuf[i - 1 - sbufl];
            s.d__ = (r1*r1 + s.d__*63.0f)/64.0f;
            if (s.d__ != 0.0f)
            {
                if (MathF.Abs(s.n) > s.d__)
                    s.fpc = r_sign(1.0f, s.n);
                else
                    s.fpc = s.n/s.d__;
            }
            l2sum2 = s.l2buf[s.l2ptr1 - 1];
            s.l2sum1 = s.l2sum1 - s.l2buf[s.l2ptr2 - 1] + s.fpc;
            s.l2buf[s.l2ptr2 - 1] = s.l2sum1;
            s.l2buf[s.l2ptr1 - 1] = s.fpc;
            s.l2ptr1 = (s.l2ptr1 & 0xF) + 1;
            s.l2ptr2 = (s.l2ptr2 & 0xF) + 1;
            if (MathF.Abs(s.l2sum1 - l2sum2) > 1.7f)
            {
                if (!s.hyst)
                {
                    if (osptr <= oslen)
                    {
                        osbuf[osptr - 1] = i - 9;
                        osptr++;
                    }
                    s.hyst = true;
                }
                s.lasti = i;
            }
            else if (s.hyst && i - s.lasti >= 10)
            {
                s.hyst = false;
            }
        }
    }
    private static void mload(int order,
                              int awins,
                              int awinf,
                              float[] speech,
                              float[] phi,
                              float[] psi)
    {
        int start;
        int i;
        int r;

        start = awins + order;
        for (r = 1; r <= order; r++)
        {
            phi[r - 1] = 0.0f;
            for (i = start; i <= awinf; i++)
                phi[r - 1] += speech[i - 2]*speech[i - r - 1];
        }
        psi[order - 1] = 0.0f;
        for (i = start - 1; i < awinf; i++)
            psi[order - 1] += speech[i]*speech[i - order];
        for (r = 1; r < order; r++)
        {
            for (i = 1; i <= r; i++)
            {
                phi[i*order + r] = phi[(i - 1)*order + r - 1]
                                   - speech[awinf - (r + 1)]*speech[awinf - (i + 1)]
                                   + speech[start - (r + 2)]*speech[start - (i + 2)];
            }
        }
        for (i = 0; i < order - 1; i++)
        {
            psi[i] = phi[i + 1]
                     - speech[start - 2]*speech[start - i - 3]
                     + speech[awinf - 1]*speech[awinf - i - 2];
        }
    }
    private static float preemp(ReadOnlySpan<float> inbuf,
                                Span<float> pebuf,
                                int nsamp,
                                float coeff,
                                float z)
    {
        int i;
        float si;

        for (i = 0; i < nsamp; i++)
        {
            si = inbuf[i];
            pebuf[i] = si - coeff*z;
            z = si;
        }
        return z;
    }



    private static void invert(int order, float[] phi, float[] psi, float[] rc)
    {
        float r1;
        int i;
        int j;
        int k;
        float[,] v = new float[10, 10];

        for (j = 0; j < order; j++)
        {
            for (i = j; i < order; i++)
                v[j, i] = phi[i + j*order];
            for (k = 0; k < j; k++)
            {
                r1 = v[k, j]*v[k, k];
                for (i = j; i < order; i++)
                    v[j, i] -= v[k, i]*r1;
            }
            if (MathF.Abs(v[j, j]) < 1.0e-10f)
            {
                for (i = j; i < order; i++)
                    rc[i] = 0.0f;
                return;
            }
            rc[j] = psi[j];
            for (k = 0; k < j; k++)
                rc[j] -= rc[k]*v[k, j];
            v[j, j] = 1.0f/v[j, j];
            rc[j] *= v[j, j];
            r1 = min(rc[j], 0.999f);
            rc[j] = max(r1, -0.999f);
        }
    }

    private static int rcchk(int order, float[] rc1f, float[] rc2f)
    {
        int i;

        for (i = 0; i < order; i++)
        {
            if (MathF.Abs(rc2f[i]) > 0.99f)
            {
                for (i = 0; i < order; i++)
                    rc2f[i] = rc1f[i];
                break;
            }
        }
        return 0;
    }

    private static void lpfilt(ReadOnlySpan<float> inbuf,
                               Span<float> lpbuf,
                               int len,
                               int nsamp)
    {
        int j;

        for (j = len - nsamp; j < len; j++)
        {
            lpbuf[j] = (inbuf[j] + inbuf[j - 30])*-0.0097201988f
                       + (inbuf[j - 1] + inbuf[j - 29])*-0.0105179986f
                       + (inbuf[j - 2] + inbuf[j - 28])*-0.0083479648f
                       + (inbuf[j - 3] + inbuf[j - 27])*0.0005860774f
                       + (inbuf[j - 4] + inbuf[j - 26])*0.0130892089f
                       + (inbuf[j - 5] + inbuf[j - 25])*0.0217052232f
                       + (inbuf[j - 6] + inbuf[j - 24])*0.0184161253f
                       + (inbuf[j - 7] + inbuf[j - 23])*0.000339723f
                       + (inbuf[j - 8] + inbuf[j - 22])*-0.0260797087f
                       + (inbuf[j - 9] + inbuf[j - 21])*-0.0455563702f
                       + (inbuf[j - 10] + inbuf[j - 20])*-0.040306855f
                       + (inbuf[j - 11] + inbuf[j - 19])*0.0005029835f
                       + (inbuf[j - 12] + inbuf[j - 18])*0.0729262903f
                       + (inbuf[j - 13] + inbuf[j - 17])*0.1572008878f
                       + (inbuf[j - 14] + inbuf[j - 16])*0.2247288674f
                       + inbuf[j - 15]*0.250535965f;
        }
    }

    private static void ivfilt(ReadOnlySpan<float> lpbuf,
                               Span<float> ivbuf,
                               int len,
                               int nsamp,
                               float[] ivrc)
    {
        int i;
        int j;
        int k;
        float[] r = new float[3];
        float pc1;
        float pc2;

        for (i = 1; i <= 3; i++)
        {
            r[i - 1] = 0.0f;
            k = (i - 1) << 2;
            for (j = (i << 2) + len - nsamp; j <= len; j += 2)
                r[i - 1] += lpbuf[j - 1]*lpbuf[j - k - 1];
        }
        pc1 = 0.0f;
        pc2 = 0.0f;
        ivrc[0] = 0.0f;
        ivrc[1] = 0.0f;
        if (r[0] > 1.0e-10f)
        {
            ivrc[0] = r[1]/r[0];
            ivrc[1] = (r[2] - ivrc[0]*r[1])/(r[0] - ivrc[0]*r[1]);
            pc1 = ivrc[0] - ivrc[0]*ivrc[1];
            pc2 = ivrc[1];
        }
        for (i = len - nsamp; i < len; i++)
            ivbuf[i] = lpbuf[i] - pc1*lpbuf[i - 4] - pc2*lpbuf[i - 8];
    }

    /// <summary>Managed equivalent of <c>lpc10_analyse</c>.</summary>
    public static void lpc10_analyse(lpc10_encode_state_t s,
                                     float[] speech,
                                     int[] voice,
                                     out int pitch,
                                     out float rms,
                                     float[] rc)
    {
        const float precoef = 0.9375f;

        float[] amdf = new float[60];
        float[] abuf = new float[LPC10_MIN_PITCH];
        float[] ivrc = new float[2];
        float temp;
        float[] phi = new float[100];
        float[] psi = new float[10];
        int half;
        int midx;
        int[,] ewin = new int[3, 2];
        int i;
        int j;
        int lanal;
        int ipitch;
        int mintau;
        int minptr;
        int maxptr;

        for (i = 0; i <= 720 - LPC10_SAMPLES_PER_FRAME - 181; i++)
        {
            s.inbuf[i] = s.inbuf[LPC10_SAMPLES_PER_FRAME + i];
            s.pebuf[i] = s.pebuf[LPC10_SAMPLES_PER_FRAME + i];
        }
        for (i = 0; i <= 540 - LPC10_SAMPLES_PER_FRAME - 229; i++)
            s.ivbuf[i] = s.ivbuf[LPC10_SAMPLES_PER_FRAME + i];
        for (i = 0; i <= 720 - LPC10_SAMPLES_PER_FRAME - 25; i++)
            s.lpbuf[i] = s.lpbuf[LPC10_SAMPLES_PER_FRAME + i];
        for (i = 0, j = 0; i < s.osptr - 1; i++)
        {
            if (s.osbuf[i] > LPC10_SAMPLES_PER_FRAME)
                s.osbuf[j++] = s.osbuf[i] - LPC10_SAMPLES_PER_FRAME;
        }
        s.osptr = j + 1;
        s.voibuf[0, 0] = s.voibuf[1, 0];
        s.voibuf[0, 1] = s.voibuf[1, 1];
        for (i = 0; i < 2; i++)
        {
            s.vwin[i, 0] = s.vwin[i + 1, 0] - LPC10_SAMPLES_PER_FRAME;
            s.vwin[i, 1] = s.vwin[i + 1, 1] - LPC10_SAMPLES_PER_FRAME;
            s.awin[i, 0] = s.awin[i + 1, 0] - LPC10_SAMPLES_PER_FRAME;
            s.awin[i, 1] = s.awin[i + 1, 1] - LPC10_SAMPLES_PER_FRAME;
            s.obound[i] = s.obound[i + 1];
            s.voibuf[i + 1, 0] = s.voibuf[i + 2, 0];
            s.voibuf[i + 1, 1] = s.voibuf[i + 2, 1];
            s.rmsbuf[i] = s.rmsbuf[i + 1];
            for (j = 0; j < LPC10_ORDER; j++)
                s.rcbuf[i, j] = s.rcbuf[i + 1, j];
        }
        temp = 0.0f;
        for (i = 0; i < LPC10_SAMPLES_PER_FRAME; i++)
        {
            s.inbuf[720 - 2*LPC10_SAMPLES_PER_FRAME + i] = speech[i]*4096.0f - s.bias;
            temp += s.inbuf[720 - 2*LPC10_SAMPLES_PER_FRAME + i];
        }
        if (temp > LPC10_SAMPLES_PER_FRAME)
            s.bias++;
        else if (temp < -LPC10_SAMPLES_PER_FRAME)
            s.bias--;
        i = 721 - LPC10_SAMPLES_PER_FRAME;
        s.zpre = preemp(s.inbuf.AsSpan(i - 181), s.pebuf.AsSpan(i - 181), LPC10_SAMPLES_PER_FRAME, precoef, s.zpre);
        onset(s, s.pebuf, s.osbuf, ref s.osptr, 10, 181, 720, LPC10_SAMPLES_PER_FRAME);
        lpc10_placev(s.osbuf, ref s.osptr, 10, ref s.obound[2], s.vwin, LPC10_SAMPLES_PER_FRAME, 90, LPC10_MIN_PITCH, 307, 462);
        lpfilt(s.inbuf.AsSpan(228), s.lpbuf.AsSpan(384), 312, LPC10_SAMPLES_PER_FRAME);
        ivfilt(s.lpbuf.AsSpan(204), s.ivbuf, 312, LPC10_SAMPLES_PER_FRAME, ivrc);
        eval_highres_amdf(s.ivbuf, LPC10_MIN_PITCH, tau, 60, amdf, out minptr, out maxptr, out mintau);
        int[] vwin = { s.vwin[2, 0], s.vwin[2, 1] };
        for (half = 0; half < 2; half++)
        {
            lpc10_voicing(s,
                          vwin,
                          s.inbuf,
                          s.lpbuf,
                          buflim,
                          half,
                          ref amdf[minptr],
                          ref amdf[maxptr],
                          ref mintau,
                          ivrc,
                          s.obound);
        }
        minptr++;
        dynamic_pitch_tracking(s, amdf, 60, ref minptr, s.voibuf[3, 1], out pitch, out midx);
        ipitch = tau[midx - 1];
        lpc10_placea(ref ipitch, s.voibuf, ref s.obound[2], s.vwin, s.awin, ewin, LPC10_SAMPLES_PER_FRAME, LPC10_MIN_PITCH);
        lanal = s.awin[2, 1] + 1 - s.awin[2, 0];
        remove_dc_bias(s.pebuf.AsSpan(s.awin[2, 0] - 181), lanal, abuf);
        s.rmsbuf[2] = energyf(abuf.AsSpan(ewin[2, 0] - s.awin[2, 0]), ewin[2, 1] - ewin[2, 0] + 1);
        mload(LPC10_ORDER, 1, lanal, abuf, phi, psi);
        float[] rc2 = new float[LPC10_ORDER];
        invert(LPC10_ORDER, phi, psi, rc2);
        float[] rc1 = new float[LPC10_ORDER];
        for (i = 0; i < LPC10_ORDER; i++)
            rc1[i] = s.rcbuf[1, i];
        rcchk(LPC10_ORDER, rc1, rc2);
        for (i = 0; i < LPC10_ORDER; i++)
            s.rcbuf[2, i] = rc2[i];
        voice[0] = s.voibuf[1, 0];
        voice[1] = s.voibuf[1, 1];
        rms = s.rmsbuf[0];
        for (i = 0; i < LPC10_ORDER; i++)
            rc[i] = s.rcbuf[0, i];
    }

}
