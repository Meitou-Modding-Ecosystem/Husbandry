using Meitou.Simulation;
using Meitou.Simulation.Construction;
using Meitou.Simulation.Items;

namespace Husbandry.Training;

/// <summary>
/// The treats a trainer feeds while training (docs/husbandry.md "Training"): Raw Meat from the trainer's own inventory, else from a storage of the
/// player's within <see cref="Reach"/> of the post. An animal trains at full rate while there is a treat and at <see cref="Without"/> times it
/// while none; each trainer feeds one treat every game hour it trains. Serial steps only.
/// </summary>
public static class Treats
{
    /// <summary>
    /// How near the post a storage counts, in game units.
    /// </summary>
    public const float Reach = 300;

    /// <summary>
    /// The training rate without treats, as a share of the full rate.
    /// </summary>
    public const float Without = 0.5f;

    /// <summary>
    /// Whether a treat is at hand for the trainer at the post.
    /// </summary>
    public static bool Have(World world, int trainer, BuildSite post, string meat) => Find(world, trainer, post, meat) is not null;

    /// <summary>
    /// Feeds one treat (the trainer's own first); false when none is at hand.
    /// </summary>
    public static bool Feed(World world, int trainer, BuildSite post, string meat)
    {
        if (Find(world, trainer, post, meat) is not var (item, owner, storage)) return false;

        owner.RemoveOne(item);
        if (storage) world.Machines.Touch();
        return true;
    }

    /// <summary>
    /// A Raw Meat at hand: the trainer's own, else the first in the nearest storage of the player's within <see cref="Reach"/>; null when none or
    /// the game has no meat.
    /// </summary>
    static (ItemInstance Item, Inventory Owner, bool Storage)? Find(World world, int trainer, BuildSite post, string meat)
    {
        if (meat.Length == 0) return null;

        if (world.Characters.Cold(trainer)?.Inventory is { } own && Meat(own, meat) is { } carried) return (carried, own, false);

        foreach (var machine in world.Machines.All.Where(m => m.Storage is not null).OrderBy(m => Away(m.Position, post)))
        {
            if (Away(machine.Position, post) > Reach) break;
            if (world.Buildings.Find(machine.Site) is not { Complete: true } site || site.Owner != world.Player.Faction) continue;
            if (Meat(machine.Storage!, meat) is { } stored) return (stored, machine.Storage!, true);
        }

        return null;
    }

    /// <summary>
    /// The first Raw Meat in an inventory (and its backpack); null when none.
    /// </summary>
    static ItemInstance? Meat(Inventory inventory, string meat) => inventory.Searched(false).FirstOrDefault(i => i.Record == meat);

    /// <summary>
    /// The distance on the ground between a point and the post.
    /// </summary>
    static float Away(System.Numerics.Vector3 at, BuildSite post) =>
        System.Numerics.Vector2.Distance(new(at.X, at.Z), new(post.Position.X, post.Position.Z));
}
