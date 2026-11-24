using Maviray.Pdf.Tests.Fonts;
using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Text;

// The metric set and the line height of spec T-08. Expected values come from the built fonts' own numbers, or from
// the values the spec quotes for Noto Sans.
public sealed class FontLineMetricsTests
{
    [Fact]
    public void TypographicMetricsWhenTheFontAsksForThem()
    {
        var font = LayoutTestFont.Create(b => b.LineMetrics(900, -300, 100).TypographicMetrics(700, -250, 50).Os2(4, 400, 0x00C0));

        Assert.Equal(new FontLineMetrics(700, -250, 50, 1000), FontLineMetrics.Of(font));
    }

    [Fact]
    public void HorizontalHeaderMetricsOtherwise()
    {
        var font = LayoutTestFont.Create(b => b.LineMetrics(900, -300, 100).TypographicMetrics(700, -250, 50));
        var withoutOs2 = LayoutTestFont.Create(b => b.LineMetrics(900, -300, 100).Without("OS/2"));

        Assert.Equal(new FontLineMetrics(900, -300, 100, 1000), FontLineMetrics.Of(font));
        Assert.Equal(new FontLineMetrics(900, -300, 100, 1000), FontLineMetrics.Of(withoutOs2));
    }

    [Fact]
    public void ANegativeLineGapCountsAsZero()
    {
        var font = LayoutTestFont.Create(b => b.LineMetrics(800, -200, -50));

        Assert.Equal(0, FontLineMetrics.Of(font).LineGap);
    }

    [Fact]
    public void AnEmptyExtentFallsBackToEightAndTwoTenthsOfAnEm()
    {
        var font = LayoutTestFont.Create(b => b.LineMetrics(0, 0, 0));

        Assert.Equal(new FontLineMetrics(800, -200, 0, 1000), FontLineMetrics.Of(font));
    }

    [Fact]
    public void NotoSansHasTheSpecsNormalLineHeight()
    {
        var metrics = FontLineMetrics.Of(TestFonts.NotoSans("Regular"));

        Assert.Equal(1.362, metrics.NormalLineHeight, 12);
        var (above, below) = metrics.Extent(10, null);
        Assert.Equal(10.69, above, 12);
        Assert.Equal(2.93, below, 12);
    }

    [Fact]
    public void LeadingIsSharedAboveAndBelow()
    {
        var metrics = FontLineMetrics.Of(LayoutTestFont.Font);

        // Line height 2 at 10 pt: 20 pt in all, 10 pt of it leading, half above and half below.
        var (above, below) = metrics.Extent(10, 2);
        Assert.Equal(13, above, 12);
        Assert.Equal(7, below, 12);

        // Line height 0.5: a negative leading of 5 pt.
        (above, below) = metrics.Extent(10, 0.5);
        Assert.Equal(5.5, above, 12);
        Assert.Equal(-0.5, below, 12);
    }
}
