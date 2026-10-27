using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T285b live SQL Server evidence for the analytic percentile family. T-SQL has no median function,
/// so the native composition is <c>percentile_cont(0.5) within group (order by value) over (...)</c>
/// and the result keeps one row per input row. The facts cover that cardinality, the partition shape,
/// the unpartitioned whole-set window, the <c>0</c>/<c>1</c> boundaries, the ignored-NULL and all-NULL
/// partition contracts, a valid non-<c>0.5</c> fraction and the fraction guard. Every fact owns its
/// marker table, created and dropped per test.
/// </summary>
[Collection("SqlServer")]
public sealed class T285bPercentileSqlServerTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    private const string CreateTableSql =
        "create table t285b_ss_value (id int not null primary key, val int null, grp int null)";

    [Fact]
    public void MedianAndPercentiles_ShouldReturnTheOrderStatisticsForEachInputRow()
    {
        CreateTable("(1, 1, 1), (2, 2, 1), (3, 3, 1), (4, 4, 1)");
        try
        {
            var rows = Values()
                .Select(x => new
                {
                    x.Id,
                    Median = SqlFunctions.SqlServer.median(x.Val).Over(),
                    Cont = SqlFunctions.Sql.percentile_cont(0.5, (double?)x.Val).Over(),
                    Disc = SqlFunctions.Sql.percentile_disc(0.5, x.Val).Over()
                })
                .ToList();

            rows.Should().HaveCount(4, "an analytic percentile keeps one row per input row");
            rows.Should().OnlyContain(r => r.Median == 2.5);
            rows.Should().OnlyContain(r => r.Cont == 2.5);
            rows.Should().OnlyContain(r => r.Disc == 2, "percentile_disc keeps the integer sort type");
        }
        finally
        {
            DropTable();
        }
    }

    [Fact]
    public void DistinctProjection_ShouldCollapseToOneRowPerGroup()
    {
        CreateTable("(1, 1, 1), (2, 2, 1), (3, 3, 1), (4, 4, 1), (5, 10, 2), (6, 20, 2)");
        try
        {
            var rows = Values()
                .Select(x => new
                {
                    x.Grp,
                    Median = SqlFunctions.SqlServer.median(x.Val).Over(partitionBy: () => x.Grp)
                })
                .Distinct()
                .ToList();

            rows.Should().HaveCount(2, "the per-row window value repeats, so DISTINCT leaves one row per group");
            rows.Should().Contain(r => r.Grp == 1 && r.Median == 2.5);
            rows.Should().Contain(r => r.Grp == 2 && r.Median == 15.0);
        }
        finally
        {
            DropTable();
        }
    }

    [Fact]
    public void PartitionBy_ShouldUsePerPartitionValuesAndTheWholeSetWithoutAPartition()
    {
        CreateTable("(1, 1, 1), (2, 2, 1), (3, 3, 1), (4, 4, 1), (5, 10, 2), (6, 20, 2)");
        try
        {
            var rows = Values()
                .Select(x => new
                {
                    x.Id,
                    x.Grp,
                    Partitioned = SqlFunctions.SqlServer.median(x.Val).Over(partitionBy: () => x.Grp),
                    Whole = SqlFunctions.SqlServer.median(x.Val).Over()
                })
                .ToList();

            rows.Should().HaveCount(6);
            rows.Where(r => r.Grp == 1).Should().OnlyContain(r => r.Partitioned == 2.5);
            rows.Where(r => r.Grp == 2).Should().OnlyContain(r => r.Partitioned == 15.0);
            rows.Should().OnlyContain(r => r.Whole == 3.5, "median over 1, 2, 3, 4, 10, 20 interpolates 3 and 4");
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
            var rows = Values()
                .Select(x => new
                {
                    x.Id,
                    ContZero = SqlFunctions.Sql.percentile_cont(0, (double?)x.Val).Over(),
                    ContOne = SqlFunctions.Sql.percentile_cont(1, (double?)x.Val).Over(),
                    DiscZero = SqlFunctions.Sql.percentile_disc(0, x.Val).Over(),
                    DiscOne = SqlFunctions.Sql.percentile_disc(1, x.Val).Over()
                })
                .ToList();

            rows.Should().HaveCount(4);
            rows.Should().OnlyContain(r => r.ContZero == 1.0);
            rows.Should().OnlyContain(r => r.ContOne == 4.0);
            rows.Should().OnlyContain(r => r.DiscZero == 1);
            rows.Should().OnlyContain(r => r.DiscOne == 4);
        }
        finally
        {
            DropTable();
        }
    }

    [Fact]
    public void Nulls_ShouldBeIgnoredAndAnAllNullPartitionShouldBeNull()
    {
        CreateTable("(1, 1, 1), (2, null, 1), (3, 3, 1), (4, null, 2), (5, null, 2)");
        try
        {
            var rows = Values()
                .Select(x => new
                {
                    x.Id,
                    x.Grp,
                    Cont = SqlFunctions.Sql.percentile_cont(0.5, (double?)x.Val).Over(partitionBy: () => x.Grp),
                    Disc = SqlFunctions.Sql.percentile_disc(0.5, x.Val).Over(partitionBy: () => x.Grp)
                })
                .ToList();

            rows.Should().HaveCount(5);
            rows.Where(r => r.Grp == 1).Should().OnlyContain(r => r.Cont == 2.0 && r.Disc == 1);
            rows.Where(r => r.Grp == 2).Should().OnlyContain(r => r.Cont == null && r.Disc == null);
        }
        finally
        {
            DropTable();
        }
    }

    [Fact]
    public void ValidFraction_ShouldExecute()
    {
        CreateTable("(1, 1, 1), (2, 2, 1), (3, 3, 1), (4, 4, 1)");
        try
        {
            var rows = Values()
                .Select(x => new { Cont = SqlFunctions.Sql.percentile_cont(0.25, (double?)x.Val).Over() })
                .ToList();

            rows.Should().HaveCount(4);
            rows.Should().OnlyContain(r => r.Cont == 1.75, "the interpolated quarter of 1, 2, 3, 4 is 1.75");
        }
        finally
        {
            DropTable();
        }
    }

    /// <summary>
    /// The fraction is validated at SQL-generation time; an out-of-range value never reaches the
    /// server, so the refusal fires regardless of whether the marker table exists.
    /// </summary>
    [Fact]
    public void OutOfRangeFraction_ShouldBeRefusedBeforeExecution()
    {
        var act = () => _sut.SimpleEntity
            .Select(x => new { C = SqlFunctions.Sql.percentile_cont(1.1, (double)x.Id).Over() })
            .First();

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("fraction")
            .WithMessage("The fraction for percentile_cont on SQL Server must be finite and between 0 and 1 inclusive.*");
    }

    /// <summary>The class-owned marker table: an optional value and an optional partition key.</summary>
    [SqlTable("t285b_ss_value")]
    public sealed class T285bSsValue
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("val")]
        public int? Val { get; set; }

        [Column("grp")]
        public int? Grp { get; set; }
    }

    private EntityBuilder<T285bSsValue> Values() => _sut.DataProvider.From<T285bSsValue>();

    private void CreateTable(string seedValues)
    {
        Execute("drop table if exists t285b_ss_value");
        Execute(CreateTableSql);
        Execute($"insert into t285b_ss_value (id, val, grp) values {seedValues}");
    }

    private void DropTable() => Execute("drop table if exists t285b_ss_value");

    private void Execute(string sql)
    {
        var context = (DataContext)_sut.DataProvider;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }
}
