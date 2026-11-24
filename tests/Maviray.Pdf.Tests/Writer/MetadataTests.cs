using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Maviray.Pdf.Tests.Validation;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

// The Info dictionary, the catalog's language and title display (L-12), and deterministic mode's hashed ID (L-13).
public sealed class MetadataTests
{
    private static readonly PdfDocumentInfo Full = new(
        "Fatura – Счёт № 42",
        "Ayşe Yılmaz",
        "Rechnung",
        "Rechnung, November",
        "Maviray.Pdf.Tests",
        new DateTimeOffset(2026, 11, 23, 14, 5, 0, TimeSpan.FromHours(1)));

    [Fact]
    public void TheInfoDictionaryHoldsWhatIsSet()
    {
        var path = GeneratedFiles.Save(Write(Full, compression: CompressionLevel.Optimal));

        var info = Poppler.Info(path);

        Assert.Equal(Full.Title, info["Title"]);
        Assert.Equal(Full.Author, info["Author"]);
        Assert.Equal("Rechnung", info["Subject"]);
        Assert.Equal("Rechnung, November", info["Keywords"]);
        Assert.Equal("Maviray.Pdf.Tests", info["Creator"]);
        Qpdf.Check(path);
    }

    [Fact]
    public void ProducerIsAlwaysWritten()
    {
        var path = GeneratedFiles.Save(Write(null));

        Assert.Equal("Maviray.Pdf " + PackageVersion.Value, Poppler.Info(path)["Producer"]);
    }

    [Fact]
    public void TheCreationDateIsWrittenWithItsOffset() =>
        Assert.Contains("/CreationDate (D:20261123140500+01'00')", Text(Write(Full)), StringComparison.Ordinal);

    [Fact]
    public void UnsetValuesArentWritten()
    {
        var text = Text(Write(new PdfDocumentInfo("Only a title", null, null, null, null, null)));

        Assert.Contains("/Title (Only a title)", text, StringComparison.Ordinal);
        foreach (var key in new[] { "/Author", "/Subject", "/Keywords", "/Creator", "/CreationDate" })
        {
            Assert.DoesNotContain(key, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void LangAndDisplayDocTitleOnlyWhenSet()
    {
        var with = Text(Write(Full, language: "de-DE"));
        var without = Text(Write(null));

        Assert.Contains("/Lang (de-DE)", with, StringComparison.Ordinal);
        Assert.Contains("/ViewerPreferences <</DisplayDocTitle true>>", with, StringComparison.Ordinal);
        Assert.DoesNotContain("/Lang", without, StringComparison.Ordinal);
        Assert.DoesNotContain("/ViewerPreferences", without, StringComparison.Ordinal);
    }

    [Fact]
    public void DisplayDocTitleFollowsTheTitleAlone()
    {
        var titleOnly = Text(Write(Full));
        var languageOnly = Text(Write(Full with { Title = null }, language: "de-DE"));

        Assert.Contains("/ViewerPreferences <</DisplayDocTitle true>>", titleOnly, StringComparison.Ordinal);
        Assert.DoesNotContain("/Lang", titleOnly, StringComparison.Ordinal);
        Assert.Contains("/Lang (de-DE)", languageOnly, StringComparison.Ordinal);
        Assert.DoesNotContain("/ViewerPreferences", languageOnly, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, 0, "D:20261123140500+01'00'")]
    [InlineData(-5, -30, "D:20261123140500-05'30'")]
    [InlineData(0, 0, "D:20261123140500Z")]
    public void TheDateFormat(int hours, int minutes, string expected) =>
        Assert.Equal(expected, PdfDate.Format(new DateTimeOffset(2026, 11, 23, 14, 5, 0, new TimeSpan(hours, minutes, 0))));

    [Fact]
    public void AHashedIdIsTheSha256OfTheFileBeforeTheTrailer()
    {
        var file = Write(Full, hashed: true);
        var text = Text(file);
        var trailer = text.IndexOf("trailer\n", StringComparison.Ordinal);

        var expected = Convert.ToHexString(SHA256.HashData(file.AsSpan(0, trailer)).AsSpan(0, 16));

        Assert.Contains($"/ID [<{expected}> <{expected}>]", text, StringComparison.Ordinal);
        Assert.Equal(file, Write(Full, hashed: true));
    }

    [Fact]
    public void AHashedIdAndAGivenIdCantBeCombined() =>
        Assert.Throws<ArgumentException>(() => new PdfDocumentWriter(new MemoryStream(), new PdfWriterOptions { HashedFileId = true, FileId = new byte[16] }));

    private static byte[] Write(PdfDocumentInfo? info, string? language = null, bool hashed = false, CompressionLevel compression = CompressionLevel.NoCompression)
    {
        var options = new PdfWriterOptions { Info = info, Language = language, HashedFileId = hashed, CompressionLevel = compression, FileId = hashed ? null : new byte[16] };
        using var stream = new MemoryStream();
        using (var document = new PdfDocumentWriter(stream, options))
        {
            var page = document.BeginPage(595.2756, 841.8898);
            document.EndPage(page);
            document.Close();
        }

        return stream.ToArray();
    }

    private static string Text(byte[] file) => Encoding.Latin1.GetString(file);
}
