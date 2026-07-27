/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * G726.cs - The ITU G.726 codec
 *
 * Direct C# port of the TKFaxEngineFX g726.c and g726.h sources.
 * Written by Steve Underwood <steveu@coppice.org>
 * Based on the Sun Microsystems G.721/G.723 reference implementation.
 *
 * Copyright (C) 2006 Steve Underwood
 *
 * This file is distributed under the terms of the GNU Lesser General Public
 * License version 2.1, matching the original source files.
 */

#nullable enable

using System.Runtime.InteropServices;
using bitstream_state_t = TKFaxEngine.Modem.BitstreamState;
using static TKFaxEngine.BitOperationsApi;
using static TKFaxEngine.Audio.g711;
using static TKFaxEngine.Modem.BitstreamApi;

namespace TKFaxEngine.Audio;

public delegate short g726_decoder_func_t(g726_state_t s, byte code);
public delegate byte g726_encoder_func_t(g726_state_t s, short amp);

/*! G.726 state */
public sealed class g726_state_t
{
    /*! The bit rate */
    public int rate;
    /*! The external coding, for tandem operation */
    public int ext_coding;
    /*! The number of bits per sample */
    public int bits_per_sample;
    /*! One of the G.726_PACKING_xxx options */
    public int packing;

    /*! Locked or steady state step size multiplier. */
    public int yl;
    /*! Unlocked or non-steady state step size multiplier. */
    public short yu;
    /*! Short term energy estimate. */
    public short dms;
    /*! Long term energy estimate. */
    public short dml;
    /*! Linear weighting coefficient of 'yl' and 'yu'. */
    public short ap;

    /*! Coefficients of pole portion of prediction filter. */
    public readonly short[] a = new short[2];
    /*! Coefficients of zero portion of prediction filter. */
    public readonly short[] b = new short[6];
    /*! Signs of previous two samples of a partially reconstructed signal. */
    public readonly short[] pk = new short[2];
    /*! Previous 6 samples of the quantized difference signal. */
    public readonly short[] dq = new short[6];
    /*! Previous 2 samples of the quantized difference signal. */
    public readonly short[] sr = new short[2];
    /*! Delayed tone detect. */
    public int td;

    /*! The bit stream processing context. */
    public readonly bitstream_state_t bs = new bitstream_state_t();

    /*! The current encoder function. */
    public g726_encoder_func_t enc_func = null!;
    /*! The current decoder function. */
    public g726_decoder_func_t dec_func = null!;
}

public static class g726
{
    public const int G726_ENCODING_LINEAR = 0;
    public const int G726_ENCODING_ULAW = 1;
    public const int G726_ENCODING_ALAW = 2;

    public const int G726_PACKING_NONE = 0;
    public const int G726_PACKING_LEFT = 1;
    public const int G726_PACKING_RIGHT = 2;

    private static readonly int[] g726_16_dqlntab =
    [
        116, 365, 365, 116
    ];

    private static readonly int[] g726_16_witab =
    [
        -704, 14048, 14048, -704
    ];

    private static readonly int[] g726_16_fitab =
    [
        0x000, 0xE00, 0xE00, 0x000
    ];

    private static readonly int[] qtab_726_16 =
    [
        261
    ];

    private static readonly int[] g726_24_dqlntab =
    [
        -2048, 135, 273, 373, 373, 273, 135, -2048
    ];

    private static readonly int[] g726_24_witab =
    [
        -128, 960, 4384, 18624, 18624, 4384, 960, -128
    ];

    private static readonly int[] g726_24_fitab =
    [
        0x000, 0x200, 0x400, 0xE00, 0xE00, 0x400, 0x200, 0x000
    ];

    private static readonly int[] qtab_726_24 =
    [
        8, 218, 331
    ];

    private static readonly int[] g726_32_dqlntab =
    [
        -2048, 4, 135, 213, 273, 323, 373, 425,
        425, 373, 323, 273, 213, 135, 4, -2048
    ];

