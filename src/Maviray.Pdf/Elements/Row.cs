using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Elements;

/// <summary>
/// Items side by side (spec L-06), each named by its kind and its position among all the row's items, such as
/// <c>Flex 2</c> (L-05).
/// <list type="bullet">
/// <item>Widths in order: fixed ones, then auto ones (the item measured at an infinite width), then flex shares of
/// what is left, with the spacing between items. An auto item that fits nothing there makes the Row
/// <see cref="Fit.None"/>.</item>
/// <item>At an infinite width, flex items are measured like auto ones, and the Row reports the width at which each
/// flex share is at least its item's own width, so it fits where it says (P1.10 review).</item>
/// <item>Its height is the tallest unfinished item's. It is <see cref="Fit.None"/> if any item is, and
/// <see cref="Fit.Partial"/> if any is.</item>
/// <item>Every unfinished item draws at its width and the row's height. An item that finished on an earlier page
/// draws nothing, and its height doesn't count. An auto item keeps the width it finished at, so the items after it
/// keep their places.</item>
/// </list>
/// </summary>
internal sealed class Row : Element
{
    private readonly RowItemKind[] _kinds;
    private readonly double[] _values;
    private readonly Slot[] _items;
    private readonly bool[] _finished;
    private readonly double[] _finishedWidths;
    private readonly double _spacing;

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
        _finishedWidths = new double[list.Length];
        _spacing = spacing;
    }

    public override bool ShowsPageCount => _items.Any(item => item.ShowsPageCount);

    public override Measurement Measure(Size available, PageContext page)
    {
        if (Widths(available, page, out var widths) is { } none)
        {
            return none;
        }

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
        if (Widths(available, page, out var widths) is not null)
        {
            return false;
        }

        var height = 0.0;
        for (var i = 0; i < _items.Length; i++)
        {
            if (!_finished[i])
            {
                height = Math.Max(height, _items[i].Measure(new Size(widths[i], available.Height), page).Size.Height);
            }
        }

        var advanced = false;
        var x = 0.0;
        for (var i = 0; i < _items.Length; i++)
        {
            if (!_finished[i])
            {
                // The item is drawn at the row's height, so whether it finishes is decided at that height.
                var space = new Size(widths[i], height);
                var fit = _items[i].Measure(space, page).Fit;
                canvas.PushOffset(x, 0);
                advanced |= _items[i].Draw(space, canvas, page);
                canvas.PopOffset();
                if (fit == Fit.Full)
                {
                    _finished[i] = true;
                    _finishedWidths[i] = widths[i];
                    advanced = true;
                }
            }

            x += widths[i] + _spacing;
        }

        return advanced || Array.TrueForAll(_finished, finished => finished);
    }

    public override void Reset()
    {
        Array.Clear(_finished);
        Array.Clear(_finishedWidths);
        foreach (var item in _items)
        {
            item.Reset();
        }
    }

    /// <summary>
    /// Each item's width. Returns the measurement of an auto item, or of a flex item at an infinite width, that fits
    /// nothing in the available height, and null otherwise. Each such item is measured once.
    /// </summary>
    /// <exception cref="LayoutException">The fixed and auto items and the spacing need more than the width.</exception>
    private Measurement? Widths(Size available, PageContext page, out double[] widths)
    {
        var infinite = double.IsPositiveInfinity(available.Width);
        widths = new double[_items.Length];
        var used = _spacing * Math.Max(0, _items.Length - 1);
        var weights = 0.0;
        var widthPerWeight = 0.0;
        for (var i = 0; i < _items.Length; i++)
        {
            var kind = _kinds[i];
            if (kind == RowItemKind.Fixed)
            {
                widths[i] = _values[i];
                used += widths[i];
                continue;
            }

            if (kind == RowItemKind.Flex && !infinite)
            {
                weights += _values[i];
                continue;
            }

            if (kind == RowItemKind.Auto && _finished[i])
            {
                widths[i] = _finishedWidths[i];
                used += widths[i];
                continue;
            }

            var measurement = _items[i].Measure(new Size(double.PositiveInfinity, available.Height), page);
            if (measurement.Fit == Fit.None)
            {
                return measurement;
            }

            if (kind == RowItemKind.Flex)
            {
                weights += _values[i];
                widthPerWeight = Math.Max(widthPerWeight, measurement.Size.Width / _values[i]);
            }
            else
            {
                widths[i] = measurement.Size.Width;
                used += widths[i];
            }
        }

        if (infinite)
        {
            SetFlexWidths(widths, widthPerWeight * weights, weights);
            return null;
        }

        if (!LayoutTolerance.Fits(used, available.Width))
        {
            throw LayoutErrors.Error(
                page,
                this,
                $"its fixed and auto items and spacing need {LayoutErrors.Length(used)}, and the space is {LayoutErrors.Length(available.Width)}.",
                available);
        }

        SetFlexWidths(widths, Math.Max(0, available.Width - used), weights);
        return null;
    }

    private void SetFlexWidths(double[] widths, double left, double weights)
    {
        for (var i = 0; i < _items.Length; i++)
        {
            if (_kinds[i] == RowItemKind.Flex)
            {
                widths[i] = left * _values[i] / weights;
            }
        }
    }

    private double Total(double[] widths) => widths.Sum() + (_spacing * Math.Max(0, widths.Length - 1));
}
