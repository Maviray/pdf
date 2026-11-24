using Maviray.Pdf.Elements;
using Maviray.Pdf.Fluent.Nodes;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf;

/// <summary>
/// Builds a page set: its size, margins and background, its default text style, and its header, content and footer.
/// Without any call it is A4, with no margins and no background.
/// </summary>
public sealed class PageBuilder
{
    private double _width = PageSize.A4.Width;
    private double _height = PageSize.A4.Height;
    private double _left;
    private double _top;
    private double _right;
    private double _bottom;
    private Color? _background;
    private TextStyle _defaultStyle = new();
    private ContainerSlot? _header;
    private ContainerSlot? _content;
    private ContainerSlot? _footer;

    internal PageBuilder()
    {
    }

    /// <summary>The page size.</summary>
    /// <param name="size">The size, such as <see cref="PageSize.A4"/>.</param>
    public PageBuilder Size(PageSize size) => Size(size.Width, size.Height);

    /// <summary>The page size.</summary>
    /// <param name="width">The width, positive and finite.</param>
    /// <param name="height">The height, positive and finite.</param>
    /// <param name="unit">The lengths' unit.</param>
    /// <exception cref="ArgumentOutOfRangeException">A length isn't positive and finite, or <paramref name="unit"/> isn't defined.</exception>
    public PageBuilder Size(double width, double height, Unit unit = Unit.Point)
    {
        _width = Positive(Units.ToPoints(width, unit, nameof(width)), width, nameof(width));
        _height = Positive(Units.ToPoints(height, unit, nameof(height)), height, nameof(height));
        return this;
    }

    /// <summary>The same margin on all four sides.</summary>
    /// <param name="all">The margin in points, zero or more and finite.</param>
    public PageBuilder Margin(double all) => Margin(all, Unit.Point);

    /// <summary>The same margin on all four sides.</summary>
    /// <param name="all">The margin, zero or more and finite.</param>
    /// <param name="unit">Its unit.</param>
    public PageBuilder Margin(double all, Unit unit)
    {
        var points = Units.ToPoints(all, unit, nameof(all));
        (_left, _top, _right, _bottom) = (points, points, points, points);
        return this;
    }

    /// <summary>The left and right margins.</summary>
    /// <param name="horizontal">The margin in points, zero or more and finite.</param>
    public PageBuilder MarginHorizontal(double horizontal) => MarginHorizontal(horizontal, Unit.Point);

    /// <summary>The left and right margins.</summary>
    /// <param name="horizontal">The margin, zero or more and finite.</param>
    /// <param name="unit">Its unit.</param>
    public PageBuilder MarginHorizontal(double horizontal, Unit unit)
    {
        _left = _right = Units.ToPoints(horizontal, unit, nameof(horizontal));
        return this;
    }

    /// <summary>The top and bottom margins.</summary>
    /// <param name="vertical">The margin in points, zero or more and finite.</param>
    public PageBuilder MarginVertical(double vertical) => MarginVertical(vertical, Unit.Point);

    /// <summary>The top and bottom margins.</summary>
    /// <param name="vertical">The margin, zero or more and finite.</param>
    /// <param name="unit">Its unit.</param>
    public PageBuilder MarginVertical(double vertical, Unit unit)
    {
        _top = _bottom = Units.ToPoints(vertical, unit, nameof(vertical));
        return this;
    }

    /// <summary>The left margin.</summary>
    /// <param name="left">The margin in points, zero or more and finite.</param>
    public PageBuilder MarginLeft(double left) => MarginLeft(left, Unit.Point);

    /// <summary>The left margin.</summary>
    /// <param name="left">The margin, zero or more and finite.</param>
    /// <param name="unit">Its unit.</param>
    public PageBuilder MarginLeft(double left, Unit unit)
    {
        _left = Units.ToPoints(left, unit, nameof(left));
        return this;
    }

    /// <summary>The top margin.</summary>
    /// <param name="top">The margin in points, zero or more and finite.</param>
    public PageBuilder MarginTop(double top) => MarginTop(top, Unit.Point);

    /// <summary>The top margin.</summary>
    /// <param name="top">The margin, zero or more and finite.</param>
    /// <param name="unit">Its unit.</param>
    public PageBuilder MarginTop(double top, Unit unit)
    {
        _top = Units.ToPoints(top, unit, nameof(top));
        return this;
    }

    /// <summary>The right margin.</summary>
    /// <param name="right">The margin in points, zero or more and finite.</param>
    public PageBuilder MarginRight(double right) => MarginRight(right, Unit.Point);

    /// <summary>The right margin.</summary>
    /// <param name="right">The margin, zero or more and finite.</param>
    /// <param name="unit">Its unit.</param>
    public PageBuilder MarginRight(double right, Unit unit)
    {
        _right = Units.ToPoints(right, unit, nameof(right));
        return this;
    }

    /// <summary>The bottom margin.</summary>
    /// <param name="bottom">The margin in points, zero or more and finite.</param>
    public PageBuilder MarginBottom(double bottom) => MarginBottom(bottom, Unit.Point);

    /// <summary>The bottom margin.</summary>
    /// <param name="bottom">The margin, zero or more and finite.</param>
    /// <param name="unit">Its unit.</param>
    public PageBuilder MarginBottom(double bottom, Unit unit)
    {
        _bottom = Units.ToPoints(bottom, unit, nameof(bottom));
        return this;
    }

    /// <summary>The colour that fills each page before anything else.</summary>
    /// <param name="color">The colour.</param>
    public PageBuilder Background(Color color)
    {
        _background = color;
        return this;
    }

    /// <summary>The text style every text of the page set inherits, over the built-in default.</summary>
    /// <param name="configure">Sets values on an empty style and returns it.</param>
    /// <exception cref="ArgumentException"><paramref name="configure"/> returned null.</exception>
    public PageBuilder DefaultTextStyle(Func<TextStyle, TextStyle> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _defaultStyle = configure(new TextStyle()) ?? throw new ArgumentException("The style function returned null.", nameof(configure));
        return this;
    }

    /// <summary>The header, at the top of the area inside the margins on every page. It must fit in full.</summary>
    /// <exception cref="InvalidOperationException">The header was already asked for.</exception>
    public IContainer Header() => _header = Once(_header, "header");

    /// <summary>The content, which flows across as many pages as it needs, between the header and the footer.</summary>
    /// <exception cref="InvalidOperationException">The content was already asked for.</exception>
    public IContainer Content() => _content = Once(_content, "content");

    /// <summary>The footer, at the bottom of the area inside the margins on every page. It must fit in full.</summary>
    /// <exception cref="InvalidOperationException">The footer was already asked for.</exception>
    public IContainer Footer() => _footer = Once(_footer, "footer");

    internal PageSet Build(FontRegistry fonts)
    {
        var context = new BuildContext(_defaultStyle, fonts);
        return new PageSet(
            _width,
            _height,
            new Margins(_left, _top, _right, _bottom),
            _background?.ToRgb(),
            _header?.Build(context),
            _content?.Build(context) ?? new Column([], 0),
            _footer?.Build(context));
    }

    private static ContainerSlot Once(ContainerSlot? slot, string name) => slot is null
        ? new ContainerSlot()
        : throw new InvalidOperationException($"The page set's {name} was already asked for: each slot takes one call.");

    private static double Positive(double points, double value, string paramName) => points > 0
        ? points
        : throw new ArgumentOutOfRangeException(paramName, value, "A page dimension must be positive and finite.");
}
