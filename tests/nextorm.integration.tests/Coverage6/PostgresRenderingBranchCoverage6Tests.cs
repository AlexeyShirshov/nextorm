using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;

namespace NextORM.Integration.Tests;

/// <summary>
/// PostgreSQL SQL-rendering branch coverage (cycle 6, cluster D3) executed against a live Testcontainers
/// server. Every fact owns its <c>sr6_postgres_*</c> tables (created and seeded in the constructor) and
/// asserts the exact rows the server returns.
///
/// <para>
/// Targeted arms: <c>SqlSourceRenderer.MakeTableFunction</c> (a built-in <c>generate_series</c> source
/// both bare and joined), <c>MakeFrom</c>/<c>MakeJoin</c> (a derived join source and a FULL JOIN with
/// unmatched rows on both sides), <c>SqlBuilder.MakeNamedWindow</c> (multi-partition, multi-key
/// ordering with a descending key, and a frame with <c>EXCLUDE CURRENT ROW</c>),
/// <c>MakeUpdateJoin</c> (<c>UPDATE ... FROM</c>) and <c>MakeDeleteJoin</c> (<c>DELETE ... USING</c>),
/// <c>SqlSourceRenderer.MakeWithClause</c> (two declarations plus a recursive one) and
/// <c>MakeSelect</c>'s <c>DISTINCT ON</c> head over a two-column key.
/// </para>
///
/// <para>
/// Overlap avoided (read and deliberately not repeated): <c>Postgres/RenderingBranchCoverage5Tests</c>
/// owns the LATERAL/plain APPLY pair, the row locks, ROLLUP/CUBE and the extreme-row dispatch;
/// <c>Postgres/PostgresSourceRenderingBranchCoverage4Tests</c> owns the correlated derived APPLY and
/// the per-join-hint rejection; <c>Postgres/PostgresDialectBranchCoverage3Tests</c> owns the single
/// <c>DISTINCT ON</c> form. This class adds the table-function source, the FULL JOIN, the two-column
/// <c>DISTINCT ON</c>, the mutations and the recursive/multi-CTE prefixes.
/// </para>
/// </summary>
[Collection("Postgres")]
public sealed class PostgresRenderingBranchCoverage6Tests : ProviderTestSuite
{
    private const string Ddl = """
        drop table if exists sr6_postgres_line;
        drop table if exists sr6_postgres_marker;
        create table sr6_postgres_marker
        (
            id int not null primary key,
            grp int not null,
            tag int not null,
            name text not null,
            score int not null
        );
        create table sr6_postgres_line
        (
            id int not null primary key,
            marker_id int not null,
            qty int not null
        );
        insert into sr6_postgres_marker (id, grp, tag, name, score) values
            (1, 1, 10, 'alpha', 5),
            (2, 1, 10, 'beta', 7),
            (3, 2, 20, 'gamma', 9),
            (4, 2, 20, 'delta', 11);
        insert into sr6_postgres_line (id, marker_id, qty) values
            (11, 1, 1),
            (12, 1, 3),
            (13, 2, 5),
            (14, 3, 7),
            (99, 99, 2);
        """;

