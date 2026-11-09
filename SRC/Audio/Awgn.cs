/*
 * TKFaxEngine - managed C# port
 *
 * Awgn.cs
 *
 * Combined and ported from awgn.c and awgn.h.
 * Implements an additive white Gaussian noise generator.
 *
 * Original implementation by Steve Underwood.
 * Licensed under the GNU Lesser General Public License version 2.1.
 */

#nullable enable

namespace TKFaxEngine.Audio;

/// <summary>
/// Additive white Gaussian noise generator using the original shuffled
/// three-generator random source and the Box-Muller polar method.
/// </summary>
public sealed class Awgn {
    private const int M1 = 259200;
    private const int Ia1 = 7141;
    private const int Ic1 = 54773;
    private const double Rm1 = 1.0 / M1;

    private const int M2 = 134456;
    private const int Ia2 = 8121;
    private const int Ic2 = 28411;
    private const double Rm2 = 1.0 / M2;

    private const int M3 = 243000;
    private const int Ia3 = 4561;
    private const int Ic3 = 51349;

    /// <summary>
    /// Difference between a full-scale square wave and the 0 dBm0 reference.
    /// This matches DBM0_MAX_POWER from the native telephony definitions.
    /// </summary>
    public const double Dbm0MaximumPower = 3.14 + 3.02;

    private readonly double[] _randomTable = new double[97];

    private double _rms;
    private bool _odd;
    private double _secondAmplitude;
    private int _ix1;
    private int _ix2;
    private int _ix3;

    /// <summary>
    /// Creates a generator whose level is expressed in dBm0.
    /// </summary>
    public Awgn(int seed, double levelDbm0) {
        RestartDbm0(seed, levelDbm0);
    }

    /// <summary>
    /// Creates a generator whose level is expressed relative to overload.
    /// </summary>
    public static Awgn FromDbov(int seed, double levelDbov) {
        var generator = new Awgn(seed, 0.0);
        generator.RestartDbov(seed, levelDbov);
        return generator;
    }

    /// <summary>
    /// Restarts the generator with a level expressed in dBm0.
    /// </summary>
    public void RestartDbm0(int seed, double levelDbm0) {
        RestartDbov(seed, levelDbm0 - Dbm0MaximumPower);
    }

    /// <summary>
    /// Restarts the generator with a level expressed relative to overload.
    /// </summary>
    public void RestartDbov(int seed, double levelDbov) {
        InitialiseRandom(seed);
        _rms = Math.Pow(10.0, levelDbov / 20.0) * 32768.0;
        _secondAmplitude = 0.0;
        _odd = true;
    }

    /// <summary>
    /// Gets the RMS sample scaling currently used by the generator.
    /// </summary>
    public double Rms => _rms;

    /// <summary>
    /// Generates the next saturated 16-bit Gaussian noise sample.
    /// </summary>
    public short NextSample() {
        double amplitude;

        _odd = !_odd;
        if (_odd) {
            amplitude = _secondAmplitude;
        } else {
            double radiusSquared;
            double value1;
            double value2;

            do {
                value1 = 2.0 * NextUniform() - 1.0;
                value2 = 2.0 * NextUniform() - 1.0;
                radiusSquared = value1 * value1 + value2 * value2;
            }
            while (radiusSquared >= 1.0 || radiusSquared <= double.Epsilon);

            double multiplier = Math.Sqrt(-2.0 * Math.Log(radiusSquared) / radiusSquared);
            _secondAmplitude = value1 * multiplier;
            amplitude = value2 * multiplier;
        }

        return Saturate(amplitude * _rms);
    }

    /// <summary>
    /// Fills a sample buffer with Gaussian noise.
    /// </summary>
    public void Fill(Span<short> destination) {
        for (int i = 0; i < destination.Length; i++)
            destination[i] = NextSample();
    }

    /// <summary>
    /// Adds generated noise to an existing sample buffer using saturated arithmetic.
    /// </summary>
    public void AddTo(ReadOnlySpan<short> source, Span<short> destination) {
        if (destination.Length < source.Length)
            throw new ArgumentException("The destination buffer is too small.", nameof(destination));

        for (int i = 0; i < source.Length; i++)
            destination[i] = Saturate(source[i] + NextSample());
    }

    private void InitialiseRandom(int seed) {
        long positiveSeed = seed;
        if (positiveSeed < 0)
            positiveSeed = -positiveSeed;

        _ix1 = (int)((Ic1 + positiveSeed) % M1);
        _ix1 = (int)(((long)Ia1 * _ix1 + Ic1) % M1);
        _ix2 = _ix1 % M2;
        _ix1 = (int)(((long)Ia1 * _ix1 + Ic1) % M1);
        _ix3 = _ix1 % M3;

        for (int index = 0; index < _randomTable.Length; index++) {
            _ix1 = (int)(((long)Ia1 * _ix1 + Ic1) % M1);
            _ix2 = (int)(((long)Ia2 * _ix2 + Ic2) % M2);
            _randomTable[index] = (_ix1 + _ix2 * Rm2) * Rm1;
        }
    }

    private double NextUniform() {
        _ix1 = (int)(((long)Ia1 * _ix1 + Ic1) % M1);
        _ix2 = (int)(((long)Ia2 * _ix2 + Ic2) % M2);
        _ix3 = (int)(((long)Ia3 * _ix3 + Ic3) % M3);

        int index = 97 * _ix3 / M3;
        if ((uint)index >= _randomTable.Length)
            throw new InvalidOperationException("The AWGN random table index is invalid.");

        double result = _randomTable[index];
        _randomTable[index] = (_ix1 + _ix2 * Rm2) * Rm1;
        return result;
    }

    private static short Saturate(double value) {
        if (value >= short.MaxValue)
            return short.MaxValue;
        if (value <= short.MinValue)
            return short.MinValue;

        return (short)Math.Round(value, MidpointRounding.ToEven);
    }
}
