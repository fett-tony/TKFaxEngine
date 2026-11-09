/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * V29Tx.cs - managed C# port of v29tx.h and v29tx.c
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2003 Steve Underwood
 *
 * This file is distributed under the GNU Lesser General Public License
 * version 2.1, matching the original source files.
 */

#nullable enable

namespace TKFaxEngine.Modem.V29;

/// <summary>Requests the next V.29 transmit bit.</summary>
public delegate int V29TxGetBitHandler(object? userData);

/// <summary>Reports V.29 transmitter status changes.</summary>
public delegate void V29TxStatusHandler(object? userData, int status);

/// <summary>Signal-status values used by the original asynchronous bit API.</summary>
public static class V29TxSignalStatus {
    public const int EndOfData = -7;
    public const int ShutdownComplete = -10;
}

/// <summary>Simple logging context associated with a V.29 transmitter.</summary>
public sealed class V29TxLoggingState {
    public string Protocol { get; } = "V.29 TX";

    public Action<string>? Handler { get; set; }

    internal void Write(string message) => Handler?.Invoke(message);
}

/// <summary>Floating-point complex value used by the V.29 transmitter.</summary>
public readonly struct V29TxComplex {
    public V29TxComplex(float real, float imaginary) {
        Real = real;
        Imaginary = imaginary;
    }

    public float Real { get; }

    public float Imaginary { get; }

    public float Re => Real;

    public float Im => Imaginary;
}

/// <summary>
/// ITU-T V.29 transmitter for 4,800, 7,200 and 9,600 bit/s operation.
/// Audio is generated as signed 16-bit PCM at 8,000 samples per second.
/// </summary>
public sealed class V29TxState : IDisposable {
    public const int SampleRate = 8000;
    public const int FilterSteps = 9;
    public const int PulseShaperCoefficientSets = 10;
    public const float CarrierNominalFrequency = 1700.0f;
    public const float Dbm0MaxSinePower = 3.14f;

    public const int TrainingSegmentTep = 0;
    public const int TrainingSegment1 = TrainingSegmentTep + 480;
    public const int TrainingSegment2 = TrainingSegment1 + 48;
    public const int TrainingSegment3 = TrainingSegment2 + 128;
    public const int TrainingSegment4 = TrainingSegment3 + 384;
    public const int TrainingEnd = TrainingSegment4 + 48;
    public const int TrainingShutdownEnd = TrainingEnd + 32;

    public const float PulseShaperGain = 1.0f;
    public const float FixedPointPulseShaperGain = 0.948561f;
    public const double FixedPointPulseShaperScale = 31081.491463;

    private static readonly int[] PhaseSteps9600 = { 1, 0, 2, 3, 6, 7, 5, 4 };
    private static readonly int[] PhaseSteps4800 = { 0, 2, 6, 4 };

    private static readonly V29TxComplex[] AbabConstellation =
    {
        new( 3.0f, -3.0f),
        new(-3.0f,  0.0f),
        new( 1.0f, -1.0f),
        new(-3.0f,  0.0f),
        new( 0.0f, -3.0f),
        new(-3.0f,  0.0f)
    };

    private static readonly V29TxComplex[] CdcdConstellation =
    {
        new( 3.0f,  0.0f),
        new(-3.0f,  3.0f),
        new( 3.0f,  0.0f),
        new(-1.0f,  1.0f),
        new( 3.0f,  0.0f),
        new( 0.0f,  3.0f)
    };

    private static readonly V29TxComplex[] MainConstellation =
    {
        new( 3.0f,  0.0f),
        new( 1.0f,  1.0f),
        new( 0.0f,  3.0f),
        new(-1.0f,  1.0f),
        new(-3.0f,  0.0f),
        new(-1.0f, -1.0f),
        new( 0.0f, -3.0f),
        new( 1.0f, -1.0f),
        new( 5.0f,  0.0f),
        new( 3.0f,  3.0f),
        new( 0.0f,  5.0f),
        new(-3.0f,  3.0f),
        new(-5.0f,  0.0f),
        new(-3.0f, -3.0f),
        new( 0.0f, -5.0f),
        new( 3.0f, -3.0f)
    };

