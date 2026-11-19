using System.Numerics;
using Meitou.Data.World;
using Meitou.Simulation;
using Meitou.Simulation.Population;

namespace Husbandry;

/// <summary>
/// Where the Beastmaster stands in Mourn's bar and where his pen lies behind it (docs/husbandry.md "The Beastmaster of Mourn").
/// </summary>
/// <param name="Site">Mourn's town site.</param>
/// <param name="Counter">His spot inside the bar.</param>
/// <param name="Pen">The pen's centre, on the far side of the bar from its doors.</param>
public sealed record BarPlace(TownSite Site, Vector3 Counter, Vector2 Pen);

/// <summary>
/// Finds Mourn's bar in the running world: the home of the town's "Bar Mourn" residents (found at run time, since the town's housing picks the
/// building), and from its doors the spot behind it for the pen. Known only while the town's zone is loaded (its doors are registered then).
/// </summary>
public static class MournBar
{
    /// <summary>
    /// Mourn's TOWN record (its editor name is "Capital") and the SQUAD_TEMPLATE of the barman's squad, whose home is the bar.
    /// </summary>
    public const string Town = "963-gamedata.base", BarResidents = "95554-Mourn.mod";

    /// <summary>
    /// How far beyond the doors' distance from the bar's origin the pen's centre lies, round the back, in game units.
    /// </summary>
    const float PenGap = 90;

    /// <summary>
    /// Extra distances tried when the first spot is not open ground. The doors' distance can be near nothing (a bar with doors on more than one side
    /// averages them to its middle), so the first spot can lie on the bar itself; the later ones reach past a large building.
    /// </summary>
    static readonly float[] Gaps = [0, 40, 80, 120, 160, 200];

    /// <summary>
    /// How far the highest walkable surface may stand above the terrain for a spot to count as open ground, in game units: more lifts it onto a
    /// roof or an upper floor (a storey is some 30 units; Mourn's bar roof stood 55 above the street).
    /// </summary>
    public const float GroundSlack = 15;

    /// <summary>
    /// Turns from straight behind tried when a spot is not walkable, in degrees.
    /// </summary>
    static readonly float[] Turns = [0, 25, -25, 50, -50, 75, -75];

    /// <summary>
    /// How far round a pen's centre the ground must be walkable, as a share of the pen radius.
    /// </summary>
    const float ClearShare = 0.6f;

    /// <summary>
    /// The bar and its spots; null while Mourn, its bar or the bar's doors are not in the world.
    /// </summary>
    public static BarPlace? Find(World world, PopulationData data, float penRadius)
    {
        if (data.Sites.FirstOrDefault(s => s.Record.Id == Town) is not { } site || Bar(world, site) is not { } bar) return null;

        var doors = world.Fixtures.DoorsByBuilding[bar.Key].ToList();
        if (doors.Count == 0) return null;

        var door = doors.Aggregate(Vector3.Zero, (sum, d) => sum + d.Position) / doors.Count;
        return new BarPlace(site, Counter(bar), PenSpot(world, bar, door, penRadius));
    }

    /// <summary>
    /// The building the bar's residents live in, else the town's first bar.
    /// </summary>
    static TownBuilding? Bar(World world, TownSite site) =>
        site.Buildings.FirstOrDefault(b => world.Fixtures.ResidentsTemplate(b) == BarResidents)
        ?? site.Buildings.FirstOrDefault(b => b.IsBar);

    /// <summary>
    /// His spot inside: the bar's first general node, else its first bar node, else its origin.
    /// </summary>
    static Vector3 Counter(TownBuilding bar)
    {
        var spot = bar.Of(HomeSpotKind.General).Concat(bar.Of(HomeSpotKind.Bar)).Select(s => (HomeSpot?)s).FirstOrDefault();
        return spot?.Position ?? bar.Position;
    }

    /// <summary>
    /// The pen's centre: straight back from the doors through the bar's origin and on past it, turned or pushed further until the ground round it is
    /// open; straight behind when none is.
    /// </summary>
    static Vector2 PenSpot(World world, TownBuilding bar, Vector3 door, float penRadius)
    {
        var origin = new Vector2(bar.Position.X, bar.Position.Z);
        var back = origin - new Vector2(door.X, door.Z);
        float depth = back.Length();
        var dir = depth > 1 ? back / depth : new Vector2(MathF.Sin(bar.Yaw), MathF.Cos(bar.Yaw));

        foreach (float gap in Gaps)
        {
            foreach (float turn in Turns)
            {
                var spot = origin + Turn(dir, turn) * (depth + PenGap + gap);
                if (Clear(world, spot, penRadius * ClearShare)) return spot;
            }
        }

        return origin + dir * (depth + PenGap);
    }

    /// <summary>
    /// Whether the spot and four points round it are open ground.
    /// </summary>
    static bool Clear(World world, Vector2 spot, float radius)
    {
        if (!OpenGround(world, spot)) return false;

        for (int k = 0; k < 4; k++)
        {
            if (!OpenGround(world, spot + Turn(Vector2.UnitX, k * 90) * radius)) return false;
        }

        return true;
    }

    /// <summary>
    /// Whether a spot is walkable ground with nothing walkable over it: not a roof, a walkway or a building's upper floor, which the walkable check
    /// alone lets through (it answers for the highest surface).
    /// </summary>
    public static bool OpenGround(World world, Vector2 spot)
    {
        var walk = world.Walkability;
        if (!walk.IsWalkable(spot.X, spot.Y)) return false;

        return walk.GroundHeight(spot.X, spot.Y) - walk.TerrainHeight(spot.X, spot.Y) <= GroundSlack;
    }

    /// <summary>
    /// A direction turned by some degrees.
    /// </summary>
    static Vector2 Turn(Vector2 dir, float degrees)
    {
        float a = degrees * MathF.PI / 180;
        return new Vector2(dir.X * MathF.Cos(a) - dir.Y * MathF.Sin(a), dir.X * MathF.Sin(a) + dir.Y * MathF.Cos(a));
    }
}
