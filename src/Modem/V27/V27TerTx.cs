/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * V27TerTx.cs - managed C# port of v27ter_tx.h and v27ter_tx.c
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2003 Steve Underwood
 *
 * This file is distributed under the GNU Lesser General Public License
 * version 2.1, matching the original source files.
 */

#nullable enable

namespace TKFaxEngine.Modem.V27;

/// <summary>Requests the next V.27ter transmit bit.</summary>
public delegate int V27TerTxGetBitHandler(object? userData);

/// <summary>Reports V.27ter transmitter status changes.</summary>
public delegate void V27TerTxStatusHandler(object? userData, int status);

/// <summary>Signal-status values used by the original asynchronous bit API.</summary>
public static class V27TerTxSignalStatus {
    public const int EndOfData = -7;
    public const int ShutdownComplete = -10;
}

/// <summary>Simple logging context associated with a V.27ter transmitter.</summary>
public sealed class V27TerTxLoggingState {
    public string Protocol { get; } = "V.27ter TX";

    public Action<string>? Handler { get; set; }

    internal void Write(string message) => Handler?.Invoke(message);
}

/// <summary>Floating-point complex value used by the V.27ter transmitter.</summary>
public readonly struct V27TerTxComplex {
    public V27TerTxComplex(float real, float imaginary) {
        Real = real;
        Imaginary = imaginary;
    }

    public float Real { get; }

    public float Imaginary { get; }

    public float Re => Real;

    public float Im => Imaginary;
}

/// <summary>
/// ITU-T V.27ter transmitter for 2,400 and 4,800 bit/s operation.
/// Audio is generated as signed 16-bit PCM at 8,000 samples per second.
/// </summary>
public sealed class V27TerTxState : IDisposable {
    public const int SampleRate = 8000;
    public const int FilterSteps = 9;
    public const int PulseShaper2400CoefficientSets = 20;
    public const int PulseShaper4800CoefficientSets = 5;
    public const float CarrierNominalFrequency = 1800.0f;
    public const float Dbm0MaxSinePower = 3.14f;

    public const int TrainingSegment1 = 0;
    public const int TrainingSegment2 = TrainingSegment1 + 320;
    public const int TrainingSegment3 = TrainingSegment2 + 32;
    public const int TrainingSegment4 = TrainingSegment3 + 50;
    public const int TrainingSegment5 = TrainingSegment4 + 1074;
    public const int TrainingEnd = TrainingSegment5 + 8;
    public const int TrainingShutdownEnd = TrainingEnd + 32;

    public const float PulseShaper2400Gain = 1.0f;
    public const float PulseShaper4800Gain = 1.0f;
    public const float FixedPointPulseShaper2400Gain = 0.875533f;
    public const float FixedPointPulseShaper4800Gain = 0.875534f;
    public const double FixedPointPulseShaper2400Scale = 28688.605380;
    public const double FixedPointPulseShaper4800Scale = 28688.606885;

    private static readonly int[] PhaseSteps4800 = { 1, 0, 2, 3, 6, 7, 5, 4 };
    private static readonly int[] PhaseSteps2400 = { 0, 2, 6, 4 };

    private static readonly V27TerTxComplex[] Constellation =
    {
        new( 1.414f,  0.000f),
        new( 1.000f,  1.000f),
        new( 0.000f,  1.414f),
        new(-1.000f,  1.000f),
        new(-1.414f,  0.000f),
        new(-1.000f, -1.000f),
        new( 0.000f, -1.414f),
        new( 1.000f, -1.000f)
    };

