using System.Numerics;
using Husbandry.Capsules;
using Meitou.Data.Fcs;
using Meitou.Data.Gameplay.Bodies;
using Meitou.Simulation;

namespace Husbandry.Tests;

/// <summary>
/// Beast Capsules (docs/husbandry.md "Beast Capsules", issue #114): a filled capsule bound to its beast acts once per settle time (so clicks while
/// paused cannot summon twice) and toggles summon and recall; the empty and filled records; the beast's saved layout; the throw's facing, landing
/// and flight; the tooltip.
/// </summary>
public sealed class CapsuleTests
{
    /// <summary>
    /// A beast as a test keeps it.
    /// </summary>
    static StoredBeast Beast(IReadOnlyList<(string, float)>? traits = null, IReadOnlyList<(string, string)>? tags = null) => new("12-gamedata.base",
        "Gorillo", 0.8f, "Grumble", true, [((int)StatsEnumerated.Strength, 45.5f), ((int)StatsEnumerated.MeleeAttack, 30)],
        [("husbandry.guard", 12), ("husbandry.hunt", 0.4f)], traits ?? [], tags ?? []);

    [Fact]
    public void A_capsule_summons_then_recalls_and_does_nothing_again_until_it_has_settled()
    {
        var bound = new BoundBeast(Beast());

        Assert.Equal(CapsuleAction.Empty, CapsuleSystem.Decide(null, 100));
        Assert.Equal(CapsuleAction.Summon, CapsuleSystem.Decide(bound, 100));

        bound.UsedAt = 100;
        bound.Out = new LastingId(7);
        Assert.Equal(CapsuleAction.Wait, CapsuleSystem.Decide(bound, 100));
        Assert.Equal(CapsuleAction.Wait, CapsuleSystem.Decide(bound, 100 + CapsuleSystem.SettleTicks - 1));
        Assert.Equal(CapsuleAction.Recall, CapsuleSystem.Decide(bound, 100 + CapsuleSystem.SettleTicks));

        bound.Out = LastingId.None;
        Assert.Equal(CapsuleAction.Summon, CapsuleSystem.Decide(bound, 200));
    }

    [Fact]
    public void A_tag_names_its_capsule_key()
    {
        Assert.Equal(42, CapsuleSystem.Key(CapsuleSystem.TagPrefix + "42"));
        Assert.Null(CapsuleSystem.Key(""));
        Assert.Null(CapsuleSystem.Key(CapsuleSystem.TagPrefix + "x"));
    }

    [Fact]
    public void The_filled_capsule_never_stacks_wears_the_red_icon_and_has_no_recipe()
    {
        var empty = HusbandryData.New(HusbandryData.Capsule, FcsRecordType.Item, "Beast Capsule");
        empty.Ints["stackable"] = Capsules.CapsuleData.EmptyStack;
        empty.Ints["production time"] = 4;
        empty.Ints["value"] = Capsules.CapsuleData.Price;
        empty.Filenames["icon"] = Capsules.CapsuleData.Icon;
        empty.References["ingredients"] = [new FcsReference("42159-gamedata.base", 200, 0, 0)];

        var filled = Capsules.CapsuleData.Filled(empty);

        Assert.Equal(5, Capsules.CapsuleData.EmptyStack);
        Assert.Equal(HusbandryData.FilledCapsule, filled.StringId);
        Assert.Equal(1, filled.Ints["stackable"]);
        Assert.Equal(Capsules.CapsuleData.FilledIcon, filled.Filenames["icon"]);
        Assert.Equal(Capsules.CapsuleData.Price, filled.Ints["value"]);
        Assert.False(filled.References.ContainsKey("ingredients"));
        Assert.False(filled.Ints.ContainsKey("production time"));
        Assert.Equal(Capsules.CapsuleData.EmptyStack, empty.Ints["stackable"]);
    }

    [Fact]
    public void A_beast_keeps_its_kind_traits_and_mod_tags_through_a_save()
    {
        var beast = Beast([("husbandry.rare", 1.5f)], [("other.mod", "rival|7")]);
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) beast.Write(w);

        stream.Position = 0;
        using var r = new BinaryReader(stream);
        var read = StoredBeast.Read(r, HusbandrySystem.SaveVersion);

