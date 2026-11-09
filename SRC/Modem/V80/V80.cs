/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * V80.cs - Managed C# port of v80.c and v80.h
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2023 Steve Underwood
 *
 * This file is distributed under the terms of the GNU Lesser General Public
 * License version 2.1, matching the original source files.
 */

#nullable enable

namespace TKFaxEngine.Modem.V80;

/// <summary>
/// V.80 escape marker and in-band command codes.
/// Numeric values match the original <c>V80_*</c> definitions.
/// </summary>
public enum V80EscapeCode {
    /// <summary>
    /// Represents the <c>EscapeMarker</c> value.
    /// </summary>
    EscapeMarker = 0x19,
    /// <summary>
    /// Represents the <c>FromDteManufacturerExtended</c> value.
    /// </summary>
    FromDteManufacturerExtended = 0x20,
    /// <summary>
    /// Represents the <c>FromDteManufacturer1</c> value.
    /// </summary>
    FromDteManufacturer1 = 0x21,
    /// <summary>
    /// Represents the <c>FromDteManufacturer2</c> value.
    /// </summary>
    FromDteManufacturer2 = 0x22,
    /// <summary>
    /// Represents the <c>FromDteManufacturer3</c> value.
    /// </summary>
    FromDteManufacturer3 = 0x23,
    /// <summary>
    /// Represents the <c>FromDteManufacturer4</c> value.
    /// </summary>
    FromDteManufacturer4 = 0x24,
    /// <summary>
    /// Represents the <c>FromDteManufacturer5</c> value.
    /// </summary>
    FromDteManufacturer5 = 0x25,
    /// <summary>
    /// Represents the <c>FromDteManufacturer6</c> value.
    /// </summary>
    FromDteManufacturer6 = 0x26,
    /// <summary>
    /// Represents the <c>FromDteManufacturer7</c> value.
    /// </summary>
    FromDteManufacturer7 = 0x27,
    /// <summary>
    /// Represents the <c>FromDteManufacturer8</c> value.
    /// </summary>
    FromDteManufacturer8 = 0x28,
    /// <summary>
    /// Represents the <c>FromDteManufacturer9</c> value.
    /// </summary>
    FromDteManufacturer9 = 0x29,
    /// <summary>
    /// Represents the <c>FromDteManufacturer10</c> value.
    /// </summary>
    FromDteManufacturer10 = 0x2A,
    /// <summary>
    /// Represents the <c>FromDteManufacturer11</c> value.
    /// </summary>
    FromDteManufacturer11 = 0x2B,
    /// <summary>
    /// Represents the <c>FromDteManufacturer12</c> value.
    /// </summary>
    FromDteManufacturer12 = 0x2C,
    /// <summary>
    /// Represents the <c>FromDteManufacturer13</c> value.
    /// </summary>
    FromDteManufacturer13 = 0x2D,
    /// <summary>
    /// Represents the <c>FromDteManufacturer14</c> value.
    /// </summary>
    FromDteManufacturer14 = 0x2E,
    /// <summary>
    /// Represents the <c>FromDteManufacturer15</c> value.
    /// </summary>
    FromDteManufacturer15 = 0x2F,
    /// <summary>
    /// Represents the <c>FromDceManufacturerExtended</c> value.
    /// </summary>
    FromDceManufacturerExtended = 0x30,
    /// <summary>
    /// Represents the <c>FromDceManufacturer1</c> value.
    /// </summary>
    FromDceManufacturer1 = 0x31,
    /// <summary>
    /// Represents the <c>FromDceManufacturer2</c> value.
    /// </summary>
    FromDceManufacturer2 = 0x32,
    /// <summary>
    /// Represents the <c>FromDceManufacturer3</c> value.
    /// </summary>
    FromDceManufacturer3 = 0x33,
    /// <summary>
    /// Represents the <c>FromDceManufacturer4</c> value.
    /// </summary>
    FromDceManufacturer4 = 0x34,
    /// <summary>
    /// Represents the <c>FromDceManufacturer5</c> value.
    /// </summary>
    FromDceManufacturer5 = 0x35,
    /// <summary>
    /// Represents the <c>FromDceManufacturer6</c> value.
    /// </summary>
    FromDceManufacturer6 = 0x36,
    /// <summary>
    /// Represents the <c>FromDceManufacturer7</c> value.
    /// </summary>
    FromDceManufacturer7 = 0x37,
    /// <summary>
    /// Represents the <c>FromDceManufacturer8</c> value.
    /// </summary>
    FromDceManufacturer8 = 0x38,
    /// <summary>
    /// Represents the <c>FromDceManufacturer9</c> value.
    /// </summary>
    FromDceManufacturer9 = 0x39,
    /// <summary>
    /// Represents the <c>FromDceManufacturer10</c> value.
    /// </summary>
    FromDceManufacturer10 = 0x3A,
    /// <summary>
    /// Represents the <c>FromDceManufacturer11</c> value.
    /// </summary>
    FromDceManufacturer11 = 0x3B,
    /// <summary>
    /// Represents the <c>FromDceManufacturer12</c> value.
    /// </summary>
    FromDceManufacturer12 = 0x3C,
    /// <summary>
    /// Represents the <c>FromDceManufacturer13</c> value.
    /// </summary>
    FromDceManufacturer13 = 0x3D,
    /// <summary>
    /// Represents the <c>FromDceManufacturer14</c> value.
    /// </summary>
    FromDceManufacturer14 = 0x3E,
    /// <summary>
    /// Represents the <c>FromDceManufacturer15</c> value.
    /// </summary>
    FromDceManufacturer15 = 0x3F,
    /// <summary>
    /// Represents the <c>FromDteExtend0</c> value.
    /// </summary>
    FromDteExtend0 = 0x40,
    /// <summary>
    /// Represents the <c>FromDteExtend1</c> value.
    /// </summary>
    FromDteExtend1 = 0x41,
    /// <summary>
    /// Represents the <c>FromDteCircuit105Off</c> value.
    /// </summary>
    FromDteCircuit105Off = 0x42,
    /// <summary>
    /// Represents the <c>FromDteCircuit105On</c> value.
    /// </summary>
    FromDteCircuit105On = 0x43,
    /// <summary>
    /// Represents the <c>FromDteCircuit108Off</c> value.
    /// </summary>
    FromDteCircuit108Off = 0x44,
    /// <summary>
    /// Represents the <c>FromDteCircuit108On</c> value.
    /// </summary>
    FromDteCircuit108On = 0x45,
    /// <summary>
    /// Represents the <c>FromDteCircuit133Off</c> value.
    /// </summary>
    FromDteCircuit133Off = 0x46,
    /// <summary>
    /// Represents the <c>FromDteCircuit133On</c> value.
    /// </summary>
    FromDteCircuit133On = 0x47,
    /// <summary>
    /// Represents the <c>FromDteSingleEmPrime</c> value.
    /// </summary>
    FromDteSingleEmPrime = 0x58,
    /// <summary>
    /// Represents the <c>FromDteDoubleEmPrime</c> value.
    /// </summary>
    FromDteDoubleEmPrime = 0x59,
    /// <summary>
    /// Represents the <c>FromDteFlowOff</c> value.
    /// </summary>
    FromDteFlowOff = 0x5A,
    /// <summary>
    /// Represents the <c>FromDteFlowOn</c> value.
    /// </summary>
    FromDteFlowOn = 0x5B,
    /// <summary>
    /// Represents the <c>FromDteSingleEm</c> value.
    /// </summary>
    FromDteSingleEm = 0x5C,
    /// <summary>
    /// Represents the <c>FromDteDoubleEm</c> value.
    /// </summary>
    FromDteDoubleEm = 0x5D,
    /// <summary>
    /// Represents the <c>FromDtePoll</c> value.
    /// </summary>
    FromDtePoll = 0x5E,
    /// <summary>
    /// Represents the <c>FromDceExtend0</c> value.
    /// </summary>
    FromDceExtend0 = 0x60,
    /// <summary>
    /// Represents the <c>FromDceExtend1</c> value.
    /// </summary>
    FromDceExtend1 = 0x61,
    /// <summary>
    /// Represents the <c>FromDceCircuit106Off</c> value.
    /// </summary>
    FromDceCircuit106Off = 0x62,
    /// <summary>
    /// Represents the <c>FromDceCircuit106On</c> value.
    /// </summary>
    FromDceCircuit106On = 0x63,
    /// <summary>
    /// Represents the <c>FromDceCircuit107Off</c> value.
    /// </summary>
    FromDceCircuit107Off = 0x64,
    /// <summary>
    /// Represents the <c>FromDceCircuit107On</c> value.
    /// </summary>
    FromDceCircuit107On = 0x65,
    /// <summary>
    /// Represents the <c>FromDceCircuit109Off</c> value.
    /// </summary>
    FromDceCircuit109Off = 0x66,
    /// <summary>
    /// Represents the <c>FromDceCircuit109On</c> value.
    /// </summary>
    FromDceCircuit109On = 0x67,
    /// <summary>
    /// Represents the <c>FromDceCircuit110Off</c> value.
    /// </summary>
    FromDceCircuit110Off = 0x68,
    /// <summary>
    /// Represents the <c>FromDceCircuit110On</c> value.
    /// </summary>
    FromDceCircuit110On = 0x69,
    /// <summary>
    /// Represents the <c>FromDceCircuit125Off</c> value.
    /// </summary>
    FromDceCircuit125Off = 0x6A,
    /// <summary>
    /// Represents the <c>FromDceCircuit125On</c> value.
    /// </summary>
    FromDceCircuit125On = 0x6B,
    /// <summary>
    /// Represents the <c>FromDceCircuit132Off</c> value.
    /// </summary>
    FromDceCircuit132Off = 0x6C,
    /// <summary>
    /// Represents the <c>FromDceCircuit132On</c> value.
    /// </summary>
    FromDceCircuit132On = 0x6D,
    /// <summary>
    /// Represents the <c>FromDceCircuit142Off</c> value.
    /// </summary>
    FromDceCircuit142Off = 0x6E,
    /// <summary>
    /// Represents the <c>FromDceCircuit142On</c> value.
    /// </summary>
    FromDceCircuit142On = 0x6F,
    /// <summary>
    /// Represents the <c>FromDceSingleEmPrime</c> value.
    /// </summary>
    FromDceSingleEmPrime = 0x76,
    /// <summary>
    /// Represents the <c>FromDceDoubleEmPrime</c> value.
    /// </summary>
    FromDceDoubleEmPrime = 0x77,
    /// <summary>
    /// Represents the <c>FromDceOffLine</c> value.
    /// </summary>
    FromDceOffLine = 0x78,
    /// <summary>
    /// Represents the <c>FromDceOnLine</c> value.
    /// </summary>
    FromDceOnLine = 0x79,
    /// <summary>
    /// Represents the <c>FromDceFlowOff</c> value.
    /// </summary>
    FromDceFlowOff = 0x7A,
    /// <summary>
    /// Represents the <c>FromDceFlowOn</c> value.
    /// </summary>
    FromDceFlowOn = 0x7B,
    /// <summary>
    /// Represents the <c>FromDceSingleEm</c> value.
    /// </summary>
    FromDceSingleEm = 0x7C,
    /// <summary>
    /// Represents the <c>FromDceDoubleEm</c> value.
    /// </summary>
    FromDceDoubleEm = 0x7D,
    /// <summary>
    /// Represents the <c>FromDcePoll</c> value.
    /// </summary>
    FromDcePoll = 0x7E,
    /// <summary>
    /// Represents the <c>TransparencyT1</c> value.
    /// </summary>
    TransparencyT1 = 0x5C,
    /// <summary>
    /// Represents the <c>TransparencyT5</c> value.
    /// </summary>
    TransparencyT5 = 0x5D,
    /// <summary>
    /// Represents the <c>TransparencyT2</c> value.
    /// </summary>
    TransparencyT2 = 0x76,
    /// <summary>
    /// Represents the <c>TransparencyT6</c> value.
    /// </summary>
    TransparencyT6 = 0x77,
    /// <summary>
    /// Represents the <c>TransparencyT3</c> value.
    /// </summary>
    TransparencyT3 = 0xA0,
    /// <summary>
    /// Represents the <c>TransparencyT4</c> value.
    /// </summary>
    TransparencyT4 = 0xA1,
    /// <summary>
    /// Represents the <c>TransparencyT7</c> value.
    /// </summary>
    TransparencyT7 = 0xA2,
    /// <summary>
    /// Represents the <c>TransparencyT8</c> value.
    /// </summary>
    TransparencyT8 = 0xA3,
    /// <summary>
    /// Represents the <c>TransparencyT9</c> value.
    /// </summary>
    TransparencyT9 = 0xA4,
    /// <summary>
    /// Represents the <c>TransparencyT10</c> value.
    /// </summary>
    TransparencyT10 = 0xA5,
    /// <summary>
    /// Represents the <c>TransparencyT11</c> value.
    /// </summary>
    TransparencyT11 = 0xA6,
    /// <summary>
    /// Represents the <c>TransparencyT12</c> value.
    /// </summary>
    TransparencyT12 = 0xA7,
    /// <summary>
    /// Represents the <c>TransparencyT13</c> value.
    /// </summary>
    TransparencyT13 = 0xA8,
    /// <summary>
    /// Represents the <c>TransparencyT14</c> value.
    /// </summary>
    TransparencyT14 = 0xA9,
    /// <summary>
    /// Represents the <c>TransparencyT15</c> value.
    /// </summary>
    TransparencyT15 = 0xAA,
    /// <summary>
    /// Represents the <c>TransparencyT16</c> value.
    /// </summary>
    TransparencyT16 = 0xAB,
    /// <summary>
    /// Represents the <c>TransparencyT17</c> value.
    /// </summary>
    TransparencyT17 = 0xAC,
    /// <summary>
    /// Represents the <c>TransparencyT18</c> value.
    /// </summary>
    TransparencyT18 = 0xAD,
    /// <summary>
    /// Represents the <c>TransparencyT19</c> value.
    /// </summary>
    TransparencyT19 = 0xAE,
    /// <summary>
    /// Represents the <c>TransparencyT20</c> value.
    /// </summary>
    TransparencyT20 = 0xAF,
    /// <summary>
    /// Represents the <c>Mark</c> value.
    /// </summary>
    Mark = 0xB0,
    /// <summary>
    /// Represents the <c>Flag</c> value.
    /// </summary>
    Flag = 0xB1,
    /// <summary>
    /// Represents the <c>Error</c> value.
    /// </summary>
    Error = 0xB2,
    /// <summary>
    /// Represents the <c>Hunt</c> value.
    /// </summary>
    Hunt = 0xB3,
    /// <summary>
    /// Represents the <c>Underflow</c> value.
    /// </summary>
    Underflow = 0xB4,
    /// <summary>
    /// Represents the <c>TransmitOverrun</c> value.
    /// </summary>
    TransmitOverrun = 0xB5,
    /// <summary>
    /// Represents the <c>ReceiveOverrun</c> value.
    /// </summary>
    ReceiveOverrun = 0xB6,
    /// <summary>
    /// Represents the <c>Resume</c> value.
    /// </summary>
    Resume = 0xB7,
    /// <summary>
    /// Represents the <c>BufferOctetCount</c> value.
    /// </summary>
    BufferOctetCount = 0xB8,
    /// <summary>
    /// Represents the <c>DiscardedOctetCount</c> value.
    /// </summary>
    DiscardedOctetCount = 0xB9,
    /// <summary>
    /// Represents the <c>EndOfTransmission</c> value.
    /// </summary>
    EndOfTransmission = 0xBA,
    /// <summary>
    /// Represents the <c>EndOfTransmissionHalfDuplex</c> value.
    /// </summary>
    EndOfTransmissionHalfDuplex = 0xBA,
    /// <summary>
    /// Represents the <c>EscapeToCommandState</c> value.
    /// </summary>
    EscapeToCommandState = 0xBB,
    /// <summary>
    /// Represents the <c>RequestRateRenegotiation</c> value.
    /// </summary>
    RequestRateRenegotiation = 0xBC,
    /// <summary>
    /// Represents the <c>PrimaryChannel</c> value.
    /// </summary>
    PrimaryChannel = 0xBC,
    /// <summary>
    /// Represents the <c>RequestRateRetrain</c> value.
    /// </summary>
    RequestRateRetrain = 0xBD,
    /// <summary>
    /// Represents the <c>PrimaryChannelRetrain</c> value.
    /// </summary>
    PrimaryChannelRetrain = 0xBD,
    /// <summary>
    /// Represents the <c>Rate</c> value.
    /// </summary>
    Rate = 0xBE,
    /// <summary>
    /// Represents the <c>HalfDuplexRate</c> value.
    /// </summary>
    HalfDuplexRate = 0xBE,
    /// <summary>
    /// Represents the <c>ControlChannel</c> value.
    /// </summary>
    ControlChannel = 0xBF,
    /// <summary>
    /// Represents the <c>ControlChannelRetrain</c> value.
    /// </summary>
    ControlChannelRetrain = 0xC0
}

