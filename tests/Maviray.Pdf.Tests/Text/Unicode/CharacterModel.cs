namespace Maviray.Pdf.Tests.Text.Unicode;

/// <summary>
/// The character properties that T-07's glyph rules test: general category Cc and Zs, from <c>UnicodeData.txt</c>,
/// and <c>Default_Ignorable_Code_Point</c>, from <c>DerivedCoreProperties.txt</c>. The generator writes the
/// character table from this model, and the tests check the table against it.
/// </summary>
internal sealed class CharacterModel
{
    private static readonly Lazy<CharacterModel> Shared = new(() => new CharacterModel());

    private readonly bool[] defaultIgnorable = new bool[NormalizationModel.CodePointCount];

    private CharacterModel()
    {
        foreach (var (_, fields) in UcdFiles.ReadRecords("DerivedCoreProperties.txt"))
        {
            if (fields[1] == "Default_Ignorable_Code_Point")
            {
                var (first, last) = UcdFiles.Range(fields[0]);
                defaultIgnorable.AsSpan(first, last - first + 1).Fill(true);
            }
        }
    }

    public static IReadOnlyList<string> SourceFiles { get; } = ["UnicodeData.txt", "DerivedCoreProperties.txt"];

    /// <summary>The model, built once and shared: it is never changed after construction.</summary>
    public static CharacterModel Load() => Shared.Value;

    /// <summary>General category Cc. The general categories come from the line-break model, which reads them.</summary>
    public bool IsControl(int codePoint) => LineBreakModel.Load().GeneralCategory(codePoint) == "Cc";

    /// <summary>General category Zs.</summary>
    public bool IsSpaceSeparator(int codePoint) => LineBreakModel.Load().GeneralCategory(codePoint) == "Zs";

    public bool IsDefaultIgnorable(int codePoint) => defaultIgnorable[codePoint];
}
