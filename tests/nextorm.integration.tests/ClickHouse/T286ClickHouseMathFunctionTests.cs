using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using ClickHouse.Driver.ADO;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T286 (#286): live execution of the ClickHouse-only math functions <c>log2</c>, <c>cbrt</c>,
/// <c>exp2</c> and <c>exp10</c> on the pinned <c>25.8-alpine</c> server. Per function the tests pin
/// the generated expression, the finite value, the CLR <see cref="double"/>, the server
/// <c>Float64</c> type, the nullable column/literal null contract (NULL, never zero), the projection
/// alias and the boolean-predicate placement.
/// </summary>
public sealed class T286ClickHouseMathFunctionTests : ProviderTestSuite
{
    private static readonly (string Name, double Input, double Expected)[] Cases =
    [
        ("log2", 8d, 3d),
        ("cbrt", 8d, 2d),
        ("exp2", 3d, 8d),
        ("exp10", 2d, 100d)
    ];

    [SqlTable("t286_math_entity")]
    internal interface IT286MathEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("v")]
        double Value { get; set; }

        [Column("nv")]
        double? NullableValue { get; set; }
    }

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    private static void Seed()
    {
        using var connection = new ClickHouseConnection(ClickHouseContainer.ConnectionString);
        connection.Open();
        foreach (var statement in new[]
                 {
                     "drop table if exists t286_math_entity",
                     "create table t286_math_entity (id Int32, v Float64, nv Nullable(Float64)) engine = Memory",
                     "insert into t286_math_entity (id, v, nv) values (1, 8, 8), (2, 8, null), (3, 3, 3), (4, 2, 2)"
                 })
        {
            using var command = connection.CreateCommand();
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }
    }

    [Fact]
    public void Projection_ShouldRenderTheExactExpressionsAndMaterializeDouble()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var e = ctx.From<IT286MathEntity>();

        var query = e.Where(x => x.Id == 1).Select(x => new
        {
            L2 = SqlFunctions.ClickHouse.log2(x.Value),
            Cb = SqlFunctions.ClickHouse.cbrt(x.Value),
            E2 = SqlFunctions.ClickHouse.exp2(x.Value),
            E10 = SqlFunctions.ClickHouse.exp10(x.Value)
        });

        var sql = SqlOf(ctx, query);
        using (new AssertionScope())
        {
            sql.Should().Contain("log2(v) as `L2`");
            sql.Should().Contain("cbrt(v) as `Cb`");
            sql.Should().Contain("exp2(v) as `E2`");
            sql.Should().Contain("exp10(v) as `E10`");
        }

        var row = query.First();
        row.L2.GetType().Should().Be(typeof(double), "the projection materializes the declared CLR double");
        row.Cb.GetType().Should().Be(typeof(double));
        row.E2.GetType().Should().Be(typeof(double));
        row.E10.GetType().Should().Be(typeof(double));

        var l2 = e.Where(x => x.Id == 1).Select(x => SqlFunctions.ClickHouse.log2(x.Value)).First();
        var cb = e.Where(x => x.Id == 2).Select(x => SqlFunctions.ClickHouse.cbrt(x.Value)).First();
        var e2 = e.Where(x => x.Id == 3).Select(x => SqlFunctions.ClickHouse.exp2(x.Value)).First();
        var e10 = e.Where(x => x.Id == 4).Select(x => SqlFunctions.ClickHouse.exp10(x.Value)).First();

        l2.Should().BeApproximately(3d, 1e-12, "log2(8) is 3");
        cb.Should().BeApproximately(2d, 1e-12, "cbrt(8) is 2");
        e2.Should().BeApproximately(8d, 1e-12, "exp2(3) is 8");
        e10.Should().BeApproximately(100d, 1e-12, "exp10(2) is 100");
    }

    [Fact]
    public void NullableColumn_ShouldKeepTheValueAndTheNull()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var rows = ctx.From<IT286MathEntity>()
            .Where(x => x.Id == 1 || x.Id == 2)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                Id = x.Id,
                L2 = SqlFunctions.ClickHouse.log2(x.NullableValue),
                Cb = SqlFunctions.ClickHouse.cbrt(x.NullableValue),
                E2 = SqlFunctions.ClickHouse.exp2(x.NullableValue),
                E10 = SqlFunctions.ClickHouse.exp10(x.NullableValue)
            })
            .ToList();

        rows.Should().HaveCount(2);
        var valueRow = rows[0];
        valueRow.L2!.Value.GetType().Should().Be(typeof(double), "a nullable column with a value materializes the value");
        valueRow.L2.Should().BeApproximately(3d, 1e-12);
        valueRow.Cb.Should().BeApproximately(2d, 1e-12);
        valueRow.E2.Should().BeApproximately(256d, 1e-12, "exp2(8) is 256");
        valueRow.E10.Should().BeApproximately(100_000_000d, 1e-12, "exp10(8) is 1e8");

        var nullRow = rows[1];
        nullRow.L2.Should().BeNull("log2(NULL) is NULL, never zero");
        nullRow.Cb.Should().BeNull("cbrt(NULL) is NULL, never zero");
        nullRow.E2.Should().BeNull("exp2(NULL) is NULL, never zero");
        nullRow.E10.Should().BeNull("exp10(NULL) is NULL, never zero");
    }

    [Fact]
    public void NullableLiteral_ShouldKeepTheValueAndTheNull()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var e = ctx.From<IT286MathEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            L2 = SqlFunctions.ClickHouse.log2((double?)8d),
            Cb = SqlFunctions.ClickHouse.cbrt((double?)8d),
            E2 = SqlFunctions.ClickHouse.exp2((double?)3d),
            E10 = SqlFunctions.ClickHouse.exp10((double?)2d),
            NullL2 = SqlFunctions.ClickHouse.log2((double?)null)
        }));

        sql.Should().Contain("log2(", "the nullable literal stays translated, not evaluated client-side");
        sql.Should().Contain("cbrt(");
        sql.Should().Contain("exp2(");
        sql.Should().Contain("exp10(");

        var row = e.Where(x => x.Id == 1).Select(x => new
        {
            L2 = SqlFunctions.ClickHouse.log2((double?)8d),
            Cb = SqlFunctions.ClickHouse.cbrt((double?)8d),
            E2 = SqlFunctions.ClickHouse.exp2((double?)3d),
            E10 = SqlFunctions.ClickHouse.exp10((double?)2d),
            NullL2 = SqlFunctions.ClickHouse.log2((double?)null)
        }).First();

        row.L2.Should().BeApproximately(3d, 1e-12);
        row.Cb.Should().BeApproximately(2d, 1e-12);
        row.E2.Should().BeApproximately(8d, 1e-12);
        row.E10.Should().BeApproximately(100d, 1e-12);
        row.NullL2.Should().BeNull("a typed nullable NULL literal stays NULL");
    }

    [Fact]
    public async Task ServerTypeAndRawDriver_ShouldBeFloat64AndNullableFloat64()
    {
        Seed();
        await using var connection = new ClickHouseConnection(ClickHouseContainer.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        async Task<(string Type, bool IsNull, string? Text, string RawType)> ProbeAsync(string expression)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"select toTypeName({expression}), isNull({expression}), toString({expression}), {expression}";
            await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            (await reader.ReadAsync(TestContext.Current.CancellationToken)).Should().BeTrue("the probe selects one row");
            var type = reader.GetString(0);
            var isNull = Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture) != 0;
            var text = reader.IsDBNull(2) ? null : reader.GetString(2);
            var raw = reader.GetValue(3);
            return (type, isNull, text, raw is DBNull ? "DBNull" : raw!.GetType().Name);
        }

        using (new AssertionScope())
        {
            foreach (var (name, input, expected) in Cases)
            {
                var literal = await ProbeAsync($"{name}({input.ToString(CultureInfo.InvariantCulture)})");
                literal.Type.Should().Be("Float64", "'{0}' returns Float64 for a non-null argument", name);
                literal.RawType.Should().Be("Double", "'{0}' materializes as System.Double", name);
                literal.IsNull.Should().BeFalse();
                double.Parse(literal.Text!, CultureInfo.InvariantCulture).Should().BeApproximately(expected, 1e-12);

                var nullable = await ProbeAsync($"{name}(CAST(NULL AS Nullable(Float64)))");
                nullable.Type.Should().Be("Nullable(Float64)", "'{0}' stays Nullable(Float64) for a nullable argument", name);
                nullable.IsNull.Should().BeTrue("'{0}' of NULL is NULL, not zero", name);
                nullable.RawType.Should().Be("DBNull");
            }
        }

        await using var columnCommand = connection.CreateCommand();
        columnCommand.CommandText = "select nv, toTypeName(nv), log2(nv), toTypeName(log2(nv)) from t286_math_entity where id in (1, 2) order by id";
        await using var columnReader = await columnCommand.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        (await columnReader.ReadAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
        columnReader.GetString(1).Should().Be("Nullable(Float64)", "the nullable column is Nullable(Float64)");
        columnReader.GetValue(0).Should().BeOfType<double>("a value row reads back as System.Double");
        columnReader.GetString(3).Should().Be("Nullable(Float64)", "log2(nv) stays Nullable(Float64)");
        columnReader.GetValue(2).Should().BeOfType<double>();

        (await columnReader.ReadAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
        columnReader.IsDBNull(0).Should().BeTrue("the NULL row is DBNull at the driver layer");
        columnReader.IsDBNull(2).Should().BeTrue("log2(NULL) is DBNull at the driver layer, not zero");
    }

    [Fact]
    public void Predicate_ShouldFilterOnTheFourFunctions()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var e = ctx.From<IT286MathEntity>();

        var query = e
            .Where(x => SqlFunctions.ClickHouse.log2(x.Value) > 2.5
                && SqlFunctions.ClickHouse.cbrt(x.Value) > 1.5
                && SqlFunctions.ClickHouse.exp2(x.Value) > 10
                && SqlFunctions.ClickHouse.exp10(x.Value) > 100_000)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);

        var sql = SqlOf(ctx, query);
        sql.Should().Contain("where", "the four calls stay a server-side predicate");
        sql.Should().Contain("log2(v) >");
        sql.Should().Contain("cbrt(v) >");
        sql.Should().Contain("exp2(v) >");
        sql.Should().Contain("exp10(v) >");

        query.ToList().Should().Equal(
            new[] { 1, 2 },
            "only the two v = 8 rows satisfy all four thresholds, so every predicate executes server-side");
    }
}
