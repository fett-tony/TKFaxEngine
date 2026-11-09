/*
 * TKFaxEngine - managed C# port of t38_decode.c.
 *
 * The PCAP, UDP, UDPTL redundancy/FEC, T.38 terminal and T.38 gateway paths
 * follow the spanDSP test utility. No native interop is used.
 */

#nullable enable
using TKFaxEngine;

using System.Buffers.Binary;
using System.Net;
using System.Text;
using TKFaxEngine.Daten.T30;
using TKFaxEngine.Daten.T38;
using static TKFaxEngine.Daten.T30.T30;
using static TKFaxEngine.Daten.T30.T30Api;
using static TKFaxEngine.Daten.T30.T30Logging;
using static TKFaxEngine.Daten.T38.T38Core;
using static TKFaxEngine.Daten.T38.T38Gateway;
using static TKFaxEngine.Daten.T38.T38Terminal;
using static TKFaxEngine.FaxApi;
using static TKFaxEngine.FaxImage.t4_rx;
using CoreSupportedModems = TKFaxEngine.Daten.T30.T30SupportedModems;

namespace TKFaxEngineTest.Fax.Test;

public static class t38_decode {
    private const string InputFileName = "Test-Files/T38.pcap";
    private const string InputTiffFileName = "Test-Files/itu/fax/itutests.tif";
    private const string OutputTiffFileName = "t38pcap.tif";
    private const string OutputWaveFileName = "t38_decode2.wav";
    private const int SamplesPerChunk = 160;

    private static T38CoreState? _t38Core;
    private static T38TerminalState? _terminal;
    private static T38GatewayState? _gateway;
    private static FaxState? _fax;
    private static PcapTimestamp _now;
    private static WaveFileWriter? _wave;
    private static bool _logAudio;
    private static bool _useTransmitOnIdle = true;
    private static bool _done;
    private static bool _started;
    private static long _current;
    private static UdptlReceiver? _udptl;

    public sealed class T38DecodeExitException : Exception {
        public T38DecodeExitException(int exitCode)
            : base($"t38_decode exited with code {exitCode}.") => ExitCode = exitCode;

        public int ExitCode { get; }
    }

