/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * pcap_parse.cs
 *
 * Managed C# port of the supplied spanDSP test helper. It reads classic PCAP
 * files and extracts IPv4/IPv6 UDP payloads for Ethernet, Linux cooked capture
 * and DLT_NULL link layers without a libpcap runtime dependency.
 */

#nullable enable

using System.Buffers.Binary;
namespace TKFaxEngineTest.System;

public delegate int PcapTimingUpdateHandler(object? userData, ref Timeval timestamp);

public delegate int PcapPacketHandler(
    object? userData,
    ReadOnlyMemory<byte> packet,
    int length,
    bool forward);

public static class PcapParse {
    private const uint TKFX_DLT_NULL = 0U;
    private const uint TKFX_DLT_EN10MB = 1U;
    private const uint TKFX_DLT_LINUX_SLL = 113U;
    private const uint MaximumCapturedPacketLength = 16U * 1024U * 1024U;

    private static ushort ReadBe16(ReadOnlySpan<byte> source) {
        return BinaryPrimitives.ReadUInt16BigEndian(source);
    }

    private static uint ReadU32(ReadOnlySpan<byte> source, bool swap) {
        return swap
            ? BinaryPrimitives.ReadUInt32BigEndian(source)
            : BinaryPrimitives.ReadUInt32LittleEndian(source);
    }

    private static ushort ReadU16(ReadOnlySpan<byte> source, bool swap) {
        return swap
            ? BinaryPrimitives.ReadUInt16BigEndian(source)
            : BinaryPrimitives.ReadUInt16LittleEndian(source);
    }

    private static bool AddressIsZero(ReadOnlySpan<byte> address, int length) {
        if (address.Length < length)
            return false;

        for (int i = 0; i < length; i++) {
            if (address[i] != 0)
                return false;
        }

        return true;
    }

    private static bool AddressMatches(
        ReadOnlySpan<byte> filter,
        ReadOnlySpan<byte> packet,
        int length) {
        return AddressIsZero(filter, length) ||
               (filter.Length >= length &&
                packet.Length >= length &&
                filter[..length].SequenceEqual(packet[..length]));
    }

    private static int DecodeUdpPacket(
        byte[] packet,
        uint linkType,
        byte[] srcAddr,
        ushort srcPort,
        byte[] destAddr,
        ushort destPort,
        bool bothways,
        PcapPacketHandler? packetHandler,
        object? userData) {
        ReadOnlySpan<byte> packetSpan = packet;
        int packetLength = packet.Length;
        int offset = 0;
        ushort protocol;

        if (linkType == TKFX_DLT_EN10MB) {
            if (packetLength < 14)
                return 0;

            protocol = ReadBe16(packetSpan.Slice(12, 2));
            offset = 14;
            while (protocol is 0x8100 or 0x88A8) {
                if (packetLength < offset + 4)
                    return 0;

                protocol = ReadBe16(packetSpan.Slice(offset + 2, 2));
                offset += 4;
            }
        } else if (linkType == TKFX_DLT_LINUX_SLL) {
            if (packetLength < 16)
                return 0;

            protocol = ReadBe16(packetSpan.Slice(14, 2));
            offset = 16;
        } else if (linkType == TKFX_DLT_NULL) {
            if (packetLength < 4)
                return 0;

            uint family = BitConverter.IsLittleEndian
                ? BinaryPrimitives.ReadUInt32LittleEndian(packetSpan[..4])
                : BinaryPrimitives.ReadUInt32BigEndian(packetSpan[..4]);
            offset = 4;

            if (family is 2U or 0x02000000U) {
                protocol = 0x0800;
            } else if (family is 23U or 24U or 28U or
                       0x17000000U or 0x18000000U or 0x1C000000U) {
                protocol = 0x86DD;
            } else {
                return 0;
            }
        } else {
            return -1;
        }

        int sourceAddressOffset;
        int destinationAddressOffset;
        int addressLength;
        int udpOffset;

