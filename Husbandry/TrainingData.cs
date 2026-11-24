using Meitou.Data;
using Meitou.Data.Fcs;

namespace Husbandry;

/// <summary>
/// The training buildings' records (docs/husbandry.md "Training", "Data file"): the Training Post, the Pet Station and the Bone Pile. Each takes
/// its look from the building the structure import made from its own model (Structures/, docs/structures.md) when the loaded data has it, else
/// from a game building (the stand-in), and its build cost from the stand-in either way. The post and the station have no function; the pile
/// keeps the stand-in's storage. All three are unlocked by the Animal Husbandry research.
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
    /// What the pile keeps of its stand-in's storage when it wears its own look.
    /// </summary>
    static readonly string[] StorageLists = ["functionality", "limit inventory"], StorageInts = ["storage size width", "storage size height"];

    /// <summary>
    /// The texts in the build menu.
    /// </summary>
    const string PostText = "Teaches animals skills. Select your animals, right-click the post and choose what to train: Fetch, Guard, Hunt,"
        + " Scout, Pack or Tie up. A training animal keeps to a pen round the post and learns by the hour, twice as fast with one of your people"
        + " standing near. At level 10 it can do the work.",
        StationText = "A home for a guard animal. Select an animal with Guard 10 or more, right-click the station and choose Guard here: it keeps"
        + " near the station and fights with +30 attack and defence within 2000 units of it.",
        PileText = "Where a hunting animal brings its kills. Select an animal with Hunt 10 or more, right-click the pile and choose Hunt from here:"
        + " it runs out far into the wilds and comes back with raw meat, more often and with more as it learns.";

    /// <summary>
    /// The three buildings. Throws when the game has none of a building's models.
    /// </summary>
    public static IEnumerable<FcsRecord> Build(GameDatabase game) =>
    [
        Copy(game, HusbandryData.TrainingPost, "Training Post", PostLook, PostModels, PostText, keepFunction: false),
        Copy(game, HusbandryData.PetStation, "Pet Station", StationLook, StationModels, StationText, keepFunction: false),
        Copy(game, HusbandryData.BonePile, "Bone Pile", PileLook, PileModels, PileText, keepFunction: true),
    ];

    /// <summary>
    /// The building in the build menu's farming list with its own name and text: a copy of its own look when the data has it, else of the first of
    /// <paramref name="models"/> the game has.
    /// </summary>
    static FcsRecord Copy(GameDatabase game, string id, string name, string look, string[] models, string text, bool keepFunction)
    {
        var standIn = models.Select(m => Model(game, m)).FirstOrDefault(m => m is not null)
            ?? throw new InvalidOperationException($"The game data has none of the BUILDINGs {string.Join(", ", models)} for the {name}.");
        var model = game.Find(look) ?? standIn;

        var record = HusbandryData.New(id, FcsRecordType.Building, name);
        HusbandryData.CopyFields(model, record);
        foreach (string list in model.ReferenceLists)
        {
            if (list == "functionality" && !keepFunction) continue;

            record.References[list] = [.. model.GetReferences(list).Select(HusbandryData.Link)];
        }

        if (model != standIn) KeepFromStandIn(standIn, record, keepFunction);

        record.Strings["building category"] = "FARMING";
        record.Strings["building group"] = "";
        record.Strings["Description"] = HusbandryText.Get(text);
        record.Bools["creates player town"] = true;
        return record;
    }

    /// <summary>
    /// A building on its own look keeps the stand-in's build cost, and the pile its storage, so only the look changes.
    /// </summary>
    static void KeepFromStandIn(GameRecord standIn, FcsRecord record, bool keepFunction)
    {
        Links(standIn, record, "construction");
        if (!keepFunction) return;

        foreach (string list in StorageLists) Links(standIn, record, list);
        record.Bools["has inventory"] = standIn.GetBool("has inventory");
        foreach (string size in StorageInts)
        {
            if (standIn.Ints.TryGetValue(size, out int value)) record.Ints[size] = value;
            else record.Ints.Remove(size);
        }
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
