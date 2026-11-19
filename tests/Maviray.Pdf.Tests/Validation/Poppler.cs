using System.Globalization;

namespace Maviray.Pdf.Tests.Validation;

/// <summary>Renders and reads generated PDFs with poppler, a PDF reader we didn't write (spec D-11).</summary>
internal static class Poppler
{
    /// <summary>
    /// Renders every page at 72 dpi, so one point is one pixel, next to the PDF as <c>&lt;name&gt;-&lt;page&gt;.ppm</c>.
    /// Fails on a nonzero exit code or anything on stderr, because poppler reports syntax errors there but may still
    /// exit with 0.
    /// </summary>
    public static IReadOnlyList<PpmImage> Render(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var stem = Path.GetFileNameWithoutExtension(path);
        foreach (var old in Directory.GetFiles(directory, stem + "-*.ppm"))
        {
            File.Delete(old);
        }

        var result = ExternalTool.Run("pdftoppm", "-r", "72", path, Path.Combine(directory, stem));
        if (result.ExitCode != 0 || result.StandardError.Length > 0)
        {
            Assert.Fail($"pdftoppm exited with {result.ExitCode} for {path}:\n{result.StandardError}");
        }

        return Directory.GetFiles(directory, stem + "-*.ppm")
            .Select(file => (Page: PageNumber(file, stem), File: file))
            .OrderBy(page => page.Page)
            .Select(page => PpmImage.Parse(File.ReadAllBytes(page.File)))
            .ToList();
    }

    /// <summary>
    /// Extracts the text with <c>pdftotext</c>. Blind spot: trailing whitespace and form feeds (page ends) are removed
    /// before returning, so a test comparing the result can't see differences in either.
    /// </summary>
    public static string ExtractText(string path)
    {
        var result = ExternalTool.Run("pdftotext", "-enc", "UTF-8", path, "-");
        if (result.ExitCode != 0)
        {
            Assert.Fail($"pdftotext exited with {result.ExitCode} for {path}:\n{result.StandardError}");
        }

        return result.StandardOutput.TrimEnd(' ', '\t', '\r', '\n', '\f');
    }

    private static int PageNumber(string file, string stem) =>
        int.Parse(Path.GetFileNameWithoutExtension(file)[(stem.Length + 1)..], CultureInfo.InvariantCulture);
}