    public static int Main(string[] args) {
        try {
            return tkfx_t38_decode_body_main(args);
        } catch (T38DecodeExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_t38_decode_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        DecodeOptions options = DecodeOptions.Parse(testArguments, testArguments.Length);
        Console.WriteLine("Using T.38 version {0}", options.T38Version);

        _done = false;
        _started = false;
        _current = 0;
        _udptl = new UdptlReceiver(IfpHandler);

        if (options.TerminalOperation)
            RunTerminal(options);
        else
            RunGateway(options);

        return 0;
    }

    private static void RunTerminal(DecodeOptions options) {
        _terminal = t38_terminal_init(null, options.Caller, TxPacketHandler, null)
            ?? throw new InvalidOperationException("Cannot start the T.38 terminal.");
        _t38Core = t38_terminal_get_t38_core_state(_terminal);

        t38_set_t38_version(_t38Core, options.T38Version);
        t38_terminal_set_config(_terminal, options.Configuration);
        t38_terminal_set_tep_mode(_terminal, options.UseTep);
        t38_terminal_set_fill_bit_removal(_terminal, options.FillRemoval);
        ConfigureLogging(t38_terminal_get_logging_state(_terminal), "T.38");
        ConfigureLogging(t38_core_get_logging_state(_t38Core), "T.38");
        ConfigureT30(t38_terminal_get_t30_state(_terminal), options, "11111111");

        if (PcapScanner.Scan(
                options.InputFile,
                options.SourceAddress,
                options.SourcePort,
                options.DestinationAddress,
                options.DestinationPort,
                false,
                TerminalTimingUpdate,
                ProcessPacket) != 0)
            Exit(2);

        _now = _now with { Seconds = _now.Seconds + 60 };
        TerminalTimingUpdate(_now);
        t38_terminal_release(_terminal);
    }

    private static void RunGateway(DecodeOptions options) {
        if (_logAudio = options.LogAudio)
            _wave = new WaveFileWriter(OutputWaveFileName, 8_000, 2);

        _gateway = t38_gateway_init(null, TxPacketHandler, null)
            ?? throw new InvalidOperationException("Cannot start the T.38 gateway.");
        _t38Core = t38_gateway_get_t38_core_state(_gateway);
        t38_gateway_set_transmit_on_idle(_gateway, _useTransmitOnIdle);
        t38_set_t38_version(_t38Core, options.T38Version);
        t38_gateway_set_ecm_capability(_gateway, options.UseEcm);
        t38_gateway_set_tep_mode(_gateway, options.UseTep);
        ConfigureLogging(t38_gateway_get_logging_state(_gateway), "T.38");
        ConfigureLogging(t38_core_get_logging_state(_t38Core), "T.38");

        _fax = fax_init(null, options.Caller);
        fax_set_transmit_on_idle(_fax, _useTransmitOnIdle ? 1 : 0);
        fax_set_tep_mode(_fax, options.UseTep ? 1 : 0);
        ConfigureT30(fax_get_t30_state(_fax), options, "22222222");

        if (PcapScanner.Scan(
                options.InputFile,
                options.SourceAddress,
                options.SourcePort,
                options.DestinationAddress,
                options.DestinationPort,
                false,
                GatewayTimingUpdate,
                ProcessPacket) != 0)
            Exit(2);

        _now = _now with { Seconds = _now.Seconds + 60 };
        GatewayTimingUpdate(_now);

        fax_free(_fax);
        t38_gateway_release(_gateway);
        _wave?.Dispose();
        _wave = null;
    }

    private static void ConfigureT30(T30State state, DecodeOptions options, string ident) {
        state.Logging.Sink = (level, text) => Console.WriteLine("T.30 [{0}] {1}", level, text);
        t30_set_supported_modems(state, options.SupportedModems);
        t30_set_tx_ident(state, ident);
        t30_set_tx_nsf(state, new byte[] { 0x50, 0x00, 0x00, 0x00, (byte)'T', (byte)'K', (byte)'F', (byte)'a', (byte)'x', (byte)'E', (byte)'n', (byte)'g' });

        if (options.Caller)
            t30_set_tx_file(state, options.InputTiffFile, -1, -1);
        else
            t30_set_rx_file(state, OutputTiffFileName, -1);

        t30_set_phase_b_handler(state, PhaseBHandler, state);
        t30_set_phase_d_handler(state, PhaseDHandler, state);
        t30_set_phase_e_handler(state, PhaseEHandler, state);
        t30_set_ecm_capability(state, options.UseEcm);

        int compressions =
            T4_COMPRESSION_T4_1D |
            T4_COMPRESSION_T4_2D |
            T4_COMPRESSION_T6 |
            T4_COMPRESSION_T85 |
            T4_COMPRESSION_T85_L0 |
            T4_COMPRESSION_T42_T81 |
            T4_COMPRESSION_COLOUR;
        t30_set_supported_compressions(state, compressions);

        int bilevel =
            T4_RESOLUTION_R8_STANDARD |
            T4_RESOLUTION_R8_FINE |
            T4_RESOLUTION_R8_SUPERFINE |
            T4_RESOLUTION_R16_SUPERFINE |
            T4_RESOLUTION_200_100 |
            T4_RESOLUTION_200_200 |
            T4_RESOLUTION_200_400 |
            T4_RESOLUTION_300_300 |
            T4_RESOLUTION_300_600 |
            T4_RESOLUTION_400_400 |
            T4_RESOLUTION_400_800 |
            T4_RESOLUTION_600_600 |
            T4_RESOLUTION_600_1200 |
            T4_RESOLUTION_1200_1200;
        t30_set_supported_bilevel_resolutions(state, bilevel);

        int colour =
            T4_RESOLUTION_100_100 |
            T4_RESOLUTION_200_200 |
            T4_RESOLUTION_300_300 |
            T4_RESOLUTION_400_400 |
            T4_RESOLUTION_600_600 |
            T4_RESOLUTION_1200_1200;
        t30_set_supported_colour_resolutions(state, colour);
    }

    private static int PhaseBHandler(object? userData, int result) {
        Console.WriteLine("A: Phase B handler on channel A - (0x{0:X}) {1}", result, t30_frametype((byte)result));
        LogT30Snapshot(userData as T30State, "A: Phase B");
        return (int)T30Error.Ok;
    }

    private static int PhaseDHandler(object? userData, int result) {
        Console.WriteLine("A: Phase D handler on channel A - (0x{0:X}) {1}", result, t30_frametype((byte)result));
        LogT30Snapshot(userData as T30State, "A: Phase D");
        return (int)T30Error.Ok;
    }

    private static void PhaseEHandler(object? userData, int result) {
        Console.WriteLine("A: Phase E handler on channel A - ({0}) {1}", result, t30_completion_code_to_str(result));
        LogT30Snapshot(userData as T30State, "A: Phase E");
    }

    private static void LogT30Snapshot(T30State? state, string tag) {
        if (state is null)
            return;
        T30Statistics stats = new();
        t30_get_transfer_statistics(state, stats);
        Console.WriteLine(
            "{0}: rate={1}, ECM={2}, txPages={3}, rxPages={4}, imageBytes={5}, badRows={6}",
            tag,
            stats.BitRate,
            stats.ErrorCorrectingMode,
            stats.PagesTransmitted,
            stats.PagesReceived,
            stats.ImageSize,
            stats.BadRows);
    }

    private static int TxPacketHandler(T38CoreState state, object? userData, ReadOnlyMemory<byte> packet, int count) {
        _ = state;
        _ = userData;
        _ = packet;
        _ = count;
        return 0;
    }

    private static int TerminalTimingUpdate(PcapTimestamp timestamp) {
        _now = timestamp;
        long when = checked(timestamp.Seconds * 1_000_000_000L + timestamp.Fraction);
        if (_current == 0) {
            if (_started)
                _current = when;
            return 0;
        }

        long difference = when - _current;
        int samples = checked((int)(difference / 125_000L));
        while (samples > 0) {
            int partial = Math.Min(samples, SamplesPerChunk);
            if (_terminal is not null && t38_terminal_send_timeout(_terminal, partial) != 0)
                break;
            _current = when;
            samples -= partial;
        }
        return 0;
    }

    private static int GatewayTimingUpdate(PcapTimestamp timestamp) {
        _now = timestamp;
        long when = checked(timestamp.Seconds * 1_000_000L + timestamp.Fraction);
        if (_current == 0) {
            if (_started)
                _current = when;
            return 0;
        }

        long difference = when - _current;
        int samples = checked((int)(difference / 125L));
        short[] gatewayAudio = new short[SamplesPerChunk];
        short[] faxAudio = new short[SamplesPerChunk];
        short[] stereo = new short[SamplesPerChunk * 2];

        while (samples > 0) {
            int partial = Math.Min(samples, SamplesPerChunk);
            Array.Clear(stereo);

            int faxLength = _fax?.Transmit(faxAudio, partial) ?? 0;
            if (!_useTransmitOnIdle && faxLength < partial) {
                Array.Clear(faxAudio, faxLength, partial - faxLength);
                faxLength = partial;
            }
            if (_logAudio)
                for (int index = 0; index < faxLength; index++)
                    stereo[index * 2 + 1] = faxAudio[index];

            if (_gateway is null || t38_gateway_rx(_gateway, faxAudio, faxLength) != 0)
                break;

            int gatewayLength = t38_gateway_tx(_gateway, gatewayAudio, partial);
            if (!_useTransmitOnIdle && gatewayLength < partial) {
                Array.Clear(gatewayAudio, gatewayLength, partial - gatewayLength);
                gatewayLength = partial;
            }
            if (_logAudio)
                for (int index = 0; index < gatewayLength; index++)
                    stereo[index * 2] = gatewayAudio[index];

            if (_fax?.Receive(gatewayAudio, partial) != 0)
                break;
            _wave?.WriteFrames(stereo, partial);

            if (_done)
                break;
            _current = when;
            samples -= partial;
        }
        return 0;
    }

    private static int IfpHandler(ReadOnlyMemory<byte> message, int sequenceNumber) {
        _started = true;
        Console.Write("{0,5} >>> ", sequenceNumber);
        foreach (byte value in message.Span)
            Console.Write("{0:X2} ", value);
        Console.WriteLine();

        if (_t38Core is null)
            return -1;
        byte[] ifpPacket = message.ToArray();
        return t38_core_rx_ifp_packet(
            _t38Core,
            ifpPacket,
            ifpPacket.Length,
            unchecked((ushort)sequenceNumber));
    }

    private static int ProcessPacket(ReadOnlyMemory<byte> packet, bool forward) {
        if (forward)
            return _udptl?.Receive(packet.Span) ?? -1;
        return 0;
    }

    private static void ConfigureLogging(T38Log log, string tag) {
        log.Sink = (level, message) => Console.WriteLine("{0} [{1}] {2}", tag, level, message);
    }

    private static void Exit(int code) => throw new T38DecodeExitException(code);

    private sealed record DecodeOptions(
        bool Caller,
        bool UseEcm,
        int T38Version,
        int Configuration,
        string InputFile,
        string InputTiffFile,
        bool FillRemoval,
        bool UseTep,
        int SupportedModems,
        bool TerminalOperation,
        bool LogAudio,
        IPAddress? SourceAddress,
        ushort SourcePort,
        IPAddress? DestinationAddress,
        ushort DestinationPort) {
        public static DecodeOptions Parse(string[] arguments, int argumentCount) {
            bool caller = false;
            bool useEcm = false;
            int version = 0;
            int configuration = 0;
            string input = InputFileName;
            string tiff = InputTiffFileName;
            bool fillRemoval = false;
            bool useTep = false;
            int supported = (int)(CoreSupportedModems.V27Ter | CoreSupportedModems.V29 | CoreSupportedModems.V17);
            bool terminal = true;
            bool logAudio = false;
            IPAddress? sourceAddress = null;
            ushort sourcePort = 0;
            IPAddress? destinationAddress = null;
            ushort destinationPort = 0;

            for (int index = 0; index < argumentCount; index++) {
                string argument = arguments[index];
                string NextValue() {
                    if (++index >= argumentCount)
                        Exit(2);
                    return arguments[index];
                }

                switch (argument) {
                    case "-c": caller = true; break;
                    case "-D": destinationAddress = ParseAddress(NextValue(), "destination"); break;
                    case "-d": destinationPort = ParsePort(NextValue()); break;
                    case "-e": useEcm = true; break;
                    case "-F": fillRemoval = true; break;
                    case "-G": terminal = false; break;
                    case "-i": input = NextValue(); break;
                    case "-l": logAudio = true; break;
                    case "-m": supported = ParseInt(NextValue()); break;
                    case "-o": configuration = ParseInt(NextValue()); break;
                    case "-S": sourceAddress = ParseAddress(NextValue(), "source"); break;
                    case "-s": sourcePort = ParsePort(NextValue()); break;
                    case "-T": tiff = NextValue(); break;
                    case "-t": useTep = true; break;
                    case "-v": version = ParseInt(NextValue()); break;
                    default: Exit(2); break;
                }
            }

            return new DecodeOptions(
                caller, useEcm, version, configuration, input, tiff,
                fillRemoval, useTep, supported, terminal, logAudio,
                sourceAddress, sourcePort, destinationAddress, destinationPort);
        }

        private static int ParseInt(string value) {
            if (!int.TryParse(value, out int parsed))
                Exit(2);
            return parsed;
        }

        private static ushort ParsePort(string value) {
            if (!ushort.TryParse(value, out ushort parsed))
                Exit(2);
            return parsed;
        }

        private static IPAddress ParseAddress(string value, string label) {
            if (!IPAddress.TryParse(value, out IPAddress? parsed) || parsed.AddressFamily != global::System.Net.Sockets.AddressFamily.InterNetwork) {
                Console.Error.WriteLine("Bad {0} address", label);
                Exit(-1);
            }
            return parsed!;
        }
    }
}

internal readonly record struct PcapTimestamp(long Seconds, long Fraction);

internal static class PcapScanner {
    private const int DltNull = 0;
    private const int DltEthernet = 1;
    private const int DltLinuxSll = 113;

