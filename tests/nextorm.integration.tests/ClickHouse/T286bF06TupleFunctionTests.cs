using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ClickHouse.Driver.ADO;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T286b D6 (F06 Tuple): live execution of the ClickHouse-only tuple members against the pinned
/// <c>25.8-alpine</c> (25.8.33.6) server. The tests pin the typed CLR mapping end to end: a tuple
/// column materialises as <see cref="Tuple"/>, element access and nesting keep their component types,
/// <c>tupleConcat</c>/<c>flattenTuple</c>/<c>tupleToNameValuePairs</c> result shapes are the declared
/// ones, the element-wise operators follow the vendor promotion and cast rules, and the deterministic
/// refusals (<c>untuple</c>, heterogeneous name/value pairs, Nullable Hamming components) never reach
/// the server.
/// </summary>
[Collection("ClickHouse")]
public sealed class T286bF06TupleFunctionTests : ProviderTestSuite
{
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

        [Column("named")]
        Tuple<int, string> Named { get; set; }

        [Column("same")]
        Tuple<int, int> Same { get; set; }

        [Column("nested")]
        Tuple<int, Tuple<int, int>> Nested { get; set; }

        [Column("deep")]
        Tuple<int, Tuple<int, Tuple<int, int>>> Deep { get; set; }

        [Column("ints")]
        Tuple<int, int> Ints { get; set; }

        [Column("nints")]
        Tuple<int?, int?> NullableInts { get; set; }

        [Column("strs")]
        Tuple<string, string> Strings { get; set; }

