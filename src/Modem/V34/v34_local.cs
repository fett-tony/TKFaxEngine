/*
 * TKFaxEngine - direct C# conversion of TKFaxEngineFX/spanDSP V.34.
 *
 * v34_local.cs - declarations from v34_local.h plus the direct managed
 * implementations of the bitstream and circular-vector primitives used by
 * v34rx.c and v34tx.c. No compatibility facades or delegated replacement
 * algorithms are present in this module.
 */

#nullable enable

namespace TKFaxEngine.Modem.V34;

public static partial class v34 {
    internal static void bitstream_init(bitstream_state_t s, bool lsb_first) {
        s.lsb_first = lsb_first;
        s.residual = 0;
        s.residual_bits = 0;
    }

    internal static void bitstream_put(bitstream_state_t s, byte[] output, ref int offset, long value, int bits) {
        ulong mask = bits == 32 ? uint.MaxValue : ((1UL << bits) - 1UL);
        ulong v = unchecked((ulong)value) & mask;

        if (s.lsb_first) {
            s.residual |= v << s.residual_bits;
            s.residual_bits += bits;
            while (s.residual_bits >= 8) {
                output[offset++] = unchecked((byte)s.residual);
                s.residual >>= 8;
                s.residual_bits -= 8;
            }
        } else {
            s.residual = (s.residual << bits) | v;
            s.residual_bits += bits;
            while (s.residual_bits >= 8) {
                s.residual_bits -= 8;
                output[offset++] = unchecked((byte)(s.residual >> s.residual_bits));
                if (s.residual_bits == 0)
                    s.residual = 0;
                else
                    s.residual &= (1UL << s.residual_bits) - 1UL;
            }
        }
    }

    internal static uint bitstream_get(bitstream_state_t s, byte[] input, ref int offset, int bits) {
        ulong mask = bits == 32 ? uint.MaxValue : ((1UL << bits) - 1UL);
        if (s.lsb_first) {
            while (s.residual_bits < bits) {
                s.residual |= (ulong)input[offset++] << s.residual_bits;
                s.residual_bits += 8;
            }
            uint lsb_result = unchecked((uint)(s.residual & mask));
            s.residual >>= bits;
            s.residual_bits -= bits;
            return lsb_result;
        }

        while (s.residual_bits < bits) {
            s.residual = (s.residual << 8) | input[offset++];
            s.residual_bits += 8;
        }
        s.residual_bits -= bits;
        uint msb_result = unchecked((uint)((s.residual >> s.residual_bits) & mask));
        if (s.residual_bits == 0)
            s.residual = 0;
        else
            s.residual &= (1UL << s.residual_bits) - 1UL;
        return msb_result;
    }

    internal static void bitstream_emit(bitstream_state_t s, byte[] output, int offset) {
        if (s.residual_bits <= 0)
            return;
        ulong mask = (1UL << s.residual_bits) - 1UL;
        output[offset] = s.lsb_first
            ? unchecked((byte)(s.residual & mask))
            : unchecked((byte)((s.residual & mask) << (8 - s.residual_bits)));
    }

    internal static void bitstream_flush(bitstream_state_t s, byte[] output, ref int offset) {
        if (s.residual_bits > 0) {
            bitstream_emit(s, output, offset);
            offset++;
        }
        s.residual = 0;
        s.residual_bits = 0;
    }

    internal static float vec_circular_dot_prodf(float[] x, int x_step, float[] h, int length) {
        float z = 0.0f;
        int p = x_step;
        for (int i = 0; i < length; i++) {
            z += x[p] * h[i];
            if (++p >= length)
                p = 0;
        }
        return z;
    }

    internal static float vec_circular_dot_prodf(float[] x, int x_step, float[,] h, int h_row, int length) {
        float z = 0.0f;
        int p = x_step;
        for (int i = 0; i < length; i++) {
            z += x[p] * h[h_row, i];
            if (++p >= length)
                p = 0;
        }
        return z;
    }
}