    public static int Scan(
        string fileName,
        IPAddress? sourceAddress,
        ushort sourcePort,
        IPAddress? destinationAddress,
        ushort destinationPort,
        bool bothWays,
        Func<PcapTimestamp, int>? timingHandler,
        Func<ReadOnlyMemory<byte>, bool, int>? packetHandler) {
        try {
            using FileStream stream = File.OpenRead(fileName);
            Span<byte> global = stackalloc byte[24];
            if (stream.Read(global) != global.Length)
                return Fail("Incomplete PCAP global header.");

            PcapFormat format = PcapFormat.Parse(global[..4]);
            ushort major = format.ReadUInt16(global[4..6]);
            ushort minor = format.ReadUInt16(global[6..8]);
            int dataLink = checked((int)format.ReadUInt32(global[20..24]));
            Console.WriteLine("PCAP file version {0}.{1}", major, minor);
            Console.WriteLine(dataLink switch {
                DltEthernet => "Datalink type ethernet",
                DltLinuxSll => "Datalink type cooked Linux socket",
                DltNull => "Datalink type NULL",
                _ => $"Unsupported data link type {dataLink}"
            });
            if (dataLink is not DltEthernet and not DltLinuxSll and not DltNull)
                return -1;

            byte[]? sourceBytes = sourceAddress?.GetAddressBytes();
            byte[]? destinationBytes = destinationAddress?.GetAddressBytes();
            int accepted = 0;
            Span<byte> packetHeader = stackalloc byte[16];

            while (true) {
                int headerRead = ReadExactlyOrEof(stream, packetHeader);
                if (headerRead == 0)
                    break;
                if (headerRead != packetHeader.Length)
                    return Fail("Incomplete PCAP packet header.");

                uint seconds = format.ReadUInt32(packetHeader[0..4]);
                uint fraction = format.ReadUInt32(packetHeader[4..8]);
                uint capturedLength = format.ReadUInt32(packetHeader[8..12]);
                uint packetLength = format.ReadUInt32(packetHeader[12..16]);
                if (capturedLength != packetLength)
                    throw new InvalidDataException($"Truncated packet - total len = {packetLength}, captured len = {capturedLength}");
                if (capturedLength > 16 * 1024 * 1024)
                    throw new InvalidDataException("PCAP packet exceeds the managed safety limit.");

                byte[] packet = new byte[checked((int)capturedLength)];
                if (ReadExactlyOrEof(stream, packet) != packet.Length)
                    return Fail("Incomplete PCAP packet body.");

                if (!TryGetUdpDatagram(packet, (int)dataLink, out UdpDatagram datagram))
                    continue;

                long microseconds = format.NanosecondPrecision ? fraction / 1_000L : fraction;
                timingHandler?.Invoke(new PcapTimestamp(seconds, microseconds));

                bool forward = Matches(datagram, sourceBytes, sourcePort, destinationBytes, destinationPort);
                if (!forward) {
                    if (!bothWays || !Matches(datagram, destinationBytes, destinationPort, sourceBytes, sourcePort))
                        continue;
                }

                packetHandler?.Invoke(datagram.Payload, forward);
                accepted++;
            }

            Console.Error.WriteLine("In pcap {0} there were {1} accepted packets", fileName, accepted);
            return 0;
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) {
            Console.Error.WriteLine("Can't scan PCAP file: {0}", ex.Message);
            return -1;
        }
    }

