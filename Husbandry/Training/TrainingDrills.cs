using System.Collections.Frozen;
using Husbandry.Capsules;
using Meitou.Data.Gameplay.Combat;
using Meitou.Simulation;

namespace Husbandry.Training;

/// <summary>
/// The trainer's drill at a Training Post (docs/husbandry.md "Training"): while a trainer works the post for its animal, every
/// <see cref="PeriodSeconds"/> from the moment the pair formed the trainer turns to face the animal and plays the drill clip once (Kenshi Races'
/// <c>kr_cartographer</c> when that mod is loaded, as a mod combat move; without it the trainer only turns). Pairs come from each training check
/// (<see cref="Pair"/>), the facing from every serial step (<see cref="Step"/>). Nothing is saved: a loaded pair starts its drill again.
/// </summary>
public sealed class TrainingDrills(CombatTechnique? move, float clipSeconds)
{
    #region Drills

    /// <summary>
    /// Seconds from the start of one drill to the start of the next.
    /// </summary>
    public const float PeriodSeconds = 10;

    /// <summary>
    /// A trainer's drill: the animal it trains and the tick the pair formed.
    /// </summary>
    readonly record struct Drill(CharacterId Animal, long Since);

    /// <summary>
    /// The drills by trainer (serial steps only).
    /// </summary>
    readonly Dictionary<CharacterId, Drill> _drills = [];

    /// <summary>
    /// The drills' start ticks by trainer, published for <see cref="Move"/>, which runs on other threads.
    /// </summary>
    volatile FrozenDictionary<CharacterId, long> _since = FrozenDictionary<CharacterId, long>.Empty;

    /// <summary>
    /// The pairs of this check: a trainer that keeps its animal keeps its rhythm, a new pair starts now, a pair no longer training ends.
    /// </summary>
    public void Pair(World world, IReadOnlyList<Lesson> lessons)
    {
        var table = world.Characters;
        var pairs = new Dictionary<CharacterId, Drill>();
        foreach (var lesson in lessons)
        {
            if (lesson.Trainer < 0) continue;

            var trainer = table.IdOf(lesson.Trainer);
            var animal = table.IdOf(lesson.Animal.Slot);
            pairs[trainer] = _drills.TryGetValue(trainer, out var kept) && kept.Animal == animal ? kept : new Drill(animal, world.Tick);
        }

        _drills.Clear();
        foreach (var (trainer, drill) in pairs) _drills[trainer] = drill;
        _since = _drills.ToFrozenDictionary(d => d.Key, d => d.Value.Since);
    }

    /// <summary>
    /// The serial step: a trainer in its drill faces its animal.
    /// </summary>
    public void Step(World world)
    {
        var table = world.Characters;
        foreach (var (trainer, drill) in _drills)
        {
            if (Progress(drill.Since, world.Tick, world.TickSeconds, clipSeconds) is null) continue;
            if (!table.TryResolveNext(trainer, out int t) || !table.TryResolveNext(drill.Animal, out int a)) continue;

            table.Next[t].Yaw = CapsuleThrows.YawTowards(table.Next[t].Position, table.Next[a].Position);
        }
    }

    /// <summary>
    /// The drill clip as a mod combat move while the character is in its drill; null otherwise, or without the clip. Any thread.
    /// </summary>
    public CombatMove? Move(World world, CharacterId id, long tick)
    {
        if (move is null || !_since.TryGetValue(id, out long since)) return null;

        return Progress(since, tick, world.TickSeconds, clipSeconds) is { } progress ? new CombatMove(move, progress) : null;
    }

    /// <summary>
    /// How far into its drill a pair formed at <paramref name="since"/> is at <paramref name="tick"/>, 0 to 1, or null between drills: the drill
    /// plays for <paramref name="clip"/> seconds at the start of every <see cref="PeriodSeconds"/>.
    /// </summary>
    public static float? Progress(long since, long tick, float tickSeconds, float clip)
    {
        if (tick < since || clip <= 0) return null;

        double into = (tick - since) * (double)tickSeconds % PeriodSeconds;
        return into < clip ? (float)(into / clip) : null;
    }

    #endregion
}
