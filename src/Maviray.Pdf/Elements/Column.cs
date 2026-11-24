using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Elements;

/// <summary>
/// Items one below the other (spec L-06), each at the full width, named <c>Item n</c> from 1 in paths (L-05).
/// <list type="bullet">
/// <item>It measures its items in order from its cursor and stops at the first that is <see cref="Fit.Partial"/> or
/// <see cref="Fit.None"/>. It is <see cref="Fit.None"/> only when its first remaining item is.</item>
/// <item>Spacing goes between two items that both take height on the page. An item with no height, such as a page
/// break's second part, takes none, so no gap opens at the top of a page (P1.10 plan, ruling 2).</item>
/// <item>Its width is the widest item's. Each item is drawn at the full width and its own measured height, and a
/// finished item never draws again.</item>
/// </list>
/// </summary>
internal sealed class Column : Element
{
    private readonly Slot[] _items;
    private readonly double _spacing;
    private int _cursor;

    /// <param name="items">The items, in order.</param>
    /// <param name="spacing">The space in points between two items on one page, zero or more and finite.</param>
    public Column(IEnumerable<Element> items, double spacing)
        : base("Column")
    {
        ArgumentNullException.ThrowIfNull(items);
        if (!double.IsFinite(spacing) || spacing < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(spacing), spacing, "The spacing must be zero or more, and finite.");
        }

        _spacing = spacing;
        _items = [.. items.Select((item, i) => Adopt(new Slot($"Item {i + 1}", item)))];
    }

    public override bool ShowsPageCount => _items.Any(item => item.ShowsPageCount);

    public override Measurement Measure(Size available, PageContext page)
    {
        var width = 0.0;
        var height = 0.0;
        var tookHeight = false;
        for (var i = _cursor; i < _items.Length; i++)
        {
            var gap = tookHeight ? _spacing : 0;
            var measurement = _items[i].Measure(Below(available, height + gap), page);
            switch (measurement.Fit)
            {
                case Fit.Full:
                    width = Math.Max(width, measurement.Size.Width);
                    if (measurement.Size.Height > 0)
                    {
                        height += gap + measurement.Size.Height;
                        tookHeight = true;
                    }

                    break;
                case Fit.Partial:
                    var total = measurement.Size.Height > 0 ? height + gap + measurement.Size.Height : height;
                    return measurement.WithSize(new Size(Math.Max(width, measurement.Size.Width), total));
                default:
                    return i == _cursor ? measurement : Measurement.Partial(new Size(width, height), this, available);
            }
        }

        return Measurement.Full(new Size(width, height), this);
    }

    public override bool Draw(Size available, ICanvas canvas, PageContext page)
    {
        var advanced = false;
        var height = 0.0;
        var tookHeight = false;
        while (_cursor < _items.Length)
        {
            var gap = tookHeight ? _spacing : 0;
            var item = _items[_cursor];
            var measurement = item.Measure(Below(available, height + gap), page);
            if (measurement.Fit == Fit.None)
            {
                break;
            }

            var top = measurement.Size.Height > 0 ? height + gap : height;
            canvas.PushOffset(0, top);
            advanced |= item.Draw(new Size(available.Width, measurement.Size.Height), canvas, page);
            canvas.PopOffset();
            if (measurement.Size.Height > 0)
            {
                height = top + measurement.Size.Height;
                tookHeight = true;
            }

            if (measurement.Fit == Fit.Partial)
            {
                break;
            }

            _cursor++;
            advanced = true;
        }

        return advanced;
    }

    public override void Reset()
    {
        _cursor = 0;
        foreach (var item in _items)
        {
            item.Reset();
        }
    }

    /// <summary>The space left below <paramref name="used"/> points, never below 0.</summary>
    private static Size Below(Size available, double used) => new(available.Width, Math.Max(0, available.Height - used));
}
