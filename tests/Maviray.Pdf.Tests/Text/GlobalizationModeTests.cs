namespace Maviray.Pdf.Tests.Text;

// CI runs the text tests twice: as usual, and with DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true (spec T-01). This test
// proves that each run is in the mode it claims, by the one effect the spec names: in invariant mode, .NET's own
// normalization returns decomposed text unchanged. It is in tests/, outside the ban on Normalize in src/.
public sealed class GlobalizationModeTests
{
    [Fact]
    public void RunsInTheModeTheEnvironmentAsks()
    {
        var variable = Environment.GetEnvironmentVariable("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT");
        var invariant = string.Equals(variable, "true", StringComparison.OrdinalIgnoreCase) || variable == "1";

        var composed = "é".Normalize();

        Assert.Equal(invariant ? "é" : "é", composed);
    }
}
