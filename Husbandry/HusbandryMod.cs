using Husbandry.Training;
using Meitou.Data.Fcs;
using Meitou.Modding;
using Meitou.Simulation;

namespace Husbandry;

/// <summary>
/// Husbandry's entry (docs/husbandry.md): the Hitching Post job, the system that keeps pens, breeds and runs the Beastmaster of Mourn (and any the
/// debug menu puts at a settlement), the trade window's gender patch, the settings and the debug rows.
/// </summary>
public sealed class HusbandryMod : IMod
{
    /// <summary>
    /// The mod's manifest id.
    /// </summary>
    public const string Id = "husbandry";

    /// <summary>
    /// The player's settings.
    /// </summary>
    public HusbandrySettings Settings { get; } = new();

    /// <summary>
    /// The system of the latest world, for the debug rows.
    /// </summary>
    HusbandrySystem? _latest;

    public void Initialize(ModContext c)
    {
        c.AddSettings(Settings);
        if (c.Data.Find(HusbandryData.Post) is null) c.LogError("Husbandry.mod has no records: generate it (docs/husbandry.md \"Data file\").");

        if (c.Data.Find(HusbandryData.TraderSquad) is null)
            c.LogError("Husbandry.mod has no Beastmaster: generate it again (docs/husbandry.md \"Data file\").");

        if (c.Data.Find(HusbandryData.TraderSquad) is not null && c.Data.Find(HusbandryData.PackList) is null)
            c.LogError("Husbandry.mod has no Beastmaster shop: generate it again (docs/husbandry.md \"Data file\").");

        if (c.Data.Find(HusbandryData.TraderSquad) is not null && c.Data.Find(HusbandryData.StockSquad(0)) is null)
            c.LogError("Husbandry.mod has no Beastmaster stock: generate it again (docs/husbandry.md \"Data file\").");

        bool stale = c.Data.Find(HusbandryData.TrainingPost) is null || c.Data.Find(HusbandryData.Capsule) is null;
        if (c.Data.Find(HusbandryData.Post) is not null && stale)
            c.LogError("Husbandry.mod has no training buildings or capsules: generate it again (docs/husbandry.md \"Data file\").");

        c.Harmony.PatchAll(typeof(HusbandryMod).Assembly);
        c.AddJobType(Hitches.Type, Hitches.Name);
        c.AddJobOffer(Hitches.Offer);
        AddTraining(c);
        c.AddItemUse(HusbandryData.Capsule, use => _latest?.Capsules.Use(use) ?? false);
        c.AddSystem("husbandry", ctx => _latest = new HusbandrySystem(ctx, Settings, c.RngPurpose, c.Log), SystemAnchor.End);
        c.AddDebugCommand("Husbandry: hitched animals", _ => Told(c, _latest?.View ?? NoWorld));
        c.AddDebugCommand("Husbandry: Beastmaster", _ => Told(c, _latest?.TraderView ?? NoWorld));
        c.AddDebugCommand("Husbandry: capsules", _ => Told(c, _latest?.CapsuleView ?? NoWorld));
        c.AddDebugCommand(SettlementTraderRow.Label, d => Told(c, SettlementTraderRow.Run(d, _latest?.Outposts)), hasField: true);
        c.AddDebugCommand("Husbandry: try breeding now", d =>
        {
            d.Enqueue(new BreedNowCommand { Tick = d.Tick });
            return "Every paired female tries at the next check.";
        });
        LogAnimals(c);
    }

    /// <summary>
    /// The animal skills (docs/husbandry.md "Training"): the skills and their effects, a training job per skill on the Training Post, and the work
    /// jobs: Fetch on every storage, Guard on the Pet Station, Hunt on the Bone Pile.
    /// </summary>
    static void AddTraining(ModContext c)
    {
        AnimalSkills.Register(c);
        foreach (var (index, _) in AnimalSkills.All.Index())
        {
            c.AddJobType(TrainingPost.TypeOf(index), TrainingPost.NameOf(index));
            c.AddJobOffer(TrainingPost.OfferOf(index));
        }

        c.AddJobType(Fetching.Type, Fetching.Name);
        c.AddJobOffer(Fetching.Offer(c.Data, c.Records((int)FcsRecordType.Building)));
        c.AddJobType(Guarding.Type, Guarding.Name);
        c.AddJobOffer(Guarding.Offer);
        c.AddJobType(Hunting.Type, Hunting.Name);
        c.AddJobOffer(Hunting.Offer);
    }

    /// <summary>
    /// The load scan: every animal kind in the merged data (mods' too) can be hitched, since the job takes any of the player's animals; the races
    /// are logged, robots marked as not breeding.
    /// </summary>
    static void LogAnimals(ModContext c)
    {
        var races = c.Records((int)FcsRecordType.AnimalCharacter)
            .SelectMany(a => a.GetReferences("race").Take(1))
            .Select(r => c.Data.Find(r.TargetStringId))
            .OfType<Meitou.Data.GameRecord>()
            .DistinctBy(r => r.StringId)
            .OrderBy(r => r.Name, StringComparer.Ordinal)
            .Select(r => r.GetBool("is robot") ? r.Name + " (robot: no breeding)" : r.Name)
            .ToList();
        int kinds = c.Records((int)FcsRecordType.AnimalCharacter).Count();
        c.Log($"husbandry: {kinds} animal kinds in {races.Count} races can be hitched: {string.Join(", ", races)}");
    }

    /// <summary>
    /// Logs a debug answer too (the status line shows only its start).
    /// </summary>
    static string Told(ModContext c, string answer)
    {
        c.Log(answer);
        return answer;
    }

    /// <summary>
    /// The debug answer before the first world.
    /// </summary>
    const string NoWorld = "no world yet";
}