    private static readonly float[,] PulseShaper2400 =
    {
        { 0.0050262000f, 0.0107704139f, -0.0150784957f, -0.0753922186f, 0.5814534468f, 0.5814534467f, -0.0753922186f, -0.0150784958f, 0.0107704138f },
        { 0.0036769615f, 0.0132151788f, -0.0108416505f, -0.0962477546f, 0.6703977440f, 0.4915574819f, -0.0543875540f, -0.0179957590f, 0.0079493141f },
        { 0.0020271558f, 0.0151310510f, -0.0054150757f, -0.1159725361f, 0.7564987991f, 0.4025543098f, -0.0341116997f, -0.0195425378f, 0.0049156947f },
        { 0.0001575810f, 0.0163856892f, 0.0009922305f, -0.1335090670f, 0.8378713095f, 0.3161906111f, -0.0153166439f, -0.0197430347f, 0.0018355829f },
        { -0.0018345654f, 0.0168753676f, 0.0080958440f, -0.1477565768f, 0.9126905920f, 0.2340689766f, 0.0013877594f, -0.0186894802f, -0.0011314547f },
        { -0.0038402663f, 0.0165323368f, 0.0155436576f, -0.1576073958f, 0.9792460719f, 0.1576074027f, 0.0155436234f, -0.0165323579f, -0.0038401980f },
        { -0.0057441249f, 0.0153307048f, 0.0229275670f, -0.1619859170f, 1.0359921022f, 0.0880058111f, 0.0268485018f, -0.0134685577f, -0.0061665144f },
        { -0.0074304100f, 0.0132904398f, 0.0297988399f, -0.1598887983f, 1.0815943709f, 0.0262205341f, 0.0351527390f, -0.0097281388f, -0.0080126759f },
        { -0.0087894106f, 0.0104791762f, 0.0356867213f, -0.1504249558f, 1.1149702967f, -0.0270525930f, 0.0404511628f, -0.0055604096f, -0.0093110523f },
        { -0.0097237709f, 0.0070115966f, 0.0401196552f, -0.1328538467f, 1.1353220123f, -0.0713862188f, 0.0428697867f, -0.0012200205f, -0.0100260766f },
        { -0.0101544658f, 0.0030462740f, 0.0426483506f, -0.1066205506f, 1.1421607836f, -0.1066205506f, 0.0426483506f, 0.0030462740f, -0.0101544658f },
        { -0.0100260766f, -0.0012200205f, 0.0428697867f, -0.0713862187f, 1.1353220123f, -0.1328538468f, 0.0401196552f, 0.0070115966f, -0.0097237709f },
        { -0.0093110523f, -0.0055604096f, 0.0404511629f, -0.0270525929f, 1.1149702967f, -0.1504249558f, 0.0356867212f, 0.0104791761f, -0.0087894106f },
        { -0.0080126759f, -0.0097281388f, 0.0351527391f, 0.0262205342f, 1.0815943708f, -0.1598887984f, 0.0297988399f, 0.0132904397f, -0.0074304100f },
        { -0.0061665144f, -0.0134685577f, 0.0268485019f, 0.0880058111f, 1.0359921022f, -0.1619859171f, 0.0229275670f, 0.0153307048f, -0.0057441249f },
        { -0.0038401980f, -0.0165323579f, 0.0155436234f, 0.1576074029f, 0.9792460718f, -0.1576073958f, 0.0155436575f, 0.0165323368f, -0.0038402663f },
        { -0.0011314547f, -0.0186894801f, 0.0013877595f, 0.2340689767f, 0.9126905919f, -0.1477565768f, 0.0080958439f, 0.0168753675f, -0.0018345654f },
        { 0.0018355830f, -0.0197430346f, -0.0153166438f, 0.3161906112f, 0.8378713094f, -0.1335090671f, 0.0009922304f, 0.0163856892f, 0.0001575810f },
        { 0.0049156947f, -0.0195425377f, -0.0341116997f, 0.4025543099f, 0.7564987990f, -0.1159725361f, -0.0054150757f, 0.0151310509f, 0.0020271558f },
        { 0.0079493141f, -0.0179957590f, -0.0543875540f, 0.4915574821f, 0.6703977439f, -0.0962477546f, -0.0108416506f, 0.0132151788f, 0.0036769615f }
    };

