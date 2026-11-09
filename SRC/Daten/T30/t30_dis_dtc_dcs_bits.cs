/*
 * TKFaxEngineFX - managed C# port
 *
 * C# conversion of t30_dis_dtc_dcs_bits.h.
 */

namespace TKFaxEngine.Daten.T30;

/// <summary>
/// Represents the <c>T30DisBits</c> type.
/// </summary>
public static class T30DisBits {
    /// <summary>
    /// Defines the <c>Bit1</c> constant.
    /// </summary>
    public const byte Bit1 = 0x01;
    /// <summary>
    /// Defines the <c>Bit2</c> constant.
    /// </summary>
    public const byte Bit2 = 0x02;
    /// <summary>
    /// Defines the <c>Bit3</c> constant.
    /// </summary>
    public const byte Bit3 = 0x04;
    /// <summary>
    /// Defines the <c>Bit4</c> constant.
    /// </summary>
    public const byte Bit4 = 0x08;
    /// <summary>
    /// Defines the <c>Bit5</c> constant.
    /// </summary>
    public const byte Bit5 = 0x10;
    /// <summary>
    /// Defines the <c>Bit6</c> constant.
    /// </summary>
    public const byte Bit6 = 0x20;
    /// <summary>
    /// Defines the <c>Bit7</c> constant.
    /// </summary>
    public const byte Bit7 = 0x40;
    /// <summary>
    /// Defines the <c>Bit8</c> constant.
    /// </summary>
    public const byte Bit8 = 0x80;
}

