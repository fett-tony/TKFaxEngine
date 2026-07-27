/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * V1501Sse.cs - Managed C# port of v150_1_sse.c and v150_1_sse.h
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2022, 2023 Steve Underwood
 *
 * This file is distributed under the terms of the GNU General Public License
 * version 2, matching the original source files.
 */

#nullable enable

namespace TKFaxEngine.Modem.V150 {
    using System;

    /// <summary>
    /// Media states defined by Table C.1/V.150.1
    /// </summary>
    /// <summary>
    /// Call-discrimination selection used when a modem-relay SSE is received
    /// </summary>
    /// <summary>
    /// Media states defined by Table C.1/V.150.1
    /// </summary>
    /// <summary>
    /// Call-discrimination selection used when a modem-relay SSE is received
    /// </summary>
    /// <summary>
    /// V.150.1 Annex C MoIP/ToIP reason-identification codes
    /// </summary>
    public enum V1501SseMoipRic {
        /// <summary>
        /// Defines the V8Cm
        /// </summary>
        V8Cm = 1,

        /// <summary>
        /// Defines the V8Jm
        /// </summary>
        V8Jm = 2,

        /// <summary>
        /// Defines the V32BisAa
        /// </summary>
        V32BisAa = 3,

        /// <summary>
        /// Defines the V32BisAc
        /// </summary>
        V32BisAc = 4,

        /// <summary>
        /// Defines the V22BisUsb1
        /// </summary>
        V22BisUsb1 = 5,

        /// <summary>
        /// Defines the V22BisSb1
        /// </summary>
        V22BisSb1 = 6,

        /// <summary>
        /// Defines the V22BisS1
        /// </summary>
        V22BisS1 = 7,

        /// <summary>
        /// Defines the V21Channel2
        /// </summary>
        V21Channel2 = 8,

        /// <summary>
        /// Defines the V21Channel1
        /// </summary>
        V21Channel1 = 9,

        /// <summary>
        /// Defines the V23HighChannel
        /// </summary>
        V23HighChannel = 10,

        /// <summary>
        /// Defines the V23LowChannel
        /// </summary>
        V23LowChannel = 11,

        /// <summary>
        /// Defines the Tone2225Hz
        /// </summary>
        Tone2225Hz = 12,

        /// <summary>
        /// Defines the V21Channel2HdlcFlags
        /// </summary>
        V21Channel2HdlcFlags = 13,

        /// <summary>
        /// Defines the IndeterminateSignal
        /// </summary>
        IndeterminateSignal = 14,

        /// <summary>
        /// Defines the Silence
        /// </summary>
        Silence = 15,

        /// <summary>
        /// Defines the Cng
        /// </summary>
        Cng = 16,

        /// <summary>
        /// Defines the Voice
        /// </summary>
        Voice = 17,

        /// <summary>
        /// Defines the Timeout
        /// </summary>
        Timeout = 18,

        /// <summary>
        /// Defines the PStateTransition
        /// </summary>
        PStateTransition = 19,

        /// <summary>
        /// Defines the Cleardown
        /// </summary>
        Cleardown = 20,

        /// <summary>
        /// Defines the AnsCed
        /// </summary>
        AnsCed = 21,

        /// <summary>
        /// Defines the Ansam
        /// </summary>
        Ansam = 22,

        /// <summary>
        /// Defines the AnsPhaseReversal
        /// </summary>
        AnsPhaseReversal = 23,

        /// <summary>
        /// Defines the AnsamPhaseReversal
        /// </summary>
        AnsamPhaseReversal = 24,

        /// <summary>
        /// Defines the V92Qc1A
        /// </summary>
        V92Qc1A = 25,

        /// <summary>
        /// Defines the V92Qc1D
        /// </summary>
        V92Qc1D = 26,

        /// <summary>
        /// Defines the V92Qc2A
        /// </summary>
        V92Qc2A = 27,

        /// <summary>
        /// Defines the V92Qc2D
        /// </summary>
        V92Qc2D = 28,

        /// <summary>
        /// Defines the V8BisCre
        /// </summary>
        V8BisCre = 29,

        /// <summary>
        /// Defines the V8BisCrd
        /// </summary>
        V8BisCrd = 30,

        /// <summary>
        /// Defines the Tia825A4545Bps
        /// </summary>
        Tia825A4545Bps = 31,

        /// <summary>
        /// Defines the Tia825A50Bps
        /// </summary>
        Tia825A50Bps = 32,

        /// <summary>
        /// Defines the Edt
        /// </summary>
        Edt = 33,

        /// <summary>
        /// Defines the Bell103
        /// </summary>
        Bell103 = 34,

        /// <summary>
        /// Defines the V21TextTelephone
        /// </summary>
        V21TextTelephone = 35,

        /// <summary>
        /// Defines the V23Minitel
        /// </summary>
        V23Minitel = 36,

        /// <summary>
        /// Defines the V18TextTelephone
        /// </summary>
        V18TextTelephone = 37,

        /// <summary>
        /// Defines the V18DtmfTextRelay
        /// </summary>
        V18DtmfTextRelay = 38,

        /// <summary>
        /// Defines the Ctm
        /// </summary>
        Ctm = 39,

        /// <summary>
        /// Defines the VendorMinimum
        /// </summary>
        VendorMinimum = 128,

        /// <summary>
        /// Defines the VendorMaximum
        /// </summary>
        VendorMaximum = 255
    }

    /// <summary>
    /// T.38 Annex F FoIP reason-identification codes
    /// </summary>
    public enum V1501SseFoipRic {
        /// <summary>
        /// Defines the V21Flags
        /// </summary>
        V21Flags = 1,

        /// <summary>
        /// Defines the V8Cm
        /// </summary>
        V8Cm = 2,

        /// <summary>
        /// Defines the PStateTransition
        /// </summary>
        PStateTransition = 19
    }

    /// <summary>
    /// V.8 CM/JM capability bits carried in the RIC information field
    /// </summary>
    [Flags]
    public enum V1501SseV8Capability {
        /// <summary>
        /// Defines the None
        /// </summary>
        None = 0,

        /// <summary>
        /// Defines the PcmMode
        /// </summary>
        PcmMode = 0x8000,

        /// <summary>
        /// Defines the V34Duplex
        /// </summary>
        V34Duplex = 0x4000,

        /// <summary>
        /// Defines the V34HalfDuplex
        /// </summary>
        V34HalfDuplex = 0x2000,

        /// <summary>
        /// Defines the V32Bis
        /// </summary>
        V32Bis = 0x1000,

        /// <summary>
        /// Defines the V22Bis
        /// </summary>
        V22Bis = 0x0800,

        /// <summary>
        /// Defines the V17
        /// </summary>
        V17 = 0x0400,

        /// <summary>
        /// Defines the V29
        /// </summary>
        V29 = 0x0200,

        /// <summary>
        /// Defines the V27Ter
        /// </summary>
        V27Ter = 0x0100,

        /// <summary>
        /// Defines the V26Ter
        /// </summary>
        V26Ter = 0x0080,

        /// <summary>
        /// Defines the V26Bis
        /// </summary>
        V26Bis = 0x0040,

        /// <summary>
        /// Defines the V23Duplex
        /// </summary>
        V23Duplex = 0x0020,

        /// <summary>
        /// Defines the V23HalfDuplex
        /// </summary>
        V23HalfDuplex = 0x0010,

        /// <summary>
        /// Defines the V21
        /// </summary>
        V21 = 0x0008,

        /// <summary>
        /// Defines the V90V92Analogue
        /// </summary>
        V90V92Analogue = 0x0004,

        /// <summary>
        /// Defines the V90V92Digital
        /// </summary>
        V90V92Digital = 0x0002,

        /// <summary>
        /// Defines the V91
        /// </summary>
        V91 = 0x0001
    }

    /// <summary>
    /// Defines the V1501SseTimeoutReason
    /// </summary>
    public enum V1501SseTimeoutReason {
        /// <summary>
        /// Defines the Null
        /// </summary>
        Null = 0,

        /// <summary>
        /// Defines the CallDiscriminationTimeout
        /// </summary>
        CallDiscriminationTimeout = 1,

        /// <summary>
        /// Defines the IpTlp
        /// </summary>
        IpTlp = 2,

        /// <summary>
        /// Defines the ExplicitAcknowledgementTimeout
        /// </summary>
        ExplicitAcknowledgementTimeout = 3
    }

    /// <summary>
    /// Defines the V1501SseCleardownReason
    /// </summary>
    public enum V1501SseCleardownReason {
        /// <summary>
        /// Defines the Unknown
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Defines the PhysicalLayerRelease
        /// </summary>
        PhysicalLayerRelease = 1,

        /// <summary>
        /// Defines the LinkLayerDisconnect
        /// </summary>
        LinkLayerDisconnect = 2,

        /// <summary>
        /// Defines the CompressionDisconnect
        /// </summary>
        CompressionDisconnect = 3,

        /// <summary>
        /// Defines the Abort
        /// </summary>
        Abort = 4,

        /// <summary>
        /// Defines the OnHook
        /// </summary>
        OnHook = 5,

        /// <summary>
        /// Defines the NetworkLayerTermination
        /// </summary>
        NetworkLayerTermination = 6,

        /// <summary>
        /// Defines the Administrative
        /// </summary>
        Administrative = 7
    }

    /// <summary>
    /// Defines the V1501SseReliabilityMethod
    /// </summary>
    public enum V1501SseReliabilityMethod {
        /// <summary>
        /// Defines the None
        /// </summary>
        None = 0,

        /// <summary>
        /// Defines the Repetition
        /// </summary>
        Repetition = 1,

        /// <summary>
        /// Defines the Rfc2198
        /// </summary>
        Rfc2198 = 2,

        /// <summary>
        /// Defines the ExplicitAcknowledgement
        /// </summary>
        ExplicitAcknowledgement = 3
    }

    /// <summary>
    /// Defines the V1501SseStatus
    /// </summary>
    public enum V1501SseStatus {
        /// <summary>
        /// Defines the V8CmReceived
        /// </summary>
        V8CmReceived = 10,

        /// <summary>
        /// Defines the V8JmReceived
        /// </summary>
        V8JmReceived = 11,

