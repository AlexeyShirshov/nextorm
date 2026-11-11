using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// T308: pins the trailing clause order of the ClickHouse generator — <c>ORDER BY</c> →
/// <c>LIMIT ... BY</c> → <c>LIMIT/OFFSET</c> → <c>SETTINGS</c> → <c>FOR UPDATE</c>. ClickHouse rejects
/// <c>... SETTINGS k = v LIMIT n</c> (server Code 62), so a single-row terminal
/// (<c>First</c>/<c>Single</c>, which sets <c>Paging.Limit</c>) must place the settings clause after the
/// page. These tests never open a database connection: they assert the prepared SQL string only.
/// </summary>
public class T308SettingsSqlTests
{
    private const string SettingsClause = "settings max_threads = 2";

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        return (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    /// <summary>Asserts the settings clause is emitted and sits after <paramref name="clause"/>.</summary>
    private static void AssertSettingsAfter(string sql, string clause)
    {
        var settingsIndex = sql.IndexOf(SettingsClause, StringComparison.Ordinal);
        settingsIndex.Should().BeGreaterThan(-1, "the SETTINGS clause must be emitted; sql was:\n{0}", sql);

        var clauseIndex = sql.IndexOf(clause, StringComparison.Ordinal);
        clauseIndex.Should().BeGreaterThan(-1, "the '{0}' clause must be emitted; sql was:\n{1}", clause, sql);

        settingsIndex.Should().BeGreaterThan(clauseIndex,
            "SETTINGS must follow '{0}'; sql was:\n{1}", clause, sql);
    }

    /// <summary>Aggregate projection + settings + the <c>First()</c> paging shape (<c>Paging.Limit = 1</c>).</summary>
    [Fact]
    public void AggregateFirst()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<IComplexEntity>()
            .Settings(("max_threads", "2"))
            .Select(x => SqlFunctions.Sql.count())
            .Limit(1));

