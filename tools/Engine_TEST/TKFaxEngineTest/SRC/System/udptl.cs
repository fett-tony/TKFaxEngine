/*
 * Managed C# port of SpanDSP UDPTL.
 * Contains declarations, state types and implementation in one file.
 */
#nullable enable

using System;
using System.Buffers.Binary;

namespace TKFaxEngineTest.System;

public delegate int UdptlRxPacketHandler(
    object? userData,
    ReadOnlySpan<byte> message,
    int sequenceNumber);

public enum UdptlErrorCorrectionScheme {
    None = 0,
    Fec = 1,
    Redundancy = 2
}

public sealed class UdptlLogger {
    public string Protocol { get; set; } = "UDPTL";
    public Action<string>? FlowSink { get; set; }
    public Action<string>? WarningSink { get; set; }

    public void Flow(string message) => FlowSink?.Invoke(message);
    public void Warning(string message) => (WarningSink ?? FlowSink)?.Invoke(message);
}

public sealed class UdptlFecTxBuffer {
    public int BufferLength { get; set; } = -1;
    public byte[] Buffer { get; } = new byte[UdptlConstants.LocalFaxMaxDatagram];
}

public sealed class UdptlFecRxBuffer {
    public int BufferLength { get; set; } = -1;
    public byte[] Buffer { get; } = new byte[UdptlConstants.LocalFaxMaxDatagram];
    public int[] FecLength { get; } = new int[UdptlConstants.LocalFaxMaxFecPackets];
    public byte[][] Fec { get; } = CreateFecBuffers();
    public int FecSpan { get; set; }
    public int FecEntries { get; set; }

    private static byte[][] CreateFecBuffers() {
        var result = new byte[UdptlConstants.LocalFaxMaxFecPackets][];
        for (int i = 0; i < result.Length; i++)
            result[i] = new byte[UdptlConstants.LocalFaxMaxDatagram];
        return result;
    }
}

public static class UdptlConstants {
    public const int LocalFaxMaxDatagram = 400;
    public const int LocalFaxMaxFecPackets = 5;
    public const int BufferMask = 15;
    public const int BufferCount = BufferMask + 1;
}

public sealed class UdptlState : IDisposable {
    internal UdptlRxPacketHandler ReceivePacketHandler { get; set; }
    internal object? UserData { get; set; }

    public UdptlErrorCorrectionScheme ErrorCorrectionScheme { get; internal set; }
    public int ErrorCorrectionEntries { get; internal set; }
    public int ErrorCorrectionSpan { get; internal set; }
    public int FarMaxDatagramSize { get; internal set; }
    public int LocalMaxDatagramSize { get; internal set; }
    public bool Verbose { get; set; }
    public int TxSequenceNumber { get; internal set; }
    public int RxSequenceNumber { get; internal set; }
    public int RxExpectedSequenceNumber { get; internal set; }
    public UdptlFecTxBuffer[] Tx { get; } = CreateTxBuffers();
    public UdptlFecRxBuffer[] Rx { get; } = CreateRxBuffers();
    public UdptlLogger Logging { get; } = new();

    internal bool Disposed { get; private set; }

    internal UdptlState(
        UdptlErrorCorrectionScheme scheme,
        int span,
        int entries,
        UdptlRxPacketHandler receivePacketHandler,
        object? userData) {
        ReceivePacketHandler = receivePacketHandler;
        UserData = userData;
        ErrorCorrectionScheme = scheme;
        ErrorCorrectionSpan = span;
        ErrorCorrectionEntries = entries;
        FarMaxDatagramSize = UdptlConstants.LocalFaxMaxDatagram;
        LocalMaxDatagramSize = UdptlConstants.LocalFaxMaxDatagram;
    }

    public int ReceivePacket(ReadOnlySpan<byte> datagram) => Udptl.ReceivePacket(this, datagram);

