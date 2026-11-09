/*
 * TKFaxEngine - managed C# port
 *
 * g1050_tests.cs
 * Direct managed port of g1050_tests.c.
 * Original implementation by Steve Underwood.
 * GNU General Public License version 2.
 */

#nullable enable

using TKFaxEngineTest.System;

using System.Globalization;

namespace TKFaxEngineTest.Network.Test;

public static class G1050Tests {
    private const int PACKET_SIZE = 256;
    private const int PACKET_INTERVAL = 20;
    private const int SIMULATION_TIME = 300;
    private const int MODEL_NO = 8;
    private const int SPEED_PATTERN_NO = 133;

    public static IMediaMonitor? MediaMonitor { get; set; }

    public static int Main(string[] args) {
        int modelNo = MODEL_NO;
        int speedPatternNo = SPEED_PATTERN_NO;
        int simulationTime = SIMULATION_TIME;
        bool useGui = false;

        for (int argIndex = 0; argIndex < args.Length; argIndex++) {
            string arg = args[argIndex];
            switch (arg) {
                case "-g":
                    useGui = true;
                    break;
                case "-m":
                    if (++argIndex >= args.Length || args[argIndex].Length == 0)
                        return 2;
                    modelNo = args[argIndex][0] - 'A' + 1;
                    if (modelNo < 0 || modelNo > 8) {
                        Console.Error.WriteLine($"Bad model ID '{args[argIndex]}'");
                        return 2;
                    }
                    break;
                case "-s":
                    if (++argIndex >= args.Length ||
                        !int.TryParse(args[argIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out speedPatternNo) ||
                        speedPatternNo < 1 || speedPatternNo > 133) {
                        string value = argIndex < args.Length ? args[argIndex] : string.Empty;
                        Console.Error.WriteLine($"Bad link speed pattern {value}");
                        return 2;
                    }
                    break;
                case "-t":
                    if (++argIndex >= args.Length ||
                        !int.TryParse(args[argIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out simulationTime)) {
                        return 2;
                    }
                    break;
                default:
                    return 2;
            }
        }

        if (useGui && MediaMonitor is null) {
            Console.Error.WriteLine("Graphical monitoring not available");
            return 2;
        }

        int packetsPerSec = 1000 / PACKET_INTERVAL;
        int numPackets = packetsPerSec * simulationTime;
        double[] packetArrivalTimes;
        try {
            packetArrivalTimes = new double[numPackets];
        } catch (OutOfMemoryException) {
            Console.Error.WriteLine("Can't allocate the data buffers");
            return 2;
        }

        _ = packetArrivalTimes;
        G1050.srand48(0x1234567);
        G1050State state = G1050.g1050_init(modelNo, speedPatternNo, PACKET_SIZE, packetsPerSec);
        G1050.g1050_dump_parms(modelNo, speedPatternNo);

        if (useGui)
            MediaMonitor!.start_media_monitor();

        byte[] putPacket = new byte[256];
        byte[] getPacket = new byte[256];
        for (int i = 0; i < putPacket.Length; i++)
            putPacket[i] = (byte)i;

        int packetsPut = 0;
        int packetsReallyPut = 0;
        int packetsGot = 0;
        int oosPacketsGot = 0;
        int missingPacketsGot = 0;
        int highestSeqNoGot = -1;
        int iPacket;

        using StreamWriter output = new("g1050_tests.txt", false);
        for (iPacket = 0; iPacket < numPackets; iPacket++) {
            double currentTime = iPacket * 0.001 * PACKET_INTERVAL;
            if (G1050.g1050_put(state, putPacket, putPacket.Length, iPacket, currentTime) > 0)
                packetsReallyPut++;
            packetsPut++;

            if (iPacket == 5)
                G1050.g1050_queue_dump(state);

            if (iPacket >= 5) {
                while (true) {
                    int getPacketLen = G1050.g1050_get(
                        state,
                        getPacket,
                        getPacket.Length,
                        currentTime,
                        out int getSeqNo,
                        out double getDepartureTime,
                        out double getArrivalTime);
                    if (getPacketLen < 0)
                        break;

                    if (useGui)
                        MediaMonitor!.media_monitor_rx(getSeqNo, getDepartureTime, getArrivalTime);

                    packetsGot++;
                    if (getSeqNo < highestSeqNoGot)
                        oosPacketsGot++;
                    else if (getSeqNo > highestSeqNoGot + 1)
                        missingPacketsGot += getSeqNo - highestSeqNoGot - 1;

                    if (getSeqNo > highestSeqNoGot)
                        highestSeqNoGot = getSeqNo;

                    output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                        $"{getSeqNo}, {getSeqNo * 0.001 * PACKET_INTERVAL:F3}, {getArrivalTime:F8}"));
                }
            }

            if (useGui)
                MediaMonitor!.media_monitor_update_display();
        }

        while (true) {
            int getPacketLen = G1050.g1050_get(
                state,
                getPacket,
                getPacket.Length,
                iPacket * 0.001 * PACKET_INTERVAL + 5.0,
                out int getSeqNo,
                out _,
                out double getArrivalTime);
            if (getPacketLen < 0)
                break;

            packetsGot++;
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{getSeqNo}, {getSeqNo * 0.001 * PACKET_INTERVAL:F3}, {getArrivalTime:F8}"));
        }

        Console.WriteLine($"Put {packetsPut} packets. Really put {packetsReallyPut} packets. Got {packetsGot} packets.");
        Console.WriteLine($"{oosPacketsGot} OOS packets, {missingPacketsGot - oosPacketsGot} missing packets");
        if (packetsReallyPut != packetsGot) {
            Console.WriteLine($"{packetsReallyPut} packets queued, but only {packetsGot} received");
            return 2;
        }

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{100.0 * (packetsPut - packetsReallyPut) / packetsPut:F3}% of packets lost"));
        G1050.g1050_free(state);
        return 0;
    }
}
