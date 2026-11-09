/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * Bert.cs - Managed C# port of bert.c and bert.h
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2004 Steve Underwood
 *
 * This file is distributed under the terms of the GNU Lesser General Public
 * License version 2.1, matching the original source files.
 */

#nullable enable

namespace TKFaxEngine.Modem {
    using System;

    /// <summary>
    /// BERT status and error-rate report events.
    /// Numeric values match the native BERT_REPORT_* constants
    /// </summary>
    public enum BertReportEvent {
        /// <summary>
        /// Defines the Synced
        /// </summary>
        Synced = 0,

        /// <summary>
        /// Defines the Unsynced
        /// </summary>
        Unsynced = 1,

        /// <summary>
        /// Defines the Regular
        /// </summary>
        Regular = 2,

        /// <summary>
        /// Defines the ErrorRateGreaterThan1In10To2
        /// </summary>
        ErrorRateGreaterThan1In10To2 = 3,

        /// <summary>
        /// Defines the ErrorRateLessThan1In10To2
        /// </summary>
        ErrorRateLessThan1In10To2 = 4,

        /// <summary>
        /// Defines the ErrorRateLessThan1In10To3
        /// </summary>
        ErrorRateLessThan1In10To3 = 5,

        /// <summary>
        /// Defines the ErrorRateLessThan1In10To4
        /// </summary>
        ErrorRateLessThan1In10To4 = 6,

        /// <summary>
        /// Defines the ErrorRateLessThan1In10To5
        /// </summary>
        ErrorRateLessThan1In10To5 = 7,

        /// <summary>
        /// Defines the ErrorRateLessThan1In10To6
        /// </summary>
        ErrorRateLessThan1In10To6 = 8,

        /// <summary>
        /// Defines the ErrorRateLessThan1In10To7
        /// </summary>
        ErrorRateLessThan1In10To7 = 9
    }

    /// <summary>
    /// Supported BERT test patterns.
    /// Numeric values match the native BERT_PATTERN_* constants
    /// </summary>
    public enum BertPattern {
        /// <summary>
        /// Defines the Zeros
        /// </summary>
        Zeros = 0,

        /// <summary>
        /// Defines the Ones
        /// </summary>
        Ones = 1,

        /// <summary>
        /// Defines the SevenToOne
        /// </summary>
        SevenToOne = 2,

        /// <summary>
        /// Defines the ThreeToOne
        /// </summary>
        ThreeToOne = 3,

        /// <summary>
        /// Defines the OneToOne
        /// </summary>
        OneToOne = 4,

        /// <summary>
        /// Defines the OneToThree
        /// </summary>
        OneToThree = 5,

        /// <summary>
        /// Defines the OneToSeven
        /// </summary>
        OneToSeven = 6,

        /// <summary>
        /// Defines the QuickBrownFox
        /// </summary>
        QuickBrownFox = 7,

        /// <summary>
        /// Defines the ItuO15123
        /// </summary>
        ItuO15123 = 8,

        /// <summary>
        /// Defines the ItuO15120
        /// </summary>
        ItuO15120 = 9,

        /// <summary>
        /// Defines the ItuO15115
        /// </summary>
        ItuO15115 = 10,

        /// <summary>
        /// Defines the ItuO15211
        /// </summary>
        ItuO15211 = 11,

        /// <summary>
        /// Defines the ItuO1539
        /// </summary>
        ItuO1539 = 12
    }

    /// <summary>
    /// Results of a bit-error-rate test
    /// </summary>
    public sealed class BertResults {
        /// <summary>
        /// Gets or sets the TotalBits
        /// </summary>
        public int TotalBits { get; set; }

        /// <summary>
        /// Gets or sets the BadBits
        /// </summary>
        public int BadBits { get; set; }

        /// <summary>
        /// Gets or sets the Resyncs
        /// </summary>
        public int Resyncs { get; set; }

        // Native-name aliases for straightforward source migration.

        /// <summary>
        /// Gets or sets the total_bits
        /// </summary>
        public int total_bits { get => TotalBits; set => TotalBits = value; }

        /// <summary>
        /// Gets or sets the bad_bits
        /// </summary>
        public int bad_bits { get => BadBits; set => BadBits = value; }

        /// <summary>
        /// Gets or sets the resyncs
        /// </summary>
        public int resyncs { get => Resyncs; set => Resyncs = value; }

        /// <summary>
        /// Gets the ErrorRate
        /// </summary>
        public double ErrorRate => TotalBits > 0 ? (double)BadBits / TotalBits : 0.0;

        /// <summary>
        /// The Clone
        /// </summary>
        /// <returns>The <see cref="BertResults"/></returns>
        public BertResults Clone() {
            return new BertResults {
                TotalBits = TotalBits,
                BadBits = BadBits,
                Resyncs = Resyncs
            };
        }

        /// <summary>
        /// The Reset
        /// </summary>
        internal void Reset() {
            TotalBits = 0;
            BadBits = 0;
            Resyncs = 0;
        }

        /// <summary>
        /// The CopyFrom
        /// </summary>
        /// <param name="source">The source<see cref="BertResults"/></param>
        internal void CopyFrom(BertResults source) {
            TotalBits = source.TotalBits;
            BadBits = source.BadBits;
            Resyncs = source.Resyncs;
        }
    }

