using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// The parent relation the correlated <c>JSON_TABLE</c> source reads per outer row. Key 1 carries an
/// empty document (no children), key 2 exactly one element and key 3 three elements (see
/// <see cref="T320MySqlTvfTests.Ddl"/>).
/// </summary>
[SqlTable("t320_my_parent")]
public interface IT320MyParent
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("name")]
    string Name { get; set; }

    /// <summary>The per-row JSON document the implicit-lateral <c>JSON_TABLE</c> call expands.</summary>
    [Column("children_json")]
    string ChildrenJson { get; set; }
}

/// <summary>
/// Row shape of the custom <c>JSON_TABLE</c> declaration. Every column is nullable so the OUTER APPLY
/// form can materialize the no-match parent without a fabricated value.
/// </summary>
public interface IT320MyChildRow
{
    [Column("child_id")]
    int? ChildId { get; set; }

    [Column("label")]
    string? Label { get; set; }
}

/// <summary>
/// The runtime TVF factory under live test. MySQL has no inline table-valued function, so the supported
/// custom-table-function form is a <c>JSON_TABLE</c> declaration: the name is the built-in function and
/// <see cref="TvfEntityBuilder{T}.CallClause"/> carries the verbatim JSON path and column definitions,
/// while the document stays in the caller's expression tree (a correlated column or a capture).
/// </summary>
internal static class T320MyIntegrationTvf
{
    public static TvfEntityBuilder<IT320MyChildRow> Children(string document) => new(
        "json_table",
        null,
        null,
        ", '$[*]' columns(child_id int path '$.child_id', label varchar(50) path '$.label')",
        Array.Empty<int>(),
        TableFunctionSchema.None);
}

/// <summary>
/// #320 (T320) live MySQL evidence for a runtime TVF used as a correlated APPLY source (matrix row
/// MX-MY, AC04). MySQL has no inline table-valued function, so the custom source is a <c>JSON_TABLE</c>
/// declaration, and it correlates an applied table-function source implicitly: the generated APPLY surface
/// is <c>join</c> / <c>left join ... on true</c> with no <c>LATERAL</c> keyword. Both shapes are exercised
/// against a real server and return the expected 4 and 5 rows, so correlation and row multiplication are
/// observed as data, not as "no exception".
/// </summary>
[Trait("Category", "T320")]
public sealed class T320MySqlTvfTests(ITestOutputHelper output) : ProviderTestSuite
{
    protected override ITestProvider Provider => MySqlTestProvider.Instance;

