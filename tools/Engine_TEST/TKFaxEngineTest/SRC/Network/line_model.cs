/*
 * TKFaxEngine - managed C# port of line_model.c and line_model.h.
 *
 * Original implementation written by Steve Underwood.
 * The original SpanDSP simulation order, filter traversal, noise split,
 * codec impairment, eight-sample bulk delay and hybrid calculations are retained.
 */

#nullable enable

using TKFaxEngine.Audio;
using static TKFaxEngine.Audio.g711;
using static TKFaxEngine.Audio.g726;
using static TKFaxEngine.Audio.tone_generate;
using static TKFaxEngine.Saturated;

namespace TKFaxEngineTest.Network.Test;

public sealed class one_way_line_model_state_t : IDisposable {
    internal LineModelCodecMungeState Munge = null!;
    public float[] near_filter = Array.Empty<float>();
    public int near_filter_len;
    public readonly float[] near_buf = new float[line_model.LINE_FILTER_SIZE];
    public int near_buf_ptr;
    public Awgn near_noise = null!;
    public int bulk_delay;
    public int bulk_delay_ptr;
    public readonly short[] bulk_delay_buf = new short[8000];
    public float[] far_filter = Array.Empty<float>();
    public int far_filter_len;
    public readonly float[] far_buf = new float[line_model.LINE_FILTER_SIZE];
    public int far_buf_ptr;
    public Awgn far_noise = null!;
    public float near_cpe_hybrid_echo;
    public float near_co_hybrid_echo;
    public float far_cpe_hybrid_echo;
    public float far_co_hybrid_echo;
    public float dc_offset;
    public int mains_interference;
    internal tone_gen_state_t? mains_tone;
    internal bool Disposed;

    public void Dispose() {
        if (Disposed) return;
        Munge?.Dispose();
        Disposed = true;
        GC.SuppressFinalize(this);
    }
}

public sealed class both_ways_line_model_state_t : IDisposable {
    public readonly one_way_line_model_state_t line1 = new();
    public readonly one_way_line_model_state_t line2 = new();
    public float fout1;
    public float fout2;
    internal bool Disposed;

    public void Dispose() {
        if (Disposed) return;
        line1.Dispose();
        line2.Dispose();
        Disposed = true;
        GC.SuppressFinalize(this);
    }
}

internal sealed class LineModelCodecMungeState : IDisposable {
    internal const int MUNGE_CODEC_NONE = 0;
    internal const int MUNGE_CODEC_ALAW = 1;
    internal const int MUNGE_CODEC_ULAW = 2;
    internal const int MUNGE_CODEC_G726_40K = 3;
    internal const int MUNGE_CODEC_G726_32K = 4;
    internal const int MUNGE_CODEC_G726_24K = 5;
    internal const int MUNGE_CODEC_G726_16K = 6;

    private int _mungingCodec;
    private g726_state_t? _encoder;
    private g726_state_t? _decoder;
    private readonly int _rbsPattern;
    private int _sequence;
    private bool _disposed;

    internal LineModelCodecMungeState(int codec, int info) {
        int rate = codec switch {
            MUNGE_CODEC_G726_40K => 40000,
            MUNGE_CODEC_G726_32K => 32000,
            MUNGE_CODEC_G726_24K => 24000,
            MUNGE_CODEC_G726_16K => 16000,
            _ => 0
        };

        if (rate != 0) {
            _encoder = g726_init(null, rate, G726_ENCODING_LINEAR, G726_PACKING_NONE);
            _decoder = g726_init(null, rate, G726_ENCODING_LINEAR, G726_PACKING_NONE);
            if (_encoder is null || _decoder is null)
                throw new InvalidOperationException("g726_init failed.");

            // SpanDSP's test utility deliberately normalises every G.726 rate
            // to this switch discriminator after constructing the rate-specific states.
            _mungingCodec = MUNGE_CODEC_G726_32K;
        } else {
            _mungingCodec = codec;
        }

        _sequence = 0;
        _rbsPattern = info;
    }

