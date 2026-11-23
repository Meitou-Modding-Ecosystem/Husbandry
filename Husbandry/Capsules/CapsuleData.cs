using Meitou.Data;
using Meitou.Data.Fcs;

namespace Husbandry.Capsules;

/// <summary>
/// The Beast Capsule's record (docs/husbandry.md "Beast Capsules", "Data file"): an ITEM copied from a small game item (its look, size, weight)
/// until the capsule's own model exists, with its own name, text and price.
/// </summary>
public static class CapsuleData
{
    /// <summary>
    /// The game items it is copied from, tried in order by name (the first the game has wins).
    /// </summary>
    static readonly string[] Models = ["Fertiliser", "Leather", "Hemp"];

    /// <summary>
    /// Its price, in cats.
    /// </summary>
    public const int Price = 500;

    /// <summary>
    /// Its text, shown as the tooltip hint.
    /// </summary>
    const string Text = "A lacquered capsule bound with cord. Right-click it in your inventory while standing next to a knocked-out beast, or one"
        + " of your own animals, to seal it inside. Right-click a full capsule to call the beast out at your side.";

    /// <summary>
    /// The capsule. Throws when the game has none of its models.
    /// </summary>
    public static FcsRecord Build(GameDatabase game)
    {
        var model = Models
            .Select(name => Model(game, name))
            .FirstOrDefault(m => m is not null)
            ?? throw new InvalidOperationException($"The game data has none of the ITEMs {string.Join(", ", Models)} for the Beast Capsule.");

        var record = HusbandryData.New(HusbandryData.Capsule, FcsRecordType.Item, "Beast Capsule");
        HusbandryData.CopyFields(model, record);
        foreach (string list in model.ReferenceLists) record.References[list] = [.. model.GetReferences(list).Select(HusbandryData.Link)];
        record.Strings["description"] = Text;
        record.Ints["value"] = Price;
        return record;
    }

    /// <summary>
    /// The game's item of a name (the lowest id when several share it), or null.
    /// </summary>
    static GameRecord? Model(GameDatabase game, string name) =>
        game.OfType(FcsRecordType.Item)
            .Where(r => r.Name == name)
            .OrderBy(r => r.StringId, StringComparer.Ordinal)
            .FirstOrDefault();
}