    private static readonly float[,] PulseShaper =
    {
        { -0.0028949626f, -0.0180558777f,  0.0644370035f, -0.1680546392f,  0.6136030985f,  0.6136030984f, -0.1680546392f,  0.0644370034f, -0.0180558778f },
        {  0.0031457248f, -0.0296755147f,  0.0821538018f, -0.1948071696f,  0.7563219631f,  0.4608861941f, -0.1273859915f,  0.0418434579f, -0.0059021774f },
        {  0.0095859909f, -0.0389394472f,  0.0918555210f, -0.2016880234f,  0.8793516917f,  0.3081345068f, -0.0792085179f,  0.0176601554f,  0.0051283325f },
        {  0.0153896883f, -0.0441001646f,  0.0909724653f, -0.1838386340f,  0.9741012686f,  0.1647552955f, -0.0297442724f, -0.0050682341f,  0.0137350940f },
        {  0.0194884088f, -0.0437412561f,  0.0779044330f, -0.1380831560f,  1.0338274098f,  0.0388498604f,  0.0155354801f, -0.0238603979f,  0.0191007894f },
        {  0.0209425252f, -0.0370198693f,  0.0523524602f, -0.0633894605f,  1.0542286891f, -0.0633894606f,  0.0523524602f, -0.0370198693f,  0.0209425251f },
        {  0.0191007894f, -0.0238603978f,  0.0155354801f,  0.0388498605f,  1.0338274098f, -0.1380831561f,  0.0779044330f, -0.0437412561f,  0.0194884087f },
        {  0.0137350940f, -0.0050682341f, -0.0297442724f,  0.1647552955f,  0.9741012686f, -0.1838386340f,  0.0909724652f, -0.0441001646f,  0.0153896883f },
        {  0.0051283326f,  0.0176601554f, -0.0792085179f,  0.3081345069f,  0.8793516917f, -0.2016880235f,  0.0918555209f, -0.0389394473f,  0.0095859909f },
        { -0.0059021774f,  0.0418434580f, -0.1273859915f,  0.4608861942f,  0.7563219631f, -0.1948071696f,  0.0821538018f, -0.0296755147f,  0.0031457248f }
    };

    private readonly float[] _rrcFilterReal = new float[FilterSteps];
    private readonly float[] _rrcFilterImaginary = new float[FilterSteps];

    private V29TxGetBitHandler _getBit = FakeGetBit;
    private object? _getBitUserData;
    private V29TxGetBitHandler _currentGetBit = FakeGetBit;
    private V29TxStatusHandler? _statusHandler;
    private object? _statusUserData;
    private float _baseGain;
    private float _gain;
    private int _rrcFilterStep;
    private uint _scrambleRegister;
    private byte _trainingScrambleRegister;
    private bool _inTraining;
    private int _trainingStep;
    private int _trainingOffset;
    private uint _carrierPhase;
    private int _carrierPhaseRate;
    private int _baudPhase;
    private int _constellationState;
    private bool _disposed;

    public V29TxState() {
        _carrierPhaseRate = PhaseRate(CarrierNominalFrequency);
        SetPower(-14.0f);
        Restart(9600, true);
    }

    public V29TxState(
        int bitRate,
        bool useTep,
        V29TxGetBitHandler getBit,
        object? userData = null) {
        ArgumentNullException.ThrowIfNull(getBit);
        _getBit = getBit;
        _getBitUserData = userData;
        _carrierPhaseRate = PhaseRate(CarrierNominalFrequency);
        SetPower(-14.0f);
        if (Restart(bitRate, useTep) != 0) {
            throw new ArgumentOutOfRangeException(nameof(bitRate), "V.29 supports 4800, 7200 or 9600 bit/s.");
        }
    }

    public int BitRate { get; private set; }

    public bool InTraining => _inTraining;

    public int TrainingStep => _trainingStep;

    public bool ShutdownComplete => _trainingStep >= TrainingShutdownEnd;

    public float CarrierFrequency => Frequency(_carrierPhaseRate);

    public float BaseGain => _baseGain;

    public float Gain => _gain;

    public int ConstellationState => _constellationState;

    public V29TxLoggingState Logging { get; } = new();