    private static readonly float[,] PulseShaper4800 =
    {
        { 0.0020271593f, 0.0151309274f, -0.0054150609f, -0.1159724027f, 0.7564986489f, 0.4025541374f, -0.0341116447f, -0.0195424311f, 0.0049156263f },
        { -0.0057440218f, 0.0153306251f, 0.0229274764f, -0.1619858035f, 1.0359920119f, 0.0880056982f, 0.0268484410f, -0.0134684453f, -0.0061664720f },
        { -0.0101543453f, 0.0030463017f, 0.0426482251f, -0.1066205433f, 1.1421607236f, -0.1066205433f, 0.0426482251f, 0.0030463016f, -0.0101543453f },
        { -0.0061664720f, -0.0134684453f, 0.0268484411f, 0.0880056982f, 1.0359920119f, -0.1619858035f, 0.0229274764f, 0.0153306251f, -0.0057440218f },
        { 0.0049156264f, -0.0195424310f, -0.0341116447f, 0.4025541375f, 0.7564986489f, -0.1159724028f, -0.0054150609f, 0.0151309274f, 0.0020271593f }
    };

    private readonly float[] _rrcFilterReal = new float[FilterSteps];
    private readonly float[] _rrcFilterImaginary = new float[FilterSteps];

    private V27TerTxGetBitHandler _getBit = FakeGetBit;
    private object? _getBitUserData;
    private V27TerTxGetBitHandler _currentGetBit = FakeGetBit;
    private V27TerTxStatusHandler? _statusHandler;
    private object? _statusUserData;
    private float _gain2400;
    private float _gain4800;
    private int _rrcFilterStep;
    private uint _scrambleRegister;
    private int _scramblerPatternCount;
    private bool _inTraining;
    private int _trainingStep;
    private uint _carrierPhase;
    private int _carrierPhaseRate;
    private int _baudPhase;
    private int _constellationState;
    private bool _disposed;

    public V27TerTxState() {
        _carrierPhaseRate = PhaseRate(CarrierNominalFrequency);
        SetPower(-14.0f);
        Restart(4800, true);
    }

    public V27TerTxState(
        int bitRate,
        bool useTep,
        V27TerTxGetBitHandler getBit,
        object? userData = null) {
        ArgumentNullException.ThrowIfNull(getBit);
        _getBit = getBit;
        _getBitUserData = userData;
        _carrierPhaseRate = PhaseRate(CarrierNominalFrequency);
        SetPower(-14.0f);
        if (Restart(bitRate, useTep) != 0) {
            throw new ArgumentOutOfRangeException(nameof(bitRate), "V.27ter supports 2400 or 4800 bit/s.");
        }
    }

    public int BitRate { get; private set; }

    public bool InTraining => _inTraining;

    public int TrainingStep => _trainingStep;

    public bool ShutdownComplete => _trainingStep >= TrainingShutdownEnd;

    public float CarrierFrequency => Frequency(_carrierPhaseRate);

    public float Gain2400 => _gain2400;

    public float Gain4800 => _gain4800;

    public int ConstellationState => _constellationState;

    public V27TerTxLoggingState Logging { get; } = new();

    /// <summary>Generates a block of signed 16-bit, 8 kHz PCM audio.</summary>
    public int Transmit(Span<short> output) {
        ThrowIfDisposed();
        if (_trainingStep >= TrainingShutdownEnd) {
            return 0;
        }

        int sample;
        if (BitRate == 4800) {
            for (sample = 0; sample < output.Length; sample++) {
                if (++_baudPhase >= 5) {
                    _baudPhase -= 5;
                    InsertSymbol(GetBaud());
                }

                int phase = PulseShaper4800CoefficientSets - 1 - _baudPhase;
                float xReal = CircularDot(_rrcFilterReal, PulseShaper4800, phase, _rrcFilterStep);
                float xImaginary = CircularDot(_rrcFilterImaginary, PulseShaper4800, phase, _rrcFilterStep);
                V27TerTxComplex carrier = NextComplex(ref _carrierPhase, _carrierPhaseRate);
                float amplitude = (xReal * carrier.Real - xImaginary * carrier.Imaginary) * _gain4800;
                output[sample] = SaturateToInt16(amplitude);
            }
        } else {
            for (sample = 0; sample < output.Length; sample++) {
                _baudPhase += 3;
                if (_baudPhase >= 20) {
                    _baudPhase -= 20;
                    InsertSymbol(GetBaud());
                }

                int phase = PulseShaper2400CoefficientSets - 1 - _baudPhase;
                float xReal = CircularDot(_rrcFilterReal, PulseShaper2400, phase, _rrcFilterStep);
                float xImaginary = CircularDot(_rrcFilterImaginary, PulseShaper2400, phase, _rrcFilterStep);
                V27TerTxComplex carrier = NextComplex(ref _carrierPhase, _carrierPhaseRate);
                float amplitude = (xReal * carrier.Real - xImaginary * carrier.Imaginary) * _gain2400;
                output[sample] = SaturateToInt16(amplitude);
            }
        }

        return sample;
    }

