using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

[Collection("Postgres")]
public sealed class T302S2GroupedScalarAllPostgresTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    private const string TableName = "t302_s2_live_group";

    private const string CreateSql =
        "create table if not exists t302_s2_live_group (id int not null, score int null, grp text null, flag int null, payload int null)";

    [SqlTable("t302_s2_live_group")]
    public sealed class Row
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("score")]
        public int? Score { get; set; }

        [Column("grp")]
        public string? Grp { get; set; }

        [Column("flag")]
        public int? Flag { get; set; }

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

    private EntityBuilder<Row> Source() => _sut.DataProvider.From<Row>();

    private void Execute(string sql)
    {
        var context = (DataContext)_sut.DataProvider;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private void CreateFixture()
    {
        Execute($"drop table if exists {TableName}");
        Execute(CreateSql);
        Execute($"insert into {TableName} (id, score, grp, flag, payload) values " +
                "(1, 9, 'g1', 1, 90), " +
                "(2, 9, 'g1', 1, 91), " +
                "(3, 5, 'g1', 1, 50), " +
                "(4, 9, 'g2', 2, 92), " +
                "(5, 1, 'g2', 2, 10), " +
                "(6, null, 'g2', 2, 60), " +
                "(7, 9, null, null, 93), " +
                "(8, 7, null, null, 70), " +
                "(9, null, null, null, 90), " +
                "(10, 3, 'g3', 1, null), " +
                "(11, 3, 'g3', 1, 31)");
    }

    /// <summary>
    /// Composite group-key fixture: covers a NULL component on either side and a fully NULL key -
    /// (<c>('a', 1)</c>, <c>('b', NULL)</c>, <c>(NULL, 9)</c>, <c>(NULL, NULL)</c>, <c>('c', 2)</c>)
    /// plus one group whose only NULL-value row must be ignored (<c>('c', 2)</c>).
    /// </summary>
    private void CreateCompositeFixture()
    {
        Execute($"drop table if exists {TableName}");
        Execute(CreateSql);
        Execute($"insert into {TableName} (id, score, grp, flag, payload) values " +
                "(1, 9, 'a', 1, 100), " +
                "(2, 9, 'a', 1, 101), " +
                "(3, 5, 'a', 1, 50), " +
                "(4, 8, 'b', null, 80), " +
                "(5, 8, 'b', null, 81), " +
                "(6, 6, null, 9, 60), " +
                "(7, 6, null, 9, 61), " +
                "(8, 7, null, null, 70), " +
                "(9, 4, null, null, 71), " +
                "(10, null, 'c', 2, 90), " +
                "(11, 3, 'c', 2, 30)");
    }

    private void CreateDuplicateFixture()
    {
        Execute($"drop table if exists {TableName}");
        Execute(CreateSql);
        Execute($"insert into {TableName} (id, score, grp, flag, payload) values " +
                "(1, 9, 'g1', 1, 90), " +
                "(1, 9, 'g1', 1, 90), " +
                "(2, 9, 'g1', 1, 91), " +
                "(3, 1, 'g1', 1, 10)");
    }

    /// <summary>
    /// One group whose every value key is NULL (<c>'y'</c>) beside a group that has a winner
    /// (<c>'x'</c>): the all-NULL group produces no aggregate row, so it must be omitted entirely
    /// while the other groups still return their winners.
    /// </summary>
    private void CreateAllNullValueGroupFixture()
    {
        Execute($"drop table if exists {TableName}");
        Execute(CreateSql);
        Execute($"insert into {TableName} (id, score, grp, flag, payload) values " +
                "(1, 9, 'x', 1, 90), " +
                "(2, null, 'y', 1, 50), " +
                "(3, null, 'y', 1, 51), " +
                "(4, 5, 'x', 1, 40)");
    }

    private void CreateAllNullValuesFixture()
    {
        Execute($"drop table if exists {TableName}");
        Execute(CreateSql);
        Execute($"insert into {TableName} (id, score, grp, flag, payload) values " +
                "(1, null, 'x', 1, 10), " +
                "(2, null, 'y', 1, 20)");
    }

    private void DropFixture() => Execute($"drop table if exists {TableName}");

    [Fact]
    public void Max_All_Grouped_NullableKey_GroupsTiesAndNullKeys()
    {
        CreateFixture();
        try
        {
            // g1: ids 1,2 (9); g2: id 4 (9, NULL value ignored for id 6); NULL key: id 7 (9,
            // all-NULL group member id 9 dropped); g3: ids 10,11 (3, NULL payload winner).
            var rows = Source().SelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Grp)
                .OrderBy(e => e.Id).ToList();

            rows.Should().HaveCount(6);
            rows.Select(r => r.Id).Should().ContainInOrder(1, 2, 4, 7, 10, 11);
            rows.Select(r => r.Score).Should().ContainInOrder(new int?[] { 9, 9, 9, 9, 3, 3 });
            rows.Select(r => r.Payload).Should().ContainInOrder(new int?[] { 90, 91, 92, 93, null, 31 });
            rows.Select(r => r.Id).Should().NotContain(9);

            // Projection forms keep the same grouped winners.
            var dtos = Source().SelectWhereMax(e => e.Score,
                    e => new Dto { Id = e.Id, Grp = e.Grp, Payload = e.Payload },
                    ExtremeRowTies.All, e => e.Grp)
                .OrderBy(e => e.Id).ToList();
            dtos.Should().HaveCount(6);
            dtos.Select(r => r.Id).Should().ContainInOrder(1, 2, 4, 7, 10, 11);

            var anon = Source().SelectWhereMax(e => e.Score,
                    e => new { e.Id, e.Grp, e.Payload },
                    ExtremeRowTies.All, e => e.Grp)
                .OrderBy(e => e.Id).ToList();
            anon.Should().HaveCount(6);
            anon.Select(r => r.Id).Should().ContainInOrder(1, 2, 4, 7, 10, 11);

            var scalars = Source().SelectWhereMax(e => e.Score, e => e.Id, ExtremeRowTies.All, e => e.Grp)
                .ToList();
            scalars.Should().BeEquivalentTo(new[] { 1, 2, 4, 7, 10, 11 });
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void Min_All_Grouped_NullableKey_GroupsTiesAndNullKeys()
    {
        CreateFixture();
        try
        {
            var rows = Source().SelectWhereMin(e => e.Score, ExtremeRowTies.All, e => e.Grp)
                .OrderBy(e => e.Id).ToList();

            rows.Should().HaveCount(5);
            rows.Select(r => r.Id).Should().ContainInOrder(3, 5, 8, 10, 11);
            rows.Select(r => r.Score).Should().ContainInOrder(new int?[] { 5, 1, 7, 3, 3 });
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void All_Grouped_AllNullValueGroup_Dropped()
    {
        CreateFixture();
        try
        {
            // In the NULL-key group only ids 7 (9) and 8 (7) have a non-null value; id 9 is a NULL
            // value row and must not create a winner of its own.
            var rows = Source().Where(e => e.Grp == null)
                .SelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Grp).ToList();
            rows.Should().HaveCount(1);
            rows[0].Id.Should().Be(7);

            // Filtering to rows whose value key is NULL leaves no eligible row for that group.
            var none = Source().Where(e => e.Grp == null && e.Score == null)
                .SelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Grp).ToList();
            none.Should().BeEmpty();
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void All_Grouped_EmptyInput_ZeroRows()
    {
        CreateFixture();
        try
        {
            var rows = Source().Where(e => e.Id == -1)
                .SelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Grp).ToList();
            rows.Should().BeEmpty();
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void All_Grouped_FilterAppliesToBothSides()
    {
        CreateFixture();
        try
        {
            // The table-wide winner (9) is filtered out on both the aggregate and the payload side:
            // every group falls back to its best row inside the filtered population.
            var rows = Source().Where(e => e.Score < 9)
                .SelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Grp)
                .OrderBy(e => e.Id).ToList();

            rows.Should().HaveCount(5);
            rows.Select(r => r.Id).Should().ContainInOrder(3, 5, 8, 10, 11);
            rows.Should().OnlyContain(r => r.Score != 9);
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void Max_All_Grouped_CompositeKey_NullCombinations()
    {
        CreateCompositeFixture();
        try
        {
            var rows = Source().SelectWhereMax(e => e.Score, ExtremeRowTies.All, e => new { e.Grp, e.Flag })
                .OrderBy(e => e.Id).ToList();

            // ('a',1): 1,2; ('b',NULL): 4,5; (NULL,9): 6,7; (NULL,NULL): 8; ('c',2): 11 (NULL value ignored).
            rows.Should().HaveCount(8);
            rows.Select(r => r.Id).Should().ContainInOrder(1, 2, 4, 5, 6, 7, 8, 11);
            rows.Select(r => (r.Grp, r.Flag)).Distinct().Should().BeEquivalentTo(new[]
            {
                ((string?)"a", (int?)1),
                ((string?)"b", (int?)null),
                ((string?)null, (int?)9),
                ((string?)null, (int?)null),
                ((string?)"c", (int?)2),
            });
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void All_Grouped_Distinct_CollapsesExactDuplicates()
    {
        CreateDuplicateFixture();
        try
        {
            var all = Source().SelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Grp).ToList();
            all.Should().HaveCount(3);

            var distinct = Source().SelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Grp)
                .Distinct().ToList();
            distinct.Should().HaveCount(2);
            distinct.Should().OnlyContain(r => r.Score == 9);
            distinct.Select(r => r.Id).Should().BeEquivalentTo(new[] { 1, 2 });
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void All_Grouped_AllNullValueGroup_Omitted_OtherGroupsReturnWinners()
    {
        CreateAllNullValueGroupFixture();
        try
        {
            // 'y' has only NULL value keys, so its aggregate is empty; 'x' keeps its winner (id 1).
            var max = Source().SelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Grp)
                .OrderBy(e => e.Id).ToList();
            max.Should().HaveCount(1);
            max[0].Id.Should().Be(1);
            max.Should().OnlyContain(r => r.Grp == "x");
            max.Select(r => r.Grp).Should().NotContain("y");

            var min = Source().SelectWhereMin(e => e.Score, ExtremeRowTies.All, e => e.Grp)
                .OrderBy(e => e.Id).ToList();
            min.Should().HaveCount(1);
            min[0].Id.Should().Be(4);
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void All_Grouped_AllNullValues_ZeroRows()
    {
        CreateAllNullValuesFixture();
        try
        {
            // Every value key in every group is NULL: no group survives the non-null aggregate guard.
            Source().SelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Grp).ToList().Should().BeEmpty();
            Source().SelectWhereMin(e => e.Score, ExtremeRowTies.All, e => e.Grp).ToList().Should().BeEmpty();
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void All_Grouped_NullablePayload_OrderByOuterColumn()
    {
        CreateFixture();
        try
        {
            var rows = Source().SelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Grp)
                .OrderBy(e => e.Id).ToList();
            rows.Select(r => r.Payload).Should().ContainInOrder(new int?[] { 90, 91, 92, 93, null, 31 });
            rows.Single(r => r.Id == 10).Payload.Should().BeNull(
                "the projected NULL payload must survive the grouped aggregate join-back");
            rows.Single(r => r.Id == 11).Payload.Should().Be(31);

            var desc = Source().SelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Grp)
                .OrderByDescending(e => e.Id).ToList();
            desc.Select(r => r.Id).Should().ContainInOrder(11, 10, 7, 4, 2, 1);
            desc.Select(r => r.Payload).Should().ContainInOrder(new int?[] { 31, null, 93, 92, 91, 90 });
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void Max_All_Grouped_RecordProjection_KeepsWinnerPayloadAndKey()
    {
        CreateFixture();
        try
        {
            // Positional record projection over the grouped aggregate: each winner's payload and its
            // group key are materialized positionally, including the NULL payload of id 10 and the
            // NULL group key of id 7.
            var records = Source().SelectWhereMax(e => e.Score,
                    e => new WinnerRecord(e.Id, e.Grp, e.Payload),
                    ExtremeRowTies.All, e => e.Grp)
                .OrderBy(e => e.Id).ToList();

            records.Should().Equal(
                new WinnerRecord(1, "g1", 90),
                new WinnerRecord(2, "g1", 91),
                new WinnerRecord(4, "g2", 92),
                new WinnerRecord(7, null, 93),
                new WinnerRecord(10, "g3", null),
                new WinnerRecord(11, "g3", 31));

            records.Single(r => r.Id == 10).Payload.Should().BeNull(
                "the record's positional NULL payload must survive the grouped aggregate join-back");
            records.Single(r => r.Id == 7).Grp.Should().BeNull(
                "the NULL group key must project into the record's key component");
        }
        finally { DropFixture(); }
    }
}
