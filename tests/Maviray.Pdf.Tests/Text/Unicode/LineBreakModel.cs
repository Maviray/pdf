using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Text.Unicode;

/// <summary>
/// The line-break data of the UCD: each code point's class from <c>LineBreak.txt</c>, its East Asian width from
/// <c>EastAsianWidth.txt</c>, <c>Extended_Pictographic</c> from <c>emoji-data.txt</c>, and its general category from
/// <c>UnicodeData.txt</c>, with LB1 of UAX #14 applied as the P1 Text spec fixes it (T-06). The generator writes the
/// line-break table from this model, and the tests check the table against it.
/// </summary>
internal sealed class LineBreakModel
{
    private const int CodePointCount = NormalizationModel.CodePointCount;

    private static readonly Lazy<LineBreakModel> Shared = new(() => new LineBreakModel());

    private readonly LineBreakClass[] rawClasses = new LineBreakClass[CodePointCount];
    private readonly string[] generalCategories = new string[CodePointCount];
    private readonly bool[] eastAsian = new bool[CodePointCount];
    private readonly bool[] pictographic = new bool[CodePointCount];

    private LineBreakModel()
    {
        Fill("LineBreak.txt", rawClasses, Enum.Parse<LineBreakClass>);
        Fill("EastAsianWidth.txt", eastAsian, value => value is "F" or "W" or "H");
        Array.Fill(generalCategories, "Cn");
        ReadGeneralCategories();
        foreach (var (_, fields) in UcdFiles.ReadRecords("emoji-data.txt"))
        {
            if (fields[1] == "Extended_Pictographic")
            {
                var (first, last) = UcdFiles.Range(fields[0]);
                pictographic.AsSpan(first, last - first + 1).Fill(true);
            }
        }
    }

    public static IReadOnlyList<string> SourceFiles { get; } =
        ["LineBreak.txt", "EastAsianWidth.txt", "emoji-data.txt", "UnicodeData.txt"];

    /// <summary>The model, built once and shared: it is never changed after construction.</summary>
    public static LineBreakModel Load() => Shared.Value;

    public LineBreakClass RawClass(int codePoint) => rawClasses[codePoint];

    /// <summary>The two-letter general category, <c>Cn</c> for an unassigned code point.</summary>
    public string GeneralCategory(int codePoint) => generalCategories[codePoint];

    /// <summary>Whether the East Asian width is F, W or H, as UAX #14 tests it.</summary>
    public bool IsEastAsian(int codePoint) => eastAsian[codePoint];

    public bool IsExtendedPictographic(int codePoint) => pictographic[codePoint];

    /// <summary>The class after LB1: AI, SG and XX are AL; SA is CM for Mn and Mc and AL otherwise; CJ is NS.</summary>
    public LineBreakClass ResolvedClass(int codePoint) => rawClasses[codePoint] switch
    {
        LineBreakClass.AI or LineBreakClass.SG or LineBreakClass.XX => LineBreakClass.AL,
        LineBreakClass.SA => generalCategories[codePoint] is "Mn" or "Mc" ? LineBreakClass.CM : LineBreakClass.AL,
        LineBreakClass.CJ => LineBreakClass.NS,
        var other => other,
    };

    public bool IsInitialQuote(int codePoint) => rawClasses[codePoint] == LineBreakClass.QU && generalCategories[codePoint] == "Pi";

    public bool IsFinalQuote(int codePoint) => rawClasses[codePoint] == LineBreakClass.QU && generalCategories[codePoint] == "Pf";

    public bool IsUnassignedPictographic(int codePoint) => pictographic[codePoint] && generalCategories[codePoint] == "Cn";

    /// <summary>Applies a file's <c>@missing</c> defaults, then its data lines, to one property.</summary>
    private static void Fill<T>(string fileName, T[] property, Func<string, T> parse)
    {
        foreach (var (first, last, value) in UcdFiles.ReadMissing(fileName))
        {
            property.AsSpan(first, last - first + 1).Fill(parse(value));
        }

        foreach (var (_, fields) in UcdFiles.ReadRecords(fileName))
        {
            var (first, last) = UcdFiles.Range(fields[0]);
            property.AsSpan(first, last - first + 1).Fill(parse(fields[1]));
        }
    }

    private void ReadGeneralCategories()
    {
        var rangeStart = -1;
        foreach (var (_, fields) in UcdFiles.ReadRecords("UnicodeData.txt"))
        {
            var codePoint = UcdFiles.Range(fields[0]).First;
            if (fields[1].EndsWith(", First>", StringComparison.Ordinal))
            {
                rangeStart = codePoint;
                continue;
            }

            var first = fields[1].EndsWith(", Last>", StringComparison.Ordinal) ? rangeStart : codePoint;
            generalCategories.AsSpan(first, codePoint - first + 1).Fill(fields[2]);
        }
    }
}
