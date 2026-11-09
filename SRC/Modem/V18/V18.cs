/*
 * TKFaxEngine - managed C# port
 *
 * V18.cs
 *
 * Combined port of:
 *   v18.h
 *   private/v18.h (merged into the supplied v18.h)
 *   v18.c
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2004-2015 Steve Underwood.
 *
 * This port preserves the LGPL-2.1 licensing terms of the original files.
 */

#nullable enable

using System.Text;
using TKFaxEngine.Audio;
using TKFaxEngine.Daten.T140;
using TKFaxEngine.Modem.V8;

namespace TKFaxEngine.Modem.V18;

/// <summary>
/// V.18 text telephone modes. Numeric values match the native V18_MODE_* constants.
/// </summary>
[Flags]
public enum V18Mode {
    /// <summary>
    /// Represents the <c>None</c> value.
    /// </summary>
    None = 0x0001,
    /// <summary>
    /// Represents the <c>Weitbrecht5Bit4545</c> value.
    /// </summary>
    Weitbrecht5Bit4545 = 0x0002,
    /// <summary>
    /// Represents the <c>Weitbrecht5Bit50</c> value.
    /// </summary>
    Weitbrecht5Bit50 = 0x0004,
    /// <summary>
    /// Represents the <c>Dtmf</c> value.
    /// </summary>
    Dtmf = 0x0008,
    /// <summary>
    /// Represents the <c>Edt</c> value.
    /// </summary>
    Edt = 0x0010,
    /// <summary>
    /// Represents the <c>Bell103</c> value.
    /// </summary>
    Bell103 = 0x0020,
    /// <summary>
    /// Represents the <c>V23Videotex</c> value.
    /// </summary>
    V23Videotex = 0x0040,
    /// <summary>
    /// Represents the <c>V21Textphone</c> value.
    /// </summary>
    V21Textphone = 0x0080,
    /// <summary>
    /// Represents the <c>V18Textphone</c> value.
    /// </summary>
    V18Textphone = 0x0100,
    /// <summary>
    /// Represents the <c>Weitbrecht5Bit476</c> value.
    /// </summary>
    Weitbrecht5Bit476 = 0x0200,
    /// <summary>
    /// Represents the <c>RepetitiveShiftsOption</c> value.
    /// </summary>
    RepetitiveShiftsOption = 0x1000
}

/// <summary>
/// National V.18 automoding preference sequence.
/// </summary>
public enum V18AutomodingMode {
    /// <summary>
    /// Represents the <c>Global</c> value.
    /// </summary>
    Global = 0,
    /// <summary>
    /// Represents the <c>None</c> value.
    /// </summary>
    None = 1,
    /// <summary>
    /// Represents the <c>Australia</c> value.
    /// </summary>
    Australia = 2,
    /// <summary>
    /// Represents the <c>Ireland</c> value.
    /// </summary>
    Ireland = 3,
    /// <summary>
    /// Represents the <c>Germany</c> value.
    /// </summary>
    Germany = 4,
    /// <summary>
    /// Represents the <c>Switzerland</c> value.
    /// </summary>
    Switzerland = 5,
    /// <summary>
    /// Represents the <c>Italy</c> value.
    /// </summary>
    Italy = 6,
    /// <summary>
    /// Represents the <c>Spain</c> value.
    /// </summary>
    Spain = 7,
    /// <summary>
    /// Represents the <c>Austria</c> value.
    /// </summary>
    Austria = 8,
    /// <summary>
    /// Represents the <c>Netherlands</c> value.
    /// </summary>
    Netherlands = 9,
    /// <summary>
    /// Represents the <c>Iceland</c> value.
    /// </summary>
    Iceland = 10,
    /// <summary>
    /// Represents the <c>Norway</c> value.
    /// </summary>
    Norway = 11,
    /// <summary>
    /// Represents the <c>Sweden</c> value.
    /// </summary>
    Sweden = 12,
    /// <summary>
    /// Represents the <c>Finland</c> value.
    /// </summary>
    Finland = 13,

    /// <summary>Compatibility alias for the misspelled native enum name.</summary>
    Finalnd = Finland,

    /// <summary>
    /// Represents the <c>Denmark</c> value.
    /// </summary>
    Denmark = 14,
    /// <summary>
    /// Represents the <c>UnitedKingdom</c> value.
    /// </summary>
    UnitedKingdom = 15,
    /// <summary>
    /// Represents the <c>Usa</c> value.
    /// </summary>
    Usa = 16,
    /// <summary>
    /// Represents the <c>France</c> value.
    /// </summary>
    France = 17,
    /// <summary>
    /// Represents the <c>Belgium</c> value.
    /// </summary>
    Belgium = 18,
    /// <summary>
    /// Represents the <c>End</c> value.
    /// </summary>
    End = 19
}

/// <summary>
/// V.18 mode-change status values.
/// </summary>
public enum V18Status {
    /// <summary>
    /// Represents the <c>SwitchToNone</c> value.
    /// </summary>
    SwitchToNone = 0,
    /// <summary>
    /// Represents the <c>SwitchToWeitbrecht5Bit4545</c> value.
    /// </summary>
    SwitchToWeitbrecht5Bit4545 = 1,
    /// <summary>
    /// Represents the <c>SwitchToWeitbrecht5Bit476</c> value.
    /// </summary>
    SwitchToWeitbrecht5Bit476 = 2,
    /// <summary>
    /// Represents the <c>SwitchToWeitbrecht5Bit50</c> value.
    /// </summary>
    SwitchToWeitbrecht5Bit50 = 3,
    /// <summary>
    /// Represents the <c>SwitchToDtmf</c> value.
    /// </summary>
    SwitchToDtmf = 4,
    /// <summary>
    /// Represents the <c>SwitchToEdt</c> value.
    /// </summary>
    SwitchToEdt = 5,
    /// <summary>
    /// Represents the <c>SwitchToBell103</c> value.
    /// </summary>
    SwitchToBell103 = 6,
    /// <summary>
    /// Represents the <c>SwitchToV23Videotex</c> value.
    /// </summary>
    SwitchToV23Videotex = 7,
    /// <summary>
    /// Represents the <c>SwitchToV21Textphone</c> value.
    /// </summary>
    SwitchToV21Textphone = 8,
    /// <summary>
    /// Represents the <c>SwitchToV18Textphone</c> value.
    /// </summary>
    SwitchToV18Textphone = 9
}

/// <summary>
/// Represents the <c>V18PutMessageHandler</c> callback.
/// </summary>
/// <param name="userData">The user data value.</param>
/// <param name="message">The message value.</param>
public delegate void V18PutMessageHandler(
    object? userData,
    ReadOnlySpan<byte> message);

/// <summary>
/// Represents the <c>V18StatusHandler</c> callback.
/// </summary>
/// <param name="userData">The user data value.</param>
/// <param name="status">The status value.</param>
public delegate void V18StatusHandler(
    object? userData,
    int status);

internal enum V18TransmitState {
    Originating1 = 1,
    Originating2 = 2,
    Originating3 = 3,
    OriginatingConnected = 42,
    Answering1 = 101,
    Answering2 = 102,
    Answering3 = 103,
    AnsweringConnected = 142
}

internal enum V18ReceiveState {
    Originating1 = 1,
    Originating2 = 2,
    Originating3 = 3,
    OriginatingConnected = 42,
    Answering1 = 101,
    Answering2 = 102,
    Answering3 = 103,
    AnsweringConnected = 142
}

