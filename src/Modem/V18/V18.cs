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

namespace TKFaxEngine.Modem.V18;

/// <summary>
/// V.18 text telephone modes. Numeric values match the native V18_MODE_* constants.
/// </summary>
[Flags]
public enum V18Mode {
    None = 0x0001,
    Weitbrecht5Bit4545 = 0x0002,
    Weitbrecht5Bit50 = 0x0004,
    Dtmf = 0x0008,
    Edt = 0x0010,
    Bell103 = 0x0020,
    V23Videotex = 0x0040,
    V21Textphone = 0x0080,
    V18Textphone = 0x0100,
    Weitbrecht5Bit476 = 0x0200,
    RepetitiveShiftsOption = 0x1000
}

/// <summary>
/// National V.18 automoding preference sequence.
/// </summary>
public enum V18AutomodingMode {
    Global = 0,
    None = 1,
    Australia = 2,
    Ireland = 3,
    Germany = 4,
    Switzerland = 5,
    Italy = 6,
    Spain = 7,
    Austria = 8,
    Netherlands = 9,
    Iceland = 10,
    Norway = 11,
    Sweden = 12,
    Finland = 13,

    /// <summary>Compatibility alias for the misspelled native enum name.</summary>
    Finalnd = Finland,

    Denmark = 14,
    UnitedKingdom = 15,
    Usa = 16,
    France = 17,
    Belgium = 18,
    End = 19
}

/// <summary>
/// V.18 mode-change status values.
/// </summary>
public enum V18Status {
    SwitchToNone = 0,
    SwitchToWeitbrecht5Bit4545 = 1,
    SwitchToWeitbrecht5Bit476 = 2,
    SwitchToWeitbrecht5Bit50 = 3,
    SwitchToDtmf = 4,
    SwitchToEdt = 5,
    SwitchToBell103 = 6,
    SwitchToV23Videotex = 7,
    SwitchToV21Textphone = 8,
    SwitchToV18Textphone = 9
}

public delegate void V18PutMessageHandler(
    object? userData,
    ReadOnlySpan<byte> message);

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

/// <summary>
/// Managed equivalent of v18_state_t and the complete implemented behavior
/// from v18.c.
/// </summary>
public sealed class V18State : IDisposable {
    public const int SampleRate = 8000;
    public const int QueueCapacity = 128;
    public const int MaximumStoredMessageBytes = 80;
    public const int GoertzelSamplesPerBlock = 102;

    private const int BaudotFigureShift = 0x1B;
    private const int BaudotLetterShift = 0x1F;
    private const int TddCharacterSuppressionMilliseconds = 480;
    private const int TddDrainSuppressionMilliseconds = 620;

    private static readonly float[] ToneFrequencies =
    {
        390.0f, 980.0f, 1180.0f, 1270.0f, 1300.0f,
        1400.0f, 1650.0f, 1800.0f, 2225.0f
    };

    private static readonly int[] ToneTargetDurations =
    {
        MillisecondsToSamples(3000),
        MillisecondsToSamples(1500),
        0,
        MillisecondsToSamples(700),
        MillisecondsToSamples(1700),
        0,
        MillisecondsToSamples(460),
        0,
        MillisecondsToSamples(460)
    };

    private static readonly bool[,] ToneEnabled =
    {
        { true, true, true, true, false, true, false, true, true },
        { true, true, true, true, true, true, true, true, false }
    };

    private static readonly byte[] BaudotEncodeTable =
    {
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x40, 0x44, 0x42, 0x42, 0x42, 0x48, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x99, 0xFF, 0x42, 0x42, 0x42, 0x44, 0x44, 0x8D, 0x91, 0x89, 0x89, 0x9D, 0x9A, 0x8B, 0x8F, 0x92, 0x9C, 0x9A, 0x8C, 0x83, 0x9C, 0x9D, 0x96, 0x97, 0x93, 0x81, 0x8A, 0x90, 0x95, 0x87, 0x86, 0x98, 0x8E, 0x9E, 0x8F, 0x94, 0x92, 0x99, 0x1D, 0x03, 0x19, 0x0E, 0x09, 0x01, 0x0D, 0x1A, 0x14, 0x06, 0x0B, 0x0F, 0x12, 0x1C, 0x0C, 0x18, 0x16, 0x17, 0x0A, 0x05, 0x10, 0x07, 0x1E, 0x13, 0x1D, 0x15, 0x11, 0x8F, 0x9D, 0x92, 0x8B, 0x44, 0x8B, 0x03, 0x19, 0x0E, 0x09, 0x01, 0x0D, 0x1A, 0x14, 0x06, 0x0B, 0x0F, 0x12, 0x1C, 0x0C, 0x18, 0x16, 0x17, 0x0A, 0x05, 0x10, 0x07, 0x1E, 0x13, 0x1D, 0x15, 0x11, 0x8F, 0x8D, 0x92, 0x44, 0xFF
    };

    private static readonly byte[][] BaudotDecodeTable =
    {
        Encoding.ASCII.GetBytes("\bE\nA SIU\rDRJNFCKTZLWHYPQOBG^MXV^"),
        Encoding.ASCII.GetBytes("\b3\n- -87\r$4',!:(5\")2=6019?+^./;^")
    };

    private static readonly string[] AsciiToDtmf =
    {
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "*0",
        "0",
        "**9",
        "**9",
        "**9",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "#0",
        "",
        "**9",
        "**9",
        "**9",
        "0",
        "0",
        "###0",
        "",
        "",
        "",
        "**5",
        "**1",
        "",
        "**6",
        "**7",
        "#9",
        "**1",
        "**8",
        "**2",
        "#9",
        "",
        "*#0",
        "*#1",
        "*#2",
        "*#3",
        "*#4",
        "*#5",
        "*#6",
        "*#7",
        "*#8",
        "*#9",
        "**4",
        "###9",
        "**6",
        "**3",
        "**7",
        "#0",
        "###8",
        "##*1",
        "##1",
        "###1",
        "##*2",
        "##2",
        "###2",
        "##*3",
        "##3",
        "###3",
        "##*4",
        "##4",
        "###4",
        "##*5",
        "##5",
        "###5",
        "##*6",
        "##6",
        "###6",
        "##*7",
        "##7",
        "###7",
        "##*8",
        "##8",
        "###8",
        "##*9",
        "##9",
        "#*4",
        "#*5",
        "#*6",
        "",
        "0",
        "",
        "*1",
        "1",
        "#1",
        "*2",
        "2",
        "#2",
        "*3",
        "3",
        "#3",
        "*4",
        "4",
        "#4",
        "*5",
        "5",
        "#5",
        "*6",
        "6",
        "#6",
        "*7",
        "7",
        "#7",
        "*8",
        "8",
        "#8",
        "*9",
        "9",
        "#*1",
        "#*2",
        "#*3",
        "0",
        "*0"
    };

