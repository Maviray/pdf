using System.Globalization;
using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Text.Unicode;

/// <summary>
/// The normalization data of the UCD, read from <c>UnicodeData.txt</c> and <c>CompositionExclusions.txt</c> and
/// derived as the Unicode Standard 3.11 and UAX #15 define it. The generator writes the tables from this model, and
/// the tests check the tables against it. Hangul syllables have no decomposition here: they are computed
/// arithmetically (the Unicode Standard 3.12).
/// </summary>
internal sealed class NormalizationModel
{
    public const int CodePointCount = 0x110000;

    private static readonly Lazy<NormalizationModel> Shared = new(() => new NormalizationModel());

    private readonly byte[] combiningClasses = new byte[CodePointCount];
    private readonly Dictionary<int, int[]> fullDecompositions = [];
    private readonly NfcQuickCheck[] quickChecks = new NfcQuickCheck[CodePointCount];

    private NormalizationModel()
    {
        var mappings = ReadUnicodeData();
        foreach (var codePoint in mappings.Keys)
        {
            fullDecompositions[codePoint] = [.. Expand(codePoint, mappings)];
        }

        var exclusions = ReadListedExclusions();
        foreach (var (codePoint, mapping) in mappings)
        {
            if (mapping.Length == 1 || combiningClasses[codePoint] != 0 || combiningClasses[mapping[0]] != 0)
            {
                exclusions.Add(codePoint);
            }
        }

        FullCompositionExclusions = exclusions;

        var composites = new Dictionary<(int First, int Second), int>();
        foreach (var (codePoint, mapping) in mappings)
        {
            if (mapping.Length == 2 && !exclusions.Contains(codePoint))
            {
                composites.Add((mapping[0], mapping[1]), codePoint);
            }
        }

        PrimaryComposites = composites;

        foreach (var (_, second) in composites.Keys)
        {
            quickChecks[second] = NfcQuickCheck.Maybe;
        }

        // The Hangul V and T jamo compose with the syllable or jamo before them (the Unicode Standard 3.12).
        for (var codePoint = 0x1161; codePoint <= 0x1175; codePoint++)
        {
            quickChecks[codePoint] = NfcQuickCheck.Maybe;
        }

        for (var codePoint = 0x11A8; codePoint <= 0x11C2; codePoint++)
        {
            quickChecks[codePoint] = NfcQuickCheck.Maybe;
        }

        // A chained composite (Unicode 16.0, NormalizationTest.txt part 5) decomposes to a sequence that starts with
        // a code point that can join the character before it. Then the composite can join that character too: U+1138B
        // U+113C7 decomposes to U+1138B U+113C2 U+113B8, and recomposes to U+1138E U+113B8. A full decomposition
        // starts with a code point that has no decomposition, so one pass finds them all.
        foreach (var (codePoint, decomposition) in fullDecompositions)
        {
            if (!exclusions.Contains(codePoint) && quickChecks[decomposition[0]] == NfcQuickCheck.Maybe)
            {
                quickChecks[codePoint] = NfcQuickCheck.Maybe;
            }
        }

        foreach (var codePoint in exclusions)
        {
            quickChecks[codePoint] = NfcQuickCheck.No;
        }

        DecomposableCodePoints = [.. fullDecompositions.Keys.Order()];
    }

    /// <summary>The source files, as the generated headers name them.</summary>
    public static IReadOnlyList<string> SourceFiles { get; } = ["UnicodeData.txt", "CompositionExclusions.txt"];

    /// <summary>The characters that never compose: the listed exclusions, singletons and non-starter decompositions.</summary>
    public IReadOnlySet<int> FullCompositionExclusions { get; }

    /// <summary>Every two-code-point canonical mapping whose character isn't excluded, keyed by its pair.</summary>
    public IReadOnlyDictionary<(int First, int Second), int> PrimaryComposites { get; }

    /// <summary>The code points that have a canonical decomposition, in ascending order.</summary>
    public IReadOnlyList<int> DecomposableCodePoints { get; }

    /// <summary>The model, built once and shared: it is never changed after construction.</summary>
    public static NormalizationModel Load() => Shared.Value;

    public byte CombiningClass(int codePoint) => IsCodePoint(codePoint) ? combiningClasses[codePoint] : (byte)0;

    /// <summary>The canonical mapping expanded until stable, or empty when the code point has none.</summary>
    public IReadOnlyList<int> FullDecomposition(int codePoint) =>
        fullDecompositions.TryGetValue(codePoint, out var decomposition) ? decomposition : [];

    public NfcQuickCheck QuickCheck(int codePoint) => IsCodePoint(codePoint) ? quickChecks[codePoint] : NfcQuickCheck.Yes;

    private static bool IsCodePoint(int codePoint) => (uint)codePoint < CodePointCount;

    private Dictionary<int, int[]> ReadUnicodeData()
    {
        var mappings = new Dictionary<int, int[]>();
        var rangeStart = -1;
        foreach (var (lineNumber, fields) in UcdFiles.ReadRecords("UnicodeData.txt"))
        {
            var codePoint = Hex(fields[0]);
            var combiningClass = byte.Parse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture);

            // A range is given by two lines, "<..., First>" and "<..., Last>", which share their properties.
            if (fields[1].EndsWith(", First>", StringComparison.Ordinal))
            {
                rangeStart = codePoint;
                continue;
            }

            var first = fields[1].EndsWith(", Last>", StringComparison.Ordinal) ? rangeStart : codePoint;
            for (var c = first; c <= codePoint; c++)
            {
                combiningClasses[c] = combiningClass;
            }

            // Field 5 is the decomposition mapping; one that starts with a <tag> is a compatibility mapping.
            if (fields[5].Length > 0 && fields[5][0] != '<')
            {
                if (first != codePoint)
                {
                    throw new InvalidDataException($"UnicodeData.txt line {lineNumber}: a range has a mapping.");
                }

                mappings.Add(codePoint, [.. fields[5].Split(' ').Select(Hex)]);
            }
        }

        return mappings;
    }

    private static HashSet<int> ReadListedExclusions() =>
        [.. UcdFiles.ReadRecords("CompositionExclusions.txt").Select(r => Hex(r.Fields[0]))];

    private static IEnumerable<int> Expand(int codePoint, Dictionary<int, int[]> mappings)
    {
        if (!mappings.TryGetValue(codePoint, out var mapping))
        {
            return [codePoint];
        }

        return mapping.SelectMany(c => Expand(c, mappings));
    }

    private static int Hex(string text) => int.Parse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
}