    private static readonly int[] g726_32_witab =
    [
        -384, 576, 1312, 2048, 3584, 6336, 11360, 35904,
        35904, 11360, 6336, 3584, 2048, 1312, 576, -384
    ];

    private static readonly int[] g726_32_fitab =
    [
        0x000, 0x000, 0x000, 0x200, 0x200, 0x200, 0x600, 0xE00,
        0xE00, 0x600, 0x200, 0x200, 0x200, 0x000, 0x000, 0x000
    ];

    private static readonly int[] qtab_726_32 =
    [
        -124, 80, 178, 246, 300, 349, 400
    ];

    private static readonly int[] g726_40_dqlntab =
    [
        -2048, -66, 28, 104, 169, 224, 274, 318,
        358, 395, 429, 459, 488, 514, 539, 566,
        566, 539, 514, 488, 459, 429, 395, 358,
        318, 274, 224, 169, 104, 28, -66, -2048
    ];

    private static readonly int[] g726_40_witab =
    [
        448, 448, 768, 1248, 1280, 1312, 1856, 3200,
        4512, 5728, 7008, 8960, 11456, 14080, 16928, 22272,
        22272, 16928, 14080, 11456, 8960, 7008, 5728, 4512,
        3200, 1856, 1312, 1280, 1248, 768, 448, 448
    ];

    private static readonly int[] g726_40_fitab =
    [
        0x000, 0x000, 0x000, 0x000, 0x000, 0x200, 0x200, 0x200,
        0x200, 0x200, 0x400, 0x600, 0x800, 0xA00, 0xC00, 0xC00,
        0xC00, 0xC00, 0xA00, 0x800, 0x600, 0x400, 0x200, 0x200,
        0x200, 0x200, 0x200, 0x000, 0x000, 0x000, 0x000, 0x000
    ];

    private static readonly int[] qtab_726_40 =
    [
        -122, -16, 68, 139, 198, 250, 298, 339,
        378, 413, 445, 475, 502, 528, 553
    ];

    private static short fmult(short an, short srn)
    {
        unchecked
        {
            short anmag;
            short anexp;
            short anmant;
            short wanexp;
            short wanmant;
            short retval;

            anmag = (short)((an > 0) ? an : ((-an) & 0x1FFF));
            anexp = (short)(top_bit((uint)anmag) - 5);
            anmant = (short)((anmag == 0) ? 32 : (anexp >= 0) ? (anmag >> anexp) : (anmag << -anexp));
            wanexp = (short)(anexp + ((srn >> 6) & 0xF) - 13);

            wanmant = (short)((anmant*(srn & 0x3F) + 0x30) >> 4);
            retval = (short)((wanexp >= 0) ? ((wanmant << wanexp) & 0x7FFF) : (wanmant >> -wanexp));

            return (short)(((an ^ srn) < 0) ? -retval : retval);
        }
    }
    /*- End of function --------------------------------------------------------*/

    private static short predictor_zero(g726_state_t s)
    {
        unchecked
        {
            int i;
            int sezi;

            sezi = fmult((short)(s.b[0] >> 2), s.dq[0]);
            for (i = 1; i < 6; i++)
                sezi += fmult((short)(s.b[i] >> 2), s.dq[i]);
            return (short)sezi;
        }
    }
    /*- End of function --------------------------------------------------------*/

    private static short predictor_pole(g726_state_t s)
    {
        unchecked
        {
            return (short)(fmult((short)(s.a[1] >> 2), s.sr[1]) + fmult((short)(s.a[0] >> 2), s.sr[0]));
        }
    }
    /*- End of function --------------------------------------------------------*/

    private static int step_size(g726_state_t s)
    {
        int y;
        int dif;
        int al;

        if (s.ap >= 256)
            return s.yu;
        y = s.yl >> 6;
        dif = s.yu - y;
        al = s.ap >> 2;
        if (dif > 0)
            y += (dif*al) >> 6;
        else if (dif < 0)
            y += (dif*al + 0x3F) >> 6;
        return y;
    }
    /*- End of function --------------------------------------------------------*/