internal enum V18ProbeTone {
    None = -1,
    Hz390 = 0,
    Hz980 = 1,
    Hz1180 = 2,
    Hz1270 = 3,
    Hz1300 = 4,
    Hz1400 = 5,
    Hz1650 = 6,
    Hz1800 = 7,
    Hz2225 = 8
}

internal enum V18ControlState {
    Released,
    Initializing,
    Calling,
    Answering,
    Monitoring,
    V8Calling,
    V8Answering,
    Connected,
    ReturningToAutomoding
}

/// <summary>
/// Managed equivalent of v18_state_t and the complete implemented behavior
/// from v18.c.
/// </summary>
public sealed partial class V18State : IDisposable {
    /// <summary>
    /// Defines the <c>SampleRate</c> constant.
    /// </summary>
    public const int SampleRate = 8000;
    /// <summary>
    /// Defines the <c>QueueCapacity</c> constant.
    /// </summary>
    public const int QueueCapacity = 128;
    /// <summary>
    /// Defines the <c>MaximumStoredMessageBytes</c> constant.
    /// </summary>
    public const int MaximumStoredMessageBytes = 80;
    /// <summary>
    /// Defines the <c>GoertzelSamplesPerBlock</c> constant.
    /// </summary>
    public const int GoertzelSamplesPerBlock = 102;

    private const int TddCharacterSuppressionMilliseconds = 480;
    private const int TddDrainSuppressionMilliseconds = 620;
    private const int AutomodingResumeMilliseconds = 10_000;

    private QueueState _queue = new(
        QueueCapacity,
        QueueFlags.ReadAtomic | QueueFlags.WriteAtomic);

    private FskTxState? _fskTransmitter;
    private FskRxState? _fskReceiver;
    private FskSpec? _activeReceiveSpec;
    private AsyncTransmitter? _asyncTransmitter;
    private ModemConnectTonesRxState? _answerToneReceiver;
    private bool _answerTonePresent;
    private ModemConnectTone _pendingAnswerTone = ModemConnectTone.None;
    private readonly DtmfTxState _dtmfTransmitter = new();

    private V8State? _v8State;
    private V8Parameters? _pendingV8Result;
    private bool _v8Active;
    private bool _v8TransmitActive;
    private bool _automodingConfigured;
    private bool _modemCallingParty;
    private bool _answerAnsFollowupActive;
    private int _carrierLossTimer;
    private bool _connectedCarrierSeen;

    private V18PutMessageHandler? _putMessage;
    private object? _putMessageUserData;
    private V18StatusHandler? _statusHandler;
    private object? _statusHandlerUserData;

    private int _baudotTransmitShift;
    private int _baudotReceiveShift;
    private int _transmitSignalOn;
    private bool _transmitDraining;
    private byte _nextByte = 0xFF;
    private readonly StringBuilder _receivedDtmf = new(32);
    private int _baudotCharactersSinceShift;
    private readonly T140Encoder _t140TransmitEncoder = new();
    private readonly T140Decoder _t140ReceiveDecoder = new();
    private bool _disposed;

    private int _toneDuration;
    private int _targetToneDuration;
    private V18ProbeTone _inTone = V18ProbeTone.None;

    /// <summary>
    /// Initializes a new instance of the <c>V18State</c> type.
    /// </summary>
    public V18State() {
        Logging = new SpanLogState((int)SpanLogSeverity.None, null);
        Logging.SetProtocol("V.18");
    }

    /// <summary>
    /// Initializes a new instance of the <c>V18State</c> type.
    /// </summary>
    /// <param name="callingParty">The calling party value.</param>
    /// <param name="mode">The mode value.</param>
    /// <param name="nation">The nation value.</param>
    /// <param name="putMessage">The put message value.</param>
    /// <param name="putMessageUserData">The put message user data value.</param>
    /// <param name="statusHandler">The status handler value.</param>
    /// <param name="statusHandlerUserData">The status handler user data value.</param>
    public V18State(
        bool callingParty,
        V18Mode mode,
        V18AutomodingMode nation,
        V18PutMessageHandler? putMessage,
        object? putMessageUserData,
        V18StatusHandler? statusHandler,
        object? statusHandlerUserData)
        : this() {
        Initialize(
            callingParty,
            mode,
            nation,
            putMessage,
            putMessageUserData,
            statusHandler,
            statusHandlerUserData);
    }

    /// <summary>
    /// Gets or sets the <c>CallingParty</c> value.
    /// </summary>
    public bool CallingParty { get; private set; }

    /// <summary>
    /// Gets or sets the <c>InitialMode</c> value.
    /// </summary>
    public V18Mode InitialMode { get; private set; }

    /// <summary>
    /// Gets or sets the <c>Nation</c> value.
    /// </summary>
    public V18AutomodingMode Nation { get; private set; }

    /// <summary>
    /// Gets or sets the <c>RepeatShifts</c> value.
    /// </summary>
    public bool RepeatShifts { get; private set; }

    /// <summary>
    /// Gets or sets the <c>Autobauding</c> value.
    /// </summary>
    public bool Autobauding { get; private set; }

    /// <summary>Gets whether national automoding was configured at initialization.</summary>
    public bool AutomodingConfigured => _automodingConfigured;

    /// <summary>
    /// Gets whether the endpoint operates as an automode monitor. Monitor mode
    /// is selected by combining <see cref="V18Mode.None"/> with a national
    /// automoding sequence.
    /// </summary>
    public bool MonitorMode { get; private set; }

    /// <summary>
    /// Gets or sets whether a negotiated carrier loss returns to automoding
    /// after the V.18 ten-second no-signal interval.
    /// </summary>
    public bool ResumeAutomodingOnCarrierLoss { get; set; } = true;

    internal V18ControlState ControlState { get; private set; } = V18ControlState.Released;

    /// <summary>
    /// Gets or sets the <c>StoredMessage</c> value.
    /// </summary>
    public string StoredMessage { get; private set; } = "V.18 pls";

    /// <summary>
    /// Gets or sets the <c>CurrentMode</c> value.
    /// </summary>
    public V18Mode CurrentMode { get; private set; } = V18Mode.None;

    /// <summary>
    /// Gets or sets the <c>ReceiveSuppressionTimer</c> value.
    /// </summary>
    public int ReceiveSuppressionTimer { get; private set; }

    /// <summary>
    /// Gets or sets the <c>TransmitSuppressionTimer</c> value.
    /// </summary>
    public int TransmitSuppressionTimer { get; private set; }

    /// <summary>
    /// Gets or sets the <c>MessageInProgressTimer</c> value.
    /// </summary>
    public int MessageInProgressTimer { get; private set; }

    /// <summary>
    /// Gets or sets the <c>TaInterval</c> value.
    /// </summary>
    public int TaInterval { get; private set; }

    /// <summary>
    /// Gets or sets the <c>TcInterval</c> value.
    /// </summary>
    public int TcInterval { get; private set; }

    /// <summary>
    /// Gets or sets the <c>TeInterval</c> value.
    /// </summary>
    public int TeInterval { get; private set; }

    /// <summary>
    /// Gets or sets the <c>TmInterval</c> value.
    /// </summary>
    public int TmInterval { get; private set; }

