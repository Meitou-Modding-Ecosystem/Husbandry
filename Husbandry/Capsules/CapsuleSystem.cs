using System.Globalization;
using System.Numerics;
using System.Text;
using Husbandry.Training;
using Meitou.Simulation;
using Meitou.Simulation.Factions;
using Meitou.Simulation.Items;
using Meitou.Simulation.Population;

namespace Husbandry.Capsules;

/// <summary>
/// Beast Capsules (docs/husbandry.md "Beast Capsules"): right-click an empty capsule in a character's inventory while standing next to a knocked-out
/// animal, or one of the player's own, and the animal goes into the capsule; right-click a full one and it comes out beside the character as one
/// of the player's, with the same name, sex, stats and skills (fully healed; what it carried is dropped when it goes in). A full capsule's
/// <see cref="ItemInstance.ModTag"/> holds its key; the animals are saved in the mod's sidecar. The clicks only queue; the serial step does the
/// work. Serial steps only.
/// </summary>
public sealed class CapsuleSystem(PopulationSystem? population, Sexes sexes, AnimalRegistry registry, Action<string> log)
{
    /// <summary>
    /// The start of a full capsule's tag; its key follows.
    /// </summary>
    public const string TagPrefix = "husbandry.capsule:";

    /// <summary>
    /// How near the character an animal must be to be caught, in game units.
    /// </summary>
    public const float CatchReach = 40;

    /// <summary>
    /// How far from the character a summoned animal comes out.
    /// </summary>
    const float SummonDistance = 15;

    /// <summary>
    /// The kept animals by key.
    /// </summary>
    readonly SortedDictionary<int, StoredBeast> _stored = [];

    /// <summary>
    /// The clicks waiting for the serial step: the user, the capsule and the inventory holding it.
    /// </summary>
    readonly List<(CharacterId User, ItemInstance Capsule, Inventory Inventory)> _clicks = [];

    /// <summary>
    /// The next key.
    /// </summary>
    int _nextKey = 1;

    /// <summary>
    /// A right click on a capsule: queued for the serial step; always used.
    /// </summary>
    public bool Use(ItemUse use)
    {
        _clicks.Add((use.User, use.Item, use.Inventory));
        return true;
    }

    /// <summary>
    /// The serial step: catches or summons for each click queued.
    /// </summary>
    public void Run(World world)
    {
        foreach (var (user, capsule, inventory) in _clicks)
        {
            if (!world.Characters.TryResolveNext(user, out int slot) || !inventory.All().Contains(capsule)) continue;

            if (Key(capsule) is { } key) Summon(world, slot, capsule, key);
            else Catch(world, slot, capsule, inventory);
        }

        _clicks.Clear();
    }

    /// <summary>
    /// Whether a capsule holds an animal.
    /// </summary>
    public static bool Full(ItemInstance capsule) => Key(capsule) is not null;

