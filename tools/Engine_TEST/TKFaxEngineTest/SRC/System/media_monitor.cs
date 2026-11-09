/*
 * TKFaxEngine - combined managed C# port of media_monitor.cpp and media_monitor.h.
 * The original 500-packet delay ring and 2,000-bin delay histogram are retained.
 */

#nullable enable

namespace TKFaxEngineTest.System;

public interface IMediaMonitor {
    int start_media_monitor();
    void media_monitor_rx(int seqNo, double departureTime, double arrivalTime);
    void media_monitor_wait_to_end();
    void media_monitor_update_display();
}

public sealed record MediaMonitorSnapshot(
    double[] PacketNumbers,
    double[] PacketDelaysMilliseconds,
    double PacketDelayMinimum,
    double PacketDelayMaximum,
    double[] DelayBinsMilliseconds,
    double[] DelayHistogram,
    int HistogramMinimumDelay,
    int HistogramMaximumDelay,
    double HistogramMaximumCount,
    int HighestSequenceNumberSeen);

public static class media_monitor {
    private static readonly object Gate = new();
    private static readonly double[] SentPlot = new double[500];
    private static readonly double[] ReceivedHistogram = new double[2000];
    private static int _skip;
    private static double _sentMinimum = 99999.0;
    private static double _sentMaximum;
    private static double _receivedMaximum;
    private static int _minimumDifference = 2000;
    private static int _maximumDifference;
    private static int _highestSequenceNumberSeen = -1;

    public static event Action<MediaMonitorSnapshot>? Updated;

    public static void media_monitor_rx(int seq_no, double departure_time, double arrival_time) {
        bool publish = false;
        lock (Gate) {
            double fdiff = (arrival_time - departure_time) * 1000.0;
            int diff = (int)fdiff;
            if (diff < 0) diff = 0;
            else if (diff > 1999) diff = 1999;

            ReceivedHistogram[diff]++;
            if (ReceivedHistogram[diff] > _receivedMaximum) _receivedMaximum = ReceivedHistogram[diff];
            if (diff > _maximumDifference) _maximumDifference = diff;
            if (diff < _minimumDifference) _minimumDifference = diff - 1;

            if (seq_no > _highestSequenceNumberSeen + 1) {
                for (int i = _highestSequenceNumberSeen + 1; i < seq_no; i++) SentPlot[i % 500] = 0.0;
            }
            SentPlot[seq_no % 500] = fdiff;
            if (fdiff > _sentMaximum) _sentMaximum = fdiff;
            if (fdiff < _sentMinimum) _sentMinimum = fdiff - 1.0;
            if (seq_no > _highestSequenceNumberSeen) _highestSequenceNumberSeen = seq_no;
            if (++_skip >= 100) { _skip = 0; publish = true; }
        }
        if (publish) Publish();
    }

    public static int start_media_monitor() {
        lock (Gate) {
            Array.Clear(SentPlot);
            Array.Clear(ReceivedHistogram);
            _skip = 0;
            _sentMinimum = 99999.0;
            _sentMaximum = 0.0;
            _receivedMaximum = 0.0;
            _minimumDifference = 2000;
            _maximumDifference = 0;
            _highestSequenceNumberSeen = -1;
        }
        Publish();
        return 0;
    }

    public static void media_monitor_wait_to_end() {
        Console.Error.WriteLine("Processing complete.  Press the <enter> key to end");
        Console.ReadLine();
    }

    public static void media_monitor_update_display() {
        for (int i = 0; i < 12; i++) Publish();
    }

    public static MediaMonitorSnapshot GetSnapshot() {
        lock (Gate) {
            var packetNumbers = new double[500];
            var packetDelays = new double[500];
            for (int i = 0; i < 500; i++) {
                packetNumbers[i] = i;
                packetDelays[i] = SentPlot[i];
            }
            var bins = new double[2000];
            var histogram = new double[2000];
            for (int i = 0; i < 2000; i++) { bins[i] = i; histogram[i] = ReceivedHistogram[i]; }
            return new MediaMonitorSnapshot(packetNumbers, packetDelays, _sentMinimum, _sentMaximum, bins, histogram, _minimumDifference, _maximumDifference, _receivedMaximum, _highestSequenceNumberSeen);
        }
    }

    private static void Publish() {
        MediaMonitorSnapshot snapshot = GetSnapshot();
        Updated?.Invoke(snapshot);
    }

}
