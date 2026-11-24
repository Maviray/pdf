using System.IO.Compression;
using System.Text;

namespace Maviray.Pdf.Tests.Text.Unicode;

/// <summary>
/// The committed Unicode Character Database files (spec T-02). They are gzip-compressed beside this file and found at
/// run time by searching upward from the test output, so no build step copies them.
/// </summary>
internal static class UcdFiles
{
    public const string Version = "16.0.0";

    private const string Folder = "tests/Maviray.Pdf.Tests/Text/Unicode/Data/" + Version;

    /// <summary>The absolute path of the data folder.</summary>
    public static string DataFolder { get; } = FindDataFolder();

    /// <summary>
    /// The text of a data file, decompressed from <c>&lt;fileName&gt;.gz</c>; <c>license.txt</c> is read as it is.
    /// </summary>
    public static string ReadText(string fileName)
    {
        var path = Path.Combine(DataFolder, fileName);
        if (File.Exists(path))
        {
            return File.ReadAllText(path, Encoding.UTF8);
        }

        using var file = File.OpenRead(path + ".gz");
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Each data line of a file with its <c>#</c> comment removed, split at <c>;</c> and trimmed. Blank lines are
    /// skipped. Line numbers start at 1.
    /// </summary>
    public static IEnumerable<(int LineNumber, string[] Fields)> ReadRecords(string fileName)
    {
        var lines = ReadText(fileName).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var hash = line.IndexOf('#', StringComparison.Ordinal);
            if (hash >= 0)
            {
                line = line[..hash];
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = line.Split(';');
            for (var f = 0; f < fields.Length; f++)
            {
                fields[f] = fields[f].Trim();
            }

            yield return (i + 1, fields);
        }
    }

    /// <summary>
    /// The default values a file states in its <c># @missing: first..last; value</c> comment lines, in file order.
    /// Data lines override them.
    /// </summary>
    public static IEnumerable<(int First, int Last, string Value)> ReadMissing(string fileName)
    {
        const string Prefix = "# @missing:";
        foreach (var line in ReadText(fileName).Split('\n'))
        {
            if (!line.StartsWith(Prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var fields = line[Prefix.Length..].Split('#')[0].Split(';');
            var (first, last) = Range(fields[0].Trim());
            yield return (first, last, fields[1].Trim());
        }
    }

    /// <summary>A code point or a <c>first..last</c> range, in hex, as the UCD files write them.</summary>
    public static (int First, int Last) Range(string field)
    {
        var dots = field.IndexOf("..", StringComparison.Ordinal);
        return dots < 0
            ? (Hex(field), Hex(field))
            : (Hex(field[..dots]), Hex(field[(dots + 2)..]));
    }

    private static int Hex(string text) =>
        int.Parse(text, System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture);

    private static string FindDataFolder()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, Folder);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException($"'{Folder}' isn't in any parent of {AppContext.BaseDirectory}.");
    }
}
