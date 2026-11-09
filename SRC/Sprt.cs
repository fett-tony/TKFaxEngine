/*
 * TKFaxEngine - managed C# port
 *
 * Sprt.cs
 *
 * Combined port of:
 *   sprt.h
 *   private/sprt.h
 *   sprt.c
 *
 * SPRT implements ITU-T V.150.1 Annex B, excluding the external packet
 * exchange mechanism.
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2022 Steve Underwood.
 *
 * IMPORTANT: The supplied SPRT source is licensed under GPL version 2,
 * not LGPL. This managed port retains that GPL-2.0 licensing requirement.
 */

namespace TKFaxEngine;

/// <summary>
/// Represents the <c>SprtConstants</c> type.
/// </summary>
public static class SprtConstants {
    /// <summary>
    /// Defines the <c>MinTc0PayloadBytes</c> constant.
    /// </summary>
    public const int MinTc0PayloadBytes = 140;
    /// <summary>
    /// Defines the <c>MaxTc0PayloadBytes</c> constant.
    /// </summary>
    public const int MaxTc0PayloadBytes = 256;
    /// <summary>
    /// Defines the <c>DefaultTc0PayloadBytes</c> constant.
    /// </summary>
    public const int DefaultTc0PayloadBytes = 140;

    /// <summary>
    /// Defines the <c>MinTc1PayloadBytes</c> constant.
    /// </summary>
    public const int MinTc1PayloadBytes = 132;
    /// <summary>
    /// Defines the <c>MaxTc1PayloadBytes</c> constant.
    /// </summary>
    public const int MaxTc1PayloadBytes = 256;
    /// <summary>
    /// Defines the <c>DefaultTc1PayloadBytes</c> constant.
    /// </summary>
    public const int DefaultTc1PayloadBytes = 132;
    /// <summary>
    /// Defines the <c>MinTc1WindowSize</c> constant.
    /// </summary>
    public const int MinTc1WindowSize = 32;
    /// <summary>
    /// Defines the <c>MaxTc1WindowSize</c> constant.
    /// </summary>
    public const int MaxTc1WindowSize = 96;
    /// <summary>
    /// Defines the <c>DefaultTc1WindowSize</c> constant.
    /// </summary>
    public const int DefaultTc1WindowSize = 32;

    /// <summary>
    /// Defines the <c>MinTc2PayloadBytes</c> constant.
    /// </summary>
    public const int MinTc2PayloadBytes = 132;
    /// <summary>
    /// Defines the <c>MaxTc2PayloadBytes</c> constant.
    /// </summary>
    public const int MaxTc2PayloadBytes = 256;
    /// <summary>
    /// Defines the <c>DefaultTc2PayloadBytes</c> constant.
    /// </summary>
    public const int DefaultTc2PayloadBytes = 132;
    /// <summary>
    /// Defines the <c>MinTc2WindowSize</c> constant.
    /// </summary>
    public const int MinTc2WindowSize = 8;
    /// <summary>
    /// Defines the <c>MaxTc2WindowSize</c> constant.
    /// </summary>
    public const int MaxTc2WindowSize = 32;
    /// <summary>
    /// Defines the <c>DefaultTc2WindowSize</c> constant.
    /// </summary>
    public const int DefaultTc2WindowSize = 8;

    /// <summary>
    /// Defines the <c>MinTc3PayloadBytes</c> constant.
    /// </summary>
    public const int MinTc3PayloadBytes = 140;
    /// <summary>
    /// Defines the <c>MaxTc3PayloadBytes</c> constant.
    /// </summary>
    public const int MaxTc3PayloadBytes = 256;
    /// <summary>
    /// Defines the <c>DefaultTc3PayloadBytes</c> constant.
    /// </summary>
    public const int DefaultTc3PayloadBytes = 140;

    /// <summary>
    /// Defines the <c>MaximumWindowSize</c> constant.
    /// </summary>
    public const int MaximumWindowSize = 96;
    /// <summary>
    /// Defines the <c>ChannelCount</c> constant.
    /// </summary>
    public const int ChannelCount = 4;

    /// <summary>
    /// Defines the <c>DefaultTimerTc1Ta01</c> constant.
    /// </summary>
    public const int DefaultTimerTc1Ta01 = 90_000;
    /// <summary>
    /// Defines the <c>DefaultTimerTc1Ta02</c> constant.
    /// </summary>
    public const int DefaultTimerTc1Ta02 = 130_000;
    /// <summary>
    /// Defines the <c>DefaultTimerTc1Tr03</c> constant.
    /// </summary>
    public const int DefaultTimerTc1Tr03 = 500_000;

    /// <summary>
    /// Defines the <c>DefaultTimerTc2Ta01</c> constant.
    /// </summary>
    public const int DefaultTimerTc2Ta01 = 90_000;
    /// <summary>
    /// Defines the <c>DefaultTimerTc2Ta02</c> constant.
    /// </summary>
    public const int DefaultTimerTc2Ta02 = 500_000;
    /// <summary>
    /// Defines the <c>DefaultTimerTc2Tr03</c> constant.
    /// </summary>
    public const int DefaultTimerTc2Tr03 = 500_000;

    /// <summary>
    /// Defines the <c>MinimumMaximumTries</c> constant.
    /// </summary>
    public const int MinimumMaximumTries = 1;
    /// <summary>
    /// Defines the <c>MaximumMaximumTries</c> constant.
    /// </summary>
    public const int MaximumMaximumTries = 20;
    /// <summary>
    /// Defines the <c>DefaultMaximumTries</c> constant.
    /// </summary>
    public const int DefaultMaximumTries = 10;

    /// <summary>
    /// Defines the <c>MaximumPacketBytes</c> constant.
    /// </summary>
    public const int MaximumPacketBytes = 12 + 256;
    /// <summary>
    /// Defines the <c>SequenceNumberMask</c> constant.
    /// </summary>
    public const ushort SequenceNumberMask = 0x3FFF;
    /// <summary>
    /// Defines the <c>FreeLengthSlot</c> constant.
    /// </summary>
    public const ushort FreeLengthSlot = 0xFFFF;
    /// <summary>
    /// Defines the <c>FreeTimerQueueSlot</c> constant.
    /// </summary>
    public const byte FreeTimerQueueSlot = 0xFF;
}

/// <summary>
/// Defines the available <c>SprtStatus</c> values.
/// </summary>
public enum SprtStatus {
    /// <summary>
    /// Represents the <c>Ok</c> value.
    /// </summary>
    Ok = 0,
    /// <summary>
    /// Represents the <c>ExcessRetries</c> value.
    /// </summary>
    ExcessRetries = 1,
    /// <summary>
    /// Represents the <c>SubsessionChanged</c> value.
    /// </summary>
    SubsessionChanged = 2,
    /// <summary>
    /// Represents the <c>OutOfSequence</c> value.
    /// </summary>
    OutOfSequence = 3
}

/// <summary>
/// Defines the available <c>SprtTransmissionChannel</c> values.
/// </summary>
public enum SprtTransmissionChannel {
    /// <summary>
    /// Represents the <c>UnreliableUnsequenced</c> value.
    /// </summary>
    UnreliableUnsequenced = 0,
    /// <summary>
    /// Represents the <c>ReliableSequenced</c> value.
    /// </summary>
    ReliableSequenced = 1,
    /// <summary>
    /// Represents the <c>ExpeditedReliableSequenced</c> value.
    /// </summary>
    ExpeditedReliableSequenced = 2,
    /// <summary>
    /// Represents the <c>UnreliableSequenced</c> value.
    /// </summary>
    UnreliableSequenced = 3
}

/// <summary>
/// Defines the available <c>SprtTimer</c> values.
/// </summary>
public enum SprtTimer {
    /// <summary>
    /// Represents the <c>Ta01</c> value.
    /// </summary>
    Ta01 = 0,
    /// <summary>
    /// Represents the <c>Ta02</c> value.
    /// </summary>
    Ta02 = 1,
    /// <summary>
    /// Represents the <c>Tr03</c> value.
    /// </summary>
    Tr03 = 2
}

/// <summary>
/// Defines the available <c>SprtTimerAction</c> values.
/// </summary>
public enum SprtTimerAction {
    /// <summary>
    /// Represents the <c>Set</c> value.
    /// </summary>
    Set = 0,
    /// <summary>
    /// Represents the <c>Clear</c> value.
    /// </summary>
    Clear = 1,
    /// <summary>
    /// Represents the <c>Adjust</c> value.
    /// </summary>
    Adjust = 2
}

