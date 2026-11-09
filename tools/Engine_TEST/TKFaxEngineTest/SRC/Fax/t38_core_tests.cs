/*
 * TKFaxEngine - managed C# port of t38_core_tests.c.
 *
 * This file retains the complete spanDSP T.38 ASN.1/IFP test flow for UDP,
 * TCP and TCP/TPKT transports, including the full malformed-packet attack run.
 */

#nullable enable

using TKFaxEngine.Daten.T38;
using static TKFaxEngine.Daten.T38.T38Core;

namespace TKFaxEngineTest.Fax.Test;

public static class t38_core_tests {
    private const int MaxFields = 42;
    private const int MaxFieldLength = 8192;
    private const int DefaultAttackPackets = 100_000;

    private static bool _succeeded = true;
    private static int _t38Version;
    private static int _okIndicatorPackets;
    private static int _badIndicatorPackets;
    private static int _okDataPackets;
    private static int _badDataPackets;
    private static int _missingPackets;

    private static readonly byte[][] FieldBody = CreateFieldBody();
    private static readonly int[] FieldLength = new int[MaxFields];
    private static int _sequenceNumber;

    private static readonly int[] MessageList = new int[1_000_000];
    private static int _messageListPointer;
    private static int _messageListPointer2;
    private static readonly byte[] Concatenated = new byte[1_000_000];
    private static int _concatenatedLength;

    public sealed class T38CoreTestsExitException : Exception {
        public T38CoreTestsExitException(int exitCode)
            : base($"t38_core_tests exited with code {exitCode}.") => ExitCode = exitCode;

        public int ExitCode { get; }
    }

