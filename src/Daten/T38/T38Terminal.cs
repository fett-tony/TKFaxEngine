/*
 * TKFaxEngine - managed C# port
 *
 * Combined port of t38_terminal.c and t38_terminal.h.
 * T.30 is owned directly by the terminal state, matching spanDSP.
 */

using CoreT30 = TKFaxEngine.Daten.T30.T30;
using CoreT30Api = TKFaxEngine.Daten.T30.T30Api;
using CoreT30Logging = TKFaxEngine.Daten.T30.T30Logging;
using CoreT30State = TKFaxEngine.Daten.T30.T30State;
using CoreT30FrontEndStatus = TKFaxEngine.Daten.T30.T30FrontEndStatus;
using CoreT30ModemType = TKFaxEngine.Daten.T30.T30ModemType;
using CoreT30SupportedModems = TKFaxEngine.Daten.T30.T30SupportedModems;

namespace TKFaxEngine.Daten.T38;

[Flags]
public enum T38TerminalOptions {
    None = 0,
    NoPacing = 0x01,
    RegularIndicators = 0x02,
    TwoSecondRepeatingIndicators = 0x04,
    NoIndicators = 0x08
}

public enum T30ModemType {
    None = 0,
    Pause,
    Ced,
    Cng,
    V21,
    V27Ter,
    V29,
    V17,
    V34Hdx,
    Done
}

public enum T30FrontEndStatus {
    SendStepComplete = 0,
    ReceiveComplete,
    SignalPresent,
    SignalAbsent,
    CedPresent,
    CngPresent
}

[Flags]
public enum T30IafMode {
    None = 0,
    T37 = 0x01,
    T38 = 0x02,
    FlowControl = 0x04,
    ContinuousFlow = 0x08,
    NoTcf = 0x10,
    NoFillBits = 0x20,
    NoIndicators = 0x40,
    RelaxedTimers = 0x80
}

[Flags]
public enum T30SupportedModems {
    None = 0,
    V27Ter = 0x01,
    V29 = 0x02,
    V17 = 0x04,
    V34 = 0x08,
    Iaf = 0x10
}

public sealed class T38TerminalHdlcRxState {
    public byte[] Buffer { get; } = new byte[T38TerminalState.MaxHdlcLength];
    public int Length { get; set; }
}

public sealed class T38TerminalHdlcTxState {
    public byte[] Buffer { get; } = new byte[T38TerminalState.MaxHdlcLength];
    public int Length { get; set; }
    public int Pointer { get; set; }
    public int ExtraBits { get; set; }
}

public sealed class T38TerminalFrontEndState {
    public T38CoreState T38 { get; set; } = new();
    public int TimedStep { get; set; }
    public int QueuedTimedStep { get; set; }
    public bool RxDataMissing { get; set; }
    public int OctetsPerDataPacket { get; set; }
    public T38TerminalHdlcRxState HdlcRx { get; } = new();
    public T38TerminalHdlcTxState HdlcTx { get; } = new();
    public int NonEcmTrailerBytes { get; set; }
    public int NextTxIndicator { get; set; }
    public int CurrentTxDataType { get; set; }
    public bool RxSignalPresent { get; set; }
    public int CurrentRxType { get; set; }
    public int CurrentTxType { get; set; }
    public int TxBitRate { get; set; }
    public int Samples { get; set; }
    public int NextTxSamples { get; set; }
    public int TimeoutTxSamples { get; set; }
    public int TimeoutRxSamples { get; set; }
}

public sealed class T38TerminalState {
    public const int MaxHdlcLength = T38Terminal.T38_MAX_HDLC_LEN;

    public CoreT30State T30 { get; } = new();
    public T38TerminalFrontEndState FrontEnd { get; } = new();
    public T38Log Logging { get; } = new();
    public bool CallingParty { get; set; }
}

public static class T38Terminal {
    // Exact TKFaxEngineFX identifiers from t38_terminal.c/t38_terminal.h.
    public const T38TerminalOptions T38_TERMINAL_OPTION_NO_PACING = T38TerminalOptions.NoPacing;
    public const T38TerminalOptions T38_TERMINAL_OPTION_REGULAR_INDICATORS = T38TerminalOptions.RegularIndicators;
    public const T38TerminalOptions T38_TERMINAL_OPTION_2S_REPEATING_INDICATORS = T38TerminalOptions.TwoSecondRepeatingIndicators;
    public const T38TerminalOptions T38_TERMINAL_OPTION_NO_INDICATORS = T38TerminalOptions.NoIndicators;
    public const int T38_MAX_HDLC_LEN = 260;
    private const int INDICATOR_TX_COUNT = 3;
    private const int DATA_TX_COUNT = 1;
    private const int DATA_END_TX_COUNT = 3;
    private const int MAX_OCTETS_PER_UNPACED_CHUNK = 300;
    private const int MID_RX_TIMEOUT = 15000;

    private const int IndicatorTxCount = INDICATOR_TX_COUNT;
    private const int DataTxCount = DATA_TX_COUNT;
    private const int DataEndTxCount = DATA_END_TX_COUNT;
    private const int MaxOctetsPerUnpacedChunk = MAX_OCTETS_PER_UNPACED_CHUNK;
    private const int MidRxTimeoutMilliseconds = MID_RX_TIMEOUT;
    private const int SampleRate = 8_000;

    private const int TimedStepNone = 0;
    private const int TimedStepNonEcmModem = 0x10;
    private const int TimedStepNonEcmModem2 = 0x11;
    private const int TimedStepNonEcmModem3 = 0x12;
    private const int TimedStepNonEcmModem4 = 0x13;
    private const int TimedStepNonEcmModem5 = 0x14;
    private const int TimedStepHdlcModem = 0x20;
    private const int TimedStepHdlcModem2 = 0x21;
    private const int TimedStepHdlcModem3 = 0x22;
    private const int TimedStepHdlcModem4 = 0x23;
    private const int TimedStepHdlcModem5 = 0x24;
    private const int TimedStepCed = 0x30;
    private const int TimedStepCed2 = 0x31;
    private const int TimedStepCed3 = 0x32;
    private const int TimedStepCng = 0x40;
    private const int TimedStepCng2 = 0x41;
    private const int TimedStepPause = 0x50;
    private const int TimedStepNoSignal = 0x60;

    private static int front_end_status(T38TerminalState state, T30FrontEndStatus status) {
        CoreT30.t30_front_end_status(state.T30, (CoreT30FrontEndStatus)(int)status);
        return state.FrontEnd.TimedStep == TimedStepNone ? -1 : 0;
    }

    private static void hdlc_accept_frame(
        T38TerminalState state,
        ReadOnlyMemory<byte>? message,
        int lengthOrStatus,
        bool ok) {
        CoreT30.t30_hdlc_accept(
            state.T30,
            message.HasValue ? message.Value.Span : ReadOnlySpan<byte>.Empty,
            lengthOrStatus,
            ok ? 1 : 0);
    }

    private static int extra_bits_in_stuffed_frame(
        ReadOnlySpan<byte> buffer,
        int length) {
        int ones = 0;
        int stuffed = 0;

        for (int octet = 0; octet < length; octet++) {
            byte value = buffer[octet];
            int bitStream = value;
            for (int bit = 0; bit < 8; bit++) {
                if ((bitStream & 1) != 0) {
                    if (++ones >= 5) {
                        ones = 0;
                        stuffed++;
                    }
                } else {
                    ones = 0;
                }
                bitStream >>= 1;
            }
        }

        return stuffed + 16 + 3 + 16;
    }

