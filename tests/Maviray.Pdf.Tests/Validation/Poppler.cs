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

    /// <summary>
    /// Lists the fonts with <c>pdffonts</c>. Fails on a nonzero exit code or anything on stderr, because poppler
    /// reports problems in a font's dictionaries there, such as a bad ToUnicode CMap, but still exits with 0. The
    /// columns are found by their header names over the dashed line, so the parse works with poppler's
    /// <c>encoding</c> column and with xpdf's pdffonts, which has none. pdffonts pads a name but never cuts it, so a
    /// name longer than its column, up to the next space, pushes every later column right by its overflow. Blind spot:
    /// a name containing a space and longer than its column would be cut at that space; embedded names never contain
    /// one (F-09).
    /// </summary>
    public static IReadOnlyList<PdfFontReport> ListFonts(string path) => ReadFonts(ExternalTool.Run("pdffonts", path), path);

    /// <summary>Reads pdffonts' result for <paramref name="path"/>, as <see cref="ListFonts"/> does.</summary>
    public static IReadOnlyList<PdfFontReport> ReadFonts(ToolResult result, string path)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.ExitCode != 0 || result.StandardError.Length > 0)
        {
            Assert.Fail($"pdffonts exited with {result.ExitCode} for {path}:\n{result.StandardError}");
        }

        var lines = result.StandardOutput.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        var dashes = lines.FindIndex(line => line.StartsWith("---", StringComparison.Ordinal));
        Assert.True(dashes > 0, $"pdffonts printed no table for {path}:\n{result.StandardOutput}");

        var columns = new Dictionary<string, (int Start, int Length)>(StringComparer.Ordinal);
        for (var start = 0; start < lines[dashes].Length;)
        {
            var end = lines[dashes].IndexOf(' ', start);
            end = end < 0 ? lines[dashes].Length : end;
            columns[Cell(lines[dashes - 1], start, end - start)] = (start, end - start);
            start = end + 1;
        }

        return lines.Skip(dashes + 1)
            .Where(row => row.Trim().Length > 0)
            .Select(row => Report(row, columns))
            .ToList();
    }

    private static PdfFontReport Report(string row, Dictionary<string, (int Start, int Length)> columns)
    {
        // A name that fills its column runs on to the next space, and every later column moves right by as much.
        var (nameStart, nameLength) = columns["name"];
        var nameEnd = nameStart + nameLength;
        var overflow = 0;
        if (nameEnd < row.Length && row[nameEnd] != ' ')
        {
            var space = row.IndexOf(' ', nameEnd);
            overflow = (space < 0 ? row.Length : space) - nameEnd;
        }

        string Column(string header)
        {
            var (start, length) = columns[header];
            return header == "name" ? Cell(row, start, length + overflow) : Cell(row, start + overflow, length);
        }

        return new PdfFontReport(Column("name"), Column("type"), Column("emb") == "yes", Column("sub") == "yes", Column("uni") == "yes");
    }

    private static string Cell(string row, int start, int length) =>
        start >= row.Length ? string.Empty : row.Substring(start, Math.Min(length, row.Length - start)).Trim();

    private static int PageNumber(string file, string stem) =>
        int.Parse(Path.GetFileNameWithoutExtension(file)[(stem.Length + 1)..], CultureInfo.InvariantCulture);
}
