using System.Numerics;
using Meitou.Data;
using Meitou.Data.Fcs;
using Meitou.Data.Gameplay.Economy;

namespace Husbandry.Capsules;

/// <summary>
/// The Capsule Bench (docs/husbandry.md "Beast Capsules", "Data file"): a crafting bench that makes Beast Capsules from their ingredients. Its
/// functionality is the game's basic medical crafting with only the capsule in its list; the building wears its own look from
/// Structures/capsule-bench.glb when the import made it, else the Basic Medical Workbench's, and keeps that bench's build cost and power draw.
/// </summary>
public static class BenchData
{
    /// <summary>
    /// The record ids: the BUILDING and its BUILDING_FUNCTIONALITY.
    /// </summary>
    public const string Bench = "36-" + HusbandryData.FileName, Work = "37-" + HusbandryData.FileName;

    /// <summary>
    /// The building the structure import makes from Structures/capsule-bench.glb (block 3 in Structures/ids.json).
    /// </summary>
    public const string Look = "900300-" + HusbandryData.FileName;

    /// <summary>
    /// The game's bench it stands in for, and that bench's functionality: string id, then name.
    /// </summary>
    const string StandInId = "1966-gamedata.base", StandInName = "Basic Medical Workbench", WorkModelId = "2268-gamedata.base",
        WorkModelName = "medical crafting basic";

    /// <summary>
    /// The capsule's ingredients, in hundredths of an item: two Iron Plates and a Leather (string id, then name).
    /// </summary>
    static readonly (string Id, string Name, int Hundredths)[] Ingredients =
        [("42159-gamedata.base", "Iron Plates", 200), ("2292-gamedata.base", "Leather", 100)];

    /// <summary>
    /// Game hours to make one capsule (an item craft's <c>production time</c>).
    /// </summary>
    const int CraftHours = 4;

    /// <summary>
    /// How near its use node the operator works (the functionality's <c>use range</c>), so the operator walks to the front of the bench.
    /// </summary>
    const float OperatorReach = 10;

    /// <summary>
    /// The use node on its own look: 6 units (room for a body) in front of the table's front edge at local +Z 4.4 (the toolboard is at -Z),
    /// turned -90 degrees about Y as the game's front nodes are, so its marker points back at the bench.
    /// </summary>
    static readonly Vector3 NodeAt = new(0, 0, 10.4f);

    /// <summary>
    /// The game's use node record.
    /// </summary>
    const string UseNode = "1183-gamedata.base";

    /// <summary>
    /// The build menu text.
    /// </summary>
    const string Text = "A workbench for making Beast Capsules from two Iron Plates and a Leather each. Give one of your people its operate job"
        + " and keep it stocked.";

    /// <summary>
    /// The functionality and the bench. Throws when the game lacks the stand-in or its functionality.
    /// </summary>
    public static IEnumerable<FcsRecord> Build(GameDatabase game) => [WorkRecord(game), BenchRecord(game)];

    /// <summary>
    /// Gives the capsule its recipe, priced so it still sells for <see cref="CapsuleData.Price"/>: the game prices a crafted item at its
    /// ingredients' worth times 1 + its <c>profitability</c>, ignoring its own <c>value</c>.
    /// </summary>
    public static void AddRecipe(GameDatabase game, FcsRecord capsule)
    {
        var prices = PriceConstants.FromDatabase(game);
        float worth = 0;
        foreach (var (id, name, hundredths) in Ingredients)
            worth += ItemValue.Of(game, Model(game, FcsRecordType.Item, id, name), 50, true, prices) * hundredths / 100f;
        if (worth <= 0) return;

        capsule.References["ingredients"] = [.. Ingredients.Select(i => new FcsReference(Model(game, FcsRecordType.Item, i.Id, i.Name).StringId,
            i.Hundredths, 0, 0))];
        capsule.Ints["production time"] = CraftHours;
        capsule.Floats["profitability"] = Math.Max(CapsuleData.Price / worth - 1, 0);
    }

    /// <summary>
    /// The functionality: the model's, crafting only Beast Capsules (the model's Fabrics input is dropped: a craft draws its item's ingredients).
    /// </summary>
    static FcsRecord WorkRecord(GameDatabase game)
    {
        var model = Model(game, FcsRecordType.BuildingFunctionality, WorkModelId, WorkModelName);
        var record = HusbandryData.New(Work, FcsRecordType.BuildingFunctionality, "Capsule Bench");
        HusbandryData.CopyFields(model, record);
        foreach (string list in model.ReferenceLists.Where(l => l != "consumes"))
            record.References[list] = [.. model.GetReferences(list).Select(HusbandryData.Link)];

        record.References["item crafts"] = [new FcsReference(HusbandryData.Capsule, 5, 0, 0)];
        record.Floats["use range"] = OperatorReach;
        return record;
    }

    /// <summary>
    /// The bench: its own look when the data has it (with the stand-in's cost and power and a front use node), else the stand-in's.
    /// </summary>
    static FcsRecord BenchRecord(GameDatabase game)
    {
        var standIn = Model(game, FcsRecordType.Building, StandInId, StandInName);
        var model = game.Find(Look) ?? standIn;

        var record = HusbandryData.New(Bench, FcsRecordType.Building, "Capsule Bench");
        HusbandryData.CopyFields(model, record);
        foreach (string list in model.ReferenceLists)
            record.References[list] = [.. model.GetReferences(list).Select(HusbandryData.Link)];

        if (model != standIn) WearOwnLook(standIn, record);

        record.References["functionality"] = [new FcsReference(Work, 0, 0, 0)];
        record.References["upgrades to"] = [];
        record.Strings["building category"] = "CRAFTING";
        record.Strings["building group"] = "";
        record.Strings["Description"] = HusbandryText.Get(Text);
        record.Bools["creates player town"] = true;
        return record;
    }

    /// <summary>
    /// On its own look the bench keeps the stand-in's build cost and power draw, and its operator stands at a use node in front of it.
    /// </summary>
    static void WearOwnLook(GameRecord standIn, FcsRecord record)
    {
        record.References["construction"] = [.. standIn.GetReferences("construction").Select(HusbandryData.Link)];
        if (standIn.Ints.TryGetValue("power output", out int power)) record.Ints["power output"] = power;
        else record.Ints.Remove("power output");

        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -MathF.PI / 2);
        record.Instances.Add(new FcsInstance { Id = $"usenode{record.Instances.Count + 1}", Target = UseNode, Position = NodeAt, Rotation = turn });
    }

    /// <summary>
    /// The game's record of a type by string id, else by name (the lowest id when several share it). Throws when it has neither.
    /// </summary>
    static GameRecord Model(GameDatabase game, FcsRecordType type, string id, string name) =>
        game.Find(id) is { } byId && byId.Type == type
            ? byId
            : game.OfType(type)
                .Where(r => r.Name == name)
                .OrderBy(r => r.StringId, StringComparer.Ordinal)
                .FirstOrDefault()
            ?? throw new InvalidOperationException($"The game data has no {type} '{name}' for the Capsule Bench.");
}