    private static int process_rx_missing(
        T38CoreState core,
        object? userData,
        int rxSequenceNumber,
        int expectedSequenceNumber) {
        _ = core;
        _ = rxSequenceNumber;
        _ = expectedSequenceNumber;
        if (userData is T38TerminalState state)
            state.FrontEnd.RxDataMissing = true;
        return 0;
    }

    private static int process_rx_indicator(
        T38CoreState core,
        object? userData,
        T38Indicator indicator) {
        if (userData is not T38TerminalState state)
            return -1;

        T38TerminalFrontEndState frontEnd = state.FrontEnd;
        if (frontEnd.CurrentRxType == (int)T30ModemType.Done)
            return 0;

        if (core.CurrentRxIndicator == (int)indicator)
            return 0;

        switch (indicator) {
            case T38Indicator.NoSignal:
                if (core.CurrentRxIndicator == (int)T38Indicator.V21Preamble
                    && (frontEnd.CurrentRxType == (int)T30ModemType.V21
                        || frontEnd.CurrentRxType == (int)T30ModemType.Cng)) {
                    hdlc_accept_frame(state, null, SignalStatus.CarrierDown, true);
                }
                frontEnd.TimeoutRxSamples = 0;
                front_end_status(state, T30FrontEndStatus.SignalAbsent);
                break;

            case T38Indicator.Cng:
                front_end_status(state, T30FrontEndStatus.CngPresent);
                break;

            case T38Indicator.Ced:
                front_end_status(state, T30FrontEndStatus.CedPresent);
                break;

            case T38Indicator.V34ControlChannel1200:
            case T38Indicator.V21Preamble:
                frontEnd.TimeoutRxSamples = frontEnd.Samples
                    + MillisecondsToSamples(MidRxTimeoutMilliseconds);
                front_end_status(state, T30FrontEndStatus.SignalPresent);
                break;

            case T38Indicator.V27Ter2400Training:
            case T38Indicator.V27Ter4800Training:
            case T38Indicator.V29_7200Training:
            case T38Indicator.V29_9600Training:
            case T38Indicator.V17_7200ShortTraining:
            case T38Indicator.V17_7200LongTraining:
            case T38Indicator.V17_9600ShortTraining:
            case T38Indicator.V17_9600LongTraining:
            case T38Indicator.V17_12000ShortTraining:
            case T38Indicator.V17_12000LongTraining:
            case T38Indicator.V17_14400ShortTraining:
            case T38Indicator.V17_14400LongTraining:
            case T38Indicator.V34PrimaryChannel:
            case T38Indicator.V33_12000Training:
            case T38Indicator.V33_14400Training:
                frontEnd.TimeoutRxSamples = frontEnd.Samples
                    + MillisecondsToSamples(MidRxTimeoutMilliseconds);
                front_end_status(state, T30FrontEndStatus.SignalPresent);
                break;

            case T38Indicator.V8Ansam:
            case T38Indicator.V8Signal:
            case T38Indicator.V34ControlChannelRetrain:
                break;

            default:
                front_end_status(state, T30FrontEndStatus.SignalAbsent);
                break;
        }

        frontEnd.HdlcRx.Length = 0;
        frontEnd.RxDataMissing = false;
        return 0;
    }

    private static int fake_rx_indicator(
        T38CoreState core,
        T38TerminalState state,
        T38Indicator indicator) {
        int result = process_rx_indicator(core, state, indicator);
        core.CurrentRxIndicator = (int)indicator;
        return result;
    }

    private static void process_hdlc_data(
        T38TerminalFrontEndState frontEnd,
        ReadOnlySpan<byte> buffer,
        int length) {
        if (frontEnd.HdlcRx.Length + length > T38TerminalState.MaxHdlcLength) {
            frontEnd.RxDataMissing = true;
            return;
        }

        for (int i = 0; i < length; i++)
            frontEnd.HdlcRx.Buffer[frontEnd.HdlcRx.Length + i] = ReverseBits(buffer[i]);
        frontEnd.HdlcRx.Length += length;
    }

