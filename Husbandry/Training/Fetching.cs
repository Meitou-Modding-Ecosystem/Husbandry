using System.Text;
using Meitou.Data;
using Meitou.Data.Fcs;
using Meitou.Data.Gameplay.Ai;
using Meitou.Data.Gameplay.Construction;
using Meitou.Simulation;
using Meitou.Simulation.Items;
using Meitou.Simulation.Population;
using Meitou.Simulation.Production;

namespace Husbandry.Training;

/// <summary>
/// Fetch (docs/husbandry.md "Training"): an animal with Fetch unlocked and a pack on, given the Fetch job on a storage, carries the goods of the
/// player's machines within <see cref="Range"/> of the storage that it takes (a mine's ore, a farm's crop) into it, in its pack, the nearest
/// machine first. It works only while idle and free of the player's orders, and learns Fetch with every load. Serial steps only.
/// </summary>
public sealed class Fetching(PopulationSystem? population)
{
    /// <summary>
    /// The job's task type (Husbandry keeps 4520 to 4529 for the work jobs).
    /// </summary>
    public const TaskType Type = (TaskType)4520;

    /// <summary>
    /// The job's name in the jobs list.
    /// </summary>
    public const string Name = "Fetching";

    /// <summary>
    /// How far from its storage the animal fetches from, in game units.
    /// </summary>
    public const float Range = 3000;

    /// <summary>
    /// Fetch gained for each load put in the storage.
    /// </summary>
    const float GainPerLoad = 0.5f;

    /// <summary>
    /// Walks to one target that may end short of it before the target is given up for <see cref="GiveUpHours"/>.
    /// </summary>
    const int WalkTries = 3;

    /// <summary>
    /// Game hours a target the animal could not reach is left alone.
    /// </summary>
    const double GiveUpHours = 1;

    /// <summary>
    /// The job set, for <see cref="AnimalWork.Holders"/>.
    /// </summary>
    static readonly HashSet<TaskType> Types = [Type];

    /// <summary>
    /// Each fetching animal's current walk (by portrait serial): the building it walks to, the walks so far, and until when a target it gave up
    /// on is skipped.
    /// </summary>
    readonly SortedDictionary<uint, (string Target, int Tries, double SkipUntil)> _walks = [];

    /// <summary>
    /// The item type of each record made, so a storage's limit can be checked before anything is made.
    /// </summary>
    readonly Dictionary<string, FcsRecordType> _types = [];

    /// <summary>
    /// The job as a storage offers it: on every storage building of the merged data.
    /// </summary>
    public static ModJobOffer Offer(GameDatabase db, IEnumerable<GameRecord> buildings) =>
        new(Type, [.. buildings.Where(b => MachineData.From(db, b) is { IsStorage: true }).Select(b => b.StringId)], HusbandryText.Get("Fetch for here"),
            ModJobTakers.Animals);

    /// <summary>
    /// One check of every fetching animal.
    /// </summary>
    public void Run(World world)
    {
        if (population is null) return;

        foreach (var w in AnimalWork.Holders(world, Types))
        {
            if (AnimalWork.Refused(world, w, AnimalSkills.Fetch) || !AnimalWork.Free(world, w.Slot, w.Cold)) continue;
            if (world.Machines.Find(w.Site.Id) is not { Storage: not null } store || !store.Data.IsStorage) continue;

            Step(world, w, store);
        }
    }

    /// <summary>
    /// One free animal: unloads at or walks to its storage while it carries what the storage takes, else takes or walks to the nearest goods.
    /// </summary>
    void Step(World world, Worker w, Machine store)
    {
        if (w.Cold.Inventory?.Backpack is not { Contents: { } pack })
        {
            w.Cold.Jobs.RemoveAll(j => j.Type == Type);
            AnimalWork.Say(world, w.Slot, "Needs a pack on to fetch");
            return;
        }

        var carried = pack.All().Where(i => store.Data.Accepts(i.Record, i.Type)).ToList();
        if (carried.Count > 0)
        {
            if (AnimalWork.Away(world, w.Slot, store.Position) <= AnimalWork.Reach) Unload(world, w, store, pack, carried);
            else Go(world, w, store);
            return;
        }

        if (Source(world, w, store) is not { } from) return;

        if (AnimalWork.Away(world, w.Slot, from.Position) <= AnimalWork.Reach) Take(world, w, from);
        else Go(world, w, from);
    }

