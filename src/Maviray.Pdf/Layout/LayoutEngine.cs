using Maviray.Pdf.Drawing;

namespace Maviray.Pdf.Layout;

/// <summary>
/// Lays page sets out on pages and draws them (spec L-04). Every loop stops: content that can never fit, a header or
/// footer that doesn't fit in full, and an element that splits without drawing anything each throw a
/// <see cref="LayoutException"/> on the page where it happens, naming the element.
/// </summary>
internal static class LayoutEngine
{
    private const int MaxLayoutPasses = 3;

    /// <summary>
    /// Generates <paramref name="sets"/> on <paramref name="canvas"/>. When a slot shows the page count, layout passes on
    /// the null canvas find it first: the first shows each page's own number, each later one the count the pass before
    /// ended with, until a pass ends with the count it showed. The render pass then shows that count.
    /// </summary>
    /// <returns>The number of pages.</returns>
    /// <exception cref="LayoutException">Content can't be laid out, or the page count still changes the layout after
    /// three layout passes.</exception>
    public static int Generate(IReadOnlyList<PageSet> sets, ICanvas canvas)
    {
        ArgumentNullException.ThrowIfNull(sets);
        ArgumentNullException.ThrowIfNull(canvas);
        if (!sets.Any(set => set.ShowsPageCount))
        {
            return RunPass(sets, canvas, null, isRenderPass: true);
        }

        var counts = new List<int>(MaxLayoutPasses);
        int? shown = null;
        for (var pass = 1; ; pass++)
        {
            var count = RunPass(sets, NullCanvas.Instance, shown, isRenderPass: false);
            counts.Add(count);
            if (pass >= 2 && count == shown)
            {
                return RunPass(sets, canvas, count, isRenderPass: true);
            }

            if (pass == MaxLayoutPasses)
            {
                throw new LayoutException(
                    "Document",
                    $"the page count keeps changing the layout: the passes ended with {counts[0]}, {counts[1]} and {counts[2]} pages.",
                    double.NaN,
                    double.NaN);
            }

            shown = count;
        }
    }

    /// <summary>One pass over every set, with the tree reset first. <paramref name="shown"/> is the count page-count
    /// spans show, or null for each page's own number.</summary>
    private static int RunPass(IReadOnlyList<PageSet> sets, ICanvas canvas, int? shown, bool isRenderPass)
    {
        foreach (var set in sets)
        {
            set.Reset();
        }

        var pageNumber = 0;
        foreach (var set in sets)
        {
            bool done;
            do
            {
                pageNumber++;
                done = RunPage(set, canvas, new PageContext(pageNumber, shown ?? pageNumber, isRenderPass));
            }
            while (!done);
        }

        return pageNumber;
    }

    /// <summary>One page of <paramref name="set"/>. Returns whether its content ended on it.</summary>
    private static bool RunPage(PageSet set, ICanvas canvas, PageContext page)
    {
        canvas.BeginPage(set.Width, set.Height);
        if (set.Background is { } background)
        {
            canvas.FillRectangle(0, 0, set.Width, set.Height, background);
        }

        var margins = set.Margins;
        var inner = new Size(Math.Max(0, set.Width - margins.Left - margins.Right), Math.Max(0, set.Height - margins.Top - margins.Bottom));
        var headerHeight = MeasureRepeating(set.Header, inner, page, "header");
        var footerHeight = MeasureRepeating(set.Footer, new Size(inner.Width, Math.Max(0, inner.Height - headerHeight)), page, "footer");

        var space = new Size(inner.Width, Math.Max(0, inner.Height - headerHeight - footerHeight));
        var content = set.Content.Measure(space, page);
        if (content.Fit == Fit.None)
        {
            throw Error(page, content.Source, "it can never fit. The space is " + LayoutErrors.Size(space) + Needs(content) + ".", space);
        }

        canvas.PushOffset(margins.Left, margins.Top + headerHeight);
        var advanced = set.Content.Draw(space, canvas, page);
        canvas.PopOffset();
        if (content.Fit == Fit.Partial && !advanced)
        {
            throw Error(page, content.Source, "it split across pages but drew nothing of itself on this page. The space is " + LayoutErrors.Size(space) + ".", space);
        }

        DrawRepeating(set.Header, canvas, page, margins.Left, margins.Top, new Size(inner.Width, headerHeight));
        DrawRepeating(set.Footer, canvas, page, margins.Left, set.Height - margins.Bottom - footerHeight, new Size(inner.Width, footerHeight));
        canvas.EndPage();
        return content.Fit == Fit.Full;
    }

    /// <summary>The height of a header or footer, which must fit in full because it repeats on every page; 0 for none.</summary>
    private static double MeasureRepeating(Slot? slot, Size space, PageContext page, string kind)
    {
        if (slot is null)
        {
            return 0;
        }

        var measurement = slot.Measure(space, page);
        if (measurement.Fit != Fit.Full)
        {
            throw Error(
                page,
                measurement.Source,
                $"the {kind} must fit in full, because it repeats on every page. The space is {LayoutErrors.Size(space)}{Needs(measurement)}.",
                space);
        }

        return measurement.Size.Height;
    }

    /// <summary>Draws a header or footer at its measured height, then resets it, so it draws again on the next page.</summary>
    private static void DrawRepeating(Slot? slot, ICanvas canvas, PageContext page, double x, double y, Size size)
    {
        if (slot is null)
        {
            return;
        }

        canvas.PushOffset(x, y);
        _ = slot.Draw(size, canvas, page);
        canvas.PopOffset();
        slot.Reset();
    }

    private static string Needs(Measurement measurement) =>
        measurement.Needed is { } needed ? ", and it needs " + LayoutErrors.Size(needed) : "";

    private static LayoutException Error(PageContext page, Element source, string reason, Size space) =>
        new($"Page {page.PageNumber} > {source.Path}", reason, space.Width, space.Height);
}