    private static int process_rx_data(
        T38CoreState core,
        object? userData,
        T38DataType dataType,
        T38FieldType fieldType,
        ReadOnlyMemory<byte> field) {
        if (userData is not T38TerminalState state)
            return -1;

        T38TerminalFrontEndState frontEnd = state.FrontEnd;
        if (frontEnd.CurrentRxType == (int)T30ModemType.Done)
            return 0;

        ReadOnlySpan<byte> data = field.Span;
        if (dataType == T38DataType.V8) {
            switch (fieldType) {
                case T38FieldType.CmMessage:
                    state.Logging.Flow(data.Length >= 1
                        ? $"CM profile {data[0] - (byte)'0'} - {T38Core.t38_cm_profile_to_str(data[0])}"
                        : $"Bad length for CM message - {data.Length}");
                    break;
                case T38FieldType.JmMessage:
                    state.Logging.Flow(data.Length >= 2
                        ? $"JM - {T38Core.t38_jm_to_str(data.ToArray(), data.Length)}"
                        : $"Bad length for JM message - {data.Length}");
                    break;
                case T38FieldType.CiMessage:
                    state.Logging.Flow(data.Length >= 1
                        ? $"CI 0x{data[0]:X}"
                        : $"Bad length for CI message - {data.Length}");
                    break;
            }
            return 0;
        }

        if (dataType == T38DataType.V34PrimaryRate) {
            if (fieldType == T38FieldType.V34Rate) {
                if (data.Length >= 3) {
                    frontEnd.T38.V34Rate = T38Core.t38_v34rate_to_bps(data.ToArray(), data.Length);
                    state.Logging.Flow($"V.34 rate {frontEnd.T38.V34Rate} bps");
                } else {
                    state.Logging.Flow($"Bad length for V34rate message - {data.Length}");
                }
            }
            return 0;
        }

        switch (fieldType) {
            case T38FieldType.HdlcData:
                if (frontEnd.TimeoutRxSamples == 0) {
                    fake_rx_indicator(core, state, T38Indicator.V21Preamble);
                    if (data.Length == 0 || data[0] != 0xFF)
                        frontEnd.RxDataMissing = true;
                }
                if (data.Length > 0)
                    process_hdlc_data(frontEnd, data, data.Length);
                frontEnd.TimeoutRxSamples = frontEnd.Samples
                    + MillisecondsToSamples(MidRxTimeoutMilliseconds);
                break;

            case T38FieldType.HdlcFcsOk:
                if (data.Length > 0) {
                    state.Logging.Warning("There is data in a T38_FIELD_HDLC_FCS_OK.");
                    process_hdlc_data(frontEnd, data, data.Length);
                }
                PostHdlcFrame(state, ok: !frontEnd.RxDataMissing, "CRC OK");
                frontEnd.RxDataMissing = false;
                frontEnd.TimeoutRxSamples = frontEnd.Samples
                    + MillisecondsToSamples(MidRxTimeoutMilliseconds);
                break;

            case T38FieldType.HdlcFcsBad:
                if (data.Length > 0) {
                    state.Logging.Warning("There is data in a T38_FIELD_HDLC_FCS_BAD.");
                    process_hdlc_data(frontEnd, data, data.Length);
                }
                PostHdlcFrame(state, ok: false, "CRC bad");
                frontEnd.RxDataMissing = false;
                frontEnd.TimeoutRxSamples = frontEnd.Samples
                    + MillisecondsToSamples(MidRxTimeoutMilliseconds);
                break;

            case T38FieldType.HdlcFcsOkSignalEnd:
                if (data.Length > 0) {
                    state.Logging.Warning("There is data in a T38_FIELD_HDLC_FCS_OK_SIG_END.");
                    process_hdlc_data(frontEnd, data, data.Length);
                }
                PostHdlcFrame(state, ok: !frontEnd.RxDataMissing, "CRC OK, sig end");
                frontEnd.RxDataMissing = false;
                if (core.CurrentRxDataType != (int)dataType
                    || core.CurrentRxFieldType != (int)fieldType) {
                    hdlc_accept_frame(state, null, SignalStatus.CarrierDown, true);
                }
                fake_rx_indicator(core, state, T38Indicator.NoSignal);
                break;

            case T38FieldType.HdlcFcsBadSignalEnd:
                if (data.Length > 0) {
                    state.Logging.Warning("There is data in a T38_FIELD_HDLC_FCS_BAD_SIG_END.");
                    process_hdlc_data(frontEnd, data, data.Length);
                }
                PostHdlcFrame(state, ok: false, "CRC bad, sig end");
                frontEnd.RxDataMissing = false;
                if (core.CurrentRxDataType != (int)dataType
                    || core.CurrentRxFieldType != (int)fieldType) {
                    hdlc_accept_frame(state, null, SignalStatus.CarrierDown, true);
                }
                fake_rx_indicator(core, state, T38Indicator.NoSignal);
                break;

            case T38FieldType.HdlcSignalEnd:
                if (data.Length > 0)
                    state.Logging.Warning("There is data in a T38_FIELD_HDLC_SIG_END.");
                if (core.CurrentRxDataType != (int)dataType
                    || core.CurrentRxFieldType != (int)fieldType) {
                    frontEnd.HdlcRx.Length = 0;
                    frontEnd.RxDataMissing = false;
                    front_end_status(state, T30FrontEndStatus.ReceiveComplete);
                }
                fake_rx_indicator(core, state, T38Indicator.NoSignal);
                break;

            case T38FieldType.T4NonEcmData:
                if (!frontEnd.RxSignalPresent) {
                    CoreT30.t30_non_ecm_put_bit(state.T30, SignalStatus.TrainingSucceeded);
                    frontEnd.RxSignalPresent = true;
                }
                if (data.Length > 0)
                    CoreT30.t30_non_ecm_put(state.T30, ReverseBits(data), data.Length);
                frontEnd.TimeoutRxSamples = frontEnd.Samples
                    + MillisecondsToSamples(MidRxTimeoutMilliseconds);
                break;

            case T38FieldType.T4NonEcmSignalEnd:
                if (core.CurrentRxDataType != (int)dataType
                    || core.CurrentRxFieldType != (int)fieldType) {
                    if (data.Length > 0) {
                        if (!frontEnd.RxSignalPresent) {
                            CoreT30.t30_non_ecm_put_bit(state.T30, SignalStatus.TrainingSucceeded);
                            frontEnd.RxSignalPresent = true;
                        }
                        CoreT30.t30_non_ecm_put(state.T30, ReverseBits(data), data.Length);
                    }
                    front_end_status(state, T30FrontEndStatus.ReceiveComplete);
                }
                frontEnd.RxSignalPresent = false;
                fake_rx_indicator(core, state, T38Indicator.NoSignal);
                break;
        }

        return 0;
    }

    private static void PostHdlcFrame(T38TerminalState state, bool ok, string description) {
        T38TerminalFrontEndState frontEnd = state.FrontEnd;
        if (frontEnd.HdlcRx.Length <= 0)
            return;

        string frameType = frontEnd.HdlcRx.Length >= 3
            ? CoreT30Logging.t30_frametype(frontEnd.HdlcRx.Buffer[2])
            : "???";
        string receiveState = frontEnd.RxDataMissing ? "missing octets" : "clean";
        state.Logging.Flow($"Type {frameType} - {description} ({receiveState})");

        byte[] frame = new byte[frontEnd.HdlcRx.Length];
        Array.Copy(frontEnd.HdlcRx.Buffer, frame, frame.Length);
        hdlc_accept_frame(state, frame, frame.Length, ok);
        frontEnd.HdlcRx.Length = 0;
    }

    private static void send_hdlc(
        T38TerminalState state,
        ReadOnlyMemory<byte>? message,
        int length) {
        T38TerminalHdlcTxState tx = state.FrontEnd.HdlcTx;
        if (length == 0) {
            tx.Length = -1;
            return;
        }
        if (length == -1) {
            tx.Length = 0;
            tx.Pointer = 0;
            return;
        }
        if (message is null || length < 0 || length > message.Value.Length)
            throw new ArgumentOutOfRangeException(nameof(length));
        if (length > tx.Buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(length), "HDLC frame exceeds 260 bytes.");

        ReadOnlySpan<byte> data = message.Value.Span[..length];
        if (state.FrontEnd.T38.PaceTransmission)
            tx.ExtraBits = extra_bits_in_stuffed_frame(data, data.Length);

        for (int i = 0; i < length; i++)
            tx.Buffer[i] = ReverseBits(data[i]);
        tx.Length = length;
        tx.Pointer = 0;
    }

    private static int bits_to_microseconds(T38TerminalState state, int bits) {
        T38TerminalFrontEndState frontEnd = state.FrontEnd;
        if (!frontEnd.T38.PaceTransmission || frontEnd.TxBitRate == 0)
            return 0;
        return (int)((long)bits * 1_000_000 / frontEnd.TxBitRate);
    }

    private static void set_octets_per_data_packet(T38TerminalState state, int bitRate) {
        T38TerminalFrontEndState frontEnd = state.FrontEnd;
        frontEnd.TxBitRate = bitRate;
        if (frontEnd.T38.PaceTransmission) {
            frontEnd.OctetsPerDataPacket =
                (frontEnd.T38.MicrosecondsPerTxChunk / 1000) * bitRate / (8 * 1000);
            if (frontEnd.OctetsPerDataPacket < 1)
                frontEnd.OctetsPerDataPacket = 1;
        } else {
            frontEnd.OctetsPerDataPacket = MaxOctetsPerUnpacedChunk;
        }
    }

    private static int set_no_signal(T38TerminalState state) {
        T38TerminalFrontEndState frontEnd = state.FrontEnd;
        if ((frontEnd.T38.ChunkingModes & T38ChunkingMode.SendRegularIndicators) != 0) {
            int delay = T38Core.t38_core_send_indicator(
                frontEnd.T38,
                0x100 | (int)T38Indicator.NoSignal);
            if (delay < 0)
                return delay;

            frontEnd.TimedStep = TimedStepNoSignal;
            frontEnd.TimeoutTxSamples =
                (frontEnd.T38.ChunkingModes & T38ChunkingMode.SendTwoSecondRegularIndicators) != 0
                    ? frontEnd.NextTxSamples + MicrosecondsToSamples(2_000_000)
                    : 0;
            return frontEnd.T38.MicrosecondsPerTxChunk;
        }

        int singleDelay = T38Core.t38_core_send_indicator(
            frontEnd.T38,
            (int)T38Indicator.NoSignal);
        if (singleDelay < 0)
            return singleDelay;
        frontEnd.TimedStep = TimedStepNone;
        return singleDelay;
    }

