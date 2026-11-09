/*
 * TKFaxEngine - managed C# port
 *
 * Hdlc.cs
 *
 * Combined port of:
 *   hdlc.h
 *   private/hdlc.h (merged into the supplied hdlc.h)
 *   hdlc.c
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2003 Steve Underwood.
 *
 * This port preserves the LGPL-2.1 licensing terms of the original files.
 */

#nullable enable

namespace TKFaxEngine.Modem;

/// <summary>
/// Receives a complete HDLC frame or a negative <see cref="SignalStatus"/> value.
/// For a status notification, <paramref name="packet"/> is <see langword="null"/>
/// and <paramref name="lengthOrStatus"/> contains the negative status value.
/// </summary>
public delegate void HdlcFrameHandler(
    object? userData,
    ReadOnlyMemory<byte>? packet,
    int lengthOrStatus,
    bool ok);

/// <summary>
/// Called when the HDLC transmitter requires more data.
/// </summary>
public delegate void HdlcUnderflowHandler(object? userData);

/// <summary>
/// Managed equivalent of <c>hdlc_rx_stats_t</c>.
/// </summary>
public sealed class HdlcReceiveStatistics {
    /// <summary>
    /// Gets or sets the <c>Bytes</c> value.
    /// </summary>
    public ulong Bytes { get; set; }

    /// <summary>
    /// Gets or sets the <c>GoodFrames</c> value.
    /// </summary>
    public ulong GoodFrames { get; set; }

    /// <summary>
    /// Gets or sets the <c>CrcErrors</c> value.
    /// </summary>
    public ulong CrcErrors { get; set; }

    /// <summary>
    /// Gets or sets the <c>LengthErrors</c> value.
    /// </summary>
    public ulong LengthErrors { get; set; }

    /// <summary>
    /// Gets or sets the <c>Aborts</c> value.
    /// </summary>
    public ulong Aborts { get; set; }

    internal void CopyFrom(HdlcReceiver receiver) {
        Bytes = receiver.ReceivedBytes;
        GoodFrames = receiver.ReceivedFrames;
        CrcErrors = receiver.ReceivedCrcErrors;
        LengthErrors = receiver.ReceivedLengthErrors;
        Aborts = receiver.ReceivedAborts;
    }
}

/// <summary>
/// HDLC receiver with bit de-stuffing, flag/abort recognition and CRC checking.
/// Managed equivalent of <c>hdlc_rx_state_t</c> and the receive side of
/// <c>hdlc.c</c>.
/// </summary>
public sealed class HdlcReceiver : IDisposable {
    /// <summary>
    /// Defines the <c>MaximumFrameLength</c> constant.
    /// </summary>
    public const int MaximumFrameLength = 400;
    /// <summary>
    /// Defines the <c>BufferLength</c> constant.
    /// </summary>
    public const int BufferLength = MaximumFrameLength + 4;

    private bool _disposed;

    internal HdlcReceiver() {
    }

    /// <summary>
    /// Initializes a new instance of the <c>HdlcReceiver</c> type.
    /// </summary>
    /// <param name="crc32">The crc32 value.</param>
    /// <param name="reportBadFrames">The report bad frames value.</param>
    /// <param name="framingOkThreshold">The framing ok threshold value.</param>
    /// <param name="frameHandler">The frame handler value.</param>
    /// <param name="userData">The user data value.</param>
    public HdlcReceiver(
        bool crc32,
        bool reportBadFrames,
        int framingOkThreshold,
        HdlcFrameHandler? frameHandler,
        object? userData = null) {
        Initialize(
            crc32,
            reportBadFrames,
            framingOkThreshold,
            frameHandler,
            userData);
    }

    /// <summary>2 for CRC-16, 4 for CRC-32.</summary>
    public int CrcBytes { get; private set; }

    /// <summary>
    /// Internal maximum length including the CRC bytes, matching
    /// <c>hdlc_rx_state_t.max_frame_len</c>.
    /// </summary>
    public int MaxFrameLength { get; private set; }

    /// <summary>
    /// Gets or sets the <c>FrameHandler</c> value.
    /// </summary>
    public HdlcFrameHandler? FrameHandler { get; private set; }

    /// <summary>
    /// Gets or sets the <c>FrameUserData</c> value.
    /// </summary>
    public object? FrameUserData { get; private set; }

    /// <summary>
    /// Gets or sets the <c>StatusHandler</c> value.
    /// </summary>
    public SpanModemStatusDelegate? StatusHandler { get; private set; }

    /// <summary>
    /// Gets or sets the <c>StatusUserData</c> value.
    /// </summary>
    public object? StatusUserData { get; private set; }

    /// <summary>
    /// Gets or sets the <c>ReportBadFrames</c> value.
    /// </summary>
    public bool ReportBadFrames { get; private set; }

    /// <summary>
    /// Gets or sets the <c>FramingOkThreshold</c> value.
    /// </summary>
    public int FramingOkThreshold { get; private set; }

    /// <summary>
    /// Gets or sets the <c>FramingOkAnnounced</c> value.
    /// </summary>
    public bool FramingOkAnnounced { get; set; }

