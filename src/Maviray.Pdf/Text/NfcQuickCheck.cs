namespace Maviray.Pdf.Text;

/// <summary>
/// The value of the Unicode property <c>NFC_Quick_Check</c> (UAX #15), which tells the normalizer whether a code
/// point can stay as it is.
/// </summary>
internal enum NfcQuickCheck : byte
{
    /// <summary>The code point never changes in NFC, whatever precedes it.</summary>
    Yes = 0,

    /// <summary>The code point may compose with the character before it.</summary>
    Maybe = 1,

    /// <summary>The code point never occurs in NFC.</summary>
    No = 2,
}
