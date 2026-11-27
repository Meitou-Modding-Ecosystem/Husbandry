using Husbandry.Capsules;

namespace Husbandry.Tests;

/// <summary>
/// The Capsule Bench's operator stands in front of it: the game takes a machine's position marker from its use nodes only with two or more.
/// </summary>
public sealed class BenchTests
{
    [Fact]
    public void The_bench_has_two_distinct_use_nodes_in_front_of_its_table()
    {
        var nodes = BenchData.FrontNodes;

        Assert.Equal(2, nodes.Length);
        Assert.NotEqual(nodes[0], nodes[1]);
        Assert.All(nodes, n => Assert.True(n.Z > 4.4f, "a node sits in front of the table's front edge at local +Z 4.4"));
    }
}
