/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * Plc.cs - Managed C# port of plc.c and plc.h
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2004 Steve Underwood
 *
 * This file is distributed under the terms of the GNU Lesser General Public
 * License version 2.1, matching the original source files.
 */

#nullable enable

namespace TKFaxEngine.Audio;

/// <summary>Constants used by the generic packet-loss concealment algorithm.</summary>
public static class Plc {
    /// <summary>Longest supported pitch period, corresponding to about 66 Hz at 8 kHz.</summary>
    public const int PitchMinimum = 120;

    /// <summary>Shortest supported pitch period, corresponding to 200 Hz at 8 kHz.</summary>
    public const int PitchMaximum = 40;

    public const int PitchOverlapMaximum = PitchMinimum >> 2;

    public const int CorrelationSpan = 160;

    public const int HistoryLength = CorrelationSpan + PitchMinimum;

    internal const float AttenuationIncrement = 0.0025f;
}

/// <summary>
/// Generic speech packet-loss concealment state. The implementation follows
/// the pitch repetition and overlap-add procedure from the native module.
/// </summary>
public sealed class PlcState : IDisposable {
    private readonly float[] _pitchBuffer = new float[Plc.PitchMinimum];
    private readonly short[] _history = new short[Plc.HistoryLength];
    private bool _disposed;
    private int _bufferPointer;

    public PlcState() {
        Initialize();
    }

    public int MissingSamples { get; private set; }

    public int PitchOffset { get; private set; }

    public int Pitch { get; private set; }

    public ReadOnlySpan<float> PitchBuffer => _pitchBuffer;

    public ReadOnlySpan<short> History => _history;

    public void Initialize() {
        ThrowIfDisposed();

        Array.Clear(_pitchBuffer);
        Array.Clear(_history);
        MissingSamples = 0;
        PitchOffset = 0;
        Pitch = 0;
        _bufferPointer = 0;
    }

    /// <summary>
    /// Processes received audio. After a loss period, the beginning of the
    /// real packet is cross-faded with the synthetic pitch cycle.
    /// </summary>
    public int Receive(Span<short> amplitudes) {
        ThrowIfDisposed();

        if (MissingSamples > 0 && !amplitudes.IsEmpty) {
            int pitchOverlap = Pitch >> 2;
            if (pitchOverlap > amplitudes.Length) {
                pitchOverlap = amplitudes.Length;
            }

            if (pitchOverlap > 0 && Pitch > 0) {
                float gain = 1.0f - MissingSamples * Plc.AttenuationIncrement;
                if (gain < 0.0f) {
                    gain = 0.0f;
                }

                float newStep = 1.0f / pitchOverlap;
                float oldStep = newStep * gain;
                float newWeight = newStep;
                float oldWeight = (1.0f - newStep) * gain;

                for (int i = 0; i < pitchOverlap; i++) {
                    amplitudes[i] = SaturateToInt16(
                        oldWeight * _pitchBuffer[PitchOffset] +
                        newWeight * amplitudes[i]);

                    PitchOffset++;
                    if (PitchOffset >= Pitch) {
                        PitchOffset = 0;
                    }

                    newWeight += newStep;
                    oldWeight -= oldStep;
                    if (oldWeight < 0.0f) {
                        oldWeight = 0.0f;
                    }
                }
            }

            MissingSamples = 0;
        }

        SaveHistory(amplitudes);
        return amplitudes.Length;
    }

