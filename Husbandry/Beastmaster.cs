using System.Numerics;
using System.Text;
using Meitou.Data.Gameplay.Ai;
using Meitou.Simulation;
using Meitou.Simulation.Ai;
using Meitou.Simulation.Factions;
using Meitou.Simulation.Movement;
using Meitou.Simulation.Population;

namespace Husbandry;

/// <summary>
/// One place in the Beastmaster's pen: the kind and sex it holds, the animal there now (its portrait serial, 0 none) and, after a sale, when it is
/// filled again (game hours, -1 not waiting).
/// </summary>
public sealed class StockSlot(int kind, bool female)
{
    /// <summary>
    /// The index of its kind in <see cref="Beastmaster.Kinds"/>.
    /// </summary>
    public int Kind { get; } = kind;

    /// <summary>
    /// Whether it holds a female.
    /// </summary>
    public bool Female { get; } = female;

    /// <summary>
    /// The portrait serial of the animal in it; 0 when empty.
    /// </summary>
    public uint Serial { get; set; }

    /// <summary>
    /// When an empty place is filled again, in game hours; -1 at once.
    /// </summary>
    public double RefillAt { get; set; } = -1;
}

/// <summary>
/// The Hive Beastmaster of Mourn (docs/husbandry.md "The Beastmaster of Mourn"): a Western Hive soldier drone in the town's bar who sells garru,
/// crabs and bonedogs, a male and a female of each, kept in a pen behind the bar. His conversation's "Show me your beasts" opens the game's own
/// animal buying window (DA 48), which lists his squad's animals and makes the bought ones the player's; the mod then walks each bought animal out
/// of the pen to follow the buyer, and fills the empty place a day later. Serial steps only.
/// </summary>
public sealed class Beastmaster(PopulationSystem? population, Sexes sexes, Action<string> log)
{
    #region Stock

    /// <summary>
    /// The kinds he sells: the name his stock goes by and its ANIMAL_CHARACTER (the game's records; "Dog" is the bonedog).
    /// </summary>
    public static readonly (string Name, string Record)[] Kinds =
    [
        ("Garru", "42201-rebirth.mod"),
        ("Crab", "56088-Newwworld.mod"),
        ("Bonedog", "3979-gamedata.base"),
    ];

    /// <summary>
    /// The age of his stock: adults (the game's "Adult" from 0.6), so a bought pair can breed at once.
    /// </summary>
    public const float StockAge = 0.8f;

    /// <summary>
    /// Game hours before a sold place is filled again.
    /// </summary>
    public const double RestockHours = 24;

    /// <summary>
    /// How far a penned animal may stray from the pen's centre, in game units.
    /// </summary>
    public const float PenRadius = 50;

    /// <summary>
    /// How far round the pen's centre new stock is put.
    /// </summary>
    const float StockRing = 20;

    /// <summary>
    /// The chance at each check that an idle penned animal strolls to another spot.
    /// </summary>
    const float GrazeChance = 0.1f;

    /// <summary>
    /// The pen's places, a female then a male of each kind.
    /// </summary>
    public List<StockSlot> Slots { get; } = [.. Kinds.Index().SelectMany(k => new[] { new StockSlot(k.Index, true), new StockSlot(k.Index, false) })];

    /// <summary>
    /// Keeps his stock: notes the animals sold and sends them to the buyer, walks strays back into the pen, fills empty places that are due.
    /// </summary>
    void KeepStock(World world, Squad squad, BarPlace place, int trader, RngPurpose purpose)
    {
        var found = Find(world);
        foreach (var slot in Slots)
        {
            if (slot.Serial != 0 && found.TryGetValue(slot.Serial, out int at)) Keep(world, slot, at, trader, place, purpose);
            else if (slot.Serial != 0) Lost(slot);
            if (slot.Serial == 0 && world.GameHours >= slot.RefillAt) Fill(world, squad, slot, place);
        }
    }

    /// <summary>
    /// The living characters holding a stock serial, by serial.
    /// </summary>
    Dictionary<uint, int> Find(World world)
    {
        var serials = Slots.Where(s => s.Serial != 0).Select(s => s.Serial).ToHashSet();
        var found = new Dictionary<uint, int>();
        var table = world.Characters;
        for (int s = 0; s < table.HighWater; s++)
        {
            if (table.Next[s].Alive && table.Cold(s) is { Medical.Dead: false, PortraitSerial: { } serial } && serials.Contains(serial))
                found[serial] = s;
        }

        return found;
    }

