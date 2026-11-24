using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Layout;

/// <summary>
/// A page set (spec L-04): pages of one size, margins and background, whose content flows across as many pages as it
/// needs, with the header and footer on every page. The next set starts on a new page.
/// </summary>
internal sealed class PageSet
{
    /// <param name="width">The page width in points, positive and finite.</param>
    /// <param name="height">The page height in points, positive and finite.</param>
    /// <param name="margins">The margins, each zero or more and finite.</param>
    /// <param name="background">The colour that fills each page first, or null for none.</param>
    /// <param name="header">The header, drawn at the top of the inner area of every page, or null.</param>
    /// <param name="content">The content, which flows across the pages.</param>
    /// <param name="footer">The footer, drawn at the bottom of the inner area of every page, or null.</param>
    public PageSet(double width, double height, Margins margins, RgbColor? background, Element? header, Element content, Element? footer)
    {
        ArgumentNullException.ThrowIfNull(content);
        Width = Positive(width, nameof(width));
        Height = Positive(height, nameof(height));
        _ = NotNegative(margins.Left, nameof(margins));
        _ = NotNegative(margins.Top, nameof(margins));
        _ = NotNegative(margins.Right, nameof(margins));
        _ = NotNegative(margins.Bottom, nameof(margins));
        Margins = margins;
        Background = background;
        Header = header is null ? null : new Slot("Header", header);
        Content = new Slot("Content", content);
        Footer = footer is null ? null : new Slot("Footer", footer);
    }

    public double Width { get; }

    public double Height { get; }

    public Margins Margins { get; }

    public RgbColor? Background { get; }

    public Slot? Header { get; }

    public Slot Content { get; }

    public Slot? Footer { get; }

    /// <summary>Whether any of its slots shows the page count.</summary>
    public bool ShowsPageCount => Content.ShowsPageCount || Header?.ShowsPageCount == true || Footer?.ShowsPageCount == true;

    /// <summary>Resets every slot, before a pass.</summary>
    public void Reset()
    {
        Header?.Reset();
        Content.Reset();
        Footer?.Reset();
    }

    private static double Positive(double value, string paramName) => double.IsFinite(value) && value > 0
        ? value
        : throw new ArgumentOutOfRangeException(paramName, value, "A page dimension must be a positive, finite number of points.");

    private static double NotNegative(double value, string paramName) => double.IsFinite(value) && value >= 0
        ? value
        : throw new ArgumentOutOfRangeException(paramName, value, "A margin must be zero or more, and finite.");
}
