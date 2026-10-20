using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// ClickHouse SQL-rendering branch coverage (cycle 6, cluster D3) executed against a live Testcontainers
/// server. Every fact owns its <c>sr6_clickhouse_*</c> tables (created and seeded in the constructor) and
/// asserts the exact rows the server returns.
///
/// <para>
/// Targeted arms: <c>SqlBuilder.MakeSelect</c>'s PREWHERE clause, the <c>FINAL</c> source modifier on a
/// versioned <c>ReplacingMergeTree</c>, the ARRAY JOIN clause, a multi-key <c>LIMIT BY</c>, a SETTINGS
/// list on a grouped statement with no limiting terminal, and a named window (<c>MakeNamedWindow</c>)
/// with a partition and an ordering.
/// </para>
///
/// <para>
/// Overlap avoided (read and deliberately not repeated): <c>ClickHouse/RenderingBranchCoverage5Tests</c>
/// owns FINAL as a row-count control, WITH TOTALS, the two single-key LimitBy overloads and a plain
/// SETTINGS read; <c>ClickHouse/ClickHouseIntegrationTests</c> owns the array-join expansion controls;
/// <c>ClickHouse/ClickHouseTypedCteIntegrationTests</c> owns the recursive-CTE rejection. This class adds
/// PREWHERE, the versioned-FINAL collapse, the multi-key LIMIT BY, SETTINGS over an aggregate and the
/// named-window composition.
/// </para>
/// </summary>
[Collection("ClickHouse")]
public sealed class ClickHouseRenderingBranchCoverage6Tests : ProviderTestSuite
{
    // The ClickHouse driver rejects multi-statement commands, so each DDL/DML statement is executed
    // on its own connection command.
    private static readonly string[] Ddl =
    [
        "drop table if exists sr6_clickhouse_final",
        "drop table if exists sr6_clickhouse_marker",
        """
        create table sr6_clickhouse_marker
        (
            id Int32,
            grp Int32,
            bucket Int32,
            label String,
            tags Array(String),
            score Int32
        )
        engine = MergeTree order by id
        """,
        """
        insert into sr6_clickhouse_marker (id, grp, bucket, label, tags, score) values
            (1, 1, 10, 'alpha', ['a', 'b'], 5),
            (2, 1, 10, 'beta', ['c'], 7),
            (3, 2, 20, 'gamma', [], 9),
            (4, 2, 20, 'delta', ['d', 'e', 'f'], 11)
        """,
        """
        create table sr6_clickhouse_final
        (
            id Int32,
            ver UInt32,
            label String
        )
        engine = ReplacingMergeTree(ver) order by id
        """,
        "insert into sr6_clickhouse_final (id, ver, label) values (1, 1, 'old'), (2, 1, 'keep')",
        "insert into sr6_clickhouse_final (id, ver, label) values (1, 2, 'new')",
    ];

    public ClickHouseRenderingBranchCoverage6Tests()
    {
        foreach (var statement in Ddl)
            Execute(statement);
    }

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    private void Execute(string sql)
    {
        var context = (DataContext)Context;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Distinguishing arm: the PREWHERE clause before the regular WHERE. Both filters are applied by the
    /// server and the companion without PREWHERE reads the same rows.
    /// </summary>
    [Fact]
    public void PreWhere_ShouldFilterBeforeTheRegularWhere()
    {
        var preWhere = Context.From<Sr6ClickHouseMarker>()
            .PreWhere(x => x.Grp == 1)
            .Where(x => x.Id > 0)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Label })
            .ToList();

        preWhere.Should().HaveCount(2, "only group 1 survives PREWHERE");
        preWhere.Select(r => r.Id).Should().Equal(new[] { 1, 2 });
        preWhere.Select(r => r.Label).Should().Equal("alpha", "beta");

