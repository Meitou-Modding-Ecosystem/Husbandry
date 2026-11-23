using Meitou.Data.Gameplay.Ai;
using Meitou.Data.Gameplay.Bodies;
using Meitou.Simulation;
using Meitou.Simulation.Bodies;
using Meitou.Simulation.Factions;

namespace Husbandry;

/// <summary>
/// The animal kinds and trainable skills Husbandry and its dependents registered (docs/husbandry.md "Supported animals", docs/depending-on-husbandry.md),
/// and what each animal may do. Filled during the mods' <c>Initialize</c>, frozen when the first world is built, read by the systems after.
/// </summary>
public sealed class AnimalRegistry
{
    #region Kinds

    /// <summary>
    /// What a robot's race supports unless a registration says otherwise: training only (it neither breeds nor fits a capsule).
    /// </summary>
    public const AnimalSupport RobotSupport = AnimalSupport.Training;

    /// <summary>
    /// What any other animal's race supports unless a registration says otherwise.
    /// </summary>
    public const AnimalSupport DefaultSupport = AnimalSupport.All;

    /// <summary>
    /// Husbandry's own list (docs/husbandry.md "Supported animals").
    /// </summary>
    public static readonly AnimalKind[] BuiltIn =
    [
        new("Leviathan", AnimalSupport.Breeding | AnimalSupport.Training, "too big for a capsule") { Owner = HusbandryMod.Id },
    ];

    /// <summary>
    /// The registered kinds in registration order.
    /// </summary>
    readonly List<AnimalKind> _kinds = [.. BuiltIn];

    /// <summary>
    /// The registered kinds in registration order.
    /// </summary>
    public IReadOnlyList<AnimalKind> Kinds => _kinds;

    /// <summary>
    /// Registers an animal kind for a mod.
    /// </summary>
    public void AddKind(string owner, AnimalKind kind)
    {
        CheckOpen();
        if (string.IsNullOrWhiteSpace(kind.Race)) throw new ArgumentException("An animal kind needs a race (a RACE string id or name).", nameof(kind));

        _kinds.Add(kind with { Owner = owner, Race = kind.Race.Trim() });
    }

    /// <summary>
    /// What animals of a race may do (<see cref="SupportOf(string, string, bool)"/>).
    /// </summary>
    public AnimalSupport SupportOf(RaceData race) => SupportOf(race.StringId, race.Name, race.IsRobot);

    /// <summary>
    /// What animals of a race may do: the last registration naming its string id, else the last naming its name, else
    /// <see cref="RobotSupport"/> for a robot and <see cref="DefaultSupport"/> for the rest.
    /// </summary>
    public AnimalSupport SupportOf(string stringId, string name, bool robot)
    {
        if (Last(stringId) is { } byId) return byId.Support;
        if (Last(name) is { } byName) return byName.Support;

        return robot ? RobotSupport : DefaultSupport;
    }

    /// <summary>
    /// Whether a character is an animal whose race supports everything in <paramref name="what"/>.
    /// </summary>
    public bool Supports(CharacterCold cold, AnimalSupport what) =>
        SquadRegistry.IsAnimal(cold) && cold.Race is { } race && (SupportOf(race) & what) == what;

    /// <summary>
    /// The list of what each race may do, a line per race ("Garru: breeding, training, taming, summoning"), sorted by name.
    /// </summary>
    public List<string> Describe(IEnumerable<(string StringId, string Name, bool Robot)> races) =>
        races
            .DistinctBy(r => r.StringId)
            .OrderBy(r => r.Name, StringComparer.Ordinal)
            .Select(r => $"{r.Name}: {Words(SupportOf(r.StringId, r.Name, r.Robot))}")
            .ToList();

    /// <summary>
    /// The words for a support ("breeding, training", "nothing").
    /// </summary>
    public static string Words(AnimalSupport support)
    {
        var words = Enum.GetValues<AnimalSupport>()
            .Where(s => s is not (AnimalSupport.None or AnimalSupport.All) && (support & s) == s)
            .Select(s => s.ToString().ToLowerInvariant())
            .ToList();
        return words.Count == 0 ? "nothing" : string.Join(", ", words);
    }

