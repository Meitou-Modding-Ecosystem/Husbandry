using AnimalBehaviors;
using Meitou.Data;
using Meitou.Data.Fcs;

namespace Husbandry;

/// <summary>
/// The mod's records (docs/husbandry.md "Data file"): the Animal Husbandry research and the Hitching Post it unlocks, then the Beastmaster of Mourn
/// (<see cref="TraderData"/>). The post is a copy of the game's own Prisoner Pole (look, collision, size, build cost) without its cage function,
/// so the file is built from the installed game and holds no game content of its own, only the copied fields and links by id.
/// </summary>
public static class HusbandryData
{
    /// <summary>
    /// The build menu category that lists every Husbandry building (the menu shows a category's name as it is written, as the game's FARMING).
    /// </summary>
    public const string Category = "HUSBANDRY";

    /// <summary>
    /// The data file's name; a record's id ends with it.
    /// </summary>
    public const string FileName = "Husbandry.mod";

    /// <summary>
    /// The record ids: the research, the post, the Beastmaster's CHARACTER, his SQUAD_TEMPLATE, his conversation's DIALOGUE (its lines follow,
    /// <see cref="TraderData"/>) and his shop's VENDOR_LIST.
    /// </summary>
    public const string Research = "1-" + FileName, Post = "2-" + FileName, Trader = "3-" + FileName, TraderSquad = "4-" + FileName,
        Conversation = "5-" + FileName, PackList = "19-" + FileName;

    /// <summary>
    /// The training buildings' record ids (<see cref="TrainingData"/>).
    /// </summary>
    public const string TrainingPost = "30-" + FileName, PetStation = "31-" + FileName, BonePile = "32-" + FileName;

    /// <summary>
    /// The Beast Capsule's record id (<see cref="Capsules.CapsuleData"/>).
    /// </summary>
    public const string Capsule = "33-" + FileName;

    /// <summary>
    /// The record number of the first stock SQUAD_TEMPLATE; one per kind of <see cref="Beastmaster.Kinds"/> follows in order.
    /// </summary>
    public const int FirstStockSquad = 20;

    /// <summary>
    /// The SQUAD_TEMPLATE of one stock animal of kind <paramref name="kind"/> (<see cref="Beastmaster.Kinds"/>).
    /// </summary>
    public static string StockSquad(int kind) => $"{FirstStockSquad + kind}-{FileName}";

    /// <summary>
    /// The game's building the post is copied from: its string id, else its name.
    /// </summary>
    public const string ModelId = "18913-small_changes_otto.mod", ModelName = "Prisoner Pole";

    /// <summary>
    /// The building the structure import makes from Structures/hitching-post.glb (block 4 in Structures/ids.json), whose look the post takes when
    /// the loaded data has it.
    /// </summary>
    public const string PostLook = "900400-" + FileName;

    /// <summary>
    /// The most animals the Hitching Post holds (Animal Behaviors' hitch capacity, docs/husbandry.md "Hitching Post").
    /// </summary>
    public const int HitchCapacity = 6;

    /// <summary>
    /// The post's text in the build menu.
    /// </summary>
    public const string PostText = "A stout post to tether animals to. Select your animals, right-click the post and choose Hitch here: they stay"
        + " within a pen round it, leaving only to eat at an Animal Feeder. Hitched adults of the same kind, a male and a female, try to breed every"
        + " few days, more often when well fed. Build an Animal Feeder nearby and keep it stocked.";

    /// <summary>
    /// The research's text.
    /// </summary>
    public const string ResearchText = "The basics of keeping animals at your base. Unlocks the Hitching Post: animals hitched to it keep to a pen"
        + " round it and breed when a male and a female of the same kind are there. Together with an Animal Feeder (Item Storage) to feed them, it is"
        + " all a ranch needs. Also unlocks the Training Post, the Pet Station and the Bone Pile, to teach animals work, and the Capsule Bench, to"
        + " make Beast Capsules.";

    /// <summary>
    /// Builds the data file from the game's records. Throws when the game has no <see cref="ModelName"/> building or no
    /// <see cref="TraderData.ModelName"/> character.
    /// </summary>
    public static FcsFile Build(GameDatabase game)
    {
        var model = Model(game) ?? throw new InvalidOperationException($"The game data has no BUILDING '{ModelName}'.");

        var file = new FcsFile
        {
            Author = "MeitouMods",
            Description = "Husbandry: hitching posts, breeding, animal training and the Beastmaster of Mourn. Generated; see docs/husbandry.md.",
        };
        var capsule = Capsules.CapsuleData.Build(game);
        Capsules.BenchData.AddRecipe(game, capsule);
        file.Records.AddRange([ResearchRecord(), PostRecord(game, model), .. TraderData.Build(game), .. TrainingData.Build(game), capsule,
            .. Capsules.ThrowData.Build(), .. Capsules.BenchData.Build(game)]);
        for (int k = 0; k < file.Records.Count; k++) file.Records[k].Id = k + 1;

        file.NextId = file.Records.Count + 1;
        return file;
    }