        /// <summary>
        /// Defines the AaReceived
        /// </summary>
        AaReceived = 12,

        /// <summary>
        /// Defines the V8CmReceivedFax
        /// </summary>
        V8CmReceivedFax = 13,

        /// <summary>
        /// Defines the V8JmReceivedFax
        /// </summary>
        V8JmReceivedFax = 14,

        /// <summary>
        /// Defines the AaReceivedFax
        /// </summary>
        AaReceivedFax = 15,

        /// <summary>
        /// Defines the Cleardown
        /// </summary>
        Cleardown = 16
    }

    /// <summary>
    /// The V1501SseStatusDelegate
    /// </summary>
    /// <param name="userData">The userData<see cref="object?"/></param>
    /// <param name="status">The status<see cref="int"/></param>
    /// <returns>The <see cref="int"/></returns>
    public delegate int V1501SseStatusDelegate(object? userData, int status);

    /// <summary>
    /// The V1501StateMachineDelegate
    /// </summary>
    /// <param name="state">The state<see cref="V1501State"/></param>
    /// <param name="signal">The signal<see cref="int"/></param>
    /// <param name="message">The message<see cref="byte[]?"/></param>
    /// <param name="length">The length<see cref="int"/></param>
    /// <returns>The <see cref="int"/></returns>
    public delegate int V1501StateMachineDelegate(
        V1501State state,
        int signal,
        byte[]? message,
        int length);

    /// <summary>
    /// The V1501LogDelegate
    /// </summary>
    /// <param name="message">The message<see cref="string"/></param>
    public delegate void V1501LogDelegate(string message);

    /// <summary>
    /// Working state for V.150.1 State Signalling Events
    /// </summary>
    public sealed class V1501SseState {
        /// <summary>
        /// Defines the ReliabilityMethod
        /// </summary>
        internal V1501SseReliabilityMethod ReliabilityMethod;

        /// <summary>
        /// Defines the RepetitionCount
        /// </summary>
        internal int RepetitionCount;

        /// <summary>
        /// Defines the RepetitionInterval
        /// </summary>
        internal int RepetitionInterval;

        /// <summary>
        /// Defines the AckN0Count
        /// </summary>
        internal int AckN0Count;

        /// <summary>
        /// Defines the AckT0Interval
        /// </summary>
        internal int AckT0Interval;

        /// <summary>
        /// Defines the AckT1Interval
        /// </summary>
        internal int AckT1Interval;

        /// <summary>
        /// Defines the RecoveryN
        /// </summary>
        internal int RecoveryN;

        /// <summary>
        /// Defines the RecoveryT1
        /// </summary>
        internal int RecoveryT1;

        /// <summary>
        /// Defines the RecoveryT2
        /// </summary>
        internal int RecoveryT2;

        /// <summary>
        /// Defines the LatestTimer
        /// </summary>
        internal ulong LatestTimer;

        /// <summary>
        /// Defines the ExplicitAckEnabled
        /// </summary>
        internal bool ExplicitAckEnabled;

        /// <summary>
        /// Defines the RecoveryTimerT1
        /// </summary>
        internal ulong RecoveryTimerT1;

        /// <summary>
        /// Defines the RecoveryTimerT2
        /// </summary>
        internal ulong RecoveryTimerT2;

        /// <summary>
        /// Defines the RecoveryCounterN
        /// </summary>
        internal int RecoveryCounterN;

        /// <summary>
        /// Defines the RepetitionTimer
        /// </summary>
        internal ulong RepetitionTimer;

        /// <summary>
        /// Defines the RepetitionCounter
        /// </summary>
        internal int RepetitionCounter;

        /// <summary>
        /// Defines the AckTimerT0
        /// </summary>
        internal ulong AckTimerT0;

        /// <summary>
        /// Defines the AckTimerT1
        /// </summary>
        internal ulong AckTimerT1;

        /// <summary>
        /// Defines the AckCounterN0
        /// </summary>
        internal int AckCounterN0;

        /// <summary>
        /// Defines the ForceResponse
        /// </summary>
        internal bool ForceResponse;

        /// <summary>
        /// Defines the ImmediateTimer
        /// </summary>
        internal bool ImmediateTimer;

        /// <summary>
        /// Defines the LastTxPacket
        /// </summary>
        internal readonly byte[] LastTxPacket = new byte[V1501Sse.MaximumPacketLength];

        /// <summary>
        /// Defines the LastTxLength
        /// </summary>
        internal int LastTxLength;

        /// <summary>
        /// Defines the PreviousRxTimestamp
        /// </summary>
        internal uint PreviousRxTimestamp;

        /// <summary>
        /// Defines the PreviousRxSequenceNumber
        /// </summary>
        internal ushort PreviousRxSequenceNumber;

        /// <summary>
        /// Defines the TxPacketHandler
        /// </summary>
        internal V1501SseTransmitPacketHandler? TxPacketHandler;

        /// <summary>
        /// Defines the TxPacketUserData
        /// </summary>
        internal object? TxPacketUserData;

        /// <summary>
        /// Gets the SelectedReliabilityMethod
        /// </summary>
        public V1501SseReliabilityMethod SelectedReliabilityMethod => ReliabilityMethod;

        /// <summary>
        /// Gets the ScheduledTimer
        /// </summary>
        public ulong ScheduledTimer => LatestTimer;

        /// <summary>
        /// Gets the PendingRepetitions
        /// </summary>
        public int PendingRepetitions => RepetitionCounter;

        /// <summary>
        /// Gets the PendingAcknowledgementTransmissions
        /// </summary>
        public int PendingAcknowledgementTransmissions => AckCounterN0;

        /// <summary>
        /// Gets the LastReceivedTimestamp
        /// </summary>
        public uint LastReceivedTimestamp => PreviousRxTimestamp;

        /// <summary>
        /// Gets the LastReceivedSequenceNumber
        /// </summary>
        public ushort LastReceivedSequenceNumber => PreviousRxSequenceNumber;

        /// <summary>
        /// The ClearRuntimeState
        /// </summary>
        internal void ClearRuntimeState() {
            LatestTimer = 0;
            ExplicitAckEnabled = false;
            RecoveryTimerT1 = 0;
            RecoveryTimerT2 = 0;
            RecoveryCounterN = 0;
            RepetitionTimer = 0;
            RepetitionCounter = 0;
            AckTimerT0 = 0;
            AckTimerT1 = 0;
            AckCounterN0 = 0;
            ForceResponse = false;
            ImmediateTimer = false;
            Array.Clear(LastTxPacket, 0, LastTxPacket.Length);
            LastTxLength = 0;
            PreviousRxTimestamp = uint.MaxValue;
            PreviousRxSequenceNumber = 0;
        }
    }

    /// <summary>
    /// Minimal managed V.150.1 context required by the SSE module. The class is
    /// partial so the remaining v150_1.c state can be added in another file
    /// </summary>
    public sealed partial class V1501State {
        /// <summary>
        /// Initializes a new instance of the <see cref="V1501State"/> class.
        /// </summary>

        /// <summary>
        /// Gets or sets the CallDiscriminationSelection
        /// </summary>
        public V1501CallDiscriminationSelection CallDiscriminationSelection { get; set; }

        /// <summary>
        /// Gets or sets the LocalMediaState
        /// </summary>

        /// <summary>
        /// Gets or sets the RemoteMediaState
        /// </summary>

        /// <summary>
        /// Gets or sets the RemoteAcknowledgement
        /// </summary>

        /// <summary>
        /// Gets the Sse
        /// </summary>
        public V1501SseState Sse { get; } = new();

        /// <summary>
        /// Gets or sets the SseStatusCallback
        /// </summary>
        public V1501SseStatusDelegate? SseStatusCallback { get; set; }

        /// <summary>
        /// Gets or sets the SseStatusUserData
        /// </summary>
        public object? SseStatusUserData { get; set; }

        /// <summary>
        /// Gets or sets the StateMachineHandler
        /// </summary>
        public V1501StateMachineDelegate? StateMachineHandler { get; set; }

        /// <summary>
        /// Gets or sets the LogHandler
        /// </summary>
        public V1501LogDelegate? LogHandler { get; set; }

        // Native-name aliases for straightforward source migration.

        /// <summary>
        /// Gets or sets the cdscselect
        /// </summary>
        public int cdscselect { get => (int)CallDiscriminationSelection; set => CallDiscriminationSelection = (V1501CallDiscriminationSelection)value; }

        /// <summary>
        /// Gets or sets the local_media_state
        /// </summary>
        public byte local_media_state { get => (byte)LocalMediaState; set => LocalMediaState = (V1501MediaState)value; }

        /// <summary>
        /// Gets or sets the remote_media_state
        /// </summary>
        public byte remote_media_state { get => (byte)RemoteMediaState; set => RemoteMediaState = (V1501MediaState)value; }

        /// <summary>
        /// Gets or sets the remote_ack
        /// </summary>
        public byte remote_ack { get => (byte)RemoteAcknowledgement; set => RemoteAcknowledgement = (V1501MediaState)value; }

        /// <summary>
        /// The RunStateMachine
        /// </summary>
        /// <param name="signal">The signal<see cref="int"/></param>
        /// <param name="message">The message<see cref="byte[]?"/></param>
        /// <param name="length">The length<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        internal int RunStateMachine(int signal, byte[]? message, int length) {
            return StateMachineHandler?.Invoke(this, signal, message, length) ?? 0;
        }