    public int BuildPacket(Span<byte> destination, ReadOnlySpan<byte> primaryMessage) =>
        Udptl.BuildPacket(this, destination, primaryMessage);

    public void Dispose() {
        if (Disposed)
            return;
        Disposed = true;
        UserData = null;
        Logging.FlowSink = null;
        Logging.WarningSink = null;
        GC.SuppressFinalize(this);
    }

    internal void ThrowIfDisposed() {
        if (Disposed)
            throw new ObjectDisposedException(nameof(UdptlState));
    }

    private static UdptlFecTxBuffer[] CreateTxBuffers() {
        var result = new UdptlFecTxBuffer[UdptlConstants.BufferCount];
        for (int i = 0; i < result.Length; i++)
            result[i] = new UdptlFecTxBuffer();
        return result;
    }

    private static UdptlFecRxBuffer[] CreateRxBuffers() {
        var result = new UdptlFecRxBuffer[UdptlConstants.BufferCount];
        for (int i = 0; i < result.Length; i++)
            result[i] = new UdptlFecRxBuffer();
        return result;
    }
}

public static class Udptl {
    public static UdptlState Initialize(
        UdptlState? state,
        UdptlErrorCorrectionScheme scheme,
        int span,
        int entries,
        UdptlRxPacketHandler receivePacketHandler,
        object? userData = null) {
        ArgumentNullException.ThrowIfNull(receivePacketHandler);
        ValidateErrorCorrection(scheme, span, entries);

        if (state is null)
            return new UdptlState(scheme, span, entries, receivePacketHandler, userData);

        state.ThrowIfDisposed();
        state.ErrorCorrectionScheme = scheme;
        state.ErrorCorrectionSpan = span;
        state.ErrorCorrectionEntries = entries;
        state.FarMaxDatagramSize = UdptlConstants.LocalFaxMaxDatagram;
        state.LocalMaxDatagramSize = UdptlConstants.LocalFaxMaxDatagram;
        state.TxSequenceNumber = 0;
        state.RxSequenceNumber = 0;
        state.RxExpectedSequenceNumber = 0;
        state.ReceivePacketHandler = receivePacketHandler;
        state.UserData = userData;

        foreach (UdptlFecRxBuffer item in state.Rx) {
            item.BufferLength = -1;
            item.FecSpan = 0;
            item.FecEntries = 0;
            Array.Clear(item.Buffer);
            Array.Clear(item.FecLength);
            foreach (byte[] fec in item.Fec)
                Array.Clear(fec);
        }

        foreach (UdptlFecTxBuffer item in state.Tx) {
            item.BufferLength = -1;
            Array.Clear(item.Buffer);
        }

        return state;
    }

    public static int ReceivePacket(UdptlState state, ReadOnlySpan<byte> buffer) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();

        int pointer = 0;
        if (buffer.Length < 2)
            return -1;

        int sequenceNumber = BinaryPrimitives.ReadUInt16BigEndian(buffer);
        pointer += 2;

        int status = DecodeOpenType(buffer, ref pointer, out int primaryOffset, out int primaryLength);
        if (status != 0)
            return -1;
        if (pointer + 1 > buffer.Length || primaryLength > UdptlConstants.LocalFaxMaxDatagram)
            return -1;

        for (int i = state.RxSequenceNumber; sequenceNumber > i; i++) {
            int slot = i & UdptlConstants.BufferMask;
            ResetRxSlot(state.Rx[slot]);
        }

        int current = sequenceNumber & UdptlConstants.BufferMask;
        SavePrimary(state.Rx[current], buffer.Slice(primaryOffset, primaryLength));

        if ((buffer[pointer++] & 0x80) == 0) {
            if (DecodeRedundancy(state, buffer, ref pointer, sequenceNumber) < 0)
                return -1;
        } else {
            if (DecodeFec(state, buffer, ref pointer, sequenceNumber) < 0)
                return -1;
        }

