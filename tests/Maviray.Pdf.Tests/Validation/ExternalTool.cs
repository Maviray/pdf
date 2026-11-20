using System.Diagnostics;
using System.Text;

namespace Maviray.Pdf.Tests.Validation;

/// <summary>
/// Finds and runs the external tools that check generated PDFs (spec D-11). A missing tool skips the check, unless
/// <c>MAVIRAY_PDF_REQUIRE_VALIDATORS</c> is <c>true</c>, which CI sets: then a missing tool fails the test.
/// </summary>
internal static class ExternalTool
{
    private const string RequireVariable = "MAVIRAY_PDF_REQUIRE_VALIDATORS";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>Returns the full path of <paramref name="tool"/>, or skips or fails the test when it isn't installed.</summary>
    public static string Locate(string tool) => Locate(tool, "Install qpdf and poppler-utils to run this check.");

    /// <summary>As <see cref="Locate(string)"/>, with <paramref name="installHint"/> in the message.</summary>
    public static string Locate(string tool, string installHint)
    {
        var path = Find(tool);
        if (path is not null)
        {
            return path;
        }

        Unavailable($"{tool} is not installed. {installHint}");
        throw new UnreachableException();
    }

    /// <summary>Skips the test with <paramref name="message"/>, or fails it when CI requires every validator.</summary>
    public static void Unavailable(string message)
    {
        if (string.Equals(Environment.GetEnvironmentVariable(RequireVariable), "true", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Fail($"{message} {RequireVariable} is true, so a missing tool fails the test.");
        }

        Assert.Skip(message);
    }

    /// <summary>Runs <paramref name="tool"/> and waits for it, for at most 60 seconds.</summary>
    public static ToolResult Run(string tool, params string[] arguments) => RunFile(Locate(tool), arguments);

    /// <summary>Runs the executable at <paramref name="path"/> and waits for it, for at most 60 seconds.</summary>
    public static ToolResult RunFile(string path, params string[] arguments)
    {
        var tool = Path.GetFileName(path);
        var start = new ProcessStartInfo(path)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"{tool} didn't start.");

        // Both streams are read while the process runs, so a full pipe can't block it.
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(Timeout))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail($"{tool} didn't finish within {Timeout.TotalSeconds} seconds.");
        }

        return new ToolResult(process.ExitCode, standardOutput.GetAwaiter().GetResult(), standardError.GetAwaiter().GetResult());
    }

    /// <summary>The full path of <paramref name="tool"/> on <c>PATH</c>, or null.</summary>
    public static string? Find(string tool)
    {
        string[] names = OperatingSystem.IsWindows() ? [tool + ".exe", tool] : [tool];
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var directory in directories)
        {
            foreach (var name in names)
            {
                var candidate = Path.Combine(directory.Trim('"'), name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
