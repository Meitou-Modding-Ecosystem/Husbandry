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
    /// The first training job's task type; one per skill of <see cref="AnimalSkills.All"/> follows in order (Husbandry keeps 4510 to 4519).
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
    /// The training jobs' task types.
    /// </summary>
    public static readonly HashSet<TaskType> Types = [.. AnimalSkills.All.Index().Select(s => TypeOf(s.Index))];

    /// <summary>
    /// The job type that trains skill number <paramref name="index"/> of <see cref="AnimalSkills.All"/>.
    /// </summary>
    public static TaskType TypeOf(int index) => (TaskType)(FirstType + index);

    /// <summary>
    /// The skill a training job trains.
    /// </summary>
    public static ModSkill SkillOf(TaskType type) => AnimalSkills.All[(int)type - FirstType];

    /// <summary>
    /// The job's name in the jobs list ("Training Fetch").
    /// </summary>
    public static string NameOf(int index) => "Training " + Short(AnimalSkills.All[index]);

    /// <summary>
    /// The job as the post offers it to animals ("Train: Fetch").
    /// </summary>
    public static ModJobOffer OfferOf(int index) =>
        new(TypeOf(index), [HusbandryData.TrainingPost], "Train: " + Short(AnimalSkills.All[index]), ModJobTakers.Animals);

    /// <summary>
    /// A skill's name without "Beast: ".
    /// </summary>
    public static string Short(ModSkill skill) => skill.Label[(skill.Label.IndexOf(':') + 2)..];

    /// <summary>
    /// One check: keeps each training animal in its pen and trains it for the game hours since the last check.
    /// </summary>
    public static void Run(World world, HusbandrySettings settings, RngPurpose purpose, double hours)
    {
        foreach (var w in AnimalWork.Holders(world, Types))
        {
            Pens.Keep(world, w.AsHitched, settings, purpose);
            if (Hitches.Down(w.Cold) || w.Cold.InCombat) continue;

            float rate = AnimalWork.NearestPerson(world, w.Site.Position) <= TrainerReach ? GainPerHour * 2 : GainPerHour;
            AnimalSkills.Gain(w.Cold, SkillOf(w.Type), rate * (float)hours);
        }
    }
}
