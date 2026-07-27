/*
 * TKFaxEngine - direct C# conversion of the TKFaxEngineFX/spanDSP V.34 sources.
 * Direct translation of v34_shell_map.h.
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2009 Steve Underwood.
 * Licensed under the GNU Lesser General Public License version 2.1.
 */

#nullable enable

namespace TKFaxEngine.Modem.V34;

public static partial class v34 {

    internal static readonly uint[] g2_1_rings = new uint[]
    {
        1U, 0U,
    };
    internal static readonly uint[] g4_1_rings = new uint[]
    {
        1U, 0U,
    };
    internal static readonly uint[] z8_1_rings = new uint[]
    {
        0x00000000U, 0x00000001U,
    };
    internal static readonly uint[] g2_2_rings = new uint[]
    {
        1U, 2U, 1U, 0U, 0U,
    };
    internal static readonly uint[] g4_2_rings = new uint[]
    {
        1U, 4U, 6U, 4U, 1U, 0U, 0U, 0U,
        0U,
    };
    internal static readonly uint[] z8_2_rings = new uint[]
    {
        0x00000000U, 0x00000001U, 0x00000009U, 0x00000025U, 0x0000005DU, 0x000000A3U, 0x000000DBU, 0x000000F7U,
        0x000000FFU,
    };
    internal static readonly uint[] g2_3_rings = new uint[]
    {
        1U, 2U, 3U, 2U, 1U, 0U, 0U, 0U,
        0U,
    };
    internal static readonly uint[] g4_3_rings = new uint[]
    {
        1U, 4U, 10U, 16U, 19U, 16U, 10U, 4U,
        1U, 0U, 0U,
    };
    internal static readonly uint[] z8_3_rings = new uint[]
    {
        0x00000000U, 0x00000001U, 0x00000009U, 0x0000002DU, 0x0000009DU, 0x000001A7U, 0x0000039FU, 0x000006AFU,
        0x00000AA7U, 0x00000EFAU, 0x000012F2U,
    };
    internal static readonly uint[] g2_4_rings = new uint[]
    {
        1U, 2U, 3U, 4U, 3U, 2U, 1U, 0U,
        0U, 0U, 0U, 0U, 0U,
    };
    internal static readonly uint[] g4_4_rings = new uint[]
    {
        1U, 4U, 10U, 20U, 31U, 40U, 44U, 40U,
        31U, 20U, 10U, 4U, 1U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U,
    };
    internal static readonly uint[] z8_4_rings = new uint[]
    {
        0x00000000U, 0x00000001U, 0x00000009U, 0x0000002DU, 0x000000A5U, 0x000001E7U, 0x000004BFU, 0x00000A53U,
        0x000013FBU, 0x000022EAU, 0x000037BAU, 0x00005202U, 0x00007032U, 0x00008FCEU, 0x0000ADFEU, 0x0000C846U,
        0x0000DD16U, 0x0000EC05U, 0x0000F5ADU, 0x0000FB41U, 0x0000FE19U, 0x0000FF5BU, 0x0000FFD3U, 0x0000FFF7U,
        0x0000FFFFU,
    };
    internal static readonly uint[] g2_5_rings = new uint[]
    {
        1U, 2U, 3U, 4U, 5U, 4U, 3U, 2U,
        1U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U,
    };
    internal static readonly uint[] g4_5_rings = new uint[]
    {
        1U, 4U, 10U, 20U, 35U, 52U, 68U, 80U,
        85U, 80U, 68U, 52U, 35U, 20U, 10U, 4U,
        1U, 0U, 0U, 0U,
    };
    internal static readonly uint[] z8_5_rings = new uint[]
    {
        0x00000000U, 0x00000001U, 0x00000009U, 0x0000002DU, 0x000000A5U, 0x000001EFU, 0x000004FFU, 0x00000B73U,
        0x000017BBU, 0x00002D1EU, 0x00004F7EU, 0x000082D2U, 0x0000CA62U, 0x000127E6U, 0x00019ABEU, 0x00021F8EU,
        0x0002B066U, 0x0003457BU, 0x0003D653U, 0x00045B23U,
    };
    internal static readonly uint[] g2_6_rings = new uint[]
    {
        1U, 2U, 3U, 4U, 5U, 6U, 5U, 4U,
        3U, 2U, 1U, 0U, 0U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U,
    };
    internal static readonly uint[] g4_6_rings = new uint[]
    {
        1U, 4U, 10U, 20U, 35U, 56U, 80U, 104U,
        125U, 140U, 146U, 140U, 125U, 104U, 80U, 56U,
        35U, 20U, 10U, 4U, 1U, 0U, 0U, 0U,
    };
    internal static readonly uint[] z8_6_rings = new uint[]
    {
        0x00000000U, 0x00000001U, 0x00000009U, 0x0000002DU, 0x000000A5U, 0x000001EFU, 0x00000507U, 0x00000BB3U,
        0x000018DBU, 0x000030DEU, 0x000059CEU, 0x00009B76U, 0x0000FF06U, 0x00018E56U, 0x000252C6U, 0x000353D6U,
        0x000495A6U, 0x000617A3U, 0x0007D3BBU, 0x0009BE4FU, 0x000BC6F7U, 0x000DDA09U, 0x000FE2B1U, 0x0011CD45U,
    };
    internal static readonly uint[] g2_7_rings = new uint[]
    {
        1U, 2U, 3U, 4U, 5U, 6U, 7U, 6U,
        5U, 4U, 3U, 2U, 1U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U,
    };
    internal static readonly uint[] g4_7_rings = new uint[]
    {
        1U, 4U, 10U, 20U, 35U, 56U, 84U, 116U,
        149U, 180U, 206U, 224U, 231U, 224U, 206U, 180U,
        149U, 116U, 84U, 56U, 35U, 20U, 10U, 4U,
        1U, 0U, 0U, 0U, 0U,
    };
    internal static readonly uint[] z8_7_rings = new uint[]
    {
        0x00000000U, 0x00000001U, 0x00000009U, 0x0000002DU, 0x000000A5U, 0x000001EFU, 0x00000507U, 0x00000BBBU,
        0x0000191BU, 0x000031FEU, 0x00005D8EU, 0x0000A5C6U, 0x000117C6U, 0x0001C3DAU, 0x0002BD0AU, 0x0004181EU,
        0x0005EA16U, 0x0008462BU, 0x000B3B83U, 0x000ED2D7U, 0x00130C5FU, 0x0017DE6DU, 0x001D34FDU, 0x0022F25DU,
        0x0028F0EDU, 0x002F05D4U, 0x00350464U, 0x003AC1C4U, 0x00401854U,
    };
    internal static readonly uint[] g2_8_rings = new uint[]
    {
        1U, 2U, 3U, 4U, 5U, 6U, 7U, 8U,
        7U, 6U, 5U, 4U, 3U, 2U, 1U, 0U,
        0U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U,
    };
    internal static readonly uint[] g4_8_rings = new uint[]
    {
        1U, 4U, 10U, 20U, 35U, 56U, 84U, 120U,
        161U, 204U, 246U, 284U, 315U, 336U, 344U, 336U,
        315U, 284U, 246U, 204U, 161U, 120U, 84U, 56U,
        35U, 20U, 10U, 4U, 1U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U,
    };
    internal static readonly uint[] z8_8_rings = new uint[]
    {
        0x00000000U, 0x00000001U, 0x00000009U, 0x0000002DU, 0x000000A5U, 0x000001EFU, 0x00000507U, 0x00000BBBU,
        0x00001923U, 0x0000323EU, 0x00005EAEU, 0x0000A986U, 0x00012216U, 0x0001DC9AU, 0x0002F2AAU, 0x00048342U,
        0x0006B232U, 0x0009A6DBU, 0x000D8A33U, 0x0012841FU, 0x0018B847U, 0x0020429DU, 0x002933E5U, 0x00338EA1U,
        0x003F44D9U, 0x004C3714U, 0x005A34B4U, 0x0068FDC4U, 0x00784624U, 0x0087B9DCU, 0x0097023CU, 0x00A5CB4CU,
        0x00B3C8ECU, 0x00C0BB27U, 0x00CC715FU, 0x00D6CC1BU, 0x00DFBD63U, 0x00E747B9U, 0x00ED7BE1U, 0x00F275CDU,
        0x00F65925U, 0x00F94DCEU, 0x00FB7CBEU, 0x00FD0D56U, 0x00FE2366U, 0x00FEDDEAU, 0x00FF567AU, 0x00FFA152U,
        0x00FFCDC2U, 0x00FFE6DDU, 0x00FFF445U, 0x00FFFAF9U, 0x00FFFE11U, 0x00FFFF5BU, 0x00FFFFD3U, 0x00FFFFF7U,
        0x00FFFFFFU,
    };
    internal static readonly uint[] g2_9_rings = new uint[]
    {
        1U, 2U, 3U, 4U, 5U, 6U, 7U, 8U,
        9U, 8U, 7U, 6U, 5U, 4U, 3U, 2U,
        1U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U,
    };
    internal static readonly uint[] g4_9_rings = new uint[]
    {
        1U, 4U, 10U, 20U, 35U, 56U, 84U, 120U,
        165U, 216U, 270U, 324U, 375U, 420U, 456U, 480U,
        489U, 480U, 456U, 420U, 375U, 324U, 270U, 216U,
        165U, 120U, 84U, 56U, 35U, 20U, 10U, 4U,
        1U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
    };
    internal static readonly uint[] z8_9_rings = new uint[]
    {
        0x00000000U, 0x00000001U, 0x00000009U, 0x0000002DU, 0x000000A5U, 0x000001EFU, 0x00000507U, 0x00000BBBU,
        0x00001923U, 0x00003246U, 0x00005EEEU, 0x0000AAA6U, 0x000125D6U, 0x0001E6EAU, 0x00030B6AU, 0x0004B8E2U,
        0x00071D72U, 0x000A6FD7U, 0x000EEEB7U, 0x0014DF0FU, 0x001C89B7U, 0x00263805U, 0x00322FADU, 0x0040AE11U,
        0x0051E349U, 0x0065ED40U, 0x007CD358U, 0x0096831CU, 0x00B2CE64U, 0x00D16B2CU, 0x00F1F53CU, 0x0113F19CU,
        0x0136D3ACU, 0x015A0395U, 0x017CE5A5U, 0x019EE205U, 0x01BF6C15U, 0x01DE08DDU, 0x01FA5425U, 0x021403E9U,
    };
    internal static readonly uint[] g2_10_rings = new uint[]
    {
        1U, 2U, 3U, 4U, 5U, 6U, 7U, 8U,
        9U, 10U, 9U, 8U, 7U, 6U, 5U, 4U,
        3U, 2U, 1U, 0U, 0U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U,
    };
    internal static readonly uint[] g4_10_rings = new uint[]
    {
        1U, 4U, 10U, 20U, 35U, 56U, 84U, 120U,
        165U, 220U, 282U, 348U, 415U, 480U, 540U, 592U,
        633U, 660U, 670U, 660U, 633U, 592U, 540U, 480U,
        415U, 348U, 282U, 220U, 165U, 120U, 84U, 56U,
        35U, 20U, 10U, 4U, 1U, 0U, 0U, 0U,
        0U, 0U,
    };
    internal static readonly uint[] z8_10_rings = new uint[]
    {
        0x00000000U, 0x00000001U, 0x00000009U, 0x0000002DU, 0x000000A5U, 0x000001EFU, 0x00000507U, 0x00000BBBU,
        0x00001923U, 0x00003246U, 0x00005EF6U, 0x0000AAE6U, 0x000126F6U, 0x0001EAAAU, 0x000315BAU, 0x0004D1A2U,
        0x00075312U, 0x000ADB17U, 0x000FB7CFU, 0x00164473U, 0x001EE87BU, 0x002A15B5U, 0x0038453DU, 0x0049F359U,
        0x005F9A51U, 0x0079AC70U, 0x00988D70U, 0x00BC8BA8U, 0x00E5D968U, 0x01148704U, 0x01487E24U, 0x01817ECCU,
        0x01BF1E6CU, 0x0200C925U, 0x0245C54DU, 0x028D3919U, 0x02D63231U, 0x031FAECFU, 0x0368A7E7U, 0x03B01BB3U,
        0x03F517DBU, 0x0436C294U,
    };
    internal static readonly uint[] g2_11_rings = new uint[]
    {
        1U, 2U, 3U, 4U, 5U, 6U, 7U, 8U,
        9U, 10U, 11U, 10U, 9U, 8U, 7U, 6U,
        5U, 4U, 3U, 2U, 1U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U,
    };
    internal static readonly uint[] g4_11_rings = new uint[]
    {
        1U, 4U, 10U, 20U, 35U, 56U, 84U, 120U,
        165U, 220U, 286U, 360U, 439U, 520U, 600U, 676U,
        745U, 804U, 850U, 880U, 891U, 880U, 850U, 804U,
        745U, 676U, 600U, 520U, 439U, 360U, 286U, 220U,
        165U, 120U, 84U, 56U, 35U, 20U, 10U, 4U,
        1U, 0U, 0U, 0U, 0U,
    };
    internal static readonly uint[] z8_11_rings = new uint[]
    {
        0x00000000U, 0x00000001U, 0x00000009U, 0x0000002DU, 0x000000A5U, 0x000001EFU, 0x00000507U, 0x00000BBBU,
        0x00001923U, 0x00003246U, 0x00005EF6U, 0x0000AAEEU, 0x00012736U, 0x0001EBCAU, 0x0003197AU, 0x0004DBF2U,
        0x00076BD2U, 0x000B10B7U, 0x0010230FU, 0x00170D8BU, 0x00204DFBU, 0x002C7559U, 0x003C26C1U, 0x00501529U,
        0x0068FFC1U, 0x0087ACF8U, 0x00ACE438U, 0x00D96680U, 0x010DE618U, 0x014AFDB0U, 0x01912750U, 0x01E0B394U,
        0x0239C1C4U, 0x029C396DU, 0x0307C5FDU, 0x037BD4C1U, 0x03F79581U, 0x0479FDD3U, 0x0501CF1BU, 0x058D9F0BU,
        0x061BE253U, 0x06AAF90EU, 0x07393C56U, 0x07C50C46U, 0x084CDD8EU,
    };
    internal static readonly uint[] g2_12_rings = new uint[]
    {
        1U, 2U, 3U, 4U, 5U, 6U, 7U, 8U,
        9U, 10U, 11U, 12U, 11U, 10U, 9U, 8U,
        7U, 6U, 5U, 4U, 3U, 2U, 1U, 0U,
        0U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U,
    };
    internal static readonly uint[] g4_12_rings = new uint[]
    {
        1U, 4U, 10U, 20U, 35U, 56U, 84U, 120U,
        165U, 220U, 286U, 364U, 451U, 544U, 640U, 736U,
        829U, 916U, 994U, 1060U, 1111U, 1144U, 1156U, 1144U,
        1111U, 1060U, 994U, 916U, 829U, 736U, 640U, 544U,
        451U, 364U, 286U, 220U, 165U, 120U, 84U, 56U,
        35U, 20U, 10U, 4U, 1U, 0U, 0U, 0U,
        0U,
    };
    internal static readonly uint[] z8_12_rings = new uint[]
    {
        0x00000000U, 0x00000001U, 0x00000009U, 0x0000002DU, 0x000000A5U, 0x000001EFU, 0x00000507U, 0x00000BBBU,
        0x00001923U, 0x00003246U, 0x00005EF6U, 0x0000AAEEU, 0x0001273EU, 0x0001EC0AU, 0x00031A9AU, 0x0004DFB2U,
        0x00077622U, 0x000B2977U, 0x001058AFU, 0x001778CBU, 0x00211713U, 0x002DDAD9U, 0x003E8681U, 0x0053F78DU,
        0x006F2565U, 0x00911EA8U, 0x00BB04E8U, 0x00EE06C8U, 0x012B5888U, 0x01742B20U, 0x01C9A220U, 0x022CC8A0U,
        0x029E85A0U, 0x031F904DU, 0x03B064B5U, 0x04513989U, 0x0501F7A1U, 0x05C233D3U, 0x06912B8BU, 0x076DC46FU,
        0x08568F37U, 0x0949CDBEU, 0x0A457C2EU, 0x0B475D06U, 0x0C4D0796U, 0x0D53F86AU, 0x0E59A2FAU, 0x0F5B83D2U,
        0x10573242U,
    };
    internal static readonly uint[] g2_13_rings = new uint[]
    {
        1U, 2U, 3U, 4U, 5U, 6U, 7U, 8U,
        9U, 10U, 11U, 12U, 13U, 12U, 11U, 10U,
        9U, 8U, 7U, 6U, 5U, 4U, 3U, 2U,
        1U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U,
    };
    internal static readonly uint[] g4_13_rings = new uint[]
    {
        1U, 4U, 10U, 20U, 35U, 56U, 84U, 120U,
        165U, 220U, 286U, 364U, 455U, 556U, 664U, 776U,
        889U, 1000U, 1106U, 1204U, 1291U, 1364U, 1420U, 1456U,
        1469U, 1456U, 1420U, 1364U, 1291U, 1204U, 1106U, 1000U,
        889U, 776U, 664U, 556U, 455U, 364U, 286U, 220U,
        165U, 120U, 84U, 56U, 35U, 20U, 10U, 4U,
        1U, 0U, 0U, 0U, 0U, 0U,
    };
    internal static readonly uint[] z8_13_rings = new uint[]
    {
        0x00000000U, 0x00000001U, 0x00000009U, 0x0000002DU, 0x000000A5U, 0x000001EFU, 0x00000507U, 0x00000BBBU,
        0x00001923U, 0x00003246U, 0x00005EF6U, 0x0000AAEEU, 0x0001273EU, 0x0001EC12U, 0x00031ADAU, 0x0004E0D2U,
        0x000779E2U, 0x000B33C7U, 0x0010716FU, 0x0017AE6BU, 0x00218253U, 0x002EA3F1U, 0x003FEC01U, 0x0056574DU,
        0x007307E5U, 0x0097452CU, 0x00C47A6CU, 0x00FC33B8U, 0x014018F8U, 0x0191E710U, 0x01F36728U, 0x02666430U,
        0x02EC9ED0U, 0x0387C009U, 0x04394AE1U, 0x05028D89U, 0x05E49281U, 0x06E01253U, 0x07F56693U, 0x09247EE7U,
        0x0A6CD8B7U, 0x0BCD7A02U, 0x0D44EFB2U, 0x0ED14FA6U, 0x10703E86U, 0x121EF952U, 0x13DA627AU, 0x159F122AU,
        0x17696952U, 0x1935A6CFU, 0x1AFFFDF7U, 0x1CC4ADA7U, 0x1E8016CFU, 0x202ED19BU,
    };
    internal static readonly uint[] g2_14_rings = new uint[]
    {
        1U, 2U, 3U, 4U, 5U, 6U, 7U, 8U,
        9U, 10U, 11U, 12U, 13U, 14U, 13U, 12U,
        11U, 10U, 9U, 8U, 7U, 6U, 5U, 4U,
        3U, 2U, 1U, 0U, 0U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U,
    };
    internal static readonly uint[] g4_14_rings = new uint[]
    {
        1U, 4U, 10U, 20U, 35U, 56U, 84U, 120U,
        165U, 220U, 286U, 364U, 455U, 560U, 676U, 800U,
        929U, 1060U, 1190U, 1316U, 1435U, 1544U, 1640U, 1720U,
        1781U, 1820U, 1834U, 1820U, 1781U, 1720U, 1640U, 1544U,
        1435U, 1316U, 1190U, 1060U, 929U, 800U, 676U, 560U,
        455U, 364U, 286U, 220U, 165U, 120U, 84U, 56U,
        35U, 20U, 10U, 4U, 1U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U,
    };
    internal static readonly uint[] z8_14_rings = new uint[]
    {
        0x00000000U, 0x00000001U, 0x00000009U, 0x0000002DU, 0x000000A5U, 0x000001EFU, 0x00000507U, 0x00000BBBU,
        0x00001923U, 0x00003246U, 0x00005EF6U, 0x0000AAEEU, 0x0001273EU, 0x0001EC12U, 0x00031AE2U, 0x0004E112U,
        0x00077B02U, 0x000B3787U, 0x00107BBFU, 0x0017C72BU, 0x0021B7F3U, 0x002F0F31U, 0x0040B519U, 0x0057BCCDU,
        0x007567A5U, 0x009B27ACU, 0x00CAA10CU, 0x0105AA1CU, 0x014E49BCU, 0x01A6B3C0U, 0x02114340U, 0x029072B8U,
        0x0326D1F8U, 0x03D6F9F9U, 0x04A37EC1U, 0x058EDF95U, 0x069B75CDU, 0x07CB62B3U, 0x09207CEBU, 0x0A9C3DF7U,
        0x0C3FB07FU, 0x0E0B601AU, 0x0FFF4B6AU, 0x121AD93AU, 0x145CD12AU, 0x16C35852U, 0x194BF222U, 0x1BF385A2U,
        0x1EB66712U, 0x219065C7U, 0x247CDDFFU, 0x2776CE43U, 0x2A78EFCBU, 0x2D7DD135U, 0x307FF2BDU, 0x3379E301U,
        0x36665B39U, 0x394059EEU, 0x3C033B5EU, 0x3EAACEDEU, 0x413368AEU,
    };
    internal static readonly uint[] g2_15_rings = new uint[]
    {
        1U, 2U, 3U, 4U, 5U, 6U, 7U, 8U,
        9U, 10U, 11U, 12U, 13U, 14U, 15U, 14U,
        13U, 12U, 11U, 10U, 9U, 8U, 7U, 6U,
        5U, 4U, 3U, 2U, 1U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U,
    };
    internal static readonly uint[] g4_15_rings = new uint[]
    {
        1U, 4U, 10U, 20U, 35U, 56U, 84U, 120U,
        165U, 220U, 286U, 364U, 455U, 560U, 680U, 812U,
        953U, 1100U, 1250U, 1400U, 1547U, 1688U, 1820U, 1940U,
        2045U, 2132U, 2198U, 2240U, 2255U, 2240U, 2198U, 2132U,
        2045U, 1940U, 1820U, 1688U, 1547U, 1400U, 1250U, 1100U,
        953U, 812U, 680U, 560U, 455U, 364U, 286U, 220U,
        165U, 120U, 84U, 56U, 35U, 20U, 10U, 4U,
        1U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U, 0U,
    };
    internal static readonly uint[] z8_15_rings = new uint[]
    {
        0x00000000U, 0x00000001U, 0x00000009U, 0x0000002DU, 0x000000A5U, 0x000001EFU, 0x00000507U, 0x00000BBBU,
        0x00001923U, 0x00003246U, 0x00005EF6U, 0x0000AAEEU, 0x0001273EU, 0x0001EC12U, 0x00031AE2U, 0x0004E11AU,
        0x00077B42U, 0x000B38A7U, 0x00107F7FU, 0x0017D17BU, 0x0021D0B3U, 0x002F44D1U, 0x00412059U, 0x005885E5U,
        0x0076CD25U, 0x009D876CU, 0x00CE838CU, 0x010BD0BCU, 0x0157C03CU, 0x01B4E564U, 0x022613C4U, 0x02AE5B10U,
        0x035100A8U, 0x041176A1U, 0x04F35049U, 0x05FA3435U, 0x0729CBFDU, 0x0885B1DFU, 0x0A115C97U, 0x0BD009CFU,
        0x0DC4A79FU, 0x0FF1BDAAU, 0x1259567AU, 0x14FCE9D2U, 0x17DD48C2U, 0x1AFA8C5EU, 0x1E5407D6U, 0x21E83E8AU,
        0x25B4DE92U, 0x29B6C00FU, 0x2DE9E977U, 0x324998EBU, 0x36D05283U, 0x3B77F359U, 0x4039C8F9U, 0x450EACB9U,
        0x49EF2259U, 0x4ED37928U, 0x53B3EEC8U, 0x5888D288U, 0x5D4AA828U, 0x61F248FEU, 0x66790296U, 0x6AD8B20AU,
        0x6F0BDB72U, 0x730DBCEFU, 0x76DA5CF7U, 0x7A6E93ABU, 0x7DC80F23U, 0x80E552BFU,
    };
    internal static readonly uint[] g2_17_rings = new uint[]
    {
        1U, 2U, 3U, 4U, 5U, 6U, 7U, 8U,
        9U, 10U, 11U, 12U, 13U, 14U, 15U, 16U,
        17U, 16U, 15U, 14U, 13U, 12U, 11U, 10U,
        9U, 8U, 7U, 6U, 5U, 4U, 3U, 2U,
        1U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U,
    };
    internal static readonly uint[] g4_17_rings = new uint[]
    {
        1U, 4U, 10U, 20U, 35U, 56U, 84U, 120U,
        165U, 220U, 286U, 364U, 455U, 560U, 680U, 816U,
        969U, 1136U, 1314U, 1500U, 1691U, 1884U, 2076U, 2264U,
        2445U, 2616U, 2774U, 2916U, 3039U, 3140U, 3216U, 3264U,
        3281U, 3264U, 3216U, 3140U, 3039U, 2916U, 2774U, 2616U,
        2445U, 2264U, 2076U, 1884U, 1691U, 1500U, 1314U, 1136U,
        969U, 816U, 680U, 560U,
    };
    internal static readonly uint[] z8_17_rings = new uint[]
    {
        0x00000000U, 0x00000001U, 0x00000009U, 0x0000002DU, 0x000000A5U, 0x000001EFU, 0x00000507U, 0x00000BBBU,
        0x00001923U, 0x00003246U, 0x00005EF6U, 0x0000AAEEU, 0x0001273EU, 0x0001EC12U, 0x00031AE2U, 0x0004E11AU,
        0x00077B4AU, 0x000B38EFU, 0x001080DFU, 0x0017D65BU, 0x0021DEC3U, 0x002F67E1U, 0x00416EB9U, 0x005926C5U,
        0x0078017DU, 0x009FB604U, 0x00D248CCU, 0x011212FCU, 0x0161C95CU, 0x01C48284U, 0x023DBC04U, 0x02D15E34U,
        0x0383BE54U, 0x04599E9DU, 0x05582BDDU, 0x0684F84DU, 0x07E5F365U, 0x09815E87U, 0x0B5DBE6FU, 0x0D81C96BU,
        0x0FF45273U, 0x12BC314EU, 0x15E02806U, 0x1966C602U, 0x1D564932U, 0x21B47DCEU, 0x26869D3EU, 0x2BD12CD6U,
        0x3197DD26U, 0x37DD6AB3U, 0x3EA38103U, 0x45EAA0FBU,
    };
    internal static readonly uint[] g2_18_rings = new uint[]
    {
        1U, 2U, 3U, 4U, 5U, 6U, 7U, 8U,
        9U, 10U, 11U, 12U, 13U, 14U, 15U, 16U,
        17U, 18U, 17U, 16U, 15U, 14U, 13U, 12U,
        11U, 10U, 9U, 8U, 7U, 6U, 5U, 4U,
        3U, 2U, 1U, 0U, 0U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U, 0U, 0U, 0U,
        0U, 0U, 0U, 0U, 0U,
    };
    internal static readonly uint[] g4_18_rings = new uint[]
    {
        1U, 4U, 10U, 20U, 35U, 56U, 84U, 120U,
        165U, 220U, 286U, 364U, 455U, 560U, 680U, 816U,
        969U, 1140U, 1326U, 1524U, 1731U, 1944U, 2160U, 2376U,
        2589U, 2796U, 2994U, 3180U, 3351U, 3504U, 3636U, 3744U,
        3825U, 3876U, 3894U, 3876U, 3825U, 3744U, 3636U, 3504U,
        3351U, 3180U, 2994U, 2796U, 2589U, 2376U, 2160U, 1944U,
        1731U, 1524U, 1326U, 1140U, 969U, 816U, 680U, 560U,
        455U,
    };
    internal static readonly uint[] z8_18_rings = new uint[]
    {
        0x00000000U, 0x00000001U, 0x00000009U, 0x0000002DU, 0x000000A5U, 0x000001EFU, 0x00000507U, 0x00000BBBU,
        0x00001923U, 0x00003246U, 0x00005EF6U, 0x0000AAEEU, 0x0001273EU, 0x0001EC12U, 0x00031AE2U, 0x0004E11AU,
        0x00077B4AU, 0x000B38EFU, 0x001080E7U, 0x0017D69BU, 0x0021DFE3U, 0x002F6BA1U, 0x00417909U, 0x00593F85U,
        0x0078371DU, 0x00A02144U, 0x00D311E4U, 0x0113787CU, 0x0164291CU, 0x01C86504U, 0x0243E2A4U, 0x02DAD4B4U,
        0x0391F014U, 0x046E701DU, 0x05761905U, 0x06AF37F1U, 0x0820A049U, 0x09D1A5F7U, 0x0BCA144FU, 0x0E122173U,
        0x10B25E1BU, 0x13B3A1BEU, 0x171EF32EU, 0x1AFD6DCEU, 0x1F58239EU, 0x2437FC6EU, 0x29A5929EU, 0x2FA90DE6U,
        0x3649FCB6U, 0x3D8F2CD3U, 0x457E83EBU, 0x4E1CD8EFU, 0x576DCF17U, 0x6173B389U, 0x6C2F5EB1U, 0x77A01A35U,
        0x83C38C4DU,
    };
    internal static readonly uint[]?[] g2s = new uint[]?[]
    {
        null,
        g2_1_rings,
        g2_2_rings,
        g2_3_rings,
        g2_4_rings,
        g2_5_rings,
        g2_6_rings,
        g2_7_rings,
        g2_8_rings,
        g2_9_rings,
        g2_10_rings,
        g2_11_rings,
        g2_12_rings,
        g2_13_rings,
        g2_14_rings,
        g2_15_rings,
        null,
        g2_17_rings,
        g2_18_rings,
    };
    internal static readonly uint[]?[] g4s = new uint[]?[]
    {
        null,
        g4_1_rings,
        g4_2_rings,
        g4_3_rings,
        g4_4_rings,
        g4_5_rings,
        g4_6_rings,
        g4_7_rings,
        g4_8_rings,
        g4_9_rings,
        g4_10_rings,
        g4_11_rings,
        g4_12_rings,
        g4_13_rings,
        g4_14_rings,
        g4_15_rings,
        null,
        g4_17_rings,
        g4_18_rings,
    };
    internal static readonly uint[]?[] z8s = new uint[]?[]
    {
        null,
        z8_1_rings,
        z8_2_rings,
        z8_3_rings,
        z8_4_rings,
        z8_5_rings,
        z8_6_rings,
        z8_7_rings,
        z8_8_rings,
        z8_9_rings,
        z8_10_rings,
        z8_11_rings,
        z8_12_rings,
        z8_13_rings,
        z8_14_rings,
        z8_15_rings,
        null,
        z8_17_rings,
        z8_18_rings,
    };
}
