using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Postgres.Tests;

public sealed class PostgresTvfShortcutSqlTests
{
    private static string Sql<T>(IDataContext context, QueryCommand<T> query)
        => ((DbPreparedQueryCommand<T>)context.GetPreparedQueryCommand(query, false, false, CancellationToken.None))
            .DbCommand.CommandText;

    [Fact]
    public void Unnest_PreservesParameterMarkerAndComposition()
    {
        using var context = PostgresTestContext.Create();
        var named = context.FromUnnest(() => SqlFunctions.Parameter<long[]>(0))
            .Where(r => r.Value > 1).Select(r => r.Value);
        var original = context.From(() => SqlFunctions.Postgres.unnest(SqlFunctions.Parameter<long[]>(0)))
            .Where(r => r.Value > 1).Select(r => r.Value);

        Sql(context, named).Should().Be(Sql(context, original)).And.Contain("unnest(@norm_p0)");
    }

    [Fact]
    public void GenerateSeries_PreservesCapturedArgumentsInSignatureOrder()
    {
        using var context = PostgresTestContext.Create();
        long start = 2, stop = 8, step = 3;
        var named = context.FromGenerateSeries(() => start, () => stop, () => step).Select(r => r.Value);
        var original = context.From(() => SqlFunctions.Postgres.generate_series(start, stop, step)).Select(r => r.Value);

        Sql(context, named).Should().Be(Sql(context, original)).And.Contain("generate_series(@start, @stop, @step)");
    }

    [Fact]
    public void Unnest_UsesExistingUnsupportedProviderError()
    {
        using var context = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        long[] values = [1, 2];

        var original = FluentActions.Invoking(() => Sql(context,
            context.From(() => SqlFunctions.Postgres.unnest(values)).Select(r => r.Value)))
            .Should().Throw<NotSupportedException>().WithMessage("*unnest*");
        FluentActions.Invoking(() => Sql(context, context.FromUnnest(() => values).Select(r => r.Value)))
            .Should().Throw<NotSupportedException>().WithMessage(original.Which.Message);
    }
}
