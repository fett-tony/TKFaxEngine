/*
 * Fresh managed C# port of sprt_decode.c.
 * Source logic: TKFaxEngine/spanDSP SPRT PCAP decoder.
 */

#nullable enable
using SprtState = TKFaxEngine.SprtState;

using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using TKFaxEngine.Modem.V150;

namespace TKFaxEngineTest.Modem.Test;

public static class SprtDecodeTest {
    private const string InputFileName = "Test-Files/sprt.pcap";
    private const string OutputWaveFileName = "sprt_decode.wav";
    private const int SamplesPerChunk = 160;

    private static long _previousForwardTime;
    private static long _previousReverseTime;
    private static long _currentTime;
    private static SprtState? _sprt;
    private static V1501State? _v1501;
    private static int _sprtPayloadType = 120;

    public static int Run(string[] args) {
        ArgumentNullException.ThrowIfNull(args);

        string inputFileName = InputFileName;
        IPAddress? sourceAddress = null;
        ushort sourcePort = 0;
        IPAddress? destinationAddress = null;
        ushort destinationPort = 0;
        _sprtPayloadType = 120;
        _previousForwardTime = 0;
        _previousReverseTime = 0;
        _currentTime = 0;
        _sprt?.Dispose();
        _sprt = null;
        _v1501?.Dispose();
        _v1501 = null;

        for (int index = 0; index < args.Length; index++) {
            string option = args[index];
            switch (option) {
                case "-D":
                    if (!TryGetOptionValue(args, ref index, out string? destinationText)
                        || !IPAddress.TryParse(destinationText, out destinationAddress)
                        || destinationAddress.AddressFamily != global::System.Net.Sockets.AddressFamily.InterNetwork) {
                        Console.Error.WriteLine("Bad destination address");
                        return -1;
                    }
                    break;

                case "-d":
                    if (!TryGetOptionValue(args, ref index, out string? destinationPortText)
                        || !ushort.TryParse(destinationPortText, NumberStyles.Integer, CultureInfo.InvariantCulture, out destinationPort)) {
                        return 2;
                    }
                    break;

                case "-i":
                    if (!TryGetOptionValue(args, ref index, out string? inputText))
                        return 2;
                    inputFileName = inputText;
                    break;

                case "-S":
                    if (!TryGetOptionValue(args, ref index, out string? sourceText)
                        || !IPAddress.TryParse(sourceText, out sourceAddress)
                        || sourceAddress.AddressFamily != global::System.Net.Sockets.AddressFamily.InterNetwork) {
                        Console.Error.WriteLine("Bad destination address");
                        return -1;
                    }
                    break;

                case "-p":
                    if (!TryGetOptionValue(args, ref index, out string? payloadTypeText)
                        || !int.TryParse(payloadTypeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out _sprtPayloadType)) {
                        return 2;
                    }
                    break;

                case "-s":
                    if (!TryGetOptionValue(args, ref index, out string? sourcePortText)
                        || !ushort.TryParse(sourcePortText, NumberStyles.Integer, CultureInfo.InvariantCulture, out sourcePort)) {
                        return 2;
                    }
                    break;

                default:
                    return 2;
            }
        }

        int scanResult = ScanPcapPackets(
            inputFileName,
            sourceAddress,
            sourcePort,
            destinationAddress,
            destinationPort,
            bothWays: true);

        if (scanResult != 0)
            return 2;

        // Match the native final time advance used to flush protocol activity.
        _currentTime += 60_000_000;
        TimingUpdate(_currentTime);
        return 0;
    }

    private static bool TryGetOptionValue(string[] args, ref int index, out string value) {
        if (index + 1 >= args.Length) {
            value = string.Empty;
            return false;
        }

        value = args[++index];
        return true;
    }

    private static ulong SprtTimerHandler(object? userData, ulong timeout) => 0;

    private static ulong V1501TimerHandler(object? userData, ulong timeout) => 0;

    private static int SprtTransmitPacketHandler(object? userData, ReadOnlySpan<byte> message) {
        Console.Write($"Response_packet {message.Length,5} >>> ");
        PrintHex(message, uppercase: true, leadingSpace: false);
        Console.WriteLine();
        return 0;
    }

