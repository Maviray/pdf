namespace Maviray.Pdf;

/// <summary>Builds a document: its settings, its metadata and its page sets, in order.</summary>
public sealed class DocumentBuilder
{
    internal DocumentBuilder()
    {
    }

    internal SettingsBuilder SettingsBuilder { get; } = new();

    internal MetadataBuilder MetadataBuilder { get; } = new();

    internal List<PageBuilder> Pages { get; } = [];

    /// <summary>How the document is generated.</summary>
    /// <param name="configure">Sets the settings.</param>
    public DocumentBuilder Settings(Action<SettingsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(SettingsBuilder);
        return this;
    }

    /// <summary>The document's information: its title, author, language and more.</summary>
    /// <param name="configure">Sets the metadata.</param>
    public DocumentBuilder Metadata(Action<MetadataBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(MetadataBuilder);
        return this;
    }

    /// <summary>A page set: pages of one size and margins, whose content flows across as many pages as it needs. Each
    /// page set starts on a new page, and page numbers run through the whole document.</summary>
    /// <param name="compose">Builds the page set.</param>
    public DocumentBuilder Page(Action<PageBuilder> compose)
    {
        ArgumentNullException.ThrowIfNull(compose);
        var page = new PageBuilder();
        compose(page);
        Pages.Add(page);
        return this;
    }
}