    /// <summary>
    /// Callback used for BERT status and result reports
    /// </summary>
    /// <param name="userData">The userData<see cref="object?"/></param>
    /// <param name="reason">The reason<see cref="int"/></param>
    /// <param name="results">The results<see cref="BertResults"/></param>
    public delegate void BertReportDelegate(
        object? userData,
        int reason,
        BertResults results);

    /// <summary>
    /// Defines the <see cref="BertTransmitterState" />
    /// </summary>
    internal sealed class BertTransmitterState {
        /// <summary>
        /// Defines the Register
        /// </summary>
        internal uint Register;

        /// <summary>
        /// Defines the Step
        /// </summary>
        internal int Step;

        /// <summary>
        /// Defines the StepBit
        /// </summary>
        internal int StepBit;

        /// <summary>
        /// Defines the Bits
        /// </summary>
        internal int Bits;

        /// <summary>
        /// Defines the Zeros
        /// </summary>
        internal int Zeros;

        /// <summary>
        /// The Reset
        /// </summary>
        internal void Reset() {
            Register = 0;
            Step = 0;
            StepBit = 0;
            Bits = 0;
            Zeros = 0;
        }
    }

    /// <summary>
    /// Defines the <see cref="BertReceiverState" />
    /// </summary>
    internal sealed class BertReceiverState {
        /// <summary>
        /// Defines the Register
        /// </summary>
        internal uint Register;

        /// <summary>
        /// Defines the ReferenceRegister
        /// </summary>
        internal uint ReferenceRegister;

        /// <summary>
        /// Defines the MasterRegister
        /// </summary>
        internal uint MasterRegister;

        /// <summary>
        /// Defines the Step
        /// </summary>
        internal int Step;

        /// <summary>
        /// Defines the StepBit
        /// </summary>
        internal int StepBit;

        /// <summary>
        /// Defines the Resync
        /// </summary>
        internal int Resync;

        /// <summary>
        /// Defines the Bits
        /// </summary>
        internal int Bits;

        /// <summary>
        /// Defines the Zeros
        /// </summary>
        internal int Zeros;

        /// <summary>
        /// Defines the ResyncLength
        /// </summary>
        internal int ResyncLength;

        /// <summary>
        /// Defines the ResyncPercent
        /// </summary>
        internal int ResyncPercent;

        /// <summary>
        /// Defines the ResyncBadBits
        /// </summary>
        internal int ResyncBadBits;

        /// <summary>
        /// Defines the ResyncCountdown
        /// </summary>
        internal int ResyncCountdown;

        /// <summary>
        /// Defines the ReportCountdown
        /// </summary>
        internal int ReportCountdown;

        /// <summary>
        /// Defines the MeasurementStep
        /// </summary>
        internal int MeasurementStep;

        /// <summary>
        /// The Reset
        /// </summary>
        internal void Reset() {
            Register = 0;
            ReferenceRegister = 0;
            MasterRegister = 0;
            Step = 0;
            StepBit = 0;
            Resync = 0;
            Bits = 0;
            Zeros = 0;
            ResyncLength = 0;
            ResyncPercent = 0;
            ResyncBadBits = 0;
            ResyncCountdown = 0;
            ReportCountdown = 0;
            MeasurementStep = 0;
        }
    }

    /// <summary>
    /// Working state for one BERT generator and analyser
    /// </summary>
    public sealed class BertState : IDisposable {
        /// <summary>
        /// Defines the _disposed
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Defines the PatternValue
        /// </summary>
        internal BertPattern PatternValue;

        /// <summary>
        /// Defines the PatternClass
        /// </summary>
        internal int PatternClass;

        /// <summary>
        /// Defines the Reporter
        /// </summary>
        internal BertReportDelegate? Reporter;

        /// <summary>
        /// Defines the ReporterUserData
        /// </summary>
        internal object? ReporterUserData;

        /// <summary>
        /// Defines the ReportFrequency
        /// </summary>
        internal int ReportFrequency;

        /// <summary>
        /// Defines the LimitValue
        /// </summary>
        internal int LimitValue;

        /// <summary>
        /// Defines the Mask
        /// </summary>
        internal uint Mask;

        /// <summary>
        /// Defines the Shift
        /// </summary>
        internal int Shift;

        /// <summary>
        /// Defines the Shift2
        /// </summary>
        internal int Shift2;

        /// <summary>
        /// Defines the MaximumZeros
        /// </summary>
        internal int MaximumZeros;

        /// <summary>
        /// Defines the Invert
        /// </summary>
        internal int Invert;

        /// <summary>
        /// Defines the ResyncTime
        /// </summary>
        internal int ResyncTime;

        /// <summary>
        /// Defines the DecadePointers
        /// </summary>
        internal readonly int[] DecadePointers = new int[9];

        /// <summary>
        /// Defines the DecadeBad
        /// </summary>
        internal readonly int[,] DecadeBad = new int[9, 10];

        /// <summary>
        /// Defines the ErrorRateDecade
        /// </summary>
        internal int ErrorRateDecade;

        /// <summary>
        /// Defines the Tx
        /// </summary>
        internal readonly BertTransmitterState Tx = new BertTransmitterState();

        /// <summary>
        /// Defines the Rx
        /// </summary>
        internal readonly BertReceiverState Rx = new BertReceiverState();

        /// <summary>
        /// Defines the CurrentResults
        /// </summary>
        internal readonly BertResults CurrentResults = new BertResults();

