using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using FluentAssertions;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// T291 (#291) live MariaDB arm. MariaDB has no <see cref="ITestProvider"/> and therefore does not
/// inherit <see cref="CommonTestSuite"/>, so the frozen AVG scenarios run here over a class-owned
/// <c>t291_avg_live</c> table against MariaDB's own container (<see cref="MariaDbContainer"/>; never the
/// MySQL suite). The fixture keeps the deterministic <c>1, 2, 1, 3</c> sequence and adds the decimal,
/// double and float controls plus the nullable twin: <c>id &lt;= 2</c> is <c>[1,2]</c>,
/// <c>id &lt;= 3</c> is <c>[1,1,2]</c>, <c>id = 1</c> is <c>[1]</c>, <c>id != 3</c> is the ascending
/// <c>[1,2,3]</c> window partition and <c>id &lt; 0</c> is the empty set.
/// </summary>
public sealed class MariaDbT291AverageLiveTests : IDisposable
{
    private const string CreateTableSql =
        "create table t291_avg_live ("
        + "id int not null primary key, "
        + "i int not null, "
        + "l bigint not null, "
        + "m decimal(18,4) not null, "
        + "d double not null, "
        + "f float not null, "
        + "n int null) engine=InnoDB";

    private const string SeedSql =
        "insert into t291_avg_live (id, i, l, m, d, f, n) values "
        + "(1, 1, 1, 1, 1, 1, null), "
        + "(2, 2, 2, 2, 2, 2, 1), "
        + "(3, 1, 1, 1, 1, 1, 2), "
        + "(4, 3, 3, 3, 3, 3, 3)";

    private readonly IDataContext _ctx;

