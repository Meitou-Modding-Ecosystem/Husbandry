using Meitou.Data.Gameplay.Ai;
using Meitou.Simulation;
using Meitou.Simulation.Bodies;
using Meitou.Simulation.Construction;
using Meitou.Simulation.Factions;
using Meitou.Simulation.Production;

namespace Husbandry;

/// <summary>
/// One hitched animal as the checks see it: its slot, its character, the post it is hitched to.
/// </summary>
public readonly record struct Hitched(int Slot, CharacterCold Cold, BuildSite Post);

/// <summary>
/// The hitching post job (docs/husbandry.md "Hitching"): which animals hold it and on which post, and the animal facts the pen and breeding read.
/// </summary>
public static class Hitches
{
    /// <summary>
    /// The job's task type, claimed with <c>AddJobType</c> (inside Husbandry's number block 4500 to 4599, README "Numbers").
    /// </summary>
    public const TaskType Type = (TaskType)4501;

    /// <summary>
    /// The job's name in the jobs list (<c>AddJobType</c>).
    /// </summary>
    public const string Name = "Hitched";

    /// <summary>
    /// The age from which an animal is an adult: the game's own "Adult" label (below 0.4 "Pup", below 0.6 "Teen"); elders count too.
    /// </summary>
    public const float AdultAge = 0.6f;

    /// <summary>
    /// The job as the engine offers it: on the Hitching Post, to animals (<c>AddJobOffer</c>).
    /// </summary>
    public static ModJobOffer Offer => new(Type, [HusbandryData.Post], HusbandryText.Get("Hitch here"), ModJobTakers.Animals);

    /// <summary>
    /// The post a character is hitched to, or null.
    /// </summary>
    public static string? PostOf(CharacterCold cold) => cold.Jobs.Find(j => j.Type == Type)?.Site;

    /// <summary>
    /// Hitches a character to a post: a job already held moves to the new post in its place in the list, else it is added last.
    /// </summary>
    public static void Hitch(CharacterCold cold, string post)
    {
        int at = cold.Jobs.FindIndex(j => j.Type == Type);
        if (at >= 0) cold.Jobs[at] = new PermaJob(Type, post);
        else cold.Jobs.Add(new PermaJob(Type, post));
    }

    /// <summary>
    /// Unhitches a character.
    /// </summary>
    public static void Unhitch(CharacterCold cold) => cold.Jobs.RemoveAll(j => j.Type == Type);

    /// <summary>
    /// The living hitched animals whose post still stands finished; a hitch on a post that is gone or unfinished is taken off.
    /// </summary>
    public static List<Hitched> All(World world)
    {
        var list = new List<Hitched>();
        var table = world.Characters;
        for (int s = 0; s < table.HighWater; s++)
        {
            if (!table.Next[s].Alive || table.Cold(s) is not { IsPlayer: true, DiedTick: < 0 } cold || PostOf(cold) is not { } post) continue;

            if (world.Buildings.Find(post) is { Complete: true } site && site.Owner == world.Player.Faction) list.Add(new Hitched(s, cold, site));
            else Unhitch(cold);
        }

        return list;
    }

    /// <summary>
    /// Whether an animal is an adult (<see cref="AdultAge"/>).
    /// </summary>
    public static bool Adult(CharacterCold cold) => cold.Age >= AdultAge;

    /// <summary>
    /// The animal's hunger (3 full, 2 and more well fed, below 1 starving); 3 for one without a body.
    /// </summary>
    public static float Hunger(CharacterCold cold) => cold.Medical?.Hunger ?? 3;

    /// <summary>
    /// The words for a hunger level, as the breeding chance groups it.
    /// </summary>
    public static string Fed(float hunger) => hunger >= 2 ? "well fed" : hunger >= 1 ? "hungry" : "starving";

    /// <summary>
    /// Whether an animal is down (knocked out, carried, ragdolled) and so not walked anywhere.
    /// </summary>
    public static bool Down(CharacterCold cold) => CharacterFacts.Down(cold) || !cold.CarriedBy.IsNone;
}
