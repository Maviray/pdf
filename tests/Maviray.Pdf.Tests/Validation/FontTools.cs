using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Maviray.Pdf.Tests.Validation;

/// <summary>
/// Runs fontTools, through <c>fonttools_check.py</c>, as an independent check of fonts (spec F-13). Python counts as
/// installed only when <c>import fontTools</c> succeeds, so Windows' Microsoft Store placeholder for Python doesn't.
/// A missing fontTools skips the check, or fails it when <c>MAVIRAY_PDF_REQUIRE_VALIDATORS</c> is <c>true</c>.
/// </summary>
internal static class FontTools
{
    private const string Missing =
        "fontTools is not installed. Install it with 'pip install fonttools' or the python3-fonttools package to run this check.";

    private static readonly Lazy<string?> Interpreter = new(FindPython);

    /// <summary>The Python that can import fontTools, or skips or fails the test.</summary>
    public static string Python()
    {
        if (Interpreter.Value is { } path)
        {
            return path;
        }

        ExternalTool.Unavailable(Missing);
        throw new UnreachableException();
    }

    /// <summary>What fontTools reads from <paramref name="fontPath"/>. The test fails if fontTools can't read it.</summary>
    public static FontDump Dump(string fontPath) => FontDump.Parse(Run("dump", fontPath).StandardOutput);

    /// <summary>
    /// Compares every glyph of the subset at <paramref name="subsetPath"/> with its original glyph in
    /// <paramref name="originalPath"/>: outline, components, instructions and metrics. The test fails on any difference,
    /// or if fontTools can't read the subset with checksum errors raised.
    /// </summary>
    /// <returns>The number of glyphs fontTools compared.</returns>
    public static int CheckSubset(string originalPath, string subsetPath, IReadOnlyList<ushort> originalGlyphIds)
    {
        var ids = subsetPath + ".glyphs.txt";
        File.WriteAllText(ids, string.Join(' ', originalGlyphIds.Select(g => g.ToString(CultureInfo.InvariantCulture))), Encoding.ASCII);
        using var result = JsonDocument.Parse(Run("subset", originalPath, subsetPath, ids).StandardOutput);
        return result.RootElement.GetProperty("glyphs").GetInt32();
    }

    private static ToolResult Run(params string[] arguments)
    {
        var script = Path.Combine(AppContext.BaseDirectory, "Validation", "fonttools_check.py");
        var result = ExternalTool.RunFile(Python(), ["-I", script, .. arguments]);
        if (result.ExitCode != 0)
        {
            Assert.Fail($"fontTools rejected {string.Join(' ', arguments)} (exit code {result.ExitCode}):\n{result.StandardError}{result.StandardOutput}");
        }

        return result;
    }

    private static string? FindPython()
    {
        foreach (var name in new[] { "python3", "python" })
        {
            if (ExternalTool.Find(name) is { } path && ExternalTool.RunFile(path, "-I", "-c", "import fontTools").ExitCode == 0)
            {
                return path;
            }
        }

        return null;
    }
}
