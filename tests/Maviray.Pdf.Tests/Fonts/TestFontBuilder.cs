using System.Buffers.Binary;
using System.Text;

namespace Maviray.Pdf.Tests.Fonts;

/// <summary>
/// Builds small, valid TrueType files from a description, following the OpenType specification, so every parser rule
/// can be tested against bytes whose meaning is known (spec F-12). The default font has four glyphs: <c>.notdef</c>
/// (advance 500), an empty glyph (250, mapped from U+0020), and two boxes (600 and 650, mapped from <c>A</c> and
/// <c>B</c>). Every setter returns the builder; <see cref="Table"/> and <see cref="Without"/> damage it on purpose.
/// </summary>
internal sealed class TestFontBuilder
{
    private readonly List<(ushort Advance, byte[] Data)> _glyphs = [];
    private readonly List<(int CodePoint, ushort GlyphId)> _map = [];
    private readonly Dictionary<string, byte[]> _overrides = new(StringComparer.Ordinal);
    private readonly HashSet<string> _removed = new(StringComparer.Ordinal);

    private uint _sfntVersion = 0x00010000;
    private ushort _unitsPerEm = 1000;
    private short _indexToLocFormat = 1;
    private int _glyphAlignment = 4;
    private short _ascender = 800;
    private short _descender = -200;
    private short _lineGap;
    private ushort? _numberOfHMetrics;
    private bool _cmapFormat12;
    private byte[]? _cmap;
    private ushort _fsType;
    private ushort _os2Version = 4;
    private ushort _weightClass = 400;
    private ushort _fsSelection = 0x40;
    private ushort _macStyle;
    private int _italicAngle;
    private uint _isFixedPitch;
    private short _typoAscender = 800;
    private short _typoDescender = -200;
    private short _typoLineGap;
    private short _underlinePosition = -100;
    private short _underlineThickness = 50;
    private short _strikeoutSize;
    private short _strikeoutPosition;
    private string? _postScriptName = "TestFont-Regular";
    private string? _family = "Test Font";
    private string? _subfamily = "Regular";

    public TestFontBuilder()
    {
        SimpleGlyph(500, 50, 0, 450, 700);
        EmptyGlyph(250);
        SimpleGlyph(600, 0, 0, 600, 700);
        SimpleGlyph(650, 0, 0, 650, 700);
        Map(0x20, 1).Map('A', 2).Map('B', 3);
    }

    public TestFontBuilder SfntVersion(uint version) => Set(() => _sfntVersion = version);

    public TestFontBuilder UnitsPerEm(ushort value) => Set(() => _unitsPerEm = value);

    public TestFontBuilder IndexToLocFormat(short format) => Set(() => _indexToLocFormat = format);

    /// <summary>The boundary each glyph's data is padded to: 4 by default, and 1 for none, which only long <c>loca</c> can describe.</summary>
    public TestFontBuilder GlyphAlignment(int bytes) => Set(() => _glyphAlignment = bytes);

    public TestFontBuilder LineMetrics(short ascender, short descender, short lineGap) =>
        Set(() => (_ascender, _descender, _lineGap) = (ascender, descender, lineGap));

    public TestFontBuilder NumberOfHMetrics(ushort value) => Set(() => _numberOfHMetrics = value);

    /// <summary>Removes every glyph and every mapping, so a test can start from glyph 0.</summary>
    public TestFontBuilder ClearGlyphs() => Set(() =>
    {
        _glyphs.Clear();
        _map.Clear();
    });