        [Column("dbls")]
        Tuple<double, double> Doubles { get; set; }
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
                     "drop table if exists t286b_f06_entity",
                     "create table t286b_f06_entity (id Int32, "
                     + "pair Tuple(Int32, String), other Tuple(Float64, String), "
                     + "named Tuple(a Int32, b String), same Tuple(a Int32, b Int32), "
                     + "nested Tuple(a Int32, b Tuple(c Int32, d Int32)), "
                     + "deep Tuple(a Int32, b Tuple(c Int32, d Tuple(e Int32, f Int32))), "
                     + "ints Tuple(Int32, Int32), nints Tuple(Nullable(Int32), Nullable(Int32)), "
                     + "strs Tuple(String, String), dbls Tuple(Float64, Float64)) engine = Memory",
                     "insert into t286b_f06_entity values "
                     + "(1, (7, 'seven'), (2.5, 'x'), (7, 'seven'), (7, 8), (7, (8, 9)), (7, (8, (9, 10))), "
                     + "(2, 3), (NULL, 4), ('a', 'b'), (2.5, 3.5)), "
                     + "(2, (0, 'zero'), (0.0, 'z'), (0, 'zero'), (0, 0), (0, (0, 0)), (0, (0, (0, 0))), "
                     + "(0, 0), (0, 0), ('', ''), (0, 0))"
                 })
        {
            using var command = connection.CreateCommand();
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }
    }

    [Fact]
    public void TupleColumnsAndElementAccess_ShouldMaterialiseTheDeclaredClrTypes()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var pair = ctx.From<IT286bTupleEntity>().Where(x => x.Id == 1).Select(x => x.Pair).First();
        pair.Should().Be(Tuple.Create(7, "seven"));
        pair.GetType().Should().Be(typeof(Tuple<int, string>));

        var row = ctx.From<IT286bTupleEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                First = x.Pair.Item1,
                Second = x.Pair.Item2,
                NestedFirst = x.Nested.Item2.Item1,
                Inline = Tuple.Create(x.Id, x.Pair.Item2),
                Nested = x.Nested
            })
            .First();

        using (new AssertionScope())
        {
            row.First.Should().Be(7);
            row.Second.Should().Be("seven");
            row.NestedFirst.Should().Be(8);
            row.Inline.Should().Be(Tuple.Create(1, "seven"));
            row.Nested.Should().Be(Tuple.Create(7, Tuple.Create(8, 9)));
            row.Nested.GetType().Should().Be(typeof(Tuple<int, Tuple<int, int>>));
        }
    }

    [Fact]
    public void TupleConcat_ShouldConcatenateBothArityForms()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bTupleEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Two = SqlFunctions.ClickHouse.tuple_concat(x.Pair, x.Other),
                Six = SqlFunctions.ClickHouse.tuple_concat(
                    Tuple.Create(x.Id, "a", x.Id),
                    Tuple.Create(1.5, "b", 2))
            })
            .First();

        using (new AssertionScope())
        {
            row.Two.Should().Be(Tuple.Create(7, "seven", 2.5, "x"));
            row.Six.Should().Be(Tuple.Create(1, "a", 1, 1.5, "b", 2));
            row.Two.GetType().Should().Be(typeof(Tuple<int, string, double, string>));
        }
    }

    [Fact]
    public void TupleNames_ShouldReturnNamesForNamedAndIndicesForUnnamedTuples()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bTupleEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Named = SqlFunctions.ClickHouse.tuple_names(x.Named),
                Unnamed = SqlFunctions.ClickHouse.tuple_names(x.Pair)
            })
            .First();

        using (new AssertionScope())
        {
            row.Named.Should().Equal("a", "b");
            row.Unnamed.Should().Equal(new[] { "1", "2" },
                "an unnamed tuple reports the one-based indices as strings");
            row.Named.GetType().Should().Be(typeof(string[]));
        }
    }

    [Fact]
    public void FlattenTuple_ShouldFlattenNamedAndInlineNestedTuples()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bTupleEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                OneLevel = SqlFunctions.ClickHouse.flatten_tuple(x.Nested),
                TwoLevels = SqlFunctions.ClickHouse.flatten_tuple(x.Deep),
                Inline = SqlFunctions.ClickHouse.flatten_tuple(
                    Tuple.Create(x.Id, Tuple.Create(x.Same.Item1, x.Same.Item2)))
            })
            .First();

        using (new AssertionScope())
        {
            row.OneLevel.Should().Be(Tuple.Create(7, 8, 9));
            row.TwoLevels.Should().Be(Tuple.Create(7, 8, 9, 10));
            row.Inline.Should().Be(Tuple.Create(1, 7, 8),
                "the synthetic named-tuple input cast makes flattenTuple work on an inline constructor");
        }
    }

    [Fact]
    public void TupleToNameValuePairs_ShouldReturnTypedPairsForAHomogeneousTuple()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bTupleEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Named = SqlFunctions.ClickHouse.tuple_to_name_value_pairs(x.Same),
                Unnamed = SqlFunctions.ClickHouse.tuple_to_name_value_pairs(x.Ints)
            })
            .First();

        using (new AssertionScope())
        {
            row.Named.Should().Equal(Tuple.Create("a", 7), Tuple.Create("b", 8));
            row.Unnamed.Should().Equal(Tuple.Create("1", 2), Tuple.Create("2", 3));
            row.Named.GetType().Should().Be(typeof(Tuple<string, int>[]));
        }
    }

    [Fact]
    public void Operators_ShouldApplyElementWiseAndCastToTheDeclaredTypes()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bTupleEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Plus = SqlFunctions.ClickHouse.tuple_plus(x.Same, x.Same),
                Minus = SqlFunctions.ClickHouse.tuple_minus(x.Same, x.Ints),
                Multiply = SqlFunctions.ClickHouse.tuple_multiply(x.Ints, x.Ints),
                Divide = SqlFunctions.ClickHouse.tuple_divide(x.Ints, x.Ints),
                DivideByZero = SqlFunctions.ClickHouse.tuple_divide(x.Ints, Tuple.Create(0, 0)),
                Floating = SqlFunctions.ClickHouse.tuple_plus(x.Doubles, x.Doubles),
                Wrapped = SqlFunctions.ClickHouse.tuple_plus(
                    Tuple.Create(int.MaxValue, 1), Tuple.Create(1, 1))
            })
            .First();

        using (new AssertionScope())
        {
            row.Plus.Should().Be(Tuple.Create(14, 16));
            row.Minus.Should().Be(Tuple.Create(5, 5));
            row.Multiply.Should().Be(Tuple.Create(4, 9));
            row.Divide.Should().Be(Tuple.Create(1.0, 1.0));
            row.DivideByZero.Item1.Should().Be(double.PositiveInfinity);
            row.DivideByZero.Item2.Should().Be(double.PositiveInfinity);
            row.Floating.Should().Be(Tuple.Create(5.0, 7.0));
            row.Wrapped.Should().Be(Tuple.Create(int.MinValue, 2),
                "the server computes Int64 and the declared Int32 cast wraps, the ClickHouse Int32 rule");
        }
    }

    [Fact]
    public void HammingAndDotProduct_ShouldReturnTheDeclaredScalarShapes()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bTupleEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                SameDistance = SqlFunctions.ClickHouse.tuple_hamming_distance(x.Pair, x.Pair),
                ThreeComponents = SqlFunctions.ClickHouse.tuple_hamming_distance(
                    Tuple.Create(1, 2, 3), Tuple.Create(1, 9, 3)),
                Dot = SqlFunctions.ClickHouse.dot_product(x.Same, x.Ints),
                DotDouble = SqlFunctions.ClickHouse.dot_product(x.Doubles, x.Doubles)
            })
            .First();

        using (new AssertionScope())
        {
            row.SameDistance.Should().Be(0L);
            row.ThreeComponents.Should().Be(1L);
            row.Dot.Should().Be(38L, "7*2 + 8*3");
            row.DotDouble.Should().Be(18.5, "2.5^2 + 3.5^2");
        }
    }

    [Fact]
    public void NullableComponents_ShouldKeepNullInTheResultAndRefuseTheAggregates()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var plus = ctx.From<IT286bTupleEntity>()
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.tuple_plus(x.NullableInts, x.NullableInts))
            .First();

        plus.Should().Be(Tuple.Create<int?, int?>(null, 8),
            "a NULL component stays NULL, the non-null component is added");

        FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bTupleEntity>()
                .Select(x => new { V = SqlFunctions.ClickHouse.tuple_hamming_distance(x.NullableInts, x.NullableInts) })))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*tuple_hamming_distance*refused*Nullable*");

        FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bTupleEntity>()
                .Select(x => new { V = SqlFunctions.ClickHouse.dot_product(x.NullableInts, x.NullableInts) })))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*dot_product*refused*Nullable*");
    }

    [Fact]
    public void Refusals_ShouldNeverReachTheServer()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        using (new AssertionScope())
        {
            FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bTupleEntity>()
                    .Select(x => new { V = SqlFunctions.ClickHouse.untuple(x.Pair) })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*untuple*refused*exactly one result column*");

            FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bTupleEntity>()
                    .Select(x => new { V = SqlFunctions.ClickHouse.tuple_to_name_value_pairs(x.Pair) })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*tuple_to_name_value_pairs*refused*just one type*");
        }
    }

    [Fact]
    public void Projection_ShouldMaterialiseEveryDeclaredShape()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bTupleEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Concat = SqlFunctions.ClickHouse.tuple_concat(x.Pair, x.Other),
                Names = SqlFunctions.ClickHouse.tuple_names(x.Named),
                Flat = SqlFunctions.ClickHouse.flatten_tuple(x.Nested),
                Pairs = SqlFunctions.ClickHouse.tuple_to_name_value_pairs(x.Same),
                Plus = SqlFunctions.ClickHouse.tuple_plus(x.Same, x.Same),
                Minus = SqlFunctions.ClickHouse.tuple_minus(x.Same, x.Ints),
                Multiply = SqlFunctions.ClickHouse.tuple_multiply(x.Ints, x.Ints),
                Divide = SqlFunctions.ClickHouse.tuple_divide(x.Ints, x.Ints),
                Distance = SqlFunctions.ClickHouse.tuple_hamming_distance(x.Pair, x.Pair),
                Dot = SqlFunctions.ClickHouse.dot_product(x.Same, x.Ints)
            })
            .First();

        using (new AssertionScope())
        {
            row.Concat.GetType().Should().Be(typeof(Tuple<int, string, double, string>));
            row.Names.GetType().Should().Be(typeof(string[]));
            row.Flat.GetType().Should().Be(typeof(Tuple<int, int, int>));
            row.Pairs.GetType().Should().Be(typeof(Tuple<string, int>[]));
            row.Plus.GetType().Should().Be(typeof(Tuple<int, int>));
            row.Minus.Should().Be(Tuple.Create(5, 5));
            row.Multiply.Should().Be(Tuple.Create(4, 9));
            row.Divide.GetType().Should().Be(typeof(Tuple<double, double>));
            row.Distance.GetType().Should().Be(typeof(long));
            row.Dot.GetType().Should().Be(typeof(long));
        }
    }
}
