using System.Reflection;

namespace Maviray.Pdf.Tests;

/// <summary>
/// Checks the shipped Maviray.Pdf assembly against the packaging rules in the design document:
/// no dependencies beyond the .NET runtime (NFR-01) and safe for trimming (NFR-02).
/// </summary>
public sealed class CoreAssemblyTests
{
    private static readonly Assembly CoreAssembly = Assembly.Load("Maviray.Pdf");

    [Fact]
    public void ReferencesOnlyAssembliesShippedWithTheDotNetRuntime()
    {
        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        var foreignReferences = CoreAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => !File.Exists(Path.Combine(runtimeDirectory, name + ".dll")))
            .ToList();

        Assert.Empty(foreignReferences);
    }

    [Fact]
    public void IsMarkedTrimmable()
    {
        var isTrimmable = CoreAssembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Any(attribute => attribute.Key == "IsTrimmable" && attribute.Value == "True");

        Assert.True(isTrimmable);
    }
}