    private static short quantize(int d, int y, int[] table, int quantizer_states)
    {
        unchecked
        {
            short dqm;
            short exp;
            short mant;
            short dl;
            short dln;
            int i;
            int size;

            dqm = (short)Math.Abs(d);
            exp = (short)(top_bit((uint)(dqm >> 1)) + 1);
            mant = (short)(((dqm << 7) >> exp) & 0x7F);
            dl = (short)((exp << 7) + mant);
            dln = (short)(dl - (short)(y >> 2));

            size = (quantizer_states - 1) >> 1;
            for (i = 0; i < size; i++)
            {
                if (dln < table[i])
                    break;
            }
            if (d < 0)
                return (short)((size << 1) + 1 - i);
            if (i == 0 && (quantizer_states & 1) != 0)
                return (short)quantizer_states;
            return (short)i;
        }
    }
    /*- End of function --------------------------------------------------------*/

    private static short reconstruct(int sign, int dqln, int y)
    {
        unchecked
        {
            short dql;
            short dex;
            short dqt;
            short dq;

            dql = (short)(dqln + (y >> 2));
            if (dql < 0)
                return (short)((sign != 0) ? -0x8000 : 0);
            dex = (short)((dql >> 7) & 15);
            dqt = (short)(128 + (dql & 127));
            dq = (short)((dqt << 7) >> (14 - dex));
            return (short)((sign != 0) ? (dq - 0x8000) : dq);
        }
    }
    /*- End of function --------------------------------------------------------*/

