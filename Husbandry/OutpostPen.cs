using System.Numerics;
using Meitou.Simulation;
using Meitou.Simulation.Construction;
using Meitou.Simulation.Population;

namespace Husbandry;

/// <summary>
/// Where a Beastmaster the debug menu put at a player settlement stands and pens his stock (docs/husbandry.md "Debug rows"): a patch of even,
/// open ground on a ring round the settlement's centre, his spot between it and the centre. His platoons are homed at the town site nearest the
/// settlement (a mod platoon needs one), so the buying window, which lists the animals of the seller's own town, lists his stock.
/// </summary>
public static class OutpostPen
{
    /// <summary>
    /// How far from the settlement's centre the pen's centre is tried, in game units, nearest first after the first.
    /// </summary>
    static readonly float[] Rings = [150, 100, 220, 300];

    /// <summary>
    /// The directions round the centre tried on each ring.
    /// </summary>
    const int Directions = 8;

    /// <summary>
    /// How much the ground may rise and fall across the pen, in game units.
    /// </summary>
    const float MaxSpread = 25;

    /// <summary>
    /// How far from the pen's edge, towards the settlement's centre, he stands.
    /// </summary>
    const float CounterGap = 20;

    /// <summary>
    /// A post for the settlement numbered <paramref name="number"/> (<see cref="PlayerTown.Number"/>), named <paramref name="name"/> in the log.
    /// </summary>
    public static TraderPost Post(int number, string name) =>
        new($"outpost.{number}", $"{name} (settlement {number})", (world, data, radius) => Find(world, data, number, radius));

    /// <summary>
    /// The place at the settlement; null while it is gone, its ground is not loaded or no patch round it is open.
    /// </summary>
    public static BarPlace? Find(World world, PopulationData data, int number, float penRadius)
    {
        if (world.Buildings.Towns.FirstOrDefault(t => t.Number == number) is not { } town || Nearest(data, town.Position) is not { } site) return null;

        var centre = new Vector2(town.Position.X, town.Position.Z);
        if (Pen(world, centre, penRadius) is not { } pen) return null;

        var (spot, floor) = pen;
        var toCentre = Vector2.Normalize(centre - spot);
        var counter2 = spot + toCentre * (penRadius + CounterGap);
        var counter = new Vector3(counter2.X, world.Walkability.GroundHeight(counter2.X, counter2.Y, floor), counter2.Y);
        return new BarPlace(site, counter, spot, floor, counter);
    }

    /// <summary>
    /// The town site nearest a point; null when the world has none.
    /// </summary>
    static TownSite? Nearest(PopulationData data, Vector3 at) => data.Sites.MinBy(s => Vector3.DistanceSquared(s.Position, at));

    /// <summary>
    /// The first pen centre on the rings whose ground is walkable and even, with its floor (the highest ground across it); null when none is.
    /// </summary>
    static (Vector2 Spot, float Floor)? Pen(World world, Vector2 centre, float penRadius)
    {
        foreach (float ring in Rings)
        {
            for (int k = 0; k < Directions; k++)
            {
                float angle = k * MathF.Tau / Directions;
                var spot = centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ring;
                if (Floor(world, spot, penRadius) is { } floor) return (spot, floor);
            }
        }

        return null;
    }

    /// <summary>
    /// The highest ground across a pen at a spot (its centre and eight points on its edge), or null when one is not walkable or the ground rises
    /// and falls more than <see cref="MaxSpread"/>.
    /// </summary>
    static float? Floor(World world, Vector2 spot, float penRadius)
    {
        var walk = world.Walkability;
        float low = float.MaxValue, high = float.MinValue;
        for (int k = 0; k <= Directions; k++)
        {
            float angle = k * MathF.Tau / Directions;
            var at = k == Directions ? spot : spot + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * penRadius;
            if (!walk.IsWalkable(at.X, at.Y)) return null;

            float height = walk.GroundHeight(at.X, at.Y);
            low = Math.Min(low, height);
            high = Math.Max(high, height);
        }

        return high - low <= MaxSpread ? high : null;
    }
}
