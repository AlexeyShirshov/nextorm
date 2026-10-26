using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.MySql.Tests;

/// <summary>
/// The runtime <see cref="TvfEntityBuilder{T}"/> factories used by the MySQL generation tests. The
/// metadata is constructor-only and independent of the argument values, so one factory can serve the
/// literal, captured, null and nested-expression argument shapes.
/// </summary>
internal static class T320MyTvf
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

    public static TvfEntityBuilder<ISimpleEntity> LeadingResultSchema(long id) => new(
        "t320_leading",
        null,
        null,
        null,
        Array.Empty<int>(),
        TableFunctionSchema.LeadingArgument);

    public static TvfEntityBuilder<ISimpleEntity> AliasResultSchema(long id) => new(
        "t320_alias",
        null,
        null,
        null,
        Array.Empty<int>(),
        TableFunctionSchema.AliasColumnList);

    public static TvfEntityBuilder<ISimpleEntity> CustomGenerateSeries(long start, long stop) => new("generate_series");
}

/// <summary>
/// Row shape of the custom <c>JSON_TABLE</c> declaration: the mapped row type supplies the projection
/// types while <see cref="TvfEntityBuilder{T}.CallClause"/> supplies the verbatim column definitions.
/// </summary>
public interface IT320JsonTableRow
{
    [Column("id")]
    int Id { get; set; }

    [Column("name")]
    string? Name { get; set; }
}

/// <summary>
/// The runtime <c>JSON_TABLE</c> declaration as a companion of <see cref="T320MyTvf.CustomJsonTable"/>:
/// a user-defined table function is emitted verbatim, so the call clause carries the JSON path and the
/// column definitions.
/// </summary>
internal static class T320MyJsonTableTvf
{
    public static TvfEntityBuilder<IT320JsonTableRow> JsonTable(string document) => new(
        "json_table",
        null,
        null,
        ", '$[*]' columns(id int path '$.id', name varchar(50) path '$.name')",
        Array.Empty<int>(),
        TableFunctionSchema.None);
}