    private static void update(g726_state_t s, int y, int wi, int fi, int dq, int sr, int dqsez)
    {
        unchecked
        {
            short mag;
            short exp;
            short a2p;
            short a1ul;
            short pks1;
            short fa1;
            short ylint;
            short dqthr;
            short ylfrac;
            short thr;
            short pk0;
            int i;
            bool tr;

            a2p = 0;
            pk0 = (short)((dqsez < 0) ? 1 : 0);

            mag = (short)(dq & 0x7FFF);
            ylint = (short)(s.yl >> 15);
            ylfrac = (short)((s.yl >> 10) & 0x1F);
            thr = (short)((ylint > 9) ? (31 << 10) : ((32 + ylfrac) << ylint));
            dqthr = (short)((thr + (thr >> 1)) >> 1);
            if (s.td == 0)
                tr = false;
            else if (mag <= dqthr)
                tr = false;
            else
                tr = true;

            s.yu = (short)(y + ((wi - y) >> 5));
            if (s.yu < 544)
                s.yu = 544;
            else if (s.yu > 5120)
                s.yu = 5120;

            s.yl += s.yu + ((-s.yl) >> 6);

            if (tr)
            {
                s.a[0] = 0;
                s.a[1] = 0;
                s.b[0] = 0;
                s.b[1] = 0;
                s.b[2] = 0;
                s.b[3] = 0;
                s.b[4] = 0;
                s.b[5] = 0;
            }
            else
            {
                pks1 = (short)(pk0 ^ s.pk[0]);

                a2p = (short)(s.a[1] - (s.a[1] >> 7));
                if (dqsez != 0)
                {
                    fa1 = (short)((pks1 != 0) ? s.a[0] : -s.a[0]);
                    if (fa1 < -8191)
                        a2p -= 0x100;
                    else if (fa1 > 8191)
                        a2p += 0xFF;
                    else
                        a2p += (short)(fa1 >> 5);

                    if ((pk0 ^ s.pk[1]) != 0)
                    {
                        if (a2p <= -12160)
                            a2p = -12288;
                        else if (a2p >= 12416)
                            a2p = 12288;
                        else
                            a2p -= 0x80;
                    }
                    else
                    {
                        if (a2p <= -12416)
                            a2p = -12288;
                        else if (a2p >= 12160)
                            a2p = 12288;
                        else
                            a2p += 0x80;
                    }
                }

                s.a[1] = a2p;

                s.a[0] -= (short)(s.a[0] >> 8);
                if (dqsez != 0)
                {
                    if (pks1 == 0)
                        s.a[0] += 192;
                    else
                        s.a[0] -= 192;
                }
                a1ul = (short)(15360 - a2p);
                if (s.a[0] < -a1ul)
                    s.a[0] = (short)-a1ul;
                else if (s.a[0] > a1ul)
                    s.a[0] = a1ul;

                for (i = 0; i < 6; i++)
                {
                    s.b[i] -= (short)(s.b[i] >> ((s.bits_per_sample == 5) ? 9 : 8));
                    if ((dq & 0x7FFF) != 0)
                    {
                        if ((dq ^ s.dq[i]) >= 0)
                            s.b[i] += 128;
                        else
                            s.b[i] -= 128;
                    }
                }
            }

            for (i = 5; i > 0; i--)
                s.dq[i] = s.dq[i - 1];
            if (mag == 0)
            {
                s.dq[0] = (short)((dq >= 0) ? 0x20 : 0xFC20);
            }
            else
            {
                exp = (short)(top_bit((uint)mag) + 1);
                s.dq[0] = (short)((dq >= 0)
                    ? ((exp << 6) + ((mag << 6) >> exp))
                    : ((exp << 6) + ((mag << 6) >> exp) - 0x400));
            }

            s.sr[1] = s.sr[0];
            if (sr == 0)
            {
                s.sr[0] = 0x20;
            }
            else if (sr > 0)
            {
                exp = (short)(top_bit((uint)sr) + 1);
                s.sr[0] = (short)((exp << 6) + ((sr << 6) >> exp));
            }
            else if (sr > -32768)
            {
                mag = (short)-sr;
                exp = (short)(top_bit((uint)mag) + 1);
                s.sr[0] = (short)((exp << 6) + ((mag << 6) >> exp) - 0x400);
            }
            else
            {
                s.sr[0] = (short)(ushort)0xFC20;
            }

            s.pk[1] = s.pk[0];
            s.pk[0] = pk0;

            if (tr)
                s.td = 0;
            else if (a2p < -11776)
                s.td = 1;
            else
                s.td = 0;

            s.dms += (short)(((short)fi - s.dms) >> 5);
            s.dml += (short)(((short)(fi << 2) - s.dml) >> 7);

            if (tr)
                s.ap = 256;
            else if (y < 1536)
                s.ap += (short)((0x200 - s.ap) >> 4);
            else if (s.td != 0)
                s.ap += (short)((0x200 - s.ap) >> 4);
            else if (Math.Abs((s.dms << 2) - s.dml) >= (s.dml >> 3))
                s.ap += (short)((0x200 - s.ap) >> 4);
            else
                s.ap += (short)((-s.ap) >> 4);
        }
    }
    /*- End of function --------------------------------------------------------*/

    private static short tandem_adjust_alaw(short sr, int se, int y, int i, int sign, int[] qtab, int quantizer_states)
    {
        unchecked
        {
            byte sp;
            short dx;
            int id;
            int sd;

            if (sr <= -32768)
                sr = -1;
            sp = linear_to_alaw((sr >> 1) << 3);
            dx = (short)((alaw_to_linear(sp) >> 2) - se);
            id = quantize(dx, y, qtab, quantizer_states);
            if (id == i)
                return (short)sp;
            if ((id ^ sign) > (i ^ sign))
            {
                if ((sp & 0x80) != 0)
                    sd = (sp == 0xD5) ? 0x55 : (((sp ^ 0x55) - 1) ^ 0x55);
                else
                    sd = (sp == 0x2A) ? 0x2A : (((sp ^ 0x55) + 1) ^ 0x55);
            }
            else
            {
                if ((sp & 0x80) != 0)
                    sd = (sp == 0xAA) ? 0xAA : (((sp ^ 0x55) + 1) ^ 0x55);
                else
                    sd = (sp == 0x55) ? 0xD5 : (((sp ^ 0x55) - 1) ^ 0x55);
            }
            return (short)sd;
        }
    }
    /*- End of function --------------------------------------------------------*/

