/*
 * TKFaxEngine - managed C# port
 *
 * Fax.cs - combined port of fax.h, private/fax.h and fax.c
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2003, 2005, 2006 Steve Underwood.
 *
 * This port preserves the LGPL-2.1 licensing terms of the original files.
 *
 * FaxState directly owns T.30, fax_modems, V.8 and logging, matching
 * fax_state_t. No runtime factory, interface backend, wrapper state or
 * externally supplied modem implementation is used in the fax signal path.
 */

#nullable enable

using System.Runtime.InteropServices;
using global::TKFaxEngine.Daten.T30;
using T30Core = global::TKFaxEngine.Daten.T30.T30;
using global::TKFaxEngine.Modem;
using global::TKFaxEngine.Modem.V8;

namespace TKFaxEngine;

public enum FaxModemChannel {
    None = -1,
    Flush = 0,
    SilenceTx = 1,
    SilenceRx = 2,
    CedToneTx = 3,
    CngToneTx = 4,
    NoCngToneTx = 5,
    CedToneRx = 6,
    CngToneRx = 7,
    V21Tx = 8,
    V17Tx = 9,
    V27TerTx = 10,
    V29Tx = 11,
    V21Rx = 12,
    V17Rx = 13,
    V27TerRx = 14,
    V29Rx = 15,
    V34Tx = 16,
    V34Rx = 17,
}

public delegate void HdlcAcceptHandler(
    ReadOnlyMemory<byte>? message,
    int lengthOrSignalStatus,
    bool ok);

public delegate void NonEcmPutBitHandler(int bit);

public delegate int NonEcmGetBitHandler();

public delegate void ToneDetectedHandler(int tone, int level, int delay);

/// <summary>
/// Managed equivalent of fax_state_t and fax.c. The object directly owns all
/// components used by the analogue fax path, as the FX implementation does.
/// </summary>
public sealed class FaxState : IDisposable {
    public const int SampleRate = 8_000;

    private const int HdlcFramingOkThreshold = 8;
    private const int V21PreambleOctets = 32;
    private const int ModemSwitchPauseMilliseconds = 75;

    private FileStream? _audioReceiveLog;
    private FileStream? _audioTransmitLog;
    private bool _released;
    private bool _disposed;

    public FaxState(
        bool callingParty,
        bool enableAudioLogging = false,
        string? audioLogDirectory = null) {
        T30 = new T30State();
        Modems = new FaxModems();
        Logging = new SpanLogState();
        EnableAudioLogging = enableAudioLogging;
        AudioLogDirectory = audioLogDirectory;
        Initialize(callingParty);
    }

    public T30State T30 { get; }

    public FaxModems Modems { get; }

    public V8State V8 { get; private set; } = null!;

    public SpanLogState Logging { get; }

    public bool EnableAudioLogging { get; set; }

    public string? AudioLogDirectory { get; set; }

    /// <summary>Managed equivalent of fax_init().</summary>
    public void Initialize(bool callingParty) {
        ThrowIfDisposed();
        CloseAudioLogs();
        _released = false;

        LoggingApi.span_log_init(Logging, (int)SpanLogSeverity.None, null);
        LoggingApi.span_log_set_protocol(Logging, "FAX");

        Modems.Initialize(
            useTep: false,
            HdlcAccept,
            HdlcUnderflowHandler,
            NonEcmPutBit,
            NonEcmGetBit,
            ToneDetected);
        T30Core.t30_init(
            T30,
            callingParty,
            FaxSetReceiveType,
            this,
            FaxSetTransmitType,
            this,
            FaxSendHdlc,
            Modems);
        T30.SupportedModems =
            T30SupportedModems.V27Ter |
            T30SupportedModems.V29 |
            T30SupportedModems.V17;

        V8Parameters parameters = BuildV8Parameters();
        V8 = V8Api.v8_init(
            V8,
            callingParty,
            parameters,
            V8Handler,
            this);

        Restart(callingParty);
    }

    /// <summary>Managed equivalent of fax_rx().</summary>
    public int Receive(short[] samples, int length) {
        ThrowIfDisposed();
        ValidateBuffer(samples, length, nameof(length));

        WriteAudio(_audioReceiveLog, samples, 0, length);
        for (int i = 0; i < length; i++)
            samples[i] = Modems.RestoreDc(samples[i]);

        Modems.ProcessReceive(samples, 0, length);
        T30Core.t30_timer_update(T30, length);
        return 0;
    }

