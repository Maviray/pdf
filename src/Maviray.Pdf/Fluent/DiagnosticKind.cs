namespace Maviray.Pdf;

/// <summary>What a <see cref="Diagnostic"/> reports.</summary>
public enum DiagnosticKind
{
    /// <summary>A character the font has no glyph for, which draws as the font's empty box (<c>.notdef</c>).</summary>
    MissingGlyph,

    /// <summary>A requested weight or style the font family doesn't have, so its nearest face is used instead.</summary>
    FontFaceSubstituted,
}
