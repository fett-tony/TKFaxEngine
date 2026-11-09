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
    EscapeMarker = 0x19,
    FromDteManufacturerExtended = 0x20,
    FromDteManufacturer1 = 0x21,
    FromDteManufacturer2 = 0x22,
    FromDteManufacturer3 = 0x23,
    FromDteManufacturer4 = 0x24,
    FromDteManufacturer5 = 0x25,
    FromDteManufacturer6 = 0x26,
    FromDteManufacturer7 = 0x27,
    FromDteManufacturer8 = 0x28,
    FromDteManufacturer9 = 0x29,
    FromDteManufacturer10 = 0x2A,
    FromDteManufacturer11 = 0x2B,
    FromDteManufacturer12 = 0x2C,
    FromDteManufacturer13 = 0x2D,
    FromDteManufacturer14 = 0x2E,
    FromDteManufacturer15 = 0x2F,
    FromDceManufacturerExtended = 0x30,
    FromDceManufacturer1 = 0x31,
    FromDceManufacturer2 = 0x32,
    FromDceManufacturer3 = 0x33,
    FromDceManufacturer4 = 0x34,
    FromDceManufacturer5 = 0x35,
    FromDceManufacturer6 = 0x36,
    FromDceManufacturer7 = 0x37,
    FromDceManufacturer8 = 0x38,
    FromDceManufacturer9 = 0x39,
    FromDceManufacturer10 = 0x3A,
    FromDceManufacturer11 = 0x3B,
    FromDceManufacturer12 = 0x3C,
    FromDceManufacturer13 = 0x3D,
    FromDceManufacturer14 = 0x3E,
    FromDceManufacturer15 = 0x3F,
    FromDteExtend0 = 0x40,
    FromDteExtend1 = 0x41,
    FromDteCircuit105Off = 0x42,
    FromDteCircuit105On = 0x43,
    FromDteCircuit108Off = 0x44,
    FromDteCircuit108On = 0x45,
    FromDteCircuit133Off = 0x46,
    FromDteCircuit133On = 0x47,
    FromDteSingleEmPrime = 0x58,
    FromDteDoubleEmPrime = 0x59,
    FromDteFlowOff = 0x5A,
    FromDteFlowOn = 0x5B,
    FromDteSingleEm = 0x5C,
    FromDteDoubleEm = 0x5D,
    FromDtePoll = 0x5E,
    FromDceExtend0 = 0x60,
    FromDceExtend1 = 0x61,
    FromDceCircuit106Off = 0x62,
    FromDceCircuit106On = 0x63,
    FromDceCircuit107Off = 0x64,
    FromDceCircuit107On = 0x65,
    FromDceCircuit109Off = 0x66,
    FromDceCircuit109On = 0x67,
    FromDceCircuit110Off = 0x68,
    FromDceCircuit110On = 0x69,
    FromDceCircuit125Off = 0x6A,
    FromDceCircuit125On = 0x6B,
    FromDceCircuit132Off = 0x6C,
    FromDceCircuit132On = 0x6D,
    FromDceCircuit142Off = 0x6E,
    FromDceCircuit142On = 0x6F,
    FromDceSingleEmPrime = 0x76,
    FromDceDoubleEmPrime = 0x77,
    FromDceOffLine = 0x78,
    FromDceOnLine = 0x79,
    FromDceFlowOff = 0x7A,
    FromDceFlowOn = 0x7B,
    FromDceSingleEm = 0x7C,
    FromDceDoubleEm = 0x7D,
    FromDcePoll = 0x7E,
    TransparencyT1 = 0x5C,
    TransparencyT5 = 0x5D,
    TransparencyT2 = 0x76,
    TransparencyT6 = 0x77,
    TransparencyT3 = 0xA0,
    TransparencyT4 = 0xA1,
    TransparencyT7 = 0xA2,
    TransparencyT8 = 0xA3,
    TransparencyT9 = 0xA4,
    TransparencyT10 = 0xA5,
    TransparencyT11 = 0xA6,
    TransparencyT12 = 0xA7,
    TransparencyT13 = 0xA8,
    TransparencyT14 = 0xA9,
    TransparencyT15 = 0xAA,
    TransparencyT16 = 0xAB,
    TransparencyT17 = 0xAC,
    TransparencyT18 = 0xAD,
    TransparencyT19 = 0xAE,
    TransparencyT20 = 0xAF,
    Mark = 0xB0,
    Flag = 0xB1,
    Error = 0xB2,
    Hunt = 0xB3,
    Underflow = 0xB4,
    TransmitOverrun = 0xB5,
    ReceiveOverrun = 0xB6,
    Resume = 0xB7,
    BufferOctetCount = 0xB8,
    DiscardedOctetCount = 0xB9,
    EndOfTransmission = 0xBA,
    EndOfTransmissionHalfDuplex = 0xBA,
    EscapeToCommandState = 0xBB,
    RequestRateRenegotiation = 0xBC,
    PrimaryChannel = 0xBC,
    RequestRateRetrain = 0xBD,
    PrimaryChannelRetrain = 0xBD,
    Rate = 0xBE,
    HalfDuplexRate = 0xBE,
    ControlChannel = 0xBF,
    ControlChannelRetrain = 0xC0
}

/// <summary>
/// Primary-channel data signalling rate codes used by V.80.
/// </summary>
public enum V80BitRateCode {
    BitRate1200 = 0x20,
    BitRate2400 = 0x21,
    BitRate4800 = 0x22,
    BitRate7200 = 0x23,
    BitRate9600 = 0x24,
    BitRate12000 = 0x25,
    BitRate14400 = 0x26,
    BitRate16800 = 0x27,
    BitRate19200 = 0x28,
    BitRate21600 = 0x29,
    BitRate24000 = 0x2A,
    BitRate26400 = 0x2B,
    BitRate28800 = 0x2C,
    BitRate31200 = 0x2D,
    BitRate33600 = 0x2E,
    BitRate32000 = 0x2F,
    BitRate56000 = 0x30,
    BitRate64000 = 0x31
}

/// <summary>
/// Managed equivalent of <c>v80_state_t</c>.
/// </summary>
public sealed class V80State {
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
    public const int EscapeMarker = 0x19;
    public const int MinimumDescribedEscape = 0x20;
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

    public static int BitRateCodeToBitRate(V80BitRateCode rateCode) =>
        BitRateCodeToBitRate((int)rateCode);

    public static bool TryGetBitRate(int rateCode, out int bitRate) {
        bitRate = BitRateCodeToBitRate(rateCode);
        return bitRate >= 0;
    }
}

/// <summary>
/// Compatibility facade retaining the two public C function names.
/// </summary>
public static class V80Api {
    public static string v80_escape_to_str(int esc) =>
        V80.EscapeToString(esc);

    public static int v80_bit_rate_code_to_bit_rate(int rateCode) =>
        V80.BitRateCodeToBitRate(rateCode);
}