    internal void Process(short[] amplitude, int offset, int length) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(amplitude);
        if (offset < 0 || length < 0 || offset > amplitude.Length - length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        switch (_mungingCodec) {
            case MUNGE_CODEC_NONE:
                return;

            case MUNGE_CODEC_ALAW:
                for (int i = 0; i < length; i++) {
                    int p = offset + i;
                    byte law = linear_to_alaw(amplitude[p]);
                    amplitude[p] = alaw_to_linear(law);
                }
                return;

            case MUNGE_CODEC_ULAW:
                for (int i = 0; i < length; i++) {
                    int p = offset + i;
                    byte law = linear_to_ulaw(amplitude[p]);
                    if ((_rbsPattern & (1 << _sequence)) != 0)
                        law &= 0xFE;
                    amplitude[p] = ulaw_to_linear(law);
                }
                return;

            case MUNGE_CODEC_G726_32K:
                if (_encoder is null || _decoder is null)
                    throw new InvalidOperationException("G.726 codec state is unavailable.");

                var input = new short[160];
                var output = new short[160];
                var adpcm = new byte[160];
                for (int i = 0; i < length;) {
                    int count = Math.Min(160, length - i);
                    Array.Copy(amplitude, offset + i, input, 0, count);
                    int bytes = g726_encode(_encoder, adpcm, input, count);
                    int decoded = g726_decode(_decoder, output, adpcm, bytes);
                    if (decoded < count)
                        throw new InvalidOperationException("g726_decode returned fewer samples than encoded.");
                    Array.Copy(output, 0, amplitude, offset + i, count);
                    i += count;
                }
                return;
        }
    }