    private static short tandem_adjust_ulaw(short sr, int se, int y, int i, int sign, int[] qtab, int quantizer_states)
    {
        unchecked
        {
            byte sp;
            short dx;
            int id;
            int sd;

            if (sr <= -32768)
                sr = 0;
            sp = linear_to_ulaw(sr << 2);
            dx = (short)((ulaw_to_linear(sp) >> 2) - se);
            id = quantize(dx, y, qtab, quantizer_states);
            if (id == i)
                return (short)sp;
            if ((id ^ sign) > (i ^ sign))
            {
                if ((sp & 0x80) != 0)
                    sd = (sp == 0xFF) ? 0x7E : (sp + 1);
                else
                    sd = (sp == 0x00) ? 0x00 : (sp - 1);
            }
            else
            {
                if ((sp & 0x80) != 0)
                    sd = (sp == 0x80) ? 0x80 : (sp - 1);
                else
                    sd = (sp == 0x7F) ? 0xFE : (sp + 1);
            }
            return (short)sd;
        }
    }
    /*- End of function --------------------------------------------------------*/

    private static byte g726_16_encoder(g726_state_t s, short amp)
    {
        unchecked
        {
            int y;
            short sei;
            short sezi;
            short se;
            short d;
            short sr;
            short dqsez;
            short dq;
            short i;

            sezi = predictor_zero(s);
            sei = (short)(sezi + predictor_pole(s));
            se = (short)(sei >> 1);
            d = (short)(amp - se);
            y = step_size(s);
            i = quantize(d, y, qtab_726_16, 4);
            dq = reconstruct(i & 2, g726_16_dqlntab[i], y);
            sr = (short)((dq < 0) ? (se - (dq & 0x3FFF)) : (se + dq));
            dqsez = (short)(sr + (sezi >> 1) - se);
            update(s, y, g726_16_witab[i], g726_16_fitab[i], dq, sr, dqsez);
            return (byte)i;
        }
    }
    /*- End of function --------------------------------------------------------*/

    private static short g726_16_decoder(g726_state_t s, byte code)
    {
        unchecked
        {
            short sezi;
            short sei;
            short se;
            short sr;
            short dq;
            short dqsez;
            int y;

            code &= 0x03;
            sezi = predictor_zero(s);
            sei = (short)(sezi + predictor_pole(s));
            y = step_size(s);
            dq = reconstruct(code & 2, g726_16_dqlntab[code], y);
            se = (short)(sei >> 1);
            sr = (short)((dq < 0) ? (se - (dq & 0x3FFF)) : (se + dq));
            dqsez = (short)(sr + (sezi >> 1) - se);
            update(s, y, g726_16_witab[code], g726_16_fitab[code], dq, sr, dqsez);
            switch (s.ext_coding)
            {
                case G726_ENCODING_ALAW:
                    return tandem_adjust_alaw(sr, se, y, code, 2, qtab_726_16, 4);
                case G726_ENCODING_ULAW:
                    return tandem_adjust_ulaw(sr, se, y, code, 2, qtab_726_16, 4);
            }
            return (short)(sr << 2);
        }
    }
    /*- End of function --------------------------------------------------------*/

