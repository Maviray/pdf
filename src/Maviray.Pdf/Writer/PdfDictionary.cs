using System.Diagnostics.CodeAnalysis;

namespace Maviray.Pdf.Writer;

/// <summary>
/// A PDF dictionary, written as <c>&lt;&lt;/Key value /Key value&gt;&gt;</c>. Entries keep their insertion order, so the
/// output is the same on every run. Dictionaries are small, so lookups search the entries in order.
/// </summary>
internal sealed class PdfDictionary : PdfObject
{
    private readonly List<KeyValuePair<PdfName, PdfObject>> _entries = [];

    public int Count => _entries.Count;

    /// <summary>Adds an entry, or replaces the value of an existing key in place.</summary>
    /// <exception cref="ArgumentException"><paramref name="value"/> is the null object. In PDF a null value means the
    /// key is absent, so remove the key instead.</exception>
    public PdfDictionary Set(PdfName key, PdfObject value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        if (value is PdfNull)
        {
            throw new ArgumentException("A null value means the key is absent; remove the key instead.", nameof(value));
        }

        var index = IndexOf(key);
        if (index >= 0)
        {
            _entries[index] = new(key, value);
        }
        else
        {
            _entries.Add(new(key, value));
        }

        return this;
    }

    public bool Remove(PdfName key)
    {
        var index = IndexOf(key);
        if (index < 0)
        {
            return false;
        }

        _entries.RemoveAt(index);
        return true;
    }

    public bool TryGetValue(PdfName key, [MaybeNullWhen(false)] out PdfObject value)
    {
        var index = IndexOf(key);
        value = index >= 0 ? _entries[index].Value : null;
        return index >= 0;
    }

    public bool ContainsKey(PdfName key) => IndexOf(key) >= 0;

    public override void WriteTo(PdfOutput output)
    {
        output.Write("<<"u8);
        WriteEntries(output);
        output.Write(">>"u8);
    }

    /// <summary>Writes the entries without the brackets, so <see cref="PdfStream"/> can add <c>/Length</c>.</summary>
    internal void WriteEntries(PdfOutput output)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            if (i > 0)
            {
                output.WriteByte((byte)' ');
            }

            _entries[i].Key.WriteTo(output);
            output.WriteByte((byte)' ');
            _entries[i].Value.WriteTo(output);
        }
    }

    private int IndexOf(PdfName key)
    {
        ArgumentNullException.ThrowIfNull(key);
        for (var i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Key.Equals(key))
            {
                return i;
            }
        }

        return -1;
    }
}
