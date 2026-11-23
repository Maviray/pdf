namespace Maviray.Pdf.Tests.Fonts;

/// <summary>
/// One component of a composite glyph built by <see cref="TestFontBuilder"/>. The builder writes the bytes the flags
/// call for, and sets <c>MORE_COMPONENTS</c> (<c>0x0020</c>) on every record but the last.
/// </summary>
internal readonly record struct TestComponent(ushort GlyphId, ushort Flags, short Arg1 = 0, short Arg2 = 0);