        /// <summary>
        /// Defines the SignalStatusHandlerValue
        /// </summary>
        internal Action<int>? SignalStatusHandlerValue;

        /// <summary>
        /// Gets the Pattern
        /// </summary>
        public BertPattern Pattern {
            get {
                ThrowIfDisposed();
                return PatternValue;
            }
        }

        /// <summary>
        /// Gets the Limit
        /// </summary>
        public int Limit {
            get {
                ThrowIfDisposed();
                return LimitValue;
            }
        }

        /// <summary>
        /// Gets the TransmittedBits
        /// </summary>
        public int TransmittedBits {
            get {
                ThrowIfDisposed();
                return Tx.Bits;
            }
        }

        /// <summary>
        /// Gets the ReceivedBits
        /// </summary>
        public int ReceivedBits {
            get {
                ThrowIfDisposed();
                return Rx.Bits;
            }
        }

        /// <summary>
        /// Gets a value indicating whether IsSynchronized
        /// </summary>
        public bool IsSynchronized {
            get {
                ThrowIfDisposed();
                return Rx.Resync == 0;
            }
        }

        /// <summary>
        /// Gets the CurrentErrorRateDecade
        /// </summary>
        public int CurrentErrorRateDecade {
            get {
                ThrowIfDisposed();
                return ErrorRateDecade;
            }
        }

        /// <summary>
        /// Gets the LoggingProtocol
        /// </summary>
        public string LoggingProtocol => "BERT";

        /// <summary>
        /// Gets or sets the SignalStatusHandler
        /// Optional handler for negative modem status values passed to PutBit.
        /// When unset, status values are written to the console as in the native code
        /// </summary>
        public Action<int>? SignalStatusHandler {
            get {
                ThrowIfDisposed();
                return SignalStatusHandlerValue;
            }
            set {
                ThrowIfDisposed();
                SignalStatusHandlerValue = value;
            }
        }

        /// <summary>
        /// The GetResults
        /// </summary>
        /// <returns>The <see cref="BertResults"/></returns>
        public BertResults GetResults() {
            ThrowIfDisposed();
            return CurrentResults.Clone();
        }

        /// <summary>
        /// The GetBit
        /// </summary>
        /// <returns>The <see cref="int"/></returns>
        public int GetBit() {
            return Bert.GetBit(this);
        }

        /// <summary>
        /// The PutBit
        /// </summary>
        /// <param name="bit">The bit<see cref="int"/></param>
        public void PutBit(int bit) {
            Bert.PutBit(this, bit);
        }

        /// <summary>
        /// The SetReport
        /// </summary>
        /// <param name="frequency">The frequency<see cref="int"/></param>
        /// <param name="reporter">The reporter<see cref="BertReportDelegate?"/></param>
        /// <param name="userData">The userData<see cref="object?"/></param>
        public void SetReport(
            int frequency,
            BertReportDelegate? reporter,
            object? userData) {
            Bert.SetReport(this, frequency, reporter, userData);
        }

        /// <summary>
        /// The Reset
        /// </summary>
        /// <param name="limit">The limit<see cref="int"/></param>
        /// <param name="pattern">The pattern<see cref="BertPattern"/></param>
        /// <param name="resyncLength">The resyncLength<see cref="int"/></param>
        /// <param name="resyncPercent">The resyncPercent<see cref="int"/></param>
        public void Reset(
            int limit,
            BertPattern pattern,
            int resyncLength,
            int resyncPercent) {
            Bert.Initialize(this, limit, pattern, resyncLength, resyncPercent);
        }