    private static bool Matches(
        UdpDatagram datagram,
        byte[]? sourceAddress,
        ushort sourcePort,
        byte[]? destinationAddress,
        ushort destinationPort) {
        return (sourceAddress is null || datagram.SourceAddress.AsSpan().SequenceEqual(sourceAddress))
            && (sourcePort == 0 || datagram.SourcePort == sourcePort)
            && (destinationAddress is null || datagram.DestinationAddress.AsSpan().SequenceEqual(destinationAddress))
            && (destinationPort == 0 || datagram.DestinationPort == destinationPort);
    }

    private static bool TryGetUdpDatagram(byte[] packet, int dataLink, out UdpDatagram datagram) {
        datagram = default;
        int offset;
        ushort etherType = 0;

        switch (dataLink) {
            case DltEthernet:
                if (packet.Length < 14)
                    return false;
                etherType = BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(12, 2));
                offset = 14;
                if (etherType == 0x8100) {
                    if (packet.Length < 18)
                        return false;
                    etherType = BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(16, 2));
                    offset = 18;
                }
                break;

            case DltLinuxSll:
                if (packet.Length < 16)
                    return false;
                etherType = BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(14, 2));
                offset = 16;
                break;

            case DltNull:
                if (packet.Length < 4)
                    return false;
                uint familyLittle = BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(0, 4));
                uint familyBig = BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(0, 4));
                if (familyLittle == 2 || familyBig == 2)
                    etherType = 0x0800;
                else if (familyLittle is 10 or 24 or 28 || familyBig is 10 or 24 or 28)
                    etherType = 0x86DD;
                else
                    return false;
                offset = 4;
                break;

            default:
                return false;
        }

        return etherType switch {
            0x0800 => TryParseIpv4(packet, offset, out datagram),
            0x86DD => TryParseIpv6(packet, offset, out datagram),
            _ => false
        };
    }

    private static bool TryParseIpv4(byte[] packet, int offset, out UdpDatagram datagram) {
        datagram = default;
        if (packet.Length < offset + 20)
            return false;
        ReadOnlySpan<byte> ip = packet.AsSpan(offset);
        if ((ip[0] >> 4) != 4 || ip[9] != 17)
            return false;
        int headerLength = (ip[0] & 0x0F) * 4;
        if (headerLength < 20 || packet.Length < offset + headerLength + 8)
            return false;

        int udpOffset = offset + headerLength;
        return BuildUdp(
            packet,
            udpOffset,
            packet.AsSpan(offset + 12, 4).ToArray(),
            packet.AsSpan(offset + 16, 4).ToArray(),
            out datagram);
    }

    private static bool TryParseIpv6(byte[] packet, int offset, out UdpDatagram datagram) {
        datagram = default;
        if (packet.Length < offset + 40)
            return false;
        ReadOnlySpan<byte> ip = packet.AsSpan(offset);
        if ((ip[0] >> 4) != 6 || ip[6] != 17)
            return false;
        int udpOffset = offset + 40;
        if (packet.Length < udpOffset + 8)
            return false;
        return BuildUdp(
            packet,
            udpOffset,
            packet.AsSpan(offset + 8, 16).ToArray(),
            packet.AsSpan(offset + 24, 16).ToArray(),
            out datagram);
    }

    private static bool BuildUdp(
        byte[] packet,
        int udpOffset,
        byte[] sourceAddress,
        byte[] destinationAddress,
        out UdpDatagram datagram) {
        datagram = default;
        ReadOnlySpan<byte> udp = packet.AsSpan(udpOffset);
        ushort sourcePort = BinaryPrimitives.ReadUInt16BigEndian(udp[0..2]);
        ushort destinationPort = BinaryPrimitives.ReadUInt16BigEndian(udp[2..4]);
        int udpLength = BinaryPrimitives.ReadUInt16BigEndian(udp[4..6]);
        if (udpLength < 8 || udpOffset + udpLength > packet.Length)
            return false;

        byte[] payload = packet.AsSpan(udpOffset + 8, udpLength - 8).ToArray();
        datagram = new UdpDatagram(sourceAddress, destinationAddress, sourcePort, destinationPort, payload);
        return true;
    }

    private static int ReadExactlyOrEof(Stream stream, Span<byte> destination) {
        int total = 0;
        while (total < destination.Length) {
            int read = stream.Read(destination[total..]);
            if (read == 0)
                break;
            total += read;
        }
        return total;
    }

    private static int Fail(string message) {
        Console.Error.WriteLine(message);
        return -1;
    }

    private readonly record struct UdpDatagram(
        byte[] SourceAddress,
        byte[] DestinationAddress,
        ushort SourcePort,
        ushort DestinationPort,
        ReadOnlyMemory<byte> Payload);

    private readonly record struct PcapFormat(bool LittleEndian, bool NanosecondPrecision) {
        public static PcapFormat Parse(ReadOnlySpan<byte> magic) {
            if (magic.SequenceEqual(new byte[] { 0xD4, 0xC3, 0xB2, 0xA1 }))
                return new PcapFormat(true, false);
            if (magic.SequenceEqual(new byte[] { 0xA1, 0xB2, 0xC3, 0xD4 }))
                return new PcapFormat(false, false);
            if (magic.SequenceEqual(new byte[] { 0x4D, 0x3C, 0xB2, 0xA1 }))
                return new PcapFormat(true, true);
            if (magic.SequenceEqual(new byte[] { 0xA1, 0xB2, 0x3C, 0x4D }))
                return new PcapFormat(false, true);
            throw new InvalidDataException("Unsupported PCAP magic number.");
        }

        public ushort ReadUInt16(ReadOnlySpan<byte> value) => LittleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(value)
            : BinaryPrimitives.ReadUInt16BigEndian(value);

        public uint ReadUInt32(ReadOnlySpan<byte> value) => LittleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(value)
            : BinaryPrimitives.ReadUInt32BigEndian(value);
    }
}