    /// <summary>A one-contour box from (xMin, yMin) to (xMax, yMax).</summary>
    public TestFontBuilder SimpleGlyph(ushort advance, short xMin, short yMin, short xMax, short yMax)
    {
        var bytes = new List<byte>();
        Put16(bytes, 1);
        Put16(bytes, (ushort)xMin);
        Put16(bytes, (ushort)yMin);
        Put16(bytes, (ushort)xMax);
        Put16(bytes, (ushort)yMax);
        Put16(bytes, 3); // endPtsOfContours[0]
        Put16(bytes, 0); // instructionLength
        bytes.AddRange([0x01, 0x01, 0x01, 0x01]); // four on-curve points, x and y as 16-bit deltas
        foreach (var dx in new[] { xMin, xMax - xMin, 0, xMin - xMax })
        {
            Put16(bytes, (ushort)dx);
        }

        foreach (var dy in new[] { yMin, 0, yMax - yMin, 0 })
        {
            Put16(bytes, (ushort)dy);
        }

        _glyphs.Add((advance, [.. bytes]));
        return this;
    }

    public TestFontBuilder EmptyGlyph(ushort advance)
    {
        _glyphs.Add((advance, []));
        return this;
    }

    /// <summary>A glyph with exactly these bytes, for damaged or unusual data.</summary>
    public TestFontBuilder RawGlyph(ushort advance, byte[] data)
    {
        _glyphs.Add((advance, data));
        return this;
    }

    public TestFontBuilder CompositeGlyph(ushort advance, params TestComponent[] components)
    {
        var bytes = new List<byte>();
        Put16(bytes, 0xFFFF); // numberOfContours = -1
        bytes.AddRange(new byte[8]); // bounding box
        for (var i = 0; i < components.Length; i++)
        {
            var component = components[i];
            var last = i == components.Length - 1;
            var flags = (ushort)((component.Flags & ~0x0020) | (last ? 0 : 0x0020));
            Put16(bytes, flags);
            Put16(bytes, component.GlyphId);
            if ((flags & 0x0001) != 0)
            {
                Put16(bytes, (ushort)component.Arg1);
                Put16(bytes, (ushort)component.Arg2);
            }
            else
            {
                bytes.Add((byte)component.Arg1);
                bytes.Add((byte)component.Arg2);
            }

            // The OpenType pseudo-code tests the transform flags in this order, so the first one set wins.
            var transformValues = (flags & 0x0008) != 0 ? 1 : (flags & 0x0040) != 0 ? 2 : (flags & 0x0080) != 0 ? 4 : 0;
            for (var v = 0; v < transformValues; v++)
            {
                Put16(bytes, 0x4000); // 1.0 in F2Dot14
            }

            if (last && (flags & 0x0100) != 0)
            {
                Put16(bytes, 2);
                bytes.AddRange([0xB0, 0x00]);
            }
        }

        _glyphs.Add((advance, [.. bytes]));
        return this;
    }

    public TestFontBuilder Map(int codePoint, ushort glyphId)
    {
        _map.Add((codePoint, glyphId));
        return this;
    }

    /// <summary>Writes the map as a (3, 10) format 12 subtable instead of (3, 1) format 4.</summary>
    public TestFontBuilder CmapFormat12() => Set(() => _cmapFormat12 = true);

    /// <summary>A whole <c>cmap</c> table, usually from <see cref="TestCmap.Table"/>, in place of the map.</summary>
    public TestFontBuilder Cmap(byte[] table) => Set(() => _cmap = table);

    public TestFontBuilder FsType(ushort value) => Set(() => _fsType = value);

    public TestFontBuilder Os2(ushort version, ushort weightClass, ushort fsSelection) =>
        Set(() => (_os2Version, _weightClass, _fsSelection) = (version, weightClass, fsSelection));

    public TestFontBuilder MacStyle(ushort value) => Set(() => _macStyle = value);

    /// <summary>OS/2 sTypoAscender, sTypoDescender and sTypoLineGap; 800, -200 and 0 by default.</summary>
    public TestFontBuilder TypographicMetrics(short ascender, short descender, short lineGap) =>
        Set(() => (_typoAscender, _typoDescender, _typoLineGap) = (ascender, descender, lineGap));

    /// <summary>post underlinePosition and underlineThickness; -100 and 50 by default.</summary>
    public TestFontBuilder Underline(short position, short thickness) =>
        Set(() => (_underlinePosition, _underlineThickness) = (position, thickness));