    public PostgresRenderingBranchCoverage6Tests() => Execute(Ddl);

    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    private void Execute(string sql)
    {
        var context = (DataContext)Context;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    /// <summary>Drops every cached plan (and the raw candidate index) on this context.</summary>
    private void ResetPlanCache() => Context.PurgeQueryCache();

    /// <summary>Resolves the prepared state (plan key material) without storing the plan.</summary>
    private static void Resolve(QueryCommand command)
        => command.PrepareCommand(dontCalculateHash: false, CancellationToken.None);

    /// <summary>Prepares without the plan store and executes the command against the live server.</summary>
    private List<TResult> Run<TResult>(QueryCommand<TResult> command)
        => Context.ToList(((DataContext)Context).GetPreparedQueryCommand(
            command,
            createEnumerator: false,
            storeInCache: false,
            CancellationToken.None));

    /// <summary>
    /// Distinguishing arm: a built-in table function as a FROM source, read bare and joined to a
    /// physical table. The join forces the function alias branch of <c>MakeTableFunction</c>.
    /// </summary>
    [Fact]
    public void TableFunction_GenerateSeriesBareAndJoined_ShouldReturnTheGeneratedRows()
    {
        var plain = Context.From(() => SqlFunctions.Postgres.generate_series(1L, 4L))
            .OrderBy(r => r.Value)
            .Select(r => r.Value)
            .ToList();

        plain.Should().Equal(new long[] { 1, 2, 3, 4 }, "generate_series(1, 4) yields four rows");

        var joined = Context.From(() => SqlFunctions.Postgres.generate_series(1L, 4L))
            .Join(Context.From<Sr6PostgresMarker>(), (g, m) => g.Value == m.Id)
            .OrderBy(p => p.Item1.Value)
            .Select(p => new { p.Item1.Value, p.Item2.Name })
            .ToList();

        joined.Should().HaveCount(4);
        joined.Select(r => r.Value).Should().Equal(new long[] { 1, 2, 3, 4 });
        joined.Select(r => r.Name).Should().Equal("alpha", "beta", "gamma", "delta");
    }

    /// <summary>
    /// Distinguishing arm: a DERIVED join source under a predicate (MakeFrom/MakeJoin), plus a FULL JOIN
    /// whose unmatched rows survive on both sides. The full-join arm differs from the cycle-5 APPLY
    /// family, which never renders the right/full join keyword.
    /// </summary>
    [Fact]
    public void DerivedJoinAndFullJoin_ShouldKeepTheDerivedRowsAndBothUnmatchedSides()
    {
        var derived = Context.From(Context.From<Sr6PostgresLine>()
            .Where(l => l.Qty >= 3)
            .Select(l => new { l.Id, l.MarkerId, l.Qty }));

        var filtered = Context.From<Sr6PostgresMarker>()
            .Join(derived, (m, d) => m.Id == d.MarkerId)
            .Where(p => p.Item1.Grp == 1)
            .OrderBy(p => p.Item1.Id)
            .OrderBy(p => p.Item2.Id)
            .Select(p => new { Marker = p.Item1.Id, Line = p.Item2.Id, p.Item2.Qty })
            .ToList();

        filtered.Should().HaveCount(2, "only group-1 markers join a line with qty >= 3");
        filtered.Select(r => r.Line).Should().Equal(new[] { 12, 13 });
        filtered.Select(r => r.Qty).Should().Equal(new[] { 3, 5 });

        var full = Context.From<Sr6PostgresMarker>()
            .FullJoin(Context.From<Sr6PostgresLine>(), (m, l) => m.Id == l.MarkerId)
            .OrderBy(p => p.Item1.Id)
            .OrderBy(p => p.Item2.Id)
            .Select(p => new { Left = (int?)p.Item1.Id, Right = (int?)p.Item2.Id })
            .ToList();

        full.Should().HaveCount(6, "four marker rows (two with two lines) plus a right-only line");
        full.Select(r => r.Left).Should().Equal(new int?[] { 1, 1, 2, 3, 4, null });
        full.Select(r => r.Right).Should().Equal(new int?[] { 11, 12, 13, 14, null, 99 });
    }

    /// <summary>
    /// Distinguishing arm: three named windows in one <c>WINDOW</c> clause. The first partitions by two
    /// columns, the second orders by two keys with a descending direction and a whole-partition frame,
    /// the third adds <c>EXCLUDE CURRENT ROW</c> to a running frame.
    /// </summary>
    [Fact]
    public void NamedWindows_FrameAndExclusion_ShouldReadTheWindowValues()
    {
        var e = Context.From<Sr6PostgresMarker>();

        var rows = e
            .Window(
                "sr6_pg_w_keys",
                partitionBy: [x => x.Grp, x => x.Tag],
                orderBy: [e.Asc(x => x.Id)])
            .Window(
                "sr6_pg_w_multi",
                orderBy: [e.Asc(x => x.Id), e.Desc(x => x.Score)],
                frame: WindowFrame.Range(WindowFrameBound.UnboundedPreceding, WindowFrameBound.UnboundedFollowing))
            .Window(
                "sr6_pg_w_exclude",
                partitionBy: [x => x.Grp],
                orderBy: [e.Asc(x => x.Id)],
                frame: WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow)
                    .WithExclusion(WindowFrameExclusion.CurrentRow))
            .Select(x => new
            {
                x.Id,
                Rank = SqlFunctions.Sql.row_number().Over("sr6_pg_w_keys"),
                Whole = SqlFunctions.Sql.count_over().Over("sr6_pg_w_multi"),
                Prior = SqlFunctions.Sql.count_over().Over("sr6_pg_w_exclude")
            })
            .OrderBy(x => x.Id)
            .ToList();

        rows.Should().HaveCount(4);
        rows.Select(r => r.Rank).Should().Equal(new[] { 1, 2, 1, 2 },
            "each (grp, tag) partition holds two rows ordered by id");
        rows.Select(r => r.Whole).Should().Equal(new[] { 4, 4, 4, 4 },
            "the unbounded frame covers the whole partition");
        rows.Select(r => r.Prior).Should().Equal(new[] { 0, 1, 0, 1 },
            "EXCLUDE CURRENT ROW leaves only the rows before the current row of each group");
    }

