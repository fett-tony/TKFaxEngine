/*
 * TKFaxEngine - managed C# port of modem_monitor.cpp and modem_monitor.h.
 *
 * Original implementation written by Steve Underwood.
 * The constellation ring, equalizer validation/scaling, 12,000-point symbol
 * and carrier trackers and the original dBm0 power recursion are retained.
 */

#nullable enable
using TKFaxEngine;
using System.Globalization;

namespace TKFaxEngineTest.System;

public interface IQamMonitor {
    int qam_monitor_clear_constel();
    int qam_monitor_update_constel(in ComplexFloat point);
    int qam_monitor_update_equalizer(ReadOnlySpan<ComplexFloat> coeffs, int len);
    int qam_monitor_update_int_equalizer(ReadOnlySpan<ComplexInt16> coeffs, int len);
    int qam_monitor_update_symbol_tracking(float totalCorrection);
    int qam_monitor_update_carrier_tracking(float carrier);
    int qam_monitor_update_audio_level(ReadOnlySpan<short> amplitude, int len);
    void qam_wait_to_end();
}

public interface IQamMonitorFactory {
    IQamMonitor qam_monitor_init(float constellationWidth, float constellationScaling, string tag);
}


public sealed record QamMonitorSnapshot(
    string Tag,
    float ConstellationWidth,
    ComplexFloat[] Constellation,
    double[] EqualizerX,
    double[] EqualizerReal,
    double[] EqualizerImaginary,
    double EqualizerMinimum,
    double EqualizerMaximum,
    double[] SymbolTracking,
    double SymbolMinimum,
    double SymbolMaximum,
    double[] CarrierTracking,
    double CarrierMinimum,
    double CarrierMaximum,
    double AudioPeak,
    double AudioLevelDbm0,
    string AudioLevelText);

public sealed class qam_monitor_t : IDisposable {
    internal const int SYMBOL_TRACKER_POINTS = 12000;
    internal const int CARRIER_TRACKER_POINTS = 12000;
    internal const int CONSTELLATION_CAPACITY = 100000;

    internal readonly object Gate = new();
    internal readonly ComplexFloat?[] ConstellationPoints = new ComplexFloat?[CONSTELLATION_CAPACITY];
    internal readonly float[] SymbolTracker = new float[SYMBOL_TRACKER_POINTS];
    internal readonly float[] CarrierTracker = new float[CARRIER_TRACKER_POINTS];

    internal float ConstellationScaling;
    internal float ConstellationWidth;
    internal string Tag = "QAM monitor";
    internal int ConstellationWindow;
    internal int NextConstellationPoint;
    internal int Skip;

    internal double[] EqualizerX = Array.Empty<double>();
    internal double[] EqualizerReal = Array.Empty<double>();
    internal double[] EqualizerImaginary = Array.Empty<double>();
    internal double EqualizerMinimum = -0.1;
    internal double EqualizerMaximum = 0.1;

    internal int SymbolTrackPoints;
    internal int SymbolTrackPointer;
    internal int SymbolTrackWindow;
    internal double SymbolMinimum = -0.1;
    internal double SymbolMaximum = 0.1;

    internal int CarrierPoints;
    internal int CarrierPointer;
    internal int CarrierWindow;
    internal double CarrierMinimum = -0.1;
    internal double CarrierMaximum = 0.1;

    internal int PowerReading;
    internal double AudioPeak;
    internal double AudioLevelDbm0 = -90.169;
    internal bool Disposed;

    public event Action<QamMonitorSnapshot>? Updated;

    public QamMonitorSnapshot GetSnapshot() {
        lock (Gate) {
            var constellation = new ComplexFloat[ConstellationWindow];
            int output = 0;
            for (int i = NextConstellationPoint; i < ConstellationWindow; i++)
                if (ConstellationPoints[i] is ComplexFloat point) constellation[output++] = point;
            for (int i = 0; i < NextConstellationPoint; i++)
                if (ConstellationPoints[i] is ComplexFloat point) constellation[output++] = point;
            if (output != constellation.Length) Array.Resize(ref constellation, output);

            return new QamMonitorSnapshot(
                Tag,
                ConstellationWidth,
                constellation,
                (double[])EqualizerX.Clone(),
                (double[])EqualizerReal.Clone(),
                (double[])EqualizerImaginary.Clone(),
                EqualizerMinimum,
                EqualizerMaximum,
                modem_monitor.CopyChronological(SymbolTracker, SymbolTrackPointer, SymbolTrackPoints),
                SymbolMinimum,
                SymbolMaximum,
                modem_monitor.CopyChronological(CarrierTracker, CarrierPointer, CarrierPoints),
                CarrierMinimum,
                CarrierMaximum,
                AudioPeak,
                AudioLevelDbm0,
                AudioLevelDbm0.ToString("F1", CultureInfo.InvariantCulture) + "dBm0");
        }
    }

