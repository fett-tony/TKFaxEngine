/*
 * TKFaxEngine - managed C# port
 *
 * VectorDsp.cs
 *
 * Combined port of:
 *   vector_float.h / vector_float.c
 *   vector_int.h / vector_int.c
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 *
 * This port preserves the LGPL-2.1 licensing terms of the original files.
 */

using System.Numerics;
using System.Runtime.CompilerServices;

namespace TKFaxEngine;

/// <summary>
/// Managed real-vector arithmetic corresponding to vector_float.h and
/// vector_float.c. Generic math is used for the operations shared by
/// <see cref="float"/> and <see cref="double"/>.
/// </summary>
public static class VectorMath {
    private const float LmsLeakRate = 0.9999f;

    public static void Copy<T>(
        Span<T> destination,
        ReadOnlySpan<T> source,
        int count)
        where T : unmanaged {
        ValidateUnary(destination.Length, source.Length, count);
        source[..count].CopyTo(destination);
    }

    public static void Negate<T>(
        Span<T> destination,
        ReadOnlySpan<T> source,
        int count)
        where T : unmanaged, INumber<T> {
        ValidateUnary(destination.Length, source.Length, count);

        for (int i = 0; i < count; i++)
            destination[i] = -source[i];
    }

    public static void Zero<T>(
        Span<T> destination,
        int count)
        where T : unmanaged {
        ValidateCount(destination.Length, count);
        destination[..count].Clear();
    }

    public static void Set<T>(
        Span<T> destination,
        T value,
        int count)
        where T : unmanaged {
        ValidateCount(destination.Length, count);
        destination[..count].Fill(value);
    }

    public static void Add<T>(
        Span<T> destination,
        ReadOnlySpan<T> x,
        ReadOnlySpan<T> y,
        int count)
        where T : unmanaged, INumber<T> {
        ValidateBinary(destination.Length, x.Length, y.Length, count);

        for (int i = 0; i < count; i++)
            destination[i] = x[i] + y[i];
    }

    /// <summary>
    /// Computes <c>destination[i] = x[i] * xScale + y[i] * yScale</c>.
    /// </summary>
    public static void ScaledXyAdd<T>(
        Span<T> destination,
        ReadOnlySpan<T> x,
        T xScale,
        ReadOnlySpan<T> y,
        T yScale,
        int count)
        where T : unmanaged, INumber<T> {
        ValidateBinary(destination.Length, x.Length, y.Length, count);

        for (int i = 0; i < count; i++)
            destination[i] = x[i] * xScale + y[i] * yScale;
    }

    /// <summary>
    /// Computes <c>destination[i] = x[i] + y[i] * yScale</c>.
    /// </summary>
    public static void ScaledYAdd<T>(
        Span<T> destination,
        ReadOnlySpan<T> x,
        ReadOnlySpan<T> y,
        T yScale,
        int count)
        where T : unmanaged, INumber<T> {
        ValidateBinary(destination.Length, x.Length, y.Length, count);

        for (int i = 0; i < count; i++)
            destination[i] = x[i] + y[i] * yScale;
    }

    public static void Subtract<T>(
        Span<T> destination,
        ReadOnlySpan<T> x,
        ReadOnlySpan<T> y,
        int count)
        where T : unmanaged, INumber<T> {
        ValidateBinary(destination.Length, x.Length, y.Length, count);

        for (int i = 0; i < count; i++)
            destination[i] = x[i] - y[i];
    }

    /// <summary>
    /// Computes <c>destination[i] = x[i] * xScale - y[i] * yScale</c>.
    /// </summary>
    public static void ScaledXySubtract<T>(
        Span<T> destination,
        ReadOnlySpan<T> x,
        T xScale,
        ReadOnlySpan<T> y,
        T yScale,
        int count)
        where T : unmanaged, INumber<T> {
        ValidateBinary(destination.Length, x.Length, y.Length, count);

        for (int i = 0; i < count; i++)
            destination[i] = x[i] * xScale - y[i] * yScale;
    }