    /// <summary>
    /// Gets or sets the <c>TrInterval</c> value.
    /// </summary>
    public int TrInterval { get; private set; }

    /// <summary>
    /// Gets or sets the <c>TtInterval</c> value.
    /// </summary>
    public int TtInterval { get; private set; }

    /// <summary>
    /// Gets or sets the <c>TaTimer</c> value.
    /// </summary>
    public int TaTimer { get; private set; }

    /// <summary>
    /// Gets or sets the <c>TcTimer</c> value.
    /// </summary>
    public int TcTimer { get; private set; }

    /// <summary>
    /// Gets or sets the <c>TeTimer</c> value.
    /// </summary>
    public int TeTimer { get; private set; }

    /// <summary>
    /// Gets or sets the <c>TmTimer</c> value.
    /// </summary>
    public int TmTimer { get; private set; }

    /// <summary>
    /// Gets or sets the <c>TrTimer</c> value.
    /// </summary>
    public int TrTimer { get; private set; }

    /// <summary>
    /// Gets or sets the <c>TtTimer</c> value.
    /// </summary>
    public int TtTimer { get; private set; }

    /// <summary>
    /// Gets or sets the <c>Logging</c> value.
    /// </summary>
    public SpanLogState Logging { get; private set; }

    /// <summary>
    /// Gets the <c>IsDisposed</c> value.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Performs the <c>Initialize</c> operation.
    /// </summary>
    /// <param name="callingParty">The calling party value.</param>
    /// <param name="mode">The mode value.</param>
    /// <param name="nation">The nation value.</param>
    /// <param name="putMessage">The put message value.</param>
    /// <param name="putMessageUserData">The put message user data value.</param>
    /// <param name="statusHandler">The status handler value.</param>
    /// <param name="statusHandlerUserData">The status handler user data value.</param>
    public void Initialize(
        bool callingParty,
        V18Mode mode,
        V18AutomodingMode nation,
        V18PutMessageHandler? putMessage,
        object? putMessageUserData,
        V18StatusHandler? statusHandler,
        object? statusHandlerUserData) {
        if (nation < V18AutomodingMode.Global || nation >= V18AutomodingMode.End)
            throw new ArgumentOutOfRangeException(nameof(nation));

        DisposeModemObjects();

        if (_queue.IsDisposed) {
            _queue = new QueueState(
                QueueCapacity,
                QueueFlags.ReadAtomic | QueueFlags.WriteAtomic);
        } else {
            _queue.Initialize(
                QueueCapacity,
                QueueFlags.ReadAtomic | QueueFlags.WriteAtomic);
        }

        CallingParty = callingParty;
        _modemCallingParty = callingParty;
        _answerAnsFollowupActive = false;
        InitialMode = mode & ~V18Mode.RepetitiveShiftsOption;
        Nation = nation;
        RepeatShifts = (mode & V18Mode.RepetitiveShiftsOption) != 0;
        _automodingConfigured = nation != V18AutomodingMode.None;
        MonitorMode = _automodingConfigured && InitialMode == V18Mode.None;
        Autobauding = _automodingConfigured;
        ResumeAutomodingOnCarrierLoss = true;
        ControlState = V18ControlState.Initializing;
        _putMessage = putMessage;
        _putMessageUserData = putMessageUserData;
        _statusHandler = statusHandler;
        _statusHandlerUserData = statusHandlerUserData;

        StoredMessage = "V.18 pls";
        _baudotTransmitShift = 0;
        _baudotReceiveShift = 0;
        _transmitSignalOn = 0;
        _transmitDraining = false;
        _nextByte = 0xFF;
        _receivedDtmf.Clear();
        _baudotCharactersSinceShift = 0;
        _t140TransmitEncoder.Reset();
        ResetT140ReceiveDataPath();
        ReceiveSuppressionTimer = 0;
        TransmitSuppressionTimer = 0;
        MessageInProgressTimer = 0;

        TaInterval = SecondsToSamples(3);
        TcInterval = SecondsToSamples(6);
        TeInterval = MillisecondsToSamples(2700);
        TmInterval = SecondsToSamples(3);
        TrInterval = SecondsToSamples(2);
        TtInterval = SecondsToSamples(3);
        TaTimer = TcTimer = TeTimer = TmTimer = TrTimer = TtTimer = 0;

        _toneBank.Reset();
        _toneDuration = 0;
        _targetToneDuration = 0;
        _inTone = V18ProbeTone.None;
        _answerTonePresent = false;
        _pendingAnswerTone = ModemConnectTone.None;
        _carrierLossTimer = 0;
        _connectedCarrierSeen = false;
        DisposeV8();

        _dtmfTransmitter.Initialize(null, null);
        Dtmf.dtmf_tx_set_timing(
            _dtmfTransmitter,
            DtmfOnMilliseconds,
            DtmfOffMilliseconds);

        _answerToneReceiver = ModemConnectTones.ReceiveInit(
            ModemConnectTone.AnsamWithPhaseReversals,
            AnswerToneReceived,
            this);

        if (_automodingConfigured) {
            EnterAutomoding(returning: false);
        } else {
            SetModem(InitialMode);
            CurrentMode = InitialMode;
            Autobauding = false;
            ControlState = V18ControlState.Connected;
            TransmitState = callingParty
                ? V18TransmitState.OriginatingConnected
                : V18TransmitState.AnsweringConnected;
            ReceiveState = callingParty
                ? V18ReceiveState.OriginatingConnected
                : V18ReceiveState.AnsweringConnected;
            InitializeReceivePath();
            InitializeTransmitPath();
        }

        Logging.Dispose();
        Logging = new SpanLogState((int)SpanLogSeverity.None, null);
        Logging.SetProtocol("V.18");
        _disposed = false;
    }

    internal V18TransmitState TransmitState { get; private set; }

    internal V18ReceiveState ReceiveState { get; private set; }

    /// <summary>
    /// Performs the <c>SetStoredMessage</c> operation.
    /// </summary>
    /// <param name="message">The message value.</param>
    /// <returns>The result of the operation.</returns>
    public int SetStoredMessage(string message) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(message);

