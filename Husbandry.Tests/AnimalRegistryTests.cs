using Meitou.Data.Gameplay.Ai;
using Meitou.Data.Gameplay.Bodies;
using Meitou.Simulation.Bodies;

namespace Husbandry.Tests;

/// <summary>
/// The list of supported animals and the trainable skills dependents register (docs/husbandry.md "Supported animals",
/// docs/depending-on-husbandry.md): which registration wins, the built-in rules, dropping a failed dependent and the end of registration.
/// </summary>
public sealed class AnimalRegistryTests
{
    /// <summary>
    /// A race as the data gives it.
    /// </summary>
    static RaceData Race(string id, string name, bool robot = false) => new() { StringId = id, Name = name, IsRobot = robot };

    /// <summary>
    /// A skill a dependent might add.
    /// </summary>
    static ModSkill Skill(string id, string label) => new(id, label, SkillColumn.Sciences);

    [Fact]
    public void An_unlisted_animal_supports_everything_and_a_robot_only_training()
    {
        var registry = new AnimalRegistry();

        Assert.Equal(AnimalSupport.All, registry.SupportOf(Race("42-x.mod", "Garru")));
        Assert.Equal(AnimalSupport.Training, registry.SupportOf(Race("43-x.mod", "Robot Spider", robot: true)));
    }

    [Fact]
    public void The_leviathan_breeds_and_trains_but_fits_no_capsule()
    {
        var registry = new AnimalRegistry();

        Assert.Equal(AnimalSupport.Breeding | AnimalSupport.Training, registry.SupportOf(Race("1-x.mod", "leviathan")));
    }

    [Fact]
    public void The_last_registration_wins_and_an_id_beats_a_name()
    {
        var registry = new AnimalRegistry();
        registry.AddKind("a.mod", new AnimalKind("Garru", AnimalSupport.Breeding));
        registry.AddKind("b.mod", new AnimalKind("GARRU", AnimalSupport.Training));
        registry.AddKind("c.mod", new AnimalKind("42-x.mod", AnimalSupport.Taming));

        Assert.Equal(AnimalSupport.Training, registry.SupportOf(Race("7-other.mod", "Garru")));
        Assert.Equal(AnimalSupport.Taming, registry.SupportOf(Race("42-x.mod", "Garru")));
    }

    [Fact]
    public void A_registration_overrides_the_robot_rule_and_the_built_in_list()
    {
        var registry = new AnimalRegistry();
        registry.AddKind("robots.mod", new AnimalKind("Robot Spider", AnimalSupport.All));
        registry.AddKind("big.mod", new AnimalKind("Leviathan", AnimalSupport.None));

        Assert.Equal(AnimalSupport.All, registry.SupportOf(Race("43-x.mod", "Robot Spider", robot: true)));
        Assert.Equal(AnimalSupport.None, registry.SupportOf(Race("1-x.mod", "Leviathan")));
    }

    [Fact]
    public void A_kind_records_its_owner_and_needs_a_race()
    {
        var registry = new AnimalRegistry();
        registry.AddKind("a.mod", new AnimalKind(" Garru ", AnimalSupport.Breeding) { Owner = "spoofed" });

        Assert.Equal(("a.mod", "Garru"), (registry.Kinds[^1].Owner, registry.Kinds[^1].Race));
        Assert.Throws<ArgumentException>(() => registry.AddKind("a.mod", new AnimalKind(" ", AnimalSupport.All)));
    }

    [Fact]
    public void A_skill_maps_its_training_job_and_a_type_or_id_is_taken_once()
    {
        var registry = new AnimalRegistry();
        var herding = registry.AddSkill("ranch.mod", Skill("ranch.herd", "Beast: Herd"), (TaskType)4700, null);

        Assert.Same(herding, registry.SkillOf((TaskType)4700));
        Assert.Contains((TaskType)4700, registry.TrainingTypes);
        Assert.Throws<InvalidOperationException>(() => registry.AddSkill("other.mod", Skill("other.x", "X"), (TaskType)4700, null));
        Assert.Throws<InvalidOperationException>(() => registry.AddSkill("other.mod", Skill("ranch.herd", "Y"), (TaskType)4701, null));
    }

    [Fact]
    public void A_skill_with_a_race_list_is_learnt_only_by_those_races_that_train()
    {
        var registry = new AnimalRegistry();
        var swim = registry.AddSkill("sea.mod", Skill("sea.swim", "Beast: Swim"), (TaskType)4800, ["Crab", "1-x.mod"]);
        var any = registry.AddSkill("sea.mod", Skill("sea.any", "Any"), (TaskType)4801, null);
        registry.AddKind("sea.mod", new AnimalKind("Goat", AnimalSupport.Breeding));

        Assert.True(registry.MayLearn(Race("5-x.mod", "crab"), swim));
        Assert.True(registry.MayLearn(Race("1-x.mod", "Leviathan"), swim));
        Assert.False(registry.MayLearn(Race("6-x.mod", "Garru"), swim));
        Assert.True(registry.MayLearn(Race("6-x.mod", "Garru"), any));
        Assert.False(registry.MayLearn(Race("8-x.mod", "Goat"), any));
    }

    [Fact]
    public void A_failed_dependent_is_dropped_and_the_built_in_list_stays()
    {
        var registry = new AnimalRegistry();
        registry.AddKind("bad.mod", new AnimalKind("Garru", AnimalSupport.None));
        registry.AddSkill("bad.mod", Skill("bad.x", "X"), (TaskType)4900, null);

        registry.Drop("bad.mod");

        Assert.Equal(AnimalSupport.All, registry.SupportOf(Race("42-x.mod", "Garru")));
        Assert.Empty(registry.Skills);
        Assert.Empty(registry.TrainingTypes);
        Assert.Equal(AnimalRegistry.BuiltIn, registry.Kinds);
        registry.AddSkill("good.mod", Skill("good.x", "X"), (TaskType)4900, null);
    }

    [Fact]
    public void Nothing_registers_once_the_first_world_is_built()
    {
        var registry = new AnimalRegistry();
        registry.Freeze();

        Assert.Throws<InvalidOperationException>(() => registry.AddKind("late.mod", new AnimalKind("Garru", AnimalSupport.None)));
        Assert.Throws<InvalidOperationException>(() => registry.AddSkill("late.mod", Skill("late.x", "X"), (TaskType)4950, null));
    }

    [Fact]
    public void The_list_names_each_race_once_in_order_with_its_support()
    {
        var registry = new AnimalRegistry();
        var races = new[] { ("2-x.mod", "Robot Spider", true), ("1-x.mod", "Garru", false), ("1-x.mod", "Garru", false), ("3-x.mod", "Leviathan", false) };

        Assert.Equal(
            ["Garru: breeding, training, taming, summoning", "Leviathan: breeding, training", "Robot Spider: training"],
            registry.Describe(races));
        Assert.Equal("nothing", AnimalRegistry.Words(AnimalSupport.None));
    }

    [Theory]
    [InlineData("Beast: Fetch", "Fetch")]
    [InlineData("Herding", "Herding")]
    [InlineData("Ranch:Herd ", "Herd")]
    public void A_skill_is_named_by_the_label_after_its_colon(string label, string name) => Assert.Equal(name, TrainableSkill.ShortOf(label));
}
