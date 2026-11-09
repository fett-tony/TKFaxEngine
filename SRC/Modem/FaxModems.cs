/*
 * TKFaxEngine - managed C# port
 *
 * FaxModems.cs - combined port of fax_modems.c and fax_modems.h
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2003, 2005, 2006, 2008, 2013 Steve Underwood.
 *
 * This port preserves the GNU Lesser General Public License version 2.1
 * terms of the original source files.
 *
 * The native fax_modems module directly owns and drives the V.17, V.27ter
 * and V.29 DSP states. This managed port keeps the same direct ownership and
 * callback wiring; no external modem backend or factory is used.
 */

#nullable enable

using global::TKFaxEngine.Audio;
using global::TKFaxEngine.Daten.T30;
using global::TKFaxEngine.Modem.V17;
using global::TKFaxEngine.Modem.V27;
using global::TKFaxEngine.Modem.V29;

namespace TKFaxEngine.Modem;

/// <summary>
/// Receives a block of signed 16-bit, 8 kHz PCM audio.
/// </summary>
public delegate int FaxReceiveAudioHandler(ReadOnlySpan<short> samples);

/// <summary>
/// Accounts for a missing block of received audio.
/// </summary>
public delegate int FaxReceiveFillInHandler(int sampleCount);

/// <summary>
/// Generates signed 16-bit, 8 kHz PCM audio.
/// </summary>
public delegate int FaxTransmitAudioHandler(Span<short> samples);

/// <summary>
/// Supplies one decoded bit or a negative modem-status value.
/// </summary>
public delegate void FaxFastPutBitHandler(int bitOrStatus);

/// <summary>
/// Requests the next bit for transmission.
/// </summary>
public delegate int FaxFastGetBitHandler();

/// <summary>
/// Reports a modem-status change and the current signal level.
/// </summary>
public delegate void FaxFastStatusHandler(int status, float signalPowerDbm0);

/// <summary>
/// Managed equivalent of fax_modems_state_t and fax_modems.c.
/// It directly owns HDLC, V.21 FSK, connection tones, timed silence and the
/// V.17, V.27ter and V.29 modem states.
/// </summary>
public sealed class FaxModems : IDisposable {
    public const int SampleRate = 8_000;
    public const int HdlcFramingOkThreshold = 5;

    private const int SignalTrainingSucceeded = -4;

    private readonly SilenceGenerator _silence = new();

    // Direct equivalents of fax_modems_state_t.fast_modems.
    private V17RxState? _v17Rx;
    private V17TxState? _v17Tx;
    private V27TerRxState? _v27TerRx;
    private V27TerTxState? _v27TerTx;
    private V29Rx? _v29Rx;
    private V29TxState? _v29Tx;

    private HdlcAcceptHandler? _hdlcAccept;
    private Action? _hdlcTransmitUnderflow;
    private NonEcmPutBitHandler? _nonEcmPutBit;
    private NonEcmGetBitHandler? _nonEcmGetBit;
    private ToneDetectedHandler? _toneDetected;
    private HdlcReceiver? _hdlcRx;
    private HdlcTransmitter? _hdlcTx;
    private FskTxState? _v21Tx;
    private FskRxState? _v21Rx;
    private ModemConnectTonesTxState? _connectTx;
    private ModemConnectTonesRxState? _connectRx;

    private FaxReceiveAudioHandler _rxHandler;
    private FaxReceiveAudioHandler _baseRxHandler;
    private FaxReceiveFillInHandler _rxFillInHandler;
    private FaxReceiveFillInHandler _baseRxFillInHandler;
    private FaxTransmitAudioHandler _txHandler;
    private FaxTransmitAudioHandler? _nextTxHandler;

    private FaxFastPutBitHandler? _activeFastPutBit;
    private FaxFastPutBitHandler? _rawV21PutBit;
    private FaxModemChannel _fastModem = FaxModemChannel.None;
    private readonly DcRestoreState _dcRestore = new();
    private bool _receiveActive;
    private bool _rxFrameReceived;
    private bool _initialized;
    private bool _disposed;

    public FaxModems() {
        _rxHandler = DummyReceive;
        _baseRxHandler = DummyReceive;
        _rxFillInHandler = DummyReceiveFillIn;
        _baseRxFillInHandler = DummyReceiveFillIn;
        _txHandler = GenerateSilence;
    }

    public bool UseTep { get; private set; }

    public bool Transmit { get; set; }

    public bool TransmitOnIdle { get; set; }

    public int RxBitRate { get; set; }

    public int TxBitRate { get; set; }

    public T30ModemType CurrentRxType { get; set; } = T30ModemType.None;

    public T30ModemType CurrentTxType { get; set; } = T30ModemType.None;

    public bool RxSignalPresent { get; internal set; }

    public bool RxTrained { get; internal set; }

    public bool RxFrameReceived {
        get => _rxFrameReceived;
        internal set => _rxFrameReceived = value;
    }

    public bool DeferredReceiveHandlerUpdates { get; set; }

    public FaxModemChannel FastModem => _fastModem;

    public bool HasNextTransmitHandler => _nextTxHandler is not null;

    public bool NextTransmitIsSilence => _nextTxHandler == GenerateSilence;

    public SpanLogState Logging { get; } = new();

    /// <summary>
    /// Equivalent to fax_modems_init(). Existing managed state is reset and
    /// callbacks are connected again.
    /// </summary>
    public void Initialize(
        bool useTep,
        HdlcAcceptHandler hdlcAccept,
        Action hdlcTransmitUnderflow,
        NonEcmPutBitHandler nonEcmPutBit,
        NonEcmGetBitHandler nonEcmGetBit,
        ToneDetectedHandler toneDetected) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(hdlcAccept);
        ArgumentNullException.ThrowIfNull(hdlcTransmitUnderflow);
        ArgumentNullException.ThrowIfNull(nonEcmPutBit);
        ArgumentNullException.ThrowIfNull(nonEcmGetBit);
        ArgumentNullException.ThrowIfNull(toneDetected);

        DisposeWorkingStates();

