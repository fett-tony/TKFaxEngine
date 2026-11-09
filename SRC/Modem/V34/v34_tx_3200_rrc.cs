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

    internal const float TX_PULSESHAPER_3200_GAIN = 1.000000f;
    internal const int TX_PULSESHAPER_3200_COEFF_SETS = 5;
    internal static readonly float[,] tx_pulseshaper_3200 = new float[5, 9]
    {
        { 0.0499874737f, -0.0752200398f, 0.1175034854f, -0.2118776091f, 0.7460998138f, 0.4788579748f, -0.1651118068f, 0.0888149054f, -0.0528322120f },
        { 0.0488106506f, -0.0676515520f, 0.0987903799f, -0.1728811214f, 0.9382774573f, 0.1994893354f, -0.0697782753f, 0.0315943304f, -0.0134557745f },
        { 0.0253730375f, -0.0281419049f, 0.0302456273f, -0.0315602816f, 1.0081670435f, -0.0315602816f, 0.0302456273f, -0.0281419050f, 0.0253730375f },
        { -0.0134557745f, 0.0315943304f, -0.0697782753f, 0.1994893354f, 0.9382774573f, -0.1728811214f, 0.0987903799f, -0.0676515520f, 0.0488106506f },
        { -0.0528322120f, 0.0888149054f, -0.1651118068f, 0.4788579749f, 0.7460998138f, -0.2118776091f, 0.1175034854f, -0.0752200398f, 0.0499874737f },
    };
}