/// <summary>
/// Primary-channel data signalling rate codes used by V.80.
/// </summary>
public enum V80BitRateCode {
    /// <summary>
    /// Represents the <c>BitRate1200</c> value.
    /// </summary>
    BitRate1200 = 0x20,
    /// <summary>
    /// Represents the <c>BitRate2400</c> value.
    /// </summary>
    BitRate2400 = 0x21,
    /// <summary>
    /// Represents the <c>BitRate4800</c> value.
    /// </summary>
    BitRate4800 = 0x22,
    /// <summary>
    /// Represents the <c>BitRate7200</c> value.
    /// </summary>
    BitRate7200 = 0x23,
    /// <summary>
    /// Represents the <c>BitRate9600</c> value.
    /// </summary>
    BitRate9600 = 0x24,
    /// <summary>
    /// Represents the <c>BitRate12000</c> value.
    /// </summary>
    BitRate12000 = 0x25,
    /// <summary>
    /// Represents the <c>BitRate14400</c> value.
    /// </summary>
    BitRate14400 = 0x26,
    /// <summary>
    /// Represents the <c>BitRate16800</c> value.
    /// </summary>
    BitRate16800 = 0x27,
    /// <summary>
    /// Represents the <c>BitRate19200</c> value.
    /// </summary>
    BitRate19200 = 0x28,
    /// <summary>
    /// Represents the <c>BitRate21600</c> value.
    /// </summary>
    BitRate21600 = 0x29,
    /// <summary>
    /// Represents the <c>BitRate24000</c> value.
    /// </summary>
    BitRate24000 = 0x2A,
    /// <summary>
    /// Represents the <c>BitRate26400</c> value.
    /// </summary>
    BitRate26400 = 0x2B,
    /// <summary>
    /// Represents the <c>BitRate28800</c> value.
    /// </summary>
    BitRate28800 = 0x2C,
    /// <summary>
    /// Represents the <c>BitRate31200</c> value.
    /// </summary>
    BitRate31200 = 0x2D,
    /// <summary>
    /// Represents the <c>BitRate33600</c> value.
    /// </summary>
    BitRate33600 = 0x2E,
    /// <summary>
    /// Represents the <c>BitRate32000</c> value.
    /// </summary>
    BitRate32000 = 0x2F,
    /// <summary>
    /// Represents the <c>BitRate56000</c> value.
    /// </summary>
    BitRate56000 = 0x30,
    /// <summary>
    /// Represents the <c>BitRate64000</c> value.
    /// </summary>
    BitRate64000 = 0x31
}

