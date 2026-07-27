/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * SigTone.cs - Managed C# port of sig_tone.c and sig_tone.h
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2004 Steve Underwood
 *
 * This file is distributed under the terms of the GNU Lesser General Public
 * License version 2.1, matching the original source files.
 */

#nullable enable

namespace TKFaxEngine.Audio;

/// <summary>Supported legacy signalling-tone sets.</summary>
public enum SigToneType {
    Tone2280Hz = 1,
    Tone2600Hz = 2,
    Tone2400Hz2600Hz = 3
}

/// <summary>Transmit/receive control and report flags.</summary>
[Flags]
public enum SigToneMode {
    None = 0,
    Tone1Present = 0x001,
    Tone1Change = 0x002,
    Tone2Present = 0x004,
    Tone2Change = 0x008,
    TransmitPassthrough = 0x010,
    ReceivePassthrough = 0x040,
    ReceiveFilterTone = 0x080,
    TransmitUpdateRequest = 0x100,
    ReceiveUpdateRequest = 0x200
}

/// <summary>Callback used for signalling-tone state changes and timeout requests.</summary>
public delegate void SigToneReportHandler(
    object? userData,
    int signal,
    int level,
    int delay);

/// <summary>Constants and descriptor tables for legacy signalling tones.</summary>
public static class SigTone {
    public const int SampleRate = 8000;

    public const int SIG_TONE_2280HZ = 1;
    public const int SIG_TONE_2600HZ = 2;
    public const int SIG_TONE_2400HZ_2600HZ = 3;

    public const int SIG_TONE_1_PRESENT = 0x001;
    public const int SIG_TONE_1_CHANGE = 0x002;
    public const int SIG_TONE_2_PRESENT = 0x004;
    public const int SIG_TONE_2_CHANGE = 0x008;
    public const int SIG_TONE_TX_PASSTHROUGH = 0x010;
    public const int SIG_TONE_RX_PASSTHROUGH = 0x040;
    public const int SIG_TONE_RX_FILTER_TONE = 0x080;
    public const int SIG_TONE_TX_UPDATE_REQUEST = 0x100;
    public const int SIG_TONE_RX_UPDATE_REQUEST = 0x200;

    internal const int PresentMask =
        SIG_TONE_1_PRESENT | SIG_TONE_2_PRESENT;

    internal const int ChangeMask =
        SIG_TONE_1_CHANGE | SIG_TONE_2_CHANGE;

    internal static int MillisecondsToSamples(int milliseconds) =>
        checked(milliseconds * SampleRate / 1000);
}

internal sealed class SigToneNotchCoefficients {
    internal SigToneNotchCoefficients(
        float[] a1,
        float[] b1,
        float[] a2,
        float[] b2) {
        A1 = a1;
        B1 = b1;
        A2 = a2;
        B2 = b2;
    }

    internal float[] A1 { get; }

    internal float[] B1 { get; }

    internal float[] A2 { get; }

    internal float[] B2 { get; }
}

internal sealed class SigToneFlatCoefficients {
    internal SigToneFlatCoefficients(float[] a, float[] b) {
        A = a;
        B = b;
    }

    internal float[] A { get; }

    internal float[] B { get; }
}

internal sealed class SigToneDescriptor {
    internal required int[] ToneFrequencies { get; init; }

    internal required int[,] ToneAmplitudes { get; init; }

    internal int HighLowTimeout { get; init; }

    internal int SharpFlatTimeout { get; init; }

    internal int NotchLagTime { get; init; }

    internal int ToneOnCheckTime { get; init; }

    internal int ToneOffCheckTime { get; init; }

    internal int Tones { get; init; }

    internal required SigToneNotchCoefficients?[] Notches { get; init; }

    internal SigToneFlatCoefficients? Flat { get; init; }

    internal float DetectionRatioDb { get; init; }

    internal float SharpDetectionThresholdDbm0 { get; init; }

    internal float FlatDetectionThresholdDbm0 { get; init; }
}

