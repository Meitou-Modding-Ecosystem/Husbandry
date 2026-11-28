using System.Numerics;
using AnimalBehaviors;
using Husbandry.Training;
using Meitou.Data.Gameplay.Ai;
using Meitou.Data.Gameplay.Bodies;
using Meitou.Data.World;
using Meitou.Modding;
using Meitou.Simulation;
using Meitou.Simulation.Ai;
using Meitou.Simulation.Bodies;
using Meitou.Simulation.Combat;
using Meitou.Simulation.Construction;
using Meitou.Simulation.Movement;
using Meitou.Simulation.Production;

namespace Husbandry.Tests;

/// <summary>
/// A lesson at the Training Post (docs/husbandry.md "Training"): the trainer and the animal stand within 5 m of the post, facing each other, and
/// the animal learns only while both are there (Matthew's play-test 2026-11-28: Beetle trained Black Gorillo from across the pen).
/// </summary>
public sealed class TrainingLessonTests
{
    /// <summary>
    /// The skill the tests train.
    /// </summary>
    static readonly ModSkill Skill = new("test.skill", "Beast: Test", SkillColumn.Sciences);

    /// <summary>
    /// The test skill's training job type.
    /// </summary>
    const TaskType TrainType = (TaskType)4510;

    /// <summary>
    /// The trainer's and the animal's spots stand within 5 m of the post, apart from each other, and turn with the post. Catches the pair standing
    /// on one spot, or a spot out of the lesson's reach.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(MathF.PI / 2)]
    public void The_spots_stand_within_the_lesson_reach_of_the_post(float turn)
    {
        using var world = Empty();
        var post = Post(world, Quaternion.CreateFromAxisAngle(Vector3.UnitY, turn));

        var trainer = Trainers.TrainerSpot(world, post);
        var pupil = Trainers.PupilSpot(world, post);

        Assert.InRange(Flat(trainer, post.Position), 1, Trainers.LessonReach);
        Assert.InRange(Flat(pupil, post.Position), 1, Trainers.LessonReach);
        Assert.InRange(Flat(trainer, pupil), 30, 2 * Trainers.LessonReach);
    }

    /// <summary>
    /// With a trainer working the post, an animal grazing far off learns nothing and is walked to its spot by the post. Catches the animal
    /// learning from across the pen while the trainer stands alone.
    /// </summary>
    [Fact]
    public void An_animal_far_from_the_post_walks_to_it_and_learns_nothing_meanwhile()
    {
        using var world = Empty();
        var post = Post(world, Quaternion.Identity);
        var registry = Registry();
        var trainer = Trainer(world, post, Trainers.TrainerSpot(world, post));
        var animal = Animal(world, post, post.Position + new Vector3(90, 0, 0));

        var lessons = Run(world, registry);

        var lesson = Assert.Single(lessons);
        Assert.Equal(Slot(world, trainer), lesson.Trainer);
        Assert.False(lesson.Ready);
        Assert.Equal(Skill.Start, AnimalSkills.Level(Cold(world, animal), Skill));
        var walk = Cold(world, animal).OrderQueue.Front;
        Assert.NotNull(walk);
        Assert.True(walk.IsMove);
        Assert.InRange(Flat(walk.Point, Trainers.PupilSpot(world, post)), 0, Trainers.SpotReach);
    }

    /// <summary>
    /// With the trainer and the animal both by the post, the lesson is ready and the animal learns. Catches the lesson never starting once the
    /// pair has come together.
    /// </summary>
    [Fact]
    public void An_animal_by_the_post_learns_from_its_trainer()
    {
        using var world = Empty();
        var post = Post(world, Quaternion.Identity);
        var registry = Registry();
        Trainer(world, post, Trainers.TrainerSpot(world, post));
        var animal = Animal(world, post, Trainers.PupilSpot(world, post));

        var lesson = Assert.Single(Run(world, registry));

        Assert.True(lesson.Ready);
        Assert.True(AnimalSkills.Level(Cold(world, animal), Skill) > Skill.Start);
    }

    /// <summary>
    /// A trainer working more than 5 m from the post does not teach: the animal waits for one. Catches a trainer teaching from 8 m off, where
    /// the old reach let it start work without walking.
    /// </summary>
    [Fact]
    public void A_trainer_more_than_5_m_from_the_post_does_not_teach()
    {
        using var world = Empty();
        var post = Post(world, Quaternion.Identity);
        var registry = Registry();
        Trainer(world, post, post.Position + new Vector3(0, 0, Trainers.LessonReach + 20));
        var animal = Animal(world, post, Trainers.PupilSpot(world, post));

        var lesson = Assert.Single(Run(world, registry));

        Assert.Equal(-1, lesson.Trainer);
        Assert.Equal(Skill.Start, AnimalSkills.Level(Cold(world, animal), Skill));
    }

