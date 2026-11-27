using Meitou.Data;
using Meitou.Data.Fcs;

namespace Husbandry.Capsules;

/// <summary>
/// The Beast Capsules' records (docs/husbandry.md "Beast Capsules", "Data file"): the empty capsule, an ITEM copied from a small game item (its
/// weight and other fields), wearing its own model from ItemModels/beast-capsule.glb when the import made it, with its own name, text, price, a
/// one-cell footprint, its own inventory icon and stacks of <see cref="EmptyStack"/>; and the filled capsule, a copy of it that never stacks, wears
/// a red-outlined icon and is never crafted.
/// </summary>
public static class CapsuleData
{
    /// <summary>
    /// The game items it is copied from, tried in order by name (the first the game has wins).
    /// </summary>
    static readonly string[] Models = ["Fertiliser", "Leather", "Hemp"];

    /// <summary>
    /// The mesh and MATERIAL_SPECS_CLOTHING the structure import makes from ItemModels/beast-capsule.glb: block 100 x n from 950000, n from
    /// ItemModels/ids.json; the mesh is named after the mod and the model.
    /// </summary>
    public const string Mesh = "husbandry_beast_capsule.mesh", Material = "950000-" + HusbandryData.FileName;

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
    /// The filled capsule's inventory icon: the empty one's picture with a bright red outline (tools/make-icons.py).
    /// </summary>
    public const string FilledIcon = @".\mods\Husbandry\items\icons\husbandry-beastcapsule-filled.png";

    /// <summary>
    /// How many empty capsules share an inventory stack (<c>stackable</c>).
    /// </summary>
    public const int EmptyStack = 5;

    /// <summary>
    /// Its text, shown as the tooltip hint.
    /// </summary>
    const string Text = "A lacquered capsule bound with cord. Right-click a knocked-out beast and choose Throw Beast Capsule, or right-click the"
        + " capsule in your inventory while standing next to the beast, to seal it inside. The filled capsule drops where the beast lay.";

    /// <summary>
    /// The filled capsule's text, shown as the tooltip hint under the beast's lines.
    /// </summary>
    const string FilledText = "A Beast Capsule holding a beast, bound to it for good. Right-click it in your inventory to call the beast out at"
        + " your side, and again to call it back while it is within 30 m.";

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
        record.Strings["description"] = HusbandryText.Get(Text);
        record.Ints["value"] = Price;
        record.Ints["inventory footprint width"] = 1;
        record.Ints["inventory footprint height"] = 1;
        record.Ints["stackable"] = EmptyStack;
        record.Filenames["icon"] = Icon;
        record.Bools["auto icon image"] = false;
        if (game.Find(Material) is not null) WearOwnModel(record);
        return record;
    }

    /// <summary>
    /// The filled capsule, made from the empty one (<see cref="Build"/>, before its recipe is added): its own id, name and text, one to a stack,
    /// the red-outlined icon and no recipe.
    /// </summary>
    public static FcsRecord Filled(FcsRecord empty)
    {
        var record = HusbandryData.New(HusbandryData.FilledCapsule, FcsRecordType.Item, "Filled Beast Capsule");
        foreach (var (key, value) in empty.Bools) record.Bools[key] = value;
        foreach (var (key, value) in empty.Floats) record.Floats[key] = value;
        foreach (var (key, value) in empty.Ints) record.Ints[key] = value;
        foreach (var (key, value) in empty.Strings) record.Strings[key] = value;
        foreach (var (key, value) in empty.Filenames) record.Filenames[key] = value;
        foreach (var (key, value) in empty.Vector3S) record.Vector3S[key] = value;
        foreach (var (key, value) in empty.Vector4S) record.Vector4S[key] = value;
        foreach (var (list, links) in empty.References) record.References[list] = [.. links];

        record.Strings["description"] = HusbandryText.Get(FilledText);
        record.Ints["stackable"] = 1;
        record.Filenames["icon"] = FilledIcon;
        record.References.Remove("ingredients");
        record.Ints.Remove("production time");
        record.Floats.Remove("profitability");
        return record;
    }

    /// <summary>
    /// Points the capsule at its own mesh and material, in place of its stand-in's look (an item's own texture map would win over its material).
    /// </summary>
    static void WearOwnModel(FcsRecord record)
    {
        record.Filenames["mesh"] = Mesh;
        record.Filenames["ground mesh"] = Mesh;
        record.Filenames["texture map"] = "";
        record.Filenames["normal map"] = "";
        record.References["material"] = [new FcsReference(Material, 0, 0, 0)];
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
