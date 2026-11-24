namespace Maviray.Pdf;

/// <summary>A page's width and height in points, in portrait unless <see cref="Landscape"/> turns it.</summary>
public readonly struct PageSize
{
    private PageSize(double width, double height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>ISO A3, 297 × 420 mm.</summary>
    public static PageSize A3 { get; } = Millimetres(297, 420);

    /// <summary>ISO A4, 210 × 297 mm.</summary>
    public static PageSize A4 { get; } = Millimetres(210, 297);

    /// <summary>ISO A5, 148 × 210 mm.</summary>
    public static PageSize A5 { get; } = Millimetres(148, 210);

    /// <summary>US Letter, 8.5 × 11 inches.</summary>
    public static PageSize Letter { get; } = new(612, 792);

    /// <summary>US Legal, 8.5 × 14 inches.</summary>
    public static PageSize Legal { get; } = new(612, 1008);

    /// <summary>The width in points.</summary>
    public double Width { get; }

    /// <summary>The height in points.</summary>
    public double Height { get; }

    /// <summary>The same size turned so that its longer side runs across.</summary>
    public PageSize Landscape() => Height > Width ? new(Height, Width) : this;

    private static PageSize Millimetres(double width, double height) => new(width * 72 / 25.4, height * 72 / 25.4);
}