        byte[] bytes = T140Encoder.EncodeBounded(message, MaximumStoredMessageBytes);
        StoredMessage = Encoding.UTF8.GetString(bytes);
        return 0;
    }

    /// <summary>
    /// Performs the <c>GetCurrentMode</c> operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public int GetCurrentMode() => (int)CurrentMode;

    /// <summary>
    /// Performs the <c>GetAutomodingSequence</c> operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public V18Mode[] GetAutomodingSequence() {
        ThrowIfDisposed();

        int index = (int)Nation;
        var result = new V18Mode[6];
        for (int i = 0; i < result.Length; i++)
            result[i] = AutomodingSequences[index, i];
        return result;
    }

    /// <summary>
    /// Performs the <c>Release</c> operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public int Release() {
        if (_disposed)
            return 0;

        _queue.Release();
        DisposeModemObjects();
        Autobauding = false;
        ControlState = V18ControlState.Released;
        return 0;
    }

    /// <summary>
    /// Performs the <c>Dispose</c> operation.
    /// </summary>
    public void Dispose() {
        if (_disposed)
            return;

        Release();
        _queue.Dispose();
        Logging.Dispose();
        _putMessage = null;
        _putMessageUserData = null;
        _statusHandler = null;
        _statusHandlerUserData = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void SetModem(V18Mode mode) {
        DisposeFskAndAsync();
        _t140TransmitEncoder.Reset();
        ResetT140ReceiveDataPath();
        _transmitSignalOn = 0;
        _transmitDraining = false;
        _dtmfTransmitter.Initialize(null, null);
        Dtmf.dtmf_tx_set_timing(
            _dtmfTransmitter,
            DtmfOnMilliseconds,
            DtmfOffMilliseconds);

        V18Mode baseMode = mode & ~V18Mode.RepetitiveShiftsOption;
        if (baseMode != V18Mode.None)
            DisposeAutomodingReceivePath();

        switch (baseMode) {
            case V18Mode.None:
                break;

            case V18Mode.Weitbrecht5Bit4545:
                InitializeFsk(
                    FskPreset.Weitbrecht4545,
                    FskPreset.Weitbrecht4545,
                    5,
                    AsyncParity.None,
                    2,
                    TddGetAsyncByte,
                    TddPutAsyncByte);
                _baudotTransmitShift = 2;
                _baudotReceiveShift = 0;
                _nextByte = 0xFF;
                break;

            case V18Mode.Weitbrecht5Bit476:
                InitializeFsk(
                    FskPreset.Weitbrecht476,
                    FskPreset.Weitbrecht476,
                    5,
                    AsyncParity.None,
                    2,
                    TddGetAsyncByte,
                    TddPutAsyncByte);
                _baudotTransmitShift = 2;
                _baudotReceiveShift = 0;
                _nextByte = 0xFF;
                break;

            case V18Mode.Weitbrecht5Bit50:
                InitializeFsk(
                    FskPreset.Weitbrecht50,
                    FskPreset.Weitbrecht50,
                    5,
                    AsyncParity.None,
                    2,
                    TddGetAsyncByte,
                    TddPutAsyncByte);
                _baudotTransmitShift = 2;
                _baudotReceiveShift = 0;
                _nextByte = 0xFF;
                break;

            case V18Mode.Dtmf:
                _dtmfTransmitter.Initialize(null, null);
                Dtmf.dtmf_tx_set_timing(
                    _dtmfTransmitter,
                    DtmfOnMilliseconds,
                    DtmfOffMilliseconds);
                InitializeDtmfReceiver();
                break;

            case V18Mode.Edt:
                InitializeFsk(
                    FskPreset.V21Channel1At110Bps,
                    FskPreset.V21Channel1At110Bps,
                    7,
                    AsyncParity.Even,
                    2,
                    EdtGetAsyncByte,
                    EdtPutAsyncByte);
                break;

            case V18Mode.Bell103:
                InitializeFsk(
                    _modemCallingParty ? FskPreset.Bell103Channel1 : FskPreset.Bell103Channel2,
                    _modemCallingParty ? FskPreset.Bell103Channel2 : FskPreset.Bell103Channel1,
                    7,
                    AsyncParity.Even,
                    1,
                    Bell103GetAsyncByte,
                    Bell103PutAsyncByte,
                    ignoreReceiveParity: true);
                Logging.Log((int)SpanLogSeverity.Flow, "Turning on the carrier\n");
                _transmitSignalOn = 1;
                break;

            case V18Mode.V23Videotex:
                InitializeFsk(
                    _modemCallingParty ? FskPreset.V23Channel2 : FskPreset.V23Channel1,
                    _modemCallingParty ? FskPreset.V23Channel1 : FskPreset.V23Channel2,
                    7,
                    AsyncParity.Even,
                    1,
                    VideotexGetAsyncByte,
                    VideotexPutAsyncByte,
                    ignoreReceiveParity: true);
                Logging.Log((int)SpanLogSeverity.Flow, "Turning on the carrier\n");
                _transmitSignalOn = 1;
                break;

            case V18Mode.V21Textphone:
                InitializeFsk(
                    _modemCallingParty ? FskPreset.V21Channel1 : FskPreset.V21Channel2,
                    _modemCallingParty ? FskPreset.V21Channel2 : FskPreset.V21Channel1,
                    7,
                    AsyncParity.Even,
                    1,
                    TextphoneGetAsyncByte,
                    TextphonePutAsyncByte,
                    ignoreReceiveParity: true);
                Logging.Log((int)SpanLogSeverity.Flow, "Turning on the carrier\n");
                _transmitSignalOn = 1;
                break;

            case V18Mode.V18Textphone:
                InitializeFsk(
                    _modemCallingParty ? FskPreset.V21Channel1 : FskPreset.V21Channel2,
                    _modemCallingParty ? FskPreset.V21Channel2 : FskPreset.V21Channel1,
                    8,
                    AsyncParity.None,
                    1,
                    TextphoneGetAsyncByte,
                    TextphonePutAsyncByte);
                Logging.Log((int)SpanLogSeverity.Flow, "Turning on the carrier\n");
                _transmitSignalOn = 1;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported V.18 mode.");
        }

        CurrentMode = baseMode;

        if (!Autobauding || baseMode != V18Mode.None) {
            TransmitState = CallingParty
                ? V18TransmitState.OriginatingConnected
                : V18TransmitState.AnsweringConnected;
            ReceiveState = CallingParty
                ? V18ReceiveState.OriginatingConnected
                : V18ReceiveState.AnsweringConnected;
        }
    }

    private void InitializeFsk(
        FskPreset transmitPreset,
        FskPreset receivePreset,
        int dataBits,
        AsyncParity parity,
        int stopBits,
        SpanGetByteDelegate byteProvider,
        SpanPutByteDelegate byteConsumer,
        bool ignoreReceiveParity = false) {
        _asyncTransmitter = new AsyncTransmitter(
            dataBits,
            parity,
            stopBits,
            false,
            byteProvider,
            this);

        _fskTransmitter = Fsk.InitializeTransmitter(
            null,
            Fsk.GetPreset(transmitPreset),
            static userData => ((V18State)userData!)._asyncTransmitter!.GetBit(),
            this);

        ConfigureReceiveBitDecoder(dataBits, parity, byteConsumer, ignoreReceiveParity);
        _activeReceiveSpec = Fsk.GetPreset(receivePreset);
        _fskReceiver = Fsk.InitializeReceiver(
            null,
            _activeReceiveSpec,
            FskFrameMode.Asynchronous,
            static (userData, value) =>
                ((V18State)userData!).ReceiveFskBit(value),
            this);
    }

    private void SwitchMode(
        V18Mode mode,
        V18Status status,
        bool? modemCallingParty = null) {
        V18Mode baseMode = mode & ~V18Mode.RepetitiveShiftsOption;
        if (baseMode == V18Mode.None) {
            EnterAutomoding(returning: true);
            return;
        }

        DisposeV8();
        ResetControlTimers();
        Autobauding = false;
        _modemCallingParty = modemCallingParty ?? CallingParty;
        _answerAnsFollowupActive = false;
        _statusHandler?.Invoke(_statusHandlerUserData, (int)status);
        SetModem(baseMode);
        ControlState = V18ControlState.Connected;
        _connectedCarrierSeen = false;

        Logging.Log(
            (int)SpanLogSeverity.Flow,
            "V.18 mode selected: %s\n",
            V18.ModeToString((int)baseMode));
    }

    private void EnterAutomoding(bool returning) {
        if (!_automodingConfigured)
            return;

        DisposeV8();
        ResetControlTimers();
        ControlState = returning
            ? V18ControlState.ReturningToAutomoding
            : V18ControlState.Initializing;
        Autobauding = true;
        _modemCallingParty = CallingParty;
        _answerAnsFollowupActive = false;
        SetModem(V18Mode.None);
        CurrentMode = V18Mode.None;
        _connectedCarrierSeen = false;
        ResetPendingReceiveEvents();

        if (MonitorMode) {
            ControlState = V18ControlState.Monitoring;
            TransmitState = V18TransmitState.Answering1;
            ReceiveState = V18ReceiveState.Answering1;
        } else if (CallingParty) {
            ControlState = V18ControlState.Calling;
            TransmitState = V18TransmitState.Originating1;
            ReceiveState = V18ReceiveState.Originating1;
        } else {
            ControlState = V18ControlState.Answering;
            TransmitState = V18TransmitState.Answering1;
            ReceiveState = V18ReceiveState.Answering1;
        }

        InitializeReceivePath();
        InitializeTransmitPath();

        if (CallingParty && !MonitorMode)
            StartV8Negotiation(callingParty: true, transmitActive: false);

        if (returning) {
            _statusHandler?.Invoke(
                _statusHandlerUserData,
                (int)V18Status.SwitchToNone);
            Logging.Log(
                (int)SpanLogSeverity.Flow,
                "Returning to V.18 automoding\n");
        }
    }

    private bool CanAcceptV23ReverseCarrier(bool caller) =>
        (caller && _txStage == V18TxStage.CallingXci) ||
        (!caller &&
         _txStage == V18TxStage.ProbeCarrier &&
         _probeMode == V18Mode.V23Videotex);

    private bool IsLocalAutomodingFskTransmission(FskPreset preset) =>
        _txStage switch {
            V18TxStage.CallingCi or V18TxStage.CallingTxp =>
                preset == FskPreset.V21Channel1,
            V18TxStage.CallingXci =>
                preset == FskPreset.V23Channel1,
            V18TxStage.AnswerTxp =>
                preset == FskPreset.V21Channel2,
            V18TxStage.ProbeFskMessage => _probeMode switch {
                V18Mode.Weitbrecht5Bit4545 => preset == FskPreset.Weitbrecht4545,
                V18Mode.Weitbrecht5Bit476 => preset == FskPreset.Weitbrecht476,
                V18Mode.Weitbrecht5Bit50 => preset == FskPreset.Weitbrecht50,
                V18Mode.Edt => preset == FskPreset.V21Channel1At110Bps,
                _ => false
            },
            _ => false
        };

    private bool IsLocalAutomodingToneTransmission(V18ProbeTone tone) =>
        _txStage switch {
            V18TxStage.CallingCi or V18TxStage.CallingTxp =>
                tone is V18ProbeTone.Hz980 or V18ProbeTone.Hz1180,
            V18TxStage.CallingXci => tone == V18ProbeTone.Hz1300,
            V18TxStage.AnswerTxp =>
                tone is V18ProbeTone.Hz1650 or V18ProbeTone.Hz1800,
            V18TxStage.ProbeCarrier => _probeMode switch {
                V18Mode.V23Videotex => tone == V18ProbeTone.Hz1300,
                V18Mode.V21Textphone or V18Mode.V18Textphone =>
                    tone == V18ProbeTone.Hz1650,
                V18Mode.Bell103 => tone == V18ProbeTone.Hz2225,
                _ => false
            },
            V18TxStage.ProbeFskMessage => _probeMode switch {
                V18Mode.Weitbrecht5Bit4545 or
                V18Mode.Weitbrecht5Bit476 or
                V18Mode.Weitbrecht5Bit50 =>
                    tone is V18ProbeTone.Hz1400 or V18ProbeTone.Hz1800,
                V18Mode.Edt =>
                    tone is V18ProbeTone.Hz980 or V18ProbeTone.Hz1180,
                _ => false
            },
            _ => false
        };

    private bool IsLocalAutomodingConnectToneTransmission() =>
        _txStage is V18TxStage.AnswerAnsam or V18TxStage.ProbeAnswerTone;

    private void HandleAnsDetected(ModemConnectTone tone) {
        if (!Autobauding || MonitorMode)
            return;

        if (!CallingParty) {
            if (tone is ModemConnectTone.Ansam or
                        ModemConnectTone.AnsamWithPhaseReversals) {
                _answerAnsFollowupActive = false;
                DisposeAutomodingTransmitters();
                StartV8Negotiation(callingParty: true, transmitActive: true);
                return;
            }

            _answerAnsFollowupActive = true;
            PauseAutomodingTransmitForToneTimer();
            Logging.Log(
                (int)SpanLogSeverity.Flow,
                "ANS detected in answer automoding; monitoring follow-up tones\n");
            return;
        }

        TransmitState = V18TransmitState.Originating2;
        _stopCallingTxpAtBoundary = false;

        if (tone is ModemConnectTone.Ansam or
                    ModemConnectTone.AnsamWithPhaseReversals) {
            DisposeAutomodingTransmitters();
            if (!_v8Active || _v8State is null || !_v8State.CallingParty)
                StartV8Negotiation(callingParty: true, transmitActive: true);
            else {
                _v8TransmitActive = true;
                ControlState = V18ControlState.V8Calling;
            }
            return;
        }

        StartSilence(V18TxStage.CallingAnsSilence, AnsToTxpSilenceMilliseconds);
    }

    private void HandleAnsLost() {
        if (!CallingParty)
            return;

        if (_v8Active && ControlState == V18ControlState.V8Calling)
            return;

        if (_txStage is V18TxStage.CallingAnsSilence or V18TxStage.CallingTxp)
            _stopCallingTxpAtBoundary = true;
    }

    private void HandleCiOrXciDetected() {
        if (MonitorMode) {
            Logging.Log(
                (int)SpanLogSeverity.Flow,
                "CI/XCI detected in monitor mode\n");
            return;
        }

        if (CallingParty || !Autobauding ||
            _txStage is V18TxStage.AnswerAnsam or
                        V18TxStage.AnswerTxpSilence or
                        V18TxStage.AnswerTxp) {
            return;
        }

        _answerAnsFollowupActive = false;
        TransmitState = V18TransmitState.Answering2;
        StartConnectTone(
            V18TxStage.AnswerAnsam,
            ModemConnectTone.AnsamWithPhaseReversals,
            AnswerAnsamMilliseconds);
        TtTimer = TtInterval;
        StartV8Negotiation(callingParty: false, transmitActive: false);
    }

    private void HandleTxpDetected() {
        if (!Autobauding)
            return;

        if (MonitorMode) {
            Logging.Log(
                (int)SpanLogSeverity.Flow,
                "TXP detected in monitor mode\n");
            return;
        }

        if (CallingParty) {
            DisposeAutomodingTransmitters();
            SwitchMode(V18Mode.V18Textphone, V18Status.SwitchToV18Textphone);
            _txStage = V18TxStage.Connected;
            return;
        }

        if (_txStage == V18TxStage.AnswerAnsam) {
            DisposeV8();
            DisposeConnectTone();
            StartSilence(V18TxStage.AnswerTxpSilence, AnswerTxpSilenceMilliseconds);
        }
    }

    private void StartV8Negotiation(
        bool callingParty,
        bool transmitActive = true) {
        DisposeV8();

        var parameters = new V8Parameters {
            GatewayMode = !callingParty,
            ModemConnectTone = callingParty
                ? ModemConnectTone.None
                : ModemConnectTone.AnsamWithPhaseReversals,
            SendCi = false,
            V92 = -1
        };
        parameters.JmCm.CallFunction = V8CallFunction.V18;
        parameters.JmCm.Modulations = V8Modulation.V21;
        parameters.JmCm.Protocols = V8Protocol.None;
        parameters.JmCm.PcmModemAvailability = V8PcmModemAvailability.None;
        parameters.JmCm.PstnAccess = V8PstnAccess.None;
        parameters.JmCm.Nsf = -1;
        parameters.JmCm.T66 = -1;

        _v8State = global::TKFaxEngine.Modem.V8.V8.Init(
            null,
            callingParty,
            parameters,
            V8Result,
            this);
        _v8State.LogHandler = message =>
            Logging.Log((int)SpanLogSeverity.Flow, "V.8: %s\n", message);
        _v8Active = true;
        _v8TransmitActive = transmitActive;
        if (transmitActive) {
            ControlState = callingParty
                ? V18ControlState.V8Calling
                : V18ControlState.V8Answering;
        }
    }

    private static void V8Result(object? userData, V8Parameters result) {
        ((V18State)userData!)._pendingV8Result = result.Clone();
    }

    private void HandleV8Result(V8Parameters result) {
        if (!_v8Active || _v8State is null)
            return;

        switch (result.Status) {
            case V8Status.V8Offered:
                if (result.JmCm.CallFunction != V8CallFunction.V18 ||
                    (result.JmCm.Modulations & V8Modulation.V21) == 0) {
                    FallbackFromV8();
                    return;
                }

                var accepted = result.JmCm.Clone();
                accepted.CallFunction = V8CallFunction.V18;
                accepted.Modulations = V8Modulation.V21;
                accepted.Protocols = V8Protocol.None;
                global::TKFaxEngine.Modem.V8.V8.Continue(_v8State, accepted);
                global::TKFaxEngine.Modem.V8.V8.Receive(_v8State, ReadOnlySpan<short>.Empty);
                DisposeAutomodingTransmitters();
                _v8TransmitActive = true;
                break;

            case V8Status.V8Call:
                if (result.JmCm.CallFunction == V8CallFunction.V18 &&
                    (result.JmCm.Modulations & V8Modulation.V21) != 0) {
                    SwitchMode(
                        V18Mode.V18Textphone,
                        V18Status.SwitchToV18Textphone,
                        modemCallingParty: _v8State.CallingParty);
                    _txStage = V18TxStage.Connected;
                } else {
                    FallbackFromV8();
                }
                break;

            case V8Status.NonV8Call:
            case V8Status.Failed:
                FallbackFromV8();
                break;
        }
    }

    private void FallbackFromV8() {
        bool v8CallingParty = _v8State?.CallingParty ?? CallingParty;
        bool callerTransmitWasActive =
            ControlState == V18ControlState.V8Calling;
        DisposeV8();
        if (!Autobauding)
            return;

        if (v8CallingParty) {
            ControlState = V18ControlState.Calling;
            if (callerTransmitWasActive) {
                StartSilence(
                    V18TxStage.CallingAnsSilence,
                    AnsToTxpSilenceMilliseconds);
            }
            return;
        }

        ControlState = V18ControlState.Answering;
        TransmitState = V18TransmitState.Answering2;
        StartConnectTone(
            V18TxStage.AnswerAnsam,
            ModemConnectTone.AnsamWithPhaseReversals,
            AnswerAnsamMilliseconds);
        TtTimer = TtInterval;
    }

    private int TransmitV8(Span<short> samples) {
        if (!_v8Active || !_v8TransmitActive || _v8State is null)
            return -1;
        return global::TKFaxEngine.Modem.V8.V8.Transmit(_v8State, samples);
    }

    private void ProcessV8Receive(ReadOnlySpan<short> samples) {
        if (!_v8Active || _v8State is null)
            return;

        global::TKFaxEngine.Modem.V8.V8.Receive(_v8State, samples);
        V8Parameters? result = _pendingV8Result;
        _pendingV8Result = null;
        if (result is not null)
            HandleV8Result(result);
    }

    private void HandleConnectedCarrierStatus(int bitOrStatus) {
        if (bitOrStatus == (int)SignalStatus.CarrierUp) {
            _connectedCarrierSeen = true;
            _carrierLossTimer = 0;
            return;
        }

        if (bitOrStatus != (int)SignalStatus.CarrierDown ||
            !_connectedCarrierSeen ||
            !_automodingConfigured ||
            !ResumeAutomodingOnCarrierLoss ||
            Autobauding) {
            return;
        }

        _carrierLossTimer = MillisecondsToSamples(AutomodingResumeMilliseconds);
        Logging.Log(
            (int)SpanLogSeverity.Flow,
            "Carrier lost; automoding resumes after 10 seconds without carrier\n");
    }

    private void BeginToneTimer(V18ProbeTone tone, bool caller) {
        if (MonitorMode || TeTimer > 0 || TrTimer > 0)
            return;

        PauseAutomodingTransmitForToneTimer();
        if (caller) {
            TrTimer = TrInterval;
            TeTimer = 0;
            Logging.Log(
                (int)SpanLogSeverity.Flow,
                "Timer Tr started for %s\n",
                ToneToString(tone));
        } else {
            TeTimer = TeInterval;
            TrTimer = 0;
            Logging.Log(
                (int)SpanLogSeverity.Flow,
                "Timer Te started for %s\n",
                ToneToString(tone));
        }
    }

    private void HandleProbeToneProgress(
        V18ProbeTone tone,
        bool caller,
        int duration) {
        if (MonitorMode)
            return;

        int timerStart = MillisecondsToSamples(200);
        if ((tone is V18ProbeTone.Hz980 or V18ProbeTone.Hz1180) &&
            duration >= timerStart &&
            TeTimer == 0 &&
            TrTimer == 0) {
            BeginToneTimer(tone, caller);
        }
    }

    private bool ProbeToneDecisionDeferred(V18ProbeTone tone) {
        if (TeTimer <= 0 && TrTimer <= 0)
            return false;

        return tone is not V18ProbeTone.Hz980 and
                           not V18ProbeTone.Hz1180;
    }

    private void HandleProbeToneEnded(
        V18ProbeTone tone,
        bool caller,
        int duration) {
        int minimum = MillisecondsToSamples(500);
        int maximum = MillisecondsToSamples(700);
        bool validCallingToneBurst = duration >= minimum && duration <= maximum;

        if (MonitorMode) {
            if (validCallingToneBurst &&
                (tone is V18ProbeTone.Hz980 or V18ProbeTone.Hz1300)) {
                Logging.Log(
                    (int)SpanLogSeverity.Flow,
                    "Monitor reported %s calling-tone burst\n",
                    ToneToString(tone));
            }
            return;
        }

        if (!caller && validCallingToneBurst &&
            (tone is V18ProbeTone.Hz980 or V18ProbeTone.Hz1300)) {
            TeTimer = 0;
            TrTimer = 0;
            StartAnswerProbing();
        }
    }

    private void StartAnswerProbing() {
        if (!Autobauding || CallingParty || MonitorMode)
            return;

        _answerAnsFollowupActive = false;
        ResetTransmitTimers();
        ControlState = V18ControlState.Answering;
        TransmitState = V18TransmitState.Answering3;
        StartNextProbe();
    }

    private void AdvanceControlTimers(int samples) {
        if (samples <= 0)
            return;

        if (_carrierLossTimer > 0) {
            _carrierLossTimer = Math.Max(0, _carrierLossTimer - samples);
            if (_carrierLossTimer == 0 &&
                _automodingConfigured &&
                ResumeAutomodingOnCarrierLoss &&
                !Autobauding) {
                EnterAutomoding(returning: true);
                return;
            }
        }

        if (TrTimer > 0) {
            TrTimer = Math.Max(0, TrTimer - samples);
            if (TrTimer == 0 && Autobauding)
                RestartAutomodingMonitor();
        }

        if (TeTimer > 0) {
            TeTimer = Math.Max(0, TeTimer - samples);
            if (TeTimer == 0 && Autobauding)
                RestartAutomodingMonitor();
        }
    }

    private void RestartAutomodingMonitor() {
        if (!_automodingConfigured || MonitorMode)
            return;

        _modemCallingParty = CallingParty;
        _answerAnsFollowupActive = false;
        ResetTonePersistence();
        ResetPendingReceiveEvents();
        ControlState = CallingParty
            ? V18ControlState.Calling
            : V18ControlState.Answering;
        TransmitState = CallingParty
            ? V18TransmitState.Originating1
            : V18TransmitState.Answering1;
        ReceiveState = CallingParty
            ? V18ReceiveState.Originating1
            : V18ReceiveState.Answering1;
        InitializeReceivePath();
        InitializeTransmitPath();
        if (CallingParty)
            StartV8Negotiation(callingParty: true, transmitActive: false);
    }

    private void ResetControlTimers() {
        _carrierLossTimer = 0;
        TaTimer = 0;
        TcTimer = 0;
        TeTimer = 0;
        TmTimer = 0;
        TrTimer = 0;
        TtTimer = 0;
    }

    private void DisposeV8() {
        _v8State?.Dispose();
        _v8State = null;
        _pendingV8Result = null;
        _v8Active = false;
        _v8TransmitActive = false;
    }


    private void DisposeFskAndAsync() {
        _fskTransmitter?.Dispose();
        _fskTransmitter = null;

        _fskReceiver?.Dispose();
        _fskReceiver = null;
        _activeReceiveSpec = null;

        _asyncTransmitter?.Dispose();
        _asyncTransmitter = null;

        ClearReceiveBitDecoder();
        _activeFskCarrierPresent = false;
    }

    private void DisposeModemObjects() {
        DisposeV8();
        DisposeAutomodingTransmitters();
        DisposeAutomodingReceivePath();
        DisposeFskAndAsync();

        _answerToneReceiver?.Dispose();
        _answerToneReceiver = null;
        _answerTonePresent = false;
    }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static FskParity ToFskParity(AsyncParity parity) {
        return parity switch {
            AsyncParity.None => FskParity.None,
            AsyncParity.Even => FskParity.Even,
            AsyncParity.Odd => FskParity.Odd,
            AsyncParity.Mark => FskParity.Mark,
            AsyncParity.Space => FskParity.Space,
            _ => throw new ArgumentOutOfRangeException(nameof(parity))
        };
    }

    private static int MillisecondsToSamples(int milliseconds) =>
        checked(milliseconds * SampleRate / 1000);

    private static int SecondsToSamples(int seconds) =>
        checked(seconds * SampleRate);

    private static string ToneToString(V18ProbeTone tone) {
        return tone switch {
            V18ProbeTone.Hz390 => "390Hz tone",
            V18ProbeTone.Hz980 => "980Hz tone",
            V18ProbeTone.Hz1180 => "1180Hz tone",
            V18ProbeTone.Hz1270 => "1270Hz tone",
            V18ProbeTone.Hz1300 => "1300Hz tone",
            V18ProbeTone.Hz1400 => "1400Hz tone",
            V18ProbeTone.Hz1650 => "1650Hz tone",
            V18ProbeTone.Hz1800 => "1800Hz tone",
            V18ProbeTone.Hz2225 => "2225Hz tone",
            _ => "???"
        };
    }

    private static string SignalStatusToString(int status) {
        return status switch {
            (int)SignalStatus.CarrierDown => "Carrier down",
            (int)SignalStatus.CarrierUp => "Carrier up",
            (int)SignalStatus.TrainingInProgress => "Training in progress",
            (int)SignalStatus.TrainingSucceeded => "Training succeeded",
            (int)SignalStatus.TrainingFailed => "Training failed",
            (int)SignalStatus.FramingOk => "Framing OK",
            (int)SignalStatus.EndOfData => "End of data",
            (int)SignalStatus.Abort => "Abort",
            (int)SignalStatus.Break => "Break",
            (int)SignalStatus.ShutdownComplete => "Shutdown complete",
            (int)SignalStatus.OctetReport => "Octet report",
            (int)SignalStatus.PoorSignalQuality => "Poor signal quality",
            (int)SignalStatus.ModemRetrainOccurred => "Modem retrain occurred",
            (int)SignalStatus.LinkConnected => "Link connected",
            (int)SignalStatus.LinkDisconnected => "Link disconnected",
            (int)SignalStatus.LinkError => "Link error",
            (int)SignalStatus.LinkIdle => "Link idle",
            _ => "???"
        };
    }
}

