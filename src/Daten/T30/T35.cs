/*
 * TKFaxEngine - managed C# port
 *
 * Combined port of t35.c and t35.h.
 * Original implementation by Steve Underwood.
 */

namespace TKFaxEngine.Daten.T30;

public sealed record T35DecodedIdentity(string? Country, string? Vendor, string? Model);

internal sealed record T35Model(byte[] Id, int MatchLength, string Name);
internal sealed record T35Vendor(byte[] Id, int MatchLength, string? Name, bool InverseStationIdOrder, T35Model[] Models);
internal sealed record T35Country(string? Name, T35Vendor[] Vendors);

public static class T35 {
    private static readonly T35Model[] ModelsCanon =
    {
        new(new byte[] { 0x80, 0x00, 0x80, 0x48, 0x00 }, 5, "Faxphone B640"),
        new(new byte[] { 0x80, 0x00, 0x80, 0x49, 0x10 }, 5, "Fax B100"),
        new(new byte[] { 0x80, 0x00, 0x8A, 0x49, 0x10 }, 5, "Laser Class 9000 Series"),
        new(new byte[] { 0x80, 0x00, 0x8A, 0x48, 0x00 }, 5, "Laser Class 2060"),
    };

    private static readonly T35Model[] ModelsBrother =
    {
        new(new byte[] { 0x55, 0x55, 0x00, 0x88, 0x90, 0x80, 0x5F, 0x00, 0x15, 0x51 }, 9, "Intellifax 770"),
        new(new byte[] { 0x55, 0x55, 0x00, 0x80, 0xB0, 0x80, 0x00, 0x00, 0x59, 0xD4 }, 9, "Personal fax 190"),
        new(new byte[] { 0x55, 0x55, 0x00, 0x8C, 0x90, 0x80, 0xF0, 0x02, 0x20 }, 9, "MFC-8600"),
    };

    private static readonly T35Model[] ModelsPanasonic0E =
    {
        new(new byte[] { 0x00, 0x00, 0x00, 0x96, 0x0F, 0x01, 0x02, 0x00, 0x10, 0x05, 0x02, 0x95, 0xC8, 0x08, 0x01, 0x49, 0x02, 0x41, 0x53, 0x54, 0x47 }, 10, "KX-F90"),
        new(new byte[] { 0x00, 0x00, 0x00, 0x96, 0x0F, 0x01, 0x03, 0x00, 0x10, 0x05, 0x02, 0x95, 0xC8, 0x08, 0x01, 0x49, 0x02, 0x03 }, 10, "KX-F230 or KX-FT21 or ..."),
        new(new byte[] { 0x00, 0x00, 0x00, 0x16, 0x0F, 0x01, 0x03, 0x00, 0x10, 0x05, 0x02, 0x95, 0xC8, 0x08 }, 10, "KX-F780"),
        new(new byte[] { 0x00, 0x00, 0x00, 0x16, 0x0F, 0x01, 0x03, 0x00, 0x10, 0x00, 0x02, 0x95, 0x80, 0x08, 0x75, 0xB5 }, 10, "KX-M260"),
        new(new byte[] { 0x00, 0x00, 0x00, 0x16, 0x0F, 0x01, 0x02, 0x00, 0x10, 0x05, 0x02, 0x85, 0xC8, 0x08, 0xAD }, 10, "KX-F2050BS"),
    };

    private static readonly T35Model[] ModelsPanasonic79 =
    {
        new(new byte[] { 0x00, 0x00, 0x00, 0x02, 0x0F, 0x09, 0x12, 0x00, 0x10, 0x05, 0x02, 0x95, 0xC8, 0x88, 0x80, 0x80, 0x01 }, 10, "UF-S10"),
        new(new byte[] { 0x00, 0x00, 0x00, 0x16, 0x7F, 0x09, 0x13, 0x00, 0x10, 0x05, 0x16, 0x8D, 0xC0, 0xD0, 0xF8, 0x80, 0x01 }, 10, "/Siemens Fax 940"),
        new(new byte[] { 0x00, 0x00, 0x00, 0x16, 0x0F, 0x09, 0x13, 0x00, 0x10, 0x05, 0x06, 0x8D, 0xC0, 0x50, 0xCB }, 10, "Panafax UF-321"),
    };

    private static readonly T35Model[] ModelsRicoh =
    {
        new(new byte[] { 0x00, 0x00, 0x00, 0x12, 0x10, 0x0D, 0x02, 0x00, 0x50, 0x00, 0x2A, 0xB8, 0x2C }, 10, "/Nashuatec P394"),
    };

    private static readonly T35Model[] ModelsSamsung16 =
    {
        new(new byte[] { 0x00, 0x00, 0xA4, 0x01 }, 4, "M545 6800"),
    };

    private static readonly T35Model[] ModelsSamsung5A =
    {
        new(new byte[] { 0x00, 0x00, 0xC0, 0x00 }, 4, "SF-5100"),
    };

    private static readonly T35Model[] ModelsSamsung8C =
    {
        new(new byte[] { 0x00, 0x00, 0x01, 0x00 }, 4, "SF-2010"),
    };

