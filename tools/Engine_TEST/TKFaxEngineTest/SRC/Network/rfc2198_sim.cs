/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * rfc2198_sim.cs - complete managed port of rfc2198_sim.c and rfc2198_sim.h.
 *
 * Original implementation by Steve Underwood.
 * Licensed under the GNU General Public License version 2.
 */

#nullable enable

using System.Buffers.Binary;
using TKFaxEngineTest.Network.Test;
using g1050_state_t = TKFaxEngineTest.Network.Test.G1050State;

namespace TKFaxEngineTest.Network.Test;

/// <summary>
/// Queue element retained from the native RFC 2198 simulation header.
/// The active implementation uses the G.1050 queue directly, exactly as the C source.
/// </summary>
public sealed class rfc2198_sim_queue_element_t {
    public rfc2198_sim_queue_element_t? next;
    public rfc2198_sim_queue_element_t? prev;
    public int seq_no;
    public double departure_time;
    public double arrival_time;
    public int len;
    public byte[] pkt = Array.Empty<byte>();
}

/// <summary>State corresponding to rfc2198_sim_state_t.</summary>
public sealed class rfc2198_sim_state_t {
    public const int PACKET_HISTORY = 32;
    public const int MAX_PACKET_SIZE = 1024;

    public int redundancy_depth;
    public int next_seq_no;
    public g1050_state_t g1050 = null!;
    public rfc2198_sim_queue_element_t? first;
    public rfc2198_sim_queue_element_t? last;

    public readonly byte[][] tx_pkt = CreatePacketMatrix();
    public readonly int[] tx_pkt_len = new int[PACKET_HISTORY];
    public readonly int[] tx_pkt_seq_no = new int[PACKET_HISTORY];
    public int next_pkt;

    public readonly byte[][] rx_pkt = CreatePacketMatrix();
    public readonly int[] rx_pkt_len = new int[PACKET_HISTORY];
    public readonly int[] rx_pkt_seq_no = new int[PACKET_HISTORY];
    public int rx_queued_pkts;

    private static byte[][] CreatePacketMatrix() {
        var packets = new byte[PACKET_HISTORY][];
        for (int index = 0; index < packets.Length; index++)
            packets[index] = new byte[MAX_PACKET_SIZE];
        return packets;
    }
}

public static class rfc2198_sim {
    private const int PACKET_BUFFER_SIZE = 8192;

    public static rfc2198_sim_state_t? rfc2198_sim_init(
        int model,
        int speed_pattern,
        int packet_size,
        int packet_rate,
        int redundancy_depth) {
        if (redundancy_depth <= 0 ||
            redundancy_depth > rfc2198_sim_state_t.PACKET_HISTORY) {
            return null;
        }

        g1050_state_t? network = G1050.g1050_init(
            model,
            speed_pattern,
            packet_size,
            packet_rate);

        if (network is null)
            return null;

        return new rfc2198_sim_state_t {
            g1050 = network,
            redundancy_depth = redundancy_depth
        };
    }

    public static int rfc2198_sim_free(rfc2198_sim_state_t state) {
        ArgumentNullException.ThrowIfNull(state);
        _ = G1050.g1050_free(state.g1050);
        return 0;
    }

    public static int rfc2198_sim_put(
        rfc2198_sim_state_t state,
        byte[] buffer,
        int length,
        int sequenceNumber,
        double departureTime) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(buffer);

        if ((uint)length > (uint)buffer.Length ||
            length > rfc2198_sim_state_t.MAX_PACKET_SIZE) {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        Array.Copy(
            buffer,
            0,
            state.tx_pkt[state.next_pkt],
            0,
            length);

        state.tx_pkt_len[state.next_pkt] = length;
        state.tx_pkt_seq_no[state.next_pkt] = sequenceNumber;

        var redundantPacket = new byte[PACKET_BUFFER_SIZE];
        int position = 0;
        int slot = state.next_pkt;

        WriteUInt16Native(
            redundantPacket,
            ref position,
            checked((ushort)state.redundancy_depth));

        for (int index = 0; index < state.redundancy_depth; index++) {
            int packetLength = state.tx_pkt_len[slot];
            WriteUInt16Native(
                redundantPacket,
                ref position,
                checked((ushort)packetLength));

            if (position + packetLength > redundantPacket.Length)
                throw new InvalidOperationException("The RFC 2198 packet buffer is too small.");

            Array.Copy(
                state.tx_pkt[slot],
                0,
                redundantPacket,
                position,
                packetLength);

            position += packetLength;
            slot = (slot - 1) & 0x1F;
        }

        state.next_pkt = (state.next_pkt + 1) & 0x1F;

        return G1050.g1050_put(
            state.g1050,
            redundantPacket,
            position,
            sequenceNumber,
            departureTime);
    }

