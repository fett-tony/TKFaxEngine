/*
 * TKFaxEngine - managed C# port of line_model_monitor.cpp/.h.
 * Contract, snapshot type and implementation are combined in this file.
 */

#nullable enable

using System;
using System.Numerics;

namespace TKFaxEngineTest.Network.Test;

public interface ILineModelMonitor {
    int start_line_model_monitor(int len);
    int line_model_monitor_can_update(ReadOnlySpan<float> coeffs, int len);
    int line_model_monitor_line_model_update(ReadOnlySpan<float> coeffs, int len);
    int line_model_monitor_line_spectrum_update(ReadOnlySpan<short> amplitude, int len);
    void line_model_monitor_wait_to_end();
    void line_model_monitor_update_display();
}

public sealed record LineModelMonitorSnapshot(
    double[] SpectrumFrequencyHz,
    double[] SpectrumDbm0,
    double[] CancellerCoefficients,
    double CancellerMinimum,
    double CancellerMaximum,
    double[] LineModelCoefficients,
    double LineModelMinimum,
    double LineModelMaximum,
    double AudioPeak);

public static class line_model_monitor {
    private static readonly object Gate = new();
    private static readonly double[] Input = new double[1024];
    private static readonly double[] SpectrumFrequency = new double[512];
    private static readonly double[] SpectrumLevel = new double[512];
    private static double[] Canceller = Array.Empty<double>();
    private static double[] Model = Array.Empty<double>();
    private static double CancellerMin = -0.1;
    private static double CancellerMax = 0.1;
    private static double ModelMin = -0.1;
    private static double ModelMax = 0.1;
    private static double AudioPeak;
    private static int InputPointer;
    private static int ExpectedLength;
    private static int Skip;

    public static event Action<LineModelMonitorSnapshot>? Updated;

    public static int line_model_monitor_can_update(float[] coeffs, int len) {
        ArgumentNullException.ThrowIfNull(coeffs);
        if (len <= 0 || len > coeffs.Length || len > 512) throw new ArgumentOutOfRangeException(nameof(len));
        lock (Gate) {
            Canceller = new double[len];
            CancellerMin = coeffs[0];
            CancellerMax = coeffs[0];
            for (int i = 0; i < len; i++) {
                Canceller[i] = coeffs[i];
                if (CancellerMin > coeffs[i]) CancellerMin = coeffs[i];
                if (CancellerMax < coeffs[i]) CancellerMax = coeffs[i];
            }
            if (CancellerMax == CancellerMin) CancellerMax += 0.2;
        }
        if (++Skip >= 100) { Skip = 0; Publish(); }
        return 0;
    }

    public static int line_model_monitor_line_model_update(float[] coeffs, int len) {
        ArgumentNullException.ThrowIfNull(coeffs);
        if (len <= 0 || len > coeffs.Length || len > 512) throw new ArgumentOutOfRangeException(nameof(len));
        lock (Gate) {
            Model = new double[len];
            ModelMin = coeffs[0];
            ModelMax = coeffs[0];
            for (int i = 0; i < len; i++) {
                Model[i] = coeffs[i];
                if (ModelMin > coeffs[i]) ModelMin = coeffs[i];
                if (ModelMax < coeffs[i]) ModelMax = coeffs[i];
            }
            if (ModelMax == ModelMin) ModelMax += 0.2;
        }
        if (++Skip >= 100) { Skip = 0; Publish(); }
        return 0;
    }

    public static int line_model_monitor_line_spectrum_update(short[] amplitude, int len) {
        ArgumentNullException.ThrowIfNull(amplitude);
        if (len < 0 || len > amplitude.Length) throw new ArgumentOutOfRangeException(nameof(len));

        lock (Gate) {
            AudioPeak = 0.0;
            for (int i = 0; i < len; i++)
                AudioPeak = Math.Max(AudioPeak, Math.Abs(amplitude[i] / 32768.0));

            if (InputPointer + len < 512) {
                for (int i = 0; i < len; i++) Input[InputPointer + i] = amplitude[i];
                InputPointer += len;
                return 0;
            }

            if (len >= 512) {
                int x = len - 512;
                for (int i = 0; i < 512; i++) Input[i] = amplitude[x + i];
            } else {
                int x = 512 - len;
                for (int i = 0; i < x; i++) Input[i] = Input[InputPointer - x + i];
                for (int i = x; i < 512; i++) Input[i] = amplitude[i - x];
            }
            InputPointer = 0;

            var fft = new Complex[1024];
            for (int i = 0; i < 1024; i++) fft[i] = new Complex(Input[i], 0.0);
            TransformBackward(fft);
            for (int i = 0; i < 512; i++) {
                SpectrumFrequency[i] = i * 4000.0 / 512.0;
                double power = fft[i].Real * fft[i].Real + fft[i].Imaginary * fft[i].Imaginary;
                SpectrumLevel[i] = 10.0 * Math.Log10(power / (256.0 * 32768.0 * 256.0 * 32768.0) + 1.0e-10) + 3.14;
            }
        }
        Publish();
        return 0;
    }

    public static int start_line_model_monitor(int len) {
        if (len <= 0 || len > 512) throw new ArgumentOutOfRangeException(nameof(len));
        lock (Gate) {
            ExpectedLength = len;
            InputPointer = 0;
            Array.Clear(Input);
            Array.Clear(SpectrumFrequency);
            Array.Clear(SpectrumLevel);
            Canceller = Array.Empty<double>();
            Model = Array.Empty<double>();
            AudioPeak = 0.0;
        }
        Publish();
        return 0;
    }

    public static void line_model_monitor_wait_to_end() {
        Console.Error.WriteLine("Processing complete.  Press the <enter> key to end");
        Console.ReadLine();
    }

    public static void line_model_monitor_update_display() {
        for (int i = 0; i < 12; i++) Publish();
    }

    public static LineModelMonitorSnapshot GetSnapshot() {
        lock (Gate) {
            return new LineModelMonitorSnapshot(
                (double[])SpectrumFrequency.Clone(),
                (double[])SpectrumLevel.Clone(),
                (double[])Canceller.Clone(), CancellerMin, CancellerMax,
                (double[])Model.Clone(), ModelMin, ModelMax,
                AudioPeak);
        }
    }

    private static void Publish() {
        LineModelMonitorSnapshot snapshot = GetSnapshot();
        Updated?.Invoke(snapshot);
    }

    private static void TransformBackward(Complex[] data) {
        int n = data.Length;
        for (int j = 1, i = 0; j < n; j++) {
            int bit = n >> 1;
            for (; (i & bit) != 0; bit >>= 1) i ^= bit;
            i ^= bit;
            if (j < i) (data[j], data[i]) = (data[i], data[j]);
        }

        for (int length = 2; length <= n; length <<= 1) {
            double angle = 2.0 * Math.PI / length;
            Complex step = new(Math.Cos(angle), Math.Sin(angle));
            for (int i = 0; i < n; i += length) {
                Complex w = Complex.One;
                int half = length >> 1;
                for (int j = 0; j < half; j++) {
                    Complex even = data[i + j];
                    Complex odd = data[i + j + half] * w;
                    data[i + j] = even + odd;
                    data[i + j + half] = even - odd;
                    w *= step;
                }
            }
        }
    }

}
