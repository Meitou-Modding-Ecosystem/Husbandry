#if MEITOU_BUILDING_ROWS
// The building panel bars need the engine's AddBuildingRows hook (MeitouClientSandbox fork, not yet upstream): Husbandry.csproj defines
// MEITOU_BUILDING_ROWS only when the engine it builds against has it, so an upstream build leaves the bars out.
using System.Globalization;
using AnimalBehaviors;
using Husbandry.Capsules;
using Husbandry.Training;
using Meitou.Data.Gameplay.Bodies;
using Meitou.Game.Ui;
using Meitou.Simulation;
using Meitou.Simulation.Bodies;

namespace Husbandry;

/// <summary>
/// The bars under a Husbandry building's Condition (docs/husbandry.md "Building panel"): how near each animal or person at it is to finishing its
/// task, and the skill it works with. Built in the training check (serial, once a game second) and published whole for the host's thread, which
/// only reads it (<see cref="StatusUi"/>).
/// </summary>
public sealed class BuildingStatus
{
    /// <summary>
    /// The engine shows at most this many bars on a building, from all mods.
    /// </summary>
    public const int MaxRows = 6;

    /// <summary>
    /// The bars by building site id, as last published.
    /// </summary>
    volatile Dictionary<string, UiBuildingRow[]> _rows = [];

    /// <summary>
    /// The bars of one building; none when it has none.
    /// </summary>
    public IReadOnlyList<UiBuildingRow> For(string? site) => site is not null && _rows.TryGetValue(site, out var rows) ? rows : [];

    /// <summary>
    /// Builds every building's bars from the check's results and the world, and publishes them.
    /// </summary>
    public void Publish(World world, List<Lesson> lessons, Hunting hunting, Breeding breeding, Sexes sexes, HusbandrySettings settings)
    {
        var rows = new Dictionary<string, List<UiBuildingRow>>();
        foreach (var lesson in lessons) Lessons(world, rows, lesson);
        foreach (var view in hunting.Views) Hunter(rows, view);
        Guards(world, rows);
        Breeders(world, rows, breeding, sexes, settings);
        Benches(world, rows);

        _rows = rows.ToDictionary(r => r.Key, r => r.Value.Take(MaxRows).ToArray());
    }

    #region Animals

    /// <summary>
    /// A training animal's level towards unlocking its skill (then towards 100), and its trainer's Animal Handling.
    /// </summary>
    static void Lessons(World world, Dictionary<string, List<UiBuildingRow>> rows, Lesson lesson)
    {
        var animal = lesson.Animal;
        float level = AnimalSkills.Level(animal.Cold, lesson.Skill.Skill);
        string skill = HusbandryText.Get(lesson.Skill.Short);
        var trainer = lesson.Trainer >= 0 ? world.Characters.Cold(lesson.Trainer) : null;
        string tooltip = trainer is null ? HusbandryText.Get("Waiting for a trainer") : HusbandryText.Format("Trained by {1}", trainer.Name);
        Add(rows, animal.Site.Id, new UiBuildingRow(HusbandryText.Format("{1}: {2}", animal.Cold.Name, skill), Towards(level), Goal(level), tooltip));
        if (trainer is null) return;

        float handling = AnimalSkills.Level(trainer, Trainers.Handling);
        string name = HusbandryText.Format("Trainer {1}", trainer.Name);
        Add(rows, animal.Site.Id, new UiBuildingRow(name, handling / 100, Whole(handling), HusbandryText.Get("Animal Handling")));
    }

    /// <summary>
    /// A hunter's way round its cycle (what it is doing on the tooltip), and its Hunt.
    /// </summary>
    static void Hunter(Dictionary<string, List<UiBuildingRow>> rows, Hunting.View view)
    {
        string doing = HusbandryText.Get(view.Doing);
        Add(rows, view.Site, new UiBuildingRow(HusbandryText.Format("{1}: {2}", view.Animal.Name, doing), view.Progress, Percent(view.Progress)));
        Skill(rows, view.Site, view.Animal, AnimalSkills.Hunt);
    }

    /// <summary>
    /// Each guard's Guard, on its Pet Station.
    /// </summary>
    static void Guards(World world, Dictionary<string, List<UiBuildingRow>> rows)
    {
        foreach (var w in AnimalWork.Holders(world, Guarding.Types)) Skill(rows, w.Site.Id, w.Cold, AnimalSkills.Guard);
    }

