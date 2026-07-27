/*
 * TKFaxEngine - managed C# port
 *
 * lpc10_decode.cs
 *
 * Direct C# conversion of EngineFX lpc10_decode.c and public declarations from lpc10.h.
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * This port preserves the GNU Lesser General Public License version 2.1.
 */

#nullable enable

namespace TKFaxEngine.Audio;

public static partial class lpc10
{
    private static readonly byte[] dactab =
    {
        16,  0,  0,  3,  0,  5, 14,  7,  0,  9, 14, 11, 14, 13, 30, 14,
         0,  9,  2,  7,  4,  7,  7, 23,  9, 25, 10,  9, 12,  9, 14,  7,
         0,  5,  2, 11,  5, 21,  6,  5,  8, 11, 11, 27, 12,  5, 14, 11,
         2,  1, 18,  2, 12,  5,  2,  7, 12,  9,  2, 11, 28, 12, 12, 15,
         0,  3,  3, 19,  4, 13,  6,  3,  8, 13, 10,  3, 13, 29, 14, 13,
         4,  1, 10,  3, 20,  4,  4,  7, 10,  9, 26, 10,  4, 13, 10, 15,
         8,  1,  6,  3,  6,  5, 22,  6, 24,  8,  8, 11,  8, 13,  6, 15,
         1, 17,  2,  1,  4,  1,  6, 15,  8,  1, 10, 15, 12, 15, 15, 31
    };

    private static readonly int[] ivtab =
    {
        24960, 24960, 24960, 24960, 25480, 25480, 25483, 25480,
        16640,  1560,  1560,  1560, 16640,  1816,  1563,  1560,
        24960, 24960, 24859, 24856, 26001, 25881, 25915, 25913,
         1560,  1560,  7800,  3640,  1561,  1561,  3643,  3641
    };

    private static readonly float[] corth =
    {
        32767.0f, 10.0f, 5.0f, 0.0f, 32767.0f,  8.0f, 4.0f, 0.0f,
           32.0f,  6.4f, 3.2f, 0.0f,    32.0f,  6.4f, 3.2f, 0.0f,
           32.0f, 11.2f, 6.4f, 0.0f,    32.0f, 11.2f, 6.4f, 0.0f,
           16.0f,  5.6f, 3.2f, 0.0f,    16.0f,  5.6f, 3.2f, 0.0f
    };

    private static readonly int[] detau =
    {
          0,   0,   0,   3,   0,   3,   3,  31,
          0,   3,   3,  21,   3,   3,  29,  30,
          0,   3,   3,  20,   3,  25,  27,  26,
          3,  23,  58,  22,   3,  24,  28,   3,
          0,   3,   3,   3,   3,  39,  33,  32,
          3,  37,  35,  36,   3,  38,  34,   3,
          3,  42,  46,  44,  50,  40,  48,   3,
         54,   3,  56,   3,  52,   3,   3,   1,
          0,   3,   3, 108,   3,  78, 100, 104,
          3,  84,  92,  88, 156,  80,  96,   3,
          3,  74,  70,  72,  66,  76,  68,   3,
         62,   3,  60,   3,  64,   3,   3,   1,
          3, 116, 132, 112, 148, 152,   3,   3,
        140,   3, 136,   3, 144,   3,   3,   1,
        124, 120, 128,   3,   3,   3,   3,   1,
          3,   3,   3,   1,   3,   1,   1,   1
    };

    private static readonly int[] rmst =
    {
        1024,  936,  856,  784,  718,  656,  600,  550,
         502,  460,  420,  384,  352,  328,  294,  270,
         246,  226,  206,  188,  172,  158,  144,  132,
         120,  110,  102,   92,   84,   78,   70,   64,
          60,   54,   50,   46,   42,   38,   34,   32,
          30,   26,   24,   22,   20,   18,   17,   16,
          15,   14,   13,   12,   11,   10,    9,    8,
           7,    6,    5,    4,    3,    2,    1,    0
    };

    private static readonly int[] detab7 =
    {
          4,  11,  18,  25,  32,  39,  46,  53,
         60,  66,  72,  77,  82,  87,  92,  96,
        101, 104, 108, 111, 114, 115, 117, 119,
        121, 122, 123, 124, 125, 126, 127, 127
    };

