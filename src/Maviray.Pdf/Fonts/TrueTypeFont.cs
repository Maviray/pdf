namespace Maviray.Pdf.Fonts;

/// <summary>
/// A static TrueType font, read once and immutable, so it can be shared across documents and threads (spec F-02 to
/// F-06). It keeps its own copy of the file. All values are in font units. Loading throws only
/// <see cref="NotSupportedException"/>, for a font the library can't embed correctly, and
/// <see cref="InvalidDataException"/>, for a damaged one; once the <c>name</c> table is read, each message starts with
/// the font's PostScript name.
/// </summary>
internal sealed class TrueTypeFont
{
    private static readonly string[] RequiredTables = ["head", "hhea", "maxp", "hmtx", "cmap", "loca", "glyf"];
    private static readonly string[] ColourTables = ["COLR", "CBDT", "sbix", "SVG "];

    private readonly byte[] _file;
    private readonly TableDirectory _directory;
    private readonly CharacterMap _characterMap;
    private readonly HorizontalMetricsTable _metrics;
    private readonly GlyphLocationTable _locations;

    private TrueTypeFont(byte[] file, TableDirectory directory, NamingTable names)
    {
        _file = file;
        _directory = directory;
        (PostScriptName, FamilyName, SubfamilyName) = (names.PostScriptName, names.FamilyName, names.SubfamilyName);

        if (directory.SfntVersion == TableDirectory.CffVersion)
        {
            throw new NotSupportedException(
                "The font has CFF outlines (an OpenType .otf font), which aren't supported. Use a TrueType (.ttf) version of the font.");
        }

        if (directory.HasTable("fvar"))
        {
            throw new NotSupportedException("The font is a variable font, which isn't supported. Use a static instance of it.");
        }

        foreach (var tag in ColourTables)
        {
            if (directory.HasTable(tag))
            {
                throw new NotSupportedException($"The font is a colour font (it has a '{tag}' table), which isn't supported.");
            }
        }

        foreach (var tag in RequiredTables)
        {
            if (!directory.HasTable(tag))
            {
                throw new InvalidDataException($"The font has no '{tag}' table, which every TrueType font needs.");
            }
        }

        var head = HeaderTable.Parse(Table("head"));
        var glyphCount = MaximumProfileTable.Parse(Table("maxp")).GlyphCount;
        var horizontalHeader = HorizontalHeaderTable.Parse(Table("hhea"), glyphCount);
        _metrics = HorizontalMetricsTable.Parse(Table("hmtx"), horizontalHeader.NumberOfHMetrics, glyphCount);
        _locations = GlyphLocationTable.Parse(Table("loca"), head.IndexToLocFormat, glyphCount, Table("glyf").Length);
        _characterMap = CharacterMap.Parse(Table("cmap"), glyphCount);
        var os2 = directory.HasTable("OS/2") ? Os2Table.Parse(Table("OS/2")) : null;
        var post = directory.HasTable("post") ? PostTable.Parse(Table("post")) : null;

        UnitsPerEm = head.UnitsPerEm;
        GlyphCount = glyphCount;
        FontBounds = head.Bounds;
        MacStyle = head.MacStyle;
        HorizontalHeaderMetrics = horizontalHeader.Metrics;
        TypographicMetrics = os2?.Typographic;
        WindowsMetrics = os2?.Windows;
        FsType = os2?.FsType ?? 0;
        Embedding = EmbeddingPermissions.Resolve(FsType);
        WeightClass = os2?.WeightClass ?? ((MacStyle & 0x0001) != 0 ? 700 : 400);
        IsItalic = ((os2?.FsSelection ?? 0) & 0x0001) != 0 || (MacStyle & 0x0002) != 0;
        CapHeight = os2?.CapHeight;
        ItalicAngle = post?.ItalicAngle ?? 0;
        IsFixedPitch = post?.IsFixedPitch ?? false;
        UnderlinePosition = post?.UnderlinePosition ?? 0;
        UnderlineThickness = post?.UnderlineThickness ?? 0;
    }

    public string? PostScriptName { get; }

    public string? FamilyName { get; }

    public string? SubfamilyName { get; }

    public int UnitsPerEm { get; }

    public int GlyphCount { get; }

    /// <summary>The bounding box of all glyphs, from <c>head</c>.</summary>
    public GlyphBounds FontBounds { get; }

