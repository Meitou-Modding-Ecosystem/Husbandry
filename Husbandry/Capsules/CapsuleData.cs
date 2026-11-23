using Meitou.Data;
using Meitou.Data.Fcs;

namespace Husbandry.Capsules;

/// <summary>
/// The Beast Capsule's record (docs/husbandry.md "Beast Capsules", "Data file"): an ITEM copied from a small game item (its look in the world and
/// its weight; the structure import makes buildings only), with its own name, text, price, a one-cell footprint and its own inventory icon.
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
    /// Its inventory icon, made by tools/make-icons.py from concept-art/beastcapsule-inventory.png; the game takes it by its bare name from its
    /// Icons group, which a mod's items/icons folder joins.
    /// </summary>
    public const string Icon = @".\mods\Husbandry\items\icons\husbandry-beastcapsule.png";

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
        record.Ints["inventory footprint width"] = 1;
        record.Ints["inventory footprint height"] = 1;
        record.Filenames["icon"] = Icon;
        record.Bools["auto icon image"] = false;
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