    /// <summary>OS/2 yStrikeoutSize and yStrikeoutPosition; 0 and 0 by default.</summary>
    public TestFontBuilder Strikeout(short size, short position) =>
        Set(() => (_strikeoutSize, _strikeoutPosition) = (size, position));

    /// <summary>The <c>post</c> header values: the angle is 16.16 fixed point.</summary>
    public TestFontBuilder Post(int italicAngleFixed, uint isFixedPitch) =>
        Set(() => (_italicAngle, _isFixedPitch) = (italicAngleFixed, isFixedPitch));

    public TestFontBuilder Names(string? postScript, string? family, string? subfamily) =>
        Set(() => (_postScriptName, _family, _subfamily) = (postScript, family, subfamily));

    /// <summary>Adds a table, or replaces a generated one, with raw bytes.</summary>
    public TestFontBuilder Table(string tag, byte[] data) => Set(() => _overrides[tag] = data);

    public TestFontBuilder Without(string tag) => Set(() => _removed.Add(tag));

    public byte[] Build()
    {
        var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["head"] = Head(),
            ["hhea"] = Hhea(),
            ["maxp"] = Maxp(),
            ["hmtx"] = Hmtx(),
            ["cmap"] = _cmap ?? CmapFromMap(),
            ["OS/2"] = Os2Table(),
            ["post"] = PostTable(),
            ["name"] = NameTable(),
        };
        var (loca, glyf) = LocaAndGlyf();
        tables["loca"] = loca;
        tables["glyf"] = glyf;
        foreach (var (tag, data) in _overrides)
        {
            tables[tag] = data;
        }

        foreach (var tag in _removed)
        {
            tables.Remove(tag);
        }