    private static readonly float[] descl =
        { 0.6953f, 0.625f, 0.5781f, 0.5469f, 0.5312f, 0.5391f, 0.4688f, 0.3828f };

    private static readonly int[] deadd =
        { 1152, -2816, -1536, -3584, -1280, -2432, 768, -1920 };

    private static readonly int[] qb =
        { 511, 511, 1023, 1023, 1023, 1023, 2047, 4095 };

    private static readonly int[] nbit =
        { 8, 8, 5, 5, 4, 4, 4, 4, 3, 2 };

    private static readonly int[] zrc =
        { 0, 0, 0, 0, 0, 3, 0, 2, 0, 0 };

    private static readonly int[] kexc =
    {
          8, -16, 26, -48, 86, -162, 294, -502, 718, -728, 184,
        672, -610, -672, 184, 728, 718, 502, 294, 162, 86, 48,
         26, 16, 8
    };

    private static int lpc10_random(lpc10_decode_state_t s)
    {
        int ret_val;

        s.y[s.k] = unchecked((short) (s.y[s.k] + s.y[s.j]));
        ret_val = s.y[s.k];
        if (--s.k < 0)
            s.k = 4;
        if (--s.j < 0)
            s.j = 4;
        return ret_val;
    }

    private static void bsynz(lpc10_decode_state_t s,
                              float[] coef,
                              int ip,
                              ref int iv,
                              Span<float> sout,
                              float rms,
                              float ratio,
                              float g2pass)
    {
        int i;
        int j;
        int k;
        int px;
        float[] noise = new float[LPC10_MIN_PITCH];
        float pulse;
        float r1;
        float gain;
        float xssq;
        float sscale;
        float xy;
        float sum;
        float ssq;
        float lpi0;
        float hpi0;

        r1 = s.rmso_bsynz/(rms + 1.0e-6f);
        xy = min(r1, 8.0f);
        s.rmso_bsynz = rms;
        for (i = 0; i < LPC10_ORDER; i++)
            s.exc2[i] = s.exc2[s.ipo + i]*xy;
        s.ipo = ip;
        if (iv == 0)
        {
            for (i = 0; i < ip; i++)
                s.exc[LPC10_ORDER + i] = (float) (lpc10_random(s)/64);
            px = (lpc10_random(s) + 32768)*(ip - 1)/65536 + LPC10_ORDER + 1;
            r1 = ratio/4.0f;
            pulse = r1*342;
            if (pulse > 2.0e3f)
                pulse = 2.0e3f;
            s.exc[px - 1] += pulse;
            s.exc[px] -= pulse;
        }
        else
        {
            sscale = MathF.Sqrt(ip)/6.928f;
            for (i = 0; i < ip; i++)
            {
                s.exc[LPC10_ORDER + i] = 0.0f;
                if (i < 25)
                    s.exc[LPC10_ORDER + i] = sscale*kexc[i];
                lpi0 = s.exc[LPC10_ORDER + i];
                s.exc[LPC10_ORDER + i] = s.exc[LPC10_ORDER + i]*0.125f + s.lpi[0]*0.75f + s.lpi[1]*0.125f;
                s.lpi[1] = s.lpi[0];
                s.lpi[0] = lpi0;
            }
            for (i = 0; i < ip; i++)
            {
                hpi0 = lpc10_random(s)/64.0f;
                noise[i] = hpi0*-0.125f + s.hpi[0]*0.25f + s.hpi[1]*-0.125f;
                s.hpi[1] = s.hpi[0];
                s.hpi[0] = hpi0;
            }
            for (i = 0; i < ip; i++)
                s.exc[LPC10_ORDER + i] += noise[i];
        }
        xssq = 0.0f;
        for (i = 0; i < ip; i++)
        {
            k = LPC10_ORDER + i;
            sum = 0.0f;
            for (j = 0; j < LPC10_ORDER; j++)
                sum += coef[j]*s.exc[k - j - 1];
            sum *= g2pass;
            s.exc2[k] = sum + s.exc[k];
        }
        for (i = 0; i < ip; i++)
        {
            k = LPC10_ORDER + i;
            sum = 0.0f;
            for (j = 0; j < LPC10_ORDER; j++)
                sum += coef[j]*s.exc2[k - j - 1];
            s.exc2[k] = sum + s.exc2[k];
            xssq += s.exc2[k]*s.exc2[k];
        }
        for (i = 0; i < LPC10_ORDER; i++)
        {
            s.exc[i] = s.exc[ip + i];
            s.exc2[i] = s.exc2[ip + i];
        }
        ssq = rms*rms*ip;
        gain = MathF.Sqrt(ssq/xssq);
        for (i = 0; i < ip; i++)
            sout[i] = gain*s.exc2[LPC10_ORDER + i];
    }

