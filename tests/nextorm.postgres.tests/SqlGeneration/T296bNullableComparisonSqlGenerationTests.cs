using System.Data.Common;
using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres.Tests;

namespace nextorm.postgres.tests.SqlGeneration;

/// <summary>
/// T296b (#296): the approved total-boolean contract for *captured* nullable operands and mapped
/// <c>HasValue</c> in the approved positions — WHERE, boolean SELECT, JOIN ON and a correlated
/// <c>Exists</c> body — plus the guards for the positions and operators that must not change.
/// <see cref="T296NullableNullSqlGenerationTests"/> keeps the literal-form pins; its three previous
/// captured/presence pins were intentionally revised in D3 (design r4).
/// No database is opened.
/// </summary>
public class T296bNullableComparisonSqlGenerationTests
{
    /// <summary>The dialect placeholder prefix used by this provider.</summary>
    private const string P = "@";

    /// <summary>The projection alias delimiters of this dialect.</summary>
    private const string EqAlias = "\"Eq\"";
    private const string NeAlias = "\"Ne\"";
    private const string StrEqAlias = "\"StrEq\"";
    private const string StrNeAlias = "\"StrNe\"";
    private const string HasAlias = "\"Has\"";
    private const string AbsentAlias = "\"Absent\"";
    private const string ComposedAlias = "\"Composed\"";
    private const string LiteralAlias = "\"Literal\"";

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string Collapse(string sql)
        => string.Join(' ', Normalize(sql).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlCollapsed<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Collapse(Prepare(ctx, cmd).DbCommand.CommandText);

    /// <summary>The legal boolean-scalar shape this dialect must produce at a SELECT boundary.</summary>
    private static string Scalar(string predicate, string alias) => $"{predicate} as {alias}";

    /// <summary>
    /// This dialect's <c>MakeBooleanPredicate</c> is the identity, so a boolean projection must never
    /// gain a CASE/CAST wrapper. The parameter names the number of boolean projections expected on a
    /// dialect that does legalise (SQL Server asserts the count there instead).
    /// </summary>
    private static void AssertScalarLegalisation(string sql, int projectionCount)
        => sql.Should().NotContain("case when",
            "this dialect legalises no predicate (expected {0} boolean projection(s))", projectionCount);

    /// <summary>
    /// Captured nullable <c>int?</c>: <c>==</c> and <c>!=</c> become total predicates — the null arms
    /// make the NULL result of a plain comparison impossible — while every captured value stays a
    /// parameter. Both operand orders must translate identically, and the shape must not depend on
    /// the captured value.
    /// </summary>
    [Fact]
    public void CapturedNullableIntComparison_ShouldRenderTotalBooleanPredicate()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IT296ShapeEntity>();
        int? capturedNull = null;
        int? capturedValue = 42;

        var equalNull = SqlCollapsed(ctx, e.Where(x => x.Int == capturedNull).Select(x => x.Id));
        equalNull.Should().Contain($"nullableint is null and {P}capturedNull is null");
        equalNull.Should().Contain(
            $"nullableint is not null and {P}capturedNull is not null and nullableint = {P}capturedNull");

        var notEqualNull = SqlCollapsed(ctx, e.Where(x => x.Int != capturedNull).Select(x => x.Id));
        notEqualNull.Should().Contain($"nullableint is null and {P}capturedNull is not null");
        notEqualNull.Should().Contain($"nullableint is not null and {P}capturedNull is null");
        notEqualNull.Should().Contain(
            $"nullableint is not null and {P}capturedNull is not null and nullableint != {P}capturedNull");

        // Same expression, non-null captured value: the predicate keeps the null arms (a runtime
        // value never selects a different SQL shape).
        var equalValue = SqlCollapsed(ctx, e.Where(x => x.Int == capturedValue).Select(x => x.Id));
        equalValue.Should().Contain($"nullableint is null and {P}capturedValue is null");
        equalValue.Should().Contain(
            $"nullableint is not null and {P}capturedValue is not null and nullableint = {P}capturedValue");

        var notEqualValue = SqlCollapsed(ctx, e.Where(x => x.Int != capturedValue).Select(x => x.Id));
        notEqualValue.Should().Contain($"nullableint is null and {P}capturedValue is not null");
        notEqualValue.Should().Contain($"nullableint is not null and {P}capturedValue is null");
        notEqualValue.Should().Contain(
            $"nullableint is not null and {P}capturedValue is not null and nullableint != {P}capturedValue");

        // Reversing the operands must not change the translation.
        SqlCollapsed(ctx, e.Where(x => capturedNull == x.Int).Select(x => x.Id)).Should().Be(equalNull);
        SqlCollapsed(ctx, e.Where(x => capturedNull != x.Int).Select(x => x.Id)).Should().Be(notEqualNull);
        SqlCollapsed(ctx, e.Where(x => capturedValue == x.Int).Select(x => x.Id)).Should().Be(equalValue);
        SqlCollapsed(ctx, e.Where(x => capturedValue != x.Int).Select(x => x.Id)).Should().Be(notEqualValue);

        // Still parameterised: one logical parameter, repeated placeholders, no inlined value.
        var prepared = Prepare(ctx, e.Where(x => x.Int != capturedValue).Select(x => x.Id));
        var parameters = prepared.DbCommand.Parameters.Cast<DbParameter>().ToArray();
        parameters.Should().ContainSingle();
        parameters[0].ParameterName.Should().Be("capturedValue");
        parameters[0].Value.Should().Be(42);
        var commandText = Normalize(prepared.DbCommand.CommandText);
        commandText.Should().NotContain("42");
        System.Text.RegularExpressions.Regex
            .Matches(commandText, System.Text.RegularExpressions.Regex.Escape($"{P}capturedValue"))
            .Count.Should().Be(4, "the three inequality arms reuse the one logical parameter");
    }

