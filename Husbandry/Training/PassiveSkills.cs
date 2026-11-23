using Meitou.Simulation;
using Meitou.Simulation.Factions;
using Meitou.Simulation.Player;

namespace Husbandry.Training;

/// <summary>
/// The skills that need no job (docs/husbandry.md "Training"): Pack gives strength and Scout stealth (a stack of their effects per 10 levels);
/// an animal without Scout unlocked cannot sneak (the engine lets any character sneak; the original never lets an animal). Both also learn by use:
/// Pack while the pack holds goods, Scout while sneaking. Serial steps only.
/// </summary>
public static class PassiveSkills
{
    /// <summary>
    /// Skill gained per game hour of use.
    /// </summary>
    const float GainPerHour = 0.3f;

    /// <summary>
    /// One check of every animal of the player's.
    /// </summary>
    public static void Run(World world, double hours)
    {
        var table = world.Characters;
        for (int s = 0; s < table.HighWater; s++)
        {
            if (!table.Next[s].Alive || table.Cold(s) is not { IsPlayer: true, DiedTick: < 0 } cold || !SquadRegistry.IsAnimal(cold)) continue;

            var id = table.IdOf(s);
            AnimalSkills.Stacks(world, id, cold, AnimalSkills.PackTrained, AnimalSkills.Pack);
            AnimalSkills.Stacks(world, id, cold, AnimalSkills.ScoutTrained, AnimalSkills.Scout);
            Sneak(world, s, cold, hours);
            if (cold.Inventory?.Backpack?.Contents?.All().Any() == true) AnimalSkills.Gain(cold, AnimalSkills.Pack, GainPerHour * (float)hours);
        }
    }

    /// <summary>
    /// Stops an untrained animal sneaking; trains a scout that sneaks.
    /// </summary>
    static void Sneak(World world, int slot, CharacterCold cold, double hours)
    {
        if ((cold.Orders & CharacterOrders.Sneak) == 0) return;

        if (AnimalSkills.Has(cold, AnimalSkills.Scout))
        {
            AnimalSkills.Gain(cold, AnimalSkills.Scout, GainPerHour * (float)hours);
            return;
        }

        cold.Orders &= ~CharacterOrders.Sneak;
        if (cold.Medical is { } medical) medical.StayingLow = false;
        AnimalWork.Say(world, slot, $"Can't sneak: needs Scout {AnimalSkills.Unlocked:0}");
    }
}
