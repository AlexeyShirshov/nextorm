using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// SQL Server SQL-rendering branch coverage (cycle 6, cluster D3) executed against a live Testcontainers
/// server. Every fact owns its <c>sr6_sqlserver_*</c> tables (created and seeded in the constructor) and
/// asserts the exact rows the server returns.
///
/// <para>
/// Targeted arms: <c>SqlSourceRenderer.MakeApplyJoin</c>/<c>MakeTableFunction</c> (CROSS and OUTER APPLY
/// over an <c>openjson</c> table function), <c>SqlBuilder.MakeSelect</c>'s TOP versus OFFSET/FETCH page
/// shapes, <c>MakeUpdateJoin</c>/<c>MakeDeleteJoin</c> (the alias-style <c>UPDATE</c>/<c>DELETE ...
/// FROM ... JOIN</c> forms), <c>SqlSourceRenderer.MakeXmlNodes</c> under OUTER APPLY, the structural
/// hint arms of <c>MakeFrom</c> (<c>MakeTableHints</c>, <c>TableHintsWithIndex</c>, <c>AppendHints</c>)
/// and a derived join source with an alias (<c>MakeFrom</c>/<c>MakeJoin</c>).
/// </para>
///
/// <para>
/// Overlap avoided (read and deliberately not repeated): <c>SqlServer/RenderingBranchCoverage5Tests</c>
/// owns the plain/derived APPLY pair, the extreme-row lowering and ROLLUP/CUBE;
/// <c>SqlServer/SqlServerSourceRenderingBranchCoverage4Tests</c> owns NOLOCK on a joined primary source
/// and the linked-server qualifier; <c>SqlServer/SqlServerSpecificTests.XmlNodes_ShouldUnfoldNodes</c>
/// owns the CROSS APPLY XML form. This class adds the table-function APPLY, the OUTER APPLY XML form,
/// the page-shape pair, the mutations and the scope-hint/index-hint combination.
/// </para>
/// </summary>
[Collection("SqlServer")]
public sealed class SqlServerRenderingBranchCoverage6Tests : ProviderTestSuite
{
    private const string Ddl = """
        drop table if exists sr6_sqlserver_doc;
        drop table if exists sr6_sqlserver_line;
        drop table if exists sr6_sqlserver_marker;
        create table sr6_sqlserver_marker
        (
            id int not null primary key,
            name nvarchar(50) not null,
            grp int not null,
            score int not null
        );
        create table sr6_sqlserver_line
        (
            id int not null primary key,
            marker_id int not null,
            qty int not null
        );
        create table sr6_sqlserver_doc
        (
            id int not null primary key,
            payload xml null
        );
        insert into sr6_sqlserver_marker (id, name, grp, score) values
            (1, N'alpha', 1, 5),
            (2, N'beta', 1, 7),
            (3, N'gamma', 2, 9),
            (4, N'delta', 2, 11);
        insert into sr6_sqlserver_line (id, marker_id, qty) values
            (11, 1, 1),
            (12, 1, 3),
            (13, 2, 5),
            (14, 3, 7);
        insert into sr6_sqlserver_doc (id, payload) values
            (1, N'<root><item id="1">alpha</item><item id="2">beta</item></root>'),
            (2, N'<root/>');
        create index ix_sr6_sqlserver_grp on sr6_sqlserver_marker (grp);
        """;