    /// <summary>
    /// Gets or sets the <c>FlagsSeen</c> value.
    /// </summary>
    public int FlagsSeen { get; set; }

    /// <summary>
    /// Gets or sets the <c>RawBitStream</c> value.
    /// </summary>
    public uint RawBitStream { get; set; }

    /// <summary>
    /// Gets or sets the <c>ByteInProgress</c> value.
    /// </summary>
    public uint ByteInProgress { get; set; }

    /// <summary>
    /// Gets or sets the <c>NumBits</c> value.
    /// </summary>
    public int NumBits { get; set; }

    /// <summary>
    /// Gets or sets the <c>OctetCountingMode</c> value.
    /// </summary>
    public bool OctetCountingMode { get; set; }

    /// <summary>
    /// Gets or sets the <c>OctetCount</c> value.
    /// </summary>
    public int OctetCount { get; set; }

    /// <summary>
    /// Gets or sets the <c>OctetCountReportInterval</c> value.
    /// </summary>
    public int OctetCountReportInterval { get; set; }

    /// <summary>
    /// Gets the <c>Buffer</c> value.
    /// </summary>
    public byte[] Buffer { get; } = new byte[BufferLength];

    /// <summary>
    /// Gets or sets the <c>Length</c> value.
    /// </summary>
    public int Length { get; set; }

    /// <summary>
    /// Gets or sets the <c>ReceivedBytes</c> value.
    /// </summary>
    public ulong ReceivedBytes { get; private set; }

    /// <summary>
    /// Gets or sets the <c>ReceivedFrames</c> value.
    /// </summary>
    public ulong ReceivedFrames { get; private set; }

    /// <summary>
    /// Gets or sets the <c>ReceivedCrcErrors</c> value.
    /// </summary>
    public ulong ReceivedCrcErrors { get; private set; }

    /// <summary>
    /// Gets or sets the <c>ReceivedLengthErrors</c> value.
    /// </summary>
    public ulong ReceivedLengthErrors { get; private set; }

    /// <summary>
    /// Gets or sets the <c>ReceivedAborts</c> value.
    /// </summary>
    public ulong ReceivedAborts { get; private set; }

    /// <summary>
    /// Gets the <c>IsDisposed</c> value.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Performs the <c>Initialize</c> operation.
    /// </summary>
    /// <param name="crc32">The crc32 value.</param>
    /// <param name="reportBadFrames">The report bad frames value.</param>
    /// <param name="framingOkThreshold">The framing ok threshold value.</param>
    /// <param name="frameHandler">The frame handler value.</param>
    /// <param name="userData">The user data value.</param>
    public void Initialize(
        bool crc32,
        bool reportBadFrames,
        int framingOkThreshold,
        HdlcFrameHandler? frameHandler,
        object? userData = null) {
        CrcBytes = crc32 ? 4 : 2;
        MaxFrameLength = Buffer.Length;
        FrameHandler = frameHandler;
        FrameUserData = userData;
        StatusHandler = null;
        StatusUserData = null;
        ReportBadFrames = reportBadFrames;
        FramingOkThreshold = Math.Max(1, framingOkThreshold);

        FramingOkAnnounced = false;
        FlagsSeen = 0;
        RawBitStream = 0;
        ByteInProgress = 0;
        NumBits = 0;
        OctetCountingMode = false;
        OctetCount = 0;
        OctetCountReportInterval = 0;
        Length = 0;

        ReceivedBytes = 0;
        ReceivedFrames = 0;
        ReceivedCrcErrors = 0;
        ReceivedLengthErrors = 0;
        ReceivedAborts = 0;

        Buffer.AsSpan().Clear();
        _disposed = false;
    }

    /// <summary>
    /// Reinitializes the active receive state without resetting statistics,
    /// configuration or callbacks.
    /// </summary>
    public int Restart() {
        ThrowIfDisposed();

        FramingOkAnnounced = false;
        FlagsSeen = 0;
        RawBitStream = 0;
        ByteInProgress = 0;
        NumBits = 0;
        OctetCountingMode = false;
        OctetCount = 0;
        Length = 0;
        return 0;
    }

    /// <summary>
    /// Performs the <c>SetFrameHandler</c> operation.
    /// </summary>
    /// <param name="handler">The handler value.</param>
    /// <param name="userData">The user data value.</param>
    public void SetFrameHandler(
        HdlcFrameHandler? handler,
        object? userData) {
        ThrowIfDisposed();
        FrameHandler = handler;
        FrameUserData = userData;
    }

    /// <summary>
    /// Performs the <c>SetStatusHandler</c> operation.
    /// </summary>
    /// <param name="handler">The handler value.</param>
    /// <param name="userData">The user data value.</param>
    public void SetStatusHandler(
        SpanModemStatusDelegate? handler,
        object? userData) {
        ThrowIfDisposed();
        StatusHandler = handler;
        StatusUserData = userData;
    }