    private static int SprtReceiveDeliveryHandler(
        object? userData,
        int channel,
        int sequenceNumber,
        ReadOnlySpan<byte> message) => 0;

    private static void SprtStatusHandler(object? userData, int status) {
    }

    private static int V1501StatusReportHandler(object? userData, V1501Status report) {
        Console.WriteLine("V.150.1 status report received");
        return 0;
    }

    private static int V1501OctetStreamHandler(object? userData, ReadOnlySpan<byte> message, int fill) {
        if (fill > 0)
            Console.WriteLine($"{fill} missing characters");

        Console.Write(">>>");
        PrintHex(message, uppercase: false, leadingSpace: true);
        Console.WriteLine("<<<");
        return 0;
    }

    private static void TimingUpdate(long timestampMicroseconds) {
        _currentTime = timestampMicroseconds;
        if (_previousForwardTime == 0)
            _previousForwardTime = _currentTime;
        if (_previousReverseTime == 0)
            _previousReverseTime = _currentTime;
    }

    private static int SprtLog(ReadOnlySpan<byte> packet) {
        int index = 0;
        if (packet.Length == 0)
            return 0;

        int messageId = packet[index++];
        Console.Write($"MSG_ID={V1501State.MessageIdToString(messageId)},");

        bool badLength = messageId switch {
            (int)V1501MessageId.Null => packet.Length != 1,
            (int)V1501MessageId.Init => packet.Length != 3,
            (int)V1501MessageId.XidExchange => packet.Length != 19,
            (int)V1501MessageId.JmInfo => packet.Length < 3,
            (int)V1501MessageId.StartJm => packet.Length != 1,
            (int)V1501MessageId.Connect => packet.Length < 9 || packet.Length > 19,
            (int)V1501MessageId.Break => packet.Length != 3,
            (int)V1501MessageId.BreakAck => packet.Length != 1,
            (int)V1501MessageId.MrEvent => packet.Length != 10,
            (int)V1501MessageId.Cleardown => packet.Length != 4,
            (int)V1501MessageId.ProfileExchange => packet.Length < 9 || packet.Length > 19,
            (int)V1501MessageId.IRawOctet => packet.Length < 2,
            (int)V1501MessageId.IRawBit => packet.Length < 2,
            (int)V1501MessageId.IOctet => packet.Length < 2,
            (int)V1501MessageId.ICharStatic => packet.Length < 2,
            (int)V1501MessageId.ICharDynamic => packet.Length < 2,
            (int)V1501MessageId.IFrame => packet.Length < 2,
            (int)V1501MessageId.IOctetCharacterSequence => packet.Length < 3,
            (int)V1501MessageId.ICharStaticCharacterSequence => packet.Length < 4,
            (int)V1501MessageId.ICharDynamicCharacterSequence => packet.Length != 4,
            _ => false
        };

        bool known = Enum.IsDefined(typeof(V1501MessageId), messageId);
        if (!known)
            Console.Write("Unknown MSGID ");
        else if (badLength)
            Console.Write("Bad length ");

        for (; index < packet.Length; index++)
            Console.Write($" {packet[index]:X2}");

        return 0;
    }

