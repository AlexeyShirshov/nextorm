using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// A LINQ type test (<c>x is T</c>, a <see cref="TypeBinaryExpression"/> with <c>TypeIs</c> or
/// <c>TypeEqual</c>) has no representable SQL form: the engine has no discriminator and no runtime type
/// switch, so rendering the bare operand as the condition silently returned wrong rows (#295). Every
/// reachable entity-dependent shape must be rejected at query preparation with a contextual
/// <see cref="NotSupportedException"/>, while supported predicates keep preparing.
/// </summary>
/// <remarks>
/// C# folds or warns away an <c>is</c> test whose static operand type already is (or derives from) the
/// tested type, and it has no <c>TypeEqual</c> syntax at all. Those rows are built with the expression
/// tree API so the node still reaches the translator over a row-dependent entity operand. Where C#
/// keeps the test intact (<c>x.Int is int</c>, <c>x.String is string</c>, <c>(object)x.Id is long</c>)
/// the test is written in plain C#.
/// </remarks>
public class TypeBinaryRejectionSqlGenerationTests
{
    /// <summary>The frozen rejection message, asserted verbatim by <see cref="Reject_Where_TypeIs_ExactFrozenMessage"/>.</summary>
    private const string FrozenMessage =
        "SQL translation of type-test expressions (TypeIs/TypeEqual) is not supported. Use explicit mapped-column predicates instead.";

    private const string FrozenSubstring = "*type-test expressions (TypeIs/TypeEqual)*";

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    /// <summary>Builds an entity-dependent predicate (<see cref="IComplexEntity"/> operand) from an explicit body.</summary>
    private static Expression<Func<IComplexEntity, bool>> Predicate(Func<ParameterExpression, Expression> build)
    {
        var parameter = Expression.Parameter(typeof(IComplexEntity), "x");
        return Expression.Lambda<Func<IComplexEntity, bool>>(build(parameter), parameter);
    }

    /// <summary>Resolves a mapped member of <see cref="IComplexEntity"/> against a built parameter.</summary>
    private static Expression Member(ParameterExpression parameter, string name)
        => Expression.Property(parameter, typeof(IComplexEntity), name);

    /// <summary>Wraps a built predicate body into an ORDER BY key (bool/boxed key bound to <c>object?</c>).</summary>
    private static Expression<Func<IComplexEntity, object?>> SortKey(Func<ParameterExpression, Expression> build)
    {
        var parameter = Expression.Parameter(typeof(IComplexEntity), "x");
        return Expression.Lambda<Func<IComplexEntity, object?>>(Expression.Convert(build(parameter), typeof(object)), parameter);
    }

    private static void ShouldReject(Action act)
        => act.Should().Throw<NotSupportedException>().WithMessage(FrozenSubstring);

    // ---------------------------------------------------------------- F1: bare Where

    /// <summary>O1 — a non-nullable value-type operand (<c>x.Id is long</c> is folded by C#).</summary>
    [Fact]
    public void Reject_Where_TypeIs_NonNullableValueType()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var predicate = Predicate(x => Expression.TypeIs(Member(x, nameof(IComplexEntity.Id)), typeof(long)));

