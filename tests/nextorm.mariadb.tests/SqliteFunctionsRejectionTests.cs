using FluentAssertions;
using NextORM.Core;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// The SQLite-only full-text surface (#181, <see cref="SqlFunctions.Sqlite"/>) must be rejected by
/// MariaDB across the whole FTS family (not only one representative) with a clear message instead
/// of emitting SQL the provider cannot execute.
/// </summary>
public class SqliteFunctionsRejectionTests
{
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    [Fact]
    [Trait("Issue", "181")]
    public void Fts_MatchTableAndColumn_ShouldThrow()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var table = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.Match("docs_fts", "foo") }));
        var column = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.Match(x.String, "foo") }));

        table.Should().Throw<NotSupportedException>().WithMessage("*Match*not supported*");
        column.Should().Throw<NotSupportedException>().WithMessage("*Match*not supported*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5Bm25_DefaultAndWeighted_ShouldThrow()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var d = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.FTS5bm25("docs_fts") }));
        var w = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.FTS5bm25("docs_fts", 1.0, 2.0) }));

        d.Should().Throw<NotSupportedException>().WithMessage("*FTS5bm25*not supported*");
        w.Should().Throw<NotSupportedException>().WithMessage("*FTS5bm25*not supported*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5HighlightAndSnippet_ShouldThrow()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var h = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.Highlight("docs_fts", 0, "[", "]") }));
        var s = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.Snippet("docs_fts", 0, "[", "]", "...", 8) }));

        h.Should().Throw<NotSupportedException>().WithMessage("*Highlight*not supported*");
        s.Should().Throw<NotSupportedException>().WithMessage("*Snippet*not supported*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5Rank_ShouldThrow()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.Rank("docs_fts") }));
        act.Should().Throw<NotSupportedException>().WithMessage("*Rank*not supported*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts3RankOverMatchInfo_ShouldThrow()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.Rank(SqlFunctions.Sqlite.FTS3MatchInfo("docs_fts")) }));
        act.Should().Throw<NotSupportedException>().WithMessage("*Rank*not supported*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts3RowIdOffsetsMatchInfoAndSnippet_ShouldThrow()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var rowId = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.RowId("docs_fts") }));
        var offsets = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.FTS3Offsets("docs_fts") }));
        var matchInfo = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.FTS3MatchInfo("docs_fts", "pcx") }));
        var snippet = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.FTS3Snippet("docs_fts", "[", "]", "...", 0, 8) }));

        rowId.Should().Throw<NotSupportedException>().WithMessage("*RowId*not supported*");
        offsets.Should().Throw<NotSupportedException>().WithMessage("*FTS3Offsets*not supported*");
        matchInfo.Should().Throw<NotSupportedException>().WithMessage("*FTS3MatchInfo*not supported*");
        snippet.Should().Throw<NotSupportedException>().WithMessage("*FTS3Snippet*not supported*");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5MatchTableFromSource_ShouldThrow()
    {
        using var ctx = MariaDbTestContext.Create();

        var act = () => SqlOf(ctx, ctx
            .FromTableFunction(() => SqlFunctions.Sqlite.MatchTable<ISimpleEntity>("docs_fts", "foo"))
            .Select(r => r.Id));
        act.Should().Throw<NotSupportedException>();
    }
}