    /// <summary>Creates synthetic replacement audio for a missing packet.</summary>
    public int FillIn(Span<short> amplitudes) {
        ThrowIfDisposed();

        int originalLength = amplitudes.Length;
        if (originalLength == 0) {
            return 0;
        }

        int index;
        float gain;

        if (MissingSamples == 0) {
            NormalizeHistory();

            Pitch = AmdfPitch(
                Plc.PitchMinimum,
                Plc.PitchMaximum,
                _history.AsSpan(
                    Plc.HistoryLength - Plc.CorrelationSpan - Plc.PitchMinimum),
                Plc.CorrelationSpan);

            int pitchOverlap = Pitch >> 2;

            for (index = 0; index < Pitch - pitchOverlap; index++) {
                _pitchBuffer[index] =
                    _history[Plc.HistoryLength - Pitch + index];
            }

            float newStep = 1.0f / pitchOverlap;
            float newWeight = newStep;

            for (; index < Pitch; index++) {
                _pitchBuffer[index] =
                    _history[Plc.HistoryLength - Pitch + index] * (1.0f - newWeight) +
                    _history[Plc.HistoryLength - 2 * Pitch + index] * newWeight;
                newWeight += newStep;
            }

            gain = 1.0f;
            newStep = 1.0f / pitchOverlap;
            float oldStep = newStep;
            newWeight = newStep;
            float oldWeight = 1.0f - newStep;

            if (pitchOverlap > originalLength) {
                pitchOverlap = originalLength;
            }

            for (index = 0; index < pitchOverlap; index++) {
                amplitudes[index] = SaturateToInt16(
                    oldWeight * _history[Plc.HistoryLength - 1 - index] +
                    newWeight * _pitchBuffer[index]);

                newWeight += newStep;
                oldWeight -= oldStep;
                if (oldWeight < 0.0f) {
                    oldWeight = 0.0f;
                }
            }

            PitchOffset = index;
            if (PitchOffset >= Pitch) {
                PitchOffset %= Pitch;
            }
        } else {
            gain = 1.0f - MissingSamples * Plc.AttenuationIncrement;
            index = 0;
        }

        for (; gain > 0.0f && index < originalLength; index++) {
            amplitudes[index] = SaturateToInt16(_pitchBuffer[PitchOffset] * gain);
            gain -= Plc.AttenuationIncrement;

            PitchOffset++;
            if (PitchOffset >= Pitch) {
                PitchOffset = 0;
            }
        }

        amplitudes[index..].Clear();

        MissingSamples = SaturatingAdd(MissingSamples, originalLength);
        SaveHistory(amplitudes);
        return originalLength;
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        Array.Clear(_pitchBuffer);
        Array.Clear(_history);
        MissingSamples = 0;
        PitchOffset = 0;
        Pitch = 0;
        _bufferPointer = 0;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void SaveHistory(ReadOnlySpan<short> source) {
        int length = source.Length;

        if (length >= Plc.HistoryLength) {
            source[^Plc.HistoryLength..].CopyTo(_history);
            _bufferPointer = 0;
            return;
        }

        if (_bufferPointer + length > Plc.HistoryLength) {
            int firstLength = Plc.HistoryLength - _bufferPointer;
            source[..firstLength].CopyTo(_history.AsSpan(_bufferPointer));

            int remaining = length - firstLength;
            source.Slice(firstLength, remaining).CopyTo(_history);
            _bufferPointer = remaining;
            return;
        }

        source.CopyTo(_history.AsSpan(_bufferPointer));
        _bufferPointer += length;
    }

    private void NormalizeHistory() {
        if (_bufferPointer == 0) {
            return;
        }

        if (_bufferPointer == Plc.HistoryLength) {
            _bufferPointer = 0;
            return;
        }

        short[] temporary = new short[_bufferPointer];
        _history.AsSpan(0, _bufferPointer).CopyTo(temporary);
        _history.AsSpan(_bufferPointer).CopyTo(_history);
        temporary.AsSpan().CopyTo(_history.AsSpan(Plc.HistoryLength - _bufferPointer));
        _bufferPointer = 0;
    }

    private static int AmdfPitch(
        int minimumPitch,
        int maximumPitch,
        ReadOnlySpan<short> amplitudes,
        int length) {
        int pitch = minimumPitch;
        long minimumAccumulator = long.MaxValue;

        for (int candidate = maximumPitch; candidate <= minimumPitch; candidate++) {
            long accumulator = 0;
            for (int j = 0; j < length; j++) {
                accumulator += Math.Abs(
                    (int)amplitudes[candidate + j] - amplitudes[j]);
            }

            if (accumulator < minimumAccumulator) {
                minimumAccumulator = accumulator;
                pitch = candidate;
            }
        }

        return pitch;
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

    private static int SaturatingAdd(int left, int right) {
        long result = (long)left + right;
        return result >= int.MaxValue ? int.MaxValue : (int)result;
    }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

/// <summary>Native-name-compatible entry points for packet-loss concealment.</summary>
public static class PlcApi {
    public static int plc_rx(PlcState state, short[] amplitudes, int length) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(amplitudes);

        if (length < 0 || length > amplitudes.Length) {
            return -1;
        }

        return state.Receive(amplitudes.AsSpan(0, length));
    }

    public static int plc_fillin(PlcState state, short[] amplitudes, int length) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(amplitudes);

        if (length < 0 || length > amplitudes.Length) {
            return -1;
        }

        return state.FillIn(amplitudes.AsSpan(0, length));
    }

    public static PlcState? plc_init(PlcState? state) {
        try {
            state ??= new PlcState();
            state.Initialize();
            return state;
        } catch (ObjectDisposedException) {
            return null;
        }
    }

    public static int plc_release(PlcState state) {
        ArgumentNullException.ThrowIfNull(state);
        return 0;
    }

    public static int plc_free(PlcState? state) {
        state?.Dispose();
        return 0;
    }
}
