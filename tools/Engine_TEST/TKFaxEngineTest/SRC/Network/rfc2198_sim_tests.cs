/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * rfc2198_sim_tests.cs - complete managed port of rfc2198_sim_tests.c.
 *
 * Original test written by Steve Underwood.
 * Licensed under the GNU General Public License version 2.
 */

#nullable enable

using System.Globalization;
using System.Text;
using static TKFaxEngineTest.Network.Test.rfc2198_sim;

namespace TKFaxEngineTest.Network.Test;

public static class rfc2198_sim_tests {
    private const int PACKET_SIZE = 256;
    private const int PACKET_INTERVAL = 20;
    private const int SIMULATION_TIME = 300;
    private const int MODEL_NO = 8;
    private const int SPEED_PATTERN_NO = 133;
    private const string OUTPUT_FILE_NAME = "rfc2198_sim_tests.txt";

    public static int main(string[] testArguments) {
        try {
            return tkfx_rfc2198_sim_tests_body_main(testArguments);
        } catch (Rfc2198SimTestsExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_rfc2198_sim_tests_body_main(string[] testArguments) {
        int modelNumber = MODEL_NO;
        int speedPatternNumber = SPEED_PATTERN_NO;
        int simulationTime = SIMULATION_TIME;

        for (int index = 0; index < testArguments.Length; index++) {
            string option = testArguments[index];
            switch (option) {
                case "-g":
                    Console.Error.WriteLine("Graphical monitoring not available");
                    Exit(2);
                    break;

                case "-m": {
                        string value = NextArgument(testArguments, testArguments.Length, ref index, option);
                        modelNumber = value[0] - 'A' + 1;
                        if (modelNumber < 0 || modelNumber > 8) {
                            Console.Error.WriteLine($"Bad model ID '{value}'");
                            Exit(2);
                        }
                        break;
                    }

                case "-s": {
                        string value = NextArgument(testArguments, testArguments.Length, ref index, option);
                        speedPatternNumber = ParseInt(value);
                        if (speedPatternNumber < 1 || speedPatternNumber > 133) {
                            Console.Error.WriteLine($"Bad link speed pattern {value}");
                            Exit(2);
                        }
                        break;
                    }

                case "-t":
                    simulationTime = ParseInt(
                        NextArgument(testArguments, testArguments.Length, ref index, option));
                    break;

                default:
                    Exit(2);
                    break;
            }
        }

        StreamWriter output;
        try {
            output = new StreamWriter(
                OUTPUT_FILE_NAME,
                append: false,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine($"Can't open {OUTPUT_FILE_NAME}: {ex.Message}");
            return 2;
        }

        using (output) {
            int packetsPerSecond = 1000 / PACKET_INTERVAL;
            int numberOfPackets = checked(packetsPerSecond * simulationTime);

            double[] packetArrivalTimes;
            try {
                packetArrivalTimes = new double[numberOfPackets];
            } catch (OutOfMemoryException) {
                Console.Error.WriteLine("Can't allocate the data buffers");
                return 2;
            }

            Array.Clear(packetArrivalTimes, 0, packetArrivalTimes.Length);

            rfc2198_sim_state_t? simulation = rfc2198_sim_init(
                modelNumber,
                speedPatternNumber,
                PACKET_SIZE,
                packetsPerSecond,
                3);

            if (simulation is null) {
                Console.Error.WriteLine("Failed to start the G.1050 model");
                Exit(2);
            }

            var putPacket = new byte[PACKET_SIZE];
            var getPacket = new byte[PACKET_SIZE];
            for (int index = 0; index < putPacket.Length; index++)
                putPacket[index] = (byte)index;

            int putPacketLength = PACKET_SIZE;
            int getPacketLength = -1;
            int getSequenceNumber = -1;
            double getDepartureTime = -1.0;
            double getArrivalTime = -1.0;

            int packetsPut = 0;
            int packetsReallyPut = 0;
            int packetsGot = 0;
            int outOfSequencePacketsGot = 0;
            int missingPacketsGot = 0;
            int highestSequenceNumberGot = -1;
            int packetIndex;

            for (packetIndex = 0; packetIndex < numberOfPackets; packetIndex++) {
                double currentTime =
                    packetIndex * 0.001 * PACKET_INTERVAL;

                int length = rfc2198_sim_put(
                    simulation,
                    putPacket,
                    putPacketLength,
                    packetIndex,
                    currentTime);

                if (length > 0)
                    packetsReallyPut++;

                packetsPut++;

                if (packetIndex >= 5) {
                    do {
                        getPacketLength = rfc2198_sim_get(
                            simulation,
                            getPacket,
                            PACKET_SIZE,
                            currentTime,
                            ref getSequenceNumber,
                            ref getDepartureTime,
                            ref getArrivalTime);

                        if (getPacketLength >= 0) {
                            packetsGot++;

                            if (getSequenceNumber < highestSequenceNumberGot) {
                                outOfSequencePacketsGot++;
                            } else if (getSequenceNumber > highestSequenceNumberGot + 1) {
                                missingPacketsGot +=
                                    getSequenceNumber -
                                    highestSequenceNumberGot -
                                    1;
                            }

                            if (getSequenceNumber > highestSequenceNumberGot)
                                highestSequenceNumberGot = getSequenceNumber;

                            output.WriteLine(
                                string.Format(
                                    CultureInfo.InvariantCulture,
                                    "{0}, {1:F3}, {2:F8}",
                                    getSequenceNumber,
                                    getSequenceNumber * 0.001 * PACKET_INTERVAL,
                                    getArrivalTime));
                        }
                    }
                    while (getPacketLength >= 0);
                }
            }

            do {
                getPacketLength = rfc2198_sim_get(
                    simulation,
                    getPacket,
                    PACKET_SIZE,
                    packetIndex * 0.001 * PACKET_INTERVAL + 5.0,
                    ref getSequenceNumber,
                    ref getDepartureTime,
                    ref getArrivalTime);

                if (getPacketLength >= 0) {
                    packetsGot++;
                    output.WriteLine(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{0}, {1:F3}, {2:F8}",
                            getSequenceNumber,
                            getSequenceNumber * 0.001 * PACKET_INTERVAL,
                            getArrivalTime));
                }
            }
            while (getPacketLength >= 0);

            _ = rfc2198_sim_free(simulation);

            Console.WriteLine(
                $"Put {packetsPut} packets. Really put {packetsReallyPut} packets. Got {packetsGot} packets.");
            Console.WriteLine(
                $"{outOfSequencePacketsGot} OOS packets, {missingPacketsGot - outOfSequencePacketsGot} missing packets");
            Console.WriteLine(
                $"{packetsReallyPut} packets queued, {packetsGot} received");
            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:F3}% of packets lost before redundancy",
                    100.0 * (packetsPut - packetsReallyPut) / packetsPut));
            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:F3}% of packets lost after redundancy",
                    100.0 * (packetsPut - packetsGot) / packetsPut));
        }

        return 0;
    }

    private static string NextArgument(
        string[] arguments,
        int argumentCount,
        ref int index,
        string option) {
        index++;
        if (index >= argumentCount || index >= arguments.Length)
            throw new ArgumentException($"Missing value for {option}.");
        return arguments[index];
    }

    private static int ParseInt(string value) =>
        int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static void Exit(int code) =>
        throw new Rfc2198SimTestsExitException(code);

    public sealed class Rfc2198SimTestsExitException : Exception {
        public Rfc2198SimTestsExitException(int exitCode)
            : base($"rfc2198_sim_tests exited with code {exitCode}.") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }
}
