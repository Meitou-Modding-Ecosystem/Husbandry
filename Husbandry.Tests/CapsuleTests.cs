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
    static StoredBeast Beast(IReadOnlyList<(string, float)>? traits = null) => new("12-gamedata.base", "Gorillo", 0.8f, "Grumble", true,
        [((int)StatsEnumerated.Strength, 45.5f), ((int)StatsEnumerated.MeleeAttack, 30)], [("husbandry.guard", 12), ("husbandry.hunt", 0.4f)],
        traits ?? []);

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
    public void A_beast_keeps_its_kind_and_traits_through_a_save()
    {
        var beast = Beast([("husbandry.rare", 1.5f)]);
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) beast.Write(w);

        stream.Position = 0;
        using var r = new BinaryReader(stream);
        var read = StoredBeast.Read(r, 6);

        Assert.Equal("Gorillo", read.Kind);
        Assert.Equal("Grumble", read.Name);
        Assert.True(read.Female);
        Assert.Equal(45.5f, read.Stat(StatsEnumerated.Strength));
        Assert.Equal(new[] { ("husbandry.rare", 1.5f) }, read.Traits);
        Assert.Equal(beast.Skills, read.Skills);
    }

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