    private static readonly Dictionary<string, byte> DtmfToAscii =
        new(StringComparer.Ordinal) {
            ["###0"] = (byte)'!',
            ["###1"] = (byte)'C',
            ["###2"] = (byte)'F',
            ["###3"] = (byte)'I',
            ["###4"] = (byte)'L',
            ["###5"] = (byte)'O',
            ["###6"] = (byte)'R',
            ["###7"] = (byte)'U',
            ["###8"] = (byte)'X',
            ["###9"] = (byte)';',
            ["##*1"] = (byte)'A',
            ["##*2"] = (byte)'D',
            ["##*3"] = (byte)'G',
            ["##*4"] = (byte)'J',
            ["##*5"] = (byte)'M',
            ["##*6"] = (byte)'P',
            ["##*7"] = (byte)'S',
            ["##*8"] = (byte)'V',
            ["##*9"] = (byte)'Y',
            ["##1"] = (byte)'B',
            ["##2"] = (byte)'E',
            ["##3"] = (byte)'H',
            ["##4"] = (byte)'K',
            ["##5"] = (byte)'N',
            ["##6"] = (byte)'Q',
            ["##7"] = (byte)'T',
            ["##8"] = (byte)'W',
            ["##9"] = (byte)'Z',
            ["##0"] = (byte)' ',
            ["#*1"] = (byte)'X',
            ["#*2"] = (byte)'X',
            ["#*3"] = (byte)'X',
            ["#*4"] = (byte)'X',
            ["#*5"] = (byte)'X',
            ["#*6"] = (byte)'X',
            ["#0"] = (byte)'?',
            ["#1"] = (byte)'c',
            ["#2"] = (byte)'f',
            ["#3"] = (byte)'i',
            ["#4"] = (byte)'l',
            ["#5"] = (byte)'o',
            ["#6"] = (byte)'r',
            ["#7"] = (byte)'u',
            ["#8"] = (byte)'x',
            ["#9"] = (byte)'.',
            ["*#0"] = (byte)'0',
            ["*#1"] = (byte)'1',
            ["*#2"] = (byte)'2',
            ["*#3"] = (byte)'3',
            ["*#4"] = (byte)'4',
            ["*#5"] = (byte)'5',
            ["*#6"] = (byte)'6',
            ["*#7"] = (byte)'7',
            ["*#8"] = (byte)'8',
            ["*#9"] = (byte)'9',
            ["**1"] = (byte)'+',
            ["**2"] = (byte)'-',
            ["**3"] = (byte)'=',
            ["**4"] = (byte)':',
            ["**5"] = (byte)'%',
            ["**6"] = (byte)'(',
            ["**7"] = (byte)')',
            ["**8"] = (byte)',',
            ["**9"] = (byte)'\n',
            ["*0"] = (byte)'\b',
            ["*1"] = (byte)'a',
            ["*2"] = (byte)'d',
            ["*3"] = (byte)'g',
            ["*4"] = (byte)'j',
            ["*5"] = (byte)'m',
            ["*6"] = (byte)'p',
            ["*7"] = (byte)'s',
            ["*8"] = (byte)'v',
            ["*9"] = (byte)'y',
            ["0"] = (byte)' ',
            ["1"] = (byte)'b',
            ["2"] = (byte)'e',
            ["3"] = (byte)'h',
            ["4"] = (byte)'k',
            ["5"] = (byte)'n',
            ["6"] = (byte)'q',
            ["7"] = (byte)'t',
            ["8"] = (byte)'w',
            ["9"] = (byte)'z'
        };

    private static readonly V18Mode[,] AutomodingSequences =
    {
        { V18Mode.Weitbrecht5Bit4545, V18Mode.Bell103, V18Mode.V21Textphone, V18Mode.V23Videotex, V18Mode.Edt, V18Mode.Dtmf },
        { V18Mode.Weitbrecht5Bit4545, V18Mode.Bell103, V18Mode.V21Textphone, V18Mode.V23Videotex, V18Mode.Edt, V18Mode.Dtmf },
        { V18Mode.Weitbrecht5Bit50, V18Mode.V21Textphone, V18Mode.V23Videotex, V18Mode.Edt, V18Mode.Dtmf, V18Mode.Bell103 },
        { V18Mode.Weitbrecht5Bit50, V18Mode.V21Textphone, V18Mode.V23Videotex, V18Mode.Edt, V18Mode.Dtmf, V18Mode.Bell103 },
        { V18Mode.Edt, V18Mode.V21Textphone, V18Mode.V23Videotex, V18Mode.Weitbrecht5Bit50, V18Mode.Dtmf, V18Mode.Bell103 },
        { V18Mode.Edt, V18Mode.V21Textphone, V18Mode.V23Videotex, V18Mode.Weitbrecht5Bit50, V18Mode.Dtmf, V18Mode.Bell103 },
        { V18Mode.Edt, V18Mode.V21Textphone, V18Mode.V23Videotex, V18Mode.Weitbrecht5Bit50, V18Mode.Dtmf, V18Mode.Bell103 },
        { V18Mode.Edt, V18Mode.V21Textphone, V18Mode.V23Videotex, V18Mode.Weitbrecht5Bit50, V18Mode.Dtmf, V18Mode.Bell103 },
        { V18Mode.Edt, V18Mode.V21Textphone, V18Mode.V23Videotex, V18Mode.Weitbrecht5Bit50, V18Mode.Dtmf, V18Mode.Bell103 },
        { V18Mode.Dtmf, V18Mode.V21Textphone, V18Mode.V23Videotex, V18Mode.Weitbrecht5Bit50, V18Mode.Edt, V18Mode.Bell103 },
        { V18Mode.V21Textphone, V18Mode.Dtmf, V18Mode.Weitbrecht5Bit50, V18Mode.Edt, V18Mode.V23Videotex, V18Mode.Bell103 },
        { V18Mode.V21Textphone, V18Mode.Dtmf, V18Mode.Weitbrecht5Bit50, V18Mode.Edt, V18Mode.V23Videotex, V18Mode.Bell103 },
        { V18Mode.V21Textphone, V18Mode.Dtmf, V18Mode.Weitbrecht5Bit50, V18Mode.Edt, V18Mode.V23Videotex, V18Mode.Bell103 },
        { V18Mode.V21Textphone, V18Mode.Dtmf, V18Mode.Weitbrecht5Bit50, V18Mode.Edt, V18Mode.V23Videotex, V18Mode.Bell103 },
        { V18Mode.V21Textphone, V18Mode.Dtmf, V18Mode.Weitbrecht5Bit50, V18Mode.Edt, V18Mode.V23Videotex, V18Mode.Bell103 },
        { V18Mode.V21Textphone, V18Mode.Weitbrecht5Bit50, V18Mode.V23Videotex, V18Mode.Edt, V18Mode.Dtmf, V18Mode.Bell103 },
        { V18Mode.Weitbrecht5Bit4545, V18Mode.Bell103, V18Mode.V21Textphone, V18Mode.V23Videotex, V18Mode.Edt, V18Mode.Dtmf },
        { V18Mode.V23Videotex, V18Mode.Edt, V18Mode.Dtmf, V18Mode.Weitbrecht5Bit50, V18Mode.V21Textphone, V18Mode.Bell103 },
        { V18Mode.V23Videotex, V18Mode.Edt, V18Mode.Dtmf, V18Mode.Weitbrecht5Bit50, V18Mode.V21Textphone, V18Mode.Bell103 }
    };