    /// <summary>Generates a block of signed 16-bit, 8 kHz PCM audio.</summary>
    public int Transmit(Span<short> output) {
        ThrowIfDisposed();
        if (_trainingStep >= TrainingShutdownEnd) {
            return 0;
        }

        int sample;
        for (sample = 0; sample < output.Length; sample++) {
            _baudPhase += 3;
            if (_baudPhase >= 10) {
                _baudPhase -= 10;
                InsertSymbol(GetBaud());
            }

            int phase = PulseShaperCoefficientSets - 1 - _baudPhase;
            float xReal = CircularDot(_rrcFilterReal, phase, _rrcFilterStep);
            float xImaginary = CircularDot(_rrcFilterImaginary, phase, _rrcFilterStep);
            V29TxComplex carrier = NextComplex(ref _carrierPhase, _carrierPhaseRate);
            float amplitude = (xReal * carrier.Real - xImaginary * carrier.Imaginary) * _gain;
            output[sample] = SaturateToInt16(amplitude);
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
        _baseGain = DbToAmplitudeRatio(powerDbm0 - Dbm0MaxSinePower) * 32768.0f / PulseShaperGain;
        SetWorkingGain();
    }

    /// <summary>Reinitializes the transmitter for 4,800, 7,200 or 9,600 bit/s.</summary>
    public int Restart(int bitRate, bool useTep) {
        ThrowIfDisposed();

        int trainingOffset;
        switch (bitRate) {
            case 9600:
                trainingOffset = 0;
                break;
            case 7200:
                trainingOffset = 2;
                break;
            case 4800:
                trainingOffset = 4;
                break;
            default:
                return -1;
        }

        BitRate = bitRate;
        _trainingOffset = trainingOffset;
        SetWorkingGain();
        Array.Clear(_rrcFilterReal, 0, _rrcFilterReal.Length);
        Array.Clear(_rrcFilterImaginary, 0, _rrcFilterImaginary.Length);
        _rrcFilterStep = 0;
        _scrambleRegister = 0;
        _trainingScrambleRegister = 0x2A;
        _inTraining = true;
        _trainingStep = useTep ? TrainingSegmentTep : TrainingSegment1;
        _carrierPhase = 0;
        _baudPhase = 0;
        _constellationState = 0;
        _currentGetBit = FakeGetBit;
        Logging.Write($"Restarting V.29 at {bitRate} bit/s");
        return 0;
    }

    public void SetGetBitHandler(V29TxGetBitHandler getBit, object? userData = null) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(getBit);
        if (_getBit == _currentGetBit) {
            _currentGetBit = getBit;
        }
        _getBit = getBit;
        _getBitUserData = userData;
    }

    public void SetModemStatusHandler(V29TxStatusHandler? handler, object? userData = null) {
        ThrowIfDisposed();
        _statusHandler = handler;
        _statusUserData = userData;
    }

    public int Release() {
        ThrowIfDisposed();
        return 0;
    }

    public static float GetPulseShaperCoefficient(int phase, int tap) {
        if ((uint)phase >= PulseShaperCoefficientSets) {
            throw new ArgumentOutOfRangeException(nameof(phase));
        }
        if ((uint)tap >= FilterSteps) {
            throw new ArgumentOutOfRangeException(nameof(tap));
        }
        return PulseShaper[phase, tap];
    }

