/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * ModemEcho.cs - Managed C# port of modem_echo.c and modem_echo.h
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2001, 2003, 2004 Steve Underwood
 *
 * This file is distributed under the terms of the GNU Lesser General Public
 * License version 2.1, matching the original source files.
 */

#nullable enable

namespace TKFaxEngine.Modem {
    using global::TKFaxEngine.Audio;
    using System;

    /// <summary>
    /// Working state for one adaptive modem line echo-canceller segment
    /// </summary>
    public sealed class ModemEchoCanSegmentState : IDisposable {
        /// <summary>
        /// Defines the _firState
        /// </summary>
        private Fir16State? _firState;

        /// <summary>
        /// Defines the _firTaps16
        /// </summary>
        private short[]? _firTaps16;

        /// <summary>
        /// Defines the _firTaps32
        /// </summary>
        private int[]? _firTaps32;

        /// <summary>
        /// Defines the _disposed
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModemEchoCanSegmentState"/> class.
        /// </summary>
        /// <param name="length">The length<see cref="int"/></param>
        internal ModemEchoCanSegmentState(int length) {
            if (length <= 0) {
                throw new ArgumentOutOfRangeException(
                    nameof(length),
                    length,
                    "The echo-canceller length must be greater than zero.");
            }

            Taps = length;
            CurrentPosition = length - 1;
            _firTaps32 = new int[length];
            _firTaps16 = new short[length];
            _firState = new Fir16State();
            Fir.fir16_create(_firState, _firTaps16, length);
        }

        /// <summary>
        /// Gets or sets a value indicating whether AdaptationEnabled
        /// Gets whether coefficient adaptation is enabled
        /// </summary>
        public bool AdaptationEnabled { get; internal set; }

        /// <summary>
        /// Gets the number of FIR taps
        /// </summary>
        public int Taps { get; }

        /// <summary>
        /// Gets or sets the optional echo-canceller length metadata from the
        /// original private C structure
        /// </summary>
        public int EchoLength { get; set; }

        /// <summary>
        /// Gets or sets the optional adaptation-rate metadata from the
        /// original private C structure. The source algorithm currently uses
        /// a fixed shift value and does not read this field
        /// </summary>
        public int AdaptationRate { get; set; }

        /// <summary>
        /// Gets or sets the TransmitPower
        /// Gets the tracked short-term transmit power
        /// </summary>
        public int TransmitPower { get; internal set; }

        /// <summary>
        /// Gets or sets the receive-power metadata from the original private
        /// C structure. The source algorithm currently does not update it
        /// </summary>
        public int ReceivePower { get; set; }

        /// <summary>
        /// Gets or sets the CurrentPosition
        /// Gets the current adaptation ring-buffer position
        /// </summary>
        public int CurrentPosition { get; internal set; }

        /// <summary>
        /// Reinitialises the echo canceller without reallocating it
        /// </summary>
        public void Flush() {
            ModemEcho.Flush(this);
        }

        /// <summary>
        /// Enables or disables adaptive coefficient updates
        /// </summary>
        /// <param name="enabled">The enabled<see cref="bool"/></param>
        public void SetAdaptationMode(bool enabled) {
            ModemEcho.SetAdaptationMode(this, enabled);
        }

        /// <summary>
        /// Processes one transmitted and one received PCM sample
        /// </summary>
        /// <param name="transmittedSample">The transmittedSample<see cref="short"/></param>
        /// <param name="receivedSample">The receivedSample<see cref="short"/></param>
        /// <returns>The <see cref="short"/></returns>
        public short Update(short transmittedSample, short receivedSample) {
            return ModemEcho.Update(this, transmittedSample, receivedSample);
        }