    private static byte g726_24_encoder(g726_state_t s, short amp)
    {
        unchecked
        {
            short sei;
            short sezi;
            short se;
            short d;
            short sr;
            short dqsez;
            short dq;
            short i;
            int y;

            sezi = predictor_zero(s);
            sei = (short)(sezi + predictor_pole(s));
            se = (short)(sei >> 1);
            d = (short)(amp - se);
            y = step_size(s);
            i = quantize(d, y, qtab_726_24, 7);
            dq = reconstruct(i & 4, g726_24_dqlntab[i], y);
            sr = (short)((dq < 0) ? (se - (dq & 0x3FFF)) : (se + dq));
            dqsez = (short)(sr + (sezi >> 1) - se);
            update(s, y, g726_24_witab[i], g726_24_fitab[i], dq, sr, dqsez);
            return (byte)i;
        }
    }
    /*- End of function --------------------------------------------------------*/

    private static short g726_24_decoder(g726_state_t s, byte code)
    {
        unchecked
        {
            short sezi;
            short sei;
            short se;
            short sr;
            short dq;
            short dqsez;
            int y;

            code &= 0x07;
            sezi = predictor_zero(s);
            sei = (short)(sezi + predictor_pole(s));
            y = step_size(s);
            dq = reconstruct(code & 4, g726_24_dqlntab[code], y);
            se = (short)(sei >> 1);
            sr = (short)((dq < 0) ? (se - (dq & 0x3FFF)) : (se + dq));
            dqsez = (short)(sr + (sezi >> 1) - se);
            update(s, y, g726_24_witab[code], g726_24_fitab[code], dq, sr, dqsez);
            switch (s.ext_coding)
            {
                case G726_ENCODING_ALAW:
                    return tandem_adjust_alaw(sr, se, y, code, 4, qtab_726_24, 7);
                case G726_ENCODING_ULAW:
                    return tandem_adjust_ulaw(sr, se, y, code, 4, qtab_726_24, 7);
            }
            return (short)(sr << 2);
        }
    }
    /*- End of function --------------------------------------------------------*/

    private static byte g726_32_encoder(g726_state_t s, short amp)
    {
        unchecked
        {
            short sei;
            short sezi;
            short se;
            short d;
            short sr;
            short dqsez;
            short dq;
            short i;
            int y;

            sezi = predictor_zero(s);
            sei = (short)(sezi + predictor_pole(s));
            se = (short)(sei >> 1);
            d = (short)(amp - se);
            y = step_size(s);
            i = quantize(d, y, qtab_726_32, 15);
            dq = reconstruct(i & 8, g726_32_dqlntab[i], y);
            sr = (short)((dq < 0) ? (se - (dq & 0x3FFF)) : (se + dq));
            dqsez = (short)(sr + (sezi >> 1) - se);
            update(s, y, g726_32_witab[i], g726_32_fitab[i], dq, sr, dqsez);
            return (byte)i;
        }
    }
    /*- End of function --------------------------------------------------------*/

    private static short g726_32_decoder(g726_state_t s, byte code)
    {
        unchecked
        {
            short sezi;
            short sei;
            short se;
            short sr;
            short dq;
            short dqsez;
            int y;

            code &= 0x0F;
            sezi = predictor_zero(s);
            sei = (short)(sezi + predictor_pole(s));
            y = step_size(s);
            dq = reconstruct(code & 8, g726_32_dqlntab[code], y);
            se = (short)(sei >> 1);
            sr = (short)((dq < 0) ? (se - (dq & 0x3FFF)) : (se + dq));
            dqsez = (short)(sr + (sezi >> 1) - se);
            update(s, y, g726_32_witab[code], g726_32_fitab[code], dq, sr, dqsez);
            switch (s.ext_coding)
            {
                case G726_ENCODING_ALAW:
                    return tandem_adjust_alaw(sr, se, y, code, 8, qtab_726_32, 15);
                case G726_ENCODING_ULAW:
                    return tandem_adjust_ulaw(sr, se, y, code, 8, qtab_726_32, 15);
            }
            return (short)(sr << 2);
        }
    }
    /*- End of function --------------------------------------------------------*/

