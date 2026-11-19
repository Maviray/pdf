namespace Maviray.Pdf.Tests.Validation;

/// <summary>Runs <c>qpdf --check</c>, the structure check every generated file has to pass (spec D-11).</summary>
internal static class Qpdf
{
    /// <summary>Fails the test unless qpdf finds no errors and no warnings: exit code 2 is errors, 3 is warnings.</summary>
    public static void Check(string path)
    {
        var result = ExternalTool.Run("qpdf", "--check", path);
        if (result.ExitCode != 0)
        {
            Assert.Fail($"qpdf --check exited with {result.ExitCode} for {path}:\n{result.StandardOutput}\n{result.StandardError}");
        }
    }
}
