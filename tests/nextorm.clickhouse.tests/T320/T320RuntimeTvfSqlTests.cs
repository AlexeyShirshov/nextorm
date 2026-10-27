using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// The runtime <see cref="TvfEntityBuilder{T}"/> factories used by the ClickHouse generation tests
/// (matrix row MX-CH). The metadata is constructor-only and independent of the argument values, so one
/// factory serves the literal, captured, null and nested-expression argument shapes.
/// </summary>
internal static class T320ChTvf
{
    public static TvfEntityBuilder<ISimpleEntity> Echo(long id) => new("t320_echo");

    public static TvfEntityBuilder<ISimpleEntity> EchoNullable(long? id) => new("t320_echo_nullable");

    public static TvfEntityBuilder<ISimpleEntity> EchoPair(long id, int extra) => new("t320_echo_pair");

    public static TvfEntityBuilder<ISimpleEntity> JoinSource(long id) => new("t320_join_probe");

    public static TvfEntityBuilder<ISimpleEntity> IdentityAlpha(string label) => new("t320_identity_alpha", "t320_other");

    public static TvfEntityBuilder<ISimpleEntity> IdentityBeta(string label) => new("t320_identity_beta", "t320_other");

    public static TvfEntityBuilder<ISimpleEntity> IdentityOtherSchema(string label) => new("t320_identity_alpha", "t320_elsewhere");

    public static TvfEntityBuilder<ISimpleEntity> IdentityVerbatim(string label) => new(
        "t320_identity_alpha",
        "t320_other",
        null,
        null,
        new[] { 0 },
        TableFunctionSchema.None);

    public static TvfEntityBuilder<ISimpleEntity> EquivalentOne(long id) => new("t320_equivalent", "t320_other");

    public static TvfEntityBuilder<ISimpleEntity> EquivalentTwo(long id) => new("t320_equivalent", "t320_other");

    public static TvfEntityBuilder<ISimpleEntity> LeadingResultSchema(string tuples) => new(
        "values",
        null,
        null,
        null,
        new[] { 0 },
        TableFunctionSchema.LeadingArgument);

    public static TvfEntityBuilder<ISimpleEntity> AliasResultSchema(long id) => new(
        "t320_alias",
        null,
        null,
        null,
        Array.Empty<int>(),
        TableFunctionSchema.AliasColumnList);

    /// <summary>
    /// A custom runtime descriptor over the provider built-in name <c>numbers</c>: the dialect wraps the
    /// call by name (casting <c>UInt64 number</c> to <c>Int64</c>) for a user-defined descriptor too.
    /// </summary>
    public static TvfEntityBuilder<SqlFunctions.INumbersRow> Numbers(long count) => new("numbers");
}