internal sealed class SigToneRxFilterState {
    internal readonly float[] NotchZ1 = new float[2];
    internal readonly float[] NotchZ2 = new float[2];
    internal readonly PowerMeterState Power = new(5);

    internal void Reset() {
        Array.Clear(NotchZ1);
        Array.Clear(NotchZ2);
        Power.Initialize(5);
    }
}

/// <summary>Working state for a signalling-tone transmitter.</summary>
public sealed class SigToneTxState : IDisposable {
    private readonly int[] _phaseRates = new int[2];
    private readonly uint[] _phaseAccumulators = new uint[2];
    private readonly float[,] _toneScaling = new float[2, 2];
    private SigToneDescriptor _descriptor = SigToneTables.GetDescriptor(SigToneType.Tone2280Hz);
    private SigToneReportHandler? _signalUpdate;
    private object? _userData;
    private bool _disposed;

    public SigToneTxState(
        SigToneType toneType,
        SigToneReportHandler signalUpdate,
        object? userData = null) {
        Initialize(toneType, signalUpdate, userData);
    }

    public int CurrentTransmitTone { get; private set; }

    public int CurrentTransmitTimeout { get; private set; }

    public int HighLowTimer { get; private set; }

    public void Initialize(
        SigToneType toneType,
        SigToneReportHandler signalUpdate,
        object? userData) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(signalUpdate);

        _descriptor = SigToneTables.GetDescriptor(toneType);
        _signalUpdate = signalUpdate;
        _userData = userData;

        Array.Clear(_phaseRates);
        Array.Clear(_phaseAccumulators);
        Array.Clear(_toneScaling);

        for (int i = 0; i < 2; i++) {
            int frequency = _descriptor.ToneFrequencies[i];
            _phaseRates[i] = frequency == 0
                ? 0
                : SigToneDds.PhaseRate(frequency);

            _toneScaling[i, 0] =
                SigToneDds.ScalingDbm0(_descriptor.ToneAmplitudes[i, 0]);
            _toneScaling[i, 1] =
                SigToneDds.ScalingDbm0(_descriptor.ToneAmplitudes[i, 1]);
        }

