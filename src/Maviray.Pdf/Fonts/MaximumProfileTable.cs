namespace Maviray.Pdf.Fonts;

/// <summary>The glyph count from <c>maxp</c> (spec F-03).</summary>
internal sealed record MaximumProfileTable(int GlyphCount)
{
    public static MaximumProfileTable Parse(ReadOnlySpan<byte> table)
    {
        var reader = new FontTableReader(table, "maxp");
        var count = reader.UInt16(4);
        if (count == 0)
        {
            throw reader.Error("gives numGlyphs = 0, but every font has at least the .notdef glyph");
        }

        return new MaximumProfileTable(count);
    }
}
