using Meitou.Data.Gameplay.Ai;
using Meitou.Simulation;
using Meitou.Simulation.Bodies;
using Meitou.Simulation.Production;

namespace Husbandry.Training;

/// <summary>
/// The Training Post (docs/husbandry.md "Training"): select animals, right-click the post and choose "Train: Fetch" (or another skill). A
/// training animal keeps to a pen round the post, as a hitched one does, and learns the skill by the game hour, twice as fast while a person of
/// the player's stands near. Serial steps only.
/// </summary>
public static class TrainingPost
{
    /// <summary>
    /// The first training job's task type; one per skill of <see cref="AnimalSkills.All"/> follows in order (4510 to 4519, in
    /// Husbandry's number block; dependents' skills claim types from their own blocks).
    /// </summary>
    public const int FirstType = 4510;

    /// <summary>
    /// Skill gained per game hour alone, on the game's curve (about ten hours from 0 to <see cref="AnimalSkills.Unlocked"/>).
    /// </summary>
    public const float GainPerHour = 1;

    /// <summary>
    /// How near a person must stand to train the animal twice as fast, in game units.
    /// </summary>
    public const float TrainerReach = 200;

    /// <summary>
    /// The job type that trains Husbandry's own skill number <paramref name="index"/> of <see cref="AnimalSkills.All"/>.
    /// </summary>
    public static TaskType TypeOf(int index) => (TaskType)(FirstType + index);

    /// <summary>
    /// One check: keeps each training animal in its pen and trains it for the game hours since the last check; an animal that may not learn
    /// the skill (docs/husbandry.md "Supported animals") is sent off the job.
    /// </summary>
    public static void Run(World world, AnimalRegistry registry, HusbandrySettings settings, RngPurpose purpose, double hours)
    {
        foreach (var w in AnimalWork.Holders(world, registry.TrainingTypes))
        {
            if (registry.SkillOf(w.Type) is not { } skill) continue;
            if (Refused(world, registry, w, skill)) continue;

            Pens.Keep(world, w.AsHitched, settings, purpose);
            if (Hitches.Down(w.Cold) || w.Cold.InCombat) continue;

            float rate = AnimalWork.NearestPerson(world, w.Site.Position) <= TrainerReach ? GainPerHour * 2 : GainPerHour;
            AnimalSkills.Gain(w.Cold, skill.Skill, rate * (float)hours);
        }
    }

    /// <summary>
    /// Takes the job off an animal that may not learn the skill, telling the player; true when it did.
    /// </summary>
    static bool Refused(World world, AnimalRegistry registry, Worker w, TrainableSkill skill)
    {
        if (registry.MayLearn(w.Cold, skill)) return false;

        w.Cold.Jobs.RemoveAll(j => j.Type == w.Type);
        AnimalWork.Say(world, w.Slot, HusbandryText.Format("A {1} can't learn {2}", w.Cold.Race?.Name ?? HusbandryText.Get("beast"), HusbandryText.Get(skill.Short)));
        return true;
    }
}
