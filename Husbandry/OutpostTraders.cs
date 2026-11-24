using System.Text;
using AnimalBehaviors;
using Meitou.Modding;
using Meitou.Simulation;
using Meitou.Simulation.Population;

namespace Husbandry;

/// <summary>
/// Puts a Beastmaster at the player settlement numbered <see cref="Number"/> (<see cref="Meitou.Simulation.Construction.PlayerTown.Number"/>),
/// named <see cref="Name"/> when asked (the debug row "Husbandry: animal trader at settlement").
/// </summary>
public sealed record SettlementTraderCommand(int Number, string Name) : SimCommand;

/// <summary>
/// The Beastmasters the debug menu put at player settlements (docs/husbandry.md "Debug rows"), by settlement number: each keeps the stock and
/// shop of Mourn's, saved in the mod's sidecar and hashed. Also publishes the settlements for the debug row, which runs on the host's thread.
/// Serial steps only.
/// </summary>
public sealed class OutpostTraders(PopulationSystem? population, Sexes sexes, Action<string> log)
{
    /// <summary>
    /// The Beastmasters, by settlement number, in the order they came.
    /// </summary>
    readonly List<(int Number, string Name, Beastmaster Trader)> _traders = [];

    /// <summary>
    /// The numbers of the settlements with one, for the debug row.
    /// </summary>
    volatile int[] _numbers = [];

    /// <summary>
    /// The settlements of the last check (number and name), for the debug row.
    /// </summary>
    volatile IReadOnlyList<(int Number, string Name)> _towns = [];

    /// <summary>
    /// The player's settlements at the last check, in founding order.
    /// </summary>
    public IReadOnlyList<(int Number, string Name)> Towns => _towns;

    /// <summary>
    /// Whether the settlement numbered <paramref name="number"/> has a Beastmaster (read on the host's thread).
    /// </summary>
    public bool Has(int number) => _numbers.Contains(number);

    /// <summary>
    /// Adds a Beastmaster at a settlement that has none.
    /// </summary>
    public void Add(SettlementTraderCommand command)
    {
        if (_traders.Any(t => t.Number == command.Number)) return;

        _traders.Add((command.Number, command.Name, New(command.Number, command.Name)));
        _numbers = [.. _traders.Select(t => t.Number)];
        log($"husbandry: a Beastmaster is coming to {command.Name} (settlement {command.Number})");
    }

    /// <summary>
    /// One check: publishes the settlements and runs each Beastmaster.
    /// </summary>
    public void Run(World world, RngPurpose purpose)
    {
        _towns = [.. world.Buildings.Towns.Select(t => (t.Number, t.Name))];
        foreach (var (_, _, trader) in _traders) trader.Run(world, purpose);
    }

    /// <summary>
    /// The debug view of every one; empty when there are none.
    /// </summary>
    public string Describe(World world)
    {
        var text = new StringBuilder();
        foreach (var (_, _, trader) in _traders) text.Append(trader.Describe(world));
        return text.ToString();
    }

    /// <summary>
    /// A Beastmaster at a settlement.
    /// </summary>
    Beastmaster New(int number, string name) => new(population, sexes, log, OutpostPen.Post(number, name));

    /// <summary>
    /// Writes each one: its settlement's number and name, then its pen.
    /// </summary>
    public void Write(BinaryWriter w)
    {
        w.Write(_traders.Count);
        foreach (var (number, name, trader) in _traders)
        {
            w.Write(number);
            w.Write(name);
            trader.Write(w);
        }
    }

    /// <summary>
    /// Reads them back.
    /// </summary>
    public void Read(BinaryReader r)
    {
        _traders.Clear();
        int count = r.ReadInt32();
        for (int k = 0; k < count; k++)
        {
            int number = r.ReadInt32();
            string name = r.ReadString();
            var trader = New(number, name);
            trader.Read(r);
            _traders.Add((number, name, trader));
        }

        _numbers = [.. _traders.Select(t => t.Number)];
    }

    /// <summary>
    /// Adds each one to the state hash.
    /// </summary>
    public void Hash(ref StateHasher hasher)
    {
        hasher.Add(_traders.Count);
        foreach (var (number, _, trader) in _traders)
        {
            hasher.Add(number);
            trader.Hash(ref hasher);
        }
    }
}