    public static int Main(string[] args) {
        try {
            return tkfx_t38_core_tests_body_main(args);
        } catch (T38CoreTestsExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_t38_core_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        int attackPackets = ParseArguments(testArguments);
        _succeeded = true;

        RunUdpTests(attackPackets);
        RunStreamTests(T38TransportType.Tcp, "TCP without TPKT", attackPackets);
        RunStreamTests(T38TransportType.TcpTpkt, "TCP with TPKT", attackPackets);

        if (!_succeeded) {
            Console.WriteLine("Tests failed");
            Exit(2);
        }

        Console.WriteLine("Tests passed");
        return 0;
    }

    private static int ParseArguments(string[] testArguments) {
        int attackPackets = DefaultAttackPackets;
        for (int index = 0; index < testArguments.Length; index++) {
            string argument = testArguments[index];
            if (argument == "-a") {
                if (++index >= testArguments.Length || !int.TryParse(testArguments[index], out attackPackets) || attackPackets < 0)
                    Exit(2);
            } else if (argument.StartsWith("-a", StringComparison.Ordinal) && argument.Length > 2) {
                if (!int.TryParse(argument.AsSpan(2), out attackPackets) || attackPackets < 0)
                    Exit(2);
            } else {
                Exit(2);
            }
        }

        return attackPackets;
    }

    private static void RunUdpTests(int attackPackets) {
        for (_t38Version = 0; _t38Version < 2; _t38Version++) {
            _sequenceNumber = 0;
            Console.WriteLine("Using T.38 version {0}", _t38Version);

            T38CoreState coreAState = new();
            T38CoreState coreBState = new();
            T38CoreState coreA = t38_core_init(
                coreAState,
                RxIndicatorHandler,
                RxDataHandler,
                RxMissingHandler,
                coreBState,
                TxPacketHandler,
                coreBState);
            T38CoreState coreB = t38_core_init(
                coreBState,
                RxIndicatorHandler,
                RxDataHandler,
                RxMissingHandler,
                coreAState,
                TxPacketHandler,
                coreAState);

            ConfigureCore(coreA, _t38Version, "T.38-A");
            ConfigureCore(coreB, _t38Version, "T.38-B");

            if (EncodeDecodeTests(coreA, coreB) != 0) {
                Console.WriteLine("Encode/decode tests failed");
                Exit(2);
            }

            coreA = t38_core_init(
                coreAState,
                RxIndicatorAttackHandler,
                RxDataAttackHandler,
                RxMissingAttackHandler,
                coreBState,
                TxPacketHandler,
                coreBState);
            t38_set_t38_version(coreA, _t38Version);

            if (AttackTests(coreA, attackPackets) != 0) {
                Console.WriteLine("Attack tests failed");
                Exit(2);
            }
        }
    }

    private static void RunStreamTests(T38TransportType transport, string label, int attackPackets) {
        _ = label;
        for (_t38Version = 0; _t38Version < 2; _t38Version++) {
            _sequenceNumber = 0;
            _concatenatedLength = 0;
            Console.WriteLine("Using T.38 version {0}", _t38Version);

            T38CoreState coreAState = new();
            T38CoreState coreBState = new();
            T38CoreState coreA = t38_core_init(
                coreAState,
                RxIndicatorHandler,
                RxDataHandler,
                RxMissingHandler,
                coreBState,
                TxConcatPacketHandler,
                coreBState);
            T38CoreState coreB = t38_core_init(
                coreBState,
                RxIndicatorHandler,
                RxDataHandler,
                RxMissingHandler,
                coreAState,
                TxConcatPacketHandler,
                coreAState);

            ConfigureCore(coreA, _t38Version, "T.38-A");
            ConfigureCore(coreB, _t38Version, "T.38-B");
            t38_set_pace_transmission(coreA, 0);
            t38_set_pace_transmission(coreB, 0);
            t38_set_data_transport_protocol(coreA, (int)transport);
            t38_set_data_transport_protocol(coreB, (int)transport);

            if (EncodeThenDecodeTests(coreA, coreB) != 0) {
                Console.WriteLine("Encode then decode tests failed");
                Exit(2);
            }

            coreA = t38_core_init(
                coreAState,
                RxIndicatorAttackHandler,
                RxDataAttackHandler,
                RxMissingAttackHandler,
                coreBState,
                TxPacketHandler,
                coreBState);
            t38_set_t38_version(coreA, _t38Version);
            t38_set_pace_transmission(coreA, 0);
            t38_set_data_transport_protocol(coreA, (int)transport);

            if (AttackTests(coreA, attackPackets) != 0) {
                Console.WriteLine("Attack tests failed");
                Exit(2);
            }
        }
    }

    private static void ConfigureCore(T38CoreState state, int version, string tag) {
        t38_set_t38_version(state, version);
        state.Logging.Sink = (level, message) =>
            Console.WriteLine("{0}: {1}: {2}", tag, level, message);
    }

    private static int RxMissingAttackHandler(
        T38CoreState state,
        object? userData,
        int rxSequenceNumber,
        int expectedSequenceNumber) {
        _ = state;
        _ = userData;
        _ = rxSequenceNumber;
        _ = expectedSequenceNumber;
        return 0;
    }

    private static int RxIndicatorAttackHandler(
        T38CoreState state,
        object? userData,
        T38Indicator indicator) {
        _ = state;
        _ = userData;
        _ = indicator;
        return 0;
    }

    private static int RxDataAttackHandler(
        T38CoreState state,
        object? userData,
        T38DataType dataType,
        T38FieldType fieldType,
        ReadOnlyMemory<byte> field) {
        _ = state;
        _ = userData;
        _ = dataType;
        _ = fieldType;
        _ = field;
        return 0;
    }

    private static int RxMissingHandler(
        T38CoreState state,
        object? userData,
        int rxSequenceNumber,
        int expectedSequenceNumber) {
        _ = state;
        _ = userData;
        _ = rxSequenceNumber;
        _ = expectedSequenceNumber;
        _missingPackets++;
        return 0;
    }

    private static int RxIndicatorHandler(
        T38CoreState state,
        object? userData,
        T38Indicator indicator) {
        _ = state;
        _ = userData;
        if (_messageListPointer2 < _messageListPointer && (int)indicator == MessageList[_messageListPointer2++])
            _okIndicatorPackets++;
        else
            _badIndicatorPackets++;
        return 0;
    }

    private static int RxDataHandler(
        T38CoreState state,
        object? userData,
        T38DataType dataType,
        T38FieldType fieldType,
        ReadOnlyMemory<byte> field) {
        _ = state;
        _ = userData;
        _ = field;

        if (_messageListPointer2 + 1 < _messageListPointer
            && (int)dataType == MessageList[_messageListPointer2]
            && (int)fieldType == MessageList[_messageListPointer2 + 1]) {
            _okDataPackets++;
        } else {
            _badDataPackets++;
        }

        _messageListPointer2 += 2;
        return 0;
    }

    private static int TxPacketHandler(
        T38CoreState state,
        object? userData,
        ReadOnlyMemory<byte> packet,
        int count) {
        if (userData is not T38CoreState target)
            throw new ArgumentException("The transmit user data must be a T38CoreState.", nameof(userData));

        state.Logging.Flow($"Send seq {state.TxSequenceNumber}, len {packet.Length}, count {count}");
        byte[] receivedPacket = packet.ToArray();
        if (t38_core_rx_ifp_packet(
                target,
                receivedPacket,
                receivedPacket.Length,
                (ushort)_sequenceNumber) < 0) {
            _succeeded = false;
        }
        _sequenceNumber = (_sequenceNumber + 1) & 0xFFFF;
        return 0;
    }

    private static int TxConcatPacketHandler(
        T38CoreState state,
        object? userData,
        ReadOnlyMemory<byte> packet,
        int count) {
        _ = userData;
        state.Logging.Flow($"Send seq {state.TxSequenceNumber}, len {packet.Length}, count {count}");
        if (_concatenatedLength + packet.Length > Concatenated.Length)
            throw new InvalidOperationException("The concatenated IFP test buffer overflowed.");
        packet.Span.CopyTo(Concatenated.AsSpan(_concatenatedLength));
        _concatenatedLength += packet.Length;
        _sequenceNumber = (_sequenceNumber + 1) & 0xFFFF;
        return 0;
    }

    private static int EncodeDecodeTests(T38CoreState sender, T38CoreState receiver) {
        _ = receiver;
        ResetCounters();
        SendAllMessages(sender);
        return VerifyCounters();
    }

    private static int EncodeThenDecodeTests(T38CoreState sender, T38CoreState receiver) {
        ResetCounters();
        SendAllMessages(sender);

        int offset = 0;
        _sequenceNumber = 0;
        while (offset < _concatenatedLength) {
            byte[] remainingStream = Concatenated
                .AsSpan(offset, _concatenatedLength - offset)
                .ToArray();
            int consumed = t38_core_rx_ifp_stream(
                receiver,
                remainingStream,
                remainingStream.Length,
                (ushort)_sequenceNumber);
            if (consumed <= 0) {
                _succeeded = false;
                return -1;
            }

            offset += consumed;
            _sequenceNumber = (_sequenceNumber + 1) & 0xFFFF;
        }

        return VerifyCounters();
    }

    private static void ResetCounters() {
        _okIndicatorPackets = 0;
        _badIndicatorPackets = 0;
        _okDataPackets = 0;
        _badDataPackets = 0;
        _missingPackets = 0;
        _messageListPointer = 0;
        _messageListPointer2 = 0;
    }

    private static void SendAllMessages(T38CoreState sender) {
        T38DataField[] fields = new T38DataField[MaxFields];
        byte[] abcd = { (byte)'A', (byte)'B', (byte)'C', (byte)'D' };

        for (int indicator = 0; indicator < 100; indicator++) {
            MessageList[_messageListPointer++] = indicator;
            if (t38_core_send_indicator(sender, indicator) < 0) {
                _messageListPointer--;
                break;
            }
        }

        for (int dataType = 0; dataType < 100; dataType++) {
            int fieldType;
            for (fieldType = 0; fieldType < 100; fieldType++) {
                MessageList[_messageListPointer++] = dataType;
                MessageList[_messageListPointer++] = fieldType;
                if (t38_core_send_data(
                        sender,
                        dataType,
                        fieldType,
                        Array.Empty<byte>(),
                        0,
                        (int)T38PacketCategory.ControlData) < 0) {
                    _messageListPointer -= 2;
                    break;
                }
            }

            if (fieldType == 0)
                break;
        }

        for (int dataType = 0; dataType < 100; dataType++) {
            int fieldType;
            for (fieldType = 0; fieldType < 100; fieldType++) {
                MessageList[_messageListPointer++] = dataType;
                MessageList[_messageListPointer++] = fieldType;
                if (t38_core_send_data(
                        sender,
                        dataType,
                        fieldType,
                        abcd,
                        abcd.Length,
                        (int)T38PacketCategory.ControlData) < 0) {
                    _messageListPointer -= 2;
                    break;
                }
            }

            if (fieldType == 0)
                break;
        }

        for (int dataType = 0; dataType < 100; dataType++) {
            int fieldType;
            for (fieldType = 0; fieldType < 100; fieldType++) {
                if (t38_core_send_data_multi_field(
                        sender,
                        dataType,
                        Array.Empty<T38DataField>(),
                        0,
                        (int)T38PacketCategory.ControlData) < 0) {
                    break;
                }
            }

            if (fieldType == 0)
                break;
        }

        for (int dataType = 0; dataType < 100; dataType++) {
            int fieldType;
            for (fieldType = 0; fieldType < 100; fieldType++) {
                MessageList[_messageListPointer++] = dataType;
                MessageList[_messageListPointer++] = fieldType;
                MessageList[_messageListPointer++] = dataType;
                MessageList[_messageListPointer++] = (int)T38FieldType.T4NonEcmSignalEnd;

                FieldLength[0] = 444;
                FieldLength[1] = 333;
                fields[0] = new T38DataField(
                    (T38FieldType)fieldType,
                    new ReadOnlyMemory<byte>(FieldBody[0], 0, FieldLength[0]));
                fields[1] = new T38DataField(
                    T38FieldType.T4NonEcmSignalEnd,
                    new ReadOnlyMemory<byte>(FieldBody[1], 0, FieldLength[1]));

                if (t38_core_send_data_multi_field(
                        sender,
                        dataType,
                        new[] { fields[0], fields[1] },
                        2,
                        (int)T38PacketCategory.ControlData) < 0) {
                    _messageListPointer -= 4;
                    break;
                }
            }

            if (fieldType == 0)
                break;
        }
    }

    private static int VerifyCounters() {
        Console.WriteLine(
            "Indicator packets: OK = {0}, bad = {1}",
            _okIndicatorPackets,
            _badIndicatorPackets);
        Console.WriteLine(
            "Data packets: OK = {0}, bad = {1}",
            _okDataPackets,
            _badDataPackets);
        Console.WriteLine("Missing packets = {0}", _missingPackets);

        int expectedIndicators = _t38Version == 0 ? 16 : 23;
        int expectedDataPackets = _t38Version == 0 ? 288 : 720;
        if (_okIndicatorPackets != expectedIndicators
            || _badIndicatorPackets != 0
            || _okDataPackets != expectedDataPackets
            || _badDataPackets != 0
            || _missingPackets > 0) {
            Console.WriteLine("Tests failed");
            return -1;
        }

        return 0;
    }

    private static int AttackTests(T38CoreState state, int packets) {
        var random = new Random(1_234_567);
        byte[] buffer = new byte[1024];
        int sequenceNumber = 0;

        for (int length = 1; length < 70; length++) {
            for (int packet = 0; packet < packets; packet++) {
                random.NextBytes(buffer.AsSpan(0, length));
                _ = t38_core_rx_ifp_packet(
                    state,
                    buffer,
                    length,
                    (ushort)sequenceNumber);
                sequenceNumber = (sequenceNumber + 1) & 0xFFFF;
            }
        }

        return 0;
    }

    private static byte[][] CreateFieldBody() {
        byte[][] result = new byte[MaxFields][];
        for (int index = 0; index < result.Length; index++)
            result[index] = new byte[MaxFieldLength];
        return result;
    }

    private static void Exit(int code) => throw new T38CoreTestsExitException(code);
}
