using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// The SQLite-only surface (<see cref="SqlFunctions.Sqlite"/>) must be rejected by a provider that
/// does not opt in, with a clear message instead of emitting SQL it cannot execute.
/// </summary>
public class SqliteFunctionsRejectionTests
{
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    [Fact]
    public void CoreScalar_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.@typeof(x.String) }));
        act.Should().Throw<NotSupportedException>().WithMessage("*typeof*not supported*");
    }

    [Fact]
    public void JsonScalar_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.json_extract<int>(x.String, "$.a") }));
        act.Should().Throw<NotSupportedException>().WithMessage("*json_extract*not supported*");
    }

    [Fact]
    public void JsonOperator_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.json_get(x.String, "$.a") }));
        act.Should().Throw<NotSupportedException>().WithMessage("*json_get*not supported*");
    }

    [Fact]
    public void MathFunction_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.acos(0.5) }));
        act.Should().Throw<NotSupportedException>().WithMessage("*acos*not supported*");
    }

    [Fact]
    public void DateFunction_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.timediff(x.Datetime, x.Datetime) }));
        act.Should().Throw<NotSupportedException>().WithMessage("*timediff*not supported*");
    }
}
