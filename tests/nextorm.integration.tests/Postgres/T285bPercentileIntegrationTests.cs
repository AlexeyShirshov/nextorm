using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T285b live PostgreSQL evidence for the ordered-set percentile family: the native median
/// composition (<c>percentile_cont(0.5) within group (order by key)</c>), the continuous and discrete
/// aggregates, their <c>0</c>/<c>1</c> boundaries, the NULL-ignoring and all-NULL contracts, the
/// one-row-per-group aggregate cardinality, the empty-input aggregate contract and the
/// floating-point sort guard. Every fact owns its marker table, created and dropped per test.
/// </summary>
[Collection("Postgres")]
public sealed class T285bPercentilePostgresTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    private const string CreateTableSql =
        "create table t285b_pg_value (id int not null primary key, val int null, grp int null)";

    [Fact]
    public void MedianAndPercentiles_ShouldReturnTheOrderStatistics()
    {
        CreateTable("(1, 1, 1), (2, 2, 1), (3, 3, 1), (4, 4, 1)");
        try
        {
            var r = Values()
                .Select(x => new
                {
                    Median = SqlFunctions.Postgres.median(() => (double?)x.Val),
                    Cont = SqlFunctions.Postgres.percentile_cont(0.5, () => (double?)x.Val),
                    Disc = SqlFunctions.Postgres.percentile_disc(0.5, () => x.Val)
                })
                .First();

            r.Median.Should().Be(2.5, "the median interpolates between 2 and 3");
            r.Cont.Should().Be(2.5);
            r.Disc.Should().Be(2, "percentile_disc keeps the integer sort type");
        }
        finally
        {
            DropTable();
        }
    }

    [Fact]
    public void PercentileBoundaries_ShouldReturnTheExtremes()
    {
        CreateTable("(1, 1, 1), (2, 2, 1), (3, 3, 1), (4, 4, 1)");
        try
        {
            var r = Values()
                .Select(x => new
                {
                    ContZero = SqlFunctions.Postgres.percentile_cont(0, () => (double?)x.Val),
                    ContOne = SqlFunctions.Postgres.percentile_cont(1, () => (double?)x.Val),
                    DiscZero = SqlFunctions.Postgres.percentile_disc(0, () => x.Val),
                    DiscOne = SqlFunctions.Postgres.percentile_disc(1, () => x.Val)
                })
                .First();

            r.ContZero.Should().Be(1.0);
            r.ContOne.Should().Be(4.0);
            r.DiscZero.Should().Be(1);
            r.DiscOne.Should().Be(4);
        }
        finally
        {
            DropTable();
        }
    }

    [Fact]
    public void Nulls_ShouldBeIgnoredAndAnAllNullGroupShouldBeNull()
    {
        CreateTable("(1, 1, 1), (2, null, 1), (3, 3, 1), (4, null, 2), (5, null, 2)");
        try
        {
            var rows = Values()
                .GroupBy(x => x.Grp)
                .Select(x => new
                {
                    x.Grp,
                    Cont = SqlFunctions.Postgres.percentile_cont(0.5, () => (double?)x.Val),
                    Disc = SqlFunctions.Postgres.percentile_disc(0.5, () => x.Val)
                })
                .ToList();

            rows.Should().HaveCount(2);

            var mixed = rows.Single(r => r.Grp == 1);
            mixed.Cont.Should().Be(2.0, "NULL is ignored, so the median interpolates between 1 and 3");
            mixed.Disc.Should().Be(1);

            var allNull = rows.Single(r => r.Grp == 2);
            allNull.Cont.Should().BeNull("a group with no non-null value has no percentile");
            allNull.Disc.Should().BeNull();
        }
        finally
        {
            DropTable();
        }
    }

    /// <summary>
    /// A bare ordered-set aggregate is still an aggregate: PostgreSQL returns exactly one row for the
    /// empty input and that row's values are NULL (there is no group to produce a statistic from).
    /// </summary>
    [Fact]
    public void EmptyInput_ShouldReturnOneAggregateRowWithNullValues()
    {
        CreateTable("(1, 1, 1), (2, 2, 1)");
        try
        {
            var rows = Values()
                .Where(x => x.Id < 0)
                .Select(x => new
                {
                    Median = SqlFunctions.Postgres.median(() => (double?)x.Val),
                    Cont = SqlFunctions.Postgres.percentile_cont(0.5, () => (double?)x.Val),
                    Disc = SqlFunctions.Postgres.percentile_disc(0.5, () => x.Val)
                })
                .ToList();

            rows.Should().ContainSingle("a bare aggregate returns one row even for an empty input");
            rows[0].Median.Should().BeNull();
            rows[0].Cont.Should().BeNull();
            rows[0].Disc.Should().BeNull();
        }
        finally
        {
            DropTable();
        }
    }

    [Fact]
    public void GroupBy_ShouldReturnOneRowPerGroup()
    {
        CreateTable("(1, 1, 1), (2, 2, 1), (3, 3, 1), (4, 4, 1), (5, 10, 2), (6, 20, 2)");
        try
        {
            var rows = Values()
                .GroupBy(x => x.Grp)
                .Select(x => new
                {
                    x.Grp,
                    Median = SqlFunctions.Postgres.median(() => (double?)x.Val)
                })
                .ToList();

            rows.Should().HaveCount(2, "the ordered-set median collapses each group into one row");
            rows.Should().Contain(r => r.Grp == 1 && r.Median == 2.5);
            rows.Should().Contain(r => r.Grp == 2 && r.Median == 15.0);
        }
        finally
        {
            DropTable();
        }
    }

    /// <summary>
    /// The continuous percentile interpolates and therefore returns <c>double precision</c>; an
    /// integer sort expression is refused at SQL-generation time with the PostgreSQL restriction text,
    /// before a command reaches the server.
    /// </summary>
    [Fact]
    public void Median_WithAnIntegerSortExpression_ShouldBeRefusedBeforeExecution()
    {
        var act = () => _sut.SimpleEntity
            .Select(x => new { M = SqlFunctions.Postgres.median(() => x.Id) })
            .First();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The percentile_cont function on PostgreSQL requires a floating-point sort expression.");
    }

    /// <summary>The class-owned marker table: an optional value and an optional group key.</summary>
    [SqlTable("t285b_pg_value")]
    public sealed class T285bPgValue
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("val")]
        public int? Val { get; set; }

        [Column("grp")]
        public int? Grp { get; set; }
    }

    private EntityBuilder<T285bPgValue> Values() => _sut.DataProvider.From<T285bPgValue>();

    private void CreateTable(string seedValues)
    {
        Execute("drop table if exists t285b_pg_value");
        Execute(CreateTableSql);
        Execute($"insert into t285b_pg_value (id, val, grp) values {seedValues}");
    }

    private void DropTable() => Execute("drop table if exists t285b_pg_value");

    private void Execute(string sql)
    {
        var context = (DataContext)_sut.DataProvider;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }
}