        ShouldReject(() => SqlOf(ctx, e.Where(predicate).Select(x => new { x.Id })));
    }

    /// <summary>O2 — a nullable value-type operand.</summary>
    [Fact]
    public void Reject_Where_TypeIs_NullableValueType()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        ShouldReject(() => SqlOf(ctx, e.Where(x => x.Int is int).Select(x => new { x.Id })));
    }

    /// <summary>O3 — a reference string operand.</summary>
    [Fact]
    public void Reject_Where_TypeIs_ReferenceString()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        ShouldReject(() => SqlOf(ctx, e.Where(x => x.String is string).Select(x => new { x.Id })));
    }

    /// <summary>O4 — a derived-entity operand tested against its base type (folded by C#).</summary>
    [Fact]
    public void Reject_Where_TypeIs_BaseTypeOfDerivedEntity()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<DerivedEntitySqlGenerationTests.TphExpressOrder>();
        var parameter = Expression.Parameter(typeof(DerivedEntitySqlGenerationTests.TphExpressOrder), "x");
        var predicate = Expression.Lambda<Func<DerivedEntitySqlGenerationTests.TphExpressOrder, bool>>(
            Expression.TypeIs(parameter, typeof(DerivedEntitySqlGenerationTests.TphOrderBase)), parameter);

        ShouldReject(() => SqlOf(ctx, e.Where(predicate).Select(x => new { x.Id })));
    }

    /// <summary>O5 — a boxed row-dependent value-type operand (<c>(object)x.Id is long</c>).</summary>
    [Fact]
    public void Reject_Where_TypeIs_BoxedMember()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        ShouldReject(() => SqlOf(ctx, e.Where(x => (object)x.Id is long).Select(x => new { x.Id })));
    }

    /// <summary>O2 — <c>TypeEqual</c> in a predicate (no C# syntax exists for it).</summary>
    [Fact]
    public void Reject_Where_TypeEqual_NullableValueType()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var predicate = Predicate(x => Expression.TypeEqual(Member(x, nameof(IComplexEntity.Int)), typeof(int)));

        ShouldReject(() => SqlOf(ctx, e.Where(predicate).Select(x => new { x.Id })));
    }

    /// <summary>The exact frozen message, not just a stable substring.</summary>
    [Fact]
    public void Reject_Where_TypeIs_ExactFrozenMessage()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Where(x => (object)x.String! is string).Select(x => new { x.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage(FrozenMessage);
    }

    // ---------------------------------------------------------------- F2: &&

    /// <summary>O1 in a conjunction with a supported mapped-column comparison.</summary>
    [Fact]
    public void Reject_Conjunction_TypeIs_NonNullableValueType()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var predicate = Predicate(x => Expression.AndAlso(
            Expression.GreaterThan(Member(x, nameof(IComplexEntity.Id)), Expression.Constant(0L)),
            Expression.TypeIs(Member(x, nameof(IComplexEntity.Id)), typeof(long))));

        ShouldReject(() => SqlOf(ctx, e.Where(predicate).Select(x => new { x.Id })));
    }

    /// <summary>O3 — <c>TypeEqual</c> in a conjunction.</summary>
    [Fact]
    public void Reject_Conjunction_TypeEqual_ReferenceString()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var predicate = Predicate(x => Expression.AndAlso(
            Expression.GreaterThan(Member(x, nameof(IComplexEntity.Id)), Expression.Constant(0L)),
            Expression.TypeEqual(Member(x, nameof(IComplexEntity.String)), typeof(string))));

        ShouldReject(() => SqlOf(ctx, e.Where(predicate).Select(x => new { x.Id })));
    }

    // ---------------------------------------------------------------- F3: ||

    /// <summary>O3 in a disjunction.</summary>
    [Fact]
    public void Reject_Disjunction_TypeIs_ReferenceString()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var predicate = Predicate(x => Expression.OrElse(
            Expression.TypeIs(Member(x, nameof(IComplexEntity.String)), typeof(string)),
            Expression.GreaterThan(Member(x, nameof(IComplexEntity.Id)), Expression.Constant(0L))));

        ShouldReject(() => SqlOf(ctx, e.Where(predicate).Select(x => new { x.Id })));
    }

    /// <summary>O5 — <c>TypeEqual</c> over a boxed operand in a disjunction.</summary>
    [Fact]
    public void Reject_Disjunction_TypeEqual_BoxedMember()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var predicate = Predicate(x => Expression.OrElse(
            Expression.TypeEqual(
                Expression.Convert(Member(x, nameof(IComplexEntity.Id)), typeof(object)),
                typeof(long)),
            Expression.GreaterThan(Member(x, nameof(IComplexEntity.Id)), Expression.Constant(0L))));

        ShouldReject(() => SqlOf(ctx, e.Where(predicate).Select(x => new { x.Id })));
    }

    // ---------------------------------------------------------------- F4: !

    /// <summary>O2 — a negated type test.</summary>
    [Fact]
    public void Reject_Negation_TypeIs_NullableValueType()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        ShouldReject(() => SqlOf(ctx, e.Where(x => !(x.Int is int)).Select(x => new { x.Id })));
    }

    /// <summary>O4 — a negated <c>TypeEqual</c> over a derived-entity operand.</summary>
    [Fact]
    public void Reject_Negation_TypeEqual_DerivedEntity()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<DerivedEntitySqlGenerationTests.TphExpressOrder>();
        var parameter = Expression.Parameter(typeof(DerivedEntitySqlGenerationTests.TphExpressOrder), "x");
        var predicate = Expression.Lambda<Func<DerivedEntitySqlGenerationTests.TphExpressOrder, bool>>(
            Expression.Not(Expression.TypeEqual(parameter, typeof(DerivedEntitySqlGenerationTests.TphExpressOrder))),
            parameter);

        ShouldReject(() => SqlOf(ctx, e.Where(predicate).Select(x => new { x.Id })));
    }

    // ---------------------------------------------------------------- F5: Select(bool) projection

    /// <summary>O3 — a type test projected as a boolean column.</summary>
    [Fact]
    public void Reject_Projection_TypeIs_ReferenceString()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        ShouldReject(() => SqlOf(ctx, e.Select(x => new { Flag = (x.String is string) })));
    }

    /// <summary>O2 — <c>TypeEqual</c> projected as a boolean column (built as a member-init projection).</summary>
    [Fact]
    public void Reject_Projection_TypeEqual_NullableValueType()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var parameter = Expression.Parameter(typeof(IComplexEntity), "x");
        var rowType = typeof(TypeTestFlagRow);
        var projection = Expression.Lambda<Func<IComplexEntity, TypeTestFlagRow>>(
            Expression.MemberInit(
                Expression.New(rowType),
                Expression.Bind(
                    rowType.GetProperty(nameof(TypeTestFlagRow.Flag))!,
                    Expression.TypeEqual(Member(parameter, nameof(IComplexEntity.Int)), typeof(int)))),
            parameter);

        ShouldReject(() => SqlOf(ctx, e.Select(projection)));
    }

    // ---------------------------------------------------------------- F6: ORDER BY

    /// <summary>O1 — a type test used as an ORDER BY key.</summary>
    [Fact]
    public void Reject_OrderBy_TypeIs_NonNullableValueType()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var key = SortKey(x => Expression.TypeIs(Member(x, nameof(IComplexEntity.Id)), typeof(long)));

        ShouldReject(() => SqlOf(ctx, e.OrderBy(key).Select(x => new { x.Id })));
    }

    /// <summary>O3 — <c>TypeEqual</c> used as an ORDER BY key.</summary>
    [Fact]
    public void Reject_OrderBy_TypeEqual_ReferenceString()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var key = SortKey(x => Expression.TypeEqual(Member(x, nameof(IComplexEntity.String)), typeof(string)));

        ShouldReject(() => SqlOf(ctx, e.OrderBy(key).Select(x => new { x.Id })));
    }

    // ---------------------------------------------------------------- F7: HAVING in a grouped query

    /// <summary>O2 — a type test in HAVING over the grouped key.</summary>
    [Fact]
    public void Reject_Having_TypeIs_NullableValueType()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        ShouldReject(() => SqlOf(ctx, e
            .GroupBy(x => x.Int)
            .Having(x => x.Int is int)
            .Select(x => new { x.Int })));
    }

    /// <summary>O1 — <c>TypeEqual</c> in HAVING.</summary>
    [Fact]
    public void Reject_Having_TypeEqual_NonNullableValueType()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var having = Predicate(x => Expression.TypeEqual(Member(x, nameof(IComplexEntity.Id)), typeof(long)));

        ShouldReject(() => SqlOf(ctx, e
            .GroupBy(x => x.Int)
            .Having(having)
            .Select(x => new { x.Int })));
    }

    // ---------------------------------------------------------------- F8: subquery / Any predicate

    /// <summary>O3 — a type test inside an existential subquery predicate.</summary>
    [Fact]
    public void Reject_SubqueryPredicate_TypeIs_ReferenceString()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var inner = ctx.From<IComplexEntity>();

        ShouldReject(() => SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            Has = inner.Where(s => s.String is string).Any()
        })));
    }

    /// <summary>O5 — <c>TypeEqual</c> over a boxed operand inside an existential subquery predicate.</summary>
    [Fact]
    public void Reject_SubqueryPredicate_TypeEqual_BoxedMember()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var inner = ctx.From<IComplexEntity>();
        var innerPredicate = Predicate(s => Expression.TypeEqual(
            Expression.Convert(Member(s, nameof(IComplexEntity.Id)), typeof(object)),
            typeof(long)));

        ShouldReject(() => SqlOf(ctx, e.Select(x => new
        {
            x.Id,
            Has = inner.Where(innerPredicate).Any()
        })));
    }

    // ---------------------------------------------------------------- controls must keep preparing

    /// <summary>Control: an ordinary mapped-column comparison still prepares.</summary>
    [Fact]
    public void Control_MappedColumnComparison_ShouldPrepare()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id > 0).Select(x => new { x.Id }));

        sql.Should().Contain("where");
        sql.Should().Contain("id");
    }

    /// <summary>Control: a null check on a nullable column still prepares.</summary>
    [Fact]
    public void Control_NullCheck_ShouldPrepare()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Int != null).Select(x => new { x.Id }));

        sql.Should().Contain("where");
        sql.Should().Contain("nullableint");
    }

    /// <summary>Control: an ordinary projection still prepares.</summary>
    [Fact]
    public void Control_Projection_ShouldPrepare()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new { x.Id, x.String }));

        sql.Should().Contain("select");
        sql.Should().Contain("somestring");
    }

    /// <summary>Control: an ordinary ORDER BY still prepares.</summary>
    [Fact]
    public void Control_OrderBy_ShouldPrepare()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.OrderBy(x => x.Int).Select(x => new { x.Id }));

        sql.Should().Contain("order by");
        sql.Should().Contain("nullableint");
    }

    /// <summary>Carrier for a projected type test; C# has no <c>TypeEqual</c> syntax to project.</summary>
    private sealed class TypeTestFlagRow
    {
        public bool Flag { get; set; }
    }
}
