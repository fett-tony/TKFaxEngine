/*
 * TKFaxEngine - direct C# conversion of the TKFaxEngineFX/spanDSP V.34 sources.
 *
 * Direct translation of the corresponding TKFaxEngineFX source file.
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2009 Steve Underwood.
 * Licensed under the GNU Lesser General Public License version 2.1.
 *
 * THIS IS A WORK IN PROGRESS - NOT YET FUNCTIONAL!
 * This status is inherited unchanged from the original V.34 source.
 */

#nullable enable

namespace TKFaxEngine.Modem.V34;

public static partial class v34 {

    internal const float TX_PULSESHAPER_2400_GAIN = 1.000000f;
    internal const int TX_PULSESHAPER_2400_COEFF_SETS = 10;
    internal static readonly float[,] tx_pulseshaper_2400 = new float[10, 9]
    {
        { 0.0427277669f, -0.0671541742f, 0.1081971140f, -0.1972254808f, 0.6180897244f, 0.6180897243f, -0.1972254808f, 0.1081971140f, -0.0671541743f },
        { 0.0499874940f, -0.0752200608f, 0.1175035007f, -0.2118776130f, 0.7460997941f, 0.4788579758f, -0.1651118220f, 0.0888149237f, -0.0528322265f },
        { 0.0522286994f, -0.0756176622f, 0.1146924527f, -0.2047556983f, 0.8550897074f, 0.3366369436f, -0.1207510403f, 0.0621858803f, -0.0341901020f },
        { 0.0488106752f, -0.0676515681f, 0.0987903837f, -0.1728811114f, 0.9382774198f, 0.1994893510f, -0.0697782927f, 0.0315943414f, -0.0134557740f },
        { 0.0396705524f, -0.0514749536f, 0.0700905826f, -0.1149515938f, 0.9904104903f, 0.0747329606f, -0.0177623585f, 0.0004307100f, 0.0070953485f },
        { 0.0253730539f, -0.0281419078f, 0.0302456177f, -0.0315602632f, 1.0081669990f, -0.0315602632f, 0.0302456177f, -0.0281419079f, 0.0253730539f },
        { 0.0070953485f, 0.0004307100f, -0.0177623585f, 0.0747329607f, 0.9904104903f, -0.1149515939f, 0.0700905825f, -0.0514749537f, 0.0396705524f },
        { -0.0134557740f, 0.0315943414f, -0.0697782927f, 0.1994893510f, 0.9382774198f, -0.1728811115f, 0.0987903837f, -0.0676515681f, 0.0488106752f },
        { -0.0341901020f, 0.0621858804f, -0.1207510403f, 0.3366369437f, 0.8550897073f, -0.2047556984f, 0.1146924526f, -0.0756176622f, 0.0522286994f },
        { -0.0528322264f, 0.0888149237f, -0.1651118220f, 0.4788579759f, 0.7460997941f, -0.2118776131f, 0.1175035006f, -0.0752200608f, 0.0499874940f },
    };
}
