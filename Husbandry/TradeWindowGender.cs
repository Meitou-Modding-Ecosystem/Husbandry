using System.Reflection;
using HarmonyLib;
using Meitou.Localisation;
using Meitou.Simulation;
using Meitou.Simulation.Dialogue;

namespace Husbandry;

/// <summary>
/// The animal buying window's "Gender:" line shows the mod's sex of each animal (<see cref="Sexes"/>), so the Beastmaster's females read Female.
/// The game takes the line from the look's <c>sex female</c>, which no animal has (docs/husbandry.md "Breeding"). A postfix on the window's
/// opening (<c>CharacterTrading.OpenCharacterTrade</c>, an engine class the mod cannot name) rewrites the line in the rows it just made; display
/// only.
/// </summary>
[HarmonyPatch]
static class TradeWindowGender
{
    /// <summary>
    /// The sexes of the latest world (set by its system).
    /// </summary>
    public static Sexes? Sexes { get; set; }

    /// <summary>
    /// The engine's character trading class.
    /// </summary>
    static readonly Type? TradingType = AccessTools.TypeByName("Meitou.Simulation.Dialogue.CharacterTrading");

    /// <summary>
    /// Its open window (a private record holding the kind and rows).
    /// </summary>
    static readonly FieldInfo? WindowField = TradingType is null ? null : AccessTools.Field(TradingType, "_characterTrade");

    /// <summary>
    /// The method patched; null in a game without it.
    /// </summary>
    static readonly MethodInfo? Opening = TradingType is null ? null : AccessTools.Method(TradingType, "OpenCharacterTrade");

    /// <summary>
    /// Whether to patch: only a game that has the method (a missing one leaves the line as the game shows it).
    /// </summary>
    static bool Prepare() => Opening is not null && WindowField is not null;

    /// <summary>
    /// The method patched.
    /// </summary>
    static MethodBase TargetMethod() => Opening!;

    /// <summary>
    /// Rewrites the gender line of each animal row of a window just opened for buying animals.
    /// </summary>
    static void Postfix(object __instance, World world)
    {
        if (Sexes is not { } sexes || WindowField?.GetValue(__instance) is not { } window) return;
        if (Traverse.Create(window).Property("Kind").GetValue<CharacterTradeKind>() != CharacterTradeKind.AnimalBuying) return;

        var rows = Traverse.Create(window).Property("Rows").GetValue<IReadOnlyList<CharacterTradeRow>>();
        string label = Loc.Tr("Gender:");
        foreach (var row in rows)
        {
            if (row.Panel1 is not ValueTuple<string, string>[] panel || !world.Characters.TryResolveNext(row.Character, out int slot)) continue;
            if (world.Characters.Cold(slot) is not { } cold) continue;

            int line = Array.FindIndex(panel, p => p.Item1 == label);
            if (line >= 0) panel[line] = (label, Loc.Tr(sexes.Female(cold) ? "Female" : "Male"));
        }
    }
}
