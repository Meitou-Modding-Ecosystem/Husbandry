using System.Globalization;
using Meitou.Data.Gameplay.Bodies;
using Meitou.Game.Ui;

namespace Husbandry.Capsules;

/// <summary>
/// A filled capsule's tooltip lines (docs/husbandry.md "Beast Capsules"): the beast it holds and its kind, its sex and age, whether it is inside or
/// out, its main stats, its trained skills and its traits. The engine keeps at most <see cref="MaxLines"/> lines of <see cref="MaxText"/>
/// characters from a mod, so skills and traits share one line each.
/// </summary>
public static class CapsuleTooltip
{
    /// <summary>
    /// The most lines the engine keeps of a mod's item tooltip lines.
    /// </summary>
    public const int MaxLines = 8;

    /// <summary>
    /// The most characters the engine keeps of a line's label or value.
    /// </summary>
    public const int MaxText = 80;

    /// <summary>
    /// The age from which a beast reads as an adult (Husbandry's adult age).
    /// </summary>
    const float AdultAge = 0.6f;

    /// <summary>
    /// The stats shown, as label and stat, on two lines.
    /// </summary>
    static readonly (string Label, StatsEnumerated Stat)[][] StatLines =
    [
        [("Str", StatsEnumerated.Strength), ("Tough", StatsEnumerated.Toughness), ("Dex", StatsEnumerated.Dexterity)],
        [("Atk", StatsEnumerated.MeleeAttack), ("Def", StatsEnumerated.MeleeDefence), ("Ath", StatsEnumerated.Athletics)],
    ];

    /// <summary>
    /// The lines for a capsule's beast; a single line when the capsule's beast is unknown (its tag names none).
    /// </summary>
    public static IReadOnlyList<UiItemTooltipLine> Lines(BeastView? view)
    {
        if (view is null) return [new UiItemTooltipLine(HusbandryText.Get("Holds no beast"))];

        var beast = view.Beast;
        var lines = new List<UiItemTooltipLine>
        {
            new(HusbandryText.Get("Contains"), Clip(beast.Kind.Length > 0 ? $"{beast.Name} ({beast.Kind})" : beast.Name)),
            new(HusbandryText.Get(beast.Female ? "Female" : "Male"), HusbandryText.Get(beast.Age >= AdultAge ? "Adult" : "Young")),
            new(HusbandryText.Get("Status"), HusbandryText.Get(view.Out ? "Out of the capsule" : "In the capsule")),
        };

        foreach (var row in StatLines) lines.Add(new(HusbandryText.Get("Stats"), Stats(beast, row)));
        if (Skills(beast) is { Length: > 0 } skills) lines.Add(new(HusbandryText.Get("Skills"), Clip(skills)));
        if (Traits(beast) is { Length: > 0 } traits) lines.Add(new(HusbandryText.Get("Traits"), Clip(traits)));
        return lines;
    }

    /// <summary>
    /// A row of stats: "Str 45  Tough 30  Dex 20", each rounded down.
    /// </summary>
    static string Stats(StoredBeast beast, (string Label, StatsEnumerated Stat)[] row) =>
        string.Join("  ", row.Select(s => $"{s.Label} {((int)beast.Stat(s.Stat)).ToString(CultureInfo.InvariantCulture)}"));

    /// <summary>
    /// The trained mod skills (level 1 or more): "Guard 12, Hunt 4".
    /// </summary>
    static string Skills(StoredBeast beast) =>
        string.Join(", ", beast.Skills
            .Where(s => s.Level >= 1)
            .Select(s => $"{SkillName(s.Skill)} {((int)s.Level).ToString(CultureInfo.InvariantCulture)}"));

    /// <summary>
    /// A skill's short name: its id after the last dot, capitalised ("husbandry.guard" is "Guard").
    /// </summary>
    static string SkillName(string id)
    {
        string name = id[(id.LastIndexOf('.') + 1)..];
        return name.Length == 0 ? id : char.ToUpperInvariant(name[0]) + name[1..];
    }

    /// <summary>
    /// The traits (<see cref="BeastTraits.Show"/>), comma separated.
    /// </summary>
    static string Traits(StoredBeast beast) => string.Join(", ", beast.Traits.Select(t => BeastTraits.Show(t.Trait, t.Value)));

    /// <summary>
    /// A value cut to what the tooltip keeps.
    /// </summary>
    static string Clip(string text) => text.Length <= MaxText ? text : text[..(MaxText - 1)] + "…";
}
