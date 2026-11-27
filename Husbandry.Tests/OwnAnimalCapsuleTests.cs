using System.Numerics;
using AnimalBehaviors;
using Husbandry.Capsules;
using Meitou.Data.Gameplay.Bodies;
using Meitou.Simulation;
using Meitou.Simulation.Bodies;
using Meitou.Simulation.Combat;
using Meitou.Simulation.Items;

namespace Husbandry.Tests;

/// <summary>
/// Beast Capsules on the player's own animals (docs/husbandry.md "Beast Capsules"): an own animal not bound to a capsule is caught without a
/// knockout, by hand or by a throw, while a wild one must be knocked out and a person never is.
/// </summary>
public sealed class OwnAnimalCapsuleTests
{
    /// <summary>
    /// A world with nothing in it.
    /// </summary>
    static World Empty() => new(new WorldSettings { Seed = 5, Threads = 1, PublishSnapshots = false }, new OpenGroundWalkability((_, _) => 0), []);

    /// <summary>
    /// A capsule system whose every race supports everything.
    /// </summary>
    static CapsuleSystem Capsules() => new(null, new Sexes(), new AnimalRegistry(_ => (AnimalSupport)~0), _ => { });

    /// <summary>
    /// A character: an animal of a race when <paramref name="animal"/>, the player's when <paramref name="player"/>.
    /// </summary>
    static CharacterId Spawn(World world, bool animal, bool player)
    {
        var cold = new CharacterCold
        {
            Name = animal ? "Grumble" : "Beep",
            IsPlayer = player,
            Race = new RaceData { StringId = "12-gamedata.base", Name = "Gorillo" },
            Fighter = animal ? new Fighter(null) { IsAnimal = true } : null,
            Medical = new MedicalState([]),
        };
        return world.Characters.Spawn(new CharacterHot { Position = new Vector3(1000, 0, 1000) }, cold, world.Tick);
    }

    /// <summary>
    /// The player's own awake animal may be caught; a wild awake one and one of the player's people may not. Catches the capsule refusing tame
    /// animals, or catching people.
    /// </summary>
    [Fact]
    public void Own_animals_are_caught_awake_but_wild_ones_and_people_are_not()
    {
        using var world = Empty();
        var capsules = Capsules();
        var own = Spawn(world, animal: true, player: true);
        var wild = Spawn(world, animal: true, player: false);
        var person = Spawn(world, animal: false, player: true);

        Assert.True(capsules.Catchable(Cold(world, own)));
        Assert.False(capsules.Catchable(Cold(world, wild)));
        Assert.False(capsules.Catchable(Cold(world, person)));
    }

    /// <summary>
    /// An animal wearing a backpack can't go into a capsule, by hand or by a throw, until the backpack comes off. Catches a pack animal's load
    /// vanishing into a capsule with it.
    /// </summary>
    [Fact]
    public void An_animal_wearing_a_backpack_is_not_caught()
    {
        using var world = Empty();
        var capsules = Capsules();
        var throws = new CapsuleThrows(capsules, HusbandryData.Capsule, null, Sexes.Purpose);
        var own = Spawn(world, animal: true, player: true);
        var cold = Cold(world, own);
        cold.Inventory = new Inventory();
        var pack = new ItemInstance { Record = "pack", Section = "backpack_attach" };
        cold.Inventory.Items.Add(pack);

        throws.Run(world);
        Assert.False(capsules.Catchable(cold));
        Assert.False(throws.Catchable(own));

        cold.Inventory.Items.Remove(pack);
        Assert.True(capsules.Catchable(cold));
    }

    /// <summary>
    /// The Throw Beast Capsule row's view lists the player's own animal as a target. Catches the row showing only on knocked-out wild beasts.
    /// </summary>
    [Fact]
    public void The_throw_row_offers_the_players_own_animal()
    {
        using var world = Empty();
        var capsules = Capsules();
        var throws = new CapsuleThrows(capsules, HusbandryData.Capsule, null, Sexes.Purpose);
        var own = Spawn(world, animal: true, player: true);
        var wild = Spawn(world, animal: true, player: false);

        throws.Run(world);

        Assert.True(throws.Catchable(own));
        Assert.False(throws.Catchable(wild));
    }

    /// <summary>
    /// A beast caught by a right click on its capsule leaves the world: the click runs in the inputs phase, where the engine refuses removals, so
    /// the body leaves at the slow world, and can't be caught again before then. Catches the capsule copying the beast and leaving it standing
    /// (Matthew's play-test 2026-11-27: the Gorillo stayed and a second one came out).
    /// </summary>
    [Fact]
    public void A_beast_put_in_a_capsule_during_the_inputs_phase_leaves_the_world()
    {
        using var world = Empty();
        var capsules = Capsules();
        var own = Spawn(world, animal: true, player: true);
        world.AddSystem(new CatchOnInputs(capsules, own));

        world.RunTick(false);

        Assert.False(world.Characters.IsAlive(own));
        world.RunTick(false);
        Assert.False(world.Characters.IsAlive(own));
        Assert.False(world.Characters.TryResolveNext(own, out _));
    }

    /// <summary>
    /// A system that puts one beast in a capsule in the inputs phase, as an item use does, and checks it can't be caught again that tick.
    /// </summary>
    sealed class CatchOnInputs(CapsuleSystem capsules, CharacterId beast) : ITickSystem
    {
        /// <summary>
        /// Marks the beast to leave, as a catch does.
        /// </summary>
        public void Inputs(World world, IReadOnlyList<SimCommand> commands)
        {
            if (!world.Characters.TryResolveNext(beast, out int slot)) return;

            
            capsules.Leave(world, slot);
            Assert.False(capsules.Catchable(world.Characters.Cold(slot)!));
        }

        /// <summary>
        /// Takes the marked beast out, as the Husbandry system does.
        /// </summary>
        public void SlowWorld(World world) => capsules.Settle(world);
    }

    /// <summary>
    /// The character's cold state.
    /// </summary>
    static CharacterCold Cold(World world, CharacterId id)
    {
        Assert.True(world.Characters.TryResolveNext(id, out int slot));
        return world.Characters.Cold(slot)!;
    }
}
