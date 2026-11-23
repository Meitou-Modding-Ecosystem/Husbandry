using Meitou.Data.Gameplay.Combat;
using System.Globalization;
using System.Text;
using Meitou.Modding;
using Husbandry.Capsules;
using Husbandry.Training;
using Meitou.Simulation;
using Meitou.Simulation.Combat;
using Meitou.Simulation.Population;

namespace Husbandry;

/// <summary>
/// Every female's breeding clock falls due at the next check (the debug row's "try breeding now").
/// </summary>
public sealed record BreedNowCommand : SimCommand;

/// <summary>
/// Husbandry in one world (docs/husbandry.md): the pens kept and breeding run in serial steps, the hitches and breeding clocks saved in the mod's
/// sidecar and hashed.
/// </summary>
public sealed class HusbandrySystem : ITickSystem, IStateHashed, IModSaveData, ICombatHitObserver
{
    #region Ticking

    /// <summary>
    /// Ticks between pen checks of one animal (one game second at 30 ticks a second), staggered by slot.
    /// </summary>
    public const int PenEveryTicks = 30;

    /// <summary>
    /// Ticks between breeding passes (ten game seconds).
    /// </summary>
    public const int BreedEveryTicks = 300;

    /// <summary>
    /// The player's settings, copied at each pass.
    /// </summary>
    readonly HusbandrySettings _settings;

    /// <summary>
    /// The population, which makes the pups.
    /// </summary>
    readonly PopulationSystem? _population;

    /// <summary>
    /// The mod's random purpose.
    /// </summary>
    readonly RngPurpose _purpose;

    /// <summary>
    /// Breeding's clocks.
    /// </summary>
    readonly Breeding _breeding;

    /// <summary>
    /// The animals' sexes.
    /// </summary>
    readonly Sexes _sexes;

    /// <summary>
    /// The Beastmaster of Mourn and his pen.
    /// </summary>
    readonly Beastmaster _beastmaster;

    /// <summary>
    /// The Beastmasters the debug menu put at player settlements.
    /// </summary>
    readonly OutpostTraders _outposts;

    /// <summary>
    /// The animals fetching for storages.
    /// </summary>
    readonly Fetching _fetching;

    /// <summary>
    /// The animals hunting from bone piles.
    /// </summary>
    readonly Hunting _hunting;

    /// <summary>
    /// The Tie up knock-downs.
    /// </summary>
    readonly TieUp _tieUp;

    /// <summary>
    /// The Beast Capsules and the animals in them.
    /// </summary>
    readonly CapsuleSystem _capsules;

    /// <summary>
    /// Capsules thrown at knocked-out beasts.
    /// </summary>
    readonly CapsuleThrows _throws;

    /// <summary>
    /// The animal kinds and trainable skills (docs/husbandry.md "Supported animals").
    /// </summary>
    readonly AnimalRegistry _registry;

    /// <summary>
    /// The game hours at the last training and work check; NaN before the first.
    /// </summary>
    double _workedAt = double.NaN;

    /// <summary>
    /// Ticks between the Beastmaster's checks (one game second).
    /// </summary>
    public const int TraderEveryTicks = 30;

    /// <summary>
    /// Set by <see cref="BreedNowCommand"/> until the next breeding pass.
    /// </summary>
    bool _breedNow;

    /// <summary>
    /// The latest view for the debug rows (written in the tick, read on the host's thread).
    /// </summary>
    volatile string _view = "No hitched animals yet.";

    /// <summary>
    /// A system for one world.
    /// </summary>
    public HusbandrySystem(SystemBuildContext context, HusbandrySettings settings, AnimalRegistry registry, RngPurpose purpose, Action<string> log,
        CombatTechnique? throwMove)
    {
        _settings = settings;
        _registry = registry;
        _purpose = purpose;
        _population = context.Find<PopulationSystem>();
        _sexes = new Sexes(purpose);
        _breeding = new Breeding(purpose, _sexes, registry, log);
        _beastmaster = new Beastmaster(_population, _sexes, log, TraderPost.Mourn);
        _outposts = new OutpostTraders(_population, _sexes, log);
        _fetching = new Fetching(_population);
        _hunting = new Hunting(_population, log);
        _tieUp = new TieUp(purpose);
        _capsules = new CapsuleSystem(_population, _sexes, registry, log);
        _throws = new CapsuleThrows(_capsules, HusbandryData.Capsule, throwMove, purpose);
        TradeWindowGender.Sexes = _sexes;
    }

    /// <summary>
    /// The debug rows' view of the hitched animals and the latest tries.
    /// </summary>
    public string View => _view;

    /// <summary>
    /// The debug row's view of the Beastmaster (written in the tick, read on the host's thread).
    /// </summary>
    public string TraderView => _traderView;

