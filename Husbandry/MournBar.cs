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
/// <param name="Pen">The pen's centre, behind the bar.</param>
/// <param name="Floor">The height of the bar's ground floor (its lowest door): the pen lies at or under it, never on the bar.</param>
public sealed record BarPlace(TownSite Site, Vector3 Counter, Vector2 Pen, float Floor);

/// <summary>
/// Finds Mourn's bar in the running world: the home of the town's "Bar Mourn" residents (found at run time, since the town's housing picks the
/// building), and the spot behind it for the pen. Known only while the town's zone is loaded (its doors are registered then).
/// </summary>
public static class MournBar
{
    /// <summary>
    /// Mourn's TOWN record (its editor name is "Capital") and the SQUAD_TEMPLATE of the barman's squad, whose home is the bar.
    /// </summary>
    public const string Town = "963-gamedata.base", BarResidents = "95554-Mourn.mod";

    /// <summary>
    /// The pen's centre in the bar's own frame, in game units along its facing (yaw 0 along +Z) and to its right: the ground behind the bar where a
    /// beak thing stood in a play test (Observed, 2026-11-19: 45 along, 123 to the left, 46 under the bar's floor), pushed a quarter further out.
    /// </summary>
    static readonly Vector2 PenOffset = new Vector2(45, -123) * 1.25f;

    /// <summary>
    /// How far beyond the pen's distance from the bar the search goes when that spot is not open ground, in game units.
    /// </summary>
    static readonly float[] Gaps = [0, 40, 80];

    /// <summary>
    /// Turns round the bar from the pen's own direction tried when a spot is not open ground, in degrees.
    /// </summary>
    static readonly float[] Turns = [0, 20, -20, 40, -40, 60, -60, 90, -90];

    /// <summary>
    /// How far round a pen's centre the ground must be open, as a share of the pen radius.
    /// </summary>
    const float ClearShare = 0.6f;

    /// <summary>
    /// How far above the bar's floor a spot's highest walkable surface may stand, in game units: more is the bar's upper floor or roof. The terrain
    /// is no measure in Mourn: the town lies in a pit, its roofs about level with the terrain round it (Observed, 2026-11-19: the bar's roof at
    /// 1100, its floor at 1033, stock on the roof passing a terrain test).
    /// </summary>
    public const float GroundSlack = 15;

    /// <summary>
    /// The bar and its spots; null while Mourn, its bar or the bar's doors are not in the world.
    /// </summary>
    public static BarPlace? Find(World world, PopulationData data, float penRadius)
    {
        if (data.Sites.FirstOrDefault(s => s.Record.Id == Town) is not { } site || Bar(world, site) is not { } bar) return null;

        var doors = world.Fixtures.DoorsByBuilding[bar.Key].ToList();
        if (doors.Count == 0) return null;

        float floor = doors.Min(d => d.Position.Y);
        return new BarPlace(site, Counter(bar), PenSpot(world, bar, floor, penRadius), floor);
    }

    /// <summary>
    /// Whether a spot is walkable and its highest walkable surface is not above the bar's floor: not the bar's roof or upper floor, nor another's.
    /// </summary>
    public static bool OpenGround(World world, Vector2 spot, float floor)
    {
        var walk = world.Walkability;
        if (!walk.IsWalkable(spot.X, spot.Y)) return false;

        return walk.GroundHeight(spot.X, spot.Y) <= floor + GroundSlack;
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
    /// The pen's centre: <see cref="PenOffset"/> in the bar's frame, else turned round the bar or pushed further until the ground round it is open;
    /// the offset's spot when none is.
    /// </summary>
    static Vector2 PenSpot(World world, TownBuilding bar, float floor, float penRadius)
    {
        var origin = new Vector2(bar.Position.X, bar.Position.Z);
        var forward = new Vector2(MathF.Sin(bar.Yaw), MathF.Cos(bar.Yaw));
        var right = new Vector2(forward.Y, -forward.X);
        var offset = forward * PenOffset.X + right * PenOffset.Y;
        float distance = offset.Length();
        var dir = offset / distance;

        foreach (float gap in Gaps)
        {
            foreach (float turn in Turns)
            {
                var spot = origin + Turn(dir, turn) * (distance + gap);
                if (Clear(world, spot, floor, penRadius * ClearShare)) return spot;
            }
        }

        return origin + offset;
    }

    /// <summary>
    /// Whether the spot and four points round it are open ground.
    /// </summary>
    static bool Clear(World world, Vector2 spot, float floor, float radius)
    {
        if (!OpenGround(world, spot, floor)) return false;

        for (int k = 0; k < 4; k++)
        {
            if (!OpenGround(world, spot + Turn(Vector2.UnitX, k * 90) * radius, floor)) return false;
        }

        return true;
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
