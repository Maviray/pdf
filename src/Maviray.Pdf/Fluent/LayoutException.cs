namespace Maviray.Pdf;

/// <summary>
/// The error a document's generation throws for content that can't be laid out: content that can never fit, a header
/// or footer that doesn't fit in full, an element that splits across pages without drawing anything, or a page count
/// that keeps changing the layout. The message names the innermost element that can't fit, the reason, and the space
/// it had.
/// </summary>
public sealed class LayoutException : Exception
{
    /// <summary>Creates an exception with a default message, no element path and no available size.</summary>
    public LayoutException()
        : this("The content can't be laid out.")
    {
    }

    /// <summary>Creates an exception with <paramref name="message"/>, no element path and no available size.</summary>
    /// <param name="message">What went wrong.</param>
    public LayoutException(string message)
        : this(message, null)
    {
    }

    /// <summary>Creates an exception with <paramref name="message"/> and its cause, no element path and no available size.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public LayoutException(string message, Exception? innerException)
        : base(message, innerException)
    {
        ElementPath = "";
        AvailableWidth = double.NaN;
        AvailableHeight = double.NaN;
    }

    /// <param name="elementPath">The path of the innermost element that can't fit, such as
    /// <c>Page 3 &gt; Content &gt; Column &gt; Item 2 &gt; Text</c>.</param>
    /// <param name="reason">Why, as a sentence that follows the path and a colon.</param>
    /// <param name="availableWidth">The width the element had, in points, or NaN.</param>
    /// <param name="availableHeight">The height the element had, in points, or NaN.</param>
    internal LayoutException(string elementPath, string reason, double availableWidth, double availableHeight)
        : base(elementPath + ": " + reason)
    {
        ElementPath = elementPath;
        AvailableWidth = availableWidth;
        AvailableHeight = availableHeight;
    }

    /// <summary>
    /// The path of the innermost element that can't fit: the names from the page down, such as
    /// <c>Page 3 &gt; Content &gt; Column &gt; Item 2 &gt; Row &gt; Flex 1 &gt; Text</c>. <c>Document</c> when the error isn't
    /// about one element, and empty when the exception wasn't made by a layout.
    /// </summary>
    public string ElementPath { get; }

    /// <summary>The width the element had, in points. NaN when the error isn't about one space.</summary>
    public double AvailableWidth { get; }

    /// <summary>The height the element had, in points. NaN when the error isn't about one space.</summary>
    public double AvailableHeight { get; }
}