    /// <summary>
    /// Distinguishing arm: the multi-table mutations on PostgreSQL. <c>MakeUpdateJoin</c> renders
    /// <c>UPDATE ... FROM</c> and <c>MakeDeleteJoin</c> renders <c>DELETE ... USING</c>; both are
    /// verified by live reads afterwards.
    /// </summary>
    [Fact]
    public void UpdateFromAndDeleteUsing_ShouldMutateTheTargetRowsOnly()
    {
        var updated = Context.From<Sr6PostgresMarker>()
            .Join(Context.From<Sr6PostgresLine>(), (m, l) => m.Id == l.MarkerId)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Score, p => p.Item2.Qty)
            .Where(p => p.Item2.Id == 14)
            .Update();

        updated.Should().Be(1);

        Context.From<Sr6PostgresMarker>()
            .OrderBy(x => x.Id)
            .Select(x => x.Score)
            .ToList()
            .Should().Equal(new[] { 5, 7, 7, 11 }, "marker 3 copies line 14's qty");

        var deleted = Context.From<Sr6PostgresLine>()
            .Join(Context.From<Sr6PostgresMarker>(), (l, m) => l.MarkerId == m.Id)
            .Where(p => p.Item1.Id == 13)
            .Delete();

        deleted.Should().Be(1);

