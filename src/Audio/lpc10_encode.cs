/*
 * TKFaxEngine - managed C# port
 *
 * lpc10_encode.cs
 *
 * Direct C# conversion of EngineFX lpc10_encode.c and public declarations from lpc10.h.
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * This port preserves the GNU Lesser General Public License version 2.1.
 */

#nullable enable

namespace TKFaxEngine.Audio;

public static partial class lpc10
{
    private static void lpc10_pack(lpc10_encode_state_t s, Span<byte> ibits, lpc10_frame_t t)
    {
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

        itab[0] = t.ipitch;
        itab[1] = t.irms;
        itab[2] = 0;
        for (i = 0; i < LPC10_ORDER; i++)
            itab[i + 3] = t.irc[LPC10_ORDER - 1 - i] & 0x7FFF;
        x = 0;
        for (i = 0; i < 53; i++)
        {
            x = (x << 1) | (itab[iblist[i] - 1] & 1);
            if ((i & 7) == 7)
                ibits[i >> 3] = (byte) (x & 0xFF);
            itab[iblist[i] - 1] >>= 1;
        }
        x = (x << 1) | (s.isync & 1);
        s.isync ^= 1;
        x <<= 2;
        ibits[6] = (byte) (x & 0xFF);
    }

    private static int encode(lpc10_encode_state_t s,
                              lpc10_frame_t t,
                              int[] voice,
                              int pitch,
                              float rms,
                              float[] rc)
    {
        ReadOnlySpan<int> enctab =
        [
            0, 7, 11, 12, 13, 10, 6, 1, 14, 9, 5, 2, 3, 4, 8, 15
        ];
        ReadOnlySpan<int> entau =
        [
            19,  11,  27,  25,  29,  21,  23,  22,  30,  14,  15,   7,  39,  38,  46,
            42,  43,  41,  45,  37,  53,  49,  51,  50,  54,  52,  60,  56,  58,  26,
            90,  88,  92,  84,  86,  82,  83,  81,  85,  69,  77,  73,  75,  74,  78,
            70,  71,  67,  99,  97, 113, 112, 114,  98, 106, 104, 108, 100, 101,  76
        ];
        ReadOnlySpan<int> enadd =
        [
            1920, -768, 2432, 1280, 3584, 1536, 2816, -1152
        ];
        ReadOnlySpan<float> enscl =
        [
            0.0204f, 0.0167f, 0.0145f, 0.0147f, 0.0143f, 0.0135f, 0.0125f, 0.0112f
        ];
        ReadOnlySpan<int> enbits =
        [
            6, 5, 4, 4, 4, 4, 3, 3
        ];
        ReadOnlySpan<int> entab6 =
        [
            0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 3, 3,
            3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5, 6, 6, 6, 6, 6,
            7, 7, 7, 7, 7, 8, 8, 8, 8, 9, 9, 9, 10, 10, 11, 11, 12, 13, 14, 15
        ];
        ReadOnlySpan<int> rmst =
        [
            1024, 936, 856, 784, 718, 656, 600, 550,
             502, 460, 420, 384, 352, 328, 294, 270,
             246, 226, 206, 188, 172, 158, 144, 132,
             120, 110, 102,  92,  84,  78,  70,  64,
              60,  54,  50,  46,  42,  38,  34,  32,
              30,  26,  24,  22,  20,  18,  17,  16,
              15,  14,  13,  12,  11,  10,   9,   8,
               7,   6,   5,   4,   3,   2,   1,   0
        ];

        int idel;
        int nbit;
        int i;
        int j;
        int i2;
        int i3;
        int mrk;

        t.irms = (int) rms;
        for (i = 0; i < LPC10_ORDER; i++)
            t.irc[i] = (int) (rc[i]*32768.0f);
        if (voice[0] != 0 && voice[1] != 0)
        {
            t.ipitch = entau[pitch - 1];
        }
        else
        {
            if (s.error_correction != 0)
            {
                t.ipitch = 0;
                if (voice[0] != voice[1])
                    t.ipitch = 127;
            }
            else
            {
                t.ipitch = (voice[0] << 1) + voice[1];
            }
        }
        j = 32;
        idel = 16;
        t.irms = min(t.irms, 1023);
        while (idel > 0)
        {
            if (t.irms > rmst[j - 1])
                j -= idel;
            if (t.irms < rmst[j - 1])
                j += idel;
            idel /= 2;
        }
        if (t.irms > rmst[j - 1])
            --j;
        t.irms = 31 - j/2;
        for (i = 0; i < 2; i++)
        {
            i2 = t.irc[i];
            mrk = 0;
            if (i2 < 0)
            {
                i2 = -i2;
                mrk = 1;
            }
            i2 = min(i2/512, 63);
            i2 = entab6[i2];
            if (mrk != 0)
                i2 = -i2;
            t.irc[i] = i2;
        }
        for (i = 2; i < LPC10_ORDER; i++)
        {
            i2 = (int) ((t.irc[i]/2 + enadd[LPC10_ORDER - 1 - i])*enscl[LPC10_ORDER - 1 - i]);
            i2 = max(i2, -127);
            i2 = min(i2, 127);
            nbit = enbits[LPC10_ORDER - 1 - i];
            i3 = i2 < 0 ? 1 : 0;
            i2 /= pow_ii(2, nbit);
            if (i3 != 0)
                i2--;
            t.irc[i] = i2;
        }
        if (s.error_correction != 0)
        {
            if (t.ipitch == 0 || t.ipitch == 127)
            {
                t.irc[4] = enctab[(t.irc[0] & 0x1E) >> 1];
                t.irc[5] = enctab[(t.irc[1] & 0x1E) >> 1];
                t.irc[6] = enctab[(t.irc[2] & 0x1E) >> 1];
                t.irc[7] = enctab[(t.irms & 0x1E) >> 1];
                t.irc[8] = enctab[(t.irc[3] & 0x1E) >> 1] >> 1;
                t.irc[9] = enctab[(t.irc[3] & 0x1E) >> 1] & 1;
            }
        }
        return 0;
    }

