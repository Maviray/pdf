using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

public sealed class FontResourcesTests
{
    [Fact]
    public void NamesAFontOnceAndReservesItOnce()
    {
        using var file = new PdfFileWriter(new MemoryStream(), new PdfWriterOptions());
        var fonts = new FontResources(file);

        var name = fonts.NameOf(StandardFont.Helvetica);
        var reference = fonts.ReferenceOf(StandardFont.Helvetica);

        Assert.Equal("F1", name.Value);
        Assert.Equal(name, fonts.NameOf(StandardFont.Helvetica));
        Assert.Equal(reference.ObjectNumber, fonts.ReferenceOf(StandardFont.Helvetica).ObjectNumber);
        Assert.Equal(reference.ObjectNumber + 1, file.Reserve().ObjectNumber); // nothing else was reserved
    }

    [Fact]
    public void WriteAllWritesEachUsedFontOnce()
    {
        var destination = new MemoryStream();
        using (var file = new PdfFileWriter(destination, new PdfWriterOptions()))
        {
            var fonts = new FontResources(file);
            fonts.NameOf(StandardFont.Helvetica);

            fonts.WriteAll();
            file.Close(fonts.ReferenceOf(StandardFont.Helvetica));
        }

        var structure = PdfFileStructure.Parse(destination.ToArray());
        Assert.Equal(2, structure.Offsets.Count); // the free entry and the one font
        Assert.Contains("/BaseFont /Helvetica", structure.ObjectText(1), StringComparison.Ordinal);
    }
}