        /// <summary>
        /// The Dispose
        /// </summary>
        public void Dispose() {
            if (_disposed)
                return;

            Reporter = null;
            ReporterUserData = null;
            SignalStatusHandlerValue = null;
            PatternValue = default;
            PatternClass = 0;
            ReportFrequency = 0;
            LimitValue = 0;
            Mask = 0;
            Shift = 0;
            Shift2 = 0;
            MaximumZeros = 0;
            Invert = 0;
            ResyncTime = 0;
            ErrorRateDecade = 0;
            Array.Clear(DecadePointers, 0, DecadePointers.Length);
            Array.Clear(DecadeBad, 0, DecadeBad.Length);
            Tx.Reset();
            Rx.Reset();
            CurrentResults.Reset();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// The ResetForInitialization
        /// </summary>
        internal void ResetForInitialization() {
            _disposed = false;
            Reporter = null;
            ReporterUserData = null;
            SignalStatusHandlerValue = null;
            PatternValue = default;
            PatternClass = 0;
            ReportFrequency = 0;
            LimitValue = 0;
            Mask = 0;
            Shift = 0;
            Shift2 = 0;
            MaximumZeros = 0;
            Invert = 0;
            ResyncTime = 72;
            ErrorRateDecade = 8;
            Array.Clear(DecadePointers, 0, DecadePointers.Length);
            Array.Clear(DecadeBad, 0, DecadeBad.Length);
            Tx.Reset();
            Rx.Reset();
            CurrentResults.Reset();
        }

        /// <summary>
        /// The ThrowIfDisposed
        /// </summary>
        internal void ThrowIfDisposed() {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BertState));
        }
    }

    /// <summary>
    /// Managed bit-error-rate generator and analyser
    /// </summary>
    public static class Bert {
        /// <summary>
        /// Defines the MeasurementStep
        /// </summary>
        public const int MeasurementStep = 100;

        /// <summary>
        /// Defines the SignalStatusEndOfData
        /// </summary>
        public const int SignalStatusEndOfData = -7;

        /// <summary>
        /// Defines the NativeResultSize
        /// </summary>
        public const int NativeResultSize = 3 * sizeof(int);

        /// <summary>
        /// Defines the QuickBrownFoxPattern
        /// </summary>
        private const string QuickBrownFoxPattern =
            "VoyeZ Le BricK GeanT QuE J'ExaminE PreS Du WharF 123 456 7890 + - * : = $ % ( )" +
            "ThE QuicK BrowN FoX JumpS OveR ThE LazY DoG 123 456 7890 + - * : = $ % ( )";

        /// <summary>
        /// The EventToString
        /// </summary>
        /// <param name="eventCode">The eventCode<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string EventToString(int eventCode) {
            return eventCode switch {
                (int)BertReportEvent.Synced => "synced",
                (int)BertReportEvent.Unsynced => "unsync'ed",
                (int)BertReportEvent.Regular => "regular",
                (int)BertReportEvent.ErrorRateGreaterThan1In10To2 => "error rate > 1 in 10^2",
                (int)BertReportEvent.ErrorRateLessThan1In10To2 => "error rate < 1 in 10^2",
                (int)BertReportEvent.ErrorRateLessThan1In10To3 => "error rate < 1 in 10^3",
                (int)BertReportEvent.ErrorRateLessThan1In10To4 => "error rate < 1 in 10^4",
                (int)BertReportEvent.ErrorRateLessThan1In10To5 => "error rate < 1 in 10^5",
                (int)BertReportEvent.ErrorRateLessThan1In10To6 => "error rate < 1 in 10^6",
                (int)BertReportEvent.ErrorRateLessThan1In10To7 => "error rate < 1 in 10^7",
                _ => "???"
            };
        }

        /// <summary>
        /// The Initialize
        /// </summary>
        /// <param name="state">The state<see cref="BertState?"/></param>
        /// <param name="limit">The limit<see cref="int"/></param>
        /// <param name="pattern">The pattern<see cref="BertPattern"/></param>
        /// <param name="resyncLength">The resyncLength<see cref="int"/></param>
        /// <param name="resyncPercent">The resyncPercent<see cref="int"/></param>
        /// <returns>The <see cref="BertState"/></returns>
        public static BertState Initialize(
            BertState? state,
            int limit,
            BertPattern pattern,
            int resyncLength,
            int resyncPercent) {
            if (limit < 0)
                throw new ArgumentOutOfRangeException(nameof(limit));
            if (!Enum.IsDefined(typeof(BertPattern), pattern))
                throw new ArgumentOutOfRangeException(nameof(pattern));
            if (resyncLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(resyncLength));
            if (resyncPercent is < 0 or > 100)
                throw new ArgumentOutOfRangeException(nameof(resyncPercent));

            state ??= new BertState();
            state.ResetForInitialization();

            state.PatternValue = pattern;
            state.LimitValue = limit;

            ConfigurePattern(state, pattern);

            state.Tx.Bits = 0;
            state.Tx.Step = 0;
            state.Tx.StepBit = 0;
            state.Tx.Zeros = 0;

            state.Rx.Register = state.Tx.Register;
            state.Rx.ReferenceRegister = state.Rx.Register;
            state.Rx.MasterRegister = state.Rx.ReferenceRegister;
            state.Rx.Bits = 0;
            state.Rx.Step = 0;
            state.Rx.StepBit = 0;
            state.Rx.Resync = 1;
            state.Rx.ResyncCountdown = resyncLength;
            state.Rx.ResyncBadBits = 0;
            state.Rx.ResyncLength = resyncLength;
            state.Rx.ResyncPercent = resyncPercent;
            state.Rx.ReportCountdown = 0;
            state.Rx.MeasurementStep = MeasurementStep;

            state.ErrorRateDecade = 8;
            return state;
        }

        /// <summary>
        /// The GetBit
        /// </summary>
        /// <param name="state">The state<see cref="BertState"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int GetBit(BertState state) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();

            if (state.LimitValue != 0 && state.Tx.Bits >= state.LimitValue)
                return SignalStatusEndOfData;

            int bit = 0;
            switch (state.PatternClass) {
                case 0:
                    bit = (int)(state.Tx.Register & 1u);
                    state.Tx.Register = unchecked(
                        (state.Tx.Register >> 1) |
                        ((state.Tx.Register & 1u) << state.Shift2));
                    break;

                case 1:
                    bit = (int)(state.Tx.Register & 1u);
                    state.Tx.Register = unchecked(
                        (state.Tx.Register >> 1) |
                        (((state.Tx.Register ^ (state.Tx.Register >> state.Shift)) & 1u) << state.Shift2));

                    if (state.MaximumZeros != 0) {
                        if (bit != 0) {
                            if (++state.Tx.Zeros > state.MaximumZeros) {
                                state.Tx.Zeros = 0;
                                bit ^= 1;
                            }
                        } else {
                            state.Tx.Zeros = 0;
                        }
                    }

                    bit ^= state.Invert;
                    break;

                case 2:
                    if (state.Tx.StepBit == 0) {
                        state.Tx.StepBit = 7;
                        if (state.Tx.Step >= QuickBrownFoxPattern.Length) {
                            state.Tx.Register = 'V';
                            state.Tx.Step = 1;
                        } else {
                            state.Tx.Register = QuickBrownFoxPattern[state.Tx.Step++];
                        }
                    }

                    bit = (int)(state.Tx.Register & 1u);
                    state.Tx.Register >>= 1;
                    state.Tx.StepBit--;
                    break;
            }

            state.Tx.Bits++;
            return bit;
        }

        /// <summary>
        /// The PutBit
        /// </summary>
        /// <param name="state">The state<see cref="BertState"/></param>
        /// <param name="bit">The bit<see cref="int"/></param>
        public static void PutBit(BertState state, int bit) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();

            if (bit < 0) {
                if (state.SignalStatusHandlerValue is not null) {
                    state.SignalStatusHandlerValue(bit);
                } else {
                    Console.WriteLine($"Status is {SignalStatusToString(bit)} ({bit})");
                }

                return;
            }

            bit = (bit & 1) ^ state.Invert;
            state.Rx.Bits++;

            switch (state.PatternClass) {
                case 0:
                    PutFixedPatternBit(state, bit);
                    break;

                case 1:
                    PutPseudoRandomPatternBit(state, bit);
                    break;

                case 2:
                    PutQuickBrownFoxBit(state, bit);
                    break;
            }

            if (state.ReportFrequency > 0) {
                if (--state.Rx.ReportCountdown <= 0) {
                    Report(state, BertReportEvent.Regular);
                    state.Rx.ReportCountdown = state.ReportFrequency;
                }
            }
        }

        /// <summary>
        /// The Result
        /// </summary>
        /// <param name="state">The state<see cref="BertState"/></param>
        /// <param name="results">The results<see cref="BertResults"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int Result(BertState state, BertResults results) {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(results);
            state.ThrowIfDisposed();

            results.CopyFrom(state.CurrentResults);
            return NativeResultSize;
        }

        /// <summary>
        /// The Result
        /// </summary>
        /// <param name="state">The state<see cref="BertState"/></param>
        /// <returns>The <see cref="BertResults"/></returns>
        public static BertResults Result(BertState state) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();
            return state.CurrentResults.Clone();
        }

        /// <summary>
        /// The SetReport
        /// </summary>
        /// <param name="state">The state<see cref="BertState"/></param>
        /// <param name="frequency">The frequency<see cref="int"/></param>
        /// <param name="reporter">The reporter<see cref="BertReportDelegate?"/></param>
        /// <param name="userData">The userData<see cref="object?"/></param>
        public static void SetReport(
            BertState state,
            int frequency,
            BertReportDelegate? reporter,
            object? userData) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();

            state.ReportFrequency = frequency;
            state.Reporter = reporter;
            state.ReporterUserData = userData;
            state.Rx.ReportCountdown = frequency;
        }

        /// <summary>
        /// The Release
        /// </summary>
        /// <param name="state">The state<see cref="BertState"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int Release(BertState state) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();
            return 0;
        }

        /// <summary>
        /// The Free
        /// </summary>
        /// <param name="state">The state<see cref="BertState?"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int Free(BertState? state) {
            state?.Dispose();
            return 0;
        }

        /// <summary>
        /// The ConfigurePattern
        /// </summary>
        /// <param name="state">The state<see cref="BertState"/></param>
        /// <param name="pattern">The pattern<see cref="BertPattern"/></param>
        private static void ConfigurePattern(BertState state, BertPattern pattern) {
            state.ResyncTime = 72;
            state.Invert = 0;

            switch (pattern) {
                case BertPattern.Zeros:
                    state.Tx.Register = 0u;
                    state.Shift2 = 31;
                    state.PatternClass = 0;
                    break;

                case BertPattern.Ones:
                    state.Tx.Register = uint.MaxValue;
                    state.Shift2 = 31;
                    state.PatternClass = 0;
                    break;

                case BertPattern.SevenToOne:
                    state.Tx.Register = 0xFEFEFEFEu;
                    state.Shift2 = 31;
                    state.PatternClass = 0;
                    break;

                case BertPattern.ThreeToOne:
                    state.Tx.Register = 0xEEEEEEEEu;
                    state.Shift2 = 31;
                    state.PatternClass = 0;
                    break;

                case BertPattern.OneToOne:
                    state.Tx.Register = 0xAAAAAAAAu;
                    state.Shift2 = 31;
                    state.PatternClass = 0;
                    break;

                case BertPattern.OneToThree:
                    state.Tx.Register = 0x11111111u;
                    state.Shift2 = 31;
                    state.PatternClass = 0;
                    break;

                case BertPattern.OneToSeven:
                    state.Tx.Register = 0x01010101u;
                    state.Shift2 = 31;
                    state.PatternClass = 0;
                    break;

                case BertPattern.QuickBrownFox:
                    state.Tx.Register = 0u;
                    state.PatternClass = 2;
                    break;

                case BertPattern.ItuO15123:
                    state.PatternClass = 1;
                    state.Tx.Register = 0x7FFFFFu;
                    state.Mask = 0x20u;
                    state.Shift = 5;
                    state.Shift2 = 22;
                    state.Invert = 1;
                    state.ResyncTime = 56;
                    state.MaximumZeros = 0;
                    break;

                case BertPattern.ItuO15120:
                    state.PatternClass = 1;
                    state.Tx.Register = 0xFFFFFu;
                    state.Mask = 0x8u;
                    state.Shift = 3;
                    state.Shift2 = 19;
                    state.Invert = 1;
                    state.ResyncTime = 50;
                    state.MaximumZeros = 14;
                    break;

                case BertPattern.ItuO15115:
                    state.PatternClass = 1;
                    state.Tx.Register = 0x7FFFu;
                    state.Mask = 0x2u;
                    state.Shift = 1;
                    state.Shift2 = 14;
                    state.Invert = 1;
                    state.ResyncTime = 40;
                    state.MaximumZeros = 0;
                    break;

                case BertPattern.ItuO15211:
                    state.PatternClass = 1;
                    state.Tx.Register = 0x7FFu;
                    state.Mask = 0x4u;
                    state.Shift = 2;
                    state.Shift2 = 10;
                    state.Invert = 0;
                    state.ResyncTime = 32;
                    state.MaximumZeros = 0;
                    break;

                case BertPattern.ItuO1539:
                    state.PatternClass = 1;
                    state.Tx.Register = 0x1FFu;
                    state.Mask = 0x10u;
                    state.Shift = 4;
                    state.Shift2 = 8;
                    state.Invert = 0;
                    state.ResyncTime = 28;
                    state.MaximumZeros = 0;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(pattern));
            }
        }

        /// <summary>
        /// The PutFixedPatternBit
        /// </summary>
        /// <param name="state">The state<see cref="BertState"/></param>
        /// <param name="bit">The bit<see cref="int"/></param>
        private static void PutFixedPatternBit(BertState state, int bit) {
            if (state.Rx.Resync != 0) {
                state.Rx.Register = unchecked(
                    (state.Rx.Register >> 1) |
                    ((uint)bit << state.Shift2));
                state.Rx.ReferenceRegister = unchecked(
                    (state.Rx.ReferenceRegister >> 1) |
                    ((state.Rx.ReferenceRegister & 1u) << state.Shift2));

                if (state.Rx.Register == state.Rx.ReferenceRegister) {
                    if (++state.Rx.Resync > state.ResyncTime) {
                        state.Rx.Resync = 0;
                        Report(state, BertReportEvent.Synced);
                    }
                } else {
                    state.Rx.Resync = 2;
                    state.Rx.ReferenceRegister = state.Rx.MasterRegister;
                }
            } else {
                state.CurrentResults.TotalBits++;
                if ((((uint)bit ^ state.Rx.ReferenceRegister) & 1u) != 0)
                    state.CurrentResults.BadBits++;

                state.Rx.ReferenceRegister = unchecked(
                    (state.Rx.ReferenceRegister >> 1) |
                    ((state.Rx.ReferenceRegister & 1u) << state.Shift2));
            }
        }

        /// <summary>
        /// The PutPseudoRandomPatternBit
        /// </summary>
        /// <param name="state">The state<see cref="BertState"/></param>
        /// <param name="bit">The bit<see cref="int"/></param>
        private static void PutPseudoRandomPatternBit(BertState state, int bit) {
            if (state.Rx.Resync != 0) {
                int predictedBit = (int)((state.Rx.Register >> state.Shift) & 1u);
                if (bit == predictedBit) {
                    if (++state.Rx.Resync > state.ResyncTime) {
                        state.Rx.Resync = 0;
                        Report(state, BertReportEvent.Synced);
                    }
                } else {
                    state.Rx.Resync = 2;
                    state.Rx.Register ^= state.Mask;
                }
            } else {
                state.CurrentResults.TotalBits++;

                if (state.MaximumZeros != 0) {
                    if ((state.Rx.Register & state.Mask) != 0) {
                        if (++state.Rx.Zeros > state.MaximumZeros) {
                            state.Rx.Zeros = 0;
                            bit ^= 1;
                        }
                    } else {
                        state.Rx.Zeros = 0;
                    }
                }

                int predictedBit = (int)((state.Rx.Register >> state.Shift) & 1u);
                if (bit != predictedBit) {
                    state.CurrentResults.BadBits++;
                    state.Rx.ResyncBadBits++;
                    state.DecadeBad[2, state.DecadePointers[2]]++;
                }

                if (--state.Rx.MeasurementStep <= 0) {
                    state.Rx.MeasurementStep = MeasurementStep;
                    AssessErrorRate(state);
                }

                if (--state.Rx.ResyncCountdown <= 0) {
                    int resyncThreshold =
                        (state.Rx.ResyncLength * state.Rx.ResyncPercent) / 100;

                    if (state.Rx.ResyncBadBits >= resyncThreshold) {
                        state.Rx.Resync = 1;
                        state.CurrentResults.Resyncs++;
                        Report(state, BertReportEvent.Unsynced);
                    }

                    state.Rx.ResyncCountdown = state.Rx.ResyncLength;
                    state.Rx.ResyncBadBits = 0;
                }
            }

            state.Rx.Register = unchecked(
                (state.Rx.Register >> 1) |
                (((state.Rx.Register ^ (state.Rx.Register >> state.Shift)) & 1u) << state.Shift2));
        }

        /// <summary>
        /// The PutQuickBrownFoxBit
        /// </summary>
        /// <param name="state">The state<see cref="BertState"/></param>
        /// <param name="bit">The bit<see cref="int"/></param>
        private static void PutQuickBrownFoxBit(BertState state, int bit) {
            state.Rx.Register = unchecked(
                (state.Rx.Register >> 1) |
                ((uint)bit << 6));

            // The native implementation has no QBF resynchronisation mechanism.
            if (++state.Rx.StepBit == 7) {
                state.Rx.StepBit = 0;

                if (state.Rx.Register != QuickBrownFoxPattern[state.Rx.Step])
                    state.CurrentResults.BadBits++;

                state.Rx.Step++;
                if (state.Rx.Step >= QuickBrownFoxPattern.Length)
                    state.Rx.Step = 0;
            }

            state.CurrentResults.TotalBits++;
        }

        /// <summary>
        /// The AssessErrorRate
        /// </summary>
        /// <param name="state">The state<see cref="BertState"/></param>
        private static void AssessErrorRate(BertState state) {
            bool test = true;
            int i;

            for (i = 2; i <= 7; i++) {
                if (++state.DecadePointers[i] < 10)
                    break;

                state.DecadePointers[i] = 0;

                int sum = 0;
                for (int j = 0; j < 10; j++)
                    sum += state.DecadeBad[i, j];

                if (test && sum > 10) {
                    test = false;
                    if (state.ErrorRateDecade != i) {
                        Report(
                            state,
                            (BertReportEvent)((int)BertReportEvent.ErrorRateGreaterThan1In10To2 + i - 2));
                    }

                    state.ErrorRateDecade = i;
                }

                state.DecadeBad[i, 0] = 0;
                if (i < 7) {
                    state.DecadeBad[
                        i + 1,
                        state.DecadePointers[i + 1]] = sum;
                }
            }

            if (i > 7) {
                if (state.DecadePointers[i] >= 10)
                    state.DecadePointers[i] = 0;

                if (test) {
                    if (state.ErrorRateDecade != i) {
                        Report(
                            state,
                            (BertReportEvent)((int)BertReportEvent.ErrorRateGreaterThan1In10To2 + i - 2));
                    }

                    state.ErrorRateDecade = i;
                }
            } else {
                state.DecadeBad[i, state.DecadePointers[i]] = 0;
            }
        }

        /// <summary>
        /// The Report
        /// </summary>
        /// <param name="state">The state<see cref="BertState"/></param>
        /// <param name="eventType">The eventType<see cref="BertReportEvent"/></param>
        private static void Report(BertState state, BertReportEvent eventType) {
            state.Reporter?.Invoke(
                state.ReporterUserData,
                (int)eventType,
                state.CurrentResults);
        }

        /// <summary>
        /// The SignalStatusToString
        /// </summary>
        /// <param name="status">The status<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        private static string SignalStatusToString(int status) {
            return status switch {
                -1 => "carrier down",
                -2 => "carrier up",
                -3 => "training in progress",
                -4 => "training succeeded",
                -5 => "training failed",
                -6 => "framing OK",
                -7 => "end of data",
                -8 => "abort",
                -9 => "break",
                -10 => "shutdown complete",
                -11 => "octet report",
                -12 => "poor signal quality",
                -13 => "modem retrain occurred",
                -14 => "link connected",
                -15 => "link disconnected",
                -16 => "link error",
                -17 => "link idle",
                _ => "unknown status"
            };
        }
    }

    /// <summary>
    /// Compatibility facade retaining the original native function and constant names
    /// </summary>
    public static class BertApi {
        /// <summary>
        /// Defines the BERT_REPORT_SYNCED
        /// </summary>
        public const int BERT_REPORT_SYNCED = (int)BertReportEvent.Synced;

        /// <summary>
        /// Defines the BERT_REPORT_UNSYNCED
        /// </summary>
        public const int BERT_REPORT_UNSYNCED = (int)BertReportEvent.Unsynced;

        /// <summary>
        /// Defines the BERT_REPORT_REGULAR
        /// </summary>
        public const int BERT_REPORT_REGULAR = (int)BertReportEvent.Regular;

        /// <summary>
        /// Defines the BERT_REPORT_GT_10_2
        /// </summary>
        public const int BERT_REPORT_GT_10_2 = (int)BertReportEvent.ErrorRateGreaterThan1In10To2;

        /// <summary>
        /// Defines the BERT_REPORT_LT_10_2
        /// </summary>
        public const int BERT_REPORT_LT_10_2 = (int)BertReportEvent.ErrorRateLessThan1In10To2;

        /// <summary>
        /// Defines the BERT_REPORT_LT_10_3
        /// </summary>
        public const int BERT_REPORT_LT_10_3 = (int)BertReportEvent.ErrorRateLessThan1In10To3;

        /// <summary>
        /// Defines the BERT_REPORT_LT_10_4
        /// </summary>
        public const int BERT_REPORT_LT_10_4 = (int)BertReportEvent.ErrorRateLessThan1In10To4;

        /// <summary>
        /// Defines the BERT_REPORT_LT_10_5
        /// </summary>
        public const int BERT_REPORT_LT_10_5 = (int)BertReportEvent.ErrorRateLessThan1In10To5;

        /// <summary>
        /// Defines the BERT_REPORT_LT_10_6
        /// </summary>
        public const int BERT_REPORT_LT_10_6 = (int)BertReportEvent.ErrorRateLessThan1In10To6;

        /// <summary>
        /// Defines the BERT_REPORT_LT_10_7
        /// </summary>
        public const int BERT_REPORT_LT_10_7 = (int)BertReportEvent.ErrorRateLessThan1In10To7;

        /// <summary>
        /// Defines the BERT_PATTERN_ZEROS
        /// </summary>
        public const int BERT_PATTERN_ZEROS = (int)BertPattern.Zeros;

        /// <summary>
        /// Defines the BERT_PATTERN_ONES
        /// </summary>
        public const int BERT_PATTERN_ONES = (int)BertPattern.Ones;

        /// <summary>
        /// Defines the BERT_PATTERN_7_TO_1
        /// </summary>
        public const int BERT_PATTERN_7_TO_1 = (int)BertPattern.SevenToOne;

        /// <summary>
        /// Defines the BERT_PATTERN_3_TO_1
        /// </summary>
        public const int BERT_PATTERN_3_TO_1 = (int)BertPattern.ThreeToOne;

        /// <summary>
        /// Defines the BERT_PATTERN_1_TO_1
        /// </summary>
        public const int BERT_PATTERN_1_TO_1 = (int)BertPattern.OneToOne;

        /// <summary>
        /// Defines the BERT_PATTERN_1_TO_3
        /// </summary>
        public const int BERT_PATTERN_1_TO_3 = (int)BertPattern.OneToThree;

        /// <summary>
        /// Defines the BERT_PATTERN_1_TO_7
        /// </summary>
        public const int BERT_PATTERN_1_TO_7 = (int)BertPattern.OneToSeven;

        /// <summary>
        /// Defines the BERT_PATTERN_QBF
        /// </summary>
        public const int BERT_PATTERN_QBF = (int)BertPattern.QuickBrownFox;

        /// <summary>
        /// Defines the BERT_PATTERN_ITU_O151_23
        /// </summary>
        public const int BERT_PATTERN_ITU_O151_23 = (int)BertPattern.ItuO15123;

        /// <summary>
        /// Defines the BERT_PATTERN_ITU_O151_20
        /// </summary>
        public const int BERT_PATTERN_ITU_O151_20 = (int)BertPattern.ItuO15120;

        /// <summary>
        /// Defines the BERT_PATTERN_ITU_O151_15
        /// </summary>
        public const int BERT_PATTERN_ITU_O151_15 = (int)BertPattern.ItuO15115;

        /// <summary>
        /// Defines the BERT_PATTERN_ITU_O152_11
        /// </summary>
        public const int BERT_PATTERN_ITU_O152_11 = (int)BertPattern.ItuO15211;

        /// <summary>
        /// Defines the BERT_PATTERN_ITU_O153_9
        /// </summary>
        public const int BERT_PATTERN_ITU_O153_9 = (int)BertPattern.ItuO1539;

        /// <summary>
        /// The bert_event_to_str
        /// </summary>
        /// <param name="eventCode">The eventCode<see cref="int"/></param>
        /// <returns>The <see cref="string"/></returns>
        public static string bert_event_to_str(int eventCode) =>
            Bert.EventToString(eventCode);

        /// <summary>
        /// The bert_init
        /// </summary>
        /// <param name="state">The state<see cref="BertState?"/></param>
        /// <param name="limit">The limit<see cref="int"/></param>
        /// <param name="pattern">The pattern<see cref="int"/></param>
        /// <param name="resyncLen">The resyncLen<see cref="int"/></param>
        /// <param name="resyncPercent">The resyncPercent<see cref="int"/></param>
        /// <returns>The <see cref="BertState"/></returns>
        public static BertState bert_init(
            BertState? state,
            int limit,
            int pattern,
            int resyncLen,
            int resyncPercent) =>
            Bert.Initialize(
                state,
                limit,
                (BertPattern)pattern,
                resyncLen,
                resyncPercent);

        /// <summary>
        /// The bert_release
        /// </summary>
        /// <param name="state">The state<see cref="BertState"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int bert_release(BertState state) =>
            Bert.Release(state);

        /// <summary>
        /// The bert_free
        /// </summary>
        /// <param name="state">The state<see cref="BertState?"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int bert_free(BertState? state) =>
            Bert.Free(state);

        /// <summary>
        /// The bert_get_bit
        /// </summary>
        /// <param name="state">The state<see cref="BertState"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int bert_get_bit(BertState state) =>
            Bert.GetBit(state);

        /// <summary>
        /// The bert_put_bit
        /// </summary>
        /// <param name="state">The state<see cref="BertState"/></param>
        /// <param name="bit">The bit<see cref="int"/></param>
        public static void bert_put_bit(BertState state, int bit) =>
            Bert.PutBit(state, bit);

        /// <summary>
        /// The bert_set_report
        /// </summary>
        /// <param name="state">The state<see cref="BertState"/></param>
        /// <param name="frequency">The frequency<see cref="int"/></param>
        /// <param name="reporter">The reporter<see cref="BertReportDelegate?"/></param>
        /// <param name="userData">The userData<see cref="object?"/></param>
        public static void bert_set_report(
            BertState state,
            int frequency,
            BertReportDelegate? reporter,
            object? userData) =>
            Bert.SetReport(state, frequency, reporter, userData);

        /// <summary>
        /// The bert_result
        /// </summary>
        /// <param name="state">The state<see cref="BertState"/></param>
        /// <param name="results">The results<see cref="BertResults"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int bert_result(BertState state, BertResults results) =>
            Bert.Result(state, results);
    }
}
