using System.Collections;

namespace Maviray.Pdf.Writer;

/// <summary>A PDF array, written as <c>[a b c]</c> with single spaces between items.</summary>
internal sealed class PdfArray : PdfObject, IReadOnlyList<PdfObject>
{
    private readonly List<PdfObject> _items = [];

    public PdfArray(params PdfObject[] items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var item in items)
        {
            Add(item);
        }
    }

    public int Count => _items.Count;

    public PdfObject this[int index] => _items[index];

    public void Add(PdfObject item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items.Add(item);
    }

    public IEnumerator<PdfObject> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override void WriteTo(PdfOutput output)
    {
        output.WriteByte((byte)'[');
        for (var i = 0; i < _items.Count; i++)
        {
            if (i > 0)
            {
                output.WriteByte((byte)' ');
            }

            _items[i].WriteTo(output);
        }

        output.WriteByte((byte)']');
    }
}
