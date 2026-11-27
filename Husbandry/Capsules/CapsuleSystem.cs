using System.Collections.Frozen;
using System.Globalization;
using System.Numerics;
using System.Text;
using AnimalBehaviors;
using Husbandry.Training;
using Meitou.Simulation;
using Meitou.Simulation.Items;
using Meitou.Simulation.Population;

namespace Husbandry.Capsules;

/// <summary>
/// What a click on a filled capsule does (<see cref="CapsuleSystem.Decide"/>).
/// </summary>
public enum CapsuleAction
{
    /// <summary>
    /// The capsule's beast is gone for good: it becomes an empty capsule.
    /// </summary>
    Empty,
    /// <summary>
    /// The capsule was used too recently (a summon or recall is still settling, or the game is paused): nothing happens.
    /// </summary>
    Wait,
    /// <summary>
    /// The beast is inside: it comes out.
    /// </summary>
    Summon,
    /// <summary>
    /// The beast is out: it goes back in.
    /// </summary>
    Recall,
}

/// <summary>
/// A beast bound to a filled capsule: the beast as last stored, the lasting id of its body while it is out (none while inside) and the tick the
/// capsule was last used.
/// </summary>
public sealed class BoundBeast(StoredBeast beast)
{
    /// <summary>
    /// The beast as it was when it last went in.
    /// </summary>
    public StoredBeast Beast { get; set; } = beast;

    /// <summary>
    /// Its body's lasting id while it is out; none while it is inside.
    /// </summary>
    public LastingId Out { get; set; }

    /// <summary>
    /// The tick the capsule last summoned or recalled it; long.MinValue never.
    /// </summary>
    public long UsedAt { get; set; } = long.MinValue;
}

/// <summary>
/// What a capsule's tooltip shows of its beast (<see cref="CapsuleTooltip"/>), published for the host's thread.
/// </summary>
public sealed record BeastView(StoredBeast Beast, bool Out);

/// <summary>
/// Beast Capsules (docs/husbandry.md "Beast Capsules"). An empty capsule catches a beast (thrown, <see cref="CapsuleThrows"/>, or right-clicked
/// next to it): the beast leaves the world and a filled capsule bound to it drops where it lay. Right-clicking the filled capsule calls the beast
/// out as one of the player's, with the same name, sex, stats, skills and traits; right-clicking it again calls that same beast back in. A capsule
/// acts at most once per <see cref="SettleTicks"/>, so a second click before the first has settled (or any while paused) does nothing. A filled
/// capsule's <see cref="ItemInstance.ModTag"/> holds its key; the beasts are saved in the mod's sidecar. Item uses run in the player system's
/// serial step; the rest in serial steps.
/// </summary>
public sealed class CapsuleSystem(PopulationSystem? population, Sexes sexes, AnimalRegistry registry, Action<string> log)
{
    /// <summary>
    /// The start of a filled capsule's tag; its key follows.
    /// </summary>
    public const string TagPrefix = "husbandry.capsule:";

    /// <summary>
    /// How near the character an animal must be to be caught by a right click, in game units.
    /// </summary>
    public const float CatchReach = 40;

    /// <summary>
    /// How near the character a beast that is out must be to be called back, in game units (30 m).
    /// </summary>
    public const float RecallReach = 300;

    /// <summary>
    /// How far from the character a summoned animal comes out.
    /// </summary>
    const float SummonDistance = 15;

    /// <summary>
    /// How long after a summon or recall the capsule does nothing, in ticks (one second): the beast is still appearing or leaving.
    /// </summary>
    public const int SettleTicks = 30;

    /// <summary>
    /// The beasts by key.
    /// </summary>
    readonly SortedDictionary<int, BoundBeast> _bound = [];

    /// <summary>
    /// The bodies that went into a capsule this tick, taken out of the world at the slow world (<see cref="Settle"/>): an item use runs in the
    /// inputs phase, where the engine refuses removals (<c>CharacterTable.Remove</c> throws there).
    /// </summary>
    readonly List<(CharacterId Id, CharacterCold Cold)> _leaving = [];

