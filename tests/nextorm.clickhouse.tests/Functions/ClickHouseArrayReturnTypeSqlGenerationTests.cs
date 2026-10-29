using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// T276 (#276) SQL contract for the ClickHouse array-returning facades. Every targeted facade must emit
/// exactly one outer <c>cast(... as Array(...))</c> whose element type is derived from the closed declared
/// CLR array type, while the lambda operand, the non-target functions and the parameter-only pass stay
/// unchanged.
/// </summary>
public class ClickHouseArrayReturnTypeSqlGenerationTests
{
    /// <summary>An <c>int[]</c> array column (the unit entity only declares a <c>long[]</c> one).</summary>
    [SqlTable("array_entity")]
    internal interface IIntArrayEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("nums")]
        int[] Nums { get; set; }
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    [Fact]
    public void Cast_TopK()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.top_k(1L, x.Int)));

        sql.Should().Contain(
            "cast(topK(1)(nullableint) as Array(Nullable(Int32)))",
            "the topK element type follows the closed declared int?[] result");
    }

    [Fact]
    public void Cast_TopKWeighted()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.top_k_weighted(1L, x.Int, x.Id)));

        sql.Should().Contain(
            "cast(topKWeighted(1)(nullableint, id) as Array(Nullable(Int32)))",
            "the weight type never determines the result type: the declared value type does");
    }

    [Fact]
    public void Cast_GroupArray()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.group_array(x.Int)));

        sql.Should().Contain("cast(groupArray(nullableint) as Array(Nullable(Int32)))");
    }

    [Fact]
    public void Cast_GroupUniqArray()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.group_uniq_array(x.Int)));

        sql.Should().Contain("cast(groupUniqArray(nullableint) as Array(Nullable(Int32)))");
    }

    [Fact]
    public void Cast_Retention()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.retention(x.Id <= 2, x.Id <= 5)));

        sql.Should().Contain(
            "cast(retention((id <= 2), (id <= 5)) as Array(Int32))",
            "retention declares int[] while the server returns Array(UInt8)");
    }

    [Fact]
    public void Cast_ArrayMap()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IIntArrayEntity>();

        var sql = SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.array_map(v => v * 2, x.Nums)));

        sql.Should().Contain(
            "cast(arrayMap(v -> (v * 2), nums) as Array(Int32))",
            "the widened Array(Int64) result is normalised to the declared int[]");
    }

    [Fact]
    public void Cast_RangeOverloads()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            One = SqlFunctions.ClickHouse.range(3),
            Two = SqlFunctions.ClickHouse.range(1, 3),
            Three = SqlFunctions.ClickHouse.range(1, 7, 2)
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain("cast(range(3) as Array(Int64))");
            sql.Should().Contain("cast(range(1, 3) as Array(Int64))");
            sql.Should().Contain("cast(range(1, 7, 2) as Array(Int64))");
        }
    }

    [Fact]
    public void Cast_NullableAndReferenceElements()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            NullableValue = SqlFunctions.ClickHouse.group_array(x.Int),
            LongValue = SqlFunctions.ClickHouse.group_array(x.Id),
            StringValue = SqlFunctions.ClickHouse.group_array(x.String)
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain(
                "cast(groupArray(nullableint) as Array(Nullable(Int32)))",
                "a nullable value element keeps Nullable(...)");
            sql.Should().Contain(
                "cast(groupArray(id) as Array(Int64))",
                "a non-nullable long element maps to Int64, not Nullable(Int64)");
            sql.Should().Contain(
                "cast(groupArray(somestring) as Array(Nullable(String)))",
                "a reference element is declared Nullable(...) unconditionally: its CLR nullable annotation is erased at runtime, so the emitted SQL text changes by design while the data and the CLR result type do not");
        }
    }

    /// <summary>
    /// The fix may only touch the rendered result of the targeted call: the caller's lambda body is
    /// rendered verbatim, so no element-wise conversion may appear inside <c>arrayMap</c>.
    /// </summary>
    [Fact]
    public void Cast_DoesNotRewriteLambdaOperand()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IIntArrayEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            M = SqlFunctions.ClickHouse.array_map(v => v * 2, x.Nums)
        }));

        using (new AssertionScope())
        {
            sql.Should().Contain(
                "cast(arrayMap(v -> (v * 2), nums) as Array(Int32))",
                "the cast wraps the completed call; the lambda operand keeps the caller's expression");
            sql.Should().NotContain(
                "toInt32(v",
                "the element conversion is never pushed inside the lambda body");
            sql.Should().NotContain(
                "v -> toInt32",
                "the lambda parameter is never wrapped in a cast");
        }
    }

    /// <summary>
    /// The parameter-only pass (second render of a cached shape) must keep its early return: it collects
    /// parameters without emitting SQL, so the cached statement is byte-identical and carries one cast.
    /// </summary>
    [Fact]
    public void Cast_ParamModeDoesNotRender()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var k = 1L;

        QueryCommand<int?[]> Build() => e.Select(x => SqlFunctions.ClickHouse.top_k(k, x.Int));

        var first = (DbPreparedQueryCommand<int?[]>)ctx.GetPreparedQueryCommand(Build(), false, false, CancellationToken.None);
        var second = (DbPreparedQueryCommand<int?[]>)ctx.GetPreparedQueryCommand(Build(), false, true, CancellationToken.None);

        var firstSql = first.DbCommand.CommandText.Replace("\r\n", "\n");
        var secondSql = second.DbCommand.CommandText.Replace("\r\n", "\n");

        using (new AssertionScope())
        {
            firstSql.Should().Contain("cast(topK(@k)(nullableint) as Array(Nullable(Int32)))");
            secondSql.Should().Be(firstSql, "the parameter-only pass emits no SQL");
            secondSql.Split("cast(").Length.Should().Be(2, "exactly one cast survives the cached render");
        }
    }

    /// <summary>
    /// The normalisation is scoped to the targeted facades: the remaining array, higher-order and
    /// aggregate functions keep their current SQL, including the scalar-returning <c>length</c> wrapper
    /// and the sequence aggregate's <c>toInt32</c>.
    /// </summary>
    [Fact]
    public void Cast_NontargetFunctionsRemainUnchanged()
    {
        using var ctx = ClickHouseTestContext.Create();
        var arrays = ctx.From<IArrayEntity>();
        var complex = ctx.From<IComplexEntity>();

        var arraySql = SqlOf(ctx, arrays.Select(x => new
        {
            Sorted = SqlFunctions.ClickHouse.array_sort(x.Nums),
            Reversed = SqlFunctions.ClickHouse.array_reverse(x.Nums),
            Distinct = SqlFunctions.ClickHouse.array_distinct(x.Nums),
            Filtered = SqlFunctions.ClickHouse.array_filter(v => v > 1, x.Nums),
            Enumerated = SqlFunctions.ClickHouse.array_enumerate(x.Nums),
            Has = SqlFunctions.ClickHouse.has(x.Nums, 2),
            Length = SqlFunctions.ClickHouse.length(x.Nums)
        }));

        var aggregateSql = SqlOf(ctx, complex.Select(x => new
        {
            Any = SqlFunctions.ClickHouse.any_agg(x.String),
            Funnel = SqlFunctions.ClickHouse.window_funnel(3600, x.Datetime, x.Id <= 2, x.Id <= 5)
        }));

        using (new AssertionScope())
        {
            arraySql.Should().Contain("arraySort(nums)");
            arraySql.Should().Contain("arrayReverse(nums)");
            arraySql.Should().Contain("arrayDistinct(nums)");
            arraySql.Should().Contain("arrayFilter(v -> (v > 1), nums)");
            arraySql.Should().Contain("arrayEnumerate(nums)");
            arraySql.Should().Contain("has(nums, 2)");
            arraySql.Should().Contain(
                "toInt64(length(nums))",
                "the pre-existing scalar normalisation for length stays the only wrapper on it");
            arraySql.Should().NotContain("cast(");

            aggregateSql.Should().Contain("any(somestring)");
            aggregateSql.Should().Contain(
                "toInt32(windowFunnel(3600)(dt, (id <= 2), (id <= 5)))",
                "the sequence aggregate keeps its existing scalar normalisation");
            aggregateSql.Should().NotContain("cast(");
        }
    }

    /// <summary>
    /// Each targeted call is normalised exactly once: a targeted call nested inside another targeted call
    /// owns its own single cast and does not get a second one from the outer call.
    /// </summary>
    [Fact]
    public void Cast_DoesNotDuplicateCast()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IArrayEntity>();

        var sql = SqlOf(ctx, e.Select(x => SqlFunctions.ClickHouse.array_map(v => v * 2, SqlFunctions.ClickHouse.range(3))));

        using (new AssertionScope())
        {
            sql.Should().Contain(
                "cast(arrayMap(v -> (v * 2), cast(range(3) as Array(Int64))) as Array(Int64))",
                "the nested range owns one cast and the outer arrayMap owns one cast");
            sql.Split("cast(").Length.Should().Be(3, "two targeted calls, two casts, no duplicate");
        }
    }
}