    public int Transmit(short[] output, int offset, int length) {
        ArgumentNullException.ThrowIfNull(output);
        ValidateRange(output.Length, offset, length);
        return Transmit(output.AsSpan(offset, length));
    }

    /// <summary>Adjusts the transmitted signal power in dBm0.</summary>
    public void SetPower(float powerDbm0) {
        ThrowIfDisposed();
        float gain = DbToAmplitudeRatio(powerDbm0 - Dbm0MaxSinePower) * 32768.0f;
        _gain2400 = gain / PulseShaper2400Gain;
        _gain4800 = gain / PulseShaper4800Gain;
    }

    /// <summary>Reinitializes the transmitter for 2,400 or 4,800 bit/s.</summary>
    public int Restart(int bitRate, bool useTep) {
        ThrowIfDisposed();
        if (bitRate != 2400 && bitRate != 4800) {
            return -1;
        }

        BitRate = bitRate;
        Array.Clear(_rrcFilterReal, 0, _rrcFilterReal.Length);
        Array.Clear(_rrcFilterImaginary, 0, _rrcFilterImaginary.Length);
        _rrcFilterStep = 0;
        _scrambleRegister = 0x3C;
        _scramblerPatternCount = 0;
        _inTraining = true;
        _trainingStep = useTep ? TrainingSegment1 : TrainingSegment2;
        _carrierPhase = 0;
        _baudPhase = 0;
        _constellationState = 0;
        _currentGetBit = FakeGetBit;
        return 0;
    }

