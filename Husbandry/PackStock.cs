using Meitou.Data;
using Meitou.Data.Fcs;
using Meitou.Simulation;
using Meitou.Simulation.Population;

namespace Husbandry;

/// <summary>
/// The animal packs the Beastmaster sells (docs/husbandry.md "The Beastmaster of Mourn"): one of every backpack an animal can wear is in his shop
/// after each of its fills. An animal pack is a CONTAINER whose <c>races</c> list names a race of an ANIMAL_CHARACTER: only such a list lets an
/// animal wear an item (the engine's <c>RaceLimiter</c>). The packs are found in the merged data at the first check, so those of other mods are
/// sold too. Serial steps only.
/// </summary>
public sealed class PackStock(PopulationData data, Action<string> log)
{
    /// <summary>
    /// The animal packs of the merged data, by string id; found at the first check.
    /// </summary>
    List<string>? _packs;

    /// <summary>
    /// The shop fill last topped up (<c>Shop.Fills</c>); -1 before any. Not saved: after a load the first check tops up once more, adding only
    /// what the stock lacks.
    /// </summary>
    int _topped = -1;

    /// <summary>
    /// One check: after each fill of his squad's shop, adds one of every animal pack the stock lacks.
    /// </summary>
    public void Top(Squad squad)
    {
        if (squad.Shop is not { } shop || shop.Fills == _topped) return;

        _topped = shop.Fills;
        var held = shop.Stock.All().Select(i => i.Record).ToHashSet();
        foreach (string pack in Packs().Where(p => !held.Contains(p)))
        {
            if (data.Items.Create(pack) is { } item && !shop.Stock.Add(item)) log($"husbandry: no room in the Beastmaster's stock for {pack}");
        }
    }

    /// <summary>
    /// The animal packs, found once and logged.
    /// </summary>
    List<string> Packs()
    {
        if (_packs is not null) return _packs;

        _packs = Find(data.Db);
        log($"husbandry: the Beastmaster sells {_packs.Count} animal packs: {string.Join(", ", _packs.Select(p => data.Db.Find(p)?.Name ?? p))}");
        return _packs;
    }

    /// <summary>
    /// The CONTAINER records whose <c>races</c> name a race of an ANIMAL_CHARACTER, by string id.
    /// </summary>
    public static List<string> Find(GameDatabase db)
    {
        var animalRaces = db.OfType(FcsRecordType.AnimalCharacter)
            .SelectMany(r => r.GetReferences("race"))
            .Select(r => r.TargetStringId)
            .ToHashSet();

        return
        [
            .. db.OfType(FcsRecordType.Container)
                .Where(r => r.GetReferences("races").Any(race => animalRaces.Contains(race.TargetStringId)))
                .Select(r => r.StringId)
                .OrderBy(id => id, StringComparer.Ordinal),
        ];
    }
}
