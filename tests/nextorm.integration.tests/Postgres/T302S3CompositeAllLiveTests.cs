using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// S3/T302: composite value-key <c>All</c> (<c>ExtremeRowColumns.Length &gt;= 2</c>) on a live SQLite
/// database. Every fact exercises the staged aggregate chain <c>E_1 … E_n</c> joined back to <c>O</c>,
/// so the lexicographic counterexample <c>(9,2)</c> vs <c>(8,100)</c> must win as a whole row and the
/// component-wise combination <c>(9,100)</c> must never be synthesized.
/// </summary>
[Collection("Postgres")]
public sealed class T302S3CompositeAllPostgresTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    private const string TableName = "t302_s3_live_all";

    private const string CreateSql =
        "create table if not exists t302_s3_live_all (id int not null, v1 int null, v2 int null, v3 int null, grp text null, gkey int null, flg int null, payload int null)";

    private const string NonNullableGroupTableName = "t302_s3_live_gnk";

    private const string CreateNonNullableGroupSql =
        "create table if not exists t302_s3_live_gnk (id int not null, v1 int null, v2 int null, gnk int not null, payload int null)";

    private const string ConvertedTableName = "t302_s3_live_conv";

    private const string CreateConvertedSql =
        "create table if not exists t302_s3_live_conv (id int not null, state text not null, sub int not null, grp text not null, payload int null)";

    private const string MappedTableName = "t302_s3_live_map";

    /// <summary>Physical column named exactly like the composite stage-1 alias.</summary>
    private const string CollisionColumn = "\"__nextorm_ext_1_v1\"";

    private const string CreateMappedSql =
        "create table if not exists t302_s3_live_map (id int not null, key_a int null, key_b int null, grp_col text null, \"__nextorm_ext_1_v1\" int null, payload int null)";

    [SqlTable("t302_s3_live_all")]
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

    /// <summary>Positional record projection: the winning row's payload plus its group key.</summary>
    public sealed record WinnerRecord(int Id, string? Grp, int? Payload);

    [SqlTable("t302_s3_live_gnk")]
    public sealed class NonNullableGroupRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("v1")]
        public int? V1 { get; set; }

        [Column("v2")]
        public int? V2 { get; set; }

        [Column("gnk")]
        public int GKey { get; set; }

        [Column("payload")]
        public int? Payload { get; set; }
    }

    [SqlTable("t302_s3_live_map")]
    public sealed class MappedRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("key_a")]
        public int? V1 { get; set; }

        [Column("key_b")]
        public int? V2 { get; set; }

        [Column("grp_col")]
        public string? Grp { get; set; }

        [Column("__nextorm_ext_1_v1")]
        public int? Collide { get; set; }

        [Column("payload")]
        public int? Payload { get; set; }
    }

    public enum ConvertedState
    {
        Alpha,
        Bravo,
        Charlie,
    }

    public enum ConvertedGroup
    {
        Alpha,
        Bravo,
    }

    [SqlTable("t302_s3_live_conv")]
    public sealed class ConvertedRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("state")]
        [ValueConverter(typeof(EnumToStringConverter<ConvertedState>))]
        public ConvertedState State { get; set; }

        [Column("sub")]
        public int Sub { get; set; }

        [Column("grp")]
        [ValueConverter(typeof(EnumToStringConverter<ConvertedGroup>))]
        public ConvertedGroup Grp { get; set; }

        [Column("payload")]
        public int? Payload { get; set; }
    }

    private EntityBuilder<Row> Source() => _sut.DataProvider.From<Row>();

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

    /// <summary>
    /// <c>(9,2)</c> beats <c>(8,100)</c> for <c>Max</c>; every other row is lexicographically smaller
    /// than <c>(9,2)</c>, and the NULL-component rows are excluded.
    /// </summary>
    private void CreateMaxCounterexampleFixture() => Reset(
        "(1, 9, 2, null, 'g', null, null, 900)",
        "(2, 8, 100, null, 'g', null, null, 8100)",
        "(3, 7, 500, null, 'g', null, null, 7500)",
        "(4, 9, 1, null, 'g', null, null, 910)",
        "(5, null, 100, null, 'g', null, null, 5100)",
        "(6, 9, null, null, 'g', null, null, 6900)");

    /// <summary>Symmetric <c>Min</c> fixture: <c>(8,100)</c> beats <c>(9,2)</c>.</summary>
    private void CreateMinCounterexampleFixture() => Reset(
        "(1, 9, 2, null, 'g', null, null, 900)",
        "(2, 8, 100, null, 'g', null, null, 8100)",
        "(3, 9, 50, null, 'g', null, null, 950)",
        "(4, 10, 0, null, 'g', null, null, 1000)",
        "(5, null, 100, null, 'g', null, null, 5100)",
        "(6, 8, null, null, 'g', null, null, 6800)");

    [Fact]
    public void Max_All_Composite2_LexicographicCounterexample_BeatsComponentwise()
    {
        CreateMaxCounterexampleFixture();
        try
        {
            var rows = Source().SelectWhereMax(e => new { e.V1, e.V2 }, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();

            rows.Should().ContainSingle();
            rows[0].Id.Should().Be(1);
            rows[0].V1.Should().Be(9);
            rows[0].V2.Should().Be(2);
            rows[0].Payload.Should().Be(900);

            rows.Should().NotContain(r => r.V1 == 8 && r.V2 == 100,
                "the component-wise runner-up must never win against a lexicographically greater row");
            rows.Should().NotContain(r => r.V1 == 9 && r.V2 == 100,
                "stage 2 must aggregate v2 strictly inside the surviving v1 prefix, so (9,100) cannot be synthesized");
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void Min_All_Composite2_LexicographicCounterexample()
    {
        CreateMinCounterexampleFixture();
        try
        {
            var rows = Source().SelectWhereMin(e => new { e.V1, e.V2 }, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();

            rows.Should().ContainSingle();
            rows[0].Id.Should().Be(2);
            rows[0].V1.Should().Be(8);
            rows[0].V2.Should().Be(100);

            rows.Should().NotContain(r => r.V1 == 9 && r.V2 == 2,
                "(9,2) must never win Min when (8,100) is present in the same population");
            rows.Should().NotContain(r => r.V1 == 8 && r.V2 == 2,
                "independent component-wise minima must never be stitched into a synthetic row");
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void Max_All_Composite3_PrefixThenNextComponent()
    {
        Reset(
            "(1, 9, 3, 0, 'g', null, null, 930)",
            "(2, 9, 2, 9, 'g', null, null, 920)",
            "(3, 8, 100, 100, 'g', null, null, 8100)",
            "(4, 9, 3, null, 'g', null, null, 9300)");
        try
        {
            var rows = Source().SelectWhereMax(e => new { e.V1, e.V2, e.V3 }, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();

            rows.Should().ContainSingle();
            rows[0].Id.Should().Be(1);
            rows[0].V1.Should().Be(9);
            rows[0].V2.Should().Be(3);
            rows[0].V3.Should().Be(0);

            rows.Should().NotContain(r => r.V1 == 9 && r.V2 == 100,
                "no component-wise (9,100,*) combination may appear");
            rows.Should().NotContain(r => r.V1 == 9 && r.V2 == 3 && r.V3 == 100,
                "the third stage aggregates v3 inside the (9,3) prefix, so (9,3,100) cannot be synthesized");
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void Max_All_Composite2_FullKeyTies_KeepAllRowsWithPayload()
    {
        Reset(
            "(1, 9, 2, null, 'g', null, null, 10)",
            "(2, 9, 2, null, 'g', null, null, 20)",
            "(3, 9, 2, null, 'g', null, null, 30)",
            "(4, 9, 1, null, 'g', null, null, 99)",
            "(5, 8, 100, null, 'g', null, null, 5)");
        try
        {
            var rows = Source().SelectWhereMax(e => new { e.V1, e.V2 }, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();

            rows.Select(r => r.Id).Should().ContainInOrder(1, 2, 3);
            rows.Select(r => r.Payload).Should().ContainInOrder(new int?[] { 10, 20, 30 });
            rows.Should().OnlyContain(r => r.V1 == 9 && r.V2 == 2);
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void Max_All_Composite2_NullValueComponents_Excluded()
    {
        Reset(
            "(1, null, 10, null, 'nullonly', null, null, 100)",
            "(2, 9, null, null, 'nullonly', null, null, 101)",
            "(3, null, null, null, 'nullonly', null, null, 102)",
            "(4, 5, 2, null, 'ok', null, null, 40)",
            "(5, 4, 100, null, 'ok', null, null, 50)");
        try
        {
            // Global: every NULL-component row is excluded, so (5,2) beats (4,100) rather than a
            // NULL row leaking in through the aggregate.
            var global = Source().SelectWhereMax(e => new { e.V1, e.V2 }, ExtremeRowTies.All).ToList();
            global.Should().ContainSingle();
            global[0].Id.Should().Be(4);
            global.Should().OnlyContain(r => r.V1 != null && r.V2 != null,
                "a row with a NULL value-key component is never a winner");

            // Grouped: 'nullonly' has no eligible aggregate row at all and is omitted, while 'ok'
            // still returns its winner.
            var grouped = Source().SelectWhereMax(e => new { e.V1, e.V2 }, ExtremeRowTies.All, e => e.Grp)
                .OrderBy(e => e.Id).ToList();
            grouped.Should().ContainSingle();
            grouped[0].Id.Should().Be(4);
            grouped.Select(r => r.Grp).Should().NotContain("nullonly");
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void Max_All_Composite2_Grouped_NullableGroupKey_KeepsNullGroup()
    {
        Reset(
            "(1, 9, 2, null, null, null, null, 10)",
            "(2, 8, 100, null, null, null, null, 20)",
            "(3, 9, 1, null, '', null, null, 30)",
            "(4, 9, 3, null, '', null, null, 40)",
            "(5, 8, 100, null, 'g', null, null, 50)",
            "(6, 9, 2, null, 'g', null, null, 60)");
        try
        {
            var rows = Source().SelectWhereMax(e => new { e.V1, e.V2 }, ExtremeRowTies.All, e => e.Grp)
                .OrderBy(e => e.Id).ToList();

            rows.Should().HaveCount(3);
            rows.Single(r => r.Grp is null).Id.Should().Be(1,
                "the NULL group key must form its own valid group and keep its composite winner");
            rows.Single(r => r.Grp == string.Empty).Id.Should().Be(4,
                "the empty-string group must stay distinct from the NULL group");
            rows.Single(r => r.Grp == "g").Id.Should().Be(6);
            rows.Select(r => r.Grp).Should().BeEquivalentTo(new string?[] { null, string.Empty, "g" });
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void Max_All_Composite2_EmptyInput_ZeroRows()
    {
        CreateMaxCounterexampleFixture();
        try
        {
            Source().Where(e => e.Id == -1)
                .SelectWhereMax(e => new { e.V1, e.V2 }, ExtremeRowTies.All).ToList().Should().BeEmpty();
            Source().Where(e => e.Id == -1)
                .SelectWhereMin(e => new { e.V1, e.V2 }, ExtremeRowTies.All).ToList().Should().BeEmpty();
            Source().Where(e => e.Id == -1)
                .SelectWhereMax(e => new { e.V1, e.V2 }, ExtremeRowTies.All, e => e.Grp).ToList().Should().BeEmpty();
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void Max_All_Composite2_FilterRemovesNaiveWinner()
    {
        Reset(
            "(1, 9, 2, null, 'g', null, 0, 10)",
            "(2, 8, 100, null, 'g', null, 0, 20)",
            "(3, 7, 50, null, 'g', null, 0, 30)",
            "(4, 9, 100, null, 'g', null, 1, 40)",
            "(5, 8, 90, null, 'g', null, 1, 50)");
        try
        {
            int capturedFlag = 0;
            // The unfiltered winner (9,2) is filtered out on every stage and on O, so the population's
            // own winner (8,100) must come back instead of a stale unfiltered aggregate.
            var filtered = Source().Where(e => e.V1 < 9 && e.Flg == capturedFlag)
                .SelectWhereMax(e => new { e.V1, e.V2 }, ExtremeRowTies.All).ToList();
            filtered.Should().ContainSingle();
            filtered[0].Id.Should().Be(2);
            filtered.Should().NotContain(r => r.V1 == 9 && r.V2 == 2);

            // The same form with a different bound value returns the other population's winner.
            capturedFlag = 1;
            var second = Source().Where(e => e.V1 < 9 && e.Flg == capturedFlag)
                .SelectWhereMax(e => new { e.V1, e.V2 }, ExtremeRowTies.All).ToList();
            second.Should().ContainSingle();
            second[0].Id.Should().Be(5);
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void Max_All_Composite2_Projections_Distinct_OrderBy()
    {
        Reset(
            "(1, 9, 2, null, 'b', null, null, 10)",
            "(2, 9, 2, null, 'b', null, null, 10)",
            "(3, 8, 100, null, 'a', null, null, 20)",
            "(4, 7, 1, null, 'c', null, null, 30)");
        try
        {
            var entity = Source().SelectWhereMax(e => new { e.V1, e.V2 }, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            entity.Select(r => r.Id).Should().ContainInOrder(1, 2);

            var dto = Source().SelectWhereMax(e => new { e.V1, e.V2 },
                    e => new Dto { Id = e.Id, Grp = e.Grp, Payload = e.Payload }, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            dto.Select(r => r.Id).Should().ContainInOrder(1, 2);

            var anon = Source().SelectWhereMax(e => new { e.V1, e.V2 },
                    e => new { e.Id, e.Grp, e.Payload }, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            anon.Select(r => r.Id).Should().ContainInOrder(1, 2);

            var records = Source().SelectWhereMax(e => new { e.V1, e.V2 },
                    e => new WinnerRecord(e.Id, e.Grp, e.Payload), ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            records.Should().Equal(
                new WinnerRecord(1, "b", 10),
                new WinnerRecord(2, "b", 10));

            var scalars = Source().SelectWhereMax(e => new { e.V1, e.V2 }, e => e.Payload, ExtremeRowTies.All)
                .ToList();
            scalars.Should().HaveCount(2).And.OnlyContain(v => v == 10);

            var desc = Source().SelectWhereMax(e => new { e.V1, e.V2 }, ExtremeRowTies.All)
                .OrderByDescending(e => e.Id).ToList();
            desc.Select(r => r.Id).Should().ContainInOrder(2, 1);

            // DISTINCT collapses the two exact duplicate winners into one.
            var all = Source().SelectWhereMax(e => new { e.V1, e.V2 },
                    e => new { e.V1, e.V2, e.Payload }, ExtremeRowTies.All).ToList();
            all.Should().HaveCount(2);
            var distinct = Source().SelectWhereMax(e => new { e.V1, e.V2 },
                    e => new { e.V1, e.V2, e.Payload }, ExtremeRowTies.All).Distinct().ToList();
            distinct.Should().ContainSingle()
                .Which.Should().Be(new { V1 = (int?)9, V2 = (int?)2, Payload = (int?)10 });
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void Max_All_Composite2_Arity3_Grouped_CompositeGroupKey()
    {
        Reset(
            "(1, 9, 3, 0, 'a', 1, null, 930)",
            "(2, 9, 2, 9, 'a', 1, null, 920)",
            "(3, 9, 3, 0, 'a', 2, null, 9300)",
            "(4, 9, 3, 1, 'a', 2, null, 9310)",
            "(5, 8, 100, 100, null, 1, null, 8100)",
            "(6, 9, 1, 1, null, 1, null, 910)");
        try
        {
            var rows = Source().SelectWhereMax(e => new { e.V1, e.V2, e.V3 }, ExtremeRowTies.All,
                    e => new { e.Grp, e.GKey })
                .OrderBy(e => e.Id).ToList();

            rows.Select(r => r.Id).Should().ContainInOrder(1, 4, 6);
            rows.Should().OnlyContain(r => r.V1 != null && r.V2 != null && r.V3 != null);
            rows.Select(r => (r.Grp, r.GKey)).Should().BeEquivalentTo(new[]
            {
                ((string?)"a", (int?)1),
                ((string?)"a", (int?)2),
                ((string?)null, (int?)1),
            });
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void Min_All_Composite3_Global_And_GroupedCompositeKey()
    {
        Reset(
            "(1, 7, 3, 9, 'a', 1, null, 739)",
            "(2, 7, 3, 10, 'a', 1, null, 740)",
            "(3, 7, 4, 0, 'a', 1, null, 750)",
            "(4, 8, 0, 0, 'a', 1, null, 800)",
            "(5, 7, 3, null, 'a', 1, null, 700)",
            "(6, 6, 2, 1, 'b', 1, null, 620)",
            "(7, 6, 2, 3, 'b', 1, null, 630)",
            "(8, 6, 3, 0, 'b', 1, null, 640)",
            "(9, null, 1, 1, 'b', 1, null, 510)");
        try
        {
            var global = Source().SelectWhereMin(e => new { e.V1, e.V2, e.V3 }, ExtremeRowTies.All)
                .ToList();
            global.Should().ContainSingle();
            global[0].Id.Should().Be(6, "the lexicographic minimum over the whole population is (6,2,1)");
            global[0].V1.Should().Be(6);
            global[0].V2.Should().Be(2);
            global[0].V3.Should().Be(1);
            global[0].Payload.Should().Be(620);
            global.Should().NotContain(r => r.V1 == 6 && r.V2 == 2 && r.V3 == 0,
                "independent component-wise minima must never be stitched into a synthetic row");
            global.Should().NotContain(r => r.V1 == null || r.V2 == null || r.V3 == null,
                "a NULL value-key component is never a winner");

            var grouped = Source().SelectWhereMin(e => new { e.V1, e.V2, e.V3 }, ExtremeRowTies.All,
                    e => new { e.Grp, e.GKey })
                .OrderBy(e => e.Id).ToList();
            grouped.Select(r => r.Id).Should().ContainInOrder(1, 6);
            grouped.Select(r => (r.Grp, r.GKey)).Should().BeEquivalentTo(new[]
            {
                ((string?)"a", (int?)1),
                ((string?)"b", (int?)1),
            });
            grouped.Select(r => r.Payload).Should().ContainInOrder(new int?[] { 739, 620 });
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void MaxMin_All_Composite2_Grouped_NonNullableGroupKey()
    {
        Execute($"drop table if exists {NonNullableGroupTableName}");
        Execute(CreateNonNullableGroupSql);
        Execute($"insert into {NonNullableGroupTableName} (id, v1, v2, gnk, payload) values " +
                "(1, 9, 2, 1, 10), " +
                "(2, 8, 100, 1, 20), " +
                "(3, 9, 1, 1, 30), " +
                "(4, 9, 2, 2, 40), " +
                "(5, 8, 100, 2, 50), " +
                "(6, 7, 999, 2, 60)");
        try
        {
            var max = _sut.DataProvider.From<NonNullableGroupRow>()
                .SelectWhereMax(e => new { e.V1, e.V2 }, ExtremeRowTies.All, e => e.GKey)
                .OrderBy(e => e.Id).ToList();
            max.Select(r => r.Id).Should().ContainInOrder(1, 4);
            max.Select(r => r.GKey).Should().ContainInOrder(1, 2);
            max.Select(r => r.Payload).Should().ContainInOrder(new int?[] { 10, 40 });

            var min = _sut.DataProvider.From<NonNullableGroupRow>()
                .SelectWhereMin(e => new { e.V1, e.V2 }, ExtremeRowTies.All, e => e.GKey)
                .OrderBy(e => e.Id).ToList();
            min.Select(r => r.Id).Should().ContainInOrder(2, 6);
            min.Select(r => r.GKey).Should().ContainInOrder(1, 2);
            min.Select(r => r.Payload).Should().ContainInOrder(new int?[] { 20, 60 });
        }
        finally { Execute($"drop table if exists {NonNullableGroupTableName}"); }
    }

    [Fact]
    public void Max_All_Composite3_FullKeyTies_PayloadIntegrity()
    {
        Reset(
            "(1, 9, 2, 5, 'g', null, null, 105)",
            "(2, 9, 2, 5, 'g', null, null, 205)",
            "(3, 9, 2, 5, 'g', null, null, null)",
            "(4, 9, 2, 4, 'g', null, null, 999)",
            "(5, 9, 1, 9, 'g', null, null, 888)");
        try
        {
            var rows = Source().SelectWhereMax(e => new { e.V1, e.V2, e.V3 }, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();

            rows.Select(r => r.Id).Should().ContainInOrder(1, 2, 3);
            rows.Select(r => r.Payload).Should().ContainInOrder(new int?[] { 105, 205, null });
            rows.Should().OnlyContain(r => r.V1 == 9 && r.V2 == 2 && r.V3 == 5,
                "every full-key tie is returned and no partial-key row leaks in");
            rows.Single(r => r.Payload is null).Id.Should().Be(3, "the NULL payload survives the join-back unchanged");
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void MaxMin_All_Composite2_ConvertedValueKey_And_ConvertedGroupKey()
    {
        Execute($"drop table if exists {ConvertedTableName}");
        Execute(CreateConvertedSql);
        // Raw physical values: the converter stores each enum as its name, so the DDL carries text.
        Execute($"insert into {ConvertedTableName} (id, state, sub, grp, payload) values " +
                "(1, 'Alpha', 2, 'Alpha', 102), " +
                "(2, 'Bravo', 100, 'Alpha', 2100), " +
                "(3, 'Charlie', 7, 'Alpha', 307), " +
                "(4, 'Charlie', 9, 'Bravo', 409), " +
                "(5, 'Bravo', 100, 'Bravo', 5100), " +
                "(6, 'Alpha', 500, 'Bravo', 1500)");
        try
        {
            // Converted composite value key, global: the stages aggregate the physical text column and
            // the join-back compares the converted key, so Max picks the greatest (state name, sub) pair.
            var max = _sut.DataProvider.From<ConvertedRow>()
                .SelectWhereMax(e => new { e.State, e.Sub }, ExtremeRowTies.All).ToList();
            max.Should().ContainSingle();
            max[0].Id.Should().Be(4);
            max[0].State.Should().Be(ConvertedState.Charlie, "the converted value-key component round-trips");
            max[0].Sub.Should().Be(9);
            max[0].Payload.Should().Be(409);

            var min = _sut.DataProvider.From<ConvertedRow>()
                .SelectWhereMin(e => new { e.State, e.Sub }, ExtremeRowTies.All).ToList();
            min.Should().ContainSingle();
            min[0].Id.Should().Be(1, "Alpha is the smallest converted state name and sub 2 the smallest inside it");
            min[0].State.Should().Be(ConvertedState.Alpha);

            // Converted group key, grouped: each converted group keeps its own converted composite winner.
            var grouped = _sut.DataProvider.From<ConvertedRow>()
                .SelectWhereMax(e => new { e.State, e.Sub }, ExtremeRowTies.All, e => e.Grp)
                .OrderBy(e => e.Id).ToList();
            grouped.Select(r => r.Id).Should().ContainInOrder(3, 4);
            grouped.Select(r => r.Grp).Should().ContainInOrder(ConvertedGroup.Alpha, ConvertedGroup.Bravo);
            grouped.Should().OnlyContain(r => r.State == ConvertedState.Charlie);
            grouped.Select(r => r.Payload).Should().ContainInOrder(new int?[] { 307, 409 });
        }
        finally { Execute($"drop table if exists {ConvertedTableName}"); }
    }

    [Fact]
    public void Max_All_Composite2_MappedColumns_And_StageAliasCollision_GlobalAndGrouped()
    {
        Execute($"drop table if exists {MappedTableName}");
        Execute(CreateMappedSql);
        Execute($"insert into {MappedTableName} (id, key_a, key_b, grp_col, {CollisionColumn}, payload) values " +
                "(1, 9, 2, 'a', 100, 900), " +
                "(2, 8, 100, 'a', 200, 8100), " +
                "(3, 9, 3, 'a', 300, 930), " +
                "(4, 9, 2, 'b', 400, 940), " +
                "(5, 8, 50, 'b', 500, 850)");
        try
        {
            var global = _sut.DataProvider.From<MappedRow>()
                .SelectWhereMax(e => new { e.V1, e.V2 }, ExtremeRowTies.All).ToList();
            global.Should().ContainSingle();
            global[0].Id.Should().Be(3);
            global[0].V1.Should().Be(9);
            global[0].V2.Should().Be(3);
            global[0].Collide.Should().Be(300,
                "the physical column named like the stage alias round-trips independently of the alias");
            global[0].Payload.Should().Be(930);

            var grouped = _sut.DataProvider.From<MappedRow>()
                .SelectWhereMax(e => new { e.V1, e.V2 }, ExtremeRowTies.All, e => e.Grp)
                .OrderBy(e => e.Id).ToList();
            grouped.Select(r => r.Id).Should().ContainInOrder(3, 4);
            grouped.Select(r => r.Grp).Should().ContainInOrder("a", "b");
            grouped.Select(r => r.Collide).Should().ContainInOrder(new int?[] { 300, 400 });
            grouped.Select(r => r.Payload).Should().ContainInOrder(new int?[] { 930, 940 });
        }
        finally { Execute($"drop table if exists {MappedTableName}"); }
    }
}
