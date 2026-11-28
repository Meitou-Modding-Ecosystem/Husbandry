using AnimalBehaviors;
using Meitou.Data.Gameplay.Ai;
using Meitou.Simulation;
using Meitou.Simulation.Bodies;
using Meitou.Simulation.Production;

namespace Husbandry.Training;

/// <summary>
/// One training animal at a check, for the building panel (<see cref="BuildingStatus"/>) and the drill (<see cref="TrainingDrills"/>): the animal,
/// the skill, its trainer's slot (-1 none) and whether the animal stands at the post for the lesson (<see cref="Trainers.LessonReach"/>).
/// </summary>
public readonly record struct Lesson(Worker Animal, TrainableSkill Skill, int Trainer, bool Ready);

/// <summary>
/// The Training Post (docs/husbandry.md "Training"): select animals, right-click the post and choose "Train: Fetch" (or another skill). A
/// training animal keeps to a pen round the post, as a hitched one does, until a trainer works the post; then it walks to its spot facing the
/// trainer (<see cref="Trainers.PupilSpot"/>) and learns the skill by the game hour only while both stand within 5 m of the post (<see cref="Trainers"/>), at the trainer's rate, halved without treats (<see cref="Treats"/>). A post takes one animal and one trainer at
/// a time. Serial steps only.
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
    /// One check: keeps each training animal in its pen and trains it for the game hours since the last check from its post's trainer; an animal
    /// that may not learn the skill (docs/husbandry.md "Supported animals") is sent off the job, and so is a second animal at a post that has one
    /// (<see cref="Choose"/>: one animal a post, kept in <paramref name="pupils"/> by post id). At each game hour mark the trainer feeds a treat, and
    /// an animal without a trainer says so. Returns the animals that trained or waited.
    /// </summary>
    public static List<Lesson> Run(World world, AnimalRegistry registry, AnimalSettings pens, RngPurpose purpose, double hours, string meat,
        Dictionary<string, CharacterId> pupils, Trainers trainers)
    {
        var table = world.Characters;
        var holders = Learners(world, registry);
        Choose(pupils, holders.Select(h => (h.Worker.Site.Id, table.IdOf(h.Worker.Slot))));

        var lessons = new List<Lesson>();
        bool hourMark = Math.Floor(world.GameHours) > Math.Floor(world.GameHours - hours);
        var working = Trainers.Working(world);
        foreach (var (w, skill) in holders)
        {
            if (pupils[w.Site.Id] != table.IdOf(w.Slot))
            {
                Crowded(world, w);
                continue;
            }

            if (Hitches.Down(w.Cold) || w.Cold.InCombat) continue;

            int trainer = working.TryGetValue(w.Site.Id, out var free) && free.TryDequeue(out int t) ? t : -1;
            if (trainer < 0) Pens.Keep(world, w.AsHitched, pens, purpose);

            bool ready = trainer >= 0 && Gather(world, w);
            lessons.Add(new Lesson(w, skill, trainer, ready));
            if (ready) Train(world, trainers, w, skill, trainer, hours, hourMark, meat);
            else if (trainer < 0 && hourMark) AnimalWork.Say(world, w.Slot, "Waiting for a trainer");
        }

        return lessons;
    }

    /// <summary>
    /// Brings the animal to the lesson: true when it stands within <see cref="Trainers.LessonReach"/> of the post, free of orders; otherwise an
    /// animal free to be sent walks to its spot (<see cref="Trainers.PupilSpot"/>). An animal the player sent elsewhere is left alone.
    /// </summary>
    public static bool Gather(World world, Worker w)
    {
        if (!AnimalWork.Free(world, w.Slot, w.Cold)) return false;
        if (AnimalWork.Away(world, w.Slot, w.Site.Position) <= Trainers.LessonReach) return true;

        AnimalWork.Walk(world, w.Slot, Trainers.PupilSpot(world, w.Site));
        return false;
    }

    /// <summary>
    /// The animals holding a training job with a known skill that they may learn, in slot order (one that may not is sent off the job).
    /// </summary>
    static List<(Worker Worker, TrainableSkill Skill)> Learners(World world, AnimalRegistry registry)
    {
        var learners = new List<(Worker, TrainableSkill)>();
        foreach (var w in AnimalWork.Holders(world, registry.TrainingTypes))
        {
            if (registry.SkillOf(w.Type) is not { } skill || Refused(world, registry, w, skill)) continue;

            learners.Add((w, skill));
        }

        return learners;
    }

    /// <summary>
    /// Picks each post's one animal: the one it had while that one still trains there, else the first in <paramref name="holders"/> (slot order);
    /// a post no animal trains at drops out.
    /// </summary>
    public static void Choose(Dictionary<string, CharacterId> pupils, IEnumerable<(string Post, CharacterId Animal)> holders)
    {
        var byPost = new Dictionary<string, List<CharacterId>>();
        foreach (var (post, animal) in holders)
        {
            if (!byPost.TryGetValue(post, out var animals)) byPost[post] = animals = [];
            animals.Add(animal);
        }

        foreach (string post in pupils.Keys.Where(p => !byPost.ContainsKey(p)).ToList()) pupils.Remove(post);
        foreach (var (post, animals) in byPost)
        {
            if (pupils.TryGetValue(post, out var kept) && animals.Contains(kept)) continue;

            pupils[post] = animals[0];
        }
    }

    /// <summary>
    /// Takes the post's training jobs off an animal sent to a post that already has one, telling the player.
    /// </summary>
    static void Crowded(World world, Worker w)
    {
        w.Cold.Jobs.RemoveAll(j => j.Site == w.Site.Id && j.Type == w.Type);
        AnimalWork.Say(world, w.Slot, "This Training Post already has an animal");
    }

    /// <summary>
    /// Trains the animal from the trainer for the hours, and the trainer's Animal Handling with it; at an hour mark the trainer feeds a treat from
    /// the post and, once the post has none left, goes to fetch more (<see cref="Trainers.Fetch"/>) or says none is near.
    /// </summary>
    static void Train(World world, Trainers trainers, Worker w, TrainableSkill skill, int trainer, double hours, bool hourMark, string meat)
    {
        var cold = world.Characters.Cold(trainer)!;
        bool treats = Treats.Have(world, w.Site, meat);
        float rate = Trainers.Rate(cold) * (treats ? 1 : Treats.Without);
        AnimalSkills.Gain(w.Cold, skill.Skill, rate * (float)hours);
        AnimalSkills.Gain(cold, Trainers.Handling, Trainers.HandlingPerHour * (float)hours);
        if (!hourMark) return;

        Treats.Feed(world, w.Site, meat);
        if (Treats.Have(world, w.Site, meat)) return;

        if (trainers.Fetch(world, trainer, w.Site)) AnimalWork.Say(world, trainer, "Fetching Raw Meat for treats");
        else AnimalWork.Say(world, trainer, "No Raw Meat for treats nearby: training at half speed");
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