/// <summary>
/// Managed equivalent of <c>v80_state_t</c>.
/// </summary>
public sealed class V80State {
    /// <summary>
    /// Initializes a new instance of the <c>V80State</c> type.
    /// </summary>
    /// <param name="callingParty">The calling party value.</param>
    public V80State(bool callingParty = false) {
        CallingParty = callingParty;
    }

    /// <summary>Gets or sets whether this endpoint is the calling party.</summary>
    public bool CallingParty { get; set; }
}

/// <summary>
/// V.80 command-description and rate-code conversion helpers.
/// </summary>
public static class V80 {
    /// <summary>
    /// Defines the <c>EscapeMarker</c> constant.
    /// </summary>
    public const int EscapeMarker = 0x19;
    /// <summary>
    /// Defines the <c>MinimumDescribedEscape</c> constant.
    /// </summary>
    public const int MinimumDescribedEscape = 0x20;
    /// <summary>
    /// Defines the <c>MaximumDescribedEscape</c> constant.
    /// </summary>
    public const int MaximumDescribedEscape = 0xC0;

    private static readonly string[] EscapeDescriptions =
    {
        "MFGExtend", "Mfg1", "Mfg2", "Mfg3",
        "Mfg4", "Mfg5", "Mfg6", "Mfg7",
        "Mfg8", "Mfg9", "Mfg10", "Mfg11",
        "Mfg12", "Mfg13", "Mfg14", "Mfg15",
        "ExtendMfg", "Mfg1", "Mfg2", "Mfg3",
        "Mfg4", "Mfg5", "Mfg6", "Mfg7",
        "Mfg8", "Mfg9", "Mfg10", "Mfg11",
        "Mfg12", "Mfg13", "Mfg14", "Mfg15",
        "Extend0", "Extend1", "Circuit 105 OFF", "Circuit 105 ON",
        "Circuit 108 OFF", "Circuit 108 ON", "Circuit 133 OFF", "Circuit 133 ON",
        "???", "???", "???", "???",
        "???", "???", "???", "???",
        "???", "???", "???", "???",
        "???", "???", "???", "???",
        "Single EM P", "Dingle EM P", "Flow OFF", "Flow ON",
        "Single EM", "Double EM", "Poll", "???",
        "Extend0", "Extend1", "Circuit 106 OFF", "Circuit 106 ON",
        "Circuit 107 OFF", "Circuit 107 ON", "Circuit 109 OFF", "Circuit 109 ON",
        "Circuit 110 OFF", "Circuit 110 ON", "Circuit 125 OFF", "Circuit 125 ON",
        "Circuit 132 OFF", "Circuit 132 ON", "Circuit 142 OFF", "Circuit 142 ON",
        "???", "???", "???", "???",
        "???", "???", "Single EM P", "Double EM P",
        "OFF line", "ON line", "Flow OFF", "Flow ON",
        "Single EM", "Double EM", "Poll", "???",
        "???", "???", "???", "???",
        "???", "???", "???", "???",
        "???", "???", "???", "???",
        "???", "???", "???", "???",
        "???", "???", "???", "???",
        "???", "???", "???", "???",
        "???", "???", "???", "???",
        "???", "???", "???", "???",
        "T3", "T4", "T7", "T8",
        "T9", "T10", "T11", "T12",
        "T13", "T14", "T15", "T16",
        "T17", "T18", "T19", "T20",
        "Mark", "Flag", "Err", "Hunt",
        "Under", "TOver", "ROver", "Resume",
        "BNum", "UNum", "EOTH", "ECS",
        "RRNH", "RTNH", "RateH", "CTL",
        "RTNC"
    };