    private static int stream_no_signal(T38TerminalState state) {
        T38TerminalFrontEndState frontEnd = state.FrontEnd;
        int delay = T38Core.t38_core_send_indicator(
            frontEnd.T38,
            0x100 | (int)T38Indicator.NoSignal);
        if (delay < 0)
            return delay;

        if (frontEnd.TimeoutTxSamples != 0
            && frontEnd.NextTxSamples >= frontEnd.TimeoutTxSamples) {
            frontEnd.TimedStep = TimedStepNone;
        }
        return frontEnd.T38.MicrosecondsPerTxChunk;
    }

    private static int stream_non_ecm(T38TerminalState state) {
        T38TerminalFrontEndState frontEnd = state.FrontEnd;
        byte[] buffer = new byte[MaxOctetsPerUnpacedChunk + 50];
        int delay = 0;

        do {
            switch (frontEnd.TimedStep) {
                case TimedStepNonEcmModem:
                    if (frontEnd.T38.CurrentTxIndicator != (int)T38Indicator.NoSignal) {
                        delay = T38Core.t38_core_send_indicator(
                            frontEnd.T38,
                            (int)T38Indicator.NoSignal);
                        if (delay < 0)
                            return delay;
                    } else if (frontEnd.T38.PaceTransmission) {
                        delay = 75_000;
                    }

                    frontEnd.TimedStep = TimedStepNonEcmModem2;
                    frontEnd.TimeoutTxSamples = frontEnd.NextTxSamples
                        + MicrosecondsToSamples(T38Core.t38_core_send_training_delay(
                            frontEnd.T38,
                            frontEnd.NextTxIndicator));
                    frontEnd.NextTxSamples = frontEnd.Samples;
                    break;

                case TimedStepNonEcmModem2:
                    if ((frontEnd.T38.ChunkingModes & T38ChunkingMode.SendRegularIndicators) != 0) {
                        delay = T38Core.t38_core_send_indicator(
                            frontEnd.T38,
                            0x100 | frontEnd.NextTxIndicator);
                        if (delay < 0)
                            return delay;
                        if (frontEnd.NextTxSamples >= frontEnd.TimeoutTxSamples)
                            frontEnd.TimedStep = TimedStepNonEcmModem3;
                        return frontEnd.T38.MicrosecondsPerTxChunk;
                    }

                    delay = T38Core.t38_core_send_indicator(
                        frontEnd.T38,
                        frontEnd.NextTxIndicator);
                    if (delay < 0)
                        return delay;
                    frontEnd.TimedStep = TimedStepNonEcmModem3;
                    break;

                case TimedStepNonEcmModem3: {
                        int length = CoreT30.t30_non_ecm_get(
                            state.T30,
                            buffer.AsSpan(0, frontEnd.OctetsPerDataPacket),
                            frontEnd.OctetsPerDataPacket);
                        if (length < 0)
                            return -1;
                        if (length > frontEnd.OctetsPerDataPacket)
                            length = frontEnd.OctetsPerDataPacket;
                        ReverseBitsInPlace(buffer.AsSpan(0, length));

                        if (length < frontEnd.OctetsPerDataPacket) {
                            if (frontEnd.T38.PaceTransmission) {
                                Array.Clear(
                                    buffer,
                                    length,
                                    frontEnd.OctetsPerDataPacket - length);
                                frontEnd.NonEcmTrailerBytes =
                                    3 * frontEnd.OctetsPerDataPacket + length;
                                length = frontEnd.OctetsPerDataPacket;
                                frontEnd.TimedStep = TimedStepNonEcmModem4;
                            } else {
                                int result = T38Core.t38_core_send_data(
                                    frontEnd.T38,
                                    frontEnd.CurrentTxDataType,
                                    (int)T38FieldType.T4NonEcmSignalEnd,
                                    buffer,
                                    length,
                                    (int)T38PacketCategory.ImageDataEnd);
                                if (result < 0)
                                    return result;
                                frontEnd.TimedStep = TimedStepNonEcmModem5;
                                if (front_end_status(state, T30FrontEndStatus.SendStepComplete) < 0)
                                    return -1;
                                break;
                            }
                        }

                        int sendResult = T38Core.t38_core_send_data(
                            frontEnd.T38,
                            frontEnd.CurrentTxDataType,
                            (int)T38FieldType.T4NonEcmData,
                            buffer,
                            length,
                            (int)T38PacketCategory.ImageData);
                        if (sendResult < 0)
                            return sendResult;
                        if (frontEnd.T38.PaceTransmission)
                            delay = bits_to_microseconds(state, 8 * length);
                        break;
                    }

                case TimedStepNonEcmModem4: {
                        int length = frontEnd.OctetsPerDataPacket;
                        frontEnd.NonEcmTrailerBytes -= frontEnd.OctetsPerDataPacket;
                        if (frontEnd.NonEcmTrailerBytes <= 0) {
                            length += frontEnd.NonEcmTrailerBytes;
                            Array.Clear(buffer, 0, length);
                            int result = T38Core.t38_core_send_data(
                                frontEnd.T38,
                                frontEnd.CurrentTxDataType,
                                (int)T38FieldType.T4NonEcmSignalEnd,
                                buffer,
                                length,
                                (int)T38PacketCategory.ImageDataEnd);
                            if (result < 0)
                                return result;
                            frontEnd.TimedStep = TimedStepNonEcmModem5;
                            if (frontEnd.T38.PaceTransmission)
                                delay = bits_to_microseconds(state, 8 * length) + 60_000;
                            if (front_end_status(state, T30FrontEndStatus.SendStepComplete) < 0)
                                return -1;
                            break;
                        }

                        Array.Clear(buffer, 0, length);
                        int paddingResult = T38Core.t38_core_send_data(
                            frontEnd.T38,
                            frontEnd.CurrentTxDataType,
                            (int)T38FieldType.T4NonEcmData,
                            buffer,
                            length,
                            (int)T38PacketCategory.ImageData);
                        if (paddingResult < 0)
                            return paddingResult;
                        if (frontEnd.T38.PaceTransmission)
                            delay = bits_to_microseconds(state, 8 * length);
                        break;
                    }

                case TimedStepNonEcmModem5:
                    delay = set_no_signal(state);
                    if (frontEnd.QueuedTimedStep != TimedStepNone) {
                        frontEnd.TimedStep = frontEnd.QueuedTimedStep;
                        frontEnd.QueuedTimedStep = TimedStepNone;
                    } else {
                        frontEnd.TimedStep = TimedStepNone;
                    }
                    return delay;

                default:
                    return delay;
            }
        }
        while (delay == 0);

        return delay;
    }

