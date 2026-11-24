namespace Maviray.Pdf.Text;

/// <summary>A run of text in one style (spec T-14).</summary>
internal sealed class TextSpan
{
    public TextSpan(string text, SpanStyle style)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(style);
        Text = text;
        Style = style;
    }

    /// <summary>The span's text as given; preparation cleans and normalizes it (T-03).</summary>
    public string Text { get; }

    public SpanStyle Style { get; }
}
