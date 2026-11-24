using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Tests.Layout;

/// <summary>
/// L-03's contract, checked on a fresh element at each size, page after page until it is drawn in full or can't fit:
/// <list type="bullet">
/// <item><c>Measure</c> twice gives the same answer;</item>
/// <item>the size never exceeds the available size;</item>
/// <item><c>None</c> has no size;</item>
/// <item>after <c>Partial</c> or <c>Full</c>, <c>Draw</c> returns that it advanced;</item>
/// <item>measured again at its measured size, it gives the same fit and size, because containers draw each child
/// at its measured size (P1.10 plan, ruling 1);</item>
/// <item><c>Reset()</c> brings back the first measurement.</item>
/// </list>
/// It also measures at an infinite width and the first size's height, which L-03 says every element handles: the
/// answer repeats and its width is finite. Nothing is drawn at an infinite width. Every element type of the engine
/// runs it (P1.9, P1.10, P1.11).
/// </summary>
internal static class ElementContract
{
    private const int MaxPages = 10_000;

    public static void Check(Func<Element> make, params Size[] sizes)
    {
        ArgumentNullException.ThrowIfNull(make);
        var page = new PageContext(1, 1, isRenderPass: true);
        if (sizes.Length > 0)
        {
            CheckInfiniteWidth(make(), new Size(double.PositiveInfinity, sizes[0].Height), page);
        }

        foreach (var size in sizes)
        {
            var element = make();
            var first = element.Measure(size, page);
            for (var pages = 0; ; pages++)
            {
                Assert.True(pages < MaxPages, $"{element.Path} at {size} wasn't done after {MaxPages} pages.");
                var measurement = element.Measure(size, page);
                var again = element.Measure(size, page);
                Assert.True(Same(measurement, again), $"{element.Path} at {size} measured {Show(measurement)}, then {Show(again)}.");
                Assert.True(
                    measurement.Size.Width <= size.Width + 1e-9 && measurement.Size.Height <= size.Height + 1e-9,
                    $"{element.Path} at {size} measured {measurement.Size}, larger than the space.");
                if (measurement.Fit == Fit.None)
                {
                    Assert.Equal(default, measurement.Size);
                    break;
                }

                var atSize = element.Measure(measurement.Size, page);
                Assert.True(
                    atSize.Fit == measurement.Fit && atSize.Size == measurement.Size,
                    $"{element.Path} at {size} measured {Show(measurement)}, and at its measured size {Show(atSize)}.");

                var advanced = element.Draw(size, NullCanvas.Instance, page);
                Assert.True(advanced, $"{element.Path} at {size} was {measurement.Fit}, and its draw didn't advance.");
                if (measurement.Fit == Fit.Full)
                {
                    break;
                }
            }

            element.Reset();
            var reset = element.Measure(size, page);
            Assert.True(Same(first, reset), $"{element.Path} at {size} measured {Show(first)} first, and {Show(reset)} after Reset.");
        }
    }

    private static void CheckInfiniteWidth(Element element, Size size, PageContext page)
    {
        var measurement = element.Measure(size, page);
        Assert.True(Same(measurement, element.Measure(size, page)), $"{element.Path} at an infinite width measured differently twice.");
        Assert.True(double.IsFinite(measurement.Size.Width), $"{element.Path} at an infinite width measured {measurement.Size}.");
        if (measurement.Fit != Fit.None)
        {
            // A Row gives an Auto item the width it measured at infinity, so the item must fit there the same way.
            var atWidth = element.Measure(new Size(measurement.Size.Width, size.Height), page);
            Assert.True(
                atWidth.Fit == measurement.Fit && atWidth.Size == measurement.Size,
                $"{element.Path} measured {Show(measurement)} at an infinite width, and {Show(atWidth)} at that width.");
        }
    }

    private static bool Same(Measurement a, Measurement b) =>
        a.Fit == b.Fit && a.Size == b.Size && ReferenceEquals(a.Source, b.Source) && a.Space == b.Space && a.Needed == b.Needed;

    private static string Show(Measurement m) => $"{m.Fit} {m.Size} from {m.Source.Path}";
}