    private static int pitsyn(lpc10_decode_state_t s,
                              int[] voice,
                              ref int pitch,
                              float rms,
                              float[] rc,
                              int[] ivuv,
                              int[] ipiti,
                              float[] rmsi,
                              float[] rci,
                              out int nout,
                              out float ratio)
    {
        int i;
        int j;
        int vflag;
        int jused;
        int lsamp;
        int ip;
        int nl;
        int ivoice;
        int istart;
        float r1;
        float alrn;
        float alro;
        float[] yarc = new float[10];
        float prop;
        float slope;
        float uvpit;
        float xxy;
        float msix;

        if (rms < 1.0f)
            rms = 1.0f;
        if (s.rmso < 1.0f)
            s.rmso = 1.0f;
        uvpit = 0.0f;
        ratio = rms/(s.rmso + 8.0f);
        if (s.first_pitsyn)
        {
            ivoice = voice[1];
            if (ivoice == 0)
                pitch = LPC10_SAMPLES_PER_FRAME/4;
            nout = LPC10_SAMPLES_PER_FRAME/pitch;
            s.jsamp = LPC10_SAMPLES_PER_FRAME - nout*pitch;
            for (i = 0; i < nout; i++)
            {
                for (j = 0; j < LPC10_ORDER; j++)
                    rci[j + i*LPC10_ORDER] = rc[j];
                ivuv[i] = ivoice;
                ipiti[i] = pitch;
                rmsi[i] = rms;
            }
            s.first_pitsyn = false;
        }
        else
        {
            vflag = 0;
            lsamp = LPC10_SAMPLES_PER_FRAME + s.jsamp;
            nout = 0;
            jused = 0;
            istart = 1;
            if (voice[0] == s.ivoico && voice[1] == voice[0])
            {
                if (voice[1] == 0)
                {
                    pitch = LPC10_SAMPLES_PER_FRAME/4;
                    s.ipito = pitch;
                    if (ratio > 8.0f)
                        s.rmso = rms;
                }
                slope = (pitch - s.ipito)/(float) lsamp;
                ivoice = voice[1];
            }
            else
            {
                if (s.ivoico != 1)
                {
                    if (s.ivoico == voice[0])
                        nl = lsamp - LPC10_SAMPLES_PER_FRAME/4;
                    else
                        nl = lsamp - LPC10_SAMPLES_PER_FRAME*3/4;
                    ipiti[0] = nl/2;
                    ipiti[1] = nl - ipiti[0];
                    ivuv[0] = 0;
                    ivuv[1] = 0;
                    rmsi[0] = s.rmso;
                    rmsi[1] = s.rmso;
                    for (i = 0; i < LPC10_ORDER; i++)
                    {
                        rci[i] = s.rco[i];
                        rci[i + LPC10_ORDER] = s.rco[i];
                        s.rco[i] = rc[i];
                    }
                    nout = 2;
                    s.ipito = pitch;
                    jused = nl;
                    istart = nl + 1;
                    ivoice = 1;
                }
                else
                {
                    if (s.ivoico != voice[0])
                        lsamp = LPC10_SAMPLES_PER_FRAME/4 + s.jsamp;
                    else
                        lsamp = LPC10_SAMPLES_PER_FRAME*3/4 + s.jsamp;
                    for (i = 0; i < LPC10_ORDER; i++)
                    {
                        yarc[i] = rc[i];
                        rc[i] = s.rco[i];
                    }
                    ivoice = 1;
                    vflag = 1;
                }
                slope = 0.0f;
            }
            for (;;)
            {
                for (i = istart; i <= lsamp; i++)
                {
                    r1 = s.ipito + slope*i;
                    ip = (int) (r1 + 0.5f);
                    if (uvpit != 0.0f)
                        ip = (int) uvpit;
                    if (ip <= i - jused)
                    {
                        ipiti[nout] = ip;
                        pitch = ip;
                        ivuv[nout] = ivoice;
                        jused += ip;
                        prop = (jused - ip/2)/(float) lsamp;
                        for (j = 0; j < LPC10_ORDER; j++)
                        {
                            alro = MathF.Log((s.rco[j] + 1)/(1 - s.rco[j]));
                            alrn = MathF.Log((rc[j] + 1)/(1 - rc[j]));
                            xxy = alro + prop*(alrn - alro);
                            xxy = MathF.Exp(xxy);
                            rci[j + nout*LPC10_ORDER] = (xxy - 1.0f)/(xxy + 1.0f);
                        }
                        msix = MathF.Log(rms) - MathF.Log(s.rmso);
                        msix = prop*msix;
                        msix = MathF.Log(s.rmso) + msix;
                        rmsi[nout] = MathF.Exp(msix);
                        nout++;
                    }
                }
                if (vflag != 1)
                    break;
                vflag = 0;
                istart = jused + 1;
                lsamp = LPC10_SAMPLES_PER_FRAME + s.jsamp;
                slope = 0.0f;
                ivoice = 0;
                uvpit = (lsamp - istart)/2;
                if (uvpit > 90.0f)
                    uvpit /= 2;
                s.rmso = rms;
                for (i = 0; i < LPC10_ORDER; i++)
                {
                    rc[i] = yarc[i];
                    s.rco[i] = yarc[i];
                }
            }
            s.jsamp = lsamp - jused;
        }
        if (nout != 0)
        {
            s.ivoico = voice[1];
            s.ipito = pitch;
            s.rmso = rms;
            for (i = 0; i < LPC10_ORDER; i++)
                s.rco[i] = rc[i];
        }
        return 0;
    }

