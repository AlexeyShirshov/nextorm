using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.ClickHouse.Tests;

namespace nextorm.clickhouse.tests.SqlGeneration;

/// <summary>
/// #296: a syntactic nullable null literal (<c>(int?)null</c>) is lowered by C# to
/// <c>Convert(Constant(null, object), int?)</c>, so the consumers' bare
/// <c>ConstantExpression</c> probes missed it and rendered <c>= null</c>/<c>!= null</c> instead of
/// the null-aware <c>IS [NOT] NULL</c> predicate. These tests pin the corrected SQL for every
/// nullable shape and every translation position on this dialect. No database is opened.
/// </summary>
public class T296NullableNullSqlGenerationTests
{
    /// <summary>The dialect placeholder prefix used by this provider (SQLite uses <c>$</c>).</summary>
    private const string P = "@";

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText);

    /// <summary>
    /// C# folds a nested/checked conversion of a null literal away, so the shape is built with the
    /// expression tree API while the left operand stays row-dependent. Every <c>Convert</c> node is
    /// method-free and targets a nullable or reference type, i.e. it preserves null.
    /// </summary>
    private static Expression<Func<IT296ShapeEntity, bool>> NestedChecked(bool negated)
    {
        var parameter = Expression.Parameter(typeof(IT296ShapeEntity), "x");
        var member = Expression.Property(parameter, typeof(IT296ShapeEntity), nameof(IT296ShapeEntity.Int));
        var nullObject = Expression.Constant(null, typeof(object));
        var checkedToNullable = Expression.ConvertChecked(nullObject, typeof(int?));
        var boxed = Expression.Convert(checkedToNullable, typeof(object));
        var nested = Expression.Convert(boxed, typeof(int?));
        var body = negated ? Expression.NotEqual(member, nested) : Expression.Equal(member, nested);
        return Expression.Lambda<Func<IT296ShapeEntity, bool>>(body, parameter);
    }

    private static void ShouldRejectHasValue(Expression<Func<IT296ShapeEntity, bool>> predicate)
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT296ShapeEntity>();

        var act = () => SqlOf(ctx, e.Where(predicate).Select(x => x.Id));

        act.Should().ThrowExactly<BuildSqlCommandException>()
            .WithMessage("Cannot resolve column for member HasValue");
    }

    [Fact]
    public void LiteralNullEquality()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT296ShapeEntity>();

        SqlOf(ctx, e.Where(x => x.Int == (int?)null).Select(x => x.Id))
            .Should().Be("select id\n from t296_shape\n where nullableint is null");

        SqlOf(ctx, e.Where(x => x.Int != (int?)null).Select(x => x.Id))
            .Should().Be("select id\n from t296_shape\n where nullableint is not null");
    }

    [Fact]
    public void LiteralNullInequality()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT296ShapeEntity>();

        SqlOf(ctx, e.Where(x => x.Int != (int?)null).Select(x => x.Id))
            .Should().Be("select id\n from t296_shape\n where nullableint is not null");

        SqlOf(ctx, e.Where(x => x.Int == (int?)null).Select(x => x.Id))
            .Should().Be("select id\n from t296_shape\n where nullableint is null");
    }

    [Fact]
    public void BareNullRemainsNullPredicate()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT296ShapeEntity>();

        SqlOf(ctx, e.Where(x => x.Int == null).Select(x => x.Id))
            .Should().Be("select id\n from t296_shape\n where nullableint is null");

        SqlOf(ctx, e.Where(x => x.Int != null).Select(x => x.Id))
            .Should().Be("select id\n from t296_shape\n where nullableint is not null");
    }

    /// <summary>
    /// A captured null and a captured non-null value are not syntactic literals: they stay bound as
    /// parameters and must not be rewritten to <c>IS [NOT] NULL</c> (semantic expansion is deferred).
    /// </summary>
    [Fact]
    public void CapturedValuesRemainParameters()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT296ShapeEntity>();
        int? capturedNull = null;
        int? capturedValue = 1;

        SqlOf(ctx, e.Where(x => x.Int == capturedNull).Select(x => x.Id))
            .Should().Contain($"nullableint = {P}capturedNull").And.NotContain("is null");

        SqlOf(ctx, e.Where(x => x.Int != capturedNull).Select(x => x.Id))
            .Should().Contain($"nullableint != {P}capturedNull").And.NotContain("is not null");

        SqlOf(ctx, e.Where(x => x.Int == capturedValue).Select(x => x.Id))
            .Should().Contain($"nullableint = {P}capturedValue").And.NotContain("is null");

        SqlOf(ctx, e.Where(x => x.Int != capturedValue).Select(x => x.Id))
            .Should().Contain($"nullableint != {P}capturedValue").And.NotContain("is not null");
    }

    [Fact]
    public void HasValueRemainsTypedUnsupported()
        => ShouldRejectHasValue(x => x.Int.HasValue);

    [Fact]
    public void NegatedHasValueRemainsTypedUnsupported()
        => ShouldRejectHasValue(x => !x.Int.HasValue);

    [Fact]
    public void DerivedOperandNullComparison()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT296ShapeEntity>();

        SqlOf(ctx, e.Where(x => (x.Int + 1) == (int?)null).Select(x => x.Id))
            .Should().Be("select id\n from t296_shape\n where (nullableint + 1) is null");

        SqlOf(ctx, e.Where(x => (x.Int + 1) != (int?)null).Select(x => x.Id))
            .Should().Be("select id\n from t296_shape\n where (nullableint + 1) is not null");
    }

    [Fact]
    public void ReferenceNullComparison()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT296ShapeEntity>();

        SqlOf(ctx, e.Where(x => x.String == (string?)null).Select(x => x.Id))
            .Should().Be("select id\n from t296_shape\n where somestring is null");

        SqlOf(ctx, e.Where(x => x.String != (string?)null).Select(x => x.Id))
            .Should().Be("select id\n from t296_shape\n where somestring is not null");
    }

    [Fact]
    public void EnumNullComparison()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT296ShapeEntity>();

        SqlOf(ctx, e.Where(x => x.State == (T296ShapeState?)null).Select(x => x.Id))
            .Should().Be("select id\n from t296_shape\n where state is null");

        SqlOf(ctx, e.Where(x => x.State != (T296ShapeState?)null).Select(x => x.Id))
            .Should().Be("select id\n from t296_shape\n where state is not null");
    }

    [Fact]
    public void DateTimeNullComparison()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT296ShapeEntity>();

        SqlOf(ctx, e.Where(x => x.Datetime == (DateTime?)null).Select(x => x.Id))
            .Should().Be("select id\n from t296_shape\n where dt is null");

        SqlOf(ctx, e.Where(x => x.Datetime != (DateTime?)null).Select(x => x.Id))
            .Should().Be("select id\n from t296_shape\n where dt is not null");
    }

    /// <summary>
    /// The compiler lowers <c>(int?)null</c> to <c>Convert(Constant(null, object), int?)</c> but
    /// <c>default(int?)</c> to a typed nullable null constant; reversed operands and nested/checked
    /// wrappers must classify the same way.
    /// </summary>
    [Theory]
    [InlineData("reversed", false)]
    [InlineData("reversed", true)]
    [InlineData("nested-checked", false)]
    [InlineData("nested-checked", true)]
    [InlineData("compiler-default", false)]
    [InlineData("compiler-default", true)]
    public void NullLiteralShapes(string shape, bool negated)
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT296ShapeEntity>();

        Expression<Func<IT296ShapeEntity, bool>> predicate;
        if (shape == "reversed")
            predicate = negated ? x => (int?)null != x.Int : x => (int?)null == x.Int;
        else if (shape == "compiler-default")
            predicate = negated ? x => x.Int != default(int?) : x => x.Int == default(int?);
        else
            predicate = NestedChecked(negated);

        var sql = SqlOf(ctx, e.Where(predicate).Select(x => x.Id));

        // Reversed operands must still render the presence predicate operand-first:
        // `null is nullableint` is not valid SQL in any supported dialect.
        if (shape == "reversed")
            sql.Should()
                .Contain(negated ? "nullableint is not null" : "nullableint is null")
                .And.NotContain("null is ");
        else
            sql.Should().Contain(negated ? "nullableint is not null" : "nullableint is null");

        sql.Should().NotContain("= null").And.NotContain("!= null");
    }

    /// <summary>
    /// Non-null constants and conversions that are not null-preserving must not take the null-aware
    /// path: the comparison keeps its ordinary operator. A user-defined conversion is never
    /// unwrapped (asserted on the classifier directly, because its translation is out of scope).
    /// </summary>
    [Fact]
    public void NonLiteralConversionsAreNotNullLiterals()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT296ShapeEntity>();

        SqlOf(ctx, e.Where(x => x.Int == (int?)1).Select(x => x.Id))
            .Should().Contain("nullableint = 1").And.NotContain("is null");

        SqlOf(ctx, e.Where(x => x.Int != (int?)1).Select(x => x.Id))
            .Should().Contain("nullableint != 1").And.NotContain("is not null");

        var parameter = Expression.Parameter(typeof(IT296ShapeEntity), "x");
        var member = Expression.Property(parameter, typeof(IT296ShapeEntity), nameof(IT296ShapeEntity.Int));
        // object -> int is a non-nullable value-type conversion, so this chain is not null-preserving.
        var notPreserving = Expression.Convert(
            Expression.Convert(Expression.Constant(null, typeof(object)), typeof(int)), typeof(int?));

        SqlOf(ctx, e.Where(Expression.Lambda<Func<IT296ShapeEntity, bool>>(
                Expression.Equal(member, notPreserving), parameter)).Select(x => x.Id))
            .Should().Contain("nullableint = null").And.NotContain("nullableint is null");

        SqlOf(ctx, e.Where(Expression.Lambda<Func<IT296ShapeEntity, bool>>(
                Expression.NotEqual(member, notPreserving), parameter)).Select(x => x.Id))
            .Should().Contain("nullableint != null").And.NotContain("nullableint is not null");

        var userDefined = Expression.Convert(
            Expression.Constant(new T296NullWrapper(null)), typeof(int?));
        userDefined.Method.Should().NotBeNull("the shape has to be a user-defined conversion");
        TypeFacts.IsNullLiteral(userDefined).Should().BeFalse("a user-defined conversion is never unwrapped");
    }

    [Fact]
    public void TranslationPositions()
    {
        using var ctx = ClickHouseTestContext.Create();
        var e = ctx.From<IT296ShapeEntity>();

        // WHERE.
        SqlOf(ctx, e.Where(x => x.Int == (int?)null).Select(x => x.Id))
            .Should().Contain("where nullableint is null");
        SqlOf(ctx, e.Where(x => x.Int != (int?)null).Select(x => x.Id))
            .Should().Contain("where nullableint is not null");

        // Reversed operands (null literal on the left) must keep the operand-first presence form.
        SqlOf(ctx, e.Where(x => (int?)null == x.Int).Select(x => x.Id))
            .Should().Contain("where nullableint is null").And.NotContain("null is ");
        SqlOf(ctx, e.Where(x => (int?)null != x.Int).Select(x => x.Id))
            .Should().Contain("where nullableint is not null").And.NotContain("null is ");

        // COUNT (aggregate projection; no database is touched).
        SqlOf(ctx, e.Where(x => x.Int == (int?)null).Select(x => SqlFunctions.Sql.count()))
            .Should().Contain("select toInt32(count(*))").And.Contain("where nullableint is null");
        SqlOf(ctx, e.Where(x => x.Int != (int?)null).Select(x => SqlFunctions.Sql.count()))
            .Should().Contain("select toInt32(count(*))").And.Contain("where nullableint is not null");

        // Reversed operands in the COUNT projection.
        SqlOf(ctx, e.Where(x => (int?)null == x.Int).Select(x => SqlFunctions.Sql.count()))
            .Should().Contain("where nullableint is null").And.NotContain("null is ");
        SqlOf(ctx, e.Where(x => (int?)null != x.Int).Select(x => SqlFunctions.Sql.count()))
            .Should().Contain("where nullableint is not null").And.NotContain("null is ");

        // Boolean projection.
        SqlOf(ctx, e.Select(x => new { x.Id, Eq = x.Int == (int?)null }))
            .Should().Contain("nullableint is null as `Eq`");
        SqlOf(ctx, e.Select(x => new { x.Id, Ne = x.Int != (int?)null }))
            .Should().Contain("nullableint is not null as `Ne`");

        // The same operand-first order holds for a reversed null literal in a boolean projection.
        SqlOf(ctx, e.Select(x => new { x.Id, Eq = (int?)null == x.Int }))
            .Should().Contain("nullableint is null").And.NotContain("null is ");
        SqlOf(ctx, e.Select(x => new { x.Id, Ne = (int?)null != x.Int }))
            .Should().Contain("nullableint is not null").And.NotContain("null is ");
    }

    private readonly struct T296NullWrapper
    {
        private readonly int? _value;

        public T296NullWrapper(int? value) => _value = value;

        public static implicit operator int?(T296NullWrapper wrapper) => wrapper._value;
    }
}

public enum T296ShapeState
{
    Unknown,
    Active,
    Closed,
}

[SqlTable("t296_shape")]
public interface IT296ShapeEntity
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("nullableint")]
    int? Int { get; set; }

    [Column("somestring")]
    string? String { get; set; }

    [Column("state")]
    T296ShapeState? State { get; set; }

    [Column("dt")]
    DateTime? Datetime { get; set; }
}
