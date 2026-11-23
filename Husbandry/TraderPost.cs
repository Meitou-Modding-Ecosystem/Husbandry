using Meitou.Simulation;
using Meitou.Simulation.Population;

namespace Husbandry;

/// <summary>
/// Where a <see cref="Beastmaster"/> trades.
/// </summary>
/// <param name="Key">Names this Beastmaster in his platoons' tags: null for Mourn's (the tags older saves hold), "outpost.N" for one at a player
/// settlement.</param>
/// <param name="Where">Where he trades, for the log and the debug view.</param>
/// <param name="Find">Finds his place (counter, pen, floor, door) in the running world, given the pen radius; null while it cannot.</param>
public sealed record TraderPost(string? Key, string Where, Func<World, PopulationData, float, BarPlace?> Find)
{
    /// <summary>
    /// The Beastmaster of Mourn's bar.
    /// </summary>
    public static TraderPost Mourn { get; } = new(null, "Mourn's bar", MournBar.Find);
}
