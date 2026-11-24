using System.Text;
using Maviray.Pdf.Elements;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Tests.Drawing;
using Maviray.Pdf.Tests.Fonts;
using Maviray.Pdf.Tests.Layout;
using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Elements;

// The Text element (spec L-07), in Noto Sans at 10 pt with a line height of 1.2, so each line is 12 points.
public sealed class TextElementTests
{
    private static readonly FontRegistry Fonts = new FontRegistry()
        .Register("Noto Sans", File.ReadAllBytes(TestFonts.NotoSansPath("Regular")))
        .Register("Noto Sans", File.ReadAllBytes(TestFonts.NotoSansPath("Bold")))
        .Register("Noto Sans", File.ReadAllBytes(TestFonts.NotoSansPath("Italic")))
        .Register("Noto Sans", File.ReadAllBytes(TestFonts.NotoSansPath("BoldItalic")));

    private static readonly TextStyle Base = new TextStyle().FontSize(10).LineHeight(1.2);

    private static readonly string ThirtyLines = string.Join("\n", Enumerable.Range(1, 30).Select(i => $"Zeile {i}"));

    [Fact]
    public void ItSplitsByLineAndEachPartStartsAtItsTop()
    {
        var page = Page();
        var measurement = Text(ThirtyLines).Measure(new Size(495, 100), page);
        var canvas = Generate(Text(ThirtyLines), height: 200);

        Assert.Equal((Fit.Partial, 96.0), (measurement.Fit, Math.Round(measurement.Size.Height, 6)));
        Assert.Equal([8, 8, 8, 6], canvas.Pages.Select(p => p.Calls.Count));
        Assert.Equal(("Zeile 9", 50.0), (LineText(canvas.Pages[1].Calls[0]), canvas.Pages[1].Calls[0].Y));
    }

    [Fact]
    public void ALastLineThatFitsExactlyIsFull() =>
        Assert.Equal(Fit.Full, Text(string.Join("\n", Enumerable.Range(1, 8).Select(i => $"Zeile {i}"))).Measure(new Size(495, 96), Page()).Fit);

    [Fact]
    public void NoLineFitsIsNoneWithTheFirstLinesSize()
    {
        var measurement = Text(ThirtyLines).Measure(new Size(495, 11), Page());

        Assert.Equal((Fit.None, 12.0), (measurement.Fit, Math.Round(measurement.Needed!.Value.Height, 6)));
    }

    [Fact]
    public void TheFillIsCachedByWidth()
    {
        var text = Text(ThirtyLines);
        var page = Page();

        text.Measure(new Size(495, 100), page);
        text.Measure(new Size(495, 100), page);
        Assert.Equal(1, text.FillCount);

        text.Measure(new Size(300, 100), page);
        text.Measure(new Size(double.PositiveInfinity, 100), page);
        text.Measure(new Size(300, 100), page);
        text.Measure(new Size(double.PositiveInfinity, 100), page);
        Assert.Equal(3, text.FillCount);

        text.Measure(new Size(495, 100), page);
        Assert.Equal(4, text.FillCount);
    }

    [Fact]
    public void PageNumberAndPageCountSpansShowTheirNumbers()
    {
        var footer = new TextElement(
            [Run("Seite "), new TextRun(TextRunKind.PageNumber, "", new TextStyle()), Run(" von "), new TextRun(TextRunKind.PageCount, "", new TextStyle())],
            Base,
            new TextStyle(),
            TextAlignment.Left,
            Fonts);
        var canvas = new RecordingCanvas();
        var content = new Column(Enumerable.Range(1, 10).Select(i => (Element)new FixedBlock($"Block {i}", 100, 100, new Maviray.Pdf.Writer.RgbColor(0, 0, 1))), 0);

        LayoutEngine.Generate([new PageSet(595, 842, new Margins(50, 50, 50, 50), null, null, content, footer)], canvas);

        Assert.True(footer.ShowsPageCount);
        Assert.Equal(["Seite 1 von 2", "Seite 2 von 2"], canvas.Pages.Select(p => LineText(Assert.Single(p.Calls, c => c.Kind == "TextLine"))));
    }

    [Fact]
    public void ATooWideCharacterThrows()
    {
        var text = new TextElement([Run("W")], new TextStyle().FontSize(100), new TextStyle(), TextAlignment.Left, Fonts);

        var error = Assert.Throws<LayoutException>(() => text.Measure(new Size(20, 500), Page()));

        Assert.Equal("Page 1 > Text", error.ElementPath);
        Assert.Matches(@"^Page 1 > Text: a character is [\d.]+ pt wide and the space is 20 pt\.$", error.Message);
    }