/// <summary>
/// Represents the <c>T30ControlBit</c> type.
/// </summary>
public static class T30ControlBit {
    /// <summary>
    /// Defines the <c>DisT37</c> constant.
    /// </summary>
    public const int DisT37 = 1;
    /// <summary>
    /// Defines the <c>DcsT37</c> constant.
    /// </summary>
    public const int DcsT37 = 1;
    /// <summary>
    /// Defines the <c>DisT38</c> constant.
    /// </summary>
    public const int DisT38 = 3;
    /// <summary>
    /// Defines the <c>DcsT38</c> constant.
    /// </summary>
    public const int DcsT38 = 3;
    /// <summary>
    /// Defines the <c>Dis3gMobile</c> constant.
    /// </summary>
    public const int Dis3gMobile = 4;
    /// <summary>
    /// Defines the <c>Dcs3gMobile</c> constant.
    /// </summary>
    public const int Dcs3gMobile = 4;
    /// <summary>
    /// Defines the <c>DisV8Capability</c> constant.
    /// </summary>
    public const int DisV8Capability = 6;
    /// <summary>
    /// Defines the <c>Dis64OctetEcmFramesPreferred</c> constant.
    /// </summary>
    public const int Dis64OctetEcmFramesPreferred = 7;
    /// <summary>
    /// Defines the <c>DisReadyToTransmitFaxDocument</c> constant.
    /// </summary>
    public const int DisReadyToTransmitFaxDocument = 9;
    /// <summary>
    /// Defines the <c>DisReadyToReceiveFaxDocument</c> constant.
    /// </summary>
    public const int DisReadyToReceiveFaxDocument = 10;
    /// <summary>
    /// Defines the <c>DcsReceiveFaxDocument</c> constant.
    /// </summary>
    public const int DcsReceiveFaxDocument = 10;
    /// <summary>
    /// Defines the <c>DisModemType1</c> constant.
    /// </summary>
    public const int DisModemType1 = 11;
    /// <summary>
    /// Defines the <c>DcsModemType1</c> constant.
    /// </summary>
    public const int DcsModemType1 = 11;
    /// <summary>
    /// Defines the <c>DisModemType2</c> constant.
    /// </summary>
    public const int DisModemType2 = 12;
    /// <summary>
    /// Defines the <c>DcsModemType2</c> constant.
    /// </summary>
    public const int DcsModemType2 = 12;
    /// <summary>
    /// Defines the <c>DisModemType3</c> constant.
    /// </summary>
    public const int DisModemType3 = 13;
    /// <summary>
    /// Defines the <c>DcsModemType3</c> constant.
    /// </summary>
    public const int DcsModemType3 = 13;
    /// <summary>
    /// Defines the <c>DisModemType4</c> constant.
    /// </summary>
    public const int DisModemType4 = 14;
    /// <summary>
    /// Defines the <c>DcsModemType4</c> constant.
    /// </summary>
    public const int DcsModemType4 = 14;
    /// <summary>
    /// Defines the <c>Dis200200Capable</c> constant.
    /// </summary>
    public const int Dis200200Capable = 15;
    /// <summary>
    /// Defines the <c>Dcs200200</c> constant.
    /// </summary>
    public const int Dcs200200 = 15;
    /// <summary>
    /// Defines the <c>Dis2dCapable</c> constant.
    /// </summary>
    public const int Dis2dCapable = 16;
    /// <summary>
    /// Defines the <c>Dcs2dMode</c> constant.
    /// </summary>
    public const int Dcs2dMode = 16;
    /// <summary>
    /// Defines the <c>Dis215mm255mmWidthCapable</c> constant.
    /// </summary>
    public const int Dis215mm255mmWidthCapable = 17;
    /// <summary>
    /// Defines the <c>Dcs255mmWidth</c> constant.
    /// </summary>
    public const int Dcs255mmWidth = 17;
    /// <summary>
    /// Defines the <c>Dis215mm255mm303mmWidthCapable</c> constant.
    /// </summary>
    public const int Dis215mm255mm303mmWidthCapable = 18;
    /// <summary>
    /// Defines the <c>Dcs303mmWidth</c> constant.
    /// </summary>
    public const int Dcs303mmWidth = 18;
    /// <summary>
    /// Defines the <c>DisA4B4LengthCapable</c> constant.
    /// </summary>
    public const int DisA4B4LengthCapable = 19;
    /// <summary>
    /// Defines the <c>DcsB4Length</c> constant.
    /// </summary>
    public const int DcsB4Length = 19;
    /// <summary>
    /// Defines the <c>DisUnlimitedLengthCapable</c> constant.
    /// </summary>
    public const int DisUnlimitedLengthCapable = 20;
    /// <summary>
    /// Defines the <c>DcsUnlimitedLength</c> constant.
    /// </summary>
    public const int DcsUnlimitedLength = 20;
    /// <summary>
    /// Defines the <c>DisMinScanLineTimeCapability1</c> constant.
    /// </summary>
    public const int DisMinScanLineTimeCapability1 = 21;
    /// <summary>
    /// Defines the <c>DcsMinScanLineTime1</c> constant.
    /// </summary>
    public const int DcsMinScanLineTime1 = 21;
    /// <summary>
    /// Defines the <c>DisMinScanLineTimeCapability2</c> constant.
    /// </summary>
    public const int DisMinScanLineTimeCapability2 = 22;
    /// <summary>
    /// Defines the <c>DcsMinScanLineTime2</c> constant.
    /// </summary>
    public const int DcsMinScanLineTime2 = 22;
    /// <summary>
    /// Defines the <c>DisMinScanLineTimeCapability3</c> constant.
    /// </summary>
    public const int DisMinScanLineTimeCapability3 = 23;
    /// <summary>
    /// Defines the <c>DcsMinScanLineTime3</c> constant.
    /// </summary>
    public const int DcsMinScanLineTime3 = 23;
    /// <summary>
    /// Defines the <c>DisMinScanLineTimeCapability4</c> constant.
    /// </summary>
    public const int DisMinScanLineTimeCapability4 = 24;
    /// <summary>
    /// Defines the <c>DcsMinScanLineTime4</c> constant.
    /// </summary>
    public const int DcsMinScanLineTime4 = 24;
    /// <summary>
    /// Defines the <c>DisUncompressedCapable</c> constant.
    /// </summary>
    public const int DisUncompressedCapable = 26;
    /// <summary>
    /// Defines the <c>DcsUncompressedMode</c> constant.
    /// </summary>
    public const int DcsUncompressedMode = 26;
    /// <summary>
    /// Defines the <c>DisEcmCapable</c> constant.
    /// </summary>
    public const int DisEcmCapable = 27;
    /// <summary>
    /// Defines the <c>DcsEcmMode</c> constant.
    /// </summary>
    public const int DcsEcmMode = 27;
    /// <summary>
    /// Defines the <c>Dcs64OctetEcmFrames</c> constant.
    /// </summary>
    public const int Dcs64OctetEcmFrames = 28;
    /// <summary>
    /// Defines the <c>DisT6Capable</c> constant.
    /// </summary>
    public const int DisT6Capable = 31;
    /// <summary>
    /// Defines the <c>DcsT6Mode</c> constant.
    /// </summary>
    public const int DcsT6Mode = 31;
    /// <summary>
    /// Defines the <c>DisFnvCapable</c> constant.
    /// </summary>
    public const int DisFnvCapable = 33;
    /// <summary>
    /// Defines the <c>DcsFnvCapable</c> constant.
    /// </summary>
    public const int DcsFnvCapable = 33;
    /// <summary>
    /// Defines the <c>DisMultipleSelectivePollingCapable</c> constant.
    /// </summary>
    public const int DisMultipleSelectivePollingCapable = 34;
    /// <summary>
    /// Defines the <c>DisPolledSubaddressingCapable</c> constant.
    /// </summary>
    public const int DisPolledSubaddressingCapable = 35;
    /// <summary>
    /// Defines the <c>DisT43Capable</c> constant.
    /// </summary>
    public const int DisT43Capable = 36;
    /// <summary>
    /// Defines the <c>DcsT43Mode</c> constant.
    /// </summary>
    public const int DcsT43Mode = 36;
    /// <summary>
    /// Defines the <c>DisPlaneInterleaveCapable</c> constant.
    /// </summary>
    public const int DisPlaneInterleaveCapable = 37;
    /// <summary>
    /// Defines the <c>DcsPlaneInterleave</c> constant.
    /// </summary>
    public const int DcsPlaneInterleave = 37;
    /// <summary>
    /// Defines the <c>DisG726Capable</c> constant.
    /// </summary>
    public const int DisG726Capable = 38;
    /// <summary>
    /// Defines the <c>DcsG726</c> constant.
    /// </summary>
    public const int DcsG726 = 38;
    /// <summary>
    /// Defines the <c>Dis200400Capable</c> constant.
    /// </summary>
    public const int Dis200400Capable = 41;
    /// <summary>
    /// Defines the <c>Dcs200400</c> constant.
    /// </summary>
    public const int Dcs200400 = 41;
    /// <summary>
    /// Defines the <c>Dis300300Capable</c> constant.
    /// </summary>
    public const int Dis300300Capable = 42;
    /// <summary>
    /// Defines the <c>Dcs300300</c> constant.
    /// </summary>
    public const int Dcs300300 = 42;
    /// <summary>
    /// Defines the <c>Dis400400Capable</c> constant.
    /// </summary>
    public const int Dis400400Capable = 43;
    /// <summary>
    /// Defines the <c>Dcs400400</c> constant.
    /// </summary>
    public const int Dcs400400 = 43;
    /// <summary>
    /// Defines the <c>DisInchResolutionPreferred</c> constant.
    /// </summary>
    public const int DisInchResolutionPreferred = 44;
    /// <summary>
    /// Defines the <c>DcsInchResolution</c> constant.
    /// </summary>
    public const int DcsInchResolution = 44;
    /// <summary>
    /// Defines the <c>DisMetricResolutionPreferred</c> constant.
    /// </summary>
    public const int DisMetricResolutionPreferred = 45;
    /// <summary>
    /// Defines the <c>DisMinScanTimeHalves</c> constant.
    /// </summary>
    public const int DisMinScanTimeHalves = 46;
    /// <summary>
    /// Defines the <c>DisSelectivePollingCapable</c> constant.
    /// </summary>
    public const int DisSelectivePollingCapable = 47;
    /// <summary>
    /// Defines the <c>DisSubaddressingCapable</c> constant.
    /// </summary>
    public const int DisSubaddressingCapable = 49;
    /// <summary>
    /// Defines the <c>DcsSubaddressTransmission</c> constant.
    /// </summary>
    public const int DcsSubaddressTransmission = 49;
    /// <summary>
    /// Defines the <c>DisPassword</c> constant.
    /// </summary>
    public const int DisPassword = 50;
    /// <summary>
    /// Defines the <c>DcsSenderIdTransmission</c> constant.
    /// </summary>
    public const int DcsSenderIdTransmission = 50;
    /// <summary>
    /// Defines the <c>DisReadyToTransmitDataFile</c> constant.
    /// </summary>
    public const int DisReadyToTransmitDataFile = 51;
    /// <summary>
    /// Defines the <c>DisBftCapable</c> constant.
    /// </summary>
    public const int DisBftCapable = 53;
    /// <summary>
    /// Defines the <c>DcsBft</c> constant.
    /// </summary>
    public const int DcsBft = 53;
    /// <summary>
    /// Defines the <c>DisDtmCapable</c> constant.
    /// </summary>
    public const int DisDtmCapable = 54;
    /// <summary>
    /// Defines the <c>DcsDtm</c> constant.
    /// </summary>
    public const int DcsDtm = 54;
    /// <summary>
    /// Defines the <c>DisEdiCapable</c> constant.
    /// </summary>
    public const int DisEdiCapable = 55;
    /// <summary>
    /// Defines the <c>DcsEdi</c> constant.
    /// </summary>
    public const int DcsEdi = 55;
    /// <summary>
    /// Defines the <c>DisBtmCapable</c> constant.
    /// </summary>
    public const int DisBtmCapable = 57;
    /// <summary>
    /// Defines the <c>DcsBtm</c> constant.
    /// </summary>
    public const int DcsBtm = 57;
    /// <summary>
    /// Defines the <c>DisReadyToTransmitMixedModeDocument</c> constant.
    /// </summary>
    public const int DisReadyToTransmitMixedModeDocument = 59;
    /// <summary>
    /// Defines the <c>DisCharacterModeCapable</c> constant.
    /// </summary>
    public const int DisCharacterModeCapable = 60;
    /// <summary>
    /// Defines the <c>DcsCharacterMode</c> constant.
    /// </summary>
    public const int DcsCharacterMode = 60;
    /// <summary>
    /// Defines the <c>DisMixedMode</c> constant.
    /// </summary>
    public const int DisMixedMode = 62;
    /// <summary>
    /// Defines the <c>DcsMixedMode</c> constant.
    /// </summary>
    public const int DcsMixedMode = 62;
    /// <summary>
    /// Defines the <c>DisProcessableMode26</c> constant.
    /// </summary>
    public const int DisProcessableMode26 = 65;
    /// <summary>
    /// Defines the <c>DisDigitalNetworkCapable</c> constant.
    /// </summary>
    public const int DisDigitalNetworkCapable = 66;
    /// <summary>
    /// Defines the <c>DcsDigitalNetworkCapable</c> constant.
    /// </summary>
    public const int DcsDigitalNetworkCapable = 66;
    /// <summary>
    /// Defines the <c>DisDuplexCapable</c> constant.
    /// </summary>
    public const int DisDuplexCapable = 67;
    /// <summary>
    /// Defines the <c>DcsDuplexCapable</c> constant.
    /// </summary>
    public const int DcsDuplexCapable = 67;
    /// <summary>
    /// Defines the <c>DisT81Capable</c> constant.
    /// </summary>
    public const int DisT81Capable = 68;
    /// <summary>
    /// Defines the <c>DcsT81Mode</c> constant.
    /// </summary>
    public const int DcsT81Mode = 68;
    /// <summary>
    /// Defines the <c>DisFullColourCapable</c> constant.
    /// </summary>
    public const int DisFullColourCapable = 69;
    /// <summary>
    /// Defines the <c>DcsFullColourMode</c> constant.
    /// </summary>
    public const int DcsFullColourMode = 69;
    /// <summary>
    /// Defines the <c>DcsPreferredHuffmanTables</c> constant.
    /// </summary>
    public const int DcsPreferredHuffmanTables = 70;
    /// <summary>
    /// Defines the <c>Dis12bitCapable</c> constant.
    /// </summary>
    public const int Dis12bitCapable = 71;
    /// <summary>
    /// Defines the <c>Dcs12bitComponent</c> constant.
    /// </summary>
    public const int Dcs12bitComponent = 71;
    /// <summary>
    /// Defines the <c>DisNoSubsampling</c> constant.
    /// </summary>
    public const int DisNoSubsampling = 73;
    /// <summary>
    /// Defines the <c>DcsNoSubsampling</c> constant.
    /// </summary>
    public const int DcsNoSubsampling = 73;
    /// <summary>
    /// Defines the <c>DisCustomIlluminant</c> constant.
    /// </summary>
    public const int DisCustomIlluminant = 74;
    /// <summary>
    /// Defines the <c>DcsCustomIlluminant</c> constant.
    /// </summary>
    public const int DcsCustomIlluminant = 74;
    /// <summary>
    /// Defines the <c>DisCustomGamutRange</c> constant.
    /// </summary>
    public const int DisCustomGamutRange = 75;
    /// <summary>
    /// Defines the <c>DcsCustomGamutRange</c> constant.
    /// </summary>
    public const int DcsCustomGamutRange = 75;
    /// <summary>
    /// Defines the <c>DisNorthAmericanLetterCapable</c> constant.
    /// </summary>
    public const int DisNorthAmericanLetterCapable = 76;
    /// <summary>
    /// Defines the <c>DcsNorthAmericanLetter</c> constant.
    /// </summary>
    public const int DcsNorthAmericanLetter = 76;
    /// <summary>
    /// Defines the <c>DisNorthAmericanLegalCapable</c> constant.
    /// </summary>
    public const int DisNorthAmericanLegalCapable = 77;
    /// <summary>
    /// Defines the <c>DcsNorthAmericanLegal</c> constant.
    /// </summary>
    public const int DcsNorthAmericanLegal = 77;
    /// <summary>
    /// Defines the <c>DisT85Capable</c> constant.
    /// </summary>
    public const int DisT85Capable = 78;
    /// <summary>
    /// Defines the <c>DcsT85Mode</c> constant.
    /// </summary>
    public const int DcsT85Mode = 78;
    /// <summary>
    /// Defines the <c>DisT85L0Capable</c> constant.
    /// </summary>
    public const int DisT85L0Capable = 79;
    /// <summary>
    /// Defines the <c>DcsT85L0Mode</c> constant.
    /// </summary>
    public const int DcsT85L0Mode = 79;
    /// <summary>
    /// Defines the <c>DisHkmKeyManagementCapable</c> constant.
    /// </summary>
    public const int DisHkmKeyManagementCapable = 81;
    /// <summary>
    /// Defines the <c>DcsHkmKeyManagementMode</c> constant.
    /// </summary>
    public const int DcsHkmKeyManagementMode = 81;
    /// <summary>
    /// Defines the <c>DisRsaKeyManagementCapable</c> constant.
    /// </summary>
    public const int DisRsaKeyManagementCapable = 82;
    /// <summary>
    /// Defines the <c>DcsRsaKeyManagementMode</c> constant.
    /// </summary>
    public const int DcsRsaKeyManagementMode = 82;
    /// <summary>
    /// Defines the <c>DisOverrideCapable</c> constant.
    /// </summary>
    public const int DisOverrideCapable = 83;
    /// <summary>
    /// Defines the <c>DcsOverrideMode</c> constant.
    /// </summary>
    public const int DcsOverrideMode = 83;
    /// <summary>
    /// Defines the <c>DisHfx40CipherCapable</c> constant.
    /// </summary>
    public const int DisHfx40CipherCapable = 84;
    /// <summary>
    /// Defines the <c>DcsHfx40CipherMode</c> constant.
    /// </summary>
    public const int DcsHfx40CipherMode = 84;
    /// <summary>
    /// Defines the <c>DisAlternativeCipher2Capable</c> constant.
    /// </summary>
    public const int DisAlternativeCipher2Capable = 85;
    /// <summary>
    /// Defines the <c>DcsAlternativeCipher2Mode</c> constant.
    /// </summary>
    public const int DcsAlternativeCipher2Mode = 85;
    /// <summary>
    /// Defines the <c>DisAlternativeCipher3Capable</c> constant.
    /// </summary>
    public const int DisAlternativeCipher3Capable = 86;
    /// <summary>
    /// Defines the <c>DcsAlternativeCipher3Mode</c> constant.
    /// </summary>
    public const int DcsAlternativeCipher3Mode = 86;
    /// <summary>
    /// Defines the <c>DisHfx40IHashingCapable</c> constant.
    /// </summary>
    public const int DisHfx40IHashingCapable = 87;
    /// <summary>
    /// Defines the <c>DcsHfx40IHashingMode</c> constant.
    /// </summary>
    public const int DcsHfx40IHashingMode = 87;
    /// <summary>
    /// Defines the <c>DisAlternativeHashing2Capable</c> constant.
    /// </summary>
    public const int DisAlternativeHashing2Capable = 89;
    /// <summary>
    /// Defines the <c>DcsAlternativeHashing2Mode</c> constant.
    /// </summary>
    public const int DcsAlternativeHashing2Mode = 89;
    /// <summary>
    /// Defines the <c>DisAlternativeHashing3Capable</c> constant.
    /// </summary>
    public const int DisAlternativeHashing3Capable = 90;
    /// <summary>
    /// Defines the <c>DcsAlternativeHashing3Mode</c> constant.
    /// </summary>
    public const int DcsAlternativeHashing3Mode = 90;
    /// <summary>
    /// Defines the <c>DisT44PageLength</c> constant.
    /// </summary>
    public const int DisT44PageLength = 95;
    /// <summary>
    /// Defines the <c>DcsT44PageLength</c> constant.
    /// </summary>
    public const int DcsT44PageLength = 95;
    /// <summary>
    /// Defines the <c>DisColourGray300300400400Capable</c> constant.
    /// </summary>
    public const int DisColourGray300300400400Capable = 97;
    /// <summary>
    /// Defines the <c>DcsColourGray300300400400</c> constant.
    /// </summary>
    public const int DcsColourGray300300400400 = 97;
    /// <summary>
    /// Defines the <c>DisColourGray100100Capable</c> constant.
    /// </summary>
    public const int DisColourGray100100Capable = 98;
    /// <summary>
    /// Defines the <c>DcsColourGray100100</c> constant.
    /// </summary>
    public const int DcsColourGray100100 = 98;
    /// <summary>
    /// Defines the <c>DisSimplePhaseCBftNegotiationsCapable</c> constant.
    /// </summary>
    public const int DisSimplePhaseCBftNegotiationsCapable = 99;
    /// <summary>
    /// Defines the <c>DcsSimplePhaseCBftNegotiationsCapable</c> constant.
    /// </summary>
    public const int DcsSimplePhaseCBftNegotiationsCapable = 99;
    /// <summary>
    /// Defines the <c>DisExtendedBftNegotiationsCapable</c> constant.
    /// </summary>
    public const int DisExtendedBftNegotiationsCapable = 100;
    /// <summary>
    /// Defines the <c>DisInternetSelectivePollingAddress</c> constant.
    /// </summary>
    public const int DisInternetSelectivePollingAddress = 101;
    /// <summary>
    /// Defines the <c>DisInternetRoutingAddress</c> constant.
    /// </summary>
    public const int DisInternetRoutingAddress = 102;
    /// <summary>
    /// Defines the <c>DcsInternetRoutingAddressTransmission</c> constant.
    /// </summary>
    public const int DcsInternetRoutingAddressTransmission = 102;
    /// <summary>
    /// Defines the <c>Dis600600Capable</c> constant.
    /// </summary>
    public const int Dis600600Capable = 105;
    /// <summary>
    /// Defines the <c>Dcs600600</c> constant.
    /// </summary>
    public const int Dcs600600 = 105;
    /// <summary>
    /// Defines the <c>Dis12001200Capable</c> constant.
    /// </summary>
    public const int Dis12001200Capable = 106;
    /// <summary>
    /// Defines the <c>Dcs12001200</c> constant.
    /// </summary>
    public const int Dcs12001200 = 106;
    /// <summary>
    /// Defines the <c>Dis300600Capable</c> constant.
    /// </summary>
    public const int Dis300600Capable = 107;
    /// <summary>
    /// Defines the <c>Dcs300600</c> constant.
    /// </summary>
    public const int Dcs300600 = 107;
    /// <summary>
    /// Defines the <c>Dis400800Capable</c> constant.
    /// </summary>
    public const int Dis400800Capable = 108;
    /// <summary>
    /// Defines the <c>Dcs400800</c> constant.
    /// </summary>
    public const int Dcs400800 = 108;
    /// <summary>
    /// Defines the <c>Dis6001200Capable</c> constant.
    /// </summary>
    public const int Dis6001200Capable = 109;
    /// <summary>
    /// Defines the <c>Dcs6001200</c> constant.
    /// </summary>
    public const int Dcs6001200 = 109;
    /// <summary>
    /// Defines the <c>DisColourGray600600Capable</c> constant.
    /// </summary>
    public const int DisColourGray600600Capable = 110;
    /// <summary>
    /// Defines the <c>DcsColourGray600600</c> constant.
    /// </summary>
    public const int DcsColourGray600600 = 110;
    /// <summary>
    /// Defines the <c>DisColourGray12001200Capable</c> constant.
    /// </summary>
    public const int DisColourGray12001200Capable = 111;
    /// <summary>
    /// Defines the <c>DcsColourGray12001200</c> constant.
    /// </summary>
    public const int DcsColourGray12001200 = 111;
    /// <summary>
    /// Defines the <c>DisAlternateDoubleSidedCapable</c> constant.
    /// </summary>
    public const int DisAlternateDoubleSidedCapable = 113;
    /// <summary>
    /// Defines the <c>DcsAlternateDoubleSidedCapable</c> constant.
    /// </summary>
    public const int DcsAlternateDoubleSidedCapable = 113;
    /// <summary>
    /// Defines the <c>DisContinuousDoubleSidedCapable</c> constant.
    /// </summary>
    public const int DisContinuousDoubleSidedCapable = 114;
    /// <summary>
    /// Defines the <c>DcsContinuousDoubleSidedCapable</c> constant.
    /// </summary>
    public const int DcsContinuousDoubleSidedCapable = 114;
    /// <summary>
    /// Defines the <c>DisBlackAndWhiteMrc</c> constant.
    /// </summary>
    public const int DisBlackAndWhiteMrc = 115;
    /// <summary>
    /// Defines the <c>DisT45Capable</c> constant.
    /// </summary>
    public const int DisT45Capable = 116;
    /// <summary>
    /// Defines the <c>DcsT45Mode</c> constant.
    /// </summary>
    public const int DcsT45Mode = 116;
    /// <summary>
    /// Defines the <c>DisT44ColourSpace</c> constant.
    /// </summary>
    public const int DisT44ColourSpace = 119;
    /// <summary>
    /// Defines the <c>DcsT44ColourSpace</c> constant.
    /// </summary>
    public const int DcsT44ColourSpace = 119;
    /// <summary>
    /// Defines the <c>DisT38FlowControlCapable</c> constant.
    /// </summary>
    public const int DisT38FlowControlCapable = 121;
    /// <summary>
    /// Defines the <c>DcsT38FlowControlCapable</c> constant.
    /// </summary>
    public const int DcsT38FlowControlCapable = 121;
    /// <summary>
    /// Defines the <c>DisKGreaterThan4</c> constant.
    /// </summary>
    public const int DisKGreaterThan4 = 122;
    /// <summary>
    /// Defines the <c>DisT38FaxCapable</c> constant.
    /// </summary>
    public const int DisT38FaxCapable = 123;
    /// <summary>
    /// Defines the <c>DcsT38FaxMode</c> constant.
    /// </summary>
    public const int DcsT38FaxMode = 123;
    /// <summary>
    /// Defines the <c>DisT88Capability1</c> constant.
    /// </summary>
    public const int DisT88Capability1 = 124;
    /// <summary>
    /// Defines the <c>DcsT88Mode1</c> constant.
    /// </summary>
    public const int DcsT88Mode1 = 124;
    /// <summary>
    /// Defines the <c>DisT88Capability2</c> constant.
    /// </summary>
    public const int DisT88Capability2 = 125;
    /// <summary>
    /// Defines the <c>DcsT88Mode2</c> constant.
    /// </summary>
    public const int DcsT88Mode2 = 125;
    /// <summary>
    /// Defines the <c>DisT88Capability3</c> constant.
    /// </summary>
    public const int DisT88Capability3 = 126;
    /// <summary>
    /// Defines the <c>DcsT88Mode3</c> constant.
    /// </summary>
    public const int DcsT88Mode3 = 126;
    /// <summary>
    /// Defines the <c>DisSyccT81Capable</c> constant.
    /// </summary>
    public const int DisSyccT81Capable = 127;
    /// <summary>
    /// Defines the <c>DcsSyccT81Mode</c> constant.
    /// </summary>
    public const int DcsSyccT81Mode = 127;
}