    /// <summary>
    /// Sets the maximum payload length. Internally the configured CRC length is
    /// added, exactly as in <c>hdlc_rx_set_max_frame_len()</c>.
    /// </summary>
    public void SetMaxFrameLength(int maxLength) {
        ThrowIfDisposed();

        if (maxLength < 0)
            throw new ArgumentOutOfRangeException(nameof(maxLength));

        long withCrc = (long)maxLength + CrcBytes;
        MaxFrameLength = (int)Math.Min(withCrc, Buffer.Length);
    }

    /// <summary>
    /// Performs the <c>SetOctetCountingReportInterval</c> operation.
    /// </summary>
    /// <param name="interval">The interval value.</param>
    public void SetOctetCountingReportInterval(int interval) {
        ThrowIfDisposed();
        OctetCountReportInterval = interval;
    }

    /// <summary>
    /// Performs the <c>GetStatistics</c> operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public HdlcReceiveStatistics GetStatistics() {
        ThrowIfDisposed();

        HdlcReceiveStatistics statistics = new();
        statistics.CopyFrom(this);
        return statistics;
    }

    /// <summary>
    /// Supplies one de-modulated bit or one negative signal status.
    /// </summary>
    public void PutBit(int newBit) {
        ThrowIfDisposed();

        if (newBit < 0) {
            ReceiveSpecialCondition(newBit);
            return;
        }

        RawBitStream = unchecked(
            (RawBitStream << 1) |
            ((uint)(newBit << 8) & 0x100u));

        PutBitCore();
    }

    /// <summary>
    /// Supplies one packed HDLC stream byte or one negative signal status.
    /// </summary>
    public void PutByte(int newByte) {
        ThrowIfDisposed();

        if (newByte < 0) {
            ReceiveSpecialCondition(newByte);
            return;
        }

        RawBitStream |= unchecked((uint)newByte);

        for (int index = 0; index < 8; index++) {
            RawBitStream = unchecked(RawBitStream << 1);
            PutBitCore();
        }
    }

    /// <summary>
    /// Performs the <c>Put</c> operation.
    /// </summary>
    /// <param name="buffer">The buffer value.</param>
    public void Put(ReadOnlySpan<byte> buffer) {
        ThrowIfDisposed();

        foreach (byte value in buffer)
            PutByte(value);
    }

    /// <summary>
    /// Matches <c>hdlc_rx_release()</c>. No unmanaged resource is held.
    /// </summary>
    public int Release() {
        return 0;
    }

    /// <summary>
    /// Performs the <c>Dispose</c> operation.
    /// </summary>
    public void Dispose() {
        if (_disposed)
            return;

        FrameHandler = null;
        FrameUserData = null;
        StatusHandler = null;
        StatusUserData = null;
        Length = 0;
        Buffer.AsSpan().Clear();
        _disposed = true;
    }

    private void ReportStatusChange(int status) {
        if (StatusHandler is not null) {
            StatusHandler(StatusUserData, status);
        } else {
            FrameHandler?.Invoke(
                FrameUserData,
                null,
                status,
                true);
        }
    }

    private void ReceiveSpecialCondition(int status) {
        switch ((SignalStatus)status) {
            case SignalStatus.CarrierUp:
            case SignalStatus.TrainingSucceeded:
                RawBitStream = 0;
                Length = 0;
                NumBits = 0;
                FlagsSeen = 0;
                FramingOkAnnounced = false;
                ReportStatusChange(status);
                break;

            case SignalStatus.TrainingInProgress:
            case SignalStatus.TrainingFailed:
            case SignalStatus.CarrierDown:
            case SignalStatus.EndOfData:
                ReportStatusChange(status);
                break;
        }
    }

    private void SetAndCountOctet() {
        if (OctetCountReportInterval == 0)
            return;

        if (OctetCountingMode) {
            OctetCount--;
            if (OctetCount <= 0) {
                OctetCount = OctetCountReportInterval;
                ReportStatusChange((int)SignalStatus.OctetReport);
            }
        } else {
            OctetCountingMode = true;
            OctetCount = OctetCountReportInterval;
        }
    }

    private void CountOctet() {
        if (OctetCountReportInterval == 0)
            return;

        if (!OctetCountingMode)
            return;

        OctetCount--;
        if (OctetCount <= 0) {
            OctetCount = OctetCountReportInterval;
            ReportStatusChange((int)SignalStatus.OctetReport);
        }
    }

    private void ProcessFlagOrAbort() {
        if ((RawBitStream & 0x0100u) != 0) {
            ReceivedAborts = unchecked(ReceivedAborts + 1);
            ReportStatusChange((int)SignalStatus.Abort);

            if (FlagsSeen < FramingOkThreshold - 1)
                FlagsSeen = 0;
            else
                FlagsSeen = FramingOkThreshold - 1;

            SetAndCountOctet();
        } else {
            OctetCountingMode = false;

            if (FlagsSeen >= FramingOkThreshold) {
                if (Length != 0)
                    CompleteFrame();
            } else {
                if (FlagsSeen != FramingOkThreshold - 1 && NumBits != 7) {
                    if (FlagsSeen < FramingOkThreshold - 1)
                        FlagsSeen = 0;
                    else
                        FlagsSeen = FramingOkThreshold - 1;
                }

                FlagsSeen++;
                if (FlagsSeen >= FramingOkThreshold && !FramingOkAnnounced) {
                    ReportStatusChange((int)SignalStatus.FramingOk);
                    FramingOkAnnounced = true;
                }
            }
        }

        Length = 0;
        NumBits = 0;
    }

    private void CompleteFrame() {
        bool frameShapeIsValid =
            NumBits == 7 &&
            Length >= CrcBytes &&
            Length <= MaxFrameLength;

        if (frameShapeIsValid) {
            bool crcIsValid = CrcBytes == 2
                ? CrcItu16.Check(Buffer.AsSpan(0, Length))
                : CrcItu32.Check(Buffer.AsSpan(0, Length));

            if (crcIsValid) {
                int payloadLength = Length - CrcBytes;
                ReceivedFrames = unchecked(ReceivedFrames + 1);
                ReceivedBytes = unchecked(ReceivedBytes + (ulong)payloadLength);

                FrameHandler?.Invoke(
                    FrameUserData,
                    new ReadOnlyMemory<byte>(Buffer, 0, payloadLength),
                    payloadLength,
                    true);
            } else {
                ReceivedCrcErrors = unchecked(ReceivedCrcErrors + 1);

                if (ReportBadFrames) {
                    int payloadLength = Length - CrcBytes;
                    FrameHandler?.Invoke(
                        FrameUserData,
                        new ReadOnlyMemory<byte>(Buffer, 0, payloadLength),
                        payloadLength,
                        false);
                }
            }

            return;
        }

        if (ReportBadFrames) {
            int payloadLength = Length >= CrcBytes
                ? Length - CrcBytes
                : 0;

            int availableLength = Math.Min(payloadLength, Buffer.Length);
            FrameHandler?.Invoke(
                FrameUserData,
                new ReadOnlyMemory<byte>(Buffer, 0, availableLength),
                payloadLength,
                false);
        }

        ReceivedLengthErrors = unchecked(ReceivedLengthErrors + 1);
    }

    private void PutBitCore() {
        if ((RawBitStream & 0x3E00u) == 0x3E00u) {
            if ((RawBitStream & 0x4100u) == 0)
                return;

            if ((RawBitStream & 0xFE00u) == 0x7E00u) {
                ProcessFlagOrAbort();
                return;
            }
        }

        NumBits++;

        if (FlagsSeen < FramingOkThreshold) {
            if ((NumBits & 0x7) == 0)
                CountOctet();

            return;
        }

        ByteInProgress =
            (ByteInProgress | (RawBitStream & 0x100u)) >> 1;

        if (NumBits != 8)
            return;

        if (Length < MaxFrameLength) {
            Buffer[Length++] = unchecked((byte)ByteInProgress);
        } else {
            Length = Buffer.Length + 1;
            FlagsSeen = FramingOkThreshold - 1;
            SetAndCountOctet();
        }

        NumBits = 0;
    }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

