using Meitou.Data.Gameplay.Ai;
using Meitou.Simulation;
using Meitou.Simulation.Bodies;
using Meitou.Simulation.Production;

namespace Husbandry.Training;

/// <summary>
/// One training animal at a check, for the building panel (<see cref="BuildingStatus"/>): the animal, the skill and its trainer's slot (-1 none).
/// </summary>
public readonly record struct Lesson(Worker Animal, TrainableSkill Skill, int Trainer);

/// <summary>
/// The Training Post (docs/husbandry.md "Training"): select animals, right-click the post and choose "Train: Fetch" (or another skill). A
/// training animal keeps to a pen round the post, as a hitched one does, and learns the skill by the game hour only while a trainer works the post
/// for it (<see cref="Trainers"/>, one animal a trainer), at the trainer's rate, halved without treats (<see cref="Treats"/>). Serial steps only.
/// </summary>
public static class TrainingPost
{
    /// <summary>
    /// The first training job's task type; one per skill of <see cref="AnimalSkills.All"/> follows in order (4510 to 4519, in
    /// Husbandry's number block; dependents' skills claim types from their own blocks).
    /// </summary>
    public const int FirstType = 4510;

    /// <summary>
    /// The job type that trains Husbandry's own skill number <paramref name="index"/> of <see cref="AnimalSkills.All"/>.
    /// </summary>
    public static TaskType TypeOf(int index) => (TaskType)(FirstType + index);

    /// <summary>
    /// One check: keeps each training animal in its pen and trains it for the game hours since the last check from the next free trainer at its
    /// post (animals in slot order); an animal that may not learn the skill (docs/husbandry.md "Supported animals") is sent off the job. At each
    /// game hour mark a trainer feeds a treat, and an animal without a trainer says so. Returns the animals that trained or waited.
    /// </summary>
    public static List<Lesson> Run(World world, AnimalRegistry registry, HusbandrySettings settings, RngPurpose purpose, double hours, string meat)
    {
        var lessons = new List<Lesson>();
        bool hourMark = Math.Floor(world.GameHours) > Math.Floor(world.GameHours - hours);
        var trainers = Trainers.Working(world);
        foreach (var w in AnimalWork.Holders(world, registry.TrainingTypes))
        {
            if (registry.SkillOf(w.Type) is not { } skill) continue;
            if (Refused(world, registry, w, skill)) continue;

            Pens.Keep(world, w.AsHitched, settings, purpose);
            if (Hitches.Down(w.Cold) || w.Cold.InCombat) continue;

            int trainer = trainers.TryGetValue(w.Site.Id, out var free) && free.TryDequeue(out int t) ? t : -1;
            lessons.Add(new Lesson(w, skill, trainer));
            if (trainer >= 0) Train(world, w, skill, trainer, hours, hourMark, meat);
            else if (hourMark) AnimalWork.Say(world, w.Slot, "Waiting for a trainer");
        }

        return lessons;
    }

    /// <summary>
    /// Trains the animal from the trainer for the hours, and the trainer's Animal Handling with it; at an hour mark the trainer feeds a treat, or
    /// says it has none.
    /// </summary>
    static void Train(World world, Worker w, TrainableSkill skill, int trainer, double hours, bool hourMark, string meat)
    {
        var cold = world.Characters.Cold(trainer)!;
        bool treats = Treats.Have(world, trainer, w.Site, meat);
        float rate = Trainers.Rate(cold) * (treats ? 1 : Treats.Without);
        AnimalSkills.Gain(w.Cold, skill.Skill, rate * (float)hours);
        AnimalSkills.Gain(cold, Trainers.Handling, Trainers.HandlingPerHour * (float)hours);
        if (!hourMark) return;

        if (!Treats.Feed(world, trainer, w.Site, meat)) AnimalWork.Say(world, trainer, "No Raw Meat for treats: training at half speed");
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