/// <summary>
/// Public V.18 helpers corresponding to v18_mode_to_str and v18_status_to_str.
/// </summary>
public static class V18 {
    /// <summary>
    /// Performs the <c>ModeToString</c> operation.
    /// </summary>
    /// <param name="mode">The mode value.</param>
    /// <returns>The result of the operation.</returns>
    public static string ModeToString(int mode) {
        return ((V18Mode)(mode & 0x0FFF)) switch {
            V18Mode.None => "None",
            V18Mode.Weitbrecht5Bit4545 => "Weitbrecht TDD (45.45bps)",
            V18Mode.Weitbrecht5Bit476 => "Weitbrecht TDD (47.6bps)",
            V18Mode.Weitbrecht5Bit50 => "Weitbrecht TDD (50bps)",
            V18Mode.Dtmf => "DTMF",
            V18Mode.Edt => "EDT",
            V18Mode.Bell103 => "Bell 103",
            V18Mode.V23Videotex => "V.23 Videotex",
            V18Mode.V21Textphone => "V.21",
            V18Mode.V18Textphone => "V.18 text telephone",
            _ => "???"
        };
    }

    /// <summary>
    /// Performs the <c>StatusToString</c> operation.
    /// </summary>
    /// <param name="status">The status value.</param>
    /// <returns>The result of the operation.</returns>
    public static string StatusToString(int status) {
        return (V18Status)status switch {
            V18Status.SwitchToNone => "Switched to None mode",
            V18Status.SwitchToWeitbrecht5Bit4545 => "Switched to Weitbrecht TDD (45.45bps) mode",
            V18Status.SwitchToWeitbrecht5Bit476 => "Switched to Weitbrecht TDD (47.6bps) mode",
            V18Status.SwitchToWeitbrecht5Bit50 => "Switched to Weitbrecht TDD (50bps) mode",
            V18Status.SwitchToDtmf => "Switched to DTMF mode",
            V18Status.SwitchToEdt => "Switched to EDT mode",
            V18Status.SwitchToBell103 => "Switched to Bell 103 mode",
            V18Status.SwitchToV23Videotex => "Switched to V.23 Videotex mode",
            V18Status.SwitchToV21Textphone => "Switched to V.21 mode",
            V18Status.SwitchToV18Textphone => "Switched to V.18 text telephone mode",
            _ => "???"
        };
    }
}

