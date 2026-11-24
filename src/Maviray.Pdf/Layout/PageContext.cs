namespace Maviray.Pdf.Layout;

/// <summary>What an element may need to know about the page it is measured or drawn on (spec L-04).</summary>
/// <param name="pageNumber">The page's number, counted from 1 through the whole document.</param>
/// <param name="pageCount">What a page-count span shows on this page: the page number in the first layout pass, and
/// the count the pass before ended with in every later one.</param>
/// <param name="isRenderPass">Whether this is the pass that draws on the output, not a layout pass.</param>
internal sealed class PageContext(int pageNumber, int pageCount, bool isRenderPass)
{
    public int PageNumber { get; } = pageNumber;

    public int PageCount { get; } = pageCount;

    public bool IsRenderPass { get; } = isRenderPass;
}
