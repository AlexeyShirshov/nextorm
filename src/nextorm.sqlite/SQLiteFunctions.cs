using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;

namespace nextorm.sqlite;

/// <summary>
/// Registers the custom SQLite aggregate functions (stdev/stdevp) on a connection.
/// Microsoft.Data.Sqlite has no attribute-based auto-registration (unlike System.Data.SQLite),
/// so functions are attached explicitly and only once per connection.
/// </summary>
internal static class SQLiteFunctions
{
    private static readonly ConditionalWeakTable<SqliteConnection, object> _registered = new();

    public static void Register(SqliteConnection connection)
    {
        if (_registered.TryGetValue(connection, out _)) return;
        _registered.Add(connection, new object());

        connection.CreateAggregate<object?, StdevAccumulator, double?>("stdev", default,
            static (acc, value) => StdevAccumulator.Step(acc, value),
            static acc => acc.Final(population: false));

        connection.CreateAggregate<object?, StdevAccumulator, double?>("stdevp", default,
            static (acc, value) => StdevAccumulator.Step(acc, value),
            static acc => acc.Final(population: true));
    }

    private struct StdevAccumulator
    {
        public double Count;
        public double Sum;
        public double SumSq;

        public static StdevAccumulator Step(StdevAccumulator acc, object? value)
        {
            if (value is null or DBNull) return acc;

            var v = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return new StdevAccumulator
            {
                Count = acc.Count + 1,
                Sum = acc.Sum + v,
                SumSq = acc.SumSq + v * v
            };
        }

        public readonly double? Final(bool population)
        {
            if (Count < (population ? 1 : 2)) return null;

            var variance = (SumSq - Sum * Sum / Count) / (population ? Count : Count - 1);
            return Math.Sqrt(variance);
        }
    }
}
