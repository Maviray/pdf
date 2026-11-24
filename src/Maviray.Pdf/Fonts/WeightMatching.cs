namespace Maviray.Pdf.Fonts;

/// <summary>
/// The weight matching of CSS Fonts Level 4 (spec L-09). For a desired weight from 400 to 500, the weights from it up to
/// 500 are tried in ascending order, then lighter ones in descending order, then those above 500 in ascending order.
/// Below 400, lighter ones come first, descending, then heavier ones ascending. Above 500, heavier ones first,
/// ascending, then lighter ones descending.
/// </summary>
internal static class WeightMatching
{
    /// <summary>The weight among <paramref name="available"/>, which holds at least one, that a request for
    /// <paramref name="desired"/> gets.</summary>
    public static int Choose(int desired, IReadOnlyCollection<int> available)
    {
        ArgumentNullException.ThrowIfNull(available);
        if (available.Count == 0)
        {
            throw new ArgumentException("There is no weight to choose from.", nameof(available));
        }

        if (available.Contains(desired))
        {
            return desired;
        }

        int? Lighter() => available.Where(weight => weight < desired).Select(weight => (int?)weight).Max();
        int? Heavier(int below = int.MaxValue) => available.Where(weight => weight > desired && weight <= below).Select(weight => (int?)weight).Min();

        return desired switch
        {
            >= 400 and <= 500 => Heavier(500) ?? Lighter() ?? Heavier()!.Value,
            < 400 => Lighter() ?? Heavier()!.Value,
            _ => Heavier() ?? Lighter()!.Value,
        };
    }
}