    private static void high_pass_100hz(lpc10_encode_state_t s, float[] speech, int start, int len)
    {
        float si;
        float err;
        int i;

        for (i = start; i < len; i++)
        {
            si = speech[i];
            err = si + s.z11*1.859076f - s.z21*0.8648249f;
            si = err - s.z11*2.0f + s.z21;
            s.z21 = s.z11;
            s.z11 = err;
            err = si + s.z12*1.935715f - s.z22*0.9417004f;
            si = err - s.z12*2.0f + s.z22;
            s.z22 = s.z12;
            s.z12 = err;
            speech[i] = si*0.902428f;
        }
    }

    public static lpc10_encode_state_t lpc10_encode_init(lpc10_encode_state_t? s, int error_correction)
    {
        int i;
        int j;

        if (s is null)
            s = new lpc10_encode_state_t();

        s.error_correction = error_correction;
        s.z11 = 0.0f;
        s.z21 = 0.0f;
        s.z12 = 0.0f;
        s.z22 = 0.0f;
        for (i = 0; i < 540; i++)
        {
            s.inbuf[i] = 0.0f;
            s.pebuf[i] = 0.0f;
        }
        for (i = 0; i < 696; i++)
            s.lpbuf[i] = 0.0f;
        for (i = 0; i < 312; i++)
            s.ivbuf[i] = 0.0f;
        s.bias = 0.0f;
        s.osptr = 1;
        for (i = 0; i < 3; i++)
            s.obound[i] = 0;
        s.vwin[2, 0] = 307;
        s.vwin[2, 1] = 462;
        s.awin[2, 0] = 307;
        s.awin[2, 1] = 462;
        for (i = 0; i < 4; i++)
        {
            s.voibuf[i, 0] = 0;
            s.voibuf[i, 1] = 0;
        }
        for (i = 0; i < 3; i++)
            s.rmsbuf[i] = 0.0f;
        for (i = 0; i < 3; i++)
        {
            for (j = 0; j < 10; j++)
                s.rcbuf[i, j] = 0.0f;
        }
        s.zpre = 0.0f;
        s.n = 0.0f;
        s.d__ = 1.0f;
        for (i = 0; i < 16; i++)
            s.l2buf[i] = 0.0f;
        s.l2sum1 = 0.0f;
        s.l2ptr1 = 1;
        s.l2ptr2 = 9;
        s.hyst = false;
        s.dither = 20.0f;
        s.maxmin = 0.0f;
        for (i = 0; i < 3; i++)
        {
            s.voice[i, 0] = 0.0f;
            s.voice[i, 1] = 0.0f;
        }
        s.lbve = 3000;
        s.fbve = 3000;
        s.fbue = 187;
        s.ofbue = 187;
        s.sfbue = 187;
        s.lbue = 93;
        s.olbue = 93;
        s.slbue = 93;
        s.snr = s.fbve/s.fbue << 6;
        for (i = 0; i < 60; i++)
            s.s[i] = 0.0f;
        for (i = 0; i < 2; i++)
        {
            for (j = 0; j < 60; j++)
                s.p[i, j] = 0;
        }
        s.ipoint = 0;
        s.alphax = 0.0f;
        s.isync = 0;
        return s;
    }

    public static int lpc10_encode_release(lpc10_encode_state_t s)
    {
        return 0;
    }

    public static int lpc10_encode_free(lpc10_encode_state_t s)
    {
        return 0;
    }

    public static int lpc10_encode(lpc10_encode_state_t s, byte[] code, short[] amp, int len)
    {
        int[] voice = new int[2];
        int pitch;
        float[] speech = new float[LPC10_SAMPLES_PER_FRAME];
        float[] rc = new float[LPC10_ORDER];
        float rms;
        lpc10_frame_t frame = new lpc10_frame_t();
        int i;
        int j;

        len /= LPC10_SAMPLES_PER_FRAME;
        for (i = 0; i < len; i++)
        {
            for (j = 0; j < LPC10_SAMPLES_PER_FRAME; j++)
                speech[j] = amp[i*LPC10_SAMPLES_PER_FRAME + j]/32768.0f;
            high_pass_100hz(s, speech, 0, LPC10_SAMPLES_PER_FRAME);
            lpc10_analyse(s, speech, voice, out pitch, out rms, rc);
            encode(s, frame, voice, pitch, rms, rc);
            lpc10_pack(s, code.AsSpan(7*i), frame);
        }
        return len*7;
    }
}