    private QueueState _queue = new(
        QueueCapacity,
        QueueFlags.ReadAtomic | QueueFlags.WriteAtomic);

    private FskTxState? _fskTransmitter;
    private FskRxState? _fskReceiver;
    private AsyncTransmitter? _asyncTransmitter;
    private ModemConnectTonesRxState? _answerToneReceiver;
    private readonly V18DtmfTransmitter _dtmfTransmitter = new();
    private readonly V18DtmfReceiver _dtmfReceiver = new();
    private readonly V18GoertzelBank _toneBank = new(ToneFrequencies, GoertzelSamplesPerBlock);

    private V18PutMessageHandler? _putMessage;
    private object? _putMessageUserData;
    private V18StatusHandler? _statusHandler;
    private object? _statusHandlerUserData;

    private int _baudotTransmitShift;
    private int _baudotReceiveShift;
    private int _transmitSignalOn;
    private bool _transmitDraining;
    private byte _nextByte = 0xFF;
    private readonly byte[] _receiveMessage = new byte[257];
    private int _receiveMessageLength;
    private readonly StringBuilder _receivedDtmf = new(32);
    private int _txpCount;
    private bool _disposed;

    private int _toneDuration;
    private int _targetToneDuration;
    private V18ProbeTone _inTone = V18ProbeTone.None;

    public V18State() {
        Logging = new SpanLogState((int)SpanLogSeverity.None, null);
        Logging.SetProtocol("V.18");
    }

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

    public bool CallingParty { get; private set; }

    public V18Mode InitialMode { get; private set; }

    public V18AutomodingMode Nation { get; private set; }

    public bool RepeatShifts { get; private set; }

    public bool Autobauding { get; private set; }

    public string StoredMessage { get; private set; } = "V.18 pls";

    public V18Mode CurrentMode { get; private set; } = V18Mode.None;

    public int ReceiveSuppressionTimer { get; private set; }

    public int TransmitSuppressionTimer { get; private set; }

    public int MessageInProgressTimer { get; private set; }

    public int TaInterval { get; private set; }

    public int TcInterval { get; private set; }

    public int TeInterval { get; private set; }

    public int TmInterval { get; private set; }

    public int TrInterval { get; private set; }

    public int TtInterval { get; private set; }

    public int TaTimer { get; private set; }

    public int TcTimer { get; private set; }

    public int TeTimer { get; private set; }

    public int TmTimer { get; private set; }

    public int TrTimer { get; private set; }

    public int TtTimer { get; private set; }

    public SpanLogState Logging { get; private set; }

    public bool IsDisposed => _disposed;

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
        InitialMode = mode & ~V18Mode.RepetitiveShiftsOption;
        Nation = nation;
        RepeatShifts = (mode & V18Mode.RepetitiveShiftsOption) != 0;
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
        _receiveMessageLength = 0;
        _receivedDtmf.Clear();
        _txpCount = 0;
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

        _dtmfReceiver.Initialize(DtmfReceived);
        _dtmfTransmitter.Reset();

        _answerToneReceiver = ModemConnectTones.ReceiveInit(
            ModemConnectTone.AnsamWithPhaseReversals,
            AnswerToneReceived,
            this);

        SetModem(InitialMode);

        if (nation == V18AutomodingMode.None) {
            Autobauding = false;
            CurrentMode = InitialMode;
            TransmitState = V18TransmitState.OriginatingConnected;
            ReceiveState = V18ReceiveState.OriginatingConnected;
        } else {
            Autobauding = true;
            CurrentMode = V18Mode.None;
            TransmitState = callingParty
                ? V18TransmitState.Originating1
                : V18TransmitState.Answering1;
            ReceiveState = callingParty
                ? V18ReceiveState.Originating1
                : V18ReceiveState.Answering1;
        }

