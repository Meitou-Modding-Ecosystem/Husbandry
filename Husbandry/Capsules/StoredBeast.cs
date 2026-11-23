using Meitou.Data.Gameplay.Bodies;
using Meitou.Simulation;
using Meitou.Simulation.Bodies;

namespace Husbandry.Capsules;

/// <summary>
/// An animal kept in a Beast Capsule: what is needed to make it again (<see cref="CapsuleSystem"/>): its record, age, name, sex, trained stats and
/// mod skill levels.
/// </summary>
public sealed record StoredBeast(string Record, float Age, string Name, bool Female, IReadOnlyList<(int Stat, float Value)> Stats,
    IReadOnlyList<(string Skill, float Level)> Skills)
{
    /// <summary>
    /// Keeps an animal: its stored stats (every number with storage) and its mod skills.
    /// </summary>
    public static StoredBeast Of(CharacterCold cold, bool female)
    {
        var stats = cold.Stats;
        var stored = stats is null
            ? []
            : Enum.GetValues<StatsEnumerated>()
                .Where(s => s != StatsEnumerated.None && StatsInfo.StorageOf(s) == s)
                .Select(s => ((int)s, stats.Stored(s)))
                .ToList();
        var skills = stats is null ? [] : ModSkills.All.Select(k => (k.Id, k.Level(stats))).ToList();
        return new StoredBeast(cold.RecordId, cold.Age, cold.Name, female, stored, skills);
    }

    /// <summary>
    /// Gives a new character the kept name, stats and skills.
    /// </summary>
    public void Restore(CharacterCold cold)
    {
        cold.Name = Name;
        if (cold.Stats is not { } stats) return;

        foreach (var (stat, value) in Stats) stats[(StatsEnumerated)stat] = value;
        foreach (var (id, level) in Skills)
        {
            if (ModSkills.Find(id) is { } skill) skill.Set(stats, level);
        }
    }

    /// <summary>
    /// Writes it.
    /// </summary>
    public void Write(BinaryWriter w)
    {
        w.Write(Record);
        w.Write(Age);
        w.Write(Name);
        w.Write(Female);
        w.Write(Stats.Count);
        foreach (var (stat, value) in Stats)
        {
            w.Write(stat);
            w.Write(value);
        }

        w.Write(Skills.Count);
        foreach (var (id, level) in Skills)
        {
            w.Write(id);
            w.Write(level);
        }
    }

    /// <summary>
    /// Reads one back.
    /// </summary>
    public static StoredBeast Read(BinaryReader r)
    {
        string record = r.ReadString();
        float age = r.ReadSingle();
        string name = r.ReadString();
        bool female = r.ReadBoolean();
        var stats = Enumerable.Range(0, r.ReadInt32()).Select(_ => (r.ReadInt32(), r.ReadSingle())).ToList();
        var skills = Enumerable.Range(0, r.ReadInt32()).Select(_ => (r.ReadString(), r.ReadSingle())).ToList();
        return new StoredBeast(record, age, name, female, stats, skills);
    }

    /// <summary>
    /// Adds it to the state hash.
    /// </summary>
    public void Hash(ref StateHasher hasher)
    {
        hasher.Add(Rng.StableHash(Record));
        hasher.Add(Age);
        hasher.Add(Female);
        foreach (var (stat, value) in Stats)
        {
            hasher.Add(stat);
            hasher.Add(value);
        }
    }
}
