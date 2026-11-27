using System.Collections.Concurrent;
using System.Globalization;
using Meitou.Simulation;

namespace Husbandry.Capsules;

/// <summary>
/// A unique quality an animal can carry beyond its race's stats (docs/husbandry.md "Beast traits"): its id, the label its tooltip line shows
/// and how its value reads. A living animal's traits are its character tag <see cref="BeastTraits.TagMod"/>, so any mod sets or reads them with
/// <see cref="BeastTraits.Set"/> and <see cref="BeastTraits.Get"/>; in a capsule they are <see cref="StoredBeast.Traits"/>, saved with it, shown
/// in its tooltip and put back on the body a summon makes. Nothing in Husbandry gives one yet: rare catches, breeding and specialised training will.
/// </summary>
public sealed record BeastTrait(string Id, string Label, string Format = "+0.#;-0.#")
{
    /// <summary>
    /// The value as its tooltip shows it.
    /// </summary>
    public string Show(float value) => value.ToString(Format, CultureInfo.InvariantCulture);
}

/// <summary>
/// The traits known to the game, by id: what <see cref="CapsuleTooltip"/> names a stored trait by, and each animal's traits. A trait whose id is
/// not registered (its mod is no longer loaded) is kept and saved but shown by its id.
/// </summary>
public static class BeastTraits
{
    #region Registered traits

    /// <summary>
    /// What a trait id may not hold: what the traits tag splits on.
    /// </summary>
    static readonly char[] Separators = ['=', ';'];

    /// <summary>
    /// The registered traits by id.
    /// </summary>
    static readonly ConcurrentDictionary<string, BeastTrait> Known = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers a trait; the id must be new and hold no '=' or ';' (<see cref="Encode"/>).
    /// </summary>
    public static BeastTrait Register(BeastTrait trait)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trait.Id);
        if (trait.Id.IndexOfAny(Separators) >= 0) throw new ArgumentException($"A beast trait id may not hold '=' or ';': '{trait.Id}'.");
        if (!Known.TryAdd(trait.Id, trait)) throw new InvalidOperationException($"The beast trait '{trait.Id}' is already registered.");
        return trait;
    }

    /// <summary>
    /// The trait of an id, or null.
    /// </summary>
    public static BeastTrait? Find(string id) => Known.GetValueOrDefault(id);

    /// <summary>
    /// A stored trait as its tooltip shows it: "Label +value", or "id value" when it is not registered.
    /// </summary>
    public static string Show(string id, float value) =>
        Find(id) is { } trait
            ? $"{HusbandryText.Get(trait.Label)} {trait.Show(value)}"
            : $"{id} {value.ToString("0.#", CultureInfo.InvariantCulture)}";

    #endregion

    #region An animal's traits

    /// <summary>
    /// The character tag (<see cref="CharacterTags"/>, its mod key) that holds a living animal's traits: "id=value;id=value", ids in ordinal order.
    /// </summary>
    public const string TagMod = "husbandry.traits";

    /// <summary>
    /// The animal's traits; none when it has none or is not in the world. Any phase.
    /// </summary>
    public static IReadOnlyList<(string Trait, float Value)> Of(World world, CharacterId animal) =>
        Decode(world.CharacterTags.Get(animal, TagMod));

    /// <summary>
    /// One trait's value on the animal, or null when it does not have it. Any phase.
    /// </summary>
    public static float? Get(World world, CharacterId animal, string trait) =>
        Of(world, animal).Where(t => t.Trait == trait).Select(t => (float?)t.Value).FirstOrDefault();

    /// <summary>
    /// Gives the animal a trait at <paramref name="value"/> (null takes it away), keeping its other traits; false when it is not in the world.
    /// The trait need not be registered. Serial phases only, as <see cref="CharacterTags.Set"/>; Beast Capsules carry it from then on.
    /// </summary>
    public static bool Set(World world, CharacterId animal, string trait, float? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trait);
        if (trait.IndexOfAny(Separators) >= 0) throw new ArgumentException($"A beast trait id may not hold '=' or ';': '{trait}'.");

        var traits = Of(world, animal).Where(t => t.Trait != trait).ToList();
        if (value is { } v) traits.Add((trait, v));
        return world.CharacterTags.Set(animal, TagMod, Encode(traits));
    }

    /// <summary>
    /// Traits as the traits tag holds them ("" for none: the tag is then removed).
    /// </summary>
    public static string Encode(IEnumerable<(string Trait, float Value)> traits) =>
        string.Join(';', traits
            .OrderBy(t => t.Trait, StringComparer.Ordinal)
            .Select(t => t.Trait + "=" + t.Value.ToString("R", CultureInfo.InvariantCulture)));

    /// <summary>
    /// The traits a traits tag holds; an entry that does not read is skipped.
    /// </summary>
    public static IReadOnlyList<(string Trait, float Value)> Decode(string? tag)
    {
        if (string.IsNullOrEmpty(tag)) return [];

        var traits = new List<(string, float)>();
        foreach (string entry in tag.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int at = entry.IndexOf('=');
            if (at <= 0) continue;
            if (!float.TryParse(entry[(at + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out float value)) continue;

            traits.Add((entry[..at], value));
        }

        return traits;
    }

    #endregion
}
