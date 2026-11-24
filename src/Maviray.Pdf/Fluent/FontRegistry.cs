using Maviray.Pdf.Fonts;

namespace Maviray.Pdf;

/// <summary>
/// The fonts a document can use, by family, weight and style. A font loads when it is registered, so one that can't be
/// used fails there. A registry is safe to use from many threads, and <see cref="Default"/> is the process-wide one. A
/// document can use another one through its settings.
/// </summary>
public sealed class FontRegistry
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, List<FontFace>> _faces = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _families = [];

    /// <summary>The process-wide registry, which documents use unless their settings name another.</summary>
    public static FontRegistry Default { get; } = new();

    /// <summary>The families, in the order they were first registered, spelled as then.</summary>
    internal IReadOnlyList<string> Families
    {
        get
        {
            lock (_lock)
            {
                return [.. _families];
            }
        }
    }

    /// <summary>Registers a TrueType font as a face of <paramref name="family"/>.</summary>
    /// <param name="family">The family's name, which documents ask for. Families compare by ordinal, ignoring case.</param>
    /// <param name="data">The font file.</param>
    /// <param name="weight">The face's weight, or null for the font's own (<c>usWeightClass</c>).</param>
    /// <param name="italic">Whether the face is italic, or null for the font's own flag.</param>
    /// <returns>This registry.</returns>
    /// <exception cref="ArgumentException"><paramref name="family"/> is blank, or the family already has a face of that
    /// weight and style.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="weight"/> isn't from 1 to 1,000.</exception>
    /// <exception cref="NotSupportedException">The font is one the library can't embed correctly.</exception>
    /// <exception cref="InvalidDataException">The font is damaged.</exception>
    public FontRegistry Register(string family, byte[] data, FontWeight? weight = null, bool? italic = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        Check(family, weight);
        return Add(family, TrueTypeFont.Load(data), weight, italic);
    }

    /// <summary>Registers a TrueType font, read from <paramref name="data"/> to its end, as a face of
    /// <paramref name="family"/>. The stream isn't disposed.</summary>
    /// <param name="family">The family's name, which documents ask for. Families compare by ordinal, ignoring case.</param>
    /// <param name="data">The font file.</param>
    /// <param name="weight">The face's weight, or null for the font's own (<c>usWeightClass</c>).</param>
    /// <param name="italic">Whether the face is italic, or null for the font's own flag.</param>
    /// <returns>This registry.</returns>
    /// <exception cref="ArgumentException"><paramref name="family"/> is blank, or the family already has a face of that
    /// weight and style.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="weight"/> isn't from 1 to 1,000.</exception>
    /// <exception cref="NotSupportedException">The font is one the library can't embed correctly.</exception>
    /// <exception cref="InvalidDataException">The font is damaged.</exception>
    public FontRegistry Register(string family, Stream data, FontWeight? weight = null, bool? italic = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        Check(family, weight);
        using var copy = new MemoryStream();
        data.CopyTo(copy);
        return Add(family, TrueTypeFont.Load(copy.GetBuffer().AsSpan(0, (int)copy.Length)), weight, italic);
    }

    /// <summary>Registers the TrueType font file at <paramref name="path"/> as a face of <paramref name="family"/>.</summary>
    /// <param name="family">The family's name, which documents ask for. Families compare by ordinal, ignoring case.</param>
    /// <param name="path">The font file's path.</param>
    /// <param name="weight">The face's weight, or null for the font's own (<c>usWeightClass</c>).</param>
    /// <param name="italic">Whether the face is italic, or null for the font's own flag.</param>
    /// <returns>This registry.</returns>
    /// <exception cref="ArgumentException"><paramref name="family"/> is blank, or the family already has a face of that
    /// weight and style.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="weight"/> isn't from 1 to 1,000.</exception>
    /// <exception cref="NotSupportedException">The font is one the library can't embed correctly.</exception>
    /// <exception cref="InvalidDataException">The font is damaged.</exception>
    public FontRegistry Register(string family, string path, FontWeight? weight = null, bool? italic = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        Check(family, weight);
        return Add(family, TrueTypeFont.Load(File.ReadAllBytes(path)), weight, italic);
    }

    /// <summary>
    /// The face a request gets (L-09), or null when the family isn't registered. An italic request uses the family's
    /// italic faces if it has any, and otherwise its upright ones; an upright request the other way round. The weight
    /// is then chosen among them by CSS Fonts Level 4. The match is substituted when the style or weight differs.
    /// </summary>
    internal FontMatch? Match(string family, int weight, bool italic)
    {
        lock (_lock)
        {
            if (!_faces.TryGetValue(family, out var faces))
            {
                return null;
            }

            var styled = faces.Where(face => face.Italic == italic).ToList();
            var styleSubstituted = styled.Count == 0;
            if (styleSubstituted)
            {
                styled = faces;
            }

            var chosen = WeightMatching.Choose(weight, [.. styled.Select(face => face.Weight)]);
            return new FontMatch(styled.First(face => face.Weight == chosen), styleSubstituted || chosen != weight);
        }
    }

    private static void Check(string family, FontWeight? weight)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(family);
        if (weight is { } value && (int)value is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), value, "A weight must be from 1 to 1,000.");
        }
    }

    private FontRegistry Add(string family, TrueTypeFont font, FontWeight? weight, bool? italic)
    {
        var faceWeight = weight is { } value ? (int)value : font.WeightClass is >= 1 and <= 1000 ? font.WeightClass : 400;
        var face = new FontFace(family, faceWeight, italic ?? font.IsItalic, font);
        lock (_lock)
        {
            if (!_faces.TryGetValue(family, out var faces))
            {
                faces = [];
                _faces.Add(family, faces);
                _families.Add(family);
            }

            if (faces.Exists(existing => existing.Weight == face.Weight && existing.Italic == face.Italic))
            {
                throw new ArgumentException(
                    $"The family '{family}' already has a{(face.Italic ? "n italic" : "n upright")} face of weight {face.Weight}.",
                    nameof(family));
            }

            faces.Add(face);
        }

        return this;
    }
}
