using Maviray.Pdf.Elements;
using Maviray.Pdf.Fluent.Nodes;

namespace Maviray.Pdf;

/// <summary>
/// The calls that fill an <see cref="IContainer"/>. A wrapping call, such as a padding or an alignment, returns the
/// container of its child; a call that ends the chain, such as a column or a line, returns nothing. Each container
/// takes one call. Lengths are in points unless an overload takes a <see cref="Unit"/>.
/// </summary>
public static class ContainerExtensions
{
    /// <summary>Items one below the other.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="compose">Adds the items.</param>
    public static void Column(this IContainer container, Action<ColumnBuilder> compose)
    {
        ArgumentNullException.ThrowIfNull(compose);
        var node = new ColumnNode();
        ContainerSlot.Of(container).Set(node);
        compose(new ColumnBuilder(node));
    }

    /// <summary>Items side by side.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="compose">Adds the items.</param>
    public static void Row(this IContainer container, Action<RowBuilder> compose)
    {
        ArgumentNullException.ThrowIfNull(compose);
        var node = new RowNode();
        ContainerSlot.Of(container).Set(node);
        compose(new RowBuilder(node));
    }

    /// <summary>Space on all four sides.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="all">The padding in points.</param>
    public static IContainer Padding(this IContainer container, double all) => Padding(container, all, Unit.Point);

    /// <summary>Space on all four sides.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="all">The padding.</param>
    /// <param name="unit">Its unit.</param>
    public static IContainer Padding(this IContainer container, double all, Unit unit)
    {
        var points = Units.ToPoints(all, unit, nameof(all));
        return Pad(container, points, points, points, points);
    }

    /// <summary>Space on the left and right.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="horizontal">The padding in points.</param>
    public static IContainer PaddingHorizontal(this IContainer container, double horizontal) => PaddingHorizontal(container, horizontal, Unit.Point);

    /// <summary>Space on the left and right.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="horizontal">The padding.</param>
    /// <param name="unit">Its unit.</param>
    public static IContainer PaddingHorizontal(this IContainer container, double horizontal, Unit unit)
    {
        var points = Units.ToPoints(horizontal, unit, nameof(horizontal));
        return Pad(container, points, 0, points, 0);
    }

    /// <summary>Space at the top and bottom.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="vertical">The padding in points.</param>
    public static IContainer PaddingVertical(this IContainer container, double vertical) => PaddingVertical(container, vertical, Unit.Point);

    /// <summary>Space at the top and bottom.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="vertical">The padding.</param>
    /// <param name="unit">Its unit.</param>
    public static IContainer PaddingVertical(this IContainer container, double vertical, Unit unit)
    {
        var points = Units.ToPoints(vertical, unit, nameof(vertical));
        return Pad(container, 0, points, 0, points);
    }

    /// <summary>Space on the left.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="left">The padding in points.</param>
    public static IContainer PaddingLeft(this IContainer container, double left) => PaddingLeft(container, left, Unit.Point);

    /// <summary>Space on the left.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="left">The padding.</param>
    /// <param name="unit">Its unit.</param>
    public static IContainer PaddingLeft(this IContainer container, double left, Unit unit) => Pad(container, Units.ToPoints(left, unit, nameof(left)), 0, 0, 0);

    /// <summary>Space at the top.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="top">The padding in points.</param>
    public static IContainer PaddingTop(this IContainer container, double top) => PaddingTop(container, top, Unit.Point);

    /// <summary>Space at the top.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="top">The padding.</param>
    /// <param name="unit">Its unit.</param>
    public static IContainer PaddingTop(this IContainer container, double top, Unit unit) => Pad(container, 0, Units.ToPoints(top, unit, nameof(top)), 0, 0);

    /// <summary>Space on the right.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="right">The padding in points.</param>
    public static IContainer PaddingRight(this IContainer container, double right) => PaddingRight(container, right, Unit.Point);

    /// <summary>Space on the right.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="right">The padding.</param>
    /// <param name="unit">Its unit.</param>
    public static IContainer PaddingRight(this IContainer container, double right, Unit unit) => Pad(container, 0, 0, Units.ToPoints(right, unit, nameof(right)), 0);

    /// <summary>Space at the bottom.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="bottom">The padding in points.</param>
    public static IContainer PaddingBottom(this IContainer container, double bottom) => PaddingBottom(container, bottom, Unit.Point);

