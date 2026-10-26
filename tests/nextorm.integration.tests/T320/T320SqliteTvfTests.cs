using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Row shape of SQLite's bundled <c>json_each</c>: for a JSON array, <c>key</c> is the zero-based index
/// and <c>value</c> the element.
/// </summary>
public interface IT320SqJsonEachRow
{
    [Column("key")]
    long Key { get; set; }

    [Column("value")]
    long Value { get; set; }
}

/// <summary>
/// The runtime TVF factory under live SQLite test: <c>json_each</c> is a built-in table-valued function
/// (JSON1 is compiled into the bundled SQLite), so it executes in-process with no database container.
/// </summary>
internal static class T320SqIntegrationTvf
{
    public static TvfEntityBuilder<IT320SqJsonEachRow> JsonEach(string json) => new("json_each");
}

/// <summary>
/// #320 (T320) live SQLite evidence for a runtime TVF source (matrix row MX-SQ, AC04/AC06/AC09). The
/// function is executed against a real file-based database: rows materialise, an ordinary join over the
/// function returns the intersecting rows, the correlated APPLY is rejected by name (SQLite has no
/// APPLY/LATERAL), and a capture change on the same cached plan instance changes the row set.
/// </summary>
[Trait("Category", "T320")]
public sealed class T320SqliteTvfTests(ITestOutputHelper output) : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext context, QueryCommand<T> command, bool storeInCache)
        => (DbPreparedQueryCommand<T>)context.GetPreparedQueryCommand(command, false, storeInCache, CancellationToken.None);

    private static List<string> ParametersOf<T>(DbPreparedQueryCommand<T> prepared)
        => [.. prepared.DbCommandParams.Cast<DbParameter>().Select(p => $"{p.ParameterName}={p.Value ?? "null"}")];

    /// <summary>
    /// A literal-argument runtime TVF executes on the file-based database and materialises every row the
    /// function returns.
    /// </summary>
    [Fact]
    public void RuntimeTvf_WithLiteralArgument_ShouldExecuteAndMaterialiseRows()
    {
        output.WriteLine($"sqlite database: {SqliteTestProvider.DatabasePath}");

        var command = Context.From(() => T320SqIntegrationTvf.JsonEach("[1,2,3]"))
            .Select(r => r.Value);

        var sql = Normalize(Prepare(Context, command, storeInCache: false).DbCommand.CommandText);
        output.WriteLine($"json_each sql: <<{sql}>>");

        sql.Should().Contain("json_each(", "the runtime descriptor renders the bundled table-valued function");

        var rows = command.ToList().OrderBy(v => v).ToArray();

        rows.Should().Equal(new long[] { 1, 2, 3 }, "json_each('[1,2,3]') yields three rows with their element values");
    }

    /// <summary>
    /// An ordinary (non-APPLY) join over the TVF source is supported and returns the intersecting rows.
    /// </summary>
    [Fact]
    public void OrdinaryJoinOverTvf_ShouldReturnTheIntersectingRows()
    {
        output.WriteLine($"sqlite database: {SqliteTestProvider.DatabasePath}");

        var command = Context.From(() => T320SqIntegrationTvf.JsonEach("[1,2,3]"))
            .Join(Context.From<ISimpleEntity>(), (tvf, entity) => tvf.Value == entity.Id)
            .Select(p => new { Number = p.Item1.Value, Id = p.Item2.Id })
            .OrderBy(x => x.Number);

        var sql = Normalize(Prepare(Context, command, storeInCache: false).DbCommand.CommandText);
        output.WriteLine($"join sql: <<{sql}>>");

        sql.Should().Contain("join simple_entity", "the ordinary table is the joined source");

        var rows = command.ToList();

        rows.Should().HaveCount(3, "json_each('[1,2,3]') intersects the seeded ids 1..3");
        rows.Select(r => r.Number).Should().Equal(new long[] { 1, 2, 3 }, "the joined rows carry the TVF column");
        rows.Select(r => r.Id).Should().Equal(new[] { 1, 2, 3 }, "the ordinary side carries the table column");
    }

    /// <summary>
    /// SQLite has no APPLY/LATERAL source: the correlated forms are rejected by name while the real
    /// database is reachable, never silently rendered as an ordinary join.
    /// </summary>
    [Fact]
    public void CorrelatedApply_ShouldBeRejectedByTheDialect()
    {
        output.WriteLine($"sqlite database: {SqliteTestProvider.DatabasePath}");

        Context.From<IComplexEntity>().Count().Should().Be(3, "the real database is reachable before the rejected calls");

        Action cross = () => Prepare(
            Context,
            Context.From<IComplexEntity>()
                .CrossApply(row => T320SqIntegrationTvf.JsonEach(row.RequiredString))
                .Select(p => p.Item1.Id),
            storeInCache: false);
        cross.Should().Throw<NotSupportedException>()
            .WithMessage("*CrossApply join is not supported by this SQL dialect*");

        Action outer = () => Prepare(
            Context,
            Context.From<IComplexEntity>()
                .OuterApply(row => T320SqIntegrationTvf.JsonEach(row.RequiredString))
                .Select(p => p.Item1.Id),
            storeInCache: false);
        outer.Should().Throw<NotSupportedException>()
            .WithMessage("*OuterApply join is not supported by this SQL dialect*");
    }

    /// <summary>
    /// A captured argument is a bound parameter: changing it and executing again reuses the same cached
    /// plan instance, refreshes the parameter and therefore changes the row set.
    /// </summary>
    [Fact]
    public void CapturedArgument_ShouldRefreshOnCacheHitAndChangeTheRowSet()
    {
        output.WriteLine($"sqlite database: {SqliteTestProvider.DatabasePath}");

        Context.PurgeQueryCache();

        var capturedJson = "[1,2]";
        QueryCommand<long> Build() => Context.From(() => T320SqIntegrationTvf.JsonEach(capturedJson)).Select(r => r.Value);

        var first = Prepare(Context, Build(), storeInCache: true);
        var firstSql = Normalize(first.DbCommand.CommandText);
        var firstObserved = $"rendered statement: <<{firstSql}>>; parameters: [{string.Join(", ", ParametersOf(first))}]";
        output.WriteLine($"first run: {firstObserved}");

        ParametersOf(first).Should().Equal(new[] { "capturedJson=[1,2]" }, $"the capture is a bound parameter; {firstObserved}");
        firstSql.Should().Contain("json_each(", firstObserved);
        firstSql.Should().NotContain("[1,2]", $"a captured value is never inlined; {firstObserved}");

        var firstRows = Build().ToList().OrderBy(x => x).ToArray();
        firstRows.Should().Equal(new long[] { 1, 2 }, $"the captured document has two elements; {firstObserved}");

        capturedJson = "[3,4,5]";

        var second = Prepare(Context, Build(), storeInCache: true);
        var secondSql = Normalize(second.DbCommand.CommandText);
        var secondObserved = $"rendered statement: <<{secondSql}>>; parameters: [{string.Join(", ", ParametersOf(second))}]";
        output.WriteLine($"second run: {secondObserved}");

        ReferenceEquals(first, second).Should().BeTrue(
            $"the same shape must reuse the cached plan so the refresh path is exercised; {secondObserved}");
        secondSql.Should().Be(firstSql, $"a capture change must not re-render the statement; {secondObserved}");
        ParametersOf(second).Should().Equal(
            new[] { "capturedJson=[3,4,5]" },
            $"the cache hit must bind the new capture, never the stale document; {secondObserved}");

        var secondRows = Build().ToList().OrderBy(x => x).ToArray();
        secondRows.Should().Equal(
            new long[] { 3, 4, 5 },
            $"the refreshed capture selects three elements on the same context; {secondObserved}");
    }
}
