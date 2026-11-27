using Meitou.Data;
using Meitou.Data.Fcs;
using Meitou.Data.Gameplay.Construction;

namespace Husbandry;

/// <summary>
/// The training buildings' records (docs/husbandry.md "Training", "Data file"): the Training Post, the Pet Station and the Bone Pile. Each takes
/// its look from the building the structure import made from its own model (Structures/, docs/structures.md) when the loaded data has it, else
/// from a game building (the stand-in), and its build cost from the stand-in either way. The post is a storage of Raw Meat only (the trainer's
/// treats, <see cref="PostWork"/>); the station has no function; the pile is a machine whose only output is Raw Meat (<see cref="PileWork"/>),
/// filled by its hunters. All three are unlocked by the Animal Husbandry
/// research.
/// </summary>
public static class TrainingData
{
    /// <summary>
    /// The game buildings each is copied from, tried in order by name (the first the game has wins).
    /// </summary>
    static readonly string[] PostModels = ["Training Dummy", HusbandryData.ModelName],
        StationModels = ["Bedroll", "Bed Roll", HusbandryData.ModelName],
        PileModels = ["Wooden Barrel", "Storage: General"];

    /// <summary>
    /// The buildings the structure import makes from Structures/training-post.glb, pet-station.glb and bone-pile.glb: block 100 x n from 900000,
    /// n from Structures/ids.json.
    /// </summary>
    public const string PostLook = "900200-" + HusbandryData.FileName, StationLook = "900100-" + HusbandryData.FileName,
        PileLook = "900000-" + HusbandryData.FileName;

    /// <summary>
    /// The pile's BUILDING_FUNCTIONALITY: a refinery (function 4) that makes nothing by itself (<c>production mult</c> 0) and needs nobody at it
    /// (<c>max operators</c> 0, an automatic machine), whose output is <see cref="MeatHeld"/> Raw Meat; the hunters fill it
    /// (<see cref="Training.Hunting"/>) and haulers carry it off, as from any machine's output, never into it.
    /// </summary>
    public const string PileWork = "38-" + HusbandryData.FileName;

    /// <summary>
    /// How many Raw Meat the pile's output holds.
    /// </summary>
    public const int MeatHeld = 20;

    /// <summary>
    /// The post's BUILDING_FUNCTIONALITY: a resource storage (function 2, the one kind that reads <c>limit inventory</c>) that takes only Raw Meat,
    /// the trainer's treats (<see cref="Training.Treats"/>).
    /// </summary>
    public const string PostWork = "41-" + HusbandryData.FileName;

    /// <summary>
    /// How many Raw Meat the post's storage holds: its one cell's stack, through <c>stackable bonus mult</c>.
    /// </summary>
    public const int PostMeat = 20;

    /// <summary>
    /// The BUILDING fields that would give the pile a storage of its own (from the stand-in barrel).
    /// </summary>
    static readonly string[] StorageInts = ["storage size width", "storage size height"];

    /// <summary>
    /// The texts in the build menu.
    /// </summary>
    const string PostText = "Teaches animals skills. Select your animals, right-click the post and choose what to train: Fetch, Guard, Hunt,"
        + " Scout, Pack or Tie up. Then give one of your people Train animals here: an animal learns only while a trainer works the post with it,"
        + " faster as the trainer's Animal Handling grows, and twice as fast with Raw Meat for treats in the post, which the trainer restocks from"
        + " your storage. At level 10 it can do the work.",
        StationText = "A home for a guard animal. Select an animal with Guard 10 or more, right-click the station and choose Guard here: it keeps"
        + " near the station and fights with +30 attack and defence within 2000 units of it.",
        PileText = "Where a hunting animal brings its kills. Select an animal with Hunt 10 or more, right-click the pile and choose Hunt from here:"
        + " it runs out far into the wilds and comes back with raw meat, more often and with more as it learns.";

    /// <summary>
    /// The three buildings and the pile's functionality (none when the game has no Raw Meat). Throws when the game has none of a building's models.
    /// </summary>
    public static IEnumerable<FcsRecord> Build(GameDatabase game)
    {
        var post = Copy(game, HusbandryData.TrainingPost, "Training Post", PostLook, PostModels, PostText);
        var pile = Copy(game, HusbandryData.BonePile, "Bone Pile", PileLook, PileModels, PileText);
        return
        [
            post,
            .. MeatStorage(game, post),
            Copy(game, HusbandryData.PetStation, "Pet Station", StationLook, StationModels, StationText),
            pile,
            .. MeatOutput(game, pile),
        ];
    }