        Assert.Equal(stream.Length, stream.Position);
        Assert.Equal("Gorillo", read.Kind);
        Assert.Equal("Grumble", read.Name);
        Assert.True(read.Female);
        Assert.Equal(45.5f, read.Stat(StatsEnumerated.Strength));
        Assert.Equal(new[] { ("husbandry.rare", 1.5f) }, read.Traits);
        Assert.Equal(beast.Skills, read.Skills);
        Assert.Equal(new[] { ("other.mod", "rival|7") }, read.Tags);
    }

    /// <summary>
    /// A version 6 beast (written before mod tags were kept) reads with its traits and no tags, to the end of its bytes.
    /// </summary>
    [Fact]
    public void A_version_6_beast_reads_without_mod_tags()
    {
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) Beast([("husbandry.rare", 2)]).Write(w);

        stream.SetLength(stream.Length - sizeof(int));
        stream.Position = 0;
        using var r = new BinaryReader(stream);
        var read = StoredBeast.Read(r, 6);

        Assert.Equal(stream.Length, stream.Position);
        Assert.Equal(new[] { ("husbandry.rare", 2f) }, read.Traits);
        Assert.Empty(read.Tags);
    }

    /// <summary>
    /// Keeping a beast reads its traits from the traits tag and keeps every other mod's tag as it is. Catches traits lost on recall, or the traits
    /// tag kept twice (as a tag and as traits).
    /// </summary>
    [Fact]
    public void Keeping_a_beast_splits_its_traits_from_other_mods_tags()
    {
        var tags = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            [BeastTraits.TagMod] = "husbandry.rare=1.5;other.swift=2",
            ["other.mod"] = "rival|7",
        };

        var beast = StoredBeast.Of(new CharacterCold { Name = "Grumble" }, true, tags);

        Assert.Equal(new[] { ("husbandry.rare", 1.5f), ("other.swift", 2f) }, beast.Traits);
        Assert.Equal(new[] { ("other.mod", "rival|7") }, beast.Tags);
    }

    /// <summary>
    /// The traits tag reads back what it wrote, in id order, and skips an entry that does not read.
    /// </summary>
    [Fact]
    public void The_traits_tag_round_trips_and_skips_bad_entries()
    {
        string tag = BeastTraits.Encode(new[] { ("z.last", -0.25f), ("a.first", 3f) });

        Assert.Equal("a.first=3;z.last=-0.25", tag);
        Assert.Equal(new[] { ("a.first", 3f), ("z.last", -0.25f) }, BeastTraits.Decode(tag));
        Assert.Equal(new[] { ("ok", 1f) }, BeastTraits.Decode("ok=1;=2;broken;bad=x"));
        Assert.Empty(BeastTraits.Decode(null));
        Assert.Throws<ArgumentException>(() => BeastTraits.Register(new BeastTrait("bad=id", "Bad")));
    }

    /// <summary>
    /// In a world: a trait another mod gives a living animal, and another mod's tag on it, go into the capsule with it (off the old body) and
    /// come back on the new body a summon makes. Catches stat gains or mod data lost between capsule trips.
    /// </summary>
    [Fact]
    public void Traits_and_mod_tags_go_into_the_capsule_and_back_onto_the_new_body()
    {
        using var world = new World(new WorldSettings { Seed = 3, Threads = 1, PublishSnapshots = false }, new OpenGroundWalkability((_, _) => 0), []);
        var animal = Spawn(world, "Grumble");
        Assert.True(BeastTraits.Set(world, animal, "other.swift", 2));
        Assert.True(world.CharacterTags.Set(animal, "other.mod", "rival|7"));

        var cold = world.Characters.Cold(animal.Slot)!;
        var beast = StoredBeast.Take(world, animal, cold, true);

        Assert.Empty(world.CharacterTags.Of(animal));
        var body = Spawn(world, "new body");
        beast.Restore(world, body, world.Characters.Cold(body.Slot)!);

        Assert.Equal("Grumble", world.Characters.Cold(body.Slot)!.Name);
        Assert.Equal(2f, BeastTraits.Get(world, body, "other.swift"));
        Assert.Equal("rival|7", world.CharacterTags.Get(body, "other.mod"));
        Assert.True(BeastTraits.Set(world, body, "other.swift", null));
        Assert.Null(BeastTraits.Get(world, body, "other.swift"));
    }

    /// <summary>
    /// A character added to <paramref name="world"/>.
    /// </summary>
    static CharacterId Spawn(World world, string name) =>
        world.Characters.Spawn(new CharacterHot { Position = new Vector3(1000, 150, 1000) }, new CharacterCold { Name = name }, world.Tick);

    [Fact]
    public void An_older_save_beast_reads_without_kind_or_traits()
    {
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write("12-gamedata.base");
            w.Write(0.8f);
            w.Write("Grumble");
            w.Write(false);
            w.Write(0);
            w.Write(1);
            w.Write("husbandry.guard");
            w.Write(12f);
        }

        stream.Position = 0;
        using var r = new BinaryReader(stream);
        var read = StoredBeast.Read(r, 5);

        Assert.Equal("", read.Kind);
        Assert.Empty(read.Traits);
        Assert.Equal(new[] { ("husbandry.guard", 12f) }, read.Skills);
        Assert.Equal(stream.Length, stream.Position);
    }

    [Fact]
    public void The_thrower_faces_the_beast_and_a_farther_beast_lands_later()
    {
        Assert.Equal(0f, CapsuleThrows.YawTowards(Vector3.Zero, new Vector3(0, 0, 10)), 4);
        Assert.Equal(MathF.PI / 2, CapsuleThrows.YawTowards(Vector3.Zero, new Vector3(10, 0, 0)), 4);
        Assert.Equal(CapsuleThrows.ReleaseSeconds, CapsuleThrows.LandAt(0), 4);
        Assert.Equal(CapsuleThrows.ReleaseSeconds + 1, CapsuleThrows.LandAt(CapsuleThrows.FlightSpeed), 4);
    }

    [Fact]
    public void A_capsule_flies_from_the_hand_to_the_beast_on_an_arc()
    {
        var flight = new CapsuleThrows.Flight(new Vector3(0, 17, 0), new Vector3(100, 5, 0), 10, 20);

        Assert.Equal(flight.From, CapsuleThrows.AlongFlight(flight, 0));
        Assert.Equal(flight.To, CapsuleThrows.AlongFlight(flight, 1));
        var middle = CapsuleThrows.AlongFlight(flight, 0.5f);
        Assert.True(middle.Y > (flight.From.Y + flight.To.Y) / 2 + 10, "the middle of the flight is lifted");
    }

    [Fact]
    public void The_tooltip_names_the_beast_and_its_kind_and_fits_the_engine_limits()
    {
        var lines = CapsuleTooltip.Lines(new BeastView(Beast([("not.registered", 2)]), Out: true));

        Assert.InRange(lines.Count, 1, CapsuleTooltip.MaxLines);
        Assert.Equal("Contains", lines[0].Label);
        Assert.Equal("Grumble (Gorillo)", lines[0].Value);
        Assert.Contains(lines, l => l.Value == "Out of the capsule");
        Assert.Contains(lines, l => l.Label == "Skills" && l.Value == "Guard 12");
        Assert.Contains(lines, l => l.Label == "Traits" && l.Value == "not.registered 2");
        Assert.Contains(lines, l => l.Value!.Contains("Str 45", StringComparison.Ordinal));
        Assert.All(lines, l => Assert.True((l.Value ?? "").Length <= CapsuleTooltip.MaxText));
    }

    [Fact]
    public void A_registered_trait_shows_by_its_label()
    {
        BeastTraits.Register(new BeastTrait("tests.swift", "Swift"));

        Assert.Equal("Swift +1.5", BeastTraits.Show("tests.swift", 1.5f));
        Assert.Throws<InvalidOperationException>(() => BeastTraits.Register(new BeastTrait("tests.swift", "Again")));
    }

    [Fact]
    public void A_capsule_without_a_beast_says_so()
    {
        var lines = CapsuleTooltip.Lines(null);

        Assert.Single(lines);
        Assert.Equal("Holds no beast", lines[0].Label);
    }
}
