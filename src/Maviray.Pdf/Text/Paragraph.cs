namespace Maviray.Pdf.Text;

/// <summary>A list of spans and an alignment (spec T-14). It keeps its own copy of the spans, so it is immutable.</summary>
internal sealed class Paragraph
{
    public Paragraph(IEnumerable<TextSpan> spans, TextAlignment alignment = TextAlignment.Left)
    {
        ArgumentNullException.ThrowIfNull(spans);
        if (!Enum.IsDefined(alignment))
        {
            throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "The alignment must be Left, Center or Right.");
        }

        var copy = spans.ToArray();
        for (var i = 0; i < copy.Length; i++)
        {
            if (copy[i] is null)
            {
                throw new ArgumentException($"Span {i} is null.", nameof(spans));
            }
        }

        Spans = copy;
        Alignment = alignment;
    }

    public IReadOnlyList<TextSpan> Spans { get; }

    public TextAlignment Alignment { get; }
}