        Logging.Dispose();
        Logging = new SpanLogState((int)SpanLogSeverity.None, null);
        Logging.SetProtocol("V.18");
        _disposed = false;
    }

    internal V18TransmitState TransmitState { get; private set; }

    internal V18ReceiveState ReceiveState { get; private set; }

    public int Transmit(Span<short> samples) {
        ThrowIfDisposed();

        if (TransmitSuppressionTimer > 0)
            TransmitSuppressionTimer = Math.Max(0, TransmitSuppressionTimer - samples.Length);

        switch (TransmitState) {
            case V18TransmitState.Originating1:
            case V18TransmitState.Originating2:
            case V18TransmitState.Originating3:
            case V18TransmitState.Answering1:
            case V18TransmitState.Answering2:
            case V18TransmitState.Answering3:
                // These automoding generation states are placeholders in v18.c.
                return 0;

            case V18TransmitState.OriginatingConnected:
            case V18TransmitState.AnsweringConnected:
                if (_transmitSignalOn == 0)
                    return 0;

                if ((CurrentMode & V18Mode.Dtmf) != 0) {
                    LoadNextDtmfCharacter();
                    return _dtmfTransmitter.Generate(samples);
                }

                if (_fskTransmitter is null)
                    return 0;

                int generated = Fsk.Transmit(_fskTransmitter, samples);
                if (generated <= 0) {
                    _transmitSignalOn = 0;
                    return 0;
                }

                return generated;

            default:
                return 0;
        }
    }

    public int Transmit(short[] samples, int maximumLength) {
        ArgumentNullException.ThrowIfNull(samples);
        if (maximumLength < 0 || maximumLength > samples.Length)
            throw new ArgumentOutOfRangeException(nameof(maximumLength));
        return Transmit(samples.AsSpan(0, maximumLength));
    }

    public int Receive(ReadOnlySpan<short> samples) {
        ThrowIfDisposed();

        if (ReceiveSuppressionTimer > 0)
            ReceiveSuppressionTimer = Math.Max(0, ReceiveSuppressionTimer - samples.Length);

        switch (ReceiveState) {
            case V18ReceiveState.Originating1:
                ScanForMode(samples, caller: true);
                break;

            case V18ReceiveState.Answering1:
                ScanForMode(samples, caller: false);
                break;

            case V18ReceiveState.OriginatingConnected:
            case V18ReceiveState.AnsweringConnected:
                if ((CurrentMode & V18Mode.Dtmf) != 0) {
                    DecrementMessageTimer(samples.Length);
                    _dtmfReceiver.Process(samples);
                } else if (_fskReceiver is not null) {
                    Fsk.Receive(_fskReceiver, samples);
                }
                break;
        }

        return 0;
    }

    public int Receive(short[] samples, int length) {
        ArgumentNullException.ThrowIfNull(samples);
        if (length < 0 || length > samples.Length)
            throw new ArgumentOutOfRangeException(nameof(length));
        return Receive(samples.AsSpan(0, length));
    }

    public int ReceiveFillIn(int length) {
        ThrowIfDisposed();
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        if (ReceiveSuppressionTimer > 0)
            ReceiveSuppressionTimer = Math.Max(0, ReceiveSuppressionTimer - length);

        if (Autobauding)
            return 0;

        if (CurrentMode != V18Mode.None) {
            if ((CurrentMode & V18Mode.Dtmf) != 0) {
                DecrementMessageTimer(length);
                _dtmfReceiver.FillIn(length);
            } else if (_fskReceiver is not null) {
                Fsk.ReceiveFillIn(_fskReceiver, length);
            }
        }

        return 0;
    }

    public int Put(string message, int length = -1) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(message);

        int nul = message.IndexOf('\0');
        string effective = nul >= 0 ? message[..nul] : message;

        byte[] data = Encoding.Latin1.GetBytes(effective);
        if (length < 0)
            length = data.Length;
        if (length > data.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        if (length == 0)
            return 0;

        int result = _queue.Write(data, length);
        if (result < 0)
            return result;

        if (_transmitSignalOn == 0) {
            Logging.Log((int)SpanLogSeverity.Flow, "Turning on the carrier\n");
            _transmitSignalOn = 1;
        }

        return result;
    }

    public int Put(ReadOnlySpan<byte> message) {
        ThrowIfDisposed();

        if (message.IsEmpty)
            return 0;

        int result = _queue.Write(message, message.Length);
        if (result < 0)
            return result;

        if (_transmitSignalOn == 0) {
            Logging.Log((int)SpanLogSeverity.Flow, "Turning on the carrier\n");
            _transmitSignalOn = 1;
        }

        return result;
    }

    public int SetStoredMessage(string message) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(message);

        byte[] bytes = Encoding.Latin1.GetBytes(message);
        if (bytes.Length > MaximumStoredMessageBytes)
            bytes = bytes[..MaximumStoredMessageBytes];

        StoredMessage = Encoding.Latin1.GetString(bytes);
        return 0;
    }

    public int GetCurrentMode() => (int)CurrentMode;

    public V18Mode[] GetAutomodingSequence() {
        ThrowIfDisposed();

        int index = (int)Nation;
        var result = new V18Mode[6];
        for (int i = 0; i < result.Length; i++)
            result[i] = AutomodingSequences[index, i];
        return result;
    }

    public int Release() {
        if (_disposed)
            return 0;

        _queue.Release();
        DisposeModemObjects();
        return 0;
    }

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
        _dtmfTransmitter.Reset();

        V18Mode baseMode = mode & ~V18Mode.RepetitiveShiftsOption;

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
                _dtmfTransmitter.Reset();
                _dtmfReceiver.Initialize(DtmfReceived);
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
                    CallingParty ? FskPreset.Bell103Channel1 : FskPreset.Bell103Channel2,
                    CallingParty ? FskPreset.Bell103Channel2 : FskPreset.Bell103Channel1,
                    7,
                    AsyncParity.Even,
                    1,
                    Bell103GetAsyncByte,
                    Bell103PutAsyncByte);
                Logging.Log((int)SpanLogSeverity.Flow, "Turning on the carrier\n");
                _transmitSignalOn = 1;
                break;

            case V18Mode.V23Videotex:
                InitializeFsk(
                    CallingParty ? FskPreset.V23Channel2 : FskPreset.V23Channel1,
                    CallingParty ? FskPreset.V23Channel1 : FskPreset.V23Channel2,
                    7,
                    AsyncParity.Even,
                    1,
                    VideotexGetAsyncByte,
                    VideotexPutAsyncByte);
                Logging.Log((int)SpanLogSeverity.Flow, "Turning on the carrier\n");
                _transmitSignalOn = 1;
                break;

            case V18Mode.V21Textphone:
                InitializeFsk(
                    CallingParty ? FskPreset.V21Channel1 : FskPreset.V21Channel2,
                    CallingParty ? FskPreset.V21Channel2 : FskPreset.V21Channel1,
                    7,
                    AsyncParity.Even,
                    1,
                    TextphoneGetAsyncByte,
                    TextphonePutAsyncByte);
                Logging.Log((int)SpanLogSeverity.Flow, "Turning on the carrier\n");
                _transmitSignalOn = 1;
                break;

            case V18Mode.V18Textphone:
                InitializeFsk(
                    FskPreset.V21Channel1,
                    FskPreset.V21Channel1,
                    7,
                    AsyncParity.Even,
                    1,
                    TextphoneGetAsyncByte,
                    TextphonePutAsyncByte);
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
        SpanPutByteDelegate byteConsumer) {
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

        _fskReceiver = Fsk.InitializeReceiver(
            null,
            Fsk.GetPreset(receivePreset),
            FskFrameMode.Framed,
            static (userData, value) => {
                V18State state = (V18State)userData!;
                state._activeByteConsumer!(state, value);
            },
            this);

        _activeByteConsumer = byteConsumer;
        Fsk.SetReceiveFrameParameters(
            _fskReceiver,
            dataBits,
            ToFskParity(parity),
            stopBits);
    }

    private SpanPutByteDelegate? _activeByteConsumer;

    private int TddGetAsyncByte(object? userData) {
        _ = userData;

        if (_nextByte != 0xFF) {
            ReceiveSuppressionTimer = MillisecondsToSamples(TddCharacterSuppressionMilliseconds);
            int next = _nextByte;
            _nextByte = 0xFF;
            return next;
        }

        while (true) {
            int character = _queue.ReadByte();
            if (character < 0) {
                if (_transmitDraining) {
                    _transmitDraining = false;
                    return (int)SignalStatus.EndOfData;
                }

                Logging.Log((int)SpanLogSeverity.Flow, "Tx shutdown with delay\n");
                _asyncTransmitter!.SetPresendBits(14);
                _transmitDraining = true;
                ReceiveSuppressionTimer = MillisecondsToSamples(TddDrainSuppressionMilliseconds);
                return (int)SignalStatus.LinkIdle;
            }

            ushort encoded = EncodeBaudot((byte)character);
            if (encoded == 0)
                continue;

            ReceiveSuppressionTimer = MillisecondsToSamples(TddCharacterSuppressionMilliseconds);

            if (_transmitSignalOn == 1) {
                _asyncTransmitter!.SetPresendBits(7);
                _transmitSignalOn = 2;
            }

            if ((encoded & 0x03E0) != 0) {
                _nextByte = (byte)(encoded & 0x1F);
                return (encoded >> 5) & 0x1F;
            }

            _nextByte = 0xFF;
            return encoded & 0x1F;
        }
    }

    private int EdtGetAsyncByte(object? userData) {
        _ = userData;
        int character = _queue.ReadByte();
        if (character >= 0) {
            ReceiveSuppressionTimer = MillisecondsToSamples(300);
            return character;
        }

        if (_transmitSignalOn != 0) {
            Logging.Log((int)SpanLogSeverity.Flow, "Turning off the carrier\n");
            _transmitSignalOn = 0;
        }

        return (int)SignalStatus.LinkIdle;
    }

    private int Bell103GetAsyncByte(object? userData) {
        _ = userData;
        int character = _queue.ReadByte();
        return character >= 0 ? character : (int)SignalStatus.LinkIdle;
    }

    private int VideotexGetAsyncByte(object? userData) => Bell103GetAsyncByte(userData);

    private int TextphoneGetAsyncByte(object? userData) => Bell103GetAsyncByte(userData);

    private void TddPutAsyncByte(object? userData, int value) {
        _ = userData;

        if (value < 0) {
            Logging.Log(
                (int)SpanLogSeverity.Flow,
                "TDD signal status is %s (%d)\n",
                SignalStatusToString(value),
                value);

            switch ((SignalStatus)value) {
                case SignalStatus.CarrierUp:
                    MessageInProgressTimer = 0;
                    _receiveMessageLength = 0;
                    break;

                case SignalStatus.CarrierDown:
                    DeliverBufferedMessage();
                    break;

                default:
                    Logging.Log(
                        (int)SpanLogSeverity.Warning,
                        "Unexpected special put byte value - %d!\n",
                        value);
                    break;
            }

            return;
        }

        if (ReceiveSuppressionTimer > 0) {
            Logging.Log(
                (int)SpanLogSeverity.Flow,
                "Rx suppressed byte 0x%02x (%d)\n",
                value,
                ReceiveSuppressionTimer);
            return;
        }

        byte decoded = DecodeBaudot((byte)value);
        if (decoded != 0xFF) {
            _receiveMessage[_receiveMessageLength++] = decoded;
            Logging.Log(
                (int)SpanLogSeverity.Flow,
                "Rx byte 0x%02x '%c'\n",
                decoded,
                decoded);
        }

        DeliverBufferedMessage();
    }

    private void EdtPutAsyncByte(object? userData, int value) {
        _ = userData;
        if (ReceiveSuppressionTimer > 0 || value < 0)
            return;
        DeliverSingleByte((byte)value);
    }

    private void Bell103PutAsyncByte(object? userData, int value) => EdtPutAsyncByte(userData, value);

    private void VideotexPutAsyncByte(object? userData, int value) => EdtPutAsyncByte(userData, value);

    private void TextphonePutAsyncByte(object? userData, int value) => EdtPutAsyncByte(userData, value);

    private ushort EncodeBaudot(byte character) {
        byte code = BaudotEncodeTable[character & 0x7F];
        if (code == 0xFF)
            return 0;

        if ((code & 0x40) != 0)
            return (ushort)(0x8000 | (code & 0x1F));

        ushort shift;
        if ((code & 0x80) != 0) {
            if (!RepeatShifts && _baudotTransmitShift == 1)
                return (ushort)(code & 0x1F);

            _baudotTransmitShift = 1;
            shift = BaudotFigureShift;
        } else {
            if (!RepeatShifts && _baudotTransmitShift == 0)
                return (ushort)(code & 0x1F);

            _baudotTransmitShift = 0;
            shift = BaudotLetterShift;
        }

        return (ushort)(0x8000 | (shift << 5) | (code & 0x1F));
    }

    private byte DecodeBaudot(byte code) {
        switch (code) {
            case BaudotFigureShift:
                _baudotReceiveShift = 1;
                return 0xFF;

            case BaudotLetterShift:
                _baudotReceiveShift = 0;
                return 0xFF;

            default:
                return BaudotDecodeTable[_baudotReceiveShift][code & 0x1F];
        }
    }

    private int TxpGetBit() {
        const string txp = "1111111111000101011100001101110000010101";
        int bit = txp[_txpCount] == '1' ? 1 : 0;
        _txpCount++;
        if (_txpCount >= 40)
            _txpCount = 0;
        return bit;
    }

    private void ScanForMode(ReadOnlySpan<short> samples, bool caller) {
        _dtmfReceiver.Process(samples);
        if (_answerToneReceiver is not null)
            ModemConnectTones.Receive(_answerToneReceiver, samples);

        int role = caller ? 0 : 1;

        for (int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex++) {
            if (!_toneBank.AddSample(samples[sampleIndex]))
                continue;

            int toneIndex = _toneBank.GetDominantTone(
                minimumAbsoluteEnergy: 2.0e8,
                minimumToneToTotalRatio: 20.0);

            if (toneIndex >= 0 && !ToneEnabled[role, toneIndex])
                toneIndex = -1;

            V18ProbeTone tone = toneIndex >= 0
                ? (V18ProbeTone)toneIndex
                : V18ProbeTone.None;

            _toneDuration = Math.Min(
                int.MaxValue - GoertzelSamplesPerBlock,
                _toneDuration + GoertzelSamplesPerBlock);

            if (tone != _inTone) {
                _inTone = tone;
                _toneDuration = 0;
                _targetToneDuration = toneIndex >= 0
                    ? ToneTargetDurations[toneIndex]
                    : 0;
            } else if (_targetToneDuration > 0 &&
                       _toneDuration >= _targetToneDuration) {
                ConfirmProbeTone(tone, caller);
                _targetToneDuration = 0;
            }

            _toneBank.ResetBlock();
        }
    }

    private void ConfirmProbeTone(V18ProbeTone tone, bool caller) {
        Logging.Log(
            (int)SpanLogSeverity.Flow,
            "Tone %s (%d) seen\n",
            ToneToString(tone),
            (int)tone);

        switch (tone) {
            case V18ProbeTone.Hz390 when caller:
            case V18ProbeTone.Hz1300:
                SwitchMode(V18Mode.V23Videotex, V18Status.SwitchToV23Videotex);
                break;

            case V18ProbeTone.Hz980:
            case V18ProbeTone.Hz1180:
            case V18ProbeTone.Hz1650:
                SwitchMode(V18Mode.V21Textphone, V18Status.SwitchToV21Textphone);
                break;

            case V18ProbeTone.Hz1270:
            case V18ProbeTone.Hz2225:
                SwitchMode(V18Mode.Bell103, V18Status.SwitchToBell103);
                break;

            case V18ProbeTone.Hz1400:
            case V18ProbeTone.Hz1800:
                // The native source also leaves exact 45.45/47.6/50 detection as TODO.
                SwitchMode(V18Mode.Weitbrecht5Bit476, V18Status.SwitchToWeitbrecht5Bit476);
                break;
        }
    }

    private void SwitchMode(V18Mode mode, V18Status status) {
        _statusHandler?.Invoke(_statusHandlerUserData, (int)status);
        Autobauding = false;
        SetModem(mode);
    }

    private void DtmfReceived(string digits) {
        if (CurrentMode != V18Mode.Dtmf)
            SwitchMode(V18Mode.Dtmf, V18Status.SwitchToDtmf);

        TransmitSuppressionTimer = MillisecondsToSamples(400);
        if (ReceiveSuppressionTimer > 0)
            return;

        foreach (char digit in digits) {
            _receivedDtmf.Append(digit);
            if (digit is < '0' or > '9')
                continue;

            if (TryDecodeDtmf(_receivedDtmf.ToString(), out byte character, out int consumed)) {
                DeliverSingleByte(character);
                _receivedDtmf.Remove(0, consumed);
            } else {
                // Drop an invalid complete sequence so the decoder cannot stall.
                int firstTerminator = FindFirstDtmfTerminator(_receivedDtmf);
                if (firstTerminator >= 0)
                    _receivedDtmf.Remove(0, firstTerminator + 1);
            }
        }

        MessageInProgressTimer = SecondsToSamples(5);
    }

    private static bool TryDecodeDtmf(
        string sequence,
        out byte character,
        out int consumed) {
        for (int length = Math.Min(4, sequence.Length); length >= 1; length--) {
            string candidate = sequence[..length];
            if (DtmfToAscii.TryGetValue(candidate, out character)) {
                consumed = length;
                return true;
            }
        }

        character = 0;
        consumed = 0;
        return false;
    }

    private static int FindFirstDtmfTerminator(StringBuilder sequence) {
        for (int i = 0; i < sequence.Length; i++) {
            if (sequence[i] is >= '0' and <= '9')
                return i;
        }

        return -1;
    }

    private void LoadNextDtmfCharacter() {
        if (!_dtmfTransmitter.IsIdle || TransmitSuppressionTimer > 0)
            return;

        int character = _queue.ReadByte();
        if (character < 0)
            return;

        string sequence;
        if ((character & 0x80) != 0) {
            sequence = character switch {
                0xC6 => AsciiToDtmf[0x5B],
                0xD8 => AsciiToDtmf[0x5C],
                0xC5 => AsciiToDtmf[0x5D],
                0xE6 => AsciiToDtmf[0x7B],
                0xF8 => AsciiToDtmf[0x7C],
                0xE5 => AsciiToDtmf[0x7D],
                _ => string.Empty
            };
        } else {
            sequence = AsciiToDtmf[character];
        }

        if (sequence.Length > 0) {
            _dtmfTransmitter.Enqueue(sequence);
            ReceiveSuppressionTimer =
                MillisecondsToSamples(300 + 100 * sequence.Length);
        }
    }

    private void AnswerToneReceived(
        object? userData,
        ModemConnectTone tone,
        int level,
        int duration) {
        _ = userData;
        _ = level;
        _ = duration;

        if (tone != ModemConnectTone.AnsamWithPhaseReversals)
            return;

        if (CallingParty)
            SwitchMode(V18Mode.V18Textphone, V18Status.SwitchToV18Textphone);
    }

    private void DeliverBufferedMessage() {
        if (_receiveMessageLength <= 0)
            return;

        _putMessage?.Invoke(
            _putMessageUserData,
            _receiveMessage.AsSpan(0, _receiveMessageLength));

        _receiveMessageLength = 0;
    }

    private void DeliverSingleByte(byte value) {
        Span<byte> one = stackalloc byte[1];
        one[0] = value;
        _putMessage?.Invoke(_putMessageUserData, one);
    }

    private void DecrementMessageTimer(int samples) {
        if (MessageInProgressTimer <= 0)
            return;

        MessageInProgressTimer -= samples;
        if (MessageInProgressTimer <= 0) {
            MessageInProgressTimer = 0;
            _receiveMessageLength = 0;
            _receivedDtmf.Clear();
        }
    }

    private void DisposeFskAndAsync() {
        _fskTransmitter?.Dispose();
        _fskTransmitter = null;

        _fskReceiver?.Dispose();
        _fskReceiver = null;

        _asyncTransmitter?.Dispose();
        _asyncTransmitter = null;

        _activeByteConsumer = null;
    }

    private void DisposeModemObjects() {
        DisposeFskAndAsync();

        _answerToneReceiver?.Dispose();
        _answerToneReceiver = null;
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
    public const int V18_MODE_NONE = (int)V18Mode.None;
    public const int V18_MODE_WEITBRECHT_5BIT_4545 = (int)V18Mode.Weitbrecht5Bit4545;
    public const int V18_MODE_WEITBRECHT_5BIT_50 = (int)V18Mode.Weitbrecht5Bit50;
    public const int V18_MODE_DTMF = (int)V18Mode.Dtmf;
    public const int V18_MODE_EDT = (int)V18Mode.Edt;
    public const int V18_MODE_BELL103 = (int)V18Mode.Bell103;
    public const int V18_MODE_V23VIDEOTEX = (int)V18Mode.V23Videotex;
    public const int V18_MODE_V21TEXTPHONE = (int)V18Mode.V21Textphone;
    public const int V18_MODE_V18TEXTPHONE = (int)V18Mode.V18Textphone;
    public const int V18_MODE_WEITBRECHT_5BIT_476 = (int)V18Mode.Weitbrecht5Bit476;
    public const int V18_MODE_REPETITIVE_SHIFTS_OPTION = (int)V18Mode.RepetitiveShiftsOption;

    public static SpanLogState v18_get_logging_state(V18State state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Logging;
    }

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

    public static int v18_release(V18State state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    public static int v18_free(V18State? state) {
        state?.Dispose();
        return 0;
    }

    public static int v18_tx(
        V18State state,
        short[] samples,
        int maximumLength) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Transmit(samples, maximumLength);
    }

    public static int v18_tx(
        V18State state,
        Span<short> samples) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Transmit(samples);
    }

    public static int v18_rx(
        V18State state,
        short[] samples,
        int length) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Receive(samples, length);
    }

    public static int v18_rx(
        V18State state,
        ReadOnlySpan<short> samples) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Receive(samples);
    }

    public static int v18_rx_fillin(V18State state, int length) {
        ArgumentNullException.ThrowIfNull(state);
        return state.ReceiveFillIn(length);
    }

    public static int v18_put(V18State state, string message, int length) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Put(message, length);
    }

    public static int v18_put(V18State state, ReadOnlySpan<byte> message) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Put(message);
    }

    public static int v18_set_stored_message(V18State state, string message) {
        ArgumentNullException.ThrowIfNull(state);
        return state.SetStoredMessage(message);
    }

    public static int v18_get_current_mode(V18State state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetCurrentMode();
    }

    public static string v18_mode_to_str(int mode) => V18.ModeToString(mode);

    public static string v18_status_to_str(int status) => V18.StatusToString(status);
}