        return Assemble(tables);
    }

    private byte[] Assemble(SortedDictionary<string, byte[]> tables)
    {
        var count = tables.Count;
        var power = 1;
        var log = 0;
        while (power * 2 <= count)
        {
            power *= 2;
            log++;
        }

        var file = new List<byte>();
        Put32(file, _sfntVersion);
        Put16(file, (ushort)count);
        Put16(file, (ushort)(power * 16));
        Put16(file, (ushort)log);
        Put16(file, (ushort)((count * 16) - (power * 16)));
        var offset = 12 + (16 * count);
        var headOffset = -1;
        foreach (var (tag, data) in tables)
        {
            file.AddRange(Encoding.Latin1.GetBytes(tag));
            Put32(file, Checksum(data));
            Put32(file, (uint)offset);
            Put32(file, (uint)data.Length);
            if (tag == "head")
            {
                headOffset = offset;
            }

            offset += (data.Length + 3) & ~3;
        }

        foreach (var data in tables.Values)
        {
            file.AddRange(data);
            file.AddRange(new byte[((data.Length + 3) & ~3) - data.Length]);
        }

        var bytes = file.ToArray();
        if (headOffset >= 0 && tables["head"].Length >= 12)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(headOffset + 8), unchecked(0xB1B0AFBA - Checksum(bytes)));
        }

        return bytes;
    }

    private byte[] Head()
    {
        var bytes = new byte[54];
        var span = bytes.AsSpan();
        BinaryPrimitives.WriteUInt32BigEndian(span, 0x00010000);
        BinaryPrimitives.WriteUInt32BigEndian(span[4..], 0x00010000); // fontRevision
        BinaryPrimitives.WriteUInt32BigEndian(span[12..], 0x5F0F3CF5); // magicNumber
        BinaryPrimitives.WriteUInt16BigEndian(span[18..], _unitsPerEm);
        BinaryPrimitives.WriteInt16BigEndian(span[36..], 0);
        BinaryPrimitives.WriteInt16BigEndian(span[38..], -200);
        BinaryPrimitives.WriteInt16BigEndian(span[40..], 650);
        BinaryPrimitives.WriteInt16BigEndian(span[42..], 800);
        BinaryPrimitives.WriteUInt16BigEndian(span[44..], _macStyle);
        BinaryPrimitives.WriteInt16BigEndian(span[48..], 2); // fontDirectionHint
        BinaryPrimitives.WriteInt16BigEndian(span[50..], _indexToLocFormat);
        return bytes;
    }

    private byte[] Hhea()
    {
        var bytes = new byte[36];
        var span = bytes.AsSpan();
        BinaryPrimitives.WriteUInt32BigEndian(span, 0x00010000);
        BinaryPrimitives.WriteInt16BigEndian(span[4..], _ascender);
        BinaryPrimitives.WriteInt16BigEndian(span[6..], _descender);
        BinaryPrimitives.WriteInt16BigEndian(span[8..], _lineGap);
        BinaryPrimitives.WriteUInt16BigEndian(span[10..], _glyphs.Count == 0 ? (ushort)0 : _glyphs.Max(g => g.Advance));
        BinaryPrimitives.WriteInt16BigEndian(span[18..], 1); // caretSlopeRise
        BinaryPrimitives.WriteUInt16BigEndian(span[34..], _numberOfHMetrics ?? (ushort)_glyphs.Count);
        return bytes;
    }

    private byte[] Maxp()
    {
        var bytes = new byte[32];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(4), (ushort)_glyphs.Count);
        return bytes;
    }

    private byte[] Hmtx()
    {
        var bytes = new List<byte>();
        var metrics = Math.Min(_numberOfHMetrics ?? _glyphs.Count, _glyphs.Count);
        for (var i = 0; i < _glyphs.Count; i++)
        {
            if (i < metrics)
            {
                Put16(bytes, _glyphs[i].Advance);
            }

            // The left side bearing is the glyph's xMin, as in real fonts; an empty glyph has 0.
            var data = _glyphs[i].Data;
            Put16(bytes, data.Length >= 4 ? BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(2)) : (ushort)0);
        }

        return [.. bytes];
    }

    private (byte[] Loca, byte[] Glyf) LocaAndGlyf()
    {
        var glyf = new List<byte>();
        var loca = new List<byte>();
        foreach (var (_, data) in _glyphs)
        {
            PutLoca(loca, glyf.Count);
            glyf.AddRange(data);
            glyf.AddRange(new byte[(_glyphAlignment - (data.Length % _glyphAlignment)) % _glyphAlignment]);
        }

        PutLoca(loca, glyf.Count);
        return ([.. loca], [.. glyf]);
    }

    private void PutLoca(List<byte> loca, int offset)
    {
        if (_indexToLocFormat == 0)
        {
            Put16(loca, (ushort)(offset / 2));
        }
        else
        {
            Put32(loca, (uint)offset);
        }
    }

    private byte[] CmapFromMap()
    {
        if (_cmapFormat12)
        {
            var groups = _map.OrderBy(m => m.CodePoint).Select(m => ((uint)m.CodePoint, (uint)m.CodePoint, (uint)m.GlyphId)).ToArray();
            return TestCmap.Table((3, 10, TestCmap.Format12(groups)));
        }

        var segments = _map.OrderBy(m => m.CodePoint)
            .Select(m => new TestSegment((ushort)m.CodePoint, (ushort)m.CodePoint, (short)(m.GlyphId - m.CodePoint)))
            .ToArray();
        return TestCmap.Table((3, 1, TestCmap.Format4(segments)));
    }

    private byte[] Os2Table()
    {
        var length = _os2Version switch
        {
            0 => 78,
            1 => 86,
            < 5 => 96,
            _ => 100,
        };
        var bytes = new byte[length];
        var span = bytes.AsSpan();
        BinaryPrimitives.WriteUInt16BigEndian(span, _os2Version);
        BinaryPrimitives.WriteInt16BigEndian(span[2..], 500); // xAvgCharWidth
        BinaryPrimitives.WriteUInt16BigEndian(span[4..], _weightClass);
        BinaryPrimitives.WriteUInt16BigEndian(span[6..], 5); // usWidthClass: medium
        BinaryPrimitives.WriteUInt16BigEndian(span[8..], _fsType);
        BinaryPrimitives.WriteInt16BigEndian(span[26..], _strikeoutSize); // yStrikeoutSize
        BinaryPrimitives.WriteInt16BigEndian(span[28..], _strikeoutPosition); // yStrikeoutPosition
        BinaryPrimitives.WriteUInt16BigEndian(span[62..], _fsSelection);
        BinaryPrimitives.WriteUInt16BigEndian(span[64..], 0x20);
        BinaryPrimitives.WriteUInt16BigEndian(span[66..], 0x42);
        BinaryPrimitives.WriteInt16BigEndian(span[68..], _typoAscender); // sTypoAscender
        BinaryPrimitives.WriteInt16BigEndian(span[70..], _typoDescender); // sTypoDescender
        BinaryPrimitives.WriteInt16BigEndian(span[72..], _typoLineGap); // sTypoLineGap
        BinaryPrimitives.WriteUInt16BigEndian(span[74..], 900); // usWinAscent
        BinaryPrimitives.WriteUInt16BigEndian(span[76..], 250); // usWinDescent
        if (_os2Version >= 2)
        {
            BinaryPrimitives.WriteInt16BigEndian(span[86..], 500); // sxHeight
            BinaryPrimitives.WriteInt16BigEndian(span[88..], 700); // sCapHeight
        }

        return bytes;
    }

    private byte[] PostTable()
    {
        var bytes = new byte[32];
        var span = bytes.AsSpan();
        BinaryPrimitives.WriteUInt32BigEndian(span, 0x00030000);
        BinaryPrimitives.WriteInt32BigEndian(span[4..], _italicAngle);
        BinaryPrimitives.WriteInt16BigEndian(span[8..], _underlinePosition); // underlinePosition
        BinaryPrimitives.WriteInt16BigEndian(span[10..], _underlineThickness); // underlineThickness
        BinaryPrimitives.WriteUInt32BigEndian(span[12..], _isFixedPitch);
        return bytes;
    }

    private byte[] NameTable()
    {
        var records = new List<(ushort NameId, byte[] Text)>();
        if (_family is not null)
        {
            records.Add((1, Encoding.BigEndianUnicode.GetBytes(_family)));
        }

        if (_subfamily is not null)
        {
            records.Add((2, Encoding.BigEndianUnicode.GetBytes(_subfamily)));
        }

        if (_postScriptName is not null)
        {
            records.Add((6, Encoding.BigEndianUnicode.GetBytes(_postScriptName)));
        }

        var bytes = new List<byte>();
        Put16(bytes, 0);
        Put16(bytes, (ushort)records.Count);
        Put16(bytes, (ushort)(6 + (12 * records.Count)));
        var offset = 0;
        foreach (var (nameId, text) in records)
        {
            Put16(bytes, 3);
            Put16(bytes, 1);
            Put16(bytes, 0x409);
            Put16(bytes, nameId);
            Put16(bytes, (ushort)text.Length);
            Put16(bytes, (ushort)offset);
            offset += text.Length;
        }

        foreach (var (_, text) in records)
        {
            bytes.AddRange(text);
        }

        return [.. bytes];
    }

    private TestFontBuilder Set(Action set)
    {
        set();
        return this;
    }

    private static uint Checksum(byte[] data)
    {
        uint sum = 0;
        for (var i = 0; i < data.Length; i += 4)
        {
            uint word = 0;
            for (var j = 0; j < 4; j++)
            {
                word = (word << 8) | (i + j < data.Length ? data[i + j] : 0u);
            }

            sum = unchecked(sum + word);
        }

        return sum;
    }

    private static void Put16(List<byte> bytes, ushort value) => TestCmap.Put16(bytes, value);

    private static void Put32(List<byte> bytes, uint value) => TestCmap.Put32(bytes, value);
}
