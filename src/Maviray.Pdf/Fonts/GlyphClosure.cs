namespace Maviray.Pdf.Fonts;

/// <summary>
/// Works out which glyphs a subset holds (spec F-08 step 1). The subset starts with the requested glyphs, one per CID,
/// duplicates included. Every component they need, at any depth, that isn't in the list yet is appended, in the order
/// the subset glyphs are visited. Only glyphs the subset reaches are read, so a broken glyph the document never uses
/// doesn't matter (F-05).
/// </summary>
internal static class GlyphClosure
{
    /// <summary>The most glyphs a TrueType font can hold (F-07).</summary>
    public const int MaxGlyphs = 65535;

    /// <summary>The most composites one chain of components may hold (F-05).</summary>
    public const int MaxNesting = 16;

    /// <param name="font">The font the glyphs come from.</param>
    /// <param name="glyphs">The original glyph of each CID, from CID 0, which must be <c>.notdef</c> (glyph 0).</param>
    /// <returns>The original glyph of every subset glyph: the input, then the components it needs.</returns>
    /// <exception cref="InvalidDataException">A glyph the subset reaches is damaged, is part of a cycle, or nests more
    /// than 16 composites.</exception>
    /// <exception cref="InvalidOperationException">The subset would hold more than 65,535 glyphs.</exception>
    public static ushort[] Resolve(TrueTypeFont font, IReadOnlyList<ushort> glyphs)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(glyphs);
        if (glyphs.Count == 0 || glyphs[0] != 0)
        {
            throw new ArgumentException("Subset glyph 0 must be .notdef, the font's glyph 0.", nameof(glyphs));
        }

        if (glyphs.Count > MaxGlyphs)
        {
            throw TooManyGlyphs();
        }

        var list = new List<ushort>(glyphs.Count);
        var listed = new HashSet<ushort>();
        foreach (var glyph in glyphs)
        {
            if (glyph >= font.GlyphCount)
            {
                throw new ArgumentOutOfRangeException(nameof(glyphs), glyph, $"Glyph {glyph} is past the font's {font.GlyphCount} glyphs.");
            }

            list.Add(glyph);
            listed.Add(glyph);
        }

        var walk = new Dictionary<ushort, GlyphComponent[]>();
        var depths = new Dictionary<ushort, int>();
        for (var i = 0; i < list.Count; i++)
        {
            var components = ComponentsOf(font, list[i], walk);
            if (components.Length == 0)
            {
                continue;
            }

            _ = Depth(font, list[i], walk, depths, []);
            foreach (var component in components)
            {
                if (!listed.Add(component.GlyphId))
                {
                    continue;
                }

                if (list.Count == MaxGlyphs)
                {
                    throw TooManyGlyphs();
                }

                list.Add(component.GlyphId);
            }
        }

        return [.. list];
    }

    /// <summary>The glyph's components, read once. Reading the header refuses data shorter than one.</summary>
    private static GlyphComponent[] ComponentsOf(TrueTypeFont font, ushort glyph, Dictionary<ushort, GlyphComponent[]> walk)
    {
        if (!walk.TryGetValue(glyph, out var components))
        {
            components = font.GetGlyphHeader(glyph) is { IsComposite: true }
                ? CompositeGlyph.ReadComponents(font.GetGlyphData(glyph), font.GlyphCount)
                : [];
            walk[glyph] = components;
        }

        return components;
    }

    /// <summary>
    /// The number of composites in the longest chain from <paramref name="glyph"/> down: 0 for a simple or empty glyph.
    /// <paramref name="path"/> holds the composites above it, so the recursion stops at <see cref="MaxNesting"/> + 1
    /// frames, and a glyph already on it is a cycle.
    /// </summary>
    private static int Depth(
        TrueTypeFont font, ushort glyph, Dictionary<ushort, GlyphComponent[]> walk, Dictionary<ushort, int> depths, List<ushort> path)
    {
        if (depths.TryGetValue(glyph, out var known))
        {
            return known;
        }

        var components = ComponentsOf(font, glyph, walk);
        if (components.Length == 0)
        {
            depths[glyph] = 0;
            return 0;
        }

        var start = path.IndexOf(glyph);
        if (start >= 0)
        {
            throw new InvalidDataException(
                $"The 'glyf' table has a cycle of composite glyphs: {string.Join(" → ", path.Skip(start).Append(glyph))}.");
        }

        if (path.Count >= MaxNesting)
        {
            throw TooDeep(path[0]);
        }

        path.Add(glyph);
        var deepest = 0;
        foreach (var component in components)
        {
            deepest = Math.Max(deepest, Depth(font, component.GlyphId, walk, depths, path));
        }

        path.RemoveAt(path.Count - 1);
        var depth = deepest + 1;
        if (depth > MaxNesting)
        {
            throw TooDeep(glyph);
        }

        depths[glyph] = depth;
        return depth;
    }

    private static InvalidDataException TooDeep(ushort glyph) =>
        new($"The 'glyf' table has composite glyph {glyph} nested deeper than {MaxNesting} levels.");

    private static InvalidOperationException TooManyGlyphs() =>
        new("The font's subset would hold more than 65,535 glyphs, the most a TrueType font can have.");
}
