using System.Numerics;
using AnimalBehaviors;
using Meitou.Data.Gameplay.Ai;
using Meitou.Modding;
using Meitou.Simulation;
using Meitou.Simulation.Ai;
using Meitou.Simulation.Bodies;
using Meitou.Simulation.Construction;
using Meitou.Simulation.Factions;
using Meitou.Simulation.Movement;
using Meitou.Simulation.Production;

namespace Husbandry.Training;

/// <summary>
/// The Training Post's trainers (docs/husbandry.md "Training"): a person given the post's Train animals job walks to the front of the post and
/// works there while an animal of the player's holds a training job on it, one trainer a post; an animal learns only from a working trainer, at
/// a rate set by the trainer's Animal Handling (<see cref="Rate"/>). The job's contributor to the chooser (<see cref="IJobSource"/>) and the serial
/// step that turns an arrived walk into work. The trainer keeps the post's treats stocked (<see cref="Fetch"/>), Raw Meat by the record id
/// <paramref name="meat"/> gives. Nothing of its own is saved: the engine saves the job, and a loaded trainer walks to the post again.
/// </summary>
public sealed class Trainers(AnimalRegistry registry, Func<string> meat) : IJobSource
{
    #region Job

    /// <summary>
    /// The job's task type (in Husbandry's number block).
    /// </summary>
    public const TaskType Type = (TaskType)4523;

    /// <summary>
    /// The job's name in the jobs list.
    /// </summary>
    public const string Name = "Training animals";

    /// <summary>
    /// Where the trainer stands: this far in front of the post (its local +Z), in game units.
    /// </summary>
    const float StandOff = 30;

    /// <summary>
    /// The job as the post offers it to people.
    /// </summary>
    public static ModJobOffer Offer => new(Type, [HusbandryData.TrainingPost], HusbandryText.Get("Train animals here"), ModJobTakers.People);

    #endregion

    #region Skill

    /// <summary>
    /// Animal Handling: a person's skill at training animals, from the game's start level 1; it sets <see cref="Rate"/>.
    /// </summary>
    public static readonly ModSkill Handling = new("husbandry.handling", HusbandryText.Get("Animal Handling"), SkillColumn.Sciences)
    {
        Description = HusbandryText.Get("How fast this person trains animals at a Training Post: an animal learns four times as fast from a trainer"
            + " at 50 as from one at 1."),
        Training = [HusbandryText.Get("A Training Post"), HusbandryText.Get("Training animals")],
    };

    /// <summary>
    /// An animal's skill gained per game hour from a trainer at Animal Handling 1 and at <see cref="MasterLevel"/> or more, with treats: about two
    /// game days, and about half a day, from 0 to <see cref="AnimalSkills.Unlocked"/>.
    /// </summary>
    public const float NoviceRate = 0.2f, MasterRate = 0.8f;

    /// <summary>
    /// The Animal Handling level from which a trainer trains at <see cref="MasterRate"/>.
    /// </summary>
    public const float MasterLevel = 50;

    /// <summary>
    /// Animal Handling a trainer gains per game hour of training an animal.
    /// </summary>
    public const float HandlingPerHour = 0.5f;

    /// <summary>
    /// An animal's skill gained per game hour from this trainer, with treats: <see cref="NoviceRate"/> at Animal Handling 1, rising in a line to
    /// <see cref="MasterRate"/> at <see cref="MasterLevel"/>.
    /// </summary>
    public static float Rate(CharacterCold trainer)
    {
        float level = AnimalSkills.Level(trainer, Handling);
        float t = Math.Clamp((level - 1) / (MasterLevel - 1), 0, 1);
        return NoviceRate + (MasterRate - NoviceRate) * t;
    }

    #endregion

    #region Chooser

    /// <summary>
    /// Runs while the character works or walks to this job's post and an animal trains there; else whether an animal trains there.
    /// </summary>
    public DecisionCheck CanRun(World world, int slot, int k)
    {
        if (PostOf(world, slot, k) is not { } post || !HasPupils(world, post)) return DecisionCheck.No(null);
        if (Taken(world, slot, post)) return DecisionCheck.No(HusbandryText.Get("Another trainer works this post"));

        return OnJob(world, slot, k) ? DecisionCheck.Runs : DecisionCheck.Run;
    }

