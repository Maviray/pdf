namespace Maviray.Pdf.Tests.Text.Unicode;

/// <summary>
/// Generates the Unicode tables in <c>src/Maviray.Pdf/Text/Unicode/</c> from the UCD files (spec T-02). The
/// staleness test compares the committed files with this output, and writes them when asked to.
/// </summary>
internal static class UnicodeTableGenerator
{
    /// <summary>log2 of the number of code points per block in the two-stage tables.</summary>
    public const int BlockShift = 7;

    /// <summary>The folder of the generated files, <c>src/Maviray.Pdf/Text/Unicode/</c>.</summary>
    public static string TablesFolder { get; } = RepositoryFiles.PathOf("src/Maviray.Pdf/Text/Unicode");

    public static IReadOnlyList<GeneratedFile> Generate()
    {
        var model = NormalizationModel.Load();
        return [NfcProperties(model), Decompositions(model), Compositions(model), LineBreaks(LineBreakModel.Load())];
    }

    private static GeneratedFile NfcProperties(NormalizationModel model) =>
        TwoStageTable(
            "NfcPropertyData",
            "Each code point's canonical combining class and NFC_Quick_Check value, in a two-stage table.",
            NormalizationModel.SourceFiles,
            codePoint => model.CombiningClass(codePoint) | ((int)model.QuickCheck(codePoint) << 8),
            "An entry holds the combining class in its low byte and the NfcQuickCheck value in bits 8 and 9.");

    private static GeneratedFile LineBreaks(LineBreakModel model) =>
        TwoStageTable(
            "LineBreakData",
            "Each code point's line-break class after LB1 of UAX #14, and the flags its rules test, in a two-stage table.",
            LineBreakModel.SourceFiles,
            codePoint => (int)model.ResolvedClass(codePoint)
                | (model.IsEastAsian(codePoint) ? 1 << 6 : 0)
                | (model.IsInitialQuote(codePoint) ? 1 << 7 : 0)
                | (model.IsFinalQuote(codePoint) ? 1 << 8 : 0)
                | (model.IsUnassignedPictographic(codePoint) ? 1 << 9 : 0),
            "An entry holds the LineBreakClass in bits 0 to 5, East Asian width F, W or H in bit 6, a QU that is Pi in bit 7, " +
            "a QU that is Pf in bit 8, and Extended_Pictographic and unassigned in bit 9.");

    /// <summary>
    /// A table of one ushort entry per code point. The entries are cut into blocks of 2^BlockShift code points, and
    /// each distinct block is stored once; a byte index gives each block of code points its stored block.
    /// </summary>
    private static GeneratedFile TwoStageTable(
        string className, string summary, IReadOnlyList<string> sourceFiles, Func<int, int> entry, string entryDescription)
    {
        var blockSize = 1 << BlockShift;
        var blockIndex = new List<int>();
        var blocks = new List<int>();
        var known = new Dictionary<string, int>(StringComparer.Ordinal);
        var block = new int[blockSize];
        for (var start = 0; start < NormalizationModel.CodePointCount; start += blockSize)
        {
            for (var i = 0; i < blockSize; i++)
            {
                block[i] = entry(start + i);
                Check(block[i] is >= 0 and <= ushort.MaxValue, $"{className}'s entries don't fit ushort.");
            }

            var key = string.Join(",", block);
            if (!known.TryGetValue(key, out var number))
            {
                number = known.Count;
                known.Add(key, number);
                blocks.AddRange(block);
            }

            blockIndex.Add(number);
        }

        Check(known.Count <= byte.MaxValue + 1, $"{className}'s blocks don't fit byte block numbers.");

        var writer = new CSharpTableWriter(className, summary, sourceFiles);
        writer.Constant("BlockShift", BlockShift, "log2 of the number of code points per block.");
        writer.Array(
            "BlockIndex",
            "byte",
            blockIndex,
            "The number of each block of code points, in code point order: entry i covers code points i << BlockShift onward.");
        writer.Array("Blocks", "ushort", blocks, "The distinct blocks. " + entryDescription);
        return writer.ToFile();
    }

    private static GeneratedFile Decompositions(NormalizationModel model)
    {
        var codePoints = model.DecomposableCodePoints;
        var starts = new List<int>(codePoints.Count + 1);
        var mappings = new List<int>();
        foreach (var codePoint in codePoints)
        {
            starts.Add(mappings.Count);
            mappings.AddRange(model.FullDecomposition(codePoint));
        }

        starts.Add(mappings.Count);
        Check(mappings.Count <= ushort.MaxValue, "DecompositionData's mappings don't fit ushort offsets.");

        var writer = new CSharpTableWriter(
            "DecompositionData",
            "The full canonical decomposition of every code point that has one, Hangul syllables aside.",
            NormalizationModel.SourceFiles);
        writer.HexArray(
            "CodePoints",
            "int",
            codePoints.Select(c => (ulong)c),
            4,
            "The code points that have a canonical decomposition, in ascending order.");
        writer.Array(
            "Starts",
            "ushort",
            starts,
            "Where each code point's decomposition starts in Mappings; the last entry is Mappings' length.");
        writer.HexArray(
            "Mappings",
            "int",
            mappings.Select(c => (ulong)c),
            4,
            "The decompositions, one after the other: CodePoints[i] decomposes to Mappings[Starts[i]..Starts[i + 1]].");
        return writer.ToFile();
    }

    private static GeneratedFile Compositions(NormalizationModel model)
    {
        var composites = model.PrimaryComposites
            .Select(p => (Key: ((ulong)(uint)p.Key.First << 32) | (uint)p.Key.Second, Composite: p.Value))
            .OrderBy(p => p.Key)
            .ToList();

        var writer = new CSharpTableWriter(
            "CompositionData",
            "Every primary composite: a canonical pair that composes in NFC, Hangul syllables aside.",
            NormalizationModel.SourceFiles);
        writer.HexArray(
            "Pairs",
            "ulong",
            composites.Select(c => c.Key),
            16,
            "Each pair as (ulong)first << 32 | (uint)second, in ascending order.");
        writer.HexArray(
            "Composites",
            "int",
            composites.Select(c => (ulong)c.Composite),
            4,
            "The composite of each pair, in the same order.");
        return writer.ToFile();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