    private static byte g726_40_encoder(g726_state_t s, short amp)
    {
        unchecked
        {
            short sei;
            short sezi;
            short se;
            short d;
            short sr;
            short dqsez;
            short dq;
            short i;
            int y;

            sezi = predictor_zero(s);
            sei = (short)(sezi + predictor_pole(s));
            se = (short)(sei >> 1);
            d = (short)(amp - se);
            y = step_size(s);
            i = quantize(d, y, qtab_726_40, 31);
            dq = reconstruct(i & 0x10, g726_40_dqlntab[i], y);
            sr = (short)((dq < 0) ? (se - (dq & 0x7FFF)) : (se + dq));
            dqsez = (short)(sr + (sezi >> 1) - se);
            update(s, y, g726_40_witab[i], g726_40_fitab[i], dq, sr, dqsez);
            return (byte)i;
        }
    }
    /*- End of function --------------------------------------------------------*/

    private static short g726_40_decoder(g726_state_t s, byte code)
    {
        unchecked
        {
            short sezi;
            short sei;
            short se;
            short sr;
            short dq;
            short dqsez;
            int y;

            code &= 0x1F;
            sezi = predictor_zero(s);
            sei = (short)(sezi + predictor_pole(s));
            y = step_size(s);
            dq = reconstruct(code & 0x10, g726_40_dqlntab[code], y);
            se = (short)(sei >> 1);
            sr = (short)((dq < 0) ? (se - (dq & 0x7FFF)) : (se + dq));
            dqsez = (short)(sr + (sezi >> 1) - se);
            update(s, y, g726_40_witab[code], g726_40_fitab[code], dq, sr, dqsez);
            switch (s.ext_coding)
            {
                case G726_ENCODING_ALAW:
                    return tandem_adjust_alaw(sr, se, y, code, 0x10, qtab_726_40, 31);
                case G726_ENCODING_ULAW:
                    return tandem_adjust_ulaw(sr, se, y, code, 0x10, qtab_726_40, 31);
            }
            return (short)(sr << 2);
        }
    }
    /*- End of function --------------------------------------------------------*/

    public static int g726_decode(g726_state_t s, short[] amp, byte[] g726_data, int g726_bytes)
    {
        unchecked
        {
            int i;
            int samples;
            byte code;
            int sl;
            Span<byte> amp_bytes = MemoryMarshal.AsBytes(amp.AsSpan());

            for (samples = i = 0; ; )
            {
                if (s.packing != G726_PACKING_NONE)
                {
                    if (s.packing != G726_PACKING_LEFT)
                    {
                        if (s.bs.Residue < s.bits_per_sample)
                        {
                            if (i >= g726_bytes)
                                break;
                            s.bs.BitBuffer |= (uint)(g726_data[i++] << s.bs.Residue);
                            s.bs.Residue += 8;
                        }
                        code = (byte)(s.bs.BitBuffer & (uint)((1 << s.bits_per_sample) - 1));
                        s.bs.BitBuffer >>= s.bits_per_sample;
                    }
                    else
                    {
                        if (s.bs.Residue < s.bits_per_sample)
                        {
                            if (i >= g726_bytes)
                                break;
                            s.bs.BitBuffer = (s.bs.BitBuffer << 8) | g726_data[i++];
                            s.bs.Residue += 8;
                        }
                        code = (byte)((s.bs.BitBuffer >> (s.bs.Residue - s.bits_per_sample)) & (uint)((1 << s.bits_per_sample) - 1));
                    }
                    s.bs.Residue -= s.bits_per_sample;
                }
                else
                {
                    if (i >= g726_bytes)
                        break;
                    code = g726_data[i++];
                }
                sl = s.dec_func(s, code);
                if (s.ext_coding != G726_ENCODING_LINEAR)
                    amp_bytes[samples++] = (byte)sl;
                else
                    amp[samples++] = (short)sl;
            }
            return samples;
        }
    }
    /*- End of function --------------------------------------------------------*/

