using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// T323 (#323) D1: PostgreSQL generation parity between a <c>SelectGroup</c> projection and the
/// equivalent explicit <c>GroupBy</c>/<c>Select</c> form, the key-inference shape rules that are
/// observable in SQL, and the composition guards. Only preparation runs: the context carries a
/// placeholder connection, so no database is opened.
/// </summary>
public class T323SelectGroupSqlTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        return (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    /// <summary>
    /// Extracts the rendered <c>GROUP BY</c> line. Assertions on the key list must not be satisfied by
    /// an aggregate's argument in the select list, which precedes the clause.
    /// </summary>
    private static string GroupByLine(string sql)
    {
        var index = sql.IndexOf("group by ", StringComparison.Ordinal);
        index.Should().BeGreaterThanOrEqualTo(0, "the rendered SQL must carry a GROUP BY clause");
        var end = sql.IndexOf('\n', index);
        return end < 0 ? sql[index..] : sql[index..end];
    }

    [Fact]
    public void SingleKey_ShouldEqualExplicitSql()
    {
        using var ctx = PostgresTestContext.Create();

        var inferred = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new { x.Id, C = SqlFunctions.Sql.count() });
        var explicitForm = ctx.From<T323LegacyEntity>()
            .GroupBy(x => x.Id)
            .Select(x => new { x.Id, C = SqlFunctions.Sql.count() });

        SqlOf(ctx, inferred).Should().Be(SqlOf(ctx, explicitForm));
    }

    [Fact]
    public void TwoKeysTwoAggregates_ShouldEqualExplicitSql()
    {
        using var ctx = PostgresTestContext.Create();

        var inferred = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new { x.Id, x.Group2, S = SqlFunctions.Sql.sum(x.Amount), C = SqlFunctions.Sql.count() });
        var explicitForm = ctx.From<T323LegacyEntity>()
            .GroupBy(x => new { x.Id, x.Group2 })
            .Select(x => new { x.Id, x.Group2, S = SqlFunctions.Sql.sum(x.Amount), C = SqlFunctions.Sql.count() });

        SqlOf(ctx, inferred).Should().Be(SqlOf(ctx, explicitForm));
    }

    [Fact]
    public void DateTruncAndSum_ShouldEqualExplicitSql()
    {
        using var ctx = PostgresTestContext.Create();

        var inferred = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new
            {
                Q = SqlFunctions.Sql.date_trunc("quarter", x.OrderedAt),
                S = SqlFunctions.Sql.sum(x.Amount),
            });
        var explicitForm = ctx.From<T323LegacyEntity>()
            .GroupBy(x => SqlFunctions.Sql.date_trunc("quarter", x.OrderedAt))
            .Select(x => new
            {
                Q = SqlFunctions.Sql.date_trunc("quarter", x.OrderedAt),
                S = SqlFunctions.Sql.sum(x.Amount),
            });

        SqlOf(ctx, inferred).Should().Be(SqlOf(ctx, explicitForm));
    }

    [Fact]
    public void JoinedAggregate_ShouldEqualExplicitSql()
    {
        using var ctx = PostgresTestContext.Create();
        var joined = ctx.From<T323LegacyEntity>().Join(ctx.From<IComplexEntity>(), (a, b) => a.Id == b.Id);

        var inferred = joined
            .SelectGroup(p => new { p.Item1.Id, S = SqlFunctions.Sql.sum(p.Item1.Amount) });
        var explicitForm = joined
            .GroupBy(p => p.Item1.Id)
            .Select(p => new { p.Item1.Id, S = SqlFunctions.Sql.sum(p.Item1.Amount) });

        SqlOf(ctx, inferred).Should().Be(SqlOf(ctx, explicitForm));
    }

    [Fact]
    public void JoinedKeyAndAggregateAcrossItems_ShouldEqualExplicitSql()
    {
        using var ctx = PostgresTestContext.Create();
        var joined = ctx.From<T323LegacyEntity>().Join(ctx.From<IComplexEntity>(), (a, b) => a.Id == b.Id);

        var inferred = joined
            .SelectGroup(p => new { p.Item2.String, S = SqlFunctions.Sql.sum(p.Item1.Amount) });
        var explicitForm = joined
            .GroupBy(p => p.Item2.String)
            .Select(p => new { p.Item2.String, S = SqlFunctions.Sql.sum(p.Item1.Amount) });

        SqlOf(ctx, inferred).Should().Be(SqlOf(ctx, explicitForm));
    }

    [Fact]
    public void OrderOfKeys_ShouldFollowFirstAppearance()
    {
        using var ctx = PostgresTestContext.Create();

        // The aggregate comes first in the anonymous shape, so the keys appear in the order the row
        // expressions are visited: Group2, then Id.
        var inferred = ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new { C = SqlFunctions.Sql.count(), G = x.Group2, I = x.Id });
        var sameOrder = ctx.From<T323LegacyEntity>()
            .GroupBy(x => new { x.Group2, x.Id })
            .Select(x => new { C = SqlFunctions.Sql.count(), G = x.Group2, I = x.Id });
        var reversed = ctx.From<T323LegacyEntity>()
            .GroupBy(x => new { x.Id, x.Group2 })
            .Select(x => new { C = SqlFunctions.Sql.count(), G = x.Group2, I = x.Id });

        var inferredSql = SqlOf(ctx, inferred);
        var sameOrderSql = SqlOf(ctx, sameOrder);

        inferredSql.Should().Be(sameOrderSql);
        GroupByLine(inferredSql).Should().Be(GroupByLine(sameOrderSql));
        GroupByLine(inferredSql).Should().NotBe(GroupByLine(SqlOf(ctx, reversed)));
    }

    [Fact]
    public void AggregateArguments_ShouldNotBecomeKeys()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<T323LegacyEntity>()
            .SelectGroup(x => new { K = x.Id, V = SqlFunctions.Sql.sum(x.Amount) }));

        var keys = GroupByLine(sql);
        keys.Should().ContainEquivalentOf("id");
        keys.Should().NotContainEquivalentOf("amount", "an aggregate's argument is opaque and never a grouping key");
    }

    [Fact]
    public void Guards_ShouldRejectExplicitGrouping()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<T323LegacyEntity>();

        FluentActions.Invoking(() => e.GroupBy(x => x.Id).SelectGroup(x => new { x.Id }))
            .Should().Throw<QueryPreparationException>().WithMessage("*cannot be combined with explicit grouping*");
        FluentActions.Invoking(() => e.GroupByRollup(x => x.Id).SelectGroup(x => new { x.Id }))
            .Should().Throw<QueryPreparationException>().WithMessage("*cannot be combined with explicit grouping*");
        FluentActions.Invoking(() => e.GroupByCube(x => x.Id).SelectGroup(x => new { x.Id }))
            .Should().Throw<QueryPreparationException>().WithMessage("*cannot be combined with explicit grouping*");
        FluentActions.Invoking(() => e.GroupByGroupingSets(x => new { x.Id, x.Group2 }, [0], [1]).SelectGroup(x => new { x.Id }))
            .Should().Throw<QueryPreparationException>().WithMessage("*cannot be combined with explicit grouping*");
        FluentActions.Invoking(() => e.WithTotals().SelectGroup(x => new { x.Id }))
            .Should().Throw<QueryPreparationException>().WithMessage("*cannot be combined with explicit grouping*");
        FluentActions.Invoking(() => e.SelectGroup<int>(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IdentityProjection_ShouldBeRejected()
    {
        using var ctx = PostgresTestContext.Create();

        FluentActions.Invoking(() => SqlOf(ctx, ctx.From<T323LegacyEntity>().SelectGroup(x => x)))
            .Should().Throw<QueryPreparationException>()
            .WithMessage("*identity projection*");
    }

    [Fact]
    public void ExplicitForm_ShouldStillRenderLegacySql()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<T323LegacyEntity>()
            .GroupBy(x => x.Id)
            .Select(x => new { x.Id, C = SqlFunctions.Sql.count() }));

        GroupByLine(sql).Should().BeEquivalentTo("group by Id");
        sql.Should().NotContain("sg_key_", "the inferred-key aliases belong to SelectGroup only");
    }
}

