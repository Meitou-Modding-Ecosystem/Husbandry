using Meitou.Data.Gameplay.Ai;
using Meitou.Data.Gameplay.Bodies;
using Meitou.Modding;
using Meitou.Simulation.Bodies;

namespace Husbandry;

/// <summary>
/// Husbandry's public surface (docs/depending-on-husbandry.md), implemented by the entry type: a dependent gets it with
/// <c>c.Dependency&lt;IHusbandryApi&gt;("husbandry")</c> in its own <c>Initialize</c>.
/// </summary>
public interface IHusbandryApi
{
    /// <summary>
    /// The registrations of the calling mod: animal kinds and trainable skills, kept under its id (<c>c.Id</c>). Allowed only before the first
    /// world is built (during dependents' <c>Initialize</c>).
    /// </summary>
    IHusbandryRegistry For(ModContext c);

    /// <summary>
    /// What Husbandry lets animals of a race do: a registration for the race (the last loaded wins), else the built-in list, else the default
    /// (docs/husbandry.md "Supported animals").
    /// </summary>
    AnimalSupport SupportOf(RaceData race);

    /// <summary>
    /// Every registered animal kind, Husbandry's own first, then dependents' in load order.
    /// </summary>
    IReadOnlyList<AnimalKind> Kinds { get; }

    /// <summary>
    /// Every skill the Training Post teaches, Husbandry's own six first, then dependents' in load order.
    /// </summary>
    IReadOnlyList<TrainableSkill> Skills { get; }
}

/// <summary>
/// One mod's registrations with Husbandry (<see cref="IHusbandryApi.For"/>).
/// </summary>
public interface IHusbandryRegistry
{
    /// <summary>
    /// Says what animals of a race may do. A later registration for the same race (from this mod or one loaded after it) replaces this one.
    /// </summary>
    void AddAnimal(AnimalKind kind);

    /// <summary>
    /// Adds a skill the Training Post teaches: registers the skill with the game, claims <paramref name="trainingType"/> as the "Training X" job
    /// and offers it on the Training Post as "Train: X" (X is the label after its colon, or the whole label). <paramref name="races"/> limits who
    /// may learn it (race string ids or names; null: every animal whose race supports training). The skill's work is the dependent's own code
    /// (read it with <see cref="Training.AnimalSkills.Has"/>).
    /// </summary>
    TrainableSkill AddSkill(ModSkill skill, TaskType trainingType, IReadOnlyCollection<string>? races = null);
}

/// <summary>
/// What Husbandry lets an animal do (docs/husbandry.md "Supported animals").
/// </summary>
[Flags]
public enum AnimalSupport
{
    /// <summary>
    /// Nothing: Husbandry leaves the animal alone (it can still be hitched, so it keeps to a pen).
    /// </summary>
    None = 0,

    /// <summary>
    /// Hitched adult pairs of the race breed at a Hitching Post.
    /// </summary>
    Breeding = 1,

    /// <summary>
    /// It learns skills at the Training Post.
    /// </summary>
    Training = 2,

    /// <summary>
    /// A wild one, knocked out, can be caught in a Beast Capsule (summoning it makes it the player's).
    /// </summary>
    Taming = 4,

    /// <summary>
    /// One of the player's own can be put in a Beast Capsule and summoned again.
    /// </summary>
    Summoning = 8,

    /// <summary>
    /// Everything.
    /// </summary>
    All = Breeding | Training | Taming | Summoning,
}

/// <summary>
/// One registered animal race: the RACE record's string id or its name (matched without case, the id first), what it supports, the mod that
/// registered it, and a note for the list (docs/husbandry.md "Supported animals").
/// </summary>
public sealed record AnimalKind(string Race, AnimalSupport Support, string Note = "")
{
    /// <summary>
    /// The id of the mod that registered it (set by the registry).
    /// </summary>
    public string Owner { get; init; } = "";
}

/// <summary>
/// One skill the Training Post teaches: the skill, its training job's type, the mod that registered it and the races that may learn it (null:
/// every race that supports training).
/// </summary>
public sealed record TrainableSkill(ModSkill Skill, TaskType TrainingType, string Owner, IReadOnlySet<string>? Races)
{
    /// <summary>
    /// The skill's name without its prefix ("Fetch" for "Beast: Fetch"): the label after its colon, or the whole label.
    /// </summary>
    public string Short => ShortOf(Skill.Label);

    /// <summary>
    /// The label after its colon, or the whole label.
    /// </summary>
    public static string ShortOf(string label) => label.IndexOf(':') is var at and >= 0 ? label[(at + 1)..].Trim() : label.Trim();
}
