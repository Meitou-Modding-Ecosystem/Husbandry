using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Numerics;
using AnimalBehaviors;
using Husbandry.Training;
using Meitou.Data.Gameplay.Combat;
using Meitou.Simulation;
using Meitou.Simulation.Bodies;
using Meitou.Simulation.Combat;
using Meitou.Simulation.Items;

namespace Husbandry.Capsules;

/// <summary>
/// The player chose Throw Beast Capsule on a knocked-out beast with these characters selected.
/// </summary>
public sealed record ThrowCapsuleCommand(IReadOnlyList<CharacterId> Throwers, CharacterId Target) : SimCommand;

/// <summary>
/// Throwing a Beast Capsule (docs/husbandry.md "Beast Capsules"): the player right-clicks a knocked-out beast and chooses Throw Beast Capsule; the
/// first selected character holding an empty capsule walks to within <see cref="Range"/> of it, stops, turns to face it and plays the throw,
/// held facing it until the clip ends and the capsule has landed. Another order or a hit on the thrower before then interrupts the throw, which
/// then fails (the capsule is kept). The capsule flies from the hand (<see cref="Flights"/>, drawn by the interface) and catches the beast as a
/// right click on the capsule beside it would, unless the beast breaks free (<see cref="BreakFreeChance"/>): it comes to, healed just enough to
/// stand, and the capsule is kept. Throws in progress are not saved. Serial steps, except <see cref="Move"/> and the published views.
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
    /// How long the throw plays, in seconds: the length of its clip (<see cref="ThrowData.Clip"/>).
    /// </summary>
    const float ThrowSeconds = 1.7f;

    /// <summary>
    /// When the hand lets go, in seconds into the throw (the clip's release).
    /// </summary>
    public const float ReleaseSeconds = 0.9f;

    /// <summary>
    /// How fast the capsule flies, in game units a second.
    /// </summary>
    public const float FlightSpeed = 300;

    /// <summary>
    /// The hand's place at the release in the thrower's frame (X to its right, Y up, Z ahead), in game units: where the flight starts.
    /// </summary>
    public static readonly Vector3 Hand = new(3, 17, 4);

    /// <summary>
    /// How far a new goal may lie from where the thrower stopped before it counts as another order, in game units.
    /// </summary>
    const float NewGoal = 5;

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

        /// <summary>
        /// Whether the capsule has landed; the thrower then finishes the clip.
        /// </summary>
        public bool Landed { get; set; }

        /// <summary>
        /// When the capsule lands, in seconds into the throw (<see cref="LandAt"/>).
        /// </summary>
        public float LandSeconds { get; set; }

        /// <summary>
        /// Where the thrower stopped to throw (X/Z): a goal elsewhere is another order.
        /// </summary>
        public Vector2 StoppedAt { get; set; }

        /// <summary>
        /// The order in hand when the throw started: another one is another order.
        /// </summary>
        public object? Front { get; set; }

        /// <summary>
        /// Whether a hit landed on the thrower during the throw.
        /// </summary>
        public bool Hit { get; set; }
    }

    /// <summary>
    /// A capsule in flight, for the interface to draw: from the hand to the beast, between two ticks.
    /// </summary>
    public sealed record Flight(Vector3 From, Vector3 To, long Released, long Lands);

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
    /// The capsules thrown and not yet landed, published after each serial step for the interface (display only).
    /// </summary>
    public IReadOnlyList<Flight> Flights => _flights;

    /// <summary>
    /// The latest <see cref="Flights"/>.
    /// </summary>
    volatile Flight[] _flights = [];

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
    /// A resolved hit: a thrower hit during its throw has it interrupted.
    /// </summary>
    public void Observe(in CombatHit hit)
    {
        var victim = hit.Victim;
        foreach (var t in _throws)
            if (t.Thrower == victim && t.StartedAt >= 0 && !t.Landed) t.Hit = true;
    }

    /// <summary>
    /// When a capsule thrown at a beast <paramref name="distance"/> away lands, in seconds into the throw.
    /// </summary>
    public static float LandAt(float distance) => ReleaseSeconds + Math.Max(distance, 0) / FlightSpeed;

    /// <summary>
    /// The heading that faces from <paramref name="from"/> to <paramref name="to"/> (the engine's yaw: 0 along +Z, towards +X).
    /// </summary>
    public static float YawTowards(Vector3 from, Vector3 to) => MathF.Atan2(to.X - from.X, to.Z - from.Z);

    /// <summary>
    /// Where a capsule is <paramref name="progress"/> (0 to 1) along its flight: on the line from the hand to the beast, lifted by an arc that
    /// peaks a fifth of the distance up halfway.
    /// </summary>
    public static Vector3 AlongFlight(Flight flight, float progress)
    {
        float p = Math.Clamp(progress, 0, 1);
        float lift = Vector3.Distance(flight.From, flight.To) * 0.2f * 4 * p * (1 - p);
        return Vector3.Lerp(flight.From, flight.To, p) + new Vector3(0, lift, 0);
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
            hasher.Add(t.Landed);
            hasher.Add(t.LandSeconds);
            hasher.Add(t.Hit);
        }
    }

    /// <summary>
    /// One throw's step; false when it is over (caught or given up).
    /// </summary>
    bool Step(World world, Throw t)
    {
        var table = world.Characters;
        if (!table.TryResolveNext(t.Thrower, out int thrower) || table.Cold(thrower) is not { } cold || Hitches.Down(cold)) return false;
        if (t.Landed) return HoldFacing(world, t, thrower);
        if (world.Tick - t.ChosenAt > GiveUpTicks) return Said(world, thrower, "Gave up the throw");
        if (!table.TryResolveNext(t.Target, out int beast) || !capsules.Catchable(table.Cold(beast)!)) return Said(world, thrower, "The beast got away");
        if (EmptyCapsule(world, thrower) is not { } capsule) return Said(world, thrower, "No empty Beast Capsule");

        if (t.StartedAt < 0) return Approach(world, t, thrower, beast);
        if (Interrupted(world, t, thrower, cold)) return Said(world, thrower, "Throw interrupted");

        Face(world, thrower, beast);
        if (Seconds(world, t) < t.LandSeconds) return true;

        if (Landed.Count < MaxLanded) Landed.Enqueue(world.Characters.Next[beast].Position);
        if (BreaksFree(world, t)) Rouse(world, thrower, beast);
        else capsules.CatchThrown(world, thrower, capsule, cold.Inventory!, beast);
        t.Landed = true;
        return true;
    }

    /// <summary>
    /// Whether the throw was interrupted before the capsule landed: the thrower was hit, given another order (a new order in hand) or sent
    /// elsewhere (a goal away from where it stopped).
    /// </summary>
    static bool Interrupted(World world, Throw t, int thrower, CharacterCold cold)
    {
        if (t.Hit) return true;
        if (cold.OrderQueue.Front is { } front && !ReferenceEquals(front, t.Front)) return true;

        return Vector2.Distance(world.Characters.Next[thrower].Goal, t.StoppedAt) > NewGoal;
    }

    /// <summary>
    /// Turns the thrower to face the beast.
    /// </summary>
    static void Face(World world, int thrower, int beast)
    {
        var table = world.Characters;
        table.Next[thrower].Yaw = YawTowards(table.Next[thrower].Position, table.Next[beast].Position);
    }

    /// <summary>
    /// After the landing: keeps the thrower facing the beast's spot until the clip ends; false then.
    /// </summary>
    static bool HoldFacing(World world, Throw t, int thrower)
    {
        if (Seconds(world, t) >= ThrowSeconds) return false;

        if (world.Characters.TryResolveNext(t.Target, out int beast)) Face(world, thrower, beast);
        return true;
    }

    /// <summary>
    /// How long a started throw has played, in seconds.
    /// </summary>
    static float Seconds(World world, Throw t) => (world.Tick - t.StartedAt) * world.TickSeconds;

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

        AnimalWork.Say(world, thrower, HusbandryText.Format("{1} broke free!", cold.Name));
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
            Start(world, t, thrower, target);
            return true;
        }

        if ((world.Tick - t.ChosenAt) % WalkEveryTicks == 0) AnimalWork.Walk(world, thrower, StopShort(world, thrower, target));
        return true;
    }

    /// <summary>
    /// Starts the throw: the thrower stops where it stands, turns to the beast, and the capsule's landing is timed by the distance.
    /// </summary>
    static void Start(World world, Throw t, int thrower, Vector3 target)
    {
        var table = world.Characters;
        var at = table.Next[thrower].Position;
        AnimalWork.Walk(world, thrower, at);
        table.Next[thrower].Yaw = YawTowards(at, target);

        t.StartedAt = world.Tick;
        t.LandSeconds = LandAt(Vector3.Distance(at, target));
        t.StoppedAt = new Vector2(at.X, at.Z);
        t.Front = table.Cold(thrower)!.OrderQueue.Front;
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
    /// The flight of a started throw whose capsule has not landed: from the thrower's hand at the release to the beast's middle.
    /// </summary>
    static Flight? FlightOf(World world, Throw t)
    {
        var table = world.Characters;
        if (t.StartedAt < 0 || t.Landed || !table.TryResolveNext(t.Thrower, out int thrower) || !table.TryResolveNext(t.Target, out int beast))
            return null;

        var hot = table.Next[thrower];
        var hand = hot.Position + Vector3.Transform(Hand, Quaternion.CreateFromAxisAngle(Vector3.UnitY, hot.Yaw));
        var to = table.Next[beast].Position + new Vector3(0, 5, 0);
        long released = t.StartedAt + (long)MathF.Round(ReleaseSeconds / world.TickSeconds);
        long lands = t.StartedAt + (long)MathF.Round(t.LandSeconds / world.TickSeconds);
        return new Flight(hand, to, released, lands);
    }

    /// <summary>
    /// Publishes the throwers in their throw, and every <see cref="ViewEveryTicks"/> the catchable beasts and the player's characters holding an
    /// empty capsule.
    /// </summary>
    void Publish(World world)
    {
        _throwing = _throws.Where(t => t.StartedAt >= 0).ToFrozenDictionary(t => t.Thrower, t => t.StartedAt);
        _flights = [.. _throws.Select(t => FlightOf(world, t)).OfType<Flight>()];
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
