using Maviray.Pdf.Elements;
using Maviray.Pdf.Fluent.Nodes;

namespace Maviray.Pdf;

/// <summary>
/// Builds a row: items side by side. Fixed items get their width, auto items their content's own, and flexible items
/// share what is left by weight.
/// </summary>
public sealed class RowBuilder
{
    private readonly RowNode _node;

    internal RowBuilder(RowNode node) => _node = node;

    /// <summary>The space between two items.</summary>
    /// <param name="spacing">The spacing in points, zero or more and finite.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="spacing"/> is negative or not finite.</exception>
    public RowBuilder Spacing(double spacing) => Spacing(spacing, Unit.Point);

    /// <summary>The space between two items.</summary>
    /// <param name="spacing">The spacing, zero or more and finite.</param>
    /// <param name="unit">The spacing's unit.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="spacing"/> is negative or not finite, or <paramref name="unit"/> isn't defined.</exception>
    public RowBuilder Spacing(double spacing, Unit unit)
    {
        _node.Spacing = Units.ToPoints(spacing, unit, nameof(spacing));
        return this;
    }

    /// <summary>An item of a fixed width.</summary>
    /// <param name="width">The width in points, zero or more and finite.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> is negative or not finite.</exception>
    public IContainer Fixed(double width) => Fixed(width, Unit.Point);

    /// <summary>An item of a fixed width.</summary>
    /// <param name="width">The width, zero or more and finite.</param>
    /// <param name="unit">The width's unit.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> is negative or not finite, or <paramref name="unit"/> isn't defined.</exception>
    public IContainer Fixed(double width, Unit unit) => Add(RowItemKind.Fixed, Units.ToPoints(width, unit, nameof(width)));

    /// <summary>An item that shares the width left after the fixed and auto items, by weight.</summary>
    /// <param name="weight">Its share, positive and finite.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="weight"/> isn't positive and finite.</exception>
    public IContainer Flex(double weight = 1)
    {
        if (!(double.IsFinite(weight) && weight > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(weight), weight, "A weight must be positive and finite.");
        }

        return Add(RowItemKind.Flex, weight);
    }

    /// <summary>An item as wide as its content.</summary>
    public IContainer Auto() => Add(RowItemKind.Auto, 0);

    private ContainerSlot Add(RowItemKind kind, double value)
    {
        var slot = new ContainerSlot();
        _node.Items.Add((kind, value, slot));
        return slot;
    }
}
