using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// T291 (#291) live ClickHouse arm. ClickHouse has no <see cref="CommonTestSuite"/>-derived class, so
/// the frozen AVG scenarios run here through <see cref="ProviderTestSuite"/> over a class-owned
/// <c>t291_avg_live</c> Memory table on ClickHouse's own container
/// (<see cref="ClickHouseTestProvider"/> / <see cref="ClickHouseContainer"/>). The fixture keeps the
/// deterministic <c>1, 2, 1, 3</c> sequence and adds the decimal, double and float controls plus the
/// nullable twin: <c>id &lt;= 2</c> is <c>[1,2]</c>, <c>id &lt;= 3</c> is <c>[1,1,2]</c>,
/// <c>id = 1</c> is <c>[1]</c>, <c>id != 3</c> is the ascending <c>[1,2,3]</c> window partition and
/// <c>id &lt; 0</c> is the empty set.
/// <para>
/// The empty-set facts assert the frozen plan value (SQL NULL). ClickHouse's native <c>avg</c> over an
/// empty or all-NULL input is <c>nan</c>, so the dialect wraps every rendered AVG in a row-eligibility
/// check (<c>if(count(...)=0, cast(null as Nullable(Float64)), avg(...))</c>) rather than testing the
/// result value: a genuine NaN or infinity from non-empty data is preserved, which the <c>nan</c>/<c>inf</c>
/// control table below pins.
/// </para>
/// </summary>
[Collection("ClickHouse")]
public sealed class ClickHouseT291AverageLiveTests : ProviderTestSuite
{
    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private const string CreateTableSql =
        "create table t291_avg_live ("
        + "id Int32, "
        + "i Int32, "
        + "l Int64, "
        + "m Decimal(18, 4), "
        + "d Float64, "
        + "f Float32, "
        + "n Nullable(Int32)) engine = Memory";

    private const string SeedSql =
        "insert into t291_avg_live (id, i, l, m, d, f, n) values "
        + "(1, 1, 1, 1, 1, 1, null), "
        + "(2, 2, 2, 2, 2, 2, 1), "
        + "(3, 1, 1, 1, 1, 1, 2), "
        + "(4, 3, 3, 3, 3, 3, 3)";

    /// <summary>A Float64 table holding <c>NaN</c>, <c>+Inf</c> and <c>-Inf</c> in one non-empty column.</summary>
    private const string SpecialTableSql = "create table t291_avg_special (id Int32, x Float64) engine = Memory";

    private const string SpecialSeedSql =
        "insert into t291_avg_special (id, x) values (1, nan), (2, inf), (3, -inf), (4, 1)";