/// <summary>
/// HDLC transmitter with CRC generation, bit stuffing and flag insertion.
/// Managed equivalent of <c>hdlc_tx_state_t</c> and the transmit side of
/// <c>hdlc.c</c>.
/// </summary>
public sealed class HdlcTransmitter : IDisposable {
    /// <summary>
    /// Defines the <c>MaximumFrameLength</c> constant.
    /// </summary>
    public const int MaximumFrameLength = 400;
    /// <summary>
    /// Defines the <c>BufferLength</c> constant.
    /// </summary>
    public const int BufferLength = MaximumFrameLength + 4;

    private bool _disposed;
    private bool _crcPrepared;

    internal HdlcTransmitter() {
    }

    /// <summary>
    /// Initializes a new instance of the <c>HdlcTransmitter</c> type.
    /// </summary>
    /// <param name="crc32">The crc32 value.</param>
    /// <param name="interFrameFlags">The inter frame flags value.</param>
    /// <param name="progressive">The progressive value.</param>
    /// <param name="underflowHandler">The underflow handler value.</param>
    /// <param name="userData">The user data value.</param>
    public HdlcTransmitter(
        bool crc32,
        int interFrameFlags,
        bool progressive,
        HdlcUnderflowHandler? underflowHandler,
        object? userData = null) {
        Initialize(
            crc32,
            interFrameFlags,
            progressive,
            underflowHandler,
            userData);
    }

    /// <summary>2 for CRC-16, 4 for CRC-32.</summary>
    public int CrcBytes { get; private set; }

    /// <summary>
    /// Gets or sets the <c>UnderflowHandler</c> value.
    /// </summary>
    public HdlcUnderflowHandler? UnderflowHandler { get; private set; }

    /// <summary>
    /// Gets or sets the <c>UserData</c> value.
    /// </summary>
    public object? UserData { get; private set; }

    /// <summary>
    /// Gets or sets the <c>InterFrameFlags</c> value.
    /// </summary>
    public int InterFrameFlags { get; private set; }

    /// <summary>
    /// Gets or sets the <c>Progressive</c> value.
    /// </summary>
    public bool Progressive { get; private set; }

