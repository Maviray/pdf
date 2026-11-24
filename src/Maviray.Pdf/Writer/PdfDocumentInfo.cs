namespace Maviray.Pdf.Writer;

/// <summary>
/// The values of the document information dictionary (spec L-12), each written as a text string when it is set. The
/// writer always adds <c>/Producer</c>.
/// </summary>
internal sealed record PdfDocumentInfo(
    string? Title,
    string? Author,
    string? Subject,
    string? Keywords,
    string? Creator,
    DateTimeOffset? CreationDate);