    private static void deemp(lpc10_decode_state_t s, Span<float> x, int len)
    {
        int i;
        float r1;
        float dei0;

        for (i = 0; i < len; i++)
        {
            dei0 = x[i];
            r1 = x[i] - s.dei[0]*1.9998f + s.dei[1];
            x[i] = r1 + s.deo[0]*2.5f - s.deo[1]*2.0925f + s.deo[2]*0.585f;
            s.dei[1] = s.dei[0];
            s.dei[0] = dei0;
            s.deo[2] = s.deo[1];
            s.deo[1] = s.deo[0];
            s.deo[0] = x[i];
        }
    }

    private static float reflection_coeffs_to_predictor_coeffs(ReadOnlySpan<float> rc, float[] pc, float gprime)
    {
        float[] temp = new float[10];
        float g2pass;
        int i;
        int j;

        g2pass = 1.0f;
        for (i = 0; i < LPC10_ORDER; i++)
            g2pass *= 1.0f - rc[i]*rc[i];
        g2pass = gprime*MathF.Sqrt(g2pass);
        pc[0] = rc[0];
        for (i = 1; i < LPC10_ORDER; i++)
        {
            for (j = 0; j < i; j++)
                temp[j] = pc[j] - rc[i]*pc[i - j - 1];
            for (j = 0; j < i; j++)
                pc[j] = temp[j];
            pc[i] = rc[i];
        }
        return g2pass;
    }

    private static int synths(lpc10_decode_state_t s,
                              int[] voice,
                              ref int pitch,
                              float rms,
                              float[] rc,
                              float[] speech)
    {
        int[] ivuv = new int[16];
        int[] ipiti = new int[16];
        int nout;
        int i;
        int j;
        float[] rmsi = new float[16];
        float ratio;
        float g2pass;
        float[] pc = new float[LPC10_ORDER];
        float[] rci = new float[16*LPC10_ORDER];

        pitch = max(min(pitch, LPC10_MIN_PITCH), LPC10_MAX_PITCH);
        for (i = 0; i < LPC10_ORDER; i++)
            rc[i] = max(min(rc[i], 0.99f), -0.99f);
        pitsyn(s, voice, ref pitch, rms, rc, ivuv, ipiti, rmsi, rci, out nout, out ratio);
        if (nout > 0)
        {
            for (j = 0; j < nout; j++)
            {
                g2pass = reflection_coeffs_to_predictor_coeffs(rci.AsSpan(j*LPC10_ORDER), pc, 0.7f);
                bsynz(s, pc, ipiti[j], ref ivuv[j], s.buf.AsSpan(s.buflen), rmsi[j], ratio, g2pass);
                deemp(s, s.buf.AsSpan(s.buflen), ipiti[j]);
                s.buflen += ipiti[j];
            }
            for (i = 0; i < LPC10_SAMPLES_PER_FRAME; i++)
                speech[i] = s.buf[i]/4096.0f;
            s.buflen -= LPC10_SAMPLES_PER_FRAME;
            for (i = 0; i < s.buflen; i++)
                s.buf[i] = s.buf[i + LPC10_SAMPLES_PER_FRAME];
        }
        return 0;
    }

