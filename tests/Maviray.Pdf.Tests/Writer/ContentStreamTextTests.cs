using System.Text;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

public sealed class ContentStreamTextTests : IDisposable
{
    private static readonly StandardFont Helvetica = StandardFont.Helvetica;

    private readonly PdfFileWriter _file = new(new MemoryStream(), new PdfWriterOptions());
    private readonly FontResources _fonts;

    public ContentStreamTextTests()
    {
        _fonts = new FontResources(_file);
    }

    [Fact]
    public void WritesTheTextOperators()
    {
        var written = Written(builder => builder
            .BeginText()
            .SetFont(Helvetica, 24)
            .SetTextMatrix(1, 0, 0, 1, 72, 750)
            .ShowText("Hello, world")
            .EndText());

        Assert.Equal("BT\n/F1 24 Tf\n1 0 0 1 72 750 Tm\n(Hello, world) Tj\nET\n", written);
    }

    [Fact]
    public void EscapesTextDelimiters()
    {
        var written = Written(builder => builder.BeginText().SetFont(Helvetica, 10).ShowText("a(b)c\\d").EndText());

        Assert.Contains("(a\\(b\\)c\\\\d) Tj\n", written, StringComparison.Ordinal);
    }

    [Fact]
    public void SkipsAFontThatIsAlreadyCurrentAcrossTextObjects()
    {
        var written = Written(builder => builder
            .BeginText().SetFont(Helvetica, 24).EndText()
            .BeginText().SetFont(Helvetica, 24).EndText()
            .BeginText().SetFont(Helvetica, 12).EndText());

        Assert.Equal("BT\n/F1 24 Tf\nET\nBT\nET\nBT\n/F1 12 Tf\nET\n", written);
    }

    [Fact]
    public void RestoreStateRestoresTheFont()
    {
        var written = Written(builder => builder
            .BeginText().SetFont(Helvetica, 24).EndText()
            .SaveState()
            .BeginText().SetFont(Helvetica, 12).EndText()
            .RestoreState()
            .BeginText().SetFont(Helvetica, 24).EndText());

        Assert.Equal("BT\n/F1 24 Tf\nET\nq\nBT\n/F1 12 Tf\nET\nQ\nBT\nET\n", written);
    }

    [Theory]
    [InlineData("Tj")]
    [InlineData("Tf")]
    [InlineData("Tm")]
    [InlineData("ET")]
    public void TextOperatorsOutsideATextObjectThrow(string textOperator)
    {
        using var builder = new ContentStreamBuilder(_fonts);

        Assert.Throws<InvalidOperationException>(() => _ = textOperator switch
        {
            "Tj" => builder.ShowText("a"),
            "Tf" => builder.SetFont(Helvetica, 12),
            "Tm" => builder.SetTextMatrix(1, 0, 0, 1, 0, 0),
            "ET" => builder.EndText(),
            _ => throw new ArgumentOutOfRangeException(nameof(textOperator)),
        });
    }

    [Theory]
    [InlineData("re")]
    [InlineData("q")]
    [InlineData("Q")]
    [InlineData("cm")]
    [InlineData("BT")]
    public void PathStateAndNestedTextOperatorsInsideATextObjectThrow(string other)
    {
        using var builder = new ContentStreamBuilder(_fonts);
        builder.SaveState().BeginText();

        Assert.Throws<InvalidOperationException>(() => _ = other switch
        {
            "re" => builder.Rectangle(0, 0, 1, 1),
            "q" => builder.SaveState(),
            "Q" => builder.RestoreState(),
            "cm" => builder.Transform(1, 0, 0, 1, 0, 0),
            "BT" => builder.BeginText(),
            _ => throw new ArgumentOutOfRangeException(nameof(other)),
        });
    }

    [Fact]
    public void ShowingTextBeforeSettingAFontThrows()
    {
        using var builder = new ContentStreamBuilder(_fonts);
        builder.BeginText();

        Assert.Throws<InvalidOperationException>(() => builder.ShowText("a"));
    }

    [Fact]
    public void FinishingWithAnOpenTextObjectThrows()
    {
        using var builder = new ContentStreamBuilder(_fonts);
        builder.BeginText();

        Assert.Throws<InvalidOperationException>(() => builder.Finish());
    }

    [Fact]
    public void ColourChangesInsideATextObjectAreAllowed()
    {
        var written = Written(builder => builder.BeginText().SetFillColor(new RgbColor(1, 0, 0)).EndText());

        Assert.Equal("BT\n1 0 0 rg\nET\n", written);
    }

    [Fact]
    public void AnUnencodableCharacterWritesNothing()
    {
        using var builder = new ContentStreamBuilder(_fonts);
        builder.BeginText().SetFont(Helvetica, 12);

        Assert.Throws<ArgumentException>(() => builder.ShowText("ok ğ"));

        var written = Encoding.Latin1.GetString(builder.EndText().Finish());
        Assert.Equal("BT\n/F1 12 Tf\nET\n", written);
    }

    [Fact]
    public void FontsUsedListsEachFontOnce()
    {
        using var builder = new ContentStreamBuilder(_fonts);

        builder.BeginText().SetFont(Helvetica, 12).SetFont(Helvetica, 14).EndText();

        Assert.Equal([Helvetica], builder.FontsUsed);
    }

    [Fact]
    public void SettingAFontWithoutFontResourcesThrows()
    {
        using var builder = new ContentStreamBuilder();
        builder.BeginText();

        Assert.Throws<InvalidOperationException>(() => builder.SetFont(Helvetica, 12));
    }

    public void Dispose() => _file.Dispose();

    private string Written(Action<ContentStreamBuilder> build)
    {
        using var builder = new ContentStreamBuilder(_fonts);
        build(builder);
        return Encoding.Latin1.GetString(builder.Finish());
    }
}