/// <summary>
/// C-compatible facade retaining the original function names.
/// </summary>
public static class V18Api {
    /// <summary>
    /// Defines the <c>V18_MODE_NONE</c> constant.
    /// </summary>
    public const int V18_MODE_NONE = (int)V18Mode.None;
    /// <summary>
    /// Defines the <c>V18_MODE_WEITBRECHT_5BIT_4545</c> constant.
    /// </summary>
    public const int V18_MODE_WEITBRECHT_5BIT_4545 = (int)V18Mode.Weitbrecht5Bit4545;
    /// <summary>
    /// Defines the <c>V18_MODE_WEITBRECHT_5BIT_50</c> constant.
    /// </summary>
    public const int V18_MODE_WEITBRECHT_5BIT_50 = (int)V18Mode.Weitbrecht5Bit50;
    /// <summary>
    /// Defines the <c>V18_MODE_DTMF</c> constant.
    /// </summary>
    public const int V18_MODE_DTMF = (int)V18Mode.Dtmf;
    /// <summary>
    /// Defines the <c>V18_MODE_EDT</c> constant.
    /// </summary>
    public const int V18_MODE_EDT = (int)V18Mode.Edt;
    /// <summary>
    /// Defines the <c>V18_MODE_BELL103</c> constant.
    /// </summary>
    public const int V18_MODE_BELL103 = (int)V18Mode.Bell103;
    /// <summary>
    /// Defines the <c>V18_MODE_V23VIDEOTEX</c> constant.
    /// </summary>
    public const int V18_MODE_V23VIDEOTEX = (int)V18Mode.V23Videotex;
    /// <summary>
    /// Defines the <c>V18_MODE_V21TEXTPHONE</c> constant.
    /// </summary>
    public const int V18_MODE_V21TEXTPHONE = (int)V18Mode.V21Textphone;
    /// <summary>
    /// Defines the <c>V18_MODE_V18TEXTPHONE</c> constant.
    /// </summary>
    public const int V18_MODE_V18TEXTPHONE = (int)V18Mode.V18Textphone;
    /// <summary>
    /// Defines the <c>V18_MODE_WEITBRECHT_5BIT_476</c> constant.
    /// </summary>
    public const int V18_MODE_WEITBRECHT_5BIT_476 = (int)V18Mode.Weitbrecht5Bit476;
    /// <summary>
    /// Defines the <c>V18_MODE_REPETITIVE_SHIFTS_OPTION</c> constant.
    /// </summary>
    public const int V18_MODE_REPETITIVE_SHIFTS_OPTION = (int)V18Mode.RepetitiveShiftsOption;

