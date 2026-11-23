using Meitou.Data;
using Meitou.Data.Fcs;

namespace Husbandry;

/// <summary>
/// The training buildings' records (docs/husbandry.md "Training", "Data file"): the Training Post, the Pet Station and the Bone Pile, each a copy of
/// a game building (its look, collision, size and build cost) until their own models exist. The post and the station drop the model's function;
/// the pile keeps its model's storage. All three are unlocked by the Animal Husbandry research.
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
        Copy(game, HusbandryData.TrainingPost, "Training Post", PostModels, PostText, keepFunction: false),
        Copy(game, HusbandryData.PetStation, "Pet Station", StationModels, StationText, keepFunction: false),
        Copy(game, HusbandryData.BonePile, "Bone Pile", PileModels, PileText, keepFunction: true),
    ];

    /// <summary>
    /// A copy of the first of <paramref name="models"/> the game has, in the build menu's farming list with its own name and text.
    /// </summary>
    static FcsRecord Copy(GameDatabase game, string id, string name, string[] models, string text, bool keepFunction)
    {
        var model = models.Select(m => Model(game, m)).FirstOrDefault(m => m is not null)
            ?? throw new InvalidOperationException($"The game data has none of the BUILDINGs {string.Join(", ", models)} for the {name}.");

        var record = HusbandryData.New(id, FcsRecordType.Building, name);
        HusbandryData.CopyFields(model, record);
        foreach (string list in model.ReferenceLists)
        {
            if (list == "functionality" && !keepFunction) continue;

            record.References[list] = [.. model.GetReferences(list).Select(HusbandryData.Link)];
        }

        record.Strings["building category"] = "FARMING";
        record.Strings["building group"] = "";
        record.Strings["Description"] = text;
        record.Bools["creates player town"] = true;
        return record;
    }

    /// <summary>
    /// The game's building of a name (the lowest id when several share it), or null.
    /// </summary>
    static GameRecord? Model(GameDatabase game, string name) =>
        game.OfType(FcsRecordType.Building)
            .Where(r => r.Name == name)
            .OrderBy(r => r.StringId, StringComparer.Ordinal)
            .FirstOrDefault();
}