    private static readonly T35Model[] ModelsSamsungA2 =
    {
        new(new byte[] { 0x00, 0x00, 0x80, 0x00 }, 4, "FX-4000"),
    };

    private static readonly T35Model[] ModelsSanyo =
    {
        new(new byte[] { 0x00, 0x00, 0x10, 0xB1, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x41, 0x26, 0xFF, 0xFF, 0x00, 0x00, 0x85, 0xA1 }, 10, "SFX-107"),
        new(new byte[] { 0x00, 0x00, 0x00, 0xB1, 0x12, 0xF2, 0x62, 0xB4, 0x82, 0x0A, 0xF2, 0x2A, 0x12, 0xD2, 0xA2, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x41, 0x4E, 0xFF, 0xFF, 0x00, 0x00 }, 10, "MFP-510"),
    };

    private static readonly T35Model[] ModelsHP =
    {
        new(new byte[] { 0x20, 0x00, 0x45, 0x00, 0x0C, 0x04, 0x70, 0xCD, 0x4F, 0x00, 0x7F, 0x49 }, 5, "LaserJet 3150"),
        new(new byte[] { 0x40, 0x80, 0x84, 0x01, 0xF0, 0x6A }, 5, "OfficeJet"),
        new(new byte[] { 0xC0, 0x00, 0x00, 0x00, 0x00 }, 5, "OfficeJet 500"),
        new(new byte[] { 0xC0, 0x00, 0x00, 0x00, 0x00, 0x8B }, 5, "Fax-920"),
    };

    private static readonly T35Model[] ModelsSharp =
    {
        new(new byte[] { 0x00, 0xCE, 0xB8, 0x80, 0x80, 0x11, 0x85, 0x0D, 0xDD, 0x00, 0x00, 0xDD, 0xDD, 0x00, 0x00, 0xDD, 0xDD, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xED, 0x22, 0xB0, 0x00, 0x00, 0x90, 0x00 }, 32, "Sharp F0-10"),
        new(new byte[] { 0x00, 0xCE, 0xB8, 0x80, 0x80, 0x11, 0x85, 0x0D, 0xDD, 0x00, 0x00, 0xDD, 0xDD, 0x00, 0x00, 0xDD, 0xDD, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xED, 0x22, 0xB0, 0x00, 0x00, 0x90, 0x00, 0x8C }, 33, "Sharp UX-460"),
        new(new byte[] { 0x00, 0x4E, 0xB8, 0x80, 0x80, 0x11, 0x84, 0x0D, 0xDD, 0x00, 0x00, 0xDD, 0xDD, 0x00, 0x00, 0xDD, 0xDD, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xED, 0x22, 0xB0, 0x00, 0x00, 0x90, 0x00, 0xAD }, 33, "Sharp UX-177"),
        new(new byte[] { 0x00, 0xCE, 0xB8, 0x00, 0x84, 0x0D, 0xDD, 0x00, 0x00, 0xDD, 0xDD, 0x00, 0x00, 0xDD, 0xDD, 0xDD, 0xDD, 0xDD, 0x02, 0x05, 0x28, 0x02, 0x22, 0x43, 0x29, 0xED, 0x23, 0x90, 0x00, 0x00, 0x90, 0x01, 0x00 }, 33, "Sharp FO-4810"),
    };

    private static readonly T35Model[] ModelsXerox =
    {
        new(new byte[] { 0x00, 0x08, 0x2D, 0x43, 0x57, 0x50, 0x61, 0x75, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01, 0x1A, 0x02, 0x02, 0x10, 0x01, 0x82, 0x01, 0x30, 0x34 }, 10, "635 Workcenter"),
    };

    private static readonly T35Model[] ModelsXeroxDA =
    {
        new(new byte[] { 0x00, 0x00, 0xC0, 0x00 }, 4, "Workcentre Pro 580"),
    };

    private static readonly T35Model[] ModelsLexmark =
    {
        new(new byte[] { 0x00, 0x80, 0xA0, 0x00 }, 4, "X4270"),
    };

    private static readonly T35Model[] ModelsJetFax =
    {
        new(new byte[] { 0x01, 0x00, 0x45, 0x00, 0x0D, 0x7F }, 6, "M910e"),
    };

    private static readonly T35Model[] ModelsPitneyBowes =
    {
        new(new byte[] { 0x79, 0x91, 0xB1, 0xB8, 0x7A, 0xD8 }, 6, "9550"),
    };

    private static readonly T35Model[] ModelsDialogic =
    {
        new(new byte[] { 0x56, 0x8B, 0x06, 0x55, 0x00, 0x15, 0x00, 0x00 }, 8, "VFX/40ESC"),
    };

    private static readonly T35Model[] ModelsMuratec45 =
    {
        new(new byte[] { 0xF4, 0x91, 0xFF, 0xFF, 0xFF, 0x42, 0x2A, 0xBC, 0x01, 0x57 }, 10, "M4700"),
    };

    private static readonly T35Model[] ModelsMuratec48 =
    {
        new(new byte[] { 0x53, 0x53, 0x61 }, 3, "M620"),
    };

