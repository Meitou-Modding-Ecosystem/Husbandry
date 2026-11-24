using AnimalBehaviors;
using Meitou.Data.Gameplay.Ai;
using Meitou.Data.Gameplay.Bodies;
using Meitou.Simulation;
using Meitou.Simulation.Bodies;
using Meitou.Simulation.Factions;

namespace Husbandry;

/// <summary>
/// The trainable skills Husbandry and its dependents registered (docs/husbandry.md "Training", docs/depending-on-husbandry.md), and what
/// each animal may do (Animal Behaviors' list). Filled during the mods' <c>Initialize</c>, frozen when the first world is built, read by the
/// systems after.
/// </summary>
public sealed class AnimalRegistry
{
    #region Support

    /// <summary>
    /// What animals of a race may do: Animal Behaviors' list of supported animals (docs/husbandry.md "Supported animals").
    /// </summary>
    readonly Func<RaceData, AnimalSupport> _supportOf;

    /// <summary>
    /// A registry reading support from Animal Behaviors' list.
    /// </summary>
    public AnimalRegistry(Func<RaceData, AnimalSupport> supportOf) => _supportOf = supportOf;

    /// <summary>
    /// Whether the character is an animal whose race supports all of <paramref name="what"/>.
    /// </summary>
    public bool Supports(CharacterCold cold, AnimalSupport what) =>
        SquadRegistry.IsAnimal(cold) && cold.Race is { } race && (_supportOf(race) & what) == what;

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
        (_supportOf(race) & AnimalSupport.Training) != 0 && (skill.Races is null || Named(skill.Races, race));

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
