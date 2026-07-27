/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * Fsk.cs - Managed C# port of fsk.c and fsk.h
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2003 Steve Underwood
 *
 * This file is distributed under the terms of the GNU Lesser General Public
 * License version 2.1, matching the original source files.
 */

#nullable enable

namespace TKFaxEngine.Modem {
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Predefined FSK modem channels. Values retain the ordering of the
    /// original FSK_* constants
    /// </summary>
    public enum FskPreset {
        /// <summary>
        /// Defines the V21Channel1
        /// </summary>
        V21Channel1 = 0,

        /// <summary>
        /// Defines the V21Channel2
        /// </summary>
        V21Channel2,

        /// <summary>
        /// Defines the V23Channel1
        /// </summary>
        V23Channel1,

        /// <summary>
        /// Defines the V23Channel2
        /// </summary>
        V23Channel2,

        /// <summary>
        /// Defines the Bell103Channel1
        /// </summary>
        Bell103Channel1,

        /// <summary>
        /// Defines the Bell103Channel2
        /// </summary>
        Bell103Channel2,

        /// <summary>
        /// Defines the Bell202
        /// </summary>
        Bell202,

        /// <summary>
        /// Defines the Weitbrecht4545
        /// </summary>
        Weitbrecht4545,

        /// <summary>
        /// Defines the Weitbrecht50
        /// </summary>
        Weitbrecht50,

        /// <summary>
        /// Defines the Weitbrecht476
        /// </summary>
        Weitbrecht476,

        /// <summary>
        /// Defines the V21Channel1At110Bps
        /// </summary>
        V21Channel1At110Bps
    }

    /// <summary>
    /// Symbol synchronization and optional start/stop framing mode
    /// </summary>
    public enum FskFrameMode {
        /// <summary>
        /// Defines the Asynchronous
        /// </summary>
        Asynchronous = 0,

        /// <summary>
        /// Defines the Synchronous
        /// </summary>
        Synchronous = 1,

        /// <summary>
        /// Defines the Framed
        /// </summary>
        Framed = 2
    }

    /// <summary>
    /// Parity modes used by the framed FSK receiver
    /// </summary>
    public enum FskParity {
        /// <summary>
        /// Defines the None
        /// </summary>
        None = 0,

        /// <summary>
        /// Defines the Even
        /// </summary>
        Even = 1,

        /// <summary>
        /// Defines the Odd
        /// </summary>
        Odd = 2,

        /// <summary>
        /// Defines the Mark
        /// </summary>
        Mark = 3,

        /// <summary>
        /// Defines the Space
        /// </summary>
        Space = 4
    }

    /// <summary>
    /// Special callback values shared by the modem modules
    /// </summary>
    public enum FskSignalStatus {
        /// <summary>
        /// Defines the CarrierDown
        /// </summary>
        CarrierDown = -1,

        /// <summary>
        /// Defines the CarrierUp
        /// </summary>
        CarrierUp = -2,

        /// <summary>
        /// Defines the TrainingInProgress
        /// </summary>
        TrainingInProgress = -3,

        /// <summary>
        /// Defines the TrainingSucceeded
        /// </summary>
        TrainingSucceeded = -4,

        /// <summary>
        /// Defines the TrainingFailed
        /// </summary>
        TrainingFailed = -5,

        /// <summary>
        /// Defines the FramingOk
        /// </summary>
        FramingOk = -6,

        /// <summary>
        /// Defines the EndOfData
        /// </summary>
        EndOfData = -7,

        /// <summary>
        /// Defines the Abort
        /// </summary>
        Abort = -8,

        /// <summary>
        /// Defines the Break
        /// </summary>
        Break = -9,

        /// <summary>
        /// Defines the ShutdownComplete
        /// </summary>
        ShutdownComplete = -10,

        /// <summary>
        /// Defines the OctetReport
        /// </summary>
        OctetReport = -11,

        /// <summary>
        /// Defines the PoorSignalQuality
        /// </summary>
        PoorSignalQuality = -12,

        /// <summary>
        /// Defines the ModemRetrainOccurred
        /// </summary>
        ModemRetrainOccurred = -13,

        /// <summary>
        /// Defines the LinkConnected
        /// </summary>
        LinkConnected = -14,

        /// <summary>
        /// Defines the LinkDisconnected
        /// </summary>
        LinkDisconnected = -15,

        /// <summary>
        /// Defines the LinkError
        /// </summary>
        LinkError = -16,

        /// <summary>
        /// Defines the LinkIdle
        /// </summary>
        LinkIdle = -17
    }

    /// <summary>
    /// The FskGetBitDelegate
    /// </summary>
    /// <param name="userData">The userData<see cref="object?"/></param>
    /// <returns>The <see cref="int"/></returns>
    public delegate int FskGetBitDelegate(object? userData);

    /// <summary>
    /// The FskPutBitDelegate
    /// </summary>
    /// <param name="userData">The userData<see cref="object?"/></param>
    /// <param name="bitOrStatus">The bitOrStatus<see cref="int"/></param>
    public delegate void FskPutBitDelegate(object? userData, int bitOrStatus);

    /// <summary>
    /// The FskModemStatusDelegate
    /// </summary>
    /// <param name="userData">The userData<see cref="object?"/></param>
    /// <param name="status">The status<see cref="int"/></param>
    public delegate void FskModemStatusDelegate(object? userData, int status);

    /// <summary>
    /// Frequencies, levels and bit rate for one FSK modem channel.
    /// The bit rate is expressed in units of 1/100 bit per second, exactly as
    /// in the native <c>fsk_spec_t</c> structure
    /// </summary>
    public sealed class FskSpec {
        /// <summary>
        /// Initializes a new instance of the <see cref="FskSpec"/> class.
        /// </summary>
        /// <param name="name">The name<see cref="string"/></param>
        /// <param name="zeroFrequency">The zeroFrequency<see cref="int"/></param>
        /// <param name="oneFrequency">The oneFrequency<see cref="int"/></param>
        /// <param name="transmitLevel">The transmitLevel<see cref="int"/></param>
        /// <param name="minimumReceiveLevel">The minimumReceiveLevel<see cref="int"/></param>
        /// <param name="baudRateHundredths">The baudRateHundredths<see cref="int"/></param>
        public FskSpec(
            string name,
            int zeroFrequency,
            int oneFrequency,
            int transmitLevel,
            int minimumReceiveLevel,
            int baudRateHundredths) {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A modem name is required.", nameof(name));
            if (zeroFrequency <= 0)
                throw new ArgumentOutOfRangeException(nameof(zeroFrequency));
            if (oneFrequency <= 0)
                throw new ArgumentOutOfRangeException(nameof(oneFrequency));
            if (baudRateHundredths <= 0)
                throw new ArgumentOutOfRangeException(nameof(baudRateHundredths));

            Name = name;
            ZeroFrequency = zeroFrequency;
            OneFrequency = oneFrequency;
            TransmitLevel = transmitLevel;
            MinimumReceiveLevel = minimumReceiveLevel;
            BaudRateHundredths = baudRateHundredths;
        }

        /// <summary>
        /// Gets the Name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the ZeroFrequency
        /// </summary>
        public int ZeroFrequency { get; }

        /// <summary>
        /// Gets the OneFrequency
        /// </summary>
        public int OneFrequency { get; }

        /// <summary>
        /// Gets the TransmitLevel
        /// </summary>
        public int TransmitLevel { get; }

        /// <summary>
        /// Gets the MinimumReceiveLevel
        /// </summary>
        public int MinimumReceiveLevel { get; }

        /// <summary>
        /// Gets the BaudRateHundredths
        /// </summary>
        public int BaudRateHundredths { get; }

        /// <summary>
        /// Gets the BitsPerSecond
        /// </summary>
        public double BitsPerSecond => BaudRateHundredths / 100.0;