    private static int stream_hdlc(T38TerminalState state) {
        T38TerminalFrontEndState frontEnd = state.FrontEnd;
        byte[] temporary = new byte[MaxOctetsPerUnpacedChunk + 50];
        int delay = 0;

        do {
            switch (frontEnd.TimedStep) {
                case TimedStepHdlcModem:
                    if (frontEnd.T38.CurrentTxIndicator != (int)T38Indicator.NoSignal) {
                        delay = T38Core.t38_core_send_indicator(
                            frontEnd.T38,
                            (int)T38Indicator.NoSignal);
                        if (delay < 0)
                            return delay;
                    } else {
                        delay = frontEnd.T38.PaceTransmission ? 75_000 : 0;
                    }

                    frontEnd.TimedStep = TimedStepHdlcModem2;
                    frontEnd.TimeoutTxSamples = frontEnd.NextTxSamples
                        + MicrosecondsToSamples(T38Core.t38_core_send_training_delay(
                            frontEnd.T38,
                            frontEnd.NextTxIndicator))
                        + MicrosecondsToSamples(T38Core.t38_core_send_flags_delay(
                            frontEnd.T38,
                            frontEnd.NextTxIndicator))
                        + MicrosecondsToSamples(delay);
                    frontEnd.NextTxSamples = frontEnd.Samples;
                    break;

                case TimedStepHdlcModem2:
                    if ((frontEnd.T38.ChunkingModes & T38ChunkingMode.SendRegularIndicators) != 0) {
                        delay = T38Core.t38_core_send_indicator(
                            frontEnd.T38,
                            0x100 | frontEnd.NextTxIndicator);
                        if (delay < 0)
                            return delay;
                        if (frontEnd.NextTxSamples >= frontEnd.TimeoutTxSamples)
                            frontEnd.TimedStep = TimedStepHdlcModem3;
                        return frontEnd.T38.MicrosecondsPerTxChunk;
                    }

                    delay = T38Core.t38_core_send_indicator(
                        frontEnd.T38,
                        frontEnd.NextTxIndicator);
                    if (delay < 0)
                        return delay;
                    delay += T38Core.t38_core_send_flags_delay(
                        frontEnd.T38,
                        frontEnd.NextTxIndicator);
                    frontEnd.TimedStep = TimedStepHdlcModem3;
                    break;

                case TimedStepHdlcModem3: {
                        int remaining = frontEnd.HdlcTx.Length - frontEnd.HdlcTx.Pointer;
                        if (remaining < 0)
                            return -1;

                        int sent;
                        if (frontEnd.OctetsPerDataPacket >= remaining) {
                            sent = remaining;
                            if ((frontEnd.T38.ChunkingModes & T38ChunkingMode.MergeFcsWithData) != 0) {
                                Array.Copy(
                                    frontEnd.HdlcTx.Buffer,
                                    frontEnd.HdlcTx.Pointer,
                                    temporary,
                                    0,
                                    sent);

                                frontEnd.HdlcTx.Pointer = 0;
                                frontEnd.HdlcTx.Length = 0;
                                if (front_end_status(state, T30FrontEndStatus.SendStepComplete) < 0)
                                    return -1;

                                bool anotherFrame = frontEnd.HdlcTx.Length >= 0;
                                T38FieldType finalType = anotherFrame
                                    ? T38FieldType.HdlcFcsOk
                                    : T38FieldType.HdlcFcsOkSignalEnd;
                                T38PacketCategory category = frontEnd.CurrentTxDataType == (int)T38DataType.V21
                                    ? (anotherFrame
                                        ? T38PacketCategory.ControlData
                                        : T38PacketCategory.ControlDataEnd)
                                    : (anotherFrame
                                        ? T38PacketCategory.ImageData
                                        : T38PacketCategory.ImageDataEnd);

                                var fields = new[]
                                {
                                new T38DataField(
                                    T38FieldType.HdlcData,
                                    new ReadOnlyMemory<byte>(temporary, 0, sent)),
                                new T38DataField(finalType, ReadOnlyMemory<byte>.Empty)
                            };

                                int multiFieldResult = T38Core.t38_core_send_data_multi_field(
                                    frontEnd.T38,
                                    frontEnd.CurrentTxDataType,
                                    fields,
                                    fields.Length,
                                    (int)category);
                                if (multiFieldResult < 0)
                                    return multiFieldResult;

                                if (anotherFrame) {
                                    frontEnd.TimedStep = TimedStepHdlcModem3;
                                    delay = bits_to_microseconds(
                                        state,
                                        sent * 8 + frontEnd.HdlcTx.ExtraBits);
                                } else {
                                    frontEnd.TimedStep = TimedStepHdlcModem5;
                                    delay = bits_to_microseconds(
                                        state,
                                        sent * 8 + frontEnd.HdlcTx.ExtraBits);
                                    if (frontEnd.T38.PaceTransmission)
                                        delay += 100_000;
                                    if (front_end_status(state, T30FrontEndStatus.SendStepComplete) < 0)
                                        return -1;
                                }
                                break;
                            }

                            byte[] chunk = new byte[sent];
                            Array.Copy(
                                frontEnd.HdlcTx.Buffer,
                                frontEnd.HdlcTx.Pointer,
                                chunk,
                                0,
                                sent);
                            int result = T38Core.t38_core_send_data(
                                frontEnd.T38,
                                frontEnd.CurrentTxDataType,
                                (int)T38FieldType.HdlcData,
                                chunk,
                                sent,
                                (int)(frontEnd.CurrentTxDataType == (int)T38DataType.V21
                                    ? T38PacketCategory.ControlData
                                    : T38PacketCategory.ImageData));
                            if (result < 0)
                                return result;
                            frontEnd.TimedStep = TimedStepHdlcModem4;
                        } else {
                            sent = frontEnd.OctetsPerDataPacket;
                            byte[] chunk = new byte[sent];
                            Array.Copy(
                                frontEnd.HdlcTx.Buffer,
                                frontEnd.HdlcTx.Pointer,
                                chunk,
                                0,
                                sent);
                            int result = T38Core.t38_core_send_data(
                                frontEnd.T38,
                                frontEnd.CurrentTxDataType,
                                (int)T38FieldType.HdlcData,
                                chunk,
                                sent,
                                (int)(frontEnd.CurrentTxDataType == (int)T38DataType.V21
                                    ? T38PacketCategory.ControlData
                                    : T38PacketCategory.ImageData));
                            if (result < 0)
                                return result;
                            frontEnd.HdlcTx.Pointer += sent;
                        }

                        delay = bits_to_microseconds(state, sent * 8);
                        break;
                    }

                case TimedStepHdlcModem4: {
                        int previousDataType = frontEnd.CurrentTxDataType;
                        frontEnd.HdlcTx.Pointer = 0;
                        frontEnd.HdlcTx.Length = 0;
                        if (front_end_status(state, T30FrontEndStatus.SendStepComplete) < 0)
                            return -1;

                        if (frontEnd.HdlcTx.Length >= 0) {
                            if (frontEnd.HdlcTx.Length == 0)
                                state.Logging.Flow("No new frame or end transmission condition.");

                            int result = T38Core.t38_core_send_data(
                                frontEnd.T38,
                                previousDataType,
                                (int)T38FieldType.HdlcFcsOk,
                                null,
                                0,
                                (int)(frontEnd.CurrentTxDataType == (int)T38DataType.V21
                                    ? T38PacketCategory.ControlData
                                    : T38PacketCategory.ImageData));
                            if (result < 0)
                                return result;
                            frontEnd.TimedStep = TimedStepHdlcModem3;
                            delay = bits_to_microseconds(state, frontEnd.HdlcTx.ExtraBits);
                        } else {
                            int result = T38Core.t38_core_send_data(
                                frontEnd.T38,
                                previousDataType,
                                (int)T38FieldType.HdlcFcsOkSignalEnd,
                                null,
                                0,
                                (int)(frontEnd.CurrentTxDataType == (int)T38DataType.V21
                                    ? T38PacketCategory.ControlDataEnd
                                    : T38PacketCategory.ImageDataEnd));
                            if (result < 0)
                                return result;
                            frontEnd.TimedStep = TimedStepHdlcModem5;
                            delay = bits_to_microseconds(state, frontEnd.HdlcTx.ExtraBits);
                            if (frontEnd.T38.PaceTransmission)
                                delay += 100_000;
                            if (front_end_status(state, T30FrontEndStatus.SendStepComplete) < 0)
                                return -1;
                        }
                        break;
                    }

                case TimedStepHdlcModem5:
                    delay = set_no_signal(state);
                    if (frontEnd.QueuedTimedStep != TimedStepNone) {
                        frontEnd.TimedStep = frontEnd.QueuedTimedStep;
                        frontEnd.QueuedTimedStep = TimedStepNone;
                    } else {
                        frontEnd.TimedStep = TimedStepNone;
                    }
                    return delay;

                default:
                    return delay;
            }
        }
        while (delay == 0);

        return delay;
    }