    /// <summary>
    /// The settlement Beastmasters, for the debug row that adds one.
    /// </summary>
    public OutpostTraders Outposts => _outposts;

    /// <summary>
    /// The Beast Capsules, for the item use and the debug row.
    /// </summary>
    public CapsuleSystem Capsules => _capsules;

    /// <summary>
    /// The capsule throws, for the right-click row and the throw animation.
    /// </summary>
    public CapsuleThrows Throws => _throws;

    /// <summary>
    /// The debug row's view of the capsules (written in the tick, read on the host's thread).
    /// </summary>
    public string CapsuleView => _capsuleView;

    /// <summary>
    /// The latest <see cref="CapsuleView"/>.
    /// </summary>
    volatile string _capsuleView = "No animals in capsules.";

    /// <summary>
    /// The latest <see cref="TraderView"/>.
    /// </summary>
    volatile string _traderView = "Beastmaster: no world yet.";

    public void Inputs(World world, IReadOnlyList<SimCommand> commands)
    {
        foreach (var command in commands)
        {
            if (command is BreedNowCommand) _breedNow = true;
            if (command is SettlementTraderCommand trader) _outposts.Add(trader);
            if (command is ThrowCapsuleCommand thrown) _throws.Take(world, thrown);
        }
    }

    public void SlowWorld(World world)
    {
        RestoreHitches(world);
        var settings = _settings.Copy();
        var hitched = Hitches.All(world);
        foreach (var h in hitched)
            if ((h.Slot + world.Tick) % PenEveryTicks == 0) Pens.Keep(world, h, settings, _purpose);

        _tieUp.Apply(world);
        _capsules.Run(world);
        _throws.Run(world);
        if (world.Tick % TraderEveryTicks == 0) RunTraders(world, settings);
        if (world.Tick % TraderEveryTicks == 0) RunTraining(world, settings);

        if (!_breedNow && world.Tick % BreedEveryTicks != 0) return;

        if (settings.Breeding) _breeding.Run(world, hitched, settings, _population, _breedNow);
        _breedNow = false;
        _view = Describe(world, hitched);
    }

    /// <summary>
    /// The Beastmasters' check: Mourn's when the setting has him, and those at player settlements (the debug menu put them there).
    /// </summary>
    void RunTraders(World world, HusbandrySettings settings)
    {
        if (settings.Beastmaster) _beastmaster.Run(world, _purpose);
        _outposts.Run(world, _purpose);
        _traderView = _beastmaster.Describe(world) + _outposts.Describe(world);
        _capsuleView = _capsules.Describe();
    }

    /// <summary>
    /// The training and work check (docs/husbandry.md "Training"): the Training Posts, the jobs that need a skill and the skills that need none,
    /// for the game hours since the last check.
    /// </summary>
    void RunTraining(World world, HusbandrySettings settings)
    {
        double hours = double.IsNaN(_workedAt) ? 0 : Math.Max(0, world.GameHours - _workedAt);
        _workedAt = world.GameHours;
        TrainingPost.Run(world, _registry, settings, _purpose, hours);
        Guarding.Run(world, settings, _purpose, hours);
        _fetching.Run(world);
        _hunting.Run(world, settings, _purpose);
        PassiveSkills.Run(world, hours);
    }

    public void HitResolved(World world, in CombatHit hit) => _tieUp.Observe(world, hit);

    /// <summary>
    /// The debug view: each hitched animal with its post, race, sex, age, hunger and next try, then the latest tries.
    /// </summary>
    string Describe(World world, List<Hitched> hitched)
    {
        if (hitched.Count == 0) return "No hitched animals.";

        var text = new StringBuilder();
        foreach (var h in hitched)
        {
            bool female = _sexes.Female(h.Cold);
            string next = h.Cold.PortraitSerial is { } s && _breeding.NextTry.TryGetValue(s, out double at)
                ? $", next try in {Math.Max(0, at - world.GameHours).ToString("0.#", CultureInfo.InvariantCulture)} h"
                : "";
            text.AppendLine($"{h.Cold.Name} ({h.Cold.Race?.Name}, {(female ? "female" : "male")}, age {h.Cold.Age:0.00}, "
                + $"{Hitches.Fed(Hitches.Hunger(h.Cold))}) at {h.Post.Id}{next}");
        }

        foreach (string line in _breeding.Recent) text.AppendLine(line);
        return text.ToString();
    }

    #endregion

    #region Saving

    /// <summary>
    /// The sidecar layout's version.
    /// </summary>
    public const int SaveVersion = 4;

    /// <summary>
    /// Hitches read from a save, waiting for the first tick to find their characters by portrait serial.
    /// </summary>
    List<(uint Serial, string Post)>? _pending;

