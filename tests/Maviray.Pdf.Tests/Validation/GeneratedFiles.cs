using System.Runtime.CompilerServices;

namespace Maviray.Pdf.Tests.Validation;

/// <summary>
/// Saves generated PDFs where the validators and people can find them:
/// <c>&lt;test output folder&gt;/generated/&lt;test file&gt;/&lt;test method&gt;.pdf</c>. The files are overwritten on every
/// run, are kept out of git by <c>bin/</c>, and CI uploads them as the <c>generated-pdfs</c> artifact (spec D-11, D-12).
/// </summary>
internal static class GeneratedFiles
{
    public static string Save(byte[] pdf, [CallerFilePath] string file = "", [CallerMemberName] string name = "") =>
        SaveAs(pdf, ".pdf", file, name);

    /// <summary>As <see cref="Save"/>, for a file of another kind, such as a font.</summary>
    public static string SaveAs(byte[] pdf, string extension, [CallerFilePath] string file = "", [CallerMemberName] string name = "")
    {
        ArgumentNullException.ThrowIfNull(pdf);

        var folder = Path.Combine(AppContext.BaseDirectory, "generated", Path.GetFileNameWithoutExtension(file));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name + extension);
        File.WriteAllBytes(path, pdf);
        return path;
    }
}
