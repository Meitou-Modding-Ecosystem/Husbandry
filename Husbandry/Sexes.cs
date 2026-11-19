using Meitou.Simulation;

namespace Husbandry;

/// <summary>
/// The sex of each animal (docs/husbandry.md "Breeding"). Neither the game nor its data give animals a sex, so the mod rolls it once from the
/// character's portrait serial (saved with the character), half and half; an animal the mod made with a set sex (the Beastmaster's stock) is kept
/// here by serial instead, saved in the sidecar.
/// </summary>
public sealed class Sexes(RngPurpose purpose)
{
    /// <summary>
    /// The counter of the sex roll.
    /// </summary>
    const ulong SexCounter = 0x5E7;

    /// <summary>
    /// The sexes the mod set, by portrait serial (true: female).
    /// </summary>
    public SortedDictionary<uint, bool> Assigned { get; } = [];

    /// <summary>
    /// Whether an animal is female: its set sex, else the roll from its serial; false for one without a serial.
    /// </summary>
    public bool Female(CharacterCold cold) => cold.PortraitSerial is { } serial && Female(serial);

    /// <summary>
    /// Whether the animal of a portrait serial is female (<see cref="Female(CharacterCold)"/>).
    /// </summary>
    public bool Female(uint serial) => Assigned.TryGetValue(serial, out bool set) ? set : Rolled(serial);

    /// <summary>
    /// The half-and-half roll of an animal without a set sex.
    /// </summary>
    bool Rolled(uint serial) => (Rng.Hash(0, serial, purpose, SexCounter) & 1) == 1;
}
