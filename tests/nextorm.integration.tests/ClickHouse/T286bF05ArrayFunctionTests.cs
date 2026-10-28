using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ClickHouse.Driver.ADO;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T286b D5 (F05 Array): live execution of the ClickHouse-only array members against the pinned
/// <c>25.8-alpine</c> (25.8.33.6) server. The tests pin the one-based/negative indexing rules, the
/// widened aggregate result types, the empty-array defaults, the consecutive-duplicate compaction,
/// the resize/rotate/shift vendor rules, the fold/fill/split lambda semantics, the nested
/// shingle/transpose shapes, the version-gap definitions (the native names raise
/// <c>UNKNOWN_FUNCTION</c> on 25.8) and the #335 control: a captured nullable-element array
/// <c>{1, null, 3}</c> survives end to end with no <c>0</c> substitution.
/// </summary>
[Collection("ClickHouse")]
public sealed class T286bF05ArrayFunctionTests : ProviderTestSuite
{
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

        [Column("strs")]
        string[] Strings { get; set; }

        [Column("nested")]
        long[][] Nested { get; set; }

        [Column("k")]
        long K { get; set; }

        [Column("elem")]
        int Element { get; set; }
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
                     "drop table if exists t286b_f05_entity",
                     "create table t286b_f05_entity (id Int32, ints Array(Int32), nints Array(Nullable(Int32)), "
                     + "longs Array(Int64), strs Array(String), nested Array(Array(Int64)), k Int64, elem Int32) "
                     + "engine = Memory",
                     "insert into t286b_f05_entity (id, ints, nints, longs, strs, nested, k, elem) values "
                     + "(1, [1, 2, 3, 2], [1, NULL, 3], [10, 20, 30, 40], ['a', 'b', 'c', 'd'], [[1, 2, 3], [4, 5, 6]], 2, 2), "
                     + "(2, [], [], [], [], [], 0, 0), "
                     + "(3, [4, 4, 5], [NULL, NULL], [7], ['x'], [[1], [2]], 1, 4)"
                 })
        {
            using var command = connection.CreateCommand();
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }
    }

    [Fact]
    public void ElementAndEnumerateDense_ShouldUseOneBasedIndexingAndDenseRanks()
    {
        using (new AssertionScope())
        {
            Scalar<int>("toInt32(arrayElement([10, 20, 30], 2))").Should().Be(20);
            Scalar<int>("toInt32(arrayElement([10, 20, 30], -1))").Should().Be(30, "a negative index counts from the end");
            Scalar<int>("toInt32(arrayElement([10, 20, 30], 4))").Should().Be(0, "out of bounds returns the element default");

            Scalar<string>("toString(arrayEnumerateDense([10, 20, 10, 30]))").Should().Be("[1,2,1,3]");
            Scalar<string>("toTypeName(arrayEnumerateDense([10, 20, 10, 30]))").Should().Be("Array(UInt32)");
        }

        Seed();
        using var ctx = Provider.CreateContext();
        var row = ctx.From<IT286bArrayEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Element = SqlFunctions.ClickHouse.array_element(x.Ints, 2),
                Last = SqlFunctions.ClickHouse.array_element(x.Ints, -1),
                NullableElement = SqlFunctions.ClickHouse.array_element(x.NullableInts, 2),
                Dense = SqlFunctions.ClickHouse.array_enumerate_dense(x.Ints)
            })
            .First();

        using (new AssertionScope())
        {
            row.Element.Should().Be(2);
            row.Last.Should().Be(2);
            row.NullableElement.Should().BeNull("a NULL element stays NULL, never 0");
            row.Dense.Should().Equal(1L, 2L, 3L, 2L);
            row.Dense.GetType().Should().Be(typeof(long[]), "Array(UInt32) is cast to Array(Int64)");
        }
    }

    [Fact]
    public void ZipAndDifference_ShouldPreserveTheTupleAndWidenedDifferenceShapes()
    {
        using (new AssertionScope())
        {
            Scalar<string>("toString(arrayZip(['a', 'b'], [5, 2]))").Should().Be("[('a',5),('b',2)]");
            Scalar<string>("toTypeName(arrayZip(['a', 'b'], [5, 2]))").Should().Be("Array(Tuple(String, UInt8))");
            FluentActions.Invoking(() => Scalar<string>("toString(arrayZip([1, 2, 3], ['a', 'b']))"))
                .Should().Throw<Exception>().WithMessage("*different array sizes*",
                    "the server refuses mismatched sizes instead of truncating");

            Scalar<string>("toString(arrayDifference([1, 2, 3, 4]))").Should().Be("[0,1,1,1]");
            Scalar<string>("toTypeName(arrayDifference([toInt32(1), toInt32(2)]))").Should().Be("Array(Int64)");
            Scalar<string>("toTypeName(arrayDifference([toInt8(1), toInt8(2)]))").Should().Be("Array(Int16)",
                "the vendor widens the subtraction result per element width");
        }

        Seed();
        using var ctx = Provider.CreateContext();
        var row = ctx.From<IT286bArrayEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Zip = SqlFunctions.ClickHouse.array_zip(x.Ints, x.Strings),
                Difference = SqlFunctions.ClickHouse.array_difference(x.Ints)
            })
            .First();

        using (new AssertionScope())
        {
            row.Difference.Should().Equal(0L, 1L, 1L, -1L);
            row.Difference.GetType().Should().Be(typeof(long[]));
        }
    }

    [Fact]
    public void Aggregates_ShouldReturnTheVendorShapesAndRejectNullableElementArrays()
    {
        using (new AssertionScope())
        {
            Scalar<double>("arrayAvg([1, 2, 3, 4])").Should().Be(2.5);
            Scalar<long>("arraySum([1, 2, 3, 4])").Should().Be(10L);
            Scalar<int>("toInt32(arrayMin([5, 3, 2, 7]))").Should().Be(2);
            Scalar<int>("toInt32(arrayMax([5, 3, 2, 7]))").Should().Be(7);
            Scalar<double>("arrayProduct([1, 2, 3, 4])").Should().Be(24.0);
            Scalar<string>("toTypeName(arrayAvg([1, 2, 3]))").Should().Be("Float64");
            Scalar<string>("toTypeName(arraySum([toInt32(1), toInt32(2)]))").Should().Be("Int64");
            Scalar<string>("toTypeName(arrayMin([toInt16(1)]))").Should().Be("Int16",
                "min/max keep the element type");

            Scalar<double>("arrayAvg(emptyArrayInt32())").Should().Be(0, "an empty typed array yields the vendor default");
            Scalar<long>("arraySum(emptyArrayInt32())").Should().Be(0L);
            Scalar<int>("arrayMin(emptyArrayInt32())").Should().Be(0);
            Scalar<double>("arrayProduct(emptyArrayInt32())").Should().Be(0);
            Scalar<string>("arrayMin(emptyArrayString())").Should().BeEmpty();
        }

        FluentActions.Invoking(() => Scalar<long>("arraySum(CAST([1, NULL, 3] AS Array(Nullable(Int32))))"))
            .Should().Throw<Exception>().WithMessage("*array aggregation function cannot be performed on type Nullable*",
                "no nullable-element overload exists; the server refuses instead of substituting a default");

        Seed();
        using var ctx = Provider.CreateContext();
        var row = ctx.From<IT286bArrayEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Average = SqlFunctions.ClickHouse.array_avg(x.Ints),
                Sum = SqlFunctions.ClickHouse.array_sum(x.Ints),
                Minimum = SqlFunctions.ClickHouse.array_min(x.Ints),
                Maximum = SqlFunctions.ClickHouse.array_max(x.Ints),
                Product = SqlFunctions.ClickHouse.array_product(x.Ints)
            })
            .First();

        using (new AssertionScope())
        {
            row.Average.Should().Be(2.0);
            row.Sum.Should().Be(8L);
            row.Minimum.Should().Be(1);
            row.Maximum.Should().Be(3);
            row.Product.Should().Be(12.0, "1 * 2 * 3 * 2");
        }
    }

    [Fact]
    public void TopKAndBottomK_ShouldFollowTheVendorDefinitionOnAPinnedServer()
    {
        using (new AssertionScope())
        {
            // The native names arrived in 26.6; the pinned 25.8 server reports them as unknown, which
            // is exactly why the arms render the vendor definition instead.
            FluentActions.Invoking(() => Scalar<int[]>("arrayTopK(3, [1, 5, 2, 7, 3])"))
                .Should().Throw<Exception>().WithMessage("*arrayTopK*UNKNOWN_FUNCTION*");
            FluentActions.Invoking(() => Scalar<int[]>("arrayBottomK(3, [1, 5, 2, 7, 3])"))
                .Should().Throw<Exception>().WithMessage("*arrayBottomK*UNKNOWN_FUNCTION*");

            Scalar<string>("toString(arraySlice(arrayReverseSort(arrayFilter(x -> isNotNull(x), [1, NULL, 5, 2, NULL, 7])), 1, 3))")
                .Should().Be("[7,5,2]", "the definition sorts descending and skips NULLs");
            Scalar<string>("toString(arraySlice(arraySort(arrayFilter(x -> isNotNull(x), [1, NULL, 5, 2, NULL, 7])), 1, 3))")
                .Should().Be("[1,2,5]", "the definition sorts ascending and skips NULLs");
        }

        Seed();
        using var ctx = Provider.CreateContext();
        var row = ctx.From<IT286bArrayEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Top = SqlFunctions.ClickHouse.array_top_k(x.Ints, 2),
                Bottom = SqlFunctions.ClickHouse.array_bottom_k(x.Ints, 2)
            })
            .First();

        using (new AssertionScope())
        {
            row.Top.Should().Equal(3, 2);
            row.Bottom.Should().Equal(1, 2);
        }
    }

    [Fact]
    public void RandomSample_ShouldHonourTheSampleSizeAndNeverAMissingElement()
    {
        using (new AssertionScope())
        {
            Scalar<long>("length(arrayRandomSample([1, 2, 3], 2))").Should().Be(2L);
            Scalar<long>("length(arrayRandomSample([1, 2, 3], 10))")
                .Should().Be(3L, "a larger request returns every element");
            Scalar<long>("length(arrayRandomSample([1, 2, 3], 0))").Should().Be(0L);
            Scalar<long>("arrayUniq(arrayRandomSample([1, 2, 3, 4], 3))").Should().Be(3L,
                "a sample contains distinct positions, never duplicates");
        }

        Seed();
        using var ctx = Provider.CreateContext();
        var sample = ctx.From<IT286bArrayEntity>()
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.ClickHouse.array_random_sample(x.Ints, 2))
            .First();

        using (new AssertionScope())
        {
            sample.Should().HaveCount(2);
            sample.Should().OnlyContain(value => value == 1 || value == 2 || value == 3);
        }
    }

    [Fact]
    public void CompactAndPops_ShouldPreserveOrderAndNullPositions()
    {
        using (new AssertionScope())
        {
            Scalar<string>("toString(arrayCompact([1, 1, NULL, NULL, 2, 3, 3]))")
                .Should().Be("[1,NULL,2,3]", "only consecutive duplicates are removed and NULLs are kept");
            Scalar<string>("toString(arrayPopBack([1, 2, 3]))").Should().Be("[1,2]");
            Scalar<string>("toString(arrayPopFront([1, 2, 3]))").Should().Be("[2,3]");
            Scalar<string>("toString(arrayPopBack(emptyArrayInt32()))").Should().Be("[]");
            Scalar<string>("toString(arrayPushFront(['b'], NULL))").Should().Be("[NULL,'b']",
                "a NULL element is prepended as NULL, never as an empty string");
        }

        Seed();
        using var ctx = Provider.CreateContext();
        var row = ctx.From<IT286bArrayEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Compact = SqlFunctions.ClickHouse.array_compact(x.Ints),
                NullableCompact = SqlFunctions.ClickHouse.array_compact(x.NullableInts),
                PopBack = SqlFunctions.ClickHouse.array_pop_back(x.Ints),
                PopFront = SqlFunctions.ClickHouse.array_pop_front(x.Ints),
                PushFront = SqlFunctions.ClickHouse.array_push_front(x.Ints, 9)
            })
            .First();

        using (new AssertionScope())
        {
            row.Compact.Should().Equal(1, 2, 3, 2);
            row.NullableCompact.Should().Equal(1, null, 3);
            row.PopBack.Should().Equal(1, 2, 3);
            row.PopFront.Should().Equal(2, 3, 2);
            row.PushFront.Should().Equal(9, 1, 2, 3, 2);
        }
    }

    [Fact]
    public void ResizeRotateAndShift_ShouldFollowTheVendorRules()
    {
        using (new AssertionScope())
        {
            Scalar<string>("toString(arrayResize([1], 3))").Should().Be("[1,0,0]");
            Scalar<string>("toString(arrayResize([1, 2], 3, NULL))").Should().Be("[1,2,NULL]");
            Scalar<string>("toString(arrayResize([1, 2, 3], 2))").Should().Be("[1,2]", "truncation is from the right");
            Scalar<string>("toString(arrayResize([1, 2, 3], -2))").Should().Be("[2,3]",
                "a negative size keeps the last |size| elements, the vendor rule");
            Scalar<string>("toString(arrayResize([1, 2, 3], 0))").Should().Be("[]");

            Scalar<string>("toString(arrayRotateLeft([1, 2, 3], 1))").Should().Be("[2,3,1]");
            Scalar<string>("toString(arrayRotateLeft([1, 2, 3], -1))").Should().Be("[3,1,2]");
            Scalar<string>("toString(arrayRotateLeft([1, 2, 3], 5))").Should().Be("[3,1,2]", "a rotation wraps");
            Scalar<string>("toString(arrayRotateRight([1, 2, 3], 5))").Should().Be("[2,3,1]");

            Scalar<string>("toString(arrayShiftLeft([1, 2, 3], 1))").Should().Be("[2,3,0]");
            Scalar<string>("toString(arrayShiftLeft([1, 2, 3], 1, 42))").Should().Be("[2,3,42]");
            Scalar<string>("toString(arrayShiftLeft([1, 2, 3], -1))").Should().Be("[0,1,2]");
            Scalar<string>("toString(arrayShiftRight([1, 2, 3], 1, 42))").Should().Be("[42,1,2]");
            Scalar<string>("toString(arrayShiftLeft([1, 2, 3], 5))").Should().Be("[0,0,0]",
                "a shift larger than the array empties it into the fill value");

            // The vendor quirk: a NULL fill value makes the whole result NULL (Nullable(Nothing)),
            // instead of filling with NULLs. The arm passes it through.
            Scalar<string>("toTypeName(arrayShiftLeft([1, 2, 3], 1, NULL))").Should().Be("Nullable(Nothing)");
        }

        Seed();
        using var ctx = Provider.CreateContext();
        var row = ctx.From<IT286bArrayEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Resized = SqlFunctions.ClickHouse.array_resize(x.Ints, 2),
                Extended = SqlFunctions.ClickHouse.array_resize(x.Ints, 6, 7),
                Rotated = SqlFunctions.ClickHouse.array_rotate_left(x.Ints, 1),
                Shifted = SqlFunctions.ClickHouse.array_shift_left(x.Ints, 1, 9)
            })
            .First();

        using (new AssertionScope())
        {
            row.Resized.Should().Equal(1, 2);
            row.Extended.Should().Equal(1, 2, 3, 2, 7, 7);
            row.Rotated.Should().Equal(2, 3, 2, 1);
            row.Shifted.Should().Equal(2, 3, 2, 9);
        }
    }

    [Fact]
    public void Remove_ShouldTreatNullsAsEqualOnAPinnedServer()
    {
        using (new AssertionScope())
        {
            FluentActions.Invoking(() => Scalar<int[]>("arrayRemove([1, 2, 2, 3], 2)"))
                .Should().Throw<Exception>().WithMessage("*arrayRemove*UNKNOWN_FUNCTION*",
                    "the native name arrived in 25.11; the arm renders the definition instead");

            Scalar<string>("toString(arrayFilter(x -> not coalesce(x = CAST('b' AS Nullable(String)), "
                    + "isNull(x) AND isNull(CAST('b' AS Nullable(String)))), ['a', NULL, 'b', NULL]))")
                .Should().Be("['a',NULL,NULL]", "the definition removes the matching value and keeps the NULLs");
            Scalar<string>("toString(arrayFilter(x -> not coalesce(x = CAST(NULL AS Nullable(String)), "
                    + "isNull(x) AND isNull(CAST(NULL AS Nullable(String)))), ['a', NULL, 'b', NULL]))")
                .Should().Be("['a','b']", "NULL equals NULL in the definition, unlike a plain x != element filter");
        }

        Seed();
        using var ctx = Provider.CreateContext();
        var row = ctx.From<IT286bArrayEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Removed = SqlFunctions.ClickHouse.array_remove(x.Ints, 2),
                RemovedNull = SqlFunctions.ClickHouse.array_remove(x.NullableInts, (int?)null)
            })
            .First();

        using (new AssertionScope())
        {
            row.Removed.Should().Equal(1, 3);
            row.RemovedNull.Should().Equal(1, 3);
        }
    }

    [Fact]
    public void FoldFillAndSplit_ShouldRunTheLambdasOnTheServer()
    {
        using (new AssertionScope())
        {
            Scalar<long>("arrayFold((acc, x) -> acc + x, [1, 2, 3, 4], toInt64(0))").Should().Be(10L);
            FluentActions.Invoking(() => Scalar<long>("arrayFold((acc, x) -> x, [1, 2], toInt64(0))"))
                .Should().Throw<Exception>().WithMessage("*Return type of lambda function must be the same as the accumulator type*",
                    "the typed signature prevents the mismatch the server rejects");

            Scalar<string>("toString(arrayFill(x -> not isNull(x), [1, NULL, 2, NULL]))").Should().Be("[1,1,2,2]");
            Scalar<string>("toString(arrayReverseFill(x -> not isNull(x), [1, NULL, 2, NULL]))").Should().Be("[1,2,2,NULL]");
            Scalar<string>("toString(arrayReverseFill(v -> v > 1, [1, 2, 3, 2]))")
                .Should().Be("[2,2,3,2]", "the last element is always preserved and a false predicate copies the next current value");
            Scalar<string>("toString(arrayFill(x -> x > 0, [1, -1, -1, 4]))")
                .Should().Be("[1,1,1,4]", "the first element is always preserved");

            Scalar<string>("toString(arraySplit(x -> x > 1, [1, 2, 3]))").Should().Be("[[1],[2],[3]]");
            Scalar<string>("toString(arrayReverseSplit(x -> x > 1, [1, 2, 3]))").Should().Be("[[1,2],[3]]");
        }

        Seed();
        using var ctx = Provider.CreateContext();
        var row = ctx.From<IT286bArrayEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Fold = SqlFunctions.ClickHouse.array_fold((acc, item) => acc + item, x.Ints, 0L),
                Fill = SqlFunctions.ClickHouse.array_fill(v => v > 1, x.Ints),
                ReverseFill = SqlFunctions.ClickHouse.array_reverse_fill(v => v > 1, x.Ints),
                Split = SqlFunctions.ClickHouse.array_split(v => v > 2, x.Ints),
                ReverseSplit = SqlFunctions.ClickHouse.array_reverse_split(v => v > 2, x.Ints)
            })
            .First();

        using (new AssertionScope())
        {
            row.Fold.Should().Be(8L);
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
    public void ReverseSortAndPartialSort_ShouldFollowTheVendorOrder()
    {
        using (new AssertionScope())
        {
            Scalar<string>("toString(arrayReverseSort([3, NULL, 1]))").Should().Be("[3,1,NULL]",
                "NULLs sort last in the vendor order");
            Scalar<string>("toString(arrayReverseSort([3, 1, 2]))").Should().Be("[3,2,1]");
            Scalar<string>("toString(arraySort(arrayPartialSort(2, [5, 9, 1, 3])))")
                .Should().Be("[1,3,5,9]", "the partial sort keeps every element");
            Scalar<string>("toString(arraySlice(arrayPartialSort(2, [5, 9, 1, 3]), 1, 2))")
                .Should().Be("[1,3]", "the first `limit` positions are sorted ascending");
            Scalar<string>("toString(arrayPartialSort(10, [5, 9, 1, 3]))")
                .Should().Be("[1,3,5,9]", "a limit larger than the array sorts the whole array");
        }

        Seed();
        using var ctx = Provider.CreateContext();
        var row = ctx.From<IT286bArrayEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                ReverseSort = SqlFunctions.ClickHouse.array_reverse_sort(x.Ints),
                PartialSort = SqlFunctions.ClickHouse.array_partial_sort(x.Ints, 2)
            })
            .First();

        using (new AssertionScope())
        {
            row.ReverseSort.Should().Equal(3, 2, 2, 1);
            row.PartialSort.Should().HaveCount(4);
            row.PartialSort.Take(2).Should().Equal(1, 2);
            row.PartialSort.OrderBy(value => value).Should().Equal(1, 2, 2, 3);
        }
    }

    [Fact]
    public void ShinglesAndTranspose_ShouldKeepTheNestedShape()
    {
        using (new AssertionScope())
        {
            Scalar<string>("toString(arrayShingles([1, 2, 3, 4], 3))").Should().Be("[[1,2,3],[2,3,4]]");
            FluentActions.Invoking(() => Scalar<string>("toString(arrayShingles([1, 2], 3))"))
                .Should().Throw<Exception>().WithMessage("*Shingle argument*");

            FluentActions.Invoking(() => Scalar<string>("toString(arrayTranspose([[1, 2], [3, 4]]))"))
                .Should().Throw<Exception>().WithMessage("*arrayTranspose*UNKNOWN_FUNCTION*",
                    "the native name arrived in 26.4; the arm renders the transposition itself");

            Scalar<string>("toString(arrayMap(i -> arrayMap(r -> r[i], ([[1, 2], [3, 4]])), "
                    + "range(1, length(([[1, 2], [3, 4]])[1]) + 1)))")
                .Should().Be("[[1,3],[2,4]]", "the definition transposes a square matrix");
            Scalar<string>("toString(arrayMap(i -> arrayMap(r -> r[i], ([[1, 2, 3], [4, 5, 6]])), "
                    + "range(1, length(([[1, 2, 3], [4, 5, 6]])[1]) + 1)))")
                .Should().Be("[[1,4],[2,5],[3,6]]", "the definition transposes a non-square matrix");
        }

        Seed();
        using var ctx = Provider.CreateContext();
        var row = ctx.From<IT286bArrayEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Shingles = SqlFunctions.ClickHouse.array_shingles(x.Ints, 2),
                Transposed = SqlFunctions.ClickHouse.array_transpose(x.Nested)
            })
            .First();

        using (new AssertionScope())
        {
            row.Shingles.Should().HaveCount(3);
            row.Shingles[0].Should().Equal(1, 2);
            row.Shingles[1].Should().Equal(2, 3);
            row.Shingles[2].Should().Equal(3, 2);

            row.Transposed.Should().HaveCount(3);
            row.Transposed[0].Should().Equal(1, 4);
            row.Transposed[1].Should().Equal(2, 5);
            row.Transposed[2].Should().Equal(3, 6);
        }
    }

    [Fact]
    public void CountEqualAndWithConstant_ShouldCountNullsAndFillNulls()
    {
        using (new AssertionScope())
        {
            Scalar<long>("countEqual([1, 2, 1, NULL], 1)").Should().Be(2L);
            Scalar<long>("countEqual([1, NULL, 1], NULL)").Should().Be(1L, "NULL counts as equal to NULL");
            Scalar<string>("toTypeName(countEqual([1], 1))").Should().Be("UInt64");

            Scalar<string>("toString(arrayWithConstant(3, 1))").Should().Be("[1,1,1]");
            Scalar<string>("toString(arrayWithConstant(2, CAST(NULL AS Nullable(Int32))))")
                .Should().Be("[NULL,NULL]", "a NULL value produces NULLs, never defaults");
        }

        Seed();
        using var ctx = Provider.CreateContext();
        var row = ctx.From<IT286bArrayEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Count = SqlFunctions.ClickHouse.count_equal(x.Ints, 2),
                NullCount = SqlFunctions.ClickHouse.count_equal(x.NullableInts, (int?)null),
                Constant = SqlFunctions.ClickHouse.array_with_constant(3, 7)
            })
            .First();

        using (new AssertionScope())
        {
            row.Count.Should().Be(2L);
            row.NullCount.Should().Be(1L);
            row.Constant.Should().Equal(7, 7, 7);
        }
    }

    [Fact]
    public void JaccardAndSimilarity_ShouldReturnTheDocumentedValues()
    {
        using (new AssertionScope())
        {
            Scalar<double>("arrayJaccardIndex([1, 2], [2, 3])").Should().Be(1.0 / 3.0);
            Scalar<double>("arrayJaccardIndex([1, 1, 2], [1, 2, 3])").Should().Be(0.5,
                "the vendor divides the distinct common elements by length(a)+length(b)-common");
            Scalar<double>("arrayJaccardIndex([1, 2, 3, 2], [1, 2, 3, 2])").Should().Be(0.6,
                "the raw lengths still count duplicates, so the classic set formula holds only for unique elements");
            Scalar<double>("arrayJaccardIndex([1], [])").Should().Be(0);
            FluentActions.Invoking(() => Scalar<double>("arrayJaccardIndex([], [])"))
                .Should().Throw<Exception>().WithMessage("*cannot be performed on two empty arrays*");

            Scalar<double>("arraySimilarity(['A', 'B', 'C'], ['A', 'K', 'L'], [1.0, 2, 3], [3.0, 4, 5])")
                .Should().BeApproximately(0.2222222222222222, 1e-12);
            Scalar<double>("arraySimilarity(['A', 'B'], ['A', 'B'], [1.0, 1.0], [1.0, 1.0])").Should().Be(1.0);
            FluentActions.Invoking(() => Scalar<double>("arraySimilarity(['A'], ['A'])"))
                .Should().Throw<Exception>().WithMessage("*Number of arguments for function arraySimilarity*",
                    "the four-argument form is the only one on the pinned server");
        }

        Seed();
        using var ctx = Provider.CreateContext();
        var query = ctx.From<IT286bArrayEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Jaccard = SqlFunctions.ClickHouse.array_jaccard_index(x.Ints, x.Ints),
                Similarity = SqlFunctions.ClickHouse.array_similarity(x.Strings, x.Strings, Weights4, Weights4)
            });

        SqlOf(ctx, query).Should().Contain("arrayJaccardIndex(ints, ints)");
        var row = query.First();

        using (new AssertionScope())
        {
            row.Jaccard.Should().Be(0.6, "Ints carries a duplicate, which the vendor formula penalises");
            row.Similarity.Should().Be(1.0);
        }
    }

    private static readonly double[] Weights = [1.0, 1.0];

    private static readonly double[] Weights4 = [1.0, 1.0, 1.0, 1.0];

    [Fact]
    public void NullableElementArrays_ShouldSurviveEndToEndAsTheT335Control()
    {
        // #335 control: the declared nullable element type is bound as Array(Nullable(Int32)), so the
        // null in {1, null, 3} reaches the server and comes back as a null at position 2 — no 0
        // substitution anywhere in the round trip.
        int?[] values = [1, null, 3];

        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bArrayEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Compact = SqlFunctions.ClickHouse.array_compact(values),
                Element = SqlFunctions.ClickHouse.array_element(values, 2),
                Resized = SqlFunctions.ClickHouse.array_resize(values, 3),
                ReverseSort = SqlFunctions.ClickHouse.array_reverse_sort(values),
                Pushed = SqlFunctions.ClickHouse.array_push_front(values, 9),
                Count = SqlFunctions.ClickHouse.count_equal(values, (int?)null)
            })
            .First();

        using (new AssertionScope())
        {
            // The null survives the round trip; no 0 substitution anywhere.
            row.Compact.Should().Equal(1, null, 3);
            row.Element.Should().BeNull("the second element is the real null");
            row.Resized.Should().Equal(1, null, 3);
            row.ReverseSort.Should().Equal(3, 1, null);
            row.Pushed.Should().Equal(9, 1, null, 3);
            row.Count.Should().Be(1L);
        }

        // The same values supplied as a raw native literal keep the identical shape (the control's
        // oracle), so the assertion above is about the bound parameter, not about the server.
        Scalar<string>("toString(CAST([1, NULL, 3] AS Array(Nullable(Int32))))").Should().Be("[1,NULL,3]");
        Scalar<string>("toString(arrayCompact(CAST([1, NULL, 3] AS Array(Nullable(Int32)))))").Should().Be("[1,NULL,3]");
    }

    [Fact]
    public void NullOperands_ShouldProduceNullNeverADefault()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bArrayEntity>()
            .Where(x => x.Id == 3)
            .Select(x => new
            {
                Element = SqlFunctions.ClickHouse.array_element(x.NullableInts, 1),
                Compact = SqlFunctions.ClickHouse.array_compact(x.NullableInts),
                Count = SqlFunctions.ClickHouse.count_equal(x.NullableInts, (int?)null)
            })
            .First();

        using (new AssertionScope())
        {
            row.Element.Should().BeNull();
            row.Compact.Should().Equal(new int?[] { null }, "the two consecutive NULLs compact into one");
            row.Count.Should().Be(2L);
        }
    }

    [Fact]
    public void EmptyArrays_ShouldStayEmptyAndNeverAnElementDefault()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bArrayEntity>()
            .Where(x => x.Id == 2)
            .Select(x => new
            {
                Element = SqlFunctions.ClickHouse.array_element(x.Ints, 1),
                Compact = SqlFunctions.ClickHouse.array_compact(x.Ints),
                PopBack = SqlFunctions.ClickHouse.array_pop_back(x.Ints),
                PopFront = SqlFunctions.ClickHouse.array_pop_front(x.Ints),
                ReverseSort = SqlFunctions.ClickHouse.array_reverse_sort(x.Ints),
                Top = SqlFunctions.ClickHouse.array_top_k(x.Ints, 3),
                Sum = SqlFunctions.ClickHouse.array_sum(x.Ints),
                Average = SqlFunctions.ClickHouse.array_avg(x.Ints),
                Count = SqlFunctions.ClickHouse.count_equal(x.Ints, 1),
                Transposed = SqlFunctions.ClickHouse.array_transpose(x.Nested)
            })
            .First();

        using (new AssertionScope())
        {
            row.Element.Should().Be(0, "out of bounds on an empty array is the vendor default");
            row.Compact.Should().BeEmpty();
            row.PopBack.Should().BeEmpty();
            row.PopFront.Should().BeEmpty();
            row.ReverseSort.Should().BeEmpty();
            row.Top.Should().BeEmpty();
            row.Transposed.Should().BeEmpty();
            row.Sum.Should().Be(0L);
            row.Average.Should().Be(0);
            row.Count.Should().Be(0L);
        }

        // A shingle longer than the (empty) array is rejected by the server; it never yields [].
        Seed();
        using var ctx2 = Provider.CreateContext();
        var e = ctx2.From<IT286bArrayEntity>();
        FluentActions.Invoking(() => SqlOf(ctx2, e.Where(x => x.Id == 2)
                .Select(x => SqlFunctions.ClickHouse.array_shingles(x.Ints, 2))))
            .Should().NotThrow("the SQL renders; only the server rejects the requested shingle size");

        FluentActions.Invoking(() => e.Where(x => x.Id == 2)
                .Select(x => SqlFunctions.ClickHouse.array_shingles(x.Ints, 2)).First())
            .Should().Throw<Exception>().WithMessage("*Shingle argument*");
    }

    [Fact]
    public void Projection_ShouldMaterializeTheDeclaredClrShapes()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        var row = ctx.From<IT286bArrayEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
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
                Compact = SqlFunctions.ClickHouse.array_compact(x.Ints),
                Resize = SqlFunctions.ClickHouse.array_resize(x.Ints, 2),
                Rotate = SqlFunctions.ClickHouse.array_rotate_right(x.Ints, 1),
                Shift = SqlFunctions.ClickHouse.array_shift_right(x.Ints, 1, 5),
                Fold = SqlFunctions.ClickHouse.array_fold((acc, item) => acc + item, x.Ints, 0L),
                Fill = SqlFunctions.ClickHouse.array_fill(v => v > 1, x.Ints),
                Split = SqlFunctions.ClickHouse.array_split(v => v > 2, x.Ints),
                ReverseSort = SqlFunctions.ClickHouse.array_reverse_sort(x.Ints),
                Shingles = SqlFunctions.ClickHouse.array_shingles(x.Ints, 2),
                Transpose = SqlFunctions.ClickHouse.array_transpose(x.Nested),
                Count = SqlFunctions.ClickHouse.count_equal(x.Ints, 2),
                Constant = SqlFunctions.ClickHouse.array_with_constant(2, 8),
                Jaccard = SqlFunctions.ClickHouse.array_jaccard_index(x.Longs, x.Longs),
                Similarity = SqlFunctions.ClickHouse.array_similarity(x.Strings, x.Strings, Weights4, Weights4)
            })
            .First();

        using (new AssertionScope())
        {
            row.Element.Should().Be(1);
            row.Dense.Should().Equal(1L, 2L, 3L, 2L);
            row.Difference.Should().Equal(0L, 1L, 1L, -1L);
            row.Average.Should().Be(2.0);
            row.Sum.Should().Be(8L);
            row.Minimum.Should().Be(1);
            row.Maximum.Should().Be(3);
            row.Product.Should().Be(12.0, "1 * 2 * 3 * 2");
            row.Top.Should().Equal(3, 2);
            row.Bottom.Should().Equal(1, 2);
            row.Compact.Should().Equal(1, 2, 3, 2);
            row.Resize.Should().Equal(1, 2);
            row.Rotate.Should().Equal(2, 1, 2, 3);
            row.Shift.Should().Equal(5, 1, 2, 3);
            row.Fold.Should().Be(8L);
            row.Fill.Should().Equal(1, 2, 3, 2);
            row.Split.Should().HaveCount(2);
            row.ReverseSort.Should().Equal(3, 2, 2, 1);
            row.Shingles.Should().HaveCount(3);
            row.Transpose.Should().HaveCount(3);
            row.Transpose[0].Should().Equal(1, 4);
            row.Count.Should().Be(2L);
            row.Constant.Should().Equal(8, 8);
            row.Jaccard.Should().Be(1.0, "Longs has no duplicate, so the vendor formula equals the set index");
            row.Similarity.Should().Be(1.0);

            row.Dense.GetType().Should().Be(typeof(long[]));
            row.Difference.GetType().Should().Be(typeof(long[]));
            row.Top.GetType().Should().Be(typeof(int[]));
            row.Shingles.GetType().Should().Be(typeof(int[][]));
            row.Transpose.GetType().Should().Be(typeof(long[][]));
            row.Zip.Should().HaveCount(4);
            row.Zip.GetType().Should().Be(typeof(Tuple<int, string>[]));
        }
    }

    [Fact]
    public void StringElementArrays_ShouldKeepTheStringArrayShapeAndRecordTheNullableLimit()
    {
        Seed();
        using var ctx = Provider.CreateContext();

        // A non-nullable String array result materializes as string[] (#336 tracks the nullable
        // string-element shape only; no F05 member is refused for it).
        var row = ctx.From<IT286bArrayEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Compact = SqlFunctions.ClickHouse.array_compact(x.Strings),
                Sorted = SqlFunctions.ClickHouse.array_reverse_sort(x.Strings),
                Shingles = SqlFunctions.ClickHouse.array_shingles(x.Strings, 2)
            })
            .First();

        using (new AssertionScope())
        {
            row.Compact.Should().Equal("a", "b", "c", "d");
            row.Sorted.Should().Equal("d", "c", "b", "a");
            row.Shingles.Should().HaveCount(3);
            row.Shingles[0].Should().Equal("a", "b");
            row.Compact.GetType().Should().Be(typeof(string[]));
        }
    }
}