    public static V29TxComplex GetMainConstellationPoint(int index) {
        if ((uint)index >= MainConstellation.Length) {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        return MainConstellation[index];
    }

    public static V29TxComplex GetAbabConstellationPoint(int index) {
        if ((uint)index >= AbabConstellation.Length) {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        return AbabConstellation[index];
    }

    public static V29TxComplex GetCdcdConstellationPoint(int index) {
        if ((uint)index >= CdcdConstellation.Length) {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        return CdcdConstellation[index];
    }

    public static short[,] CreateFixedPointPulseShaper() =>
        CreateFixedPointTable(PulseShaper, FixedPointPulseShaperScale);

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
        V29TxGetBitHandler getBit,
        object? userData) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(getBit);
        _getBit = getBit;
        _getBitUserData = userData;
        _carrierPhaseRate = PhaseRate(CarrierNominalFrequency);
        SetPower(-14.0f);
        if (Restart(bitRate, useTep) != 0) {
            throw new ArgumentOutOfRangeException(nameof(bitRate), "V.29 supports 4800, 7200 or 9600 bit/s.");
        }
    }

    private void InsertSymbol(V29TxComplex symbol) {
        _rrcFilterReal[_rrcFilterStep] = symbol.Real;
        _rrcFilterImaginary[_rrcFilterStep] = symbol.Imaginary;
        if (++_rrcFilterStep >= FilterSteps) {
            _rrcFilterStep = 0;
        }
    }

    private V29TxComplex GetBaud() {
        if (_inTraining) {
            _trainingStep++;
            if (_trainingStep <= TrainingSegment4) {
                if (_trainingStep <= TrainingSegment3) {
                    if (_trainingStep <= TrainingSegment1) {
                        return MainConstellation[0];
                    }

                    if (_trainingStep <= TrainingSegment2) {
                        return default;
                    }

                    return AbabConstellation[(_trainingStep & 1) + _trainingOffset];
                }

                int bit = _trainingScrambleRegister & 1;
                _trainingScrambleRegister >>= 1;
                _trainingScrambleRegister |= (byte)(((bit ^ _trainingScrambleRegister) & 1) << 6);
                return CdcdConstellation[bit + _trainingOffset];
            }

            if (_trainingStep == TrainingEnd + 1) {
                _currentGetBit = _getBit;
                _inTraining = false;
                Logging.Write("Training completed; switching to user data");
            }

            if (_trainingStep == TrainingShutdownEnd) {
                _statusHandler?.Invoke(_statusUserData, V29TxSignalStatus.ShutdownComplete);
                Logging.Write("Shutdown sequence completed");
            }
        }

        int amplitudeIndex = 0;
        if (BitRate == 9600 && GetScrambledBit() != 0) {
            amplitudeIndex = 8;
        }

        int bits = GetScrambledBit();
        bits = (bits << 1) | GetScrambledBit();
        if (BitRate == 4800) {
            bits = PhaseSteps4800[bits];
        } else {
            bits = (bits << 1) | GetScrambledBit();
            bits = PhaseSteps9600[bits];
        }

        _constellationState = (_constellationState + bits) & 7;
        return MainConstellation[amplitudeIndex | _constellationState];
    }

    private int GetScrambledBit() {
        int bit = _currentGetBit(_getBitUserData);
        if (bit == V29TxSignalStatus.EndOfData) {
            _statusHandler?.Invoke(_statusUserData, V29TxSignalStatus.EndOfData);
            _currentGetBit = FakeGetBit;
            _inTraining = true;
            bit = 1;
        }

        int outputBit =
            (bit ^ (int)(_scrambleRegister >> 17) ^ (int)(_scrambleRegister >> 22)) & 1;
        _scrambleRegister = unchecked((_scrambleRegister << 1) | (uint)outputBit);
        return outputBit;
    }

    private void SetWorkingGain() {
        _gain = BitRate switch {
            9600 => 0.387f * _baseGain,
            7200 => 0.605f * _baseGain,
            4800 => 0.470f * _baseGain,
            _ => 0.0f
        };
    }

    private static int FakeGetBit(object? userData) {
        _ = userData;
        return 1;
    }

    private static float CircularDot(float[] signal, int phase, int position) {
        float sum = 0.0f;
        int index = position;
        for (int tap = 0; tap < FilterSteps; tap++) {
            sum += signal[index] * PulseShaper[phase, tap];
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

    private static V29TxComplex NextComplex(ref uint phase, int phaseRate) {
        double angle = phase * (2.0 * Math.PI / 4294967296.0);
        var value = new V29TxComplex((float)Math.Cos(angle), (float)Math.Sin(angle));
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

/// <summary>C-style compatibility entry points matching v29tx.h.</summary>
public static class V29TxApi {
    public const int SIG_STATUS_END_OF_DATA = V29TxSignalStatus.EndOfData;
    public const int SIG_STATUS_SHUTDOWN_COMPLETE = V29TxSignalStatus.ShutdownComplete;

    public static V29TxState? v29_tx_init(
        V29TxState? state,
        int bitRate,
        bool tep,
        V29TxGetBitHandler getBit,
        object? userData) {
        ArgumentNullException.ThrowIfNull(getBit);
        if (bitRate != 4800 && bitRate != 7200 && bitRate != 9600) {
            return null;
        }

        if (state is null) {
            return new V29TxState(bitRate, tep, getBit, userData);
        }

        state.Initialize(bitRate, tep, getBit, userData);
        return state;
    }

    public static int v29_tx(V29TxState state, short[] output, int length) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(output);
        if ((uint)length > (uint)output.Length) {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        return state.Transmit(output.AsSpan(0, length));
    }

    public static void v29_tx_power(V29TxState state, float power) =>
        state.SetPower(power);

    public static int v29_tx_restart(V29TxState state, int bitRate, bool tep) =>
        state.Restart(bitRate, tep);

    public static int v29_tx_release(V29TxState state) => state.Release();

    public static int v29_tx_free(V29TxState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.Dispose();
        return 0;
    }

    public static V29TxLoggingState v29_tx_get_logging_state(V29TxState state) =>
        state.Logging;

    public static void v29_tx_set_get_bit(
        V29TxState state,
        V29TxGetBitHandler getBit,
        object? userData) => state.SetGetBitHandler(getBit, userData);

    public static void v29_tx_set_modem_status_handler(
        V29TxState state,
        V29TxStatusHandler? handler,
        object? userData) => state.SetModemStatusHandler(handler, userData);
}