    /// <summary>
    /// The beasts by key as of the last change, for the tooltips (host's thread).
    /// </summary>
    volatile FrozenDictionary<int, BeastView> _views = FrozenDictionary<int, BeastView>.Empty;

    /// <summary>
    /// The next key.
    /// </summary>
    int _nextKey = 1;

    /// <summary>
    /// Whether a capsule holds a beast: a filled capsule, or an old save's capsule carrying a key.
    /// </summary>
    public static bool Full(ItemInstance capsule) => capsule.Record == HusbandryData.FilledCapsule || Key(capsule.ModTag) is not null;

    /// <summary>
    /// The key a tag holds, or null.
    /// </summary>
    public static int? Key(string tag) =>
        tag.StartsWith(TagPrefix, StringComparison.Ordinal)
        && int.TryParse(tag.AsSpan(TagPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int key)
            ? key
            : null;

    /// <summary>
    /// The tooltip's view of a capsule's beast by its tag, or null (host's thread).
    /// </summary>
    public BeastView? View(string tag) => Key(tag) is { } key ? _views.GetValueOrDefault(key) : null;

    /// <summary>
    /// What a click on a filled capsule does now: nothing within <see cref="SettleTicks"/> of its last use, else a summon while the beast is inside,
    /// a recall while it is out, and an empty capsule when no beast is bound to it.
    /// </summary>
    public static CapsuleAction Decide(BoundBeast? bound, long tick)
    {
        if (bound is null) return CapsuleAction.Empty;
        if (bound.UsedAt != long.MinValue && tick - bound.UsedAt < SettleTicks) return CapsuleAction.Wait;

        return bound.Out.IsNone ? CapsuleAction.Summon : CapsuleAction.Recall;
    }

    /// <summary>
    /// A right click on a capsule in a character's own inventory: a filled one summons or recalls its beast, an empty one catches the nearest
    /// catchable beast beside the character. Always used.
    /// </summary>
    public bool Use(ItemUse use)
    {
        var world = use.World;
        if (!world.Characters.TryResolveNext(use.User, out int slot)) return true;

        if (Full(use.Item)) Click(world, slot, use.Item, use.Inventory);
        else Catch(world, slot, use.Item, use.Inventory);

        Publish();
        return true;
    }

    /// <summary>
    /// A click on a filled capsule (<see cref="Decide"/>).
    /// </summary>
    void Click(World world, int user, ItemInstance capsule, Inventory inventory)
    {
        int? key = Key(capsule.ModTag);
        var bound = key is { } k ? _bound.GetValueOrDefault(k) : null;
        switch (Decide(bound, world.Tick))
        {
            case CapsuleAction.Empty:
                Emptied(world, user, capsule, inventory);
                break;
            case CapsuleAction.Wait:
                AnimalWork.Say(world, user, "Wait a moment");
                break;
            case CapsuleAction.Summon:
                Summon(world, user, key!.Value, bound!);
                break;
            case CapsuleAction.Recall:
                Recall(world, user, key!.Value, bound!);
                break;
        }

        if (_bound.ContainsKey(key ?? 0) && capsule.Record != HusbandryData.FilledCapsule) Refill(world, user, capsule, inventory);
    }

    #region Catching

    /// <summary>
    /// A right click on an empty capsule: puts the nearest catchable beast beside the character in it; with none, says why (an animal in reach that
    /// already has a capsule, or nothing in reach).
    /// </summary>
    void Catch(World world, int user, ItemInstance capsule, Inventory inventory)
    {
        if (Target(world, user) is { } target)
        {
            Put(world, user, capsule, inventory, target);
            return;
        }

        if (Nearest(world, user, c => Packed(c) && MayHold(c)) is { } packed)
            AnimalWork.Say(world, user, HusbandryText.Format("Take {1}'s backpack off first", world.Characters.Cold(packed)!.Name));
        else if (Nearest(world, user, c => IsOut(c.LastingId)) is { } bound)
            AnimalWork.Say(world, user, HusbandryText.Format("{1} already has a Beast Capsule", world.Characters.Cold(bound)!.Name));
        else AnimalWork.Say(world, user, "Nothing to catch: stand next to one of your animals, or knock a beast out");
    }

    /// <summary>
    /// Catches the beast a thrown capsule hit (<see cref="CapsuleThrows"/>): as a right click beside it would.
    /// </summary>
    public void CatchThrown(World world, int user, ItemInstance capsule, Inventory inventory, int target)
    {
        Put(world, user, capsule, inventory, target);
        Publish();
    }

    /// <summary>
    /// Binds a beast to a new filled capsule, which drops where it lay, uses up one empty capsule of the stack and takes the beast out of the world.
    /// </summary>
    void Put(World world, int user, ItemInstance capsule, Inventory inventory, int target)
    {
        var table = world.Characters;
        var cold = table.Cold(target)!;
        var at = table.Next[target].Position;
        if (population?.Data.Items.Create(HusbandryData.FilledCapsule) is not { } filled) return;

        int key = _nextKey++;
        _bound[key] = new BoundBeast(StoredBeast.Take(world, table.IdOf(target), cold, sexes.Female(cold)));
        filled.ModTag = Tag(key);
        UseOne(capsule, inventory);
        DropCarried(world, target, cold);
        world.GroundItems.Drop(filled, at, 0);
        if (cold.PortraitSerial is { } serial) sexes.Assigned.Remove(serial);

        AnimalWork.Say(world, user, HusbandryText.Format("Caught {1}!", cold.Name));
        log($"husbandry: {table.Cold(user)!.Name} caught {cold.Name} ({cold.RecordId}) in capsule {key}");
        Leave(world, target);
    }

    /// <summary>
    /// The nearest animal within reach that may be caught (<see cref="Catchable"/>).
    /// </summary>
    int? Target(World world, int user) => Nearest(world, user, Catchable);

    /// <summary>
    /// The nearest living character other than the user within <see cref="CatchReach"/> that <paramref name="fits"/>.
    /// </summary>
    static int? Nearest(World world, int user, Func<CharacterCold, bool> fits)
    {
        var table = world.Characters;
        var at = table.Next[user].Position;
        int? best = null;
        float bestDistance = CatchReach;
        for (int s = 0; s < table.HighWater; s++)
        {
            if (s == user || !table.Next[s].Alive || table.Cold(s) is not { Medical.Dead: false } c || !fits(c)) continue;

            float d = Vector3.Distance(table.Next[s].Position, at);
            if (d > bestDistance) continue;

            best = s;
            bestDistance = d;
        }

        return best;
    }

    /// <summary>
    /// Whether an animal may be caught now: one of the player's whose race supports summoning, or a knocked-out one whose race supports taming
    /// (docs/husbandry.md "Supported animals"); never one carried, one wearing a backpack, nor one already out of a capsule (its own capsule calls
    /// it back).
    /// </summary>
    public bool Catchable(CharacterCold c) => !Packed(c) && MayHold(c);

    /// <summary>
    /// <see cref="Catchable"/> but for the backpack.
    /// </summary>
    bool MayHold(CharacterCold c)
    {
        if (!c.CarriedBy.IsNone || IsOut(c.LastingId) || Leaving(c)) return false;
        if (c.IsPlayer) return registry.Supports(c, AnimalSupport.Summoning);

        return c.Medical is { Unconscious: true } && registry.Supports(c, AnimalSupport.Taming);
    }

    /// <summary>
    /// Whether an animal wears a backpack: it must come off before the animal goes into a capsule.
    /// </summary>
    static bool Packed(CharacterCold c) => c.Inventory?.Backpack is not null;

    /// <summary>
    /// Whether a body is a capsule's beast that is out.
    /// </summary>
    bool IsOut(LastingId id) => !id.IsNone && _bound.Values.Any(b => b.Out == id);

    /// <summary>
    /// Takes one capsule off a stack, or the capsule itself when it is the last.
    /// </summary>
    static void UseOne(ItemInstance capsule, Inventory inventory)
    {
        if (capsule.Quantity > 1) capsule.Quantity -= 1;
        else inventory.Remove(capsule);
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
    /// Marks a character to leave the world at the slow world (<see cref="Settle"/>); until then it can't be caught again.
    /// </summary>
    internal void Leave(World world, int slot) => _leaving.Add((world.Characters.IdOf(slot), world.Characters.Cold(slot)!));

    /// <summary>
    /// The serial slow-world step: takes the bodies that went into a capsule out of the world, as the engine removes a fed corpse.
    /// </summary>
    public void Settle(World world)
    {
        foreach (var (id, _) in _leaving)
        {
            if (!world.Characters.TryResolveNext(id, out _)) continue;

            world.Uniques.Left(id);
            world.Characters.Remove(id);
        }

        _leaving.Clear();
    }

    /// <summary>
    /// Whether a body went into a capsule this tick and has yet to leave the world.
    /// </summary>
    bool Leaving(CharacterCold c) => _leaving.Exists(l => ReferenceEquals(l.Cold, c));

    #endregion

    #region Summoning and recalling

    /// <summary>
    /// Brings the beast out beside the user as one of the player's, in the user's squad; the capsule stays bound to it.
    /// </summary>
    void Summon(World world, int user, int key, BoundBeast bound)
    {
        var table = world.Characters;
        var beast = bound.Beast;
        var at = table.Next[user].Position;
        var spot = new Vector2(at.X + SummonDistance, at.Z);
        var id = population?.SpawnIntoPlayerSquad(world, table.Cold(user)!.SquadId, beast.Record, spot, beast.Age) ?? CharacterId.None;
        if (id.IsNone || !table.TryResolveNext(id, out int slot))
        {
            AnimalWork.Say(world, user, "It won't come out here (squad full, or no room)");
            return;
        }

        var cold = table.Cold(slot)!;
        beast.Restore(world, id, cold);
        if (cold.PortraitSerial is { } serial) sexes.Assigned[serial] = beast.Female;
        bound.Out = world.LastingIds.GetOrGive(id);
        bound.UsedAt = world.Tick;
        AnimalWork.Say(world, slot, HusbandryText.Format("{1}, come out!", beast.Name));
        log($"husbandry: capsule {key} summoned {beast.Name}");
    }

    /// <summary>
    /// Calls the capsule's beast back in when it is within <see cref="RecallReach"/> of the user, keeping its stats, skills, traits and mod tags
    /// as they are now; a beast that died or is gone for good leaves the capsule empty.
    /// </summary>
    void Recall(World world, int user, int key, BoundBeast bound)
    {
        var table = world.Characters;
        var id = world.LastingIds.Find(bound.Out);
        if (!table.TryResolveNext(id, out int slot) || table.Cold(slot) is not { Medical.Dead: false } cold)
        {
            if (!world.LastingIds.KeptIds().Contains(bound.Out)) Lost(world, user, key, bound);
            else AnimalWork.Say(world, user, HusbandryText.Format("{1} is too far away to call back", bound.Beast.Name));
            return;
        }

        if (Vector3.Distance(table.Next[slot].Position, table.Next[user].Position) > RecallReach)
        {
            AnimalWork.Say(world, user, HusbandryText.Format("{1} is too far away to call back", cold.Name));
            return;
        }

        bound.Beast = StoredBeast.Take(world, id, cold, sexes.Female(cold));
        bound.Out = default;
        bound.UsedAt = world.Tick;
        DropCarried(world, slot, cold);
        if (cold.PortraitSerial is { } serial) sexes.Assigned.Remove(serial);

        AnimalWork.Say(world, user, HusbandryText.Format("{1}, come back!", cold.Name));
        log($"husbandry: capsule {key} recalled {cold.Name}");
        Leave(world, slot);
    }

    /// <summary>
    /// The beast died or is gone for good: the capsule is unbound (it becomes empty at its next click).
    /// </summary>
    void Lost(World world, int user, int key, BoundBeast bound)
    {
        _bound.Remove(key);
        AnimalWork.Say(world, user, HusbandryText.Format("{1} is gone; the capsule is empty", bound.Beast.Name));
        log($"husbandry: capsule {key} lost {bound.Beast.Name}");
    }

    /// <summary>
    /// A filled capsule with no beast bound to it becomes an empty capsule in the same inventory (on the ground when it has no room).
    /// </summary>
    void Emptied(World world, int user, ItemInstance capsule, Inventory inventory)
    {
        inventory.Remove(capsule);
        if (population?.Data.Items.Create(HusbandryData.Capsule) is not { } empty) return;

        if (!inventory.Add(empty)) world.GroundItems.Drop(empty, world.Characters.Next[user].Position, 0);
    }

    /// <summary>
    /// An older save's full capsule (the empty record with a key) becomes a filled capsule with the same key, in the same inventory.
    /// </summary>
    void Refill(World world, int user, ItemInstance capsule, Inventory inventory)
    {
        if (population?.Data.Items.Create(HusbandryData.FilledCapsule) is not { } filled) return;

        filled.ModTag = capsule.ModTag;
        UseOne(capsule, inventory);
        if (!inventory.Add(filled)) world.GroundItems.Drop(filled, world.Characters.Next[user].Position, 0);
    }

    /// <summary>
    /// A filled capsule's tag for a key.
    /// </summary>
    static string Tag(int key) => TagPrefix + key.ToString(CultureInfo.InvariantCulture);

    #endregion

    #region Saving, the views and the debug row

    /// <summary>
    /// Publishes the tooltips' views; a serial step calls it after each change, and once a load has read the beasts.
    /// </summary>
    public void Publish() => _views = _bound.ToFrozenDictionary(b => b.Key, b => new BeastView(b.Value.Beast, !b.Value.Out.IsNone));

    /// <summary>
    /// Writes the beasts (sidecar version 6).
    /// </summary>
    public void Write(BinaryWriter w)
    {
        w.Write(_nextKey);
        w.Write(_bound.Count);
        foreach (var (key, bound) in _bound)
        {
            w.Write(key);
            bound.Beast.Write(w);
            w.Write(bound.Out.Value);
        }
    }

    /// <summary>
    /// Reads them back; before version 6 every beast was inside its capsule.
    /// </summary>
    public void Read(BinaryReader r, int version)
    {
        _bound.Clear();
        _nextKey = r.ReadInt32();
        int count = r.ReadInt32();
        for (int k = 0; k < count; k++)
        {
            int key = r.ReadInt32();
            var bound = new BoundBeast(StoredBeast.Read(r, version));
            if (version >= 6) bound.Out = new LastingId(r.ReadUInt64());
            _bound[key] = bound;
        }

        Publish();
    }

    /// <summary>
    /// Adds them to the state hash.
    /// </summary>
    public void Hash(ref StateHasher hasher)
    {
        hasher.Add(_nextKey);
        hasher.Add(_bound.Count);
        foreach (var (key, bound) in _bound)
        {
            hasher.Add(key);
            hasher.Add(bound.Out.Value);
            hasher.Add(bound.UsedAt);
            bound.Beast.Hash(ref hasher);
        }
    }

    /// <summary>
    /// The debug view: each bound beast.
    /// </summary>
    public string Describe()
    {
        if (_bound.Count == 0) return "No animals in capsules.";

        var text = new StringBuilder();
        foreach (var (key, bound) in _bound)
        {
            var beast = bound.Beast;
            string sex = beast.Female ? "female" : "male";
            string where = bound.Out.IsNone ? "inside" : "out";
            text.AppendLine($"Capsule {key}: {beast.Name} ({beast.Kind}, {sex}, age {beast.Age:0.00}, {where})");
        }

        return text.ToString();
    }

    #endregion
}
