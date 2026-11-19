namespace Maviray.Pdf.Writer;

/// <summary>
/// Writes a page's content stream (spec D-09): operators in PDF user space, in points, with the origin at the bottom
/// left. It tracks the fill colour, stroke colour, line width, and font and size, so a setting that is already current
/// writes nothing, and it refuses any operator order the PDF specification doesn't allow.
/// </summary>
internal sealed class ContentStreamBuilder : IDisposable
{
    private readonly MemoryStream _buffer = new();
    private readonly PdfOutput _output;
    private readonly FontResources? _fonts;
    private readonly List<StandardFont> _fontsUsed = [];
    private readonly Stack<GraphicsState> _saved = new();

    // Every tracked value starts unknown (null), so the first setting on a page is always written. The font belongs to
    // the graphics state: it survives ET and is saved and restored by q and Q.
    private GraphicsState _state;
    private bool _pathOpen;
    private bool _inText;
    private bool _finished;

    /// <summary>A builder for graphics only: setting a font throws.</summary>
    public ContentStreamBuilder()
        : this(null)
    {
    }

    /// <summary>A builder whose fonts get their resource names from <paramref name="fonts"/>.</summary>
    public ContentStreamBuilder(FontResources? fonts)
    {
        _fonts = fonts;
        _output = new PdfOutput(_buffer, bufferSize: 4096);
    }

    /// <summary>True until the first operator is written.</summary>
    public bool IsEmpty => _output.Position == 0;

    /// <summary>The fonts this content stream set, each once, for the page's <c>/Resources</c>.</summary>
    public IReadOnlyCollection<StandardFont> FontsUsed => _fontsUsed;

    /// <summary>Writes <c>q</c>: saves the graphics state, including the tracked values.</summary>
    public ContentStreamBuilder SaveState()
    {
        ThrowIfPathOpenOrInText("SaveState (q)");
        _saved.Push(_state);
        return Operator("q"u8);
    }

    /// <summary>Writes <c>Q</c>: restores the graphics state saved by the matching <see cref="SaveState"/>.</summary>
    public ContentStreamBuilder RestoreState()
    {
        ThrowIfPathOpenOrInText("RestoreState (Q)");
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
        ThrowIfPathOpenOrInText("Transform (cm)");
        Operands(a, b, c, d);
        Operands(e, f);
        return Operator("cm"u8);
    }

    /// <summary>Writes <c>w</c>, unless <paramref name="width"/> is already the line width.</summary>
    public ContentStreamBuilder SetLineWidth(double width)
    {
        ThrowIfPathOpen();
        if (_state.LineWidth == width)
        {
            return this;
        }

        _state = _state with { LineWidth = width };
        Operands(width);
        return Operator("w"u8);
    }

    /// <summary>Writes <c>rg</c>, unless <paramref name="color"/> is already the fill colour. Allowed inside text.</summary>
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

    /// <summary>Writes <c>RG</c>, unless <paramref name="color"/> is already the stroke colour. Allowed inside text.</summary>
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
        ThrowIfInText("Rectangle (re)");
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

    /// <summary>Writes <c>BT</c>: begins a text object.</summary>
    public ContentStreamBuilder BeginText()
    {
        ThrowIfPathOpenOrInText("BeginText (BT)");
        _inText = true;
        return Operator("BT"u8);
    }

    /// <summary>Writes <c>ET</c>: ends the text object.</summary>
    public ContentStreamBuilder EndText()
    {
        ThrowIfNotInText("EndText (ET)");
        _inText = false;
        return Operator("ET"u8);
    }

    /// <summary>Writes <c>Tf</c>, unless <paramref name="font"/> at <paramref name="size"/> is already current.</summary>
    /// <exception cref="InvalidOperationException">This builder has no font resources, or no text object is open.</exception>
    public ContentStreamBuilder SetFont(StandardFont font, double size)
    {
        ArgumentNullException.ThrowIfNull(font);
        ThrowIfNotInText("SetFont (Tf)");
        if (_fonts is null)
        {
            throw new InvalidOperationException("This content stream has no font resources, so it can't set a font.");
        }

        if (ReferenceEquals(_state.Font, font) && _state.FontSize == size)
        {
            return this;
        }

        var name = _fonts.NameOf(font);
        if (!_fontsUsed.Contains(font))
        {
            _fontsUsed.Add(font);
        }

        _state = _state with { Font = font, FontSize = size };
        name.WriteTo(_output);
        _output.WriteByte((byte)' ');
        Operands(size);
        return Operator("Tf"u8);
    }

    /// <summary>Writes <c>Tm</c>: sets the text matrix, and the start of the next line, to [a b c d e f].</summary>
    public ContentStreamBuilder SetTextMatrix(double a, double b, double c, double d, double e, double f)
    {
        ThrowIfNotInText("SetTextMatrix (Tm)");
        Operands(a, b, c, d);
        Operands(e, f);
        return Operator("Tm"u8);
    }

    /// <summary>Writes <c>Tj</c>: shows <paramref name="text"/> in the current font, encoded with WinAnsi.</summary>
    /// <exception cref="ArgumentException">A character has no WinAnsi code. Nothing is written then.</exception>
    /// <exception cref="InvalidOperationException">No text object is open, or no font is set.</exception>
    public ContentStreamBuilder ShowText(string text)
    {
        ThrowIfNotInText("ShowText (Tj)");
        if (_state.Font is null)
        {
            throw new InvalidOperationException("ShowText (Tj) needs a font. Call SetFont first.");
        }

        // Encoded before anything is written, so an unencodable character leaves the stream unchanged.
        var bytes = WinAnsiEncoding.Encode(text);
        PdfString.WriteLiteral(_output, bytes);
        _output.WriteByte((byte)' ');
        return Operator("Tj"u8);
    }

    /// <summary>Returns the content stream's bytes. The builder can't be used afterwards.</summary>
    /// <exception cref="InvalidOperationException">A path or a text object is still open, or a <c>q</c> has no matching
    /// <c>Q</c>.</exception>
    public byte[] Finish()
    {
        ThrowIfFinished();
        if (_pathOpen)
        {
            throw new InvalidOperationException("A path is still open. Paint it with Fill, Stroke or FillAndStroke first.");
        }

        if (_inText)
        {
            throw new InvalidOperationException("A text object is still open. End it with EndText first.");
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
        ThrowIfInText("Painting a path");
        if (!_pathOpen)
        {
            throw new InvalidOperationException("There is no path to paint. Build one with Rectangle first.");
        }

        _pathOpen = false;
        return Operator(paintOperator);
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

    // Inside BT…ET, PDF allows no path operators and none of q, Q and cm.
    private void ThrowIfInText(string operation)
    {
        ThrowIfFinished();
        if (_inText)
        {
            throw new InvalidOperationException($"{operation} isn't allowed inside a text object. Call EndText first.");
        }
    }

    private void ThrowIfPathOpenOrInText(string operation)
    {
        ThrowIfPathOpen();
        ThrowIfInText(operation);
    }

    private void ThrowIfNotInText(string operation)
    {
        ThrowIfFinished();
        if (!_inText)
        {
            throw new InvalidOperationException($"{operation} is only allowed inside a text object. Call BeginText first.");
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
    private record struct GraphicsState(RgbColor? Fill, RgbColor? Stroke, double? LineWidth, StandardFont? Font, double? FontSize);
}