        CurrentTransmitTone = 0;
        CurrentTransmitTimeout = 0;
        HighLowTimer = 0;
    }

    public void SetMode(int mode, int duration) {
        ThrowIfDisposed();

        if (duration < 0) {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        int oldTones = CurrentTransmitTone & SigTone.PresentMask;
        int newTones = mode & SigTone.PresentMask;

        if (newTones != 0 && oldTones != newTones) {
            HighLowTimer = _descriptor.HighLowTimeout;
        }

        if ((mode & SigTone.SIG_TONE_1_PRESENT) != 0 &&
            (CurrentTransmitTone & SigTone.SIG_TONE_1_PRESENT) == 0) {
            _phaseAccumulators[0] = 0;
        }

        if ((mode & SigTone.SIG_TONE_2_PRESENT) != 0 &&
            (CurrentTransmitTone & SigTone.SIG_TONE_2_PRESENT) == 0) {
            _phaseAccumulators[1] = 0;
        }

        CurrentTransmitTone = mode;
        CurrentTransmitTimeout = duration;
    }

    public int Generate(Span<short> amplitudes) {
        ThrowIfDisposed();

        int offset = 0;
        while (offset < amplitudes.Length) {
            int count = amplitudes.Length - offset;

            if (CurrentTransmitTimeout > 0) {
                count = Math.Min(count, CurrentTransmitTimeout);
            }

            if ((CurrentTransmitTone & SigTone.PresentMask) != 0 &&
                HighLowTimer > 0) {
                count = Math.Min(count, HighLowTimer);
            }

            if (count <= 0) {
                break;
            }

            bool passthrough =
                (CurrentTransmitTone & SigTone.SIG_TONE_TX_PASSTHROUGH) != 0;

            Span<short> block = amplitudes.Slice(offset, count);
            if (!passthrough) {
                block.Clear();
            }

            int highLow = HighLowTimer > 0 ? 0 : 1;

            if ((CurrentTransmitTone & SigTone.PresentMask) != 0) {
                for (int sample = 0; sample < block.Length; sample++) {
                    float value = block[sample];

                    for (int tone = 0; tone < _descriptor.Tones; tone++) {
                        int presentBit = SigToneTables.TonePresentBits[tone];
                        if ((CurrentTransmitTone & presentBit) != 0 &&
                            _phaseRates[tone] != 0) {
                            value += SigToneDds.Mod(
                                ref _phaseAccumulators[tone],
                                _phaseRates[tone],
                                _toneScaling[tone, highLow]);
                        }
                    }

                    block[sample] = SaturateToInt16(value);
                }
            }

            if (HighLowTimer > 0) {
                HighLowTimer -= count;
            }

            bool requestUpdate = false;
            if (CurrentTransmitTimeout > 0) {
                CurrentTransmitTimeout -= count;
                requestUpdate = CurrentTransmitTimeout == 0;
            }

            offset += count;

            if (requestUpdate) {
                _signalUpdate?.Invoke(
                    _userData,
                    SigTone.SIG_TONE_TX_UPDATE_REQUEST,
                    0,
                    0);
            }
        }

        return offset;
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        Array.Clear(_phaseRates);
        Array.Clear(_phaseAccumulators);
        Array.Clear(_toneScaling);
        _signalUpdate = null;
        _userData = null;
        CurrentTransmitTone = 0;
        CurrentTransmitTimeout = 0;
        HighLowTimer = 0;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static short SaturateToInt16(float value) {
        int rounded = (int)MathF.Round(value);
        if (rounded > short.MaxValue) {
            return short.MaxValue;
        }

        if (rounded < short.MinValue) {
            return short.MinValue;
        }

        return (short)rounded;
    }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

/// <summary>Working state for a signalling-tone receiver.</summary>
public sealed class SigToneRxState : IDisposable {
    private readonly SigToneRxFilterState[] _tones =
    {
        new(),
        new(),
        new()
    };

    private readonly float[] _flatZ = new float[2];
    private readonly PowerMeterState _flatPower = new(5);

    private SigToneDescriptor _descriptor = SigToneTables.GetDescriptor(SigToneType.Tone2280Hz);
    private SigToneReportHandler? _signalUpdate;
    private object? _userData;
    private bool _disposed;

    public SigToneRxState(
        SigToneType toneType,
        SigToneReportHandler signalUpdate,
        object? userData = null) {
        Initialize(toneType, signalUpdate, userData);
    }

    public int CurrentReceiveTone { get; private set; }

    public int CurrentNotchFilter { get; private set; }

    public int SignallingState { get; private set; }

    public int SignallingStateDuration { get; private set; }

    public bool FlatMode { get; private set; }

    public int FlatModeTimeout { get; private set; }

    public int NotchInsertionTimeout { get; private set; }

    public void Initialize(
        SigToneType toneType,
        SigToneReportHandler signalUpdate,
        object? userData) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(signalUpdate);

        _descriptor = SigToneTables.GetDescriptor(toneType);
        _signalUpdate = signalUpdate;
        _userData = userData;

        foreach (SigToneRxFilterState tone in _tones) {
            tone.Reset();
        }

        Array.Clear(_flatZ);
        _flatPower.Initialize(5);

        CurrentReceiveTone = 0;
        CurrentNotchFilter = 0;
        SignallingState = 0;
        SignallingStateDuration = 0;
        FlatMode = false;
        FlatModeTimeout = 0;
        NotchInsertionTimeout = 0;
        TonePersistenceTimeout = 0;
        LastSampleTonePresent = -1;

        FlatDetectionThreshold =
            PowerMeter.LevelDbm0(_descriptor.FlatDetectionThresholdDbm0);
        SharpDetectionThreshold =
            PowerMeter.LevelDbm0(_descriptor.SharpDetectionThresholdDbm0);
        DetectionRatio = (int)(
            PowerMeter.DbToPowerRatio(_descriptor.DetectionRatioDb) + 1.0f);
    }

    public int TonePersistenceTimeout { get; private set; }

    public int LastSampleTonePresent { get; private set; }

    public int FlatDetectionThreshold { get; private set; }

    public int SharpDetectionThreshold { get; private set; }

    public int DetectionRatio { get; private set; }

    public void SetMode(int mode, int duration) {
        ThrowIfDisposed();
        _ = duration;
        CurrentReceiveTone = mode;
    }

    public int Process(Span<short> amplitudes) {
        ThrowIfDisposed();

        int filterCount = _descriptor.Tones == 2 ? 3 : _descriptor.Tones;
        Span<float> notchedSignal = stackalloc float[3];
        Span<int> notchPower = stackalloc int[3];

        for (int sampleIndex = 0; sampleIndex < amplitudes.Length; sampleIndex++) {
            if (SignallingStateDuration < int.MaxValue) {
                SignallingStateDuration++;
            }

            notchedSignal.Clear();
            notchPower[0] = 0;
            notchPower[1] = int.MaxValue;
            notchPower[2] = int.MaxValue;

            float signal = amplitudes[sampleIndex];

            for (int filter = 0; filter < filterCount; filter++) {
                int coefficientSet = SigToneTables.CoefficientSets[filter];
                SigToneNotchCoefficients coefficients =
                    _descriptor.Notches[coefficientSet] ??
                    throw new InvalidOperationException(
                        "The selected signalling-tone descriptor has no notch filter.");

                SigToneRxFilterState state = _tones[filter];

                float value =
                    signal * coefficients.A1[0] +
                    state.NotchZ1[0] * coefficients.B1[1] +
                    state.NotchZ1[1] * coefficients.B1[2];

                float x = value;
                value +=
                    state.NotchZ1[0] * coefficients.A1[1] +
                    state.NotchZ1[1] * coefficients.A1[2];

                state.NotchZ1[1] = state.NotchZ1[0];
                state.NotchZ1[0] = x;

                value +=
                    state.NotchZ2[0] * coefficients.B2[1] +
                    state.NotchZ2[1] * coefficients.B2[2];

                x = value;
                value +=
                    state.NotchZ2[0] * coefficients.A2[1] +
                    state.NotchZ2[1] * coefficients.A2[2];

                state.NotchZ2[1] = state.NotchZ2[0];
                state.NotchZ2[0] = x;

                notchedSignal[filter] = value;
                notchPower[filter] =
                    state.Power.Update(SaturateToInt16(value));

                if (filter == 1) {
                    signal = value;
                }
            }

            if ((SignallingState & SigTone.PresentMask) != 0) {
                if (FlatModeTimeout > 0) {
                    FlatModeTimeout--;
                    if (FlatModeTimeout == 0) {
                        FlatMode = true;
                    }
                }
            } else {
                FlatModeTimeout = _descriptor.SharpFlatTimeout;
                FlatMode = false;
            }

            int immediate = -1;

            if (FlatMode) {
                float bandpassSignal = amplitudes[sampleIndex];
                if (_descriptor.Flat is not null) {
                    SigToneFlatCoefficients coefficients = _descriptor.Flat;

                    float value =
                        amplitudes[sampleIndex] * coefficients.A[0] +
                        _flatZ[0] * coefficients.B[1] +
                        _flatZ[1] * coefficients.B[2];

                    float x = value;
                    value +=
                        _flatZ[0] * coefficients.A[1] +
                        _flatZ[1] * coefficients.A[2];

                    _flatZ[1] = _flatZ[0];
                    _flatZ[0] = x;
                    bandpassSignal = value;
                }

                int flatPower =
                    _flatPower.Update(SaturateToInt16(bandpassSignal));

                if ((SignallingState & SigTone.PresentMask) != 0) {
                    if (flatPower < FlatDetectionThreshold) {
                        SignallingState &= ~SigTone.SIG_TONE_1_PRESENT;
                        SignallingState |= SigTone.SIG_TONE_1_CHANGE;
                    }
                } else if (flatPower > FlatDetectionThreshold) {
                    SignallingState |=
                        SigTone.SIG_TONE_1_PRESENT |
                        SigTone.SIG_TONE_1_CHANGE;
                }

                if ((SignallingState & SigTone.PresentMask) != 0) {
                    NotchInsertionTimeout = _descriptor.NotchLagTime;
                } else if (NotchInsertionTimeout > 0) {
                    NotchInsertionTimeout--;
                }
            } else {
                int flatPower = _flatPower.Update(amplitudes[sampleIndex]);

                if (flatPower >= SharpDetectionThreshold) {
                    int bestSingle =
                        notchPower[0] < notchPower[1] ? 0 : 1;

                    if ((long)(notchPower[bestSingle] >> 6) * DetectionRatio <
                        (flatPower >> 6)) {
                        immediate = bestSingle;
                    } else if (
                          (long)(notchPower[2] >> 6) * DetectionRatio <
                          (flatPower >> 7)) {
                        immediate = 2;
                    }
                }

                if ((SignallingState & SigTone.PresentMask) != 0) {
                    if (immediate != CurrentNotchFilter) {
                        if (TonePersistenceTimeout > 0) {
                            TonePersistenceTimeout--;
                        }

                        if (TonePersistenceTimeout == 0) {
                            TonePersistenceTimeout =
                                _descriptor.ToneOnCheckTime;

                            SignallingState |=
                                (SignallingState & SigTone.PresentMask) << 1;
                            SignallingState &= ~SigTone.PresentMask;
                        }
                    } else {
                        TonePersistenceTimeout =
                            _descriptor.ToneOffCheckTime;
                    }
                } else {
                    if (NotchInsertionTimeout > 0) {
                        NotchInsertionTimeout--;
                    }

                    if (immediate >= 0 &&
                        immediate == LastSampleTonePresent) {
                        if (TonePersistenceTimeout > 0) {
                            TonePersistenceTimeout--;
                        }

                        if (TonePersistenceTimeout == 0) {
                            TonePersistenceTimeout =
                                _descriptor.ToneOffCheckTime;
                            NotchInsertionTimeout =
                                _descriptor.NotchLagTime;

                            SignallingState |=
                                SigToneTables.TonePresentBits[immediate] |
                                SigToneTables.ToneChangeBits[immediate];

                            CurrentNotchFilter = immediate;
                        }
                    } else {
                        TonePersistenceTimeout =
                            _descriptor.ToneOnCheckTime;
                    }
                }
            }

            if ((SignallingState & SigTone.ChangeMask) != 0) {
                _signalUpdate?.Invoke(
                    _userData,
                    SignallingState,
                    0,
                    SignallingStateDuration);

                SignallingState &= ~SigTone.ChangeMask;
                SignallingStateDuration = 0;
            }

            if ((CurrentReceiveTone & SigTone.SIG_TONE_RX_PASSTHROUGH) != 0) {
                if ((CurrentReceiveTone & SigTone.SIG_TONE_RX_FILTER_TONE) != 0 ||
                    NotchInsertionTimeout > 0) {
                    int selected = Math.Clamp(CurrentNotchFilter, 0, filterCount - 1);
                    amplitudes[sampleIndex] =
                        SaturateToInt16(notchedSignal[selected]);
                }
            } else {
                amplitudes[sampleIndex] = 0;
            }

            LastSampleTonePresent = immediate;
        }

        return amplitudes.Length;
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        foreach (SigToneRxFilterState tone in _tones) {
            tone.Reset();
        }

        Array.Clear(_flatZ);
        _flatPower.Initialize(5);
        _signalUpdate = null;
        _userData = null;
        CurrentReceiveTone = 0;
        SignallingState = 0;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static short SaturateToInt16(float value) {
        int rounded = (int)MathF.Round(value);
        if (rounded > short.MaxValue) {
            return short.MaxValue;
        }

        if (rounded < short.MinValue) {
            return short.MinValue;
        }

        return (short)rounded;
    }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

/// <summary>Native-name-compatible entry points for signalling-tone processing.</summary>
public static class SigToneApi {
    public static int sig_tone_rx(
        SigToneRxState state,
        short[] amplitudes,
        int length) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(amplitudes);

        if (length < 0 || length > amplitudes.Length) {
            return -1;
        }

        return state.Process(amplitudes.AsSpan(0, length));
    }

    public static void sig_tone_rx_set_mode(
        SigToneRxState state,
        int mode,
        int duration) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetMode(mode, duration);
    }

    public static SigToneRxState? sig_tone_rx_init(
        SigToneRxState? state,
        int toneType,
        SigToneReportHandler? signalUpdate,
        object? userData) {
        if (signalUpdate is null ||
            toneType < SigTone.SIG_TONE_2280HZ ||
            toneType > SigTone.SIG_TONE_2400HZ_2600HZ) {
            return null;
        }

        try {
            SigToneType type = (SigToneType)toneType;
            if (state is null) {
                return new SigToneRxState(type, signalUpdate, userData);
            }

            state.Initialize(type, signalUpdate, userData);
            return state;
        } catch (ArgumentException) {
            return null;
        }
    }

    public static int sig_tone_rx_release(SigToneRxState state) {
        ArgumentNullException.ThrowIfNull(state);
        return 0;
    }

    public static int sig_tone_rx_free(SigToneRxState? state) {
        state?.Dispose();
        return 0;
    }

    public static int sig_tone_tx(
        SigToneTxState state,
        short[] amplitudes,
        int length) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(amplitudes);

        if (length < 0 || length > amplitudes.Length) {
            return -1;
        }

        return state.Generate(amplitudes.AsSpan(0, length));
    }

    public static void sig_tone_tx_set_mode(
        SigToneTxState state,
        int mode,
        int duration) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetMode(mode, duration);
    }

    public static SigToneTxState? sig_tone_tx_init(
        SigToneTxState? state,
        int toneType,
        SigToneReportHandler? signalUpdate,
        object? userData) {
        if (signalUpdate is null ||
            toneType < SigTone.SIG_TONE_2280HZ ||
            toneType > SigTone.SIG_TONE_2400HZ_2600HZ) {
            return null;
        }

        try {
            SigToneType type = (SigToneType)toneType;
            if (state is null) {
                return new SigToneTxState(type, signalUpdate, userData);
            }

            state.Initialize(type, signalUpdate, userData);
            return state;
        } catch (ArgumentException) {
            return null;
        }
    }

    public static int sig_tone_tx_release(SigToneTxState state) {
        ArgumentNullException.ThrowIfNull(state);
        return 0;
    }

    public static int sig_tone_tx_free(SigToneTxState? state) {
        state?.Dispose();
        return 0;
    }
}

