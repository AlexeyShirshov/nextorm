using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// The parent relation the correlated runtime-TVF APPLY reads per outer row. Key 1 has no children,
/// key 2 has exactly one and key 3 has three (see <see cref="T320RuntimeTvfPostgresTests.Ddl"/>).
/// </summary>
[SqlTable("t320_parent")]
public interface IT320Parent
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("name")]
    string Name { get; set; }
}

/// <summary>
/// Row shape of the custom PostgreSQL function <c>t320_children(p_parent_id int)</c>. Every column is
/// nullable so the OUTER APPLY form can materialize the no-match parent without a fabricated value.
/// </summary>
public interface IT320ChildRow
{
    [Column("child_id")]
    int? ChildId { get; set; }

    [Column("parent_id")]
    int? ParentId { get; set; }

    [Column("label")]
    string? Label { get; set; }
}

/// <summary>
/// The runtime TVF factory under test: metadata only, so the descriptor is independent of the argument
/// value and the call expression keeps the argument (a captured value or a correlated outer column).
/// </summary>
internal static class T320PgTvf
{
    public static TvfEntityBuilder<IT320ChildRow> Children(int parentId) => new("t320_children");
}

/// <summary>
/// #320 (T320) live PostgreSQL evidence for a runtime TVF used as a join/APPLY source. A custom
/// function — <c>CREATE FUNCTION ... RETURNS TABLE</c> — is created by the fixture so the custom-TVF
/// rendering path is exercised, and it returns 0, 1 and several rows for different parent keys, so
/// correlation and row multiplication are observed as data, not as "no exception".
/// </summary>
[Trait("Category", "T320")]
public sealed class T320RuntimeTvfPostgresTests(ITestOutputHelper output) : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    /// <summary>
    /// Exact fixture DDL. A function (not a built-in series) is used on purpose: the descriptor supplies
    /// the name and the result columns come from the mapped row type, so the custom-TVF path is the one
    /// under test. The <c>order by</c> inside the function keeps the multiplicity assertion stable.
    /// </summary>
    internal const string Ddl =
        """
        drop function if exists t320_children(int);
        drop table if exists t320_child;
        drop table if exists t320_parent;

        create table t320_parent (id int primary key, name text not null);
        create table t320_child (child_id int primary key, parent_id int not null, label text not null);

        insert into t320_parent (id, name) values (1, 'zero'), (2, 'one'), (3, 'three');
        insert into t320_child (child_id, parent_id, label)
        values (21, 2, 'c21'), (31, 3, 'c31'), (32, 3, 'c32'), (33, 3, 'c33');

        create function t320_children(p_parent_id int)
        returns table (child_id int, parent_id int, label text)
        language sql
        stable
        as $$
            select c.child_id, c.parent_id, c.label
            from t320_child c
            where c.parent_id = p_parent_id
            order by c.child_id
        $$;
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

            using var version = Context.ExecuteRaw("select version() as v");
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
    /// row, the one-match parent exactly one and the several-match parent exactly three. Both the values
    /// and the total multiplicity are asserted.
    /// </summary>
    [Fact]
    public void CrossApply_ShouldJoinPerOuterRow_WithZeroOneAndSeveralMatches()
    {
        var version = EnsureFixture();
        output.WriteLine($"postgres version: {version}");

        var command = Context.From<IT320Parent>()
            .CrossApply(row => T320PgTvf.Children(row.Id))
            .Select(p => new { p.Item1.Id, p.Item1.Name, p.Item2.ChildId, p.Item2.Label });

        var sql = Normalize(Prepare(Context, command, storeInCache: false).DbCommand.CommandText);
        output.WriteLine($"cross apply sql: <<{sql}>>");

        sql.Should().Contain("cross join lateral", "PostgreSQL renders the correlated APPLY as a lateral join");
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
        output.WriteLine($"postgres version: {version}");

        var command = Context.From<IT320Parent>()
            .OuterApply(row => T320PgTvf.Children(row.Id))
            .Select(p => new { p.Item1.Id, p.Item1.Name, p.Item2.ChildId, p.Item2.Label });

        var sql = Normalize(Prepare(Context, command, storeInCache: false).DbCommand.CommandText);
        output.WriteLine($"outer apply sql: <<{sql}>>");

        sql.Should().Contain("left join lateral", "PostgreSQL renders the correlated OUTER APPLY as a left lateral join");

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
    /// Parameterised non-correlated source: <c>ctx.From(() =&gt; Db.Children(capturedId))</c> binds the
    /// captured value as a parameter. Changing the capture and executing again on the same context must
    /// use the new value (cache-hit refresh) and therefore change the row set.
    /// </summary>
    [Fact]
    public void FromCapturedArgument_ShouldBindAParameterAndRefreshOnCacheHit()
    {
        var version = EnsureFixture();
        output.WriteLine($"postgres version: {version}");

        Context.PurgeQueryCache();

        var capturedId = 2;
        QueryCommand<int?> Build() => Context.From(() => T320PgTvf.Children(capturedId)).Select(c => c.ChildId);

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

    /// <summary>
    /// Fail-closed: a source with no factory-call provenance (<c>ctx.From(() =&gt; capturedDescriptor)</c>)
    /// is rejected with the named message while the real database is reachable, so it can never silently
    /// degrade into a table scan or a zero-argument call.
    /// </summary>
    [Fact]
    public void CapturedDescriptor_ShouldFailClosedWithTheNamedMessage()
    {
        var version = EnsureFixture();
        output.WriteLine($"postgres version: {version}");

        Context.From<IT320Parent>().Count().Should().Be(3, "the real database is reachable before the rejected call");

        var descriptor = T320PgTvf.Children(2);

        var act = () => Context.From(() => descriptor).ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*must be a direct call to a runtime table-valued-function factory*provenance*");
    }
}