/// <summary>
/// #320 (T320) MySQL SQL generation for the runtime TVF source (matrix row MX-MY). These tests never
/// open a database connection: the connection string is a placeholder, so they run on every build. The
/// live counterpart is <c>T320MySqlTvfTests</c> in the integration project.
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
        using var ctx = MySqlTestContext.Create();

        var prepared = Prepare(ctx, ctx.From(() => T320MyTvf.Echo(42)).Select(r => r.Id), false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);
        output.WriteLine($"literal: sql=<<{sql}>>; parameters=[{string.Join(", ", parameters)}]");

        sql.Should().Be("select id\n from t320_echo(42) as `t1`", "a literal argument renders inline on the call");
        parameters.Should().BeEmpty("a literal argument is not parameterised");
    }

    [Fact]
    public void RuntimeTvf_C_CapturedArgument_ShouldBindAParameter()
    {
        using var ctx = MySqlTestContext.Create();
        var id = 42L;

        var prepared = Prepare(ctx, ctx.From(() => T320MyTvf.Echo(id)).Select(r => r.Id), false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);
        output.WriteLine($"capture: sql=<<{sql}>>; parameters=[{string.Join(", ", parameters)}]");

        sql.Should().Be("select id\n from t320_echo(@id) as `t1`", "a captured value is bound, never inlined");
        parameters.Should().Equal(new[] { "id=42" }, "the capture travels as one parameter with the captured value");
    }

    [Fact]
    public void RuntimeTvf_N_NullArgument_ShouldRenderNullWithoutAParameter()
    {
        using var ctx = MySqlTestContext.Create();

        var prepared = Prepare(ctx, ctx.From(() => T320MyTvf.EchoNullable(null)).Select(r => r.Id), false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);
        output.WriteLine($"null: sql=<<{sql}>>; parameters=[{string.Join(", ", parameters)}]");

        sql.Should().Be("select id\n from t320_echo_nullable(null) as `t1`", "a null argument stays the SQL NULL literal");
        parameters.Should().BeEmpty("a null literal is not replaced by a metadata-evaluation default");
    }

    [Fact]
    public void RuntimeTvf_X_NestedSqlFunctionArgument_ShouldKeepTheNestedCallAndTheParameter()
    {
        using var ctx = MySqlTestContext.Create();
        var captured = 42L;

        var prepared = Prepare(
            ctx,
            ctx.From(() => T320MyTvf.EchoNullable(SqlFunctions.Sql.nullif(captured, 0L))).Select(r => r.Id),
            false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);
        output.WriteLine($"nested: sql=<<{sql}>>; parameters=[{string.Join(", ", parameters)}]");

        sql.Should().Contain("from t320_echo_nullable(nullif(@captured, 0))", "the nested SQL function is the argument, not its value");
        sql.Should().NotContain("t320_echo_nullable(42)", "the nested expression is never evaluated into a constant");
        parameters.Should().Equal(new[] { "captured=42" }, "the capture inside the nested call is a bound parameter");
    }

    // ---- A: correlated APPLY (function in FROM, implicit correlation, no LATERAL keyword) ---------

    [Fact]
    public void RuntimeTvf_A_CorrelatedJsonTableApply_ShouldRenderTheFunctionDirectlyInTheJoin()
    {
        using var ctx = MySqlTestContext.Create();

        // The eligible source: a correlated JSON_TABLE call wrapped only in the transparent column
        // projection of its own row type. MySQL correlates a function in FROM with the preceding tables
        // implicitly, so the derived wrapper (and the LATERAL keyword it would need) is removed and the
        // function itself becomes the join's FROM source.
        var cross = Prepare(
            ctx,
            ctx.From<IComplexEntity>()
                .CrossApply(row => T320MyJsonTableTvf.JsonTable(row.String!))
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id }),
            false);
        var crossSql = Normalize(cross.DbCommand.CommandText);
        output.WriteLine($"json_table cross apply: sql=<<{crossSql}>>; parameters=[{string.Join(", ", ParametersOf(cross))}]");

        crossSql.Should().Contain(" join json_table(t1.somestring", "the correlated function is the inner join's FROM source");
        crossSql.Should().Contain("'$[*]' columns(id int path '$.id', name varchar(50) path '$.name')", "the verbatim call clause survives the unwrap");
        crossSql.Should().Contain(") as `t2`", "the function keeps a collision-free alias of its own");
        crossSql.Should().Contain("t2.id", "every projected column is remapped to the function column structurally");
        crossSql.Should().NotContain("lateral", "MySQL has no LATERAL keyword in this position");
        crossSql.Should().NotContain("(select", "the derived wrapper is removed, so no projected subquery remains");
        crossSql.Should().NotContain("t3", "every reference to the removed derived alias is gone");
        ParametersOf(cross).Should().BeEmpty("the correlated column is a column reference, not a parameter");

        var outer = Prepare(
            ctx,
            ctx.From<IComplexEntity>()
                .OuterApply(row => T320MyJsonTableTvf.JsonTable(row.String!))
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id }),
            false);
        var outerSql = Normalize(outer.DbCommand.CommandText);
        output.WriteLine($"json_table outer apply: sql=<<{outerSql}>>; parameters=[{string.Join(", ", ParametersOf(outer))}]");

        outerSql.Should().Contain(" left join json_table(t1.somestring", "OUTER APPLY is the left join form over the function itself");
        outerSql.Should().Contain(" on true", "the outer join carries the ANSI ON TRUE condition and null extension");
        outerSql.Should().Contain("t2.id", "the downstream projection is remapped to the function column");
        outerSql.Should().NotContain("lateral", "MySQL has no LATERAL keyword in this position");
        outerSql.Should().NotContain("(select", "the derived wrapper is removed, so no projected subquery remains");
        outerSql.Should().NotContain("t3", "every reference to the removed derived alias is gone");
    }

    [Fact]
    public void RuntimeTvf_A_IneligibleApplySources_ShouldKeepTheDerivedRendering()
    {
        using var ctx = MySqlTestContext.Create();

        // (a) Uncorrelated: the source never reads the outer row, so it is outside the rewrite's scope.
        var document = "[]";
        var uncorrelated = Prepare(
            ctx,
            ctx.From<ISimpleEntity>()
                .CrossApply(row => T320MyJsonTableTvf.JsonTable(document))
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id }),
            false);
        var uncorrelatedSql = Normalize(uncorrelated.DbCommand.CommandText);
        output.WriteLine($"uncorrelated json_table apply: sql=<<{uncorrelatedSql}>>; parameters=[{string.Join(", ", ParametersOf(uncorrelated))}]");

        uncorrelatedSql.Should().Contain(" cross join lateral (select", "an uncorrelated source keeps the existing derived rendering");
        uncorrelatedSql.Should().Contain("from json_table(@document", "the function is still the derived table's source");
        uncorrelatedSql.Should().NotContain(" join json_table(", "the direct form is reserved for a correlated eligible source");
        ParametersOf(uncorrelated).Should().Equal("document=[]");

        // (b) A function other than JSON_TABLE is outside the declared scope.
        var extra = 7;
        var other = Prepare(
            ctx,
            ctx.From<ISimpleEntity>()
                .CrossApply(row => T320MyTvf.EchoPair(row.Id, extra))
                .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id }),
            false);
        var otherSql = Normalize(other.DbCommand.CommandText);
        output.WriteLine($"non-json_table apply: sql=<<{otherSql}>>; parameters=[{string.Join(", ", ParametersOf(other))}]");

        otherSql.Should().Contain(" cross join lateral (select", "a non-JSON_TABLE function keeps the existing derived rendering");
        otherSql.Should().Contain("t1.id", "the correlated argument stays an outer column reference");
        otherSql.Should().NotContain(" join t320_echo_pair(", "only the bounded eligible source is rendered directly");
        ParametersOf(other).Should().Equal("extra=7");
    }

    // ---- J: ordinary lambda Join and a mixed source ---------------------------------------------

    [Fact]
    public void RuntimeTvf_J_OrdinaryJoinAndMixedSource_ShouldRenderTheTvfSource()
    {
        using var ctx = MySqlTestContext.Create();
        var joinId = 3L;

        var joined = Prepare(
            ctx,
            ctx.From<ISimpleEntity>()
                .Join(() => T320MyTvf.JoinSource(joinId), (left, right) => left.Id == right.Id)
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
            ctx.From(() => T320MyTvf.Echo(fromId))
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
        using var ctx = MySqlTestContext.Create();
        var id = 42L;

        // The composed projection changes the builder row type while the factory row type stays
        // ISimpleEntity: the source and its argument must survive that difference.
        var source = ctx.From(() => T320MyTvf.Echo(id).As(r => new { r.Id }));
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
        using var ctx = MySqlTestContext.Create();
        var id = 7L;

        // Upcast: the descriptor is usable through the EntityBuilder<T> static type.
        TvfEntityBuilder<ISimpleEntity> descriptor = T320MyTvf.Echo(id);
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
        var first = Prepare(ctx, ctx.From(() => T320MyTvf.Echo(id)).Select(r => r.Id), false);
        var second = Prepare(ctx, ctx.From(() => T320MyTvf.Echo(id)).Select(r => r.Id), false);
        var firstSql = Normalize(first.DbCommand.CommandText);
        var secondSql = Normalize(second.DbCommand.CommandText);
        output.WriteLine($"reuse: first=<<{firstSql}>>; second=<<{secondSql}>>; parameters=[{string.Join(", ", ParametersOf(second))}]");

        secondSql.Should().Be(firstSql, "reusing the same factory/descriptor shape renders the same statement");
        ParametersOf(second).Should().Equal(ParametersOf(first));
        ParametersOf(second).Should().Equal("id=7");
    }

    // ---- R: result placement ---------------------------------------------------------------------

    [Fact]
    public void RuntimeTvf_R_None_ShouldRenderTheBareCall()
    {
        using var ctx = MySqlTestContext.Create();

        // Caller-declared placement None means "the function supplies its own result shape": no leading
        // column list and no alias column list, just the call and its alias.
        var prepared = Prepare(ctx, ctx.From(() => T320MyTvf.Echo(42)).Select(r => r.Id), false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        output.WriteLine($"placement none: sql=<<{sql}>>");

        sql.Should().Be("select id\n from t320_echo(42) as `t1`");
    }

    [Fact]
    public void RuntimeTvf_R_LeadingAndAliasPlacements_ShouldBeNamedCapabilityRefusals()
    {
        using var ctx = MySqlTestContext.Create();

        // Matrix MX-MY is T/G/G: MySQL expresses neither a leading column-definition argument nor an
        // alias column list, so both placements are named capability refusals, not silently dropped.
        Action leading = () => Prepare(ctx, ctx.From(() => T320MyTvf.LeadingResultSchema(42)).Select(r => r.Id), false);
        leading.Should().Throw<NotSupportedException>()
            .WithMessage("*'t320_leading'*'LeadingArgument' form*not supported by this provider*");

        Action alias = () => Prepare(ctx, ctx.From(() => T320MyTvf.AliasResultSchema(42)).Select(r => r.Id), false);
        alias.Should().Throw<NotSupportedException>()
            .WithMessage("*'t320_alias'*'AliasColumnList' form*not supported by this provider*");
    }

    // ---- F: custom TVF versus built-in classification -------------------------------------------

    [Fact]
    public void RuntimeTvf_F_CustomJsonTableCallClause_ShouldRenderTheVerbatimColumnList()
    {
        using var ctx = MySqlTestContext.Create();
        var document = "[]";

        var prepared = Prepare(
            ctx,
            ctx.From(() => T320MyJsonTableTvf.JsonTable(document)).Select(r => new { r.Id, r.Name }),
            false);
        var sql = Normalize(prepared.DbCommand.CommandText);
        var parameters = ParametersOf(prepared);
        output.WriteLine($"custom json_table: sql=<<{sql}>>; parameters=[{string.Join(", ", parameters)}]");

        sql.Should().Be(
            "select id, name\n"
            + " from json_table(@document, '$[*]' columns(id int path '$.id', name varchar(50) path '$.name')) as `t1`",
            "a custom runtime TVF renders its verbatim call clause with the mapped projection");
        parameters.Should().Equal("document=[]");
    }

    [Fact]
    public void RuntimeTvf_F_BuiltInLookingName_ShouldBeCustomClassifiedAndBuiltInsGated()
    {
        using var ctx = MySqlTestContext.Create();

        // A built-in name routed through the built-in surface is gated by the provider support list:
        // MySQL has no generate_series table function.
        Action builtIn = () => Prepare(
            ctx,
            ctx.FromTableFunction(() => SqlFunctions.Postgres.generate_series(1, 3)).Select(r => r.Value),
            false);
        builtIn.Should().Throw<NotSupportedException>()
            .WithMessage("*'generate_series'*not supported by this provider*");

        // The same SQL name through a custom runtime factory is emitted verbatim: a runtime descriptor is
        // a user-defined function, so the built-in capability gate must not apply to it.
        var custom = Prepare(ctx, ctx.From(() => T320MyTvf.CustomGenerateSeries(1, 3)).Select(r => r.Id), false);
        var sql = Normalize(custom.DbCommand.CommandText);
        output.WriteLine($"custom generate_series: sql=<<{sql}>>");

        sql.Should().Contain("from generate_series(1, 3) as `t1`", "a custom TVF is not gated by the built-in support list");
    }

    // ---- H/I: cache-hit refresh and SQL identity -------------------------------------------------

    [Fact]
    public void RuntimeTvf_H_CacheHit_ShouldRefreshTheCapturedArgument()
    {
        using var ctx = MySqlTestContext.Create();
        ctx.PurgeQueryCache();
        var id = 1L;

        QueryCommand<int> Build() => ctx.From(() => T320MyTvf.Echo(id)).Select(r => r.Id);

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
    public void RuntimeTvf_I_DifferentSqlIdentities_ShouldNotShareAPlan()
    {
        using var ctx = MySqlTestContext.Create();
        ctx.PurgeQueryCache();

        var before = QueryPlanStore.Values.Count();

        var alpha = Prepare(ctx, ctx.From(() => T320MyTvf.IdentityAlpha("raw")).Select(r => r.Id), true);
        var afterAlpha = QueryPlanStore.Values.Count();

        var beta = Prepare(ctx, ctx.From(() => T320MyTvf.IdentityBeta("raw")).Select(r => r.Id), true);
        var afterBeta = QueryPlanStore.Values.Count();

        var schema = Prepare(ctx, ctx.From(() => T320MyTvf.IdentityOtherSchema("raw")).Select(r => r.Id), true);
        var verbatim = Prepare(ctx, ctx.From(() => T320MyTvf.IdentityVerbatim("raw")).Select(r => r.Id), true);
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
    }

    [Fact]
    public void RuntimeTvf_I_EquivalentSqlIdentities_ShouldShareOnePlan()
    {
        using var ctx = MySqlTestContext.Create();
        ctx.PurgeQueryCache();
        var id = 5L;

        var before = QueryPlanStore.Values.Count();
        var one = Prepare(ctx, ctx.From(() => T320MyTvf.EquivalentOne(id)).Select(r => r.Id), true);
        var afterOne = QueryPlanStore.Values.Count();
        var two = Prepare(ctx, ctx.From(() => T320MyTvf.EquivalentTwo(id)).Select(r => r.Id), true);
        var afterTwo = QueryPlanStore.Values.Count();

        output.WriteLine(
            $"equivalent identities: plan store {before} -> {afterOne} -> {afterTwo}; ReferenceEquals={ReferenceEquals(one, two)}; "
            + $"sql=<<{Normalize(two.DbCommand.CommandText)}>>");

        afterOne.Should().Be(before + 1);
        afterTwo.Should().Be(afterOne, "equivalent metadata and argument shapes must share one plan entry");
        ReferenceEquals(one, two).Should().BeTrue(
            "two distinct factories with the same SQL identity must reuse the cached plan instance");
        Normalize(two.DbCommand.CommandText).Should().Be(Normalize(one.DbCommand.CommandText));
        ParametersOf(two).Should().Equal(ParametersOf(one));
        ParametersOf(two).Should().Equal("id=5");
    }
}
