using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// T285b residual aggregate cells for SQLite: all four cells are deterministic refusals naming SQLite.
/// The shipped SQLite build has no median/percentile aggregate (<c>ENABLE_PERCENTILE=0</c>) and this
/// library does not emulate one, so no <c>median(...)</c>/<c>percentile...</c> text may ever reach the
/// command: translation throws before any SQL is appended. These tests never open a database connection.
/// </summary>
public class T285bResidualAggregateTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText);

    /// <summary>
    /// Invokes the SQL generation and returns the refusal. A returned SQL string (the success path) is a
    /// test failure by construction: every cell below must throw, so no percentile text can be produced.
    /// </summary>
    private static Exception Refusal<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        try
        {
            var sql = SqlOf(ctx, cmd);
            throw new InvalidOperationException($"Expected a refusal, but SQL was generated: {sql}");
        }
        catch (NotSupportedException ex)
        {
            return ex;
        }
    }

    [Fact]
    public void Median_ShouldBeRefusedWithTheSqliteNameAndProduceNoSql()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var refusal = Refusal(ctx, e.Select(x => new { M = SqlFunctions.Sqlite.median(x.Id) }));

        refusal.Should().BeOfType<NotSupportedException>()
            .Which.Message.Should().Be("The median function is not supported by SQLite.");
        refusal.Message.Should().NotContain("within group");
    }

    [Fact]
    public void GenericPercentile_ShouldBeRefusedWithTheSqliteNameAndProduceNoSql()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var refusal = Refusal(ctx, e.Select(x => new { P = SqlFunctions.Sqlite.percentile(0.5, x.Id) }));

        refusal.Should().BeOfType<NotSupportedException>()
            .Which.Message.Should().Be("The percentile function is not supported by SQLite.");
        refusal.Message.Should().NotContain("within group");
    }

    [Fact]
    public void ContinuousCell_ShouldBeRefusedWithTheSqliteName()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var refusal = Refusal(ctx, e.Select(x => new { C = SqlFunctions.Sql.percentile_cont(0.5, x.Id).Over() }));

        refusal.Should().BeOfType<NotSupportedException>()
            .Which.Message.Should().Be("The percentile_cont function is not supported by SQLite.");
    }

    [Fact]
    public void DiscreteCell_ShouldBeRefusedWithTheSqliteName()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var refusal = Refusal(ctx, e.Select(x => new { D = SqlFunctions.Sql.percentile_disc(0.5, x.Id).Over() }));

        refusal.Should().BeOfType<NotSupportedException>()
            .Which.Message.Should().Be("The percentile_disc function is not supported by SQLite.");
    }

    [Fact]
    public void AllFourCells_ShouldRefuseBeforeAnySqlIsAppended()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var cells = new Exception[]
        {
            Refusal(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.median(x.Id) })),
            Refusal(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.percentile(0.25, x.Id) })),
            Refusal(ctx, e.Select(x => new { V = SqlFunctions.Sql.percentile_cont(0.5, x.Id).Over() })),
            Refusal(ctx, e.Select(x => new { V = SqlFunctions.Sql.percentile_disc(0.5, x.Id).Over() }))
        };

        foreach (var refusal in cells)
        {
            refusal.Message.Should().MatchRegex("^The (median|percentile|percentile_cont|percentile_disc) function is not supported by SQLite\\.$");
        }
    }
}
