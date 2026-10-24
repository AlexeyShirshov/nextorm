using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ClickHouse.Driver;
using ClickHouse.Driver.ADO;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T286b D17 (F17 Window): live execution of <c>nonNegativeDerivative</c> against the pinned
/// <c>25.8-alpine</c> (25.8.33.6) server. The tests pin the window-only contract (the server rejects
/// an aggregate call), the partition/order/frame execution, the non-negative rate semantics (the
/// first row of a partition and every negative change yield 0), the always-<c>Float64</c> result, the
/// NULL rejection for both arguments, and the precise server error identities.
/// </summary>
public sealed class T286bF17WindowFunctionTests : ProviderTestSuite
{
    [SqlTable("t286b_f17_entity")]
    internal interface IT286bDerivativeEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("v")]
        int V { get; set; }

        [Column("t")]
        DateTime T { get; set; }

        [Column("f")]
        double F { get; set; }

        [Column("t64")]
        DateTime T64 { get; set; }

        [Column("p")]
        int P { get; set; }
    }

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private static List<T> Raw<T>(string sql)
    {
        using var context = ClickHouseTestProvider.Instance.CreateContext();
        using var result = context.ExecuteRaw(sql);
        return result.Read<T>().ToList();
    }

    private static void Seed()
    {
        using var connection = new ClickHouseConnection(ClickHouseContainer.ConnectionString);
        connection.Open();
        foreach (var statement in new[]
                 {
                     "drop table if exists t286b_f17_entity",
                     "create table t286b_f17_entity (id Int32, v Int32, f Float64, " +
                     "t DateTime, t64 DateTime64(3), p Int32) engine = Memory",
                     "insert into t286b_f17_entity (id, v, f, t, t64, p) values " +
                     "(1, 1, 1.5, '2024-01-01 00:00:00', '2024-01-01 00:00:00.500', 1), " +
                     "(2, 3, 2.5, '2024-01-01 00:00:02', '2024-01-01 00:00:02.500', 1), " +
                     "(3, 2, 2.0, '2024-01-01 00:00:03', '2024-01-01 00:00:03.000', 1), " +
                     "(4, 10, 10.5, '2024-01-01 00:00:01', '2024-01-01 00:00:01.000', 2), " +
                     "(5, 20, 20.5, '2024-01-01 00:00:03', '2024-01-01 00:00:03.000', 2)"
                 })
        {
            using var command = connection.CreateCommand();
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }
    }

    [Fact]
    public void PartitionAndOrder_ShouldReturnTheNonNegativeRate()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var rows = ctx.From<IT286bDerivativeEntity>()
            .Select(x => new
            {
                x.Id,
                D = SqlFunctions.ClickHouse.non_negative_derivative(x.V, x.T)
                    .Over(partitionBy: () => x.P, orderBy: () => x.T)
            })
            .ToList()
            .OrderBy(x => x.Id)
            .ToList();

        using (new AssertionScope())
        {
            rows.Should().HaveCount(5);
            rows.Select(x => x.D).Should().Equal(0, 1, 0, 0, 5);
        }
    }

    [Fact]
    public void OrderOnly_ShouldUseTheSinglePartition()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var rows = ctx.From<IT286bDerivativeEntity>()
            .Where(x => x.P == 1)
            .Select(x => new
            {
                x.Id,
                D = SqlFunctions.ClickHouse.non_negative_derivative(x.V, x.T)
                    .Over(SqlFunctions.Sql.asc(() => x.T))
            })
            .ToList()
            .OrderBy(x => x.Id)
            .ToList();

        using (new AssertionScope())
        {
            rows.Select(x => x.D).Should().Equal(new[] { 0.0, 1.0, 0.0 },
                "the first row has no predecessor and the 3 -> 2 change is negative");
        }
    }

    [Fact]
    public void Frame_ShouldExecuteWithTheRowsFrame()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var rows = ctx.From<IT286bDerivativeEntity>()
            .Where(x => x.P == 1)
            .Select(x => new
            {
                x.Id,
                D = SqlFunctions.ClickHouse.non_negative_derivative(x.V, x.T).Over(
                    SqlFunctions.Sql.asc(() => x.T),
                    WindowFrame.Rows(WindowFrameBound.Preceding(1), WindowFrameBound.CurrentRow))
            })
            .ToList();

        using (new AssertionScope())
        {
            rows.Should().HaveCount(3);
            rows.Should().OnlyContain(x => x.D >= 0);
        }
    }

    [Fact]
    public void Result_ShouldAlwaysBeFloat64()
    {
        Seed();

        var typeNames = Raw<string>(
            "select toTypeName(nonNegativeDerivative(v, t) over (order by t)) from t286b_f17_entity");

        typeNames.Should().HaveCount(5).And.OnlyContain(name => name == "Float64");
    }

    [Fact]
    public void FloatValues_ShouldReturnTheExactRate()
    {
        Seed();

        var values = Raw<double>(
            "select nonNegativeDerivative(f, t) over (order by t) " +
            "from t286b_f17_entity where id in (1, 2) order by t");

        values.Should().Equal(0, 0.5);
    }

    [Fact]
    public void BareAggregateCall_ShouldBeRejectedByTheServer()
    {
        Seed();

        FluentActions.Invoking(() => Raw<double>(
                "select nonNegativeDerivative(v, t) from (select 1 as v, " +
                "toDateTime('2024-01-01 00:00:00') as t)"))
            .Should().Throw<ClickHouseServerException>()
            .WithMessage("*can only be used as a window function*");
    }

    [Fact]
    public void NullableArguments_ShouldBeRejectedByTheServer()
    {
        Seed();

        using (new AssertionScope())
        {
            FluentActions.Invoking(() => Raw<double>(
                    "select nonNegativeDerivative(CAST(NULL AS Nullable(Int32)), t) over () " +
                    "from (select toDateTime('2024-01-01 00:00:00') as t)"))
                .Should().Throw<ClickHouseServerException>()
                .WithMessage("*Argument 0 must be a number*");

            FluentActions.Invoking(() => Raw<double>(
                    "select nonNegativeDerivative(1, toNullable(t)) over () " +
                    "from (select toDateTime('2024-01-01 00:00:00') as t)"))
                .Should().Throw<ClickHouseServerException>()
                .WithMessage("*Argument 1 must be DateTime or DateTime64*");
        }
    }

    [Fact]
    public void DateTime64Timestamp_ShouldBeAccepted()
    {
        Seed();

        var values = Raw<double>(
            "select nonNegativeDerivative(v, t64) over (order by t64) " +
            "from t286b_f17_entity where id in (1, 2) order by t64");

        values.Should().Equal(0, 1);
    }
}