    /// <summary>
    /// Computes <c>destination[i] = x[i] * xScale - y[i]</c>.
    /// This operation is declared in vector_float.h but is absent from the
    /// supplied vector_float.c.
    /// </summary>
    public static void ScaledXSubtract<T>(
        Span<T> destination,
        ReadOnlySpan<T> x,
        T xScale,
        ReadOnlySpan<T> y,
        int count)
        where T : unmanaged, INumber<T> {
        ValidateBinary(destination.Length, x.Length, y.Length, count);

        for (int i = 0; i < count; i++)
            destination[i] = x[i] * xScale - y[i];
    }

    /// <summary>
    /// Computes <c>destination[i] = x[i] - y[i] * yScale</c>.
    /// This operation is declared in vector_float.h but is absent from the
    /// supplied vector_float.c.
    /// </summary>
    public static void ScaledYSubtract<T>(
        Span<T> destination,
        ReadOnlySpan<T> x,
        ReadOnlySpan<T> y,
        T yScale,
        int count)
        where T : unmanaged, INumber<T> {
        ValidateBinary(destination.Length, x.Length, y.Length, count);

        for (int i = 0; i < count; i++)
            destination[i] = x[i] - y[i] * yScale;
    }

    public static void ScalarMultiply<T>(
        Span<T> destination,
        ReadOnlySpan<T> x,
        T scalar,
        int count)
        where T : unmanaged, INumber<T> {
        ValidateUnary(destination.Length, x.Length, count);

        for (int i = 0; i < count; i++)
            destination[i] = x[i] * scalar;
    }

    public static void ScalarAdd<T>(
        Span<T> destination,
        ReadOnlySpan<T> x,
        T scalar,
        int count)
        where T : unmanaged, INumber<T> {
        ValidateUnary(destination.Length, x.Length, count);

        for (int i = 0; i < count; i++)
            destination[i] = x[i] + scalar;
    }

    public static void ScalarSubtract<T>(
        Span<T> destination,
        ReadOnlySpan<T> x,
        T scalar,
        int count)
        where T : unmanaged, INumber<T> {
        ValidateUnary(destination.Length, x.Length, count);

        for (int i = 0; i < count; i++)
            destination[i] = x[i] - scalar;
    }

    public static void Multiply<T>(
        Span<T> destination,
        ReadOnlySpan<T> x,
        ReadOnlySpan<T> y,
        int count)
        where T : unmanaged, INumber<T> {
        ValidateBinary(destination.Length, x.Length, y.Length, count);

        for (int i = 0; i < count; i++)
            destination[i] = x[i] * y[i];
    }

    public static T DotProduct<T>(
        ReadOnlySpan<T> x,
        ReadOnlySpan<T> y,
        int count)
        where T : unmanaged, INumber<T> {
        ValidatePair(x.Length, y.Length, count);

        T result = T.Zero;
        for (int i = 0; i < count; i++)
            result += x[i] * y[i];

        return result;
    }

    public static T CircularDotProduct<T>(
        ReadOnlySpan<T> x,
        ReadOnlySpan<T> y,
        int count,
        int position)
        where T : unmanaged, INumber<T> {
        ValidateCircular(x.Length, y.Length, count, position);

        if (count == 0)
            return T.Zero;

        T first = DotProduct(x[position..], y, count - position);
        T second = DotProduct(x, y[(count - position)..], position);
        return first + second;
    }

    /// <summary>
    /// Applies the original floating-point leaky LMS update:
    /// <c>y[i] = y[i] * 0.9999 + x[i] * error</c>.
    /// </summary>
    public static void Lms(
        ReadOnlySpan<float> x,
        Span<float> y,
        int count,
        float error) {
        ValidateUnary(y.Length, x.Length, count);

        for (int i = 0; i < count; i++)
            y[i] = y[i] * LmsLeakRate + x[i] * error;
    }