    /// <summary>
    /// Makes the pile a Raw Meat machine with no storage of its own: its functionality (<see cref="PileWork"/>) linked, the stand-in's inventory
    /// dropped. Nothing when the game has no Raw Meat (the pile is then plain scenery and hunts drop their meat by it).
    /// </summary>
    static IEnumerable<FcsRecord> MeatOutput(GameDatabase game, FcsRecord pile)
    {
        pile.Bools["has inventory"] = false;
        foreach (string size in StorageInts) pile.Ints.Remove(size);
        pile.References["limit inventory"] = [];
        if (Meat(game) is not { } meat) yield break;

        var work = HusbandryData.New(PileWork, FcsRecordType.BuildingFunctionality, "Bone Pile");
        work.Ints["function"] = MachineData.RefineryFunction;
        work.Ints["max operators"] = 0;
        work.Floats["production mult"] = 0;
        work.Bools["is production building"] = true;
        work.References["produces"] = [new FcsReference(meat, MeatHeld, 100, 0)];
        pile.References["functionality"] = [new FcsReference(PileWork, 0, 0, 0)];
        yield return work;
    }

    /// <summary>
    /// Makes the post a storage of Raw Meat only (<see cref="PostWork"/>): one cell holding <see cref="PostMeat"/>, filled by the trainer, the
    /// player or haulers. Nothing when the game has no Raw Meat (the post then keeps no treats).
    /// </summary>
    static IEnumerable<FcsRecord> MeatStorage(GameDatabase game, FcsRecord post)
    {
        if (Meat(game) is not { } meat) yield break;

        var work = HusbandryData.New(PostWork, FcsRecordType.BuildingFunctionality, "Training Post");
        work.Ints["function"] = MachineData.ResourceStorageFunction;
        post.References["functionality"] = [new FcsReference(PostWork, 0, 0, 0)];
        post.Bools["has inventory"] = true;
        post.Ints["itemtype limit"] = (int)FcsRecordType.Item;
        post.References["limit inventory"] = [new FcsReference(meat, 0, 0, 0)];
        post.Floats["stackable bonus mult"] = StackMult(game.Find(meat)!.GetInt("stackable", 1));
        yield return work;
    }

    /// <summary>
    /// The <c>stackable bonus mult</c> that makes one stack of an item that stacks <paramref name="stackable"/> hold <see cref="PostMeat"/>.
    /// </summary>
    public static float StackMult(int stackable) => (float)PostMeat / Math.Max(1, stackable);

    /// <summary>
    /// The game's Raw Meat ITEM (the lowest id when several share the name), or null.
    /// </summary>
    static string? Meat(GameDatabase game) =>
        game.OfType(FcsRecordType.Item)
            .Where(r => r.Name == Training.Hunting.MeatName)
            .Select(r => r.StringId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>
    /// The building in the build menu's Husbandry category with its own name and text: a copy of its own look when the data has it, else of the
    /// first of <paramref name="models"/> the game has.
    /// </summary>
    static FcsRecord Copy(GameDatabase game, string id, string name, string look, string[] models, string text)
    {
        var standIn = models.Select(m => Model(game, m)).FirstOrDefault(m => m is not null)
            ?? throw new InvalidOperationException($"The game data has none of the BUILDINGs {string.Join(", ", models)} for the {name}.");
        var model = game.Find(look) ?? standIn;

        var record = HusbandryData.New(id, FcsRecordType.Building, name);
        HusbandryData.CopyFields(model, record);
        foreach (string list in model.ReferenceLists)
        {
            if (list == "functionality") continue;

            record.References[list] = [.. model.GetReferences(list).Select(HusbandryData.Link)];
        }

        if (model != standIn) Links(standIn, record, "construction");

        record.Strings["building category"] = HusbandryData.Category;
        record.Strings["building group"] = "";
        record.Strings["Description"] = HusbandryText.Get(text);
        record.Bools["creates player town"] = true;
        return record;
    }

    /// <summary>
    /// Sets a reference list to the stand-in's (none when it has none).
    /// </summary>
    static void Links(GameRecord standIn, FcsRecord record, string list) =>
        record.References[list] = [.. standIn.GetReferences(list).Select(HusbandryData.Link)];

    /// <summary>
    /// The game's building of a name (the lowest id when several share it), or null.
    /// </summary>
    static GameRecord? Model(GameDatabase game, string name) =>
        game.OfType(FcsRecordType.Building)
            .Where(r => r.Name == name)
            .OrderBy(r => r.StringId, StringComparer.Ordinal)
            .FirstOrDefault();
}
