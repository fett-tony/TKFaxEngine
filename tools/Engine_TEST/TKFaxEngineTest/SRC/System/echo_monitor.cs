/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * echo_monitor.cs - managed port of echo_monitor.cpp.
 *
 * The original FLTK plots are represented by a thread-safe managed monitor
 * model. It performs the same coefficient scaling, rolling input collection,
 * 1024-point FFT and spectrum conversion. A host UI can bind to SnapshotUpdated.
 */

#nullable enable

using System.Numerics;

namespace TKFaxEngineTest.System;

public readonly record struct EchoMonitorSnapshot(
    int TapCount,
    double AudioPeak,
    double[] SpectrumFrequency,
    double[] SpectrumLevel,
    double[] CancellerCoefficients,
    double[] LineModelCoefficients,
    double CancellerMinimum,
    double CancellerMaximum,
    double LineModelMinimum,
    double LineModelMaximum);

public interface IEchoCanMonitor {
    int Start(int length);
    int UpdateCanceller(ReadOnlySpan<short> coefficients);
    int UpdateLineModel(ReadOnlySpan<int> coefficients);
    int UpdateLineSpectrum(ReadOnlySpan<short> amplitudes);
    void WaitToEnd();
    void UpdateDisplay();
    EchoMonitorSnapshot Snapshot { get; }
}

public sealed class EchoCanMonitor : IEchoCanMonitor {
    private const int SpectrumBins = 512;
    private const int FftLength = 1024;
    private readonly object _sync = new();
    private readonly double[] _spectrumFrequency = new double[SpectrumBins];
    private readonly double[] _spectrumLevel = new double[SpectrumBins];
    private readonly double[] _fftInput = new double[FftLength];
    private double[] _canceller = Array.Empty<double>();
    private double[] _lineModel = Array.Empty<double>();
    private int _inputPointer;
    private int _tapCount;
    private int _skip;
    private double _audioPeak;
    private double _cancellerMinimum;
    private double _cancellerMaximum;
    private double _lineModelMinimum;
    private double _lineModelMaximum;

    public event EventHandler<EchoMonitorSnapshot>? SnapshotUpdated;

    public EchoMonitorSnapshot Snapshot {
        get {
            lock (_sync) {
                return new EchoMonitorSnapshot(
                    _tapCount,
                    _audioPeak,
                    (double[])_spectrumFrequency.Clone(),
                    (double[])_spectrumLevel.Clone(),
                    (double[])_canceller.Clone(),
                    (double[])_lineModel.Clone(),
                    _cancellerMinimum,
                    _cancellerMaximum,
                    _lineModelMinimum,
                    _lineModelMaximum);
            }
        }
    }

    public int Start(int length) {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        lock (_sync) {
            _tapCount = length;
            _canceller = new double[length];
            _lineModel = new double[length];
            Array.Clear(_spectrumFrequency);
            Array.Clear(_spectrumLevel);
            Array.Clear(_fftInput);
            _inputPointer = 0;
            _skip = 0;
            _audioPeak = 0.0;
            _cancellerMinimum = -0.1;
            _cancellerMaximum = 0.1;
            _lineModelMinimum = -0.1;
            _lineModelMaximum = 0.1;
        }

        Publish();
        return 0;
    }

    public int UpdateCanceller(ReadOnlySpan<short> coefficients) {
        lock (_sync) {
            EnsureStarted(coefficients.Length);
            int length = Math.Min(coefficients.Length, _tapCount);
            if (_canceller.Length != length)
                _canceller = new double[length];

            if (length > 0) {
                double minimum = coefficients[0];
                double maximum = coefficients[0];
                for (int i = 0; i < length; i++) {
                    double value = coefficients[i];
                    _canceller[i] = value;
                    minimum = Math.Min(minimum, value);
                    maximum = Math.Max(maximum, value);
                }
                _cancellerMinimum = minimum;
                _cancellerMaximum = maximum == minimum ? maximum + 0.2 : maximum;
            }

            if (++_skip < 100)
                return 0;
            _skip = 0;
        }

        Publish();
        return 0;
    }

    public int UpdateLineModel(ReadOnlySpan<int> coefficients) {
        lock (_sync) {
            EnsureStarted(coefficients.Length);
            int length = Math.Min(coefficients.Length, _tapCount);
            if (_lineModel.Length != length)
                _lineModel = new double[length];

            if (length > 0) {
                double minimum = coefficients[0];
                double maximum = coefficients[0];
                for (int i = 0; i < length; i++) {
                    double value = coefficients[i];
                    _lineModel[i] = value;
                    minimum = Math.Min(minimum, value);
                    maximum = Math.Max(maximum, value);
                }
                _lineModelMinimum = minimum;
                _lineModelMaximum = maximum == minimum ? maximum + 0.2 : maximum;
            }
        }

        Publish();
        return 0;
    }