internal sealed class V18GoertzelBank {
    private readonly double[] _coefficients;
    private readonly double[] _q1;
    private readonly double[] _q2;
    private readonly int _blockLength;
    private double _totalEnergy;
    private int _sampleCount;

    public V18GoertzelBank(float[] frequencies, int blockLength) {
        _blockLength = blockLength;
        _coefficients = new double[frequencies.Length];
        _q1 = new double[frequencies.Length];
        _q2 = new double[frequencies.Length];

        for (int i = 0; i < frequencies.Length; i++) {
            double omega = 2.0 * Math.PI * frequencies[i] / V18State.SampleRate;
            _coefficients[i] = 2.0 * Math.Cos(omega);
        }
    }

    public bool AddSample(short sample) {
        double value = sample;
        _totalEnergy += value * value;

        for (int i = 0; i < _coefficients.Length; i++) {
            double q0 = value + _coefficients[i] * _q1[i] - _q2[i];
            _q2[i] = _q1[i];
            _q1[i] = q0;
        }

        _sampleCount++;
        return _sampleCount >= _blockLength;
    }

    public int GetDominantTone(
        double minimumAbsoluteEnergy,
        double minimumToneToTotalRatio) {
        if (_sampleCount < _blockLength || _totalEnergy <= 0.0)
            return -1;

        Span<double> energies = stackalloc double[_coefficients.Length];
        GetEnergies(energies);

        int bestIndex = -1;
        double bestEnergy = 0.0;
        double secondEnergy = 0.0;

        for (int i = 0; i < energies.Length; i++) {
            double energy = energies[i];

            if (energy > bestEnergy) {
                secondEnergy = bestEnergy;
                bestEnergy = energy;
                bestIndex = i;
            } else if (energy > secondEnergy) {
                secondEnergy = energy;
            }
        }

        if (bestEnergy < minimumAbsoluteEnergy)
            return -1;

        if (bestEnergy < _totalEnergy * minimumToneToTotalRatio)
            return -1;

        if (secondEnergy > 0.0 && bestEnergy < secondEnergy * 2.5)
            return -1;

        return bestIndex;
    }