/// <summary>
/// Defines the available <c>SprtLogLevel</c> values.
/// </summary>
public enum SprtLogLevel {
    /// <summary>
    /// Represents the <c>None</c> value.
    /// </summary>
    None = 0,
    /// <summary>
    /// Represents the <c>Flow</c> value.
    /// </summary>
    Flow = 1,
    /// <summary>
    /// Represents the <c>Error</c> value.
    /// </summary>
    Error = 2
}

/// <summary>
/// Represents the <c>SprtChannelParameters</c> type.
/// </summary>
/// <param name="PayloadBytes">The payload bytes value.</param>
/// <param name="WindowSize">The window size value.</param>
/// <param name="TimerTa01">The timer ta01 value.</param>
/// <param name="TimerTa02">The timer ta02 value.</param>
/// <param name="TimerTr03">The timer tr03 value.</param>
public readonly record struct SprtChannelParameters(
    ushort PayloadBytes,
    ushort WindowSize,
    int TimerTa01,
    int TimerTa02,
    int TimerTr03);

/// <summary>
/// Represents the <c>SprtTransmitPacketHandler</c> callback.
/// </summary>
/// <param name="userData">The user data value.</param>
/// <param name="packet">The packet value.</param>
/// <returns>The result of the operation.</returns>
public delegate int SprtTransmitPacketHandler(
    object? userData,
    ReadOnlySpan<byte> packet);

/// <summary>
/// Represents the <c>SprtReceiveDeliveryHandler</c> callback.
/// </summary>
/// <param name="userData">The user data value.</param>
/// <param name="channel">The channel value.</param>
/// <param name="sequenceNumber">The sequence number value.</param>
/// <param name="message">The message value.</param>
/// <returns>The result of the operation.</returns>
public delegate int SprtReceiveDeliveryHandler(
    object? userData,
    int channel,
    int sequenceNumber,
    ReadOnlySpan<byte> message);

/// <summary>
/// Passing <see cref="ulong.MaxValue"/> queries the current timestamp.
/// Passing any other value requests that absolute timeout.
/// </summary>
public delegate ulong SprtTimerHandler(
    object? userData,
    ulong timeout);

/// <summary>
/// Represents the <c>SprtStatusHandler</c> callback.
/// </summary>
/// <param name="userData">The user data value.</param>
/// <param name="status">The status value.</param>
public delegate void SprtStatusHandler(
    object? userData,
    int status);

/// <summary>
/// Represents the <c>SprtLogHandler</c> callback.
/// </summary>
/// <param name="level">The level value.</param>
/// <param name="message">The message value.</param>
public delegate void SprtLogHandler(
    SprtLogLevel level,
    string message);

/// <summary>
/// Represents the <c>SprtLogger</c> type.
/// </summary>
public sealed class SprtLogger {
    /// <summary>
    /// Gets or sets the <c>Level</c> value.
    /// </summary>
    public SprtLogLevel Level { get; set; } = SprtLogLevel.None;

    /// <summary>
    /// Gets or sets the <c>Handler</c> value.
    /// </summary>
    public SprtLogHandler? Handler { get; set; }

    internal void Flow(string message) {
        Write(SprtLogLevel.Flow, message);
    }

    internal void Error(string message) {
        Write(SprtLogLevel.Error, message);
    }

    internal void Buffer(SprtLogLevel level, string prefix, ReadOnlySpan<byte> data) {
        if (!IsEnabled(level))
            return;

        Write(level, $"{prefix}: {Convert.ToHexString(data)}");
    }

    private void Write(SprtLogLevel level, string message) {
        if (!IsEnabled(level))
            return;

        Handler?.Invoke(level, message);
    }

    private bool IsEnabled(SprtLogLevel level) {
        return Handler is not null &&
               Level != SprtLogLevel.None &&
               level >= Level;
    }
}

internal readonly record struct SprtChannelLimits(
    ushort MinimumPayloadBytes,
    ushort MaximumPayloadBytes,
    ushort MinimumWindowSize,
    ushort MaximumWindowSize);

internal sealed class SprtChannelState {
    public SprtChannelState(int maximumWindowSize, int maximumPayloadBytes) {
        Buffer = new byte[checked((maximumWindowSize + 1) * maximumPayloadBytes)];
        BufferLength = new ushort[maximumWindowSize + 1];
        Tr03Timers = new ulong[maximumWindowSize + 1];

        PreviousInTime = new byte[SprtConstants.MaximumWindowSize];
        NextInTime = new byte[SprtConstants.MaximumWindowSize];
        RemainingTries = new byte[SprtConstants.MaximumWindowSize];

        ResetSlots();
    }

    public bool Active { get; set; }

    public int MaximumPayloadBytes { get; set; }

    public int WindowSize { get; set; }

    public int Ta02Timeout { get; set; }

    public int Tr03Timeout { get; set; }

    public ulong Ta02Timer { get; set; }

    public ushort BaseSequenceNumber { get; set; }

    public ushort QueuingSequenceNumber { get; set; }

    public byte MaximumTries { get; set; }

    public int BufferInputPointer { get; set; }

    public int BufferAcknowledgedOutputPointer { get; set; }

    public byte[] Buffer { get; }

    public ushort[] BufferLength { get; }

    public ulong[] Tr03Timers { get; }

    public byte[] PreviousInTime { get; }

    public byte[] NextInTime { get; }

    public byte[] RemainingTries { get; }

    public byte FirstInTime { get; set; } = SprtConstants.FreeTimerQueueSlot;

    public byte LastInTime { get; set; } = SprtConstants.FreeTimerQueueSlot;

    public bool Busy { get; set; }

    public void Configure(SprtChannelParameters parameters) {
        Active = false;
        MaximumPayloadBytes = parameters.PayloadBytes;
        WindowSize = parameters.WindowSize;
        Ta02Timeout = parameters.TimerTa02;
        Tr03Timeout = parameters.TimerTr03;
        Ta02Timer = 0;
        BaseSequenceNumber = 0;
        QueuingSequenceNumber = 0;
        MaximumTries = SprtConstants.DefaultMaximumTries;
        BufferInputPointer = 0;
        BufferAcknowledgedOutputPointer = 0;
        Busy = false;

        ResetSlots();
    }

    private void ResetSlots() {
        Array.Fill(BufferLength, SprtConstants.FreeLengthSlot);
        Array.Clear(Tr03Timers);
        Array.Fill(PreviousInTime, SprtConstants.FreeTimerQueueSlot);
        Array.Fill(NextInTime, SprtConstants.FreeTimerQueueSlot);
        Array.Clear(RemainingTries);

        FirstInTime = SprtConstants.FreeTimerQueueSlot;
        LastInTime = SprtConstants.FreeTimerQueueSlot;
    }
}

internal class SprtDirectionState {
    public SprtDirectionState() {
        Channels =
        [
            new SprtChannelState(1, SprtConstants.MaxTc0PayloadBytes),
            new SprtChannelState(SprtConstants.MaxTc1WindowSize, SprtConstants.MaxTc1PayloadBytes),
            new SprtChannelState(SprtConstants.MaxTc2WindowSize, SprtConstants.MaxTc2PayloadBytes),
            new SprtChannelState(1, SprtConstants.MaxTc3PayloadBytes)
        ];
    }

    public byte SubsessionId { get; set; }

    public byte PayloadType { get; set; }

    public SprtChannelState[] Channels { get; }
}

internal sealed class SprtTransmitState : SprtDirectionState {
    public ushort[] AcknowledgementQueue { get; } = new ushort[3];

    public int AcknowledgementQueuePointer { get; set; }

    public int Ta01Timeout { get; set; }

    public ulong Ta01Timer { get; set; }

    public bool ImmediateTimer { get; set; }
}

/// <summary>
/// Managed SPRT protocol state corresponding to <c>sprt_state_t</c>.
/// Packet transmission and timer scheduling are delegated to the application.
/// </summary>
public sealed class SprtState : IDisposable {
    private static readonly SprtChannelLimits[] ChannelLimits =
    [
        new(
            SprtConstants.MinTc0PayloadBytes,
            SprtConstants.MaxTc0PayloadBytes,
            1,
            1),
        new(
            SprtConstants.MinTc1PayloadBytes,
            SprtConstants.MaxTc1PayloadBytes,
            SprtConstants.MinTc1WindowSize,
            SprtConstants.MaxTc1WindowSize),
        new(
            SprtConstants.MinTc2PayloadBytes,
            SprtConstants.MaxTc2PayloadBytes,
            SprtConstants.MinTc2WindowSize,
            SprtConstants.MaxTc2WindowSize),
        new(
            SprtConstants.MinTc3PayloadBytes,
            SprtConstants.MaxTc3PayloadBytes,
            1,
            1)
    ];

