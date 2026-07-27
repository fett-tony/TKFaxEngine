/*
 * TKFaxEngine - managed C# port
 *
 * lpc10_voicing.cs
 *
 * Direct C# conversion of EngineFX lpc10_voicing.c and declarations from lpc10_encdecs.h.
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * This port preserves the GNU Lesser General Public License version 2.1.
 */

#nullable enable

namespace TKFaxEngine.Audio;

public static partial class lpc10 {
    private static void vparms(int[] vwin,
                               float[] inbuf,
                               float[] lpbuf,
                               int[] buflim,
                               int half,
                               ref float dither,
                               ref int mintau,
                               out int zc,
                               out int lbe,
                               out int fbe,
                               out float qs,
                               out float rc1,
                               out float ar_b,
                               out float ar_f) {
        int inbuf_offset;
        int lpbuf_offset;
        int vlen;
        int stop;
        int i;
        int start;
        float r1;
        float r2;
        float e_pre;
        float ap_rms;
        float e_0;
        float oldsgn;
        float lp_rms;
        float e_b;
        float e_f;
        float r_b;
        float r_f;
        float e0ap;

        lpbuf_offset = buflim[2];
        inbuf_offset = buflim[0];

        lp_rms = 0.0f;
        ap_rms = 0.0f;
        e_pre = 0.0f;
        e0ap = 0.0f;
        rc1 = 0.0f;
        e_0 = 0.0f;
        e_b = 0.0f;
        e_f = 0.0f;
        r_f = 0.0f;
        r_b = 0.0f;
        zc = 0;
        vlen = vwin[1] - vwin[0] + 1;
        start = vwin[0] + half * vlen / 2 + 1;
        stop = start + vlen / 2 - 1;

        oldsgn = r_sign(1.0f, inbuf[start - 1 - inbuf_offset] - dither);
        for (i = start; i <= stop; i++) {
            lp_rms += MathF.Abs(lpbuf[i - lpbuf_offset]);
            ap_rms += MathF.Abs(inbuf[i - inbuf_offset]);
            e_pre += MathF.Abs(inbuf[i - inbuf_offset] - inbuf[i - 1 - inbuf_offset]);
            r1 = inbuf[i - inbuf_offset];
            e0ap += r1 * r1;
            rc1 += inbuf[i - inbuf_offset] * inbuf[i - 1 - inbuf_offset];
            r1 = lpbuf[i - lpbuf_offset];
            e_0 += r1 * r1;
            r1 = lpbuf[i - mintau - lpbuf_offset];
            e_b += r1 * r1;
            r1 = lpbuf[i + mintau - lpbuf_offset];
            e_f += r1 * r1;
            r_f += lpbuf[i - lpbuf_offset] * lpbuf[i + mintau - lpbuf_offset];
            r_b += lpbuf[i - lpbuf_offset] * lpbuf[i - mintau - lpbuf_offset];
            r1 = inbuf[i - inbuf_offset] + dither;
            if (r_sign(1.0f, r1) != oldsgn) {
                ++zc;
                oldsgn = -oldsgn;
            }
            dither = -dither;
        }
        rc1 /= max(e0ap, 1.0f);
        r1 = ap_rms * 2.0f;
        qs = e_pre / max(r1, 1.0f);
        ar_b = r_b / max(e_b, 1.0f) * (r_b / max(e_0, 1.0f));
        ar_f = r_f / max(e_f, 1.0f) * (r_f / max(e_0, 1.0f));
        r2 = zc << 1;
        zc = global::TKFaxEngine.FastConvert.lfastrintf(r2 * (90.0f / vlen));
        r1 = lp_rms / 4 * (90.0f / vlen);
        lbe = min(global::TKFaxEngine.FastConvert.lfastrintf(r1), 32767);
        r1 = ap_rms / 4 * (90.0f / vlen);
        fbe = min(global::TKFaxEngine.FastConvert.lfastrintf(r1), 32767);
    }