    /// <summary>
    /// Gets or sets the <c>MaxFrameLength</c> value.
    /// </summary>
    public int MaxFrameLength { get; private set; }

    /// <summary>
    /// Gets or sets the <c>OctetsInProgress</c> value.
    /// </summary>
    public uint OctetsInProgress { get; set; }

    /// <summary>
    /// Gets or sets the <c>NumBits</c> value.
    /// </summary>
    public int NumBits { get; set; }

    /// <summary>
    /// Gets or sets the <c>IdleOctet</c> value.
    /// </summary>
    public int IdleOctet { get; set; }

    /// <summary>
    /// Gets or sets the <c>FlagOctets</c> value.
    /// </summary>
    public int FlagOctets { get; set; }

    /// <summary>
    /// Gets or sets the <c>AbortOctets</c> value.
    /// </summary>
    public int AbortOctets { get; set; }

    /// <summary>
    /// Gets or sets the <c>ReportFlagUnderflow</c> value.
    /// </summary>
    public bool ReportFlagUnderflow { get; set; }

    /// <summary>
    /// Gets the <c>Buffer</c> value.
    /// </summary>
    public byte[] Buffer { get; } = new byte[BufferLength];

    /// <summary>
    /// Gets or sets the <c>Length</c> value.
    /// </summary>
    public int Length { get; set; }

    /// <summary>
    /// Gets or sets the <c>Position</c> value.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets the <c>Crc</c> value.
    /// </summary>
    public uint Crc { get; set; }

    /// <summary>
    /// Gets or sets the <c>Byte</c> value.
    /// </summary>
    public int Byte { get; set; }

    /// <summary>
    /// Gets or sets the <c>Bits</c> value.
    /// </summary>
    public int Bits { get; set; }

    /// <summary>
    /// Gets or sets the <c>TxEnd</c> value.
    /// </summary>
    public bool TxEnd { get; set; }

    /// <summary>
    /// Gets the <c>IsDisposed</c> value.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Performs the <c>Initialize</c> operation.
    /// </summary>
    /// <param name="crc32">The crc32 value.</param>
    /// <param name="interFrameFlags">The inter frame flags value.</param>
    /// <param name="progressive">The progressive value.</param>
    /// <param name="underflowHandler">The underflow handler value.</param>
    /// <param name="userData">The user data value.</param>
    public void Initialize(
        bool crc32,
        int interFrameFlags,
        bool progressive,
        HdlcUnderflowHandler? underflowHandler,
        object? userData = null) {
        CrcBytes = crc32 ? 4 : 2;
        UnderflowHandler = underflowHandler;
        UserData = userData;
        InterFrameFlags = Math.Max(1, interFrameFlags);
        Progressive = progressive;
        MaxFrameLength = MaximumFrameLength;

        OctetsInProgress = 0;
        NumBits = 0;
        IdleOctet = 0x7E;
        FlagOctets = 0;
        AbortOctets = 0;
        ReportFlagUnderflow = false;
        Length = 0;
        Position = 0;
        Crc = crc32
            ? CrcItu32.InitialValue
            : CrcItu16.InitialValue;
        Byte = 0;
        Bits = 0;
        TxEnd = false;

        _crcPrepared = false;
        Buffer.AsSpan().Clear();
        _disposed = false;
    }

    /// <summary>
    /// Reinitializes the active transmit state without changing callbacks or
    /// configuration.
    /// </summary>
    public int Restart() {
        ThrowIfDisposed();

        OctetsInProgress = 0;
        NumBits = 0;
        IdleOctet = 0x7E;
        FlagOctets = 0;
        AbortOctets = 0;
        ReportFlagUnderflow = false;
        Length = 0;
        Position = 0;
        Crc = CrcBytes == 2
            ? CrcItu16.InitialValue
            : CrcItu32.InitialValue;
        Byte = 0;
        Bits = 0;
        TxEnd = false;
        _crcPrepared = false;
        return 0;
    }

    /// <summary>
    /// Performs the <c>SetMaxFrameLength</c> operation.
    /// </summary>
    /// <param name="maxLength">The max length value.</param>
    public void SetMaxFrameLength(int maxLength) {
        ThrowIfDisposed();

        if (maxLength < 0)
            throw new ArgumentOutOfRangeException(nameof(maxLength));

        MaxFrameLength = Math.Min(maxLength, MaximumFrameLength);
    }

    /// <summary>
    /// Queues a frame or appends a progressive frame segment. An empty segment
    /// requests end-of-data after all queued output has drained.
    /// </summary>
    public int Frame(ReadOnlySpan<byte> frame) {
        ThrowIfDisposed();

        if (frame.IsEmpty) {
            TxEnd = true;
            return 0;
        }

        if ((long)Length + frame.Length > MaxFrameLength)
            return -1;

        if (Progressive) {
            if (Position >= MaximumFrameLength)
                return -1;
        } else if (Length != 0) {
            return -1;
        }

        frame.CopyTo(Buffer.AsSpan(Length));

        Crc = CrcBytes == 2
            ? CrcItu16.Calculate(frame, unchecked((ushort)Crc))
            : CrcItu32.Calculate(frame, Crc);

        if (Progressive)
            Length += frame.Length;
        else
            Length = frame.Length;

        _crcPrepared = false;
        TxEnd = false;
        return 0;
    }