    internal void Publish() {
        QamMonitorSnapshot snapshot = GetSnapshot();
        Updated?.Invoke(snapshot);
    }

    public void Dispose() {
        if (Disposed) return;
        Disposed = true;
        GC.SuppressFinalize(this);
    }

    internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Disposed, this);

}

public static class modem_monitor {
    public static qam_monitor_t qam_monitor_init(float constel_width, float constel_scaling, string? tag) {
        if (constel_scaling == 0.0f) throw new ArgumentOutOfRangeException(nameof(constel_scaling));
        var s = new qam_monitor_t {
            ConstellationWidth = constel_width,
            ConstellationScaling = 1.0f / constel_scaling,
            Tag = string.IsNullOrEmpty(tag) ? "QAM monitor" : tag,
            NextConstellationPoint = 0,
            ConstellationWindow = 10000,
            CarrierPoints = 0,
            CarrierPointer = 0,
            CarrierWindow = 100,
            SymbolTrackPoints = 0,
            SymbolTrackPointer = 0,
            SymbolTrackWindow = 10000
        };
        s.Publish();
        return s;
    }

    public static int qam_monitor_clear_constel(qam_monitor_t s) {
        ArgumentNullException.ThrowIfNull(s);
        s.ThrowIfDisposed();
        lock (s.Gate) {
            for (int i = 0; i < s.ConstellationWindow; i++) s.ConstellationPoints[i] = null;
            s.NextConstellationPoint = 0;
        }
        s.Publish();
        return 0;
    }

    public static int qam_monitor_update_constel(qam_monitor_t s, in ComplexFloat pt) {
        ArgumentNullException.ThrowIfNull(s);
        s.ThrowIfDisposed();
        lock (s.Gate) {
            s.ConstellationPoints[s.NextConstellationPoint++] = pt;
            if (s.NextConstellationPoint >= s.ConstellationWindow) s.NextConstellationPoint = 0;
            if (++s.Skip < 100) return 0;
            s.Skip = 0;
        }
        s.Publish();
        return 0;
    }

    public static int qam_monitor_update_equalizer(qam_monitor_t s, ComplexFloat[] coeffs, int len) {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(coeffs);
        s.ThrowIfDisposed();
        if (len <= 0 || len > coeffs.Length || len > 100) throw new ArgumentOutOfRangeException(nameof(len));

        for (int i = 0; i < len; i++) {
            float re = coeffs[i].Re;
            float im = coeffs[i].Im;
            if (float.IsNaN(re) || float.IsInfinity(re) || float.IsNaN(im) || float.IsInfinity(im) || re < -20.0f || re > 20.0f || im < -20.0f || im > 20.0f)
                return -1;
        }

        lock (s.Gate) {
            s.EqualizerX = new double[len];
            s.EqualizerReal = new double[len];
            s.EqualizerImaginary = new double[len];
            float min = Math.Min(coeffs[0].Re, coeffs[0].Im);
            float max = Math.Max(coeffs[0].Re, coeffs[0].Im);
            for (int i = 0; i < len; i++) {
                s.EqualizerX[i] = (i - len / 2) / 2.0;
                s.EqualizerReal[i] = coeffs[i].Re * s.ConstellationScaling;
                s.EqualizerImaginary[i] = coeffs[i].Im * s.ConstellationScaling;
                min = Math.Min(min, Math.Min(coeffs[i].Re, coeffs[i].Im));
                max = Math.Max(max, Math.Max(coeffs[i].Re, coeffs[i].Im));
            }
            s.EqualizerMinimum = min;
            s.EqualizerMaximum = max == min ? max + 0.2 : max;
        }
        s.Publish();
        return 0;
    }