    /// <summary>Managed equivalent of fax_rx_fillin().</summary>
    public int ReceiveFillIn(int length) {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        if (_audioReceiveLog is not null && length > 0)
            _audioReceiveLog.Write(new byte[checked(length * sizeof(short))]);

        Modems.ProcessReceiveFillIn(length);
        T30Core.t30_timer_update(T30, length);
        return 0;
    }

    /// <summary>Managed equivalent of fax_tx().</summary>
    public int Transmit(short[] destination, int maximumLength) {
        ThrowIfDisposed();
        ValidateBuffer(destination, maximumLength, nameof(maximumLength));

        int length = 0;
        while (Modems.Transmit && length < maximumLength) {
            int generated = Modems.GenerateTransmit(
                destination,
                length,
                maximumLength - length);
            if ((uint)generated > (uint)(maximumLength - length))
                throw new InvalidOperationException(
                    $"The transmit modem returned an invalid sample count: {generated}.");

            length += generated;
            if (length >= maximumLength)
                break;

            if (Modems.SetNextTransmitType() != 0
                && Modems.CurrentTxType is not T30ModemType.None
                and not T30ModemType.Done) {
                T30Core.t30_front_end_status(
                    T30,
                    T30FrontEndStatus.SendStepComplete);
            }
        }

        if (Modems.TransmitOnIdle) {
            Array.Clear(destination, length, maximumLength - length);
            length = maximumLength;
        }

        if (_audioTransmitLog is not null) {
            if (length < maximumLength)
                Array.Clear(destination, length, maximumLength - length);
            WriteAudio(_audioTransmitLog, destination, 0, maximumLength);
        }

        return length;
    }

    public void SetTransmitOnIdle(bool transmitOnIdle) {
        ThrowIfDisposed();
        Modems.TransmitOnIdle = transmitOnIdle;
    }

    public void SetTepMode(bool useTep) {
        ThrowIfDisposed();
        Modems.SetTepMode(useTep);
    }

    public T30State GetT30State() {
        ThrowIfDisposed();
        return T30;
    }

    public SpanLogState GetLoggingState() {
        ThrowIfDisposed();
        return Logging;
    }

    /// <summary>Managed equivalent of fax_restart().</summary>
    public int Restart(bool callingParty) {
        ThrowIfDisposed();

        Modems.Restart();
        V8Api.v8_restart(V8, callingParty, BuildV8Parameters());
        T30Core.t30_restart(T30, callingParty);
        OpenAudioLogs();
        return 0;
    }

    /// <summary>Managed equivalent of fax_release().</summary>
    public int Release() {
        if (_released)
            return 0;

        CloseAudioLogs();
        T30Core.t30_release(T30);
        V8Api.v8_release(V8);
        _released = true;
        return 0;
    }

    public int Free() {
        Dispose();
        return 0;
    }

