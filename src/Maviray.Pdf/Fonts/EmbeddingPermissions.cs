namespace Maviray.Pdf.Fonts;

/// <summary>
/// Reads a font's embedding permissions, <c>OS/2.fsType</c> (spec F-06). When several of bits 1 to 3 are set, the least
/// restrictive wins, as the OpenType specification says.
/// </summary>
internal static class EmbeddingPermissions
{
    private const ushort RestrictedLicense = 0x0002;
    private const ushort PreviewAndPrintOrEditable = 0x0004 | 0x0008;
    private const ushort NoSubsetting = 0x0100;
    private const ushort BitmapOnly = 0x0200;

    /// <exception cref="NotSupportedException">The permissions forbid embedding the font's outlines.</exception>
    public static FontEmbedding Resolve(ushort fsType)
    {
        if ((fsType & BitmapOnly) != 0)
        {
            throw new NotSupportedException(
                $"The font's embedding permissions (OS/2 fsType 0x{fsType:X4}) allow Bitmap embedding only, and the library embeds outlines.");
        }

        if ((fsType & RestrictedLicense) != 0 && (fsType & PreviewAndPrintOrEditable) == 0)
        {
            throw new NotSupportedException(
                $"The font's embedding permissions (OS/2 fsType 0x{fsType:X4}, Restricted License embedding) forbid embedding it in a document.");
        }

        return (fsType & NoSubsetting) != 0 ? FontEmbedding.Whole : FontEmbedding.Subset;
    }
}
