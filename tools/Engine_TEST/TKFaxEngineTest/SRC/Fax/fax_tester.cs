/*
 * TKFaxEngine - managed C# port
 *
 * fax_tester.cs - XML driven TSB-85 fax tester.
 *
 * Original implementation written by Steve Underwood.
 * Copyright (C) 2008 Steve Underwood.
 * Licensed under the GNU General Public License version 2.
 *
 * Fresh port from fax_tester.c. The XML cursor, repeat handling, modem
 * selection, HDLC/ECM streaming, non-ECM bit streaming, T.4 image generation,
 * timers and T.30 callbacks follow the native control flow.
 */

#nullable enable
using TKFaxEngine;
using TKFaxEngineTest.Fax.Test;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using TKFaxEngine.Daten.T30;
using TKFaxEngine.Daten.T38;
using TKFaxEngine.FaxImage;
using TKFaxEngine.Modem;
using static TKFaxEngine.AsyncApi;
using static TKFaxEngine.BitOperationsApi;
using static TKFaxEngine.Daten.T30.T30;
using static TKFaxEngine.Daten.T30.T30Api;
using static TKFaxEngine.Daten.T30.T30Logging;
using static TKFaxEngine.FaxApi;
using static TKFaxEngine.FaxImage.t4_rx;
using static TKFaxEngine.FaxImage.t4_tx;
using static TKFaxEngine.Modem.FaxModemsApi;
using FaxT30ModemType = TKFaxEngine.Daten.T30.T30ModemType;
using ModemSignalStatus = TKFaxEngine.SignalStatus;

namespace TKFaxEngineTest.Fax.Test;

public delegate void FaxTesterFlushHandler(
    FaxTesterState state,
    object? userData,
    int which);

/// <summary>
/// Managed equivalent of <c>faxtester_state_t</c>. The members deliberately
/// follow the native state layout so the XML step engine, modem callbacks,
/// timers and image streaming remain directly traceable to fax_tester.c.
/// </summary>
public sealed class FaxTesterState : IDisposable {
    public FaxState? FarFax { get; set; }
    public T38TerminalState? FarT38 { get; set; }
    public int FarTag { get; set; }
    public T38TerminalState? FarT38Fax { get; set; }
    public T30State? FarT30 { get; set; }
    public T30ExchangedInfo ExpectedRxInfo { get; } = new();

    public bool UseReceiverNotReady { get; set; }
    public bool TestLocalInterrupt { get; set; }
    public string ImagePath { get; set; } = ".";

    public XDocument? Document { get; set; }
    public XElement? Current { get; set; }
    public int RepeatMinimum { get; set; }
    public int RepeatMaximum { get; set; }
    public int RepeatCount { get; set; }
    public XElement? RepeatStart { get; set; }
    public XElement? RepeatParent { get; set; }

    public FaxTesterFlushHandler? FlushHandler { get; set; }
    public object? FlushUserData { get; set; }

    public ReadOnlyMemory<byte> ImageBuffer { get; set; }
    public int ImageLength { get; set; }
    public int ImagePointer { get; set; }
    public int ImageBitPointer { get; set; }
    public byte[] Image { get; } = new byte[1_000_000];
    public int EcmFrameSize { get; set; }
    public int CorruptCrc { get; set; }
    public int FinalDelayed { get; set; }

    public FaxModems Modems { get; set; } = null!;
    public bool Transmit { get; set; }
    public bool ShortTrain { get; set; }
    public FaxT30ModemType CurrentRxType { get; set; }
    public FaxT30ModemType CurrentTxType { get; set; }
    public int WaitForSilence { get; set; }
    public int ToneState { get; set; }
    public long ToneOnTime { get; set; }
    public long Timer { get; set; }
    public long Timeout { get; set; }
    public bool TestForCallClear { get; set; }
    public int CallClearTimer { get; set; }
    public bool FarEndClearedCall { get; set; }
    public int TimeInMilliseconds { get; set; } = -1;
    public int TimeoutMilliseconds { get; set; } = -1;
    public byte[] Awaited { get; } = new byte[1000];
    public int AwaitedLength { get; set; }
    public string NextTxFile { get; set; } = string.Empty;

    public bool TransmitOnIdle {
        get => Modems.TransmitOnIdle;
        set => Modems.TransmitOnIdle = value;
    }

    public SpanLogState Logging { get; } = new();
    public Action<string>? LogSink { get; set; }

    internal List<XElement> CurrentSiblings { get; set; } = new();
    internal int CurrentSiblingIndex { get; set; }
    internal List<XElement>? RepeatSiblings { get; set; }
    internal int RepeatParentSiblingIndex { get; set; }

    public void Log(string message) {
        if (LogSink is not null)
            LogSink(message);
        else
            Console.WriteLine(message);
    }

    public void Dispose() {
        Document = null;
        Current = null;
        RepeatStart = null;
        RepeatParent = null;
        CurrentSiblings.Clear();
        RepeatSiblings = null;
        Modems?.Dispose();
        ImageBuffer = ReadOnlyMemory<byte>.Empty;
    }
}

public static class fax_tester {
    private const int HdlcFramingOkThreshold = 5;
    private const long InfiniteTimeout = long.MaxValue;

    private sealed record NodeParameters(
        string? Direction,
        string? Type,
        string? Modem,
        string? Value,
        string? Tag,
        string? BadRows,
        string? CrcError,
        string? Pattern,
        string? TimeIn,
        string? Timeout,
        string? MinimumBits,
        string? FrameSize,
        string? Block,
        string? Compression);

    private static readonly Dictionary<string, T30Error> T30Status =
        Enum.GetValues<T30Error>()
            .ToDictionary(
                static value => ToNativeStatusTag(value),
                static value => value,
                StringComparer.OrdinalIgnoreCase);

    public static int faxtester_rx(FaxTesterState state, short[] amplitude, int length) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(amplitude);
        if ((uint)length > (uint)amplitude.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        for (int i = 0; i < length; i++)
            amplitude[i] = state.Modems.RestoreDc(amplitude[i]);

        state.Modems.ProcessReceive(amplitude, 0, length);
        TimerUpdate(state, length);

        if (state.WaitForSilence != 0 && !state.Modems.RxSignalPresent) {
            state.WaitForSilence = 0;
            FrontEndStepComplete(state);
        }

        return 0;
    }

    public static int faxtester_tx(FaxTesterState state, short[] amplitude, int maximumLength) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(amplitude);
        if ((uint)maximumLength > (uint)amplitude.Length)
            throw new ArgumentOutOfRangeException(nameof(maximumLength));

        int length = 0;
        if (state.Transmit) {
            while (length < maximumLength) {
                int generated = state.Modems.GenerateTransmit(
                    amplitude,
                    length,
                    maximumLength - length);
                length += generated;
                if (length >= maximumLength)
                    break;

                FrontEndStepComplete(state);
                if (!state.Transmit) {
                    if (state.TransmitOnIdle) {
                        Array.Clear(amplitude, length, maximumLength - length);
                        length = maximumLength;
                    }
                    break;
                }

                if (generated == 0)
                    break;
            }
        } else if (state.TransmitOnIdle) {
            Array.Clear(amplitude, 0, maximumLength);
            length = maximumLength;
        }

