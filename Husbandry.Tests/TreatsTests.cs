using Husbandry.Training;
using Meitou.Simulation.Items;

namespace Husbandry.Tests;

/// <summary>
/// The Training Post's treats (docs/husbandry.md "Training"): Raw Meat kept in the post's own storage, which the trainer restocks by moving meat
/// between inventories.
/// </summary>
public sealed class TreatsTests
{
    /// <summary>
    /// The meat record used here.
    /// </summary>
    const string Meat = "meat";

    /// <summary>
    /// A stack of <paramref name="quantity"/> of <paramref name="record"/>, stacking to 99.
    /// </summary>
    static ItemInstance Stack(string record, int quantity) => new() { Record = record, Quantity = quantity, StackLimit = 99 };

    /// <summary>
    /// Moving fewer than a stack holds splits it: the rest stays behind, and other items are left alone. Catches a trainer emptying a whole storage
    /// into its bag, or carrying off something that is not meat.
    /// </summary>
    [Fact]
    public void Moving_part_of_a_stack_splits_it_and_leaves_other_items()
    {
        var from = new Inventory();
        var to = new Inventory();
        Assert.True(from.Add(Stack(Meat, 30)));
        Assert.True(from.Add(Stack("bread", 5)));

        int moved = Treats.Move(from, to, Meat, 12);

        Assert.Equal(12, moved);
        Assert.Equal(12, Treats.Count(to, Meat));
        Assert.Equal(18, Treats.Count(from, Meat));
        Assert.Equal(5, from.Searched(false).Where(i => i.Record == "bread").Sum(i => i.Quantity));
        Assert.DoesNotContain(to.Searched(false), i => i.Record == "bread");
    }

    /// <summary>
    /// Asking for more than there is moves all of it and says how much; asking for none moves nothing.
    /// </summary>
    [Fact]
    public void Moving_more_than_there_is_moves_all_of_it()
    {
        var from = new Inventory();
        var to = new Inventory();
        Assert.True(from.Add(Stack(Meat, 7)));

        Assert.Equal(0, Treats.Move(from, to, Meat, 0));
        Assert.Equal(7, Treats.Move(from, to, Meat, 20));
        Assert.Equal(0, Treats.Count(from, Meat));
        Assert.Equal(7, Treats.Count(to, Meat));
    }

    /// <summary>
    /// What has no room in the target stays where it was: no meat is lost when the bag is full.
    /// </summary>
    [Fact]
    public void Meat_without_room_stays_behind()
    {
        var from = new Inventory();
        var to = new Inventory(1, 1);
        Assert.True(from.Add(Stack(Meat, 150)));

        int moved = Treats.Move(from, to, Meat, 150);

        Assert.Equal(Treats.Count(to, Meat), moved);
        Assert.Equal(150, Treats.Count(from, Meat) + Treats.Count(to, Meat));
    }

    /// <summary>
    /// The post's storage multiplier makes one stack of Raw Meat hold <see cref="TrainingData.PostMeat"/>, whatever the meat stacks to.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(40)]
    public void The_post_storage_holds_one_stack_of_twenty(int stackable)
    {
        Assert.Equal(TrainingData.PostMeat, stackable * TrainingData.StackMult(stackable), 3);
    }
}