    /// <summary>
    /// Performs the <c>v18_get_logging_state</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static SpanLogState v18_get_logging_state(V18State state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Logging;
    }

    /// <summary>
    /// Performs the <c>v18_init</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="callingParty">The calling party value.</param>
    /// <param name="mode">The mode value.</param>
    /// <param name="nation">The nation value.</param>
    /// <param name="putMessage">The put message value.</param>
    /// <param name="putMessageUserData">The put message user data value.</param>
    /// <param name="statusHandler">The status handler value.</param>
    /// <param name="statusHandlerUserData">The status handler user data value.</param>
    /// <returns>The result of the operation.</returns>
    public static V18State? v18_init(
        V18State? state,
        bool callingParty,
        int mode,
        int nation,
        V18PutMessageHandler? putMessage,
        object? putMessageUserData,
        V18StatusHandler? statusHandler,
        object? statusHandlerUserData) {
        if (nation < 0 || nation >= (int)V18AutomodingMode.End)
            return null;

        state ??= new V18State();
        state.Initialize(
            callingParty,
            (V18Mode)mode,
            (V18AutomodingMode)nation,
            putMessage,
            putMessageUserData,
            statusHandler,
            statusHandlerUserData);

        return state;
    }

    /// <summary>
    /// Performs the <c>v18_release</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int v18_release(V18State state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    /// <summary>
    /// Performs the <c>v18_free</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int v18_free(V18State? state) {
        state?.Dispose();
        return 0;
    }

    /// <summary>
    /// Performs the <c>v18_tx</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="samples">The samples value.</param>
    /// <param name="maximumLength">The maximum length value.</param>
    /// <returns>The result of the operation.</returns>
    public static int v18_tx(
        V18State state,
        short[] samples,
        int maximumLength) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Transmit(samples, maximumLength);
    }

    /// <summary>
    /// Performs the <c>v18_tx</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="samples">The samples value.</param>
    /// <returns>The result of the operation.</returns>
    public static int v18_tx(
        V18State state,
        Span<short> samples) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Transmit(samples);
    }

    /// <summary>
    /// Performs the <c>v18_rx</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="samples">The samples value.</param>
    /// <param name="length">The length value.</param>
    /// <returns>The result of the operation.</returns>
    public static int v18_rx(
        V18State state,
        short[] samples,
        int length) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Receive(samples, length);
    }

    /// <summary>
    /// Performs the <c>v18_rx</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="samples">The samples value.</param>
    /// <returns>The result of the operation.</returns>
    public static int v18_rx(
        V18State state,
        ReadOnlySpan<short> samples) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Receive(samples);
    }

    /// <summary>
    /// Performs the <c>v18_rx_fillin</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="length">The length value.</param>
    /// <returns>The result of the operation.</returns>
    public static int v18_rx_fillin(V18State state, int length) {
        ArgumentNullException.ThrowIfNull(state);
        return state.ReceiveFillIn(length);
    }

    /// <summary>
    /// Performs the <c>v18_put</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="message">The message value.</param>
    /// <param name="length">The length value.</param>
    /// <returns>The result of the operation.</returns>
    public static int v18_put(V18State state, string message, int length) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Put(message, length);
    }

    /// <summary>
    /// Performs the <c>v18_put</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="message">The message value.</param>
    /// <returns>The result of the operation.</returns>
    public static int v18_put(V18State state, ReadOnlySpan<byte> message) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Put(message);
    }

    /// <summary>
    /// Performs the <c>v18_set_stored_message</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="message">The message value.</param>
    /// <returns>The result of the operation.</returns>
    public static int v18_set_stored_message(V18State state, string message) {
        ArgumentNullException.ThrowIfNull(state);
        return state.SetStoredMessage(message);
    }

    /// <summary>
    /// Performs the <c>v18_get_current_mode</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int v18_get_current_mode(V18State state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetCurrentMode();
    }

    /// <summary>
    /// Performs the <c>v18_mode_to_str</c> operation.
    /// </summary>
    /// <param name="mode">The mode value.</param>
    /// <returns>The result of the operation.</returns>
    public static string v18_mode_to_str(int mode) => V18.ModeToString(mode);

    /// <summary>
    /// Performs the <c>v18_status_to_str</c> operation.
    /// </summary>
    /// <param name="status">The status value.</param>
    /// <returns>The result of the operation.</returns>
    public static string v18_status_to_str(int status) => V18.StatusToString(status);
}