    /// <summary>
    /// Once paired, the trainer and the animal turn to face each other between drills too. Catches the trainer facing the animal only while the
    /// drill clip plays.
    /// </summary>
    [Fact]
    public void A_paired_trainer_and_animal_face_each_other_between_drills()
    {
        using var world = Empty();
        var post = Post(world, Quaternion.Identity);
        var registry = Registry();
        var trainer = Trainer(world, post, Trainers.TrainerSpot(world, post));
        var animal = Animal(world, post, Trainers.PupilSpot(world, post));
        var drills = new TrainingDrills(null, 3, new HashSet<string>());

        drills.Pair(world, Run(world, registry));
        drills.Step(world);

        var table = world.Characters;
        int t = Slot(world, trainer), a = Slot(world, animal);
        Assert.Equal(Husbandry.Capsules.CapsuleThrows.YawTowards(table.Next[t].Position, table.Next[a].Position), table.Next[t].Yaw, 3);
        Assert.Equal(Husbandry.Capsules.CapsuleThrows.YawTowards(table.Next[a].Position, table.Next[t].Position), table.Next[a].Yaw, 3);
    }

    /// <summary>
    /// One training check of an hour.
    /// </summary>
    static List<Lesson> Run(World world, AnimalRegistry registry) =>
        TrainingPost.Run(world, registry, new AnimalSettings(), Sexes.Purpose, 1, "", [], new Trainers(registry, () => ""));

    /// <summary>
    /// A world of dry open ground with nothing on it but movement, the player's faction 0.
    /// </summary>
    static World Empty()
    {
        var ground = new OpenGroundWalkability((_, _) => WorldWater.Height + 100);
        var world = new World(new WorldSettings { Seed = 5, Threads = 1, PublishSnapshots = false }, ground, [new MovementSystem(new PathService(ground, true))]);
        world.Player.Faction = 0;
        return world;
    }

    /// <summary>
    /// A registry whose every race supports everything, training the test skill.
    /// </summary>
    static AnimalRegistry Registry()
    {
        var registry = new AnimalRegistry(_ => (AnimalSupport)~0);
        registry.AddSkill("test", Skill, TrainType, null);
        return registry;
    }

    /// <summary>
    /// A finished Training Post of the player's at (1000, 0, 1000).
    /// </summary>
    static BuildSite Post(World world, Quaternion rotation)
    {
        var post = new BuildSite
        {
            Id = "1-test-INGAME", Record = HusbandryData.TrainingPost, Position = new Vector3(1000, 0, 1000), Rotation = rotation, Owner = 0,
            Materials = [], Complete = true,
        };
        world.Buildings.Add(post);
        return post;
    }

    /// <summary>
    /// A person of the player's working the post's Train animals job, standing at <paramref name="at"/>.
    /// </summary>
    static CharacterId Trainer(World world, BuildSite post, Vector3 at)
    {
        var cold = new CharacterCold { Name = "Beetle", IsPlayer = true, Race = new RaceData { StringId = "crevan", Name = "Crevan" }, Medical = new MedicalState([]),
            Stats = new CharacterStats() };
        cold.Jobs.Add(new PermaJob(Trainers.Type, post.Id));
        cold.Running = new RunningTask(CharacterTask.Idle, Trainers.Type, TaskSource.Job, 0, CharacterId.None, post.Id, 0);
        return world.Characters.Spawn(new CharacterHot { Position = at }, cold, world.Tick);
    }

    /// <summary>
    /// An animal of the player's holding the post's training job, standing idle at <paramref name="at"/>.
    /// </summary>
    static CharacterId Animal(World world, BuildSite post, Vector3 at)
    {
        var cold = new CharacterCold
        {
            Name = "Black Gorillo", IsPlayer = true, Race = new RaceData { StringId = "12-gamedata.base", Name = "Gorillo" },
            Fighter = new Fighter(null) { IsAnimal = true }, Medical = new MedicalState([]), Stats = new CharacterStats(),
        };
        cold.Jobs.Add(new PermaJob(TrainType, post.Id));
        return world.Characters.Spawn(new CharacterHot { Position = at }, cold, world.Tick);
    }

    /// <summary>
    /// The distance on the ground between two points.
    /// </summary>
    static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));

    /// <summary>
    /// The character's slot.
    /// </summary>
    static int Slot(World world, CharacterId id)
    {
        Assert.True(world.Characters.TryResolveNext(id, out int slot));
        return slot;
    }

    /// <summary>
    /// The character's cold state.
    /// </summary>
    static CharacterCold Cold(World world, CharacterId id) => world.Characters.Cold(Slot(world, id))!;
}
