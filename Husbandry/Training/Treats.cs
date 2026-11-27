using Meitou.Simulation;
using Meitou.Simulation.Construction;
using Meitou.Simulation.Items;
using Meitou.Simulation.Production;

namespace Husbandry.Training;

/// <summary>
/// The treats a trainer feeds while training (docs/husbandry.md "Training"): Raw Meat kept in the Training Post's own storage
/// (<see cref="TrainingData.PostWork"/>), restocked by the trainer from the player's storages within <see cref="Reach"/> of the post when it runs
/// out (<see cref="Trainers"/>). An animal trains at full rate while the post holds a treat and at <see cref="Without"/> times it while not; the
/// trainer feeds one treat every game hour it trains. Serial steps only.
/// </summary>
public static class Treats
{
    /// <summary>
    /// How near the post a storage the trainer restocks from must be, in game units (300 m).
    /// </summary>
    public const float Reach = 3000;

    /// <summary>
    /// The training rate without treats, as a share of the full rate.
    /// </summary>
    public const float Without = 0.5f;

    /// <summary>
    /// The post's own storage; null when the post has none (the game has no Raw Meat, or the post was built before it had one).
    /// </summary>
    public static Inventory? Store(World world, BuildSite post) => world.Machines.Find(post.Id)?.Storage;

    /// <summary>
    /// Whether the post holds a treat.
    /// </summary>
    public static bool Have(World world, BuildSite post, string meat) => Store(world, post) is { } store && Meat(store, meat) is not null;

    /// <summary>
    /// Feeds one treat from the post; false when it holds none.
    /// </summary>
    public static bool Feed(World world, BuildSite post, string meat)
    {
        if (Store(world, post) is not { } store || Meat(store, meat) is not { } treat) return false;

        store.RemoveOne(treat);
        world.Machines.Touch();
        return true;
    }

    /// <summary>
    /// The nearest finished storage of the player's other than the post, within <see cref="Reach"/> of it, that holds Raw Meat; null when none.
    /// </summary>
    public static Machine? Source(World world, BuildSite post, string meat)
    {
        if (meat.Length == 0) return null;

        return world.Machines.All
            .Where(m => m.Site != post.Id && m.Storage is { } store && Meat(store, meat) is not null)
            .Where(m => Away(m.Position, post) <= Reach && Players(world, m))
            .MinBy(m => Away(m.Position, post));
    }

    /// <summary>
    /// Moves up to <paramref name="most"/> Raw Meat from one inventory to another, splitting a stack when needed; what has no room stays where it
    /// was. Returns how many moved.
    /// </summary>
    public static int Move(Inventory from, Inventory to, string meat, int most)
    {
        int moved = 0;
        foreach (var stack in from.Searched(false).Where(i => i.Record == meat).ToList())
        {
            if (moved >= most) break;

            int want = Math.Min(most - moved, stack.Quantity);
            var piece = Take(from, stack, want);
            if (to.Add(piece))
            {
                moved += want;
                continue;
            }

            // What did not fit stays in the piece (Inventory.Add places the piece itself only when all of it fits).
            moved += want - piece.Quantity;
            if (piece.Quantity > 0) from.Add(piece);
            break;
        }

        return moved;
    }

    /// <summary>
    /// How many more Raw Meat the post's storage takes (<see cref="TrainingData.PostMeat"/> less what it holds); 0 without a storage.
    /// </summary>
    public static int Room(World world, BuildSite post, string meat) =>
        Store(world, post) is { } store ? Math.Max(0, TrainingData.PostMeat - Count(store, meat)) : 0;

    /// <summary>
    /// The Raw Meat units in an inventory (and its backpack).
    /// </summary>
    public static int Count(Inventory inventory, string meat) => inventory.Searched(false).Where(i => i.Record == meat).Sum(i => i.Quantity);

    /// <summary>
    /// <paramref name="want"/> units of a stack, out of the inventory: the stack itself when it is all of it, else a split-off copy.
    /// </summary>
    static ItemInstance Take(Inventory from, ItemInstance stack, int want)
    {
        if (want >= stack.Quantity)
        {
            from.Remove(stack);
            return stack;
        }

        stack.Quantity -= want;
        return stack.WithQuantity(want);
    }

    /// <summary>
    /// The first Raw Meat in an inventory (and its backpack); null when none.
    /// </summary>
    static ItemInstance? Meat(Inventory inventory, string meat) => inventory.Searched(false).FirstOrDefault(i => i.Record == meat);

    /// <summary>
    /// Whether a machine stands on a finished building of the player's.
    /// </summary>
    static bool Players(World world, Machine m) => world.Buildings.Find(m.Site) is { Complete: true } site && site.Owner == world.Player.Faction;

    /// <summary>
    /// The distance on the ground between a point and the post.
    /// </summary>
    static float Away(System.Numerics.Vector3 at, BuildSite post) =>
        System.Numerics.Vector2.Distance(new(at.X, at.Z), new(post.Position.X, post.Position.Z));
}