        LoggingApi.span_log_init(Logging, (int)SpanLogSeverity.None, null);
        LoggingApi.span_log_set_protocol(Logging, "FAX modems");

        _hdlcAccept = hdlcAccept;
        _hdlcTransmitUnderflow = hdlcTransmitUnderflow;
        _nonEcmPutBit = nonEcmPutBit;
        _nonEcmGetBit = nonEcmGetBit;
        _toneDetected = toneDetected;
        UseTep = useTep;
        TransmitOnIdle = false;
        Transmit = false;
        RxBitRate = 0;
        TxBitRate = 0;
        CurrentRxType = T30ModemType.None;
        CurrentTxType = T30ModemType.None;
        _fastModem = FaxModemChannel.None;
        DcRestore.dc_restore_init(_dcRestore);
        _rxFrameReceived = false;
        RxSignalPresent = false;
        RxTrained = false;
        _activeFastPutBit = null;
        _rawV21PutBit = null;
        _activeTransmitGetBit = null;
        DeferredReceiveHandlerUpdates = false;

        _connectTx = ModemConnectTones.TransmitInit(ModemConnectTone.FaxCng);
        _connectRx = ModemConnectTones.ReceiveInit(
            ModemConnectTone.FaxCng,
            OnToneReported,
            this);

        _hdlcRx = new HdlcReceiver(
            crc32: false,
            reportBadFrames: true,
            framingOkThreshold: HdlcFramingOkThreshold,
            frameHandler: OnHdlcFrame,
            userData: this);

        _hdlcTx = new HdlcTransmitter(
            crc32: false,
            interFrameFlags: 2,
            progressive: false,
            underflowHandler: OnHdlcUnderflow,
            userData: this);

        CreateV21Receiver(-39.09f);
        CreateV21Transmitter();

