using System.Runtime.CompilerServices;

namespace Maviray.Pdf;

/// <summary>The Noto Sans fonts compiled into this package, for documents that bring no fonts of their own.</summary>
public static class DefaultFonts
{
    private const string Family = "Noto Sans";

    private static readonly (string Resource, FontWeight Weight, bool Italic)[] Faces =
    [
        ("NotoSans-Regular.ttf", FontWeight.Normal, false),
        ("NotoSans-Bold.ttf", FontWeight.Bold, false),
        ("NotoSans-Italic.ttf", FontWeight.Normal, true),
        ("NotoSans-BoldItalic.ttf", FontWeight.Bold, true),
    ];

    // The registries this method has filled, so a second call on one returns at once. A registry that is collected
    // leaves the table by itself.
    private static readonly ConditionalWeakTable<FontRegistry, object> Filled = new();
    private static readonly object Marker = new();
    private static readonly Lock Gate = new();

    /// <summary>
    /// Registers the family <c>Noto Sans</c>, the family of the built-in default text style, with its regular, bold,
    /// italic and bold italic faces. A second call on the same registry does nothing, so setup code may run more than
    /// once.
    /// </summary>
    /// <param name="registry">The registry, such as <see cref="FontRegistry.Default"/>.</param>
    /// <returns>The registry.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is null.</exception>
    /// <exception cref="ArgumentException">The registry already has a Noto Sans face that this method didn't register.</exception>
    public static FontRegistry RegisterNotoSans(this FontRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        lock (Gate)
        {
            if (Filled.TryGetValue(registry, out _))
            {
                return registry;
            }

            foreach (var (resource, weight, italic) in Faces)
            {
                using var font = typeof(DefaultFonts).Assembly.GetManifestResourceStream(resource)
                    ?? throw new InvalidOperationException($"Maviray.Pdf.Fonts.Default is missing its resource {resource}.");
                registry.Register(Family, font, weight, italic);
            }

            Filled.Add(registry, Marker);
        }

        return registry;
    }
}
