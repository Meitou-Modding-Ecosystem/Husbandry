using System.Globalization;
using AnimalBehaviors;
using Meitou.Data.Gameplay.Ai;
using Meitou.Simulation;
using Meitou.Simulation.Movement;

namespace Husbandry;

/// <summary>
/// One animal on a Hitching Post's roster (docs/husbandry.md "Hitching Post"): who it is, how it eats and its next litter.
/// </summary>
public sealed record PostAnimal(string Name, string Race, bool Female, string Age, float Hunger, string Doing, double? NextLitter);

/// <summary>
/// A Hitching Post's roster: its animals in slot order and how many it holds (0: no limit).
/// </summary>
public sealed record PostRosterView(IReadOnlyList<PostAnimal> Animals, int Capacity);

/// <summary>
/// The animals hitched at each post, built in the serial check once a game second and published whole for the host's thread, which only reads it
/// (<see cref="HitchingPostWindow"/>).
/// </summary>
public sealed class PostRoster
{
    /// <summary>
    /// The task rows of a food run (the game's own, as Animal Behaviors' pen reads them).
    /// </summary>
    static readonly HashSet<TaskType> FoodRows = [TaskType.AquireFoodAtHomebase, TaskType.GrabOneFood, TaskType.EatFoodOnGround];

    /// <summary>
    /// The rosters by post site id, as last published.
    /// </summary>
    volatile Dictionary<string, PostRosterView> _posts = [];

    /// <summary>
    /// A post's roster; empty when nothing is hitched there.
    /// </summary>
    public PostRosterView For(string? site, int capacity) =>
        site is not null && _posts.TryGetValue(site, out var view) ? view : new PostRosterView([], capacity);

    /// <summary>
    /// Builds every post's roster from the hitched animals and publishes them.
    /// </summary>
    public void Publish(World world, IAnimalState animals, Breeding breeding, int penRadius)
    {
        var posts = new Dictionary<string, (List<PostAnimal> Animals, int Capacity)>();
        foreach (var h in Hitches.All(world))
        {
            if (!posts.TryGetValue(h.Post.Id, out var post)) posts[h.Post.Id] = post = ([], animals.Capacity(h.Post));
            post.Animals.Add(Row(world, h, animals.Sexes, breeding, penRadius));
        }

        _posts = posts.ToDictionary(p => p.Key, p => new PostRosterView(p.Value.Animals, p.Value.Capacity));
    }

    /// <summary>
    /// One animal's roster line.
    /// </summary>
    static PostAnimal Row(World world, Hitched h, Sexes sexes, Breeding breeding, int penRadius)
    {
        double? next = h.Cold.PortraitSerial is { } serial && breeding.NextTry.TryGetValue(serial, out double at)
            ? Math.Max(0, at - world.GameHours)
            : null;
        string race = h.Cold.Race?.Name ?? "";
        return new PostAnimal(h.Cold.Name, race, sexes.Female(h.Cold), AgeOf(h.Cold.Age), Hitches.Hunger(h.Cold), Doing(world, h, penRadius), next);
    }

    /// <summary>
    /// The game's age label of an animal: Pup below 0.4, Teen below 0.6, else Adult.
    /// </summary>
    static string AgeOf(float age) => age < 0.4f ? "Pup" : age < Hitches.AdultAge ? "Teen" : "Adult";

    /// <summary>
    /// What the animal is doing about food and its pen.
    /// </summary>
    static string Doing(World world, Hitched h, int penRadius)
    {
        if (Hitches.Down(h.Cold)) return "Down";
        if (h.Cold.FetchingFood || h.Cold.Running is { } run && FoodRows.Contains(run.Row)) return "Getting food";

        var at = world.Characters.Next[h.Slot].Position;
        float away = System.Numerics.Vector2.Distance(new(at.X, at.Z), new(h.Post.Position.X, h.Post.Position.Z));
        if (away > penRadius) return "Out of the pen";

        return (CharacterTask)world.Characters.Next[h.Slot].Task == CharacterTask.Idle ? "Resting in the pen" : "In the pen";
    }

    /// <summary>
    /// Hunger as the game's panel shows it (level x 100, out of 300).
    /// </summary>
    public static string HungerText(float hunger) => ((int)(hunger * 100)).ToString(CultureInfo.InvariantCulture) + "/300";
}