    public static int rfc2198_sim_get(
        rfc2198_sim_state_t state,
        byte[] buffer,
        int maxLength,
        double currentTime,
        ref int sequenceNumber,
        ref double departureTime,
        ref double arrivalTime) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(buffer);

        if (state.rx_queued_pkts != 0) {
            state.rx_queued_pkts--;
            int queuedIndex = state.rx_queued_pkts;
            int queuedLength = state.rx_pkt_len[queuedIndex];

            if (queuedLength > maxLength || queuedLength > buffer.Length)
                throw new ArgumentException("The destination packet buffer is too small.", nameof(buffer));

            Array.Copy(
                state.rx_pkt[queuedIndex],
                0,
                buffer,
                0,
                queuedLength);

            sequenceNumber = state.rx_pkt_seq_no[queuedIndex];
            return queuedLength;
        }

        var networkPacket = new byte[checked(state.redundancy_depth * 1024)];
        int length = G1050.g1050_get(
            state.g1050,
            networkPacket,
            networkPacket.Length,
            currentTime,
            out int networkSequenceNumber,
            out double networkDepartureTime,
            out double networkArrivalTime);

        departureTime = networkDepartureTime;
        arrivalTime = networkArrivalTime;

        if (length <= 0)
            return length;

        int position = 0;
        int redundancyDepth = ReadUInt16Native(networkPacket, ref position);
        int index = 0;

        if (networkSequenceNumber > state.next_seq_no) {
            state.rx_queued_pkts =
                networkSequenceNumber - state.next_seq_no;

            if (state.rx_queued_pkts >= redundancyDepth)
                state.rx_queued_pkts = redundancyDepth - 1;

            for (index = 0; index < state.rx_queued_pkts; index++) {
                int recoveredLength =
                    ReadUInt16Native(networkPacket, ref position);

                if (recoveredLength > rfc2198_sim_state_t.MAX_PACKET_SIZE ||
                    position + recoveredLength > length) {
                    throw new InvalidDataException("Malformed RFC 2198 redundancy packet.");
                }

                state.rx_pkt_len[index] = recoveredLength;
                Array.Copy(
                    networkPacket,
                    position,
                    state.rx_pkt[index],
                    0,
                    recoveredLength);

                state.rx_pkt_seq_no[index] =
                    networkSequenceNumber - index;

                position += recoveredLength;
            }
        }

        sequenceNumber = networkSequenceNumber - index;
        int outputLength = ReadUInt16Native(networkPacket, ref position);

        if (outputLength > maxLength ||
            outputLength > buffer.Length ||
            position + outputLength > length) {
            throw new InvalidDataException("Malformed RFC 2198 output packet.");
        }

        Array.Copy(
            networkPacket,
            position,
            buffer,
            0,
            outputLength);

        state.next_seq_no = networkSequenceNumber + 1;
        return outputLength;
    }

    private static void WriteUInt16Native(
        byte[] destination,
        ref int position,
        ushort value) {
        BinaryPrimitives.WriteUInt16LittleEndian(
            destination.AsSpan(position, sizeof(ushort)),
            value);

        position += sizeof(ushort);
    }

    private static ushort ReadUInt16Native(
        byte[] source,
        ref int position) {
        if (position + sizeof(ushort) > source.Length)
            throw new InvalidDataException("Truncated RFC 2198 packet.");

        ushort value = BinaryPrimitives.ReadUInt16LittleEndian(
            source.AsSpan(position, sizeof(ushort)));

        position += sizeof(ushort);
        return value;
    }
}