    private static readonly SprtChannelParameters[] DefaultChannelParameters =
    [
        new(
            SprtConstants.DefaultTc0PayloadBytes,
            1,
            -1,
            -1,
            -1),
        new(
            SprtConstants.DefaultTc1PayloadBytes,
            SprtConstants.DefaultTc1WindowSize,
            SprtConstants.DefaultTimerTc1Ta01,
            SprtConstants.DefaultTimerTc1Ta02,
            SprtConstants.DefaultTimerTc1Tr03),
        new(
            SprtConstants.DefaultTc2PayloadBytes,
            SprtConstants.DefaultTc2WindowSize,
            SprtConstants.DefaultTimerTc2Ta01,
            SprtConstants.DefaultTimerTc2Ta02,
            SprtConstants.DefaultTimerTc2Tr03),
        new(
            SprtConstants.DefaultTc3PayloadBytes,
            1,
            -1,
            -1,
            -1)
    ];

    private SprtTransmitPacketHandler? _transmitPacketHandler;
    private object? _transmitUserData;

    private SprtReceiveDeliveryHandler? _receiveDeliveryHandler;
    private object? _receiveUserData;

    private SprtTimerHandler? _timerHandler;
    private object? _timerUserData;

    private SprtStatusHandler? _statusHandler;
    private object? _statusUserData;

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <c>SprtState</c> type.
    /// </summary>
    /// <param name="subsessionId">The subsession id value.</param>
    /// <param name="receivePayloadType">The receive payload type value.</param>
    /// <param name="transmitPayloadType">The transmit payload type value.</param>
    /// <param name="parameters">The parameters value.</param>
    /// <param name="transmitPacketHandler">The transmit packet handler value.</param>
    /// <param name="transmitUserData">The transmit user data value.</param>
    /// <param name="receiveDeliveryHandler">The receive delivery handler value.</param>
    /// <param name="receiveUserData">The receive user data value.</param>
    /// <param name="timerHandler">The timer handler value.</param>
    /// <param name="timerUserData">The timer user data value.</param>
    /// <param name="statusHandler">The status handler value.</param>
    /// <param name="statusUserData">The status user data value.</param>
    public SprtState(
        byte subsessionId,
        byte receivePayloadType,
        byte transmitPayloadType,
        SprtChannelParameters[]? parameters,
        SprtTransmitPacketHandler transmitPacketHandler,
        object? transmitUserData,
        SprtReceiveDeliveryHandler receiveDeliveryHandler,
        object? receiveUserData,
        SprtTimerHandler timerHandler,
        object? timerUserData,
        SprtStatusHandler statusHandler,
        object? statusUserData) {
        Initialize(
            subsessionId,
            receivePayloadType,
            transmitPayloadType,
            parameters,
            transmitPacketHandler,
            transmitUserData,
            receiveDeliveryHandler,
            receiveUserData,
            timerHandler,
            timerUserData,
            statusHandler,
            statusUserData);
    }

    internal SprtState() {
    }

    /// <summary>
    /// Gets the <c>Logging</c> value.
    /// </summary>
    public SprtLogger Logging { get; } = new();

    /// <summary>
    /// Gets or sets the <c>LatestTimer</c> value.
    /// </summary>
    public ulong LatestTimer { get; private set; }

    /// <summary>
    /// Gets the <c>IsDisposed</c> value.
    /// </summary>
    public bool IsDisposed => _disposed;

    internal SprtDirectionState Receive { get; } = new();

    internal SprtTransmitState Transmit { get; } = new();

    /// <summary>
    /// Performs the <c>Initialize</c> operation.
    /// </summary>
    /// <param name="subsessionId">The subsession id value.</param>
    /// <param name="receivePayloadType">The receive payload type value.</param>
    /// <param name="transmitPayloadType">The transmit payload type value.</param>
    /// <param name="parameters">The parameters value.</param>
    /// <param name="transmitPacketHandler">The transmit packet handler value.</param>
    /// <param name="transmitUserData">The transmit user data value.</param>
    /// <param name="receiveDeliveryHandler">The receive delivery handler value.</param>
    /// <param name="receiveUserData">The receive user data value.</param>
    /// <param name="timerHandler">The timer handler value.</param>
    /// <param name="timerUserData">The timer user data value.</param>
    /// <param name="statusHandler">The status handler value.</param>
    /// <param name="statusUserData">The status user data value.</param>
    public void Initialize(
        byte subsessionId,
        byte receivePayloadType,
        byte transmitPayloadType,
        SprtChannelParameters[]? parameters,
        SprtTransmitPacketHandler transmitPacketHandler,
        object? transmitUserData,
        SprtReceiveDeliveryHandler receiveDeliveryHandler,
        object? receiveUserData,
        SprtTimerHandler timerHandler,
        object? timerUserData,
        SprtStatusHandler statusHandler,
        object? statusUserData) {
        ArgumentNullException.ThrowIfNull(transmitPacketHandler);
        ArgumentNullException.ThrowIfNull(receiveDeliveryHandler);
        ArgumentNullException.ThrowIfNull(timerHandler);
        ArgumentNullException.ThrowIfNull(statusHandler);

        SprtChannelParameters[] selected = parameters ?? DefaultChannelParameters;

        if (selected.Length < SprtConstants.ChannelCount) {
            throw new ArgumentException(
                "Four SPRT channel parameter entries are required.",
                nameof(parameters));
        }

        ValidateParameters(selected);

        _transmitPacketHandler = transmitPacketHandler;
        _transmitUserData = transmitUserData;
        _receiveDeliveryHandler = receiveDeliveryHandler;
        _receiveUserData = receiveUserData;
        _timerHandler = timerHandler;
        _timerUserData = timerUserData;
        _statusHandler = statusHandler;
        _statusUserData = statusUserData;

        Receive.SubsessionId = 0xFF;
        Transmit.SubsessionId = subsessionId;
        Receive.PayloadType = receivePayloadType;
        Transmit.PayloadType = transmitPayloadType;

        Transmit.AcknowledgementQueuePointer = 0;
        Array.Clear(Transmit.AcknowledgementQueue);
        Transmit.Ta01Timeout =
            selected[(int)SprtTransmissionChannel.ReliableSequenced].TimerTa01;
        Transmit.Ta01Timer = 0;
        Transmit.ImmediateTimer = false;

        for (int channel = 0; channel < SprtConstants.ChannelCount; channel++) {
            Receive.Channels[channel].Configure(selected[channel]);
            Transmit.Channels[channel].Configure(selected[channel]);
        }

        LatestTimer = 0;
        _disposed = false;
    }

    /// <summary>
    /// Performs the <c>TransmissionChannelToString</c> operation.
    /// </summary>
    /// <param name="channel">The channel value.</param>
    /// <returns>The result of the operation.</returns>
    public static string TransmissionChannelToString(int channel) {
        return channel switch {
            (int)SprtTransmissionChannel.UnreliableUnsequenced =>
                "unreliable unsequenced",

            (int)SprtTransmissionChannel.ReliableSequenced =>
                "reliable sequenced",

            (int)SprtTransmissionChannel.ExpeditedReliableSequenced =>
                "expedited reliable sequenced",

            (int)SprtTransmissionChannel.UnreliableSequenced =>
                "unreliable sequenced",

            _ => "unknown"
        };
    }

    /// <summary>
    /// Performs the <c>TimerExpired</c> operation.
    /// </summary>
    /// <param name="now">The now value.</param>
    /// <returns>The result of the operation.</returns>
    public int TimerExpired(ulong now) {
        ThrowIfDisposed();

        Logging.Flow($"Timer expired at {now}");

        if (now < LatestTimer) {
            Logging.Flow(
                $"Timer returned {LatestTimer - now}us early");

            _timerHandler?.Invoke(_timerUserData, LatestTimer);
            return 0;
        }

        bool somethingWasSent = false;

        if (Transmit.ImmediateTimer) {
            Transmit.ImmediateTimer = false;
            DeliverBufferedPackets();
        }

        for (int channel = 1; channel <= 2; channel++) {
            bool sentForChannel =
                RetransmitUnacknowledged(channel, now);

            SprtChannelState transmitChannel =
                Transmit.Channels[channel];

            if (transmitChannel.Ta02Timer != 0) {
                if (transmitChannel.Ta02Timer <= now &&
                    !sentForChannel) {
                    Logging.Flow("Keepalive only packet sent");
                    BuildAndSendPacket(
                        channel,
                        0,
                        ReadOnlySpan<byte>.Empty);
                    sentForChannel = true;
                }

                if (sentForChannel) {
                    transmitChannel.Ta02Timer =
                        AddTimeout(now, transmitChannel.Ta02Timeout);

                    Logging.Flow(
                        $"TA02({channel}) set to " +
                        $"{transmitChannel.Ta02Timer}");
                }
            }

            if (sentForChannel)
                somethingWasSent = true;
        }

        if (!somethingWasSent &&
            Transmit.Ta01Timer != 0 &&
            Transmit.Ta01Timer <= now &&
            Transmit.AcknowledgementQueuePointer > 0) {
            Logging.Flow("ACK only packet sent");

            BuildAndSendPacket(
                (int)SprtTransmissionChannel.UnreliableUnsequenced,
                0,
                ReadOnlySpan<byte>.Empty);
        }

        UpdateTimer();
        return 0;
    }

