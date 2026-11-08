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
/// T286b D6 (F06 Tuple): the ClickHouse-only tuple members. The tests pin every rendered arm and the
/// declared-type cast (the server promotes Int32 components to Int64 and the declaration must stay
/// exact), the synthetic named-tuple input cast of <c>flattenTuple</c>, the homogeneous-element
/// contract of <c>tupleToNameValuePairs</c>, the numeric-component guards, the tested refusal of
/// <c>untuple</c>, and the fail-closed refusal of every other provider. They never open a connection.
/// </summary>
public class T286bF06TupleFunctionSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    [SqlTable("t286b_f06_entity")]
    internal interface IT286bTupleEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("pair")]
        Tuple<int, string> Pair { get; set; }

        [Column("other")]
        Tuple<double, string> Other { get; set; }

        [Column("triple")]
        Tuple<int, string, int> Triple { get; set; }

        [Column("triple_other")]
        Tuple<double, string, int> TripleOther { get; set; }

        [Column("ints")]
        Tuple<int, int> Ints { get; set; }

        [Column("same")]
        Tuple<int, int> Same { get; set; }

        [Column("nested")]
        Tuple<int, Tuple<int, int>> Nested { get; set; }

        [Column("deep")]
        Tuple<int, Tuple<int, Tuple<int, int>>> Deep { get; set; }

        [Column("nullable_ints")]
        Tuple<int?, int?> NullableInts { get; set; }

        [Column("strs")]
        Tuple<string, string> Strings { get; set; }

        [Column("dbls")]
        Tuple<double, double> Doubles { get; set; }
    }

    private static readonly string[] MemberNames =
    [
        "tuple_concat", "tuple_names", "flatten_tuple", "tuple_to_name_value_pairs",
        "tuple_plus", "tuple_minus", "tuple_multiply", "tuple_divide",
        "tuple_hamming_distance", "dot_product"
    ];

    [Fact]
    public void Render_ShouldEmitTheExactClickHouseArmForEveryMember()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bTupleEntity>();

        using (new AssertionScope())
        {
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.tuple_concat(x.Pair, x.Other) }))
                .Should().Contain("cast(tupleConcat(pair, other) as Tuple(Int32, String, Float64, String)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.tuple_concat(x.Triple, x.TripleOther) }))
                .Should().Contain("cast(tupleConcat(triple, triple_other) as Tuple(Int32, String, Int32, Float64, String, Int32)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.tuple_names(x.Pair) }))
                .Should().Contain("cast(tupleNames(pair) as Array(String)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.tuple_names(x.Triple) }))
                .Should().Contain("cast(tupleNames(triple) as Array(String)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.flatten_tuple(x.Nested) }))
                .Should().Contain(
                    "cast(flattenTuple(nested) as Tuple(Int32, Int32, Int32)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.flatten_tuple(x.Deep) }))
                .Should().Contain(
                    "cast(flattenTuple(deep) as Tuple(Int32, Int32, Int32, Int32)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.tuple_to_name_value_pairs(x.Same) }))
                .Should().Contain("cast(tupleToNameValuePairs(same) as Array(Tuple(String, Int32))) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.tuple_plus(x.Same, x.Same) }))
                .Should().Contain("cast(tuplePlus(same, same) as Tuple(Int32, Int32)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.tuple_minus(x.Same, x.Same) }))
                .Should().Contain("cast(tupleMinus(same, same) as Tuple(Int32, Int32)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.tuple_multiply(x.Same, x.Same) }))
                .Should().Contain("cast(tupleMultiply(same, same) as Tuple(Int32, Int32)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.tuple_divide(x.Ints, x.Ints) }))
                .Should().Contain("cast(tupleDivide(ints, ints) as Tuple(Float64, Float64)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.tuple_hamming_distance(x.Pair, x.Pair) }))
                .Should().Contain("cast(toInt64(tupleHammingDistance(pair, pair)) as Int64) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.dot_product(x.Same, x.Same) }))
                .Should().Contain("cast(dotProduct(same, same) as Int64) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.dot_product(x.Doubles, x.Doubles) }))
                .Should().Contain("cast(dotProduct(dbls, dbls) as Float64) as `V`");
        }
    }

    [Fact]
    public void Flatten_ShouldUseTheSyntheticNamedCastForInlineTuplesToo()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bTupleEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            V = SqlFunctions.ClickHouse.flatten_tuple(
                Tuple.Create(x.Id, Tuple.Create(x.Same.Item1, x.Same.Item2)))
        }));

        sql.Should().Contain(
            "cast(flattenTuple(cast(tuple(id, tuple(tupleElement(same, 1), tupleElement(same, 2))) as Tuple(f1 Int32, f2 Tuple(f3 Int32, f4 Int32)))) as Tuple(Int32, Int32, Int32))");
    }

    [Fact]
    public void DeclaredShapes_ShouldMatchTheNativeResultTypes()
    {
        (string Member, Type[] Generic, Type[] Parameters, Type Result)[] rows =
        [
            ("tuple_concat", [typeof(int), typeof(string), typeof(double), typeof(string)],
                [typeof(Tuple<int, string>), typeof(Tuple<double, string>)], typeof(Tuple<int, string, double, string>)),
            ("tuple_names", [typeof(int), typeof(string)], [typeof(Tuple<int, string>)], typeof(string[])),
            ("flatten_tuple", [typeof(int), typeof(int), typeof(int)],
                [typeof(Tuple<int, Tuple<int, int>>)], typeof(Tuple<int, int, int>)),
            ("tuple_to_name_value_pairs", [typeof(int), typeof(int)],
                [typeof(Tuple<int, int>)], typeof(Tuple<string, int>[])),
            ("tuple_plus", [typeof(int), typeof(int)],
                [typeof(Tuple<int, int>), typeof(Tuple<int, int>)], typeof(Tuple<int, int>)),
            ("tuple_divide", [typeof(int), typeof(int)],
                [typeof(Tuple<int, int>), typeof(Tuple<int, int>)], typeof(Tuple<double, double>)),
            ("tuple_hamming_distance", [typeof(int), typeof(int)],
                [typeof(Tuple<int, int>), typeof(Tuple<int, int>)], typeof(long)),
            ("dot_product", [typeof(int), typeof(int)],
                [typeof(Tuple<int, int>), typeof(Tuple<int, int>)], typeof(long)),
            ("dot_product", [], [typeof(Tuple<double, double>), typeof(Tuple<double, double>)], typeof(double)),
            ("untuple", [typeof(int), typeof(string)], [typeof(Tuple<int, string>)], typeof(object))
        ];

        using (new AssertionScope())
        {
            foreach (var row in rows)
            {
                Resolve(row.Member, row.Generic, row.Parameters).ReturnType.Should().Be(
                    row.Result, "'{0}' declares its ClickHouse result shape", row.Member);
            }
        }

        static MethodInfo Resolve(string member, Type[] generic, Type[] parameters)
        {
            var candidates = typeof(ClickHouseFunctions).GetMethods()
                .Where(method => method.Name == member
                    && method.GetGenericArguments().Length == generic.Length
                    && method.GetParameters().Length == parameters.Length)
                .Where(method => generic.Length == 0 ? !method.IsGenericMethodDefinition : method.IsGenericMethodDefinition)
                .Select(method => generic.Length == 0 ? method : method.MakeGenericMethod(generic))
                .Where(method => method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(parameters));

            return candidates.Single();
        }
    }

    [Fact]
    public void TupleToNameValuePairs_ShouldRefuseHeterogeneousTuples()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bTupleEntity>();

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.tuple_to_name_value_pairs(x.Pair))))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*tuple_to_name_value_pairs*refused*just one type*heterogeneous*common-supertype*");

        // The precise reason wins over the provider gate: the same refusal on a provider without tuples.
        using var postgres = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        FluentActions.Invoking(() => SqlOf(postgres, postgres.From<IT286bTupleEntity>()
                .Select(x => SqlFunctions.ClickHouse.tuple_to_name_value_pairs(x.Pair))))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*tuple_to_name_value_pairs*refused*just one type*");
    }

    [Fact]
    public void NumericOperators_ShouldRefuseNonNumericComponents()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bTupleEntity>();

        using (new AssertionScope())
        {
            FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.tuple_plus(x.Strings, x.Strings))))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*tuple_plus*refused for the component type*element-wise arithmetic only for numeric*");

            FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.dot_product(x.Strings, x.Strings))))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*dot_product*refused for the component type*");
        }
    }

    [Fact]
    public void Untuple_ShouldThrowThePreciseRefusal()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bTupleEntity>();

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.untuple(x.Pair) })))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*untuple*refused*syntactic substitution*dynamic set of columns*exactly one result column*");

        using var sqlServer = new SqlServerDataContext(
            "Server=localhost;Database=nextorm;User Id=sa;Password=nextorm;TrustServerCertificate=True",
            new DataContextBuilder());
        FluentActions.Invoking(() => SqlOf(sqlServer, sqlServer.From<IT286bTupleEntity>()
                .Select(x => new { V = SqlFunctions.ClickHouse.untuple(x.Pair) })))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*untuple*refused*syntactic substitution*");
    }

    [Fact]
    public void NullComponents_ShouldStayRealNullOperandsAndRefuseNullableAggregates()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bTupleEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Sum = SqlFunctions.ClickHouse.tuple_plus(x.NullableInts, x.NullableInts)
        }));

        sql.Should().Contain("cast(tuplePlus(nullable_ints, nullable_ints) as Tuple(Nullable(Int32), Nullable(Int32))) as `Sum`");

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new
            {
                Distance = SqlFunctions.ClickHouse.tuple_hamming_distance(x.NullableInts, x.NullableInts)
            })))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*tuple_hamming_distance*refused for the component type*Nullable*non-nullable*");
    }

    [Fact]
    public void Projection_ShouldRenderEveryMemberOverColumns()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bTupleEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Concat = SqlFunctions.ClickHouse.tuple_concat(x.Pair, x.Other),
            Names = SqlFunctions.ClickHouse.tuple_names(x.Pair),
            Flat = SqlFunctions.ClickHouse.flatten_tuple(x.Nested),
            Pairs = SqlFunctions.ClickHouse.tuple_to_name_value_pairs(x.Same),
            Plus = SqlFunctions.ClickHouse.tuple_plus(x.Same, x.Same),
            Minus = SqlFunctions.ClickHouse.tuple_minus(x.Same, x.Same),
            Multiply = SqlFunctions.ClickHouse.tuple_multiply(x.Same, x.Same),
            Divide = SqlFunctions.ClickHouse.tuple_divide(x.Ints, x.Ints),
            Hamming = SqlFunctions.ClickHouse.tuple_hamming_distance(x.Pair, x.Pair),
            Dot = SqlFunctions.ClickHouse.dot_product(x.Same, x.Same)
        }));

        foreach (var fragment in new[]
                 {
                     "cast(tupleConcat(pair, other) as Tuple(Int32, String, Float64, String))",
                     "cast(tupleNames(pair) as Array(String))",
                     "cast(flattenTuple(nested) as Tuple(Int32, Int32, Int32))",
                     "cast(tupleToNameValuePairs(same) as Array(Tuple(String, Int32)))",
                     "cast(tuplePlus(same, same) as Tuple(Int32, Int32))",
                     "cast(tupleMinus(same, same) as Tuple(Int32, Int32))",
                     "cast(tupleMultiply(same, same) as Tuple(Int32, Int32))",
                     "cast(tupleDivide(ints, ints) as Tuple(Float64, Float64))",
                     "cast(toInt64(tupleHammingDistance(pair, pair)) as Int64)",
                     "cast(dotProduct(same, same) as Int64)"
                 })
        {
            sql.Should().Contain(fragment, "'{0}' is part of the F06 projection", fragment);
        }
    }

    [Fact]
    public void Predicate_ShouldKeepTheFunctionsInTheWhereClause()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bTupleEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => SqlFunctions.ClickHouse.tuple_hamming_distance(x.Pair, x.Pair) == 0
                && SqlFunctions.ClickHouse.dot_product(x.Same, x.Same) > 0)
            .Select(x => x.Id));

        using (new AssertionScope())
        {
            sql.Should().Contain("where");
            sql.Should().Contain("cast(toInt64(tupleHammingDistance(pair, pair)) as Int64) = 0");
            sql.Should().Contain("cast(dotProduct(same, same) as Int64) > 0");
        }
    }

    [Fact]
    public void QuotedIdentifiers_ShouldPreserveTheColumnAndAlias()
    {
        using var ctx = ClickHouseTestContext.CreateQuoted();
        var e = ctx.From<IT286bTupleEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.ClickHouse.tuple_names(x.Pair) }));

        sql.Should().Contain("cast(tupleNames(`pair`) as Array(String)) as `V`");
    }

    [Fact]
    public void PostgresAndSqlServer_ShouldRejectEveryMember()
    {
        using var postgres = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        using var sqlServer = new SqlServerDataContext(
            "Server=localhost;Database=nextorm;User Id=sa;Password=nextorm;TrustServerCertificate=True",
            new DataContextBuilder());

        using (new AssertionScope())
        {
            foreach (var name in MemberNames)
            {
                AssertRejected(postgres, name);
                AssertRejected(sqlServer, name);
            }
        }

        static void AssertRejected(IDataContext ctx, string name)
        {
            Func<string> query = name switch
            {
                "tuple_concat" => () => SqlOf(ctx, ctx.From<IT286bTupleEntity>().Select(x => new { V = SqlFunctions.ClickHouse.tuple_concat(x.Pair, x.Other) })),
                "tuple_names" => () => SqlOf(ctx, ctx.From<IT286bTupleEntity>().Select(x => new { V = SqlFunctions.ClickHouse.tuple_names(x.Pair) })),
                "flatten_tuple" => () => SqlOf(ctx, ctx.From<IT286bTupleEntity>().Select(x => new { V = SqlFunctions.ClickHouse.flatten_tuple(x.Nested) })),
                "tuple_to_name_value_pairs" => () => SqlOf(ctx, ctx.From<IT286bTupleEntity>().Select(x => new { V = SqlFunctions.ClickHouse.tuple_to_name_value_pairs(x.Same) })),
                "tuple_plus" => () => SqlOf(ctx, ctx.From<IT286bTupleEntity>().Select(x => new { V = SqlFunctions.ClickHouse.tuple_plus(x.Same, x.Same) })),
                "tuple_minus" => () => SqlOf(ctx, ctx.From<IT286bTupleEntity>().Select(x => new { V = SqlFunctions.ClickHouse.tuple_minus(x.Same, x.Same) })),
                "tuple_multiply" => () => SqlOf(ctx, ctx.From<IT286bTupleEntity>().Select(x => new { V = SqlFunctions.ClickHouse.tuple_multiply(x.Same, x.Same) })),
                "tuple_divide" => () => SqlOf(ctx, ctx.From<IT286bTupleEntity>().Select(x => new { V = SqlFunctions.ClickHouse.tuple_divide(x.Ints, x.Ints) })),
                "tuple_hamming_distance" => () => SqlOf(ctx, ctx.From<IT286bTupleEntity>().Select(x => new { V = SqlFunctions.ClickHouse.tuple_hamming_distance(x.Pair, x.Pair) })),
                "dot_product" => () => SqlOf(ctx, ctx.From<IT286bTupleEntity>().Select(x => new { V = SqlFunctions.ClickHouse.dot_product(x.Same, x.Same) })),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
            };

            FluentActions.Invoking(query).Should().Throw<NotSupportedException>(
                "'{0}' is ClickHouse-only and must be refused by every other provider", name);
        }
    }
}
