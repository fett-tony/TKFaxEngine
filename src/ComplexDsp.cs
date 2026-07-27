/*
 * TKFaxEngine - managed C# port
 *
 * ComplexDsp.cs
 *
 * Combined port of:
 *   complex.h
 *   complex_filters.h / complex_filters.c
 *   complex_vector_float.h / complex_vector_float.c
 *   complex_vector_int.h / complex_vector_int.c
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 *
 * This port preserves the LGPL-2.1 licensing terms of the original files.
 */

using System.Runtime.CompilerServices;

namespace TKFaxEngine;

/// <summary>Managed equivalent of <c>complexf_t</c>.</summary>
public struct ComplexFloat {
    public float Re;
    public float Im;

    public ComplexFloat(float re, float im) {
        Re = re;
        Im = im;
    }

    public readonly float Power => Re * Re + Im * Im;

    public readonly ComplexFloat Conjugate() => new(Re, -Im);

    public override readonly string ToString() => $"({Re}, {Im})";
}

/// <summary>Managed equivalent of <c>complex_t</c>.</summary>
public struct ComplexDouble {
    public double Re;
    public double Im;

    public ComplexDouble(double re, double im) {
        Re = re;
        Im = im;
    }

    public readonly double Power => Re * Re + Im * Im;

    public readonly ComplexDouble Conjugate() => new(Re, -Im);

    public override readonly string ToString() => $"({Re}, {Im})";
}

/// <summary>
/// Managed equivalent of <c>complexl_t</c>.
/// .NET has no portable native C <c>long double</c>; values are represented by
/// <see cref="double"/> so the type remains usable on all supported runtimes.
/// </summary>
public struct ComplexLongDouble {
    public double Re;
    public double Im;

    public ComplexLongDouble(double re, double im) {
        Re = re;
        Im = im;
    }

    public readonly double Power => Re * Re + Im * Im;

    public readonly ComplexLongDouble Conjugate() => new(Re, -Im);

    public override readonly string ToString() => $"({Re}, {Im})";
}

/// <summary>Managed equivalent of <c>complexi_t</c>.</summary>
public struct ComplexInt {
    public int Re;
    public int Im;

    public ComplexInt(int re, int im) {
        Re = re;
        Im = im;
    }

    public override readonly string ToString() => $"({Re}, {Im})";
}

/// <summary>Managed equivalent of <c>complexi16_t</c>.</summary>
public struct ComplexInt16 {
    public short Re;
    public short Im;

    public ComplexInt16(short re, short im) {
        Re = re;
        Im = im;
    }

    public readonly int Power => unchecked(Re * Re + Im * Im);

    public override readonly string ToString() => $"({Re}, {Im})";
}

/// <summary>Managed equivalent of <c>complexi32_t</c>.</summary>
public struct ComplexInt32 {
    public int Re;
    public int Im;

    public ComplexInt32(int re, int im) {
        Re = re;
        Im = im;
    }

    public override readonly string ToString() => $"({Re}, {Im})";
}