        /// <summary>
        /// The ToString
        /// </summary>
        /// <returns>The <see cref="string"/></returns>
        public override string ToString() => Name;
    }

    /// <summary>
    /// Working state for one FSK transmitter
    /// </summary>
    public sealed class FskTxState : IDisposable {
        /// <summary>
        /// Defines the _disposed
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Defines the BaudRate
        /// </summary>
        internal int BaudRate;

        /// <summary>
        /// Defines the GetBitCallback
        /// </summary>
        internal FskGetBitDelegate? GetBitCallback;

        /// <summary>
        /// Defines the GetBitUserData
        /// </summary>
        internal object? GetBitUserData;

        /// <summary>
        /// Defines the StatusHandler
        /// </summary>
        internal FskModemStatusDelegate? StatusHandler;

        /// <summary>
        /// Defines the StatusUserData
        /// </summary>
        internal object? StatusUserData;

        /// <summary>
        /// Defines the PhaseRates
        /// </summary>
        internal readonly int[] PhaseRates = new int[2];

        /// <summary>
        /// Defines the Scaling
        /// </summary>
        internal short Scaling;

        /// <summary>
        /// Defines the CurrentPhaseRate
        /// </summary>
        internal int CurrentPhaseRate;

        /// <summary>
        /// Defines the PhaseAccumulator
        /// </summary>
        internal uint PhaseAccumulator;

        /// <summary>
        /// Defines the BaudFraction
        /// </summary>
        internal int BaudFraction;

        /// <summary>
        /// Defines the Shutdown
        /// </summary>
        internal bool Shutdown;

        /// <summary>
        /// Gets a value indicating whether IsShutdown
        /// </summary>
        public bool IsShutdown {
            get {
                ThrowIfDisposed();
                return Shutdown;
            }
        }

        /// <summary>
        /// The Generate
        /// </summary>
        /// <param name="samples">The samples<see cref="Span{short}"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int Generate(Span<short> samples) {
            return Fsk.Transmit(this, samples);
        }

        /// <summary>
        /// The Restart
        /// </summary>
        /// <param name="spec">The spec<see cref="FskSpec"/></param>
        public void Restart(FskSpec spec) {
            Fsk.RestartTransmitter(this, spec);
        }

        /// <summary>
        /// The SetPower
        /// </summary>
        /// <param name="powerDbm0">The powerDbm0<see cref="float"/></param>
        public void SetPower(float powerDbm0) {
            Fsk.SetTransmitPower(this, powerDbm0);
        }