    public static void lpc10_voicing(lpc10_encode_state_t s,
                                     int[] vwin,
                                     float[] inbuf,
                                     float[] lpbuf,
                                     int[] buflim,
                                     int half,
                                     ref float minamd,
                                     ref float maxamd,
                                     ref int mintau,
                                     float[] ivrc,
                                     int[] obound) {
        ReadOnlySpan<float> vdc =
        [
            0.0f, 1714.0f, -110.0f, 334.0f, -4096.0f,  -654.0f, 3752.0f, 3769.0f, 0.0f,  1181.0f,
            0.0f,  874.0f,  -97.0f, 300.0f, -4096.0f, -1021.0f, 2451.0f, 2527.0f, 0.0f,  -500.0f,
            0.0f,  510.0f,  -70.0f, 250.0f, -4096.0f, -1270.0f, 2194.0f, 2491.0f, 0.0f, -1500.0f,
            0.0f,  500.0f,  -10.0f, 200.0f, -4096.0f, -1300.0f,  2.0e3f,  2.0e3f, 0.0f,  -2.0e3f,
            0.0f,  500.0f,    0.0f,   0.0f, -4096.0f, -1300.0f,  2.0e3f,  2.0e3f, 0.0f, -2500.0f,
            0.0f,    0.0f,    0.0f,   0.0f,     0.0f,     0.0f,    0.0f,    0.0f, 0.0f,     0.0f,
            0.0f,    0.0f,    0.0f,   0.0f,     0.0f,     0.0f,    0.0f,    0.0f, 0.0f,     0.0f,
            0.0f,    0.0f,    0.0f,   0.0f,     0.0f,     0.0f,    0.0f,    0.0f, 0.0f,     0.0f,
            0.0f,    0.0f,    0.0f,   0.0f,     0.0f,     0.0f,    0.0f,    0.0f, 0.0f,     0.0f,
            0.0f,    0.0f,    0.0f,   0.0f,     0.0f,     0.0f,    0.0f,    0.0f, 0.0f,     0.0f
        ];
        const int nvdcl = 5;
        ReadOnlySpan<float> vdcl =
        [
            600.0f, 450.0f, 300.0f, 200.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f
        ];

        int i1;
        float r1;
        float r2;
        float ar_b;
        float ar_f;
        int snrl;
        int i;
        float[] value = new float[9];
        int zc;
        int ot;
        float qs;
        int vstate;
        float rc1;
        int fbe;
        int lbe;
        float snr2;

        if (half == 0) {
            s.voice[0, 0] = s.voice[1, 0];
            s.voice[0, 1] = s.voice[1, 1];
            s.voice[1, 0] = s.voice[2, 0];
            s.voice[1, 1] = s.voice[2, 1];
            s.maxmin = maxamd / max(minamd, 1.0f);
        }
        vparms(vwin,
               inbuf,
               lpbuf,
               buflim,
               half,
               ref s.dither,
               ref mintau,
               out zc,
               out lbe,
               out fbe,
               out qs,
               out rc1,
               out ar_b,
               out ar_f);
        r1 = (s.snr + s.fbve / (float)max(s.fbue, 1)) * 63 / 64.0f;
        s.snr = global::TKFaxEngine.FastConvert.lfastrintf(r1);
        snr2 = s.snr * s.fbue / max(s.lbue, 1);
        i1 = nvdcl - 1;
        for (snrl = 0; snrl < i1; snrl++) {
            if (snr2 > vdcl[snrl])
                break;
        }
        value[0] = s.maxmin;
        value[1] = (float)lbe / max(s.lbve, 1);
        value[2] = zc;
        value[3] = rc1;
        value[4] = qs;
        value[5] = ivrc[1];
        value[6] = ar_b;
        value[7] = ar_f;
        s.voice[2, half] = vdc[snrl * 10 + 9];
        for (i = 0; i < 8; i++)
            s.voice[2, half] += vdc[snrl * 10 + i] * value[i];
        s.voibuf[3, half] = s.voice[2, half] > 0.0f ? 1 : 0;
        if (half != 0) {
            ot = (((obound[0] & 2) != 0 || obound[1] == 1) && (obound[2] & 1) == 0) ? 1 : 0;
            vstate = (s.voibuf[1, 0] << 3) + (s.voibuf[1, 1] << 2) + (s.voibuf[2, 0] << 1) + s.voibuf[2, 1];
            switch (vstate + 1) {
                case 2:
                    if (ot != 0 && s.voibuf[3, 0] == 1)
                        s.voibuf[2, 0] = 1;
                    break;
                case 3:
                    if (s.voibuf[3, 0] == 0 || s.voice[1, 0] < -s.voice[1, 1])
                        s.voibuf[2, 0] = 0;
                    else
                        s.voibuf[2, 1] = 1;
                    break;
                case 5:
                    s.voibuf[1, 1] = 0;
                    break;
                case 6:
                    if (s.voice[0, 1] < -s.voice[1, 0])
                        s.voibuf[1, 1] = 0;
                    else
                        s.voibuf[2, 0] = 1;
                    break;
                case 7:
                    if (s.voibuf[0, 0] == 1 || s.voibuf[3, 0] == 1 || s.voice[1, 1] > s.voice[0, 0])
                        s.voibuf[2, 1] = 1;
                    else
                        s.voibuf[1, 0] = 1;
                    break;
                case 8:
                    if (ot != 0)
                        s.voibuf[1, 1] = 0;
                    break;
                case 9:
                    if (ot != 0)
                        s.voibuf[1, 1] = 1;
                    break;
                case 11:
                    if (s.voice[1, 0] < -s.voice[0, 1])
                        s.voibuf[2, 0] = 0;
                    else
                        s.voibuf[1, 1] = 1;
                    break;
                case 12:
                    s.voibuf[1, 1] = 1;
                    break;
                case 14:
                    if (s.voibuf[3, 0] == 0 && s.voice[1, 1] < -s.voice[1, 0])
                        s.voibuf[2, 1] = 0;
                    else
                        s.voibuf[2, 0] = 1;
                    break;
                case 15:
                    if (ot != 0 && s.voibuf[3, 0] == 0)
                        s.voibuf[2, 0] = 0;
                    break;
            }
        }
        if (s.voibuf[3, half] == 0) {
            r1 = (s.sfbue * 63 + (min(fbe, s.ofbue * 3) << 3)) / 64.0f;
            s.sfbue = global::TKFaxEngine.FastConvert.lfastrintf(r1);
            s.fbue = s.sfbue / 8;
            s.ofbue = fbe;
            r1 = (s.slbue * 63 + (min(lbe, s.olbue * 3) << 3)) / 64.0f;
            s.slbue = global::TKFaxEngine.FastConvert.lfastrintf(r1);
            s.lbue = s.slbue / 8;
            s.olbue = lbe;
        } else {
            s.lbve = global::TKFaxEngine.FastConvert.lfastrintf((s.lbve * 63 + lbe) / 64.0f);
            s.fbve = global::TKFaxEngine.FastConvert.lfastrintf((s.fbve * 63 + fbe) / 64.0f);
        }
        r2 = MathF.Sqrt((float)(s.lbue * s.lbve)) * 64 / 3000;
        r1 = max(r2, 1.0f);
        s.dither = min(r1, 20.0f);
    }
}