    private static void lpc10_unpack(lpc10_frame_t t, ReadOnlySpan<byte> ibits)
    {
        ReadOnlySpan<int> bit =
        [
            2, 4, 8, 8, 8, 8, 16, 16, 16, 16
        ];
        ReadOnlySpan<int> iblist =
        [
            13, 12, 11,  1,  2, 13, 12, 11,  1,  2,
            13, 10, 11,  2,  1, 10, 13, 12, 11, 10,
             2, 13, 12, 11, 10,  2,  1, 12,  7,  6,
             1, 10,  9,  8,  7,  4,  6,  9,  8,  7,
             5,  1,  9,  8,  4,  6,  1,  5,  9,  8,
             7,  5,  6
        ];
        Span<int> itab = stackalloc int[13];
        int x;
        int i;

        for (i = 0; i < 13; i++)
            itab[i] = 0;
        for (i = 0; i < 53; i++)
        {
            x = 52 - i;
            x = (ibits[x >> 3] >> (7 - (x & 7))) & 1;
            itab[iblist[52 - i] - 1] = (itab[iblist[52 - i] - 1] << 1) | x;
        }
        for (i = 0; i < LPC10_ORDER; i++)
        {
            if ((itab[i + 3] & bit[i]) != 0)
                itab[i + 3] -= bit[i] << 1;
        }
        t.ipitch = itab[0];
        t.irms = itab[1];
        for (i = 0; i < LPC10_ORDER; i++)
            t.irc[i] = itab[LPC10_ORDER - 1 - i + 3];
    }
    private static int hamming_84_decode(int input, ref int errcnt)
    {
        int i;
        int parity;
        int output;

        parity = input & 255;
        parity ^= parity >> 4;
        parity ^= parity >> 2;
        parity ^= parity >> 1;
        parity &= 1;
        i = dactab[input & 127];
        output = i & 15;
        if ((i & 16) != 0)
        {
            if (parity != 0)
                errcnt++;
        }
        else
        {
            errcnt++;
            if (parity == 0)
            {
                errcnt++;
                output = -1;
            }
        }
        return output;
    }
    private static int median(int d1, int d2, int d3)
    {
        int ret_val;

        ret_val = d2;
        if (d2 > d1 && d2 > d3)
        {
            ret_val = d1;
            if (d3 > d1)
                ret_val = d3;
        }
        else if (d2 < d1 && d2 < d3)
        {
            ret_val = d1;
            if (d3 < d1)
                ret_val = d3;
        }
        return ret_val;
    }
    private static void decode(lpc10_decode_state_t s,
                               lpc10_frame_t t,
                               int[] voice,
                               out int pitch,
                               out float rms,
                               float[] rc)
    {
        ReadOnlySpan<int> bit =
        [
            2, 4, 8, 16, 32
        ];
        int ipit;
        int iout;
        int i;
        int icorf;
        int index;
        int ivoic;
        int ixcor;
        int i1;
        int i2;
        int i4;
        int ishift;
        int lsb;
        int errcnt;

        i4 = detau[t.ipitch];
        if (s.error_correction == 0)
        {
            voice[0] = 1;
            voice[1] = 1;
            if (t.ipitch <= 1)
                voice[0] = 0;
            if (t.ipitch == 0 || t.ipitch == 2)
                voice[1] = 0;
            if (i4 <= 4)
                i4 = s.iptold;
            pitch = i4;
            if (voice[0] == 1 && voice[1] == 1)
                s.iptold = pitch;
            if (voice[0] != voice[1])
                pitch = s.iptold;
        }
        else
        {
            if (i4 > 4)
            {
                s.dpit[0] = i4;
                ivoic = 2;
                s.iavgp = (s.iavgp*15 + i4 + 8)/16;
            }
            else
            {
                s.dpit[0] = s.iavgp;
                ivoic = i4;
            }
            s.drms[0] = t.irms;
            for (i = 0; i < LPC10_ORDER; i++)
                s.drc[i, 0] = t.irc[i];
            index = (s.ivp2h << 4) + (s.iovoic << 2) + ivoic + 1;
            i1 = ivtab[index - 1];
            ipit = i1 & 3;
            icorf = i1 >> 3;
            if (s.erate < 2048)
                icorf /= 64;
            ixcor = 4;
            if (s.erate < 2048)
                ixcor = 3;
            if (s.erate < 1024)
                ixcor = 2;
            if (s.erate < 128)
                ixcor = 1;
            voice[0] = icorf/2 & 1;
            voice[1] = icorf & 1;
            if (s.first)
            {
                s.first = false;
                if (i4 <= 4)
                    i4 = s.iptold;
                pitch = i4;
            }
            else
            {
                if ((icorf & bit[3]) != 0)
                {
                    errcnt = 0;
                    lsb = s.drms[1] & 1;
                    index = (s.drc[7, 1] << 4) + s.drms[1]/2;
                    iout = hamming_84_decode(index, ref errcnt);
                    s.drms[1] = s.drms[2];
                    if (iout >= 0)
                        s.drms[1] = (iout << 1) + lsb;
                    for (i = 1; i <= 4; i++)
                    {
                        if (i == 1)
                            i1 = ((s.drc[8, 1] & 7) << 1) + (s.drc[9, 1] & 1);
                        else
                            i1 = s.drc[8 - i, 1] & 15;
                        i2 = s.drc[4 - i, 1] & 31;
                        lsb = i2 & 1;
                        index = (i1 << 4) + (i2 >> 1);
                        iout = hamming_84_decode(index, ref errcnt);
                        if (iout >= 0)
                        {
                            iout = (iout << 1) + lsb;
                            if ((iout & 16) == 16)
                                iout -= 32;
                        }
                        else
                        {
                            iout = s.drc[4 - i, 2];
                        }
                        s.drc[4 - i, 1] = iout;
                    }
                    s.erate = (int) (s.erate*0.96875f + errcnt*102.0f);
                }
                t.irms = s.drms[1];
                for (i = 0; i < LPC10_ORDER; i++)
                    t.irc[i] = s.drc[i, 1];
                if (ipit == 1)
                    s.dpit[1] = s.dpit[2];
                if (ipit == 3)
                    s.dpit[1] = s.dpit[0];
                pitch = s.dpit[1];
                if ((icorf & bit[1]) != 0)
                {
                    if (Math.Abs(s.drms[1] - s.drms[0]) >= corth[ixcor + 3]
                        && Math.Abs(s.drms[1] - s.drms[2]) >= corth[ixcor + 3])
                    {
                        t.irms = median(s.drms[2], s.drms[1], s.drms[0]);
                    }
                    for (i = 0; i < 6; i++)
                    {
                        if (Math.Abs(s.drc[i, 1] - s.drc[i, 0]) >= corth[ixcor + ((i + 3) << 2) - 5]
                            && Math.Abs(s.drc[i, 1] - s.drc[i, 2]) >= corth[ixcor + ((i + 3) << 2) - 5])
                        {
                            t.irc[i] = median(s.drc[i, 2], s.drc[i, 1], s.drc[i, 0]);
                        }
                    }
                }
                if ((icorf & bit[2]) != 0)
                {
                    if (Math.Abs(s.dpit[1] - s.dpit[0]) >= corth[ixcor - 1]
                        && Math.Abs(s.dpit[1] - s.dpit[2]) >= corth[ixcor - 1])
                    {
                        pitch = median(s.dpit[2], s.dpit[1], s.dpit[0]);
                    }
                }
            }
            if ((icorf & bit[4]) != 0)
            {
                for (i = 4; i < LPC10_ORDER; i++)
                    t.irc[i] = zrc[i];
            }
            s.iovoic = ivoic;
            s.ivp2h = voice[1];
            s.dpit[2] = s.dpit[1];
            s.dpit[1] = s.dpit[0];
            s.drms[2] = s.drms[1];
            s.drms[1] = s.drms[0];
            for (i = 0; i < LPC10_ORDER; i++)
            {
                s.drc[i, 2] = s.drc[i, 1];
                s.drc[i, 1] = s.drc[i, 0];
            }
        }
        t.irms = rmst[(31 - t.irms)*2];
        for (i = 0; i < 2; i++)
        {
            i2 = t.irc[i];
            i1 = 0;
            if (i2 < 0)
            {
                i1 = 1;
                i2 = -i2;
                if (i2 > 15)
                    i2 = 0;
            }
            i2 = detab7[i2*2];
            if (i1 == 1)
                i2 = -i2;
            ishift = 15 - nbit[i];
            t.irc[i] = i2*pow_ii(2, ishift);
        }
        for (i = 2; i < LPC10_ORDER; i++)
        {
            ishift = 15 - nbit[i];
            i2 = t.irc[i]*pow_ii(2, ishift) + qb[i - 2];
            t.irc[i] = (int) (i2*descl[i - 2] + deadd[i - 2]);
        }
        rms = t.irms;
        for (i = 0; i < LPC10_ORDER; i++)
            rc[i] = t.irc[i]/16384.0f;
    }



