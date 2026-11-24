namespace Maviray.Pdf.Tests.Validation;

// Poppler reports a file's problems on stderr but may still exit with 0, so the helper fails on what it writes there.
// The Windows build also writes that it lacks a substitute font for Symbol or Arial Unicode, which is about the
// machine, not the file.
public sealed class PopplerDiagnosticsTests
{
    [Fact]
    public void TheWindowsBuildsMissingDisplayFontsAreNoProblem()
    {
        const string Missing = "Syntax Error: No display font for 'Symbol'\nSyntax Error: No display font for 'ArialUnicode'\n";

        Assert.Empty(Poppler.Diagnostics(Missing));
        Assert.Empty(Poppler.Diagnostics(Missing.Replace("\n", "\r\n", StringComparison.Ordinal)));
    }

    [Fact]
    public void EveryOtherMessageIsAProblem()
    {
        var diagnostics = Poppler.Diagnostics("Syntax Error: No display font for 'Symbol'\nSyntax Error (1234): Illegal character '>'\n");

        Assert.Equal(["Syntax Error (1234): Illegal character '>'"], diagnostics);
    }

    [Fact]
    public void AMessageThatOnlyStartsLikeTheFontOneIsAProblem()
    {
        var diagnostics = Poppler.Diagnostics("Syntax Error: No display font for 'Symbol' in object 7\n");

        Assert.Equal(["Syntax Error: No display font for 'Symbol' in object 7"], diagnostics);
    }

    [Fact]
    public void NoOutputIsNoProblem() => Assert.Empty(Poppler.Diagnostics(""));
}
