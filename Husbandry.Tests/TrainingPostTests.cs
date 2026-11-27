using Husbandry.Capsules;
using Husbandry.Training;
using Meitou.Data.Ogre;
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
    /// Husbandry's animation files hold both clips at the lengths the code plays them for, on the game's 30 bones, where the ANIMATION_FILE
    /// points; the drill fits in its period. Catches a clip missing from a file (the trainer would stand still) or a bad copy.
    /// </summary>
    [Theory]
    [InlineData("husbandry_anims_male.skeleton")]
    [InlineData("husbandry_anims_female.skeleton")]
    public void The_animation_files_hold_the_throw_and_the_drill(string file)
    {
        var skeleton = OgreSkeletonReader.Read(File.ReadAllBytes(Path.Combine(Folder(), file)));

        Assert.Equal(30, skeleton.Bones.Count);
        Assert.Equal(1.7f, Assert.Single(skeleton.Animations, a => a.Name == ThrowData.Clip).Length, 0.01f);
        var drill = Assert.Single(skeleton.Animations, a => a.Name == ThrowData.DrillClip);
        Assert.Equal(ThrowData.DrillSeconds, drill.Length, 0.01f);
        Assert.InRange(drill.Length, 0.5f, TrainingDrills.PeriodSeconds - 1);
        Assert.EndsWith(@"\items\animations\" + file, file.Contains("female") ? ThrowData.FemaleFile : ThrowData.MaleFile);
    }

    /// <summary>
    /// The drill's technique plays its clip and is disabled, so no fight picks it.
    /// </summary>
    [Fact]
    public void The_drill_technique_is_disabled_and_plays_the_drill_clip()
    {
        var drill = Assert.Single(ThrowData.Build(), r => r.StringId == ThrowData.DrillTechnique);

        Assert.Equal(ThrowData.DrillClip, drill.Strings["anim name"]);
        Assert.True(drill.Bools["disabled"]);
    }

    /// <summary>
    /// The repo's Husbandry/items/animations, found walking up from the test's output folder.
    /// </summary>
    static string Folder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string path = Path.Combine(dir.FullName, "Husbandry", "items", "animations");
            if (Directory.Exists(path)) return path;
        }

        throw new DirectoryNotFoundException("No Husbandry/items/animations above " + AppContext.BaseDirectory);
    }
}
