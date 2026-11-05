using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

[Collection("SqlServer")]
public sealed class T302S1GlobalScalarAllSqlServerTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    private const string TableName = "t302_s1_live_all";

    private const string CreateSql =
        "create table t302_s1_live_all (id int not null, score int null, label nvarchar(50) not null, payload int null, flag int not null)";

    [SqlTable("t302_s1_live_all")]
    public sealed class Row
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("score")]
        public int? Score { get; set; }

        [Column("label")]
        public string Label { get; set; } = string.Empty;

        [Column("payload")]
        public int? Payload { get; set; }

        [Column("flag")]
        public int Flag { get; set; }
    }

    public sealed class Dto
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public int? Payload { get; set; }
    }

    private EntityBuilder<Row> Source() => _sut.DataProvider.From<Row>();

    private void CreateFixture()
    {
        Execute($"drop table if exists {TableName}");
        Execute(CreateSql);
        Execute($"insert into {TableName} (id, score, label, payload, flag) values " +
                "(1, null, 'nullrow', null, 1), " +
                "(2, 5, 'five-a', 50, 1), " +
                "(3, 9, 'nine-a', 90, 1), " +
                "(3, 9, 'nine-a', 90, 1), " +
                "(4, 9, 'nine-b', 91, 1), " +
                "(5, 5, 'five-b', 51, 1), " +
                "(6, 1, 'one-a', 10, 1), " +
                "(7, 1, 'one-b', 11, 1)");
    }

    /// <summary>
    /// Dedicated fixture for the nullable-payload/ORDER BY fact: exactly three winners tie on the
    /// extreme key (score = 9) and one of them carries a real NULL in the projected nullable payload
    /// column while the other two carry distinct non-null values. The external <c>Id</c> column is
    /// non-null and unique, so the winner sequence is provider-independent (no NULLS FIRST/LAST
    /// assumption across providers).
    /// </summary>
    private void CreatePayloadFixture()
    {
        Execute($"drop table if exists {TableName}");
        Execute(CreateSql);
        Execute($"insert into {TableName} (id, score, label, payload, flag) values " +
                "(1, null, 'nullrow', null, 1), " +
                "(2, 5, 'five-a', 50, 1), " +
                "(3, 9, 'nine-a', 90, 1), " +
                "(4, 9, 'nine-b', 91, 1), " +
                "(6, 1, 'one-a', 10, 1), " +
                "(8, 9, 'nine-c', null, 1)");
    }

    private void DropFixture() => Execute($"drop table if exists {TableName}");

    private void Execute(string sql)
    {
        var context = (DataContext)_sut.DataProvider;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    [Fact]
    public void Max_All_Global_MultipleTies_PayloadIntegrity()
    {
        CreateFixture();
        try
        {
            var rows = Source().SelectWhereMaxOld(e => e.Score, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();

            rows.Should().HaveCount(3);
            rows.Should().OnlyContain(r => r.Score == 9);
            rows.Select(r => (r.Id, r.Label, r.Payload))
                .Should().BeEquivalentTo(new[] { (3, "nine-a", (int?)90), (3, "nine-a", (int?)90), (4, "nine-b", (int?)91) });
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void Min_All_Global_MultipleTies_PayloadIntegrity()
    {
        CreateFixture();
        try
        {
            var rows = Source().SelectWhereMinOld(e => e.Score, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();

            rows.Should().HaveCount(2);
            rows.Should().OnlyContain(r => r.Score == 1);
            rows.Select(r => r.Id).Should().BeEquivalentTo(new[] { 6, 7 });
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void All_Global_Distinct_CollapsesExactDuplicates()
    {
        CreateFixture();
        try
        {
            var all = Source().SelectWhereMaxOld(e => e.Score, ExtremeRowTies.All).ToList();
            all.Should().HaveCount(3);

            var distinct = Source().SelectWhereMaxOld(e => e.Score, ExtremeRowTies.All).Distinct().ToList();
            distinct.Should().HaveCount(2);
            distinct.Should().OnlyContain(r => r.Score == 9);
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void All_Global_NullIgnored_WhenNonNullExists()
    {
        CreateFixture();
        try
        {
            var rows = Source().SelectWhereMaxOld(e => e.Score, ExtremeRowTies.All).ToList();
            rows.Should().NotBeEmpty();
            rows.Should().OnlyContain(r => r.Score.HasValue);
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void All_Global_AllNull_ZeroRows()
    {
        CreateFixture();
        try
        {
            var rows = Source().Where(e => e.Score == null)
                .SelectWhereMaxOld(e => e.Score, ExtremeRowTies.All).ToList();
            rows.Should().BeEmpty();
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void All_Global_FilterRemovingAll_ZeroRows()
    {
        CreateFixture();
        try
        {
            var rows = Source().Where(e => e.Id == -1)
                .SelectWhereMaxOld(e => e.Score, ExtremeRowTies.All).ToList();
            rows.Should().BeEmpty();
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void All_Global_FilterRemovesTableWideWinner()
    {
        CreateFixture();
        try
        {
            var rows = Source().Where(e => e.Score < 9)
                .SelectWhereMaxOld(e => e.Score, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();

            rows.Should().HaveCount(2);
            rows.Should().OnlyContain(r => r.Score == 5);
            rows.Select(r => r.Id).Should().BeEquivalentTo(new[] { 2, 5 });
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void All_Global_Projection_Entity_Dto_Anonymous_Scalar()
    {
        CreateFixture();
        try
        {
            var entities = Source().SelectWhereMaxOld(e => e.Score, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();
            entities.Should().HaveCount(3);

            var dtos = Source().SelectWhereMaxOld(e => e.Score,
                e => new Dto { Id = e.Id, Label = e.Label, Payload = e.Payload },
                ExtremeRowTies.All).OrderBy(e => e.Id).ToList();
            dtos.Should().HaveCount(3);
            dtos[0].Label.Should().Be("nine-a");

            var anon = Source().SelectWhereMaxOld(e => e.Score,
                e => new { e.Id, e.Label, e.Payload },
                ExtremeRowTies.All).OrderBy(e => e.Id).ToList();
            anon.Should().HaveCount(3);
            anon[0].Label.Should().Be("nine-a");

            var scalars = Source().SelectWhereMaxOld(e => e.Score,
                e => e.Score, ExtremeRowTies.All).ToList();
            scalars.Should().HaveCount(3);
            scalars.Should().OnlyContain(v => v == 9);
        }
        finally { DropFixture(); }
    }

    [Fact]
    public void All_Global_NullablePayload_OrderByOuterColumn()
    {
        CreatePayloadFixture();
        try
        {
            // The outer ORDER BY column (Id) is non-null and unique, so the ordered winner sequence is
            // deterministic on every provider; the projected NULL payload must survive the join-back on
            // its own row instead of forcing an assumption about NULLS FIRST/LAST.
            var rows = Source().SelectWhereMaxOld(e => e.Score, ExtremeRowTies.All)
                .OrderBy(e => e.Id).ToList();

            rows.Should().HaveCount(3);
            rows.Should().OnlyContain(r => r.Score == 9);
            rows.Select(r => r.Id).Should().ContainInOrder(3, 4, 8);
            rows.Select(r => r.Payload).Should().ContainInOrder(new int?[] { 90, 91, null });

            // The NULL payload is a real NULL of the winning row, not an absent row.
            rows.Single(r => r.Id == 8).Payload.Should().BeNull(
                "the projected NULL payload must survive the aggregate join-back");
            rows.Single(r => r.Id == 3).Payload.Should().Be(90);
            rows.Single(r => r.Id == 4).Payload.Should().Be(91);

            // Descending on the same unambiguous column must reverse the exact sequence.
            var desc = Source().SelectWhereMaxOld(e => e.Score, ExtremeRowTies.All)
                .OrderByDescending(e => e.Id).ToList();
            desc.Select(r => r.Id).Should().ContainInOrder(8, 4, 3);
            desc.Select(r => r.Payload).Should().ContainInOrder(new int?[] { null, 91, 90 });
            desc.Single(r => r.Id == 8).Payload.Should().BeNull();

            // A second outer ORDER BY column orders the same winners without touching payloads.
            var labelDesc = Source().SelectWhereMaxOld(e => e.Score, ExtremeRowTies.All)
                .OrderByDescending(e => e.Label).ToList();
            labelDesc.Select(r => r.Label).Should().ContainInOrder("nine-c", "nine-b", "nine-a");
        }
        finally { DropFixture(); }
    }
}
