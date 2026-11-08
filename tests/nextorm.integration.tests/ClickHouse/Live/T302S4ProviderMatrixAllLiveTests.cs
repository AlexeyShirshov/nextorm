using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// S4/T302 (AC-S4-2, AC-S4-4, VM-S4-03, VM-S4-06, VM-S4-09, VM-S4-10): the <c>ExtremeRowTies.All</c>
/// provider matrix against a live ClickHouse 25.8 server, including the nullable group keys that the
/// S2/S3 SQL-generation step had to defer.
/// <para>
/// ClickHouse rejects <c>isNotDistinctFrom</c>/<c>&lt;=&gt;</c> outside <c>JOIN ON</c>, so the lowering
/// renders the group-key match inside the <c>INNER JOIN ... ON</c> as <c>isNotDistinctFrom</c> (approved
/// plan §7 seam, the <c>#296</c> guard untouched). These facts execute that SQL: the <c>NULL</c> group
/// must be a real group with its own winners, must not leak into the non-<c>NULL</c> groups, and a group
/// whose rows all carry a <c>NULL</c> value key must return no rows.
/// </para>
/// </summary>
[Collection("ClickHouse")]
public sealed class T302S4ProviderMatrixAllClickHouseTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private const string TableName = "t302_s4_live_all";

    private const string CreateSql =
        "create table if not exists t302_s4_live_all (id Int32, v1 Nullable(Int32), v2 Nullable(Int32), " +
        "v3 Nullable(Int32), grp Nullable(String), gkey Nullable(Int32), flg Nullable(Int32), " +
        "payload Nullable(Int32)) engine = Memory";

    private const string TextTableName = "t302_s4_live_text";

    private const string CreateTextSql =
        "create table if not exists t302_s4_live_text (id Int32, val Nullable(String), " +
        "payload Nullable(Int32)) engine = Memory";

    private const string FloatTableName = "t302_s4_live_float";

    private const string CreateFloatSql =
        "create table if not exists t302_s4_live_float (id Int32, val Nullable(Float64), " +
        "payload Nullable(Int32)) engine = Memory";

    [SqlTable("t302_s4_live_all")]
    public sealed class Row
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("v1")]
        public int? V1 { get; set; }

        [Column("v2")]
        public int? V2 { get; set; }

        [Column("v3")]
        public int? V3 { get; set; }

        [Column("grp")]
        public string? Grp { get; set; }

        [Column("gkey")]
        public int? GKey { get; set; }

        [Column("flg")]
        public int? Flg { get; set; }

        [Column("payload")]
        public int? Payload { get; set; }
    }

    public sealed class Dto
    {
        public int Id { get; set; }
        public string? Grp { get; set; }
        public int? Payload { get; set; }
    }

    /// <summary>Positional record projection of a winning row.</summary>
    public sealed record WinnerRecord(int Id, string? Grp, int? Payload);

    [SqlTable("t302_s4_live_text")]
    public sealed class TextRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("val")]
        public string? Val { get; set; }

        [Column("payload")]
        public int? Payload { get; set; }
    }

    [SqlTable("t302_s4_live_float")]
    public sealed class FloatRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("val")]
        public double? Val { get; set; }

        [Column("payload")]
        public int? Payload { get; set; }
    }

    private EntityBuilder<Row> Source() => _sut.DataProvider.From<Row>();

    private EntityBuilder<TextRow> TextSource() => _sut.DataProvider.From<TextRow>();

    private EntityBuilder<FloatRow> FloatSource() => _sut.DataProvider.From<FloatRow>();

    private void Execute(string sql)
    {
        var context = (DataContext)_sut.DataProvider;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private void Reset(params string[] values)
    {
        Execute($"drop table if exists {TableName}");
        Execute(CreateSql);
        Execute($"insert into {TableName} (id, v1, v2, v3, grp, gkey, flg, payload) values " +
                string.Join(", ", values));
    }

    private void DropFixture() => Execute($"drop table if exists {TableName}");

    /// <summary>Three winners tie on the extreme scalar key and one of them carries a NULL payload.</summary>
    [Fact]
    public void MaxMin_All_Global_Scalar_Ties_AndPayloadIntegrity()
    {
        Reset(
            "(1, 9, null, null, 'g', null, null, 900)",
            "(2, 9, null, null, 'g', null, null, null)",
            "(3, 9, null, null, 'g', null, null, 902)",
            "(4, 5, null, null, 'g', null, null, 500)",
            "(5, null, null, null, 'g', null, null, 100)");
        try
        {
            var max = Source().SelectWhereMaxOld(e => e.V1, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            max.Select(r => r.Id).Should().ContainInOrder(1, 2, 3);
            max.Select(r => r.Payload).Should().ContainInOrder(new int?[] { 900, null, 902 });
            max.Should().OnlyContain(r => r.V1 == 9, "the NULL value key is never a winner");

            var min = Source().SelectWhereMinOld(e => e.V1, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            min.Select(r => r.Id).Should().ContainInOrder(4);
            min[0].Payload.Should().Be(500);
        }
        finally { DropFixture(); }
    }

    /// <summary>
    /// Grouped scalar <c>Max</c> on the nullable <c>grp</c> column: this is the shape that made ClickHouse
    /// refuse the query before this step. The NULL group must be a real group with its own winner, stay
    /// distinct from the empty-string group, and a group whose rows all carry a NULL value key must be
    /// omitted.
    /// </summary>
    [Fact]
    public void Max_All_Grouped_Scalar_NullableGroupKey_KeepsNullGroup()
    {
        Reset(
            "(1, 9, null, null, null, null, null, 10)",
            "(2, 5, null, null, null, null, null, 20)",
            "(3, 9, null, null, '', null, null, 30)",
            "(4, 7, null, null, '', null, null, 40)",
            "(5, null, null, null, 'a', null, null, 50)",
            "(6, 9, null, null, 'a', null, null, 60)",
            "(7, 4, null, null, 'a', null, null, 70)");
        try
        {
            var rows = Source().SelectWhereMaxOld(e => e.V1, ExtremeRowTies.All, e => e.Grp)
                .OrderBy(e => e.Id).ToList();

            rows.Select(r => r.Id).Should().ContainInOrder(1, 3, 6);
            rows.Select(r => r.Grp).Should().BeEquivalentTo(new string?[] { null, string.Empty, "a" });
            rows.Select(r => r.Payload).Should().ContainInOrder(new int?[] { 10, 30, 60 });
            rows.Should().OnlyContain(r => r.V1 == 9);
        }
        finally { DropFixture(); }
    }

    /// <summary>
    /// The lexicographic counterexample: <c>(9,2)</c> beats <c>(8,100)</c> for <c>Max</c>, <c>(8,100)</c>
    /// beats <c>(9,2)</c> for <c>Min</c>, and the component-wise mix <c>(9,100)</c> is never synthesized.
    /// </summary>
    [Fact]
    public void MaxMin_All_Global_Composite2_LexicographicCounterexample()
    {
        Reset(
            "(1, 9, 2, null, 'g', null, null, 900)",
            "(2, 8, 100, null, 'g', null, null, 8100)",
            "(3, 7, 500, null, 'g', null, null, 7500)",
            "(4, 9, 1, null, 'g', null, null, 910)",
            "(5, null, 100, null, 'g', null, null, 5100)",
            "(6, 9, null, null, 'g', null, null, 6900)");
        try
        {
            var max = Source().SelectWhereMaxOld(e => new { e.V1, e.V2 }, ExtremeRowTies.All).ToList();
            max.Should().ContainSingle();
            max[0].Id.Should().Be(1);
            max[0].Payload.Should().Be(900);
            max.Should().NotContain(r => r.V1 == 8 && r.V2 == 100,
                "the component-wise runner-up must never win against a lexicographically greater row");
            max.Should().NotContain(r => r.V1 == 9 && r.V2 == 100,
                "stage 2 aggregates inside the surviving v1 prefix, so (9,100) cannot be synthesized");
        }
        finally { DropFixture(); }

        Reset(
            "(1, 9, 2, null, 'g', null, null, 900)",
            "(2, 8, 100, null, 'g', null, null, 8100)",
            "(3, 9, 50, null, 'g', null, null, 950)",
            "(4, 10, 0, null, 'g', null, null, 1000)",
            "(5, null, 100, null, 'g', null, null, 5100)",
            "(6, 8, null, null, 'g', null, null, 6800)");
        try
        {
            var min = Source().SelectWhereMinOld(e => new { e.V1, e.V2 }, ExtremeRowTies.All).ToList();
            min.Should().ContainSingle();
            min[0].Id.Should().Be(2);
            min.Should().NotContain(r => r.V1 == 9 && r.V2 == 2);
            min.Should().NotContain(r => r.V1 == 8 && r.V2 == 2,
                "independent component-wise minima must never be stitched into a synthetic row");
        }
        finally { DropFixture(); }
    }

    /// <summary>
    /// Grouped composite <c>All</c> over a composite group key whose both components are nullable (the
    /// S2/S3 deferred shape): every <c>(NULL, …)</c> and <c>(…, NULL)</c> group keeps its own composite
    /// winner without cross-contaminating the non-NULL groups.
    /// </summary>
    [Fact]
    public void Max_All_Grouped_Composite2_NullableCompositeGroupKey()
    {
        Reset(
            "(1, 9, 2, null, null, null, null, 10)",
            "(2, 8, 100, null, null, null, null, 20)",
            "(3, 9, 4, null, 'a', null, null, 30)",
            "(4, 9, 1, null, 'a', null, null, 40)",
            "(5, 9, 3, null, 'a', 7, null, 50)",
            "(6, 9, 3, null, 'a', 8, null, 60)");
        try
        {
            var rows = Source()
                .SelectWhereMaxOld(e => new { e.V1, e.V2 }, ExtremeRowTies.All, e => new { e.Grp, e.GKey })
                .OrderBy(e => e.Id).ToList();

            rows.Select(r => r.Id).Should().ContainInOrder(1, 3, 5, 6);
            rows.Select(r => (r.Grp, r.GKey)).Should().BeEquivalentTo(new[]
            {
                ((string?)null, (int?)null),
                ((string?)"a", (int?)null),
                ((string?)"a", (int?)7),
                ((string?)"a", (int?)8),
            });
        }
        finally { DropFixture(); }
    }

    /// <summary>
    /// A row with any NULL value-key component is never a winner, and a group whose rows all have a NULL
    /// value key returns no rows at all instead of a fabricated aggregate row.
    /// </summary>
    [Fact]
    public void Max_All_Composite2_NullValueComponentsExcluded_AllNullGroupZeroRows()
    {
        Reset(
            "(1, null, 10, null, 'nullonly', null, null, 100)",
            "(2, 9, null, null, 'nullonly', null, null, 101)",
            "(3, null, null, null, 'nullonly', null, null, 102)",
            "(4, 5, 2, null, 'ok', null, null, 40)",
            "(5, 4, 100, null, 'ok', null, null, 50)");
        try
        {
            var global = Source().SelectWhereMaxOld(e => new { e.V1, e.V2 }, ExtremeRowTies.All).ToList();
            global.Should().ContainSingle();
            global[0].Id.Should().Be(4);
            global.Should().OnlyContain(r => r.V1 != null && r.V2 != null);

            var grouped = Source()
                .SelectWhereMaxOld(e => new { e.V1, e.V2 }, ExtremeRowTies.All, e => e.Grp)
                .OrderBy(e => e.Id).ToList();
            grouped.Should().ContainSingle();
            grouped[0].Grp.Should().Be("ok");
            grouped.Select(r => r.Grp).Should().NotContain("nullonly",
                "a group without any eligible aggregate row must be omitted, not returned with an empty key");
        }
        finally { DropFixture(); }
    }

    /// <summary>
    /// The table-wide winner is filtered out on every stage and on the join-back, so the surviving
    /// population's own winner must come back; the same command re-bound to another captured value must
    /// return the other population's winner instead of a stale aggregate.
    /// </summary>
    [Fact]
    public void Max_All_Composite2_FilterRemovesNaiveWinner_ParametersDeterministic()
    {
        Reset(
            "(1, 9, 2, null, 'g', null, 0, 10)",
            "(2, 8, 100, null, 'g', null, 0, 20)",
            "(3, 7, 50, null, 'g', null, 0, 30)",
            "(4, 9, 100, null, 'g', null, 1, 40)",
            "(5, 8, 90, null, 'g', null, 1, 50)");
        try
        {
            var capturedFlag = 0;
            var first = Source().Where(e => e.V1 < 9 && e.Flg == capturedFlag)
                .SelectWhereMaxOld(e => new { e.V1, e.V2 }, ExtremeRowTies.All).ToList();
            first.Should().ContainSingle();
            first[0].Id.Should().Be(2);
            first.Should().NotContain(r => r.V1 == 9 && r.V2 == 2);

            capturedFlag = 1;
            var second = Source().Where(e => e.V1 < 9 && e.Flg == capturedFlag)
                .SelectWhereMaxOld(e => new { e.V1, e.V2 }, ExtremeRowTies.All).ToList();
            second.Should().ContainSingle();
            second[0].Id.Should().Be(5);

            // The same shape bound back to the first value must not reuse the second value's aggregate.
            capturedFlag = 0;
            var again = Source().Where(e => e.V1 < 9 && e.Flg == capturedFlag)
                .SelectWhereMaxOld(e => new { e.V1, e.V2 }, ExtremeRowTies.All).ToList();
            again.Select(r => r.Id).Should().Equal(first.Select(r => r.Id),
                "the captured value is re-bound on every execution, so the plan cannot leak a stale winner");
        }
        finally { DropFixture(); }
    }

    /// <summary>
    /// Projection shapes (entity / DTO / anonymous / record / scalar), a NULL payload that survives the
    /// join-back untouched, outer <c>Distinct()</c>, and deterministic <c>OrderBy</c>/<c>ThenBy</c>.
    /// </summary>
    [Fact]
    public void Max_All_Composite2_Projections_NullablePayload_Distinct_OrderByThenBy()
    {
        Reset(
            "(1, 9, 2, null, 'b', null, null, 10)",
            "(2, 9, 2, null, 'b', null, null, null)",
            "(3, 8, 100, null, 'a', null, null, 20)",
            "(4, 7, 1, null, 'c', null, null, 30)");
        try
        {
            var entity = Source().SelectWhereMaxOld(e => new { e.V1, e.V2 }, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            entity.Select(r => r.Id).Should().ContainInOrder(1, 2);
            entity.Single(r => r.Payload is null).Id.Should().Be(2,
                "the NULL payload survives the join-back unchanged");

            var dto = Source().SelectWhereMaxOld(e => new { e.V1, e.V2 },
                    e => new Dto { Id = e.Id, Grp = e.Grp, Payload = e.Payload }, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            dto.Select(r => r.Id).Should().ContainInOrder(1, 2);

            var anon = Source().SelectWhereMaxOld(e => new { e.V1, e.V2 },
                    e => new { e.Id, e.Grp, e.Payload }, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            anon.Select(r => r.Id).Should().ContainInOrder(1, 2);

            var records = Source().SelectWhereMaxOld(e => new { e.V1, e.V2 },
                    e => new WinnerRecord(e.Id, e.Grp, e.Payload), ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            records.Should().Equal(new WinnerRecord(1, "b", 10), new WinnerRecord(2, "b", null));

            var scalars = Source().SelectWhereMaxOld(e => new { e.V1, e.V2 }, e => e.Payload, ExtremeRowTies.All)
                .ToList();
            scalars.Should().HaveCount(2).And.Contain(new int?[] { 10, null });

            var ordered = Source().SelectWhereMaxOld(e => new { e.V1, e.V2 }, ExtremeRowTies.All)
                .OrderBy(e => e.Grp).ThenBy(e => e.Id).ToList();
            ordered.Select(r => r.Id).Should().ContainInOrder(1, 2);

            var distinct = Source().SelectWhereMaxOld(e => new { e.V1, e.V2 },
                    e => new { e.V1, e.V2, e.Grp }, ExtremeRowTies.All).Distinct().ToList();
            distinct.Should().ContainSingle()
                .Which.Should().Be(new { V1 = (int?)9, V2 = (int?)2, Grp = (string?)"b" });
        }
        finally { DropFixture(); }
    }

    /// <summary>
    /// Collation parity (VM-S4-09): ClickHouse compares <c>String</c> byte-wise, so <c>'a'</c> is strictly
    /// greater than <c>'A'</c> and exactly one candidate wins; the <c>All</c> winner set must still equal
    /// the raw <c>rank() = 1</c> set over the same live table.
    /// </summary>
    [Fact]
    public void MaxMin_All_Collation_WinnerSetEqualsRankOracle()
    {
        Execute($"drop table if exists {TextTableName}");
        Execute(CreateTextSql);
        Execute($"insert into {TextTableName} (id, val, payload) values " +
                "(1, 'a', 10), (2, 'A', 20), (3, '0', 30)");
        try
        {
            var max = TextSource().SelectWhereMaxOld(e => e.Val, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            var maxOracle = T302S4ParityOracle.RankedWinnerIds(
                _sut.DataProvider, TextTableName, "val", descending: true);

            max.Select(r => r.Id).Should().Equal(maxOracle,
                "All must return exactly the rank() = 1 winners of the same population");
            max.Should().NotBeEmpty();
            max.Should().ContainSingle("ClickHouse String comparison is byte-wise, so 'a' and 'A' are distinct");
            max[0].Val.Should().Be("a");

            var min = TextSource().SelectWhereMinOld(e => e.Val, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            var minOracle = T302S4ParityOracle.RankedWinnerIds(
                _sut.DataProvider, TextTableName, "val", descending: false);
            min.Select(r => r.Id).Should().Equal(minOracle);
            min.Select(r => r.Id).Should().ContainInOrder(3);
        }
        finally { Execute($"drop table if exists {TextTableName}"); }
    }

    /// <summary>
    /// Finite floating-point parity (VM-S4-10): a unique maximum and a tied minimum must both match the
    /// raw <c>rank() = 1</c> winner set, and the NULL value key must stay out of the population.
    /// </summary>
    [Fact]
    public void MaxMin_All_FloatingPoint_Finite_WinnerSetEqualsRankOracle()
    {
        Execute($"drop table if exists {FloatTableName}");
        Execute(CreateFloatSql);
        Execute($"insert into {FloatTableName} (id, val, payload) values " +
                "(1, 1.5, 10), (2, 1.5, 20), (3, 2.25, 30), (4, 3.5, 40), (5, null, 50)");
        try
        {
            var max = FloatSource().SelectWhereMaxOld(e => e.Val, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            var maxOracle = T302S4ParityOracle.RankedWinnerIds(
                _sut.DataProvider, FloatTableName, "val", descending: true);
            max.Select(r => r.Id).Should().Equal(maxOracle);
            max.Select(r => r.Id).Should().ContainInOrder(4);

            var min = FloatSource().SelectWhereMinOld(e => e.Val, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            var minOracle = T302S4ParityOracle.RankedWinnerIds(
                _sut.DataProvider, FloatTableName, "val", descending: false);
            min.Select(r => r.Id).Should().Equal(minOracle);
            min.Select(r => r.Id).Should().ContainInOrder(1, 2);
        }
        finally { Execute($"drop table if exists {FloatTableName}"); }
    }

    /// <summary>
    /// Non-finite parity (VM-S4-10): ClickHouse <c>Float64</c> stores ±Infinity and <c>NaN</c>, so their
    /// winner sets must equal the raw <c>rank() = 1</c> oracle. The two populations are checked separately
    /// so a divergence is attributable: ±Infinity first, then the same table plus a <c>NaN</c> row.
    /// </summary>
    [Fact]
    public void MaxMin_All_FloatingPoint_NonFinite_WinnerSetEqualsRankOracle()
    {
        Execute($"drop table if exists {FloatTableName}");
        Execute(CreateFloatSql);
        Execute($"insert into {FloatTableName} (id, val, payload) values " +
                "(1, 1.5, 10), (2, -2.5, 20), (3, inf, 30), (4, -inf, 40), (5, null, 50)");
        try
        {
            var max = FloatSource().SelectWhereMaxOld(e => e.Val, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            var maxOracle = T302S4ParityOracle.RankedWinnerIds(
                _sut.DataProvider, FloatTableName, "val", descending: true);
            max.Select(r => r.Id).Should().Equal(maxOracle);
            max.Select(r => r.Id).Should().ContainInOrder(3);

            var min = FloatSource().SelectWhereMinOld(e => e.Val, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            var minOracle = T302S4ParityOracle.RankedWinnerIds(
                _sut.DataProvider, FloatTableName, "val", descending: false);
            min.Select(r => r.Id).Should().Equal(minOracle);
            min.Select(r => r.Id).Should().ContainInOrder(4);

            Execute($"insert into {FloatTableName} (id, val, payload) values (6, nan, 60)");

            var nanMax = FloatSource().SelectWhereMaxOld(e => e.Val, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            var nanMaxOracle = T302S4ParityOracle.RankedWinnerIds(
                _sut.DataProvider, FloatTableName, "val", descending: true);
            nanMax.Select(r => r.Id).Should().Equal(nanMaxOracle);

            var nanMin = FloatSource().SelectWhereMinOld(e => e.Val, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            var nanMinOracle = T302S4ParityOracle.RankedWinnerIds(
                _sut.DataProvider, FloatTableName, "val", descending: false);
            nanMin.Select(r => r.Id).Should().Equal(nanMinOracle);
        }
        finally { Execute($"drop table if exists {FloatTableName}"); }
    }
}