    public SqlServerRenderingBranchCoverage6Tests() => Execute(Ddl);

    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    private void Execute(string sql)
    {
        var context = (DataContext)Context;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Distinguishing arm: CROSS and OUTER APPLY over a table-valued function (<c>openjson</c>). The
    /// cross form yields one row per generated element; the outer form over an empty array keeps every
    /// left row with a NULL right side.
    /// </summary>
    [Fact]
    public void ApplyOverTableFunction_CrossAndOuter_ShouldExpandAndKeepTheLeftRows()
    {
        const string json = """[{"k":1},{"k":2}]""";

        var crossed = Context.From<Sr6SqlServerMarker>()
            .CrossApply(Context.FromTableFunction(() => SqlFunctions.SqlServer.openjson(json)))
            .Select(p => new { Marker = p.Item1.Id, p.Item2.Key })
            .ToList();

        crossed.Should().HaveCount(8, "four marker rows times two JSON elements");
        crossed.Select(r => r.Key).Should().OnlyContain(k => k == "0" || k == "1");
        crossed.Select(r => r.Key).Should().Contain("0").And.Contain("1");

        var outerEmpty = Context.From<Sr6SqlServerMarker>()
            .OuterApply(Context.FromTableFunction(() => SqlFunctions.SqlServer.openjson("[]")))
            .OrderBy(p => p.Item1.Id)
            .Select(p => new { Marker = p.Item1.Id, Key = (string?)p.Item2.Key })
            .ToList();

        outerEmpty.Should().HaveCount(4, "an OUTER APPLY keeps every left row when the function yields no row");
        outerEmpty.Select(r => r.Key).Should().OnlyContain(k => k == null);
    }

    /// <summary>
    /// Distinguishing arm: the two SQL Server page shapes. A limit without offset renders TOP; a limit
    /// with offset (and an offset-only page) renders OFFSET/FETCH, and every page returns its exact
    /// window.
    /// </summary>
    [Fact]
    public void Pages_TopAndOffsetFetch_ShouldReturnTheRequestedWindows()
    {
        var top = Context.From<Sr6SqlServerMarker>()
            .OrderBy(x => x.Id)
            .Limit(2)
            .Select(x => x.Id)
            .ToList();

        top.Should().Equal(new[] { 1, 2 }, "TOP(2) keeps the first two ordered rows");

        var offsetFetch = Context.From<Sr6SqlServerMarker>()
            .OrderBy(x => x.Id)
            .Limit(2)
            .Offset(2)
            .Select(x => x.Id)
            .ToList();

        offsetFetch.Should().Equal(new[] { 3, 4 }, "OFFSET 2 FETCH NEXT 2 skips the first two ordered rows");

        var offsetOnly = Context.From<Sr6SqlServerMarker>()
            .OrderBy(x => x.Id)
            .Offset(1)
            .Select(x => x.Id)
            .ToList();

        offsetOnly.Should().Equal(new[] { 2, 3, 4 }, "an offset-only page keeps everything after the first row");
    }

    /// <summary>
    /// Distinguishing arm: the alias-style multi-table mutations. <c>MakeUpdateJoin</c> renders
    /// <c>UPDATE &lt;alias&gt; ... FROM ... JOIN</c> and <c>MakeDeleteJoin</c> renders
    /// <c>DELETE &lt;alias&gt; FROM ... JOIN</c>; the live reads prove the target row changed/vanished.
    /// </summary>
    [Fact]
    public void UpdateAndDeleteJoin_ShouldMutateTheTargetRowsOnly()
    {
        var updated = Context.From<Sr6SqlServerMarker>()
            .Join(Context.From<Sr6SqlServerLine>(), (m, l) => m.Id == l.MarkerId)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Score, p => p.Item2.Qty)
            .Where(p => p.Item2.Id == 14)
            .Update();

        updated.Should().Be(1);

        Context.From<Sr6SqlServerMarker>()
            .OrderBy(x => x.Id)
            .Select(x => x.Score)
            .ToList()
            .Should().Equal(new[] { 5, 7, 7, 11 }, "marker 3 copies line 14's qty");

        var deleted = Context.From<Sr6SqlServerLine>()
            .Join(Context.From<Sr6SqlServerMarker>(), (l, m) => l.MarkerId == m.Id)
            .Where(p => p.Item1.Id == 13)
            .Delete();

        deleted.Should().Be(1);

        Context.From<Sr6SqlServerLine>()
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList()
            .Should().Equal(new[] { 11, 12, 14 }, "only line 13 was removed");
    }

    /// <summary>
    /// Distinguishing arm: <c>MakeXmlNodes</c> under OUTER APPLY. A document whose XQuery selects no
    /// node keeps its row with a NULL rowset column, which the CROSS APPLY form can never return.
    /// </summary>
    [Fact]
    public void XmlNodesOuterApply_ShouldKeepTheDocumentWithoutNodes()
    {
        var rows = Context.From<Sr6SqlServerDoc>()
            .OuterApply(x => SqlFunctions.SqlServer.xml_nodes(x.Payload, "/root/item"))
            .OrderBy(p => p.Item1.Id)
            .Select(p => new
            {
                p.Item1.Id,
                Value = SqlFunctions.SqlServer.xml_value<string>(p.Item2.Value, "(.)[1]", "nvarchar(100)")
            })
            .ToList();

        rows.Should().HaveCount(3, "document 1 contributes two nodes and document 2 contributes none");
        rows.Select(r => r.Id).Should().Equal(new[] { 1, 1, 2 });
        rows.Select(r => r.Value).Should().Equal(new string?[] { "alpha", "beta", null },
            "the node-less document survives with a NULL rowset value");
    }

