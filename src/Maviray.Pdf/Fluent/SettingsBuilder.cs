using System.IO.Compression;

namespace Maviray.Pdf;

/// <summary>How a document is generated: deterministic output, compression, its fonts, and where diagnostics go.</summary>
public sealed class SettingsBuilder
{
    internal SettingsBuilder()
    {
    }

    internal bool IsDeterministic { get; private set; }

    internal CompressionLevel CompressionLevel { get; private set; } = CompressionLevel.Optimal;

    internal FontRegistry Registry { get; private set; } = FontRegistry.Default;

    internal Action<Diagnostic>? Handler { get; private set; }

    /// <summary>
    /// Deterministic output: the same document and settings give identical bytes on every platform. The file's ID is
    /// a hash of its content, and a creation date is written only when the metadata sets one.
    /// </summary>
    /// <param name="value">True to turn it on, false to turn it off again.</param>
    public SettingsBuilder Deterministic(bool value = true)
    {
        IsDeterministic = value;
        return this;
    }

    /// <summary>How the content and fonts are compressed; <see cref="CompressionLevel.Optimal"/> unless set.</summary>
    /// <param name="level">The level.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> isn't a defined value.</exception>
    public SettingsBuilder Compression(CompressionLevel level)
    {
        if (!Enum.IsDefined(level))
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "The compression level isn't a defined value.");
        }

        CompressionLevel = level;
        return this;
    }

    /// <summary>The registry the document's font families come from; <see cref="FontRegistry.Default"/> unless set.</summary>
    /// <param name="registry">The registry.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is null.</exception>
    public SettingsBuilder Fonts(FontRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Registry = registry;
        return this;
    }

    /// <summary>Called on the generating thread for each diagnostic, such as a missing glyph. Without it, they are dropped.</summary>
    /// <param name="handler">The handler.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is null.</exception>
    public SettingsBuilder OnDiagnostic(Action<Diagnostic> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Handler = handler;
        return this;
    }
}