internal static class SigToneTables {
    internal static readonly int[] TonePresentBits =
    {
        SigTone.SIG_TONE_1_PRESENT,
        SigTone.SIG_TONE_2_PRESENT,
        SigTone.SIG_TONE_1_PRESENT | SigTone.SIG_TONE_2_PRESENT
    };

    internal static readonly int[] ToneChangeBits =
    {
        SigTone.SIG_TONE_1_CHANGE,
        SigTone.SIG_TONE_2_CHANGE,
        SigTone.SIG_TONE_1_CHANGE | SigTone.SIG_TONE_2_CHANGE
    };

    internal static readonly int[] CoefficientSets = { 0, 1, 0 };

    private static readonly SigToneNotchCoefficients Notch2280 = new(
        new[] { 0.878906f, 0.439362f, 1.0f },
        new[] { 0.0f, -0.287627f, -0.883605f },
        new[] { 0.0f, 0.433228f, 1.0f },
        new[] { 0.0f, -0.530792f, -0.883605f });

    private static readonly SigToneNotchCoefficients Notch2400 = new(
        new[] { 0.862000f, 0.612055f, 1.0f },
        new[] { 0.0f, -0.456264f, -0.864899f },
        new[] { 0.0f, 0.621021f, 1.0f },
        new[] { 0.0f, -0.690738f, -0.864899f });

