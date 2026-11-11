using System.Data.Common;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// The runtime TVF factory under live test: metadata only, so the descriptor is independent of the
/// argument value and the call expression keeps the argument (a captured value or a correlated outer
/// column). The function itself is created by <see cref="T320SqlServerTvfTests.Ddl"/>.
/// </summary>
internal static class T320SsIntegrationTvf
{
    public static TvfEntityBuilder<IT320ChildRow> Children(int parentId) => new("t320_children", "dbo");
}

/// <summary>
/// #320 (T320) live SQL Server evidence for a runtime TVF used as a correlated APPLY source (matrix row
/// MX-SS, AC04). An inline table-valued function is created by the fixture, so the custom-TVF rendering
/// path is exercised against a real server and returns 0, 1 and several rows for different parent keys —
/// correlation and row multiplication are observed as data, not as "no exception".
/// </summary>
[Trait("Category", "T320")]
[Collection("T320 tvf plan identity")]
public sealed class T320SqlServerTvfTests(ITestOutputHelper output) : ProviderTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    /// <summary>
    /// Exact fixture DDL. An inline table-valued function (not a built-in) is used on purpose: the
    /// descriptor supplies the name and schema while the result columns come from the mapped row type,
    /// so the custom-TVF path is the one under test. Key 1 has no children, key 2 has exactly one and
    /// key 3 has three.
    /// </summary>
    internal const string Ddl =
        """
        if object_id('dbo.t320_children', 'IF') is not null drop function dbo.t320_children;
        if object_id('dbo.t320_child', 'U') is not null drop table dbo.t320_child;
        if object_id('dbo.t320_parent', 'U') is not null drop table dbo.t320_parent;

        create table dbo.t320_parent (id int primary key, name nvarchar(50) not null);
        create table dbo.t320_child (child_id int primary key, parent_id int not null, label nvarchar(50) not null);

        insert into dbo.t320_parent (id, name) values (1, N'zero'), (2, N'one'), (3, N'three');
        insert into dbo.t320_child (child_id, parent_id, label)
        values (21, 2, N'c21'), (31, 3, N'c31'), (32, 3, N'c32'), (33, 3, N'c33');
        """;

    /// <summary>
    /// The function body is a separate batch because T-SQL requires <c>CREATE [OR ALTER] FUNCTION</c> to be
    /// the first statement of its batch.
    /// </summary>
    internal const string FunctionDdl =
        """
        create or alter function dbo.t320_children(@parent int)
        returns table
        as
        return (
            select c.child_id, c.parent_id, c.label
            from dbo.t320_child c
            where c.parent_id = @parent
        );
        """;

    private static readonly object FixtureGate = new();
    private static string? _serverVersion;

    private IDataContext Context => _sut.DataProvider;

    /// <summary>Creates the fixture once per process and returns the observed server version.</summary>
    private string EnsureFixture()
    {
        lock (FixtureGate)
        {
            if (_serverVersion is not null)
                return _serverVersion;

            var context = (DataContext)Context;
            context.EnsureConnectionOpen();
            using (var command = context.CreateCommand(Ddl))
                command.ExecuteNonQuery();
            using (var command = context.CreateCommand(FunctionDdl))
                command.ExecuteNonQuery();

            using var version = Context.ExecuteRaw("select @@version as v");
            _serverVersion = version.Read<string>().Single();

            output.WriteLine($"T320 fixture DDL executed; server version: {_serverVersion}");
            return _serverVersion;
        }
    }

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext context, QueryCommand<T> command, bool storeInCache)
        => (DbPreparedQueryCommand<T>)context.GetPreparedQueryCommand(command, false, storeInCache, CancellationToken.None);

    private static List<string> ParametersOf<T>(DbPreparedQueryCommand<T> prepared)
        => [.. prepared.DbCommandParams.Cast<DbParameter>().Select(p => $"{p.ParameterName}={p.Value ?? "null"}")];

    /// <summary>
    /// Correlated inner APPLY: the function receives the outer key, so the zero-match parent produces no
    /// row, the one-match parent exactly one and the several-match parent exactly three.
    /// </summary>
    [Fact]
    public void CrossApply_ShouldJoinPerOuterRow_WithZeroOneAndSeveralMatches()
    {
        var version = EnsureFixture();
        output.WriteLine($"sqlserver version: {version}");

        var command = Context.From<IT320Parent>()
            .CrossApply(row => T320SsIntegrationTvf.Children(row.Id))
            .Select(p => new { p.Item1.Id, p.Item1.Name, p.Item2.ChildId, p.Item2.Label });

        var sql = Normalize(Prepare(Context, command, storeInCache: false).DbCommand.CommandText);
        output.WriteLine($"cross apply sql: <<{sql}>>");

        sql.Should().Contain("cross apply ", "SQL Server renders a correlated APPLY natively");
        sql.Should().Contain("t320_children(t1.id)", "the correlated argument is the outer column reference, never an evaluated value");

        var rows = command.ToList();

        rows.Should().HaveCount(4, "1 parent x 1 row + 1 parent x 3 rows; the zero-match parent contributes nothing");
        rows.Select(r => r.Id).Distinct().Should().BeEquivalentTo(new[] { 2, 3 }, "the zero-match parent must not appear");

        var one = rows.Where(r => r.Id == 2).ToArray();
        one.Should().ContainSingle("parent 2 has exactly one child");
        one[0].Name.Should().Be("one");
        one[0].ChildId.Should().Be(21);
        one[0].Label.Should().Be("c21");

        var several = rows.Where(r => r.Id == 3).ToArray();
        several.Should().HaveCount(3, "parent 3 has three children");
        several.Should().OnlyContain(r => r.Name == "three");
        several.Select(r => r.ChildId).Should().BeEquivalentTo(new int?[] { 31, 32, 33 });
        several.Select(r => r.Label).Should().BeEquivalentTo(new[] { "c31", "c32", "c33" });
    }

    /// <summary>
    /// Correlated OUTER APPLY: the zero-match parent survives exactly once with NULL child columns, and
    /// the one/several multiplicities are unchanged.
    /// </summary>
    [Fact]
    public void OuterApply_ShouldPreserveTheZeroMatchParentAndKeepOneAndSeveralMatches()
    {
        var version = EnsureFixture();
        output.WriteLine($"sqlserver version: {version}");

        var command = Context.From<IT320Parent>()
            .OuterApply(row => T320SsIntegrationTvf.Children(row.Id))
            .Select(p => new { p.Item1.Id, p.Item1.Name, p.Item2.ChildId, p.Item2.Label });

        var sql = Normalize(Prepare(Context, command, storeInCache: false).DbCommand.CommandText);
        output.WriteLine($"outer apply sql: <<{sql}>>");

        sql.Should().Contain("outer apply ", "SQL Server renders a correlated OUTER APPLY natively");
        sql.Should().Contain("t320_children(t1.id)", "the correlated argument is the outer column reference");

        var rows = command.ToList();

        rows.Should().HaveCount(5, "2 matching rows contributions (1 + 3) plus the single preserved zero-match parent");
        rows.Select(r => r.Id).Distinct().Should().BeEquivalentTo(new[] { 1, 2, 3 });

        var zero = rows.Where(r => r.Id == 1).ToArray();
        zero.Should().ContainSingle("the zero-match parent survives exactly once, not once per missing row");
        zero[0].Name.Should().Be("zero");
        zero[0].ChildId.Should().BeNull("the absent applied row must materialize as NULL, not as a default value");
        zero[0].Label.Should().BeNull("the absent applied row must not fabricate a label");

        rows.Where(r => r.Id == 2).Select(r => r.ChildId).Should().BeEquivalentTo(new int?[] { 21 });
        rows.Where(r => r.Id == 3).Select(r => r.ChildId).Should().BeEquivalentTo(new int?[] { 31, 32, 33 });
    }

    /// <summary>
    /// Parameterised non-correlated source: <c>ctx.From(() =&gt; Tvf.Children(capturedId))</c> binds the
    /// captured value as a parameter. Changing the capture and executing again must use the new value on
    /// the same cached plan instance (cache-hit refresh) and therefore change the row set.
    /// </summary>
    [Fact]
    public void FromCapturedArgument_ShouldBindAParameterAndRefreshOnCacheHit()
    {
        var version = EnsureFixture();
        output.WriteLine($"sqlserver version: {version}");

        Context.PurgeQueryCache();

        var capturedId = 2;
        QueryCommand<int?> Build() => Context.From(() => T320SsIntegrationTvf.Children(capturedId)).Select(c => c.ChildId);

        var first = Prepare(Context, Build(), storeInCache: true);
        var firstSql = Normalize(first.DbCommand.CommandText);
        var firstParameters = ParametersOf(first);
        var firstObserved = $"rendered statement: <<{firstSql}>>; parameters: [{string.Join(", ", firstParameters)}]";
        output.WriteLine($"first run: {firstObserved}");

        firstParameters.Should().Equal(new[] { "capturedId=2" }, $"the capture is a bound parameter; {firstObserved}");
        firstSql.Should().Contain("t320_children(", firstObserved);
        firstSql.Should().NotContain("t320_children(2)", $"a captured value is never inlined; {firstObserved}");

        var firstRows = Build().ToList().OrderBy(x => x).ToArray();
        firstRows.Should().Equal(new int?[] { 21 }, $"the captured 2 selects parent 2's only child; {firstObserved}");

        capturedId = 3;

        var second = Prepare(Context, Build(), storeInCache: true);
        var secondSql = Normalize(second.DbCommand.CommandText);
        var secondParameters = ParametersOf(second);
        var secondObserved = $"rendered statement: <<{secondSql}>>; parameters: [{string.Join(", ", secondParameters)}]";
        output.WriteLine($"second run: {secondObserved}");

        ReferenceEquals(first, second).Should().BeTrue(
            $"the same shape must reuse the cached plan so the refresh path is exercised; {secondObserved}");
        secondSql.Should().Be(firstSql, $"a capture change must not re-render the statement; {secondObserved}");
        secondParameters.Should().Equal(
            new[] { "capturedId=3" },
            $"the cache hit must bind the new capture, never the stale 2; {secondObserved}");

        var secondRows = Build().ToList().OrderBy(x => x).ToArray();
        secondRows.Should().Equal(
            new int?[] { 31, 32, 33 },
            $"the refreshed capture selects parent 3's three children on the same context; {secondObserved}");
    }
}