internal sealed class UdptlReceiver {
    private const int MaxDatagram = 400;
    private const int MaxFecPackets = 5;
    private const int BufferMask = 15;
    private readonly Func<ReadOnlyMemory<byte>, int, int> _handler;
    private readonly RxSlot[] _rx = Enumerable.Range(0, BufferMask + 1).Select(_ => new RxSlot()).ToArray();
    private int _rxSequenceNumber;

    public UdptlReceiver(Func<ReadOnlyMemory<byte>, int, int> handler) {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public int Receive(ReadOnlySpan<byte> packet) {
        int pointer = 0;
        if (packet.Length < 2)
            return -1;
        int sequenceNumber = BinaryPrimitives.ReadUInt16BigEndian(packet[..2]);
        pointer += 2;

        if (!DecodeOpenType(packet, ref pointer, out ReadOnlySpan<byte> primary))
            return -1;
        if (pointer >= packet.Length || primary.Length > MaxDatagram)
            return -1;

        for (int index = _rxSequenceNumber; sequenceNumber > index; index++)
            _rx[index & BufferMask].ClearMissing();

        int slotIndex = sequenceNumber & BufferMask;
        _rx[slotIndex].SetPrimary(primary);

        bool fecMode = (packet[pointer++] & 0x80) != 0;
        if (!fecMode) {
            List<byte[]> secondaries = new(8);
            int status;
            do {
                status = DecodeLength(packet, ref pointer, out int count);
                if (status < 0 || secondaries.Count + count >= 16)
                    return -1;
                for (int index = 0; index < count; index++) {
                    if (!DecodeOpenType(packet, ref pointer, out ReadOnlySpan<byte> secondary))
                        return -1;
                    secondaries.Add(secondary.ToArray());
                }
            }
            while (status > 0);

            if (pointer != packet.Length)
                return -1;

            if (sequenceNumber > _rxSequenceNumber) {
                for (int index = secondaries.Count; index > 0; index--) {
                    int recoveredSequence = sequenceNumber - index;
                    if (recoveredSequence < _rxSequenceNumber)
                        continue;
                    byte[] recovered = secondaries[index - 1];
                    _rx[recoveredSequence & BufferMask].SetPrimary(recovered);
                    _handler(recovered, recoveredSequence);
                }
            }
        } else {
            if (pointer + 2 > packet.Length || packet[pointer++] != 1)
                return -1;
            int span = packet[pointer++];
            RxSlot current = _rx[slotIndex];
            current.FecSpan = span;

            if (pointer >= packet.Length)
                return -1;
            int entries = packet[pointer++];
            if (entries > MaxFecPackets)
                return -1;
            current.FecEntries = entries;

            for (int index = 0; index < entries; index++) {
                if (!DecodeOpenType(packet, ref pointer, out ReadOnlySpan<byte> fec) || fec.Length > MaxDatagram)
                    return -1;
                current.SetFec(index, fec);
            }
            if (pointer != packet.Length)
                return -1;

            RepairFec(sequenceNumber, slotIndex, span, entries);
        }

        if (sequenceNumber >= _rxSequenceNumber)
            _handler(primary.ToArray(), sequenceNumber);
        _rxSequenceNumber = (sequenceNumber + 1) & 0xFFFF;
        return 0;
    }

    private void RepairFec(int sequenceNumber, int currentIndex, int span, int entries) {
        bool[] repaired = new bool[BufferMask + 1];
        repaired[currentIndex] = true;
        int end = (currentIndex - (16 - span * entries)) & BufferMask;

        for (int slot = currentIndex; slot != end; slot = (slot - 1) & BufferMask) {
            RxSlot fecSlot = _rx[slot];
            if (fecSlot.FecLength[0] <= 0)
                continue;

            for (int entry = 0; entry < fecSlot.FecEntries; entry++) {
                int limit = (slot + entry) & BufferMask;
                int missing = -1;
                int start = (limit - fecSlot.FecSpan * fecSlot.FecEntries) & BufferMask;
                for (int source = start; source != limit; source = (source + fecSlot.FecEntries) & BufferMask) {
                    if (_rx[source].BufferLength <= 0)
                        missing = missing == -1 ? source : -2;
                }
                if (missing < 0)
                    continue;

                int length = fecSlot.FecLength[entry];
                byte[] destination = _rx[missing].Buffer;
                for (int octet = 0; octet < length; octet++) {
                    byte value = fecSlot.Fec[entry][octet];
                    for (int source = start; source != limit; source = (source + fecSlot.FecEntries) & BufferMask) {
                        if (_rx[source].BufferLength > octet)
                            value ^= _rx[source].Buffer[octet];
                    }
                    destination[octet] = value;
                }
                _rx[missing].BufferLength = length;
                repaired[missing] = true;
            }
        }

        int recoveredSequence = sequenceNumber - BufferMask;
        for (int slot = (currentIndex + 1) & BufferMask; slot != currentIndex; slot = (slot + 1) & BufferMask, recoveredSequence++) {
            if (repaired[slot])
                _handler(_rx[slot].Buffer.AsMemory(0, _rx[slot].BufferLength), recoveredSequence);
        }
    }

    private static int DecodeLength(ReadOnlySpan<byte> packet, ref int pointer, out int value) {
        value = 0;
        if (pointer >= packet.Length)
            return -1;
        byte first = packet[pointer];
        if ((first & 0x80) == 0) {
            value = first;
            pointer++;
            return 0;
        }
        if ((first & 0x40) == 0) {
            if (pointer >= packet.Length - 1)
                return -1;
            value = BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(pointer, 2)) & 0x3FFF;
            pointer += 2;
            return 0;
        }
        value = (first & 0x3F) << 14;
        pointer++;
        return 1;
    }

