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

    internal const float TX_PULSESHAPER_3429_GAIN = 1.000000f;
    internal const int TX_PULSESHAPER_3429_COEFF_SETS = 7;
    internal static readonly float[,] tx_pulseshaper_3429 = new float[7, 9]
    {
        { 0.0483842149f, -0.0736480266f, 0.1160094591f, -0.2097460513f, 0.7111395730f, 0.5193054249f, -0.1757768510f, 0.0952271910f, -0.0574570498f },
        { 0.0520947979f, -0.0749976357f, 0.1132297372f, -0.2017675472f, 0.8686947733f, 0.3165523743f, -0.1137410107f, 0.0579785101f, -0.0313022851f },
        { 0.0442666028f, -0.0593661574f, 0.0838830266f, -0.1429760359f, 0.9721316557f, 0.1262593043f, -0.0398417091f, 0.0136446808f, -0.0015488886f },
        { 0.0253730464f, -0.0281419874f, 0.0302457715f, -0.0315604668f, 1.0081672727f, -0.0315604669f, 0.0302457714f, -0.0281419874f, 0.0253730464f },
        { -0.0015488886f, 0.0136446808f, -0.0398417091f, 0.1262593044f, 0.9721316557f, -0.1429760359f, 0.0838830266f, -0.0593661574f, 0.0442666027f },
        { -0.0313022851f, 0.0579785102f, -0.1137410107f, 0.3165523744f, 0.8686947733f, -0.2017675472f, 0.1132297372f, -0.0749976357f, 0.0520947979f },
        { -0.0574570498f, 0.0952271910f, -0.1757768510f, 0.5193054249f, 0.7111395730f, -0.2097460513f, 0.1160094591f, -0.0736480266f, 0.0483842149f },
    };
}