    public double TotalEnergy => _totalEnergy;

    public bool BlockComplete => _sampleCount >= _blockLength;

    public void GetEnergies(Span<double> destination) {
        if (destination.Length < _coefficients.Length)
            throw new ArgumentException(
                "The destination is shorter than the Goertzel bin count.",
                nameof(destination));

        for (int i = 0; i < _coefficients.Length; i++) {
            destination[i] =
                _q1[i] * _q1[i] +
                _q2[i] * _q2[i] -
                _coefficients[i] * _q1[i] * _q2[i];
        }
    }

    public void ResetBlock() {
        Array.Clear(_q1);
        Array.Clear(_q2);
        _totalEnergy = 0.0;
        _sampleCount = 0;
    }

    public void Reset() => ResetBlock();
}

internal sealed class V18DtmfTransmitter {
    private const int ToneSamples = V18State.SampleRate / 20;
    private const int PauseSamples = V18State.SampleRate / 20;
    private const double Amplitude = 5000.0;

    private readonly Queue<char> _digits = new();
    private char _activeDigit;
    private int _remainingTone;
    private int _remainingPause;
    private double _rowPhase;
    private double _columnPhase;

    public bool IsIdle =>
        _digits.Count == 0 &&
        _remainingTone == 0 &&
        _remainingPause == 0;