    /// <summary>
    /// Forces a timed sequence of flag octets. A negative value adds flags to
    /// the existing count; a non-negative value replaces the count.
    /// </summary>
    public int Flags(int length) {
        ThrowIfDisposed();

        if (Position != 0)
            return -1;

        if (length < 0)
            FlagOctets = unchecked(FlagOctets - length);
        else
            FlagOctets = length;

        ReportFlagUnderflow = true;
        TxEnd = false;
        return 0;
    }

    /// <summary>
    /// Emits the same crude abort sequence as the native implementation.
    /// The original function returns -1 after scheduling the abort.
    /// </summary>
    public int Abort() {
        ThrowIfDisposed();
        FlagOctets = unchecked(FlagOctets + 1);
        AbortOctets = unchecked(AbortOctets + 1);
        return -1;
    }

    /// <summary>
    /// Performs the <c>CorruptFrame</c> operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public int CorruptFrame() {
        ThrowIfDisposed();

        if (Length <= 0)
            return -1;

        Crc ^= 0xFFFFu;
        Buffer[MaximumFrameLength] ^= 0xFF;
        Buffer[MaximumFrameLength + 1] ^= 0xFF;
        Buffer[MaximumFrameLength + 2] ^= 0xFF;
        Buffer[MaximumFrameLength + 3] ^= 0xFF;
        return 0;
    }

    /// <summary>
    /// Returns the next packed HDLC stream byte or a negative
    /// <see cref="SignalStatus"/> value.
    /// </summary>
    public int GetByte() {
        ThrowIfDisposed();

        if (FlagOctets > 0) {
            FlagOctets--;

            if (FlagOctets <= 0 && ReportFlagUnderflow) {
                ReportFlagUnderflow = false;

                if (Length == 0)
                    UnderflowHandler?.Invoke(UserData);
            }

            if (AbortOctets != 0) {
                AbortOctets = 0;
                return 0x7F;
            }

            return IdleOctet;
        }

        if (Length != 0) {
            if (NumBits >= 8) {
                NumBits -= 8;
                return unchecked((int)((OctetsInProgress >> NumBits) & 0xFFu));
            }

            if (Position >= Length) {
                if (!_crcPrepared && Position == Length) {
                    PrepareCrcBytes();
                } else if (Position == MaximumFrameLength + CrcBytes) {
                    int txByte = unchecked((byte)(
                        (OctetsInProgress << (8 - NumBits)) |
                        (0x7Eu >> NumBits)));

                    IdleOctet = unchecked((int)((0x7E7Eu >> NumBits) & 0xFFu));
                    OctetsInProgress = unchecked(
                        (uint)IdleOctet >> (8 - NumBits));
                    FlagOctets = InterFrameFlags - 1;
                    Length = 0;
                    Position = 0;
                    Crc = CrcBytes == 2
                        ? CrcItu16.InitialValue
                        : CrcItu32.InitialValue;
                    _crcPrepared = false;

                    ReportFlagUnderflow = false;
                    UnderflowHandler?.Invoke(UserData);

                    if (Length == 0 && FlagOctets < 2)
                        FlagOctets = 2;

                    return txByte;
                }
            }

            int byteInProgress = Buffer[Position++];
            int firstBit = BitOperationsEx.BottomBit(
                unchecked((uint)(byteInProgress | 0x100)));

            OctetsInProgress = unchecked(OctetsInProgress << firstBit);
            byteInProgress >>= firstBit;

            for (int bit = firstBit; bit < 8; bit++) {
                OctetsInProgress = unchecked(
                    (OctetsInProgress << 1) |
                    (uint)(byteInProgress & 0x01));
                byteInProgress >>= 1;

                if ((OctetsInProgress & 0x1Fu) == 0x1Fu) {
                    OctetsInProgress = unchecked(OctetsInProgress << 1);
                    NumBits++;
                }
            }

            return unchecked((int)((OctetsInProgress >> NumBits) & 0xFFu));
        }

        if (TxEnd) {
            TxEnd = false;
            return (int)SignalStatus.EndOfData;
        }

        return IdleOctet;
    }

    /// <summary>
    /// Performs the <c>GetBit</c> operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public int GetBit() {
        ThrowIfDisposed();

        if (Bits == 0) {
            Byte = GetByte();
            if (Byte < 0)
                return Byte;

            Bits = 8;
        }

        Bits--;
        return (Byte >> Bits) & 0x01;
    }

    /// <summary>
    /// Performs the <c>Get</c> operation.
    /// </summary>
    /// <param name="buffer">The buffer value.</param>
    /// <returns>The result of the operation.</returns>
    public int Get(Span<byte> buffer) {
        ThrowIfDisposed();

        int index;
        for (index = 0; index < buffer.Length; index++) {
            int value = GetByte();
            if (value == (int)SignalStatus.EndOfData)
                return index;

            buffer[index] = unchecked((byte)value);
        }

        return index;
    }

    /// <summary>
    /// Matches <c>hdlc_tx_release()</c>. No unmanaged resource is held.
    /// </summary>
    public int Release() {
        return 0;
    }

    /// <summary>
    /// Performs the <c>Dispose</c> operation.
    /// </summary>
    public void Dispose() {
        if (_disposed)
            return;

        UnderflowHandler = null;
        UserData = null;
        Length = 0;
        Position = 0;
        Buffer.AsSpan().Clear();
        _disposed = true;
    }

    private void PrepareCrcBytes() {
        Crc ^= 0xFFFFFFFFu;
        Buffer[MaximumFrameLength] = unchecked((byte)Crc);
        Buffer[MaximumFrameLength + 1] = unchecked((byte)(Crc >> 8));

        if (CrcBytes == 4) {
            Buffer[MaximumFrameLength + 2] = unchecked((byte)(Crc >> 16));
            Buffer[MaximumFrameLength + 3] = unchecked((byte)(Crc >> 24));
        }

        Position = MaximumFrameLength;
        _crcPrepared = true;
    }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

