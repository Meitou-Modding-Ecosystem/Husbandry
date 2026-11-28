using System.Numerics;
using AnimalBehaviors;
using Husbandry.Capsules;
using Husbandry.Training;
using Meitou.Data.Fcs;
using Meitou.Data.Gameplay.Ai;
using Meitou.Data.Gameplay.Bodies;
using Meitou.Data.Gameplay.Combat;
using Meitou.Modding;
using Meitou.Simulation;
using Meitou.Simulation.Bodies;
using Meitou.Simulation.Production;

namespace Husbandry;

/// <summary>
/// Husbandry's entry (docs/husbandry.md), built on Animal Behaviors (the supported animals, sexes and the Hitched job its Hitching Post offers):
/// the system that breeds, trains and runs the Beastmaster of Mourn (and any the debug menu puts at a settlement), the capsules, the settings
/// and the debug rows. Its records are added in the data phase from the loaded game's own (<see cref="HusbandryData"/>), so the shipped
/// Husbandry.mod is empty. Also the framework other mods extend with trainable skills (<see cref="IHusbandryApi"/>, docs/depending-on-husbandry.md).
/// </summary>
public sealed class HusbandryMod : IMod, IModDataPhase, IHusbandryApi, IModDependents
{
    /// <summary>
    /// The mod's manifest id.
    /// </summary>
    public const string Id = "husbandry";

    /// <summary>
    /// The player's settings.
    /// </summary>
    public HusbandrySettings Settings { get; } = new();

    /// <summary>
    /// The system of the latest world, for the debug rows.
    /// </summary>
    HusbandrySystem? _latest;

    /// <summary>
    /// The trainable skills, Husbandry's own and its dependents', and what each animal may do (Animal Behaviors' list).
    /// </summary>
    readonly AnimalRegistry _registry;

    /// <summary>
    /// Animal Behaviors, the framework Husbandry builds on: the supported animals, sexes and the Hitched job.
    /// </summary>
    IAnimalBehaviorsApi? _animals;

    /// <summary>
    /// The mod, its registry reading support from Animal Behaviors once <see cref="Initialize"/> has it.
    /// </summary>
    public HusbandryMod() => _registry = new AnimalRegistry(race => _animals?.SupportOf(race) ?? AnimalSupport.All);

    /// <summary>
    /// The throw's COMBAT_TECHNIQUE (<see cref="ThrowData"/>); null when the data phase did not add it (the throw then plays nothing).
    /// </summary>
    CombatTechnique? _throwMove;

    /// <summary>
    /// The trainers' drill COMBAT_TECHNIQUE (<see cref="ThrowData.DrillTechnique"/>); null when the data phase did not add it (trainers then only
    /// turn to their animals).
    /// </summary>
    CombatTechnique? _drillMove;

    /// <summary>
    /// The races that link Husbandry's animation file (the drill's debug row says whether a trainer's does).
    /// </summary>
    IReadOnlySet<string> _drillRaces = new HashSet<string>();

    /// <summary>
    /// The mist where a thrown capsule lands: the game's white poison gas (EFFECT 62780-Newwworld.mod) tinted pink, for <see cref="MistSeconds"/>.
    /// </summary>
    const string MistEffect = "Poison_Gas_White";

    /// <summary>
    /// The mist's tint (rgb, 1 the full channel).
    /// </summary>
    static readonly Vector3 MistTint = new(1, 0.35f, 0.75f);

    /// <summary>
    /// How long the mist emits, in seconds.
    /// </summary>
    const float MistSeconds = 3;

    /// <summary>
    /// The thrown capsules' flights, drawn once a frame (none before <see cref="Initialize"/>).
    /// </summary>
    CapsuleFlights? _flights;

    /// <summary>
    /// Adds the mod's records, built from the merged game data (docs/husbandry.md "Data file"). Throws, so the game leaves the mod out, when the
    /// game lacks a record they copy.
    /// </summary>
    public void EditData(ModDataContext c)
    {
        foreach (var record in HusbandryData.Build(c.Data).Records) c.Apply(record);
        c.Log("husbandry: animations linked to " + ThrowData.LinkRaces(c) + " races on the human skeletons");
    }

