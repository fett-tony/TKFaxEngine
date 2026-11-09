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

/// <summary>
/// Defines the available <c>FaxModemChannel</c> values.
/// </summary>
public enum FaxModemChannel {
    /// <summary>
    /// Represents the <c>None</c> value.
    /// </summary>
    None = -1,
    /// <summary>
    /// Represents the <c>Flush</c> value.
    /// </summary>
    Flush = 0,
    /// <summary>
    /// Represents the <c>SilenceTx</c> value.
    /// </summary>
    SilenceTx = 1,
    /// <summary>
    /// Represents the <c>SilenceRx</c> value.
    /// </summary>
    SilenceRx = 2,
    /// <summary>
    /// Represents the <c>CedToneTx</c> value.
    /// </summary>
    CedToneTx = 3,
    /// <summary>
    /// Represents the <c>CngToneTx</c> value.
    /// </summary>
    CngToneTx = 4,
    /// <summary>
    /// Represents the <c>NoCngToneTx</c> value.
    /// </summary>
    NoCngToneTx = 5,
    /// <summary>
    /// Represents the <c>CedToneRx</c> value.
    /// </summary>
    CedToneRx = 6,
    /// <summary>
    /// Represents the <c>CngToneRx</c> value.
    /// </summary>
    CngToneRx = 7,
    /// <summary>
    /// Represents the <c>V21Tx</c> value.
    /// </summary>
    V21Tx = 8,
    /// <summary>
    /// Represents the <c>V17Tx</c> value.
    /// </summary>
    V17Tx = 9,
    /// <summary>
    /// Represents the <c>V27TerTx</c> value.
    /// </summary>
    V27TerTx = 10,
    /// <summary>
    /// Represents the <c>V29Tx</c> value.
    /// </summary>
    V29Tx = 11,
    /// <summary>
    /// Represents the <c>V21Rx</c> value.
    /// </summary>
    V21Rx = 12,
    /// <summary>
    /// Represents the <c>V17Rx</c> value.
    /// </summary>
    V17Rx = 13,
    /// <summary>
    /// Represents the <c>V27TerRx</c> value.
    /// </summary>
    V27TerRx = 14,
    /// <summary>
    /// Represents the <c>V29Rx</c> value.
    /// </summary>
    V29Rx = 15,
    /// <summary>
    /// Represents the <c>V34Tx</c> value.
    /// </summary>
    V34Tx = 16,
    /// <summary>
    /// Represents the <c>V34Rx</c> value.
    /// </summary>
    V34Rx = 17,
}

/// <summary>
/// Represents the <c>HdlcAcceptHandler</c> callback.
/// </summary>
/// <param name="message">The message value.</param>
/// <param name="lengthOrSignalStatus">The length or signal status value.</param>
/// <param name="ok">The ok value.</param>
public delegate void HdlcAcceptHandler(
    ReadOnlyMemory<byte>? message,
    int lengthOrSignalStatus,
    bool ok);

/// <summary>
/// Represents the <c>NonEcmPutBitHandler</c> callback.
/// </summary>
/// <param name="bit">The bit value.</param>
public delegate void NonEcmPutBitHandler(int bit);

/// <summary>
/// Represents the <c>NonEcmGetBitHandler</c> callback.
/// </summary>
/// <returns>The result of the operation.</returns>
public delegate int NonEcmGetBitHandler();

/// <summary>
/// Represents the <c>ToneDetectedHandler</c> callback.
/// </summary>
/// <param name="tone">The tone value.</param>
/// <param name="level">The level value.</param>
/// <param name="delay">The delay value.</param>
public delegate void ToneDetectedHandler(int tone, int level, int delay);

/// <summary>
/// Managed equivalent of fax_state_t and fax.c. The object directly owns all
/// components used by the analogue fax path, as the FX implementation does.
/// </summary>
public sealed class FaxState : IDisposable {
    /// <summary>
    /// Defines the <c>SampleRate</c> constant.
    /// </summary>
    public const int SampleRate = 8_000;

    private const int HdlcFramingOkThreshold = 8;
    private const int V21PreambleOctets = 32;
    private const int ModemSwitchPauseMilliseconds = 75;

    private FileStream? _audioReceiveLog;
    private FileStream? _audioTransmitLog;
    private bool _released;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <c>FaxState</c> type.
    /// </summary>
    /// <param name="callingParty">The calling party value.</param>
    /// <param name="enableAudioLogging">The enable audio logging value.</param>
    /// <param name="audioLogDirectory">The audio log directory value.</param>
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