    public void SetGetBitHandler(V27TerTxGetBitHandler getBit, object? userData = null) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(getBit);
        if (_getBit == _currentGetBit) {
            _currentGetBit = getBit;
        }
        _getBit = getBit;
        _getBitUserData = userData;
    }

    public void SetModemStatusHandler(V27TerTxStatusHandler? handler, object? userData = null) {
        ThrowIfDisposed();
        _statusHandler = handler;
        _statusUserData = userData;
    }

    public int Release() {
        ThrowIfDisposed();
        return 0;
    }

    public static float GetPulseShaperCoefficient(int bitRate, int phase, int tap) {
        if ((uint)tap >= FilterSteps) {
            throw new ArgumentOutOfRangeException(nameof(tap));
        }

        return bitRate switch {
            2400 when (uint)phase < PulseShaper2400CoefficientSets => PulseShaper2400[phase, tap],
            4800 when (uint)phase < PulseShaper4800CoefficientSets => PulseShaper4800[phase, tap],
            2400 or 4800 => throw new ArgumentOutOfRangeException(nameof(phase)),
            _ => throw new ArgumentOutOfRangeException(nameof(bitRate))
        };
    }

    public static short[,] CreateFixedPointPulseShaper2400() =>
        CreateFixedPointTable(PulseShaper2400, FixedPointPulseShaper2400Scale);

    public static short[,] CreateFixedPointPulseShaper4800() =>
        CreateFixedPointTable(PulseShaper4800, FixedPointPulseShaper4800Scale);

    public void Dispose() {
        if (_disposed) {
            return;
        }

        Array.Clear(_rrcFilterReal, 0, _rrcFilterReal.Length);
        Array.Clear(_rrcFilterImaginary, 0, _rrcFilterImaginary.Length);
        _getBit = FakeGetBit;
        _currentGetBit = FakeGetBit;
        _getBitUserData = null;
        _statusHandler = null;
        _statusUserData = null;
        Logging.Handler = null;
        _disposed = true;
    }

    internal void Initialize(
        int bitRate,
        bool useTep,
        V27TerTxGetBitHandler getBit,
        object? userData) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(getBit);
        _getBit = getBit;
        _getBitUserData = userData;
        _carrierPhaseRate = PhaseRate(CarrierNominalFrequency);
        SetPower(-14.0f);
        if (Restart(bitRate, useTep) != 0) {
            throw new ArgumentOutOfRangeException(nameof(bitRate), "V.27ter supports 2400 or 4800 bit/s.");
        }
    }

    private void InsertSymbol(V27TerTxComplex symbol) {
        _rrcFilterReal[_rrcFilterStep] = symbol.Real;
        _rrcFilterImaginary[_rrcFilterStep] = symbol.Imaginary;
        if (++_rrcFilterStep >= FilterSteps) {
            _rrcFilterStep = 0;
        }
    }

    private V27TerTxComplex GetBaud() {
        if (_inTraining) {
            _trainingStep++;
            if (_trainingStep <= TrainingSegment5) {
                if (_trainingStep <= TrainingSegment4) {
                    if (_trainingStep <= TrainingSegment2) {
                        return Constellation[0];
                    }

                    if (_trainingStep <= TrainingSegment3) {
                        return default;
                    }

                    _constellationState = (_constellationState + 4) & 7;
                    return Constellation[_constellationState];
                }

                int reversal = GetScrambledBit() << 2;
                _ = GetScrambledBit();
                _ = GetScrambledBit();
                _constellationState = (_constellationState + reversal) & 7;
                return Constellation[_constellationState];
            }

            if (_trainingStep == TrainingEnd + 1) {
                _currentGetBit = _getBit;
                _inTraining = false;
                Logging.Write("Training completed; switching to user data");
            }

            if (_trainingStep == TrainingShutdownEnd) {
                _statusHandler?.Invoke(_statusUserData, V27TerTxSignalStatus.ShutdownComplete);
                Logging.Write("Shutdown sequence completed");
            }
        }

        int bits;
        if (BitRate == 4800) {
            bits = GetScrambledBit();
            bits = (bits << 1) | GetScrambledBit();
            bits = (bits << 1) | GetScrambledBit();
            bits = PhaseSteps4800[bits];
        } else {
            bits = GetScrambledBit();
            bits = (bits << 1) | GetScrambledBit();
            bits = PhaseSteps2400[bits];
        }

        _constellationState = (_constellationState + bits) & 7;
        return Constellation[_constellationState];
    }

    private int GetScrambledBit() {
        int bit = _currentGetBit(_getBitUserData);
        if (bit == V27TerTxSignalStatus.EndOfData) {
            _statusHandler?.Invoke(_statusUserData, V27TerTxSignalStatus.EndOfData);
            _currentGetBit = FakeGetBit;
            _inTraining = true;
            bit = 1;
        }
        return Scramble(bit);
    }

    private int Scramble(int inputBit) {
        int outputBit = (inputBit ^ (int)(_scrambleRegister >> 5) ^ (int)(_scrambleRegister >> 6)) & 1;
        if (_scramblerPatternCount >= 33) {
            outputBit ^= 1;
            _scramblerPatternCount = 0;
        } else {
            int repeated =
                ((int)(_scrambleRegister >> 7) ^ outputBit) &
                ((int)(_scrambleRegister >> 8) ^ outputBit) &
                ((int)(_scrambleRegister >> 11) ^ outputBit) & 1;
            _scramblerPatternCount = repeated != 0 ? 0 : _scramblerPatternCount + 1;
        }

        _scrambleRegister = unchecked((_scrambleRegister << 1) | (uint)outputBit);
        return outputBit;
    }

    private static int FakeGetBit(object? userData) {
        _ = userData;
        return 1;
    }

    private static float CircularDot(float[] signal, float[,] coefficients, int phase, int position) {
        float sum = 0.0f;
        int index = position;
        for (int tap = 0; tap < FilterSteps; tap++) {
            sum += signal[index] * coefficients[phase, tap];
            if (++index == FilterSteps) {
                index = 0;
            }
        }
        return sum;
    }

    private static int PhaseRate(float frequency) =>
        unchecked((int)Math.Round(frequency * (4294967296.0 / SampleRate), MidpointRounding.ToEven));

    private static float Frequency(int phaseRate) =>
        (float)(phaseRate * (SampleRate / 4294967296.0));

    private static V27TerTxComplex NextComplex(ref uint phase, int phaseRate) {
        double angle = phase * (2.0 * Math.PI / 4294967296.0);
        var value = new V27TerTxComplex((float)Math.Cos(angle), (float)Math.Sin(angle));
        phase = unchecked(phase + (uint)phaseRate);
        return value;
    }

    private static float DbToAmplitudeRatio(float decibels) =>
        MathF.Pow(10.0f, decibels / 20.0f);

    private static short SaturateToInt16(float value) {
        int rounded = (int)MathF.Round(value, MidpointRounding.ToEven);
        if (rounded > short.MaxValue) {
            return short.MaxValue;
        }
        if (rounded < short.MinValue) {
            return short.MinValue;
        }
        return (short)rounded;
    }

    private static short[,] CreateFixedPointTable(float[,] source, double scale) {
        int rows = source.GetLength(0);
        int columns = source.GetLength(1);
        var result = new short[rows, columns];
        for (int row = 0; row < rows; row++) {
            for (int column = 0; column < columns; column++) {
                double scaled = source[row, column] * scale;
                int rounded = (int)(scaled + (scaled >= 0.0 ? 0.5 : -0.5));
                result[row, column] = unchecked((short)rounded);
            }
        }
        return result;
    }

    private static void ValidateRange(int length, int offset, int count) {
        if (offset < 0 || count < 0 || offset > length - count) {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}

/// <summary>C-style compatibility entry points matching v27ter_tx.h.</summary>
public static class V27TerTxApi {
    public const int SIG_STATUS_END_OF_DATA = V27TerTxSignalStatus.EndOfData;
    public const int SIG_STATUS_SHUTDOWN_COMPLETE = V27TerTxSignalStatus.ShutdownComplete;

    public static V27TerTxState? v27ter_tx_init(
        V27TerTxState? state,
        int bitRate,
        bool tep,
        V27TerTxGetBitHandler getBit,
        object? userData) {
        ArgumentNullException.ThrowIfNull(getBit);
        if (bitRate != 2400 && bitRate != 4800) {
            return null;
        }

        if (state is null) {
            return new V27TerTxState(bitRate, tep, getBit, userData);
        }

        state.Initialize(bitRate, tep, getBit, userData);
        return state;
    }

    public static int v27ter_tx(V27TerTxState state, short[] output, int length) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(output);
        if ((uint)length > (uint)output.Length) {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        return state.Transmit(output.AsSpan(0, length));
    }

    public static void v27ter_tx_power(V27TerTxState state, float power) =>
        state.SetPower(power);

    public static int v27ter_tx_restart(V27TerTxState state, int bitRate, bool tep) =>
        state.Restart(bitRate, tep);

    public static int v27ter_tx_release(V27TerTxState state) => state.Release();

    public static int v27ter_tx_free(V27TerTxState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.Dispose();
        return 0;
    }

    public static V27TerTxLoggingState v27ter_tx_get_logging_state(V27TerTxState state) =>
        state.Logging;

    public static void v27ter_tx_set_get_bit(
        V27TerTxState state,
        V27TerTxGetBitHandler getBit,
        object? userData) => state.SetGetBitHandler(getBit, userData);

    public static void v27ter_tx_set_modem_status_handler(
        V27TerTxState state,
        V27TerTxStatusHandler? handler,
        object? userData) => state.SetModemStatusHandler(handler, userData);
}
