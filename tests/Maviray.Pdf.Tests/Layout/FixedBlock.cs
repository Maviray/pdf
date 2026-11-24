using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Layout;

/// <summary>
/// A test element of one fixed size: <see cref="Fit.Full"/> when it fits, and otherwise <see cref="Fit.None"/>, with its
/// size as what it needs. It draws one rectangle in its colour. With <c>advances</c> false, its draw says it didn't
/// advance, which breaks L-03's contract, so tests can show what a container does with such a child.
/// </summary>
internal sealed class FixedBlock(string name, double width, double height, RgbColor color, bool advances = true) : Element(name)
{
    public double Width { get; } = width;

    public double Height { get; } = height;

    public RgbColor Color { get; } = color;

    public override Measurement Measure(Size available, PageContext page) =>
        LayoutTolerance.Fits(Width, available.Width) && LayoutTolerance.Fits(Height, available.Height)
            ? Measurement.Full(new Size(Width, Height), this)
            : Measurement.None(this, available, new Size(Width, Height));

    public override bool Draw(Size available, ICanvas canvas, PageContext page)
    {
        canvas.FillRectangle(0, 0, Width, Height, Color);
        return advances;
    }

    public override void Reset()
    {
    }
}
