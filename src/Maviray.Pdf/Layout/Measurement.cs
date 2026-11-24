namespace Maviray.Pdf.Layout;

/// <summary>
/// What an element's <see cref="Element.Measure"/> found (spec L-03): its fit and its size, and the element the answer
/// comes from with the space that element had, so an error can name the innermost one and its own space (L-05).
/// </summary>
internal readonly struct Measurement
{
    private Measurement(Fit fit, Size size, Element source, Size space, Size? needed)
    {
        ArgumentNullException.ThrowIfNull(source);
        Fit = fit;
        Size = size;
        Source = source;
        Space = space;
        Needed = needed;
    }

    public Fit Fit { get; }

    /// <summary>The size of what fits. Zero for <see cref="Fit.None"/>, which carries no size.</summary>
    public Size Size { get; }

    /// <summary>
    /// For <see cref="Fit.None"/>, the innermost element that can't fit. For <see cref="Fit.Partial"/>, the innermost
    /// element that splits. For <see cref="Fit.Full"/>, the element measured.
    /// </summary>
    public Element Source { get; }

    /// <summary>The space <see cref="Source"/> was measured in, which an error reports. Zero for <see cref="Fit.Full"/>.</summary>
    public Size Space { get; }

    /// <summary>For <see cref="Fit.None"/>, the size the source needs, when it is known.</summary>
    public Size? Needed { get; }

    public static Measurement Full(Size size, Element source) => new(Fit.Full, size, source, default, null);

    /// <summary>Part of <paramref name="source"/> fits in <paramref name="space"/>, taking <paramref name="size"/>.</summary>
    public static Measurement Partial(Size size, Element source, Size space) => new(Fit.Partial, size, source, space, null);

    /// <summary>Nothing of <paramref name="source"/> fits in <paramref name="space"/>.</summary>
    public static Measurement None(Element source, Size space, Size? needed = null) => new(Fit.None, default, source, space, needed);

    /// <summary>The same answer with another size, for a container that passes a child's split up with its own size.</summary>
    public Measurement WithSize(Size size) => new(Fit, Fit == Fit.None ? default : size, Source, Space, Needed);
}
