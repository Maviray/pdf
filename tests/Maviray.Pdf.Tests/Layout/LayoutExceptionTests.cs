using System.Globalization;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Tests.Layout;

public sealed class LayoutExceptionTests
{
    [Fact]
    public void TheMessageNamesThePathAndTheReason()
    {
        var exception = new LayoutException("Page 3 > Content > Block", "it is 31.2 pt wide and the space is 20 pt.", 20, 100);

        Assert.Equal("Page 3 > Content > Block: it is 31.2 pt wide and the space is 20 pt.", exception.Message);
        Assert.Equal("Page 3 > Content > Block", exception.ElementPath);
    }

    [Fact]
    public void ItKeepsTheAvailableSize()
    {
        var exception = new LayoutException("Page 1 > Content", "it can never fit.", 451.28, 722);

        Assert.Equal((451.28, 722.0), (exception.AvailableWidth, exception.AvailableHeight));
    }

    [Fact]
    public void LengthsAreWrittenWithoutTheCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            Assert.Equal("31.25 pt", LayoutErrors.Length(31.25));
            Assert.Equal("20 pt", LayoutErrors.Length(20));
            Assert.Equal("0.33 pt", LayoutErrors.Length(1.0 / 3));
            Assert.Equal("451.28 × 722 pt", LayoutErrors.Size(new Size(451.28, 722)));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
