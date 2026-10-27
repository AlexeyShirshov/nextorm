using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.SqlServer;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// T286b D8 (F08 Higher-order): the lambda binding of the ClickHouse-only higher-order array members.
/// The tests pin every arm that this unit owns or verifies (including the members delivered by D5,
/// which F08 closes as already-covered), the new <c>arrayCumSumNonNegative</c> result cast, the
/// element/index/multiple-argument lambda shapes (one parameter per array, the documented
/// <c>arrayEnumerate</c> index idiom), captured values in the parameter-only pass, nesting, and the
/// deterministic negative binding cases (non-inline lambda, parameter member access, non-scalar fold
/// accumulator, refused reduce members). They never open a connection.
/// </summary>
public class T286bF08HigherOrderFunctionSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

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

    /// <summary>The delivered/verified catalog names, one per non-refused member.</summary>
    private static readonly string[] MemberNames =
    [
        "array_fold", "array_cum_sum_non_negative", "array_fill", "array_reverse_fill",
        "array_sort", "array_reverse_sort", "array_partial_sort", "array_split",
        "array_reverse_split", "array_sum", "array_min", "array_max", "array_avg",
        "array_product", "array_top_k", "array_bottom_k"
    ];

    [Fact]
    public void Render_ShouldEmitTheExactClickHouseArmForEveryDeliveredMember()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bHigherOrderEntity>();

        using (new AssertionScope())
        {
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_fold((acc, item) => acc + item, x.Ints, 0L) }))
                .Should().Contain("arrayFold((acc, item) -> (acc + cast(item as Int64)), ints, cast(0 as Int64))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_cum_sum_non_negative(x.Ints) }))
                .Should().Contain("cast(arrayCumSumNonNegative(ints) as Array(Int64))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_fill(v => v > 0, x.Ints) }))
                .Should().Contain("cast(arrayFill(v -> (v > 0), ints) as Array(Int32))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_reverse_fill(v => v > 0, x.Ints) }))
                .Should().Contain("cast(arrayReverseFill(v -> (v > 0), ints) as Array(Int32))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_sort(x.Ints) }))
                .Should().Contain("arraySort(ints)");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_reverse_sort(x.Ints) }))
                .Should().Contain("cast(arrayReverseSort(ints) as Array(Int32))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_partial_sort(x.Ints, 2L) }))
                .Should().Contain("cast(arrayPartialSort(2, ints) as Array(Int32))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_split(v => v > 1, x.Ints) }))
                .Should().Contain("cast(arraySplit(v -> (v > 1), ints) as Array(Array(Int32)))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_reverse_split(v => v > 1, x.Ints) }))
                .Should().Contain("cast(arrayReverseSplit(v -> (v > 1), ints) as Array(Array(Int32)))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_sum(x.Ints) }))
                .Should().Contain("arraySum(ints)");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_min(x.Ints) }))
                .Should().Contain("arrayMin(ints)");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_max(x.Ints) }))
                .Should().Contain("arrayMax(ints)");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_avg(x.Ints) }))
                .Should().Contain("arrayAvg(ints)");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_product(x.Ints) }))
                .Should().Contain("arrayProduct(ints)");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_top_k(x.Ints, 2L) }))
                .Should().Contain("cast(arraySlice(arrayReverseSort(arrayFilter(_x -> isNotNull(_x), ints)), 1, 2) as Array(Int32))");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_bottom_k(x.Ints, 2L) }))
                .Should().Contain("cast(arraySlice(arraySort(arrayFilter(_x -> isNotNull(_x), ints)), 1, 2) as Array(Int32))");
        }
    }

    [Fact]
    public void CumSumNonNegative_ShouldCastToLongArrayAndRefuseNonIntegralElements()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bHigherOrderEntity>();

        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_cum_sum_non_negative(x.Ints) }))
            .Should().Contain("cast(arrayCumSumNonNegative(ints) as Array(Int64)) as `V`");

        // An empty captured array still binds as one typed parameter and keeps the declared long[].
        int[] empty = [];
        var command = Prepare(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_cum_sum_non_negative(empty) }));
        Normalize(command.DbCommand.CommandText)
            .Should().Contain("cast(arrayCumSumNonNegative(@p0) as Array(Int64)) as `V`");
        command.DbCommandParams.Cast<System.Data.Common.DbParameter>().Should().ContainSingle();

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new
            {
                V = SqlFunctions.ClickHouse.array_cum_sum_non_negative(new double[] { 1.5 })
            })))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*array_cum_sum_non_negative*refused for element type*Double*declared long*");
    }

    [Fact]
    public void Fold_ShouldBindTheTwoArgumentLambdaAndItsCapturedAccumulator()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bHigherOrderEntity>();

        long seed = 10;
        var command = Prepare(ctx, e.Select(x => new
        {
            V = SqlFunctions.ClickHouse.array_fold((acc, item) => acc + item, x.Ints, seed)
        }));

        Normalize(command.DbCommand.CommandText)
            .Should().Contain("arrayFold((acc, item) -> (acc + cast(item as Int64)), ints, cast(@seed as Int64)) as `V`");
        command.DbCommandParams.Cast<System.Data.Common.DbParameter>().Should().ContainSingle();
    }

    [Fact]
    public void ElementLambda_ShouldBindACapturedValueInsideTheBody()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bHigherOrderEntity>();

        int threshold = 2;
        var command = Prepare(ctx, e.Select(x => new
        {
            V = SqlFunctions.ClickHouse.array_filter(v => v > threshold, x.Ints)
        }));

        Normalize(command.DbCommand.CommandText).Should().Contain("arrayFilter(v -> (v > @threshold), ints) as `V`");
        command.DbCommandParams.Cast<System.Data.Common.DbParameter>().Should().ContainSingle();

        // Plan-cache hit: the second preparation renders the identical SQL and parameter list.
        var second = Prepare(ctx, e.Select(x => new
        {
            V = SqlFunctions.ClickHouse.array_filter(v => v > threshold, x.Ints)
        }));
        Normalize(second.DbCommand.CommandText).Should().Be(Normalize(command.DbCommand.CommandText));
        second.DbCommandParams.Cast<System.Data.Common.DbParameter>().Should().ContainSingle();
    }

    [Fact]
    public void IndexLambda_ShouldBindTheEnumeratedArrayAsASecondArgument()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bHigherOrderEntity>();

        // ClickHouse has no separate index parameter: the documented idiom passes arrayEnumerate as
        // the additional array, so the lambda has one parameter per array.
        SqlOf(ctx, e.Select(x => new
            {
                V = SqlFunctions.ClickHouse.array_map((i, v) => v + i, SqlFunctions.ClickHouse.array_enumerate(x.Ints), x.Ints)
            }))
            .Should().Contain("cast(arrayMap((i, v) -> (cast(v as Int64) + i), arrayEnumerate(ints), ints) as Array(Int64)) as `V`");

        SqlOf(ctx, e.Select(x => new
            {
                V = SqlFunctions.ClickHouse.array_filter((v, i) => i > 1, x.Ints, SqlFunctions.ClickHouse.array_enumerate(x.Ints))
            }))
            .Should().Contain("arrayFilter((v, i) -> (i > 1), ints, arrayEnumerate(ints)) as `V`");
    }

    [Fact]
    public void MultiArgumentLambda_ShouldBindEveryArrayOperandInOrder()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bHigherOrderEntity>();

        int[] second = [10, 20, 30];
        var command = Prepare(ctx, e.Select(x => new
        {
            V = SqlFunctions.ClickHouse.array_map((left, right) => left + right, x.Ints, second)
        }));

        var sql = Normalize(command.DbCommand.CommandText);
        sql.Should().Contain("cast(arrayMap((left, right) -> (left + right), ints, @p0) as Array(Int32)) as `V`",
            "one lambda parameter is bound per array operand, in argument order");
        command.DbCommandParams.Cast<System.Data.Common.DbParameter>().Should().ContainSingle()
            .Which.ParameterName.Should().Be("p0");
    }

    [Fact]
    public void NestedLambda_ShouldResolveTheOuterParameterInsideTheInnerBody()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bHigherOrderEntity>();

        SqlOf(ctx, e.Select(x => new
            {
                V = SqlFunctions.ClickHouse.array_map(
                    outer => SqlFunctions.ClickHouse.array_map(inner => inner + outer, x.Ints), x.Ints)
            }))
            .Should().Contain(
                "cast(arrayMap(outer -> cast(arrayMap(inner -> (inner + outer), ints) as Array(Int32)), ints) as Array(Array(Int32))) as `V`");
    }

    [Fact]
    public void NonInlineLambda_ShouldBeRefusedWithTheFunctionName()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bHigherOrderEntity>();

        Expression<Func<int, int>> doubled = v => v + v;

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new
            {
                V = SqlFunctions.ClickHouse.array_map(doubled, x.Ints)
            })))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*array_map*requires an inline lambda argument*not part of the query expression tree*");

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new
            {
                V = SqlFunctions.ClickHouse.array_fold((acc, item) => acc + item, x.Ints, 0L)
            })))
            .Should().NotThrow("the inline lambda remains the supported positive form");
    }

    [Fact]
    public void MemberAccessOnTheLambdaParameter_ShouldBeRefusedWithTheMemberName()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bHigherOrderEntity>();

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new
            {
                V = SqlFunctions.ClickHouse.array_map(s => s.Length, x.Strings)
            })))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*Member access '.Length'*lambda parameter*bare SQL element*");
    }

    [Fact]
    public void FoldWithNonScalarAccumulator_ShouldBeRefused()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bHigherOrderEntity>();

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new
            {
                V = SqlFunctions.ClickHouse.array_fold((acc, item) => acc, x.Ints, new int[0])
            })))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*accumulator type*Int32[]*refused*scalar*");
    }

    [Fact]
    public void ReduceMembers_ShouldRefuseDeterministicallyOnEveryProvider()
    {
        using var ctx = ClickHouseTestContext.Create();
        using var postgres = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        var e = ctx.From<IT286bHigherOrderEntity>();

        // A tuple-array literal cannot appear in an expression tree (CS8143), so the ranges are a
        // captured value; the refusal still wins before any operand is translated.
        (long Start, long End)[] ranges = [(1L, 2L)];

        using (new AssertionScope())
        {
            FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new
                {
                    V = SqlFunctions.ClickHouse.array_reduce("sum", x.Ints)
                })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*array_reduce*refused*runtime SQL string*no typed aggregate contract*");

            FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new
                {
                    V = SqlFunctions.ClickHouse.array_reduce_in_ranges("sum", ranges, x.Ints)
                })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*array_reduce_in_ranges*refused*runtime SQL string*no typed aggregate contract*");

            // The refusal wins before the provider gate, so a non-ClickHouse context reports the
            // precise CLR-shape reason instead of a generic provider message.
            FluentActions.Invoking(() => SqlOf(postgres, postgres.From<IT286bHigherOrderEntity>().Select(x => new
                {
                    V = SqlFunctions.ClickHouse.array_reduce_in_ranges("sum", ranges, x.Ints)
                })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*array_reduce_in_ranges*refused*runtime SQL string*");

            // SqlFunctions.ClickHouse is a query-expression placeholder (default!), so the member's
            // own thrower is reached through an instance instead of the placeholder.
            var functions = new ClickHouseFunctions();
            FluentActions.Invoking(() => functions.array_reduce<int>("sum", [1, 2]))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*array_reduce*refused*");
            FluentActions.Invoking(() => functions.array_reduce_in_ranges<int>("sum", [(1L, 2L)], [1, 2]))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*array_reduce_in_ranges*refused*");
        }
    }

    [Fact]
    public void NullableElements_ShouldKeepTheDeclaredNullableElementTypeInTheHigherOrderArms()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bHigherOrderEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Fill = SqlFunctions.ClickHouse.array_fill(v => v != null, x.NullableInts),
            ReverseFill = SqlFunctions.ClickHouse.array_reverse_fill(v => v != null, x.NullableInts),
            Split = SqlFunctions.ClickHouse.array_split(v => v != null, x.NullableInts)
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain("cast(arrayFill(v -> v is not null, nints) as Array(Nullable(Int32)))");
            sql.Should().Contain("cast(arrayReverseFill(v -> v is not null, nints) as Array(Nullable(Int32)))");
            sql.Should().Contain("cast(arraySplit(v -> v is not null, nints) as Array(Array(Nullable(Int32))))");
        }
    }

    [Fact]
    public void DeclaredShapes_ShouldMatchTheNativeResultTypes()
    {
        typeof(ClickHouseFunctions).GetMethod("array_cum_sum_non_negative")!.ReturnType.Should().Be(typeof(long[]));
        typeof(ClickHouseFunctions).GetMethod("array_sum")!.ReturnType.Should().Be(typeof(long));
        typeof(ClickHouseFunctions).GetMethod("array_fold")!.ReturnType.IsGenericParameter.Should().BeTrue();

        var topK = typeof(ClickHouseFunctions).GetMethod("array_top_k")!.ReturnType;
        topK.IsArray.Should().BeTrue();
        topK.GetElementType()!.IsGenericParameter.Should().BeTrue();
        typeof(ClickHouseFunctions).GetMethod("array_reduce_in_ranges")!.ReturnType.IsGenericParameter.Should().BeTrue();
    }

    [Fact]
    public void Projection_ShouldRenderEveryDeliveredMember()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bHigherOrderEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Fold = SqlFunctions.ClickHouse.array_fold((acc, item) => acc + item, x.Ints, 0L),
            CumSumNonNegative = SqlFunctions.ClickHouse.array_cum_sum_non_negative(x.Ints),
            Top = SqlFunctions.ClickHouse.array_top_k(x.Ints, 1L),
            Bottom = SqlFunctions.ClickHouse.array_bottom_k(x.Ints, 1L),
            Sorted = SqlFunctions.ClickHouse.array_sort(x.Ints),
            Reversed = SqlFunctions.ClickHouse.array_reverse_sort(x.Ints),
            PartialSorted = SqlFunctions.ClickHouse.array_partial_sort(x.Ints, 1L),
            Filled = SqlFunctions.ClickHouse.array_fill(v => v > 0, x.Ints),
            ReverseFilled = SqlFunctions.ClickHouse.array_reverse_fill(v => v > 0, x.Ints),
            Split = SqlFunctions.ClickHouse.array_split(v => v > 1, x.Ints),
            ReverseSplit = SqlFunctions.ClickHouse.array_reverse_split(v => v > 1, x.Ints),
            Sum = SqlFunctions.ClickHouse.array_sum(x.Ints),
            Min = SqlFunctions.ClickHouse.array_min(x.Ints),
            Max = SqlFunctions.ClickHouse.array_max(x.Ints),
            Avg = SqlFunctions.ClickHouse.array_avg(x.Ints),
            Product = SqlFunctions.ClickHouse.array_product(x.Ints)
        }));

        using (new AssertionScope())
        {
            foreach (var alias in new[]
                     {
                         "Fold", "CumSumNonNegative", "Top", "Bottom", "Sorted", "Reversed",
                         "PartialSorted", "Filled", "ReverseFilled", "Split", "ReverseSplit",
                         "Sum", "Min", "Max", "Avg", "Product"
                     })
            {
                sql.Should().Contain($"as `{alias}`");
            }
        }
    }

    [Fact]
    public void Predicate_ShouldKeepTheHigherOrderFunctionsInTheWhereClause()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bHigherOrderEntity>();

        var sql = SqlOf(ctx, e.Where(x => SqlFunctions.ClickHouse.array_exists(v => v > 5, x.Ints))
            .Select(x => new { x.Id }));

        sql.Should().Contain("arrayExists(v -> (v > 5), ints)");
        sql.Should().NotContain("as `arrayExists`");
    }

    [Fact]
    public void QuotedIdentifiers_ShouldPreserveTheColumnAndAlias()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bHigherOrderEntity>();

        SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_cum_sum_non_negative(x.Ints) }))
            .Should().Contain("cast(arrayCumSumNonNegative(ints) as Array(Int64)) as `V`");
    }

    [Fact]
    public void PostgresAndSqlServer_ShouldRejectEveryDeliveredMember()
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
                "array_fold" => () => SqlOf(ctx, ctx.From<IT286bHigherOrderEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_fold((acc, item) => acc + item, x.Ints, 0L) })),
                "array_cum_sum_non_negative" => () => SqlOf(ctx, ctx.From<IT286bHigherOrderEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_cum_sum_non_negative(x.Ints) })),
                "array_fill" => () => SqlOf(ctx, ctx.From<IT286bHigherOrderEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_fill(v => v > 0, x.Ints) })),
                "array_reverse_fill" => () => SqlOf(ctx, ctx.From<IT286bHigherOrderEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_reverse_fill(v => v > 0, x.Ints) })),
                "array_sort" => () => SqlOf(ctx, ctx.From<IT286bHigherOrderEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_sort(x.Ints) })),
                "array_reverse_sort" => () => SqlOf(ctx, ctx.From<IT286bHigherOrderEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_reverse_sort(x.Ints) })),
                "array_partial_sort" => () => SqlOf(ctx, ctx.From<IT286bHigherOrderEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_partial_sort(x.Ints, 2L) })),
                "array_split" => () => SqlOf(ctx, ctx.From<IT286bHigherOrderEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_split(v => v > 0, x.Ints) })),
                "array_reverse_split" => () => SqlOf(ctx, ctx.From<IT286bHigherOrderEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_reverse_split(v => v > 0, x.Ints) })),
                "array_sum" => () => SqlOf(ctx, ctx.From<IT286bHigherOrderEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_sum(x.Ints) })),
                "array_min" => () => SqlOf(ctx, ctx.From<IT286bHigherOrderEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_min(x.Ints) })),
                "array_max" => () => SqlOf(ctx, ctx.From<IT286bHigherOrderEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_max(x.Ints) })),
                "array_avg" => () => SqlOf(ctx, ctx.From<IT286bHigherOrderEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_avg(x.Ints) })),
                "array_product" => () => SqlOf(ctx, ctx.From<IT286bHigherOrderEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_product(x.Ints) })),
                "array_top_k" => () => SqlOf(ctx, ctx.From<IT286bHigherOrderEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_top_k(x.Ints, 2L) })),
                "array_bottom_k" => () => SqlOf(ctx, ctx.From<IT286bHigherOrderEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_bottom_k(x.Ints, 2L) })),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, "not part of the F08 slice")
            };

            FluentActions.Invoking(query).Should().Throw<NotSupportedException>()
                .WithMessage("*require a provider*ClickHouse*");
        }
    }
}
