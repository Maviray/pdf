using System.Collections.Concurrent;
using Maviray.Pdf.Fonts;

namespace Maviray.Pdf.Tests.Fonts;

/// <summary>
/// The committed Noto Sans files (spec F-12). They live where the <c>Maviray.Pdf.Fonts.Default</c> package will be
/// built from, and are found at run time by searching upward from the test output, so no build step copies them.
/// </summary>
internal static class TestFonts
{
    private const string Folder = "src/Maviray.Pdf.Fonts.Default/Fonts";

    public static TheoryData<string> Styles => new() { "Regular", "Bold", "Italic", "BoldItalic" };

    private static readonly ConcurrentDictionary<string, TrueTypeFont> Loaded = new(StringComparer.Ordinal);

    /// <summary>Noto Sans in one style, loaded once and shared: a loaded font is immutable.</summary>
    public static TrueTypeFont NotoSans(string style) =>
        Loaded.GetOrAdd(style, s => TrueTypeFont.Load(File.ReadAllBytes(NotoSansPath(s))));

    /// <summary>The path of <c>NotoSans-&lt;style&gt;.ttf</c>.</summary>
    public static string NotoSansPath(string style) => FilePath($"NotoSans-{style}.ttf");

    /// <summary>The path of a file in the fonts folder, such as <c>OFL.txt</c>.</summary>
    public static string FilePath(string fileName)
    {
        var relative = Path.Combine(Folder, fileName);
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            $"'{relative}' isn't in any parent of {AppContext.BaseDirectory}.", relative);
    }
}