    /// <summary>
    /// Converts an in-band escape code to the same short description used
    /// by <c>v80_escape_to_str()</c>.
    /// </summary>
    public static string EscapeToString(int escape) {
        if (escape < MinimumDescribedEscape || escape > MaximumDescribedEscape)
            return "???";

        return EscapeDescriptions[escape - MinimumDescribedEscape];
    }

    /// <summary>
    /// Performs the <c>EscapeToString</c> operation.
    /// </summary>
    /// <param name="escape">The escape value.</param>
    /// <returns>The result of the operation.</returns>
    public static string EscapeToString(V80EscapeCode escape) =>
        EscapeToString((int)escape);

    /// <summary>
    /// Converts a V.80 primary-channel rate code to bits per second.
    /// Returns -1 for an unknown code.
    /// </summary>
    /// <remarks>
    /// The supplied native files omit the 14,400 bit/s entry at code 0x26
    /// in both the public enum and lookup table. That omission shifts all
    /// later array results and makes code 0x31 read beyond the native array.
    /// This managed port restores the missing standard code and maps every
    /// explicitly named rate directly.
    /// </remarks>
    public static int BitRateCodeToBitRate(int rateCode) {
        return rateCode switch {
            0x20 => 1200,
            0x21 => 2400,
            0x22 => 4800,
            0x23 => 7200,
            0x24 => 9600,
            0x25 => 12000,
            0x26 => 14400,
            0x27 => 16800,
            0x28 => 19200,
            0x29 => 21600,
            0x2A => 24000,
            0x2B => 26400,
            0x2C => 28800,
            0x2D => 31200,
            0x2E => 33600,
            0x2F => 32000,
            0x30 => 56000,
            0x31 => 64000,
            _ => -1
        };
    }

