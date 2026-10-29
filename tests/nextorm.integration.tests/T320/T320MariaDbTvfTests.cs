using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// The parent relation the correlated <c>JSON_TABLE</c> source reads per outer row. Key 1 carries an
/// empty document (no children), key 2 exactly one element and key 3 three elements (see
/// <see cref="T320MariaDbTvfTests.Ddl"/>).
/// </summary>
[SqlTable("t320_ma_parent")]
public interface IT320MaParent
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("name")]
    string Name { get; set; }

    /// <summary>The per-row JSON document the correlated <c>JSON_TABLE</c> call expands.</summary>
    [Column("children_json")]
    string ChildrenJson { get; set; }
}

/// <summary>
/// Row shape of the custom <c>JSON_TABLE</c> declaration. Every column is nullable so the LEFT JOIN
/// form can materialize the no-match parent without a fabricated value.
/// </summary>
public interface IT320MaChildRow
{
    [Column("child_id")]
    int? ChildId { get; set; }

    [Column("label")]
    string? Label { get; set; }
}

/// <summary>
/// The runtime TVF factory under live test. MariaDB has no inline table-valued function, so the
/// supported custom-table-function form is a <c>JSON_TABLE</c> declaration: the name is the built-in
/// function and <see cref="TvfEntityBuilder{T}.CallClause"/> carries the verbatim JSON path and column
/// definitions, while the document stays in the caller's expression tree (a correlated column or a
/// capture).
/// </summary>
internal static class T320MaIntegrationTvf
{
    public static TvfEntityBuilder<IT320MaChildRow> Children(string document) => new(
        "json_table",
        null,
        null,
        ", '$[*]' columns(child_id int path '$.child_id', label varchar(50) path '$.label')",
        Array.Empty<int>(),
        TableFunctionSchema.None);
}

/// <summary>
/// #320 (T320) live MariaDB evidence for a runtime TVF used as a correlated source (matrix row MX-MA,
/// AC04). This class runs against its own <c>mariadb:11.4</c> Testcontainers server (or
/// <c>NEXTORM_MARIADB_CONNECTION</c>) — never the MySQL container — and records the observed server
/// version and image. MariaDB's grammar has no <c>LATERAL</c> keyword: an applied table-function source
/// is correlated implicitly, so the generated APPLY surface is <c>join</c> / <c>left join ... on true</c>
/// and both shapes are exercised live, expanding the correlated documents to the expected 4 and 5 rows.
/// </summary>
[Trait("Category", "T320")]
[Collection("T320 tvf plan identity")]
public sealed class T320MariaDbTvfTests : IDisposable
{
    /// <summary>The declared image, mirroring <c>MariaDbContainer</c>'s default.</summary>
    internal const string ContainerImage = "mariadb:11.4";

    /// <summary>
    /// Exact fixture DDL: one JSON document per parent. Key 1 has an empty array (no rows), key 2 has one
    /// element and key 3 has three, so the zero/one/several multiplicities are data-driven.
    /// </summary>
    internal const string Ddl =
        """
        drop table if exists t320_ma_parent;

        create table t320_ma_parent
        (
            id int not null primary key,
            name varchar(50) not null,
            children_json json not null
        );

        insert into t320_ma_parent (id, name, children_json) values
            (1, 'zero', '[]'),
            (2, 'one', '[{"child_id":21,"label":"c21"}]'),
            (3, 'three', '[{"child_id":31,"label":"c31"},{"child_id":32,"label":"c32"},{"child_id":33,"label":"c33"}]');
        """;

    private static readonly object FixtureGate = new();
    private static string? _serverVersion;

    private readonly IDataContext _ctx;
    private readonly ITestOutputHelper _output;

    public T320MariaDbTvfTests(ITestOutputHelper output)
    {
        _output = output;
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        EnsureFixture();
    }

