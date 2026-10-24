using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using ClickHouse.Driver.ADO;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T286b D8 (F08 Higher-order): live execution of the lambda-bound ClickHouse array members against
/// the pinned <c>25.8-alpine</c> (25.8.33.6) server. The tests pin <c>arrayCumSumNonNegative</c>'s
/// negative-clamp rule and widened <c>Array(Int64)</c> result, the fold accumulator initialiser, the
/// first/last preservation of fill and the split boundary rule, the element/index/multi-argument
/// lambda bindings (one parameter per array, the <c>arrayEnumerate</c> index idiom), nesting and
/// captured values, and the deterministic refusals that never reach the server.
/// </summary>
public sealed class T286bF08HigherOrderFunctionTests : ProviderTestSuite
{
    [SqlTable("t286b_f08_entity")]
    internal interface IT286bHigherOrderEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("ints")]
        int[] Ints { get; set; }

        [Column("nints")]
        int?[] NullableInts { get; set; }

        [Column("longs")]
        long[] Longs { get; set; }

        [Column("strs")]
        string[] Strings { get; set; }

        [Column("k")]
        long K { get; set; }
    }

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    private static T Scalar<T>(string expression)
    {
        using var context = ClickHouseTestProvider.Instance.CreateContext();
        using var result = context.ExecuteRaw($"select {expression} as v");
        return result.Read<T>().Single();
    }

    private static void Seed()
    {
        using var connection = new ClickHouseConnection(ClickHouseContainer.ConnectionString);
        connection.Open();
        foreach (var statement in new[]
                 {
                     "drop table if exists t286b_f08_entity",
                     "create table t286b_f08_entity (id Int32, ints Array(Int32), nints Array(Nullable(Int32)), "
                     + "longs Array(Int64), strs Array(String), k Int64) engine = Memory",
                     "insert into t286b_f08_entity (id, ints, nints, longs, strs, k) values "
                     + "(1, [1, 2, 3, 2], [1, NULL, 3], [10, 20, 30, 40], ['a', 'b', 'c', 'd'], 2), "
                     + "(2, [], [], [], [], 0), "
                     + "(3, [-1, -2, 3], [NULL, NULL], [7], ['x'], 1), "
                     + "(4, [5, 1, 4, 2], [], [], [], 0), "
                     + "(5, [1, -2, 3, -4, 5], [], [], [], 0)"
                 })
        {
            using var command = connection.CreateCommand();
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }
    }

    [Fact]
    public void CumSumNonNegative_ShouldClampNegativeRunningSums()
    {
        using (new AssertionScope())
        {
            Scalar<string>("toString(arrayCumSumNonNegative(CAST([1, -2, 3, -4, 5] AS Array(Int32))))")
                .Should().Be("[1,0,3,0,5]");
            Scalar<string>("toString(arrayCumSumNonNegative(CAST([-1, -2] AS Array(Int32))))")
                .Should().Be("[0,0]", "every negative running sum is replaced by zero");
            Scalar<string>("toString(arrayCumSumNonNegative(CAST([] AS Array(Int32))))")
                .Should().Be("[]");
            Scalar<string>("toTypeName(arrayCumSumNonNegative(CAST([1, 2, 3] AS Array(Int32))))")
                .Should().Be("Array(Int64)");
        }

        Seed();
        using var ctx = Provider.CreateContext();
        var clamped = ctx.From<IT286bHigherOrderEntity>()
            .Where(x => x.Id == 5)
            .Select(x => new { CumSum = SqlFunctions.ClickHouse.array_cum_sum_non_negative(x.Ints) })
            .First();
        var empty = ctx.From<IT286bHigherOrderEntity>()
            .Where(x => x.Id == 2)
            .Select(x => new { CumSum = SqlFunctions.ClickHouse.array_cum_sum_non_negative(x.Ints) })
            .First();

        using (new AssertionScope())
        {
            clamped.CumSum.Should().Equal(1L, 0L, 3L, 0L, 5L);
            clamped.CumSum.GetType().Should().Be(typeof(long[]));
            empty.CumSum.Should().BeEmpty("an empty input yields an empty array");
        }
    }

    [Fact]
    public void Fold_ShouldUseTheCapturedAccumulatorInitialiser()
    {
        Scalar<long>("arrayFold((acc, x) -> acc + x, CAST([1, 2, 3, 4] AS Array(Int32)), toInt64(10))")
            .Should().Be(20L);

        Seed();
        using var ctx = Provider.CreateContext();
        long seed = 10;

        var row = ctx.From<IT286bHigherOrderEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Fold = SqlFunctions.ClickHouse.array_fold((acc, item) => acc + item, x.Ints, seed)
            })
            .First();
        var empty = ctx.From<IT286bHigherOrderEntity>()
            .Where(x => x.Id == 2)
            .Select(x => new
            {
                Fold = SqlFunctions.ClickHouse.array_fold((acc, item) => acc + item, x.Ints, seed)
            })
            .First();

        using (new AssertionScope())
        {
            row.Fold.Should().Be(18L, "10 + (1 + 2 + 3 + 2)");
            empty.Fold.Should().Be(10L, "an empty array keeps the initialiser");
        }
    }

    [Fact]
    public void FillAndSplit_ShouldPreserveTheBoundaryElements()
    {
        using (new AssertionScope())
        {
            Scalar<string>("toString(arrayFill(x -> not isNull(x), [1, NULL, 2, NULL]))").Should().Be("[1,1,2,2]");
            Scalar<string>("toString(arrayReverseFill(x -> not isNull(x), [1, NULL, 2, NULL]))").Should().Be("[1,2,2,NULL]");
            Scalar<string>("toString(arraySplit(x -> x > 1, [1, 2, 3]))").Should().Be("[[1],[2],[3]]");
            Scalar<string>("toString(arrayReverseSplit(x -> x > 1, [1, 2, 3]))").Should().Be("[[1,2],[3]]");
            Scalar<string>("toString(arraySplit(x -> x > 2, CAST([] AS Array(Int32))))").Should().Be("[]");
        }

        Seed();
        using var ctx = Provider.CreateContext();
        var row = ctx.From<IT286bHigherOrderEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Fill = SqlFunctions.ClickHouse.array_fill(v => v > 1, x.Ints),
                ReverseFill = SqlFunctions.ClickHouse.array_reverse_fill(v => v > 1, x.Ints),
                Split = SqlFunctions.ClickHouse.array_split(v => v > 2, x.Ints),
                ReverseSplit = SqlFunctions.ClickHouse.array_reverse_split(v => v > 2, x.Ints)
            })
            .First();

        using (new AssertionScope())
        {
            row.Fill.Should().Equal(1, 2, 3, 2);
            row.ReverseFill.Should().Equal(2, 2, 3, 2);
            row.Split.Should().HaveCount(2);
            row.Split[0].Should().Equal(1, 2);
            row.Split[1].Should().Equal(3, 2);
            row.ReverseSplit.Should().HaveCount(2);
            row.ReverseSplit[0].Should().Equal(1, 2, 3);
            row.ReverseSplit[1].Should().Equal(2);
        }
    }

    [Fact]
    public void IndexLambda_ShouldBindArrayEnumerateAsTheIndexArray()
    {
        using (new AssertionScope())
        {
            Scalar<string>("toString(arrayMap((i, x) -> i + x, arrayEnumerate(CAST([10, 20, 30] AS Array(Int32))), "
                + "CAST([10, 20, 30] AS Array(Int32))))").Should().Be("[11,22,33]");
            Scalar<string>("toString(arrayFilter((x, i) -> i % 2 = 0, CAST([1, 2, 3, 4] AS Array(Int32)), "
                + "arrayEnumerate(CAST([1, 2, 3, 4] AS Array(Int32)))))").Should().Be("[2,4]",
                "the source array is the first operand, so the result is a subset of the elements");
        }

        Seed();
        using var ctx = Provider.CreateContext();
        var row = ctx.From<IT286bHigherOrderEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Indexed = SqlFunctions.ClickHouse.array_map(
                    (i, v) => v + i,
                    SqlFunctions.ClickHouse.array_enumerate(x.Ints),
                    x.Ints),
                ByIndex = SqlFunctions.ClickHouse.array_filter(
                    (v, i) => i > 1,
                    x.Ints,
                    SqlFunctions.ClickHouse.array_enumerate(x.Ints))
            })
            .First();

        using (new AssertionScope())
        {
            row.Indexed.Should().Equal([2L, 4L, 6L, 6L], "one-based index plus element");
            row.Indexed.GetType().Should().Be(typeof(long[]));
            row.ByIndex.Should().Equal(2, 3, 2);
            row.ByIndex.GetType().Should().Be(typeof(int[]));
        }
    }

    [Fact]
    public void MultiArgumentLambda_ShouldBindEveryArray()
    {
        using (new AssertionScope())
        {
            Scalar<string>("toString(arrayMap((x, y) -> x + y, CAST([1, 2] AS Array(Int32)), "
                + "CAST([10, 20] AS Array(Int32))))").Should().Be("[11,22]");
            FluentActions.Invoking(() => Scalar<string>(
                    "toString(arrayMap((x, y) -> x + y, CAST([1, 2, 3] AS Array(Int32)), "
                    + "CAST([10, 20] AS Array(Int32))))"))
                .Should().Throw<Exception>().WithMessage("*must have equal size*",
                    "the server requires same-length arrays instead of truncating");
        }

        Seed();
        using var ctx = Provider.CreateContext();
        int[] first = [1, 2, 3];
        int[] second = [10, 20, 30];

        var row = ctx.From<IT286bHigherOrderEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Sum = SqlFunctions.ClickHouse.array_map((left, right) => left + right, first, second)
            })
            .First();

        row.Sum.Should().Equal(11, 22, 33);
    }

    [Fact]
    public void NestedLambda_ShouldResolveTheOuterParameter()
    {
        Scalar<string>("toString(arrayMap(x -> arrayMap(y -> y + x, CAST([1, 2] AS Array(Int32))), "
            + "CAST([10, 20] AS Array(Int32))))").Should().Be("[[11,12],[21,22]]");

        Seed();
        using var ctx = Provider.CreateContext();
        var row = ctx.From<IT286bHigherOrderEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Nested = SqlFunctions.ClickHouse.array_map(
                    outer => SqlFunctions.ClickHouse.array_map(inner => inner + outer, x.Ints), x.Ints)
            })
            .First();

        using (new AssertionScope())
        {
            row.Nested.Should().HaveCount(4);
            row.Nested[0].Should().Equal(2, 3, 4, 3);
            row.Nested[1].Should().Equal(3, 4, 5, 4);
            row.Nested[2].Should().Equal(4, 5, 6, 5);
            row.Nested[3].Should().Equal(3, 4, 5, 4);
            row.Nested.GetType().Should().Be(typeof(int[][]));
        }
    }

    [Fact]
    public void CapturedValue_ShouldBindInsideTheLambdaBody()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        int threshold = 2;

        var first = ctx.From<IT286bHigherOrderEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.ClickHouse.array_filter(v => v > threshold, x.Ints) })
            .First();
        var second = ctx.From<IT286bHigherOrderEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.ClickHouse.array_filter(v => v > threshold, x.Ints) })
            .First();

        first.V.Should().Equal(3);
        second.V.Should().Equal([3], "the plan-cache hit keeps the captured parameter binding");
    }

    [Fact]
    public void AggregatesAndSorts_ShouldReturnTheDeclaredShapes()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var row = ctx.From<IT286bHigherOrderEntity>()
            .Where(x => x.Id == 4)
            .Select(x => new
            {
                Sum = SqlFunctions.ClickHouse.array_sum(x.Ints),
                Minimum = SqlFunctions.ClickHouse.array_min(x.Ints),
                Maximum = SqlFunctions.ClickHouse.array_max(x.Ints),
                Average = SqlFunctions.ClickHouse.array_avg(x.Ints),
                Product = SqlFunctions.ClickHouse.array_product(x.Ints),
                Sorted = SqlFunctions.ClickHouse.array_sort(x.Ints),
                Reverse = SqlFunctions.ClickHouse.array_reverse_sort(x.Ints),
                Partial = SqlFunctions.ClickHouse.array_partial_sort(x.Ints, 2),
                Top = SqlFunctions.ClickHouse.array_top_k(x.Ints, 2),
                Bottom = SqlFunctions.ClickHouse.array_bottom_k(x.Ints, 2)
            })
            .First();

        using (new AssertionScope())
        {
            row.Sum.Should().Be(12L);
            row.Minimum.Should().Be(1);
            row.Maximum.Should().Be(5);
            row.Average.Should().Be(3.0);
            row.Product.Should().Be(40.0, "5 * 1 * 4 * 2");
            row.Sorted.Should().Equal(1, 2, 4, 5);
            row.Reverse.Should().Equal(5, 4, 2, 1);
            row.Partial.Take(2).Should().Equal([1, 2], "only the sorted prefix is deterministic");
            row.Top.Should().Equal(5, 4);
            row.Bottom.Should().Equal(1, 2);
        }
    }

    [Fact]
    public void EmptyArrays_ShouldReturnEmptyResultsOrDefaults()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var row = ctx.From<IT286bHigherOrderEntity>()
            .Where(x => x.Id == 2)
            .Select(x => new
            {
                CumSum = SqlFunctions.ClickHouse.array_cum_sum_non_negative(x.Ints),
                Fold = SqlFunctions.ClickHouse.array_fold((acc, item) => acc + item, x.Ints, 0L),
                Sum = SqlFunctions.ClickHouse.array_sum(x.Ints),
                Average = SqlFunctions.ClickHouse.array_avg(x.Ints),
                Product = SqlFunctions.ClickHouse.array_product(x.Ints),
                Fill = SqlFunctions.ClickHouse.array_fill(v => v > 0, x.Ints),
                Split = SqlFunctions.ClickHouse.array_split(v => v > 0, x.Ints),
                Sorted = SqlFunctions.ClickHouse.array_sort(x.Ints)
            })
            .First();

        using (new AssertionScope())
        {
            row.CumSum.Should().BeEmpty();
            row.Fold.Should().Be(0L);
            row.Sum.Should().Be(0L);
            row.Average.Should().Be(0.0);
            row.Product.Should().Be(0.0);
            row.Fill.Should().BeEmpty();
            row.Split.Should().BeEmpty();
            row.Sorted.Should().BeEmpty();
        }
    }

    [Fact]
    public void NullableElements_ShouldBeRejectedByTheServerForCumSumNonNegative()
    {
        // The declared long[] element type is not Nullable, and the server refuses a Nullable-element
        // accumulation outright (ILLEGAL_TYPE_OF_ARGUMENT); this is a server rule, not a CLR-shape
        // refusal, so it is asserted as the server exception rather than a silent 0 substitution.
        FluentActions.Invoking(() => Scalar<string>(
                "toString(arrayCumSumNonNegative(CAST([1, NULL, 3] AS Array(Nullable(Int32)))))"))
            .Should().Throw<Exception>()
            .WithMessage("*Nullable(Int32)*");
    }

    [Fact]
    public void Refusals_ShouldNeverReachTheServer()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        Expression<Func<int, int>> doubled = v => v + v;
        (long Start, long End)[] ranges = [(1L, 2L)];

        using (new AssertionScope())
        {
            FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bHigherOrderEntity>()
                    .Select(x => new { V = SqlFunctions.ClickHouse.array_cum_sum_non_negative(new double[] { 1.5 }) })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*array_cum_sum_non_negative*refused for element type*Double*declared long*");

            FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bHigherOrderEntity>()
                    .Select(x => new { V = SqlFunctions.ClickHouse.array_map(doubled, x.Ints) })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*array_map*requires an inline lambda argument*");

            FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bHigherOrderEntity>()
                    .Select(x => new { V = SqlFunctions.ClickHouse.array_map(s => s.Length, x.Strings) })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*Member access '.Length'*lambda parameter*");

            FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bHigherOrderEntity>()
                    .Select(x => new { V = SqlFunctions.ClickHouse.array_fold((acc, item) => acc, x.Ints, new int[0]) })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*accumulator type*Int32[]*refused*scalar*");

            FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bHigherOrderEntity>()
                    .Select(x => new { V = SqlFunctions.ClickHouse.array_reduce("sum", x.Ints) })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*array_reduce*refused*runtime SQL string*");

            FluentActions.Invoking(() => SqlOf(ctx, ctx.From<IT286bHigherOrderEntity>()
                    .Select(x => new { V = SqlFunctions.ClickHouse.array_reduce_in_ranges("sum", ranges, x.Ints) })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*array_reduce_in_ranges*refused*runtime SQL string*");
        }
    }

    [Fact]
    public void Projection_ShouldMaterialiseEveryDeliveredMember()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var row = ctx.From<IT286bHigherOrderEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Fold = SqlFunctions.ClickHouse.array_fold((acc, item) => acc + item, x.Ints, 0L),
                CumSumNonNegative = SqlFunctions.ClickHouse.array_cum_sum_non_negative(x.Ints),
                Fill = SqlFunctions.ClickHouse.array_fill(v => v > 1, x.Ints),
                ReverseFill = SqlFunctions.ClickHouse.array_reverse_fill(v => v > 1, x.Ints),
                Sorted = SqlFunctions.ClickHouse.array_sort(x.Ints),
                ReverseSort = SqlFunctions.ClickHouse.array_reverse_sort(x.Ints),
                PartialSort = SqlFunctions.ClickHouse.array_partial_sort(x.Ints, 2),
                Split = SqlFunctions.ClickHouse.array_split(v => v > 2, x.Ints),
                ReverseSplit = SqlFunctions.ClickHouse.array_reverse_split(v => v > 2, x.Ints),
                Sum = SqlFunctions.ClickHouse.array_sum(x.Ints),
                Minimum = SqlFunctions.ClickHouse.array_min(x.Ints),
                Maximum = SqlFunctions.ClickHouse.array_max(x.Ints),
                Average = SqlFunctions.ClickHouse.array_avg(x.Ints),
                Product = SqlFunctions.ClickHouse.array_product(x.Ints),
                Top = SqlFunctions.ClickHouse.array_top_k(x.Ints, 2),
                Bottom = SqlFunctions.ClickHouse.array_bottom_k(x.Ints, 2)
            })
            .First();

        using (new AssertionScope())
        {
            row.Fold.Should().Be(8L);
            row.CumSumNonNegative.Should().Equal(1L, 3L, 6L, 8L);
            row.Fill.Should().Equal(1, 2, 3, 2);
            row.ReverseFill.Should().Equal(2, 2, 3, 2);
            row.Sorted.Should().Equal(1, 2, 2, 3);
            row.ReverseSort.Should().Equal(3, 2, 2, 1);
            row.PartialSort.Should().HaveCount(4);
            row.PartialSort.Take(2).Should().Equal(1, 2);
            row.Split.Should().HaveCount(2);
            row.ReverseSplit.Should().HaveCount(2);
            row.Sum.Should().Be(8L);
            row.Minimum.Should().Be(1);
            row.Maximum.Should().Be(3);
            row.Average.Should().Be(2.0);
            row.Product.Should().Be(12.0);
            row.Top.Should().Equal(3, 2);
            row.Bottom.Should().Equal(1, 2);

            row.CumSumNonNegative.GetType().Should().Be(typeof(long[]));
            row.Split.GetType().Should().Be(typeof(int[][]));
            row.ReverseSplit.GetType().Should().Be(typeof(int[][]));
            row.Top.GetType().Should().Be(typeof(int[]));
        }
    }
}
