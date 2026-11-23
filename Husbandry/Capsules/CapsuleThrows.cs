using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Numerics;
using Husbandry.Training;
using Meitou.Data.Gameplay.Combat;
using Meitou.Simulation;
using Meitou.Simulation.Bodies;
using Meitou.Simulation.Items;

namespace Husbandry.Capsules;

/// <summary>
/// The player chose Throw Beast Capsule on a knocked-out beast with these characters selected.
/// </summary>
public sealed record ThrowCapsuleCommand(IReadOnlyList<CharacterId> Throwers, CharacterId Target) : SimCommand;

/// <summary>
/// Throwing a Beast Capsule (docs/husbandry.md "Beast Capsules"): the player right-clicks a knocked-out beast and chooses Throw Beast Capsule; the
/// first selected character holding an empty capsule walks to within <see cref="Range"/> of it, plays the throw and catches it as a right click on
/// the capsule beside it would, unless the beast breaks free (<see cref="BreakFreeChance"/>): it comes to, healed just enough to stand, and the
/// capsule stays empty. Throws in progress are not saved. Serial steps, except <see cref="Move"/> and the published views.
/// </summary>
public sealed class CapsuleThrows(CapsuleSystem capsules, string capsuleRecord, CombatTechnique? throwMove, RngPurpose purpose)
{
    /// <summary>
    /// The chance in 100 that a thrown capsule's beast breaks free instead of being caught.
    /// </summary>
    public const int BreakFreeChance = 20;

    /// <summary>
    /// The salt of the break-free roll.
    /// </summary>
    const ulong BreakFreeSalt = 0xCA75;

    /// <summary>
    /// What a vital part and the blood are brought up to when a beast breaks free: just above where it would stay down (h - s + rig at 0).
    /// </summary>
    const float RousedHealth = 1;

    /// <summary>
    /// How near the beast the thrower must be, in game units (30 m).
    /// </summary>
    public const float Range = 300;

    /// <summary>
    /// How short of <see cref="Range"/> the thrower stops, so it is inside it on arrival.
    /// </summary>
    const float Margin = 20;

    /// <summary>
    /// How long the throw plays before the capsule lands, in seconds.
    /// </summary>
    const float ThrowSeconds = 1;

    /// <summary>
    /// How often a walking thrower is sent on again (a move order ends when something interrupts it), in ticks.
    /// </summary>
    const int WalkEveryTicks = 60;

    /// <summary>
    /// The longest a throw may take before it is given up, in ticks (two minutes at 30 ticks a second).
    /// </summary>
    const int GiveUpTicks = 3600;

    /// <summary>
    /// How often the menu's views of catchable beasts and capsule holders are refreshed, in ticks.
    /// </summary>
    const int ViewEveryTicks = 10;

    /// <summary>
    /// One throw in progress: who throws at which beast, when it was chosen and when the throw started (-1 while walking).
    /// </summary>
    sealed record Throw(CharacterId Thrower, CharacterId Target, long ChosenAt)
    {
        /// <summary>
        /// The tick the throw started, -1 while the thrower is still walking.
        /// </summary>
        public long StartedAt { get; set; } = -1;
    }

    /// <summary>
    /// The most landings <see cref="Landed"/> holds undrawn (no interface drains it in a headless world).
    /// </summary>
    const int MaxLanded = 16;

    /// <summary>
    /// The throws in progress.
    /// </summary>
    readonly List<Throw> _throws = [];

    /// <summary>
    /// Where capsules landed, for the mist the interface draws there (display only: never read by the world, not hashed).
    /// </summary>
    public ConcurrentQueue<Vector3> Landed { get; } = new();

    /// <summary>
    /// The throwers in their throw and the tick it started, published after each serial step for <see cref="Move"/>.
    /// </summary>
    volatile FrozenDictionary<CharacterId, long> _throwing = FrozenDictionary<CharacterId, long>.Empty;

