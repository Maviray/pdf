using Maviray.Pdf.Layout;
using Maviray.Pdf.Tests.Drawing;
using Maviray.Pdf.Tests.Fonts;

namespace Maviray.Pdf.Tests.Layout;

// Diagnostics (spec L-14): each at most once per document, and only from the render pass.
public sealed class DiagnosticSinkTests
{
    [Fact]
    public void EachIsReportedOnce()
    {
        var seen = new List<Diagnostic>();
        var sink = new DiagnosticSink(seen.Add);
        var font = TestFonts.NotoSans("Regular");

        sink.MissingGlyph(font, 0x4E00, "Page 1 > Content > Text", 1, "no glyph");
        sink.MissingGlyph(font, 0x4E00, "Page 2 > Content > Text", 2, "no glyph");
        sink.Substituted("Noto Sans", 300, true, "Page 1 > Content > Text", 1, "substituted");
        sink.Substituted("noto sans", 300, true, "Page 3 > Content > Text", 3, "substituted");

        Assert.Equal(
            [(DiagnosticKind.MissingGlyph, "Page 1 > Content > Text", 1, "no glyph"), (DiagnosticKind.FontFaceSubstituted, "Page 1 > Content > Text", 1, "substituted")],
            seen.Select(d => (d.Kind, d.ElementPath, d.PageNumber, d.Message)));
    }

    [Fact]
    public void DifferentFontsCodePointsOrFacesAreReportedSeparately()
    {
        var seen = new List<Diagnostic>();
        var sink = new DiagnosticSink(seen.Add);

        sink.MissingGlyph(TestFonts.NotoSans("Regular"), 0x4E00, "a", 1, "m");
        sink.MissingGlyph(TestFonts.NotoSans("Bold"), 0x4E00, "a", 1, "m");
        sink.MissingGlyph(TestFonts.NotoSans("Regular"), 0x4E01, "a", 1, "m");
        sink.Substituted("Noto Sans", 300, true, "a", 1, "s");
        sink.Substituted("Noto Sans", 300, false, "a", 1, "s");
        sink.Substituted("Noto Sans", 600, true, "a", 1, "s");

        Assert.Equal(6, seen.Count);
    }

    [Fact]
    public void ANullHandlerDropsThem()
    {
        var sink = new DiagnosticSink(null);

        sink.MissingGlyph(TestFonts.NotoSans("Regular"), 0x4E00, "a", 1, "m");
        sink.Substituted("Noto Sans", 300, true, "a", 1, "s");
    }

    [Fact]
    public void OnlyTheRenderPassCarriesTheSink()
    {
        var probe = new SinkProbe("Probe");
        var content = new TestStack("Stack", [probe, new PageCountBlock("Count", _ => 10)]);

        LayoutEngine.Generate([new PageSet(595, 842, new Margins(50, 50, 50, 50), null, null, content, null)], new RecordingCanvas(), new DiagnosticSink(null));

        Assert.Equal([(false, false), (false, false), (true, true)], probe.Draws);
    }
}
