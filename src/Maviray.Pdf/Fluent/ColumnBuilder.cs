using Maviray.Pdf.Fluent.Nodes;

namespace Maviray.Pdf;

/// <summary>Builds a column: items one below the other, each at the full width.</summary>
public sealed class ColumnBuilder
{
    private readonly ColumnNode _node;

    internal ColumnBuilder(ColumnNode node) => _node = node;

    /// <summary>The space between two items on one page.</summary>
    /// <param name="spacing">The spacing in points, zero or more and finite.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="spacing"/> is negative or not finite.</exception>
    public ColumnBuilder Spacing(double spacing) => Spacing(spacing, Unit.Point);

    /// <summary>The space between two items on one page.</summary>
    /// <param name="spacing">The spacing, zero or more and finite.</param>
    /// <param name="unit">The spacing's unit.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="spacing"/> is negative or not finite, or <paramref name="unit"/> isn't defined.</exception>
    public ColumnBuilder Spacing(double spacing, Unit unit)
    {
        _node.Spacing = Units.ToPoints(spacing, unit, nameof(spacing));
        return this;
    }

    /// <summary>A new item, below the ones before it.</summary>
    public IContainer Item()
    {
        var slot = new ContainerSlot();
        _node.Items.Add(slot);
        return slot;
    }
}
