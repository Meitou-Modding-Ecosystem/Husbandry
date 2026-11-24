using Meitou.Data.Gameplay.Ai;
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
    /// The registrations of the calling mod: trainable skills, kept under its id (<c>c.Id</c>). Allowed only before the first world is built
    /// (during dependents' <c>Initialize</c>). Animal kinds are registered with Animal Behaviors (docs/depending-on-husbandry.md).
    /// </summary>
    IHusbandryRegistry For(ModContext c);

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
    /// Adds a skill the Training Post teaches: registers the skill with the game, claims <paramref name="trainingType"/> as the "Training X" job
    /// and offers it on the Training Post as "Train: X" (X is the label after its colon, or the whole label). <paramref name="races"/> limits who
    /// may learn it (race string ids or names; null: every animal whose race supports training). The skill's work is the dependent's own code
    /// (read it with <see cref="Training.AnimalSkills.Has"/>).
    /// </summary>
    TrainableSkill AddSkill(ModSkill skill, TaskType trainingType, IReadOnlyCollection<string>? races = null);
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
