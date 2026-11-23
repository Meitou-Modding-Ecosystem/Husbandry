using Meitou.Data.Gameplay.Ai;
using Meitou.Simulation;
using Meitou.Simulation.Factions;
using Meitou.Simulation.Production;

namespace Husbandry.Training;

/// <summary>
/// The Pet Station (docs/husbandry.md "Training"): an animal with Guard unlocked, given the Guard job on a station, keeps to a pen round it as a
/// hitched animal does, and fights with +30 melee attack and defence (<see cref="AnimalSkills.Guarding"/>) while within <see cref="Range"/> of
/// it, wherever a fight takes it. It learns Guard by the game hour at its station, faster in a fight. Serial steps only.
/// </summary>
public static class Guarding
{
    /// <summary>
    /// The job's task type.
    /// </summary>
    public const TaskType Type = (TaskType)4521;

    /// <summary>
    /// The job's name in the jobs list.
    /// </summary>
    public const string Name = "Guarding";

    /// <summary>
    /// How far from its station the bonus holds, in game units (200 m).
    /// </summary>
    public const float Range = 2000;

    /// <summary>
    /// Guard gained per game hour at the station, and in a fight within range.
    /// </summary>
    const float GainPerHour = 0.2f, FightGainPerHour = 2;

    /// <summary>
    /// The job set, for <see cref="AnimalWork.Holders"/>.
    /// </summary>
    static readonly HashSet<TaskType> Types = [Type];

    /// <summary>
    /// The job as the station offers it to animals.
    /// </summary>
    public static ModJobOffer Offer => new(Type, [HusbandryData.PetStation], "Guard here", ModJobTakers.Animals);

    /// <summary>
    /// One check: pens each guard, gives the bonus to those in range and takes it off every other animal of the player's.
    /// </summary>
    public static void Run(World world, HusbandrySettings settings, RngPurpose purpose, double hours)
    {
        var guarding = new HashSet<int>();
        foreach (var w in AnimalWork.Holders(world, Types))
        {
            if (AnimalWork.Refused(world, w, AnimalSkills.Guard)) continue;

            Pens.Keep(world, w.AsHitched, settings, purpose);
            if (AnimalWork.Away(world, w.Slot, w.Site.Position) > Range) continue;

            guarding.Add(w.Slot);
            var id = world.Characters.IdOf(w.Slot);
            if (AnimalSkills.Guarding.On(world, id) is null) AnimalSkills.Guarding.Apply(world, id, double.PositiveInfinity);
            AnimalSkills.Gain(w.Cold, AnimalSkills.Guard, (w.Cold.InCombat ? FightGainPerHour : GainPerHour) * (float)hours);
        }

        Lift(world, guarding);
    }

    /// <summary>
    /// Takes the bonus off every animal of the player's not guarding in range now.
    /// </summary>
    static void Lift(World world, HashSet<int> guarding)
    {
        var table = world.Characters;
        for (int s = 0; s < table.HighWater; s++)
        {
            if (guarding.Contains(s) || !table.Next[s].Alive || table.Cold(s) is not { IsPlayer: true, Stats: { } stats } cold) continue;
            if (!SquadRegistry.IsAnimal(cold) || AnimalSkills.Guarding.On(stats) is null) continue;

            AnimalSkills.Guarding.Remove(world, table.IdOf(s));
        }
    }
}