        /// <summary>
        /// Releases the managed buffers held by this state object
        /// </summary>
        public void Dispose() {
            if (_disposed) {
                return;
            }

            if (_firTaps16 != null) {
                Array.Clear(_firTaps16, 0, _firTaps16.Length);
            }

            if (_firTaps32 != null) {
                Array.Clear(_firTaps32, 0, _firTaps32.Length);
            }

            _firState = null;
            _firTaps16 = null;
            _firTaps32 = null;
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Gets the FirState
        /// </summary>
        internal Fir16State FirState {
            get {
                ThrowIfDisposed();
                return _firState!;
            }
        }

        /// <summary>
        /// Gets the FirTaps16
        /// </summary>
        internal short[] FirTaps16 {
            get {
                ThrowIfDisposed();
                return _firTaps16!;
            }
        }

        /// <summary>
        /// Gets the FirTaps32
        /// </summary>
        internal int[] FirTaps32 {
            get {
                ThrowIfDisposed();
                return _firTaps32!;
            }
        }

        /// <summary>
        /// The ThrowIfDisposed
        /// </summary>
        internal void ThrowIfDisposed() {
            if (_disposed) {
                throw new ObjectDisposedException(nameof(ModemEchoCanSegmentState));
            }
        }
    }

    /// <summary>
    /// Combined near/far echo-canceller state declared by the private C header.
    /// The uploaded source files do not contain processing functions for this
    /// aggregate state, so it is represented as a managed data container
    /// </summary>
    public sealed class ModemEchoCanState : IDisposable {
        /// <summary>
        /// Gets or sets the LocalDelay
        /// </summary>
        public short[] LocalDelay { get; set; } = Array.Empty<short>();

        /// <summary>
        /// Gets or sets the NearSegment
        /// </summary>
        public ModemEchoCanSegmentState? NearSegment { get; set; }

        /// <summary>
        /// Gets or sets the BulkDelay
        /// </summary>
        public short[] BulkDelay { get; set; } = Array.Empty<short>();

        /// <summary>
        /// Gets or sets the FarSegment
        /// </summary>
        public ModemEchoCanSegmentState? FarSegment { get; set; }

        /// <summary>
        /// Gets or sets the FarDelay
        /// </summary>
        public short[] FarDelay { get; set; } = Array.Empty<short>();

        /// <summary>
        /// Gets or sets the LoggingState
        /// Optional project-specific logging state. It is typed as object so
        /// this merged file has no dependency on the native logging_state_t
        /// </summary>
        public object? LoggingState { get; set; }

        /// <summary>
        /// Gets the LocalDelayLength
        /// </summary>
        public int LocalDelayLength {
            get { return LocalDelay.Length; }
        }

        /// <summary>
        /// Gets the BulkDelayLength
        /// </summary>
        public int BulkDelayLength {
            get { return BulkDelay.Length; }
        }

        /// <summary>
        /// Gets the FarDelayLength
        /// </summary>
        public int FarDelayLength {
            get { return FarDelay.Length; }
        }

        /// <summary>
        /// The Dispose
        /// </summary>
        public void Dispose() {
            NearSegment?.Dispose();
            FarSegment?.Dispose();

            Array.Clear(LocalDelay, 0, LocalDelay.Length);
            Array.Clear(BulkDelay, 0, BulkDelay.Length);
            Array.Clear(FarDelay, 0, FarDelay.Length);

            LocalDelay = Array.Empty<short>();
            BulkDelay = Array.Empty<short>();
            FarDelay = Array.Empty<short>();
            NearSegment = null;
            FarSegment = null;
            LoggingState = null;
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Adaptive FIR line echo canceller for modem signals
    /// </summary>
    public static class ModemEcho {
        /// <summary>
        /// Creates a modem echo-canceller segment.
        /// Equivalent to modem_echo_can_segment_init
        /// </summary>
        /// <param name="length">The length<see cref="int"/></param>
        /// <returns>The <see cref="ModemEchoCanSegmentState"/></returns>
        public static ModemEchoCanSegmentState SegmentInit(int length) {
            return new ModemEchoCanSegmentState(length);
        }

        /// <summary>
        /// Releases a modem echo-canceller segment.
        /// Equivalent to modem_echo_can_segment_free
        /// </summary>
        /// <param name="state">The state<see cref="ModemEchoCanSegmentState?"/></param>
        public static void SegmentFree(ModemEchoCanSegmentState? state) {
            state?.Dispose();
        }

        /// <summary>
        /// Reinitialises a modem echo-canceller segment.
        /// Equivalent to modem_echo_can_flush
        /// </summary>
        /// <param name="state">The state<see cref="ModemEchoCanSegmentState"/></param>
        public static void Flush(ModemEchoCanSegmentState state) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();

            state.TransmitPower = 0;
            Fir.fir16_flush(state.FirState);
            state.FirState.CurrentPosition = state.Taps - 1;
            Array.Clear(state.FirTaps32, 0, state.FirTaps32.Length);
            Array.Clear(state.FirTaps16, 0, state.FirTaps16.Length);
            state.CurrentPosition = state.Taps - 1;
        }

        /// <summary>
        /// Sets the adaptation mode.
        /// Equivalent to modem_echo_can_adaption_mode
        /// </summary>
        /// <param name="state">The state<see cref="ModemEchoCanSegmentState"/></param>
        /// <param name="enabled">The enabled<see cref="bool"/></param>
        public static void SetAdaptationMode(
            ModemEchoCanSegmentState state,
            bool enabled) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();
            state.AdaptationEnabled = enabled;
        }