        _silence.SetDuration(0);
        _baseRxHandler = DummyReceive;
        _baseRxFillInHandler = DummyReceiveFillIn;
        _rxHandler = DummyReceive;
        _rxFillInHandler = DummyReceiveFillIn;
        _receiveActive = false;
        _txHandler = GenerateSilence;
        _nextTxHandler = null;
        _initialized = true;
    }

    public void Restart() {
        EnsureInitialized();
        CurrentTxType = (T30ModemType)(-1);
    }

    /// <summary>
    /// DC restoration used by fax_rx(), matching dc_restore() from the native
    /// audio helper.
    /// </summary>
    public short RestoreDc(short sample) {
        EnsureInitialized();
        return DcRestore.dc_restore(_dcRestore, sample);
    }

    public void ProcessReceive(short[] samples, int offset, int count) {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(samples);
        ValidateRange(samples.Length, offset, count);
        _rxHandler(samples.AsSpan(offset, count));
    }

    public int ProcessReceive(ReadOnlySpan<short> samples) {
        EnsureInitialized();
        return _rxHandler(samples);
    }

    public void ProcessReceiveFillIn(int sampleCount) {
        EnsureInitialized();
        ArgumentOutOfRangeException.ThrowIfNegative(sampleCount);
        _rxFillInHandler(sampleCount);
    }

    public int GenerateTransmit(short[] destination, int offset, int maximumCount) {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(destination);
        ValidateRange(destination.Length, offset, maximumCount);
        return GenerateTransmit(destination.AsSpan(offset, maximumCount));
    }

    public int GenerateTransmit(Span<short> samples) {
        EnsureInitialized();
        int generated = _txHandler(samples);
        if (generated < 0 || generated > samples.Length) {
            throw new InvalidOperationException(
                $"Transmit handler returned an invalid sample count: {generated}.");
        }

        return generated;
    }

    /// <summary>
    /// Advances a two-stage transmit operation. A transition to either the
    /// configured next handler or idle silence counts as a completed step.
    /// </summary>
    public int SetNextTransmitType() {
        EnsureInitialized();

        if (_nextTxHandler is not null) {
            _txHandler = _nextTxHandler;
            _nextTxHandler = null;
            return 0;
        }

        _silence.SetDuration(0);
        _txHandler = GenerateSilence;
        _nextTxHandler = null;
        Transmit = false;
        return -1;
    }

    public void InitializeHdlcReceiver(
        bool useCrc32,
        bool reportBadFrames,
        int framingOkThreshold) {
        EnsureInitialized();
        _hdlcRx?.Dispose();
        _hdlcRx = new HdlcReceiver(
            useCrc32,
            reportBadFrames,
            framingOkThreshold,
            OnHdlcFrame,
            this);
        _rxFrameReceived = false;
    }

    public void InitializeHdlcTransmitter(bool progressive) {
        EnsureInitialized();
        _hdlcTx?.Dispose();
        _hdlcTx = new HdlcTransmitter(
            crc32: false,
            interFrameFlags: 2,
            progressive: progressive,
            underflowHandler: OnHdlcUnderflow,
            userData: this);
    }

    public void ConfigureRawV21Receiver(FaxFastPutBitHandler putBit, float cutoffDbm0 = -39.09f) {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(putBit);
        _rawV21PutBit = putBit;
        CreateV21Receiver(cutoffDbm0);
        SetReceiveHandler(ReceiveV21, FillInV21);
    }

    public void ConfigureHdlcV21Receiver(float cutoffDbm0 = -39.09f) {
        EnsureInitialized();
        _rawV21PutBit = null;
        CreateV21Receiver(cutoffDbm0);
        SetReceiveHandler(ReceiveV21, FillInV21);
    }

    public void StartSlowModem(FaxModemChannel channel) {
        EnsureInitialized();

        switch (channel) {
            case FaxModemChannel.V21Rx:
                CreateV21Receiver(-39.09f);
                SetReceiveHandler(ReceiveV21, FillInV21);
                break;

            case FaxModemChannel.CedToneRx:
                ReplaceConnectReceiver(ModemConnectTone.FaxCed);
                SetReceiveHandler(ReceiveConnectTone, FillInConnectTone);
                break;

            case FaxModemChannel.CngToneRx:
                ReplaceConnectReceiver(ModemConnectTone.FaxCng);
                SetReceiveHandler(ReceiveConnectTone, FillInConnectTone);
                break;

            case FaxModemChannel.V21Tx:
                CreateV21Transmitter();
                SetTransmitHandler(GenerateV21);
                SetNextTransmitHandler(null);
                break;

            case FaxModemChannel.CedToneTx:
                ReplaceConnectTransmitter(ModemConnectTone.FaxCed);
                SetTransmitHandler(GenerateConnectTone);
                SetNextTransmitHandler(null);
                break;

            case FaxModemChannel.CngToneTx:
                ReplaceConnectTransmitter(ModemConnectTone.FaxCng);
                SetTransmitHandler(GenerateConnectTone);
                SetNextTransmitHandler(null);
                break;

            case FaxModemChannel.NoCngToneTx:
            case FaxModemChannel.SilenceTx:
                _silence.SetDuration(int.MaxValue);
                SetTransmitHandler(GenerateSilence);
                SetNextTransmitHandler(null);
                break;

            case FaxModemChannel.SilenceRx:
                SetReceiveIdle();
                break;

            case FaxModemChannel.Flush:
                _hdlcRx!.Restart();
                _hdlcTx!.Restart();
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(channel),
                    channel,
                    "The selected channel is not a slow FAX modem or tone function.");
        }

        _rxFrameReceived = false;
    }

    public void StartFastModem(
        FaxModemChannel channel,
        int bitRate,
        bool shortTrain,
        bool useHdlc) {
        EnsureInitialized();
        ValidateBitRate(bitRate);

        FaxFastGetBitHandler getBit = useHdlc
            ? GetHdlcTransmitBit
            : GetNonEcmTransmitBit;

        FaxFastPutBitHandler putBit = useHdlc
            ? PutHdlcReceiveBit
            : PutNonEcmReceiveBit;

        _activeTransmitGetBit = getBit;
        _activeFastPutBit = putBit;
        bool modemChanged = _fastModem != channel;

        if (modemChanged) {
            ReleaseFastModemState();
            _fastModem = channel;
            shortTrain = false;

            switch (channel) {
                case FaxModemChannel.V17Rx:
                    RxBitRate = bitRate;
                    _v17Rx = new V17RxState(bitRate, OnV17PutBit, this);
                    _v17Rx.SetModemStatusHandler(OnV17Status, this);
                    SetReceiveHandler(ReceiveFastAndV21, FillInFastAndV21);
                    break;

                case FaxModemChannel.V27TerRx:
                    RxBitRate = bitRate;
                    _v27TerRx = V27TerRx.Initialize(null, bitRate, OnV27TerPutBit, this)
                        ?? throw InvalidFastBitRate(channel, bitRate);
                    V27TerRx.SetModemStatusHandler(_v27TerRx, OnV27TerStatus, this);
                    SetReceiveHandler(ReceiveFastAndV21, FillInFastAndV21);
                    break;

                case FaxModemChannel.V29Rx:
                    RxBitRate = bitRate;
                    _v29Rx = new V29Rx(bitRate, OnV29PutBit);
                    _v29Rx.SetSignalCutoff(-45.5f);
                    _v29Rx.SetModemStatusHandler(OnV29Status);
                    SetReceiveHandler(ReceiveFastAndV21, FillInFastAndV21);
                    break;

                case FaxModemChannel.V17Tx:
                    TxBitRate = bitRate;
                    _v17Tx = new V17TxState(
                        bitRate,
                        UseTep,
                        static userData => ((FaxModems)userData!).GetActiveTransmitBit(),
                        this);
                    SetTransmitHandler(GenerateFastModem);
                    SetNextTransmitHandler(null);
                    break;
                case FaxModemChannel.V27TerTx:
                    TxBitRate = bitRate;
                    _v27TerTx = new V27TerTxState(
                        bitRate,
                        UseTep,
                        static userData => ((FaxModems)userData!).GetActiveTransmitBit(),
                        this);
                    SetTransmitHandler(GenerateFastModem);
                    SetNextTransmitHandler(null);
                    break;
                case FaxModemChannel.V29Tx:
                    TxBitRate = bitRate;
                    _v29Tx = new V29TxState(
                        bitRate,
                        UseTep,
                        static userData => ((FaxModems)userData!).GetActiveTransmitBit(),
                        this);
                    SetTransmitHandler(GenerateFastModem);
                    SetNextTransmitHandler(null);
                    break;
                case FaxModemChannel.V34Rx:
                case FaxModemChannel.V34Tx:
                    SetNextTransmitHandler(null);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(channel),
                        channel,
                        "The selected channel is not a fast FAX modem.");
            }
        } else {
            switch (channel) {
                case FaxModemChannel.V17Rx:
                    RxBitRate = bitRate;
                    if (_v17Rx is null || _v17Rx.Restart(bitRate, shortTrain ? 1 : 0) < 0)
                        throw InvalidFastBitRate(channel, bitRate);
                    _v17Rx.SetPutBit(OnV17PutBit, this);
                    _v17Rx.SetModemStatusHandler(OnV17Status, this);
                    SetReceiveHandler(ReceiveFastAndV21, FillInFastAndV21);
                    break;

                case FaxModemChannel.V27TerRx:
                    RxBitRate = bitRate;
                    if (_v27TerRx is null || _v27TerRx.Restart(bitRate, oldTraining: false) < 0)
                        throw InvalidFastBitRate(channel, bitRate);
                    V27TerRx.SetPutBit(_v27TerRx, OnV27TerPutBit, this);
                    V27TerRx.SetModemStatusHandler(_v27TerRx, OnV27TerStatus, this);
                    SetReceiveHandler(ReceiveFastAndV21, FillInFastAndV21);
                    break;

                case FaxModemChannel.V29Rx:
                    RxBitRate = bitRate;
                    if (_v29Rx is null || _v29Rx.Restart(bitRate, oldTrain: false) < 0)
                        throw InvalidFastBitRate(channel, bitRate);
                    _v29Rx.SetPutBitHandler(OnV29PutBit);
                    _v29Rx.SetModemStatusHandler(OnV29Status);
                    SetReceiveHandler(ReceiveFastAndV21, FillInFastAndV21);
                    break;

                case FaxModemChannel.V17Tx:
                    TxBitRate = bitRate;
                    if (_v17Tx is null || _v17Tx.Restart(bitRate, UseTep, shortTrain) < 0)
                        throw InvalidFastBitRate(channel, bitRate);
                    _v17Tx.SetGetBit(
                        static userData => ((FaxModems)userData!).GetActiveTransmitBit(),
                        this);
                    SetTransmitHandler(GenerateFastModem);
                    SetNextTransmitHandler(null);
                    break;

                case FaxModemChannel.V27TerTx:
                    TxBitRate = bitRate;
                    if (_v27TerTx is null || _v27TerTx.Restart(bitRate, UseTep) < 0)
                        throw InvalidFastBitRate(channel, bitRate);
                    _v27TerTx.SetGetBitHandler(
                        static userData => ((FaxModems)userData!).GetActiveTransmitBit(),
                        this);
                    SetTransmitHandler(GenerateFastModem);
                    SetNextTransmitHandler(null);
                    break;

                case FaxModemChannel.V29Tx:
                    TxBitRate = bitRate;
                    if (_v29Tx is null || _v29Tx.Restart(bitRate, UseTep) < 0)
                        throw InvalidFastBitRate(channel, bitRate);
                    _v29Tx.SetGetBitHandler(
                        static userData => ((FaxModems)userData!).GetActiveTransmitBit(),
                        this);
                    SetTransmitHandler(GenerateFastModem);
                    SetNextTransmitHandler(null);
                    break;
                case FaxModemChannel.V34Rx:
                case FaxModemChannel.V34Tx:
                    SetNextTransmitHandler(null);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(channel),
                        channel,
                        "The selected channel is not a fast FAX modem.");
            }
        }

        _rxFrameReceived = false;
    }

    public void SetReceiveIdle() {
        EnsureInitialized();
        SetReceiveActive(false);
        RxSignalPresent = false;
        RxTrained = false;
    }

    public void ConfigureTransmitPause(int durationSamples) {
        EnsureInitialized();
        ArgumentOutOfRangeException.ThrowIfNegative(durationSamples);
        _silence.SetDuration(durationSamples);
        SetTransmitHandler(GenerateSilence);
        SetNextTransmitHandler(null);
        Transmit = true;
    }

    public void ConfigureTransmitTone(
        FaxModemChannel channel,
        bool continueWithSilence = false) {
        EnsureInitialized();
        if (channel is not FaxModemChannel.CedToneTx
            and not FaxModemChannel.CngToneTx
            and not FaxModemChannel.NoCngToneTx) {
            throw new ArgumentOutOfRangeException(nameof(channel), channel, "Not a transmit tone.");
        }

        StartSlowModem(channel);
        if (continueWithSilence) {
            _silence.SetDuration(0);
            SetNextTransmitHandler(GenerateSilence);
        }
        Transmit = true;
    }

    public void ConfigureTransmitV21(int preambleFlags, int pauseSamples) {
        EnsureInitialized();
        ArgumentOutOfRangeException.ThrowIfNegative(preambleFlags);
        ArgumentOutOfRangeException.ThrowIfNegative(pauseSamples);

        _hdlcTx!.Flags(preambleFlags);
        CreateV21Transmitter();

        if (pauseSamples > 0) {
            _silence.SetDuration(pauseSamples);
            SetTransmitHandler(GenerateSilence);
            SetNextTransmitHandler(GenerateV21);
        } else {
            SetTransmitHandler(GenerateV21);
            SetNextTransmitHandler(null);
        }

        Transmit = true;
    }

    public void ConfigureTransmitFast(
        FaxModemChannel channel,
        int bitRate,
        bool shortTrain,
        bool useHdlc,
        int preambleFlags,
        int pauseSamples) {
        EnsureInitialized();
        ArgumentOutOfRangeException.ThrowIfNegative(preambleFlags);
        ArgumentOutOfRangeException.ThrowIfNegative(pauseSamples);

        if (!IsTransmitChannel(channel))
            throw new ArgumentOutOfRangeException(nameof(channel), channel, "Not a fast transmit modem.");

        if (useHdlc)
            _hdlcTx!.Flags(preambleFlags);

        StartFastModem(channel, bitRate, shortTrain, useHdlc);
        FaxTransmitAudioHandler fastHandler = GenerateFastModem;

        if (pauseSamples > 0) {
            _silence.SetDuration(pauseSamples);
            SetTransmitHandler(GenerateSilence);
            SetNextTransmitHandler(fastHandler);
        } else {
            SetTransmitHandler(fastHandler);
            SetNextTransmitHandler(null);
        }

        Transmit = true;
    }

    public void StopTransmit() {
        EnsureInitialized();
        _silence.SetDuration(0);
        _txHandler = GenerateSilence;
        _nextTxHandler = null;
        Transmit = false;
    }

    public void SetTepMode(bool useTep) {
        EnsureInitialized();
        UseTep = useTep;
    }

    /// <summary>
    /// Forwards an HDLC frame or status report to the configured upper layer.
    /// This is the managed equivalent of fax_modems_hdlc_accept().
    /// </summary>
    public void AcceptHdlcFrame(
        ReadOnlyMemory<byte>? packet,
        int lengthOrStatus,
        bool ok) {
        EnsureInitialized();
        if (lengthOrStatus >= 0 && ok)
            _rxFrameReceived = true;

        _hdlcAccept!(packet, lengthOrStatus, ok);
    }

    /// <summary>
    /// Processes a block through the active fast receiver and V.21 receiver
    /// in parallel, matching fax_modems_v17_v21_rx(),
    /// fax_modems_v27ter_v21_rx() and fax_modems_v29_v21_rx().
    /// </summary>
    public int ProcessFastAndV21(ReadOnlySpan<short> samples) {
        EnsureInitialized();
        return ReceiveFastAndV21(samples);
    }

    /// <summary>
    /// Missing-sample counterpart of ProcessFastAndV21().
    /// </summary>
    public int ProcessFastAndV21FillIn(int sampleCount) {
        EnsureInitialized();
        ArgumentOutOfRangeException.ThrowIfNegative(sampleCount);
        return FillInFastAndV21(sampleCount);
    }

    public void TransmitHdlcFrame(ReadOnlyMemory<byte> frame) {
        EnsureInitialized();
        _hdlcTx!.Frame(frame.Span);
    }

    public void StartHdlcTransmit(ReadOnlyMemory<byte> frame, bool append) {
        EnsureInitialized();
        if (!append)
            _hdlcTx!.Restart();
        _hdlcTx!.Frame(frame.Span);
    }

    public void CorruptHdlcTransmit() {
        EnsureInitialized();
        _hdlcTx!.CorruptFrame();
    }

    public void StopHdlcTransmit() {
        EnsureInitialized();
        _hdlcTx!.Frame(ReadOnlySpan<byte>.Empty);
    }

    public void RestartHdlcTransmitter() {
        EnsureInitialized();
        _hdlcTx!.Restart();
    }

    public void TransmitHdlcFlags(int flags) {
        EnsureInitialized();
        _hdlcTx!.Flags(flags);
    }

    public string ConnectToneToString(int tone) {
        return ModemConnectTones.ToneToString((ModemConnectTone)tone);
    }

    /// <summary>
    /// Managed equivalent of fax_modems_set_put_bit().
    /// </summary>
    public void SetPutBit(NonEcmPutBitHandler putBit) {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(putBit);
        _nonEcmPutBit = putBit;
    }

    /// <summary>
    /// Managed equivalent of fax_modems_set_get_bit().
    /// </summary>
    public void SetGetBit(NonEcmGetBitHandler getBit) {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(getBit);
        _nonEcmGetBit = getBit;
    }

    /// <summary>
    /// Managed equivalent of fax_modems_set_rx_handler().
    /// </summary>
    public void SetReceiveHandler(
        FaxReceiveAudioHandler receiveHandler,
        FaxReceiveFillInHandler receiveFillInHandler) {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(receiveHandler);
        ArgumentNullException.ThrowIfNull(receiveFillInHandler);

        if (DeferredReceiveHandlerUpdates) {
            if (_rxHandler != DummyReceive)
                _rxHandler = receiveHandler;
            _baseRxHandler = receiveHandler;

            if (_rxFillInHandler != DummyReceiveFillIn)
                _rxFillInHandler = receiveFillInHandler;
            _baseRxFillInHandler = receiveFillInHandler;
        } else {
            _rxHandler = receiveHandler;
            _rxFillInHandler = receiveFillInHandler;
        }

        _receiveActive = _rxHandler != DummyReceive;
    }

    /// <summary>
    /// Managed equivalent of fax_modems_set_rx_active().
    /// </summary>
    public void SetReceiveActive(bool active) {
        EnsureInitialized();
        _receiveActive = active;
        _rxHandler = active ? _baseRxHandler : DummyReceive;
        _rxFillInHandler = active ? _baseRxFillInHandler : DummyReceiveFillIn;
    }

    /// <summary>
    /// Managed equivalent of fax_modems_set_tx_handler().
    /// </summary>
    public void SetTransmitHandler(FaxTransmitAudioHandler handler) {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(handler);
        _txHandler = handler;
    }

    /// <summary>
    /// Managed equivalent of fax_modems_set_next_tx_handler().
    /// </summary>
    public void SetNextTransmitHandler(FaxTransmitAudioHandler? handler) {
        EnsureInitialized();
        _nextTxHandler = handler;
    }

    public int Release() {
        if (_disposed)
            return 0;

        DisposeWorkingStates();
        _hdlcAccept = null;
        _hdlcTransmitUnderflow = null;
        _nonEcmPutBit = null;
        _nonEcmGetBit = null;
        _toneDetected = null;
        _initialized = false;
        Transmit = false;
        return 0;
    }

    public int Free() {
        Dispose();
        return 0;
    }

    public void Dispose() {
        if (_disposed)
            return;

        DisposeWorkingStates();
        _hdlcAccept = null;
        _hdlcTransmitUnderflow = null;
        _nonEcmPutBit = null;
        _nonEcmGetBit = null;
        _toneDetected = null;
        Logging.Dispose();
        _initialized = false;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public static string ModemToString(FaxModemChannel modem) {
        return modem switch {
            FaxModemChannel.None => "None",
            FaxModemChannel.Flush => "Flush",
            FaxModemChannel.SilenceTx => "Silence Tx",
            FaxModemChannel.SilenceRx => "Silence Rx",
            FaxModemChannel.CedToneTx => "CED Tx",
            FaxModemChannel.CngToneTx => "CNG Tx",
            FaxModemChannel.NoCngToneTx => "No CNG Tx",
            FaxModemChannel.CedToneRx => "CED Rx",
            FaxModemChannel.CngToneRx => "CNG Rx",
            FaxModemChannel.V21Tx => "V.21 Tx",
            FaxModemChannel.V17Tx => "V.17 Tx",
            FaxModemChannel.V27TerTx => "V.27ter Tx",
            FaxModemChannel.V29Tx => "V.29 Tx",
            FaxModemChannel.V21Rx => "V.21 Rx",
            FaxModemChannel.V17Rx => "V.17 Rx",
            FaxModemChannel.V27TerRx => "V.27ter Rx",
            FaxModemChannel.V29Rx => "V.29 Rx",
            FaxModemChannel.V34Tx => "V.34 HDX Tx",
            FaxModemChannel.V34Rx => "V.34 HDX Rx",
            _ => "???"
        };
    }

    private int ReceiveV21(ReadOnlySpan<short> samples) {
        return _v21Rx!.Process(samples);
    }

    private int FillInV21(int sampleCount) {
        return _v21Rx!.FillIn(sampleCount);
    }

    private int ReceiveConnectTone(ReadOnlySpan<short> samples) {
        return _connectRx!.Process(samples);
    }

    private int FillInConnectTone(int sampleCount) {
        return _connectRx!.FillIn(sampleCount);
    }

    private int ReceiveFastAndV21(ReadOnlySpan<short> samples) {
        ReceiveActiveFastModem(samples);
        _v21Rx!.Process(samples);

        if (_rxFrameReceived) {
            Logging.Log(
                (int)SpanLogSeverity.Flow,
                "Switching from %s + V.21 to V.21 (%.2fdBm0)\n",
                ModemToString(_fastModem),
                _v21Rx.SignalPowerDbm0);
            SetReceiveHandler(ReceiveV21, FillInV21);
        }

        return 0;
    }

    private int FillInFastAndV21(int sampleCount) {
        FillInActiveFastModem(sampleCount);
        _v21Rx!.FillIn(sampleCount);
        return 0;
    }

    private int ReceiveFastOnly(ReadOnlySpan<short> samples) {
        return ReceiveActiveFastModem(samples);
    }

    private int FillInFastOnly(int sampleCount) {
        return FillInActiveFastModem(sampleCount);
    }

    private int GenerateV21(Span<short> samples) {
        return _v21Tx!.Generate(samples);
    }

    private int GenerateConnectTone(Span<short> samples) {
        return _connectTx!.Generate(samples);
    }

    private int GenerateFastModem(Span<short> samples) {
        return _fastModem switch {
            FaxModemChannel.V17Tx => _v17Tx!.Transmit(samples),
            FaxModemChannel.V27TerTx => _v27TerTx!.Transmit(samples),
            FaxModemChannel.V29Tx => _v29Tx!.Transmit(samples),
            _ => 0
        };
    }

    private int GenerateSilence(Span<short> samples) {
        return _silence.Generate(samples);
    }

    private void CreateV21Receiver(float cutoffDbm0) {
        _v21Rx?.Dispose();
        _v21Rx = Fsk.InitializeReceiver(
            state: null,
            spec: Fsk.GetPreset(FskPreset.V21Channel2),
            framingMode: FskFrameMode.Synchronous,
            putBit: static (userData, bit) => ((FaxModems)userData!).OnV21ReceiveBit(bit),
            userData: this);
        _v21Rx.SetSignalCutoff(cutoffDbm0);
    }

    private void OnV21ReceiveBit(int bit) {
        if (_rawV21PutBit is not null)
            _rawV21PutBit(bit);
        else
            PutHdlcReceiveBit(bit);
    }

    private void CreateV21Transmitter() {
        _v21Tx?.Dispose();
        _v21Tx = Fsk.InitializeTransmitter(
            state: null,
            spec: Fsk.GetPreset(FskPreset.V21Channel2),
            getBit: static userData => ((FaxModems)userData!).GetHdlcTransmitBit(),
            userData: this);
    }

    private void ReplaceConnectReceiver(ModemConnectTone tone) {
        _connectRx?.Dispose();
        _connectRx = ModemConnectTones.ReceiveInit(tone, OnToneReported, this);
    }

    private void ReplaceConnectTransmitter(ModemConnectTone tone) {
        _connectTx?.Dispose();
        _connectTx = ModemConnectTones.TransmitInit(tone);
    }

    private void OnHdlcFrame(
        object? userData,
        ReadOnlyMemory<byte>? packet,
        int lengthOrStatus,
        bool ok) {
        _ = userData;
        AcceptHdlcFrame(packet, lengthOrStatus, ok);
    }

    private void OnHdlcUnderflow(object? userData) {
        _ = userData;
        _hdlcTransmitUnderflow!();
    }

    private void OnToneReported(
        object? userData,
        ModemConnectTone tone,
        int level,
        int duration) {
        _ = userData;
        _toneDetected!((int)tone, level, duration);
    }

    private void OnFastModemStatus(int status, float signalPowerDbm0) {
        if (status == SignalTrainingSucceeded) {
            Logging.Log(
                (int)SpanLogSeverity.Flow,
                "Switching from %s + V.21 to %s (%.2fdBm0)\n",
                ModemToString(_fastModem),
                ModemToString(_fastModem),
                signalPowerDbm0);
            SetReceiveHandler(ReceiveFastOnly, FillInFastOnly);
            DisableActiveFastStatusHandler();
        }

        _activeFastPutBit?.Invoke(status);
    }

    private void DisableActiveFastStatusHandler() {
        switch (_fastModem) {
            case FaxModemChannel.V17Rx:
                _v17Rx?.SetModemStatusHandler(null, this);
                break;

            case FaxModemChannel.V27TerRx:
                if (_v27TerRx is not null)
                    V27TerRx.SetModemStatusHandler(_v27TerRx, null, this);
                break;

            case FaxModemChannel.V29Rx:
                _v29Rx?.SetModemStatusHandler(null);
                break;
        }
    }

    private int ReceiveActiveFastModem(ReadOnlySpan<short> samples) {
        return _fastModem switch {
            FaxModemChannel.V17Rx => _v17Rx!.Receive(samples),
            FaxModemChannel.V27TerRx => _v27TerRx!.Receive(samples),
            FaxModemChannel.V29Rx => _v29Rx!.Process(samples),
            _ => 0
        };
    }

    private int FillInActiveFastModem(int sampleCount) {
        return _fastModem switch {
            FaxModemChannel.V17Rx => _v17Rx!.ReceiveFillIn(sampleCount),
            FaxModemChannel.V27TerRx => _v27TerRx!.FillIn(sampleCount),
            FaxModemChannel.V29Rx => _v29Rx!.FillIn(sampleCount),
            _ => 0
        };
    }

    private int GetActiveTransmitBit() {
        return _fastModem switch {
            FaxModemChannel.V17Tx or FaxModemChannel.V27TerTx or FaxModemChannel.V29Tx
                => _activeTransmitGetBit!(),
            _ => -1
        };
    }

    private FaxFastGetBitHandler? _activeTransmitGetBit;

    private static void OnV17PutBit(object? userData, int bitOrStatus) =>
        ((FaxModems)userData!)._activeFastPutBit?.Invoke(bitOrStatus);

    private static void OnV17Status(object? userData, int status) {
        FaxModems state = (FaxModems)userData!;
        state.OnFastModemStatus(status, state._v17Rx?.SignalPower ?? -99.0f);
    }

    private static void OnV27TerPutBit(object? userData, int bitOrStatus) =>
        ((FaxModems)userData!)._activeFastPutBit?.Invoke(bitOrStatus);

    private static void OnV27TerStatus(object? userData, int status) {
        FaxModems state = (FaxModems)userData!;
        float power = state._v27TerRx is null
            ? -99.0f
            : V27TerRx.GetSignalPower(state._v27TerRx);
        state.OnFastModemStatus(status, power);
    }

    private void OnV29PutBit(int bitOrStatus) =>
        _activeFastPutBit?.Invoke(bitOrStatus);

    private void OnV29Status(V29Rx.V29RxStatus status) =>
        OnFastModemStatus((int)status, _v29Rx?.SignalPower ?? -99.0f);

    private static ArgumentOutOfRangeException InvalidFastBitRate(
        FaxModemChannel channel,
        int bitRate) =>
        new(nameof(bitRate), bitRate, $"{ModemToString(channel)} does not support this bit rate.");

    private void PutHdlcReceiveBit(int bitOrStatus) {
        _hdlcRx!.PutBit(bitOrStatus);
    }

    private void PutNonEcmReceiveBit(int bitOrStatus) {
        _nonEcmPutBit!(bitOrStatus);
    }

    private int GetHdlcTransmitBit() {
        return _hdlcTx!.GetBit();
    }

    private int GetNonEcmTransmitBit() {
        return _nonEcmGetBit!();
    }

    private static int DummyReceive(ReadOnlySpan<short> samples) {
        _ = samples;
        return 0;
    }

    private static int DummyReceiveFillIn(int sampleCount) {
        _ = sampleCount;
        return 0;
    }

    private void EnsureInitialized() {
        ThrowIfDisposed();
        if (!_initialized)
            throw new InvalidOperationException("FaxModems.Initialize must be called first.");
    }

    private void ReleaseFastModemState() {
        _v17Rx?.Dispose();
        _v17Tx?.Dispose();
        _v27TerRx?.Dispose();
        _v27TerTx?.Dispose();
        _v29Tx?.Dispose();
        _v29Rx?.Release();

        _v17Rx = null;
        _v17Tx = null;
        _v27TerRx = null;
        _v27TerTx = null;
        _v29Rx = null;
        _v29Tx = null;
    }

    private void DisposeWorkingStates() {
        _hdlcRx?.Dispose();
        _hdlcTx?.Dispose();
        _v21Rx?.Dispose();
        _v21Tx?.Dispose();
        _connectRx?.Dispose();
        _connectTx?.Dispose();
        _v17Rx?.Dispose();
        _v17Tx?.Dispose();
        _v27TerRx?.Dispose();
        _v27TerTx?.Dispose();
        _v29Tx?.Dispose();
        _v29Rx?.Release();

        _hdlcRx = null;
        _hdlcTx = null;
        _v21Rx = null;
        _v21Tx = null;
        _connectRx = null;
        _connectTx = null;
        _v17Rx = null;
        _v17Tx = null;
        _v27TerRx = null;
        _v27TerTx = null;
        _v29Rx = null;
        _v29Tx = null;

        _rxHandler = DummyReceive;
        _baseRxHandler = DummyReceive;
        _rxFillInHandler = DummyReceiveFillIn;
        _baseRxFillInHandler = DummyReceiveFillIn;
        _txHandler = GenerateSilence;
        _nextTxHandler = null;
        _receiveActive = false;
        _activeFastPutBit = null;
        _rawV21PutBit = null;
        _activeTransmitGetBit = null;
        _silence.SetDuration(0);
    }

    private static bool IsReceiveChannel(FaxModemChannel channel) {
        return channel is FaxModemChannel.V17Rx
            or FaxModemChannel.V27TerRx
            or FaxModemChannel.V29Rx
            or FaxModemChannel.V34Rx
            ;
    }

    private static bool IsTransmitChannel(FaxModemChannel channel) {
        return channel is FaxModemChannel.V17Tx
            or FaxModemChannel.V27TerTx
            or FaxModemChannel.V29Tx
            or FaxModemChannel.V34Tx
            ;
    }

    private static void ValidateBitRate(int bitRate) {
        if (bitRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(bitRate));
    }

    private static void ValidateRange(int length, int offset, int count) {
        if (offset < 0 || count < 0 || offset > length - count)
            throw new ArgumentOutOfRangeException(nameof(count));
    }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class SilenceGenerator {
        private int _remainingSamples;

        internal void SetDuration(int sampleCount) {
            if (sampleCount < 0)
                throw new ArgumentOutOfRangeException(nameof(sampleCount));
            _remainingSamples = sampleCount;
        }

        internal int Generate(Span<short> samples) {
            if (_remainingSamples == 0 || samples.IsEmpty)
                return 0;

            int count = _remainingSamples == int.MaxValue
                ? samples.Length
                : Math.Min(samples.Length, _remainingSamples);

            samples[..count].Clear();
            if (_remainingSamples != int.MaxValue)
                _remainingSamples -= count;
            return count;
        }
    }
}