    /// <summary>
    /// One stocked animal: sold when it has become the player's, else held in the pen.
    /// </summary>
    void Keep(World world, StockSlot slot, int at, int trader, BarPlace place, RngPurpose purpose)
    {
        var cold = world.Characters.Cold(at)!;
        if (cold.IsPlayer)
        {
            Sold(world, slot, at, trader);
            return;
        }

        cold.Engaged = true;
        if (!Idle(world, at, cold)) return;

        if (Stranded(world, at)) PutAt(world, at, Ground(world, StockSpot(world, place.Pen, Slots.IndexOf(slot))));
        else Pen(world, at, place.Pen, purpose);
    }

    /// <summary>
    /// Whether a penned animal stands up on a building (a roof or an upper floor, where a pen placed by an older version put the stock), from where it
    /// cannot walk down into the pen.
    /// </summary>
    static bool Stranded(World world, int at)
    {
        var pos = world.Characters.Next[at].Position;
        return pos.Y - world.Walkability.TerrainHeight(pos.X, pos.Z) > MournBar.GroundSlack;
    }

    /// <summary>
    /// A point of the ground at a spot.
    /// </summary>
    static Vector3 Ground(World world, Vector2 spot) => new(spot.X, world.Walkability.TerrainHeight(spot.X, spot.Y), spot.Y);

    /// <summary>
    /// A stocked animal the buying window made the player's: it follows the player character nearest the Beastmaster (the buyer stands talking to
    /// him), its sex stays set, and its place is filled a day later.
    /// </summary>
    void Sold(World world, StockSlot slot, int at, int trader)
    {
        var table = world.Characters;
        table.Cold(at)!.Engaged = false;
        slot.Serial = 0;
        slot.RefillAt = world.GameHours + RestockHours;
        if (Buyer(world, trader) is not { } buyer) return;

        world.Commands.Enqueue(new FollowOrder([table.IdOf(at)], table.IdOf(buyer)) { Tick = world.Tick + 1 });
        log($"husbandry: {table.Cold(at)!.Name} was bought and follows {table.Cold(buyer)!.Name}");
    }

    /// <summary>
    /// The living player character, not an animal, nearest the Beastmaster; null when none.
    /// </summary>
    static int? Buyer(World world, int trader)
    {
        var table = world.Characters;
        var from = table.Next[trader].Position;
        int? best = null;
        float bestDistance = float.MaxValue;
        for (int s = 0; s < table.HighWater; s++)
        {
            if (!table.Next[s].Alive || table.Cold(s) is not { IsPlayer: true, Medical.Dead: false } c || SquadRegistry.IsAnimal(c)) continue;

            float d = Vector3.Distance(table.Next[s].Position, from);
            if (d >= bestDistance) continue;

            best = s;
            bestDistance = d;
        }

        return best;
    }

    /// <summary>
    /// A stocked animal that is gone without being sold (it died, or its squad left the world and came back without it): its place is filled at
    /// once and its set sex dropped.
    /// </summary>
    void Lost(StockSlot slot)
    {
        sexes.Assigned.Remove(slot.Serial);
        slot.Serial = 0;
        slot.RefillAt = -1;
    }

    /// <summary>
    /// Puts a new animal of the place's kind and sex in the pen, in his squad, named for both ("Garru (female)").
    /// </summary>
    void Fill(World world, Squad squad, StockSlot slot, BarPlace place)
    {
        var (name, record) = Kinds[slot.Kind];
        var spot = StockSpot(world, place.Pen, Slots.IndexOf(slot));
        var id = population?.SpawnAnimal(world, record, squad.Id, spot, StockAge) ?? CharacterId.None;
        if (id.IsNone || !world.Characters.TryResolveNext(id, out int at) || world.Characters.Cold(at) is not { PortraitSerial: { } serial } cold)
        {
            slot.RefillAt = world.GameHours + 1;
            log($"husbandry: the Beastmaster could not stock a {name} (no such animal record?); trying again in an hour");
            return;
        }

        cold.Name = $"{name} ({(slot.Female ? "female" : "male")})";
        cold.Engaged = true;
        sexes.Assigned[serial] = slot.Female;
        slot.Serial = serial;
        slot.RefillAt = -1;
    }