/// <summary>
/// T323 (#323) D3a: the aggregate annotation surface as a row source for <c>SelectGroup</c> key
/// inference — <see cref="SqlFunctionAttribute.IsAggregate"/> semantics (a method attribute replaces
/// the declaring type's attribute, it never merges), the PostgreSQL advanced aggregate families, and
/// the CLR-name collision (<c>sum</c>) through both <c>SelectGroup</c> and the ordinary <c>Select</c>.
/// Generation only: the context carries a placeholder connection, so no database is opened.
/// </summary>
public class T323SelectGroupAggregateAttributeTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        return (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    /// <summary>
    /// Extracts the rendered <c>GROUP BY</c> line. Assertions on the key list must not be satisfied by
    /// an aggregate's argument in the select list, which precedes the clause.
    /// </summary>
    private static string GroupByLine(string sql)
    {
        var index = sql.IndexOf("group by ", StringComparison.Ordinal);
        index.Should().BeGreaterThanOrEqualTo(0, "the rendered SQL must carry a GROUP BY clause");
        var end = sql.IndexOf('\n', index);
        return end < 0 ? sql[index..] : sql[index..end];
    }

    [Fact]
    public void AttributeDefaults_ShouldBeScalar()
    {
        using var ctx = PostgresTestContext.Create();

        // The method attribute leaves IsAggregate unset, so the default is a scalar: its argument is a
        // legitimate grouping key and the whole call becomes the key.
        var inferred = ctx.From<T323AggEntity>()
            .SelectGroup(x => new { V = T323ScalarUdf.F(x.Amount) });
        var explicitForm = ctx.From<T323AggEntity>()
            .GroupBy(x => T323ScalarUdf.F(x.Amount))
            .Select(x => new { V = T323ScalarUdf.F(x.Amount) });

        var sql = SqlOf(ctx, inferred);

        sql.Should().Be(SqlOf(ctx, explicitForm));
        sql.Should().Contain("t323_f(");
        GroupByLine(sql).Should().ContainEquivalentOf(
            "amount",
            "an unannotated (scalar) function does not keep its argument opaque");
    }

    [Fact]
    public void ClassAggregateAttribute_ShouldApplyWithoutMethodAttribute()
    {
        using var ctx = PostgresTestContext.Create();

        // No method attribute at all: the declaring-type attribute applies entirely, including
        // IsAggregate, and the SQL name falls back to the CLR method name.
        var inferred = ctx.From<T323AggEntity>()
            .SelectGroup(x => new { x.Id, V = T323ClassAggregateUdf.T323ClsAgg(x.Amount) });
        var explicitForm = ctx.From<T323AggEntity>()
            .GroupBy(x => x.Id)
            .Select(x => new { x.Id, V = T323ClassAggregateUdf.T323ClsAgg(x.Amount) });

        var sql = SqlOf(ctx, inferred);

        sql.Should().Be(SqlOf(ctx, explicitForm));
        sql.Should().Contain("T323ClsAgg(");
        GroupByLine(sql).Should().NotContainEquivalentOf(
            "amount",
            "the declaring-type IsAggregate makes the call an opaque aggregate boundary");
    }

    [Fact]
    public void MethodAttribute_ShouldReplaceClassAttributeEntirely()
    {
        using var ctx = PostgresTestContext.Create();

        // The class says cls_fn + IsAggregate=true; the method attribute replaces that annotation
        // entirely instead of merging with it.
        var inferred = ctx.From<T323AggEntity>()
            .SelectGroup(x => new { V = T323MixedUdf.M(x.Amount) });
        var explicitForm = ctx.From<T323AggEntity>()
            .GroupBy(x => T323MixedUdf.M(x.Amount))
            .Select(x => new { V = T323MixedUdf.M(x.Amount) });

        var sql = SqlOf(ctx, inferred);

        sql.Should().Be(SqlOf(ctx, explicitForm));
        sql.Should().Contain("meth_fn(");
        sql.Should().NotContain("cls_fn", "the method attribute replaces the type attribute, it never merges");
        GroupByLine(sql).Should().ContainEquivalentOf(
            "amount",
            "IsAggregate must not leak from the class: the effective annotation is scalar, so its argument groups");
    }

    [Fact]
    public void AnnotatedScalarNamedSum_ShouldUseDeclaredSqlName()
    {
        using var ctx = PostgresTestContext.Create();

        // A CLR method called sum on a non-CommonFunctions type is not the built-in aggregate: it
        // renders its declared SQL name and stays a scalar key source.
        var inferred = ctx.From<T323AggEntity>()
            .SelectGroup(x => new { V = T323CollisionUdf.sum(x.Amount) });
        var explicitForm = ctx.From<T323AggEntity>()
            .GroupBy(x => T323CollisionUdf.sum(x.Amount))
            .Select(x => new { V = T323CollisionUdf.sum(x.Amount) });

        var sql = SqlOf(ctx, inferred);

        sql.Should().Be(SqlOf(ctx, explicitForm));
        sql.Should().Contain("t323_my_scalar(");
        sql.Should().Contain("group by");
        sql.Should().NotContain("sum(", "the CLR name never reaches the SQL text through the attribute");
    }

    [Fact]
    public void AnnotatedAggregateNamedSum_ShouldUseDeclaredSqlName()
    {
        using var ctx = PostgresTestContext.Create();

        var inferred = ctx.From<T323AggEntity>()
            .SelectGroup(x => new { x.Id, V = T323CollisionAggregateUdf.sum(x.Amount) });
        var explicitForm = ctx.From<T323AggEntity>()
            .GroupBy(x => x.Id)
            .Select(x => new { x.Id, V = T323CollisionAggregateUdf.sum(x.Amount) });

        var sql = SqlOf(ctx, inferred);

        sql.Should().Be(SqlOf(ctx, explicitForm));
        sql.Should().Contain("t323_my_scalar(");
        GroupByLine(sql).Should().NotContainEquivalentOf(
            "amount",
            "IsAggregate = true keeps the annotated call opaque");
        sql.Should().NotContain("sum(", "the CLR name never reaches the SQL text through the attribute");
    }

    [Fact]
    public void OrdinarySelect_ShouldPreserveAnnotatedCollisionSemantics()
    {
        using var ctx = PostgresTestContext.Create();

        // The annotation classifies a call; it never changes how the call is rendered. Both plain
        // Select shapes must therefore render identical SQL, and SelectGroup must reuse that name.
        var scalarSelect = SqlOf(ctx, ctx.From<T323AggEntity>()
            .Select(x => new { x.Id, V = T323CollisionUdf.sum(x.Amount) }));
        var aggregateSelect = SqlOf(ctx, ctx.From<T323AggEntity>()
            .Select(x => new { x.Id, V = T323CollisionAggregateUdf.sum(x.Amount) }));
        var scalarGroup = SqlOf(ctx, ctx.From<T323AggEntity>()
            .SelectGroup(x => new { x.Id, V = T323CollisionUdf.sum(x.Amount) }));

        aggregateSelect.Should().Be(scalarSelect, "IsAggregate is a classification, never a rendering switch");
        scalarSelect.Should().Contain("t323_my_scalar(").And.NotContain("sum(");
        scalarGroup.Should().Contain("t323_my_scalar(", "SelectGroup renders the same declared SQL name as Select");
        scalarGroup.Should().NotContain("sum(");
    }

    [Fact]
    public void BuiltinAggregates_ShouldRetainLegacySql()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<T323AggEntity>()
            .SelectGroup(x => new { x.Id, S = SqlFunctions.Sql.sum(x.Amount), C = SqlFunctions.Sql.count() }));

        sql.Should().Contain("sum(", "the built-in aggregate surface is untouched");
        sql.Should().Contain("count(");
    }

    [Fact]
    public void PostgresAdvancedAggregate_ShouldBeOpaque()
    {
        using var ctx = PostgresTestContext.Create();

        // bool_and is rendered by AdvancedAggregateTranslator, so the classifier must agree with the
        // renderer: its argument is not a key.
        var inferred = ctx.From<T323AggEntity>()
            .SelectGroup(x => new { x.Id, B = SqlFunctions.Postgres.bool_and(x.Flag) });
        var explicitForm = ctx.From<T323AggEntity>()
            .GroupBy(x => x.Id)
            .Select(x => new { x.Id, B = SqlFunctions.Postgres.bool_and(x.Flag) });

        var sql = SqlOf(ctx, inferred);

        sql.Should().Be(SqlOf(ctx, explicitForm));
        sql.Should().Contain("bool_and(");
        GroupByLine(sql).Should().NotContainEquivalentOf(
            "flag",
            "a provider-advanced aggregate is an opaque boundary for key inference");
    }
}