/// <summary>
/// Compatibility facade retaining the public C function names from
/// fax_modems.h where a direct managed equivalent is meaningful.
/// </summary>
public static class FaxModemsApi {
    public const int FAX_MODEM_NONE = (int)FaxModemChannel.None;
    public const int FAX_MODEM_FLUSH = (int)FaxModemChannel.Flush;
    public const int FAX_MODEM_SILENCE_TX = (int)FaxModemChannel.SilenceTx;
    public const int FAX_MODEM_SILENCE_RX = (int)FaxModemChannel.SilenceRx;
    public const int FAX_MODEM_CED_TONE_TX = (int)FaxModemChannel.CedToneTx;
    public const int FAX_MODEM_CNG_TONE_TX = (int)FaxModemChannel.CngToneTx;
    public const int FAX_MODEM_NOCNG_TONE_TX = (int)FaxModemChannel.NoCngToneTx;
    public const int FAX_MODEM_CED_TONE_RX = (int)FaxModemChannel.CedToneRx;
    public const int FAX_MODEM_CNG_TONE_RX = (int)FaxModemChannel.CngToneRx;
    public const int FAX_MODEM_V21_TX = (int)FaxModemChannel.V21Tx;
    public const int FAX_MODEM_V17_TX = (int)FaxModemChannel.V17Tx;
    public const int FAX_MODEM_V27TER_TX = (int)FaxModemChannel.V27TerTx;
    public const int FAX_MODEM_V29_TX = (int)FaxModemChannel.V29Tx;
    public const int FAX_MODEM_V21_RX = (int)FaxModemChannel.V21Rx;
    public const int FAX_MODEM_V17_RX = (int)FaxModemChannel.V17Rx;
    public const int FAX_MODEM_V27TER_RX = (int)FaxModemChannel.V27TerRx;
    public const int FAX_MODEM_V29_RX = (int)FaxModemChannel.V29Rx;
    public const int FAX_MODEM_V34_TX = (int)FaxModemChannel.V34Tx;
    public const int FAX_MODEM_V34_RX = (int)FaxModemChannel.V34Rx;

