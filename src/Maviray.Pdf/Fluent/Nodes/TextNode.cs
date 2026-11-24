using Maviray.Pdf.Elements;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Text;

namespace Maviray.Pdf.Fluent.Nodes;

/// <summary>
/// A text call: its spans, each with its builder's style, and the alignment. It builds the Text element once the
/// compose callback is done, so a span styled after <c>.Text</c> returns is styled; after that, the span builders refuse
/// further calls.
/// </summary>
internal sealed class TextNode : Node
{
    private readonly List<TextSpanBuilder> _spans = [];
    private bool _built;

    public TextAlignment Alignment { get; set; } = TextAlignment.Left;

    public TextSpanBuilder Add(TextRunKind kind, string text)
    {
        ThrowIfBuilt();
        var span = new TextSpanBuilder(this, kind, text);
        _spans.Add(span);
        return span;
    }

    /// <exception cref="InvalidOperationException">The text's element was already built.</exception>
    public void ThrowIfBuilt()
    {
        if (_built)
        {
            throw new InvalidOperationException("This text was already laid out; style it while the document is composed.");
        }
    }

    public override Element Build(BuildContext context)
    {
        _built = true;
        var runs = _spans.Select(span => new TextRun(span.Kind, span.Text, span.Style)).ToList();
        return new TextElement(runs, new TextStyle(), context.Inherited, Alignment, context.Fonts);
    }
}