        /// <summary>
        /// C-compatible overload where zero disables adaptation and every
        /// non-zero value enables it
        /// </summary>
        /// <param name="state">The state<see cref="ModemEchoCanSegmentState"/></param>
        /// <param name="adapt">The adapt<see cref="int"/></param>
        public static void SetAdaptionMode(
            ModemEchoCanSegmentState state,
            int adapt) {
            SetAdaptationMode(state, adapt != 0);
        }

        /// <summary>
        /// Processes one PCM sample through the echo canceller.
        /// Equivalent to modem_echo_can_update
        /// </summary>
        /// <param name="state">The state<see cref="ModemEchoCanSegmentState"/></param>
        /// <param name="transmittedSample">The transmittedSample<see cref="short"/></param>
        /// <param name="receivedSample">The receivedSample<see cref="short"/></param>
        /// <returns>The <see cref="short"/></returns>
        public static short Update(
            ModemEchoCanSegmentState state,
            short transmittedSample,
            short receivedSample) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();

            unchecked {
                // Evaluate the estimated echo by applying the FIR filter.
                int echoValue = Fir.fir16(state.FirState, transmittedSample);
                int cleanReceived = receivedSample - echoValue;

                if (state.AdaptationEnabled) {
                    // Short-term transmit power using the same single-pole IIR
                    // and integer-overflow behaviour as the C implementation.
                    int transmittedPower = transmittedSample * transmittedSample;
                    state.TransmitPower +=
                        (transmittedPower - state.TransmitPower) >> 5;

                    const int shift = 1;
                    int offset2 = state.CurrentPosition;
                    int offset1 = state.Taps - offset2;
                    int index;

                    for (index = state.Taps - 1; index >= offset1; index--) {
                        AdaptTap(
                            state,
                            index,
                            state.FirState.History[index - offset1],
                            cleanReceived,
                            shift);
                    }

                    for (; index >= 0; index--) {
                        AdaptTap(
                            state,
                            index,
                            state.FirState.History[index + offset2],
                            cleanReceived,
                            shift);
                    }
                }

                // Roll the adaptation ring-buffer position exactly as in C.
                if (state.CurrentPosition <= 0) {
                    state.CurrentPosition = state.Taps;
                }

                state.CurrentPosition--;
                return (short)cleanReceived;
            }
        }

        /// <summary>
        /// The AdaptTap
        /// </summary>
        /// <param name="state">The state<see cref="ModemEchoCanSegmentState"/></param>
        /// <param name="tapIndex">The tapIndex<see cref="int"/></param>
        /// <param name="historySample">The historySample<see cref="short"/></param>
        /// <param name="cleanReceived">The cleanReceived<see cref="int"/></param>
        /// <param name="shift">The shift<see cref="int"/></param>
        private static void AdaptTap(
            ModemEchoCanSegmentState state,
            int tapIndex,
            short historySample,
            int cleanReceived,
            int shift) {
            unchecked {
                int coefficient = state.FirTaps32[tapIndex];

                // Slow coefficient leak prevents long-term drift.
                coefficient -= coefficient >> 23;
                coefficient += (historySample * cleanReceived) >> shift;

                state.FirTaps32[tapIndex] = coefficient;
                state.FirTaps16[tapIndex] = (short)(coefficient >> 15);
            }
        }
    }

}
