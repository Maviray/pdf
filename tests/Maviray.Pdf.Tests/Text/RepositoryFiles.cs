namespace Maviray.Pdf.Tests.Text;

/// <summary>
/// Files of the repository that tests read or write, such as the notices or the generated Unicode tables. The root is
/// found at run time by searching upward from the test output for the solution file, so no build step copies anything.
/// </summary>
internal static class RepositoryFiles
{
    private const string Marker = "Maviray.Pdf.slnx";

    /// <summary>The repository's root folder, the one that holds <c>Maviray.Pdf.slnx</c>.</summary>
    public static string Root { get; } = FindRoot();

    /// <summary>The absolute path of a repository path such as <c>src/Directory.Build.props</c>.</summary>
    public static string PathOf(string relativePath) => Path.GetFullPath(Path.Combine(Root, relativePath));

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, Marker)))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException($"No parent of {AppContext.BaseDirectory} holds {Marker}.");
    }
}