    /// <summary>
    /// Performs the <c>ReceivePacket</c> operation.
    /// </summary>
    /// <param name="packet">The packet value.</param>
    /// <returns>The result of the operation.</returns>
    public int ReceivePacket(ReadOnlySpan<byte> packet) {
        ThrowIfDisposed();

        Logging.Buffer(SprtLogLevel.Flow, "Rx", packet);

        if (packet.Length < 6) {
            Logging.Flow("Rx packet too short");
            return -1;
        }

        int headerExtensionBit = (packet[0] >> 7) & 1;
        int reservedBit = (packet[1] >> 7) & 1;
        byte subsessionId = (byte)(packet[0] & 0x7F);
        byte payloadType = (byte)(packet[1] & 0x7F);

        if (headerExtensionBit != 0 || reservedBit != 0) {
            Logging.Flow(
                "Rx packet header does not look like SPRT");
            return -1;
        }

        if (payloadType != Receive.PayloadType) {
            Logging.Flow(
                $"Rx payload type {payloadType}, expected " +
                $"{Receive.PayloadType}");
            return -1;
        }

        if (Receive.SubsessionId == 0xFF) {
            Receive.SubsessionId = subsessionId;
        } else if (subsessionId != Receive.SubsessionId) {
            Logging.Flow(
                $"Rx subsession ID {subsessionId}, expected " +
                $"{Receive.SubsessionId}");

            ReportStatus(SprtStatus.SubsessionChanged);

            // Matches the supplied C source, where sprt_rx_reinit()
            // is present but intentionally contains only a TODO.
            ReceiveReinitialize();
            return -1;
        }

        int channel = (packet[2] >> 6) & 3;
        ushort sequenceNumber =
            (ushort)(ReadUInt16BigEndian(packet, 2) &
                     SprtConstants.SequenceNumberMask);

        int acknowledgementCount = (packet[4] >> 6) & 3;
        SprtChannelState receiveChannel =
            Receive.Channels[channel];

        ushort baseSequenceNumber =
            (ushort)(ReadUInt16BigEndian(packet, 4) &
                     SprtConstants.SequenceNumberMask);

        SprtChannelState transmitChannel =
            Transmit.Channels[channel];

        if (transmitChannel.Busy &&
            transmitChannel.BaseSequenceNumber != baseSequenceNumber) {
            Logging.Flow(
                $"BSN for channel {channel} changed from " +
                $"{transmitChannel.BaseSequenceNumber} to " +
                $"{baseSequenceNumber}");
        }

        transmitChannel.BaseSequenceNumber =
            baseSequenceNumber;

        int headerLength = 6;

        if (acknowledgementCount > 0) {
            if (packet.Length < 6 + 2 * acknowledgementCount) {
                Logging.Flow("Rx packet too short");
                return -1;
            }

            Span<int> acknowledgementChannels = stackalloc int[3];
            Span<int> acknowledgementSequences = stackalloc int[3];

            for (int i = 0; i < acknowledgementCount; i++) {
                acknowledgementChannels[i] =
                    (packet[headerLength] >> 6) & 3;

                acknowledgementSequences[i] =
                    ReadUInt16BigEndian(packet, headerLength) &
                    SprtConstants.SequenceNumberMask;

                headerLength += 2;
            }

            ProcessAcknowledgements(
                acknowledgementCount,
                acknowledgementChannels,
                acknowledgementSequences);
        }

        int payloadLength = packet.Length - headerLength;

        Logging.Flow(
            $"Rx ch {channel} seq {sequenceNumber} " +
            $"noa {acknowledgementCount} len {payloadLength}");

        if (payloadLength <= 0)
            return 0;

        if (payloadLength > receiveChannel.MaximumPayloadBytes) {
            Logging.Error(
                $"Payload too long {payloadLength} " +
                $"({receiveChannel.MaximumPayloadBytes})");
            return 0;
        }

        ReadOnlySpan<byte> payload =
            packet.Slice(headerLength, payloadLength);

        switch ((SprtTransmissionChannel)channel) {
            case SprtTransmissionChannel.ReliableSequenced:
            case SprtTransmissionChannel.ExpeditedReliableSequenced:
                ProcessReliablePayload(
                    channel,
                    sequenceNumber,
                    receiveChannel,
                    payload);
                receiveChannel.Active = true;
                break;

            case SprtTransmissionChannel.UnreliableUnsequenced:
            case SprtTransmissionChannel.UnreliableSequenced:
                _receiveDeliveryHandler?.Invoke(
                    _receiveUserData,
                    channel,
                    sequenceNumber,
                    payload);

                receiveChannel.Active = true;
                break;
        }

        return 0;
    }

    /// <summary>
    /// Performs the <c>TransmitMessage</c> operation.
    /// </summary>
    /// <param name="channel">The channel value.</param>
    /// <param name="payload">The payload value.</param>
    /// <returns>The result of the operation.</returns>
    public int TransmitMessage(
        int channel,
        ReadOnlySpan<byte> payload) {
        ThrowIfDisposed();

        if (!IsValidChannel(channel))
            return -1;

        SprtChannelState state =
            Transmit.Channels[channel];

        if (payload.IsEmpty ||
            payload.Length > state.MaximumPayloadBytes) {
            return -1;
        }

        switch ((SprtTransmissionChannel)channel) {
            case SprtTransmissionChannel.ReliableSequenced:
            case SprtTransmissionChannel.ExpeditedReliableSequenced:
                return TransmitReliable(channel, state, payload);

            case SprtTransmissionChannel.UnreliableUnsequenced:
                BuildAndSendPacket(channel, 0, payload);
                return 0;

            case SprtTransmissionChannel.UnreliableSequenced: {
                    ushort sequenceNumber =
                        state.QueuingSequenceNumber;

                    BuildAndSendPacket(
                        channel,
                        sequenceNumber,
                        payload);

                    state.QueuingSequenceNumber =
                        IncrementSequence(sequenceNumber);

                    return 0;
                }

            default:
                return -1;
        }
    }

    /// <summary>
    /// Performs the <c>SetLocalWindowSize</c> operation.
    /// </summary>
    /// <param name="channel">The channel value.</param>
    /// <param name="size">The size value.</param>
    /// <returns>The result of the operation.</returns>
    public int SetLocalWindowSize(int channel, int size) {
        ThrowIfDisposed();

        if (!IsReliableChannel(channel) ||
            !IsWindowSizeValid(channel, size)) {
            return -1;
        }

        Receive.Channels[channel].WindowSize = size;
        return 0;
    }

    /// <summary>
    /// Performs the <c>GetLocalWindowSize</c> operation.
    /// </summary>
    /// <param name="channel">The channel value.</param>
    /// <returns>The result of the operation.</returns>
    public int GetLocalWindowSize(int channel) {
        ThrowIfDisposed();

        return IsReliableChannel(channel)
            ? Receive.Channels[channel].WindowSize
            : -1;
    }

    /// <summary>
    /// Performs the <c>SetLocalPayloadBytes</c> operation.
    /// </summary>
    /// <param name="channel">The channel value.</param>
    /// <param name="maximumLength">The maximum length value.</param>
    /// <returns>The result of the operation.</returns>
    public int SetLocalPayloadBytes(int channel, int maximumLength) {
        ThrowIfDisposed();

        if (!IsValidChannel(channel) ||
            !IsPayloadSizeValid(channel, maximumLength)) {
            return -1;
        }

        Receive.Channels[channel].MaximumPayloadBytes =
            maximumLength;

        return 0;
    }

    /// <summary>
    /// Performs the <c>GetLocalPayloadBytes</c> operation.
    /// </summary>
    /// <param name="channel">The channel value.</param>
    /// <returns>The result of the operation.</returns>
    public int GetLocalPayloadBytes(int channel) {
        ThrowIfDisposed();

        return IsValidChannel(channel)
            ? Receive.Channels[channel].MaximumPayloadBytes
            : -1;
    }

