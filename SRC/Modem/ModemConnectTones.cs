/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * ModemConnectTones.cs - Managed C# port of modem_connect_tones.c and
 *                        modem_connect_tones.h
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2003, 2006 Steve Underwood
 *
 * This file also contains the minimal DDS, power-meter and synchronous V.21
 * FSK receive support required by modem_connect_tones.c.
 *
 * This file is distributed under the terms of the GNU Lesser General Public
 * License version 2.1, matching the original source files.
 */

#nullable enable

namespace TKFaxEngine.Modem {
    using global::TKFaxEngine.Audio;
    using System;

    /// <summary>
    /// Modem and fax connection-tone identifiers
    /// </summary>
    public enum ModemConnectTone {
        /// <summary>Reported when a previously detected tone stops.</summary>

        /// <summary>
        /// Defines the None
        /// </summary>
        None = 0,
        /// <summary>FAX CNG: 1100 Hz in 0.5 second bursts with 3 seconds silence.</summary>

        /// <summary>
        /// Defines the FaxCng
        /// </summary>
        FaxCng = 1,
        /// <summary>ANS, or the equivalent FAX CED tone, at 2100 Hz.</summary>

        /// <summary>
        /// Defines the Ans
        /// </summary>
        Ans = 2,
        /// <summary>FAX CED is identical to ANS.</summary>

        /// <summary>
        /// Defines the FaxCed
        /// </summary>
        FaxCed = Ans,
        /// <summary>ANS with 180-degree phase reversals every 450 ms.</summary>

        /// <summary>
        /// Defines the AnsWithPhaseReversals
        /// </summary>
        AnsWithPhaseReversals = 3,
        /// <summary>ANS with 15 Hz amplitude modulation.</summary>

        /// <summary>
        /// Defines the Ansam
        /// </summary>
        Ansam = 4,
        /// <summary>ANSam with 180-degree phase reversals every 450 ms.</summary>

        /// <summary>
        /// Defines the AnsamWithPhaseReversals
        /// </summary>
        AnsamWithPhaseReversals = 5,
        /// <summary>FAX V.21 HDLC preamble.</summary>

        /// <summary>
        /// Defines the FaxPreamble
        /// </summary>
        FaxPreamble = 6,
        /// <summary>Receive-only detector for either FAX CED or V.21 preamble.</summary>

        /// <summary>
        /// Defines the FaxCedOrPreamble
        /// </summary>
        FaxCedOrPreamble = 7,
        /// <summary>Bell answer tone at 2225 Hz.</summary>

        /// <summary>
        /// Defines the BellAns
        /// </summary>
        BellAns = 8,
        /// <summary>Calling tone at 1300 Hz.</summary>

        /// <summary>
        /// Defines the CallingTone
        /// </summary>
        CallingTone = 9,

        /// <summary>
        /// Defines the RealTimeReports
        /// </summary>
        RealTimeReports = 0x1000
    }

    /// <summary>
    /// Callback used to report modem connection-tone state changes
    /// </summary>
    /// <param name="userData">Opaque user value supplied during initialisation</param>
    /// <param name="tone">Detected tone, or <see cref="ModemConnectTone.None"/> when it stops</param>
    /// <param name="level">Estimated signal level in dBm0, or -99 when a tone stops</param>
    /// <param name="duration">Reserved duration field. The source implementation reports zero</param>
    public delegate void ModemConnectToneReportHandler(
        object? userData,
        ModemConnectTone tone,
        int level,
        int duration);

    /// <summary>
    /// State for one modem connection-tone generator
    /// </summary>
    public sealed class ModemConnectTonesTxState : IDisposable {
        /// <summary>
        /// Defines the _disposed
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModemConnectTonesTxState"/> class.
        /// </summary>
        /// <param name="toneType">The toneType<see cref="ModemConnectTone"/></param>
        internal ModemConnectTonesTxState(ModemConnectTone toneType) {
            ToneType = toneType;
        }

        /// <summary>
        /// Gets or sets the ToneType
        /// </summary>
        public ModemConnectTone ToneType { get; internal set; }

        /// <summary>
        /// Gets or sets the TonePhaseRate
        /// </summary>
        public int TonePhaseRate { get; internal set; }

        /// <summary>Gets the current 32-bit DDS tone phase accumulator.</summary>

        /// <summary>
        /// Gets the TonePhase
        /// </summary>
        public uint TonePhase => TonePhaseAccumulator;

        /// <summary>
        /// Defines the TonePhaseAccumulator
        /// </summary>
        internal uint TonePhaseAccumulator;

        /// <summary>
        /// Gets or sets the Level
        /// </summary>
        public short Level { get; internal set; }

        /// <summary>
        /// Gets or sets the HopTimer
        /// </summary>
        public int HopTimer { get; internal set; }

        /// <summary>
        /// Gets or sets the DurationTimer
        /// </summary>
        public int DurationTimer { get; internal set; }

        /// <summary>Gets the current 32-bit DDS modulation phase accumulator.</summary>

        /// <summary>
        /// Gets the ModulationPhase
        /// </summary>
        public uint ModulationPhase => ModulationPhaseAccumulator;

        /// <summary>
        /// Defines the ModulationPhaseAccumulator
        /// </summary>
        internal uint ModulationPhaseAccumulator;

        /// <summary>
        /// Gets or sets the ModulationPhaseRate
        /// </summary>
        public int ModulationPhaseRate { get; internal set; }

        /// <summary>
        /// Gets or sets the ModulationLevel
        /// </summary>
        public short ModulationLevel { get; internal set; }

        /// <summary>Generates samples into the complete destination buffer.</summary>

        /// <summary>
        /// The Generate
        /// </summary>
        /// <param name="samples">The samples<see cref="Span{short}"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int Generate(Span<short> samples) {
            return ModemConnectTones.Transmit(this, samples);
        }

