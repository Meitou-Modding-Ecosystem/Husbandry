using Husbandry.Training;

namespace Husbandry.Tests;

/// <summary>
/// Every task type Husbandry claims is in the README's <c>## Numbers</c> list, which <see cref="NumbersTests"/> keeps inside the block.
/// </summary>
public sealed class ClaimedNumbersTests
{
    [Fact]
    public void Every_claimed_task_type_is_listed_in_the_readme()
    {
        var listed = NumbersTests.Listed();
        int[] claimed =
        [
            (int)Hitches.Type, (int)Fetching.Type, (int)Guarding.Type, (int)Hunting.Type,
            .. Enumerable.Range(0, AnimalSkills.All.Length).Select(i => (int)TrainingPost.TypeOf(i)),
        ];

        Assert.All(claimed, n => Assert.Contains(n, listed));
    }
}