    private static bool DecodeOpenType(ReadOnlySpan<byte> packet, ref int pointer, out ReadOnlySpan<byte> value) {
        value = default;
        if (DecodeLength(packet, ref pointer, out int length) != 0)
            return false;
        if (length < 0 || pointer + length > packet.Length)
            return false;
        value = packet.Slice(pointer, length);
        pointer += length;
        return true;
    }

    private sealed class RxSlot {
        public byte[] Buffer { get; } = new byte[MaxDatagram];
        public int BufferLength { get; set; } = -1;
        public int[] FecLength { get; } = new int[MaxFecPackets];
        public byte[][] Fec { get; } = Enumerable.Range(0, MaxFecPackets).Select(_ => new byte[MaxDatagram]).ToArray();
        public int FecSpan { get; set; }
        public int FecEntries { get; set; }

        public void ClearMissing() {
            BufferLength = -1;
            Array.Clear(FecLength);
            FecSpan = 0;
            FecEntries = 0;
        }

        public void SetPrimary(ReadOnlySpan<byte> data) {
            data.CopyTo(Buffer);
            BufferLength = data.Length;
            Array.Clear(FecLength);
            FecSpan = 0;
            FecEntries = 0;
        }

        public void SetFec(int index, ReadOnlySpan<byte> data) {
            data.CopyTo(Fec[index]);
            FecLength[index] = data.Length;
        }
    }
}