    /// <summary>Space at the bottom.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="bottom">The padding.</param>
    /// <param name="unit">Its unit.</param>
    public static IContainer PaddingBottom(this IContainer container, double bottom, Unit unit) => Pad(container, 0, 0, 0, Units.ToPoints(bottom, unit, nameof(bottom)));

    /// <summary>Places the content at the left of the space it is drawn in.</summary>
    /// <param name="container">The container to fill.</param>
    public static IContainer AlignLeft(this IContainer container) => Align(container, HorizontalAlignment.Left, null);

    /// <summary>Centres the content across the space it is drawn in.</summary>
    /// <param name="container">The container to fill.</param>
    public static IContainer AlignCenter(this IContainer container) => Align(container, HorizontalAlignment.Center, null);

    /// <summary>Places the content at the right of the space it is drawn in.</summary>
    /// <param name="container">The container to fill.</param>
    public static IContainer AlignRight(this IContainer container) => Align(container, HorizontalAlignment.Right, null);

    /// <summary>Places the content at the top of the space it is drawn in.</summary>
    /// <param name="container">The container to fill.</param>
    public static IContainer AlignTop(this IContainer container) => Align(container, null, VerticalAlignment.Top);

    /// <summary>Centres the content down the space it is drawn in. A part split across pages stays at the top.</summary>
    /// <param name="container">The container to fill.</param>
    public static IContainer AlignMiddle(this IContainer container) => Align(container, null, VerticalAlignment.Middle);

    /// <summary>Places the content at the bottom of the space it is drawn in. A part split across pages stays at the top.</summary>
    /// <param name="container">The container to fill.</param>
    public static IContainer AlignBottom(this IContainer container) => Align(container, null, VerticalAlignment.Bottom);

    /// <summary>Exactly this width. With less space it doesn't fit.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="width">The width in points.</param>
    public static IContainer Width(this IContainer container, double width) => Width(container, width, Unit.Point);

    /// <summary>Exactly this width. With less space it doesn't fit.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="width">The width.</param>
    /// <param name="unit">Its unit.</param>
    public static IContainer Width(this IContainer container, double width, Unit unit)
    {
        var points = Units.ToPoints(width, unit, nameof(width));
        return Limit(container, "Width", points, points, 0, double.PositiveInfinity);
    }

    /// <summary>At least this width. With less space it doesn't fit.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="width">The width in points.</param>
    public static IContainer MinWidth(this IContainer container, double width) => MinWidth(container, width, Unit.Point);

    /// <summary>At least this width. With less space it doesn't fit.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="width">The width.</param>
    /// <param name="unit">Its unit.</param>
    public static IContainer MinWidth(this IContainer container, double width, Unit unit) =>
        Limit(container, "MinWidth", Units.ToPoints(width, unit, nameof(width)), double.PositiveInfinity, 0, double.PositiveInfinity);

    /// <summary>At most this width.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="width">The width in points.</param>
    public static IContainer MaxWidth(this IContainer container, double width) => MaxWidth(container, width, Unit.Point);

    /// <summary>At most this width.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="width">The width.</param>
    /// <param name="unit">Its unit.</param>
    public static IContainer MaxWidth(this IContainer container, double width, Unit unit) =>
        Limit(container, "MaxWidth", 0, Units.ToPoints(width, unit, nameof(width)), 0, double.PositiveInfinity);

    /// <summary>Exactly this height on each page.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="height">The height in points.</param>
    public static IContainer Height(this IContainer container, double height) => Height(container, height, Unit.Point);

    /// <summary>Exactly this height on each page.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="height">The height.</param>
    /// <param name="unit">Its unit.</param>
    public static IContainer Height(this IContainer container, double height, Unit unit)
    {
        var points = Units.ToPoints(height, unit, nameof(height));
        return Limit(container, "Height", 0, double.PositiveInfinity, points, points);
    }

    /// <summary>At least this height on each page; with less left on the page, it goes to the next one.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="height">The height in points.</param>
    public static IContainer MinHeight(this IContainer container, double height) => MinHeight(container, height, Unit.Point);

    /// <summary>At least this height on each page; with less left on the page, it goes to the next one.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="height">The height.</param>
    /// <param name="unit">Its unit.</param>
    public static IContainer MinHeight(this IContainer container, double height, Unit unit) =>
        Limit(container, "MinHeight", 0, double.PositiveInfinity, Units.ToPoints(height, unit, nameof(height)), double.PositiveInfinity);

    /// <summary>At most this height on each page.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="height">The height in points.</param>
    public static IContainer MaxHeight(this IContainer container, double height) => MaxHeight(container, height, Unit.Point);