    /// <summary>
    /// Performs the <c>BitRateCodeToBitRate</c> operation.
    /// </summary>
    /// <param name="rateCode">The rate code value.</param>
    /// <returns>The result of the operation.</returns>
    public static int BitRateCodeToBitRate(V80BitRateCode rateCode) =>
        BitRateCodeToBitRate((int)rateCode);

    /// <summary>
    /// Performs the <c>TryGetBitRate</c> operation.
    /// </summary>
    /// <param name="rateCode">The rate code value.</param>
    /// <param name="bitRate">The bit rate value.</param>
    /// <returns>The result of the operation.</returns>
    public static bool TryGetBitRate(int rateCode, out int bitRate) {
        bitRate = BitRateCodeToBitRate(rateCode);
        return bitRate >= 0;
    }
}

/// <summary>
/// Compatibility facade retaining the two public C function names.
/// </summary>
public static class V80Api {
    /// <summary>
    /// Performs the <c>v80_escape_to_str</c> operation.
    /// </summary>
    /// <param name="esc">The esc value.</param>
    /// <returns>The result of the operation.</returns>
    public static string v80_escape_to_str(int esc) =>
        V80.EscapeToString(esc);

    /// <summary>
    /// Performs the <c>v80_bit_rate_code_to_bit_rate</c> operation.
    /// </summary>
    /// <param name="rateCode">The rate code value.</param>
    /// <returns>The result of the operation.</returns>
    public static int v80_bit_rate_code_to_bit_rate(int rateCode) =>
        V80.BitRateCodeToBitRate(rateCode);
}