    public void Dispose() => _ctx.Dispose();

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext context, QueryCommand<T> command, bool storeInCache)
        => (DbPreparedQueryCommand<T>)context.GetPreparedQueryCommand(command, false, storeInCache, CancellationToken.None);

    private static List<string> ParametersOf<T>(DbPreparedQueryCommand<T> prepared)
        => [.. prepared.DbCommandParams.Cast<DbParameter>().Select(p => $"{p.ParameterName}={p.Value ?? "null"}")];

    /// <summary>Creates the fixture once per process and returns the observed server version.</summary>
    private string EnsureFixture()
    {
        lock (FixtureGate)
        {
            if (_serverVersion is not null)
                return _serverVersion;

            var context = (DataContext)_ctx;
            context.EnsureConnectionOpen();
            using (var command = context.CreateCommand(Ddl))
                command.ExecuteNonQuery();

            using var version = _ctx.ExecuteRaw("select version() as v");
            _serverVersion = version.Read<string>().Single();

            _output.WriteLine($"T320 MariaDB fixture DDL executed; image {ContainerImage}; server version: {_serverVersion}");
            return _serverVersion;
        }
    }

    private long Count(string sql)
    {
        using var result = _ctx.ExecuteRaw(sql);
        return result.Read<long>().Single();
    }

    private static string JsonJoin(int? parentId, bool outer) =>
        "select count(*) as c from t320_ma_parent as t1 "
        + (outer ? "left join " : "join ")
        + "json_table(t1.children_json, '$[*]' columns(child_id int path '$.child_id', label varchar(50) path '$.label')) as jt"
        + (outer ? " on true" : "")
        + (parentId is null ? "" : $" where t1.id = {parentId}");

    /// <summary>
    /// Identity fact: the server is the declared MariaDB image, and the version string proves a MariaDB
    /// server (not MySQL). JSON_TABLE needs MariaDB 10.6+, so the 11.4 image is also the capability floor.
    /// </summary>
    [Fact]
    public void ServerIdentity_ShouldBeTheDeclaredMariaDbImageAndNotMySql()
    {
        var version = EnsureFixture();
        using var comment = _ctx.ExecuteRaw("select @@version_comment as v");
        var versionComment = comment.Read<string>().Single();
        _output.WriteLine($"image: {ContainerImage}; select version(): {version}; @@version_comment: {versionComment}");

        version.Should().Contain("MariaDB", $"the live server must be MariaDB, not MySQL; observed '{version}'");
        versionComment.Should().ContainEquivalentOf("MariaDB", $"the server must identify as MariaDB; observed '{versionComment}'");
        versionComment.Should().NotContainEquivalentOf("MySQL", $"a MySQL server would make this evidence invalid; observed '{versionComment}'");

        if (!MariaDbContainer.IsExternallyConfigured)
        {
            version.Should().StartWith(
                "11.4",
                $"the declared container image is {ContainerImage} and the observed server version must match it");
        }
    }

    /// <summary>
    /// Correlated CROSS APPLY over a runtime <c>JSON_TABLE</c> source. MariaDB has no <c>LATERAL</c>
    /// keyword and correlates an applied table-function source implicitly, so the generated statement is a
    /// plain <c>join</c>; each outer row is multiplied by its document elements and the zero-match parent
    /// contributes no row.
    /// </summary>
    [Fact]
    public void CrossApply_ShouldJoinPerOuterRow_WithZeroOneAndSeveralMatches()
    {
        var version = EnsureFixture();
        _output.WriteLine($"mariadb version: {version}");

        var command = _ctx.From<IT320MaParent>()
            .CrossApply(row => T320MaIntegrationTvf.Children(row.ChildrenJson))
            .Select(p => new { p.Item1.Id, p.Item1.Name, p.Item2.ChildId, p.Item2.Label });

        var sql = Normalize(Prepare(_ctx, command, storeInCache: false).DbCommand.CommandText);
        _output.WriteLine($"cross apply sql: <<{sql}>>");

        var rows = command.ToList();
        _output.WriteLine($"cross apply rows: {rows.Count}");

        rows.Should().HaveCount(4, "1 parent x 1 element + 1 parent x 3 elements; the empty document contributes no row");
        rows.Select(r => r.Id).Distinct().Should().BeEquivalentTo(new[] { 2, 3 }, "the zero-match parent is excluded from an inner join");
        rows.Where(r => r.Id == 2).Select(r => r.ChildId).Should().BeEquivalentTo(new int?[] { 21 });
        rows.Where(r => r.Id == 3).Select(r => r.ChildId).Should().BeEquivalentTo(new int?[] { 31, 32, 33 });

        sql.Should().Contain(" join json_table(t1.children_json", "the correlated function is the join's FROM source: no derived wrapper, no LATERAL keyword");
        sql.Should().NotContain("lateral", "MariaDB has no LATERAL keyword at all");
        sql.Should().NotContain("(select", "the derived wrapper is removed, so no projected subquery remains");
    }

    /// <summary>
    /// Correlated OUTER APPLY over a runtime <c>JSON_TABLE</c> source: the generated
    /// <c>left join ... on true</c> (no <c>LATERAL</c> keyword) preserves exactly one zero-match parent
    /// with NULL expanded columns and keeps the one/several multiplicities.
    /// </summary>
    [Fact]
    public void OuterApply_ShouldPreserveTheZeroMatchParentAndKeepOneAndSeveralMatches()
    {
        var version = EnsureFixture();
        _output.WriteLine($"mariadb version: {version}");

        var command = _ctx.From<IT320MaParent>()
            .OuterApply(row => T320MaIntegrationTvf.Children(row.ChildrenJson))
            .Select(p => new { p.Item1.Id, p.Item1.Name, p.Item2.ChildId, p.Item2.Label });

        var sql = Normalize(Prepare(_ctx, command, storeInCache: false).DbCommand.CommandText);
        _output.WriteLine($"outer apply sql: <<{sql}>>");

        var rows = command.ToList();
        _output.WriteLine($"outer apply rows: {rows.Count}");

        rows.Should().HaveCount(5, "2 matching contributions (1 + 3) plus the single preserved zero-match parent");
        rows.Select(r => r.Id).Distinct().Should().BeEquivalentTo(new[] { 1, 2, 3 });

        var zero = rows.Where(r => r.Id == 1).ToArray();
        zero.Should().ContainSingle("the zero-match parent survives exactly once, not once per missing row");
        zero[0].Name.Should().Be("zero");
        zero[0].ChildId.Should().BeNull("the absent applied row must materialize as NULL, not as a default value");
        zero[0].Label.Should().BeNull("the absent applied row must not fabricate a label");

        rows.Where(r => r.Id == 2).Select(r => r.ChildId).Should().BeEquivalentTo(new int?[] { 21 });
        rows.Where(r => r.Id == 3).Select(r => r.ChildId).Should().BeEquivalentTo(new int?[] { 31, 32, 33 });

        sql.Should().Contain(" left join json_table(t1.children_json", "the outer join applies the function itself, so an unmatched parent is null-extended without a derived wrapper");
        sql.Should().Contain(" on true", "the outer join carries the ANSI ON TRUE condition");
        sql.Should().NotContain("lateral", "MariaDB has no LATERAL keyword at all");
        sql.Should().NotContain("(select", "the derived wrapper is removed, so no projected subquery remains");
    }

    /// <summary>
    /// The supported MariaDB correlation form (implicit lateral <c>JSON_TABLE</c>) expands the
    /// data-driven 0/1/N multiplicities per parent: key 1 yields no rows, key 2 exactly one and key 3
    /// three, with the same totals for the LEFT JOIN form.
    /// </summary>
    [Fact]
    public void SupportedImplicitLateralJsonTable_ShouldExpandZeroOneAndSeveralMatches()
    {
        var version = EnsureFixture();
        _output.WriteLine($"mariadb version: {version}");

        var zero = Count(JsonJoin(1, outer: false));
        var one = Count(JsonJoin(2, outer: false));
        var three = Count(JsonJoin(3, outer: false));
        _output.WriteLine($"implicit-lateral per-parent counts: 1={zero}, 2={one}, 3={three}");

        zero.Should().Be(0, "the empty document expands to no rows in an inner join");
        one.Should().Be(1, "the one-element document expands to exactly one row");
        three.Should().Be(3, "the three-element document expands to three rows");

        Count(JsonJoin(1, outer: true)).Should().Be(1, "the zero-match parent is preserved once by the LEFT JOIN form");
        Count(JsonJoin(2, outer: true)).Should().Be(1);
        Count(JsonJoin(3, outer: true)).Should().Be(3);
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
        _output.WriteLine($"mariadb version: {version}");

        _ctx.PurgeQueryCache();

        var document = """[{"child_id":21,"label":"c21"}]""";
        QueryCommand<int?> Build() => _ctx.From(() => T320MaIntegrationTvf.Children(document)).Select(c => c.ChildId);

        var first = Prepare(_ctx, Build(), storeInCache: true);
        var firstSql = Normalize(first.DbCommand.CommandText);
        var firstParameters = ParametersOf(first);
        var firstObserved = $"rendered statement: <<{firstSql}>>; parameters: [{string.Join(", ", firstParameters)}]";
        _output.WriteLine($"first run: {firstObserved}");

        firstParameters.Should().Equal(new[] { $"document={document}" }, $"the capture is a bound parameter; {firstObserved}");
        firstSql.Should().Contain("json_table(@document", firstObserved);
        firstSql.Should().NotContain(document, $"a captured document is never inlined; {firstObserved}");

        var firstRows = Build().ToList().OrderBy(x => x).ToArray();
        firstRows.Should().Equal(new int?[] { 21 }, $"the captured document expands to its only element; {firstObserved}");

        document = """[{"child_id":31,"label":"c31"},{"child_id":32,"label":"c32"},{"child_id":33,"label":"c33"}]""";

        var second = Prepare(_ctx, Build(), storeInCache: true);
        var secondSql = Normalize(second.DbCommand.CommandText);
        var secondParameters = ParametersOf(second);
        var secondObserved = $"rendered statement: <<{secondSql}>>; parameters: [{string.Join(", ", secondParameters)}]";
        _output.WriteLine($"second run: {secondObserved}");

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