    /// <summary>At most this height on each page.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="height">The height.</param>
    /// <param name="unit">Its unit.</param>
    public static IContainer MaxHeight(this IContainer container, double height, Unit unit) =>
        Limit(container, "MaxHeight", 0, double.PositiveInfinity, 0, Units.ToPoints(height, unit, nameof(height)));

    /// <summary>A colour behind the content, on every page part.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="color">The colour.</param>
    /// <param name="stretch">Whether it fills the height it is drawn at, such as a row's, or only the content's.</param>
    public static IContainer Background(this IContainer container, Color color, bool stretch = true) => Wrap(container, new BackgroundNode(color, stretch)).Slot;

    /// <summary>A border on all four sides, which takes space as in CSS.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="width">The border's width in points.</param>
    /// <param name="color">Its colour.</param>
    /// <param name="stretch">Whether it frames the height it is drawn at, such as a row's, or only the content's.</param>
    public static IContainer Border(this IContainer container, double width, Color color, bool stretch = true) => Border(container, width, Unit.Point, color, stretch);

    /// <summary>A border on all four sides, which takes space as in CSS.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="width">The border's width.</param>
    /// <param name="unit">Its unit.</param>
    /// <param name="color">Its colour.</param>
    /// <param name="stretch">Whether it frames the height it is drawn at, such as a row's, or only the content's.</param>
    public static IContainer Border(this IContainer container, double width, Unit unit, Color color, bool stretch = true)
    {
        var points = Units.ToPoints(width, unit, nameof(width));
        return Wrap(container, new BorderNode(points, points, points, points, color, stretch)).Slot;
    }

    /// <summary>A border on the left side.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="width">The border's width in points.</param>
    /// <param name="color">Its colour.</param>
    /// <param name="stretch">Whether it runs the height it is drawn at, such as a row's, or only the content's.</param>
    public static IContainer BorderLeft(this IContainer container, double width, Color color, bool stretch = true) => BorderLeft(container, width, Unit.Point, color, stretch);

    /// <summary>A border on the left side.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="width">The border's width.</param>
    /// <param name="unit">Its unit.</param>
    /// <param name="color">Its colour.</param>
    /// <param name="stretch">Whether it runs the height it is drawn at, such as a row's, or only the content's.</param>
    public static IContainer BorderLeft(this IContainer container, double width, Unit unit, Color color, bool stretch = true) =>
        Wrap(container, new BorderNode(Units.ToPoints(width, unit, nameof(width)), 0, 0, 0, color, stretch)).Slot;

    /// <summary>A border on the top side.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="width">The border's width in points.</param>
    /// <param name="color">Its colour.</param>
    /// <param name="stretch">Whether the box is the height it is drawn at, such as a row's, or only the content's.</param>
    public static IContainer BorderTop(this IContainer container, double width, Color color, bool stretch = true) => BorderTop(container, width, Unit.Point, color, stretch);

    /// <summary>A border on the top side.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="width">The border's width.</param>
    /// <param name="unit">Its unit.</param>
    /// <param name="color">Its colour.</param>
    /// <param name="stretch">Whether the box is the height it is drawn at, such as a row's, or only the content's.</param>
    public static IContainer BorderTop(this IContainer container, double width, Unit unit, Color color, bool stretch = true) =>
        Wrap(container, new BorderNode(0, Units.ToPoints(width, unit, nameof(width)), 0, 0, color, stretch)).Slot;

    /// <summary>A border on the right side.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="width">The border's width in points.</param>
    /// <param name="color">Its colour.</param>
    /// <param name="stretch">Whether it runs the height it is drawn at, such as a row's, or only the content's.</param>
    public static IContainer BorderRight(this IContainer container, double width, Color color, bool stretch = true) => BorderRight(container, width, Unit.Point, color, stretch);

    /// <summary>A border on the right side.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="width">The border's width.</param>
    /// <param name="unit">Its unit.</param>
    /// <param name="color">Its colour.</param>
    /// <param name="stretch">Whether it runs the height it is drawn at, such as a row's, or only the content's.</param>
    public static IContainer BorderRight(this IContainer container, double width, Unit unit, Color color, bool stretch = true) =>
        Wrap(container, new BorderNode(0, 0, Units.ToPoints(width, unit, nameof(width)), 0, color, stretch)).Slot;

