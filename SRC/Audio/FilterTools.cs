/*
 * TKFaxEngine - managed C# port
 *
 * FilterTools.cs - routines used for FIR filter design.
 *
 * Ported from filter_tools.c and filter_tools.h.
 */

#nullable enable

using System.Numerics;

namespace TKFaxEngine.Audio;

/// <summary>
/// Utility routines for filter design.
/// </summary>
/// <remarks>
/// The inverse FFT intentionally follows the original TKFaxEngine implementation:
/// it uses a positive complex exponential and does not normalise the output.
/// </remarks>
public static class FilterTools {
    public const int MaxPolesAndZeros = 8192;
    public const int SequenceLength = 8192;
    public const int MaximumFftLength = SequenceLength;

    private static readonly Complex[] Circle = CreateCircle();

    /// <summary>
    /// Performs the unnormalised inverse FFT used by the native filter-design code.
    /// </summary>
    /// <param name="data">Complex input/output array.</param>
    /// <param name="length">Number of values to transform.</param>
    public static void Ifft(Complex[] data, int length) {
        ArgumentNullException.ThrowIfNull(data);

        if (length <= 0 || length > MaximumFftLength) {
            throw new ArgumentOutOfRangeException(
                nameof(length),
                $"Length must be between 1 and {MaximumFftLength}.");
        }

        if (data.Length < length) {
            throw new ArgumentException(
                "The data array is shorter than the requested transform length.",
                nameof(data));
        }

        if (!IsPowerOfTwo(length)) {
            throw new ArgumentException(
                "The transform length must be a power of two.",
                nameof(length));
        }

        if (MaximumFftLength % length != 0) {
            throw new ArgumentException(
                $"The transform length must divide {MaximumFftLength}.",
                nameof(length));
        }

        var temporary = new Complex[length];
        Fftx(data, 0, temporary, 0, length);
    }

    /// <summary>
    /// Designs a raised-cosine or root-raised-cosine FIR filter.
    /// </summary>
    public static void ComputeRaisedCosineFilter(
        double[] coefficients,
        int length,
        bool root,
        bool sincCompensate,
        double alpha,
        double beta) {
        ValidateCoefficientArray(coefficients, length);

        if (alpha <= 0.0) {
            throw new ArgumentOutOfRangeException(
                nameof(alpha),
                "Alpha must be greater than zero.");
        }

        if (beta < 0.0) {
            throw new ArgumentOutOfRangeException(
                nameof(beta),
                "Beta must not be negative.");
        }

        double f1 = (1.0 - beta) * alpha;
        double f2 = (1.0 + beta) * alpha;
        double tau = 0.5 / alpha;

        var vector = new Complex[SequenceLength];

        for (int i = 0; i <= SequenceLength / 2; i++) {
            double frequency = (double)i / SequenceLength;
            double real;

            if (frequency <= f1) {
                real = 1.0;
            } else if (frequency <= f2) {
                real = 0.5 *
                       (1.0 + Math.Cos((Math.PI * tau / beta) * (frequency - f1)));
            } else {
                real = 0.0;
            }

            vector[i] = new Complex(real, 0.0);
        }

        if (root) {
            for (int i = 0; i <= SequenceLength / 2; i++) {
                vector[i] = new Complex(Math.Sqrt(vector[i].Real), 0.0);
            }
        }

        if (sincCompensate) {
            for (int i = 1; i <= SequenceLength / 2; i++) {
                double x = Math.PI * i / SequenceLength;
                vector[i] = new Complex(vector[i].Real * (x / Math.Sin(x)), 0.0);
            }
        }

        for (int i = 0; i <= SequenceLength / 2; i++) {
            vector[i] = new Complex(vector[i].Real * tau, 0.0);
        }

        for (int i = 1; i < SequenceLength / 2; i++) {
            vector[SequenceLength - i] = vector[i];
        }

        Ifft(vector, SequenceLength);

        int half = (length - 1) / 2;
        for (int i = 0; i < length; i++) {
            int sourceIndex = (SequenceLength - half + i) % SequenceLength;
            coefficients[i] = vector[sourceIndex].Real / SequenceLength;
        }
    }

    /// <summary>
    /// Native-compatible overload using integer flags.
    /// </summary>
    public static void ComputeRaisedCosineFilter(
        double[] coefficients,
        int length,
        int root,
        int sincCompensate,
        double alpha,
        double beta) {
        ComputeRaisedCosineFilter(
            coefficients,
            length,
            root != 0,
            sincCompensate != 0,
            alpha,
            beta);
    }

    /// <summary>
    /// Computes the ideal odd-length Hilbert-transform impulse response.
    /// </summary>
    public static void ComputeHilbertTransform(double[] coefficients, int length) {
        ValidateOddCoefficientArray(coefficients, length);

        int half = (length - 1) / 2;
        coefficients[half] = 0.0;

        for (int i = 1; i <= half; i++) {
            if ((i & 1) != 0) {
                double value = 1.0 / i;
                coefficients[half + i] = -value;
                coefficients[half - i] = value;
            } else {
                coefficients[half + i] = 0.0;
                coefficients[half - i] = 0.0;
            }
        }
    }