        /// <summary>Generates samples into a section of an array.</summary>

        /// <summary>
        /// The Generate
        /// </summary>
        /// <param name="samples">The samples<see cref="short[]"/></param>
        /// <param name="offset">The offset<see cref="int"/></param>
        /// <param name="length">The length<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int Generate(short[] samples, int offset, int length) {
            ArgumentNullException.ThrowIfNull(samples);
            return Generate(samples.AsSpan(offset, length));
        }

        /// <summary>
        /// The Dispose
        /// </summary>
        public void Dispose() {
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// The ThrowIfDisposed
        /// </summary>
        internal void ThrowIfDisposed() {
            if (_disposed) {
                throw new ObjectDisposedException(nameof(ModemConnectTonesTxState));
            }
        }
    }

    /// <summary>
    /// State for one modem connection-tone detector
    /// </summary>
    public sealed class ModemConnectTonesRxState : IDisposable {
        /// <summary>
        /// Defines the _disposed
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModemConnectTonesRxState"/> class.
        /// </summary>
        /// <param name="toneType">The toneType<see cref="ModemConnectTone"/></param>
        /// <param name="realTimeReports">The realTimeReports<see cref="bool"/></param>
        /// <param name="callback">The callback<see cref="ModemConnectToneReportHandler?"/></param>
        /// <param name="callbackData">The callbackData<see cref="object?"/></param>
        internal ModemConnectTonesRxState(
            ModemConnectTone toneType,
            bool realTimeReports,
            ModemConnectToneReportHandler? callback,
            object? callbackData) {
            ToneType = toneType;
            RealTimeReports = realTimeReports;
            ToneCallback = callback;
            CallbackData = callbackData;
        }

        /// <summary>Gets the effective receive tone type.</summary>

        /// <summary>
        /// Gets or sets the ToneType
        /// </summary>
        public ModemConnectTone ToneType { get; internal set; }

        /// <summary>Gets whether the real-time-report modifier was requested.</summary>

        /// <summary>
        /// Gets a value indicating whether RealTimeReports
        /// </summary>
        public bool RealTimeReports { get; }

        /// <summary>Gets the currently confirmed tone.</summary>

        /// <summary>
        /// Gets or sets the TonePresent
        /// </summary>
        public ModemConnectTone TonePresent { get; internal set; }

        /// <summary>Gets the current total-channel level estimator.</summary>

        /// <summary>
        /// Gets or sets the ChannelLevel
        /// </summary>
        public int ChannelLevel { get; internal set; }

        /// <summary>Gets the current notch-filter level estimator.</summary>

        /// <summary>
        /// Gets or sets the NotchLevel
        /// </summary>
        public int NotchLevel { get; internal set; }

        /// <summary>Gets the current 15 Hz AM level estimator.</summary>

        /// <summary>
        /// Gets or sets the AmLevel
        /// </summary>
        public int AmLevel { get; internal set; }

        /// <summary>Processes a block of 8 kHz signed linear PCM samples.</summary>

        /// <summary>
        /// The Process
        /// </summary>
        /// <param name="samples">The samples<see cref="ReadOnlySpan{short}"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int Process(ReadOnlySpan<short> samples) {
            return ModemConnectTones.Receive(this, samples);
        }

        /// <summary>Processes a section of an array.</summary>

        /// <summary>
        /// The Process
        /// </summary>
        /// <param name="samples">The samples<see cref="short[]"/></param>
        /// <param name="offset">The offset<see cref="int"/></param>
        /// <param name="length">The length<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int Process(short[] samples, int offset, int length) {
            ArgumentNullException.ThrowIfNull(samples);
            return Process(samples.AsSpan(offset, length));
        }

        /// <summary>Returns and clears the last latched tone.</summary>

        /// <summary>
        /// The GetDetectedTone
        /// </summary>
        /// <returns>The <see cref="ModemConnectTone"/></returns>
        public ModemConnectTone GetDetectedTone() {
            return ModemConnectTones.ReceiveGet(this);
        }

        /// <summary>Accounts for a missing block. The original function is a no-op.</summary>

        /// <summary>
        /// The FillIn
        /// </summary>
        /// <param name="sampleCount">The sampleCount<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int FillIn(int sampleCount) {
            return ModemConnectTones.ReceiveFillIn(this, sampleCount);
        }

        /// <summary>
        /// The Dispose
        /// </summary>
        public void Dispose() {
            if (_disposed) {
                return;
            }

            V21Receiver?.Dispose();
            V21Receiver = null;
            ToneCallback = null;
            CallbackData = null;
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Gets or sets the ToneCallback
        /// </summary>
        internal ModemConnectToneReportHandler? ToneCallback { get; set; }

        /// <summary>
        /// Gets or sets the CallbackData
        /// </summary>
        internal object? CallbackData { get; set; }

        /// <summary>
        /// Gets or sets the ZNotch1
        /// </summary>
        internal float ZNotch1 { get; set; }

        /// <summary>
        /// Gets or sets the ZNotch2
        /// </summary>
        internal float ZNotch2 { get; set; }

        /// <summary>
        /// Gets or sets the Z15Hz1
        /// </summary>
        internal float Z15Hz1 { get; set; }

        /// <summary>
        /// Gets or sets the Z15Hz2
        /// </summary>
        internal float Z15Hz2 { get; set; }

        /// <summary>
        /// Gets or sets the ChunkRemainder
        /// </summary>
        internal int ChunkRemainder { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether ToneOn
        /// </summary>
        internal bool ToneOn { get; set; }

        /// <summary>
        /// Gets or sets the ToneCycleDuration
        /// </summary>
        internal int ToneCycleDuration { get; set; }

        /// <summary>
        /// Gets or sets the GoodCycles
        /// </summary>
        internal int GoodCycles { get; set; }

        /// <summary>
        /// Gets or sets the Hit
        /// </summary>
        internal ModemConnectTone Hit { get; set; }

        /// <summary>
        /// Gets or sets the V21Receiver
        /// </summary>
        internal V21FskReceiver? V21Receiver { get; set; }

        /// <summary>
        /// Gets or sets the RawBitStream
        /// </summary>
        internal uint RawBitStream { get; set; }

        /// <summary>
        /// Gets or sets the NumBits
        /// </summary>
        internal int NumBits { get; set; }

        /// <summary>
        /// Gets or sets the FlagsSeen
        /// </summary>
        internal int FlagsSeen { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether FramingOkAnnounced
        /// </summary>
        internal bool FramingOkAnnounced { get; set; }

        /// <summary>
        /// The ThrowIfDisposed
        /// </summary>
        internal void ThrowIfDisposed() {
            if (_disposed) {
                throw new ObjectDisposedException(nameof(ModemConnectTonesRxState));
            }
        }
    }

    /// <summary>
    /// Generation and detection of modem and fax connection tones.
    /// Audio is signed 16-bit linear PCM at 8000 samples per second
    /// </summary>
    public static class ModemConnectTones {
        /// <summary>
        /// Defines the SampleRate
        /// </summary>
        public const int SampleRate = 8000;

        /// <summary>
        /// Defines the HdlcFramingOkThreshold
        /// </summary>
        private const int HdlcFramingOkThreshold = 5;

        /// <summary>
        /// Defines the Dbm0MaxPower
        /// </summary>
        private const float Dbm0MaxPower = 6.16f;

        /// <summary>Returns the descriptive string used by the C implementation.</summary>

        /// <summary>
        /// The ToneToString
        /// </summary>
        /// <param name="tone">The tone<see cref="ModemConnectTone"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string ToneToString(ModemConnectTone tone) {
            tone = BaseTone(tone);
            return tone switch {
                ModemConnectTone.None => "No tone",
                ModemConnectTone.FaxCng => "FAX CNG",
                ModemConnectTone.Ans => "ANS or FAX CED",
                ModemConnectTone.AnsWithPhaseReversals => "ANS/",
                ModemConnectTone.Ansam => "ANSam",
                ModemConnectTone.AnsamWithPhaseReversals => "ANSam/",
                ModemConnectTone.FaxPreamble => "FAX preamble",
                ModemConnectTone.FaxCedOrPreamble => "FAX CED or preamble",
                ModemConnectTone.BellAns => "Bell ANS",
                ModemConnectTone.CallingTone => "Calling tone",
                _ => "???"
            };
        }

        /// <summary>
        /// Creates and initialises a modem connection-tone generator.
        /// Equivalent to modem_connect_tones_tx_init
        /// </summary>
        /// <param name="toneType">The toneType<see cref="ModemConnectTone"/></param>
        /// <returns>The <see cref="ModemConnectTonesTxState"/></returns>
        public static ModemConnectTonesTxState TransmitInit(ModemConnectTone toneType) {
            toneType = BaseTone(toneType);
            var state = new ModemConnectTonesTxState(toneType);

            switch (toneType) {
                case ModemConnectTone.FaxCng:
                    state.TonePhaseRate = Dds.PhaseRate(1100.0f);
                    state.Level = Dds.ScalingDbm0(-11.0f);
                    state.DurationTimer = MillisecondsToSamples(500 + 3000);
                    break;

                case ModemConnectTone.Ans:
                case ModemConnectTone.Ansam:
                    state.TonePhaseRate = Dds.PhaseRate(2100.0f);
                    state.Level = Dds.ScalingDbm0(-11.0f);
                    if (toneType == ModemConnectTone.Ansam) {
                        state.ModulationPhaseRate = Dds.PhaseRate(15.0f);
                        state.ModulationLevel = unchecked((short)(state.Level / 5));
                        state.DurationTimer = MillisecondsToSamples(200 + 5000);
                    } else {
                        state.DurationTimer = MillisecondsToSamples(200 + 2600);
                    }
                    break;

                case ModemConnectTone.AnsWithPhaseReversals:
                case ModemConnectTone.AnsamWithPhaseReversals:
                    state.TonePhaseRate = Dds.PhaseRate(2100.0f);
                    state.Level = Dds.ScalingDbm0(-12.0f);
                    if (toneType == ModemConnectTone.AnsamWithPhaseReversals) {
                        state.ModulationPhaseRate = Dds.PhaseRate(15.0f);
                        state.ModulationLevel = unchecked((short)(state.Level / 5));
                        state.DurationTimer = MillisecondsToSamples(200 + 5000);
                    } else {
                        state.DurationTimer = MillisecondsToSamples(200 + 3300);
                    }
                    state.HopTimer = MillisecondsToSamples(450);
                    break;

                case ModemConnectTone.BellAns:
                    state.TonePhaseRate = Dds.PhaseRate(2225.0f);
                    state.Level = Dds.ScalingDbm0(-11.0f);
                    state.DurationTimer = MillisecondsToSamples(200 + 2600);
                    break;

                case ModemConnectTone.CallingTone:
                    state.TonePhaseRate = Dds.PhaseRate(1300.0f);
                    state.Level = Dds.ScalingDbm0(-11.0f);
                    state.DurationTimer = MillisecondsToSamples(600 + 2000);
                    break;

                default:
                    state.Dispose();
                    throw new ArgumentOutOfRangeException(
                        nameof(toneType),
                        toneType,
                        "This tone type cannot be generated.");
            }

            state.TonePhaseAccumulator = 0;
            state.ModulationPhaseAccumulator = 0;
            return state;
        }

        /// <summary>
        /// Generates a block of modem connection-tone samples.
        /// Equivalent to modem_connect_tones_tx
        /// </summary>
        /// <param name="state">The state<see cref="ModemConnectTonesTxState"/></param>
        /// <param name="samples">The samples<see cref="Span{short}"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int Transmit(ModemConnectTonesTxState state, Span<short> samples) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();

            int len = samples.Length;
            int i = 0;
            int xlen;

            switch (state.ToneType) {
                case ModemConnectTone.FaxCng:
                    for (; i < len; i++) {
                        if (state.DurationTimer > MillisecondsToSamples(3000)) {
                            xlen = i + state.DurationTimer - MillisecondsToSamples(3000);
                            if (xlen > len) {
                                xlen = len;
                            }

                            state.DurationTimer -= xlen - i;
                            for (; i < xlen; i++) {
                                samples[i] = Dds.dds_mod(
                                    ref state.TonePhaseAccumulator,
                                    state.TonePhaseRate,
                                    state.Level, 0);
                            }
                        }

                        if (state.DurationTimer > 0) {
                            xlen = i + state.DurationTimer;
                            if (xlen > len) {
                                xlen = len;
                            }

                            state.DurationTimer -= xlen - i;
                            samples.Slice(i, xlen - i).Clear();
                            i = xlen;
                        }

                        if (state.DurationTimer == 0) {
                            state.DurationTimer = MillisecondsToSamples(500 + 3000);
                        }
                    }
                    break;

                case ModemConnectTone.Ans:
                    if (state.DurationTimer < len) {
                        len = state.DurationTimer;
                    }

                    if (state.DurationTimer > MillisecondsToSamples(2600)) {
                        i = state.DurationTimer - MillisecondsToSamples(2600);
                        if (i > len) {
                            i = len;
                        }
                        samples.Slice(0, i).Clear();
                    }

                    for (; i < len; i++) {
                        samples[i] = Dds.dds_mod(
                            ref state.TonePhaseAccumulator,
                            state.TonePhaseRate,
                            state.Level, 0);
                    }
                    state.DurationTimer -= len;
                    break;

                case ModemConnectTone.AnsWithPhaseReversals:
                    if (state.DurationTimer < len) {
                        len = state.DurationTimer;
                    }

                    if (state.DurationTimer > MillisecondsToSamples(3300)) {
                        i = state.DurationTimer - MillisecondsToSamples(3300);
                        if (i > len) {
                            i = len;
                        }
                        samples.Slice(0, i).Clear();
                    }

                    for (; i < len; i++) {
                        if (--state.HopTimer <= 0) {
                            state.HopTimer = MillisecondsToSamples(450);
                            state.TonePhaseAccumulator = unchecked(state.TonePhaseAccumulator + 0x80000000u);
                        }

                        samples[i] = Dds.dds_mod(
                            ref state.TonePhaseAccumulator,
                            state.TonePhaseRate,
                            state.Level, 0);
                    }
                    state.DurationTimer -= len;
                    break;

                case ModemConnectTone.Ansam:
                    if (state.DurationTimer < len) {
                        len = state.DurationTimer;
                    }

                    if (state.DurationTimer > MillisecondsToSamples(5000)) {
                        i = state.DurationTimer - MillisecondsToSamples(5000);
                        if (i > len) {
                            i = len;
                        }
                        samples.Slice(0, i).Clear();
                    }

                    for (; i < len; i++) {
                        short modulation = unchecked((short)(
                            state.Level + Dds.dds_mod(
                                ref state.ModulationPhaseAccumulator,
                                state.ModulationPhaseRate,
                                state.ModulationLevel, 0)));

                        samples[i] = Dds.dds_mod(
                            ref state.TonePhaseAccumulator,
                            state.TonePhaseRate,
                            modulation, 0);
                    }
                    state.DurationTimer -= len;
                    break;

                case ModemConnectTone.AnsamWithPhaseReversals:
                    if (state.DurationTimer < len) {
                        len = state.DurationTimer;
                    }

                    if (state.DurationTimer > MillisecondsToSamples(5000)) {
                        i = state.DurationTimer - MillisecondsToSamples(5000);
                        if (i > len) {
                            i = len;
                        }
                        samples.Slice(0, i).Clear();
                    }

                    for (; i < len; i++) {
                        if (--state.HopTimer <= 0) {
                            state.HopTimer = MillisecondsToSamples(450);
                            state.TonePhaseAccumulator = unchecked(state.TonePhaseAccumulator + 0x80000000u);
                        }

                        short modulation = unchecked((short)(
                            state.Level + Dds.dds_mod(
                                ref state.ModulationPhaseAccumulator,
                                state.ModulationPhaseRate,
                                state.ModulationLevel, 0)));

                        samples[i] = Dds.dds_mod(
                            ref state.TonePhaseAccumulator,
                            state.TonePhaseRate,
                            modulation, 0);
                    }
                    state.DurationTimer -= len;
                    break;

                case ModemConnectTone.BellAns:
                    if (state.DurationTimer < len) {
                        len = state.DurationTimer;
                    }

                    if (state.DurationTimer > MillisecondsToSamples(2600)) {
                        i = state.DurationTimer - MillisecondsToSamples(2600);
                        if (i > len) {
                            i = len;
                        }
                        samples.Slice(0, i).Clear();
                    }

                    for (; i < len; i++) {
                        samples[i] = Dds.dds_mod(
                            ref state.TonePhaseAccumulator,
                            state.TonePhaseRate,
                            state.Level, 0);
                    }
                    state.DurationTimer -= len;
                    break;

                case ModemConnectTone.CallingTone:
                    for (; i < len; i++) {
                        if (state.DurationTimer > MillisecondsToSamples(2000)) {
                            xlen = i + state.DurationTimer - MillisecondsToSamples(2000);
                            if (xlen > len) {
                                xlen = len;
                            }

                            state.DurationTimer -= xlen - i;
                            for (; i < xlen; i++) {
                                samples[i] = Dds.dds_mod(
                                    ref state.TonePhaseAccumulator,
                                    state.TonePhaseRate,
                                    state.Level, 0);
                            }
                        }

                        if (state.DurationTimer > 0) {
                            xlen = i + state.DurationTimer;
                            if (xlen > len) {
                                xlen = len;
                            }

                            state.DurationTimer -= xlen - i;
                            samples.Slice(i, xlen - i).Clear();
                            i = xlen;
                        }

                        if (state.DurationTimer == 0) {
                            state.DurationTimer = MillisecondsToSamples(600 + 2000);
                        }
                    }
                    break;
            }

            return len;
        }

        /// <summary>
        /// The TransmitRelease
        /// </summary>
        /// <param name="state">The state<see cref="ModemConnectTonesTxState"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int TransmitRelease(ModemConnectTonesTxState state) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();
            return 0;
        }

        /// <summary>
        /// The TransmitFree
        /// </summary>
        /// <param name="state">The state<see cref="ModemConnectTonesTxState?"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int TransmitFree(ModemConnectTonesTxState? state) {
            state?.Dispose();
            return 0;
        }

        /// <summary>
        /// Creates and initialises a modem connection-tone detector.
        /// Equivalent to modem_connect_tones_rx_init
        /// </summary>
        /// <param name="toneType">The toneType<see cref="ModemConnectTone"/></param>
        /// <param name="toneCallback">The toneCallback<see cref="ModemConnectToneReportHandler?"/></param>
        /// <param name="userData">The userData<see cref="object?"/></param>
        /// <returns>The <see cref="ModemConnectTonesRxState"/></returns>
        public static ModemConnectTonesRxState ReceiveInit(
            ModemConnectTone toneType,
            ModemConnectToneReportHandler? toneCallback = null,
            object? userData = null) {
            bool realTimeReports = ((int)toneType & (int)ModemConnectTone.RealTimeReports) != 0;
            ModemConnectTone baseTone = BaseTone(toneType);

            var state = new ModemConnectTonesRxState(
                baseTone,
                realTimeReports,
                toneCallback,
                userData);

            switch (baseTone) {
                case ModemConnectTone.FaxPreamble:
                case ModemConnectTone.FaxCedOrPreamble:
                    state.V21Receiver = new V21FskReceiver(bit => V21PutBit(state, bit));
                    state.V21Receiver.SetSignalCutoff(-45.5f);
                    break;

                case ModemConnectTone.AnsWithPhaseReversals:
                case ModemConnectTone.Ansam:
                case ModemConnectTone.AnsamWithPhaseReversals:
                    // These all use the combined 2100 Hz/phase-reversal/AM detector.
                    state.ToneType = ModemConnectTone.Ans;
                    break;
            }

            state.ChannelLevel = 0;
            state.NotchLevel = 0;
            state.AmLevel = 0;
            state.TonePresent = ModemConnectTone.None;
            state.ToneCycleDuration = 0;
            state.GoodCycles = 0;
            state.Hit = ModemConnectTone.None;
            state.ToneOn = false;
            state.ZNotch1 = 0.0f;
            state.ZNotch2 = 0.0f;
            state.Z15Hz1 = 0.0f;
            state.Z15Hz2 = 0.0f;
            state.NumBits = 0;
            state.FlagsSeen = 0;
            state.FramingOkAnnounced = false;
            state.RawBitStream = 0;
            return state;
        }

        /// <summary>
        /// Processes received PCM samples through the selected detector.
        /// Equivalent to modem_connect_tones_rx
        /// </summary>
        /// <param name="state">The state<see cref="ModemConnectTonesRxState"/></param>
        /// <param name="samples">The samples<see cref="ReadOnlySpan{short}"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int Receive(ModemConnectTonesRxState state, ReadOnlySpan<short> samples) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();

            switch (state.ToneType) {
                case ModemConnectTone.FaxCng:
                    ReceiveFaxCng(state, samples);
                    break;

                case ModemConnectTone.FaxPreamble:
                    state.V21Receiver!.Process(samples);
                    break;

                case ModemConnectTone.FaxCedOrPreamble:
                    state.V21Receiver!.Process(samples);
                    ReceiveAns(state, samples);
                    break;

                case ModemConnectTone.Ans:
                    ReceiveAns(state, samples);
                    break;

                case ModemConnectTone.BellAns:
                    ReceiveBellAns(state, samples);
                    break;

                case ModemConnectTone.CallingTone:
                    ReceiveCallingTone(state, samples);
                    break;
            }

            return 0;
        }

        /// <summary>
        /// Fake processing for missing samples. The source implementation is a no-op
        /// </summary>
        /// <param name="state">The state<see cref="ModemConnectTonesRxState"/></param>
        /// <param name="sampleCount">The sampleCount<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int ReceiveFillIn(ModemConnectTonesRxState state, int sampleCount) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();
            if (sampleCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(sampleCount));
            }
            return 0;
        }

        /// <summary>Returns and clears the detector's latched tone.</summary>

        /// <summary>
        /// The ReceiveGet
        /// </summary>
        /// <param name="state">The state<see cref="ModemConnectTonesRxState"/></param>
        /// <returns>The <see cref="ModemConnectTone"/></returns>
        public static ModemConnectTone ReceiveGet(ModemConnectTonesRxState state) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();
            ModemConnectTone tone = state.Hit;
            state.Hit = ModemConnectTone.None;
            return tone;
        }

        /// <summary>
        /// The ReceiveRelease
        /// </summary>
        /// <param name="state">The state<see cref="ModemConnectTonesRxState"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int ReceiveRelease(ModemConnectTonesRxState state) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();
            return 0;
        }

        /// <summary>
        /// The ReceiveFree
        /// </summary>
        /// <param name="state">The state<see cref="ModemConnectTonesRxState?"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int ReceiveFree(ModemConnectTonesRxState? state) {
            state?.Dispose();
            return 0;
        }

        /// <summary>
        /// The ReceiveFaxCng
        /// </summary>
        /// <param name="state">The state<see cref="ModemConnectTonesRxState"/></param>
        /// <param name="samples">The samples<see cref="ReadOnlySpan{short}"/></param>
        private static void ReceiveFaxCng(
            ModemConnectTonesRxState state,
            ReadOnlySpan<short> samples) {
            for (int i = 0; i < samples.Length; i++) {
                float famp = samples[i];
                float v1 = (0.792928f * famp)
                    + (1.0018744927985f * state.ZNotch1)
                    - (0.54196833412465f * state.ZNotch2);

                famp = v1 - (1.2994747954630f * state.ZNotch1) + state.ZNotch2;
                state.ZNotch2 = state.ZNotch1;
                state.ZNotch1 = v1;
                short notched = unchecked((short)RoundToInt(famp));

                state.ChannelLevel += (Math.Abs((int)samples[i]) - state.ChannelLevel) >> 5;
                state.NotchLevel += (Math.Abs((int)notched) - state.NotchLevel) >> 5;

                if (state.ChannelLevel > 70 && state.NotchLevel * 6 < state.ChannelLevel) {
                    if (state.TonePresent != ModemConnectTone.FaxCng) {
                        if (++state.ToneCycleDuration >= MillisecondsToSamples(415)) {
                            ReportToneState(
                                state,
                                ModemConnectTone.FaxCng,
                                ChannelLevelToDbm0(state.ChannelLevel));
                        }
                    }
                } else {
                    if (state.TonePresent == ModemConnectTone.FaxCng) {
                        ReportToneState(state, ModemConnectTone.None, -99);
                    }
                    state.ToneCycleDuration = 0;
                }
            }
        }

        /// <summary>
        /// The ReceiveAns
        /// </summary>
        /// <param name="state">The state<see cref="ModemConnectTonesRxState"/></param>
        /// <param name="samples">The samples<see cref="ReadOnlySpan{short}"/></param>
        private static void ReceiveAns(
            ModemConnectTonesRxState state,
            ReadOnlySpan<short> samples) {
            for (int i = 0; i < samples.Length; i++) {
                float famp = samples[i];

                float v1 = MathF.Abs(famp)
                    + (1.996667f * state.Z15Hz1)
                    - (0.9968004f * state.Z15Hz2);

                float filtered = 0.001599787f * (v1 - state.Z15Hz2);
                state.Z15Hz2 = state.Z15Hz1;
                state.Z15Hz1 = v1;
                state.AmLevel += Math.Abs(RoundToInt(filtered)) - (state.AmLevel >> 8);

                v1 = (0.7552f * famp)
                    - (0.1183852f * state.ZNotch1)
                    - (0.5104039f * state.ZNotch2);

                famp = v1 + (0.1567596f * state.ZNotch1) + state.ZNotch2;
                state.ZNotch2 = state.ZNotch1;
                state.ZNotch1 = v1;
                short notched = unchecked((short)RoundToInt(famp));

                state.ChannelLevel += (Math.Abs((int)samples[i]) - state.ChannelLevel) >> 5;
                state.NotchLevel += (Math.Abs((int)notched) - state.NotchLevel) >> 4;

                if (state.ChannelLevel <= 70) {
                    if (state.TonePresent != ModemConnectTone.None) {
                        ReportToneState(state, ModemConnectTone.None, -99);
                    }

                    state.ToneCycleDuration = 0;
                    state.GoodCycles = 0;
                    state.ToneOn = false;
                    continue;
                }

                state.ToneCycleDuration++;
                if (state.NotchLevel * 6 < state.ChannelLevel) {
                    if (!state.ToneOn) {
                        if (state.ToneCycleDuration >= MillisecondsToSamples(450 - 25)) {
                            if (++state.GoodCycles == 3) {
                                ReportToneState(
                                    state,
                                    IsAmplitudeModulated(state)
                                        ? ModemConnectTone.AnsamWithPhaseReversals
                                        : ModemConnectTone.AnsWithPhaseReversals,
                                    ChannelLevelToDbm0(state.ChannelLevel));
                            }
                        } else {
                            state.GoodCycles = 0;
                        }

                        state.ToneCycleDuration = 0;
                    } else if (state.ToneCycleDuration >= MillisecondsToSamples(450 + 100)) {
                        if (state.TonePresent == ModemConnectTone.None) {
                            ReportToneState(
                                state,
                                IsAmplitudeModulated(state)
                                    ? ModemConnectTone.Ansam
                                    : ModemConnectTone.Ans,
                                ChannelLevelToDbm0(state.ChannelLevel));
                        }

                        state.GoodCycles = 0;
                        state.ToneCycleDuration = MillisecondsToSamples(450 + 100);
                    }

                    state.ToneOn = true;
                } else if (state.NotchLevel * 5 > state.ChannelLevel) {
                    if (state.TonePresent == ModemConnectTone.Ans) {
                        ReportToneState(state, ModemConnectTone.None, -99);
                        state.GoodCycles = 0;
                    } else if (state.ToneCycleDuration >= MillisecondsToSamples(450 + 25)) {
                        if (state.TonePresent == ModemConnectTone.AnsWithPhaseReversals
                            || state.TonePresent == ModemConnectTone.AnsamWithPhaseReversals) {
                            ReportToneState(state, ModemConnectTone.None, -99);
                        }
                        state.GoodCycles = 0;
                    }

                    state.ToneOn = false;
                }
            }
        }

        /// <summary>
        /// The ReceiveBellAns
        /// </summary>
        /// <param name="state">The state<see cref="ModemConnectTonesRxState"/></param>
        /// <param name="samples">The samples<see cref="ReadOnlySpan{short}"/></param>
        private static void ReceiveBellAns(
            ModemConnectTonesRxState state,
            ReadOnlySpan<short> samples) {
            for (int i = 0; i < samples.Length; i++) {
                float famp = samples[i];
                float v1 = (0.739651f * famp)
                    - (0.257384f * state.ZNotch1)
                    - (0.510404f * state.ZNotch2);

                famp = v1 + (0.351437f * state.ZNotch1) + state.ZNotch2;
                state.ZNotch2 = state.ZNotch1;
                state.ZNotch1 = v1;
                short notched = unchecked((short)RoundToInt(famp));

                state.ChannelLevel += (Math.Abs((int)samples[i]) - state.ChannelLevel) >> 5;
                state.NotchLevel += (Math.Abs((int)notched) - state.NotchLevel) >> 5;

                if (state.ChannelLevel > 70 && state.NotchLevel * 6 < state.ChannelLevel) {
                    if (state.TonePresent != ModemConnectTone.BellAns) {
                        if (++state.ToneCycleDuration >= MillisecondsToSamples(415)) {
                            ReportToneState(
                                state,
                                ModemConnectTone.BellAns,
                                ChannelLevelToDbm0(state.ChannelLevel));
                        }
                    }
                } else {
                    if (state.TonePresent == ModemConnectTone.BellAns) {
                        ReportToneState(state, ModemConnectTone.None, -99);
                    }
                    state.ToneCycleDuration = 0;
                }
            }
        }

        /// <summary>
        /// The ReceiveCallingTone
        /// </summary>
        /// <param name="state">The state<see cref="ModemConnectTonesRxState"/></param>
        /// <param name="samples">The samples<see cref="ReadOnlySpan{short}"/></param>
        private static void ReceiveCallingTone(
            ModemConnectTonesRxState state,
            ReadOnlySpan<short> samples) {
            for (int i = 0; i < samples.Length; i++) {
                float famp = samples[i];
                float v1 = (0.755582f * famp)
                    + (0.820887174515f * state.ZNotch1)
                    - (0.541968324778f * state.ZNotch2);

                famp = v1 - (1.0456667108f * state.ZNotch1) + state.ZNotch2;
                state.ZNotch2 = state.ZNotch1;
                state.ZNotch1 = v1;
                short notched = unchecked((short)RoundToInt(famp));

                state.ChannelLevel += (Math.Abs((int)samples[i]) - state.ChannelLevel) >> 5;
                state.NotchLevel += (Math.Abs((int)notched) - state.NotchLevel) >> 5;

                if (state.ChannelLevel > 70 && state.NotchLevel * 6 < state.ChannelLevel) {
                    if (state.TonePresent != ModemConnectTone.CallingTone) {
                        if (++state.ToneCycleDuration >= MillisecondsToSamples(415)) {
                            ReportToneState(
                                state,
                                ModemConnectTone.CallingTone,
                                ChannelLevelToDbm0(state.ChannelLevel));
                        }
                    }
                } else {
                    if (state.TonePresent == ModemConnectTone.CallingTone) {
                        ReportToneState(state, ModemConnectTone.None, -99);
                    }
                    state.ToneCycleDuration = 0;
                }
            }
        }

        /// <summary>
        /// The ReportToneState
        /// </summary>
        /// <param name="state">The state<see cref="ModemConnectTonesRxState"/></param>
        /// <param name="tone">The tone<see cref="ModemConnectTone"/></param>
        /// <param name="level">The level<see cref="int"/></param>
        private static void ReportToneState(
            ModemConnectTonesRxState state,
            ModemConnectTone tone,
            int level) {
            if (tone == state.TonePresent) {
                return;
            }

            if (state.ToneCallback != null) {
                state.ToneCallback(state.CallbackData, tone, level, 0);
            } else if (tone != ModemConnectTone.None) {
                state.Hit = tone;
            }

            state.TonePresent = tone;
        }

        /// <summary>
        /// The V21PutBit
        /// </summary>
        /// <param name="state">The state<see cref="ModemConnectTonesRxState"/></param>
        /// <param name="bit">The bit<see cref="int"/></param>
        private static void V21PutBit(ModemConnectTonesRxState state, int bit) {
            if (bit < 0) {
                switch (bit) {
                    case V21FskReceiver.SignalStatusCarrierDown:
                        if (state.TonePresent == ModemConnectTone.FaxPreamble) {
                            ReportToneState(state, ModemConnectTone.None, -99);
                        }
                        goto case V21FskReceiver.SignalStatusCarrierUp;

                    case V21FskReceiver.SignalStatusCarrierUp:
                        state.RawBitStream = 0;
                        state.NumBits = 0;
                        state.FlagsSeen = 0;
                        state.FramingOkAnnounced = false;
                        break;
                }
                return;
            }

            state.RawBitStream = unchecked(
                (state.RawBitStream << 1) | (uint)((bit << 8) & 0x100));
            state.NumBits++;

            if ((state.RawBitStream & 0x7F00u) == 0x7E00u) {
                if ((state.RawBitStream & 0x8000u) != 0) {
                    state.FlagsSeen = 0;
                } else if (state.FlagsSeen < HdlcFramingOkThreshold) {
                    if (state.NumBits != 8) {
                        state.FlagsSeen = 0;
                    }

                    if (++state.FlagsSeen >= HdlcFramingOkThreshold
                        && !state.FramingOkAnnounced) {
                        ReportToneState(
                            state,
                            ModemConnectTone.FaxPreamble,
                            RoundToInt(state.V21Receiver!.SignalPowerDbm0));
                        state.FramingOkAnnounced = true;
                    }
                }

                state.NumBits = 0;
            } else if (state.FlagsSeen >= HdlcFramingOkThreshold && state.NumBits == 8) {
                state.FramingOkAnnounced = false;
                state.FlagsSeen = 0;
            }
        }

        /// <summary>
        /// The IsAmplitudeModulated
        /// </summary>
        /// <param name="state">The state<see cref="ModemConnectTonesRxState"/></param>
        /// <returns>The <see cref="bool"/></returns>
        private static bool IsAmplitudeModulated(ModemConnectTonesRxState state) {
            return state.AmLevel * 15 / 256 > state.ChannelLevel;
        }

        /// <summary>
        /// The ChannelLevelToDbm0
        /// </summary>
        /// <param name="channelLevel">The channelLevel<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int ChannelLevelToDbm0(int channelLevel) {
            float level = channelLevel == 0
                ? -96.329f + Dbm0MaxPower
                : AmplitudeRatioToDb(channelLevel / 32768.0f);

            return RoundToInt(level + Dbm0MaxPower + 0.8f);
        }

        /// <summary>
        /// The AmplitudeRatioToDb
        /// </summary>
        /// <param name="ratio">The ratio<see cref="float"/></param>
        /// <returns>The <see cref="float"/></returns>
        private static float AmplitudeRatioToDb(float ratio) {
            return 20.0f * MathF.Log10(ratio);
        }

        /// <summary>
        /// The RoundToInt
        /// </summary>
        /// <param name="value">The value<see cref="float"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int RoundToInt(float value) {
            return unchecked((int)MathF.Round(value, MidpointRounding.ToEven));
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
        /// The BaseTone
        /// </summary>
        /// <param name="tone">The tone<see cref="ModemConnectTone"/></param>
        /// <returns>The <see cref="ModemConnectTone"/></returns>
        private static ModemConnectTone BaseTone(ModemConnectTone tone) {
            return (ModemConnectTone)((int)tone & 0x0FFF);
        }
    }

    /// <summary>
    /// Minimal synchronous V.21 channel-2 FSK receiver used only for FAX
    /// preamble detection. It is a managed port of the receive path used by
    /// fsk_rx in the original library
    /// </summary>
    internal sealed class V21FskReceiver : IDisposable {
        /// <summary>
        /// Defines the SignalStatusCarrierDown
        /// </summary>
        internal const int SignalStatusCarrierDown = -1;

        /// <summary>
        /// Defines the SignalStatusCarrierUp
        /// </summary>
        internal const int SignalStatusCarrierUp = -2;

        /// <summary>
        /// Defines the SampleRate
        /// </summary>
        private const int SampleRate = 8000;

        /// <summary>
        /// Defines the BaudRate
        /// </summary>
        private const int BaudRate = 300 * 100;

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
        /// Initializes a new instance of the <see cref="V21FskReceiver"/> class.
        /// </summary>
        /// <param name="putBit">The putBit<see cref="Action{int}"/></param>
        internal V21FskReceiver(Action<int> putBit) {
            _putBit = putBit ?? throw new ArgumentNullException(nameof(putBit));
            Restart();
        }

        /// <summary>
        /// Gets the SignalPowerDbm0
        /// </summary>
        internal float SignalPowerDbm0 {
            get {
                ThrowIfDisposed();
                return _power.CurrentDbm0;
            }
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
                for (int i = 0; i < samples.Length; i++) {
                    int sum0 = UpdateCorrelator(0, bufferPosition, samples[i]);
                    int sum1 = UpdateCorrelator(1, bufferPosition, samples[i]);

                    short x = (short)(samples[i] >> 1);
                    short powerSample = (short)(x - _lastSample);
                    int power = _power.Update(powerSample);
                    _lastSample = x;

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
                        if (_baudPhase < SampleRate * 50) {
                            _baudPhase += BaudRate >> 3;
                        } else {
                            _baudPhase -= BaudRate >> 3;
                        }
                    }

                    _baudPhase += BaudRate;
                    if (_baudPhase >= SampleRate * 100) {
                        _baudPhase -= SampleRate * 100;
                        _putBit(baudState);
                    }

                    if (++bufferPosition >= _correlationSpan) {
                        bufferPosition = 0;
                    }
                }
            }

            _bufferPosition = bufferPosition;
            return 0;
        }

        /// <summary>
        /// The Dispose
        /// </summary>
        public void Dispose() {
            if (_disposed) {
                return;
            }

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
        private void Restart() {
            _phaseRates[0] = Dds.PhaseRate(1850.0f);
            _phaseRates[1] = Dds.PhaseRate(1650.0f);
            _phaseAccumulators[0] = 0;
            _phaseAccumulators[1] = 0;
            _lastSample = 0;

            _correlationSpan = SampleRate * 100 / BaudRate;
            if (_correlationSpan > MaximumWindowLength) {
                _correlationSpan = MaximumWindowLength;
            }

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
        /// <param name="index">The index<see cref="int"/></param>
        /// <param name="bufferPosition">The bufferPosition<see cref="int"/></param>
        /// <param name="sample">The sample<see cref="short"/></param>
        /// <returns>The <see cref="int"/></returns>
        private int UpdateCorrelator(int index, int bufferPosition, short sample) {
            ComplexInt32 old = _window[index][bufferPosition];
            ComplexInt32 dot = _dot[index];
            dot.Re = unchecked(dot.Re - old.Re);
            dot.Im = unchecked(dot.Im - old.Im);

            DdsComplexInt16 phase = Dds.dds_complexi16(
                ref _phaseAccumulators[index],
                _phaseRates[index]);

            var current = new ComplexInt32 {
                Re = unchecked((phase.Real * sample) >> _scalingShift),
                Im = unchecked((phase.Imaginary * sample) >> _scalingShift)
            };

            _window[index][bufferPosition] = current;
            dot.Re = unchecked(dot.Re + current.Re);
            dot.Im = unchecked(dot.Im + current.Im);
            _dot[index] = dot;

            int component = dot.Re >> 15;
            int sum = unchecked(component * component);
            component = dot.Im >> 15;
            return unchecked(sum + (component * component));
        }

        /// <summary>
        /// The ThrowIfDisposed
        /// </summary>
        private void ThrowIfDisposed() {
            if (_disposed) {
                throw new ObjectDisposedException(nameof(V21FskReceiver));
            }
        }
    }
}