    public MariaDbT291AverageLiveTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        CreateFixture();
    }

    public void Dispose()
    {
        try
        {
            DropFixture();
        }
        finally
        {
            _ctx.Dispose();
        }
    }

    [Fact]
    [Trait("PDCA", "T291")]
    public void OrdinaryAverage_ShouldKeepFractionAndDeclaredClrType()
    {
        var e = _ctx.From<T291LiveRow>().Where(x => x.Id <= 2);

        ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg(x.I)).First(), 1.5, "int [1,2]");
        ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg(x.L)).First(), 1.5, "long [1,2]");
        ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg(x.D)).First(), 1.5, "double [1,2]");
        ShouldBeFloat(e.Select(x => SqlFunctions.Sql.avg(x.F)).First(), 1.5f, "float [1,2]");
        ShouldBeDecimal(e.Select(x => SqlFunctions.Sql.avg(x.M)).First(), 1.5m, "decimal [1,2]");
    }

    [Fact]
    [Trait("PDCA", "T291")]
    public void DistinctAverage_ShouldKeepFractionWithOrdinaryControl()
    {
        var e = _ctx.From<T291LiveRow>().Where(x => x.Id <= 3);

        ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg_distinct(x.I)).First(), 1.5, "distinct int [1,1,2]");
        ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg_distinct(x.L)).First(), 1.5, "distinct long [1,1,2]");

        // The ordinary control over the same three rows is (1 + 2 + 1) / 3, not 1.5. MariaDB evaluates
        // AVG over an integer argument as DECIMAL(14,4), so the provider-native value is exactly 1.3333.
        ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg(x.I)).First(), 1.3333, "ordinary int [1,1,2] DECIMAL(14,4)");
    }

    [Fact]
    [Trait("PDCA", "T291")]
    public void NullableAverage_ShouldIgnoreNulls()
    {
        var e = _ctx.From<T291LiveRow>().Where(x => x.Id <= 3);

        ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg(x.N)).First(), 1.5, "nullable int [NULL,1,2]");
    }

    [Fact]
    [Trait("PDCA", "T291")]
    public void OneRowAverage_ShouldBeOne()
    {
        var e = _ctx.From<T291LiveRow>().Where(x => x.Id == 1);

        ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg(x.I)).First(), 1d, "int [1]");
        ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg(x.L)).First(), 1d, "long [1]");
    }

    [Fact]
    [Trait("PDCA", "T291")]
    public void EmptySetAverage_ShouldBeNull()
    {
        var e = _ctx.From<T291LiveRow>().Where(x => x.Id < 0);

        // A scalar AVG over an empty set still yields one row whose value is SQL NULL; FirstOrDefault
        // observes that NULL (First() rejects a NULL scalar by contract).
        ShouldBeNull(e.Select(x => SqlFunctions.Sql.avg(x.I)).FirstOrDefault(), "int empty set");
        ShouldBeNull(e.Select(x => SqlFunctions.Sql.avg(x.N)).FirstOrDefault(), "nullable int empty set");
    }

    [Fact]
    [Trait("PDCA", "T291")]
    public void RunningWindowAverage_ShouldKeepFractions()
    {
        var rows = _ctx.From<T291LiveRow>()
            .Where(x => x.Id != 3)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                R = SqlFunctions.Sql.avg_over(x.I).Over(
                    SqlFunctions.Sql.asc(() => x.Id),
                    WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow)),
                RL = SqlFunctions.Sql.avg_over(x.L).Over(
                    SqlFunctions.Sql.asc(() => x.Id),
                    WindowFrame.Rows(WindowFrameBound.UnboundedPreceding, WindowFrameBound.CurrentRow))
            })
            .ToList();

        rows.Select(r => r.Id).Should().Equal(1, 2, 4);
        ShouldBeDouble(rows[0].R, 1d, "running window int row 1");
        ShouldBeDouble(rows[1].R, 1.5, "running window int row 2");
        ShouldBeDouble(rows[2].R, 2d, "running window int row 3");
        ShouldBeDouble(rows[2].RL, 2d, "running window long row 3");
    }

    [Fact]
    [Trait("PDCA", "T291")]
    public void WholePartitionWindowAverage_ShouldBeTwo()
    {
        var rows = _ctx.From<T291LiveRow>()
            .Where(x => x.Id != 3)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                R = SqlFunctions.Sql.avg_over(x.I).Over()
            })
            .ToList();

        rows.Should().HaveCount(3);
        ShouldBeDouble(rows[0].R, 2d, "whole-partition int row 1");
        ShouldBeDouble(rows[1].R, 2d, "whole-partition int row 2");
        ShouldBeDouble(rows[2].R, 2d, "whole-partition int row 3");
    }

    private static void ShouldBeDouble(object? value, double expected, string scenario)
    {
        Convert.ToDouble(value, CultureInfo.InvariantCulture).Should().BeApproximately(
            expected, 1e-12, "T291: {0} must keep the fractional average", scenario);
        value.Should().BeOfType<double>(
            "T291: {0} must materialize an integral AVG as System.Double (nullable at the API level)", scenario);
    }

    private static void ShouldBeFloat(object? value, float expected, string scenario)
    {
        Convert.ToSingle(value, CultureInfo.InvariantCulture).Should().BeApproximately(
            expected, 1e-6f, "T291: {0} must keep the fractional average", scenario);
        value.Should().BeOfType<float>(
            "T291: {0} must materialize a float AVG as System.Single (nullable at the API level)", scenario);
    }

    private static void ShouldBeDecimal(object? value, decimal expected, string scenario)
    {
        value.Should().BeOfType<decimal>(
            "T291: {0} must materialize a decimal AVG as System.Decimal (nullable at the API level)", scenario);
        ((decimal)value!).Should().Be(expected, "T291: {0} must keep the fractional average", scenario);
    }

    private static void ShouldBeNull(object? value, string scenario)
        => value.Should().BeNull("T291: {0} over an empty set is SQL NULL", scenario);

    private void Execute(string sql)
    {
        var context = (DataContext)_ctx;
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private void CreateFixture()
    {
        Execute("drop table if exists t291_avg_live");
        Execute(CreateTableSql);
        Execute(SeedSql);
    }

    private void DropFixture() => Execute("drop table if exists t291_avg_live");

    /// <summary>The class-owned fixture: integer, long, decimal, double, float and nullable integer columns.</summary>
    [SqlTable("t291_avg_live")]
    public sealed class T291LiveRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("i")]
        public int I { get; set; }

        [Column("l")]
        public long L { get; set; }

        [Column("m")]
        public decimal M { get; set; }

        [Column("d")]
        public double D { get; set; }

        [Column("f")]
        public float F { get; set; }

        [Column("n")]
        public int? N { get; set; }
    }
}