    /// <summary>
    /// The building the post is copied from, or null.
    /// </summary>
    static GameRecord? Model(GameDatabase game) =>
        game.Find(ModelId) is { Type: FcsRecordType.Building } byId
            ? byId
            : game.OfType(FcsRecordType.Building)
                .Where(r => r.Name == ModelName)
                .OrderBy(r => r.StringId, StringComparer.Ordinal)
                .FirstOrDefault();

    /// <summary>
    /// The research: level 1, no blueprint, in the Core list, as quick as the game's Item Storage.
    /// </summary>
    static FcsRecord ResearchRecord()
    {
        var record = New(Research, FcsRecordType.Research, "Animal Husbandry");
        record.Ints["level"] = 1;
        record.Ints["money"] = 0;
        record.Ints["repeats"] = 0;
        record.Ints["time"] = 4;
        record.Ints["power capacity increase"] = 0;
        record.Ints["power increase"] = 0;
        record.Floats["production mult"] = 1;
        record.Floats["repeat mult"] = 1;
        record.Bools["blueprint only"] = false;
        record.Bools["is level upgrade"] = false;
        record.Strings["category"] = "Core";
        record.Strings["description"] = HusbandryText.Get(ResearchText);
        record.References["enable buildings"] =
        [
            new FcsReference(Post, 0, 0, 0),
            new FcsReference(TrainingPost, 0, 0, 0),
            new FcsReference(PetStation, 0, 0, 0),
            new FcsReference(BonePile, 0, 0, 0),
            new FcsReference(Capsules.BenchData.Bench, 0, 0, 0),
        ];
        return record;
    }

    /// <summary>
    /// The post: its own model's look (<see cref="PostLook"/>) when the data has it, else the stand-in's, with the stand-in's build cost and
    /// without its function (the cage), in the build menu's Husbandry category, with its own text, marked as an Animal Behaviors hitching post
    /// holding <see cref="HitchCapacity"/> animals.
    /// </summary>
    static FcsRecord PostRecord(GameDatabase game, GameRecord standIn)
    {
        var model = game.Find(PostLook) ?? standIn;
        var record = New(Post, FcsRecordType.Building, "Hitching Post");
        CopyFields(model, record);
        foreach (string list in model.ReferenceLists)
        {
            if (list == "functionality") continue;

            record.References[list] = [.. model.GetReferences(list).Select(Link)];
        }

        if (model != standIn) record.References["construction"] = [.. standIn.GetReferences("construction").Select(Link)];

        record.Strings["building category"] = Category;
        record.Strings["building group"] = "";
        record.Strings["Description"] = HusbandryText.Get(PostText);
        record.Bools["creates player town"] = true;
        Sites.MarkHitch(record, HitchCapacity);
        return record;
    }

    /// <summary>
    /// A new record, as a mod file adds one.
    /// </summary>
    internal static FcsRecord New(string id, FcsRecordType type, string name) => new() { StringId = id, Flags = 0x10, Name = HusbandryText.Get(name), RecordType = type };

    /// <summary>
    /// Copies every field of a merged record but its links.
    /// </summary>
    internal static void CopyFields(GameRecord from, FcsRecord to)
    {
        foreach (var (key, value) in from.Bools) to.Bools[key] = value;
        foreach (var (key, value) in from.Floats) to.Floats[key] = value;
        foreach (var (key, value) in from.Ints) to.Ints[key] = value;
        foreach (var (key, value) in from.Vector3S) to.Vector3S[key] = value;
        foreach (var (key, value) in from.Vector4S) to.Vector4S[key] = value;
        foreach (var (key, value) in from.Strings) to.Strings[key] = value;
        foreach (var (key, value) in from.Filenames) to.Filenames[key] = value;
    }

    /// <summary>
    /// A merged link as a file's.
    /// </summary>
    internal static FcsReference Link(GameReference link) => new(link.TargetStringId, link.Values.Value0, link.Values.Value1, link.Values.Value2);
}
