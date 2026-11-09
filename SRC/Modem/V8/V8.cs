/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * V8.cs - Managed C# port of v8.c and v8.h
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2004 Steve Underwood
 *
 * This file is distributed under the terms of the GNU Lesser General Public
 * License version 2.1, matching the original source files.
 */

#nullable enable

namespace TKFaxEngine.Modem.V8 {
    using global::TKFaxEngine.Audio;
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>V.8 call-function values.</summary>

    /// <summary>
    /// Defines the V8CallFunction
    /// </summary>
    public enum V8CallFunction {
        /// <summary>
        /// Defines the Tbs
        /// </summary>
        Tbs = 0,

        /// <summary>
        /// Defines the H324
        /// </summary>
        H324 = 1,

        /// <summary>
        /// Defines the V18
        /// </summary>
        V18 = 2,

        /// <summary>
        /// Defines the T101
        /// </summary>
        T101 = 3,

        /// <summary>
        /// Defines the T30TransmitFax
        /// </summary>
        T30TransmitFax = 4,

        /// <summary>
        /// Defines the T30ReceiveFax
        /// </summary>
        T30ReceiveFax = 5,

        /// <summary>
        /// Defines the VSeriesModem
        /// </summary>
        VSeriesModem = 6,

        /// <summary>
        /// Defines the Extension
        /// </summary>
        Extension = 7
    }

    /// <summary>V.8 modulation capability mask.</summary>

    /// <summary>
    /// Defines the V8Modulation
    /// </summary>
    [Flags]
    public enum V8Modulation : uint {
        /// <summary>
        /// Defines the None
        /// </summary>
        None = 0,

        /// <summary>
        /// Defines the V17
        /// </summary>
        V17 = 1u << 0,

        /// <summary>
        /// Defines the V21
        /// </summary>
        V21 = 1u << 1,

        /// <summary>
        /// Defines the V22
        /// </summary>
        V22 = 1u << 2,

        /// <summary>
        /// Defines the V23HalfDuplex
        /// </summary>
        V23HalfDuplex = 1u << 3,

        /// <summary>
        /// Defines the V23
        /// </summary>
        V23 = 1u << 4,

        /// <summary>
        /// Defines the V26Bis
        /// </summary>
        V26Bis = 1u << 5,

        /// <summary>
        /// Defines the V26Ter
        /// </summary>
        V26Ter = 1u << 6,

        /// <summary>
        /// Defines the V27Ter
        /// </summary>
        V27Ter = 1u << 7,

        /// <summary>
        /// Defines the V29
        /// </summary>
        V29 = 1u << 8,

        /// <summary>
        /// Defines the V32
        /// </summary>
        V32 = 1u << 9,

        /// <summary>
        /// Defines the V34HalfDuplex
        /// </summary>
        V34HalfDuplex = 1u << 10,

        /// <summary>
        /// Defines the V34
        /// </summary>
        V34 = 1u << 11,

        /// <summary>
        /// Defines the V90
        /// </summary>
        V90 = 1u << 12,

        /// <summary>
        /// Defines the V92
        /// </summary>
        V92 = 1u << 13
    }

    /// <summary>V.8 protocol values.</summary>

    /// <summary>
    /// Defines the V8Protocol
    /// </summary>
    public enum V8Protocol {
        /// <summary>
        /// Defines the None
        /// </summary>
        None = 0,

        /// <summary>
        /// Defines the LapmV42
        /// </summary>
        LapmV42 = 1,

        /// <summary>
        /// Defines the Extension
        /// </summary>
        Extension = 7
    }

    /// <summary>V.8 PSTN access flags.</summary>

    /// <summary>
    /// Defines the V8PstnAccess
    /// </summary>
    [Flags]
    public enum V8PstnAccess {
        /// <summary>
        /// Defines the None
        /// </summary>
        None = 0,

        /// <summary>
        /// Defines the CallingDceCellular
        /// </summary>
        CallingDceCellular = 0x01,

        /// <summary>
        /// Defines the AnsweringDceCellular
        /// </summary>
        AnsweringDceCellular = 0x02,

        /// <summary>
        /// Defines the DceOnDigitalNetwork
        /// </summary>
        DceOnDigitalNetwork = 0x04
    }

    /// <summary>V.8 PCM-modem availability flags.</summary>

    /// <summary>
    /// Defines the V8PcmModemAvailability
    /// </summary>
    [Flags]
    public enum V8PcmModemAvailability {
        /// <summary>
        /// Defines the None
        /// </summary>
        None = 0,

        /// <summary>
        /// Defines the V90V92Analogue
        /// </summary>
        V90V92Analogue = 0x01,

        /// <summary>
        /// Defines the V90V92Digital
        /// </summary>
        V90V92Digital = 0x02,

        /// <summary>
        /// Defines the V91
        /// </summary>
        V91 = 0x04
    }

    /// <summary>V.8 negotiation result status.</summary>

    /// <summary>
    /// Defines the V8Status
    /// </summary>
    public enum V8Status {
        /// <summary>
        /// Defines the InProgress
        /// </summary>
        InProgress = 0,

        /// <summary>
        /// Defines the V8Offered
        /// </summary>
        V8Offered = 1,

        /// <summary>
        /// Defines the V8Call
        /// </summary>
        V8Call = 2,

        /// <summary>
        /// Defines the NonV8Call
        /// </summary>
        NonV8Call = 3,

        /// <summary>
        /// Defines the Failed
        /// </summary>
        Failed = 4,

        /// <summary>
        /// Defines the CallFunctionReceived
        /// </summary>
        CallFunctionReceived = 5,

        /// <summary>
        /// Defines the CallingToneReceived
        /// </summary>
        CallingToneReceived = 6,

        /// <summary>
        /// Defines the FaxCngToneReceived
        /// </summary>
        FaxCngToneReceived = 7
    }

    /// <summary>Parameters carried in V.8 CM and JM messages.</summary>

    /// <summary>
    /// Defines the <see cref="V8CmJmParameters" />
    /// </summary>
    public sealed class V8CmJmParameters {
        /// <summary>
        /// Gets or sets the CallFunction
        /// </summary>
        public V8CallFunction CallFunction { get; set; }

        /// <summary>
        /// Gets or sets the Modulations
        /// </summary>
        public V8Modulation Modulations { get; set; }

        /// <summary>
        /// Gets or sets the Protocols
        /// </summary>
        public V8Protocol Protocols { get; set; }

        /// <summary>
        /// Gets or sets the PstnAccess
        /// </summary>
        public V8PstnAccess PstnAccess { get; set; }

        /// <summary>
        /// Gets or sets the Nsf
        /// </summary>
        public int Nsf { get; set; } = -1;

        /// <summary>
        /// Gets or sets the PcmModemAvailability
        /// </summary>
        public V8PcmModemAvailability PcmModemAvailability { get; set; }

        /// <summary>
        /// Gets or sets the T66
        /// </summary>
        public int T66 { get; set; } = -1;

        /// <summary>
        /// The Clone
        /// </summary>
        /// <returns>The <see cref="V8CmJmParameters"/></returns>
        public V8CmJmParameters Clone() {
            var clone = new V8CmJmParameters();
            clone.CopyFrom(this);
            return clone;
        }

        /// <summary>
        /// The CopyFrom
        /// </summary>
        /// <param name="source">The source<see cref="V8CmJmParameters"/></param>
        public void CopyFrom(V8CmJmParameters source) {
            ArgumentNullException.ThrowIfNull(source);
            CallFunction = source.CallFunction;
            Modulations = source.Modulations;
            Protocols = source.Protocols;
            PstnAccess = source.PstnAccess;
            Nsf = source.Nsf;
            PcmModemAvailability = source.PcmModemAvailability;
            T66 = source.T66;
        }
    }

    /// <summary>Complete V.8 configuration or result structure.</summary>

    /// <summary>
    /// Defines the <see cref="V8Parameters" />
    /// </summary>
    public sealed class V8Parameters {
        /// <summary>
        /// Gets or sets the Status
        /// </summary>
        public V8Status Status { get; set; } = V8Status.InProgress;

        /// <summary>
        /// Gets or sets a value indicating whether GatewayMode
        /// </summary>
        public bool GatewayMode { get; set; }

        /// <summary>
        /// Gets or sets the ModemConnectTone
        /// </summary>
        public ModemConnectTone ModemConnectTone { get; set; } = ModemConnectTone.None;

        /// <summary>
        /// Gets or sets a value indicating whether SendCi
        /// </summary>
        public bool SendCi { get; set; }

        /// <summary>
        /// Gets or sets the V92
        /// </summary>
        public int V92 { get; set; } = -1;

        /// <summary>
        /// Gets the JmCm
        /// </summary>
        public V8CmJmParameters JmCm { get; } = new V8CmJmParameters();

        /// <summary>
        /// The Clone
        /// </summary>
        /// <returns>The <see cref="V8Parameters"/></returns>
        public V8Parameters Clone() {
            var clone = new V8Parameters();
            clone.CopyFrom(this);
            return clone;
        }

        /// <summary>
        /// The CopyFrom
        /// </summary>
        /// <param name="source">The source<see cref="V8Parameters"/></param>
        public void CopyFrom(V8Parameters source) {
            ArgumentNullException.ThrowIfNull(source);
            Status = source.Status;
            GatewayMode = source.GatewayMode;
            ModemConnectTone = source.ModemConnectTone;
            SendCi = source.SendCi;
            V92 = source.V92;
            JmCm.CopyFrom(source.JmCm);
        }
    }

    /// <summary>Callback used to report V.8 negotiation events.</summary>

    /// <summary>
    /// The V8ResultHandler
    /// </summary>
    /// <param name="userData">The userData<see cref="object?"/></param>
    /// <param name="result">The result<see cref="V8Parameters"/></param>
    public delegate void V8ResultHandler(object? userData, V8Parameters result);

    /// <summary>
    /// Defines the V8ProtocolState
    /// </summary>
    internal enum V8ProtocolState {
        /// <summary>
        /// Defines the WaitOneSecond
        /// </summary>
        WaitOneSecond = 0,

        /// <summary>
        /// Defines the AwaitAnsam
        /// </summary>
        AwaitAnsam,

        /// <summary>
        /// Defines the CiOn
        /// </summary>
        CiOn,

        /// <summary>
        /// Defines the CiOff
        /// </summary>
        CiOff,

        /// <summary>
        /// Defines the HeardAnsam
        /// </summary>
        HeardAnsam,