    private static int stream_ced(T38TerminalState state) {
        T38TerminalFrontEndState frontEnd = state.FrontEnd;
        int delay = 0;

        do {
            switch (frontEnd.TimedStep) {
                case TimedStepCed:
                    frontEnd.TimedStep = TimedStepCed2;
                    delay = T38Core.t38_core_send_indicator(
                        frontEnd.T38,
                        (int)T38Indicator.NoSignal);
                    if (delay < 0)
                        return delay;
                    delay = frontEnd.T38.PaceTransmission ? 200_000 : 0;
                    frontEnd.NextTxSamples = frontEnd.Samples;
                    break;

                case TimedStepCed2:
                    frontEnd.TimedStep = TimedStepCed3;
                    delay = T38Core.t38_core_send_indicator(
                        frontEnd.T38,
                        (int)T38Indicator.Ced);
                    if (delay < 0)
                        return delay;
                    frontEnd.CurrentTxDataType = (int)T38DataType.None;
                    break;

                case TimedStepCed3:
                    frontEnd.TimedStep = frontEnd.QueuedTimedStep;
                    if (front_end_status(state, T30FrontEndStatus.SendStepComplete) < 0)
                        return -1;
                    return 0;

                default:
                    return delay;
            }
        }
        while (delay == 0);

        return delay;
    }

    private static int stream_cng(T38TerminalState state) {
        T38TerminalFrontEndState frontEnd = state.FrontEnd;
        int delay = 0;

        do {
            switch (frontEnd.TimedStep) {
                case TimedStepCng:
                    frontEnd.TimedStep = TimedStepCng2;
                    delay = T38Core.t38_core_send_indicator(
                        frontEnd.T38,
                        (int)T38Indicator.NoSignal);
                    if (delay < 0)
                        return delay;
                    delay = frontEnd.T38.PaceTransmission ? 200_000 : 0;
                    frontEnd.NextTxSamples = frontEnd.Samples;
                    break;

                case TimedStepCng2:
                    delay = T38Core.t38_core_send_indicator(
                        frontEnd.T38,
                        (int)T38Indicator.Cng);
                    frontEnd.TimedStep = frontEnd.QueuedTimedStep;
                    frontEnd.CurrentTxDataType = (int)T38DataType.None;
                    return delay;

                default:
                    return delay;
            }
        }
        while (delay == 0);

        return delay;
    }

    public static int t38_terminal_send_timeout(T38TerminalState state, int samples) {
        ArgumentNullException.ThrowIfNull(state);
        T38TerminalFrontEndState frontEnd = state.FrontEnd;

        if (frontEnd.CurrentRxType == (int)T30ModemType.Done
            || frontEnd.CurrentTxType == (int)T30ModemType.Done) {
            return 1;
        }

        frontEnd.Samples += samples;
        CoreT30.t30_timer_update(state.T30, samples);

        if (frontEnd.TimeoutRxSamples != 0
            && frontEnd.Samples > frontEnd.TimeoutRxSamples) {
            state.Logging.Flow("Timeout mid-receive");
            frontEnd.TimeoutRxSamples = 0;
            front_end_status(state, T30FrontEndStatus.ReceiveComplete);
        }

        if (frontEnd.TimedStep == TimedStepNone)
            return 0;
        if (frontEnd.T38.PaceTransmission
            && frontEnd.Samples < frontEnd.NextTxSamples) {
            return 0;
        }

        int delay = 0;
        switch (frontEnd.TimedStep & 0xFFF0) {
            case TimedStepNonEcmModem:
                delay = stream_non_ecm(state);
                break;
            case TimedStepHdlcModem:
                delay = stream_hdlc(state);
                break;
            case TimedStepCed:
                delay = stream_ced(state);
                break;
            case TimedStepCng:
                delay = stream_cng(state);
                break;
            case TimedStepPause:
                frontEnd.TimedStep = TimedStepNone;
                front_end_status(state, T30FrontEndStatus.SendStepComplete);
                break;
            case TimedStepNoSignal:
                delay = stream_no_signal(state);
                break;
        }

        if (delay < 0) {
            CoreT30.t30_terminate(state.T30);
            return 1;
        }

        frontEnd.NextTxSamples += MicrosecondsToSamples(delay);
        return 0;
    }

    private static void set_rx_type(
        T38TerminalState state,
        T30ModemType type,
        int bitRate,
        int shortTrain,
        bool useHdlc) {
        _ = bitRate;
        _ = shortTrain;
        _ = useHdlc;
        state.Logging.Flow($"Set rx type {(int)type}");
        state.FrontEnd.CurrentRxType = (int)type;
    }

    private static void start_tx(T38TerminalFrontEndState frontEnd, bool useHdlc) {
        int step = useHdlc ? TimedStepHdlcModem : TimedStepNonEcmModem;
        if (frontEnd.TimedStep == TimedStepNone) {
            frontEnd.QueuedTimedStep = TimedStepNone;
            frontEnd.TimedStep = step;
        } else {
            frontEnd.QueuedTimedStep = step;
        }

        if (frontEnd.NextTxSamples < frontEnd.Samples)
            frontEnd.NextTxSamples = frontEnd.Samples;
    }