    /// <summary>
    /// Performs the <c>SetLocalMaximumTries</c> operation.
    /// </summary>
    /// <param name="channel">The channel value.</param>
    /// <param name="maximumTries">The maximum tries value.</param>
    /// <returns>The result of the operation.</returns>
    public int SetLocalMaximumTries(int channel, int maximumTries) {
        ThrowIfDisposed();

        if (!IsReliableChannel(channel) ||
            maximumTries < SprtConstants.MinimumMaximumTries ||
            maximumTries > SprtConstants.MaximumMaximumTries) {
            return -1;
        }

        Transmit.Channels[channel].MaximumTries =
            (byte)maximumTries;

        return 0;
    }

    /// <summary>
    /// Performs the <c>GetLocalMaximumTries</c> operation.
    /// </summary>
    /// <param name="channel">The channel value.</param>
    /// <returns>The result of the operation.</returns>
    public int GetLocalMaximumTries(int channel) {
        ThrowIfDisposed();

        return IsReliableChannel(channel)
            ? Transmit.Channels[channel].MaximumTries
            : -1;
    }

    /// <summary>
    /// Performs the <c>SetFarWindowSize</c> operation.
    /// </summary>
    /// <param name="channel">The channel value.</param>
    /// <param name="size">The size value.</param>
    /// <returns>The result of the operation.</returns>
    public int SetFarWindowSize(int channel, int size) {
        ThrowIfDisposed();

        if (!IsReliableChannel(channel) ||
            !IsWindowSizeValid(channel, size)) {
            return -1;
        }

        Transmit.Channels[channel].WindowSize = size;
        return 0;
    }

    /// <summary>
    /// Performs the <c>GetFarWindowSize</c> operation.
    /// </summary>
    /// <param name="channel">The channel value.</param>
    /// <returns>The result of the operation.</returns>
    public int GetFarWindowSize(int channel) {
        ThrowIfDisposed();

        return IsReliableChannel(channel)
            ? Transmit.Channels[channel].WindowSize
            : -1;
    }

    /// <summary>
    /// Performs the <c>SetFarPayloadBytes</c> operation.
    /// </summary>
    /// <param name="channel">The channel value.</param>
    /// <param name="maximumLength">The maximum length value.</param>
    /// <returns>The result of the operation.</returns>
    public int SetFarPayloadBytes(int channel, int maximumLength) {
        ThrowIfDisposed();

        if (!IsValidChannel(channel) ||
            !IsPayloadSizeValid(channel, maximumLength)) {
            return -1;
        }

        Transmit.Channels[channel].MaximumPayloadBytes =
            maximumLength;

        return 0;
    }

    /// <summary>
    /// Performs the <c>GetFarPayloadBytes</c> operation.
    /// </summary>
    /// <param name="channel">The channel value.</param>
    /// <returns>The result of the operation.</returns>
    public int GetFarPayloadBytes(int channel) {
        ThrowIfDisposed();

        return IsValidChannel(channel)
            ? Transmit.Channels[channel].MaximumPayloadBytes
            : -1;
    }

    /// <summary>
    /// Performs the <c>SetTimeout</c> operation.
    /// </summary>
    /// <param name="channel">The channel value.</param>
    /// <param name="timer">The timer value.</param>
    /// <param name="timeout">The timeout value.</param>
    /// <returns>The result of the operation.</returns>
    public int SetTimeout(
        int channel,
        SprtTimer timer,
        int timeout) {
        ThrowIfDisposed();

        switch (timer) {
            case SprtTimer.Ta01:
                if (!IsValidChannel(channel))
                    return -1;

                Transmit.Ta01Timeout = timeout;
                return 0;

            case SprtTimer.Ta02:
                if (!IsReliableChannel(channel))
                    return -1;

                Transmit.Channels[channel].Ta02Timeout = timeout;
                return 0;

            case SprtTimer.Tr03:
                if (!IsReliableChannel(channel))
                    return -1;

                Transmit.Channels[channel].Tr03Timeout = timeout;
                return 0;

            default:
                return -1;
        }
    }

    /// <summary>
    /// Performs the <c>GetTimeout</c> operation.
    /// </summary>
    /// <param name="channel">The channel value.</param>
    /// <param name="timer">The timer value.</param>
    /// <returns>The result of the operation.</returns>
    public int GetTimeout(
        int channel,
        SprtTimer timer) {
        ThrowIfDisposed();

        return timer switch {
            SprtTimer.Ta01 when IsValidChannel(channel) =>
                Transmit.Ta01Timeout,

            SprtTimer.Ta02 when IsReliableChannel(channel) =>
                Transmit.Channels[channel].Ta02Timeout,

            SprtTimer.Tr03 when IsReliableChannel(channel) =>
                Transmit.Channels[channel].Tr03Timeout,

            _ => -1
        };
    }

    /// <summary>
    /// Performs the <c>SetLocalBusy</c> operation.
    /// </summary>
    /// <param name="channel">The channel value.</param>
    /// <param name="busy">The busy value.</param>
    /// <returns>The result of the operation.</returns>
    public int SetLocalBusy(int channel, bool busy) {
        ThrowIfDisposed();

        bool previousBusy = false;

        if (IsReliableChannel(channel)) {
            SprtChannelState state =
                Receive.Channels[channel];

            previousBusy = state.Busy;
            state.Busy = busy;

            if (previousBusy && !busy) {
                Transmit.ImmediateTimer = true;
                UpdateTimer();
            }
        }

        return previousBusy ? 1 : 0;
    }

    /// <summary>
    /// Performs the <c>GetFarBusyStatus</c> operation.
    /// </summary>
    /// <param name="channel">The channel value.</param>
    /// <returns>The result of the operation.</returns>
    public bool GetFarBusyStatus(int channel) {
        ThrowIfDisposed();

        return IsValidChannel(channel) &&
               Transmit.Channels[channel].Busy;
    }

