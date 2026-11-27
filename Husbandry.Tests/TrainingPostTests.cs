using Husbandry.Training;
using Meitou.Simulation;

namespace Husbandry.Tests;

/// <summary>
/// The Training Post (docs/husbandry.md "Training"): one animal a post, kept while it trains there, and the trainer's drill every
/// <see cref="TrainingDrills.PeriodSeconds"/>.
/// </summary>
public sealed class TrainingPostTests
{
    /// <summary>
    /// The game's tick length: 30 ticks a second.
    /// </summary>
    const float Tick = 1f / 30;

    /// <summary>
    /// A post takes the first animal sent; a second sent later does not replace it, even when it comes first in slot order. Catches two animals
    /// training at once, or a newcomer taking an animal's place.
    /// </summary>
    [Fact]
    public void A_post_keeps_its_one_animal_while_it_trains_there()
    {
        var first = new CharacterId(5, 0);
        var second = new CharacterId(2, 0);
        var pupils = new Dictionary<string, CharacterId>();

        TrainingPost.Choose(pupils, [("post", first)]);
        TrainingPost.Choose(pupils, [("post", second), ("post", first)]);

        Assert.Equal(first, pupils["post"]);
    }

    /// <summary>
    /// When the post's animal leaves, the next in slot order takes its place; a post no animal trains at is forgotten, and posts do not share.
    /// </summary>
    [Fact]
    public void The_next_animal_takes_over_and_posts_keep_apart()
    {
        var a = new CharacterId(1, 0);
        var b = new CharacterId(2, 0);
        var c = new CharacterId(3, 0);
        var pupils = new Dictionary<string, CharacterId> { ["post"] = a, ["gone"] = a };

        TrainingPost.Choose(pupils, [("post", b), ("post", c), ("other", c)]);

        Assert.Equal(b, pupils["post"]);
        Assert.Equal(c, pupils["other"]);
        Assert.False(pupils.ContainsKey("gone"));
    }

    /// <summary>
    /// The drill plays from the pair's start for the clip's length, then rests until the next period, and repeats; nothing before the start.
    /// </summary>
    [Fact]
    public void The_drill_plays_at_the_start_of_every_period()
    {
        const float clip = 2.967f;
        long period = (long)(TrainingDrills.PeriodSeconds * 30);

        Assert.Equal(0f, TrainingDrills.Progress(100, 100, Tick, clip)!.Value);
        Assert.Equal(0.5f, TrainingDrills.Progress(100, 100 + (long)(clip / 2 * 30), Tick, clip)!.Value, 1);
        Assert.Null(TrainingDrills.Progress(100, 100 + 4 * 30, Tick, clip));
        Assert.Null(TrainingDrills.Progress(100, 100 + period - 1, Tick, clip));
        Assert.Equal(0f, TrainingDrills.Progress(100, 100 + period, Tick, clip)!.Value, 3);
        Assert.Equal(0f, TrainingDrills.Progress(100, 100 + 100 * period, Tick, clip)!.Value, 3);
        Assert.Null(TrainingDrills.Progress(100, 99, Tick, clip));
        Assert.Null(TrainingDrills.Progress(100, 100, Tick, 0));
    }

    /// <summary>
    /// The drill clip fits in its period, so the trainer rests between drills.
    /// </summary>
    [Fact]
    public void The_cartographer_clip_fits_in_the_drill_period()
    {
        Assert.InRange(KenshiRaces.RaceAnimations.Cartographer.Length, 0.5f, TrainingDrills.PeriodSeconds - 1);
    }
}
