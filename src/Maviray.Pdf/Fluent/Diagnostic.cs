namespace Maviray.Pdf;

/// <summary>
/// Something that went wrong without stopping the document: a missing glyph or a substituted font face. Each is reported
/// at most once per document, to the handler the document's settings give, on the generating thread.
/// </summary>
public sealed class Diagnostic
{
    internal Diagnostic(DiagnosticKind kind, string message, string elementPath, int pageNumber)
    {
        Kind = kind;
        Message = message;
        ElementPath = elementPath;
        PageNumber = pageNumber;
    }

    /// <summary>What it reports.</summary>
    public DiagnosticKind Kind { get; }

    /// <summary>What happened, as a sentence.</summary>
    public string Message { get; }

    /// <summary>The element it happened in, from the page down, such as <c>Page 3 &gt; Content &gt; Column &gt; Item 2 &gt; Text</c>.</summary>
    public string ElementPath { get; }

    /// <summary>The page, counted from 1, where the element was first drawn. A character missing on a later page of
    /// the same paragraph is reported with this page too.</summary>
    public int PageNumber { get; }

    /// <summary>The kind, the path and the message.</summary>
    public override string ToString() => $"{Kind}: {ElementPath}: {Message}";
}
