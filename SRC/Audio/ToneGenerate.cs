/*
 * TKFaxEngineFX - a series of DSP components for telephony
 *
 * tone_generate.cs - direct C# conversion of tone_generate.h and tone_generate.c
 *
 * Written by Steve Underwood <steveu@coppice.org>
 *
 * Copyright (C) 2001 Steve Underwood
 *
 * This file preserves the GNU Lesser General Public License version 2.1
 * terms of the original source files.
 */

#nullable enable

using static global::TKFaxEngine.Audio.Dds;
using static global::TKFaxEngine.Audio.Telephony;
using static global::TKFaxEngine.FastConvert;

namespace TKFaxEngine.Audio;

public struct tone_gen_tone_descriptor_t
{
    public int phase_rate;
    public float gain;
}

public sealed class tone_gen_descriptor_t
{
    public tone_gen_tone_descriptor_t[] tone = new tone_gen_tone_descriptor_t[4];
    public int[] duration = new int[4];
    public int repeat;
}

public sealed class tone_gen_state_t
{
    public tone_gen_tone_descriptor_t[] tone = new tone_gen_tone_descriptor_t[4];

    public uint[] phase = new uint[4];
    public int[] duration = new int[4];
    public int repeat;

    public int current_section;
    public int current_position;
}

public static class tone_generate
{
    public static tone_gen_descriptor_t? tone_gen_descriptor_init(tone_gen_descriptor_t? s,
                                                                  int f1,
                                                                  int l1,
                                                                  int f2,
                                                                  int l2,
                                                                  int d1,
                                                                  int d2,
                                                                  int d3,
                                                                  int d4,
                                                                  int repeat)
    {
        if (s == null)
            s = new tone_gen_descriptor_t();

        Array.Clear(s.tone);
        Array.Clear(s.duration);
        s.repeat = 0;

        if (f1 != 0)
        {
            s.tone[0].phase_rate = dds_phase_ratef((float)f1);
            if (f2 < 0)
                s.tone[0].phase_rate = -s.tone[0].phase_rate;
            s.tone[0].gain = dds_scaling_dbm0f((float)l1);
        }
        if (f2 != 0)
        {
            s.tone[1].phase_rate = dds_phase_ratef((float)Math.Abs(f2));
            s.tone[1].gain = (f2 < 0) ? (float)l2 / 100.0f : dds_scaling_dbm0f((float)l2);
        }

        s.duration[0] = d1 * SAMPLE_RATE / 1000;
        s.duration[1] = d2 * SAMPLE_RATE / 1000;
        s.duration[2] = d3 * SAMPLE_RATE / 1000;
        s.duration[3] = d4 * SAMPLE_RATE / 1000;

        s.repeat = repeat;

        return s;
    }

    public static void tone_gen_descriptor_free(tone_gen_descriptor_t? s)
    {
    }

    public static int tone_gen(tone_gen_state_t s, Span<short> amp, int max_samples)
    {
        int samples;
        int limit;
        float xamp;
        int i;

        if (s.current_section < 0)
            return 0;
        for (samples = 0; samples < max_samples;)
        {
            limit = samples + s.duration[s.current_section] - s.current_position;
            if (limit > max_samples)
                limit = max_samples;
            s.current_position += limit - samples;
            if ((s.current_section & 1) != 0)
            {
                for (; samples < limit; samples++)
                    amp[samples] = 0;
            }
            else
            {
                if (s.tone[0].phase_rate < 0)
                {
                    for (; samples < limit; samples++)
                    {
                        xamp = dds_modf(ref s.phase[0], -s.tone[0].phase_rate, s.tone[0].gain, 0)
                             * (1.0f + dds_modf(ref s.phase[1], s.tone[1].phase_rate, s.tone[1].gain, 0));
                        amp[samples] = unchecked((short)lfastrintf(xamp));
                    }
                }
                else
                {
                    for (; samples < limit; samples++)
                    {
                        xamp = 0.0f;
                        for (i = 0; i < 4; i++)
                        {
                            if (s.tone[i].phase_rate == 0)
                                break;
                            xamp += dds_modf(ref s.phase[i], s.tone[i].phase_rate, s.tone[i].gain, 0);
                        }
                        amp[samples] = unchecked((short)lfastrintf(xamp));
                    }
                }
            }
            if (s.current_position >= s.duration[s.current_section])
            {
                s.current_position = 0;
                if (++s.current_section > 3 || s.duration[s.current_section] == 0)
                {
                    if (s.repeat == 0)
                    {
                        s.current_section = -1;
                        break;
                    }
                    s.current_section = 0;
                }
            }
        }
        return samples;
    }

    public static tone_gen_state_t? tone_gen_init(tone_gen_state_t? s, tone_gen_descriptor_t t)
    {
        int i;

        if (s == null)
            s = new tone_gen_state_t();

        Array.Clear(s.tone);
        Array.Clear(s.phase);
        Array.Clear(s.duration);
        s.repeat = 0;
        s.current_section = 0;
        s.current_position = 0;

        for (i = 0; i < 4; i++)
        {
            s.tone[i] = t.tone[i];
            s.phase[i] = 0;
        }

        for (i = 0; i < 4; i++)
            s.duration[i] = t.duration[i];
        s.repeat = t.repeat;

        s.current_section = 0;
        s.current_position = 0;
        return s;
    }

    public static int tone_gen_release(tone_gen_state_t s)
    {
        return 0;
    }

    public static int tone_gen_free(tone_gen_state_t? s)
    {
        return 0;
    }
}