    /// <summary>
    /// Where a place's new animal is put: its own point on a ring round the pen's centre when open ground, else the centre.
    /// </summary>
    static Vector2 StockSpot(World world, Vector2 pen, int index)
    {
        float angle = index * MathF.Tau / 6;
        var spot = pen + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * StockRing;
        return MournBar.OpenGround(world, spot) ? spot : pen;
    }

    /// <summary>
    /// Walks an idle animal back into the pen when it strayed, or now and then to another spot inside.
    /// </summary>
    static void Pen(World world, int at, Vector2 pen, RngPurpose purpose)
    {
        var pos = world.Characters.Next[at].Position;
        float away = Vector2.Distance(new Vector2(pos.X, pos.Z), pen);
        ulong roll = Rng.Hash(world.Seed, world.Characters.Cold(at)!.PortraitSerial ?? (ulong)at, purpose, (ulong)world.Tick);
        if (away <= PenRadius && Rng.Float(roll) >= GrazeChance) return;

        float angle = Rng.Float(Rng.Hash(roll, 1, 0, 1)) * MathF.Tau;
        float r = MathF.Sqrt(Rng.Float(Rng.Hash(roll, 1, 0, 2))) * PenRadius * 0.5f;
        var goal = pen + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * r;
        if (!MournBar.OpenGround(world, goal)) goal = pen;

        TaskStart.Walk(world, at, goal, SpeedMode.Walk, TaskType.MoveOnFreeWill, TaskSource.Npc);
    }

    #endregion

    #region Trader

    /// <summary>
    /// The tag that finds his platoon again after a load (<see cref="Platoon.ModTag"/>).
    /// </summary>
    public const string Tag = "husbandry.beastmaster";

    /// <summary>
    /// His faction: the Western Hive.
    /// </summary>
    public const string Faction = "18000-gamedata.base";

    /// <summary>
    /// How near his spot in the bar counts as there.
    /// </summary>
    const float CounterReach = 20;

    /// <summary>
    /// How far from his spot he is put straight back instead of walked: past this he has lost his way (a walk to the spot failed and left him
    /// across town), and a walk would fail again.
    /// </summary>
    const float LostReach = 150;

    /// <summary>
    /// Game hours between tries to place him when a try fails.
    /// </summary>
    const double RetryHours = 1;

    /// <summary>
    /// The bar once found (it does not move).
    /// </summary>
    BarPlace? _place;

    /// <summary>
    /// When the next try to place him may run, in game hours.
    /// </summary>
    double _nextTry;

    /// <summary>
    /// One check: places him while Mourn is loaded and he is not in the world yet, holds his loaded squad, keeps him at the bar and his stock in the
    /// pen.
    /// </summary>
    public void Run(World world, RngPurpose purpose)
    {
        if (population is null || Place(world) is not { } place) return;

        var platoon = world.Platoons.All.FirstOrDefault(p => p.ModTag == Tag) ?? Spawn(world, place);
        if (platoon is null) return;

        PopulationSystem.HoldPlatoon(world, platoon.Id, true);
        if (platoon.SquadId < 0 || world.Squads.Find(platoon.SquadId) is not { } squad || Trader(world, squad) is not { } trader) return;

        KeepAtBar(world, trader, place);
        KeepStock(world, squad, place, trader, purpose);
    }

    /// <summary>
    /// The bar, found once while Mourn is loaded.
    /// </summary>
    BarPlace? Place(World world) => _place ??= MournBar.Find(world, population!.Data, PenRadius);

    /// <summary>
    /// Adds his platoon behind the bar (it loads as the player is near); null when that fails, tried again an hour later.
    /// </summary>
    Platoon? Spawn(World world, BarPlace place)
    {
        if (world.GameHours < _nextTry) return null;

        int faction = population!.Data.FactionIndex(Faction);
        int id = faction < 0 ? -1 : population.SpawnPlatoon(world, faction, HusbandryData.TraderSquad, place.Site.Index, place.Pen, 0, PenRadius);
        if (world.Platoons.Find(id) is not { } platoon)
        {
            _nextTry = world.GameHours + RetryHours;
            log("husbandry: could not place the Beastmaster in Mourn (is Husbandry.mod generated?); trying again in an hour");
            return null;
        }

        platoon.ModTag = Tag;
        log($"husbandry: the Beastmaster arrives at Mourn's bar, his pen at {place.Pen.X:0}, {place.Pen.Y:0}");
        return platoon;
    }