    /// <summary>
    /// Applies the symmetric Hamming window used by the native implementation.
    /// </summary>
    public static void ApplyHammingWindow(double[] coefficients, int length) {
        ValidateOddCoefficientArray(coefficients, length);

        int half = (length - 1) / 2;

        for (int i = 1; i <= half; i++) {
            double window =
                0.53836 -
                0.46164 *
                Math.Cos(
                    2.0 *
                    Math.PI *
                    (half + i) /
                    (length - 1.0));

            coefficients[half + i] *= window;
            coefficients[half - i] *= window;
        }
    }

    /// <summary>
    /// Scales and quantises coefficients to the requested signed fixed-point width.
    /// </summary>
    public static void TruncateCoefficients(
        double[] coefficients,
        int length,
        int bits,
        bool hilbert) {
        ValidateOddCoefficientArray(coefficients, length);

        if (bits < 2 || bits > 63) {
            throw new ArgumentOutOfRangeException(
                nameof(bits),
                "The coefficient width must be between 2 and 63 bits.");
        }

        int half = (length - 1) / 2;
        if (hilbert && half == 0) {
            throw new ArgumentException(
                "A Hilbert-transform filter must contain at least three coefficients.",
                nameof(length));
        }

        double factor = Math.Pow(2.0, bits - 1.0);
        double maximum = hilbert
            ? coefficients[half - 1]
            : coefficients[half];

        if (maximum == 0.0 || double.IsNaN(maximum) || double.IsInfinity(maximum)) {
            throw new InvalidOperationException(
                "The reference coefficient must be finite and non-zero.");
        }

        double scale = (factor - 1.0) / (factor * maximum);

        for (int i = 0; i < length; i++) {
            double scaled = coefficients[i] * scale;
            coefficients[i] = Fix(scaled * factor) / factor;
        }
    }

    /// <summary>
    /// Native-compatible overload using an integer Hilbert flag.
    /// </summary>
    public static void TruncateCoefficients(
        double[] coefficients,
        int length,
        int bits,
        int hilbert) {
        TruncateCoefficients(coefficients, length, bits, hilbert != 0);
    }

    // Native function-name aliases for direct ports of callers.
    public static void ifft(Complex[] data, int len) =>
        Ifft(data, len);

    public static void apply_hamming_window(double[] coeffs, int len) =>
        ApplyHammingWindow(coeffs, len);

    public static void truncate_coeffs(
        double[] coeffs,
        int len,
        int bits,
        int hilbert) =>
        TruncateCoefficients(coeffs, len, bits, hilbert);

    public static void compute_raised_cosine_filter(
        double[] coeffs,
        int len,
        int root,
        int sincCompensate,
        double alpha,
        double beta) =>
        ComputeRaisedCosineFilter(
            coeffs,
            len,
            root,
            sincCompensate,
            alpha,
            beta);

    public static void compute_hilbert_transform(double[] coeffs, int len) =>
        ComputeHilbertTransform(coeffs, len);

    private static Complex[] CreateCircle() {
        var circle = new Complex[MaximumFftLength / 2];

        for (int i = 0; i < circle.Length; i++) {
            double angle = 2.0 * Math.PI * i / MaximumFftLength;
            circle[i] = new Complex(Math.Cos(angle), Math.Sin(angle));
        }

        return circle;
    }

    private static void Fftx(
        Complex[] data,
        int dataOffset,
        Complex[] temporary,
        int temporaryOffset,
        int length) {
        if (length <= 1) {
            return;
        }

        int half = length / 2;

        for (int i = 0; i < half; i++) {
            int sourceIndex = dataOffset + i * 2;
            temporary[temporaryOffset + i] = data[sourceIndex];
            temporary[temporaryOffset + half + i] = data[sourceIndex + 1];
        }

        Fftx(temporary, temporaryOffset, data, dataOffset, half);
        Fftx(temporary, temporaryOffset + half, data, dataOffset + half, half);

        int circleIndex = 0;
        int circleStep = MaximumFftLength / length;

        for (int i = 0; i < half; i++) {
            Complex even = temporary[temporaryOffset + i];
            Complex weightedOdd =
                Circle[circleIndex] *
                temporary[temporaryOffset + half + i];

            data[dataOffset + i] = even + weightedOdd;
            data[dataOffset + half + i] = even - weightedOdd;
            circleIndex += circleStep;
        }
    }

    private static double Fix(double value) {
        return value >= 0.0
            ? Math.Floor(0.5 + value)
            : -Math.Floor(0.5 - value);
    }

    private static bool IsPowerOfTwo(int value) {
        return (value & (value - 1)) == 0;
    }

    private static void ValidateCoefficientArray(
        double[] coefficients,
        int length) {
        ArgumentNullException.ThrowIfNull(coefficients);

        if (length <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(length),
                "Length must be greater than zero.");
        }

        if (length > SequenceLength) {
            throw new ArgumentOutOfRangeException(
                nameof(length),
                $"Length must not exceed {SequenceLength}.");
        }

        if (coefficients.Length < length) {
            throw new ArgumentException(
                "The coefficient array is shorter than the requested length.",
                nameof(coefficients));
        }
    }

    private static void ValidateOddCoefficientArray(
        double[] coefficients,
        int length) {
        ValidateCoefficientArray(coefficients, length);

        if ((length & 1) == 0) {
            throw new ArgumentException(
                "The filter length must be odd.",
                nameof(length));
        }
    }
}
