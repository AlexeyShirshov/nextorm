using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;

namespace nextorm.sqlite;

/// <summary>
/// Registers the custom SQLite aggregate functions (stdev, stdevp, var, varp) on a connection.
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

        connection.CreateAggregate<object?, VarianceAccumulator, double?>("stdev", default,
            static (acc, value) => VarianceAccumulator.Step(acc, value),
            static acc => acc.FinalVariance(population: false) is { } v ? Math.Sqrt(v) : null);

        connection.CreateAggregate<object?, VarianceAccumulator, double?>("stdevp", default,
            static (acc, value) => VarianceAccumulator.Step(acc, value),
            static acc => acc.FinalVariance(population: true) is { } v ? Math.Sqrt(v) : null);

        connection.CreateAggregate<object?, VarianceAccumulator, double?>("var", default,
            static (acc, value) => VarianceAccumulator.Step(acc, value),
            static acc => acc.FinalVariance(population: false));

        connection.CreateAggregate<object?, VarianceAccumulator, double?>("varp", default,
            static (acc, value) => VarianceAccumulator.Step(acc, value),
            static acc => acc.FinalVariance(population: true));
    }

    private struct VarianceAccumulator
    {
        public double Count;
        public double Sum;
        public double SumSq;

        public static VarianceAccumulator Step(VarianceAccumulator acc, object? value)
        {
            if (value is null or DBNull) return acc;

            var v = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return new VarianceAccumulator
            {
                Count = acc.Count + 1,
                Sum = acc.Sum + v,
                SumSq = acc.SumSq + v * v
            };
        }

        /// <summary>
        /// Sample (n-1) or population (n) variance. The standard deviation is its square root, so
        /// stdev/stdevp and var/varp share one accumulator. Returns <c>null</c> when there are too
        /// few non-null rows for the requested flavour.
        /// </summary>
        public readonly double? FinalVariance(bool population)
        {
            if (Count < (population ? 1 : 2)) return null;

            return (SumSq - Sum * Sum / Count) / (population ? Count : Count - 1);
        }
    }
}
