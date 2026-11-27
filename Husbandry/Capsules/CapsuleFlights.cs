using System.Numerics;
using Meitou.Modding;

namespace Husbandry.Capsules;

/// <summary>
/// Draws thrown capsules in flight (docs/husbandry.md "Beast Capsules"), display only: once a tick of each flight (<see cref="CapsuleThrows.Flights"/>)
/// a small, short puff of the mist's particles, tinted the capsule's red, at the capsule's place on its arc from the hand to the beast, so the
/// throw leaves a red streak. The engine has no way for a mod to draw a flying item, so the streak stands in for it. Host's thread.
/// </summary>
public sealed class CapsuleFlights(Action<EffectAnchor> puff)
{
    /// <summary>
    /// How long a puff emits, in seconds.
    /// </summary>
    public const float PuffSeconds = 0.15f;

    /// <summary>
    /// The puffs' tint: the capsule's lacquer red, bright.
    /// </summary>
    public static readonly Vector3 Tint = new(1.6f, 0.15f, 0.12f);

    /// <summary>
    /// The puffs' size against the mist's.
    /// </summary>
    const float Size = 0.12f;

    /// <summary>
    /// The tick each flight (by its release tick) was last drawn at, so a tick draws once however many frames show it.
    /// </summary>
    readonly Dictionary<long, long> _drawn = [];

    /// <summary>
    /// Once a frame: a puff for each flight in the air at the frame's tick, not yet drawn at it.
    /// </summary>
    public void Draw(ModFrame frame, CapsuleThrows? throws)
    {
        var flights = throws?.Flights ?? [];
        long tick = frame.Tick;
        foreach (var flight in flights)
        {
            if (tick < flight.Released || tick > flight.Lands) continue;
            if (_drawn.TryGetValue(flight.Released, out long last) && last == tick) continue;

            _drawn[flight.Released] = tick;
            float progress = flight.Lands > flight.Released ? (float)(tick - flight.Released) / (flight.Lands - flight.Released) : 1;
            puff(EffectAnchor.At(CapsuleThrows.AlongFlight(flight, progress)).Sized(Size));
        }

        if (_drawn.Count > 32) _drawn.Clear();
    }
}
