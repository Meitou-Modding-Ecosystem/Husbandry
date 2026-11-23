using System.Globalization;
using Meitou.Modding;

namespace Husbandry;

/// <summary>
/// The debug row "Husbandry: animal trader at settlement" (docs/husbandry.md "Debug rows"): its field names the player settlement, by name or by
/// its number in the list the row prints (1 the first founded); empty picks the only one. A name that is missing or shared, or a bad number,
/// answers with the list. Runs on the host's thread: it reads the settlements the last check published and queues the command.
/// </summary>
public static class SettlementTraderRow
{
    /// <summary>
    /// The row's label.
    /// </summary>
    public const string Label = "Husbandry: animal trader at settlement";

    /// <summary>
    /// Picks the settlement and queues its Beastmaster; the answer is the status line.
    /// </summary>
    public static string Run(DebugCommandContext d, OutpostTraders? outposts)
    {
        if (outposts is null) return "No world yet.";

        var towns = outposts.Towns;
        if (towns.Count == 0) return "You have no settlement yet (found one, or wait a second after loading).";

        if (Pick(towns, d.Field.Trim(), out string problem) is not { } town) return $"{problem} {List(towns)}";

        if (outposts.Has(town.Number)) return $"{town.Name} already has a Beastmaster.";

        d.Enqueue(new SettlementTraderCommand(town.Number, town.Name) { Tick = d.Tick });
        return $"A Beastmaster is coming to {town.Name}: he and his pen appear on open ground near its centre while you are there.";
    }

    /// <summary>
    /// The settlement the field names; null, with the reason, when it names none or more than one.
    /// </summary>
    static (int Number, string Name)? Pick(IReadOnlyList<(int Number, string Name)> towns, string field, out string problem)
    {
        problem = "";
        if (field.Length == 0 && towns.Count == 1) return towns[0];

        if (field.Length == 0)
        {
            problem = "Type a settlement's name or number in the field.";
            return null;
        }

        var named = towns.Where(t => string.Equals(t.Name, field, StringComparison.OrdinalIgnoreCase)).ToList();
        if (named.Count == 1) return named[0];

        bool isNumber = int.TryParse(field, NumberStyles.None, CultureInfo.InvariantCulture, out int index);
        if (named.Count == 0 && isNumber && index >= 1 && index <= towns.Count) return towns[index - 1];

        problem = named.Count > 1 ? $"{named.Count} settlements are named \"{field}\": type its number instead." : $"No settlement is \"{field}\".";
        return null;
    }

    /// <summary>
    /// The settlements with the numbers to type.
    /// </summary>
    static string List(IReadOnlyList<(int Number, string Name)> towns) =>
        "Your settlements (type the name or the number): " + string.Join(", ", towns.Select((t, k) => $"{k + 1} = {t.Name}")) + ".";
}