    /// <summary>Initializes or resets an LPC10 decoder state.</summary>
    public static lpc10_decode_state_t lpc10_decode_init(lpc10_decode_state_t? s, int error_correction)
    {
        ReadOnlySpan<short> rand_init =
        [
            -21161,
             -8478,
             30892,
            -10216,
             16950
        ];
        int i;
        int j;

        if (s is null)
            s = new lpc10_decode_state_t();

        s.error_correction = error_correction;
        s.iptold = 60;
        s.first = true;
        s.ivp2h = 0;
        s.iovoic = 0;
        s.iavgp = 60;
        s.erate = 0;
        for (i = 0; i < 3; i++)
        {
            for (j = 0; j < 10; j++)
                s.drc[j, i] = 0;
            s.dpit[i] = 0;
            s.drms[i] = 0;
        }
        for (i = 0; i < 360; i++)
            s.buf[i] = 0.0f;
        s.buflen = LPC10_SAMPLES_PER_FRAME;
        s.rmso = 1.0f;
        s.first_pitsyn = true;
        s.ipo = 0;
        for (i = 0; i < 166; i++)
        {
            s.exc[i] = 0.0f;
            s.exc2[i] = 0.0f;
        }
        for (i = 0; i < 3; i++)
        {
            s.lpi[i] = 0.0f;
            s.hpi[i] = 0.0f;
        }
        s.rmso_bsynz = 0.0f;
        s.j = 1;
        s.k = 4;
        for (i = 0; i < 5; i++)
            s.y[i] = rand_init[i];
        for (i = 0; i < 2; i++)
            s.dei[i] = 0.0f;
        for (i = 0; i < 3; i++)
            s.deo[i] = 0.0f;
        return s;
    }