/// <summary>
/// Scalar complex-number operations corresponding to the inline functions from
/// <c>complex.h</c>.
/// </summary>
public static class ComplexMath {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexFloat Set(float re, float im) => new(re, im);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexDouble Set(double re, double im) => new(re, im);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexLongDouble SetLongDouble(double re, double im) => new(re, im);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexInt SetInt(int re, int im) => new(re, im);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexInt16 SetInt16(short re, short im) => new(re, im);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexInt32 SetInt32(int re, int im) => new(re, im);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexFloat Add(in ComplexFloat x, in ComplexFloat y) =>
        new(x.Re + y.Re, x.Im + y.Im);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexDouble Add(in ComplexDouble x, in ComplexDouble y) =>
        new(x.Re + y.Re, x.Im + y.Im);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexLongDouble Add(in ComplexLongDouble x, in ComplexLongDouble y) =>
        new(x.Re + y.Re, x.Im + y.Im);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexInt Add(in ComplexInt x, in ComplexInt y) =>
        new(unchecked(x.Re + y.Re), unchecked(x.Im + y.Im));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexInt16 Add(in ComplexInt16 x, in ComplexInt16 y) =>
        new(unchecked((short)(x.Re + y.Re)), unchecked((short)(x.Im + y.Im)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexInt32 Add(in ComplexInt32 x, in ComplexInt32 y) =>
        new(unchecked(x.Re + y.Re), unchecked(x.Im + y.Im));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexFloat Subtract(in ComplexFloat x, in ComplexFloat y) =>
        new(x.Re - y.Re, x.Im - y.Im);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexDouble Subtract(in ComplexDouble x, in ComplexDouble y) =>
        new(x.Re - y.Re, x.Im - y.Im);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexLongDouble Subtract(in ComplexLongDouble x, in ComplexLongDouble y) =>
        new(x.Re - y.Re, x.Im - y.Im);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexInt Subtract(in ComplexInt x, in ComplexInt y) =>
        new(unchecked(x.Re - y.Re), unchecked(x.Im - y.Im));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexInt16 Subtract(in ComplexInt16 x, in ComplexInt16 y) =>
        new(unchecked((short)(x.Re - y.Re)), unchecked((short)(x.Im - y.Im)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexInt32 Subtract(in ComplexInt32 x, in ComplexInt32 y) =>
        new(unchecked(x.Re - y.Re), unchecked(x.Im - y.Im));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexFloat Multiply(in ComplexFloat x, in ComplexFloat y) =>
        new(
            x.Re * y.Re - x.Im * y.Im,
            x.Re * y.Im + x.Im * y.Re);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexDouble Multiply(in ComplexDouble x, in ComplexDouble y) =>
        new(
            x.Re * y.Re - x.Im * y.Im,
            x.Re * y.Im + x.Im * y.Re);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexLongDouble Multiply(in ComplexLongDouble x, in ComplexLongDouble y) =>
        new(
            x.Re * y.Re - x.Im * y.Im,
            x.Re * y.Im + x.Im * y.Re);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexInt Multiply(in ComplexInt x, in ComplexInt y) =>
        new(
            unchecked(x.Re * y.Re - x.Im * y.Im),
            unchecked(x.Re * y.Im + x.Im * y.Re));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexInt16 Multiply(in ComplexInt16 x, in ComplexInt16 y) =>
        new(
            unchecked((short)(x.Re * y.Re - x.Im * y.Im)),
            unchecked((short)(x.Re * y.Im + x.Im * y.Re)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexInt16 MultiplyQ1_15(in ComplexInt16 x, in ComplexInt16 y) =>
        new(
            unchecked((short)((x.Re * y.Re - x.Im * y.Im) >> 15)),
            unchecked((short)((x.Re * y.Im + x.Im * y.Re) >> 15)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexInt32 Multiply(in ComplexInt32 x, in ComplexInt16 y) =>
        new(
            unchecked(x.Re * y.Re - x.Im * y.Im),
            unchecked(x.Re * y.Im + x.Im * y.Re));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexInt32 Multiply(in ComplexInt32 x, in ComplexInt32 y) =>
        new(
            unchecked(x.Re * y.Re - x.Im * y.Im),
            unchecked(x.Re * y.Im + x.Im * y.Re));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexFloat Divide(in ComplexFloat x, in ComplexFloat y) {
        float denominator = y.Re * y.Re + y.Im * y.Im;
        return new(
            (x.Re * y.Re + x.Im * y.Im) / denominator,
            (-x.Re * y.Im + x.Im * y.Re) / denominator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexDouble Divide(in ComplexDouble x, in ComplexDouble y) {
        double denominator = y.Re * y.Re + y.Im * y.Im;
        return new(
            (x.Re * y.Re + x.Im * y.Im) / denominator,
            (-x.Re * y.Im + x.Im * y.Re) / denominator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexLongDouble Divide(in ComplexLongDouble x, in ComplexLongDouble y) {
        double denominator = y.Re * y.Re + y.Im * y.Im;
        return new(
            (x.Re * y.Re + x.Im * y.Im) / denominator,
            (-x.Re * y.Im + x.Im * y.Re) / denominator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexFloat Conjugate(in ComplexFloat x) => new(x.Re, -x.Im);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexDouble Conjugate(in ComplexDouble x) => new(x.Re, -x.Im);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexLongDouble Conjugate(in ComplexLongDouble x) => new(x.Re, -x.Im);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexInt Conjugate(in ComplexInt x) =>
        new(x.Re, unchecked(-x.Im));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexInt16 Conjugate(in ComplexInt16 x) =>
        new(x.Re, unchecked((short)-x.Im));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ComplexInt32 Conjugate(in ComplexInt32 x) =>
        new(x.Re, unchecked(-x.Im));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Power(in ComplexInt16 x) =>
        unchecked(x.Re * x.Re + x.Im * x.Im);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Power(in ComplexFloat x) =>
        x.Re * x.Re + x.Im * x.Im;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Power(in ComplexDouble x) =>
        x.Re * x.Re + x.Im * x.Im;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Power(in ComplexLongDouble x) =>
        x.Re * x.Re + x.Im * x.Im;
}

/// <summary>
/// Callback used by a real-valued filter specification.
/// It corresponds to <c>filter_step_func_t</c>.
/// </summary>
public delegate float FilterStepDelegate(FilterState filter, float input);

/// <summary>Managed equivalent of <c>fspec_t</c>.</summary>
public sealed class FilterSpec {
    public FilterSpec(int zeroCount, int poleCount, FilterStepDelegate step) {
        if (zeroCount < 0)
            throw new ArgumentOutOfRangeException(nameof(zeroCount));
        if (poleCount < 0)
            throw new ArgumentOutOfRangeException(nameof(poleCount));

        Step = step ?? throw new ArgumentNullException(nameof(step));
        ZeroCount = zeroCount;
        PoleCount = poleCount;
    }

    public int ZeroCount { get; }

    public int PoleCount { get; }

    public FilterStepDelegate Step { get; }
}

/// <summary>
/// Managed equivalent of <c>filter_t</c>. The value array contains
/// <c>PoleCount + 1</c> entries, exactly as allocated by the C implementation.
/// </summary>
public sealed class FilterState : IDisposable {
    private bool _disposed;

    public FilterState(FilterSpec specification) {
        Specification = specification ?? throw new ArgumentNullException(nameof(specification));
        Values = new float[checked(specification.PoleCount + 1)];
    }

    public FilterSpec Specification { get; }

    public float Sum { get; set; }

    public int Pointer { get; set; }

    public float[] Values { get; }

    public bool IsDisposed => _disposed;

    public float Step(float input) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Specification.Step(this, input);
    }

    public void Reset() {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Sum = 0.0f;
        Pointer = 0;
        Array.Clear(Values);
    }

    public void Dispose() {
        if (_disposed)
            return;

        Sum = 0.0f;
        Pointer = 0;
        Array.Clear(Values);
        _disposed = true;
    }
}

/// <summary>Managed equivalent of <c>cfilter_t</c>.</summary>
public sealed class ComplexFilterState : IDisposable {
    private bool _disposed;

    public ComplexFilterState(FilterSpec specification) {
        ArgumentNullException.ThrowIfNull(specification);
        RealFilter = new FilterState(specification);
        ImaginaryFilter = new FilterState(specification);
    }

    public FilterState RealFilter { get; }

    public FilterState ImaginaryFilter { get; }

    public bool IsDisposed => _disposed;

    public ComplexFloat Step(in ComplexFloat value) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new(
            RealFilter.Step(value.Re),
            ImaginaryFilter.Step(value.Im));
    }

    public void Reset() {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RealFilter.Reset();
        ImaginaryFilter.Reset();
    }

    public void Dispose() {
        if (_disposed)
            return;

        RealFilter.Dispose();
        ImaginaryFilter.Dispose();
        _disposed = true;
    }
}

public static class ComplexVectorFloat {
    private const float LmsLeakRate = 0.9999f;

    public static void Copy(Span<ComplexFloat> destination, ReadOnlySpan<ComplexFloat> source, int count) {
        ValidateUnary(destination.Length, source.Length, count);
        source[..count].CopyTo(destination);
    }

    public static void Copy(Span<ComplexDouble> destination, ReadOnlySpan<ComplexDouble> source, int count) {
        ValidateUnary(destination.Length, source.Length, count);
        source[..count].CopyTo(destination);
    }

    public static void Copy(Span<ComplexLongDouble> destination, ReadOnlySpan<ComplexLongDouble> source, int count) {
        ValidateUnary(destination.Length, source.Length, count);
        source[..count].CopyTo(destination);
    }

    public static void Zero(Span<ComplexFloat> destination, int count) {
        ValidateCount(destination.Length, count);
        destination[..count].Clear();
    }

    public static void Zero(Span<ComplexDouble> destination, int count) {
        ValidateCount(destination.Length, count);
        destination[..count].Clear();
    }

    public static void Zero(Span<ComplexLongDouble> destination, int count) {
        ValidateCount(destination.Length, count);
        destination[..count].Clear();
    }

    public static void Set(Span<ComplexFloat> destination, in ComplexFloat value, int count) {
        ValidateCount(destination.Length, count);
        destination[..count].Fill(value);
    }

    public static void Set(Span<ComplexDouble> destination, in ComplexDouble value, int count) {
        ValidateCount(destination.Length, count);
        destination[..count].Fill(value);
    }

    public static void Set(Span<ComplexLongDouble> destination, in ComplexLongDouble value, int count) {
        ValidateCount(destination.Length, count);
        destination[..count].Fill(value);
    }

    public static void Multiply(
        Span<ComplexFloat> destination,
        ReadOnlySpan<ComplexFloat> x,
        ReadOnlySpan<ComplexFloat> y,
        int count) {
        ValidateBinary(destination.Length, x.Length, y.Length, count);

        for (int i = 0; i < count; i++) {
            destination[i] = new ComplexFloat(
                x[i].Re * y[i].Re - x[i].Im * y[i].Im,
                x[i].Re * y[i].Im + x[i].Im * y[i].Re);
        }
    }

    public static void Multiply(
        Span<ComplexDouble> destination,
        ReadOnlySpan<ComplexDouble> x,
        ReadOnlySpan<ComplexDouble> y,
        int count) {
        ValidateBinary(destination.Length, x.Length, y.Length, count);

        for (int i = 0; i < count; i++) {
            destination[i] = new ComplexDouble(
                x[i].Re * y[i].Re - x[i].Im * y[i].Im,
                x[i].Re * y[i].Im + x[i].Im * y[i].Re);
        }
    }

    public static void Multiply(
        Span<ComplexLongDouble> destination,
        ReadOnlySpan<ComplexLongDouble> x,
        ReadOnlySpan<ComplexLongDouble> y,
        int count) {
        ValidateBinary(destination.Length, x.Length, y.Length, count);

        for (int i = 0; i < count; i++) {
            destination[i] = new ComplexLongDouble(
                x[i].Re * y[i].Re - x[i].Im * y[i].Im,
                x[i].Re * y[i].Im + x[i].Im * y[i].Re);
        }
    }

    public static ComplexFloat DotProduct(
        ReadOnlySpan<ComplexFloat> x,
        ReadOnlySpan<ComplexFloat> y,
        int count) {
        ValidateBinaryNoDestination(x.Length, y.Length, count);

        float real = 0.0f;
        float imaginary = 0.0f;

        for (int i = 0; i < count; i++) {
            real += x[i].Re * y[i].Re - x[i].Im * y[i].Im;
            imaginary += x[i].Re * y[i].Im + x[i].Im * y[i].Re;
        }

        return new ComplexFloat(real, imaginary);
    }

    public static ComplexDouble DotProduct(
        ReadOnlySpan<ComplexDouble> x,
        ReadOnlySpan<ComplexDouble> y,
        int count) {
        ValidateBinaryNoDestination(x.Length, y.Length, count);

        double real = 0.0;
        double imaginary = 0.0;

        for (int i = 0; i < count; i++) {
            real += x[i].Re * y[i].Re - x[i].Im * y[i].Im;
            imaginary += x[i].Re * y[i].Im + x[i].Im * y[i].Re;
        }

        return new ComplexDouble(real, imaginary);
    }

    public static ComplexLongDouble DotProduct(
        ReadOnlySpan<ComplexLongDouble> x,
        ReadOnlySpan<ComplexLongDouble> y,
        int count) {
        ValidateBinaryNoDestination(x.Length, y.Length, count);

        double real = 0.0;
        double imaginary = 0.0;

        for (int i = 0; i < count; i++) {
            real += x[i].Re * y[i].Re - x[i].Im * y[i].Im;
            imaginary += x[i].Re * y[i].Im + x[i].Im * y[i].Re;
        }

        return new ComplexLongDouble(real, imaginary);
    }

    public static ComplexFloat CircularDotProduct(
        ReadOnlySpan<ComplexFloat> x,
        ReadOnlySpan<ComplexFloat> y,
        int count,
        int position) {
        ValidateCircular(x.Length, y.Length, count, position);

        if (count == 0)
            return default;

        ComplexFloat first = DotProduct(x[position..], y, count - position);
        ComplexFloat second = DotProduct(x, y[(count - position)..], position);
        return ComplexMath.Add(first, second);
    }

    public static void Lms(
        ReadOnlySpan<ComplexFloat> x,
        Span<ComplexFloat> y,
        int count,
        in ComplexFloat error) {
        ValidateUnary(y.Length, x.Length, count);

        for (int i = 0; i < count; i++) {
            ComplexFloat current = y[i];
            current.Re =
                current.Re * LmsLeakRate +
                (x[i].Im * error.Im + x[i].Re * error.Re);
            current.Im =
                current.Im * LmsLeakRate +
                (x[i].Re * error.Im - x[i].Im * error.Re);
            y[i] = current;
        }
    }

    public static void CircularLms(
        ReadOnlySpan<ComplexFloat> x,
        Span<ComplexFloat> y,
        int count,
        int position,
        in ComplexFloat error) {
        ValidateCircular(x.Length, y.Length, count, position);

        if (count == 0)
            return;

        Lms(x[position..], y, count - position, error);
        Lms(x, y[(count - position)..], position, error);
    }

    private static void ValidateCount(int available, int count) {
        if (count < 0 || count > available)
            throw new ArgumentOutOfRangeException(nameof(count));
    }

    private static void ValidateUnary(int destinationLength, int sourceLength, int count) {
        ValidateCount(destinationLength, count);
        ValidateCount(sourceLength, count);
    }

    private static void ValidateBinary(int destinationLength, int xLength, int yLength, int count) {
        ValidateCount(destinationLength, count);
        ValidateBinaryNoDestination(xLength, yLength, count);
    }

    private static void ValidateBinaryNoDestination(int xLength, int yLength, int count) {
        ValidateCount(xLength, count);
        ValidateCount(yLength, count);
    }

    private static void ValidateCircular(int xLength, int yLength, int count, int position) {
        ValidateBinaryNoDestination(xLength, yLength, count);

        if (count == 0) {
            if (position != 0)
                throw new ArgumentOutOfRangeException(nameof(position));
            return;
        }

        if ((uint)position >= (uint)count)
            throw new ArgumentOutOfRangeException(nameof(position));
    }
}

public static class ComplexVectorInt {
    public static void Copy(Span<ComplexInt> destination, ReadOnlySpan<ComplexInt> source, int count) {
        ValidateUnary(destination.Length, source.Length, count);
        source[..count].CopyTo(destination);
    }

    public static void Copy(Span<ComplexInt16> destination, ReadOnlySpan<ComplexInt16> source, int count) {
        ValidateUnary(destination.Length, source.Length, count);
        source[..count].CopyTo(destination);
    }

    public static void Copy(Span<ComplexInt32> destination, ReadOnlySpan<ComplexInt32> source, int count) {
        ValidateUnary(destination.Length, source.Length, count);
        source[..count].CopyTo(destination);
    }

    public static void Zero(Span<ComplexInt> destination, int count) {
        ValidateCount(destination.Length, count);
        destination[..count].Clear();
    }

    public static void Zero(Span<ComplexInt16> destination, int count) {
        ValidateCount(destination.Length, count);
        destination[..count].Clear();
    }

    public static void Zero(Span<ComplexInt32> destination, int count) {
        ValidateCount(destination.Length, count);
        destination[..count].Clear();
    }

    public static void Set(Span<ComplexInt> destination, in ComplexInt value, int count) {
        ValidateCount(destination.Length, count);
        destination[..count].Fill(value);
    }

    public static void Set(Span<ComplexInt16> destination, in ComplexInt16 value, int count) {
        ValidateCount(destination.Length, count);
        destination[..count].Fill(value);
    }

    public static void Set(Span<ComplexInt32> destination, in ComplexInt32 value, int count) {
        ValidateCount(destination.Length, count);
        destination[..count].Fill(value);
    }

    public static ComplexInt32 DotProduct(
        ReadOnlySpan<ComplexInt16> x,
        ReadOnlySpan<ComplexInt16> y,
        int count) {
        ValidateBinary(x.Length, y.Length, count);

        int real = 0;
        int imaginary = 0;

        unchecked {
            for (int i = 0; i < count; i++) {
                real += x[i].Re * y[i].Re - x[i].Im * y[i].Im;
                imaginary += x[i].Re * y[i].Im + x[i].Im * y[i].Re;
            }
        }

        return new ComplexInt32(real, imaginary);
    }

    public static ComplexInt32 DotProduct(
        ReadOnlySpan<ComplexInt32> x,
        ReadOnlySpan<ComplexInt32> y,
        int count) {
        ValidateBinary(x.Length, y.Length, count);

        int real = 0;
        int imaginary = 0;

        unchecked {
            for (int i = 0; i < count; i++) {
                real += x[i].Re * y[i].Re - x[i].Im * y[i].Im;
                imaginary += x[i].Re * y[i].Im + x[i].Im * y[i].Re;
            }
        }

        return new ComplexInt32(real, imaginary);
    }

    public static ComplexInt32 CircularDotProduct(
        ReadOnlySpan<ComplexInt16> x,
        ReadOnlySpan<ComplexInt16> y,
        int count,
        int position) {
        ValidateCircular(x.Length, y.Length, count, position);

        if (count == 0)
            return default;

        ComplexInt32 first = DotProduct(x[position..], y, count - position);
        ComplexInt32 second = DotProduct(x, y[(count - position)..], position);
        return ComplexMath.Add(first, second);
    }

    public static void Lms(
        ReadOnlySpan<ComplexInt16> x,
        Span<ComplexInt16> y,
        int count,
        in ComplexInt16 error) {
        ValidateUnary(y.Length, x.Length, count);

        unchecked {
            for (int i = 0; i < count; i++) {
                int realDelta =
                    (x[i].Im * error.Im + x[i].Re * error.Re) >> 12;
                int imaginaryDelta =
                    (x[i].Re * error.Im - x[i].Im * error.Re) >> 12;

                ComplexInt16 current = y[i];
                current.Re = (short)(current.Re + (short)realDelta);
                current.Im = (short)(current.Im + (short)imaginaryDelta);
                y[i] = current;
            }
        }
    }

    public static void CircularLms(
        ReadOnlySpan<ComplexInt16> x,
        Span<ComplexInt16> y,
        int count,
        int position,
        in ComplexInt16 error) {
        ValidateCircular(x.Length, y.Length, count, position);

        if (count == 0)
            return;

        Lms(x[position..], y, count - position, error);
        Lms(x, y[(count - position)..], position, error);
    }

    private static void ValidateCount(int available, int count) {
        if (count < 0 || count > available)
            throw new ArgumentOutOfRangeException(nameof(count));
    }

    private static void ValidateUnary(int destinationLength, int sourceLength, int count) {
        ValidateCount(destinationLength, count);
        ValidateCount(sourceLength, count);
    }

    private static void ValidateBinary(int xLength, int yLength, int count) {
        ValidateCount(xLength, count);
        ValidateCount(yLength, count);
    }

    private static void ValidateCircular(int xLength, int yLength, int count, int position) {
        ValidateBinary(xLength, yLength, count);

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
/// </summary>
public static class ComplexDspApi {
    // complex.h

    public static ComplexFloat complex_setf(float re, float im) => ComplexMath.Set(re, im);

    public static ComplexDouble complex_set(double re, double im) => ComplexMath.Set(re, im);

    public static ComplexLongDouble complex_setl(double re, double im) =>
        ComplexMath.SetLongDouble(re, im);

    public static ComplexInt complex_seti(int re, int im) => ComplexMath.SetInt(re, im);

    public static ComplexInt16 complex_seti16(short re, short im) => ComplexMath.SetInt16(re, im);

    public static ComplexInt32 complex_seti32(int re, int im) => ComplexMath.SetInt32(re, im);

    public static ComplexFloat complex_addf(in ComplexFloat x, in ComplexFloat y) =>
        ComplexMath.Add(x, y);

    public static ComplexDouble complex_add(in ComplexDouble x, in ComplexDouble y) =>
        ComplexMath.Add(x, y);

    public static ComplexLongDouble complex_addl(in ComplexLongDouble x, in ComplexLongDouble y) =>
        ComplexMath.Add(x, y);

    public static ComplexInt complex_addi(in ComplexInt x, in ComplexInt y) =>
        ComplexMath.Add(x, y);

    public static ComplexInt16 complex_addi16(in ComplexInt16 x, in ComplexInt16 y) =>
        ComplexMath.Add(x, y);

    public static ComplexInt32 complex_addi32(in ComplexInt32 x, in ComplexInt32 y) =>
        ComplexMath.Add(x, y);

    public static ComplexFloat complex_subf(in ComplexFloat x, in ComplexFloat y) =>
        ComplexMath.Subtract(x, y);

    public static ComplexDouble complex_sub(in ComplexDouble x, in ComplexDouble y) =>
        ComplexMath.Subtract(x, y);

    public static ComplexLongDouble complex_subl(in ComplexLongDouble x, in ComplexLongDouble y) =>
        ComplexMath.Subtract(x, y);

    public static ComplexInt complex_subi(in ComplexInt x, in ComplexInt y) =>
        ComplexMath.Subtract(x, y);

    public static ComplexInt16 complex_subi16(in ComplexInt16 x, in ComplexInt16 y) =>
        ComplexMath.Subtract(x, y);

    public static ComplexInt32 complex_subi32(in ComplexInt32 x, in ComplexInt32 y) =>
        ComplexMath.Subtract(x, y);

    public static ComplexFloat complex_mulf(in ComplexFloat x, in ComplexFloat y) =>
        ComplexMath.Multiply(x, y);

    public static ComplexDouble complex_mul(in ComplexDouble x, in ComplexDouble y) =>
        ComplexMath.Multiply(x, y);

    public static ComplexLongDouble complex_mull(in ComplexLongDouble x, in ComplexLongDouble y) =>
        ComplexMath.Multiply(x, y);

    public static ComplexInt complex_muli(in ComplexInt x, in ComplexInt y) =>
        ComplexMath.Multiply(x, y);

    public static ComplexInt16 complex_muli16(in ComplexInt16 x, in ComplexInt16 y) =>
        ComplexMath.Multiply(x, y);

    public static ComplexInt16 complex_mul_q1_15(in ComplexInt16 x, in ComplexInt16 y) =>
        ComplexMath.MultiplyQ1_15(x, y);

    public static ComplexInt32 complex_muli32i16(in ComplexInt32 x, in ComplexInt16 y) =>
        ComplexMath.Multiply(x, y);

    public static ComplexInt32 complex_muli32(in ComplexInt32 x, in ComplexInt32 y) =>
        ComplexMath.Multiply(x, y);

    public static ComplexFloat complex_divf(in ComplexFloat x, in ComplexFloat y) =>
        ComplexMath.Divide(x, y);

    public static ComplexDouble complex_div(in ComplexDouble x, in ComplexDouble y) =>
        ComplexMath.Divide(x, y);

    public static ComplexLongDouble complex_divl(in ComplexLongDouble x, in ComplexLongDouble y) =>
        ComplexMath.Divide(x, y);

    public static ComplexFloat complex_conjf(in ComplexFloat x) => ComplexMath.Conjugate(x);

    public static ComplexDouble complex_conj(in ComplexDouble x) => ComplexMath.Conjugate(x);

    public static ComplexLongDouble complex_conjl(in ComplexLongDouble x) =>
        ComplexMath.Conjugate(x);

    public static ComplexInt complex_conji(in ComplexInt x) => ComplexMath.Conjugate(x);

    public static ComplexInt16 complex_conji16(in ComplexInt16 x) => ComplexMath.Conjugate(x);

    public static ComplexInt32 complex_conji32(in ComplexInt32 x) => ComplexMath.Conjugate(x);

    public static int poweri16(in ComplexInt16 x) => ComplexMath.Power(x);

    public static float powerf(in ComplexFloat x) => ComplexMath.Power(x);

    public static double power(in ComplexDouble x) => ComplexMath.Power(x);

    public static double powerl(in ComplexLongDouble x) => ComplexMath.Power(x);

    // complex_filters.h / complex_filters.c

    public static FilterState filter_create(FilterSpec specification) =>
        new(specification);

    public static void filter_delete(FilterState? filter) =>
        filter?.Dispose();

    public static float filter_step(FilterState filter, float input) {
        ArgumentNullException.ThrowIfNull(filter);
        return filter.Step(input);
    }

    public static ComplexFilterState cfilter_create(FilterSpec specification) =>
        new(specification);

    public static void cfilter_delete(ComplexFilterState? filter) =>
        filter?.Dispose();

    public static ComplexFloat cfilter_step(ComplexFilterState filter, in ComplexFloat value) {
        ArgumentNullException.ThrowIfNull(filter);
        return filter.Step(value);
    }

    // complex_vector_float.h / complex_vector_float.c

    public static void cvec_copyf(
        Span<ComplexFloat> destination,
        ReadOnlySpan<ComplexFloat> source,
        int count) =>
        ComplexVectorFloat.Copy(destination, source, count);

    public static void cvec_copy(
        Span<ComplexDouble> destination,
        ReadOnlySpan<ComplexDouble> source,
        int count) =>
        ComplexVectorFloat.Copy(destination, source, count);

    public static void cvec_copyl(
        Span<ComplexLongDouble> destination,
        ReadOnlySpan<ComplexLongDouble> source,
        int count) =>
        ComplexVectorFloat.Copy(destination, source, count);

    public static void cvec_zerof(Span<ComplexFloat> destination, int count) =>
        ComplexVectorFloat.Zero(destination, count);

    public static void cvec_zero(Span<ComplexDouble> destination, int count) =>
        ComplexVectorFloat.Zero(destination, count);

    public static void cvec_zerol(Span<ComplexLongDouble> destination, int count) =>
        ComplexVectorFloat.Zero(destination, count);

    public static void cvec_setf(Span<ComplexFloat> destination, in ComplexFloat value, int count) =>
        ComplexVectorFloat.Set(destination, value, count);

    public static void cvec_set(Span<ComplexDouble> destination, in ComplexDouble value, int count) =>
        ComplexVectorFloat.Set(destination, value, count);

    public static void cvec_setl(
        Span<ComplexLongDouble> destination,
        in ComplexLongDouble value,
        int count) =>
        ComplexVectorFloat.Set(destination, value, count);

    public static void cvec_mulf(
        Span<ComplexFloat> destination,
        ReadOnlySpan<ComplexFloat> x,
        ReadOnlySpan<ComplexFloat> y,
        int count) =>
        ComplexVectorFloat.Multiply(destination, x, y, count);

    public static void cvec_mul(
        Span<ComplexDouble> destination,
        ReadOnlySpan<ComplexDouble> x,
        ReadOnlySpan<ComplexDouble> y,
        int count) =>
        ComplexVectorFloat.Multiply(destination, x, y, count);

    public static void cvec_mull(
        Span<ComplexLongDouble> destination,
        ReadOnlySpan<ComplexLongDouble> x,
        ReadOnlySpan<ComplexLongDouble> y,
        int count) =>
        ComplexVectorFloat.Multiply(destination, x, y, count);

    public static ComplexFloat cvec_dot_prodf(
        ReadOnlySpan<ComplexFloat> x,
        ReadOnlySpan<ComplexFloat> y,
        int count) =>
        ComplexVectorFloat.DotProduct(x, y, count);

    public static ComplexDouble cvec_dot_prod(
        ReadOnlySpan<ComplexDouble> x,
        ReadOnlySpan<ComplexDouble> y,
        int count) =>
        ComplexVectorFloat.DotProduct(x, y, count);

    public static ComplexLongDouble cvec_dot_prodl(
        ReadOnlySpan<ComplexLongDouble> x,
        ReadOnlySpan<ComplexLongDouble> y,
        int count) =>
        ComplexVectorFloat.DotProduct(x, y, count);

    public static ComplexFloat cvec_circular_dot_prodf(
        ReadOnlySpan<ComplexFloat> x,
        ReadOnlySpan<ComplexFloat> y,
        int count,
        int position) =>
        ComplexVectorFloat.CircularDotProduct(x, y, count, position);

    public static void cvec_lmsf(
        ReadOnlySpan<ComplexFloat> x,
        Span<ComplexFloat> y,
        int count,
        in ComplexFloat error) =>
        ComplexVectorFloat.Lms(x, y, count, error);

    public static void cvec_circular_lmsf(
        ReadOnlySpan<ComplexFloat> x,
        Span<ComplexFloat> y,
        int count,
        int position,
        in ComplexFloat error) =>
        ComplexVectorFloat.CircularLms(x, y, count, position, error);

    // complex_vector_int.h / complex_vector_int.c

    public static void cvec_copyi(
        Span<ComplexInt> destination,
        ReadOnlySpan<ComplexInt> source,
        int count) =>
        ComplexVectorInt.Copy(destination, source, count);

    public static void cvec_copyi16(
        Span<ComplexInt16> destination,
        ReadOnlySpan<ComplexInt16> source,
        int count) =>
        ComplexVectorInt.Copy(destination, source, count);

    public static void cvec_copyi32(
        Span<ComplexInt32> destination,
        ReadOnlySpan<ComplexInt32> source,
        int count) =>
        ComplexVectorInt.Copy(destination, source, count);

    public static void cvec_zeroi(Span<ComplexInt> destination, int count) =>
        ComplexVectorInt.Zero(destination, count);

    public static void cvec_zeroi16(Span<ComplexInt16> destination, int count) =>
        ComplexVectorInt.Zero(destination, count);

    public static void cvec_zeroi32(Span<ComplexInt32> destination, int count) =>
        ComplexVectorInt.Zero(destination, count);

    public static void cvec_seti(Span<ComplexInt> destination, in ComplexInt value, int count) =>
        ComplexVectorInt.Set(destination, value, count);

    public static void cvec_seti16(
        Span<ComplexInt16> destination,
        in ComplexInt16 value,
        int count) =>
        ComplexVectorInt.Set(destination, value, count);

    public static void cvec_seti32(
        Span<ComplexInt32> destination,
        in ComplexInt32 value,
        int count) =>
        ComplexVectorInt.Set(destination, value, count);

    public static ComplexInt32 cvec_dot_prodi16(
        ReadOnlySpan<ComplexInt16> x,
        ReadOnlySpan<ComplexInt16> y,
        int count) =>
        ComplexVectorInt.DotProduct(x, y, count);

    public static ComplexInt32 cvec_dot_prodi32(
        ReadOnlySpan<ComplexInt32> x,
        ReadOnlySpan<ComplexInt32> y,
        int count) =>
        ComplexVectorInt.DotProduct(x, y, count);

    public static ComplexInt32 cvec_circular_dot_prodi16(
        ReadOnlySpan<ComplexInt16> x,
        ReadOnlySpan<ComplexInt16> y,
        int count,
        int position) =>
        ComplexVectorInt.CircularDotProduct(x, y, count, position);

    public static void cvec_lmsi16(
        ReadOnlySpan<ComplexInt16> x,
        Span<ComplexInt16> y,
        int count,
        in ComplexInt16 error) =>
        ComplexVectorInt.Lms(x, y, count, error);

    public static void cvec_circular_lmsi16(
        ReadOnlySpan<ComplexInt16> x,
        Span<ComplexInt16> y,
        int count,
        int position,
        in ComplexInt16 error) =>
        ComplexVectorInt.CircularLms(x, y, count, position, error);
}
