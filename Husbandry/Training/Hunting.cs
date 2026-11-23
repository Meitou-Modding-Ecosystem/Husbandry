using System.Numerics;
using Meitou.Data.Fcs;
using Meitou.Data.Gameplay.Ai;
using Meitou.Simulation;
using Meitou.Simulation.Population;
using Meitou.Simulation.Production;

namespace Husbandry.Training;

/// <summary>
/// The Bone Pile (docs/husbandry.md "Training"): an animal with Hunt unlocked, given the Hunt job on a pile, rests in a pen round it, then runs
/// out to a spot up to <see cref="Range"/> away and back, and drops raw meat into the pile (one piece, and one more for every 25 levels of Hunt).
/// It rests 8 game hours between hunts at Hunt 10, down to 2 at 100. Serial steps only.
/// </summary>
public sealed class Hunting(PopulationSystem? population, Action<string> log)
{
    /// <summary>
    /// The job's task type.
    /// </summary>
    public const TaskType Type = (TaskType)4522;

    /// <summary>
    /// The job's name in the jobs list.
    /// </summary>
    public const string Name = "Hunting";

    /// <summary>
    /// How far from its pile a hunt goes at most, in game units (1200 m), and at least.
    /// </summary>
    public const float Range = 12000, MinRange = 1500;

    /// <summary>
    /// The meat: the game's "Raw Meat" ITEM, found by name.
    /// </summary>
    public const string MeatName = "Raw Meat";

    /// <summary>
    /// Game hours a run out or back may take before the animal turns round or is let rest.
    /// </summary>
    const double LegHours = 3;

    /// <summary>
    /// Hunt gained for each hunt brought home.
    /// </summary>
    const float GainPerHunt = 2;

    /// <summary>
    /// The job set, for <see cref="AnimalWork.Holders"/>.
    /// </summary>
    static readonly HashSet<TaskType> Types = [Type];

    /// <summary>
    /// A hunt's leg: resting at the pile, running out, or coming back.
    /// </summary>
    enum Leg
    {
        Resting,
        Out,
        Back,
    }

    /// <summary>
    /// Each hunting animal's leg and when it ends (game hours), by portrait serial.
    /// </summary>
    readonly SortedDictionary<uint, (Leg Leg, double Until)> _hunts = [];

    /// <summary>
    /// The meat record, found once; empty when the game has none.
    /// </summary>
    string? _meat;

    /// <summary>
    /// The job as the pile offers it to animals.
    /// </summary>
    public static ModJobOffer Offer => new(Type, [HusbandryData.BonePile], "Hunt from here", ModJobTakers.Animals);

    /// <summary>
    /// One check of every hunting animal.
    /// </summary>
    public void Run(World world, HusbandrySettings settings, RngPurpose purpose)
    {
        if (population is null) return;

        foreach (var w in AnimalWork.Holders(world, Types))
        {
            if (AnimalWork.Refused(world, w, AnimalSkills.Hunt)) continue;

            uint serial = w.Cold.PortraitSerial ?? (uint)w.Slot;
            var hunt = _hunts.GetValueOrDefault(serial, (Leg.Resting, world.GameHours + RestHours(w)));
            _hunts[serial] = Step(world, w, hunt, settings, purpose);
        }
    }

    /// <summary>
    /// One animal's leg: rests until its time, runs out, turns back when it arrives or its time is up, and drops its meat at the pile.
    /// </summary>
    (Leg, double) Step(World world, Worker w, (Leg Leg, double Until) hunt, HusbandrySettings settings, RngPurpose purpose)
    {
        bool free = AnimalWork.Free(world, w.Slot, w.Cold);
        bool due = world.GameHours >= hunt.Until;
        switch (hunt.Leg)
        {
            case Leg.Resting when due && free:
                return Out(world, w, purpose) ?? (Leg.Resting, world.GameHours + 1);
            case Leg.Resting:
                Pens.Keep(world, w.AsHitched, settings, purpose);
                return hunt;
            case Leg.Out when free || due:
                AnimalWork.Walk(world, w.Slot, w.Site.Position);
                return (Leg.Back, world.GameHours + LegHours);
            case Leg.Back when free && AnimalWork.Away(world, w.Slot, w.Site.Position) <= AnimalWork.Reach * 1.5f:
            case Leg.Back when due:
                Home(world, w);
                return (Leg.Resting, world.GameHours + RestHours(w));
            case Leg.Back when free:
                AnimalWork.Walk(world, w.Slot, w.Site.Position);
                return hunt;
            default:
                return hunt;
        }
    }

    /// <summary>
    /// Sends the animal out to a walkable spot; null when none was found.
    /// </summary>
    (Leg, double)? Out(World world, Worker w, RngPurpose purpose)
    {
        ulong roll = Rng.Hash(world.Seed, w.Cold.PortraitSerial ?? (ulong)w.Slot, purpose, (ulong)world.Tick);
        if (AnimalWork.Spot(world, w.Site.Position, MinRange, Range, roll) is not { } goal) return null;

        AnimalWork.Walk(world, w.Slot, goal);
        return (Leg.Out, world.GameHours + LegHours);
    }

    /// <summary>
    /// Drops the hunt's meat into the pile (on the ground by it when the pile is full) and trains Hunt; only for an animal that made it home.
    /// </summary>
    void Home(World world, Worker w)
    {
        if (AnimalWork.Away(world, w.Slot, w.Site.Position) > AnimalWork.Reach * 1.5f) return;
        int pieces = 1 + (int)(AnimalSkills.Level(w.Cold, AnimalSkills.Hunt) / 25);
        if (Meat() is not { Length: > 0 } meat || population!.Data.Items.Create(meat, pieces) is not { } kill) return;

        var pile = world.Machines.Find(w.Site.Id)?.Storage;
        if (pile is null || !pile.Add(kill)) world.GroundItems.Drop(kill, w.Site.Position + new Vector3(0, 5, 0), 0);

        world.Machines.Touch();
        AnimalWork.Say(world, w.Slot, "Brought back meat");
        AnimalSkills.Gain(w.Cold, AnimalSkills.Hunt, GainPerHunt);
    }

    /// <summary>
    /// The game hours an animal rests between hunts: 8 at Hunt 10, down to 2 at 100.
    /// </summary>
    static double RestHours(Worker w)
    {
        float level = Math.Clamp(AnimalSkills.Level(w.Cold, AnimalSkills.Hunt), AnimalSkills.Unlocked, 100);
        return 8 - 6 * (level - AnimalSkills.Unlocked) / (100 - AnimalSkills.Unlocked);
    }

    /// <summary>
    /// The meat record, found by name once.
    /// </summary>
    string Meat()
    {
        if (_meat is not null) return _meat;

        _meat = population!.Data.Db.OfType(FcsRecordType.Item)
            .Where(r => r.Name == MeatName)
            .Select(r => r.StringId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .FirstOrDefault() ?? "";
        if (_meat.Length == 0) log($"husbandry: the game has no '{MeatName}' item, so hunting animals bring nothing back");
        return _meat;
    }

    /// <summary>
    /// Adds the hunts to the state hash.
    /// </summary>
    public void Hash(ref StateHasher hasher)
    {
        hasher.Add(_hunts.Count);
        foreach (var (serial, hunt) in _hunts)
        {
            hasher.Add(serial);
            hasher.Add((int)hunt.Leg);
            hasher.Add(hunt.Until);
        }
    }
}