    public void Dispose() {
        if (_disposed)
            return;

        Release();
        Modems.Dispose();
        V8.Dispose();
        Logging.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static void FaxSetReceiveType(
        object? userData,
        T30ModemType type,
        int bitRate,
        int shortTrain,
        bool useHdlc) {
        ((FaxState)userData!).SetReceiveType(type, bitRate, shortTrain, useHdlc);
    }

    private static void FaxSetTransmitType(
        object? userData,
        T30ModemType type,
        int bitRate,
        int shortTrain,
        bool useHdlc) {
        ((FaxState)userData!).SetTransmitType(type, bitRate, shortTrain, useHdlc);
    }

    private static void FaxSendHdlc(
        object? userData,
        ReadOnlyMemory<byte>? message,
        int length) {
        FaxModemsApi.fax_modems_hdlc_tx_frame(
            (FaxModems)userData!,
            message,
            length);
    }

    private void HdlcAccept(
        ReadOnlyMemory<byte>? message,
        int lengthOrStatus,
        bool ok) {
        if (message.HasValue) {
            T30Core.t30_hdlc_accept(
                T30,
                message.Value.Span,
                lengthOrStatus,
                ok ? 1 : 0);
        } else {
            T30Core.t30_hdlc_accept(
                T30,
                ReadOnlySpan<byte>.Empty,
                lengthOrStatus,
                ok ? 1 : 0);
        }
    }

    private void HdlcUnderflowHandler() {
        T30Core.t30_front_end_status(
            T30,
            T30FrontEndStatus.SendStepComplete);
    }

    private void NonEcmPutBit(int bit) {
        T30Core.t30_non_ecm_put_bit(T30, bit);
    }

    private int NonEcmGetBit() {
        return T30Core.t30_non_ecm_get_bit(T30);
    }

    private void ToneDetected(int tone, int level, int delay) {
        _ = delay;
        T30.Logging.Flow(
            $"{ModemConnectTones.ToneToString((ModemConnectTone)tone)} detected ({level}dBm0)");
    }

    private static void V8Handler(object? userData, V8Parameters result) {
        _ = result;
        FaxState state = (FaxState)userData!;
        state.Logging.Log(
            (int)SpanLogSeverity.Flow,
            "V.8 report received\n");
    }

    private void SetReceiveType(
        T30ModemType type,
        int bitRate,
        int shortTrain,
        bool useHdlc) {
        Logging.Log(
            (int)SpanLogSeverity.Flow,
            "Set rx type %s (%d)\n",
            T30Logging.t30_modem_to_str((int)type),
            (int)type);

        if (Modems.CurrentRxType == type)
            return;

        Modems.CurrentRxType = type;
        Modems.RxBitRate = bitRate;
        Modems.InitializeHdlcReceiver(
            useCrc32: false,
            reportBadFrames: true,
            framingOkThreshold: HdlcFramingOkThreshold);

        switch (type) {
            case T30ModemType.V21:
                Modems.StartSlowModem(FaxModemChannel.V21Rx);
                break;

            case T30ModemType.V17:
                Modems.StartFastModem(
                    FaxModemChannel.V17Rx,
                    bitRate,
                    shortTrain != 0,
                    useHdlc);
                break;

            case T30ModemType.V27Ter:
                Modems.StartFastModem(
                    FaxModemChannel.V27TerRx,
                    bitRate,
                    shortTrain != 0,
                    useHdlc);
                break;

            case T30ModemType.V29:
                Modems.StartFastModem(
                    FaxModemChannel.V29Rx,
                    bitRate,
                    shortTrain != 0,
                    useHdlc);
                break;

            case T30ModemType.Done:
                Logging.Log(
                    (int)SpanLogSeverity.Flow,
                    "FAX exchange complete\n");
                Modems.SetReceiveIdle();
                break;

            default:
                Modems.SetReceiveIdle();
                break;
        }
    }

    private void SetTransmitType(
        T30ModemType type,
        int bitRate,
        int shortTrain,
        bool useHdlc) {
        Logging.Log(
            (int)SpanLogSeverity.Flow,
            "Set tx type %s (%d)\n",
            T30Logging.t30_modem_to_str((int)type),
            (int)type);

        if (Modems.CurrentTxType == type)
            return;

        switch (type) {
            case T30ModemType.Pause:
                Modems.ConfigureTransmitPause(MillisecondsToSamples(shortTrain));
                break;

            case T30ModemType.Ced:
                Modems.ConfigureTransmitTone(FaxModemChannel.CedToneTx);
                break;

            case T30ModemType.Cng:
                Modems.ConfigureTransmitTone(FaxModemChannel.CngToneTx);
                break;

            case T30ModemType.V21:
                Modems.ConfigureTransmitV21(
                    V21PreambleOctets,
                    MillisecondsToSamples(ModemSwitchPauseMilliseconds));
                break;

            case T30ModemType.V17:
                ConfigureFastTransmit(
                    FaxModemChannel.V17Tx,
                    bitRate,
                    shortTrain != 0,
                    useHdlc);
                break;

            case T30ModemType.V27Ter:
                ConfigureFastTransmit(
                    FaxModemChannel.V27TerTx,
                    bitRate,
                    shortTrain != 0,
                    useHdlc);
                break;

            case T30ModemType.V29:
                ConfigureFastTransmit(
                    FaxModemChannel.V29Tx,
                    bitRate,
                    shortTrain != 0,
                    useHdlc);
                break;

            case T30ModemType.Done:
                Logging.Log(
                    (int)SpanLogSeverity.Flow,
                    "FAX exchange complete\n");
                Modems.StopTransmit();
                break;

            default:
                Modems.StopTransmit();
                break;
        }

        Modems.TxBitRate = bitRate;
        Modems.CurrentTxType = type;
    }

    private void ConfigureFastTransmit(
        FaxModemChannel channel,
        int bitRate,
        bool shortTrain,
        bool useHdlc) {
        Modems.ConfigureTransmitFast(
            channel,
            bitRate,
            shortTrain,
            useHdlc,
            bitRate / (8 * 5),
            MillisecondsToSamples(ModemSwitchPauseMilliseconds));
    }

    private V8Parameters BuildV8Parameters() {
        V8Parameters parameters = new() {
            ModemConnectTone = ModemConnectTone.AnsamWithPhaseReversals
        };

        parameters.JmCm.CallFunction = V8CallFunction.T30ReceiveFax;
        parameters.JmCm.Modulations = V8Modulation.V21;
        if ((T30.SupportedModems & T30SupportedModems.V27Ter) != 0)
            parameters.JmCm.Modulations |= V8Modulation.V27Ter;
        if ((T30.SupportedModems & T30SupportedModems.V29) != 0)
            parameters.JmCm.Modulations |= V8Modulation.V29;
        if ((T30.SupportedModems & T30SupportedModems.V17) != 0)
            parameters.JmCm.Modulations |= V8Modulation.V17;
        if ((T30.SupportedModems & T30SupportedModems.V34Hdx) != 0)
            parameters.JmCm.Modulations |= V8Modulation.V34HalfDuplex;

        parameters.JmCm.Protocols = V8Protocol.None;
        parameters.JmCm.PcmModemAvailability = V8PcmModemAvailability.None;
        parameters.JmCm.PstnAccess = V8PstnAccess.None;
        parameters.JmCm.Nsf = -1;
        parameters.JmCm.T66 = -1;
        return parameters;
    }

    private void OpenAudioLogs() {
        CloseAudioLogs();
        if (!EnableAudioLogging)
            return;

        string directory = string.IsNullOrWhiteSpace(AudioLogDirectory)
            ? Path.Combine(Path.GetTempPath(), "TKFaxEngine", "FaxAudio")
            : AudioLogDirectory;
        Directory.CreateDirectory(directory);

        string stamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        string instance = GetHashCode().ToString("x8");
        _audioReceiveLog = new FileStream(
            Path.Combine(directory, $"fax-rx-audio-{instance}-{stamp}.raw"),
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read);
        _audioTransmitLog = new FileStream(
            Path.Combine(directory, $"fax-tx-audio-{instance}-{stamp}.raw"),
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read);
    }

    private void CloseAudioLogs() {
        _audioReceiveLog?.Dispose();
        _audioReceiveLog = null;
        _audioTransmitLog?.Dispose();
        _audioTransmitLog = null;
    }

    private static void WriteAudio(
        Stream? destination,
        short[] samples,
        int offset,
        int count) {
        if (destination is null || count == 0)
            return;

        destination.Write(MemoryMarshal.AsBytes(samples.AsSpan(offset, count)));
    }

    private static int MillisecondsToSamples(int milliseconds) {
        return checked(milliseconds * (SampleRate / 1_000));
    }

    private static void ValidateBuffer(
        short[] buffer,
        int requestedLength,
        string parameterName) {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(requestedLength, parameterName);
        if (requestedLength > buffer.Length)
            throw new ArgumentException(
                "The requested sample count exceeds the supplied buffer length.",
                parameterName);
    }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

/// <summary>Compatibility facade retaining the native fax.h names.</summary>
public static class FaxApi {
    public static int fax_rx(FaxState state, short[] samples, int length) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Receive(samples, length);
    }

    public static int fax_rx_fillin(FaxState state, int length) {
        ArgumentNullException.ThrowIfNull(state);
        return state.ReceiveFillIn(length);
    }

    public static int fax_tx(FaxState state, short[] destination, int maximumLength) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Transmit(destination, maximumLength);
    }

    public static void fax_set_transmit_on_idle(FaxState state, int transmitOnIdle) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetTransmitOnIdle(transmitOnIdle != 0);
    }

    public static void fax_set_tep_mode(FaxState state, int useTep) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetTepMode(useTep != 0);
    }

    public static T30State fax_get_t30_state(FaxState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetT30State();
    }

    public static SpanLogState fax_get_logging_state(FaxState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetLoggingState();
    }

    public static int fax_restart(FaxState state, bool callingParty) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Restart(callingParty);
    }

    public static FaxState fax_init(FaxState? state, bool callingParty) {
        if (state is null)
            return new FaxState(callingParty);

        state.Initialize(callingParty);
        return state;
    }

    public static int fax_release(FaxState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    public static int fax_free(FaxState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Free();
    }
}