    private static int ProcessPacket(ReadOnlySpan<byte> packet, bool forward) {
        EnsureStates();

        long difference;
        if (forward) {
            difference = _currentTime - _previousForwardTime;
            _previousForwardTime = _currentTime;
        } else {
            difference = _currentTime - _previousReverseTime;
            _previousReverseTime = _currentTime;
        }

        float differenceSeconds = difference / 1_000_000.0f;
        if (packet.Length < 6)
            return 0;

        if ((packet[0] & 0x80) == 0) {
            int payloadType = packet[1] & 0x7F;
            if (_sprtPayloadType == 0 || payloadType == _sprtPayloadType) {
                int numberOfAcknowledgements = (packet[4] >> 6) & 0x03;
                Console.Write($"{(forward ? "FWD" : "REV")} {differenceSeconds:F6}s {packet.Length,3} >>> ");
                Console.Write(
                    $"SSID={packet[0] & 0x7F}, PT={payloadType}, TC={(packet[2] >> 6) & 0x03}, " +
                    $"SQN={((packet[2] << 8) | packet[3]) & 0x3FFF}, " +
                    $"BSN={((packet[4] << 8) | packet[5]) & 0x3FFF}");

                int index = 6;
                if (numberOfAcknowledgements > 0) {
                    Console.Write(", (ACKS ");
                    for (int acknowledgement = 0; acknowledgement < numberOfAcknowledgements; acknowledgement++) {
                        if (acknowledgement > 0)
                            Console.Write(", ");

                        if (index + 1 >= packet.Length) {
                            Console.Write("truncated");
                            index = packet.Length;
                            break;
                        }

                        Console.Write(
                            $"TC={(packet[index] >> 6) & 0x03} " +
                            $"SQN={((packet[index] << 8) | packet[index + 1]) & 0x3FFF}");
                        index += 2;
                    }
                    Console.Write(")");
                }

                if (packet.Length > index) {
                    Console.Write(" - ");
                    SprtLog(packet[index..]);
                }

                Console.WriteLine();
            }
        }

        // The native source deliberately keeps protocol packet injection under #if 0.
        return 0;
    }

    private static void EnsureStates() {
        _sprt ??= new SprtState(
            0,
            120,
            120,
            null,
            SprtTransmitPacketHandler,
            null,
            SprtReceiveDeliveryHandler,
            null,
            SprtTimerHandler,
            null,
            SprtStatusHandler,
            null);

        _v1501 ??= new V1501State(
            SprtTransmitPacketHandler,
            null,
            120,
            120,
            null,
            null,
            V1501TimerHandler,
            null,
            V1501OctetStreamHandler,
            null,
            V1501StatusReportHandler,
            null,
            null,
            null);
    }