    /// <summary>
    /// Captured nullable <c>string?</c>: the same total shape, provider-ordinary comparison for the
    /// non-null arm, no CLR collation substitution and no inlining.
    /// </summary>
    [Fact]
    public void CapturedNullableStringComparison_ShouldRenderTotalBooleanPredicate()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IT296ShapeEntity>();
        string? capturedNull = null;
        string? capturedValue = "ttwo";

        var equalNull = SqlCollapsed(ctx, e.Where(x => x.String == capturedNull).Select(x => x.Id));
        equalNull.Should().Contain($"somestring is null and {P}capturedNull is null");
        equalNull.Should().Contain(
            $"somestring is not null and {P}capturedNull is not null and somestring = {P}capturedNull");

        var notEqualNull = SqlCollapsed(ctx, e.Where(x => x.String != capturedNull).Select(x => x.Id));
        notEqualNull.Should().Contain($"somestring is null and {P}capturedNull is not null");
        notEqualNull.Should().Contain($"somestring is not null and {P}capturedNull is null");
        notEqualNull.Should().Contain(
            $"somestring is not null and {P}capturedNull is not null and somestring != {P}capturedNull");

        var equalValue = SqlCollapsed(ctx, e.Where(x => x.String == capturedValue).Select(x => x.Id));
        equalValue.Should().Contain($"somestring is null and {P}capturedValue is null");
        equalValue.Should().Contain(
            $"somestring is not null and {P}capturedValue is not null and somestring = {P}capturedValue");

        var notEqualValue = SqlCollapsed(ctx, e.Where(x => x.String != capturedValue).Select(x => x.Id));
        notEqualValue.Should().Contain($"somestring is null and {P}capturedValue is not null");
        notEqualValue.Should().Contain($"somestring is not null and {P}capturedValue is null");
        notEqualValue.Should().Contain(
            $"somestring is not null and {P}capturedValue is not null and somestring != {P}capturedValue");

        SqlCollapsed(ctx, e.Where(x => capturedNull == x.String).Select(x => x.Id)).Should().Be(equalNull);
        SqlCollapsed(ctx, e.Where(x => capturedNull != x.String).Select(x => x.Id)).Should().Be(notEqualNull);
        SqlCollapsed(ctx, e.Where(x => capturedValue == x.String).Select(x => x.Id)).Should().Be(equalValue);
        SqlCollapsed(ctx, e.Where(x => capturedValue != x.String).Select(x => x.Id)).Should().Be(notEqualValue);

