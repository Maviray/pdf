namespace Maviray.Pdf.Fonts;

/// <summary>How a font may be embedded, from its <c>OS/2.fsType</c> (spec F-06).</summary>
internal enum FontEmbedding
{
    /// <summary>As a subset holding only the glyphs a document uses.</summary>
    Subset,

    /// <summary>Whole, because the font's licence forbids subsetting it.</summary>
    Whole,
}
