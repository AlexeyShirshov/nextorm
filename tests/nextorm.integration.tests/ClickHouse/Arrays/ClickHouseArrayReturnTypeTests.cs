using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ClickHouse.Driver;
using ClickHouse.Driver.ADO;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T276 (#276): the ClickHouse array-returning facades declare closed CLR array types
/// (<c>int?[]</c>, <c>int[]</c>, <c>long[]</c>, <c>string[]</c>, <c>byte[]</c>) while the server returns
/// its own element type (<c>Array(Int32)</c>, <c>Array(UInt8)</c>, <c>Array(Int64)</c>,
/// <c>Array(Nullable(Int32))</c>). The six <c>DeclaredArray_*</c> tests are the original materialisation
/// regressions; the remaining tests pin the controls, the declared-type variants, the nullable driver arm
/// and the unchanged higher-order functions.
/// </summary>
[Collection("ClickHouse")]
public sealed class ClickHouseArrayReturnTypeTests : ProviderTestSuite
{
    [SqlTable("t276_array_return_entity")]
    internal interface IT276ArrayReturnEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("nint")]
        int? Int { get; set; }

        [Column("nums")]
        int[] Nums { get; set; }

        [Column("lid")]
        long Lid { get; set; }

        [Column("nlid")]
        long? NLid { get; set; }

        [Column("s")]
        string S { get; set; }

        [Column("nints")]
        int?[] NInts { get; set; }
    }

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    /// <summary>Id 1..5, nullable Int = 3,1,null,3,2, Nums = [3,1,2], NInts = [1,null,3].</summary>
    private static void Seed()
    {
        using var connection = new ClickHouseConnection(ClickHouseContainer.ConnectionString);
        connection.Open();
        foreach (var statement in new[]
                 {
                     "drop table if exists t276_array_return_entity",
                     "create table t276_array_return_entity (id Int32, nint Nullable(Int32), nums Array(Int32), " +
                     "lid Int64, nlid Nullable(Int64), s String, nints Array(Nullable(Int32))) engine = Memory",
                     "insert into t276_array_return_entity (id, nint, nums, lid, nlid, s, nints) values " +
                     "(1, 3, [3, 1, 2], 10, 100, 'a', [1, null, 3]), " +
                     "(2, 1, [3, 1, 2], 20, null, 'b', [1, null, 3]), " +
                     "(3, null, [3, 1, 2], 30, 300, 'c', [1, null, 3]), " +
                     "(4, 3, [3, 1, 2], 40, null, 'd', [1, null, 3]), " +
                     "(5, 2, [3, 1, 2], 50, 500, 'e', [1, null, 3])"
                 })
        {
            using var command = connection.CreateCommand();
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }
    }

    [Fact]
    public void DeclaredArray_TopK()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var value = ctx.From<IT276ArrayReturnEntity>()
            .Select(x => SqlFunctions.ClickHouse.top_k(1L, x.Int))
            .First();

        using (new AssertionScope())
        {
            value.Should().BeOfType<int?[]>(
                "top_k over a nullable value declares int?[] and the result is normalised to it");
            value.Should().Equal(new int?[] { 3 }, "3 is the most frequent non-null value");
        }
    }

    [Fact]
    public void DeclaredArray_GroupArray()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var value = ctx.From<IT276ArrayReturnEntity>()
            .Select(x => SqlFunctions.ClickHouse.group_array(x.Int))
            .First();

        using (new AssertionScope())
        {
            value.Should().BeOfType<int?[]>("group_array declares int?[] for a nullable value");
            value.Should().HaveCount(4, "ClickHouse elides NULL aggregate inputs, keeping [3,1,3,2]");
            value.OrderBy(v => v).Should().Equal(
                new int?[] { 1, 2, 3, 3 },
                "the aggregate order is unspecified, the multiplicity is not");
        }
    }

    [Fact]
    public void DeclaredArray_GroupUniqArray()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var value = ctx.From<IT276ArrayReturnEntity>()
            .Select(x => SqlFunctions.ClickHouse.group_uniq_array(x.Int))
            .First();

        using (new AssertionScope())
        {
            value.Should().BeOfType<int?[]>("group_uniq_array declares int?[] for a nullable value");
            value.OrderBy(v => v).Should().Equal(
                new int?[] { 1, 2, 3 },
                "the distinct non-null values are a set, so no order applies");
        }
    }

    [Fact]
    public void DeclaredArray_Retention()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var value = ctx.From<IT276ArrayReturnEntity>()
            .Select(x => SqlFunctions.ClickHouse.retention(x.Id > 0, x.Id > 1, x.Id > 2))
            .First();

        using (new AssertionScope())
        {
            value.Should().BeOfType<int[]>(
                "retention declares int[] while the server returns Array(UInt8)");
            value.Should().Equal(new[] { 1, 1, 1 }, "each cumulative condition is satisfied by some row");
        }
    }

    [Fact]
    public void DeclaredArray_ArrayMap()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var value = ctx.From<IT276ArrayReturnEntity>()
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.array_map(v => v * 2, x.Nums))
            .First();

        using (new AssertionScope())
        {
            value.Should().BeOfType<int[]>(
                "array_map declares TOut[] as int[] and the widened Array(Int64) result is normalised");
            value.Should().Equal(new[] { 6, 2, 4 }, "arrayMap preserves the input order");
        }
    }

    [Fact]
    public void DeclaredArray_Range()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var value = ctx.From<IT276ArrayReturnEntity>()
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.range(3))
            .First();

        using (new AssertionScope())
        {
            value.Should().BeOfType<long[]>(
                "range declares long[] while the inlined literal yields Array(UInt8)");
            value.Should().Equal(new[] { 0L, 1L, 2L });
        }
    }

    [Fact]
    public void WeightedAggregate_DeclaredArray()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var value = ctx.From<IT276ArrayReturnEntity>()
            .Select(x => SqlFunctions.ClickHouse.top_k_weighted(1L, x.Int, x.Id))
            .First();

        using (new AssertionScope())
        {
            value.Should().BeOfType<int?[]>(
                "the weight type must not change the result type: the declared value type does");
            value.Should().HaveCount(1);
            value.Should().OnlyContain(v => v == 2 || v == 3,
                "value 2 and value 3 tie on the accumulated weight, so no tie order is assumed");
        }
    }

    [Fact]
    public void Controls_StayGreen()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var e = ctx.From<IT276ArrayReturnEntity>();

        var topK = e.Select(x => SqlFunctions.ClickHouse.top_k(1L, x.Id)).First();
        var grouped = e.Select(x => SqlFunctions.ClickHouse.group_array(x.Id)).First();
        var sorted = e.Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.array_sort(x.Nums))
            .First();

        using (new AssertionScope())
        {
            topK.Should().BeOfType<int[]>("a non-nullable value keeps a non-nullable element type");
            topK.Should().HaveCount(1).And.OnlyContain(id => id >= 1 && id <= 5);

            grouped.Should().BeOfType<int[]>("#276 must not make non-nullable Id results nullable");
            grouped.OrderBy(v => v).Should().Equal(new[] { 1, 2, 3, 4, 5 });

            sorted.Should().BeOfType<int[]>();
            sorted.Should().Equal(new[] { 1, 2, 3 });
        }
    }

    [Fact]
    public void Range_OverloadsAndEdges()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var e = ctx.From<IT276ArrayReturnEntity>().Where(x => x.Id == 1);

        var one = e.Select(x => SqlFunctions.ClickHouse.range(3)).First();
        var two = e.Select(x => SqlFunctions.ClickHouse.range(1L, 3L)).First();
        var three = e.Select(x => SqlFunctions.ClickHouse.range(1L, 7L, 2L)).First();
        var zero = e.Select(x => SqlFunctions.ClickHouse.range(0)).First();
        var empty = e.Select(x => SqlFunctions.ClickHouse.range(2L, 2L)).First();

        using (new AssertionScope())
        {
            one.Should().BeOfType<long[]>();
            one.Should().Equal(new[] { 0L, 1L, 2L });
            two.Should().BeOfType<long[]>();
            two.Should().Equal(new[] { 1L, 2L });
            three.Should().BeOfType<long[]>();
            three.Should().Equal(new[] { 1L, 3L, 5L });
            zero.Should().BeOfType<long[]>();
            zero.Should().BeEmpty("range(0) is an empty range");
            empty.Should().BeOfType<long[]>();
            empty.Should().BeEmpty("equal bounds are an empty range");
        }

        // The invalid zero step keeps the server's own rejection: no client-side guard is introduced.
        var zeroStep = FluentActions.Invoking(() => e
                .Select(x => SqlFunctions.ClickHouse.range(1L, 5L, 0L))
                .First())
            .Should().Throw<ClickHouseServerException>("the server rejects a zero range step");

        zeroStep.Which.Message.Should().ContainEquivalentOf("step");
    }

    [Fact]
    public void MappedElementKinds_MatchDeclaredType()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var e = ctx.From<IT276ArrayReturnEntity>();

        var longs = e.Select(x => SqlFunctions.ClickHouse.group_array(x.Lid)).First();
        var nullableLongs = e.Select(x => SqlFunctions.ClickHouse.group_array(x.NLid)).First();
        var strings = e.Select(x => SqlFunctions.ClickHouse.group_array(x.S)).First();
        var bytes = e.Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.array_map(v => v, new byte[] { 1, 2, 3 }))
            .First();
        // The bound input deliberately carries non-null values: a bound nullable-array *parameter*
        // containing null loses the null element before the server evaluates it (pre-existing
        // input-binding defect, tracked as issue #335). Actual null elements are covered by the
        // column and native CAST scenarios in NullableArrayColumn_PreservesNulls.
        var nullableElements = e.Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.array_map(v => v, new int?[] { 1, 2, 3 }))
            .First();

        using (new AssertionScope())
        {
            longs.Should().BeOfType<long[]>();
            longs.OrderBy(v => v).Should().Equal(new[] { 10L, 20L, 30L, 40L, 50L });

            nullableLongs.Should().BeOfType<long?[]>("a nullable value element maps to a nullable CLR element");
            nullableLongs.OrderBy(v => v).Should().Equal(new long?[] { 100, 300, 500 },
                "ClickHouse elides NULL aggregate inputs");

            strings.Should().BeOfType<string[]>();
            strings.OrderBy(v => v).Should().Equal("a", "b", "c", "d", "e");

            bytes.Should().BeOfType<byte[]>("a byte element maps to Array(UInt8)");
            bytes.Should().Equal(new byte[] { 1, 2, 3 });

            nullableElements.Should().BeOfType<int?[]>();
            nullableElements.Should().Equal(new int?[] { 1, 2, 3 },
                "nullable declared element types remain nullable; preservation of actual null elements " +
                "is verified through the column and native CAST scenarios");
        }
    }

    [Fact]
    public void NullableArrayColumn_PreservesNulls()
    {
        Seed();

        // The previously unmeasured arm, measured at the driver level first on the same live connection.
        using (var connection = new ClickHouseConnection(ClickHouseContainer.ConnectionString))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "select CAST([1, NULL, 3] AS Array(Nullable(Int32)))";
            var raw = command.ExecuteScalar();

            raw.Should().BeOfType<int?[]>("the pinned driver returns a Nullable(Int32) array as int?[]");
            ((int?[])raw!).Should().Equal(new int?[] { 1, null, 3 });
        }

        // A genuine Array(Nullable(Int32)) column through the translated query.
        using var ctx = Provider.CreateContext();
        var value = ctx.From<IT276ArrayReturnEntity>()
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.array_map(v => v, x.NInts))
            .First();

        using (new AssertionScope())
        {
            value.Should().BeOfType<int?[]>(
                "the declared closed type keeps the nullable element type");
            value.Should().Equal(new int?[] { 1, null, 3 });
        }
    }

    [Fact]
    public void T277_FiledColumnLambda_StaysGreen()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var value = ctx.From<IT276ArrayReturnEntity>()
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.array_filter(v => v > 1, x.Nums))
            .First();

        using (new AssertionScope())
        {
            value.Should().BeOfType<int[]>("array_filter is not normalised: its native result already matches");
            value.Should().Equal(new[] { 3, 2 }, "the filed T277 query keeps working unchanged");
        }
    }

    [Fact]
    public void ColumnArrayMap_MaterializesDeclaredIntArray()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var value = ctx.From<IT276ArrayReturnEntity>()
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.array_map(v => -v, x.Nums))
            .First();

        using (new AssertionScope())
        {
            value.Should().BeOfType<int[]>("the bare column map is normalised to the declared int[]");
            value.Should().Equal(new[] { -3, -1, -2 });
        }
    }

    [Fact]
    public void OtherHigherOrderFunctions_Unchanged()
    {
        Seed();
        using var ctx = Provider.CreateContext();
        var e = ctx.From<IT276ArrayReturnEntity>().Where(x => x.Id == 1);

        var exists = e.Select(x => SqlFunctions.ClickHouse.array_exists(v => v > 2, x.Nums)).First();
        var all = e.Select(x => SqlFunctions.ClickHouse.array_all(v => v > 0, x.Nums)).First();
        var count = e.Select(x => SqlFunctions.ClickHouse.array_count(v => v > 1, x.Nums)).First();
        var filtered = e.Select(x => SqlFunctions.ClickHouse.array_filter(v => v > 1, x.Nums)).First();

        using (new AssertionScope())
        {
            exists.Should().BeTrue();
            all.Should().BeTrue();
            count.Should().Be(2);
            filtered.Should().Equal(new[] { 3, 2 });
        }
    }
}
