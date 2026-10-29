using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.MySql.Tests;

namespace nextorm.mysql.tests.SqlGeneration;

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

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

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

    [Fact]
    public void LiteralNullEquality()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IT296ShapeEntity>();

        SqlOf(ctx, e.Where(x => x.Int == (int?)null).Select(x => x.Id))
            .Should().Be("select id\n from t296_shape\n where nullableint is null");

        SqlOf(ctx, e.Where(x => x.Int != (int?)null).Select(x => x.Id))
            .Should().Be("select id\n from t296_shape\n where nullableint is not null");
    }

    [Fact]
    public void LiteralNullInequality()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IT296ShapeEntity>();

        SqlOf(ctx, e.Where(x => x.Int != (int?)null).Select(x => x.Id))
            .Should().Be("select id\n from t296_shape\n where nullableint is not null");

        SqlOf(ctx, e.Where(x => x.Int == (int?)null).Select(x => x.Id))
            .Should().Be("select id\n from t296_shape\n where nullableint is null");
    }

    [Fact]
    public void BareNullRemainsNullPredicate()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IT296ShapeEntity>();

        SqlOf(ctx, e.Where(x => x.Int == null).Select(x => x.Id))
            .Should().Be("select id\n from t296_shape\n where nullableint is null");

        SqlOf(ctx, e.Where(x => x.Int != null).Select(x => x.Id))
            .Should().Be("select id\n from t296_shape\n where nullableint is not null");
    }

    /// <summary>
    /// A captured nullable value is not a syntactic literal: it stays bound as a parameter, and since
    /// design r4 it takes the approved total comparison (explicit null arms) in both operand orders.
    /// This intentionally replaces the previous <c>CapturedValuesRemainParameters</c> pin while keeping
    /// the parameterisation guarantee: one logical parameter, repeated placeholders, no inlined value.
    /// </summary>
    [Fact]
    public void CapturedValuesUseTotalComparison()
    {
        static string Collapsed(string sql)
            => string.Join(' ', Normalize(sql).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IT296ShapeEntity>();
        int? capturedNull = null;
        int? capturedValue = 4242;

        var equalNull = Collapsed(SqlOf(ctx, e.Where(x => x.Int == capturedNull).Select(x => x.Id)));
        equalNull.Should().Contain($"nullableint is null and {P}capturedNull is null");
        equalNull.Should().Contain(
            $"nullableint is not null and {P}capturedNull is not null and nullableint = {P}capturedNull");

        var notEqualNull = Collapsed(SqlOf(ctx, e.Where(x => x.Int != capturedNull).Select(x => x.Id)));
        notEqualNull.Should().Contain($"nullableint is null and {P}capturedNull is not null");
        notEqualNull.Should().Contain($"nullableint is not null and {P}capturedNull is null");
        notEqualNull.Should().Contain(
            $"nullableint is not null and {P}capturedNull is not null and nullableint != {P}capturedNull");

        var equalValue = Collapsed(SqlOf(ctx, e.Where(x => x.Int == capturedValue).Select(x => x.Id)));
        equalValue.Should().Contain($"nullableint is null and {P}capturedValue is null");
        equalValue.Should().Contain(
            $"nullableint is not null and {P}capturedValue is not null and nullableint = {P}capturedValue");

        var notEqualValue = Collapsed(SqlOf(ctx, e.Where(x => x.Int != capturedValue).Select(x => x.Id)));
        notEqualValue.Should().Contain($"nullableint is null and {P}capturedValue is not null");
        notEqualValue.Should().Contain($"nullableint is not null and {P}capturedValue is null");
        notEqualValue.Should().Contain(
            $"nullableint is not null and {P}capturedValue is not null and nullableint != {P}capturedValue");

        // Both operand orders render the same statement: the captured value never becomes the left
        // operand of the rendered comparison.
        Collapsed(SqlOf(ctx, e.Where(x => capturedNull == x.Int).Select(x => x.Id))).Should().Be(equalNull);
        Collapsed(SqlOf(ctx, e.Where(x => capturedNull != x.Int).Select(x => x.Id))).Should().Be(notEqualNull);
        Collapsed(SqlOf(ctx, e.Where(x => capturedValue == x.Int).Select(x => x.Id))).Should().Be(equalValue);
        Collapsed(SqlOf(ctx, e.Where(x => capturedValue != x.Int).Select(x => x.Id))).Should().Be(notEqualValue);

        // Still parameterised: one logical parameter, the placeholder reused for every occurrence,
        // and the captured value never inlined.
        var prepared = Prepare(ctx, e.Where(x => x.Int != capturedValue).Select(x => x.Id));
        var parameters = prepared.DbCommand.Parameters.Cast<DbParameter>().ToArray();
        parameters.Should().ContainSingle();
        parameters[0].ParameterName.Should().Be("capturedValue");
        parameters[0].Value.Should().Be(4242);

        var commandText = Normalize(prepared.DbCommand.CommandText);
        commandText.Should().NotContain("4242");
        System.Text.RegularExpressions.Regex
            .Matches(commandText, System.Text.RegularExpressions.Regex.Escape($"{P}capturedValue"))
            .Count.Should().Be(4, "the three inequality arms reuse the one logical parameter");
    }

    [Fact]
    public void HasValueTranslatesToIsNotNull()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IT296ShapeEntity>();

        SqlOf(ctx, e.Where(x => x.Int.HasValue).Select(x => x.Id))
            .Should().Contain("where nullableint is not null");
    }

    [Fact]
    public void NegatedHasValueTranslatesToIsNull()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IT296ShapeEntity>();

        SqlOf(ctx, e.Where(x => !x.Int.HasValue).Select(x => x.Id))
            .Should().Contain("where nullableint is null")
            .And.NotContain("not (nullableint is not null)");
    }

    [Fact]
    public void DerivedOperandNullComparison()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IT296ShapeEntity>();

        SqlOf(ctx, e.Where(x => (x.Int + 1) == (int?)null).Select(x => x.Id))
            .Should().Be("select id\n from t296_shape\n where (nullableint + 1) is null");

        SqlOf(ctx, e.Where(x => (x.Int + 1) != (int?)null).Select(x => x.Id))
            .Should().Be("select id\n from t296_shape\n where (nullableint + 1) is not null");
    }

    [Fact]
    public void ReferenceNullComparison()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IT296ShapeEntity>();

        SqlOf(ctx, e.Where(x => x.String == (string?)null).Select(x => x.Id))
            .Should().Be("select id\n from t296_shape\n where somestring is null");

        SqlOf(ctx, e.Where(x => x.String != (string?)null).Select(x => x.Id))
            .Should().Be("select id\n from t296_shape\n where somestring is not null");
    }

    [Fact]
    public void EnumNullComparison()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IT296ShapeEntity>();

        SqlOf(ctx, e.Where(x => x.State == (T296ShapeState?)null).Select(x => x.Id))
            .Should().Be("select id\n from t296_shape\n where state is null");

        SqlOf(ctx, e.Where(x => x.State != (T296ShapeState?)null).Select(x => x.Id))
            .Should().Be("select id\n from t296_shape\n where state is not null");
    }

    [Fact]
    public void DateTimeNullComparison()
    {
        using var ctx = MySqlTestContext.Create();
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
        using var ctx = MySqlTestContext.Create();
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
        using var ctx = MySqlTestContext.Create();
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
        using var ctx = MySqlTestContext.Create();
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
            .Should().Contain("select count(*)").And.Contain("where nullableint is null");
        SqlOf(ctx, e.Where(x => x.Int != (int?)null).Select(x => SqlFunctions.Sql.count()))
            .Should().Contain("select count(*)").And.Contain("where nullableint is not null");

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