        if (protocol == 0x0800) {
            if (packetLength < offset + 20 || (packetSpan[offset] >> 4) != 4)
                return 0;

            int ipHeaderLength = (packetSpan[offset] & 0x0F) * 4;
            if (ipHeaderLength < 20 || packetLength < offset + ipHeaderLength + 8)
                return 0;
            if (packetSpan[offset + 9] != 17)
                return 0;

            sourceAddressOffset = offset + 12;
            destinationAddressOffset = offset + 16;
            addressLength = 4;
            udpOffset = offset + ipHeaderLength;
        } else if (protocol == 0x86DD) {
            if (packetLength < offset + 40 || (packetSpan[offset] >> 4) != 6)
                return 0;

            sourceAddressOffset = offset + 8;
            destinationAddressOffset = offset + 24;
            addressLength = 16;
            byte nextHeader = packetSpan[offset + 6];
            int cursor = offset + 40;

            while (nextHeader is 0 or 43 or 60) {
                if (packetLength < cursor + 8)
                    return 0;

                int extensionLength = (packetSpan[cursor + 1] + 1) * 8;
                nextHeader = packetSpan[cursor];
                if (packetLength < cursor + extensionLength)
                    return 0;

                cursor += extensionLength;
            }

            if (nextHeader == 44) {
                if (packetLength < cursor + 8)
                    return 0;

                nextHeader = packetSpan[cursor];
                cursor += 8;
            }

            if (nextHeader != 17 || packetLength < cursor + 8)
                return 0;

            udpOffset = cursor;
        } else {
            return 0;
        }

        ushort sourcePort = ReadBe16(packetSpan.Slice(udpOffset, 2));
        ushort destinationPort = ReadBe16(packetSpan.Slice(udpOffset + 2, 2));
        int udpLength = ReadBe16(packetSpan.Slice(udpOffset + 4, 2));
        if (udpLength < 8)
            return 0;
        if (udpOffset + udpLength > packetLength)
            udpLength = packetLength - udpOffset;

        bool forward;
        if (AddressMatches(
                srcAddr,
                packetSpan.Slice(sourceAddressOffset, addressLength),
                addressLength) &&
            (srcPort == 0 || srcPort == sourcePort) &&
            AddressMatches(
                destAddr,
                packetSpan.Slice(destinationAddressOffset, addressLength),
                addressLength) &&
            (destPort == 0 || destPort == destinationPort)) {
            forward = true;
        } else if (bothways &&
                   AddressMatches(
                       destAddr,
                       packetSpan.Slice(sourceAddressOffset, addressLength),
                       addressLength) &&
                   (destPort == 0 || destPort == sourcePort) &&
                   AddressMatches(
                       srcAddr,
                       packetSpan.Slice(destinationAddressOffset, addressLength),
                       addressLength) &&
                   (srcPort == 0 || srcPort == destinationPort)) {
            forward = false;
        } else {
            return 0;
        }

        packetHandler?.Invoke(
            userData,
            new ReadOnlyMemory<byte>(packet, udpOffset + 8, udpLength - 8),
            udpLength - 8,
            forward);
        return 1;
    }

    public static int pcap_scan_pkts(
        string file,
        byte[] srcAddr,
        ushort srcPort,
        byte[] destAddr,
        ushort destPort,
        bool bothways,
        PcapTimingUpdateHandler? timingUpdateHandler,
        PcapPacketHandler? packetHandler,
        object? userData) {
        ArgumentNullException.ThrowIfNull(srcAddr);
        ArgumentNullException.ThrowIfNull(destAddr);

        FileStream input;
        try {
            input = File.OpenRead(file);
        } catch (Exception exception) when (
            exception is ArgumentException or
            IOException or
            UnauthorizedAccessException or
            NotSupportedException) {
            Console.Error.WriteLine($"Cannot open PCAP file '{file ?? "(null)"}'");
            return -1;
        }

