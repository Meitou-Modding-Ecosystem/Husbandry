using Meitou.Data.Gameplay.Bodies;
using Meitou.Simulation;
using Meitou.Simulation.Bodies;

namespace Husbandry.Capsules;

/// <summary>
/// An animal bound to a Beast Capsule: what is needed to make it again (<see cref="CapsuleSystem"/>): its record, kind (its race's name), age, name,
/// sex, trained stats, every mod's skill levels, its traits (<see cref="BeastTraits"/>) and every other mod's character tag on it, so all of it
/// comes back on the new body a summon makes.
/// </summary>
public sealed record StoredBeast(string Record, string Kind, float Age, string Name, bool Female, IReadOnlyList<(int Stat, float Value)> Stats,
    IReadOnlyList<(string Skill, float Level)> Skills, IReadOnlyList<(string Trait, float Value)> Traits, IReadOnlyList<(string Mod, string Tag)> Tags)
{
    /// <summary>
    /// Keeps an animal: its stored stats (every number with storage), every registered mod skill, and its character tags
    /// (<paramref name="tags"/>, <see cref="CharacterTags.Of(LastingId)"/>): the traits tag read as traits, the others kept as they are.
    /// </summary>
    public static StoredBeast Of(CharacterCold cold, bool female, IReadOnlyDictionary<string, string> tags)
    {
        var stats = cold.Stats;
        var stored = stats is null
            ? []
            : Enum.GetValues<StatsEnumerated>()
                .Where(s => s != StatsEnumerated.None && StatsInfo.StorageOf(s) == s)
                .Select(s => ((int)s, stats.Stored(s)))
                .ToList();
        var skills = stats is null ? [] : ModSkills.All.Select(k => (k.Id, k.Level(stats))).ToList();
        var traits = BeastTraits.Decode(tags.GetValueOrDefault(BeastTraits.TagMod));
        var others = tags.Where(t => t.Key != BeastTraits.TagMod).Select(t => (t.Key, t.Value)).ToList();
        return new StoredBeast(cold.RecordId, cold.Race?.Name ?? "", cold.Age, cold.Name, female, stored, skills, traits, others);
    }

    /// <summary>
    /// Keeps an animal in the world: <see cref="Of(CharacterCold, bool, IReadOnlyDictionary{string, string})"/> with its tags, which it then
    /// takes off the body, so no mod finds a body that is in a capsule. Serial phases only (<see cref="CharacterTags.Set"/>).
    /// </summary>
    public static StoredBeast Take(World world, CharacterId id, CharacterCold cold, bool female)
    {
        var tags = world.CharacterTags.Of(cold.LastingId);
        var beast = Of(cold, female, tags);
        foreach (string mod in tags.Keys.ToList()) world.CharacterTags.Set(id, mod, null);
        return beast;
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

    /// <summary>
    /// Gives a new character in the world the kept name, stats and skills, every kept mod's tag and the traits (as the traits tag). Serial phases
    /// only (<see cref="CharacterTags.Set"/>).
    /// </summary>
    public void Restore(World world, CharacterId id, CharacterCold cold)
    {
        Restore(cold);
        foreach (var (mod, tag) in Tags) world.CharacterTags.Set(id, mod, tag);
        if (Traits.Count > 0) world.CharacterTags.Set(id, BeastTraits.TagMod, BeastTraits.Encode(Traits));
    }

    #region Saving

    /// <summary>
    /// Writes it (the layout of sidecar version 7).
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
        w.Write(Tags.Count);
        foreach (var (mod, tag) in Tags)
        {
            w.Write(mod);
            w.Write(tag);
        }
    }

    /// <summary>
    /// Reads one back; a sidecar before version 6 has no kind and no traits, one before version 7 no tags.
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
        var tags = version >= 7 ? Enumerable.Range(0, r.ReadInt32()).Select(_ => (r.ReadString(), r.ReadString())).ToList() : [];
        return new StoredBeast(record, kind, age, name, female, stats, skills, traits, tags);
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

        foreach (var (mod, tag) in Tags)
        {
            hasher.Add(Rng.StableHash(mod));
            hasher.Add(Rng.StableHash(tag));
        }
    }

    #endregion
}
