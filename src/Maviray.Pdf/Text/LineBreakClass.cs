namespace Maviray.Pdf.Text;

/// <summary>
/// The line-break classes of UAX #14 for Unicode 16.0, in alphabetical order. The generated table stores each code
/// point's class after LB1, so AI, SG, XX, SA and CJ never occur in it.
/// </summary>
internal enum LineBreakClass : byte
{
    /// <summary>Ambiguous (alphabetic or ideographic).</summary>
    AI,

    /// <summary>Aksara.</summary>
    AK,

    /// <summary>Alphabetic.</summary>
    AL,

    /// <summary>Aksara pre-base.</summary>
    AP,

    /// <summary>Aksara start.</summary>
    AS,

    /// <summary>Break opportunity before and after.</summary>
    B2,

    /// <summary>Break after.</summary>
    BA,

    /// <summary>Break before.</summary>
    BB,

    /// <summary>Mandatory break.</summary>
    BK,

    /// <summary>Contingent break opportunity.</summary>
    CB,

    /// <summary>Conditional Japanese starter.</summary>
    CJ,

    /// <summary>Close punctuation.</summary>
    CL,

    /// <summary>Combining mark.</summary>
    CM,

    /// <summary>Close parenthesis.</summary>
    CP,

    /// <summary>Carriage return.</summary>
    CR,

    /// <summary>Emoji base.</summary>
    EB,

    /// <summary>Emoji modifier.</summary>
    EM,

    /// <summary>Exclamation or interrogation.</summary>
    EX,

    /// <summary>Non-breaking ("glue").</summary>
    GL,

    /// <summary>Hangul LV syllable.</summary>
    H2,

    /// <summary>Hangul LVT syllable.</summary>
    H3,

    /// <summary>Hebrew letter.</summary>
    HL,

    /// <summary>Hyphen.</summary>
    HY,

    /// <summary>Ideographic.</summary>
    ID,

    /// <summary>Inseparable.</summary>
    IN,

    /// <summary>Infix numeric separator.</summary>
    IS,

    /// <summary>Hangul L jamo.</summary>
    JL,

    /// <summary>Hangul T jamo.</summary>
    JT,

    /// <summary>Hangul V jamo.</summary>
    JV,

    /// <summary>Line feed.</summary>
    LF,

    /// <summary>Next line.</summary>
    NL,

    /// <summary>Nonstarter.</summary>
    NS,

    /// <summary>Numeric.</summary>
    NU,

    /// <summary>Open punctuation.</summary>
    OP,

    /// <summary>Postfix numeric.</summary>
    PO,

    /// <summary>Prefix numeric.</summary>
    PR,

    /// <summary>Quotation.</summary>
    QU,

    /// <summary>Regional indicator.</summary>
    RI,

    /// <summary>Complex context dependent (South East Asian).</summary>
    SA,

    /// <summary>Surrogate.</summary>
    SG,

    /// <summary>Space.</summary>
    SP,

    /// <summary>Symbols allowing break after.</summary>
    SY,

    /// <summary>Virama final.</summary>
    VF,

    /// <summary>Virama.</summary>
    VI,

    /// <summary>Word joiner.</summary>
    WJ,

    /// <summary>Unknown.</summary>
    XX,

    /// <summary>Zero width space.</summary>
    ZW,

    /// <summary>Zero width joiner.</summary>
    ZWJ,
}