    public void Initialize(ModContext c)
    {
        _animals = c.Dependency<IAnimalBehaviorsApi>(AnimalBehaviorsMod.Id)
            ?? throw new InvalidOperationException("Husbandry needs Animal Behaviors (animal.behaviors).");
        c.AddSettings(Settings);
        if (c.Data.Find(HusbandryData.Capsule) is null) c.LogError("Husbandry's records are missing: the data phase did not add them (see the log).");

        c.Harmony.PatchAll(typeof(HusbandryMod).Assembly);
        AddTraining(c);
        c.AddItemUse(HusbandryData.Capsule, use => _latest?.Capsules.Use(use) ?? false);
        c.AddItemUse(HusbandryData.FilledCapsule, use => _latest?.Capsules.Use(use) ?? false);
        _throwMove = c.Data.Find(ThrowData.Technique) is { } move ? CombatTechnique.From(move) : null;
        c.AddCombatMove((world, id, tick) => _latest?.Throws.Move(world, id, tick));
        _drillMove = c.Data.Find(ThrowData.DrillTechnique) is { } drill ? CombatTechnique.From(drill) : null;
        c.Log(_drillMove is null
            ? "husbandry: training drill technique " + ThrowData.DrillTechnique + " is missing: trainers only turn to their animals"
            : "husbandry: training drill plays clip " + _drillMove.AnimName);
        c.AddCombatMove((world, id, tick) => _latest?.Drills.Move(world, id, tick));
        _drillRaces = ThrowData.LinkedRaces(c.Data);
#if MEITOU_BUILDING_ROWS
        c.AddUi(new StatusUi(() => _latest?.Status));
#endif
        c.AddUi(new HitchingPostWindow(() => _latest?.Roster));
        c.AddUi(new CapsuleUi(() => _latest?.Throws, () => _latest?.Capsules, at => c.PlayEffect(MistEffect, at, MistSeconds, MistTint)));
        _flights = new CapsuleFlights(at => c.PlayEffect(MistEffect, at, CapsuleFlights.PuffSeconds, CapsuleFlights.Tint));
        c.AddFrame(frame => _flights.Draw(frame, _latest?.Throws));
        c.AddSystem("husbandry", ctx => _latest = Build(c, ctx), SystemAnchor.End);
        c.AddDebugCommand("Husbandry: hitched animals", _ => Told(c, _latest?.View ?? NoWorld));
        c.AddDebugCommand("Husbandry: Beastmaster", _ => Told(c, _latest?.TraderView ?? NoWorld));
        c.AddDebugCommand("Husbandry: capsules", _ => Told(c, _latest?.CapsuleView ?? NoWorld));
        c.AddDebugCommand("Husbandry: training drills", _ => Told(c, _latest?.Drills.View ?? NoWorld));
        c.AddDebugCommand(SettlementTraderRow.Label, d => Told(c, SettlementTraderRow.Run(d, _latest?.Outposts)), hasField: true);
        c.AddDebugCommand("Husbandry: try breeding now", d =>
        {
            d.Enqueue(new BreedNowCommand { Tick = d.Tick });
            return "Every paired female tries at the next check.";
        });
    }

    /// <summary>
    /// A world's system, on that world's Animal Behaviors state (its factory runs first); the first ends registration.
    /// </summary>
    HusbandrySystem Build(ModContext c, SystemBuildContext context)
    {
        _registry.Freeze();
        var animals = _animals!.Current ?? throw new InvalidOperationException("Animal Behaviors built no state for this world.");
        return new HusbandrySystem(context, Settings, _animals.Settings, animals, _registry, c.RngPurpose, c.Log, _throwMove, _drillMove,
            ThrowData.DrillSeconds, _drillRaces);
    }

    /// <summary>
    /// The animal skills (docs/husbandry.md "Training"): the skills and their effects, a training job per skill and the people's trainer job (with
    /// its Animal Handling skill) on the Training Post, and the work jobs: Fetch on every storage, Guard on the Pet Station, Hunt on the Bone Pile.
    /// </summary>
    void AddTraining(ModContext c)
    {
        AnimalSkills.Register(c);
        var own = For(c);
        foreach (var (index, skill) in AnimalSkills.All.Index()) own.AddSkill(skill, TrainingPost.TypeOf(index));

        c.AddSkill(Trainers.Handling);
        c.AddJobType(Trainers.Type, HusbandryText.Get(Trainers.Name));
        c.AddJobOffer(Trainers.Offer);

        c.AddJobType(Fetching.Type, HusbandryText.Get(Fetching.Name));
        c.AddJobOffer(Fetching.Offer(c.Data, c.Records((int)FcsRecordType.Building)));
        c.AddJobType(Guarding.Type, HusbandryText.Get(Guarding.Name));
        c.AddJobOffer(Guarding.Offer);
        c.AddJobType(Hunting.Type, HusbandryText.Get(Hunting.Name));
        c.AddJobOffer(Hunting.Offer);
    }

    #region Framework

    public IHusbandryRegistry For(ModContext c) => new Registration(this, c);

    public IReadOnlyList<TrainableSkill> Skills => _registry.Skills;

    public void DependentFailed(string modId) => _registry.Drop(modId);

    /// <summary>
    /// One mod's registrations: kinds go to the registry; a skill is registered with the game through the mod's own context, so it is the mod's.
    /// </summary>
    sealed class Registration(HusbandryMod husbandry, ModContext c) : IHusbandryRegistry
    {
        public TrainableSkill AddSkill(ModSkill skill, TaskType trainingType, IReadOnlyCollection<string>? races = null)
        {
            if (husbandry._registry.Frozen) throw new InvalidOperationException("Husbandry takes registrations only in a mod's Initialize.");

            string name = TrainableSkill.ShortOf(skill.Label);
            c.AddSkill(skill);
            c.AddJobType(trainingType, HusbandryText.Format("Training {1}", HusbandryText.Get(name)));
            c.AddJobOffer(new ModJobOffer(trainingType, [HusbandryData.TrainingPost], HusbandryText.Format("Train: {1}", HusbandryText.Get(name)), ModJobTakers.Animals));
            return husbandry._registry.AddSkill(c.Id, skill, trainingType, races);
        }
    }

    #endregion

    /// <summary>
    /// Logs a debug answer too (the status line shows only its start).
    /// </summary>
    static string Told(ModContext c, string answer)
    {
        c.Log(answer);
        return answer;
    }

    /// <summary>
    /// The debug answer before the first world.
    /// </summary>
    const string NoWorld = "no world yet";
}