internal sealed class WaveFileWriter : IDisposable {
    private readonly FileStream _stream;
    private readonly int _channels;
    private long _dataBytes;
    private bool _disposed;

    public WaveFileWriter(string fileName, int sampleRate, int channels) {
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (channels <= 0)
            throw new ArgumentOutOfRangeException(nameof(channels));
        _channels = channels;
        _stream = File.Create(fileName);
        WriteHeader(sampleRate, channels);
    }

    public void WriteFrames(ReadOnlySpan<short> interleaved, int frames) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(frames);
        int samples = checked(frames * _channels);
        if (samples > interleaved.Length)
            throw new ArgumentException("The interleaved buffer does not contain the requested frame count.", nameof(interleaved));

        Span<byte> bytes = stackalloc byte[512];
        int sampleIndex = 0;
        while (sampleIndex < samples) {
            int blockSamples = Math.Min(bytes.Length / sizeof(short), samples - sampleIndex);
            Span<byte> block = bytes[..checked(blockSamples * sizeof(short))];
            for (int index = 0; index < blockSamples; index++)
                BinaryPrimitives.WriteInt16LittleEndian(block.Slice(index * 2, 2), interleaved[sampleIndex + index]);
            _stream.Write(block);
            _dataBytes += block.Length;
            sampleIndex += blockSamples;
        }
    }

    public void Dispose() {
        if (_disposed)
            return;
        long fileLength = _stream.Length;
        _stream.Position = 4;
        WriteUInt32(checked((uint)(fileLength - 8)));
        _stream.Position = 40;
        WriteUInt32(checked((uint)_dataBytes));
        _stream.Dispose();
        _disposed = true;
    }

    private void WriteHeader(int sampleRate, int channels) {
        _stream.Write(Encoding.ASCII.GetBytes("RIFF"));
        WriteUInt32(0);
        _stream.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        WriteUInt32(16);
        WriteUInt16(1);
        WriteUInt16(checked((ushort)channels));
        WriteUInt32(checked((uint)sampleRate));
        WriteUInt32(checked((uint)(sampleRate * channels * sizeof(short))));
        WriteUInt16(checked((ushort)(channels * sizeof(short))));
        WriteUInt16(16);
        _stream.Write(Encoding.ASCII.GetBytes("data"));
        WriteUInt32(0);
    }

    private void WriteUInt16(ushort value) {
        Span<byte> data = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(data, value);
        _stream.Write(data);
    }

    private void WriteUInt32(uint value) {
        Span<byte> data = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(data, value);
        _stream.Write(data);
    }
}
