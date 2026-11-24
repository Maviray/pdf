namespace Maviray.Pdf;

/// <summary>The document information a viewer shows: title, author, subject, keywords, creator, language and date.</summary>
public sealed class MetadataBuilder
{
    internal MetadataBuilder()
    {
    }

    internal string? TitleText { get; private set; }

    internal string? AuthorText { get; private set; }

    internal string? SubjectText { get; private set; }

    internal string? KeywordsText { get; private set; }

    internal string? CreatorText { get; private set; }

    internal string? LanguageTag { get; private set; }

    internal DateTimeOffset? Date { get; private set; }

    /// <summary>The title, which viewers show instead of the file name.</summary>
    /// <param name="title">The title.</param>
    public MetadataBuilder Title(string title) => Set(title, nameof(title), value => TitleText = value);

    /// <summary>The author.</summary>
    /// <param name="author">The author.</param>
    public MetadataBuilder Author(string author) => Set(author, nameof(author), value => AuthorText = value);

    /// <summary>The subject.</summary>
    /// <param name="subject">The subject.</param>
    public MetadataBuilder Subject(string subject) => Set(subject, nameof(subject), value => SubjectText = value);

    /// <summary>The keywords.</summary>
    /// <param name="keywords">The keywords, as one text.</param>
    public MetadataBuilder Keywords(string keywords) => Set(keywords, nameof(keywords), value => KeywordsText = value);

    /// <summary>The application that created the document.</summary>
    /// <param name="creator">Its name.</param>
    public MetadataBuilder Creator(string creator) => Set(creator, nameof(creator), value => CreatorText = value);

    /// <summary>The document's language, as a BCP 47 tag such as <c>de-DE</c>. Only its syntax is checked.</summary>
    /// <param name="tag">The tag: letters and digits in hyphen-separated parts of 1 to 8 characters.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tag"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="tag"/> isn't a BCP 47 tag by its syntax.</exception>
    public MetadataBuilder Language(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        if (!IsLanguageTag(tag))
        {
            throw new ArgumentException($"'{tag}' isn't a language tag: write letters and digits in parts of 1 to 8, joined by hyphens, such as de-DE.", nameof(tag));
        }

        LanguageTag = tag;
        return this;
    }

    /// <summary>When the document was created. Unless set, it is the time a save starts, and none in deterministic mode.</summary>
    /// <param name="date">The date and time, with its offset.</param>
    public MetadataBuilder CreationDate(DateTimeOffset date)
    {
        Date = date;
        return this;
    }

    private static bool IsLanguageTag(string tag)
    {
        var parts = tag.Split('-');
        return parts.All(part => part.Length is >= 1 and <= 8 && part.All(char.IsAsciiLetterOrDigit));
    }

    private MetadataBuilder Set(string value, string paramName, Action<string> store)
    {
        ArgumentNullException.ThrowIfNull(value, paramName);
        store(value);
        return this;
    }
}