/// <summary>A plain row for the D3a annotation cells; <c>Flag</c> feeds a boolean aggregate.</summary>
public class T323AggEntity
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public bool Flag { get; set; }
}

/// <summary>Method-level <see cref="SqlFunctionAttribute"/> with the default (scalar) <c>IsAggregate</c>.</summary>
public static class T323ScalarUdf
{
    [SqlFunction("t323_f")]
    public static decimal F(decimal value) => value;
}

/// <summary>A declaring-type <see cref="SqlFunctionAttribute"/> that marks every member an aggregate.</summary>
[SqlFunction(IsAggregate = true)]
public static class T323ClassAggregateUdf
{
    /// <summary>No method attribute: the type attribute applies entirely, the SQL name is the CLR name.</summary>
    public static decimal T323ClsAgg(decimal value) => value;
}

/// <summary>A type attribute (name + <c>IsAggregate</c>) plus a method attribute that must replace it.</summary>
[SqlFunction("cls_fn", IsAggregate = true)]
public static class T323MixedUdf
{
    [SqlFunction("meth_fn")]
    public static decimal M(decimal value) => value;
}

/// <summary>A CLR method whose name collides with the built-in <c>sum</c>, mapped to a scalar.</summary>
public static class T323CollisionUdf
{
    [SqlFunction("t323_my_scalar")]
    public static decimal sum(decimal value) => value;
}

