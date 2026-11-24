using System.Globalization;
using System.Text;
using AnimalBehaviors;
using Husbandry.Capsules;
using Husbandry.Training;
using Meitou.Data.Gameplay.Combat;
using Meitou.Modding;
using Meitou.Simulation;
using Meitou.Simulation.Ai;
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
public sealed class HusbandrySystem : ITickSystem, IStateHashed, IModSaveData, ICombatHitObserver, IChooserOwner
{
    #region Ticking

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
    /// Animal Behaviors in this world: the sexes and the hitching posts' room.
    /// </summary>
    readonly IAnimalState _animals;

    /// <summary>
    /// The animals' sexes (Animal Behaviors').
    /// </summary>
    readonly Sexes _sexes;

    /// <summary>
    /// Animal Behaviors' pen settings, for the animals working at Husbandry's posts; copied at each check.
    /// </summary>
    readonly AnimalSettings _pens;

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
    /// The bars under the buildings' Condition.
    /// </summary>
    public BuildingStatus Status { get; } = new();

    /// <summary>
    /// The animals at each Hitching Post, for its window.
    /// </summary>
    public PostRoster Roster { get; } = new();

    /// <summary>
    /// The Training Post's trainers.
    /// </summary>
    readonly Trainers _trainers;

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
    public HusbandrySystem(SystemBuildContext context, HusbandrySettings settings, AnimalSettings pens, IAnimalState animals, AnimalRegistry registry,
        RngPurpose purpose, Action<string> log, CombatTechnique? throwMove)
    {
        _settings = settings;
        _pens = pens;
        _animals = animals;
        _registry = registry;
        _purpose = purpose;
        _population = context.Find<PopulationSystem>();
        _sexes = animals.Sexes;
        _breeding = new Breeding(purpose, animals, registry, log);
        _beastmaster = new Beastmaster(_population, _sexes, log, TraderPost.Mourn);
        _outposts = new OutpostTraders(_population, _sexes, log);
        _fetching = new Fetching(_population);
        _hunting = new Hunting(_population, log);
        _tieUp = new TieUp(purpose);
        _trainers = new Trainers(registry);
        _capsules = new CapsuleSystem(_population, _sexes, registry, log);
        _throws = new CapsuleThrows(_capsules, HusbandryData.Capsule, throwMove, purpose);
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

    public void Register(ChooserRegistry chooser) => chooser.Jobs(Trainers.Type, _trainers);

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
        var settings = _settings.Copy();
        _tieUp.Apply(world);
        _trainers.Step(world);
        _capsules.Run(world);
        _throws.Run(world);
        if (world.Tick % TraderEveryTicks == 0) RunTraders(world, settings);
        if (world.Tick % TraderEveryTicks == 0) RunTraining(world, settings);

        if (!_breedNow && world.Tick % BreedEveryTicks != 0) return;

        var hitched = Hitches.All(world);
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
        var pens = _pens.Copy();
        var lessons = TrainingPost.Run(world, _registry, pens, _purpose, hours, _hunting.MeatId);
        Guarding.Run(world, pens, _purpose, hours);
        _fetching.Run(world);
        _hunting.Run(world, pens, _purpose);
        PassiveSkills.Run(world, hours);
        Status.Publish(world, lessons, _hunting, _breeding, _sexes, settings);
        Roster.Publish(world, _animals, _breeding, pens.PenRadius);
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
    /// The sidecar layout's version. Version 5 moved the hitches and the set sexes to Animal Behaviors' sidecar; an older save's are handed to it.
    /// </summary>
    public const int SaveVersion = 5;

    public void Write(Stream stream)
    {
        using var w = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        w.Write(SaveVersion);
        w.Write(_breeding.NextTry.Count);
        foreach (var (serial, next) in _breeding.NextTry)
        {
            w.Write(serial);
            w.Write(next);
        }

        _beastmaster.Write(w);
        _outposts.Write(w);
        _capsules.Write(w);
    }

    public void Read(Stream stream)
    {
        using var r = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        int version = r.ReadInt32();
        if (version is < 1 or > SaveVersion) return;

        if (version < 5) ReadHitches(r);
        _breeding.NextTry.Clear();
        int clocks = r.ReadInt32();
        for (int k = 0; k < clocks; k++) _breeding.NextTry[r.ReadUInt32()] = r.ReadDouble();

        if (version < 2) return;

        if (version < 5) ReadSexes(r);
        _beastmaster.Read(r);
        if (version >= 3) _outposts.Read(r);
        if (version >= 4) _capsules.Read(r);
    }

    /// <summary>
    /// Hands an older save's hitches to Animal Behaviors, which puts them back at its next tick.
    /// </summary>
    void ReadHitches(BinaryReader r)
    {
        var hitches = new List<(uint, string)>();
        int count = r.ReadInt32();
        for (int k = 0; k < count; k++) hitches.Add((r.ReadUInt32(), r.ReadString()));
        _animals.ImportHitches(hitches);
    }

    /// <summary>
    /// Hands an older save's set sexes to Animal Behaviors (version 2 to 4).
    /// </summary>
    void ReadSexes(BinaryReader r)
    {
        int count = r.ReadInt32();
        for (int k = 0; k < count; k++) _sexes.Assigned[r.ReadUInt32()] = r.ReadBoolean();
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

        _beastmaster.Hash(ref hasher);
        _outposts.Hash(ref hasher);
        _fetching.Hash(ref hasher);
        _hunting.Hash(ref hasher);
        _tieUp.Hash(ref hasher);
        _capsules.Hash(ref hasher);
        hasher.Add(_workedAt);
        hasher.Add(_breedNow);
    }

    #endregion
}