    public static string fax_modem_to_str(int modem) {
        return FaxModems.ModemToString((FaxModemChannel)modem);
    }

    public static FaxModems fax_modems_init(
        FaxModems? state,
        int useTep,
        HdlcAcceptHandler hdlcAccept,
        Action hdlcTxUnderflow,
        NonEcmPutBitHandler nonEcmPutBit,
        NonEcmGetBitHandler nonEcmGetBit,
        ToneDetectedHandler toneCallback) {
        state ??= new FaxModems();
        state.Initialize(
            useTep != 0,
            hdlcAccept,
            hdlcTxUnderflow,
            nonEcmPutBit,
            nonEcmGetBit,
            toneCallback);
        return state;
    }

    public static void fax_modems_hdlc_accept(
        FaxModems state,
        ReadOnlyMemory<byte>? message,
        int lengthOrStatus,
        int ok) {
        ArgumentNullException.ThrowIfNull(state);
        state.AcceptHdlcFrame(message, lengthOrStatus, ok != 0);
    }

    public static int fax_modems_v17_v21_rx(
        FaxModems state,
        ReadOnlySpan<short> samples) {
        ArgumentNullException.ThrowIfNull(state);
        return state.ProcessFastAndV21(samples);
    }

    public static int fax_modems_v27ter_v21_rx(
        FaxModems state,
        ReadOnlySpan<short> samples) {
        ArgumentNullException.ThrowIfNull(state);
        return state.ProcessFastAndV21(samples);
    }