        /// <summary>
        /// The ReportSseStatus
        /// </summary>
        /// <param name="status">The status<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        internal int ReportSseStatus(int status) {
            return SseStatusCallback?.Invoke(SseStatusUserData, status) ?? 0;
        }

        /// <summary>
        /// The UpdateSseSchedule
        /// </summary>
        /// <param name="timeout">The timeout<see cref="ulong"/></param>
        /// <returns>The <see cref="ulong"/></returns>
        internal ulong UpdateSseSchedule(ulong timeout) {
            return UpdateSseTimer(timeout);
        }

        /// <summary>
        /// The Log
        /// </summary>
        /// <param name="message">The message<see cref="string"/></param>
        internal void Log(string message) {
            LogHandler?.Invoke(message);
        }

    }

    /// <summary>
    /// Managed implementation of V.150.1 Annex C State Signalling Events
    /// </summary>
    public static class V1501Sse {
        /// <summary>
        /// Defines the DefaultRepetitions
        /// </summary>
        public const int DefaultRepetitions = 3;

        /// <summary>
        /// Defines the DefaultRepetitionInterval
        /// </summary>
        public const int DefaultRepetitionInterval = 20_000;

        /// <summary>
        /// Defines the DefaultAckN0
        /// </summary>
        public const int DefaultAckN0 = 3;

        /// <summary>
        /// Defines the DefaultAckT0
        /// </summary>
        public const int DefaultAckT0 = 10_000;

        /// <summary>
        /// Defines the DefaultAckT1
        /// </summary>
        public const int DefaultAckT1 = 300_000;

        /// <summary>
        /// Defines the DefaultRecoveryN
        /// </summary>
        public const int DefaultRecoveryN = 5;

        /// <summary>
        /// Defines the DefaultRecoveryT1
        /// </summary>
        public const int DefaultRecoveryT1 = 1_000_000;

        /// <summary>
        /// Defines the DefaultRecoveryT2
        /// </summary>
        public const int DefaultRecoveryT2 = 1_000_000;

        /// <summary>
        /// Defines the MaximumPacketLength
        /// </summary>
        public const int MaximumPacketLength = 256;

        /// <summary>
        /// The MediaStateToString
        /// </summary>
        /// <param name="state">The state<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string MediaStateToString(int state) {
            return state switch {
                (int)V1501MediaState.ItuReserved0 => "ITU reserved",
                (int)V1501MediaState.InitialAudio => "initial audio",
                (int)V1501MediaState.VoiceBandData => "voice band data",
                (int)V1501MediaState.ModemRelay => "modem relay",
                (int)V1501MediaState.FaxRelay => "fax relay",
                (int)V1501MediaState.TextRelay => "text relay",
                (int)V1501MediaState.TextProbe => "text probe",
                (int)V1501MediaState.Indeterminate => "indeterminate",
                >= (int)V1501MediaState.ItuReservedMinimum and <= (int)V1501MediaState.ItuReservedMaximum
                    => "ITU reserved",
                >= (int)V1501MediaState.VendorReservedMinimum and <= (int)V1501MediaState.VendorReservedMaximum
                    => "vendor reserved",
                _ => "unknown"
            };
        }

        /// <summary>
        /// The MoipRicToString
        /// </summary>
        /// <param name="ric">The ric<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string MoipRicToString(int ric) {
            return ric switch {
                (int)V1501SseMoipRic.V8Cm => "V.8 CM",
                (int)V1501SseMoipRic.V8Jm => "V.8 JM",
                (int)V1501SseMoipRic.V32BisAa => "V.32/V.32bis AA",
                (int)V1501SseMoipRic.V32BisAc => "V.32/V.32bis AC",
                (int)V1501SseMoipRic.V22BisUsb1 => "V.22bis USB1",
                (int)V1501SseMoipRic.V22BisSb1 => "V.22bis SB1",
                (int)V1501SseMoipRic.V22BisS1 => "V.22bis S1",
                (int)V1501SseMoipRic.V21Channel2 => "V.21 Ch2",
                (int)V1501SseMoipRic.V21Channel1 => "V.21 Ch1",
                (int)V1501SseMoipRic.V23HighChannel => "V.23 high channel",
                (int)V1501SseMoipRic.V23LowChannel => "V.23 low channel",
                (int)V1501SseMoipRic.Tone2225Hz => "2225Hz tone",
                (int)V1501SseMoipRic.V21Channel2HdlcFlags => "V.21 Ch2 HDLC flags",
                (int)V1501SseMoipRic.IndeterminateSignal => "Indeterminate signal",
                (int)V1501SseMoipRic.Silence => "Silence",
                (int)V1501SseMoipRic.Cng => "CNG",
                (int)V1501SseMoipRic.Voice => "Voice",
                (int)V1501SseMoipRic.Timeout => "Time-out",
                (int)V1501SseMoipRic.PStateTransition => "P' state transition",
                (int)V1501SseMoipRic.Cleardown => "Cleardown",
                (int)V1501SseMoipRic.AnsCed => "CED",
                (int)V1501SseMoipRic.Ansam => "ANSam",
                (int)V1501SseMoipRic.AnsPhaseReversal => "/ANS",
                (int)V1501SseMoipRic.AnsamPhaseReversal => "/ANSam",
                (int)V1501SseMoipRic.V92Qc1A => "V.92 QC1a",
                (int)V1501SseMoipRic.V92Qc1D => "V.92 QC1d",
                (int)V1501SseMoipRic.V92Qc2A => "V.92 QC2a",
                (int)V1501SseMoipRic.V92Qc2D => "V.92 QC2d",
                (int)V1501SseMoipRic.V8BisCre => "V.8bis Cre",
                (int)V1501SseMoipRic.V8BisCrd => "V.8bis CRd",
                (int)V1501SseMoipRic.Tia825A4545Bps => "TIA825A 45.45BPS",
                (int)V1501SseMoipRic.Tia825A50Bps => "TIA825A 50BPS",
                (int)V1501SseMoipRic.Edt => "EDT",
                (int)V1501SseMoipRic.Bell103 => "Bell 103",
                (int)V1501SseMoipRic.V21TextTelephone => "Text telephone",
                (int)V1501SseMoipRic.V23Minitel => "V.23 Minitel",
                (int)V1501SseMoipRic.V18TextTelephone => "Text telephone",
                (int)V1501SseMoipRic.V18DtmfTextRelay => "Text relay",
                (int)V1501SseMoipRic.Ctm => "CTM",
                _ => "unknown"
            };
        }

        /// <summary>
        /// The TimeoutReasonToString
        /// </summary>
        /// <param name="reason">The reason<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string TimeoutReasonToString(int reason) {
            return reason switch {
                (int)V1501SseTimeoutReason.Null => "NULL",
                (int)V1501SseTimeoutReason.CallDiscriminationTimeout => "Call discrimination timeout",
                (int)V1501SseTimeoutReason.IpTlp => "IP-TLP",
                (int)V1501SseTimeoutReason.ExplicitAcknowledgementTimeout
                    => "TSSE explicit acknowledgement timeout",
                _ => "unknown"
            };
        }

        /// <summary>
        /// The CleardownReasonToString
        /// </summary>
        /// <param name="reason">The reason<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string CleardownReasonToString(int reason) {
            return reason switch {
                (int)V1501SseCleardownReason.Unknown => "Unknown/unspecified",
                (int)V1501SseCleardownReason.PhysicalLayerRelease => "Physical Layer Release",
                (int)V1501SseCleardownReason.LinkLayerDisconnect => "Link Layer Disconnect",
                (int)V1501SseCleardownReason.CompressionDisconnect => "Data compression disconnect",
                (int)V1501SseCleardownReason.Abort => "Abort",
                (int)V1501SseCleardownReason.OnHook => "On-hook",
                (int)V1501SseCleardownReason.NetworkLayerTermination => "Network layer termination",
                (int)V1501SseCleardownReason.Administrative => "Administrative",
                _ => "unknown"
            };
        }

        /// <summary>
        /// The StatusToString
        /// </summary>
        /// <param name="status">The status<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string StatusToString(int status) {
            return status switch {
                (int)V1501SseStatus.V8CmReceived => "V.8 CM received",
                (int)V1501SseStatus.V8JmReceived => "V.8 JM received",
                (int)V1501SseStatus.AaReceived => "V.32 AA received",
                (int)V1501SseStatus.V8CmReceivedFax => "Fax V.8 CM received",
                (int)V1501SseStatus.V8JmReceivedFax => "Fax V.8 JM received",
                (int)V1501SseStatus.AaReceivedFax => "Fax AA received",
                (int)V1501SseStatus.Cleardown => "cleardown",
                _ => "unknown"
            };
        }

        /// <summary>
        /// The Initialize
        /// </summary>
        /// <param name="state">The state<see cref="V1501State"/></param>
        /// <param name="txPacketHandler">The txPacketHandler<see cref="V1501SseTransmitPacketHandler?"/></param>
        /// <param name="txPacketUserData">The txPacketUserData<see cref="object?"/></param>
        public static void Initialize(
            V1501State state,
            V1501SseTransmitPacketHandler? txPacketHandler,
            object? txPacketUserData) {
            ArgumentNullException.ThrowIfNull(state);

            V1501SseState sse = state.Sse;
            sse.ClearRuntimeState();
            sse.ReliabilityMethod = V1501SseReliabilityMethod.None;

            sse.RepetitionCount = DefaultRepetitions - 1;
            sse.RepetitionInterval = DefaultRepetitionInterval;

            sse.AckN0Count = DefaultAckN0;
            sse.AckT0Interval = DefaultAckT0;
            sse.AckT1Interval = DefaultAckT1;

            sse.RecoveryN = DefaultRecoveryN;
            sse.RecoveryT1 = DefaultRecoveryT1;
            sse.RecoveryT2 = DefaultRecoveryT2;

            sse.TxPacketHandler = txPacketHandler;
            sse.TxPacketUserData = txPacketUserData;
        }

        /// <summary>
        /// The SetReliabilityMethod
        /// </summary>
        /// <param name="state">The state<see cref="V1501State"/></param>
        /// <param name="method">The method<see cref="V1501SseReliabilityMethod"/></param>
        /// <param name="parameter1">The parameter1<see cref="int"/></param>
        /// <param name="parameter2">The parameter2<see cref="int"/></param>
        /// <param name="parameter3">The parameter3<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int SetReliabilityMethod(
            V1501State state,
            V1501SseReliabilityMethod method,
            int parameter1,
            int parameter2,
            int parameter3) {
            ArgumentNullException.ThrowIfNull(state);
            V1501SseState sse = state.Sse;

            switch (method) {
                case V1501SseReliabilityMethod.None:
                    break;

                case V1501SseReliabilityMethod.Repetition:
                    if (parameter1 is < 2 or > 10)
                        return -1;
                    if (parameter2 is < 10_000 or > 1_000_000)
                        return -1;

                    sse.RepetitionCount = parameter1 - 1;
                    sse.RepetitionInterval = parameter2;
                    break;

                case V1501SseReliabilityMethod.Rfc2198:
                    break;

                case V1501SseReliabilityMethod.ExplicitAcknowledgement:
                    if (parameter1 is < 2 or > 10)
                        return -1;
                    if (parameter2 is < 10_000 or > 1_000_000)
                        return -1;
                    if (parameter3 is < 10_000 or > 1_000_000)
                        return -1;

                    sse.AckN0Count = parameter1;
                    sse.AckT0Interval = parameter2;
                    sse.AckT1Interval = parameter3;
                    break;

                default:
                    return -1;
            }

            sse.ReliabilityMethod = method;
            sse.ExplicitAckEnabled = method == V1501SseReliabilityMethod.ExplicitAcknowledgement;
            return 0;
        }

        /// <summary>
        /// The ReceivePacket
        /// </summary>
        /// <param name="state">The state<see cref="V1501State"/></param>
        /// <param name="sequenceNumber">The sequenceNumber<see cref="ushort"/></param>
        /// <param name="timestamp">The timestamp<see cref="uint"/></param>
        /// <param name="packet">The packet<see cref="byte[]"/></param>
        /// <param name="length">The length<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int ReceivePacket(
            V1501State state,
            ushort sequenceNumber,
            uint timestamp,
            byte[] packet,
            int length) {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(packet);
            ValidatePacketLength(packet, length);

            V1501SseState sse = state.Sse;
            state.Log($"Rx message - {length} bytes");

            if (length < 4)
                return -1;

            if (sse.PreviousRxTimestamp == timestamp) {
                state.Log($"Repeat SSE timestamp {timestamp}");
                return 0;
            }

            sse.PreviousRxTimestamp = timestamp;
            sse.PreviousRxSequenceNumber = sequenceNumber;

            int eventCode = (packet[0] >> 2) & 0x3F;
            bool forceResponse = ((packet[0] >> 1) & 0x01) != 0;
            bool hasExtension = (packet[0] & 0x01) != 0;
            state.Log($"Rx SSE event {MediaStateToString(eventCode)}");

            if (hasExtension) {
                if (length < 6) {
                    state.Log("Malformed SSE extension header");
                    return -1;
                }

                int extensionLength = ReadNetworkUInt16(packet, 4) & 0x07FF;
                if (extensionLength > length - 6) {
                    state.Log($"Malformed SSE extension length {extensionLength}");
                    return -1;
                }

                if (extensionLength >= 1)
                    state.RemoteAcknowledgement = (V1501MediaState)(packet[6] & 0x3F);
            } else if (length != 4) {
                state.Log($"Non-extended message of length {length}");
            }

            state.RunStateMachine(eventCode, packet, length);

            int result = eventCode switch {
                (int)V1501MediaState.InitialAudio => ReceiveInitialAudioPacket(state),
                (int)V1501MediaState.VoiceBandData => ReceiveVoiceBandDataPacket(state),
                (int)V1501MediaState.ModemRelay => ReceiveModemRelayPacket(state, packet, forceResponse),
                (int)V1501MediaState.FaxRelay => ReceiveFaxRelayPacket(state, packet, forceResponse),
                (int)V1501MediaState.TextRelay => ReceiveTextRelayPacket(state, packet, forceResponse),
                (int)V1501MediaState.TextProbe => ReceiveTextProbePacket(state, packet, forceResponse),
                _ => UnexpectedReceiveEvent(state, eventCode)
            };

            state.RemoteMediaState = (V1501MediaState)eventCode;
            return result;
        }

        /// <summary>
        /// The TransmitPacket
        /// </summary>
        /// <param name="state">The state<see cref="V1501State"/></param>
        /// <param name="eventCode">The eventCode<see cref="int"/></param>
        /// <param name="ric">The ric<see cref="int"/></param>
        /// <param name="ricInformation">The ricInformation<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int TransmitPacket(
            V1501State state,
            int eventCode,
            int ric,
            int ricInformation) {
            ArgumentNullException.ThrowIfNull(state);
            state.Log($"Tx event {MediaStateToString(eventCode)}");

            int result;
            switch (eventCode) {
                case (int)V1501MediaState.InitialAudio:
                case (int)V1501MediaState.VoiceBandData:
                case (int)V1501MediaState.ModemRelay:
                case (int)V1501MediaState.FaxRelay:
                case (int)V1501MediaState.TextRelay:
                case (int)V1501MediaState.TextProbe:
                    result = BuildAndSendPacket(state, eventCode, ric, ricInformation);
                    break;

                default:
                    state.Log($"Unexpected SSE event {eventCode}");
                    result = -1;
                    break;
            }

            state.LocalMediaState = (V1501MediaState)eventCode;
            return result;
        }

        /// <summary>
        /// The TimerExpired
        /// </summary>
        /// <param name="state">The state<see cref="V1501State"/></param>
        /// <param name="now">The now<see cref="ulong"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int TimerExpired(V1501State state, ulong now) {
            ArgumentNullException.ThrowIfNull(state);
            V1501SseState sse = state.Sse;
            state.Log($"SSE timer expired at {now}");

            if (now < sse.LatestTimer) {
                state.Log($"SSE timer returned {sse.LatestTimer - now}us early");
                state.UpdateSseSchedule(sse.LatestTimer);
                return 0;
            }

            if (sse.ImmediateTimer) {
                sse.ImmediateTimer = false;
                // The native source leaves immediate-timer processing as TODO.
            }

            if (sse.AckTimerT0 != 0 && sse.AckTimerT0 <= now) {
                state.Log("SSE T0 expired");

                if (sse.AckCounterN0 > 0 && state.LocalMediaState != state.RemoteAcknowledgement) {
                    state.Log($"SSE resend ({sse.AckCounterN0})");
                    SendSavedPacket(sse, true);
                    sse.AckCounterN0--;
                    sse.AckTimerT0 = AddTimestamp(now, sse.AckT0Interval);
                    UpdateTimer(state);
                }
            }

            if (sse.AckTimerT1 != 0 && sse.AckTimerT1 <= now) {
                state.Log("SSE T1 expired");

                if (sse.AckCounterN0 == 0 && state.LocalMediaState != state.RemoteAcknowledgement) {
                    state.Log($"SSE resend ({sse.AckCounterN0})");
                    SendSavedPacket(sse, true);
                    sse.AckTimerT1 = AddTimestamp(now, sse.AckT1Interval);
                    UpdateTimer(state);
                }
            }

            if (sse.RepetitionTimer != 0 && sse.RepetitionTimer <= now) {
                state.Log("SSE repetition timer expired");

                if (sse.RepetitionCounter > 1) {
                    sse.RepetitionTimer = AddTimestamp(sse.RepetitionTimer, sse.RepetitionInterval);
                    UpdateTimer(state);
                } else {
                    sse.RepetitionTimer = 0;
                }

                sse.RepetitionCounter--;
                SendSavedPacket(sse, true);
            }

            if (sse.RecoveryTimerT1 != 0 && sse.RecoveryTimerT1 <= now) {
                // The native source leaves recovery timer T1 processing empty.
            }

            if (sse.RecoveryTimerT2 != 0 && sse.RecoveryTimerT2 <= now) {
                // The native source leaves recovery timer T2 processing empty.
            }

            return 0;
        }

        /// <summary>
        /// The ReceiveInitialAudioPacket
        /// </summary>
        /// <param name="state">The state<see cref="V1501State"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int ReceiveInitialAudioPacket(V1501State state) {
            if (state.RemoteMediaState != V1501MediaState.InitialAudio) {
                state.LocalMediaState = V1501MediaState.InitialAudio;
                state.RemoteMediaState = V1501MediaState.InitialAudio;
            }

            return 0;
        }

        /// <summary>
        /// The ReceiveVoiceBandDataPacket
        /// </summary>
        /// <param name="state">The state<see cref="V1501State"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int ReceiveVoiceBandDataPacket(V1501State state) {
            if (state.RemoteMediaState != V1501MediaState.VoiceBandData) {
                state.LocalMediaState = V1501MediaState.VoiceBandData;
                state.RemoteMediaState = V1501MediaState.VoiceBandData;
            }

            return 0;
        }

        /// <summary>
        /// The ReceiveModemRelayPacket
        /// </summary>
        /// <param name="state">The state<see cref="V1501State"/></param>
        /// <param name="packet">The packet<see cref="byte[]"/></param>
        /// <param name="forceResponse">The forceResponse<see cref="bool"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int ReceiveModemRelayPacket(
            V1501State state,
            byte[] packet,
            bool forceResponse) {
            int ric = packet[1];
            int ricInformation = ReadNetworkUInt16(packet, 2);
            LogReceivedReason(state, ric, ricInformation, forceResponse, includeSsePrefix: false);

            if (state.RemoteMediaState != V1501MediaState.ModemRelay) {
                if (state.CallDiscriminationSelection is
                    V1501CallDiscriminationSelection.VbdPreferred or
                    V1501CallDiscriminationSelection.Mixed) {
                    state.LocalMediaState = V1501MediaState.VoiceBandData;
                    state.RemoteMediaState = V1501MediaState.VoiceBandData;
                } else {
                    state.LocalMediaState = V1501MediaState.ModemRelay;
                    state.RemoteMediaState = V1501MediaState.ModemRelay;
                }
            }

            return HandleRelayRic(state, ric, ricInformation, faxRelay: false);
        }

        /// <summary>
        /// The ReceiveFaxRelayPacket
        /// </summary>
        /// <param name="state">The state<see cref="V1501State"/></param>
        /// <param name="packet">The packet<see cref="byte[]"/></param>
        /// <param name="forceResponse">The forceResponse<see cref="bool"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int ReceiveFaxRelayPacket(
            V1501State state,
            byte[] packet,
            bool forceResponse) {
            int ric = packet[1];
            int ricInformation = ReadNetworkUInt16(packet, 2);
            LogReceivedReason(state, ric, ricInformation, forceResponse, includeSsePrefix: true);

            if (state.RemoteMediaState != V1501MediaState.FaxRelay) {
                state.LocalMediaState = V1501MediaState.FaxRelay;
                state.RemoteMediaState = V1501MediaState.FaxRelay;
            }

            return HandleRelayRic(state, ric, ricInformation, faxRelay: true);
        }

        /// <summary>
        /// The HandleRelayRic
        /// </summary>
        /// <param name="state">The state<see cref="V1501State"/></param>
        /// <param name="ric">The ric<see cref="int"/></param>
        /// <param name="ricInformation">The ricInformation<see cref="int"/></param>
        /// <param name="faxRelay">The faxRelay<see cref="bool"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int HandleRelayRic(
            V1501State state,
            int ric,
            int ricInformation,
            bool faxRelay) {
            int result = 0;
            V1501MediaState responseState = faxRelay
                ? V1501MediaState.FaxRelay
                : V1501MediaState.ModemRelay;

            switch (ric) {
                case (int)V1501SseMoipRic.V8Cm:
                    state.Log(faxRelay ? "Switch on V.8 detection" : "Switch on V.8 (CM) detection");
                    if (!faxRelay)
                        LogV8RicInformation(state, ricInformation);
                    BuildAndSendPacket(state, (int)responseState, (int)V1501SseMoipRic.PStateTransition, 0);
                    result = state.ReportSseStatus(
                        faxRelay
                            ? (int)V1501SseStatus.V8CmReceivedFax
                            : (int)V1501SseStatus.V8CmReceived);
                    break;

                case (int)V1501SseMoipRic.V8Jm:
                    state.Log(faxRelay ? "Switch on V.8 detection" : "Switch on V.8 (JM) detection");
                    if (!faxRelay) {
                        LogV8RicInformation(state, ricInformation);
                        result = state.ReportSseStatus((int)V1501SseStatus.V8JmReceived);
                    }
                    // The native fax-relay path defines but does not emit the
                    // V8JmReceivedFax status. This port retains that behavior.
                    break;

                case (int)V1501SseMoipRic.V32BisAa:
                    state.Log("Switch on V.32bis detection");
                    BuildAndSendPacket(state, (int)responseState, (int)V1501SseMoipRic.PStateTransition, 0);
                    result = state.ReportSseStatus(
                        faxRelay
                            ? (int)V1501SseStatus.AaReceivedFax
                            : (int)V1501SseStatus.AaReceived);
                    break;

                case (int)V1501SseMoipRic.V32BisAc:
                    state.Log("Switch on V.32bis detection");
                    break;

                case (int)V1501SseMoipRic.V22BisUsb1:
                case (int)V1501SseMoipRic.V22BisSb1:
                case (int)V1501SseMoipRic.V22BisS1:
                    state.Log("Switch on V.22bis detection");
                    break;

                case (int)V1501SseMoipRic.V21Channel2:
                case (int)V1501SseMoipRic.V21Channel1:
                    state.Log("Switch on V.21 detection");
                    break;

                case (int)V1501SseMoipRic.V23HighChannel:
                case (int)V1501SseMoipRic.V23LowChannel:
                    state.Log("Switch on V.23 detection");
                    break;

                case (int)V1501SseMoipRic.Tone2225Hz:
                    state.Log("Switch on 2225Hz tone detection");
                    break;

                case (int)V1501SseMoipRic.V21Channel2HdlcFlags:
                    state.Log("Switch on V.21 flags detection");
                    break;

                case (int)V1501SseMoipRic.Cng:
                    state.Log("Switch on CNG detection");
                    break;

                case (int)V1501SseMoipRic.Voice:
                    state.Log("Switch on voice detection");
                    break;

                case (int)V1501SseMoipRic.Timeout:
                    LogTimeout(state, ricInformation);
                    break;

                case (int)V1501SseMoipRic.PStateTransition:
                    state.Log("P' received");
                    break;

                case (int)V1501SseMoipRic.Cleardown:
                    LogCleardown(state, ricInformation);
                    result = state.ReportSseStatus((int)V1501SseStatus.Cleardown);
                    break;

                case (int)V1501SseMoipRic.AnsCed:
                    state.Log("Switch on ANS/CED detection");
                    break;

                case (int)V1501SseMoipRic.Ansam:
                    state.Log("Switch on ANSam detection");
                    break;

                case (int)V1501SseMoipRic.AnsPhaseReversal:
                    state.Log("Switch on /ANS detection");
                    break;

                case (int)V1501SseMoipRic.AnsamPhaseReversal:
                    state.Log("Switch on /ANSam detection");
                    break;

                case (int)V1501SseMoipRic.Bell103:
                    state.Log("Switch on Bell103 detection");
                    break;

                case (int)V1501SseMoipRic.V21TextTelephone:
                    state.Log("Switch on V.21 text telephone detection");
                    break;

                case (int)V1501SseMoipRic.V23Minitel:
                    state.Log("Switch on V.23 minitel detection");
                    break;

                case (int)V1501SseMoipRic.V18DtmfTextRelay:
                    state.Log("Switch on DTMF text relay detection");
                    break;
            }

            return result;
        }

        /// <summary>
        /// The ReceiveTextRelayPacket
        /// </summary>
        /// <param name="state">The state<see cref="V1501State"/></param>
        /// <param name="packet">The packet<see cref="byte[]"/></param>
        /// <param name="forceResponse">The forceResponse<see cref="bool"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int ReceiveTextRelayPacket(
            V1501State state,
            byte[] packet,
            bool forceResponse) {
            int ric = packet[1];
            int ricInformation = ReadNetworkUInt16(packet, 2);
            LogReceivedReason(state, ric, ricInformation, forceResponse, includeSsePrefix: true);

            if (state.RemoteMediaState != V1501MediaState.TextRelay) {
                state.LocalMediaState = V1501MediaState.TextRelay;
                state.RemoteMediaState = V1501MediaState.TextRelay;
            }

            switch (ric) {
                case (int)V1501SseMoipRic.Timeout:
                    LogTimeout(state, ricInformation);
                    break;
                case (int)V1501SseMoipRic.Bell103:
                    state.Log("Switch on Bell103 detection");
                    break;
                case (int)V1501SseMoipRic.V21TextTelephone:
                    state.Log("Switch on V.21 text telephone detection");
                    break;
                case (int)V1501SseMoipRic.V23Minitel:
                    state.Log("Switch on V.23 minitel detection");
                    break;
                case (int)V1501SseMoipRic.V18DtmfTextRelay:
                    state.Log("Switch on DTMF text relay detection");
                    break;
            }

            return 0;
        }

        /// <summary>
        /// The ReceiveTextProbePacket
        /// </summary>
        /// <param name="state">The state<see cref="V1501State"/></param>
        /// <param name="packet">The packet<see cref="byte[]"/></param>
        /// <param name="forceResponse">The forceResponse<see cref="bool"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int ReceiveTextProbePacket(
            V1501State state,
            byte[] packet,
            bool forceResponse) {
            int ric = packet[1];
            int ricInformation = ReadNetworkUInt16(packet, 2);
            LogReceivedReason(state, ric, ricInformation, forceResponse, includeSsePrefix: true);

            // This follows the native source, which moves a received text-probe
            // event to the text-relay state.
            if (state.RemoteMediaState != V1501MediaState.TextRelay) {
                state.LocalMediaState = V1501MediaState.TextRelay;
                state.RemoteMediaState = V1501MediaState.TextRelay;
            }

            if (ric == (int)V1501SseMoipRic.Timeout)
                LogTimeout(state, ricInformation);

            return 0;
        }

        /// <summary>
        /// The BuildAndSendPacket
        /// </summary>
        /// <param name="state">The state<see cref="V1501State"/></param>
        /// <param name="eventCode">The eventCode<see cref="int"/></param>
        /// <param name="ric">The ric<see cref="int"/></param>
        /// <param name="ricInformation">The ricInformation<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int BuildAndSendPacket(
            V1501State state,
            int eventCode,
            int ric,
            int ricInformation) {
            V1501SseState sse = state.Sse;
            byte[] packet = new byte[MaximumPacketLength];
            byte flags = 0;

            if (sse.ReliabilityMethod == V1501SseReliabilityMethod.ExplicitAcknowledgement) {
                flags |= 0x01;
                if (sse.ForceResponse)
                    flags |= 0x02;
            }

            state.Log($"Sending {MoipRicToString(ric)}");
            packet[0] = unchecked((byte)((eventCode << 2) | flags));
            packet[1] = unchecked((byte)ric);
            WriteNetworkUInt16(packet, 2, unchecked((ushort)ricInformation));
            int length = 4;

            if (sse.ReliabilityMethod == V1501SseReliabilityMethod.ExplicitAcknowledgement) {
                WriteNetworkUInt16(packet, length, 1);
                length += 2;
                packet[length++] = (byte)state.RemoteMediaState;
            }

            return SendPacket(state, packet, length);
        }

        /// <summary>
        /// The SendPacket
        /// </summary>
        /// <param name="state">The state<see cref="V1501State"/></param>
        /// <param name="packet">The packet<see cref="byte[]"/></param>
        /// <param name="length">The length<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int SendPacket(V1501State state, byte[] packet, int length) {
            V1501SseState sse = state.Sse;
            sse.TxPacketHandler?.Invoke(sse.TxPacketUserData, false, packet.AsSpan(0, length));

            switch (sse.ReliabilityMethod) {
                case V1501SseReliabilityMethod.Repetition: {
                        SavePacket(sse, packet, length);
                        ulong now = state.UpdateSseSchedule(ulong.MaxValue);
                        sse.RepetitionTimer = AddTimestamp(now, sse.RepetitionInterval);
                        sse.RepetitionCounter = sse.RepetitionCount;
                        UpdateTimer(state);
                        break;
                    }

                case V1501SseReliabilityMethod.ExplicitAcknowledgement: {
                        SavePacket(sse, packet, length);
                        ulong now = state.UpdateSseSchedule(ulong.MaxValue);
                        sse.AckCounterN0 = sse.AckN0Count;
                        sse.AckTimerT0 = AddTimestamp(now, sse.AckT0Interval);
                        sse.AckTimerT1 = AddTimestamp(now, sse.AckT1Interval);
                        sse.ForceResponse = false;
                        UpdateTimer(state);
                        break;
                    }
            }

            return 0;
        }

        /// <summary>
        /// The UpdateTimer
        /// </summary>
        /// <param name="state">The state<see cref="V1501State"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int UpdateTimer(V1501State state) {
            V1501SseState sse = state.Sse;
            ulong shortest;
            int shortestIs;

            if (sse.ImmediateTimer) {
                shortest = 1;
                shortestIs = 4;
            } else {
                shortest = ulong.MaxValue;
                shortestIs = -1;

                SelectEarlier(sse.AckTimerT0, 0, ref shortest, ref shortestIs);
                SelectEarlier(sse.AckTimerT1, 1, ref shortest, ref shortestIs);
                SelectEarlier(sse.RepetitionTimer, 2, ref shortest, ref shortestIs);
                SelectEarlier(sse.RecoveryTimerT1, 3, ref shortest, ref shortestIs);
                SelectEarlier(sse.RecoveryTimerT2, 4, ref shortest, ref shortestIs);

                if (shortest == ulong.MaxValue)
                    shortest = 0;
            }

            state.Log($"Update timer to {shortest} ({shortestIs})");
            sse.LatestTimer = shortest;
            state.UpdateSseSchedule(shortest);
            return 0;
        }

        /// <summary>
        /// The SelectEarlier
        /// </summary>
        /// <param name="candidate">The candidate<see cref="ulong"/></param>
        /// <param name="candidateId">The candidateId<see cref="int"/></param>
        /// <param name="shortest">The shortest<see cref="ulong"/></param>
        /// <param name="shortestId">The shortestId<see cref="int"/></param>
        private static void SelectEarlier(
            ulong candidate,
            int candidateId,
            ref ulong shortest,
            ref int shortestId) {
            if (candidate != 0 && candidate < shortest) {
                shortest = candidate;
                shortestId = candidateId;
            }
        }

        /// <summary>
        /// The SavePacket
        /// </summary>
        /// <param name="sse">The sse<see cref="V1501SseState"/></param>
        /// <param name="packet">The packet<see cref="byte[]"/></param>
        /// <param name="length">The length<see cref="int"/></param>
        private static void SavePacket(V1501SseState sse, byte[] packet, int length) {
            Array.Clear(sse.LastTxPacket, 0, sse.LastTxPacket.Length);
            Buffer.BlockCopy(packet, 0, sse.LastTxPacket, 0, length);
            sse.LastTxLength = length;
        }

        /// <summary>
        /// The SendSavedPacket
        /// </summary>
        /// <param name="sse">The sse<see cref="V1501SseState"/></param>
        /// <param name="repeat">The repeat<see cref="bool"/></param>
        private static void SendSavedPacket(V1501SseState sse, bool repeat) {
            if (sse.LastTxLength <= 0)
                return;

            sse.TxPacketHandler?.Invoke(
                sse.TxPacketUserData,
                repeat,
                sse.LastTxPacket.AsSpan(0, sse.LastTxLength));
        }

        /// <summary>
        /// The UnexpectedReceiveEvent
        /// </summary>
        /// <param name="state">The state<see cref="V1501State"/></param>
        /// <param name="eventCode">The eventCode<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int UnexpectedReceiveEvent(V1501State state, int eventCode) {
            state.Log($"Unexpected SSE event {eventCode}");
            return -1;
        }

        /// <summary>
        /// The LogReceivedReason
        /// </summary>
        /// <param name="state">The state<see cref="V1501State"/></param>
        /// <param name="ric">The ric<see cref="int"/></param>
        /// <param name="ricInformation">The ricInformation<see cref="int"/></param>
        /// <param name="forceResponse">The forceResponse<see cref="bool"/></param>
        /// <param name="includeSsePrefix">The includeSsePrefix<see cref="bool"/></param>
        private static void LogReceivedReason(
            V1501State state,
            int ric,
            int ricInformation,
            bool forceResponse,
            bool includeSsePrefix) {
            string prefix = includeSsePrefix ? "SSE " : string.Empty;
            string forced = forceResponse ? "Force response. " : string.Empty;
            state.Log($"{prefix}{forced}Reason {MoipRicToString(ric)} - 0x{ricInformation:X}");
        }

        /// <summary>
        /// The LogTimeout
        /// </summary>
        /// <param name="state">The state<see cref="V1501State"/></param>
        /// <param name="ricInformation">The ricInformation<see cref="int"/></param>
        private static void LogTimeout(V1501State state, int ricInformation) {
            int reason = ricInformation >> 8;
            state.Log(
                $"Timeout {reason} - {TimeoutReasonToString(reason)} - 0x{ricInformation & 0xFF:X}");
        }

        /// <summary>
        /// The LogCleardown
        /// </summary>
        /// <param name="state">The state<see cref="V1501State"/></param>
        /// <param name="ricInformation">The ricInformation<see cref="int"/></param>
        private static void LogCleardown(V1501State state, int ricInformation) {
            int reason = ricInformation >> 8;
            state.Log($"Cleardown {reason} - {CleardownReasonToString(reason)}");
        }

        /// <summary>
        /// The LogV8RicInformation
        /// </summary>
        /// <param name="state">The state<see cref="V1501State"/></param>
        /// <param name="ricInformation">The ricInformation<see cref="int"/></param>
        private static void LogV8RicInformation(V1501State state, int ricInformation) {
            V1501SseV8Capability capabilities = (V1501SseV8Capability)ricInformation;

            LogCapability(state, capabilities, V1501SseV8Capability.PcmMode, "PCM mode");
            LogCapability(state, capabilities, V1501SseV8Capability.V34Duplex, "V.34 duplex");
            LogCapability(state, capabilities, V1501SseV8Capability.V34HalfDuplex, "V.34 half duplex");
            LogCapability(state, capabilities, V1501SseV8Capability.V32Bis, "V.32/V32.bis");
            LogCapability(state, capabilities, V1501SseV8Capability.V22Bis, "V.22/V22.bis");
            LogCapability(state, capabilities, V1501SseV8Capability.V17, "V.17");
            LogCapability(state, capabilities, V1501SseV8Capability.V29, "V.29 half-duplex");
            LogCapability(state, capabilities, V1501SseV8Capability.V27Ter, "V.27ter");
            LogCapability(state, capabilities, V1501SseV8Capability.V26Ter, "V.26ter");
            LogCapability(state, capabilities, V1501SseV8Capability.V26Bis, "V.26bis");
            LogCapability(state, capabilities, V1501SseV8Capability.V23Duplex, "V.23 duplex");
            LogCapability(state, capabilities, V1501SseV8Capability.V23HalfDuplex, "V.23 half-duplex");
            LogCapability(state, capabilities, V1501SseV8Capability.V21, "V.21");
            LogCapability(state, capabilities, V1501SseV8Capability.V90V92Analogue, "V.90/V.92 analogue");
            LogCapability(state, capabilities, V1501SseV8Capability.V90V92Digital, "V.90/V.92 digital");
            LogCapability(state, capabilities, V1501SseV8Capability.V91, "V.91");
        }

        /// <summary>
        /// The LogCapability
        /// </summary>
        /// <param name="state">The state<see cref="V1501State"/></param>
        /// <param name="capabilities">The capabilities<see cref="V1501SseV8Capability"/></param>
        /// <param name="capability">The capability<see cref="V1501SseV8Capability"/></param>
        /// <param name="description">The description<see cref="string"/></param>
        private static void LogCapability(
            V1501State state,
            V1501SseV8Capability capabilities,
            V1501SseV8Capability capability,
            string description) {
            if ((capabilities & capability) != 0)
                state.Log($"    {description}");
        }

        /// <summary>
        /// The ReadNetworkUInt16
        /// </summary>
        /// <param name="buffer">The buffer<see cref="byte[]"/></param>
        /// <param name="offset">The offset<see cref="int"/></param>
        /// <returns>The <see cref="ushort"/></returns>
        private static ushort ReadNetworkUInt16(byte[] buffer, int offset) {
            return unchecked((ushort)((buffer[offset] << 8) | buffer[offset + 1]));
        }

        /// <summary>
        /// The WriteNetworkUInt16
        /// </summary>
        /// <param name="buffer">The buffer<see cref="byte[]"/></param>
        /// <param name="offset">The offset<see cref="int"/></param>
        /// <param name="value">The value<see cref="ushort"/></param>
        private static void WriteNetworkUInt16(byte[] buffer, int offset, ushort value) {
            buffer[offset] = unchecked((byte)(value >> 8));
            buffer[offset + 1] = unchecked((byte)value);
        }

        /// <summary>
        /// The AddTimestamp
        /// </summary>
        /// <param name="timestamp">The timestamp<see cref="ulong"/></param>
        /// <param name="interval">The interval<see cref="int"/></param>
        /// <returns>The <see cref="ulong"/></returns>
        private static ulong AddTimestamp(ulong timestamp, int interval) {
            return unchecked(timestamp + (ulong)interval);
        }

        /// <summary>
        /// The ValidatePacketLength
        /// </summary>
        /// <param name="packet">The packet<see cref="byte[]"/></param>
        /// <param name="length">The length<see cref="int"/></param>
        private static void ValidatePacketLength(byte[] packet, int length) {
            if (length < 0 || length > packet.Length)
                throw new ArgumentOutOfRangeException(nameof(length));
        }
    }

    /// <summary>
    /// Compatibility facade retaining the original native function and
    /// constant names
    /// </summary>
    public static class V1501SseApi {
        /// <summary>
        /// Defines the V150_1_SSE_DEFAULT_REPETITIONS
        /// </summary>
        public const int V150_1_SSE_DEFAULT_REPETITIONS = V1501Sse.DefaultRepetitions;

        /// <summary>
        /// Defines the V150_1_SSE_DEFAULT_REPETITION_INTERVAL
        /// </summary>
        public const int V150_1_SSE_DEFAULT_REPETITION_INTERVAL = V1501Sse.DefaultRepetitionInterval;

        /// <summary>
        /// Defines the V150_1_SSE_DEFAULT_ACK_N0
        /// </summary>
        public const int V150_1_SSE_DEFAULT_ACK_N0 = V1501Sse.DefaultAckN0;

        /// <summary>
        /// Defines the V150_1_SSE_DEFAULT_ACK_T0
        /// </summary>
        public const int V150_1_SSE_DEFAULT_ACK_T0 = V1501Sse.DefaultAckT0;

        /// <summary>
        /// Defines the V150_1_SSE_DEFAULT_ACK_T1
        /// </summary>
        public const int V150_1_SSE_DEFAULT_ACK_T1 = V1501Sse.DefaultAckT1;

        /// <summary>
        /// Defines the V150_1_SSE_DEFAULT_RECOVERY_N
        /// </summary>
        public const int V150_1_SSE_DEFAULT_RECOVERY_N = V1501Sse.DefaultRecoveryN;

        /// <summary>
        /// Defines the V150_1_SSE_DEFAULT_RECOVERY_T1
        /// </summary>
        public const int V150_1_SSE_DEFAULT_RECOVERY_T1 = V1501Sse.DefaultRecoveryT1;

        /// <summary>
        /// Defines the V150_1_SSE_DEFAULT_RECOVERY_T2
        /// </summary>
        public const int V150_1_SSE_DEFAULT_RECOVERY_T2 = V1501Sse.DefaultRecoveryT2;

        /// <summary>
        /// Defines the V150_1_MEDIA_STATE_ITU_RESERVED_0
        /// </summary>
        public const int V150_1_MEDIA_STATE_ITU_RESERVED_0 = (int)V1501MediaState.ItuReserved0;

        /// <summary>
        /// Defines the V150_1_MEDIA_STATE_INITIAL_AUDIO
        /// </summary>
        public const int V150_1_MEDIA_STATE_INITIAL_AUDIO = (int)V1501MediaState.InitialAudio;

        /// <summary>
        /// Defines the V150_1_MEDIA_STATE_VOICE_BAND_DATA
        /// </summary>
        public const int V150_1_MEDIA_STATE_VOICE_BAND_DATA = (int)V1501MediaState.VoiceBandData;

        /// <summary>
        /// Defines the V150_1_MEDIA_STATE_MODEM_RELAY
        /// </summary>
        public const int V150_1_MEDIA_STATE_MODEM_RELAY = (int)V1501MediaState.ModemRelay;

        /// <summary>
        /// Defines the V150_1_MEDIA_STATE_FAX_RELAY
        /// </summary>
        public const int V150_1_MEDIA_STATE_FAX_RELAY = (int)V1501MediaState.FaxRelay;

        /// <summary>
        /// Defines the V150_1_MEDIA_STATE_TEXT_RELAY
        /// </summary>
        public const int V150_1_MEDIA_STATE_TEXT_RELAY = (int)V1501MediaState.TextRelay;

        /// <summary>
        /// Defines the V150_1_MEDIA_STATE_TEXT_PROBE
        /// </summary>
        public const int V150_1_MEDIA_STATE_TEXT_PROBE = (int)V1501MediaState.TextProbe;

        /// <summary>
        /// Defines the V150_1_MEDIA_STATE_INDETERMINATE
        /// </summary>
        public const int V150_1_MEDIA_STATE_INDETERMINATE = (int)V1501MediaState.Indeterminate;

        /// <summary>
        /// Defines the V150_1_CDSCSELECT_INDETERMINATE
        /// </summary>
        public const int V150_1_CDSCSELECT_INDETERMINATE = (int)V1501CallDiscriminationSelection.Indeterminate;

        /// <summary>
        /// Defines the V150_1_CDSCSELECT_AUDIO_RFC4733
        /// </summary>
        public const int V150_1_CDSCSELECT_AUDIO_RFC4733 = (int)V1501CallDiscriminationSelection.AudioRfc4733;

        /// <summary>
        /// Defines the V150_1_CDSCSELECT_VBD_PREFERRED
        /// </summary>
        public const int V150_1_CDSCSELECT_VBD_PREFERRED = (int)V1501CallDiscriminationSelection.VbdPreferred;

        /// <summary>
        /// Defines the V150_1_CDSCSELECT_MIXED
        /// </summary>
        public const int V150_1_CDSCSELECT_MIXED = (int)V1501CallDiscriminationSelection.Mixed;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_V8_CM
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_V8_CM = (int)V1501SseMoipRic.V8Cm;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_V8_JM
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_V8_JM = (int)V1501SseMoipRic.V8Jm;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_V32BIS_AA
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_V32BIS_AA = (int)V1501SseMoipRic.V32BisAa;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_V32BIS_AC
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_V32BIS_AC = (int)V1501SseMoipRic.V32BisAc;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_V22BIS_USB1
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_V22BIS_USB1 = (int)V1501SseMoipRic.V22BisUsb1;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_V22BIS_SB1
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_V22BIS_SB1 = (int)V1501SseMoipRic.V22BisSb1;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_V22BIS_S1
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_V22BIS_S1 = (int)V1501SseMoipRic.V22BisS1;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_V21_CH2
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_V21_CH2 = (int)V1501SseMoipRic.V21Channel2;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_V21_CH1
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_V21_CH1 = (int)V1501SseMoipRic.V21Channel1;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_V23_HIGH_CHANNEL
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_V23_HIGH_CHANNEL = (int)V1501SseMoipRic.V23HighChannel;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_V23_LOW_CHANNEL
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_V23_LOW_CHANNEL = (int)V1501SseMoipRic.V23LowChannel;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_TONE_2225HZ
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_TONE_2225HZ = (int)V1501SseMoipRic.Tone2225Hz;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_V21_CH2_HDLC_FLAGS
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_V21_CH2_HDLC_FLAGS = (int)V1501SseMoipRic.V21Channel2HdlcFlags;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INDETERMINATE_SIGNAL
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INDETERMINATE_SIGNAL = (int)V1501SseMoipRic.IndeterminateSignal;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_SILENCE
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_SILENCE = (int)V1501SseMoipRic.Silence;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_CNG
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_CNG = (int)V1501SseMoipRic.Cng;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_VOICE
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_VOICE = (int)V1501SseMoipRic.Voice;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_TIMEOUT
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_TIMEOUT = (int)V1501SseMoipRic.Timeout;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_P_STATE_TRANSITION
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_P_STATE_TRANSITION = (int)V1501SseMoipRic.PStateTransition;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_CLEARDOWN
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_CLEARDOWN = (int)V1501SseMoipRic.Cleardown;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_ANS_CED
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_ANS_CED = (int)V1501SseMoipRic.AnsCed;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_ANSAM
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_ANSAM = (int)V1501SseMoipRic.Ansam;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_ANS_PR
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_ANS_PR = (int)V1501SseMoipRic.AnsPhaseReversal;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_ANSAM_PR
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_ANSAM_PR = (int)V1501SseMoipRic.AnsamPhaseReversal;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_V92_QC1A
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_V92_QC1A = (int)V1501SseMoipRic.V92Qc1A;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_V92_QC1D
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_V92_QC1D = (int)V1501SseMoipRic.V92Qc1D;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_V92_QC2A
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_V92_QC2A = (int)V1501SseMoipRic.V92Qc2A;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_V92_QC2D
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_V92_QC2D = (int)V1501SseMoipRic.V92Qc2D;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_V8BIS_CRE
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_V8BIS_CRE = (int)V1501SseMoipRic.V8BisCre;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_V8BIS_CRD
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_V8BIS_CRD = (int)V1501SseMoipRic.V8BisCrd;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_TIA825A_45_45BPS
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_TIA825A_45_45BPS = (int)V1501SseMoipRic.Tia825A4545Bps;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_TIA825A_50BPS
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_TIA825A_50BPS = (int)V1501SseMoipRic.Tia825A50Bps;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_EDT
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_EDT = (int)V1501SseMoipRic.Edt;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_BELL103
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_BELL103 = (int)V1501SseMoipRic.Bell103;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_V21_TEXT_TELEPHONE
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_V21_TEXT_TELEPHONE = (int)V1501SseMoipRic.V21TextTelephone;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_V23_MINITEL
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_V23_MINITEL = (int)V1501SseMoipRic.V23Minitel;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_V18_TEXT_TELEPHONE
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_V18_TEXT_TELEPHONE = (int)V1501SseMoipRic.V18TextTelephone;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_V18_DTMF_TEXT_RELAY
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_V18_DTMF_TEXT_RELAY = (int)V1501SseMoipRic.V18DtmfTextRelay;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_CTM
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_CTM = (int)V1501SseMoipRic.Ctm;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_VENDOR_MIN
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_VENDOR_MIN = (int)V1501SseMoipRic.VendorMinimum;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_VENDOR_MAX
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_VENDOR_MAX = (int)V1501SseMoipRic.VendorMaximum;

        /// <summary>
        /// Defines the V150_1_SSE_FOIP_RIC_V21_FLAGS
        /// </summary>
        public const int V150_1_SSE_FOIP_RIC_V21_FLAGS = (int)V1501SseFoipRic.V21Flags;

        /// <summary>
        /// Defines the V150_1_SSE_FOIP_RIC_V8_CM
        /// </summary>
        public const int V150_1_SSE_FOIP_RIC_V8_CM = (int)V1501SseFoipRic.V8Cm;

        /// <summary>
        /// Defines the V150_1_SSE_FOIP_RIC_P_STATE_TRANSITION
        /// </summary>
        public const int V150_1_SSE_FOIP_RIC_P_STATE_TRANSITION = (int)V1501SseFoipRic.PStateTransition;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_V8_CM_PCM_MODE
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_V8_CM_PCM_MODE = (int)V1501SseV8Capability.PcmMode;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_V8_CM_V34_DUPLEX
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_V8_CM_V34_DUPLEX = (int)V1501SseV8Capability.V34Duplex;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_V8_CM_V34_HALF_DUPLEX
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_V8_CM_V34_HALF_DUPLEX = (int)V1501SseV8Capability.V34HalfDuplex;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_V8_CM_V32BIS
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_V8_CM_V32BIS = (int)V1501SseV8Capability.V32Bis;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_V8_CM_V22BIS
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_V8_CM_V22BIS = (int)V1501SseV8Capability.V22Bis;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_V8_CM_V17
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_V8_CM_V17 = (int)V1501SseV8Capability.V17;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_V8_CM_V29
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_V8_CM_V29 = (int)V1501SseV8Capability.V29;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_V8_CM_V27TER
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_V8_CM_V27TER = (int)V1501SseV8Capability.V27Ter;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_V8_CM_V26TER
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_V8_CM_V26TER = (int)V1501SseV8Capability.V26Ter;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_V8_CM_V26BIS
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_V8_CM_V26BIS = (int)V1501SseV8Capability.V26Bis;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_V8_CM_V23_DUPLEX
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_V8_CM_V23_DUPLEX = (int)V1501SseV8Capability.V23Duplex;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_V8_CM_V23_HALF_DUPLEX
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_V8_CM_V23_HALF_DUPLEX = (int)V1501SseV8Capability.V23HalfDuplex;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_V8_CM_V21
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_V8_CM_V21 = (int)V1501SseV8Capability.V21;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_V8_CM_V90_V92_ANALOGUE
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_V8_CM_V90_V92_ANALOGUE = (int)V1501SseV8Capability.V90V92Analogue;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_V8_CM_V90_V92_DIGITAL
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_V8_CM_V90_V92_DIGITAL = (int)V1501SseV8Capability.V90V92Digital;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_V8_CM_V91
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_V8_CM_V91 = (int)V1501SseV8Capability.V91;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_TIMEOUT_NULL
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_TIMEOUT_NULL = (int)V1501SseTimeoutReason.Null;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_TIMEOUT_CALL_DISCRIMINATION_TIMEOUT
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_TIMEOUT_CALL_DISCRIMINATION_TIMEOUT = (int)V1501SseTimeoutReason.CallDiscriminationTimeout;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_TIMEOUT_IP_TLP
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_TIMEOUT_IP_TLP = (int)V1501SseTimeoutReason.IpTlp;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_TIMEOUT_SSE_EXPLICIT_ACK_TIMEOUT
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_TIMEOUT_SSE_EXPLICIT_ACK_TIMEOUT = (int)V1501SseTimeoutReason.ExplicitAcknowledgementTimeout;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_CLEARDOWN_UNKNOWN
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_CLEARDOWN_UNKNOWN = (int)V1501SseCleardownReason.Unknown;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_CLEARDOWN_PHYSICAL_LAYER_RELEASE
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_CLEARDOWN_PHYSICAL_LAYER_RELEASE = (int)V1501SseCleardownReason.PhysicalLayerRelease;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_CLEARDOWN_LINK_LAYER_DISCONNECT
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_CLEARDOWN_LINK_LAYER_DISCONNECT = (int)V1501SseCleardownReason.LinkLayerDisconnect;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_CLEARDOWN_COMPRESSION_DISCONNECT
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_CLEARDOWN_COMPRESSION_DISCONNECT = (int)V1501SseCleardownReason.CompressionDisconnect;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_CLEARDOWN_ABORT
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_CLEARDOWN_ABORT = (int)V1501SseCleardownReason.Abort;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_CLEARDOWN_ON_HOOK
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_CLEARDOWN_ON_HOOK = (int)V1501SseCleardownReason.OnHook;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_CLEARDOWN_NETWORK_LAYER_TERMINATION
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_CLEARDOWN_NETWORK_LAYER_TERMINATION = (int)V1501SseCleardownReason.NetworkLayerTermination;

        /// <summary>
        /// Defines the V150_1_SSE_MOIP_RIC_INFO_CLEARDOWN_ADMINISTRATIVE
        /// </summary>
        public const int V150_1_SSE_MOIP_RIC_INFO_CLEARDOWN_ADMINISTRATIVE = (int)V1501SseCleardownReason.Administrative;

        /// <summary>
        /// Defines the V150_1_SSE_RELIABILITY_NONE
        /// </summary>
        public const int V150_1_SSE_RELIABILITY_NONE = (int)V1501SseReliabilityMethod.None;

        /// <summary>
        /// Defines the V150_1_SSE_RELIABILITY_BY_REPETITION
        /// </summary>
        public const int V150_1_SSE_RELIABILITY_BY_REPETITION = (int)V1501SseReliabilityMethod.Repetition;

        /// <summary>
        /// Defines the V150_1_SSE_RELIABILITY_BY_RFC2198
        /// </summary>
        public const int V150_1_SSE_RELIABILITY_BY_RFC2198 = (int)V1501SseReliabilityMethod.Rfc2198;

        /// <summary>
        /// Defines the V150_1_SSE_RELIABILITY_BY_EXPLICIT_ACK
        /// </summary>
        public const int V150_1_SSE_RELIABILITY_BY_EXPLICIT_ACK = (int)V1501SseReliabilityMethod.ExplicitAcknowledgement;

        /// <summary>
        /// Defines the V150_1_SSE_STATUS_V8_CM_RECEIVED
        /// </summary>
        public const int V150_1_SSE_STATUS_V8_CM_RECEIVED = (int)V1501SseStatus.V8CmReceived;

        /// <summary>
        /// Defines the V150_1_SSE_STATUS_V8_JM_RECEIVED
        /// </summary>
        public const int V150_1_SSE_STATUS_V8_JM_RECEIVED = (int)V1501SseStatus.V8JmReceived;

        /// <summary>
        /// Defines the V150_1_SSE_STATUS_AA_RECEIVED
        /// </summary>
        public const int V150_1_SSE_STATUS_AA_RECEIVED = (int)V1501SseStatus.AaReceived;

        /// <summary>
        /// Defines the V150_1_SSE_STATUS_V8_CM_RECEIVED_FAX
        /// </summary>
        public const int V150_1_SSE_STATUS_V8_CM_RECEIVED_FAX = (int)V1501SseStatus.V8CmReceivedFax;

        /// <summary>
        /// Defines the V150_1_SSE_STATUS_V8_JM_RECEIVED_FAX
        /// </summary>
        public const int V150_1_SSE_STATUS_V8_JM_RECEIVED_FAX = (int)V1501SseStatus.V8JmReceivedFax;

        /// <summary>
        /// Defines the V150_1_SSE_STATUS_AA_RECEIVED_FAX
        /// </summary>
        public const int V150_1_SSE_STATUS_AA_RECEIVED_FAX = (int)V1501SseStatus.AaReceivedFax;

        /// <summary>
        /// Defines the V150_1_SSE_STATUS_CLEARDOWN
        /// </summary>
        public const int V150_1_SSE_STATUS_CLEARDOWN = (int)V1501SseStatus.Cleardown;

        /// <summary>
        /// The v150_1_sse_media_state_to_str
        /// </summary>
        /// <param name="state">The state<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string v150_1_sse_media_state_to_str(int state) =>
            V1501Sse.MediaStateToString(state);

        /// <summary>
        /// The v150_1_sse_moip_ric_to_str
        /// </summary>
        /// <param name="ric">The ric<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string v150_1_sse_moip_ric_to_str(int ric) =>
            V1501Sse.MoipRicToString(ric);

        /// <summary>
        /// The v150_1_sse_timeout_reason_to_str
        /// </summary>
        /// <param name="reason">The reason<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string v150_1_sse_timeout_reason_to_str(int reason) =>
            V1501Sse.TimeoutReasonToString(reason);

        /// <summary>
        /// The v150_1_sse_cleardown_reason_to_str
        /// </summary>
        /// <param name="reason">The reason<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string v150_1_sse_cleardown_reason_to_str(int reason) =>
            V1501Sse.CleardownReasonToString(reason);

        /// <summary>
        /// The v150_1_sse_status_to_str
        /// </summary>
        /// <param name="status">The status<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string v150_1_sse_status_to_str(int status) =>
            V1501Sse.StatusToString(status);

        /// <summary>
        /// The v150_1_rx_sse_packet
        /// </summary>
        /// <param name="state">The state<see cref="V1501State"/></param>
        /// <param name="sequenceNumber">The sequenceNumber<see cref="ushort"/></param>
        /// <param name="timestamp">The timestamp<see cref="uint"/></param>
        /// <param name="packet">The packet<see cref="byte[]"/></param>
        /// <param name="length">The length<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int v150_1_rx_sse_packet(
            V1501State state,
            ushort sequenceNumber,
            uint timestamp,
            byte[] packet,
            int length) =>
            V1501Sse.ReceivePacket(state, sequenceNumber, timestamp, packet, length);

        /// <summary>
        /// The v150_1_tx_sse_packet
        /// </summary>
        /// <param name="state">The state<see cref="V1501State"/></param>
        /// <param name="eventCode">The eventCode<see cref="int"/></param>
        /// <param name="ric">The ric<see cref="int"/></param>
        /// <param name="ricInformation">The ricInformation<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int v150_1_tx_sse_packet(
            V1501State state,
            int eventCode,
            int ric,
            int ricInformation) =>
            V1501Sse.TransmitPacket(state, eventCode, ric, ricInformation);

        /// <summary>
        /// The v150_1_set_sse_reliability_method
        /// </summary>
        /// <param name="state">The state<see cref="V1501State"/></param>
        /// <param name="method">The method<see cref="int"/></param>
        /// <param name="parameter1">The parameter1<see cref="int"/></param>
        /// <param name="parameter2">The parameter2<see cref="int"/></param>
        /// <param name="parameter3">The parameter3<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int v150_1_set_sse_reliability_method(
            V1501State state,
            int method,
            int parameter1,
            int parameter2,
            int parameter3) =>
            V1501Sse.SetReliabilityMethod(
                state,
                (V1501SseReliabilityMethod)method,
                parameter1,
                parameter2,
                parameter3);

        /// <summary>
        /// The v150_1_sse_timer_expired
        /// </summary>
        /// <param name="state">The state<see cref="V1501State"/></param>
        /// <param name="now">The now<see cref="ulong"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int v150_1_sse_timer_expired(V1501State state, ulong now) =>
            V1501Sse.TimerExpired(state, now);

        /// <summary>
        /// The v150_1_sse_init
        /// </summary>
        /// <param name="state">The state<see cref="V1501State"/></param>
        /// <param name="txPacketHandler">The txPacketHandler<see cref="V1501SseTransmitPacketHandler?"/></param>
        /// <param name="txPacketUserData">The txPacketUserData<see cref="object?"/></param>
        public static void v150_1_sse_init(
            V1501State state,
            V1501SseTransmitPacketHandler? txPacketHandler,
            object? txPacketUserData) =>
            V1501Sse.Initialize(state, txPacketHandler, txPacketUserData);
    }
}
