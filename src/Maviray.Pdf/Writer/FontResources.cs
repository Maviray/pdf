using System.Globalization;
using Maviray.Pdf.Fonts;

namespace Maviray.Pdf.Writer;

/// <summary>
/// The fonts of one document. A font gets its resource name (<c>/F1</c>, <c>/F2</c>, …) and its object number the first
/// time a page uses it, so pages can refer to it before it is written; <see cref="WriteAll"/> writes each used font's
/// objects once, at the end of the document (spec D-06, D-10). Standard and embedded fonts share one sequence of names,
/// and each TrueType font is embedded once per document (F-10).
/// </summary>
internal sealed class FontResources
{
    private readonly PdfFileWriter _file;
    private readonly List<(PdfFont Font, PdfName Name, PdfReference Reference)> _fonts = [];
    private readonly Dictionary<TrueTypeFont, EmbeddedFont> _embedded = new(ReferenceEqualityComparer.Instance);

    public FontResources(PdfFileWriter file)
    {
        ArgumentNullException.ThrowIfNull(file);
        _file = file;
    }

    /// <summary>The resource name pages use for <paramref name="font"/>.</summary>
    public PdfName NameOf(PdfFont font) => Entry(font).Name;

    /// <summary>The object that will hold <paramref name="font"/>'s dictionary.</summary>
    public PdfReference ReferenceOf(PdfFont font) => Entry(font).Reference;

    /// <summary>This document's embedded font for <paramref name="font"/>: the same instance for the same font.</summary>
    public EmbeddedFont Embed(TrueTypeFont font)
    {
        ArgumentNullException.ThrowIfNull(font);
        if (!_embedded.TryGetValue(font, out var embedded))
        {
            embedded = new EmbeddedFont(font, this);
            _embedded.Add(font, embedded);
        }

        return embedded;
    }

    /// <summary>Writes the objects of every font that was used.</summary>
    public void WriteAll()
    {
        foreach (var (font, _, reference) in _fonts)
        {
            font.Write(_file, reference);
        }
    }

    private (PdfFont Font, PdfName Name, PdfReference Reference) Entry(PdfFont font)
    {
        ArgumentNullException.ThrowIfNull(font);
        foreach (var entry in _fonts)
        {
            if (ReferenceEquals(entry.Font, font))
            {
                return entry;
            }
        }

        var name = new PdfName(string.Create(CultureInfo.InvariantCulture, $"F{_fonts.Count + 1}"));
        var added = (font, name, _file.Reserve());
        _fonts.Add(added);
        return added;
    }
}