/// <summary>The same CLR-name collision, annotated as an aggregate.</summary>
public static class T323CollisionAggregateUdf
{
    [SqlFunction("t323_my_scalar", IsAggregate = true)]
    public static decimal sum(decimal value) => value;
}

/// <summary>
/// T323 (#323) D3b: the two key-less <c>SelectGroup</c> modes. A global aggregate renders one row for
/// the whole source and its <c>HAVING</c> is a filter on that row (no <c>GROUP BY</c>); a constant
/// projection renders <c>select distinct ...</c> and its row-independent <c>HAVING</c> is a pre-group
/// <c>WHERE</c> filter. The legacy ungrouped <c>HAVING</c> and the explicit grouped <c>HAVING</c> are
/// pinned as controls so the renderer change cannot move them. Only preparation runs: the context
/// carries a placeholder connection, so no database is opened.
/// </summary>
public class T323SelectGroupDegenerateModesTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        return (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    [Fact]
    public void GlobalAggregateHaving_ShouldEmitHaving()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<T323LegacyEntity>()
            .Having(x => SqlFunctions.Sql.sum(x.Amount) > 0)
            .SelectGroup(x => new { S = SqlFunctions.Sql.sum(x.Amount) }));

        sql.Should().Contain(" having ", "a global aggregate has one row to filter");
        sql.Should().Contain("sum(", "the aggregate projection is unchanged");
        sql.Should().NotContain("group by", "a global aggregate has no grouping key");
    }

    [Fact]
    public void GlobalAggregateHaving_ShouldBindCapturedThreshold()
    {
        decimal threshold = 13m;
        using var ctx = PostgresTestContext.Create();

        var prepared = Prepare(ctx, ctx.From<T323LegacyEntity>()
            .Having(x => SqlFunctions.Sql.sum(x.Amount) > threshold)
            .SelectGroup(x => new { S = SqlFunctions.Sql.sum(x.Amount) }));
        var sql = Normalize(prepared.DbCommand.CommandText);

        sql.Should().Contain(" having ");
        sql.Should().Contain("@", "the captured threshold is bound as a parameter");
        sql.Should().NotContain("13", "the captured value is never spliced into the statement");
    }

    [Fact]
    public void GlobalAggregateHaving_OrderByAggregate_ShouldRender()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<T323LegacyEntity>()
            .Having(x => SqlFunctions.Sql.sum(x.Amount) > 0)
            .OrderBy(x => SqlFunctions.Sql.sum(x.Amount))
            .SelectGroup(x => new { S = SqlFunctions.Sql.sum(x.Amount) }));

        var havingIndex = sql.IndexOf(" having ", StringComparison.Ordinal);
        var orderByIndex = sql.IndexOf(" order by ", StringComparison.Ordinal);

        havingIndex.Should().BeGreaterThanOrEqualTo(0);
        orderByIndex.Should().BeGreaterThan(havingIndex, "HAVING belongs to the group filter, ORDER BY to the output");
    }

    [Fact]
    public void ConstantProjection_ShouldRenderDistinct()
    {
        using var ctx = PostgresTestContext.Create();

        var grouped = SqlOf(ctx, ctx.From<T323LegacyEntity>().SelectGroup(x => new { V = 1 }));

        grouped.TrimStart().StartsWith("select distinct", StringComparison.OrdinalIgnoreCase)
            .Should().BeTrue($"a key-less constant projection must collapse to one row, got: {grouped}");
        grouped.Should().NotContain("group by");

        var legacy = SqlOf(ctx, ctx.From<T323LegacyEntity>().Select(x => new { V = 1 }));
        legacy.Should().NotContain("distinct", "the DISTINCT rendering is gated on the SelectGroup mode");
    }

    [Fact]
    public void ConstantProjection_ConstantHaving_ShouldBeWhereFilter()
    {
        int min = 0;
        using var ctx = PostgresTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<T323LegacyEntity>()
            .Having(x => 1 > min)
            .SelectGroup(x => new { V = 1 }));

        sql.Should().Contain(" where ", "a row-independent HAVING has no group and filters rows instead");
        sql.Should().NotContain(" having ", "the predicate must not be rendered as a group filter");
        sql.Should().NotContain("group by");
    }

    [Fact]
    public void LegacyUngroupedHaving_ShouldStillBeDropped()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<T323LegacyEntity>()
            .Having(x => x.Id > 1)
            .Select(x => new { x.Id }));

        sql.Should().NotContain("having", "an ungrouped legacy HAVING keeps its historical behaviour");
    }

    [Fact]
    public void ExplicitGroupedHaving_ShouldStillRender()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<T323LegacyEntity>()
            .GroupBy(x => x.Id)
            .Having(x => SqlFunctions.Sql.sum(x.Amount) > 0)
            .Select(x => new { x.Id }));

        sql.Should().Contain(" having ", "the explicit grouped HAVING path is untouched");
    }
}