    /// <summary>
    /// Performs the <c>Release</c> operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public int Release() {
        return 0;
    }

    /// <summary>
    /// Performs the <c>Dispose</c> operation.
    /// </summary>
    public void Dispose() {
        if (_disposed)
            return;

        _transmitPacketHandler = null;
        _transmitUserData = null;
        _receiveDeliveryHandler = null;
        _receiveUserData = null;
        _timerHandler = null;
        _timerUserData = null;
        _statusHandler = null;
        _statusUserData = null;

        LatestTimer = 0;
        _disposed = true;
    }

    private int TransmitReliable(
        int channel,
        SprtChannelState state,
        ReadOnlySpan<byte> payload) {
        int inputPointer = state.BufferInputPointer;
        int outputPointer =
            state.BufferAcknowledgedOutputPointer;

        int available =
            outputPointer - inputPointer - 1;

        if (available < 0)
            available += state.WindowSize;

        if (available < 1)
            return -1;

        payload.CopyTo(
            GetBufferSlot(state, inputPointer, payload.Length));

        state.BufferLength[inputPointer] =
            checked((ushort)payload.Length);

        ushort sequenceNumber =
            state.QueuingSequenceNumber;

        state.QueuingSequenceNumber =
            IncrementSequence(sequenceNumber);

        ulong now = QueryCurrentTime();

        state.Tr03Timers[inputPointer] =
            AddTimeout(now, state.Tr03Timeout);

        Logging.Flow(
            $"TR03({channel})[{inputPointer}] set to " +
            $"{state.Tr03Timers[inputPointer]}");

        state.RemainingTries[inputPointer] =
            state.MaximumTries;

        AddTimerQueueLastEntry(
            channel,
            inputPointer);

        inputPointer++;

        if (inputPointer >= state.WindowSize)
            inputPointer = 0;

        state.BufferInputPointer = inputPointer;

        now = QueryCurrentTime();

        state.Ta02Timer =
            AddTimeout(now, state.Ta02Timeout);

        Logging.Flow(
            $"TA02({channel}) set to {state.Ta02Timer}");

        BuildAndSendPacket(
            channel,
            sequenceNumber,
            payload);

        return 0;
    }

    private void ProcessReliablePayload(
        int channel,
        ushort sequenceNumber,
        SprtChannelState state,
        ReadOnlySpan<byte> payload) {
        if (sequenceNumber == state.BaseSequenceNumber) {
            int inputPointer = state.BufferInputPointer;

            QueueAcknowledgement(
                channel,
                sequenceNumber);

            if (state.Busy) {
                payload.CopyTo(
                    GetBufferSlot(
                        state,
                        inputPointer,
                        payload.Length));

                state.BufferLength[inputPointer] =
                    checked((ushort)payload.Length);

                return;
            }

            _receiveDeliveryHandler?.Invoke(
                _receiveUserData,
                channel,
                sequenceNumber,
                payload);

            state.BaseSequenceNumber =
                IncrementSequence(state.BaseSequenceNumber);

            state.BufferLength[inputPointer] =
                SprtConstants.FreeLengthSlot;

            inputPointer++;

            if (inputPointer >= state.WindowSize)
                inputPointer = 0;

            while (state.BufferLength[inputPointer] !=
                   SprtConstants.FreeLengthSlot) {
                if (state.Busy)
                    break;

                int length =
                    state.BufferLength[inputPointer];

                _receiveDeliveryHandler?.Invoke(
                    _receiveUserData,
                    channel,
                    state.BaseSequenceNumber,
                    GetBufferSlot(
                        state,
                        inputPointer,
                        length));

                state.BaseSequenceNumber =
                    IncrementSequence(state.BaseSequenceNumber);

                state.BufferLength[inputPointer] =
                    SprtConstants.FreeLengthSlot;

                inputPointer++;

                if (inputPointer >= state.WindowSize)
                    inputPointer = 0;
            }

            state.BufferInputPointer = inputPointer;
            return;
        }

        int difference =
            (sequenceNumber - state.BaseSequenceNumber) &
            SprtConstants.SequenceNumberMask;

        if (difference < state.WindowSize) {
            QueueAcknowledgement(
                channel,
                sequenceNumber);

            int inputPointer =
                state.BufferInputPointer + difference;

            if (inputPointer >= state.WindowSize)
                inputPointer -= state.WindowSize;

            payload.CopyTo(
                GetBufferSlot(
                    state,
                    inputPointer,
                    payload.Length));

            state.BufferLength[inputPointer] =
                checked((ushort)payload.Length);

            return;
        }

        if (difference > 2 * SprtConstants.MaximumWindowSize) {
            QueueAcknowledgement(
                channel,
                sequenceNumber);

            ReportStatus(SprtStatus.OutOfSequence);
        }
    }

    private void ProcessAcknowledgements(
        int count,
        ReadOnlySpan<int> channels,
        ReadOnlySpan<int> sequenceNumbers) {
        if (count > 0) {
            Logging.Flow(
                $"Received {count} acknowledgements");
        }

        for (int i = 0; i < count; i++) {
            int channel = channels[i];
            int sequenceNumber = sequenceNumbers[i];

            Logging.Flow(
                $"ACK received for channel " +
                $"{TransmissionChannelToString(channel)}, " +
                $"seq no {sequenceNumber}");

            SprtChannelState state =
                Transmit.Channels[channel];

            switch ((SprtTransmissionChannel)channel) {
                case SprtTransmissionChannel.ReliableSequenced:
                case SprtTransmissionChannel.ExpeditedReliableSequenced: {
                        int difference =
                            (state.QueuingSequenceNumber -
                             sequenceNumber) &
                            SprtConstants.SequenceNumberMask;

                        if (difference >= state.WindowSize) {
                            Logging.Flow(
                                $"Slot BAD {channel} This is an ack " +
                                $"for something outside the current window - " +
                                $"{state.QueuingSequenceNumber} " +
                                $"{sequenceNumber}");
                            break;
                        }

                        int slot =
                            state.BufferInputPointer - difference;

                        if (slot < 0)
                            slot += state.WindowSize;

                        if (state.BufferLength[slot] ==
                            SprtConstants.FreeLengthSlot) {
                            Logging.Flow(
                                $"Slot BAD {channel}/{slot} does not contain " +
                                $"{sequenceNumber} " +
                                $"[{state.QueuingSequenceNumber}, " +
                                $"{state.BufferInputPointer}]");
                            break;
                        }

                        Logging.Flow(
                            $"Slot OK {channel}/{slot} contains " +
                            $"{sequenceNumber} " +
                            $"[{state.QueuingSequenceNumber}, " +
                            $"{state.BufferInputPointer}]");

                        state.BufferLength[slot] =
                            SprtConstants.FreeLengthSlot;

                        state.Tr03Timers[slot] = 0;

                        Logging.Flow(
                            $"TR03({channel})[{slot}] cancelled");

                        DeleteTimerQueueEntry(
                            channel,
                            slot);

                        int pointer =
                            state.BufferAcknowledgedOutputPointer;

                        if (slot == pointer) {
                            do {
                                pointer++;

                                if (pointer >= state.WindowSize)
                                    pointer = 0;
                            }
                            while (
                                pointer != state.BufferInputPointer &&
                                state.BufferLength[pointer] ==
                                SprtConstants.FreeLengthSlot);

                            state.BufferAcknowledgedOutputPointer =
                                pointer;
                        }

                        break;
                    }

                case SprtTransmissionChannel.UnreliableUnsequenced:
                case SprtTransmissionChannel.UnreliableSequenced:
                    Logging.Flow(
                        "Acknowledgement received for unreliable " +
                        $"channel {TransmissionChannelToString(channel)}");
                    break;
            }
        }
    }

    private bool RetransmitUnacknowledged(
        int channel,
        ulong now) {
        bool sent = false;

        if (!IsReliableChannel(channel))
            return false;

        SprtChannelState state =
            Transmit.Channels[channel];

        while (state.FirstInTime !=
                   SprtConstants.FreeTimerQueueSlot &&
               state.Tr03Timers[state.FirstInTime] <= now) {
            int first = state.FirstInTime;

            int difference =
                state.BufferInputPointer - first;

            if (difference < 0)
                difference += state.WindowSize;

            ushort sequenceNumber =
                unchecked((ushort)
                    (state.QueuingSequenceNumber - difference));

            if (state.BufferLength[first] !=
                SprtConstants.FreeLengthSlot) {
                int length = state.BufferLength[first];

                BuildAndSendPacket(
                    channel,
                    sequenceNumber,
                    GetBufferSlot(state, first, length));

                sent = true;
            } else {
                Logging.Error(
                    $"Empty slot scheduled {first} " +
                    $"{state.BufferLength[first]}");
            }

            DeleteTimerQueueEntry(
                channel,
                first);

            state.RemainingTries[first]--;

            if (state.RemainingTries[first] <= 0) {
                ReportStatus(SprtStatus.ExcessRetries);
            } else {
                state.Tr03Timers[first] =
                    AddTimeout(
                        state.Tr03Timers[first],
                        state.Tr03Timeout);

                AddTimerQueueLastEntry(
                    channel,
                    first);
            }
        }

        return sent;
    }

    private int DeliverBufferedPackets() {
        for (int channel = 1; channel <= 2; channel++) {
            SprtChannelState state =
                Receive.Channels[channel];

            int inputPointer =
                state.BufferInputPointer;

            while (state.BufferLength[inputPointer] !=
                   SprtConstants.FreeLengthSlot) {
                if (state.Busy)
                    break;

                int length =
                    state.BufferLength[inputPointer];

                _receiveDeliveryHandler?.Invoke(
                    _receiveUserData,
                    channel,
                    state.BaseSequenceNumber,
                    GetBufferSlot(
                        state,
                        inputPointer,
                        length));

                state.BaseSequenceNumber =
                    IncrementSequence(state.BaseSequenceNumber);

                state.BufferLength[inputPointer] =
                    SprtConstants.FreeLengthSlot;

                inputPointer++;

                if (inputPointer >= state.WindowSize)
                    inputPointer = 0;
            }

            state.BufferInputPointer = inputPointer;
        }

        return 0;
    }

    private int QueueAcknowledgement(
        int channel,
        ushort sequenceNumber) {
        if (Transmit.AcknowledgementQueuePointer >= 3) {
            Logging.Error("ACK queue overflow");

            BuildAndSendPacket(
                channel,
                0,
                ReadOnlySpan<byte>.Empty);
        }

        ushort entry =
            unchecked((ushort)
                ((channel << 14) | sequenceNumber));

        bool found = false;

        for (int i = 0;
             i < Transmit.AcknowledgementQueuePointer;
             i++) {
            if (Transmit.AcknowledgementQueue[i] == entry) {
                found = true;
                break;
            }
        }

        if (found)
            return 0;

        int pointer =
            Transmit.AcknowledgementQueuePointer;

        Transmit.AcknowledgementQueue[pointer] = entry;
        Transmit.AcknowledgementQueuePointer++;

        if (Transmit.AcknowledgementQueuePointer == 1) {
            ulong now = QueryCurrentTime();

            Transmit.Ta01Timer =
                AddTimeout(now, Transmit.Ta01Timeout);

            Logging.Flow(
                $"TA01 set to {Transmit.Ta01Timer}");

            UpdateTimer();
        } else if (Transmit.AcknowledgementQueuePointer >= 3) {
            BuildAndSendPacket(
                channel,
                0,
                ReadOnlySpan<byte>.Empty);
        }

        return 0;
    }

    private int BuildAndSendPacket(
        int channel,
        ushort sequenceNumber,
        ReadOnlySpan<byte> payload) {
        Span<byte> packet =
            stackalloc byte[SprtConstants.MaximumPacketBytes];

        packet[0] = Transmit.SubsessionId;
        packet[1] = Transmit.PayloadType;

        WriteUInt16BigEndian(
            packet,
            2,
            unchecked((ushort)
                ((channel << 14) |
                 (sequenceNumber &
                  SprtConstants.SequenceNumberMask))));

        int length = 6;
        int acknowledgementCount = 0;

        if (Transmit.AcknowledgementQueuePointer > 0) {
            for (int i = 0;
                 i < Transmit.AcknowledgementQueuePointer;
                 i++) {
                WriteUInt16BigEndian(
                    packet,
                    length,
                    Transmit.AcknowledgementQueue[i]);

                length += 2;
                acknowledgementCount++;
            }

            Transmit.AcknowledgementQueuePointer = 0;
            Transmit.Ta01Timer = 0;

            Logging.Flow("TA01 cancelled");
        }

        WriteUInt16BigEndian(
            packet,
            4,
            unchecked((ushort)
                ((acknowledgementCount << 14) |
                 Receive.Channels[channel].BaseSequenceNumber)));

        if (!payload.IsEmpty) {
            payload.CopyTo(packet[length..]);
            length += payload.Length;
        }

        ReadOnlySpan<byte> completedPacket =
            packet[..length];

        Logging.Buffer(
            SprtLogLevel.Flow,
            "Tx",
            completedPacket);

        _transmitPacketHandler?.Invoke(
            _transmitUserData,
            completedPacket);

        UpdateTimer();
        return length;
    }

    private int UpdateTimer() {
        ulong shortest;
        int shortestIs;

        if (Transmit.ImmediateTimer) {
            shortest = 1;
            shortestIs = 4;
        } else {
            shortest = ulong.MaxValue;
            shortestIs = 0;

            if (Transmit.Ta01Timer != 0 &&
                Transmit.Ta01Timer < shortest) {
                shortest = Transmit.Ta01Timer;
                shortestIs = 1;
            }

            for (int channel = 1; channel <= 2; channel++) {
                SprtChannelState state =
                    Transmit.Channels[channel];

                if (state.Ta02Timer != 0 &&
                    state.Ta02Timer < shortest) {
                    shortest = state.Ta02Timer;
                    shortestIs = 2 + 10 * channel;
                }

                byte first = state.FirstInTime;

                if (first != SprtConstants.FreeTimerQueueSlot &&
                    state.Tr03Timers[first] != 0 &&
                    state.Tr03Timers[first] < shortest) {
                    shortest = state.Tr03Timers[first];
                    shortestIs = 3 + 10 * channel;
                }
            }

            if (shortest == ulong.MaxValue)
                shortest = 0;
        }

        Logging.Flow(
            $"Update timer to {shortest} ({shortestIs})");

        LatestTimer = shortest;

        _timerHandler?.Invoke(
            _timerUserData,
            LatestTimer);

        return 0;
    }

    private void DeleteTimerQueueEntry(
        int channel,
        int slot) {
        SprtChannelState state =
            Transmit.Channels[channel];

        if (state.FirstInTime ==
                SprtConstants.FreeTimerQueueSlot ||
            slot == SprtConstants.FreeTimerQueueSlot) {
            return;
        }

        if (state.FirstInTime == slot) {
            state.FirstInTime =
                state.NextInTime[slot];
        } else {
            int previous =
                state.PreviousInTime[slot];

            state.NextInTime[previous] =
                state.NextInTime[slot];
        }

        if (state.LastInTime == slot) {
            state.LastInTime =
                state.PreviousInTime[slot];
        } else {
            int next =
                state.NextInTime[slot];

            state.PreviousInTime[next] =
                state.PreviousInTime[slot];
        }

        state.PreviousInTime[slot] =
            SprtConstants.FreeTimerQueueSlot;

        state.NextInTime[slot] =
            SprtConstants.FreeTimerQueueSlot;
    }

    private void AddTimerQueueLastEntry(
        int channel,
        int slot) {
        SprtChannelState state =
            Transmit.Channels[channel];

        if (state.LastInTime ==
            SprtConstants.FreeTimerQueueSlot) {
            state.FirstInTime = (byte)slot;
        } else {
            state.NextInTime[state.LastInTime] =
                (byte)slot;
        }

        state.PreviousInTime[slot] =
            state.LastInTime;

        state.NextInTime[slot] =
            SprtConstants.FreeTimerQueueSlot;

        state.LastInTime = (byte)slot;
    }

    private void ReceiveReinitialize() {
        // Intentionally left without state changes to match the supplied
        // sprt.c, whose sprt_rx_reinit() body contains only "TODO".
    }

    private void ReportStatus(SprtStatus status) {
        _statusHandler?.Invoke(
            _statusUserData,
            (int)status);
    }

    private ulong QueryCurrentTime() {
        SprtTimerHandler callback =
            _timerHandler ??
            throw new InvalidOperationException(
                "The SPRT timer handler is not configured.");

        return callback(
            _timerUserData,
            ulong.MaxValue);
    }

    private static Span<byte> GetBufferSlot(
        SprtChannelState state,
        int slot,
        int length) {
        int offset =
            checked(slot * state.MaximumPayloadBytes);

        return state.Buffer.AsSpan(offset, length);
    }

    private static ReadOnlySpan<byte> GetBufferSlot(
        SprtChannelState state,
        int slot,
        ushort length) {
        int offset =
            checked(slot * state.MaximumPayloadBytes);

        return state.Buffer.AsSpan(offset, length);
    }

    private static void ValidateParameters(
        SprtChannelParameters[] parameters) {
        for (int channel = 0;
             channel < SprtConstants.ChannelCount;
             channel++) {
            SprtChannelParameters value =
                parameters[channel];

            if (!IsPayloadSizeValid(
                    channel,
                    value.PayloadBytes)) {
                throw new ArgumentOutOfRangeException(
                    nameof(parameters),
                    $"Channel {channel} payload size is invalid.");
            }

            if (!IsWindowSizeValid(
                    channel,
                    value.WindowSize)) {
                throw new ArgumentOutOfRangeException(
                    nameof(parameters),
                    $"Channel {channel} window size is invalid.");
            }
        }
    }

    private static bool IsPayloadSizeValid(
        int channel,
        int value) {
        if (!IsValidChannel(channel))
            return false;

        SprtChannelLimits limits =
            ChannelLimits[channel];

        return value >= limits.MinimumPayloadBytes &&
               value <= limits.MaximumPayloadBytes;
    }

    private static bool IsWindowSizeValid(
        int channel,
        int value) {
        if (!IsValidChannel(channel))
            return false;

        SprtChannelLimits limits =
            ChannelLimits[channel];

        return value >= limits.MinimumWindowSize &&
               value <= limits.MaximumWindowSize;
    }

    private static bool IsValidChannel(int channel) {
        return (uint)channel <
               SprtConstants.ChannelCount;
    }

    private static bool IsReliableChannel(int channel) {
        return channel is
            (int)SprtTransmissionChannel.ReliableSequenced or
            (int)SprtTransmissionChannel.ExpeditedReliableSequenced;
    }

    private static ushort IncrementSequence(ushort value) {
        return unchecked((ushort)
            ((value + 1) &
             SprtConstants.SequenceNumberMask));
    }

    private static ulong AddTimeout(
        ulong timestamp,
        int timeout) {
        return unchecked(
            timestamp + (ulong)timeout);
    }

    private static ushort ReadUInt16BigEndian(
        ReadOnlySpan<byte> source,
        int offset) {
        return unchecked((ushort)
            ((source[offset] << 8) |
             source[offset + 1]));
    }

    private static void WriteUInt16BigEndian(
        Span<byte> destination,
        int offset,
        ushort value) {
        destination[offset] =
            unchecked((byte)(value >> 8));

        destination[offset + 1] =
            unchecked((byte)value);
    }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}

