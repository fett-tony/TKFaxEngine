/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * TimeScale.cs - Managed C# port of time_scale.c and time_scale.h
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2004 Steve Underwood
 *
 * This file is distributed under the terms of the GNU Lesser General Public
 * License version 2.1, matching the original source files.
 */

#nullable enable

namespace TKFaxEngine.Audio;

/// <summary>
/// Incremental pitch-preserving time scaling for signed linear PCM speech.
/// The implementation follows the PICOLA algorithm used by the native source.
/// </summary>
public sealed class TimeScaleState : IDisposable {
    public const int MaximumSampleRate = 48_000;
    public const int MinimumPitchHz = 60;
    public const int MaximumPitchHz = 250;
    public const int MaximumBufferLength = 2 * MaximumSampleRate / MinimumPitchHz;

    private short[] _buffer = Array.Empty<short>();
    private bool _disposed;

    public TimeScaleState(int sampleRate, float playoutRate) {
        Initialize(sampleRate, playoutRate);
    }

    public int SampleRate { get; private set; }

    /// <summary>Largest pitch period searched, in samples.</summary>
    public int MinimumPitch { get; private set; }

    /// <summary>Smallest pitch period searched, in samples.</summary>
    public int MaximumPitch { get; private set; }

    /// <summary>
    /// Output-time/input-time ratio. Values above one slow playback down;
    /// values below one speed playback up.
    /// </summary>
    public float PlayoutRate { get; private set; }

    public int BufferedSamples => _fill;

    public int WorkingBufferLength => _bufferLength;

    private double _rateCompensation;
    private double _rateNudge;
    private int _lcp;
    private int _bufferLength;
    private int _fill;

    public void Initialize(int sampleRate, float playoutRate) {
        ThrowIfDisposed();

        if (sampleRate <= 0 || sampleRate > MaximumSampleRate) {
            throw new ArgumentOutOfRangeException(nameof(sampleRate),
                $"Sample rate must be between 1 and {MaximumSampleRate} Hz.");
        }

        SampleRate = sampleRate;
        MinimumPitch = Math.Max(1, sampleRate / MinimumPitchHz);
        MaximumPitch = Math.Max(1, sampleRate / MaximumPitchHz);
        _bufferLength = checked(2 * sampleRate / MinimumPitchHz);
        _buffer = new short[_bufferLength];

        _rateNudge = 0.0;
        _fill = 0;
        _lcp = 0;
        SetRate(playoutRate);
    }

    /// <summary>Changes the time-scale ratio without discarding buffered audio.</summary>
    public int SetRate(float playoutRate) {
        ThrowIfDisposed();

        if (!float.IsFinite(playoutRate) || playoutRate <= 0.0f) {
            return -1;
        }

        if (playoutRate >= 0.99f && playoutRate <= 1.01f) {
            playoutRate = 1.0f;
            _rateCompensation = 0.0;
        } else if (playoutRate < 1.0f) {
            _rateCompensation = playoutRate / (1.0f - playoutRate);
        } else {
            _rateCompensation = 1.0f / (playoutRate - 1.0f);
        }

        PlayoutRate = playoutRate;
        return 0;
    }

    /// <summary>
    /// Returns a conservative output capacity for one processing call, including
    /// samples retained from earlier calls.
    /// </summary>
    public int GetMaximumOutputLength(int inputLength) {
        ThrowIfDisposed();

        if (inputLength < 0) {
            return -1;
        }

        if (PlayoutRate == 1.0f) {
            return inputLength;
        }

        double rate = PlayoutRate > 1.0f ? PlayoutRate : 1.0;
        double maximum = ((double)inputLength + _fill) * rate
            + 2.0 * _bufferLength + MaximumPitch + 1.0;

        return maximum >= int.MaxValue ? int.MaxValue : (int)Math.Ceiling(maximum);
    }

    /// <summary>Processes one chunk of signed 16-bit PCM.</summary>
    public int Process(ReadOnlySpan<short> input, Span<short> output) {
        ThrowIfDisposed();

        int required = GetMaximumOutputLength(input.Length);
        if (required < 0 || output.Length < required) {
            throw new ArgumentException(
                $"The output buffer must contain at least {required} samples.", nameof(output));
        }

        if (PlayoutRate == 1.0f) {
            input.CopyTo(output);
            return input.Length;
        }

        int outputLength = 0;
        int inputPosition = 0;

        if (_fill + input.Length < _bufferLength) {
            input.CopyTo(_buffer.AsSpan(_fill));
            _fill += input.Length;
            return 0;
        }

        int topUp = _bufferLength - _fill;
        input[..topUp].CopyTo(_buffer.AsSpan(_fill));
        inputPosition += topUp;
        _fill = _bufferLength;

        while (_fill == _bufferLength) {
            while (_lcp >= _bufferLength) {
                CopyToOutput(_buffer.AsSpan(0, _bufferLength), output, ref outputLength);

                if (input.Length - inputPosition < _bufferLength) {
                    int remaining = input.Length - inputPosition;
                    input.Slice(inputPosition, remaining).CopyTo(_buffer);
                    _fill = remaining;
                    _lcp -= _bufferLength;
                    return outputLength;
                }

                input.Slice(inputPosition, _bufferLength).CopyTo(_buffer);
                inputPosition += _bufferLength;
                _lcp -= _bufferLength;
            }

            if (_lcp > 0) {
                CopyToOutput(_buffer.AsSpan(0, _lcp), output, ref outputLength);
                Array.Copy(_buffer, _lcp, _buffer, 0, _bufferLength - _lcp);

                if (input.Length - inputPosition < _lcp) {
                    int remaining = input.Length - inputPosition;
                    input.Slice(inputPosition, remaining)
                        .CopyTo(_buffer.AsSpan(_bufferLength - _lcp));
                    _fill = _bufferLength - _lcp + remaining;
                    _lcp = 0;
                    return outputLength;
                }

                input.Slice(inputPosition, _lcp)
                    .CopyTo(_buffer.AsSpan(_bufferLength - _lcp));
                inputPosition += _lcp;
                _lcp = 0;
            }

            int pitch = FindPitch(MaximumPitch, MinimumPitch, _buffer, MinimumPitch);
            double lcpFloating = pitch * _rateCompensation;
            _lcp = (int)lcpFloating;

            _rateNudge += _lcp - lcpFloating;
            if (_rateNudge >= 0.5) {
                _lcp--;
                _rateNudge -= 1.0;
            } else if (_rateNudge <= -0.5) {
                _lcp++;
                _rateNudge += 1.0;
            }

            if (PlayoutRate < 1.0f) {
                OverlapAdd(_buffer.AsSpan(pitch, pitch), _buffer.AsSpan(0, pitch));
                Array.Copy(_buffer, 2 * pitch, _buffer, pitch, _bufferLength - 2 * pitch);

                int remaining = input.Length - inputPosition;
                if (remaining < pitch) {
                    input.Slice(inputPosition, remaining)
                        .CopyTo(_buffer.AsSpan(_bufferLength - pitch));
                    _fill += remaining - pitch;
                    return outputLength;
                }

                input.Slice(inputPosition, pitch)
                    .CopyTo(_buffer.AsSpan(_bufferLength - pitch));
                inputPosition += pitch;
            } else {
                CopyToOutput(_buffer.AsSpan(0, pitch), output, ref outputLength);
                OverlapAdd(_buffer.AsSpan(0, pitch), _buffer.AsSpan(pitch, pitch));
            }
        }

        return outputLength;
    }

