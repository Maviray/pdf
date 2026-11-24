using Maviray.Pdf.Drawing;

namespace Maviray.Pdf.Layout;

/// <summary>
/// A node of the element tree (spec L-03). It is measured, then drawn, and reset before each pass.
/// <list type="bullet">
/// <item><see cref="Measure"/> has no side effects, and the same state and size give the same answer.</item>
/// <item>A measured size never exceeds the available size, and <see cref="Fit.None"/> carries no size.</item>
/// <item><see cref="Draw"/> draws exactly what <see cref="Measure"/> said fits at that size, and advances the
/// element's state. After <see cref="Fit.Partial"/> it advances at least one unit: a line, an item or a child.</item>
/// <item>Available sizes are finite, except that the width may be infinite when a Row measures an <c>Auto</c> item
/// (L-06).</item>
/// </list>
/// </summary>
internal abstract class Element
{
    /// <param name="name">The element's name in a path (L-05), such as <c>Column</c>, <c>Item 2</c> or <c>Text</c>.</param>
    protected Element(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
    }

    public string Name { get; }

    /// <summary>The element that holds this one, or null for the top of a tree, such as a page slot.</summary>
    public Element? Parent { get; private set; }

    /// <summary>The names from the top of the tree down to this element, joined with <c> &gt; </c>.</summary>
    public string Path => Parent is null ? Name : Parent.Path + " > " + Name;

    /// <summary>
    /// Whether the element, or one inside it, shows the page count. The engine then runs layout passes to find the
    /// count before it draws (L-04). A container returns whether any child does.
    /// </summary>
    public virtual bool ShowsPageCount => false;

    /// <summary>How much of what is left of the element fits in <paramref name="available"/>.</summary>
    public abstract Measurement Measure(Size available, PageContext page);

    /// <summary>
    /// Draws what <see cref="Measure"/> said fits in <paramref name="available"/>, with the element's top left at the
    /// canvas's origin, and moves the element past it.
    /// </summary>
    /// <returns>
    /// Whether the element advanced. A draw after <see cref="Fit.Full"/> always advances, because it finishes the
    /// element. A container advanced if a child advanced or its own cursor moved.
    /// </returns>
    public abstract bool Draw(Size available, ICanvas canvas, PageContext page);

    /// <summary>Returns the element, and every element inside it, to its state before the first draw.</summary>
    public abstract void Reset();

    /// <summary>Makes <paramref name="child"/> this element's child, so its path runs through this element.</summary>
    /// <exception cref="InvalidOperationException">The child already belongs to an element.</exception>
    protected T Adopt<T>(T child)
        where T : Element
    {
        ArgumentNullException.ThrowIfNull(child);
        if (child.Parent is not null)
        {
            throw new InvalidOperationException($"{child.Name} already belongs to {child.Parent.Path}, and an element has one parent.");
        }

        child.Parent = this;
        return child;
    }
}