/// <summary>
/// Compatibility facade retaining the original C function names.
/// </summary>
public static class SprtApi {
    /// <summary>
    /// Performs the <c>sprt_transmission_channel_to_str</c> operation.
    /// </summary>
    /// <param name="channel">The channel value.</param>
    /// <returns>The result of the operation.</returns>
    public static string sprt_transmission_channel_to_str(
        int channel) =>
        SprtState.TransmissionChannelToString(channel);

    /// <summary>
    /// Performs the <c>sprt_timer_expired</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="now">The now value.</param>
    /// <returns>The result of the operation.</returns>
    public static int sprt_timer_expired(
        SprtState state,
        ulong now) {
        ArgumentNullException.ThrowIfNull(state);
        return state.TimerExpired(now);
    }

    /// <summary>
    /// Performs the <c>sprt_rx_packet</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="packet">The packet value.</param>
    /// <returns>The result of the operation.</returns>
    public static int sprt_rx_packet(
        SprtState state,
        ReadOnlySpan<byte> packet) {
        ArgumentNullException.ThrowIfNull(state);
        return state.ReceivePacket(packet);
    }

    /// <summary>
    /// Performs the <c>sprt_tx</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="channel">The channel value.</param>
    /// <param name="payload">The payload value.</param>
    /// <returns>The result of the operation.</returns>
    public static int sprt_tx(
        SprtState state,
        int channel,
        ReadOnlySpan<byte> payload) {
        ArgumentNullException.ThrowIfNull(state);
        return state.TransmitMessage(channel, payload);
    }