    public static int fax_modems_v29_v21_rx(
        FaxModems state,
        ReadOnlySpan<short> samples) {
        ArgumentNullException.ThrowIfNull(state);
        return state.ProcessFastAndV21(samples);
    }

    public static int fax_modems_v17_v21_rx_fillin(FaxModems state, int length) {
        ArgumentNullException.ThrowIfNull(state);
        return state.ProcessFastAndV21FillIn(length);
    }

    public static int fax_modems_v27ter_v21_rx_fillin(FaxModems state, int length) {
        ArgumentNullException.ThrowIfNull(state);
        return state.ProcessFastAndV21FillIn(length);
    }

    public static int fax_modems_v29_v21_rx_fillin(FaxModems state, int length) {
        ArgumentNullException.ThrowIfNull(state);
        return state.ProcessFastAndV21FillIn(length);
    }

    public static SpanLogState fax_modems_get_logging_state(FaxModems state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Logging;
    }

    public static void fax_modems_hdlc_tx_frame(
        FaxModems state,
        ReadOnlyMemory<byte>? message,
        int length) {
        ArgumentNullException.ThrowIfNull(state);
        if (length == -1) {
            state.RestartHdlcTransmitter();
            return;
        }

        if (message is null) {
            if (length == 0) {
                state.StopHdlcTransmit();
                return;
            }
            throw new ArgumentNullException(nameof(message));
        }
        if (length < 0 || length > message.Value.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        state.TransmitHdlcFrame(message.Value[..length]);
    }

    public static void fax_modems_hdlc_tx_flags(FaxModems state, int flags) {
        ArgumentNullException.ThrowIfNull(state);
        state.TransmitHdlcFlags(flags);
    }

    public static void fax_modems_start_fast_modem(
        FaxModems state,
        int which,
        int bitRate,
        int shortTrain,
        int hdlcMode) {
        ArgumentNullException.ThrowIfNull(state);
        state.StartFastModem(
            (FaxModemChannel)which,
            bitRate,
            shortTrain != 0,
            hdlcMode != 0);
    }

    public static void fax_modems_start_slow_modem(FaxModems state, int which) {
        ArgumentNullException.ThrowIfNull(state);
        state.StartSlowModem((FaxModemChannel)which);
    }

    public static void fax_modems_set_tep_mode(FaxModems state, int useTep) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetTepMode(useTep != 0);
    }

