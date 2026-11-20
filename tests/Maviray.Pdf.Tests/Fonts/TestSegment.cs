namespace Maviray.Pdf.Tests.Fonts;

/// <summary>
/// One segment of a format 4 <c>cmap</c> subtable built by <see cref="TestCmap.Format4(TestSegment[])"/>. With
/// <paramref name="Glyphs"/>, the segment's <c>idRangeOffset</c> points at them in the glyph array;
/// <paramref name="RangeOffset"/> overrides the computed value, to build a broken subtable.
/// </summary>
internal readonly record struct TestSegment(ushort Start, ushort End, short Delta, ushort[]? Glyphs = null, ushort? RangeOffset = null);
