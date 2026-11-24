namespace Maviray.Pdf.Elements;

/// <summary>What a run of a Text element shows (spec L-07).</summary>
internal enum TextRunKind
{
    /// <summary>Its own text.</summary>
    Text,

    /// <summary>The number of the page it is drawn on.</summary>
    PageNumber,

    /// <summary>The document's page count.</summary>
    PageCount,
}
