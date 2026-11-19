using System.Globalization;

namespace Maviray.Pdf.Writer;

/// <summary>
/// The fonts of one document. A font gets its resource name (<c>/F1</c>, <c>/F2</c>, …) and its object number the first
/// time a page uses it, so pages can refer to it before it is written; <see cref="WriteAll"/> writes each used font's
/// dictionary once, at the end of the document (spec D-06, D-10).
/// </summary>
internal sealed class FontResources
{
    private readonly PdfFileWriter _file;
    private readonly List<(StandardFont Font, PdfName Name, PdfReference Reference)> _fonts = [];

    public FontResources(PdfFileWriter file)
    {
        ArgumentNullException.ThrowIfNull(file);
        _file = file;
    }

    /// <summary>The resource name pages use for <paramref name="font"/>.</summary>
    public PdfName NameOf(StandardFont font) => Entry(font).Name;

    /// <summary>The object that will hold <paramref name="font"/>'s dictionary.</summary>
    public PdfReference ReferenceOf(StandardFont font) => Entry(font).Reference;

    /// <summary>Writes the dictionary of every font that was used.</summary>
    public void WriteAll()
    {
        foreach (var (font, _, reference) in _fonts)
        {
            _file.Write(reference, font.CreateDictionary());
        }
    }

    private (StandardFont Font, PdfName Name, PdfReference Reference) Entry(StandardFont font)
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