    public static void fax_modems_set_put_bit(FaxModems state, NonEcmPutBitHandler putBit) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetPutBit(putBit);
    }

    public static void fax_modems_set_get_bit(FaxModems state, NonEcmGetBitHandler getBit) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetGetBit(getBit);
    }

    public static void fax_modems_set_rx_handler(
        FaxModems state,
        FaxReceiveAudioHandler rxHandler,
        FaxReceiveFillInHandler rxFillInHandler) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetReceiveHandler(rxHandler, rxFillInHandler);
    }

    public static void fax_modems_set_rx_active(FaxModems state, int active) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetReceiveActive(active != 0);
    }

    public static void fax_modems_set_tx_handler(
        FaxModems state,
        FaxTransmitAudioHandler handler) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetTransmitHandler(handler);
    }

    public static void fax_modems_set_next_tx_handler(
        FaxModems state,
        FaxTransmitAudioHandler? handler) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetNextTransmitHandler(handler);
    }

    public static int fax_modems_set_next_tx_type(FaxModems state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.SetNextTransmitType();
    }

    public static int fax_modems_restart(FaxModems state) {
        ArgumentNullException.ThrowIfNull(state);
        state.Restart();
        return 0;
    }

    public static int fax_modems_release(FaxModems state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    public static int fax_modems_free(FaxModems? state) {
        state?.Dispose();
        return 0;
    }
}