    private static readonly SigToneNotchCoefficients Notch2600 = new(
        new[] { 0.862000f, 0.902374f, 1.0f },
        new[] { 0.0f, -0.732727f, -0.864899f },
        new[] { 0.0f, 0.910766f, 1.0f },
        new[] { 0.0f, -0.952393f, -0.864899f });

    private static readonly SigToneFlatCoefficients Flat2280 = new(
        new[] { 0.393676f, -0.5f, -0.5f },
        new[] { 0.0f, -0.261778f, -0.359985f });

    private static readonly SigToneDescriptor[] Descriptors =
    {
        new()
        {
            ToneFrequencies = new[] { 2280, 0 },
            ToneAmplitudes = new[,] { { -10, -20 }, { 0, 0 } },
            HighLowTimeout = SigTone.MillisecondsToSamples(400),
            SharpFlatTimeout = SigTone.MillisecondsToSamples(225),
            NotchLagTime = SigTone.MillisecondsToSamples(225),
            ToneOnCheckTime = SigTone.MillisecondsToSamples(3),
            ToneOffCheckTime = SigTone.MillisecondsToSamples(8),
            Tones = 1,
            Notches = new SigToneNotchCoefficients?[] { Notch2280, null },
            Flat = Flat2280,
            DetectionRatioDb = 13.0f,
            SharpDetectionThresholdDbm0 = -30.0f,
            FlatDetectionThresholdDbm0 = -30.0f
        },
        new()
        {
            ToneFrequencies = new[] { 2600, 0 },
            ToneAmplitudes = new[,] { { -8, -8 }, { 0, 0 } },
            HighLowTimeout = 0,
            SharpFlatTimeout = 0,
            NotchLagTime = SigTone.MillisecondsToSamples(225),
            ToneOnCheckTime = SigTone.MillisecondsToSamples(3),
            ToneOffCheckTime = SigTone.MillisecondsToSamples(8),
            Tones = 1,
            Notches = new SigToneNotchCoefficients?[] { Notch2600, null },
            Flat = null,
            DetectionRatioDb = 15.6f,
            SharpDetectionThresholdDbm0 = -30.0f,
            FlatDetectionThresholdDbm0 = -30.0f
        },
        new()
        {
            ToneFrequencies = new[] { 2400, 2600 },
            ToneAmplitudes = new[,] { { -8, -8 }, { -8, -8 } },
            HighLowTimeout = 0,
            SharpFlatTimeout = 0,
            NotchLagTime = SigTone.MillisecondsToSamples(225),
            ToneOnCheckTime = SigTone.MillisecondsToSamples(3),
            ToneOffCheckTime = SigTone.MillisecondsToSamples(8),
            Tones = 2,
            Notches = new SigToneNotchCoefficients?[] { Notch2400, Notch2600 },
            Flat = null,
            DetectionRatioDb = 15.6f,
            SharpDetectionThresholdDbm0 = -30.0f,
            FlatDetectionThresholdDbm0 = -30.0f
        }
    };

    internal static SigToneDescriptor GetDescriptor(SigToneType type) {
        int index = (int)type - 1;
        if ((uint)index >= (uint)Descriptors.Length) {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        return Descriptors[index];
    }
}

internal static class SigToneDds {
    private const float TwoPi = 2.0f * MathF.PI;
    private const float Dbm0MaximumSinePower = 3.14f;

    internal static int PhaseRate(float frequency) {
        return unchecked(
            (int)(frequency * 65536.0f * 65536.0f / SigTone.SampleRate));
    }

    internal static float ScalingDbm0(float level) {
        return MathF.Pow(
            10.0f,
            (level - Dbm0MaximumSinePower) / 20.0f) * 32767.0f;
    }

    internal static float Mod(
        ref uint phaseAccumulator,
        int phaseRate,
        float scale) {
        float phase = phaseAccumulator * (TwoPi / 4294967296.0f);
        float amplitude = MathF.Sin(phase) * scale;
        phaseAccumulator = unchecked(phaseAccumulator + (uint)phaseRate);
        return amplitude;
    }
}
