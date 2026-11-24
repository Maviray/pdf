using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Tests.Layout;

/// <summary>
/// A test container that stacks its children downward from its cursor, with no spacing. It stops at the first child
/// that is <see cref="Fit.Partial"/> or <see cref="Fit.None"/>, is <see cref="Fit.None"/> only when its first remaining
/// child is, and passes that child's source and space up. Its width is the widest child's. Moving past a finished child
/// is progress. Column (P1.10) is the real one.
/// </summary>
internal sealed class TestStack : Element
{
    private readonly Element[] _children;
    private int _cursor;

    public TestStack(string name, IEnumerable<Element> children)
        : base(name)
    {
        _children = [.. children.Select(Adopt)];
    }

    public override bool ShowsPageCount => _children.Any(child => child.ShowsPageCount);

    public override Measurement Measure(Size available, PageContext page)
    {
        var width = 0.0;
        var height = 0.0;
        for (var i = _cursor; i < _children.Length; i++)
        {
            var measurement = _children[i].Measure(new Size(available.Width, available.Height - height), page);
            switch (measurement.Fit)
            {
                case Fit.Full:
                    width = Math.Max(width, measurement.Size.Width);
                    height += measurement.Size.Height;
                    break;
                case Fit.Partial:
                    return measurement.WithSize(new Size(Math.Max(width, measurement.Size.Width), height + measurement.Size.Height));
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
        while (_cursor < _children.Length)
        {
            var space = new Size(available.Width, available.Height - height);
            var measurement = _children[_cursor].Measure(space, page);
            if (measurement.Fit == Fit.None)
            {
                break;
            }

            canvas.PushOffset(0, height);
            advanced |= _children[_cursor].Draw(space, canvas, page);
            canvas.PopOffset();
            height += measurement.Size.Height;
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
        foreach (var child in _children)
        {
            child.Reset();
        }
    }
}
