using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Elements;

/// <summary>
/// Items side by side (spec L-06), each named by its kind and its position among all the row's items, such as
/// <c>Flex 2</c> (L-05).
/// <list type="bullet">
/// <item>Widths in order: fixed ones, then auto ones (the item measured at an infinite width), then flex shares of
/// what is left, with the spacing between items. At an infinite width, flex items are measured like auto ones.</item>
/// <item>Its height is the tallest unfinished item's. It is <see cref="Fit.None"/> if any item is, and
/// <see cref="Fit.Partial"/> if any is.</item>
/// <item>Every unfinished item draws at its width and the row's height. An item that finished on an earlier page
/// draws nothing, and its height doesn't count.</item>
/// <item>The widths are fixed at the row's first draw and kept on later pages while its width stays the same, so its
/// items keep their places across pages (P1.10 log, ruling).</item>
/// </list>
/// </summary>
internal sealed class Row : Element
{
    private const double Tolerance = 1e-9;

    private readonly RowItemKind[] _kinds;
    private readonly double[] _values;
    private readonly Slot[] _items;
    private readonly bool[] _finished;
    private readonly double _spacing;
    private double[]? _drawnWidths;
    private double _drawnFor;

    /// <param name="items">The items, in order.</param>
    /// <param name="spacing">The space in points between two items, zero or more and finite.</param>
    public Row(IEnumerable<RowItem> items, double spacing)
        : base("Row")
    {
        ArgumentNullException.ThrowIfNull(items);
        if (!double.IsFinite(spacing) || spacing < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(spacing), spacing, "The spacing must be zero or more, and finite.");
        }

        var list = items.ToArray();
        _kinds = new RowItemKind[list.Length];
        _values = new double[list.Length];
        _items = new Slot[list.Length];
        for (var i = 0; i < list.Length; i++)
        {
            var (kind, value, element) = list[i];
            if (kind == RowItemKind.Fixed && !(double.IsFinite(value) && value >= 0))
            {
                throw new ArgumentOutOfRangeException(nameof(items), value, "A fixed item's width must be zero or more, and finite.");
            }

            if (kind == RowItemKind.Flex && !(double.IsFinite(value) && value > 0))
            {
                throw new ArgumentOutOfRangeException(nameof(items), value, "A flex item's weight must be positive and finite.");
            }

            _kinds[i] = kind;
            _values[i] = value;
            _items[i] = Adopt(new Slot($"{kind} {i + 1}", element));
        }

        _finished = new bool[list.Length];
        _spacing = spacing;
    }

    public override bool ShowsPageCount => _items.Any(item => item.ShowsPageCount);

    public override Measurement Measure(Size available, PageContext page)
    {
        var widths = Widths(available, page);
        var height = 0.0;
        Measurement? split = null;
        for (var i = 0; i < _items.Length; i++)
        {
            if (_finished[i])
            {
                continue;
            }

            var measurement = _items[i].Measure(new Size(widths[i], available.Height), page);
            if (measurement.Fit == Fit.None)
            {
                return measurement;
            }

            if (measurement.Fit == Fit.Partial)
            {
                split ??= measurement;
            }

            height = Math.Max(height, measurement.Size.Height);
        }

        var size = new Size(Total(widths), height);
        return split is { } partial ? partial.WithSize(size) : Measurement.Full(size, this);
    }

    public override bool Draw(Size available, ICanvas canvas, PageContext page)
    {
        var height = Measure(available, page).Size.Height;
        var widths = Widths(available, page);
        _drawnWidths = widths;
        _drawnFor = available.Width;

        var advanced = false;
        var x = 0.0;
        for (var i = 0; i < _items.Length; i++)
        {
            if (!_finished[i])
            {
                var fit = _items[i].Measure(new Size(widths[i], available.Height), page).Fit;
                canvas.PushOffset(x, 0);
                advanced |= _items[i].Draw(new Size(widths[i], height), canvas, page);
                canvas.PopOffset();
                if (fit == Fit.Full)
                {
                    _finished[i] = true;
                    advanced = true;
                }
            }

            x += widths[i] + _spacing;
        }

        return advanced;
    }

    public override void Reset()
    {
        Array.Clear(_finished);
        _drawnWidths = null;
        foreach (var item in _items)
        {
            item.Reset();
        }
    }

    /// <summary>Each item's width, or the widths of the row's first draw while its width is the same.</summary>
    /// <exception cref="LayoutException">The fixed and auto items and the spacing need more than the width.</exception>
    private double[] Widths(Size available, PageContext page)
    {
        if (_drawnWidths is not null && _drawnFor == available.Width)
        {
            return _drawnWidths;
        }

        var infinite = double.IsPositiveInfinity(available.Width);
        var widths = new double[_items.Length];
        var used = _spacing * Math.Max(0, _items.Length - 1);
        var weights = 0.0;
        for (var i = 0; i < _items.Length; i++)
        {
            switch (_kinds[i])
            {
                case RowItemKind.Fixed:
                    widths[i] = _values[i];
                    used += widths[i];
                    break;
                case RowItemKind.Flex when !infinite:
                    weights += _values[i];
                    break;
                default:
                    widths[i] = _items[i].Measure(new Size(double.PositiveInfinity, available.Height), page).Size.Width;
                    used += widths[i];
                    break;
            }
        }

        if (!infinite && used > available.Width + Tolerance)
        {
            throw LayoutErrors.Error(
                page,
                this,
                $"its fixed and auto items and spacing need {LayoutErrors.Length(used)}, and the space is {LayoutErrors.Length(available.Width)}.",
                available);
        }

        if (weights > 0)
        {
            var left = Math.Max(0, available.Width - used);
            for (var i = 0; i < _items.Length; i++)
            {
                if (_kinds[i] == RowItemKind.Flex)
                {
                    widths[i] = left * _values[i] / weights;
                }
            }
        }

        return widths;
    }

    private double Total(double[] widths) => widths.Sum() + (_spacing * Math.Max(0, widths.Length - 1));
}
