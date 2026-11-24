using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Layout;

/// <summary>
/// A test element of one fixed size: <see cref="Fit.Full"/> when it fits, and otherwise <see cref="Fit.None"/>, with its
/// size as what it needs. It draws one rectangle in its colour.
/// </summary>
internal sealed class FixedBlock(string name, double width, double height, RgbColor color) : Element(name)
{
    public double Width { get; } = width;

    public double Height { get; } = height;

    public RgbColor Color { get; } = color;

    public override Measurement Measure(Size available, PageContext page) =>
        Width <= available.Width && Height <= available.Height
            ? Measurement.Full(new Size(Width, Height), this)
            : Measurement.None(this, new Size(Width, Height));

    public override bool Draw(Size available, ICanvas canvas, PageContext page)
    {
        canvas.FillRectangle(0, 0, Width, Height, Color);
        return true;
    }

    public override void Reset()
    {
    }
}