    public static int lpc10_decode_release(lpc10_decode_state_t s)
    {
        return 0;
    }

    public static int lpc10_decode_free(lpc10_decode_state_t s)
    {
        return 0;
    }

    /// <summary>Decodes complete seven-byte LPC10 frames into 180 PCM samples each.</summary>
    public static int lpc10_decode(lpc10_decode_state_t s, short[] amp, byte[] code, int len)
    {
        int[] voice = new int[2];
        int pitch;
        float[] speech = new float[LPC10_SAMPLES_PER_FRAME];
        float[] rc = new float[LPC10_ORDER];
        lpc10_frame_t frame = new lpc10_frame_t();
        float rms;
        int i;
        int j;
        int @base;

        len /= 7;
        for (i = 0; i < len; i++)
        {
            lpc10_unpack(frame, code.AsSpan(i*7));
            decode(s, frame, voice, out pitch, out rms, rc);
            synths(s, voice, ref pitch, rms, rc, speech);
            @base = i*LPC10_SAMPLES_PER_FRAME;
            for (j = 0; j < LPC10_SAMPLES_PER_FRAME; j++)
                amp[@base + j] = unchecked((short)global::TKFaxEngine.FastConvert.lfastrintf(32768.0f*speech[j]));
        }
        return len*LPC10_SAMPLES_PER_FRAME;
    }

}