        return length;
    }

    public static void faxtester_set_rx_type(
        object? userData,
        int type,
        int bitRate,
        int shortTrain,
        bool useHdlc) {
        FaxTesterState state = RequireState(userData);
        FaxT30ModemType modemType = (FaxT30ModemType)type;
        state.Log($"Set rx type {modemType} ({type})");
        if (state.CurrentRxType == modemType)
            return;

        state.CurrentRxType = modemType;
        switch (modemType) {
            case FaxT30ModemType.Ced:
                fax_modems_start_slow_modem(state.Modems, FAX_MODEM_CED_TONE_RX);
                state.ToneState = 0;
                break;
            case FaxT30ModemType.Cng:
                fax_modems_start_slow_modem(state.Modems, FAX_MODEM_CNG_TONE_RX);
                state.ToneState = 0;
                break;
            case FaxT30ModemType.V21:
                state.FlushHandler?.Invoke(state, state.FlushUserData, 3);
                fax_modems_start_slow_modem(state.Modems, FAX_MODEM_V21_RX);
                break;
            case FaxT30ModemType.V27Ter:
                fax_modems_start_fast_modem(
                    state.Modems,
                    FAX_MODEM_V27TER_RX,
                    bitRate,
                    shortTrain,
                    useHdlc ? 1 : 0);
                break;
            case FaxT30ModemType.V29:
                fax_modems_start_fast_modem(
                    state.Modems,
                    FAX_MODEM_V29_RX,
                    bitRate,
                    shortTrain,
                    useHdlc ? 1 : 0);
                break;
            case FaxT30ModemType.V17:
                fax_modems_start_fast_modem(
                    state.Modems,
                    FAX_MODEM_V17_RX,
                    bitRate,
                    shortTrain,
                    useHdlc ? 1 : 0);
                break;
            case FaxT30ModemType.Done:
                state.Log("FAX exchange complete");
                state.Modems.SetReceiveIdle();
                break;
            default:
                state.Modems.SetReceiveIdle();
                break;
        }
    }

    public static void faxtester_set_tx_type(
        object? userData,
        int type,
        int bitRate,
        int shortTrain,
        bool useHdlc) {
        FaxTesterState state = RequireState(userData);
        FaxT30ModemType modemType = (FaxT30ModemType)type;
        state.Log($"Set tx type {modemType} ({type})");

        if (state.CurrentTxType == modemType) {
            if (modemType == FaxT30ModemType.Pause)
                state.Modems.ConfigureTransmitPause(MillisecondsToSamples(shortTrain));
            return;
        }

        switch (modemType) {
            case FaxT30ModemType.Pause:
                state.Modems.ConfigureTransmitPause(MillisecondsToSamples(shortTrain));
                state.Transmit = true;
                break;
            case FaxT30ModemType.Ced:
                state.Modems.ConfigureTransmitTone(FaxModemChannel.CedToneTx);
                state.Transmit = true;
                break;
            case FaxT30ModemType.Cng:
                state.Modems.ConfigureTransmitTone(FaxModemChannel.CngToneTx);
                state.Transmit = true;
                break;
            case FaxT30ModemType.V21:
                state.Modems.StartSlowModem(FaxModemChannel.V21Tx);
                state.Transmit = true;
                break;
            case FaxT30ModemType.V27Ter:
            case FaxT30ModemType.V29:
            case FaxT30ModemType.V17:
                state.Modems.SetGetBit(() => NonEcmGetBit(state));
                state.Modems.StartFastModem(
                    modemType switch {
                        FaxT30ModemType.V27Ter => FaxModemChannel.V27TerTx,
                        FaxT30ModemType.V29 => FaxModemChannel.V29Tx,
                        _ => FaxModemChannel.V17Tx
                    },
                    bitRate,
                    shortTrain != 0,
                    useHdlc);
                fax_modems_hdlc_tx_flags(state.Modems, bitRate / (8 * 5));
                state.Transmit = true;
                break;
            case FaxT30ModemType.Done:
                state.Log("FAX exchange complete");
                state.Modems.StopTransmit();
                state.Transmit = false;
                break;
            default:
                state.Modems.StopTransmit();
                state.Transmit = false;
                break;
        }

        state.CurrentTxType = modemType;
    }

    public static void faxtest_set_rx_silence(FaxTesterState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.WaitForSilence = 1;
    }

    public static void faxtester_send_hdlc_flags(FaxTesterState state, int flags) {
        ArgumentNullException.ThrowIfNull(state);
        fax_modems_hdlc_tx_flags(state.Modems, flags);
    }

    public static void faxtester_send_hdlc_msg(
        FaxTesterState state,
        ReadOnlySpan<byte> message,
        int length,
        bool crcOk) {
        ArgumentNullException.ThrowIfNull(state);
        if ((uint)length > (uint)message.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        state.Modems.TransmitHdlcFrame(message[..length].ToArray());
        if (!crcOk)
            CorruptCurrentHdlcFrame(state.Modems);
    }

    public static void faxtester_set_flush_handler(
        FaxTesterState state,
        FaxTesterFlushHandler? handler,
        object? userData) {
        state.FlushHandler = handler;
        state.FlushUserData = userData;
    }

    public static void faxtester_set_transmit_on_idle(
        FaxTesterState state,
        int transmitOnIdle) =>
        state.TransmitOnIdle = transmitOnIdle != 0;

    public static void faxtester_set_tep_mode(
        FaxTesterState state,
        int useTep) =>
        fax_modems_set_tep_mode(state.Modems, useTep);

    public static void faxtester_set_timeout(FaxTesterState state, int timeout) {
        state.Timeout = timeout >= 0
            ? state.Timer + MillisecondsToSamples(timeout)
            : InfiniteTimeout;
    }

    public static int faxtester_next_step(FaxTesterState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.TestForCallClear = false;

        XElement? step = MoveToNextStep(state);
        if (step is null) {
            if (state.FinalDelayed == 0) {
                faxtester_set_timeout(state, -1);
                faxtester_set_rx_type(state, (int)FaxT30ModemType.None, 0, 0, false);
                faxtester_set_tx_type(state, (int)FaxT30ModemType.Pause, 0, 120_000, false);
                state.FinalDelayed = 1;
                return 1;
            }

            Console.WriteLine("Test passed");
            throw new FaxTesterExitException(0);
        }

        NodeParameters parameters = GetNodeParameters(step);
        state.Log(
            $"Dir - {parameters.Direction ?? " "}, type - {parameters.Type ?? ""}, " +
            $"modem - {parameters.Modem ?? ""}, value - {parameters.Value ?? ""}, " +
            $"timein - {parameters.TimeIn ?? ""}, timeout - {parameters.Timeout ?? ""}, " +
            $"tag - {parameters.Tag ?? ""}");

        if (parameters.Type is null)
            return 1;

        state.TimeInMilliseconds = ParseInt(parameters.TimeIn, -1);
        state.TimeoutMilliseconds = ParseInt(parameters.Timeout, -1);
        string type = parameters.Type;

        if (EqualsIgnoreCase(type, "ANSWER")) {
            StartFarEnd(state, callingParty: true);
            PrepareFarFax(state);
            state.NextTxFile = string.Empty;
            if (state.FarT30 is not null) {
                t30_set_supported_output_compressions(
                    state.FarT30,
                    T4_COMPRESSION_T4_1D);
                if (!string.IsNullOrEmpty(parameters.Value))
                    t30_set_tx_file(state.FarT30, BuildImagePath(state, parameters.Value), -1, -1);
            }
            return 0;
        }

        if (EqualsIgnoreCase(type, "CALL")) {
            StartFarEnd(state, callingParty: false);
            PrepareFarFax(state);
            state.NextTxFile = string.Empty;
            if (state.FarT30 is not null) {
                t30_set_rx_file(state.FarT30, fax_tests.OutputTiffFileName, -1);
                t30_set_supported_output_compressions(
                    state.FarT30,
                    T4_COMPRESSION_T4_1D);
                if (!string.IsNullOrEmpty(parameters.Value))
                    t30_set_tx_file(state.FarT30, BuildImagePath(state, parameters.Value), -1, -1);
            }
            return 0;
        }

        if (EqualsIgnoreCase(type, "STATUS")) {
            VerifyStatus(state, parameters.Value);
            return 0;
        }

        if (EqualsIgnoreCase(type, "WAIT")) {
            faxtester_set_timeout(state, state.TimeoutMilliseconds);
            int delay = ParseInt(parameters.Value, 1);
            faxtester_set_rx_type(state, (int)FaxT30ModemType.None, 0, 0, false);
            faxtester_set_tx_type(state, (int)FaxT30ModemType.Pause, 0, delay, false);
            return 1;
        }

        bool receive = EqualsIgnoreCase(parameters.Direction, "R");
        return receive
            ? ExecuteReceiveStep(state, parameters)
            : ExecuteTransmitStep(state, parameters);
    }

    public static SpanLogState faxtester_get_logging_state(FaxTesterState state) =>
        state.Logging;

    public static bool faxtester_far_call_active(FaxTesterState state) =>
        state.FarT30 is not null && t30_call_active(state.FarT30) != 0;

    public static FaxTesterState? faxtester_init(
        FaxTesterState? state,
        string testFile,
        string test) {
        state ??= new FaxTesterState();
        state.Modems?.Dispose();
        state.Modems = fax_modems_init(
            null,
            0,
            (message, lengthOrStatus, ok) => HdlcAccept(state, message, lengthOrStatus, ok),
            () => HdlcUnderflowHandler(state),
            bit => NonEcmPutBit(state, bit),
            () => NonEcmGetBit(state),
            (tone, level, delay) => ToneDetected(state, tone, level, delay));
        fax_modems_set_tep_mode(state.Modems, 0);
        fax_modems_set_rx_active(state.Modems, 1);
        faxtester_set_timeout(state, -1);
        state.TimeInMilliseconds = -1;
        state.TimeoutMilliseconds = -1;
        faxtester_set_tx_type(state, (int)FaxT30ModemType.None, 0, 0, false);
        state.ImagePath = ".";
        state.NextTxFile = string.Empty;
        state.ExpectedRxInfo.Ident = null;
        state.ExpectedRxInfo.SubAddress = null;
        state.ExpectedRxInfo.SelectivePollingAddress = null;
        state.ExpectedRxInfo.PolledSubAddress = null;
        state.ExpectedRxInfo.SenderIdent = null;
        state.ExpectedRxInfo.Password = null;
        state.ExpectedRxInfo.Nsf = Array.Empty<byte>();
        state.ExpectedRxInfo.Nsc = Array.Empty<byte>();
        state.ExpectedRxInfo.Nss = Array.Empty<byte>();

        if (!GetTestSet(state, testFile, test)) {
            state.Dispose();
            return null;
        }

        return state;
    }

    public static int faxtester_release(FaxTesterState state) {
        state.Document = null;
        state.Current = null;
        state.CurrentSiblings.Clear();
        return 0;
    }

    public static int faxtester_free(FaxTesterState? state) {
        state?.Dispose();
        return 0;
    }

    private static int ExecuteReceiveStep(FaxTesterState state, NodeParameters parameters) {
        if (state.TimeoutMilliseconds < 0)
            state.TimeoutMilliseconds = 7000;
        faxtester_set_timeout(state, state.TimeoutMilliseconds);

        if (!string.IsNullOrEmpty(parameters.Modem)) {
            bool hdlc = EqualsIgnoreCase(parameters.Type, "PREAMBLE");
            bool shortTrain = !EqualsIgnoreCase(parameters.Type, "TCF");
            faxtester_set_tx_type(state, (int)FaxT30ModemType.None, 0, 0, false);
            SelectReceiveModem(state, parameters.Modem, shortTrain, hdlc);
        }

        string type = parameters.Type!;
        if (EqualsIgnoreCase(type, "CED")) {
            faxtester_set_rx_type(state, (int)FaxT30ModemType.Ced, 0, 0, false);
            faxtester_set_tx_type(state, (int)FaxT30ModemType.None, 0, 0, false);
        } else if (EqualsIgnoreCase(type, "CLEAR")) {
            state.Log("Far end should drop the call");
            faxtester_set_rx_type(state, (int)FaxT30ModemType.None, 0, 0, false);
            faxtester_set_tx_type(
                state,
                (int)FaxT30ModemType.Pause,
                0,
                state.TimeoutMilliseconds,
                false);
            state.TestForCallClear = true;
            state.CallClearTimer = 0;
        } else if (EqualsIgnoreCase(type, "CNG")) {
            faxtester_set_rx_type(state, (int)FaxT30ModemType.Cng, 0, 0, false);
            faxtester_set_tx_type(state, (int)FaxT30ModemType.None, 0, 0, false);
        } else if (EqualsIgnoreCase(type, "HDLC")) {
            byte[] message = new byte[1024];
            byte[] mask = new byte[1024];
            int length = StringToMessage(message, mask, parameters.Value ?? string.Empty);
            ReverseBytes(state.Awaited, message, Math.Abs(length));
            state.AwaitedLength = length;
        } else if (EqualsIgnoreCase(type, "SET")) {
            SetExpectedInfo(state.ExpectedRxInfo, parameters.Tag, parameters.Value);
            return 0;
        } else if (EqualsIgnoreCase(type, "SILENCE")) {
            faxtest_set_rx_silence(state);
        } else if (!EqualsIgnoreCase(type, "MSG") &&
                   !EqualsIgnoreCase(type, "PP") &&
                   !EqualsIgnoreCase(type, "TCF") &&
                   !EqualsIgnoreCase(type, "PREAMBLE")) {
            state.Log($"Unrecognised type '{type}'");
            return 0;
        }

        return 1;
    }

    private static int ExecuteTransmitStep(FaxTesterState state, NodeParameters parameters) {
        faxtester_set_timeout(state, state.TimeoutMilliseconds);
        if (!string.IsNullOrEmpty(parameters.Modem)) {
            bool hdlc = EqualsIgnoreCase(parameters.Type, "PREAMBLE");
            bool shortTrain = !EqualsIgnoreCase(parameters.Type, "TCF");
            faxtester_set_rx_type(state, (int)FaxT30ModemType.None, 0, 0, false);
            SelectTransmitModem(state, parameters.Modem, shortTrain, hdlc);
        }

        string type = parameters.Type!;
        if (EqualsIgnoreCase(type, "CED")) {
            faxtester_set_rx_type(state, (int)FaxT30ModemType.None, 0, 0, false);
            faxtester_set_tx_type(state, (int)FaxT30ModemType.Ced, 0, 0, false);
        } else if (EqualsIgnoreCase(type, "CLEAR")) {
            state.Log("Time to drop the call");
            faxtester_set_rx_type(state, (int)FaxT30ModemType.None, 0, 0, false);
            faxtester_set_tx_type(
                state,
                (int)FaxT30ModemType.Pause,
                0,
                state.TimeoutMilliseconds,
                false);
            if (state.FarT30 is not null)
                t30_terminate(state.FarT30);
            return 0;
        } else if (EqualsIgnoreCase(type, "CNG")) {
            faxtester_set_rx_type(state, (int)FaxT30ModemType.None, 0, 0, false);
            faxtester_set_tx_type(state, (int)FaxT30ModemType.Cng, 0, 0, false);
        } else if (EqualsIgnoreCase(type, "HDLC")) {
            byte[] message = new byte[1024];
            byte[] mask = new byte[1024];
            int length = StringToMessage(message, mask, parameters.Value ?? string.Empty);
            ReverseBytes(message, message, Math.Abs(length));
            bool crcOk = !EqualsIgnoreCase(parameters.CrcError, "0");
            faxtester_send_hdlc_msg(state, message, Math.Abs(length), crcOk);
        } else if (EqualsIgnoreCase(type, "MSG")) {
            LoadImage(state, parameters, ecm: false);
        } else if (EqualsIgnoreCase(type, "POSTAMBLE")) {
            faxtester_send_hdlc_flags(state, ParseInt(parameters.Value, 5));
        } else if (EqualsIgnoreCase(type, "PP")) {
            LoadImage(state, parameters, ecm: true);
        } else if (EqualsIgnoreCase(type, "PREAMBLE")) {
            faxtester_send_hdlc_flags(state, ParseInt(parameters.Value, 37));
        } else if (EqualsIgnoreCase(type, "SET")) {
            SetFarT30Value(state, parameters.Tag, parameters.Value);
            return 0;
        } else if (EqualsIgnoreCase(type, "TCF")) {
            int length = ParseInt(parameters.Value, 450);
            if ((uint)length > (uint)state.Image.Length)
                throw new InvalidDataException("TCF length exceeds the fax tester image buffer.");
            int patterned = Math.Clamp(ParseInt(parameters.Pattern, 0), 0, length);
            if (patterned > 0)
                Array.Fill(state.Image, (byte)0x55, 0, patterned);
            if (length > patterned)
                Array.Clear(state.Image, patterned, length - patterned);
            state.ImagePointer = 0;
            state.ImageBitPointer = 8;
            state.ImageBuffer = state.Image.AsMemory(0, length);
            state.ImageLength = length;
        } else {
            state.Log($"Unrecognised type '{type}'");
            return 0;
        }

        return 1;
    }

    private static void TimerUpdate(FaxTesterState state, int length) {
        state.Timer += length;
        if (state.Timer <= state.Timeout)
            return;

        state.Timeout = InfiniteTimeout;
        state.Log("FAX tester step timed out");
        Console.WriteLine("Test failed");
        throw new FaxTesterExitException(2);
    }

    private static void FrontEndStepComplete(FaxTesterState state) {
        while (faxtester_next_step(state) == 0) {
        }
    }

    private static int PhaseBHandler(object? userData, int result) {
        FaxTesterState state = RequireState(userData);
        T30State t30 = RequireFarT30(state);
        int status = (int)T30Error.Ok;
        char channel = (char)state.FarTag;

        status = CompareRemoteString(
            channel,
            "ident",
            state.ExpectedRxInfo.Ident,
            t30_get_rx_ident(t30),
            T30Error.IdentUnacceptable,
            status);
        status = CompareRemoteString(
            channel,
            "sub-address",
            state.ExpectedRxInfo.SubAddress,
            t30_get_rx_sub_address(t30),
            T30Error.SubUnacceptable,
            status);
        status = CompareRemoteString(
            channel,
            "polled sub-address",
            state.ExpectedRxInfo.PolledSubAddress,
            t30_get_rx_polled_sub_address(t30),
            T30Error.PsaUnacceptable,
            status);
        status = CompareRemoteString(
            channel,
            "selective polling address",
            state.ExpectedRxInfo.SelectivePollingAddress,
            t30_get_rx_selective_polling_address(t30),
            T30Error.SepUnacceptable,
            status);
        status = CompareRemoteString(
            channel,
            "sender ident",
            state.ExpectedRxInfo.SenderIdent,
            t30_get_rx_sender_ident(t30),
            T30Error.SidUnacceptable,
            status);
        status = CompareRemoteString(
            channel,
            "password",
            state.ExpectedRxInfo.Password,
            t30_get_rx_password(t30),
            T30Error.PwdUnacceptable,
            status);

        Console.WriteLine(
            $"{channel}: Phase B handler on channel {channel} - " +
            $"(0x{result:X}) {FrameType(result)}");
        return status;
    }

    private static int PhaseDHandler(object? userData, int result) {
        FaxTesterState state = RequireState(userData);
        T30State t30 = RequireFarT30(state);
        char channel = (char)state.FarTag;
        string tag = $"{channel}: Phase D";
        Console.WriteLine(
            $"{channel}: Phase D handler on channel {channel} - " +
            $"(0x{result:X}) {FrameType(result)}");
        fax_utils.fax_log_page_transfer_statistics(t30, tag);
        fax_utils.fax_log_tx_parameters(t30, tag);
        fax_utils.fax_log_rx_parameters(t30, tag);
        if (state.UseReceiverNotReady)
            t30_set_receiver_not_ready(t30, 3);
        if (state.TestLocalInterrupt)
            t30_local_interrupt_request(t30, 1);
        return (int)T30Error.Ok;
    }

    private static void PhaseEHandler(object? userData, int result) {
        FaxTesterState state = RequireState(userData);
        T30State t30 = RequireFarT30(state);
        char channel = (char)state.FarTag;
        string tag = $"{channel}: Phase E";
        Console.WriteLine(
            $"{channel}: Phase E handler on channel {channel} - " +
            $"({result}) {t30_completion_code_to_str(result)}");
        fax_utils.fax_log_final_transfer_statistics(t30, tag);
        fax_utils.fax_log_tx_parameters(t30, tag);
        fax_utils.fax_log_rx_parameters(t30, tag);
    }

    private static void T30RealTimeFrameHandler(
        object? userData,
        bool incoming,
        ReadOnlyMemory<byte> message) {
        _ = userData;
        if (message.Length < 3)
            return;
        Console.Error.WriteLine(
            $"T.30: Real time frame handler - " +
            $"{(incoming ? "line->T.30" : "T.30->line")}, " +
            $"{t30_frametype(message.Span[2])}, length = {message.Length}");
    }

    private static int DocumentHandler(object? userData, int documentEvent) {
        FaxTesterState state = RequireState(userData);
        char channel = (char)state.FarTag;
        Console.Error.WriteLine(
            $"{channel}: Document handler on channel {channel} - event {documentEvent}");
        if (state.NextTxFile.Length == 0 || state.FarT30 is null)
            return 0;

        t30_set_tx_file(state.FarT30, state.NextTxFile, -1, -1);
        state.NextTxFile = string.Empty;
        return 1;
    }

    private static void TesterRealTimeFrameHandler(
        FaxTesterState state,
        bool incoming,
        ReadOnlyMemory<byte>? message) {
        if (!message.HasValue) {
            FrontEndStepComplete(state);
            return;
        }

        ReadOnlySpan<byte> frame = message.Value.Span;
        if (frame.Length < 2)
            return;
        Console.Error.WriteLine(
            $"TST: Real time frame handler - " +
            $"{(incoming ? "line->tester" : "tester->line")}, " +
            $"{(frame.Length > 2 ? t30_frametype(frame[2]) : "short frame")}, " +
            $"length = {frame.Length}");

        if (incoming && state.AwaitedLength != 0 && frame[1] == state.Awaited[1]) {
            int expectedLength = Math.Abs(state.AwaitedLength);
            bool badLength = state.AwaitedLength >= 0
                ? frame.Length != expectedLength
                : frame.Length < expectedLength;
            bool badData = frame.Length < expectedLength ||
                !frame[..expectedLength].SequenceEqual(state.Awaited.AsSpan(0, expectedLength));
            if (badLength || badData) {
                state.Log($"Expected {Convert.ToHexString(state.Awaited, 0, expectedLength)}");
                state.Log($"Received {Convert.ToHexString(frame)}");
                Console.WriteLine("Test failed");
                throw new FaxTesterExitException(2);
            }
        }

        if (state.AwaitedLength != 0 && frame[1] == state.Awaited[1])
            FrontEndStepComplete(state);
    }

    private static void HdlcUnderflowHandler(FaxTesterState state) {
        if (!state.ImageBuffer.IsEmpty) {
            if (state.ImagePointer < state.ImageLength) {
                int frameLength = Math.Min(
                    state.EcmFrameSize,
                    state.ImageLength - state.ImagePointer);
                byte[] frame = new byte[4 + frameLength];
                frame[0] = 0xFF;
                frame[1] = 0x03;
                frame[2] = 0x06;
                frame[3] = (byte)(state.ImagePointer / state.EcmFrameSize);
                state.ImageBuffer.Span
                    .Slice(state.ImagePointer, frameLength)
                    .CopyTo(frame.AsSpan(4));
                state.Modems.TransmitHdlcFrame(frame);
                if (state.CorruptCrc >= 0 &&
                    state.CorruptCrc == state.ImagePointer / state.EcmFrameSize) {
                    CorruptCurrentHdlcFrame(state.Modems);
                }
                state.ImagePointer += state.EcmFrameSize;
                return;
            }

            if (state.ImageBitPointer > 2) {
                state.ImageBitPointer--;
                state.Modems.TransmitHdlcFrame(new byte[] { 0xFF, 0x03, 0x86 });
                return;
            }

            state.ImageBuffer = ReadOnlyMemory<byte>.Empty;
        }

        FrontEndStepComplete(state);
    }

    private static void ToneDetected(
        FaxTesterState state,
        int tone,
        int level,
        int delay) {
        _ = delay;
        state.Log($"Tone {tone} declared ({level}dBm0)");
        if (tone != 0)
            state.ToneOnTime = state.Timer;
        else
            state.Log(
                $"Tone was on for " +
                $"{(state.Timer - state.ToneOnTime) / (double)fax_utils.TelephonySampleRate + 0.55:F3}s");
        state.ToneState = tone;
        if (tone == 0)
            FrontEndStepComplete(state);
    }

    private static int NonEcmGetBit(FaxTesterState state) {
        if (state.ImageBuffer.IsEmpty)
            return (int)ModemSignalStatus.EndOfData;

        if (state.ImageBitPointer == 0) {
            if (state.ImagePointer >= state.ImageLength) {
                state.ImageBuffer = ReadOnlyMemory<byte>.Empty;
                return (int)ModemSignalStatus.EndOfData;
            }
            state.ImageBitPointer = 8;
            state.ImagePointer++;
        }

        state.ImageBitPointer--;
        return (state.Image[state.ImagePointer] >> (7 - state.ImageBitPointer)) & 1;
    }

    private static void NonEcmPutBit(FaxTesterState state, int bit) {
        if (bit >= 0)
            return;

        state.Log($"Non-ECM carrier status is {signal_status_to_str(bit)} ({bit})");
        switch ((ModemSignalStatus)bit) {
            case ModemSignalStatus.TrainingFailed:
                break;
            case ModemSignalStatus.TrainingSucceeded:
                break;
            case ModemSignalStatus.CarrierDown:
                TesterRealTimeFrameHandler(state, true, null);
                break;
        }
    }

    private static void HdlcAccept(
        FaxTesterState state,
        ReadOnlyMemory<byte>? message,
        int lengthOrStatus,
        bool ok) {
        _ = ok;
        if (lengthOrStatus < 0) {
            state.Log(
                $"HDLC carrier status is " +
                $"{signal_status_to_str(lengthOrStatus)} ({lengthOrStatus})");
            return;
        }

        if (!message.HasValue)
            return;
        TesterRealTimeFrameHandler(
            state,
            true,
            message.Value[..Math.Min(lengthOrStatus, message.Value.Length)]);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_hdlcTx")]
    private static extern ref HdlcTransmitter? AccessHdlcTransmitter(FaxModems modems);

    private static void CorruptCurrentHdlcFrame(FaxModems modems) {
        HdlcTransmitter transmitter =
            AccessHdlcTransmitter(modems) ??
            throw new InvalidOperationException("The active HDLC transmitter is unavailable.");
        _ = transmitter.CorruptFrame();
    }

    private static void SetEcmImageBuffer(
        FaxTesterState state,
        int block,
        int frameSize,
        int crcHit) {
        state.ImagePointer = checked(256 * frameSize * block);
        int maximum = checked(state.ImagePointer + 256 * frameSize);
        if (state.ImageLength > maximum)
            state.ImageLength = maximum;
        state.EcmFrameSize = frameSize;
        state.ImageBitPointer = 8;
        state.CorruptCrc = crcHit;
        state.ImageBuffer = state.Image.AsMemory(0, state.ImageLength);
        HdlcUnderflowHandler(state);
    }

    private static void CorruptImage(FaxTesterState state, string badRows) {
        HashSet<int> rowsToCorrupt = badRows
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static value => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int row) ? row : -1)
            .Where(static row => row > 0)
            .ToHashSet();

        uint bits = 0x7FF;
        uint altered = 0x7FF;
        int rowNumber = 0;
        for (int i = 0; i < state.ImageLength; i++) {
            bits ^= (uint)state.Image[i] << 11;
            altered ^= (uint)state.Image[i] << 11;
            for (int bit = 0; bit < 8; bit++) {
                if ((bits & 0xFFF) == 0x800) {
                    rowNumber++;
                    if (rowsToCorrupt.Contains(rowNumber))
                        altered ^= 0x1000;
                }
                bits >>= 1;
                altered >>= 1;
            }
            state.Image[i] = (byte)((altered >> 3) & 0xFF);
        }
        state.Log($"{rowNumber} rows found. {rowsToCorrupt.Count} corrupted");
    }

    private static int StringToMessage(byte[] message, byte[] mask, string text) {
        int index = 0;
        int position = 0;
        if (message.Length == 0 || mask.Length == 0)
            return 0;
        message[0] = 0;
        mask[0] = 0xFF;

        while (position < text.Length) {
            while (position < text.Length && char.IsWhiteSpace(text[position]))
                position++;
            if (position >= text.Length)
                break;
            if (position + 2 < text.Length && text.AsSpan(position, 3).SequenceEqual("..."))
                return -index;
            if (!Uri.IsHexDigit(text[position])) {
                position++;
                continue;
            }
            if (index >= message.Length || index >= mask.Length)
                throw new InvalidDataException("HDLC XML value exceeds the message buffer.");

            byte value = 0;
            while (position < text.Length && Uri.IsHexDigit(text[position])) {
                value = unchecked((byte)((value << 4) | HexValue(text[position])));
                position++;
            }
            message[index] = value;

            if (position < text.Length && text[position] == '/') {
                position++;
                byte maskValue = 0;
                while (position < text.Length && Uri.IsHexDigit(text[position])) {
                    maskValue = unchecked((byte)((maskValue << 4) | HexValue(text[position])));
                    position++;
                }
                mask[index] = maskValue;
            } else {
                mask[index] = 0xFF;
            }

            if (position < text.Length && !char.IsWhiteSpace(text[position]))
                return 0;
            index++;
        }

        return index;
    }

    private static void PrepareFarFax(FaxTesterState state) {
        faxtester_set_timeout(state, state.TimeoutMilliseconds);
        FaxState? farFax = state.FarFax;
        if (farFax is not null) {
            fax_set_transmit_on_idle(farFax, 1);
            fax_set_tep_mode(farFax, 1);
        }

        if (state.FarT30 is null)
            return;

        T30State t30 = state.FarT30;
        t30_set_retransmit_capable(t30, false);
        t30_set_tx_nsf(t30, Encoding.ASCII.GetBytes("\x50\0\0\0Spandsp NSF\0"));
        t30_set_tx_nsc(t30, Encoding.ASCII.GetBytes("\x50\0\0\0Spandsp NSC\0"));
        t30_set_ecm_capability(t30, true);
        t30_set_supported_t30_features(
            t30,
            (int)(T30SupportedFeatures.Identification |
                  T30SupportedFeatures.SelectivePolling |
                  T30SupportedFeatures.SubAddressing));
        t30_set_supported_image_sizes(
            t30,
            T4_SUPPORT_WIDTH_215MM |
            T4_SUPPORT_WIDTH_255MM |
            T4_SUPPORT_WIDTH_303MM |
            T4_SUPPORT_LENGTH_US_LETTER |
            T4_SUPPORT_LENGTH_US_LEGAL |
            T4_SUPPORT_LENGTH_UNLIMITED);
        t30_set_supported_bilevel_resolutions(t30, AllBilevelResolutions);
        t30_set_supported_colour_resolutions(t30, 0);
        t30_set_supported_modems(
            t30,
            (int)(TKFaxEngine.Daten.T30.T30SupportedModems.V27Ter |
                  TKFaxEngine.Daten.T30.T30SupportedModems.V29 |
                  TKFaxEngine.Daten.T30.T30SupportedModems.V17));
        t30_set_supported_compressions(
            t30,
            T4_COMPRESSION_T4_1D |
            T4_COMPRESSION_T4_2D |
            T4_COMPRESSION_T6);
        t30_set_phase_b_handler(t30, PhaseBHandler, state);
        t30_set_phase_d_handler(t30, PhaseDHandler, state);
        t30_set_phase_e_handler(t30, PhaseEHandler, state);
        t30_set_real_time_frame_handler(t30, T30RealTimeFrameHandler, state);
        t30_set_document_handler(t30, DocumentHandler, state);
    }

    private static void StartFarEnd(FaxTesterState state, bool callingParty) {
        faxtester_set_timeout(state, state.TimeoutMilliseconds);
        FaxState? farFax = state.FarFax;
        if (farFax is not null)
            fax_restart(farFax, callingParty);
        else if (state.FarT38 is not null)
            T38Terminal.t38_terminal_restart(state.FarT38, callingParty);
        else
            throw new InvalidOperationException("No far-end fax or T.38 terminal is connected.");
    }

    private static void VerifyStatus(FaxTesterState state, string? value) {
        if (string.IsNullOrWhiteSpace(value) || state.FarT30 is null)
            return;

        int expected = T30Status.TryGetValue(value, out T30Error code)
            ? (int)code
            : ParseInt(value, 0);
        var statistics = new T30Statistics();
        TKFaxEngine.Daten.T30.T30.t30_get_transfer_statistics(state.FarT30, statistics);
        if (expected == (int)statistics.CurrentStatus) {
            state.Log($"Expected status ({value}) found");
            return;
        }

        state.Log(
            $"Expected status {value}, but found " +
            $"{t30_completion_code_to_str((int)statistics.CurrentStatus)} " +
            $"({statistics.CurrentStatus})");
        Console.WriteLine("Test failed");
        throw new FaxTesterExitException(2);
    }

    private static void LoadImage(
        FaxTesterState state,
        NodeParameters parameters,
        bool ecm) {
        if (string.IsNullOrEmpty(parameters.Value))
            throw new InvalidDataException("The XML image step has no value attribute.");

        int minimumRowBits = ParseInt(parameters.MinimumBits, 0);
        string path = BuildImagePath(state, parameters.Value);
        t4_tx_state_t tx = t4_tx_init(null, path, -1, -1)
            ?? throw new FaxTesterExitException(2, "Failed to init T.4 send");

        try {
            t4_tx_set_header_info(tx, null);
            int compression = ParseCompression(parameters.Compression);
            if (t4_tx_set_tx_image_format(
                    tx,
                    compression,
                    T4_SUPPORT_WIDTH_215MM |
                    T4_SUPPORT_LENGTH_US_LETTER |
                    T4_SUPPORT_LENGTH_US_LEGAL |
                    T4_SUPPORT_LENGTH_UNLIMITED,
                    AllBilevelResolutions,
                    AllColourResolutions) < 0) {
                Fail(state, "Failed to set T.4 compression");
            }
            t4_tx_set_min_bits_per_row(tx, minimumRowBits);
            if (t4_tx_start_page(tx) != 0)
                Fail(state, "Failed to start T.4 send");
            state.ImageLength = t4_tx_get(tx, state.Image, state.Image.Length);
        } finally {
            t4_tx_release(tx);
        }

        if (!string.IsNullOrEmpty(parameters.BadRows)) {
            state.Log("We need to corrupt the image");
            CorruptImage(state, parameters.BadRows);
        }

        state.Log(
            $"{(ecm ? "ECM" : "Non-ECM")} image is " +
            $"{state.ImageLength} bytes (min row bits {minimumRowBits})");

        if (ecm) {
            SetEcmImageBuffer(
                state,
                ParseInt(parameters.Block, 0),
                ParseInt(parameters.FrameSize, 64),
                ParseInt(parameters.CrcError, -1));
        } else {
            state.ImagePointer = 0;
            state.ImageBitPointer = 8;
            state.ImageBuffer = state.Image.AsMemory(0, state.ImageLength);
        }
    }

    private static int ParseCompression(string? value) {
        if (EqualsIgnoreCase(value, "T.4 2D"))
            return T4_COMPRESSION_T4_2D;
        if (EqualsIgnoreCase(value, "T.6"))
            return T4_COMPRESSION_T6;
        if (EqualsIgnoreCase(value, "T.85"))
            return T4_COMPRESSION_T85;
        return T4_COMPRESSION_T4_1D;
    }

    private static void SelectReceiveModem(
        FaxTesterState state,
        string modem,
        bool shortTrain,
        bool hdlc) {
        (FaxT30ModemType type, int rate) = ParseModem(modem);
        if (type == FaxT30ModemType.None) {
            state.Log("Unrecognised modem");
            return;
        }
        faxtester_set_rx_type(state, (int)type, rate, shortTrain ? 1 : 0, hdlc);
    }

    private static void SelectTransmitModem(
        FaxTesterState state,
        string modem,
        bool shortTrain,
        bool hdlc) {
        (FaxT30ModemType type, int rate) = ParseModem(modem);
        if (type == FaxT30ModemType.None) {
            state.Log("Unrecognised modem");
            return;
        }
        faxtester_set_tx_type(state, (int)type, rate, shortTrain ? 1 : 0, hdlc);
    }

    private static (FaxT30ModemType Type, int BitRate) ParseModem(string modem) {
        return modem.ToUpperInvariant() switch {
            "V.21" => (FaxT30ModemType.V21, 300),
            "V.17/14400" => (FaxT30ModemType.V17, 14_400),
            "V.17/12000" => (FaxT30ModemType.V17, 12_000),
            "V.17/9600" => (FaxT30ModemType.V17, 9_600),
            "V.17/7200" => (FaxT30ModemType.V17, 7_200),
            "V.29/9600" => (FaxT30ModemType.V29, 9_600),
            "V.29/7200" => (FaxT30ModemType.V29, 7_200),
            "V.27TER/4800" => (FaxT30ModemType.V27Ter, 4_800),
            "V.27TER/2400" => (FaxT30ModemType.V27Ter, 2_400),
            _ => (FaxT30ModemType.None, 0)
        };
    }

    private static XElement? MoveToNextStep(FaxTesterState state) {
        while (true) {
            if (state.CurrentSiblingIndex >= state.CurrentSiblings.Count) {
                if (state.RepeatParent is null || state.RepeatSiblings is null)
                    return null;

                state.RepeatCount++;
                if (state.RepeatCount > state.RepeatMaximum) {
                    Console.WriteLine("Too many repeats");
                    Console.WriteLine("Test failed");
                    throw new FaxTesterExitException(0);
                }

                if (state.RepeatCount < state.RepeatMinimum) {
                    state.CurrentSiblings = state.RepeatParent.Elements().ToList();
                    state.CurrentSiblingIndex = 0;
                } else {
                    state.CurrentSiblings = state.RepeatSiblings;
                    state.CurrentSiblingIndex = state.RepeatParentSiblingIndex + 1;
                    state.RepeatParent = null;
                    state.RepeatStart = null;
                    state.RepeatSiblings = null;
                }
                continue;
            }

            XElement current = state.CurrentSiblings[state.CurrentSiblingIndex++];
            state.Current = current;
            if (current.Name.LocalName.Equals("step", StringComparison.Ordinal))
                return current;

            if (state.RepeatParent is null &&
                current.Name.LocalName.Equals("repeat", StringComparison.Ordinal)) {
                state.RepeatMinimum = ParseInt(current.Attribute("min")?.Value, 0);
                state.RepeatMaximum = ParseInt(current.Attribute("max")?.Value, int.MaxValue);
                state.RepeatCount = 0;
                if (state.RepeatMinimum > 0) {
                    state.RepeatParent = current;
                    state.RepeatStart = current.Elements().FirstOrDefault();
                    state.RepeatSiblings = state.CurrentSiblings;
                    state.RepeatParentSiblingIndex = state.CurrentSiblingIndex - 1;
                    state.CurrentSiblings = current.Elements().ToList();
                    state.CurrentSiblingIndex = 0;
                }
            }
        }
    }

    private static NodeParameters GetNodeParameters(XElement node) => new(
        node.Attribute("dir")?.Value,
        node.Attribute("type")?.Value,
        node.Attribute("modem")?.Value,
        node.Attribute("value")?.Value,
        node.Attribute("tag")?.Value,
        node.Attribute("bad_rows")?.Value,
        node.Attribute("crc_error")?.Value,
        node.Attribute("pattern")?.Value,
        node.Attribute("timein")?.Value,
        node.Attribute("timeout")?.Value,
        node.Attribute("min_bits")?.Value,
        node.Attribute("frame_size")?.Value,
        node.Attribute("block")?.Value,
        node.Attribute("compression")?.Value);

    private static bool GetTestSet(
        FaxTesterState state,
        string testFile,
        string test) {
        try {
            XDocument document = XDocument.Load(
                testFile,
                LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            XElement? root = document.Root;
            if (root is null)
                throw new InvalidDataException("Empty document");
            if (!root.Name.LocalName.Equals("fax-tests", StringComparison.Ordinal))
                throw new InvalidDataException("Document root is not fax-tests.");

            foreach (XElement config in root.Elements().Where(
                         static item => item.Name.LocalName == "config")) {
                foreach (XElement path in config.Elements().Where(
                             static item => item.Name.LocalName == "path")) {
                    if (EqualsIgnoreCase(path.Attribute("type")?.Value, "IMAGE")) {
                        string? value = path.Attribute("value")?.Value;
                        if (value is not null)
                            state.ImagePath = NormalizeImagePath(value);
                    }
                }
            }

            XElement? selected = root
                .Elements()
                .Where(static item => item.Name.LocalName == "test-group")
                .SelectMany(static group => group.Elements())
                .FirstOrDefault(item =>
                    item.Name.LocalName == "test" &&
                    string.Equals(
                        item.Attribute("name")?.Value,
                        test,
                        StringComparison.Ordinal));
            if (selected is null)
                throw new InvalidDataException("XML test not found");

            state.Document = document;
            state.CurrentSiblings = selected.Elements().ToList();
            state.CurrentSiblingIndex = 0;
            state.Current = state.CurrentSiblings.FirstOrDefault();
            return true;
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or global::System.Xml.XmlException) {
            Console.Error.WriteLine(ex.Message);
            return false;
        }
    }

    private static string NormalizeImagePath(string source) {
        string normalized = source.Replace('\\', '/');
        if (normalized.Equals("../test-data/etsi/fax", StringComparison.Ordinal))
            return "Test-Files/etsi/fax";
        if (normalized.StartsWith("../test-data/", StringComparison.Ordinal))
            return "Test-Files/" + normalized[13..];
        return source;
    }

    private static void SetExpectedInfo(
        T30ExchangedInfo info,
        string? tag,
        string? value) {
        if (EqualsIgnoreCase(tag, "IDENT"))
            info.Ident = value;
        else if (EqualsIgnoreCase(tag, "SUB"))
            info.SubAddress = value;
        else if (EqualsIgnoreCase(tag, "SEP"))
            info.SelectivePollingAddress = value;
        else if (EqualsIgnoreCase(tag, "PSA"))
            info.PolledSubAddress = value;
        else if (EqualsIgnoreCase(tag, "SID"))
            info.SenderIdent = value;
        else if (EqualsIgnoreCase(tag, "PWD"))
            info.Password = value;
    }

    private static void SetFarT30Value(
        FaxTesterState state,
        string? tag,
        string? value) {
        T30State? t30 = state.FarT30;
        if (EqualsIgnoreCase(tag, "TXFILE")) {
            state.NextTxFile = BuildImagePath(state, value ?? string.Empty);
            Console.WriteLine($"Push '{state.NextTxFile}'");
            return;
        }
        if (t30 is null)
            return;

        if (EqualsIgnoreCase(tag, "IDENT"))
            t30_set_tx_ident(t30, value);
        else if (EqualsIgnoreCase(tag, "SUB"))
            t30_set_tx_sub_address(t30, value);
        else if (EqualsIgnoreCase(tag, "SEP"))
            t30_set_tx_selective_polling_address(t30, value);
        else if (EqualsIgnoreCase(tag, "PSA"))
            t30_set_tx_polled_sub_address(t30, value);
        else if (EqualsIgnoreCase(tag, "SID"))
            t30_set_tx_sender_ident(t30, value);
        else if (EqualsIgnoreCase(tag, "PWD"))
            t30_set_tx_password(t30, value);
        else if (EqualsIgnoreCase(tag, "RXFILE"))
            t30_set_rx_file(t30, value ?? fax_tests.OutputTiffFileName, -1);
    }

    private static int CompareRemoteString(
        char channel,
        string label,
        string? expected,
        string? actual,
        T30Error error,
        int currentStatus) {
        if (actual is not null) {
            Console.WriteLine($"{channel}: Phase B: remote {label} '{actual}'");
            if (!string.IsNullOrEmpty(expected) && !string.Equals(expected, actual, StringComparison.Ordinal)) {
                Console.WriteLine(
                    $"{channel}: Phase B: remote {label} incorrect! - expected '{expected}'");
                return (int)error;
            }
        } else if (!string.IsNullOrEmpty(expected)) {
            Console.WriteLine($"{channel}: Phase B: remote {label} missing!");
            return (int)error;
        }
        return currentStatus;
    }

    private static void ReverseBytes(byte[] destination, byte[] source, int length) {
        if ((uint)length > (uint)source.Length || (uint)length > (uint)destination.Length)
            throw new ArgumentOutOfRangeException(nameof(length));
        if (ReferenceEquals(destination, source)) {
            for (int i = 0; i < length; i++)
                destination[i] = bit_reverse8(destination[i]);
        } else {
            for (int i = 0; i < length; i++)
                destination[i] = bit_reverse8(source[i]);
        }
    }

    private static string BuildImagePath(FaxTesterState state, string value) =>
        Path.Combine(state.ImagePath, value);

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : fallback;

    private static bool EqualsIgnoreCase(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static int HexValue(char value) => value switch {
        >= '0' and <= '9' => value - '0',
        >= 'A' and <= 'F' => value - 'A' + 10,
        >= 'a' and <= 'f' => value - 'a' + 10,
        _ => 0
    };

    private static int MillisecondsToSamples(int milliseconds) =>
        checked(milliseconds * fax_utils.TelephonySampleRate / 1000);

    private static string FrameType(int result) =>
        t30_frametype(unchecked((byte)result));

    private static FaxTesterState RequireState(object? userData) =>
        userData as FaxTesterState ??
        throw new ArgumentException("Callback user data must be a FaxTesterState.", nameof(userData));

    private static T30State RequireFarT30(FaxTesterState state) =>
        state.FarT30 ?? throw new InvalidOperationException("The fax tester has no connected far-end T.30 state.");

    private static void Fail(FaxTesterState state, string message) {
        state.Log(message);
        Console.WriteLine("Test failed");
        throw new FaxTesterExitException(2);
    }

    private static string ToNativeStatusTag(T30Error value) {
        string name = value.ToString();
        var builder = new StringBuilder(name.Length + 8);
        for (int i = 0; i < name.Length; i++) {
            char ch = name[i];
            if (i > 0 && char.IsUpper(ch) && char.IsLower(name[i - 1]))
                builder.Append('_');
            builder.Append(char.ToUpperInvariant(ch));
        }
        return builder.ToString();
    }

    private const int AllBilevelResolutions =
        T4_RESOLUTION_R8_STANDARD |
        T4_RESOLUTION_R8_FINE |
        T4_RESOLUTION_R8_SUPERFINE |
        T4_RESOLUTION_R16_SUPERFINE |
        T4_RESOLUTION_100_100 |
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

    private const int AllColourResolutions =
        T4_RESOLUTION_100_100 |
        T4_RESOLUTION_200_200 |
        T4_RESOLUTION_300_300 |
        T4_RESOLUTION_400_400 |
        T4_RESOLUTION_600_600 |
        T4_RESOLUTION_1200_1200;

    public sealed class FaxTesterExitException : Exception {
        public FaxTesterExitException(int exitCode)
            : this(exitCode, $"fax_tester requested exit code {exitCode}.") {
        }

        public FaxTesterExitException(int exitCode, string message)
            : base(message) => ExitCode = exitCode;

        public int ExitCode { get; }
    }

}
