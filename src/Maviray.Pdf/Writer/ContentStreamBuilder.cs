namespace Maviray.Pdf.Writer;

/// <summary>
/// Writes a page's content stream (spec D-09): operators in PDF user space, in points, with the origin at the bottom
/// left. It tracks the fill colour, stroke colour and line width, so a setting that is already current writes nothing,
/// and it refuses any operator order the PDF specification doesn't allow. The text operators arrive in P0.4.
/// </summary>
internal sealed class ContentStreamBuilder : IDisposable
{
    private readonly MemoryStream _buffer = new();
    private readonly PdfOutput _output;
    private readonly Stack<GraphicsState> _saved = new();

    // Every tracked value starts unknown (null), so the first setting on a page is always written.
    private GraphicsState _state;
    private bool _pathOpen;
    private bool _finished;

    public ContentStreamBuilder()
    {
        _output = new PdfOutput(_buffer, bufferSize: 4096);
    }

    /// <summary>True until the first operator is written.</summary>
    public bool IsEmpty => _output.Position == 0;

    /// <summary>Writes <c>q</c>: saves the graphics state, including the tracked values.</summary>
    public ContentStreamBuilder SaveState()
    {
        ThrowIfPathOpen();
        _saved.Push(_state);
        return Operator("q"u8);
    }

    /// <summary>Writes <c>Q</c>: restores the graphics state saved by the matching <see cref="SaveState"/>.</summary>
    public ContentStreamBuilder RestoreState()
    {
        ThrowIfPathOpen();
        if (_saved.Count == 0)
        {
            throw new InvalidOperationException("RestoreState (Q) has no matching SaveState (q).");
        }

        _state = _saved.Pop();
        return Operator("Q"u8);
    }

    /// <summary>Writes <c>cm</c>: concatenates the matrix [a b c d e f] with the current transformation.</summary>
    public ContentStreamBuilder Transform(double a, double b, double c, double d, double e, double f)
    {
        ThrowIfPathOpen();
        CheckMatrix(a, b, c, d, e, f);
        Operands(a, b, c, d);
        Operands(e, f);
        return Operator("cm"u8);
    }

    /// <summary>Writes <c>w</c>, unless <paramref name="width"/> is already the line width.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The width is negative, or not a number a PDF can hold. Zero is
    /// allowed: it draws the thinnest line the device can.</exception>
    public ContentStreamBuilder SetLineWidth(double width)
    {
        ThrowIfPathOpen();
        Check(width, nameof(width));
        if (width < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "A line width can't be negative.");
        }

        if (_state.LineWidth == width)
        {
            return this;
        }

        _state = _state with { LineWidth = width };
        Operands(width);
        return Operator("w"u8);
    }

    /// <summary>Writes <c>rg</c>, unless <paramref name="color"/> is already the fill colour.</summary>
    public ContentStreamBuilder SetFillColor(RgbColor color)
    {
        ThrowIfPathOpen();
        if (_state.Fill == color)
        {
            return this;
        }

        _state = _state with { Fill = color };
        Operands(color.Red, color.Green, color.Blue);
        return Operator("rg"u8);
    }

    /// <summary>Writes <c>RG</c>, unless <paramref name="color"/> is already the stroke colour.</summary>
    public ContentStreamBuilder SetStrokeColor(RgbColor color)
    {
        ThrowIfPathOpen();
        if (_state.Stroke == color)
        {
            return this;
        }

        _state = _state with { Stroke = color };
        Operands(color.Red, color.Green, color.Blue);
        return Operator("RG"u8);
    }

    /// <summary>Writes <c>re</c>: adds a rectangle to the current path, which stays open until it is painted.</summary>
    public ContentStreamBuilder Rectangle(double x, double y, double width, double height)
    {
        ThrowIfFinished();
        Check(x, nameof(x));
        Check(y, nameof(y));
        Check(width, nameof(width));
        Check(height, nameof(height));
        Operands(x, y, width, height);
        _pathOpen = true;
        return Operator("re"u8);
    }

    /// <summary>Writes <c>f</c>: fills the current path.</summary>
    public ContentStreamBuilder Fill() => Paint("f"u8);

    /// <summary>Writes <c>S</c>: strokes the current path.</summary>
    public ContentStreamBuilder Stroke() => Paint("S"u8);

    /// <summary>Writes <c>B</c>: fills, then strokes, the current path.</summary>
    public ContentStreamBuilder FillAndStroke() => Paint("B"u8);

    /// <summary>Returns the content stream's bytes. The builder can't be used afterwards.</summary>
    /// <exception cref="InvalidOperationException">A path is still open, or a <c>q</c> has no matching <c>Q</c>.</exception>
    public byte[] Finish()
    {
        ThrowIfFinished();
        if (_pathOpen)
        {
            throw new InvalidOperationException("A path is still open. Paint it with Fill, Stroke or FillAndStroke first.");
        }

        if (_saved.Count > 0)
        {
            throw new InvalidOperationException($"{_saved.Count} SaveState (q) without a matching RestoreState (Q).");
        }

        _output.Flush();
        _finished = true;
        return _buffer.ToArray();
    }

    public void Dispose()
    {
        _output.Dispose();
        _buffer.Dispose();
    }

    private ContentStreamBuilder Paint(ReadOnlySpan<byte> paintOperator)
    {
        ThrowIfFinished();
        if (!_pathOpen)
        {
            throw new InvalidOperationException("There is no path to paint. Build one with Rectangle first.");
        }

        _pathOpen = false;
        return Operator(paintOperator);
    }

    // Operands are checked before any of them is written, so a call that throws leaves the stream and the tracked state
    // as they were. Writing checks them again, but by then a bad value would have left half an operator behind.
    private static void Check(double value, string paramName) => PdfNumberFormat.ThrowIfOutOfRange(value, paramName);

    private static void CheckMatrix(double a, double b, double c, double d, double e, double f)
    {
        Check(a, nameof(a));
        Check(b, nameof(b));
        Check(c, nameof(c));
        Check(d, nameof(d));
        Check(e, nameof(e));
        Check(f, nameof(f));
    }

    private void Operands(params ReadOnlySpan<double> values)
    {
        foreach (var value in values)
        {
            _output.WriteNumber(value);
            _output.WriteByte((byte)' ');
        }
    }

    private ContentStreamBuilder Operator(ReadOnlySpan<byte> name)
    {
        _output.Write(name);
        _output.WriteByte((byte)'\n');
        return this;
    }

    // Between building a path and painting it, PDF allows only more path construction.
    private void ThrowIfPathOpen()
    {
        ThrowIfFinished();
        if (_pathOpen)
        {
            throw new InvalidOperationException(
                "A path is open. Paint it with Fill, Stroke or FillAndStroke before changing the graphics state.");
        }
    }

    private void ThrowIfFinished()
    {
        if (_finished)
        {
            throw new InvalidOperationException("The content stream is finished.");
        }
    }

    // The values the builder tracks; null means not known yet.
    private record struct GraphicsState(RgbColor? Fill, RgbColor? Stroke, double? LineWidth);
}
