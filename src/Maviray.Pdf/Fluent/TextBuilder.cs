using Maviray.Pdf.Elements;
using Maviray.Pdf.Fluent.Nodes;
using Maviray.Pdf.Text;

namespace Maviray.Pdf;

/// <summary>Builds a paragraph of spans: text, the page number and the page count, and how its lines are aligned.</summary>
public sealed class TextBuilder
{
    private readonly TextNode _node;

    internal TextBuilder(TextNode node) => _node = node;

    /// <summary>A span of text.</summary>
    /// <param name="text">The text; a line feed starts a new line.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public TextSpanBuilder Span(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return _node.Add(TextRunKind.Text, text);
    }

    /// <summary>A span that shows the number of the page it is drawn on.</summary>
    public TextSpanBuilder PageNumber() => _node.Add(TextRunKind.PageNumber, "");

    /// <summary>A span that shows the document's page count.</summary>
    public TextSpanBuilder PageCount() => _node.Add(TextRunKind.PageCount, "");

    /// <summary>Lines start at the left edge, the default.</summary>
    public TextBuilder AlignLeft() => Align(TextAlignment.Left);

    /// <summary>Lines are centred.</summary>
    public TextBuilder AlignCenter() => Align(TextAlignment.Center);

    /// <summary>Lines end at the right edge.</summary>
    public TextBuilder AlignRight() => Align(TextAlignment.Right);

    private TextBuilder Align(TextAlignment alignment)
    {
        _node.ThrowIfBuilt();
        _node.Alignment = alignment;
        return this;
    }
}