        AssertSettingsAfter(sql, "limit 1");
        sql.Should().Contain("count(*)");
    }

    /// <summary>Plain entity + settings + <c>First()</c> paging (<c>Paging.Limit = 1</c>).</summary>
    [Fact]
    public void First()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<ISimpleEntity>()
            .Settings(("max_threads", "2"))
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .Limit(1));

        AssertSettingsAfter(sql, "limit 1");
        sql.Should().Contain("order by id");
    }

    /// <summary>Filtered + ordered query + settings + <c>FirstOrDefault()</c> paging (<c>Paging.Limit = 1</c>).</summary>
    [Fact]
    public void FirstOrDefault()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<IComplexEntity>()
            .Where(x => x.Id > 0)
            .Settings(("max_threads", "2"))
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .Limit(1));

        AssertSettingsAfter(sql, "limit 1");
        sql.Should().Contain("where (id > 0)");
    }

    /// <summary>Aggregate + settings + the <c>Single()</c> paging shape (<c>Paging.Limit = 2</c>).</summary>
    [Fact]
    public void Single()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<IComplexEntity>()
            .Settings(("max_threads", "2"))
            .Select(x => SqlFunctions.Sql.count())
            .Limit(2));

        AssertSettingsAfter(sql, "limit 2");
    }

    /// <summary>Filtered query + settings + <c>SingleOrDefault()</c> paging (<c>Paging.Limit = 2</c>).</summary>
    [Fact]
    public void SingleOrDefault()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<IComplexEntity>()
            .Settings(("max_threads", "2"))
            .Where(x => x.Id == 1)
            .Select(x => x.Id)
            .Limit(2));

        AssertSettingsAfter(sql, "limit 2");
        sql.Should().Contain("where id = 1");
    }

    /// <summary>An explicit page (<c>Take</c>-style <c>Limit(2)</c>) keeps SETTINGS after the limit.</summary>
    [Fact]
    public void Take()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<IComplexEntity>()
            .Settings(("max_threads", "2"))
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .Limit(2));

        AssertSettingsAfter(sql, "limit 2");
    }

    /// <summary><c>Skip(5).Take(2)</c> renders <c>limit 2 offset 5</c>; SETTINGS must follow the whole page.</summary>
    [Fact]
    public void SkipTake()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<IComplexEntity>()
            .Settings(("max_threads", "2"))
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .Offset(5)
            .Limit(2));

        AssertSettingsAfter(sql, "limit 2 offset 5");
    }

    /// <summary>An offset-only page (ClickHouse renders the max-unsigned limit) still precedes SETTINGS.</summary>
    [Fact]
    public void OffsetOnly()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<IComplexEntity>()
            .Settings(("max_threads", "2"))
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .Offset(5));

        AssertSettingsAfter(sql, "limit 18446744073709551615 offset 5");
    }

    /// <summary><c>LIMIT BY</c> plus a single-row terminal: ORDER BY → LIMIT BY → LIMIT → SETTINGS.</summary>
    [Fact]
    public void LimitByFirst()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<IComplexEntity>()
            .Settings(("max_threads", "2"))
            .OrderBy(x => x.Id)
            .LimitBy(1, x => x.Int)
            .Select(x => x.Id)
            .Limit(1));

        var orderBy = sql.IndexOf("order by id", StringComparison.Ordinal);
        var limitBy = sql.IndexOf("limit 1 by nullableint", StringComparison.Ordinal);
        var page = limitBy < 0 ? -1 : sql.IndexOf("limit 1", limitBy + 1, StringComparison.Ordinal);
        var settings = sql.IndexOf(SettingsClause, StringComparison.Ordinal);

        orderBy.Should().BeGreaterThan(-1, "ORDER BY must be emitted; sql was:\n{0}", sql);
        limitBy.Should().BeGreaterThan(-1, "LIMIT BY must be emitted; sql was:\n{0}", sql);
        page.Should().BeGreaterThan(-1, "the plain page must follow LIMIT BY; sql was:\n{0}", sql);
        settings.Should().BeGreaterThan(-1, "SETTINGS must be emitted; sql was:\n{0}", sql);

        orderBy.Should().BeLessThan(limitBy, "LIMIT BY follows ORDER BY; sql was:\n{0}", sql);
        limitBy.Should().BeLessThan(page, "the page follows LIMIT BY; sql was:\n{0}", sql);
        page.Should().BeLessThan(settings, "SETTINGS follows the page; sql was:\n{0}", sql);
    }

    /// <summary>A correlated scalar subquery plus a single-row terminal keeps SETTINGS at the outer tail.</summary>
    [Fact]
    public void CorrelatedFirst()
    {
        using var ctx = ClickHouseTestContext.Create();
        var inner = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, ctx.From<IComplexEntity>()
            .Settings(("max_threads", "2"))
            .Select(it => new
            {
                it.Id,
                sid = inner.Where(s => s.Id == it.Id).Select(s => s.Id).First()
            })
            .Limit(1));

        var outerLimit = sql.LastIndexOf("limit 1", StringComparison.Ordinal);
        outerLimit.Should().BeGreaterThan(-1, "the single-row terminal must emit a limit; sql was:\n{0}", sql);
        AssertSettingsAfter(sql, "limit 1");
        sql.IndexOf(SettingsClause, StringComparison.Ordinal)
            .Should().BeGreaterThan(outerLimit, "SETTINGS follows the outermost page; sql was:\n{0}", sql);
        sql.Should().Contain("= t1.id");
    }

    /// <summary>A set operation whose operand carries settings keeps the settings clause at the end.</summary>
    [Fact]
    public void Union()
    {
        using var ctx = ClickHouseTestContext.Create();

        var low = ctx.From<IComplexEntity>()
            .Settings(("max_threads", "2"))
            .Where(x => x.Id <= 2)
            .Select(x => x.Id);
        var high = ctx.From<IComplexEntity>()
            .Where(x => x.Id == 3)
            .Select(x => x.Id);

        var sql = SqlOf(ctx, low.Union(high));

        AssertSettingsAfter(sql, " union ");
        sql.TrimEnd().Should().EndWith(SettingsClause, "SETTINGS is the trailing clause of the union; sql was:\n{0}", sql);
    }

    /// <summary>Without paging the settings clause simply trails the query.</summary>
    [Fact]
    public void NoPaging()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<IComplexEntity>()
            .Settings(("max_threads", "2"))
            .OrderBy(x => x.Id)
            .Select(x => x.Id));

        sql.TrimEnd().Should().EndWith(SettingsClause, "no page means SETTINGS stays last; sql was:\n{0}", sql);
        sql.Should().Contain("order by id");
    }

    /// <summary>Uppercase keywords keep the same order: LIMIT before SETTINGS.</summary>
    [Fact]
    public void KeywordCase()
    {
        using var ctx = ClickHouseTestContext.CreateUppercase();

        var sql = SqlOf(ctx, ctx.From<IComplexEntity>()
            .Settings(("max_threads", "2"))
            .Select(x => x.Id)
            .Limit(1));

        var settingsIndex = sql.IndexOf(" SETTINGS max_threads = 2", StringComparison.Ordinal);
        settingsIndex.Should().BeGreaterThan(-1, "the uppercase SETTINGS clause must be emitted; sql was:\n{0}", sql);

        var limitIndex = sql.IndexOf("LIMIT 1", StringComparison.Ordinal);
        limitIndex.Should().BeGreaterThan(-1, "the uppercase LIMIT must be emitted; sql was:\n{0}", sql);
        settingsIndex.Should().BeGreaterThan(limitIndex, "SETTINGS must follow LIMIT; sql was:\n{0}", sql);
        sql.Should().NotContain(" settings ");
    }

    /// <summary>An empty settings list emits no <c>SETTINGS</c> clause at all.</summary>
    [Fact]
    public void EmptySettings()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<IComplexEntity>()
            .Settings()
            .Select(x => x.Id)
            .Limit(1));

        sql.Should().NotContain("settings", "an empty settings list renders no clause; sql was:\n{0}", sql);
        sql.TrimEnd().Should().EndWith("limit 1", "the page is then the trailing clause; sql was:\n{0}", sql);
    }
}
