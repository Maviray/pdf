using Maviray.Pdf.Fonts;

namespace Maviray.Pdf.Layout;

/// <summary>
/// Where elements report diagnostics during the render pass (spec L-14). Each goes to the handler at most once per
/// document: a missing glyph once per font and code point, and a substituted face once per requested family, weight and
/// style. Without a handler they are dropped.
/// </summary>
internal sealed class DiagnosticSink(Action<Diagnostic>? handler)
{
    private readonly HashSet<(TrueTypeFont Font, int CodePoint)> _missing = [];
    private readonly HashSet<(string Family, int Weight, bool Italic)> _substituted = [];

    public void MissingGlyph(TrueTypeFont font, int codePoint, string path, int page, string message)
    {
        if (_missing.Add((font, codePoint)))
        {
            handler?.Invoke(new Diagnostic(DiagnosticKind.MissingGlyph, message, path, page));
        }
    }

    public void Substituted(string family, int weight, bool italic, string path, int page, string message)
    {
        if (_substituted.Add((family.ToUpperInvariant(), weight, italic)))
        {
            handler?.Invoke(new Diagnostic(DiagnosticKind.FontFaceSubstituted, message, path, page));
        }
    }
}
