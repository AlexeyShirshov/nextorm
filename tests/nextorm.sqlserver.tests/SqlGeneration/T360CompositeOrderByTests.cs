using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

public class T360CompositeKey
{
    public long A { get; set; }
    public int? B { get; set; }
}

public struct T360CompositeStruct
{
    public long A { get; set; }
    public int? B { get; set; }
}

/// <summary>The composite selector families of AC-T360-03.1.</summary>
public enum T360Family
{
    Anonymous,
    ValueTupleNew,
    TupleNew,
    ValueTupleCreate,
    TupleCreate,
    MemberInitClass,
    MemberInitStruct,
}

/// <summary>The query-source shapes of AC-T360-05.2.</summary>
public enum T360OrderShape
{
    Plain,
    Joined,
    AliasChain,
    Cte,
}

/// <summary>The tuple constructor form of the nested-/<c>TRest</c> proof (the factory form is covered by the core suite).</summary>
public enum T360TupleCtor
{
    ValueTuple,
    Tuple,
}

/// <summary>One generated matrix row: the raw SQL of both calls plus the expanded keys taken from the composite builder.</summary>
internal sealed record T360MatrixResult(
    string CompositeSql,
    string ChainedSql,
    string UnsortedSql,
    OrderDirection[] Directions,
    NullsPosition[] NullsPositions);

/// <summary>
/// T360 (issue #360) SQL Server dialect proof for composite <c>ORDER BY</c> selectors. The matrix is a
/// generated cross product — (anonymous, <c>ValueTuple</c> ctor, <c>Tuple</c> ctor, <c>ValueTuple.Create</c>,
/// <c>Tuple.Create</c>, <c>MemberInit</c> class, <c>MemberInit</c> struct) × (Asc, Desc) ×
/// (<see cref="NullsPosition.Default"/>, First, Last) × (plain, joined, alias-chain, CTE) — and every row
/// asserts the raw SQL byte-identical to the equivalent chained <c>OrderBy*</c>/<c>ThenBy*</c> call with
/// every expanded key carrying the call's direction and NULL placement. No line-ending normalization and no
/// substring checks are used anywhere in these assertions.
/// </summary>
public class T360CompositeOrderByTests
{
    /// <summary>7 families × 2 directions × 3 NULL placements × 4 source shapes.</summary>
    internal const int ExpectedMatrixCardinality = 168;

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Prepare(ctx, cmd).DbCommand.CommandText;

    private static IDataContext Ctx() => SqlServerTestContext.Create();

    // ---------------------------------------------------------------- generated matrix

    /// <summary>The full generated cross product; the theory runs one case per row.</summary>
    public static IEnumerable<object[]> MatrixRows()
    {
        foreach (var family in Enum.GetValues<T360Family>())
            foreach (var direction in Enum.GetValues<OrderDirection>())
                foreach (var nulls in Enum.GetValues<NullsPosition>())
                    foreach (var shape in Enum.GetValues<T360OrderShape>())
                        yield return [family, direction, nulls, shape];
    }

    /// <summary>
    /// Completeness guard: a missing dimension value (a new family, direction, NULL placement or source
    /// shape that the matrix silently stopped covering) fails this test instead of quietly shrinking the set.
    /// </summary>
    [Fact]
    public void Matrix_Cardinality_IsExact()
    {
        Enum.GetValues<T360Family>().Should().HaveCount(7, "every composite family must stay in the matrix");
        Enum.GetValues<OrderDirection>().Should().HaveCount(2);
        Enum.GetValues<NullsPosition>().Should().HaveCount(3);
        Enum.GetValues<T360OrderShape>().Should().HaveCount(4);

        var rows = MatrixRows().ToArray();
        rows.Should().HaveCount(ExpectedMatrixCardinality);
        rows.Select(r => $"{(T360Family)r[0]}|{(OrderDirection)r[1]}|{(NullsPosition)r[2]}|{(T360OrderShape)r[3]}")
            .Distinct()
            .Should().HaveCount(ExpectedMatrixCardinality, "every cross-product combination must appear exactly once");
    }

