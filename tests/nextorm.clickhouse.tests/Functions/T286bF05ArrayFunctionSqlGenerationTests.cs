using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.SqlServer;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// T286b D5 (F05 Array): the ClickHouse-only array members. The tests pin every rendered arm
/// (including the version-gap definitions rendered because the native names arrived after the pinned
/// 25.8 server), the declared CLR shapes, the array-parameter binding (a captured array becomes one
/// parameter, never a per-element list), the two CLR-shape guards and the fail-closed refusal of every
/// other provider. They never open a connection.
/// </summary>
public class T286bF05ArrayFunctionSqlGenerationTests
{
    private static readonly double[] Weights = [1.0, 1.0];

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    [SqlTable("t286b_f05_entity")]
    internal interface IT286bArrayEntity
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

        [Column("dbls")]
        double[] Doubles { get; set; }

        [Column("strs")]
        string[] Strings { get; set; }

        [Column("nested")]
        long[][] Nested { get; set; }

        [Column("k")]
        long K { get; set; }

        [Column("elem")]
        int Element { get; set; }
    }

    /// <summary>The delivered catalog names, one per implemented member.</summary>
    private static readonly string[] MemberNames =
    [
        "array_element", "array_enumerate_dense", "array_zip", "array_difference", "array_avg",
        "array_sum", "array_min", "array_max", "array_product", "array_top_k", "array_bottom_k",
        "array_random_sample", "array_compact", "array_pop_back", "array_pop_front",
        "array_push_front", "array_resize", "array_remove", "array_rotate_left", "array_rotate_right",
        "array_shift_left", "array_shift_right", "array_fold", "array_fill", "array_reverse_fill",
        "array_split", "array_reverse_split", "array_reverse_sort", "array_partial_sort",
        "array_shingles", "array_transpose", "count_equal", "array_with_constant",
        "array_jaccard_index", "array_similarity"
    ];

    [Fact]
    public void Render_ShouldEmitTheExactClickHouseArmForEveryMember()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bArrayEntity>();

        using (new AssertionScope())
        {
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_element(x.Ints, 2) }))
                .Should().Contain("arrayElement(ints, 2) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_element(x.NullableInts, -1) }))
                .Should().Contain("arrayElement(nints, -1) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_enumerate_dense(x.Ints) }))
                .Should().Contain("cast(arrayEnumerateDense(ints) as Array(Int64)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_zip(x.Ints, x.Strings) }))
                .Should().Contain("arrayZip(ints, strs) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_difference(x.Ints) }))
                .Should().Contain("cast(arrayDifference(ints) as Array(Int64)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_avg(x.Ints) }))
                .Should().Contain("arrayAvg(ints) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_sum(x.Ints) }))
                .Should().Contain("arraySum(ints) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_min(x.Ints) }))
                .Should().Contain("arrayMin(ints) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_max(x.Longs) }))
                .Should().Contain("arrayMax(longs) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_product(x.Ints) }))
                .Should().Contain("arrayProduct(ints) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_random_sample(x.Ints, 2) }))
                .Should().Contain("cast(arrayRandomSample(ints, 2) as Array(Int32)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_compact(x.NullableInts) }))
                .Should().Contain("cast(arrayCompact(nints) as Array(Nullable(Int32))) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_pop_back(x.Ints) }))
                .Should().Contain("cast(arrayPopBack(ints) as Array(Int32)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_pop_front(x.Ints) }))
                .Should().Contain("cast(arrayPopFront(ints) as Array(Int32)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_push_front(x.Ints, 7) }))
                .Should().Contain("cast(arrayPushFront(ints, 7) as Array(Int32)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_resize(x.Ints, 4) }))
                .Should().Contain("cast(arrayResize(ints, 4) as Array(Int32)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_resize(x.Ints, 4, 9) }))
                .Should().Contain("cast(arrayResize(ints, 4, 9) as Array(Int32)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_rotate_left(x.Ints, 2) }))
                .Should().Contain("cast(arrayRotateLeft(ints, 2) as Array(Int32)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_rotate_right(x.Ints, 2) }))
                .Should().Contain("cast(arrayRotateRight(ints, 2) as Array(Int32)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_shift_left(x.Ints, 2) }))
                .Should().Contain("cast(arrayShiftLeft(ints, 2) as Array(Int32)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_shift_left(x.Ints, 2, 42) }))
                .Should().Contain("cast(arrayShiftLeft(ints, 2, 42) as Array(Int32)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_shift_right(x.Ints, 2) }))
                .Should().Contain("cast(arrayShiftRight(ints, 2) as Array(Int32)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_shift_right(x.Ints, 2, 42) }))
                .Should().Contain("cast(arrayShiftRight(ints, 2, 42) as Array(Int32)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_reverse_sort(x.Ints) }))
                .Should().Contain("cast(arrayReverseSort(ints) as Array(Int32)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_partial_sort(x.Ints, 2) }))
                .Should().Contain("cast(arrayPartialSort(2, ints) as Array(Int32)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_shingles(x.Ints, 2) }))
                .Should().Contain("cast(arrayShingles(ints, 2) as Array(Array(Int32))) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.count_equal(x.Ints, 2) }))
                .Should().Contain("toInt64(countEqual(ints, 2)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_with_constant(3, 7) }))
                .Should().Contain("cast(arrayWithConstant(3, 7) as Array(Int32)) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_jaccard_index(x.Longs, x.Longs) }))
                .Should().Contain("arrayJaccardIndex(longs, longs) as `V`");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_similarity(x.Strings, x.Strings, Weights, Weights) }))
                .Should().Contain("arraySimilarity(strs, strs, ", "the four-argument form is the only one on the pinned 25.8 server");
        }
    }

    [Fact]
    public void VersionGapArms_ShouldRenderTheVendorDefinitions()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bArrayEntity>();

        using (new AssertionScope())
        {
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_top_k(x.Ints, 2) }))
                .Should().Contain("cast(arraySlice(arrayReverseSort(arrayFilter(_x -> isNotNull(_x), ints)), 1, 2) as Array(Int32)) as `V`",
                    "arrayTopK arrived in 26.6; the definition keeps the sorted, null-skipping contract");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_bottom_k(x.Ints, 2) }))
                .Should().Contain("cast(arraySlice(arraySort(arrayFilter(_x -> isNotNull(_x), ints)), 1, 2) as Array(Int32)) as `V`",
                    "arrayBottomK arrived in 26.6; the definition keeps the sorted, null-skipping contract");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_remove(x.NullableInts, 3) }))
                .Should().Contain(
                    "cast(arrayFilter(_x -> not coalesce(_x = 3, isNull(_x) AND isNull(3)), nints) as Array(Nullable(Int32))) as `V`",
                    "arrayRemove arrived in 25.11; the definition treats NULLs as equal");
            SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_transpose(x.Nested) }))
                .Should().Contain(
                    "cast(arrayMap(_i -> arrayMap(_r -> _r[_i], (nested)), range(1, length((nested)[1]) + 1)) as Array(Array(Int64))) as `V`",
                    "arrayTranspose arrived in 26.4; the definition is the transposition itself");
        }
    }

    [Fact]
    public void VersionGapArms_ShouldNotDeclareTheNativeNamesAsScalarFunctions()
    {
        using (new AssertionScope())
        {
            ClickHouseDialect.Instance.SupportsArrayFunctions.Should().BeTrue();
            ClickHouseDialect.Instance.SupportsHigherOrderArrayFunctions.Should().BeTrue();

            PostgresDialect.Instance.SupportsArrayFunctions.Should().BeFalse("the array surface is ClickHouse-only");
            SqlServerDialect.Instance.SupportsArrayFunctions.Should().BeFalse("the array surface is ClickHouse-only");
        }
    }

    [Fact]
    public void DeclaredShapes_ShouldMatchTheNativeResultTypes()
    {
        // (member, generic arguments, parameter types, declared result)
        (string Member, Type[] Generic, Type[] Parameters, Type Result)[] rows =
        [
            ("array_element", [typeof(int)], [typeof(int[]), typeof(long)], typeof(int)),
            ("array_element", [typeof(int?)], [typeof(int?[]), typeof(long)], typeof(int?)),
            ("array_enumerate_dense", [typeof(int)], [typeof(int[])], typeof(long[])),
            ("array_zip", [typeof(int), typeof(string)], [typeof(int[]), typeof(string[])], typeof(Tuple<int, string>[])),
            ("array_difference", [typeof(int)], [typeof(int[])], typeof(long[])),
            ("array_avg", [typeof(int)], [typeof(int[])], typeof(double)),
            ("array_sum", [typeof(int)], [typeof(int[])], typeof(long)),
            ("array_min", [typeof(int)], [typeof(int[])], typeof(int)),
            ("array_max", [typeof(string)], [typeof(string[])], typeof(string)),
            ("array_product", [typeof(int)], [typeof(int[])], typeof(double)),
            ("array_top_k", [typeof(int)], [typeof(int[]), typeof(long)], typeof(int[])),
            ("array_bottom_k", [typeof(int)], [typeof(int[]), typeof(long)], typeof(int[])),
            ("array_random_sample", [typeof(int)], [typeof(int[]), typeof(long)], typeof(int[])),
            ("array_compact", [typeof(int?)], [typeof(int?[])], typeof(int?[])),
            ("array_pop_back", [typeof(int)], [typeof(int[])], typeof(int[])),
            ("array_pop_front", [typeof(int)], [typeof(int[])], typeof(int[])),
            ("array_push_front", [typeof(int)], [typeof(int[]), typeof(int)], typeof(int[])),
            ("array_resize", [typeof(int)], [typeof(int[]), typeof(long)], typeof(int[])),
            ("array_resize", [typeof(int)], [typeof(int[]), typeof(long), typeof(int)], typeof(int[])),
            ("array_remove", [typeof(int?)], [typeof(int?[]), typeof(int?)], typeof(int?[])),
            ("array_rotate_left", [typeof(int)], [typeof(int[]), typeof(long)], typeof(int[])),
            ("array_rotate_right", [typeof(int)], [typeof(int[]), typeof(long)], typeof(int[])),
            ("array_shift_left", [typeof(int)], [typeof(int[]), typeof(long)], typeof(int[])),
            ("array_shift_left", [typeof(int)], [typeof(int[]), typeof(long), typeof(int)], typeof(int[])),
            ("array_shift_right", [typeof(int)], [typeof(int[]), typeof(long)], typeof(int[])),
            ("array_shift_right", [typeof(int)], [typeof(int[]), typeof(long), typeof(int)], typeof(int[])),
            ("array_fold", [typeof(int), typeof(long)],
                [typeof(System.Linq.Expressions.Expression<Func<long, int, long>>), typeof(int[]), typeof(long)], typeof(long)),
            ("array_fill", [typeof(int)],
                [typeof(System.Linq.Expressions.Expression<Func<int, bool>>), typeof(int[])], typeof(int[])),
            ("array_reverse_fill", [typeof(int)],
                [typeof(System.Linq.Expressions.Expression<Func<int, bool>>), typeof(int[])], typeof(int[])),
            ("array_split", [typeof(int)],
                [typeof(System.Linq.Expressions.Expression<Func<int, bool>>), typeof(int[])], typeof(int[][])),
            ("array_reverse_split", [typeof(int)],
                [typeof(System.Linq.Expressions.Expression<Func<int, bool>>), typeof(int[])], typeof(int[][])),
            ("array_reverse_sort", [typeof(int)], [typeof(int[])], typeof(int[])),
            ("array_partial_sort", [typeof(int)], [typeof(int[]), typeof(long)], typeof(int[])),
            ("array_shingles", [typeof(int)], [typeof(int[]), typeof(long)], typeof(int[][])),
            ("array_transpose", [typeof(int)], [typeof(int[][])], typeof(int[][])),
            ("count_equal", [typeof(int)], [typeof(int[]), typeof(int)], typeof(long)),
            ("array_with_constant", [typeof(int)], [typeof(long), typeof(int)], typeof(int[])),
            ("array_jaccard_index", [typeof(int)], [typeof(int[]), typeof(int[])], typeof(double)),
            ("array_similarity", [typeof(string)],
                [typeof(string[]), typeof(string[]), typeof(double[]), typeof(double[])], typeof(double)),
            ("array_reduce", [typeof(int)], [typeof(string), typeof(int[])], typeof(int))
        ];

        using (new AssertionScope())
        {
            foreach (var row in rows)
            {
                Declared(row.Member, row.Generic, row.Parameters).Should().Be(
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

        static Type Declared(string member, Type[] generic, Type[] parameters) => Resolve(member, generic, parameters).ReturnType;
    }

    [Fact]
    public void Refusal_ShouldThrowThePreciseReasonForArrayReduce()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bArrayEntity>();

        FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.array_reduce("sum", x.Ints))))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*array_reduce*refused*runtime SQL string*no typed aggregate contract*");

        // The precise reason wins over the provider gate, so it is the same on a provider without arrays.
        using var postgres = new PostgresDataContext("Host=localhost", new DataContextBuilder());
        FluentActions.Invoking(() => SqlOf(postgres, postgres.From<IT286bArrayEntity>()
                .Select(x => SqlFunctions.ClickHouse.array_reduce("sum", x.Ints))))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*array_reduce*refused*runtime SQL string*");
    }

    [Fact]
    public void ElementTypeGuards_ShouldRefuseAggregationsTheDeclaredClrCannotCarry()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bArrayEntity>();

        using (new AssertionScope())
        {
            FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_sum(x.Doubles) })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*array_sum*refused for element type*Double*float*decimal*declared long*");

            FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_difference(x.Doubles) })))
                .Should().Throw<NotSupportedException>()
                .WithMessage("*array_difference*refused for element type*Double*long[]*");

            FluentActions.Invoking(() => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_sum(x.Ints) })))
                .Should().NotThrow("integral sums are the declared shape");
        }
    }

    [Fact]
    public void CapturedArrays_ShouldBindAsOneParameterAndKeepNullableElementType()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bArrayEntity>();

        int[] numbers = [3, 1, 2];
        var command = Prepare(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_compact(numbers) }));

        Normalize(command.DbCommand.CommandText).Should().Contain("cast(arrayCompact(@p0) as Array(Int32)) as `V`",
            "a captured array is bound as one parameter, never expanded per element");
        command.DbCommandParams.Cast<System.Data.Common.DbParameter>().Should().ContainSingle();
    }

    [Fact]
    public void NullableElementCapturedArray_ShouldKeepTheDeclaredNullableElementType()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bArrayEntity>();

        // #335 control: the declared nullable element type is what the provider binds, so the null
        // element keeps its position instead of becoming the element default.
        int?[] nullable = [1, null, 3];
        var command = Prepare(ctx, e.Select(x => new { V = SqlFunctions.ClickHouse.array_compact(nullable) }));

        Normalize(command.DbCommand.CommandText).Should().Contain(
            "cast(arrayCompact(@p0) as Array(Nullable(Int32))) as `V`",
            "#335: the declared nullable element type drives the bound Array(Nullable(Int32))");
        command.DbCommandParams.Cast<System.Data.Common.DbParameter>().Should().ContainSingle();
    }

    [Fact]
    public void Fold_ShouldRenderTheTwoParameterLambdaAndKeepTheAccumulatorOrder()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bArrayEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            V = SqlFunctions.ClickHouse.array_fold((acc, item) => acc + item, x.Ints, 0L)
        }));

        sql.Should().Contain("arrayFold((acc, item) -> (acc + cast(item as Int64)), ints, cast(0 as Int64)) as `V`");
    }

    [Fact]
    public void FillAndSplit_ShouldRenderTheOneParameterLambdas()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bArrayEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Fill = SqlFunctions.ClickHouse.array_fill(v => v > 0, x.Ints),
            ReverseFill = SqlFunctions.ClickHouse.array_reverse_fill(v => v > 0, x.Ints),
            Split = SqlFunctions.ClickHouse.array_split(v => v > 1, x.Ints),
            ReverseSplit = SqlFunctions.ClickHouse.array_reverse_split(v => v > 1, x.Ints)
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain("cast(arrayFill(v -> (v > 0), ints) as Array(Int32)) as `Fill`");
            sql.Should().Contain("cast(arrayReverseFill(v -> (v > 0), ints) as Array(Int32)) as `ReverseFill`");
            sql.Should().Contain("cast(arraySplit(v -> (v > 1), ints) as Array(Array(Int32))) as `Split`");
            sql.Should().Contain("cast(arrayReverseSplit(v -> (v > 1), ints) as Array(Array(Int32))) as `ReverseSplit`");
        }
    }

    [Fact]
    public void NullValues_ShouldStayRealNullOperands()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bArrayEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Element = SqlFunctions.ClickHouse.array_element(x.NullableInts, x.K),
            Compact = SqlFunctions.ClickHouse.array_compact(x.NullableInts),
            Pushed = SqlFunctions.ClickHouse.array_push_front(x.NullableInts, null),
            Resized = SqlFunctions.ClickHouse.array_resize(x.NullableInts, 3, null),
            Count = SqlFunctions.ClickHouse.count_equal(x.NullableInts, null),
            Constant = SqlFunctions.ClickHouse.array_with_constant(2, (int?)null),
            Removed = SqlFunctions.ClickHouse.array_remove(x.NullableInts, null)
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain("arrayElement(nints, k) as `Element`");
            sql.Should().Contain("cast(arrayCompact(nints) as Array(Nullable(Int32))) as `Compact`");
            sql.Should().Contain("cast(arrayPushFront(nints, null) as Array(Nullable(Int32))) as `Pushed`", "a null element is a real NULL operand");
            sql.Should().Contain("cast(arrayResize(nints, 3, null) as Array(Nullable(Int32))) as `Resized`");
            sql.Should().Contain("toInt64(countEqual(nints, null)) as `Count`");
            sql.Should().Contain("cast(arrayWithConstant(2, null) as Array(Nullable(Int32))) as `Constant`");
            sql.Should().Contain("cast(arrayFilter(_x -> not coalesce(_x = null, isNull(_x) AND isNull(null)), nints) as Array(Nullable(Int32))) as `Removed`");
        }
    }

    [Fact]
    public void EmptyArrays_ShouldStayEmptyArraysNeverAnElementDefault()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bArrayEntity>();

        long[] empty = [];
        var command = Prepare(ctx, e.Select(x => new
        {
            Top = SqlFunctions.ClickHouse.array_top_k(empty, 3),
            Sample = SqlFunctions.ClickHouse.array_random_sample(empty, 3),
            Compact = SqlFunctions.ClickHouse.array_compact(empty)
        }));

        Normalize(command.DbCommand.CommandText).Should().Contain("cast(arraySlice(arrayReverseSort(arrayFilter(_x -> isNotNull(_x), @p0)), 1, 3) as Array(Int64))",
            "an empty capture keeps its declared Array(Int64) element type and is not turned into a default");
        var parameters = command.DbCommandParams.Cast<System.Data.Common.DbParameter>().ToList();
        parameters.Should().HaveCount(3, "each call binds its own captured array operand");
        parameters.Should().OnlyContain(parameter => parameter.Value is long[],
            "an empty captured array keeps its declared Array(Int64) element type instead of collapsing to Nothing");
    }

    [Fact]
    public void Projection_ShouldRenderEveryMemberOverColumns()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bArrayEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Element = SqlFunctions.ClickHouse.array_element(x.Ints, 1),
            Dense = SqlFunctions.ClickHouse.array_enumerate_dense(x.Ints),
            Zip = SqlFunctions.ClickHouse.array_zip(x.Ints, x.Strings),
            Difference = SqlFunctions.ClickHouse.array_difference(x.Ints),
            Average = SqlFunctions.ClickHouse.array_avg(x.Ints),
            Sum = SqlFunctions.ClickHouse.array_sum(x.Ints),
            Minimum = SqlFunctions.ClickHouse.array_min(x.Ints),
            Maximum = SqlFunctions.ClickHouse.array_max(x.Ints),
            Product = SqlFunctions.ClickHouse.array_product(x.Ints),
            Top = SqlFunctions.ClickHouse.array_top_k(x.Ints, 2),
            Bottom = SqlFunctions.ClickHouse.array_bottom_k(x.Ints, 2),
            Sample = SqlFunctions.ClickHouse.array_random_sample(x.Ints, 2),
            Compact = SqlFunctions.ClickHouse.array_compact(x.Ints),
            PopBack = SqlFunctions.ClickHouse.array_pop_back(x.Ints),
            PopFront = SqlFunctions.ClickHouse.array_pop_front(x.Ints),
            PushFront = SqlFunctions.ClickHouse.array_push_front(x.Ints, 5),
            Resize = SqlFunctions.ClickHouse.array_resize(x.Ints, 3),
            Remove = SqlFunctions.ClickHouse.array_remove(x.Ints, 5),
            RotateLeft = SqlFunctions.ClickHouse.array_rotate_left(x.Ints, 1),
            RotateRight = SqlFunctions.ClickHouse.array_rotate_right(x.Ints, 1),
            ShiftLeft = SqlFunctions.ClickHouse.array_shift_left(x.Ints, 1),
            ShiftRight = SqlFunctions.ClickHouse.array_shift_right(x.Ints, 1),
            Fold = SqlFunctions.ClickHouse.array_fold((acc, item) => acc + item, x.Ints, 0L),
            Fill = SqlFunctions.ClickHouse.array_fill(v => v > 0, x.Ints),
            ReverseFill = SqlFunctions.ClickHouse.array_reverse_fill(v => v > 0, x.Ints),
            Split = SqlFunctions.ClickHouse.array_split(v => v > 1, x.Ints),
            ReverseSplit = SqlFunctions.ClickHouse.array_reverse_split(v => v > 1, x.Ints),
            ReverseSort = SqlFunctions.ClickHouse.array_reverse_sort(x.Ints),
            PartialSort = SqlFunctions.ClickHouse.array_partial_sort(x.Ints, 2),
            Shingles = SqlFunctions.ClickHouse.array_shingles(x.Ints, 2),
            Transpose = SqlFunctions.ClickHouse.array_transpose(x.Nested),
            Count = SqlFunctions.ClickHouse.count_equal(x.Ints, 5),
            Constant = SqlFunctions.ClickHouse.array_with_constant(3, 5),
            Jaccard = SqlFunctions.ClickHouse.array_jaccard_index(x.Longs, x.Longs),
            Similarity = SqlFunctions.ClickHouse.array_similarity(x.Strings, x.Strings, Weights, Weights)
        }));

        foreach (var name in new[]
                 {
                     "arrayElement(ints, 1)", "cast(arrayEnumerateDense(ints) as Array(Int64))",
                     "arrayZip(ints, strs)", "cast(arrayDifference(ints) as Array(Int64))",
                     "arrayAvg(ints)", "arraySum(ints)", "arrayMin(ints)", "arrayMax(ints)",
                     "arrayProduct(ints)", "cast(arrayRandomSample(ints, 2) as Array(Int32))", "cast(arrayCompact(ints) as Array(Int32))",
                     "cast(arrayPopBack(ints) as Array(Int32))", "cast(arrayPopFront(ints) as Array(Int32))", "cast(arrayPushFront(ints, 5) as Array(Int32))",
                     "cast(arrayResize(ints, 3) as Array(Int32))", "cast(arrayRotateLeft(ints, 1) as Array(Int32))", "cast(arrayRotateRight(ints, 1) as Array(Int32))",
                     "cast(arrayShiftLeft(ints, 1) as Array(Int32))", "cast(arrayShiftRight(ints, 1) as Array(Int32))",
                     "arrayFold((acc, item) -> (acc + cast(item as Int64)), ints, cast(0 as Int64))",
                     "cast(arrayFill(v -> (v > 0), ints) as Array(Int32))", "cast(arrayReverseFill(v -> (v > 0), ints) as Array(Int32))",
                     "cast(arraySplit(v -> (v > 1), ints) as Array(Array(Int32)))", "cast(arrayReverseSplit(v -> (v > 1), ints) as Array(Array(Int32)))",
                     "cast(arrayReverseSort(ints) as Array(Int32))", "cast(arrayPartialSort(2, ints) as Array(Int32))", "cast(arrayShingles(ints, 2) as Array(Array(Int32)))",
                     "cast(arrayMap(_i -> arrayMap(_r -> _r[_i], (nested)), range(1, length((nested)[1]) + 1)) as Array(Array(Int64)))",
                     "toInt64(countEqual(ints, 5))", "cast(arrayWithConstant(3, 5) as Array(Int32))",
                     "arrayJaccardIndex(longs, longs)", "arraySimilarity(strs, strs, "
                 })
        {
            sql.Should().Contain(name, "'{0}' is part of the F05 projection", name);
        }
    }

    [Fact]
    public void Predicate_ShouldKeepTheFunctionsInTheWhereClause()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT286bArrayEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => SqlFunctions.ClickHouse.array_element(x.Ints, 1) == 7
                && SqlFunctions.ClickHouse.array_sum(x.Ints) == 6
                && SqlFunctions.ClickHouse.count_equal(x.Ints, 2) == 2)
            .Select(x => x.Id));

        using (new AssertionScope())
        {
            sql.Should().Contain("where");
            sql.Should().Contain("arrayElement(ints, 1) = 7");
            sql.Should().Contain("arraySum(ints) = 6");
            sql.Should().Contain("toInt64(countEqual(ints, 2)) = 2");
        }
    }

    [Fact]
    public void QuotedIdentifiers_ShouldPreserveTheColumnAndAlias()
    {
        using var ctx = ClickHouseTestContext.CreateQuoted();
        var e = ctx.From<IT286bArrayEntity>();

        var sql = SqlOf(ctx, e
            .Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.ClickHouse.array_compact(x.NullableInts) }));

        sql.Should().Contain("cast(arrayCompact(`nints`) as Array(Nullable(Int32))) as `V`", "the argument stays a quoted column and the alias is preserved");
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
                "array_element" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_element(x.Ints, 1) })),
                "array_enumerate_dense" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_enumerate_dense(x.Ints) })),
                "array_zip" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_zip(x.Ints, x.Strings) })),
                "array_difference" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_difference(x.Ints) })),
                "array_avg" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_avg(x.Ints) })),
                "array_sum" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_sum(x.Ints) })),
                "array_min" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_min(x.Ints) })),
                "array_max" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_max(x.Ints) })),
                "array_product" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_product(x.Ints) })),
                "array_top_k" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_top_k(x.Ints, 2) })),
                "array_bottom_k" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_bottom_k(x.Ints, 2) })),
                "array_random_sample" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_random_sample(x.Ints, 2) })),
                "array_compact" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_compact(x.Ints) })),
                "array_pop_back" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_pop_back(x.Ints) })),
                "array_pop_front" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_pop_front(x.Ints) })),
                "array_push_front" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_push_front(x.Ints, 1) })),
                "array_resize" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_resize(x.Ints, 2) })),
                "array_remove" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_remove(x.Ints, 1) })),
                "array_rotate_left" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_rotate_left(x.Ints, 1) })),
                "array_rotate_right" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_rotate_right(x.Ints, 1) })),
                "array_shift_left" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_shift_left(x.Ints, 1) })),
                "array_shift_right" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_shift_right(x.Ints, 1) })),
                "array_fold" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_fold((acc, item) => acc + item, x.Ints, 0L) })),
                "array_fill" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_fill(v => v > 0, x.Ints) })),
                "array_reverse_fill" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_reverse_fill(v => v > 0, x.Ints) })),
                "array_split" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_split(v => v > 0, x.Ints) })),
                "array_reverse_split" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_reverse_split(v => v > 0, x.Ints) })),
                "array_reverse_sort" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_reverse_sort(x.Ints) })),
                "array_partial_sort" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_partial_sort(x.Ints, 2) })),
                "array_shingles" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_shingles(x.Ints, 2) })),
                "array_transpose" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_transpose(x.Nested) })),
                "count_equal" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.count_equal(x.Ints, 1) })),
                "array_with_constant" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_with_constant(2, 1) })),
                "array_jaccard_index" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_jaccard_index(x.Ints, x.Ints) })),
                "array_similarity" => () => SqlOf(ctx, ctx.From<IT286bArrayEntity>().Select(x => new { V = SqlFunctions.ClickHouse.array_similarity(x.Strings, x.Strings, Weights, Weights) })),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, "not part of the F05 slice")
            };

            FluentActions.Invoking(query).Should().Throw<NotSupportedException>()
                .WithMessage("*require a provider*ClickHouse*");
        }
    }
}
