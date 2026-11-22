using System.Reflection;
using HarmonyLib;
using Meitou.Localisation;
using Meitou.Simulation;

namespace Husbandry;

/// <summary>
/// The character panel's Age line of an animal also names its sex (<see cref="Sexes"/>): "Adult (female)", with a "Sex:" row in its tooltip. The
/// game has no sex for animals and so no line for it. A postfix on the panel's lines (<c>HudTips.Lines</c>, an engine class the mod cannot name)
/// rewrites the Age line in place, so the panel keeps its number of lines; display only.
/// </summary>
[HarmonyPatch]
static class StatsPanelSex
{
    /// <summary>
    /// The method patched; null in a game without it.
    /// </summary>
    static readonly MethodInfo? Lines = AccessTools.TypeByName("Meitou.Simulation.Bodies.HudTips") is { } type
        ? AccessTools.Method(type, "Lines")
        : null;

    /// <summary>
    /// The label of the line rewritten, as the engine writes it (the panel translates it).
    /// </summary>
    const string AgeLabel = "Age:";

    /// <summary>
    /// Whether to patch: only a game that has the method (a missing one leaves the panel as the game shows it).
    /// </summary>
    static bool Prepare() => Lines is not null;

    /// <summary>
    /// The method patched.
    /// </summary>
    static MethodBase TargetMethod() => Lines!;

#pragma warning disable IDE1006 // Harmony passes the patched method's result only to a parameter named __result.
    /// <summary>
    /// Adds the animal's sex to its Age line.
    /// </summary>
    static void Postfix(CharacterCold cold, List<HudStatLine> __result)
#pragma warning restore IDE1006
    {
        if (cold.Animal is null || TradeWindowGender.Sexes is not { } sexes) return;

        int line = __result.FindIndex(l => l.Label == AgeLabel);
        if (line < 0) return;

        bool female;
        try
        {
            female = sexes.Female(cold);
        }
        catch (InvalidOperationException)
        {
            // The panel is built while the world may be changing the table; the next frame shows it.
            return;
        }

        var age = __result[line];
        string sex = Loc.Tr(female ? "female" : "male");
        __result[line] = age with
        {
            Text = age.Text is { } word ? $"{word} ({sex})" : sex,
            Tip = [.. age.Tip, (Loc.Tr("Sex:"), Loc.Tr(female ? "Female" : "Male"))],
        };
    }
}