    public int UpdateLineSpectrum(ReadOnlySpan<short> amplitudes) {
        lock (_sync) {
            foreach (short amplitude in amplitudes)
                _audioPeak = Math.Max(_audioPeak * 0.995, Math.Abs(amplitude / 32768.0));

            if (_inputPointer + amplitudes.Length < SpectrumBins) {
                for (int i = 0; i < amplitudes.Length; i++)
                    _fftInput[_inputPointer + i] = amplitudes[i];
                _inputPointer += amplitudes.Length;
                return 0;
            }

            if (amplitudes.Length >= SpectrumBins) {
                int source = amplitudes.Length - SpectrumBins;
                for (int i = 0; i < SpectrumBins; i++)
                    _fftInput[i] = amplitudes[source + i];
            } else {
                int retained = SpectrumBins - amplitudes.Length;
                int oldStart = Math.Max(0, _inputPointer - retained);
                for (int i = 0; i < retained; i++)
                    _fftInput[i] = _fftInput[oldStart + i];
                for (int i = retained; i < SpectrumBins; i++)
                    _fftInput[i] = amplitudes[i - retained];
            }

            Array.Clear(_fftInput, SpectrumBins, FftLength - SpectrumBins);
            _inputPointer = 0;
            Complex[] values = new Complex[FftLength];
            for (int i = 0; i < FftLength; i++)
                values[i] = new Complex(_fftInput[i], 0.0);
            Fft(values);

            double divisor = 256.0 * 32768.0;
            divisor *= divisor;
            for (int i = 0; i < SpectrumBins; i++) {
                _spectrumFrequency[i] = i * 4000.0 / SpectrumBins;
                double power = values[i].Real * values[i].Real + values[i].Imaginary * values[i].Imaginary;
                _spectrumLevel[i] = 10.0 * Math.Log10(power / divisor + 1.0e-10) + 3.14;
            }
        }

        Publish();
        return 0;
    }

    public void WaitToEnd() {
        Console.Error.WriteLine("Processing complete. Press the <enter> key to end");
        Console.ReadLine();
    }

    public void UpdateDisplay() => Publish();

    private void EnsureStarted(int requestedLength) {
        if (_tapCount == 0)
            Start(Math.Max(1, requestedLength));
    }

    private void Publish() {
        EventHandler<EchoMonitorSnapshot>? handler = SnapshotUpdated;
        if (handler is not null)
            handler(this, Snapshot);
        Thread.Yield();
    }

    private static void Fft(Complex[] values) {
        int n = values.Length;
        for (int j = 1, i = 0; j < n; j++) {
            int bit = n >> 1;
            for (; (i & bit) != 0; bit >>= 1)
                i ^= bit;
            i ^= bit;
            if (j < i)
                (values[j], values[i]) = (values[i], values[j]);
        }

        for (int length = 2; length <= n; length <<= 1) {
            double angle = 2.0 * Math.PI / length;
            Complex step = new(Math.Cos(angle), Math.Sin(angle));
            for (int start = 0; start < n; start += length) {
                Complex factor = Complex.One;
                int half = length >> 1;
                for (int offset = 0; offset < half; offset++) {
                    Complex even = values[start + offset];
                    Complex odd = values[start + offset + half] * factor;
                    values[start + offset] = even + odd;
                    values[start + offset + half] = even - odd;
                    factor *= step;
                }
            }
        }
    }
}

public static class echo_monitor {
    private static readonly EchoCanMonitor Monitor = new();
    public static EchoCanMonitor Instance => Monitor;
    public static int start_echo_can_monitor(int len) => Monitor.Start(len);
    public static int echo_can_monitor_can_update(short[] coeffs, int len) => Monitor.UpdateCanceller(coeffs.AsSpan(0, Math.Min(len, coeffs.Length)));
    public static int echo_can_monitor_line_model_update(int[] coeffs, int len) => Monitor.UpdateLineModel(coeffs.AsSpan(0, Math.Min(len, coeffs.Length)));
    public static int echo_can_monitor_line_spectrum_update(short[] amp, int len) => Monitor.UpdateLineSpectrum(amp.AsSpan(0, Math.Min(len, amp.Length)));
    public static void echo_can_monitor_wait_to_end() => Monitor.WaitToEnd();
    public static void echo_can_monitor_update_display() => Monitor.UpdateDisplay();
}