    /// <summary>
    /// Each hitched female's way to her next breeding try, on her post (a male has no clock).
    /// </summary>
    static void Breeders(World world, Dictionary<string, List<UiBuildingRow>> rows, Breeding breeding, Sexes sexes, HusbandrySettings settings)
    {
        double period = settings.DaysBetweenTries * 24.0;
        foreach (var h in Hitches.All(world))
        {
            if (!sexes.Female(h.Cold) || h.Cold.PortraitSerial is not { } serial || !breeding.NextTry.TryGetValue(serial, out double next)) continue;

            double left = Math.Max(0, next - world.GameHours);
            float done = period <= 0 ? 1 : (float)Math.Clamp(1 - left / period, 0, 1);
            string hours = HusbandryText.Format("{1} h", Whole((float)left));
            string name = HusbandryText.Format("{1}: next litter", h.Cold.Name);
            Add(rows, h.Post.Id, new UiBuildingRow(name, done, hours, HusbandryText.Get("Breeds with a male of her kind hitched here")));
        }
    }

    /// <summary>
    /// An animal's level in a work skill, out of 100.
    /// </summary>
    static void Skill(Dictionary<string, List<UiBuildingRow>> rows, string site, CharacterCold animal, ModSkill skill)
    {
        float level = AnimalSkills.Level(animal, skill);
        string name = HusbandryText.Format("{1}: {2}", animal.Name, HusbandryText.Get(TrainableSkill.ShortOf(skill.Label)));
        Add(rows, site, new UiBuildingRow(name, level / 100, Whole(level)));
    }

    #endregion

    #region Benches

    /// <summary>
    /// Each Capsule Bench's current capsule and the skill of the person last working it, while someone works it.
    /// </summary>
    static void Benches(World world, Dictionary<string, List<UiBuildingRow>> rows)
    {
        foreach (var m in world.Machines.All)
        {
            if (m.Data.Id != BenchData.Bench || m.Craft is not { } craft) continue;

            float progress = craft.Queue.Count > 0 ? craft.Queue[0].Progress : 0;
            string making = HusbandryText.Format("Crafting {1}", HusbandryText.Get("Beast Capsule"));
            Add(rows, m.Site, new UiBuildingRow(making, progress, Percent(progress)));
            if (Smith(world, m) is not { Stats: { } stats } smith)
            {
                Add(rows, m.Site, new UiBuildingRow(HusbandryText.Get("No one at the bench"), 0));
                continue;
            }

            float level = m.Data.Stat > 0 ? stats[(StatsEnumerated)m.Data.Stat] : 0;
            string stat = Spaced(((StatsEnumerated)m.Data.Stat).ToString());
            Add(rows, m.Site, new UiBuildingRow(HusbandryText.Format("Crafter {1}", smith.Name), level / 100, Whole(level), stat));
        }
    }

    /// <summary>
    /// The person working the bench this tick; null when nobody is.
    /// </summary>
    static CharacterCold? Smith(World world, Meitou.Simulation.Production.Machine m) =>
        m.Operators > 0 && world.Characters.TryResolveNext(m.Smith, out int slot) ? world.Characters.Cold(slot) : null;

    /// <summary>
    /// A stat's name with a space before each capital after the first ("FieldMedic" to "Field Medic").
    /// </summary>
    static string Spaced(string name) => string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));

    #endregion

    #region Rows

    /// <summary>
    /// Adds a bar to a building's.
    /// </summary>
    static void Add(Dictionary<string, List<UiBuildingRow>> rows, string site, UiBuildingRow row)
    {
        if (!rows.TryGetValue(site, out var list)) rows[site] = list = [];
        list.Add(row);
    }

    /// <summary>
    /// How full a training bar is: towards <see cref="AnimalSkills.Unlocked"/> until then, towards 100 after.
    /// </summary>
    static float Towards(float level) => level < AnimalSkills.Unlocked ? level / AnimalSkills.Unlocked : level / 100;

    /// <summary>
    /// A training bar's number: "7.3/10" until unlocked, then the level.
    /// </summary>
    static string Goal(float level) =>
        level < AnimalSkills.Unlocked
            ? level.ToString("0.0", CultureInfo.InvariantCulture) + "/" + Whole(AnimalSkills.Unlocked)
            : Whole(level);

    /// <summary>
    /// A level as a whole number.
    /// </summary>
    static string Whole(float level) => ((int)level).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// A fraction as a percentage.
    /// </summary>
    static string Percent(float fraction) => ((int)(fraction * 100)).ToString(CultureInfo.InvariantCulture) + "%";

    #endregion
}

/// <summary>
/// Shows <see cref="BuildingStatus"/>' bars under a selected Husbandry building's Condition.
/// </summary>
public sealed class StatusUi(Func<BuildingStatus?> status) : IUiExtension
{
    public string Id => HusbandryMod.Id + ".status";

    public void Register(UiRegistration registration) => registration.AddBuildingRows(context => status()?.For(context.Building.SiteId) ?? []);
}
#endif
