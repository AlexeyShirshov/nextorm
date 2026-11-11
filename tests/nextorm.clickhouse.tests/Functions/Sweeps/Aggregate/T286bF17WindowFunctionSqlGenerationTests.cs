using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.SqlServer;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// T286b D17 (F17 Window): <c>nonNegativeDerivative</c> is a ClickHouse window-only aggregate. The
/// tests pin its dispatch through the existing window translator (the vendor spelling plus the
/// <c>OVER (...)</c> specification), the precise fail-closed message for a bare call, the
/// partition/order/frame shapes, the non-nullable parameter contract, the provider guard, and the
/// dialect-table row the guard uses. No connection is opened.
/// </summary>
public class T286bF17WindowFunctionSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

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

        [Column("p")]
        int P { get; set; }
    }

    [Fact]
    public void OrderBy_ShouldRenderTheVendorSpellingAndTheOverClause()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bDerivativeEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            D = SqlFunctions.ClickHouse.non_negative_derivative(x.V, x.T)
                .Over(SqlFunctions.Sql.asc(() => x.T))
        }));

        sql.Should().Contain("nonNegativeDerivative(v, t) over (order by t)");
    }

    [Fact]
    public void PartitionAndOrder_ShouldRenderBothWindowKeys()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bDerivativeEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            D = SqlFunctions.ClickHouse.non_negative_derivative(x.V, x.T)
                .Over(partitionBy: () => x.P, orderBy: () => x.T)
        }));

        sql.Should().Contain("nonNegativeDerivative(v, t) over (partition by p order by t)");
    }

    [Fact]
    public void Frame_ShouldRenderTheRowsFrame()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bDerivativeEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            D = SqlFunctions.ClickHouse.non_negative_derivative(x.V, x.T).Over(
                SqlFunctions.Sql.asc(() => x.T),
                WindowFrame.Rows(WindowFrameBound.Preceding(1), WindowFrameBound.CurrentRow))
        }));

        sql.Should().Contain(
            "nonNegativeDerivative(v, t) over (order by t rows between 1 preceding and current row)");
    }

    [Fact]
    public void WithoutOver_ShouldKeepTheExistingPreciseMessage()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bDerivativeEntity>();

        Action act = () => SqlOf(ctx, e.Select(x => new
        {
            V = SqlFunctions.ClickHouse.non_negative_derivative(x.V, x.T)
        }));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The window function nonNegativeDerivative must be completed with Over(...).");
    }

    [Fact]
    public void PostgresAndSqlServer_ShouldRejectTheCompletedWindowFunction()
    {
        using var postgres = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        using var sqlServer = new SqlServerDataContext(
            "Server=localhost;Database=nextorm;User Id=sa;Password=nextorm;TrustServerCertificate=True",
            new DataContextBuilder());

        using (new AssertionScope())
        {
            AssertRejected(postgres);
            AssertRejected(sqlServer);
        }

        static void AssertRejected(IDataContext ctx)
        {
            Action act = () => SqlOf(ctx, ctx.From<IT286bDerivativeEntity>().Select(x => new
            {
                V = SqlFunctions.ClickHouse.non_negative_derivative(x.V, x.T)
                    .Over(SqlFunctions.Sql.asc(() => x.T))
            }));

            act.Should().Throw<NotSupportedException>()
                .WithMessage("The nonNegativeDerivative window function is not supported by this provider.");
        }
    }

    [Fact]
    public void DeclaredShape_ShouldBeAWindowFunctionOfDoubleWithNonNullableParameters()
    {
        var method = typeof(ClickHouseFunctions)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(m => m.Name == "non_negative_derivative");

        using (new AssertionScope())
        {
            method.ReturnType.Should().Be(typeof(WindowFunction<double>));
            method.GetParameters().Select(p => p.ParameterType)
                .Should().BeEquivalentTo(new[] { method.GetGenericArguments()[0], typeof(DateTime) });
            method.GetGenericArguments()[0].GetGenericParameterConstraints()
                .Should().Contain(typeof(ValueType));

            var nullability = new NullabilityInfoContext();
            nullability.Create(method.GetParameters()[0]).ReadState.Should().Be(NullabilityState.NotNull);
            nullability.Create(method.GetParameters()[1]).ReadState.Should().Be(NullabilityState.NotNull);
        }
    }

    [Fact]
    public void DialectTable_ShouldCarryTheWindowOnlyNameForTheProviderGate()
    {
        var scalars = ClickHouseDialect.Instance.ScalarFunctions!;

        using (new AssertionScope())
        {
            scalars.Supports("non_negative_derivative").Should().BeTrue();
            scalars.Render("non_negative_derivative", ["v", "t"])
                .Should().Be("nonNegativeDerivative(v, t)");
        }
    }
}