    [Theory]
    [MemberData(nameof(MatrixRows))]
    public void CompositeOrderBy_EmitsRawSqlIdenticalToChained(
        T360Family family,
        OrderDirection direction,
        NullsPosition nulls,
        T360OrderShape shape)
    {
        using var ctx = Ctx();
        var result = BuildMatrix(ctx, family, direction, nulls, shape);

        result.Directions.Should().HaveCount(2, "a two-member composite selector expands into two ORDER BY keys");
        result.NullsPositions.Should().HaveCount(2);
        for (var i = 0; i < result.Directions.Length; i++)
        {
            result.Directions[i].Should().Be(direction, $"expanded key {i} must carry the call's direction");
            result.NullsPositions[i].Should().Be(nulls, $"expanded key {i} must carry the call's NullsPosition");
        }

        result.CompositeSql.Should().NotBe(result.UnsortedSql, "the ORDER BY clause must actually be rendered");
        result.CompositeSql.Should().Be(
            result.ChainedSql,
            "the composite call must emit the same raw SQL, byte for byte, as the chained OrderBy*/ThenBy* equivalent");
    }

    [Fact]
    public void LeftJoinSource_CompositeOrderBy_EmitsRawSqlIdenticalToChained()
    {
        using var ctx = Ctx();
        var joined = ctx.From<IComplexEntity>().LeftJoin(ctx.From<IComplexEntity>(), (a, b) => a.Id == b.Id);

        var compositeSql = SqlOf(ctx, joined
            .OrderBy(x => new { x.Item1.Id, x.Item2.Int }, OrderDirection.Desc, NullsPosition.First)
            .Select(x => new { x.Item1.Id }));
        var chainedSql = SqlOf(ctx, joined
            .OrderByDescending(x => x.Item1.Id, NullsPosition.First)
            .ThenByDescending(x => x.Item2.Int, NullsPosition.First)
            .Select(x => new { x.Item1.Id }));

        compositeSql.Should().Be(chainedSql);
    }

    // ---------------------------------------------------------------- constructor TRest (nested / 8+)

    /// <summary>4 source shapes × the two tuple constructor forms.</summary>
    public static IEnumerable<object[]> RestRows()
    {
        foreach (var shape in Enum.GetValues<T360OrderShape>())
            foreach (var ctor in Enum.GetValues<T360TupleCtor>())
                yield return [shape, ctor];
    }

    [Fact]
    public void RestRows_Cardinality_IsExact()
    {
        var rows = RestRows().ToArray();
        rows.Should().HaveCount(8);
        rows.Select(r => $"{(T360OrderShape)r[0]}|{(T360TupleCtor)r[1]}").Distinct().Should().HaveCount(8);
    }

