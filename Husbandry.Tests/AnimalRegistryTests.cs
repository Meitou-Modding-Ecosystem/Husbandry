using AnimalBehaviors;
using Meitou.Data.Gameplay.Ai;
using Meitou.Data.Gameplay.Bodies;
using Meitou.Simulation.Bodies;

namespace Husbandry.Tests;

/// <summary>
/// The trainable skills dependents register (docs/depending-on-husbandry.md): one training job per skill, who may learn a skill (Animal
/// Behaviors' support and the skill's races), dropping a failed dependent and the end of registration.
/// </summary>
public sealed class AnimalRegistryTests
{
    /// <summary>
    /// A race as the data gives it.
    /// </summary>
    static RaceData Race(string id, string name) => new() { StringId = id, Name = name };

    /// <summary>
    /// A skill a dependent might add.
    /// </summary>
    static ModSkill Skill(string id, string label) => new(id, label, SkillColumn.Sciences);

    /// <summary>
    /// A registry on a support list where goats only breed and everything else does all.
    /// </summary>
    static AnimalRegistry Registry() => new(race => race.Name == "Goat" ? AnimalSupport.Breeding : AnimalSupport.All);

    [Fact]
    public void A_skill_maps_its_training_job_and_a_type_or_id_is_taken_once()
    {
        var registry = Registry();
        var herding = registry.AddSkill("ranch.mod", Skill("ranch.herd", "Beast: Herd"), (TaskType)4700, null);

        Assert.Same(herding, registry.SkillOf((TaskType)4700));
        Assert.Contains((TaskType)4700, registry.TrainingTypes);
        Assert.Throws<InvalidOperationException>(() => registry.AddSkill("b.mod", Skill("ranch.herd", "Herd"), (TaskType)4701, null));
        Assert.Throws<InvalidOperationException>(() => registry.AddSkill("b.mod", Skill("b.x", "X"), (TaskType)4700, null));
    }

    [Fact]
    public void A_skill_with_a_race_list_is_learnt_only_by_those_races_that_train()
    {
        var registry = Registry();
        var swim = registry.AddSkill("sea.mod", Skill("sea.swim", "Beast: Swim"), (TaskType)4800, ["Crab", "1-x.mod"]);
        var any = registry.AddSkill("sea.mod", Skill("sea.any", "Any"), (TaskType)4801, null);

        Assert.True(registry.MayLearn(Race("5-x.mod", "crab"), swim));
        Assert.True(registry.MayLearn(Race("1-x.mod", "Leviathan"), swim));
        Assert.False(registry.MayLearn(Race("6-x.mod", "Garru"), swim));
        Assert.True(registry.MayLearn(Race("6-x.mod", "Garru"), any));
        Assert.False(registry.MayLearn(Race("8-x.mod", "Goat"), any));
    }

    [Fact]
    public void A_failed_dependent_is_dropped()
    {
        var registry = Registry();
        registry.AddSkill("bad.mod", Skill("bad.x", "X"), (TaskType)4900, null);

        registry.Drop("bad.mod");

        Assert.Empty(registry.Skills);
        Assert.Empty(registry.TrainingTypes);
        registry.AddSkill("good.mod", Skill("good.x", "X"), (TaskType)4900, null);
    }

    [Fact]
    public void Nothing_registers_once_the_first_world_is_built()
    {
        var registry = Registry();
        registry.Freeze();

        Assert.Throws<InvalidOperationException>(() => registry.AddSkill("late.mod", Skill("late.x", "X"), (TaskType)4950, null));
    }

    [Theory]
    [InlineData("Beast: Fetch", "Fetch")]
    [InlineData("Herding", "Herding")]
    [InlineData("Ranch:Herd ", "Herd")]
    public void A_skill_is_named_by_the_label_after_its_colon(string label, string name) => Assert.Equal(name, TrainableSkill.ShortOf(label));
}