    /// <summary>
    /// The key a full capsule holds, or null for an empty one.
    /// </summary>
    static int? Key(ItemInstance capsule) =>
        capsule.ModTag.StartsWith(TagPrefix, StringComparison.Ordinal)
        && int.TryParse(capsule.ModTag.AsSpan(TagPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int key)
            ? key
            : null;

    #region Catching

    /// <summary>
    /// Puts the nearest catchable animal in one capsule of the stack.
    /// </summary>
    void Catch(World world, int user, ItemInstance capsule, Inventory inventory)
    {
        if (Target(world, user) is not { } target)
        {
            AnimalWork.Say(world, user, "Nothing to catch: knock a beast out and stand next to it");
            return;
        }

        Put(world, user, capsule, inventory, target);
    }

    /// <summary>
    /// Catches the beast a thrown capsule hit (<see cref="CapsuleThrows"/>): as a right click beside it would.
    /// </summary>
    public void CatchThrown(World world, int user, ItemInstance capsule, Inventory inventory, int target) => Put(world, user, capsule, inventory, target);

    /// <summary>
    /// Puts an animal in one capsule of the stack and takes it out of the world.
    /// </summary>
    void Put(World world, int user, ItemInstance capsule, Inventory inventory, int target)
    {
        var table = world.Characters;
        var cold = table.Cold(target)!;
        bool female = sexes.Female(cold);
        int key = _nextKey++;
        _stored[key] = StoredBeast.Of(cold, female);
        DropCarried(world, target, cold);
        Fill(world, user, capsule, inventory, key);
        if (cold.PortraitSerial is { } serial) sexes.Assigned.Remove(serial);

        AnimalWork.Say(world, user, $"Caught {cold.Name}!");
        log($"husbandry: {table.Cold(user)!.Name} caught {cold.Name} ({cold.RecordId}) in a capsule");
        var id = table.IdOf(target);
        world.Uniques.Left(id);
        table.Remove(id);
    }

    /// <summary>
    /// The nearest animal within reach that may be caught (<see cref="Catchable"/>).
    /// </summary>
    int? Target(World world, int user)
    {
        var table = world.Characters;
        var at = table.Next[user].Position;
        int? best = null;
        float bestDistance = CatchReach;
        for (int s = 0; s < table.HighWater; s++)
        {
            if (!table.Next[s].Alive || table.Cold(s) is not { Medical.Dead: false } c || !Catchable(c)) continue;

            float d = Vector3.Distance(table.Next[s].Position, at);
            if (d > bestDistance) continue;

            best = s;
            bestDistance = d;
        }

        return best;
    }

    /// <summary>
    /// Whether an animal may be caught now: one of the player's whose race supports summoning, or a knocked-out one whose race supports taming
    /// (docs/husbandry.md "Supported animals"); never one carried.
    /// </summary>
    public bool Catchable(CharacterCold c)
    {
        if (!c.CarriedBy.IsNone) return false;
        if (c.IsPlayer) return registry.Supports(c, AnimalSupport.Summoning);

        return c.Medical is { Unconscious: true } && registry.Supports(c, AnimalSupport.Taming);
    }

    /// <summary>
    /// Drops what the animal carries (its pack included) on the ground where it lies.
    /// </summary>
    static void DropCarried(World world, int slot, CharacterCold cold)
    {
        if (cold.Inventory is not { } inv) return;

        var at = world.Characters.Next[slot].Position;
        foreach (var item in inv.All().ToList())
        {
            inv.Remove(item);
            world.GroundItems.Drop(item, at, 0);
        }
    }

    /// <summary>
    /// Marks one capsule of the stack full: the capsule itself when alone, else a new one split off (on the ground when the inventory has no room).
    /// </summary>
    void Fill(World world, int user, ItemInstance capsule, Inventory inventory, int key)
    {
        if (capsule.Quantity <= 1)
        {
            capsule.ModTag = TagPrefix + key.ToString(CultureInfo.InvariantCulture);
            return;
        }

        if (population?.Data.Items.Create(capsule.Record) is not { } full) return;

        capsule.Quantity -= 1;
        full.ModTag = TagPrefix + key.ToString(CultureInfo.InvariantCulture);
        if (!inventory.Add(full)) world.GroundItems.Drop(full, world.Characters.Next[user].Position, 0);
    }

    #endregion

    #region Summoning

    /// <summary>
    /// Brings a capsule's animal out beside the user as one of the player's, in the user's squad; the capsule is empty again.
    /// </summary>
    void Summon(World world, int user, ItemInstance capsule, int key)
    {
        var table = world.Characters;
        if (!_stored.TryGetValue(key, out var beast))
        {
            capsule.ModTag = "";
            return;
        }

        var at = table.Next[user].Position;
        var spot = new Vector2(at.X + SummonDistance, at.Z);
        var id = population?.SpawnIntoPlayerSquad(world, table.Cold(user)!.SquadId, beast.Record, spot, beast.Age) ?? CharacterId.None;
        if (id.IsNone || !table.TryResolveNext(id, out int slot))
        {
            AnimalWork.Say(world, user, "It won't come out here (squad full, or no room)");
            return;
        }

        var cold = table.Cold(slot)!;
        beast.Restore(cold);
        if (cold.PortraitSerial is { } serial) sexes.Assigned[serial] = beast.Female;
        _stored.Remove(key);
        capsule.ModTag = "";
        AnimalWork.Say(world, slot, $"{beast.Name}, come out!");
    }

    #endregion

    #region Saving and the debug view

    /// <summary>
    /// Writes the kept animals.
    /// </summary>
    public void Write(BinaryWriter w)
    {
        w.Write(_nextKey);
        w.Write(_stored.Count);
        foreach (var (key, beast) in _stored)
        {
            w.Write(key);
            beast.Write(w);
        }
    }

    /// <summary>
    /// Reads them back.
    /// </summary>
    public void Read(BinaryReader r)
    {
        _stored.Clear();
        _nextKey = r.ReadInt32();
        int count = r.ReadInt32();
        for (int k = 0; k < count; k++) _stored[r.ReadInt32()] = StoredBeast.Read(r);
    }

    /// <summary>
    /// Adds them to the state hash.
    /// </summary>
    public void Hash(ref StateHasher hasher)
    {
        hasher.Add(_nextKey);
        hasher.Add(_stored.Count);
        hasher.Add(_clicks.Count);
        foreach (var (key, beast) in _stored)
        {
            hasher.Add(key);
            beast.Hash(ref hasher);
        }
    }

    /// <summary>
    /// The debug view: each kept animal.
    /// </summary>
    public string Describe()
    {
        if (_stored.Count == 0) return "No animals in capsules.";

        var text = new StringBuilder();
        foreach (var (key, beast) in _stored)
        {
            string sex = beast.Female ? "female" : "male";
            text.AppendLine($"Capsule {key}: {beast.Name} ({sex}, age {beast.Age:0.00})");
        }

        return text.ToString();
    }

    #endregion
}