    /// <summary>
    /// Exact fixture DDL: one JSON document per parent. Key 1 has an empty array (no rows), key 2 has one
    /// element and key 3 has three, so the zero/one/several multiplicities are data-driven.
    /// </summary>
    internal const string Ddl =
        """
        drop table if exists t320_my_parent;

        create table t320_my_parent
        (
            id int not null primary key,
            name varchar(50) not null,
            children_json json not null
        );

        insert into t320_my_parent (id, name, children_json) values
            (1, 'zero', '[]'),
            (2, 'one', '[{"child_id":21,"label":"c21"}]'),
            (3, 'three', '[{"child_id":31,"label":"c31"},{"child_id":32,"label":"c32"},{"child_id":33,"label":"c33"}]');
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
    /// Correlated CROSS APPLY over a runtime <c>JSON_TABLE</c> source: MySQL correlates an applied
    /// table-function source implicitly, so the generated statement joins it without the <c>LATERAL</c>
    /// keyword and multiplies each outer row by its document elements. The zero-match parent
    /// contributes no row to an inner join.
    /// </summary>
    [Fact]
    public void CrossApply_ShouldJoinPerOuterRow_WithZeroOneAndSeveralMatches()
    {
        var version = EnsureFixture();
        output.WriteLine($"mysql version: {version}");

        var command = Context.From<IT320MyParent>()
            .CrossApply(row => T320MyIntegrationTvf.Children(row.ChildrenJson))
            .Select(p => new { p.Item1.Id, p.Item1.Name, p.Item2.ChildId, p.Item2.Label });

        var sql = Normalize(Prepare(Context, command, storeInCache: false).DbCommand.CommandText);
        output.WriteLine($"cross apply sql: <<{sql}>>");

        var rows = command.ToList();
        output.WriteLine($"cross apply rows: {rows.Count}");

        rows.Should().HaveCount(4, "1 parent x 1 element + 1 parent x 3 elements; the empty document contributes no row");
        rows.Select(r => r.Id).Distinct().Should().BeEquivalentTo(new[] { 2, 3 }, "the zero-match parent is excluded from an inner join");
        rows.Where(r => r.Id == 2).Select(r => r.ChildId).Should().BeEquivalentTo(new int?[] { 21 });
        rows.Where(r => r.Id == 3).Select(r => r.ChildId).Should().BeEquivalentTo(new int?[] { 31, 32, 33 });

        sql.Should().Contain(" join json_table(t1.children_json", "the correlated function is the join's FROM source: no derived wrapper, no LATERAL keyword");
        sql.Should().NotContain("lateral", "MySQL has no LATERAL keyword in this position");
        sql.Should().NotContain("(select", "the derived wrapper is removed, so no projected subquery remains");
    }

    /// <summary>
    /// Correlated OUTER APPLY: the empty-document parent survives exactly once with NULL expanded
    /// columns, and the one/several multiplicities are unchanged.
    /// </summary>
    [Fact]
    public void OuterApply_ShouldPreserveTheZeroMatchParentAndKeepOneAndSeveralMatches()
    {
        var version = EnsureFixture();
        output.WriteLine($"mysql version: {version}");

        var command = Context.From<IT320MyParent>()
            .OuterApply(row => T320MyIntegrationTvf.Children(row.ChildrenJson))
            .Select(p => new { p.Item1.Id, p.Item1.Name, p.Item2.ChildId, p.Item2.Label });

        var sql = Normalize(Prepare(Context, command, storeInCache: false).DbCommand.CommandText);
        output.WriteLine($"outer apply sql: <<{sql}>>");

        sql.Should().Contain(" left join json_table(t1.children_json", "the outer join applies the function itself, so an unmatched parent is null-extended without a derived wrapper");
        sql.Should().Contain(" on true", "the outer join carries the ANSI ON TRUE condition");
        sql.Should().NotContain("lateral", "MySQL has no LATERAL keyword in this position");
        sql.Should().NotContain("(select", "the derived wrapper is removed, so no projected subquery remains");

        var rows = command.ToList();

        rows.Should().HaveCount(5, "2 matching contributions (1 + 3) plus the single preserved zero-match parent");
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
    /// Parameterised non-correlated source: <c>ctx.From(() =&gt; Tvf.Children(capturedDocument))</c> binds
    /// the captured JSON document as a parameter. Changing the capture and executing again must use the
    /// new document on the same cached plan instance (cache-hit refresh) and therefore change the row set.
    /// </summary>
    [Fact]
    public void FromCapturedArgument_ShouldBindAParameterAndRefreshOnCacheHit()
    {
        var version = EnsureFixture();
        output.WriteLine($"mysql version: {version}");

        Context.PurgeQueryCache();

        var document = """[{"child_id":21,"label":"c21"}]""";
        QueryCommand<int?> Build() => Context.From(() => T320MyIntegrationTvf.Children(document)).Select(c => c.ChildId);

        var first = Prepare(Context, Build(), storeInCache: true);
        var firstSql = Normalize(first.DbCommand.CommandText);
        var firstParameters = ParametersOf(first);
        var firstObserved = $"rendered statement: <<{firstSql}>>; parameters: [{string.Join(", ", firstParameters)}]";
        output.WriteLine($"first run: {firstObserved}");

        firstParameters.Should().Equal(new[] { $"document={document}" }, $"the capture is a bound parameter; {firstObserved}");
        firstSql.Should().Contain("json_table(@document", firstObserved);
        firstSql.Should().NotContain(document, $"a captured document is never inlined; {firstObserved}");

        var firstRows = Build().ToList().OrderBy(x => x).ToArray();
        firstRows.Should().Equal(new int?[] { 21 }, $"the captured document expands to its only element; {firstObserved}");

        document = """[{"child_id":31,"label":"c31"},{"child_id":32,"label":"c32"},{"child_id":33,"label":"c33"}]""";

        var second = Prepare(Context, Build(), storeInCache: true);
        var secondSql = Normalize(second.DbCommand.CommandText);
        var secondParameters = ParametersOf(second);
        var secondObserved = $"rendered statement: <<{secondSql}>>; parameters: [{string.Join(", ", secondParameters)}]";
        output.WriteLine($"second run: {secondObserved}");

        ReferenceEquals(first, second).Should().BeTrue(
            $"the same shape must reuse the cached plan so the refresh path is exercised; {secondObserved}");
        secondSql.Should().Be(firstSql, $"a capture change must not re-render the statement; {secondObserved}");
        secondParameters.Should().Equal(
            new[] { $"document={document}" },
            $"the cache hit must bind the new capture, never the stale document; {secondObserved}");

        var secondRows = Build().ToList().OrderBy(x => x).ToArray();
        secondRows.Should().Equal(
            new int?[] { 31, 32, 33 },
            $"the refreshed capture expands to the three elements on the same context; {secondObserved}");
    }
}