    /// <summary>
    /// A nested 2-tuple in the 8th <c>TRest</c> slot of a <c>new ValueTuple&lt;...&gt;(...)</c> /
    /// <c>new Tuple&lt;...&gt;(...)</c> constructor (not the factory form) expands into 9 keys in declaration
    /// order on every source shape, and matches the chained 9-key equivalent at the prepared AST / plan-key level.
    /// </summary>
    [Theory]
    [MemberData(nameof(RestRows))]
    public void ConstructorTupleRest_ExpandsInDeclarationOrder_AndMatchesChained(T360OrderShape shape, T360TupleCtor ctor)
    {
        using var ctx = Ctx();
        switch (shape)
        {
            case T360OrderShape.Plain:
                AssertRestPlain(ctx, ctor);
                break;
            case T360OrderShape.Joined:
                AssertRestJoined(ctx, ctor);
                break;
            case T360OrderShape.AliasChain:
                AssertRestAliasChain(ctx, ctor);
                break;
            case T360OrderShape.Cte:
                AssertRestCte(ctx, ctor);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(shape), shape, "unhandled source shape");
        }
    }

    // ---------------------------------------------------------------- non-expandable

    [Fact]
    public void NonExpandable_ThrowsNamedMessage_AndLeavesPriorSortingUnchanged()
    {
        using var ctx = Ctx();
        var builder = ctx.From<IComplexEntity>().OrderBy(x => x.Id);
        var beforeList = builder.Sorting;
        var before = builder.Sorting!.ToArray();
        before.Should().HaveCount(1);

        FluentActions.Invoking(() => builder.OrderBy(x => new object?[] { x.Id, x.Int }))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*array composite*");

        builder.Sorting.Should().BeSameAs(beforeList);
        builder.Sorting!.Should().HaveCount(before.Length);
        for (var i = 0; i < before.Length; i++)
        {
            builder.Sorting![i].Direction.Should().Be(before[i].Direction);
            builder.Sorting![i].NullsPosition.Should().Be(before[i].NullsPosition);
            builder.Sorting![i].ColumnIndex.Should().Be(before[i].ColumnIndex);
            builder.Sorting![i].SortExpression!.ToString().Should().Be(before[i].SortExpression!.ToString());
        }
    }

    // ---------------------------------------------------------------- helpers

    private static T360MatrixResult BuildMatrix(
        IDataContext ctx,
        T360Family family,
        OrderDirection direction,
        NullsPosition nulls,
        T360OrderShape shape)
        => shape switch
        {
            T360OrderShape.Plain => Plain(ctx, family, direction, nulls),
            T360OrderShape.Joined => Joined(ctx, family, direction, nulls),
            T360OrderShape.AliasChain => AliasChain(ctx, family, direction, nulls),
            T360OrderShape.Cte => CteSource(ctx, family, direction, nulls),
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "unhandled source shape"),
        };

    private static T360MatrixResult Plain(IDataContext ctx, T360Family family, OrderDirection direction, NullsPosition nulls)
    {
        var e = ctx.From<IComplexEntity>();
        var composite = family switch
        {
            T360Family.Anonymous => e.OrderBy(x => new { x.Id, x.Int }, direction, nulls),
            T360Family.ValueTupleNew => e.OrderBy(x => new ValueTuple<long, int?>(x.Id, x.Int), direction, nulls),
            T360Family.TupleNew => e.OrderBy(x => new Tuple<long, int?>(x.Id, x.Int), direction, nulls),
            T360Family.ValueTupleCreate => e.OrderBy(x => ValueTuple.Create(x.Id, x.Int), direction, nulls),
            T360Family.TupleCreate => e.OrderBy(x => Tuple.Create(x.Id, x.Int), direction, nulls),
            T360Family.MemberInitClass => e.OrderBy(x => new T360CompositeKey { A = x.Id, B = x.Int }, direction, nulls),
            T360Family.MemberInitStruct => e.OrderBy(x => new T360CompositeStruct { A = x.Id, B = x.Int }, direction, nulls),
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "unhandled family"),
        };
        var chained = direction == OrderDirection.Asc
            ? e.OrderBy(x => x.Id, OrderDirection.Asc, nulls).ThenBy(x => x.Int, OrderDirection.Asc, nulls)
            : e.OrderByDescending(x => x.Id, nulls).ThenByDescending(x => x.Int, nulls);

        return Capture(ctx, composite, chained,
            composite.Select(x => new { x.Id }), chained.Select(x => new { x.Id }), e.Select(x => new { x.Id }));
    }

    private static T360MatrixResult Joined(IDataContext ctx, T360Family family, OrderDirection direction, NullsPosition nulls)
    {
        var joined = ctx.From<IComplexEntity>().Join<IComplexEntity>((a, b) => a.Id == b.Id);
        var composite = family switch
        {
            T360Family.Anonymous => joined.OrderBy(x => new { x.Item1.Id, x.Item2.Int }, direction, nulls),
            T360Family.ValueTupleNew => joined.OrderBy(x => new ValueTuple<long, int?>(x.Item1.Id, x.Item2.Int), direction, nulls),
            T360Family.TupleNew => joined.OrderBy(x => new Tuple<long, int?>(x.Item1.Id, x.Item2.Int), direction, nulls),
            T360Family.ValueTupleCreate => joined.OrderBy(x => ValueTuple.Create(x.Item1.Id, x.Item2.Int), direction, nulls),
            T360Family.TupleCreate => joined.OrderBy(x => Tuple.Create(x.Item1.Id, x.Item2.Int), direction, nulls),
            T360Family.MemberInitClass => joined.OrderBy(x => new T360CompositeKey { A = x.Item1.Id, B = x.Item2.Int }, direction, nulls),
            T360Family.MemberInitStruct => joined.OrderBy(x => new T360CompositeStruct { A = x.Item1.Id, B = x.Item2.Int }, direction, nulls),
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "unhandled family"),
        };
        var chained = direction == OrderDirection.Asc
            ? joined.OrderBy(x => x.Item1.Id, OrderDirection.Asc, nulls).ThenBy(x => x.Item2.Int, OrderDirection.Asc, nulls)
            : joined.OrderByDescending(x => x.Item1.Id, nulls).ThenByDescending(x => x.Item2.Int, nulls);

        return Capture(ctx, composite, chained,
            composite.Select(x => new { x.Item1.Id }), chained.Select(x => new { x.Item1.Id }),
            joined.Select(x => new { x.Item1.Id }));
    }

    private static T360MatrixResult AliasChain(IDataContext ctx, T360Family family, OrderDirection direction, NullsPosition nulls)
    {
        var src = ctx.From(ctx.From<IComplexEntity>().Select(x => new { x.Id, x.Int }));
        var composite = family switch
        {
            T360Family.Anonymous => src.OrderBy(x => new { x.Id, x.Int }, direction, nulls),
            T360Family.ValueTupleNew => src.OrderBy(x => new ValueTuple<long, int?>(x.Id, x.Int), direction, nulls),
            T360Family.TupleNew => src.OrderBy(x => new Tuple<long, int?>(x.Id, x.Int), direction, nulls),
            T360Family.ValueTupleCreate => src.OrderBy(x => ValueTuple.Create(x.Id, x.Int), direction, nulls),
            T360Family.TupleCreate => src.OrderBy(x => Tuple.Create(x.Id, x.Int), direction, nulls),
            T360Family.MemberInitClass => src.OrderBy(x => new T360CompositeKey { A = x.Id, B = x.Int }, direction, nulls),
            T360Family.MemberInitStruct => src.OrderBy(x => new T360CompositeStruct { A = x.Id, B = x.Int }, direction, nulls),
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "unhandled family"),
        };
        var chained = direction == OrderDirection.Asc
            ? src.OrderBy(x => x.Id, OrderDirection.Asc, nulls).ThenBy(x => x.Int, OrderDirection.Asc, nulls)
            : src.OrderByDescending(x => x.Id, nulls).ThenByDescending(x => x.Int, nulls);

        return Capture(ctx, composite, chained,
            composite.Select(x => new { x.Id }), chained.Select(x => new { x.Id }), src.Select(x => new { x.Id }));
    }

    private static T360MatrixResult CteSource(IDataContext ctx, T360Family family, OrderDirection direction, NullsPosition nulls)
    {
        var cte = ctx.From<IComplexEntity>().Where(x => x.Id > 1).Select(x => new { x.Id, x.Int }).AsCte("recent");
        var src = ctx.From(cte);
        var composite = family switch
        {
            T360Family.Anonymous => src.OrderBy(x => new { x.Id, x.Int }, direction, nulls),
            T360Family.ValueTupleNew => src.OrderBy(x => new ValueTuple<long, int?>(x.Id, x.Int), direction, nulls),
            T360Family.TupleNew => src.OrderBy(x => new Tuple<long, int?>(x.Id, x.Int), direction, nulls),
            T360Family.ValueTupleCreate => src.OrderBy(x => ValueTuple.Create(x.Id, x.Int), direction, nulls),
            T360Family.TupleCreate => src.OrderBy(x => Tuple.Create(x.Id, x.Int), direction, nulls),
            T360Family.MemberInitClass => src.OrderBy(x => new T360CompositeKey { A = x.Id, B = x.Int }, direction, nulls),
            T360Family.MemberInitStruct => src.OrderBy(x => new T360CompositeStruct { A = x.Id, B = x.Int }, direction, nulls),
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "unhandled family"),
        };
        var chained = direction == OrderDirection.Asc
            ? src.OrderBy(x => x.Id, OrderDirection.Asc, nulls).ThenBy(x => x.Int, OrderDirection.Asc, nulls)
            : src.OrderByDescending(x => x.Id, nulls).ThenByDescending(x => x.Int, nulls);

        return Capture(ctx, composite, chained,
            composite.Select(x => new { x.Id }), chained.Select(x => new { x.Id }), src.Select(x => new { x.Id }));
    }

    private static T360MatrixResult Capture<TEntity, TRow>(
        IDataContext ctx,
        EntityBuilder<TEntity> composite,
        EntityBuilder<TEntity> chained,
        QueryCommand<TRow> compositeCommand,
        QueryCommand<TRow> chainedCommand,
        QueryCommand<TRow> unsortedCommand)
    {
        var sorting = composite.Sorting;
        sorting.Should().NotBeNull();
        return new T360MatrixResult(
            SqlOf(ctx, compositeCommand),
            SqlOf(ctx, chainedCommand),
            SqlOf(ctx, unsortedCommand),
            [.. sorting!.Select(s => s.Direction)],
            [.. sorting.Select(s => s.NullsPosition)]);
    }

    // ---- Rest shapes -----------------------------------------------------------------------

    private static void AssertRestPlain(IDataContext ctx, T360TupleCtor ctor)
    {
        var e = ctx.From<IComplexEntity>();
        var composite = ctor == T360TupleCtor.ValueTuple
            ? e.OrderBy(x => new ValueTuple<long, int?, long, int?, long, int?, long, ValueTuple<long, int?>>(
                x.Id, x.Int, x.Id, x.Int, x.Id, x.Int, x.Id, new ValueTuple<long, int?>(x.Id, x.Int)))
            : e.OrderBy(x => new Tuple<long, int?, long, int?, long, int?, long, Tuple<long, int?>>(
                x.Id, x.Int, x.Id, x.Int, x.Id, x.Int, x.Id, new Tuple<long, int?>(x.Id, x.Int)));
        var chained = NineKeyChained(e, x => x.Id, x => x.Int);

        AssertExpandedKeys(composite.Sorting!, chained.Sorting!, IdIntOrder);
        AssertPreparedPlanKeyEqual(ctx,
            composite.Select(x => new { x.Id }), chained.Select(x => new { x.Id }), IdIntOrder.Length);
    }

    private static void AssertRestJoined(IDataContext ctx, T360TupleCtor ctor)
    {
        var joined = ctx.From<IComplexEntity>().Join<IComplexEntity>((a, b) => a.Id == b.Id);
        var composite = ctor == T360TupleCtor.ValueTuple
            ? joined.OrderBy(x => new ValueTuple<long, int?, long, int?, long, int?, long, ValueTuple<long, int?>>(
                x.Item1.Id, x.Item2.Int, x.Item1.Id, x.Item2.Int, x.Item1.Id, x.Item2.Int, x.Item1.Id,
                new ValueTuple<long, int?>(x.Item1.Id, x.Item2.Int)))
            : joined.OrderBy(x => new Tuple<long, int?, long, int?, long, int?, long, Tuple<long, int?>>(
                x.Item1.Id, x.Item2.Int, x.Item1.Id, x.Item2.Int, x.Item1.Id, x.Item2.Int, x.Item1.Id,
                new Tuple<long, int?>(x.Item1.Id, x.Item2.Int)));
        var chained = NineKeyChained(joined, x => x.Item1.Id, x => x.Item2.Int);

        AssertExpandedKeys(composite.Sorting!, chained.Sorting!, JoinedOrder);
        AssertPreparedPlanKeyEqual(ctx,
            composite.Select(x => new { x.Item1.Id }), chained.Select(x => new { x.Item1.Id }), JoinedOrder.Length);
    }

    private static void AssertRestAliasChain(IDataContext ctx, T360TupleCtor ctor)
    {
        var src = ctx.From(ctx.From<IComplexEntity>().Select(x => new { x.Id, x.Int }));
        var composite = ctor == T360TupleCtor.ValueTuple
            ? src.OrderBy(x => new ValueTuple<long, int?, long, int?, long, int?, long, ValueTuple<long, int?>>(
                x.Id, x.Int, x.Id, x.Int, x.Id, x.Int, x.Id, new ValueTuple<long, int?>(x.Id, x.Int)))
            : src.OrderBy(x => new Tuple<long, int?, long, int?, long, int?, long, Tuple<long, int?>>(
                x.Id, x.Int, x.Id, x.Int, x.Id, x.Int, x.Id, new Tuple<long, int?>(x.Id, x.Int)));
        var chained = NineKeyChained(src, x => x.Id, x => x.Int);

        AssertExpandedKeys(composite.Sorting!, chained.Sorting!, IdIntOrder);
        AssertPreparedPlanKeyEqual(ctx,
            composite.Select(x => new { x.Id }), chained.Select(x => new { x.Id }), IdIntOrder.Length);
    }

    private static void AssertRestCte(IDataContext ctx, T360TupleCtor ctor)
    {
        var cte = ctx.From<IComplexEntity>().Where(x => x.Id > 1).Select(x => new { x.Id, x.Int }).AsCte("recent");
        var composite = ctor == T360TupleCtor.ValueTuple
            ? ctx.From(cte).OrderBy(x => new ValueTuple<long, int?, long, int?, long, int?, long, ValueTuple<long, int?>>(
                x.Id, x.Int, x.Id, x.Int, x.Id, x.Int, x.Id, new ValueTuple<long, int?>(x.Id, x.Int)))
            : ctx.From(cte).OrderBy(x => new Tuple<long, int?, long, int?, long, int?, long, Tuple<long, int?>>(
                x.Id, x.Int, x.Id, x.Int, x.Id, x.Int, x.Id, new Tuple<long, int?>(x.Id, x.Int)));
        var chained = NineKeyChained(ctx.From(cte), x => x.Id, x => x.Int);

        AssertExpandedKeys(composite.Sorting!, chained.Sorting!, IdIntOrder);
        AssertPreparedPlanKeyEqual(ctx,
            composite.Select(x => new { x.Id }), chained.Select(x => new { x.Id }), IdIntOrder.Length);
    }

    // Declaration order of the 9 expanded keys: (Id, Int) × 3, then Id, then the nested Rest (Id, Int).
    private static readonly string[] IdIntOrder =
    [
        "x => Convert(x.Id, Object)",
        "x => Convert(x.Int, Object)",
        "x => Convert(x.Id, Object)",
        "x => Convert(x.Int, Object)",
        "x => Convert(x.Id, Object)",
        "x => Convert(x.Int, Object)",
        "x => Convert(x.Id, Object)",
        "x => Convert(x.Id, Object)",
        "x => Convert(x.Int, Object)",
    ];

    private static readonly string[] JoinedOrder =
    [
        "x => Convert(x.Item1.Id, Object)",
        "x => Convert(x.Item2.Int, Object)",
        "x => Convert(x.Item1.Id, Object)",
        "x => Convert(x.Item2.Int, Object)",
        "x => Convert(x.Item1.Id, Object)",
        "x => Convert(x.Item2.Int, Object)",
        "x => Convert(x.Item1.Id, Object)",
        "x => Convert(x.Item1.Id, Object)",
        "x => Convert(x.Item2.Int, Object)",
    ];

    private static EntityBuilder<TEntity> NineKeyChained<TEntity>(
        EntityBuilder<TEntity> e,
        System.Linq.Expressions.Expression<Func<TEntity, object?>> first,
        System.Linq.Expressions.Expression<Func<TEntity, object?>> second)
        => e.OrderBy(first, OrderDirection.Asc, NullsPosition.Default)
            .ThenBy(second, OrderDirection.Asc, NullsPosition.Default)
            .ThenBy(first, OrderDirection.Asc, NullsPosition.Default)
            .ThenBy(second, OrderDirection.Asc, NullsPosition.Default)
            .ThenBy(first, OrderDirection.Asc, NullsPosition.Default)
            .ThenBy(second, OrderDirection.Asc, NullsPosition.Default)
            .ThenBy(first, OrderDirection.Asc, NullsPosition.Default)
            .ThenBy(first, OrderDirection.Asc, NullsPosition.Default)
            .ThenBy(second, OrderDirection.Asc, NullsPosition.Default);

    /// <summary>Asserts the expansion count and the declaration order/direction/placement of every key.</summary>
    private static void AssertExpandedKeys(
        IReadOnlyList<Sorting> composite,
        IReadOnlyList<Sorting> chained,
        IReadOnlyList<string> expectedExpressions)
    {
        composite.Should().HaveCount(expectedExpressions.Count, "the nested TRest tuple expands to one key per element");
        chained.Should().HaveCount(expectedExpressions.Count);
        for (var i = 0; i < expectedExpressions.Count; i++)
        {
            composite[i].SortExpression!.ToString().Should().Be(expectedExpressions[i], $"composite key {i} keeps declaration order");
            composite[i].Direction.Should().Be(OrderDirection.Asc, $"composite key {i} direction");
            composite[i].NullsPosition.Should().Be(NullsPosition.Default, $"composite key {i} NULL placement");
            chained[i].SortExpression!.ToString().Should().Be(expectedExpressions[i], $"chained key {i} keeps declaration order");
        }
    }

    /// <summary>Prepares both commands and asserts per-key plan-key equality (AST comparer) of the expanded keys.</summary>
    private static void AssertPreparedPlanKeyEqual<TRow>(
        IDataContext ctx,
        QueryCommand<TRow> compositeCommand,
        QueryCommand<TRow> chainedCommand,
        int expectedKeyCount)
    {
        var compositePrepared = Prepare(ctx, compositeCommand);
        var chainedPrepared = Prepare(ctx, chainedCommand);

        compositeCommand.Sorting.Should().NotBeNull();
        chainedCommand.Sorting.Should().NotBeNull();
        compositeCommand.Sorting!.Should().HaveCount(expectedKeyCount);
        chainedCommand.Sorting!.Should().HaveCount(expectedKeyCount);

        var compositeComparer = compositeCommand.GetSortingExpressionPlanEqualityComparer();
        var chainedComparer = chainedCommand.GetSortingExpressionPlanEqualityComparer();
        for (var i = 0; i < expectedKeyCount; i++)
        {
            var compositeKey = compositeCommand.Sorting![i];
            var chainedKey = chainedCommand.Sorting![i];
            compositeKey.PreparedExpression.Should().NotBeNull($"prepared key {i}");
            compositeComparer.Equals(compositeKey, chainedKey).Should().BeTrue($"plan-key equality at key {i}");
            compositeComparer.GetHashCode(compositeKey).Should().Be(
                chainedComparer.GetHashCode(chainedKey), $"plan-key hash equality at key {i}");
        }

        compositePrepared.DbCommand.CommandText.Should().Be(chainedPrepared.DbCommand.CommandText);
    }
}