    /// <summary>Emits retained samples at the end of a stream.</summary>
    public int Flush(Span<short> output) {
        ThrowIfDisposed();

        if (PlayoutRate < 1.0f) {
            return 0;
        }

        int padding = PlayoutRate > 1.0f
            ? (int)(_fill * (PlayoutRate - 1.0f))
            : 0;
        int length = checked(_fill + padding);

        if (output.Length < length) {
            throw new ArgumentException(
                $"The output buffer must contain at least {length} samples.", nameof(output));
        }

        _buffer.AsSpan(0, _fill).CopyTo(output);
        output.Slice(_fill, padding).Clear();
        _fill = 0;
        return length;
    }

    public void Reset() {
        ThrowIfDisposed();
        Array.Clear(_buffer);
        _rateNudge = 0.0;
        _lcp = 0;
        _fill = 0;
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        Array.Clear(_buffer);
        _buffer = Array.Empty<short>();
        _fill = 0;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static int FindPitch(int firstPitch, int lastPitch, short[] samples, int compareLength) {
        int bestPitch = firstPitch;
        long minimumDifference = long.MaxValue;

        for (int pitch = firstPitch; pitch <= lastPitch; pitch++) {
            long difference = 0;
            for (int i = 0; i < compareLength; i++) {
                difference += Math.Abs((int)samples[pitch + i] - samples[i]);
            }

            if (difference < minimumDifference) {
                minimumDifference = difference;
                bestPitch = pitch;
            }
        }

        return bestPitch;
    }

    private static void OverlapAdd(Span<short> destination, ReadOnlySpan<short> source) {
        int length = destination.Length;
        if (source.Length < length || length == 0) {
            return;
        }

        float step = 1.0f / length;
        float weight = 0.0f;
        for (int i = 0; i < length; i++) {
            float value = source[i] * (1.0f - weight) + destination[i] * weight;
            destination[i] = SaturateToInt16(value);
            weight += step;
        }
    }

    private static void CopyToOutput(
        ReadOnlySpan<short> source,
        Span<short> output,
        ref int outputPosition) {
        source.CopyTo(output[outputPosition..]);
        outputPosition += source.Length;
    }

    private static short SaturateToInt16(float value) {
        if (value > short.MaxValue) {
            return short.MaxValue;
        }

        if (value < short.MinValue) {
            return short.MinValue;
        }

        return (short)value;
    }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

/// <summary>Native-name-compatible entry points for the time-scale module.</summary>
public static class TimeScaleApi {
    public static int time_scale_rate(TimeScaleState state, float playoutRate) {
        ArgumentNullException.ThrowIfNull(state);
        return state.SetRate(playoutRate);
    }

    public static int time_scale_max_output_len(TimeScaleState state, int inputLength) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetMaximumOutputLength(inputLength);
    }

    public static int time_scale(TimeScaleState state, short[] output, short[] input, int length) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(input);
        if ((uint)length > (uint)input.Length) {
            return -1;
        }

        try {
            return state.Process(input.AsSpan(0, length), output);
        } catch (ArgumentException) {
            return -1;
        }
    }

    public static int time_scale_flush(TimeScaleState state, short[] output) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(output);
        try {
            return state.Flush(output);
        } catch (ArgumentException) {
            return -1;
        }
    }

    public static TimeScaleState? time_scale_init(
        TimeScaleState? state,
        int sampleRate,
        float playoutRate) {
        try {
            if (state is null) {
                return new TimeScaleState(sampleRate, playoutRate);
            }

            state.Initialize(sampleRate, playoutRate);
            return state;
        } catch (ArgumentException) {
            return null;
        }
    }

    public static int time_scale_release(TimeScaleState state) {
        ArgumentNullException.ThrowIfNull(state);
        return 0;
    }

    public static int time_scale_free(TimeScaleState? state) {
        state?.Dispose();
        return 0;
    }
}
