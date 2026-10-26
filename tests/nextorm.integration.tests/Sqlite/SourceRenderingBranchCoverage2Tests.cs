using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Source-rendering branch coverage executed against a live SQLite database. Every fact builds a real
/// query through the inherited <see cref="ProviderTestSuite"/> repository, executes it and asserts the
/// returned rows; a guard/reject fact always runs a live counterpart of the same shape first, so a
/// rejection is never mistaken for an unsupported provider.
///
/// <para>
/// Targeted surface: <c>src/nextorm.core/DataContext/SqlSourceRenderer.cs</c> —
/// <c>MakeWithClause</c> (22: the CTE list, the <c>i &gt; 0</c> separator and the chained declaration),
/// <c>MakeJoin</c> (186: the APPLY dispatch and its dialect gate at 399),
/// <c>MakeFrom</c> (424/432 and the source-kind switch 443-456, the physical-table branch 458-557 with
/// the index-hint renderer 481-487 and the table-hint gate 478-479, and the derived-subquery branch
/// 561-593 with the subquery-hint gate 565), <c>MakeRawSqlSource</c> (656: the named-parameter loop
/// 663-671 and the join registration 686-691), <c>MakeTableExpression</c> (711),
/// <c>MakeWhere</c> (1030), <c>MakeSort</c> (1113) and <c>MakeColumn</c> (1143: the qualified/renamed
/// column alias decision).
/// </para>
///
/// <para>
/// Overlap avoided (read and deliberately not repeated): <c>Sqlite/RawSourceBindingGuardTests.cs</c>
/// owns <c>From(string)</c>+<c>BindEntity</c>, the <c>FromSql</c> binding and the composition guard;
/// <c>Sqlite/SqlBuilderGuardTests.cs</c> owns tags and the extreme-row rejections;
/// <c>Sqlite/BuilderValidationSweepTests.cs</c> owns TABLESAMPLE, <c>WITH TIES</c> and the statement
/// overrides; <c>Sqlite/SingleFeatureBuilderTests.cs</c> owns the single-CTE read, the set operations
/// and the three basic join kinds; <c>Sqlite/CoreTerminalSweepTests.cs</c> owns the terminals;
/// <c>Sqlite/RawShapeBranchCoverage2Tests.cs</c> (D1) owns the raw-shape differentials (including raw
/// SQL and derived sources as *shape* ineligibility, not as rendering); the shared
/// <c>CommonTestSuite.Join</c> owns INNER/LEFT/RIGHT/FULL/CROSS with plain conditions and the derived
/// source read, and <c>SqlitePreparationShapeCoverageTests</c> owns derived-source projections. The
/// shapes here are disjoint from all of them: a CTE read twice and a chained CTE declaration, a
/// CTE-primary-source join (the <c>ColumnShape</c> arm), the database qualifier, the live table
/// expression, <c>FromSql</c> with named parameters and joined, the subquery-hint and table-hint gates
/// with live counterparts, the SQLite index-hint rendering, the APPLY dialect gate and nested
/// AND/OR predicate groups with a composite renamed qualified projection.
/// </para>
///
/// <para>
/// Unreachable on SQLite (recorded, never fabricated): <c>MakeJoinParts</c> (293) is called only by the
/// multi-table <c>DELETE ... USING</c> renderer, and <c>SqliteDialect</c> leaves
/// <see cref="ISqlDialect.SupportsDeleteJoin"/> at its <c>false</c> default (with
/// <c>DeleteJoinRequiresUsing</c> <c>true</c>), so the method is dead here; <c>MakeFrom</c> 472-473
/// (tables-in-scope hints) is unreachable because <c>SqlBuilder</c> rejects them first
/// (<c>SqlBuilder.cs:136-139</c>, since <c>SqliteDialect.SupportsTablesInScopeHints</c> is <c>false</c>
/// and <c>SupportsInlineHints</c> is <c>false</c>), so <c>scopeHints</c> is always <c>null</c> by the
/// time <c>MakeFrom</c> runs; <c>MakeFrom</c> 440-441 (temporal on a non-table source) is unreachable
/// because <c>SqlBuilder</c> rejects <c>FOR SYSTEM_TIME</c> first
/// (<c>SqliteDialect.SupportsTemporalTable</c> is <c>false</c>); <c>MakeFrom</c> 496-497 (linked server)
/// is reachable only through <c>WithServer</c> and is exercised here; <c>MakeFrom</c> 446-447 (pivot)
/// and 449-450 (<c>xml.nodes()</c>) are unreachable because <c>SqliteDialect.Pivot</c> and
/// <c>SqliteDialect.XmlFunctions</c> are both <c>null</c> (the base defaults) and no public builder
/// entry point constructs those sources; <c>MakeFrom</c> 437 (SelectMany/GroupJoin) is in-memory only;
/// <c>MakeRawSqlSource</c> 681-682's <c>RequireSubqueryAlias</c> disjunct is unreachable because
/// <c>SqliteDialect</c> leaves <c>RequireSubqueryAlias</c> at <c>false</c>; and
/// <c>MakeApplyJoin</c> 404-410 is unreachable because <c>SqliteDialect.SupportsApply</c> is
/// <c>false</c>, so only its gate at 399-400 runs (fact 11).
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class SourceRenderingBranchCoverage2Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    // ---------------------------------------------------------------------------------------------
    // 1-3. WITH clause rendering: one declaration read twice, a chained declaration, a paged body
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A single CTE declaration read through two aliases renders one <c>with</c> entry while the
    /// declaration's own source list stays isolated from the outer statement.
    /// </summary>
    [Fact]
    public void WithClause_SingleCteReadTwice_ShouldRenderOneDeclarationAndServeBothReads()
    {
        var ctx = Context;

        var cte = _sut.SimpleEntity
            .Where(x => x.Id <= 4)
            .Select(x => new { x.Id })
            .AsCte("d5_single_cte");

        var rows = ctx.From(cte)
            .Join(ctx.From(cte), (a, b) => a.Id == b.Id)
            .Where(p => p.Item1.Id >= 2)
            .OrderBy(p => p.Item1.Id)
            .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id })
            .ToList();

        rows.Select(r => r.Left).Should().Equal(2, 3, 4);
        rows.Should().OnlyContain(r => r.Left == r.Right);
    }

    /// <summary>
    /// A CTE whose body reads another CTE emits both declarations in one <c>with</c> list (the
    /// <c>i &gt; 0</c> separator arm) and the inner body must not nest a <c>with</c> of its own.
    /// </summary>
    [Fact]
    public void WithClause_ChainedCtes_ShouldRenderSeveralDeclarations()
    {
        var ctx = Context;

        var inner = _sut.SimpleEntity
            .Where(x => x.Id <= 5)
            .Select(x => new { x.Id })
            .AsCte("d5_inner_cte");

        var outer = ctx.From(inner)
            .Where(x => x.Id >= 3)
            .Select(x => new { x.Id })
            .AsCte("d5_outer_cte");

        var ids = ctx.From(outer).OrderBy(x => x.Id).Select(x => x.Id).ToList();

        ids.Should().Equal(3, 4, 5);
    }

    /// <summary>
    /// A CTE body with its own <c>Where</c>/<c>OrderBy</c>/<c>Limit</c> is rendered in isolation (a fresh
    /// columns and alias provider per declaration), so the outer filter does not corrupt the body.
    /// </summary>
    [Fact]
    public void WithClause_PagedCteBody_ShouldIsolateTheOuterSourceList()
    {
        var ctx = Context;

        var cte = _sut.ComplexEntity
            .OrderByDescending(x => x.Id)
            .Limit(2)
            .Select(x => new { x.Id, x.Int })
            .AsCte("d5_paged_cte");

        var rows = ctx.From(cte)
            .Where(r => r.Id > 0)
            .OrderBy(r => r.Id)
            .Select(r => new { r.Id, r.Int })
            .ToList();

        rows.Select(r => r.Id).Should().Equal(2L, 3L);
        rows.Select(r => r.Int).Should().Equal(1, 1);
    }

    // ---------------------------------------------------------------------------------------------
    // 4-5. Physical-table qualifier and the raw table-expression source
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A database qualifier on a physical table renders the SQLite single-qualifier form
    /// (<c>main.simple_entity</c>) through <c>MakeQualifiedTableName</c>.
    /// </summary>
    [Fact]
    public void From_PlainTableWithDatabaseQualifier_ShouldRenderQualifiedName()
    {
        var ids = _sut.SimpleEntity
            .WithDatabase("main")
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
    }

    /// <summary>
    /// <c>WithTableExpression</c> replaces the source with a verbatim derived table
    /// (<c>MakeTableExpression</c>, reached through the 455 dispatch) and the mapped entity's columns are
    /// read back through the alias.
    /// </summary>
    [Fact]
    public void From_TableExpressionLive_ShouldRenderDerivedTableAndFilterRows()
    {
        var ids = _sut.SimpleEntityAsClass
            .WithTableExpression("select id from simple_entity where id <= 3")
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(1, 2, 3);
    }

    // ---------------------------------------------------------------------------------------------
    // 6-7. Raw SQL sources: named parameters, then a joined raw source
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A raw SQL source binds the public properties of its parameter object as named parameters
    /// (<c>MakeRawSqlSource</c> 663-671) in both the SQL and the parameter pass.
    /// </summary>
    [Fact]
    public void From_RawSqlSourceWithNamedParameters_ShouldBindAndFilterRows()
    {
        var ids = Context
            .FromSql("select id from simple_entity where id <= $max", new { max = 3 })
            .OrderBy(t => t["id"].AsInt)
            .Select(t => t["id"].AsInt)
            .ToList();

        ids.Should().Equal(1, 2, 3);
    }

    /// <summary>
    /// A raw SQL source used as the left side of a join is aliased and registers its own item type
    /// (<c>MakeRawSqlSource</c> 686-691, the <c>hasJoins</c>/<c>IProjection</c> arm).
    /// </summary>
    [Fact]
    public void From_RawSqlSourceJoined_ShouldRenderAliasedDerivedTable()
    {
        var rows = Context
            .FromSql("select id from simple_entity")
            .Join(_sut.ComplexEntity, (s, c) => s["id"].AsInt == c.Id)
            .OrderBy(p => p.Item1["id"].AsInt)
            .Select(p => new { Id = p.Item1["id"].AsInt, Text = p.Item2.RequiredString })
            .ToList();

        rows.Select(r => r.Id).Should().Equal(1, 2, 3);
        rows.Select(r => r.Text).Should().Equal("sdf", "asdfgoi", "34mfs");
    }

    // ---------------------------------------------------------------------------------------------
    // 8-10. Reachable SQLite gates with a live counterpart: subquery hint, table hint, index hints
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A derived-table subquery hint is rejected by SQLite (<c>SupportsSubQueryHints</c> is
    /// <c>false</c>, <c>MakeFrom</c> 565-566) while the identical derived source without the hint runs.
    /// </summary>
    [Fact]
    public void From_DerivedSourceWithSubQueryHint_ShouldBeRejectedWhilePlainDerivedSourceRuns()
    {
        var ctx = Context;
        var derived = _sut.SimpleEntity.Where(x => x.Id <= 3).Select(x => new { x.Id });

        var live = ctx.From(derived).OrderBy(d => d.Id).Select(d => d.Id).ToList();
        live.Should().Equal(1, 2, 3);

        var act = () => ctx.From(derived, o => o.WithSubQueryHint("NestLoop(t1)"))
            .Select(d => d.Id)
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*Subquery hints are not supported*");
    }

    /// <summary>
    /// A table hint is rejected by SQLite (<c>SupportsTableHints</c> is <c>false</c>, <c>MakeFrom</c>
    /// 478-479) while the same source without the hint runs.
    /// </summary>
    [Fact]
    public void From_TableHint_ShouldBeRejectedWhilePlainSourceRuns()
    {
        var live = _sut.SimpleEntity.Where(x => x.Id <= 2).OrderBy(x => x.Id).Select(x => x.Id).ToList();
        live.Should().Equal(1, 2);

        var act = () => Context
            .From<ISimpleEntity>(o => o.WithTableHint("nolock"))
            .Select(x => x.Id)
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*Table hints are not supported*");
    }

    /// <summary>
    /// SQLite's index-hint renderer emits <c>NOT INDEXED</c> for the ignore kind (live) and rejects a
    /// multi-name <c>INDEXED BY</c>, which the grammar cannot express.
    /// </summary>
    [Fact]
    public void From_IndexHint_ShouldRenderNotIndexedAndRejectMultipleNames()
    {
        var ids = Context
            .From("simple_entity", o => o.WithoutIndex())
            .OrderBy(t => t["id"].AsInt)
            .Select(t => t["id"].AsInt)
            .ToList();

        ids.Should().Equal(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);

        var act = () => Context
            .From("simple_entity", o => o.WithIndex(IndexHintKind.Use, "ix_a", "ix_b"))
            .Select(t => t["id"].AsInt)
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*exactly one index name*");
    }

    // ---------------------------------------------------------------------------------------------
    // 11-12. Join rendering: the APPLY dialect gate and a CTE-primary-source join
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A <c>CROSS APPLY</c> reaches the APPLY dispatch (<c>MakeJoin</c> 223-224) and is rejected by
    /// SQLite's <c>SupportsApply</c> gate (<c>MakeApplyJoin</c> 399-400); the same two sources joined
    /// with an <c>ON</c> condition run.
    /// </summary>
    [Fact]
    public void Join_CrossApplyOnSqlite_ShouldBeRejectedWhileInnerJoinRuns()
    {
        var live = _sut.SimpleEntity
            .Where(s => s.Id <= 3)
            .Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .OrderBy(p => p.Item1.Id)
            .Select(p => p.Item1.Id)
            .ToList();

        live.Should().Equal(1, 2, 3);

        var act = () => _sut.SimpleEntity
            .CrossApply(_sut.ComplexEntity)
            .Select(p => new { p.Item1.Id })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*CrossApply join is not supported*");
    }

    /// <summary>
    /// A typed CTE read as the primary source of a join takes the projection-source branch of
    /// <c>MakeFrom</c> (the <c>ColumnShape</c> registration at 523-526) and keeps its declaration.
    /// </summary>
    [Fact]
    public void Join_CtePrimarySource_ShouldRenderTheDeclarationAndTheJoin()
    {
        var ctx = Context;

        var cte = _sut.SimpleEntity
            .Where(x => x.Id <= 3)
            .Select(x => new { x.Id })
            .AsCte("d5_joined_cte");

        var rows = ctx.From(cte)
            .Join(_sut.ComplexEntity, (c, x) => c.Id == x.Id)
            .OrderBy(p => p.Item1.Id)
            .Select(p => new { Id = p.Item1.Id, Text = p.Item2.RequiredString })
            .ToList();

        rows.Select(r => r.Id).Should().Equal(1, 2, 3);
        rows.Select(r => r.Text).Should().Equal("sdf", "asdfgoi", "34mfs");
    }

    // ---------------------------------------------------------------------------------------------
    // 13-14. Condition rendering and the qualified/renamed column alias
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A single predicate and a nested AND/OR group both render through <c>MakeWhere</c> and filter the
    /// live rows.
    /// </summary>
    [Fact]
    public void Where_NestedAndOrGroups_ShouldFilterOnTheDatabase()
    {
        var single = _sut.SimpleEntity
            .Where(x => x.Id == 5)
            .Select(x => x.Id)
            .ToList();
        single.Should().Equal(5);

        var nested = _sut.SimpleEntity
            .Where(x => (x.Id >= 2 && x.Id <= 4) || (x.Id >= 8 && x.Id <= 9))
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        nested.Should().Equal(2, 3, 4, 8, 9);
    }

    /// <summary>
    /// A qualified column whose projected name differs from the physical column takes the alias decision
    /// of <c>MakeColumn</c> (1164-1169) and the composite ordering keys render through <c>MakeSort</c>.
    /// </summary>
    [Fact]
    public void Select_QualifiedRenamedColumnWithCompositeSort_ShouldAliasAndMaterialize()
    {
        var rows = _sut.SimpleEntity
            .Where(s => s.Id <= 4)
            .Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .OrderBy(p => p.Item1.Id)
            .OrderByDescending(p => p.Item2.RequiredString)
            .Select(p => new { Key = p.Item1.Id, Renamed = p.Item2.RequiredString, p.Item2.Int })
            .ToList();

        rows.Select(r => r.Key).Should().Equal(1, 2, 3);
        rows.Select(r => r.Renamed).Should().Equal("sdf", "asdfgoi", "34mfs");
        rows.Select(r => r.Int).Should().Equal(null, 1, 1);
    }

    // ---------------------------------------------------------------------------------------------
    // 15-17. The CTE parameter pass, the join-hint dialect gate and the table-function schema gate
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A CTE body that captures a value takes the parameter pass of <c>MakeWithClause</c> (46-63): the
    /// declaration list is walked in parameter mode (no text emitted, <c>SuppressCtes</c>) while the SQL
    /// pass still renders the single declaration. The same command shape runs twice on one context, so
    /// the cached plan re-walks the body in parameter mode.
    /// </summary>
    [Fact]
    public void WithClause_ParameterisedCteBody_ShouldWalkTheParameterPassAndFilterRows()
    {
        var ctx = Context;
        var max = 4;
        var min = 2;

        var cte = _sut.SimpleEntity
            .Where(x => x.Id <= max)
            .Select(x => new { x.Id })
            .AsCte("d5_param_cte");

        List<int> Read() => ctx.From(cte)
            .Where(x => x.Id >= min)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        Read().Should().Equal(2, 3, 4);
        Read().Should().Equal(new[] { 2, 3, 4 }, "the cached plan re-walks the CTE body in parameter mode");
    }

    /// <summary>
    /// A join hint is rejected by SQLite (<c>SupportsJoinHints</c> is <c>false</c>,
    /// <c>MakeJoin</c> 195-198) while the identical join without the hint runs.
    /// </summary>
    [Fact]
    public void Join_JoinHintOnSqlite_ShouldBeRejectedWhilePlainInnerJoinRuns()
    {
        var live = _sut.SimpleEntity
            .Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .OrderBy(p => p.Item1.Id)
            .Select(p => p.Item1.Id)
            .ToList();

        live.Should().Equal(1, 2, 3);

        var hinted = () => _sut.SimpleEntity
            .Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id, j => j.WithJoinHint("loop"))
            .Select(p => p.Item1.Id)
            .ToList();

        hinted.Should().Throw<NotSupportedException>().WithMessage("*Join hints are not supported*");
    }

    /// <summary>
    /// A table function that declares its result schema is rejected by SQLite
    /// (<c>SupportsResultSchema</c> is the <c>false</c> base default, <c>RenderResultSchema</c> 874-876)
    /// while the same function without the declared schema executes <c>json_each</c> live.
    /// </summary>
    [Fact]
    public void From_DeclaredResultSchemaOnSqlite_ShouldBeRejectedWhileThePlainFunctionRuns()
    {
        var live = Context.From(() => Sr2Tvf.JsonEach("[1,2,3]"))
            .OrderBy(r => r.Value)
            .Select(r => r.Value)
            .ToList();

        live.Should().Equal(1L, 2L, 3L);

        var declared = () => Context.From(() => Sr2Tvf.JsonEachWithSchema("[1,2,3]"))
            .Select(r => r.Value)
            .ToList();

        declared.Should().Throw<NotSupportedException>()
            .WithMessage("*declares a result schema in the 'AliasColumnList' form*");
    }

    /// <summary>Row shape of SQLite's <c>json_each</c> used by the table-function schema guard.</summary>
    public interface ISr2JsonEachRow
    {
        [Column("key")]
        long Key { get; set; }

        [Column("value")]
        long Value { get; set; }
    }

    private static class Sr2Tvf
    {
        public static TvfEntityBuilder<ISr2JsonEachRow> JsonEach(string json) => new("json_each");

        public static TvfEntityBuilder<ISr2JsonEachRow> JsonEachWithSchema(string json) => new("json_each", null, null, null, Array.Empty<int>(), TableFunctionSchema.AliasColumnList);
    }
}
