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

    // ---------------------------------------------------------------------------------------------
    // FTS3/FTS4/FTS5 (#181): every new SQLite-only FTS family is rejected by a provider that does
    // not opt in, including the FTS5 table-valued FROM source.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Issue", "181")]
    public void Fts_Match_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.Match("docs_fts", "foo") }));
        act.Should().Throw<NotSupportedException>().WithMessage("*Match*not supported*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5Bm25_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.FTS5bm25("docs_fts") }));
        act.Should().Throw<NotSupportedException>().WithMessage("*FTS5bm25*not supported*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5Highlight_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.Highlight("docs_fts", 0, "[", "]") }));
        act.Should().Throw<NotSupportedException>().WithMessage("*Highlight*not supported*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5Snippet_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.Snippet("docs_fts", 0, "[", "]", "...", 8) }));
        act.Should().Throw<NotSupportedException>().WithMessage("*Snippet*not supported*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5Rank_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.Rank("docs_fts") }));
        act.Should().Throw<NotSupportedException>().WithMessage("*Rank*not supported*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts3RankOverMatchInfo_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.Rank(SqlFunctions.Sqlite.FTS3MatchInfo("docs_fts")) }));
        act.Should().Throw<NotSupportedException>().WithMessage("*Rank*not supported*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts3RowId_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.RowId("docs_fts") }));
        act.Should().Throw<NotSupportedException>().WithMessage("*RowId*not supported*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts3Offsets_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.FTS3Offsets("docs_fts") }));
        act.Should().Throw<NotSupportedException>().WithMessage("*FTS3Offsets*not supported*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts3MatchInfo_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.FTS3MatchInfo("docs_fts", "pcx") }));
        act.Should().Throw<NotSupportedException>().WithMessage("*FTS3MatchInfo*not supported*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts3Snippet_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.FTS3Snippet("docs_fts") }));
        act.Should().Throw<NotSupportedException>().WithMessage("*FTS3Snippet*not supported*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5MatchTable_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => SqlOf(ctx, ctx
            .FromTableFunction(() => SqlFunctions.Sqlite.MatchTable<ISimpleEntity>("docs_fts", "foo"))
            .Select(r => r.Id));
        act.Should().Throw<NotSupportedException>();
    }

    // ---------------------------------------------------------------------------------------------
    // FTS5 maintenance/control (#195): the SQLite-only control-interface surface is rejected by a
    // provider without FTS5 from ToSql/Execute/ExecuteAsync, before any connection I/O.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Issue", "195")]
    public void Fts5Maintenance_ToSql_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.CreateSqliteFts5CommandBuilder("docs_fts").Optimize().ToSql();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("PostgresDialect does not support SQLite FTS5 maintenance commands.");
    }

    [Fact]
    [Trait("Issue", "195")]
    public void Fts5Maintenance_Execute_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.CreateSqliteFts5CommandBuilder("docs_fts").Rebuild().Execute();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("PostgresDialect does not support SQLite FTS5 maintenance commands.");
    }

    [Fact]
    [Trait("Issue", "195")]
    public async Task Fts5Maintenance_ExecuteAsync_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.CreateSqliteFts5CommandBuilder("docs_fts").IntegrityCheck().ExecuteAsync();

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("PostgresDialect does not support SQLite FTS5 maintenance commands.");
    }
}