    /// <summary>A border on the bottom side.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="width">The border's width in points.</param>
    /// <param name="color">Its colour.</param>
    /// <param name="stretch">Whether it sits at the foot of the height it is drawn at, such as a row's, or under the content.</param>
    public static IContainer BorderBottom(this IContainer container, double width, Color color, bool stretch = true) => BorderBottom(container, width, Unit.Point, color, stretch);

    /// <summary>A border on the bottom side.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="width">The border's width.</param>
    /// <param name="unit">Its unit.</param>
    /// <param name="color">Its colour.</param>
    /// <param name="stretch">Whether it sits at the foot of the height it is drawn at, such as a row's, or under the content.</param>
    public static IContainer BorderBottom(this IContainer container, double width, Unit unit, Color color, bool stretch = true) =>
        Wrap(container, new BorderNode(0, 0, 0, Units.ToPoints(width, unit, nameof(width)), color, stretch)).Slot;

    /// <summary>A horizontal rule across the width it is drawn at.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="thickness">The rule's thickness in points.</param>
    /// <param name="color">Its colour.</param>
    public static void HorizontalLine(this IContainer container, double thickness, Color color) => HorizontalLine(container, thickness, Unit.Point, color);

    /// <summary>A horizontal rule across the width it is drawn at.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="thickness">The rule's thickness.</param>
    /// <param name="unit">Its unit.</param>
    /// <param name="color">Its colour.</param>
    public static void HorizontalLine(this IContainer container, double thickness, Unit unit, Color color) =>
        ContainerSlot.Of(container).Set(new LineNode(false, Units.ToPoints(thickness, unit, nameof(thickness)), color));

    /// <summary>A vertical rule down the height it is drawn at, which in a row is the row's height.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="thickness">The rule's thickness in points.</param>
    /// <param name="color">Its colour.</param>
    public static void VerticalLine(this IContainer container, double thickness, Color color) => VerticalLine(container, thickness, Unit.Point, color);

    /// <summary>A vertical rule down the height it is drawn at, which in a row is the row's height.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="thickness">The rule's thickness.</param>
    /// <param name="unit">Its unit.</param>
    /// <param name="color">Its colour.</param>
    public static void VerticalLine(this IContainer container, double thickness, Unit unit, Color color) =>
        ContainerSlot.Of(container).Set(new LineNode(true, Units.ToPoints(thickness, unit, nameof(thickness)), color));

    /// <summary>A paragraph of one span of text, styled through the builder it returns.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="text">The text; a line feed starts a new line.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public static TextSpanBuilder Text(this IContainer container, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Wrap(container, new TextNode()).Add(TextRunKind.Text, text);
    }

    /// <summary>A paragraph of spans: text, the page number and the page count.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="compose">Adds the spans and sets the alignment.</param>
    public static void Text(this IContainer container, Action<TextBuilder> compose)
    {
        ArgumentNullException.ThrowIfNull(compose);
        compose(new TextBuilder(Wrap(container, new TextNode())));
    }

    /// <summary>Ends the page here; what follows starts on the next page.</summary>
    /// <param name="container">The container to fill.</param>
    public static void PageBreak(this IContainer container) => ContainerSlot.Of(container).Set(new PageBreakNode());

    /// <summary>The text style everything inside inherits, over the one inherited from outside.</summary>
    /// <param name="container">The container to fill.</param>
    /// <param name="configure">Sets values on an empty style and returns it.</param>
    /// <exception cref="ArgumentException"><paramref name="configure"/> returned null.</exception>
    public static IContainer DefaultTextStyle(this IContainer container, Func<TextStyle, TextStyle> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var slot = ContainerSlot.Of(container);
        var style = configure(new TextStyle()) ?? throw new ArgumentException("The style function returned null.", nameof(configure));
        var node = new DefaultStyleNode(style);
        slot.Set(node);
        return node.Slot;
    }

    private static ContainerSlot Pad(IContainer container, double left, double top, double right, double bottom) =>
        Wrap(container, new PaddingNode(left, top, right, bottom)).Slot;

    private static ContainerSlot Align(IContainer container, HorizontalAlignment? horizontal, VerticalAlignment? vertical) =>
        Wrap(container, new AlignNode(horizontal, vertical)).Slot;

    private static ContainerSlot Limit(IContainer container, string name, double minWidth, double maxWidth, double minHeight, double maxHeight) =>
        Wrap(container, new SizeLimitNode(name, minWidth, maxWidth, minHeight, maxHeight)).Slot;

    private static T Wrap<T>(IContainer container, T node)
        where T : Node
    {
        ContainerSlot.Of(container).Set(node);
        return node;
    }
}
