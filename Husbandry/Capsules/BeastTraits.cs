using System.Collections.Concurrent;
using System.Globalization;

namespace Husbandry.Capsules;

/// <summary>
/// A unique quality a capsule's beast can carry beyond its race's stats (docs/husbandry.md "Beast traits"): its id, the label its tooltip line shows
/// and how its value reads. Traits live on <see cref="StoredBeast.Traits"/>, so they stay with the capsule while the beast is out, are saved with
/// it and show in its tooltip. Nothing gives one yet: rare catches, breeding and specialised training will add them through <see cref="Register"/>.
/// </summary>
public sealed record BeastTrait(string Id, string Label, string Format = "+0.#;-0.#")
{
    /// <summary>
    /// The value as its tooltip shows it.
    /// </summary>
    public string Show(float value) => value.ToString(Format, CultureInfo.InvariantCulture);
}

/// <summary>
/// The traits known to the game, by id: what <see cref="CapsuleTooltip"/> names a stored trait by. A trait whose id is not registered (its mod
/// is no longer loaded) is kept and saved but shown by its id.
/// </summary>
public static class BeastTraits
{
    /// <summary>
    /// The registered traits by id.
    /// </summary>
    static readonly ConcurrentDictionary<string, BeastTrait> Known = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers a trait; the id must be new.
    /// </summary>
    public static BeastTrait Register(BeastTrait trait)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trait.Id);
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
}
