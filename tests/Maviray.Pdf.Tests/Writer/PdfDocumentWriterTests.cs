using System.IO.Compression;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

public sealed class PdfDocumentWriterTests
{
    private const double A4Width = 595.2756;
    private const double A4Height = 841.8898;

    [Fact]
    public void WritesABlankA4Page()
    {
        var file = PdfFileStructure.Parse(Write(document => document.EndPage(document.BeginPage(A4Width, A4Height))));

        Assert.Equal("1 0 obj\n<</Type /Pages /Kids [2 0 R] /Count 1>>\nendobj\n", file.ObjectText(1));
        Assert.Equal(
            "2 0 obj\n<</Type /Page /Parent 1 0 R /MediaBox [0 0 595.2756 841.8898] /Resources <<>>>>\nendobj\n",
            file.ObjectText(2));
        Assert.Equal("3 0 obj\n<</Type /Catalog /Pages 1 0 R>>\nendobj\n", file.ObjectText(3));
        Assert.Contains("/Root 3 0 R", file.Trailer, StringComparison.Ordinal);
    }

    [Fact]
    public void WritesTheContentStreamBeforeThePage()
    {
        var file = PdfFileStructure.Parse(Write(document =>
        {
            var page = document.BeginPage(A4Width, A4Height);
            page.Content.Rectangle(72, 600, 200, 100).Fill();
            document.EndPage(page);
        }));

        Assert.Equal("2 0 obj\n<</Length 20>>\nstream\n72 600 200 100 re\nf\n\nendstream\nendobj\n", file.ObjectText(2));
        Assert.EndsWith("/Resources <<>> /Contents 2 0 R>>\nendobj\n", file.ObjectText(3), StringComparison.Ordinal);
    }

    [Fact]
    public void TwoPagesShareOneFontObject()
    {
        var file = PdfFileStructure.Parse(Write(document =>
        {
            for (var i = 0; i < 2; i++)
            {
                var page = document.BeginPage(A4Width, A4Height);
                page.Content.BeginText().SetFont(StandardFont.Helvetica, 12).ShowText("Page").EndText();
                document.EndPage(page);
            }
        }));

        // Object 2 is the font, reserved when the first page set it; then each page's content stream and dictionary.
        Assert.Contains("/Resources <</Font <</F1 2 0 R>>>> /Contents 3 0 R>>", file.ObjectText(4), StringComparison.Ordinal);
        Assert.Contains("/Resources <</Font <</F1 2 0 R>>>> /Contents 5 0 R>>", file.ObjectText(6), StringComparison.Ordinal);
        Assert.Equal(
            "2 0 obj\n<</Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding>>\nendobj\n",
            file.ObjectText(2));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(file.Text, "/BaseFont"));
    }

    [Fact]
    public void APageWithoutTextHasNoFontResources()
    {
        var file = PdfFileStructure.Parse(Write(document =>
        {
            var page = document.BeginPage(A4Width, A4Height);
            page.Content.BeginText().SetFont(StandardFont.Helvetica, 12).ShowText("Text").EndText();
            document.EndPage(page);
            var blank = document.BeginPage(A4Width, A4Height);
            blank.Content.Rectangle(0, 0, 10, 10).Fill();
            document.EndPage(blank);
        }));

        Assert.Contains("/Resources <<>> /Contents 5 0 R>>", file.ObjectText(6), StringComparison.Ordinal);
    }

    [Fact]
    public void EndingAPageWithAnOpenPathThrows()
    {
        using var document = new PdfDocumentWriter(new MemoryStream());
        var page = document.BeginPage(A4Width, A4Height);
        page.Content.Rectangle(0, 0, 10, 10);

        Assert.Throws<InvalidOperationException>(() => document.EndPage(page));
    }

    [Fact]
    public void KeepsPagesInOrder()
    {
        var file = PdfFileStructure.Parse(Write(document =>
        {
            for (var i = 0; i < 3; i++)
            {
                document.EndPage(document.BeginPage(A4Width, A4Height));
            }
        }));

        Assert.Equal("1 0 obj\n<</Type /Pages /Kids [2 0 R 3 0 R 4 0 R] /Count 3>>\nendobj\n", file.ObjectText(1));
    }

    [Fact]
    public void ClosingWithNoPagesThrows()
    {
        using var document = new PdfDocumentWriter(new MemoryStream());

        Assert.Throws<InvalidOperationException>(document.Close);
    }

    [Fact]
    public void BeginningAPageWhileOneIsOpenThrows()
    {
        using var document = new PdfDocumentWriter(new MemoryStream());
        document.BeginPage(A4Width, A4Height);

        Assert.Throws<InvalidOperationException>(() => document.BeginPage(A4Width, A4Height));
    }

    [Fact]
    public void EndingAPageThatIsNotOpenThrows()
    {
        using var document = new PdfDocumentWriter(new MemoryStream());
        var page = document.BeginPage(A4Width, A4Height);
        document.EndPage(page);

        Assert.Throws<InvalidOperationException>(() => document.EndPage(page));
    }

    [Fact]
    public void EndingAnotherPageWhileOneIsOpenThrows()
    {
        using var document = new PdfDocumentWriter(new MemoryStream());
        using var other = new PdfDocumentWriter(new MemoryStream());
        var ended = document.BeginPage(A4Width, A4Height);
        document.EndPage(ended);
        document.BeginPage(A4Width, A4Height);
        var foreign = other.BeginPage(A4Width, A4Height);

        Assert.Throws<InvalidOperationException>(() => document.EndPage(ended));
        Assert.Throws<InvalidOperationException>(() => document.EndPage(foreign));
    }

    [Fact]
    public void ClosingWithAPageOpenThrows()
    {
        using var document = new PdfDocumentWriter(new MemoryStream());
        document.BeginPage(A4Width, A4Height);

        Assert.Throws<InvalidOperationException>(document.Close);
    }

    [Theory]
    [InlineData(0.0, 10.0)]
    [InlineData(10.0, -1.0)]
    [InlineData(double.NaN, 10.0)]
    public void RefusesPageSizesThatAreNotPositive(double width, double height)
    {
        using var document = new PdfDocumentWriter(new MemoryStream());

        Assert.Throws<ArgumentOutOfRangeException>(() => document.BeginPage(width, height));
    }

    /// <summary>Writes a document with a fixed ID and no compression, so every byte is predictable.</summary>
    internal static byte[] Write(Action<PdfDocumentWriter> body, CompressionLevel compression = CompressionLevel.NoCompression)
    {
        var destination = new MemoryStream();
        var options = new PdfWriterOptions { FileId = new byte[16], CompressionLevel = compression };
        using (var document = new PdfDocumentWriter(destination, options))
        {
            body(document);
            document.Close();
        }

        return destination.ToArray();
    }
}