    public void Dispose() {
        if (_disposed) return;
        if (_encoder is not null) g726_free(_encoder);
        if (_decoder is not null) g726_free(_decoder);
        _encoder = null;
        _decoder = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

public static class line_model {
    public const int LINE_FILTER_SIZE = 129;

    private static readonly float[] null_line_model = BuildNullLineModel();

    public static readonly float[][] line_models = new float[][]
    {
        null_line_model,
        global::TKFaxEngineTest.Network.Test.line_models.proakis_line_model,
        global::TKFaxEngineTest.Network.Test.line_models.ad_1_edd_1_model,
        global::TKFaxEngineTest.Network.Test.line_models.ad_1_edd_2_model,
        global::TKFaxEngineTest.Network.Test.line_models.ad_1_edd_3_model,
        global::TKFaxEngineTest.Network.Test.line_models.ad_5_edd_1_model,
        global::TKFaxEngineTest.Network.Test.line_models.ad_5_edd_2_model,
        global::TKFaxEngineTest.Network.Test.line_models.ad_5_edd_3_model,
        global::TKFaxEngineTest.Network.Test.line_models.ad_6_edd_1_model,
        global::TKFaxEngineTest.Network.Test.line_models.ad_6_edd_2_model,
        global::TKFaxEngineTest.Network.Test.line_models.ad_6_edd_3_model,
        global::TKFaxEngineTest.Network.Test.line_models.ad_7_edd_1_model,
        global::TKFaxEngineTest.Network.Test.line_models.ad_7_edd_2_model,
        global::TKFaxEngineTest.Network.Test.line_models.ad_7_edd_3_model,
        global::TKFaxEngineTest.Network.Test.line_models.ad_8_edd_1_model,
        global::TKFaxEngineTest.Network.Test.line_models.ad_8_edd_2_model,
        global::TKFaxEngineTest.Network.Test.line_models.ad_8_edd_3_model,
        global::TKFaxEngineTest.Network.Test.line_models.ad_9_edd_1_model,
        global::TKFaxEngineTest.Network.Test.line_models.ad_9_edd_2_model,
        global::TKFaxEngineTest.Network.Test.line_models.ad_9_edd_3_model
    };

    private static float[] BuildNullLineModel() {
        var result = new float[LINE_FILTER_SIZE];
        result[LINE_FILTER_SIZE - 1] = 1.0f;
        return result;
    }

    private static float calc_near_line_filter(one_way_line_model_state_t s, float value) {
        int p = s.near_buf_ptr;
        s.near_buf[p] = value;
        if (++p == s.near_filter_len) p = 0;
        s.near_buf_ptr = p;

        float sum = 0.0f;
        for (int j = 0; j < s.near_filter_len; j++) {
            sum += s.near_filter[j] * s.near_buf[p];
            if (++p >= s.near_filter_len) p = 0;
        }
        sum += s.near_noise.NextSample();
        return sum;
    }

    private static float calc_far_line_filter(one_way_line_model_state_t s, float value) {
        int p = s.far_buf_ptr;
        s.far_buf[p] = value;
        if (++p == s.far_filter_len) p = 0;
        s.far_buf_ptr = p;

        float sum = 0.0f;
        for (int j = 0; j < s.far_filter_len; j++) {
            sum += s.far_filter[j] * s.far_buf[p];
            if (++p >= s.far_filter_len) p = 0;
        }
        sum += s.far_noise.NextSample();
        return sum;
    }

    public static void one_way_line_model(
        one_way_line_model_state_t s,
        short[] output,
        short[] input,
        int samples) => one_way_line_model(s, output, 0, input, 0, samples);

    public static void one_way_line_model(
        one_way_line_model_state_t s,
        short[] output,
        int outputOffset,
        short[] input,
        int inputOffset,
        int samples) {
        ValidateBuffers(s, output, outputOffset, input, inputOffset, samples);
        var amp = new short[1];

        for (int i = 0; i < samples; i++) {
            float inputSample = input[inputOffset + i];
            float outSample = calc_near_line_filter(s, inputSample);

            amp[0] = CInt16(outSample);
            s.Munge.Process(amp, 0, 1);
            outSample = amp[0];

            float delayed = s.bulk_delay_buf[s.bulk_delay_ptr];
            s.bulk_delay_buf[s.bulk_delay_ptr] = CInt16(outSample);
            outSample = delayed;
            if (++s.bulk_delay_ptr >= s.bulk_delay) s.bulk_delay_ptr = 0;

            outSample = calc_far_line_filter(s, outSample);
            if (s.mains_interference != 0) {
                if (s.mains_tone is null)
                    throw new InvalidOperationException("Mains tone state is unavailable.");
                tone_gen(s.mains_tone, amp, 1);
                outSample += amp[0];
            }

            output[outputOffset + i] = CInt16(outSample + s.dc_offset);
        }
    }

    public static void one_way_line_model_set_dc(one_way_line_model_state_t s, float dc) {
        ArgumentNullException.ThrowIfNull(s);
        s.dc_offset = dc;
    }

    public static void one_way_line_model_set_mains_pickup(one_way_line_model_state_t s, int frequency, float level) {
        ArgumentNullException.ThrowIfNull(s);
        if (frequency != 0) {
            tone_gen_descriptor_t descriptor = tone_gen_descriptor_init(
                null, frequency, (int)(level - 10.0f), frequency * 3, (int)level,
                1, 0, 0, 0, 1)!;
            s.mains_tone = tone_gen_init(s.mains_tone, descriptor);
            tone_gen_descriptor_free(descriptor);
        }
        s.mains_interference = frequency;
    }

    public static void both_ways_line_model(
        both_ways_line_model_state_t s,
        short[] output1,
        short[] input1,
        short[] output2,
        short[] input2,
        int samples) => both_ways_line_model(s, output1, 0, input1, 0, output2, 0, input2, 0, samples);

    public static void both_ways_line_model(
        both_ways_line_model_state_t s,
        short[] output1, int output1Offset,
        short[] input1, int input1Offset,
        short[] output2, int output2Offset,
        short[] input2, int input2Offset,
        int samples) {
        ArgumentNullException.ThrowIfNull(s);
        ValidateArray(output1, output1Offset, samples, nameof(output1));
        ValidateArray(input1, input1Offset, samples, nameof(input1));
        ValidateArray(output2, output2Offset, samples, nameof(output2));
        ValidateArray(input2, input2Offset, samples, nameof(input2));
        if (s.Disposed) throw new ObjectDisposedException(nameof(both_ways_line_model_state_t));

        var amp = new short[1];
        for (int i = 0; i < samples; i++) {
            float in1 = input1[input1Offset + i];
            float in2 = input2[input2Offset + i];

            float tmp1 = in1 + s.fout2 * s.line1.near_co_hybrid_echo;
            float tmp2 = in2 + s.fout1 * s.line2.near_co_hybrid_echo;
            s.fout1 = calc_near_line_filter(s.line1, tmp1);
            s.fout2 = calc_near_line_filter(s.line2, tmp2);

            amp[0] = CInt16(s.fout1);
            s.line1.Munge.Process(amp, 0, 1);
            s.fout1 = amp[0];
            amp[0] = CInt16(s.fout2);
            s.line2.Munge.Process(amp, 0, 1);
            s.fout2 = amp[0];

            float out1 = s.line1.bulk_delay_buf[s.line1.bulk_delay_ptr];
            s.line1.bulk_delay_buf[s.line1.bulk_delay_ptr] = CInt16(s.fout1);
            s.fout1 = out1;
            if (++s.line1.bulk_delay_ptr >= s.line1.bulk_delay) s.line1.bulk_delay_ptr = 0;

            float out2 = s.line2.bulk_delay_buf[s.line2.bulk_delay_ptr];
            s.line2.bulk_delay_buf[s.line2.bulk_delay_ptr] = CInt16(s.fout2);
            s.fout2 = out2;
            if (++s.line2.bulk_delay_ptr >= s.line2.bulk_delay) s.line2.bulk_delay_ptr = 0;

            out1 += in2 * s.line1.far_cpe_hybrid_echo;
            out2 += in1 * s.line2.far_cpe_hybrid_echo;
            out1 = calc_far_line_filter(s.line1, out1);
            out2 = calc_far_line_filter(s.line2, out2);

            output1[output1Offset + i] = fsaturate(out1 + s.line1.dc_offset);
            output2[output2Offset + i] = fsaturate(out2 + s.line2.dc_offset);
        }
    }

    public static void both_ways_line_model_set_dc(both_ways_line_model_state_t s, float dc1, float dc2) {
        ArgumentNullException.ThrowIfNull(s);
        s.line1.dc_offset = dc1;
        s.line2.dc_offset = dc2;
    }

    public static void both_ways_line_model_set_mains_pickup(
        both_ways_line_model_state_t s, int frequency, float level1, float level2) {
        ArgumentNullException.ThrowIfNull(s);
        if (frequency != 0) {
            tone_gen_descriptor_t descriptor1 = tone_gen_descriptor_init(
                null, frequency, (int)(level1 - 10.0f), frequency * 3, (int)level1,
                1, 0, 0, 0, 1)!;
            tone_gen_descriptor_t descriptor2 = tone_gen_descriptor_init(
                null, frequency, (int)(level2 - 10.0f), frequency * 3, (int)level2,
                1, 0, 0, 0, 1)!;
            s.line1.mains_tone = tone_gen_init(s.line1.mains_tone, descriptor1);
            s.line2.mains_tone = tone_gen_init(s.line2.mains_tone, descriptor2);
            tone_gen_descriptor_free(descriptor1);
            tone_gen_descriptor_free(descriptor2);
        }
        s.line1.mains_interference = frequency;
        s.line2.mains_interference = frequency;
    }

    public static one_way_line_model_state_t one_way_line_model_init(
        int model, float noise, int codec, int rbs_pattern) {
        float[] coefficients = line_models[model];
        var s = new one_way_line_model_state_t {
            bulk_delay = 8,
            bulk_delay_ptr = 0,
            Munge = new LineModelCodecMungeState(codec, rbs_pattern),
            near_filter = coefficients,
            near_filter_len = LINE_FILTER_SIZE,
            far_filter = coefficients,
            far_filter_len = LINE_FILTER_SIZE,
            near_noise = new Awgn(1234567, noise - 3.02f),
            far_noise = new Awgn(1234567, noise - 3.02f),
            dc_offset = 0.0f,
            mains_interference = 0
        };
        return s;
    }

    public static int one_way_line_model_free(one_way_line_model_state_t? s) {
        s?.Dispose();
        return 0;
    }

    public static both_ways_line_model_state_t both_ways_line_model_init(
        int model1, float noise1, float echo_level_cpe1, float echo_level_co1,
        int model2, float noise2, float echo_level_cpe2, float echo_level_co2,
        int codec, int rbs_pattern) {
        var s = new both_ways_line_model_state_t();
        InitializeDirection(s.line1, line_models[model1], noise1, 1234567, codec, rbs_pattern);
        InitializeDirection(s.line2, line_models[model2], noise2, 7654321, codec, rbs_pattern);

        s.line1.near_co_hybrid_echo = MathF.Pow(10.0f, echo_level_co1 / 20.0f);
        s.line2.near_co_hybrid_echo = MathF.Pow(10.0f, echo_level_co2 / 20.0f);
        s.line1.near_cpe_hybrid_echo = MathF.Pow(10.0f, echo_level_cpe1 / 20.0f);
        s.line2.near_cpe_hybrid_echo = MathF.Pow(10.0f, echo_level_cpe2 / 20.0f);
        return s;
    }

    private static void InitializeDirection(
        one_way_line_model_state_t s, float[] coefficients, float noise, int seed,
        int codec, int rbsPattern) {
        s.Munge = new LineModelCodecMungeState(codec, rbsPattern);
        s.bulk_delay = 8;
        s.bulk_delay_ptr = 0;
        s.near_filter = coefficients;
        s.near_filter_len = LINE_FILTER_SIZE;
        s.far_filter = coefficients;
        s.far_filter_len = LINE_FILTER_SIZE;
        s.near_noise = new Awgn(seed, noise - 3.02f);
        s.far_noise = new Awgn(seed, noise - 3.02f);
        s.dc_offset = 0.0f;
        s.mains_interference = 0;
    }

    public static int both_ways_line_model_free(both_ways_line_model_state_t? s) {
        s?.Dispose();
        return 0;
    }

    private static void ValidateBuffers(
        one_way_line_model_state_t s, short[] output, int outputOffset,
        short[] input, int inputOffset, int samples) {
        ArgumentNullException.ThrowIfNull(s);
        if (s.Disposed) throw new ObjectDisposedException(nameof(one_way_line_model_state_t));
        ValidateArray(output, outputOffset, samples, nameof(output));
        ValidateArray(input, inputOffset, samples, nameof(input));
    }

    private static void ValidateArray(short[] value, int offset, int length, string name) {
        ArgumentNullException.ThrowIfNull(value, name);
        if (offset < 0 || length < 0 || offset > value.Length - length)
            throw new ArgumentOutOfRangeException(name);
    }

    private static short CInt16(float value) => unchecked((short)(int)value);
}