    private static readonly T35Vendor[] Vendors00 =
    {
        new(new byte[] { 0x00, 0x00 }, 2, "Unknown - indeterminate", true, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x01 }, 2, "Anritsu", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x02 }, 2, "Nippon Telephone", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x05 }, 2, "Mitsuba Electric", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x06 }, 2, "Master Net", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x09 }, 2, "Xerox/Toshiba", true, ModelsXerox),
        new(new byte[] { 0x00, 0x0A }, 2, "Kokusai", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x0D }, 2, "Logic System International", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x0E }, 2, "Panasonic", false, ModelsPanasonic0E),
        new(new byte[] { 0x00, 0x11 }, 2, "Canon", false, ModelsCanon),
        new(new byte[] { 0x00, 0x15 }, 2, "Toyotsushen Machinery", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x16 }, 2, "System House Mind", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x19 }, 2, "Xerox", true, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x1D }, 2, "Hitachi Software", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x21 }, 2, "OKI Electric/Lanier", true, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x25 }, 2, "Ricoh", true, ModelsRicoh),
        new(new byte[] { 0x00, 0x26 }, 2, "Konica", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x29 }, 2, "Japan Wireless", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x2D }, 2, "Sony", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x31 }, 2, "Sharp/Olivetti", false, ModelsSharp),
        new(new byte[] { 0x00, 0x35 }, 2, "Kogyu", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x36 }, 2, "Japan Telecom", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x3D }, 2, "IBM Japan", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x39 }, 2, "Panasonic", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x41 }, 2, "Swasaki Communication", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x45 }, 2, "Muratec", false, ModelsMuratec45),
        new(new byte[] { 0x00, 0x46 }, 2, "Pheonix", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x48 }, 2, "Muratec", false, ModelsMuratec48),
        new(new byte[] { 0x00, 0x49 }, 2, "Japan Electric", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x4D }, 2, "Okura Electric", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x51 }, 2, "Sanyo", false, ModelsSanyo),
        new(new byte[] { 0x00, 0x55 }, 2, "Unknown - Japan 55", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x56 }, 2, "Brother", false, ModelsBrother),
        new(new byte[] { 0x00, 0x59 }, 2, "Fujitsu", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x5D }, 2, "Kuoni", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x61 }, 2, "Casio", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x65 }, 2, "Tateishi Electric", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x66 }, 2, "Utax/Mita", true, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x69 }, 2, "Hitachi Production", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x6D }, 2, "Hitachi Telecom", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x71 }, 2, "Tamura Electric Works", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x75 }, 2, "Tokyo Electric Corp.", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x76 }, 2, "Advance", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x79 }, 2, "Panasonic", false, ModelsPanasonic79),
        new(new byte[] { 0x00, 0x7D }, 2, "Seiko", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x08, 0x00 }, 2, "Daiko", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x10, 0x00 }, 2, "Funai Electric", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x20, 0x00 }, 2, "Eagle System", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x30, 0x00 }, 2, "Nippon Business Systems", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x40, 0x00 }, 2, "Comtron", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x48, 0x00 }, 2, "Cosmo Consulting", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x50, 0x00 }, 2, "Orion Electric", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x60, 0x00 }, 2, "Nagano Nippon", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x70, 0x00 }, 2, "Kyocera", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x80, 0x00 }, 2, "Kanda Networks", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x88, 0x00 }, 2, "Soft Front", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x90, 0x00 }, 2, "Arctic", false, Array.Empty<T35Model>()),
        new(new byte[] { 0xA0, 0x00 }, 2, "Nakushima", false, Array.Empty<T35Model>()),
        new(new byte[] { 0xB0, 0x00 }, 2, "Minolta", false, Array.Empty<T35Model>()),
        new(new byte[] { 0xC0, 0x00 }, 2, "Tohoku Pioneer", false, Array.Empty<T35Model>()),
        new(new byte[] { 0xD0, 0x00 }, 2, "USC", false, Array.Empty<T35Model>()),
        new(new byte[] { 0xE0, 0x00 }, 2, "Hiboshi", false, Array.Empty<T35Model>()),
        new(new byte[] { 0xF0, 0x00 }, 2, "Sumitomo Electric", false, Array.Empty<T35Model>()),
    };

    private static readonly T35Vendor[] Vendors20 =
    {
        new(new byte[] { 0x09 }, 1, "ITK Institut für Telekommunikation GmbH & Co KG", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x11 }, 1, "Dr. Neuhaus Mikroelektronik", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x21 }, 1, "ITO Communication", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x31 }, 1, "mbp Kommunikationssysteme GmbH", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x41 }, 1, "Siemens", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x42 }, 1, "Deutsche Telekom AG", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x51 }, 1, "mps Software", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x61 }, 1, "Hauni Elektronik", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x71 }, 1, "Digitronic computersysteme gmbh", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x81, 0x00 }, 2, "Innovaphone GmbH", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x81, 0x40 }, 2, "TEDAS Gesellschaft für Telekommunikations-, Daten- und Audiosysteme mbH", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x81, 0x80 }, 2, "AVM Audiovisuelles Marketing und Computersysteme GmbH", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x81, 0xC0 }, 2, "EICON Technology Research GmbH", false, Array.Empty<T35Model>()),
        new(new byte[] { 0xB1 }, 1, "Schneider Rundfunkwerke AG", false, Array.Empty<T35Model>()),
        new(new byte[] { 0xC2 }, 1, "Deutsche Telekom AG", false, Array.Empty<T35Model>()),
        new(new byte[] { 0xD1 }, 1, "Ferrari electronik GmbH", false, Array.Empty<T35Model>()),
        new(new byte[] { 0xF1 }, 1, "DeTeWe - Deutsche Telephonwerke AG & Co", false, Array.Empty<T35Model>()),
        new(new byte[] { 0xFF }, 1, "Germany Regional Code", false, Array.Empty<T35Model>()),
    };

    private static readonly T35Vendor[] Vendors61 =
    {
        new(new byte[] { 0x00, 0x7A }, 2, "Xerox", false, Array.Empty<T35Model>()),
    };

    private static readonly T35Vendor[] Vendors64 =
    {
        new(new byte[] { 0x00, 0x00 }, 2, "Unknown - China 00 00", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x01, 0x00 }, 2, "Unknown - China 01 00", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x01, 0x01 }, 2, "Unknown - China 01 01", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x01, 0x02 }, 2, "Unknown - China 01 02", false, Array.Empty<T35Model>()),
    };

    private static readonly T35Vendor[] Vendors86 =
    {
        new(new byte[] { 0x00, 0x02 }, 2, "Unknown - Korea 02", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x06 }, 2, "Unknown - Korea 06", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x08 }, 2, "Unknown - Korea 08", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x0A }, 2, "Unknown - Korea 0A", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x0E }, 2, "Unknown - Korea 0E", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x10 }, 2, "Samsung", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x11 }, 2, "Unknown - Korea 11", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x16 }, 2, "Samsung", false, ModelsSamsung16),
        new(new byte[] { 0x00, 0x1A }, 2, "Unknown - Korea 1A", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x40 }, 2, "Unknown - Korea 40", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x48 }, 2, "Unknown - Korea 48", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x52 }, 2, "Unknown - Korea 52", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x5A }, 2, "Samsung", false, ModelsSamsung5A),
        new(new byte[] { 0x00, 0x5E }, 2, "Unknown - Korea 5E", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x66 }, 2, "Unknown - Korea 66", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x6E }, 2, "Unknown - Korea 6E", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x82 }, 2, "Unknown - Korea 82", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x88 }, 2, "Unknown - Korea 88", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x8A }, 2, "Unknown - Korea 8A", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x8C }, 2, "Samsung", false, ModelsSamsung8C),
        new(new byte[] { 0x00, 0x92 }, 2, "Unknown - Korea 92", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x98 }, 2, "Samsung", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0xA2 }, 2, "Samsung", false, ModelsSamsungA2),
        new(new byte[] { 0x00, 0xA4 }, 2, "Unknown - Korea A4", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0xC2 }, 2, "Samsung", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0xC9 }, 2, "Unknown - Korea C9", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0xCC }, 2, "Unknown - Korea CC", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0xD2 }, 2, "Unknown - Korea D2", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0xDA }, 2, "Xerox", false, ModelsXeroxDA),
        new(new byte[] { 0x00, 0xE2 }, 2, "Unknown - Korea E2", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0xEC }, 2, "Unknown - Korea EC", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0xEE }, 2, "Unknown - Korea EE", false, Array.Empty<T35Model>()),
    };

    private static readonly T35Vendor[] VendorsAD =
    {
        new(new byte[] { 0x00, 0x00 }, 2, "Pitney Bowes", false, ModelsPitneyBowes),
        new(new byte[] { 0x00, 0x0C }, 2, "Dialogic", false, ModelsDialogic),
        new(new byte[] { 0x00, 0x15 }, 2, "Lexmark", false, ModelsLexmark),
        new(new byte[] { 0x00, 0x16 }, 2, "JetFax", false, ModelsJetFax),
        new(new byte[] { 0x00, 0x24 }, 2, "Octel", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x36 }, 2, "HP", false, ModelsHP),
        new(new byte[] { 0x00, 0x42 }, 2, "FaxTalk", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x44 }, 2, null, true, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x46 }, 2, "BrookTrout", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x51 }, 2, "Telogy Networks", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x55 }, 2, "HylaFAX", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x5C }, 2, "IBM", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x98 }, 2, "Unknown - USA 98", true, Array.Empty<T35Model>()),
    };

    private static readonly T35Vendor[] VendorsB4 =
    {
        new(new byte[] { 0x00, 0xB0 }, 2, "DCE", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0xB1 }, 2, "Hasler", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0xB2 }, 2, "Interquad", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0xB3 }, 2, "Comwave", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0xB4 }, 2, "Iconographic", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0xB5 }, 2, "Wordcraft", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0xB6 }, 2, "Acorn", false, Array.Empty<T35Model>()),
    };

    private static readonly T35Vendor[] VendorsB5 =
    {
        new(new byte[] { 0x00, 0x01 }, 2, "Picturetel", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x20 }, 2, "Conexant", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x22 }, 2, "Comsat", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x24 }, 2, "Octel", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x26 }, 2, "ROLM", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x28 }, 2, "SOFNET", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x29 }, 2, "TIA TR-29 Committee", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x2A }, 2, "STF Tech", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x2C }, 2, "HKB", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x2E }, 2, "Delrina", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x30 }, 2, "Dialogic", false, ModelsDialogic),
        new(new byte[] { 0x00, 0x32 }, 2, "Applied Synergy", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x34 }, 2, "Syncro Development", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x36 }, 2, "Genoa", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x38 }, 2, "Texas Instruments", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x3A }, 2, "IBM", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x3C }, 2, "ViaSat", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x3E }, 2, "Ericsson", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x42 }, 2, "Bogosian", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x44 }, 2, "Adobe", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x46 }, 2, "Fremont Communications", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x48 }, 2, "Hayes", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x4A }, 2, "Lucent", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x4C }, 2, "Data Race", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x4E }, 2, "TRW", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x52 }, 2, "Audiofax", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x54 }, 2, "Computer Automation", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x56 }, 2, "Serca", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x58 }, 2, "Octocom", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x5C }, 2, "Power Solutions", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x5A }, 2, "Digital Sound", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x5E }, 2, "Pacific Data", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x60 }, 2, "Commetrex", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x62 }, 2, "BrookTrout", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x64 }, 2, "Gammalink", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x66 }, 2, "Castelle", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x68 }, 2, "Hybrid Fax", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x6A }, 2, "Omnifax", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x6C }, 2, "HP", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x6E }, 2, "Microsoft", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x72 }, 2, "Speaking Devices", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x74 }, 2, "Compaq", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x76 }, 2, "Microsoft", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x78 }, 2, "Cylink", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x7A }, 2, "Pitney Bowes", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x7C }, 2, "Digiboard", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x7E }, 2, "Codex", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x82 }, 2, "Wang Labs", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x84 }, 2, "Netexpress Communications", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x86 }, 2, "Cable-Sat", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x88 }, 2, "MFPA", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x8A }, 2, "Telogy Networks", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x8E }, 2, "Telecom Multimedia Systems", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x8C }, 2, "AT&T", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x92 }, 2, "Nuera", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x94 }, 2, "K56flex", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x96 }, 2, "MiBridge", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x98 }, 2, "Xerox", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x9A }, 2, "Fujitsu", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x9B }, 2, "Fujitsu", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x9C }, 2, "Natural Microsystems", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0x9E }, 2, "CopyTele", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0xA2 }, 2, "Murata", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0xA4 }, 2, "Lanier", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0xA6 }, 2, "Qualcomm", false, Array.Empty<T35Model>()),
        new(new byte[] { 0x00, 0xAA }, 2, "HylaFAX", false, Array.Empty<T35Model>()),
    };

    private static readonly T35Vendor[] VendorsBC =
    {
        new(new byte[] { 0x53, 0x01 }, 2, "Minolta", false, Array.Empty<T35Model>()),
    };

    private static readonly T35Country[] Countries =
    {
        new("Japan", Vendors00),
        new("Albania", Array.Empty<T35Vendor>()),
        new("Algeria", Array.Empty<T35Vendor>()),
        new("American Samoa", Array.Empty<T35Vendor>()),
        new("Germany", Array.Empty<T35Vendor>()),
        new("Anguilla", Array.Empty<T35Vendor>()),
        new("Antigua and Barbuda", Array.Empty<T35Vendor>()),
        new("Argentina", Array.Empty<T35Vendor>()),
        new("Ascension (see S. Helena)", Array.Empty<T35Vendor>()),
        new("Australia", Array.Empty<T35Vendor>()),
        new("Austria", Array.Empty<T35Vendor>()),
        new("Bahamas", Array.Empty<T35Vendor>()),
        new("Bahrain", Array.Empty<T35Vendor>()),
        new("Bangladesh", Array.Empty<T35Vendor>()),
        new("Barbados", Array.Empty<T35Vendor>()),
        new("Belgium", Array.Empty<T35Vendor>()),
        new("Belize", Array.Empty<T35Vendor>()),
        new("Benin (Republic of)", Array.Empty<T35Vendor>()),
        new("Bermudas", Array.Empty<T35Vendor>()),
        new("Bhutan (Kingdom of)", Array.Empty<T35Vendor>()),
        new("Bolivia", Array.Empty<T35Vendor>()),
        new("Botswana", Array.Empty<T35Vendor>()),
        new("Brazil", Array.Empty<T35Vendor>()),
        new("British Antarctic Territory", Array.Empty<T35Vendor>()),
        new("British Indian Ocean Territory", Array.Empty<T35Vendor>()),
        new("British Virgin Islands", Array.Empty<T35Vendor>()),
        new("Brunei Darussalam", Array.Empty<T35Vendor>()),
        new("Bulgaria", Array.Empty<T35Vendor>()),
        new("Myanmar (Union of)", Array.Empty<T35Vendor>()),
        new("Burundi", Array.Empty<T35Vendor>()),
        new("Byelorussia", Array.Empty<T35Vendor>()),
        new("Cameroon", Array.Empty<T35Vendor>()),
        new("Canada", Vendors20),
        new("Cape Verde", Array.Empty<T35Vendor>()),
        new("Cayman Islands", Array.Empty<T35Vendor>()),
        new("Central African Republic", Array.Empty<T35Vendor>()),
        new("Chad", Array.Empty<T35Vendor>()),
        new("Chile", Array.Empty<T35Vendor>()),
        new("China", Array.Empty<T35Vendor>()),
        new("Colombia", Array.Empty<T35Vendor>()),
        new("Comoros", Array.Empty<T35Vendor>()),
        new("Congo", Array.Empty<T35Vendor>()),
        new("Cook Islands", Array.Empty<T35Vendor>()),
        new("Costa Rica", Array.Empty<T35Vendor>()),
        new("Cuba", Array.Empty<T35Vendor>()),
        new("Cyprus", Array.Empty<T35Vendor>()),
        new("Czech and Slovak Federal Republic", Array.Empty<T35Vendor>()),
        new("Cambodia", Array.Empty<T35Vendor>()),
        new("Democratic People's Republic of Korea", Array.Empty<T35Vendor>()),
        new("Denmark", Array.Empty<T35Vendor>()),
        new("Djibouti", Array.Empty<T35Vendor>()),
        new("Dominican Republic", Array.Empty<T35Vendor>()),
        new("Dominica", Array.Empty<T35Vendor>()),
        new("Ecuador", Array.Empty<T35Vendor>()),
        new("Egypt", Array.Empty<T35Vendor>()),
        new("El Salvador", Array.Empty<T35Vendor>()),
        new("Equatorial Guinea", Array.Empty<T35Vendor>()),
        new("Ethiopia", Array.Empty<T35Vendor>()),
        new("Falkland Islands", Array.Empty<T35Vendor>()),
        new("Fiji", Array.Empty<T35Vendor>()),
        new("Finland", Array.Empty<T35Vendor>()),
        new("France", Array.Empty<T35Vendor>()),
        new("French Polynesia", Array.Empty<T35Vendor>()),
        new("French Southern and Antarctic Lands", Array.Empty<T35Vendor>()),
        new("Gabon", Array.Empty<T35Vendor>()),
        new("Gambia", Array.Empty<T35Vendor>()),
        new("Germany (Federal Republic of)", Array.Empty<T35Vendor>()),
        new("Angola", Array.Empty<T35Vendor>()),
        new("Ghana", Array.Empty<T35Vendor>()),
        new("Gibraltar", Array.Empty<T35Vendor>()),
        new("Greece", Array.Empty<T35Vendor>()),
        new("Grenada", Array.Empty<T35Vendor>()),
        new("Guam", Array.Empty<T35Vendor>()),
        new("Guatemala", Array.Empty<T35Vendor>()),
        new("Guernsey", Array.Empty<T35Vendor>()),
        new("Guinea", Array.Empty<T35Vendor>()),
        new("Guinea-Bissau", Array.Empty<T35Vendor>()),
        new("Guayana", Array.Empty<T35Vendor>()),
        new("Haiti", Array.Empty<T35Vendor>()),
        new("Honduras", Array.Empty<T35Vendor>()),
        new("Hong Kong", Array.Empty<T35Vendor>()),
        new("Hungary (Republic of)", Array.Empty<T35Vendor>()),
        new("Iceland", Array.Empty<T35Vendor>()),
        new("India", Array.Empty<T35Vendor>()),
        new("Indonesia", Array.Empty<T35Vendor>()),
        new("Iran (Islamic Republic of)", Array.Empty<T35Vendor>()),
        new("Iraq", Array.Empty<T35Vendor>()),
        new("Ireland", Array.Empty<T35Vendor>()),
        new("Israel", Array.Empty<T35Vendor>()),
        new("Italy", Array.Empty<T35Vendor>()),
        new("Cote d'Ivoire", Array.Empty<T35Vendor>()),
        new("Jamaica", Array.Empty<T35Vendor>()),
        new("Afghanistan", Array.Empty<T35Vendor>()),
        new("Jersey", Array.Empty<T35Vendor>()),
        new("Jordan", Array.Empty<T35Vendor>()),
        new("Kenya", Array.Empty<T35Vendor>()),
        new("Kiribati", Array.Empty<T35Vendor>()),
        new("Korea (Republic of)", Vendors61),
        new("Kuwait", Array.Empty<T35Vendor>()),
        new("Lao (People's Democratic Republic)", Array.Empty<T35Vendor>()),
        new("Lebanon", Vendors64),
        new("Lesotho", Array.Empty<T35Vendor>()),
        new("Liberia", Array.Empty<T35Vendor>()),
        new("Libya", Array.Empty<T35Vendor>()),
        new("Liechtenstein", Array.Empty<T35Vendor>()),
        new("Luxembourg", Array.Empty<T35Vendor>()),
        new("Macau", Array.Empty<T35Vendor>()),
        new("Madagascar", Array.Empty<T35Vendor>()),
        new("Malaysia", Array.Empty<T35Vendor>()),
        new("Malawi", Array.Empty<T35Vendor>()),
        new("Maldives", Array.Empty<T35Vendor>()),
        new("Mali", Array.Empty<T35Vendor>()),
        new("Malta", Array.Empty<T35Vendor>()),
        new("Mauritania", Array.Empty<T35Vendor>()),
        new("Mauritius", Array.Empty<T35Vendor>()),
        new("Mexico", Array.Empty<T35Vendor>()),
        new("Monaco", Array.Empty<T35Vendor>()),
        new("Mongolia", Array.Empty<T35Vendor>()),
        new("Montserrat", Array.Empty<T35Vendor>()),
        new("Morocco", Array.Empty<T35Vendor>()),
        new("Mozambique", Array.Empty<T35Vendor>()),
        new("Nauru", Array.Empty<T35Vendor>()),
        new("Nepal", Array.Empty<T35Vendor>()),
        new("Netherlands", Array.Empty<T35Vendor>()),
        new("Netherlands Antilles", Array.Empty<T35Vendor>()),
        new("New Caledonia", Array.Empty<T35Vendor>()),
        new("New Zealand", Array.Empty<T35Vendor>()),
        new("Nicaragua", Array.Empty<T35Vendor>()),
        new("Niger", Array.Empty<T35Vendor>()),
        new("Nigeria", Array.Empty<T35Vendor>()),
        new("Norway", Array.Empty<T35Vendor>()),
        new("Oman", Array.Empty<T35Vendor>()),
        new("Pakistan", Array.Empty<T35Vendor>()),
        new("Panama", Array.Empty<T35Vendor>()),
        new("Papua New Guinea", Vendors86),
        new("Paraguay", Array.Empty<T35Vendor>()),
        new("Peru", Array.Empty<T35Vendor>()),
        new("Philippines", Array.Empty<T35Vendor>()),
        new("Poland (Republic of)", Array.Empty<T35Vendor>()),
        new("Portugal", Array.Empty<T35Vendor>()),
        new("Puerto Rico", Array.Empty<T35Vendor>()),
        new("Qatar", Array.Empty<T35Vendor>()),
        new("Romania", Array.Empty<T35Vendor>()),
        new("Rwanda", Array.Empty<T35Vendor>()),
        new("Saint Kitts and Nevis", Array.Empty<T35Vendor>()),
        new("Saint Croix", Array.Empty<T35Vendor>()),
        new("Saint Helena and Ascension", Array.Empty<T35Vendor>()),
        new("Saint Lucia", Array.Empty<T35Vendor>()),
        new("San Marino", Array.Empty<T35Vendor>()),
        new("Saint Thomas", Array.Empty<T35Vendor>()),
        new("Sao Tome and Principe", Array.Empty<T35Vendor>()),
        new("Saint Vincent and the Grenadines", Array.Empty<T35Vendor>()),
        new("Saudi Arabia", Array.Empty<T35Vendor>()),
        new("Senegal", Array.Empty<T35Vendor>()),
        new("Seychelles", Array.Empty<T35Vendor>()),
        new("Sierra Leone", Array.Empty<T35Vendor>()),
        new("Singapore", Array.Empty<T35Vendor>()),
        new("Solomon Islands", Array.Empty<T35Vendor>()),
        new("Somalia", Array.Empty<T35Vendor>()),
        new("South Africa", Array.Empty<T35Vendor>()),
        new("Spain", Array.Empty<T35Vendor>()),
        new("Sri Lanka", Array.Empty<T35Vendor>()),
        new("Sudan", Array.Empty<T35Vendor>()),
        new("Suriname", Array.Empty<T35Vendor>()),
        new("Swaziland", Array.Empty<T35Vendor>()),
        new("Sweden", Array.Empty<T35Vendor>()),
        new("Switzerland", Array.Empty<T35Vendor>()),
        new("Syria", Array.Empty<T35Vendor>()),
        new("Tanzania", Array.Empty<T35Vendor>()),
        new("Thailand", Array.Empty<T35Vendor>()),
        new("Togo", Array.Empty<T35Vendor>()),
        new("Tonga", Array.Empty<T35Vendor>()),
        new("Trinidad and Tobago", Array.Empty<T35Vendor>()),
        new("Tunisia", VendorsAD),
        new("Turkey", Array.Empty<T35Vendor>()),
        new("Turks and Caicos Islands", Array.Empty<T35Vendor>()),
        new("Tuvalu", Array.Empty<T35Vendor>()),
        new("Uganda", Array.Empty<T35Vendor>()),
        new("Ukraine", Array.Empty<T35Vendor>()),
        new("United Arab Emirates", Array.Empty<T35Vendor>()),
        new("United Kingdom", VendorsB4),
        new("United States", VendorsB5),
        new("Burkina Faso", Array.Empty<T35Vendor>()),
        new("Uruguay", Array.Empty<T35Vendor>()),
        new("U.S.S.R.", Array.Empty<T35Vendor>()),
        new("Vanuatu", Array.Empty<T35Vendor>()),
        new("Vatican City State", Array.Empty<T35Vendor>()),
        new("Venezuela", Array.Empty<T35Vendor>()),
        new("Vietnam", VendorsBC),
        new("Wallis and Futuna", Array.Empty<T35Vendor>()),
        new("Western Samoa", Array.Empty<T35Vendor>()),
        new("Yemen (Republic of)", Array.Empty<T35Vendor>()),
        new("Yemen (Republic of)", Array.Empty<T35Vendor>()),
        new("Yugoslavia", Array.Empty<T35Vendor>()),
        new("Zaire", Array.Empty<T35Vendor>()),
        new("Zambia", Array.Empty<T35Vendor>()),
        new("Zimbabwe", Array.Empty<T35Vendor>()),
        new("Slovakia", Array.Empty<T35Vendor>()),
        new("Slovenia", Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new("Lithuania", Array.Empty<T35Vendor>()),
        new("Latvia", Array.Empty<T35Vendor>()),
        new("Estonia", Array.Empty<T35Vendor>()),
        new("US Virgin Islands", Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new(null, Array.Empty<T35Vendor>()),
        new("(Universal)", Array.Empty<T35Vendor>()),
        new("Taiwan", Array.Empty<T35Vendor>()),
    };

    public static int t35_real_country_code(int countryCode, int countryCodeExtension) {
        _ = countryCodeExtension;
        if ((uint)countryCode > 0xFFu || countryCode == 0xFF)
            return -1;

        countryCode = countryCode switch {
            0x20 or 0x2D or 0x64 or 0x86 or 0xAD or 0xBC => ReverseBits((byte)countryCode),
            _ => countryCode
        };

        if (countryCode < Countries.Length && Countries[countryCode].Name is not null)
            return countryCode;

        int reversed = ReverseBits((byte)countryCode);
        return reversed < Countries.Length && Countries[reversed].Name is not null ? reversed : -1;
    }

    public static string? t35_real_country_code_to_str(int countryCode, int countryCodeExtension) {
        int real = t35_real_country_code(countryCode, countryCodeExtension);
        return real >= 0 ? Countries[real].Name : null;
    }

    public static string? t35_country_code_to_str(int countryCode, int countryCodeExtension) {
        _ = countryCodeExtension;
        if ((uint)countryCode >= (uint)Countries.Length || countryCode == 0xFF)
            return null;
        return Countries[countryCode].Name;
    }

    public static string? t35_vendor_to_str(ReadOnlySpan<byte> message) {
        return FindVendor(message)?.Name;
    }

    public static string? t35_vendor_to_str(byte[] message, int length) {
        ArgumentNullException.ThrowIfNull(message);
        if (length < 0 || length > message.Length)
            throw new ArgumentOutOfRangeException(nameof(length));
        return t35_vendor_to_str(message.AsSpan(0, length));
    }

    public static bool t35_decode(
        ReadOnlySpan<byte> message,
        out string? country,
        out string? vendor,
        out string? model) {
        country = message.Length > 0
            ? t35_real_country_code_to_str(message[0], message.Length > 1 ? message[1] : 0)
            : null;
        vendor = null;
        model = null;

        T35Vendor? matchedVendor = FindVendor(message);
        if (matchedVendor is null)
            return false;

        vendor = matchedVendor.Name;
        int modelOffset = 1 + matchedVendor.MatchLength;
        foreach (T35Model candidate in matchedVendor.Models) {
            if (message.Length != modelOffset + candidate.MatchLength)
                continue;
            if (message.Slice(modelOffset, candidate.MatchLength).SequenceEqual(candidate.Id.AsSpan(0, candidate.MatchLength))) {
                model = candidate.Name;
                break;
            }
        }
        return true;
    }

    public static T35DecodedIdentity Decode(ReadOnlySpan<byte> message) {
        t35_decode(message, out string? country, out string? vendor, out string? model);
        return new T35DecodedIdentity(country, vendor, model);
    }

    private static T35Vendor? FindVendor(ReadOnlySpan<byte> message) {
        if (message.Length < 2 || message[0] == 0xFF)
            return null;

        int realCountry = t35_real_country_code(message[0], message[1]);
        if (realCountry < 0 || realCountry >= Countries.Length)
            return null;

        foreach (T35Vendor candidate in Countries[realCountry].Vendors) {
            if (message.Length < 1 + candidate.MatchLength)
                continue;
            if (message.Slice(1, candidate.MatchLength).SequenceEqual(candidate.Id.AsSpan(0, candidate.MatchLength)))
                return candidate;
        }
        return null;
    }

    private static int ReverseBits(byte value) {
        uint v = value;
        v = ((v & 0x55u) << 1) | ((v >> 1) & 0x55u);
        v = ((v & 0x33u) << 2) | ((v >> 2) & 0x33u);
        v = ((v & 0x0Fu) << 4) | ((v >> 4) & 0x0Fu);
        return (int)v;
    }
}
