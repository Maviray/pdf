using System.Security.Cryptography;

namespace Maviray.Pdf.Tests.Fonts;

public sealed class TestFontsTests
{
    // The checksums of NotoSans-v2.015 (notofonts/latin-greek-cyrillic), recorded in the P1.1 plan. A replaced or
    // re-encoded file fails here before any font test can pass for the wrong reason.
    [Theory]
    [InlineData("NotoSans-Regular.ttf", "478c558ea716033cd60c03438f628dfa75694dcf6b5f6d505a2f05fd2b4f3823")]
    [InlineData("NotoSans-Bold.ttf", "1df075a380fc7cb898acf64c1f7b3b4dd780de3caa860178bf929de35817a913")]
    [InlineData("NotoSans-Italic.ttf", "467e3f89eeca4108bb8710a2b9e0cf2281ac56d5b0609211a83776d0505eecb5")]
    [InlineData("NotoSans-BoldItalic.ttf", "1b602a9d6353be42c91df097a4857b69fa2696f26703d7a33b54a15d87c2622c")]
    [InlineData("OFL.txt", "cee9892f9f0cc8fe882c9e9537ee6a89621d86ee7ceaf70b02e2b2b1c25c061a")]
    public void NotoSansFilesAreTheRecordedRelease(string fileName, string sha256)
    {
        var bytes = File.ReadAllBytes(TestFonts.FilePath(fileName));

        Assert.Equal(sha256, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }
}