    /// <summary>
    /// Sends the character to the front of the post, or sets it to work when it stands there already.
    /// </summary>
    public void Start(World world, int slot, int k)
    {
        if (PostOf(world, slot, k) is not { } post) return;

        if (At(world, slot, post)) Work(world, slot, k, post);
        else TaskStart.Start(world, slot, new TaskRequest(CharacterTask.GoTo, Type, TaskSource.Job) { Point = StandAt(world, post), Site = post.Id, Job = k });
    }

    /// <summary>
    /// No animal trains at the post any more: the trainer stands.
    /// </summary>
    public void Stop(World world, int slot, int k)
    {
        if (OnJob(world, slot, k)) TaskStart.Stand(world.Characters, slot);
    }

    #endregion

    #region Stepping

    /// <summary>
    /// The serial step: a trainer whose walk to the post has ended works there, or stands and chooses again when the walk ended short; one whose
    /// walk to a storage has ended takes Raw Meat there and walks back (<see cref="Restock"/>).
    /// </summary>
    public void Step(World world)
    {
        var table = world.Characters;
        for (int s = 0; s < table.HighWater; s++)
        {
            if (!table.Next[s].Alive || table.Cold(s) is not { Running: { Source: TaskSource.Job, Row: Type, Task: CharacterTask.GoTo } run } cold) continue;
            if ((CharacterTask)table.Next[s].Task == CharacterTask.GoTo || run.Site is not { } id || world.Buildings.Find(id) is not { } post) continue;

            if (post.Record != HusbandryData.TrainingPost)
            {
                Restock(world, s, run.Job, post);
                continue;
            }

            if (At(world, s, post))
            {
                Work(world, s, run.Job, post);
                continue;
            }

            TaskStart.Stand(table, s);
            DecisionSystem.Request(cold);
        }
    }

    /// <summary>
    /// The trainer at work at each post, by post id: the first in slot order of the people of the player's standing at the post on its job, up and
    /// out of a fight (a post takes one trainer).
    /// </summary>
    public static Dictionary<string, Queue<int>> Working(World world)
    {
        var posts = new Dictionary<string, Queue<int>>();
        var table = world.Characters;
        for (int s = 0; s < table.HighWater; s++)
        {
            if (!table.Next[s].Alive || table.Cold(s) is not { IsPlayer: true, DiedTick: < 0 } cold || !Works(world, s, cold)) continue;

            string site = cold.Running!.Site!;
            if (!posts.TryGetValue(site, out var queue)) posts[site] = queue = new Queue<int>();
            if (queue.Count == 0) queue.Enqueue(s);
        }

        return posts;
    }

    /// <summary>
    /// Whether a character stands working the job at its post.
    /// </summary>
    static bool Works(World world, int slot, CharacterCold cold)
    {
        if (cold.Running is not { Source: TaskSource.Job, Row: Type, Task: CharacterTask.Idle, Site: { } id }) return false;
        if (SquadRegistry.IsAnimal(cold) || Hitches.Down(cold) || cold.InCombat) return false;

        return world.Buildings.Find(id) is { } post && At(world, slot, post);
    }

    /// <summary>
    /// Sets the character working at the post on job <paramref name="k"/>'s row, first putting the Raw Meat it carries into the post's storage.
    /// </summary>
    void Work(World world, int slot, int k, BuildSite post)
    {
        Stock(world, slot, post);
        TaskStart.Start(world, slot, new TaskRequest(CharacterTask.Idle, Type, TaskSource.Job) { Site = post.Id, Job = k });
    }

    #endregion

    #region Treats

    /// <summary>
    /// How near a storage the trainer must have got to take Raw Meat from it, in game units.
    /// </summary>
    public const float StorageReach = 150;

    /// <summary>
    /// Sends a trainer working its post to the nearest storage of the player's holding Raw Meat (<see cref="Treats.Source"/>) when the post has
    /// room for more; false when none is near enough or the post is full.
    /// </summary>
    public bool Fetch(World world, int slot, BuildSite post)
    {
        string id = meat();
        if (Treats.Room(world, post, id) <= 0 || Treats.Source(world, post, id) is not { } source) return false;
        if (world.Characters.Cold(slot)?.Running is not { Source: TaskSource.Job, Row: Type } run) return false;

        var point = MovementSystem.FloorApproach(world, source.Position);
        TaskStart.Start(world, slot, new TaskRequest(CharacterTask.GoTo, Type, TaskSource.Job) { Point = point, Site = source.Site, Job = run.Job });
        return true;
    }