        if (sequenceNumber >= state.RxSequenceNumber) {
            if (state.ReceivePacketHandler(
                    state.UserData,
                    buffer.Slice(primaryOffset, primaryLength),
                    sequenceNumber) < 0) {
                state.Logging.Flow("Bad IFP");
            }
        }

        state.RxSequenceNumber = (sequenceNumber + 1) & 0xFFFF;
        return 0;
    }

    public static int BuildPacket(
        UdptlState state,
        Span<byte> destination,
        ReadOnlySpan<byte> primaryMessage) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();

        int messageLength = primaryMessage.Length;
        if (messageLength < 1 || messageLength > UdptlConstants.LocalFaxMaxDatagram)
            return -1;

        int sequence = state.TxSequenceNumber & 0xFFFF;
        int entry = sequence & UdptlConstants.BufferMask;
        UdptlFecTxBuffer saved = state.Tx[entry];
        saved.BufferLength = messageLength;
        primaryMessage.CopyTo(saved.Buffer);

        int length = 0;
        if (!EnsureCapacity(destination, length, 2))
            return -1;
        BinaryPrimitives.WriteUInt16BigEndian(destination, (ushort)sequence);
        length += 2;

        if (EncodeOpenType(destination, ref length, primaryMessage) < 0)
            return -1;

        switch (state.ErrorCorrectionScheme) {
            case UdptlErrorCorrectionScheme.None:
                if (!EnsureCapacity(destination, length, 1))
                    return -1;
                destination[length++] = 0x00;
                if (EncodeLength(destination, ref length, 0) < 0)
                    return -1;
                break;

            case UdptlErrorCorrectionScheme.Redundancy:
                if (BuildRedundancy(state, destination, ref length, entry) < 0)
                    return -1;
                break;

            case UdptlErrorCorrectionScheme.Fec:
                if (BuildFec(state, destination, ref length, sequence, entry) < 0)
                    return -1;
                break;

            default:
                return -1;
        }

        state.TxSequenceNumber++;
        return length;
    }

    public static int SetErrorCorrection(
        UdptlState state,
        int scheme,
        int span,
        int entries) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();

        if (scheme != -1) {
            if (!Enum.IsDefined(typeof(UdptlErrorCorrectionScheme), scheme))
                return -1;
            state.ErrorCorrectionScheme = (UdptlErrorCorrectionScheme)scheme;
        }
        if (span >= 0)
            state.ErrorCorrectionSpan = span;
        if (entries >= 0)
            state.ErrorCorrectionEntries = entries;
        return 0;
    }

    public static int GetErrorCorrection(
        UdptlState state,
        out UdptlErrorCorrectionScheme scheme,
        out int span,
        out int entries) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        scheme = state.ErrorCorrectionScheme;
        span = state.ErrorCorrectionSpan;
        entries = state.ErrorCorrectionEntries;
        return 0;
    }

    public static int SetLocalMaxDatagram(UdptlState state, int maximum) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        state.LocalMaxDatagramSize = maximum;
        return 0;
    }

    public static int GetLocalMaxDatagram(UdptlState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        return state.LocalMaxDatagramSize;
    }

    public static int SetFarMaxDatagram(UdptlState state, int maximum) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        state.FarMaxDatagramSize = maximum;
        return 0;
    }

    public static int GetFarMaxDatagram(UdptlState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        return state.FarMaxDatagramSize;
    }

    public static UdptlLogger GetLoggingState(UdptlState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        return state.Logging;
    }

    public static int Release(UdptlState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        return 0;
    }

    public static int Free(UdptlState? state) {
        state?.Dispose();
        return 0;
    }

    private static int DecodeRedundancy(
        UdptlState state,
        ReadOnlySpan<byte> buffer,
        ref int pointer,
        int sequenceNumber) {
        var offsets = new int[UdptlConstants.BufferCount];
        var lengths = new int[UdptlConstants.BufferCount];
        int totalCount = 0;
        int status;

        do {
            status = DecodeLength(buffer, ref pointer, out int count);
            if (status < 0 || totalCount + count >= UdptlConstants.BufferCount)
                return -1;

            for (int i = 0; i < count; i++) {
                if (DecodeOpenType(buffer, ref pointer, out offsets[totalCount + i], out lengths[totalCount + i]) != 0)
                    return -1;
            }
            totalCount += count;
        } while (status > 0);

        if (pointer != buffer.Length)
            return -1;

        if (sequenceNumber > state.RxSequenceNumber) {
            for (int i = totalCount; i > 0; i--) {
                int recoveredSequence = sequenceNumber - i;
                if (recoveredSequence < state.RxSequenceNumber)
                    continue;

                ReadOnlySpan<byte> secondary = buffer.Slice(offsets[i - 1], lengths[i - 1]);
                int slot = recoveredSequence & UdptlConstants.BufferMask;
                SavePrimary(state.Rx[slot], secondary);
                if (state.ReceivePacketHandler(state.UserData, secondary, recoveredSequence) < 0)
                    state.Logging.Flow("Bad IFP");
            }
        }

        return 0;
    }

    private static int DecodeFec(
        UdptlState state,
        ReadOnlySpan<byte> buffer,
        ref int pointer,
        int sequenceNumber) {
        if (pointer + 2 > buffer.Length || buffer[pointer++] != 1)
            return -1;

        int span = buffer[pointer++];
        int current = sequenceNumber & UdptlConstants.BufferMask;
        state.Rx[current].FecSpan = span;

        var repaired = new bool[UdptlConstants.BufferCount];
        repaired[current] = true;

        if (pointer + 1 > buffer.Length)
            return -1;
        int entries = buffer[pointer++];
        if (entries > UdptlConstants.LocalFaxMaxFecPackets)
            return -1;
        state.Rx[current].FecEntries = entries;

        for (int i = 0; i < entries; i++) {
            if (DecodeOpenType(buffer, ref pointer, out int offset, out int fecLength) != 0)
                return -1;
            if (fecLength > UdptlConstants.LocalFaxMaxDatagram)
                return -1;
            state.Rx[current].FecLength[i] = fecLength;
            if (fecLength > 0)
                buffer.Slice(offset, fecLength).CopyTo(state.Rx[current].Fec[i]);
        }

        if (pointer != buffer.Length)
            return -1;

        int stop = (current - (UdptlConstants.BufferCount - span * entries)) & UdptlConstants.BufferMask;
        for (int l = current; l != stop; l = (l - 1) & UdptlConstants.BufferMask) {
            UdptlFecRxBuffer parityOwner = state.Rx[l];
            if (parityOwner.FecLength[0] <= 0)
                continue;

            for (int m = 0; m < parityOwner.FecEntries; m++) {
                int limit = (l + m) & UdptlConstants.BufferMask;
                int missing = -1;
                int start = (limit - parityOwner.FecSpan * parityOwner.FecEntries) & UdptlConstants.BufferMask;

                for (int k = start; k != limit; k = (k + parityOwner.FecEntries) & UdptlConstants.BufferMask) {
                    if (state.Rx[k].BufferLength <= 0)
                        missing = missing == -1 ? k : -2;
                }

                if (missing < 0)
                    continue;

                int recoveredLength = parityOwner.FecLength[m];
                UdptlFecRxBuffer target = state.Rx[missing];
                for (int j = 0; j < recoveredLength; j++) {
                    byte value = parityOwner.Fec[m][j];
                    for (int k = start; k != limit; k = (k + parityOwner.FecEntries) & UdptlConstants.BufferMask) {
                        if (state.Rx[k].BufferLength > j)
                            value ^= state.Rx[k].Buffer[j];
                    }
                    target.Buffer[j] = value;
                }
                target.BufferLength = recoveredLength;
                repaired[missing] = true;
            }
        }

        int replaySequence = sequenceNumber - UdptlConstants.BufferMask;
        for (int slot = (current + 1) & UdptlConstants.BufferMask;
             slot != current;
             slot = (slot + 1) & UdptlConstants.BufferMask, replaySequence++) {
            if (!repaired[slot])
                continue;
            UdptlFecRxBuffer item = state.Rx[slot];
            if (state.ReceivePacketHandler(
                    state.UserData,
                    item.Buffer.AsSpan(0, item.BufferLength),
                    replaySequence) < 0) {
                state.Logging.Flow("Bad IFP");
            }
        }

        return 0;
    }

    private static int BuildRedundancy(
        UdptlState state,
        Span<byte> destination,
        ref int length,
        int entry) {
        if (!EnsureCapacity(destination, length, 1))
            return -1;
        destination[length++] = 0x00;

        int entries = state.TxSequenceNumber > state.ErrorCorrectionEntries
            ? state.ErrorCorrectionEntries
            : state.TxSequenceNumber;
        int entriesLengthOffset = length;
        if (EncodeLength(destination, ref length, entries) < 0)
            return -1;

        for (int m = 0; m < entries; m++) {
            int previousLength = length;
            int sourceIndex = (entry - m - 1) & UdptlConstants.BufferMask;
            UdptlFecTxBuffer source = state.Tx[sourceIndex];
            if (EncodeOpenType(destination, ref length, source.Buffer.AsSpan(0, source.BufferLength)) < 0)
                return -1;

            if (length > state.FarMaxDatagramSize) {
                length = previousLength;
                int rewrite = entriesLengthOffset;
                if (EncodeLength(destination, ref rewrite, m) < 0)
                    return -1;
                break;
            }
        }
        return 0;
    }

    private static int BuildFec(
        UdptlState state,
        Span<byte> destination,
        ref int length,
        int sequence,
        int entry) {
        int span = state.ErrorCorrectionSpan;
        int entries = state.ErrorCorrectionEntries;
        if (entries > UdptlConstants.LocalFaxMaxFecPackets)
            return -1;

        if (sequence < state.ErrorCorrectionSpan * state.ErrorCorrectionEntries) {
            entries = state.ErrorCorrectionSpan == 0 ? 0 : sequence / state.ErrorCorrectionSpan;
            if (sequence < state.ErrorCorrectionSpan)
                span = 0;
        }

        if (!EnsureCapacity(destination, length, 4))
            return -1;
        destination[length++] = 0x80;
        destination[length++] = 1;
        destination[length++] = (byte)span;
        int entriesOffset = length;
        destination[length++] = (byte)entries;

        var fec = new byte[UdptlConstants.LocalFaxMaxDatagram];
        for (int m = 0; m < entries; m++) {
            int previousLength = length;
            int limit = (entry + m) & UdptlConstants.BufferMask;
            int highTide = 0;
            Array.Clear(fec);

            int start = (limit - span * entries) & UdptlConstants.BufferMask;
            for (int i = start; i != limit; i = (i + entries) & UdptlConstants.BufferMask) {
                UdptlFecTxBuffer source = state.Tx[i];
                if (source.BufferLength < 0)
                    continue;

                if (highTide < source.BufferLength) {
                    for (int j = 0; j < highTide; j++)
                        fec[j] ^= source.Buffer[j];
                    for (int j = highTide; j < source.BufferLength; j++)
                        fec[j] = source.Buffer[j];
                    highTide = source.BufferLength;
                } else {
                    for (int j = 0; j < source.BufferLength; j++)
                        fec[j] ^= source.Buffer[j];
                }
            }

            if (EncodeOpenType(destination, ref length, fec.AsSpan(0, highTide)) < 0)
                return -1;

            if (length > state.FarMaxDatagramSize) {
                length = previousLength;
                destination[entriesOffset] = (byte)m;
                break;
            }
        }
        return 0;
    }

    private static int DecodeLength(ReadOnlySpan<byte> buffer, ref int offset, out int value) {
        value = 0;
        if (offset >= buffer.Length)
            return -1;

        byte first = buffer[offset];
        if ((first & 0x80) == 0) {
            value = first;
            offset++;
            return 0;
        }

        if ((first & 0x40) == 0) {
            if (offset >= buffer.Length - 1)
                return -1;
            value = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(offset, 2)) & 0x3FFF;
            offset += 2;
            return 0;
        }

        value = (buffer[offset++] & 0x3F) << 14;
        return 1;
    }

    private static int DecodeOpenType(
        ReadOnlySpan<byte> buffer,
        ref int offset,
        out int objectOffset,
        out int octetCount) {
        objectOffset = 0;
        octetCount = 0;
        if (DecodeLength(buffer, ref offset, out int count) != 0)
            return -1;

        octetCount = count;
        objectOffset = offset;
        if (count > 0) {
            if (offset + count > buffer.Length)
                return -1;
            offset += count;
        }
        return 0;
    }

    private static int EncodeLength(Span<byte> buffer, ref int offset, int value) {
        if (value < 0)
            return -1;
        if (value < 0x80) {
            if (!EnsureCapacity(buffer, offset, 1))
                return -1;
            buffer[offset++] = (byte)value;
            return value;
        }

        if (value < 0x4000) {
            if (!EnsureCapacity(buffer, offset, 2))
                return -1;
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(offset, 2), (ushort)(0x8000 | value));
            offset += 2;
            return value;
        }

        int multiplier = value < 0x10000 ? value >> 14 : 4;
        if (!EnsureCapacity(buffer, offset, 1))
            return -1;
        buffer[offset++] = (byte)(0xC0 | multiplier);
        return multiplier << 14;
    }

    private static int EncodeOpenType(
        Span<byte> buffer,
        ref int offset,
        ReadOnlySpan<byte> data) {
        if (data.Length == 0)
            data = new byte[1];

        int remaining = data.Length;
        int sourceOffset = 0;
        while (true) {
            int encodedLength = EncodeLength(buffer, ref offset, remaining);
            if (encodedLength < 0)
                return -1;
            if (encodedLength > 0) {
                if (!EnsureCapacity(buffer, offset, encodedLength))
                    return -1;
                data.Slice(sourceOffset, encodedLength).CopyTo(buffer.Slice(offset, encodedLength));
                offset += encodedLength;
            }
            if (encodedLength >= remaining)
                break;
            remaining -= encodedLength;
            sourceOffset += encodedLength;
        }
        return 0;
    }

    private static void ResetRxSlot(UdptlFecRxBuffer slot) {
        slot.BufferLength = -1;
        slot.FecLength[0] = 0;
        slot.FecSpan = 0;
        slot.FecEntries = 0;
    }

    private static void SavePrimary(UdptlFecRxBuffer target, ReadOnlySpan<byte> message) {
        if (!message.IsEmpty)
            message.CopyTo(target.Buffer);
        target.BufferLength = message.Length;
        target.FecLength[0] = 0;
        target.FecSpan = 0;
        target.FecEntries = 0;
    }

    private static bool EnsureCapacity(Span<byte> destination, int offset, int needed) =>
        offset >= 0 && needed >= 0 && offset <= destination.Length - needed;

    private static void ValidateErrorCorrection(
        UdptlErrorCorrectionScheme scheme,
        int span,
        int entries) {
        if (!Enum.IsDefined(scheme))
            throw new ArgumentOutOfRangeException(nameof(scheme));
        if (span < 0)
            throw new ArgumentOutOfRangeException(nameof(span));
        if (entries < 0 || entries > UdptlConstants.LocalFaxMaxFecPackets)
            throw new ArgumentOutOfRangeException(nameof(entries));
    }
}