    public void Enqueue(string digits) {
        foreach (char digit in digits) {
            if ("0123456789*#ABCD".IndexOf(digit) >= 0)
                _digits.Enqueue(digit);
        }
    }

    public int Generate(Span<short> samples) {
        int produced = 0;

        while (produced < samples.Length) {
            if (_remainingTone == 0 && _remainingPause == 0) {
                if (_digits.Count == 0)
                    break;

                _activeDigit = _digits.Dequeue();
                _remainingTone = ToneSamples;
                _rowPhase = 0.0;
                _columnPhase = 0.0;
            }

            if (_remainingTone > 0) {
                (double row, double column) = Frequencies(_activeDigit);
                int count = Math.Min(samples.Length - produced, _remainingTone);

                double rowStep = 2.0 * Math.PI * row / V18State.SampleRate;
                double columnStep = 2.0 * Math.PI * column / V18State.SampleRate;

                for (int i = 0; i < count; i++) {
                    double value =
                        Math.Sin(_rowPhase) * Amplitude +
                        Math.Sin(_columnPhase) * Amplitude;

                    samples[produced + i] = (short)Math.Clamp(
                        (int)Math.Round(value),
                        short.MinValue,
                        short.MaxValue);

                    _rowPhase += rowStep;
                    _columnPhase += columnStep;
                }

                produced += count;
                _remainingTone -= count;

                if (_remainingTone == 0)
                    _remainingPause = PauseSamples;

                continue;
            }

            int pause = Math.Min(samples.Length - produced, _remainingPause);
            samples.Slice(produced, pause).Clear();
            produced += pause;
            _remainingPause -= pause;
        }

        return produced;
    }