/// <summary>
/// Represents the <c>t30_dis_dtc_dcs_bits</c> type.
/// </summary>
public static class t30_dis_dtc_dcs_bits {
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_T37</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_T37 = 1;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_T37</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_T37 = 1;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_T38</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_T38 = 3;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_T38</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_T38 = 3;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_3G_MOBILE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_3G_MOBILE = 4;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_3G_MOBILE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_3G_MOBILE = 4;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_V8_CAPABILITY</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_V8_CAPABILITY = 6;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_64_OCTET_ECM_FRAMES_PREFERRED</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_64_OCTET_ECM_FRAMES_PREFERRED = 7;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_READY_TO_TRANSMIT_FAX_DOCUMENT</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_READY_TO_TRANSMIT_FAX_DOCUMENT = 9;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_READY_TO_RECEIVE_FAX_DOCUMENT</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_READY_TO_RECEIVE_FAX_DOCUMENT = 10;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_RECEIVE_FAX_DOCUMENT</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_RECEIVE_FAX_DOCUMENT = 10;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_MODEM_TYPE_1</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_MODEM_TYPE_1 = 11;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_MODEM_TYPE_1</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_MODEM_TYPE_1 = 11;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_MODEM_TYPE_2</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_MODEM_TYPE_2 = 12;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_MODEM_TYPE_2</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_MODEM_TYPE_2 = 12;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_MODEM_TYPE_3</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_MODEM_TYPE_3 = 13;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_MODEM_TYPE_3</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_MODEM_TYPE_3 = 13;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_MODEM_TYPE_4</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_MODEM_TYPE_4 = 14;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_MODEM_TYPE_4</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_MODEM_TYPE_4 = 14;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_200_200_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_200_200_CAPABLE = 15;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_200_200</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_200_200 = 15;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_2D_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_2D_CAPABLE = 16;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_2D_MODE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_2D_MODE = 16;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_215MM_255MM_WIDTH_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_215MM_255MM_WIDTH_CAPABLE = 17;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_255MM_WIDTH</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_255MM_WIDTH = 17;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_215MM_255MM_303MM_WIDTH_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_215MM_255MM_303MM_WIDTH_CAPABLE = 18;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_303MM_WIDTH</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_303MM_WIDTH = 18;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_A4_B4_LENGTH_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_A4_B4_LENGTH_CAPABLE = 19;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_B4_LENGTH</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_B4_LENGTH = 19;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_UNLIMITED_LENGTH_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_UNLIMITED_LENGTH_CAPABLE = 20;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_UNLIMITED_LENGTH</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_UNLIMITED_LENGTH = 20;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_MIN_SCAN_LINE_TIME_CAPABILITY_1</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_MIN_SCAN_LINE_TIME_CAPABILITY_1 = 21;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_MIN_SCAN_LINE_TIME_1</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_MIN_SCAN_LINE_TIME_1 = 21;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_MIN_SCAN_LINE_TIME_CAPABILITY_2</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_MIN_SCAN_LINE_TIME_CAPABILITY_2 = 22;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_MIN_SCAN_LINE_TIME_2</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_MIN_SCAN_LINE_TIME_2 = 22;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_MIN_SCAN_LINE_TIME_CAPABILITY_3</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_MIN_SCAN_LINE_TIME_CAPABILITY_3 = 23;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_MIN_SCAN_LINE_TIME_3</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_MIN_SCAN_LINE_TIME_3 = 23;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_MIN_SCAN_LINE_TIME_CAPABILITY_4</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_MIN_SCAN_LINE_TIME_CAPABILITY_4 = 24;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_MIN_SCAN_LINE_TIME_4</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_MIN_SCAN_LINE_TIME_4 = 24;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_UNCOMPRESSED_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_UNCOMPRESSED_CAPABLE = 26;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_UNCOMPRESSED_MODE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_UNCOMPRESSED_MODE = 26;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_ECM_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_ECM_CAPABLE = 27;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_ECM_MODE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_ECM_MODE = 27;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_64_OCTET_ECM_FRAMES</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_64_OCTET_ECM_FRAMES = 28;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_T6_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_T6_CAPABLE = 31;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_T6_MODE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_T6_MODE = 31;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_FNV_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_FNV_CAPABLE = 33;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_FNV_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_FNV_CAPABLE = 33;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_MULTIPLE_SELECTIVE_POLLING_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_MULTIPLE_SELECTIVE_POLLING_CAPABLE = 34;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_POLLED_SUBADDRESSING_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_POLLED_SUBADDRESSING_CAPABLE = 35;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_T43_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_T43_CAPABLE = 36;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_T43_MODE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_T43_MODE = 36;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_PLANE_INTERLEAVE_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_PLANE_INTERLEAVE_CAPABLE = 37;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_PLANE_INTERLEAVE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_PLANE_INTERLEAVE = 37;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_G726_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_G726_CAPABLE = 38;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_G726</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_G726 = 38;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_200_400_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_200_400_CAPABLE = 41;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_200_400</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_200_400 = 41;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_300_300_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_300_300_CAPABLE = 42;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_300_300</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_300_300 = 42;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_400_400_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_400_400_CAPABLE = 43;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_400_400</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_400_400 = 43;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_INCH_RESOLUTION_PREFERRED</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_INCH_RESOLUTION_PREFERRED = 44;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_INCH_RESOLUTION</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_INCH_RESOLUTION = 44;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_METRIC_RESOLUTION_PREFERRED</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_METRIC_RESOLUTION_PREFERRED = 45;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_MIN_SCAN_TIME_HALVES</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_MIN_SCAN_TIME_HALVES = 46;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_SELECTIVE_POLLING_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_SELECTIVE_POLLING_CAPABLE = 47;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_SUBADDRESSING_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_SUBADDRESSING_CAPABLE = 49;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_SUBADDRESS_TRANSMISSION</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_SUBADDRESS_TRANSMISSION = 49;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_PASSWORD</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_PASSWORD = 50;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_SENDER_ID_TRANSMISSION</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_SENDER_ID_TRANSMISSION = 50;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_READY_TO_TRANSMIT_DATA_FILE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_READY_TO_TRANSMIT_DATA_FILE = 51;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_BFT_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_BFT_CAPABLE = 53;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_BFT</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_BFT = 53;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_DTM_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_DTM_CAPABLE = 54;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_DTM</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_DTM = 54;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_EDI_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_EDI_CAPABLE = 55;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_EDI</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_EDI = 55;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_BTM_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_BTM_CAPABLE = 57;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_BTM</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_BTM = 57;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_READY_TO_TRANSMIT_MIXED_MODE_DOCUMENT</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_READY_TO_TRANSMIT_MIXED_MODE_DOCUMENT = 59;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_CHARACTER_MODE_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_CHARACTER_MODE_CAPABLE = 60;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_CHARACTER_MODE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_CHARACTER_MODE = 60;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_MIXED_MODE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_MIXED_MODE = 62;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_MIXED_MODE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_MIXED_MODE = 62;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_PROCESSABLE_MODE_26</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_PROCESSABLE_MODE_26 = 65;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_DIGITAL_NETWORK_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_DIGITAL_NETWORK_CAPABLE = 66;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_DIGITAL_NETWORK_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_DIGITAL_NETWORK_CAPABLE = 66;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_DUPLEX_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_DUPLEX_CAPABLE = 67;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_DUPLEX_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_DUPLEX_CAPABLE = 67;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_T81_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_T81_CAPABLE = 68;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_T81_MODE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_T81_MODE = 68;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_FULL_COLOUR_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_FULL_COLOUR_CAPABLE = 69;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_FULL_COLOUR_MODE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_FULL_COLOUR_MODE = 69;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_PREFERRED_HUFFMAN_TABLES</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_PREFERRED_HUFFMAN_TABLES = 70;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_12BIT_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_12BIT_CAPABLE = 71;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_12BIT_COMPONENT</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_12BIT_COMPONENT = 71;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_NO_SUBSAMPLING</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_NO_SUBSAMPLING = 73;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_NO_SUBSAMPLING</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_NO_SUBSAMPLING = 73;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_CUSTOM_ILLUMINANT</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_CUSTOM_ILLUMINANT = 74;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_CUSTOM_ILLUMINANT</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_CUSTOM_ILLUMINANT = 74;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_CUSTOM_GAMUT_RANGE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_CUSTOM_GAMUT_RANGE = 75;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_CUSTOM_GAMUT_RANGE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_CUSTOM_GAMUT_RANGE = 75;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_NORTH_AMERICAN_LETTER_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_NORTH_AMERICAN_LETTER_CAPABLE = 76;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_NORTH_AMERICAN_LETTER</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_NORTH_AMERICAN_LETTER = 76;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_NORTH_AMERICAN_LEGAL_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_NORTH_AMERICAN_LEGAL_CAPABLE = 77;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_NORTH_AMERICAN_LEGAL</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_NORTH_AMERICAN_LEGAL = 77;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_T85_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_T85_CAPABLE = 78;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_T85_MODE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_T85_MODE = 78;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_T85_L0_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_T85_L0_CAPABLE = 79;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_T85_L0_MODE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_T85_L0_MODE = 79;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_HKM_KEY_MANAGEMENT_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_HKM_KEY_MANAGEMENT_CAPABLE = 81;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_HKM_KEY_MANAGEMENT_MODE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_HKM_KEY_MANAGEMENT_MODE = 81;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_RSA_KEY_MANAGEMENT_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_RSA_KEY_MANAGEMENT_CAPABLE = 82;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_RSA_KEY_MANAGEMENT_MODE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_RSA_KEY_MANAGEMENT_MODE = 82;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_OVERRIDE_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_OVERRIDE_CAPABLE = 83;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_OVERRIDE_MODE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_OVERRIDE_MODE = 83;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_HFX40_CIPHER_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_HFX40_CIPHER_CAPABLE = 84;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_HFX40_CIPHER_MODE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_HFX40_CIPHER_MODE = 84;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_ALTERNATIVE_CIPHER_2_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_ALTERNATIVE_CIPHER_2_CAPABLE = 85;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_ALTERNATIVE_CIPHER_2_MODE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_ALTERNATIVE_CIPHER_2_MODE = 85;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_ALTERNATIVE_CIPHER_3_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_ALTERNATIVE_CIPHER_3_CAPABLE = 86;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_ALTERNATIVE_CIPHER_3_MODE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_ALTERNATIVE_CIPHER_3_MODE = 86;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_HFX40_I_HASHING_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_HFX40_I_HASHING_CAPABLE = 87;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_HFX40_I_HASHING_MODE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_HFX40_I_HASHING_MODE = 87;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_ALTERNATIVE_HASHING_2_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_ALTERNATIVE_HASHING_2_CAPABLE = 89;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_ALTERNATIVE_HASHING_2_MODE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_ALTERNATIVE_HASHING_2_MODE = 89;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_ALTERNATIVE_HASHING_3_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_ALTERNATIVE_HASHING_3_CAPABLE = 90;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_ALTERNATIVE_HASHING_3_MODE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_ALTERNATIVE_HASHING_3_MODE = 90;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_T44_PAGE_LENGTH</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_T44_PAGE_LENGTH = 95;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_T44_PAGE_LENGTH</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_T44_PAGE_LENGTH = 95;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_COLOUR_GRAY_300_300_400_400_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_COLOUR_GRAY_300_300_400_400_CAPABLE = 97;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_COLOUR_GRAY_300_300_400_400</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_COLOUR_GRAY_300_300_400_400 = 97;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_COLOUR_GRAY_100_100_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_COLOUR_GRAY_100_100_CAPABLE = 98;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_COLOUR_GRAY_100_100</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_COLOUR_GRAY_100_100 = 98;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_SIMPLE_PHASE_C_BFT_NEGOTIATIONS_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_SIMPLE_PHASE_C_BFT_NEGOTIATIONS_CAPABLE = 99;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_SIMPLE_PHASE_C_BFT_NEGOTIATIONS_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_SIMPLE_PHASE_C_BFT_NEGOTIATIONS_CAPABLE = 99;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_EXTENDED_BFT_NEGOTIATIONS_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_EXTENDED_BFT_NEGOTIATIONS_CAPABLE = 100;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_INTERNET_SELECTIVE_POLLING_ADDRESS</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_INTERNET_SELECTIVE_POLLING_ADDRESS = 101;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_INTERNET_ROUTING_ADDRESS</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_INTERNET_ROUTING_ADDRESS = 102;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_INTERNET_ROUTING_ADDRESS_TRANSMISSION</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_INTERNET_ROUTING_ADDRESS_TRANSMISSION = 102;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_600_600_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_600_600_CAPABLE = 105;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_600_600</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_600_600 = 105;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_1200_1200_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_1200_1200_CAPABLE = 106;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_1200_1200</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_1200_1200 = 106;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_300_600_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_300_600_CAPABLE = 107;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_300_600</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_300_600 = 107;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_400_800_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_400_800_CAPABLE = 108;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_400_800</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_400_800 = 108;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_600_1200_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_600_1200_CAPABLE = 109;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_600_1200</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_600_1200 = 109;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_COLOUR_GRAY_600_600_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_COLOUR_GRAY_600_600_CAPABLE = 110;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_COLOUR_GRAY_600_600</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_COLOUR_GRAY_600_600 = 110;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_COLOUR_GRAY_1200_1200_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_COLOUR_GRAY_1200_1200_CAPABLE = 111;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_COLOUR_GRAY_1200_1200</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_COLOUR_GRAY_1200_1200 = 111;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_ALTERNATE_DOUBLE_SIDED_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_ALTERNATE_DOUBLE_SIDED_CAPABLE = 113;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_ALTERNATE_DOUBLE_SIDED_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_ALTERNATE_DOUBLE_SIDED_CAPABLE = 113;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_CONTINUOUS_DOUBLE_SIDED_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_CONTINUOUS_DOUBLE_SIDED_CAPABLE = 114;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_CONTINUOUS_DOUBLE_SIDED_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_CONTINUOUS_DOUBLE_SIDED_CAPABLE = 114;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_BLACK_AND_WHITE_MRC</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_BLACK_AND_WHITE_MRC = 115;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_T45_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_T45_CAPABLE = 116;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_T45_MODE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_T45_MODE = 116;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_T44_COLOUR_SPACE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_T44_COLOUR_SPACE = 119;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_T44_COLOUR_SPACE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_T44_COLOUR_SPACE = 119;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_T38_FLOW_CONTROL_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_T38_FLOW_CONTROL_CAPABLE = 121;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_T38_FLOW_CONTROL_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_T38_FLOW_CONTROL_CAPABLE = 121;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_K_GREATER_THAN_4</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_K_GREATER_THAN_4 = 122;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_T38_FAX_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_T38_FAX_CAPABLE = 123;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_T38_FAX_MODE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_T38_FAX_MODE = 123;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_T88_CAPABILITY_1</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_T88_CAPABILITY_1 = 124;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_T88_MODE_1</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_T88_MODE_1 = 124;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_T88_CAPABILITY_2</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_T88_CAPABILITY_2 = 125;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_T88_MODE_2</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_T88_MODE_2 = 125;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_T88_CAPABILITY_3</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_T88_CAPABILITY_3 = 126;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_T88_MODE_3</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_T88_MODE_3 = 126;
    /// <summary>
    /// Defines the <c>T30_DIS_BIT_SYCC_T81_CAPABLE</c> constant.
    /// </summary>
    public const int T30_DIS_BIT_SYCC_T81_CAPABLE = 127;
    /// <summary>
    /// Defines the <c>T30_DCS_BIT_SYCC_T81_MODE</c> constant.
    /// </summary>
    public const int T30_DCS_BIT_SYCC_T81_MODE = 127;
}
