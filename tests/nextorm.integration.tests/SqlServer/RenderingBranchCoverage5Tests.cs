using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.Generated.nextorm_integration_tests;
using NextORM.SqlServer;

namespace NextORM.Integration.Tests;

/// <summary>
/// SQL Server source-rendering coverage (cycle 5, cluster C6/D6) executed against a live Testcontainers
/// server. Every fact creates, seeds and drops its own <c>ss5r_marker</c>/<c>ss5r_line</c> tables and
/// asserts the rows the server returns.
///
/// <para>
/// Targeted arms: the <c>EntityBuilder</c> APPLY dispatch over a PLAIN physical table
/// (<c>CrossApply&lt;TJoinEntity&gt;(EntityBuilder&lt;TJoinEntity&gt;)</c> :2703 /
/// <c>OuterApply</c> :2712) and over a CORRELATED DERIVED query, rendered as T-SQL
/// <c>CROSS APPLY</c>/<c>OUTER APPLY</c> by <c>SqlServerDialect.MakeApply</c> (:371-375); the
/// <c>SelectWhereMax</c>/<c>SelectWhereMin</c> portable rank lowering over a nullable key
/// (<c>SqlBuilder.MakeExtremeRowOuter</c> :1273) with every tied row and with a single winner.
/// </para>
///
/// <para>
/// Overlap avoided: the plain/derived APPLY forms, hints on a joined primary source and the
/// linked-server qualifier are proved live by <c>CommonTestSuite.CorrelatedQuery</c> and
/// <c>SqlServerSourceRenderingBranchCoverage4Tests</c>; this class adds the provider-specific
/// derived-source pair and the extreme-row lowering, never Pivot/Unpivot (<c>SqlServerFeaturesTests</c>).
/// </para>
/// </summary>
[Collection("SqlServer")]
public sealed class SqlServerRenderingBranchCoverage5Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    private void Execute(string sql)
    {
        var context = (DataContext)Context;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private void CreateMarker()
    {
        Execute("drop table if exists ss5r_line");
        Execute("drop table if exists ss5r_marker");
        Execute("create table ss5r_marker (id int not null primary key, num int null, grp int not null)");
        Execute("create table ss5r_line (id int not null primary key, marker_id int not null, amount int null)");
        Execute("insert into ss5r_marker (id, num, grp) values (1, 10, 1), (2, 10, 1), (3, 20, 2), (4, 20, 2), (5, null, 3)");
        Execute("insert into ss5r_line (id, marker_id, amount) values (11, 1, 5), (12, 1, 7), (13, 2, 9)");
    }

    private void DropMarker()
    {
        Execute("drop table if exists ss5r_line");
        Execute("drop table if exists ss5r_marker");
    }

    /// <summary>
    /// Distinguishing arm: APPLY over a PLAIN physical table (SQL Server keeps the native CROSS APPLY
    /// spelling, unlike PostgreSQL's lateral fallback) and over a correlated DERIVED query. A CROSS
    /// APPLY has no <c>ON</c>, so the unmatched left row survives the physical form while the
    /// correlated form keeps only rows whose inner query returns a row.
    /// </summary>
    [Fact]
    public void CrossApply_PhysicalAndDerived_ShouldReturnOnlyTheMatchedSide()
    {
        CreateMarker();
        try
        {
            var physical = Context.From<Ss5rMarker>()
                .CrossApply(Context.From<Ss5rLine>())
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id })
                .ToList();

            physical.Should().HaveCount(15, "five marker rows times three line rows with no ON condition");
            physical.Select(r => r.Left).Should().Contain(5, "a left row with no matching line still participates in the cross product");
            physical.Select(r => r.Right).Should().Contain(new[] { 11, 12, 13 });

            var derived = Context.From<Ss5rMarker>()
                .Where(m => m.Grp == 1)
                .CrossApply(m => Context.From<Ss5rLine>()
                    .Where(l => l.MarkerId == m.Id && l.Amount >= 7)
                    .Select(l => new { l.Id, l.Amount }))
                .OrderBy(p => p.Item1.Id)
                .OrderBy(p => p.Item2.Id)
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id, p.Item2.Amount })
                .ToList();

            derived.Should().HaveCount(2, "marker 1 matches line 12 and marker 2 matches line 13");
            derived.Select(r => r.Left).Should().Equal(1, 2);
            derived.Select(r => r.Right).Should().Equal(12, 13);
            derived.Select(r => r.Amount).Should().Equal(new int?[] { 7, 9 });
            derived.Select(r => r.Right).Should().NotContain(11, "line 11 is below the correlated amount filter");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// Distinguishing arm: OUTER APPLY over the same physical and correlated derived sources. The
    /// correlated form keeps the left row whose inner query returns nothing and projects NULLs on the
    /// right side; the physical form keeps every left row with a non-NULL right row.
    /// </summary>
    [Fact]
    public void OuterApply_PhysicalAndDerived_ShouldKeepTheUnmatchedLeftRows()
    {
        CreateMarker();
        try
        {
            var physical = Context.From<Ss5rMarker>()
                .OuterApply(Context.From<Ss5rLine>())
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id })
                .ToList();

            physical.Should().HaveCount(15, "an OUTER APPLY over an uncorrelated table keeps every pair");
            physical.Should().OnlyContain(r => r.Right > 0, "the physical side always yields a line row");

            var derived = Context.From<Ss5rMarker>()
                .Where(m => m.Grp == 1)
                .OuterApply(m => Context.From<Ss5rLine>()
                    .Where(l => l.MarkerId == m.Id && l.Amount >= 9)
                    .Select(l => new { l.Id, l.Amount }))
                .OrderBy(p => p.Item1.Id)
                .Select(p => new { Left = p.Item1.Id, Right = (int?)p.Item2.Id, Amount = (int?)p.Item2.Amount })
                .ToList();

            derived.Should().HaveCount(2, "both group-1 marker rows survive the OUTER APPLY");
            derived.Select(r => r.Right).Should().Equal(new int?[] { null, 13 });
            derived.Select(r => r.Amount).Should().Equal(new int?[] { null, 9 });
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// Distinguishing arm: the extreme-row lowering over a nullable key with ties and with a single
    /// winner. Every tied row is kept for <see cref="ExtremeRowTies.All"/>; the single-row form keeps
    /// exactly one real row; NULL key rows never win.
    /// </summary>
    [Fact]
    public void Extreme_TiesAndSingle_ShouldKeepTheExtremeRowsOnly()
    {
        CreateMarker();
        try
        {
            var maxAll = Context.From<Ss5rMarker>()
                .SelectWhereMax(x => x.Num, ExtremeRowTies.All)
                .OrderBy(x => x.Id)
                .ToList();

            maxAll.Select(r => r.Id).Should().Equal(new[] { 3, 4 }, "both rows carry the maximum 20");
            maxAll.Should().OnlyContain(r => r.Num == 20);

            var maxOne = Context.From<Ss5rMarker>().SelectWhereMax(x => x.Num).ToList();
            maxOne.Should().ContainSingle();
            maxOne[0].Num.Should().Be(20);
            maxOne[0].Id.Should().BeOneOf(3, 4);

            var minAll = Context.From<Ss5rMarker>()
                .SelectWhereMin(x => x.Num, ExtremeRowTies.All)
                .OrderBy(x => x.Id)
                .ToList();

            minAll.Select(r => r.Id).Should().Equal(new[] { 1, 2 }, "both rows carry the minimum 10");
            minAll.Should().OnlyContain(r => r.Num == 10);

            var minOne = Context.From<Ss5rMarker>().SelectWhereMin(x => x.Num).ToList();
            minOne.Should().ContainSingle();
            minOne[0].Num.Should().Be(10);
            minOne[0].Id.Should().BeOneOf(1, 2);

            maxAll.Select(r => r.Id).Should().NotContain(5);
            minAll.Select(r => r.Id).Should().NotContain(5, "a NULL comparison value never wins");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// Distinguishing arm: the ANSI super-aggregate renderings <c>GROUP BY ROLLUP (...)</c> and
    /// <c>GROUP BY CUBE (...)</c> executed live on this provider. The class-owned seed has three groups
    /// with 2, 2 and 1 rows, so every form must return the three group counts plus the grand total 5.
    /// </summary>
    [Fact]
    public void GroupingRollupAndCube_ShouldReturnGroupAndGrandTotalAggregates()
    {
        CreateMarker();
        try
        {
            var rollup = Context.From<Ss5rMarker>()
                .GroupByRollup(x => new { x.Grp })
                .Select(g => SqlFunctions.Sql.count())
                .ToList();

            rollup.Should().BeEquivalentTo(new[] { 2, 2, 1, 5 },
                "three group counts plus the rollup grand total");

            var cube = Context.From<Ss5rMarker>()
                .GroupByCube(x => new { x.Grp })
                .Select(g => SqlFunctions.Sql.count())
                .ToList();

            cube.Should().BeEquivalentTo(new[] { 2, 2, 1, 5 },
                "one grouping column makes CUBE and ROLLUP return the same aggregate rows");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// Distinguishing arm: the extreme-row lowering on a GROUPED and FILTERED source through the explicit
    /// projection overload. The partition gives one winner per group in the single form and every tied
    /// winner per group in the ties form; the all-NULL group never surfaces.
    /// </summary>
    [Fact]
    public void Extreme_GroupedTiesAndSingle_ShouldKeepPerGroupExtremes()
    {
        CreateMarker();
        try
        {
            var maxOnePerGroup = Context.From<Ss5rMarker>()
                .Where(x => x.Num != null)
                .SelectWhereMax(x => x.Num, x => new { x.Id, x.Num, x.Grp }, ExtremeRowTies.One, x => x.Grp)
                .OrderBy(r => r.Grp)
                .ToList();

            maxOnePerGroup.Should().HaveCount(2, "one ranked winner per non-NULL group");
            maxOnePerGroup.Select(r => r.Grp).Should().Equal(new[] { 1, 2 });
            maxOnePerGroup.Select(r => r.Num).Should().Equal(new int?[] { 10, 20 });
            maxOnePerGroup[0].Id.Should().BeOneOf(new[] { 1, 2 }, "both group-1 rows tie at the maximum 10");
            maxOnePerGroup[1].Id.Should().BeOneOf(new[] { 3, 4 }, "both group-2 rows tie at the maximum 20");

            var minAllPerGroup = Context.From<Ss5rMarker>()
                .Where(x => x.Num != null)
                .SelectWhereMin(x => x.Num, x => new { x.Id, x.Num, x.Grp }, ExtremeRowTies.All, x => x.Grp)
                .OrderBy(r => r.Grp)
                .OrderBy(r => r.Id)
                .ToList();

            minAllPerGroup.Should().HaveCount(4, "every tied group minimum survives");
            minAllPerGroup.Select(r => r.Id).Should().Equal(new[] { 1, 2, 3, 4 });
            minAllPerGroup.Select(r => r.Num).Should().Equal(new int?[] { 10, 10, 20, 20 });
            minAllPerGroup.Select(r => r.Grp).Should().Equal(new[] { 1, 1, 2, 2 });
            minAllPerGroup.Select(r => r.Id).Should().NotContain(5, "the all-NULL group has no extreme row");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// Covers the typed <c>Cte&lt;T&gt;</c> APPLY overloads (both the one-argument and the
    /// options overload of <c>CrossApply</c>/<c>OuterApply</c>) against a live server. The CTE
    /// carries the three line rows, so every APPLY form returns the full cross product.
    /// </summary>
    [Fact]
    public void CteApply_CrossAndOuterOverloads_ShouldExecute()
    {
        CreateMarker();
        try
        {
            var cte = Context.From<Ss5rLine>().ToCommand().AsCte("ss5r_line_cte");

            var cross = Context.From<Ss5rMarker>()
                .CrossApply(cte)
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id })
                .ToList();
            cross.Should().HaveCount(15, "five marker rows times the three CTE rows");
            cross.Select(r => r.Right).Should().Contain(new[] { 11, 12, 13 });

            var crossWithOptions = Context.From<Ss5rMarker>()
                .CrossApply(cte, null)
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id })
                .ToList();
            crossWithOptions.Should().HaveCount(15);

            var outer = Context.From<Ss5rMarker>()
                .OuterApply(cte)
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id })
                .ToList();
            outer.Should().HaveCount(15);
            outer.Select(r => r.Left).Should().Contain(5);

            var outerWithOptions = Context.From<Ss5rMarker>()
                .OuterApply(cte, null)
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id })
                .ToList();
            outerWithOptions.Should().HaveCount(15);
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// Covers the explicit correlated <c>JoinAlias</c> overload taking a <c>QueryCommand</c> source:
    /// the applied derived query correlates on the left-hand row and keeps only matching line ids.
    /// The uncorrelated generated alias APPLY is proved by <c>CommonTestSuite.CorrelatedQuery</c>.
    /// </summary>
    [Fact]
    public void AliasApply_CorrelatedQueryCommandSource_ShouldExecute()
    {
        CreateMarker();
        try
        {
            var correlated = Context.From<Ss5rMarker>()
                .JoinAlias<AliasJoin_P1_A2_Buyer<Ss5rMarker, Ss5rLine>, AliasProjection_P1_A2_Buyer<Ss5rMarker, Ss5rLine>, Ss5rLine>(
                    static dc => new AliasJoin_P1_A2_Buyer<Ss5rMarker, Ss5rLine>(dc),
                    m => Context.From<Ss5rLine>().Where(l => l.MarkerId == m.Id),
                    JoinType.CrossApply)
                .Select(p => p.Buyer.Id)
                .ToList();
            correlated.Should().Equal(new[] { 11, 12, 13 }, "the correlated source keeps only the matching line rows");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// A join modifier and the alias-apply strictness modifier both reach the base join renderer on a
    /// dialect that did not opt in: SQL Server rejects <c>ANY</c> with the documented exception.
    /// </summary>
    [Fact]
    public void JoinStrictness_JoinAndAliasApply_ShouldBeRejectedByTheDialect()
    {
        CreateMarker();
        try
        {
            var join = () => Context.From<Ss5rMarker>()
                .Join(Context.From<Ss5rLine>(), (m, l) => m.Id == l.MarkerId, j => j.WithStrictness(JoinStrictness.Any))
                .Select(p => p.Item1.Id)
                .ToList();
            join.Should().Throw<NotSupportedException>()
                .WithMessage("*The Any join modifier is not supported by this SQL dialect*");

            var apply = () => Context.From<Ss5rMarker>()
                .JoinAlias<AliasJoin_P1_A2_Buyer<Ss5rMarker, Ss5rLine>, AliasProjection_P1_A2_Buyer<Ss5rMarker, Ss5rLine>, Ss5rLine>(
                    static dc => new AliasJoin_P1_A2_Buyer<Ss5rMarker, Ss5rLine>(dc),
                    m => Context.From<Ss5rLine>().Where(l => l.MarkerId == m.Id),
                    JoinType.CrossApply,
                    j => j.WithStrictness(JoinStrictness.Any))
                .Select(p => p.Buyer.Id)
                .ToList();
            apply.Should().Throw<NotSupportedException>()
                .WithMessage("*The Any join modifier is not supported by this SQL dialect*");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// The base APPLY renderer's default arm: an invalid join kind is rejected with
    /// <see cref="ArgumentOutOfRangeException"/> rather than being rendered as a lateral source.
    /// </summary>
    [Fact]
    public void MakeApply_InvalidKind_ShouldBeRejected()
    {
        var act = () => new SqlServerDialect().MakeApply(JoinType.Inner, "src");
        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*Not an APPLY join type*");
    }

    /// <summary>
    /// A live temporal table read through every <c>FOR SYSTEM_TIME</c> kind, plus the documented
    /// rejection when the temporal clause is combined with a raw table expression.
    /// </summary>
    [Fact]
    public void ForSystemTime_AllKindsAndTableExpressionGuard_ShouldRenderAndExecute()
    {
        Execute("if object_id('dbo.ss5r_temporal') is not null exec('alter table ss5r_temporal set (system_versioning = off)')");
        Execute("drop table if exists ss5r_temporal_history");
        Execute("drop table if exists ss5r_temporal");
        Execute("create table ss5r_temporal (id int not null primary key, num int null, "
                + "sys_start datetime2 generated always as row start not null, "
                + "sys_end datetime2 generated always as row end not null, "
                + "period for system_time (sys_start, sys_end)) "
                + "with (system_versioning = on (history_table = dbo.ss5r_temporal_history))");
        Execute("insert into ss5r_temporal (id, num) values (1, 10), (2, 20)");
        try
        {
            var now = DateTime.UtcNow;
            var currentClauses = new[]
            {
                TemporalClause.All(),
                TemporalClause.AsOf(now.AddMinutes(1)),
                TemporalClause.Between(now.AddMinutes(-1), now.AddMinutes(1)),
                TemporalClause.FromTo(now.AddMinutes(-1), now.AddMinutes(1))
            };

            foreach (var clause in currentClauses)
            {
                var ids = Context.From<Ss5rTemporal>()
                    .ForSystemTime(clause)
                    .OrderBy(x => x.Id)
                    .Select(x => x.Id)
                    .ToList();

                ids.Should().Equal(new[] { 1, 2 }, "the seeded rows are current for the whole test window");
            }

            var contained = Context.From<Ss5rTemporal>()
                .ForSystemTime(TemporalClause.ContainedIn(now.AddMinutes(-1), now.AddMinutes(1)))
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToList();
            contained.Should().BeEmpty(
                "CONTAINED IN keeps only rows whose closed period fits the window, and the current rows end at 9999-12-31");

            var tableExpression = () => Context.From<Ss5rTemporal>()
                .WithTableExpression("select id, num from ss5r_temporal")
                .ForSystemTime(TemporalClause.All())
                .Select(x => x.Id)
                .ToList();
            tableExpression.Should().Throw<NotSupportedException>()
                .WithMessage("*FOR SYSTEM_TIME clause cannot be combined with a table expression source*");
        }
        finally
        {
            Execute("if object_id('dbo.ss5r_temporal') is not null exec('alter table ss5r_temporal set (system_versioning = off)')");
            Execute("drop table if exists ss5r_temporal_history");
            Execute("drop table if exists ss5r_temporal");
        }
    }

    /// <summary>
    /// The two distinct <c>WITH TIES</c> guards behind <c>SupportsWithTies</c>: a non-positive page
    /// limit and a <c>DISTINCT</c> conflict are rejected while the SQL is assembled.
    /// </summary>
    [Fact]
    public void WithTies_ZeroLimitAndDistinct_ShouldBeRejected()
    {
        CreateMarker();
        try
        {
            var zeroLimit = () => Context.From<Ss5rMarker>()
                .OrderBy(x => x.Id)
                .Limit(0)
                .WithTies()
                .Select(x => x.Id)
                .ToList();
            zeroLimit.Should().Throw<BuildSqlCommandException>()
                .WithMessage("*WITH TIES requires a positive page limit*");

            var distinct = () => Context.From<Ss5rMarker>()
                .Distinct()
                .OrderBy(x => x.Id)
                .Limit(1)
                .WithTies()
                .Select(x => x.Id)
                .ToList();
            distinct.Should().Throw<BuildSqlCommandException>()
                .WithMessage("*WITH TIES cannot be combined with DISTINCT or DISTINCT ON*");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// A blank join-table hint is ignored (the later valid hint wins) while the join still executes;
    /// a blank optimizer join hint is rejected by the option itself.
    /// </summary>
    [Fact]
    public void JoinHints_BlankTableHintAndEmptyJoinHint_ShouldNormalizeAndReject()
    {
        CreateMarker();
        try
        {
            var rows = Context.From<Ss5rMarker>()
                .Join(Context.From<Ss5rLine>(), (m, l) => m.Id == l.MarkerId, j =>
                {
                    j.WithJoinTableHint(" ");
                    j.WithJoinTableHint("nolock");
                })
                .OrderBy(p => p.Item2.Id)
                .Select(p => p.Item2.Id)
                .ToList();
            rows.Should().Equal(new[] { 11, 12, 13 });

            var hinted = Context.From<Ss5rMarker>()
                .Join(Context.From<Ss5rLine>(), (m, l) => m.Id == l.MarkerId, j => j.WithJoinHint("loop"))
                .OrderBy(p => p.Item2.Id)
                .Select(p => p.Item2.Id)
                .ToList();
            hinted.Should().Equal(rows, "an optimizer join hint does not change the rows");

            var empty = () => new JoinOptions().WithJoinHint(" ");
            empty.Should().Throw<ArgumentException>().WithMessage("*A join hint must be a non-empty string*");
        }
        finally
        {
            DropMarker();
        }
    }

    /// <summary>
    /// The asynchronous <c>FOR XML</c> terminal executes, and the dialect renders every XML mode arm
    /// (RAW/AUTO/EXPLICIT/PATH) through its public renderer.
    /// </summary>
    [Fact]
    public async Task ForXmlAsync_AndDialectModes_ShouldRenderAndExecute()
    {
        CreateMarker();
        try
        {
            var xml = await Context.From<Ss5rMarker>()
                .Where(x => x.Id == 1)
                .Select(x => new { x.Id, x.Num })
                .ForXmlAsync(ForXmlMode.Path, root: "items", cancellationToken: TestContext.Current.CancellationToken);

            xml.Should().NotBeNull();
            xml!.Should().Contain("<items>");

            var dialect = new SqlServerDialect();
            dialect.MakeForXml(new ForXmlClause(ForXmlMode.Raw, ElementName: "row"), KeywordCase.Lower)
                .Should().Be("for xml raw('row')");
            dialect.MakeForXml(new ForXmlClause(ForXmlMode.Auto), KeywordCase.Lower)
                .Should().Be("for xml auto");
            dialect.MakeForXml(new ForXmlClause(ForXmlMode.Explicit), KeywordCase.Lower)
                .Should().Be("for xml explicit");
            dialect.MakeForXml(new ForXmlClause(ForXmlMode.Path), KeywordCase.Lower)
                .Should().Be("for xml path");
        }
        finally
        {
            DropMarker();
        }
    }

    [SqlTable("ss5r_marker")]
    public sealed class Ss5rMarker
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("num")]
        public int? Num { get; set; }

        [Column("grp")]
        public int Grp { get; set; }
    }

    [SqlTable("ss5r_line")]
    public sealed class Ss5rLine
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("marker_id")]
        public int MarkerId { get; set; }

        [Column("amount")]
        public int? Amount { get; set; }
    }

    [SqlTable("ss5r_temporal")]
    public sealed class Ss5rTemporal
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("num")]
        public int? Num { get; set; }
    }
}