    /// <summary>
    /// The world the last tick ran on, for the save's hitches.
    /// </summary>
    World? _world;

    public void Write(Stream stream)
    {
        using var w = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        w.Write(SaveVersion);
        var hitches = SavedHitches();
        w.Write(hitches.Count);
        foreach (var (serial, post) in hitches)
        {
            w.Write(serial);
            w.Write(post);
        }

        w.Write(_breeding.NextTry.Count);
        foreach (var (serial, next) in _breeding.NextTry)
        {
            w.Write(serial);
            w.Write(next);
        }

        WriteSexes(w);
        _beastmaster.Write(w);
        _outposts.Write(w);
        _capsules.Write(w);
    }

    /// <summary>
    /// Writes the sexes the mod set (version 2).
    /// </summary>
    void WriteSexes(BinaryWriter w)
    {
        w.Write(_sexes.Assigned.Count);
        foreach (var (serial, female) in _sexes.Assigned)
        {
            w.Write(serial);
            w.Write(female);
        }
    }

    public void Read(Stream stream)
    {
        using var r = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        int version = r.ReadInt32();
        if (version is < 1 or > SaveVersion) return;

        _pending = [];
        int hitches = r.ReadInt32();
        for (int k = 0; k < hitches; k++) _pending.Add((r.ReadUInt32(), r.ReadString()));

        _breeding.NextTry.Clear();
        int clocks = r.ReadInt32();
        for (int k = 0; k < clocks; k++) _breeding.NextTry[r.ReadUInt32()] = r.ReadDouble();

        if (version < 2) return;

        _sexes.Assigned.Clear();
        int sexes = r.ReadInt32();
        for (int k = 0; k < sexes; k++) _sexes.Assigned[r.ReadUInt32()] = r.ReadBoolean();

        _beastmaster.Read(r);
        if (version >= 3) _outposts.Read(r);
        if (version >= 4) _capsules.Read(r);
    }

    /// <summary>
    /// Every hitch of the last world ticked, and those still waiting for their characters.
    /// </summary>
    List<(uint Serial, string Post)> SavedHitches()
    {
        var list = new List<(uint, string)>(_pending ?? []);
        if (_world is not { } world) return list;

        var table = world.Characters;
        for (int s = 0; s < table.HighWater; s++)
        {
            if (table.Next[s].Alive && table.Cold(s) is { PortraitSerial: { } serial } cold && Hitches.PostOf(cold) is { } post)
                list.Add((serial, post));
        }

        return list;
    }

    /// <summary>
    /// Puts the saved hitches back on their characters at the first tick after a load (the engine keeps no mod jobs in its save).
    /// </summary>
    void RestoreHitches(World world)
    {
        _world = world;
        if (_pending is not { } pending) return;

        _pending = null;
        var posts = pending.ToDictionary(p => p.Serial, p => p.Post);
        var table = world.Characters;
        for (int s = 0; s < table.HighWater; s++)
        {
            if (table.Next[s].Alive && table.Cold(s) is { IsPlayer: true, PortraitSerial: { } serial } cold && posts.TryGetValue(serial, out string? post))
                Hitches.Hitch(cold, post);
        }
    }

    #endregion

    #region Hashing

    public void Hash(ref StateHasher hasher)
    {
        _throws.Hash(ref hasher);
        hasher.Add(_breeding.NextTry.Count);
        foreach (var (serial, next) in _breeding.NextTry)
        {
            hasher.Add(serial);
            hasher.Add(next);
        }

        hasher.Add(_sexes.Assigned.Count);
        foreach (var (serial, female) in _sexes.Assigned)
        {
            hasher.Add(serial);
            hasher.Add(female);
        }

        _beastmaster.Hash(ref hasher);
        _outposts.Hash(ref hasher);
        _fetching.Hash(ref hasher);
        _hunting.Hash(ref hasher);
        _tieUp.Hash(ref hasher);
        _capsules.Hash(ref hasher);
        hasher.Add(_workedAt);
        hasher.Add(_breedNow);
        hasher.Add(_pending?.Count ?? -1);
        HashHitches(ref hasher);
    }

    /// <summary>
    /// The hitches on the characters (the engine hashes no mod jobs).
    /// </summary>
    void HashHitches(ref StateHasher hasher)
    {
        if (_world is not { } world) return;

        var table = world.Characters;
        for (int s = 0; s < table.HighWater; s++)
        {
            if (!table.Next[s].Alive || table.Cold(s) is not { } cold || Hitches.PostOf(cold) is not { } post) continue;

            hasher.Add(s);
            hasher.Add(Rng.StableHash(post));
        }
    }

    #endregion
}