    private static void set_tx_type(
        T38TerminalState state,
        T30ModemType type,
        int bitRate,
        int shortTrain,
        bool useHdlc) {
        ArgumentNullException.ThrowIfNull(state);
        T38TerminalFrontEndState frontEnd = state.FrontEnd;
        state.Logging.Flow($"Set tx type {(int)type}");
        if (frontEnd.CurrentTxType == (int)type)
            return;

        set_octets_per_data_packet(state, bitRate);
        switch (type) {
            case T30ModemType.None:
                if (frontEnd.TimedStep != TimedStepNonEcmModem5
                    && frontEnd.TimedStep != TimedStepHdlcModem5) {
                    frontEnd.TimedStep = TimedStepNone;
                }
                frontEnd.CurrentTxDataType = (int)T38DataType.None;
                break;

            case T30ModemType.Pause:
                frontEnd.NextTxSamples = frontEnd.T38.PaceTransmission
                    ? frontEnd.Samples + MillisecondsToSamples(shortTrain)
                    : frontEnd.Samples;
                if (frontEnd.TimedStep == TimedStepNone) {
                    frontEnd.QueuedTimedStep = TimedStepNone;
                    frontEnd.TimedStep = TimedStepPause;
                } else {
                    frontEnd.QueuedTimedStep = TimedStepPause;
                }
                frontEnd.CurrentTxDataType = (int)T38DataType.None;
                break;

            case T30ModemType.Ced:
                frontEnd.NextTxSamples = frontEnd.Samples;
                frontEnd.TimedStep = TimedStepCed;
                frontEnd.CurrentTxDataType = (int)T38DataType.None;
                break;

            case T30ModemType.Cng:
                frontEnd.NextTxSamples = frontEnd.Samples;
                frontEnd.TimedStep = TimedStepCng;
                frontEnd.CurrentTxDataType = (int)T38DataType.None;
                break;

            case T30ModemType.V21:
                frontEnd.NextTxIndicator = (int)T38Indicator.V21Preamble;
                frontEnd.CurrentTxDataType = (int)T38DataType.V21;
                start_tx(frontEnd, useHdlc);
                break;

            case T30ModemType.V27Ter:
                if (bitRate == 2400) {
                    frontEnd.NextTxIndicator = (int)T38Indicator.V27Ter2400Training;
                    frontEnd.CurrentTxDataType = (int)T38DataType.V27Ter2400;
                } else if (bitRate == 4800) {
                    frontEnd.NextTxIndicator = (int)T38Indicator.V27Ter4800Training;
                    frontEnd.CurrentTxDataType = (int)T38DataType.V27Ter4800;
                }
                start_tx(frontEnd, useHdlc);
                break;

            case T30ModemType.V29:
                if (bitRate == 7200) {
                    frontEnd.NextTxIndicator = (int)T38Indicator.V29_7200Training;
                    frontEnd.CurrentTxDataType = (int)T38DataType.V29_7200;
                } else if (bitRate == 9600) {
                    frontEnd.NextTxIndicator = (int)T38Indicator.V29_9600Training;
                    frontEnd.CurrentTxDataType = (int)T38DataType.V29_9600;
                }
                start_tx(frontEnd, useHdlc);
                break;

            case T30ModemType.V17:
                switch (bitRate) {
                    case 7200:
                        frontEnd.NextTxIndicator = (int)(shortTrain != 0
                            ? T38Indicator.V17_7200ShortTraining
                            : T38Indicator.V17_7200LongTraining);
                        frontEnd.CurrentTxDataType = (int)T38DataType.V17_7200;
                        break;
                    case 9600:
                        frontEnd.NextTxIndicator = (int)(shortTrain != 0
                            ? T38Indicator.V17_9600ShortTraining
                            : T38Indicator.V17_9600LongTraining);
                        frontEnd.CurrentTxDataType = (int)T38DataType.V17_9600;
                        break;
                    case 12000:
                        frontEnd.NextTxIndicator = (int)(shortTrain != 0
                            ? T38Indicator.V17_12000ShortTraining
                            : T38Indicator.V17_12000LongTraining);
                        frontEnd.CurrentTxDataType = (int)T38DataType.V17_12000;
                        break;
                    case 14400:
                        frontEnd.NextTxIndicator = (int)(shortTrain != 0
                            ? T38Indicator.V17_14400ShortTraining
                            : T38Indicator.V17_14400LongTraining);
                        frontEnd.CurrentTxDataType = (int)T38DataType.V17_14400;
                        break;
                }
                start_tx(frontEnd, useHdlc);
                break;

            case T30ModemType.Done:
                state.Logging.Flow("FAX exchange complete");
                frontEnd.TimedStep = TimedStepNone;
                frontEnd.CurrentTxDataType = (int)T38DataType.None;
                break;
        }

        frontEnd.CurrentTxType = (int)type;
    }

    public static void t38_terminal_set_config(T38TerminalState state, int configuration) {
        ArgumentNullException.ThrowIfNull(state);
        T38TerminalOptions options = (T38TerminalOptions)configuration;
        T38TerminalFrontEndState frontEnd = state.FrontEnd;

        bool noIndicators = (options & T38TerminalOptions.NoIndicators) != 0;
        if ((options & T38TerminalOptions.NoPacing) != 0) {
            T38Core.t38_set_pace_transmission(frontEnd.T38, 0);
            frontEnd.HdlcTx.ExtraBits = 0;
            T38Core.t38_set_redundancy_control(
                frontEnd.T38,
                (int)T38PacketCategory.Indicator,
                noIndicators ? 0 : 1);
            T38Core.t38_set_redundancy_control(frontEnd.T38, (int)T38PacketCategory.ControlData, 1);
            T38Core.t38_set_redundancy_control(frontEnd.T38, (int)T38PacketCategory.ControlDataEnd, 1);
            T38Core.t38_set_redundancy_control(frontEnd.T38, (int)T38PacketCategory.ImageData, 1);
            T38Core.t38_set_redundancy_control(frontEnd.T38, (int)T38PacketCategory.ImageDataEnd, 1);
            frontEnd.T38.ChunkingModes &= ~T38ChunkingMode.SendRegularIndicators;
            frontEnd.T38.ChunkingModes |= T38ChunkingMode.MergeFcsWithData;
        } else {
            T38Core.t38_set_pace_transmission(frontEnd.T38, 1);
            frontEnd.HdlcTx.ExtraBits = 0;
            T38Core.t38_set_redundancy_control(
                frontEnd.T38,
                (int)T38PacketCategory.Indicator,
                noIndicators ? 0 : IndicatorTxCount);
            T38Core.t38_set_redundancy_control(
                frontEnd.T38,
                (int)T38PacketCategory.ControlData,
                DataTxCount);
            T38Core.t38_set_redundancy_control(
                frontEnd.T38,
                (int)T38PacketCategory.ControlDataEnd,
                DataEndTxCount);
            T38Core.t38_set_redundancy_control(
                frontEnd.T38,
                (int)T38PacketCategory.ImageData,
                DataTxCount);
            T38Core.t38_set_redundancy_control(
                frontEnd.T38,
                (int)T38PacketCategory.ImageDataEnd,
                DataEndTxCount);

            bool regular = (options
                & (T38TerminalOptions.RegularIndicators
                   | T38TerminalOptions.TwoSecondRepeatingIndicators)) != 0;
            if (regular)
                frontEnd.T38.ChunkingModes |= T38ChunkingMode.SendRegularIndicators;
            else
                frontEnd.T38.ChunkingModes &= ~T38ChunkingMode.SendRegularIndicators;

            if ((options & T38TerminalOptions.TwoSecondRepeatingIndicators) != 0)
                frontEnd.T38.ChunkingModes |= T38ChunkingMode.SendTwoSecondRegularIndicators;
            else
                frontEnd.T38.ChunkingModes &= ~T38ChunkingMode.SendTwoSecondRegularIndicators;
        }

        set_octets_per_data_packet(state, 300);
    }