    public static int qam_monitor_update_int_equalizer(qam_monitor_t s, ComplexInt16[] coeffs, int len) {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(coeffs);
        s.ThrowIfDisposed();
        if (len <= 0 || len > coeffs.Length || len > 100) throw new ArgumentOutOfRangeException(nameof(len));

        lock (s.Gate) {
            s.EqualizerX = new double[len];
            s.EqualizerReal = new double[len];
            s.EqualizerImaginary = new double[len];
            float min = Math.Min(coeffs[0].Re, coeffs[0].Im);
            float max = Math.Max(coeffs[0].Re, coeffs[0].Im);
            for (int i = 0; i < len; i++) {
                s.EqualizerX[i] = (i - len / 2) / 2.0;
                s.EqualizerReal[i] = coeffs[i].Re * s.ConstellationScaling;
                s.EqualizerImaginary[i] = coeffs[i].Im * s.ConstellationScaling;
                min = Math.Min(min, Math.Min(coeffs[i].Re, coeffs[i].Im));
                max = Math.Max(max, Math.Max(coeffs[i].Re, coeffs[i].Im));
            }
            min *= s.ConstellationScaling;
            max *= s.ConstellationScaling;
            s.EqualizerMinimum = min;
            s.EqualizerMaximum = max == min ? max + 0.2 : max;
        }
        s.Publish();
        return 0;
    }

    public static int qam_monitor_update_symbol_tracking(qam_monitor_t s, float total_correction) {
        ArgumentNullException.ThrowIfNull(s);
        s.ThrowIfDisposed();
        lock (s.Gate) {
            s.SymbolTracker[s.SymbolTrackPointer++] = total_correction;
            if (s.SymbolTrackPoints < qam_monitor_t.SYMBOL_TRACKER_POINTS) s.SymbolTrackPoints++;
            if (s.SymbolTrackPointer >= qam_monitor_t.SYMBOL_TRACKER_POINTS) s.SymbolTrackPointer = 0;
            ComputeExtrema(s.SymbolTracker, s.SymbolTrackPointer, s.SymbolTrackPoints, out float min, out float max);
            s.SymbolMinimum = min;
            s.SymbolMaximum = Math.Abs(max - min) < 0.05f ? max + 0.05f : max;
        }
        return 0;
    }

    public static int qam_monitor_update_carrier_tracking(qam_monitor_t s, float carrier_freq) {
        ArgumentNullException.ThrowIfNull(s);
        s.ThrowIfDisposed();
        lock (s.Gate) {
            s.CarrierTracker[s.CarrierPointer++] = carrier_freq;
            if (s.CarrierPoints < qam_monitor_t.CARRIER_TRACKER_POINTS) s.CarrierPoints++;
            if (s.CarrierPointer >= qam_monitor_t.CARRIER_TRACKER_POINTS) s.CarrierPointer = 0;
            ComputeExtrema(s.CarrierTracker, s.CarrierPointer, s.CarrierPoints, out float min, out float max);
            s.CarrierMinimum = min;
            s.CarrierMaximum = Math.Abs(max - min) < 0.05f ? max + 0.05f : max;
        }
        return 0;
    }

    public static int qam_monitor_update_audio_level(qam_monitor_t s, short[] amp, int len) {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(amp);
        s.ThrowIfDisposed();
        if (len < 0 || len > amp.Length) throw new ArgumentOutOfRangeException(nameof(len));
        lock (s.Gate) {
            for (int i = 0; i < len; i++) {
                s.AudioPeak = Math.Max(s.AudioPeak, Math.Abs(amp[i] / 32768.0));
                int samplePower = amp[i] * amp[i];
                s.PowerReading = unchecked(s.PowerReading + ((samplePower - s.PowerReading) >> 10));
            }
            s.AudioLevelDbm0 = 10.0 * Math.Log10((double)s.PowerReading / (32767.0 * 32767.0) + 1.0e-10) + 3.14 + 3.02;
        }
        s.Publish();
        return 0;
    }

    public static void qam_wait_to_end(qam_monitor_t s) {
        ArgumentNullException.ThrowIfNull(s);
        s.ThrowIfDisposed();
        Console.Error.WriteLine("Processing complete.  Press the <enter> key to end");
        Console.ReadLine();
    }

    internal static double[] CopyChronological(float[] source, int pointer, int count) {
        var result = new double[count];
        int output = 0;
        for (int i = pointer; i < count; i++) result[output++] = source[i];
        for (int i = 0; i < pointer && output < count; i++) result[output++] = source[i];
        return result;
    }

    private static void ComputeExtrema(float[] values, int pointer, int count, out float min, out float max) {
        if (count == 0) { min = max = 0.0f; return; }
        min = max = values[0];
        for (int i = pointer; i < count; i++) { min = Math.Min(min, values[i]); max = Math.Max(max, values[i]); }
        for (int i = 0; i < pointer; i++) { min = Math.Min(min, values[i]); max = Math.Max(max, values[i]); }
    }
}
