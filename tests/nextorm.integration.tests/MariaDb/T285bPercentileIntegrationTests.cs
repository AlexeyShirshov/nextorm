using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// T285b live MariaDB evidence for the analytic percentile family. MariaDB inherits the MySQL
/// dialect but, unlike MySQL, supports <c>percentile_cont</c>/<c>percentile_disc</c> as window
/// functions, so the native median composition <c>percentile_cont(0.5) within group (order by
/// value) over (...)</c> executes. The facts cover the native median and both percentiles, the
/// per-input-row cardinality, the partition shape, the <c>DISTINCT</c> collapse, the <c>0</c>/<c>1</c>
/// boundaries, the ignored-NULL and all-NULL partition contracts and the empty-input cardinality.
/// Every fact owns its marker table, created and dropped per test.
/// </summary>
public sealed class T285bPercentileMariaDbTests : IDisposable
{
    private const string CreateTableSql =
        "create table t285b_maria_value (id int not null primary key, val int null, grp int null)";

    private readonly IDataContext _ctx;

    /// <summary>
    /// Fails closed when the MariaDB server is unavailable (the same contract as
    /// <see cref="ProviderTestSuite"/>): a missing database must not look like a pass, and these tests
    /// must not add to the skip budget.
    /// </summary>
    public T285bPercentileMariaDbTests()
    {
        if (!MariaDbContainer.IsAvailable)
        {
            throw new InvalidOperationException(
                $"MariaDB is unavailable ({MariaDbContainer.Failure}). Integration tests fail closed so a "
                + "missing database cannot look like a pass: start the container runtime and set DOCKER_HOST.");
        }

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
    }

    public void Dispose() => _ctx.Dispose();

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
                    Median = SqlFunctions.MySql.median(x.Val).Over(),
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
    public void PartitionBy_ShouldKeepTheInputRowCardinality()
    {
        CreateTable("(1, 1, 1), (2, 2, 1), (3, 3, 1), (4, 10, 2), (5, 20, 2)");
        try
        {
            var rows = Values()
                .Select(x => new
                {
                    x.Id,
                    x.Grp,
                    Median = SqlFunctions.MySql.median(x.Val).Over(partitionBy: () => x.Grp)
                })
                .ToList();

            rows.Should().HaveCount(5, "the window value is repeated per input row, not collapsed per group");
            rows.Where(r => r.Grp == 1).Should().OnlyContain(r => r.Median == 2.0);
            rows.Where(r => r.Grp == 2).Should().OnlyContain(r => r.Median == 15.0);
        }
        finally
        {
            DropTable();
        }
    }

    [Fact]
    public void DistinctProjection_ShouldCollapseToOneRowPerGroup()
    {
        CreateTable("(1, 1, 1), (2, 2, 1), (3, 3, 1), (4, 10, 2), (5, 20, 2)");
        try
        {
            var rows = Values()
                .Select(x => new
                {
                    x.Grp,
                    Median = SqlFunctions.MySql.median(x.Val).Over(partitionBy: () => x.Grp)
                })
                .Distinct()
                .ToList();

            rows.Should().HaveCount(2, "the per-row window value repeats, so DISTINCT leaves one row per group");
            rows.Should().Contain(r => r.Grp == 1 && r.Median == 2.0);
            rows.Should().Contain(r => r.Grp == 2 && r.Median == 15.0);
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
                    Median = SqlFunctions.MySql.median(x.Val).Over(partitionBy: () => x.Grp),
                    Cont = SqlFunctions.Sql.percentile_cont(0.5, (double?)x.Val).Over(partitionBy: () => x.Grp),
                    Disc = SqlFunctions.Sql.percentile_disc(0.5, x.Val).Over(partitionBy: () => x.Grp)
                })
                .ToList();

            rows.Should().HaveCount(5);
            rows.Where(r => r.Grp == 1).Should().OnlyContain(r => r.Median == 2.0 && r.Cont == 2.0 && r.Disc == 1);
            rows.Where(r => r.Grp == 2).Should().OnlyContain(r => r.Median == null && r.Cont == null && r.Disc == null);
        }
        finally
        {
            DropTable();
        }
    }

    [Fact]
    public void EmptyInput_ShouldReturnNoRows()
    {
        CreateTable("(1, 1, 1), (2, 2, 1)");
        try
        {
            var rows = Values()
                .Where(x => x.Id < 0)
                .Select(x => new { Median = SqlFunctions.MySql.median(x.Val).Over() })
                .ToList();

            rows.Should().BeEmpty("an analytic percentile returns one row per input row, and the input is empty");
        }
        finally
        {
            DropTable();
        }
    }

    /// <summary>The class-owned marker table: an optional value and an optional partition key.</summary>
    [SqlTable("t285b_maria_value")]
    public sealed class T285bMariaValue
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("val")]
        public int? Val { get; set; }

        [Column("grp")]
        public int? Grp { get; set; }
    }

    private EntityBuilder<T285bMariaValue> Values() => _ctx.From<T285bMariaValue>();

    private void CreateTable(string seedValues)
    {
        Execute("drop table if exists t285b_maria_value");
        Execute(CreateTableSql);
        Execute($"insert into t285b_maria_value (id, val, grp) values {seedValues}");
    }

    private void DropTable() => Execute("drop table if exists t285b_maria_value");

    private void Execute(string sql)
    {
        ((DataContext)_ctx).EnsureConnectionOpen();
        using var command = ((DataContext)_ctx).CreateCommand(sql);
        command.ExecuteNonQuery();
    }
}