    public static int g726_encode(g726_state_t s, byte[] g726_data, short[] amp, int len)
    {
        unchecked
        {
            int i;
            int g726_bytes;
            short sl;
            byte code;
            ReadOnlySpan<byte> amp_bytes = MemoryMarshal.AsBytes(amp.AsSpan());

            for (g726_bytes = i = 0; i < len; i++)
            {
                switch (s.ext_coding)
                {
                    case G726_ENCODING_ALAW:
                        sl = (short)(alaw_to_linear(amp_bytes[i]) >> 2);
                        break;
                    case G726_ENCODING_ULAW:
                        sl = (short)(ulaw_to_linear(amp_bytes[i]) >> 2);
                        break;
                    default:
                        sl = (short)(amp[i] >> 2);
                        break;
                }
                code = s.enc_func(s, sl);
                if (s.packing != G726_PACKING_NONE)
                {
                    if (s.packing != G726_PACKING_LEFT)
                    {
                        s.bs.BitBuffer |= (uint)(code << s.bs.Residue);
                        s.bs.Residue += s.bits_per_sample;
                        if (s.bs.Residue >= 8)
                        {
                            g726_data[g726_bytes++] = (byte)(s.bs.BitBuffer & 0xFF);
                            s.bs.BitBuffer >>= 8;
                            s.bs.Residue -= 8;
                        }
                    }
                    else
                    {
                        s.bs.BitBuffer = (s.bs.BitBuffer << s.bits_per_sample) | code;
                        s.bs.Residue += s.bits_per_sample;
                        if (s.bs.Residue >= 8)
                        {
                            g726_data[g726_bytes++] = (byte)((s.bs.BitBuffer >> (s.bs.Residue - 8)) & 0xFF);
                            s.bs.Residue -= 8;
                        }
                    }
                }
                else
                {
                    g726_data[g726_bytes++] = code;
                }
            }
            return g726_bytes;
        }
    }
    /*- End of function --------------------------------------------------------*/

    public static g726_state_t? g726_init(g726_state_t? s, int bit_rate, int ext_coding, int packing)
    {
        int i;

        if (bit_rate != 16000 && bit_rate != 24000 && bit_rate != 32000 && bit_rate != 40000)
            return null;
        if (s is null)
        {
            try
            {
                s = new g726_state_t();
            }
            catch (OutOfMemoryException)
            {
                return null;
            }
        }
        s.yl = 34816;
        s.yu = 544;
        s.dms = 0;
        s.dml = 0;
        s.ap = 0;
        s.rate = bit_rate;
        s.ext_coding = ext_coding;
        s.packing = packing;
        for (i = 0; i < 2; i++)
        {
            s.a[i] = 0;
            s.pk[i] = 0;
            s.sr[i] = 32;
        }
        for (i = 0; i < 6; i++)
        {
            s.b[i] = 0;
            s.dq[i] = 32;
        }
        s.td = 0;
        switch (bit_rate)
        {
            case 16000:
                s.enc_func = g726_16_encoder;
                s.dec_func = g726_16_decoder;
                s.bits_per_sample = 2;
                break;
            case 24000:
                s.enc_func = g726_24_encoder;
                s.dec_func = g726_24_decoder;
                s.bits_per_sample = 3;
                break;
            case 32000:
            default:
                s.enc_func = g726_32_encoder;
                s.dec_func = g726_32_decoder;
                s.bits_per_sample = 4;
                break;
            case 40000:
                s.enc_func = g726_40_encoder;
                s.dec_func = g726_40_decoder;
                s.bits_per_sample = 5;
                break;
        }
        bitstream_init(s.bs, (s.packing != G726_PACKING_LEFT) ? 1 : 0);
        return s;
    }
    /*- End of function --------------------------------------------------------*/

    public static int g726_release(g726_state_t s)
    {
        return 0;
    }
    /*- End of function --------------------------------------------------------*/

    public static int g726_free(g726_state_t? s)
    {
        return 0;
    }
    /*- End of function --------------------------------------------------------*/

}
/*- End of file ------------------------------------------------------------*/
