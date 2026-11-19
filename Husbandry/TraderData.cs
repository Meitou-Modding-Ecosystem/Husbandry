using Meitou.Data;
using Meitou.Data.Fcs;

namespace Husbandry;

/// <summary>
/// The Beastmaster's records (docs/husbandry.md "Data file"): his CHARACTER, a copy of the game's Western Hive "Soldier Drone" in the best kit, his
/// SQUAD_TEMPLATE, and his conversation (a DIALOGUE with its lines and the DA 48 action that opens the animal buying window). The kit, faction and
/// links name the game's records by id; a linked record the game lacks is left out.
/// </summary>
public static class TraderData
{
    /// <summary>
    /// The game's CHARACTER he is copied from: its string id, else its name.
    /// </summary>
    public const string ModelId = "42196-rebirth.mod", ModelName = "Soldier Drone";

    /// <summary>
    /// His name.
    /// </summary>
    public const string Name = "Hive Beastmaster";

    /// <summary>
    /// The game's records his kit names: Crab Armour and Crab Trousers (crab-shell plate from the beasts he breaks), the Hiver Chain Shirt, and a
    /// Naginata of Cross make (whose only model is the Meitou, the top grade). A soldier drone wears no hat or shoes.
    /// </summary>
    static readonly string[] Clothing = ["64892-Newwworld.mod", "64891-Newwworld.mod", "1534053-Newwworld Plus.mod"];

    /// <summary>
    /// His weapon and its maker (see <see cref="Clothing"/>).
    /// </summary>
    const string Weapon = "52308-rebirth.mod", Maker = "52288-rebirth.mod";

    /// <summary>
    /// The armour grade of his clothing: 5, Masterwork, the top.
    /// </summary>
    const int TopGrade = 5;

    /// <summary>
    /// The DIALOG_ACTION numbers used: 48 CHOOSE_ANIMALS_BUYING.
    /// </summary>
    const int BuyAnimals = 48;

    /// <summary>
    /// His records, after the research and the post. Throws when the game has no <see cref="ModelName"/> character.
    /// </summary>
    public static IEnumerable<FcsRecord> Build(GameDatabase game)
    {
        var model = Model(game) ?? throw new InvalidOperationException($"The game data has no CHARACTER '{ModelName}'.");
        return [CharacterRecord(game, model), SquadRecord(game), .. Conversation()];
    }

    /// <summary>
    /// The character he is copied from, or null.
    /// </summary>
    static GameRecord? Model(GameDatabase game) =>
        game.Find(ModelId) is { Type: FcsRecordType.Character } byId
            ? byId
            : game.OfType(FcsRecordType.Character)
                .Where(r => r.Name == ModelName)
                .OrderBy(r => r.StringId, StringComparer.Ordinal)
                .FirstOrDefault();

    #region Character and squad

    /// <summary>
    /// The squad flags he has off: no shop, no home, no regrowth.
    /// </summary>
    static readonly string[] OffFlags = ["building public", "is trader", "regenerates", "roaming military", "malnourished", "sell home"];

    /// <summary>
    /// The Beastmaster: the soldier drone's race, look and talk, with masterwork crab armour, a Cross naginata, a veteran's skills and his
    /// conversation.
    /// </summary>
    static FcsRecord CharacterRecord(GameDatabase game, GameRecord model)
    {
        var record = HusbandryData.New(HusbandryData.Trader, FcsRecordType.Character, Name);
        HusbandryData.CopyFields(model, record);
        foreach (string list in model.ReferenceLists) record.References[list] = [.. model.GetReferences(list).Select(HusbandryData.Link)];

        record.Ints["armour grade"] = TopGrade;
        record.Ints["armour upgrade chance"] = 0;
        record.Ints["combat stats"] = 80;
        record.Ints["strength"] = 75;
        record.Ints["unarmed stats"] = 40;
        record.Ints["stats randomise"] = 5;
        record.Ints["money min"] = 0;
        record.Ints["money max"] = 0;
        record.References["clothing"] = Links(game, Clothing, 1, 100);
        record.References["weapons"] = Links(game, [Weapon], 1, 0);
        record.References["weapon level"] = Links(game, [Maker], 0, 0);
        record.References["dialogue"] = [new FcsReference(HusbandryData.Conversation, 0, 0, 0)];
        return record;
    }

    /// <summary>
    /// His squad: him alone (his stock joins it in play), of the Western Hive, with no AI package: the mod holds and walks him.
    /// </summary>
    static FcsRecord SquadRecord(GameDatabase game)
    {
        var record = HusbandryData.New(HusbandryData.TraderSquad, FcsRecordType.SquadTemplate, Name);
        foreach (string flag in OffFlags) record.Bools[flag] = false;

        record.Bools["dont multiply"] = true;
        record.Floats["animal age random"] = 0;
        record.Ints["num random chars"] = 0;
        record.Ints["num random chars max"] = 0;
        record.Ints["vendor money"] = 0;
        record.Strings["building name"] = "";
        record.Strings["layout interior"] = "";
        record.Strings["layout exterior"] = "";
        record.References["leader"] = [new FcsReference(HusbandryData.Trader, 1, 0, 0)];
        record.References["faction"] = Links(game, [Beastmaster.Faction], 0, 0);
        return record;
    }