    /// <summary>
    /// The knocked-out beasts a capsule may catch, published for the right-click menu.
    /// </summary>
    volatile FrozenSet<CharacterId> _catchable = FrozenSet<CharacterId>.Empty;

    /// <summary>
    /// The player's characters holding an empty capsule, published for the right-click menu.
    /// </summary>
    volatile FrozenSet<CharacterId> _holders = FrozenSet<CharacterId>.Empty;

    /// <summary>
    /// Whether a beast may be caught by a throw now (as of the last serial step).
    /// </summary>
    public bool Catchable(CharacterId beast) => _catchable.Contains(beast);

    /// <summary>
    /// The first of <paramref name="selected"/> holding an empty capsule (as of the last serial step), or null.
    /// </summary>
    public CharacterId? Holder(IEnumerable<CharacterId> selected) => selected.Where(_holders.Contains).Cast<CharacterId?>().FirstOrDefault();

    /// <summary>
    /// Takes the player's choices: the first chosen thrower holding an empty capsule gets the throw, replacing one it had.
    /// </summary>
    public void Take(World world, ThrowCapsuleCommand command)
    {
        foreach (var thrower in command.Throwers)
        {
            if (!world.Characters.TryResolveNext(thrower, out int slot) || EmptyCapsule(world, slot) is null) continue;

            _throws.RemoveAll(t => t.Thrower == thrower);
            _throws.Add(new Throw(thrower, command.Target, world.Tick));
            return;
        }
    }

    /// <summary>
    /// The serial step: walks, throws and catches, then publishes the views.
    /// </summary>
    public void Run(World world)
    {
        _throws.RemoveAll(t => !Step(world, t));
        Publish(world);
    }

    /// <summary>
    /// The throw animation for a character in its throw (<c>AddCombatMove</c>): read-only, from the last serial step's view.
    /// </summary>
    public CombatMove? Move(World world, CharacterId id, long tick)
    {
        if (throwMove is null || !_throwing.TryGetValue(id, out long started)) return null;

        float progress = (tick - started) * world.TickSeconds / ThrowSeconds;
        return new CombatMove(throwMove, Math.Clamp(progress, 0, 1));
    }

    /// <summary>
    /// The throws in progress, for the state hash.
    /// </summary>
    public void Hash(ref StateHasher hasher)
    {
        hasher.Add(_throws.Count);
        foreach (var t in _throws)
        {
            hasher.Add(t.Thrower.Slot);
            hasher.Add(t.Thrower.Generation);
            hasher.Add(t.Target.Slot);
            hasher.Add(t.Target.Generation);
            hasher.Add(t.StartedAt);
        }
    }

    /// <summary>
    /// One throw's step; false when it is over (caught or given up).
    /// </summary>
    bool Step(World world, Throw t)
    {
        var table = world.Characters;
        if (!table.TryResolveNext(t.Thrower, out int thrower) || table.Cold(thrower) is not { } cold || Hitches.Down(cold)) return false;
        if (world.Tick - t.ChosenAt > GiveUpTicks) return Said(world, thrower, "Gave up the throw");
        if (!table.TryResolveNext(t.Target, out int beast) || !capsules.Catchable(table.Cold(beast)!)) return Said(world, thrower, "The beast got away");
        if (EmptyCapsule(world, thrower) is not { } capsule) return Said(world, thrower, "No empty Beast Capsule");

        if (t.StartedAt < 0) return Approach(world, t, thrower, beast);
        if ((world.Tick - t.StartedAt) * world.TickSeconds < ThrowSeconds) return true;

        if (Landed.Count < MaxLanded) Landed.Enqueue(world.Characters.Next[beast].Position);
        if (BreaksFree(world, t)) Rouse(world, thrower, beast);
        else capsules.CatchThrown(world, thrower, capsule, cold.Inventory!, beast);
        return false;
    }

