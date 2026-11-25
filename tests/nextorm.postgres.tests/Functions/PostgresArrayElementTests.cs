using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Postgres.Tests;

public sealed class PostgresArrayElementTests
{
    [Fact]
    public void ResultTypeIsNullableForEveryRankAndElementFamily()
    {
        Result((int[] a) => SqlFunctions.Postgres.array_element(a, 1)).Should().Be(typeof(int?));
        Result((float[,] a) => SqlFunctions.Postgres.array_element(a, 1, 2)).Should().Be(typeof(float?));
        Result((float?[,] a) => SqlFunctions.Postgres.array_element(a, 1, 2)).Should().Be(typeof(float?));
        Result((int[,,] a) => SqlFunctions.Postgres.array_element(a, 1, 2, 3)).Should().Be(typeof(int?));
        Result((int[,,,] a) => SqlFunctions.Postgres.array_element(a, 1, 2, 3, 4)).Should().Be(typeof(int?));
        Result((int[,,,,] a) => SqlFunctions.Postgres.array_element(a, 1, 2, 3, 4, 5)).Should().Be(typeof(int?));
        Result((int[,,,,,] a) => SqlFunctions.Postgres.array_element(a, 1, 2, 3, 4, 5, 6)).Should().Be(typeof(int?));
        Expression<Func<string?[], string?>> reference = a => SqlFunctions.Postgres.array_element(a, 1);
        var method = ((MethodCallExpression)reference.Body).Method.GetGenericMethodDefinition();
        new NullabilityInfoContext().Create(method.ReturnParameter).ReadState.Should().Be(NullabilityState.Nullable);

        static Type Result<TArray, TResult>(Expression<Func<TArray, TResult>> projection) => projection.ReturnType;
    }

    [Fact]
    public void ParametersStayInsideNativeSubscriptsInPredicateAndProjection()
    {
        using var ctx = PostgresTestContext.Create();
        var cmd = ctx.FromSql("select null::real[] as data")
            .Where(x => SqlFunctions.Postgres.array_element(SqlFunctions.Column<float[,]>(x, "data"),
                SqlFunctions.Parameter<int>(0), 3) != null)
            .Select(x => SqlFunctions.Postgres.array_element(SqlFunctions.Column<float[,]>(x, "data"), 2, 3));

        var prepared = (DbPreparedQueryCommand<float?>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
        prepared.DbCommand.CommandText.Should().Contain("\"data\"[2][3]")
            .And.MatchRegex("""["]data["]\[@\w+\]\[3\]""");
    }

    [Fact]
    public void SqliteRefusesValueAndReferenceMarkersBeforeEmittingSubscripts()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var source = ctx.FromSql("select null as data");
        var value = source.Select(x => SqlFunctions.Postgres.array_element(SqlFunctions.Column<int[]>(x, "data"), 1));
        var reference = source.Select(x => SqlFunctions.Postgres.array_element(SqlFunctions.Column<string[]>(x, "data"), 1));

        FluentActions.Invoking(() => ctx.GetPreparedQueryCommand(value, false, false, CancellationToken.None))
            .Should().Throw<NotSupportedException>().WithMessage("*array_element requires PostgreSQL array subscripting*");
        FluentActions.Invoking(() => ctx.GetPreparedQueryCommand(reference, false, false, CancellationToken.None))
            .Should().Throw<NotSupportedException>().WithMessage("*array_element requires PostgreSQL array subscripting*");
    }

    [Fact]
    public void PostgresRefusesScalarByteaAndMappedMultirangeAsArrayOperands()
    {
        using var ctx = PostgresTestContext.Create();
        var source = ctx.FromSql("select NULL as data");
        var bytea = source.Select(x => SqlFunctions.Postgres.array_element(SqlFunctions.Column<byte[]>(x, "data"), 1));
        var multirange = source.Select(x => SqlFunctions.Postgres.array_element(SqlFunctions.Column<Range<int>[]>(x, "data"), 1));

        FluentActions.Invoking(() => ctx.GetPreparedQueryCommand(bytea, false, false, CancellationToken.None))
            .Should().Throw<NotSupportedException>().WithMessage("*not bytea, multirange, or a jagged array*");
        FluentActions.Invoking(() => ctx.GetPreparedQueryCommand(multirange, false, false, CancellationToken.None))
            .Should().Throw<NotSupportedException>().WithMessage("*not bytea, multirange, or a jagged array*");
    }
}
