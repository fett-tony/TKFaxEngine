/*
 * Managed C# port of TKFX_fftw_compat.h.
 *
 * Implements the same radix-2 iterative FFT used by the supplied C
 * compatibility header. Forward transforms are unscaled and use a negative
 * phase; backward transforms are unscaled and use a positive phase.
 */

#nullable enable

namespace TKFaxEngineTest.System;

public struct fftw_complex {
    public double re;
    public double im;

    public fftw_complex(double real, double imaginary) {
        re = real;
        im = imaginary;
    }
}

public sealed class fftw_plan {
    public int n;
    public int direction;
}

public static class FftwCompat {
    public const int FFTW_FORWARD = -1;
    public const int FFTW_BACKWARD = 1;
    public const int FFTW_ESTIMATE = 0;

    private const double M_PI = 3.14159265358979323846;

    public static fftw_plan? fftw_create_plan(int n, int direction, int flags) {
        _ = flags;
        if (n <= 0 || (n & (n - 1)) != 0)
            return null;

        return new fftw_plan {
            n = n,
            direction = direction
        };
    }

    public static uint tkfx_fft_reverse_bits(uint value, uint bits) {
        uint reversed = 0;
        for (uint i = 0; i < bits; ++i) {
            reversed = (reversed << 1) | (value & 1U);
            value >>= 1;
        }
        return reversed;
    }

    public static void fftw_one(
        fftw_plan? plan,
        fftw_complex[]? input,
        fftw_complex[]? output) {
        if (plan is null || input is null || output is null)
            return;

        int n = plan.n;
        if (input.Length < n || output.Length < n)
            throw new ArgumentException("FFT input and output buffers must contain plan.n elements.");

        uint bits = 0;
        while ((1U << (int)bits) < (uint)n)
            bits++;

        for (int i = 0; i < n; ++i)
            output[(int)tkfx_fft_reverse_bits((uint)i, bits)] = input[i];

        double sign = plan.direction == FFTW_BACKWARD ? 1.0 : -1.0;
        for (int length = 2; length <= n; length <<= 1) {
            int half = length >> 1;
            double angleStep = sign * 2.0 * M_PI / length;
            for (int blockBase = 0; blockBase < n; blockBase += length) {
                for (int j = 0; j < half; ++j) {
                    double angle = angleStep * j;
                    double wr = Math.Cos(angle);
                    double wi = Math.Sin(angle);
                    fftw_complex even = output[blockBase + j];
                    fftw_complex odd = output[blockBase + j + half];
                    double tr = wr * odd.re - wi * odd.im;
                    double ti = wr * odd.im + wi * odd.re;

                    output[blockBase + j].re = even.re + tr;
                    output[blockBase + j].im = even.im + ti;
                    output[blockBase + j + half].re = even.re - tr;
                    output[blockBase + j + half].im = even.im - ti;
                }
            }
        }
    }

    public static void fftw_destroy_plan(fftw_plan? plan) {
        if (plan is null)
            return;

        plan.n = 0;
        plan.direction = 0;
    }
}