        using (input) {
            byte[] globalHeader = new byte[24];
            if (!ReadExact(input, globalHeader)) {
                Console.Error.WriteLine($"Invalid PCAP global header in '{file}'");
                return -1;
            }

            bool swap;
            bool nanosecondTimestamps;
            if (globalHeader.AsSpan(0, 4).SequenceEqual(new byte[] { 0xD4, 0xC3, 0xB2, 0xA1 })) {
                swap = false;
                nanosecondTimestamps = false;
            } else if (globalHeader.AsSpan(0, 4).SequenceEqual(new byte[] { 0xA1, 0xB2, 0xC3, 0xD4 })) {
                swap = true;
                nanosecondTimestamps = false;
            } else if (globalHeader.AsSpan(0, 4).SequenceEqual(new byte[] { 0x4D, 0x3C, 0xB2, 0xA1 })) {
                swap = false;
                nanosecondTimestamps = true;
            } else if (globalHeader.AsSpan(0, 4).SequenceEqual(new byte[] { 0xA1, 0xB2, 0x3C, 0x4D })) {
                swap = true;
                nanosecondTimestamps = true;
            } else {
                Console.Error.WriteLine($"Unsupported PCAP magic in '{file}'");
                return -1;
            }

            ushort major = ReadU16(globalHeader.AsSpan(4, 2), swap);
            ushort minor = ReadU16(globalHeader.AsSpan(6, 2), swap);
            uint linkType = ReadU32(globalHeader.AsSpan(20, 4), swap);
            Console.WriteLine($"PCAP file version {major}.{minor}");

            if (linkType != TKFX_DLT_EN10MB &&
                linkType != TKFX_DLT_LINUX_SLL &&
                linkType != TKFX_DLT_NULL) {
                Console.Error.WriteLine($"Unsupported PCAP data link type {linkType}");
                return -1;
            }

            int accepted = 0;
            byte[] packetHeader = new byte[16];
            while (true) {
                int firstByte = input.ReadByte();
                if (firstByte < 0)
                    break;

                packetHeader[0] = unchecked((byte)firstByte);
                if (!ReadExact(input, packetHeader.AsSpan(1))) {
                    Console.Error.WriteLine($"Truncated PCAP packet header in '{file}'");
                    return -1;
                }

                uint timestampSeconds = ReadU32(packetHeader.AsSpan(0, 4), swap);
                uint timestampFraction = ReadU32(packetHeader.AsSpan(4, 4), swap);
                uint capturedLength = ReadU32(packetHeader.AsSpan(8, 4), swap);
                uint originalLength = ReadU32(packetHeader.AsSpan(12, 4), swap);

                if (capturedLength > MaximumCapturedPacketLength) {
                    Console.Error.WriteLine(
                        $"Unreasonable PCAP packet length {capturedLength} in '{file}'");
                    return -1;
                }

                byte[] packet = new byte[checked((int)capturedLength)];
                if (capturedLength != 0 && !ReadExact(input, packet)) {
                    Console.Error.WriteLine($"Truncated PCAP packet data in '{file}'");
                    return -1;
                }

                Timeval timestamp = new() {
                    tv_sec = timestampSeconds,
                    tv_usec = nanosecondTimestamps
                        ? timestampFraction / 1000U
                        : timestampFraction
                };
                timingUpdateHandler?.Invoke(userData, ref timestamp);

                if (capturedLength != originalLength) {
                    Console.Error.WriteLine(
                        $"Truncated captured packet - original={originalLength} captured={capturedLength}");
                    return -1;
                }

                int decoded = DecodeUdpPacket(
                    packet,
                    linkType,
                    srcAddr,
                    srcPort,
                    destAddr,
                    destPort,
                    bothways,
                    packetHandler,
                    userData);
                if (decoded < 0)
                    return -1;

                accepted += decoded;
            }

            Console.Error.WriteLine(
                $"In pcap {file} there were {accepted} accepted packets");
            return 0;
        }
    }

    private static bool ReadExact(Stream stream, byte[] destination) {
        return ReadExact(stream, destination.AsSpan());
    }

    private static bool ReadExact(Stream stream, Span<byte> destination) {
        int total = 0;
        while (total < destination.Length) {
            int read = stream.Read(destination[total..]);
            if (read == 0)
                return false;
            total += read;
        }

        return true;
    }
}