    /// <summary>
    /// The Beastmaster himself: his squad's living member that is no animal; null when he is dead.
    /// </summary>
    static int? Trader(World world, Squad squad)
    {
        var table = world.Characters;
        foreach (var id in squad.Members)
        {
            if (table.TryResolveNext(id, out int s) && table.Cold(s) is { Medical.Dead: false } c && !SquadRegistry.IsAnimal(c)) return s;
        }

        return null;
    }

    /// <summary>
    /// Keeps him off the AI, walks him back to his spot in the bar whenever he stands idle away from it, and puts him there when he is far from it
    /// out of a fight.
    /// </summary>
    static void KeepAtBar(World world, int trader, BarPlace place)
    {
        var cold = world.Characters.Cold(trader)!;
        cold.Engaged = true;

        var at = world.Characters.Next[trader].Position;
        float away = Vector2.Distance(new Vector2(at.X, at.Z), new Vector2(place.Counter.X, place.Counter.Z));
        if (away > LostReach && !cold.InCombat && !Hitches.Down(cold))
        {
            PutAt(world, trader, place.Counter);
            return;
        }

        if (away <= CounterReach || !Idle(world, trader, cold)) return;

        TaskStart.Walk(world, trader, place.Counter, SpeedMode.Walk, TaskType.MoveOnFreeWill, TaskSource.Npc);
    }

    /// <summary>
    /// Stands a character at a point, on the floor nearest its height there, as the debug menu's teleport does.
    /// </summary>
    static void PutAt(World world, int slot, Vector3 point)
    {
        var table = world.Characters;
        TaskStart.Stand(table, slot);
        table.Next[slot].Position = new Vector3(point.X, world.Walkability.GroundHeight(point.X, point.Z, point.Y), point.Z);
        table.Next[slot].Velocity = Vector3.Zero;
    }

    /// <summary>
    /// Whether a character stands idle and able.
    /// </summary>
    static bool Idle(World world, int slot, CharacterCold cold) =>
        !Hitches.Down(cold) && (CharacterTask)world.Characters.Next[slot].Task == CharacterTask.Idle;

    #endregion

    #region Saving and the debug view

    /// <summary>
    /// Writes the pen's places.
    /// </summary>
    public void Write(BinaryWriter w)
    {
        w.Write(Slots.Count);
        foreach (var slot in Slots)
        {
            w.Write(slot.Serial);
            w.Write(slot.RefillAt);
        }
    }

    /// <summary>
    /// Reads the pen's places (a count that does not match today's places is skipped).
    /// </summary>
    public void Read(BinaryReader r)
    {
        int count = r.ReadInt32();
        for (int k = 0; k < count; k++)
        {
            uint serial = r.ReadUInt32();
            double refill = r.ReadDouble();
            if (k >= Slots.Count) continue;

            Slots[k].Serial = serial;
            Slots[k].RefillAt = refill;
        }
    }

    /// <summary>
    /// Adds the pen's places to the state hash.
    /// </summary>
    public void Hash(ref StateHasher hasher)
    {
        foreach (var slot in Slots)
        {
            hasher.Add(slot.Serial);
            hasher.Add(slot.RefillAt);
        }
    }

    /// <summary>
    /// The debug view: where the bar and pen are, and each place.
    /// </summary>
    public string Describe(World world)
    {
        if (_place is not { } place) return "Beastmaster: Mourn's bar not found yet (visit Mourn).";

        var text = new StringBuilder($"Beastmaster: pen at {place.Pen.X:0}, {place.Pen.Y:0}");
        text.AppendLine(world.Platoons.All.Any(p => p.ModTag == Tag) ? "" : " (not placed yet)");
        foreach (var slot in Slots)
        {
            string state = slot.Serial != 0 ? "in the pen" : slot.RefillAt < 0 ? "filling" : $"sold, back at hour {slot.RefillAt:0}";
            text.AppendLine($"  {Kinds[slot.Kind].Name} ({(slot.Female ? "female" : "male")}): {state}");
        }

        return text.ToString();
    }

    #endregion
}
