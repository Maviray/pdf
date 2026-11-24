namespace Maviray.Pdf.Layout;

/// <summary>
/// What an element's <see cref="Element.Measure"/> found (spec L-03): its fit, its size, and the element the answer
/// comes from, so an error can name the innermost one (L-05).
/// </summary>
internal readonly struct Measurement
{
    private Measurement(Fit fit, Size size, Element source, Size? needed)
    {
        ArgumentNullException.ThrowIfNull(source);
        Fit = fit;
        Size = size;
        Source = source;
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

    /// <summary>For <see cref="Fit.None"/>, the size the source needs, when it is known.</summary>
    public Size? Needed { get; }

    public static Measurement Full(Size size, Element source) => new(Fit.Full, size, source, null);

    public static Measurement Partial(Size size, Element source) => new(Fit.Partial, size, source, null);

    public static Measurement None(Element source, Size? needed = null) => new(Fit.None, default, source, needed);
}