        /// <summary>
        /// Defines the CmOn
        /// </summary>
        CmOn,

        /// <summary>
        /// Defines the CjOn
        /// </summary>
        CjOn,

        /// <summary>
        /// Defines the CmWait
        /// </summary>
        CmWait,

        /// <summary>
        /// Defines the PostCmWait
        /// </summary>
        PostCmWait,

        /// <summary>
        /// Defines the SigC
        /// </summary>
        SigC,

        /// <summary>
        /// Defines the JmOn
        /// </summary>
        JmOn,

        /// <summary>
        /// Defines the SigA
        /// </summary>
        SigA,

        /// <summary>
        /// Defines the Parked
        /// </summary>
        Parked
    }

    /// <summary>
    /// Defines the V8SyncType
    /// </summary>
    internal enum V8SyncType {
        /// <summary>
        /// Defines the Unknown
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Defines the Ci
        /// </summary>
        Ci,

        /// <summary>
        /// Defines the CmJm
        /// </summary>
        CmJm,

        /// <summary>
        /// Defines the V92
        /// </summary>
        V92
    }

    /// <summary>
    /// Defines the V8FskChannel
    /// </summary>
    internal enum V8FskChannel {
        /// <summary>
        /// Defines the V21Channel1
        /// </summary>
        V21Channel1,

        /// <summary>
        /// Defines the V21Channel2
        /// </summary>
        V21Channel2
    }

    /// <summary>Managed equivalent of <c>v8_state_t</c>.</summary>

    /// <summary>
    /// Defines the <see cref="V8State" />
    /// </summary>
    public sealed class V8State : IDisposable {
        /// <summary>
        /// Defines the _disposed
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="V8State"/> class.
        /// </summary>
        internal V8State() {
        }

        /// <summary>Gets whether this endpoint is the calling party.</summary>

        /// <summary>
        /// Gets or sets a value indicating whether CallingParty
        /// </summary>
        public bool CallingParty { get; internal set; }

        /// <summary>Gets the current protocol result object.</summary>

        /// <summary>
        /// Gets the Result
        /// </summary>
        public V8Parameters Result { get; } = new V8Parameters();

        /// <summary>Gets the current local negotiation parameters.</summary>

        /// <summary>
        /// Gets the Parameters
        /// </summary>
        public V8Parameters Parameters { get; } = new V8Parameters();

        /// <summary>Gets whether the state machine has stopped processing V.8.</summary>

        /// <summary>
        /// Gets a value indicating whether IsParked
        /// </summary>
        public bool IsParked => State == V8ProtocolState.Parked;

        /// <summary>Optional flow-log sink.</summary>

        /// <summary>
        /// Gets or sets the LogHandler
        /// </summary>
        public Action<string>? LogHandler { get; set; }

        /// <summary>Generates V.8 audio into the supplied 8 kHz PCM buffer.</summary>

        /// <summary>
        /// The Transmit
        /// </summary>
        /// <param name="samples">The samples<see cref="Span{short}"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int Transmit(Span<short> samples) {
            return V8.Transmit(this, samples);
        }

