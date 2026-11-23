using System.Buffers.Binary;
using System.Text;
using Maviray.Pdf.Fonts;

namespace Maviray.Pdf.Writer;

/// <summary>
/// A TrueType font embedded in one document (spec F-07, F-09 to F-11). It hands out one CID per glyph and character,
/// from 1 on first use, so every character copies out as itself, even one the font lacks and draws as
/// <c>.notdef</c>. When the document closes, it writes its objects:
/// <list type="number">
/// <item><description><c>FontFile2</c>, holding the subset or, for a font that may not be subset, the whole file;</description></item>
/// <item><description>the font descriptor;</description></item>
/// <item><description>for a whole font, the <c>CIDToGIDMap</c> stream;</description></item>
/// <item><description>the CIDFont;</description></item>
/// <item><description>ToUnicode;</description></item>
/// <item><description>the Type 0 font, at the reference pages already use.</description></item>
/// </list>
/// Its CIDs belong to its document and are never shared (NFR-04).
/// </summary>
internal sealed class EmbeddedFont : PdfFont
{
    /// <summary>The last CID: a font holds at most 65,535 glyphs, and CID 0 is <c>.notdef</c> (F-07).</summary>
    public const int MaxCid = 65534;

    private readonly Dictionary<(ushort Glyph, int CodePoint), ushort> _cids = [];
    private readonly List<ushort> _glyphs = [0];
    private readonly List<int> _codePoints = [0];
    private bool _written;

    internal EmbeddedFont(TrueTypeFont font, FontResources resources)
    {
        Font = font;
        Resources = resources;
    }

    public TrueTypeFont Font { get; }

    /// <summary>The fonts of the document this font belongs to.</summary>
    public FontResources Resources { get; }

    /// <summary>The CID for <paramref name="glyphId"/> drawn for <paramref name="codePoint"/>; a new pair gets the next one.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The glyph is past the font, or the code point isn't a Unicode scalar
    /// value.</exception>
    /// <exception cref="InvalidOperationException">The font already has 65,534 CIDs, or its objects are written.</exception>
    public ushort Encode(ushort glyphId, int codePoint)
    {
        if (_written)
        {
            throw new InvalidOperationException("The font's objects are already written, because its document is closed.");
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((int)glyphId, Font.GlyphCount, nameof(glyphId));
        if (!Rune.IsValid(codePoint))
        {
            throw new ArgumentOutOfRangeException(nameof(codePoint), codePoint, "A code point must be a Unicode scalar value.");
        }

        if (_cids.TryGetValue((glyphId, codePoint), out var cid))
        {
            return cid;
        }

        if (_glyphs.Count > MaxCid)
        {
            throw new InvalidOperationException("The font already has 65,534 CIDs, the most one embedded font can hold.");
        }

        cid = (ushort)_glyphs.Count;
        _cids.Add((glyphId, codePoint), cid);
        _glyphs.Add(glyphId);
        _codePoints.Add(codePoint);
        return cid;
    }

    /// <summary>True for a CID from 1 to the last one assigned.</summary>
    public bool IsAssigned(ushort cid) => cid >= 1 && cid < _glyphs.Count;

    /// <exception cref="InvalidOperationException">The subset would pass 65,535 glyphs (F-07).</exception>
    /// <exception cref="InvalidDataException">A glyph the subset needs is damaged.</exception>
    public override void Write(PdfFileWriter file, PdfReference reference)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(reference);
        _written = true;

        // Everything that can throw runs before the first object is written.
        var whole = Font.Embedding == FontEmbedding.Whole;
        var program = whole ? Font.FileData : FontSubsetter.Build(Font, _glyphs).Data;
        var name = whole ? EmbeddedFontName.BaseName(Font) : EmbeddedFontName.Tagged(Font, _glyphs);
        var fontFile = file.Reserve();
        var descriptor = FontDescriptor.Create(Font, name, fontFile);
        var widths = CidWidths.Create(Font, _glyphs);
        var toUnicode = ToUnicodeCMap.Build(_codePoints);

        file.Write(fontFile, new PdfStream(new PdfDictionary().Set(PdfName.Length1, new PdfInteger(program.Length)), program));
        var descriptorReference = file.Write(descriptor);
        PdfObject cidToGid = PdfName.Identity;
        if (whole)
        {
            var map = new byte[2 * _glyphs.Count];
            for (var cid = 0; cid < _glyphs.Count; cid++)
            {
                BinaryPrimitives.WriteUInt16BigEndian(map.AsSpan(2 * cid), _glyphs[cid]);
            }

            cidToGid = file.Write(new PdfStream(new PdfDictionary(), map));
        }

        var baseFont = new PdfName(name);
        var cidFont = file.Write(new PdfDictionary()
            .Set(PdfName.Type, PdfName.Font)
            .Set(PdfName.Subtype, PdfName.CIDFontType2)
            .Set(PdfName.BaseFont, baseFont)
            .Set(PdfName.CIDSystemInfo, new PdfDictionary()
                .Set(PdfName.Registry, PdfString.FromText("Adobe"))
                .Set(PdfName.Ordering, PdfString.FromText("Identity"))
                .Set(PdfName.Supplement, new PdfInteger(0)))
            .Set(PdfName.FontDescriptor, descriptorReference)
            .Set(PdfName.W, widths)
            .Set(PdfName.CIDToGIDMap, cidToGid));
        var toUnicodeReference = file.Write(new PdfStream(new PdfDictionary(), toUnicode));

        file.Write(reference, new PdfDictionary()
            .Set(PdfName.Type, PdfName.Font)
            .Set(PdfName.Subtype, PdfName.Type0)
            .Set(PdfName.BaseFont, baseFont)
            .Set(PdfName.Encoding, PdfName.IdentityH)
            .Set(PdfName.DescendantFonts, new PdfArray(cidFont))
            .Set(PdfName.ToUnicode, toUnicodeReference));
    }
}