        /// <summary>
        /// The Dispose
        /// </summary>
        public void Dispose() {
            if (_disposed)
                return;

            GetBitCallback = null;
            GetBitUserData = null;
            StatusHandler = null;
            StatusUserData = null;
            Array.Clear(PhaseRates, 0, PhaseRates.Length);
            Scaling = 0;
            CurrentPhaseRate = 0;
            PhaseAccumulator = 0;
            BaudFraction = 0;
            BaudRate = 0;
            Shutdown = true;
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// The ResetForInitialization
        /// </summary>
        internal void ResetForInitialization() {
            _disposed = false;
            GetBitCallback = null;
            GetBitUserData = null;
            StatusHandler = null;
            StatusUserData = null;
            Array.Clear(PhaseRates, 0, PhaseRates.Length);
            Scaling = 0;
            CurrentPhaseRate = 0;
            PhaseAccumulator = 0;
            BaudFraction = 0;
            BaudRate = 0;
            Shutdown = false;
        }

        /// <summary>
        /// The ThrowIfDisposed
        /// </summary>
        internal void ThrowIfDisposed() {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FskTxState));
        }
    }

    /// <summary>
    /// Working state for one FSK receiver
    /// </summary>
    public sealed class FskRxState : IDisposable {
        /// <summary>
        /// Defines the _disposed
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Defines the BaudRate
        /// </summary>
        internal int BaudRate;

        /// <summary>
        /// Defines the FramingMode
        /// </summary>
        internal FskFrameMode FramingMode;

        /// <summary>
        /// Defines the DataBits
        /// </summary>
        internal int DataBits;

        /// <summary>
        /// Defines the Parity
        /// </summary>
        internal FskParity Parity;

        /// <summary>
        /// Defines the StopBits
        /// </summary>
        internal int StopBits;

        /// <summary>
        /// Defines the TotalDataBits
        /// </summary>
        internal int TotalDataBits;

        /// <summary>
        /// Defines the PutBitCallback
        /// </summary>
        internal FskPutBitDelegate? PutBitCallback;

        /// <summary>
        /// Defines the PutBitUserData
        /// </summary>
        internal object? PutBitUserData;

        /// <summary>
        /// Defines the StatusHandler
        /// </summary>
        internal FskModemStatusDelegate? StatusHandler;

        /// <summary>
        /// Defines the StatusUserData
        /// </summary>
        internal object? StatusUserData;

        /// <summary>
        /// Defines the CarrierOnPower
        /// </summary>
        internal int CarrierOnPower;

        /// <summary>
        /// Defines the CarrierOffPower
        /// </summary>
        internal int CarrierOffPower;

        /// <summary>
        /// Defines the PowerShift
        /// </summary>
        internal int PowerShift;

        /// <summary>
        /// Defines the PowerReading
        /// </summary>
        internal int PowerReading;

        /// <summary>
        /// Defines the LastSample
        /// </summary>
        internal short LastSample;

        /// <summary>
        /// Defines the SignalPresentCounter
        /// </summary>
        internal int SignalPresentCounter;

        /// <summary>
        /// Defines the PhaseRates
        /// </summary>
        internal readonly int[] PhaseRates = new int[2];

        /// <summary>
        /// Defines the PhaseAccumulators
        /// </summary>
        internal readonly uint[] PhaseAccumulators = new uint[2];

        /// <summary>
        /// Defines the CorrelationSpan
        /// </summary>
        internal int CorrelationSpan;

        /// <summary>
        /// Defines the Window
        /// </summary>
        internal readonly FskComplex32[][] Window =
        {
            new FskComplex32[Fsk.MaximumWindowLength],
            new FskComplex32[Fsk.MaximumWindowLength]
        };

        /// <summary>
        /// Defines the Dot
        /// </summary>
        internal readonly FskComplex32[] Dot = new FskComplex32[2];

        /// <summary>
        /// Defines the BufferPosition
        /// </summary>
        internal int BufferPosition;

        /// <summary>
        /// Defines the FramePosition
        /// </summary>
        internal int FramePosition;

        /// <summary>
        /// Defines the FrameInProgress
        /// </summary>
        internal ushort FrameInProgress;

        /// <summary>
        /// Defines the BaudPhase
        /// </summary>
        internal int BaudPhase;

        /// <summary>
        /// Defines the LastBit
        /// </summary>
        internal int LastBit;

        /// <summary>
        /// Defines the ScalingShift
        /// </summary>
        internal int ScalingShift;

        /// <summary>
        /// Defines the ParityErrors
        /// </summary>
        internal int ParityErrors;

        /// <summary>
        /// Defines the FramingErrors
        /// </summary>
        internal int FramingErrors;

        /// <summary>
        /// Gets a value indicating whether SignalPresent
        /// </summary>
        public bool SignalPresent {
            get {
                ThrowIfDisposed();
                return SignalPresentCounter > 0;
            }
        }

        /// <summary>
        /// Gets the Mode
        /// </summary>
        public FskFrameMode Mode {
            get {
                ThrowIfDisposed();
                return FramingMode;
            }
        }

        /// <summary>
        /// Gets the SignalPowerDbm0
        /// </summary>
        public float SignalPowerDbm0 {
            get {
                return Fsk.GetReceiveSignalPower(this);
            }
        }

        /// <summary>
        /// The Process
        /// </summary>
        /// <param name="samples">The samples<see cref="ReadOnlySpan{short}"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int Process(ReadOnlySpan<short> samples) {
            return Fsk.Receive(this, samples);
        }

        /// <summary>
        /// The FillIn
        /// </summary>
        /// <param name="missingSampleCount">The missingSampleCount<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int FillIn(int missingSampleCount) {
            return Fsk.ReceiveFillIn(this, missingSampleCount);
        }

        /// <summary>
        /// The Restart
        /// </summary>
        /// <param name="spec">The spec<see cref="FskSpec"/></param>
        /// <param name="mode">The mode<see cref="FskFrameMode"/></param>
        public void Restart(FskSpec spec, FskFrameMode mode) {
            Fsk.RestartReceiver(this, spec, mode);
        }

        /// <summary>
        /// The SetSignalCutoff
        /// </summary>
        /// <param name="cutoffDbm0">The cutoffDbm0<see cref="float"/></param>
        public void SetSignalCutoff(float cutoffDbm0) {
            Fsk.SetReceiveSignalCutoff(this, cutoffDbm0);
        }

        /// <summary>
        /// The SetFrameParameters
        /// </summary>
        /// <param name="dataBits">The dataBits<see cref="int"/></param>
        /// <param name="parity">The parity<see cref="FskParity"/></param>
        /// <param name="stopBits">The stopBits<see cref="int"/></param>
        public void SetFrameParameters(int dataBits, FskParity parity, int stopBits) {
            Fsk.SetReceiveFrameParameters(this, dataBits, parity, stopBits);
        }

        /// <summary>
        /// The GetParityErrors
        /// </summary>
        /// <param name="reset">The reset<see cref="bool"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int GetParityErrors(bool reset) {
            return Fsk.GetParityErrors(this, reset);
        }

        /// <summary>
        /// The GetFramingErrors
        /// </summary>
        /// <param name="reset">The reset<see cref="bool"/></param>
        /// <returns>The <see cref="int"/></returns>
        public int GetFramingErrors(bool reset) {
            return Fsk.GetFramingErrors(this, reset);
        }

        /// <summary>
        /// The Dispose
        /// </summary>
        public void Dispose() {
            if (_disposed)
                return;

            PutBitCallback = null;
            PutBitUserData = null;
            StatusHandler = null;
            StatusUserData = null;
            ClearRuntimeState();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// The ResetForInitialization
        /// </summary>
        internal void ResetForInitialization() {
            _disposed = false;
            PutBitCallback = null;
            PutBitUserData = null;
            StatusHandler = null;
            StatusUserData = null;
            ClearRuntimeState();
        }

        /// <summary>
        /// The ThrowIfDisposed
        /// </summary>
        internal void ThrowIfDisposed() {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FskRxState));
        }

        /// <summary>
        /// The ClearRuntimeState
        /// </summary>
        private void ClearRuntimeState() {
            BaudRate = 0;
            FramingMode = FskFrameMode.Asynchronous;
            DataBits = 0;
            Parity = FskParity.None;
            StopBits = 0;
            TotalDataBits = 0;
            CarrierOnPower = 0;
            CarrierOffPower = 0;
            PowerShift = 0;
            PowerReading = 0;
            LastSample = 0;
            SignalPresentCounter = 0;
            Array.Clear(PhaseRates, 0, PhaseRates.Length);
            Array.Clear(PhaseAccumulators, 0, PhaseAccumulators.Length);
            CorrelationSpan = 0;
            Array.Clear(Window[0], 0, Window[0].Length);
            Array.Clear(Window[1], 0, Window[1].Length);
            Array.Clear(Dot, 0, Dot.Length);
            BufferPosition = 0;
            FramePosition = 0;
            FrameInProgress = 0;
            BaudPhase = 0;
            LastBit = 0;
            ScalingShift = 0;
            ParityErrors = 0;
            FramingErrors = 0;
        }
    }

    /// <summary>
    /// Defines the <see cref="FskComplex32" />
    /// </summary>
    internal struct FskComplex32 {
        /// <summary>
        /// Defines the Real
        /// </summary>
        internal int Real;

        /// <summary>
        /// Defines the Imaginary
        /// </summary>
        internal int Imaginary;
    }

    /// <summary>
    /// Managed FSK transmitter and receiver implementation corresponding to
    /// the complete public and private declarations from fsk.h and the DSP
    /// implementation from fsk.c
    /// </summary>
    public static class Fsk {
        /// <summary>
        /// Defines the SampleRate
        /// </summary>
        public const int SampleRate = 8000;

        /// <summary>
        /// Defines the MaximumWindowLength
        /// </summary>
        public const int MaximumWindowLength = 128;

        /// <summary>
        /// Defines the RateScale
        /// </summary>
        private const int RateScale = 100;

        /// <summary>
        /// Defines the BaudThreshold
        /// </summary>
        private const int BaudThreshold = SampleRate * RateScale;

        /// <summary>
        /// Defines the Dbm0MaximumPower
        /// </summary>
        private const float Dbm0MaximumPower = 3.14f + 3.02f;

        /// <summary>
        /// Defines the Dbm0MaximumSinePower
        /// </summary>
        private const float Dbm0MaximumSinePower = 3.14f;

        /// <summary>
        /// Defines the DdsSteps
        /// </summary>
        private const int DdsSteps = 256;

        /// <summary>
        /// Defines the DdsShift
        /// </summary>
        private const int DdsShift = 22;

        /// <summary>
        /// Defines the PresetsInternal
        /// </summary>
        private static readonly FskSpec[] PresetsInternal =
        {
            new FskSpec("V21 ch 1", 1180, 980, -14, -30, 30000),
            new FskSpec("V21 ch 2", 1850, 1650, -14, -30, 30000),
            new FskSpec("V23 ch 1", 2100, 1300, -14, -30, 120000),
            new FskSpec("V23 ch 2", 450, 390, -14, -30, 7500),
            new FskSpec("Bell103 ch 1", 1070, 1270, -14, -30, 30000),
            new FskSpec("Bell103 ch 2", 2025, 2225, -14, -30, 30000),
            new FskSpec("Bell202", 2200, 1200, -14, -30, 120000),
            new FskSpec("Weitbrecht 45.45", 1800, 1400, -14, -30, 4545),
            new FskSpec("Weitbrecht 50", 1800, 1400, -14, -30, 5000),
            new FskSpec("Weitbrecht 47.6", 1800, 1400, -14, -30, 4760),
            new FskSpec("V21 (110bps) ch 1", 1180, 980, -14, -30, 11000)
        };

        /// <summary>
        /// Defines the SineTable
        /// </summary>
        private static readonly short[] SineTable =
        {
            0, 201, 402, 603, 804, 1005, 1206, 1407, 1608, 1809, 2009, 2210,
            2410, 2611, 2811, 3012, 3212, 3412, 3612, 3811, 4011, 4210, 4410, 4609,
            4808, 5007, 5205, 5404, 5602, 5800, 5998, 6195, 6393, 6590, 6786, 6983,
            7179, 7375, 7571, 7767, 7962, 8157, 8351, 8545, 8739, 8933, 9126, 9319,
            9512, 9704, 9896, 10087, 10278, 10469, 10659, 10849, 11039, 11228, 11417, 11605,
            11793, 11980, 12167, 12353, 12539, 12725, 12910, 13094, 13279, 13462, 13645, 13828,
            14010, 14191, 14372, 14553, 14732, 14912, 15090, 15269, 15446, 15623, 15800, 15976,
            16151, 16325, 16499, 16673, 16846, 17018, 17189, 17360, 17530, 17700, 17869, 18037,
            18204, 18371, 18537, 18703, 18868, 19032, 19195, 19357, 19519, 19680, 19841, 20000,
            20159, 20317, 20475, 20631, 20787, 20942, 21096, 21250, 21403, 21554, 21705, 21856,
            22005, 22154, 22301, 22448, 22594, 22739, 22884, 23027, 23170, 23311, 23452, 23592,
            23731, 23870, 24007, 24143, 24279, 24413, 24547, 24680, 24811, 24942, 25072, 25201,
            25329, 25456, 25582, 25708, 25832, 25955, 26077, 26198, 26319, 26438, 26556, 26674,
            26790, 26905, 27019, 27133, 27245, 27356, 27466, 27575, 27683, 27790, 27896, 28001,
            28105, 28208, 28310, 28411, 28510, 28609, 28706, 28803, 28898, 28992, 29085, 29177,
            29268, 29358, 29447, 29534, 29621, 29706, 29791, 29874, 29956, 30037, 30117, 30195,
            30273, 30349, 30424, 30498, 30571, 30643, 30714, 30783, 30852, 30919, 30985, 31050,
            31113, 31176, 31237, 31297, 31356, 31414, 31470, 31526, 31580, 31633, 31685, 31736,
            31785, 31833, 31880, 31926, 31971, 32014, 32057, 32098, 32137, 32176, 32213, 32250,
            32285, 32318, 32351, 32382, 32412, 32441, 32469, 32495, 32521, 32545, 32567, 32589,
            32609, 32628, 32646, 32663, 32678, 32692, 32705, 32717, 32728, 32737, 32745, 32752,
            32757, 32761, 32765, 32766, 32767
        };

        /// <summary>
        /// Gets the PresetSpecs
        /// </summary>
        public static IReadOnlyList<FskSpec> PresetSpecs => PresetsInternal;

        /// <summary>
        /// The GetPreset
        /// </summary>
        /// <param name="preset">The preset<see cref="FskPreset"/></param>
        /// <returns>The <see cref="FskSpec"/></returns>
        public static FskSpec GetPreset(FskPreset preset) {
            int index = (int)preset;
            if ((uint)index >= (uint)PresetsInternal.Length)
                throw new ArgumentOutOfRangeException(nameof(preset));

            return PresetsInternal[index];
        }

        /// <summary>
        /// The InitializeTransmitter
        /// </summary>
        /// <param name="state">The state<see cref="FskTxState?"/></param>
        /// <param name="spec">The spec<see cref="FskSpec"/></param>
        /// <param name="getBit">The getBit<see cref="FskGetBitDelegate"/></param>
        /// <param name="userData">The userData<see cref="object?"/></param>
        /// <returns>The <see cref="FskTxState"/></returns>
        public static FskTxState InitializeTransmitter(
            FskTxState? state,
            FskSpec spec,
            FskGetBitDelegate getBit,
            object? userData = null) {
            ArgumentNullException.ThrowIfNull(spec);
            ArgumentNullException.ThrowIfNull(getBit);

            state ??= new FskTxState();
            state.ResetForInitialization();
            state.GetBitCallback = getBit;
            state.GetBitUserData = userData;
            RestartTransmitter(state, spec);
            return state;
        }

        /// <summary>
        /// The RestartTransmitter
        /// </summary>
        /// <param name="state">The state<see cref="FskTxState"/></param>
        /// <param name="spec">The spec<see cref="FskSpec"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int RestartTransmitter(FskTxState state, FskSpec spec) {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(spec);
            state.ThrowIfDisposed();

            state.BaudRate = spec.BaudRateHundredths;
            state.PhaseRates[0] = DdsPhaseRate(spec.ZeroFrequency);
            state.PhaseRates[1] = DdsPhaseRate(spec.OneFrequency);
            state.Scaling = DdsScalingDbm0(spec.TransmitLevel);
            state.PhaseAccumulator = 0;
            state.BaudFraction = 0;
            state.CurrentPhaseRate = state.PhaseRates[1];
            state.Shutdown = false;
            return 0;
        }

        /// <summary>
        /// The SetTransmitPower
        /// </summary>
        /// <param name="state">The state<see cref="FskTxState"/></param>
        /// <param name="powerDbm0">The powerDbm0<see cref="float"/></param>
        public static void SetTransmitPower(FskTxState state, float powerDbm0) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();
            state.Scaling = DdsScalingDbm0(powerDbm0);
        }

        /// <summary>
        /// The SetTransmitBitSource
        /// </summary>
        /// <param name="state">The state<see cref="FskTxState"/></param>
        /// <param name="getBit">The getBit<see cref="FskGetBitDelegate"/></param>
        /// <param name="userData">The userData<see cref="object?"/></param>
        public static void SetTransmitBitSource(
            FskTxState state,
            FskGetBitDelegate getBit,
            object? userData = null) {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(getBit);
            state.ThrowIfDisposed();
            state.GetBitCallback = getBit;
            state.GetBitUserData = userData;
        }

        /// <summary>
        /// The SetTransmitStatusHandler
        /// </summary>
        /// <param name="state">The state<see cref="FskTxState"/></param>
        /// <param name="handler">The handler<see cref="FskModemStatusDelegate?"/></param>
        /// <param name="userData">The userData<see cref="object?"/></param>
        public static void SetTransmitStatusHandler(
            FskTxState state,
            FskModemStatusDelegate? handler,
            object? userData = null) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();
            state.StatusHandler = handler;
            state.StatusUserData = userData;
        }

        /// <summary>
        /// The Transmit
        /// </summary>
        /// <param name="state">The state<see cref="FskTxState"/></param>
        /// <param name="samples">The samples<see cref="Span{short}"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int Transmit(FskTxState state, Span<short> samples) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();

            if (state.Shutdown)
                return 0;

            FskGetBitDelegate getBit = state.GetBitCallback
                ?? throw new InvalidOperationException("The FSK transmitter has no bit-source callback.");

            int sampleIndex;
            for (sampleIndex = 0; sampleIndex < samples.Length; sampleIndex++) {
                state.BaudFraction += state.BaudRate;
                if (state.BaudFraction >= BaudThreshold) {
                    state.BaudFraction -= BaudThreshold;
                    int bit = getBit(state.GetBitUserData);

                    if (bit == (int)FskSignalStatus.EndOfData) {
                        state.StatusHandler?.Invoke(
                            state.StatusUserData,
                            (int)FskSignalStatus.EndOfData);
                        state.StatusHandler?.Invoke(
                            state.StatusUserData,
                            (int)FskSignalStatus.ShutdownComplete);
                        state.Shutdown = true;
                        break;
                    }

                    state.CurrentPhaseRate = state.PhaseRates[bit & 1];
                }

                samples[sampleIndex] = DdsMod(
                    ref state.PhaseAccumulator,
                    state.CurrentPhaseRate,
                    state.Scaling);
            }

            return sampleIndex;
        }

        /// <summary>
        /// The InitializeReceiver
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState?"/></param>
        /// <param name="spec">The spec<see cref="FskSpec"/></param>
        /// <param name="framingMode">The framingMode<see cref="FskFrameMode"/></param>
        /// <param name="putBit">The putBit<see cref="FskPutBitDelegate"/></param>
        /// <param name="userData">The userData<see cref="object?"/></param>
        /// <returns>The <see cref="FskRxState"/></returns>
        public static FskRxState InitializeReceiver(
            FskRxState? state,
            FskSpec spec,
            FskFrameMode framingMode,
            FskPutBitDelegate putBit,
            object? userData = null) {
            ArgumentNullException.ThrowIfNull(spec);
            ArgumentNullException.ThrowIfNull(putBit);
            ValidateFrameMode(framingMode);

            state ??= new FskRxState();
            state.ResetForInitialization();
            state.PutBitCallback = putBit;
            state.PutBitUserData = userData;
            RestartReceiver(state, spec, framingMode);
            return state;
        }

        /// <summary>
        /// The RestartReceiver
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <param name="spec">The spec<see cref="FskSpec"/></param>
        /// <param name="framingMode">The framingMode<see cref="FskFrameMode"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int RestartReceiver(
            FskRxState state,
            FskSpec spec,
            FskFrameMode framingMode) {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(spec);
            state.ThrowIfDisposed();
            ValidateFrameMode(framingMode);

            state.BaudRate = spec.BaudRateHundredths;
            state.FramingMode = framingMode;
            if (framingMode == FskFrameMode.Framed)
                SetReceiveFrameParameters(state, 8, FskParity.None, 1);

            SetReceiveSignalCutoff(state, spec.MinimumReceiveLevel);

            state.PhaseRates[0] = DdsPhaseRate(spec.ZeroFrequency);
            state.PhaseRates[1] = DdsPhaseRate(spec.OneFrequency);
            state.PhaseAccumulators[0] = 0;
            state.PhaseAccumulators[1] = 0;
            state.LastSample = 0;

            state.CorrelationSpan = BaudThreshold / state.BaudRate;
            if (state.CorrelationSpan > MaximumWindowLength)
                state.CorrelationSpan = MaximumWindowLength;

            state.ScalingShift = 0;
            int chop = state.CorrelationSpan;
            while (chop != 0) {
                state.ScalingShift++;
                chop >>= 1;
            }

            state.BaudPhase = 0;
            state.FramePosition = -2;
            state.FrameInProgress = 0;
            state.LastBit = 0;

            state.PowerShift = 4;
            state.PowerReading = 0;
            state.SignalPresentCounter = 0;
            return 0;
        }

        /// <summary>
        /// The SetReceiveSignalCutoff
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <param name="cutoffDbm0">The cutoffDbm0<see cref="float"/></param>
        public static void SetReceiveSignalCutoff(FskRxState state, float cutoffDbm0) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();

            state.CarrierOnPower = PowerMeterLevelDbm0(cutoffDbm0 + 2.5f - 5.3f);
            state.CarrierOffPower = PowerMeterLevelDbm0(cutoffDbm0 - 2.5f - 5.3f);
        }

        /// <summary>
        /// The GetReceiveSignalPower
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <returns>The <see cref="float"/></returns>
        public static float GetReceiveSignalPower(FskRxState state) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();
            return PowerMeterCurrentDbm0(state.PowerReading);
        }

        /// <summary>
        /// The SetReceiveBitSink
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <param name="putBit">The putBit<see cref="FskPutBitDelegate"/></param>
        /// <param name="userData">The userData<see cref="object?"/></param>
        public static void SetReceiveBitSink(
            FskRxState state,
            FskPutBitDelegate putBit,
            object? userData = null) {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(putBit);
            state.ThrowIfDisposed();
            state.PutBitCallback = putBit;
            state.PutBitUserData = userData;
        }

        /// <summary>
        /// The SetReceiveStatusHandler
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <param name="handler">The handler<see cref="FskModemStatusDelegate?"/></param>
        /// <param name="userData">The userData<see cref="object?"/></param>
        public static void SetReceiveStatusHandler(
            FskRxState state,
            FskModemStatusDelegate? handler,
            object? userData = null) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();
            state.StatusHandler = handler;
            state.StatusUserData = userData;
        }

        /// <summary>
        /// The SetReceiveFrameParameters
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <param name="dataBits">The dataBits<see cref="int"/></param>
        /// <param name="parity">The parity<see cref="FskParity"/></param>
        /// <param name="stopBits">The stopBits<see cref="int"/></param>
        public static void SetReceiveFrameParameters(
            FskRxState state,
            int dataBits,
            FskParity parity,
            int stopBits) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();

            if (state.FramingMode != FskFrameMode.Framed)
                return;

            if (dataBits is < 1 or > 15)
                throw new ArgumentOutOfRangeException(nameof(dataBits));
            if (!Enum.IsDefined(parity))
                throw new ArgumentOutOfRangeException(nameof(parity));
            if (stopBits is < 1 or > 2)
                throw new ArgumentOutOfRangeException(nameof(stopBits));

            state.DataBits = dataBits;
            state.Parity = parity;
            state.StopBits = stopBits;
            state.TotalDataBits = dataBits;
            if (parity != FskParity.None)
                state.TotalDataBits++;
        }

        /// <summary>
        /// The GetParityErrors
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <param name="reset">The reset<see cref="bool"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int GetParityErrors(FskRxState state, bool reset) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();
            int errors = state.ParityErrors;
            if (reset)
                state.ParityErrors = 0;
            return errors;
        }

        /// <summary>
        /// The GetFramingErrors
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <param name="reset">The reset<see cref="bool"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int GetFramingErrors(FskRxState state, bool reset) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();
            int errors = state.FramingErrors;
            if (reset)
                state.FramingErrors = 0;
            return errors;
        }

        /// <summary>
        /// The Receive
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <param name="samples">The samples<see cref="ReadOnlySpan{short}"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int Receive(FskRxState state, ReadOnlySpan<short> samples) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();

            FskPutBitDelegate putBit = state.PutBitCallback
                ?? throw new InvalidOperationException("The FSK receiver has no bit-sink callback.");

            int bufferPosition = state.BufferPosition;
            Span<int> sums = stackalloc int[2];

            for (int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex++) {
                short inputSample = samples[sampleIndex];

                for (int tone = 0; tone < 2; tone++) {
                    FskComplex32 dot = state.Dot[tone];
                    FskComplex32 oldWindow = state.Window[tone][bufferPosition];
                    dot.Real = unchecked(dot.Real - oldWindow.Real);
                    dot.Imaginary = unchecked(dot.Imaginary - oldWindow.Imaginary);

                    FskComplex32 phase = DdsComplex(
                        ref state.PhaseAccumulators[tone],
                        state.PhaseRates[tone]);

                    FskComplex32 newWindow;
                    newWindow.Real = unchecked((phase.Real * inputSample) >> state.ScalingShift);
                    newWindow.Imaginary = unchecked((phase.Imaginary * inputSample) >> state.ScalingShift);
                    state.Window[tone][bufferPosition] = newWindow;

                    dot.Real = unchecked(dot.Real + newWindow.Real);
                    dot.Imaginary = unchecked(dot.Imaginary + newWindow.Imaginary);
                    state.Dot[tone] = dot;

                    int component = dot.Real >> 15;
                    int sum = unchecked(component * component);
                    component = dot.Imaginary >> 15;
                    sums[tone] = unchecked(sum + component * component);
                }

                short halfSample = unchecked((short)(inputSample >> 1));
                short highPassSample = unchecked((short)(halfSample - state.LastSample));
                int power = PowerMeterUpdate(state, highPassSample);
                state.LastSample = halfSample;

                if (state.SignalPresentCounter != 0) {
                    if (power < state.CarrierOffPower) {
                        state.SignalPresentCounter--;
                        if (state.SignalPresentCounter <= 0) {
                            ReportStatusChange(state, (int)FskSignalStatus.CarrierDown);
                            state.BaudPhase = 0;
                            continue;
                        }
                    }
                } else {
                    if (power < state.CarrierOnPower) {
                        state.BaudPhase = 0;
                        continue;
                    }

                    if (state.BaudPhase < (state.CorrelationSpan >> 1) - 30) {
                        state.BaudPhase++;
                        continue;
                    }

                    state.SignalPresentCounter = 1;
                    state.BaudPhase = 0;
                    state.FramePosition = -2;
                    state.FrameInProgress = 0;
                    state.LastBit = 0;
                    ReportStatusChange(state, (int)FskSignalStatus.CarrierUp);
                }

                int baudState = sums[0] < sums[1] ? 1 : 0;
                switch (state.FramingMode) {
                    case FskFrameMode.Synchronous:
                        if (state.LastBit != baudState) {
                            state.LastBit = baudState;
                            if (state.BaudPhase < SampleRate * 50)
                                state.BaudPhase += state.BaudRate >> 3;
                            else
                                state.BaudPhase -= state.BaudRate >> 3;
                        }

                        state.BaudPhase += state.BaudRate;
                        if (state.BaudPhase >= BaudThreshold) {
                            state.BaudPhase -= BaudThreshold;
                            putBit(state.PutBitUserData, baudState);
                        }
                        break;

                    case FskFrameMode.Asynchronous:
                        if (state.LastBit != baudState) {
                            state.LastBit = baudState;
                            state.BaudPhase = SampleRate * 50;
                        }

                        state.BaudPhase += state.BaudRate;
                        if (state.BaudPhase >= BaudThreshold) {
                            state.BaudPhase -= BaudThreshold;
                            putBit(state.PutBitUserData, baudState);
                        }
                        break;

                    case FskFrameMode.Framed:
                        ProcessFramedSample(state, baudState, putBit);
                        break;

                    default:
                        throw new InvalidOperationException($"Unsupported FSK framing mode: {state.FramingMode}.");
                }

                bufferPosition++;
                if (bufferPosition >= state.CorrelationSpan)
                    bufferPosition = 0;
            }

            state.BufferPosition = bufferPosition;
            return 0;
        }

        /// <summary>
        /// The ReceiveFillIn
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <param name="missingSampleCount">The missingSampleCount<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int ReceiveFillIn(FskRxState state, int missingSampleCount) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();
            if (missingSampleCount < 0)
                throw new ArgumentOutOfRangeException(nameof(missingSampleCount));

            int bufferPosition = state.BufferPosition;
            for (int sampleIndex = 0; sampleIndex < missingSampleCount; sampleIndex++) {
                for (int tone = 0; tone < 2; tone++) {
                    FskComplex32 dot = state.Dot[tone];
                    FskComplex32 oldWindow = state.Window[tone][bufferPosition];
                    dot.Real = unchecked(dot.Real - oldWindow.Real);
                    dot.Imaginary = unchecked(dot.Imaginary - oldWindow.Imaginary);

                    DdsAdvance(
                        ref state.PhaseAccumulators[tone],
                        state.PhaseRates[tone]);

                    FskComplex32 emptyWindow = default;
                    state.Window[tone][bufferPosition] = emptyWindow;
                    dot.Real = unchecked(dot.Real + emptyWindow.Real);
                    dot.Imaginary = unchecked(dot.Imaginary + emptyWindow.Imaginary);
                    state.Dot[tone] = dot;
                }

                // The native implementation intentionally leaves the buffer
                // position unchanged while replacing the current slot.
            }

            state.BufferPosition = bufferPosition;
            return 0;
        }

        /// <summary>
        /// The ReleaseTransmitter
        /// </summary>
        /// <param name="state">The state<see cref="FskTxState"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int ReleaseTransmitter(FskTxState state) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();
            return 0;
        }

        /// <summary>
        /// The FreeTransmitter
        /// </summary>
        /// <param name="state">The state<see cref="FskTxState?"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int FreeTransmitter(FskTxState? state) {
            state?.Dispose();
            return 0;
        }

        /// <summary>
        /// The ReleaseReceiver
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int ReleaseReceiver(FskRxState state) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();
            return 0;
        }

        /// <summary>
        /// The FreeReceiver
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState?"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int FreeReceiver(FskRxState? state) {
            state?.Dispose();
            return 0;
        }

        /// <summary>
        /// The ProcessFramedSample
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <param name="baudState">The baudState<see cref="int"/></param>
        /// <param name="putBit">The putBit<see cref="FskPutBitDelegate"/></param>
        private static void ProcessFramedSample(
            FskRxState state,
            int baudState,
            FskPutBitDelegate putBit) {
            if (state.FramePosition == -2) {
                if (baudState == 0) {
                    state.BaudPhase = SampleRate * (100 - 40) / 2;
                    state.FramePosition = -1;
                    state.FrameInProgress = 0;
                    state.LastBit = -1;
                }
                return;
            }

            if (state.FramePosition == -1) {
                if (baudState != 0) {
                    state.FramePosition = -2;
                } else {
                    state.BaudPhase += state.BaudRate;
                    if (state.BaudPhase >= BaudThreshold) {
                        state.FramePosition = 0;
                        state.LastBit = baudState;
                    }
                }
                return;
            }

            state.BaudPhase += state.BaudRate;
            if (state.BaudPhase < SampleRate * (100 - 40))
                return;

            if (state.LastBit < 0)
                state.LastBit = baudState;

            if (state.LastBit != baudState) {
                state.FramePosition = -2;
                state.FramingErrors++;
                return;
            }

            if (state.BaudPhase < BaudThreshold)
                return;

            int previousFramePosition = state.FramePosition;
            state.FramePosition++;
            if (previousFramePosition > state.TotalDataBits) {
                if (baudState == 1)
                    PutFrame(state, state.FrameInProgress, putBit);
                else
                    state.FramingErrors++;

                state.FramePosition = -2;
            } else {
                state.FrameInProgress = unchecked((ushort)(
                    (state.FrameInProgress >> 1) |
                    (baudState << 15)));
            }

            state.BaudPhase -= BaudThreshold;
            state.LastBit = -1;
        }

        /// <summary>
        /// The PutFrame
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <param name="frame">The frame<see cref="ushort"/></param>
        /// <param name="putBit">The putBit<see cref="FskPutBitDelegate"/></param>
        private static void PutFrame(
            FskRxState state,
            ushort frame,
            FskPutBitDelegate putBit) {
            if (state.Parity != FskParity.None) {
                int receivedParity = (frame >> 15) & 1;
                frame &= 0x7FFF;
                frame >>= 16 - state.TotalDataBits;

                int expectedParity;
                switch (state.Parity) {
                    case FskParity.Odd:
                        expectedParity = Parity8(unchecked((byte)frame)) ^ 1;
                        break;
                    case FskParity.Even:
                        expectedParity = Parity8(unchecked((byte)frame));
                        break;
                    case FskParity.Mark:
                        expectedParity = 1;
                        break;
                    case FskParity.Space:
                        expectedParity = 0;
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported FSK parity mode: {state.Parity}.");
                }

                if (receivedParity == expectedParity)
                    putBit(state.PutBitUserData, frame);
                else
                    state.ParityErrors++;
            } else {
                frame >>= 16 - state.TotalDataBits;
                putBit(state.PutBitUserData, frame);
            }
        }

        /// <summary>
        /// The ReportStatusChange
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <param name="status">The status<see cref="int"/></param>
        private static void ReportStatusChange(FskRxState state, int status) {
            if (state.StatusHandler is not null) {
                state.StatusHandler(state.StatusUserData, status);
            } else {
                state.PutBitCallback?.Invoke(state.PutBitUserData, status);
            }
        }

        /// <summary>
        /// The PowerMeterUpdate
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <param name="sample">The sample<see cref="short"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int PowerMeterUpdate(FskRxState state, short sample) {
            unchecked {
                int squared = sample * sample;
                state.PowerReading += (squared - state.PowerReading) >> state.PowerShift;
                return state.PowerReading;
            }
        }

        /// <summary>
        /// The PowerMeterLevelDbm0
        /// </summary>
        /// <param name="level">The level<see cref="float"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int PowerMeterLevelDbm0(float level) {
            level -= Dbm0MaximumPower;
            if (level > 0.0f)
                level = 0.0f;

            double powerRatio = Math.Pow(10.0, level / 10.0f);
            return unchecked((int)(powerRatio * (32767.0f * 32767.0f)));
        }

        /// <summary>
        /// The PowerMeterCurrentDbm0
        /// </summary>
        /// <param name="reading">The reading<see cref="int"/></param>
        /// <returns>The <see cref="float"/></returns>
        private static float PowerMeterCurrentDbm0(int reading) {
            if (reading <= 0)
                return -96.329f + Dbm0MaximumPower;

            return 10.0f * MathF.Log10(
                reading / (32767.0f * 32767.0f) + 1.0e-10f) +
                Dbm0MaximumPower;
        }

        /// <summary>
        /// The DdsPhaseRate
        /// </summary>
        /// <param name="frequency">The frequency<see cref="float"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int DdsPhaseRate(float frequency) {
            return unchecked((int)(frequency * 65536.0f * 65536.0f / SampleRate));
        }

        /// <summary>
        /// The DdsScalingDbm0
        /// </summary>
        /// <param name="level">The level<see cref="float"/></param>
        /// <returns>The <see cref="short"/></returns>
        private static short DdsScalingDbm0(float level) {
            double amplitudeRatio = Math.Pow(10.0, (level - Dbm0MaximumSinePower) / 20.0);
            return unchecked((short)(int)(amplitudeRatio * 32767.0));
        }

        /// <summary>
        /// The DdsLookup
        /// </summary>
        /// <param name="phase">The phase<see cref="uint"/></param>
        /// <returns>The <see cref="short"/></returns>
        private static short DdsLookup(uint phase) {
            uint reducedPhase = phase >> DdsShift;
            uint step = reducedPhase & (DdsSteps - 1u);
            if ((reducedPhase & DdsSteps) != 0)
                step = DdsSteps - step;

            short amplitude = SineTable[step];
            if ((reducedPhase & (2u * DdsSteps)) != 0)
                amplitude = unchecked((short)-amplitude);

            return amplitude;
        }

        /// <summary>
        /// The DdsAdvance
        /// </summary>
        /// <param name="phaseAccumulator">The phaseAccumulator<see cref="uint"/></param>
        /// <param name="phaseRate">The phaseRate<see cref="int"/></param>
        private static void DdsAdvance(ref uint phaseAccumulator, int phaseRate) {
            phaseAccumulator = unchecked(phaseAccumulator + (uint)phaseRate);
        }

        /// <summary>
        /// The DdsMod
        /// </summary>
        /// <param name="phaseAccumulator">The phaseAccumulator<see cref="uint"/></param>
        /// <param name="phaseRate">The phaseRate<see cref="int"/></param>
        /// <param name="scale">The scale<see cref="short"/></param>
        /// <returns>The <see cref="short"/></returns>
        private static short DdsMod(
            ref uint phaseAccumulator,
            int phaseRate,
            short scale) {
            int product = DdsLookup(phaseAccumulator) * scale;
            short result = unchecked((short)(product >> 15));
            phaseAccumulator = unchecked(phaseAccumulator + (uint)phaseRate);
            return result;
        }

        /// <summary>
        /// The DdsComplex
        /// </summary>
        /// <param name="phaseAccumulator">The phaseAccumulator<see cref="uint"/></param>
        /// <param name="phaseRate">The phaseRate<see cref="int"/></param>
        /// <returns>The <see cref="FskComplex32"/></returns>
        private static FskComplex32 DdsComplex(
            ref uint phaseAccumulator,
            int phaseRate) {
            FskComplex32 result;
            result.Real = DdsLookup(unchecked(phaseAccumulator + (1u << 30)));
            result.Imaginary = DdsLookup(phaseAccumulator);
            phaseAccumulator = unchecked(phaseAccumulator + (uint)phaseRate);
            return result;
        }

        /// <summary>
        /// The Parity8
        /// </summary>
        /// <param name="value">The value<see cref="byte"/></param>
        /// <returns>The <see cref="int"/></returns>
        private static int Parity8(byte value) {
            value = unchecked((byte)((value ^ (value >> 4)) & 0x0F));
            return (0x6996 >> value) & 1;
        }

        /// <summary>
        /// The ValidateFrameMode
        /// </summary>
        /// <param name="mode">The mode<see cref="FskFrameMode"/></param>
        private static void ValidateFrameMode(FskFrameMode mode) {
            if (!Enum.IsDefined(mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }

    /// <summary>
    /// Compatibility facade retaining the original native function names
    /// </summary>
    public static class FskApi {
        /// <summary>
        /// Defines the FSK_V21CH1
        /// </summary>
        public const int FSK_V21CH1 = (int)FskPreset.V21Channel1;

        /// <summary>
        /// Defines the FSK_V21CH2
        /// </summary>
        public const int FSK_V21CH2 = (int)FskPreset.V21Channel2;

        /// <summary>
        /// Defines the FSK_V23CH1
        /// </summary>
        public const int FSK_V23CH1 = (int)FskPreset.V23Channel1;

        /// <summary>
        /// Defines the FSK_V23CH2
        /// </summary>
        public const int FSK_V23CH2 = (int)FskPreset.V23Channel2;

        /// <summary>
        /// Defines the FSK_BELL103CH1
        /// </summary>
        public const int FSK_BELL103CH1 = (int)FskPreset.Bell103Channel1;

        /// <summary>
        /// Defines the FSK_BELL103CH2
        /// </summary>
        public const int FSK_BELL103CH2 = (int)FskPreset.Bell103Channel2;

        /// <summary>
        /// Defines the FSK_BELL202
        /// </summary>
        public const int FSK_BELL202 = (int)FskPreset.Bell202;

        /// <summary>
        /// Defines the FSK_WEITBRECHT_4545
        /// </summary>
        public const int FSK_WEITBRECHT_4545 = (int)FskPreset.Weitbrecht4545;

        /// <summary>
        /// Defines the FSK_WEITBRECHT_50
        /// </summary>
        public const int FSK_WEITBRECHT_50 = (int)FskPreset.Weitbrecht50;

        /// <summary>
        /// Defines the FSK_WEITBRECHT_476
        /// </summary>
        public const int FSK_WEITBRECHT_476 = (int)FskPreset.Weitbrecht476;

        /// <summary>
        /// Defines the FSK_V21CH1_110
        /// </summary>
        public const int FSK_V21CH1_110 = (int)FskPreset.V21Channel1At110Bps;

        /// <summary>
        /// Defines the FSK_FRAME_MODE_ASYNC
        /// </summary>
        public const int FSK_FRAME_MODE_ASYNC = (int)FskFrameMode.Asynchronous;

        /// <summary>
        /// Defines the FSK_FRAME_MODE_SYNC
        /// </summary>
        public const int FSK_FRAME_MODE_SYNC = (int)FskFrameMode.Synchronous;

        /// <summary>
        /// Defines the FSK_FRAME_MODE_FRAMED
        /// </summary>
        public const int FSK_FRAME_MODE_FRAMED = (int)FskFrameMode.Framed;

        /// <summary>
        /// Defines the FSK_MAX_WINDOW_LEN
        /// </summary>
        public const int FSK_MAX_WINDOW_LEN = Fsk.MaximumWindowLength;

        /// <summary>
        /// Gets the preset_fsk_specs
        /// </summary>
        public static IReadOnlyList<FskSpec> preset_fsk_specs => Fsk.PresetSpecs;

        /// <summary>
        /// The fsk_tx_init
        /// </summary>
        /// <param name="state">The state<see cref="FskTxState?"/></param>
        /// <param name="spec">The spec<see cref="FskSpec"/></param>
        /// <param name="getBit">The getBit<see cref="FskGetBitDelegate"/></param>
        /// <param name="userData">The userData<see cref="object?"/></param>
        /// <returns>The <see cref="FskTxState"/></returns>
        public static FskTxState fsk_tx_init(
            FskTxState? state,
            FskSpec spec,
            FskGetBitDelegate getBit,
            object? userData) =>
            Fsk.InitializeTransmitter(state, spec, getBit, userData);

        /// <summary>
        /// The fsk_tx_restart
        /// </summary>
        /// <param name="state">The state<see cref="FskTxState"/></param>
        /// <param name="spec">The spec<see cref="FskSpec"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int fsk_tx_restart(FskTxState state, FskSpec spec) =>
            Fsk.RestartTransmitter(state, spec);

        /// <summary>
        /// The fsk_tx_release
        /// </summary>
        /// <param name="state">The state<see cref="FskTxState"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int fsk_tx_release(FskTxState state) =>
            Fsk.ReleaseTransmitter(state);

        /// <summary>
        /// The fsk_tx_free
        /// </summary>
        /// <param name="state">The state<see cref="FskTxState?"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int fsk_tx_free(FskTxState? state) =>
            Fsk.FreeTransmitter(state);

        /// <summary>
        /// The fsk_tx_power
        /// </summary>
        /// <param name="state">The state<see cref="FskTxState"/></param>
        /// <param name="power">The power<see cref="float"/></param>
        public static void fsk_tx_power(FskTxState state, float power) =>
            Fsk.SetTransmitPower(state, power);

        /// <summary>
        /// The fsk_tx_set_get_bit
        /// </summary>
        /// <param name="state">The state<see cref="FskTxState"/></param>
        /// <param name="getBit">The getBit<see cref="FskGetBitDelegate"/></param>
        /// <param name="userData">The userData<see cref="object?"/></param>
        public static void fsk_tx_set_get_bit(
            FskTxState state,
            FskGetBitDelegate getBit,
            object? userData) =>
            Fsk.SetTransmitBitSource(state, getBit, userData);

        /// <summary>
        /// The fsk_tx_set_modem_status_handler
        /// </summary>
        /// <param name="state">The state<see cref="FskTxState"/></param>
        /// <param name="handler">The handler<see cref="FskModemStatusDelegate?"/></param>
        /// <param name="userData">The userData<see cref="object?"/></param>
        public static void fsk_tx_set_modem_status_handler(
            FskTxState state,
            FskModemStatusDelegate? handler,
            object? userData) =>
            Fsk.SetTransmitStatusHandler(state, handler, userData);

        /// <summary>
        /// The fsk_tx
        /// </summary>
        /// <param name="state">The state<see cref="FskTxState"/></param>
        /// <param name="samples">The samples<see cref="Span{short}"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int fsk_tx(FskTxState state, Span<short> samples) =>
            Fsk.Transmit(state, samples);

        /// <summary>
        /// The fsk_tx
        /// </summary>
        /// <param name="state">The state<see cref="FskTxState"/></param>
        /// <param name="samples">The samples<see cref="short[]"/></param>
        /// <param name="length">The length<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int fsk_tx(FskTxState state, short[] samples, int length) {
            ArgumentNullException.ThrowIfNull(samples);
            ValidateLength(samples.Length, length);
            return Fsk.Transmit(state, samples.AsSpan(0, length));
        }

        /// <summary>
        /// The fsk_rx_init
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState?"/></param>
        /// <param name="spec">The spec<see cref="FskSpec"/></param>
        /// <param name="framingMode">The framingMode<see cref="int"/></param>
        /// <param name="putBit">The putBit<see cref="FskPutBitDelegate"/></param>
        /// <param name="userData">The userData<see cref="object?"/></param>
        /// <returns>The <see cref="FskRxState"/></returns>
        public static FskRxState fsk_rx_init(
            FskRxState? state,
            FskSpec spec,
            int framingMode,
            FskPutBitDelegate putBit,
            object? userData) =>
            Fsk.InitializeReceiver(
                state,
                spec,
                (FskFrameMode)framingMode,
                putBit,
                userData);

        /// <summary>
        /// The fsk_rx_restart
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <param name="spec">The spec<see cref="FskSpec"/></param>
        /// <param name="framingMode">The framingMode<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int fsk_rx_restart(
            FskRxState state,
            FskSpec spec,
            int framingMode) =>
            Fsk.RestartReceiver(state, spec, (FskFrameMode)framingMode);

        /// <summary>
        /// The fsk_rx_release
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int fsk_rx_release(FskRxState state) =>
            Fsk.ReleaseReceiver(state);

        /// <summary>
        /// The fsk_rx_free
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState?"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int fsk_rx_free(FskRxState? state) =>
            Fsk.FreeReceiver(state);

        /// <summary>
        /// The fsk_rx_signal_power
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <returns>The <see cref="float"/></returns>
        public static float fsk_rx_signal_power(FskRxState state) =>
            Fsk.GetReceiveSignalPower(state);

        /// <summary>
        /// The fsk_rx_set_signal_cutoff
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <param name="cutoff">The cutoff<see cref="float"/></param>
        public static void fsk_rx_set_signal_cutoff(FskRxState state, float cutoff) =>
            Fsk.SetReceiveSignalCutoff(state, cutoff);

        /// <summary>
        /// The fsk_rx_set_frame_parameters
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <param name="dataBits">The dataBits<see cref="int"/></param>
        /// <param name="parity">The parity<see cref="int"/></param>
        /// <param name="stopBits">The stopBits<see cref="int"/></param>
        public static void fsk_rx_set_frame_parameters(
            FskRxState state,
            int dataBits,
            int parity,
            int stopBits) =>
            Fsk.SetReceiveFrameParameters(
                state,
                dataBits,
                (FskParity)parity,
                stopBits);

        /// <summary>
        /// The fsk_rx_get_parity_errors
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <param name="reset">The reset<see cref="bool"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int fsk_rx_get_parity_errors(FskRxState state, bool reset) =>
            Fsk.GetParityErrors(state, reset);

        /// <summary>
        /// The fsk_rx_get_framing_errors
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <param name="reset">The reset<see cref="bool"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int fsk_rx_get_framing_errors(FskRxState state, bool reset) =>
            Fsk.GetFramingErrors(state, reset);

        /// <summary>
        /// The fsk_rx_set_put_bit
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <param name="putBit">The putBit<see cref="FskPutBitDelegate"/></param>
        /// <param name="userData">The userData<see cref="object?"/></param>
        public static void fsk_rx_set_put_bit(
            FskRxState state,
            FskPutBitDelegate putBit,
            object? userData) =>
            Fsk.SetReceiveBitSink(state, putBit, userData);

        /// <summary>
        /// The fsk_rx_set_modem_status_handler
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <param name="handler">The handler<see cref="FskModemStatusDelegate?"/></param>
        /// <param name="userData">The userData<see cref="object?"/></param>
        public static void fsk_rx_set_modem_status_handler(
            FskRxState state,
            FskModemStatusDelegate? handler,
            object? userData) =>
            Fsk.SetReceiveStatusHandler(state, handler, userData);

        /// <summary>
        /// The fsk_rx
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <param name="samples">The samples<see cref="ReadOnlySpan{short}"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int fsk_rx(FskRxState state, ReadOnlySpan<short> samples) =>
            Fsk.Receive(state, samples);

        /// <summary>
        /// The fsk_rx
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <param name="samples">The samples<see cref="short[]"/></param>
        /// <param name="length">The length<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int fsk_rx(FskRxState state, short[] samples, int length) {
            ArgumentNullException.ThrowIfNull(samples);
            ValidateLength(samples.Length, length);
            return Fsk.Receive(state, samples.AsSpan(0, length));
        }

        /// <summary>
        /// The fsk_rx_fillin
        /// </summary>
        /// <param name="state">The state<see cref="FskRxState"/></param>
        /// <param name="length">The length<see cref="int"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int fsk_rx_fillin(FskRxState state, int length) =>
            Fsk.ReceiveFillIn(state, length);

        /// <summary>
        /// The ValidateLength
        /// </summary>
        /// <param name="availableLength">The availableLength<see cref="int"/></param>
        /// <param name="requestedLength">The requestedLength<see cref="int"/></param>
        private static void ValidateLength(int availableLength, int requestedLength) {
            if (requestedLength < 0 || requestedLength > availableLength)
                throw new ArgumentOutOfRangeException(nameof(requestedLength));
        }
    }
}
