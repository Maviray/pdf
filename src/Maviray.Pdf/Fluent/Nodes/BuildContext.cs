namespace Maviray.Pdf.Fluent.Nodes;

/// <summary>What a node needs to build its element: the text style it inherits, and the registry its fonts come from.</summary>
internal sealed record BuildContext(TextStyle Inherited, FontRegistry Fonts);