    public ushort MacStyle { get; }

    public LineMetrics HorizontalHeaderMetrics { get; }

    /// <summary>The <c>OS/2</c> typographic metrics, or null without <c>OS/2</c>.</summary>
    public LineMetrics? TypographicMetrics { get; }

    /// <summary>The <c>OS/2</c> Windows metrics as (usWinAscent, −usWinDescent, 0), or null without <c>OS/2</c>.</summary>
    public LineMetrics? WindowsMetrics { get; }

    public ushort FsType { get; }

    public FontEmbedding Embedding { get; }

    public int WeightClass { get; }

    public bool IsItalic { get; }

    public bool IsFixedPitch { get; }

    /// <summary>Degrees counter-clockwise from vertical; negative for a font that leans right.</summary>
    public double ItalicAngle { get; }

    /// <summary><c>OS/2.sCapHeight</c>, from version 2, or null.</summary>
    public int? CapHeight { get; }

    public int UnderlinePosition { get; }

    public int UnderlineThickness { get; }

    /// <summary>The font's own copy of its file, for embedding a font that may not be subset whole (F-11).</summary>
    public ReadOnlyMemory<byte> FileData => _file;

    /// <exception cref="NotSupportedException">The font is one the library can't embed correctly.</exception>
    /// <exception cref="InvalidDataException">The font is damaged.</exception>
    public static TrueTypeFont Load(ReadOnlySpan<byte> file)
    {
        var bytes = file.ToArray();
        var directory = TableDirectory.Read(bytes);
        var names = directory.HasTable("name") ? NamingTable.Parse(directory.GetTable(bytes, "name")) : NamingTable.Empty;
        if (names.PostScriptName is not { } name)
        {
            return new TrueTypeFont(bytes, directory, names);
        }

        try
        {
            return new TrueTypeFont(bytes, directory, names);
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"{name}: {error.Message}", error);
        }
        catch (NotSupportedException error)
        {
            throw new NotSupportedException($"{name}: {error.Message}", error);
        }
    }

    /// <summary>The glyph for a code point, or 0 (<c>.notdef</c>) when the font doesn't map it (spec F-04).</summary>
    public ushort GetGlyphId(int codePoint) => _characterMap.GetGlyphId(codePoint);

    /// <summary>Every mapped code point and its glyph, in ascending code point order, without glyph 0.</summary>
    public IEnumerable<(int CodePoint, ushort GlyphId)> CharacterMappings() => _characterMap.Mappings();

    public int GetAdvanceWidth(ushort glyphId) => _metrics.AdvanceOf(CheckGlyph(glyphId));

    public int GetLeftSideBearing(ushort glyphId) => _metrics.LeftSideBearingOf(CheckGlyph(glyphId));

    /// <summary>The glyph's bytes in <c>glyf</c>; empty for a glyph with no outline, such as a space.</summary>
    public ReadOnlySpan<byte> GetGlyphData(ushort glyphId)
    {
        var (offset, length) = _locations.RangeOf(CheckGlyph(glyphId));
        return Table("glyf").Slice(offset, length);
    }

    /// <summary>The glyph's header, or null for a glyph with no outline.</summary>
    /// <exception cref="InvalidDataException">The glyph's data is shorter than a header.</exception>
    public GlyphHeader? GetGlyphHeader(ushort glyphId)
    {
        var data = GetGlyphData(glyphId);
        if (data.IsEmpty)
        {
            return null;
        }

        var reader = new FontTableReader(data, "glyf");
        if (reader.Length < 10)
        {
            throw reader.Error($"holds glyph {glyphId} in {reader.Length} bytes, less than a glyph header's 10");
        }

        return new GlyphHeader(reader.Int16(0), new GlyphBounds(reader.Int16(2), reader.Int16(4), reader.Int16(6), reader.Int16(8)));
    }

    public bool TryGetTable(string tag, out ReadOnlySpan<byte> table)
    {
        table = _directory.GetTable(_file, tag);
        return _directory.HasTable(tag);
    }

    private ReadOnlySpan<byte> Table(string tag) => _directory.GetTable(_file, tag);

    private int CheckGlyph(ushort glyphId)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((int)glyphId, GlyphCount, nameof(glyphId));
        return glyphId;
    }
}
