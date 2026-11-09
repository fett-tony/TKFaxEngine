/*
 * TKFaxEngine - managed C# port
 *
 * T140Controls.cs
 *
 * ITU-T T.140 presentation protocol code elements.
 */

#nullable enable

namespace TKFaxEngine.Daten.T140;

/// <summary>Receives one complete UTF-8 encoded T.140 code element.</summary>
public delegate void T140ByteSink(ReadOnlySpan<byte> data);

/// <summary>Receives one classified T.140 presentation protocol element.</summary>
public delegate void T140ElementSink(
    T140ElementType elementType,
    ReadOnlySpan<byte> data);

/// <summary>Consumes one validated Unicode scalar; false applies backpressure.</summary>
public delegate bool T140RuneSink(System.Text.Rune rune);

/// <summary>T.140 presentation protocol element classification.</summary>
public enum T140ElementType {
    /// <summary>Initial U+FEFF code signature.</summary>
    SessionSignature,
    /// <summary>Ordinary ISO/IEC 10646 text.</summary>
    Text,
    /// <summary>BEL, U+0007.</summary>
    Alert,
    /// <summary>BS, U+0008.</summary>
    BackSpace,
    /// <summary>LINE SEPARATOR or accepted CR LF.</summary>
    NewLine,
    /// <summary>INT, ESC followed by U+0061.</summary>
    Interrupt,
    /// <summary>ISO-defined UCS subset identification.</summary>
    UcsSubset,
    /// <summary>SOS, function code, parameter string and ST.</summary>
    ApplicationProtocol,
    /// <summary>CSI Ps m select graphic rendition sequence.</summary>
    SelectGraphicRendition,
    /// <summary>U+FFFD missing-text marker from T.140 Addendum 1.</summary>
    MissingText
}

/// <summary>Unicode and ISO/IEC 6429 controls used by ITU-T T.140.</summary>
public static class T140Controls {
    /// <summary>ZERO WIDTH NO-BREAK SPACE used as the session code signature.</summary>
    public const int SessionSignature = 0xFEFF;
    /// <summary>BELL.</summary>
    public const int Bell = 0x0007;
    /// <summary>BACK SPACE.</summary>
    public const int BackSpace = 0x0008;
    /// <summary>CARRIAGE RETURN.</summary>
    public const int CarriageReturn = 0x000D;
    /// <summary>LINE FEED.</summary>
    public const int LineFeed = 0x000A;
    /// <summary>ESCAPE.</summary>
    public const int Escape = 0x001B;
    /// <summary>LINE SEPARATOR, the preferred T.140 new-line code element.</summary>
    public const int LineSeparator = 0x2028;
    /// <summary>PARAGRAPH SEPARATOR.</summary>
    public const int ParagraphSeparator = 0x2029;
    /// <summary>START OF STRING.</summary>
    public const int StartOfString = 0x0098;
    /// <summary>CONTROL SEQUENCE INTRODUCER.</summary>
    public const int ControlSequenceIntroducer = 0x009B;
    /// <summary>STRING TERMINATOR.</summary>
    public const int StringTerminator = 0x009C;
    /// <summary>INT final character following ESC.</summary>
    public const int InterruptFinal = 0x0061;
    /// <summary>SGR final character following CSI and its parameters.</summary>
    public const int SelectGraphicRenditionFinal = 0x006D;
    /// <summary>Replacement character and T.140 missing-text marker.</summary>
    public const int ReplacementCharacter = 0xFFFD;
    /// <summary>Application function code for an unsupported request.</summary>
    public const int UnsupportedRequestFunction = 0x003F;
    /// <summary>Application function code announcing the ENHANCED profile.</summary>
    public const int EnhancedProfileFunction = 0x005F;
    /// <summary>Maximum number of characters in an application parameter string.</summary>
    public const int MaximumApplicationParameterCharacters = 255;
    /// <summary>Maximum accepted number of characters in an SGR parameter sequence.</summary>
    public const int MaximumSgrParameterCharacters = 255;
}