    [Fact]
    public void EmptyTextIsFullWithNoSize()
    {
        Assert.Equal((Fit.Full, new Size(0, 0)), Shape(Text("").Measure(new Size(495, 100), Page())));
        Assert.Equal((Fit.Full, new Size(0, 0)), Shape(new TextElement([], Base, new TextStyle(), TextAlignment.Left, Fonts).Measure(new Size(495, 100), Page())));
    }

    [Fact]
    public void TheStylesResolveAndTheFontsMatch()
    {
        var red = Color.FromHex("#CC0000");
        var text = new TextElement([Run("normal "), new TextRun(TextRunKind.Text, "fett", new TextStyle().Bold().FontColor(red))], Base, new TextStyle(), TextAlignment.Left, Fonts);

        var (_, calls) = ElementCanvas.Draw(text, new Size(495, 100));

        var line = Assert.Single(calls);
        Assert.Equal([Color.Black.ToRgb(), red.ToRgb()], line.SpanColors);
        Assert.Equal("NotoSans-Bold", line.Layout!.Source.Source.Spans[1].Style.Font.PostScriptName);
    }

    [Fact]
    public void AnUnknownFamilyThrows()
    {
        var text = new TextElement([Run("Hallo")], Base.FontFamily("Arial"), new TextStyle(), TextAlignment.Left, Fonts);

        var error = Assert.Throws<LayoutException>(() => text.Measure(new Size(495, 100), Page()));

        Assert.Equal("Page 1 > Text: the font family 'Arial' isn't registered. The registered families are Noto Sans.", error.Message);
    }

    [Fact]
    public void TheDefaultFamilyMissingPointsAtTheDefaultFontsPackage()
    {
        var text = new TextElement([Run("Hallo")], new TextStyle(), new TextStyle(), TextAlignment.Left, new FontRegistry());

        var error = Assert.Throws<LayoutException>(() => text.Measure(new Size(495, 100), Page()));

        Assert.Equal(
            "Page 1 > Text: the font family 'Noto Sans' isn't registered, and no family is registered. Reference Maviray.Pdf.Fonts.Default and call RegisterNotoSans(), or register a font and make it the default style's family.",
            error.Message);
    }

    [Fact]
    public void AMissingGlyphIsReportedOnceInTheRenderPass()
    {
        var seen = new List<Diagnostic>();
        var cjk = char.ConvertFromUtf32(0x4E2D);
        var content = new Column([Text("A " + cjk), Text("B " + cjk)], 0);

        LayoutEngine.Generate([new PageSet(595, 842, new Margins(50, 50, 50, 50), null, null, content, null)], new RecordingCanvas(), new DiagnosticSink(seen.Add));

        var diagnostic = Assert.Single(seen);
        Assert.Equal((DiagnosticKind.MissingGlyph, "Page 1 > Content > Column > Item 1 > Text", 1), (diagnostic.Kind, diagnostic.ElementPath, diagnostic.PageNumber));
        Assert.Equal("NotoSans-Regular has no glyph for U+4E2D, which draws as .notdef.", diagnostic.Message);
    }

    [Fact]
    public void ASubstitutedFaceIsReported()
    {
        var seen = new List<Diagnostic>();
        var upright = new FontRegistry()
            .Register("Noto Sans", File.ReadAllBytes(TestFonts.NotoSansPath("Regular")))
            .Register("Noto Sans", File.ReadAllBytes(TestFonts.NotoSansPath("Bold")));
        var text = new TextElement([Run("leicht")], Base.Weight(FontWeight.Light).Italic(), new TextStyle(), TextAlignment.Left, upright);

        LayoutEngine.Generate([new PageSet(595, 842, new Margins(50, 50, 50, 50), null, null, text, null)], new RecordingCanvas(), new DiagnosticSink(seen.Add));

        var diagnostic = Assert.Single(seen);
        Assert.Equal(DiagnosticKind.FontFaceSubstituted, diagnostic.Kind);
        Assert.Equal("No italic face of weight 300 in 'Noto Sans'; Noto Sans 400 upright is used instead.", diagnostic.Message);
    }

    [Fact]
    public void ItAlignsItsLines()
    {
        var text = new TextElement([Run("Mitte")], Base, new TextStyle(), TextAlignment.Center, Fonts);

        var (measurement, calls) = ElementCanvas.Draw(text, new Size(495, 100));

        var line = calls[0].Layout!.Lines[0];
        Assert.Equal((495 - measurement.Size.Width) / 2, line.Offset, 9);
    }

    // With room for one line, a too-wide second line is the next page's problem, not this one's.
    [Fact]
    public void TheTooWideCheckCountsOnlyTheLinesThatFit()
    {
        var text = new TextElement(
            [Run("a\n"), new TextRun(TextRunKind.Text, "W", new TextStyle().FontSize(100))],
            Base,
            new TextStyle(),
            TextAlignment.Left,
            Fonts);

        Assert.Equal(Fit.Partial, text.Measure(new Size(20, 12), Page()).Fit);
    }