    public void Reset() {
        _digits.Clear();
        _activeDigit = '\0';
        _remainingTone = 0;
        _remainingPause = 0;
        _rowPhase = 0.0;
        _columnPhase = 0.0;
    }

    private static (double Row, double Column) Frequencies(char digit) {
        return digit switch {
            '1' => (697, 1209),
            '2' => (697, 1336),
            '3' => (697, 1477),
            'A' => (697, 1633),
            '4' => (770, 1209),
            '5' => (770, 1336),
            '6' => (770, 1477),
            'B' => (770, 1633),
            '7' => (852, 1209),
            '8' => (852, 1336),
            '9' => (852, 1477),
            'C' => (852, 1633),
            '*' => (941, 1209),
            '0' => (941, 1336),
            '#' => (941, 1477),
            'D' => (941, 1633),
            _ => (0, 0)
        };
    }
}

internal sealed class V18DtmfReceiver {
    private static readonly float[] Frequencies =
    {
        697.0f, 770.0f, 852.0f, 941.0f,
        1209.0f, 1336.0f, 1477.0f, 1633.0f
    };

    private static readonly char[,] Digits =
    {
        { '1', '2', '3', 'A' },
        { '4', '5', '6', 'B' },
        { '7', '8', '9', 'C' },
        { '*', '0', '#', 'D' }
    };

    private const int BlockLength = 102;
    private const double MinimumBinEnergy = 2.0e8;
    private const double MinimumBinToTotalRatio = 4.0;
    private const double MinimumDominanceRatio = 2.5;
    private const double MinimumTwistRatio = 0.20;
    private const double MaximumTwistRatio = 5.0;

    private readonly V18GoertzelBank _bank =
        new(Frequencies, BlockLength);

    private Action<string>? _callback;
    private char _candidate;
    private int _candidateBlocks;
    private char _reported;

    public void Initialize(Action<string> callback) {
        ArgumentNullException.ThrowIfNull(callback);

        _callback = callback;
        _candidate = '\0';
        _candidateBlocks = 0;
        _reported = '\0';
        _bank.Reset();
    }

    public void Process(ReadOnlySpan<short> samples) {
        foreach (short sample in samples) {
            if (!_bank.AddSample(sample))
                continue;

            char detected = Detect();

            if (detected == _candidate) {
                _candidateBlocks++;
            } else {
                _candidate = detected;
                _candidateBlocks = 1;
            }

            // Two consecutive 12.75 ms blocks provide a conservative guard
            // against talk-off while still reporting a normal 50 ms DTMF tone.
            if (_candidateBlocks >= 2 &&
                detected != '\0' &&
                detected != _reported) {
                _reported = detected;
                _callback?.Invoke(detected.ToString());
            }

            if (detected == '\0')
                _reported = '\0';

            _bank.ResetBlock();
        }
    }

    public void FillIn(int samples) {
        if (samples <= 0)
            return;

        Span<short> silence = stackalloc short[BlockLength];
        silence.Clear();

        int remaining = samples;
        while (remaining > 0) {
            int count = Math.Min(remaining, silence.Length);
            Process(silence[..count]);
            remaining -= count;
        }
    }

    private char Detect() {
        if (!_bank.BlockComplete || _bank.TotalEnergy <= 0.0)
            return '\0';

        Span<double> energies = stackalloc double[8];
        _bank.GetEnergies(energies);

        (int row, double rowEnergy, double nextRowEnergy) =
            FindStrongest(energies[..4]);

        (int column, double columnEnergy, double nextColumnEnergy) =
            FindStrongest(energies[4..]);

        if (row < 0 || column < 0)
            return '\0';

        if (rowEnergy < MinimumBinEnergy ||
            columnEnergy < MinimumBinEnergy) {
            return '\0';
        }

        if (rowEnergy < _bank.TotalEnergy * MinimumBinToTotalRatio ||
            columnEnergy < _bank.TotalEnergy * MinimumBinToTotalRatio) {
            return '\0';
        }

        if (nextRowEnergy > 0.0 &&
            rowEnergy < nextRowEnergy * MinimumDominanceRatio) {
            return '\0';
        }

        if (nextColumnEnergy > 0.0 &&
            columnEnergy < nextColumnEnergy * MinimumDominanceRatio) {
            return '\0';
        }

        double twist = rowEnergy / columnEnergy;
        if (twist < MinimumTwistRatio || twist > MaximumTwistRatio)
            return '\0';

        return Digits[row, column];
    }

    private static (int Index, double Energy, double NextEnergy) FindStrongest(
        ReadOnlySpan<double> energies) {
        int bestIndex = -1;
        double bestEnergy = 0.0;
        double nextEnergy = 0.0;

        for (int i = 0; i < energies.Length; i++) {
            double energy = energies[i];
            if (energy > bestEnergy) {
                nextEnergy = bestEnergy;
                bestEnergy = energy;
                bestIndex = i;
            } else if (energy > nextEnergy) {
                nextEnergy = energy;
            }
        }

        return (bestIndex, bestEnergy, nextEnergy);
    }
}
