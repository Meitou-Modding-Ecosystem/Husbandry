using Meitou.Data.Gameplay.Bodies;
using Meitou.Simulation;
using Meitou.Simulation.Bodies;

namespace Husbandry.Capsules;

/// <summary>
/// An animal bound to a Beast Capsule: what is needed to make it again (<see cref="CapsuleSystem"/>): its record, kind (its race's name), age, name,
/// sex, trained stats, mod skill levels and its traits (<see cref="BeastTraits"/>), which stay with the capsule while the animal is out.
/// </summary>
public sealed record StoredBeast(string Record, string Kind, float Age, string Name, bool Female, IReadOnlyList<(int Stat, float Value)> Stats,
    IReadOnlyList<(string Skill, float Level)> Skills, IReadOnlyList<(string Trait, float Value)> Traits)
{
    /// <summary>
    /// Keeps an animal: its stored stats (every number with storage), its mod skills and the traits it already had.
    /// </summary>
    public static StoredBeast Of(CharacterCold cold, bool female, IReadOnlyList<(string Trait, float Value)>? traits = null)
    {
        var stats = cold.Stats;
        var stored = stats is null
            ? []
            : Enum.GetValues<StatsEnumerated>()
                .Where(s => s != StatsEnumerated.None && StatsInfo.StorageOf(s) == s)
                .Select(s => ((int)s, stats.Stored(s)))
                .ToList();
        var skills = stats is null ? [] : ModSkills.All.Select(k => (k.Id, k.Level(stats))).ToList();
        return new StoredBeast(cold.RecordId, cold.Race?.Name ?? "", cold.Age, cold.Name, female, stored, skills, traits ?? []);
    }

    /// <summary>
    /// A stored stat's value, 0 when it was not kept.
    /// </summary>
    public float Stat(StatsEnumerated stat) => Stats.FirstOrDefault(s => s.Stat == (int)stat).Value;

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

    #region Saving

    /// <summary>
    /// Writes it (the layout of sidecar version 6).
    /// </summary>
    public void Write(BinaryWriter w)
    {
        w.Write(Record);
        w.Write(Kind);
        w.Write(Age);
        w.Write(Name);
        w.Write(Female);
        w.Write(Stats.Count);
        foreach (var (stat, value) in Stats)
        {
            w.Write(stat);
            w.Write(value);
        }

        WritePairs(w, Skills);
        WritePairs(w, Traits);
    }

    /// <summary>
    /// Reads one back; a sidecar before version 6 has no kind and no traits.
    /// </summary>
    public static StoredBeast Read(BinaryReader r, int version)
    {
        string record = r.ReadString();
        string kind = version >= 6 ? r.ReadString() : "";
        float age = r.ReadSingle();
        string name = r.ReadString();
        bool female = r.ReadBoolean();
        var stats = Enumerable.Range(0, r.ReadInt32()).Select(_ => (r.ReadInt32(), r.ReadSingle())).ToList();
        var skills = ReadPairs(r);
        var traits = version >= 6 ? ReadPairs(r) : [];
        return new StoredBeast(record, kind, age, name, female, stats, skills, traits);
    }

    /// <summary>
    /// Writes a list of named numbers.
    /// </summary>
    static void WritePairs(BinaryWriter w, IReadOnlyList<(string Id, float Value)> pairs)
    {
        w.Write(pairs.Count);
        foreach (var (id, value) in pairs)
        {
            w.Write(id);
            w.Write(value);
        }
    }

    /// <summary>
    /// Reads a list of named numbers.
    /// </summary>
    static List<(string, float)> ReadPairs(BinaryReader r) => [.. Enumerable.Range(0, r.ReadInt32()).Select(_ => (r.ReadString(), r.ReadSingle()))];

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

        foreach (var (trait, value) in Traits)
        {
            hasher.Add(Rng.StableHash(trait));
            hasher.Add(value);
        }
    }

    #endregion
}
