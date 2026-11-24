using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Layout;

/// <summary>
/// A test element of lines, like a paragraph: as many lines as fit from its cursor. All of them is
/// <see cref="Fit.Full"/>, some is <see cref="Fit.Partial"/>, and none is <see cref="Fit.None"/>. It draws one rectangle
/// per line, and moves its cursor past them.
/// </summary>
internal sealed class SplittingBlock(string name, double width, double lineHeight, int lines, RgbColor color) : Element(name)
{
    private int _cursor;

    public override Measurement Measure(Size available, PageContext page)
    {
        var remaining = lines - _cursor;
        var fitting = Fitting(available);
        if (remaining == 0 || fitting == remaining)
        {
            return Measurement.Full(new Size(remaining == 0 ? 0 : width, remaining * lineHeight), this);
        }

        return fitting == 0
            ? Measurement.None(this, available, new Size(width, lineHeight))
            : Measurement.Partial(new Size(width, fitting * lineHeight), this, available);
    }

    public override bool Draw(Size available, ICanvas canvas, PageContext page)
    {
        var fitting = Fitting(available);
        for (var i = 0; i < fitting; i++)
        {
            canvas.FillRectangle(0, i * lineHeight, width, lineHeight, color);
        }

        _cursor += fitting;
        return fitting > 0 || _cursor == lines;
    }

    public override void Reset() => _cursor = 0;

    /// <summary>How many of the remaining lines fit, with a tolerance for the sums of line heights.</summary>
    private int Fitting(Size available) => width > available.Width
        ? 0
        : Math.Min(lines - _cursor, (int)Math.Floor((available.Height / lineHeight) + 1e-9));
}