    /// <summary>
    /// The last registration naming a race, or null.
    /// </summary>
    AnimalKind? Last(string race) =>
        race.Length == 0 ? null : _kinds.LastOrDefault(k => string.Equals(k.Race, race, StringComparison.OrdinalIgnoreCase));

    #endregion

    #region Skills

    /// <summary>
    /// The trainable skills in registration order.
    /// </summary>
    readonly List<TrainableSkill> _skills = [];

    /// <summary>
    /// The training job types, for finding the animals that hold one.
    /// </summary>
    readonly HashSet<TaskType> _trainingTypes = [];

    /// <summary>
    /// The trainable skills in registration order.
    /// </summary>
    public IReadOnlyList<TrainableSkill> Skills => _skills;

    /// <summary>
    /// The training job types.
    /// </summary>
    public IReadOnlySet<TaskType> TrainingTypes => _trainingTypes;

    /// <summary>
    /// Registers a trainable skill for a mod (the game-side registrations are the caller's: <see cref="HusbandryMod"/>).
    /// </summary>
    public TrainableSkill AddSkill(string owner, ModSkill skill, TaskType trainingType, IReadOnlyCollection<string>? races)
    {
        CheckOpen();
        if (_skills.Any(s => s.Skill.Id == skill.Id)) throw new InvalidOperationException($"Skill '{skill.Id}' is already trainable.");
        if (!_trainingTypes.Add(trainingType)) throw new InvalidOperationException($"Training job type {trainingType} is already taken.");

        var set = races is null ? null : new HashSet<string>(races.Select(r => r.Trim()), StringComparer.OrdinalIgnoreCase);
        var trainable = new TrainableSkill(skill, trainingType, owner, set);
        _skills.Add(trainable);
        return trainable;
    }

    /// <summary>
    /// The skill a training job teaches, or null.
    /// </summary>
    public TrainableSkill? SkillOf(TaskType type) => _skills.Find(s => s.TrainingType == type);

    /// <summary>
    /// Whether a character may learn a skill: an animal whose race supports training and that the skill's race list (if any) names.
    /// </summary>
    public bool MayLearn(CharacterCold cold, TrainableSkill skill) => SquadRegistry.IsAnimal(cold) && cold.Race is { } race && MayLearn(race, skill);

    /// <summary>
    /// Whether an animal of a race may learn a skill: its race supports training and the skill's race list (if any) names it.
    /// </summary>
    public bool MayLearn(RaceData race, TrainableSkill skill) =>
        (SupportOf(race) & AnimalSupport.Training) != 0 && (skill.Races is null || Named(skill.Races, race));

    /// <summary>
    /// Whether a race list names a race by string id or name.
    /// </summary>
    static bool Named(IReadOnlySet<string> races, RaceData race) => races.Contains(race.StringId) || races.Contains(race.Name);

    #endregion

    #region Lifetime

    /// <summary>
    /// Set when the first world is built; registrations after it throw.
    /// </summary>
    public bool Frozen { get; private set; }

    /// <summary>
    /// Ends registration (the first world is being built).
    /// </summary>
    public void Freeze() => Frozen = true;

    /// <summary>
    /// Drops everything a mod registered (its <c>Initialize</c> failed).
    /// </summary>
    public void Drop(string owner)
    {
        _kinds.RemoveAll(k => k.Owner == owner);
        foreach (var skill in _skills.Where(s => s.Owner == owner)) _trainingTypes.Remove(skill.TrainingType);
        _skills.RemoveAll(s => s.Owner == owner);
    }

    /// <summary>
    /// Throws once registration has ended.
    /// </summary>
    void CheckOpen()
    {
        if (Frozen) throw new InvalidOperationException("Husbandry takes registrations only in a mod's Initialize, before the first world.");
    }

    #endregion
}
