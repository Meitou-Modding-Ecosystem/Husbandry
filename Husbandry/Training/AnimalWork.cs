using System.Numerics;
using Meitou.Data.Gameplay.Ai;
using Meitou.Simulation;
using Meitou.Simulation.Ai;
using Meitou.Simulation.Bodies;
using Meitou.Simulation.Construction;
using Meitou.Simulation.Factions;
using Meitou.Simulation.Movement;
using Meitou.Simulation.Player;
using Meitou.Simulation.Production;

namespace Husbandry.Training;

/// <summary>
/// One of the player's animals holding a training or work job: its slot, its character, the building and the job's type.
/// </summary>
public readonly record struct Worker(int Slot, CharacterCold Cold, BuildSite Site, TaskType Type)
{
    /// <summary>
    /// The worker as a hitched animal, for <see cref="Pens.Keep"/>.
    /// </summary>
    public Hitched AsHitched => new(Slot, Cold, Site);
}

/// <summary>
/// What the training and work jobs share (docs/husbandry.md "Training"): finding the animals that hold them, whether one is free to be sent,
/// walking it, and telling the player why it refuses a job.
/// </summary>
public static class AnimalWork
{
    /// <summary>
    /// How near a building's centre counts as at it, in game units.
    /// </summary>
    public const float Reach = 80;

    /// <summary>
    /// The living player animals holding a job of one of <paramref name="types"/> on a finished building of the player's; a job on a building that
    /// is gone or not the player's is taken off.
    /// </summary>
    public static List<Worker> Holders(World world, IReadOnlySet<TaskType> types)
    {
        var list = new List<Worker>();
        var table = world.Characters;
        for (int s = 0; s < table.HighWater; s++)
        {
            if (!table.Next[s].Alive || table.Cold(s) is not { IsPlayer: true, DiedTick: < 0 } cold || !SquadRegistry.IsAnimal(cold)) continue;

            foreach (var job in cold.Jobs.Where(j => types.Contains(j.Type)).ToList())
            {
                if (job.Site is { } id && world.Buildings.Find(id) is { Complete: true } site && site.Owner == world.Player.Faction)
                    list.Add(new Worker(s, cold, site, job.Type));
                else cold.Jobs.Remove(job);
            }
        }

        return list;
    }

    /// <summary>
    /// Whether an animal can be sent somewhere: up, idle, out of a fight and holding no order of the player's.
    /// </summary>
    public static bool Free(World world, int slot, CharacterCold cold) =>
        !Hitches.Down(cold) && !cold.InCombat && !PlayerOrders.Holds(world, slot)
        && (CharacterTask)world.Characters.Next[slot].Task == CharacterTask.Idle;

    /// <summary>
    /// The distance on the ground between a character and a point.
    /// </summary>
    public static float Away(World world, int slot, Vector3 point)
    {
        var at = world.Characters.Next[slot].Position;
        return Vector2.Distance(new Vector2(at.X, at.Z), new Vector2(point.X, point.Z));
    }

    /// <summary>
    /// Walks a character to a point with a move order (held against its squad's mind until it arrives).
    /// </summary>
    public static void Walk(World world, int slot, Vector3 point) =>
        world.GiveOrder(world.Characters.IdOf(slot), TaskType.MoveCusOrdered, point: point);

    /// <summary>
    /// Floats a line of text over a character.
    /// </summary>
    public static void Say(World world, int slot, string text) =>
        world.Floaters?.Enqueue(Floater.Label(world.Characters.Next[slot].Position + new Vector3(0, 20, 0), text));

    /// <summary>
    /// Takes a job off an animal that has not unlocked its skill, telling the player; true when it did.
    /// </summary>
    public static bool Refused(World world, Worker w, ModSkill skill)
    {
        if (AnimalSkills.Has(w.Cold, skill)) return false;

        w.Cold.Jobs.RemoveAll(j => j.Type == w.Type);
        Say(world, w.Slot, $"Needs {TrainableSkill.ShortOf(skill.Label)} {AnimalSkills.Unlocked:0}: train it at a Training Post");
        return true;
    }

    /// <summary>
    /// How far the nearest standing person (not an animal) of the player's is from a point; float.MaxValue when none.
    /// </summary>
    public static float NearestPerson(World world, Vector3 point)
    {
        var table = world.Characters;
        float best = float.MaxValue;
        for (int s = 0; s < table.HighWater; s++)
        {
            if (!table.Next[s].Alive || table.Cold(s) is not { IsPlayer: true, DiedTick: < 0 } c) continue;
            if (SquadRegistry.IsAnimal(c) || Hitches.Down(c)) continue;

            best = Math.Min(best, Away(world, s, point));
        }

        return best;
    }

    /// <summary>
    /// A walkable spot round a centre between <paramref name="min"/> and <paramref name="max"/> away, from a roll; null when none of a few tries is.
    /// </summary>
    public static Vector3? Spot(World world, Vector3 centre, float min, float max, ulong roll)
    {
        for (int k = 0; k < 8; k++)
        {
            float angle = Rng.Float(Rng.Hash(roll, (ulong)k, 0, 1)) * MathF.Tau;
            float r = min + Rng.Float(Rng.Hash(roll, (ulong)k, 0, 2)) * (max - min);
            var spot = new Vector2(centre.X, centre.Z) + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * r;
            if (world.Walkability.IsWalkable(spot.X, spot.Y)) return new Vector3(spot.X, world.Walkability.GroundHeight(spot.X, spot.Y), spot.Y);
        }

        return null;
    }
}