    public static void t38_terminal_set_tep_mode(T38TerminalState state, bool useTep) {
        if (useTep)
            state.FrontEnd.T38.ChunkingModes |= T38ChunkingMode.AllowTepTime;
        else
            state.FrontEnd.T38.ChunkingModes &= ~T38ChunkingMode.AllowTepTime;
        T38Core.t38_set_tep_handling(state.FrontEnd.T38, useTep);
    }

    public static void t38_terminal_set_fill_bit_removal(T38TerminalState state, bool remove) {
        if (remove)
            state.FrontEnd.T38.Iaf |= (int)T30IafMode.NoFillBits;
        else
            state.FrontEnd.T38.Iaf &= ~(int)T30IafMode.NoFillBits;
        CoreT30Api.t30_set_iaf_mode(state.T30, state.FrontEnd.T38.Iaf);
    }

    public static CoreT30State t38_terminal_get_t30_state(T38TerminalState state)
        => state.T30;

    public static T38CoreState t38_terminal_get_t38_core_state(T38TerminalState state)
        => state.FrontEnd.T38;

    public static T38Log t38_terminal_get_logging_state(T38TerminalState state)
        => state.Logging;

    public static int t38_terminal_restart(T38TerminalState state, bool callingParty) {
        ArgumentNullException.ThrowIfNull(state);
        t38_terminal_t38_fe_restart(state);
        state.CallingParty = callingParty;
        CoreT30.t30_restart(state.T30, callingParty);
        return 0;
    }

    public static T38TerminalState? t38_terminal_init(
        T38TerminalState? state,
        bool callingParty,
        T38TxPacketHandler? txPacketHandler,
        object? txPacketUserData) {
        if (txPacketHandler is null)
            return null;

        state ??= new T38TerminalState();

        // Equivalent to memset(s, 0, sizeof(*s)) in t38_terminal_init().
        state.Logging.Sink = null;
        state.CallingParty = false;
        T38TerminalFrontEndState frontEnd = state.FrontEnd;
        frontEnd.TimedStep = 0;
        frontEnd.QueuedTimedStep = 0;
        frontEnd.RxDataMissing = false;
        frontEnd.OctetsPerDataPacket = 0;
        Array.Clear(frontEnd.HdlcRx.Buffer);
        frontEnd.HdlcRx.Length = 0;
        Array.Clear(frontEnd.HdlcTx.Buffer);
        frontEnd.HdlcTx.Length = 0;
        frontEnd.HdlcTx.Pointer = 0;
        frontEnd.HdlcTx.ExtraBits = 0;
        frontEnd.NonEcmTrailerBytes = 0;
        frontEnd.NextTxIndicator = 0;
        frontEnd.CurrentTxDataType = 0;
        frontEnd.RxSignalPresent = false;
        frontEnd.CurrentRxType = 0;
        frontEnd.CurrentTxType = 0;
        frontEnd.TxBitRate = 0;
        frontEnd.Samples = 0;
        frontEnd.NextTxSamples = 0;
        frontEnd.TimeoutTxSamples = 0;
        frontEnd.TimeoutRxSamples = 0;
        state.CallingParty = callingParty;

        t38_terminal_t38_fe_init(state, txPacketHandler, txPacketUserData);
        t38_terminal_set_config(state, 0);

        CoreT30.t30_init(
            state.T30,
            callingParty,
            (_, type, bitRate, shortTrain, useHdlc) =>
                set_rx_type(
                    state,
                    (T30ModemType)(int)type,
                    bitRate,
                    shortTrain,
                    useHdlc),
            state,
            (_, type, bitRate, shortTrain, useHdlc) =>
                set_tx_type(
                    state,
                    (T30ModemType)(int)type,
                    bitRate,
                    shortTrain,
                    useHdlc),
            state,
            (_, message, length) => send_hdlc(state, message, length),
            state);
        CoreT30Api.t30_set_iaf_mode(state.T30, state.FrontEnd.T38.Iaf);
        CoreT30Api.t30_set_supported_modems(
            state.T30,
            (int)(CoreT30SupportedModems.V27Ter
                | CoreT30SupportedModems.V29
                | CoreT30SupportedModems.V17
                | CoreT30SupportedModems.Iaf));
        CoreT30.t30_restart(state.T30, callingParty);
        return state;
    }

    public static int t38_terminal_release(T38TerminalState state) {
        ArgumentNullException.ThrowIfNull(state);
        CoreT30.t30_release(state.T30);
        return 0;
    }

    public static int t38_terminal_free(T38TerminalState? state) {
        if (state is not null)
            t38_terminal_release(state);
        return 0;
    }

    private static int t38_terminal_t38_fe_restart(T38TerminalState state) {
        T38TerminalFrontEndState frontEnd = state.FrontEnd;
        T38Core.t38_core_restart(frontEnd.T38);
        frontEnd.CurrentTxType = -1;
        frontEnd.RxSignalPresent = false;
        frontEnd.TimedStep = TimedStepNone;
        frontEnd.T38.Iaf = (int)T30IafMode.T38;
        frontEnd.CurrentTxDataType = (int)T38DataType.None;
        frontEnd.NextTxSamples = 0;
        frontEnd.HdlcTx.Pointer = 0;
        frontEnd.HdlcTx.ExtraBits = 0;
        return 0;
    }

    private static int t38_terminal_t38_fe_init(
        T38TerminalState state,
        T38TxPacketHandler txPacketHandler,
        object? txPacketUserData) {
        T38TerminalFrontEndState frontEnd = state.FrontEnd;
        frontEnd.T38 = T38Core.t38_core_init(
            frontEnd.T38,
            process_rx_indicator,
            process_rx_data,
            process_rx_missing,
            state,
            txPacketHandler,
            txPacketUserData);

        T38Core.t38_set_fastest_image_data_rate(frontEnd.T38, 14_400);
        frontEnd.RxSignalPresent = false;
        frontEnd.TimedStep = TimedStepNone;
        frontEnd.QueuedTimedStep = TimedStepNone;
        frontEnd.T38.Iaf = (int)T30IafMode.T38;
        frontEnd.CurrentTxDataType = (int)T38DataType.None;
        frontEnd.NextTxSamples = 0;
        frontEnd.T38.ChunkingModes = T38ChunkingMode.AllowTepTime;
        frontEnd.HdlcTx.Pointer = 0;
        frontEnd.HdlcTx.ExtraBits = 0;
        return 0;
    }

    private static int MillisecondsToSamples(int milliseconds)
        => checked(milliseconds * (SampleRate / 1000));

    private static int MicrosecondsToSamples(int microseconds)
        => (int)((long)microseconds * SampleRate / 1_000_000);

    private static byte ReverseBits(byte value) {
        value = (byte)(((value & 0x55) << 1) | ((value >> 1) & 0x55));
        value = (byte)(((value & 0x33) << 2) | ((value >> 2) & 0x33));
        return (byte)((value << 4) | (value >> 4));
    }

    private static byte[] ReverseBits(ReadOnlySpan<byte> source) {
        byte[] result = new byte[source.Length];
        for (int i = 0; i < source.Length; i++)
            result[i] = ReverseBits(source[i]);
        return result;
    }

    private static void ReverseBitsInPlace(Span<byte> data) {
        for (int i = 0; i < data.Length; i++)
            data[i] = ReverseBits(data[i]);
    }
}