/// <summary>
/// #320 (T320) ClickHouse SQL generation for the runtime TVF source (matrix row MX-CH). These tests never
/// open a database connection: the connection string is a placeholder, so they run on every build. The
/// cells are L literal, C capture, N null/default, X nested expression, A APPLY rejection, J ordinary
/// join, S type-changing Select, U clone/upcast/reuse, R result placement, F custom wrapper versus
/// built-in gating, H cache-hit refresh and I SQL identity. The live counterpart is
/// <c>T320ClickHouseTvfTests</c> in the integration project.
/// </summary>
[Trait("Category", "T320")]
public class T320RuntimeTvfSqlTests(ITestOutputHelper output)
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> command, bool storeInCache)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(command, false, storeInCache, CancellationToken.None);

    private static List<string> ParametersOf<T>(DbPreparedQueryCommand<T> prepared)
        => [.. prepared.DbCommandParams.Cast<DbParameter>().Select(p => $"{p.ParameterName}={p.Value ?? "null"}")];

    // ---- L/C/N/X: the four argument shapes --------------------------------------------------------

    [Fact]
    public void RuntimeTvf_L_LiteralArgument_ShouldRenderAnInlineLiteral()
    {
        using var ctx = ClickHouseTestContext.Create();

        var prepared = Prepare(ctx, ctx.From(() => T320ChTvf.Echo(42)).Select(r => r.Id), false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);
        output.WriteLine($"literal: sql=<<{sql}>>; parameters=[{string.Join(", ", parameters)}]");

        sql.Should().Be("select id\n from t320_echo(42) as `t1`", "a literal argument renders inline on the call");
        parameters.Should().BeEmpty("a literal argument is not parameterised");
    }

    [Fact]
    public void RuntimeTvf_C_CapturedArgument_ShouldBindAParameter()
    {
        using var ctx = ClickHouseTestContext.Create();
        var id = 42L;

        var prepared = Prepare(ctx, ctx.From(() => T320ChTvf.Echo(id)).Select(r => r.Id), false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);
        output.WriteLine($"capture: sql=<<{sql}>>; parameters=[{string.Join(", ", parameters)}]");

        sql.Should().Be("select id\n from t320_echo(@id) as `t1`", "a captured value is bound, never inlined");
        parameters.Should().Equal(new[] { "id=42" }, "the capture travels as one parameter with the captured value");
    }

    [Fact]
    public void RuntimeTvf_N_NullArgument_ShouldRenderNullWithoutAParameter()
    {
        using var ctx = ClickHouseTestContext.Create();

        var prepared = Prepare(ctx, ctx.From(() => T320ChTvf.EchoNullable(null)).Select(r => r.Id), false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);
        output.WriteLine($"null: sql=<<{sql}>>; parameters=[{string.Join(", ", parameters)}]");

        sql.Should().Be("select id\n from t320_echo_nullable(null) as `t1`", "a null argument stays the SQL NULL literal");
        parameters.Should().BeEmpty("a null literal is not replaced by a metadata-evaluation default");
    }

    [Fact]
    public void RuntimeTvf_X_NestedSqlFunctionArgument_ShouldKeepTheNestedCallAndTheParameter()
    {
        using var ctx = ClickHouseTestContext.Create();
        var captured = 42L;

        var prepared = Prepare(
            ctx,
            ctx.From(() => T320ChTvf.EchoNullable(SqlFunctions.Sql.nullif(captured, 0L))).Select(r => r.Id),
            false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);
        output.WriteLine($"nested: sql=<<{sql}>>; parameters=[{string.Join(", ", parameters)}]");

        sql.Should().Contain("from t320_echo_nullable(nullif(@captured, 0))", "the nested SQL function is the argument, not its value");
        sql.Should().NotContain("t320_echo_nullable(42)", "the nested expression is never evaluated into a constant");
        parameters.Should().Equal(new[] { "captured=42" }, "the capture inside the nested call is a bound parameter");
    }

    // ---- A: correlated APPLY is a named refusal on ClickHouse --------------------------------------

    [Fact]
    public void RuntimeTvf_A_CrossAndOuterApply_ShouldBeRejectedByTheDialect()
    {
        using var ctx = ClickHouseTestContext.Create();
        var extra = 7;

        // ClickHouse has no APPLY/LATERAL source, so the dialect rejects both forms by name instead of
        // rendering an ordinary join (which would silently drop the correlation).
        Action cross = () => Prepare(
            ctx,
            ctx.From<ISimpleEntity>()
                .CrossApply(row => T320ChTvf.EchoPair(row.Id, extra))
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id }),
            false);
        cross.Should().Throw<NotSupportedException>()
            .WithMessage("*CrossApply join is not supported by this SQL dialect*");

        Action outer = () => Prepare(
            ctx,
            ctx.From<ISimpleEntity>()
                .OuterApply(row => T320ChTvf.EchoPair(row.Id, extra))
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id }),
            false);
        outer.Should().Throw<NotSupportedException>()
            .WithMessage("*OuterApply join is not supported by this SQL dialect*");
    }

    // ---- J: ordinary lambda Join and a mixed source ---------------------------------------------

    [Fact]
    public void RuntimeTvf_J_OrdinaryJoinAndMixedSource_ShouldRenderTheTvfSource()
    {
        using var ctx = ClickHouseTestContext.Create();
        var joinId = 3L;

        var joined = Prepare(
            ctx,
            ctx.From<ISimpleEntity>()
                .Join(() => T320ChTvf.JoinSource(joinId), (left, right) => left.Id == right.Id)
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id }),
            false);
        var joinedSql = Normalize(joined.DbCommand.CommandText);
        output.WriteLine($"join: sql=<<{joinedSql}>>; parameters=[{string.Join(", ", ParametersOf(joined))}]");

        joinedSql.Should().Contain("join t320_join_probe(@joinId) as `t2`", "an ordinary lambda join renders the TVF as the right source");
        joinedSql.Should().NotContain("lateral", "an ordinary join is not an APPLY");
        ParametersOf(joined).Should().Equal("joinId=3");

        var fromId = 5L;
        var mixed = Prepare(
            ctx,
            ctx.From(() => T320ChTvf.Echo(fromId))
                .Join(ctx.From<ISimpleEntity>(), (tvf, entity) => tvf.Id == entity.Id)
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id }),
            false);
        var mixedSql = Normalize(mixed.DbCommand.CommandText);
        output.WriteLine($"mixed: sql=<<{mixedSql}>>; parameters=[{string.Join(", ", ParametersOf(mixed))}]");

        mixedSql.Should().Contain("from t320_echo(@fromId) as `t1`", "the TVF is the FROM source");
        mixedSql.Should().Contain("join simple_entity as `t2`", "the ordinary table composes as the joined source");
        ParametersOf(mixed).Should().Equal("fromId=5");
    }

    // ---- S/U: type-changing projection, clone/upcast/reuse ---------------------------------------

    [Fact]
    public void RuntimeTvf_S_TypeChangingSelect_ShouldKeepTheSourceAndItsArgument()
    {
        using var ctx = ClickHouseTestContext.Create();
        var id = 42L;

        // The composed projection changes the builder row type while the factory row type stays
        // ISimpleEntity: the source and its argument must survive that difference.
        var source = ctx.From(() => T320ChTvf.Echo(id).As(r => new { r.Id }));
        var prepared = Prepare(ctx, source.Select(r => r.Id), false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);
        output.WriteLine($"projection: sql=<<{sql}>>; parameters=[{string.Join(", ", parameters)}]");

        sql.Should().Contain("t320_echo(@id)", "the factory call and its argument survive the type-changing projection");
        parameters.Should().Equal("id=42");
    }

    [Fact]
    public void RuntimeTvf_U_CloneUpcastAndReuse_ShouldKeepTheSourceAndItsArgument()
    {
        using var ctx = ClickHouseTestContext.Create();
        var id = 7L;

        // Upcast: the descriptor is usable through the EntityBuilder<T> static type.
        TvfEntityBuilder<ISimpleEntity> descriptor = T320ChTvf.Echo(id);
        EntityBuilder<ISimpleEntity> upcast = descriptor;
        var clone = upcast.Clone();

        clone.Should().BeAssignableTo<EntityBuilder<ISimpleEntity>>();
        clone.Should().BeOfType<TvfEntityBuilder<ISimpleEntity>>("the clone keeps the runtime descriptor type");
        clone.Should().NotBeSameAs(upcast, "the clone is a copy, not the same builder");
        var cloned = (TvfEntityBuilder<ISimpleEntity>)clone;
        cloned.Name.Should().Be("t320_echo");
        cloned.Schema.Should().BeNull();
        cloned.ResultSchema.Should().Be(TableFunctionSchema.None);
        cloned.VerbatimArguments.Should().BeEmpty();

        // Reuse: two independent consumers of the same source shape render the same statement.
        var first = Prepare(ctx, ctx.From(() => T320ChTvf.Echo(id)).Select(r => r.Id), false);
        var second = Prepare(ctx, ctx.From(() => T320ChTvf.Echo(id)).Select(r => r.Id), false);
        var firstSql = Normalize(first.DbCommand.CommandText);
        var secondSql = Normalize(second.DbCommand.CommandText);
        output.WriteLine($"reuse: first=<<{firstSql}>>; second=<<{secondSql}>>; parameters=[{string.Join(", ", ParametersOf(second))}]");

        secondSql.Should().Be(firstSql, "reusing the same factory/descriptor shape renders the same statement");
        ParametersOf(second).Should().Equal(ParametersOf(first));
        ParametersOf(second).Should().Equal("id=7");
    }

    // ---- R: result placement (None / LeadingArgument / AliasColumnList) ---------------------------

    [Fact]
    public void RuntimeTvf_R_ResultPlacements_ShouldRenderNoneAndLeadingAndRefuseAliasColumns()
    {
        using var ctx = ClickHouseTestContext.Create();

        // None: the function supplies its own result shape, so the call is bare.
        var none = Prepare(ctx, ctx.From(() => T320ChTvf.Echo(42)).Select(r => r.Id), false);
        var noneSql = Normalize(none.DbCommand.CommandText);
        output.WriteLine($"placement none: sql=<<{noneSql}>>");
        noneSql.Should().Be("select id\n from t320_echo(42) as `t1`");

        // LeadingArgument: ClickHouse is the one provider that renders the row-derived structure as the
        // leading call argument (the values(...) form), before the verbatim tuple list.
        var leading = Prepare(
            ctx,
            ctx.From(() => T320ChTvf.LeadingResultSchema("(1), (2)")).Select(r => r.Id),
            false);
        var leadingSql = Normalize(leading.DbCommand.CommandText);
        output.WriteLine($"placement leading: sql=<<{leadingSql}>>");
        leadingSql.Should().Contain(
            "from values('id Int32', (1), (2))",
            "the row-derived structure is the leading argument, before the verbatim tuple list");

        // AliasColumnList: the PostgreSQL form is a named capability refusal here, never silently dropped.
        Action alias = () => Prepare(ctx, ctx.From(() => T320ChTvf.AliasResultSchema(42)).Select(r => r.Id), false);
        alias.Should().Throw<NotSupportedException>()
            .WithMessage("*'t320_alias'*'AliasColumnList' form*not supported by this provider*");
    }

    // ---- F: custom wrapper over a built-in name versus the built-in capability gate ---------------

    [Fact]
    public void RuntimeTvf_F_CustomNumbersWrapper_ShouldRenderTheCastAndTheBuiltInGateShouldReject()
    {
        using var ctx = ClickHouseTestContext.Create();
        var count = 3L;

        // A custom runtime descriptor over the name `numbers` goes through the dialect's name-based
        // wrapper: the unsigned number column is cast to Int64 so it maps to the declared row shape.
        var custom = Prepare(ctx, ctx.From(() => T320ChTvf.Numbers(count)).Select(r => r.Value), false);
        var customSql = Normalize(custom.DbCommand.CommandText);
        output.WriteLine($"custom numbers: sql=<<{customSql}>>; parameters=[{string.Join(", ", ParametersOf(custom))}]");

        customSql.Should().Contain(
            "from (select toInt64(number) as number\n from numbers(@count)) as `t1`",
            "the wrapper casts the built-in's UInt64 column for a custom descriptor too");
        ParametersOf(custom).Should().Equal("count=3");

        // The built-in route renders the same wrapped statement: the distinction under test is the
        // custom-versus-built-in classification, not a different rendering.
        var builtIn = Prepare(
            ctx,
            ctx.From(() => SqlFunctions.ClickHouse.numbers(count)).Select(r => r.Value),
            false);
        Normalize(builtIn.DbCommand.CommandText).Should().Be(customSql, "the cast wrapper is name-based and route-independent");

        // A built-in function the provider does not support is gated by the built-in support list, while
        // the custom descriptor above is user-defined and emitted verbatim.
        Action mismatch = () => Prepare(
            ctx,
            ctx.From(() => SqlFunctions.Postgres.generate_series(1, 3)).Select(r => r.Value),
            false);
        mismatch.Should().Throw<NotSupportedException>()
            .WithMessage("*'generate_series'*not supported by this provider*");
    }

    // ---- H/I: cache-hit refresh and SQL identity -------------------------------------------------

    [Fact]
    public void RuntimeTvf_H_CacheHit_ShouldRefreshTheCapturedArgument()
    {
        using var ctx = ClickHouseTestContext.Create();
        ctx.PurgeQueryCache();
        var id = 1L;

        QueryCommand<int> Build() => ctx.From(() => T320ChTvf.Echo(id)).Select(r => r.Id);

        var first = Prepare(ctx, Build(), true);
        var firstSql = Normalize(first.DbCommand.CommandText);
        output.WriteLine($"refresh run 1: sql=<<{firstSql}>>; parameters=[{string.Join(", ", ParametersOf(first))}]");
        ParametersOf(first).Should().Equal("id=1");

        id = 2L;
        var second = Prepare(ctx, Build(), true);
        var secondSql = Normalize(second.DbCommand.CommandText);
        output.WriteLine(
            $"refresh run 2: sql=<<{secondSql}>>; parameters=[{string.Join(", ", ParametersOf(second))}]; "
            + $"ReferenceEquals(run1, run2)={ReferenceEquals(first, second)}");

        ReferenceEquals(first, second).Should().BeTrue(
            "the same source shape must reuse the cached plan so the refresh path is exercised");
        secondSql.Should().Be(firstSql, "a capture change must not re-render the statement");
        ParametersOf(second).Should().Equal(new[] { "id=2" }, "the cache hit binds the new capture, never the stale 1");
    }

    [Fact]
    public void RuntimeTvf_I_DifferentAndEquivalentSqlIdentities_ShouldSplitAndSharePlans()
    {
        using var ctx = ClickHouseTestContext.Create();
        ctx.PurgeQueryCache();

        var before = QueryPlanStore.Values.Count();

        var alpha = Prepare(ctx, ctx.From(() => T320ChTvf.IdentityAlpha("raw")).Select(r => r.Id), true);
        var afterAlpha = QueryPlanStore.Values.Count();

        var beta = Prepare(ctx, ctx.From(() => T320ChTvf.IdentityBeta("raw")).Select(r => r.Id), true);
        var afterBeta = QueryPlanStore.Values.Count();

        var schema = Prepare(ctx, ctx.From(() => T320ChTvf.IdentityOtherSchema("raw")).Select(r => r.Id), true);
        var verbatim = Prepare(ctx, ctx.From(() => T320ChTvf.IdentityVerbatim("raw")).Select(r => r.Id), true);
        var afterAll = QueryPlanStore.Values.Count();

        output.WriteLine(
            $"identity variants: plan store {before} -> {afterAlpha} -> {afterBeta} -> {afterAll}; "
            + $"alpha=<<{Normalize(alpha.DbCommand.CommandText)}>>; "
            + $"beta=<<{Normalize(beta.DbCommand.CommandText)}>>; "
            + $"schema=<<{Normalize(schema.DbCommand.CommandText)}>>; "
            + $"verbatim=<<{Normalize(verbatim.DbCommand.CommandText)}>>");

        afterAlpha.Should().Be(before + 1, "the first identity stores exactly one plan entry");
        afterBeta.Should().Be(afterAlpha + 1, "a different function name is a different SQL identity");
        afterAll.Should().Be(afterBeta + 2, "a different schema and a different verbatim-argument shape are different identities");

        var statements = new[]
        {
            Normalize(alpha.DbCommand.CommandText),
            Normalize(beta.DbCommand.CommandText),
            Normalize(schema.DbCommand.CommandText),
            Normalize(verbatim.DbCommand.CommandText),
        };
        statements.Should().OnlyHaveUniqueItems("each runtime SQL identity has its own statement");
        statements[0].Should().Contain("t320_other.t320_identity_alpha");
        statements[1].Should().Contain("t320_other.t320_identity_beta");
        statements[2].Should().Contain("t320_elsewhere.t320_identity_alpha");
        statements[3].Should().Contain("(raw)", "a verbatim argument is emitted as SQL, not bound");
        ReferenceEquals(alpha, beta).Should().BeFalse();

        // Equivalent identity across two distinct factories: same statement and one shared plan entry.
        var id = 5L;
        var beforeEquivalent = QueryPlanStore.Values.Count();
        var one = Prepare(ctx, ctx.From(() => T320ChTvf.EquivalentOne(id)).Select(r => r.Id), true);
        var afterOne = QueryPlanStore.Values.Count();
        var two = Prepare(ctx, ctx.From(() => T320ChTvf.EquivalentTwo(id)).Select(r => r.Id), true);
        var afterTwo = QueryPlanStore.Values.Count();

        output.WriteLine(
            $"equivalent identities: plan store {beforeEquivalent} -> {afterOne} -> {afterTwo}; "
            + $"ReferenceEquals={ReferenceEquals(one, two)}; sql=<<{Normalize(two.DbCommand.CommandText)}>>");

        afterOne.Should().Be(beforeEquivalent + 1);
        afterTwo.Should().Be(afterOne, "equivalent metadata and argument shapes must share one plan entry");
        ReferenceEquals(one, two).Should().BeTrue(
            "two distinct factories with the same SQL identity must reuse the cached plan instance");
        Normalize(two.DbCommand.CommandText).Should().Be(Normalize(one.DbCommand.CommandText));
        ParametersOf(two).Should().Equal(ParametersOf(one));
        ParametersOf(two).Should().Equal("id=5");
    }
}
