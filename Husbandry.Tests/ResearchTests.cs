using Meitou.Data.Gameplay.Construction;

namespace Husbandry.Tests;

/// <summary>
/// The Animal Husbandry research enables the Beast Capsule's craft: a bench lists in the Crafting tab, and makes, only the crafts a finished research
/// has enabled.
/// </summary>
public sealed class ResearchTests
{
    [Fact]
    public void The_research_enables_the_capsule_craft_through_a_field_the_engine_reads()
    {
        var research = HusbandryData.ResearchRecord();

        Assert.Contains(ResearchData.EnableFields, f => f.Field == "enable item" && f.Kind == CraftKind.Item);
        Assert.Contains(research.References["enable item"], r => r.TargetStringId == HusbandryData.Capsule);
    }

    [Fact]
    public void The_research_still_unlocks_the_capsule_bench()
    {
        var research = HusbandryData.ResearchRecord();

        Assert.Contains(research.References["enable buildings"], r => r.TargetStringId == Capsules.BenchData.Bench);
    }
}
