using Maviray.Pdf.Elements;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Fluent.Nodes;

/// <summary>The library's <see cref="IContainer"/>: a place for one node. An empty one builds an empty Column.</summary>
internal sealed class ContainerSlot : IContainer
{
    public Node? Child { get; private set; }

    /// <summary>The slot behind <paramref name="container"/>, which must be the library's own.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="container"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="container"/> wasn't made by the library.</exception>
    public static ContainerSlot Of(IContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);
        return container as ContainerSlot
            ?? throw new ArgumentException("Only the containers the library's builders give can take content.", nameof(container));
    }

    /// <exception cref="InvalidOperationException">The slot already has its child.</exception>
    public void Set(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (Child is not null)
        {
            throw new InvalidOperationException("This container already has its content: a container takes one child.");
        }

        Child = node;
    }

    public Element Build(BuildContext context) => Child?.Build(context) ?? new Column([], 0);
}