        Context.From<Sr6PostgresLine>()
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList()
            .Should().Equal(new[] { 11, 12, 14, 99 }, "only line 13 was removed");
    }

    /// <summary>
    /// Distinguishing arm: the CTE prefix with two declarations (the comma arm) and a recursive
    /// declaration (<c>WITH RECURSIVE</c> plus the step's self-reference), both executed live.
    /// </summary>
    [Fact]
    public void Ctes_TwoDeclarationsAndRecursive_ShouldReadTheHoistedRows()
    {
        var high = Context.From<Sr6PostgresMarker>().Where(m => m.Grp == 1).Select(m => new { m.Id });
        var low = Context.From<Sr6PostgresMarker>().Where(m => m.Grp == 2).Select(m => new { m.Id });

        var twoCtes = Context
            .With("sr6_pg_high", high)
            .With("sr6_pg_low", low)
            .From("sr6_pg_high")
            .OrderBy(t => t["id"].AsInt)
            .Select(t => t["id"].AsInt)
            .ToList();

        twoCtes.Should().Equal(new[] { 1, 2 }, "the outer query reads the first declaration");

        var anchor = Context.From<Sr6PostgresMarker>().Where(m => m.Id == 1).Select(m => new { m.Id });
        var step = Context.From("sr6_pg_nums")
            .Where(t => t["id"].AsInt < 3)
            .Select(t => new { Id = t["id"].AsInt + 1 });

        var series = Context
            .WithRecursive("sr6_pg_nums", anchor.Union(step))
            .From("sr6_pg_nums")
            .OrderBy(t => t["id"].AsInt)
            .Select(t => t["id"].AsInt)
            .ToList();

        series.Should().Equal(new[] { 1, 2, 3 }, "the recursive step stops at 3");
    }

    /// <summary>
    /// Distinguishing arm: <c>DISTINCT ON</c> over a two-column key ordered by a third, descending key
    /// (a different column set and order than the single-key form already proved by
    /// <c>PostgresDialectBranchCoverage3Tests</c>).
    /// </summary>
    [Fact]
    public void DistinctOn_TwoColumnKey_ShouldKeepTheHighestScorePerPair()
    {
        var rows = Context.From<Sr6PostgresMarker>()
            .DistinctOn(x => new { x.Grp, x.Tag })
            .OrderBy(x => x.Grp)
            .OrderBy(x => x.Tag)
            .OrderByDescending(x => x.Score)
            .Select(x => new { x.Id, x.Grp, x.Tag, x.Score })
            .ToList();

        rows.Should().HaveCount(2, "the four rows form two (grp, tag) pairs");
        rows.Select(r => r.Id).Should().Equal(new[] { 2, 4 }, "the descending score keeps the higher row of each pair");
        rows.Select(r => r.Score).Should().Equal(new[] { 7, 11 });
        rows.Should().OnlyContain(r => r.Score > 5);

        // PostgreSQL has no index-hint spelling: the source-level hint is rejected by the dialect gate.
        FluentActions.Invoking(() => Context.From<Sr6PostgresMarker>(o => o.WithIndex("ix_sr6_pg_grp"))
                .Select(x => x.Id)
                .ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*Index hints are not supported*");
    }

    // ---------------------------------------------------------------------------------------------
    // Plan key: GroupingSetsEqual (D1b)
    // ---------------------------------------------------------------------------------------------
    /// <summary>
    /// Distinguishing arm: <c>QueryPlanEqualityComparer.GroupingSetsEqual</c> matching control. Two
    /// independently built commands with the same two-set list resolve and compare TRUE, and both read
    /// the live grouping-set rows: one count per grouping column plus no grand total.
    /// </summary>
    [Fact]
    public void GroupingSets_EqualList_ShouldShareThePlanKeyAndReadTheSetRows()
    {
        ResetPlanCache();

        QueryCommand<int> Counts() => Context.From<Sr6PostgresMarker>()
            .GroupByGroupingSets(x => new { x.Grp, x.Tag }, [0], [1])
            .Select(g => SqlFunctions.Sql.count());

        var equalA = Counts();
        var equalB = Counts();

        Resolve(equalA);
        Resolve(equalB);

        var comparer = equalA.GetQueryPlanEqualityComparer();
        comparer.Equals(equalA, equalB).Should().BeTrue("identical grouping-set lists are the same plan key");
        comparer.GetHashCode(equalA).Should().Be(comparer.GetHashCode(equalB));

        Run(equalA).Should().BeEquivalentTo(new[] { 2, 2, 2, 2 },
            "two groups and two tags over four rows each count two, with no grand-total set");
        Run(equalB).Should().BeEquivalentTo(new[] { 2, 2, 2, 2 },
            "the equal command reads the same live set rows");
    }

    /// <summary>
    /// Distinguishing arm: <c>GroupingSetsEqual</c> one-field negatives — a different set count, a set of
    /// a different arity, a different member index and a swapped set order. Each side resolves, compares
    /// FALSE and reads its own exact live row multiset.
    /// </summary>
    [Fact]
    public void GroupingSets_OneFieldDifferences_ShouldKeySeparately()
    {
        ResetPlanCache();

        QueryCommand<int> Counts(params int[][] sets) => Context.From<Sr6PostgresMarker>()
            .GroupByGroupingSets(x => new { x.Grp, x.Tag }, sets)
            .Select(g => SqlFunctions.Sql.count());

        var byCount = Counts([0], [1]);
        var oneSet = Counts([0]);
        var byArity = Counts([0, 1]);
        var byIndex = Counts([1, 0]);
        var swapped = Counts([1], [0]);

        Resolve(byCount);
        var comparer = byCount.GetQueryPlanEqualityComparer();

        Run(byCount).Should().BeEquivalentTo(new[] { 2, 2, 2, 2 }, "two single-column sets over four rows");
        Run(oneSet).Should().BeEquivalentTo(new[] { 2, 2 }, "one set yields one row per group");
        Run(byArity).Should().BeEquivalentTo(new[] { 2, 2 }, "one two-column set yields one row per pair");
        Run(swapped).Should().BeEquivalentTo(new[] { 2, 2, 2, 2 },
            "the swapped list is a different key but the same live row multiset");

        Resolve(oneSet);
        Resolve(byArity);
        Resolve(byIndex);
        Resolve(swapped);

        comparer.Equals(byCount, oneSet).Should().BeFalse("a different number of grouping sets is a different plan key");
        comparer.Equals(byCount, byArity).Should().BeFalse("a set of a different arity is a different plan key");
        comparer.Equals(byArity, byIndex).Should()
            .BeFalse("a different set member index is a different plan key");
        comparer.Equals(byCount, swapped).Should().BeFalse("the set order is part of the compared list");
    }

    [SqlTable("sr6_postgres_marker")]
    public sealed class Sr6PostgresMarker
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("grp")]
        public int Grp { get; set; }

        [Column("tag")]
        public int Tag { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("score")]
        public int Score { get; set; }
    }

    [SqlTable("sr6_postgres_line")]
    public sealed class Sr6PostgresLine
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("marker_id")]
        public int MarkerId { get; set; }

        [Column("qty")]
        public int Qty { get; set; }
    }
}
