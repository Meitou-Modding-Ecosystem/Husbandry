using Meitou.Data.Gameplay.Bodies;
using Meitou.Modding;
using Meitou.Simulation;
using Meitou.Simulation.Bodies;

namespace Husbandry.Training;

/// <summary>
/// The skills an animal learns (docs/husbandry.md "Training"): six mod skills in the stats window, each 0 until trained at a Training Post. A
/// skill is unlocked at <see cref="Unlocked"/>; below it the animal refuses the work that needs it. Also the status effects the skills give.
/// </summary>
public static class AnimalSkills
{
    /// <summary>
    /// The level from which a skill is unlocked.
    /// </summary>
    public const float Unlocked = 10;

    /// <summary>
    /// Fetch: carries a storage's goods from its machines (<see cref="Fetching"/>).
    /// </summary>
    public static readonly ModSkill Fetch = Skill("husbandry.fetch", "Beast: Fetch", SkillColumn.Sciences,
        "Carries goods from your machines to a storage, in its pack. Give it the Fetch job on a storage.", "Fetching goods");

    /// <summary>
    /// Guard: fights better near its Pet Station (<see cref="Guarding"/>).
    /// </summary>
    public static readonly ModSkill Guard = Skill("husbandry.guard", "Beast: Guard", SkillColumn.Combat,
        "Lives at a Pet Station and fights harder near it (+30 attack and defence within 2000 units).", "Guarding its station");

    /// <summary>
    /// Hunt: roams from a Bone Pile and brings meat back (<see cref="Hunting"/>).
    /// </summary>
    public static readonly ModSkill Hunt = Skill("husbandry.hunt", "Beast: Hunt", SkillColumn.Sciences,
        "Works a Bone Pile: roams up to 12000 units from it and comes back with raw meat, more and more often as it learns.", "Hunting");

    /// <summary>
    /// Scout: may sneak, and sneaks better (<see cref="PassiveSkills"/>).
    /// </summary>
    public static readonly ModSkill Scout = Skill("husbandry.scout", "Beast: Scout", SkillColumn.Thievery,
        "Lets the animal sneak (untrained animals stop sneaking at once), with up to +30 stealth.", "Sneaking");

    /// <summary>
    /// Pack: carries more (<see cref="PassiveSkills"/>).
    /// </summary>
    public static readonly ModSkill Pack = Skill("husbandry.pack", "Beast: Pack", SkillColumn.Sciences,
        "Up to +30 strength, so it carries more and slows less under a load.", "Carrying a load");

    /// <summary>
    /// Tie up: its bites can knock a foe down for a moment (<see cref="TieUp"/>).
    /// </summary>
    public static readonly ModSkill TieUp = Skill("husbandry.tieup", "Beast: Tie up", SkillColumn.Combat,
        "Its hits may knock the foe down for up to 2 seconds; the chance and the time grow with the skill.", "Landing hits in a fight");

    /// <summary>
    /// Every skill, in the order the Training Post offers them.
    /// </summary>
    public static readonly ModSkill[] All = [Fetch, Guard, Hunt, Scout, Pack, TieUp];

    /// <summary>
    /// The guard bonus: +30 melee attack and defence while near its station.
    /// </summary>
    public static readonly StatusEffect Guarding = new("husbandry.guarding", HusbandryText.Get("Guarding home"))
    {
        Description = HusbandryText.Get("Near its Pet Station: +30 melee attack and defence."),
        Tone = StatusEffectTone.Good,
        Modifiers = [new(StatsEnumerated.MeleeAttack, 30), new(StatsEnumerated.MeleeDefence, 30)],
    };

    /// <summary>
    /// The pack bonus: +3 strength a stack, a stack per 10 levels of Pack.
    /// </summary>
    public static readonly StatusEffect PackTrained = new("husbandry.pack", HusbandryText.Get("Pack trained"))
    {
        Description = HusbandryText.Get("Trained to carry: +3 strength for every 10 levels of Pack."),
        Tone = StatusEffectTone.Good,
        Stacking = StatusEffectStacking.Stack,
        MaxStacks = 10,
        Modifiers = [new(StatsEnumerated.Strength, 3)],
    };

    /// <summary>
    /// The scout bonus: +3 stealth a stack, a stack per 10 levels of Scout.
    /// </summary>
    public static readonly StatusEffect ScoutTrained = new("husbandry.scout", HusbandryText.Get("Scout trained"))
    {
        Description = HusbandryText.Get("Trained to sneak: +3 stealth for every 10 levels of Scout."),
        Tone = StatusEffectTone.Good,
        Stacking = StatusEffectStacking.Stack,
        MaxStacks = 10,
        Modifiers = [new(StatsEnumerated.Stealth, 3)],
    };

    /// <summary>
    /// Registers the effects (the skills are registered as trainable: <see cref="HusbandryMod"/>).
    /// </summary>
    public static void Register(ModContext c)
    {
        c.AddStatusEffect(Guarding);
        c.AddStatusEffect(PackTrained);
        c.AddStatusEffect(ScoutTrained);
    }

    /// <summary>
    /// A character's level in a skill; 0 without stats.
    /// </summary>
    public static float Level(CharacterCold cold, ModSkill skill) => cold.Stats is { } stats ? skill.Level(stats) : 0;

    /// <summary>
    /// Whether a character has unlocked a skill.
    /// </summary>
    public static bool Has(CharacterCold cold, ModSkill skill) => Level(cold, skill) >= Unlocked;

    /// <summary>
    /// Trains a skill on the game's curve.
    /// </summary>
    public static void Gain(CharacterCold cold, ModSkill skill, float amount)
    {
        if (cold.Stats is { } stats && amount > 0) skill.Gain(stats, amount);
    }

    /// <summary>
    /// Keeps a stacking effect at one stack per 10 levels of a skill (none below 10), for as long as the skill holds.
    /// </summary>
    public static void Stacks(World world, CharacterId id, CharacterCold cold, StatusEffect effect, ModSkill skill)
    {
        int want = Math.Min(effect.MaxStacks, (int)(Level(cold, skill) / 10));
        int have = cold.Stats is { } stats ? effect.On(stats)?.Stacks ?? 0 : 0;
        if (want == have) return;

        effect.Remove(world, id);
        if (want > 0) effect.Apply(world, id, double.PositiveInfinity, want);
    }

    /// <summary>
    /// A skill that starts at 0.
    /// </summary>
    static ModSkill Skill(string id, string label, SkillColumn column, string description, string trainedBy) =>
        new(id, HusbandryText.Format("Beast: {1}", HusbandryText.Get(TrainableSkill.ShortOf(label))), column)
        {
            Description = HusbandryText.Get(description),
            Training = [HusbandryText.Get("A Training Post"), HusbandryText.Get(trainedBy)],
            Start = 0,
        };
}