        var prepared = Prepare(ctx, e.Where(x => x.String == capturedValue).Select(x => x.Id));
        var parameters = prepared.DbCommand.Parameters.Cast<DbParameter>().ToArray();
        parameters.Should().ContainSingle();
        parameters[0].ParameterName.Should().Be("capturedValue");
        parameters[0].Value.Should().Be("ttwo");
        Normalize(prepared.DbCommand.CommandText).Should().NotContain("ttwo");
    }

    /// <summary>
    /// Mapped <c>Nullable&lt;T&gt;.HasValue</c> on a mapped column receiver: <c>IS NOT NULL</c>, and
    /// its negation <c>IS NULL</c>, standalone and composed with <c>&amp;&amp;</c>/<c>||</c>.
    /// </summary>
    [Fact]
    public void MappedHasValueAndNegation_ShouldRenderPresencePredicatesAndCompose()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IT296ShapeEntity>();

        SqlCollapsed(ctx, e.Where(x => x.Int.HasValue).Select(x => x.Id))
            .Should().Contain("where nullableint is not null");
        SqlCollapsed(ctx, e.Where(x => !x.Int.HasValue).Select(x => x.Id))
            .Should().Contain("where nullableint is null");

        var andSql = SqlCollapsed(ctx, e.Where(x => x.Int.HasValue && x.Id > 0).Select(x => x.Id));
        andSql.Should().Contain("nullableint is not null").And.Contain(" and ").And.Contain("id > 0");

        var orSql = SqlCollapsed(ctx, e.Where(x => x.Int.HasValue || x.Id > 0).Select(x => x.Id));
        orSql.Should().Contain("nullableint is not null").And.Contain(" or ").And.Contain("id > 0");

        var negatedAndSql = SqlCollapsed(ctx, e.Where(x => !x.Int.HasValue && x.Id > 0).Select(x => x.Id));
        negatedAndSql.Should().Contain("nullableint is null").And.Contain(" and ").And.Contain("id > 0");

        var negatedOrSql = SqlCollapsed(ctx, e.Where(x => !x.Int.HasValue || x.Id > 0).Select(x => x.Id));
        negatedOrSql.Should().Contain("nullableint is null").And.Contain(" or ").And.Contain("id > 0");
    }

    /// <summary>
    /// The same forms in a boolean SELECT projection must reach the legal scalar shape exactly once:
    /// the total predicate is assembled in predicate context and wrapped by the dialect scalar
    /// boundary (a no-op on this dialect; SQL Server wraps the whole predicate in one CASE/CAST).
    /// </summary>
    [Fact]
    public void BooleanSelectProjection_ShouldRenderLegalBooleanScalar()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IT296ShapeEntity>();
        int? capturedValue = 42;
        string? capturedText = "ttwo";

        var equal = SqlCollapsed(ctx, e.Select(x => new { x.Id, Eq = x.Int == capturedValue }));
        equal.Should().Contain($"nullableint is null and {P}capturedValue is null");
        equal.Should().Contain(
            $"nullableint is not null and {P}capturedValue is not null and nullableint = {P}capturedValue");
        equal.Should().Contain($" as {EqAlias}");
        AssertScalarLegalisation(equal, 1);

        var notEqual = SqlCollapsed(ctx, e.Select(x => new { x.Id, Ne = x.Int != capturedValue }));
        notEqual.Should().Contain($"nullableint is null and {P}capturedValue is not null");
        notEqual.Should().Contain($"nullableint is not null and {P}capturedValue is null");
        notEqual.Should().Contain(
            $"nullableint is not null and {P}capturedValue is not null and nullableint != {P}capturedValue");
        notEqual.Should().Contain($" as {NeAlias}");
        AssertScalarLegalisation(notEqual, 1);

        // Reversed operand order renders the same projection.
        SqlCollapsed(ctx, e.Select(x => new { x.Id, Eq = capturedValue == x.Int })).Should().Be(equal);
        SqlCollapsed(ctx, e.Select(x => new { x.Id, Ne = capturedValue != x.Int })).Should().Be(notEqual);

        // Captured strings in a projection.
        var stringEqual = SqlCollapsed(ctx, e.Select(x => new { x.Id, StrEq = x.String == capturedText }));
        stringEqual.Should().Contain($"somestring is null and {P}capturedText is null");
        stringEqual.Should().Contain($" as {StrEqAlias}");
        AssertScalarLegalisation(stringEqual, 1);

        var stringNotEqual = SqlCollapsed(ctx, e.Select(x => new { x.Id, StrNe = x.String != capturedText }));
        stringNotEqual.Should().Contain($"somestring is null and {P}capturedText is not null");
        stringNotEqual.Should().Contain($" as {StrNeAlias}");
        AssertScalarLegalisation(stringNotEqual, 1);

        // Presence forms, standalone and composed.
        var has = SqlCollapsed(ctx, e.Select(x => new { x.Id, Has = x.Int.HasValue }));
        has.Should().Contain(Scalar("nullableint is not null", HasAlias));
        AssertScalarLegalisation(has, 1);

        var absent = SqlCollapsed(ctx, e.Select(x => new { x.Id, Absent = !x.Int.HasValue }));
        absent.Should().Contain(Scalar("nullableint is null", AbsentAlias));
        AssertScalarLegalisation(absent, 1);

        var composed = SqlCollapsed(ctx, e.Select(x => new { x.Id, Composed = x.Int.HasValue && x.Id > 0 }));
        composed.Should().Contain("nullableint is not null").And.Contain("id > 0");
        composed.Should().Contain($" as {ComposedAlias}");
        AssertScalarLegalisation(composed, 1);

        // Nullable literal forms keep the approved literal semantics in the same position.
        var literal = SqlCollapsed(ctx, e.Select(x => new { x.Id, Literal = x.Int == (int?)null }));
        literal.Should().Contain(Scalar("nullableint is null", LiteralAlias));
        AssertScalarLegalisation(literal, 1);
    }

    /// <summary>
    /// The same forms inside a JOIN <c>ON</c> clause, on the joined alias, with the join structure
    /// unchanged: table order, join kind and aliases are byte-identical to the same join without the
    /// null-safe predicate, and only the appended predicate differs.
    /// </summary>
    [Fact]
    public void JoinOnPredicate_ShouldRenderTotalBooleanShapes()
    {
        using var ctx = PostgresTestContext.Create();
        var inner = ctx.From<IT296ShapeEntity>();
        int? capturedValue = 42;
        string? capturedText = "ttwo";

        var plain = SqlCollapsed(ctx, ctx.From<IT296ShapeEntity>()
            .Join(inner, (a, b) => a.Id == b.Id)
            .Select(p => new { p.Item1.Id }));
        plain.Should().Contain("join t296_shape as ").And.Contain("on t1.id = t2.id");
        plain.Should().NotContain("left join").And.NotContain("right join").And.NotContain("is null");

        var equal = SqlCollapsed(ctx, ctx.From<IT296ShapeEntity>()
            .Join(inner, (a, b) => a.Id == b.Id && b.Int == capturedValue)
            .Select(p => new { p.Item1.Id }));
        equal.Should().StartWith(plain.Replace(" on ", " on ("), "only the ON predicate may change");
        equal.Should().Contain($"t2.nullableint is null and {P}capturedValue is null");
        equal.Should().Contain(
            $"t2.nullableint is not null and {P}capturedValue is not null and t2.nullableint = {P}capturedValue");

        var notEqual = SqlCollapsed(ctx, ctx.From<IT296ShapeEntity>()
            .Join(inner, (a, b) => a.Id == b.Id && b.Int != capturedValue)
            .Select(p => new { p.Item1.Id }));
        notEqual.Should().Contain($"t2.nullableint is null and {P}capturedValue is not null");
        notEqual.Should().Contain($"t2.nullableint is not null and {P}capturedValue is null");
        notEqual.Should().Contain(
            $"t2.nullableint is not null and {P}capturedValue is not null and t2.nullableint != {P}capturedValue");

        // Operand order must not change the ON translation either.
        SqlCollapsed(ctx, ctx.From<IT296ShapeEntity>()
                .Join(inner, (a, b) => a.Id == b.Id && capturedValue == b.Int)
                .Select(p => new { p.Item1.Id }))
            .Should().Be(equal);
        SqlCollapsed(ctx, ctx.From<IT296ShapeEntity>()
                .Join(inner, (a, b) => a.Id == b.Id && capturedValue != b.Int)
                .Select(p => new { p.Item1.Id }))
            .Should().Be(notEqual);

        // Captured string in ON.
        SqlCollapsed(ctx, ctx.From<IT296ShapeEntity>()
                .Join(inner, (a, b) => a.Id == b.Id && b.String == capturedText)
                .Select(p => new { p.Item1.Id }))
            .Should().Contain($"t2.somestring is null and {P}capturedText is null")
            .And.Contain($"t2.somestring is not null and {P}capturedText is not null and t2.somestring = {P}capturedText");

        // Presence forms in ON.
        var hasValue = SqlCollapsed(ctx, ctx.From<IT296ShapeEntity>()
            .Join(inner, (a, b) => a.Id == b.Id && b.Int.HasValue)
            .Select(p => new { p.Item1.Id }));
        hasValue.Should().StartWith(plain.Replace(" on ", " on (")).And.Contain("t2.nullableint is not null");

        var noValue = SqlCollapsed(ctx, ctx.From<IT296ShapeEntity>()
            .Join(inner, (a, b) => a.Id == b.Id && !b.Int.HasValue)
            .Select(p => new { p.Item1.Id }));
        noValue.Should().StartWith(plain.Replace(" on ", " on (")).And.Contain("t2.nullableint is null");

        // Nullable literal in ON keeps the literal presence semantics.
        SqlCollapsed(ctx, ctx.From<IT296ShapeEntity>()
                .Join(inner, (a, b) => a.Id == b.Id && b.Int == (int?)null)
                .Select(p => new { p.Item1.Id }))
            .Should().StartWith(plain.Replace(" on ", " on (")).And.Contain("t2.nullableint is null");
    }

    /// <summary>
    /// The same forms inside a correlated <c>Exists</c> body: the total shape is rendered on the
    /// inner alias and the outer association is retained (SQL shape only here; live association is
    /// covered by the integration phase).
    /// </summary>
    [Fact]
    public void CorrelatedExistsBody_ShouldRenderTotalBooleanShapes()
    {
        using var ctx = PostgresTestContext.Create();
        var outer = ctx.From<IT296ShapeEntity>();
        var inner = ctx.From<IT296ShapeEntity>();
        int? capturedValue = 42;
        string? capturedText = "ttwo";

        var equal = SqlCollapsed(ctx, outer
            .Where(x => SqlFunctions.Sql.exists(inner.Where(i => i.Id == x.Id && i.Int == capturedValue)))
            .Select(x => x.Id));
        equal.Should().Contain("exists(select *");
        equal.Should().Contain("t2.id = t1.id");
        equal.Should().Contain($"t2.nullableint is null and {P}capturedValue is null");
        equal.Should().Contain(
            $"t2.nullableint is not null and {P}capturedValue is not null and t2.nullableint = {P}capturedValue");

        var notEqual = SqlCollapsed(ctx, outer
            .Where(x => SqlFunctions.Sql.exists(inner.Where(i => i.Id == x.Id && i.Int != capturedValue)))
            .Select(x => x.Id));
        notEqual.Should().Contain("t2.id = t1.id");
        notEqual.Should().Contain($"t2.nullableint is null and {P}capturedValue is not null");
        notEqual.Should().Contain($"t2.nullableint is not null and {P}capturedValue is null");
        notEqual.Should().Contain(
            $"t2.nullableint is not null and {P}capturedValue is not null and t2.nullableint != {P}capturedValue");

        // Operand order.
        SqlCollapsed(ctx, outer
                .Where(x => SqlFunctions.Sql.exists(inner.Where(i => i.Id == x.Id && capturedValue == i.Int)))
                .Select(x => x.Id))
            .Should().Be(equal);
        SqlCollapsed(ctx, outer
                .Where(x => SqlFunctions.Sql.exists(inner.Where(i => i.Id == x.Id && capturedValue != i.Int)))
                .Select(x => x.Id))
            .Should().Be(notEqual);

        // Captured string in the correlated body.
        SqlCollapsed(ctx, outer
                .Where(x => SqlFunctions.Sql.exists(inner.Where(i => i.Id == x.Id && i.String == capturedText)))
                .Select(x => x.Id))
            .Should().Contain($"t2.somestring is null and {P}capturedText is null")
            .And.Contain("t2.id = t1.id");

        // Presence forms.
        var hasValue = SqlCollapsed(ctx, outer
            .Where(x => SqlFunctions.Sql.exists(inner.Where(i => i.Id == x.Id && i.Int.HasValue)))
            .Select(x => x.Id));
        hasValue.Should().Contain("t2.id = t1.id");
        hasValue.Should().Contain("t2.nullableint is not null");

        var noValue = SqlCollapsed(ctx, outer
            .Where(x => SqlFunctions.Sql.exists(inner.Where(i => i.Id == x.Id && !i.Int.HasValue)))
            .Select(x => x.Id));
        noValue.Should().Contain("t2.id = t1.id");
        noValue.Should().Contain("t2.nullableint is null");

        // Nullable literal forms keep the approved literal semantics and outer association.
        SqlCollapsed(ctx, outer
                .Where(x => SqlFunctions.Sql.exists(inner.Where(i => i.Id == x.Id && i.Int == (int?)null)))
                .Select(x => x.Id))
            .Should().Contain("t2.id = t1.id").And.Contain("t2.nullableint is null");
        SqlCollapsed(ctx, outer
                .Where(x => SqlFunctions.Sql.exists(inner.Where(i => i.Id == x.Id && i.Int != (int?)null)))
                .Select(x => x.Id))
            .Should().Contain("t2.id = t1.id").And.Contain("t2.nullableint is not null");
    }

    /// <summary>
    /// Guards: the approved rewrite is limited to captured operands and mapped presence in the approved
    /// positions. Column-to-column predicates (A17/A30), the relational operators (A18) and the
    /// excluded value positions ORDER BY / GROUP BY (A32) keep their previous translation.
    /// </summary>
    [Fact]
    public void GuardPins_ShouldRemainUnchanged()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IT296ShapeEntity>();
        var inner = ctx.From<IT296ShapeEntity>();
        int? capturedValue = 42;

        // A30: column-to-column in a JOIN ON is never null-safe.
        var columnJoin = SqlCollapsed(ctx, ctx.From<IT296ShapeEntity>()
            .Join(inner, (a, b) => a.Id == b.Id && a.Int == b.Int)
            .Select(p => new { p.Item1.Id }));
        columnJoin.Should().Contain("t1.nullableint = t2.nullableint")
            .And.NotContain("t1.nullableint is null")
            .And.NotContain("t2.nullableint is null");

        // A17: column-to-column inside a correlated Exists body.
        var columnExists = SqlCollapsed(ctx, e
            .Where(x => SqlFunctions.Sql.exists(inner.Where(i => i.Id == x.Id && i.Int == x.Int)))
            .Select(x => x.Id));
        columnExists.Should().Contain("t2.nullableint = t1.nullableint")
            .And.NotContain("t1.nullableint is null")
            .And.NotContain("t2.nullableint is null");

        // A18: <, >, <=, >= keep the ordinary operator and gain no null arms.
        SqlCollapsed(ctx, e.Where(x => x.Int < capturedValue).Select(x => x.Id))
            .Should().Contain($"nullableint < {P}capturedValue").And.NotContain("is null");
        SqlCollapsed(ctx, e.Where(x => x.Int > capturedValue).Select(x => x.Id))
            .Should().Contain($"nullableint > {P}capturedValue").And.NotContain("is null");
        SqlCollapsed(ctx, e.Where(x => x.Int <= capturedValue).Select(x => x.Id))
            .Should().Contain($"nullableint <= {P}capturedValue").And.NotContain("is null");
        SqlCollapsed(ctx, e.Where(x => x.Int >= capturedValue).Select(x => x.Id))
            .Should().Contain($"nullableint >= {P}capturedValue").And.NotContain("is null");

        // A32: ORDER BY keeps the value translation (no null arms, parameter still used).
        SqlCollapsed(ctx, e.OrderBy(x => x.Int == capturedValue).Select(x => x.Id))
            .Should().Contain($"nullableint = {P}capturedValue").And.NotContain(" is null");

        // A32: presence in ORDER BY / GROUP BY is still the previous typed rejection.
        var orderByHasValue = () => SqlCollapsed(ctx, e.OrderBy(x => x.Int.HasValue).Select(x => x.Id));
        orderByHasValue.Should().Throw<BuildSqlCommandException>()
            .WithMessage("Cannot resolve column for member HasValue");

        var groupByHasValue = () => SqlCollapsed(ctx, e.GroupBy(x => x.Int.HasValue).Select(x => x.Int));
        groupByHasValue.Should().Throw<BuildSqlCommandException>()
            .WithMessage("Cannot resolve column for member HasValue");
    }
}