/// <summary>
/// Compatibility facade retaining the original C function names.
/// </summary>
public static class HdlcApi {
    /// <summary>
    /// Defines the <c>HDLC_MAXFRAME_LEN</c> constant.
    /// </summary>
    public const int HDLC_MAXFRAME_LEN = HdlcTransmitter.MaximumFrameLength;

    /// <summary>
    /// Performs the <c>hdlc_rx_init</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="crc32">The crc32 value.</param>
    /// <param name="reportBadFrames">The report bad frames value.</param>
    /// <param name="framingOkThreshold">The framing ok threshold value.</param>
    /// <param name="handler">The handler value.</param>
    /// <param name="userData">The user data value.</param>
    /// <returns>The result of the operation.</returns>
    public static HdlcReceiver hdlc_rx_init(
        HdlcReceiver? state,
        bool crc32,
        bool reportBadFrames,
        int framingOkThreshold,
        HdlcFrameHandler? handler,
        object? userData) {
        state ??= new HdlcReceiver();
        state.Initialize(
            crc32,
            reportBadFrames,
            framingOkThreshold,
            handler,
            userData);
        return state;
    }

    /// <summary>
    /// Performs the <c>hdlc_rx_restart</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int hdlc_rx_restart(HdlcReceiver state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Restart();
    }

    /// <summary>
    /// Performs the <c>hdlc_rx_set_frame_handler</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="handler">The handler value.</param>
    /// <param name="userData">The user data value.</param>
    public static void hdlc_rx_set_frame_handler(
        HdlcReceiver state,
        HdlcFrameHandler? handler,
        object? userData) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetFrameHandler(handler, userData);
    }

    /// <summary>
    /// Performs the <c>hdlc_rx_set_status_handler</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="handler">The handler value.</param>
    /// <param name="userData">The user data value.</param>
    public static void hdlc_rx_set_status_handler(
        HdlcReceiver state,
        SpanModemStatusDelegate? handler,
        object? userData) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetStatusHandler(handler, userData);
    }

    /// <summary>
    /// Performs the <c>hdlc_rx_release</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int hdlc_rx_release(HdlcReceiver state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    /// <summary>
    /// Performs the <c>hdlc_rx_free</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int hdlc_rx_free(HdlcReceiver? state) {
        state?.Dispose();
        return 0;
    }

    /// <summary>
    /// Performs the <c>hdlc_rx_set_max_frame_len</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="maxLength">The max length value.</param>
    public static void hdlc_rx_set_max_frame_len(
        HdlcReceiver state,
        int maxLength) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetMaxFrameLength(maxLength);
    }

    /// <summary>
    /// Performs the <c>hdlc_rx_set_octet_counting_report_interval</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="interval">The interval value.</param>
    public static void hdlc_rx_set_octet_counting_report_interval(
        HdlcReceiver state,
        int interval) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetOctetCountingReportInterval(interval);
    }

    /// <summary>
    /// Performs the <c>hdlc_rx_get_stats</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="statistics">The statistics value.</param>
    /// <returns>The result of the operation.</returns>
    public static int hdlc_rx_get_stats(
        HdlcReceiver state,
        HdlcReceiveStatistics statistics) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(statistics);
        statistics.CopyFrom(state);
        return 0;
    }

    /// <summary>
    /// Performs the <c>hdlc_rx_get_stats</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static HdlcReceiveStatistics hdlc_rx_get_stats(
        HdlcReceiver state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetStatistics();
    }

    /// <summary>
    /// Performs the <c>hdlc_rx_put_bit</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="newBit">The new bit value.</param>
    public static void hdlc_rx_put_bit(
        HdlcReceiver state,
        int newBit) {
        ArgumentNullException.ThrowIfNull(state);
        state.PutBit(newBit);
    }

    /// <summary>
    /// Performs the <c>hdlc_rx_put_byte</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="newByte">The new byte value.</param>
    public static void hdlc_rx_put_byte(
        HdlcReceiver state,
        int newByte) {
        ArgumentNullException.ThrowIfNull(state);
        state.PutByte(newByte);
    }

    /// <summary>
    /// Performs the <c>hdlc_rx_put</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="buffer">The buffer value.</param>
    /// <param name="length">The length value.</param>
    public static void hdlc_rx_put(
        HdlcReceiver state,
        ReadOnlySpan<byte> buffer,
        int length) {
        ArgumentNullException.ThrowIfNull(state);
        ValidateLength(buffer.Length, length);
        state.Put(buffer[..length]);
    }

    /// <summary>
    /// Performs the <c>hdlc_rx_put</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="buffer">The buffer value.</param>
    public static void hdlc_rx_put(
        HdlcReceiver state,
        ReadOnlySpan<byte> buffer) {
        ArgumentNullException.ThrowIfNull(state);
        state.Put(buffer);
    }

    /// <summary>
    /// Performs the <c>hdlc_tx_init</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="crc32">The crc32 value.</param>
    /// <param name="interFrameFlags">The inter frame flags value.</param>
    /// <param name="progressive">The progressive value.</param>
    /// <param name="handler">The handler value.</param>
    /// <param name="userData">The user data value.</param>
    /// <returns>The result of the operation.</returns>
    public static HdlcTransmitter hdlc_tx_init(
        HdlcTransmitter? state,
        bool crc32,
        int interFrameFlags,
        bool progressive,
        HdlcUnderflowHandler? handler,
        object? userData) {
        state ??= new HdlcTransmitter();
        state.Initialize(
            crc32,
            interFrameFlags,
            progressive,
            handler,
            userData);
        return state;
    }

    /// <summary>
    /// Performs the <c>hdlc_tx_restart</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int hdlc_tx_restart(HdlcTransmitter state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Restart();
    }

    /// <summary>
    /// Performs the <c>hdlc_tx_release</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int hdlc_tx_release(HdlcTransmitter state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    /// <summary>
    /// Performs the <c>hdlc_tx_free</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int hdlc_tx_free(HdlcTransmitter? state) {
        state?.Dispose();
        return 0;
    }

    /// <summary>
    /// Performs the <c>hdlc_tx_set_max_frame_len</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="maxLength">The max length value.</param>
    public static void hdlc_tx_set_max_frame_len(
        HdlcTransmitter state,
        int maxLength) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetMaxFrameLength(maxLength);
    }

    /// <summary>
    /// Performs the <c>hdlc_tx_frame</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="frame">The frame value.</param>
    /// <param name="length">The length value.</param>
    /// <returns>The result of the operation.</returns>
    public static int hdlc_tx_frame(
        HdlcTransmitter state,
        ReadOnlySpan<byte> frame,
        int length) {
        ArgumentNullException.ThrowIfNull(state);
        ValidateLength(frame.Length, length);
        return state.Frame(frame[..length]);
    }

    /// <summary>
    /// Performs the <c>hdlc_tx_frame</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="frame">The frame value.</param>
    /// <returns>The result of the operation.</returns>
    public static int hdlc_tx_frame(
        HdlcTransmitter state,
        ReadOnlySpan<byte> frame) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Frame(frame);
    }

    /// <summary>
    /// Performs the <c>hdlc_tx_corrupt_frame</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int hdlc_tx_corrupt_frame(HdlcTransmitter state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.CorruptFrame();
    }

    /// <summary>
    /// Performs the <c>hdlc_tx_flags</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="length">The length value.</param>
    /// <returns>The result of the operation.</returns>
    public static int hdlc_tx_flags(
        HdlcTransmitter state,
        int length) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Flags(length);
    }

    /// <summary>
    /// Performs the <c>hdlc_tx_abort</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int hdlc_tx_abort(HdlcTransmitter state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Abort();
    }

    /// <summary>
    /// Performs the <c>hdlc_tx_get_bit</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int hdlc_tx_get_bit(HdlcTransmitter state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetBit();
    }

    /// <summary>
    /// Performs the <c>hdlc_tx_get_byte</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int hdlc_tx_get_byte(HdlcTransmitter state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetByte();
    }

    /// <summary>
    /// Performs the <c>hdlc_tx_get</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="buffer">The buffer value.</param>
    /// <param name="maxLength">The max length value.</param>
    /// <returns>The result of the operation.</returns>
    public static int hdlc_tx_get(
        HdlcTransmitter state,
        Span<byte> buffer,
        int maxLength) {
        ArgumentNullException.ThrowIfNull(state);
        ValidateLength(buffer.Length, maxLength);
        return state.Get(buffer[..maxLength]);
    }

    /// <summary>
    /// Performs the <c>hdlc_tx_get</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="buffer">The buffer value.</param>
    /// <returns>The result of the operation.</returns>
    public static int hdlc_tx_get(
        HdlcTransmitter state,
        Span<byte> buffer) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Get(buffer);
    }

    private static void ValidateLength(int available, int length) {
        if (length < 0 || length > available)
            throw new ArgumentOutOfRangeException(nameof(length));
    }
}
