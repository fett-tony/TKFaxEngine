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

    internal const float TX_PULSESHAPER_3000_GAIN = 1.000000f;
    internal const int TX_PULSESHAPER_3000_COEFF_SETS = 8;
    internal static readonly float[,] tx_pulseshaper_3000 = new float[8, 9]
    {
        { 0.0427271868f, -0.0671536030f, 0.1081966381f, -0.1972251766f, 0.6180896729f, 0.6180896729f, -0.1972251767f, 0.1081966381f, -0.0671536030f },
        { 0.0510518006f, -0.0760785820f, 0.1179946119f, -0.2122805523f, 0.7754171978f, 0.4432602021f, -0.1549525755f, 0.0827132849f, -0.0484922225f },
        { 0.0512450052f, -0.0726911695f, 0.1083907728f, -0.1920366087f, 0.9002787762f, 0.2669473129f, -0.0957354160f, 0.0471713784f, -0.0239400533f },
        { 0.0424717349f, -0.0562465570f, 0.0783985626f, -0.1318782202f, 0.9805068546f, 0.1044023433f, -0.0305746246f, 0.0080950239f, 0.0020945067f },
        { 0.0253730726f, -0.0281421501f, 0.0302460480f, -0.0315608187f, 1.0081676963f, -0.0315608187f, 0.0302460480f, -0.0281421501f, 0.0253730726f },
        { 0.0020945067f, 0.0080950240f, -0.0305746246f, 0.1044023434f, 0.9805068545f, -0.1318782203f, 0.0783985626f, -0.0562465570f, 0.0424717349f },
        { -0.0239400532f, 0.0471713784f, -0.0957354160f, 0.2669473130f, 0.9002787762f, -0.1920366087f, 0.1083907728f, -0.0726911695f, 0.0512450052f },
        { -0.0484922225f, 0.0827132849f, -0.1549525754f, 0.4432602022f, 0.7754171978f, -0.2122805523f, 0.1179946119f, -0.0760785820f, 0.0510518006f },
    };
}
