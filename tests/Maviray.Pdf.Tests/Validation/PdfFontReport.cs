namespace Maviray.Pdf.Tests.Validation;

/// <summary>One font as <c>pdffonts</c> reports it (spec F-13): its name, its type, and whether it is embedded, subset
/// and Unicode-mapped.</summary>
internal sealed record PdfFontReport(string Name, string Type, bool Embedded, bool Subset, bool Unicode);
