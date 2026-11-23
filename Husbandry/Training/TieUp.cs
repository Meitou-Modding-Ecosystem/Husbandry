using Meitou.Simulation;
using Meitou.Simulation.Combat;
using Meitou.Simulation.Factions;

namespace Husbandry.Training;

/// <summary>
/// Tie up (docs/husbandry.md "Training"): a melee hit by a player's animal with Tie up unlocked may knock the foe down: 2.5 % at Tie up 10 up to
/// 25 % at 100, for 0.65 to 2 seconds. Hits are observed in the combat step (which may not change the world) and the knock-downs applied in the
/// next serial step. Aiming at the legs and severing one wait for an engine hit hook (Husbandry #40, #41).
/// </summary>
public sealed class TieUp(RngPurpose purpose)
{
    /// <summary>
    /// The chance of a knock-down per hit, per level of Tie up.
    /// </summary>
    const float ChancePerLevel = 0.0025f;

    /// <summary>
    /// The longest knock-down, in seconds, at Tie up 100 and over.
    /// </summary>
    const float MaxSeconds = 2;

    /// <summary>
    /// Tie up gained for each landed hit.
    /// </summary>
    const float GainPerHit = 0.05f;

    /// <summary>
    /// The knock-downs and the hits to train, waiting for the serial step: the foe (none for a hit that only trains), its seconds, the animal.
    /// </summary>
    readonly List<(CharacterId Foe, float Seconds, CharacterId Animal)> _pending = [];

    /// <summary>
    /// One resolved hit: queues a knock-down when an animal with Tie up landed it and the roll falls.
    /// </summary>
    public void Observe(World world, in CombatHit hit)
    {
        if (hit.Source != CombatHitSource.Melee || !hit.VictimWasAlive || hit.VictimWasIncapacitated) return;
        if (!world.Characters.TryResolveNext(hit.Attacker, out int slot) || world.Characters.Cold(slot) is not { IsPlayer: true } cold) return;
        if (!SquadRegistry.IsAnimal(cold) || !AnimalSkills.Has(cold, AnimalSkills.TieUp)) return;

        float level = Math.Min(AnimalSkills.Level(cold, AnimalSkills.TieUp), 100);
        ulong which = (ulong)hit.Tick ^ (ulong)hit.Sequence << 20 ^ (ulong)hit.Blow << 40;
        ulong roll = Rng.Hash(world.Seed, cold.PortraitSerial ?? (ulong)slot, purpose, which);
        bool down = Rng.Float(roll) < level * ChancePerLevel;
        _pending.Add((down ? hit.Victim : CharacterId.None, MaxSeconds * level / 100 + 0.45f, hit.Attacker));
    }

    /// <summary>
    /// Applies the queued knock-downs and training.
    /// </summary>
    public void Apply(World world)
    {
        var table = world.Characters;
        foreach (var (foe, seconds, animal) in _pending)
        {
            if (table.TryResolveNext(animal, out int a) && table.Cold(a) is { } cold) AnimalSkills.Gain(cold, AnimalSkills.TieUp, GainPerHit);
            if (foe.IsNone || !table.TryResolveNext(foe, out int f) || table.Cold(f)?.Medical is not { Incapacitated: false } medical) continue;

            medical.KnockOut(Math.Min(seconds, MaxSeconds));
            AnimalWork.Say(world, f, "Tied up!");
        }

        _pending.Clear();
    }

    /// <summary>
    /// Adds the queue to the state hash.
    /// </summary>
    public void Hash(ref StateHasher hasher) => hasher.Add(_pending.Count);
}