    [Fact]
    [Trait("PDCA", "T291")]
    public void OrdinaryAverage_ShouldKeepFractionAndDeclaredClrType()
        => WithFixture(context =>
        {
            var e = context.From<T291LiveRow>().Where(x => x.Id <= 2);

            ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg(x.I)).First(), 1.5, "int [1,2]");
            ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg(x.L)).First(), 1.5, "long [1,2]");
            ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg(x.D)).First(), 1.5, "double [1,2]");
            ShouldBeFloat(e.Select(x => SqlFunctions.Sql.avg(x.F)).First(), 1.5f, "float [1,2]");
            ShouldBeDecimal(e.Select(x => SqlFunctions.Sql.avg(x.M)).First(), 1.5m, "decimal [1,2]");
        });

    [Fact]
    [Trait("PDCA", "T291")]
    public void DistinctAverage_ShouldKeepFractionWithOrdinaryControl()
        => WithFixture(context =>
        {
            var e = context.From<T291LiveRow>().Where(x => x.Id <= 3);

            ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg_distinct(x.I)).First(), 1.5, "distinct int [1,1,2]");
            ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg_distinct(x.L)).First(), 1.5, "distinct long [1,1,2]");

            // The ordinary control over the same three rows is (1 + 2 + 1) / 3, not 1.5. ClickHouse
            // evaluates AVG over an integer argument as Float64, so the native value is 4d/3d.
            ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg(x.I)).First(), 4d / 3d, "ordinary int [1,1,2] Float64");
        });

    [Fact]
    [Trait("PDCA", "T291")]
    public void NullableAverage_ShouldIgnoreNulls()
        => WithFixture(context =>
        {
            var e = context.From<T291LiveRow>().Where(x => x.Id <= 3);

            ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg(x.N)).First(), 1.5, "nullable int [NULL,1,2]");
        });

    [Fact]
    [Trait("PDCA", "T291")]
    public void OneRowAverage_ShouldBeOne()
        => WithFixture(context =>
        {
            var e = context.From<T291LiveRow>().Where(x => x.Id == 1);

            ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg(x.I)).First(), 1d, "int [1]");
            ShouldBeDouble(e.Select(x => SqlFunctions.Sql.avg(x.L)).First(), 1d, "long [1]");
        });

    [Fact]
    [Trait("PDCA", "T291")]
    public void EmptySetAverage_ShouldBeNull()
        => WithFixture(context =>
        {
            var e = context.From<T291LiveRow>().Where(x => x.Id < 0);

            // A scalar AVG over an empty set still yields one row whose value is SQL NULL; FirstOrDefault
            // observes that NULL (First() rejects a NULL scalar by contract).
            ShouldBeNull(e.Select(x => SqlFunctions.Sql.avg(x.I)).FirstOrDefault(), "int empty set");
            ShouldBeNull(e.Select(x => SqlFunctions.Sql.avg_distinct(x.I)).FirstOrDefault(), "distinct int empty set");
            ShouldBeNull(e.Select(x => SqlFunctions.Sql.avg(x.N)).FirstOrDefault(), "nullable int empty set");
        });

    [Fact]
    [Trait("PDCA", "T291")]
    public void AllNullAverage_ShouldBeNull()
        => WithFixture(context =>
        {
            // Only id = 1 is selected and its nullable column is NULL, so no row is eligible even though
            // the input set itself is non-empty.
            var e = context.From<T291LiveRow>().Where(x => x.Id == 1);

            ShouldBeNull(e.Select(x => SqlFunctions.Sql.avg(x.N)).FirstOrDefault(), "all-null nullable int");
        });

    [Fact]
    [Trait("PDCA", "T291")]
    public void EmptyFilteredAverage_ShouldBeNull()
        => WithFixture(context =>
        {
            // The aggregate-filter combinator qualifies no row, so avgIf receives no value.
            var value = context.From<T291LiveRow>()
                .Select(x => SqlFunctions.Sql.avg(x.I, () => x.Id < 0))
                .FirstOrDefault();

            ShouldBeNull(value, "filtered avgIf with zero qualifying rows");
        });

    [Fact]
    [Trait("PDCA", "T291")]
    public void EmptyWindowFrameAverage_ShouldBeNull()
        => WithFixture(context =>
        {
            // rows between 2 preceding and 1 preceding is empty for the first ordered row; the later
            // frames stay non-empty and keep their ordinary averages.
            var rows = context.From<T291LiveRow>()
                .Where(x => x.Id != 3)
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    R = SqlFunctions.Sql.avg_over(x.I).Over(
                        SqlFunctions.Sql.asc(() => x.Id),
                        WindowFrame.Rows(WindowFrameBound.Preceding(2), WindowFrameBound.Preceding(1)))
                })
                .ToList();

            rows.Select(r => r.Id).Should().Equal(1, 2, 4);
            ShouldBeNull(rows[0].R, "window AVG with an empty frame (row 1)");
            ShouldBeDouble(rows[1].R, 1d, "window AVG frame [row 1]");
            ShouldBeDouble(rows[2].R, 1.5, "window AVG frame [rows 1,2]");
        });

    /// <summary>
    /// The eligibility check must look at row eligibility, never at the result value: a genuine NaN or
    /// infinity produced by non-empty data stays exactly as ClickHouse computes it and is never turned
    /// into NULL.
    /// </summary>
    [Fact]
    [Trait("PDCA", "T291")]
    public void NonFiniteAverage_ShouldPreserveNaNAndInfinities()
        => WithFixture(context =>
        {
            var e = context.From<T291SpecialRow>();

            ShouldBeNaN(e.Where(x => x.Id >= 1).Select(x => SqlFunctions.Sql.avg(x.X)).First(), "avg over [nan, inf, -inf, 1]");
            ShouldBeNaN(e.Where(x => x.Id >= 2 && x.Id <= 3).Select(x => SqlFunctions.Sql.avg(x.X)).First(), "avg over opposite infinities [inf, -inf]");
            ShouldBeNaN(e.Where(x => x.Id == 1).Select(x => SqlFunctions.Sql.avg_distinct(x.X)).First(), "distinct avg over [nan]");
            ShouldBePositiveInfinity(e.Where(x => x.Id == 2).Select(x => SqlFunctions.Sql.avg(x.X)).First(), "avg over [inf]");
            ShouldBeNegativeInfinity(e.Where(x => x.Id == 3).Select(x => SqlFunctions.Sql.avg(x.X)).First(), "avg over [-inf]");
            ShouldBeNaN(e.Where(x => x.Id >= 1).Select(x => SqlFunctions.Sql.avg(x.X, () => x.Id <= 3)).First(), "filtered avgIf over [nan, inf, -inf]");
            ShouldBePositiveInfinity(e.Where(x => x.Id != 3).Select(x => SqlFunctions.Sql.avg(x.X, () => x.Id == 2)).First(), "filtered avgIf over [inf] only");

            var window = e.OrderBy(x => x.Id)
                .Select(x => new { x.Id, R = SqlFunctions.Sql.avg_over(x.X).Over() })
                .ToList();
            window.Should().HaveCount(4);
            window.Should().OnlyContain(r => r.R != null, "a non-empty partition must not be turned into NULL");
            window[0].R.Should().NotBeNull();
            double.IsNaN(window[0].R!.Value).Should().BeTrue(
                "the whole-partition window AVG over [nan, inf, -inf, 1] is NaN; observed {0}", window[0].R);
        });

    [Fact]
    [Trait("PDCA", "T291")]
    public void RunningWindowAverage_ShouldKeepFractions()
        => WithFixture(context =>
        {
            var rows = context.From<T291LiveRow>()
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
        });

    [Fact]
    [Trait("PDCA", "T291")]
    public void WholePartitionWindowAverage_ShouldBeTwo()
        => WithFixture(context =>
        {
            var rows = context.From<T291LiveRow>()
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
        });

    /// <summary>
    /// The ClickHouse filtered AVG path is the native <c>avgIf</c> combinator, which the plan keeps.
    /// </summary>
    [Fact]
    [Trait("PDCA", "T291")]
    public void FilteredAverage_ShouldKeepFractionThroughAvgIf()
        => WithFixture(context =>
        {
            var value = context.From<T291LiveRow>()
                .Where(x => x.Id != 3)
                .Select(x => SqlFunctions.Sql.avg(x.I, () => x.Id <= 2))
                .First();

            ShouldBeDouble(value, 1.5, "filtered avgIf int [1,2,3] where <= 2");
        });

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

    private static void ShouldBeNaN(object? value, string scenario)
    {
        value.Should().NotBeNull("T291: {0} is produced by non-empty data and must not be SQL NULL", scenario);
        value.Should().BeOfType<double>("T291: {0} materializes System.Double", scenario);
        double.IsNaN((double)value!).Should().BeTrue("T291: {0} must keep the native NaN", scenario);
    }

    private static void ShouldBePositiveInfinity(object? value, string scenario)
    {
        value.Should().NotBeNull("T291: {0} is produced by non-empty data and must not be SQL NULL", scenario);
        value.Should().BeOfType<double>("T291: {0} materializes System.Double", scenario);
        double.IsPositiveInfinity((double)value!).Should().BeTrue("T291: {0} must keep the native +Inf", scenario);
    }

    private static void ShouldBeNegativeInfinity(object? value, string scenario)
    {
        value.Should().NotBeNull("T291: {0} is produced by non-empty data and must not be SQL NULL", scenario);
        value.Should().BeOfType<double>("T291: {0} materializes System.Double", scenario);
        double.IsNegativeInfinity((double)value!).Should().BeTrue("T291: {0} must keep the native -Inf", scenario);
    }

    private void WithFixture(Action<DataContext> body)
    {
        var context = (DataContext)_sut.DataProvider;
        CreateFixture(context);
        try
        {
            body(context);
        }
        finally
        {
            DropFixture(context);
        }
    }

    private static void Execute(DataContext context, string sql)
    {
        context.EnsureConnectionOpen();
        using var command = context.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private static void CreateFixture(DataContext context)
    {
        Execute(context, "drop table if exists t291_avg_live");
        Execute(context, CreateTableSql);
        Execute(context, SeedSql);
        Execute(context, "drop table if exists t291_avg_special");
        Execute(context, SpecialTableSql);
        Execute(context, SpecialSeedSql);
    }

    private static void DropFixture(DataContext context)
    {
        Execute(context, "drop table if exists t291_avg_live");
        Execute(context, "drop table if exists t291_avg_special");
    }

    /// <summary>The non-finite control table: one non-empty Float64 column with NaN and both infinities.</summary>
    [SqlTable("t291_avg_special")]
    public sealed class T291SpecialRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("x")]
        public double X { get; set; }
    }

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