        /// <summary>
        /// The Transmit
        /// </summary>
        /// <param name="samples">The samples<see cref="short[]"/></param>
        /// <param name="offset">The offset<see cref="int"/></param>
        /// <param name="length">The length<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int Transmit(short[] samples, int offset, int length) {
            ArgumentNullException.ThrowIfNull(samples);
            return Transmit(samples.AsSpan(offset, length));
        }

        /// <summary>Processes received 8 kHz PCM audio through the V.8 state machine.</summary>

        /// <summary>
        /// The Receive
        /// </summary>
        /// <param name="samples">The samples<see cref="ReadOnlySpan{short}"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int Receive(ReadOnlySpan<short> samples) {
            return V8.Receive(this, samples);
        }

        /// <summary>
        /// The Receive
        /// </summary>
        /// <param name="samples">The samples<see cref="short[]"/></param>
        /// <param name="offset">The offset<see cref="int"/></param>
        /// <param name="length">The length<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int Receive(short[] samples, int offset, int length) {
            ArgumentNullException.ThrowIfNull(samples);
            return Receive(samples.AsSpan(offset, length));
        }

        /// <summary>Decodes V.8 signalling without running the negotiation state machine.</summary>

        /// <summary>
        /// The DecodeReceive
        /// </summary>
        /// <param name="samples">The samples<see cref="ReadOnlySpan{short}"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int DecodeReceive(ReadOnlySpan<short> samples) {
            return V8.DecodeReceive(this, samples);
        }

        /// <summary>
        /// Gets or sets the ResultHandler
        /// </summary>
        internal V8ResultHandler? ResultHandler { get; set; }

        /// <summary>
        /// Gets or sets the ResultHandlerUserData
        /// </summary>
        internal object? ResultHandlerUserData { get; set; }

        /// <summary>
        /// Gets or sets the State
        /// </summary>
        internal V8ProtocolState State { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether FskTxOn
        /// </summary>
        internal bool FskTxOn { get; set; }

        /// <summary>
        /// Gets or sets the ModemConnectToneTxTimer
        /// </summary>
        internal int ModemConnectToneTxTimer { get; set; }

        /// <summary>
        /// Gets or sets the NegotiationTimer
        /// </summary>
        internal int NegotiationTimer { get; set; }

        /// <summary>
        /// Gets or sets the CiTimer
        /// </summary>
        internal int CiTimer { get; set; }

        /// <summary>
        /// Gets or sets the CiRepetitionCount
        /// </summary>
        internal int CiRepetitionCount { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether Proceed
        /// </summary>
        internal bool Proceed { get; set; }

        /// <summary>
        /// Gets or sets the V21Transmitter
        /// </summary>
        internal V8FskTransmitter? V21Transmitter { get; set; }

        /// <summary>
        /// Gets or sets the V21Receiver
        /// </summary>
        internal V8FskReceiver? V21Receiver { get; set; }

        /// <summary>
        /// Gets the TransmitQueue
        /// </summary>
        internal Queue<byte> TransmitQueue { get; } = new Queue<byte>(1024);

        /// <summary>
        /// Gets or sets the AnsamTransmitter
        /// </summary>
        internal ModemConnectTonesTxState? AnsamTransmitter { get; set; }

        /// <summary>
        /// Gets or sets the AnsamReceiver
        /// </summary>
        internal ModemConnectTonesRxState? AnsamReceiver { get; set; }

        /// <summary>
        /// Gets or sets the CallingToneReceiver
        /// </summary>
        internal ModemConnectTonesRxState? CallingToneReceiver { get; set; }

        /// <summary>
        /// Gets or sets the CngToneReceiver
        /// </summary>
        internal ModemConnectTonesRxState? CngToneReceiver { get; set; }

        /// <summary>
        /// Gets or sets the ModulationBytes
        /// </summary>
        internal int ModulationBytes { get; set; }

        /// <summary>
        /// Gets or sets the BitStream
        /// </summary>
        internal uint BitStream { get; set; }

        /// <summary>
        /// Gets or sets the BitCount
        /// </summary>
        internal int BitCount { get; set; }

        /// <summary>
        /// Gets or sets the PreambleType
        /// </summary>
        internal V8SyncType PreambleType { get; set; }

        /// <summary>
        /// Gets the ReceiveData
        /// </summary>
        internal byte[] ReceiveData { get; } = new byte[64];

        /// <summary>
        /// Gets or sets the ReceiveDataPointer
        /// </summary>
        internal int ReceiveDataPointer { get; set; }

        /// <summary>
        /// Gets the CmJmData
        /// </summary>
        internal byte[] CmJmData { get; } = new byte[64];

        /// <summary>
        /// Gets or sets the CmJmLength
        /// </summary>
        internal int CmJmLength { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether GotCi
        /// </summary>
        internal bool GotCi { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether GotCmJm
        /// </summary>
        internal bool GotCmJm { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether GotCj
        /// </summary>
        internal bool GotCj { get; set; }

        /// <summary>
        /// Gets or sets the ZeroByteCount
        /// </summary>
        internal int ZeroByteCount { get; set; }

        /// <summary>
        /// The Log
        /// </summary>
        /// <param name="message">The message<see cref="string"/></param>
        internal void Log(string message) {
            LogHandler?.Invoke(message);
        }

        /// <summary>
        /// The ThrowIfDisposed
        /// </summary>
        internal void ThrowIfDisposed() {
            if (_disposed)
                throw new ObjectDisposedException(nameof(V8State));
        }

        /// <summary>
        /// The Dispose
        /// </summary>
        public void Dispose() {
            if (_disposed)
                return;

            V21Transmitter?.Dispose();
            V21Receiver?.Dispose();
            AnsamTransmitter?.Dispose();
            AnsamReceiver?.Dispose();
            CallingToneReceiver?.Dispose();
            CngToneReceiver?.Dispose();
            TransmitQueue.Clear();
            ResultHandler = null;
            ResultHandlerUserData = null;
            LogHandler = null;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>V.8 modem-negotiation processing.</summary>

    /// <summary>
    /// Defines the <see cref="V8" />
    /// </summary>
    public static class V8 {
        /// <summary>
        /// Defines the SampleRate
        /// </summary>
        public const int SampleRate = 8000;

        /// <summary>
        /// Defines the TeTimeoutMilliseconds
        /// </summary>
        public const int TeTimeoutMilliseconds = 500;

        /// <summary>
        /// Defines the CallFunctionTag
        /// </summary>
        private const int CallFunctionTag = 0x01;

        /// <summary>
        /// Defines the ModulationTag
        /// </summary>
        private const int ModulationTag = 0x05;

        /// <summary>
        /// Defines the ProtocolsTag
        /// </summary>
        private const int ProtocolsTag = 0x0A;

        /// <summary>
        /// Defines the PstnAccessTag
        /// </summary>
        private const int PstnAccessTag = 0x0D;

        /// <summary>
        /// Defines the NsfTag
        /// </summary>
        private const int NsfTag = 0x0F;

        /// <summary>
        /// Defines the PcmModemAvailabilityTag
        /// </summary>
        private const int PcmModemAvailabilityTag = 0x07;

        /// <summary>
        /// Defines the T66Tag
        /// </summary>
        private const int T66Tag = 0x0E;

        /// <summary>
        /// Defines the CiSyncOctet
        /// </summary>
        private const byte CiSyncOctet = 0x00;

        /// <summary>
        /// Defines the CmJmSyncOctet
        /// </summary>
        private const byte CmJmSyncOctet = 0xE0;

        /// <summary>
        /// Defines the V92SyncOctet
        /// </summary>
        private const byte V92SyncOctet = 0x55;

        /// <summary>
        /// The StatusToString
        /// </summary>
        /// <param name="status">The status<see cref="V8Status"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string StatusToString(V8Status status) {
            return status switch {
                V8Status.InProgress => "Negotiation in progress",
                V8Status.V8Offered => "V.8 offered by the other party",
                V8Status.V8Call => "V.8 call negotiation successful",
                V8Status.NonV8Call => "Non-V.8 call negotiation successful",
                V8Status.Failed => "Call negotiation failed",
                V8Status.CallFunctionReceived => "Call function (CI) received",
                V8Status.CallingToneReceived => "Calling tone received",
                V8Status.FaxCngToneReceived => "FAX CNG tone received",
                _ => "Unknown status"
            };
        }

        /// <summary>
        /// The CallFunctionToString
        /// </summary>
        /// <param name="callFunction">The callFunction<see cref="V8CallFunction"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string CallFunctionToString(V8CallFunction callFunction) {
            return callFunction switch {
                V8CallFunction.Tbs => "TBS",
                V8CallFunction.H324 => "H.324 PSTN multimedia terminal",
                V8CallFunction.V18 => "V.18 textphone",
                V8CallFunction.T101 => "T.101 videotext",
                V8CallFunction.T30TransmitFax => "T.30 Tx FAX",
                V8CallFunction.T30ReceiveFax => "T.30 Rx FAX",
                V8CallFunction.VSeriesModem => "V series modem data",
                V8CallFunction.Extension => "Call function is in extension octet",
                _ => "Unknown call function"
            };
        }

        /// <summary>
        /// The ModulationToString
        /// </summary>
        /// <param name="modulation">The modulation<see cref="V8Modulation"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string ModulationToString(V8Modulation modulation) {
            return modulation switch {
                V8Modulation.V17 => "V.17 half-duplex",
                V8Modulation.V21 => "V.21 duplex",
                V8Modulation.V22 => "V.22/V.22bis duplex",
                V8Modulation.V23HalfDuplex => "V.23 half-duplex",
                V8Modulation.V23 => "V.23 duplex",
                V8Modulation.V26Bis => "V.26bis duplex",
                V8Modulation.V26Ter => "V.26ter duplex",
                V8Modulation.V27Ter => "V.27ter duplex",
                V8Modulation.V29 => "V.29 half-duplex",
                V8Modulation.V32 => "V.32/V.32bis duplex",
                V8Modulation.V34HalfDuplex => "V.34 half-duplex",
                V8Modulation.V34 => "V.34 duplex",
                V8Modulation.V90 => "V.90 duplex",
                V8Modulation.V92 => "V.92 duplex",
                _ => "???"
            };
        }

        /// <summary>
        /// The ProtocolToString
        /// </summary>
        /// <param name="protocol">The protocol<see cref="V8Protocol"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string ProtocolToString(V8Protocol protocol) {
            return protocol switch {
                V8Protocol.None => "None",
                V8Protocol.LapmV42 => "LAPM",
                V8Protocol.Extension => "Extension",
                _ => "Undefined"
            };
        }

        /// <summary>
        /// The PstnAccessToString
        /// </summary>
        /// <param name="access">The access<see cref="V8PstnAccess"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string PstnAccessToString(V8PstnAccess access) {
            return access switch {
                V8PstnAccess.CallingDceCellular => "Calling modem on cellular",
                V8PstnAccess.AnsweringDceCellular => "Answering modem on cellular",
                V8PstnAccess.CallingDceCellular | V8PstnAccess.AnsweringDceCellular =>
                    "Answering and calling modems on cellular",
                V8PstnAccess.DceOnDigitalNetwork => "DCE on digital",
                V8PstnAccess.DceOnDigitalNetwork | V8PstnAccess.CallingDceCellular =>
                    "DCE on digital, and calling modem on cellular",
                V8PstnAccess.DceOnDigitalNetwork | V8PstnAccess.AnsweringDceCellular =>
                    "DCE on digital, answering modem on cellular",
                V8PstnAccess.DceOnDigitalNetwork | V8PstnAccess.AnsweringDceCellular |
                    V8PstnAccess.CallingDceCellular =>
                    "DCE on digital, and answering and calling modems on cellular",
                _ => "PSTN access unknown"
            };
        }

        /// <summary>
        /// The NsfToString
        /// </summary>
        /// <param name="nsf">The nsf<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string NsfToString(int nsf) {
            return "???";
        }

        /// <summary>
        /// The PcmModemAvailabilityToString
        /// </summary>
        /// <param name="availability">The availability<see cref="V8PcmModemAvailability"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string PcmModemAvailabilityToString(V8PcmModemAvailability availability) {
            return availability switch {
                V8PcmModemAvailability.None => "PCM unavailable",
                V8PcmModemAvailability.V90V92Analogue => "V.90/V.92 analogue available",
                V8PcmModemAvailability.V90V92Digital => "V.90/V.92 digital available",
                V8PcmModemAvailability.V90V92Digital | V8PcmModemAvailability.V90V92Analogue =>
                    "V.90/V.92 digital/analogue available",
                V8PcmModemAvailability.V91 => "V.91 available",
                V8PcmModemAvailability.V91 | V8PcmModemAvailability.V90V92Analogue =>
                    "V.91 and V.90/V.92 analogue available",
                V8PcmModemAvailability.V91 | V8PcmModemAvailability.V90V92Digital =>
                    "V.91 and V.90/V.92 digital available",
                V8PcmModemAvailability.V91 | V8PcmModemAvailability.V90V92Digital |
                    V8PcmModemAvailability.V90V92Analogue =>
                    "V.91 and V.90/V.92 digital/analogue available",
                _ => "PCM availability unknown"
            };
        }

        /// <summary>
        /// The T66ToString
        /// </summary>
        /// <param name="t66">The t66<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string T66ToString(int t66) {
            return t66 switch {
                0 => "???",
                1 => "Reserved TIA",
                2 => "Reserved",
                3 => "Reserved TIA + others",
                4 => "Reserved",
                5 => "Reserved TIA + others",
                6 => "Reserved",
                7 => "Reserved TIA + others",
                _ => "???"
            };
        }

        /// <summary>
        /// The SupportedModulationsToString
        /// </summary>
        /// <param name="modulationSchemes">The modulationSchemes<see cref="V8Modulation"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string SupportedModulationsToString(V8Modulation modulationSchemes) {
            var text = new StringBuilder();
            for (int bit = 0; bit < 32; bit++) {
                V8Modulation value = (V8Modulation)(1u << bit);
                if ((modulationSchemes & value) == 0)
                    continue;

                if (text.Length > 0)
                    text.Append(", ");
                text.Append(ModulationToString(value));
            }

            text.Append(" supported");
            return text.ToString();
        }

        /// <summary>
        /// The LogSupportedModulations
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="modulationSchemes">The modulationSchemes<see cref="V8Modulation"/></param>
        public static void LogSupportedModulations(V8State state, V8Modulation modulationSchemes) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();
            state.Log(SupportedModulationsToString(modulationSchemes));
        }

        /// <summary>
        /// The Init
        /// </summary>
        /// <param name="state">The state<see cref="V8State?"/></param>
        /// <param name="callingParty">The callingParty<see cref="bool"/></param>
        /// <param name="parameters">The parameters<see cref="V8Parameters"/></param>
        /// <param name="resultHandler">The resultHandler<see cref="V8ResultHandler?"/></param>
        /// <param name="userData">The userData<see cref="object?"/></param>
        /// <returns>The <see cref="V8State"/></returns>
        public static V8State Init(
            V8State? state,
            bool callingParty,
            V8Parameters parameters,
            V8ResultHandler? resultHandler,
            object? userData) {
            ArgumentNullException.ThrowIfNull(parameters);
            state ??= new V8State();
            state.ThrowIfDisposed();
            state.ResultHandler = resultHandler;
            state.ResultHandlerUserData = userData;
            Restart(state, callingParty, parameters);
            return state;
        }

        /// <summary>
        /// The Restart
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="callingParty">The callingParty<see cref="bool"/></param>
        /// <param name="parameters">The parameters<see cref="V8Parameters"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int Restart(V8State state, bool callingParty, V8Parameters parameters) {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(parameters);
            state.ThrowIfDisposed();

            state.Parameters.CopyFrom(parameters);
            ResetResult(state.Result);
            state.Result.Status = V8Status.InProgress;
            state.Proceed = false;
            state.Result.SendCi = state.Parameters.SendCi;
            state.Result.ModemConnectTone = ModemConnectTone.None;
            state.Result.JmCm.Modulations = state.Parameters.JmCm.Modulations;
            state.Result.JmCm.CallFunction = state.Parameters.JmCm.CallFunction;
            state.Result.JmCm.Nsf = -1;
            state.Result.JmCm.T66 = -1;
            state.ModulationBytes = 3;
            state.CiTimer = 0;
            state.CallingParty = callingParty;

            state.AnsamReceiver = ReplaceReceiver(
                state.AnsamReceiver,
                ModemConnectTone.AnsWithPhaseReversals);
            state.CallingToneReceiver = ReplaceReceiver(
                state.CallingToneReceiver,
                ModemConnectTone.CallingTone);
            state.CngToneReceiver = ReplaceReceiver(
                state.CngToneReceiver,
                ModemConnectTone.FaxCng);
            DecodeInit(state);

            state.AnsamTransmitter?.Dispose();
            state.AnsamTransmitter = null;
            state.V21Transmitter?.Dispose();
            state.V21Transmitter = null;

            if (state.CallingParty) {
                if (state.Parameters.SendCi) {
                    state.State = V8ProtocolState.WaitOneSecond;
                    state.NegotiationTimer = MillisecondsToSamples(1000);
                    state.CiRepetitionCount = 0;
                } else {
                    state.State = V8ProtocolState.AwaitAnsam;
                }

                state.V21Transmitter = new V8FskTransmitter(
                    V8FskChannel.V21Channel1,
                    () => GetBit(state));
                state.ModemConnectToneTxTimer = MillisecondsToSamples(75) + 2;
            } else {
                state.State = V8ProtocolState.CmWait;
                state.NegotiationTimer = MillisecondsToSamples(200 + 5000);
                state.AnsamTransmitter = ModemConnectTones.TransmitInit(
                    state.Parameters.ModemConnectTone);
                state.ModemConnectToneTxTimer = MillisecondsToSamples(75) + 1;
            }

            state.TransmitQueue.Clear();
            state.FskTxOn = false;
            return 0;
        }

        /// <summary>
        /// The Continue
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="parameters">The parameters<see cref="V8Parameters"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int Continue(V8State state, V8Parameters parameters) {
            ArgumentNullException.ThrowIfNull(parameters);
            return Continue(state, parameters.JmCm);
        }

        /// <summary>
        /// The Continue
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="parameters">The parameters<see cref="V8CmJmParameters"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int Continue(V8State state, V8CmJmParameters parameters) {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(parameters);
            state.ThrowIfDisposed();
            state.Parameters.JmCm.CopyFrom(parameters);
            state.Result.JmCm.CopyFrom(parameters);
            state.Proceed = true;
            return 0;
        }

        /// <summary>
        /// The Transmit
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="samples">The samples<see cref="Span{short}"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int Transmit(V8State state, Span<short> samples) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();

            int maxLength = samples.Length;
            int length = 0;

            if (state.ModemConnectToneTxTimer != 0) {
                int initialSilenceMarker = MillisecondsToSamples(75) + 2;
                int toneMarker = MillisecondsToSamples(75) + 1;

                if (state.ModemConnectToneTxTimer == initialSilenceMarker) {
                    if (state.FskTxOn)
                        state.ModemConnectToneTxTimer = 0;
                } else if (state.ModemConnectToneTxTimer == toneMarker) {
                    if (state.AnsamTransmitter == null)
                        throw new InvalidOperationException("The ANSam transmitter is not initialised.");

                    length = ModemConnectTones.Transmit(
                        state.AnsamTransmitter,
                        samples);
                    if (length < maxLength) {
                        state.Log("ANSam or ANSam/ ended");
                        state.ModemConnectToneTxTimer = MillisecondsToSamples(75);
                    }
                } else {
                    length = Math.Min(maxLength, state.ModemConnectToneTxTimer);
                    samples.Slice(0, length).Clear();
                    state.ModemConnectToneTxTimer -= length;
                }
            }

            if (state.FskTxOn && length < maxLength) {
                if (state.V21Transmitter == null)
                    throw new InvalidOperationException("The V.21 transmitter is not initialised.");

                int produced = state.V21Transmitter.Process(samples.Slice(length));
                length += produced;
                if (length < maxLength) {
                    state.Log($"FSK ends ({length}/{maxLength})");
                    state.FskTxOn = false;
                }
            }

            if (state.State != V8ProtocolState.Parked && length < maxLength) {
                samples.Slice(length).Clear();
                length = maxLength;
            }

            return length;
        }

        /// <summary>
        /// The DecodeReceive
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="samples">The samples<see cref="ReadOnlySpan{short}"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int DecodeReceive(V8State state, ReadOnlySpan<short> samples) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();

            ProcessAndLogTone(state, state.AnsamReceiver, samples);
            ProcessAndLogTone(state, state.CallingToneReceiver, samples);
            ProcessAndLogTone(state, state.CngToneReceiver, samples);
            state.V21Receiver?.Process(samples);
            return 0;
        }

        /// <summary>
        /// The Receive
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="samples">The samples<see cref="ReadOnlySpan{short}"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int Receive(V8State state, ReadOnlySpan<short> samples) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();

            int residualSamples = 0;
            ReadOnlySpan<short> current = samples;

            do {
                residualSamples = ProcessReceiveState(state, current);
                if (residualSamples > 0 && residualSamples < current.Length)
                    current = current.Slice(current.Length - residualSamples);
            }
            while (residualSamples > 0 && state.State != V8ProtocolState.Parked);

            return residualSamples;
        }

        /// <summary>
        /// The GetLoggingState
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <returns>The <see cref="Action{string}?"/></returns>
        public static Action<string>? GetLoggingState(V8State state) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();
            return state.LogHandler;
        }

        /// <summary>
        /// The Release
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int Release(V8State state) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();
            state.TransmitQueue.Clear();
            return 0;
        }

        /// <summary>
        /// The Free
        /// </summary>
        /// <param name="state">The state<see cref="V8State?"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int Free(V8State? state) {
            state?.Dispose();
            return 0;
        }

        /// <summary>
        /// The ProcessReceiveState
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="samples">The samples<see cref="ReadOnlySpan{short}"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int ProcessReceiveState(V8State state, ReadOnlySpan<short> samples) {
            int length = samples.Length;
            int residualSamples = 0;
            ModemConnectTone tone;

            switch (state.State) {
                case V8ProtocolState.WaitOneSecond:
                    residualSamples = ProcessTone(state.AnsamReceiver, samples);
                    if ((state.NegotiationTimer -= length) > 0)
                        break;

                    EnsureTransmitter(state).Restart(V8FskChannel.V21Channel1);
                    SendCi(state);
                    state.State = V8ProtocolState.CiOn;
                    state.FskTxOn = true;
                    break;

                case V8ProtocolState.CiOn:
                    residualSamples = ProcessTone(state.AnsamReceiver, samples);
                    tone = GetTone(state.AnsamReceiver);
                    if (tone != ModemConnectTone.None) {
                        HandleCallingModemConnectTone(state, tone);
                        break;
                    }

                    if (!state.FskTxOn) {
                        state.State = V8ProtocolState.CiOff;
                        state.CiTimer = MillisecondsToSamples(TeTimeoutMilliseconds);
                        state.NegotiationTimer = MillisecondsToSamples(5000);
                    }
                    break;

                case V8ProtocolState.CiOff:
                    residualSamples = ProcessTone(state.AnsamReceiver, samples);
                    tone = GetTone(state.AnsamReceiver);
                    if (tone != ModemConnectTone.None) {
                        HandleCallingModemConnectTone(state, tone);
                        break;
                    }

                    if ((state.CiTimer -= length) <= 0) {
                        if (++state.CiRepetitionCount >= 10) {
                            state.Log("Timeout waiting for modem connect tone");
                            state.State = V8ProtocolState.Parked;
                            state.Result.Status = V8Status.Failed;
                            ReportEvent(state);
                        } else {
                            EnsureTransmitter(state).Restart(V8FskChannel.V21Channel1);
                            SendCi(state);
                            state.State = V8ProtocolState.CiOn;
                            state.FskTxOn = true;
                        }
                    }
                    break;

                case V8ProtocolState.AwaitAnsam:
                    residualSamples = ProcessTone(state.AnsamReceiver, samples);
                    tone = GetTone(state.AnsamReceiver);
                    if (tone != ModemConnectTone.None)
                        HandleCallingModemConnectTone(state, tone);
                    break;

                case V8ProtocolState.HeardAnsam:
                    if ((state.CiTimer -= length) <= 0) {
                        DecodeInit(state);
                        state.NegotiationTimer = MillisecondsToSamples(5000);
                        EnsureTransmitter(state).Restart(V8FskChannel.V21Channel1);
                        ConditionallySendV92(state);
                        SendCmJm(state);
                        state.FskTxOn = true;
                        state.State = V8ProtocolState.CmOn;
                    }
                    goto case V8ProtocolState.CmOn;

                case V8ProtocolState.CmOn:
                    residualSamples = ProcessFsk(state, samples);
                    if (state.GotCmJm) {
                        state.Log("JM recognised");
                        EnsureTransmitter(state).Restart(V8FskChannel.V21Channel1);
                        PutBytes(state, new byte[] { 0, 0, 0 });
                        state.Log("<CJ: 00-00");
                        state.State = V8ProtocolState.CjOn;
                        state.FskTxOn = true;
                        break;
                    }

                    if ((state.NegotiationTimer -= length) <= 0) {
                        state.Log("Timeout waiting for JM");
                        state.State = V8ProtocolState.Parked;
                        state.Result.Status = V8Status.Failed;
                        ReportEvent(state);
                    }

                    if (state.TransmitQueue.Count < 10)
                        SendCmJm(state);
                    break;

                case V8ProtocolState.CjOn:
                    residualSamples = ProcessFsk(state, samples);
                    if (!state.FskTxOn) {
                        state.Log("Negotiation succeeded");
                        state.State = V8ProtocolState.Parked;
                        state.Result.Status = V8Status.V8Call;
                        ReportEvent(state);
                    }
                    break;

                case V8ProtocolState.SigC:
                    if ((state.NegotiationTimer -= length) <= 0) {
                        state.Log("Negotiation succeeded");
                        state.State = V8ProtocolState.Parked;
                        state.Result.Status = V8Status.V8Call;
                        ReportEvent(state);
                    }
                    break;

                case V8ProtocolState.CmWait:
                    ProcessTone(state.CallingToneReceiver, samples);
                    ProcessTone(state.CngToneReceiver, samples);
                    tone = GetTone(state.CallingToneReceiver);
                    if (tone != ModemConnectTone.None)
                        HandleAnsweringModemConnectTone(state, tone);
                    else {
                        tone = GetTone(state.CngToneReceiver);
                        if (tone != ModemConnectTone.None)
                            HandleAnsweringModemConnectTone(state, tone);
                    }

                    residualSamples = ProcessFsk(state, samples);
                    if (state.GotCmJm) {
                        state.Log("CM recognised");
                        state.Result.Status = V8Status.V8Offered;
                        ReportEvent(state);

                        if (state.Parameters.GatewayMode) {
                            state.State = V8ProtocolState.PostCmWait;
                        } else {
                            ReplaceTransmitter(state, V8FskChannel.V21Channel2);
                            state.NegotiationTimer = MillisecondsToSamples(5000);
                            state.State = V8ProtocolState.JmOn;
                            SendCmJm(state);
                            state.ModemConnectToneTxTimer = MillisecondsToSamples(75);
                            state.FskTxOn = true;
                        }
                    } else if ((state.NegotiationTimer -= length) <= 0) {
                        state.Log("Timeout waiting for CM");
                        state.State = V8ProtocolState.Parked;
                        state.Result.Status = V8Status.Failed;
                        ReportEvent(state);
                    }
                    break;

                case V8ProtocolState.PostCmWait:
                    if (state.Proceed) {
                        ReplaceTransmitter(state, V8FskChannel.V21Channel2);
                        state.NegotiationTimer = MillisecondsToSamples(5000);
                        state.State = V8ProtocolState.JmOn;
                        SendCmJm(state);
                        state.ModemConnectToneTxTimer = MillisecondsToSamples(75);
                        state.FskTxOn = true;
                    }
                    break;

                case V8ProtocolState.JmOn:
                    residualSamples = ProcessFsk(state, samples);
                    if (state.GotCj) {
                        state.Log("CJ recognised");
                        state.TransmitQueue.Clear();
                        state.NegotiationTimer = MillisecondsToSamples(75);
                        state.State = V8ProtocolState.SigA;
                        break;
                    }

                    if ((state.NegotiationTimer -= length) <= 0) {
                        state.Log("Timeout waiting for CJ");
                        state.State = V8ProtocolState.Parked;
                        state.Result.Status = V8Status.Failed;
                        ReportEvent(state);
                        break;
                    }

                    if (state.TransmitQueue.Count < 10)
                        SendCmJm(state);
                    break;

                case V8ProtocolState.SigA:
                    if (!state.FskTxOn) {
                        state.Log("Negotiation succeeded");
                        state.State = V8ProtocolState.Parked;
                        state.Result.Status = V8Status.V8Call;
                        ReportEvent(state);
                    }
                    break;

                case V8ProtocolState.Parked:
                    residualSamples = length;
                    break;
            }

            return residualSamples;
        }

        /// <summary>
        /// The DecodeInit
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        private static void DecodeInit(V8State state) {
            state.V21Receiver?.Dispose();
            state.V21Receiver = new V8FskReceiver(
                state.CallingParty
                    ? V8FskChannel.V21Channel2
                    : V8FskChannel.V21Channel1,
                bit => PutBit(state, bit));
            state.V21Receiver.SetSignalCutoff(-45.5f);
            state.PreambleType = V8SyncType.Unknown;
            state.BitStream = 0;
            state.CmJmLength = 0;
            state.GotCi = false;
            state.GotCmJm = false;
            state.GotCj = false;
            state.ZeroByteCount = 0;
            state.ReceiveDataPointer = 0;
            state.BitCount = 0;
            Array.Clear(state.ReceiveData, 0, state.ReceiveData.Length);
            Array.Clear(state.CmJmData, 0, state.CmJmData.Length);
        }

        /// <summary>
        /// The PutBit
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="bit">The bit<see cref="int"/></param>
        private static void PutBit(V8State state, int bit) {
            if (bit < 0)
                return;

            state.BitStream = (state.BitStream >> 1) | ((uint)(bit & 1) << 19);
            V8SyncType newPreamble = state.BitStream switch {
                0x803FFu => V8SyncType.Ci,
                0xF03FFu => V8SyncType.CmJm,
                0xAABFFu => V8SyncType.V92,
                _ => V8SyncType.Unknown
            };

            if (newPreamble != V8SyncType.Unknown) {
                if (state.PreambleType != V8SyncType.Unknown) {
                    string tag = state.PreambleType switch {
                        V8SyncType.Ci => ">CI: ",
                        V8SyncType.CmJm => state.CallingParty ? ">JM: " : ">CM: ",
                        V8SyncType.V92 => ">V.92: ",
                        _ => ">??: "
                    };
                    state.Log(tag + FormatBytes(
                        state.ReceiveData.AsSpan(0, state.ReceiveDataPointer)));
                }

                switch (state.PreambleType) {
                    case V8SyncType.Ci:
                        DecodeCi(state);
                        break;
                    case V8SyncType.CmJm:
                        DecodeCmJm(state);
                        break;
                }

                state.PreambleType = newPreamble;
                state.BitCount = 0;
                state.ReceiveDataPointer = 0;
            }

            if (state.PreambleType == V8SyncType.Unknown)
                return;

            state.BitCount++;
            if ((state.BitStream & 0x80400u) == 0x80000u && state.BitCount >= 10) {
                byte data = (byte)((state.BitStream >> 11) & 0xFFu);
                if (data == 0) {
                    if (++state.ZeroByteCount == 3)
                        state.GotCj = true;
                } else {
                    state.ZeroByteCount = 0;
                }

                if (state.ReceiveDataPointer < state.ReceiveData.Length - 1)
                    state.ReceiveData[state.ReceiveDataPointer++] = data;
                state.BitCount = 0;
            }
        }

        /// <summary>
        /// The DecodeCi
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        private static void DecodeCi(V8State state) {
            if (state.GotCi)
                return;

            if (state.ReceiveDataPointer > 0 &&
                (state.ReceiveData[0] & 0x1F) == CallFunctionTag) {
                state.Result.JmCm.CallFunction =
                    (V8CallFunction)((state.ReceiveData[0] >> 5) & 0x07);
                state.Log(CallFunctionToString(state.Result.JmCm.CallFunction));
            }

            state.GotCi = true;
            state.Result.Status = V8Status.CallFunctionReceived;
            ReportEvent(state);
        }

        /// <summary>
        /// The DecodeCmJm
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        private static void DecodeCmJm(V8State state) {
            if (state.GotCmJm)
                return;

            ReadOnlySpan<byte> received = state.ReceiveData.AsSpan(
                0,
                state.ReceiveDataPointer);
            if (state.CmJmLength <= 0 ||
                state.CmJmLength != received.Length ||
                !state.CmJmData.AsSpan(0, state.CmJmLength).SequenceEqual(received)) {
                state.CmJmLength = received.Length;
                received.CopyTo(state.CmJmData);
                return;
            }

            state.GotCmJm = true;
            state.Log("Decoding");
            state.Result.JmCm.Modulations = V8Modulation.None;
            int pointer = 0;

            while (pointer < state.CmJmLength && state.CmJmData[pointer] != 0) {
                int originalPointer = pointer;
                int tag = state.CmJmData[pointer] & 0x1F;
                switch (tag) {
                    case CallFunctionTag:
                        pointer = ProcessCallFunction(state, pointer);
                        break;
                    case ModulationTag:
                        pointer = ProcessModulationMode(state, pointer);
                        break;
                    case ProtocolsTag:
                        pointer = ProcessProtocols(state, pointer);
                        break;
                    case PstnAccessTag:
                        pointer = ProcessPstnAccess(state, pointer);
                        break;
                    case NsfTag:
                        pointer = ProcessNonStandardFacilities(state, pointer);
                        break;
                    case PcmModemAvailabilityTag:
                        pointer = ProcessPcmModemAvailability(state, pointer);
                        break;
                    case T66Tag:
                        pointer = ProcessT66(state, pointer);
                        break;
                    default:
                        pointer++;
                        break;
                }

                while (pointer < state.CmJmLength &&
                    (state.CmJmData[pointer] & 0x38) == 0x10) {
                    pointer++;
                }

                if (pointer <= originalPointer)
                    pointer = originalPointer + 1;
            }
        }

        /// <summary>
        /// The ProcessCallFunction
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="pointer">The pointer<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int ProcessCallFunction(V8State state, int pointer) {
            state.Result.JmCm.CallFunction =
                (V8CallFunction)((state.CmJmData[pointer] >> 5) & 0x07);
            state.Log(CallFunctionToString(state.Result.JmCm.CallFunction));
            return pointer + 1;
        }

        /// <summary>
        /// The ProcessModulationMode
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="pointer">The pointer<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int ProcessModulationMode(V8State state, int pointer) {
            V8Modulation modulations = V8Modulation.None;
            state.ModulationBytes = 1;

            byte first = state.CmJmData[pointer++];
            if ((first & 0x80) != 0)
                modulations |= V8Modulation.V34HalfDuplex;
            if ((first & 0x40) != 0)
                modulations |= V8Modulation.V34;
            if ((first & 0x20) != 0)
                modulations |= V8Modulation.V90;

            if (pointer < state.CmJmLength &&
                (state.CmJmData[pointer] & 0x38) == 0x10) {
                state.ModulationBytes++;
                byte second = state.CmJmData[pointer++];
                if ((second & 0x80) != 0)
                    modulations |= V8Modulation.V27Ter;
                if ((second & 0x40) != 0)
                    modulations |= V8Modulation.V29;
                if ((second & 0x04) != 0)
                    modulations |= V8Modulation.V17;
                if ((second & 0x02) != 0)
                    modulations |= V8Modulation.V22;
                if ((second & 0x01) != 0)
                    modulations |= V8Modulation.V32;

                if (pointer < state.CmJmLength &&
                    (state.CmJmData[pointer] & 0x38) == 0x10) {
                    state.ModulationBytes++;
                    byte third = state.CmJmData[pointer++];
                    if ((third & 0x80) != 0)
                        modulations |= V8Modulation.V21;
                    if ((third & 0x40) != 0)
                        modulations |= V8Modulation.V23HalfDuplex;
                    if ((third & 0x04) != 0)
                        modulations |= V8Modulation.V23;
                    if ((third & 0x02) != 0)
                        modulations |= V8Modulation.V26Bis;
                    if ((third & 0x01) != 0)
                        modulations |= V8Modulation.V26Ter;
                }
            }

            state.Result.JmCm.Modulations = modulations;
            LogSupportedModulations(state, modulations);
            return pointer;
        }

        /// <summary>
        /// The ProcessProtocols
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="pointer">The pointer<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int ProcessProtocols(V8State state, int pointer) {
            state.Result.JmCm.Protocols =
                (V8Protocol)((state.CmJmData[pointer] >> 5) & 0x07);
            state.Log(ProtocolToString(state.Result.JmCm.Protocols));
            return pointer + 1;
        }

        /// <summary>
        /// The ProcessPstnAccess
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="pointer">The pointer<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int ProcessPstnAccess(V8State state, int pointer) {
            state.Result.JmCm.PstnAccess =
                (V8PstnAccess)((state.CmJmData[pointer] >> 5) & 0x07);
            state.Log(PstnAccessToString(state.Result.JmCm.PstnAccess));
            return pointer + 1;
        }

        /// <summary>
        /// The ProcessNonStandardFacilities
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="pointer">The pointer<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int ProcessNonStandardFacilities(V8State state, int pointer) {
            state.Result.JmCm.Nsf = (state.CmJmData[pointer] >> 5) & 0x07;
            state.Log(NsfToString(state.Result.JmCm.Nsf));
            return pointer + 1;
        }

        /// <summary>
        /// The ProcessPcmModemAvailability
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="pointer">The pointer<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int ProcessPcmModemAvailability(V8State state, int pointer) {
            state.Result.JmCm.PcmModemAvailability =
                (V8PcmModemAvailability)((state.CmJmData[pointer] >> 5) & 0x07);
            state.Log(PcmModemAvailabilityToString(
                state.Result.JmCm.PcmModemAvailability));
            return pointer + 1;
        }

        /// <summary>
        /// The ProcessT66
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="pointer">The pointer<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int ProcessT66(V8State state, int pointer) {
            state.Result.JmCm.T66 = (state.CmJmData[pointer] >> 5) & 0x07;
            state.Log(T66ToString(state.Result.JmCm.T66));
            return pointer + 1;
        }

        /// <summary>
        /// The PutPreamble
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        private static void PutPreamble(V8State state) {
            for (int index = 0; index < 10; index++)
                state.TransmitQueue.Enqueue(1);
        }

        /// <summary>
        /// The PutBytes
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="bytes">The bytes<see cref="ReadOnlySpan{byte}"/></param>
        private static void PutBytes(V8State state, ReadOnlySpan<byte> bytes) {
            foreach (byte value in bytes) {
                state.TransmitQueue.Enqueue(0);
                byte remaining = value;
                for (int bit = 0; bit < 8; bit++) {
                    state.TransmitQueue.Enqueue((byte)(remaining & 1));
                    remaining >>= 1;
                }
                state.TransmitQueue.Enqueue(1);
            }
        }

        /// <summary>
        /// The SendCmJm
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        private static void SendCmJm(V8State state) {
            PutPreamble(state);
            Span<byte> buffer = stackalloc byte[10];
            int pointer = 0;
            buffer[pointer++] = CmJmSyncOctet;
            buffer[pointer++] = (byte)(((int)state.Result.JmCm.CallFunction << 5) |
                CallFunctionTag);

            V8Modulation offered = state.Result.JmCm.Modulations;
            int modulationByteCount = 0;
            int value = ModulationTag;
            if ((offered & V8Modulation.V90) != 0)
                value |= 0x20;
            if ((offered & V8Modulation.V34) != 0)
                value |= 0x40;
            if ((offered & V8Modulation.V34HalfDuplex) != 0)
                value |= 0x80;
            buffer[pointer++] = (byte)value;

            if (++modulationByteCount < state.ModulationBytes) {
                value = 0x10;
                if ((offered & V8Modulation.V32) != 0)
                    value |= 0x01;
                if ((offered & V8Modulation.V22) != 0)
                    value |= 0x02;
                if ((offered & V8Modulation.V17) != 0)
                    value |= 0x04;
                if ((offered & V8Modulation.V29) != 0)
                    value |= 0x40;
                if ((offered & V8Modulation.V27Ter) != 0)
                    value |= 0x80;
                buffer[pointer++] = (byte)value;
            }

            if (++modulationByteCount < state.ModulationBytes) {
                value = 0x10;
                if ((offered & V8Modulation.V26Ter) != 0)
                    value |= 0x01;
                if ((offered & V8Modulation.V26Bis) != 0)
                    value |= 0x02;
                if ((offered & V8Modulation.V23) != 0)
                    value |= 0x04;
                if ((offered & V8Modulation.V23HalfDuplex) != 0)
                    value |= 0x40;
                if ((offered & V8Modulation.V21) != 0)
                    value |= 0x80;
                buffer[pointer++] = (byte)value;
            }

            if (state.Parameters.JmCm.Protocols != V8Protocol.None) {
                buffer[pointer++] = (byte)(((int)state.Parameters.JmCm.Protocols << 5) |
                    ProtocolsTag);
            }

            if (state.Parameters.JmCm.PstnAccess != V8PstnAccess.None) {
                buffer[pointer++] = (byte)(((int)state.Parameters.JmCm.PstnAccess << 5) |
                    PstnAccessTag);
            }

            if (state.Parameters.JmCm.PcmModemAvailability !=
                V8PcmModemAvailability.None) {
                buffer[pointer++] = (byte)(((int)state.Parameters.JmCm.PcmModemAvailability << 5) |
                    PcmModemAvailabilityTag);
            }

            if (state.Parameters.JmCm.T66 >= 0) {
                buffer[pointer++] = (byte)((state.Parameters.JmCm.T66 << 5) |
                    T66Tag);
            }

            state.Log((state.CallingParty ? "<CM: " : "<JM: ") +
                FormatBytes(buffer.Slice(1, pointer - 1)));
            PutBytes(state, buffer.Slice(0, pointer));
        }

        /// <summary>
        /// The ConditionallySendV92
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        private static void ConditionallySendV92(V8State state) {
            if (state.Parameters.V92 < 0)
                return;

            Span<byte> buffer = stackalloc byte[2];
            buffer[0] = V92SyncOctet;
            buffer[1] = unchecked((byte)state.Parameters.V92);
            for (int index = 0; index < 2; index++) {
                PutPreamble(state);
                state.Log("<V.92: " + FormatBytes(buffer.Slice(1, 1)));
                PutBytes(state, buffer);
            }
        }

        /// <summary>
        /// The SendCi
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        private static void SendCi(V8State state) {
            Span<byte> buffer = stackalloc byte[2];
            buffer[0] = CiSyncOctet;
            buffer[1] = (byte)(((int)state.Result.JmCm.CallFunction << 5) |
                CallFunctionTag);

            for (int index = 0; index < 4; index++) {
                PutPreamble(state);
                state.Log("<CI: " + FormatBytes(buffer.Slice(1, 1)));
                PutBytes(state, buffer);
            }
        }

        /// <summary>
        /// The HandleCallingModemConnectTone
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="tone">The tone<see cref="ModemConnectTone"/></param>
        private static void HandleCallingModemConnectTone(
            V8State state,
            ModemConnectTone tone) {
            state.Result.ModemConnectTone = tone;
            state.Log($"'{ModemConnectTones.ToneToString(tone)}' recognised");
            if (tone == ModemConnectTone.Ansam ||
                tone == ModemConnectTone.AnsamWithPhaseReversals) {
                state.State = V8ProtocolState.HeardAnsam;
                state.CiTimer = MillisecondsToSamples(2 * TeTimeoutMilliseconds);
                state.NegotiationTimer = MillisecondsToSamples(5000);
                DecodeInit(state);
            } else {
                state.Log("Non-V.8 modem connect tone detected");
                state.State = V8ProtocolState.Parked;
                state.Result.Status = V8Status.NonV8Call;
                ReportEvent(state);
            }
        }

        /// <summary>
        /// The HandleAnsweringModemConnectTone
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="tone">The tone<see cref="ModemConnectTone"/></param>
        private static void HandleAnsweringModemConnectTone(
            V8State state,
            ModemConnectTone tone) {
            state.Result.ModemConnectTone = tone;
            state.Log($"'{ModemConnectTones.ToneToString(tone)}' recognised");
            if (tone == ModemConnectTone.CallingTone) {
                state.State = V8ProtocolState.Parked;
                state.Result.Status = V8Status.CallingToneReceived;
                ReportEvent(state);
            } else if (tone == ModemConnectTone.FaxCng) {
                state.State = V8ProtocolState.Parked;
                state.Result.Status = V8Status.FaxCngToneReceived;
                ReportEvent(state);
            } else {
                state.Log("Non-V.8 modem connect tone detected");
                state.State = V8ProtocolState.Parked;
                state.Result.Status = V8Status.Failed;
                ReportEvent(state);
            }
        }

        /// <summary>
        /// The ReportEvent
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int ReportEvent(V8State state) {
            state.ResultHandler?.Invoke(state.ResultHandlerUserData, state.Result);
            return 0;
        }

        /// <summary>
        /// The GetBit
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int GetBit(V8State state) {
            return state.TransmitQueue.Count > 0
                ? state.TransmitQueue.Dequeue()
                : V8FskTransmitter.SignalStatusEndOfData;
        }

        /// <summary>
        /// The ProcessFsk
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="samples">The samples<see cref="ReadOnlySpan{short}"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int ProcessFsk(V8State state, ReadOnlySpan<short> samples) {
            return state.V21Receiver?.Process(samples) ?? 0;
        }

        /// <summary>
        /// The ProcessTone
        /// </summary>
        /// <param name="receiver">The receiver<see cref="ModemConnectTonesRxState?"/></param>
        /// <param name="samples">The samples<see cref="ReadOnlySpan{short}"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int ProcessTone(
            ModemConnectTonesRxState? receiver,
            ReadOnlySpan<short> samples) {
            return receiver == null ? 0 : ModemConnectTones.Receive(receiver, samples);
        }

        /// <summary>
        /// The GetTone
        /// </summary>
        /// <param name="receiver">The receiver<see cref="ModemConnectTonesRxState?"/></param>
        /// <returns>The <see cref="ModemConnectTone"/></returns>
        private static ModemConnectTone GetTone(ModemConnectTonesRxState? receiver) {
            return receiver == null
                ? ModemConnectTone.None
                : ModemConnectTones.ReceiveGet(receiver);
        }

        /// <summary>
        /// The ProcessAndLogTone
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="receiver">The receiver<see cref="ModemConnectTonesRxState?"/></param>
        /// <param name="samples">The samples<see cref="ReadOnlySpan{short}"/></param>
        private static void ProcessAndLogTone(
            V8State state,
            ModemConnectTonesRxState? receiver,
            ReadOnlySpan<short> samples) {
            if (receiver == null)
                return;

            ModemConnectTones.Receive(receiver, samples);
            ModemConnectTone tone = ModemConnectTones.ReceiveGet(receiver);
            if (tone != ModemConnectTone.None) {
                state.Log($"'{ModemConnectTones.ToneToString(tone)}' recognised");
            }
        }

        /// <summary>
        /// The EnsureTransmitter
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <returns>The <see cref="V8FskTransmitter"/></returns>
        private static V8FskTransmitter EnsureTransmitter(V8State state) {
            state.V21Transmitter ??= new V8FskTransmitter(
                V8FskChannel.V21Channel1,
                () => GetBit(state));
            return state.V21Transmitter;
        }

        /// <summary>
        /// The ReplaceTransmitter
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="channel">The channel<see cref="V8FskChannel"/></param>
        private static void ReplaceTransmitter(V8State state, V8FskChannel channel) {
            state.V21Transmitter?.Dispose();
            state.V21Transmitter = new V8FskTransmitter(
                channel,
                () => GetBit(state));
        }

        /// <summary>
        /// The ReplaceReceiver
        /// </summary>
        /// <param name="receiver">The receiver<see cref="ModemConnectTonesRxState?"/></param>
        /// <param name="tone">The tone<see cref="ModemConnectTone"/></param>
        /// <returns>The <see cref="ModemConnectTonesRxState"/></returns>
        private static ModemConnectTonesRxState ReplaceReceiver(
            ModemConnectTonesRxState? receiver,
            ModemConnectTone tone) {
            receiver?.Dispose();
            return ModemConnectTones.ReceiveInit(tone);
        }

        /// <summary>
        /// The ResetResult
        /// </summary>
        /// <param name="result">The result<see cref="V8Parameters"/></param>
        private static void ResetResult(V8Parameters result) {
            result.Status = V8Status.InProgress;
            result.GatewayMode = false;
            result.ModemConnectTone = ModemConnectTone.None;
            result.SendCi = false;
            result.V92 = 0;
            result.JmCm.CallFunction = V8CallFunction.Tbs;
            result.JmCm.Modulations = V8Modulation.None;
            result.JmCm.Protocols = V8Protocol.None;
            result.JmCm.PstnAccess = V8PstnAccess.None;
            result.JmCm.Nsf = 0;
            result.JmCm.PcmModemAvailability = V8PcmModemAvailability.None;
            result.JmCm.T66 = 0;
        }

        /// <summary>
        /// The MillisecondsToSamples
        /// </summary>
        /// <param name="milliseconds">The milliseconds<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int MillisecondsToSamples(int milliseconds) {
            return milliseconds * (SampleRate / 1000);
        }

        /// <summary>
        /// The FormatBytes
        /// </summary>
        /// <param name="bytes">The bytes<see cref="ReadOnlySpan{byte}"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string FormatBytes(ReadOnlySpan<byte> bytes) {
            if (bytes.IsEmpty)
                return string.Empty;

            var text = new StringBuilder(bytes.Length * 3 - 1);
            for (int index = 0; index < bytes.Length; index++) {
                if (index > 0)
                    text.Append('-');
                text.Append(bytes[index].ToString("X2"));
            }
            return text.ToString();
        }
    }

    /// <summary>
    /// Minimal V.21 FSK transmitter required by V.8. It preserves the native
    /// phase-continuous DDS and 1/100-baud timing behaviour
    /// </summary>
    internal sealed class V8FskTransmitter : IDisposable {
        /// <summary>
        /// Defines the SignalStatusEndOfData
        /// </summary>
        internal const int SignalStatusEndOfData = -3;

        /// <summary>
        /// Defines the _getBit
        /// </summary>
        private readonly Func<int> _getBit;

        /// <summary>
        /// Defines the _phaseRates
        /// </summary>
        private readonly int[] _phaseRates = new int[2];

        /// <summary>
        /// Defines the _baudRate
        /// </summary>
        private int _baudRate;

        /// <summary>
        /// Defines the _phaseAccumulator
        /// </summary>
        private uint _phaseAccumulator;

        /// <summary>
        /// Defines the _baudFraction
        /// </summary>
        private int _baudFraction;

        /// <summary>
        /// Defines the _currentPhaseRate
        /// </summary>
        private int _currentPhaseRate;

        /// <summary>
        /// Defines the _scaling
        /// </summary>
        private short _scaling;

        /// <summary>
        /// Defines the _shutdown
        /// </summary>
        private bool _shutdown;

        /// <summary>
        /// Defines the _disposed
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="V8FskTransmitter"/> class.
        /// </summary>
        /// <param name="channel">The channel<see cref="V8FskChannel"/></param>
        /// <param name="getBit">The getBit<see cref="Func{int}"/></param>
        internal V8FskTransmitter(V8FskChannel channel, Func<int> getBit) {
            _getBit = getBit ?? throw new ArgumentNullException(nameof(getBit));
            Restart(channel);
        }

        /// <summary>
        /// The Restart
        /// </summary>
        /// <param name="channel">The channel<see cref="V8FskChannel"/></param>
        internal void Restart(V8FskChannel channel) {
            ThrowIfDisposed();
            GetChannelParameters(
                channel,
                out int frequencyZero,
                out int frequencyOne,
                out int transmitLevel,
                out _baudRate);
            _phaseRates[0] = Dds.PhaseRate(frequencyZero);
            _phaseRates[1] = Dds.PhaseRate(frequencyOne);
            _scaling = Dds.ScalingDbm0(transmitLevel);
            _phaseAccumulator = 0;
            _baudFraction = 0;
            _currentPhaseRate = _phaseRates[1];
            _shutdown = false;
        }

        /// <summary>
        /// The Process
        /// </summary>
        /// <param name="samples">The samples<see cref="Span{short}"/></param>
        /// <returns>The <see cref="int"/></returns>
        internal int Process(Span<short> samples) {
            ThrowIfDisposed();
            if (_shutdown)
                return 0;

            int sample;
            for (sample = 0; sample < samples.Length; sample++) {
                if ((_baudFraction += _baudRate) >= V8.SampleRate * 100) {
                    _baudFraction -= V8.SampleRate * 100;
                    int bit = _getBit();
                    if (bit == SignalStatusEndOfData) {
                        _shutdown = true;
                        break;
                    }
                    _currentPhaseRate = _phaseRates[bit & 1];
                }

                samples[sample] = Dds.dds_mod(
                    ref _phaseAccumulator,
                    _currentPhaseRate,
                    _scaling, 0);
            }
            return sample;
        }

        /// <summary>
        /// The Dispose
        /// </summary>
        public void Dispose() {
            if (_disposed)
                return;
            Array.Clear(_phaseRates, 0, _phaseRates.Length);
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// The ThrowIfDisposed
        /// </summary>
        private void ThrowIfDisposed() {
            if (_disposed)
                throw new ObjectDisposedException(nameof(V8FskTransmitter));
        }

        /// <summary>
        /// The GetChannelParameters
        /// </summary>
        /// <param name="channel">The channel<see cref="V8FskChannel"/></param>
        /// <param name="frequencyZero">The frequencyZero<see cref="int"/></param>
        /// <param name="frequencyOne">The frequencyOne<see cref="int"/></param>
        /// <param name="transmitLevel">The transmitLevel<see cref="int"/></param>
        /// <param name="baudRate">The baudRate<see cref="int"/></param>
        internal static void GetChannelParameters(
            V8FskChannel channel,
            out int frequencyZero,
            out int frequencyOne,
            out int transmitLevel,
            out int baudRate) {
            switch (channel) {
                case V8FskChannel.V21Channel1:
                    frequencyZero = 1180;
                    frequencyOne = 980;
                    break;
                case V8FskChannel.V21Channel2:
                    frequencyZero = 1850;
                    frequencyOne = 1650;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(channel));
            }

            transmitLevel = -14;
            baudRate = 300 * 100;
        }
    }

    /// <summary>
    /// Minimal asynchronous V.21 FSK receiver required by V.8. It uses the
    /// same sliding quadrature correlator as the native FSK receive path
    /// </summary>
    internal sealed class V8FskReceiver : IDisposable {
        /// <summary>
        /// Defines the SignalStatusCarrierDown
        /// </summary>
        private const int SignalStatusCarrierDown = -1;

        /// <summary>
        /// Defines the SignalStatusCarrierUp
        /// </summary>
        private const int SignalStatusCarrierUp = -2;

        /// <summary>
        /// Defines the MaximumWindowLength
        /// </summary>
        private const int MaximumWindowLength = 128;

        /// <summary>
        /// Defines the _putBit
        /// </summary>
        private readonly Action<int> _putBit;

        /// <summary>
        /// Defines the _phaseRates
        /// </summary>
        private readonly int[] _phaseRates = new int[2];

        /// <summary>
        /// Defines the _phaseAccumulators
        /// </summary>
        private readonly uint[] _phaseAccumulators = new uint[2];

        /// <summary>
        /// Defines the _window
        /// </summary>
        private readonly ComplexInt32[][] _window =
        {
            new ComplexInt32[MaximumWindowLength],
            new ComplexInt32[MaximumWindowLength]
        };

        /// <summary>
        /// Defines the _dot
        /// </summary>
        private readonly ComplexInt32[] _dot = new ComplexInt32[2];

        /// <summary>
        /// Defines the _power
        /// </summary>
        private readonly PowerMeterState _power = new PowerMeterState(4);

        /// <summary>
        /// Defines the _baudRate
        /// </summary>
        private int _baudRate;

        /// <summary>
        /// Defines the _carrierOnPower
        /// </summary>
        private int _carrierOnPower;

        /// <summary>
        /// Defines the _carrierOffPower
        /// </summary>
        private int _carrierOffPower;

        /// <summary>
        /// Defines the _lastSample
        /// </summary>
        private short _lastSample;

        /// <summary>
        /// Defines the _signalPresent
        /// </summary>
        private int _signalPresent;

        /// <summary>
        /// Defines the _correlationSpan
        /// </summary>
        private int _correlationSpan;

        /// <summary>
        /// Defines the _bufferPosition
        /// </summary>
        private int _bufferPosition;

        /// <summary>
        /// Defines the _baudPhase
        /// </summary>
        private int _baudPhase;

        /// <summary>
        /// Defines the _lastBit
        /// </summary>
        private int _lastBit;

        /// <summary>
        /// Defines the _scalingShift
        /// </summary>
        private int _scalingShift;

        /// <summary>
        /// Defines the _disposed
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="V8FskReceiver"/> class.
        /// </summary>
        /// <param name="channel">The channel<see cref="V8FskChannel"/></param>
        /// <param name="putBit">The putBit<see cref="Action{int}"/></param>
        internal V8FskReceiver(V8FskChannel channel, Action<int> putBit) {
            _putBit = putBit ?? throw new ArgumentNullException(nameof(putBit));
            Restart(channel);
        }

        /// <summary>
        /// The SetSignalCutoff
        /// </summary>
        /// <param name="cutoff">The cutoff<see cref="float"/></param>
        internal void SetSignalCutoff(float cutoff) {
            ThrowIfDisposed();
            _carrierOnPower = PowerMeter.LevelDbm0(cutoff + 2.5f - 5.3f);
            _carrierOffPower = PowerMeter.LevelDbm0(cutoff - 2.5f - 5.3f);
        }

        /// <summary>
        /// The Process
        /// </summary>
        /// <param name="samples">The samples<see cref="ReadOnlySpan{short}"/></param>
        /// <returns>The <see cref="int"/></returns>
        internal int Process(ReadOnlySpan<short> samples) {
            ThrowIfDisposed();
            int bufferPosition = _bufferPosition;

            unchecked {
                for (int index = 0; index < samples.Length; index++) {
                    int sum0 = UpdateCorrelator(0, bufferPosition, samples[index]);
                    int sum1 = UpdateCorrelator(1, bufferPosition, samples[index]);

                    short current = (short)(samples[index] >> 1);
                    int power = _power.Update((short)(current - _lastSample));
                    _lastSample = current;

                    if (_signalPresent != 0) {
                        if (power < _carrierOffPower && --_signalPresent <= 0) {
                            _putBit(SignalStatusCarrierDown);
                            _baudPhase = 0;
                            continue;
                        }
                    } else {
                        if (power < _carrierOnPower) {
                            _baudPhase = 0;
                            continue;
                        }

                        if (_baudPhase < (_correlationSpan >> 1) - 30) {
                            _baudPhase++;
                            continue;
                        }

                        _signalPresent = 1;
                        _baudPhase = 0;
                        _lastBit = 0;
                        _putBit(SignalStatusCarrierUp);
                    }

                    int baudState = sum0 < sum1 ? 1 : 0;
                    if (_lastBit != baudState) {
                        _lastBit = baudState;
                        _baudPhase = V8.SampleRate * 50;
                    }

                    _baudPhase += _baudRate;
                    if (_baudPhase >= V8.SampleRate * 100) {
                        _baudPhase -= V8.SampleRate * 100;
                        _putBit(baudState);
                    }

                    if (++bufferPosition >= _correlationSpan)
                        bufferPosition = 0;
                }
            }

            _bufferPosition = bufferPosition;
            return 0;
        }

        /// <summary>
        /// The Dispose
        /// </summary>
        public void Dispose() {
            if (_disposed)
                return;

            Array.Clear(_phaseRates, 0, _phaseRates.Length);
            Array.Clear(_phaseAccumulators, 0, _phaseAccumulators.Length);
            Array.Clear(_window[0], 0, _window[0].Length);
            Array.Clear(_window[1], 0, _window[1].Length);
            Array.Clear(_dot, 0, _dot.Length);
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// The Restart
        /// </summary>
        /// <param name="channel">The channel<see cref="V8FskChannel"/></param>
        private void Restart(V8FskChannel channel) {
            V8FskTransmitter.GetChannelParameters(
                channel,
                out int frequencyZero,
                out int frequencyOne,
                out _,
                out _baudRate);
            _phaseRates[0] = Dds.PhaseRate(frequencyZero);
            _phaseRates[1] = Dds.PhaseRate(frequencyOne);
            _phaseAccumulators[0] = 0;
            _phaseAccumulators[1] = 0;
            _lastSample = 0;
            _correlationSpan = V8.SampleRate * 100 / _baudRate;
            if (_correlationSpan > MaximumWindowLength)
                _correlationSpan = MaximumWindowLength;

            _scalingShift = 0;
            int chop = _correlationSpan;
            while (chop != 0) {
                _scalingShift++;
                chop >>= 1;
            }

            _baudPhase = 0;
            _lastBit = 0;
            _power.Initialize(4);
            _signalPresent = 0;
            _bufferPosition = 0;
            SetSignalCutoff(-30.0f);
        }

        /// <summary>
        /// The UpdateCorrelator
        /// </summary>
        /// <param name="correlator">The correlator<see cref="int"/></param>
        /// <param name="position">The position<see cref="int"/></param>
        /// <param name="sample">The sample<see cref="short"/></param>
        /// <returns>The <see cref="int"/></returns>
        private int UpdateCorrelator(int correlator, int position, short sample) {
            ComplexInt32 old = _window[correlator][position];
            ComplexInt32 dot = _dot[correlator];
            dot.Re = unchecked(dot.Re - old.Re);
            dot.Im = unchecked(dot.Im - old.Im);

            DdsComplexInt16 phase = Dds.dds_complexi16(
                ref _phaseAccumulators[correlator],
                _phaseRates[correlator]);
            var current = new ComplexInt32 {
                Re = unchecked((phase.Real * sample) >> _scalingShift),
                Im = unchecked((phase.Imaginary * sample) >> _scalingShift)
            };
            _window[correlator][position] = current;
            dot.Re = unchecked(dot.Re + current.Re);
            dot.Im = unchecked(dot.Im + current.Im);
            _dot[correlator] = dot;

            int component = dot.Re >> 15;
            int sum = unchecked(component * component);
            component = dot.Im >> 15;
            return unchecked(sum + component * component);
        }

        /// <summary>
        /// The ThrowIfDisposed
        /// </summary>
        private void ThrowIfDisposed() {
            if (_disposed)
                throw new ObjectDisposedException(nameof(V8FskReceiver));
        }
    }

    /// <summary>Compatibility facade retaining the public C function names.</summary>

    /// <summary>
    /// Defines the <see cref="V8Api" />
    /// </summary>
    public static class V8Api {
        /// <summary>
        /// The v8_init
        /// </summary>
        /// <param name="state">The state<see cref="V8State?"/></param>
        /// <param name="callingParty">The callingParty<see cref="bool"/></param>
        /// <param name="parameters">The parameters<see cref="V8Parameters"/></param>
        /// <param name="resultHandler">The resultHandler<see cref="V8ResultHandler?"/></param>
        /// <param name="userData">The userData<see cref="object?"/></param>
        /// <returns>The <see cref="V8State"/></returns>
        public static V8State v8_init(
            V8State? state,
            bool callingParty,
            V8Parameters parameters,
            V8ResultHandler? resultHandler,
            object? userData) {
            return V8.Init(state, callingParty, parameters, resultHandler, userData);
        }

        /// <summary>
        /// The v8_restart
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="callingParty">The callingParty<see cref="bool"/></param>
        /// <param name="parameters">The parameters<see cref="V8Parameters"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int v8_restart(
            V8State state,
            bool callingParty,
            V8Parameters parameters) {
            return V8.Restart(state, callingParty, parameters);
        }

        /// <summary>
        /// The v8_continue
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="parameters">The parameters<see cref="V8Parameters"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int v8_continue(V8State state, V8Parameters parameters) {
            return V8.Continue(state, parameters);
        }

        /// <summary>
        /// The v8_continue
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="parameters">The parameters<see cref="V8CmJmParameters"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int v8_continue(V8State state, V8CmJmParameters parameters) {
            return V8.Continue(state, parameters);
        }

        /// <summary>
        /// The v8_tx
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="samples">The samples<see cref="short[]"/></param>
        /// <param name="maxLength">The maxLength<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int v8_tx(V8State state, short[] samples, int maxLength) {
            ArgumentNullException.ThrowIfNull(samples);
            if ((uint)maxLength > (uint)samples.Length)
                throw new ArgumentOutOfRangeException(nameof(maxLength));
            return V8.Transmit(state, samples.AsSpan(0, maxLength));
        }

        /// <summary>
        /// The v8_rx
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="samples">The samples<see cref="short[]"/></param>
        /// <param name="length">The length<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int v8_rx(V8State state, short[] samples, int length) {
            ArgumentNullException.ThrowIfNull(samples);
            if ((uint)length > (uint)samples.Length)
                throw new ArgumentOutOfRangeException(nameof(length));
            return V8.Receive(state, samples.AsSpan(0, length));
        }

        /// <summary>
        /// The v8_decode_rx
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="samples">The samples<see cref="short[]"/></param>
        /// <param name="length">The length<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int v8_decode_rx(V8State state, short[] samples, int length) {
            ArgumentNullException.ThrowIfNull(samples);
            if ((uint)length > (uint)samples.Length)
                throw new ArgumentOutOfRangeException(nameof(length));
            return V8.DecodeReceive(state, samples.AsSpan(0, length));
        }

        /// <summary>
        /// The v8_release
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int v8_release(V8State state) => V8.Release(state);

        /// <summary>
        /// The v8_free
        /// </summary>
        /// <param name="state">The state<see cref="V8State?"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int v8_free(V8State? state) => V8.Free(state);

        /// <summary>
        /// The v8_get_logging_state
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <returns>The <see cref="Action{string}?"/></returns>
        public static Action<string>? v8_get_logging_state(V8State state) =>
            V8.GetLoggingState(state);

        /// <summary>
        /// The v8_log_supported_modulations
        /// </summary>
        /// <param name="state">The state<see cref="V8State"/></param>
        /// <param name="modulationSchemes">The modulationSchemes<see cref="int"/></param>
        public static void v8_log_supported_modulations(
            V8State state,
            int modulationSchemes) {
            V8.LogSupportedModulations(state, (V8Modulation)(uint)modulationSchemes);
        }

        /// <summary>
        /// The v8_status_to_str
        /// </summary>
        /// <param name="status">The status<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string v8_status_to_str(int status) =>
            V8.StatusToString((V8Status)status);

        /// <summary>
        /// The v8_call_function_to_str
        /// </summary>
        /// <param name="callFunction">The callFunction<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string v8_call_function_to_str(int callFunction) =>
            V8.CallFunctionToString((V8CallFunction)callFunction);

        /// <summary>
        /// The v8_modulation_to_str
        /// </summary>
        /// <param name="modulationScheme">The modulationScheme<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string v8_modulation_to_str(int modulationScheme) =>
            V8.ModulationToString((V8Modulation)(uint)modulationScheme);

        /// <summary>
        /// The v8_protocol_to_str
        /// </summary>
        /// <param name="protocol">The protocol<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string v8_protocol_to_str(int protocol) =>
            V8.ProtocolToString((V8Protocol)protocol);

        /// <summary>
        /// The v8_pstn_access_to_str
        /// </summary>
        /// <param name="pstnAccess">The pstnAccess<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string v8_pstn_access_to_str(int pstnAccess) =>
            V8.PstnAccessToString((V8PstnAccess)pstnAccess);

        /// <summary>
        /// The v8_nsf_to_str
        /// </summary>
        /// <param name="nsf">The nsf<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string v8_nsf_to_str(int nsf) => V8.NsfToString(nsf);

        /// <summary>
        /// The v8_pcm_modem_availability_to_str
        /// </summary>
        /// <param name="availability">The availability<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string v8_pcm_modem_availability_to_str(int availability) =>
            V8.PcmModemAvailabilityToString((V8PcmModemAvailability)availability);

        /// <summary>
        /// The v8_t66_to_str
        /// </summary>
        /// <param name="t66">The t66<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string v8_t66_to_str(int t66) => V8.T66ToString(t66);
    }
}