        var plain = Context.From<Sr6ClickHouseMarker>()
            .Where(x => x.Grp == 1)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        plain.Should().Equal(preWhere.Select(r => r.Id), "PREWHERE is a pre-filter, not a different result set");
    }

    /// <summary>
    /// Distinguishing arm: FINAL on a versioned <c>ReplacingMergeTree</c>. The two parts for id 1 collapse
    /// to the highest-version row; the untouched id keeps its only row.
    /// </summary>
    [Fact]
    public void Final_OnVersionedReplacingMergeTree_ShouldKeepTheLatestVersionPerKey()
    {
        var rows = Context.From<Sr6ClickHouseFinal>()
            .Final()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Ver, x.Label })
            .ToList();

        rows.Should().HaveCount(2, "FINAL collapses the two versions of id 1");
        rows.Select(r => r.Id).Should().Equal(new[] { 1, 2 });
        rows.Select(r => r.Ver).Should().Equal(2u, 1u);
        rows.Select(r => r.Label).Should().Equal(new[] { "new", "keep" },
            "the highest-version row wins");
    }

    /// <summary>
    /// Distinguishing arm: the ARRAY JOIN clause bound to the expanded element. Empty arrays drop their
    /// row, and every kept element carries its owning row's id.
    /// </summary>
    [Fact]
    public void ArrayJoin_ShouldExpandElementsAndDropEmptyArrays()
    {
        var rows = Context.From<Sr6ClickHouseMarker>()
            .ArrayJoinElement(x => x.Tags)
            .Select(p => new { Marker = p.Item1.Id, p.Element })
            .ToList()
            .OrderBy(r => r.Marker)
            .ThenBy(r => r.Element, StringComparer.Ordinal)
            .ToList();

        rows.Should().HaveCount(6, "two + one + zero + three elements");
        rows.Select(r => r.Marker).Should().Equal(new[] { 1, 1, 2, 4, 4, 4 });
        rows.Select(r => r.Element).Should().Equal("a", "b", "c", "d", "e", "f");
        rows.Select(r => r.Marker).Should().NotContain(3, "the empty-array row is dropped by ARRAY JOIN");
    }

    /// <summary>
    /// Distinguishing arm: a multi-key LIMIT BY (the cycle-5 class proved the single-key overloads).
    /// One row per (grp, bucket) pair is kept in id order.
    /// </summary>
    [Fact]
    public void LimitBy_MultiKey_ShouldKeepTheFirstRowPerPair()
    {
        var ids = Context.From<Sr6ClickHouseMarker>()
            .OrderBy(x => x.Id)
            .LimitBy(1, x => new { x.Grp, x.Bucket })
            .Select(x => x.Id)
            .ToList();

        ids.Should().Equal(new[] { 1, 3 }, "the first row of each (grp, bucket) pair");
    }

    /// <summary>
    /// Distinguishing arm: a SETTINGS list on a GROUPED statement. The aggregate has no limiting
    /// terminal, so the settings clause is followed only by the group result (the #308 SETTINGS+LIMIT
    /// shape is deliberately not asserted).
    /// </summary>
    [Fact]
    public void Settings_OnGroupedStatement_ShouldKeepTheGroupAggregates()
    {
        var groups = Context.From<Sr6ClickHouseMarker>()
            .Settings(("max_threads", "1"), ("max_block_size", "1000"))
            .GroupBy(x => new { x.Grp })
            .Select(g => new { g.Grp, Count = SqlFunctions.Sql.count() })
            .OrderBy(1, OrderDirection.Asc)
            .ToList();

        groups.Should().HaveCount(2);
        groups.Select(g => g.Grp).Should().Equal(new[] { 1, 2 });
        groups.Select(g => g.Count).Should().Equal(new[] { 2, 2 });
    }

    /// <summary>
    /// Distinguishing arm: a named window (<c>WINDOW w AS (PARTITION BY ... ORDER BY ...)</c>) reused by
    /// one expression. The rank restarts inside each group.
    /// </summary>
    [Fact]
    public void NamedWindow_ShouldRankInsideEachPartition()
    {
        var e = Context.From<Sr6ClickHouseMarker>();

        var rows = e
            .Window(
                "sr6_ch_w",
                partitionBy: [x => x.Grp],
                orderBy: [e.Asc(x => x.Id)])
            .Select(x => new
            {
                x.Id,
                Rank = SqlFunctions.Sql.row_number().Over("sr6_ch_w")
            })
            .OrderBy(x => x.Id)
            .ToList();

        rows.Should().HaveCount(4);
        rows.Select(r => r.Rank).Should().Equal(new[] { 1, 2, 1, 2 },
            "the named window partitions by grp and orders by id");
    }

    [SqlTable("sr6_clickhouse_marker")]
    public sealed class Sr6ClickHouseMarker
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("grp")]
        public int Grp { get; set; }

        [Column("bucket")]
        public int Bucket { get; set; }

        [Column("label")]
        public string Label { get; set; } = string.Empty;

        [Column("tags")]
        public string[] Tags { get; set; } = [];

        [Column("score")]
        public int Score { get; set; }
    }

    [SqlTable("sr6_clickhouse_final")]
    public sealed class Sr6ClickHouseFinal
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("ver")]
        public uint Ver { get; set; }

        [Column("label")]
        public string Label { get; set; } = string.Empty;
    }
}