    /// <summary>
    /// Distinguishing arm: the structural hint arms. A per-table hint and a tables-in-scope hint are
    /// merged into one <c>WITH (...)</c> clause, and an index hint is folded into the same clause on the
    /// joined primary source. The hinted statement reads exactly the plain statement's rows.
    /// </summary>
    [Fact]
    public void TableAndIndexHints_ShouldMergeIntoOneClauseAndKeepTheRows()
    {
        var plain = Context.From<Sr6SqlServerMarker>()
            .Join(Context.From<Sr6SqlServerLine>(), (m, l) => m.Id == l.MarkerId)
            .OrderBy(p => p.Item1.Id)
            .OrderBy(p => p.Item2.Id)
            .Select(p => new { Marker = p.Item1.Id, Line = p.Item2.Id })
            .ToList();

        plain.Should().HaveCount(4);
        plain.Select(r => r.Line).Should().Equal(new[] { 11, 12, 13, 14 });

        var scopeMerged = Context.From<Sr6SqlServerMarker>(o => o.WithTableHint("forceseek"))
            .WithTablesInScopeHint("nolock")
            .Join(Context.From<Sr6SqlServerLine>(), (m, l) => m.Id == l.MarkerId)
            .OrderBy(p => p.Item1.Id)
            .OrderBy(p => p.Item2.Id)
            .Select(p => new { Marker = p.Item1.Id, Line = p.Item2.Id })
            .ToList();

        scopeMerged.Should().BeEquivalentTo(plain, "hints are a rendering dimension, not a row filter");

        var indexMerged = Context.From<Sr6SqlServerMarker>(o =>
            {
                o.WithTableHint("rowlock");
                o.WithIndex("ix_sr6_sqlserver_grp");
            })
            .Join(Context.From<Sr6SqlServerLine>(), (m, l) => m.Id == l.MarkerId)
            .OrderBy(p => p.Item1.Id)
            .OrderBy(p => p.Item2.Id)
            .Select(p => new { Marker = p.Item1.Id, Line = p.Item2.Id })
            .ToList();

        indexMerged.Should().BeEquivalentTo(plain, "the folded index hint keeps the same rows");
    }

    /// <summary>
    /// Distinguishing arm: a derived aggregate source with an alias joined back to the physical table
    /// under a predicate. The join condition resolves through the derived alias and the GROUP BY runs
    /// inside the derived table.
    /// </summary>
    [Fact]
    public void DerivedAggregateJoin_ShouldJoinTheAggregatedRows()
    {
        var totals = Context.From(Context.From<Sr6SqlServerLine>()
            .GroupBy(l => new { l.MarkerId })
            .Select(g => new { g.MarkerId, Total = SqlFunctions.Sql.sum(g.Qty) }));

        var rows = Context.From<Sr6SqlServerMarker>()
            .Join(totals, (m, t) => m.Id == t.MarkerId)
            .Where(p => p.Item1.Grp == 1)
            .OrderBy(p => p.Item1.Id)
            .Select(p => new { Marker = p.Item1.Id, p.Item2.Total })
            .ToList();

        rows.Should().HaveCount(2);
        rows.Select(r => r.Marker).Should().Equal(new[] { 1, 2 });
        rows.Select(r => r.Total).Should().Equal(new[] { 4, 5 },
            "marker 1 sums lines 11 and 12; marker 2 has only line 13");

        // T-SQL has no WINDOW clause: a named window is rejected before any SQL is emitted.
        FluentActions.Invoking(() =>
            {
                var w = Context.From<Sr6SqlServerMarker>();
                w.Window("sr6_ss_w", orderBy: [w.Asc(x => x.Id)])
                    .Select(x => new { x.Id, Rank = SqlFunctions.Sql.row_number().Over("sr6_ss_w") })
                    .ToList();
            })
            .Should().Throw<NotSupportedException>()
            .WithMessage("*Named windows*");
    }

    [SqlTable("sr6_sqlserver_marker")]
    public sealed class Sr6SqlServerMarker
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("grp")]
        public int Grp { get; set; }

        [Column("score")]
        public int Score { get; set; }
    }

    [SqlTable("sr6_sqlserver_line")]
    public sealed class Sr6SqlServerLine
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("marker_id")]
        public int MarkerId { get; set; }

        [Column("qty")]
        public int Qty { get; set; }
    }

    [SqlTable("sr6_sqlserver_doc")]
    public sealed class Sr6SqlServerDoc
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("payload")]
        public string? Payload { get; set; }
    }
}