    /// <summary>
    /// Links to those of the game's records that exist, each with the values given.
    /// </summary>
    static List<FcsReference> Links(GameDatabase game, IEnumerable<string> ids, int value0, int value1) =>
        [.. ids.Where(id => game.Find(id) is not null).Select(id => new FcsReference(id, value0, value1, 0))];

    #endregion

    #region Conversation

    /// <summary>
    /// The record numbers of the conversation's lines, in file order after the DIALOGUE (5).
    /// </summary>
    const int Greeting = 6, Show = 7, Buy = 8, AskWho = 9, Who = 10, AskHow = 11, How = 12, AskPairs = 13, Pairs = 14, AskBye = 15, Bye = 16;

    /// <summary>
    /// The player's choices after each of his answers.
    /// </summary>
    static readonly int[] Choices = [Show, AskWho, AskHow, AskPairs, AskBye];

    /// <summary>
    /// The DIALOGUE, its lines (his are speaker 0, the player's 1) and the buying action, in record number order.
    /// </summary>
    static IEnumerable<FcsRecord> Conversation()
    {
        var dialogue = HusbandryData.New(HusbandryData.Conversation, FcsRecordType.Dialogue, "husbandry beastmaster");
        dialogue.Bools["for enemies"] = false;
        dialogue.Bools["locked"] = false;
        dialogue.Bools["monologue"] = true;
        dialogue.Bools["one at a time"] = false;
        Chances(dialogue);
        dialogue.Strings["tag"] = "";
        dialogue.References["lines"] = [Ref(Greeting)];

        return
        [
            dialogue,
            Line(Greeting, 0, Choices,
                "Soft-skin. You stand before the Beastmaster. The wild things of this world, this one breaks them and makes them serve. Garru."
                    + " Crab. Bonedog. All kneel now. The pen behind this place is full.",
                "Soft-skin. This one has beasts in the pen out back. Strong ones. You want?"),
            Line(Show, 1, [], "Show me your beasts.", effect: Buy),
            Action(Buy, BuyAnimals),
            Line(AskWho, 1, [Who], "A hiver who tames beasts?"),
            Line(Who, 0, Choices,
                "This one was hatched a soldier, to guard the Prince. The Prince looked out at the wild lands and said: the Hive needs backs to carry,"
                    + " jaws to guard. So this one walked out alone with a pole and no fear. Every beast in the pen, this one fought it until it"
                    + " stopped fighting. The ones the Hive does not need, this one sells."),
            Line(AskHow, 1, [How], "How do you tame a crab?"),
            Line(How, 0, Choices,
                "A crab has a thick shell and a thin mind. Strike the shell until the mind listens. Then feed it. A fed beast is a loyal beast. Garru"
                    + " are slow to anger, bonedogs quick. All of them learn. The wild is only a Hive that has not met its Queen."),
            Line(AskPairs, 1, [Pairs], "Why keep a male and a female of each?"),
            Line(Pairs, 0, Choices,
                "So the herd grows without this one. Pen a male and a female of one kind, keep them fed, and young will come. The Hive grows the"
                    + " same way. Sold beasts are gone from the pen; by the next day this one has caught more."),
            Line(AskBye, 1, [Bye], "Not today."),
            Line(Bye, 0, [], "The beasts will wait. They are patient. Like the Hive."),
        ];
    }

    /// <summary>
    /// A DIALOGUE_LINE: who says it, its text (and a second wording), the lines that may follow, and an action it runs.
    /// </summary>
    static FcsRecord Line(int number, int speaker, int[] next, string text, string? text1 = null, int? effect = null)
    {
        var line = HusbandryData.New(Id(number), FcsRecordType.DialogueLine, "DIALOGUE_LINE");
        Chances(line);
        line.Ints["speaker"] = speaker;
        line.Strings["text0"] = text;
        if (text1 is not null) line.Strings["text1"] = text1;
        if (next.Length > 0) line.References["lines"] = [.. next.Select(Ref)];
        if (effect is { } action) line.References["effects"] = [Ref(action)];

        return line;
    }

    /// <summary>
    /// A DIALOG_ACTION running action <paramref name="action"/>.
    /// </summary>
    static FcsRecord Action(int number, int action)
    {
        var record = HusbandryData.New(Id(number), FcsRecordType.DialogAction, "DIALOG_ACTION");
        record.Ints["action name"] = action;
        return record;
    }

    /// <summary>
    /// The fields every line and dialogue of the game carries: always chosen, no repetition limit, no score bonus, any target.
    /// </summary>
    static void Chances(FcsRecord record)
    {
        record.Floats["chance permanent"] = 100;
        record.Floats["chance temporary"] = 100;
        record.Ints["repetition limit"] = 0;
        record.Ints["score bonus"] = 0;
        record.Ints["target is type"] = 0;
    }

    /// <summary>
    /// A link to the record of a number.
    /// </summary>
    static FcsReference Ref(int number) => new(Id(number), 0, 0, 0);

    /// <summary>
    /// The string id of a record number.
    /// </summary>
    static string Id(int number) => $"{number}-{HusbandryData.FileName}";

    #endregion
}