    private static int ScanPcapPackets(
        string fileName,
        IPAddress? sourceAddress,
        ushort sourcePort,
        IPAddress? destinationAddress,
        ushort destinationPort,
        bool bothWays) {
        try {
            using FileStream stream = File.OpenRead(fileName);
            Span<byte> globalHeader = stackalloc byte[24];
            if (!ReadExactly(stream, globalHeader)) {
                Console.Error.WriteLine("Can't open PCAP file: truncated global header");
                return -1;
            }

            PcapByteOrder byteOrder;
            bool nanosecondTimestamps;
            ReadOnlySpan<byte> magic = globalHeader[..4];
            if (magic.SequenceEqual(new byte[] { 0xD4, 0xC3, 0xB2, 0xA1 })) {
                byteOrder = PcapByteOrder.LittleEndian;
                nanosecondTimestamps = false;
            } else if (magic.SequenceEqual(new byte[] { 0xA1, 0xB2, 0xC3, 0xD4 })) {
                byteOrder = PcapByteOrder.BigEndian;
                nanosecondTimestamps = false;
            } else if (magic.SequenceEqual(new byte[] { 0x4D, 0x3C, 0xB2, 0xA1 })) {
                byteOrder = PcapByteOrder.LittleEndian;
                nanosecondTimestamps = true;
            } else if (magic.SequenceEqual(new byte[] { 0xA1, 0xB2, 0x3C, 0x4D })) {
                byteOrder = PcapByteOrder.BigEndian;
                nanosecondTimestamps = true;
            } else {
                Console.Error.WriteLine("Can't open PCAP file: unsupported magic value");
                return -1;
            }

            ushort major = ReadUInt16(globalHeader[4..6], byteOrder);
            ushort minor = ReadUInt16(globalHeader[6..8], byteOrder);
            uint dataLinkType = ReadUInt32(globalHeader[20..24], byteOrder);
            Console.WriteLine($"PCAP file version {major}.{minor}");

            switch (dataLinkType) {
                case 1:
                    Console.WriteLine("Datalink type ethernet");
                    break;
                case 113:
                    Console.WriteLine("Datalink type cooked Linux socket");
                    break;
                case 0:
                    Console.WriteLine("Datalink type NULL");
                    break;
                default:
                    Console.Error.WriteLine($"Unsupported data link type {dataLinkType}");
                    return -1;
            }

            int acceptedPackets = 0;
            Span<byte> packetHeader = stackalloc byte[16];
            while (true) {
                int headerBytes = stream.Read(packetHeader);
                if (headerBytes == 0)
                    break;
                while (headerBytes < packetHeader.Length) {
                    int read = stream.Read(packetHeader[headerBytes..]);
                    if (read == 0) {
                        Console.Error.WriteLine("Truncated PCAP packet header");
                        return -1;
                    }
                    headerBytes += read;
                }

                uint seconds = ReadUInt32(packetHeader[0..4], byteOrder);
                uint fraction = ReadUInt32(packetHeader[4..8], byteOrder);
                uint capturedLength = ReadUInt32(packetHeader[8..12], byteOrder);
                uint originalLength = ReadUInt32(packetHeader[12..16], byteOrder);

                if (capturedLength > int.MaxValue) {
                    Console.Error.WriteLine("PCAP packet is too large");
                    return -1;
                }

                byte[] frame = new byte[(int)capturedLength];
                if (!ReadExactly(stream, frame)) {
                    Console.Error.WriteLine("Truncated PCAP packet data");
                    return -1;
                }

                if (originalLength != capturedLength) {
                    Console.Error.WriteLine(
                        $"Truncated packet - total len = {originalLength}, captured len = {capturedLength}");
                    return 2;
                }

                if (!TryExtractUdpPacket(frame, dataLinkType, out UdpPacket packet))
                    continue;

                long timestampMicroseconds = checked((long)seconds * 1_000_000L)
                    + (nanosecondTimestamps ? fraction / 1000L : fraction);
                TimingUpdate(timestampMicroseconds);

                bool forward;
                if (Matches(
                    packet.SourceAddress,
                    packet.SourcePort,
                    packet.DestinationAddress,
                    packet.DestinationPort,
                    sourceAddress,
                    sourcePort,
                    destinationAddress,
                    destinationPort)) {
                    forward = true;
                } else if (bothWays && Matches(
                    packet.SourceAddress,
                    packet.SourcePort,
                    packet.DestinationAddress,
                    packet.DestinationPort,
                    destinationAddress,
                    destinationPort,
                    sourceAddress,
                    sourcePort)) {
                    forward = false;
                } else {
                    continue;
                }

                ProcessPacket(packet.Payload.Span, forward);
                acceptedPackets++;
            }

            Console.Error.WriteLine($"In pcap {fileName} there were {acceptedPackets} accepted packets");
            return 0;
        } catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException) {
            Console.Error.WriteLine($"Can't open PCAP file: {exception.Message}");
            return -1;
        }
    }

    private static bool Matches(
        IPAddress actualSourceAddress,
        ushort actualSourcePort,
        IPAddress actualDestinationAddress,
        ushort actualDestinationPort,
        IPAddress? requiredSourceAddress,
        ushort requiredSourcePort,
        IPAddress? requiredDestinationAddress,
        ushort requiredDestinationPort) {
        return (requiredSourceAddress is null || requiredSourceAddress.Equals(actualSourceAddress))
            && (requiredSourcePort == 0 || requiredSourcePort == actualSourcePort)
            && (requiredDestinationAddress is null || requiredDestinationAddress.Equals(actualDestinationAddress))
            && (requiredDestinationPort == 0 || requiredDestinationPort == actualDestinationPort);
    }

    private static bool TryExtractUdpPacket(
        ReadOnlyMemory<byte> frame,
        uint dataLinkType,
        out UdpPacket packet) {
        ReadOnlySpan<byte> bytes = frame.Span;
        int networkOffset;
        ushort etherType = 0;

        switch (dataLinkType) {
            case 1: // DLT_EN10MB
                if (bytes.Length < 14) {
                    packet = default;
                    return false;
                }
                etherType = BinaryPrimitives.ReadUInt16BigEndian(bytes[12..14]);
                networkOffset = 14;
                if (etherType == 0x8100) {
                    if (bytes.Length < 18) {
                        packet = default;
                        return false;
                    }
                    etherType = BinaryPrimitives.ReadUInt16BigEndian(bytes[16..18]);
                    networkOffset = 18;
                }
                if (etherType is not 0x0800 and not 0x86DD) {
                    packet = default;
                    return false;
                }
                break;

            case 113: // DLT_LINUX_SLL
                if (bytes.Length < 16) {
                    packet = default;
                    return false;
                }
                etherType = BinaryPrimitives.ReadUInt16BigEndian(bytes[14..16]);
                networkOffset = 16;
                if (etherType is not 0x0800 and not 0x86DD) {
                    packet = default;
                    return false;
                }
                break;

            case 0: // DLT_NULL
                if (bytes.Length < 4) {
                    packet = default;
                    return false;
                }
                networkOffset = 4;
                break;

            default:
                packet = default;
                return false;
        }

        if (networkOffset >= bytes.Length) {
            packet = default;
            return false;
        }

        int version = bytes[networkOffset] >> 4;
        int udpOffset;
        IPAddress sourceAddress;
        IPAddress destinationAddress;

        if (version == 4) {
            if (bytes.Length < networkOffset + 20) {
                packet = default;
                return false;
            }

            int headerLength = (bytes[networkOffset] & 0x0F) * 4;
            if (headerLength < 20 || bytes.Length < networkOffset + headerLength || bytes[networkOffset + 9] != 17) {
                packet = default;
                return false;
            }

            sourceAddress = new IPAddress(bytes.Slice(networkOffset + 12, 4));
            destinationAddress = new IPAddress(bytes.Slice(networkOffset + 16, 4));
            udpOffset = networkOffset + headerLength;
        } else if (version == 6) {
            if (bytes.Length < networkOffset + 40 || bytes[networkOffset + 6] != 17) {
                packet = default;
                return false;
            }

            sourceAddress = new IPAddress(bytes.Slice(networkOffset + 8, 16));
            destinationAddress = new IPAddress(bytes.Slice(networkOffset + 24, 16));
            udpOffset = networkOffset + 40;
        } else {
            packet = default;
            return false;
        }

        if (bytes.Length < udpOffset + 8) {
            packet = default;
            return false;
        }

        ushort sourcePort = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(udpOffset, 2));
        ushort destinationPort = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(udpOffset + 2, 2));
        ushort udpLength = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(udpOffset + 4, 2));
        if (udpLength < 8 || udpOffset + udpLength > bytes.Length) {
            packet = default;
            return false;
        }

        packet = new UdpPacket(
            sourceAddress,
            sourcePort,
            destinationAddress,
            destinationPort,
            frame.Slice(udpOffset + 8, udpLength - 8));
        return true;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, PcapByteOrder byteOrder) =>
        byteOrder == PcapByteOrder.LittleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(bytes)
            : BinaryPrimitives.ReadUInt16BigEndian(bytes);

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, PcapByteOrder byteOrder) =>
        byteOrder == PcapByteOrder.LittleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(bytes)
            : BinaryPrimitives.ReadUInt32BigEndian(bytes);

    private static bool ReadExactly(Stream stream, Span<byte> destination) {
        int total = 0;
        while (total < destination.Length) {
            int read = stream.Read(destination[total..]);
            if (read == 0)
                return false;
            total += read;
        }
        return true;
    }

    private static void PrintHex(ReadOnlySpan<byte> bytes, bool uppercase, bool leadingSpace) {
        string format = uppercase ? "X2" : "x2";
        for (int index = 0; index < bytes.Length; index++) {
            if (leadingSpace || index > 0)
                Console.Write(' ');
            Console.Write(bytes[index].ToString(format, CultureInfo.InvariantCulture));
        }
    }

    private enum PcapByteOrder {
        LittleEndian,
        BigEndian
    }

    private readonly record struct UdpPacket(
        IPAddress SourceAddress,
        ushort SourcePort,
        IPAddress DestinationAddress,
        ushort DestinationPort,
        ReadOnlyMemory<byte> Payload);
}