    /// <summary>
    /// The break-free roll of a landed throw (<see cref="BreakFreeChance"/>), from the world's seed, the beast and the tick.
    /// </summary>
    bool BreaksFree(World world, Throw t)
    {
        ulong entity = ((ulong)(uint)t.Target.Slot << 32) | (uint)t.Target.Generation;
        return Rng.Float(Rng.Hash(world.Seed, entity, purpose, BreakFreeSalt ^ (ulong)world.Tick)) * 100 < BreakFreeChance;
    }

    /// <summary>
    /// The beast breaks free: its knock-out ends and its vital parts and blood come up just enough that the medical tick lets it stand.
    /// </summary>
    static void Rouse(World world, int thrower, int beast)
    {
        var cold = world.Characters.Cold(beast)!;
        if (cold.Medical is not { } medical) return;

        medical.KoTimer = 0;
        medical.Blood = Math.Max(medical.Blood, RousedHealth);
        foreach (var part in medical.Parts.Where(p => p.IsVital)) RaisePart(part);

        AnimalWork.Say(world, thrower, $"{cold.Name} broke free!");
    }

    /// <summary>
    /// Brings a vital part's h - s + rig up to <see cref="RousedHealth"/>: its stun first, then its flesh.
    /// </summary>
    static void RaisePart(HealthPart part)
    {
        float lack = RousedHealth - (part.Flesh - part.Stun + part.Rig);
        if (lack <= 0) return;

        float unstun = Math.Min(part.Stun, lack);
        part.Stun -= unstun;
        part.Flesh += lack - unstun;
    }

    /// <summary>
    /// Walks the thrower towards the beast until it is in range, then starts the throw; always true.
    /// </summary>
    static bool Approach(World world, Throw t, int thrower, int beast)
    {
        var target = world.Characters.Next[beast].Position;
        if (AnimalWork.Away(world, thrower, target) <= Range)
        {
            t.StartedAt = world.Tick;
            return true;
        }

        if ((world.Tick - t.ChosenAt) % WalkEveryTicks == 0) AnimalWork.Walk(world, thrower, StopShort(world, thrower, target));
        return true;
    }

    /// <summary>
    /// The point on the line from the beast to the thrower just inside <see cref="Range"/> of the beast.
    /// </summary>
    static Vector3 StopShort(World world, int thrower, Vector3 target)
    {
        var at = world.Characters.Next[thrower].Position;
        var away = new Vector3(at.X - target.X, 0, at.Z - target.Z);
        return target + Vector3.Normalize(away) * (Range - Margin);
    }

    /// <summary>
    /// Tells the player why a throw ended; false.
    /// </summary>
    static bool Said(World world, int slot, string text)
    {
        AnimalWork.Say(world, slot, text);
        return false;
    }

    /// <summary>
    /// An empty capsule in a character's own inventory, or null.
    /// </summary>
    ItemInstance? EmptyCapsule(World world, int slot) =>
        world.Characters.Cold(slot)?.Inventory?.All().FirstOrDefault(i => i.Record == capsuleRecord && !CapsuleSystem.Full(i));

    /// <summary>
    /// Publishes the throwers in their throw, and every <see cref="ViewEveryTicks"/> the catchable beasts and the player's characters holding an
    /// empty capsule.
    /// </summary>
    void Publish(World world)
    {
        _throwing = _throws.Where(t => t.StartedAt >= 0).ToFrozenDictionary(t => t.Thrower, t => t.StartedAt);
        if (world.Tick % ViewEveryTicks != 0) return;

        var table = world.Characters;
        var catchable = new HashSet<CharacterId>();
        var holders = new HashSet<CharacterId>();
        for (int s = 0; s < table.HighWater; s++)
        {
            if (!table.Next[s].Alive || table.Cold(s) is not { Medical.Dead: false } c) continue;

            if (!c.IsPlayer && capsules.Catchable(c)) catchable.Add(table.IdOf(s));
            if (c.IsPlayer && EmptyCapsule(world, s) is not null) holders.Add(table.IdOf(s));
        }

        _catchable = catchable.ToFrozenSet();
        _holders = holders.ToFrozenSet();
    }
}