    /// <summary>
    /// Puts the carried goods in the storage; what has no room stays in the pack.
    /// </summary>
    void Unload(World world, Worker w, Machine store, Inventory pack, List<ItemInstance> carried)
    {
        foreach (var item in carried)
        {
            pack.Remove(item);
            if (store.Storage!.Add(item)) continue;

            pack.Add(item);
            break;
        }

        _walks.Remove(Serial(w));
        world.Machines.Touch();
        AnimalSkills.Gain(w.Cold, AnimalSkills.Fetch, GainPerLoad);
    }

    /// <summary>
    /// Takes as much of a machine's output as the pack holds.
    /// </summary>
    void Take(World world, Worker w, Machine from)
    {
        var output = from.Output!;
        if (population!.Data.Items.Create(output.Recipe.Item, output.Count) is not { } made) return;

        int asked = made.Quantity;
        w.Cold.Inventory!.Add(made, mainShut: true);
        int moved = asked - made.Quantity;
        _walks.Remove(Serial(w));
        if (moved <= 0)
        {
            AnimalWork.Say(world, w.Slot, "Pack full");
            return;
        }

        output.Count -= moved;
        world.Machines.Touch();
    }

    /// <summary>
    /// The nearest machine of the player's within <see cref="Range"/> of the storage with goods the storage takes, not one the animal gave up on.
    /// </summary>
    Machine? Source(World world, Worker w, Machine store)
    {
        var skip = _walks.TryGetValue(Serial(w), out var walk) && world.GameHours < walk.SkipUntil ? walk.Target : null;
        return world.Machines.All
            .Where(m => m.Site != skip && Holds(world, m, store))
            .MinBy(m => AnimalWork.Away(world, w.Slot, m.Position));
    }

    /// <summary>
    /// Whether a machine is a finished one of the player's near the storage with output the storage takes.
    /// </summary>
    bool Holds(World world, Machine m, Machine store)
    {
        if (m.Data.IsStorage || m.Output is not { Count: > 0 } output) return false;
        if (System.Numerics.Vector3.Distance(m.Position, store.Position) > Range) return false;
        if (world.Buildings.Find(m.Site) is not { Complete: true } site || site.Owner != world.Player.Faction) return false;

        return TypeOf(output.Recipe.Item) is { } type && store.Data.Accepts(output.Recipe.Item, type);
    }

    /// <summary>
    /// An item record's type; null for a record that makes no item.
    /// </summary>
    FcsRecordType? TypeOf(string record)
    {
        if (_types.TryGetValue(record, out var known)) return known;
        if (population!.Data.Items.Create(record) is not { } unit) return null;

        _types[record] = unit.Type;
        return unit.Type;
    }

    /// <summary>
    /// Walks the animal to a building; after <see cref="WalkTries"/> walks to it that end short, it is skipped for <see cref="GiveUpHours"/>.
    /// </summary>
    void Go(World world, Worker w, Machine to)
    {
        uint serial = Serial(w);
        var walk = _walks.TryGetValue(serial, out var known) && known.Target == to.Site ? known : (Target: to.Site, Tries: 0, SkipUntil: 0d);
        if (walk.Tries >= WalkTries)
        {
            _walks[serial] = (to.Site, 0, world.GameHours + GiveUpHours);
            return;
        }

        _walks[serial] = (walk.Target, walk.Tries + 1, walk.SkipUntil);
        AnimalWork.Walk(world, w.Slot, to.Position);
    }

    /// <summary>
    /// The key of an animal's walk.
    /// </summary>
    static uint Serial(Worker w) => w.Cold.PortraitSerial ?? (uint)w.Slot;

    /// <summary>
    /// Adds the walks to the state hash.
    /// </summary>
    public void Hash(ref StateHasher hasher)
    {
        hasher.Add(_walks.Count);
        foreach (var (serial, walk) in _walks)
        {
            hasher.Add(serial);
            hasher.Add(Rng.StableHash(walk.Target));
            hasher.Add(walk.Tries);
            hasher.Add(walk.SkipUntil);
        }
    }

    /// <summary>
    /// The debug view's line.
    /// </summary>
    public string Describe() => _walks.Count == 0 ? "" : new StringBuilder().AppendLine($"Fetching: {_walks.Count} animals on a walk").ToString();
}