    [Fact]
    public void APageNumberAtAnInfiniteWidthPreparesAgain()
    {
        var footer = new TextElement([Run("Seite "), new TextRun(TextRunKind.PageNumber, "", new TextStyle())], Base, new TextStyle(), TextAlignment.Left, Fonts);
        var infinite = new Size(double.PositiveInfinity, 100);

        var one = footer.Measure(infinite, new PageContext(1, 10, isRenderPass: true)).Size.Width;
        var ten = footer.Measure(infinite, new PageContext(10, 10, isRenderPass: true)).Size.Width;

        Assert.True(ten > one, $"Page 10's footer is {ten} wide, page 1's {one}.");
    }

    [Fact]
    public void LetterSpacingIsAFractionOfTheSize()
    {
        var plain = new TextElement([Run("ab")], Base, new TextStyle(), TextAlignment.Left, Fonts).Measure(new Size(495, 100), Page()).Size.Width;
        var spaced = new TextElement([Run("ab")], Base.LetterSpacing(0.1), new TextStyle(), TextAlignment.Left, Fonts).Measure(new Size(495, 100), Page()).Size.Width;

        Assert.Equal(plain + 1, spaced, 9);
    }

    [Fact]
    public void TheElementsStyleWinsOverTheInheritedOne()
    {
        var text = new TextElement([Run("Hallo")], new TextStyle().FontSize(14).LineHeight(1.2), new TextStyle().FontSize(10), TextAlignment.Left, Fonts);

        Assert.Equal(16.8, text.Measure(new Size(495, 100), Page()).Size.Height, 6);
    }

    [Fact]
    public void UnderlineAndStrikethroughAreEachTheirOwn()
    {
        var text = new TextElement(
            [new TextRun(TextRunKind.Text, "unter ", new TextStyle().Underline()), new TextRun(TextRunKind.Text, "durch", new TextStyle().Strikethrough())],
            Base,
            new TextStyle(),
            TextAlignment.Left,
            Fonts);

        var (_, calls) = ElementCanvas.Draw(text, new Size(495, 100));

        var kinds = calls[0].Layout!.Lines[0].Decorations.Select(decoration => (decoration.SpanIndex, decoration.Kind));
        Assert.Equal([(0, DecorationKind.Underline), (1, DecorationKind.Strikethrough)], kinds);
    }

    // A parent that adds lengths in another order can give the Text a hair less than it measured (P1.10 review).
    [Fact]
    public void AHeightAHairShortOfTheMeasuredOneStillFits()
    {
        var text = new TextElement([Run(string.Join("\n", Enumerable.Range(1, 7).Select(i => $"Zeile {i}")))], new TextStyle().FontSize(10).LineHeight(1.23), new TextStyle(), TextAlignment.Left, Fonts);
        var height = text.Measure(new Size(495, 1000), Page()).Size.Height;

        Assert.Equal(Fit.Full, text.Measure(new Size(495, height - 5e-7), Page()).Fit);
    }

    [Fact]
    public void NoneNeedsTheFirstLinesWidthToo()
    {
        var measurement = Text(ThirtyLines).Measure(new Size(495, 11), Page());

        Assert.True(measurement.Needed!.Value.Width > 0, "None gave no width for the first line.");
    }

    [Fact]
    public void KeepsTheContract() => ElementContract.Check(
        () => Text(ThirtyLines),
        new Size(495, 100), new Size(300.5, 50.25), new Size(200, 11), new Size(495, 360));

    private static TextRun Run(string text) => new(TextRunKind.Text, text, new TextStyle());

    private static TextElement Text(string text) => new([Run(text)], Base, new TextStyle(), TextAlignment.Left, Fonts);

    private static PageContext Page() => new(1, 1, isRenderPass: true);

    private static (Fit, Size) Shape(Measurement measurement) => (measurement.Fit, measurement.Size);

    private static string LineText(CanvasCall call)
    {
        var text = new StringBuilder();
        foreach (var run in call.Layout!.Lines[call.LineIndex].Runs)
        {
            foreach (var glyph in run.Glyphs)
            {
                text.Append(char.ConvertFromUtf32(glyph.CodePoint));
            }
        }

        return text.ToString();
    }

    private static RecordingCanvas Generate(Element content, double height)
    {
        var canvas = new RecordingCanvas();
        LayoutEngine.Generate([new PageSet(595, height, new Margins(50, 50, 50, 50), null, null, content, null)], canvas);
        return canvas;
    }
}