    /// <summary>
    /// Performs the <c>sprt_set_local_tc_windows_size</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="channel">The channel value.</param>
    /// <param name="size">The size value.</param>
    /// <returns>The result of the operation.</returns>
    public static int sprt_set_local_tc_windows_size(
        SprtState state,
        int channel,
        int size) {
        ArgumentNullException.ThrowIfNull(state);
        return state.SetLocalWindowSize(channel, size);
    }

    /// <summary>
    /// Performs the <c>sprt_get_local_tc_windows_size</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="channel">The channel value.</param>
    /// <returns>The result of the operation.</returns>
    public static int sprt_get_local_tc_windows_size(
        SprtState state,
        int channel) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetLocalWindowSize(channel);
    }

    /// <summary>
    /// Performs the <c>sprt_set_local_tc_payload_bytes</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="channel">The channel value.</param>
    /// <param name="maximumLength">The maximum length value.</param>
    /// <returns>The result of the operation.</returns>
    public static int sprt_set_local_tc_payload_bytes(
        SprtState state,
        int channel,
        int maximumLength) {
        ArgumentNullException.ThrowIfNull(state);
        return state.SetLocalPayloadBytes(
            channel,
            maximumLength);
    }

    /// <summary>
    /// Performs the <c>sprt_get_local_tc_payload_bytes</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="channel">The channel value.</param>
    /// <returns>The result of the operation.</returns>
    public static int sprt_get_local_tc_payload_bytes(
        SprtState state,
        int channel) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetLocalPayloadBytes(channel);
    }

    /// <summary>
    /// Performs the <c>sprt_set_local_tc_max_tries</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="channel">The channel value.</param>
    /// <param name="maximumTries">The maximum tries value.</param>
    /// <returns>The result of the operation.</returns>
    public static int sprt_set_local_tc_max_tries(
        SprtState state,
        int channel,
        int maximumTries) {
        ArgumentNullException.ThrowIfNull(state);
        return state.SetLocalMaximumTries(
            channel,
            maximumTries);
    }

    /// <summary>
    /// Performs the <c>sprt_get_local_tc_max_tries</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="channel">The channel value.</param>
    /// <returns>The result of the operation.</returns>
    public static int sprt_get_local_tc_max_tries(
        SprtState state,
        int channel) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetLocalMaximumTries(channel);
    }

    /// <summary>
    /// Performs the <c>sprt_set_far_tc_windows_size</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="channel">The channel value.</param>
    /// <param name="size">The size value.</param>
    /// <returns>The result of the operation.</returns>
    public static int sprt_set_far_tc_windows_size(
        SprtState state,
        int channel,
        int size) {
        ArgumentNullException.ThrowIfNull(state);
        return state.SetFarWindowSize(channel, size);
    }

    /// <summary>
    /// Performs the <c>sprt_get_far_tc_windows_size</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="channel">The channel value.</param>
    /// <returns>The result of the operation.</returns>
    public static int sprt_get_far_tc_windows_size(
        SprtState state,
        int channel) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetFarWindowSize(channel);
    }

    /// <summary>
    /// Performs the <c>sprt_set_far_tc_payload_bytes</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="channel">The channel value.</param>
    /// <param name="maximumLength">The maximum length value.</param>
    /// <returns>The result of the operation.</returns>
    public static int sprt_set_far_tc_payload_bytes(
        SprtState state,
        int channel,
        int maximumLength) {
        ArgumentNullException.ThrowIfNull(state);
        return state.SetFarPayloadBytes(
            channel,
            maximumLength);
    }

    /// <summary>
    /// Performs the <c>sprt_get_far_tc_payload_bytes</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="channel">The channel value.</param>
    /// <returns>The result of the operation.</returns>
    public static int sprt_get_far_tc_payload_bytes(
        SprtState state,
        int channel) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetFarPayloadBytes(channel);
    }

    /// <summary>
    /// Performs the <c>sprt_set_tc_timeout</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="channel">The channel value.</param>
    /// <param name="timer">The timer value.</param>
    /// <param name="timeout">The timeout value.</param>
    /// <returns>The result of the operation.</returns>
    public static int sprt_set_tc_timeout(
        SprtState state,
        int channel,
        int timer,
        int timeout) {
        ArgumentNullException.ThrowIfNull(state);

        return state.SetTimeout(
            channel,
            (SprtTimer)timer,
            timeout);
    }

    /// <summary>
    /// Performs the <c>sprt_get_tc_timeout</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="channel">The channel value.</param>
    /// <param name="timer">The timer value.</param>
    /// <returns>The result of the operation.</returns>
    public static int sprt_get_tc_timeout(
        SprtState state,
        int channel,
        int timer) {
        ArgumentNullException.ThrowIfNull(state);

        return state.GetTimeout(
            channel,
            (SprtTimer)timer);
    }

    /// <summary>
    /// Performs the <c>sprt_set_local_busy</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="channel">The channel value.</param>
    /// <param name="busy">The busy value.</param>
    /// <returns>The result of the operation.</returns>
    public static int sprt_set_local_busy(
        SprtState state,
        int channel,
        bool busy) {
        ArgumentNullException.ThrowIfNull(state);
        return state.SetLocalBusy(channel, busy);
    }

    /// <summary>
    /// Performs the <c>sprt_get_far_busy_status</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="channel">The channel value.</param>
    /// <returns>The result of the operation.</returns>
    public static bool sprt_get_far_busy_status(
        SprtState state,
        int channel) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetFarBusyStatus(channel);
    }

    /// <summary>
    /// Performs the <c>sprt_get_logging_state</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static SprtLogger sprt_get_logging_state(
        SprtState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Logging;
    }

    /// <summary>
    /// Performs the <c>sprt_init</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="subsessionId">The subsession id value.</param>
    /// <param name="receivePayloadType">The receive payload type value.</param>
    /// <param name="transmitPayloadType">The transmit payload type value.</param>
    /// <param name="parameters">The parameters value.</param>
    /// <param name="transmitPacketHandler">The transmit packet handler value.</param>
    /// <param name="transmitUserData">The transmit user data value.</param>
    /// <param name="receiveDeliveryHandler">The receive delivery handler value.</param>
    /// <param name="receiveUserData">The receive user data value.</param>
    /// <param name="timerHandler">The timer handler value.</param>
    /// <param name="timerUserData">The timer user data value.</param>
    /// <param name="statusHandler">The status handler value.</param>
    /// <param name="statusUserData">The status user data value.</param>
    /// <returns>The result of the operation.</returns>
    public static SprtState sprt_init(
        SprtState? state,
        byte subsessionId,
        byte receivePayloadType,
        byte transmitPayloadType,
        SprtChannelParameters[]? parameters,
        SprtTransmitPacketHandler transmitPacketHandler,
        object? transmitUserData,
        SprtReceiveDeliveryHandler receiveDeliveryHandler,
        object? receiveUserData,
        SprtTimerHandler timerHandler,
        object? timerUserData,
        SprtStatusHandler statusHandler,
        object? statusUserData) {
        state ??= new SprtState();

        state.Initialize(
            subsessionId,
            receivePayloadType,
            transmitPayloadType,
            parameters,
            transmitPacketHandler,
            transmitUserData,
            receiveDeliveryHandler,
            receiveUserData,
            timerHandler,
            timerUserData,
            statusHandler,
            statusUserData);

        return state;
    }

    /// <summary>
    /// Performs the <c>sprt_release</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int sprt_release(SprtState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    /// <summary>
    /// Performs the <c>sprt_free</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int sprt_free(SprtState? state) {
        if (state is null)
            return 0;

        int result = state.Release();
        state.Dispose();
        return result;
    }
}