    /// <summary>
    /// Gets the <c>T30</c> value.
    /// </summary>
    public T30State T30 { get; }

    /// <summary>
    /// Gets the <c>Modems</c> value.
    /// </summary>
    public FaxModems Modems { get; }

    /// <summary>
    /// Gets or sets the <c>V8</c> value.
    /// </summary>
    public V8State V8 { get; private set; } = null!;

    /// <summary>
    /// Gets the <c>Logging</c> value.
    /// </summary>
    public SpanLogState Logging { get; }

    /// <summary>
    /// Gets or sets the <c>EnableAudioLogging</c> value.
    /// </summary>
    public bool EnableAudioLogging { get; set; }

    /// <summary>
    /// Gets or sets the <c>AudioLogDirectory</c> value.
    /// </summary>
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

    /// <summary>
    /// Performs the <c>SetTransmitOnIdle</c> operation.
    /// </summary>
    /// <param name="transmitOnIdle">The transmit on idle value.</param>
    public void SetTransmitOnIdle(bool transmitOnIdle) {
        ThrowIfDisposed();
        Modems.TransmitOnIdle = transmitOnIdle;
    }

    /// <summary>
    /// Performs the <c>SetTepMode</c> operation.
    /// </summary>
    /// <param name="useTep">The use tep value.</param>
    public void SetTepMode(bool useTep) {
        ThrowIfDisposed();
        Modems.SetTepMode(useTep);
    }

    /// <summary>
    /// Performs the <c>GetT30State</c> operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public T30State GetT30State() {
        ThrowIfDisposed();
        return T30;
    }

    /// <summary>
    /// Performs the <c>GetLoggingState</c> operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Performs the <c>Free</c> operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public int Free() {
        Dispose();
        return 0;
    }

    /// <summary>
    /// Performs the <c>Dispose</c> operation.
    /// </summary>
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
    /// <summary>
    /// Performs the <c>fax_rx</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="samples">The samples value.</param>
    /// <param name="length">The length value.</param>
    /// <returns>The result of the operation.</returns>
    public static int fax_rx(FaxState state, short[] samples, int length) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Receive(samples, length);
    }

    /// <summary>
    /// Performs the <c>fax_rx_fillin</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="length">The length value.</param>
    /// <returns>The result of the operation.</returns>
    public static int fax_rx_fillin(FaxState state, int length) {
        ArgumentNullException.ThrowIfNull(state);
        return state.ReceiveFillIn(length);
    }

    /// <summary>
    /// Performs the <c>fax_tx</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="destination">The destination value.</param>
    /// <param name="maximumLength">The maximum length value.</param>
    /// <returns>The result of the operation.</returns>
    public static int fax_tx(FaxState state, short[] destination, int maximumLength) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Transmit(destination, maximumLength);
    }

    /// <summary>
    /// Performs the <c>fax_set_transmit_on_idle</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="transmitOnIdle">The transmit on idle value.</param>
    public static void fax_set_transmit_on_idle(FaxState state, int transmitOnIdle) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetTransmitOnIdle(transmitOnIdle != 0);
    }

    /// <summary>
    /// Performs the <c>fax_set_tep_mode</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="useTep">The use tep value.</param>
    public static void fax_set_tep_mode(FaxState state, int useTep) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetTepMode(useTep != 0);
    }

    /// <summary>
    /// Performs the <c>fax_get_t30_state</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static T30State fax_get_t30_state(FaxState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetT30State();
    }

    /// <summary>
    /// Performs the <c>fax_get_logging_state</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static SpanLogState fax_get_logging_state(FaxState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetLoggingState();
    }

    /// <summary>
    /// Performs the <c>fax_restart</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="callingParty">The calling party value.</param>
    /// <returns>The result of the operation.</returns>
    public static int fax_restart(FaxState state, bool callingParty) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Restart(callingParty);
    }

    /// <summary>
    /// Performs the <c>fax_init</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="callingParty">The calling party value.</param>
    /// <returns>The result of the operation.</returns>
    public static FaxState fax_init(FaxState? state, bool callingParty) {
        if (state is null)
            return new FaxState(callingParty);

        state.Initialize(callingParty);
        return state;
    }

    /// <summary>
    /// Performs the <c>fax_release</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int fax_release(FaxState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    /// <summary>
    /// Performs the <c>fax_free</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int fax_free(FaxState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Free();
    }
}
