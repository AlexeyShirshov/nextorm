using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;

namespace NextORM.Integration.Tests;

public sealed class PostgresTvfShortcutTests
{
    [Fact]
    public void Unnest_BindsRuntimeArrayAndComposes()
    {
        using var context = new PostgresDataContext(PostgresContainer.ConnectionString, new DataContextBuilder());
        long[] values = [1, 2, 3];

        var named = context.FromUnnest(() => SqlFunctions.Parameter<long[]>(0))
            .Where(r => r.Value > 1).OrderBy(r => r.Value).Select(r => r.Value);
        var original = context.From(() => SqlFunctions.Postgres.unnest(SqlFunctions.Parameter<long[]>(0)))
            .Where(r => r.Value > 1).OrderBy(r => r.Value).Select(r => r.Value);

        var expected = original.ToList(values);
        expected.Should().Equal(2L, 3L);
        named.ToList(values).Should().Equal(expected);
    }

    [Fact]
    public void GenerateSeries_BindsCapturedValuesInOrder()
    {
        using var context = new PostgresDataContext(PostgresContainer.ConnectionString, new DataContextBuilder());
        long start = 2, stop = 8, step = 3;

        var named = context.FromGenerateSeries(() => start, () => stop, () => step)
            .OrderBy(r => r.Value).Select(r => r.Value);
        var original = context.From(() => SqlFunctions.Postgres.generate_series(start, stop, step))
            .OrderBy(r => r.Value).Select(r => r.Value);

        var expected = original.ToList();
        expected.Should().Equal(2L, 5L, 8L);
        named.ToList().Should().Equal(expected);
    }
}