    /// <summary>
    /// A trainer at the end of its walk to a storage: takes as much Raw Meat as the post has room for (<see cref="Treats.Room"/>) when it got near,
    /// and walks back to the post (or stands and chooses again when its job is gone).
    /// </summary>
    void Restock(World world, int slot, int k, BuildSite storage)
    {
        var cold = world.Characters.Cold(slot)!;
        if (PostOf(world, slot, k) is not { } post)
        {
            TaskStart.Stand(world.Characters, slot);
            DecisionSystem.Request(cold);
            return;
        }

        bool near = AnimalWork.Away(world, slot, storage.Position) <= StorageReach;
        if (near && world.Machines.Find(storage.Id)?.Storage is { } from && cold.Inventory is { } bag)
        {
            string id = meat();
            if (Treats.Move(from, bag, id, Treats.Room(world, post, id) - Treats.Count(bag, id)) > 0) world.Machines.Touch();
        }

        Start(world, slot, k);
    }

    /// <summary>
    /// Puts the Raw Meat the trainer carries into the post's storage, as much as it has room for.
    /// </summary>
    void Stock(World world, int slot, BuildSite post)
    {
        string id = meat();
        if (id.Length == 0 || world.Characters.Cold(slot)?.Inventory is not { } bag || Treats.Store(world, post) is not { } store) return;

        if (Treats.Move(bag, store, id, Treats.Room(world, post, id)) > 0) world.Machines.Touch();
    }

    #endregion

    #region Reading

    /// <summary>
    /// The finished player Training Post of the character's job <paramref name="k"/> when that job is this one; else null.
    /// </summary>
    static BuildSite? PostOf(World world, int slot, int k)
    {
        if (world.Characters.Cold(slot)?.Jobs is not { } jobs || (uint)k >= (uint)jobs.Count) return null;
        if (jobs[k] is not { Type: Type, Site: { } id }) return null;

        return world.Buildings.Find(id) is { Complete: true } post && post.Owner == world.Player.Faction ? post : null;
    }

    /// <summary>
    /// Whether the character's task is this job <paramref name="k"/>'s walk or work.
    /// </summary>
    static bool OnJob(World world, int slot, int k) =>
        world.Characters.Cold(slot)?.Running is { Source: TaskSource.Job, Row: Type } run && run.Job == k;

    /// <summary>
    /// Whether a living animal of the player's holds a training job on the post (a read: <see cref="AnimalWork.Holders"/> changes job lists).
    /// </summary>
    bool HasPupils(World world, BuildSite post)
    {
        var table = world.Characters;
        for (int s = 0; s < table.HighWater; s++)
        {
            if (!table.Next[s].Alive || table.Cold(s) is not { IsPlayer: true, DiedTick: < 0 } cold || !SquadRegistry.IsAnimal(cold)) continue;
            if (cold.Jobs.Any(j => j.Site == post.Id && registry.TrainingTypes.Contains(j.Type))) return true;
        }

        return false;
    }

    /// <summary>
    /// Whether another living character walks to the post on this job, works it or fetches treats for it: a post takes one trainer.
    /// </summary>
    static bool Taken(World world, int slot, BuildSite post)
    {
        var table = world.Characters;
        for (int s = 0; s < table.HighWater; s++)
        {
            if (s == slot || !table.Next[s].Alive || table.Cold(s) is not { DiedTick: < 0 } cold) continue;
            if (cold.Running is not { Source: TaskSource.Job, Row: Type } run) continue;
            if (run.Site == post.Id || (uint)run.Job < (uint)cold.Jobs.Count && cold.Jobs[run.Job].Site == post.Id) return true;
        }

        return false;
    }

    /// <summary>
    /// Whether the character stands by the post and has stopped walking.
    /// </summary>
    static bool At(World world, int slot, BuildSite post) =>
        AnimalWork.Away(world, slot, post.Position) <= AnimalWork.Reach && (CharacterTask)world.Characters.Next[slot].Task != CharacterTask.GoTo;

    /// <summary>
    /// The trainer's spot: <see cref="StandOff"/> in front of the post, on the ground.
    /// </summary>
    static Vector3 StandAt(World world, BuildSite post)
    {
        var at = post.Position + Vector3.Transform(new Vector3(0, 0, StandOff), post.Rotation);
        return world.Walkability.IsWalkable(at.X, at.Z) ? at with { Y = world.Walkability.GroundHeight(at.X, at.Z) } : post.Position;
    }

    #endregion
}