    public static void CircularLms(
        ReadOnlySpan<float> x,
        Span<float> y,
        int count,
        int position,
        float error) {
        ValidateCircular(x.Length, y.Length, count, position);

        if (count == 0)
            return;

        Lms(x[position..], y, count - position, error);
        Lms(x, y[(count - position)..], position, error);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateCount(int available, int count) {
        if (count < 0 || count > available)
            throw new ArgumentOutOfRangeException(nameof(count));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateUnary(int destinationLength, int sourceLength, int count) {
        ValidateCount(destinationLength, count);
        ValidateCount(sourceLength, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateBinary(
        int destinationLength,
        int xLength,
        int yLength,
        int count) {
        ValidateCount(destinationLength, count);
        ValidatePair(xLength, yLength, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidatePair(int xLength, int yLength, int count) {
        ValidateCount(xLength, count);
        ValidateCount(yLength, count);
    }

    private static void ValidateCircular(
        int xLength,
        int yLength,
        int count,
        int position) {
        ValidatePair(xLength, yLength, count);

        if (count == 0) {
            if (position != 0)
                throw new ArgumentOutOfRangeException(nameof(position));
            return;
        }

        if ((uint)position >= (uint)count)
            throw new ArgumentOutOfRangeException(nameof(position));
    }
}

/// <summary>
/// Result from the int16 minimum/maximum scan.
/// </summary>
public readonly record struct Int16MinMaxResult(
    short Maximum,
    short Minimum,
    int AbsoluteMaximum);

/// <summary>
/// Managed integer-vector routines corresponding to vector_int.h and
/// vector_int.c.
/// </summary>
public static class VectorIntMath {
    public static void Copy<T>(
        Span<T> destination,
        ReadOnlySpan<T> source,
        int count)
        where T : unmanaged {
        ValidateUnary(destination.Length, source.Length, count);
        source[..count].CopyTo(destination);
    }

    /// <summary>
    /// Overlap-safe copy corresponding to the original memmove wrappers.
    /// Span.CopyTo has memmove semantics for overlapping spans.
    /// </summary>
    public static void Move<T>(
        Span<T> destination,
        ReadOnlySpan<T> source,
        int count)
        where T : unmanaged {
        ValidateUnary(destination.Length, source.Length, count);
        source[..count].CopyTo(destination);
    }

    public static void Zero<T>(
        Span<T> destination,
        int count)
        where T : unmanaged {
        ValidateCount(destination.Length, count);
        destination[..count].Clear();
    }

    public static void Set<T>(
        Span<T> destination,
        T value,
        int count)
        where T : unmanaged {
        ValidateCount(destination.Length, count);
        destination[..count].Fill(value);
    }

    public static int DotProduct(
        ReadOnlySpan<short> x,
        ReadOnlySpan<short> y,
        int count) {
        ValidatePair(x.Length, y.Length, count);

        int result = 0;
        unchecked {
            for (int i = 0; i < count; i++)
                result += x[i] * y[i];
        }

        return result;
    }

    public static int CircularDotProduct(
        ReadOnlySpan<short> x,
        ReadOnlySpan<short> y,
        int count,
        int position) {
        ValidateCircular(x.Length, y.Length, count, position);

        if (count == 0)
            return 0;

        unchecked {
            int result = DotProduct(x[position..], y, count - position);
            result += DotProduct(x, y[(count - position)..], position);
            return result;
        }
    }

    /// <summary>
    /// Applies the original Q1.15 integer LMS update.
    /// </summary>
    public static void Lms(
        ReadOnlySpan<short> x,
        Span<short> y,
        int count,
        short error) {
        ValidateUnary(y.Length, x.Length, count);

        unchecked {
            for (int i = 0; i < count; i++) {
                int delta = (x[i] * error) >> 15;
                y[i] = (short)(y[i] + (short)delta);
            }
        }
    }

    public static void CircularLms(
        ReadOnlySpan<short> x,
        Span<short> y,
        int count,
        int position,
        short error) {
        ValidateCircular(x.Length, y.Length, count, position);

        if (count == 0)
            return;

        Lms(x[position..], y, count - position, error);
        Lms(x, y[(count - position)..], position, error);
    }

    public static Int16MinMaxResult MinMax(
        ReadOnlySpan<short> values,
        int count) {
        ValidateCount(values.Length, count);

        short maximum = short.MinValue;
        short minimum = short.MaxValue;

        for (int i = 0; i < count; i++) {
            short value = values[i];

            if (value > maximum)
                maximum = value;

            if (value < minimum)
                minimum = value;
        }

        int negativeMagnitude = Math.Abs((int)minimum);
        int absoluteMaximum = negativeMagnitude > maximum
            ? negativeMagnitude
            : maximum;

        return new Int16MinMaxResult(maximum, minimum, absoluteMaximum);
    }

    /// <summary>
    /// Writes maximum to output[0] and minimum to output[1] when an output span
    /// is supplied, and returns the absolute maximum.
    /// </summary>
    public static int MinMax(
        ReadOnlySpan<short> values,
        int count,
        Span<short> output) {
        if (!output.IsEmpty && output.Length < 2)
            throw new ArgumentException("The output span must contain at least two elements.", nameof(output));

        Int16MinMaxResult result = MinMax(values, count);

        if (!output.IsEmpty) {
            output[0] = result.Maximum;
            output[1] = result.Minimum;
        }

        return result.AbsoluteMaximum;
    }

    public static int NormSquared(
        ReadOnlySpan<short> values,
        int count) {
        ValidateCount(values.Length, count);

        int sum = 0;
        unchecked {
            for (int i = 0; i < count; i++)
                sum += values[i] * values[i];
        }

        return sum;
    }

    public static void ShiftArithmeticRight(
        Span<short> values,
        int count,
        int shift) {
        ValidateCount(values.Length, count);

        if (shift is < 0 or > 15)
            throw new ArgumentOutOfRangeException(nameof(shift), shift, "The shift must be between 0 and 15.");

        for (int i = 0; i < count; i++)
            values[i] = unchecked((short)(values[i] >> shift));
    }

    public static int MaximumBitCount(
        ReadOnlySpan<short> values,
        int count) {
        ValidateCount(values.Length, count);

        int maximum = 0;
        for (int i = 0; i < count; i++) {
            int value = Math.Abs((int)values[i]);
            if (value > maximum)
                maximum = value;
        }

        int bits = 0;
        while (maximum != 0) {
            bits++;
            maximum >>= 1;
        }

        return bits;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateCount(int available, int count) {
        if (count < 0 || count > available)
            throw new ArgumentOutOfRangeException(nameof(count));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateUnary(int destinationLength, int sourceLength, int count) {
        ValidateCount(destinationLength, count);
        ValidateCount(sourceLength, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidatePair(int xLength, int yLength, int count) {
        ValidateCount(xLength, count);
        ValidateCount(yLength, count);
    }

    private static void ValidateCircular(
        int xLength,
        int yLength,
        int count,
        int position) {
        ValidatePair(xLength, yLength, count);

        if (count == 0) {
            if (position != 0)
                throw new ArgumentOutOfRangeException(nameof(position));
            return;
        }

        if ((uint)position >= (uint)count)
            throw new ArgumentOutOfRangeException(nameof(position));
    }
}

/// <summary>
/// Compatibility facade retaining the original C function names.
/// C long double operations are represented by double because .NET does not
/// expose a portable native long-double type.
/// </summary>
public static class VectorDspApi {
    // vector_float.h / vector_float.c

    public static void vec_copyf(
        Span<float> destination,
        ReadOnlySpan<float> source,
        int count) =>
        VectorMath.Copy(destination, source, count);

    public static void vec_copy(
        Span<double> destination,
        ReadOnlySpan<double> source,
        int count) =>
        VectorMath.Copy(destination, source, count);

    public static void vec_copyl(
        Span<double> destination,
        ReadOnlySpan<double> source,
        int count) =>
        VectorMath.Copy(destination, source, count);

    public static void vec_negatef(
        Span<float> destination,
        ReadOnlySpan<float> source,
        int count) =>
        VectorMath.Negate(destination, source, count);

    public static void vec_negate(
        Span<double> destination,
        ReadOnlySpan<double> source,
        int count) =>
        VectorMath.Negate(destination, source, count);

    public static void vec_negatel(
        Span<double> destination,
        ReadOnlySpan<double> source,
        int count) =>
        VectorMath.Negate(destination, source, count);

    public static void vec_zerof(Span<float> destination, int count) =>
        VectorMath.Zero(destination, count);

    public static void vec_zero(Span<double> destination, int count) =>
        VectorMath.Zero(destination, count);

    public static void vec_zerol(Span<double> destination, int count) =>
        VectorMath.Zero(destination, count);

    public static void vec_setf(Span<float> destination, float value, int count) =>
        VectorMath.Set(destination, value, count);

    public static void vec_set(Span<double> destination, double value, int count) =>
        VectorMath.Set(destination, value, count);

    public static void vec_setl(Span<double> destination, double value, int count) =>
        VectorMath.Set(destination, value, count);

    public static void vec_addf(
        Span<float> destination,
        ReadOnlySpan<float> x,
        ReadOnlySpan<float> y,
        int count) =>
        VectorMath.Add(destination, x, y, count);

    public static void vec_add(
        Span<double> destination,
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        int count) =>
        VectorMath.Add(destination, x, y, count);

    public static void vec_addl(
        Span<double> destination,
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        int count) =>
        VectorMath.Add(destination, x, y, count);

    public static void vec_scaledxy_addf(
        Span<float> destination,
        ReadOnlySpan<float> x,
        float xScale,
        ReadOnlySpan<float> y,
        float yScale,
        int count) =>
        VectorMath.ScaledXyAdd(destination, x, xScale, y, yScale, count);

    public static void vec_scaledxy_add(
        Span<double> destination,
        ReadOnlySpan<double> x,
        double xScale,
        ReadOnlySpan<double> y,
        double yScale,
        int count) =>
        VectorMath.ScaledXyAdd(destination, x, xScale, y, yScale, count);

    public static void vec_scaledxy_addl(
        Span<double> destination,
        ReadOnlySpan<double> x,
        double xScale,
        ReadOnlySpan<double> y,
        double yScale,
        int count) =>
        VectorMath.ScaledXyAdd(destination, x, xScale, y, yScale, count);

    public static void vec_scaledy_addf(
        Span<float> destination,
        ReadOnlySpan<float> x,
        ReadOnlySpan<float> y,
        float yScale,
        int count) =>
        VectorMath.ScaledYAdd(destination, x, y, yScale, count);

    public static void vec_scaledy_add(
        Span<double> destination,
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        double yScale,
        int count) =>
        VectorMath.ScaledYAdd(destination, x, y, yScale, count);

    public static void vec_scaledy_addl(
        Span<double> destination,
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        double yScale,
        int count) =>
        VectorMath.ScaledYAdd(destination, x, y, yScale, count);

    public static void vec_subf(
        Span<float> destination,
        ReadOnlySpan<float> x,
        ReadOnlySpan<float> y,
        int count) =>
        VectorMath.Subtract(destination, x, y, count);

    public static void vec_sub(
        Span<double> destination,
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        int count) =>
        VectorMath.Subtract(destination, x, y, count);

    public static void vec_subl(
        Span<double> destination,
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        int count) =>
        VectorMath.Subtract(destination, x, y, count);

    public static void vec_scaledxy_subf(
        Span<float> destination,
        ReadOnlySpan<float> x,
        float xScale,
        ReadOnlySpan<float> y,
        float yScale,
        int count) =>
        VectorMath.ScaledXySubtract(destination, x, xScale, y, yScale, count);

    public static void vec_scaledxy_sub(
        Span<double> destination,
        ReadOnlySpan<double> x,
        double xScale,
        ReadOnlySpan<double> y,
        double yScale,
        int count) =>
        VectorMath.ScaledXySubtract(destination, x, xScale, y, yScale, count);

    public static void vec_scaledxy_subl(
        Span<double> destination,
        ReadOnlySpan<double> x,
        double xScale,
        ReadOnlySpan<double> y,
        double yScale,
        int count) =>
        VectorMath.ScaledXySubtract(destination, x, xScale, y, yScale, count);

    public static void vec_scaledx_subf(
        Span<float> destination,
        ReadOnlySpan<float> x,
        float xScale,
        ReadOnlySpan<float> y,
        int count) =>
        VectorMath.ScaledXSubtract(destination, x, xScale, y, count);

    public static void vec_scaledx_sub(
        Span<double> destination,
        ReadOnlySpan<double> x,
        double xScale,
        ReadOnlySpan<double> y,
        int count) =>
        VectorMath.ScaledXSubtract(destination, x, xScale, y, count);

    public static void vec_scaledx_subl(
        Span<double> destination,
        ReadOnlySpan<double> x,
        double xScale,
        ReadOnlySpan<double> y,
        int count) =>
        VectorMath.ScaledXSubtract(destination, x, xScale, y, count);

    public static void vec_scaledy_subf(
        Span<float> destination,
        ReadOnlySpan<float> x,
        ReadOnlySpan<float> y,
        float yScale,
        int count) =>
        VectorMath.ScaledYSubtract(destination, x, y, yScale, count);

    public static void vec_scaledy_sub(
        Span<double> destination,
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        double yScale,
        int count) =>
        VectorMath.ScaledYSubtract(destination, x, y, yScale, count);

    public static void vec_scaledy_subl(
        Span<double> destination,
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        double yScale,
        int count) =>
        VectorMath.ScaledYSubtract(destination, x, y, yScale, count);

    public static void vec_scalar_mulf(
        Span<float> destination,
        ReadOnlySpan<float> x,
        float scalar,
        int count) =>
        VectorMath.ScalarMultiply(destination, x, scalar, count);

    public static void vec_scalar_mul(
        Span<double> destination,
        ReadOnlySpan<double> x,
        double scalar,
        int count) =>
        VectorMath.ScalarMultiply(destination, x, scalar, count);

    public static void vec_scalar_mull(
        Span<double> destination,
        ReadOnlySpan<double> x,
        double scalar,
        int count) =>
        VectorMath.ScalarMultiply(destination, x, scalar, count);

    public static void vec_scalar_addf(
        Span<float> destination,
        ReadOnlySpan<float> x,
        float scalar,
        int count) =>
        VectorMath.ScalarAdd(destination, x, scalar, count);

    public static void vec_scalar_add(
        Span<double> destination,
        ReadOnlySpan<double> x,
        double scalar,
        int count) =>
        VectorMath.ScalarAdd(destination, x, scalar, count);

    public static void vec_scalar_addl(
        Span<double> destination,
        ReadOnlySpan<double> x,
        double scalar,
        int count) =>
        VectorMath.ScalarAdd(destination, x, scalar, count);

    public static void vec_scalar_subf(
        Span<float> destination,
        ReadOnlySpan<float> x,
        float scalar,
        int count) =>
        VectorMath.ScalarSubtract(destination, x, scalar, count);

    public static void vec_scalar_sub(
        Span<double> destination,
        ReadOnlySpan<double> x,
        double scalar,
        int count) =>
        VectorMath.ScalarSubtract(destination, x, scalar, count);

    public static void vec_scalar_subl(
        Span<double> destination,
        ReadOnlySpan<double> x,
        double scalar,
        int count) =>
        VectorMath.ScalarSubtract(destination, x, scalar, count);

    public static void vec_mulf(
        Span<float> destination,
        ReadOnlySpan<float> x,
        ReadOnlySpan<float> y,
        int count) =>
        VectorMath.Multiply(destination, x, y, count);

    public static void vec_mul(
        Span<double> destination,
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        int count) =>
        VectorMath.Multiply(destination, x, y, count);

    public static void vec_mull(
        Span<double> destination,
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        int count) =>
        VectorMath.Multiply(destination, x, y, count);

    public static float vec_dot_prodf(
        ReadOnlySpan<float> x,
        ReadOnlySpan<float> y,
        int count) =>
        VectorMath.DotProduct(x, y, count);

    public static double vec_dot_prod(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        int count) =>
        VectorMath.DotProduct(x, y, count);

    public static double vec_dot_prodl(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        int count) =>
        VectorMath.DotProduct(x, y, count);

    public static float vec_circular_dot_prodf(
        ReadOnlySpan<float> x,
        ReadOnlySpan<float> y,
        int count,
        int position) =>
        VectorMath.CircularDotProduct(x, y, count, position);

    public static void vec_lmsf(
        ReadOnlySpan<float> x,
        Span<float> y,
        int count,
        float error) =>
        VectorMath.Lms(x, y, count, error);

    public static void vec_circular_lmsf(
        ReadOnlySpan<float> x,
        Span<float> y,
        int count,
        int position,
        float error) =>
        VectorMath.CircularLms(x, y, count, position, error);

    // vector_int.h / vector_int.c

    public static void vec_copyi(
        Span<int> destination,
        ReadOnlySpan<int> source,
        int count) =>
        VectorIntMath.Copy(destination, source, count);

    public static void vec_copyi16(
        Span<short> destination,
        ReadOnlySpan<short> source,
        int count) =>
        VectorIntMath.Copy(destination, source, count);

    public static void vec_copyi32(
        Span<int> destination,
        ReadOnlySpan<int> source,
        int count) =>
        VectorIntMath.Copy(destination, source, count);

    public static void vec_movei(
        Span<int> destination,
        ReadOnlySpan<int> source,
        int count) =>
        VectorIntMath.Move(destination, source, count);

    public static void vec_movei16(
        Span<short> destination,
        ReadOnlySpan<short> source,
        int count) =>
        VectorIntMath.Move(destination, source, count);

    public static void vec_movei32(
        Span<int> destination,
        ReadOnlySpan<int> source,
        int count) =>
        VectorIntMath.Move(destination, source, count);

    public static void vec_zeroi(Span<int> destination, int count) =>
        VectorIntMath.Zero(destination, count);

    public static void vec_zeroi16(Span<short> destination, int count) =>
        VectorIntMath.Zero(destination, count);

    public static void vec_zeroi32(Span<int> destination, int count) =>
        VectorIntMath.Zero(destination, count);

    public static void vec_seti(Span<int> destination, int value, int count) =>
        VectorIntMath.Set(destination, value, count);

    public static void vec_seti16(Span<short> destination, short value, int count) =>
        VectorIntMath.Set(destination, value, count);

    public static void vec_seti32(Span<int> destination, int value, int count) =>
        VectorIntMath.Set(destination, value, count);

    public static int vec_dot_prodi16(
        ReadOnlySpan<short> x,
        ReadOnlySpan<short> y,
        int count) =>
        VectorIntMath.DotProduct(x, y, count);

    public static int vec_circular_dot_prodi16(
        ReadOnlySpan<short> x,
        ReadOnlySpan<short> y,
        int count,
        int position) =>
        VectorIntMath.CircularDotProduct(x, y, count, position);

    public static void vec_lmsi16(
        ReadOnlySpan<short> x,
        Span<short> y,
        int count,
        short error) =>
        VectorIntMath.Lms(x, y, count, error);

    public static void vec_circular_lmsi16(
        ReadOnlySpan<short> x,
        Span<short> y,
        int count,
        int position,
        short error) =>
        VectorIntMath.CircularLms(x, y, count, position, error);

    public static int vec_min_maxi16(
        ReadOnlySpan<short> values,
        int count,
        Span<short> output = default) =>
        VectorIntMath.MinMax(values, count, output);

    public static int vec_norm2i16(
        ReadOnlySpan<short> values,
        int count) =>
        VectorIntMath.NormSquared(values, count);

    public static void vec_sari16(
        Span<short> values,
        int count,
        int shift) =>
        VectorIntMath.ShiftArithmeticRight(values, count, shift);

    public static int vec_max_bitsi16(
        ReadOnlySpan<short> values,
        int count) =>
        VectorIntMath.MaximumBitCount(values, count);
}
